using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;
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
            string department = txtDepartment.Text.Trim();
            string jobTitle = txtJobTitle.Text.Trim();

            string? windowsUser = chkLinkWindows.Checked
                ? (Environment.UserDomainName + "\\" + Environment.UserName).ToLower()
                : null;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username and password are required.");
                return;
            }

            // Using new DatabaseHelper
            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                // This will now be an NpgsqlTransaction
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        int? employeeId = null;

                        // Insert into Employees if we got details
                        if (!string.IsNullOrWhiteSpace(fullName))
                        {
                            // Using NpgsqlCommand
                            using (var cmd = new NpgsqlCommand())
                            {
                                cmd.Connection = conn;
                                cmd.Transaction = tran;
                                // SQL syntax for PostgreSQL (RETURNING and positional parameters)
                                cmd.CommandText = @"
                                    INSERT INTO Employees (FullName, Department, JobTitle)
                                    VALUES ($1, $2, $3)
                                    RETURNING EmployeeID;";

                                //  Positional parameters in order
                                cmd.Parameters.AddWithValue(fullName);
                                cmd.Parameters.AddWithValue(department);
                                cmd.Parameters.AddWithValue(jobTitle);
                                employeeId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }

                        // Insert into Users (always Role = Employee)
                        using (var cmd = new NpgsqlCommand())
                        {
                            cmd.Connection = conn;
                            cmd.Transaction = tran;
                            // SQL syntax for PostgreSQL (positional parameters)
                            cmd.CommandText = @"
                                INSERT INTO Users 
                                (Email, PasswordHash, Role, EmployeeID, WindowsUsername)
                                VALUES ($1, $2, $3, $4, $5)";

                            // Positional parameters in order
                            cmd.Parameters.AddWithValue(username);
                            cmd.Parameters.AddWithValue(HashPassword(password)); // hashed
                            cmd.Parameters.AddWithValue("Employee");            // default role
                            cmd.Parameters.AddWithValue((object?)employeeId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue((object?)windowsUser ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        tran.Commit();
                        MessageBox.Show("User created successfully!");
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        // Better error message for unique username violation
                        if (ex.Message.Contains("duplicate key value violates unique constraint"))
                        {
                            MessageBox.Show("Error: This username (email) already exists.");
                        }
                        else
                        {
                            MessageBox.Show("Error creating user: " + ex.Message);
                        }
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