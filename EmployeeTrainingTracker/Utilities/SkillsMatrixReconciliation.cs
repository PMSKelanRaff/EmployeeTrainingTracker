using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// DRY RUN ONLY - proposes how each person name written in the Skills Matrix workbook might relate to an
    /// employee record, for a human to confirm. Nothing is ever confirmed or matched automatically:
    /// every row starts UNCONFIRMED, and similar names are only ever suggestions with the reason explained.
    ///
    /// Employee records come only from a list supplied by the caller (e.g. a local test CSV); this class
    /// never reads or writes the database.
    /// </summary>
    public static class SkillsMatrixReconciliation
    {
        public const string Unconfirmed = "UNCONFIRMED";

        public sealed record EmployeeRecord(long EmployeeId, string FullName, bool? IsArchived);

        /// <summary>A local employee list used for a dry run, and where it came from.</summary>
        public sealed record EmployeeRoster(string Source, string? Sha256, IReadOnlyList<EmployeeRecord> Employees);

        public enum MatchStatus { ExactCandidate, SuggestedMatch, Unmatched, StudentOrNonEmployee, ArchivedEmployee }

        /// <summary>Where a name appears: sheet, column letter and the header cell holding the name.</summary>
        public sealed record Occurrence(string Sheet, string Column, string HeaderCell);

        public sealed record ReconciliationRow(
            string OriginalName,
            IReadOnlyList<Occurrence> Occurrences,
            MatchStatus Status,
            string MatchType,
            string Confidence,
            long? ProposedEmployeeId,
            string? ProposedEmployeeName,
            bool? ProposedEmployeeArchived,
            string Reason,
            string Confirmation);

        public static string StatusText(MatchStatus s) => s switch
        {
            MatchStatus.ExactCandidate => "Exact candidate",
            MatchStatus.SuggestedMatch => "Suggested match - requires confirmation",
            MatchStatus.Unmatched => "Unmatched",
            MatchStatus.StudentOrNonEmployee => "Student / non-employee",
            MatchStatus.ArchivedEmployee => "Archived employee",
            _ => s.ToString(),
        };

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// One row per distinct name exactly as written (the same text on several sheets is one row with
        /// several occurrences). With no roster, every name is reported unmatched (or student).
        /// </summary>
        public static List<ReconciliationRow> Reconcile(IEnumerable<(string Name, Occurrence Where)> names, EmployeeRoster? roster)
        {
            var rows = new List<ReconciliationRow>();
            foreach (var group in names.GroupBy(n => n.Name, StringComparer.Ordinal))
                rows.Add(ReconcileOne(group.Key, group.Select(g => g.Where).ToList(), roster?.Employees));

            return rows.OrderBy(r => r.Status).ThenBy(r => r.OriginalName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static ReconciliationRow ReconcileOne(string original, List<Occurrence> where, IReadOnlyList<EmployeeRecord>? employees)
        {
            ReconciliationRow Row(MatchStatus status, string type, string confidence, EmployeeRecord? e, string reason) =>
                new(original, where, status, type, confidence, e?.EmployeeId, e?.FullName, e?.IsArchived, reason, Unconfirmed);

            if (original.StartsWith("(unnamed column", StringComparison.Ordinal))
                return Row(MatchStatus.Unmatched, "None", "None", null, "The header cell is empty in the workbook, so there is no name to match.");

            var tokens = Tokens(original);
            bool student = tokens.Any(t => t is "student" or "students");
            var search = tokens.Where(t => t is not ("student" or "students")).ToList();
            string searchKey = string.Concat(search);

            if (employees == null)
                return student
                    ? Row(MatchStatus.StudentOrNonEmployee, "None", "None", null,
                          "The header text says 'student'. No employee list was supplied for this dry run, so no record was looked up.")
                    : Row(MatchStatus.Unmatched, "None", "None", null, "No employee list was supplied for this dry run, so no record was looked up.");

            if (searchKey.Length == 0)
                return student
                    ? Row(MatchStatus.StudentOrNonEmployee, "None", "None", null, "The header text says 'student' and contains no name to look up.")
                    : Row(MatchStatus.Unmatched, "None", "None", null, "The header text contains no letters or digits to match.");

            // Candidate tiers, strongest first. Each tier is only used if every stronger tier is empty.
            var identical = employees.Where(e => string.Equals(e.FullName.Trim(), original.Trim(), StringComparison.Ordinal)).ToList();
            var normalised = employees.Where(e => Key(e.FullName) == searchKey).Except(identical).ToList();
            int maxDistance = searchKey.Length >= 10 ? 2 : 1;
            var close = employees
                .Select(e => (E: e, D: Levenshtein(Key(e.FullName), searchKey)))
                .Where(x => x.D >= 1 && x.D <= maxDistance)
                .ToList();
            var close1 = close.Where(x => x.D == 1).Select(x => x.E).ToList();
            var close2 = close.Where(x => x.D == 2).Select(x => x.E).ToList();
            var contained = search.Count >= 2
                ? employees.Where(e =>
                  {
                      var et = Tokens(e.FullName);
                      return et.Count >= 2 && Key(e.FullName) != searchKey &&
                             (et.All(search.Contains) || search.All(et.Contains));
                  }).ToList()
                : new List<EmployeeRecord>();

            (List<EmployeeRecord> List, string Type, string Confidence, bool Exact)? tier =
                identical.Count > 0 ? (identical, "Identical text", "High", true)
                : normalised.Count > 0 ? (normalised, "Same after normalising case, punctuation, spacing and accents", "High", true)
                : close1.Count > 0 ? (close1, "Close spelling (1 character different)", "Medium", false)
                : close2.Count + contained.Count > 0 ? (close2.Union(contained).ToList(), "Close spelling (2 characters different) or one name contains the other", "Low", false)
                : null;

            if (tier == null)
            {
                // Same surname only is never proposed: it is very likely a different person.
                string last = search[^1];
                var sameSurname = employees.Where(e => Tokens(e.FullName) is { Count: > 0 } t && t[^1] == last).ToList();
                string note = sameSurname.Count > 0
                    ? $" Not proposed (same surname only, different first name - very likely a different person): {Describe(sameSurname)}."
                    : "";
                return student
                    ? Row(MatchStatus.StudentOrNonEmployee, "None", "None", null, "The header text says 'student'; no employee record has a matching name." + note)
                    : Row(MatchStatus.Unmatched, "None", "None", null, "No employee record has the same or a closely spelled name." + note);
            }

            var (list, type, confidence, exact) = tier.Value;
            var distinct = list.DistinctBy(e => e.EmployeeId).ToList();

            if (student)
                return Row(MatchStatus.StudentOrNonEmployee, "Not matched (student)", "None", null,
                    $"The header text says 'student', so no employee match is proposed. Employee record(s) with a matching name exist: " +
                    $"{Describe(distinct)} - confirm whether this person is (now) an employee.");

            if (distinct.Count > 1)
                return Row(MatchStatus.SuggestedMatch, "Ambiguous - " + type, "Low", null,
                    $"{distinct.Count} employee records match equally well, so none is proposed: {Describe(distinct)}. Choose the right one, if any.");

            var e = distinct[0];
            string reason = exact
                ? (identical.Count > 0
                    ? $"The workbook text is identical to employee record {e.EmployeeId} '{e.FullName}'."
                    : $"'{original}' and employee record {e.EmployeeId} '{e.FullName}' differ only in letter case, punctuation, spacing or accents.")
                : $"'{original}' is spelled similarly to employee record {e.EmployeeId} '{e.FullName}' ({type.ToLowerInvariant()}). A similar name is not proof that this is the same person.";

            var et2 = Tokens(e.FullName);
            if (!exact && et2.Count > 0 && search.Count > 0 && et2[0] != search[0])
                reason += $" The first names differ ('{search[0]}' vs '{et2[0]}') - this may well be a different person.";

            if (e.IsArchived == true)
                return Row(MatchStatus.ArchivedEmployee, type, confidence, e,
                    reason + " That employee record is ARCHIVED - confirm whether the matrix should include them.");

            if (e.IsArchived == null)
                reason += " Archived status is not known from the employee list.";

            return Row(exact ? MatchStatus.ExactCandidate : MatchStatus.SuggestedMatch, type, confidence, e,
                       reason + " Needs human confirmation before any import.");
        }

        private static string Describe(IEnumerable<EmployeeRecord> list) =>
            string.Join("; ", list.Select(e => $"ID {e.EmployeeId} '{e.FullName}'{(e.IsArchived == true ? " (archived)" : "")}"));

        // ---------------------------------------------------------------------------------------------
        // Name normalisation (used only to PROPOSE candidates; the original text is always kept)

        /// <summary>Lower case, accents removed, apostrophes removed, other punctuation as spaces, spaces collapsed.</summary>
        public static string Normalise(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (ch is '\'' or '’' or '‘' or '`') continue;       // O'Grady == OGrady
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static List<string> Tokens(string s) => Normalise(s).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        /// <summary>Normalised name without spaces: "James O'Grady", "james o grady" and "James OGrady" share one key.</summary>
        public static string Key(string s) => Normalise(s).Replace(" ", "");

        public static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        // ---------------------------------------------------------------------------------------------
        // Local employee list (CSV), read-only

        /// <summary>
        /// Reads a local employee list: header row with EmployeeID and FullName, optionally IsArchived
        /// (true/false, yes/no, 1/0; blank = not known). Opened read-only; nothing is written.
        /// </summary>
        public static EmployeeRoster LoadRosterCsv(string path)
        {
            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                bytes = ms.ToArray();
            }
            var records = ParseCsv(new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true).ReadToEnd());
            if (records.Count == 0) throw new InvalidDataException("The employee list is empty.");

            var header = records[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
            int idCol = header.IndexOf("employeeid"), nameCol = header.IndexOf("fullname"), archCol = header.IndexOf("isarchived");
            if (idCol < 0 || nameCol < 0)
                throw new InvalidDataException("The employee list must have a header row with EmployeeID and FullName (and optionally IsArchived).");

            var list = new List<EmployeeRecord>();
            var seen = new HashSet<long>();
            for (int i = 1; i < records.Count; i++)
            {
                var r = records[i];
                if (r.All(string.IsNullOrWhiteSpace)) continue;
                string Get(int c) => c >= 0 && c < r.Count ? r[c].Trim() : "";
                if (!long.TryParse(Get(idCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id <= 0)
                    throw new InvalidDataException($"Line {i + 1}: EmployeeID '{Get(idCol)}' is not a positive whole number.");
                if (!seen.Add(id))
                    throw new InvalidDataException($"Line {i + 1}: EmployeeID {id} appears more than once.");
                string name = Get(nameCol);
                if (name.Length == 0)
                    throw new InvalidDataException($"Line {i + 1}: FullName is empty.");
                bool? archived = Get(archCol).ToLowerInvariant() switch
                {
                    "true" or "yes" or "1" or "t" or "y" => true,
                    "false" or "no" or "0" or "f" or "n" => false,
                    "" => null,
                    var other => throw new InvalidDataException($"Line {i + 1}: IsArchived '{other}' is not true/false."),
                };
                list.Add(new EmployeeRecord(id, name, archived));
            }
            return new EmployeeRoster(Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)), list);
        }

        /// <summary>Minimal RFC 4180 parser (quoted fields, doubled quotes, line breaks inside quotes).</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false, any = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else field.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"': quoted = true; any = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); any = true; break;
                    case '\r': break;
                    case '\n': row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); any = false; break;
                    default: field.Append(ch); any = true; break;
                }
            }
            if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
