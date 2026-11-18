-- Tests for migration 007 (Skills Matrix team managers). DISPOSABLE TEST DATABASE ONLY.
-- Expects: stand-in schema, 004, fixture import 1, 005, 006, then (added by the runner before 007) stand-in employees
--   111 'Dermot Leonard' (matches the seed), 23 'Pat Burns' (wrong name: the seed expects 'Patrick Burns'),
--   75 'Darragh O''Dea' archived (the seed skips archived people); then 007.
\set ON_ERROR_STOP 1
SET client_min_messages = warning;

CREATE TEMP TABLE results (n SERIAL, name TEXT, passed BOOLEAN, detail TEXT);
CREATE FUNCTION pg_temp.ok(p_name TEXT, p_cond BOOLEAN, p_detail TEXT DEFAULT NULL) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN INSERT INTO results (name, passed, detail) VALUES (p_name, COALESCE(p_cond, FALSE), p_detail); END $$;
CREATE FUNCTION pg_temp.fails(p_name TEXT, p_sql TEXT, p_pattern TEXT) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
        INSERT INTO results (name, passed, detail) VALUES (p_name, FALSE, 'no error raised');
    EXCEPTION WHEN OTHERS THEN
        INSERT INTO results (name, passed, detail) VALUES (p_name, SQLERRM ILIKE p_pattern, SQLERRM);
    END;
END $$;

SELECT pg_temp.ok('P1 one table with the 007 marker',
    obj_description('public.skillmatrixteammanagers'::regclass, 'pg_class') = 'Skills Matrix team managers (migration 007)'
    AND (SELECT COUNT(*) FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r'
         AND obj_description(c.oid, 'pg_class') = 'Skills Matrix team managers (migration 007)') = 1);
SELECT pg_temp.ok('P2 seed: only the ID + exact name + active match is added (Dermot Leonard)',
    (SELECT COUNT(*) FROM public.SkillMatrixTeamManagers) = 1
    AND EXISTS (SELECT 1 FROM public.SkillMatrixTeamManagers WHERE HeadingText = 'Dermot Leonard Lab Manager' AND EmployeeID = 111 AND AddedBy = 'migration 007'));
SELECT pg_temp.ok('P3 seed: a wrong name never grants access (23 is not ''Patrick Burns'' here)',
    NOT EXISTS (SELECT 1 FROM public.SkillMatrixTeamManagers WHERE EmployeeID = 23));
SELECT pg_temp.ok('P4 seed: an archived employee is not added',
    NOT EXISTS (SELECT 1 FROM public.SkillMatrixTeamManagers WHERE EmployeeID = 75));
SELECT pg_temp.fails('P5 an unknown employee is rejected (foreign key)',
    $q$INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('X', 999999, 't')$q$, '%foreign key%');
SELECT pg_temp.fails('P6 a blank heading is rejected',
    $q$INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('  ', 1, 't')$q$, '%check constraint%');
SELECT pg_temp.fails('P7 the same heading + manager twice is rejected',
    $q$INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('Dermot Leonard Lab Manager', 111, 't')$q$, '%duplicate key%');
INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('Dermot Leonard Lab Manager', 1, 'test');
SELECT pg_temp.ok('P8 a heading can name more than one manager',
    (SELECT COUNT(*) FROM public.SkillMatrixTeamManagers WHERE HeadingText = 'Dermot Leonard Lab Manager') = 2);
DELETE FROM public.Employees WHERE EmployeeID = 111;
SELECT pg_temp.ok('P9 deleting an employee removes their team-manager rows (and nothing else)',
    NOT EXISTS (SELECT 1 FROM public.SkillMatrixTeamManagers WHERE EmployeeID = 111)
    AND (SELECT COUNT(*) FROM public.SkillMatrixTeamManagers) = 1);
SELECT pg_temp.ok('P10 no other Skills Matrix data was touched: no changes logged, no re-seed, team labels as before',
    (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action <> 'Reseed') = 0
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed') = 1
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveTeamLabels) = 9);

SELECT CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END || '  ' || name || COALESCE(CASE WHEN NOT passed THEN '  -- ' || detail END, '')
FROM results ORDER BY n;
SELECT 'RESULT ' || COUNT(*) FILTER (WHERE passed) || '/' || COUNT(*) FROM results;
DO $$ BEGIN IF EXISTS (SELECT 1 FROM results WHERE NOT passed) THEN RAISE EXCEPTION 'some 007 tests failed'; END IF; END $$;
