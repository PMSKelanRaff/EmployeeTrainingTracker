// Isolated tests for the Skills Matrix workbook reader, dry run and employee reconciliation.
// Everything uses DUMMY data generated here (a fixture workbook and dummy employee lists in a temp folder).
// No database is used: none of the code under test opens a connection.
using EmployeeTrainingTracker.Utilities;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Security.Cryptography;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixDryRun;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixReconciliation;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixWorkbookPreview;

internal static class Program
{
    private static int _passed, _failed;
    private static string _group = "";

    private static int Main(string[] args)
    {
        // --import-test <connection string>: write a plan to a DISPOSABLE local test database (refuses anything else)
        if (args.Length == 2 && args[0] == "--import-test") return ImportTests.WriteToDisposableDatabase(args[1]);
        // --live-test <connection string>: live-copy read/save tests on a DISPOSABLE local test database (refuses anything else)
        if (args.Length == 2 && args[0] == "--live-test") return LiveTests.Run(args[1]);

        // --sample <folder>: write the dummy fixture's dry-run report files there (for reviewing the format)
        if (args.Length == 2 && args[0] == "--sample")
        {
            Directory.CreateDirectory(args[1]);
            string wb = Fixture.BuildWorkbook(Path.Combine(args[1], "dummy_matrix_fixture.xlsx"));
            var result = SkillsMatrixDryRun.Run(SkillsMatrixWorkbookPreview.Load(wb), Fixture.Roster(),
                                                new DateTime(2026, 1, 2, 3, 4, 5), "test-runner");
            foreach (var f in WriteFiles(result, args[1])) Console.WriteLine(f);
            return 0;
        }

        string root = Path.Combine(Path.GetTempPath(), "SkillsMatrixTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string workbook = Fixture.BuildWorkbook(Path.Combine(root, "dummy_matrix_fixture.xlsx"));
            File.SetAttributes(workbook, FileAttributes.ReadOnly);   // reading must work on a read-only file
            byte[] originalBytes = File.ReadAllBytes(workbook);
            DateTime originalWrite = File.GetLastWriteTimeUtc(workbook);

            var preview = SkillsMatrixWorkbookPreview.Load(workbook);
            var roster = Fixture.Roster();

            ParserTests(preview);
            LevelAndBlankTests(preview);
            FormulaAndAnomalyTests(preview);
            LegendTests(preview);
            FormattingTests(preview);
            ReconciliationTests(preview, roster);
            ReconciliationRuleTests();
            RosterCsvTests(root);
            DryRunAndReportTests(preview, roster, root);
            IntegrityTests(workbook, originalBytes, originalWrite, preview);
            NoDatabaseTests();
            ImportTests.Run(preview, roster, root);
        }
        catch (Exception ex)
        {
            Fail("unexpected exception", ex.ToString());
        }
        finally
        {
            try
            {
                foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(root, recursive: true);
            }
            catch { /* temp folder; best effort */ }
        }

        Console.WriteLine();
        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------------------------------

    internal static void Group(string name) { _group = name; Console.WriteLine($"\n== {name} =="); }
    internal static void Pass(string name) { _passed++; Console.WriteLine($"[PASS] {name}"); }
    internal static void Fail(string name, string detail) { _failed++; Console.WriteLine($"[FAIL] {_group}: {name} -> {detail}"); }
    internal static void Check(bool ok, string name, string detail = "") { if (ok) Pass(name); else Fail(name, detail); }
    internal static void Eq<T>(T expected, T actual, string name) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), name, $"expected {expected}, actual {actual}");

    internal static string ThisSourceFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    private static PreviewArea Sheet(PreviewResult p, string name) => p.Areas.Single(a => a.Area.SourceSheet == name);
    private static SkillsMatrixService.SkillCellInfo? CellAt(PreviewArea a, string cell) =>
        a.Data.Cells.Values.SingleOrDefault(c => c.SourceCell == $"{a.Area.SourceSheet}!{cell}");

    // ---------------------------------------------------------------------------------------------

    private static void ParserTests(PreviewResult p)
    {
        Group("Parser: sheets, people, skills, scope");
        Eq(2, p.Areas.Count, "only the two recognised visible matrix sheets are processed");
        Check(p.ExcludedSheets.Any(s => s.StartsWith("Sheet1 - hidden")), "hidden sheet excluded with reason");
        Check(p.ExcludedSheets.Any(s => s.StartsWith("Skill Matrix required - template")), "template sheet excluded with reason");
        Check(p.ExcludedSheets.Any(s => s.StartsWith("Notes - visible but not a recognised")), "unrecognised visible sheet excluded with reason");

        var lab = Sheet(p, "Laboratory");
        var names = lab.Data.Members.Select(m => m.SourceHeaderText).ToList();
        Check(names.SequenceEqual(new[] { "Alice Example", "Bob O'Test", "Student Carol Sample", "Dan Archived", "Eve Closename",
                                          "Frank Nomatch", "(unnamed column I)", "Gina Twice" }),
              "people columns read with names exactly as written", string.Join(" | ", names));
        Check(lab.Scope.SkippedColumns.SequenceEqual(new[] { "Laboratory!J3 - no name in the header row and no values" }),
              "empty unnamed column excluded with reason", string.Join(" | ", lab.Scope.SkippedColumns));
        Eq("Team A", lab.Data.Members[1].SubGroupLabel, "team heading from a merged cell kept (D under C2:D2)");
        Eq(4, lab.Data.Skills.Count, "skill rows (incl. hidden and out-of-range rows)");
        Eq("Laboratory Tests to EN", lab.Data.Categories.Single().Name, "category heading row");
        Eq(1, lab.Scope.BlankRowsSkipped, "blank row skipped and counted");
        Check(lab.Scope.ValidationRanges.SequenceEqual(new[] { "Laboratory!C5:I7" }), "source range = the sheet's data-validation range",
              string.Join(",", lab.Scope.ValidationRanges));
        Eq("Laboratory!A1:K1", lab.Scope.LegendRange, "legend range recorded");
        var alpha = lab.Data.Skills.First(s => s.Name == "Skill alpha");
        Eq("Laboratory!A5", alpha.SourceRef, "skill keeps its source cell reference");
        Eq(true, alpha.InabAccredited, "INAB 'Yes' read");
        Eq("Yes", alpha.InabRaw, "INAB raw text kept");
        Eq(false, lab.Data.Skills.First(s => s.Name == "Skill beta").InabAccredited, "INAB 'No' read");
    }

    private static void LevelAndBlankTests(PreviewResult p)
    {
        Group("Level handling and blank cells");
        var lab = Sheet(p, "Laboratory");
        var c5 = CellAt(lab, "C5");
        Check(c5 is { Status: "Level", Level: 5 }, "whole number 5 in range -> Level 5");
        var d5 = CellAt(lab, "D5");
        Check(d5 is { Status: "Unassessed", Level: null, RawValue: null }, "blank cell in range -> Unassessed (not 1, not 0)", $"{d5}");
        Check(lab.Data.Cells.Values.Where(c => c.Status == "Unassessed").All(c => c.Level == null), "no Unassessed cell carries a level");
        Check(!lab.Data.Cells.Values.Any(c => c.Level == 0), "no cell is ever level 0");
        Check(CellAt(lab, "K6") == null, "blank cell outside the range -> no record");
        Eq(8, lab.Data.Cells.Values.Count(c => c.Status == "Level"), "assessed cells (Laboratory)");
        Eq(9, lab.Data.Cells.Values.Count(c => c.Status == "Unassessed"), "unassessed cells (Laboratory)");
        Eq(6, lab.Data.Cells.Values.Count(c => c.Status == "Flagged"), "flagged cells (Laboratory)");
        Eq(9, lab.Scope.OutOfRangeBlankCells, "blank cells outside the range counted");
    }

    private static void FormulaAndAnomalyTests(PreviewResult p)
    {
        Group("Formulas and anomalies are flagged, never converted");
        var lab = Sheet(p, "Laboratory");
        var h5 = CellAt(lab, "H5");
        Check(h5 is { Status: "Flagged", Level: null, RawValue: "=C5/1" }, "formula cell flagged with its formula, no level", $"{h5}");
        Check(CellAt(lab, "C6") is { Status: "Flagged", RawValue: "x" }, "text value flagged as written");
        Check(CellAt(lab, "D6") is { Status: "Flagged", Level: null } d6 && d6.RawValue == "2.5", "2.5 flagged, not rounded");
        Check(CellAt(lab, "E6") is { Status: "Flagged", Level: null } e6 && e6.RawValue == "7", "7 flagged, not clamped");
        Check(CellAt(lab, "K5") is { Status: "Flagged" } k5 && k5.FlagReason!.StartsWith("Value outside"), "value outside the range flagged");
        Check(CellAt(lab, "C9") is { Status: "Flagged" }, "value on a row below the range flagged");

        int Kind(string k) => lab.ReviewItems.Count(i => i.Kind == k);
        Eq(1, Kind(ReviewKind.Formula), "review item: formula");
        Eq(2, Kind(ReviewKind.OutOfRange), "review items: out-of-range values");
        Eq(3, Kind(ReviewKind.UnexpectedValue), "review items: unexpected values");
        Eq(1, Kind(ReviewKind.HiddenRow), "review item: hidden row");
        Eq(1, Kind(ReviewKind.DuplicateSkill), "review item: duplicate skill name");
        Eq(1, Kind(ReviewKind.UnexpectedInab), "review item: unexpected INAB value 'Maybe'");
        Eq(1, Kind(ReviewKind.UnnamedColumn), "review item: column with values but no name");
        Check(lab.ReviewItems.All(i => i.Cell == null || i.Cell.StartsWith("Laboratory!")), "every review item carries its sheet cell reference");
        Check(lab.Data.Skills.Single(s => s.SourceRef == "Laboratory!A7").HiddenInSource, "hidden row kept and marked hidden");
        Eq("Maybe", lab.Data.Skills.Single(s => s.SourceRef == "Laboratory!A7").InabRaw, "unexpected INAB text kept as written");
        Eq(null, lab.Data.Skills.Single(s => s.SourceRef == "Laboratory!A7").InabAccredited, "unexpected INAB text not converted");
    }

    private static void LegendTests(PreviewResult p)
    {
        Group("Level definitions (ambiguous scales)");
        var lab = Sheet(p, "Laboratory");
        var admin = Sheet(p, "Admin");
        Check(!lab.Data.Legend.NeedsConfirmation && lab.Data.Legend.Entries.Count == 5, "complete 1-5 key read as written");
        Eq("3: Requires Supervision", lab.Data.Legend.Entries.Single(e => e.Level == 3).Label, "level label kept exactly");
        Check(admin.Data.Legend.NeedsConfirmation, "key listing only 1, 4, 5 needs confirmation");
        Check(admin.Data.Legend.LegendNote?.Contains("does not list level(s) 2, 3") == true, "missing levels named", admin.Data.Legend.LegendNote ?? "");
        Check(admin.ReviewItems.Any(i => i.Kind == ReviewKind.AmbiguousLegend), "ambiguous level definition is a review item");
        Check(CellAt(admin, "C6") is { Status: "Flagged" } c6 && c6.RawValue == "=B6/1", "Admin formula cell flagged");
    }

    private static void FormattingTests(PreviewResult p)
    {
        Group("Formatting provenance");
        var lab = Sheet(p, "Laboratory");
        var c5 = lab.Colours.Display["Laboratory!C5"];
        Eq("ConditionalFormat", c5.FillSource, "C5 fill comes from a conditional-formatting rule");
        Check(c5.ConditionalRule?.Contains("containsText \"5\" on C5:I7") == true, "winning rule recorded", c5.ConditionalRule ?? "(none)");
        Eq(Color.FromArgb(0xA9, 0xD0, 0x8E).ToArgb(), c5.Fill?.ToArgb(), "C5 displayed fill = the rule's fill");
        var a4 = lab.Colours.Display["Laboratory!A4"];
        Check(a4 is { FillSource: "CellStyle", Pattern: "solid", ConditionalRule: null }, "category cell: fixed solid fill", $"{a4}");
        var b6 = lab.Colours.Display["Laboratory!B6"];
        Check(b6 is { FontSource: "ConditionalFormat", FillSource: "None" } && b6.FontRule!.Contains("containsText \"No\""),
              "INAB 'No' font colour comes from its rule", $"{b6}");
        Eq(Color.FromArgb(0xC0, 0x00, 0x00).ToArgb(), b6.Font.ToArgb(), "INAB 'No' displayed font colour");
        var e3 = lab.Colours.Display["Laboratory!E3"];
        Check(e3 is { FillSource: "CellStyle", Pattern: "mediumGray" } && e3.PatternBack != null, "patterned fill keeps its OOXML pattern and colours", $"{e3}");
    }

    private static void ReconciliationTests(PreviewResult p, EmployeeRoster roster)
    {
        Group("Employee reconciliation (fixture workbook + dummy employee list)");
        var r = SkillsMatrixDryRun.Run(p, roster, new DateTime(2026, 1, 2, 3, 4, 5), "test-runner");
        ReconciliationRow Row(string name) => r.Reconciliation.Single(x => x.OriginalName == name);

        Eq(9, r.Reconciliation.Count, "one row per distinct name as written");
        Check(r.Reconciliation.All(x => x.Confirmation == Unconfirmed), "every row starts UNCONFIRMED");

        var alice = Row("Alice Example");
        Check(alice is { Status: MatchStatus.ExactCandidate, ProposedEmployeeId: 1, Confidence: "High" }, "identical text -> exact candidate", $"{alice.Status} {alice.ProposedEmployeeId}");
        Eq(2, alice.Occurrences.Count, "same text on two sheets -> one row, two occurrences");
        Check(Row("alice  EXAMPLE") is { Status: MatchStatus.ExactCandidate, ProposedEmployeeId: 1 } a2 && a2.Reason.Contains("differ only"),
              "case/spacing difference -> exact candidate, explained");
        Check(Row("Bob O'Test") is { Status: MatchStatus.ExactCandidate, ProposedEmployeeId: 2 }, "apostrophe difference -> exact candidate");
        var carol = Row("Student Carol Sample");
        Check(carol is { Status: MatchStatus.StudentOrNonEmployee, ProposedEmployeeId: null } && carol.Reason.Contains("ID 3"),
              "student -> no match proposed; the similar record is only mentioned", carol.Reason);
        Check(Row("Dan Archived") is { Status: MatchStatus.ArchivedEmployee, ProposedEmployeeId: 4, ProposedEmployeeArchived: true },
              "match to an archived record -> archived employee status");
        var eve = Row("Eve Closename");
        Check(eve is { Status: MatchStatus.SuggestedMatch, ProposedEmployeeId: 5, Confidence: "Medium" } && eve.Reason.Contains("not proof"),
              "close spelling -> suggestion only, with warning", $"{eve.Status} {eve.Reason}");
        var frank = Row("Frank Nomatch");
        Check(frank is { Status: MatchStatus.Unmatched, ProposedEmployeeId: null } && frank.Reason.Contains("Not proposed") && frank.Reason.Contains("ID 8"),
              "same surname only -> unmatched, record mentioned as NOT proposed", frank.Reason);
        Check(Row("(unnamed column I)") is { Status: MatchStatus.Unmatched }, "unnamed column -> unmatched");
        var gina = Row("Gina Twice");
        Check(gina is { Status: MatchStatus.SuggestedMatch, ProposedEmployeeId: null } && gina.Reason.Contains("ID 6") && gina.Reason.Contains("ID 7"),
              "two equally good records -> ambiguous, none proposed", gina.Reason);

        Eq(3, r.Count(MatchStatus.ExactCandidate), "count: exact candidates");
        Eq(2, r.Count(MatchStatus.SuggestedMatch), "count: suggested");
        Eq(2, r.Count(MatchStatus.Unmatched), "count: unmatched");
        Eq(1, r.Count(MatchStatus.StudentOrNonEmployee), "count: student / non-employee");
        Eq(1, r.Count(MatchStatus.ArchivedEmployee), "count: archived");

        var none = SkillsMatrixDryRun.Run(p, null);
        Check(none.Reconciliation.All(x => x.ProposedEmployeeId == null && x.Status is MatchStatus.Unmatched or MatchStatus.StudentOrNonEmployee),
              "no employee list -> nothing proposed");
        Check(none.Reconciliation.Where(x => !x.OriginalName.StartsWith("(unnamed")).All(x => x.Reason.Contains("No employee list")),
              "no employee list -> the reason says so");
    }

    private static void ReconciliationRuleTests()
    {
        Group("Reconciliation rules (unit)");
        EmployeeRoster R(params (long Id, string Name, bool? Archived)[] e) =>
            new("unit-test list", null, e.Select(x => new EmployeeRecord(x.Id, x.Name, x.Archived)).ToList());
        ReconciliationRow One(string name, EmployeeRoster roster) =>
            Reconcile(new[] { (name, new Occurrence("S", "C", "S!C3")) }, roster).Single();

        var brian = One("Brian Feighan", R((1, "Kieran Feighan", false)));
        Check(brian is { Status: MatchStatus.Unmatched, ProposedEmployeeId: null }, "different first name, same surname -> NOT proposed", brian.Reason);
        var mark = One("Mark Byrne", R((1, "Mary Byrne", false)));
        Check(mark is { Status: MatchStatus.SuggestedMatch } && mark.Reason.Contains("first names differ"),
              "one-letter first-name difference -> suggestion with a 'may be a different person' warning", mark.Reason);
        Check(One("Seán Óg Test", R((1, "Sean Og Test", false))) is { Status: MatchStatus.ExactCandidate }, "accents ignored for the candidate only");
        Eq("Seán Óg Test", One("Seán Óg Test", R((1, "Sean Og Test", false))).OriginalName, "original text kept unchanged");
        Check(One("Pat Example", R((1, "Pat Example", null))) is { Status: MatchStatus.ExactCandidate } u && u.Reason.Contains("not known"),
              "unknown archived status is stated");
        Check(One("Dermot Leonard Lab Manager", R((1, "Dermot Leonard", false))) is { Status: MatchStatus.SuggestedMatch, Confidence: "Low" },
              "header containing a full name plus other words -> low-confidence suggestion");
        Check(One("Zed", R((1, "Zed Other", false))) is { Status: MatchStatus.Unmatched }, "single word is never matched by containment");
        Eq(0, Levenshtein("abc", "abc"), "edit distance 0");
        Eq(1, Levenshtein("closename", "closname"), "edit distance 1");
        Eq("james ogrady", Normalise("  James  O'Grady "), "normalisation");
    }

    private static void RosterCsvTests(string root)
    {
        Group("Employee list CSV (dummy)");
        string ok = Path.Combine(root, "roster_ok.csv");
        File.WriteAllText(ok, "EmployeeID,FullName,IsArchived\r\n1,\"Example, Alice\",false\r\n2,\"Bob \"\"B\"\" Test\",yes\r\n3,Cara Test,\r\n");
        var r = LoadRosterCsv(ok);
        Eq(3, r.Employees.Count, "rows read");
        Eq("Example, Alice", r.Employees[0].FullName, "quoted comma kept");
        Eq("Bob \"B\" Test", r.Employees[1].FullName, "doubled quotes kept");
        Eq(true, r.Employees[1].IsArchived, "archived 'yes'");
        Eq(null, r.Employees[2].IsArchived, "blank archived = not known");
        Check(r.Sha256?.Length == 64, "list's SHA-256 recorded");

        void Bad(string content, string expect, string name)
        {
            string path = Path.Combine(root, $"roster_bad_{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, content);
            try { LoadRosterCsv(path); Fail(name, "accepted"); }
            catch (InvalidDataException ex) { Check(ex.Message.Contains(expect), name, ex.Message); }
        }
        Bad("Name\r\nx\r\n", "EmployeeID and FullName", "missing columns rejected");
        Bad("EmployeeID,FullName\r\n1,A\r\n1,B\r\n", "more than once", "duplicate ID rejected");
        Bad("EmployeeID,FullName\r\nx,A\r\n", "not a positive whole number", "bad ID rejected");
        Bad("EmployeeID,FullName,IsArchived\r\n1,A,maybe\r\n", "not true/false", "bad archived value rejected");
    }

    private static void DryRunAndReportTests(PreviewResult p, EmployeeRoster roster, string root)
    {
        Group("Dry run: proposed records and report files");
        var r = SkillsMatrixDryRun.Run(p, roster, new DateTime(2026, 1, 2, 3, 4, 5), "test-runner");
        var c = r.Proposed;
        Check(c is { Imports: 1, Scales: 2, Areas: 2, ScaleLevels: 8, Categories: 2, Skills: 5, Columns: 11 }, "proposed record counts", $"{c}");
        Check(c is { MemberColumns: 0, ExcludedColumns: 11, Cells: 0, CellsHeldBack: 26 }, "nothing confirmed: no member columns, all cells held back", $"{c}");
        Eq(r.Formats.Count, c.CellFormats, "formatting rows counted");
        Check(r.Formats.All(f => f.FillSource != "ConditionalFormat" || (f.Pattern == "solid" && f.ConditionalRule != null && f.PatternColorRgb == null)),
              "conditional fills fit the formats table rules (solid, rule recorded)");
        Check(r.Formats.All(f => f.FillSource != "None" || (f.FillRgb == null && f.Pattern == null)), "no-fill rows have no colour");
        Check(r.Formats.All(f => (f.FontSource == "ConditionalFormat") == (f.FontRule != null)), "font rule recorded exactly when a rule set the font");

        string report = RenderReport(r);
        Check(report.StartsWith("SKILLS MATRIX - DRY RUN REPORT"), "report title");
        Check(report.Contains(Banner) && report.TrimEnd().EndsWith(Banner), "dry-run banner at the top and bottom");
        Check(!report.Contains("import completed", StringComparison.OrdinalIgnoreCase) && !report.Contains("successfully imported", StringComparison.OrdinalIgnoreCase),
              "report never describes a completed import");
        Check(report.Contains("unchanged during reading"), "report states the workbook was unchanged during reading");
        Check(report.Contains("DOES NOT MATCH"), "report flags that the fixture is not the verified copy");
        Check(report.Contains("Laboratory!C5:I7") && report.Contains("Laboratory!J3 - no name"), "report lists ranges and excluded columns");
        Check(report.Contains("NEEDS CONFIRMATION"), "report lists the ambiguous key");

        string recon = ReconciliationCsv(r);
        var rows = ParseCsv(recon);
        Check(rows[0].SequenceEqual(new[] { "DryRun", "OriginalName", "Occurrences", "Status", "MatchType", "Confidence", "ProposedEmployeeID",
                "ProposedEmployeeName", "ProposedEmployeeArchived", "Reason", "HumanConfirmation", "ConfirmedEmployeeID", "ReviewerNotes" }),
              "reconciliation CSV header");
        Check(rows.Skip(1).All(x => x[0] == "DRY RUN" && x[10] == Unconfirmed && x[11] == "" && x[12] == ""),
              "every CSV row: DRY RUN, UNCONFIRMED, confirmation fields empty");
        Check(rows.Skip(1).Any(x => x[1] == "Bob O'Test"), "original name written exactly");

        Eq("'=1+1,\"a,b\",'-1,'@x,plain", CsvLine("=1+1", "a,b", "-1", "@x", "plain"),
           "CSV: formula-like values prefixed with ', commas quoted");
        Eq("\"say \"\"hi\"\"\"", CsvLine("say \"hi\""), "CSV: quotes doubled");
        string review = ReviewItemsCsv(r);
        Check(ParseCsv(review).Skip(1).Any(x => x[2] == "Laboratory!H5" && x[3] == ReviewKind.Formula && x[4].Contains("=C5/1")),
              "review-items CSV lists the formula cell with its formula");

        string outDir = Path.Combine(root, "out");
        Directory.CreateDirectory(outDir);
        var written = WriteFiles(r, outDir);
        Eq(4, written.Count, "four files written");
        Check(written.All(f => Path.GetFileName(f).StartsWith("SkillsMatrix_DRYRUN_20260102_030405_")), "file names say DRYRUN and carry the timestamp");
        var before = written.ToDictionary(f => f, File.ReadAllBytes);
        try { WriteFiles(r, outDir); Fail("existing files are never overwritten", "second write succeeded"); }
        catch (IOException) { Check(written.All(f => File.ReadAllBytes(f).SequenceEqual(before[f])), "existing files are never overwritten"); }
    }

    private static void IntegrityTests(string workbook, byte[] originalBytes, DateTime originalWrite, PreviewResult p)
    {
        Group("Workbook integrity");
        Check(File.ReadAllBytes(workbook).SequenceEqual(originalBytes), "workbook bytes unchanged after reading, dry run and report");
        Eq(originalWrite, File.GetLastWriteTimeUtc(workbook), "workbook last-write time unchanged");
        Eq(Convert.ToHexString(SHA256.HashData(originalBytes)), p.Sha256Before, "SHA-256 before = file hash");
        Eq(p.Sha256Before, p.Sha256After, "SHA-256 after reading = before");
        Check(!p.MatchesVerifiedCopy, "dummy fixture is (correctly) not reported as the verified copy");
        Check(SkillsMatrixWorkbookPreview.RejectReason("https://example.invalid/sites/x/matrix.xlsx") != null, "web / SharePoint address refused");
        Check(SkillsMatrixWorkbookPreview.RejectReason(Path.Combine(Path.GetTempPath(), "matrix.xls")) != null, "non-.xlsx file refused");
        Check(SkillsMatrixWorkbookPreview.RejectReason(workbook) == null, "local .xlsx copy accepted");
        if (Environment.GetEnvironmentVariable("OneDrive") is string od && od.Length > 0)
            Check(SkillsMatrixWorkbookPreview.RejectReason(Path.Combine(od, "matrix.xlsx")) != null, "file inside the OneDrive folder refused");
    }

    private static void NoDatabaseTests()
    {
        Group("No database access in the dry-run code (source check)");
        string? dir = Path.GetDirectoryName(ThisSourceFile());
        while (dir != null && !File.Exists(Path.Combine(dir, "EmployeeTrainingTracker.sln"))) dir = Path.GetDirectoryName(dir);
        if (dir == null) { Fail("repository found", "EmployeeTrainingTracker.sln not found above this test's source file"); return; }
        string app = Path.Combine(dir, "EmployeeTrainingTracker");
        foreach (var f in new[] { @"Utilities\SkillsMatrixDryRun.cs", @"Utilities\SkillsMatrixReconciliation.cs",
                                  @"Utilities\SkillsMatrixWorkbookPreview.cs", @"Utilities\SkillsMatrixWorkbookColours.cs",
                                  @"Forms\SkillsMatrixDryRunForm.cs", @"Utilities\SkillsMatrixImportPlan.cs" })
        {
            string src = File.ReadAllText(Path.Combine(app, f));
            Check(!src.Contains("Npgsql") && !src.Contains("DatabaseHelper") && !src.Contains("SkillsMatrixService.Get") && !src.Contains("EnsureAdmin("),
                  $"{Path.GetFileName(f)}: no database access");
        }
        string view = File.ReadAllText(Path.Combine(app, @"Forms\SkillsMatrixView.cs"));
        int start = view.IndexOf("private void RunDryRun()", StringComparison.Ordinal);
        int end = view.IndexOf("\n        private ", start + 10, StringComparison.Ordinal);
        string body = start >= 0 && end > start ? view[start..end] : "";
        Check(body.Length > 0 && !body.Contains("SkillsMatrixService") && !body.Contains("DatabaseHelper"),
              "the dry-run button handler makes no database call");
    }
}

