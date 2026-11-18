-- 007_skills_matrix_team_managers_rollback.sql
-- Undoes 007_skills_matrix_team_managers.sql: drops SkillMatrixTeamManagers (only if it carries the marker
-- 'Skills Matrix team managers (migration 007)'). Nothing else is touched. Afterwards Managers see no sheets
-- of the Skills Matrix (the app treats a missing table as "no team managers"); Admins are unaffected.
-- All-or-nothing (own BEGIN ... COMMIT), no CASCADE, lock_timeout 5s.
-- How to run: psql "host=... sslmode=require" -X -v ON_ERROR_STOP=1 -f 007_skills_matrix_team_managers_rollback.sql

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
DECLARE
    v_oid OID := to_regclass('public.skillmatrixteammanagers');
BEGIN
    IF v_oid IS NOT NULL AND obj_description(v_oid, 'pg_class') IS DISTINCT FROM 'Skills Matrix team managers (migration 007)' THEN
        RAISE EXCEPTION 'Refusing to roll back: table skillmatrixteammanagers exists but was not created by migration 007 (marker missing).';
    END IF;
END;
$$;

DROP TABLE IF EXISTS public.SkillMatrixTeamManagers;

COMMIT;
