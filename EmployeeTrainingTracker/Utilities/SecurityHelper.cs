using System;
using System.Security.Cryptography;
using System.Text;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Centralized password/token hashing. This used to be copy-pasted (with the exact
    /// same SHA256 implementation) inside LoginForm, SignUpForm, ForgotPasswordForm,
    /// AdminDashboard and ManagerDashboard. Keeping a single copy means a future change
    /// to the hashing algorithm only has to happen in one place.
    /// </summary>
    public static class SecurityHelper
    {
        public static string HashPassword(string? password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(password ?? "");
            byte[] hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
