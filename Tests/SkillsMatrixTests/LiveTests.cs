// Tests for SkillsMatrixLiveService (reading and saving the live copy) against a DISPOSABLE local database
// prepared by Database/Tests/Run-005Tests.ps1: stand-in tables (dummy people), migration 004, fixture import 1,
// migration 005. Refuses any target other than localhost:54329/live with the sentinel table.
using EmployeeTrainingTracker.Utilities;
using Npgsql;
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

        // --- Managers (stand-in: Carol (3) manages group 'Lab' = Bob (2) and Carol) ---
        var carol = GetAccess(conn, null, Carol);
        Check(!carol.IsAdmin && carol.Managed.SetEquals(new long[] { 2, 3 }) && carol.CanEdit(2) && !carol.CanEdit(3) && !carol.CanSee(1),
              "manager access: sees Bob and herself, edits Bob only");
        Throws<UnauthorizedAccessException>(() => GetAccess(conn, null, Bob), "an Employee has no access to the matrix", "Admins and Managers");
        var carolAreas = GetAreas(conn, carol);
        Check(carolAreas.Select(a => a.Name).SequenceEqual(new[] { "Lab", "Admin" }), "manager: sheets with her people (Lab: Bob; Admin: herself)");
        var cm = GetMatrix(conn, lab.AreaId, carol);
        Check(cm.Members.Count == 1 && cm.Members[0].EmployeeName == "Bob Tester" && cm.Cells.Keys.All(k => k.Item2 == bob) && cm.ExcludedColumns.Count == 0,
              "manager: Lab shows only Bob's column and cells; no excluded columns");
        var binBob = cm.Cells[(bin, bob)];
        r = SaveCells(conn, Carol, lab.AreaId, new[] { new CellChange(bin, bob, binBob.Version, 3) });
        var carolEntry = GetChanges(conn, lab.AreaId).First(c => c.Action == "SetCell");
        Check(r.Saved == 1 && GetMatrix(conn, lab.AreaId).Cells[(bin, bob)].Level == 3 && carolEntry.ChangedBy == "Carol Manager" && carolEntry.EmployeeName == "Bob Tester",
              "manager saves a level for her person, logged under her name");
        before = Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges");
        var aliceDen = GetMatrix(conn, lab.AreaId).Cells[(den, alice)];
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, lab.AreaId, new[] { new CellChange(den, alice, aliceDen.Version, 5) }),
                                            "manager cannot change someone she doesn't manage", "people you manage");
        var admCarolCol = Col(GetMatrix(conn, adminArea.AreaId), "Carol Manager");
        var ownCell = GetMatrix(conn, adminArea.AreaId).Cells[(fil, admCarolCol)];
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, adminArea.AreaId, new[] { new CellChange(fil, admCarolCol, ownCell.Version, 5) }),
                                            "manager cannot change her own level", "own levels");
        var binBobNow = GetMatrix(conn, lab.AreaId).Cells[(bin, bob)];
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, lab.AreaId, new[] {
                new CellChange(bin, bob, binBobNow.Version, 1), new CellChange(den, alice, aliceDen.Version, 5) }),
            "a batch with one forbidden cell is refused", "people you manage");
        Check(GetMatrix(conn, lab.AreaId).Cells[(bin, bob)].Level == 3 && Scalar("SELECT COUNT(*) FROM public.SkillMatrixLiveChanges") == before,
              "... and nothing in it was saved");
        var carolLog = GetChanges(conn, lab.AreaId, 1000, carol);
        Check(carolLog.Count > 0 && carolLog.All(c => c.Action == "Reseed" || c.EmployeeName == "Bob Tester"), "manager's history: only her people (and rebuilds)");
        // Taken out of her group: refused at once (access is re-read inside the save)
        new NpgsqlCommand("DELETE FROM public.GroupMembers WHERE GroupID = 1 AND EmployeeID = 2", conn).ExecuteNonQuery();
        Throws<UnauthorizedAccessException>(() => SaveCells(conn, Carol, lab.AreaId, new[] { new CellChange(bin, bob, binBobNow.Version, 1) }),
                                            "manager loses access as soon as the person leaves her group", "people you manage");
        new NpgsqlCommand("INSERT INTO public.GroupMembers (GroupID, EmployeeID) VALUES (1, 2)", conn).ExecuteNonQuery();
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

        Console.WriteLine();
        Console.WriteLine($"LIVE {_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }
}