/// <summary>Builds the DUMMY fixture workbook (same layout as two of the real sheets; made-up names and values).</summary>
internal static class Fixture
{
    public static string BuildWorkbook(string path)
    {
        ExcelPackage.License.SetNonCommercialOrganization("Student");
        using var p = new ExcelPackage();

        // --- Laboratory layout: key A1:K1, teams row 2, names row 3, data from row 4 -------------------
        var lab = p.Workbook.Worksheets.Add("Laboratory");
        string[] key = { "1: To be Trained", "2: In Training", "3: Requires Supervision", "4: Skilled in Area", "5: Skilled to train others" };
        for (int i = 0; i < key.Length; i++) lab.Cells[1, i + 1].Value = key[i];
        lab.Cells["C2"].Value = "Team A"; lab.Cells["C2:D2"].Merge = true; lab.Cells["E2"].Value = "Team B";
        lab.Cells["A3"].Value = "Skills Record"; lab.Cells["B3"].Value = "INAB accredited";
        string?[] people = { "Alice Example", "Bob O'Test", "Student Carol Sample", "Dan Archived", "Eve Closename", "Frank Nomatch", null, null, "Gina Twice" };
        for (int i = 0; i < people.Length; i++) if (people[i] != null) lab.Cells[3, 3 + i].Value = people[i];   // C3..K3 (I3, J3 empty)
        lab.Cells["E3"].Style.Fill.PatternType = ExcelFillStyle.MediumGray;                                    // patterned header fill
        lab.Cells["E3"].Style.Fill.PatternColor.SetColor(Color.White);
        lab.Cells["E3"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xBF, 0xBF, 0xBF));

