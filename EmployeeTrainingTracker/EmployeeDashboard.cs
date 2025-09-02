using System;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Windows.Forms;

namespace EmployeeTrainingTracker
{
    public partial class EmployeeDashboard : Form
    {
        private int employeeId;

        public EmployeeDashboard(int empId)
        {
            InitializeComponent();
            employeeId = empId;
            LoadCertificates(employeeId);
        }

        // Load certificates for the employee
        private void LoadCertificates(int employeeId)
        {
            // Get the data from the service (database layer)
            DataTable table = CertificateService.GetCertificates(employeeId);

            dataGridView1.DataSource = table;

            // Add clickable link column for FilePath if it doesn't exist yet
            if (!dataGridView1.Columns.Contains("FileLink"))
            {
                var linkCol = new DataGridViewLinkColumn
                {
                    Name = "FileLink",
                    HeaderText = "Certificate File",
                    DataPropertyName = "FilePath",
                    TrackVisitedState = true,
                    UseColumnTextForLinkValue = false
                };
                dataGridView1.Columns.Add(linkCol);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string certName = txtCertName.Text.Trim();
            DateTime issueDate = dtpIssueDate.Value;
            DateTime expiryDate = dtpExpiryDate.Value;

            if (string.IsNullOrEmpty(certName))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim();
            CertificateService.AddCertificate(employeeId, certName, issueDate, expiryDate, filePath);

            LoadCertificates(employeeId);
            ClearInputs();
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null)
            {
                MessageBox.Show("Please select a certificate to edit.");
                return;
            }

            int certId = Convert.ToInt32(dataGridView1.CurrentRow.Cells["CertificateID"].Value);
            string certName = txtCertName.Text.Trim();
            DateTime issueDate = dtpIssueDate.Value;
            DateTime expiryDate = dtpExpiryDate.Value;

            CertificateService.UpdateCertificate(certId, certName, issueDate, expiryDate);

            LoadCertificates(employeeId);
            ClearInputs();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null)
            {
                MessageBox.Show("Please select a certificate to delete.");
                return;
            }

            int certId = Convert.ToInt32(dataGridView1.CurrentRow.Cells["CertificateID"].Value);

            var confirm = MessageBox.Show("Are you sure you want to delete this certificate?", "Confirm", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.No) return;

            CertificateService.DeleteCertificate(certId);

            LoadCertificates(employeeId);
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow != null)
            {
                txtCertName.Text = dataGridView1.CurrentRow.Cells["CertificateName"].Value?.ToString() ?? "";
                if (DateTime.TryParse(dataGridView1.CurrentRow.Cells["IssueDate"].Value?.ToString(), out DateTime issue))
                    dtpIssueDate.Value = issue;
                if (DateTime.TryParse(dataGridView1.CurrentRow.Cells["ExpiryDate"].Value?.ToString(), out DateTime expiry))
                    dtpExpiryDate.Value = expiry;
            }
        }

        private void ClearInputs()
        {
            txtCertName.Text = "";
            dtpIssueDate.Value = DateTime.Today;
            dtpExpiryDate.Value = DateTime.Today;
        }

        private void dgvCertificates_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "FileLink")
            {
                string? path = dataGridView1.Rows[e.RowIndex].Cells["FileLink"].Value?.ToString();
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