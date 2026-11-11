-- Tests for migration 005 (Skills Matrix live copy). DISPOSABLE TEST DATABASE ONLY.
-- Expects: stand-in schema, migration 004, fixture_import.sql (import 1), then migration 005 (which seeds).
-- Prints one line per test and a final 'RESULT passed/total'.
\set ON_ERROR_STOP 1
SET client_min_messages = warning;

CREATE TEMP TABLE results (n SERIAL, name TEXT, passed BOOLEAN, detail TEXT);

CREATE FUNCTION pg_temp.ok(p_name TEXT, p_cond BOOLEAN, p_detail TEXT DEFAULT NULL) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO results (name, passed, detail) VALUES (p_name, COALESCE(p_cond, FALSE), p_detail);
END $$;

-- The statement must fail with an error message matching p_pattern (ILIKE). Runs in a subtransaction.
CREATE FUNCTION pg_temp.fails(p_name TEXT, p_sql TEXT, p_pattern TEXT) RETURNS VOID LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE p_sql;
        INSERT INTO results (name, passed, detail) VALUES (p_name, FALSE, 'no error raised');
        RETURN;
    EXCEPTION WHEN OTHERS THEN
        INSERT INTO results (name, passed, detail) VALUES (p_name, SQLERRM ILIKE p_pattern, SQLERRM);
    END;
END $$;

-- Live IDs by name
CREATE FUNCTION pg_temp.sk(p TEXT) RETURNS BIGINT LANGUAGE sql AS $$ SELECT SkillID FROM public.SkillMatrixLiveSkills WHERE Name = p $$;
CREATE FUNCTION pg_temp.ar(p TEXT) RETURNS BIGINT LANGUAGE sql AS $$ SELECT AreaID FROM public.SkillMatrixLiveAreas WHERE Name = p $$;
CREATE FUNCTION pg_temp.co(p_area TEXT, p_emp BIGINT) RETURNS BIGINT LANGUAGE sql AS $$
    SELECT c.ColumnID FROM public.SkillMatrixLiveColumns c JOIN public.SkillMatrixLiveAreas a ON a.AreaID = c.AreaID
    WHERE a.Name = p_area AND c.EmployeeID = p_emp $$;
CREATE FUNCTION pg_temp.cell(p_skill TEXT, p_area TEXT, p_emp BIGINT) RETURNS public.SkillMatrixLiveCells LANGUAGE sql AS $$
    SELECT * FROM public.SkillMatrixLiveCells WHERE SkillID = pg_temp.sk(p_skill) AND ColumnID = pg_temp.co(p_area, p_emp) $$;

-- ---------------------------------------------------------------------------------------------
-- M: objects
SELECT pg_temp.ok('M1 12 marker objects (7 tables + 5 functions)',
    (SELECT COUNT(*) FROM (
        SELECT obj_description(c.oid, 'pg_class') d FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r'
        UNION ALL SELECT obj_description(p.oid, 'pg_proc') FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace) x
     WHERE d = 'Skills Matrix live (migration 005)') = 12);
