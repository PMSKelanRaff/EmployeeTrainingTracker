-- Tests for migration 006 (uniform keys per live sheet, team names). DISPOSABLE TEST DATABASE ONLY.
-- Expects: stand-in schema, 004, fixture import 1, 005, one edit (Lab!C5 -> 1 by 'before 006'), then 006.
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
-- A level row of a live sheet, by sheet name
CREATE FUNCTION pg_temp.lv(p_area TEXT, p_level INT) RETURNS public.SkillMatrixLiveLevels LANGUAGE sql AS $$
    SELECT l.* FROM public.SkillMatrixLiveLevels l JOIN public.SkillMatrixLiveAreas a ON a.AreaID = l.AreaID
    WHERE a.Name = p_area AND l.Level = p_level $$;

-- Objects
SELECT pg_temp.ok('K1 3 marker objects (2 tables + 1 function)',
    (SELECT COUNT(*) FROM (
        SELECT obj_description(c.oid, 'pg_class') d FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r'
        UNION ALL SELECT obj_description(p.oid, 'pg_proc') FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace) x
     WHERE d = 'Skills Matrix live keys (migration 006)') = 3);
SELECT pg_temp.ok('K2 2 triggers on SkillMatrixLiveLevels', (SELECT COUNT(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
    WHERE NOT t.tgisinternal AND c.relname = 'skillmatrixlivelevels') = 2);
SELECT pg_temp.ok('K3 seed function still carries the 005 marker', obj_description('public.skillmatrixlive_seed(bigint,bigint,text)'::regprocedure, 'pg_proc') = 'Skills Matrix live (migration 005)');
SELECT pg_temp.ok('K4 9 team names', (SELECT COUNT(*) FROM public.SkillMatrixLiveTeamLabels) = 9
    AND (SELECT DisplayLabel FROM public.SkillMatrixLiveTeamLabels WHERE HeadingText = 'Darragh O''Dea Non-Structural Team Manager') = 'Darragh O''Dea');

-- Levels built for the fixture's import 1 (Lab key: 1, 5; Admin key: 4)
SELECT pg_temp.ok('K5 Lab: 1 and 5 as written, 4 filled in from Admin',
    (pg_temp.lv('Lab', 1)).Label = '1: To be Trained' AND (pg_temp.lv('Lab', 1)).Origin = 'AsWritten'
    AND (pg_temp.lv('Lab', 5)).Origin = 'AsWritten'
    AND (pg_temp.lv('Lab', 4)).Label = '4: Skilled' AND (pg_temp.lv('Lab', 4)).Origin = 'FilledIn');
SELECT pg_temp.ok('K6 levels no sheet defines are not invented (no 2 or 3)',
    NOT EXISTS (SELECT 1 FROM public.SkillMatrixLiveLevels WHERE Level IN (2, 3)));
SELECT pg_temp.ok('K7 the edit made before 006 is kept; no re-seed happened',
    (SELECT Level = 1 AND UpdatedByName = 'before 006' FROM public.SkillMatrixLiveCells
     WHERE SourceCellID = (SELECT CellID FROM public.SkillMatrixCells WHERE SourceCell = 'Lab!C5'))
    AND (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed') = 1);

-- Level rows: versioned, not deletable outside a seed
UPDATE public.SkillMatrixLiveLevels SET Label = '4: Skilled!' WHERE (AreaID, Level) = (SELECT AreaID, Level FROM pg_temp.lv('Lab', 4));
SELECT pg_temp.ok('K8 updating a level increments Version', (pg_temp.lv('Lab', 4)).Version = 2);
SELECT pg_temp.fails('K9 level delete rejected', $q$DELETE FROM public.SkillMatrixLiveLevels$q$, '%not deleted%');
SELECT pg_temp.fails('K10 level 6 rejected',
    $q$INSERT INTO public.SkillMatrixLiveLevels (AreaID, Level, Label, Origin) SELECT AreaID, 6, 'x', 'AsWritten' FROM public.SkillMatrixLiveAreas LIMIT 1$q$, '%check constraint%');
SELECT pg_temp.fails('K11 unknown origin rejected',
    $q$UPDATE public.SkillMatrixLiveLevels SET Origin = 'Guess'$q$, '%check constraint%');
SELECT pg_temp.fails('K12 lowercase / bad colour rejected',
    $q$UPDATE public.SkillMatrixLiveLevels SET FillRgb = 'zzzzzz'$q$, '%check constraint%');

-- A realistic second import: S1, S2 standard keys; S3 words level 4 twice (key 2 = the common wording);
-- S4 lists only 1, 4, 5; S5 has its own scale. Key cells carry colours.
DO $$
DECLARE imp BIGINT; s BIGINT; a BIGINT; cat BIGINT; sk BIGINT; col BIGINT; i INT;
        sheets TEXT[] := ARRAY['S1', 'S2', 'S3', 'S4', 'S5'];
        std TEXT[] := ARRAY['1: To be Trained', '2: In Training', '3: Requires Supervision', '4: Skilled in Area', '5: Skilled to train others'];
        own TEXT[] := ARRAY['1: Awareness', '2: Supported Work', '3: Independent Work', '4: Advanced Work', '5: Lead'];
        fills TEXT[] := ARRAY['9BC2E6', 'FFD966', 'F4B084', 'CC99FF', 'A9D08E'];
        sh TEXT; lvl INT;
BEGIN
    INSERT INTO public.SkillMatrixImports (SourceFileName, SourceSha256, ApprovedBy, ImportedBy) VALUES ('keys.xlsx', repeat('C', 64), 'Tester', 'Tester') RETURNING ImportID INTO imp;
    FOR i IN 1..5 LOOP
        sh := sheets[i];
        INSERT INTO public.SkillMatrixScales (ImportID, Name, LegendStatus, LegendNote, SourceRef)
            VALUES (imp, sh || ' key', CASE WHEN i IN (3, 4) THEN 'NeedsConfirmation' ELSE 'AsWritten' END,
                    CASE WHEN i IN (3, 4) THEN 'check' END, sh || '!A1:K1') RETURNING ScaleID INTO s;
        INSERT INTO public.SkillMatrixAreas (ImportID, ScaleID, Name, SourceSheet, DisplayOrder) VALUES (imp, s, sh, sh, i) RETURNING AreaID INTO a;
        FOR lvl IN 1..5 LOOP
            CONTINUE WHEN i = 4 AND lvl IN (2, 3);
            INSERT INTO public.SkillMatrixScaleLevels (ScaleID, KeyNumber, Level, Label, SourceRef)
                VALUES (s, 1, lvl, CASE WHEN i = 5 THEN own[lvl] WHEN i = 3 AND lvl = 4 THEN '4: Trained' ELSE std[lvl] END, sh || '!' || chr(64 + lvl) || '1');
            INSERT INTO public.SkillMatrixCellFormats (AreaID, SourceRef, FillSource, FillRgb, Pattern, FontSource, FontRgb, Bold)
                VALUES (a, sh || '!' || chr(64 + lvl) || '1', 'CellStyle', CASE WHEN i = 1 THEN fills[lvl] ELSE '00000' || i END, 'solid', 'CellStyle', '000000', FALSE);
        END LOOP;
        IF i = 3 THEN
            INSERT INTO public.SkillMatrixScaleLevels (ScaleID, KeyNumber, Level, Label, SourceRef) VALUES (s, 2, 4, '4: Skilled in Area', 'S3!J1');
            INSERT INTO public.SkillMatrixCellFormats (AreaID, SourceRef, FillSource, FillRgb, Pattern, FontSource, FontRgb, Bold)
                VALUES (a, 'S3!J1', 'CellStyle', 'CC99FF', 'solid', 'CellStyle', '000000', FALSE);
        END IF;
        INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (a, 'Cat', 1, sh || '!A4') RETURNING CategoryID INTO cat;
        INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, DisplayOrder, SourceRow, SourceRef) VALUES (a, cat, 'Skill ' || sh, 1, 5, sh || '!A5') RETURNING SkillID INTO sk;
        INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, SubGroupLabel, Disposition, EmployeeID, DisplayOrder)
            VALUES (a, 'C', 'Alice', sh || '!C3', CASE WHEN i = 1 THEN 'Dermot Leonard Lab Manager' ELSE 'Support Lab Staff' END, 'Member', 1, 1) RETURNING ColumnID INTO col;
        INSERT INTO public.SkillMatrixCells (AreaID, SkillID, ColumnID, Status, Level, SourceCell) VALUES (a, sk, col, 'Level', 4, sh || '!C5');
    END LOOP;
    UPDATE public.SkillMatrixImports SET LoadState = 'Complete' WHERE ImportID = imp;
    UPDATE public.SkillMatrixImports SET IsCurrent = FALSE WHERE IsCurrent AND ImportID <> imp;
    UPDATE public.SkillMatrixImports SET IsCurrent = TRUE WHERE ImportID = imp;
    PERFORM public.SkillMatrixLive_Seed(imp, 1, 'Alice Admin');
END $$;

SELECT pg_temp.ok('R1 re-seed rebuilt the levels: 5 sheets x 5 levels, nothing from import 1 left',
    (SELECT COUNT(*) FROM public.SkillMatrixLiveLevels) = 25
    AND (SELECT COUNT(DISTINCT AreaID) FROM public.SkillMatrixLiveLevels) = 5
    AND NOT EXISTS (SELECT 1 FROM public.SkillMatrixLiveLevels l JOIN public.SkillMatrixLiveAreas a ON a.AreaID = l.AreaID WHERE a.Name = 'Lab'));
SELECT pg_temp.ok('R2 S3 (level 4 worded twice): the common wording, with its own key colour',
    (pg_temp.lv('S3', 4)).Label = '4: Skilled in Area' AND (pg_temp.lv('S3', 4)).Origin = 'MostCommonWording'
    AND (pg_temp.lv('S3', 4)).FillRgb = 'CC99FF' AND (pg_temp.lv('S3', 4)).SourceRef = 'S3!J1');
SELECT pg_temp.ok('R3 S4 (no 2 or 3): filled in with the common wording and colour of the first sheet using it',
    (pg_temp.lv('S4', 2)).Label = '2: In Training' AND (pg_temp.lv('S4', 2)).Origin = 'FilledIn' AND (pg_temp.lv('S4', 2)).FillRgb = 'FFD966'
    AND (pg_temp.lv('S4', 3)).Label = '3: Requires Supervision' AND (pg_temp.lv('S4', 3)).FillRgb = 'F4B084'
    AND (pg_temp.lv('S4', 4)).Origin = 'AsWritten' AND (pg_temp.lv('S4', 4)).FillRgb = '000004');
SELECT pg_temp.ok('R4 S5 (its own scale) keeps its own wording, all as written',
    (SELECT bool_and(l.Origin = 'AsWritten') AND COUNT(*) = 5 AND MIN(l.Label) = '1: Awareness'
     FROM public.SkillMatrixLiveLevels l JOIN public.SkillMatrixLiveAreas a ON a.AreaID = l.AreaID WHERE a.Name = 'S5'));
SELECT pg_temp.ok('R5 standard sheets as written with their own colours',
    (pg_temp.lv('S1', 1)).Origin = 'AsWritten' AND (pg_temp.lv('S1', 1)).FillRgb = '9BC2E6' AND (pg_temp.lv('S2', 5)).FillRgb = '000002');
SELECT pg_temp.ok('R6 the snapshot key is unchanged (S3 still has 4: Trained)',
    EXISTS (SELECT 1 FROM public.SkillMatrixScaleLevels WHERE Label = '4: Trained'));
SELECT pg_temp.ok('R7 re-seed logged once more', (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed') = 2);
SELECT pg_temp.ok('R8 team name lookup: mapped heading -> manager, others as written',
    (SELECT COALESCE(t.DisplayLabel, c.SubGroupLabel) FROM public.SkillMatrixLiveColumns c JOIN public.SkillMatrixLiveAreas a ON a.AreaID = c.AreaID
       LEFT JOIN public.SkillMatrixLiveTeamLabels t ON t.HeadingText = c.SubGroupLabel WHERE a.Name = 'S1') = 'Dermot Leonard'
    AND (SELECT COALESCE(t.DisplayLabel, c.SubGroupLabel) FROM public.SkillMatrixLiveColumns c JOIN public.SkillMatrixLiveAreas a ON a.AreaID = c.AreaID
       LEFT JOIN public.SkillMatrixLiveTeamLabels t ON t.HeadingText = c.SubGroupLabel WHERE a.Name = 'S2') = 'Support Lab Staff');
SELECT pg_temp.ok('R9 SeedLevels alone is repeatable (same 25 rows)', public.SkillMatrixLive_SeedLevels() = 25);

SELECT n, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS r, name, CASE WHEN passed THEN '' ELSE COALESCE(detail, '') END AS detail
FROM results ORDER BY n;
SELECT 'RESULT ' || COUNT(*) FILTER (WHERE passed) || '/' || COUNT(*) AS summary FROM results;
