using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixImportPlan;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// The ONLY Skills Matrix code that writes to a database: loads a READY import plan into migration 004's
    /// tables in a single transaction, makes it the current snapshot, and leaves earlier snapshots unchanged.
    ///
    /// Safety:
    ///   * The caller supplies the (already open) connection; this class never chooses a database itself.
    ///   * Refuses unless the plan has no errors, every acknowledgement is given, and the typed confirmation
    ///     matches ExpectedConfirmation(plan).
    ///   * Before inserting anything, re-reads every confirmed employee from the target database and refuses if
    ///     one is missing or its name / archived status differs from the reviewed employee list.
    ///   * One transaction with SET LOCAL search_path / lock_timeout (as the migration): on any error, nothing
    ///     is kept. The database's own triggers then re-check the snapshot rules at COMMIT.
    ///   * Only INSERTs into the new tables and UPDATEs of SkillMatrixImports.LoadState / IsCurrent - the
    ///     only changes migration 004 allows. No existing table is written.
    ///   * When the live copy exists (migration 005), the same transaction rebuilds it from the new import
    ///     (SkillMatrixLive_Seed): edits made in the app since the last rebuild are replaced and stay in the change
    ///     log (decision 2026-11-11: the launch re-import resets the live copy). If that fails, nothing is kept.
    ///   * With a merge (SkillsMatrixMerge, decided by the person importing): after the rebuild, the app's values
    ///     chosen to be kept are put back, each logged. Refused if the live copy changed after the comparison
    ///     (the live cells are locked against saves for the rest of the transaction).
    /// In the application the import step is switched off (SkillsMatrixImportForm.ImportExecutionEnabled).
    /// </summary>
    public static class SkillsMatrixImportWriter
    {
        /// <summary>LiveCellsRebuilt: cells in the rebuilt live copy, or null when the live copy (migration 005) is not installed.</summary>
        public sealed record WriteResult(long ImportId, int Areas, int Skills, int Columns, int Cells, int Formats, TimeSpan Took,
            int? LiveCellsRebuilt = null, int AppValuesKept = 0);

        /// <summary>
        /// The connection failed while committing, so it is not known whether the server committed the import.
        /// Check SkillMatrixImports before trying again.
        /// </summary>
        public sealed class CommitOutcomeUnknownException : Exception
        {
            public CommitOutcomeUnknownException(Exception inner)
                : base("The connection failed while the import was being committed, so it is NOT KNOWN whether it was saved. " +
                       "Check SkillMatrixImports (the newest row and which import is current) before trying again. " + inner.Message, inner) { }
        }

        /// <summary>The text a person must type to confirm, e.g. "IMPORT 82DFD157".</summary>
        public static string ExpectedConfirmation(ImportPlan plan) => "IMPORT " + plan.Import.SourceSha256[..8].ToUpperInvariant();

        public static WriteResult Write(NpgsqlConnection conn, ImportPlan plan, IReadOnlySet<string> acknowledged, string typedConfirmation,
                                        long? importedByEmployeeId = null, SkillsMatrixMerge.MergePlan? merge = null)
        {
            if (!plan.IsReady(acknowledged))
                throw new InvalidOperationException("The import plan is not ready (blocking errors or missing acknowledgements). Nothing was written.");
            if (!string.Equals(typedConfirmation?.Trim(), ExpectedConfirmation(plan), StringComparison.Ordinal))
                throw new InvalidOperationException($"Confirmation text did not match '{ExpectedConfirmation(plan)}'. Nothing was written.");
            var recheck = ValidateAgainstSchema(plan);
            if (recheck.Count > 0)
                throw new InvalidOperationException("The plan breaks migration 004's rules: " + recheck[0] + " Nothing was written.");
            if (conn.State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("The connection must be open.");

            var timer = Stopwatch.StartNew();
            using var tx = conn.BeginTransaction();
            try
            {
                Exec(conn, tx, "SET LOCAL search_path = pg_catalog, pg_temp; SET LOCAL lock_timeout = '5s';");
                if (Scalar(conn, tx, "SELECT to_regclass('public.skillmatriximports') IS NOT NULL") is not true)
                    throw new InvalidOperationException("Migration 004 is not installed in this database. Nothing was written.");
                VerifyEmployees(conn, tx, plan);   // before anything is inserted

                // Merge: the live copy must be exactly as compared; no saves from here until COMMIT
                if (merge != null)
                {
                    Exec(conn, tx, "LOCK TABLE public.SkillMatrixLiveCells IN SHARE ROW EXCLUSIVE MODE");
                    long lastChange = Convert.ToInt64(Scalar(conn, tx, "SELECT COALESCE(MAX(ChangeID), 0) FROM public.SkillMatrixLiveChanges"));
                    if (lastChange != merge.LastChangeId)
                        throw new InvalidOperationException("The live copy has changed since the comparison (someone saved a change). " +
                                                            "Compare with the live copy again. Nothing was written.");
                }
                var allSkillIds = new Dictionary<long, long>();    // plan skill key  -> snapshot SkillID
                var allColumnIds = new Dictionary<long, long>();   // plan column key -> snapshot ColumnID

                var i = plan.Import;
                long importId = (long)Scalar(conn, tx,
                    "INSERT INTO public.SkillMatrixImports (SourceFileName, SourceSha256, SourceVersion, ApprovedBy, ImportedBy, Notes, ColourMethod, ColourVerification) " +
                    "VALUES (@f, @sha, @ver, @appr, @imp, @notes, @cm, @cv) RETURNING ImportID",
                    ("f", i.SourceFileName), ("sha", i.SourceSha256), ("ver", i.SourceVersion), ("appr", i.ApprovedBy), ("imp", i.ImportedBy),
                    ("notes", i.Notes), ("cm", i.ColourMethod), ("cv", i.ColourVerification))!;

                int skills = 0, columns = 0, cells = 0, formats = 0;
                foreach (var a in plan.Areas)
                {
                    long scaleId = (long)Scalar(conn, tx,
                        "INSERT INTO public.SkillMatrixScales (ImportID, Name, LegendStatus, LegendNote, SourceRef) VALUES (@imp, @n, @st, @note, @ref) RETURNING ScaleID",
                        ("imp", importId), ("n", a.Scale.Name), ("st", a.Scale.LegendStatus), ("note", a.Scale.LegendNote), ("ref", a.Scale.SourceRef))!;
                    Batch(conn, tx, a.Scale.Levels.Select(l => Cmd(
                        "INSERT INTO public.SkillMatrixScaleLevels (ScaleID, KeyNumber, Level, Label, Description, SourceRef) VALUES (@s, @k, @l, @lab, @d, @ref)",
                        ("s", scaleId), ("k", (short)l.KeyNumber), ("l", (short)l.Level), ("lab", l.Label), ("d", l.Description), ("ref", l.SourceRef))));

                    long areaId = (long)Scalar(conn, tx,
                        "INSERT INTO public.SkillMatrixAreas (ImportID, ScaleID, Name, SourceSheet, DisplayOrder, SkillHeaderRef, InabHeaderRef) " +
                        "VALUES (@imp, @s, @n, @sheet, @o, @sh, @ih) RETURNING AreaID",
                        ("imp", importId), ("s", scaleId), ("n", a.Name), ("sheet", a.Sheet), ("o", a.DisplayOrder), ("sh", a.SkillHeaderRef), ("ih", a.InabHeaderRef))!;

                    var categoryIds = new Dictionary<long, long>();
                    foreach (var c in a.Categories)
                        categoryIds[c.Key] = (long)Scalar(conn, tx,
                            "INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (@a, @n, @o, @ref) RETURNING CategoryID",
                            ("a", areaId), ("n", c.Name), ("o", c.DisplayOrder), ("ref", c.SourceRef))!;

                    var skillIds = new Dictionary<long, long>();
                    foreach (var k in a.Skills)
                        skillIds[k.Key] = (long)Scalar(conn, tx,
                            "INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, InabAccredited, InabRaw, InabSourceRef, HiddenInSource, DisplayOrder, SourceRow, SourceRef) " +
                            "VALUES (@a, @c, @n, @ia, @ir, @iref, @h, @o, @row, @ref) RETURNING SkillID",
                            ("a", areaId), ("c", categoryIds[k.CategoryKey]), ("n", k.Name), ("ia", k.InabAccredited), ("ir", k.InabRaw), ("iref", k.InabSourceRef),
                            ("h", k.HiddenInSource), ("o", k.DisplayOrder), ("row", k.SourceRow), ("ref", k.SourceRef))!;

                    var columnIds = new Dictionary<long, long>();
                    foreach (var c in a.Columns)
                        columnIds[c.Key] = (long)Scalar(conn, tx,
                            "INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, SubGroupLabel, SubGroupSourceRef, " +
                            "Disposition, EmployeeID, ExcludeReason, DisplayOrder) VALUES (@a, @col, @h, @href, @g, @gref, @disp, @e, @why, @o) RETURNING ColumnID",
                            ("a", areaId), ("col", c.SourceColumn), ("h", c.SourceHeaderText), ("href", c.HeaderSourceRef), ("g", c.SubGroupLabel),
                            ("gref", c.SubGroupSourceRef), ("disp", c.Disposition), ("e", c.EmployeeId), ("why", c.ExcludeReason), ("o", c.DisplayOrder))!;

                    Batch(conn, tx, a.Cells.Select(c => Cmd(
                        "INSERT INTO public.SkillMatrixCells (AreaID, SkillID, ColumnID, Status, Level, RawValue, FlagReason, SourceCell) VALUES (@a, @s, @c, @st, @l, @raw, @why, @ref)",
                        ("a", areaId), ("s", skillIds[c.SkillKey]), ("c", columnIds[c.ColumnKey]), ("st", c.Status),
                        ("l", c.Level is int lv ? (short?)lv : null), ("raw", c.RawValue), ("why", c.FlagReason), ("ref", c.SourceCell))));

                    Batch(conn, tx, a.Formats.Select(f => Cmd(
                        "INSERT INTO public.SkillMatrixCellFormats (AreaID, SourceRef, FillSource, FillRgb, Pattern, PatternColorRgb, PatternBackRgb, ConditionalRule, " +
                        "FontSource, FontRgb, FontRule, Bold) VALUES (@a, @ref, @fs, @fill, @pat, @pc, @pb, @rule, @fo, @font, @frule, @b)",
                        ("a", areaId), ("ref", f.Cell), ("fs", f.FillSource), ("fill", f.FillRgb), ("pat", f.Pattern), ("pc", f.PatternColorRgb), ("pb", f.PatternBackRgb),
                        ("rule", f.ConditionalRule), ("fo", f.FontSource), ("font", f.FontRgb), ("frule", f.FontRule), ("b", f.Bold))));

                    skills += a.Skills.Count; columns += a.Columns.Count; cells += a.Cells.Count; formats += a.Formats.Count;
                    foreach (var kv in skillIds) allSkillIds[kv.Key] = kv.Value;
                    foreach (var kv in columnIds) allColumnIds[kv.Key] = kv.Value;
                }

                // Complete -> swap current (old snapshots are kept, unchanged) -> commit (triggers re-check at commit)
                Exec(conn, tx, "UPDATE public.SkillMatrixImports SET LoadState = 'Complete' WHERE ImportID = @id", ("id", importId));
                Exec(conn, tx, "UPDATE public.SkillMatrixImports SET IsCurrent = FALSE WHERE IsCurrent AND ImportID <> @id", ("id", importId));
                Exec(conn, tx, "UPDATE public.SkillMatrixImports SET IsCurrent = TRUE WHERE ImportID = @id", ("id", importId));

                // Live copy (migration 005): rebuild it from this import, in this transaction
                int? liveCells = null;
                if (Scalar(conn, tx, "SELECT to_regprocedure('public.skillmatrixlive_seed(bigint,bigint,text)') IS NOT NULL") is true)
                    liveCells = Convert.ToInt32(Scalar(conn, tx, "SELECT public.SkillMatrixLive_Seed(@id, @by, @name)",
                        ("id", importId), ("by", importedByEmployeeId), ("name", i.ImportedBy)));
                else if (merge != null)
                    throw new InvalidOperationException("A merge was requested but the live copy is not installed. Nothing was written.");

                // Merge: put back the app's values chosen to be kept (each logged)
                int kept = 0;
                foreach (var k in merge?.Keep ?? Array.Empty<SkillsMatrixMerge.Keep>())
                {
                    if (!allSkillIds.TryGetValue(k.PlanSkillKey, out long snapSkill) || !allColumnIds.TryGetValue(k.PlanColumnKey, out long snapCol))
                        throw new InvalidOperationException("A kept value refers to a skill or person that is not in this import. Compare again. Nothing was written.");
                    using var upd = new NpgsqlCommand(@"
                        UPDATE public.SkillMatrixLiveCells c
                        SET Status = @st, Level = @lv, RawValue = NULL, FlagReason = NULL,
                            UpdatedAt = @at, UpdatedByEmployeeID = @byId, UpdatedByName = @byName
                        FROM public.SkillMatrixLiveSkills s, public.SkillMatrixLiveColumns col, public.SkillMatrixLiveAreas a, public.Employees e
                        WHERE s.SkillID = c.SkillID AND col.ColumnID = c.ColumnID AND a.AreaID = c.AreaID AND e.EmployeeID = col.EmployeeID
                          AND s.SourceSkillID = @sk AND col.SourceColumnID = @col
                        RETURNING c.AreaID, a.Name, c.SkillID, s.Name, c.ColumnID, col.EmployeeID, e.FullName", conn, tx);
                    foreach (var p in new[] { Param("st", k.Level.HasValue ? "Level" : "Unassessed"), Param("lv", k.Level.HasValue ? (short?)k.Level.Value : null),
                                              Param("at", k.ChangedAt), Param("byId", k.ChangedByEmployeeId), Param("byName", k.ChangedBy),
                                              Param("sk", snapSkill), Param("col", snapCol) })
                        upd.Parameters.Add(p);
                    var rows = new List<(long Area, string AreaName, long Skill, string SkillName, long Col, long Emp, string EmpName)>();
                    using (var r = upd.ExecuteReader())
                        while (r.Read())
                            rows.Add((r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetString(3), r.GetInt64(4), Convert.ToInt64(r.GetValue(5)), r.GetString(6)));
                    if (rows.Count != 1)
                        throw new InvalidOperationException("A kept value could not be placed in the rebuilt live copy. Nothing was written.");
                    var x = rows[0];
                    Exec(conn, tx, @"
                        INSERT INTO public.SkillMatrixLiveChanges
                            (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, AreaID, AreaName, SkillID, SkillName,
                             ColumnID, EmployeeID, EmployeeName, OldValue, NewValue, Note)
                        VALUES (@by, @byName, @imp, 'SetCell', @a, @aName, @s, @sName, @c, @e, @eName, @old, @new, @note)",
                        ("by", importedByEmployeeId), ("byName", i.ImportedBy), ("imp", importId), ("a", x.Area), ("aName", x.AreaName),
                        ("s", x.Skill), ("sName", x.SkillName), ("c", x.Col), ("e", x.Emp), ("eName", x.EmpName),
                        ("old", k.ExcelValue), ("new", k.AppValue),
                        ("note", $"Kept the app's value over the workbook (import {importId}); changed in the app by {k.ChangedBy} on {k.ChangedAt:yyyy-MM-dd HH:mm}"));
                    kept++;
                }
                try
                {
                    tx.Commit();
                }
                catch (Exception ex) when (ex is not PostgresException)
                {
                    // A PostgresException here means the server rejected the commit (rolled back, nothing kept).
                    // Anything else (network, timeout) leaves the outcome unknown.
                    throw new CommitOutcomeUnknownException(ex);
                }
                return new WriteResult(importId, plan.Areas.Count, skills, columns, cells, formats, timer.Elapsed, liveCells, kept);
            }
            catch
            {
                try { tx.Rollback(); } catch { /* connection may already be broken; the server rolls back */ }
                throw;
            }
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Every confirmed employee must exist in THIS database with the same name (and, where the reviewed list
        /// gave one, the same archived status) as in the employee list the decisions were reviewed against.
        /// Read-only (SELECT ... FOR KEY SHARE: the rows cannot be deleted while the import runs). Throws, before
        /// anything is inserted, if any differ - e.g. a stale or wrong export.
        /// </summary>
        private static void VerifyEmployees(NpgsqlConnection conn, NpgsqlTransaction tx, ImportPlan plan)
        {
            var expected = plan.Areas.SelectMany(a => a.Columns).Where(c => c.Disposition == "Member" && c.EmployeeId != null)
                               .GroupBy(c => c.EmployeeId!.Value).ToDictionary(g => g.Key, g => g.First());
            if (expected.Count == 0) return;

            var found = new Dictionary<long, (string? Name, bool Archived)>();
            using (var cmd = new NpgsqlCommand(
                "SELECT EmployeeID, FullName, IsArchived FROM public.Employees WHERE EmployeeID = ANY(@ids) FOR KEY SHARE", conn, tx))
            {
                cmd.Parameters.AddWithValue("ids", expected.Keys.ToArray());
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    found[Convert.ToInt64(r.GetValue(0))] = (r.IsDBNull(1) ? null : r.GetString(1), !r.IsDBNull(2) && r.GetBoolean(2));
            }

            var problems = new List<string>();
            foreach (var (id, col) in expected.OrderBy(kv => kv.Key))
            {
                if (!found.TryGetValue(id, out var db))
                { problems.Add($"employee {id} does not exist in this database"); continue; }
                if (!string.Equals(db.Name?.Trim(), col.EmployeeName?.Trim(), StringComparison.Ordinal))
                    problems.Add($"employee {id} is '{db.Name}' in this database but '{col.EmployeeName}' in the reviewed employee list");
                if (col.EmployeeArchived is bool archived && archived != db.Archived)
                    problems.Add($"employee {id} is {(db.Archived ? "archived" : "not archived")} in this database but " +
                                 $"{(archived ? "archived" : "not archived")} in the reviewed employee list");
            }
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    "The employee records in this database differ from the employee list the decisions were reviewed against, so nothing was written. " +
                    "Export the employee list again and re-run Prepare import. " + string.Join("; ", problems.Take(10)) +
                    (problems.Count > 10 ? $"; and {problems.Count - 10} more." : "."));
        }

        private static NpgsqlBatchCommand Cmd(string sql, params (string Name, object? Value)[] p)
        {
            var c = new NpgsqlBatchCommand(sql);
            foreach (var (n, v) in p) c.Parameters.Add(Param(n, v));
            return c;
        }

        private static void Batch(NpgsqlConnection conn, NpgsqlTransaction tx, IEnumerable<NpgsqlBatchCommand> commands)
        {
            foreach (var chunk in commands.Chunk(250))
            {
                using var batch = new NpgsqlBatch(conn, tx);
                foreach (var c in chunk) batch.BatchCommands.Add(c);
                batch.ExecuteNonQuery();
            }
        }

        private static object? Scalar(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object? Value)[] p)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            foreach (var (n, v) in p) cmd.Parameters.Add(Param(n, v));
            return cmd.ExecuteScalar();
        }

        private static void Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object? Value)[] p)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            foreach (var (n, v) in p) cmd.Parameters.Add(Param(n, v));
            cmd.ExecuteNonQuery();
        }

        // NULLs are sent untyped, so PostgreSQL takes the type from the target column (bigint, boolean, smallint, text)
        private static NpgsqlParameter Param(string name, object? value) => value switch
        {
            null => new NpgsqlParameter(name, NpgsqlDbType.Unknown) { Value = DBNull.Value },
            _ => new NpgsqlParameter(name, value),
        };
    }
}
