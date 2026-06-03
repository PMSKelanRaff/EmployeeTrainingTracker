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
            _managerId = managerId;
        }

        // Add this helper method right below the constructor
        private string GetManagerDepartment(int empId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT Department FROM Employees WHERE EmployeeID = $1", conn);
            cmd.Parameters.AddWithValue(empId);
            return cmd.ExecuteScalar()?.ToString();
        }

        private void SetCurrentEmployeeName(string employeeName)
        {
            currentEmployeeName = employeeName;
        }

        private void ManagerDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                // 1. Authenticate Department
                _managerDepartment = GetManagerDepartment(_managerId);
                this.Text = $"Manager Dashboard - {_managerDepartment}";

                if (string.IsNullOrEmpty(_managerDepartment) || _managerDepartment == "Unknown")
                {
                    MessageBox.Show("Your account is not assigned to a valid department.", "Access Denied");
                    this.Close();
                    return;
                }

                // 2. Lock Sensitive Controls (Managers can't change these)
                cmbRole.Enabled = false;
                cmbDept.Enabled = false;

                // 3. Load Restricted Data
                LoadEmployees(); // Now uses the restricted version below
                LoadEmployeeList();
                LoadPlannedTraining();

                tabCertificates.Enabled = false;
                LoadReportSettings();
                StyleAllDGVs();

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

            // NEW SQL: Filters by the Group hierarchy instead of a text-based Department match
            string query = @"
        SELECT DISTINCT
            u.UserID,
            COALESCE(e.FullName, u.Email) AS FullName,
            u.Email AS Email,
            u.Role,
            e.EmployeeID,
            COALESCE(e.Department, 'Unknown') AS Department,
            COALESCE(e.JobTitle, 'Unknown') AS JobTitle
        FROM Users u
        INNER JOIN Employees e ON u.EmployeeID = e.EmployeeID
        INNER JOIN GroupMembers gm ON e.EmployeeID = gm.EmployeeID
        INNER JOIN Groups g ON gm.GroupID = g.GroupID
        WHERE g.ManagerID = $1
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
                HeaderText = "Issue Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } // <--- ADD THIS LINE
            });

            // Expiry Date
            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpiryDate",
                DataPropertyName = "ExpiryDate",
                HeaderText = "Expiry Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } // <--- ADD THIS LINE
            });

            // Inside LoadCertificates(int employeeId)
            dgvCertificates.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "FileLink",
                DataPropertyName = "S3Key",
                HeaderText = "Certificate File",
                TrackVisitedState = true,
                Width = 200
            });

            dgvCertificates.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastNotifiedDate",
                DataPropertyName = "LastNotifiedDate",
                HeaderText = "Last Notified"
            });



            // Set DataSource last
            dgvCertificates.DataSource = table;

            if (dgvCertificates.Columns.Contains("CertificateID"))
            {
                dgvCertificates.Columns["CertificateID"].Visible = false;
            }

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

            string issue = dtpIssueDate.Value.ToString("yyyy-MM-dd");
            string? expiry = dtpExpiryDate.Checked ? dtpExpiryDate.Value.ToString("yyyy-MM-dd") : null;
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

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("A password is required for new users.");
                return;
            }

            // 3. Hash the password
            string passwordHash = HashPassword(password);

            long newEmpId;
            long newUserId;

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // --- STEP 1: Find the Manager's GroupID ---
                int? managerGroupId = null;
                using (var cmdGroup = new NpgsqlCommand("SELECT GroupID FROM Groups WHERE ManagerID = $1 LIMIT 1;", conn))
                {
                    cmdGroup.Parameters.AddWithValue(_managerId);
                    var result = cmdGroup.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        managerGroupId = Convert.ToInt32(result);
                    }
                }

                // If for some reason this manager isn't assigned a group in the database, stop here.
                if (!managerGroupId.HasValue)
                {
                    MessageBox.Show("You are not currently assigned to manage any groups. Cannot add employee.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // --- STEP 2: Insert into Employees Table ---
                using (var cmdEmp = new NpgsqlCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1, $2, $3) RETURNING EmployeeID;", conn))
                {
                    cmdEmp.Parameters.AddWithValue(fullName);
                    cmdEmp.Parameters.AddWithValue(department);
                    cmdEmp.Parameters.AddWithValue(jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }

                // --- STEP 3: Insert into Users Table ---
                using (var cmdUser = new NpgsqlCommand(
                    "INSERT INTO Users (Email, Role, EmployeeID, passwordHash) VALUES ($1, $2, $3, $4) RETURNING UserID;", conn))
                {
                    cmdUser.Parameters.AddWithValue(email);
                    cmdUser.Parameters.AddWithValue(role);
                    cmdUser.Parameters.AddWithValue(newEmpId);
                    cmdUser.Parameters.AddWithValue(passwordHash);
                    newUserId = (long)cmdUser.ExecuteScalar();
                }

                // --- STEP 4: Insert into GroupMembers Table ---
                using (var cmdMember = new NpgsqlCommand(
                    "INSERT INTO GroupMembers (GroupID, EmployeeID) VALUES ($1, $2);", conn))
                {
                    cmdMember.Parameters.AddWithValue(managerGroupId.Value);
                    cmdMember.Parameters.AddWithValue(newEmpId);
                    cmdMember.ExecuteNonQuery();
                }
            }

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

            // 2. Gather Input Data
            string fullName = txtFullName.Text.Trim();
            string email = txtUsername.Text.Trim(); // Assuming this is the email/username field
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            // Get Password Input
            string newPassword = txtPassword.Text.Trim();

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // 3. Handle Employee Table (Insert or Update)
                if (employeeId.HasValue)
                {
                    // Update existing employee profile
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
                    // Insert new employee profile
                    using (var cmdInsertEmp = new NpgsqlCommand(
                        "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1,$2,$3) RETURNING EmployeeID;", conn))
                    {
                        cmdInsertEmp.Parameters.AddWithValue(fullName);
                        cmdInsertEmp.Parameters.AddWithValue(department);
                        cmdInsertEmp.Parameters.AddWithValue(jobTitle);

                        long newEmpId = (long)cmdInsertEmp.ExecuteScalar();

                        // Link the existing User to this new Employee record
                        using (var cmdUpdateUserEmp = new NpgsqlCommand(
                            "UPDATE Users SET EmployeeID=$1 WHERE UserID=$2", conn))
                        {
                            cmdUpdateUserEmp.Parameters.AddWithValue(newEmpId);
                            cmdUpdateUserEmp.Parameters.AddWithValue(userId);
                            cmdUpdateUserEmp.ExecuteNonQuery();
                        }
                    }
                }

                // 4. Update User Email and Role (Always runs)
                using (var cmdUser = new NpgsqlCommand(
                    "UPDATE Users SET Email=$1, Role=$2 WHERE UserID=$3", conn))
                {
                    cmdUser.Parameters.AddWithValue(email);
                    cmdUser.Parameters.AddWithValue(role);
                    cmdUser.Parameters.AddWithValue(userId);
                    cmdUser.ExecuteNonQuery();
                }

                // 5. Update Password (ONLY if the textbox is not empty)
                if (!string.IsNullOrEmpty(newPassword))
                {
                    // Hash the password using your helper method
                    string hashedPassword = HashPassword(newPassword);

                    using (var cmdPass = new NpgsqlCommand(
                        "UPDATE Users SET passwordHash=$1 WHERE UserID=$2", conn))
                    {
                        cmdPass.Parameters.AddWithValue(hashedPassword);
                        cmdPass.Parameters.AddWithValue(userId);
                        cmdPass.ExecuteNonQuery();
                    }
                }
            }

            // 6. Refresh UI
            MessageBox.Show("Employee updated successfully.");
            LoadEmployees();

            // Clear the password box so it doesn't stay on screen
            txtPassword.Text = "";
        }

        private string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
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

                // ---------------------------------------------------------
                // 1. NEW: Unassign them as Manager from any Groups
                // ---------------------------------------------------------
                using (var cmdUnassign = new NpgsqlCommand("UPDATE Groups SET ManagerID = NULL WHERE ManagerID = $1", conn))
                {
                    cmdUnassign.Parameters.AddWithValue(empId.Value);
                    cmdUnassign.ExecuteNonQuery();
                }
                // ---------------------------------------------------------

                // 2. Delete certificates (Uncomment this if you don't have CASCADE delete in DB)
                using (var cmdCert = new NpgsqlCommand("DELETE FROM TrainingCertificates WHERE EmployeeID=$1", conn))
                {
                    cmdCert.Parameters.AddWithValue(empId.Value);
                    cmdCert.ExecuteNonQuery();
                }

                // 3. Delete from GroupMembers (Just in case they are also a member)
                using (var cmdMembers = new NpgsqlCommand("DELETE FROM GroupMembers WHERE EmployeeID=$1", conn))
                {
                    cmdMembers.Parameters.AddWithValue(empId.Value);
                    cmdMembers.ExecuteNonQuery();
                }

                // 4. Delete user account
                using (var cmdUser = new NpgsqlCommand("DELETE FROM Users WHERE EmployeeID=$1", conn))
                {
                    cmdUser.Parameters.AddWithValue(empId.Value);
                    cmdUser.ExecuteNonQuery();
                }

                // 5. Finally, delete the employee record
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

            // Grab the S3Key instead of the old FilePath
            string? s3Key = rowView["S3Key"]?.ToString();

            if (string.IsNullOrEmpty(s3Key))
            {
                MessageBox.Show("No cloud file linked for this certificate.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 1. Generate the secure Presigned URL from AWS
                string secureUrl = S3Service.GetSecureViewUrl(s3Key);

                // 2. Open the URL in the default web browser
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = secureUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the file from the cloud:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

        private void dgvCertificates_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Check if the column being drawn is our FileLink column
            if (dgvCertificates.Columns[e.ColumnIndex].Name == "FileLink" && e.Value != null)
            {
                string fullS3Key = e.Value.ToString() ?? "";

                // System.IO.Path.GetFileName automatically strips off the folder path!
                string cleanFileName = System.IO.Path.GetFileName(fullS3Key);

                e.Value = cleanFileName;
                e.FormattingApplied = true;
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
    }

}