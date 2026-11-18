-- 003_last_notified_date.sql
-- Converts LastNotifiedDate (written by TrainingTrackerCertificateNotifier) from TEXT to DATE.
-- Run AFTER the updated notifier is in place - the updated notifier works with either type,
-- the old one only with TEXT.
--
-- Run each statement one at a time in DBeaver (Ctrl+Enter on each), in order.
-- Safe to run more than once.


-- STEP 1 (check): values that would NOT convert. Should return no rows.
SELECT 'trainingcertificates' AS tbl, certificateid AS id, lastnotifieddate::text AS value
FROM trainingcertificates WHERE NULLIF(TRIM(lastnotifieddate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$'
UNION ALL
SELECT 'trainingsessions', sessionid, lastnotifieddate::text
FROM trainingsessions WHERE NULLIF(TRIM(lastnotifieddate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$';


-- STEP 2 (convert)
ALTER TABLE trainingcertificates
    ALTER COLUMN lastnotifieddate TYPE date USING NULLIF(TRIM(lastnotifieddate::text), '')::date;

ALTER TABLE trainingsessions
    ALTER COLUMN lastnotifieddate TYPE date USING NULLIF(TRIM(lastnotifieddate::text), '')::date;


-- STEP 3 (verify): both rows should say "date".
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND column_name = 'lastnotifieddate'
ORDER BY table_name;
