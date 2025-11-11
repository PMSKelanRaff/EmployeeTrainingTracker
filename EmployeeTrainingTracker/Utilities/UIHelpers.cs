using System;
using System.Collections.Generic;
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
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Default cell style
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
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
        }

       
        public static void RenameColumns(DataGridView dgv)
        {
            var renameMap = new Dictionary<string, string>
        {
            { "CertificateName", "Certificate Name" },
             { "CertificateID", "Certificate ID" },
            { "PlannedDate", "Planned Date" },
            { "IssueDate", "Issue Date" },
            { "ExpiryDate", "Expiry Date" },
            { "EmployeeEmail", "Email" },
            { "FullName", "Full Name" },
            { "ManagerEmail", "Manager Email" },
            { "JobTitle", "Job Title" },
            { "GroupName", "Group Name" },
            { "ManagerName", "Manager Name" },
            { "EmployeeID", "Emp ID" }
        };

            foreach (var kvp in renameMap)
            {
                if (dgv.Columns.Contains(kvp.Key))
                    dgv.Columns[kvp.Key].HeaderText = kvp.Value;
            }
        }
    }
}
