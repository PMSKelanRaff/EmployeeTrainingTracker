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
            DataTable table = CertificateService.GetCertificates(employeeId);

            dataGridView1.DataSource = table;

            if (dataGridView1.Columns.Contains("FilePath"))
                dataGridView1.Columns["FilePath"].Visible = false;

            // Add clickable link column if not already present
            if (!dataGridView1.Columns.Contains("FileLink"))
            {
                var linkCol = new DataGridViewLinkColumn
                {
                    Name = "FileLink",
                    HeaderText = "Certificate File",
                    DataPropertyName = "FilePath", // bind to hidden FilePath
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

            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();
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
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim();

            CertificateService.UpdateCertificate(certId, certName, issueDate, expiryDate, filePath);

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

        private void ClearInputs()
        {
            txtCertName.Text = "";
            dtpIssueDate.Value = DateTime.Today;
            dtpExpiryDate.Value = DateTime.Today;
        }

        // Events

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "FileLink")
            {
                string? path = dataGridView1.Rows[e.RowIndex].Cells["FilePath"].Value?.ToString();

                if (!string.IsNullOrEmpty(path))
                {
                    // Remove any surrounding quotes
                    path = path.Trim('"');

                    if (System.IO.File.Exists(path))
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
        }

    }
}