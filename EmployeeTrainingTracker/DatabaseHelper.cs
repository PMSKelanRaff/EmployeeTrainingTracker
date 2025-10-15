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
            // Employees table
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Employees (
                    EmployeeID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Department TEXT,
                    JobTitle TEXT
                );";
            cmd.ExecuteNonQuery();

            // Users table with Email instead of Username
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT CHECK(Role IN ('Admin','Employee')),
                    EmployeeID INTEGER,
                    WindowsUsername NVARCHAR(100),
                    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
                );";
            cmd.ExecuteNonQuery();

            // TrainingCertificates table
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS TrainingCertificates (
                    CertificateID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeID INTEGER,
                    CertificateName TEXT NOT NULL,
                    IssueDate TEXT,
                    ExpiryDate TEXT,
                    FilePath TEXT,
                    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
                );";
            cmd.ExecuteNonQuery();
        }
    }

    private static void CreateSQLiteSchema(SqliteConnection conn)
    {
        EnsureSQLiteTablesExist(conn);
    }

    private static void SeedData(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            // Only seed Employees if empty
            cmd.CommandText = "SELECT COUNT(*) FROM Employees";
            int empCount = Convert.ToInt32(cmd.ExecuteScalar());

            if (empCount == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO Employees (FullName, Department, JobTitle) VALUES
                        ('Alice Smith', 'HR', 'Manager'),
                        ('Bob Jones', 'IT', 'Technician'),
                        ('Kelan Rafferty', 'Operations', 'Employee'),
                        ('Charlie Brown', 'Finance', 'Accountant');";
                cmd.ExecuteNonQuery();
            }

            // Only seed Users if empty
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            int userCount = Convert.ToInt32(cmd.ExecuteScalar());

            if (userCount == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO Users (Email, PasswordHash, Role, EmployeeID, WindowsUsername) VALUES
                        ('alice@pms.ie','', 'Admin', 1, 'AliceS'),
                        ('bob@pms.ie','', 'Employee', 2, 'BobJ'),
                        ('kelan@pms.ie','', 'Employee', 3, 'KelanR'),
                        ('charlie@pms.ie','', 'Employee', 4, 'CharlieB');";
                cmd.ExecuteNonQuery();
            }

            // Only seed TrainingCertificates if empty
            cmd.CommandText = "SELECT COUNT(*) FROM TrainingCertificates";
            int certCount = Convert.ToInt32(cmd.ExecuteScalar());

            if (certCount == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath) VALUES
                        (3, 'Fire Warden', '2024-01-01', '2026-01-01', NULL),
                        (3, 'Safe Pass', '2023-06-12', '2025-06-12', NULL),
                        (4, 'First Aid', '2022-05-20', '2025-05-20', NULL),
                        (4, 'Manual Handling', '2023-02-10', '2026-02-10', NULL);";
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
