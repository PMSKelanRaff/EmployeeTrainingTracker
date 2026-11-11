-- 006_skills_matrix_live_keys_rollback.sql
-- Undoes 006_skills_matrix_live_keys.sql: drops SkillMatrixLiveLevels, SkillMatrixLiveTeamLabels and
-- SkillMatrixLive_SeedLevels (only if they carry the marker 'Skills Matrix live keys (migration 006)'), and
-- restores migration 005's body of SkillMatrixLive_Seed (copied verbatim below). The live cells, skills, people
-- and change log are not touched; the live copy then shows each sheet's key as imported again.
-- All-or-nothing (own BEGIN ... COMMIT), no CASCADE, lock_timeout 5s; takes no lock on Employees.
-- How to run: psql "host=... sslmode=require" -X -v ON_ERROR_STOP=1 -f 006_skills_matrix_live_keys_rollback.sql
-- Roll back 006 before 005.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
DECLARE
    v_marker CONSTANT TEXT := 'Skills Matrix live keys (migration 006)';
    v_name   TEXT;
    v_oid    OID;
BEGIN
    FOREACH v_name IN ARRAY ARRAY['skillmatrixlivelevels', 'skillmatrixliveteamlabels']
    LOOP
        v_oid := to_regclass('public.' || v_name);
        IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_class') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: table % exists but was not created by migration 006 (marker missing).', v_name;
        END IF;
    END LOOP;
    v_oid := to_regprocedure('public.skillmatrixlive_seedlevels()');
    IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_proc') IS DISTINCT FROM v_marker THEN
        RAISE EXCEPTION 'Refusing to roll back: function skillmatrixlive_seedlevels() was not created by migration 006 (marker missing).';
    END IF;
END;
$$;

-- 1. Migration 005's SkillMatrixLive_Seed, exactly as 005 created it (005 must still be installed)
DO $outer$
BEGIN
    IF to_regprocedure('public.skillmatrixlive_seed(bigint,bigint,text)') IS NULL THEN
        RAISE EXCEPTION 'Migration 005 is not installed; roll back 006 before 005.';
    END IF;
END;
$outer$;
CREATE OR REPLACE FUNCTION public.SkillMatrixLive_Seed(p_import_id BIGINT, p_by_employee_id BIGINT, p_by_name TEXT) RETURNS INT
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_state   TEXT;
    v_cells   INT;
    v_prev    BIGINT;
BEGIN
    IF p_by_name IS NULL OR btrim(p_by_name) = '' THEN
        RAISE EXCEPTION 'SkillMatrixLive_Seed needs the name of who is seeding.';
    END IF;
    SELECT LoadState INTO v_state FROM public.SkillMatrixImports WHERE ImportID = p_import_id;
    IF v_state IS DISTINCT FROM 'Complete' THEN
        RAISE EXCEPTION 'Import % is not a Complete import (state: %).', p_import_id, COALESCE(v_state, 'not found');
    END IF;
    SELECT BasedOnImportID INTO v_prev FROM public.SkillMatrixLiveState;

    PERFORM set_config('skillmatrix.live_seed', 'on', true);   -- this transaction only

    DELETE FROM public.SkillMatrixLiveCells;
    DELETE FROM public.SkillMatrixLiveSkills;
    DELETE FROM public.SkillMatrixLiveColumns;
    DELETE FROM public.SkillMatrixLiveCategories;
    DELETE FROM public.SkillMatrixLiveAreas;

    INSERT INTO public.SkillMatrixLiveAreas (SourceAreaID, Name, DisplayOrder)
    SELECT a.AreaID, a.Name, a.DisplayOrder
    FROM public.SkillMatrixAreas a WHERE a.ImportID = p_import_id;

    -- DisplayOrder in steps of 1000, so rows added later can be placed between existing ones
    INSERT INTO public.SkillMatrixLiveCategories (AreaID, Name, DisplayOrder, SourceCategoryID)
    SELECT la.AreaID, c.Name,
           1000 * ROW_NUMBER() OVER (PARTITION BY c.AreaID ORDER BY c.DisplayOrder, c.CategoryID), c.CategoryID
    FROM public.SkillMatrixCategories c
    JOIN public.SkillMatrixLiveAreas la ON la.SourceAreaID = c.AreaID;

    INSERT INTO public.SkillMatrixLiveSkills (AreaID, CategoryID, Name, InabAccredited, InabRaw, HiddenInSource, DisplayOrder, SourceSkillID)
    SELECT la.AreaID, lc.CategoryID, s.Name, s.InabAccredited, s.InabRaw, s.HiddenInSource,
           1000 * ROW_NUMBER() OVER (PARTITION BY s.AreaID ORDER BY s.DisplayOrder, s.SourceRow, s.SkillID), s.SkillID
    FROM public.SkillMatrixSkills s
    JOIN public.SkillMatrixLiveAreas la      ON la.SourceAreaID = s.AreaID
    JOIN public.SkillMatrixLiveCategories lc ON lc.SourceCategoryID = s.CategoryID;

    INSERT INTO public.SkillMatrixLiveColumns (AreaID, EmployeeID, HeaderText, SubGroupLabel, DisplayOrder, SourceColumnID)
    SELECT la.AreaID, c.EmployeeID, c.SourceHeaderText, c.SubGroupLabel,
           1000 * ROW_NUMBER() OVER (PARTITION BY c.AreaID ORDER BY c.DisplayOrder, c.ColumnID), c.ColumnID
    FROM public.SkillMatrixColumns c
    JOIN public.SkillMatrixLiveAreas la ON la.SourceAreaID = c.AreaID
    WHERE c.Disposition = 'Member';

    INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status, Level, RawValue, FlagReason, SourceCellID)
    SELECT ls.SkillID, lcol.ColumnID, ls.AreaID, c.Status, c.Level, c.RawValue, c.FlagReason, c.CellID
    FROM public.SkillMatrixCells c
    JOIN public.SkillMatrixLiveSkills ls    ON ls.SourceSkillID = c.SkillID
    JOIN public.SkillMatrixLiveColumns lcol ON lcol.SourceColumnID = c.ColumnID;
    GET DIAGNOSTICS v_cells = ROW_COUNT;

    PERFORM set_config('skillmatrix.live_seed', 'off', true);

    INSERT INTO public.SkillMatrixLiveState (Singleton, BasedOnImportID, SeededAt, SeededBy)
    VALUES (TRUE, p_import_id, CURRENT_TIMESTAMP, p_by_name)
    ON CONFLICT (Singleton) DO UPDATE
        SET BasedOnImportID = EXCLUDED.BasedOnImportID, SeededAt = EXCLUDED.SeededAt, SeededBy = EXCLUDED.SeededBy;

    INSERT INTO public.SkillMatrixLiveChanges (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, OldValue, NewValue, Note)
    VALUES (p_by_employee_id, p_by_name, p_import_id, 'Reseed',
            CASE WHEN v_prev IS NULL THEN NULL ELSE 'import ' || v_prev END,
            'import ' || p_import_id,
            'Live copy rebuilt from import ' || p_import_id || ': ' || v_cells || ' cells. Earlier edits remain in this log.');

    RETURN v_cells;
END;
$$;

-- 2. Objects created by 006
DROP TABLE IF EXISTS public.SkillMatrixLiveLevels;
DROP TABLE IF EXISTS public.SkillMatrixLiveTeamLabels;
DROP FUNCTION IF EXISTS public.SkillMatrixLive_SeedLevels();

COMMIT;