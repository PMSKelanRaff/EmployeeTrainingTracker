using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql; 
using System.Windows.Forms;

namespace EmployeeTrainingTracker.Utilities
{
    internal class PlannedTrainingService
    {
        // Load all planned training sessions
        public static DataTable GetPlannedTraining(int? employeeId = null)
        {
            string query = @"
            SELECT 
                ts.SessionID,
                ts.CertificateName,
                ts.Key,
                ts.HRS,
                ts.Provider,
                ts.PlannedDate,
                ts.IssueDate,
                ts.ExpiryDate,
                ts.FilePath,
                STRING_AGG(e.FullName, ', ') AS Participants
            FROM TrainingSessions ts
            LEFT JOIN TrainingParticipants tp ON ts.SessionID = tp.SessionID
            LEFT JOIN Employees e ON tp.EmployeeID = e.EmployeeID
            WHERE ts.Status = 'Planned'";

            var parameters = new List<NpgsqlParameter>();

            if (employeeId.HasValue)
            {
                query += " AND tp.EmployeeID = $1";
                parameters.Add(new NpgsqlParameter(null, employeeId.Value));
            }

            query += " GROUP BY ts.SessionID ORDER BY ts.PlannedDate";

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(query, conn);
            if (parameters.Any())
                cmd.Parameters.AddRange(parameters.ToArray());

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);

            return table;
        }

        // Add a new planned training session
        // No connection string
        public static void AddPlannedSession(string certificateName, string key, double? hrs, string provider, DateTime plannedDate, string notes, List<int> employeeIds)
        {
            // Use new DatabaseHelper
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            // Use NpgsqlCommand, positional params, and RETURNING SessionID
            var cmd = new NpgsqlCommand(@"
            INSERT INTO TrainingSessions (CertificateName, Key, HRS, Provider, PlannedDate, Notes)
            VALUES ($1, $2, $3, $4, $5, $6)
            RETURNING SessionID;", conn, tx);

            cmd.Parameters.AddWithValue(certificateName);
            cmd.Parameters.AddWithValue(key ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(hrs ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(provider ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(plannedDate.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue(notes ?? (object)DBNull.Value);

            long sessionId = (long)cmd.ExecuteScalar();

            // Insert participants
            foreach (int empId in employeeIds)
            {
                var partCmd = new NpgsqlCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES ($1, $2)", conn, tx);
                partCmd.Parameters.AddWithValue(sessionId);
                partCmd.Parameters.AddWithValue(empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // Update an existing session
        public static void UpdatePlannedSession(int sessionId, string certificateName, string key, double? hrs, string provider, DateTime plannedDate, string notes, List<int> employeeIds)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            var cmd = new NpgsqlCommand(@"
            UPDATE TrainingSessions 
            SET CertificateName=$1, Key=$2, HRS=$3, Provider=$4, PlannedDate=$5, Notes=$6
            WHERE SessionID=$7", conn, tx);

            cmd.Parameters.AddWithValue(certificateName);
            cmd.Parameters.AddWithValue(key ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(hrs ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(provider ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(plannedDate.Date); // Pass as DateTime
            cmd.Parameters.AddWithValue(notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(sessionId);

            cmd.ExecuteNonQuery();

            // Remove old participants
            var delCmd = new NpgsqlCommand("DELETE FROM TrainingParticipants WHERE SessionID=$1", conn, tx);
            delCmd.Parameters.AddWithValue(sessionId);
            delCmd.ExecuteNonQuery();

            // Add new participants
            foreach (int empId in employeeIds)
            {
                var partCmd = new NpgsqlCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES ($1, $2)", conn, tx);
                partCmd.Parameters.AddWithValue(sessionId);
                partCmd.Parameters.AddWithValue(empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // Delete a planned session
        public static void DeletePlannedSession(int sessionId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            var delParticipants = new NpgsqlCommand("DELETE FROM TrainingParticipants WHERE SessionID=$1", conn, tx);
            delParticipants.Parameters.AddWithValue(sessionId);
            delParticipants.ExecuteNonQuery();

            var delSession = new NpgsqlCommand("DELETE FROM TrainingSessions WHERE SessionID=$1", conn, tx);
            delSession.Parameters.AddWithValue(sessionId);
            delSession.ExecuteNonQuery();

            tx.Commit();
        }

        // Complete a training session
        public static void CompleteTrainingSession(int sessionId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            // --- 1️⃣ Get session info ---
            var sessionCmd = new NpgsqlCommand(@"
                SELECT CertificateName, Key, HRS, Provider, FilePath, IssueDate, ExpiryDate
                FROM TrainingSessions
                WHERE SessionID = $1", conn, tx);
            sessionCmd.Parameters.AddWithValue(sessionId);

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
            var partCmd = new NpgsqlCommand("SELECT EmployeeID FROM TrainingParticipants WHERE SessionID = $1", conn, tx);
            partCmd.Parameters.AddWithValue(sessionId);

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
                var insertCmd = new NpgsqlCommand(@"
            INSERT INTO TrainingCertificates 
                (EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, FilePath)
            VALUES 
                ($1, $2, $3, $4, $5, $6, $7, $8)", conn, tx);

                insertCmd.Parameters.AddWithValue(empId);
                insertCmd.Parameters.AddWithValue(certificateName ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue(string.IsNullOrEmpty(key) ? DBNull.Value : key);
                insertCmd.Parameters.AddWithValue(hrs ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue(string.IsNullOrEmpty(provider) ? DBNull.Value : provider);
                insertCmd.Parameters.AddWithValue(issueDate.Date); // Pass as DateTime
                insertCmd.Parameters.AddWithValue(expiryDate.Date); // Pass as DateTime
                insertCmd.Parameters.AddWithValue(string.IsNullOrEmpty(filePath) ? DBNull.Value : filePath);

                insertCmd.ExecuteNonQuery();
            }

            // --- 4️⃣ Update session status ---
            var updateCmd = new NpgsqlCommand(
                "UPDATE TrainingSessions SET Status = 'Completed' WHERE SessionID = $1", conn, tx);
            updateCmd.Parameters.AddWithValue(sessionId);
            updateCmd.ExecuteNonQuery();

            tx.Commit();

            MessageBox.Show("Training session marked as completed and moved to Certificates.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static List<EmployeeItem> GetAllEmployees()
        {
            var employees = new List<EmployeeItem>();

            string query = "SELECT EmployeeID, FullName FROM Employees ORDER BY FullName";


            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
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

            string query = "SELECT EmployeeID FROM TrainingParticipants WHERE SessionID = $1";

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue(sessionId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt32(0));

            return ids;
        }

        public static void UpdatePlannedSessionParticipants(int sessionId, List<int> employeeIds)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            var delCmd = new NpgsqlCommand("DELETE FROM TrainingParticipants WHERE SessionID=$1", conn, tx);
            delCmd.Parameters.AddWithValue(sessionId);
            delCmd.ExecuteNonQuery();

            // Add new participants
            foreach (var empId in employeeIds)
            {
                var insertCmd = new NpgsqlCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES ($1, $2)", conn, tx);
                insertCmd.Parameters.AddWithValue(sessionId);
                insertCmd.Parameters.AddWithValue(empId);
                insertCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }
}