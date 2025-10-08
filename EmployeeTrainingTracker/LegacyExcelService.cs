using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OfficeOpenXml;

public static class LegacyExcelService
{

    private static readonly string RootFolder = @"C:\Users\KelanRafferty\Desktop\Staff Training Certs";

    public static void AppendTrainingRecord(int employeeId, string certName, DateTime issueDate)
    {
        // Get employee name from DB
        string fullName = GetEmployeeName(employeeId);
        if (string.IsNullOrEmpty(fullName))
            throw new Exception("Employee name not found.");

        // Find their Excel file (supports variations like Rev1.1, case insensitivity, fada's etc.)
        string searchPattern = $"{fullName} HTSF13 Training Acknowledgement Record*.xlsx";
        string[] files = Directory.GetFiles(RootFolder, "*.xlsx", SearchOption.AllDirectories);

        string normalizedFullName = fullName.ToLower().Replace("'", "").Trim();

        string? filePath = files.FirstOrDefault(f =>
        {
            string nameOnly = Path.GetFileNameWithoutExtension(f).ToLower().Replace("'", "");
            return nameOnly.Contains(normalizedFullName) && nameOnly.Contains("training acknowledgement record");
        });

        if (filePath == null)
            throw new Exception($"No Excel file found for {fullName}.");
        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            var ws = package.Workbook.Worksheets["Training Acknowledgement Record"]
                     ?? package.Workbook.Worksheets.FirstOrDefault()
                     ?? package.Workbook.Worksheets.Add("Training Acknowledgement Record");

            // Start scanning from row 7 (after the header) to find the last training record
            int lastRow = 7;
            while (!string.IsNullOrWhiteSpace(ws.Cells[lastRow + 1, 1].Text))
            {
                lastRow++;
            }

            int newRow = lastRow + 1;

            ws.Cells[newRow, 1].Value = issueDate.ToString("dd/MM/yyyy");  // Column A: Date
            ws.Cells[newRow, 2].Value = "T";                               // Column B: Training/Retraining key
            ws.Cells[newRow, 3].Value = certName;                          // Column C: Certificate / Procedure

            // Columns D and E are intentionally skipped to align with the template.

            ws.Cells[newRow, 6].Value = "0";                               // Column F: CPD hrs
            ws.Cells[newRow, 7].Value = "KR";                              // Column G: Trainee initials
            ws.Cells[newRow, 8].Value = "Auto";                            // Column H: Trainer / source

            package.Save();
        }

    }

    private static string GetEmployeeName(int employeeId)
    {
        using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT FullName FROM Employees WHERE EmployeeID = @id";
        cmd.Parameters.AddWithValue("@id", employeeId);

        return cmd.ExecuteScalar()?.ToString() ?? "";
    }
}
