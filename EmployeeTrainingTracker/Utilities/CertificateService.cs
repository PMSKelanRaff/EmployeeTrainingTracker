using EmployeeTrainingTracker.Helpers;
using Npgsql; 
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmployeeTrainingTracker.Utilities
{
    public static class CertificateService
    {
        public static DataTable GetCertificates(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            NpgsqlCommand cmd;

            if (employeeId == 0)
            {
                // "All Employees" - Added tc.status!
                cmd = new NpgsqlCommand(@"
            SELECT 
                tc.CertificateID, 
                tc.CertificateName, 
                tc.Key, 
                tc.HRS, 
                tc.Provider, 
                tc.IssueDate, 
                tc.ExpiryDate, 
                tc.FilePath, 
                tc.S3Key, 
                tc.LastNotifiedDate, 
                tc.ismarkedfordeletion, 
                tc.status, 
                COALESCE(e.FullName, 'Unknown') AS EmployeeName    
            FROM TrainingCertificates tc
            LEFT JOIN Employees e ON tc.EmployeeID = e.EmployeeID
            WHERE NOT COALESCE(e.IsArchived, FALSE)    -- archived employees' certificates are kept but not listed here
              AND tc.DeletedAt IS NULL                 -- soft-deleted (migration 008): only on the Admin Tasks tab
            ORDER BY e.FullName ASC, tc.IssueDate DESC", conn);
            }
            else
            {
                // Specific Employee - Added tc.status!
                cmd = new NpgsqlCommand(@"
            SELECT 
                tc.CertificateID, 
                tc.CertificateName, 
                tc.Key, 
                tc.HRS, 
                tc.Provider, 
                tc.IssueDate, 
                tc.ExpiryDate, 
                tc.FilePath, 
                tc.S3Key, 
                tc.LastNotifiedDate, 
                tc.ismarkedfordeletion, 
                tc.status, 
                COALESCE(e.FullName, 'Unknown') AS EmployeeName    
            FROM TrainingCertificates tc    
            LEFT JOIN Employees e ON tc.EmployeeID = e.EmployeeID    
            WHERE tc.EmployeeID = $1 AND tc.DeletedAt IS NULL
            ORDER BY tc.IssueDate DESC", conn);

                cmd.Parameters.AddWithValue(employeeId);
            }

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        // Manager view - only certificates for employees in groups they manage.
        // employeeId = 0 means "All Employees"; otherwise that employee must also be one of the manager's.
        public static DataTable GetCertificatesForManager(int managerId, int employeeId = 0)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
            SELECT
                tc.CertificateID,
                tc.CertificateName,
                tc.Key,
                tc.HRS,
                tc.Provider,
                tc.IssueDate,
                tc.ExpiryDate,
                tc.FilePath,
                tc.S3Key,
                tc.LastNotifiedDate,
                tc.ismarkedfordeletion,
                tc.status,
                COALESCE(e.FullName, 'Unknown') AS EmployeeName
            FROM TrainingCertificates tc
            INNER JOIN Employees e ON tc.EmployeeID = e.EmployeeID
            WHERE (tc.EmployeeID = @managerId    -- the manager's own certificates
                   OR tc.EmployeeID IN (
                       SELECT gm.EmployeeID
                       FROM GroupMembers gm
                       INNER JOIN Groups g ON gm.GroupID = g.GroupID
                       WHERE g.ManagerID = @managerId))
              AND (@employeeId = 0 OR tc.EmployeeID = @employeeId)
              AND tc.DeletedAt IS NULL
            ORDER BY e.FullName ASC, tc.IssueDate DESC", conn);

            cmd.Parameters.AddWithValue("managerId", managerId);
            cmd.Parameters.AddWithValue("employeeId", employeeId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static async Task<bool> SaveCertificateAsync(
        int employeeId, string employeeName, string certName, string localFilePath,
        DateTime issueDate, DateTime expiryDate, string key, double? cpdHrs, string provider)
        {
            string fileName = Path.GetFileName(localFilePath);
            string cleanName = string.IsNullOrWhiteSpace(employeeName) ? "Unknown" : employeeName.Replace(" ", "_");
            string s3Key = $"{cleanName}_{employeeId}/{fileName}";

            try
            {
                // 1. Upload to S3
                bool s3Success = await S3Service.UploadCertificateAsync(localFilePath, s3Key);
                if (!s3Success) return false;

                // 2. Save EVERYTHING to PostgreSQL
                using (var conn = DatabaseHelper.GetConnection())
                {
                    await conn.OpenAsync();
                    const string sql = @"INSERT INTO TrainingCertificates 
                                (EmployeeID, CertificateName, S3Key, IssueDate, ExpiryDate, Key, HRS, Provider, IsMarkedForDeletion) 
                                VALUES (@empId, @name, @keyPath, @issue, @expiry, @key, @hrs, @provider, FALSE)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("empId", employeeId);
                        cmd.Parameters.AddWithValue("name", certName);
                        cmd.Parameters.AddWithValue("keyPath", s3Key);
                        cmd.Parameters.Add(DatabaseHelper.DateParam(issueDate, "issue"));
                        cmd.Parameters.Add(DatabaseHelper.DateParam(expiryDate, "expiry"));
                        cmd.Parameters.AddWithValue("key", key ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("hrs", cpdHrs ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("provider", provider ?? (object)DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Upload Workflow Error: {ex.Message}");
                return false;
            }
        }

        public static void AddCertificate(int employeeId, string certName, string key, double cpdHrs, string provider, DateTime issueDate, DateTime? expiryDate, string? filePath = null)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)", conn);


            cmd.Parameters.AddWithValue(employeeId);
            cmd.Parameters.AddWithValue(certName);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue(cpdHrs);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.Add(DatabaseHelper.DateParam(issueDate));
            cmd.Parameters.Add(DatabaseHelper.DateParam(expiryDate));

            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateCertificate(int certId, string name, string key, double hrs, string provider, DateTime issueDate, DateTime? expiryDate, string? filePath)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
        UPDATE TrainingCertificates 
        SET CertificateName = $1, 
            Key = $2, 
            HRS = $3, 
            Provider = $4, 
            IssueDate = $5, 
            ExpiryDate = $6, 
            FilePath = $7
        WHERE CertificateID = $8 AND DeletedAt IS NULL", conn);

            cmd.Parameters.AddWithValue(name);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue(hrs);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.Add(DatabaseHelper.DateParam(issueDate));
            cmd.Parameters.Add(DatabaseHelper.DateParam(expiryDate)); // null = no expiry

            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

            // The ID goes last because it is the last parameter ($8) in the query
            cmd.Parameters.AddWithValue(certId);

            cmd.ExecuteNonQuery();
        }

        // ------------------------------------------------------------------
        // Soft delete (migration 008, decision 2026-11-18): certificates are never deleted from the database or
        // S3. A delete marks the row (DeletedAt, who, why) and keeps IsMarkedForDeletion = TRUE, which hides it from
        // every list in the app and from the certificate notifier; an Admin can restore it from the Tasks tab.
        // The core methods take an open connection (tests run them against a disposable database).
        // ------------------------------------------------------------------

        public const string ReasonDeletionRequestApproved = "DeletionRequestApproved";   // Tasks: Delete
        public const string ReasonUploadRejected = "UploadRejected";                     // Tasks: Reject (awaiting approval)
        public const string ReasonDeletedDirectly = "DeletedDirectly";                   // Certificates tab: Delete

        public static void SoftDeleteCertificate(int certId, int byEmployeeId, string reason)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            SoftDeleteCertificate(conn, certId, byEmployeeId, reason);
        }

        /// <summary>Throws InvalidOperationException when the certificate does not exist or is already deleted.</summary>
        public static void SoftDeleteCertificate(NpgsqlConnection conn, int certId, int byEmployeeId, string reason)
        {
            if (reason is not (ReasonDeletionRequestApproved or ReasonUploadRejected or ReasonDeletedDirectly))
                throw new ArgumentException($"Unknown deletion reason '{reason}'.", nameof(reason));

            using var cmd = new NpgsqlCommand(@"
                UPDATE TrainingCertificates
                SET DeletedAt = CURRENT_TIMESTAMP,
                    DeletedByEmployeeID = @by,
                    DeletedByName = COALESCE((SELECT FullName FROM Employees WHERE EmployeeID = @by), 'employee ' || @by),
                    DeletedReason = @reason,
                    IsMarkedForDeletion = TRUE
                WHERE CertificateID = @id AND DeletedAt IS NULL", conn);
            cmd.Parameters.AddWithValue("id", (long)certId);
            cmd.Parameters.AddWithValue("by", (long)byEmployeeId);
            cmd.Parameters.AddWithValue("reason", reason);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("That certificate no longer exists or has already been deleted. Refresh and try again.");
        }

        public static void RestoreCertificate(int certId, int adminEmployeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            RestoreCertificate(conn, certId, adminEmployeeId);
        }

        /// <summary>
        /// Puts a soft-deleted certificate back where it was: an approved deletion or a direct delete -> an active
        /// certificate again (no longer marked for deletion); a rejected upload -> back in "awaiting approval".
        /// Only an active Admin (re-checked here). Throws UnauthorizedAccessException / InvalidOperationException.
        /// </summary>
        public static void RestoreCertificate(NpgsqlConnection conn, int certId, int adminEmployeeId)
        {
            using (var check = new NpgsqlCommand(@"
                SELECT EXISTS (SELECT 1 FROM Users u JOIN Employees e ON e.EmployeeID = u.EmployeeID
                               WHERE u.EmployeeID = @admin AND u.Role = 'Admin' AND NOT COALESCE(e.IsArchived, FALSE))", conn))
            {
                check.Parameters.AddWithValue("admin", (long)adminEmployeeId);
                if (check.ExecuteScalar() is not true)
                    throw new UnauthorizedAccessException("Only Admins can restore deleted certificates.");
            }

            using var cmd = new NpgsqlCommand(@"
                UPDATE TrainingCertificates
                SET IsMarkedForDeletion = FALSE, DeletedAt = NULL, DeletedByEmployeeID = NULL, DeletedByName = NULL, DeletedReason = NULL
                WHERE CertificateID = @id AND DeletedAt IS NOT NULL", conn);
            cmd.Parameters.AddWithValue("id", (long)certId);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("That certificate is not deleted (it may have been restored already). Refresh and try again.");
        }

        public static DataTable GetDeletedCertificates()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            return GetDeletedCertificates(conn);
        }

        /// <summary>Soft-deleted certificates, newest first, with the same columns as the deletion requests plus who/when/why.</summary>
        public static DataTable GetDeletedCertificates(NpgsqlConnection conn)
        {
            using var cmd = new NpgsqlCommand(@"
                SELECT
                    tc.CertificateID,
                    COALESCE(e.FullName, 'Unknown') AS EmployeeName,
                    tc.CertificateName,
                    tc.Key,
                    tc.HRS,
                    tc.Provider,
                    tc.IssueDate,
                    tc.ExpiryDate,
                    tc.S3Key,
                    tc.LastNotifiedDate,
                    tc.DeletedAt,
                    tc.DeletedByName,
                    CASE tc.DeletedReason
                        WHEN 'DeletionRequestApproved' THEN 'Deletion request approved'
                        WHEN 'UploadRejected'          THEN 'Rejected while awaiting approval'
                        WHEN 'DeletedDirectly'         THEN 'Deleted on the Certificates tab'
                        ELSE tc.DeletedReason END AS DeletedReason
                FROM TrainingCertificates tc
                LEFT JOIN Employees e ON e.EmployeeID = tc.EmployeeID
                WHERE tc.DeletedAt IS NOT NULL
                ORDER BY tc.DeletedAt DESC, tc.CertificateID DESC", conn);
            using var reader = cmd.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static void MarkCertificateForDeletion(int certId)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                // We update the boolean instead of deleting the row
                using (var cmd = new NpgsqlCommand(
                    "UPDATE TrainingCertificates SET IsMarkedForDeletion = TRUE WHERE CertificateID = $1 AND DeletedAt IS NULL", conn))
                {
                    cmd.Parameters.AddWithValue(certId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Fetches all certificates flagged for deletion for the Admin Tasks Tab
        public static DataTable GetPendingDeletions()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
        SELECT 
            tc.CertificateID, 
            e.FullName AS EmployeeName, 
            tc.CertificateName, 
            tc.Key,
            tc.HRS,
            tc.Provider,
            tc.IssueDate, 
            tc.ExpiryDate,
            tc.S3Key,
            tc.LastNotifiedDate
        FROM TrainingCertificates tc
        JOIN Employees e ON tc.EmployeeID = e.EmployeeID
        WHERE tc.ismarkedfordeletion = TRUE AND NOT e.IsArchived AND tc.DeletedAt IS NULL
        ORDER BY tc.IssueDate DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static DataTable GetPendingDeletionsForManager(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // This query joins the Groups and GroupMembers tables to ensure 
            // the manager only sees requests from employees they manage.
            string sql = @"
        SELECT DISTINCT
            tc.CertificateID, 
            e.FullName AS EmployeeName, 
            tc.CertificateName, 
            tc.Key,
            tc.HRS,
            tc.Provider,
            tc.IssueDate, 
            tc.ExpiryDate,
            tc.S3Key,
            tc.LastNotifiedDate
        FROM TrainingCertificates tc
        JOIN Employees e ON tc.EmployeeID = e.EmployeeID
        JOIN GroupMembers gm ON e.EmployeeID = gm.EmployeeID
        JOIN Groups g ON gm.GroupID = g.GroupID
        WHERE tc.ismarkedfordeletion = TRUE AND g.ManagerID = @managerId AND tc.DeletedAt IS NULL
        ORDER BY tc.IssueDate DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("managerId", managerId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        // Un-flags a certificate if the Admin rejects the deletion
        public static void UnmarkCertificateForDeletion(int certificateId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("UPDATE TrainingCertificates SET IsMarkedForDeletion = FALSE WHERE CertificateID = @id AND DeletedAt IS NULL", conn);
            cmd.Parameters.AddWithValue("id", certificateId);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Certificates awaiting approval. managerId = a Manager: only employees in groups they manage;
        /// null = an Admin: everyone (decision 2026-11-18, as for deletion requests). Archived employees are left out.
        /// </summary>
        public static DataTable GetPendingCertificateApprovals(int? managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Same columns as the deletion requests (GetPendingDeletions), so both Tasks tables read the same,
            // plus the Trainer Name typed in when acknowledging
            string sql = @"
        SELECT
            tc.CertificateID,
            e.FullName AS EmployeeName,
            tc.CertificateName,
            tc.Key,
            tc.HRS,
            tc.Provider,
            tc.IssueDate,
            tc.ExpiryDate,
            tc.S3Key,
            tc.LastNotifiedDate,
            '' AS trainername
        FROM trainingcertificates tc
        JOIN employees e ON tc.EmployeeID = e.EmployeeID
        WHERE tc.status = 'Pending' AND tc.DeletedAt IS NULL AND NOT COALESCE(e.IsArchived, FALSE)
          AND (@manId IS NULL OR EXISTS (
                SELECT 1 FROM GroupMembers gm JOIN Groups g ON gm.GroupID = g.GroupID
                WHERE gm.EmployeeID = e.EmployeeID AND g.ManagerID = @manId))
        ORDER BY tc.CertificateID ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("manId", NpgsqlTypes.NpgsqlDbType.Bigint) { Value = managerId.HasValue ? (long)managerId.Value : DBNull.Value });

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static void ApproveCertificate(int certificateId, string trainerName)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
        UPDATE trainingcertificates 
        SET status = 'Completed', 
            trainername = @trainer, 
            trainersignedat = CURRENT_TIMESTAMP
        WHERE CertificateID = @certId AND DeletedAt IS NULL";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("trainer", trainerName);
            cmd.Parameters.AddWithValue("certId", certificateId);

            cmd.ExecuteNonQuery();
        }
    }
}