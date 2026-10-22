-- 004_skills_matrix_rollback.sql
-- Undoes 004_skills_matrix.sql by dropping ONLY the Skills Matrix objects it created.
--
-- Safety:
--   * Step 1 verifies that every object about to be dropped carries the exact marker comment
--     'Skills Matrix (migration 004)' set by the migration. If any same-named object exists WITHOUT
--     that marker (i.e. something this migration did not create), the script stops and drops nothing.
--   * Every object is named explicitly and NO CASCADE is used: if any other object depends on one of
--     these tables or functions, the DROP fails instead of removing the dependent object.
--   * The check covers the 9 tables and 4 functions; indexes, constraints, sequences and triggers
--     belong to those tables and are dropped with them.
--   * No existing table (Employees, Users, Groups, TrainingCertificates, ...) is touched. The only
--     link to existing data is the foreign key SkillMatrixColumns -> Employees; dropping
--     SkillMatrixColumns removes that constraint and its internal triggers, not any employee row.
--   * Dropping a table also drops its own triggers, indexes and constraints. DROP is DDL, so the
--     insert-only triggers (which block UPDATE/DELETE/TRUNCATE) do not prevent it.
--   * Order: child tables before parents (foreign keys), tables before the trigger functions their
--     triggers use.
--   * WARNING: this permanently deletes all imported Skills Matrix snapshots. The audited workbook is
--     unaffected, but take a backup first if an approved import has been loaded.
--
--   * Schema: the ownership check and every DROP name the same objects, explicitly in schema 'public'.
--     The session search_path is set to pg_catalog, pg_temp for this transaction only, so no name can
--     resolve to a same-named object in another schema.
--   * All-or-nothing: the script contains its own BEGIN ... COMMIT. If the ownership check (or any
--     DROP) fails, the transaction is aborted, every later DROP is refused, and the final COMMIT is
--     turned into a ROLLBACK by the server - whether or not the client stops at the first error.
--   * Locking: dropping SkillMatrixColumns removes the foreign key's internal triggers from
--     Employees, which takes an ACCESS EXCLUSIVE lock on Employees until COMMIT: other sessions can
--     neither read nor write Employees while this script waits for that lock or holds it. SET LOCAL
--     lock_timeout = '5s' limits each wait to 5 seconds, then the script fails and rolls back. So the
--     application's employee reads and writes can be delayed by up to about 5 s - run it at a quiet time.
--   * Repeatable: once the objects are gone, running it again changes nothing.
--
-- How to run (supported, tested method: psql, from a NEW session, as a complete file):
--   psql "host=<host> port=5432 dbname=<db> user=<user> sslmode=require" -X -v ON_ERROR_STOP=1 -f 004_skills_matrix_rollback.sql
--   Do NOT add -1 / --single-transaction. Never run it inside an already-open transaction. Without
--   ON_ERROR_STOP psql exits with code 0 even when the script rolled back - check the output for
--   ROLLBACK. Other clients are not tested; see 004_skills_matrix.sql and docs/SkillsMatrix.md.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;  -- unqualified names fail instead of resolving elsewhere
SET LOCAL lock_timeout = '5s';                 -- wait at most 5 s for any lock, then fail and roll back

-- 1. Verify ownership of everything that will be dropped -----------------------------------------
DO $$
DECLARE
    v_marker CONSTANT TEXT := 'Skills Matrix (migration 004)';
    v_name   TEXT;
    v_oid    OID;
BEGIN
    FOREACH v_name IN ARRAY ARRAY['skillmatriximports', 'skillmatrixscales', 'skillmatrixscalelevels',
                                  'skillmatrixareas', 'skillmatrixcategories', 'skillmatrixskills',
                                  'skillmatrixcolumns', 'skillmatrixcells', 'skillmatrixcellformats']
    LOOP
        v_oid := to_regclass('public.' || v_name);
        IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_class') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: table % exists but was not created by migration 004 (marker missing).', v_name;
        END IF;
    END LOOP;

    FOREACH v_name IN ARRAY ARRAY['skillmatrix_rejectchange()', 'skillmatrix_requireloadingimport()',
                                  'skillmatriximports_guardupdate()', 'skillmatriximports_checkinvariants()']
    LOOP
        v_oid := to_regprocedure('public.' || v_name);
        IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_proc') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: function % exists but was not created by migration 004 (marker missing).', v_name;
        END IF;
    END LOOP;
END;
$$;

-- 2. Tables: children first, parents last (no CASCADE) ---------------------------------------------
DROP TABLE IF EXISTS public.SkillMatrixCellFormats;
DROP TABLE IF EXISTS public.SkillMatrixCells;
DROP TABLE IF EXISTS public.SkillMatrixColumns;
DROP TABLE IF EXISTS public.SkillMatrixSkills;
DROP TABLE IF EXISTS public.SkillMatrixCategories;
DROP TABLE IF EXISTS public.SkillMatrixAreas;
DROP TABLE IF EXISTS public.SkillMatrixScaleLevels;
DROP TABLE IF EXISTS public.SkillMatrixScales;
DROP TABLE IF EXISTS public.SkillMatrixImports;

-- 3. Trigger functions (no longer used once the tables are gone; no CASCADE) -----------------------
DROP FUNCTION IF EXISTS public.SkillMatrixImports_CheckInvariants();
DROP FUNCTION IF EXISTS public.SkillMatrixImports_GuardUpdate();
DROP FUNCTION IF EXISTS public.SkillMatrix_RequireLoadingImport();
DROP FUNCTION IF EXISTS public.SkillMatrix_RejectChange();

COMMIT;
