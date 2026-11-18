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
        public static int AddEmployee(string fullName, string email, string role, string department, string jobTitle, string password, int? autoAssignToGroupId = null)
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

            return (int)newEmpId;
        }

        /// <summary>
        /// Updates (or, if employeeId is null, creates and links) an employee profile,
        /// updates the linked user's email/role, and optionally updates their password
        /// when newPassword is non-empty.
        /// </summary>
        public static void UpdateEmployee(int userId, int? employeeId, string fullName, string email, string role, string? department, string jobTitle, string? newPassword)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            if (employeeId.HasValue)
            {
                // A null department keeps the existing legacy value (real departments live in GroupMembers)
                using var cmdEmp = new NpgsqlCommand(
                    "UPDATE Employees SET FullName=$1, Department=COALESCE($2, Department), JobTitle=$3 WHERE EmployeeID=$4", conn);
                cmdEmp.Parameters.AddWithValue(fullName);
                cmdEmp.Parameters.Add(new NpgsqlParameter { Value = (object?)department ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
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
                    cmdInsertEmp.Parameters.AddWithValue((object?)department ?? "Unassigned");
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

        public enum DepartmentChangeResult { Unchanged, Moved, MultipleDepartments, NotFound }

        /// <summary>
        /// Puts the employee in the named department (group). An employee in no department is
        /// added to it; an employee in exactly one department is moved. Employees in several
        /// departments are left alone - those memberships are managed on the Departments tab.
        /// </summary>
        public static DepartmentChangeResult SetEmployeeDepartment(int employeeId, string departmentName)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            int? groupId;
            using (var cmdGroup = new NpgsqlCommand("SELECT GroupID FROM Groups WHERE GroupName = $1 LIMIT 1", conn, tx))
            {
                cmdGroup.Parameters.AddWithValue(departmentName);
                var result = cmdGroup.ExecuteScalar();
                groupId = result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
            }
            if (!groupId.HasValue) return DepartmentChangeResult.NotFound;

            var currentGroups = new List<int>();
            using (var cmdCurrent = new NpgsqlCommand("SELECT GroupID FROM GroupMembers WHERE EmployeeID = $1", conn, tx))
            {
                cmdCurrent.Parameters.AddWithValue(employeeId);
                using var reader = cmdCurrent.ExecuteReader();
                while (reader.Read())
                    currentGroups.Add(Convert.ToInt32(reader.GetValue(0)));
            }

            if (currentGroups.Count == 1 && currentGroups[0] == groupId.Value) return DepartmentChangeResult.Unchanged;
            if (currentGroups.Count > 1) return DepartmentChangeResult.MultipleDepartments;

            using (var cmdRemove = new NpgsqlCommand("DELETE FROM GroupMembers WHERE EmployeeID = $1", conn, tx))
            {
                cmdRemove.Parameters.AddWithValue(employeeId);
                cmdRemove.ExecuteNonQuery();
            }
            using (var cmdAdd = new NpgsqlCommand("INSERT INTO GroupMembers (GroupID, EmployeeID) VALUES ($1, $2)", conn, tx))
            {
                cmdAdd.Parameters.AddWithValue(groupId.Value);
                cmdAdd.Parameters.AddWithValue(employeeId);
                cmdAdd.ExecuteNonQuery();
            }

            tx.Commit();
            return DepartmentChangeResult.Moved;
        }

        /// <summary>
        /// "Deletes" an employee by archiving them. Nothing is removed from the database:
        /// the employee, their login, certificates, acknowledgements and completed training
        /// history are all kept. They disappear from the active lists, can no longer log in,
        /// and can be brought back with RestoreEmployee if they return.
        /// Their current department(s) are saved so a restore can put them back.
        /// </summary>
        public static void DeleteEmployee(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            // 1. Snapshot department names into the legacy Department column and archive
            using (var cmdArchive = new NpgsqlCommand($@"
                UPDATE Employees e
                SET Department = {GroupService.EmployeeDepartmentsSql},
                    IsArchived = TRUE,
                    ArchivedAt = CURRENT_TIMESTAMP
                WHERE e.EmployeeID = $1", conn, tx))
            {
                cmdArchive.Parameters.AddWithValue(employeeId);
                cmdArchive.ExecuteNonQuery();
            }

            // 2. A departed employee can't keep managing a department
            using (var cmdUnassign = new NpgsqlCommand("UPDATE Groups SET ManagerID = NULL WHERE ManagerID = $1", conn, tx))
            {
                cmdUnassign.Parameters.AddWithValue(employeeId);
                cmdUnassign.ExecuteNonQuery();
            }

            // 3. Leave their departments (names were saved in step 1)
            using (var cmdMembers = new NpgsqlCommand("DELETE FROM GroupMembers WHERE EmployeeID = $1", conn, tx))
            {
                cmdMembers.Parameters.AddWithValue(employeeId);
                cmdMembers.ExecuteNonQuery();
            }

            // 4. Drop them from training that hasn't happened yet; completed sessions are history and stay
            using (var cmdPlanned = new NpgsqlCommand(@"
                DELETE FROM TrainingParticipants tp
                USING TrainingSessions ts
                WHERE tp.SessionID = ts.SessionID
                  AND tp.EmployeeID = $1
                  AND COALESCE(ts.Status, 'Planned') <> 'Completed'", conn, tx))
            {
                cmdPlanned.Parameters.AddWithValue(employeeId);
                cmdPlanned.ExecuteNonQuery();
            }

            tx.Commit();
        }

        /// <summary>
        /// Brings an archived employee back (e.g. they've returned to the company). Their login
        /// works again and they rejoin whichever of their previous departments still exist.
        /// When intoGroupId is given (Manager dashboard) they join that department instead.
        /// Returns the names of the departments they were put back into.
        /// </summary>
        public static List<string> RestoreEmployee(int employeeId, int? intoGroupId = null)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            string savedDepartments;
            using (var cmdGet = new NpgsqlCommand("SELECT COALESCE(Department, '') FROM Employees WHERE EmployeeID = $1", conn, tx))
            {
                cmdGet.Parameters.AddWithValue(employeeId);
                savedDepartments = cmdGet.ExecuteScalar()?.ToString() ?? "";
            }

            using (var cmdRestore = new NpgsqlCommand(
                "UPDATE Employees SET IsArchived = FALSE, ArchivedAt = NULL WHERE EmployeeID = $1", conn, tx))
            {
                cmdRestore.Parameters.AddWithValue(employeeId);
                cmdRestore.ExecuteNonQuery();
            }

            // Rejoin the given department, or previous departments that still exist
            var rejoined = new List<string>();
            var names = savedDepartments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
            if (intoGroupId.HasValue || names.Length > 0)
            {
                using var cmdRejoin = new NpgsqlCommand(@"
                    INSERT INTO GroupMembers (GroupID, EmployeeID)
                    SELECT g.GroupID, $1 FROM Groups g
                    WHERE ($3::int IS NOT NULL AND g.GroupID = $3)
                       OR ($3::int IS NULL AND g.GroupName = ANY($2))
                    ON CONFLICT DO NOTHING
                    RETURNING (SELECT GroupName FROM Groups WHERE GroupID = GroupMembers.GroupID)", conn, tx);
                cmdRejoin.Parameters.AddWithValue(employeeId);
                cmdRejoin.Parameters.AddWithValue(names);
                cmdRejoin.Parameters.Add(new NpgsqlParameter { Value = (object?)intoGroupId ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
                using var reader = cmdRejoin.ExecuteReader();
                while (reader.Read())
                    rejoined.Add(reader.GetString(0));
            }

            tx.Commit();
            return rejoined;
        }

        // If this email belongs to an archived employee, returns their EmployeeID (for "welcome back" restores)
        public static int? FindArchivedEmployeeByEmail(string email)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT e.EmployeeID FROM Users u
                INNER JOIN Employees e ON u.EmployeeID = e.EmployeeID
                WHERE lower(u.Email) = lower($1) AND e.IsArchived
                LIMIT 1", conn);
            cmd.Parameters.AddWithValue(email);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        public static bool IsEmployeeArchived(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand("SELECT IsArchived FROM Employees WHERE EmployeeID = $1", conn);
            cmd.Parameters.AddWithValue(employeeId);
            return cmd.ExecuteScalar() is bool archived && archived;
        }
    }
}
