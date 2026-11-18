-- Tests for migration 009 (Skills Matrix cell evidence). DISPOSABLE TEST DATABASE ONLY.
-- Expects: stand-in schema (dummy certificates 1-5), 004, fixture import 1, 005, 008, then 009.
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
-- A live cell of Bob (employee 2) on the Lab sheet
CREATE TEMP TABLE bobcell AS
SELECT c.SkillID, c.ColumnID FROM public.SkillMatrixLiveCells c JOIN public.SkillMatrixLiveColumns col ON col.ColumnID = c.ColumnID
WHERE col.EmployeeID = 2 ORDER BY c.SkillID LIMIT 1;

SELECT pg_temp.ok('X1 table, function and both indexes carry the 009 marker',
    obj_description('public.skillmatrixcellevidence'::regclass, 'pg_class') = 'Skills Matrix cell evidence (migration 009)'
    AND obj_description('public.skillmatrixcellevidence_rejectdelete()'::regprocedure, 'pg_proc') = 'Skills Matrix cell evidence (migration 009)'
    AND obj_description('public.ux_skillmatrixcellevidence_current'::regclass, 'pg_class') = 'Skills Matrix cell evidence (migration 009)'
    AND obj_description('public.ix_skillmatrixcellevidence_certificate'::regclass, 'pg_class') = 'Skills Matrix cell evidence (migration 009)');
SELECT pg_temp.ok('X2 two triggers (delete, truncate); no rows yet',
    (SELECT COUNT(*) FROM pg_trigger WHERE tgrelid = 'public.skillmatrixcellevidence'::regclass AND NOT tgisinternal) = 2
    AND (SELECT COUNT(*) FROM public.SkillMatrixCellEvidence) = 0);

INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByName)
SELECT SkillID, ColumnID, 2, 1, 3, 'test' FROM bobcell;
SELECT pg_temp.ok('X3 a link can be added', (SELECT COUNT(*) FROM public.SkillMatrixCellEvidence) = 1);
SELECT pg_temp.fails('X4 a second CURRENT link on the same cell is rejected',
    $q$INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByName)
       SELECT SkillID, ColumnID, 2, 5, 3, 'test' FROM bobcell$q$, '%ux_skillmatrixcellevidence_current%');
UPDATE public.SkillMatrixCellEvidence SET RemovedAt = now(), RemovedByName = 'test', RemovedReason = 'Replaced';
INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByName)
SELECT SkillID, ColumnID, 2, 5, 3, 'test' FROM bobcell;
SELECT pg_temp.ok('X5 after closing it, the cell can get a new current link (history kept)',
    (SELECT COUNT(*) FROM public.SkillMatrixCellEvidence) = 2 AND (SELECT COUNT(*) FROM public.SkillMatrixCellEvidence WHERE RemovedAt IS NULL) = 1);
SELECT pg_temp.fails('X6 deleting a link is rejected', $q$DELETE FROM public.SkillMatrixCellEvidence$q$, '%not deleted%');
SELECT pg_temp.fails('X7 truncating is rejected', $q$TRUNCATE public.SkillMatrixCellEvidence$q$, '%not deleted%');
SELECT pg_temp.fails('X8 level 6 is rejected',
    $q$INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByName) VALUES (1, 1, 2, 1, 6, 't')$q$, '%check constraint%');
SELECT pg_temp.fails('X9 an unknown certificate is rejected (foreign key)',
    $q$INSERT INTO public.SkillMatrixCellEvidence (SkillID, ColumnID, EmployeeID, CertificateID, LinkedLevel, LinkedByName) VALUES (1, 1, 2, 999, 3, 't')$q$, '%foreign key%');
SELECT pg_temp.fails('X10 closed without a reason is rejected',
    $q$UPDATE public.SkillMatrixCellEvidence SET RemovedAt = now() WHERE RemovedAt IS NULL$q$, '%removed_consistent%');
SELECT pg_temp.fails('X11 an unknown reason is rejected',
    $q$UPDATE public.SkillMatrixCellEvidence SET RemovedAt = now(), RemovedReason = 'Oops' WHERE RemovedAt IS NULL$q$, '%check constraint%');
SELECT pg_temp.fails('X12 a linked certificate cannot be hard-deleted (it is soft-deleted instead, migration 008)',
    $q$DELETE FROM public.TrainingCertificates WHERE CertificateID = 5$q$, '%foreign key%');
SELECT pg_temp.ok('X13 the live cells and certificates were not touched',
    (SELECT COUNT(*) FROM public.TrainingCertificates) = 5 AND (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action <> 'Reseed') = 0);

SELECT n, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS r, name, CASE WHEN passed THEN '' ELSE COALESCE(detail, '') END AS detail
FROM results ORDER BY n;
SELECT 'RESULT ' || COUNT(*) FILTER (WHERE passed) || '/' || COUNT(*) AS summary FROM results;
DO $$ BEGIN IF EXISTS (SELECT 1 FROM results WHERE NOT passed) THEN RAISE EXCEPTION 'some 009 tests failed'; END IF; END $$;
