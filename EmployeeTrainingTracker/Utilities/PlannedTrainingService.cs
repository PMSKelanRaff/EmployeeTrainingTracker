using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker.Utilities
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
        public static void AddPlannedSession(string certificateName, string key, double? hrs, string provider, DateTime plannedDate, string notes, List<int> employeeIds)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            var cmd = new SqliteCommand(@"
            INSERT INTO TrainingSessions (CertificateName, Key, HRS, Provider, PlannedDate, Notes)
            VALUES (@cert, @key, @hrs, @prov, @planned, @notes);
            SELECT last_insert_rowid();", conn, tx);

            cmd.Parameters.AddWithValue("@cert", certificateName);
            cmd.Parameters.AddWithValue("@key", key ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hrs", hrs ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@prov", provider ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@planned", plannedDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);

            long sessionId = (long)cmd.ExecuteScalar();

            // Insert participants
            foreach (int empId in employeeIds)
            {
                var partCmd = new SqliteCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES (@sid, @eid)", conn, tx);
                partCmd.Parameters.AddWithValue("@sid", sessionId);
                partCmd.Parameters.AddWithValue("@eid", empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // Update an existing session
        public static void UpdatePlannedSession(int sessionId, string certificateName, string key, double? hrs, string provider, DateTime plannedDate, string notes, List<int> employeeIds)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            var cmd = new SqliteCommand(@"
            UPDATE TrainingSessions 
            SET CertificateName=@cert, Key=@key, HRS=@hrs, Provider=@prov, PlannedDate=@planned, Notes=@notes
            WHERE SessionID=@sid", conn, tx);

            cmd.Parameters.AddWithValue("@cert", certificateName);
            cmd.Parameters.AddWithValue("@key", key ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hrs", hrs ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@prov", provider ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@planned", plannedDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sid", sessionId);

            cmd.ExecuteNonQuery();

            // Remove old participants
            var delCmd = new SqliteCommand("DELETE FROM TrainingParticipants WHERE SessionID=@sid", conn, tx);
            delCmd.Parameters.AddWithValue("@sid", sessionId);
            delCmd.ExecuteNonQuery();

            // Add new participants
            foreach (int empId in employeeIds)
            {
                var partCmd = new SqliteCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES (@sid, @eid)", conn, tx);
                partCmd.Parameters.AddWithValue("@sid", sessionId);
                partCmd.Parameters.AddWithValue("@eid", empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // Delete a planned session
        public static void DeletePlannedSession(int sessionId)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            var delParticipants = new SqliteCommand("DELETE FROM TrainingParticipants WHERE SessionID=@sid", conn, tx);
            delParticipants.Parameters.AddWithValue("@sid", sessionId);
            delParticipants.ExecuteNonQuery();

            var delSession = new SqliteCommand("DELETE FROM TrainingSessions WHERE SessionID=@sid", conn, tx);
            delSession.Parameters.AddWithValue("@sid", sessionId);
            delSession.ExecuteNonQuery();

            tx.Commit();
        }

        // Complete a training session
        public static void CompleteTrainingSession(int sessionId)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            // --- 1️⃣ Get session info ---
            var sessionCmd = new SqliteCommand(@"
        SELECT CertificateName, Key, HRS, Provider, FilePath, IssueDate, ExpiryDate
        FROM TrainingSessions
        WHERE SessionID = @sid", conn, tx);
            sessionCmd.Parameters.AddWithValue("@sid", sessionId);

            using var sessionReader = sessionCmd.ExecuteReader();
            if (!sessionReader.Read())
            {
                MessageBox.Show("Training session not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string certificateName = sessionReader["CertificateName"]?.ToString();
            string key = sessionReader["Key"]?.ToString();
            double? hrs = sessionReader["HRS"] == DBNull.Value ? null : Convert.ToDouble(sessionReader["HRS"]);
            string provider = sessionReader["Provider"]?.ToString();
            string filePath = sessionReader["FilePath"]?.ToString();

            DateTime issueDate = sessionReader["IssueDate"] != DBNull.Value
                ? DateTime.Parse(sessionReader["IssueDate"].ToString())
                : DateTime.Today;

            DateTime expiryDate = sessionReader["ExpiryDate"] != DBNull.Value
                ? DateTime.Parse(sessionReader["ExpiryDate"].ToString())
                : issueDate.AddYears(1); // fallback to 1 year if missing

            sessionReader.Close();

            // --- 2️⃣ Get participants ---
            var partCmd = new SqliteCommand("SELECT EmployeeID FROM TrainingParticipants WHERE SessionID = @sid", conn, tx);
            partCmd.Parameters.AddWithValue("@sid", sessionId);

            List<int> participants = new();
            using (var pr = partCmd.ExecuteReader())
            {
                while (pr.Read())
                    participants.Add(pr.GetInt32(0));
            }

            if (participants.Count == 0)
            {
                MessageBox.Show("No participants found for this training session.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // --- 3️⃣ Insert into TrainingCertificates ---
            foreach (int empId in participants)
            {
                var insertCmd = new SqliteCommand(@"
            INSERT INTO TrainingCertificates 
                (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
            VALUES 
                (@emp, @cert, @key, @hrs, @prov, @issue, @exp, @path)", conn, tx);

                insertCmd.Parameters.AddWithValue("@emp", empId);
                insertCmd.Parameters.AddWithValue("@cert", certificateName ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@key", string.IsNullOrEmpty(key) ? DBNull.Value : key);
                insertCmd.Parameters.AddWithValue("@hrs", hrs ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@prov", string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
                insertCmd.Parameters.AddWithValue("@issue", issueDate.ToString("yyyy-MM-dd"));
                insertCmd.Parameters.AddWithValue("@exp", expiryDate.ToString("yyyy-MM-dd"));
                insertCmd.Parameters.AddWithValue("@path", string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

                insertCmd.ExecuteNonQuery();
            }

            // --- 4️⃣ Update session status ---
            var updateCmd = new SqliteCommand(
                "UPDATE TrainingSessions SET Status = 'Completed' WHERE SessionID = @sid", conn, tx);
            updateCmd.Parameters.AddWithValue("@sid", sessionId);
            updateCmd.ExecuteNonQuery();

            tx.Commit();

            MessageBox.Show("Training session marked as completed and moved to Certificates.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static List<EmployeeItem> GetAllEmployees()
        {
            var employees = new List<EmployeeItem>();

            string query = "SELECT EmployeeID, FullName FROM Employees ORDER BY FullName";

            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                employees.Add(new EmployeeItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            return employees;
        }

        public static List<int> GetPlannedEmployeeIds(int sessionId)
        {
            var ids = new List<int>();

            string query = "SELECT EmployeeID FROM TrainingParticipants WHERE SessionID = @sid";

            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@sid", sessionId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt32(0));

            return ids;
        }

        public static void UpdatePlannedSessionParticipants(int sessionId, List<int> employeeIds)
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            // Remove old participants
            var delCmd = new SqliteCommand("DELETE FROM TrainingParticipants WHERE SessionID=@sid", conn, tx);
            delCmd.Parameters.AddWithValue("@sid", sessionId);
            delCmd.ExecuteNonQuery();

            // Add new participants
            foreach (var empId in employeeIds)
            {
                var insertCmd = new SqliteCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES (@sid, @eid)", conn, tx);
                insertCmd.Parameters.AddWithValue("@sid", sessionId);
                insertCmd.Parameters.AddWithValue("@eid", empId);
                insertCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }
}