SELECT pg_temp.ok('M2 14 triggers on the live tables',
    (SELECT COUNT(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
     WHERE NOT t.tgisinternal AND c.relname LIKE 'skillmatrixlive%') = 14);
SELECT pg_temp.ok('M3 functions pin search_path',
    (SELECT bool_and(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp']) FROM pg_proc p
     WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'skillmatrixlive%'));
SELECT pg_temp.ok('M4 no trigger added to any 004 table or Employees (only FK internals)',
    (SELECT COUNT(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
     WHERE NOT t.tgisinternal AND (c.relname = 'employees' OR (c.relname LIKE 'skillmatrix%' AND c.relname NOT LIKE 'skillmatrixlive%'))) = 28);

-- S: seed from the current import
SELECT pg_temp.ok('S1 live state = import 1, seeded by migration 005',
    (SELECT BasedOnImportID = 1 AND SeededBy = 'migration 005' FROM public.SkillMatrixLiveState));
SELECT pg_temp.ok('S2 counts: 2 areas, 3 categories, 4 skills, 4 member columns (excluded not copied), 7 cells',
    (SELECT COUNT(*) FROM public.SkillMatrixLiveAreas) = 2 AND (SELECT COUNT(*) FROM public.SkillMatrixLiveCategories) = 3
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveSkills) = 4 AND (SELECT COUNT(*) FROM public.SkillMatrixLiveColumns) = 4
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveCells) = 7);
SELECT pg_temp.ok('S3 every live cell equals its imported cell (status, level, raw value, reason)',
    NOT EXISTS (
        SELECT 1 FROM public.SkillMatrixLiveCells l JOIN public.SkillMatrixCells s ON s.CellID = l.SourceCellID
        WHERE (l.Status, l.Level, l.RawValue, l.FlagReason) IS DISTINCT FROM (s.Status, s.Level, s.RawValue, s.FlagReason))
    AND NOT EXISTS (SELECT 1 FROM public.SkillMatrixLiveCells WHERE SourceCellID IS NULL OR Version <> 1 OR UpdatedAt IS NOT NULL));
SELECT pg_temp.ok('S4 cell outside the range stays absent (Density x Bob)', pg_temp.cell('Density', 'Lab', 2) IS NULL
    AND (pg_temp.cell('Density', 'Lab', 2)).SkillID IS NULL);
SELECT pg_temp.ok('S5 skill details copied (INAB, hidden, category on the same sheet)',
    (SELECT InabAccredited AND InabRaw = 'Yes' FROM public.SkillMatrixLiveSkills WHERE Name = 'Abrasion')
    AND (SELECT HiddenInSource FROM public.SkillMatrixLiveSkills WHERE Name = 'Density')
    AND (SELECT c.Name = 'Checks' FROM public.SkillMatrixLiveSkills s JOIN public.SkillMatrixLiveCategories c ON c.CategoryID = s.CategoryID WHERE s.Name = 'Density'));
SELECT pg_temp.ok('S6 order kept: Abrasion < Binder, Alice before Bob',
    (SELECT (SELECT DisplayOrder FROM public.SkillMatrixLiveSkills WHERE Name = 'Abrasion') < (SELECT DisplayOrder FROM public.SkillMatrixLiveSkills WHERE Name = 'Binder'))
    AND (SELECT (SELECT DisplayOrder FROM public.SkillMatrixLiveColumns WHERE ColumnID = pg_temp.co('Lab', 1))
              < (SELECT DisplayOrder FROM public.SkillMatrixLiveColumns WHERE ColumnID = pg_temp.co('Lab', 2))));
SELECT pg_temp.ok('S7 header text and team label copied',
    (SELECT HeaderText = 'Alice A' AND SubGroupLabel = 'Senior' FROM public.SkillMatrixLiveColumns WHERE ColumnID = pg_temp.co('Lab', 1)));
SELECT pg_temp.ok('S8 one Reseed entry logged', (SELECT COUNT(*) = 1 AND bool_and(Action = 'Reseed' AND NewValue = 'import 1')
    FROM public.SkillMatrixLiveChanges));

-- E: editing cells
UPDATE public.SkillMatrixLiveCells SET Level = 4, UpdatedAt = CURRENT_TIMESTAMP, UpdatedByEmployeeID = 1, UpdatedByName = 'Alice Admin'
WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 2) AND Version = 1;
SELECT pg_temp.ok('E1 level 3 -> 4 saved, Version 1 -> 2', (pg_temp.cell('Abrasion', 'Lab', 2)).Level = 4 AND (pg_temp.cell('Abrasion', 'Lab', 2)).Version = 2);
DO $$ DECLARE n INT; BEGIN
    UPDATE public.SkillMatrixLiveCells SET Level = 1, UpdatedAt = CURRENT_TIMESTAMP, UpdatedByName = 'Someone'
    WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 2) AND Version = 1;
    GET DIAGNOSTICS n = ROW_COUNT;
    PERFORM pg_temp.ok('E2 stale save (expected Version 1, now 2) updates nothing', n = 0 AND (pg_temp.cell('Abrasion', 'Lab', 2)).Level = 4);
END $$;
DO $$ BEGIN
    UPDATE public.SkillMatrixLiveCells SET Version = 1 WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 2);
    PERFORM pg_temp.ok('E3 Version cannot be set back (trigger sets OLD+1)', (pg_temp.cell('Abrasion', 'Lab', 2)).Version = 3);
