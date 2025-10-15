using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace EmployeeTrainingTracker
{
    public partial class SignUpForm : Form
    {
        public SignUpForm()
        {
            InitializeComponent();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string department = txtDepartment.Text.Trim();   // <-- use textbox instead of label
            string jobTitle = txtJobTitle.Text.Trim();

            string? windowsUser = chkLinkWindows.Checked
                ? (Environment.UserDomainName + "\\" + Environment.UserName).ToLower()
                : null;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username and password are required.");
                return;
            }

            using (var conn = new SqliteConnection(DatabaseHelper.ConnectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        int? employeeId = null;

                        // Insert into Employees if we got details
                        if (!string.IsNullOrWhiteSpace(fullName))
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    INSERT INTO Employees (FullName, Department, JobTitle)
                                    VALUES (@fn, @dep, @jt);
                                    SELECT last_insert_rowid();";
                                cmd.Parameters.AddWithValue("@fn", fullName);
                                cmd.Parameters.AddWithValue("@dep", department);
                                cmd.Parameters.AddWithValue("@jt", jobTitle);
                                employeeId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }

                        // Insert into Users (always Role = Employee)
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tran;
                            cmd.CommandText = @"
                                INSERT INTO Users 
                                (Email, PasswordHash, Role, EmployeeID, WindowsUsername)
                                VALUES (@u, @p, @r, @emp, @wu)";
                            cmd.Parameters.AddWithValue("@u", username);
                            cmd.Parameters.AddWithValue("@p", HashPassword(password)); // hashed
                            cmd.Parameters.AddWithValue("@r", "Employee");             // default role
                            cmd.Parameters.AddWithValue("@emp", (object?)employeeId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@wu", (object?)windowsUser ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        tran.Commit();
                        MessageBox.Show("User created successfully!");
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        MessageBox.Show("Error creating user: " + ex.Message);
                    }
                }
            }
        }

        private string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }


    }
}

