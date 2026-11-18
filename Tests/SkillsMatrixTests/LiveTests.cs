// Tests for SkillsMatrixLiveService (reading and saving the live copy) against a DISPOSABLE local database
// prepared by Database/Tests/Run-005Tests.ps1: stand-in tables (dummy people), migration 004, fixture import 1,
// migration 005. Refuses any target other than localhost:54329/live with the sentinel table.
using EmployeeTrainingTracker.Utilities;
using Npgsql;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixImportPlan;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixLiveService;

internal static class LiveTests
{
    private static int _passed, _failed;

    private static void Check(bool ok, string name, string detail = "")
    {
        if (ok) { _passed++; Console.WriteLine($"[PASS] {name}"); }
        else { _failed++; Console.WriteLine($"[FAIL] {name}{(detail.Length > 0 ? " -> " + detail : "")}"); }
    }

    private static void Throws<T>(Action action, string name, string messagePart) where T : Exception
    {
        try { action(); Check(false, name, "no exception"); }
        catch (T ex) { Check(ex.Message.Contains(messagePart, StringComparison.OrdinalIgnoreCase), name, ex.Message); }
        catch (Exception ex) { Check(false, name, $"{ex.GetType().Name}: {ex.Message}"); }
    }

    // ---------------------------------------------------------------------------------------------
    // Re-import with merge (SkillsMatrixMerge + SkillsMatrixImportWriter)

