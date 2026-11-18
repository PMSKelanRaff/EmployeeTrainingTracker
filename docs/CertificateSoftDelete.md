# Certificate soft delete (migration 008, 2026-11-18)

Certificates are no longer removed from the database, and their files are no longer removed from S3. Deleting one
marks it as deleted; it disappears from every list in the app, and an Admin can restore it.

## Decisions (2026-11-18)

- Soft delete **all** certificate deletes: Tasks > Delete (approving a deletion request), Tasks > Reject (an upload
  awaiting approval), and Delete on the Certificates tab (Admin and Manager).
- **Keep the S3 file**, so a restore brings back the document too.
- **Admins restore** from a grey "Deleted certificates" table on the Admin Tasks tab (who deleted it, when, why).
- Admins now see **all** certificates awaiting approval (before: only those of groups they manage), as they already
  did for deletion requests. Managers are unchanged (their groups only).

## Database (`Database/Migrations/008_certificate_soft_delete.sql`, rollback alongside)

Four nullable columns on `TrainingCertificates`: `DeletedAt`, `DeletedByEmployeeID`, `DeletedByName`,
`DeletedReason` (`DeletionRequestApproved` | `UploadRejected` | `DeletedDirectly`), and two CHECKs:

- `trainingcertificates_deleted_consistent`: DeletedAt and DeletedReason are set together;
- `trainingcertificates_deleted_stays_marked`: a deleted row keeps `IsMarkedForDeletion = TRUE`.

**The certificate notifier needs no change**: it already skips rows marked for deletion and `Pending` rows, and the
second CHECK guarantees every deleted row is marked. A deleted renewal no longer hides the older certificate's
reminder (correct: the renewal is gone). Tested with a verbatim copy of the notifier's selection (tests_008 T4-T13).

The rollback refuses while any certificate is soft-deleted (dropping the columns would turn them back into visible
deletion requests and lose who/when) - restore them first.

**Order:** install 008 before publishing the app version that uses it (its queries need the columns). The version
published before it ignores the columns and keeps working.

## Behaviour

| Action | Result | Restore puts it back as |
|---|---|---|
| Tasks > Delete (deletion request) | deleted, reason "Deletion request approved" | active certificate, no longer marked for deletion |
| Tasks > Reject (awaiting approval) | deleted, status stays Pending, reason "Rejected while awaiting approval" | awaiting approval again |
| Certificates tab > Delete | deleted, reason "Deleted on the Certificates tab" | active certificate |

Every query in the app that reads or changes `TrainingCertificates` filters `DeletedAt IS NULL` (certificate lists,
Tasks tables, reports, HTSF13 export, manager access check, edit, approve, mark/unmark). A source-check test fails if
a query without it is added, if anything deletes a certificate row, or if a dashboard deletes an S3 file.
`RestoreCertificate` re-checks that the caller is an active Admin. Purging old deleted certificates is not built.

## Tests

008 SQL suite 14/14; runner scenarios T-W (apply, repeat refused, rollback x2 restores schema and data exactly,
rollback refused while something is deleted, marker check); C# 11 tests through `CertificateService` (delete
reasons, who, refusals, restore by Admin only, archived Admin refused, rejected upload back to Pending); source
check of 16 certificate queries. All against a disposable local database with dummy certificates.
