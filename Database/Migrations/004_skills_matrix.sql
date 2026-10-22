-- 004_skills_matrix.sql
-- Skills Matrix: a READ-ONLY representation of approved data from the audited Skills Matrix
-- workbook. Data is only ever loaded by the separately approved import tool (Phase 2), reading a
-- verified COPY of the workbook. Nothing here writes to, or synchronises with, the workbook.
--
-- ADDITIVE ONLY. Creates exactly these new objects and nothing else:
--   Tables (9):     SkillMatrixImports, SkillMatrixScales, SkillMatrixScaleLevels, SkillMatrixAreas, SkillMatrixCategories,
--                   SkillMatrixSkills, SkillMatrixColumns, SkillMatrixCells, SkillMatrixCellFormats
--   Functions (4):  SkillMatrix_RejectChange, SkillMatrix_RequireLoadingImport,
--                   SkillMatrixImports_GuardUpdate, SkillMatrixImports_CheckInvariants
--   Triggers (28):  on the 9 new tables only (listed in section 10)
--   Indexes:        primary keys / unique constraints of the new tables, plus 5 named indexes
-- No existing table, column, constraint, index, role, grant, permission or setting is altered.
--
-- ONE side effect on an existing table (needs explicit approval): the foreign key
-- SkillMatrixColumns.EmployeeID -> Employees(EmployeeID). PostgreSQL implements a foreign key with
-- two INTERNAL system triggers on the referenced table (Employees), and creating it takes a
-- SHARE ROW EXCLUSIVE lock on Employees, held until this script's COMMIT (blocks writes to Employees,
-- not reads). Effect: deleting an Employees row that a Skills Matrix
-- column references is rejected (ON DELETE RESTRICT). No Employees row, column or user-defined
-- object changes, and neither app hard-deletes employees (they are archived). Dropping
-- SkillMatrixColumns (rollback) removes those internal triggers again.
--
-- Fails rather than overwrites: no IF NOT EXISTS / OR REPLACE is used, so if any object with one of
-- these names already exists the script fails and changes nothing.
--
-- Schema: every object is created in, and every reference points to, the schema 'public' explicitly.
-- The session search_path is set to pg_catalog, pg_temp for this transaction only, so a name that is
-- not schema-qualified cannot resolve to (or be created in) another schema - it fails instead.
-- The trigger functions pin the same search_path and qualify every table they read.
--
-- All-or-nothing: the script contains its own BEGIN ... COMMIT. Any error aborts the transaction;
-- every later statement is refused and the final COMMIT is turned into a ROLLBACK by the server, so
-- nothing is kept, whether or not the client stops at the first error.
--
-- Locking: SET LOCAL lock_timeout = '5s' limits each lock wait to 5 seconds; if a lock is not granted
-- in time the script fails and rolls back, instead of waiting indefinitely. While it waits for the
-- Employees lock, other sessions' writes to Employees queue behind it (reads continue), so the
-- application's employee writes can be delayed by up to about 5 s. Once granted, the lock blocks
-- writes to Employees until COMMIT (the rest of the script takes well under a second when tested).
--
-- How to run (supported, tested method: psql, from a NEW session, as a complete file):
--   psql "host=<host> port=5432 dbname=<db> user=<user> sslmode=require" -X -v ON_ERROR_STOP=1 -f 004_skills_matrix.sql
--   * Do NOT add -1 / --single-transaction: the file manages its own transaction.
--   * With ON_ERROR_STOP=1, psql stops at the first error and exits with code 3; the open transaction
--     is rolled back when the session ends.
--   * Without ON_ERROR_STOP, psql reports the error, keeps going ("current transaction is aborted"),
--     prints ROLLBACK for the final COMMIT, and exits with code 0 - check the output, not the exit code.
--   * Other clients (e.g. DBeaver) are NOT tested. If one is used: auto-commit ON (so this file's own
--     BEGIN/COMMIT controls the transaction), run the whole file as ONE script in ONE fresh session,
--     and the client must send each dollar-quoted ($$ ... $$) function body intact. If any statement
--     fails, stop, run ROLLBACK, and verify with the read-only check in docs/SkillsMatrix.md.
--   * Never run it inside a transaction that is already open: BEGIN would only warn, and COMMIT
--     would also commit whatever that transaction had already done.
-- Undo with 004_skills_matrix_rollback.sql (only drops objects carrying this migration's marker comment).
--
-- ------------------------------------------------------------------------------------------------
-- Data model
--   SkillMatrixImports  one immutable snapshot per approved import
--     SkillMatrixScales         legend(s) per sheet, wording as written
--       SkillMatrixScaleLevels    legend entries (KeyNumber allows two keys on one sheet)
--     SkillMatrixAreas          one per imported (visible) sheet
--       SkillMatrixCategories     category header rows
--         SkillMatrixSkills              skill rows (kept per sheet; duplicates kept, unique by source row)
--       SkillMatrixColumns    every person column of the sheet: 'Member' (confirmed employee match)
--                           or 'Excluded' (unconfirmed / student / archived / unmatched, with reason)
--       SkillMatrixCells    one row per in-range cell of a Member column:
--                             Level      level 1-5; no raw value, no flag reason
--                             Unassessed blank in source; no level, no raw value, no flag reason
--                             Flagged    formula / unusual value; raw value and reason required,
--                                        never converted to a level
--                           No row = the cell is outside that sheet's source range.
--       SkillMatrixCellFormats  the WORKBOOK's formatting of every cell the snapshot shows (levels,
--                           skill names, INAB, names, team headings, category headings, key cells):
--                           the fill / font colour Excel DISPLAYS, and where it comes from -
--                             None              no fill
--                             CellStyle         the cell's own fixed fill (pattern details kept; a patterned
--                                               fill's FillRgb is its displayed blend)
--                             ConditionalFormat a conditional-formatting rule (the winning rule is recorded,
--                                               because the workbook's rules overlap and contradict)
--                           Only workbook formatting is stored. Application markers (grey out-of-range,
--                           the ⚠ / — symbols, any app-generated proficiency colouring) are never stored.
--
-- Isolation between snapshots/sheets (composite foreign keys):
--   area.scale must be from the area's import; skill.category must be from the skill's area;
--   cell.skill and cell.column must both be from the cell's area; cell.column must be a Member.
--   SkillMatrixScaleLevels hang off exactly one scale and are referenced by nothing, so they cannot be
--   associated with another import; they need no ImportID of their own.
--
-- Snapshot lifecycle (enforced by triggers):
--   * An import is inserted as 'Loading'; child rows can only be inserted while their import is
--     'Loading'; it becomes 'Complete' (one way) before it can be current.
--   * At COMMIT (deferred check): no import may still be 'Loading', and if any import exists then
--     EXACTLY ONE is current. Zero imports = the Admin tab shows its empty state.
--   * Snapshot rows can never be updated, deleted or truncated; on an import only LoadState
--     (Loading -> Complete) and IsCurrent may change.
--   Replacement = one transaction: insert new import (Loading) and its rows -> mark Complete ->
--   clear old IsCurrent -> set new IsCurrent -> COMMIT. Any failure rolls the whole transaction
--   back, leaving the previous current snapshot untouched. Readers see the old or the new snapshot.
--
-- Limits: these triggers prevent ACCIDENTAL change. They are not a security control. The table
-- owner / a superuser (here 'postgres', member of rds_superuser) can disable or drop triggers,
-- defer or bypass them (e.g. ALTER TABLE ... DISABLE TRIGGER, session_replication_role = replica),
-- or drop the tables.
-- ------------------------------------------------------------------------------------------------

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;  -- unqualified names fail instead of resolving elsewhere
SET LOCAL lock_timeout = '5s';                 -- wait at most 5 s for any lock, then fail and roll back


-- 1. Import snapshots ----------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixImports (
    ImportID        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    SourceFileName  TEXT      NOT NULL CHECK (btrim(SourceFileName) <> ''),
    SourceSha256    CHAR(64)  NOT NULL CHECK (SourceSha256 ~ '^[0-9A-Fa-f]{64}$'),
    SourceVersion   TEXT      NULL,                     -- e.g. '2026 v1'
    ApprovedBy      TEXT      NOT NULL CHECK (btrim(ApprovedBy) <> ''),
    ImportedBy      TEXT      NOT NULL CHECK (btrim(ImportedBy) <> ''),
    ImportedAt      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LoadState       TEXT      NOT NULL DEFAULT 'Loading' CHECK (LoadState IN ('Loading', 'Complete')),
    IsCurrent       BOOLEAN   NOT NULL DEFAULT FALSE,
    Notes           TEXT      NULL,
    ColourMethod    TEXT      NULL,                     -- how displayed colours were worked out (tool + version)
    ColourVerification TEXT   NULL,                     -- how they were checked, e.g. against Excel's DisplayFormat
    CHECK (NOT IsCurrent OR LoadState = 'Complete')     -- only a completed import can be current
);

-- At most one current snapshot at any moment
CREATE UNIQUE INDEX UX_SkillMatrixImports_Current ON public.SkillMatrixImports (IsCurrent) WHERE IsCurrent;


-- 2. Proficiency scales (one per sheet) ------------------------------------------------------------
CREATE TABLE public.SkillMatrixScales (
    ScaleID       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ImportID      BIGINT NOT NULL REFERENCES public.SkillMatrixImports (ImportID) ON DELETE RESTRICT,
    Name          TEXT   NOT NULL,                      -- e.g. 'Laboratory key'
    LegendStatus  TEXT   NOT NULL CHECK (LegendStatus IN ('AsWritten', 'NeedsConfirmation')),
    LegendNote    TEXT   NULL,                          -- why confirmation is needed
    SourceRef     TEXT   NOT NULL,                      -- e.g. 'Laboratory!A1:K1'
    UNIQUE (ScaleID, ImportID),                         -- target of SkillMatrixAreas' composite FK
    CHECK (LegendStatus = 'AsWritten' OR (LegendNote IS NOT NULL AND btrim(LegendNote) <> ''))
);


-- 3. Legend entries, wording exactly as written ---------------------------------------------------
--    KeyNumber allows more than one key on a sheet (H&S Environmental has two). Only levels actually
--    written in a key are stored (e.g. Admin's key lists 1, 4 and 5 only).
CREATE TABLE public.SkillMatrixScaleLevels (
    ScaleID      BIGINT   NOT NULL REFERENCES public.SkillMatrixScales (ScaleID) ON DELETE RESTRICT,
    KeyNumber    SMALLINT NOT NULL DEFAULT 1 CHECK (KeyNumber >= 1),
    Level        SMALLINT NOT NULL CHECK (Level BETWEEN 1 AND 5),
    Label        TEXT     NOT NULL,                     -- e.g. '3: Requires Supervision'
    Description  TEXT     NULL,                         -- e.g. Software and Data Analytics descriptions
    SourceRef    TEXT     NOT NULL,                     -- the exact key cell, e.g. 'Laboratory!C1' (its formatting
                                                        -- is in SkillMatrixCellFormats)
    PRIMARY KEY (ScaleID, KeyNumber, Level)
);


-- 4. Areas: one per imported (visible) sheet -------------------------------------------------------
--    Department (Groups) mappings are deliberately not stored yet (see docs/SkillsMatrix.md).
CREATE TABLE public.SkillMatrixAreas (
    AreaID        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ImportID      BIGINT NOT NULL REFERENCES public.SkillMatrixImports (ImportID) ON DELETE RESTRICT,
    ScaleID       BIGINT NOT NULL,
    Name          TEXT   NOT NULL,
    SourceSheet   TEXT   NOT NULL,
    DisplayOrder  INT    NOT NULL,
    SkillHeaderRef TEXT  NULL,                          -- e.g. 'Laboratory!A3' ('Skills Record' heading cell)
    InabHeaderRef  TEXT  NULL,                          -- e.g. 'Laboratory!B3'; NULL when the sheet has no INAB column
    UNIQUE (ImportID, SourceSheet),
    -- the area's scale must belong to the same import
    FOREIGN KEY (ScaleID, ImportID) REFERENCES public.SkillMatrixScales (ScaleID, ImportID) ON DELETE RESTRICT
);
CREATE INDEX IX_SkillMatrixAreas_Import ON public.SkillMatrixAreas (ImportID);


-- 5. Category header rows (e.g. 'Laboratory Tests to EN'; the Software sheet's 'Area' column) ----
CREATE TABLE public.SkillMatrixCategories (
    CategoryID    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID        BIGINT NOT NULL REFERENCES public.SkillMatrixAreas (AreaID) ON DELETE RESTRICT,
    Name          TEXT   NOT NULL,
    DisplayOrder  INT    NOT NULL,
    SourceRef     TEXT   NOT NULL,
    UNIQUE (CategoryID, AreaID)                         -- target of SkillMatrixSkills' composite FK
);


-- 6. Skill rows ------------------------------------------------------------------------------------
--    Kept separate per sheet. Duplicate names are kept (unique by source row, not by name).
CREATE TABLE public.SkillMatrixSkills (
    SkillID         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID          BIGINT  NOT NULL,
    CategoryID      BIGINT  NOT NULL,
    Name            TEXT    NOT NULL,                   -- as written
    InabAccredited  BOOLEAN NULL,                       -- NULL = not specified on the sheet
    InabRaw         TEXT    NULL,                       -- original 'INAB accredited' cell text
    InabSourceRef   TEXT    NULL,                       -- the INAB cell, e.g. 'Laboratory!B5' (NULL if the sheet has none)
    HiddenInSource  BOOLEAN NOT NULL DEFAULT FALSE,     -- row is hidden in the workbook
    DisplayOrder    INT     NOT NULL,
    SourceRow       INT     NOT NULL CHECK (SourceRow >= 1),
    SourceRef       TEXT    NOT NULL,                   -- e.g. 'Laboratory!A5'
    UNIQUE (AreaID, SourceRow),
    UNIQUE (SkillID, AreaID),                           -- target of SkillMatrixCells' composite FK
    CHECK (InabAccredited IS NULL OR InabRaw IS NOT NULL),
    CHECK (InabRaw IS NULL OR InabSourceRef IS NOT NULL),
    -- the category must belong to the same sheet
    FOREIGN KEY (CategoryID, AreaID) REFERENCES public.SkillMatrixCategories (CategoryID, AreaID) ON DELETE RESTRICT
);


-- 7. Person columns of each sheet ------------------------------------------------------------------
--    'Member'   = manually confirmed employee match (EmployeeID required, no exclusion reason)
--    'Excluded' = not imported (no EmployeeID; reason required) - e.g. unconfirmed match, student,
--                 archived employee, unmatched name. Kept for transparency; never holds cells.
CREATE TABLE public.SkillMatrixColumns (
    ColumnID          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID            BIGINT NOT NULL REFERENCES public.SkillMatrixAreas (AreaID) ON DELETE RESTRICT,
    SourceColumn      TEXT   NOT NULL CHECK (SourceColumn ~ '^[A-Z]{1,3}$'),
    SourceHeaderText  TEXT   NOT NULL,                  -- name exactly as written, e.g. 'James O Grady'
    HeaderSourceRef   TEXT   NOT NULL,                  -- the name cell, e.g. 'Laboratory!F3'
    SubGroupLabel     TEXT   NULL,                      -- e.g. 'Support Lab Staff'
    SubGroupSourceRef TEXT   NULL,                      -- the team-heading cell (top-left of its merge), e.g. 'Laboratory!H2'
    Disposition       TEXT   NOT NULL CHECK (Disposition IN ('Member', 'Excluded')),
    EmployeeID        BIGINT NULL REFERENCES public.Employees (EmployeeID) ON DELETE RESTRICT,
    ExcludeReason     TEXT   NULL,
    DisplayOrder      INT    NOT NULL,
    UNIQUE (AreaID, SourceColumn),
    UNIQUE (ColumnID, AreaID, Disposition),             -- target of SkillMatrixCells' composite FK
    CHECK (
        (Disposition = 'Member'   AND EmployeeID IS NOT NULL AND ExcludeReason IS NULL) OR
        (Disposition = 'Excluded' AND EmployeeID IS NULL     AND ExcludeReason IS NOT NULL AND btrim(ExcludeReason) <> '')
    )
);
CREATE INDEX IX_SkillMatrixColumns_Employee ON public.SkillMatrixColumns (EmployeeID);


-- 8. Cells: one row per in-range cell of a Member column -------------------------------------------
CREATE TABLE public.SkillMatrixCells (
    CellID             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,  -- future evidence links attach here
    AreaID             BIGINT   NOT NULL,
    SkillID            BIGINT   NOT NULL,
    ColumnID           BIGINT   NOT NULL,
    ColumnDisposition  TEXT     NOT NULL DEFAULT 'Member' CHECK (ColumnDisposition = 'Member'),
    Status             TEXT     NOT NULL CHECK (Status IN ('Level', 'Unassessed', 'Flagged')),
    Level              SMALLINT NULL,
    RawValue           TEXT     NULL,                   -- original value / formula, Flagged cells only
    FlagReason         TEXT     NULL,                   -- Flagged cells only
    SourceCell         TEXT     NOT NULL,               -- e.g. 'Admin!H6'
    UNIQUE (SkillID, ColumnID),
    -- Exactly these field combinations per state. (Level IS NOT NULL is explicit: a CHECK that
    -- evaluates to unknown passes, and 'NULL BETWEEN 1 AND 5' is unknown, not false.)
    CHECK (
        (Status = 'Level'      AND Level IS NOT NULL AND Level BETWEEN 1 AND 5 AND RawValue IS NULL AND FlagReason IS NULL) OR
        (Status = 'Unassessed' AND Level IS NULL         AND RawValue IS NULL     AND FlagReason IS NULL) OR
        (Status = 'Flagged'    AND Level IS NULL         AND RawValue IS NOT NULL AND FlagReason IS NOT NULL AND btrim(FlagReason) <> '')
    ),
    -- the skill and the column must both belong to the cell's sheet, and the column must be a Member
    FOREIGN KEY (SkillID, AreaID) REFERENCES public.SkillMatrixSkills (SkillID, AreaID) ON DELETE RESTRICT,
    FOREIGN KEY (ColumnID, AreaID, ColumnDisposition)
        REFERENCES public.SkillMatrixColumns (ColumnID, AreaID, Disposition) ON DELETE RESTRICT
);
CREATE INDEX IX_SkillMatrixCells_Column ON public.SkillMatrixCells (ColumnID);
CREATE INDEX IX_SkillMatrixCells_Area   ON public.SkillMatrixCells (AreaID);


-- 8b. Workbook formatting of the cells the snapshot shows ---------------------------------------------
--    One row per workbook cell referenced by the snapshot (SourceCell / SourceRef / InabSourceRef /
--    HeaderSourceRef / SubGroupSourceRef / key-cell SourceRef / area heading refs).
--    Records what Excel DISPLAYS and where it comes from; never application-generated colouring.
CREATE TABLE public.SkillMatrixCellFormats (
    AreaID           BIGINT  NOT NULL REFERENCES public.SkillMatrixAreas (AreaID) ON DELETE RESTRICT,
    SourceRef        TEXT    NOT NULL CHECK (SourceRef ~ '^.+![A-Z]{1,3}[1-9][0-9]*$'),   -- e.g. 'Laboratory!C5'
    FillSource       TEXT    NOT NULL CHECK (FillSource IN ('None', 'CellStyle', 'ConditionalFormat')),
    FillRgb          CHAR(6) NULL CHECK (FillRgb ~ '^[0-9A-F]{6}$'),          -- colour as displayed
    Pattern          TEXT    NULL CHECK (Pattern IN ('solid', 'darkGray', 'mediumGray', 'lightGray', 'gray125', 'gray0625',
                                                     'darkHorizontal', 'darkVertical', 'darkDown', 'darkUp', 'darkGrid', 'darkTrellis',
                                                     'lightHorizontal', 'lightVertical', 'lightDown', 'lightUp', 'lightGrid', 'lightTrellis')),
    PatternColorRgb  CHAR(6) NULL CHECK (PatternColorRgb ~ '^[0-9A-F]{6}$'),  -- patterned fills: dot/line colour
    PatternBackRgb   CHAR(6) NULL CHECK (PatternBackRgb ~ '^[0-9A-F]{6}$'),   -- patterned fills: background
    ConditionalRule  TEXT    NULL,       -- winning rule, e.g. 'priority 1: containsText "4" on E5:E18 -> CC99FF'
    FontSource       TEXT    NOT NULL CHECK (FontSource IN ('CellStyle', 'ConditionalFormat')),
    FontRgb          CHAR(6) NOT NULL CHECK (FontRgb ~ '^[0-9A-F]{6}$'),
    FontRule         TEXT    NULL,       -- rule that set the font colour, when FontSource = 'ConditionalFormat'
    Bold             BOOLEAN NOT NULL,
    PRIMARY KEY (AreaID, SourceRef),
    -- Exactly these fill combinations (every comparison on a nullable column is NULL-guarded):
    CHECK (
        (FillSource = 'None'
            AND FillRgb IS NULL AND Pattern IS NULL AND PatternColorRgb IS NULL AND PatternBackRgb IS NULL
            AND ConditionalRule IS NULL) OR
        (FillSource = 'ConditionalFormat'
            AND FillRgb IS NOT NULL AND Pattern IS NOT NULL AND Pattern = 'solid'
            AND PatternColorRgb IS NULL AND PatternBackRgb IS NULL
            AND ConditionalRule IS NOT NULL AND btrim(ConditionalRule) <> '') OR
        (FillSource = 'CellStyle'
            AND FillRgb IS NOT NULL AND Pattern IS NOT NULL AND Pattern = 'solid'
            AND PatternColorRgb IS NULL AND PatternBackRgb IS NULL AND ConditionalRule IS NULL) OR
        (FillSource = 'CellStyle'
            AND FillRgb IS NOT NULL AND Pattern IS NOT NULL AND Pattern <> 'solid'
            AND PatternColorRgb IS NOT NULL AND PatternBackRgb IS NOT NULL AND ConditionalRule IS NULL)
    ),
    CHECK ((FontSource = 'ConditionalFormat') = (FontRule IS NOT NULL AND btrim(FontRule) <> ''))
);


-- 9. Trigger functions --------------------------------------------------------------------------------

-- Snapshot rows are never updated, deleted or truncated.
CREATE FUNCTION public.SkillMatrix_RejectChange() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp   -- fixed: independent of the caller's search_path
AS $$
BEGIN
    RAISE EXCEPTION 'Skills Matrix snapshot data is read-only (% on %). Load a new approved import instead.',
        TG_OP, TG_TABLE_NAME;
END;
$$;

-- Child rows may only be added to an import that is still 'Loading' (i.e. being loaded now).
CREATE FUNCTION public.SkillMatrix_RequireLoadingImport() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp   -- fixed: independent of the caller's search_path
AS $$
DECLARE
    v_import_id BIGINT;
    v_state     TEXT;
BEGIN
    IF TG_TABLE_NAME IN ('skillmatrixscales', 'skillmatrixareas') THEN
        v_import_id := NEW.importid;
    ELSIF TG_TABLE_NAME = 'skillmatrixscalelevels' THEN
        SELECT s.ImportID INTO v_import_id FROM public.SkillMatrixScales s WHERE s.ScaleID = NEW.scaleid;
    ELSE  -- skillmatrixcategories, skillmatrixskills, skillmatrixcolumns, skillmatrixcells, skillmatrixcellformats
        SELECT a.ImportID INTO v_import_id FROM public.SkillMatrixAreas a WHERE a.AreaID = NEW.areaid;
    END IF;

    SELECT i.LoadState INTO v_state FROM public.SkillMatrixImports i WHERE i.ImportID = v_import_id;

    IF v_state IS DISTINCT FROM 'Loading' THEN
        RAISE EXCEPTION 'Cannot add % rows to Skills Matrix import % (state: %). Completed snapshots are read-only.',
            TG_TABLE_NAME, v_import_id, COALESCE(v_state, 'not found');
    END IF;

    -- A format row must describe a cell on its own area's sheet ('Laboratory!C5' only for Laboratory).
    -- Nested IF on purpose: PL/pgSQL does not short-circuit AND, so NEW.sourceref must not appear in an
    -- expression that is evaluated for tables without that column.
    IF TG_TABLE_NAME = 'skillmatrixcellformats' THEN
        IF NOT EXISTS (
            SELECT 1 FROM public.SkillMatrixAreas a
            WHERE a.AreaID = NEW.areaid AND left(NEW.sourceref, length(a.SourceSheet) + 1) = a.SourceSheet || '!') THEN
            RAISE EXCEPTION 'Format row % does not belong to the sheet of area %.', NEW.sourceref, NEW.areaid;
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

-- On an import only LoadState (Loading -> Complete, one way) and IsCurrent may change.
CREATE FUNCTION public.SkillMatrixImports_GuardUpdate() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp   -- fixed: independent of the caller's search_path
AS $$
BEGIN
    IF (NEW.ImportID, NEW.SourceFileName, NEW.SourceSha256, NEW.SourceVersion, NEW.ApprovedBy,
        NEW.ImportedBy, NEW.ImportedAt, NEW.Notes, NEW.ColourMethod, NEW.ColourVerification)
       IS DISTINCT FROM
       (OLD.ImportID, OLD.SourceFileName, OLD.SourceSha256, OLD.SourceVersion, OLD.ApprovedBy,
        OLD.ImportedBy, OLD.ImportedAt, OLD.Notes, OLD.ColourMethod, OLD.ColourVerification) THEN
        RAISE EXCEPTION 'Skills Matrix import % is read-only; only LoadState and IsCurrent may change.', OLD.ImportID;
    END IF;
    IF OLD.LoadState = 'Complete' AND NEW.LoadState <> 'Complete' THEN
        RAISE EXCEPTION 'Skills Matrix import % is Complete and cannot return to %.', OLD.ImportID, NEW.LoadState;
    END IF;
    RETURN NEW;
END;
$$;

-- Checked at COMMIT: no half-loaded import, and exactly one current import whenever any exist.
CREATE FUNCTION public.SkillMatrixImports_CheckInvariants() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp   -- fixed: independent of the caller's search_path
AS $$
DECLARE
    v_total   BIGINT;
    v_current BIGINT;
BEGIN
    IF EXISTS (SELECT 1 FROM public.SkillMatrixImports WHERE LoadState = 'Loading') THEN
        RAISE EXCEPTION 'A Skills Matrix import is still Loading at commit; a load must complete within its transaction.';
    END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE IsCurrent) INTO v_total, v_current FROM public.SkillMatrixImports;
    IF v_total > 0 AND v_current <> 1 THEN
        RAISE EXCEPTION 'Skills Matrix must have exactly one current import when any import exists (found % current of %).',
            v_current, v_total;
    END IF;
    RETURN NULL;
END;
$$;


-- 10. Triggers (new tables only) -------------------------------------------------------------------

-- SkillMatrixImports
CREATE TRIGGER TR_SkillMatrixImports_Update   BEFORE UPDATE   ON public.SkillMatrixImports FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixImports_GuardUpdate();
CREATE TRIGGER TR_SkillMatrixImports_Delete   BEFORE DELETE   ON public.SkillMatrixImports FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixImports_Truncate BEFORE TRUNCATE ON public.SkillMatrixImports FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE CONSTRAINT TRIGGER TR_SkillMatrixImports_Invariants
    AFTER INSERT OR UPDATE ON public.SkillMatrixImports
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixImports_CheckInvariants();

-- Child tables: insert only while the import is Loading; never update, delete or truncate
CREATE TRIGGER TR_SkillMatrixScales_Insert       BEFORE INSERT           ON public.SkillMatrixScales      FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixScales_Change       BEFORE UPDATE OR DELETE ON public.SkillMatrixScales      FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixScales_Truncate     BEFORE TRUNCATE         ON public.SkillMatrixScales      FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixScaleLevels_Insert   BEFORE INSERT           ON public.SkillMatrixScaleLevels FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixScaleLevels_Change   BEFORE UPDATE OR DELETE ON public.SkillMatrixScaleLevels FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixScaleLevels_Truncate BEFORE TRUNCATE         ON public.SkillMatrixScaleLevels FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixAreas_Insert         BEFORE INSERT           ON public.SkillMatrixAreas       FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixAreas_Change         BEFORE UPDATE OR DELETE ON public.SkillMatrixAreas       FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixAreas_Truncate       BEFORE TRUNCATE         ON public.SkillMatrixAreas       FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixCategories_Insert    BEFORE INSERT           ON public.SkillMatrixCategories  FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixCategories_Change    BEFORE UPDATE OR DELETE ON public.SkillMatrixCategories  FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixCategories_Truncate  BEFORE TRUNCATE         ON public.SkillMatrixCategories  FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixSkills_Insert             BEFORE INSERT           ON public.SkillMatrixSkills           FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixSkills_Change             BEFORE UPDATE OR DELETE ON public.SkillMatrixSkills           FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixSkills_Truncate           BEFORE TRUNCATE         ON public.SkillMatrixSkills           FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixColumns_Insert   BEFORE INSERT           ON public.SkillMatrixColumns FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixColumns_Change   BEFORE UPDATE OR DELETE ON public.SkillMatrixColumns FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixColumns_Truncate BEFORE TRUNCATE         ON public.SkillMatrixColumns FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixCells_Insert   BEFORE INSERT           ON public.SkillMatrixCells FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixCells_Change   BEFORE UPDATE OR DELETE ON public.SkillMatrixCells FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixCells_Truncate BEFORE TRUNCATE         ON public.SkillMatrixCells FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();

CREATE TRIGGER TR_SkillMatrixCellFormats_Insert   BEFORE INSERT           ON public.SkillMatrixCellFormats FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RequireLoadingImport();
CREATE TRIGGER TR_SkillMatrixCellFormats_Change   BEFORE UPDATE OR DELETE ON public.SkillMatrixCellFormats FOR EACH ROW EXECUTE FUNCTION public.SkillMatrix_RejectChange();
CREATE TRIGGER TR_SkillMatrixCellFormats_Truncate BEFORE TRUNCATE         ON public.SkillMatrixCellFormats FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrix_RejectChange();


-- 11. Ownership marker ---------------------------------------------------------------------------
-- Identifies every object created by this migration. 004_skills_matrix_rollback.sql refuses to drop
-- anything that does not carry exactly this comment, so it can never remove an unrelated object.
COMMENT ON TABLE public.SkillMatrixImports      IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixScales       IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixScaleLevels  IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixAreas        IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixCategories   IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixSkills       IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixColumns      IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixCells        IS 'Skills Matrix (migration 004)';
COMMENT ON TABLE public.SkillMatrixCellFormats  IS 'Skills Matrix (migration 004)';
COMMENT ON FUNCTION public.SkillMatrix_RejectChange()           IS 'Skills Matrix (migration 004)';
COMMENT ON FUNCTION public.SkillMatrix_RequireLoadingImport()   IS 'Skills Matrix (migration 004)';
COMMENT ON FUNCTION public.SkillMatrixImports_GuardUpdate()     IS 'Skills Matrix (migration 004)';
COMMENT ON FUNCTION public.SkillMatrixImports_CheckInvariants() IS 'Skills Matrix (migration 004)';

COMMIT;