END $$;
SELECT pg_temp.fails('E4 level 6 rejected',
    $q$UPDATE public.SkillMatrixLiveCells SET Level = 6 WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$, '%check constraint%');
SELECT pg_temp.fails('E5 level 0 rejected',
    $q$UPDATE public.SkillMatrixLiveCells SET Level = 0 WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$, '%check constraint%');
SELECT pg_temp.fails('E6 Status Level without a level rejected',
    $q$UPDATE public.SkillMatrixLiveCells SET Level = NULL WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$, '%check constraint%');
SELECT pg_temp.fails('E7 UpdatedAt without UpdatedByName rejected',
    $q$UPDATE public.SkillMatrixLiveCells SET Level = 2, UpdatedAt = CURRENT_TIMESTAMP WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$, '%check constraint%');
UPDATE public.SkillMatrixLiveCells SET Status = 'Unassessed', Level = NULL, UpdatedAt = CURRENT_TIMESTAMP, UpdatedByName = 'Alice Admin'
WHERE SkillID = pg_temp.sk('Binder') AND ColumnID = pg_temp.co('Lab', 2);
SELECT pg_temp.ok('E8 level -> unassessed allowed', (pg_temp.cell('Binder', 'Lab', 2)).Status = 'Unassessed');
SELECT pg_temp.fails('E9 a level cannot become Flagged',
    $q$UPDATE public.SkillMatrixLiveCells SET Status = 'Flagged', Level = NULL, RawValue = 'x', FlagReason = 'y' WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$,
    '%only be changed to a level or unassessed%');
SELECT pg_temp.fails('E10 a flagged cell cannot be edited into another flagged value',
    $q$UPDATE public.SkillMatrixLiveCells SET RawValue = '=1+1' WHERE SkillID = pg_temp.sk('Filing') AND ColumnID = pg_temp.co('Admin', 1)$q$,
    '%only be changed to a level or unassessed%');
UPDATE public.SkillMatrixLiveCells SET Status = 'Level', Level = 2, RawValue = NULL, FlagReason = NULL, UpdatedAt = CURRENT_TIMESTAMP, UpdatedByName = 'Alice Admin'
WHERE SkillID = pg_temp.sk('Filing') AND ColumnID = pg_temp.co('Admin', 1);
SELECT pg_temp.ok('E11 flagged cell resolved to a level; still linked to its imported cell',
    (pg_temp.cell('Filing', 'Admin', 1)).Level = 2 AND (pg_temp.cell('Filing', 'Admin', 1)).SourceCellID IS NOT NULL);
SELECT pg_temp.fails('E12 a cell cannot move to another skill',
    $q$UPDATE public.SkillMatrixLiveCells SET SkillID = pg_temp.sk('Binder') WHERE SkillID = pg_temp.sk('Density') AND ColumnID = pg_temp.co('Lab', 1)$q$,
    '%cannot change%');
SELECT pg_temp.fails('E13 a cell''s source link cannot change',
    $q$UPDATE public.SkillMatrixLiveCells SET SourceCellID = NULL WHERE SkillID = pg_temp.sk('Abrasion') AND ColumnID = pg_temp.co('Lab', 1)$q$, '%cannot change%');

-- N: new rows (skills / people added in the app)
INSERT INTO public.SkillMatrixLiveSkills (AreaID, CategoryID, Name, DisplayOrder)
SELECT pg_temp.ar('Lab'), CategoryID, 'New Skill', 1500 FROM public.SkillMatrixLiveCategories WHERE Name = 'Tests';
INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status)
SELECT pg_temp.sk('New Skill'), c.ColumnID, c.AreaID, 'Unassessed' FROM public.SkillMatrixLiveColumns c WHERE c.AreaID = pg_temp.ar('Lab');
SELECT pg_temp.ok('N1 new skill with unassessed cells for every person on the sheet',
    (SELECT COUNT(*) FROM public.SkillMatrixLiveCells WHERE SkillID = pg_temp.sk('New Skill')) = 2
    AND (SELECT SourceSkillID IS NULL FROM public.SkillMatrixLiveSkills WHERE Name = 'New Skill'));
