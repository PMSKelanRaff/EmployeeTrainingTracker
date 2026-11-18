using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixImportPlan;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Re-import with merge (decision 2026-11-11): compares a prepared import plan (the new workbook) with the live
    /// copy before importing, so levels changed in the app are not lost. Three versions of every cell:
    ///   base  = the import the live copy was built from,   app = the live copy now,   Excel = the new workbook.
    /// Rules:
    ///   * changed only in Excel           -> Excel's value (automatic: the rebuild takes it)
    ///   * changed only in the app         -> the app's value is KEPT (re-applied after the rebuild)
    ///   * changed in both, to the same    -> nothing to do
    ///   * changed in both, differently    -> CONFLICT: the person importing chooses, cell by cell
    ///   * changed in the app, but the skill / person / cell is not in the new workbook -> listed as LOST
    /// Structure (sheets, categories, skills, people) follows the workbook: the app cannot change structure.
    ///
    /// Read-only: Compare only reads. SkillsMatrixImportWriter applies the outcome in the import's transaction and
    /// refuses if the live copy changed after the comparison (LastChangeId).
    ///
    /// Matching: sheets by sheet name; skills by (sheet, category name, skill name, n-th occurrence of that name in
    /// that category) - so a skill renamed in Excel counts as removed + added; people by employee ID on the sheet.
    /// </summary>
    public static class SkillsMatrixMerge
    {
        public enum Outcome { KeepApp, Conflict, Lost }

        /// <summary>A level changed in the app since the live copy was built, and what the merge does with it.</summary>
        public sealed record AppEdit(string Sheet, string Category, string Skill, long EmployeeId, string EmployeeName,
            string BaseValue, string AppValue, int? AppLevel, string? ExcelValue, string ChangedBy, DateTime ChangedAt, long? ChangedByEmployeeId,
            long? PlanSkillKey, long? PlanColumnKey, Outcome Outcome, string? LostReason);

        public sealed record Report(long BasedOnImportId, long LastChangeId, IReadOnlyList<AppEdit> Edits, int SameInBoth,
            int ExcelChangedCells, IReadOnlyList<string> ExcelChanges, IReadOnlyList<string> Structure)
        {
            public IEnumerable<AppEdit> Conflicts => Edits.Where(e => e.Outcome == Outcome.Conflict);
            public int KeptAutomatically => Edits.Count(e => e.Outcome == Outcome.KeepApp);
            public int Lost => Edits.Count(e => e.Outcome == Outcome.Lost);
        }

        /// <summary>An app value to put back after the rebuild (plan keys identify the new workbook's skill and person).</summary>
        public sealed record Keep(long PlanSkillKey, long PlanColumnKey, int? Level, string AppValue, string ExcelValue,
            string ChangedBy, DateTime ChangedAt, long? ChangedByEmployeeId);

        /// <summary>What the import applies: refused if the live copy changed after LastChangeId.</summary>
        public sealed record MergePlan(long LastChangeId, IReadOnlyList<Keep> Keep);

        // ---------------------------------------------------------------------------------------------

        private sealed record LiveCell(string Sheet, string Category, string Skill, int Occurrence, long EmployeeId, int PersonOccurrence, string EmployeeName,
            string Value, string BaseValue, int? Level, DateTime? UpdatedAt, string? UpdatedBy, long? UpdatedById);

        /// <summary>Compares the plan with the live copy (read-only). Requires migration 005 and a built live copy.</summary>
        public static Report Compare(NpgsqlConnection conn, ImportPlan plan)
        {
            var state = SkillsMatrixLiveService.GetState(conn);
            if (!state.Installed || state.BasedOnImportId == null)
                throw new InvalidOperationException("There is no live copy to compare with; the import will build it.");
            long lastChange;
            using (var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(ChangeID), 0) FROM public.SkillMatrixLiveChanges", conn))
                lastChange = Convert.ToInt64(cmd.ExecuteScalar());

            // --- the live copy: every cell, with its value at the base import ---
            var live = new List<LiveCell>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT a.SourceSheet, lc.Name, ls.Name, occ.n,
                       col.EmployeeID, e.FullName, c.Status, c.Level, c.RawValue, sc.Status, sc.Level, sc.RawValue,
                       c.UpdatedAt, c.UpdatedByName, c.UpdatedByEmployeeID, pocc.n
                FROM public.SkillMatrixLiveCells c
                JOIN public.SkillMatrixLiveSkills ls     ON ls.SkillID = c.SkillID
                JOIN (SELECT s2.SkillID, ROW_NUMBER() OVER (PARTITION BY s2.AreaID, c2.Name, s2.Name ORDER BY s2.DisplayOrder, s2.SkillID) AS n
                      FROM public.SkillMatrixLiveSkills s2 JOIN public.SkillMatrixLiveCategories c2 ON c2.CategoryID = s2.CategoryID
                      WHERE NOT s2.IsDeleted) occ ON occ.SkillID = c.SkillID
                -- a person can appear in more than one column of a sheet (side-by-side tables): n-th column of that person
                JOIN (SELECT p2.ColumnID, ROW_NUMBER() OVER (PARTITION BY p2.AreaID, p2.EmployeeID ORDER BY p2.DisplayOrder, p2.ColumnID) AS n
                      FROM public.SkillMatrixLiveColumns p2 WHERE NOT p2.IsDeleted) pocc ON pocc.ColumnID = c.ColumnID
                JOIN public.SkillMatrixLiveCategories lc ON lc.CategoryID = ls.CategoryID
                JOIN public.SkillMatrixLiveColumns col   ON col.ColumnID = c.ColumnID
                JOIN public.Employees e                  ON e.EmployeeID = col.EmployeeID
                JOIN public.SkillMatrixLiveAreas la      ON la.AreaID = c.AreaID
                JOIN public.SkillMatrixAreas a           ON a.AreaID = la.SourceAreaID
                LEFT JOIN public.SkillMatrixCells sc     ON sc.CellID = c.SourceCellID
                WHERE NOT ls.IsDeleted AND NOT col.IsDeleted", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                    int? L(int i) => r.IsDBNull(i) ? null : r.GetInt16(i);
                    string value = SkillsMatrixLiveService.DescribeValue(r.GetString(6), L(7), S(8));
                    string baseValue = r.IsDBNull(9) ? "(not in the import)" : SkillsMatrixLiveService.DescribeValue(r.GetString(9), L(10), S(11));
                    live.Add(new LiveCell(r.GetString(0), r.GetString(1), r.GetString(2), Convert.ToInt32(r.GetValue(3)),
                        Convert.ToInt64(r.GetValue(4)), Convert.ToInt32(r.GetValue(15)), r.GetString(5), value, baseValue, L(7),
                        r.IsDBNull(12) ? null : r.GetDateTime(12), S(13), r.IsDBNull(14) ? null : r.GetInt64(14)));
                }
            }

            // --- the new workbook (plan): skills with their occurrence index, members, cells ---
            var planSkills = new Dictionary<(string Sheet, string Category, string Skill, int Occurrence), long>();
            var planMembers = new Dictionary<(string Sheet, long EmployeeId, int Occurrence), long>();
            var planCells = new Dictionary<(long SkillKey, long ColumnKey), PlanCell>();
            foreach (var area in plan.Areas)
            {
                var categories = area.Categories.ToDictionary(c => c.Key, c => c.Name);
                foreach (var g in area.Skills.GroupBy(s => (Category: categories[s.CategoryKey], s.Name)))
                {
                    int n = 0;
                    foreach (var s in g.OrderBy(s => s.DisplayOrder).ThenBy(s => s.SourceRow))
                        planSkills[(area.Sheet, g.Key.Category, g.Key.Name, ++n)] = s.Key;
                }
                foreach (var g in area.Columns.Where(c => c.Disposition == "Member" && c.EmployeeId != null).GroupBy(c => c.EmployeeId!.Value))
                {
                    int n = 0;
                    foreach (var c in g.OrderBy(c => c.DisplayOrder)) planMembers[(area.Sheet, g.Key, ++n)] = c.Key;
                }
                foreach (var cell in area.Cells) planCells[(cell.SkillKey, cell.ColumnKey)] = cell;
            }

            // --- each cell changed in the app: keep, conflict or lost ---
            var edits = new List<AppEdit>();
            int same = 0;
            foreach (var c in live.Where(c => c.UpdatedAt != null && c.Value != c.BaseValue))
            {
                string by = c.UpdatedBy ?? "?";
                AppEdit Edit(Outcome o, string? excel, long? sk, long? ck, string? lost) => new(c.Sheet, c.Category, c.Skill, c.EmployeeId, c.EmployeeName,
                    c.BaseValue, c.Value, c.Level, excel, by, c.UpdatedAt!.Value, c.UpdatedById, sk, ck, o, lost);

                if (!planSkills.TryGetValue((c.Sheet, c.Category, c.Skill, c.Occurrence), out long sKey))
                { edits.Add(Edit(Outcome.Lost, null, null, null, "the skill is not in the new workbook (removed, renamed or moved)")); continue; }
                if (!planMembers.TryGetValue((c.Sheet, c.EmployeeId, c.PersonOccurrence), out long cKey))
                { edits.Add(Edit(Outcome.Lost, null, null, null, "the person is not on this sheet in the new workbook")); continue; }
                if (!planCells.TryGetValue((sKey, cKey), out var pc))
                { edits.Add(Edit(Outcome.Lost, null, sKey, cKey, "the cell is outside the sheet's range in the new workbook")); continue; }

                string excel = SkillsMatrixLiveService.DescribeValue(pc.Status, pc.Level, pc.RawValue);
                if (excel == c.Value) { same++; continue; }
                edits.Add(Edit(excel == c.BaseValue ? Outcome.KeepApp : Outcome.Conflict, excel, sKey, cKey, null));
            }

            // --- cells changed only in Excel (taken automatically) ---
            var excelChanges = new List<string>();
            int excelChanged = 0;
            foreach (var c in live.Where(c => c.UpdatedAt == null || c.Value == c.BaseValue))
            {
                if (!planSkills.TryGetValue((c.Sheet, c.Category, c.Skill, c.Occurrence), out long sKey)
                    || !planMembers.TryGetValue((c.Sheet, c.EmployeeId, c.PersonOccurrence), out long cKey)
                    || !planCells.TryGetValue((sKey, cKey), out var pc)) continue;
                string excel = SkillsMatrixLiveService.DescribeValue(pc.Status, pc.Level, pc.RawValue);
                if (excel == c.Value) continue;
                excelChanged++;
                excelChanges.Add($"{c.Sheet} / {c.Skill} / {c.EmployeeName}: {c.Value} -> {excel}");
            }

            return new Report(state.BasedOnImportId.Value, lastChange, edits, same, excelChanged, excelChanges, Structure(conn, plan));
        }

        /// <summary>Sheets, skills and people added or removed in the new workbook, compared with the live copy.</summary>
        private static List<string> Structure(NpgsqlConnection conn, ImportPlan plan)
        {
            var liveSkills = new List<(string Sheet, string Category, string Skill)>();
            var livePeople = new List<(string Sheet, long EmployeeId, string Name)>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT a.SourceSheet, lc.Name, ls.Name FROM public.SkillMatrixLiveSkills ls
                JOIN public.SkillMatrixLiveCategories lc ON lc.CategoryID = ls.CategoryID
                JOIN public.SkillMatrixLiveAreas la ON la.AreaID = ls.AreaID JOIN public.SkillMatrixAreas a ON a.AreaID = la.SourceAreaID
                WHERE NOT ls.IsDeleted", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) liveSkills.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            using (var cmd = new NpgsqlCommand(@"
                SELECT a.SourceSheet, col.EmployeeID, e.FullName FROM public.SkillMatrixLiveColumns col
                JOIN public.Employees e ON e.EmployeeID = col.EmployeeID
                JOIN public.SkillMatrixLiveAreas la ON la.AreaID = col.AreaID JOIN public.SkillMatrixAreas a ON a.AreaID = la.SourceAreaID
                WHERE NOT col.IsDeleted", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) livePeople.Add((r.GetString(0), Convert.ToInt64(r.GetValue(1)), r.GetString(2)));

            var newSkills = plan.Areas.SelectMany(a => a.Skills.Select(s => (a.Sheet, Category: a.Categories.First(c => c.Key == s.CategoryKey).Name, s.Name))).ToList();
            var newPeople = plan.Areas.SelectMany(a => a.Columns.Where(c => c.Disposition == "Member" && c.EmployeeId != null)
                                                               .Select(c => (a.Sheet, EmployeeId: c.EmployeeId!.Value, Name: c.EmployeeName ?? c.SourceHeaderText))).ToList();

            var lines = new List<string>();
            var liveSheets = liveSkills.Select(s => s.Sheet).Concat(livePeople.Select(p => p.Sheet)).Distinct().ToList();
            var newSheets = plan.Areas.Select(a => a.Sheet).ToList();
            foreach (var s in newSheets.Except(liveSheets)) lines.Add($"New sheet: {s}");
            foreach (var s in liveSheets.Except(newSheets)) lines.Add($"Sheet no longer in the workbook (removed from the live copy): {s}");
            foreach (var sheet in newSheets.Intersect(liveSheets))
            {
                // Multisets: a duplicate name added or removed is counted
                var added = Minus(newSkills.Where(x => x.Sheet == sheet).Select(x => $"{x.Category} / {x.Name}"),
                                  liveSkills.Where(x => x.Sheet == sheet).Select(x => $"{x.Category} / {x.Skill}"));
                var removed = Minus(liveSkills.Where(x => x.Sheet == sheet).Select(x => $"{x.Category} / {x.Skill}"),
                                    newSkills.Where(x => x.Sheet == sheet).Select(x => $"{x.Category} / {x.Name}"));
                foreach (var a in added) lines.Add($"{sheet}: new skill '{a}'");
                foreach (var a in removed) lines.Add($"{sheet}: skill no longer in the workbook '{a}'");
                var livePeopleIds = livePeople.Where(p => p.Sheet == sheet).Select(p => p.EmployeeId).ToHashSet();
                var newPeopleIds = newPeople.Where(p => p.Sheet == sheet).Select(p => p.EmployeeId).ToHashSet();
                foreach (var p in newPeople.Where(p => p.Sheet == sheet && !livePeopleIds.Contains(p.EmployeeId))) lines.Add($"{sheet}: new person {p.Name}");
                foreach (var p in livePeople.Where(p => p.Sheet == sheet && !newPeopleIds.Contains(p.EmployeeId))) lines.Add($"{sheet}: person no longer on the sheet {p.Name}");
            }
            return lines;
        }

        private static List<string> Minus(IEnumerable<string> a, IEnumerable<string> b)
        {
            var counts = b.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var result = new List<string>();
            foreach (var x in a)
            {
                if (counts.TryGetValue(x, out int n) && n > 0) counts[x] = n - 1;
                else result.Add(x);
            }
            return result;
        }

        /// <summary>
        /// The merge to apply: every automatic keep, plus each conflict where keepApp[i] is true (index into Conflicts).
        /// Throws if a conflict has no decision.
        /// </summary>
        public static MergePlan Decide(Report report, IReadOnlyList<bool?> conflictKeepsApp)
        {
            var conflicts = report.Conflicts.ToList();
            if (conflictKeepsApp.Count != conflicts.Count || conflictKeepsApp.Any(d => d == null))
                throw new InvalidOperationException("Decide every conflict (keep the app's value or take the workbook's) before importing.");
            var keep = report.Edits.Where(e => e.Outcome == Outcome.KeepApp)
                .Concat(conflicts.Where((_, i) => conflictKeepsApp[i] == true))
                .Select(e => new Keep(e.PlanSkillKey!.Value, e.PlanColumnKey!.Value,
                                      e.AppValue == SkillsMatrixLiveService.NotAssessedText ? null : e.AppLevel,
                                      e.AppValue, e.ExcelValue!, e.ChangedBy, e.ChangedAt, e.ChangedByEmployeeId))
                .ToList();
            return new MergePlan(report.LastChangeId, keep);
        }

        /// <summary>The comparison as text (for the window and the saved plan summary).</summary>
        public static string Render(Report r)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("COMPARISON WITH THE LIVE COPY (what the import would do to changes made in the app)");
            sb.AppendLine(new string('=', 78));
            sb.AppendLine($"Live copy built from import {r.BasedOnImportId}.");
            sb.AppendLine($"  Changed only in the app - kept:                 {r.KeptAutomatically}");
            sb.AppendLine($"  Changed in both, differently - you choose:      {r.Conflicts.Count()}");
            sb.AppendLine($"  Changed in both to the same value:              {r.SameInBoth}");
            sb.AppendLine($"  Changed in the app, not in the new workbook:    {r.Lost}  (cannot be kept)");
            sb.AppendLine($"  Changed only in the workbook - taken:           {r.ExcelChangedCells}");
            sb.AppendLine();
            sb.AppendLine($"STRUCTURE (follows the workbook): {(r.Structure.Count == 0 ? "no changes" : r.Structure.Count + " change(s)")}");
            foreach (var s in r.Structure) sb.AppendLine("  " + s);
            if (r.Edits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("CHANGES MADE IN THE APP");
                foreach (var e in r.Edits)
                    sb.AppendLine($"  [{e.Outcome}] {e.Sheet} / {e.Skill} / {e.EmployeeName}: import {e.BaseValue}, app {e.AppValue} " +
                                  $"({e.ChangedBy} {e.ChangedAt:yyyy-MM-dd}), workbook {e.ExcelValue ?? "-"}" + (e.LostReason != null ? $" - {e.LostReason}" : ""));
            }
            if (r.ExcelChanges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("CHANGED ONLY IN THE WORKBOOK");
                foreach (var s in r.ExcelChanges.Take(500)) sb.AppendLine("  " + s);
                if (r.ExcelChanges.Count > 500) sb.AppendLine($"  ... and {r.ExcelChanges.Count - 500} more");
            }
            return sb.ToString();
        }
    }
}
