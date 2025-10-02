using Microsoft.Data.Sqlite;

public static class DatabaseHelper
{
    private static string dbFile = @"\\2016fs03\d$\Kelan\Apps\TrainingTracker\Database\TrainingDB.db";
    private static string connectionString = $"Data Source={dbFile}";

    public static string ConnectionString => connectionString;

    public static void InitializeDatabase()
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();

            // Check what type of database we have
            DatabaseType dbType = DetermineDatabaseType(conn);

            if (dbType == DatabaseType.SQLServerStyle)
            {
                MigrateToSQLiteSchema(conn);
            }
            else if (dbType == DatabaseType.SQLiteStyle)
            {
                // Already using SQLite schema, ensure tables exist
                EnsureSQLiteTablesExist(conn);
            }
            else
            {
                // New database, create SQLite schema
                CreateSQLiteSchema(conn);
                SeedData(conn);
            }
        }
    }

    private static DatabaseType DetermineDatabaseType(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            // Check if we have SQL Server style tables
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master 
                WHERE type='table' AND (name='Users' OR name='Employees' OR name='TrainingCertificates')";

            int tableCount = Convert.ToInt32(cmd.ExecuteScalar());

            if (tableCount == 0)
                return DatabaseType.New;

            // Check if any table uses SQL Server syntax
            cmd.CommandText = @"
                SELECT sql FROM sqlite_master 
                WHERE type='table' AND name IN ('Users', 'Employees', 'TrainingCertificates')
                LIMIT 1";

            var result = cmd.ExecuteScalar() as string;
            if (result != null && result.Contains("IDENTITY"))
                return DatabaseType.SQLServerStyle;

            return DatabaseType.SQLiteStyle;
        }
    }

    private static void MigrateToSQLiteSchema(SqliteConnection conn)
    {
        // Backup data first (important!)
        BackupData(conn);

        // Drop old tables and create new ones with SQLite syntax
        using (var cmd = conn.CreateCommand())
        {
            // Drop foreign key constraints first
            cmd.CommandText = "PRAGMA foreign_keys = OFF";
            cmd.ExecuteNonQuery();

            // Drop old tables
            cmd.CommandText = @"
                DROP TABLE IF EXISTS TrainingCertificates;
                DROP TABLE IF EXISTS Users;
                DROP TABLE IF EXISTS Employees;
            ";
            cmd.ExecuteNonQuery();

            // Create new tables with SQLite syntax
            cmd.CommandText = @"
                CREATE TABLE Employees (
                    EmployeeID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Department TEXT,
                    JobTitle TEXT
                );

                CREATE TABLE TrainingCertificates (
                    CertificateID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeID INTEGER,
                    CertificateName TEXT NOT NULL,
                    IssueDate TEXT,
                    ExpiryDate TEXT,
                    FilePath TEXT,
                    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
                );
            ";
            cmd.ExecuteNonQuery();

            // Restore foreign key constraints
            cmd.CommandText = "PRAGMA foreign_keys = ON";
            cmd.ExecuteNonQuery();
        }

        // Restore data from backup
        RestoreData(conn);

        MessageBox.Show("Database migrated from SQL Server to SQLite format successfully!");
    }

    private static void BackupData(SqliteConnection conn)
    {
        // This is a simple backup - in production, you'd want a more robust solution
        using (var cmd = conn.CreateCommand())
        {
            // Create backup tables
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Backup_Employees AS SELECT * FROM Employees;
                CREATE TABLE IF NOT EXISTS Backup_TrainingCertificates AS SELECT * FROM TrainingCertificates;
            ";
            cmd.ExecuteNonQuery();
        }
    }

    private static void RestoreData(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            // Restore data from backup tables
            cmd.CommandText = @"
                INSERT INTO Employees (FullName, Department, JobTitle)
                SELECT FullName, Department, JobTitle FROM Backup_Employees;

                INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath)
                SELECT EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath FROM Backup_TrainingCertificates;

                DROP TABLE IF EXISTS Backup_Employees;
                DROP TABLE IF EXISTS Backup_TrainingCertificates;
            ";
            cmd.ExecuteNonQuery();
        }
    }

    private static void EnsureSQLiteTablesExist(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Employees (
                    EmployeeID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Department TEXT,
                    JobTitle TEXT
                );

                CREATE TABLE IF NOT EXISTS TrainingCertificates (
                    CertificateID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeID INTEGER,
                    CertificateName TEXT NOT NULL,
                    IssueDate TEXT,
                    ExpiryDate TEXT,
                    FilePath TEXT,
                    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    private static void CreateSQLiteSchema(SqliteConnection conn)
    {
        EnsureSQLiteTablesExist(conn);
    }

    private static void SeedData(SqliteConnection conn)
    {
        // Only seed if tables are empty
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Employees";
            int count = Convert.ToInt32(cmd.ExecuteScalar());

            if (count == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO Employees (FullName, Department, JobTitle) VALUES
                    ('Alice Smith', 'HR', 'Manager'),
                    ('Bob Jones', 'IT', 'Technician'),
                    ('Charlie Brown', 'Finance', 'Accountant');

                    INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath) VALUES
                    (2, 'Fire Warden', '2024-01-01', '2026-01-01', NULL),
                    (2, 'Safe Pass', '2023-06-12', '2025-06-12', NULL),
                    (3, 'First Aid', '2022-05-20', '2025-05-20', NULL),
                    (3, 'Manual Handling', '2023-02-10', '2026-02-10', NULL);
                ";
                cmd.ExecuteNonQuery();
            }
        }
    }

    private enum DatabaseType
    {
        New,
        SQLServerStyle,
        SQLiteStyle
    }
}
