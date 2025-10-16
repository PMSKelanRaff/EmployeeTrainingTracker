using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker.Utilities
{
    public static class CertificateService
    {
        public static DataTable GetCertificates(int employeeId)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            using var cmd = new SqliteCommand(@"
                  SELECT CertificateID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath, LastNotifiedDate
                    FROM TrainingCertificates
                    WHERE EmployeeID = @id", conn);

            cmd.Parameters.AddWithValue("@id", employeeId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static void AddCertificate(int employeeId, string certName, string key, double cpdHrs, string provider, DateTime issueDate, DateTime expiryDate, string? filePath = null)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            using var cmd = new SqliteCommand(@"
        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
        VALUES (@eid, @name, @key, @hrs, @provider, @issue, @expiry, @file)", conn);

            cmd.Parameters.AddWithValue("@eid", employeeId);
            cmd.Parameters.AddWithValue("@name", certName);
            cmd.Parameters.AddWithValue("@key", string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue("@hrs", cpdHrs);
            cmd.Parameters.AddWithValue("@provider", string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.AddWithValue("@issue", issueDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@expiry", expiryDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@file", string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

            cmd.ExecuteNonQuery();
        }

        public static void UpdateCertificate(int certId, string name, string key, double cpdHrs, string provider, DateTime issue, DateTime expiry, string? filePath)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        UPDATE TrainingCertificates
        SET CertificateName = @name,
            Key = @key,
            HRS = @hrs,
            Provider = @provider,
            IssueDate = @issue,
            ExpiryDate = @expiry,
            FilePath = @file
        WHERE CertificateID = @id";

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@key", string.IsNullOrEmpty(key) ? DBNull.Value : key);
            cmd.Parameters.AddWithValue("@hrs", cpdHrs);
            cmd.Parameters.AddWithValue("@provider", string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
            cmd.Parameters.AddWithValue("@issue", issue.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@expiry", expiry.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@file", (object?)filePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", certId);

            cmd.ExecuteNonQuery();
        }

        public static void DeleteCertificate(int certId)
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(
                    "DELETE FROM TrainingCertificates WHERE CertificateID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", certId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