SELECT pg_temp.fails('N2 a new cell cannot be Flagged',
    $q$INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status, RawValue, FlagReason) VALUES (pg_temp.sk('Density'), pg_temp.co('Lab', 2), pg_temp.ar('Lab'), 'Flagged', 'x', 'y')$q$,
    '%Flagged cells come only from an import%');
SELECT pg_temp.fails('N3 a new cell cannot claim an imported source cell',
    $q$INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status, SourceCellID) VALUES (pg_temp.sk('Density'), pg_temp.co('Lab', 2), pg_temp.ar('Lab'), 'Unassessed', (SELECT MAX(CellID) FROM public.SkillMatrixCells))$q$,
    '%Only the seed%');
SELECT pg_temp.fails('N4 a cell cannot join a skill and a person from different sheets',
    $q$INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status) VALUES (pg_temp.sk('Filing'), pg_temp.co('Lab', 2), pg_temp.ar('Lab'), 'Unassessed')$q$,
    '%foreign key%');
SELECT pg_temp.fails('N5 a skill cannot use a category from another sheet',
    $q$INSERT INTO public.SkillMatrixLiveSkills (AreaID, CategoryID, Name, DisplayOrder) SELECT pg_temp.ar('Admin'), CategoryID, 'X', 1 FROM public.SkillMatrixLiveCategories WHERE Name = 'Tests'$q$,
    '%foreign key%');
SELECT pg_temp.fails('N6 a blank skill name is rejected',
    $q$INSERT INTO public.SkillMatrixLiveSkills (AreaID, CategoryID, Name, DisplayOrder) SELECT pg_temp.ar('Lab'), CategoryID, '  ', 1 FROM public.SkillMatrixLiveCategories WHERE Name = 'Tests'$q$,
    '%check constraint%');
SELECT pg_temp.fails('N7 a person must be an existing employee',
    $q$INSERT INTO public.SkillMatrixLiveColumns (AreaID, EmployeeID, DisplayOrder) VALUES (pg_temp.ar('Lab'), 999, 1)$q$, '%foreign key%');
INSERT INTO public.SkillMatrixLiveColumns (AreaID, EmployeeID, DisplayOrder) VALUES (pg_temp.ar('Lab'), 4, 2500);
SELECT pg_temp.ok('N8 person added to a sheet', pg_temp.co('Lab', 4) IS NOT NULL);
SELECT pg_temp.fails('N9 an employee on the live matrix cannot be hard-deleted',
    $q$DELETE FROM public.Employees WHERE EmployeeID = 4$q$, '%foreign key%');

-- R: row rules
UPDATE public.SkillMatrixLiveSkills SET Name = 'Abrasion Value' WHERE Name = 'Abrasion';
SELECT pg_temp.ok('R1 rename increments Version', (SELECT Version = 2 FROM public.SkillMatrixLiveSkills WHERE Name = 'Abrasion Value'));
UPDATE public.SkillMatrixLiveSkills SET Name = 'Abrasion' WHERE Name = 'Abrasion Value';
UPDATE public.SkillMatrixLiveSkills SET IsDeleted = TRUE WHERE Name = 'Binder';
SELECT pg_temp.ok('R2 soft delete keeps the skill and its cells',
    (SELECT IsDeleted FROM public.SkillMatrixLiveSkills WHERE Name = 'Binder') AND (SELECT COUNT(*) FROM public.SkillMatrixLiveCells WHERE SkillID = pg_temp.sk('Binder')) = 2);
SELECT pg_temp.fails('R3 a skill cannot move to another sheet',
    $q$UPDATE public.SkillMatrixLiveSkills SET AreaID = pg_temp.ar('Admin') WHERE Name = 'Abrasion'$q$, '%cannot move to another sheet%');
SELECT pg_temp.fails('R4 a skill''s source cannot change',
    $q$UPDATE public.SkillMatrixLiveSkills SET SourceSkillID = NULL WHERE Name = 'Abrasion'$q$, '%source cannot change%');
SELECT pg_temp.fails('R5 a column''s employee cannot change',
    $q$UPDATE public.SkillMatrixLiveColumns SET EmployeeID = 4 WHERE ColumnID = pg_temp.co('Lab', 1)$q$, '%cannot change%');
