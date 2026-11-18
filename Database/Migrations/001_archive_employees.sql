-- 001_archive_employees.sql
-- Employees are archived instead of deleted, so their record and certificates stay in
-- the database for archiving and can be restored if they return.
-- Safe to run more than once.

ALTER TABLE Employees ADD COLUMN IF NOT EXISTS IsArchived BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE Employees ADD COLUMN IF NOT EXISTS ArchivedAt TIMESTAMP NULL;

-- Most lists filter on active employees
CREATE INDEX IF NOT EXISTS IX_Employees_IsArchived ON Employees (IsArchived);
