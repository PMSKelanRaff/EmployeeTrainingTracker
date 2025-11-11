using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql; 

namespace EmployeeTrainingTracker.Utilities
{
    public static class CertificateService
    {
        public static DataTable GetCertificates(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                  SELECT CertificateID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath, LastNotifiedDate
                    FROM TrainingCertificates
                    WHERE EmployeeID = $1", conn);

            cmd.Parameters.AddWithValue(employeeId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static void AddCertificate(int employeeId, string certName, string key, double cpdHrs, string provider, DateTime issueDate, DateTime expiryDate, string? filePath = null)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)", conn);

            // Positional parameters in order
            cmd.Parameters.AddWithValue(employeeId);
            cmd.Parameters.AddWithValue(certName);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue(cpdHrs); // Assumes 0 if not provided
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.AddWithValue(issueDate.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue(expiryDate.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

            cmd.ExecuteNonQuery();
        }

        // No more connection string
        public static void UpdateCertificate(int certId, string name, string key, double cpdHrs, string provider, DateTime issue, DateTime expiry, string? filePath)
        {
            // Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Using NpgsqlCommand and positional parameters ($1, $2, etc.)
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        UPDATE TrainingCertificates
        SET CertificateName = $1,
            Key = $2,
            HRS = $3,
            Provider = $4,
            IssueDate = $5,
            ExpiryDate = $6,
            FilePath = $7
        WHERE CertificateID = $8";

            cmd.Parameters.AddWithValue(name);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue(cpdHrs);
            cmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.AddWithValue(issue.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue(expiry.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue((object?)filePath ?? DBNull.Value);
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
    }
}