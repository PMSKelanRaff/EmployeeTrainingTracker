using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql; // CHANGED: From Sqlite to Npgsql

namespace EmployeeTrainingTracker.Utilities
{
    public static class GroupService
    {

        // GROUP CRUD OPERATIONS
        // CHANGED: Removed connectionString parameter
        public static DataTable GetAllGroups()
        {
            var dt = new DataTable();
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL syntax IFNULL replaced with COALESCE
            string query = @"
                SELECT g.GroupID, g.GroupName, g.Description, 
                       COALESCE(e.FullName, '—') AS ManagerName,
                       g.ManagerID
                FROM Groups g
                LEFT JOIN Employees e ON g.ManagerID = e.EmployeeID
                ORDER BY g.GroupName;";

            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        // CHANGED: Removed connectionString parameter
        public static void AddGroup(string groupName, string description, int? managerId)
        {
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @Name to $1, $2 (positional)
            string sql = @"INSERT INTO Groups (GroupName, Description, ManagerID)
                           VALUES ($1, $2, $3)";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupName);
            cmd.Parameters.AddWithValue((object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue(managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        // CHANGED: Removed connectionString parameter
        public static void UpdateGroup(int groupId, string groupName, string description, int? managerId)
        {
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @Name to $1, $2 (positional)
            string sql = @"UPDATE Groups 
                           SET GroupName=$1, Description=$2, ManagerID=$3 
                           WHERE GroupID=$4";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupName);
            cmd.Parameters.AddWithValue((object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue(managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue(groupId);
            cmd.ExecuteNonQuery();
        }

        // CHANGED: Removed connectionString parameter
        public static void DeleteGroup(int groupId)
        {
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @ID to $1 (positional)
            string sql = "DELETE FROM Groups WHERE GroupID=$1";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupId);
            cmd.ExecuteNonQuery();
        }


        // GROUP MEMBERSHIP
        // CHANGED: Removed connectionString parameter
        public static DataTable GetMembersByGroup(int groupId)
        {
            var dt = new DataTable();
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @GroupID to $1 (positional)
            string query = @"
                SELECT e.EmployeeID, e.FullName, e.Department, e.JobTitle
                FROM GroupMembers gm
                JOIN Employees e ON gm.EmployeeID = e.EmployeeID
                WHERE gm.GroupID = $1
                ORDER BY e.FullName;";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(query, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        // CHANGED: Removed connectionString parameter
        public static void AddMemberToGroup(int groupId, int employeeId)
        {
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL syntax from "INSERT OR IGNORE" to "ON CONFLICT DO NOTHING"
            string sql = "INSERT INTO GroupMembers (GroupID, EmployeeID) VALUES ($1, $2) ON CONFLICT DO NOTHING";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupId);
            cmd.Parameters.AddWithValue(employeeId);
            cmd.ExecuteNonQuery();
        }

        // CHANGED: Removed connectionString parameter
        public static void RemoveMemberFromGroup(int groupId, int employeeId)
        {
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @Name to $1, $2 (positional)
            string sql = "DELETE FROM GroupMembers WHERE GroupID=$1 AND EmployeeID=$2";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupId);
            cmd.Parameters.AddWithValue(employeeId);
            cmd.ExecuteNonQuery();
        }

        // CHANGED: Removed connectionString parameter
        public static DataTable GetAvailableEmployeesForGroup(int groupId)
        {
            var dt = new DataTable();
            // CHANGED: Uses new DatabaseHelper.GetConnection()
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // CHANGED: SQL parameters from @GroupID to $1 (positional)
            string sql = @"
                SELECT EmployeeID, FullName, Department, JobTitle
                FROM Employees
                WHERE EmployeeID NOT IN (
                    SELECT EmployeeID FROM GroupMembers WHERE GroupID = $1
                )
                ORDER BY FullName;";
            // CHANGED: Using NpgsqlCommand
            using var cmd = new NpgsqlCommand(sql, conn);
            // CHANGED: Parameters are now positional
            cmd.Parameters.AddWithValue(groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }
    }
}

