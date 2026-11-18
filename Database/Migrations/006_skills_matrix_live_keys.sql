-- 006_skills_matrix_live_keys.sql
-- Skills Matrix live copy: one uniform key (level wording + colours) per sheet, and short team names.
-- Requires migration 005. Additive, plus one function body replaced (SkillMatrixLive_Seed, see below).
--
-- Decisions (2026-11-11):
--   * Every sheet of the live copy has exactly one wording per level. Where a sheet's key words a level more
--     than once, the wording used by the most sheets is kept (H&S Environmental: '4: Skilled in Area', not
--     '4: Trained'). Where a sheet's key leaves a level out, the most common wording (and its key colour) is
--     filled in (Admin: '2: In Training', '3: Requires Supervision'). The imported snapshots keep the key exactly
--     as written; this only changes what the live copy shows.
--   * The team/group row shows the manager's name taken from the workbook heading ('Dermot Leonard Lab Manager'
--     -> 'Dermot Leonard'). Headings without a manager ('Support Lab Staff') are shown as written.
--
-- Creates exactly:
--   Tables (2):     SkillMatrixLiveLevels       one row per (live sheet, level 1-5): wording, description, key colours,
--                                               and Origin = AsWritten | MostCommonWording | FilledIn
--                   SkillMatrixLiveTeamLabels   heading as written -> name shown (applied when displayed, so it also
--                                               applies after a re-seed while the heading is unchanged)
--   Functions (1):  SkillMatrixLive_SeedLevels  rebuilds SkillMatrixLiveLevels from the live copy's import
--   Triggers (2):   on SkillMatrixLiveLevels (Version +1 on update; no deletes outside a seed) - they use
--                   migration 005's guard functions, so 005 cannot be rolled back while 006 is installed.
-- Replaces the body of SkillMatrixLive_Seed (migration 005) so that a re-seed also rebuilds the levels; the
-- only difference is the added 'PERFORM public.SkillMatrixLive_SeedLevels();'. The rollback restores 005's body.
-- No existing table is altered; the live cells, skills, people and change log are not touched.
-- How to run, all-or-nothing and locking: as migrations 004/005 (psql -X -v ON_ERROR_STOP=1 -f, new session,
-- no -1). It takes no lock on Employees. Undo with 006_skills_matrix_live_keys_rollback.sql.

BEGIN;
SET LOCAL search_path = pg_catalog, pg_temp;
SET LOCAL lock_timeout = '5s';

DO $$
BEGIN
    IF to_regclass('public.skillmatrixlivestate') IS NULL THEN
        RAISE EXCEPTION 'Migration 005 (Skills Matrix live copy) must be installed before migration 006.';
    END IF;
END;
$$;


-- 1. One key per live sheet ------------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveLevels (
    AreaID       BIGINT   NOT NULL REFERENCES public.SkillMatrixLiveAreas (AreaID) ON DELETE RESTRICT,
    Level        SMALLINT NOT NULL CHECK (Level BETWEEN 1 AND 5),
    Label        TEXT     NOT NULL CHECK (btrim(Label) <> ''),
    Description  TEXT     NULL,
    FillRgb      CHAR(6)  NULL CHECK (FillRgb ~ '^[0-9A-F]{6}$'),     -- the key cell's displayed fill (NULL = none)
    FontRgb      CHAR(6)  NULL CHECK (FontRgb ~ '^[0-9A-F]{6}$'),
    Origin       TEXT     NOT NULL CHECK (Origin IN ('AsWritten', 'MostCommonWording', 'FilledIn')),
    SourceRef    TEXT     NULL,                                        -- the key cell the wording/colour came from
    Version      INT      NOT NULL DEFAULT 1,
    PRIMARY KEY (AreaID, Level)
);


-- 2. Team headings -> name shown ----------------------------------------------------------------------
CREATE TABLE public.SkillMatrixLiveTeamLabels (
    HeadingText   TEXT PRIMARY KEY CHECK (btrim(HeadingText) <> ''),
    DisplayLabel  TEXT NOT NULL CHECK (btrim(DisplayLabel) <> '')
);

INSERT INTO public.SkillMatrixLiveTeamLabels (HeadingText, DisplayLabel) VALUES
    ('Dermot Leonard Lab Manager',                           'Dermot Leonard'),
    ('Pat Burns Data Collection Manager',                    'Pat Burns'),
    ('Darragh O''Dea Non-Structural Team Manager',           'Darragh O''Dea'),
    ('Eric Donohue Network Team Manager',                    'Eric Donohue'),
    ('Monica Loughnane Structural Team Manager',             'Monica Loughnane'),
    ('GIS Team Rory Donnellan Manager',                      'Rory Donnellan'),
    ('Software and Data Analytics Team Manager David Power', 'David Power'),
    ('Equipment Innovation Team Joe Mchale Manager',         'Joe McHale'),
    ('Innovation Team Kieran Feighan Manager',               'Kieran Feighan');


