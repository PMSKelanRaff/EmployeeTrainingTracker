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
            tabCertificates.Enabled = false; // Certificates tab disabled until an employee is selected
        }

        // Load all users into DataGridView (Employees tab)
        private void LoadEmployees()
        {
            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("SELECT EmployeeID, Username, Role FROM Users", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        DataTable table = new DataTable();
                        table.Load(reader);
                        dgvEmployees.DataSource = table;
                    }
                }
            }
        }

        // Load certificates for selected employee (Certificates tab)
        private void LoadCertificates(int employeeId)
        {
            // Get the data from the service (database layer)
            DataTable table = CertificateService.GetCertificates(employeeId);

            dgvCertificates.DataSource = table;

            // Add clickable link column for FilePath if it doesn't exist yet
            if (!dgvCertificates.Columns.Contains("FileLink"))
            {
                var linkCol = new DataGridViewLinkColumn
                {
                    Name = "FileLink",
                    HeaderText = "Certificate File",
                    DataPropertyName = "FilePath", // binds to the FilePath column
                    TrackVisitedState = true,
                    UseColumnTextForLinkValue = false
                };
                dgvCertificates.Columns.Add(linkCol);
            }
        }

        // CRUD for certificates
        private void btnAddCert_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string name = txtCertName.Text.Trim();
            DateTime issue = dtpIssueDate.Value;
            DateTime expiry = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            // Pass filePath to service
            filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim();
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
        private void dgvEmployees_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow != null)
            {
                var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"].Value;

                if (empIdObj != null && empIdObj != DBNull.Value)
                {
                    int empId = Convert.ToInt32(empIdObj);
                    LoadCertificates(empId);
                    tabCertificates.Enabled = true; // enable tab here
                }
                else
                {
                    dgvCertificates.DataSource = null;
                    tabCertificates.Enabled = false;
                }
            }
        }

        private void btnAddEmployee_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username and password are required.");
                return;
            }

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("INSERT INTO Users (Username, PasswordHash, Role) VALUES (@u,@p,@r)", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", password); // ⚠️ Replace later with hashed password
                    cmd.Parameters.AddWithValue("@r", role);
                    cmd.ExecuteNonQuery();
                }
            }

            LoadEmployees();       // Refresh the DataGridView
            ClearEmployeeInputs(); // Clear input fields
        }

        private void btnEditEmployee_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;

            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Employee";

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Username is required.");
                return;
            }

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("UPDATE Users SET Username=@u, PasswordHash=@p, Role=@r WHERE EmployeeID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", password); // ⚠️ Later hash
                    cmd.Parameters.AddWithValue("@r", role);
                    cmd.Parameters.AddWithValue("@id", empId);
                    cmd.ExecuteNonQuery();
                }
            }

            LoadEmployees();
            ClearEmployeeInputs();
        }

        private void btnDeleteEmployee_Click(object sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow == null) return;
            int empId = Convert.ToInt32(dgvEmployees.CurrentRow.Cells["EmployeeID"].Value);

            var confirm = MessageBox.Show("Delete this employee and all their certificates?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // Delete certificates first (foreign key dependency)
                using (var cmdCert = new SqliteCommand("DELETE FROM TrainingCertificates WHERE EmployeeID=@id", conn))
                {
                    cmdCert.Parameters.AddWithValue("@id", empId);
                    cmdCert.ExecuteNonQuery();
                }

                // Then delete user
                using (var cmdUser = new SqliteCommand("DELETE FROM Users WHERE EmployeeID=@id", conn))
                {
                    cmdUser.Parameters.AddWithValue("@id", empId);
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
        }

        private void dgvCertificates_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvCertificates.Columns[e.ColumnIndex].Name == "FileLink")
            {
                string? path = dgvCertificates.Rows[e.RowIndex].Cells["FileLink"].Value?.ToString();
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
                    MessageBox.Show("File not found.");
                }
            }
        }



    }
}