SELECT pg_temp.fails('R6 a category''s source cannot change',
    $q$UPDATE public.SkillMatrixLiveCategories SET SourceCategoryID = NULL WHERE Name = 'Tests'$q$, '%source cannot change%');
SELECT pg_temp.fails('R7 a sheet''s source cannot change',
    $q$UPDATE public.SkillMatrixLiveAreas SET SourceAreaID = NULL WHERE Name = 'Lab'$q$, '%source cannot change%');

-- D: no hard deletes outside the seed
SELECT pg_temp.fails('D1 cell delete rejected',     $q$DELETE FROM public.SkillMatrixLiveCells WHERE SkillID = pg_temp.sk('Abrasion')$q$, '%not deleted%');
SELECT pg_temp.fails('D2 skill delete rejected',    $q$DELETE FROM public.SkillMatrixLiveSkills WHERE Name = 'Density'$q$, '%not deleted%');
SELECT pg_temp.fails('D3 column delete rejected',   $q$DELETE FROM public.SkillMatrixLiveColumns WHERE ColumnID = pg_temp.co('Lab', 4)$q$, '%not deleted%');
SELECT pg_temp.fails('D4 category delete rejected', $q$DELETE FROM public.SkillMatrixLiveCategories WHERE Name = 'Checks'$q$, '%not deleted%');
SELECT pg_temp.fails('D5 sheet delete rejected',    $q$DELETE FROM public.SkillMatrixLiveAreas WHERE Name = 'Admin'$q$, '%not deleted%');
SELECT pg_temp.fails('D6 live state delete rejected', $q$DELETE FROM public.SkillMatrixLiveState$q$, '%not deleted%');
SELECT pg_temp.fails('D7 cell truncate rejected',   $q$TRUNCATE public.SkillMatrixLiveCells$q$, '%not deleted%');

-- C: change log is append-only
INSERT INTO public.SkillMatrixLiveChanges (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, AreaID, AreaName, SkillID, SkillName, ColumnID, EmployeeID, EmployeeName, OldValue, NewValue)
VALUES (1, 'Alice Admin', 1, 'SetCell', pg_temp.ar('Lab'), 'Lab', pg_temp.sk('Abrasion'), 'Abrasion', pg_temp.co('Lab', 2), 2, 'Bob Tester', '3', '4');
SELECT pg_temp.ok('C1 change logged', (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'SetCell') = 1);
SELECT pg_temp.fails('C2 change log update rejected',   $q$UPDATE public.SkillMatrixLiveChanges SET NewValue = '5'$q$, '%append-only%');
SELECT pg_temp.fails('C3 change log delete rejected',   $q$DELETE FROM public.SkillMatrixLiveChanges$q$, '%append-only%');
SELECT pg_temp.fails('C4 change log truncate rejected', $q$TRUNCATE public.SkillMatrixLiveChanges$q$, '%append-only%');
SELECT pg_temp.fails('C5 unknown action rejected',
    $q$INSERT INTO public.SkillMatrixLiveChanges (ChangedByName, BasedOnImportID, Action) VALUES ('x', 1, 'Hack')$q$, '%check constraint%');
SELECT pg_temp.fails('C6 change log needs who',
    $q$INSERT INTO public.SkillMatrixLiveChanges (ChangedByName, BasedOnImportID, Action) VALUES (' ', 1, 'SetCell')$q$, '%check constraint%');

-- Q: seed function
SELECT pg_temp.fails('Q1 seeding from a missing import rejected', $q$SELECT public.SkillMatrixLive_Seed(999, 1, 'Alice')$q$, '%not a Complete import%');
SELECT pg_temp.fails('Q2 seeding needs a name', $q$SELECT public.SkillMatrixLive_Seed(1, 1, '')$q$, '%needs the name%');
SELECT pg_temp.fails('Q3 the seed''s delete permission ends with the seed (same transaction)',
    $q$SELECT public.SkillMatrixLive_Seed(1, 1, 'Alice'); DELETE FROM public.SkillMatrixLiveCells$q$, '%not deleted%');