        lab.Cells["A4"].Value = "Laboratory Tests to EN";                                                    // category heading
        lab.Cells["A4"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        lab.Cells["A4"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xD9, 0xD9, 0xD9));

        Row(lab, 5, "Skill alpha", "Yes", ("C", 5), ("E", 3), ("F", 1), ("G", 2), ("I", 4), ("K", 2));     // D5 blank; K5 outside range
        lab.Cells["H5"].Formula = "C5/1";                                                                    // formula in a level cell
        Row(lab, 6, "Skill beta", "No", ("C", "x"), ("D", 2.5), ("E", 7), ("F", 4), ("H", 1));
        Row(lab, 7, "Skill alpha", "Maybe", ("C", 4));                                                       // duplicate name, odd INAB
        lab.Row(7).Hidden = true;
        Row(lab, 9, "Skill gamma", "Yes", ("C", 1));                                                         // row 8 blank; row 9 outside range

        var dv = lab.DataValidations.AddListValidation("C5:I7");
        foreach (var v in new[] { "1", "2", "3", "4", "5" }) dv.Formula.Values.Add(v);
        var five = lab.ConditionalFormatting.AddContainsText(lab.Cells["C5:I7"]);
        five.Text = "5";
        five.Style.Fill.BackgroundColor.Color = Color.FromArgb(0xA9, 0xD0, 0x8E);
        var no = lab.ConditionalFormatting.AddContainsText(lab.Cells["B5:B7"]);
        no.Text = "No";
        no.Style.Font.Color.Color = Color.FromArgb(0xC0, 0x00, 0x00);

