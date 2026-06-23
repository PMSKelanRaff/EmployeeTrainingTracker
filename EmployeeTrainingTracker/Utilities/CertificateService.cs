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
            WHERE tc.EmployeeID = $1    
            ORDER BY tc.IssueDate DESC", conn);

                cmd.Parameters.AddWithValue(employeeId);
            }

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
                        cmd.Parameters.AddWithValue("issue", issueDate);
                        cmd.Parameters.AddWithValue("expiry", expiryDate);
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

        public static void AddCertificate(int employeeId, string certName, string key, double cpdHrs, string provider, string issueDate, string? expiryDate, string? filePath = null)
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
            cmd.Parameters.AddWithValue(issueDate);
            if (!string.IsNullOrEmpty(expiryDate))
            {
                // It is already a string, so just pass it.
                cmd.Parameters.AddWithValue(expiryDate);
            }
            else
            {
                cmd.Parameters.AddWithValue(DBNull.Value);
            }

            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateCertificate(int certId, string name, string key, double hrs, string provider, string issueDate, string? expiryDate, string? filePath)
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
        WHERE CertificateID = $8", conn);

            cmd.Parameters.AddWithValue(name);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue(hrs);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.AddWithValue(issueDate);

            // Check for null string
            if (!string.IsNullOrEmpty(expiryDate))
                cmd.Parameters.AddWithValue(expiryDate);
            else
                cmd.Parameters.AddWithValue(DBNull.Value);

            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

            // The ID goes last because it is the last parameter ($8) in the query
            cmd.Parameters.AddWithValue(certId);

            cmd.ExecuteNonQuery();
        }

        public static void DeleteCertificate(int certId)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "DELETE FROM TrainingCertificates WHERE CertificateID=$1", conn))
                {
                    cmd.Parameters.AddWithValue(certId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void MarkCertificateForDeletion(int certId)
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                // We update the boolean instead of deleting the row
                using (var cmd = new NpgsqlCommand(
                    "UPDATE TrainingCertificates SET IsMarkedForDeletion = TRUE WHERE CertificateID = $1", conn))
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
        WHERE tc.ismarkedfordeletion = TRUE
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
        WHERE tc.ismarkedfordeletion = TRUE AND g.ManagerID = @managerId
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
            using var cmd = new NpgsqlCommand("UPDATE TrainingCertificates SET IsMarkedForDeletion = FALSE WHERE CertificateID = @id", conn);
            cmd.Parameters.AddWithValue("id", certificateId);
            cmd.ExecuteNonQuery();
        }

        public static DataTable GetPendingCertificateApprovals(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
        SELECT 
            tc.CertificateID, 
            e.FullName AS employeename,
            tc.CertificateName AS trainingtopic, 
            tc.HRS AS hours, 
            tc.IssueDate AS trainingdate, 
            tc.S3Key,
            '' AS trainername
        FROM trainingcertificates tc
        JOIN employees e ON tc.EmployeeID = e.EmployeeID
        JOIN GroupMembers gm ON e.EmployeeID = gm.EmployeeID
        JOIN Groups g ON gm.GroupID = g.GroupID
        WHERE g.ManagerID = @manId AND tc.status = 'Pending'
        ORDER BY tc.CertificateID ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("manId", managerId);

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
        WHERE CertificateID = @certId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("trainer", trainerName);
            cmd.Parameters.AddWithValue("certId", certificateId);

            cmd.ExecuteNonQuery();
        }
    }
}