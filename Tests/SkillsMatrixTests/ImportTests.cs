// Targeted tests for import preparation (decisions -> plan -> checks) and the gated writer.
// Plan tests use dummy data only and no database. WriteToDisposableDatabase writes ONLY to a disposable local
// PostgreSQL instance started by the test script (it refuses any other host/port).
using EmployeeTrainingTracker.Utilities;
using Npgsql;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixDryRun;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixImportPlan;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixReconciliation;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixWorkbookPreview;
using static Program;

internal static class ImportTests
{
    /// <summary>Reviewer decisions for the dummy fixture (as a person would record them in the reconciliation CSV).</summary>
    public static readonly Dictionary<string, (string Decision, string Id, string Notes)> FixtureDecisions = new()
    {
        ["Alice Example"] = (Confirmed, "1", ""),
        ["alice  EXAMPLE"] = (Confirmed, "1", "same person, written differently on Admin"),
        ["Bob O'Test"] = (Confirmed, "2", ""),
        ["Dan Archived"] = (Confirmed, "4", "returning employee"),
        ["Eve Closename"] = (Excluded, "", "not the same person as Eve Closname"),
        ["Gina Twice"] = (Confirmed, "6", "checked with HR"),
        ["Frank Nomatch"] = (Excluded, "", "left the company"),
        ["(unnamed column I)"] = (Excluded, "", "no name in the workbook"),
        ["Student Carol Sample"] = (Excluded, "", "student"),
    };

