using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EmployeeTrainingTracker.Helpers;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Shared logic for the three dashboards (Admin/Manager/Employee). Each dashboard
    /// used to hand-roll its own copy of the certificate grid column definitions, the
    /// "click a link to open the cloud file" behavior, the pending/deletion row
    /// highlighting, employee-input clearing and CSV export. All of that lived in three
    /// (sometimes byte-for-byte identical) copies. This class holds one copy of each,
    /// parameterized by the DataGridView/controls involved so every dashboard keeps its
    /// own layout and control names but shares the behavior.
    /// </summary>
    public static class DashboardHelper
    {
        // ------------------------------------------------------------------
        // Grid column builders
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the standard certificate columns (ID, Name, Key, HRS, Provider,
        /// IssueDate, ExpiryDate, FileLink, LastNotifiedDate) used by every dashboard's
        /// certificates grid. Pass includeDeletionFlagColumn: true (Employee dashboard)
        /// to also add the hidden IsMarkedForDeletion column used to drive UI state.
        /// </summary>
        public static void BuildCertificateColumns(DataGridView dgv, bool includeDeletionFlagColumn = false)
        {
            dgv.Columns.Clear();
            dgv.AutoGenerateColumns = false;

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateID",
                DataPropertyName = "CertificateID",
                HeaderText = "ID",
                Visible = false
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CertificateName",
                DataPropertyName = "CertificateName",
                HeaderText = "Certificate Name"
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Key",
                DataPropertyName = "Key",
                HeaderText = "Training Key"
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HRS",
                DataPropertyName = "HRS",
                HeaderText = "CPD Hrs"
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Provider",
                DataPropertyName = "Provider",
                HeaderText = "Provider"
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IssueDate",
                DataPropertyName = "IssueDate",
                HeaderText = "Issue Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ExpiryDate",
                DataPropertyName = "ExpiryDate",
                HeaderText = "Expiry Date",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
            });

            dgv.Columns.Add(new DataGridViewLinkColumn
            {
                Name = "FileLink",
                DataPropertyName = "S3Key",
                HeaderText = "Certificate File",
                TrackVisitedState = true,
                Width = 200
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LastNotifiedDate",
                DataPropertyName = "LastNotifiedDate",
                HeaderText = "Last Notified"
            });

            if (includeDeletionFlagColumn)
            {
                dgv.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = "IsMarkedForDeletion",
                    DataPropertyName = "IsMarkedForDeletion",
                    HeaderText = "Pending Deletion",
                    Visible = false
                });
            }
        }

        /// <summary>
        /// Binds a certificate DataTable to dgv using the standard columns above and
        /// applies the usual styling/visibility rules. Optionally sorts deletion-pending
        /// rows to the bottom (Employee dashboard).
        /// </summary>
        public static void BindCertificates(DataGridView dgv, DataTable table, bool includeDeletionFlagColumn = false, bool sortPendingDeletionLast = false)
        {
            BuildCertificateColumns(dgv, includeDeletionFlagColumn);

            if (sortPendingDeletionLast && table.Columns.Contains("ismarkedfordeletion"))
            {
                table.DefaultView.Sort = "ismarkedfordeletion ASC, CertificateName ASC";
            }

            dgv.DataSource = table;

            if (dgv.Columns.Contains("CertificateID"))
                dgv.Columns["CertificateID"].Visible = false;

            UIHelpers.StyleDataGridView(dgv);
        }

        /// <summary>
        /// Builds the "pending deletion tasks" grid columns shared by Admin and Manager.
        /// </summary>
        public static void BuildDeletionTaskColumns(DataGridView dgv)
        {
            dgv.Columns.Clear();
            dgv.AutoGenerateColumns = false;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateID", DataPropertyName = "CertificateID", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "Employee Name" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateName", DataPropertyName = "CertificateName", HeaderText = "Certificate Name" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", DataPropertyName = "Key", HeaderText = "Training Key" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HRS", DataPropertyName = "HRS", HeaderText = "CPD Hrs" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Provider", DataPropertyName = "Provider", HeaderText = "Provider" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssueDate", DataPropertyName = "IssueDate", HeaderText = "Issue Date", DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpiryDate", DataPropertyName = "ExpiryDate", HeaderText = "Expiry Date", DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
            dgv.Columns.Add(new DataGridViewLinkColumn { Name = "FileLink", DataPropertyName = "S3Key", HeaderText = "Certificate File", TrackVisitedState = true, Width = 200 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastNotifiedDate", DataPropertyName = "LastNotifiedDate", HeaderText = "Last Notified" });
        }

        public static void BindDeletionTasks(DataGridView dgv, DataTable table)
        {
            BuildDeletionTaskColumns(dgv);
            dgv.DataSource = table;

            if (dgv.Columns.Contains("CertificateID"))
                dgv.Columns["CertificateID"].Visible = false;

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            UIHelpers.StyleDataGridView(dgv);
        }

        /// <summary>
        /// Builds the "pending training approvals" grid columns shared by Admin and Manager.
        /// </summary>
        public static void BuildPendingApprovalColumns(DataGridView dgv)
        {
            dgv.Columns.Clear();
            dgv.AutoGenerateColumns = false;
            dgv.AllowUserToAddRows = false;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "CertificateID", DataPropertyName = "CertificateID", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "employeename", DataPropertyName = "employeename", HeaderText = "Employee Name", ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "trainingtopic", DataPropertyName = "trainingtopic", HeaderText = "Certificate / Topic", ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "hours", DataPropertyName = "hours", HeaderText = "Hours", ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "trainingdate", DataPropertyName = "trainingdate", HeaderText = "Date", ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
            dgv.Columns.Add(new DataGridViewLinkColumn { Name = "FileLink", DataPropertyName = "S3Key", HeaderText = "View Document", TrackVisitedState = true });

            var trainerCol = new DataGridViewTextBoxColumn
            {
                Name = "trainername",
                DataPropertyName = "trainername",
                HeaderText = "Trainer Name",
                ReadOnly = false
            };
            trainerCol.DefaultCellStyle.BackColor = Color.LightYellow;
            dgv.Columns.Add(trainerCol);
        }

        public static void BindPendingApprovals(DataGridView dgv, DataTable table)
        {
            BuildPendingApprovalColumns(dgv);
            dgv.DataSource = table;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            UIHelpers.StyleDataGridView(dgv);
        }

        // ------------------------------------------------------------------
        // Shared "FileLink" column behavior (cell formatting + click-to-open)
        // ------------------------------------------------------------------

        /// <summary>
        /// Wire this directly to any grid's CellFormatting event
        /// (dgv.CellFormatting += DashboardHelper.FileLinkCellFormatting;)
        /// to show just the file name instead of the full S3 key in the FileLink column.
        /// </summary>
        public static void FileLinkCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView dgv) return;
            if (e.ColumnIndex < 0 || e.ColumnIndex >= dgv.Columns.Count) return;

            if (dgv.Columns[e.ColumnIndex].Name == "FileLink" && e.Value != null)
            {
                string fullS3Key = e.Value.ToString() ?? "";
                e.Value = System.IO.Path.GetFileName(fullS3Key);
                e.FormattingApplied = true;
            }
        }

        /// <summary>
        /// Wire this directly to any grid's CellContentClick event to open the secure
        /// (pre-signed) S3 URL for the row's certificate file.
        /// </summary>
        public static void FileLinkCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (sender is not DataGridView dgv) return;
            if (dgv.Columns[e.ColumnIndex].Name != "FileLink") return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not DataRowView rowView) return;

            string? s3Key = rowView["S3Key"]?.ToString();

            if (string.IsNullOrEmpty(s3Key))
            {
                MessageBox.Show("No cloud file linked for this certificate.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Could not open the file from the cloud:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------
        // Row highlighting
        // ------------------------------------------------------------------

        /// <summary>
        /// Colors rows whose "status" column is "pending" light yellow (and completed/other
        /// rows white). Call this together with UIHelpers.ApplyDeletionRowStyling from a
        /// grid's DataBindingComplete handler - deletion styling wins if both apply.
        /// </summary>
        public static void ApplyPendingHighlighting(DataGridView dgv)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow || row.DataBoundItem is not DataRowView rowView) continue;

                bool isMarked = false;
                if (rowView.Row.Table.Columns.Contains("ismarkedfordeletion"))
                {
                    isMarked = rowView["ismarkedfordeletion"] != DBNull.Value &&
                               Convert.ToBoolean(rowView["ismarkedfordeletion"]);
                }

                if (!isMarked && rowView.Row.Table.Columns.Contains("status"))
                {
                    string status = rowView["status"]?.ToString().Trim().ToLower() ?? "";
                    row.DefaultCellStyle.BackColor = status == "pending" ? Color.LightYellow : Color.White;
                }
            }
        }

        /// <summary>
        /// Convenience wrapper: applies both the deletion-row styling and the pending
        /// highlight in one call. Wire directly to DataBindingComplete:
        /// dgv.DataBindingComplete += (s, e) =&gt; DashboardHelper.ApplyStandardRowStyling((DataGridView)s!);
        /// </summary>
        public static void ApplyStandardRowStyling(DataGridView dgv)
        {
            UIHelpers.ApplyDeletionRowStyling(dgv);
            ApplyPendingHighlighting(dgv);
        }

        // ------------------------------------------------------------------
        // Employee grid/textbox helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads the EmployeeID of the currently selected row in an employees grid, or
        /// null if nothing valid is selected.
        /// </summary>
        public static int? GetSelectedEmployeeId(DataGridView dgvEmployees)
        {
            if (dgvEmployees.CurrentRow == null || dgvEmployees.CurrentRow.IsNewRow)
                return null;

            var empIdObj = dgvEmployees.CurrentRow.Cells["EmployeeID"]?.Value;

            if (empIdObj == null || empIdObj == DBNull.Value)
                return null;

            return Convert.ToInt32(empIdObj);
        }

        /// <summary>
        /// Clears the "add/edit employee" input controls shared by Admin and Manager dashboards.
        /// </summary>
        public static void ClearEmployeeInputs(TextBox txtUsername, TextBox txtPassword, ComboBox cmbRole, ComboBox cmbDept, TextBox txtJobTitle)
        {
            txtUsername.Text = "";
            txtPassword.Text = "";
            cmbRole.SelectedIndex = -1;
            cmbDept.Text = "";
            txtJobTitle.Text = "";
        }

        // ------------------------------------------------------------------
        // CSV export (Report tab)
        // ------------------------------------------------------------------

        /// <summary>
        /// Prompts the user for a save location and exports the visible columns/rows of
        /// a DataGridView to CSV. Shared by Admin and Manager's "Export CSV" report button.
        /// </summary>
        public static void ExportDataGridViewToCsv(DataGridView dgv, string fileNamePrefix = "Report")
        {
            if (dgv.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.");
                return;
            }

            using var sfd = new SaveFileDialog();
            sfd.Filter = "CSV files (*.csv)|*.csv";
            sfd.FileName = $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd}.csv";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();

                var columnNames = dgv.Columns
                    .Cast<DataGridViewColumn>()
                    .Where(c => c.Visible)
                    .Select(c => c.HeaderText);
                sb.AppendLine(string.Join(",", columnNames));

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;

                    var cells = row.Cells.Cast<DataGridViewCell>()
                        .Where(c => c.OwningColumn.Visible)
                        .Select(c => EscapeCsvValue(c.Value?.ToString() ?? ""));
                    sb.AppendLine(string.Join(",", cells));
                }

                System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
                MessageBox.Show("CSV exported successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting CSV: {ex.Message}");
            }
        }

        private static string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                value = value.Replace("\"", "\"\"");
                value = $"\"{value}\"";
            }
            return value;
        }
    }
}
