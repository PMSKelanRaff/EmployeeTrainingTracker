using Npgsql;
using System;
using System.Data;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Add/update/delete logic for employee + user records. This was previously
    /// duplicated almost verbatim between AdminDashboard and ManagerDashboard - the only
    /// real difference was that a Manager adding a new employee also auto-assigns them
    /// to the manager's group, which is handled here via the optional
    /// autoAssignToGroupId parameter.
    /// </summary>
    public static class EmployeeAdminService
    {
        /// <summary>
        /// Looks up the GroupID managed by the given manager (Managers currently manage
        /// exactly one group). Returns null if the manager has no group assigned.
        /// </summary>
        public static int? GetManagerGroupId(int managerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT GroupID FROM Groups WHERE ManagerID = $1 LIMIT 1;", conn);
            cmd.Parameters.AddWithValue(managerId);
            var result = cmd.ExecuteScalar();

            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : (int?)null;
        }

        /// <summary>
        /// Creates a new Employee + linked Users row. When autoAssignToGroupId is
        /// supplied (Manager dashboard), the new employee is also added as a member of
        /// that group.
        /// </summary>
        public static void AddEmployee(string fullName, string email, string role, string department, string jobTitle, string password, int? autoAssignToGroupId = null)
        {
            string passwordHash = SecurityHelper.HashPassword(password);

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            long newEmpId;

            using (var cmdEmp = new NpgsqlCommand(
                "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1, $2, $3) RETURNING EmployeeID;", conn))
            {
                cmdEmp.Parameters.AddWithValue(fullName);
                cmdEmp.Parameters.AddWithValue(department);
                cmdEmp.Parameters.AddWithValue(jobTitle);
                newEmpId = (long)cmdEmp.ExecuteScalar();
            }

            using (var cmdUser = new NpgsqlCommand(
                "INSERT INTO Users (Email, Role, EmployeeID, passwordHash) VALUES ($1, $2, $3, $4) RETURNING UserID;", conn))
            {
                cmdUser.Parameters.AddWithValue(email);
                cmdUser.Parameters.AddWithValue(role);
                cmdUser.Parameters.AddWithValue(newEmpId);
                cmdUser.Parameters.AddWithValue(passwordHash);
                cmdUser.ExecuteScalar();
            }

            if (autoAssignToGroupId.HasValue)
            {
                using var cmdMember = new NpgsqlCommand(
                    "INSERT INTO GroupMembers (GroupID, EmployeeID) VALUES ($1, $2);", conn);
                cmdMember.Parameters.AddWithValue(autoAssignToGroupId.Value);
                cmdMember.Parameters.AddWithValue(newEmpId);
                cmdMember.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates (or, if employeeId is null, creates and links) an employee profile,
        /// updates the linked user's email/role, and optionally updates their password
        /// when newPassword is non-empty.
        /// </summary>
        public static void UpdateEmployee(int userId, int? employeeId, string fullName, string email, string role, string department, string jobTitle, string? newPassword)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            if (employeeId.HasValue)
            {
                using var cmdEmp = new NpgsqlCommand(
                    "UPDATE Employees SET FullName=$1, Department=$2, JobTitle=$3 WHERE EmployeeID=$4", conn);
                cmdEmp.Parameters.AddWithValue(fullName);
                cmdEmp.Parameters.AddWithValue(department);
                cmdEmp.Parameters.AddWithValue(jobTitle);
                cmdEmp.Parameters.AddWithValue(employeeId.Value);
                cmdEmp.ExecuteNonQuery();
            }
            else
            {
                long newEmpId;
                using (var cmdInsertEmp = new NpgsqlCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1,$2,$3) RETURNING EmployeeID;", conn))
                {
                    cmdInsertEmp.Parameters.AddWithValue(fullName);
                    cmdInsertEmp.Parameters.AddWithValue(department);
                    cmdInsertEmp.Parameters.AddWithValue(jobTitle);
                    newEmpId = (long)cmdInsertEmp.ExecuteScalar();
                }

                using var cmdUpdateUserEmp = new NpgsqlCommand(
                    "UPDATE Users SET EmployeeID=$1 WHERE UserID=$2", conn);
                cmdUpdateUserEmp.Parameters.AddWithValue(newEmpId);
                cmdUpdateUserEmp.Parameters.AddWithValue(userId);
                cmdUpdateUserEmp.ExecuteNonQuery();
            }

            using (var cmdUser = new NpgsqlCommand(
                "UPDATE Users SET Email=$1, Role=$2 WHERE UserID=$3", conn))
            {
                cmdUser.Parameters.AddWithValue(email);
                cmdUser.Parameters.AddWithValue(role);
                cmdUser.Parameters.AddWithValue(userId);
                cmdUser.ExecuteNonQuery();
            }

            if (!string.IsNullOrEmpty(newPassword))
            {
                string hashedPassword = SecurityHelper.HashPassword(newPassword);

                using var cmdPass = new NpgsqlCommand(
                    "UPDATE Users SET passwordHash=$1 WHERE UserID=$2", conn);
                cmdPass.Parameters.AddWithValue(hashedPassword);
                cmdPass.Parameters.AddWithValue(userId);
                cmdPass.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes an employee and everything that references them: any group they
        /// manage is unassigned, their certificates and group memberships are removed,
        /// then their user account and employee record are deleted.
        /// </summary>
        public static void DeleteEmployee(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using (var cmdUnassign = new NpgsqlCommand("UPDATE Groups SET ManagerID = NULL WHERE ManagerID = $1", conn))
            {
                cmdUnassign.Parameters.AddWithValue(employeeId);
                cmdUnassign.ExecuteNonQuery();
            }

            using (var cmdCert = new NpgsqlCommand("DELETE FROM TrainingCertificates WHERE EmployeeID=$1", conn))
            {
                cmdCert.Parameters.AddWithValue(employeeId);
                cmdCert.ExecuteNonQuery();
            }

            using (var cmdMembers = new NpgsqlCommand("DELETE FROM GroupMembers WHERE EmployeeID=$1", conn))
            {
                cmdMembers.Parameters.AddWithValue(employeeId);
                cmdMembers.ExecuteNonQuery();
            }

            using (var cmdUser = new NpgsqlCommand("DELETE FROM Users WHERE EmployeeID=$1", conn))
            {
                cmdUser.Parameters.AddWithValue(employeeId);
                cmdUser.ExecuteNonQuery();
            }

            using (var cmdEmp = new NpgsqlCommand("DELETE FROM Employees WHERE EmployeeID=$1", conn))
            {
                cmdEmp.Parameters.AddWithValue(employeeId);
                cmdEmp.ExecuteNonQuery();
            }
        }
    }
}
