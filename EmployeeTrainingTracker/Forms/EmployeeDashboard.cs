using System;
using System.Data;
using Npgsql;
using System.Windows.Forms;
using EmployeeTrainingTracker.Utilities;

namespace EmployeeTrainingTracker
{
    public partial class EmployeeDashboard : Form
    {
        private int employeeId;

        public EmployeeDashboard(int empId)
        {
            InitializeComponent();
            employeeId = empId;
            
        }

        private void EmployeeDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                LoadCertificates(employeeId);

                // Style the DGV
                UIHelpers.StyleDataGridView(dataGridView1);

                // Rename the columns after DataSource is assigned
                UIHelpers.RenameColumns(dataGridView1);
            }
            catch (Exception ex)
            {
                // THIS WILL FINALLY SHOW YOU THE REAL ERROR!
                MessageBox.Show($"A critical error occurred while loading your dashboard:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close(); // Close the form if it fails to load
            }
        }

        // Load certificates for the employee
        private void LoadCertificates(int employeeId)
        {
            // NO CHANGE NEEDED: Assumes CertificateService is refactored
            DataTable table = CertificateService.GetCertificates(employeeId);

            dataGridView1.Columns.Clear();
            dataGridView1.AutoGenerateColumns = false;

            // Hidden ID column (needed for editing/deleting)
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateID",
                DataPropertyName = "CertificateID",
                HeaderText = "ID",
                Visible = false
            });

            // Certificate Name
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateName",
                DataPropertyName = "CertificateName",
                HeaderText = "Certificate Name"
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Key",
                DataPropertyName = "Key",
                HeaderText = "Training Key"
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HRS",
                DataPropertyName = "HRS",
                HeaderText = "CPD Hrs"
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Provider",
                DataPropertyName = "Provider",
                HeaderText = "Provider"
            });

            // Issue Date
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IssueDate",
                DataPropertyName = "IssueDate",
                HeaderText = "Issue Date"
            });

            // Expiry Date
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpiryDate",
                DataPropertyName = "ExpiryDate",
                HeaderText = "Expiry Date"
            });

            // FileLink
            dataGridView1.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "FileLink",
                DataPropertyName = "FilePath",
                HeaderText = "Certificate File",
                TrackVisitedState = true,
                UseColumnTextForLinkValue = false,
                Width = 200
            });

            // Last Notified
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastNotifiedDate",
                DataPropertyName = "LastNotifiedDate",
                HeaderText = "Last Notified"
            });

            // Bind DataSource last
            dataGridView1.DataSource = table;

            // Apply consistent styling
            UIHelpers.StyleDataGridView(dataGridView1);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string certName = txtCertName.Text.Trim();
            string key = txtKey.Text.Trim();
            double.TryParse(txtHrs.Text.Trim(), out double hrs);
            string provider = txtProvider.Text.Trim();
            DateTime issueDate = dtpIssueDate.Value;
            DateTime expiryDate = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();

            if (string.IsNullOrEmpty(certName))
            {
                MessageBox.Show("Certificate name is required.");
                return;
            }

            CertificateService.AddCertificate(employeeId, certName, key, hrs, provider, issueDate, expiryDate, filePath);
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
            string key = txtKey.Text.Trim();
            double.TryParse(txtHrs.Text.Trim(), out double hrs);
            string provider = txtProvider.Text.Trim();
            DateTime issueDate = dtpIssueDate.Value;
            DateTime expiryDate = dtpExpiryDate.Value;
            string? filePath = string.IsNullOrEmpty(txtFilePath.Text.Trim()) ? null : txtFilePath.Text.Trim('"').Trim();

            CertificateService.UpdateCertificate(certId, certName, key, hrs, provider, issueDate, expiryDate, filePath);
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
            ClearInputs();
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

        private void ClearInputs()
        {
            txtCertName.Text = "";
            txtKey.Text = "";
            txtHrs.Text = "";
            txtProvider.Text = "";
            txtFilePath.Text = "";
            dtpIssueDate.Value = DateTime.Today;
            dtpExpiryDate.Value = DateTime.Today;
        }

        // Events

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "FileLink")
            {
                // NOTE: You had "FilePath" here but the column name is "FileLink".
                // I am assuming the DataPropertyName is FilePath, so this should work.
                string? path = dataGridView1.Rows[e.RowIndex].Cells["FileLink"].Value?.ToString();

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

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null || dataGridView1.CurrentRow.IsNewRow)
            {
                txtCertName.Text = "";
                txtKey.Text = "";
                txtHrs.Text = "";
                txtProvider.Text = "";
                dtpIssueDate.Value = DateTime.Today;
                dtpExpiryDate.Value = DateTime.Today;
                txtFilePath.Text = "";
                return;
            }

            if (dataGridView1.CurrentRow.DataBoundItem is not DataRowView rowView) return;

            txtCertName.Text = rowView["CertificateName"]?.ToString() ?? "";
            txtKey.Text = rowView["Key"]?.ToString() ?? "";
            txtHrs.Text = rowView["HRS"]?.ToString() ?? "";
            txtProvider.Text = rowView["Provider"]?.ToString() ?? "";

            if (DateTime.TryParse(rowView["IssueDate"]?.ToString(), out var issue))
                dtpIssueDate.Value = issue;
            else
                dtpIssueDate.Value = DateTime.Today;

            if (DateTime.TryParse(rowView["ExpiryDate"]?.ToString(), out var expiry))
                dtpExpiryDate.Value = expiry;
            else
                dtpExpiryDate.Value = DateTime.Today;

            txtFilePath.Text = rowView["FilePath"]?.ToString() ?? "";
        }

        
    }
}