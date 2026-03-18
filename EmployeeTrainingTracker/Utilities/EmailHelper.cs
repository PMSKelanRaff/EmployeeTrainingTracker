using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration; // Ensure you have this NuGet package

namespace EmployeeTrainingTracker.Forms
{
    public static class EmailHelper
    {
        private static readonly IConfiguration _config;

        static EmailHelper()
        {
            try
            {
                _config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // More robust for WinForms
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
            }
            catch (Exception ex)
            {
                // This will pop up immediately if the config file is missing or broken
                System.Windows.Forms.MessageBox.Show($"CONFIG ERROR: {ex.Message}");
                throw;
            }
        }

        public static bool SendResetCode(string toEmail, string code)
        {
            try
            {
                // Pull values from config at runtime
                string smtpHost = _config["Smtp:Host"];
                int smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
                string smtpUser = _config["Smtp:User"];
                string smtpPass = _config["Smtp:Password"];

                if (string.IsNullOrEmpty(smtpPass))
                {
                    return false;
                }

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var mail = new MailMessage
                    {
                        From = new MailAddress(smtpUser, "PMS Training Tracker"),
                        Subject = "Your Password Reset Code",
                        Body = $"Your password reset code is: {code}\n\nThis code will expire in 15 minutes.",
                        IsBodyHtml = false
                    };

                    mail.To.Add(toEmail);
                    client.Send(mail); // Ported logic from your implementation
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Show the actual error to your face while testing
                System.Windows.Forms.MessageBox.Show($"SMTP Diagnostic: {ex.Message}");
                System.Windows.Forms.MessageBox.Show($"Mailing Error: {ex.Message}\nInner: {ex.InnerException?.Message}");
                return false;
            }
        }
    }
}