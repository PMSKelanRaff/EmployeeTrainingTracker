using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Windows.Forms; // Needed for MessageBox

namespace EmployeeTrainingTracker
{
    public static class DatabaseHelper
    {
        private static readonly string _connectionString;

        static DatabaseHelper()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = config["Database:Host"],
                Port = int.Parse(config["Database:Port"] ?? "5432"),
                Database = config["Database:Name"],
                Username = config["Database:User"],
                Password = config["Database:Pass"], // The builder safely handles the !' here
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            };

            _connectionString = builder.ToString();
        }

        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        // 3.  A test method to see if it works
        public static bool TestConnection()
        {
            using (var conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    return true; // Success!
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database connection failed: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false; // Failed
                }
            }
        }
    }
}