    /// <summary>Writes the reconciliation CSV with the given decisions filled in, as a reviewer would.</summary>
    public static string WriteDecisions(DryRunResult r, string path, IReadOnlyDictionary<string, (string Decision, string Id, string Notes)> decisions,
                                        Func<List<string>, List<string>>? tamper = null)
    {
        var rows = ParseCsv(ReconciliationCsv(r));
        var lines = new List<string> { CsvLine(rows[0].ToArray()) };
        foreach (var row in rows.Skip(1).Where(x => x.Count > 1))
        {
            if (decisions.TryGetValue(row[1], out var d)) { row[10] = d.Decision; row[11] = d.Id; row[12] = d.Notes; }
            var final = tamper?.Invoke(row) ?? row;
            lines.Add(CsvLine(final.ToArray()));
        }
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new System.Text.UTF8Encoding(true));
        return path;
    }

    private static ImportDetails Details(string approvedBy = "Test Approver") => new(approvedBy, "test-runner", "fixture v1", "dummy test import");

    public static void Run(PreviewResult preview, EmployeeRoster roster, string root)
    {
        var dry = SkillsMatrixDryRun.Run(preview, roster, new DateTime(2026, 1, 2, 3, 4, 5), "test-runner");

        Group("Import plan: blocking until every decision is recorded");
        var noDecisions = Build(dry, null, Details());
        Check(noDecisions.Errors.Any(e => e.Contains("No decisions file")) && !noDecisions.IsReady(new HashSet<string>()), "no decisions file -> not ready");
        var untouched = Build(dry, LoadDecisionsCsv(WriteDecisions(dry, Path.Combine(root, "d_untouched.csv"), new Dictionary<string, (string, string, string)>())), Details());
        Eq(9, untouched.Errors.Count(e => e.Contains("still UNCONFIRMED")), "the dry run's CSV as saved (all UNCONFIRMED) -> one blocking error per name");
        Check(untouched.Areas.SelectMany(a => a.Columns).All(c => c.Disposition == "Excluded" && c.EmployeeId == null), "nothing is matched while unconfirmed");
        Check(Build(dry, null, Details("  ")).Errors.Any(e => e.Contains("Approved by")), "'Approved by' required");

        Group("Import plan: recorded decisions -> exact rows");
        var decisions = LoadDecisionsCsv(WriteDecisions(dry, Path.Combine(root, "d_ok.csv"), FixtureDecisions));
        var plan = Build(dry, decisions, Details());
        Check(plan.Errors.Count == 0, "no blocking errors", string.Join(" | ", plan.Errors));
        var expectedAcks = new[] { "unverified-copy", "archived:Dan Archived", "legend:Admin", "flagged:Laboratory", "flagged:Admin", "twice:Admin:1" };
        Check(plan.Acknowledgements.Select(a => a.Key).OrderBy(k => k).SequenceEqual(expectedAcks.OrderBy(k => k)),
              "required acknowledgements: unverified copy, archived employee, ambiguous key, flagged cells, same employee twice",
              string.Join(", ", plan.Acknowledgements.Select(a => a.Key)));
        Check(!plan.IsReady(new HashSet<string>(expectedAcks.Skip(1))), "not ready while any acknowledgement is missing");
        Check(plan.IsReady(new HashSet<string>(expectedAcks)), "ready once every acknowledgement is given");
        Eq(7, plan.Members, "member columns = confirmed names x occurrences");
        Eq(4, plan.ExcludedColumns, "excluded columns kept with their reason");
        Eq(14, plan.Cells, "cells loaded only for member columns");
        var lab = plan.Areas.Single(a => a.Sheet == "Laboratory");
        Check(lab.Columns.Single(c => c.SourceColumn == "G").ExcludeReason == "Excluded by reviewer: not the same person as Eve Closname",
              "exclusion reason comes from the reviewer's notes");
        Check(lab.Columns.Single(c => c.SourceColumn == "C") is { Disposition: "Member", EmployeeId: 1, SourceHeaderText: "Alice Example", HeaderSourceRef: "Laboratory!C3" },
              "member column keeps the original text and its header cell");
        Check(lab.Cells.Any(c => c.SourceCell == "Laboratory!C6" && c.Status == "Flagged" && c.RawValue == "x"), "flagged cell kept with its raw value");
        Check(!lab.Cells.Any(c => c.SourceCell.StartsWith("Laboratory!H")), "no cells for an excluded column");
        Check(lab.Formats.Any(f => f.Cell == "Laboratory!H3") && !lab.Formats.Any(f => f.Cell == "Laboratory!H5"),
              "formatting stored for the excluded column's header but not its unloaded cells");
        Check(lab.Scale.Levels.All(l => l.SourceRef.StartsWith("Laboratory!")) && lab.Scale.LegendStatus == "AsWritten", "key entries keep their source cells");
        Eq("NeedsConfirmation", plan.Areas.Single(a => a.Sheet == "Admin").Scale.LegendStatus, "ambiguous key stored flagged, not resolved");
        Check(lab.Skills.Single(k => k.SourceRef == "Laboratory!A5").InabSourceRef == "Laboratory!B5", "INAB cell reference carried");
        Check(lab.Columns.Single(c => c.SourceColumn == "F") is { EmployeeId: 4, EmployeeName: "Dan Archived", EmployeeArchived: true }
              && lab.Columns.Single(c => c.SourceColumn == "C").EmployeeArchived == false,
              "plan carries each confirmed employee's reviewed name and archived status (re-checked by the writer)");
        Eq(0, ValidateAgainstSchema(plan).Count, "plan satisfies every migration-004 rule");
        Check(plan.Import.ColourVerification.StartsWith("Not verified"), "colour verification claimed only for the verified copy");
        string summary = RenderSummary(plan, new HashSet<string>(expectedAcks));
        Check(summary.Contains("IMPORT PLAN (NOT WRITTEN)") && summary.Contains("Status: READY") && summary.Contains("SkillMatrixCells        14"),
              "summary states what would be written and that nothing was");

        Group("Import plan: side-by-side tables sharing rows (Innovation layout)");
        var inno = InnovationPlan(root, out var innoPreview);
        var innoSkills = innoPreview.Areas.Single().Data.Skills;
        Check(innoSkills.Any(s => s.SourceRef == "Innovation!A6") && innoSkills.Any(s => s.SourceRef == "Innovation!G6"),
              "both tables' skills on row 6 are read");
        Check(inno.Errors.Count == 0, "two skills on the same row are allowed (unique by source cell)", string.Join(" | ", inno.Errors));
        Eq(2, inno.Members, "one member column in each table");
        Eq(2, inno.Cells, "one cell in each table");

        Group("Import plan: decision-file errors");
        string Err(string name, Func<List<string>, List<string>> tamper, string file) =>
            string.Join(" | ", Build(dry, LoadDecisionsCsv(WriteDecisions(dry, Path.Combine(root, file), FixtureDecisions, tamper)), Details()).Errors);
        Check(Err("id", r => { if (r[1] == "Bob O'Test") r[11] = "99"; return r; }, "d1.csv").Contains("not in the employee list"), "confirmed ID not in the employee list -> error");
        Check(Err("id", r => { if (r[1] == "Bob O'Test") r[11] = "abc"; return r; }, "d2.csv").Contains("is not an employee ID"), "non-numeric ID -> error");
        Check(Err("notes", r => { if (r[1] == "Frank Nomatch") r[12] = ""; return r; }, "d3.csv").Contains("ReviewerNotes is empty"), "exclusion without a reason -> error");
        Check(Err("both", r => { if (r[1] == "Frank Nomatch") r[11] = "8"; return r; }, "d4.csv").Contains("choose one"), "excluded and an ID -> error");
        Check(Err("name", r => { if (r[1] == "Bob O'Test") r[1] = "Bob O Test"; return r; }, "d5.csv").Contains("not a name in this workbook"), "unknown / edited name -> error");
        Check(Err("where", r => { if (r[1] == "Bob O'Test") r[2] = "Laboratory!Z3"; return r; }, "d6.csv").Contains("re-run the dry run"), "changed cell reference -> error");
        Check(Err("typo", r => { if (r[1] == "Bob O'Test") r[10] = "YES"; return r; }, "d7.csv").Contains("still YES"), "anything but CONFIRMED / EXCLUDED -> error");
        var changed = SkillsMatrixDryRun.Run(preview with { Sha256After = new string('0', 64) }, roster);
        Check(Build(changed, decisions, Details()).Errors.Any(e => e.Contains("changed while it was being read")), "workbook changed during reading -> error");
        string guarded = Path.Combine(root, "d_guard.csv");
        File.WriteAllText(guarded, "OriginalName,Occurrences,HumanConfirmation,ConfirmedEmployeeID,ReviewerNotes\r\n'=Odd Name,'=S!C3,EXCLUDED,,x\r\n");
        Eq("=Odd Name", LoadDecisionsCsv(guarded).Rows[0].OriginalName, "the CSV's text-guard apostrophe is removed from names when reading back");
        Eq("=S!C3", LoadDecisionsCsv(guarded).Rows[0].Occurrences, "...and from cell references");

        Group("Import writer refuses before touching any connection");
        var acks = new HashSet<string>(expectedAcks);
        using var neverOpened = new NpgsqlConnection();   // no connection string: cannot reach any database
        string Refusal(ImportPlan p, IReadOnlySet<string> a, string typed)
        {
            try { SkillsMatrixImportWriter.Write(neverOpened, p, a, typed); return "WROTE"; }
            catch (InvalidOperationException ex) { return ex.Message; }
        }
        Check(Refusal(untouched, acks, "x").Contains("not ready"), "plan with errors refused");
        Check(Refusal(plan, new HashSet<string>(), SkillsMatrixImportWriter.ExpectedConfirmation(plan)).Contains("not ready"), "missing acknowledgements refused");
        Check(Refusal(plan, acks, "IMPORT").Contains("did not match"), "wrong confirmation text refused");
        Check(Refusal(plan, acks, SkillsMatrixImportWriter.ExpectedConfirmation(plan)).Contains("must be open"), "unopened connection refused (no connection attempted)");
        Eq("IMPORT " + preview.Sha256Before[..8], SkillsMatrixImportWriter.ExpectedConfirmation(plan), "confirmation text = IMPORT + first 8 of the file's SHA-256");

        Group("Import step in the application is gated (switched on 2026-11-11 for the approved import)");
        string? dir = Path.GetDirectoryName(ThisSourceFile());
        while (dir != null && !File.Exists(Path.Combine(dir, "EmployeeTrainingTracker.sln"))) dir = Path.GetDirectoryName(dir);
        string form = File.ReadAllText(Path.Combine(dir!, "EmployeeTrainingTracker", "Forms", "SkillsMatrixImportForm.cs"));
        Check(form.Contains("internal static readonly bool ImportExecutionEnabled = true;"), "ImportExecutionEnabled = true");
        Check(form.Contains("btnImport.Enabled = ImportExecutionEnabled && ready;") && form.Contains("if (!ImportExecutionEnabled || _plan == null || !_plan.IsReady(_acknowledged)) return;"),
              "Import button needs a READY plan and the handler re-checks the flag and readiness");
    }

    /// <summary>The dummy Innovation-layout plan (employees 1 'Alice Example', 2 'Bob O Test'), for the live-copy tests.</summary>
    internal static ImportPlan DummyInnovationPlan(string root) => InnovationPlan(root, out _);

    /// <summary>A plan for any dummy workbook, with the dummy employee list and the given decisions (merge tests).</summary>
    internal static ImportPlan PlanForWorkbook(string workbook, string root, Dictionary<string, (string Decision, string Id, string Notes)> decisions,
                                               out DryRunResult dry)
    {
        dry = SkillsMatrixDryRun.Run(SkillsMatrixWorkbookPreview.Load(workbook), Fixture.Roster());
        var path = WriteDecisions(dry, Path.Combine(root, $"d_{Guid.NewGuid():N}.csv"), decisions);
        return Build(dry, LoadDecisionsCsv(path), Details());
    }

    /// <summary>Plan for the dummy Innovation-layout workbook (Alice and Bob confirmed).</summary>
    private static ImportPlan InnovationPlan(string root, out PreviewResult preview)
    {
        preview = SkillsMatrixWorkbookPreview.Load(Fixture.BuildInnovationWorkbook(Path.Combine(root, $"dummy_innovation_{Guid.NewGuid():N}.xlsx")));
        var dry = SkillsMatrixDryRun.Run(preview, Fixture.Roster());
        var path = WriteDecisions(dry, Path.Combine(root, $"d_inno_{Guid.NewGuid():N}.csv"),
            new Dictionary<string, (string, string, string)> { ["Alice Example"] = (Confirmed, "1", ""), ["Bob O'Test"] = (Confirmed, "2", "") });
        return Build(dry, LoadDecisionsCsv(path), Details());
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Writes the dummy plan to a DISPOSABLE local database (started by the test script): first import, a failing
    /// import (unknown employee - must leave nothing), then a replacement import. Refuses any target but localhost:54329.
    /// </summary>
    public static int WriteToDisposableDatabase(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        if (csb.Host is not ("localhost" or "127.0.0.1") || csb.Port != 54329 || csb.Database != "smimport")
        {
            Console.WriteLine($"REFUSED: {csb.Host}:{csb.Port}/{csb.Database} is not the disposable test database (localhost:54329/smimport).");
            return 2;
        }
        string root = Path.Combine(Path.GetTempPath(), "SkillsMatrixImportTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var preview = SkillsMatrixWorkbookPreview.Load(Fixture.BuildWorkbook(Path.Combine(root, "dummy_matrix_fixture.xlsx")));
            var roster = new EmployeeRoster("dummy list", null, Fixture.Roster().Employees.Append(new EmployeeRecord(9, "Not In Database", false)).ToList());
            var dry = SkillsMatrixDryRun.Run(preview, roster);
            var acks = new HashSet<string> { "unverified-copy", "archived:Dan Archived", "legend:Admin", "flagged:Laboratory", "flagged:Admin", "twice:Admin:1" };

            using var conn = new NpgsqlConnection(csb.ConnectionString);
            conn.Open();
            // Second guard: the test script creates this table in the disposable database only
            using (var sentinel = new NpgsqlCommand("SELECT to_regclass('public.disposable_test_sentinel') IS NOT NULL", conn))
                if (sentinel.ExecuteScalar() is not true) { Console.WriteLine("REFUSED: no disposable-test sentinel table in this database."); return 2; }
            Console.WriteLine($"TARGET {conn.Host}:{conn.Port}/{conn.Database}");

            var plan = Build(dry, LoadDecisionsCsv(WriteDecisions(dry, Path.Combine(root, "d1.csv"), FixtureDecisions)), Details());
            var r1 = SkillsMatrixImportWriter.Write(conn, plan, acks, SkillsMatrixImportWriter.ExpectedConfirmation(plan));
            Console.WriteLine($"IMPORT1 id={r1.ImportId} columns={r1.Columns} cells={r1.Cells} formats={r1.Formats} seconds={r1.Took.TotalSeconds:0.00}");

            // The employee re-check against the target database: each case must be refused with nothing written
            void Refused(string label, EmployeeRoster list, Dictionary<string, (string, string, string)> decisions, string expect)
            {
                var d = SkillsMatrixDryRun.Run(preview, list);
                var p = Build(d, LoadDecisionsCsv(WriteDecisions(d, Path.Combine(root, $"{label}.csv"), decisions)), Details());
                var pAcks = p.Acknowledgements.Select(a => a.Key).ToHashSet();
                if (p.Errors.Count > 0) { Console.WriteLine($"{label}_PLAN_ERROR {p.Errors[0]}"); return; }
                try { SkillsMatrixImportWriter.Write(conn, p, pAcks, SkillsMatrixImportWriter.ExpectedConfirmation(p)); Console.WriteLine($"{label}_NOT_REJECTED"); }
                catch (InvalidOperationException ex) { Console.WriteLine(ex.Message.Contains(expect) ? $"{label}_REJECTED" : $"{label}_WRONG_REASON {ex.Message}"); }
            }
            Refused("MISSING", roster, new Dictionary<string, (string, string, string)>(FixtureDecisions) { ["Frank Nomatch"] = (Confirmed, "9", "") },
                    "employee 9 does not exist in this database");
            Refused("RENAMED", new EmployeeRoster("stale list", null, roster.Employees.Select(e => e.EmployeeId == 2 ? e with { FullName = "Bob Renamed" } : e).ToList()),
                    new Dictionary<string, (string, string, string)>(FixtureDecisions) { ["Bob O'Test"] = (Confirmed, "2", "") },
                    "employee 2 is 'Bob O Test' in this database but 'Bob Renamed'");
            Refused("ARCHIVED", new EmployeeRoster("stale list", null, roster.Employees.Select(e => e.EmployeeId == 4 ? e with { IsArchived = false } : e).ToList()),
                    FixtureDecisions, "employee 4 is archived in this database but not archived");

            var r2 = SkillsMatrixImportWriter.Write(conn, plan, acks, SkillsMatrixImportWriter.ExpectedConfirmation(plan));
            Console.WriteLine($"IMPORT2 id={r2.ImportId} columns={r2.Columns} cells={r2.Cells} formats={r2.Formats}");

            var inno = InnovationPlan(root, out var _);
            var innoAcks = inno.Acknowledgements.Select(a => a.Key).ToHashSet();   // only 'unverified-copy' for the dummy file
            var r3 = SkillsMatrixImportWriter.Write(conn, inno, innoAcks, SkillsMatrixImportWriter.ExpectedConfirmation(inno));
            Console.WriteLine($"IMPORT3 id={r3.ImportId} skills={r3.Skills} columns={r3.Columns} cells={r3.Cells}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR " + ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
