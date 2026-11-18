-- 005_skills_matrix_live_rollback.sql
-- Undoes 005_skills_matrix_live.sql by dropping ONLY the objects it created (the live copy and its change log).
-- Migration 004's imported snapshots are not touched.
--
-- Safety (same approach as 004_skills_matrix_rollback.sql):
--   * Step 1 verifies every object about to be dropped carries the exact marker comment
--     'Skills Matrix live (migration 005)'. If a same-named object exists WITHOUT it, nothing is dropped.
--   * Every object is named explicitly and NO CASCADE is used.
--   * All-or-nothing (own BEGIN ... COMMIT), SET LOCAL search_path = pg_catalog, pg_temp, lock_timeout 5s.
--   * Locking: dropping SkillMatrixLiveColumns removes its foreign key's internal triggers from Employees,
--     which takes an ACCESS EXCLUSIVE lock on Employees until COMMIT (reads and writes wait, at most ~5 s).
--   * WARNING: this permanently deletes every edit made in the application and the change log.
--     Take a backup (and, if wanted, an export of SkillMatrixLiveChanges) first.
--   * Repeatable: once the objects are gone, running it again changes nothing.
--
-- How to run: psql "host=... sslmode=require" -X -v ON_ERROR_STOP=1 -f 005_skills_matrix_live_rollback.sql
-- (a new session; no -1). Roll back 005 before 004.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

-- 1. Verify ownership of everything that will be dropped -----------------------------------------
DO $$
DECLARE
    v_marker CONSTANT TEXT := 'Skills Matrix live (migration 005)';
    v_name   TEXT;
    v_oid    OID;
BEGIN
    FOREACH v_name IN ARRAY ARRAY['skillmatrixlivestate', 'skillmatrixliveareas', 'skillmatrixlivecategories',
                                  'skillmatrixliveskills', 'skillmatrixlivecolumns', 'skillmatrixlivecells',
                                  'skillmatrixlivechanges']
    LOOP
        v_oid := to_regclass('public.' || v_name);
        IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_class') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: table % exists but was not created by migration 005 (marker missing).', v_name;
        END IF;
    END LOOP;

    FOREACH v_name IN ARRAY ARRAY['skillmatrixlive_rejectchange()', 'skillmatrixlive_guardrow()', 'skillmatrixlive_guardcell()',
                                  'skillmatrixlive_guarddelete()', 'skillmatrixlive_seed(bigint,bigint,text)']
    LOOP
        v_oid := to_regprocedure('public.' || v_name);
        IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_proc') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: function % exists but was not created by migration 005 (marker missing).', v_name;
        END IF;
    END LOOP;
END;
$$;

-- 2. Tables: children first (no CASCADE) ------------------------------------------------------------
DROP TABLE IF EXISTS public.SkillMatrixLiveChanges;
DROP TABLE IF EXISTS public.SkillMatrixLiveCells;
DROP TABLE IF EXISTS public.SkillMatrixLiveSkills;
DROP TABLE IF EXISTS public.SkillMatrixLiveColumns;
DROP TABLE IF EXISTS public.SkillMatrixLiveCategories;
DROP TABLE IF EXISTS public.SkillMatrixLiveAreas;
DROP TABLE IF EXISTS public.SkillMatrixLiveState;

-- 3. Functions ----------------------------------------------------------------------------------------
DROP FUNCTION IF EXISTS public.SkillMatrixLive_Seed(BIGINT, BIGINT, TEXT);
DROP FUNCTION IF EXISTS public.SkillMatrixLive_GuardDelete();
DROP FUNCTION IF EXISTS public.SkillMatrixLive_GuardCell();
DROP FUNCTION IF EXISTS public.SkillMatrixLive_GuardRow();
DROP FUNCTION IF EXISTS public.SkillMatrixLive_RejectChange();

COMMIT;
