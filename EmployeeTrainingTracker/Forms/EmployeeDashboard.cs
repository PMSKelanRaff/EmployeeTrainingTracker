using EmployeeTrainingTracker.Helpers;
using EmployeeTrainingTracker.Utilities;
using Npgsql;
using System;
using System.Data;
using System.Windows.Forms;

namespace EmployeeTrainingTracker
{
    public partial class EmployeeDashboard : Form
    {
        private int employeeId;

        public EmployeeDashboard(int empId)
        {
            InitializeComponent();
            ResponsiveScaler.Attach(this);
            employeeId = empId;

        }

        private void EmployeeDashboard_Load(object sender, EventArgs e)
        {
            try
            {
                dataGridView1.DataBindingComplete += DataGridView1_DataBindingComplete;
                dataGridView1.CellFormatting += dataGridView1_CellFormatting;

                LoadCertificates(employeeId);
                LoadPlannedTraining(employeeId);

                UIHelpers.StyleDataGridView(dataGridView1);
                UIHelpers.RenameColumns(dataGridView1);
                UIHelpers.StyleDataGridView(dgvPlannedTraining);
                UIHelpers.RenameColumns(dgvPlannedTraining);

                dataGridView1.DataBindingComplete += DataGridView1_DataBindingComplete;
                dataGridView1.CellFormatting += dataGridView1_CellFormatting;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A critical error occurred while loading your dashboard:\n\n{ex.Message}\n\n{ex.StackTrace}",
                                "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        // Load certificates for the employee
        private void LoadCertificates(int employeeId)
        {
            DataTable table = CertificateService.GetCertificates(employeeId);
            DashboardHelper.BindCertificates(dataGridView1, table, includeDeletionFlagColumn: true, sortPendingDeletionLast: true);
        }

        private void LoadPlannedTraining(int employeeId)
        {
            // 1. Define the SQL Query
            // We join TrainingSessions to TrainingParticipants and filter by the employeeId.
            string sql = @"
        SELECT
            ts.SessionID,
            ts.CertificateName,
            ts.HRS,
            ts.Provider,
            ts.PlannedDate,
            ts.Status,
            ts.Notes
        FROM public.TrainingSessions ts
        INNER JOIN public.TrainingParticipants tp ON ts.SessionID = tp.SessionID
        WHERE tp.EmployeeID = @EmployeeID
        ORDER BY ts.PlannedDate;";

            DataTable table = new DataTable();

            try
            {
                // 2. Execute the Query
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        // Add the parameter for the logged-in employee ID
                        cmd.Parameters.AddWithValue("EmployeeID", employeeId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            table.Load(reader);
                        }
                    }
                }

                // 3. Configure the DataGridView
                dgvPlannedTraining.Columns.Clear();
                dgvPlannedTraining.AutoGenerateColumns = false;
                dgvPlannedTraining.DataSource = table;

                // Add Columns (Match the SELECT statement aliases)
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "SessionID", DataPropertyName = "SessionID", HeaderText = "ID", Visible = false });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateName", DataPropertyName = "CertificateName", HeaderText = "Training Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "HRS", DataPropertyName = "HRS", HeaderText = "CPD Hrs" });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", DataPropertyName = "Provider", HeaderText = "Provider" });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "PlannedDate", DataPropertyName = "PlannedDate", HeaderText = "Planned Date" });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "Status" });
                dgvPlannedTraining.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", DataPropertyName = "Notes", HeaderText = "Notes", Visible = false });

                // Apply consistent styling (assuming UIHelpers.StyleDataGridView exists)
                UIHelpers.StyleDataGridView(dgvPlannedTraining);
                UIHelpers.RenameColumns(dgvPlannedTraining); // If you still want to run your renaming utility
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading planned training sessions:\n{ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.jpg;*.png)|*.jpg;*.png|All Files (*.*)|*.*";
                ofd.Title = "Select a Certificate to Upload";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string localFilePath = ofd.FileName;

                    string certName = txtCertName.Text.Trim();
                    string key = string.IsNullOrWhiteSpace(txtKey.Text) ? string.Empty : txtKey.Text.Trim()[0].ToString();
                    double.TryParse(txtHrs.Text.Trim(), out double hrs);
                    string provider = txtProvider.Text.Trim();
                    DateTime issueDate = dtpIssueDate.Value.Date;
                    DateTime expiryDate = dtpExpiryDate.Value.Date;

                    if (string.IsNullOrEmpty(certName))
                    {
                        MessageBox.Show("Please enter a name for the certificate.");
                        return;
                    }

                    // We pass an empty string for the name because the employee dashboard doesn't track it.
                    // S3Service will automatically default to "Unknown_123/file.pdf", which works perfectly!
                    bool success = await CertificateService.SaveCertificateAsync(
                        employeeId, "", certName, localFilePath,
                        issueDate, expiryDate, key, hrs, provider);

                    if (success)
                    {
                        MessageBox.Show("Certificate successfully uploaded to the cloud!");
                        LoadCertificates(employeeId);
                        ClearInputs();
                    }
                    else
                    {
                        MessageBox.Show("Upload failed. Please try again.");
                    }
                }
            }
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
            string key = string.IsNullOrWhiteSpace(txtKey.Text)
                ? string.Empty
                : txtKey.Text.Trim()[0].ToString();
            double.TryParse(txtHrs.Text.Trim(), out double hrs);
            string provider = txtProvider.Text.Trim();

            // Check dates before processing
            if (dtpExpiryDate.Checked && dtpExpiryDate.Value.Date < dtpIssueDate.Value.Date)
            {
                MessageBox.Show("Expiry Date cannot be earlier than Issue Date.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string issueDate = dtpIssueDate.Value.ToString("yyyy-MM-dd");
            string? expiryDate = dtpExpiryDate.Checked ? dtpExpiryDate.Value.ToString("yyyy-MM-dd") : null;

            // UPDATE THIS LINE to just pass null at the end instead of filePath:
            CertificateService.UpdateCertificate(certId, certName, key, hrs, provider, issueDate, expiryDate, null);


            LoadCertificates(employeeId);
            ClearInputs();
        }

        private void btnRequestDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null)
            {
                MessageBox.Show("Please select a certificate.");
                return;
            }

            int certId = Convert.ToInt32(dataGridView1.CurrentRow.Cells["CertificateID"].Value);
            bool isMarked = false;

            // Check the current status of the selected row
            if (dataGridView1.CurrentRow.DataBoundItem is DataRowView rowView &&
                rowView.Row.Table.Columns.Contains("ismarkedfordeletion"))
            {
                isMarked = rowView["ismarkedfordeletion"] != DBNull.Value &&
                           Convert.ToBoolean(rowView["ismarkedfordeletion"]);
            }

            if (isMarked)
            {
                // Execute Revoke Logic
                var confirm = MessageBox.Show("Cancel your deletion request and keep this certificate?", "Revoke Request", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (confirm == DialogResult.No) return;

                CertificateService.UnmarkCertificateForDeletion(certId);
                MessageBox.Show("Deletion request revoked. The certificate is active again.");
            }
            else
            {
                // Execute Deletion Request Logic
                var confirm = MessageBox.Show("Request manager approval to delete this certificate?", "Confirm Request", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm == DialogResult.No) return;

                CertificateService.MarkCertificateForDeletion(certId);
                MessageBox.Show("Certificate flagged for deletion. It will be removed once a manager approves.");
            }

            // Refresh UI
            LoadCertificates(employeeId);
            ClearInputs();
        }


        private void ClearInputs()
        {
            txtCertName.Text = "";
            txtKey.Text = "";
            txtHrs.Text = "";
            txtProvider.Text = "";
            dtpIssueDate.Value = DateTime.Today;
            dtpExpiryDate.Value = DateTime.Today;
        }

        // Events
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
            => DashboardHelper.FileLinkCellClick(sender, e);

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentRow == null || dataGridView1.CurrentRow.IsNewRow)
            {
                // Reset inputs
                txtCertName.Text = "";
                txtKey.Text = "";
                txtHrs.Text = "";
                txtProvider.Text = "";
                dtpIssueDate.Value = DateTime.Today;
                dtpExpiryDate.Value = DateTime.Today;

                // DELETED: txtFilePath.Text = ""; <-- This was causing the crash!

                // Reset Button
                btnRequestDelete.Text = "Request Deletion";
                return;
            }

            if (dataGridView1.CurrentRow.DataBoundItem is not DataRowView rowView) return;

            // Populate inputs
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

            // DELETED: txtFilePath.Text = rowView["FilePath"]?.ToString() ?? ""; <-- This would have crashed too!

            // TOGGLE BUTTON TEXT LOGIC
            if (rowView.Row.Table.Columns.Contains("ismarkedfordeletion"))
            {
                bool isMarked = rowView["ismarkedfordeletion"] != DBNull.Value &&
                                Convert.ToBoolean(rowView["ismarkedfordeletion"]);

                btnRequestDelete.Text = isMarked ? "Revoke Deletion" : "Request Deletion";
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
            => DashboardHelper.FileLinkCellFormatting(sender, e);

        private void DataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
            => DashboardHelper.ApplyStandardRowStyling((DataGridView)sender);


    }
}