// Standalone export tool - NOT part of the main WinForms app build.
//
// Run it from this folder with:
//     dotnet run
//
// It connects to the same Postgres database as the main app (reading the
// same appsettings.json - it walks up the folder tree to find it, so it
// works no matter what Debug/Release output folder .NET puts it in) and
// writes a two-sheet Excel workbook:
//   1. Employees - Full Name, Email, Role, Department, Job Title
//   2. Groups    - Group Name, Description, Manager, Members, Member Count
//
// The output lands in ..\..\Exports\ (next to appsettings.json) named
// Employee_Group_Export_<date>.xlsx.

using System.Drawing;
using System.Text.Json;
using Npgsql;
using OfficeOpenXml;
using OfficeOpenXml.Style;

ExcelPackage.License.SetNonCommercialPersonal("Kelan Rafferty");

string? settingsPath = FindAppSettings();
if (settingsPath == null)
{
    Console.WriteLine("Could not find appsettings.json anywhere above this folder.");
    Console.WriteLine("Run this tool from inside the EmployeeTrainingTracker project tree.");
    return 1;
}

Console.WriteLine($"Using config: {settingsPath}");

using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
var dbSection = doc.RootElement.GetProperty("Database");
string host = dbSection.GetProperty("Host").GetString()!;
int port = int.Parse(dbSection.GetProperty("Port").GetString() ?? "5432");
string name = dbSection.GetProperty("Name").GetString()!;
string user = dbSection.GetProperty("User").GetString()!;
string pass = dbSection.GetProperty("Pass").GetString()!;

var csb = new NpgsqlConnectionStringBuilder
{
    Host = host,
    Port = port,
    Database = name,
    Username = user,
    Password = pass,
    SslMode = SslMode.Require,
    TrustServerCertificate = true
};

Console.WriteLine($"Connecting to {host}:{port}/{name} ...");

using var conn = new NpgsqlConnection(csb.ConnectionString);
conn.Open();

using var package = new ExcelPackage();

// ---------------------------------------------------------------------
// Sheet 1: Employees (same columns/order as the Admin dashboard's grid)
// ---------------------------------------------------------------------
var wsEmp = package.Workbook.Worksheets.Add("Employees");
string[] empHeaders = { "Full Name", "Email", "Role", "Department", "Job Title" };
for (int i = 0; i < empHeaders.Length; i++)
    wsEmp.Cells[1, i + 1].Value = empHeaders[i];

int empRowCount = 0;
using (var cmd = new NpgsqlCommand(@"
    SELECT
        COALESCE(e.FullName, u.Email) AS FullName,
        u.Email AS Email,
        u.Role,
        COALESCE(e.Department, 'Unknown') AS Department,
        COALESCE(e.JobTitle, 'Unknown') AS JobTitle
    FROM Users u
    LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
    ORDER BY FullName ASC", conn))
using (var reader = cmd.ExecuteReader())
{
    int row = 2;
    while (reader.Read())
    {
        for (int c = 0; c < empHeaders.Length; c++)
            wsEmp.Cells[row, c + 1].Value = reader.IsDBNull(c) ? "" : reader.GetValue(c).ToString();
        row++;
        empRowCount++;
    }
}
StyleSheet(wsEmp, empHeaders.Length);

// ---------------------------------------------------------------------
// Sheet 2: Groups (Departments) with members and managers
// ---------------------------------------------------------------------
var wsGrp = package.Workbook.Worksheets.Add("Groups");
string[] grpHeaders = { "Department", "Description", "Manager", "Members", "Member Count" };
for (int i = 0; i < grpHeaders.Length; i++)
    wsGrp.Cells[1, i + 1].Value = grpHeaders[i];

int grpRowCount = 0;
using (var cmd = new NpgsqlCommand(@"
    SELECT
        g.GroupName,
        COALESCE(g.Description, '') AS Description,
        COALESCE(mgr.FullName, 'Unassigned') AS ManagerName,
        STRING_AGG(DISTINCT mem.FullName, ', ' ORDER BY mem.FullName) AS Members,
        COUNT(DISTINCT gm.EmployeeID) AS MemberCount
    FROM Groups g
    LEFT JOIN Employees mgr ON g.ManagerID = mgr.EmployeeID
    LEFT JOIN GroupMembers gm ON g.GroupID = gm.GroupID
    LEFT JOIN Employees mem ON gm.EmployeeID = mem.EmployeeID
    GROUP BY g.GroupID, g.GroupName, g.Description, mgr.FullName
    ORDER BY g.GroupName ASC", conn))
using (var reader = cmd.ExecuteReader())
{
    int row = 2;
    while (reader.Read())
    {
        wsGrp.Cells[row, 1].Value = reader.GetString(0);
        wsGrp.Cells[row, 2].Value = reader.IsDBNull(1) ? "" : reader.GetString(1);
        wsGrp.Cells[row, 3].Value = reader.GetString(2);
        wsGrp.Cells[row, 4].Value = reader.IsDBNull(3) ? "(no members)" : reader.GetString(3);
        wsGrp.Cells[row, 5].Value = reader.GetInt32(4);
        row++;
        grpRowCount++;
    }
}
StyleSheet(wsGrp, grpHeaders.Length);

string mainProjectDir = Path.GetDirectoryName(settingsPath)!;
string outDir = Path.Combine(mainProjectDir, "Exports");
Directory.CreateDirectory(outDir);
string outPath = Path.Combine(outDir, $"Employee_Group_Export_{DateTime.Now:yyyy-MM-dd}.xlsx");
package.SaveAs(new FileInfo(outPath));

Console.WriteLine($"Employees exported: {empRowCount}");
Console.WriteLine($"Groups exported:    {grpRowCount}");
Console.WriteLine($"Saved to: {outPath}");
return 0;

static string? FindAppSettings()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 10 && dir != null; i++)
    {
        var candidate = Path.Combine(dir.FullName, "appsettings.json");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

static void StyleSheet(ExcelWorksheet ws, int colCount)
{
    if (ws.Dimension == null) return;

    using (var header = ws.Cells[1, 1, 1, colCount])
    {
        header.Style.Font.Bold = true;
        header.Style.Font.Color.SetColor(Color.White);
        header.Style.Fill.PatternType = ExcelFillStyle.Solid;
        header.Style.Fill.BackgroundColor.SetColor(Color.SteelBlue);
        header.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    ws.View.FreezePanes(2, 1);
    ws.Cells[ws.Dimension.Address].AutoFitColumns();
}
