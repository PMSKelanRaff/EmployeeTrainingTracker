using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

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

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("Invalid login.");
                            return;
                        }

                        // Safely handle nullable Role
                        object roleObj = reader["Role"];
                        if (roleObj == DBNull.Value)
                        {
                            MessageBox.Show("Role is missing for this user!");
                            return;
                        }
                        string role = roleObj.ToString()!; // non-null now

                        // Safely handle nullable EmployeeID
                        object empIdObj = reader["EmployeeID"];
                        int? employeeId = empIdObj == DBNull.Value ? null : Convert.ToInt32(empIdObj);

                        // Open the appropriate dashboard
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

    }
}
