using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixService;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// PREVIEW ONLY - temporary, local, read-only view of a verified COPY of the audited Skills Matrix
    /// workbook, so the Admin "Skills Matrix" tab can be seen before any approved database import.
    ///
    /// Guarantees:
    ///   * The file is opened read-only (shared), read into memory once, and parsed from memory.
    ///     It is never saved, written, moved or renamed. Its SHA-256 is computed from the bytes read and
    ///     re-checked against the file on disk afterwards.
    ///   * No database access of any kind (no reads, no writes).
    ///   * Nothing is reconciled: names are shown exactly as written and are NOT matched to employees.
    ///   * Blank in-range cells are 'Unassessed'. Formulas, text and other unusual values are 'Flagged'
    ///     with their raw value - never converted to a level. Colours are not interpreted.
    ///   * Only the 9 visible matrix sheets are previewed; hidden/legacy sheets and the
    ///     'Skill Matrix required' template are excluded and listed.
    ///
    /// Produces the same read-only model types as SkillsMatrixService so the existing view renders it.
    /// </summary>
    public static class SkillsMatrixWorkbookPreview
    {
        /// <summary>SHA-256 of the local copy inspected and verified on 2026-10-21 (docs/SkillsMatrix.md).</summary>
        public const string VerifiedCopySha256 = "82DFD157278C526832461CC4B8DD0BEA8AB2A91FDD7A15BA1767F27B23EF9422";

        /// <summary>A key/legend cell and where it is, so its displayed colours can be shown with its wording.</summary>
        public sealed record LegendCell(int Level, string Label, string? Description, string Ref);

        /// <summary>
        /// The colours Excel displays for the workbook cells this preview shows (the workbook's colour
        /// standard), keyed by full reference ("Sheet!A1").
        /// </summary>
        public sealed record PreviewColours(
            IReadOnlyDictionary<string, SkillsMatrixWorkbookColours.CellDisplay> Display,
            IReadOnlyDictionary<long, string> InabRefs,     // skill id  -> its INAB cell
            IReadOnlyDictionary<long, string> HeaderRefs,   // member id -> its name cell
            IReadOnlyDictionary<long, string> GroupRefs,    // member id -> its team/group heading cell
            string? SkillHeaderRef,
            string? InabHeaderRef,
            IReadOnlyList<LegendCell> Legend);

        /// <summary>
        /// Something on a sheet that needs human review. Kind is one of the ReviewKind constants; Cell is the
        /// full reference ("Sheet!A1") or null when the item concerns the whole sheet.
        /// </summary>
        public sealed record ReviewItem(string Kind, string? Cell, string Message);

        public static class ReviewKind
        {
            public const string Formula = "Formula";
            public const string UnexpectedValue = "UnexpectedValue";
            public const string OutOfRange = "OutOfRangeValue";
            public const string HiddenRow = "HiddenRow";
            public const string DuplicateSkill = "DuplicateSkillName";
            public const string AmbiguousLegend = "AmbiguousLevelDefinition";
            public const string UnnamedColumn = "UnnamedColumn";
            public const string MissingSkillName = "MissingSkillName";
            public const string NoCategoryHeading = "NoCategoryHeading";
            public const string UnexpectedInab = "UnexpectedInabValue";
            public const string CategoryRowValue = "ValueInCategoryRow";
            public const string ColourInconsistency = "ColourInconsistency";
            public const string NoValidationRange = "NoValidationRange";
            public const string LevelNotInKey = "LevelNotInKey";
        }

        /// <summary>What was read on a sheet, and what was left out (with reasons).</summary>
        public sealed record SheetScope(
            string LegendRange,
            IReadOnlyList<string> HeaderRows,          // e.g. "row 3, columns C:K"
            IReadOnlyList<string> ValidationRanges,    // the sheet's data-validation ranges = the in-range cells
            int LastRow,
            IReadOnlyList<string> SkippedColumns,      // "Laboratory!J3 - no name in the header row and no values"
            int BlankRowsSkipped,
            int CategoryRows,
            int OutOfRangeBlankCells);                 // blank cells outside the validation range (no record)

        public sealed record PreviewArea(SkillAreaInfo Area, SkillMatrixData Data, IReadOnlyList<ReviewItem> ReviewItems,
                                         PreviewColours Colours, SheetScope Scope)
        {
            /// <summary>The review items as plain text (the preview's "Review notes").</summary>
            public IReadOnlyList<string> Warnings => ReviewItems.Select(i => i.Message).ToList();
        }

        public sealed record PreviewResult(
            string FilePath,
            string Sha256Before,
            string Sha256After,
            bool MatchesVerifiedCopy,
            IReadOnlyList<PreviewArea> Areas,
            IReadOnlyList<string> ExcludedSheets);

        // ---------------------------------------------------------------------------------------------
        // Per-sheet layout (from the read-only inspection of the verified copy). Column numbers are 1-based.

        private sealed record Block(int HeaderRow, int[] SubgroupRows, int SkillCol, int? InabCol, int? CategoryCol,
                                    int FirstPersonCol, int LastPersonCol, int FirstDataRow);

        private sealed record SheetLayout(string Sheet, string LegendRange, Block[] Blocks);

        private static readonly SheetLayout[] Layouts =
        {
            new("Laboratory",                  "A1:K1",  new[] { new Block(3, new[] { 2 }, 1, 2, null, 3, 11, 4) }),
            new("Data Collection",             "A1:P1",  new[] { new Block(3, new[] { 2 }, 1, 2, null, 3, 16, 4) }),
            new("Data Analysis",               "B2:Y2",  new[] { new Block(5, new[] { 3 }, 2, 3, null, 4, 25, 6) }),
            new("GIS",                         "B2:M2",  new[] { new Block(5, new[] { 3 }, 2, 3, null, 4, 13, 6) }),
            new("Software and Data Analytics", "J4:K13", new[] { new Block(3, new[] { 1 }, 3, 4, 2, 5, 8, 4) }),
            new("QMS",                         "A2:L2",  new[] { new Block(4, Array.Empty<int>(), 1, null, null, 2, 12, 5) }),
            new("Innovation",                  "A1:L1",  new[] { new Block(4, new[] { 2 }, 1, 2, null, 3, 5, 5),
                                                               new Block(4, new[] { 2 }, 7, 8, null, 9, 12, 5) }),
            new("H&S Environmental",           "A3:AW3", new[] { new Block(4, Array.Empty<int>(), 1, null, null, 2, 49, 5) }),
            new("Admin",                       "A3:H3",  new[] { new Block(4, Array.Empty<int>(), 1, null, null, 2, 8, 5) }),
        };

        private const string TemplateSheet = "Skill Matrix required";
        private static readonly Regex LegendLabel = new(@"^\s*([1-5])\s*:\s*(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex CellRef = new(@"^([A-Z]{1,3})(\d+)(?::([A-Z]{1,3})(\d+))?$", RegexOptions.Compiled);

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Refuses paths that may be the live audited original (cloud / sync locations) rather than a copy.
        /// Returns null when the path is acceptable, otherwise the reason it was refused.
        /// </summary>
        public static string? RejectReason(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "No file selected.";
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return "Web/SharePoint locations are not allowed. Download a copy and choose the local copy.";

            string full = Path.GetFullPath(path);
            foreach (var root in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" }
                         .Select(Environment.GetEnvironmentVariable)
                         .Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                if (full.StartsWith(Path.GetFullPath(root!), StringComparison.OrdinalIgnoreCase))
                    return "Files inside a OneDrive/SharePoint-synced folder are not allowed (it could be the live original). " +
                           "Copy the workbook to a local folder outside OneDrive and choose that copy.";
            }
            if (full.Contains("SharePoint", StringComparison.OrdinalIgnoreCase))
                return "Files inside a SharePoint-synced folder are not allowed. Choose a local copy.";
            if (!full.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return "Only .xlsx workbook copies can be previewed.";
            return null;
        }

        public static PreviewResult Load(string path)
        {
            string? reject = RejectReason(path);
            if (reject != null)
                throw new InvalidOperationException(reject);

            // Read the file ONCE, read-only and shared (works while Excel has it open), then work from memory.
            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                bytes = ms.ToArray();
            }
            string shaBefore = Convert.ToHexString(SHA256.HashData(bytes));

            var areas = new List<PreviewArea>();
            var excluded = new List<string>();

            ExcelPackage.License.SetNonCommercialOrganization("Student"); // same setting as LegacyExcelService
            using (var package = new ExcelPackage(new MemoryStream(bytes, writable: false)))
            {
                long nextId = 1;
                foreach (var ws in package.Workbook.Worksheets)
                {
                    var layout = Layouts.FirstOrDefault(l => l.Sheet == ws.Name);
                    if (ws.Hidden != eWorkSheetHidden.Visible)
                        excluded.Add($"{ws.Name} - hidden sheet (legacy/auxiliary)");
                    else if (ws.Name == TemplateSheet)
                        excluded.Add($"{ws.Name} - template with placeholder staff, not live data");
                    else if (layout == null)
                        excluded.Add($"{ws.Name} - visible but not a recognised matrix sheet (not previewed)");
                    else
                        areas.Add(ReadSheet(ws, layout, SkillsMatrixWorkbookColours.For(ws, package, bytes), ref nextId));
                }
            } // package disposed without saving - nothing is ever written

            // Re-hash the file on disk to show it was not changed
            string shaAfter;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                shaAfter = Convert.ToHexString(SHA256.HashData(fs));

            return new PreviewResult(Path.GetFullPath(path), shaBefore, shaAfter,
                string.Equals(shaBefore, VerifiedCopySha256, StringComparison.OrdinalIgnoreCase), areas, excluded);
        }

        // ---------------------------------------------------------------------------------------------

        private static PreviewArea ReadSheet(ExcelWorksheet ws, SheetLayout layout, SkillsMatrixWorkbookColours colours, ref long nextId)
        {
            var warnings = new List<ReviewItem>();
            void Flag(string kind, string? cell, string message) => warnings.Add(new ReviewItem(kind, cell, message));
            var headerRows = new List<string>();
            var skippedColumns = new List<string>();
            int blankRows = 0, categoryRows = 0, outOfRangeBlank = 0;

            // Displayed colours of every cell the preview shows (the workbook's own colour standard)
            var display = new Dictionary<string, SkillsMatrixWorkbookColours.CellDisplay>();
            var inabRefs = new Dictionary<long, string>();
            var headerRefs = new Dictionary<long, string>();
            var groupRefs = new Dictionary<long, string>();
            string Remember(int row, int col)
            {
                string key = $"{ws.Name}!{Col(col)}{row}";
                if (!display.ContainsKey(key)) display[key] = colours.Display(row, col);
                return key;
            }
            long areaId = nextId++;
            var area = new SkillAreaInfo(areaId, ws.Name, ws.Name);
            int lastRow = ws.Dimension?.End.Row ?? 0;

            // In-range cells = the sheet's own data-validation ranges
            var ranges = ReadValidationRanges(ws);
            bool hasValidation = ranges.Count > 0;
            if (!hasValidation)
                Flag(ReviewKind.NoValidationRange, null, "This sheet has no data-validation range, so every person/skill cell is treated as part of the matrix.");

            var legend = ReadLegend(ws, layout.LegendRange);
            if (legend.NeedsConfirmation)
                Flag(ReviewKind.AmbiguousLegend, legend.SourceRef, $"Level definitions need confirmation: {legend.LegendNote}");

            var categories = new List<SkillCategoryInfo>();
            var skills = new List<SkillRowInfo>();
            var members = new List<SkillMemberInfo>();
            var cells = new Dictionary<(long, long), SkillCellInfo>();

            foreach (var block in layout.Blocks)
            {
                // Person columns: named header cells; unnamed columns only if they hold values
                var blockMembers = new List<(SkillMemberInfo Member, int Col)>();
                headerRows.Add($"row {block.HeaderRow}, people in columns {Col(block.FirstPersonCol)}:{Col(block.LastPersonCol)}, " +
                               $"skills in column {Col(block.SkillCol)}" +
                               (block.InabCol.HasValue ? $", INAB in column {Col(block.InabCol.Value)}" : ", no INAB column") +
                               $", data from row {block.FirstDataRow}");
                for (int c = block.FirstPersonCol; c <= block.LastPersonCol; c++)
                {
                    string header = Text(ws.Cells[block.HeaderRow, c]);
                    bool hasValues = Enumerable.Range(block.FirstDataRow, Math.Max(0, lastRow - block.FirstDataRow + 1))
                                               .Any(r => ws.Cells[r, c].Value != null || !string.IsNullOrEmpty(ws.Cells[r, c].Formula));
                    if (header.Length == 0 && !hasValues)
                    {
                        skippedColumns.Add($"{ws.Name}!{Col(c)}{block.HeaderRow} - no name in the header row and no values");
                        continue;
                    }
                    if (header.Length == 0)
                    {
                        header = $"(unnamed column {Col(c)})";
                        Flag(ReviewKind.UnnamedColumn, $"{ws.Name}!{Col(c)}{block.HeaderRow}",
                             $"Column {Col(c)} has values but no name in the header row ({ws.Name}!{Col(c)}{block.HeaderRow}).");
                    }
                    var member = new SkillMemberInfo(nextId++, 0, "", false, header, Subgroup(ws, block.SubgroupRows, c), Col(c));
                    blockMembers.Add((member, c));
                    members.Add(member);
                    headerRefs[member.MemberId] = Remember(block.HeaderRow, c);
                    if (SubgroupCell(ws, block.SubgroupRows, c) is (int gr, int gc))
                        groupRefs[member.MemberId] = Remember(gr, gc);
                }

                long? currentCategory = null;
                for (int r = block.FirstDataRow; r <= lastRow; r++)
                {
                    var skillCell = ws.Cells[r, block.SkillCol];
                    string skillName = Text(skillCell);
                    string inabRaw = block.InabCol.HasValue ? Text(ws.Cells[r, block.InabCol.Value]) : "";
                    bool anyNumber = blockMembers.Any(m => ws.Cells[r, m.Col].Value is double or int or decimal);
                    bool anyValue = blockMembers.Any(m => ws.Cells[r, m.Col].Value != null || !string.IsNullOrEmpty(ws.Cells[r, m.Col].Formula));

                    // Category from a dedicated column (Software sheet 'Area', merged vertically)
                    if (block.CategoryCol.HasValue)
                    {
                        string catText = MergedText(ws, r, block.CategoryCol.Value);
                        if (catText.Length > 0 && (currentCategory == null || categories.Last(cg => cg.CategoryId == currentCategory).Name != catText))
                        {
                            var (cr, cc) = MergedTopLeft(ws, r, block.CategoryCol.Value);
                            var cat = new SkillCategoryInfo(nextId++, catText, Remember(cr, cc));
                            categories.Add(cat);
                            currentCategory = cat.CategoryId;
                        }
                    }
                    else if (skillName.Length > 0 && inabRaw.Length == 0 && !anyNumber && HasFill(skillCell))
                    {
                        // Category header row: shaded label with no INAB value and no numeric levels
                        var cat = new SkillCategoryInfo(nextId++, skillName, Remember(r, block.SkillCol));
                        categories.Add(cat);
                        categoryRows++;
                        currentCategory = cat.CategoryId;
                        foreach (var bm in blockMembers.Where(x => ws.Cells[r, x.Col].Value != null))
                            Flag(ReviewKind.CategoryRowValue, $"{ws.Name}!{Col(bm.Col)}{r}",
                                 $"Value '{Text(ws.Cells[r, bm.Col])}' in category header row ({ws.Name}!{Col(bm.Col)}{r}) - not shown.");
                        continue;
                    }

                    if (skillName.Length == 0 && inabRaw.Length == 0 && !anyValue) { blankRows++; continue; } // blank row

                    if (skillName.Length == 0)
                    {
                        skillName = "(no skill name in source)";
                        Flag(ReviewKind.MissingSkillName, $"{ws.Name}!{Col(block.SkillCol)}{r}",
                             $"Row {r} has an INAB value and/or level cells but no skill name ({ws.Name}!{Col(block.SkillCol)}{r}) - shown as '(no skill name in source)'.");
                    }

                    if (currentCategory == null)
                    {
                        var cat = new SkillCategoryInfo(nextId++, "(no category heading)", $"{ws.Name}!{Col(block.SkillCol)}{r}");
                        categories.Add(cat);
                        currentCategory = cat.CategoryId;
                        Flag(ReviewKind.NoCategoryHeading, cat.SourceRef,
                             $"Skill rows start at {cat.SourceRef} before any category heading - grouped under '(no category heading)'.");
                    }

                    bool? inab = inabRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase) ? true
                               : inabRaw.Equals("No", StringComparison.OrdinalIgnoreCase) ? false
                               : null;
                    if (inab == null && inabRaw.Length > 0)
                        Flag(ReviewKind.UnexpectedInab, $"{ws.Name}!{Col(block.InabCol!.Value)}{r}",
                             $"Unexpected INAB value '{inabRaw}' ({ws.Name}!{Col(block.InabCol!.Value)}{r}) - shown as written.");

                    bool hidden = ws.Row(r).Hidden;
                    var skill = new SkillRowInfo(nextId++, currentCategory.Value, skillName, inab,
                        inabRaw.Length > 0 ? inabRaw : null, hidden, Remember(r, block.SkillCol));
                    skills.Add(skill);
                    if (block.InabCol.HasValue)
                        inabRefs[skill.SkillId] = Remember(r, block.InabCol.Value);
                    if (hidden)
                        Flag(ReviewKind.HiddenRow, skill.SourceRef, $"Row {r} '{skillName}' is hidden in the workbook.");

                    foreach (var (member, c) in blockMembers)
                    {
                        var info = ClassifyCell(ws, r, c, !hasValidation || InRange(ranges, r, c));
                        if (info == null) { outOfRangeBlank++; continue; } // blank and outside the source range: no record
                        cells[(skill.SkillId, member.MemberId)] = info;
                        Remember(r, c);
                        if (info.Status == "Flagged")
                            Flag(FlagKind(info), info.SourceCell, $"{info.SourceCell}: {info.FlagReason} (value: {info.RawValue}).");
                    }
                }
            }

            foreach (var dup in skills.Where(s => s.Name != "(no skill name in source)")
                                      .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                Flag(ReviewKind.DuplicateSkill, dup.First().SourceRef,
                     $"Skill name '{dup.Key}' appears {dup.Count()} times on this sheet (kept separately: {string.Join(", ", dup.Select(d => d.SourceRef))}).");

            // Key cells (with their displayed colours) and the column-heading cells
            var legendCells = new List<LegendCell>();
            var (lr1, lc1, lr2, lc2) = ParseRange(layout.LegendRange);
            for (int r = lr1; r <= lr2; r++)
            for (int c = lc1; c <= lc2; c++)
            {
                var m = LegendLabel.Match(Text(ws.Cells[r, c]));
                if (!m.Success) continue;
                string label = Text(ws.Cells[r, c]);
                string right = c + 1 <= lc2 ? Text(ws.Cells[r, c + 1]) : "";
                legendCells.Add(new LegendCell(int.Parse(m.Groups[1].Value), label,
                    right.Length > 0 && !LegendLabel.IsMatch(right) ? right : null, Remember(r, c)));
            }
            var first = layout.Blocks[0];
            string? skillHeaderRef = Remember(first.HeaderRow, first.SkillCol);
            string? inabHeaderRef = first.InabCol.HasValue ? Remember(first.HeaderRow, first.InabCol.Value) : null;

            // Colour inconsistencies inside the workbook are shown as written, and listed for review
            AddColourNotes(warnings, cells.Values.Where(x => x.Status == "Level").Select(x => (Key: $"level {x.Level}", x.SourceCell)), display);
            AddColourNotes(warnings, skills.Where(s => s.InabRaw != null && inabRefs.ContainsKey(s.SkillId))
                                           .Select(s => (Key: $"INAB '{s.InabRaw}'", inabRefs[s.SkillId])), display);

            var data = new SkillMatrixData(area, legend, categories, skills, members, cells, Array.Empty<SkillExcludedColumn>());
            var preview = new PreviewColours(display, inabRefs, headerRefs, groupRefs, skillHeaderRef, inabHeaderRef, legendCells);
            var scope = new SheetScope($"{ws.Name}!{layout.LegendRange}", headerRows,
                ranges.Select(x => $"{ws.Name}!{Col(x.C1)}{x.R1}:{Col(x.C2)}{x.R2}").ToList(),
                lastRow, skippedColumns, blankRows, categoryRows, outOfRangeBlank);
            return new PreviewArea(area, data, warnings, preview, scope);
        }

        private static string FlagKind(SkillCellInfo info) =>
            info.RawValue?.StartsWith("=") == true ? ReviewKind.Formula
            : info.FlagReason?.StartsWith("Value outside") == true ? ReviewKind.OutOfRange
            : ReviewKind.UnexpectedValue;

        // Lists cells whose displayed fill differs from the most common fill used for the same value on the sheet
        private static void AddColourNotes(List<ReviewItem> warnings, IEnumerable<(string Key, string Ref)> items,
                                           Dictionary<string, SkillsMatrixWorkbookColours.CellDisplay> display)
        {
            static string F(System.Drawing.Color? c) => c.HasValue ? $"#{c.Value.R:X2}{c.Value.G:X2}{c.Value.B:X2}" : "no fill";
            foreach (var group in items.GroupBy(i => i.Key))
            {
                var fills = group.Select(i => (i.Ref, Fill: F(display[i.Ref].Fill))).ToList();
                string usual = fills.GroupBy(f => f.Fill).OrderByDescending(g => g.Count()).First().Key;
                foreach (var odd in fills.Where(f => f.Fill != usual))
                    warnings.Add(new ReviewItem(ReviewKind.ColourInconsistency, odd.Ref,
                        $"{odd.Ref}: {group.Key} is coloured {odd.Fill} in the workbook (most {group.Key} cells on this sheet are {usual}) - shown as in the workbook."));
            }
        }

        private static SkillCellInfo? ClassifyCell(ExcelWorksheet ws, int r, int c, bool inRange)
        {
            var cell = ws.Cells[r, c];
            string src = $"{ws.Name}!{Col(c)}{r}";
            string formula = cell.Formula ?? "";
            object? v = cell.Value;

            if (formula.Length > 0)
                return new SkillCellInfo("Flagged", null, "=" + formula,
                    $"Formula in a proficiency cell (cached result: {v ?? "none"}) - not converted to a level", src);

            if (v == null)
                return inRange ? new SkillCellInfo("Unassessed", null, null, null, src) : null;

            if (v is double or int or decimal)
            {
                double d = Convert.ToDouble(v);
                if (d == Math.Floor(d) && d >= 1 && d <= 5)
                    return inRange
                        ? new SkillCellInfo("Level", (int)d, null, null, src)
                        : new SkillCellInfo("Flagged", null, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), "Value outside the sheet's data-validation range", src);
                return new SkillCellInfo("Flagged", null, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), "Number that is not a whole level 1-5", src);
            }

            if (v is string s)
                return new SkillCellInfo("Flagged", null, s,
                    s.Trim().Length == 0 ? "Whitespace-only cell" : "Text value, not a level", src);

            return new SkillCellInfo("Flagged", null, Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), $"Unexpected value type ({v.GetType().Name})", src);
        }

        private static SkillLegend ReadLegend(ExcelWorksheet ws, string range)
        {
            var (r1, c1, r2, c2) = ParseRange(range);
            var entries = new List<SkillLegendEntry>();
            for (int r = r1; r <= r2; r++)
            for (int c = c1; c <= c2; c++)
            {
                var m = LegendLabel.Match(Text(ws.Cells[r, c]));
                if (!m.Success) continue;
                int level = int.Parse(m.Groups[1].Value);
                string label = Text(ws.Cells[r, c]);
                string? description = null;
                if (c + 1 <= c2)
                {
                    string right = Text(ws.Cells[r, c + 1]);
                    if (right.Length > 0 && !LegendLabel.IsMatch(right)) description = right;
                }
                if (!entries.Any(e => e.Level == level && e.Label.Equals(label, StringComparison.OrdinalIgnoreCase)))
                    entries.Add(new SkillLegendEntry(1, level, label, description));
            }

            // Several distinct labels for one level -> number them as separate keys (as written)
            var numbered = entries.GroupBy(e => e.Level)
                .SelectMany(g => g.Select((e, i) => e with { KeyNumber = i + 1 }))
                .ToList();

            var notes = new List<string>();
            var conflicting = numbered.GroupBy(e => e.Level).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (conflicting.Count > 0)
                notes.Add($"the sheet gives different wording for level(s) {string.Join(", ", conflicting)} " +
                          $"({string.Join("; ", numbered.Where(e => conflicting.Contains(e.Level)).Select(e => e.Label))})");
            var missing = Enumerable.Range(1, 5).Except(numbered.Select(e => e.Level)).ToList();
            if (numbered.Count == 0)
                notes.Add("no legend was found");
            else if (missing.Count > 0)
                notes.Add($"the legend does not list level(s) {string.Join(", ", missing)}");

            bool needs = notes.Count > 0;
            return new SkillLegend($"{ws.Name} key", needs, needs ? string.Join("; ", notes) + "." : null,
                $"{ws.Name}!{range}", numbered);
        }

        private static List<(int R1, int C1, int R2, int C2)> ReadValidationRanges(ExcelWorksheet ws)
        {
            var list = new List<(int, int, int, int)>();
            foreach (var dv in ws.DataValidations)
            {
                string address = dv.Address?.Address ?? "";
                foreach (var part in address.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (CellRef.IsMatch(part))
                        list.Add(ParseRange(part));
                }
            }
            return list;
        }

        private static bool InRange(List<(int R1, int C1, int R2, int C2)> ranges, int r, int c) =>
            ranges.Any(x => r >= x.R1 && r <= x.R2 && c >= x.C1 && c <= x.C2);

        private static string? Subgroup(ExcelWorksheet ws, int[] rows, int col)
        {
            foreach (int r in rows)
            {
                string text = MergedText(ws, r, col);
                if (text.Length > 0) return text;
            }
            return null;
        }

        /// <summary>The cell holding a column's team/group heading (top-left of its merged range), if any.</summary>
        private static (int Row, int Col)? SubgroupCell(ExcelWorksheet ws, int[] rows, int col)
        {
            foreach (int r in rows)
            {
                var (tr, tc) = MergedTopLeft(ws, r, col);
                if (Text(ws.Cells[tr, tc]).Length > 0) return (tr, tc);
            }
            return null;
        }

        private static (int Row, int Col) MergedTopLeft(ExcelWorksheet ws, int r, int c)
        {
            foreach (var address in ws.MergedCells)
            {
                if (string.IsNullOrEmpty(address)) continue;
                var (r1, c1, r2, c2) = ParseRange(address);
                if (r >= r1 && r <= r2 && c >= c1 && c <= c2) return (r1, c1);
            }
            return (r, c);
        }

        /// <summary>Text of a cell, or of the top-left cell of the merged range containing it.</summary>
        private static string MergedText(ExcelWorksheet ws, int r, int c)
        {
            foreach (var address in ws.MergedCells)
            {
                if (string.IsNullOrEmpty(address)) continue;
                var (r1, c1, r2, c2) = ParseRange(address);
                if (r >= r1 && r <= r2 && c >= c1 && c <= c2)
                    return Text(ws.Cells[r1, c1]);
            }
            return Text(ws.Cells[r, c]);
        }

        private static bool HasFill(ExcelRange cell) =>
            cell.Style.Fill.PatternType != ExcelFillStyle.None;

        private static string Text(ExcelRange cell) => (cell.Value?.ToString() ?? "").Trim();

        private static (int R1, int C1, int R2, int C2) ParseRange(string address)
        {
            var m = CellRef.Match(address.Replace("$", "").Trim());
            if (!m.Success) throw new FormatException($"Unrecognised range '{address}'.");
            int r1 = int.Parse(m.Groups[2].Value), c1 = ColNum(m.Groups[1].Value);
            int r2 = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : r1;
            int c2 = m.Groups[3].Success ? ColNum(m.Groups[3].Value) : c1;
            return (r1, c1, r2, c2);
        }

        private static int ColNum(string letters) => letters.Aggregate(0, (n, ch) => n * 26 + (ch - 'A' + 1));

        private static string Col(int n)
        {
            string s = "";
            while (n > 0) { int m = (n - 1) % 26; s = (char)('A' + m) + s; n = (n - 1) / 26; }
            return s;
        }
    }
}
