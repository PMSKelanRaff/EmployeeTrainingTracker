-- A small approved import (import 1), loaded the way SkillsMatrixImportWriter does it (DISPOSABLE TEST DATABASE ONLY).
--   Lab:   categories Tests, Checks; skills Abrasion (A5), Binder (A6), Density (A8, hidden in source)
--          people C = Alice (1), D = Bob (2), E = 'Student X' (Excluded)
--          cells: Abrasion C=5 D=3 | Binder C=unassessed D=1 | Density C=2, D outside the range (no cell)
--   Admin: category General; skill Filing (A5); people B = Alice (1), C = Carol (3)
--          cells: Filing B=Flagged '=SUM(A1)', C=4
BEGIN;
DO $$
DECLARE
    imp BIGINT; sc1 BIGINT; sc2 BIGINT; lab BIGINT; adm BIGINT;
    cTests BIGINT; cChecks BIGINT; cGen BIGINT;
    sAbr BIGINT; sBin BIGINT; sDen BIGINT; sFil BIGINT;
    labC BIGINT; labD BIGINT; admB BIGINT; admC BIGINT;
BEGIN
    INSERT INTO public.SkillMatrixImports (SourceFileName, SourceSha256, ApprovedBy, ImportedBy, Notes)
    VALUES ('fixture.xlsx', repeat('A', 64), 'Tester', 'Tester', 'fixture import 1') RETURNING ImportID INTO imp;

    INSERT INTO public.SkillMatrixScales (ImportID, Name, LegendStatus, SourceRef) VALUES (imp, 'Lab key', 'AsWritten', 'Lab!A1:K1') RETURNING ScaleID INTO sc1;
    INSERT INTO public.SkillMatrixScales (ImportID, Name, LegendStatus, LegendNote, SourceRef) VALUES (imp, 'Admin key', 'NeedsConfirmation', 'levels 2, 3 not listed', 'Admin!A3:H3') RETURNING ScaleID INTO sc2;
    INSERT INTO public.SkillMatrixScaleLevels (ScaleID, Level, Label, SourceRef) VALUES
        (sc1, 1, '1: To be Trained', 'Lab!A1'), (sc1, 5, '5: Skilled to train others', 'Lab!E1'), (sc2, 4, '4: Skilled', 'Admin!D3');

    INSERT INTO public.SkillMatrixAreas (ImportID, ScaleID, Name, SourceSheet, DisplayOrder) VALUES (imp, sc1, 'Lab', 'Lab', 1) RETURNING AreaID INTO lab;
    INSERT INTO public.SkillMatrixAreas (ImportID, ScaleID, Name, SourceSheet, DisplayOrder) VALUES (imp, sc2, 'Admin', 'Admin', 2) RETURNING AreaID INTO adm;

    INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (lab, 'Tests', 1, 'Lab!A4') RETURNING CategoryID INTO cTests;
    INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (lab, 'Checks', 2, 'Lab!A7') RETURNING CategoryID INTO cChecks;
    INSERT INTO public.SkillMatrixCategories (AreaID, Name, DisplayOrder, SourceRef) VALUES (adm, 'General', 1, 'Admin!A4') RETURNING CategoryID INTO cGen;

    INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, InabAccredited, InabRaw, InabSourceRef, DisplayOrder, SourceRow, SourceRef)
        VALUES (lab, cTests, 'Abrasion', TRUE, 'Yes', 'Lab!B5', 1, 5, 'Lab!A5') RETURNING SkillID INTO sAbr;
    INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, InabAccredited, InabRaw, InabSourceRef, DisplayOrder, SourceRow, SourceRef)
        VALUES (lab, cTests, 'Binder', FALSE, 'No', 'Lab!B6', 2, 6, 'Lab!A6') RETURNING SkillID INTO sBin;
    INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, HiddenInSource, DisplayOrder, SourceRow, SourceRef)
        VALUES (lab, cChecks, 'Density', TRUE, 3, 8, 'Lab!A8') RETURNING SkillID INTO sDen;
    INSERT INTO public.SkillMatrixSkills (AreaID, CategoryID, Name, DisplayOrder, SourceRow, SourceRef)
        VALUES (adm, cGen, 'Filing', 1, 5, 'Admin!A5') RETURNING SkillID INTO sFil;

    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, SubGroupLabel, Disposition, EmployeeID, DisplayOrder)
        VALUES (lab, 'C', 'Alice A', 'Lab!C3', 'Senior', 'Member', 1, 1) RETURNING ColumnID INTO labC;
    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, Disposition, EmployeeID, DisplayOrder)
        VALUES (lab, 'D', 'Bob T', 'Lab!D3', 'Member', 2, 2) RETURNING ColumnID INTO labD;
    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, Disposition, ExcludeReason, DisplayOrder)
        VALUES (lab, 'E', 'Student X', 'Lab!E3', 'Excluded', 'not signed up yet', 3);
    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, Disposition, EmployeeID, DisplayOrder)
        VALUES (adm, 'B', 'Alice', 'Admin!B4', 'Member', 1, 1) RETURNING ColumnID INTO admB;
    INSERT INTO public.SkillMatrixColumns (AreaID, SourceColumn, SourceHeaderText, HeaderSourceRef, Disposition, EmployeeID, DisplayOrder)
        VALUES (adm, 'C', 'Carol', 'Admin!C4', 'Member', 3, 2) RETURNING ColumnID INTO admC;

    INSERT INTO public.SkillMatrixCells (AreaID, SkillID, ColumnID, Status, Level, RawValue, FlagReason, SourceCell) VALUES
        (lab, sAbr, labC, 'Level', 5, NULL, NULL, 'Lab!C5'),
        (lab, sAbr, labD, 'Level', 3, NULL, NULL, 'Lab!D5'),
        (lab, sBin, labC, 'Unassessed', NULL, NULL, NULL, 'Lab!C6'),
        (lab, sBin, labD, 'Level', 1, NULL, NULL, 'Lab!D6'),
        (lab, sDen, labC, 'Level', 2, NULL, NULL, 'Lab!C8'),
        (adm, sFil, admB, 'Flagged', NULL, '=SUM(A1)', 'Formula', 'Admin!B5'),
        (adm, sFil, admC, 'Level', 4, NULL, NULL, 'Admin!C5');

    INSERT INTO public.SkillMatrixCellFormats (AreaID, SourceRef, FillSource, FillRgb, Pattern, FontSource, FontRgb, Bold)
        VALUES (lab, 'Lab!C5', 'CellStyle', 'A9D08E', 'solid', 'CellStyle', '000000', FALSE);

    UPDATE public.SkillMatrixImports SET LoadState = 'Complete' WHERE ImportID = imp;
    UPDATE public.SkillMatrixImports SET IsCurrent = TRUE WHERE ImportID = imp;
END;
$$;
COMMIT;
