using System;
using System.Windows.Forms;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace EmployeeTrainingTracker.Forms
{
    public partial class ForgotPasswordForm : Form
    {
        // Stores the email after the first step so we know who to reset
        private string _targetEmail = string.Empty;

        public ForgotPasswordForm()
        {
            InitializeComponent();

            // Set initial UI state: Hide reset fields until code is sent
            resetCodeTxt.Visible = false;
            resetCodeLbl.Visible = false;
            newPasswordTxt.Visible = false;
            newPasswordLbl.Visible = false;
        }

        private void sendResetCodeBtn_Click(object sender, EventArgs e)
        {
            // We use the button text to determine if we are in "Request" mode or "Reset" mode
            if (sendResetCodeBtn.Text == "Send Reset Code")
            {
                HandleRequestCode();
            }
            else
            {
                HandleReset();
            }
        }

        private void HandleRequestCode()
        {
            _targetEmail = emailTxt.Text.Trim();

            if (string.IsNullOrEmpty(_targetEmail))
            {
                MessageBox.Show("Please enter your email address.");
                return;
            }

            string rawToken = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string hashedToken = HashString(rawToken);

            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand(
                        "INSERT INTO PasswordResetTokens (Email, TokenHash, ExpiryTime) " +
                        "VALUES ($1, $2, $3)", conn);
                    cmd.Parameters.AddWithValue(_targetEmail);
                    cmd.Parameters.AddWithValue(hashedToken);
                    cmd.Parameters.AddWithValue(DateTime.Now.AddMinutes(15));
                    cmd.ExecuteNonQuery();
                }

                // 3. Call your mailing service
                bool emailSent = EmailHelper.SendResetCode(_targetEmail, rawToken);

                if (emailSent)
                {
                    MessageBox.Show($"Verification code sent to {_targetEmail}.");

                    // 4. Transition UI ONLY inside the successful block
                    emailTxt.Enabled = false;
                    ToggleResetFields(true); // Helper method for visibility
                    sendResetCodeBtn.Text = "Verify & Reset";
                }
                else
                {
                    MessageBox.Show("Failed to send the email. Please check your internet or SMTP settings.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message);
            }
        }

        private void HandleReset()
        {
            string inputToken = resetCodeTxt.Text.Trim();
            string newPass = newPasswordTxt.Text.Trim();

            // 1. Basic empty check to ensure all fields are filled
            if (string.IsNullOrEmpty(inputToken) || string.IsNullOrEmpty(newPass))
            {
                MessageBox.Show("Please fill in the code and your new password.");
                return;
            }

            // 2. Minimum length validation (6 characters)
            if (newPass.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters long.");
                return;
            }

            // Hash the inputs once they pass the basic length checks
            string hashedInput = HashString(inputToken);
            string hashedNewPass = HashString(newPass);

            using (var conn = DatabaseHelper.GetConnection())
            {
                conn.Open();
                // Validate the token matches, isn't used, and isn't expired
                var cmd = new NpgsqlCommand(
                    "SELECT TokenID FROM PasswordResetTokens WHERE Email = $1 AND TokenHash = $2 AND IsUsed = FALSE AND ExpiryTime > $3", conn);
                cmd.Parameters.AddWithValue(_targetEmail);
                cmd.Parameters.AddWithValue(hashedInput);
                cmd.Parameters.AddWithValue(DateTime.Now);

                object result = cmd.ExecuteScalar();

                if (result != null)
                {
                    int tokenId = Convert.ToInt32(result);

                    // 1. Update the password in your Users table
                    using (var upd = new NpgsqlCommand("UPDATE Users SET PasswordHash = $1 WHERE Email = $2", conn))
                    {
                        upd.Parameters.AddWithValue(hashedNewPass);
                        upd.Parameters.AddWithValue(_targetEmail);
                        upd.ExecuteNonQuery();
                    }

                    // 2. Mark the token as used so it can't be reused
                    using (var burn = new NpgsqlCommand("UPDATE PasswordResetTokens SET IsUsed = TRUE WHERE TokenID = $1", conn))
                    {
                        burn.Parameters.AddWithValue(tokenId);
                        burn.ExecuteNonQuery();
                    }

                    MessageBox.Show("Password successfully updated! You can now log in.");
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid or expired reset code.");
                }
            }
        }

        // Helper for UI visibility
        private void ToggleResetFields(bool show)
        {
            resetCodeTxt.Visible = resetCodeLbl.Visible = show;
            newPasswordTxt.Visible = newPasswordLbl.Visible = show;
        }

        // SHA256 hashing to match your login form
        private string HashString(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}