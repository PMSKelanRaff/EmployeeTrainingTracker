using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace EmployeeTrainingTracker
{
    // Temporary class to set up SQLite database
    public static class DatabaseHelper
    {
        private static string dbFile = "TrainingDB.db";
        private static string connectionString = $"Data Source={dbFile}";

        public static string ConnectionString => connectionString;

        public static void InitializeDatabase()
        {
            // Microsoft.Data.Sqlite automatically creates the file if it doesn't exist
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    // Create tables
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Employees (
                            EmployeeID INTEGER PRIMARY KEY AUTOINCREMENT,
                            FullName TEXT NOT NULL,
                            Department TEXT,
                            JobTitle TEXT
                        );

                        CREATE TABLE IF NOT EXISTS Users (
                            UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                            Username TEXT NOT NULL UNIQUE,
                            PasswordHash TEXT NOT NULL,
                            Role TEXT CHECK(Role IN ('Admin','Employee')),
                            EmployeeID INTEGER,
                            FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
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

                // Seed data if tables are empty
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        -- Employees
                        INSERT INTO Employees (FullName, Department, JobTitle)
                        SELECT 'Alice Smith', 'HR', 'Manager'
                        WHERE NOT EXISTS(SELECT 1 FROM Employees WHERE FullName='Alice Smith');

                        INSERT INTO Employees (FullName, Department, JobTitle)
                        SELECT 'Bob Jones', 'IT', 'Technician'
                        WHERE NOT EXISTS(SELECT 1 FROM Employees WHERE FullName='Bob Jones');

                        INSERT INTO Employees (FullName, Department, JobTitle)
                        SELECT 'Charlie Brown', 'Finance', 'Accountant'
                        WHERE NOT EXISTS(SELECT 1 FROM Employees WHERE FullName='Charlie Brown');

                        -- Users
                        INSERT INTO Users (Username, PasswordHash, Role, EmployeeID)
                        SELECT 'admin', 'admin123', 'Admin', NULL
                        WHERE NOT EXISTS(SELECT 1 FROM Users WHERE Username='admin');

                        INSERT INTO Users (Username, PasswordHash, Role, EmployeeID)
                        SELECT 'bob', 'bob123', 'Employee', 2
                        WHERE NOT EXISTS(SELECT 1 FROM Users WHERE Username='bob');

                        INSERT INTO Users (Username, PasswordHash, Role, EmployeeID)
                        SELECT 'charlie', 'charlie123', 'Employee', 3
                        WHERE NOT EXISTS(SELECT 1 FROM Users WHERE Username='charlie');

                        -- TrainingCertificates for Bob
                        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath)
                        SELECT 2, 'Fire Warden', '2024-01-01', '2026-01-01', NULL
                        WHERE NOT EXISTS(SELECT 1 FROM TrainingCertificates WHERE CertificateName='Fire Warden' AND EmployeeID=2);

                        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath)
                        SELECT 2, 'Safe Pass', '2023-06-12', '2025-06-12', NULL
                        WHERE NOT EXISTS(SELECT 1 FROM TrainingCertificates WHERE CertificateName='Safe Pass' AND EmployeeID=2);

                        -- TrainingCertificates for Charlie
                        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath)
                        SELECT 3, 'First Aid', '2022-05-20', '2025-05-20', NULL
                        WHERE NOT EXISTS(SELECT 1 FROM TrainingCertificates WHERE CertificateName='First Aid' AND EmployeeID=3);

                        INSERT INTO TrainingCertificates (EmployeeID, CertificateName, IssueDate, ExpiryDate, FilePath)
                        SELECT 3, 'Manual Handling', '2023-02-10', '2026-02-10', NULL
                        WHERE NOT EXISTS(SELECT 1 FROM TrainingCertificates WHERE CertificateName='Manual Handling' AND EmployeeID=3);
                    ";
                    cmd.ExecuteNonQuery();
                }

            }
        }
    }
}