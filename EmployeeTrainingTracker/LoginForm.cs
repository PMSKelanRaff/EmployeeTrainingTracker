using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using System.Security.Principal; // for WindowsIdentity if needed

namespace EmployeeTrainingTracker
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void Login_btn_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Role, EmployeeID FROM Users WHERE Username = @u AND PasswordHash = @p";
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", password); // TODO: hash in production

                    HandleLogin(cmd);
                }
            }
        }

        private void WindowsLogin_btn_Click(object sender, EventArgs e)
        {
            string windowsUser = (Environment.UserDomainName + "\\" + Environment.UserName).ToLower();

            MessageBox.Show($"Detected Windows user: {windowsUser}");

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Role, EmployeeID FROM Users WHERE lower(WindowsUsername) = @wu";
                    cmd.Parameters.AddWithValue("@wu", windowsUser);

                    HandleLogin(cmd);
                }
            }
        }

        private void HandleLogin(SqliteCommand cmd)
        {
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    MessageBox.Show("Invalid login.");
                    return;
                }

                object roleObj = reader["Role"];
                if (roleObj == DBNull.Value)
                {
                    MessageBox.Show("Role is missing for this user!");
                    return;
                }
                string role = roleObj.ToString()!;

                object empIdObj = reader["EmployeeID"];
                int? employeeId = empIdObj == DBNull.Value ? null : Convert.ToInt32(empIdObj);

                if (role == "Admin")
                {
                    new AdminDashboard().Show();
                }
                else if (role == "Employee" && employeeId.HasValue)
                {
                    new EmployeeDashboard(employeeId.Value).Show();
                }
                else
                {
                    MessageBox.Show("This account is not linked to an employee.");
                    return;
                }

                this.Hide();
            }
        }

    }
}