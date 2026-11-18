-- Tests for migration 008 (certificate soft delete). DISPOSABLE TEST DATABASE ONLY.
-- Expects: stand-in schema (dummy TrainingCertificates 1-5), then 008.
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

-- The certificate notifier's selection (TrainingTrackerCertificateNotifier Program.cs, copied verbatim apart from
-- the Users/e-mail join and the window, which are not about deletion): which certificates would get a reminder.
CREATE FUNCTION pg_temp.notifier_would_remind() RETURNS SETOF BIGINT LANGUAGE sql AS $$
    WITH certs AS (
        SELECT tc.CertificateID, tc.EmployeeID, tc.CertificateName,
               NULLIF(TRIM(tc.ExpiryDate::text), '')::date AS Expiry,
               NULLIF(TRIM(tc.IssueDate::text), '')::date  AS Issued
        FROM TrainingCertificates tc
        INNER JOIN Employees e ON e.EmployeeID = tc.EmployeeID
        WHERE NOT e.IsArchived
          AND NOT COALESCE(tc.IsMarkedForDeletion, FALSE)
          AND COALESCE(tc.Status, '') <> 'Pending'
          AND NULLIF(TRIM(tc.LastNotifiedDate::text), '') IS NULL
    )
    SELECT c.CertificateID FROM certs c
    WHERE c.Expiry >= CURRENT_DATE
      AND c.Expiry <= CURRENT_DATE + make_interval(months => 3)
      AND NOT EXISTS (
          SELECT 1 FROM TrainingCertificates n
          WHERE n.EmployeeID = c.EmployeeID
            AND n.CertificateID <> c.CertificateID
            AND LOWER(TRIM(n.CertificateName)) = LOWER(TRIM(c.CertificateName))
            AND NOT COALESCE(n.IsMarkedForDeletion, FALSE)
            AND (NULLIF(TRIM(n.ExpiryDate::text), '')::date > c.Expiry
                 OR (NULLIF(TRIM(n.ExpiryDate::text), '') IS NULL
                     AND NULLIF(TRIM(n.IssueDate::text), '')::date > c.Issued)))
$$;
-- What the app does on Delete / Reject (CertificateService.SoftDeleteCertificate)
CREATE FUNCTION pg_temp.soft_delete(p_id BIGINT, p_reason TEXT) RETURNS VOID LANGUAGE sql AS $$
    UPDATE public.TrainingCertificates
    SET DeletedAt = CURRENT_TIMESTAMP, DeletedByEmployeeID = 1, DeletedByName = 'Alice Admin', DeletedReason = p_reason, IsMarkedForDeletion = TRUE
    WHERE CertificateID = p_id AND DeletedAt IS NULL $$;
CREATE FUNCTION pg_temp.restore(p_id BIGINT) RETURNS VOID LANGUAGE sql AS $$
    UPDATE public.TrainingCertificates
    SET IsMarkedForDeletion = FALSE, DeletedAt = NULL, DeletedByEmployeeID = NULL, DeletedByName = NULL, DeletedReason = NULL
    WHERE CertificateID = p_id AND DeletedAt IS NOT NULL $$;

-- Objects and existing data
SELECT pg_temp.ok('T1 four Deleted* columns, each with the 008 marker',
    (SELECT COUNT(*) FROM pg_attribute WHERE attrelid = 'public.trainingcertificates'::regclass AND NOT attisdropped
       AND attname IN ('deletedat', 'deletedbyemployeeid', 'deletedbyname', 'deletedreason')
       AND col_description(attrelid, attnum) = 'Certificate soft delete (migration 008)') = 4);
SELECT pg_temp.ok('T2 the two named CHECK constraints exist',
    (SELECT COUNT(*) FROM pg_constraint WHERE conrelid = 'public.trainingcertificates'::regclass
       AND conname IN ('trainingcertificates_deleted_consistent', 'trainingcertificates_deleted_stays_marked')) = 2);
SELECT pg_temp.ok('T3 existing rows unchanged: none deleted; the deletion request is still just a request',
    NOT EXISTS (SELECT 1 FROM public.TrainingCertificates WHERE DeletedAt IS NOT NULL OR DeletedReason IS NOT NULL)
    AND (SELECT IsMarkedForDeletion FROM public.TrainingCertificates WHERE CertificateID = 2));
