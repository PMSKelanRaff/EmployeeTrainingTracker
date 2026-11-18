-- 008_certificate_soft_delete_rollback.sql
-- Undoes 008_certificate_soft_delete.sql: drops the two CHECK constraints and the four Deleted* columns.
-- REFUSES (changes nothing) while any certificate is soft-deleted: dropping the columns would turn those rows back
-- into visible "deletion requests" and lose who deleted them and when. Restore them in the app first (or decide
-- explicitly what to do with them). Also refuses a Deleted* column without the 008 marker.
-- Roll back only together with (or after) going back to an app version that does not use these columns.
-- All-or-nothing (own BEGIN ... COMMIT), no CASCADE, lock_timeout 5s.
-- How to run: psql "host=... sslmode=require" -X -v ON_ERROR_STOP=1 -f 008_certificate_soft_delete_rollback.sql

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
DECLARE
    v_col TEXT;
    v_deleted BIGINT;
BEGIN
    FOREACH v_col IN ARRAY ARRAY['deletedat', 'deletedbyemployeeid', 'deletedbyname', 'deletedreason']
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
                   AND table_name = 'trainingcertificates' AND column_name = v_col)
           AND col_description('public.trainingcertificates'::regclass,
                   (SELECT attnum FROM pg_attribute WHERE attrelid = 'public.trainingcertificates'::regclass AND attname = v_col))
               IS DISTINCT FROM 'Certificate soft delete (migration 008)' THEN
            RAISE EXCEPTION 'Refusing to roll back: column % exists but was not created by migration 008 (marker missing).', v_col;
        END IF;
    END LOOP;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
               AND table_name = 'trainingcertificates' AND column_name = 'deletedat') THEN
        EXECUTE 'SELECT COUNT(*) FROM public.TrainingCertificates WHERE DeletedAt IS NOT NULL' INTO v_deleted;
        IF v_deleted > 0 THEN
            RAISE EXCEPTION 'Refusing to roll back: % certificate(s) are soft-deleted. Restore them first; nothing was changed.', v_deleted;
        END IF;
    END IF;
END;
$$;

ALTER TABLE public.TrainingCertificates
    DROP CONSTRAINT IF EXISTS trainingcertificates_deleted_stays_marked,
    DROP CONSTRAINT IF EXISTS trainingcertificates_deleted_consistent,
    DROP COLUMN IF EXISTS DeletedReason,
    DROP COLUMN IF EXISTS DeletedByName,
    DROP COLUMN IF EXISTS DeletedByEmployeeID,
    DROP COLUMN IF EXISTS DeletedAt;

COMMIT;
