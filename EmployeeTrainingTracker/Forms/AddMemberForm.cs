using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace EmployeeTrainingTracker.Utilities
{
    public partial class AddMemberForm : Form
    {
        private readonly int _groupId;

        public AddMemberForm(int groupId)
        {
            InitializeComponent();
            _groupId = groupId;
            LoadAvailableEmployees();
        }

        private void LoadAvailableEmployees()
        {
            DataTable dt = GroupService.GetAvailableEmployeesForGroup(_groupId);
            dgvAvailable.DataSource = dt;

            dgvAvailable.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvAvailable.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAvailable.MultiSelect = false; // allow single selection
            dgvAvailable.ReadOnly = true;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (dgvAvailable.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an employee to add.", "Add Member", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (DataGridViewRow row in dgvAvailable.SelectedRows)
            {
                int employeeId = Convert.ToInt32(row.Cells["EmployeeID"].Value);
                // CHANGED: Removed DatabaseHelper.ConnectionString parameter
                GroupService.AddMemberToGroup(_groupId, employeeId);
            }

            MessageBox.Show("Member(s) added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
