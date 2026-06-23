using Npgsql;
using System;
using System.Data;

namespace EmployeeTrainingTracker
{
    public static class AcknowledgementService
    {
        // 1. Automatically finds the assigned manager for an employee based on their Group
        public static int? GetEmployeeManagerId(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            string sql = @"
                SELECT g.ManagerID 
                FROM GroupMembers gm
                JOIN Groups g ON gm.GroupID = g.GroupID
                WHERE gm.EmployeeID = @empId LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("empId", employeeId);
            var result = cmd.ExecuteScalar();

            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : (int?)null;
        }

        // 2. Employee Submits the Form
        public static void SubmitAcknowledgement(int employeeId, int managerId, string topic, double hours, DateTime trainingDate)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Removed revno from the INSERT statement
            string sql = @"
        INSERT INTO trainingacknowledgements 
        (employeeid, managerid, trainingtopic, hours, trainingdate, status, traineesignedat) 
        VALUES (@empId, @manId, @topic, @hrs, @date, 'Pending Manager', @signedAt)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("empId", employeeId);
            cmd.Parameters.AddWithValue("manId", managerId);
            cmd.Parameters.AddWithValue("topic", topic);
            cmd.Parameters.AddWithValue("hrs", hours);
            cmd.Parameters.AddWithValue("date", trainingDate);
            cmd.Parameters.AddWithValue("signedAt", DateTime.Now);

            cmd.ExecuteNonQuery();
        }

        // 3. Fetch records for the Employee Dashboard
        public static DataTable GetEmployeeAcknowledgements(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
                SELECT 
                    acknowledgementid, 
                    trainingtopic, 
                    revno, 
                    hours, 
                    trainingdate, 
                    status, 
                    traineesignedat, 
                    trainername
                FROM trainingacknowledgements
                WHERE employeeid = @empId
                ORDER BY createdat DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("empId", employeeId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        // 1. Fetch Pending Approvals for the Manager's Dashboard
        public static DataTable GetPendingApprovals(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Bulletproof SQL with explicit table names
            string sql = @"
        SELECT 
            trainingacknowledgements.acknowledgementid, 
            employees.FullName AS employeename,
            trainingacknowledgements.trainingtopic, 
            trainingacknowledgements.hours, 
            trainingacknowledgements.trainingdate, 
            trainingacknowledgements.traineesignedat,
            '' AS trainername
        FROM trainingacknowledgements
        JOIN employees ON trainingacknowledgements.employeeid = employees.employeeid
        WHERE trainingacknowledgements.managerid = @manId AND trainingacknowledgements.status = 'Pending'
        ORDER BY trainingacknowledgements.createdat ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("manId", managerId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        // 2. Manager Approves the Record
        public static void ApproveAcknowledgement(int acknowledgementId, string trainerName)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"
        UPDATE trainingacknowledgements 
        SET status = 'Completed', 
            approvedat = @signedAt,
            trainername = @trainerName
        WHERE acknowledgementid = @ackId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("ackId", acknowledgementId);
            cmd.Parameters.AddWithValue("signedAt", DateTime.Now);
            cmd.Parameters.AddWithValue("trainerName", string.IsNullOrWhiteSpace(trainerName) ? (object)DBNull.Value : trainerName.Trim());

            cmd.ExecuteNonQuery();
        }
    }
}