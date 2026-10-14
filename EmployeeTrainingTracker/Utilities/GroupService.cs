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
        /// <summary>
        /// SQL expression for an employee's real department(s), taken from their group
        /// memberships rather than the old free-text Employees.Department column.
        /// Requires the Employees table to be aliased as "e" in the outer query.
        /// </summary>
        public const string EmployeeDepartmentsSql = @"COALESCE((
                SELECT STRING_AGG(dg.GroupName, ', ' ORDER BY dg.GroupName)
                FROM GroupMembers dgm
                INNER JOIN Groups dg ON dgm.GroupID = dg.GroupID
                WHERE dgm.EmployeeID = e.EmployeeID), 'Unassigned')";

        // Department names for drop-downs
        public static List<string> GetDepartmentNames()
        {
            var names = new List<string>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT GroupName FROM Groups ORDER BY GroupName", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        }

        // The departments (groups) a manager runs - GroupID, GroupName
        public static DataTable GetGroupsByManager(int managerId)
        {
            var dt = new DataTable();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT GroupID, GroupName FROM Groups WHERE ManagerID = $1 ORDER BY GroupName", conn);
            cmd.Parameters.AddWithValue(managerId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        // Comma-separated names of an employee's departments, or "Unassigned"
        public static string GetEmployeeDepartmentNames(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(
                $"SELECT {EmployeeDepartmentsSql} FROM Employees e WHERE e.EmployeeID = $1", conn);
            cmd.Parameters.AddWithValue(employeeId);
            return cmd.ExecuteScalar()?.ToString() ?? "Unassigned";
        }

        // Comma-separated names of the departments (groups) a manager runs
        public static string GetManagedDepartmentNames(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT STRING_AGG(GroupName, ', ' ORDER BY GroupName) FROM Groups WHERE ManagerID = $1", conn);
            cmd.Parameters.AddWithValue(managerId);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? "" : result.ToString()!;
        }

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

            if (managerId.HasValue)
            {
                PromoteToManager(managerId.Value, conn);
            }

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

            // 1. Auto-Promote the user to "Manager" role if they are assigned
            if (managerId.HasValue)
            {
                PromoteToManager(managerId.Value, conn);
            }

            // 2. Update the group (Existing Logic)
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


            string query = $@"
                SELECT e.EmployeeID, e.FullName, {EmployeeDepartmentsSql} AS Department, e.JobTitle
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


            string sql = $@"
                SELECT e.EmployeeID, e.FullName, {EmployeeDepartmentsSql} AS Department, e.JobTitle
                FROM Employees e
                WHERE NOT e.IsArchived
                  AND e.EmployeeID NOT IN (
                    SELECT EmployeeID FROM GroupMembers WHERE GroupID = $1
                )
                ORDER BY e.FullName;";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(groupId);
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        private static void PromoteToManager(int employeeId, NpgsqlConnection conn)
        {
            // Check if they are currently just an "Employee"
            // We don't want to downgrade an Admin, so only update if Role is 'Employee'
            string checkSql = "SELECT Role FROM Users WHERE EmployeeID = $1";
            using (var checkCmd = new NpgsqlCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue(employeeId);
                var currentRole = checkCmd.ExecuteScalar()?.ToString();

                if (currentRole == "Employee")
                {
                    string updateSql = "UPDATE Users SET Role = 'Manager' WHERE EmployeeID = $1";
                    using (var updateCmd = new NpgsqlCommand(updateSql, conn))
                    {
                        updateCmd.Parameters.AddWithValue(employeeId);
                        updateCmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}

