using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixDryRun;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixReconciliation;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixWorkbookPreview;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// PREPARES (does not perform) an import: turns a dry run plus the reviewer's decisions into the exact rows
    /// migration 004's tables would receive, checks them against the migration's rules, and lists every
    /// blocking error and every acknowledgement a person must give. No database access here - the separately
    /// gated SkillsMatrixImportWriter is the only code that can write a plan.
    ///
    /// Decisions come from the dry run's reconciliation CSV after review: each row's HumanConfirmation must be
    /// CONFIRMED (with ConfirmedEmployeeID) or EXCLUDED (with ReviewerNotes as the reason). Anything left
    /// UNCONFIRMED blocks the import. Nothing is accepted on the reviewer's behalf.
    /// </summary>
    public static class SkillsMatrixImportPlan
    {
        public const string Confirmed = "CONFIRMED";
        public const string Excluded = "EXCLUDED";

        // ---------------------------------------------------------------------------------------------
        // Reviewed decisions (the reconciliation CSV, filled in by a person)

        public sealed record NameDecision(int Line, string OriginalName, string Occurrences, string Decision, string ConfirmedEmployeeId, string Notes);
        public sealed record DecisionsFile(string Path, string Sha256, IReadOnlyList<NameDecision> Rows);

        /// <summary>Reads the reviewed reconciliation CSV (read-only). Throws if it is not that file's format.</summary>
        public static DecisionsFile LoadDecisionsCsv(string path)
        {
            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var ms = new MemoryStream()) { fs.CopyTo(ms); bytes = ms.ToArray(); }
            var rows = ParseCsv(new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true).ReadToEnd());
            if (rows.Count == 0) throw new InvalidDataException("The decisions file is empty.");

            var header = rows[0].Select(h => h.Trim()).ToList();
            int Col(string name) => header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)) is int i && i >= 0
                ? i : throw new InvalidDataException($"The decisions file has no '{name}' column - choose the reconciliation CSV saved by the dry run.");
            int cName = Col("OriginalName"), cOcc = Col("Occurrences"), cDec = Col("HumanConfirmation"), cId = Col("ConfirmedEmployeeID"), cNotes = Col("ReviewerNotes");

            var list = new List<NameDecision>();
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.All(string.IsNullOrWhiteSpace)) continue;
                string Get(int c) => c < r.Count ? r[c] : "";
                list.Add(new NameDecision(i + 1, Unguard(Get(cName)), Unguard(Get(cOcc).Trim()), Get(cDec).Trim().ToUpperInvariant(), Get(cId).Trim(), Get(cNotes).Trim()));
            }
            return new DecisionsFile(Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)), list);
        }

        // The dry-run CSV prefixes values starting with = + - @ with ' (so spreadsheets show them as text); undo that
        private static string Unguard(string s) => s.Length > 1 && s[0] == '\'' && "=+-@\t\r".Contains(s[1]) ? s[1..] : s;

        // ---------------------------------------------------------------------------------------------
        // The plan: exactly what would be written

        public sealed record ImportDetails(string ApprovedBy, string ImportedBy, string? SourceVersion, string? Notes);

        public sealed record PlanImport(string SourceFileName, string SourceSha256, string? SourceVersion, string ApprovedBy,
            string ImportedBy, string Notes, string ColourMethod, string ColourVerification);
        public sealed record PlanLevel(int KeyNumber, int Level, string Label, string? Description, string SourceRef);
        public sealed record PlanScale(string Name, string LegendStatus, string? LegendNote, string SourceRef, IReadOnlyList<PlanLevel> Levels);
        public sealed record PlanCategory(long Key, string Name, int DisplayOrder, string SourceRef);
        public sealed record PlanSkill(long Key, long CategoryKey, string Name, bool? InabAccredited, string? InabRaw, string? InabSourceRef,
            bool HiddenInSource, int DisplayOrder, int SourceRow, string SourceRef);
        public sealed record PlanColumn(long Key, string SourceColumn, string SourceHeaderText, string HeaderSourceRef, string? SubGroupLabel,
            string? SubGroupSourceRef, string Disposition, long? EmployeeId, string? EmployeeName, string? ExcludeReason, int DisplayOrder,
            bool? EmployeeArchived = null);   // name and archived status as in the reviewed employee list (re-checked by the writer)
        public sealed record PlanCell(long SkillKey, long ColumnKey, string Status, int? Level, string? RawValue, string? FlagReason, string SourceCell);
        public sealed record PlanArea(string Sheet, string Name, int DisplayOrder, string? SkillHeaderRef, string? InabHeaderRef, PlanScale Scale,
            IReadOnlyList<PlanCategory> Categories, IReadOnlyList<PlanSkill> Skills, IReadOnlyList<PlanColumn> Columns,
            IReadOnlyList<PlanCell> Cells, IReadOnlyList<FormatRow> Formats, int CellsNotLoaded);

        /// <summary>Something a person must explicitly accept before the plan can be imported.</summary>
        public sealed record Acknowledgement(string Key, string Text);

        public sealed record ImportPlan(PlanImport Import, IReadOnlyList<PlanArea> Areas, IReadOnlyList<string> Errors,
            IReadOnlyList<string> Warnings, IReadOnlyList<Acknowledgement> Acknowledgements, DateTime PreparedAtLocal)
        {
            public bool IsReady(IReadOnlySet<string> acknowledged) =>
                Errors.Count == 0 && Acknowledgements.All(a => acknowledged.Contains(a.Key));
            public int Members => Areas.Sum(a => a.Columns.Count(c => c.Disposition == "Member"));
            public int ExcludedColumns => Areas.Sum(a => a.Columns.Count(c => c.Disposition == "Excluded"));
            public int Cells => Areas.Sum(a => a.Cells.Count);
            public int Formats => Areas.Sum(a => a.Formats.Count);
        }

        public const string ColourMethodText =
            "SkillsMatrixWorkbookColours (EPPlus 8.2.0): conditional-formatting rules evaluated in priority order " +
            "(containsText, colour scales), then fixed fills; theme colours and tints from the workbook theme; patterned fills as their visual blend.";

        public const string VerifiedColourText =
            "Checked cell by cell against Excel's DisplayFormat for this file (SHA-256 82DFD157...9422) on 2026-10-22: " +
            "5,820 of 5,999 fills exact, 179 within 2/255 on one channel (theme accent 1, lighter 40%); all font colours and bold exact.";

        public static ImportPlan Build(DryRunResult dry, DecisionsFile? decisions, ImportDetails details)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var acks = new List<Acknowledgement>();
            var wb = dry.Workbook;

            if (!dry.WorkbookUnchanged)
                errors.Add("The workbook changed while it was being read. Reopen the preview once the file is no longer being changed.");
            if (!wb.MatchesVerifiedCopy)
                acks.Add(new("unverified-copy", $"This file (SHA-256 {wb.Sha256Before[..12]}...) is NOT the copy verified on 2026-10-21. Import this version anyway."));
            if (string.IsNullOrWhiteSpace(details.ApprovedBy)) errors.Add("'Approved by' is required (the person who approved this import).");
            if (string.IsNullOrWhiteSpace(details.ImportedBy)) errors.Add("'Imported by' is required.");
            if (dry.Roster == null) errors.Add("An employee list is required, so confirmed employee IDs can be checked.");
            if (decisions == null) errors.Add("No decisions file: choose the reconciliation CSV after every name has been marked CONFIRMED or EXCLUDED.");

            // --- Employee decisions ------------------------------------------------------------------
            var byName = new Dictionary<string, (long? EmployeeId, string? Name, string? Reason)>(StringComparer.Ordinal);
            var employees = dry.Roster?.Employees.ToDictionary(e => e.EmployeeId) ?? new Dictionary<long, EmployeeRecord>();
            if (decisions != null)
            {
                foreach (var dup in decisions.Rows.GroupBy(r => r.OriginalName, StringComparer.Ordinal).Where(g => g.Count() > 1))
                    errors.Add($"'{dup.Key}' appears {dup.Count()} times in the decisions file (lines {string.Join(", ", dup.Select(d => d.Line))}).");
                var known = dry.Reconciliation.Select(r => r.OriginalName).ToHashSet(StringComparer.Ordinal);
                foreach (var extra in decisions.Rows.Where(r => !known.Contains(r.OriginalName)))
                    errors.Add($"Line {extra.Line}: '{extra.OriginalName}' is not a name in this workbook - the decisions file belongs to a different workbook or was edited. " +
                               "(If it contains accented letters, save the CSV as 'CSV UTF-8'.)");

                foreach (var rec in dry.Reconciliation)
                {
                    var d = decisions.Rows.FirstOrDefault(r => r.OriginalName == rec.OriginalName);
                    string occ = string.Join("; ", rec.Occurrences.Select(o => o.HeaderCell));
                    if (d == null) { errors.Add($"'{rec.OriginalName}' [{occ}] has no row in the decisions file."); continue; }
                    if (d.Occurrences != occ)
                        errors.Add($"Line {d.Line}: '{rec.OriginalName}' is recorded at '{d.Occurrences}' but the workbook has it at '{occ}' - re-run the dry run and review again.");

                    switch (d.Decision)
                    {
                        case Confirmed:
                            if (!long.TryParse(d.ConfirmedEmployeeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id <= 0)
                            { errors.Add($"Line {d.Line}: '{rec.OriginalName}' is CONFIRMED but ConfirmedEmployeeID '{d.ConfirmedEmployeeId}' is not an employee ID."); break; }
                            if (!employees.TryGetValue(id, out var emp))
                            { errors.Add($"Line {d.Line}: '{rec.OriginalName}' is CONFIRMED as employee {id}, which is not in the employee list."); break; }
                            byName[rec.OriginalName] = (id, emp.FullName, null);
                            if (emp.IsArchived == true)
                                acks.Add(new($"archived:{rec.OriginalName}", $"'{rec.OriginalName}' [{occ}] is confirmed as ARCHIVED employee {id} '{emp.FullName}'. Include them."));
                            if (rec.Status == MatchStatus.StudentOrNonEmployee)
                                acks.Add(new($"student:{rec.OriginalName}", $"'{rec.OriginalName}' [{occ}] is marked as a student in the workbook but is confirmed as employee {id} '{emp.FullName}'. Include them."));
                            if (rec.ProposedEmployeeId is long p && p != id)
                                warnings.Add($"'{rec.OriginalName}' is confirmed as employee {id} '{emp.FullName}', not the proposed {p} '{rec.ProposedEmployeeName}'.");
                            else if (rec.ProposedEmployeeId == null)
                                warnings.Add($"'{rec.OriginalName}' had no proposed match and is confirmed as employee {id} '{emp.FullName}'.");
                            break;
                        case Excluded:
                            if (d.Notes.Length == 0)
                            { errors.Add($"Line {d.Line}: '{rec.OriginalName}' is EXCLUDED but ReviewerNotes is empty - give the reason."); break; }
                            if (d.ConfirmedEmployeeId.Length > 0)
                            { errors.Add($"Line {d.Line}: '{rec.OriginalName}' is EXCLUDED but also has ConfirmedEmployeeID '{d.ConfirmedEmployeeId}' - choose one."); break; }
                            byName[rec.OriginalName] = (null, null, "Excluded by reviewer: " + d.Notes);
                            break;
                        default:
                            errors.Add($"Line {d.Line}: '{rec.OriginalName}' is still {(d.Decision.Length == 0 ? "blank" : d.Decision)} - mark it CONFIRMED (with ConfirmedEmployeeID) or EXCLUDED (with a reason in ReviewerNotes).");
                            break;
                    }
                }
            }

            // --- Rows per sheet ----------------------------------------------------------------------
            var areas = new List<PlanArea>();
            int order = 0;
            foreach (var a in wb.Areas)
            {
                var d = a.Data;
                string sheet = a.Area.SourceSheet;
                order++;

                // Key / legend
                var levels = new List<PlanLevel>();
                foreach (var e in d.Legend.Entries)
                {
                    var cell = a.Colours.Legend.FirstOrDefault(l => l.Level == e.Level && l.Label == e.Label);
                    if (cell == null) errors.Add($"{sheet}: key entry '{e.Label}' has no source cell.");
                    levels.Add(new PlanLevel(e.KeyNumber, e.Level, e.Label, e.Description, cell?.Ref ?? ""));
                }
                var scale = new PlanScale(d.Legend.ScaleName, d.Legend.NeedsConfirmation ? "NeedsConfirmation" : "AsWritten",
                                          d.Legend.LegendNote, d.Legend.SourceRef, levels);
                if (d.Legend.NeedsConfirmation)
                    acks.Add(new($"legend:{sheet}", $"{sheet}: import the key exactly as written, flagged 'needs confirmation' ({d.Legend.LegendNote})"));

                var categories = d.Categories.Select((c, i) => new PlanCategory(c.CategoryId, c.Name, i + 1, c.SourceRef)).ToList();
                var skills = d.Skills.Select((s, i) => new PlanSkill(s.SkillId, s.CategoryId, s.Name, s.InabAccredited, s.InabRaw,
                    a.Colours.InabRefs.TryGetValue(s.SkillId, out var ir) ? ir : null, s.HiddenInSource, i + 1, RowOf(s.SourceRef), s.SourceRef)).ToList();

                var columns = new List<PlanColumn>();
                int colOrder = 0;
                foreach (var m in d.Members)
                {
                    colOrder++;
                    string headerRef = a.Colours.HeaderRefs.TryGetValue(m.MemberId, out var hr) ? hr : "";
                    string? groupRef = a.Colours.GroupRefs.TryGetValue(m.MemberId, out var gr) ? gr : null;
                    var (empId, empName, reason) = byName.TryGetValue(m.SourceHeaderText, out var v) ? v : (null, null, "Unresolved - no decision recorded");
                    columns.Add(new PlanColumn(m.MemberId, m.SourceColumn, m.SourceHeaderText, headerRef, m.SubGroupLabel, groupRef,
                        empId != null ? "Member" : "Excluded", empId, empName, empId != null ? null : reason, colOrder,
                        empId is long eid && employees.TryGetValue(eid, out var er) ? er.IsArchived : null));
                }
                foreach (var dup in columns.Where(c => c.EmployeeId != null).GroupBy(c => c.EmployeeId).Where(g => g.Count() > 1))
                    acks.Add(new($"twice:{sheet}:{dup.Key}", $"{sheet}: employee {dup.Key} '{dup.First().EmployeeName}' would appear in {dup.Count()} columns " +
                                                            $"({string.Join(", ", dup.Select(c => c.HeaderSourceRef))}). Import them that way."));

                // Cells: only for confirmed (Member) columns
                var memberKeys = columns.Where(c => c.Disposition == "Member").Select(c => c.Key).ToHashSet();
                var cells = d.Cells.Where(kv => memberKeys.Contains(kv.Key.MemberId))
                    .Select(kv => new PlanCell(kv.Key.SkillId, kv.Key.MemberId, kv.Value.Status, kv.Value.Level, kv.Value.RawValue, kv.Value.FlagReason, kv.Value.SourceCell))
                    .OrderBy(c => c.SourceCell, StringComparer.Ordinal).ToList();
                int notLoaded = d.Cells.Count - cells.Count;

                int flagged = cells.Count(c => c.Status == "Flagged");
                if (flagged > 0)
                    acks.Add(new($"flagged:{sheet}", $"{sheet}: import {flagged} flagged cell(s) as 'Flagged' with their original value (formulas, text, out-of-range values) - never as levels."));
                var definedLevels = d.Legend.Entries.Select(e => e.Level).ToHashSet();
                var undefined = cells.Where(c => c.Status == "Level" && !definedLevels.Contains(c.Level!.Value)).ToList();
                if (undefined.Count > 0)
                    acks.Add(new($"undefined-level:{sheet}", $"{sheet}: {undefined.Count} cell(s) use level(s) {string.Join(", ", undefined.Select(c => c.Level).Distinct().OrderBy(x => x))} " +
                                                            $"that the sheet's key does not define - import the numbers as written."));

                // Formatting: only for workbook cells that the stored rows refer to
                var refs = new HashSet<string>(StringComparer.Ordinal);
                void R(string? r) { if (!string.IsNullOrEmpty(r)) refs.Add(r); }
                R(a.Colours.SkillHeaderRef); R(a.Colours.InabHeaderRef);
                levels.ForEach(l => R(l.SourceRef)); categories.ForEach(c => R(c.SourceRef));
                skills.ForEach(s => { R(s.SourceRef); R(s.InabSourceRef); });
                columns.ForEach(c => { R(c.HeaderSourceRef); R(c.SubGroupSourceRef); });
                cells.ForEach(c => R(c.SourceCell));
                var formats = dry.Formats.Where(f => f.Sheet == sheet && refs.Contains(f.Cell)).ToList();

                areas.Add(new PlanArea(sheet, a.Area.Name, order, a.Colours.SkillHeaderRef, a.Colours.InabHeaderRef, scale,
                                       categories, skills, columns, cells, formats, notLoaded));
            }

            var notes = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(details.Notes)) notes.AppendLine(details.Notes.Trim());
            notes.AppendLine($"Prepared {DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} from dry run of {dry.RunAtLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.");
            if (decisions != null) notes.AppendLine($"Decisions file: {Path.GetFileName(decisions.Path)} (SHA-256 {decisions.Sha256}).");
            if (dry.Roster != null) notes.Append($"Employee list: {Path.GetFileName(dry.Roster.Source)} (SHA-256 {dry.Roster.Sha256 ?? "n/a"}).");

            var import = new PlanImport(Path.GetFileName(wb.FilePath), wb.Sha256Before, string.IsNullOrWhiteSpace(details.SourceVersion) ? null : details.SourceVersion.Trim(),
                details.ApprovedBy.Trim(), details.ImportedBy.Trim(), notes.ToString().Trim(), ColourMethodText,
                wb.MatchesVerifiedCopy ? VerifiedColourText : "Not verified against Excel for this file version.");

            var plan = new ImportPlan(import, areas, errors, warnings, acks, DateTime.Now);
            errors.AddRange(ValidateAgainstSchema(plan));
            return plan;
        }

        private static int RowOf(string cellRef)
        {
            var m = Regex.Match(cellRef, @"(\d+)$");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        // ---------------------------------------------------------------------------------------------
        // Migration 004's CHECK / UNIQUE / FK rules, applied before anything could be written

        private static readonly Regex Sha = new("^[0-9A-Fa-f]{64}$");
        private static readonly Regex HexRgb = new("^[0-9A-F]{6}$");
        private static readonly Regex ColumnLetters = new("^[A-Z]{1,3}$");
        private static readonly Regex FullCellRef = new(@"^.+![A-Z]{1,3}[1-9][0-9]*$");
        private static readonly HashSet<string> Patterns = new(StringComparer.Ordinal)
        {
            "solid", "darkGray", "mediumGray", "lightGray", "gray125", "gray0625", "darkHorizontal", "darkVertical", "darkDown", "darkUp",
            "darkGrid", "darkTrellis", "lightHorizontal", "lightVertical", "lightDown", "lightUp", "lightGrid", "lightTrellis",
        };

        public static List<string> ValidateAgainstSchema(ImportPlan p)
        {
            var e = new List<string>();
            void Rule(bool ok, string message) { if (!ok) e.Add("Schema rule: " + message); }
            var i = p.Import;
            Rule(!string.IsNullOrWhiteSpace(i.SourceFileName), "source file name required");
            Rule(Sha.IsMatch(i.SourceSha256), "source SHA-256 must be 64 hex characters");

            foreach (var a in p.Areas)
            {
                string s = a.Sheet;
                Rule(a.Scale.LegendStatus is "AsWritten" or "NeedsConfirmation", $"{s}: legend status");
                Rule(a.Scale.LegendStatus == "AsWritten" || !string.IsNullOrWhiteSpace(a.Scale.LegendNote), $"{s}: a key needing confirmation must say why");
                foreach (var l in a.Scale.Levels)
                    Rule(l.Level is >= 1 and <= 5 && l.KeyNumber >= 1 && l.SourceRef.Length > 0, $"{s}: key entry '{l.Label}'");
                Rule(a.Scale.Levels.GroupBy(l => (l.KeyNumber, l.Level)).All(g => g.Count() == 1), $"{s}: one key entry per key number and level");
                var catKeys = a.Categories.Select(c => c.Key).ToHashSet();
                Rule(a.Skills.GroupBy(k => k.SourceRef).All(g => g.Count() == 1), $"{s}: one skill per source cell");
                foreach (var k in a.Skills)
                {
                    Rule(catKeys.Contains(k.CategoryKey), $"{k.SourceRef}: skill's category on the same sheet");
                    Rule(k.SourceRow >= 1, $"{k.SourceRef}: source row");
                    Rule(k.InabAccredited == null || k.InabRaw != null, $"{k.SourceRef}: INAB value needs its source text");
                    Rule(k.InabRaw == null || k.InabSourceRef != null, $"{k.SourceRef}: INAB text needs its cell reference");
                }
                Rule(a.Columns.GroupBy(c => c.SourceColumn).All(g => g.Count() == 1), $"{s}: one column record per source column");
                foreach (var c in a.Columns)
                {
                    Rule(ColumnLetters.IsMatch(c.SourceColumn), $"{s}: column '{c.SourceColumn}'");
                    Rule(c.HeaderSourceRef.Length > 0, $"{s}!{c.SourceColumn}: header cell reference");
                    Rule(c.Disposition == "Member"
                            ? c.EmployeeId != null && c.ExcludeReason == null
                            : c.EmployeeId == null && !string.IsNullOrWhiteSpace(c.ExcludeReason),
                         $"{c.HeaderSourceRef}: Member needs an employee and no reason; Excluded needs a reason and no employee");
                }
                var members = a.Columns.Where(c => c.Disposition == "Member").Select(c => c.Key).ToHashSet();
                var skillKeys = a.Skills.Select(k => k.Key).ToHashSet();
                Rule(a.Cells.GroupBy(c => (c.SkillKey, c.ColumnKey)).All(g => g.Count() == 1), $"{s}: one cell per skill and column");
                foreach (var c in a.Cells)
                {
                    Rule(skillKeys.Contains(c.SkillKey) && members.Contains(c.ColumnKey), $"{c.SourceCell}: cell must belong to a skill and a Member column of this sheet");
                    Rule(c.Status switch
                    {
                        "Level" => c.Level is >= 1 and <= 5 && c.RawValue == null && c.FlagReason == null,
                        "Unassessed" => c.Level == null && c.RawValue == null && c.FlagReason == null,
                        "Flagged" => c.Level == null && c.RawValue != null && !string.IsNullOrWhiteSpace(c.FlagReason),
                        _ => false,
                    }, $"{c.SourceCell}: cell state '{c.Status}'");
                }
                Rule(a.Formats.GroupBy(f => f.Cell).All(g => g.Count() == 1), $"{s}: one format row per cell");
                foreach (var f in a.Formats)
                {
                    bool fill = (f.FillSource == "None" && f.FillRgb == null && f.Pattern == null && f.PatternColorRgb == null && f.PatternBackRgb == null && f.ConditionalRule == null)
                        || (f.FillSource == "ConditionalFormat" && f.FillRgb != null && f.Pattern == "solid" && f.PatternColorRgb == null && f.PatternBackRgb == null && !string.IsNullOrWhiteSpace(f.ConditionalRule))
                        || (f.FillSource == "CellStyle" && f.FillRgb != null && f.Pattern == "solid" && f.PatternColorRgb == null && f.PatternBackRgb == null && f.ConditionalRule == null)
                        || (f.FillSource == "CellStyle" && f.FillRgb != null && f.Pattern != null && f.Pattern != "solid" && f.PatternColorRgb != null && f.PatternBackRgb != null && f.ConditionalRule == null);
                    Rule(fill && FullCellRef.IsMatch(f.Cell) && f.Cell.StartsWith(s + "!", StringComparison.Ordinal)
                         && (f.Pattern == null || Patterns.Contains(f.Pattern)) && HexRgb.IsMatch(f.FontRgb)
                         && new[] { f.FillRgb, f.PatternColorRgb, f.PatternBackRgb }.All(x => x == null || HexRgb.IsMatch(x))
                         && (f.FontSource == "ConditionalFormat") == !string.IsNullOrWhiteSpace(f.FontRule),
                         $"{f.Cell}: formatting row");
                }
            }
            return e;
        }

        // ---------------------------------------------------------------------------------------------

        public static string RenderSummary(ImportPlan p, IReadOnlySet<string> acknowledged)
        {
            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);
            var i = p.Import;
            L("SKILLS MATRIX - IMPORT PLAN (NOT WRITTEN)");
            L(new string('=', 78));
            L("This is what an approved import WOULD write. Nothing has been written to any database.");
            L($"Status: {(p.IsReady(acknowledged) ? "READY - every decision recorded and acknowledged" : "NOT READY")}" +
              $"   ({p.Errors.Count} blocking error(s), {p.Acknowledgements.Count(a => !acknowledged.Contains(a.Key))} acknowledgement(s) outstanding)");
            L();
            L($"Source file:   {i.SourceFileName}   SHA-256 {i.SourceSha256}");
            L($"Version label: {i.SourceVersion ?? "(none)"}");
            L($"Approved by:   {(i.ApprovedBy.Length > 0 ? i.ApprovedBy : "(missing)")}      Imported by: {i.ImportedBy}");
            L($"Colours:       {i.ColourVerification}");
            L();
            L("WOULD WRITE (one transaction; the new snapshot becomes current, the previous one is kept unchanged)");
            L($"  SkillMatrixImports      1");
            L($"  SkillMatrixScales       {p.Areas.Count}     SkillMatrixScaleLevels {p.Areas.Sum(a => a.Scale.Levels.Count)}");
            L($"  SkillMatrixAreas        {p.Areas.Count}     SkillMatrixCategories  {p.Areas.Sum(a => a.Categories.Count)}");
            L($"  SkillMatrixSkills       {p.Areas.Sum(a => a.Skills.Count)}");
            L($"  SkillMatrixColumns      {p.Members + p.ExcludedColumns}   ({p.Members} Member, {p.ExcludedColumns} Excluded with reason)");
            L($"  SkillMatrixCells        {p.Cells}   ({p.Areas.Sum(a => a.CellsNotLoaded)} cells of Excluded columns are not loaded)");
            L($"  SkillMatrixCellFormats  {p.Formats}");
            L();
            L(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1,8}{2,9}{3,7}{4,9}{5,9}{6,9}  {7}", "Sheet", "Members", "Excluded", "Cells", "Assessed", "Unassess", "Flagged", "Key"));
            foreach (var a in p.Areas)
                L(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1,8}{2,9}{3,7}{4,9}{5,9}{6,9}  {7}", a.Sheet.Length > 27 ? a.Sheet[..26] + "…" : a.Sheet,
                    a.Columns.Count(c => c.Disposition == "Member"), a.Columns.Count(c => c.Disposition == "Excluded"), a.Cells.Count,
                    a.Cells.Count(c => c.Status == "Level"), a.Cells.Count(c => c.Status == "Unassessed"), a.Cells.Count(c => c.Status == "Flagged"),
                    a.Scale.LegendStatus == "AsWritten" ? "as written" : "NEEDS CONFIRMATION"));
            L();
            if (p.Errors.Count > 0)
            {
                L($"BLOCKING ERRORS ({p.Errors.Count}) - the import cannot proceed until these are fixed:");
                foreach (var e in p.Errors) L("  x " + e);
                L();
            }
            if (p.Acknowledgements.Count > 0)
            {
                L($"ACKNOWLEDGEMENTS REQUIRED ({p.Acknowledgements.Count}):");
                foreach (var a in p.Acknowledgements) L($"  [{(acknowledged.Contains(a.Key) ? "x" : " ")}] {a.Text}");
                L();
            }
            if (p.Warnings.Count > 0)
            {
                L($"FOR INFORMATION ({p.Warnings.Count}):");
                foreach (var w in p.Warnings) L("  - " + w);
                L();
            }
            L("MEMBER COLUMNS (confirmed employees):");
            foreach (var a in p.Areas)
                foreach (var c in a.Columns.Where(c => c.Disposition == "Member"))
                    L($"  {c.HeaderSourceRef,-24} '{c.SourceHeaderText}' -> employee {c.EmployeeId} '{c.EmployeeName}'");
            L("EXCLUDED COLUMNS (stored with their reason; their cells are not loaded):");
            foreach (var a in p.Areas)
                foreach (var c in a.Columns.Where(c => c.Disposition == "Excluded"))
                    L($"  {c.HeaderSourceRef,-24} '{c.SourceHeaderText}' - {c.ExcludeReason}");
            L();
            L("Import notes to be stored:");
            foreach (var line in i.Notes.Split('\n')) L("  " + line.TrimEnd('\r'));
            return sb.ToString();
        }
    }
}
