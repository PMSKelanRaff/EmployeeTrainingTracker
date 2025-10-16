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

    public static void UpdateTrainingRecord(int employeeId, string certName, DateTime newIssueDate)
    {
        string fullName = GetEmployeeName(employeeId);
        if (string.IsNullOrEmpty(fullName))
            throw new Exception("Employee name not found.");

        // Match same search logic as AppendTrainingRecord
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
                     ?? throw new Exception("Worksheet not found in Excel file.");

            int lastRow = 7;
            while (!string.IsNullOrWhiteSpace(ws.Cells[lastRow + 1, 1].Text))
            {
                lastRow++;
            }

            bool found = false;
            for (int row = 7; row <= lastRow; row++)
            {
                string existingCert = ws.Cells[row, 3].Text.Trim(); // column C

                // Match ignoring case and "(Edited)" suffix if present
                string cleanExisting = existingCert.Replace("(Edited)", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (string.Equals(cleanExisting, certName, StringComparison.OrdinalIgnoreCase))
                {
                    ws.Cells[row, 1].Value = newIssueDate.ToString("dd/MM/yyyy"); // update date
                    ws.Cells[row, 3].Value = certName + " (Edited)";             // mark edited
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // fallback — append new entry if cert name not found
                int newRow = lastRow + 1;
                ws.Cells[newRow, 1].Value = newIssueDate.ToString("dd/MM/yyyy");
                ws.Cells[newRow, 2].Value = "T";
                ws.Cells[newRow, 3].Value = certName + " (Edited)";
                ws.Cells[newRow, 6].Value = "0";
                ws.Cells[newRow, 7].Value = "KR";
                ws.Cells[newRow, 8].Value = "Auto";
            }

            package.Save();
        }
    }

    public static void DeleteTrainingRecord(int employeeId, string certName)
    {
        string fullName = GetEmployeeName(employeeId);
        if (string.IsNullOrEmpty(fullName))
            throw new Exception("Employee name not found.");

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
                     ?? throw new Exception("Worksheet not found in Excel file.");

            int lastRow = 7;
            while (!string.IsNullOrWhiteSpace(ws.Cells[lastRow + 1, 1].Text))
            {
                lastRow++;
            }

            bool deleted = false;
            for (int row = 7; row <= lastRow; row++)
            {
                string existingCert = ws.Cells[row, 3].Text.Trim(); // column C
                string cleanExisting = existingCert.Replace("(Edited)", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (string.Equals(cleanExisting, certName, StringComparison.OrdinalIgnoreCase))
                {
                    ws.DeleteRow(row);
                    deleted = true;
                    break;
                }
            }

            if (deleted)
                package.Save();
            else
                throw new Exception($"Certificate '{certName}' not found in Excel for {fullName}.");
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
