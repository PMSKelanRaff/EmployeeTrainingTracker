# Skills Matrix (Admin dashboard)

A **read-only** representation, inside the Training Tracker, of approved data from the audited
*Skills Training Matrix* Excel workbook.

## Status

| Phase | Scope | Status |
|---|---|---|
| 1 | Schema (migration 004), read-only service, Admin "Skills Matrix" tab, this document | Built; **migration not yet run** |
| 2a | Import tool dry run + employee reconciliation report (writes nothing to the database) | Not started (separate approval) |
| 2b | Approved load of confirmed data from a verified workbook copy | Not started (separate approval) |
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
  Kept as separate skills per sheet/source row.
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
