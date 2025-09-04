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

                using (var cmd = new SqliteCommand(
                    @"SELECT Users.Username,
                     Users.Role,
                     IFNULL(Employees.Department, 'Unknown') AS Department,
                     IFNULL(Employees.JobTitle, 'Unknown') AS JobTitle,
                     Users.EmployeeID
              FROM Users
              LEFT JOIN Employees ON Users.EmployeeID = Employees.EmployeeID", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);
                        dgvEmployees.DataSource = table;

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

                // Create Employee row with Department and JobTitle from form
                using (var cmdEmp = new SqliteCommand(
                    "INSERT INTO Employees (FullName, Department, JobTitle) VALUES (@name, @dept, @title); SELECT last_insert_rowid();", conn))
                {
                    cmdEmp.Parameters.AddWithValue("@name", username);
                    cmdEmp.Parameters.AddWithValue("@dept", department);
                    cmdEmp.Parameters.AddWithValue("@title", jobTitle);
                    newEmpId = (long)cmdEmp.ExecuteScalar();
                }

                // Create User row linked to the EmployeeID
                using (var cmdUser = new SqliteCommand(
                    "INSERT INTO Users (Username, PasswordHash, Role, EmployeeID) VALUES (@u, @p, @r, @eid); SELECT last_insert_rowid();", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", username);
                    cmdUser.Parameters.AddWithValue("@p", password); // hash later
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

        private void btnEditEmployee_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";
            string department = cmbDept.Text.Trim();   // new TextBox
            string jobTitle = txtJobTitle.Text.Trim();       // new TextBox

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Username is required.");
                return;
            }

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // Update Employee info
                using (var cmdEmp = new SqliteCommand(
                    "UPDATE Employees SET FullName=@name, Department=@dept, JobTitle=@title WHERE EmployeeID=@id", conn))
                {
                    cmdEmp.Parameters.AddWithValue("@name", username);
                    cmdEmp.Parameters.AddWithValue("@dept", string.IsNullOrEmpty(department) ? "Unknown" : department);
                    cmdEmp.Parameters.AddWithValue("@title", string.IsNullOrEmpty(jobTitle) ? "Unknown" : jobTitle);
                    cmdEmp.Parameters.AddWithValue("@id", empId);
                    cmdEmp.ExecuteNonQuery();
                }

                // Update User info
                using (var cmdUser = new SqliteCommand(
                    "UPDATE Users SET Username=@u, PasswordHash=@p, Role=@r WHERE EmployeeID=@id", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", username);
                    cmdUser.Parameters.AddWithValue("@p", password); // hash later
                    cmdUser.Parameters.AddWithValue("@r", role);
                    cmdUser.Parameters.AddWithValue("@id", empId);
                    cmdUser.ExecuteNonQuery();
                }
            }

            LoadEmployees();
            ClearEmployeeInputs();
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

                if (empIdObj != null && empIdObj != DBNull.Value)
                {
                    int empId = Convert.ToInt32(empIdObj);

                    // Load employee details into the input fields
                    using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqliteCommand("SELECT Username, Role, Department, JobTitle FROM Employees INNER JOIN Users ON Employees.EmployeeID = Users.EmployeeID WHERE Employees.EmployeeID=@id", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", empId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    txtUsername.Text = reader["Username"].ToString();
                                    cmbRole.SelectedItem = reader["Role"].ToString();
                                    cmbDept.Text = reader["Department"].ToString();
                                    txtJobTitle.Text = reader["JobTitle"].ToString();
                                }
                            }
                        }
                    }

                    LoadCertificates(empId);
                    tabCertificates.Enabled = true;
                }
                else
                {
                    dgvCertificates.DataSource = null;
                    tabCertificates.Enabled = false;
                    ClearEmployeeInputs();
                }
            }
            else
            {
                ClearEmployeeInputs();
                tabCertificates.Enabled = false;
            }

            // Update certificate buttons
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