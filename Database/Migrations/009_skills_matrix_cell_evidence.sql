-- 009_skills_matrix_cell_evidence.sql
-- Skills Matrix: a certificate linked to a level as supporting evidence (the small square in a cell's corner).
-- Requires migrations 005 (live copy) and 008 (certificate soft delete). Additive only: one new table.
--
-- Decisions (2026-11-18):
--   * one certificate per cell (skill x person), and only an APPROVED (Status 'Completed') certificate of THAT
--     person, not deleted (checked by the app when linking);
--   * the link is valid while the cell's level equals the level it was linked at (3 -> 4 needs relinking;
--     back to 3 is valid again). Validity is worked out when the sheet is shown; nothing is changed by an edit;
--   * a link is never deleted: replacing or removing it closes it (RemovedAt + reason), so the history of which
--     evidence backed which level is kept.
-- The cells it points at are LIVE IDs (SkillID, ColumnID). There is deliberately NO foreign key to the live cells:
-- a re-import rebuilds the live copy with new IDs, and the import carries every current link over to the same
-- sheet / category / skill / person (SkillsMatrixImportWriter); links whose cell is not in the new workbook are
-- closed with reason 'NotInNewImport'.
--
-- Creates exactly:
--   Table (1):     SkillMatrixCellEvidence
--   Indexes (2):   UX_SkillMatrixCellEvidence_Current (one current link per cell), IX_SkillMatrixCellEvidence_Certificate
--   Function (1):  SkillMatrixCellEvidence_RejectDelete   + Trigger (1) rejecting deletes (no CASCADE anywhere)
-- Foreign keys to Employees and TrainingCertificates take a brief SHARE ROW EXCLUSIVE lock on each (lock_timeout 5s).
-- How to run: psql -X -v ON_ERROR_STOP=1 -f (all-or-nothing). Undo: 009_skills_matrix_cell_evidence_rollback.sql.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
BEGIN
    IF to_regclass('public.skillmatrixlivecells') IS NULL THEN
        RAISE EXCEPTION 'Migration 005 (Skills Matrix live copy) must be installed before migration 009.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
                   AND table_name = 'trainingcertificates' AND column_name = 'deletedat') THEN
        RAISE EXCEPTION 'Migration 008 (certificate soft delete) must be installed before migration 009.';
    END IF;
END;
$$;

CREATE TABLE public.SkillMatrixCellEvidence (
    EvidenceID          BIGINT    GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    SkillID             BIGINT    NOT NULL,            -- live IDs; no FK (see above)
    ColumnID            BIGINT    NOT NULL,
    EmployeeID          BIGINT    NOT NULL REFERENCES public.Employees (EmployeeID) ON DELETE RESTRICT,     -- the person (column)
    CertificateID       BIGINT    NOT NULL REFERENCES public.TrainingCertificates (CertificateID) ON DELETE RESTRICT,
    LinkedLevel         SMALLINT  NOT NULL CHECK (LinkedLevel BETWEEN 1 AND 5),
    LinkedAt            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LinkedByEmployeeID  BIGINT    NULL,
    LinkedByName        TEXT      NOT NULL CHECK (btrim(LinkedByName) <> ''),
    RemovedAt           TIMESTAMP NULL,
    RemovedByName       TEXT      NULL,
    RemovedReason       TEXT      NULL CHECK (RemovedReason IN ('Replaced', 'Unlinked', 'NotInNewImport')),
    CONSTRAINT skillmatrixcellevidence_removed_consistent CHECK ((RemovedAt IS NULL) = (RemovedReason IS NULL))
);

CREATE UNIQUE INDEX UX_SkillMatrixCellEvidence_Current ON public.SkillMatrixCellEvidence (SkillID, ColumnID) WHERE RemovedAt IS NULL;
CREATE INDEX IX_SkillMatrixCellEvidence_Certificate ON public.SkillMatrixCellEvidence (CertificateID);

CREATE FUNCTION public.SkillMatrixCellEvidence_RejectDelete() RETURNS TRIGGER
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    RAISE EXCEPTION 'Evidence links are not deleted: close them instead (RemovedAt, RemovedReason).';
END;
$$;
CREATE TRIGGER TR_SkillMatrixCellEvidence_Delete BEFORE DELETE ON public.SkillMatrixCellEvidence
    FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixCellEvidence_RejectDelete();
CREATE TRIGGER TR_SkillMatrixCellEvidence_Truncate BEFORE TRUNCATE ON public.SkillMatrixCellEvidence
    FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrixCellEvidence_RejectDelete();

COMMENT ON TABLE public.SkillMatrixCellEvidence IS 'Skills Matrix cell evidence (migration 009)';
COMMENT ON FUNCTION public.SkillMatrixCellEvidence_RejectDelete() IS 'Skills Matrix cell evidence (migration 009)';
COMMENT ON INDEX public.UX_SkillMatrixCellEvidence_Current IS 'Skills Matrix cell evidence (migration 009)';
COMMENT ON INDEX public.IX_SkillMatrixCellEvidence_Certificate IS 'Skills Matrix cell evidence (migration 009)';

COMMIT;
