using EmployeeTrainingTracker.Helpers;
using EmployeeTrainingTracker.Utilities;
using Microsoft.VisualBasic.ApplicationServices;
using Npgsql; 
using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace EmployeeTrainingTracker
{
    public partial class AdminDashboard : Form
    {
        private string currentEmployeeName = "None";
        private bool _loadingManagerCombo = false;
        private bool _isSyncingSelection = false;
        private int loggedInUserId;


        private void SetCurrentEmployeeName(string employeeName)
        {
            currentEmployeeName = employeeName;
        }


        public AdminDashboard(int userId)
        {
            InitializeComponent();
            ResponsiveScaler.Attach(this);
            loggedInUserId = userId; // Save the ID when the form opens
        }

        public AdminDashboard()
        {
            InitializeComponent();
            ResponsiveScaler.Attach(this);

        }

        private void AdminDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                // Perfectly divide the split container in half dynamically
                splitContainer1.SplitterDistance = splitContainer1.Height / 2;

                LoadEmployees();
                LoadGroupsForReports();
                LoadEmployeeList();
                LoadPlannedTraining();
                LoadDeletionTasks();

                dgvPendingAcks.CellContentClick += dgvPendingAcks_CellContentClick;
                dgvPendingAcks.CellFormatting += dgvPendingAcks_CellFormatting;
                dgvTasks.CellFormatting += dgvTasks_CellFormatting;
                dgvTasks.CellContentClick += dgvTasks_CellContentClick;
                dgvCertificates.CellFormatting += dgvCertificates_CellFormatting;
                dgvPlannedTraining.CellFormatting += dgvPlannedTraining_CellFormatting;
                dgvCertificates.DataBindingComplete += dgvCertificates_DataBindingComplete;
                LoadPendingApprovals();

                LoadReportSettings();
                StyleAllDGVs();
                LoadManagers();
                LoadGroups();

                LoadDepartmentOptions();

                cbManager.SelectedIndexChanged += cbManager_SelectedIndexChanged;
                tabControl.SelectedIndexChanged += tabControl_SelectedIndexChanged_Refresh;

                // Select the logged-in user once the grid is on screen (the grid picks row 0 when first shown)
                this.Shown += (s, args) => SelectEmployeeRow(loggedInUserId);
                dgvGroups.DataBindingComplete += dgvGroupGrids_DataBindingComplete;
                dgvGroupMembers.DataBindingComplete += dgvGroupGrids_DataBindingComplete;

                var plannedSessions = PlannedTrainingService.GetPlannedTraining();
                if (plannedSessions.Rows.Count > 0)
                {
                    int sessionId = Convert.ToInt32(plannedSessions.Rows[0]["SessionID"]);
                    LoadEmployeesForPlanning(sessionId);
                }
            }
            catch (Exception ex)
            {
                // This will show you the error if an Admin logs in and it fails
                MessageBox.Show($"A critical error occurred while loading the admin dashboard:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }


        // Load data for each tab
        private void LoadEmployees()
        {
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Active employees by default; "Show archived employees" switches to the archive.
                // Archived employees show the department(s) they were in when archived.
                using (var cmd = new NpgsqlCommand(@"
            SELECT
                u.UserID,
                COALESCE(e.FullName, u.Email) AS FullName,
                u.Email AS Email,
                u.Role,
                e.EmployeeID,
                CASE WHEN e.EmployeeID IS NULL THEN 'Unassigned'
                     WHEN e.IsArchived THEN COALESCE(e.Department, 'Unassigned')
                     ELSE " + GroupService.EmployeeDepartmentsSql + @" END AS Department,
                COALESCE(e.JobTitle, 'Unknown') AS JobTitle,
                e.ArchivedAt
            FROM Users u
            LEFT JOIN Employees e ON u.EmployeeID = e.EmployeeID
            WHERE COALESCE(e.IsArchived, FALSE) = @archived
            ORDER BY FullName ASC", conn))
                {
                    cmd.Parameters.AddWithValue("archived", chkShowArchived.Checked);

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

                        if (dgvEmployees.Columns.Contains("ArchivedAt"))
                        {
                            dgvEmployees.Columns["ArchivedAt"].Visible = chkShowArchived.Checked;
                            dgvEmployees.Columns["ArchivedAt"].HeaderText = "Archived On";
                        }

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

        private void LoadCertificates(int employeeId)
        {
            DataTable table = CertificateService.GetCertificates(employeeId);
            DashboardHelper.BindCertificates(dgvCertificates, table);

            // update buttons correctly
            UpdateCertificateButtons();
        } //Certs

        private void LoadEmployeeList()
        {
            clbEmployees.Items.Clear();
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                var cmd = new NpgsqlCommand("SELECT EmployeeID, FullName FROM Employees WHERE NOT IsArchived ORDER BY FullName", conn);
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
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var cmd = new NpgsqlCommand(@"
        SELECT 
            ts.SessionID,
            ts.CertificateName,
            ts.Key,
            ts.HRS,
            ts.Provider,
            ts.PlannedDate,
            ts.Status,
            ts.Notes,
            STRING_AGG(e.FullName, ', ') AS Participants
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

            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.CellFormatting += dgvPlannedTraining_CellFormatting;
            dgvCertificates.CellFormatting += dgvCertificates_CellFormatting;

            if (dgvPlannedTraining.Columns.Contains("SessionID"))
                dgvPlannedTraining.Columns["SessionID"].Visible = false;
        } //Planning

        private void LoadGroups()
        {
            // Fetch all groups
            DataTable dtGroups = GroupService.GetAllGroups();

            dgvGroups.AutoGenerateColumns = true;
            dgvGroups.AllowUserToAddRows = true;    // empty bottom row = "new department" (fill the boxes, then click Add)
            dgvGroups.DataSource = dtGroups;

            // Format DGV
            dgvGroups.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn col in dgvGroups.Columns)
                col.ReadOnly = true;

            // Hide internal IDs if you want
            if (dgvGroups.Columns.Contains("GroupID"))
                dgvGroups.Columns["GroupID"].Visible = false;
            if (dgvGroups.Columns.Contains("ManagerID"))
                dgvGroups.Columns["ManagerID"].Visible = false;

            // Optional: pre-select first row if you want auto-population
            ApplyGroupGridHeaders();

            if (dgvGroups.Rows.Count > 0)
                dgvGroups.Rows[0].Selected = true;
        }

        // Re-reads everything that depends on the employee list (groups, members,
        // manager names, report pickers) so the other tabs never show stale data.
        private void RefreshEmployeeDependentTabs()
        {
            int? selectedGroupId = null;
            if (dgvGroups.SelectedRows.Count > 0 && dgvGroups.Columns.Contains("GroupID")
                && dgvGroups.SelectedRows[0].Cells["GroupID"].Value is not (null or DBNull))
                selectedGroupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);

            LoadGroups();   // re-selects row 0, which reloads members + manager combo

            if (selectedGroupId.HasValue)
            {
                foreach (DataGridViewRow row in dgvGroups.Rows)
                {
                    if (Convert.ToInt32(row.Cells["GroupID"].Value) == selectedGroupId.Value)
                    {
                        dgvGroups.ClearSelection();
                        row.Selected = true;
                        dgvGroups.CurrentCell = row.Cells["GroupName"];
                        break;
                    }
                }
            }

            LoadEmployeeList();
            LoadGroupsForReports();
            LoadDepartmentOptions();
        }

        private void tabControl_SelectedIndexChanged_Refresh(object? sender, EventArgs e)
        {
            // Departments tab: always show fresh data when the user switches to it
            if (tabControl.SelectedTab == Groups)
                RefreshEmployeeDependentTabs();

            // Employees tab: department memberships may have changed on the Departments tab
            if (tabControl.SelectedTab == tabEmployees)
            {
                int? selectedEmpId = GetSelectedEmployeeId();
                LoadDepartmentOptions();
                LoadEmployees();
                if (selectedEmpId.HasValue) SelectEmployeeRow(selectedEmpId.Value);
            }
        }

        // Department drop-down on the Employees tab lists the real departments (Groups)
        private void LoadDepartmentOptions()
        {
            string current = cmbDept.Text;
            cmbDept.Items.Clear();
            foreach (string name in GroupService.GetDepartmentNames())
                cmbDept.Items.Add(name);
            cmbDept.Text = current;
        }

        private bool IsRealDepartment(string department)
            => cmbDept.Items.Cast<object>().Any(i => string.Equals(i.ToString(), department, StringComparison.OrdinalIgnoreCase));

        // Selects an employee in the Employees grid, which also loads them on the Certificates tab
        private void SelectEmployeeRow(int employeeId)
            => DashboardHelper.SelectEmployeeRow(dgvEmployees, employeeId);

        private void LoadManagers()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string query = @"
            SELECT EmployeeID, FullName
            FROM Employees
            WHERE NOT IsArchived
            ORDER BY FullName;";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            DataTable dtManagers = new DataTable();
            dtManagers.Load(reader);

            cbManager.DataSource = dtManagers;
            cbManager.DisplayMember = "FullName";
            cbManager.ValueMember = "EmployeeID";

            cbManager.SelectedIndex = -1; // no default selection
        }

        private void LoadGroupMembers(int groupId)
        {
            // Fetch members from DB
            DataTable dtMembers = GroupService.GetMembersByGroup(groupId);

            // Debug: confirm rows returned
            Console.WriteLine($"GroupID {groupId} Members returned: {dtMembers.Rows.Count}");

            // Bind to DGV
            dgvGroupMembers.AutoGenerateColumns = true;
            dgvGroupMembers.DataSource = dtMembers;
            dgvGroupMembers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            foreach (DataGridViewColumn col in dgvGroupMembers.Columns)
                col.ReadOnly = true;

            ApplyGroupGridHeaders();
        }

        // Friendly headers for the Departments tab grids. Matches column names
        // case-insensitively (Postgres returns them lowercase) and is re-applied
        // after every data bind so a refresh can never put the raw names back.
        private void ApplyGroupGridHeaders()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "GroupName", "Department" },
                { "Description", "Description" },
                { "ManagerName", "Manager Name" },
                { "EmployeeID", "Emp ID" },
                { "FullName", "Full Name" },
                { "Department", "Department" },
                { "JobTitle", "Job Title" },
            };

            foreach (var grid in new[] { dgvGroups, dgvGroupMembers })
            {
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (headers.TryGetValue(col.Name, out var text) ||
                        (!string.IsNullOrEmpty(col.DataPropertyName) && headers.TryGetValue(col.DataPropertyName, out text)))
                        col.HeaderText = text;
                }
            }
        }

        private void dgvGroupGrids_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
            => ApplyGroupGridHeaders();

        private void LoadEmployeesForPlanning(int sessionId)
        {
            clbEmployeesPlan.Items.Clear();

            var allEmployees = PlannedTrainingService.GetAllEmployees();
            var participantIds = PlannedTrainingService.GetPlannedEmployeeIds(sessionId);

            foreach (var emp in allEmployees)
            {
                clbEmployeesPlan.Items.Add(emp, participantIds.Contains(emp.Id)); // pre-check participants
            }

            // Load groups
            var allGroups = GroupService.GetAllGroups();
            foreach (DataRow row in allGroups.Rows)
            {
                int groupId = Convert.ToInt32((long)row["GroupID"]);
                var groupItem = new EmployeeItem
                {
                    Id = groupId,
                    Name = $"{row["GroupName"]} (Group)",

                    IsGroup = true
                };

                // Get all member IDs for this group
                var members = GroupService.GetMembersByGroup(groupId)
                                         .AsEnumerable()
                                         .Select(r => Convert.ToInt32((long)r["EmployeeID"]))
                                         .ToList();

                // Pre-check only if *all* group members are selected
                bool isChecked = members.Count > 0 && members.All(m => participantIds.Contains(m));

                clbEmployeesPlan.Items.Add(groupItem, isChecked);
            }
        }

        private void LoadGroupsForReports()
        {
            clbGroups.Items.Clear();

            var allGroups = GroupService.GetAllGroups();

            foreach (DataRow row in allGroups.Rows)
            {
                var groupItem = new EmployeeItem
                {
                    Id = Convert.ToInt32((long)row["GroupID"]),
                    Name = $"{row["GroupName"]} (Group)",
                    IsGroup = true
                };

                clbGroups.Items.Add(groupItem);
            }
        }

        private void LoadAvailableMembersForGroup(int groupId)
        {
            // Reuse the exact service call used in your AddMemberForm
            DataTable dt = GroupService.GetAvailableEmployeesForGroup(groupId);

            // Bind to the new ComboBox
            cmbPotentialMembers.DataSource = dt;
            cmbPotentialMembers.DisplayMember = "FullName"; // Assumed column name based on other queries
            cmbPotentialMembers.ValueMember = "EmployeeID"; // The PK for the employee

            // Reset selection so it doesn't default to the first person immediately
            cmbPotentialMembers.SelectedIndex = -1;

            // Disable the button if no one is available to add
            btnAddMemberDirect.Enabled = dt.Rows.Count > 0;
        }

        private void LoadDeletionTasks()
        {
            try
            {
                DataTable pendingDeletions = CertificateService.GetPendingDeletions();
                DashboardHelper.BindDeletionTasks(dgvTasks, pendingDeletions);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tasks: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadPendingApprovals()
        {
            try
            {
                // 1. Point this to our NEW Certificate method
                DataTable pendingAcks = CertificateService.GetPendingCertificateApprovals(loggedInUserId); // Use loggedInUserId for AdminDashboard
                DashboardHelper.BindPendingApprovals(dgvPendingAcks, pendingAcks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading pending approvals: {ex.Message}");
            }
        }


        // CRUD for certificates
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

            DateTime issue = dtpIssueDate.Value.Date;
            DateTime? expiry = dtpExpiryDate.Checked ? dtpExpiryDate.Value.Date : null;
            double.TryParse(hrsText, out double cpdHrs);

            CertificateService.UpdateCertificate(certId, name, key, cpdHrs, provider, issue, expiry, null);
            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            LoadCertificates(empId);

            // clear the boxes 
            txtCertName.Clear();
            txtHrsCertsTab.Clear();
            txtProviderCertsTab.Clear();
            txtKeyCertsTab.SelectedIndex = -1;

            MessageBox.Show("Certificate updated successfully!");

        }

        private async void btnDeleteCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvCertificates.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "Unknown";
            string? s3Key = dgvCertificates.CurrentRow.Cells["FileLink"].Value?.ToString();

            var confirm = MessageBox.Show($"Are you sure you want to permanently delete the certificate '{certName}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.No) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);

            try
            {
                // 1. Delete the physical file from S3
                if (!string.IsNullOrEmpty(s3Key))
                {
                    await S3Service.DeleteCertificateAsync(s3Key);
                }

                // 2. Delete the record from database
                CertificateService.DeleteCertificate(certId);

                // 3. Refresh UI
                LoadCertificates(empId);

                // Optional: Clear the textboxes if the deleted row was selected
                txtCertName.Clear();
                txtHrsCertsTab.Clear();
                txtProviderCertsTab.Clear();
                txtKeyCertsTab.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while deleting:\n{ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async void btnUpload_Click(object sender, EventArgs e)
        {
            int? currentEmployeeId = GetSelectedEmployeeId();

            if (currentEmployeeId == null)
            {
                MessageBox.Show("Please select an employee first before uploading a certificate.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.jpg;*.png)|*.jpg;*.png|All Files (*.*)|*.*";
                ofd.Title = "Select a Certificate to Upload";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string localFilePath = ofd.FileName;

                    // 1. Gather ALL the data from the UI
                    string certName = txtCertName.Text.Trim();
                    string key = string.IsNullOrWhiteSpace(txtKeyCertsTab.Text) ? string.Empty : txtKeyCertsTab.Text.Trim()[0].ToString();
                    string hrsText = txtHrsCertsTab.Text.Trim();
                    string provider = txtProviderCertsTab.Text.Trim();
                    DateTime issueDate = dtpIssueDate.Value.Date;
                    DateTime expiryDate = dtpExpiryDate.Value.Date;

                    if (string.IsNullOrEmpty(certName))
                    {
                        MessageBox.Show("Please enter a name for the certificate.");
                        return;
                    }

                    // Parse the hours safely
                    double? cpdHrs = double.TryParse(hrsText, out double parsedHrs) ? parsedHrs : (double?)null;

                    btnUpload.Enabled = false;
                    btnUpload.Text = "Uploading...";

                    // 2. Pass all data to the service
                    bool success = await CertificateService.SaveCertificateAsync(
                        currentEmployeeId.Value, currentEmployeeName, certName, localFilePath,
                        issueDate, expiryDate, key, cpdHrs, provider);

                    if (success)
                    {
                        MessageBox.Show("Certificate successfully uploaded to the cloud!");

                        // 3. Clear UI and Reload Grid
                        txtCertName.Clear();
                        txtKeyCertsTab.SelectedIndex = -1;
                        txtHrsCertsTab.Clear();
                        txtProviderCertsTab.Clear();

                        LoadCertificates(currentEmployeeId.Value);
                    }
                    else
                    {
                        MessageBox.Show("Upload failed. Please try again.");
                    }

                    btnUpload.Enabled = true;
                    btnUpload.Text = "Upload";
                }
            }
        }


        private void btnAddEmployee_Click(object sender, EventArgs e)
        {
            // 1. Gather Input from TextBoxes (instead of Windows Identity)
            string fullName = txtFullName.Text.Trim();
            string email = txtUsername.Text.Trim(); // Assuming txtUsername is your Email box
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();
            string password = txtPassword.Text.Trim();

            // 2. Basic Validation
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Full Name and Email are required.");
                return;
            }

            // Returning employee: offer to restore their archived record (keeps their history)
            int? archivedId = EmployeeAdminService.FindArchivedEmployeeByEmail(email);
            if (archivedId.HasValue)
            {
                var restore = MessageBox.Show(
                    $"{email} belongs to an archived employee.\n\nRestore their previous record, including their certificates and training history?",
                    "Returning Employee", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (restore == DialogResult.Yes)
                    RestoreAndShow(archivedId.Value, fullName);
                else
                    MessageBox.Show("That email is still in use by the archived record, so a new account can't be created with it.", "Email In Use");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("A password is required for new users.");
                return;
            }

            // 3. Create the employee + linked user account, placed in the chosen department (group)
            int newEmployeeId = EmployeeAdminService.AddEmployee(fullName, email, role, department, jobTitle, password);
            if (IsRealDepartment(department))
                EmployeeAdminService.SetEmployeeDepartment(newEmployeeId, department);

            // 6. Refresh UI
            LoadEmployees();
            RefreshEmployeeDependentTabs();
            ClearEmployeeInputs();
            MessageBox.Show("Employee added successfully.");

            // Select the newly added user in the grid
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.Cells["Email"].Value?.ToString() == email)
                {
                    row.Selected = true;
                    dgvEmployees.CurrentCell = row.Cells["Email"];
                    break;
                }
            }
        }

        private void btnUpdateEmployee_Click(object sender, EventArgs e)
        {
            // 1. Basic Validation
            if (dgvEmployees.CurrentRow == null) return;

            var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"].Value;
            var userIdObj = dgvEmployees.CurrentRow.Cells["UserID"].Value;

            if (userIdObj == null || userIdObj == DBNull.Value)
            {
                MessageBox.Show("Please select a valid user row.");
                return;
            }

            int userId = Convert.ToInt32(userIdObj);
            int? employeeId = empIdObj == null || empIdObj == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(empIdObj);

            // 2. Gather Input Data
            string fullName = txtFullName.Text.Trim();
            string email = txtUsername.Text.Trim(); // Assuming this is the email/username field
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            // Get Password Input
            string newPassword = txtPassword.Text.Trim();

            // Departments are group memberships - only a real department name is applied
            bool realDepartment = IsRealDepartment(department);
            EmployeeAdminService.UpdateEmployee(userId, employeeId, fullName, email, role, realDepartment ? department : null, jobTitle, newPassword);

            string departmentNote = "";
            if (realDepartment && employeeId.HasValue
                && EmployeeAdminService.SetEmployeeDepartment(employeeId.Value, department) == EmployeeAdminService.DepartmentChangeResult.MultipleDepartments)
            {
                departmentNote = "\n\nThis employee belongs to more than one department, so their departments were not changed. Use the Departments tab to manage them.";
            }

            // 6. Refresh UI
            MessageBox.Show("Employee updated successfully." + departmentNote);
            LoadEmployees();
            RefreshEmployeeDependentTabs();

            // Clear the password box so it doesn't stay on screen
            txtPassword.Text = "";
        }

        private void btnDeleteEmployee_Click(object sender, EventArgs e)
        {
            int? empId = GetSelectedEmployeeId();
            if (empId == null)
            {
                MessageBox.Show("Please select a valid employee.");
                return;
            }

            if (empId.Value == loggedInUserId)
            {
                MessageBox.Show("You can't delete your own account while logged in.", "Not Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Delete this employee?\n\n" +
                "They will be archived: removed from all active lists and unable to log in, but their record and " +
                "certificates are kept. You can restore them from 'Show archived employees' if they return.",
                "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            EmployeeAdminService.DeleteEmployee(empId.Value);

            LoadEmployees();
            RefreshEmployeeDependentTabs();
            dgvCertificates.DataSource = null;
            tabCertificates.Enabled = false;
        }

        private void btnRestoreEmployee_Click(object? sender, EventArgs e)
        {
            int? empId = GetSelectedEmployeeId();
            if (empId == null)
            {
                MessageBox.Show("Please select an archived employee to restore.");
                return;
            }

            string name = dgvEmployees.CurrentRow?.Cells["FullName"].Value?.ToString() ?? "this employee";
            var confirm = MessageBox.Show($"Restore {name}? They will be able to log in again.", "Restore Employee", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            RestoreAndShow(empId.Value, name);
        }

        // Restores an archived employee, switches back to the active list and selects them
        private void RestoreAndShow(int employeeId, string name)
        {
            var rejoined = EmployeeAdminService.RestoreEmployee(employeeId);

            chkShowArchived.Checked = false; // reloads the active list
            RefreshEmployeeDependentTabs();
            SelectEmployeeRow(employeeId);

            string departments = rejoined.Count > 0
                ? $"They have been put back into: {string.Join(", ", rejoined)}."
                : "Their previous department no longer exists, so they are Unassigned - pick a department and click Edit.";
            MessageBox.Show($"{name} has been restored.\n\n{departments}", "Employee Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void chkShowArchived_CheckedChanged(object? sender, EventArgs e)
        {
            bool archiveView = chkShowArchived.Checked;

            // The archive is read-only apart from Restore
            btnRestoreEmployee.Visible = archiveView;
            btnDeleteEmployee.Enabled = !archiveView;
            btnEditEmployee.Enabled = !archiveView;
            btnAddEmployee.Enabled = !archiveView;

            ClearEmployeeInputs();
            LoadEmployees();
        }

        private void ClearEmployeeInputs()
        {
            DashboardHelper.ClearEmployeeInputs(txtUsername, txtPassword, cmbRole, cmbDept, txtJobTitle);
        }

        private int? GetSelectedEmployeeId()
        {
            return DashboardHelper.GetSelectedEmployeeId(dgvEmployees);
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
        private void btnAddGroup_Click(object sender, EventArgs e)
        {
            GroupService.AddGroup(
                txtGroupName.Text.Trim(),
                txtDescription.Text.Trim(),
                cbManager.SelectedValue as int?
            );

            LoadGroups();
        }

        // True only when a saved department (not the empty "new" row) is selected
        private bool HasSavedGroupSelected()
        {
            return dgvGroups.SelectedRows.Count > 0
                && !dgvGroups.SelectedRows[0].IsNewRow
                && dgvGroups.SelectedRows[0].Cells["GroupID"].Value is not (null or DBNull);
        }

        private void btnEditGroup_Click(object sender, EventArgs e)
        {
            if (!HasSavedGroupSelected())
            {
                MessageBox.Show("Please select a group to edit.", "Edit Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);
            string groupName = txtGroupName.Text.Trim();
            string description = txtDescription.Text.Trim();

            // Handle manager selection safely
            int? managerId = null;
            if (cbManager.SelectedValue != null && cbManager.SelectedValue != DBNull.Value)
                managerId = Convert.ToInt32(cbManager.SelectedValue);

            // Update the group
            GroupService.UpdateGroup(
                groupId,
                groupName,
                description,
                managerId
            );

            // Refresh the groups grid
            LoadGroups();

            // Optionally re-select the edited row
            foreach (DataGridViewRow row in dgvGroups.Rows)
            {
                if (Convert.ToInt32(row.Cells["GroupID"].Value) == groupId)
                {
                    row.Selected = true;
                    dgvGroups.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }

            MessageBox.Show("Group updated successfully.", "Edit Group", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnDeleteGroup_Click(object sender, EventArgs e)
        {
            if (!HasSavedGroupSelected())
            {
                MessageBox.Show("Please select a group to delete.", "Delete Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);

            var confirm = MessageBox.Show(
                "Are you sure you want to delete this group? All memberships will also be removed.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                GroupService.DeleteGroup(groupId);
                LoadGroups();
                dgvGroupMembers.DataSource = null; // clear members grid
            }
        }

        private void btnAddMemberDirect_Click(object sender, EventArgs e)
        {
            // 1. Validation: Is a group selected?
            if (!HasSavedGroupSelected())
            {
                MessageBox.Show("Please select a group first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Validation: Is a user selected in the dropdown?
            if (cmbPotentialMembers.SelectedIndex == -1 || cmbPotentialMembers.SelectedValue == null)
            {
                MessageBox.Show("Please select an employee to add.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);
                int employeeId = Convert.ToInt32(cmbPotentialMembers.SelectedValue);

                // 3. Call the service (Same logic as AddMemberForm.cs)
                GroupService.AddMemberToGroup(groupId, employeeId);

                // 4. Refresh the grids and the dropdown
                LoadGroupMembers(groupId); // Show the new member in the grid
                LoadAvailableMembersForGroup(groupId); // Remove the added member from the dropdown

                // Optional: Show success message (or keep it silent for speed)
                // MessageBox.Show("Member added successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding member: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRemoveMember_Click(object sender, EventArgs e)
        {
            if (!HasSavedGroupSelected() || dgvGroupMembers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a group and a member to remove.", "Remove Member", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);
            int employeeId = Convert.ToInt32(dgvGroupMembers.SelectedRows[0].Cells["EmployeeID"].Value);

            var confirm = MessageBox.Show(
                "Are you sure you want to remove this member from the group?",
                "Confirm Remove",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                GroupService.RemoveMemberFromGroup(groupId, employeeId);
                LoadGroupMembers(groupId); // refresh members DGV
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
            DashboardHelper.ExportDataGridViewToCsv(dgvReportResults, "Report");
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

        // Tasks
        private async void btnApproveDeletion_Click(object sender, EventArgs e)
        {
            if (dgvTasks.CurrentRow == null)
            {
                MessageBox.Show("Please select a task to approve.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int certId = Convert.ToInt32(dgvTasks.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvTasks.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "Unknown";
            string s3Key = dgvTasks.CurrentRow.Cells["FileLink"].Value?.ToString();

            var confirm = MessageBox.Show($"Are you sure you want to PERMANENTLY delete '{certName}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.No) return;

            try
            {
                // 1. Delete the physical file from S3 Cloud (Reusing your existing logic)
                if (!string.IsNullOrEmpty(s3Key))
                {
                    await S3Service.DeleteCertificateAsync(s3Key);
                }

                // 2. Delete the record from the database
                CertificateService.DeleteCertificate(certId);

                MessageBox.Show("Certificate permanently deleted.");

                // 3. Refresh the grids
                LoadDeletionTasks();

                // Optional: Refresh the main employee cert grid if that specific employee happens to be selected
                int? currentEmp = GetSelectedEmployeeId();
                if (currentEmp != null) LoadCertificates(currentEmp.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving deletion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRejectDeletion_Click(object sender, EventArgs e)
        {
            if (dgvTasks.CurrentRow == null)
            {
                MessageBox.Show("Please select a task to reject.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int certId = Convert.ToInt32(dgvTasks.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvTasks.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "Unknown";

            var confirm = MessageBox.Show($"Reject deletion request for '{certName}'? It will be restored to the employee's active dashboard.", "Confirm Reject", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (confirm == DialogResult.No) return;

            try
            {
                // Un-flag it in the database
                CertificateService.UnmarkCertificateForDeletion(certId);

                MessageBox.Show("Deletion request rejected. Certificate restored.");

                // Refresh the grids
                LoadDeletionTasks();

                int? currentEmp = GetSelectedEmployeeId();
                if (currentEmp != null) LoadCertificates(currentEmp.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rejecting deletion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnApproveAck_Click(object sender, EventArgs e)
        {
            // Changed to check CurrentRow instead of SelectedRows
            if (dgvPendingAcks.CurrentRow == null)
            {
                MessageBox.Show("Please select a record to approve.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Grab the row the user is currently clicked into
            DataGridViewRow selectedRow = dgvPendingAcks.CurrentRow;

            int ackId = Convert.ToInt32(selectedRow.Cells["CertificateID"].Value);

            // Grab the typed-in Trainer Name (if any)
            string trainerName = selectedRow.Cells["trainername"].Value?.ToString() ?? "";

            var confirm = MessageBox.Show(
                "Are you sure you want to approve this training record? This will digitally sign it as Completed.",
                "Confirm Approval",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    // Note: Make sure this uses CertificateService!
                    CertificateService.ApproveCertificate(ackId, trainerName);
                    MessageBox.Show("Record Approved Successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    LoadPendingApprovals(); // Refresh the grid
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error approving record: {ex.Message}");
                }
            }
        }

        private async void btnRejectAck_Click(object sender, EventArgs e)
        {
            // Changed to check CurrentRow
            if (dgvPendingAcks.CurrentRow == null)
            {
                MessageBox.Show("Please select a record to reject.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Grab the row the user is currently clicked into
            DataGridViewRow selectedRow = dgvPendingAcks.CurrentRow;

            int certId = Convert.ToInt32(selectedRow.Cells["CertificateID"].Value);
            string topic = selectedRow.Cells["trainingtopic"].Value?.ToString() ?? "Unknown";
            string? s3Key = selectedRow.Cells["FileLink"].Value?.ToString();

            var confirm = MessageBox.Show(
                $"Are you sure you want to REJECT and DELETE the pending certificate for '{topic}'?",
                "Confirm Rejection",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    // 1. Delete the bad file from the S3 Cloud
                    if (!string.IsNullOrEmpty(s3Key))
                    {
                        await S3Service.DeleteCertificateAsync(s3Key);
                    }

                    // 2. Delete the record from the database
                    CertificateService.DeleteCertificate(certId);

                    MessageBox.Show("Record Rejected and Deleted Successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    LoadPendingApprovals(); // Refresh the grid
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error rejecting record: {ex.Message}");
                }
            }
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
                btnEdit.Enabled = employeeSelected;
                btnDelete.Enabled = employeeSelected;

                tabCertificates.Enabled = true;
            }
        }



        private void cbManager_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_loadingManagerCombo) return; // skip if combo is still loading
            if (!HasSavedGroupSelected()) return;
            if (cbManager.SelectedValue == null) return;

            int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);
            int newManagerId = Convert.ToInt32(cbManager.SelectedValue);

            // Update DB
            GroupService.UpdateGroup(
                groupId,
                dgvGroups.SelectedRows[0].Cells["GroupName"].Value.ToString(),
                dgvGroups.SelectedRows[0].Cells["Description"].Value.ToString(),
                newManagerId
            );

            // Refresh main grid safely after update
            LoadGroups();

            // Re-select group after reload
            foreach (DataGridViewRow row in dgvGroups.Rows)
            {
                if (Convert.ToInt32(row.Cells["GroupID"].Value) == groupId)
                {
                    row.Selected = true;
                    break;
                }
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
            => DashboardHelper.FileLinkCellClick(sender, e);

        private void dgvCertificates_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
            => DashboardHelper.ApplyStandardRowStyling((DataGridView)sender);

        private void UpdateCertificateButtons()
        {
            // Check if an employee is selected in the grid AND the dropdown is NOT set to "All" (0)
            bool employeeSelected = dgvEmployees.CurrentRow != null &&
                                    cmbCurrentEmployee.SelectedValue != null &&
                                    Convert.ToInt32(cmbCurrentEmployee.SelectedValue) != 0;

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

        private void dgvGroups_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (dgvGroups.SelectedRows.Count == 0) return;

                var selected = dgvGroups.SelectedRows[0];
                if (selected.IsNewRow || selected.Cells["GroupID"].Value == null || selected.Cells["GroupID"].Value == DBNull.Value)
                {
                    // "New department" row: clear the form so a fresh department can be typed in
                    _loadingManagerCombo = true;
                    txtGroupName.Clear();
                    txtDescription.Clear();
                    dgvGroupMembers.DataSource = null;
                    cbManager.DataSource = null;
                    cmbPotentialMembers.DataSource = null;
                    _loadingManagerCombo = false;
                    txtGroupName.Focus();
                    return;
                }

                int groupId = Convert.ToInt32(selected.Cells["GroupID"].Value);

                LoadGroupMembers(groupId); // Your log message

                var row = dgvGroups.SelectedRows[0];
                txtGroupName.Text = row.Cells["GroupName"].Value.ToString();
                txtDescription.Text = row.Cells["Description"].Value.ToString();

                // --- APPLY THIS CHANGE ---
                _loadingManagerCombo = true;
                PopulateManagerComboBox(groupId, row.Cells["ManagerID"].Value);
                _loadingManagerCombo = false;

                LoadAvailableMembersForGroup(groupId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while selecting the group:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateManagerComboBox(int groupId, object currentManagerId)
        {
            DataTable dtMembers = GroupService.GetMembersByGroup(groupId);

            cbManager.DataSource = dtMembers;
            cbManager.DisplayMember = "FullName";
            cbManager.ValueMember = "EmployeeID";

            // Set current manager if exists
            if (currentManagerId != DBNull.Value)
                cbManager.SelectedValue = Convert.ToInt32(currentManagerId);
            else
                cbManager.SelectedIndex = -1;
        }

        private void dgvPendingAcks_CellContentClick(object sender, DataGridViewCellEventArgs e)
            => DashboardHelper.FileLinkCellClick(sender, e);

        private void dgvPendingAcks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);


        // Helper Functions

        private void StyleAllDGVs()
        {
            // Apply styling to all DGVs
            UIHelpers.StyleDataGridView(dgvPlannedTraining);
            UIHelpers.StyleDataGridView(dgvCertificates);
            UIHelpers.StyleDataGridView(dgvEmployees);
            UIHelpers.StyleDataGridView(dgvReportResults);
            UIHelpers.StyleDataGridView(dgvGroups);
            UIHelpers.StyleDataGridView(dgvGroupMembers);

            UIHelpers.RenameColumns(dgvPlannedTraining);
            UIHelpers.RenameColumns(dgvCertificates);
            UIHelpers.RenameColumns(dgvEmployees);
            UIHelpers.RenameColumns(dgvReportResults);
            UIHelpers.RenameColumns(dgvGroups);
            UIHelpers.RenameColumns(dgvGroupMembers);

            foreach (TabPage tab in tabControl.TabPages)
            {
                //tab.BackColor = Color.Gray; // or Color.Gainsboro / Color.WhiteSmoke / LightGray
            }

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

        private void dgvTasks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);

        private void dgvTasks_CellContentClick(object sender, DataGridViewCellEventArgs e)
            => DashboardHelper.FileLinkCellClick(sender, e);

        private void dgvCertificates_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);

        private void btnExportHTSF13_Click(object sender, EventArgs e)
        {
            // 1. Ensure an employee is selected
            int? currentEmployeeId = GetSelectedEmployeeId();
            if (currentEmployeeId == null || currentEmployeeId == 0)
            {
                MessageBox.Show("Please select a specific employee to generate their record.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Locate your blank template file 
            // (Put your blank template in the same folder as your .exe, or hardcode your desktop path here if preferred)
            string templatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "HTSF13_Template.xlsx");

            if (!System.IO.File.Exists(templatePath))
            {
                MessageBox.Show($"Could not find the blank template file at:\n{templatePath}", "Template Missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Ask the user where they want to save the new file
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = $"{currentEmployeeName.Replace(" ", "_")}_HTSF13_Record_{DateTime.Now:yyyyMMdd}.xlsx";
                sfd.Title = "Save HTSF13 Record";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Generate!
                        LegacyExcelService.GenerateHTSF13(currentEmployeeId.Value, currentEmployeeName, templatePath, sfd.FileName);

                        MessageBox.Show("HTSF13 Record generated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Open the file immediately so they can see it
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error generating Excel file:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnExportAllHTSF13_Click(object sender, EventArgs e)
        {
            string templatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "HTSF13_Template.xlsx");

            if (!System.IO.File.Exists(templatePath))
            {
                MessageBox.Show($"Could not find the template at:\n{templatePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to save ALL company HTSF13 Records";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string saveFolder = fbd.SelectedPath;

                    try
                    {
                        // ADMIN SETTINGS: Set to true so it grabs the entire company!
                        bool isAdmin = true;
                        int dummyUserId = 0; // The query ignores this when isAdmin is true

                        DataTable employeesToExport = LegacyExcelService.GetEmployeesForExport(dummyUserId, isAdmin);

                        if (employeesToExport.Rows.Count == 0)
                        {
                            MessageBox.Show("No employees found in the database.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        int count = 0;

                        foreach (DataRow row in employeesToExport.Rows)
                        {
                            int empId = Convert.ToInt32(row["EmployeeID"]);
                            string empName = row["FullName"].ToString();
                            string safeName = empName.Replace(" ", "_");

                            string savePath = System.IO.Path.Combine(saveFolder, $"{safeName}_HTSF13_{DateTime.Now:yyyyMMdd}.xlsx");

                            LegacyExcelService.GenerateHTSF13(empId, empName, templatePath, savePath);
                            count++;
                        }

                        MessageBox.Show($"Success! Generated {count} HTSF13 records.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        System.Diagnostics.Process.Start("explorer.exe", saveFolder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"A bulk export error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

}