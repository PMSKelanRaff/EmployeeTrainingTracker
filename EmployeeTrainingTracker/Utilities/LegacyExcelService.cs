using OfficeOpenXml;
using Npgsql;
using System;
using System.Data;
using System.IO;

namespace EmployeeTrainingTracker
{
    public static class LegacyExcelService
    {

        public static void GenerateHTSF13(int employeeId, string employeeName, string templatePath, string savePath)
        {
            ExcelPackage.License.SetNonCommercialOrganization("Student");

            DataTable completedTraining = GetCompletedTrainingForExcel(employeeId);

            using (var package = new ExcelPackage(new FileInfo(templatePath)))
            {
                var ws = package.Workbook.Worksheets["Training Acknowledgement Record"]
                         ?? package.Workbook.Worksheets.FirstOrDefault();

                if (ws == null) throw new Exception("Could not find a worksheet in the template.");

                ws.Cells["C5"].Value = employeeName;

                int currentRow = 9;
                int maxRow = 48;

                foreach (DataRow row in completedTraining.Rows)
                {
                    if (currentRow > maxRow) break;

                    // 1. SAFELY PARSE DATE (In case the text in the DB is blank or weird)
                    string rawDate = row["IssueDate"]?.ToString() ?? "";
                    string safeDate = DateTime.TryParse(rawDate, out DateTime parsedDate)
                        ? parsedDate.ToString("dd/MM/yyyy")
                        : rawDate; // If it can't read it, just paste whatever text is there

                    // 2. SAFELY EXTRACT STRINGS
                    string key = row["Key"]?.ToString() ?? "T";
                    string certName = row["CertificateName"]?.ToString() ?? "Unknown Training";
                    string hrs = row["HRS"]?.ToString() ?? "0";
                    string trainerSig = row["TrainerName"]?.ToString() ?? "Auto";

                    // 3. SAFELY HANDLE SIGNATURES (Updated: Name only, no date)
                    string traineeSig = "KR"; // Default fallback
                    if (row["Traineesignedat"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Traineesignedat"].ToString()))
                    {
                        traineeSig = employeeName;
                    }

                    // Map to Excel
                    ws.Cells[currentRow, 1].Value = safeDate;
                    ws.Cells[currentRow, 2].Value = key;
                    ws.Cells[currentRow, 3].Value = certName;
                    ws.Cells[currentRow, 6].Value = hrs;
                    ws.Cells[currentRow, 7].Value = traineeSig;
                    ws.Cells[currentRow, 8].Value = trainerSig;

                    currentRow++;
                }

                package.SaveAs(new FileInfo(savePath));
            }
        }

        private static DataTable GetCompletedTrainingForExcel(int employeeId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Fetch approved training, ordered oldest to newest to read like a true logbook
            string sql = @"
                SELECT 
                    IssueDate, 
                    Key, 
                    CertificateName, 
                    HRS, 
                    Traineesignedat, 
                    Trainername
                FROM trainingcertificates
                WHERE EmployeeID = @empId AND status = 'Completed'
                ORDER BY IssueDate ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("empId", employeeId);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        public static DataTable GetEmployeesForExport(int loggedInEmployeeId, bool isAdmin)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string sql;

            if (isAdmin)
            {
                // Admins just grab absolutely everyone in the company
                sql = "SELECT employeeid, fullname FROM employees ORDER BY fullname ASC";
            }
            else
            {
                // Managers only grab employees who belong to a group that they manage
                sql = @"
            SELECT DISTINCT e.employeeid, e.fullname 
            FROM employees e
            JOIN groupmembers gm ON e.employeeid = gm.employeeid
            JOIN groups g ON gm.groupid = g.groupid
            WHERE g.managerid = @managerId
            ORDER BY e.fullname ASC";
            }

            using var cmd = new NpgsqlCommand(sql, conn);

            if (!isAdmin)
            {
                // We pass in the logged-in manager's EmployeeID here
                cmd.Parameters.AddWithValue("managerId", loggedInEmployeeId);
            }

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }
    }
}