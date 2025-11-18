using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace EmployeeTrainingTracker.Utilities
{
    public static class GroupService
    {

        // GROUP CRUD OPERATIONS
        public static DataTable GetAllGroups()
        {
            var dt = new DataTable();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string query = @"
                SELECT g.GroupID, g.GroupName, g.Description, 
                       COALESCE(e.FullName, '—') AS ManagerName,
                       g.ManagerID
                FROM Groups g
                LEFT JOIN Employees e ON g.ManagerID = e.EmployeeID
                ORDER BY g.GroupName;";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public static void AddGroup(string groupName, string description, int? managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql = @"INSERT INTO Groups (GroupName, Description, ManagerID)
                           VALUES ($1, $2, $3)";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupName);
            cmd.Parameters.AddWithValue((object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue(managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateGroup(int groupId, string groupName, string description, int? managerId)
        {

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string sql = @"UPDATE Groups 
                           SET GroupName=$1, Description=$2, ManagerID=$3 
                           WHERE GroupID=$4";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupName);
            cmd.Parameters.AddWithValue((object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue(managerId.HasValue ? (object)managerId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue(groupId);
            cmd.ExecuteNonQuery();
        }
      
        public static void DeleteGroup(int groupId)
        {
            
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string sql = "DELETE FROM Groups WHERE GroupID=$1";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupId);
            cmd.ExecuteNonQuery();
        }


        // GROUP MEMBERSHIP
        public static DataTable GetMembersByGroup(int groupId)
        {
            var dt = new DataTable();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string query = @"
                SELECT e.EmployeeID, e.FullName, e.Department, e.JobTitle
                FROM GroupMembers gm
                JOIN Employees e ON gm.EmployeeID = e.EmployeeID
                WHERE gm.GroupID = $1
                ORDER BY e.FullName;";

            using var cmd = new NpgsqlCommand(query, conn);

            cmd.Parameters.AddWithValue(groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public static void AddMemberToGroup(int groupId, int employeeId)
        {

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string sql = "INSERT INTO GroupMembers (GroupID, EmployeeID) VALUES ($1, $2) ON CONFLICT DO NOTHING";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupId);
            cmd.Parameters.AddWithValue(employeeId);
            cmd.ExecuteNonQuery();
        }

        public static void RemoveMemberFromGroup(int groupId, int employeeId)
        {

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string sql = "DELETE FROM GroupMembers WHERE GroupID=$1 AND EmployeeID=$2";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupId);
            cmd.Parameters.AddWithValue(employeeId);
            cmd.ExecuteNonQuery();
        }

        public static DataTable GetAvailableEmployeesForGroup(int groupId)
        {
            var dt = new DataTable();

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();


            string sql = @"
                SELECT EmployeeID, FullName, Department, JobTitle
                FROM Employees
                WHERE EmployeeID NOT IN (
                    SELECT EmployeeID FROM GroupMembers WHERE GroupID = $1
                )
                ORDER BY FullName;";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }
    }
}

