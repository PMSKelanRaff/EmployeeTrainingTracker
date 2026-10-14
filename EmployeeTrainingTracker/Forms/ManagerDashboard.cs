using EmployeeTrainingTracker.Helpers;
using EmployeeTrainingTracker.Utilities;
using Npgsql; 
using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace EmployeeTrainingTracker
{
    public partial class ManagerDashboard : Form
    {
        private int _managerId;
        private string _managerDepartment;
        private string currentEmployeeName = "None";
        private bool _loadingManagerCombo = false;
        private bool _isSyncingSelection = false;

        public ManagerDashboard(int managerId)
        {
            InitializeComponent();
            ResponsiveScaler.Attach(this);
            _managerId = managerId;
        }

        // Shows the standard message and returns false when the manager doesn't own the record
        private bool EnsureAccess(bool allowed)
        {
            if (!allowed)
            {
                MessageBox.Show("You can only manage records for employees in your own groups.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return allowed;
        }

        // A session with no participants from the manager's groups would vanish from their grid
        private bool EnsureParticipantsSelected(List<int> employeeIds)
        {
            if (employeeIds.Count == 0)
            {
                MessageBox.Show("Please select at least one employee from your groups.", "No Participants", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // Selects an employee in the Employees grid, which also loads them on the Certificates tab
        private void SelectEmployeeRow(int employeeId)
            => DashboardHelper.SelectEmployeeRow(dgvEmployees, employeeId);

        // Department drop-down shows the real departments this manager runs (read-only for managers)
        private void LoadDepartmentOptions()
        {
            string current = cmbDept.Text;
            cmbDept.Items.Clear();
            foreach (DataRow row in GroupService.GetGroupsByManager(_managerId).Rows)
                cmbDept.Items.Add(row["GroupName"].ToString()!);
            cmbDept.Text = current;
        }

        // Reports tab "Groups" list - only the departments this manager runs
        private void LoadGroupsForReports()
        {
            clbGroups.Items.Clear();
            foreach (DataRow row in GroupService.GetGroupsByManager(_managerId).Rows)
            {
                clbGroups.Items.Add(new EmployeeItem
                {
                    Id = Convert.ToInt32(row["GroupID"]),
                    Name = $"{row["GroupName"]} (Group)",
                    IsGroup = true
                });
            }
        }

        // Re-reads department membership when switching tabs, since an Admin may have changed it
        private void tabControl_SelectedIndexChanged_Refresh(object? sender, EventArgs e)
        {
            try
            {
                if (tabControl.SelectedTab == tabEmployees)
                {
                    int? selectedEmpId = GetSelectedEmployeeId();
                    LoadDepartmentOptions();
                    LoadEmployees();
                    if (selectedEmpId.HasValue) SelectEmployeeRow(selectedEmpId.Value);
                }
                else if (tabControl.SelectedTab == Reports)
                {
                    LoadEmployeeList();
                    LoadGroupsForReports();
                }
                else if (tabControl.SelectedTab == Planning)
                {
                    LoadPlannedTraining();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetCurrentEmployeeName(string employeeName)
        {
            currentEmployeeName = employeeName;
        }


        //Load event for the Manager Dashboard
        private void ManagerDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                // 1. Authenticate Department - the department(s) this manager runs, from Groups
                _managerDepartment = GroupService.GetManagedDepartmentNames(_managerId);
                this.Text = $"Manager Dashboard - {_managerDepartment}";

                if (string.IsNullOrEmpty(_managerDepartment))
                {
                    MessageBox.Show("Your account is not assigned to manage any department.", "Access Denied");
                    this.Close();
                    return;
                }

                // 2. Lock Sensitive Controls (Managers can't change these)
                cmbRole.Enabled = false;
                cmbDept.Enabled = false;

                // 3. Load Restricted Data
                LoadDepartmentOptions();
                LoadEmployees(); // Now uses the restricted version below
                LoadEmployeeList();
                LoadGroupsForReports();
                LoadPlannedTraining();

                LoadDeletionTasks();
                dgvTasks.CellFormatting += dgvTasks_CellFormatting;
                dgvTasks.CellContentClick += dgvTasks_CellContentClick;
                dgvCertificates.CellFormatting += dgvCertificates_CellFormatting;
                dgvPlannedTraining.CellFormatting += dgvPlannedTraining_CellFormatting;
                dgvCertificates.DataBindingComplete += dgvCertificates_DataBindingComplete;

                LoadPendingApprovals();

                tabCertificates.Enabled = false;
                LoadReportSettings();
                StyleAllDGVs();

                // Select the manager themselves once the grid is on screen (the grid picks row 0 when first shown)
                this.Shown += (s, args) => SelectEmployeeRow(_managerId);

                // Pick up department changes an Admin makes while this dashboard is open
                tabControl.SelectedIndexChanged += tabControl_SelectedIndexChanged_Refresh;

                // Disable Admin-only tabs if they carried over
                // tabControl.TabPages.Remove(tabGroups); // Uncomment if you want to completely hide the Groups tab
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard:\n\n{ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void LoadEmployees()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Employees in the groups this manager runs, plus the manager themselves.
            // Department comes from group membership, not the old free-text column.
            string query = $@"
        SELECT
            u.UserID,
            COALESCE(e.FullName, u.Email) AS FullName,
            u.Email AS Email,
            u.Role,
            e.EmployeeID,
            {GroupService.EmployeeDepartmentsSql} AS Department,
            COALESCE(e.JobTitle, 'Unknown') AS JobTitle
        FROM Users u
        INNER JOIN Employees e ON u.EmployeeID = e.EmployeeID
        WHERE e.EmployeeID = $1
           OR e.EmployeeID IN (
               SELECT gm.EmployeeID FROM GroupMembers gm
               INNER JOIN Groups g ON gm.GroupID = g.GroupID
               WHERE g.ManagerID = $1)
        ORDER BY FullName ASC";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue(_managerId); // Pass the Manager's ID, not the Department!

            using var reader = cmd.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);

            dgvEmployees.DataSource = table;

            if (dgvEmployees.Columns.Contains("UserID")) dgvEmployees.Columns["UserID"].Visible = false;
            if (dgvEmployees.Columns.Contains("EmployeeID")) dgvEmployees.Columns["EmployeeID"].Visible = false;

            // Populate Combo for "Current Employee" selection with an "All" option
            DataTable comboTable = table.Copy();
            DataRow allRow = comboTable.NewRow();
            allRow["EmployeeID"] = 0;
            allRow["FullName"] = "All Employees";
            comboTable.Rows.InsertAt(allRow, 0);

            cmbCurrentEmployee.SelectedIndexChanged -= cmbCurrentEmployee_SelectedIndexChanged;
            cmbCurrentEmployee.DataSource = comboTable;
            cmbCurrentEmployee.DisplayMember = "FullName";
            cmbCurrentEmployee.ValueMember = "EmployeeID";
            cmbCurrentEmployee.SelectedIndexChanged += cmbCurrentEmployee_SelectedIndexChanged;
        }

        private void LoadCertificates(int employeeId)
        {
            // Always scoped to this manager's groups, whether "All Employees" (0) or a single employee
            DataTable table = CertificateService.GetCertificatesForManager(_managerId, employeeId);
            DashboardHelper.BindCertificates(dgvCertificates, table);

            // update buttons correctly
            UpdateCertificateButtons();
        } //Certs

        private void LoadEmployeeList()
        {
            clbEmployees.Items.Clear();

            // Switch to the new Manager ID method
            var list = PlannedTrainingService.GetEmployeesByManager(_managerId);
            foreach (var item in list)
            {
                clbEmployees.Items.Add(item);
            }
        }

        private void LoadPlannedTraining()
        {
            // Switch to the new Manager ID method
            DataTable table = PlannedTrainingService.GetPlannedTrainingForManager(_managerId);

            dgvPlannedTraining.DataSource = table;
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (dgvPlannedTraining.Columns.Contains("SessionID"))
                dgvPlannedTraining.Columns["SessionID"].Visible = false;
        }

        private void LoadEmployeesForPlanning(int sessionId)
        {
            clbEmployeesPlan.Items.Clear();

            // Switch to the new Manager ID method
            var deptEmployees = PlannedTrainingService.GetEmployeesByManager(_managerId);
            var participantIds = PlannedTrainingService.GetPlannedEmployeeIds(sessionId);

            foreach (var emp in deptEmployees)
            {
                clbEmployeesPlan.Items.Add(emp, participantIds.Contains(emp.Id));
            }
        }

        private void LoadDeletionTasks()
        {
            try
            {
                // Notice we are passing _managerId here!
                DataTable pendingDeletions = CertificateService.GetPendingDeletionsForManager(_managerId);
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
                DataTable pendingAcks = CertificateService.GetPendingCertificateApprovals(_managerId); // Use loggedInUserId for AdminDashboard
                DashboardHelper.BindPendingApprovals(dgvPendingAcks, pendingAcks);

                // Wire up the file-link column now that the columns exist (Admin's
                // dashboard also has these events on its dgvPendingAcks - Manager's
                // grid previously had no CellFormatting/CellContentClick handlers wired
                // at all, so the "View Document" link never shortened its text or opened
                // the file for Managers. Wiring it here (shared with every other grid)
                // fixes that for free.)
                dgvPendingAcks.CellFormatting -= DashboardHelper.FileLinkCellFormatting;
                dgvPendingAcks.CellFormatting += DashboardHelper.FileLinkCellFormatting;
                dgvPendingAcks.CellContentClick -= DashboardHelper.FileLinkCellClick;
                dgvPendingAcks.CellContentClick += DashboardHelper.FileLinkCellClick;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading pending approvals: {ex.Message}");
            }
        }

        private void LoadReportSettings()
        {
            dtpStart.Enabled = false;
            dtpEnd.Enabled = false;
            dtpStart.MinDate = new DateTime(2000, 1, 1);
            dtpEnd.MaxDate = DateTime.Today;
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

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, certId))) return;

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

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, certId))) return;

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

            if (!EnsureAccess(ManagerAccessService.CanManageEmployee(_managerId, currentEmployeeId.Value))) return;

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

        // CRUD for employees

        private void btnAddEmployee_Click(object sender, EventArgs e)
        {
            // 1. Gather Input from TextBoxes
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

            // 3. Find the Manager's GroupID - new employees are auto-assigned to it
            int? managerGroupId = EmployeeAdminService.GetManagerGroupId(_managerId);

            // Returning employee: restore their archived record into this manager's department
            int? archivedId = EmployeeAdminService.FindArchivedEmployeeByEmail(email);
            if (archivedId.HasValue && managerGroupId.HasValue)
            {
                var restore = MessageBox.Show(
                    $"{email} belongs to an archived employee.\n\nRestore their previous record (including certificates and training history) into your department?",
                    "Returning Employee", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (restore == DialogResult.Yes)
                {
                    EmployeeAdminService.RestoreEmployee(archivedId.Value, managerGroupId.Value);
                    LoadEmployees();
                    LoadEmployeeList();
                    ClearEmployeeInputs();
                    SelectEmployeeRow(archivedId.Value);
                    MessageBox.Show($"{fullName} has been restored and added to your department.", "Employee Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("That email is still in use by the archived record, so a new account can't be created with it.", "Email In Use");
                }
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("A password is required for new users.");
                return;
            }

            if (!managerGroupId.HasValue)
            {
                MessageBox.Show("You are not currently assigned to manage any groups. Cannot add employee.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 4. Create the employee + linked user account, auto-assigned to the manager's group
            EmployeeAdminService.AddEmployee(fullName, email, role, department, jobTitle, password, managerGroupId);

            // 4. Refresh UI
            LoadEmployees();
            ClearEmployeeInputs();
            MessageBox.Show("Employee added and automatically assigned to your group successfully.");

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

            // Both the user account and the employee record being updated must be the manager's
            bool allowed = ManagerAccessService.CanManageUser(_managerId, userId)
                && (!employeeId.HasValue || ManagerAccessService.CanManageEmployee(_managerId, employeeId.Value));
            if (!EnsureAccess(allowed)) return;

            // 2. Gather Input Data
            string fullName = txtFullName.Text.Trim();
            string email = txtUsername.Text.Trim(); // Assuming this is the email/username field
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            // Get Password Input
            string newPassword = txtPassword.Text.Trim();

            // Department is group membership (managed by Admin), so leave it unchanged here
            EmployeeAdminService.UpdateEmployee(userId, employeeId, fullName, email, role, null, jobTitle, newPassword);

            // 6. Refresh UI
            MessageBox.Show("Employee updated successfully.");
            LoadEmployees();

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

            if (empId.Value == _managerId)
            {
                MessageBox.Show("You can't delete your own account. Please ask an Admin.", "Not Allowed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureAccess(ManagerAccessService.CanManageEmployee(_managerId, empId.Value))) return;

            var confirm = MessageBox.Show(
                "Delete this employee?\n\n" +
                "They will be archived: removed from all active lists and unable to log in, but their record and " +
                "certificates are kept. If they return, add them again with the same email to restore their history.",
                "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            EmployeeAdminService.DeleteEmployee(empId.Value);

            LoadEmployees();
            dgvCertificates.DataSource = null;
            tabCertificates.Enabled = false;
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
            if (!EnsureParticipantsSelected(selectedEmployeeIds)) return;

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

            if (!EnsureAccess(ManagerAccessService.CanManageSession(_managerId, sessionId))) return;
            if (!EnsureParticipantsSelected(selectedEmployeeIds)) return;

            double? hrs = double.TryParse(txtHrsPlan.Text, out double parsedHrs) ? parsedHrs : (double?)null;

            // Shared session + changed details: let the manager choose who the change applies to.
            // Participant-only changes never need asking - other groups' staff are always kept.
            bool splitFromShared = false;
            int otherCount = PlannedTrainingService.GetOtherParticipantCount(_managerId, sessionId);
            if (otherCount > 0 && SessionDetailsChanged(hrs))
            {
                var choice = MessageBox.Show(
                    $"This session is shared with {otherCount} employee(s) from other groups.\n\n" +
                    "Yes - apply the new details to the whole session (everyone attending).\n" +
                    "No - move only your employees into their own session with the new details; the original stays as it is for the other groups.\n" +
                    "Cancel - make no changes.",
                    "Shared Session", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (choice == DialogResult.Cancel) return;
                splitFromShared = choice == DialogResult.No;
            }

            int updatedId = PlannedTrainingService.UpdatePlannedSessionForManager(
                _managerId,
                sessionId,
                txtCertificateNamePlan.Text,
                txtKeyPlan.Text,
                hrs,
                txtProviderPlan.Text,
                dtpPlannedDate.Value,
                textNotesPlan.Text,
                selectedEmployeeIds,
                splitFromShared
            );

            LoadPlannedTraining();
            SelectPlannedSession(updatedId);
        }

        // Compares the edit boxes with the selected session row
        private bool SessionDetailsChanged(double? hrs)
        {
            if (dgvPlannedTraining.CurrentRow?.DataBoundItem is not DataRowView row) return true;

            static string Norm(object? value) => value == null || value == DBNull.Value ? "" : value.ToString()!.Trim();

            double? rowHrs = double.TryParse(Norm(row["HRS"]), out double h) ? h : (double?)null;
            DateTime? rowDate = DateTime.TryParse(Norm(row["PlannedDate"]), out DateTime d) ? d.Date : (DateTime?)null;

            return Norm(row["CertificateName"]) != txtCertificateNamePlan.Text.Trim()
                || Norm(row["Key"]) != txtKeyPlan.Text.Trim()
                || Norm(row["Provider"]) != txtProviderPlan.Text.Trim()
                || Norm(row["Notes"]) != textNotesPlan.Text.Trim()
                || rowHrs != hrs
                || rowDate != dtpPlannedDate.Value.Date;
        }

        private void SelectPlannedSession(int sessionId)
        {
            foreach (DataGridViewRow row in dgvPlannedTraining.Rows)
            {
                if (row.Cells["SessionID"].Value != null && Convert.ToInt32(row.Cells["SessionID"].Value) == sessionId)
                {
                    var firstVisible = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                    if (firstVisible != null) dgvPlannedTraining.CurrentCell = firstVisible;
                    break;
                }
            }
        }

        private void btnDeleteSession_Click(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow == null) return;

            int sessionId = Convert.ToInt32(dgvPlannedTraining.CurrentRow.Cells["SessionID"].Value);

            if (!EnsureAccess(ManagerAccessService.CanManageSession(_managerId, sessionId))) return;

            int otherCount = PlannedTrainingService.GetOtherParticipantCount(_managerId, sessionId);
            string prompt = otherCount > 0
                ? $"This session is shared with {otherCount} employee(s) from other groups.\n\n" +
                  "Your employees will be removed from it, but the session will stay in place for the other groups. Continue?"
                : "Are you sure you want to delete this session?";

            var confirm = MessageBox.Show(prompt, "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            bool deleted = PlannedTrainingService.DeletePlannedSessionForManager(_managerId, sessionId);
            if (!deleted)
            {
                MessageBox.Show("Your employees have been removed from the shared session.", "Session Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            LoadPlannedTraining();
        }

        private void btnCompleteTraining_Click(object sender, EventArgs e)
        {
            if (dgvPlannedTraining.CurrentRow != null)
            {
                int sessionId = Convert.ToInt32(dgvPlannedTraining.CurrentRow.Cells["SessionID"].Value);
                if (!EnsureAccess(ManagerAccessService.CanManageSession(_managerId, sessionId))) return;

                int otherCount = PlannedTrainingService.GetOtherParticipantCount(_managerId, sessionId);
                if (otherCount > 0)
                {
                    var confirm = MessageBox.Show(
                        $"This session is shared with {otherCount} employee(s) from other groups.\n\n" +
                        "It will be marked complete (and certificates created) for your employees only. " +
                        "The other groups' employees stay in the planned session. Continue?",
                        "Shared Session", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm == DialogResult.No) return;
                }

                PlannedTrainingService.CompleteTrainingSessionForManager(_managerId, sessionId);
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

            // Drop anyone outside the manager's groups (e.g. members pulled in via a group lookup)
            selectedEmployees = ManagerAccessService.FilterManagedEmployees(_managerId, selectedEmployees);
            if (selectedEmployees.Count == 0)
            {
                MessageBox.Show("No employees from your groups were selected.", "Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
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

        //Tasks
        private async void btnApproveDeletion_Click(object sender, EventArgs e)
        {
            if (dgvTasks.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvTasks.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvTasks.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "Unknown";

            // FIXED: Using "FileLink" instead of "S3Key" to avoid the crash
            string s3Key = dgvTasks.CurrentRow.Cells["FileLink"].Value?.ToString();

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, certId))) return;

            var confirm = MessageBox.Show($"Permanently delete '{certName}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm == DialogResult.No) return;

            try
            {
                if (!string.IsNullOrEmpty(s3Key))
                {
                    await S3Service.DeleteCertificateAsync(s3Key);
                }

                CertificateService.DeleteCertificate(certId);

                MessageBox.Show("Certificate permanently deleted.");
                LoadDeletionTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving deletion: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRejectDeletion_Click(object sender, EventArgs e)
        {
            if (dgvTasks.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvTasks.CurrentRow.Cells["CertificateID"].Value);
            string certName = dgvTasks.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "Unknown";

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, certId))) return;

            var confirm = MessageBox.Show($"Reject deletion request for '{certName}'? It will be restored to the employee's dashboard.", "Confirm Reject", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (confirm == DialogResult.No) return;

            CertificateService.UnmarkCertificateForDeletion(certId);

            MessageBox.Show("Deletion request rejected. Certificate restored.");
            LoadDeletionTasks();
        }

        private void dgvTasks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);

        private void dgvTasks_CellContentClick(object sender, DataGridViewCellEventArgs e)
            => DashboardHelper.FileLinkCellClick(sender, e);

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

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, ackId))) return;

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

            if (!EnsureAccess(ManagerAccessService.CanManageCertificate(_managerId, certId))) return;

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

        private void dgvCertificates_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
            => DashboardHelper.ApplyStandardRowStyling((DataGridView)sender);

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


        // Helper Functions

        private void StyleAllDGVs()
        {
            // Apply styling to all DGVs
            UIHelpers.StyleDataGridView(dgvPlannedTraining);
            UIHelpers.StyleDataGridView(dgvCertificates);
            UIHelpers.StyleDataGridView(dgvEmployees);
            UIHelpers.StyleDataGridView(dgvReportResults);
            //UIHelpers.StyleDataGridView(dgvGroups);
            //UIHelpers.StyleDataGridView(dgvGroupMembers);

            UIHelpers.RenameColumns(dgvPlannedTraining);
            UIHelpers.RenameColumns(dgvCertificates);
            UIHelpers.RenameColumns(dgvEmployees);
            UIHelpers.RenameColumns(dgvReportResults);
            //UIHelpers.RenameColumns(dgvGroups);
            //UIHelpers.RenameColumns(dgvGroupMembers);

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

            // Remove duplicates and anyone outside the manager's groups
            return ManagerAccessService.FilterManagedEmployees(_managerId, selectedIds);
        }

        private void dgvCertificates_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);

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
                        if (!EnsureAccess(ManagerAccessService.CanManageEmployee(_managerId, currentEmployeeId.Value))) return;

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
                fbd.Description = "Select a folder to save HTSF13 Records for all employees in your groups";

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string saveFolder = fbd.SelectedPath;

                    try
                    {
                        // Manager export - only employees in groups this manager runs
                        bool isAdmin = false;
                        int managerEmployeeId = _managerId;

                        DataTable employeesToExport = LegacyExcelService.GetEmployeesForExport(managerEmployeeId, isAdmin);

                        if (employeesToExport.Rows.Count == 0)
                        {
                            MessageBox.Show("No employees found in your groups.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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