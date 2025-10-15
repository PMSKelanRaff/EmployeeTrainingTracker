using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker
{
    internal class PlannedTrainingService
    {
        // Load all planned training sessions
        public static DataTable GetPlannedTraining(int? employeeId = null)
        {
            string query = @"
        SELECT ts.SessionID, ts.CertificateName, ts.Key, ts.HRS, ts.Provider,
               ts.PlannedDate, ts.IssueDate, ts.ExpiryDate, ts.FilePath
        FROM TrainingSessions ts
        LEFT JOIN TrainingParticipants tp ON ts.SessionID = tp.SessionID
        WHERE ts.Status = 'Planned'";

            var parameters = new List<SqliteParameter>();

            if (employeeId.HasValue)
            {
                query += " AND tp.EmployeeID = @emp";
                parameters.Add(new SqliteParameter("@emp", employeeId.Value));
            }

            query += " ORDER BY ts.PlannedDate";

            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new SqliteCommand(query, conn);
            if (parameters.Any()) cmd.Parameters.AddRange(parameters.ToArray());

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        // Add a new planned training session
        public static int AddPlannedSession(string certificateName, string key, double? hrs, string provider, DateTime plannedDate, DateTime expiryDate, string filePath, List<int> employeeIds)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            // Insert session with ExpiryDate
            var insertSessionCmd = new SqliteCommand(@"
        INSERT INTO TrainingSessions 
            (CertificateName, Key, HRS, Provider, PlannedDate, ExpiryDate, FilePath)
        VALUES (@cert, @key, @hrs, @prov, @planned, @exp, @path);
        SELECT last_insert_rowid();", conn, tx);

            insertSessionCmd.Parameters.AddWithValue("@cert", certificateName);
            insertSessionCmd.Parameters.AddWithValue("@key", key ?? (object)DBNull.Value);
            insertSessionCmd.Parameters.AddWithValue("@hrs", hrs.HasValue ? hrs.Value : (object)DBNull.Value);
            insertSessionCmd.Parameters.AddWithValue("@prov", provider ?? (object)DBNull.Value);
            insertSessionCmd.Parameters.AddWithValue("@planned", plannedDate.ToString("yyyy-MM-dd"));
            insertSessionCmd.Parameters.AddWithValue("@exp", expiryDate.ToString("yyyy-MM-dd"));
            insertSessionCmd.Parameters.AddWithValue("@path", filePath ?? (object)DBNull.Value);

            long sessionId = (long)insertSessionCmd.ExecuteScalar();

            // Insert participants
            foreach (var empId in employeeIds)
            {
                var partCmd = new SqliteCommand(@"
            INSERT INTO TrainingParticipants (SessionID, EmployeeID)
            VALUES (@sid, @emp)", conn, tx);
                partCmd.Parameters.AddWithValue("@sid", sessionId);
                partCmd.Parameters.AddWithValue("@emp", empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return (int)sessionId;
        }

        // Complete a training session
        public static void CompleteTrainingSession(int sessionId, DateTime issueDate, DateTime expiryDate)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            // Get session info
            var sessionCmd = new SqliteCommand(@"
            SELECT CertificateName, Key, HRS, Provider, FilePath
            FROM TrainingSessions
            WHERE SessionID = @sid", conn, tx);
            sessionCmd.Parameters.AddWithValue("@sid", sessionId);

            using var sessionReader = sessionCmd.ExecuteReader();
            if (!sessionReader.Read())
                throw new Exception("Training session not found.");

            string certificateName = sessionReader["CertificateName"].ToString();
            string key = sessionReader["Key"]?.ToString();
            double? hrs = sessionReader["HRS"] as double?;
            string provider = sessionReader["Provider"]?.ToString();
            string filePath = sessionReader["FilePath"]?.ToString();
            sessionReader.Close();

            // Get participants
            var partCmd = new SqliteCommand("SELECT EmployeeID FROM TrainingParticipants WHERE SessionID = @sid", conn, tx);
            partCmd.Parameters.AddWithValue("@sid", sessionId);
            using var partReader = partCmd.ExecuteReader();

            while (partReader.Read())
            {
                int employeeId = partReader.GetInt32(0);
                var insertCmd = new SqliteCommand(@"
                INSERT INTO TrainingCertificates 
                    (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
                VALUES 
                    (@emp, @cert, @key, @hrs, @prov, @issue, @exp, @path)", conn, tx);

                insertCmd.Parameters.AddWithValue("@emp", employeeId);
                insertCmd.Parameters.AddWithValue("@cert", certificateName);
                insertCmd.Parameters.AddWithValue("@key", key ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@hrs", hrs.HasValue ? hrs.Value : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@prov", provider ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@issue", issueDate.ToString("yyyy-MM-dd"));
                insertCmd.Parameters.AddWithValue("@exp", expiryDate.ToString("yyyy-MM-dd"));
                insertCmd.Parameters.AddWithValue("@path", filePath ?? (object)DBNull.Value);
                insertCmd.ExecuteNonQuery();
            }

            // Mark session completed
            var updateCmd = new SqliteCommand("UPDATE TrainingSessions SET Status = 'Completed' WHERE SessionID = @sid", conn, tx);
            updateCmd.Parameters.AddWithValue("@sid", sessionId);
            updateCmd.ExecuteNonQuery();

            tx.Commit();
        }

        private static void AddParticipants(SqliteConnection conn, SqliteTransaction tx, long sessionId, List<int> employeeIds)
        {
            foreach (var empId in employeeIds)
            {
                var cmd = new SqliteCommand(
                    "INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES (@sid, @emp)",
                    conn, tx);
                cmd.Parameters.AddWithValue("@sid", sessionId);
                cmd.Parameters.AddWithValue("@emp", empId);
                cmd.ExecuteNonQuery();
            }
        }

    }
}
