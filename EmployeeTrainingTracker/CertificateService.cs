using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker
{
    public static class CertificateService
    {
        public static DataTable GetCertificates(int employeeId)
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(
                    "SELECT CertificateID, CertificateName, IssueDate, ExpiryDate, FilePath FROM TrainingCertificates WHERE EmployeeID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", employeeId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);
                        return table;
                    }
                }
            }
        }

        public static void AddCertificate(int employeeId, string certName, DateTime issueDate, DateTime expiryDate, string? filePath = null)
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(
                    "INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath) " +
                    "VALUES (@eid, @name, @issue, @expiry, @file)", conn))
                {
                    cmd.Parameters.AddWithValue("@eid", employeeId);
                    cmd.Parameters.AddWithValue("@name", certName);
                    cmd.Parameters.AddWithValue("@issue", issueDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@expiry", expiryDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@file", string.IsNullOrEmpty(filePath) ? DBNull.Value : (object)filePath);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateCertificate(int certId, string certName, DateTime issueDate, DateTime expiryDate, string? filePath = null)
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(
                    "UPDATE TrainingCertificates SET CertificateName=@name, IssueDate=@issue, ExpiryDate=@expiry, FilePath=@file WHERE CertificateID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@name", certName);
                    cmd.Parameters.AddWithValue("@issue", issueDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@expiry", expiryDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@file", string.IsNullOrEmpty(filePath) ? DBNull.Value : (object)filePath);
                    cmd.Parameters.AddWithValue("@id", certId);
                    cmd.ExecuteNonQuery();
                }
            }
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
