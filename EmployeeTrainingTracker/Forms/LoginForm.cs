using System;
using System.Data;
using System.Windows.Forms;
using Npgsql;
using System.Security.Principal; // for WindowsIdentity if needed
using System.Security.Cryptography;
using System.Text;

namespace EmployeeTrainingTracker
{
    public partial class LoginForm : Form
    {
        public Form MainFormToRun { get; private set; }

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

            // Using NpgsqlConnection
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // 1) Try hashed password first
                // Using NpgsqlCommand and positional parameters ($1, $2)
                using (var cmd = new NpgsqlCommand("SELECT Role, EmployeeID FROM Users WHERE Email = $1 AND PasswordHash = $2", conn))
                {
                    cmd.Parameters.AddWithValue(username);
                    cmd.Parameters.AddWithValue(hashed);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Success! Get data and pass to our new HandleLogin method
                            string role = reader["Role"].ToString();
                            object empIdObj = reader["EmployeeID"];
                            int? employeeId = empIdObj == DBNull.Value ? null : Convert.ToInt32(empIdObj);

                            HandleLogin(role, employeeId);
                            return;
                        }
                    }
                }

                // 2) Fallback - try raw/plaintext (for legacy accounts)
                using (var cmdPlain = new NpgsqlCommand("SELECT UserID, Role, EmployeeID FROM Users WHERE Email = $1 AND PasswordHash = $2", conn))
                {
                    cmdPlain.Parameters.AddWithValue(username);
                    cmdPlain.Parameters.AddWithValue(password); // Using raw password

                    using (var readerPlain = cmdPlain.ExecuteReader())
                    {
                        if (readerPlain.Read())
                        {

                            long userId = Convert.ToInt64(readerPlain["UserID"]);
                            string role = readerPlain["Role"].ToString();
                            object empIdObj = readerPlain["EmployeeID"];
                            int? employeeId = empIdObj == DBNull.Value ? null : Convert.ToInt32(empIdObj);


                            readerPlain.Close();

                            using (var update = new NpgsqlCommand("UPDATE Users SET PasswordHash = $1 WHERE UserID = $2", conn))
                            {
                                update.Parameters.AddWithValue(hashed);
                                update.Parameters.AddWithValue(userId);
                                update.ExecuteNonQuery();
                            }

                            // Now login the user
                            HandleLogin(role, employeeId);
                            return;
                        }
                    }
                }

                MessageBox.Show("Invalid login.");
            }
        }

        private void WindowsLogin_btn_Click(object sender, EventArgs e)
        {
            string windowsUser = (Environment.UserDomainName + "\\" + Environment.UserName).ToLower();

            
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT Role, EmployeeID FROM Users WHERE lower(WindowsUsername) = $1", conn))
                {
                    cmd.Parameters.AddWithValue(windowsUser);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string role = reader["Role"].ToString();
                            object empIdObj = reader["EmployeeID"];
                            int? employeeId = empIdObj == DBNull.Value ? null : Convert.ToInt32(empIdObj);

                            HandleLogin(role, employeeId);
                        }
                        else
                        {
                            MessageBox.Show("Invalid login.");
                        }
                    }
                }
            }
        }


        private void HandleLogin(string role, int? employeeId)
        {
            if (string.IsNullOrEmpty(role))
            {
                MessageBox.Show("Role is missing for this user!");
                return;
            }

            if (role == "Admin")
            {
                // 1. Assign the form to the property
                this.MainFormToRun = new AdminDashboard();
            }
            else if (role == "Employee" && employeeId.HasValue)
            {
                // 1. Assign the form to the property
                this.MainFormToRun = new EmployeeDashboard(employeeId.Value);
            }
            else
            {
                MessageBox.Show("This account is not linked to an employee.");
                return;
            }

            // 2. Set the DialogResult to OK. This tells Program.cs it was successful.
            this.DialogResult = DialogResult.OK;

            // 3. Close the login form. This will resume the code in Program.cs.
            this.Close();
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