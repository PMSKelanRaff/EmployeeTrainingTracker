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
        public static DataTable GetPlannedTraining(int? employeeId = null, string department = null)
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
                ts.Status,
                ts.Notes,
                STRING_AGG(e.FullName, ', ') AS Participants
            FROM TrainingSessions ts
            LEFT JOIN TrainingParticipants tp ON ts.SessionID = tp.SessionID
            LEFT JOIN Employees e ON tp.EmployeeID = e.EmployeeID
            WHERE 1=1 "; // Use 1=1 to make appending AND clauses easier

            var parameters = new List<NpgsqlParameter>();

            if (!string.IsNullOrEmpty(department))
            {
                // Filter: Only show sessions where at least one participant is in the department
                // OR checks if the session is intended for that department context
                query += " AND e.Department = $1";
                parameters.Add(new NpgsqlParameter(null, department));
            }

            // If looking for specific employee (Admin/Profile view)
            if (employeeId.HasValue)
            {
                query += " AND tp.EmployeeID = $2"; // Note: Index might need adjustment based on if department is present
                parameters.Add(new NpgsqlParameter(null, employeeId.Value));
            }

            
            query += " AND (ts.Status = 'Planned' OR ts.Status = 'Completed')";

            query += " GROUP BY ts.SessionID, ts.PlannedDate, ts.CertificateName, ts.Key, ts.HRS, ts.Provider, ts.IssueDate, ts.ExpiryDate, ts.FilePath, ts.Status, ts.Notes ORDER BY ts.PlannedDate";


            int paramIndex = 1;
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Replace("$1", $"${paramIndex++}");
                if (employeeId.HasValue) query = query.Replace("$2", $"${paramIndex++}");
            }
            else if (employeeId.HasValue)
            {
                query = query.Replace("$2", $"${paramIndex++}"); // In case $1 wasn't used
            }


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
            cmd.Parameters.Add(DatabaseHelper.DateParam(plannedDate)); // date only
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
            cmd.Parameters.Add(DatabaseHelper.DateParam(plannedDate)); // date only
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
            // FOR UPDATE locks the row so two clicks / two users can't both complete it at once
            var sessionCmd = new NpgsqlCommand(@"
                SELECT CertificateName, Key, HRS, Provider, FilePath, IssueDate, ExpiryDate, Status
                FROM TrainingSessions
                WHERE SessionID = $1
                FOR UPDATE", conn, tx);
            sessionCmd.Parameters.AddWithValue(sessionId);

            using var sessionReader = sessionCmd.ExecuteReader();
            if (!sessionReader.Read())
            {
                MessageBox.Show("Training session not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Already completed - certificates were created the first time, so don't create duplicates
            if (string.Equals(sessionReader["Status"]?.ToString()?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("This training session has already been completed. Certificates were created at that time.",
                    "Already Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string certificateName = sessionReader["CertificateName"]?.ToString();
            string key = sessionReader["Key"]?.ToString();
            double? hrs = sessionReader["HRS"] == DBNull.Value ? null : Convert.ToDouble(sessionReader["HRS"]);
            string provider = sessionReader["Provider"]?.ToString();
            string filePath = sessionReader["FilePath"]?.ToString();

            DateTime issueDate = sessionReader["IssueDate"] != DBNull.Value
                ? Convert.ToDateTime(sessionReader["IssueDate"])
                : DateTime.Today;

            DateTime expiryDate = sessionReader["ExpiryDate"] != DBNull.Value
                ? Convert.ToDateTime(sessionReader["ExpiryDate"])
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
                insertCmd.Parameters.Add(DatabaseHelper.DateParam(issueDate)); // date only
                insertCmd.Parameters.Add(DatabaseHelper.DateParam(expiryDate)); // date only
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

            string query = "SELECT EmployeeID, FullName FROM Employees WHERE NOT IsArchived ORDER BY FullName";


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

        public static List<EmployeeItem> GetEmployeesByDepartment(string department)
        {
            var employees = new List<EmployeeItem>();
            string query = "SELECT EmployeeID, FullName FROM Employees WHERE Department = $1 AND NOT IsArchived ORDER BY FullName";

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue(department);

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

        // Fetch employees based on the Groups and GroupMembers tables
        public static List<EmployeeItem> GetEmployeesByManager(int managerId)
        {
            var list = new List<EmployeeItem>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
                SELECT DISTINCT e.EmployeeID, e.FullName 
                FROM Employees e
                INNER JOIN GroupMembers gm ON e.EmployeeID = gm.EmployeeID
                INNER JOIN Groups g ON gm.GroupID = g.GroupID
                WHERE g.ManagerID = $1
                ORDER BY e.FullName";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(managerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EmployeeItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return list;
        }

        // Fetch planned training sessions restricted to the manager's group members
        public static DataTable GetPlannedTrainingForManager(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
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
                    ts.Status,
                    ts.Notes,
                    STRING_AGG(DISTINCT e.FullName, ', ') AS Participants,
                    -- Shared sessions: how many participants belong to other managers' groups (names stay hidden)
                    (SELECT COUNT(*) FROM TrainingParticipants tpo
                     WHERE tpo.SessionID = ts.SessionID
                       AND tpo.EmployeeID NOT IN (
                           SELECT gmo.EmployeeID FROM GroupMembers gmo
                           INNER JOIN Groups gro ON gmo.GroupID = gro.GroupID
                           WHERE gro.ManagerID = $1)) AS OtherGroupParticipants
                FROM TrainingSessions ts
                INNER JOIN TrainingParticipants tp ON ts.SessionID = tp.SessionID
                INNER JOIN Employees e ON tp.EmployeeID = e.EmployeeID
                INNER JOIN GroupMembers gm ON e.EmployeeID = gm.EmployeeID
                INNER JOIN Groups g ON gm.GroupID = g.GroupID
                WHERE g.ManagerID = $1
                  AND (ts.Status = 'Planned' OR ts.Status = 'Completed')
                GROUP BY 
                    ts.SessionID, ts.CertificateName, ts.Key, ts.HRS, ts.Provider, 
                    ts.PlannedDate, ts.IssueDate, ts.ExpiryDate, ts.FilePath, ts.Status, ts.Notes
                ORDER BY ts.PlannedDate;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(managerId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);

            return table;
        }

        // ---------------------------------------------------------------------------------
        // Shared sessions (Manager dashboard)
        // A session can contain employees from several managers' groups. These methods only
        // ever add, remove or move the calling manager's own participants - everyone else in
        // the session is left exactly as it was.
        // ---------------------------------------------------------------------------------

        private const string ManagedEmployeesSql = @"
            SELECT gm.EmployeeID FROM GroupMembers gm
            INNER JOIN Groups g ON gm.GroupID = g.GroupID
            WHERE g.ManagerID = @managerId";

        // Number of participants in the session who are NOT in this manager's groups
        public static int GetOtherParticipantCount(int managerId, int sessionId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            return CountOtherParticipants(conn, null, managerId, sessionId);
        }

        /// <summary>
        /// Edits a session from the Manager dashboard. The manager's own participants are
        /// replaced with employeeIds; other managers' participants are always kept.
        /// When splitFromShared is true, the manager's employees are moved into a new copy of
        /// the session and the new details apply only to that copy - the original session is
        /// left untouched for everyone else. Returns the SessionID that was updated.
        /// </summary>
        public static int UpdatePlannedSessionForManager(int managerId, int sessionId, string certificateName, string key, double? hrs, string provider, DateTime plannedDate, string notes, List<int> employeeIds, bool splitFromShared)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            int targetId = sessionId;
            if (splitFromShared)
            {
                targetId = CloneSession(conn, tx, sessionId);
                MoveManagerParticipants(conn, tx, managerId, sessionId, targetId);
            }

            var cmd = new NpgsqlCommand(@"
            UPDATE TrainingSessions
            SET CertificateName=$1, Key=$2, HRS=$3, Provider=$4, PlannedDate=$5, Notes=$6
            WHERE SessionID=$7", conn, tx);

            cmd.Parameters.AddWithValue(certificateName);
            cmd.Parameters.AddWithValue(key ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(hrs ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(provider ?? (object)DBNull.Value);
            cmd.Parameters.Add(DatabaseHelper.DateParam(plannedDate)); // date only
            cmd.Parameters.AddWithValue(notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue(targetId);
            cmd.ExecuteNonQuery();

            // Swap out only this manager's participants
            RemoveManagerParticipants(conn, tx, managerId, targetId);
            foreach (int empId in employeeIds.Distinct())
            {
                var partCmd = new NpgsqlCommand("INSERT INTO TrainingParticipants (SessionID, EmployeeID) VALUES ($1, $2)", conn, tx);
                partCmd.Parameters.AddWithValue(targetId);
                partCmd.Parameters.AddWithValue(empId);
                partCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return targetId;
        }

        /// <summary>
        /// Removes the manager's employees from the session. The session itself is only deleted
        /// when nobody else is left in it. Returns true if the whole session was deleted.
        /// </summary>
        public static bool DeletePlannedSessionForManager(int managerId, int sessionId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            RemoveManagerParticipants(conn, tx, managerId, sessionId);

            var countCmd = new NpgsqlCommand("SELECT COUNT(*) FROM TrainingParticipants WHERE SessionID = $1", conn, tx);
            countCmd.Parameters.AddWithValue(sessionId);
            bool sessionEmpty = Convert.ToInt64(countCmd.ExecuteScalar()) == 0;

            if (sessionEmpty)
            {
                var delSession = new NpgsqlCommand("DELETE FROM TrainingSessions WHERE SessionID=$1", conn, tx);
                delSession.Parameters.AddWithValue(sessionId);
                delSession.ExecuteNonQuery();
            }

            tx.Commit();
            return sessionEmpty;
        }

        /// <summary>
        /// Completes the session for the manager's employees only. If other managers' employees
        /// are also in the session, the manager's employees are first moved into their own copy,
        /// which is then completed - the original stays Planned for everyone else.
        /// </summary>
        public static void CompleteTrainingSessionForManager(int managerId, int sessionId)
        {
            int targetId = sessionId;

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();

                // Check before splitting, otherwise a copy of an already-completed session would be left behind
                if (IsSessionCompleted(conn, tx, sessionId))
                {
                    MessageBox.Show("This training session has already been completed. Certificates were created at that time.",
                        "Already Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (CountOtherParticipants(conn, tx, managerId, sessionId) > 0)
                {
                    targetId = CloneSession(conn, tx, sessionId);
                    MoveManagerParticipants(conn, tx, managerId, sessionId, targetId);
                }

                tx.Commit();
            }

            CompleteTrainingSession(targetId);
        }

        private static bool IsSessionCompleted(NpgsqlConnection conn, NpgsqlTransaction tx, int sessionId)
        {
            using var cmd = new NpgsqlCommand("SELECT Status FROM TrainingSessions WHERE SessionID = @sessionId FOR UPDATE", conn, tx);
            cmd.Parameters.AddWithValue("sessionId", sessionId);
            return string.Equals(cmd.ExecuteScalar()?.ToString()?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountOtherParticipants(NpgsqlConnection conn, NpgsqlTransaction? tx, int managerId, int sessionId)
        {
            using var cmd = new NpgsqlCommand($@"
                SELECT COUNT(*) FROM TrainingParticipants
                WHERE SessionID = @sessionId
                  AND EmployeeID NOT IN ({ManagedEmployeesSql})", conn, tx);
            cmd.Parameters.AddWithValue("sessionId", sessionId);
            cmd.Parameters.AddWithValue("managerId", managerId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // Copies a session's details (no participants) and returns the new SessionID
        private static int CloneSession(NpgsqlConnection conn, NpgsqlTransaction tx, int sessionId)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO TrainingSessions (CertificateName, Key, HRS, Provider, PlannedDate, Notes, IssueDate, ExpiryDate, FilePath, Status)
                SELECT CertificateName, Key, HRS, Provider, PlannedDate, Notes, IssueDate, ExpiryDate, FilePath, Status
                FROM TrainingSessions WHERE SessionID = @sessionId
                RETURNING SessionID", conn, tx);
            cmd.Parameters.AddWithValue("sessionId", sessionId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void MoveManagerParticipants(NpgsqlConnection conn, NpgsqlTransaction tx, int managerId, int fromSessionId, int toSessionId)
        {
            using var cmd = new NpgsqlCommand($@"
                UPDATE TrainingParticipants SET SessionID = @toId
                WHERE SessionID = @fromId
                  AND EmployeeID IN ({ManagedEmployeesSql})", conn, tx);
            cmd.Parameters.AddWithValue("toId", toSessionId);
            cmd.Parameters.AddWithValue("fromId", fromSessionId);
            cmd.Parameters.AddWithValue("managerId", managerId);
            cmd.ExecuteNonQuery();
        }

        private static void RemoveManagerParticipants(NpgsqlConnection conn, NpgsqlTransaction tx, int managerId, int sessionId)
        {
            using var cmd = new NpgsqlCommand($@"
                DELETE FROM TrainingParticipants
                WHERE SessionID = @sessionId
                  AND EmployeeID IN ({ManagedEmployeesSql})", conn, tx);
            cmd.Parameters.AddWithValue("sessionId", sessionId);
            cmd.Parameters.AddWithValue("managerId", managerId);
            cmd.ExecuteNonQuery();
        }
    }
}