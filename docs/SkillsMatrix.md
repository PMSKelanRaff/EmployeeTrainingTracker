# Skills Matrix (Admin dashboard)

A **read-only** representation, inside the Training Tracker, of approved data from the audited
*Skills Training Matrix* Excel workbook.

## Status

| Phase | Scope | Status |
|---|---|---|
| 1 | Schema (migration 004), read-only service, Admin "Skills Matrix" tab, this document | Built; **migration not yet run** |
| 2a | Import tool dry run + employee reconciliation report (writes nothing to the database) | Built (see "Dry run" below); not yet run against real employee records |
| 2b | Approved load of confirmed data from a verified workbook copy | Prepared: decisions, import plan, checks and writer built; **the import step is switched off** (see "Preparing the import") |
| Out of scope | Editing, sync back to Excel, Employee/Manager views, training-evidence links, department mapping, security/permission/authentication/deployment changes | Future decisions |

Until an approved import exists the tab shows an explicit empty state.

## Decisions (agreed 2026-10-21)

1. **The original workbook is never modified** (an audited business document). All inspection and
   import work uses a separate copy whose SHA-256 is recorded and verified before and after use.
2. Initial scope is the Admin tab with a read-only view only.
3. **Blank cells = unknown / unassessed** - never level 1 or zero. Stored as `Unassessed`.
4. **Each sheet keeps its own scale wording.** Numbers plus a text legend; no colour meanings are
   inferred from the workbook. Ambiguous or conflicting legends are flagged (`NeedsConfirmation`), not guessed.
   *Update (2026-10-22, requested - the workbook's colours are a standard that must be followed):* the
   preview shows the **workbook's own colours exactly as Excel displays them** ("Workbook colours", on by
   default): level colours, INAB Yes/No green/red, name and team-heading fills (e.g. the yellow student
   highlight), category-row shading and the key cells (shown as coloured key chips with their wording).
   `SkillsMatrixWorkbookColours` evaluates the sheet's conditional formatting as Excel does (priority order;
   'contains text' rules and colour scales) and falls back to fixed fills, resolving theme colours and tints.
   Patterned fills (the 50% grey dots on category rows) are shown as their visual blend, as a grid cell
   cannot draw the dot pattern. Numbers are always shown; flagged cells keep their workbook fill plus an
   orange ⚠; cells outside a sheet's range are shown grey (an app marker, not a workbook colour).
   Where the workbook colours the same value differently (e.g. Laboratory `E44:E51`, `F44:F51`;
   Data Analysis `C32` INAB "No" in green), it is shown as in the workbook and listed in Review notes.

   *Verification (2026-10-22):* Excel itself (new hidden instance, read-only, macros disabled, never saved)
   reported the displayed colours of all 6,160 cells of the verified copy; the copy's SHA-256 was unchanged.
   Of the 5,999 cells that are displayed individually (161 merged non-top-left cells excluded): fill matches
   exactly for 5,820; the other 179 are all theme "Accent 1, lighter 40%" (level 1 light blue), within
   2/255 on one channel (`#9DC4E6` vs Excel's `#9BC2E6`, not visible). Font colour and bold match for all.
   *Database import:* migration 004 stores the displayed formatting of every imported cell in
   `SkillMatrixCellFormats` (see "Formatting" below); the import tool that captures it is Phase 2 and
   separately approved.
5. Hidden legacy/auxiliary sheets and the "Skill Matrix required" template are excluded from
   display and import (recorded below). HTSF09 sign-off data is a possible future evidence source,
   not imported now.
6. Duplicate rows, formulas and unusual values are **flagged for review**, never silently dropped
   or reinterpreted. Formula results are not converted into proficiency levels.
7. **Only manually confirmed employee matches are imported.** Close matches, unmatched names,
   students and archived employees are not assigned automatically; a reconciliation report is
   produced for confirmation.
8. Skills stay separate per sheet. Sheet-to-department mappings are not forced (proposals below).
9. **The workbook remains the audited reference**; the application is a read-only representation
   of its approved data. No editing and no synchronisation back to Excel. Making the application
   authoritative is a separate future decision (including how auditability is maintained).
10. The existing architecture and access-control pattern are used unchanged. Limitations are
    documented below for a future security phase.

## Workbook findings (inspected copy)

Inspected file: `Copy of Skills Training Matrix 2026 v1.xlsx` (local copy, 136,728 bytes),
SHA-256 `82DFD157278C526832461CC4B8DD0BEA8AB2A91FDD7A15BA1767F27B23EF9422` - identical before and
after inspection. Inspection used a second working copy opened by read-only scripts that never save.

### Sheets

| Sheet | State | Treatment |
|---|---|---|
| Laboratory | visible | Import candidate |
| Data Collection | visible | Import candidate |
| Data Analysis | visible | Import candidate (three team blocks) |
| GIS | visible | Import candidate |
| Software and Data Analytics | visible | Import candidate (own scale, "Area" sub-groups) |
| QMS | visible | Import candidate (no INAB column) |
| Innovation | visible | Import candidate (two side-by-side tables with different people) |
| H&S Environmental | visible | Import candidate (two legends - see below) |
| Admin | visible | Import candidate (legend lists 1, 4, 5 only; contains formulas) |
| Skill Matrix required | visible | **Excluded** - a template with placeholder staff ("Team Member A-F"), Current/Target levels, soft/technical skills and capability-gap formulas. Possible future target design. |
| Sheet1 | hidden | **Excluded** - appears to be an older Data Analysis / Data Collection layout |
| Inab | hidden | **Excluded** - appears to be an older INAB-focused layout |
| CPD | hidden | **Excluded** - CPD courses (IAT, Train the Trainer); possible future relevance |
| Students | hidden | **Excluded** - 2026 student intake; possible future relevance |
| HTSF09 | hidden | **Excluded** - sign-off dates with initials (e.g. `07/01/13 JW`); **possible future training-evidence source** |

### Structure of the matrix sheets

- Rows are skills grouped under category header rows (e.g. *Laboratory Tests to EN*, *On-Site Checks*).
- Columns are named staff, sometimes in labelled sub-groups (e.g. "Dermot Leonard Lab Manager" /
  "Support Lab Staff"; Data Analysis has Network / Non-Structural / Structural team blocks).
- Most sheets have an "INAB accredited" Yes/No column; absent on QMS, H&S Environmental and Admin,
  and blank on some rows (e.g. Laboratory Checks) - stored as *not specified*.
- Every matrix range has a data-validation list of exactly `1,2,3,4,5` with blanks allowed.

### Proficiency legends (stored as written, per sheet)

| Sheet(s) | Legend as written | Status |
|---|---|---|
| Laboratory, Data Collection, Data Analysis, GIS, QMS, Innovation | 1: To be Trained · 2: In Training · 3: Requires Supervision · 4: Skilled in Area · 5: Skilled to train others | As written |
| Software and Data Analytics | 1: Awareness · 2: Supported Work · 3: Independent Work · 4: Advanced Work · 5: Lead, each with a description | As written |
| H&S Environmental | **Two keys:** "1: To be Trained, 4: Trained, 5: Skilled to train others" *and* the standard 1-5 key | **Needs confirmation** |
| Admin | 1: To be Trained · 4: Skilled in Area · 5: Skilled to train others (no 2 or 3) | **Needs confirmation** |
| Skill Matrix required (template, excluded) | 1 Identified to be trained · 2 Basic Proficiency · 3 Show Capability · 4 Fully Capable · 5 Expert/Coach/Mentor | n/a |

Colour: conditional formatting is overlapping and contradictory (e.g. five different colour rules
for level 4), so **no colour meaning is used**.

### Items to flag in the import report (Phase 2)

- Formulas inside proficiency cells: `Admin!H6` and `Admin!H7` = `=G6/F6` (cached result 1).
  These are imported as **Flagged**, never as a level.
- Hidden rows: `Laboratory` row 41 (a duplicate "Compaction Moulds"), `Data Collection` row 12
  ("Griptester").
- Repeated skill names within or across sheets (e.g. FAP, ITS, MIST appear under both Laboratory
  Tests and Laboratory Checks; Liquid Limit under EN and BS; ArcGIS Pro on GIS and Data Analysis).
  Kept as separate skills per sheet/source cell.
- Data Collection "External" section (LUGS, Chemical Awareness Training, Digital Tacho, Eco
  Driving) has no values and overlaps H&S Environmental.
- Template formula rule (excluded sheet): "capable" counts **exactly level 4** (`COUNTIF(...,4)`),
  excluding level 5 - to be confirmed if the template is adopted later.

### Employee reconciliation (summary)

64 distinct names across the visible sheets compared with `Employees.FullName`:
45 match after case/punctuation normalisation, 16 are close spellings, 3 have no match.
Notable cases needing manual decisions: a close match whose first name differs
("Brian Feighan" vs "Kieran Feighan" - likely wrong), a match to an **archived** employee
(Peadar Joliffe Byrne), students (e.g. "Student Liam Melia", "(Student)" columns) and three
unmatched names. **No match - including exact ones - is used until confirmed** in the Phase 2
reconciliation file.

### Proposed sheet-to-department mappings (unresolved - not stored)

| Sheet | Possible group(s) | Note |
|---|---|---|
| Laboratory | Laboratory | |
| Data Collection | Data Collection | |
| Data Analysis | Data Analysis N / NS / S | One per team block? |
| Software and Data Analytics | Software | |
| QMS | Quality | |
| H&S Environmental | Health&Safety | Sheet lists most staff company-wide |
| Innovation | Innovation, Equipment | Two tables on one sheet |
| GIS | ? | No matching group |
| Admin | ? (Commercial / Operations?) | Unclear |

Sheet columns do not equal department membership (people appear on sheets outside their group),
so membership is stored per sheet (`SkillMatrixColumns`), not derived from `GroupMembers`.

## Data model (migration `Database/Migrations/004_skills_matrix.sql`)

Additive only. Creates exactly: 9 tables, 4 trigger functions, 28 triggers (all on the new tables),
their constraints/sequences, and 5 named indexes. Every new table and function carries the comment
`Skills Matrix (migration 004)`. Undo with `004_skills_matrix_rollback.sql`, which drops only objects
carrying that marker (no `CASCADE`).

```
SkillMatrixImports          one immutable snapshot per approved import; LoadState Loading -> Complete
  SkillMatrixScales         legend(s), wording as written; LegendStatus AsWritten | NeedsConfirmation
    SkillMatrixScaleLevels  entries; KeyNumber allows two keys on one sheet
  SkillMatrixAreas          one per imported sheet (its scale must be from the same import)
    SkillMatrixCategories   category header rows
      SkillMatrixSkills     skill rows; INAB Yes/No/not specified; HiddenInSource; source row/ref
                            (unique by source cell: Innovation has two side-by-side tables sharing rows)
    SkillMatrixColumns      every person column: Member (confirmed EmployeeID) | Excluded (reason)
    SkillMatrixCells        Level (1-5) | Unassessed (blank in source) | Flagged (raw value + reason);
                            no row = outside the sheet's source range
    SkillMatrixCellFormats  the workbook's DISPLAYED formatting of one source cell (see below)
```

Source references: every stored value records the exact cell it came from - legend entries
(`ScaleLevels.SourceRef`), the skill and INAB header cells (`Areas.SkillHeaderRef` / `InabHeaderRef`),
each skill's INAB cell (`Skills.InabSourceRef`, required whenever `InabRaw` is stored), each person
column's header and sub-group cells (`Columns.HeaderSourceRef` / `SubGroupSourceRef`).

### Formatting (`SkillMatrixCellFormats`)

Three different things, kept apart:

| | What it is | Stored? |
|---|---|---|
| Raw cell style | the fill/font saved in the cell's style, before conditional formatting | **No** - only as the source of a displayed colour when no rule overrides it |
| Effective displayed formatting | what Excel actually shows: cell style + the winning conditional-formatting rule (colour scales, "contains text", ...) | **Yes** - one row per formatted source cell |
| App colouring | the app's own markers (grey out-of-range cells, "Workbook colours" toggle, key chips) | **Never** - derived at display time |

Per row: `FillSource` = `None` | `CellStyle` | `ConditionalFormat`; `FillRgb` (6-hex) with `Pattern`
(OOXML pattern name) and, for non-solid patterns, `PatternColorRgb` + `PatternBackRgb`;
`ConditionalRule` = the winning rule as text (required exactly when the fill came from a rule, and
such fills are always solid); `FontSource` / `FontRgb` / `FontRule` (rule text required exactly when
the font colour came from a rule); `Bold`. A trigger requires `SourceRef` to be on the area's own
sheet. Imports record how colours were derived (`ColourMethod`) and how they were checked
(`ColourVerification`), both immutable.

Cell states (one CHECK constraint enforces exactly these combinations):

| Status | Level | RawValue | FlagReason |
|---|---|---|---|
| `Level` | required, 1-5 | must be NULL | must be NULL |
| `Unassessed` | must be NULL | must be NULL | must be NULL |
| `Flagged` | must be NULL | required (value / formula as written) | required, non-blank |

Integrity rules enforced by the database:
- **Isolation** (composite foreign keys): an area's scale must belong to the same import; a skill's
  category must belong to the same sheet; a cell's skill and column must both belong to the cell's
  sheet, and the column must be a `Member`. `SkillMatrixScaleLevels` hang off exactly one scale and
  are referenced by nothing, so they need no `ImportID`.
- **Lifecycle** (triggers): child rows can only be inserted while their import is `Loading`;
  `Loading -> Complete` is one-way; only a `Complete` import can be current; a partial unique index
  allows at most one current import; and a deferred check at **commit** requires that no import is
  still `Loading` and that **exactly one** import is current whenever any exist (zero imports = the
  tab's empty state). Replacement = one transaction (load new -> Complete -> clear old `IsCurrent` ->
  set new `IsCurrent` -> commit); any failure rolls back and leaves the previous snapshot current.
- **History**: snapshot rows can never be updated, deleted or truncated; on an import only
  `LoadState` and `IsCurrent` may change.
- **Limits**: these triggers prevent accidental change; they are not a security control. The table
  owner or a superuser (`postgres`, member of `rds_superuser`) can disable or bypass triggers
  (e.g. `ALTER TABLE ... DISABLE TRIGGER`, `session_replication_role = replica`) or drop the tables.
- **Side effect on an existing table (needs explicit approval before production)**: the foreign key
  `SkillMatrixColumns.EmployeeID -> Employees(EmployeeID)` makes PostgreSQL add two *internal*
  system triggers to `Employees` and locks it against writes until the migration commits (see
  "Schema and transaction safety" for lock modes). Effect: an employee
  referenced by a Skills Matrix column cannot be hard-deleted (`ON DELETE RESTRICT`). Neither app
  hard-deletes employees (they are archived). No `Employees` row, column or user-defined object
  changes; the rollback removes the internal triggers. Alternative: omit the foreign key (zero
  footprint on `Employees`; integrity then relies on the import tool). **Decision (2026-10-22): keep
  the foreign key (option A).** Consequence: because snapshots can never be deleted, an employee
  referenced by any snapshot cannot be hard-deleted (archived or not) unless the whole Skills Matrix
  is rolled back.
- `SkillMatrixCells.CellID` is the attachment point for future evidence links (not built).

The read-only view anchors every query to one `ImportID`, so a snapshot swap while the tab is open
cannot produce an inconsistent display.

### Isolated test (2026-10-21)

Run on a disposable local PostgreSQL 18.6 instance (localhost:54329, dummy stand-in tables, removed
afterwards); never connected to RDS or the existing local service. Result: **109/109 passed** on the
final run (a first run found a real `CHECK` NULL-logic bug, fixed before the final run). Covers:
object inventory, no change to pre-existing objects/data, service SQL (empty state, Admin check,
reads), every cell-state and isolation constraint, snapshot lifecycle (replacement, failed
replacement, two-current / zero-current / still-Loading rejection, immutability on all tables),
rollback guard, failure on name conflicts, and rollback restoring the exact pre-migration state.

### Isolated test, revised schema with formats table (2026-10-22)

Same disposable setup. The first run found a real bug: `SkillMatrix_RequireLoadingImport` referenced
`NEW.sourceref` inside `TG_TABLE_NAME = ... AND ...`; PL/pgSQL does not short-circuit, so every insert
into a table without that column failed. Fixed with a nested `IF`. Final runs:
- **Option A (foreign key, file as written): 134/134 passed**, including 16 format/source-reference
  constraint tests, immutability of format rows and colour provenance, and rollback leaving the
  database identical (schema and data).
- **Option B (temporary copy without the foreign key): 127/134**. The 7 differences are exactly the
  expected consequences of having no foreign key: no internal triggers on `Employees` (M7); a
  column pointing to a non-existent employee is not rejected by the database (C26); an employee
  referenced by a column can be hard-deleted (C34/C35), and because that dummy delete committed, the
  data-inclusive rollback comparisons differ in the stand-in Employees/Users/GroupMembers data
  (R5/R7/R8). Schema-only comparison after rollback and after a second migrate/rollback cycle:
  identical (R5s/R8s).

### Schema and transaction safety (2026-10-22)

Changes to both scripts:
- **Schema**: every object is created in, and every reference (including `public.Employees`) points
  to, schema `public` explicitly. The rollback's ownership check and its `DROP`s name the same
  `public` objects. Each script sets `SET LOCAL search_path = pg_catalog, pg_temp`, so an unqualified
  name fails instead of resolving to another schema; `LOCAL` means it ends with the transaction.
- **Trigger functions**: all four pin `SET search_path = pg_catalog, pg_temp` and qualify every table
  they read. They are not `SECURITY DEFINER`, so this is about correctness rather than privilege:
  without it, a session whose `search_path` puts another schema first could make the loading check
  or the commit-time check read a same-named table there.
- **All-or-nothing**: each script contains its own `BEGIN ... COMMIT`. Any error aborts the
  transaction, every later statement is refused, and the server turns the final `COMMIT` into
  `ROLLBACK`, whether or not the client stops at the first error.
- **`SET LOCAL lock_timeout = '5s'`**: each lock wait is limited to 5 s; if a lock is not granted in
  time the script fails and rolls back instead of waiting indefinitely. Lock modes on `Employees`
  (measured on the disposable instance):
  - migration: `SHARE ROW EXCLUSIVE` (to create the foreign key), held until `COMMIT` - blocks other
    sessions' writes to `Employees`, not reads;
  - rollback: `ACCESS EXCLUSIVE` (to remove the foreign key's internal triggers), held until
    `COMMIT` - blocks other sessions' reads **and** writes of `Employees`.
  While either script waits for its lock, conflicting application requests queue behind it, so they
  can be delayed by up to about 5 s; once granted, the rest of the script takes well under a second
  in testing. Run both at a quiet time.

Test results (disposable instance as above): **147/147 passed**, including the 134 earlier tests run
the documented way (complete file, no `-1`), plus:
- a failure part-way through the migration (after the 9 tables were created) leaves nothing, both
  with `ON_ERROR_STOP` (psql exit 3) and without it (psql exit 0, final tag `ROLLBACK`);
- the rollback's ownership refusal, without `ON_ERROR_STOP`, drops nothing;
- with `search_path = decoy, public` (decoy holding same-named tables, one even carrying the marker):
  the migration creates everything in `public`, the trigger functions ignore the decoy tables, and
  the rollback drops only the `public` objects;
- the four functions' pinned `search_path`, and that the session `search_path` is back to normal after
  the migration;
- a migration started while another session holds a write lock on `Employees` fails after 5.1 s
  with "lock timeout" and creates nothing.

The same suite run against the previous version of the scripts failed these checks. With
`ON_ERROR_STOP` but no `-1`, a part-way failure kept the tables already created. Without
`ON_ERROR_STOP`, the rollback dropped the unrelated table after its own check had refused. With the
decoy path, the migration targeted the decoy schema and the rollback dropped the decoy tables.

### Running migration 004 / its rollback

Supported and tested method: `psql` (PostgreSQL 18 client), a **new** session, the **complete** file:

```
psql "host=<host> port=5432 dbname=<db> user=<user> sslmode=require" -X -v ON_ERROR_STOP=1 -f 004_skills_matrix.sql
```

- Do **not** add `-1` / `--single-transaction`; the files manage their own transaction (psql's
  documentation warns that `-1` does not behave as intended when a script has its own `BEGIN`/`COMMIT`).
- `ON_ERROR_STOP=1`: psql stops at the first error and exits with code 3; the open transaction is
  rolled back when the session ends. Without it psql continues, reports `ROLLBACK` for the final
  `COMMIT`, and exits with code **0** - so always read the output, not only the exit code.
- Never run either file inside a transaction that is already open: `BEGIN` would only warn, and
  `COMMIT` would also commit whatever that transaction had already done.
- Other clients (DBeaver, pgAdmin) are **not tested**. If one is used anyway: auto-commit on (so the
  file's own `BEGIN`/`COMMIT` controls the transaction), run the whole file as one script in one fresh
  session, and make sure the client sends each `$$ ... $$` function body intact. If any statement
  fails, stop, run `ROLLBACK`, then verify with the query below.
- Do not run the file piece by piece (selected statements only): if the `BEGIN` line is not part of
  what is sent, an auto-commit client commits each statement on its own and a failure part-way then
  needs manual clean-up.

Read-only check afterwards (expected `13` after the migration, `0` after the rollback):

```sql
SELECT COUNT(*) FROM (
  SELECT obj_description(c.oid, 'pg_class') AS d FROM pg_class c
   WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r' AND c.relname LIKE 'skillmatrix%'
  UNION ALL
  SELECT obj_description(p.oid, 'pg_proc') FROM pg_proc p
   WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'skillmatrix%') x
WHERE d = 'Skills Matrix (migration 004)';
```

Prerequisites before production (each read-only check needs separate approval before it is run):
a database backup/snapshot; `public.Employees(EmployeeID)` exists with a primary key or unique
constraint; no `skillmatrix*` objects already exist; the role running the migration is the role the
application connects as (the migration adds no grants); a decision on the hard-delete consequence
above.

## Dry run and employee reconciliation (Phase 2a)

**Where:** Admin tab -> "Open workbook copy (preview)…" -> **"Dry run report…"**. The button exists only in
preview mode and works on the copy already read into memory. Its only outputs are an on-screen window and,
if the user chooses, new report files. There is no import, confirm or database action anywhere in it.

**No database:** `SkillsMatrixDryRun`, `SkillsMatrixReconciliation`, the preview reader and the dry-run
window make no database connection (checked by the tests). The Admin check happens once, when the preview
is opened (unchanged).

**Employee records:** reconciliation uses only an employee list file chosen by the user: a local CSV with
`EmployeeID`, `FullName` and optionally `IsArchived`. The file is opened read-only and its SHA-256 goes in the
report. With no list, every name is reported unmatched (or student). Real reconciliation needs the
current employee records in such a file. Producing it from the live database is a separate decision,
not part of this phase.

**Rules** (all rows start `UNCONFIRMED`; nothing is ever accepted automatically):

| Status | When |
|---|---|
| Exact candidate | identical text, or identical after normalising case, punctuation (incl. apostrophes), spacing and accents |
| Suggested match - requires confirmation | close spelling (1 character; 2 for names of 10+ letters), one name containing the other, or several equally good records (then none is proposed) |
| Unmatched | nothing close; an empty header. Same surname with a different first name is listed as *not proposed* |
| Student / non-employee | the header text contains the word "student"; no match is proposed, similar records are only mentioned |
| Archived employee | the best candidate's record is archived |

Every row keeps the original text, all its header-cell references, the match type, a confidence level
(High / Medium / Low / None), a reason, and empty `ConfirmedEmployeeID` / `ReviewerNotes` columns for
the reviewer. When first names differ, the reason warns that it may be a different person.

**Files** (new files only, never overwriting; names start `SkillsMatrix_DRYRUN_<timestamp>`): `_Report.txt`
(the summary), `_Reconciliation.csv`, `_ReviewItems.csv` (every flagged cell or row, with its reference and
kind) and `_Formatting.csv` (each shown cell's displayed formatting in the `SkillMatrixCellFormats`
shape). CSV values starting with `= + - @` get a leading `'` so spreadsheet programs show them as text.
The files contain employee names.

**Tests:** `Tests/SkillsMatrixTests` is a dependency-free console runner, kept outside the solution so
the application build is unchanged. Run it with `dotnet run --project Tests/SkillsMatrixTests`. It
generates a dummy workbook and dummy employee lists in a temp folder; `--sample <folder>` writes the
dummy dry-run files for review. Result (2026-10-22): **131/131 passed**. Against the local verified
working copy (read-only, no employee list), the dry run gives the level counts verified earlier
(Laboratory 221, Data Collection 151, Data Analysis 277, GIS 16, Software 42, QMS 15, Innovation 12,
H&S 229, Admin 14), the 2 formula cells, 64 distinct names, and 3,174 formatting rows. All 3,174 meet
migration 004's formatting rules. The copy's SHA-256 was unchanged.

## Preparing the import (Phase 3)

**Workflow** (Admin tab, all steps read-only until the last, which is switched off):
1. **Open workbook copy (preview)…** - read into memory, SHA-256 checked before and after.
2. **Dry run report…** - "Decisions needed" tab lists, most urgent first, every name, ambiguous key or
   level, formula / flagged cell and other check; **Save dry-run report files…** writes the report and CSVs.
3. In the saved reconciliation CSV, the reviewer sets `HumanConfirmation` to `CONFIRMED` (with
   `ConfirmedEmployeeID`) or `EXCLUDED` (reason in `ReviewerNotes`) for **every** name, and saves it as
   "CSV UTF-8". Nothing is confirmed by the application.
4. **Prepare import…** - choose the employee list used for the review, then the reviewed CSV, and enter
   *Approved by*. The window shows exactly what would be written (per table and sheet, every member and
   excluded column), the blocking errors, and the acknowledgements to tick. **Save plan summary…** writes it
   to a new file. The **Import** button is enabled (`SkillsMatrixImportForm.ImportExecutionEnabled = true`,
   switched on 2026-11-11 after the production pre-check: migration 004 installed and empty, all 49 plan
   employees match the reviewed list). Set it to `false` to disable the button again.

**Blocking errors** (nothing can be imported while any remain): a name still UNCONFIRMED (or any value other
than CONFIRMED / EXCLUDED); a confirmed ID that is not a whole number or not in the employee list; an
exclusion without a reason, or with an ID; a decisions file whose names or cell references do not match the
workbook (another workbook, an edited file, or a CSV saved without UTF-8); duplicate rows; a workbook that
changed while being read; no employee list; no *Approved by*; any breach of migration 004's rules (checked
in the application before anything could be written).

**Acknowledgements** (each must be ticked by a person): a file that is not the verified copy; each key that
needs confirmation (imported as written, `NeedsConfirmation`); each sheet's flagged cells (imported as
`Flagged` with their original value, never as levels); levels a sheet's key does not define; each archived
or "student" name confirmed as an employee; the same employee in two columns of one sheet. The current
workbook has no levels outside its keys; H&S Environmental and Admin need key acknowledgements, Admin its
two formula cells.

**What an import writes:** one transaction - the import row (with approver, importer, notes recording the
decisions file and employee list with their SHA-256, colour method and verification), then scales, key
entries, areas, categories, skills, every person column (`Member` with the confirmed employee, or
`Excluded` with the reviewer's reason), cells for `Member` columns only, and the displayed formatting of
the stored cells. It is then marked Complete and made current; earlier snapshots are kept unchanged. Any
error (e.g. an employee ID the database does not have - the Employees foreign key) leaves nothing.
Nothing in existing tables is written. Departments are not mapped (no decision has been made).

**Reading an import:** the Admin tab's normal (non-preview) view now also reads `SkillMatrixCellFormats`,
so an imported snapshot shows the workbook's colours as the preview does.

**Writer** (`SkillsMatrixImportWriter`): the only Skills Matrix code that writes. It uses the connection it is
given, refuses a plan that is not ready, missing acknowledgements, or a typed confirmation other than
`IMPORT <first 8 characters of the file's SHA-256>`, and sets `search_path` / `lock_timeout` like the
migration. Before the first real import, the remaining steps are listed in "Remaining before the first import".

**Tests (2026-10-22):** isolated suite **170/170** (adds decisions, plan, schema-rule, refusal and
switch-off tests); writer on a disposable local PostgreSQL with migration 004 and dummy data **20/20**
(first import, a rejected import leaving nothing, a replacement import keeping the first snapshot, stored
states, reasons, formatting and the service's formatting query).

### Remaining before the first import (each needs a separate decision / approval)
1. **Current employee records** for the reconciliation: an employee-list CSV produced from the live
   `Employees` table (a read-only export) - not done; the app never reads it for this.
2. **Review**: every name CONFIRMED / EXCLUDED in the reconciliation CSV; the H&S and Admin keys and the
   two Admin formula cells acknowledged (or the workbook corrected and re-run).
3. **Migration 004 on production** (prerequisite checks and backup listed above).
4. **Enabling the import step** for the approved run (`ImportExecutionEnabled`), and access for the new
   tables if the application does not connect as their owner.

## Editing: live copy and change log (migration `Database/Migrations/005_skills_matrix_live.sql`)

First import completed on production 2026-11-11 (import 1: 112 member columns, 2,522 cells, 3,091 formatting rows;
verified read-only against the plan). Decisions (2026-11-11): edit in the app; Admins edit everything, managers
edit levels of the people in the groups they manage; the planned re-import at company-wide launch (~2 months)
**resets** the live copy to the new import (earlier edits stay in the change log); the app is expected to become
the source of truth later.

Additive only; requires 004 and changes none of its objects. Creates 7 tables, 5 functions, 14 triggers and
5 named indexes, all marked `Skills Matrix live (migration 005)`. Undo with `005_skills_matrix_live_rollback.sql`
(roll back 005 **before** 004 - 004's rollback refuses while 005 exists).

```
SkillMatrixLiveState       which import the live copy was built from (one row)
SkillMatrixLiveAreas       sheets (legend and workbook refs through SourceAreaID -> the snapshot)
  SkillMatrixLiveCategories, SkillMatrixLiveSkills, SkillMatrixLiveColumns (Member columns only)
  SkillMatrixLiveCells     Level | Unassessed | Flagged (Flagged only as imported; resolvable to a level)
SkillMatrixLiveChanges     append-only log: who, when, what (IDs + names as text), old -> new
```

- `Source*ID` links each live row to the snapshot row it came from (NULL = added in the app); it never changes.
- Every update increases the row's `Version` (trigger): saves use `WHERE Version = <read version>`, so a save
  over someone else's newer change updates nothing and is reported instead of overwriting.
- Rows are not deleted (`IsDeleted`); only `SkillMatrixLive_Seed` deletes, while rebuilding the copy.
- `SkillMatrixLive_Seed(importId, employeeId, name)` rebuilds the live copy from a Complete import in the
  caller's transaction and logs a `Reseed` entry. The migration seeds from the current import.
- Same side effect on `Employees` as 004 (foreign key: internal triggers, SHARE ROW EXCLUSIVE lock during the
  migration; ACCESS EXCLUSIVE during the rollback).

**Tests (2026-11-11)**, `Database/Tests/Run-005Tests.ps1` - a disposable local PostgreSQL 18.6 cluster on
localhost:54329 (created and deleted by the script; stand-in Employees/Users/Groups with dummy people):
**63/63** SQL tests (`tests_005.sql`: objects, seed fidelity, cell rules, versioning, new rows, no hard deletes,
append-only log, re-seed from a second import, 004 rules intact) and **16/16** scenario checks (second run
refused; rollback restores schema **and** data exactly, twice; 005 without 004 refused; rollback refuses an
unmarked table; 004 rollback refused while 005 exists; no import yet; failure part-way leaves nothing).

### Editing levels in the app (step 2: Admins)

- The tab shows the live copy when 005 is installed and seeded (otherwise the import, read-only, as before).
  The source line says which import the live copy was built from, and warns if a newer import is current.
- **Edit** (tick box): select one or more cells and type `1`-`5`, or `Delete` / `Backspace` / `0` / `-` for not
  assessed; or right-click (levels named as in the sheet's key; "Undo unsaved change"). `Ctrl+S` saves,
  `Ctrl+Z` undoes the selected unsaved changes. Cells outside the sheet's range are not editable.
- Unsaved changes are highlighted (bold on amber, tooltip `old -> new`) until **Save** or **Discard**.
  Switching sheet, Refresh, opening a preview, leaving edit mode or closing the dashboard asks first.
- **Save** (`SkillsMatrixLiveService.SaveCells`): one transaction; re-checks the editor is an active Admin;
  locks the cells and refuses the whole save if any changed since it was read, naming who changed it; writes
  each cell and one `SetCell` log entry (old -> new; resolving a flagged cell keeps its original value in the log).
- Display: a level changed in the app takes the colour of that level's key cell on the sheet (workbook
  colours on); hover shows who changed it and when. **History…** lists the sheet's change log.
- Tests: `Run-005Tests.ps1` scenario H builds `Tests/SkillsMatrixTests` and runs `--live-test` (guarded:
  localhost:54329/live with a sentinel table): **31/31** (reads, saves, stale-save refusal, all-or-nothing,
  Employee/Manager/unknown/archived refused, invalid levels, cells outside the range or from another sheet,
  flagged cell resolved, no-op save, snapshot unchanged). Existing suite: 176/176.

Migration 005 applied to production 2026-11-11 11:31 (RDS snapshot taken beforehand; read-only checks before and
after: 12 objects / 14 triggers; live copy = import 1 exactly: 9 sheets, 17 categories, 199 skills, 112 people,
2,522 cells, 0 differences; import 1 unchanged).

### Changes requested 2026-11-11

- **People on a sheet** are managed with the employee records (Employees tab), not added/removed in the matrix.
- **Undo from History**: select a level change, "Undo selected change". Only when the cell still holds that
  change's new value (undo newer changes first); a resolved flagged value cannot be put back; logged with the
  note "Undo of change N" (`SkillsMatrixLiveService.UndoChange`).
- **Import tools moved** to a separate **Skills Matrix Import** tab (workbook preview, dry run, reconciliation,
  prepare/import), shown only to the employees in `appsettings.json` `SkillsMatrix:ImportEmployeeIds` (currently
  `5`) and re-checked when each tool is used (`SkillsMatrixService.EnsureImportUser`). The Skills Matrix tab is
  view and edit only. Application-level control, like the rest of the app.
- **Uniform keys and manager names** - migration `006_skills_matrix_live_keys.sql` (+ rollback): one wording per
  level for every live sheet (`SkillMatrixLiveLevels`): where a key words a level twice, the wording used by most
  sheets (H&S Environmental: `4: Skilled in Area`, not `4: Trained`); where a key leaves a level out, the most
  common wording and colour (Admin: `2: In Training`, `3: Requires Supervision`). Sheets with their own scale
  (Software and Data Analytics) keep it. The team row shows the manager's name from the workbook heading
  (`SkillMatrixLiveTeamLabels`: 9 headings, e.g. `Dermot Leonard Lab Manager` -> `Dermot Leonard`); headings
  without a manager stay as written. Both survive a re-seed (the seed function now also rebuilds the levels).
  The imported snapshots keep the keys exactly as written. Read-only preview on production: only H&S level 4
  and Admin levels 2-3 differ from the keys as written; all 9 headings matched.
- Tests (disposable PostgreSQL 18.6): 006 SQL suite **21/21** (fixture plus a five-sheet import: duplicate
  wording, missing levels, own scale, colours, re-seed, team names); scenarios now **30/30** (006 rollback
  restores schema and data exactly twice, incl. 005's seed function; 006 without 005 refused; 005 rollback
  refused while 006 exists; 006 -> 005 -> 004 rollback order; unmarked-table refusal); C# live tests **38/38**
  (adds the live key, undo, undo refusals); existing suite 176/176.

Migration 006 applied to production 2026-11-11 (read-only checks before and after: 3 objects; 45 key rows; only
H&S level 4 and Admin levels 2-3 differ from the keys as written; 41 people show a manager's name; the 2,522 live
cells byte-for-byte unchanged (fingerprint), the change log untouched; snapshot unchanged).

### Managers (step 4) and re-import (step 5)

- **Managers**: the Manager dashboard has a **Skills Matrix** tab (live copy only). It lists the sheets that have
  at least one of their people, and on each sheet shows only the people in the groups they manage (Groups.ManagerID,
  the same rule as `ManagerAccessService`) plus themselves. They can change those people's levels, **not their
  own** (their column is read-only), use Undo, and see the history of their people. Admins are unchanged.
  `SkillsMatrixLiveService.GetAccess` re-reads role, archived status and group membership on every call, and
  `SaveCells` re-checks every cell inside the save transaction (a person removed from the group is refused at
  once; a batch with one forbidden cell saves nothing).
- **An approved import rebuilds the live copy** in the same transaction (`SkillsMatrixImportWriter.Write` calls
  `SkillMatrixLive_Seed` when 005 is installed; failure keeps nothing). The confirmation box states how many
  changes made in the app since the last rebuild would be replaced (they stay in History).
- Tests: C# live tests **52/52** (adds manager access, filtering, own-level and other-people refusals, mixed
  batch refused, group removal, manager undo, history filter, import rebuilding the live copy); database
  suites and scenarios unchanged (63/63, 21/21, 30/30); existing suite 176/176.

### Archived people and Refresh (2026-11-11)

- **Archived in the Employees tab = hidden on the Skills Matrix** (Admin and Manager views), read at display time,
  so it is always in sync. Their levels are kept: unarchived, they reappear with their data. "Show archived
  people" shows them greyed, marked "(archived)", read-only; `SaveCells` refuses archived people. A manager's
  sheet list leaves out sheets where all their people are archived. On production this currently hides Liam
  Melia (Data Collection), Oisin Porter (Data Analysis) and Peadar Jolliffe-Byrne (GIS, H&S Environmental), who
  were imported deliberately although archived.
- **Refresh stays on the sheet being viewed** (matched by sheet name).
- C# live tests **63/63** (adds archived hidden / shown on request / save refused / manager sheet list / unarchived).

### Re-import with merge (2026-11-11)

For updating the matrix from a newer workbook without losing levels changed in the app (Skills Matrix Import
tab -> Prepare import -> **Compare with live copy…**). `SkillsMatrixMerge.Compare` (read-only) compares three
versions of every cell - the import the live copy was built from, the app's value now, the new workbook:

| Situation | Result |
|---|---|
| changed only in the workbook | the workbook's value (automatic) |
| changed only in the app | the app's value is kept (automatic) |
| changed in both, to the same value | nothing to do |
| changed in both, differently | **conflict**: the person importing chooses per cell ("Keep all app values" / "Take all workbook values"); the import cannot run until all are decided |
| changed in the app, but the skill / person / cell is not in the new workbook | listed as "cannot be kept", with the reason |

- Structure (sheets, categories, skills, people) follows the workbook; the comparison lists what is new or gone.
- Matching: sheet by name; skill by (sheet, category, name, n-th occurrence of that name) - so a renamed skill
  is "removed + added"; person by (sheet, employee, n-th column of that person - a person can be in both tables
  of a side-by-side sheet).
- The import (`SkillsMatrixImportWriter.Write(..., merge)`) rebuilds the live copy and puts the kept app values
  back in the same transaction (shown as changed by their original author; one `SetCell` log entry each, "Kept the
  app's value over the workbook ..."). It locks the live cells and is refused if anyone saved a change after the
  comparison. If there are app changes and no comparison, Import asks: compare first / replace them / cancel.
- The saved plan summary includes the comparison.
- **New people right of a sheet's usual columns** are now read automatically (header cells with names, up to the
  first empty header, never into the next table on the sheet), reported as `AddedPersonColumn` review items, and go
  through the normal reconciliation. **New sheets** still need their layout added in code (they are listed as
  "visible but not a recognised matrix sheet"). New rows need the sheet's data-validation range to cover them.
- Tests: C# live tests **88/88** (adds 24 merge tests on a two-version dummy workbook: keep, conflict, same, lost,
  workbook-only change, structure, undecided conflict refused, stale comparison refused, kept values in place and
  logged, renamed row, new row / person, auto-detected column excluded). Real workbook copy: 115 people columns,
  0 added automatically (unchanged). Read-only comparison of that copy with production's live copy: 0 differences
  of any kind across 2,522 cells (both app edits had been set back to their imported values).

## Access control and its limitations

- The tab exists only on the Admin dashboard, and `SkillsMatrixService` re-checks **on every call**
  that the logged-in employee has an `Admin` login and is not archived (same pattern as
  `ManagerAccessService`).
- **This is application-level access control, not backend-enforced security.** Known limitations,
  recorded for a future, separately approved security phase:
  - Both the app and the notifier connect as `postgres` (member of `rds_superuser`); these
    credentials are in `appsettings.json`, which ships with every ClickOnce install. Anyone with
    that file can bypass all application checks.
  - No table has row-level security; all are owned by `postgres`.
  - A `tracker_app_user` login exists with read/write on the existing tables but is unused. The new
    Skills Matrix tables are **not** granted to it (no permission changes in this phase), which the
    security phase will need to address.
  - The login role is read once at login; dashboards trust it (the Skills Matrix re-checks it).
  - `TrustServerCertificate=true` (no TLS certificate validation), a legacy plaintext-password login
    fallback, and no unique constraint on `Users.Email` were also observed.

## Future work (not in scope)

- Database-enforced permissions (dedicated least-privilege login, grants, possibly RLS).
- Deciding whether the application becomes the authoritative record, then editing with a full audit trail.
- Training-evidence links (certificates; HTSF09 sign-offs) attached to `SkillMatrixCells`.
- Confirmed sheet-to-department mappings; Employee/Manager views.
- Use of the "Skill Matrix required" template design (Current/Target, required counts).