SELECT pg_temp.ok('Q4 failed seed attempts changed nothing (edits still there)', (pg_temp.cell('Abrasion', 'Lab', 2)).Level = 4
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed') = 1);

-- A second approved import (import 2): Lab only, Abrasion x Alice = 4; becomes current
DO $$
DECLARE imp BIGINT; sc BIGINT; lab BIGINT; cat BIGINT; sk BIGINT; col BIGINT;
BEGIN
    INSERT INTO public.SkillMatrixImports (SourceFileName, SourceSha256, ApprovedBy, ImportedBy) VALUES ('fixture2.xlsx', repeat('B', 64), 'Tester', 'Tester') RETURNING ImportID INTO imp;
    INSERT INTO public.SkillMatrixScales (ImportID, Name, LegendStatus, SourceRef) VALUES (imp, 'Lab key', 'AsWritten', 'Lab!A1:K1') RETURNING ScaleID INTO sc;
    INSERT INTO public.SkillMatrixAreas (ImportID, ScaleID, Name, SourceSheet, DisplayOrder) VALUES (imp, sc, 'Lab', 'Lab', 1) RETURNING AreaID INTO lab;
    INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (lab, 'Tests', 1, 'Lab!A4') RETURNING CategoryID INTO cat;
    INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, DisplayOrder, SourceRow, SourceRef) VALUES (lab, cat, 'Abrasion', 1, 5, 'Lab!A5') RETURNING SkillID INTO sk;
    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, Disposition, EmployeeID, DisplayOrder) VALUES (lab, 'C', 'Alice', 'Lab!C3', 'Member', 1, 1) RETURNING ColumnID INTO col;
    INSERT INTO public.SkillMatrixCells (AreaID, SkillID, ColumnID, Status, Level, SourceCell) VALUES (lab, sk, col, 'Level', 4, 'Lab!C5');
    UPDATE public.SkillMatrixImports SET LoadState = 'Complete' WHERE ImportID = imp;
    UPDATE public.SkillMatrixImports SET IsCurrent = FALSE WHERE IsCurrent AND ImportID <> imp;
    UPDATE public.SkillMatrixImports SET IsCurrent = TRUE WHERE ImportID = imp;
    PERFORM public.SkillMatrixLive_Seed(imp, 1, 'Alice Admin');
END $$;
SELECT pg_temp.ok('Q5 re-seed from import 2: live copy = import 2 only (1 sheet, 1 skill, 1 person, 1 cell)',
    (SELECT BasedOnImportID = 2 AND SeededBy = 'Alice Admin' FROM public.SkillMatrixLiveState)
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveAreas) = 1 AND (SELECT COUNT(*) FROM public.SkillMatrixLiveSkills) = 1
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveColumns) = 1 AND (SELECT COUNT(*) FROM public.SkillMatrixLiveCells) = 1
    AND (pg_temp.cell('Abrasion', 'Lab', 1)).Level = 4 AND (pg_temp.cell('Abrasion', 'Lab', 1)).Version = 1);
SELECT pg_temp.ok('Q6 change log kept across the re-seed (2 Reseed + the SetCell entry)',
    (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed') = 2
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'SetCell') = 1
    AND (SELECT OldValue = 'import 1' AND NewValue = 'import 2' FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed' ORDER BY ChangeID DESC LIMIT 1));
SELECT pg_temp.fails('Q7 the old import''s rows can still not be changed (004 rules intact)',
    $q$UPDATE public.SkillMatrixCells SET Level = 1 WHERE Level = 5$q$, '%read-only%');
SELECT pg_temp.ok('Q8 seed returns its cell count', public.SkillMatrixLive_Seed(2, NULL, 'test') = 1);

-- Z: migration 004 data and Employees: changed only by the deliberate import 2 above
SELECT pg_temp.ok('Z1 snapshot tables differ from the start only by import 2 (import 1 rows unchanged)',
    (SELECT COUNT(*) FROM public.SkillMatrixCells c JOIN public.SkillMatrixAreas a ON a.AreaID = c.AreaID WHERE a.ImportID = 1) = 7
    AND (SELECT COUNT(*) FROM public.SkillMatrixImports) = 2 AND (SELECT COUNT(*) FROM public.Employees) = 5);

-- Report
SELECT n, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS r, name, CASE WHEN passed THEN '' ELSE COALESCE(detail, '') END AS detail
FROM results ORDER BY n;
SELECT 'RESULT ' || COUNT(*) FILTER (WHERE passed) || '/' || COUNT(*) AS summary FROM results;
