using Microsoft.Data.Sqlite;
using System.Data;

public static class ReportService
{

    public static DataTable GenerateReport(string reportType, DateTime? start, DateTime? end, List<int> employeeIds)
    {
        string query = @"
        SELECT 
            tc.CertificateID, 
            tc.CertificateName, 
            tc.IssueDate, 
            tc.ExpiryDate, 
            u.Email AS EmployeeEmail,
            e.FullName,
            m.Email AS ManagerEmail
        FROM TrainingCertificates tc
        JOIN Users u ON tc.EmployeeID = u.EmployeeID
        LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
        LEFT JOIN Managers m ON e.Department = m.Department
        WHERE 1=1";

        var parameters = new List<SqliteParameter>();

        // 🔹 Report type filters
        if (reportType == "Current Year")
        {
            query += " AND date(tc.IssueDate) <= date('now') AND date(tc.ExpiryDate) >= date('now')";
        }
        else if (reportType == "Out Of Date")
        {
            query += " AND date(tc.ExpiryDate) < date('now')";
        }
        else if (reportType == "Custom Range" && start.HasValue && end.HasValue)
        {
            query += " AND date(tc.ExpiryDate) BETWEEN @start AND @end";
            parameters.Add(new SqliteParameter("@start", start.Value.ToString("yyyy-MM-dd")));
            parameters.Add(new SqliteParameter("@end", end.Value.ToString("yyyy-MM-dd")));
        }

        // 🔹 Employee filter
        if (employeeIds?.Any() == true)
        {
            var idParams = string.Join(",", employeeIds.Select((_, i) => $"@emp{i}"));
            query += $" AND e.EmployeeID IN ({idParams})";

            for (int i = 0; i < employeeIds.Count; i++)
                parameters.Add(new SqliteParameter($"@emp{i}", employeeIds[i]));
        }

        // No MIN() needed, grouping only if necessary
        query += @"
        GROUP BY 
            tc.CertificateID, 
            tc.CertificateName, 
            tc.IssueDate, 
            tc.ExpiryDate, 
            u.Email,
            e.FullName,
            m.Email";

        using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);
        if (parameters.Any())
            cmd.Parameters.AddRange(parameters.ToArray());

        using var reader = cmd.ExecuteReader();
        DataTable table = new DataTable();
        table.Load(reader);
        return table;
    }
}