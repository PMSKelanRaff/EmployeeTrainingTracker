using System;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Windows.Forms;
using System.Text;
using EmployeeTrainingTracker.Utilities;

namespace EmployeeTrainingTracker
{
    public partial class AdminDashboard : Form
    {

        private string currentEmployeeName = "None";

        private void SetCurrentEmployeeName(string employeeName)
        {
            currentEmployeeName = employeeName;
        }

        public AdminDashboard()
        {
            InitializeComponent();
            LoadEmployees();
            LoadEmployeeList();
            LoadPlannedTraining();
            tabCertificates.Enabled = false;
            LoadReportSettings();
            StyleAllDGVs();

            var plannedSessions = PlannedTrainingService.GetPlannedTraining();
            if (plannedSessions.Rows.Count > 0)
            {
                int sessionId = Convert.ToInt32(plannedSessions.Rows[0]["SessionID"]);
                LoadEmployeesForPlanning(sessionId);
            }
        }


        // Load data for each tab
        private void LoadEmployees()
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                using (var cmd = new SqliteCommand(@"
                SELECT 
                    u.UserID,
                    u.Email AS Email,
                    u.Role,
                    e.EmployeeID,
                    IFNULL(e.FullName, u.Email) AS FullName,
                    IFNULL(e.Department, 'Unknown') AS Department,
                    IFNULL(e.JobTitle, 'Unknown') AS JobTitle
                FROM Users u
                LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);
                        dgvEmployees.DataSource = table;

                        // Optional: hide technical ID columns so UI looks cleaner
                        if (dgvEmployees.Columns.Contains("UserID"))
                            dgvEmployees.Columns["UserID"].Visible = false;

                        if (dgvEmployees.Columns.Contains("EmployeeID"))
                            dgvEmployees.Columns["EmployeeID"].Visible = false;

                        // Populate ComboBox
                        cmbCurrentEmployee.DataSource = table;
                        cmbCurrentEmployee.DisplayMember = "FullName";
                        cmbCurrentEmployee.ValueMember = "EmployeeID";
                    }
                }
            }
        } //Employees

        private void LoadCertificates(int employeeId)
        {
            DataTable table = CertificateService.GetCertificates(employeeId);

            dgvCertificates.Columns.Clear();
            dgvCertificates.AutoGenerateColumns = false;

            // CertificateID (hidden)
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateID",
                DataPropertyName = "CertificateID",
                Visible = false
            });

            // Certificate Name
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateName",
                DataPropertyName = "CertificateName",
                HeaderText = "Certificate Name"
            });

            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Key",
                DataPropertyName = "Key",
                HeaderText = "Training Key"
            });

            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HRS",
                DataPropertyName = "HRS",  // or CPDHrs depending on your DB
                HeaderText = "CPD Hrs"
            });

            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Provider",
                DataPropertyName = "Provider",
                HeaderText = "Provider"
            });

            // Issue Date
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IssueDate",
                DataPropertyName = "IssueDate",
                HeaderText = "Issue Date"
            });

            // Expiry Date
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpiryDate",
                DataPropertyName = "ExpiryDate",
                HeaderText = "Expiry Date"
            });

            // FileLink
            dgvCertificates.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "FileLink",
                DataPropertyName = "FilePath",
                HeaderText = "Certificate File",
                TrackVisitedState = true,
                Width = 200 // width (pixels)
            });

            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastNotifiedDate",
                DataPropertyName = "LastNotifiedDate",
                HeaderText = "Last Notified"
            });



            // Set DataSource last
            dgvCertificates.DataSource = table;

            // update buttons correctly
            UpdateCertificateButtons();
        } //Certs

        private void LoadEmployeeList()
        {
            clbEmployees.Items.Clear();
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand("SELECT EmployeeID, FullName FROM Employees ORDER BY FullName", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        clbEmployees.Items.Add(new EmployeeItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }
        } //Reports

        private void LoadPlannedTraining()
        {
            using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            // No transaction needed for a simple SELECT
            var cmd = new SqliteCommand(@"
        SELECT 
            ts.SessionID,
            ts.CertificateName,
            ts.Key,
            ts.HRS,
            ts.Provider,
            ts.PlannedDate,
            ts.Status,
            ts.Notes,
            GROUP_CONCAT(e.FullName, ', ') AS Participants
        FROM TrainingSessions ts
        LEFT JOIN TrainingParticipants tp ON ts.SessionID = tp.SessionID
        LEFT JOIN Employees e ON tp.EmployeeID = e.EmployeeID
        GROUP BY ts.SessionID
        ORDER BY ts.PlannedDate;", conn);

            DataTable table = new DataTable();
            using (var reader = cmd.ExecuteReader())
            {
                table.Load(reader);
            }

            dgvPlannedTraining.DataSource = table;

            // Optional: nicer UI setup
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.CellFormatting += dgvPlannedTraining_CellFormatting;

            if (dgvPlannedTraining.Columns.Contains("SessionID"))
                dgvPlannedTraining.Columns["SessionID"].Visible = false;
        } //Planning


        // CRUD for certificates
        private void btnAddCert_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string name = txtCertName.Text.Trim();
            string key = txtKeyCertsTab.Text.Trim();
            string hrsText = txtHrsCertsTab.Text.Trim();
            string provider = txtProviderCertsTab.Text.Trim();
            DateTime issue = dtpIssueDate.Value;
            DateTime expiry = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            double.TryParse(hrsText, out double cpdHrs);

            CertificateService.AddCertificate(empId, name, key, cpdHrs, provider, issue, expiry, filePath);

            LoadCertificates(empId);

            if (chkAddToTrainingFolder.Checked)
            {
                try
                {
                    LegacyExcelService.AppendTrainingRecord(empId, name, issue);
                    MessageBox.Show("Record also added to employee's training sheet.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Certificate added, but failed to update Excel sheet:\n{ex.Message}");
                }
            }
        }

        private void btnEditCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            string name = txtCertName.Text.Trim();
            string key = txtKeyCertsTab.Text.Trim();
            string hrsText = txtHrsCertsTab.Text.Trim();
            string provider = txtProviderCertsTab.Text.Trim();
            DateTime issue = dtpIssueDate.Value;
            DateTime expiry = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim())
                ? null
                : txtFilePath.Text.Trim('"').Trim();

            double.TryParse(hrsText, out double cpdHrs);

            CertificateService.UpdateCertificate(certId, name, key, cpdHrs, provider, issue, expiry, filePath);

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            LoadCertificates(empId);

            if (chkAddToTrainingFolder.Checked)
            {
                try
                {
                    LegacyExcelService.UpdateTrainingRecord(empId, name, issue);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Certificate updated, but failed to update Excel sheet:\n{ex.Message}");
                }
            }

            MessageBox.Show("Certificate updated successfully!");
        }

        private void btnDeleteCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvCertificates.CurrentRow.Cells["CertificateName"].Value.ToString();

            var confirm = MessageBox.Show("Delete this certificate?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);

            // Delete from database
            CertificateService.DeleteCertificate(certId);

            // Delete from Excel
            try
            {
                LegacyExcelService.DeleteTrainingRecord(empId, certName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deleted from database, but failed to update Excel: {ex.Message}");
            }

            LoadCertificates(empId);
        }


        // CRUD for employees
        private void btnAddEmployee_Click(object sender, EventArgs e)
        {
            // Get Windows username
            string windowsUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string shortUser = windowsUser.Contains("\\")
                ? windowsUser.Split('\\')[1]
                : windowsUser;

            string username = shortUser;  // Store the short name as Username
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            long newEmpId;
            long newUserId;

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // Create Employee row
                using (var cmdEmp = new SqliteCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES (@name, @dept, @title); SELECT last_insert_rowid();", conn))
                {
                    cmdEmp.Parameters.AddWithValue("@name", username);
                    cmdEmp.Parameters.AddWithValue("@dept", department);
                    cmdEmp.Parameters.AddWithValue("@title", jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }

                // Create User row (no password)
                using (var cmdUser = new SqliteCommand(
                    "INSERT INTO Users (Email, Role, EmployeeID) VALUES (@u, @r, @eid); SELECT last_insert_rowid();", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", username);
                    cmdUser.Parameters.AddWithValue("@r", role);
                    cmdUser.Parameters.AddWithValue("@eid", newEmpId);
                    newUserId = (long)cmdUser.ExecuteScalar();
                }
            }

            LoadEmployees();
            ClearEmployeeInputs();

            // Select the newly added user
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.Cells["Username"].Value?.ToString() == username)
                {
                    row.Selected = true;
                    dgvEmployees.CurrentCell = row.Cells["Username"];
                    break;
                }
            }
        }

        private void btnUpdateEmployee_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"].Value;
            var userIdObj = dgvEmployees.CurrentRow.Cells["UserID"].Value;

            if (userIdObj == null || userIdObj == DBNull.Value)
            {
                MessageBox.Show("Please select a valid employee.");
                return;
            }

            int userId = Convert.ToInt32(userIdObj);
            int? employeeId = empIdObj == null || empIdObj == DBNull.Value ? (int?)null : Convert.ToInt32(empIdObj);

            string username = txtUsername.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                if (employeeId.HasValue)
                {
                    // Update existing Employee
                    using (var cmdEmp = new SqliteCommand(
                        "UPDATE Employees SET FullName=@name, Department=@dept, JobTitle=@title WHERE EmployeeID=@id", conn))
                    {
                        cmdEmp.Parameters.AddWithValue("@name", username);
                        cmdEmp.Parameters.AddWithValue("@dept", department);
                        cmdEmp.Parameters.AddWithValue("@title", jobTitle);
                        cmdEmp.Parameters.AddWithValue("@id", employeeId.Value);
                        cmdEmp.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Insert new Employee
                    using (var cmdInsertEmp = new SqliteCommand(
                        "INSERT INTO Employees (FullName, Department, JobTitle) VALUES (@name,@dept,@title); SELECT last_insert_rowid();", conn))
                    {
                        cmdInsertEmp.Parameters.AddWithValue("@name", username);
                        cmdInsertEmp.Parameters.AddWithValue("@dept", department);
                        cmdInsertEmp.Parameters.AddWithValue("@title", jobTitle);

                        long newEmpId = (long)cmdInsertEmp.ExecuteScalar();

                        // Link back to Users
                        using (var cmdUpdateUserEmp = new SqliteCommand(
                            "UPDATE Users SET EmployeeID=@empId WHERE UserID=@uid", conn))
                        {
                            cmdUpdateUserEmp.Parameters.AddWithValue("@empId", newEmpId);
                            cmdUpdateUserEmp.Parameters.AddWithValue("@uid", userId);
                            cmdUpdateUserEmp.ExecuteNonQuery();
                        }
                    }
                }

                // Always update Users table (username + role)
                using (var cmdUser = new SqliteCommand(
                    "UPDATE Users SET Email=@u, Role=@r WHERE UserID=@uid", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", username);
                    cmdUser.Parameters.AddWithValue("@r", role);
                    cmdUser.Parameters.AddWithValue("@uid", userId);
                    cmdUser.ExecuteNonQuery();
                }
            }

            LoadEmployees();
        }

        private void btnDeleteEmployee_Click(object sender, EventArgs e)
        {
            int? empId = GetSelectedEmployeeId();
            if (empId == null)
            {
                MessageBox.Show("Please select a valid employee.");
                return;
            }

            var confirm = MessageBox.Show("Delete this employee and all their certificates?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // Delete certificates first
                using (var cmdCert = new SqliteCommand("DELETE FROM TrainingCertificates WHERE EmployeeID=@id", conn))
                {
                    cmdCert.Parameters.AddWithValue("@id", empId.Value);
                    cmdCert.ExecuteNonQuery();
                }

                // Then delete user
                using (var cmdUser = new SqliteCommand("DELETE FROM Users WHERE EmployeeID=@id", conn))
                {
                    cmdUser.Parameters.AddWithValue("@id", empId.Value);
                    cmdUser.ExecuteNonQuery();
                }

                // Finally, delete the employee record
                using (var cmdEmp = new SqliteCommand("DELETE FROM Employees WHERE EmployeeID=@id", conn))
                {
                    cmdEmp.Parameters.AddWithValue("@id", empId.Value);
                    cmdEmp.ExecuteNonQuery();
                }
            }

            LoadEmployees();
            dgvCertificates.DataSource = null;
            tabCertificates.Enabled = false;
        }

        private void ClearEmployeeInputs()
        {
            txtUsername.Text = "";
            txtPassword.Text = "";
            cmbRole.SelectedIndex = -1;
            cmbDept.Text = "";
            txtJobTitle.Text = "";
        }

        private int? GetSelectedEmployeeId()
        {
            if (dgvEmployees.CurrentRow == null || dgvEmployees.CurrentRow.IsNewRow)
                return null;

            var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"]?.Value;

            if (empIdObj == null || empIdObj == DBNull.Value)
                return null;

            return Convert.ToInt32(empIdObj);
        }


        //CRUD for Planning
        private void btnAddSession_Click(object sender, EventArgs e)
        {
            var selectedEmployeeIds = GetSelectedEmployees(); // List<int> from CheckedListBox
            PlannedTrainingService.AddPlannedSession(
                txtCertificateNamePlan.Text,
                txtKeyPlan.Text,
                double.TryParse(txtHrsPlan.Text, out double hrs) ? hrs : (double?)null,
                txtProviderPlan.Text,
                dtpPlannedDate.Value,
                textNotesPlan.Text,
                selectedEmployeeIds
            );

            LoadPlannedTraining(); // refresh DGV
        }

        private void btnEditSession_Click(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow == null) return;

            int sessionId = Convert.ToInt32(dgvPlannedTraining.CurrentRow.Cells["SessionID"].Value);
            var selectedEmployeeIds = GetSelectedEmployees();

            PlannedTrainingService.UpdatePlannedSession(
                sessionId,
                txtCertificateNamePlan.Text,
                txtKeyPlan.Text,
                double.TryParse(txtHrsPlan.Text, out double hrs) ? hrs : (double?)null,
                txtProviderPlan.Text,
                dtpPlannedDate.Value,
                textNotesPlan.Text,
                selectedEmployeeIds
            );

            LoadPlannedTraining();
        }

        private void btnDeleteSession_Click(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow == null) return;

            int sessionId = Convert.ToInt32(dgvPlannedTraining.CurrentRow.Cells["SessionID"].Value);

            var confirm = MessageBox.Show("Are you sure you want to delete this session?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            PlannedTrainingService.DeletePlannedSession(sessionId);
            LoadPlannedTraining();
        }

        private void btnCompleteTraining_Click(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow != null)
            {
                int sessionId = Convert.ToInt32(dgvPlannedTraining.CurrentRow.Cells["SessionID"].Value);
                PlannedTrainingService.CompleteTrainingSession(sessionId);
                LoadPlannedTraining(); // refresh grid
            }
        }


        // Reporting and Exports
        private void btnGenerateReport_Click(object sender, EventArgs e)
        {
            string reportType = cmbReportType.SelectedItem?.ToString() ?? "";
            bool isCustomRange = reportType.Contains("Custom Range");
            DateTime? start = isCustomRange ? dtpStart.Value.Date : null;
            DateTime? end = isCustomRange ? dtpEnd.Value.Date : null;

            // Get checked employees
            var selectedEmployees = clbEmployees.CheckedItems
                .Cast<EmployeeItem>()
                .Select(x => x.Id)
                .ToList();

            // If none are selected, automatically include all employees
            if (selectedEmployees.Count == 0)
            {
                selectedEmployees = clbEmployees.Items
                    .Cast<EmployeeItem>()
                    .Select(x => x.Id)
                    .ToList();
            }

            try
            {
                DataTable results = ReportService.GenerateReport(reportType, start, end, selectedEmployees);
                dgvReportResults.DataSource = results;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}");
            }
        }

        private void btnExportCsv_Click(object sender, EventArgs e)
        {
            if (dgvReportResults.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV files (*.csv)|*.csv";
                sfd.FileName = $"Report_{DateTime.Now:yyyyMMdd}.csv";

                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    StringBuilder sb = new StringBuilder();

                    // Header row
                    var columnNames = dgvReportResults.Columns
                                       .Cast<DataGridViewColumn>()
                                       .Where(c => c.Visible)
                                       .Select(c => c.HeaderText);
                    sb.AppendLine(string.Join(",", columnNames));

                    // Data rows
                    foreach (DataGridViewRow row in dgvReportResults.Rows)
                    {
                        if (!row.IsNewRow)
                        {
                            var cells = row.Cells.Cast<DataGridViewCell>()
                                           .Where(c => c.OwningColumn.Visible)
                                           .Select(c => EscapeCsvValue(c.Value?.ToString() ?? ""));
                            sb.AppendLine(string.Join(",", cells));
                        }
                    }

                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
                    MessageBox.Show("CSV exported successfully!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting CSV: {ex.Message}");
                }
            }
        }

        private void cmbReportType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string reportType = cmbReportType.SelectedItem?.ToString() ?? "";
            bool isCustomRange = reportType.Contains("Custom Range");

            dtpStart.Enabled = isCustomRange;
            dtpEnd.Enabled = isCustomRange;

            if (!isCustomRange)
            {
                dtpStart.Value = DateTime.Today;
                dtpEnd.Value = DateTime.Today;
            }
        }

        private void LoadReportSettings()
        {
            dtpStart.Enabled = false;
            dtpEnd.Enabled = false;
            dtpStart.MinDate = new DateTime(2000, 1, 1);
            dtpEnd.MaxDate = DateTime.Today;
        }


        // Events
        private void dgvEmployees_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow != null)
            {
                var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"].Value;
                var userIdObj = dgvEmployees.CurrentRow.Cells["UserID"].Value;

                if (userIdObj != null && userIdObj != DBNull.Value)
                {
                    int userId = Convert.ToInt32(userIdObj);

                    // Load employee + user details directly from DataGridView (faster than requerying DB)
                    txtUsername.Text = dgvEmployees.CurrentRow.Cells["Email"].Value?.ToString();
                    cmbRole.SelectedItem = dgvEmployees.CurrentRow.Cells["Role"].Value?.ToString();
                    cmbDept.Text = dgvEmployees.CurrentRow.Cells["Department"].Value?.ToString();
                    txtJobTitle.Text = dgvEmployees.CurrentRow.Cells["JobTitle"].Value?.ToString();

                    // If EmployeeID exists, load certificates
                    if (empIdObj != null && empIdObj != DBNull.Value)
                    {
                        int empId = Convert.ToInt32(empIdObj);
                        string fullName = dgvEmployees.CurrentRow.Cells["FullName"].Value?.ToString() ?? "Unknown";
                        SetCurrentEmployeeName(fullName);
                        LoadCertificates(empId);
                        tabCertificates.Enabled = true;
                    }
                    else
                    {
                        SetCurrentEmployeeName("None");
                        dgvCertificates.DataSource = null;
                        tabCertificates.Enabled = false;
                    }
                }
                else
                {
                    // No valid user selected
                    ClearEmployeeInputs();
                    dgvCertificates.DataSource = null;
                    tabCertificates.Enabled = false;
                }
            }
            else
            {
                ClearEmployeeInputs();
                tabCertificates.Enabled = false;
            }

            // Update certificate buttons (add/edit/delete)
            UpdateCertificateButtons();
        }

        private void dgvCertificates_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null || dgvCertificates.CurrentRow.IsNewRow)
            {
                txtCertName.Text = "";
                txtKeyCertsTab.Text = "";
                txtHrsCertsTab.Text = "";
                txtProviderCertsTab.Text = "";
                dtpIssueDate.Value = DateTime.Today;
                dtpExpiryDate.Value = DateTime.Today;
                txtFilePath.Text = "";
                chkAddToTrainingFolder.Checked = false; // reset when nothing selected
                return;
            }

            if (dgvCertificates.CurrentRow.DataBoundItem is not DataRowView rowView)
                return;

            // Existing population logic (unchanged)
            txtCertName.Text = rowView["CertificateName"]?.ToString() ?? "";
            txtKeyCertsTab.Text = rowView["Key"]?.ToString() ?? "";
            txtHrsCertsTab.Text = rowView["HRS"]?.ToString() ?? "";
            txtProviderCertsTab.Text = rowView["Provider"]?.ToString() ?? "";

            if (DateTime.TryParse(rowView["IssueDate"]?.ToString(), out var issue))
                dtpIssueDate.Value = issue;
            else
                dtpIssueDate.Value = DateTime.Today;

            if (DateTime.TryParse(rowView["ExpiryDate"]?.ToString(), out var expiry))
                dtpExpiryDate.Value = expiry;
            else
                dtpExpiryDate.Value = DateTime.Today;

            txtFilePath.Text = rowView["FilePath"]?.ToString() ?? "";

            // NEW SECTION: check if the training record already exists in legacy Excel
            try
            {
                // Make sure your DataTable contains EmployeeID or similar identifier
                if (rowView.Row.Table.Columns.Contains("EmployeeID"))
                {
                    int employeeId = Convert.ToInt32(rowView["EmployeeID"]);
                    string certName = txtCertName.Text;

                    bool exists = LegacyExcelService.TrainingRecordExists(employeeId, certName);
                    chkAddToTrainingFolder.Checked = exists;
                }
                else
                {
                    // If no EmployeeID in table, just default to unchecked
                    chkAddToTrainingFolder.Checked = false;
                }
            }
            catch (Exception ex)
            {
                chkAddToTrainingFolder.Checked = false;
                Console.WriteLine($"Error checking Excel: {ex.Message}");
            }
        }

        private void cmbCurrentEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbCurrentEmployee.SelectedItem == null) return;

            if (cmbCurrentEmployee.SelectedItem is DataRowView drv)
            {
                int empId = Convert.ToInt32(drv["EmployeeID"]);
                LoadCertificates(empId);
                SyncDgvSelection(empId);
            }
        }

        private void dgvPlannedTraining_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow?.DataBoundItem is not DataRowView rowView)
                return;

            // Fill textboxes with session info
            txtCertificateNamePlan.Text = rowView["CertificateName"]?.ToString() ?? "";
            txtKeyPlan.Text = rowView["Key"]?.ToString() ?? "";
            txtHrsPlan.Text = rowView["HRS"]?.ToString() ?? "";
            txtProviderPlan.Text = rowView["Provider"]?.ToString() ?? "";
            textNotesPlan.Text = rowView["Notes"]?.ToString() ?? "";
            textStatusPlan.Text = rowView["Status"]?.ToString() ?? "";
            dtpPlannedDate.Value = DateTime.TryParse(rowView["PlannedDate"]?.ToString(), out var d) ? d : DateTime.Today;

            // Populate CLB with participants
            int sessionId = Convert.ToInt32(rowView["SessionID"]);
            LoadEmployeesForPlanning(sessionId);
        }

        private void dgvCertificates_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ignore header clicks or invalid clicks
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Only handle clicks on the FileLink column
            if (dgvCertificates.Columns[e.ColumnIndex].Name != "FileLink")
                return;

            if (dgvCertificates.Rows[e.RowIndex].DataBoundItem is not DataRowView rowView)
                return;

            string? path = rowView["FilePath"]?.ToString()?.Trim('"').Trim();

            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("No file linked for this certificate.");
                return;
            }

            // Allowed extensions
            string[] allowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
            string ext = System.IO.Path.GetExtension(path).ToLower();

            if (!System.IO.File.Exists(path))
            {
                MessageBox.Show($"File not found:\n{path}");
                return;
            }

            if (!allowedExtensions.Contains(ext))
            {
                MessageBox.Show($"Unsupported file type: {ext}");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file:\n{ex.Message}");
            }
        }

        private void UpdateCertificateButtons()
        {
            // Always enable all certificate buttons if an employee is selected
            bool employeeSelected = dgvEmployees.CurrentRow != null;
            btnAdd.Enabled = employeeSelected;
            btnEdit.Enabled = employeeSelected;
            btnDelete.Enabled = employeeSelected;
        }

        private void SyncDgvSelection(int employeeId)
        {
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.Cells["EmployeeID"].Value != null &&
                    Convert.ToInt32(row.Cells["EmployeeID"].Value) == employeeId)
                {
                    row.Selected = true;
                    dgvEmployees.CurrentCell = row.Cells["Email"]; // or any visible cell
                    break;
                }
            }
        }

        private void dgvPlannedTraining_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvPlannedTraining.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string status = e.Value.ToString()!.Trim().ToLower();

                switch (status)
                {
                    case "completed":
                        e.CellStyle.BackColor = Color.LightGreen;
                        e.CellStyle.ForeColor = Color.Black;
                        break;

                    case "planned":
                        e.CellStyle.BackColor = Color.Khaki;
                        e.CellStyle.ForeColor = Color.Black;
                        break;

                    case "cancelled":
                        e.CellStyle.BackColor = Color.LightCoral;
                        e.CellStyle.ForeColor = Color.White;
                        break;

                    default:
                        e.CellStyle.BackColor = dgvPlannedTraining.DefaultCellStyle.BackColor;
                        e.CellStyle.ForeColor = dgvPlannedTraining.DefaultCellStyle.ForeColor;
                        break;
                }
            }
        }

        // Helper Functions
        private string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                value = value.Replace("\"", "\"\"");
                value = $"\"{value}\"";
            }
            return value;
        }

        private List<int> GetSelectedEmployees() //For Planning tab - Employees CLB
        {
            return clbEmployeesPlan.CheckedItems
                .Cast<EmployeeItem>() // your EmployeeItem class with Id property
                .Select(e => e.Id)
                .ToList();
        }

        private void LoadEmployeesForPlanning(int sessionId)
        {
            clbEmployeesPlan.Items.Clear();

            var allEmployees = PlannedTrainingService.GetAllEmployees();
            var participantIds = PlannedTrainingService.GetPlannedEmployeeIds(sessionId);

            foreach (var emp in allEmployees)
            {
                clbEmployeesPlan.Items.Add(emp, participantIds.Contains(emp.Id)); // pre-check participants
            }
        }

        private void StyleDataGridView(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;

            // Header style
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.SteelBlue;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Default cell style
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgv.DefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
            dgv.DefaultCellStyle.SelectionBackColor = Color.LightSteelBlue;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Alternating rows for readability
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.Black;

            // Layout and grid look
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.BackgroundColor = Color.White;
            dgv.GridColor = Color.LightGray;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
        }

        private void StyleAllDGVs()
        {
            // Apply styling to all DGVs
            StyleDataGridView(dgvPlannedTraining);
            StyleDataGridView(dgvCertificates);
            StyleDataGridView(dgvEmployees);
            StyleDataGridView(dgvReportResults);

            RenameColumns(dgvPlannedTraining);
            RenameColumns(dgvCertificates);
            RenameColumns(dgvEmployees);
            RenameColumns(dgvReportResults);

            foreach (TabPage tab in tabControl.TabPages)
            {
                //tab.BackColor = Color.Gray; // or Color.Gainsboro / Color.WhiteSmoke / LightGray
            }

        }

        private void RenameColumns(DataGridView dgv)
        {
            var renameMap = new Dictionary<string, string>
        {
            { "CertificateName", "Certificate Name" },
            { "PlannedDate", "Planned Date" },
            { "IssueDate", "Issue Date" },
            { "ExpiryDate", "Expiry Date" },
            { "EmployeeEmail", "Email" },
            { "FullName", "Full Name" },
            { "ManagerEmail", "Manager Email" },
            { "JobTitle", "Job Title" }
        };

            foreach (var kvp in renameMap)
            {
                if (dgv.Columns.Contains(kvp.Key))
                    dgv.Columns[kvp.Key].HeaderText = kvp.Value;
            }
        }

    }

}