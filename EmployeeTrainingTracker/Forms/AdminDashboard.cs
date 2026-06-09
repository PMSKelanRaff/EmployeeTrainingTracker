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
    public partial class AdminDashboard : Form
    {

        private string currentEmployeeName = "None";

        private bool _loadingManagerCombo = false;
        private bool _isSyncingSelection = false; // Add this line

        private void SetCurrentEmployeeName(string employeeName)
        {
            currentEmployeeName = employeeName;
        }

        public AdminDashboard()
        {
            InitializeComponent();

        }

        private void AdminDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                LoadEmployees();
                LoadGroupsForReports();
                LoadEmployeeList();
                LoadPlannedTraining();
                LoadDeletionTasks();
                tabCertificates.Enabled = false;
                LoadReportSettings();
                StyleAllDGVs();
                LoadManagers();
                LoadGroups();

                cbManager.SelectedIndexChanged += cbManager_SelectedIndexChanged;

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
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                var cmd = new NpgsqlCommand("SELECT EmployeeID, FullName FROM Employees ORDER BY FullName", conn);
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
            if (dgvGroups.Rows.Count > 0)
                dgvGroups.Rows[0].Selected = true;
        }

        private void LoadManagers()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            string query = @"
            SELECT EmployeeID, FullName 
            FROM Employees
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

                dgvTasks.Columns.Clear();
                dgvTasks.AutoGenerateColumns = false;

                // Hidden ID
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateID", DataPropertyName = "CertificateID", Visible = false });

                // Employee Name (Crucial for the Admin to see)
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "Employee Name" });

                // Certificate Details
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateName", DataPropertyName = "CertificateName", HeaderText = "Certificate Name" });
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", DataPropertyName = "Key", HeaderText = "Training Key" });
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "HRS", DataPropertyName = "HRS", HeaderText = "CPD Hrs" });
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", DataPropertyName = "Provider", HeaderText = "Provider" });

                // Dates
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssueDate", DataPropertyName = "IssueDate", HeaderText = "Issue Date", DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpiryDate", DataPropertyName = "ExpiryDate", HeaderText = "Expiry Date", DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });

                // File Link
                dgvTasks.Columns.Add(new DataGridViewLinkColumn { Name = "FileLink", DataPropertyName = "S3Key", HeaderText = "Certificate File", TrackVisitedState = true, Width = 200 });

                // Notified Date
                dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastNotifiedDate", DataPropertyName = "LastNotifiedDate", HeaderText = "Last Notified" });

                dgvTasks.DataSource = pendingDeletions;

                dgvTasks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                UIHelpers.StyleDataGridView(dgvTasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tasks: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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


        //public static async Task MigrateOldCertificatesAsync()
        //{
        //    int successCount = 0;
        //    int failCount = 0;

        //    using (var conn = DatabaseHelper.GetConnection())
        //    {
        //        await conn.OpenAsync();

        //        string selectSql = @"
        //    SELECT tc.CertificateID, tc.EmployeeID, tc.FilePath, e.FullName 
        //    FROM TrainingCertificates tc
        //    JOIN Employees e ON tc.EmployeeID = e.EmployeeID
        //    WHERE tc.S3Key IS NULL AND tc.FilePath IS NOT NULL";

        //        using (var cmd = new NpgsqlCommand(selectSql, conn))
        //        using (var reader = await cmd.ExecuteReaderAsync())
        //        {
        //            var recordsToMigrate = new List<(int CertId, int EmpId, string Path, string EmpName)>();
        //            while (await reader.ReadAsync())
        //            {
        //                recordsToMigrate.Add((
        //                    reader.GetInt32(0),
        //                    reader.GetInt32(1),
        //                    reader.GetString(2),
        //                    reader.GetString(3)
        //                ));
        //            }
        //            reader.Close();

        //            foreach (var record in recordsToMigrate)
        //            {
        //                string actualPath = record.Path;

        //                // 1. Check if the file exists at the exact database path
        //                if (!System.IO.File.Exists(actualPath))
        //                {
        //                    // FALLBACK: Did someone move it to the "Obsolete" folder?
        //                    string directory = System.IO.Path.GetDirectoryName(record.Path);
        //                    string fileName = System.IO.Path.GetFileName(record.Path);
        //                    string obsoletePath = System.IO.Path.Combine(directory, "Obsolete", fileName);

        //                    if (System.IO.File.Exists(obsoletePath))
        //                    {
        //                        // We found it! Update the path so we upload the right file.
        //                        actualPath = obsoletePath;
        //                    }
        //                    else
        //                    {
        //                        // It's genuinely missing from both places. Skip it.
        //                        failCount++;
        //                        continue;
        //                    }
        //                }

        //                // Now use 'actualPath' for the rest of the logic
        //                string finalFileName = System.IO.Path.GetFileName(actualPath);
        //                string cleanName = record.EmpName.Replace(" ", "_");
        //                string s3Key = $"{cleanName}_{record.EmpId}/{finalFileName}";

        //                // 2. Try to upload to S3 using the corrected path
        //                bool uploaded = await S3Service.UploadCertificateAsync(actualPath, s3Key);

        //                if (uploaded)
        //                {
        //                    using (var updateCmd = new NpgsqlCommand("UPDATE TrainingCertificates SET S3Key = @key WHERE CertificateID = @id", conn))
        //                    {
        //                        updateCmd.Parameters.AddWithValue("key", s3Key);
        //                        updateCmd.Parameters.AddWithValue("id", record.CertId);
        //                        await updateCmd.ExecuteNonQueryAsync();
        //                    }
        //                    successCount++;
        //                }
        //                else
        //                {
        //                    System.Windows.Forms.MessageBox.Show(
        //                        $"Migration stopped.\n\nThe file exists locally, but the S3 Upload failed for:\n{actualPath}",
        //                        "Diagnostic: S3 Upload Failed");
        //                    return;
        //                }
        //            }
        //        }
        //    }

        //    System.Windows.Forms.MessageBox.Show($"Migration Complete!\nSuccess: {successCount}\nFailed/Missing: {failCount}");
        //}

        // CRUD for employees

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

                // 4. Insert into Employees Table
                using (var cmdEmp = new NpgsqlCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES ($1, $2, $3) RETURNING EmployeeID;", conn))
                {
                    cmdEmp.Parameters.AddWithValue(fullName);   // Use the variable, not the Windows User!
                    cmdEmp.Parameters.AddWithValue(department);
                    cmdEmp.Parameters.AddWithValue(jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }

                // 5. Insert into Users Table (Now including PasswordHash)
                using (var cmdUser = new NpgsqlCommand(
                    "INSERT INTO Users (Email, Role, EmployeeID, passwordHash) VALUES ($1, $2, $3, $4) RETURNING UserID;", conn))
                {
                    cmdUser.Parameters.AddWithValue(email);     // Use the variable!
                    cmdUser.Parameters.AddWithValue(role);
                    cmdUser.Parameters.AddWithValue(newEmpId);
                    cmdUser.Parameters.AddWithValue(passwordHash); // Save the hashed password
                    newUserId = (long)cmdUser.ExecuteScalar();
                }
            }

            // 6. Refresh UI
            LoadEmployees();
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

        private void btnEditGroup_Click(object sender, EventArgs e)
        {
            if (dgvGroups.SelectedRows.Count == 0)
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
            if (dgvGroups.SelectedRows.Count == 0)
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
            if (dgvGroups.SelectedRows.Count == 0)
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
            if (dgvGroups.SelectedRows.Count == 0 || dgvGroupMembers.SelectedRows.Count == 0)
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

        private void cbManager_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingManagerCombo) return; // skip if combo is still loading
            if (dgvGroups.SelectedRows.Count == 0) return;
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

                int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupID"].Value);

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

        private void dgvTasks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Clean up the S3 File Name exactly like the other grids
            if (dgvTasks.Columns[e.ColumnIndex].Name == "FileLink" && e.Value != null)
            {
                string fullS3Key = e.Value.ToString() ?? "";
                e.Value = System.IO.Path.GetFileName(fullS3Key);
                e.FormattingApplied = true;
            }
        }

        private void dgvTasks_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dgvTasks.Columns[e.ColumnIndex].Name == "FileLink")
            {
                if (dgvTasks.Rows[e.RowIndex].DataBoundItem is not DataRowView rowView) return;

                string? s3Key = rowView["S3Key"]?.ToString();

                if (string.IsNullOrEmpty(s3Key))
                {
                    MessageBox.Show("No cloud file linked for this certificate.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    // Open the file securely
                    string secureUrl = S3Service.GetSecureViewUrl(s3Key);
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
        }
    }

}