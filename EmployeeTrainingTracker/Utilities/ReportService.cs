using EmployeeTrainingTracker;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public static class ReportService
{
    public static DataTable GenerateReport(string reportType, DateTime? start, DateTime? end, List<int> employeeIds)
    {
        string query = @"
        SELECT 
            tc.CertificateName, 
            tc.IssueDate, 
            tc.ExpiryDate, 
            u.Email AS EmployeeEmail,
            e.FullName,
            -- Manager(s) of the department(s) the employee belongs to (Groups.ManagerID)
            (SELECT STRING_AGG(DISTINCT mu.Email, ', ')
             FROM GroupMembers rgm
             INNER JOIN Groups rg ON rgm.GroupID = rg.GroupID
             INNER JOIN Users mu ON mu.EmployeeID = rg.ManagerID
             WHERE rgm.EmployeeID = e.EmployeeID) AS ManagerEmail
        FROM TrainingCertificates tc
        JOIN Users u ON tc.EmployeeID = u.EmployeeID
        LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
        WHERE 1=1";

        var parameters = new List<NpgsqlParameter>();
        int paramCounter = 1;

        // 🔹 Report type filters
        if (reportType == "Current Year (Valid)")
        {
            query += " AND tc.IssueDate::date <= CURRENT_DATE AND tc.ExpiryDate::date >= CURRENT_DATE";
        }
        else if (reportType == "Out Of Date (Invalid)")
        {
            query += " AND tc.ExpiryDate::date < CURRENT_DATE";
        }
        else if (reportType == "Custom Range (Valid)" && start.HasValue && end.HasValue)
        {
            query += $@"
            AND (
                (tc.IssueDate::date <= ${paramCounter++}) 
                AND (tc.ExpiryDate::date >= ${paramCounter++})
            )
            AND tc.ExpiryDate::date >= CURRENT_DATE";

            parameters.Add(new NpgsqlParameter(null, end.Value.Date));
            parameters.Add(new NpgsqlParameter(null, start.Value.Date));
        }
        else if (reportType == "Custom Range (Invalid)" && start.HasValue && end.HasValue)
        {
            query += $@"
            AND tc.ExpiryDate::date BETWEEN ${paramCounter++} AND ${paramCounter++}
            AND tc.ExpiryDate::date < CURRENT_DATE";

            parameters.Add(new NpgsqlParameter(null, start.Value.Date));
            parameters.Add(new NpgsqlParameter(null, end.Value.Date));
        }

        // 🔹 Employee filter
        if (employeeIds?.Any() == true)
        {
            query += $" AND e.EmployeeID = ANY(${paramCounter++})";
            parameters.Add(new NpgsqlParameter(null, employeeIds.ToArray()));
        }

        query += @"
        GROUP BY  
            tc.CertificateName, 
            tc.IssueDate, 
            tc.ExpiryDate, 
            u.Email,
            e.EmployeeID,
            e.FullName";

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
}