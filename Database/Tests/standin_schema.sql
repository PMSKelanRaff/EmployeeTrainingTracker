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

INSERT INTO public.Employees (EmployeeID, FullName, IsArchived) VALUES
    (1, 'Alice Admin', FALSE), (2, 'Bob Tester', FALSE), (3, 'Carol Manager', FALSE), (4, 'Dave Spare', FALSE), (5, 'Erin Archived', TRUE);
INSERT INTO public.Users (UserID, EmployeeID, Role) VALUES (1, 1, 'Admin'), (2, 2, 'Employee'), (3, 3, 'Manager');
INSERT INTO public.Groups (GroupID, GroupName, ManagerID) VALUES (1, 'Lab', 3);
INSERT INTO public.GroupMembers (GroupID, EmployeeID) VALUES (1, 2), (1, 3);
