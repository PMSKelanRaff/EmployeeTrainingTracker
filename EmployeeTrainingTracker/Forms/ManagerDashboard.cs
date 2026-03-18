using System;
using System.Data;
using Npgsql; 
using System.Windows.Forms;
using System.Text;
using EmployeeTrainingTracker.Utilities;
using System.Security.Cryptography;

namespace EmployeeTrainingTracker
{
    public partial class ManagerDashboard : Form
    {
        private int _managerId;
        private string _managerDepartment;
        private string _currentEmployeeName = "None";
        private bool _loadingManagerCombo = false;
        private bool _isSyncingSelection = false;

        private void SetCurrentEmployeeName(string employeeName)
        {
            _currentEmployeeName = employeeName;
        }

        public ManagerDashboard(int managerId)
        {
            InitializeComponent();
            _managerId = managerId;
        }

        private void ManagerDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                // 1. Get Manager's Department
                _managerDepartment = GetManagerDepartment(_managerId);
                this.Text = $"Manager Dashboard - {_managerDepartment}";

                if (string.IsNullOrEmpty(_managerDepartment) || _managerDepartment == "Unknown")
                {
                    MessageBox.Show("Your account is not assigned to a valid department.", "Access Denied");
                    this.Close();
                    return;
                }

                // -----------------------------------------------------------
                // LOCK SENSITIVE CONTROLS
                // -----------------------------------------------------------
                cmbRole.Enabled = false; // Manager cannot change roles
                cmbDept.Enabled = false; // Manager cannot change departments
                                         // -----------------------------------------------------------

                // 2. Load Data restricted by Department
                LoadDepartmentEmployees();
                LoadEmployeeListForReports();
                LoadPlannedTraining();

                // UI Setup
                tabCertificates.Enabled = false;
                LoadReportSettings();
                StyleAllDGVs();

               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        // Load data for each tab
        private string GetManagerDepartment(int empId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT Department FROM Employees WHERE EmployeeID = $1", conn);
            cmd.Parameters.AddWithValue(empId);
            return cmd.ExecuteScalar()?.ToString();
        }

