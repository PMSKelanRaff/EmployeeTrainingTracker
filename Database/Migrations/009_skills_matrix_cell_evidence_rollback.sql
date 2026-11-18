-- 009_skills_matrix_cell_evidence_rollback.sql
-- Undoes 009_skills_matrix_cell_evidence.sql: drops SkillMatrixCellEvidence (its indexes and triggers go with it)
-- and SkillMatrixCellEvidence_RejectDelete - only if they carry the marker 'Skills Matrix cell evidence (migration 009)'.
-- REFUSES (changes nothing) while the table holds any link, current or closed: that would lose which certificate
-- backed which level. Decide explicitly what to do with them first.
-- Roll back only together with (or after) going back to an app version that does not use the table.
-- All-or-nothing (own BEGIN ... COMMIT), no CASCADE, lock_timeout 5s.
-- How to run: psql "host=... sslmode=require" -X -v ON_ERROR_STOP=1 -f 009_skills_matrix_cell_evidence_rollback.sql

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
DECLARE
    v_marker CONSTANT TEXT := 'Skills Matrix cell evidence (migration 009)';
    v_oid OID := to_regclass('public.skillmatrixcellevidence');
    v_rows BIGINT;
BEGIN
    IF v_oid IS NOT NULL THEN
        IF obj_description(v_oid, 'pg_class') IS DISTINCT FROM v_marker THEN
            RAISE EXCEPTION 'Refusing to roll back: table skillmatrixcellevidence exists but was not created by migration 009 (marker missing).';
        END IF;
        EXECUTE 'SELECT COUNT(*) FROM public.SkillMatrixCellEvidence' INTO v_rows;
        IF v_rows > 0 THEN
            RAISE EXCEPTION 'Refusing to roll back: SkillMatrixCellEvidence holds % evidence link(s). Nothing was changed.', v_rows;
        END IF;
    END IF;
    v_oid := to_regprocedure('public.skillmatrixcellevidence_rejectdelete()');
    IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_proc') IS DISTINCT FROM v_marker THEN
        RAISE EXCEPTION 'Refusing to roll back: function skillmatrixcellevidence_rejectdelete() was not created by migration 009 (marker missing).';
    END IF;
END;
$$;

DROP TABLE IF EXISTS public.SkillMatrixCellEvidence;
DROP FUNCTION IF EXISTS public.SkillMatrixCellEvidence_RejectDelete();

COMMIT;