        // --- Admin layout: key A3:H3 (levels 1, 4, 5 only), names row 4, data from row 5 -------------
        var admin = p.Workbook.Worksheets.Add("Admin");
        admin.Cells["A3"].Value = "1: To be Trained"; admin.Cells["B3"].Value = "4: Skilled in Area"; admin.Cells["C3"].Value = "5: Skilled to train others";
        admin.Cells["A4"].Value = "Skills"; admin.Cells["B4"].Value = "Alice Example"; admin.Cells["C4"].Value = "alice  EXAMPLE"; admin.Cells["D4"].Value = "Gina Twice";
        admin.Cells["A5"].Value = "Admin tasks";
        admin.Cells["A5"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        admin.Cells["A5"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xD9, 0xD9, 0xD9));
        admin.Cells["A6"].Value = "Filing"; admin.Cells["B6"].Value = 4; admin.Cells["C6"].Formula = "B6/1";
        var adv = admin.DataValidations.AddListValidation("B6:H6");
        foreach (var v in new[] { "1", "2", "3", "4", "5" }) adv.Formula.Values.Add(v);

        // --- Sheets that must be excluded ------------------------------------------------------------
        p.Workbook.Worksheets.Add("Sheet1").Hidden = eWorkSheetHidden.Hidden;
        p.Workbook.Worksheets.Add("Skill Matrix required").Cells["A1"].Value = "Team Member A";
        p.Workbook.Worksheets.Add("Notes").Cells["A1"].Value = "free text";

