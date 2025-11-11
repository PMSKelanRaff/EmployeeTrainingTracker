using Npgsql;
using System.Windows.Forms; // Needed for MessageBox

namespace EmployeeTrainingTracker
{
    public static class DatabaseHelper
    {
        // 1. Store your new AWS connection string
        private static readonly string _connectionString =
            "Host=training-tracker-database.cluster-ce36aqqcqbm1.us-east-1.rds.amazonaws.com;" +
            "Port=5432;" +
            "Database=postgres;" +
            "Username=postgres;" +
            "Password=Open1234;" + // <-- PUT YOUR REAL PASSWORD HERE
            "SslMode=Require;" +
            "Trust Server Certificate=true";

        // 2. This is the simple method your forms will call
        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        // 3. (OPTIONAL) A test method to see if it works
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