        // --- TAB 1: EMPLOYEES (Restricted to Department) ---
        private void LoadEmployees()
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // CHANGED: 
                // 1. Swapped positions of 'Email' and 'FullName' in the SELECT list
                // 2. Added 'ORDER BY FullName ASC' at the end
                using (var cmd = new NpgsqlCommand(@"
            SELECT 
                u.UserID,
                COALESCE(e.FullName, u.Email) AS FullName,
                u.Email AS Email,
                u.Role,
                e.EmployeeID,
                COALESCE(e.Department, 'Unknown') AS Department,
                COALESCE(e.JobTitle, 'Unknown') AS JobTitle
            FROM Users u
            LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
            ORDER BY FullName ASC", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);

                        // 1. Bind original data to DataGridView
                        dgvEmployees.DataSource = table;

                        // Hide technical ID columns
                        if (dgvEmployees.Columns.Contains("UserID"))
                            dgvEmployees.Columns["UserID"].Visible = false;

                        if (dgvEmployees.Columns.Contains("EmployeeID"))
                            dgvEmployees.Columns["EmployeeID"].Visible = false;

                        // ---------------------------------------------------------
                        // NEW: Populate ComboBox independently with an "All" option
                        // ---------------------------------------------------------
                        DataTable comboTable = table.Copy();

                        DataRow allRow = comboTable.NewRow();
                        allRow["EmployeeID"] = 0; // Using 0 as our "All" flag
                        allRow["FullName"] = "All Employees";
                        comboTable.Rows.InsertAt(allRow, 0);

                        // Unhook event to prevent errors while the data source is setting
                        cmbCurrentEmployee.SelectedIndexChanged -= cmbCurrentEmployee_SelectedIndexChanged;

                        cmbCurrentEmployee.DataSource = comboTable;
                        cmbCurrentEmployee.DisplayMember = "FullName";
                        cmbCurrentEmployee.ValueMember = "EmployeeID";

                        // Re-hook the event
                        cmbCurrentEmployee.SelectedIndexChanged += cmbCurrentEmployee_SelectedIndexChanged;
                        // ---------------------------------------------------------
                    }
                }
            }
        }

        private void LoadDepartmentEmployees()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Filter by Department
            string query = @"
                SELECT 
                    u.UserID,
                    u.Email AS Email,
                    u.Role,
                    e.EmployeeID,
                    COALESCE(e.FullName, u.Email) AS FullName,
                    COALESCE(e.Department, 'Unknown') AS Department,
                    COALESCE(e.JobTitle, 'Unknown') AS JobTitle
                FROM Users u
                LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
                WHERE e.Department = $1";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue(_managerDepartment);

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            dgvEmployees.DataSource = table;

            if (dgvEmployees.Columns.Contains("UserID")) dgvEmployees.Columns["UserID"].Visible = false;
            if (dgvEmployees.Columns.Contains("EmployeeID")) dgvEmployees.Columns["EmployeeID"].Visible = false;

            // Populate Combo for "Current Employee" selection
            cmbCurrentEmployee.DataSource = table;
            cmbCurrentEmployee.DisplayMember = "FullName";
            cmbCurrentEmployee.ValueMember = "EmployeeID";
        }


        // --- TAB 2: CERTIFICATES (Restricted to Department)) ---
        private void LoadCertificates(int employeeId)
        {
            DataTable table = CertificateService.GetCertificates(employeeId);

            dgvCertificates.Columns.Clear();
            dgvCertificates.AutoGenerateColumns = false;

            // Hidden ID column (needed for editing/deleting)
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateID",
                DataPropertyName = "CertificateID",
                HeaderText = "ID",
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
        }

        private void SetupCertGridColumns()
        {
            dgvCertificates.Columns.Clear();
            dgvCertificates.AutoGenerateColumns = false;
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateID", DataPropertyName = "CertificateID", Visible = false });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateName", DataPropertyName = "CertificateName", HeaderText = "Certificate Name" });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", DataPropertyName = "Key", HeaderText = "Key" });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "HRS", DataPropertyName = "HRS", HeaderText = "CPD Hrs" });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", DataPropertyName = "Provider", HeaderText = "Provider" });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssueDate", DataPropertyName = "IssueDate", HeaderText = "Issue Date" });
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpiryDate", DataPropertyName = "ExpiryDate", HeaderText = "Expiry Date" });
            dgvCertificates.Columns.Add(new DataGridViewLinkColumn { Name = "FileLink", DataPropertyName = "FilePath", HeaderText = "File", TrackVisitedState = true });

            if (dgvCertificates.Columns.Contains("CertificateID"))
            {
                dgvCertificates.Columns["CertificateID"].Visible = false;
            }
        }

        // --- TAB 3: PLANNING (Restricted to Department) ---
        private void LoadPlannedTraining()
        {
            // Use the NEW overload in PlannedTrainingService that accepts department
            DataTable table = PlannedTrainingService.GetPlannedTraining(null, _managerDepartment);

            dgvPlannedTraining.DataSource = table;
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (dgvPlannedTraining.Columns.Contains("SessionID"))
                dgvPlannedTraining.Columns["SessionID"].Visible = false;
        }

        private void LoadEmployeesForPlanning(int sessionId)
        {
            clbEmployeesPlan.Items.Clear();

            // Get ONLY department employees
            var deptEmployees = PlannedTrainingService.GetEmployeesByDepartment(_managerDepartment);
            var participantIds = PlannedTrainingService.GetPlannedEmployeeIds(sessionId);

            foreach (var emp in deptEmployees)
            {
                clbEmployeesPlan.Items.Add(emp, participantIds.Contains(emp.Id));
            }

            // Optional: Load groups managed by this manager
            // ...
        }

        // --- TAB 4: REPORTS (Restricted Lists) ---
        private void LoadEmployeeListForReports()
        {
            clbEmployees.Items.Clear();
            // Use the new service method
            var list = PlannedTrainingService.GetEmployeesByDepartment(_managerDepartment);
            foreach (var item in list)
            {
                clbEmployees.Items.Add(item);
            }
        }


        // CRUD for certificates
        private void btnAddCert_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string name = txtCertName.Text.Trim();
            string key = string.IsNullOrWhiteSpace(txtKeyCertsTab.Text)
                ? string.Empty
                : txtKeyCertsTab.Text.Trim()[0].ToString();
            string hrsText = txtHrsCertsTab.Text.Trim();
            string provider = txtProviderCertsTab.Text.Trim();


            // Check dates before processing

            if (dtpExpiryDate.Checked && dtpExpiryDate.Value.Date < dtpIssueDate.Value.Date)
            {
                MessageBox.Show("Expiry Date cannot be earlier than Issue Date.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string issue = dtpIssueDate.Value.ToString("yyyy-MM-dd");
            string? expiry = dtpExpiryDate.Checked ? dtpExpiryDate.Value.ToString("yyyy-MM-dd") : null;

            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            double.TryParse(hrsText, out double cpdHrs);

            CertificateService.AddCertificate(empId, name, key, cpdHrs, provider, issue, expiry, filePath);

            LoadCertificates(empId);

        }

        private void btnEditCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            string name = txtCertName.Text.Trim();
            string key = string.IsNullOrWhiteSpace(txtKeyCertsTab.Text)
                ? string.Empty
                : txtKeyCertsTab.Text.Trim()[0].ToString();
            string hrsText = txtHrsCertsTab.Text.Trim();
            string provider = txtProviderCertsTab.Text.Trim();

            // Check dates before processing
            if (dtpExpiryDate.Checked && dtpExpiryDate.Value.Date < dtpIssueDate.Value.Date)
            {
                MessageBox.Show("Expiry Date cannot be earlier than Issue Date.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string issue = dtpIssueDate.Value.ToString("yyyy-MM-dd");
            string? expiry = dtpExpiryDate.Checked ? dtpExpiryDate.Value.ToString("yyyy-MM-dd") : null;

            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim())
                ? null
                : txtFilePath.Text.Trim('"').Trim();

            double.TryParse(hrsText, out double cpdHrs);

            CertificateService.UpdateCertificate(certId, name, key, cpdHrs, provider, issue, expiry, filePath);

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            LoadCertificates(empId);

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

            LoadCertificates(empId);
        }

        private void btnBrowseFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Report File";
                ofd.Filter = "PDF Files (*.pdf)|*.pdf|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                }
            }
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


            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                using (var cmdEmp = new NpgsqlCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1, $2, $3) RETURNING EmployeeID;", conn))
                {

                    cmdEmp.Parameters.AddWithValue(username);
                    cmdEmp.Parameters.AddWithValue(department);
                    cmdEmp.Parameters.AddWithValue(jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }


                using (var cmdUser = new NpgsqlCommand(
                    "INSERT INTO Users (Email, Role, EmployeeID) VALUES ($1, $2, $3) RETURNING UserID;", conn))
                {

                    cmdUser.Parameters.AddWithValue(username);
                    cmdUser.Parameters.AddWithValue(role);
                    cmdUser.Parameters.AddWithValue(newEmpId);
                    newUserId = (long)cmdUser.ExecuteScalar();
                }
            }

            LoadEmployees();
            ClearEmployeeInputs();

            // Select the newly added user
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.Cells["Email"].Value?.ToString() == username) 
                {
                    row.Selected = true;
                    dgvEmployees.CurrentCell = row.Cells["Email"]; 
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
            int? employeeId = empIdObj == null || empIdObj == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(empIdObj);

            // 1. Gather Input
            string fullName = txtFullName.Text.Trim();
            string email = txtUsername.Text.Trim(); // Assuming txtUsername holds the Email
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            // NEW: Get the password
            string newPassword = txtPassword.Text.Trim(); // Ensure your textbox is named 'txtPassword'

            // 2. Validate Password (if user typed one)
            // We only validate length if the box is NOT empty. If empty, we ignore it (keep old password).
            if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters.");
                return; // Stop the update
            }

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // 3. Update or Insert Employee Info (Your existing logic)
                if (employeeId.HasValue)
                {
                    using (var cmdEmp = new NpgsqlCommand(
                        "UPDATE Employees SET FullName=$1, Department=$2, JobTitle=$3 WHERE EmployeeID=$4", conn))
                    {
                        cmdEmp.Parameters.AddWithValue(fullName);
                        cmdEmp.Parameters.AddWithValue(department);
                        cmdEmp.Parameters.AddWithValue(jobTitle);
                        cmdEmp.Parameters.AddWithValue(employeeId.Value);
                        cmdEmp.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmdInsertEmp = new NpgsqlCommand(
                        "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1,$2,$3) RETURNING EmployeeID;", conn))
                    {
                        cmdInsertEmp.Parameters.AddWithValue(fullName);
                        cmdInsertEmp.Parameters.AddWithValue(department);
                        cmdInsertEmp.Parameters.AddWithValue(jobTitle);

                        long newEmpId = (long)cmdInsertEmp.ExecuteScalar();

                        using (var cmdUpdateUserEmp = new NpgsqlCommand(
                            "UPDATE Users SET EmployeeID=$1 WHERE UserID=$2", conn))
                        {
                            cmdUpdateUserEmp.Parameters.AddWithValue(newEmpId);
                            cmdUpdateUserEmp.Parameters.AddWithValue(userId);
                            cmdUpdateUserEmp.ExecuteNonQuery();
                        }
                    }
                }

                // 4. Update User Role and Email (Always runs)
                using (var cmdUser = new NpgsqlCommand(
                    "UPDATE Users SET Email=$1, Role=$2 WHERE UserID=$3", conn))
                {
                    cmdUser.Parameters.AddWithValue(email);
                    cmdUser.Parameters.AddWithValue(role);
                    cmdUser.Parameters.AddWithValue(userId);
                    cmdUser.ExecuteNonQuery();
                }

                // 5. NEW: Update Password (Only runs if text box is not empty)
                if (!string.IsNullOrEmpty(newPassword))
                {
                    // Hash the password using the helper method to match your signup format
                    string passwordHash = HashPassword(newPassword);

                    // Verify your column name is 'Password' or 'PasswordHash' in your database!
                    using (var cmdPass = new NpgsqlCommand(
                        "UPDATE Users SET Password=$1 WHERE UserID=$2", conn))
                    {
                        cmdPass.Parameters.AddWithValue(passwordHash);
                        cmdPass.Parameters.AddWithValue(userId);
                        cmdPass.ExecuteNonQuery();
                    }
                }
            }

            MessageBox.Show("Employee updated successfully.");
            LoadEmployees();

            // Optional: Clear the password box after update for security
            txtPassword.Text = "";
        }

        private string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
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

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // REMOVED: Explicit DELETE from TrainingCertificates
                //// Delete certificates first
                //using (var cmdCert = new NpgsqlCommand("DELETE FROM TrainingCertificates WHERE EmployeeID=$1", conn))
                //{
                //    cmdCert.Parameters.AddWithValue(empId.Value);
                //    cmdCert.ExecuteNonQuery();
                //}

                // Then delete user
                using (var cmdUser = new NpgsqlCommand("DELETE FROM Users WHERE EmployeeID=$1", conn))
                {
                    cmdUser.Parameters.AddWithValue(empId.Value);
                    cmdUser.ExecuteNonQuery();
                }

                // Finally, delete the employee record
                using (var cmdEmp = new NpgsqlCommand("DELETE FROM Employees WHERE EmployeeID=$1", conn))
                {
                    cmdEmp.Parameters.AddWithValue(empId.Value);
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


        //CRUD for Groups
       

        // Reporting and Exports
        private void btnGenerateReport_Click(object sender, EventArgs e)
        {
            string reportType = cmbReportType.SelectedItem?.ToString() ?? "";
            bool isCustomRange = reportType.Contains("Custom Range");
            DateTime? start = isCustomRange ? dtpStart.Value.Date : null;
            DateTime? end = isCustomRange ? dtpEnd.Value.Date : null;

            // Get checked employees
            // Collect individual employees
            var selectedEmployees = clbEmployees.CheckedItems
                .Cast<EmployeeItem>()
                .Select(x => x.Id)
                .ToList();

            // Include all employees from selected groups
            foreach (EmployeeItem group in clbGroups.CheckedItems)
            {
                if (!group.IsGroup) continue;

                // CHANGED: Assumes GroupHelper is refactored
                var members = GroupService.GetMembersByGroup(group.Id)
                                         .AsEnumerable()
                                         .Select(r => Convert.ToInt32((long)r["EmployeeID"]))
                                         .ToList();

                foreach (var memberId in members)
                {
                    if (!selectedEmployees.Contains(memberId))
                        selectedEmployees.Add(memberId);
                }
            }

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
                // CHANGED: Assumes ReportService is refactored
                DataTable results = ReportService.GenerateReport(reportType, start, end, selectedEmployees);
                dgvReportResults.DataSource = results;

                // Style the DGV
                UIHelpers.StyleDataGridView(dgvReportResults);

                // Rename the columns after DataSource is assigned
                UIHelpers.RenameColumns(dgvReportResults);
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
            // Prevent infinite loop if we are currently syncing from the ComboBox
            if (_isSyncingSelection) return;

            if (dgvEmployees.CurrentRow != null)
            {
                var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"].Value;
                var userIdObj = dgvEmployees.CurrentRow.Cells["UserID"].Value;

                if (userIdObj != null && userIdObj != DBNull.Value)
                {
                    // Load employee details into textboxes
                    txtFullName.Text = dgvEmployees.CurrentRow.Cells["FullName"].Value?.ToString();
                    txtUsername.Text = dgvEmployees.CurrentRow.Cells["Email"].Value?.ToString();
                    cmbRole.SelectedItem = dgvEmployees.CurrentRow.Cells["Role"].Value?.ToString();
                    cmbDept.Text = dgvEmployees.CurrentRow.Cells["Department"].Value?.ToString();
                    txtJobTitle.Text = dgvEmployees.CurrentRow.Cells["JobTitle"].Value?.ToString();

                    if (empIdObj != null && empIdObj != DBNull.Value)
                    {
                        int empId = Convert.ToInt32(empIdObj);
                        string fullName = dgvEmployees.CurrentRow.Cells["FullName"].Value?.ToString() ?? "Unknown";
                        SetCurrentEmployeeName(fullName);

                        // TURN ON THE FLAG before changing the ComboBox
                        _isSyncingSelection = true;
                        cmbCurrentEmployee.SelectedValue = empId; // This syncs the dropdown!
                        _isSyncingSelection = false; // TURN IT OFF

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
                //chkAddToTrainingFolder.Checked = false; // reset when nothing selected
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

            try
            {
                // Make sure your DataTable contains EmployeeID or similar identifier
                if (rowView.Row.Table.Columns.Contains("EmployeeID"))
                {
                    int employeeId = Convert.ToInt32(rowView["EmployeeID"]);
                    string certName = txtCertName.Text;

                    
                }
            }
            catch (Exception ex)
            {
                //chkAddToTrainingFolder.Checked = false;
                Console.WriteLine($"Error checking Excel: {ex.Message}");
            }
        }

        private void cmbCurrentEmployee_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Prevent infinite loop if we are currently syncing from the DataGridView
            if (_isSyncingSelection) return;

            if (cmbCurrentEmployee.SelectedItem == null) return;

            if (cmbCurrentEmployee.SelectedItem is DataRowView drv)
            {
                int empId = Convert.ToInt32(drv["EmployeeID"]);

                if (empId == 0)
                {
                    // "All Employees" is selected
                    SetCurrentEmployeeName("All Employees");
                    LoadCertificates(0);

                    // Turn on flag, clear grid selection, turn off flag
                    _isSyncingSelection = true;
                    dgvEmployees.ClearSelection();
                    _isSyncingSelection = false;
                }
                else
                {
                    // Specific employee selected
                    SetCurrentEmployeeName(drv["FullName"].ToString());
                    LoadCertificates(empId);

                    // Turn on flag, update grid selection, turn off flag
                    _isSyncingSelection = true;
                    SyncDgvSelection(empId);
                    _isSyncingSelection = false;
                }

                // Enable/Disable buttons
                bool employeeSelected = (empId != 0);
                btnAdd.Enabled = employeeSelected;
                btnEdit.Enabled = employeeSelected;
                btnDelete.Enabled = employeeSelected;

                tabCertificates.Enabled = true;
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

        private void StyleAllDGVs()
        {
            // Apply styling to all DGVs
            UIHelpers.StyleDataGridView(dgvPlannedTraining);
            UIHelpers.StyleDataGridView(dgvCertificates);
            UIHelpers.StyleDataGridView(dgvEmployees);
            UIHelpers.StyleDataGridView(dgvReportResults);
        

            UIHelpers.RenameColumns(dgvPlannedTraining);
            UIHelpers.RenameColumns(dgvCertificates);
            UIHelpers.RenameColumns(dgvEmployees);
            UIHelpers.RenameColumns(dgvReportResults);
         
            foreach (TabPage tab in tabControl.TabPages)
            {
                //tab.BackColor = Color.Gray; // or Color.Gainsboro / Color.WhiteSmoke / LightGray
            }

        }

        private string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                value = value.Replace("\"", "\"\"");
                value = $"\"{value}\"";
            }
            return value;
        }

        private List<int> GetSelectedEmployees()
        {
            var selectedIds = new List<int>();

            foreach (var item in clbEmployeesPlan.CheckedItems)
            {
                if (item is EmployeeItem empItem)
                {
                    if (empItem.IsGroup)
                    {
                        // Add all members of the group
                        var members = GroupService.GetMembersByGroup(empItem.Id)
                                                 .AsEnumerable()
                                                 .Select(r => Convert.ToInt32((long)r["EmployeeID"]))
                                                 .ToList();
                        selectedIds.AddRange(members);
                    }
                    else
                    {
                        selectedIds.Add(empItem.Id);
                    }
                }
            }

            return selectedIds.Distinct().ToList(); // remove duplicates in case multiple groups overlap
        }
    }

}