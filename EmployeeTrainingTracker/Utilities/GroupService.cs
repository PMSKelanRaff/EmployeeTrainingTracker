using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker.Utilities
{
    public static class GroupHelper
    {
        
        // GROUP CRUD OPERATIONS
        public static DataTable GetAllGroups(string connectionString)
        {
            var dt = new DataTable();
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string query = @"
                SELECT g.GroupID, g.GroupName, g.Description, 
                       IFNULL(e.FullName, '—') AS ManagerName,
                       g.ManagerID
                FROM Groups g
                LEFT JOIN Employees e ON g.ManagerID = e.EmployeeID
                ORDER BY g.GroupName;";

            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public static void AddGroup(string connectionString, string groupName, string description, int? managerId)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = @"INSERT INTO Groups (GroupName, Description, ManagerID)
                           VALUES (@Name, @Desc, @ManagerID)";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", groupName);
            cmd.Parameters.AddWithValue("@Desc", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ManagerID", managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateGroup(string connectionString, int groupId, string groupName, string description, int? managerId)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = @"UPDATE Groups 
                           SET GroupName=@Name, Description=@Desc, ManagerID=@ManagerID 
                           WHERE GroupID=@ID";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", groupName);
            cmd.Parameters.AddWithValue("@Desc", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ManagerID", managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ID", groupId);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteGroup(string connectionString, int groupId)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = "DELETE FROM Groups WHERE GroupID=@ID";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", groupId);
            cmd.ExecuteNonQuery();
        }


        // GROUP MEMBERSHIP
        public static DataTable GetMembersByGroup(string connectionString, int groupId)
        {
            var dt = new DataTable();
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string query = @"
                SELECT e.EmployeeID, e.FullName, e.Department, e.JobTitle
                FROM GroupMembers gm
                JOIN Employees e ON gm.EmployeeID = e.EmployeeID
                WHERE gm.GroupID = @GroupID
                ORDER BY e.FullName;";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@GroupID", groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public static void AddMemberToGroup(string connectionString, int groupId, int employeeId)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = "INSERT OR IGNORE INTO GroupMembers (GroupID, EmployeeID) VALUES (@GroupID, @EmployeeID)";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GroupID", groupId);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.ExecuteNonQuery();
        }

        public static void RemoveMemberFromGroup(string connectionString, int groupId, int employeeId)
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = "DELETE FROM GroupMembers WHERE GroupID=@GroupID AND EmployeeID=@EmployeeID";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GroupID", groupId);
            cmd.Parameters.AddWithValue("@EmployeeID", employeeId);
            cmd.ExecuteNonQuery();
        }

        public static DataTable GetAvailableEmployeesForGroup(string connectionString, int groupId)
        {
            var dt = new DataTable();
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string sql = @"
                SELECT EmployeeID, FullName, Department, JobTitle
                FROM Employees
                WHERE EmployeeID NOT IN (
                    SELECT EmployeeID FROM GroupMembers WHERE GroupID = @GroupID
                )
                ORDER BY FullName;";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@GroupID", groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }
    }
}

