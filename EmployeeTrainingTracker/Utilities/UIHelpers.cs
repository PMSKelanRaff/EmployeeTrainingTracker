using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmployeeTrainingTracker.Utilities
{
    public static class UIHelpers
    {
        public static void StyleDataGridView(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;

            // Header style
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.SteelBlue;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Default cell style
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgv.DefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
            dgv.DefaultCellStyle.SelectionBackColor = Color.LightSteelBlue;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Alternating rows for readability
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.Black;

            // Layout and grid look
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.BackgroundColor = Color.White;
            dgv.GridColor = Color.LightGray;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;

            ApplyGridFonts(dgv, ResponsiveScaler.GetScale(dgv));
        }

        // Grid fonts at the given window scale; only touches grids styled by StyleDataGridView
        public static void ApplyGridFonts(DataGridView dgv, float scale)
        {
            if (dgv.EnableHeadersVisualStyles)
                return;

            dgv.ColumnHeadersDefaultCellStyle.Font = ResponsiveScaler.ScaledFont("Segoe UI", 10F, FontStyle.Bold, scale);
            dgv.DefaultCellStyle.Font = ResponsiveScaler.ScaledFont("Segoe UI", 9F, FontStyle.Regular, scale);
        }

        public static void RenameColumns(DataGridView dgv)
        {
            var renameMap = new Dictionary<string, string>
        {
            { "email", "Email" },
            { "role", "Role" },
            { "key", "Training Key" },
            { "hrs", "CPD Hrs" },
            { "provider", "Provider" },
            { "status", "Status" },
            { "notes", "Notes" },
            { "participants", "Participants" },
            { "department", "Department" },
            { "CertificateName", "Certificate Name" },
            { "certificateid", "Certificate ID" },
            { "PlannedDate", "Planned Date" },
            { "IssueDate", "Issue Date" },
            { "ExpiryDate", "Expiry Date" },
            { "EmployeeEmail", "Email" },
            { "FullName", "Full Name" },
            { "ManagerEmail", "Manager Email" },
            { "JobTitle", "Job Title" },
            { "GroupName", "Department" },
            { "ManagerName", "Manager Name" },
            { "employeename", "Name" },
            { "EmployeeID", "Emp ID" },
            { "groupname", "Department" },
            { "description", "Description" },
            { "managername", "Manager Name" },
            { "employeeid", "Emp ID" },
            { "fullname", "Full Name" },
            { "jobtitle", "Job Title" },

        };

            foreach (var kvp in renameMap)
            {
                if (dgv.Columns.Contains(kvp.Key))
                    dgv.Columns[kvp.Key].HeaderText = kvp.Value;
            }
        }

        public static void ApplyDeletionRowStyling(DataGridView dgv)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.DataBoundItem is DataRowView rowView)
                {
                    // Case-insensitive check to avoid crashes if the column isn't there
                    if (rowView.Row.Table.Columns.Contains("ismarkedfordeletion"))
                    {
                        bool isMarked = rowView["ismarkedfordeletion"] != DBNull.Value &&
                                        Convert.ToBoolean(rowView["ismarkedfordeletion"]);

                        if (isMarked)
                        {
                            // Apply style to the whole row
                            row.DefaultCellStyle.BackColor = Color.MistyRose;
                            row.DefaultCellStyle.ForeColor = Color.Gray;
                        }
                    }
                }
            }
        }
    }
}