-- 3. Rebuild the levels of every live sheet from the import the live copy was built from -------------
CREATE FUNCTION public.SkillMatrixLive_SeedLevels() RETURNS INT
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_rows INT;
BEGIN
    PERFORM set_config('skillmatrix.live_seed', 'on', true);
    DELETE FROM public.SkillMatrixLiveLevels;
    PERFORM set_config('skillmatrix.live_seed', 'off', true);

    WITH entries AS (       -- every key entry of every live sheet, with its key cell's displayed colours
        SELECT la.AreaID AS live_area, la.DisplayOrder AS area_order, l.KeyNumber, l.Level, l.Label, l.Description, l.SourceRef,
               CASE WHEN f.FillSource = 'None' THEN NULL ELSE f.FillRgb END AS fill, f.FontRgb AS font
        FROM public.SkillMatrixLiveAreas la
        JOIN public.SkillMatrixAreas a        ON a.AreaID = la.SourceAreaID
        JOIN public.SkillMatrixScaleLevels l  ON l.ScaleID = a.ScaleID
        LEFT JOIN public.SkillMatrixCellFormats f ON f.AreaID = a.AreaID AND f.SourceRef = l.SourceRef
    ),
    popularity AS (         -- how many sheets use each wording of each level (case-insensitive)
        SELECT Level, lower(btrim(Label)) AS wording, COUNT(DISTINCT live_area) AS sheets
        FROM entries GROUP BY Level, lower(btrim(Label))
    ),
    ranked AS (
        SELECT e.*, p.sheets,
               COUNT(*) OVER (PARTITION BY e.live_area, e.Level) AS wordings_on_sheet
        FROM entries e JOIN popularity p ON p.Level = e.Level AND p.wording = lower(btrim(e.Label))
    ),
    own AS (                -- the sheet's own wording; if it has several, the most common one (then the first key)
        SELECT DISTINCT ON (live_area, Level) *
        FROM ranked ORDER BY live_area, Level, sheets DESC, KeyNumber
    ),
    common AS (             -- the most common wording of each level, from the first sheet that uses it
        SELECT DISTINCT ON (Level) *
        FROM ranked ORDER BY Level, sheets DESC, area_order, KeyNumber
    )
    INSERT INTO public.SkillMatrixLiveLevels (AreaID, Level, Label, Description, FillRgb, FontRgb, Origin, SourceRef)
    SELECT live_area, Level, Label, Description, fill, font,
           CASE WHEN wordings_on_sheet > 1 THEN 'MostCommonWording' ELSE 'AsWritten' END, SourceRef
    FROM own
    UNION ALL
    SELECT la.AreaID, c.Level, c.Label, c.Description, c.fill, c.font, 'FilledIn', c.SourceRef
    FROM public.SkillMatrixLiveAreas la
    CROSS JOIN common c
    WHERE NOT EXISTS (SELECT 1 FROM own o WHERE o.live_area = la.AreaID AND o.Level = c.Level);
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END;
$$;

CREATE TRIGGER TR_SkillMatrixLiveLevels_Update BEFORE UPDATE ON public.SkillMatrixLiveLevels FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixLive_GuardRow();
CREATE TRIGGER TR_SkillMatrixLiveLevels_Delete BEFORE DELETE ON public.SkillMatrixLiveLevels FOR EACH ROW EXECUTE FUNCTION public.SkillMatrixLive_GuardDelete();


-- 4. A re-seed (re-import) also rebuilds the levels: 005's body + one line --------------------------
CREATE OR REPLACE FUNCTION public.SkillMatrixLive_Seed(p_import_id BIGINT, p_by_employee_id BIGINT, p_by_name TEXT) RETURNS INT
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

    DELETE FROM public.SkillMatrixLiveLevels;
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

    PERFORM public.SkillMatrixLive_SeedLevels();   -- migration 006

    INSERT INTO public.SkillMatrixLiveChanges (ChangedByEmployeeID, ChangedByName, BasedOnImportID, Action, OldValue, NewValue, Note)
    VALUES (p_by_employee_id, p_by_name, p_import_id, 'Reseed',
            CASE WHEN v_prev IS NULL THEN NULL ELSE 'import ' || v_prev END,
            'import ' || p_import_id,
            'Live copy rebuilt from import ' || p_import_id || ': ' || v_cells || ' cells. Earlier edits remain in this log.');

    RETURN v_cells;
END;
$$;


-- 5. Ownership marker, then build the levels of the current live copy ---------------------------------
COMMENT ON TABLE public.SkillMatrixLiveLevels        IS 'Skills Matrix live keys (migration 006)';
COMMENT ON TABLE public.SkillMatrixLiveTeamLabels    IS 'Skills Matrix live keys (migration 006)';
COMMENT ON FUNCTION public.SkillMatrixLive_SeedLevels() IS 'Skills Matrix live keys (migration 006)';

SELECT public.SkillMatrixLive_SeedLevels() AS levels_built;

COMMIT;
