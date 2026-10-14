-- 002_date_columns.sql
-- Issue, expiry and planned dates were stored as TEXT, with a mix of '2025-03-11' and
-- '2025-03-11 00:00:00'. This converts them to the DATE type, which holds a date only.
-- Existing values are converted in place - nothing is lost.
--
-- LastNotifiedDate is deliberately left as text: nothing in the app writes it, so something
-- outside the app (e.g. a reminder job) probably does, and changing its type could break that.
--
-- Run each statement one at a time in DBeaver (Ctrl+Enter on each), in order.
-- Safe to run more than once.


-- STEP 1 (check): lists any values that would NOT convert to a date.
-- Should return no rows. If it returns rows, fix or clear those values before Step 2.
SELECT 'trainingcertificates' AS tbl, certificateid AS id, 'issuedate' AS col, issuedate::text AS value
FROM trainingcertificates WHERE NULLIF(TRIM(issuedate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$'
UNION ALL
SELECT 'trainingcertificates', certificateid, 'expirydate', expirydate::text
FROM trainingcertificates WHERE NULLIF(TRIM(expirydate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$'
UNION ALL
SELECT 'trainingsessions', sessionid, 'issuedate', issuedate::text
FROM trainingsessions WHERE NULLIF(TRIM(issuedate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$'
UNION ALL
SELECT 'trainingsessions', sessionid, 'expirydate', expirydate::text
FROM trainingsessions WHERE NULLIF(TRIM(expirydate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$'
UNION ALL
SELECT 'trainingsessions', sessionid, 'planneddate', planneddate::text
FROM trainingsessions WHERE NULLIF(TRIM(planneddate::text), '') !~ '^\d{4}-\d{2}-\d{2}( 00:00:00)?$';


-- STEP 2 (convert): blank values become NULL; '2025-03-11 00:00:00' becomes 2025-03-11.
ALTER TABLE trainingcertificates
    ALTER COLUMN issuedate  TYPE date USING NULLIF(TRIM(issuedate::text), '')::date,
    ALTER COLUMN expirydate TYPE date USING NULLIF(TRIM(expirydate::text), '')::date;

ALTER TABLE trainingsessions
    ALTER COLUMN issuedate   TYPE date USING NULLIF(TRIM(issuedate::text), '')::date,
    ALTER COLUMN expirydate  TYPE date USING NULLIF(TRIM(expirydate::text), '')::date,
    ALTER COLUMN planneddate TYPE date USING NULLIF(TRIM(planneddate::text), '')::date;


-- STEP 3 (verify): every row should now say "date".
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('trainingcertificates', 'trainingsessions')
  AND column_name IN ('issuedate', 'expirydate', 'planneddate')
ORDER BY table_name, column_name;
