-- 008_certificate_soft_delete.sql
-- Certificates are no longer deleted from the database (or their files from S3): a delete marks the row as
-- deleted, hides it everywhere in the app, and an Admin can restore it (Tasks tab -> Deleted certificates).
--
-- Decisions (2026-11-18): soft-delete BOTH 'Delete' (approving a deletion request; also Delete on the
-- Certificates tab) and 'Reject' (an upload awaiting approval); keep the S3 file; Admins restore.
--
-- Adds four nullable columns to public.TrainingCertificates (no default, so no table rewrite):
--   DeletedAt            when it was deleted (NULL = not deleted)
--   DeletedByEmployeeID  who deleted it (no foreign key: the record of who did it must outlive the employee)
--   DeletedByName        their name at the time
--   DeletedReason        DeletionRequestApproved | UploadRejected | DeletedDirectly
-- and two CHECK constraints:
--   * DeletedAt and DeletedReason are set together;
--   * a deleted row keeps IsMarkedForDeletion = TRUE. The certificate notifier
--     (TrainingTrackerCertificateNotifier) already skips rows marked for deletion and 'Pending' rows, so it never
--     emails about a deleted certificate - with no change to the notifier. The constraint guarantees it.
-- Existing rows: all four columns NULL (not deleted); the one row currently marked for deletion stays a pending
-- deletion request. No data is changed.
-- Locking: ALTER TABLE takes a brief ACCESS EXCLUSIVE lock on TrainingCertificates (lock_timeout 5s; fails
-- cleanly rather than waiting if the app holds it). The CHECKs validate the existing ~200 rows instantly.
-- Install BEFORE publishing the app version that uses these columns (that version's queries need them).
-- The app version currently published ignores the new columns and keeps working.
-- How to run: psql -X -v ON_ERROR_STOP=1 -f (all-or-nothing). Undo: 008_certificate_soft_delete_rollback.sql.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
BEGIN
    IF to_regclass('public.trainingcertificates') IS NULL THEN
        RAISE EXCEPTION 'public.TrainingCertificates not found.';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'trainingcertificates'
               AND column_name IN ('deletedat', 'deletedbyemployeeid', 'deletedbyname', 'deletedreason')) THEN
        RAISE EXCEPTION 'Migration 008 is already (partly) installed: a Deleted* column exists.';
    END IF;
END;
$$;

ALTER TABLE public.TrainingCertificates
    ADD COLUMN DeletedAt           TIMESTAMP NULL,
    ADD COLUMN DeletedByEmployeeID BIGINT    NULL,
    ADD COLUMN DeletedByName       TEXT      NULL,
    ADD COLUMN DeletedReason       TEXT      NULL
        CONSTRAINT trainingcertificates_deletedreason_check
        CHECK (DeletedReason IN ('DeletionRequestApproved', 'UploadRejected', 'DeletedDirectly')),
    ADD CONSTRAINT trainingcertificates_deleted_consistent
        CHECK ((DeletedAt IS NULL) = (DeletedReason IS NULL)),
    ADD CONSTRAINT trainingcertificates_deleted_stays_marked
        CHECK (DeletedAt IS NULL OR IsMarkedForDeletion IS TRUE);

COMMENT ON COLUMN public.TrainingCertificates.DeletedAt           IS 'Certificate soft delete (migration 008)';
COMMENT ON COLUMN public.TrainingCertificates.DeletedByEmployeeID IS 'Certificate soft delete (migration 008)';
COMMENT ON COLUMN public.TrainingCertificates.DeletedByName       IS 'Certificate soft delete (migration 008)';
COMMENT ON COLUMN public.TrainingCertificates.DeletedReason       IS 'Certificate soft delete (migration 008)';

SELECT COUNT(*) AS certificates, COUNT(*) FILTER (WHERE DeletedAt IS NOT NULL) AS deleted
FROM public.TrainingCertificates;

COMMIT;
