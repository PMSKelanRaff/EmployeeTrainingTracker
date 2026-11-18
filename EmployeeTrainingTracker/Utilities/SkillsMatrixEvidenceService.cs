using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixService;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Supporting evidence for Skills Matrix levels (migration 009): one APPROVED certificate of the cell's person
    /// linked to a cell. The link is valid while the cell's level equals the level it was linked at (decision
    /// 2026-11-18); otherwise it needs relinking. Links are never deleted: replacing or removing one closes it.
    ///
    /// Who may link / remove: whoever may change that cell's level (SkillsMatrixLiveService.Access.CanEdit: Admins,
    /// and Managers on the sheets where they manage a team, not their own column) - re-checked inside the transaction.
    /// Anyone who can see the sheet sees the links and can open the document.
    /// The core methods take an open connection (tests run them against a disposable database).
    /// </summary>
    public static class SkillsMatrixEvidenceService
    {
        public enum EvidenceState { None, Valid, NeedsRelinking, CertificateRemoved }

        /// <summary>The current link of one cell.</summary>
        public sealed record CellEvidence(long EvidenceId, long SkillId, long ColumnId, long CertificateId, string CertificateName,
            string? S3Key, int LinkedLevel, DateTime LinkedAt, string LinkedBy, bool CertificateDeleted, bool CertificateApproved)
        {
            /// <summary>Valid / needs relinking (level changed) / certificate no longer usable (deleted or no longer approved).</summary>
            public EvidenceState StateFor(SkillCellInfo? cell) =>
                CertificateDeleted || !CertificateApproved ? EvidenceState.CertificateRemoved
                : cell is { Status: "Level" } && cell.Level == LinkedLevel ? EvidenceState.Valid
                : EvidenceState.NeedsRelinking;
        }

        /// <summary>A certificate that can be linked: the person's own, approved, not deleted.</summary>
        public sealed record LinkableCertificate(long CertificateId, string Name, string? Key, string? Provider,
            DateTime? IssueDate, DateTime? ExpiryDate, string? S3Key);

        public static EvidenceState StateOf(CellEvidence? evidence, SkillCellInfo? cell) =>
            evidence == null ? EvidenceState.None : evidence.StateFor(cell);

        // ---- Application wrappers ----------------------------------------------------------------------

        public static bool IsInstalled()
        {
            using var conn = Open();
            return IsInstalled(conn);
        }

        public static Dictionary<(long SkillId, long ColumnId), CellEvidence> GetEvidence(int employeeId, long liveAreaId)
        {
            using var conn = Open();
            return GetEvidence(conn, SkillsMatrixLiveService.GetAccess(conn, null, employeeId), liveAreaId);
        }

        public static List<LinkableCertificate> GetLinkableCertificates(int employeeId, long liveAreaId, long columnId)
        {
            using var conn = Open();
            return GetLinkableCertificates(conn, SkillsMatrixLiveService.GetAccess(conn, null, employeeId), liveAreaId, columnId);
        }

        public static void LinkCertificate(int editorEmployeeId, long liveAreaId, long skillId, long columnId, long certificateId)
        {
            using var conn = Open();
            LinkCertificate(conn, editorEmployeeId, liveAreaId, skillId, columnId, certificateId);
        }

        public static void RemoveLink(int editorEmployeeId, long liveAreaId, long skillId, long columnId)
        {
            using var conn = Open();
            RemoveLink(conn, editorEmployeeId, liveAreaId, skillId, columnId);
        }

        private static NpgsqlConnection Open()
        {
            var conn = DatabaseHelper.GetConnection();
            conn.Open();
            return conn;
        }

        // ---- Core ---------------------------------------------------------------------------------------

        public static bool IsInstalled(NpgsqlConnection conn, NpgsqlTransaction? tx = null)
        {
            using var cmd = new NpgsqlCommand("SELECT to_regclass('public.skillmatrixcellevidence') IS NOT NULL", conn, tx);
            return cmd.ExecuteScalar() is true;
        }

        /// <summary>The current links on one live sheet (the caller must be able to see the sheet).</summary>
        public static Dictionary<(long SkillId, long ColumnId), CellEvidence> GetEvidence(NpgsqlConnection conn,
            SkillsMatrixLiveService.Access access, long liveAreaId)
        {
            if (!access.CanSeeArea(liveAreaId))
                throw new UnauthorizedAccessException(SkillsMatrixLiveService.NotYourSheet);
            var result = new Dictionary<(long, long), CellEvidence>();
            if (!IsInstalled(conn)) return result;

            using var cmd = new NpgsqlCommand(@"
                SELECT ev.EvidenceID, ev.SkillID, ev.ColumnID, ev.CertificateID, COALESCE(tc.CertificateName, '(no name)'), tc.S3Key,
                       ev.LinkedLevel, ev.LinkedAt, ev.LinkedByName, tc.DeletedAt IS NOT NULL, COALESCE(tc.Status, '') = 'Completed'
                FROM public.SkillMatrixCellEvidence ev
                JOIN public.SkillMatrixLiveCells c ON c.SkillID = ev.SkillID AND c.ColumnID = ev.ColumnID
                JOIN public.TrainingCertificates tc ON tc.CertificateID = ev.CertificateID
                WHERE ev.RemovedAt IS NULL AND c.AreaID = @a", conn);
            cmd.Parameters.AddWithValue("a", liveAreaId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var e = new CellEvidence(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5), r.GetInt16(6), r.GetDateTime(7), r.GetString(8), r.GetBoolean(9), r.GetBoolean(10));
                result[(e.SkillId, e.ColumnId)] = e;
            }
            return result;
        }

        /// <summary>The certificates that can back a level for the person of one column: theirs, approved, not deleted.</summary>
        public static List<LinkableCertificate> GetLinkableCertificates(NpgsqlConnection conn, SkillsMatrixLiveService.Access access,
            long liveAreaId, long columnId)
        {
            if (!access.CanSeeArea(liveAreaId))
                throw new UnauthorizedAccessException(SkillsMatrixLiveService.NotYourSheet);
            using var cmd = new NpgsqlCommand(@"
                SELECT tc.CertificateID, COALESCE(tc.CertificateName, '(no name)'), tc.Key, tc.Provider, tc.IssueDate, tc.ExpiryDate, tc.S3Key
                FROM public.SkillMatrixLiveColumns col
                JOIN public.TrainingCertificates tc ON tc.EmployeeID = col.EmployeeID
                WHERE col.ColumnID = @c AND col.AreaID = @a
                  AND tc.Status = 'Completed' AND tc.DeletedAt IS NULL
                ORDER BY tc.IssueDate DESC NULLS LAST, tc.CertificateName", conn);
            cmd.Parameters.AddWithValue("c", columnId);
            cmd.Parameters.AddWithValue("a", liveAreaId);
            var list = new List<LinkableCertificate>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new LinkableCertificate(r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3), ReadDate(r, 4), ReadDate(r, 5), r.IsDBNull(6) ? null : r.GetString(6)));
            return list;
        }

        /// <summary>
        /// Links a certificate to a cell at its current level, replacing (closing) any current link. All-or-nothing.
        /// Throws UnauthorizedAccessException (may not change this cell), ArgumentException (cell or certificate not
        /// allowed: not a 1-5 level, archived person, someone else's / unapproved / deleted certificate).
        /// </summary>
        public static void LinkCertificate(NpgsqlConnection conn, int editorEmployeeId, long liveAreaId, long skillId, long columnId, long certificateId)
        {
            using var tx = conn.BeginTransaction();
            try
            {
                Exec(conn, tx, "SET LOCAL lock_timeout = '5s'");
                if (!IsInstalled(conn, tx))
                    throw new InvalidOperationException("Evidence links are not set up in this database (migration 009). Nothing was linked.");
                var access = SkillsMatrixLiveService.GetAccess(conn, tx, editorEmployeeId);
                var cell = LockCell(conn, tx, liveAreaId, skillId, columnId, access, "link evidence to");
                if (cell.Status != "Level" || cell.Level is not (>= 1 and <= 5))
                    throw new ArgumentException("Evidence can only be linked to a level 1-5. Nothing was linked.");

                long? owner; string? status; bool deleted;
                using (var cmd = new NpgsqlCommand(
                    "SELECT EmployeeID, Status, DeletedAt IS NOT NULL FROM public.TrainingCertificates WHERE CertificateID = @id FOR SHARE", conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", certificateId);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read()) throw new ArgumentException("That certificate no longer exists. Nothing was linked.");
                    owner = r.IsDBNull(0) ? null : Convert.ToInt64(r.GetValue(0));
                    status = r.IsDBNull(1) ? null : r.GetString(1);
                    deleted = r.GetBoolean(2);
                }
                if (owner != cell.EmployeeId)
                    throw new ArgumentException($"That certificate does not belong to {cell.EmployeeName}. Nothing was linked.");
                if (deleted)
                    throw new ArgumentException("That certificate has been deleted. Nothing was linked.");
                if (status != "Completed")
                    throw new ArgumentException("Only approved certificates can be linked (this one is awaiting approval). Nothing was linked.");

                Exec(conn, tx, @"
                    UPDATE public.SkillMatrixCellEvidence SET RemovedAt = CURRENT_TIMESTAMP, RemovedByName = @by, RemovedReason = 'Replaced'
                    WHERE SkillID = @s AND ColumnID = @c AND RemovedAt IS NULL",
                    ("by", access.Name), ("s", skillId), ("c", columnId));
                Exec(conn, tx, @"
                    INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByEmployeeID, LinkedByName)
                    VALUES (@s, @c, @e, @cert, @lv, @byId, @by)",
                    ("s", skillId), ("c", columnId), ("e", cell.EmployeeId), ("cert", certificateId), ("lv", (short)cell.Level.Value),
                    ("byId", (long)editorEmployeeId), ("by", access.Name));
                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* already rolled back / connection broken: the server rolls back */ }
                throw;
            }
        }

        /// <summary>Removes (closes) a cell's current link. Throws InvalidOperationException when the cell has none.</summary>
        public static void RemoveLink(NpgsqlConnection conn, int editorEmployeeId, long liveAreaId, long skillId, long columnId)
        {
            using var tx = conn.BeginTransaction();
            try
            {
                Exec(conn, tx, "SET LOCAL lock_timeout = '5s'");
                var access = SkillsMatrixLiveService.GetAccess(conn, tx, editorEmployeeId);
                LockCell(conn, tx, liveAreaId, skillId, columnId, access, "remove evidence from");
                int n = Exec(conn, tx, @"
                    UPDATE public.SkillMatrixCellEvidence SET RemovedAt = CURRENT_TIMESTAMP, RemovedByName = @by, RemovedReason = 'Unlinked'
                    WHERE SkillID = @s AND ColumnID = @c AND RemovedAt IS NULL",
                    ("by", access.Name), ("s", skillId), ("c", columnId));
                if (n == 0) throw new InvalidOperationException("This cell has no evidence linked (it may have been removed already). Refresh the tab.");
                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* already rolled back / connection broken: the server rolls back */ }
                throw;
            }
        }

        // ---- Re-import (called by SkillsMatrixImportWriter inside the import transaction) ----------------

        // Identity of a live cell that survives a rebuild: sheet name, category name, skill name and its n-th
        // occurrence there; the person and their n-th column on the sheet (as SkillsMatrixMerge matches them).
        private const string IdentitySql = @"
            sk AS (SELECT s.SkillID, a.Name AS area, cat.Name AS cat, s.Name AS skill,
                          row_number() OVER (PARTITION BY s.AreaID, s.CategoryID, s.Name ORDER BY s.DisplayOrder, s.SkillID) AS occ
                   FROM public.SkillMatrixLiveSkills s
                   JOIN public.SkillMatrixLiveAreas a ON a.AreaID = s.AreaID
                   JOIN public.SkillMatrixLiveCategories cat ON cat.CategoryID = s.CategoryID
                   WHERE NOT s.IsDeleted),
            co AS (SELECT col.ColumnID, a.Name AS area, col.EmployeeID,
                          row_number() OVER (PARTITION BY col.AreaID, col.EmployeeID ORDER BY col.DisplayOrder, col.ColumnID) AS occ
                   FROM public.SkillMatrixLiveColumns col
                   JOIN public.SkillMatrixLiveAreas a ON a.AreaID = col.AreaID
                   WHERE NOT col.IsDeleted)";

        /// <summary>Before the live copy is rebuilt: remembers where every current link is (by identity, not ID).</summary>
        internal static void RememberLinksBeforeRebuild(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            Exec(conn, tx, $@"
                CREATE TEMP TABLE sm_evidence_carry ON COMMIT DROP AS
                WITH {IdentitySql}
                SELECT ev.EvidenceID, sk.area AS skarea, sk.cat, sk.skill, sk.occ AS skocc, co.area AS coarea, co.EmployeeID, co.occ AS cocc
                FROM public.SkillMatrixCellEvidence ev
                LEFT JOIN sk ON sk.SkillID = ev.SkillID
                LEFT JOIN co ON co.ColumnID = ev.ColumnID
                WHERE ev.RemovedAt IS NULL");
        }

        /// <summary>
        /// After the rebuild: points every remembered link at the same cell in the new live copy; links whose cell is
        /// not in the new import are closed ('NotInNewImport'). Whether a carried link is still valid follows from the
        /// level (a level changed by the workbook needs relinking). Returns (carried, closed).
        /// </summary>
        internal static (int Carried, int Closed) CarryLinksAfterRebuild(NpgsqlConnection conn, NpgsqlTransaction tx, string importedBy)
        {
            int carried = Exec(conn, tx, $@"
                WITH {IdentitySql}
                UPDATE public.SkillMatrixCellEvidence ev SET SkillID = sk.SkillID, ColumnID = co.ColumnID
                FROM pg_temp.sm_evidence_carry x
                JOIN sk ON sk.area = x.skarea AND sk.cat = x.cat AND sk.skill = x.skill AND sk.occ = x.skocc
                JOIN co ON co.area = x.coarea AND co.EmployeeID = x.EmployeeID AND co.occ = x.cocc AND co.area = sk.area
                JOIN public.SkillMatrixLiveCells c ON c.SkillID = sk.SkillID AND c.ColumnID = co.ColumnID
                WHERE ev.EvidenceID = x.EvidenceID");
            int closed = Exec(conn, tx, @"
                UPDATE public.SkillMatrixCellEvidence ev
                SET RemovedAt = CURRENT_TIMESTAMP, RemovedByName = @by, RemovedReason = 'NotInNewImport'
                WHERE ev.RemovedAt IS NULL AND ev.EvidenceID IN (SELECT EvidenceID FROM pg_temp.sm_evidence_carry)
                  AND NOT EXISTS (SELECT 1 FROM public.SkillMatrixLiveCells c WHERE c.SkillID = ev.SkillID AND c.ColumnID = ev.ColumnID)",
                ("by", importedBy));
            Exec(conn, tx, "DROP TABLE pg_temp.sm_evidence_carry");
            return (carried, closed);
        }

        // ---- Helpers --------------------------------------------------------------------------------------

        private sealed record LockedCell(string Status, int? Level, long EmployeeId, string EmployeeName);

        // Locks the cell (no level change can slip in while linking) and checks the editor may change it
        private static LockedCell LockCell(NpgsqlConnection conn, NpgsqlTransaction tx, long liveAreaId, long skillId, long columnId,
            SkillsMatrixLiveService.Access access, string what)
        {
            using var cmd = new NpgsqlCommand(@"
                SELECT c.AreaID, c.Status, c.Level, col.EmployeeID, e.FullName, e.IsArchived, s.IsDeleted, col.IsDeleted
                FROM public.SkillMatrixLiveCells c
                JOIN public.SkillMatrixLiveSkills s ON s.SkillID = c.SkillID
                JOIN public.SkillMatrixLiveColumns col ON col.ColumnID = c.ColumnID
                JOIN public.Employees e ON e.EmployeeID = col.EmployeeID
                WHERE c.SkillID = @s AND c.ColumnID = @c
                FOR UPDATE OF c", conn, tx);
            cmd.Parameters.AddWithValue("s", skillId);
            cmd.Parameters.AddWithValue("c", columnId);
            using var r = cmd.ExecuteReader();
            if (!r.Read() || r.GetInt64(0) != liveAreaId)
                throw new ArgumentException("That cell is not on this sheet (the matrix may have been re-imported). Refresh the tab.");
            var cell = new LockedCell(r.GetString(1), r.IsDBNull(2) ? null : r.GetInt16(2), Convert.ToInt64(r.GetValue(3)), r.GetString(4));
            bool archived = r.GetBoolean(5), removed = r.GetBoolean(6) || r.GetBoolean(7);
            r.Close();
            if (removed) throw new ArgumentException("That skill or person has been removed from the sheet. Refresh the tab.");
            if (archived) throw new ArgumentException($"{cell.EmployeeName} is archived (Employees tab), so their levels are read-only.");
            if (!access.CanEdit(liveAreaId, cell.EmployeeId))
                throw new UnauthorizedAccessException(!access.CanSeeArea(liveAreaId)
                    ? SkillsMatrixLiveService.NotYourSheet
                    : $"You cannot {what} your own levels; ask your manager or an Admin.");
            return cell;
        }

        private static DateTime? ReadDate(NpgsqlDataReader r, int i) =>
            r.IsDBNull(i) ? null : r.GetValue(i) switch { DateTime d => d, DateOnly d => d.ToDateTime(TimeOnly.MinValue), _ => null };

        private static int Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, params (string Name, object? Value)[] ps)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }
    }
}
