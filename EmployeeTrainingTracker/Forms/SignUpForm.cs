using System;
using System.ComponentModel; 
using System.Text;
using System.Text.RegularExpressions; 
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;
using System.Security.Cryptography;

namespace EmployeeTrainingTracker
{
    public partial class SignUpForm : Form
    {
        private ErrorProvider errorProvider1;

        public SignUpForm()
        {
            InitializeComponent();

            errorProvider1 = new ErrorProvider();
            errorProvider1.BlinkStyle = ErrorBlinkStyle.NeverBlink;

            this.txtFullName.Validating += new CancelEventHandler(this.txtFullName_Validating);
            this.txtUsername.Validating += new CancelEventHandler(this.txtUsername_Validating);
            this.txtDepartment.Validating += new CancelEventHandler(this.txtRequired_Validating);
            this.txtJobTitle.Validating += new CancelEventHandler(this.txtRequired_Validating);
            this.txtPassword.Validating += new CancelEventHandler(this.txtPassword_Validating);
        }

        // --- VALIDATION LOGIC ------------------------------------------------

        private void txtFullName_Validating(object sender, CancelEventArgs e)
        {
            // Regex: Start(^) to End($) allowing only Letters, Spaces, Hyphens, Apostrophes
            // This will FAIL if the user types digits (like a phone number)
            string namePattern = @"^[a-zA-Z\s\-\']+$";

            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                errorProvider1.SetError(txtFullName, "Full Name is required.");
            }
            else if (!Regex.IsMatch(txtFullName.Text, namePattern))
            {
                errorProvider1.SetError(txtFullName, "Name cannot contain numbers or special characters.");
            }
            else
            {
                // Valid
                errorProvider1.SetError(txtFullName, "");
            }
        }

        // Validate Email (Username)
        private void txtUsername_Validating(object sender, CancelEventArgs e)
        {
            // Simple Email Regex
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

            if (!Regex.IsMatch(txtUsername.Text, emailPattern))
            {
                errorProvider1.SetError(txtUsername, "Please enter a valid email address.");
            }
            else
            {
                errorProvider1.SetError(txtUsername, "");
            }
        }

        // --- SUBMIT BUTTON ---------------------------------------------------

        private void btnCreate_Click(object sender, EventArgs e)
        {
            // 4. Trigger validation for all controls before proceeding
            if (!ValidateChildren(ValidationConstraints.Enabled))
            {
                MessageBox.Show("Please correct the errors indicated by the red icons before proceeding.");
                return; // STOP execution here if validation fails
            }

            // --- Existing Logic Below (Runs only if validation passes) ---

            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string department = txtDepartment.Text.Trim();
            string jobTitle = txtJobTitle.Text.Trim();

            // Existing Windows User Logic...
            string? windowsUser = null;
            try
            {
                string fullUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                if (!string.IsNullOrEmpty(fullUser))
                {
                    windowsUser = fullUser.Contains("\\") ? fullUser.Split('\\')[1] : fullUser;
                    windowsUser = windowsUser.ToLower();
                }
            }
            catch { windowsUser = null; }

            // Basic empty check (backup)
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username and password are required.");
                return;
            }

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        int? employeeId = null;

                        if (!string.IsNullOrWhiteSpace(fullName))
                        {
                            using (var cmd = new NpgsqlCommand())
                            {
                                cmd.Connection = conn;
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    INSERT INTO Employees (FullName, Department, JobTitle)
                                    VALUES ($1, $2, $3)
                                    RETURNING EmployeeID;";

                                cmd.Parameters.AddWithValue(fullName);
                                cmd.Parameters.AddWithValue(department);
                                cmd.Parameters.AddWithValue(jobTitle);
                                employeeId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                        }

                        using (var cmd = new NpgsqlCommand())
                        {
                            cmd.Connection = conn;
                            cmd.Transaction = tran;
                            cmd.CommandText = @"
                                INSERT INTO Users 
                                (Email, PasswordHash, Role, EmployeeID, WindowsUsername)
                                VALUES ($1, $2, $3, $4, $5)";

                            cmd.Parameters.AddWithValue(username);
                            cmd.Parameters.AddWithValue(HashPassword(password));
                            cmd.Parameters.AddWithValue("Employee");
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

        private void txtRequired_Validating(object sender, CancelEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                errorProvider1.SetError(tb, "This field is required.");
            }
            else
            {
                errorProvider1.SetError(tb, "");
            }
        }

        private void txtPassword_Validating(object sender, CancelEventArgs e)
        {
            string password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(password))
            {
                errorProvider1.SetError(txtPassword, "Password is required.");
            }
            else if (password.Length < 6)
            {
                // Optional: Enforce minimum length
                errorProvider1.SetError(txtPassword, "Password must be at least 6 characters.");
            }
            else
            {
                errorProvider1.SetError(txtPassword, "");
            }
        }
    }
}