    /// <summary>
    /// A small 'Innovation' sheet. Version 1: Alice (C), Bob (D); skills one..four = 1, 2, 3, 4 for both.
    /// Version 2 (the 'next' workbook): Alice skill two 2 -> 3; Alice skill three 3 -> 4; 'Skill four' renamed 'Skill 4';
    /// Bob skill one 1 -> 5; new row 'Skill five'; new person Carol (E); a new person right of the usual columns (F).
    /// </summary>
    private static string MergeWorkbook(string path, int version)
    {
        ExcelPackage.License.SetNonCommercialOrganization("Student");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Innovation");
        string[] key = { "1: To be Trained", "2: In Training", "3: Requires Supervision", "4: Skilled in Area", "5: Skilled to train others" };
        for (int i = 0; i < key.Length; i++) ws.Cells[1, i + 1].Value = key[i];
        ws.Cells["A4"].Value = "Skill"; ws.Cells["B4"].Value = "INAB accredited"; ws.Cells["C4"].Value = "Alice Example"; ws.Cells["D4"].Value = "Bob O'Test";
        ws.Cells["G4"].Value = "Skill"; ws.Cells["H4"].Value = "INAB accredited"; ws.Cells["I4"].Value = "Bob O'Test";
        foreach (var c in new[] { "A5", "G5" })
        {
            ws.Cells[c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[c].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xD9, 0xD9, 0xD9));
        }
        ws.Cells["A5"].Value = "Innovation table 1"; ws.Cells["G5"].Value = "Innovation table 2";
        void Row(int r, string skill, string inab, int alice, int bob) { ws.Cells[r, 1].Value = skill; ws.Cells[r, 2].Value = inab; ws.Cells[r, 3].Value = alice; ws.Cells[r, 4].Value = bob; }
        Row(6, "Skill one", "Yes", 1, version == 1 ? 1 : 5);
        Row(7, "Skill two", "Yes", version == 1 ? 2 : 3, 2);
        Row(8, "Skill three", "No", version == 1 ? 3 : 4, 3);
        Row(9, version == 1 ? "Skill four" : "Skill 4", "No", 4, 4);
        ws.Cells["G6"].Value = "Other skill"; ws.Cells["H6"].Value = "No"; ws.Cells["I6"].Value = 5;
        int lastRow = 9;
        if (version == 2)
        {
            Row(10, "Skill five", "Yes", 2, 3);
            lastRow = 10;
            ws.Cells["E4"].Value = "Carol Sample"; ws.Cells["E6"].Value = 2;
            ws.Cells["F4"].Value = "Eve Closname";   // right of the usual columns (C:E): included automatically
        }
        foreach (var range in new[] { $"C6:F{lastRow}", "I6:L6" })
        {
            var dv = ws.DataValidations.AddListValidation(range);
            foreach (var v in new[] { "1", "2", "3", "4", "5" }) dv.Formula.Values.Add(v);
        }
        p.SaveAs(new FileInfo(path));
        return path;
    }

    private static void MergeTests(NpgsqlConnection conn)
    {
        long Scalar(string sql) => Convert.ToInt64(new NpgsqlCommand(sql, conn).ExecuteScalar());
        const int Alice = 1;
        // Dummy names as in the dummy employee list (disposable database)
        new NpgsqlCommand("UPDATE public.Employees SET FullName = 'Alice Example' WHERE EmployeeID = 1; UPDATE public.Employees SET FullName = 'Bob O Test' WHERE EmployeeID = 2; " +
                          "UPDATE public.Employees SET FullName = 'Carol Sample' WHERE EmployeeID = 3", conn).ExecuteNonQuery();
        string root = Path.Combine(Path.GetTempPath(), "SkillsMatrixMergeTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var d1 = new Dictionary<string, (string, string, string)> { ["Alice Example"] = (Confirmed, "1", ""), ["Bob O'Test"] = (Confirmed, "2", "") };
            var plan1 = ImportTests.PlanForWorkbook(MergeWorkbook(Path.Combine(root, "v1.xlsx"), 1), root, d1, out _);
            Check(plan1.Errors.Count == 0, "merge: version 1 plan ready", string.Join(" | ", plan1.Errors));
            SkillsMatrixImportWriter.Write(conn, plan1, plan1.Acknowledgements.Select(a => a.Key).ToHashSet(), SkillsMatrixImportWriter.ExpectedConfirmation(plan1), Alice);

            var area = GetAreas(conn).Single(a => a.Name == "Innovation");
            var m = GetMatrix(conn, area.AreaId);
            long Sk(SkillsMatrixService.SkillMatrixData d, string n) => d.Skills.Single(s => s.Name == n).SkillId;
            long Pe(SkillsMatrixService.SkillMatrixData d, string n) => d.Members.First(x => x.EmployeeName == n).MemberId;   // first column (table 1)
            long ali = Pe(m, "Alice Example"), bob = Pe(m, "Bob O Test");
            int V(string skill, long who) => m.Cells[(Sk(m, skill), who)].Version;
            var r = SaveCells(conn, Alice, area.AreaId, new[] {
                new CellChange(Sk(m, "Skill one"), ali, V("Skill one", ali), 5),      // only in the app        -> kept
                new CellChange(Sk(m, "Skill two"), ali, V("Skill two", ali), 5),      // Excel 3 too            -> conflict
                new CellChange(Sk(m, "Skill three"), ali, V("Skill three", ali), 4),  // Excel 4 too            -> same
                new CellChange(Sk(m, "Skill four"), bob, V("Skill four", bob), 1),    // row renamed in Excel   -> lost
            });
            Check(r.Saved == 4, "merge: four changes made in the app");

            var d2 = new Dictionary<string, (string, string, string)>(d1) { ["Carol Sample"] = (Confirmed, "3", ""), ["Eve Closname"] = (Excluded, "", "test: not imported") };
            var plan2 = ImportTests.PlanForWorkbook(MergeWorkbook(Path.Combine(root, "v2.xlsx"), 2), root, d2, out var dry2);
            Check(plan2.Errors.Count == 0, "merge: version 2 plan ready", string.Join(" | ", plan2.Errors));
            Check(dry2.ReviewItems.Any(i => i.Kind == SkillsMatrixWorkbookPreview.ReviewKind.AddedColumn && i.Cell == "Innovation!F4")
                  && plan2.Areas.Single().Columns.Any(c => c.SourceColumn == "F" && c.Disposition == "Excluded"),
                  "new column right of the usual ones (F) is read, reported, and goes through reconciliation");

            var rep = SkillsMatrixMerge.Compare(conn, plan2);
            var edits = rep.Edits.ToDictionary(e => (e.Skill, e.EmployeeName));
            Check(rep.KeptAutomatically == 1 && edits[("Skill one", "Alice Example")].Outcome == SkillsMatrixMerge.Outcome.KeepApp, "compare: changed only in the app -> kept");
            var conflict = edits[("Skill two", "Alice Example")];
            Check(rep.Conflicts.Count() == 1 && conflict.Outcome == SkillsMatrixMerge.Outcome.Conflict && conflict.BaseValue == "2" && conflict.AppValue == "5" && conflict.ExcelValue == "3",
                  "compare: changed in both differently -> conflict with import / app / workbook values");
            Check(rep.SameInBoth == 1, "compare: changed in both to the same value -> nothing to do");
            Check(rep.Lost == 1 && edits[("Skill four", "Bob O Test")].LostReason!.Contains("skill"), "compare: app change on a row renamed in Excel -> cannot be kept, with the reason");
            Check(rep.ExcelChangedCells == 1 && rep.ExcelChanges.Single().Contains("Skill one / Bob O Test: 1 -> 5"), "compare: changed only in the workbook -> taken", string.Join(" | ", rep.ExcelChanges));
            Check(rep.Structure.Any(s => s.Contains("new skill 'Innovation table 1 / Skill five'")) && rep.Structure.Any(s => s.Contains("new skill 'Innovation table 1 / Skill 4'"))
                  && rep.Structure.Any(s => s.Contains("no longer in the workbook 'Innovation table 1 / Skill four'")) && rep.Structure.Any(s => s.Contains("new person Carol Sample")),
                  "compare: structure changes listed (new / removed rows, new person)", string.Join(" | ", rep.Structure));
            Throws<InvalidOperationException>(() => SkillsMatrixMerge.Decide(rep, new bool?[] { null }), "an undecided conflict cannot be imported", "Decide every conflict");
            Check(SkillsMatrixMerge.Decide(rep, new bool?[] { true }).Keep.Count == 2, "keeping the app's value in a conflict adds it to the kept values");

            // Someone saves after the comparison -> the import is refused, nothing written
            var takeExcel = SkillsMatrixMerge.Decide(rep, new bool?[] { false });
            long importsBefore = Scalar("SELECT COUNT(*) FROM public.SkillMatrixImports");
            r = SaveCells(conn, Alice, area.AreaId, new[] { new CellChange(Sk(m, "Skill three"), bob, V("Skill three", bob), 2) });
            Throws<InvalidOperationException>(() => SkillsMatrixImportWriter.Write(conn, plan2, plan2.Acknowledgements.Select(a => a.Key).ToHashSet(),
                                                  SkillsMatrixImportWriter.ExpectedConfirmation(plan2), Alice, takeExcel),
                                              "import refused when the live copy changed after the comparison", "changed since the comparison");
            Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixImports") == importsBefore, "... and nothing was written");

            // Compare again (Bob's new change is app-only -> kept too), take Excel for the conflict, import
            rep = SkillsMatrixMerge.Compare(conn, plan2);
            var w = SkillsMatrixImportWriter.Write(conn, plan2, plan2.Acknowledgements.Select(a => a.Key).ToHashSet(),
                                                   SkillsMatrixImportWriter.ExpectedConfirmation(plan2), Alice, SkillsMatrixMerge.Decide(rep, new bool?[] { false }));
            Check(w.AppValuesKept == 2 && rep.KeptAutomatically == 2, "merge import: 2 app values kept", $"kept {w.AppValuesKept}");

            area = GetAreas(conn).Single(a => a.Name == "Innovation");
            m = GetMatrix(conn, area.AreaId);
            ali = Pe(m, "Alice Example"); bob = Pe(m, "Bob O Test");
            SkillsMatrixService.SkillCellInfo C(string skill, long who) => m.Cells[(Sk(m, skill), who)];
            Check(C("Skill one", ali).Level == 5 && C("Skill one", ali).UpdatedByName == "Alice Example", "after: app-only change kept, still shown as changed by its author");
            Check(C("Skill two", ali).Level == 3 && C("Skill two", ali).UpdatedAt == null, "after: conflict resolved to the workbook's value");
            Check(C("Skill three", ali).Level == 4 && C("Skill three", bob).Level == 2, "after: same-in-both value, and the later app change, in place");
            Check(C("Skill one", bob).Level == 5, "after: workbook-only change taken");
            Check(C("Skill 4", bob).Level == 4 && m.Skills.All(s => s.Name != "Skill four"), "after: renamed row follows the workbook");
            Check(m.Skills.Any(s => s.Name == "Skill five") && m.Members.Any(x => x.EmployeeName == "Carol Sample") && C("Skill one", Pe(m, "Carol Sample")).Level == 2,
                  "after: new row and new person from the workbook");
            Check(m.Members.All(x => x.SourceColumn != "F"), "after: the excluded new column is not on the sheet");
            var notes = GetChanges(conn, area.AreaId).Where(c => c.Note != null && c.Note.StartsWith("Kept the app's value over the workbook")).ToList();
            Check(notes.Count == 2 && notes.All(n => n.Action == "SetCell"), "after: each kept value logged");
            Check(EditsSinceRebuild(conn) == 2, "after: the kept values count as changes since the rebuild (the next import compares them again)");
            var again = SkillsMatrixMerge.Compare(conn, plan2);
            Check(again.KeptAutomatically == 2 && !again.Conflicts.Any(), "after: comparing the same workbook again keeps them again, no conflicts");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    public static int Run(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        if (csb.Host is not ("localhost" or "127.0.0.1") || csb.Port != 54329 || csb.Database != "live")
        {
            Console.WriteLine($"REFUSED: {csb.Host}:{csb.Port}/{csb.Database} is not the disposable test database (localhost:54329/live).");
            return 2;
        }
        using var conn = new NpgsqlConnection(csb.ConnectionString);
        conn.Open();
        using (var sentinel = new NpgsqlCommand("SELECT to_regclass('public.disposable_test_sentinel') IS NOT NULL", conn))
            if (sentinel.ExecuteScalar() is not true) { Console.WriteLine("REFUSED: no disposable-test sentinel table in this database."); return 2; }
        Console.WriteLine($"TARGET {conn.Host}:{conn.Port}/{conn.Database}");

        long Scalar(string sql) => Convert.ToInt64(new NpgsqlCommand(sql, conn).ExecuteScalar());
        const int Alice = 1, Bob = 2, Carol = 3;   // Alice = Admin, Bob = Employee, Carol = Manager (stand-in Users)

        // --- Reads ---
        var state = GetState(conn);
        Check(state.Installed && state.BasedOnImportId == 1 && state.SeededBy == "migration 005", "state: installed, built from import 1");
        var areas = GetAreas(conn);
        Check(areas.Select(a => a.Name).SequenceEqual(new[] { "Lab", "Admin" }), "areas: Lab, Admin in order");
        var lab = areas[0];
        var m = GetMatrix(conn, lab.AreaId);
        Check(m.Categories.Count == 2 && m.Skills.Count == 3 && m.Members.Count == 2 && m.Cells.Count == 5, "Lab: 2 categories, 3 skills, 2 people, 5 cells");
        Check(m.Members.Select(x => x.EmployeeName).SequenceEqual(new[] { "Alice Admin", "Bob Tester" }), "people in order, with employee names");
        Check(m.Members[0].SourceHeaderText == "Alice A" && m.Members[0].SubGroupLabel == "Senior" && m.Members[0].HeaderSourceRef == "Lab!C3",
              "header text, team and workbook reference from the import");
        // Migration 006 is installed here: one wording per level (Lab's key lists 1 and 5; 4 is filled in from Admin's key)
        Check(m.Legend.Entries.Select(e => e.Level).SequenceEqual(new[] { 1, 4, 5 }) && m.Legend.Entries.Single(e => e.Level == 4).Label == "4: Skilled"
              && !m.Legend.NeedsConfirmation && m.Legend.Entries.All(e => e.SourceRef == LevelKeyRef(e.Level)), "live key (006): levels 1, 4 (filled in), 5; no warning");
        Check(m.Formats != null && m.Formats.ContainsKey("Lab!C5") && m.Formats.ContainsKey(LevelKeyRef(4)), "workbook formats plus the key's level colours");
        Check(m.ExcludedColumns.Count == 1 && m.ExcludedColumns[0].SourceHeaderText == "Student X", "excluded column listed");
        long Skill(string name) => m.Skills.Single(s => s.Name == name).SkillId;
        long Col(SkillsMatrixService.SkillMatrixData d, string emp) => d.Members.Single(x => x.EmployeeName == emp).MemberId;
        long abr = Skill("Abrasion"), bin = Skill("Binder"), alice = Col(m, "Alice Admin"), bob = Col(m, "Bob Tester");
        var abrBob = m.Cells[(abr, bob)];
        Check(abrBob.Status == "Level" && abrBob.Level == 3 && abrBob.Version == 1 && abrBob.UpdatedAt == null && abrBob.SourceCell == "Lab!D5",
              "cell as imported: level 3, version 1, workbook ref");

        // --- Save ---
        var r = SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, 1, 4), new CellChange(bin, alice, 1, 2) });
        Check(r.Succeeded && r.Saved == 2, "Admin saves two changes", $"saved {r.Saved}, conflicts {r.Conflicts.Count}");
        m = GetMatrix(conn, lab.AreaId);
        Check(m.Cells[(abr, bob)].Level == 4 && m.Cells[(abr, bob)].Version == 2 && m.Cells[(abr, bob)].UpdatedByName == "Alice Admin",
              "re-read: level 4, version 2, changed by Alice Admin");
        Check(m.Cells[(bin, alice)].Status == "Level" && m.Cells[(bin, alice)].Level == 2, "re-read: not assessed -> 2");
        var log = GetChanges(conn, lab.AreaId);
        var set = log.Where(c => c.Action == "SetCell").ToList();
        Check(set.Count == 2 && set.Any(c => c.SkillName == "Abrasion" && c.EmployeeName == "Bob Tester" && c.OldValue == "3" && c.NewValue == "4")
              && set.Any(c => c.SkillName == "Binder" && c.OldValue == NotAssessedText && c.NewValue == "2") && set.All(c => c.ChangedBy == "Alice Admin"),
              "change log: one entry per cell with old and new values");
        Check(log.Any(c => c.Action == "Reseed"), "history includes the rebuild from the import");

        // Stale save: Abrasion x Bob was read at version 1 and is now 2 -> refused, and the other change in the same save is not saved either
        long before = Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges");
        long den = Skill("Density");
        r = SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, 1, 5), new CellChange(den, alice, 1, 5) });
        Check(!r.Succeeded && r.Conflicts.Count == 1 && r.Conflicts[0].CurrentValue == "4" && r.Conflicts[0].ChangedBy == "Alice Admin",
              "stale save refused with who/what changed it");
        m = GetMatrix(conn, lab.AreaId);
        Check(m.Cells[(abr, bob)].Level == 4 && m.Cells[(den, alice)].Level == 2 && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before,
              "all-or-nothing: neither cell changed, nothing logged");

        // Permissions and validation: nothing saved in any of these
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Bob, lab.AreaId, new[] { new CellChange(den, alice, 1, 5) }), "an Employee cannot save", "Admins and Managers");
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, 999, lab.AreaId, new[] { new CellChange(den, alice, 1, 5) }), "an unknown employee cannot save", "Admins and Managers");
        Throws<ArgumentException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(den, alice, 1, 6) }), "level 6 refused", "not allowed");
        Throws<ArgumentException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(den, alice, 1, 0) }), "level 0 refused", "not allowed");
        Throws<ArgumentException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(den, alice, 1, 5), new CellChange(den, alice, 1, 4) }),
                                  "same cell twice refused", "twice");
        Throws<ArgumentException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(den, bob, 1, 5) }),
                                  "a cell outside the sheet's range cannot be saved", "not on this sheet");
        var adminArea = areas[1];
        Throws<ArgumentException>(() => SaveCells(conn, Alice, adminArea.AreaId, new[] { new CellChange(den, alice, 1, 5) }),
                                  "a cell from another sheet is refused", "not on this sheet");
        Check(GetMatrix(conn, lab.AreaId).Cells[(den, alice)].Level == 2 && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before,
              "after the refusals: nothing changed, nothing logged");

        // Flagged cell resolved; a no-op change saves nothing
        var adm = GetMatrix(conn, adminArea.AreaId);
        long fil = adm.Skills.Single(s => s.Name == "Filing").SkillId, admAlice = Col(adm, "Alice Admin");
        Check(adm.Cells[(fil, admAlice)].Status == "Flagged", "Admin sheet: flagged cell as imported");
        r = SaveCells(conn, Alice, adminArea.AreaId, new[] { new CellChange(fil, admAlice, 1, 3) });
        var resolved = GetChanges(conn, adminArea.AreaId).First(c => c.Action == "SetCell");
        Check(r.Saved == 1 && GetMatrix(conn, adminArea.AreaId).Cells[(fil, admAlice)].Level == 3
              && resolved.OldValue == "flagged: =SUM(A1)" && resolved.Note == "Resolved a flagged imported value", "flagged cell resolved to 3, logged with the original value");
        before = Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges");
        r = SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, 2, 4) });
        Check(r.Succeeded && r.Saved == 0 && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before
              && GetMatrix(conn, lab.AreaId).Cells[(abr, bob)].Version == 2, "saving the same value: nothing written, nothing logged");

        // Set back to not assessed
        r = SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, 2, null) });
        var cleared = GetMatrix(conn, lab.AreaId).Cells[(abr, bob)];
        Check(r.Saved == 1 && cleared.Status == "Unassessed" && cleared.Level == null && cleared.Version == 3, "level -> not assessed");

        // Archived Admins cannot save (archive Alice, try, then restore - all in the test database)
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = TRUE WHERE EmployeeID = 1", conn).ExecuteNonQuery();
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, 3, 1) }), "an archived Admin cannot save", "Admins and Managers");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = FALSE WHERE EmployeeID = 1", conn).ExecuteNonQuery();

        // Undo from the history
        long ChangeIdOf(string skill, string emp, string from, string to) => GetChanges(conn, lab.AreaId)
            .Where(c => c.Action == "SetCell" && c.SkillName == skill && c.EmployeeName == emp && c.OldValue == from && c.NewValue == to)
            .Select(c => c.ChangeId).First();
        long binChange = ChangeIdOf("Binder", "Alice Admin", NotAssessedText, "2");
        r = UndoChange(conn, Alice, binChange);
        var undone = GetMatrix(conn, lab.AreaId).Cells[(bin, alice)];
        var undoEntry = GetChanges(conn, lab.AreaId).First(c => c.Action == "SetCell");
        Check(r.Saved == 1 && undone.Status == "Unassessed" && undoEntry.Note == $"Undo of change {binChange}"
              && undoEntry.OldValue == "2" && undoEntry.NewValue == NotAssessedText, "undo: Binder x Alice back to not assessed, logged as an undo");
        Throws<InvalidOperationException>(() => UndoChange(conn, Alice, binChange), "the same change cannot be undone twice (cell changed since)", "changed again");
        Throws<InvalidOperationException>(() => UndoChange(conn, Alice, ChangeIdOf("Abrasion", "Bob Tester", "3", "4")),
                                          "undo refused when the cell was changed again later", "changed again");
        Throws<ArgumentException>(() => UndoChange(conn, Alice, resolved.ChangeId), "a resolved flagged value cannot be put back", "flagged");
        Throws<ArgumentException>(() => UndoChange(conn, Alice, GetChanges(conn, lab.AreaId).First(c => c.Action == "Reseed").ChangeId),
                                  "a rebuild cannot be undone here", "Only level changes");
        long lastAbr = ChangeIdOf("Abrasion", "Bob Tester", "4", NotAssessedText);
        Throws<UnauthorizedAccessException>(() => UndoChange(conn, Bob, lastAbr), "an Employee cannot undo", "Admins and Managers");
        r = UndoChange(conn, Alice, lastAbr);
        Check(r.Saved == 1 && GetMatrix(conn, lab.AreaId).Cells[(abr, bob)].Level == 4, "undo the latest change: Abrasion x Bob back to 4");

        // Imported snapshot untouched by all of the above
        Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixCells WHERE Status = 'Level' AND Level = 3 AND SourceCell = 'Lab!D5'") == 1
              && Scalar("SELECT COUNT(*) FROM public.SkillMatrixCells WHERE Status = 'Flagged'") == 1, "imported snapshot unchanged");

        // --- Team row: an unmapped heading shows the person's manager from the groups ---
        // Alice: heading 'Senior', in no group -> heading as written. Bob: give him the heading 'Support Lab Staff'
        // (in the test database) -> group 'Lab' is managed by Carol -> 'Carol Manager'. People without a heading: none.
        Check(GetMatrix(conn, lab.AreaId).Members.Single(x => x.EmployeeName == "Alice Admin").SubGroupLabel == "Senior",
              "team row: unmapped heading, person in no group -> heading as written");
        new NpgsqlCommand($"UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = 'Support Lab Staff' WHERE ColumnID = {bob}", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, lab.AreaId).Members.Single(x => x.EmployeeName == "Bob Tester").SubGroupLabel == "Carol Manager",
              "team row: unmapped heading -> the person's manager from the groups");
        new NpgsqlCommand($"UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = 'Dermot Leonard Lab Manager' WHERE ColumnID = {bob}", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, lab.AreaId).Members.Single(x => x.EmployeeName == "Bob Tester").SubGroupLabel == "Dermot Leonard",
              "team row: a heading naming its manager -> that name (takes precedence over the groups)");
        new NpgsqlCommand($"UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = NULL WHERE ColumnID = {bob}", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, lab.AreaId).Members.Single(x => x.EmployeeName == "Bob Tester").SubGroupLabel == null,
              "team row: no heading -> nothing (sheets without team headings stay without a team row)");
        // Carol manages her own group: her own group is left out (she is in no other group) -> no manager found -> heading
        new NpgsqlCommand("UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = 'Support Lab Staff' WHERE EmployeeID = 3", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, adminArea.AreaId).Members.Single(x => x.EmployeeName == "Carol Manager").SubGroupLabel == "Support Lab Staff",
              "team row: a group the person manages themselves is not their manager");
        new NpgsqlCommand("UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = NULL WHERE EmployeeID = 3", conn).ExecuteNonQuery();

        // --- Archived people (Employees tab) are hidden; shown read-only on request; their levels are kept ---
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = TRUE WHERE EmployeeID = 2", conn).ExecuteNonQuery();
        var hidden = GetMatrix(conn, lab.AreaId);
        Check(hidden.Members.Count == 1 && hidden.Members[0].EmployeeName == "Alice Admin" && hidden.Cells.Keys.All(k => k.Item2 == alice),
              "archived: Bob hidden from the sheet (column and cells)");
        var withArchived = GetMatrix(conn, lab.AreaId, null, includeArchived: true);
        var bobArchived = withArchived.Members.Single(x => x.EmployeeName == "Bob Tester");
        Check(bobArchived.EmployeeArchived && withArchived.Cells.ContainsKey((abr, bob)), "archived: shown on request, marked archived, levels kept");
        before = Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges");
        Throws<ArgumentException>(() => SaveCells(conn, Alice, lab.AreaId, new[] { new CellChange(abr, bob, withArchived.Cells[(abr, bob)].Version, 2) }),
                                  "archived: saving their level is refused", "archived");
        Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before, "... nothing saved or logged");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = FALSE WHERE EmployeeID = 2", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, lab.AreaId).Members.Any(x => x.EmployeeName == "Bob Tester" && x.MemberId == bob),
              "unarchived: Bob is back, same column and levels");

        // --- Managers: the workbook's team headings decide (migration 007), not the app's groups ---
        // Stand-in: Carol (3) is a Manager and manages the app group 'Lab' (Bob and herself). Migration 007's seed
        // names production employees, none of whom exist here, so no team managers yet.
        Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixTeamManagers") == 0, "007 seeded no team managers in the test database");
        var carol = GetAccess(conn, null, Carol);
        Check(!carol.IsAdmin && carol.ManagedAreas.Count == 0 && GetAreas(conn, carol).Count == 0,
              "manager named on no team heading: no sheets (her app group does not count)");
        Throws<UnauthorizedAccessException>(() => GetMatrix(conn, lab.AreaId, carol), "... and cannot open a sheet", "manage a team");
        Throws<UnauthorizedAccessException>(() => GetAccess(conn, null, Bob), "an Employee has no access to the matrix", "Admins and Managers");

        // Name her as the manager of Lab's 'Senior' team heading
        new NpgsqlCommand("INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('Senior', 3, 'test')", conn).ExecuteNonQuery();
        carol = GetAccess(conn, null, Carol);
        Check(GetAreas(conn, carol).Select(a => a.Name).SequenceEqual(new[] { "Lab" }),
              "manager named on Lab's heading: Lab only (not Admin, although she is a column there)");
        var cm = GetMatrix(conn, lab.AreaId, carol);
        Check(cm.Members.Select(x => x.EmployeeName).SequenceEqual(new[] { "Alice Admin", "Bob Tester" })
              && cm.Cells.Count == GetMatrix(conn, lab.AreaId).Cells.Count && cm.ExcludedColumns.Count == 0,
              "manager: the whole Lab sheet (everyone, every cell, as in the workbook); no excluded columns");
        Check(carol.CanEdit(lab.AreaId, 1) && carol.CanEdit(lab.AreaId, 2) && !carol.CanEdit(adminArea.AreaId, 1) && !carol.CanEdit(lab.AreaId, 3),
              "manager CanEdit: everyone on Lab, nobody on other sheets, never herself");
        Throws<UnauthorizedAccessException>(() => GetMatrix(conn, adminArea.AreaId, carol), "manager cannot open a sheet she manages no team on", "manage a team");
        Throws<UnauthorizedAccessException>(() => GetChanges(conn, adminArea.AreaId, 1000, carol), "... nor see its history", "manage a team");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = TRUE WHERE EmployeeID = 2", conn).ExecuteNonQuery();
        Check(GetMatrix(conn, lab.AreaId, carol).Members.All(x => x.EmployeeName != "Bob Tester"), "manager: archived people hidden as for Admins");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = FALSE WHERE EmployeeID = 2", conn).ExecuteNonQuery();

        // Saves for anyone on her sheet - Bob (in her group) and Alice (not in her group): the heading decides
        var binBob = cm.Cells[(bin, bob)];
        var aliceDen = cm.Cells[(den, alice)];
        r = SaveCells(conn, Carol, lab.AreaId, new[] { new CellChange(bin, bob, binBob.Version, 3), new CellChange(den, alice, aliceDen.Version, 5) });
        var carolEntry = GetChanges(conn, lab.AreaId).First(c => c.Action == "SetCell" && c.EmployeeName == "Bob Tester");
        Check(r.Saved == 2 && GetMatrix(conn, lab.AreaId).Cells[(bin, bob)].Level == 3 && GetMatrix(conn, lab.AreaId).Cells[(den, alice)].Level == 5
              && carolEntry.ChangedBy == "Carol Manager", "manager saves levels for anyone on her sheet (also outside her app group), logged under her name");
        var carolLog = GetChanges(conn, lab.AreaId, 1000, carol);
        Check(carolLog.Any(c => c.EmployeeName == "Alice Admin") && carolLog.Any(c => c.EmployeeName == "Bob Tester") && carolLog.Any(c => c.Action == "Reseed"),
              "manager's history: the whole sheet's changes (and rebuilds)");

        // Another sheet: refused. Her own column: refused, even on a sheet she manages; a batch with one such cell saves nothing
        before = Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges");
        var admNow = GetMatrix(conn, adminArea.AreaId);
        long admCarolCol = Col(admNow, "Carol Manager");
        var admAliceFil = admNow.Cells[(fil, admAlice)];
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, adminArea.AreaId, new[] { new CellChange(fil, admAlice, admAliceFil.Version, 1) }),
                                            "manager cannot change levels on a sheet she manages no team on", "manage a team");
        new NpgsqlCommand($"UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = 'Senior' WHERE ColumnID = {admCarolCol}", conn).ExecuteNonQuery();
        carol = GetAccess(conn, null, Carol);
        Check(GetAreas(conn, carol).Select(a => a.Name).SequenceEqual(new[] { "Lab", "Admin" }), "her heading on a second sheet: both sheets");
        var ownCell = GetMatrix(conn, adminArea.AreaId).Cells[(fil, admCarolCol)];
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, adminArea.AreaId, new[] { new CellChange(fil, admCarolCol, ownCell.Version, 5) }),
                                            "manager cannot change her own level", "own levels");
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, adminArea.AreaId, new[] {
                new CellChange(fil, admAlice, admAliceFil.Version, 1), new CellChange(fil, admCarolCol, ownCell.Version, 5) }),
            "a batch with one forbidden cell is refused", "own levels");
        Check(GetMatrix(conn, adminArea.AreaId).Cells[(fil, admAlice)].Level == admAliceFil.Level && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before,
              "... and nothing in it was saved");
        new NpgsqlCommand($"UPDATE public.SkillMatrixLiveColumns SET SubGroupLabel = NULL WHERE ColumnID = {admCarolCol}", conn).ExecuteNonQuery();

        // No longer named on the heading: refused at once (access is re-read inside the save)
        var binBobNow = GetMatrix(conn, lab.AreaId).Cells[(bin, bob)];
        new NpgsqlCommand("DELETE FROM public.SkillMatrixTeamManagers WHERE EmployeeID = 3", conn).ExecuteNonQuery();
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, lab.AreaId, new[] { new CellChange(bin, bob, binBobNow.Version, 1) }),
                                            "manager loses access as soon as the heading no longer names her", "manage a team");
        new NpgsqlCommand("INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('Senior', 3, 'test')", conn).ExecuteNonQuery();
        r = UndoChange(conn, Carol, carolEntry.ChangeId);
        Check(r.Saved == 1 && GetMatrix(conn, lab.AreaId).Cells[(bin, bob)].Level == 1, "manager undoes her own change (Bob's Binder back to 1)");

        // --- An approved import rebuilds the live copy (step 5) ---
        // The dummy plan names employees 1 and 2 'Alice Example' / 'Bob O Test': rename them for this test (disposable database)
        new NpgsqlCommand("UPDATE public.Employees SET FullName = 'Alice Example' WHERE EmployeeID = 1; UPDATE public.Employees SET FullName = 'Bob O Test' WHERE EmployeeID = 2", conn).ExecuteNonQuery();
        string root = Path.Combine(Path.GetTempPath(), "SkillsMatrixLiveTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = ImportTests.DummyInnovationPlan(root);
            var acks = plan.Acknowledgements.Select(a => a.Key).ToHashSet();
            long editsBefore = EditsSinceRebuild(conn) ?? -1;
            var w = SkillsMatrixImportWriter.Write(conn, plan, acks, SkillsMatrixImportWriter.ExpectedConfirmation(plan), Alice);
            var st = GetState(conn);
            var rebuilt = GetChanges(conn, GetAreas(conn)[0].AreaId).First(c => c.Action == "Reseed");
            Check(editsBefore > 0 && w.LiveCellsRebuilt == w.Cells && st.BasedOnImportId == w.ImportId && rebuilt.NewValue == $"import {w.ImportId}"
                  && rebuilt.ChangedBy == plan.Import.ImportedBy, "import rebuilds the live copy in the same transaction (cells, state, logged)");
            Check(EditsSinceRebuild(conn) == 0 && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveLevels") == 5L * GetAreas(conn).Count,
                  "after the rebuild: no edits pending, keys rebuilt for every sheet");
            Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'SetCell'") > 0, "earlier edits remain in the change log");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        MergeTests(conn);
        EvidenceTests(conn);
        CertificateSoftDeleteTests(conn);

        Console.WriteLine();
        Console.WriteLine($"LIVE {_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Supporting evidence (migration 009) through SkillsMatrixEvidenceService, on the dummy 'Innovation' sheet:
    /// import version 1, link certificates (rules, permissions, replace / remove), then import version 2 WITHOUT a
    /// merge and check every link is carried to the same skill / person, needs relinking where the workbook changed
    /// the level, and is closed where the row is gone. Stand-in certificates: Bob (2) 1, 2, 5 approved, 3 pending;
    /// Alice (1) 4 approved.
    /// </summary>
    private static void EvidenceTests(NpgsqlConnection conn)
    {
        const int Alice = 1, Bob = 2, Carol = 3;   // Admin, Employee, Manager
        Console.WriteLine();
        Console.WriteLine("-- Supporting evidence (migration 009) --");
        long Scalar(string sql) => Convert.ToInt64(new NpgsqlCommand(sql, conn).ExecuteScalar());
        new NpgsqlCommand("UPDATE public.Employees SET FullName = 'Alice Example' WHERE EmployeeID = 1; UPDATE public.Employees SET FullName = 'Bob O Test' WHERE EmployeeID = 2; " +
                          "UPDATE public.Employees SET FullName = 'Carol Sample' WHERE EmployeeID = 3", conn).ExecuteNonQuery();
        string root = Path.Combine(Path.GetTempPath(), "SkillsMatrixEvidenceTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var d1 = new Dictionary<string, (string, string, string)> { ["Alice Example"] = (Confirmed, "1", ""), ["Bob O'Test"] = (Confirmed, "2", "") };
            var plan1 = ImportTests.PlanForWorkbook(MergeWorkbook(Path.Combine(root, "v1.xlsx"), 1), root, d1, out _);
            SkillsMatrixImportWriter.Write(conn, plan1, plan1.Acknowledgements.Select(a => a.Key).ToHashSet(), SkillsMatrixImportWriter.ExpectedConfirmation(plan1), Alice);

            var area = GetAreas(conn).Single(a => a.Name == "Innovation");
            var m = GetMatrix(conn, area.AreaId);
            long Sk(SkillsMatrixService.SkillMatrixData d, string n) => d.Skills.Single(s => s.Name == n).SkillId;
            long Pe(SkillsMatrixService.SkillMatrixData d, string n) => d.Members.First(x => x.EmployeeName == n).MemberId;
            long ali = Pe(m, "Alice Example"), bob = Pe(m, "Bob O Test");
            var admin = GetAccess(conn, null, Alice);
            SkillsMatrixEvidenceService.EvidenceState State(long skill, long col)
            {
                var mm = GetMatrix(conn, area.AreaId);
                var ev = SkillsMatrixEvidenceService.GetEvidence(conn, admin, area.AreaId);
                return SkillsMatrixEvidenceService.StateOf(ev.GetValueOrDefault((skill, col)), mm.Cells.GetValueOrDefault((skill, col)));
            }
            void Link(int who, long skill, long col, long cert) => SkillsMatrixEvidenceService.LinkCertificate(conn, who, area.AreaId, skill, col, cert);

            // Which certificates can be linked for Bob: his approved, not deleted ones
            var linkable = SkillsMatrixEvidenceService.GetLinkableCertificates(conn, admin, area.AreaId, bob).Select(c => c.CertificateId).OrderBy(x => x).ToList();
            Check(linkable.SequenceEqual(new long[] { 1, 2, 5 }), "linkable for Bob: his approved certificates only (not the pending one, not Alice's)", string.Join(",", linkable));

            // Link, validity follows the level
            Link(Alice, Sk(m, "Skill one"), bob, 1);       // Bob skill one = 1 (the workbook changes it to 5 in version 2)
            Link(Alice, Sk(m, "Skill two"), bob, 5);       // Bob skill two = 2 (unchanged in version 2)
            Link(Alice, Sk(m, "Skill two"), ali, 4);       // Alice skill two = 2 (version 2: 3)
            Link(Alice, Sk(m, "Skill four"), ali, 4);      // row renamed in version 2 -> closed
            Check(State(Sk(m, "Skill one"), bob) == SkillsMatrixEvidenceService.EvidenceState.Valid
                  && Scalar("SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE RemovedAt IS NULL") == 4
                  && Scalar($"SELECT LinkedLevel FROM public.SkillMatrixCellEvidence WHERE RemovedAt IS NULL AND SkillID = {Sk(m, "Skill one")} AND ColumnID = {bob}") == 1,
                  "linked: valid, at the cell's current level, with who linked it");
            var r = SaveCells(conn, Alice, area.AreaId, new[] { new CellChange(Sk(m, "Skill one"), bob, GetMatrix(conn, area.AreaId).Cells[(Sk(m, "Skill one"), bob)].Version, 3) });
            Check(State(Sk(m, "Skill one"), bob) == SkillsMatrixEvidenceService.EvidenceState.NeedsRelinking, "level changed 1 -> 3: needs relinking");
            SaveCells(conn, Alice, area.AreaId, new[] { new CellChange(Sk(m, "Skill one"), bob, GetMatrix(conn, area.AreaId).Cells[(Sk(m, "Skill one"), bob)].Version, 1) });
            Check(State(Sk(m, "Skill one"), bob) == SkillsMatrixEvidenceService.EvidenceState.Valid, "level back to 1: valid again (decision: the level decides)");

            // Refusals - nothing changes
            long rows = Scalar("SELECT COUNT(*) FROM public.SkillMatrixCellEvidence");
            Throws<ArgumentException>(() => Link(Alice, Sk(m, "Skill three"), bob, 4), "someone else's certificate is refused", "does not belong");
            Throws<ArgumentException>(() => Link(Alice, Sk(m, "Skill three"), bob, 3), "a certificate awaiting approval is refused", "approved");
            new NpgsqlCommand("UPDATE public.TrainingCertificates SET DeletedAt = now(), DeletedReason = 'DeletedDirectly', IsMarkedForDeletion = TRUE WHERE CertificateID = 5", conn).ExecuteNonQuery();
            Check(State(Sk(m, "Skill two"), bob) == SkillsMatrixEvidenceService.EvidenceState.CertificateRemoved, "a linked certificate that is deleted: needs relinking");
            Throws<ArgumentException>(() => Link(Alice, Sk(m, "Skill three"), bob, 5), "a deleted certificate is refused", "deleted");
            new NpgsqlCommand("UPDATE public.TrainingCertificates SET DeletedAt = NULL, DeletedReason = NULL, IsMarkedForDeletion = FALSE WHERE CertificateID = 5", conn).ExecuteNonQuery();
            Check(State(Sk(m, "Skill two"), bob) == SkillsMatrixEvidenceService.EvidenceState.Valid, "... restored: valid again");
            Throws<UnauthorizedAccessException>(() => Link(Bob, Sk(m, "Skill three"), bob, 1), "an Employee cannot link", "Admins and Managers");
            Throws<UnauthorizedAccessException>(() => Link(Carol, Sk(m, "Skill three"), bob, 1), "a Manager cannot link on a sheet where they manage no team", "manage a team");
            new NpgsqlCommand("UPDATE public.Employees SET IsArchived = TRUE WHERE EmployeeID = 2", conn).ExecuteNonQuery();
            Throws<ArgumentException>(() => Link(Alice, Sk(m, "Skill three"), bob, 1), "an archived person's level cannot get evidence", "archived");
            new NpgsqlCommand("UPDATE public.Employees SET IsArchived = FALSE WHERE EmployeeID = 2", conn).ExecuteNonQuery();
            SaveCells(conn, Alice, area.AreaId, new[] { new CellChange(Sk(m, "Skill three"), bob, GetMatrix(conn, area.AreaId).Cells[(Sk(m, "Skill three"), bob)].Version, null) });
            Throws<ArgumentException>(() => Link(Alice, Sk(m, "Skill three"), bob, 1), "a 'not assessed' cell cannot get evidence", "level 1-5");
            Throws<ArgumentException>(() => SkillsMatrixEvidenceService.LinkCertificate(conn, Alice, area.AreaId + 999, Sk(m, "Skill one"), bob, 1),
                                      "a cell from another sheet is refused", "not on this sheet");
            Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixCellEvidence") == rows, "... none of the refusals wrote anything");

            // Replace and remove: old links are closed, never deleted; one current link per cell
            Link(Alice, Sk(m, "Skill one"), bob, 2);
            Check(Scalar($"SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE SkillID = {Sk(m, "Skill one")} AND ColumnID = {bob} AND RemovedAt IS NULL") == 1
                  && Scalar($"SELECT CertificateID FROM public.SkillMatrixCellEvidence WHERE SkillID = {Sk(m, "Skill one")} AND ColumnID = {bob} AND RemovedAt IS NULL") == 2
                  && Scalar($"SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE SkillID = {Sk(m, "Skill one")} AND ColumnID = {bob} AND RemovedReason = 'Replaced'") == 1,
                  "replacing: one current link (the new certificate), the old one closed as Replaced");
            SkillsMatrixEvidenceService.RemoveLink(conn, Alice, area.AreaId, Sk(m, "Skill one"), bob);
            Check(State(Sk(m, "Skill one"), bob) == SkillsMatrixEvidenceService.EvidenceState.None
                  && Scalar("SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE RemovedReason = 'Unlinked'") == 1, "removing: no evidence, the link closed as Unlinked");
            Throws<InvalidOperationException>(() => SkillsMatrixEvidenceService.RemoveLink(conn, Alice, area.AreaId, Sk(m, "Skill one"), bob), "removing again is refused", "no evidence");
            Throws<PostgresException>(() => new NpgsqlCommand("DELETE FROM public.SkillMatrixCellEvidence", conn).ExecuteNonQuery(), "the database refuses to delete links", "not deleted");
            Link(Alice, Sk(m, "Skill one"), bob, 1);   // back, for the re-import below

            // Re-import (version 2, no merge: the workbook's values replace the app's)
            var d2 = new Dictionary<string, (string, string, string)>(d1) { ["Carol Sample"] = (Confirmed, "3", ""), ["Eve Closname"] = (Excluded, "", "test: not imported") };
            var plan2 = ImportTests.PlanForWorkbook(MergeWorkbook(Path.Combine(root, "v2.xlsx"), 2), root, d2, out _);
            var w = SkillsMatrixImportWriter.Write(conn, plan2, plan2.Acknowledgements.Select(a => a.Key).ToHashSet(), SkillsMatrixImportWriter.ExpectedConfirmation(plan2), Alice);
            Check(w.EvidenceCarried == 3 && w.EvidenceClosed == 1, "re-import: 3 links carried over, 1 closed (its row was renamed)", $"carried {w.EvidenceCarried}, closed {w.EvidenceClosed}");
            area = GetAreas(conn).Single(a => a.Name == "Innovation");
            m = GetMatrix(conn, area.AreaId);
            ali = Pe(m, "Alice Example"); bob = Pe(m, "Bob O Test");
            Check(State(Sk(m, "Skill two"), bob) == SkillsMatrixEvidenceService.EvidenceState.Valid, "after: unchanged level -> still valid, on the new cell");
            Check(State(Sk(m, "Skill one"), bob) == SkillsMatrixEvidenceService.EvidenceState.NeedsRelinking, "after: the workbook changed the level (1 -> 5) -> needs relinking");
            Check(State(Sk(m, "Skill two"), ali) == SkillsMatrixEvidenceService.EvidenceState.NeedsRelinking, "after: Alice skill two 2 -> 3 -> needs relinking");
            Check(Scalar("SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE RemovedReason = 'NotInNewImport'") == 1
                  && State(Sk(m, "Skill 4"), ali) == SkillsMatrixEvidenceService.EvidenceState.None, "after: the renamed row's link closed (NotInNewImport), the new row has none");
            Check(Scalar(@"SELECT COUNT(*) FROM public.SkillMatrixCellEvidence ev WHERE ev.RemovedAt IS NULL
                           AND NOT EXISTS (SELECT 1 FROM public.SkillMatrixLiveCells c WHERE c.SkillID = ev.SkillID AND c.ColumnID = ev.ColumnID)") == 0,
                  "after: every current link points at a cell of the new live copy");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Certificate soft delete (migration 008) through the app's CertificateService, against the stand-in
    /// TrainingCertificates (dummy rows 1-5): Bob (2) has 1 active, 2 a deletion request, 3 awaiting approval.
    /// </summary>
    private static void CertificateSoftDeleteTests(NpgsqlConnection conn)
    {
        const int Alice = 1, Bob = 2, Carol = 3;   // Admin, Employee, Manager
        Console.WriteLine();
        Console.WriteLine("-- Certificate soft delete (migration 008) --");
        // (the import test above renamed the stand-in people; put the names back)
        new NpgsqlCommand("UPDATE public.Employees SET FullName = 'Alice Admin' WHERE EmployeeID = 1; UPDATE public.Employees SET FullName = 'Bob Tester' WHERE EmployeeID = 2; " +
                          "UPDATE public.Employees SET FullName = 'Carol Manager' WHERE EmployeeID = 3", conn).ExecuteNonQuery();
        string Q(string sql) => Convert.ToString(new NpgsqlCommand(sql, conn).ExecuteScalar()) ?? "";
        string Row(long id) => Q($"SELECT concat_ws('|', Status, IsMarkedForDeletion, DeletedAt IS NOT NULL, DeletedByName, DeletedReason) FROM public.TrainingCertificates WHERE CertificateID = {id}");
        long count = Convert.ToInt64(new NpgsqlCommand("SELECT COUNT(*) FROM public.TrainingCertificates", conn).ExecuteScalar());

        // Approving a deletion request (Tasks: Delete), by a manager
        CertificateService.SoftDeleteCertificate(conn, 2, Carol, CertificateService.ReasonDeletionRequestApproved);
        Check(Row(2) == "Completed|t|t|Carol Manager|DeletionRequestApproved",
              "Delete: the record is kept, marked deleted with who and why, still marked for deletion", Row(2));
        Throws<InvalidOperationException>(() => CertificateService.SoftDeleteCertificate(conn, 2, Carol, CertificateService.ReasonDeletionRequestApproved),
                                          "deleting it again is refused", "already been deleted");
        Throws<InvalidOperationException>(() => CertificateService.SoftDeleteCertificate(conn, 999, Carol, CertificateService.ReasonDeletedDirectly),
                                          "deleting an unknown certificate is refused", "no longer exists");
        Throws<ArgumentException>(() => CertificateService.SoftDeleteCertificate(conn, 1, Carol, "Oops"), "an unknown reason is refused", "Unknown deletion reason");

        // Rejecting an upload awaiting approval (Tasks: Reject)
        CertificateService.SoftDeleteCertificate(conn, 3, Alice, CertificateService.ReasonUploadRejected);
        Check(Row(3) == "Pending|t|t|Alice Admin|UploadRejected", "Reject: kept as Pending, marked deleted", Row(3));

        var deleted = CertificateService.GetDeletedCertificates(conn);
        var ids = deleted.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToInt64(r["CertificateID"])).ToList();
        Check(ids.Count == 2 && ids.Contains(2) && ids.Contains(3)
              && deleted.Rows.Cast<System.Data.DataRow>().Any(r => (string)r["DeletedReason"] == "Rejected while awaiting approval")
              && deleted.Rows.Cast<System.Data.DataRow>().Any(r => (string)r["DeletedReason"] == "Deletion request approved"
                                                                   && (string)r["EmployeeName"] == "Bob Tester" && (string)r["DeletedByName"] == "Carol Manager"),
              "Deleted certificates list: both, with employee, readable reason and who deleted them");
        Check(Convert.ToInt64(new NpgsqlCommand("SELECT COUNT(*) FROM public.TrainingCertificates", conn).ExecuteScalar()) == count,
              "no certificate row was removed");

        // Restore: Admins only
        Throws<UnauthorizedAccessException>(() => CertificateService.RestoreCertificate(conn, 2, Carol), "a Manager cannot restore", "Only Admins");
        Throws<UnauthorizedAccessException>(() => CertificateService.RestoreCertificate(conn, 2, Bob), "an Employee cannot restore", "Only Admins");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = TRUE WHERE EmployeeID = 1", conn).ExecuteNonQuery();
        Throws<UnauthorizedAccessException>(() => CertificateService.RestoreCertificate(conn, 2, Alice), "an archived Admin cannot restore", "Only Admins");
        new NpgsqlCommand("UPDATE public.Employees SET IsArchived = FALSE WHERE EmployeeID = 1", conn).ExecuteNonQuery();
        Check(Row(2).Contains("|t|t|"), "... and nothing was restored");

        CertificateService.RestoreCertificate(conn, 2, Alice);
        Check(Row(2) == "Completed|f|f", "restored deletion: an active certificate again (no longer marked for deletion)", Row(2));
        CertificateService.RestoreCertificate(conn, 3, Alice);
        Check(Row(3) == "Pending|f|f", "restored rejected upload: awaiting approval again", Row(3));
        Throws<InvalidOperationException>(() => CertificateService.RestoreCertificate(conn, 3, Alice), "restoring a certificate that is not deleted is refused", "not deleted");
        Check(CertificateService.GetDeletedCertificates(conn).Rows.Count == 0, "Deleted certificates list empty again");
    }
}
