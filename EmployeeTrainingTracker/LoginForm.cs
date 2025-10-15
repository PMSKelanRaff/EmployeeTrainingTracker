using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using System.Security.Principal; // for WindowsIdentity if needed
using System.Security.Cryptography;
using System.Text;

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

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Enter username and password.");
                return;
            }

            string hashed = HashPassword(password);

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();

                // 1) Try hashed password first
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Role, EmployeeID FROM Users WHERE Email = @u AND PasswordHash = @p";
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", hashed);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // matched with hashed password -> reuse existing handler
                            // To reuse existing HandleLogin which expects a SqliteCommand,
                            // we can create a new command and call HandleLogin, but simpler:
                            // create a small command returning Role & EmployeeID again and pass it
                            reader.Close();
                            using (var cmd2 = conn.CreateCommand())
                            {
                                cmd2.CommandText = "SELECT Role, EmployeeID FROM Users WHERE Email = @u";
                                cmd2.Parameters.AddWithValue("@u", username);
                                HandleLogin(cmd2);
                                return;
                            }
                        }
                    }
                }

                // 2) Fallback - try raw/plaintext (for legacy accounts)
                using (var cmdPlain = conn.CreateCommand())
                {
                    cmdPlain.CommandText = "SELECT UserID FROM Users WHERE Email = @u AND PasswordHash = @pPlain";
                    cmdPlain.Parameters.AddWithValue("@u", username);
                    cmdPlain.Parameters.AddWithValue("@pPlain", password);

                    object objUserId = cmdPlain.ExecuteScalar();
                    if (objUserId != null)
                    {
                        // Legacy plaintext match -> upgrade to hashed password
                        long userId = Convert.ToInt64(objUserId);

                        using (var update = conn.CreateCommand())
                        {
                            update.CommandText = "UPDATE Users SET PasswordHash = @newHash WHERE UserID = @id";
                            update.Parameters.AddWithValue("@newHash", hashed);
                            update.Parameters.AddWithValue("@id", userId);
                            update.ExecuteNonQuery();
                        }

                        // Now login the user using the normal path
                        using (var cmd3 = conn.CreateCommand())
                        {
                            cmd3.CommandText = "SELECT Role, EmployeeID FROM Users WHERE UserID = @id";
                            cmd3.Parameters.AddWithValue("@id", userId);
                            HandleLogin(cmd3);
                            return;
                        }
                    }
                }

                // 3) Nothing matched
                MessageBox.Show("Invalid login.");
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

        private void SignUp_btn_Click(object sender, EventArgs e)
        {
            var signupForm = new SignUpForm();
            signupForm.ShowDialog(); // modal
        }

        private static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password ?? "");
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}