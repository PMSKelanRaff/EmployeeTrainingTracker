using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixReconciliation;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixWorkbookPreview;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// DRY RUN ONLY - works out what an import of the workbook copy WOULD contain, and writes a report for
    /// review. It never imports anything: there is no database access in this class (no reads, no writes,
    /// no connection), and it works only from a workbook preview that has already been read into memory
    /// (SkillsMatrixWorkbookPreview) plus an optional local employee list.
    ///
    /// "Proposed records" are counts of what migration 004's tables would receive if this data were later
    /// approved and imported by a separate, approved process. Nothing in a dry run is confirmed: every
    /// person column stays unconfirmed, so no cell would be loaded until its column is confirmed.
    /// </summary>
    public static class SkillsMatrixDryRun
    {
        public const string Banner = "DRY RUN ONLY - nothing was imported and nothing was written to any database. This is not an approved or completed import.";

        public sealed record SheetSummary(
            string Sheet, SheetScope Scope, int Categories, int Skills, int HiddenSkills, int People,
            int LevelCells, int UnassessedCells, int FlaggedCells, int FormulaCells, int OutOfRangeValueCells,
            int LegendEntries, bool LegendNeedsConfirmation, string? LegendNote, string LegendSourceRef,
            int FormatRows, int ReviewItems);

        public sealed record DryRunReviewItem(string Sheet, string Kind, string? Cell, string Message);

        /// <summary>One workbook cell's displayed formatting, in the shape of SkillMatrixCellFormats.</summary>
        public sealed record FormatRow(string Sheet, string Cell, string FillSource, string? FillRgb, string? Pattern,
            string? PatternColorRgb, string? PatternBackRgb, string? ConditionalRule, string FontSource, string FontRgb,
            string? FontRule, bool Bold);

        /// <summary>What migration 004's tables would receive if this were approved (nothing is written).</summary>
        public sealed record ProposedCounts(int Imports, int Scales, int ScaleLevels, int Areas, int Categories, int Skills,
            int Columns, int MemberColumns, int ExcludedColumns, int Cells, int CellsHeldBack, int CellFormats);

        public sealed record DryRunResult(
            DateTime RunAtLocal, string RunBy, PreviewResult Workbook, EmployeeRoster? Roster,
            IReadOnlyList<SheetSummary> Sheets, IReadOnlyList<ReconciliationRow> Reconciliation,
            IReadOnlyList<DryRunReviewItem> ReviewItems, IReadOnlyList<FormatRow> Formats, ProposedCounts Proposed)
        {
            public bool WorkbookUnchanged => string.Equals(Workbook.Sha256Before, Workbook.Sha256After, StringComparison.OrdinalIgnoreCase);
            public int Count(MatchStatus s) => Reconciliation.Count(r => r.Status == s);
        }

        // ---------------------------------------------------------------------------------------------

        public static DryRunResult Run(PreviewResult workbook, EmployeeRoster? roster, DateTime? runAt = null, string? runBy = null)
        {
            var sheets = new List<SheetSummary>();
            var items = new List<DryRunReviewItem>();
            var formats = new List<FormatRow>();
            var names = new List<(string, Occurrence)>();

            foreach (var a in workbook.Areas)
            {
                var d = a.Data;
                var cells = d.Cells.Values.ToList();
                int formula = cells.Count(c => c.Status == "Flagged" && c.RawValue?.StartsWith("=") == true);
                int outOfRange = a.ReviewItems.Count(i => i.Kind == ReviewKind.OutOfRange);
                var sheetFormats = a.Colours.Display.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                    .Select(kv => ToFormatRow(a.Area.SourceSheet, kv.Key, kv.Value)).ToList();
                formats.AddRange(sheetFormats);
                items.AddRange(a.ReviewItems.Select(i => new DryRunReviewItem(a.Area.SourceSheet, i.Kind, i.Cell, i.Message)));
                // A level the sheet's own key does not define (e.g. 3 on a key listing only 1, 4, 5) is ambiguous
                var defined = d.Legend.Entries.Select(e => e.Level).ToHashSet();
                foreach (var c in cells.Where(c => c.Status == "Level" && !defined.Contains(c.Level!.Value)).OrderBy(c => c.SourceCell, StringComparer.Ordinal))
                    items.Add(new DryRunReviewItem(a.Area.SourceSheet, ReviewKind.LevelNotInKey, c.SourceCell,
                        $"{c.SourceCell}: level {c.Level} is not defined by this sheet's key ({d.Legend.SourceRef}) - its meaning needs confirming."));

                foreach (var m in d.Members)
                {
                    string headerCell = a.Colours.HeaderRefs.TryGetValue(m.MemberId, out var hr) ? hr : $"{a.Area.SourceSheet}!{m.SourceColumn}";
                    names.Add((m.SourceHeaderText, new Occurrence(a.Area.SourceSheet, m.SourceColumn, headerCell)));
                }

                sheets.Add(new SheetSummary(a.Area.SourceSheet, a.Scope, d.Categories.Count, d.Skills.Count,
                    d.Skills.Count(s => s.HiddenInSource), d.Members.Count,
                    cells.Count(c => c.Status == "Level"), cells.Count(c => c.Status == "Unassessed"),
                    cells.Count(c => c.Status == "Flagged"), formula, outOfRange,
                    d.Legend.Entries.Count, d.Legend.NeedsConfirmation, d.Legend.LegendNote, d.Legend.SourceRef,
                    sheetFormats.Count, items.Count(i => i.Sheet == a.Area.SourceSheet)));
            }

            var reconciliation = Reconcile(names, roster);
            int people = sheets.Sum(s => s.People);
            var proposed = new ProposedCounts(
                Imports: 1,
                Scales: workbook.Areas.Count,
                ScaleLevels: sheets.Sum(s => s.LegendEntries),
                Areas: workbook.Areas.Count,
                Categories: sheets.Sum(s => s.Categories),
                Skills: sheets.Sum(s => s.Skills),
                Columns: people,
                MemberColumns: 0,                 // nothing is confirmed in a dry run
                ExcludedColumns: people,
                Cells: 0,                         // cells are only loaded for confirmed (Member) columns
                CellsHeldBack: sheets.Sum(s => s.LevelCells + s.UnassessedCells + s.FlaggedCells),
                CellFormats: formats.Count);

            return new DryRunResult(runAt ?? DateTime.Now, runBy ?? Environment.UserName, workbook, roster,
                                    sheets, reconciliation, items, formats, proposed);
        }

        private static FormatRow ToFormatRow(string sheet, string cell, SkillsMatrixWorkbookColours.CellDisplay d)
        {
            bool patterned = d.FillSource == "CellStyle" && d.Pattern != null && d.Pattern != "solid";
            return new FormatRow(sheet, cell, d.FillSource,
                d.FillSource == "None" ? null : Hex(d.Fill),
                d.FillSource == "None" ? null : (d.Pattern ?? "solid"),
                patterned ? Hex(d.PatternColor) : null,
                patterned ? Hex(d.PatternBack) : null,
                d.FillSource == "ConditionalFormat" ? d.ConditionalRule : null,
                d.FontSource, Hex(d.Font)!, d.FontSource == "ConditionalFormat" ? d.FontRule : null, d.Bold);
        }

        private static string? Hex(Color? c) => c is Color v ? $"{v.R:X2}{v.G:X2}{v.B:X2}" : null;

        // ---------------------------------------------------------------------------------------------
        // Report (plain text)

        public static string RenderReport(DryRunResult r)
        {
            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);
            var w = r.Workbook;

            L("SKILLS MATRIX - DRY RUN REPORT");
            L(new string('=', 78));
            L(Banner);
            L("Every employee match below is UNCONFIRMED. Nothing here may be imported without separate approval.");
            L();
            L($"Generated:      {r.RunAtLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} (local time) by {r.RunBy}");
            L($"Workbook copy:  {w.FilePath}");
            L($"  SHA-256 before reading: {w.Sha256Before}");
            L($"  SHA-256 after reading:  {w.Sha256After}  -> {(r.WorkbookUnchanged ? "unchanged during reading" : "FILE CHANGED WHILE BEING READ - do not rely on this report")}");
            L($"  Verified copy (2026-10-21): {VerifiedCopySha256}  -> {(w.MatchesVerifiedCopy ? "matches" : "DOES NOT MATCH (a different version of the workbook)")}");
            L($"  The workbook was opened read-only and was not modified.");
            L(r.Roster == null
                ? "Employee list:  NONE SUPPLIED - names were not looked up; every name is reported as unmatched (or student)."
                : $"Employee list:  {r.Roster.Source} ({r.Roster.Employees.Count} records, SHA-256 {r.Roster.Sha256 ?? "n/a"})");
            L("                (The dry run never reads employee records from the database.)");
            L();

            L("1. SHEETS AND SOURCE RANGES PROCESSED");
            L(new string('-', 78));
            foreach (var s in r.Sheets)
            {
                L($"{s.Sheet}");
                L($"   Key (legend):   {s.Scope.LegendRange}");
                foreach (var h in s.Scope.HeaderRows) L($"   Header:         {h}");
                L($"   In-range cells: {(s.Scope.ValidationRanges.Count == 0 ? "(no data-validation range on this sheet)" : string.Join(", ", s.Scope.ValidationRanges))}");
                L($"   Rows read to:   {s.Scope.LastRow}");
            }
            L();

            L("2. WHAT WAS FOUND");
            L(new string('-', 78));
            L(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1,7}{2,7}{3,7}{4,9}{5,9}{6,9}{7,9}{8,8}",
                "Sheet", "Categ.", "Skills", "People", "Assessed", "Unassess", "Flagged", "Formula", "Review"));
            foreach (var s in r.Sheets)
                L(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1,7}{2,7}{3,7}{4,9}{5,9}{6,9}{7,9}{8,8}",
                    Trunc(s.Sheet, 27), s.Categories, s.Skills, s.People, s.LevelCells, s.UnassessedCells, s.FlaggedCells, s.FormulaCells, s.ReviewItems));
            L(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1,7}{2,7}{3,7}{4,9}{5,9}{6,9}{7,9}{8,8}",
                "TOTAL", r.Sheets.Sum(s => s.Categories), r.Sheets.Sum(s => s.Skills), r.Sheets.Sum(s => s.People),
                r.Sheets.Sum(s => s.LevelCells), r.Sheets.Sum(s => s.UnassessedCells), r.Sheets.Sum(s => s.FlaggedCells),
                r.Sheets.Sum(s => s.FormulaCells), r.ReviewItems.Count));
            L("Assessed = a whole level 1-5 inside the sheet's range.  Unassessed = blank in the workbook (never treated as 1 or 0).");
            L("Flagged = formula, text, a number that is not a whole level 1-5, or a value outside the sheet's range -");
            L("          kept as written, never converted to a level. 'People' counts columns (one person can appear on several sheets).");
            L($"Hidden skill rows: {r.Sheets.Sum(s => s.HiddenSkills)} (kept, marked hidden).  Values outside a sheet's range: {r.Sheets.Sum(s => s.OutOfRangeValueCells)}.");
            L();

            L("3. LEVEL DEFINITIONS (KEYS) - AS WRITTEN");
            L(new string('-', 78));
            foreach (var s in r.Sheets)
                L($"{Trunc(s.Sheet, 28),-28} {s.LegendEntries} entries at {s.LegendSourceRef}  " +
                  (s.LegendNeedsConfirmation ? $"NEEDS CONFIRMATION: {s.LegendNote}" : "as written"));
            L();

            var p = r.Proposed;
            L("4. PROPOSED RECORDS (what migration 004 would receive if approved - NOTHING WAS WRITTEN)");
            L(new string('-', 78));
            L($"Imports {p.Imports} | Scales {p.Scales} | Scale levels {p.ScaleLevels} | Areas {p.Areas} | Categories {p.Categories} | Skills {p.Skills}");
            L($"Person columns {p.Columns}: {p.MemberColumns} confirmed (Member), {p.ExcludedColumns} held back as unconfirmed (Excluded)");
            L($"Cells {p.Cells} now - {p.CellsHeldBack} cells are held back until their person column is confirmed");
            L($"Cell formatting rows {p.CellFormats} (displayed colours and where they come from - see the formatting CSV)");
            L();

            L("5. EMPLOYEE RECONCILIATION (ALL UNCONFIRMED)");
            L(new string('-', 78));
            L($"Distinct names as written: {r.Reconciliation.Count}");
            foreach (MatchStatus st in Enum.GetValues<MatchStatus>())
                L($"   {StatusText(st),-42}{r.Count(st),5}");
            foreach (MatchStatus st in new[] { MatchStatus.SuggestedMatch, MatchStatus.Unmatched, MatchStatus.StudentOrNonEmployee, MatchStatus.ArchivedEmployee })
            {
                var rows = r.Reconciliation.Where(x => x.Status == st).ToList();
                if (rows.Count == 0) continue;
                L();
                L($"{StatusText(st)}:");
                foreach (var x in rows)
                    L($"   '{x.OriginalName}' [{string.Join(", ", x.Occurrences.Select(o => o.HeaderCell))}]" +
                      (x.ProposedEmployeeId is long id ? $" -> ID {id} '{x.ProposedEmployeeName}'" : "") + $" - {x.Reason}");
            }
            L();

            L("6. EXCLUDED - WITH REASONS");
            L(new string('-', 78));
            L("Sheets not processed:");
            foreach (var s in w.ExcludedSheets) L($"   {s}");
            L("Columns not processed:");
            var skipped = r.Sheets.SelectMany(s => s.Scope.SkippedColumns).ToList();
            if (skipped.Count == 0) L("   (none)");
            foreach (var s in skipped) L($"   {s}");
            L("Rows and cells without a record:");
            foreach (var s in r.Sheets)
                L($"   {Trunc(s.Sheet, 28),-28} {s.Scope.CategoryRows} category heading rows (kept as categories), " +
                  $"{s.Scope.BlankRowsSkipped} blank rows, {s.Scope.OutOfRangeBlankCells} blank cells outside the sheet's range");
            L();

            L("7. ITEMS FOR REVIEW");
            L(new string('-', 78));
            foreach (var g in r.ReviewItems.GroupBy(i => i.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
                L($"   {g.Key,-28}{g.Count(),5}");
            if (r.ReviewItems.Count == 0) L("   (none)");
            L("   Every item, with its cell reference, is listed in the review-items CSV.");
            L();
            L(Banner);
            return sb.ToString();
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

        // ---------------------------------------------------------------------------------------------
        // CSV files

        public static string ReconciliationCsv(DryRunResult r)
        {
            var sb = new StringBuilder();
            Csv(sb, "DryRun", "OriginalName", "Occurrences", "Status", "MatchType", "Confidence",
                "ProposedEmployeeID", "ProposedEmployeeName", "ProposedEmployeeArchived", "Reason",
                "HumanConfirmation", "ConfirmedEmployeeID", "ReviewerNotes");
            foreach (var x in r.Reconciliation)
                Csv(sb, "DRY RUN", x.OriginalName, string.Join("; ", x.Occurrences.Select(o => o.HeaderCell)), StatusText(x.Status),
                    x.MatchType, x.Confidence, x.ProposedEmployeeId?.ToString(CultureInfo.InvariantCulture), x.ProposedEmployeeName,
                    x.ProposedEmployeeArchived switch { true => "Yes", false => "No", null => x.ProposedEmployeeId == null ? "" : "Unknown" },
                    x.Reason, x.Confirmation, "", "");
            return sb.ToString();
        }

        public static string ReviewItemsCsv(DryRunResult r)
        {
            var sb = new StringBuilder();
            Csv(sb, "DryRun", "Sheet", "Cell", "Kind", "Detail");
            foreach (var i in r.ReviewItems) Csv(sb, "DRY RUN", i.Sheet, i.Cell, i.Kind, i.Message);
            return sb.ToString();
        }

        public static string FormattingCsv(DryRunResult r)
        {
            var sb = new StringBuilder();
            Csv(sb, "DryRun", "Sheet", "Cell", "FillSource", "FillRgb", "Pattern", "PatternColorRgb", "PatternBackRgb",
                "ConditionalRule", "FontSource", "FontRgb", "FontRule", "Bold");
            foreach (var f in r.Formats)
                Csv(sb, "DRY RUN", f.Sheet, f.Cell, f.FillSource, f.FillRgb, f.Pattern, f.PatternColorRgb, f.PatternBackRgb,
                    f.ConditionalRule, f.FontSource, f.FontRgb, f.FontRule, f.Bold ? "Yes" : "No");
            return sb.ToString();
        }

        /// <summary>
        /// One CSV line. Values starting with = + - @ (or a tab / carriage return) get a leading apostrophe so a
        /// spreadsheet program shows them as text instead of running them as formulas; the report says so.
        /// </summary>
        private static void Csv(StringBuilder sb, params string?[] values) => sb.AppendLine(CsvLine(values));

        public static string CsvLine(params string?[] values) =>
            string.Join(",", values.Select(v =>
            {
                string s = v ?? "";
                if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
                return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || s != s.Trim() ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            }));

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes the report and three CSV files as NEW files in the chosen folder (never overwrites an
        /// existing file; refuses the workbook itself). Returns the paths written.
        /// </summary>
        public static IReadOnlyList<string> WriteFiles(DryRunResult r, string folder)
        {
            string stamp = r.RunAtLocal.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var files = new (string Name, string Content)[]
            {
                ($"SkillsMatrix_DRYRUN_{stamp}_Report.txt", RenderReport(r) + Footer()),
                ($"SkillsMatrix_DRYRUN_{stamp}_Reconciliation.csv", ReconciliationCsv(r)),
                ($"SkillsMatrix_DRYRUN_{stamp}_ReviewItems.csv", ReviewItemsCsv(r)),
                ($"SkillsMatrix_DRYRUN_{stamp}_Formatting.csv", FormattingCsv(r)),
            };

            string workbook = Path.GetFullPath(r.Workbook.FilePath);
            var paths = files.Select(f => Path.GetFullPath(Path.Combine(folder, f.Name))).ToList();
            if (paths.Any(p => string.Equals(p, workbook, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Refusing to write over the workbook.");
            var existing = paths.Where(File.Exists).ToList();
            if (existing.Count > 0)
                throw new IOException("A dry-run file with this name already exists (nothing was written): " + string.Join(", ", existing));

            var written = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                using var fs = new FileStream(paths[i], FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                sw.Write(files[i].Content);
                written.Add(paths[i]);
            }
            return written;

            static string Footer() =>
                Environment.NewLine + "Files: the reconciliation, review-items and formatting CSV files with the same timestamp." +
                Environment.NewLine + "In the CSV files, values that begin with = + - or @ are prefixed with ' so spreadsheet programs show them as text." +
                Environment.NewLine + "The reports contain employee names - store and share them accordingly." + Environment.NewLine;
        }
    }
}
