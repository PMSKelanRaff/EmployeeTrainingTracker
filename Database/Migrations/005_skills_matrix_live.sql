-- 005_skills_matrix_live.sql
-- Skills Matrix: an EDITABLE live copy of the current approved import, plus an append-only change log.
-- Requires migration 004. The imported snapshots (migration 004 tables) stay read-only and unchanged:
-- they are the history; the live copy is what the application shows and edits.
--
-- ADDITIVE ONLY. Creates exactly these new objects and nothing else:
--   Tables (7):     SkillMatrixLiveState, SkillMatrixLiveAreas, SkillMatrixLiveCategories, SkillMatrixLiveSkills,
--                   SkillMatrixLiveColumns, SkillMatrixLiveCells, SkillMatrixLiveChanges
--   Functions (5):  SkillMatrixLive_Seed, SkillMatrixLive_GuardRow, SkillMatrixLive_GuardCell,
--                   SkillMatrixLive_GuardDelete, SkillMatrixLive_RejectChange
--   Triggers (14):  on the 7 new tables only (section 9)
--   Indexes:        primary keys / unique constraints of the new tables, plus 5 named indexes
-- No existing table, column, constraint, index, role, grant, permission or setting is altered, and no
-- migration 004 object is changed (004's tables are only read, by the seed function).
--
-- ONE side effect on an existing table (same kind as migration 004's): the foreign key
-- SkillMatrixLiveColumns.EmployeeID -> Employees(EmployeeID) adds two INTERNAL system triggers to
-- Employees and takes a SHARE ROW EXCLUSIVE lock on Employees until COMMIT (blocks writes, not reads).
-- Effect: an employee on the live matrix cannot be hard-deleted (ON DELETE RESTRICT) - already true for
-- every employee in an imported snapshot. Neither app hard-deletes employees (they are archived).
--
-- Data model
--   SkillMatrixLiveState       one row: which import the live copy was built from, when and by whom
--   SkillMatrixLiveAreas       one per sheet (from the import; legend/scale and workbook refs via SourceAreaID)
--     SkillMatrixLiveCategories  category headings      - editable; deleted = IsDeleted (kept, restorable)
--     SkillMatrixLiveSkills      skill rows              - editable; deleted = IsDeleted (kept, restorable)
--     SkillMatrixLiveColumns     people on the sheet     - Member columns only; removed = IsDeleted
--     SkillMatrixLiveCells       one per skill x person  - Level 1-5 | Unassessed | Flagged (as imported)
--                                No row = outside the sheet's range (as in the import), not editable.
--   SkillMatrixLiveChanges     append-only log of every change (who, when, what, old -> new). It has no
--                              foreign keys to the live rows and stores names as text, so it survives a
--                              re-seed and stays readable after rows are renamed or deleted.
--   Source*ID columns link each live row to the snapshot row it was copied from (NULL = added in the app).
--
-- Rules (triggers; they prevent ACCIDENTAL change and are not a security control - see 004's notes):
--   * Version: every UPDATE of a live row increases its Version by exactly 1 (set by the trigger), so the
--     application can refuse a save when someone else changed the row first (optimistic concurrency).
--   * A live row never moves to another sheet, and its Source*ID never changes.
--   * Rows are not deleted (use IsDeleted) - except by SkillMatrixLive_Seed, which rebuilds the copy.
--   * Cells: 'Flagged' (imported formula / unusual value) can only come from the seed; a flagged cell can
--     be resolved to a Level / Unassessed, never edited into another flagged value.
--   * The change log is insert-only: never updated, deleted or truncated.
--
-- SkillMatrixLive_Seed(import, by employee, by name) rebuilds the live copy from a Complete import in the
-- caller's transaction: it removes the live rows, copies the import's sheets, categories, skills, Member
-- columns and cells, records the import in SkillMatrixLiveState and logs a 'Reseed' change. Edits made
-- before a re-seed remain in the change log. This migration seeds from the current import, if any.
--
-- Fails rather than overwrites, all-or-nothing (own BEGIN ... COMMIT), SET LOCAL search_path / lock_timeout,
-- and how to run: exactly as migration 004 (psql -X -v ON_ERROR_STOP=1 -f, a new session, no -1).
-- Undo with 005_skills_matrix_live_rollback.sql. Roll back 005 BEFORE 004 (005's tables reference 004's).

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;  -- unqualified names fail instead of resolving elsewhere
SET LOCAL lock_timeout = '5s';                 -- wait at most 5 s for any lock, then fail and roll back

DO $$
BEGIN
    IF to_regclass('public.skillmatriximports') IS NULL THEN
        RAISE EXCEPTION 'Migration 004 (Skills Matrix) must be installed before migration 005.';
    END IF;
END;
$$;


-- 1. Which import the live copy was built from (exactly one row once seeded) -----------------------
CREATE TABLE public.SkillMatrixLiveState (
    Singleton        BOOLEAN   PRIMARY KEY DEFAULT TRUE CHECK (Singleton),
    BasedOnImportID  BIGINT    NOT NULL REFERENCES public.SkillMatrixImports (ImportID) ON DELETE RESTRICT,
    SeededAt         TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SeededBy         TEXT      NOT NULL CHECK (btrim(SeededBy) <> '')
);


-- 2. Sheets ------------------------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveAreas (
    AreaID        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    SourceAreaID  BIGINT NOT NULL UNIQUE REFERENCES public.SkillMatrixAreas (AreaID) ON DELETE RESTRICT,
    Name          TEXT   NOT NULL CHECK (btrim(Name) <> ''),
    DisplayOrder  INT    NOT NULL,
    Version       INT    NOT NULL DEFAULT 1
);


-- 3. Category headings ------------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveCategories (
    CategoryID        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID            BIGINT  NOT NULL REFERENCES public.SkillMatrixLiveAreas (AreaID) ON DELETE RESTRICT,
    Name              TEXT    NOT NULL CHECK (btrim(Name) <> ''),
    DisplayOrder      INT     NOT NULL,
    SourceCategoryID  BIGINT  NULL UNIQUE REFERENCES public.SkillMatrixCategories (CategoryID) ON DELETE RESTRICT,
    IsDeleted         BOOLEAN NOT NULL DEFAULT FALSE,
    Version           INT     NOT NULL DEFAULT 1,
    UNIQUE (CategoryID, AreaID)                         -- target of SkillMatrixLiveSkills' composite FK
);


-- 4. Skill rows --------------------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveSkills (
    SkillID          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID           BIGINT  NOT NULL,
    CategoryID       BIGINT  NOT NULL,
    Name             TEXT    NOT NULL CHECK (btrim(Name) <> ''),
    InabAccredited   BOOLEAN NULL,                      -- NULL = not specified
    InabRaw          TEXT    NULL,                      -- imported INAB text (an unexpected value is shown as written)
    HiddenInSource   BOOLEAN NOT NULL DEFAULT FALSE,
    DisplayOrder     INT     NOT NULL,
    SourceSkillID    BIGINT  NULL UNIQUE REFERENCES public.SkillMatrixSkills (SkillID) ON DELETE RESTRICT,
    IsDeleted        BOOLEAN NOT NULL DEFAULT FALSE,
    Version          INT     NOT NULL DEFAULT 1,
    UNIQUE (SkillID, AreaID),                           -- target of SkillMatrixLiveCells' composite FK
    -- the category must belong to the same sheet
    FOREIGN KEY (CategoryID, AreaID) REFERENCES public.SkillMatrixLiveCategories (CategoryID, AreaID) ON DELETE RESTRICT
);
CREATE INDEX IX_SkillMatrixLiveSkills_Area ON public.SkillMatrixLiveSkills (AreaID);


-- 5. People on each sheet (confirmed employees only) --------------------------------------------------
CREATE TABLE public.SkillMatrixLiveColumns (
    ColumnID        BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    AreaID          BIGINT  NOT NULL REFERENCES public.SkillMatrixLiveAreas (AreaID) ON DELETE RESTRICT,
    EmployeeID      BIGINT  NOT NULL REFERENCES public.Employees (EmployeeID) ON DELETE RESTRICT,
    HeaderText      TEXT    NULL,                       -- name as written in the workbook (imported columns)
    SubGroupLabel   TEXT    NULL,                       -- team / group heading
    DisplayOrder    INT     NOT NULL,
    SourceColumnID  BIGINT  NULL UNIQUE REFERENCES public.SkillMatrixColumns (ColumnID) ON DELETE RESTRICT,
    IsDeleted       BOOLEAN NOT NULL DEFAULT FALSE,
    Version         INT     NOT NULL DEFAULT 1,
    UNIQUE (ColumnID, AreaID)                           -- target of SkillMatrixLiveCells' composite FK
);
CREATE INDEX IX_SkillMatrixLiveColumns_Area     ON public.SkillMatrixLiveColumns (AreaID);
CREATE INDEX IX_SkillMatrixLiveColumns_Employee ON public.SkillMatrixLiveColumns (EmployeeID);


-- 6. Cells: one per (skill, person) inside the sheet's range ------------------------------------------
CREATE TABLE public.SkillMatrixLiveCells (
    SkillID              BIGINT    NOT NULL,
    ColumnID             BIGINT    NOT NULL,
    AreaID               BIGINT    NOT NULL,
    Status               TEXT      NOT NULL CHECK (Status IN ('Level', 'Unassessed', 'Flagged')),
    Level                SMALLINT  NULL,
    RawValue             TEXT      NULL,                -- Flagged only: the imported value / formula
    FlagReason           TEXT      NULL,                -- Flagged only
    SourceCellID         BIGINT    NULL UNIQUE REFERENCES public.SkillMatrixCells (CellID) ON DELETE RESTRICT,
    Version              INT       NOT NULL DEFAULT 1,
    UpdatedAt            TIMESTAMP NULL,                -- NULL = as imported
    UpdatedByEmployeeID  BIGINT    NULL,
    UpdatedByName        TEXT      NULL,
    PRIMARY KEY (SkillID, ColumnID),
    -- Same state rules as the snapshot cells (Level IS NOT NULL explicit: NULL BETWEEN 1 AND 5 is unknown)
    CHECK (
        (Status = 'Level'      AND Level IS NOT NULL AND Level BETWEEN 1 AND 5 AND RawValue IS NULL AND FlagReason IS NULL) OR
        (Status = 'Unassessed' AND Level IS NULL         AND RawValue IS NULL     AND FlagReason IS NULL) OR
        (Status = 'Flagged'    AND Level IS NULL         AND RawValue IS NOT NULL AND FlagReason IS NOT NULL AND btrim(FlagReason) <> '')
    ),
    CHECK ((UpdatedAt IS NULL) = (UpdatedByName IS NULL)),
    -- the skill and the person must both belong to the cell's sheet
    FOREIGN KEY (SkillID, AreaID)  REFERENCES public.SkillMatrixLiveSkills  (SkillID, AreaID)  ON DELETE RESTRICT,
    FOREIGN KEY (ColumnID, AreaID) REFERENCES public.SkillMatrixLiveColumns (ColumnID, AreaID) ON DELETE RESTRICT
);
CREATE INDEX IX_SkillMatrixLiveCells_Column ON public.SkillMatrixLiveCells (ColumnID);


-- 7. Change log (append-only) -------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveChanges (
    ChangeID             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ChangedAt            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ChangedByEmployeeID  BIGINT    NULL,                -- NULL = the migration / a system action
    ChangedByName        TEXT      NOT NULL CHECK (btrim(ChangedByName) <> ''),
    BasedOnImportID      BIGINT    NOT NULL REFERENCES public.SkillMatrixImports (ImportID) ON DELETE RESTRICT,
    Action               TEXT      NOT NULL CHECK (Action IN (
                             'SetCell', 'AddSkill', 'EditSkill', 'DeleteSkill', 'RestoreSkill',
                             'AddCategory', 'EditCategory', 'DeleteCategory', 'RestoreCategory',
                             'AddColumn', 'RemoveColumn', 'RestoreColumn', 'Reseed')),
    -- what was changed, by ID (no foreign keys: the log outlives a re-seed) and by name as it was then
    AreaID               BIGINT    NULL,
    AreaName             TEXT      NULL,
    CategoryID           BIGINT    NULL,
    SkillID              BIGINT    NULL,
    SkillName            TEXT      NULL,
    ColumnID             BIGINT    NULL,
    EmployeeID           BIGINT    NULL,
    EmployeeName         TEXT      NULL,
    OldValue             TEXT      NULL,
    NewValue             TEXT      NULL,
    Note                 TEXT      NULL
);
CREATE INDEX IX_SkillMatrixLiveChanges_Area ON public.SkillMatrixLiveChanges (AreaID, ChangedAt);


-- 8. Functions -------------------------------------------------------------------------------------

-- Change log rows are never updated, deleted or truncated.
CREATE FUNCTION public.SkillMatrixLive_RejectChange() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    RAISE EXCEPTION 'The Skills Matrix change log is append-only (% on %).', TG_OP, TG_TABLE_NAME;
END;
$$;

-- Live rows (areas, categories, skills, columns): Version +1 on every update; the sheet and the source
-- link never change. (Nested IFs: PL/pgSQL does not short-circuit, and the tables have different columns.)
CREATE FUNCTION public.SkillMatrixLive_GuardRow() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    IF TG_TABLE_NAME = 'skillmatrixliveareas' THEN
        IF NEW.SourceAreaID IS DISTINCT FROM OLD.SourceAreaID THEN
            RAISE EXCEPTION 'A live sheet''s source cannot change.';
        END IF;
    ELSE
        IF NEW.AreaID IS DISTINCT FROM OLD.AreaID THEN
            RAISE EXCEPTION '% rows cannot move to another sheet.', TG_TABLE_NAME;
        END IF;
        IF TG_TABLE_NAME = 'skillmatrixlivecategories' THEN
            IF NEW.SourceCategoryID IS DISTINCT FROM OLD.SourceCategoryID THEN
                RAISE EXCEPTION 'A live category''s source cannot change.';
            END IF;
        ELSIF TG_TABLE_NAME = 'skillmatrixliveskills' THEN
            IF NEW.SourceSkillID IS DISTINCT FROM OLD.SourceSkillID THEN
                RAISE EXCEPTION 'A live skill''s source cannot change.';
            END IF;
        ELSIF TG_TABLE_NAME = 'skillmatrixlivecolumns' THEN
            IF NEW.SourceColumnID IS DISTINCT FROM OLD.SourceColumnID OR NEW.EmployeeID IS DISTINCT FROM OLD.EmployeeID THEN
                RAISE EXCEPTION 'A live column''s source and employee cannot change (remove it and add the right person instead).';
            END IF;
        END IF;
    END IF;
    NEW.Version := OLD.Version + 1;
    RETURN NEW;
END;
$$;

-- Live cells: 'Flagged' only from the seed; Version +1 on every update; identity columns fixed.
CREATE FUNCTION public.SkillMatrixLive_GuardCell() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_seeding BOOLEAN := COALESCE(current_setting('skillmatrix.live_seed', true), '') = 'on';
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF NEW.Status = 'Flagged' AND NOT v_seeding THEN
            RAISE EXCEPTION 'Flagged cells come only from an import; enter a level or leave the cell unassessed.';
        END IF;
        IF NEW.SourceCellID IS NOT NULL AND NOT v_seeding THEN
            RAISE EXCEPTION 'Only the seed links a live cell to an imported cell.';
        END IF;
        RETURN NEW;
    END IF;

    -- UPDATE
    IF (NEW.SkillID, NEW.ColumnID, NEW.AreaID) IS DISTINCT FROM (OLD.SkillID, OLD.ColumnID, OLD.AreaID)
       OR NEW.SourceCellID IS DISTINCT FROM OLD.SourceCellID THEN
        RAISE EXCEPTION 'A live cell''s skill, person, sheet and source cannot change.';
    END IF;
    IF NEW.Status = 'Flagged' AND (OLD.Status <> 'Flagged' OR NEW.RawValue IS DISTINCT FROM OLD.RawValue
                                   OR NEW.FlagReason IS DISTINCT FROM OLD.FlagReason) THEN
        RAISE EXCEPTION 'A cell can only be changed to a level or unassessed.';
    END IF;
    NEW.Version := OLD.Version + 1;
    RETURN NEW;
END;
$$;

-- Live rows are not deleted (IsDeleted is used instead) - except while SkillMatrixLive_Seed rebuilds the copy.
CREATE FUNCTION public.SkillMatrixLive_GuardDelete() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    IF COALESCE(current_setting('skillmatrix.live_seed', true), '') <> 'on' THEN
        RAISE EXCEPTION 'Skills Matrix live rows are not deleted (% on %); mark them deleted instead.', TG_OP, TG_TABLE_NAME;
    END IF;
    IF TG_LEVEL = 'ROW' THEN
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$;

-- Rebuilds the live copy from a Complete import, in the caller's transaction. Returns the number of cells copied.
CREATE FUNCTION public.SkillMatrixLive_Seed(p_import_id BIGINT, p_by_employee_id BIGINT, p_by_name TEXT) RETURNS INT
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_state   TEXT;
    v_cells   INT;
    v_prev    BIGINT;
BEGIN
    IF p_by_name IS NULL OR btrim(p_by_name) = '' THEN
        RAISE EXCEPTION 'SkillMatrixLive_Seed needs the name of who is seeding.';
    END IF;
    SELECT LoadState INTO v_state FROM public.SkillMatrixImports WHERE ImportID = p_import_id;
    IF v_state IS DISTINCT FROM 'Complete' THEN
        RAISE EXCEPTION 'Import % is not a Complete import (state: %).', p_import_id, COALESCE(v_state, 'not found');
    END IF;
    SELECT BasedOnImportID INTO v_prev FROM public.SkillMatrixLiveState;

    PERFORM set_config('skillmatrix.live_seed', 'on', true);   -- this transaction only

    DELETE FROM public.SkillMatrixLiveCells;
    DELETE FROM public.SkillMatrixLiveSkills;
    DELETE FROM public.SkillMatrixLiveColumns;
    DELETE FROM public.SkillMatrixLiveCategories;
    DELETE FROM public.SkillMatrixLiveAreas;

    INSERT INTO public.SkillMatrixLiveAreas (SourceAreaID, Name, DisplayOrder)
    SELECT a.AreaID, a.Name, a.DisplayOrder
    FROM public.SkillMatrixAreas a WHERE a.ImportID = p_import_id;

    -- DisplayOrder in steps of 1000, so rows added later can be placed between existing ones
    INSERT INTO public.SkillMatrixLiveCategories (AreaID, Name, DisplayOrder, SourceCategoryID)
    SELECT la.AreaID, c.Name,
           1000 * ROW_NUMBER() OVER (PARTITION BY c.AreaID ORDER BY c.DisplayOrder, c.CategoryID), c.CategoryID
    FROM public.SkillMatrixCategories c
    JOIN public.SkillMatrixLiveAreas la ON la.SourceAreaID = c.AreaID;

    INSERT INTO public.SkillMatrixLiveSkills (AreaID, CategoryID, Name, InabAccredited, InabRaw, HiddenInSource, DisplayOrder, SourceSkillID)
    SELECT la.AreaID, lc.CategoryID, s.Name, s.InabAccredited, s.InabRaw, s.HiddenInSource,
           1000 * ROW_NUMBER() OVER (PARTITION BY s.AreaID ORDER BY s.DisplayOrder, s.SourceRow, s.SkillID), s.SkillID
    FROM public.SkillMatrixSkills s
    JOIN public.SkillMatrixLiveAreas la      ON la.SourceAreaID = s.AreaID
    JOIN public.SkillMatrixLiveCategories lc ON lc.SourceCategoryID = s.CategoryID;

    INSERT INTO public.SkillMatrixLiveColumns (AreaID, EmployeeID, HeaderText, SubGroupLabel, DisplayOrder, SourceColumnID)
    SELECT la.AreaID, c.EmployeeID, c.SourceHeaderText, c.SubGroupLabel,
           1000 * ROW_NUMBER() OVER (PARTITION BY c.AreaID ORDER BY c.DisplayOrder, c.ColumnID), c.ColumnID
    FROM public.SkillMatrixColumns c
    JOIN public.SkillMatrixLiveAreas la ON la.SourceAreaID = c.AreaID
    WHERE c.Disposition = 'Member';

    INSERT INTO public.SkillMatrixLiveCells (SkillID, ColumnID, AreaID, Status, Level, RawValue, FlagReason, SourceCellID)
    SELECT ls.SkillID, lcol.ColumnID, ls.AreaID, c.Status, c.Level, c.RawValue, c.FlagReason, c.CellID
    FROM public.SkillMatrixCells c
    JOIN public.SkillMatrixLiveSkills ls    ON ls.SourceSkillID = c.SkillID
    JOIN public.SkillMatrixLiveColumns lcol ON lcol.SourceColumnID = c.ColumnID;
    GET DIAGNOSTICS v_cells = ROW_COUNT;

    PERFORM set_config('skillmatrix.live_seed', 'off', true);

    INSERT INTO public.SkillMatrixLiveState (Singleton, BasedOnImportID, SeededAt, SeededBy)
    VALUES (TRUE, p_import_id, CURRENT_TIMESTAMP, p_by_name)
    ON CONFLICT (Singleton) DO UPDATE
        SET BasedOnImportID = EXCLUDED.BasedOnImportID, SeededAt = EXCLUDED.SeededAt, SeededBy = EXCLUDED.SeededBy;

    INSERT INTO public.SkillMatrixLiveChanges (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, OldValue, NewValue, Note)
    VALUES (p_by_employee_id, p_by_name, p_import_id, 'Reseed',
            CASE WHEN v_prev IS NULL THEN NULL ELSE 'import ' || v_prev END,
            'import ' || p_import_id,
            'Live copy rebuilt from import ' || p_import_id || ': ' || v_cells || ' cells. Earlier edits remain in this log.');

    RETURN v_cells;
END;
$$;


-- 9. Triggers (new tables only) -------------------------------------------------------------------
CREATE TRIGGER TR_SkillMatrixLiveAreas_Update       BEFORE UPDATE   ON public.SkillMatrixLiveAreas      FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardRow();
CREATE TRIGGER TR_SkillMatrixLiveAreas_Delete       BEFORE DELETE   ON public.SkillMatrixLiveAreas      FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveCategories_Update  BEFORE UPDATE   ON public.SkillMatrixLiveCategories FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardRow();
CREATE TRIGGER TR_SkillMatrixLiveCategories_Delete  BEFORE DELETE   ON public.SkillMatrixLiveCategories FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveSkills_Update      BEFORE UPDATE   ON public.SkillMatrixLiveSkills     FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardRow();
CREATE TRIGGER TR_SkillMatrixLiveSkills_Delete      BEFORE DELETE   ON public.SkillMatrixLiveSkills     FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveColumns_Update     BEFORE UPDATE   ON public.SkillMatrixLiveColumns    FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardRow();
CREATE TRIGGER TR_SkillMatrixLiveColumns_Delete     BEFORE DELETE   ON public.SkillMatrixLiveColumns    FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveCells_Write        BEFORE INSERT OR UPDATE ON public.SkillMatrixLiveCells FOR EACH ROW   EXECUTE FUNCTION public.SkillMatrixLive_GuardCell();
CREATE TRIGGER TR_SkillMatrixLiveCells_Delete       BEFORE DELETE   ON public.SkillMatrixLiveCells      FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveState_Delete       BEFORE DELETE   ON public.SkillMatrixLiveState      FOR EACH ROW       EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();
CREATE TRIGGER TR_SkillMatrixLiveChanges_Change     BEFORE UPDATE OR DELETE ON public.SkillMatrixLiveChanges FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixLive_RejectChange();
CREATE TRIGGER TR_SkillMatrixLiveChanges_Truncate   BEFORE TRUNCATE ON public.SkillMatrixLiveChanges    FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrixLive_RejectChange();
-- TRUNCATE of the live tables is refused too (outside the seed, which uses DELETE)
CREATE TRIGGER TR_SkillMatrixLiveCells_Truncate     BEFORE TRUNCATE ON public.SkillMatrixLiveCells      FOR EACH STATEMENT EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();


-- 10. Ownership marker ------------------------------------------------------------------------------
COMMENT ON TABLE public.SkillMatrixLiveState       IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveAreas       IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveCategories  IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveSkills      IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveColumns     IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveCells       IS 'Skills Matrix live (migration 005)';
COMMENT ON TABLE public.SkillMatrixLiveChanges     IS 'Skills Matrix live (migration 005)';
COMMENT ON FUNCTION public.SkillMatrixLive_RejectChange()               IS 'Skills Matrix live (migration 005)';
COMMENT ON FUNCTION public.SkillMatrixLive_GuardRow()                   IS 'Skills Matrix live (migration 005)';
COMMENT ON FUNCTION public.SkillMatrixLive_GuardCell()                  IS 'Skills Matrix live (migration 005)';
COMMENT ON FUNCTION public.SkillMatrixLive_GuardDelete()                IS 'Skills Matrix live (migration 005)';
COMMENT ON FUNCTION public.SkillMatrixLive_Seed(BIGINT, BIGINT, TEXT)   IS 'Skills Matrix live (migration 005)';


-- 11. Seed from the current import, if there is one ------------------------------------------------
SELECT public.SkillMatrixLive_Seed(i.ImportID, NULL, 'migration 005')
FROM public.SkillMatrixImports i WHERE i.IsCurrent;

COMMIT;
