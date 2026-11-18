-- 007_skills_matrix_team_managers.sql
-- Skills Matrix: who manages each team, as the workbook says (the matrix's own chain of command).
-- Requires migration 005. Additive only: one new table; nothing existing is altered.
--
-- Decision (2026-11-18): the app's Groups (set by the director) stay as they are and keep deciding manager access
-- everywhere else in the app. The Skills Matrix instead follows the workbook's team headings, because that is the
-- chain of command for changing it:
--   * a Manager sees the WHOLE sheet(s) on which a team heading names them (every column, as in Excel) and can
--     change the levels of everyone on those sheets except their own; they see no other sheets;
--   * sheets without team headings (H&S Environmental, QMS, Admin) and headings that name no manager
--     ('Support Lab Staff', 'Data Collection Support staff') name nobody, so only Admins edit those;
--   * Admins are unchanged: every sheet, every person.
-- The heading text is the key, so the link survives a re-import as long as the heading is written the same.
-- (Application-level access, as everywhere else in the app - see docs/SkillsMatrix.md.)
--
-- Creates exactly:
--   Table (1): SkillMatrixTeamManagers  (heading as written in the workbook, manager's EmployeeID)
-- Seeds the 9 headings that name a manager (checked against production 2026-11-18). A row is only added when the
-- EmployeeID exists with exactly the expected name and is not archived, so a wrong ID never grants access; any
-- row not added is reported (NOTICE). 'Pat Burns' in the workbook is the employee 'Patrick Burns'.
-- How to run, all-or-nothing and locking: as migrations 004-006 (psql -X -v ON_ERROR_STOP=1 -f, new session,
-- no -1). The foreign key takes a brief SHARE ROW EXCLUSIVE lock on Employees (lock_timeout 5s).
-- Undo with 007_skills_matrix_team_managers_rollback.sql.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
BEGIN
    IF to_regclass('public.skillmatrixlivestate') IS NULL THEN
        RAISE EXCEPTION 'Migration 005 (Skills Matrix live copy) must be installed before migration 007.';
    END IF;
END;
$$;

CREATE TABLE public.SkillMatrixTeamManagers (
    HeadingText  TEXT      NOT NULL CHECK (btrim(HeadingText) <> ''),   -- team heading exactly as in the workbook
    EmployeeID   BIGINT    NOT NULL REFERENCES public.Employees (EmployeeID) ON DELETE CASCADE,
    AddedAt      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    AddedBy      TEXT      NOT NULL CHECK (btrim(AddedBy) <> ''),
    PRIMARY KEY (HeadingText, EmployeeID)
);

DO $$
DECLARE
    v_row     RECORD;
    v_added   INT := 0;
BEGIN
    FOR v_row IN
        SELECT * FROM (VALUES
            ('Dermot Leonard Lab Manager',                           111::BIGINT, 'Dermot Leonard'),
            ('Pat Burns Data Collection Manager',                     23::BIGINT, 'Patrick Burns'),
            ('Eric Donohue Network Team Manager',                     89::BIGINT, 'Eric Donohue'),
            ('Darragh O''Dea Non-Structural Team Manager',            75::BIGINT, 'Darragh O''Dea'),
            ('Monica Loughnane Structural Team Manager',              63::BIGINT, 'Monica Loughnane'),
            ('GIS Team Rory Donnellan Manager',                       68::BIGINT, 'Rory Donnellan'),
            ('Software and Data Analytics Team Manager David Power',  21::BIGINT, 'David Power'),
            ('Equipment Innovation Team Joe Mchale Manager',         133::BIGINT, 'Joe McHale'),
            ('Innovation Team Kieran Feighan Manager',               138::BIGINT, 'Kieran Feighan')
        ) AS t (heading, employee_id, expected_name)
    LOOP
        INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy)
        SELECT v_row.heading, e.EmployeeID, 'migration 007'
        FROM public.Employees e
        WHERE e.EmployeeID = v_row.employee_id AND e.FullName = v_row.expected_name AND NOT e.IsArchived;
        IF FOUND THEN
            v_added := v_added + 1;
        ELSE
            RAISE NOTICE 'Not added: "%" -> employee % (%) - no active employee with that ID and name.',
                v_row.heading, v_row.employee_id, v_row.expected_name;
        END IF;
    END LOOP;
    RAISE NOTICE 'Team managers added: % of 9.', v_added;
END;
$$;

COMMENT ON TABLE public.SkillMatrixTeamManagers IS 'Skills Matrix team managers (migration 007)';

SELECT COUNT(*) AS team_managers_added FROM public.SkillMatrixTeamManagers;

COMMIT;
