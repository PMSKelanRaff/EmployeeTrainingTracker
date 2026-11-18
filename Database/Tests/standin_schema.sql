-- Stand-in for the production tables the Skills Matrix migrations reference (DISPOSABLE TEST DATABASE ONLY).
-- Only the columns the migrations and the Skills Matrix code use. Dummy people, not real employees.
CREATE TABLE public.Employees (
    EmployeeID  BIGINT  PRIMARY KEY,
    FullName    TEXT    NOT NULL,
    IsArchived  BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE TABLE public.Users (
    UserID      BIGINT PRIMARY KEY,
    EmployeeID  BIGINT REFERENCES public.Employees (EmployeeID),
    Role        TEXT   NOT NULL
);
CREATE TABLE public.Groups (
    GroupID    BIGINT PRIMARY KEY,
    GroupName  TEXT   NOT NULL,
    ManagerID  BIGINT NULL
);
CREATE TABLE public.GroupMembers (
    GroupID     BIGINT REFERENCES public.Groups (GroupID),
    EmployeeID  BIGINT REFERENCES public.Employees (EmployeeID),
    PRIMARY KEY (GroupID, EmployeeID)
);

-- Same columns as production's TrainingCertificates before migration 008 (read 2026-11-18)
CREATE TABLE public.TrainingCertificates (
    CertificateID       BIGINT PRIMARY KEY,
    EmployeeID          BIGINT REFERENCES public.Employees (EmployeeID),
    CertificateName     TEXT,
    Key                 TEXT,
    HRS                 REAL,
    Provider            TEXT,
    IssueDate           DATE,
    ExpiryDate          DATE,
    FilePath            TEXT,
    LastNotifiedDate    DATE,
    IsMarkedForDeletion BOOLEAN DEFAULT FALSE,
    S3Key               VARCHAR,
    Status              VARCHAR DEFAULT 'Pending',
    TrainerName         VARCHAR,
    TraineeSignedAt     TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    TrainerSignedAt     TIMESTAMP
);

INSERT INTO public.Employees (EmployeeID, FullName, IsArchived) VALUES
    (1, 'Alice Admin', FALSE), (2, 'Bob Tester', FALSE), (3, 'Carol Manager', FALSE), (4, 'Dave Spare', FALSE), (5, 'Erin Archived', TRUE);
INSERT INTO public.Users (UserID, EmployeeID, Role) VALUES (1, 1, 'Admin'), (2, 2, 'Employee'), (3, 3, 'Manager');
INSERT INTO public.Groups (GroupID, GroupName, ManagerID) VALUES (1, 'Lab', 3);
INSERT INTO public.GroupMembers (GroupID, EmployeeID) VALUES (1, 2), (1, 3);
-- Dummy certificates: 1 active (expires in 30 days), 2 a deletion request, 3 awaiting approval,
-- 4 Alice's active one, 5 Bob's OLDER 'First Aid' (expires in 10 days; renewed by 1, so no reminder while 1 exists)
INSERT INTO public.TrainingCertificates (CertificateID, EmployeeID, CertificateName, Key, HRS, Provider, IssueDate, ExpiryDate, S3Key, Status, IsMarkedForDeletion) VALUES
    (1, 2, 'First Aid',       'HS', 8, 'Dummy Ltd', CURRENT_DATE - 300, CURRENT_DATE + 30, 'Bob_2/first-aid.pdf', 'Completed', FALSE),
    (2, 2, 'Manual Handling', 'HS', 2, 'Dummy Ltd', CURRENT_DATE - 200, CURRENT_DATE + 90, 'Bob_2/manual.pdf',    'Completed', TRUE),
    (3, 2, 'Forklift',        'OP', 4, 'Dummy Ltd', CURRENT_DATE - 5,   CURRENT_DATE + 700, 'Bob_2/forklift.pdf', 'Pending',   FALSE),
    (4, 1, 'First Aid',       'HS', 8, 'Dummy Ltd', CURRENT_DATE - 100, CURRENT_DATE + 60, 'Alice_1/fa.pdf',      'Completed', FALSE),
    (5, 2, 'First Aid',       'HS', 8, 'Dummy Ltd', CURRENT_DATE - 700, CURRENT_DATE + 10, 'Bob_2/fa-old.pdf',    'Completed', FALSE);
