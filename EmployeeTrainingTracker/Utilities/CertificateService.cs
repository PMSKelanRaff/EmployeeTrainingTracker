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
    }
}