        p.SaveAs(new FileInfo(path));
        return path;
    }

    /// <summary>DUMMY workbook with an Innovation-layout sheet: two side-by-side tables whose skills share rows.</summary>
    public static string BuildInnovationWorkbook(string path)
    {
        ExcelPackage.License.SetNonCommercialOrganization("Student");
        using var p = new ExcelPackage();
        var ws = p.Workbook.Worksheets.Add("Innovation");
        string[] key = { "1: To be Trained", "2: In Training", "3: Requires Supervision", "4: Skilled in Area", "5: Skilled to train others" };
        for (int i = 0; i < key.Length; i++) ws.Cells[1, i + 1].Value = key[i];
        ws.Cells["A4"].Value = "Skill"; ws.Cells["B4"].Value = "INAB accredited"; ws.Cells["C4"].Value = "Alice Example";   // table 1: A-E
        ws.Cells["G4"].Value = "Skill"; ws.Cells["H4"].Value = "INAB accredited"; ws.Cells["I4"].Value = "Bob O'Test";      // table 2: G-L
        foreach (var c in new[] { "A5", "G5" })
        {
            ws.Cells[c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[c].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xD9, 0xD9, 0xD9));
        }
        ws.Cells["A5"].Value = "Innovation table 1"; ws.Cells["G5"].Value = "Innovation table 2";
        ws.Cells["A6"].Value = "Shared-row skill one"; ws.Cells["B6"].Value = "Yes"; ws.Cells["C6"].Value = 3;   // same row 6 ...
        ws.Cells["G6"].Value = "Shared-row skill two"; ws.Cells["H6"].Value = "No"; ws.Cells["I6"].Value = 4;    // ... in both tables
        foreach (var range in new[] { "C6:E6", "I6:L6" })
        {
            var dv = ws.DataValidations.AddListValidation(range);
            foreach (var v in new[] { "1", "2", "3", "4", "5" }) dv.Formula.Values.Add(v);
        }
        p.SaveAs(new FileInfo(path));
        return path;
    }

    private static void Row(ExcelWorksheet ws, int row, string skill, string inab, params (string Col, object Value)[] values)
    {
        ws.Cells[row, 1].Value = skill;
        ws.Cells[row, 2].Value = inab;
        foreach (var (col, value) in values) ws.Cells[$"{col}{row}"].Value = value;
    }

    /// <summary>DUMMY employee list (not real people).</summary>
    public static EmployeeRoster Roster() => new("dummy fixture list", null, new List<EmployeeRecord>
    {
        new(1, "Alice Example", false),
        new(2, "Bob O Test", false),
        new(3, "Carol Sample", false),
        new(4, "Dan Archived", true),
        new(5, "Eve Closname", false),
        new(6, "Gina Twice", false),
        new(7, "Gina Twice", false),
        new(8, "Ivan Nomatch", false),
    });
}
