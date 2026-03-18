using Npgsql;
using System.Windows.Forms; // Needed for MessageBox

namespace EmployeeTrainingTracker
{
    public static class DatabaseHelper
    {
        // 1. Store your new AWS connection string
        private static readonly string _connectionString =
            "Host=trainingtracker-db.cb48g6awa7ky.eu-west-1.rds.amazonaws.com;" +
            "Port=5432;" +
            "Database=postgres;" +
            "Username=tracker_app_user;" +
            "Password='387£0!K;:4sBh%c7KzWa,o_Pj!';" +
            "SslMode=Require;" +
            "Trust Server Certificate=true";

        // 2. The method your forms will call
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