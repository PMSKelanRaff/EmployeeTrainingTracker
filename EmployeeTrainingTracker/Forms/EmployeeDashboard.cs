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

            dataGridView1.Columns.Clear();
            dataGridView1.AutoGenerateColumns = false;

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
                HeaderText = "Issue Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } // <--- ADD THIS LINE
            });

            // Expiry Date
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpiryDate",
                DataPropertyName = "ExpiryDate",
                HeaderText = "Expiry Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } // <--- ADD THIS LINE
            });

            // FileLink
            dataGridView1.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "FileLink",
                DataPropertyName = "S3Key", // <--- CHANGED THIS TO S3Key
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

            // Marked for deletion (necessary for deletion approval functionality)
            dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "IsMarkedForDeletion",
                DataPropertyName = "IsMarkedForDeletion",
                HeaderText = "Pending Deletion",
                Visible = false // Keep it hidden, we only need it for the formatting trigger
            });

            table.DefaultView.Sort = "ismarkedfordeletion ASC, CertificateName ASC";
            // Bind DataSource last
            dataGridView1.DataSource = table;

            if (dataGridView1.Columns.Contains("CertificateID"))
            {
                dataGridView1.Columns["CertificateID"].Visible = false;
            }

            // Apply consistent styling
            UIHelpers.StyleDataGridView(dataGridView1);
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
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dataGridView1.Columns[e.ColumnIndex].Name == "FileLink")
            {
                if (dataGridView1.Rows[e.RowIndex].DataBoundItem is not DataRowView rowView) return;

                string? s3Key = rowView["S3Key"]?.ToString();

                if (string.IsNullOrEmpty(s3Key))
                {
                    MessageBox.Show("No cloud file linked for this certificate.");
                    return;
                }

                try
                {
                    string secureUrl = S3Service.GetSecureViewUrl(s3Key);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = secureUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open file:\n{ex.Message}");
                }
            }
        }

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
                txtFilePath.Text = "";

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

            // TOGGLE BUTTON TEXT LOGIC
            if (rowView.Row.Table.Columns.Contains("ismarkedfordeletion"))
            {
                bool isMarked = rowView["ismarkedfordeletion"] != DBNull.Value &&
                                Convert.ToBoolean(rowView["ismarkedfordeletion"]);

                btnRequestDelete.Text = isMarked ? "Revoke Deletion" : "Request Deletion";
            }
        }

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Ignore header rows or out-of-bounds rows
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView1.Rows.Count) return;

            // Handle the Clean S3 File Names
            if (dataGridView1.Columns[e.ColumnIndex].Name == "FileLink" && e.Value != null)
            {
                string fullS3Key = e.Value.ToString() ?? "";
                e.Value = System.IO.Path.GetFileName(fullS3Key);
                e.FormattingApplied = true;
            }
        }

        private void DataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Call your new centralized helper!
            UIHelpers.ApplyDeletionRowStyling((DataGridView)sender);
        }

        private void LoadAcknowledgements()
        {
            try
            {
                DataTable acks = AcknowledgementService.GetEmployeeAcknowledgements(employeeId);

                dgvAcknowledgements.Columns.Clear();
                dgvAcknowledgements.AutoGenerateColumns = false;

                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "acknowledgementid", DataPropertyName = "acknowledgementid", Visible = false });
                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "trainingtopic", DataPropertyName = "trainingtopic", HeaderText = "Topic / SOP" });
                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "hours", DataPropertyName = "hours", HeaderText = "Hours" });
                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "trainingdate", DataPropertyName = "trainingdate", HeaderText = "Date", DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", DataPropertyName = "status", Visible = false }); // Hidden for color coding
                dgvAcknowledgements.Columns.Add(new DataGridViewTextBoxColumn { Name = "trainername", DataPropertyName = "trainername", HeaderText = "Trainer" });

                dgvAcknowledgements.DataSource = acks;
                dgvAcknowledgements.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                UIHelpers.StyleDataGridView(dgvAcknowledgements);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading TARs: {ex.Message}");
            }
        }

        // Wire this up to the DataBindingComplete event of dgvAcknowledgements
        private void dgvAcknowledgements_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in dgvAcknowledgements.Rows)
            {
                if (row.DataBoundItem is DataRowView rowView && rowView.Row.Table.Columns.Contains("status"))
                {
                    string status = rowView["status"].ToString();

                    if (status == "Pending Manager")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightYellow;
                        row.DefaultCellStyle.ForeColor = Color.Black;
                    }
                    else if (status == "Completed")
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                        row.DefaultCellStyle.ForeColor = Color.Black;
                    }
                }
            }
        }

        private void btnSubmitAck_Click(object sender, EventArgs e)
        {
            string topic = txtTopic.Text.Trim();

            if (string.IsNullOrEmpty(topic))
            {
                MessageBox.Show("Please enter a Training Topic / SOP.");
                return;
            }

            if (!double.TryParse(txtAckHours.Text.Trim(), out double hours))
            {
                MessageBox.Show("Please enter a valid number for Hours.");
                return;
            }

            // Automatically find the manager
            int? managerId = AcknowledgementService.GetEmployeeManagerId(employeeId);
            if (managerId == null)
            {
                MessageBox.Show("You do not have an assigned manager in the system. Cannot route for approval.", "Routing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show("Sign and submit this TAR for manager approval?", "Confirm Signature", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (confirm == DialogResult.No) return;

            try
            {
                // Removed revNo from the method call
                AcknowledgementService.SubmitAcknowledgement(employeeId, managerId.Value, topic, hours, dtpAckDate.Value.Date);

                MessageBox.Show("Record signed and submitted!");

                txtTopic.Clear();
                txtAckHours.Clear();

                LoadAcknowledgements(); // Refresh grid
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error submitting: {ex.Message}");
            }
        }
    }
}