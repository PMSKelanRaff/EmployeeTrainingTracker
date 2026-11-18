using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixService;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// The EDITABLE live copy of the Skills Matrix (migration 005): reads a sheet of the live copy and saves level
    /// changes with a change-log entry each. The imported snapshots (migration 004) are only read, never written.
    ///
    /// Saving (SaveCells) is all-or-nothing, in one transaction:
    ///   * re-reads the editor from the database: an active Admin may change any level; an active Manager only the
    ///     levels on the sheets where a workbook team heading names them (migration 007), never their own
    ///     (application-level check, as everywhere else in the app - not database-enforced security; see docs/SkillsMatrix.md);
    ///   * locks the affected cells and refuses the whole save if any of them changed since it was read (Version),
    ///     reporting who changed it and when, so nothing is silently overwritten;
    ///   * only levels 1-5 or 'not assessed' can be saved; a flagged cell can be resolved to one of those.
    /// The core methods take an open connection (tests run them against a disposable database); the
    /// application uses the wrappers, which connect with DatabaseHelper.
    /// </summary>
    public static class SkillsMatrixLiveService
    {
        public sealed record LiveState(bool Installed, long? BasedOnImportId, DateTime? SeededAt, string? SeededBy);

        /// <summary>
        /// Who is using the matrix. Admins: every sheet and person, edit everything.
        /// Managers (decision 2026-11-18): the matrix follows the WORKBOOK's chain of command, not the app's groups.
        /// A Manager sees the whole sheet(s) on which a team heading names them (SkillMatrixTeamManagers, migration
        /// 007) - every column, as in Excel - and can change everyone's levels there except their own. No other
        /// sheets. Re-read from the database on every call. ManagedAreas holds LIVE area IDs.
        /// </summary>
        public sealed record Access(int EmployeeId, string Name, bool IsAdmin, IReadOnlySet<long> ManagedAreas)
        {
            public bool CanSeeArea(long areaId) => IsAdmin || ManagedAreas.Contains(areaId);
            public bool CanEdit(long areaId, long employeeId) => IsAdmin || (employeeId != EmployeeId && ManagedAreas.Contains(areaId));
        }

        /// <summary>
        /// One cell to save. NewLevel null = not assessed. ExpectedVersion = the Version that was read.
        /// Note: stored with the change-log entry (e.g. "Undo of change 12").
        /// </summary>
        public sealed record CellChange(long SkillId, long ColumnId, int ExpectedVersion, int? NewLevel, string? Note = null);

        /// <summary>A cell that someone else changed after it was read: the save was refused.</summary>
        public sealed record CellConflict(long SkillId, long ColumnId, string SkillName, string EmployeeName,
            string CurrentValue, string? ChangedBy, DateTime? ChangedAt);

        public sealed record SaveResult(int Saved, IReadOnlyList<CellConflict> Conflicts)
        {
            public bool Succeeded => Conflicts.Count == 0;
        }

        public sealed record ChangeEntry(DateTime ChangedAt, string ChangedBy, string Action, string? SkillName,
            string? EmployeeName, string? OldValue, string? NewValue, string? Note,
            long ChangeId = 0, long? AreaId = null, long? SkillId = null, long? ColumnId = null);

        public const string NotAssessedText = "not assessed";

        /// <summary>Format key of a level's key colour in SkillMatrixData.Formats / legend SourceRef (migration 006).</summary>
        public static string LevelKeyRef(int level) => "#level" + level;

        // ---- Application wrappers (DatabaseHelper connection; access re-checked on every call) -------------

        public static Access GetAccess(int employeeId)
        {
            using var conn = Open();
            return GetAccess(conn, null, employeeId);
        }

        public static LiveState GetState(int employeeId)
        {
            using var conn = Open();
            GetAccess(conn, null, employeeId);
            return GetState(conn);
        }

        public static List<SkillAreaInfo> GetAreas(int employeeId)
        {
            using var conn = Open();
            return GetAreas(conn, GetAccess(conn, null, employeeId));
        }

        public static SkillMatrixData GetMatrix(int employeeId, long liveAreaId, bool includeArchived = false)
        {
            using var conn = Open();
            return GetMatrix(conn, liveAreaId, GetAccess(conn, null, employeeId), includeArchived);
        }

        public static SaveResult SaveCells(int editorEmployeeId, long liveAreaId, IReadOnlyList<CellChange> changes)
        {
            using var conn = Open();
            return SaveCells(conn, editorEmployeeId, liveAreaId, changes);
        }

        public static List<ChangeEntry> GetChanges(int employeeId, long liveAreaId, int limit = 1000)
        {
            using var conn = Open();
            return GetChanges(conn, liveAreaId, limit, GetAccess(conn, null, employeeId));
        }

        /// <summary>
        /// Level changes made in the app since the live copy was last built (what a new import would replace),
        /// or null when the live copy is not installed.
        /// </summary>
        public static int? EditsSinceRebuild(NpgsqlConnection conn)
        {
            if (!Exists(conn, "public.skillmatrixlivechanges")) return null;
            using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM public.SkillMatrixLiveChanges
                WHERE Action <> 'Reseed'
                  AND ChangeID > COALESCE((SELECT MAX(ChangeID) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed'), 0)", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static SaveResult UndoChange(int editorEmployeeId, long changeId)
        {
            using var conn = Open();
            return UndoChange(conn, editorEmployeeId, changeId);
        }

        private static NpgsqlConnection Open()
        {
            var conn = DatabaseHelper.GetConnection();
            conn.Open();
            return conn;
        }

        // ---- Access -----------------------------------------------------------------------------------

        /// <summary>
        /// The caller's access: an active (non-archived) Admin or Manager. Throws UnauthorizedAccessException otherwise.
        /// </summary>
        public static Access GetAccess(NpgsqlConnection conn, NpgsqlTransaction? tx, int employeeId)
        {
            string name;
            bool isAdmin, isManager;
            using (var cmd = new NpgsqlCommand(@"
                SELECT e.FullName,
                       EXISTS (SELECT 1 FROM public.Users u WHERE u.EmployeeID = e.EmployeeID AND u.Role = 'Admin'),
                       EXISTS (SELECT 1 FROM public.Users u WHERE u.EmployeeID = e.EmployeeID AND u.Role = 'Manager')
                FROM public.Employees e WHERE e.EmployeeID = @id AND NOT e.IsArchived", conn, tx))
            {
                cmd.Parameters.AddWithValue("id", (long)employeeId);
                using var r = cmd.ExecuteReader();
                if (!r.Read() || !(r.GetBoolean(1) || r.GetBoolean(2)))
                    throw new UnauthorizedAccessException("The Skills Matrix is available to Admins and Managers only.");
                name = r.GetString(0); isAdmin = r.GetBoolean(1); isManager = r.GetBoolean(2);
            }
            // A Manager's sheets: those with a (not removed) column under a team heading that names them.
            // Before migration 007 is installed there are no team managers, so a Manager sees no sheets.
            var areas = new HashSet<long>();
            if (!isAdmin && isManager && Exists(conn, tx, "public.skillmatrixteammanagers"))
            {
                using var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT c.AreaID FROM public.SkillMatrixLiveColumns c
                    JOIN public.SkillMatrixTeamManagers tm ON tm.HeadingText = c.SubGroupLabel
                    WHERE tm.EmployeeID = @id AND NOT c.IsDeleted", conn, tx);
                cmd.Parameters.AddWithValue("id", (long)employeeId);
                using var r = cmd.ExecuteReader();
                while (r.Read()) areas.Add(r.GetInt64(0));
            }
            return new Access(employeeId, name, isAdmin, areas);
        }

        // ---- Reads ------------------------------------------------------------------------------------

        /// <summary>Whether migration 005 is installed and which import the live copy was built from.</summary>
        public static LiveState GetState(NpgsqlConnection conn)
        {
            using (var check = new NpgsqlCommand("SELECT to_regclass('public.skillmatrixlivestate') IS NOT NULL", conn))
                if (check.ExecuteScalar() is not true)
                    return new LiveState(false, null, null, null);

            using var cmd = new NpgsqlCommand("SELECT BasedOnImportID, SeededAt, SeededBy FROM public.SkillMatrixLiveState", conn);
            using var r = cmd.ExecuteReader();
            return r.Read()
                ? new LiveState(true, r.GetInt64(0), r.GetDateTime(1), r.GetString(2))
                : new LiveState(true, null, null, null);
        }

        /// <summary>
        /// Sheets of the live copy, in order. AreaId is the LIVE area ID. With a Manager's access: only the sheets
        /// on which they manage a team. (null access = everything, as for an Admin.)
        /// </summary>
        public static List<SkillAreaInfo> GetAreas(NpgsqlConnection conn, Access? access = null)
        {
            bool filter = access is { IsAdmin: false };
            using var cmd = new NpgsqlCommand($@"
                SELECT la.AreaID, la.Name, a.SourceSheet, a.SkillHeaderRef, a.InabHeaderRef
                FROM public.SkillMatrixLiveAreas la
                JOIN public.SkillMatrixAreas a ON a.AreaID = la.SourceAreaID
                {(filter ? "WHERE la.AreaID = ANY(@areas)" : "")}
                ORDER BY la.DisplayOrder, la.AreaID", conn);
            if (filter) cmd.Parameters.AddWithValue("areas", access!.ManagedAreas.ToArray());
            var areas = new List<SkillAreaInfo>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                areas.Add(new SkillAreaInfo(r.GetInt64(0), r.GetString(1), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4)));
            return areas;
        }

        /// <summary>
        /// One sheet of the live copy, in the same shape as an imported sheet: IDs are LIVE IDs; legend, workbook
        /// references and formatting come from the import the sheet was copied from. Deleted rows are left out.
        /// </summary>
        /// <summary>
        /// People archived in the Employees tab are left out (their levels are kept: unarchived, they reappear),
        /// unless includeArchived (shown read-only; SaveCells refuses them).
        /// </summary>
        public static SkillMatrixData GetMatrix(NpgsqlConnection conn, long liveAreaId, Access? access = null, bool includeArchived = false)
        {
            if (access != null && !access.CanSeeArea(liveAreaId))
                throw new UnauthorizedAccessException(NotYourSheet);
            SkillAreaInfo area;
            long sourceAreaId, scaleId;
            string scaleName, scaleRef;
            bool needsConfirmation;
            string? legendNote;
            using (var cmd = new NpgsqlCommand(@"
                SELECT la.AreaID, la.Name, a.SourceSheet, a.SkillHeaderRef, a.InabHeaderRef, a.AreaID,
                       s.ScaleID, s.Name, s.LegendStatus, s.LegendNote, s.SourceRef
                FROM public.SkillMatrixLiveAreas la
                JOIN public.SkillMatrixAreas a  ON a.AreaID = la.SourceAreaID
                JOIN public.SkillMatrixScales s ON s.ScaleID = a.ScaleID AND s.ImportID = a.ImportID
                WHERE la.AreaID = @id", conn))
            {
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                    throw new InvalidOperationException("That sheet is not part of the Skills Matrix (it may have been re-imported). Refresh the tab.");
                area = new SkillAreaInfo(r.GetInt64(0), r.GetString(1), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4));
                sourceAreaId = r.GetInt64(5);
                scaleId = r.GetInt64(6);
                scaleName = r.GetString(7);
                needsConfirmation = r.GetString(8) == "NeedsConfirmation";
                legendNote = r.IsDBNull(9) ? null : r.GetString(9);
                scaleRef = r.GetString(10);
            }

            bool hasKeys = Exists(conn, "public.skillmatrixlivelevels");
            var formats = SkillsMatrixService.ReadFormats(conn, null, sourceAreaId);
            var entries = new List<SkillLegendEntry>();
            if (hasKeys)
            {
                // Migration 006: one wording per level for this live sheet, with the key colours as synthetic
                // format entries ("#level4"), so key chips and edited cells use them like any workbook cell.
                using var cmd = new NpgsqlCommand(@"
                    SELECT Level, Label, Description, FillRgb, FontRgb FROM public.SkillMatrixLiveLevels
                    WHERE AreaID = @id ORDER BY Level", conn);
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    int level = r.GetInt16(0);
                    entries.Add(new SkillLegendEntry(1, level, r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), LevelKeyRef(level)));
                    var fill = r.IsDBNull(3) ? (System.Drawing.Color?)null : FromHex(r.GetString(3));
                    formats[LevelKeyRef(level)] = new SkillsMatrixWorkbookColours.CellDisplay(
                        fill, (r.IsDBNull(4) ? null : FromHex(r.GetString(4))) ?? System.Drawing.Color.Black, false,
                        fill == null ? null : "solid", null, null, fill == null ? "None" : "CellStyle");
                }
                needsConfirmation = false;   // the live key has one wording per level (see migration 006)
                legendNote = null;
            }
            else
            {
                using var cmd = new NpgsqlCommand(@"
                    SELECT KeyNumber, Level, Label, Description, SourceRef FROM public.SkillMatrixScaleLevels
                    WHERE ScaleID = @id ORDER BY KeyNumber, Level", conn);
                cmd.Parameters.AddWithValue("id", scaleId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    entries.Add(new SkillLegendEntry(r.GetInt16(0), r.GetInt16(1), r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4)));
            }

            var categories = new List<SkillCategoryInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT lc.CategoryID, lc.Name, COALESCE(c.SourceRef, '')
                FROM public.SkillMatrixLiveCategories lc
                LEFT JOIN public.SkillMatrixCategories c ON c.CategoryID = lc.SourceCategoryID
                WHERE lc.AreaID = @id AND NOT lc.IsDeleted
                ORDER BY lc.DisplayOrder, lc.CategoryID", conn))
            {
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    categories.Add(new SkillCategoryInfo(r.GetInt64(0), r.GetString(1), r.GetString(2)));
            }

            var skills = new List<SkillRowInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT ls.SkillID, ls.CategoryID, ls.Name, ls.InabAccredited, ls.InabRaw, ls.HiddenInSource,
                       COALESCE(s.SourceRef, ''), s.InabSourceRef
                FROM public.SkillMatrixLiveSkills ls
                LEFT JOIN public.SkillMatrixSkills s ON s.SkillID = ls.SourceSkillID
                WHERE ls.AreaID = @id AND NOT ls.IsDeleted
                ORDER BY ls.DisplayOrder, ls.SkillID", conn))
            {
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    skills.Add(new SkillRowInfo(r.GetInt64(0), r.GetInt64(1), r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetBoolean(3), r.IsDBNull(4) ? null : r.GetString(4),
                        r.GetBoolean(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
            }

            // Team row (people with a team heading only):
            //   1. a heading that names its manager -> that name (SkillMatrixLiveTeamLabels, migration 006);
            //   2. otherwise (e.g. 'Support Lab Staff') -> the person's manager(s) from the app's groups, leaving out
            //      groups they manage themselves (several -> 'A / B'), so it follows the Employees / Departments tabs;
            //   3. otherwise (in no group) -> the heading as written.
            const string groupManager = @"(SELECT string_agg(DISTINCT m.FullName, ' / ') FROM public.GroupMembers gm
                    JOIN public.Groups g ON g.GroupID = gm.GroupID JOIN public.Employees m ON m.EmployeeID = g.ManagerID
                    WHERE gm.EmployeeID = lc.EmployeeID AND g.ManagerID <> lc.EmployeeID)";
            bool hasTeamNames = Exists(conn, "public.skillmatrixliveteamlabels");
            string teamLabel = hasTeamNames
                ? $"CASE WHEN lc.SubGroupLabel IS NULL THEN NULL ELSE COALESCE(t.DisplayLabel, {groupManager}, lc.SubGroupLabel) END"
                : "lc.SubGroupLabel";
            string teamJoin = hasTeamNames
                ? "LEFT JOIN public.SkillMatrixLiveTeamLabels t ON t.HeadingText = lc.SubGroupLabel" : "";
            var members = new List<SkillMemberInfo>();
            using (var cmd = new NpgsqlCommand($@"
                SELECT lc.ColumnID, lc.EmployeeID, e.FullName, e.IsArchived, COALESCE(lc.HeaderText, e.FullName),
                       {teamLabel}, COALESCE(c.SourceColumn, ''), c.HeaderSourceRef, c.SubGroupSourceRef
                FROM public.SkillMatrixLiveColumns lc
                JOIN public.Employees e ON e.EmployeeID = lc.EmployeeID
                LEFT JOIN public.SkillMatrixColumns c ON c.ColumnID = lc.SourceColumnID
                {teamJoin}
                WHERE lc.AreaID = @id AND NOT lc.IsDeleted
                ORDER BY lc.DisplayOrder, lc.ColumnID", conn))
            {
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    members.Add(new SkillMemberInfo(r.GetInt64(0), Convert.ToInt64(r.GetValue(1)), r.GetString(2), r.GetBoolean(3),
                        r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6),
                        r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8)));
            }

            var cells = new Dictionary<(long, long), SkillCellInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT lc.SkillID, lc.ColumnID, lc.Status, lc.Level, lc.RawValue, lc.FlagReason, COALESCE(sc.SourceCell, ''),
                       lc.Version, lc.UpdatedAt, lc.UpdatedByName
                FROM public.SkillMatrixLiveCells lc
                LEFT JOIN public.SkillMatrixCells sc ON sc.CellID = lc.SourceCellID
                WHERE lc.AreaID = @id", conn))
            {
                cmd.Parameters.AddWithValue("id", liveAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    cells[(r.GetInt64(0), r.GetInt64(1))] = new SkillCellInfo(r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetInt16(3), r.IsDBNull(4) ? null : r.GetString(4),
                        r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6),
                        r.GetInt32(7), r.IsDBNull(8) ? null : r.GetDateTime(8), r.IsDBNull(9) ? null : r.GetString(9));
            }

            var excluded = new List<SkillExcludedColumn>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT SourceColumn, SourceHeaderText, ExcludeReason FROM public.SkillMatrixColumns
                WHERE AreaID = @id AND Disposition = 'Excluded' ORDER BY LENGTH(SourceColumn), SourceColumn", conn))
            {
                cmd.Parameters.AddWithValue("id", sourceAreaId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    excluded.Add(new SkillExcludedColumn(r.GetString(0), r.GetString(1), r.GetString(2)));
            }

            // A Manager sees the whole sheet (checked above), but not the import's excluded columns (import detail).
            // Archived people (Employees tab) are hidden unless asked for.
            if (access is { IsAdmin: false }) excluded = new List<SkillExcludedColumn>();
            if (!includeArchived)
            {
                members = members.Where(x => !x.EmployeeArchived).ToList();
                var shown = members.Select(x => x.MemberId).ToHashSet();
                cells = cells.Where(kv => shown.Contains(kv.Key.Item2)).ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            var legend = new SkillLegend(scaleName, needsConfirmation, legendNote, scaleRef, entries);
            return new SkillMatrixData(area, legend, categories, skills, members, cells, excluded, formats);
        }

        /// <summary>The change log for one live sheet (plus re-seeds), newest first.</summary>
        public static List<ChangeEntry> GetChanges(NpgsqlConnection conn, long liveAreaId, int limit = 1000, Access? access = null)
        {
            // A Manager sees the whole history of the sheets they manage a team on (and the rebuilds)
            if (access != null && !access.CanSeeArea(liveAreaId))
                throw new UnauthorizedAccessException(NotYourSheet);
            using var cmd = new NpgsqlCommand(@"
                SELECT ChangedAt, ChangedByName, Action, SkillName, EmployeeName, OldValue, NewValue, Note,
                       ChangeID, AreaID, SkillID, ColumnID
                FROM public.SkillMatrixLiveChanges
                WHERE Action = 'Reseed' OR AreaID = @id
                ORDER BY ChangedAt DESC, ChangeID DESC
                LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("id", liveAreaId);
            cmd.Parameters.AddWithValue("limit", limit);
            var list = new List<ChangeEntry>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                long? L(int i) => r.IsDBNull(i) ? null : r.GetInt64(i);
                list.Add(new ChangeEntry(r.GetDateTime(0), r.GetString(1), r.GetString(2), S(3), S(4), S(5), S(6), S(7),
                    r.GetInt64(8), L(9), L(10), L(11)));
            }
            return list;
        }

        /// <summary>
        /// Puts a cell back to the value it had before one logged level change. Only when the cell still holds that
        /// change's new value (otherwise it was changed again since, and the newer change must be undone first), and
        /// not for a resolved flagged value (a flagged value can only come from an import). Saved like any change,
        /// with the note "Undo of change N". Throws ArgumentException / InvalidOperationException when not possible.
        /// </summary>
        public static SaveResult UndoChange(NpgsqlConnection conn, int editorEmployeeId, long changeId)
        {
            long areaId, skillId, columnId;
            string? oldValue, newValue;
            using (var cmd = new NpgsqlCommand(@"
                SELECT Action, AreaID, SkillID, ColumnID, OldValue, NewValue FROM public.SkillMatrixLiveChanges WHERE ChangeID = @id", conn))
            {
                cmd.Parameters.AddWithValue("id", changeId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) throw new ArgumentException($"Change {changeId} was not found.");
                if (r.GetString(0) != "SetCell" || r.IsDBNull(1) || r.IsDBNull(2) || r.IsDBNull(3))
                    throw new ArgumentException("Only level changes can be undone.");
                areaId = r.GetInt64(1); skillId = r.GetInt64(2); columnId = r.GetInt64(3);
                oldValue = r.IsDBNull(4) ? null : r.GetString(4);
                newValue = r.IsDBNull(5) ? null : r.GetString(5);
            }

            int? restore;
            if (oldValue == NotAssessedText) restore = null;
            else if (int.TryParse(oldValue, out var lv) && lv is >= 1 and <= 5) restore = lv;
            else throw new ArgumentException($"This change replaced an imported flagged value ({oldValue}), which cannot be put back. " +
                                             "Set the level you want instead.");

            string status; int? level; string? raw; int version;
            using (var cmd = new NpgsqlCommand(@"
                SELECT Status, Level, RawValue, Version FROM public.SkillMatrixLiveCells WHERE SkillID = @s AND ColumnID = @c AND AreaID = @a", conn))
            {
                cmd.Parameters.AddWithValue("s", skillId);
                cmd.Parameters.AddWithValue("c", columnId);
                cmd.Parameters.AddWithValue("a", areaId);
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                    throw new InvalidOperationException("That cell is no longer in the live copy (it has been rebuilt from an import since).");
                status = r.GetString(0); level = r.IsDBNull(1) ? null : r.GetInt16(1); raw = r.IsDBNull(2) ? null : r.GetString(2); version = r.GetInt32(3);
            }
            string now = DescribeValue(status, level, raw);
            if (now != newValue)
                throw new InvalidOperationException($"The cell has been changed again since (it is now {now}, not {newValue}). " +
                                                    "Undo the newer change first, or set the level you want.");

            // Saved through SaveCells: same permission check, lock and version check (version read just now)
            return SaveCells(conn, editorEmployeeId, areaId, new[] { new CellChange(skillId, columnId, version, restore, $"Undo of change {changeId}") });
        }

        public const string NotYourSheet =
            "You can only open and change the Skills Matrix sheets on which you manage a team (as named in the workbook's team headings).";

        private static bool Exists(NpgsqlConnection conn, string table) => Exists(conn, null, table);

        private static bool Exists(NpgsqlConnection conn, NpgsqlTransaction? tx, string table)
        {
            using var cmd = new NpgsqlCommand("SELECT to_regclass(@t) IS NOT NULL", conn, tx);
            cmd.Parameters.AddWithValue("t", table);
            return cmd.ExecuteScalar() is true;
        }

        private static System.Drawing.Color? FromHex(string? rgb) =>
            rgb is { Length: 6 }
                ? System.Drawing.Color.FromArgb(Convert.ToInt32(rgb[0..2], 16), Convert.ToInt32(rgb[2..4], 16), Convert.ToInt32(rgb[4..6], 16))
                : null;

        // ---- Save -------------------------------------------------------------------------------------

        /// <summary>The cell's value as text, as the change log records it.</summary>
        public static string DescribeValue(string status, int? level, string? rawValue) => status switch
        {
            "Level" => level?.ToString() ?? "?",
            "Unassessed" => NotAssessedText,
            _ => $"flagged: {rawValue}",
        };

        /// <summary>
        /// Saves level changes for one sheet, all-or-nothing. Returns the number of cells changed, or - when any cell
        /// was changed by someone else since it was read - saves nothing and returns those conflicts.
        /// Throws UnauthorizedAccessException (not an active Admin) or ArgumentException (an invalid change).
        /// </summary>
        public static SaveResult SaveCells(NpgsqlConnection conn, int editorEmployeeId, long liveAreaId, IReadOnlyList<CellChange> changes)
        {
            if (changes.Count == 0) return new SaveResult(0, Array.Empty<CellConflict>());
            foreach (var c in changes)
                if (c.NewLevel is int lv && (lv < 1 || lv > 5))
                    throw new ArgumentException($"Level {lv} is not allowed: use 1-5, or not assessed. Nothing was saved.");
            if (changes.GroupBy(c => (c.SkillId, c.ColumnId)).Any(g => g.Count() > 1))
                throw new ArgumentException("The same cell appears twice in one save. Nothing was saved.");

            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmd = new NpgsqlCommand("SET LOCAL lock_timeout = '5s'", conn, tx)) cmd.ExecuteNonQuery();

                // Who is saving, and whose levels they may change: re-read from the database inside this transaction
                Access access;
                try { access = GetAccess(conn, tx, editorEmployeeId); }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException("Only Admins and Managers can edit the Skills Matrix. Nothing was saved.");
                }
                string editorName = access.Name;

                long basedOnImport;
                using (var cmd = new NpgsqlCommand("SELECT BasedOnImportID FROM public.SkillMatrixLiveState", conn, tx))
                    basedOnImport = cmd.ExecuteScalar() is long id ? id
                        : throw new InvalidOperationException("The Skills Matrix live copy has not been set up. Nothing was saved.");

                // Lock the cells being saved and read what they are now
                var current = new Dictionary<(long, long), (long AreaId, string AreaName, string Status, int? Level, string? Raw, int Version,
                    DateTime? UpdatedAt, string? UpdatedBy, string SkillName, bool SkillDeleted, long EmployeeId, string EmployeeName, bool ColumnDeleted,
                    bool EmployeeArchived)>();
                using (var cmd = new NpgsqlCommand(@"
                    SELECT c.SkillID, c.ColumnID, c.AreaID, a.Name, c.Status, c.Level, c.RawValue, c.Version, c.UpdatedAt, c.UpdatedByName,
                           s.Name, s.IsDeleted, col.EmployeeID, e.FullName, col.IsDeleted, e.IsArchived
                    FROM public.SkillMatrixLiveCells c
                    JOIN public.SkillMatrixLiveAreas a     ON a.AreaID = c.AreaID
                    JOIN public.SkillMatrixLiveSkills s    ON s.SkillID = c.SkillID
                    JOIN public.SkillMatrixLiveColumns col ON col.ColumnID = c.ColumnID
                    JOIN public.Employees e                ON e.EmployeeID = col.EmployeeID
                    WHERE (c.SkillID, c.ColumnID) IN (SELECT * FROM unnest(@skills, @cols))
                    FOR UPDATE OF c", conn, tx))
                {
                    cmd.Parameters.AddWithValue("skills", changes.Select(c => c.SkillId).ToArray());
                    cmd.Parameters.AddWithValue("cols", changes.Select(c => c.ColumnId).ToArray());
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                        current[(r.GetInt64(0), r.GetInt64(1))] = (r.GetInt64(2), r.GetString(3), r.GetString(4),
                            r.IsDBNull(5) ? null : r.GetInt16(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt32(7),
                            r.IsDBNull(8) ? null : r.GetDateTime(8), r.IsDBNull(9) ? null : r.GetString(9),
                            r.GetString(10), r.GetBoolean(11), Convert.ToInt64(r.GetValue(12)), r.GetString(13), r.GetBoolean(14), r.GetBoolean(15));
                }

                var conflicts = new List<CellConflict>();
                foreach (var c in changes)
                {
                    if (!current.TryGetValue((c.SkillId, c.ColumnId), out var now) || now.AreaId != liveAreaId)
                        throw new ArgumentException("A cell being saved is not on this sheet (the matrix may have been re-imported). Refresh the tab. Nothing was saved.");
                    if (now.SkillDeleted || now.ColumnDeleted)
                        throw new ArgumentException($"'{now.SkillName}' / {now.EmployeeName} has been removed from the sheet. Refresh the tab. Nothing was saved.");
                    if (now.EmployeeArchived)
                        throw new ArgumentException($"{now.EmployeeName} is archived (Employees tab), so their levels are read-only. Nothing was saved.");
                    if (!access.CanEdit(now.AreaId, now.EmployeeId))
                        throw new UnauthorizedAccessException(!access.CanSeeArea(now.AreaId)
                            ? NotYourSheet + " Nothing was saved."
                            : "You cannot change your own levels; ask your manager or an Admin. Nothing was saved.");
                    if (now.Version != c.ExpectedVersion)
                        conflicts.Add(new CellConflict(c.SkillId, c.ColumnId, now.SkillName, now.EmployeeName,
                            DescribeValue(now.Status, now.Level, now.Raw), now.UpdatedBy, now.UpdatedAt));
                }
                if (conflicts.Count > 0)
                {
                    tx.Rollback();
                    return new SaveResult(0, conflicts);
                }

                int saved = 0;
                foreach (var c in changes)
                {
                    var now = current[(c.SkillId, c.ColumnId)];
                    string newStatus = c.NewLevel.HasValue ? "Level" : "Unassessed";
                    if (now.Status == newStatus && now.Level == c.NewLevel) continue;   // no actual change

                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE public.SkillMatrixLiveCells
                        SET Status = @st, Level = @lv, RawValue = NULL, FlagReason = NULL,
                            UpdatedAt = CURRENT_TIMESTAMP, UpdatedByEmployeeID = @by, UpdatedByName = @byName
                        WHERE SkillID = @s AND ColumnID = @c AND Version = @v", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("st", newStatus);
                        cmd.Parameters.Add(new NpgsqlParameter("lv", NpgsqlTypes.NpgsqlDbType.Smallint) { Value = c.NewLevel.HasValue ? (short)c.NewLevel.Value : DBNull.Value });
                        cmd.Parameters.AddWithValue("by", (long)editorEmployeeId);
                        cmd.Parameters.AddWithValue("byName", editorName);
                        cmd.Parameters.AddWithValue("s", c.SkillId);
                        cmd.Parameters.AddWithValue("c", c.ColumnId);
                        cmd.Parameters.AddWithValue("v", c.ExpectedVersion);
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A cell changed while saving. Nothing was saved; refresh and try again.");
                    }

                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO public.SkillMatrixLiveChanges
                            (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, AreaID, AreaName,
                             SkillID, SkillName, ColumnID, EmployeeID, EmployeeName, OldValue, NewValue, Note)
                        VALUES (@by, @byName, @imp, 'SetCell', @a, @aName, @s, @sName, @c, @e, @eName, @old, @new, @note)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("by", (long)editorEmployeeId);
                        cmd.Parameters.AddWithValue("byName", editorName);
                        cmd.Parameters.AddWithValue("imp", basedOnImport);
                        cmd.Parameters.AddWithValue("a", now.AreaId);
                        cmd.Parameters.AddWithValue("aName", now.AreaName);
                        cmd.Parameters.AddWithValue("s", c.SkillId);
                        cmd.Parameters.AddWithValue("sName", now.SkillName);
                        cmd.Parameters.AddWithValue("c", c.ColumnId);
                        cmd.Parameters.AddWithValue("e", now.EmployeeId);
                        cmd.Parameters.AddWithValue("eName", now.EmployeeName);
                        cmd.Parameters.AddWithValue("old", DescribeValue(now.Status, now.Level, now.Raw));
                        cmd.Parameters.AddWithValue("new", DescribeValue(newStatus, c.NewLevel, null));
                        string? note = c.Note ?? (now.Status == "Flagged" ? "Resolved a flagged imported value" : null);
                        cmd.Parameters.Add(new NpgsqlParameter("note", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)note ?? DBNull.Value });
                        cmd.ExecuteNonQuery();
                    }
                    saved++;
                }

                tx.Commit();
                return new SaveResult(saved, Array.Empty<CellConflict>());
            }
            catch
            {
                try { tx.Rollback(); } catch { /* already rolled back / connection broken: the server rolls back */ }
                throw;
            }
        }
    }
}