SELECT pg_temp.ok('T4 notifier before any delete: reminds about Bob''s First Aid (1) and Alice''s (4) only (5 is renewed by 1; 2 marked; 3 pending)',
    ARRAY(SELECT * FROM pg_temp.notifier_would_remind() ORDER BY 1) = ARRAY[1, 4]::BIGINT[]);

-- Constraints
SELECT pg_temp.fails('T5 deleted without a reason is rejected',
    $q$UPDATE public.TrainingCertificates SET DeletedAt = now(), IsMarkedForDeletion = TRUE WHERE CertificateID = 4$q$, '%deleted_consistent%');
SELECT pg_temp.fails('T6 a reason without DeletedAt is rejected',
    $q$UPDATE public.TrainingCertificates SET DeletedReason = 'DeletedDirectly' WHERE CertificateID = 4$q$, '%deleted_consistent%');
SELECT pg_temp.fails('T7 a deleted row that is not marked for deletion is rejected (the notifier would see it)',
    $q$UPDATE public.TrainingCertificates SET DeletedAt = now(), DeletedReason = 'DeletedDirectly', IsMarkedForDeletion = FALSE WHERE CertificateID = 4$q$, '%deleted_stays_marked%');
SELECT pg_temp.fails('T8 an unknown reason is rejected',
    $q$UPDATE public.TrainingCertificates SET DeletedAt = now(), DeletedReason = 'Oops', IsMarkedForDeletion = TRUE WHERE CertificateID = 4$q$, '%deletedreason_check%');

-- Soft delete and the notifier
SELECT pg_temp.soft_delete(1, 'DeletedDirectly');
SELECT pg_temp.ok('T9 soft-deleted row is kept, with who / why, and stays marked for deletion',
    (SELECT DeletedAt IS NOT NULL AND DeletedByName = 'Alice Admin' AND DeletedReason = 'DeletedDirectly' AND IsMarkedForDeletion
     FROM public.TrainingCertificates WHERE CertificateID = 1));
SELECT pg_temp.ok('T10 notifier never reminds about a deleted certificate; the older one it no longer renews (5) is reminded again',
    ARRAY(SELECT * FROM pg_temp.notifier_would_remind() ORDER BY 1) = ARRAY[4, 5]::BIGINT[]);
SELECT pg_temp.soft_delete(3, 'UploadRejected');
SELECT pg_temp.ok('T11 a rejected upload keeps its status (Pending) and is not reminded about',
    (SELECT Status = 'Pending' AND DeletedReason = 'UploadRejected' FROM public.TrainingCertificates WHERE CertificateID = 3)
    AND NOT EXISTS (SELECT 1 FROM pg_temp.notifier_would_remind() r WHERE r = 3));
SELECT pg_temp.soft_delete(2, 'DeletionRequestApproved');
SELECT pg_temp.ok('T12 an approved deletion request is soft-deleted (record kept)',
    (SELECT DeletedReason = 'DeletionRequestApproved' FROM public.TrainingCertificates WHERE CertificateID = 2)
    AND (SELECT COUNT(*) FROM public.TrainingCertificates) = 5);

-- Restore
SELECT pg_temp.restore(1);
SELECT pg_temp.ok('T13 restored: active again, unmarked, who/why cleared; notifier back to 1 and 4',
    (SELECT DeletedAt IS NULL AND DeletedReason IS NULL AND DeletedByName IS NULL AND NOT IsMarkedForDeletion FROM public.TrainingCertificates WHERE CertificateID = 1)
    AND ARRAY(SELECT * FROM pg_temp.notifier_would_remind() ORDER BY 1) = ARRAY[1, 4]::BIGINT[]);
SELECT pg_temp.restore(3);
SELECT pg_temp.ok('T14 a restored rejected upload is awaiting approval again',
    (SELECT Status = 'Pending' AND NOT IsMarkedForDeletion AND DeletedAt IS NULL FROM public.TrainingCertificates WHERE CertificateID = 3));

SELECT n, CASE WHEN passed THEN 'PASS' ELSE 'FAIL' END AS r, name, CASE WHEN passed THEN '' ELSE COALESCE(detail, '') END AS detail
FROM results ORDER BY n;
SELECT 'RESULT ' || COUNT(*) FILTER (WHERE passed) || '/' || COUNT(*) AS summary FROM results;
DO $$ BEGIN IF EXISTS (SELECT 1 FROM results WHERE NOT passed) THEN RAISE EXCEPTION 'some 008 tests failed'; END IF; END $$;
