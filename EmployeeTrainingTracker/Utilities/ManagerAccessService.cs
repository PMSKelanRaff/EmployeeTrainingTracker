using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Database-side ownership checks for the Manager dashboard. A manager may only act on
    /// employees who belong to a group they manage (Groups.ManagerID), and on records
    /// (certificates, users, training sessions) that belong to those employees. The UI grids
    /// are already filtered this way - these checks make sure an action can't reach
    /// another department's data even if a stale or unexpected ID gets through.
    /// </summary>
    public static class ManagerAccessService
    {
        // Reusable sub-query: every EmployeeID the manager is responsible for
        private const string ManagedEmployeesSql = @"
            SELECT gm.EmployeeID
            FROM GroupMembers gm
            INNER JOIN Groups g ON gm.GroupID = g.GroupID
            WHERE g.ManagerID = @managerId";

        // A manager can always manage their own record, plus everyone in their groups
        public static bool CanManageEmployee(int managerId, int employeeId)
        {
            if (employeeId == managerId) return true;

            return Exists($@"
                SELECT EXISTS (
                    SELECT 1 FROM ({ManagedEmployeesSql}) m
                    WHERE m.EmployeeID = @id)", managerId, employeeId);
        }

        // UserID must be linked to an employee the manager is responsible for
        public static bool CanManageUser(int managerId, int userId)
        {
            return Exists($@"
                SELECT EXISTS (
                    SELECT 1 FROM Users u
                    WHERE u.UserID = @id
                      AND (u.EmployeeID = @managerId OR u.EmployeeID IN ({ManagedEmployeesSql})))", managerId, userId);
        }

        public static bool CanManageCertificate(int managerId, int certificateId)
        {
            return Exists($@"
                SELECT EXISTS (
                    SELECT 1 FROM TrainingCertificates tc
                    WHERE tc.CertificateID = @id
                      AND (tc.EmployeeID = @managerId OR tc.EmployeeID IN ({ManagedEmployeesSql})))", managerId, certificateId);
        }

        // Matches GetPlannedTrainingForManager: a session is visible to the manager when at
        // least one of its participants is one of their employees
        public static bool CanManageSession(int managerId, int sessionId)
        {
            return Exists($@"
                SELECT EXISTS (
                    SELECT 1 FROM TrainingParticipants tp
                    WHERE tp.SessionID = @id
                      AND tp.EmployeeID IN ({ManagedEmployeesSql}))", managerId, sessionId);
        }

        /// <summary>
        /// Returns only the IDs from the list that the manager is responsible for.
        /// </summary>
        public static List<int> FilterManagedEmployees(int managerId, IEnumerable<int> employeeIds)
        {
            var requested = employeeIds.Distinct().ToArray();
            if (requested.Length == 0) return new List<int>();

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand($@"
                SELECT DISTINCT m.EmployeeID
                FROM ({ManagedEmployeesSql}) m
                WHERE m.EmployeeID = ANY(@ids)", conn);
            cmd.Parameters.AddWithValue("managerId", managerId);
            cmd.Parameters.AddWithValue("ids", requested);

            var allowed = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                allowed.Add(Convert.ToInt32(reader.GetValue(0)));
            }
            return allowed;
        }

        private static bool Exists(string sql, int managerId, int id)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("managerId", managerId);
            cmd.Parameters.AddWithValue("id", id);

            return cmd.ExecuteScalar() is bool b && b;
        }
    }
}
