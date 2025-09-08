using System;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Windows.Forms;

namespace EmployeeTrainingTracker
{
    public partial class AdminDashboard : Form
    {
        public AdminDashboard()
        {
            InitializeComponent();
            LoadEmployees();
            tabCertificates.Enabled = false;
        }

        // Load all users into DataGridView (Employees tab)
        private void LoadEmployees()
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                using (var cmd = new SqliteCommand(@"
            SELECT 
                u.UserID,
                u.Username,
                u.Role,
                e.EmployeeID,
                IFNULL(e.FullName, u.Username) AS FullName,
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
                    }
                }
            }
        }


        // Load certificates for selected employee (Certificates tab)
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
                TrackVisitedState = true
            });

            // Set DataSource last
            dgvCertificates.DataSource = table;

            // update buttons correctly
            UpdateCertificateButtons();
        }


        // CRUD for certificates
        private void btnAddCert_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string name = txtCertName.Text.Trim();
            DateTime issue = dtpIssueDate.Value;
            DateTime expiry = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            // Pass filePath to service
            filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();
            CertificateService.AddCertificate(empId, name, issue, expiry, filePath);

            LoadCertificates(empId);
        }

        private void btnEditCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            string name = txtCertName.Text.Trim();
            DateTime issue = dtpIssueDate.Value;
            DateTime expiry = dtpExpiryDate.Value;

            CertificateService.UpdateCertificate(certId, name, issue, expiry);
            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            LoadCertificates(empId);
        }

        private void btnDeleteCert_Click(object sender, EventArgs e)
        {
            if (dgvCertificates.CurrentRow == null) return;

            int certId = Convert.ToInt32(dgvCertificates.CurrentRow.Cells["CertificateID"].Value);
            var confirm = MessageBox.Show("Delete this certificate?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            CertificateService.DeleteCertificate(certId);
            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            LoadCertificates(empId);
        }


        // CRUD for employees
        private void btnAddEmployee_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = string.IsNullOrEmpty(cmbDept.Text.Trim()) ? "Unknown" : cmbDept.Text.Trim();
            string jobTitle = string.IsNullOrEmpty(txtJobTitle.Text.Trim()) ? "Unknown" : txtJobTitle.Text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username and password are required.");
                return;
            }

            long newEmpId;
            long newUserId;

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // Insert Employee first
                using (var cmdEmp = new SqliteCommand(
                    @"INSERT INTO Employees (FullName, Department, JobTitle) 
              VALUES (@name, @dept, @title); 
              SELECT last_insert_rowid();", conn))
                {
                    cmdEmp.Parameters.AddWithValue("@name", username);
                    cmdEmp.Parameters.AddWithValue("@dept", department);
                    cmdEmp.Parameters.AddWithValue("@title", jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }

                // Insert User linked to Employee
                using (var cmdUser = new SqliteCommand(
                    @"INSERT INTO Users (Username, PasswordHash, Role, EmployeeID) 
              VALUES (@u, @p, @r, @eid); 
              SELECT last_insert_rowid();", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", username);
                    cmdUser.Parameters.AddWithValue("@p", password); // TODO: hash later
                    cmdUser.Parameters.AddWithValue("@r", role);
                    cmdUser.Parameters.AddWithValue("@eid", newEmpId);
                    newUserId = (long)cmdUser.ExecuteScalar();
                }
            }

            // Refresh UI
            LoadEmployees();
            ClearEmployeeInputs();

            // Auto-select the newly added row
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
                    "UPDATE Users SET Username=@u, Role=@r WHERE UserID=@uid", conn))
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
                    txtUsername.Text = dgvEmployees.CurrentRow.Cells["Username"].Value?.ToString();
                    cmbRole.SelectedItem = dgvEmployees.CurrentRow.Cells["Role"].Value?.ToString();
                    cmbDept.Text = dgvEmployees.CurrentRow.Cells["Department"].Value?.ToString();
                    txtJobTitle.Text = dgvEmployees.CurrentRow.Cells["JobTitle"].Value?.ToString();

                    // If EmployeeID exists, load certificates
                    if (empIdObj != null && empIdObj != DBNull.Value)
                    {
                        int empId = Convert.ToInt32(empIdObj);
                        LoadCertificates(empId);
                        tabCertificates.Enabled = true;
                    }
                    else
                    {
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


        private void dgvCertificates_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // 🚨 Defensive guards
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return; // Ignore header or invalid clicks
            if (e.ColumnIndex >= dgvCertificates.Columns.Count) return; // Prevent out-of-range

            if (dgvCertificates.Columns[e.ColumnIndex].Name == "FileLink")
            {
                if (dgvCertificates.Rows[e.RowIndex].DataBoundItem is not DataRowView rowView) return;

                string? path = rowView["FilePath"]?.ToString()?.Trim('"').Trim();

                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"File not found:\n{path}");
                }
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


    }
}