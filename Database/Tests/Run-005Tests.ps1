<#
  Tests migration 005 (Skills Matrix live copy) on a DISPOSABLE local PostgreSQL instance.

  Creates a brand-new cluster in a temporary folder, listening on localhost only (port 54329 by default),
  runs every scenario against it, then stops it and deletes the folder. It never connects anywhere else:
  every psql / pg_dump call passes -h localhost -p <port> explicitly and PG* environment variables are cleared.

  Usage:  powershell -ExecutionPolicy Bypass -File Database\Tests\Run-005Tests.ps1 [-PgBin <folder>] [-Port 54329]
#>
param(
    [string]$PgBin = 'C:\Program Files\PostgreSQL\18\bin',
    [int]$Port = 54329,
    [switch]$SkipAppTests,   # skip scenario H (builds and runs the C# live-copy service tests)
    [string]$WorkDir = (Join-Path ([IO.Path]::GetTempPath()) ("skillsmatrix005_" + [Guid]::NewGuid().ToString('N').Substring(0, 8)))
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$mig = Join-Path (Split-Path -Parent $here) 'Migrations'
$psql = Join-Path $PgBin 'psql.exe'; $pgdump = Join-Path $PgBin 'pg_dump.exe'
$pgctl = Join-Path $PgBin 'pg_ctl.exe'; $initdb = Join-Path $PgBin 'initdb.exe'

# Never inherit a connection target from the environment
foreach ($v in 'PGHOST','PGHOSTADDR','PGPORT','PGDATABASE','PGUSER','PGPASSWORD','PGSERVICE','PGSSLMODE','PGOPTIONS','PGPASSFILE') {
    Remove-Item "Env:$v" -ErrorAction SilentlyContinue
}
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { throw "Port $Port is already in use; pick another with -Port." }

$checks = New-Object System.Collections.Generic.List[object]
function Check([string]$name, [bool]$cond, [string]$detail = '') {
    $checks.Add([pscustomobject]@{ Name = $name; Passed = $cond; Detail = $detail })
    '{0}  {1}{2}' -f ($(if ($cond) { 'PASS' } else { 'FAIL' })), $name, $(if (-not $cond -and $detail) { "  -- $detail" } else { '' })
}
# Runs a SQL file (or -c command); returns @{ Code; Out }
function Sql([string]$db, [string]$file = $null, [string]$cmd = $null) {
    $a = @('-h', 'localhost', '-p', $Port, '-U', 'postgres', '-d', $db, '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-At')
    if ($file) { $a += @('-f', $file) } else { $a += @('-c', $cmd) }
    $ErrorActionPreference = 'Continue'   # expected errors on stderr must not stop the runner (PowerShell 5.1)
    $out = & $psql @a 2>&1 | Out-String
    @{ Code = $LASTEXITCODE; Out = $out }
}
function Scalar([string]$db, [string]$cmd) { (Sql $db -cmd $cmd).Out.Trim() }
function NewDb([string]$db, [switch]$With004, [switch]$WithImport) {
    $r = Sql 'postgres' -cmd "CREATE DATABASE $db"; if ($r.Code) { throw $r.Out }
    foreach ($f in @((Join-Path $here 'standin_schema.sql')) + $(if ($With004) { @(Join-Path $mig '004_skills_matrix.sql') } else { @() }) +
                   $(if ($WithImport) { @(Join-Path $here 'fixture_import.sql') } else { @() })) {
        $r = Sql $db -file $f; if ($r.Code) { throw "setup $f failed: $($r.Out)" }
    }
}
function Dump([string]$db, [string]$mode) {
    $flag = if ($mode -eq 'schema') { '-s' } else { '-a' }
    $ErrorActionPreference = 'Continue'
    $out =& $pgdump -h localhost -p $Port -U postgres -d $db $flag 2>&1 | Out-String
    if ($LASTEXITCODE) { throw "pg_dump failed: $out" }
    # pg_dump 17.6+/18 writes a random \restrict key per run; it is not part of the schema
    ($out -split "`r?`n" | Where-Object { $_ -notmatch '^\\(un)?restrict ' }) -join "`n"
}
$m005 = Join-Path $mig '005_skills_matrix_live.sql'
$r005 = Join-Path $mig '005_skills_matrix_live_rollback.sql'
$r004 = Join-Path $mig '004_skills_matrix_rollback.sql'
# pg_ctl start leaves the server running with pg_ctl's output handles: piping it (| Out-Null) would wait for the
# server to exit. Run it as a plain process, output to a file, and wait for pg_ctl itself only.
function PgCtl([string[]]$arguments) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $pgctl
    $psi.Arguments = ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.WaitForExit()
    $p.ExitCode
}
$liveCount ="SELECT COUNT(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relkind = 'r' AND relname LIKE 'skillmatrixlive%'"

New-Item -ItemType Directory -Path $WorkDir | Out-Null
$data = Join-Path $WorkDir 'data'
try {
    & $initdb -D $data -U postgres -A trust -E UTF8 --no-locale 2>&1 | Out-Null
    if ($LASTEXITCODE) { throw 'initdb failed' }
    if (PgCtl @('-D', $data, '-o', "-p $Port -c listen_addresses=localhost", '-l', (Join-Path $WorkDir 'server.log'), '-w', 'start')) { throw 'server did not start' }
    "Disposable PostgreSQL: " + (Scalar 'postgres' 'SELECT version()')
    ''

    # A. Main suite: 004 + import 1 -> 005 (seeds) -> SQL tests
    NewDb 'a' -With004 -WithImport
    $r = Sql 'a' -file $m005
    Check 'A1 migration 005 applies on 004 with an import' ($r.Code -eq 0) $r.Out
    $t = Sql 'a' -file (Join-Path $here 'tests_005.sql')
    $t.Out.Trim()
    $m = [regex]::Match($t.Out, 'RESULT (\d+)/(\d+)')
    Check 'A2 SQL test suite ran and every test passed' ($t.Code -eq 0 -and $m.Success -and $m.Groups[1].Value -eq $m.Groups[2].Value) "exit $($t.Code)"
    $r = Sql 'a' -file $m005
    Check 'A3 running 005 a second time fails (objects exist) and changes nothing' ($r.Code -ne 0 -and (Scalar 'a' "SELECT COUNT(*) FROM public.SkillMatrixLiveChanges WHERE Action = 'Reseed'") -eq '3') $r.Out

    # B. Rollback restores the exact pre-migration database (schema AND data), twice
    NewDb 'b' -With004 -WithImport
    $schema0 = Dump 'b' 'schema'; $data0 = Dump 'b' 'data'
    foreach ($cycle in 1, 2) {
        $r = Sql 'b' -file $m005; if ($r.Code) { throw "B: 005 failed: $($r.Out)" }
        # some edits and a change-log row, so the rollback has real data to remove
        $r = Sql 'b' -cmd "UPDATE public.SkillMatrixLiveCells SET Level = 1, UpdatedAt = now(), UpdatedByName = 'x' WHERE Status = 'Level'; INSERT INTO public.SkillMatrixLiveChanges (ChangedByName, BasedOnImportID, Action) VALUES ('x', 1, 'SetCell')"
        if ($r.Code) { throw "B: edits failed: $($r.Out)" }
        $r = Sql 'b' -file $r005
        Check "B$cycle-1 rollback succeeds (cycle $cycle)" ($r.Code -eq 0) $r.Out
        Check "B$cycle-2 schema identical to before 005 (cycle $cycle)" ((Dump 'b' 'schema') -eq $schema0)
        Check "B$cycle-3 data identical to before 005 - snapshots and Employees untouched (cycle $cycle)" ((Dump 'b' 'data') -eq $data0)
    }
    $r = Sql 'b' -file $r005
    Check 'B4 rollback again when nothing is installed changes nothing' ($r.Code -eq 0 -and (Dump 'b' 'schema') -eq $schema0) $r.Out

    # C. 005 requires 004
    NewDb 'c'
    $schemaC = Dump 'c' 'schema'
    $r = Sql 'c' -file $m005
    Check 'C1 005 without 004 fails with a clear message and creates nothing' ($r.Code -ne 0 -and $r.Out -match 'Migration 004' -and (Dump 'c' 'schema') -eq $schemaC) $r.Out

    # D. Rollback refuses an object without the marker, and then drops nothing
    NewDb 'd' -With004 -WithImport
    if ((Sql 'd' -file $m005).Code) { throw 'D: 005 failed' }
    Sql 'd' -cmd "COMMENT ON TABLE public.SkillMatrixLiveCells IS 'someone else''s table'" | Out-Null
    $r = Sql 'd' -file $r005
    Check 'D1 rollback refuses a table without the 005 marker and drops nothing' ($r.Code -ne 0 -and $r.Out -match 'marker missing' -and (Scalar 'd' $liveCount) -eq '7') $r.Out

    # E. 004's rollback cannot remove 004 while 005 is installed (no CASCADE), and drops nothing
    NewDb 'e' -With004 -WithImport
    if ((Sql 'e' -file $m005).Code) { throw 'E: 005 failed' }
    $schemaE = Dump 'e' 'schema'
    $r = Sql 'e' -file $r004
    Check 'E1 004 rollback fails while 005 is installed, nothing dropped' ($r.Code -ne 0 -and (Dump 'e' 'schema') -eq $schemaE) $r.Out
    $r1 = Sql 'e' -file $r005; $r2 = Sql 'e' -file $r004
    Check 'E2 rolling back 005 then 004 works' ($r1.Code -eq 0 -and $r2.Code -eq 0 -and (Scalar 'e' "SELECT COUNT(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relname LIKE 'skillmatrix%'") -eq '0') ($r1.Out + $r2.Out)

    # F. 004 installed but no import yet: 005 installs, live copy empty, nothing logged
    NewDb 'f' -With004
    $r = Sql 'f' -file $m005
    Check 'F1 005 with no import: installs, no live rows, no state, no log' ($r.Code -eq 0 -and
        (Scalar 'f' 'SELECT (SELECT COUNT(*) FROM public.SkillMatrixLiveState) + (SELECT COUNT(*) FROM public.SkillMatrixLiveCells) + (SELECT COUNT(*) FROM public.SkillMatrixLiveChanges)') -eq '0') $r.Out

    # G. A failure part-way through 005 leaves nothing (all-or-nothing)
    NewDb 'g' -With004 -WithImport
    $schemaG = Dump 'g' 'schema'
    $broken = Join-Path $WorkDir '005_broken.sql'
    # (written without a byte-order mark, so the only error is the deliberate one)
    [IO.File]::WriteAllText($broken, ((Get-Content $m005 -Raw) -replace '(-- 9\. Triggers)', "SELECT 1/0;`n`$1"), (New-Object Text.UTF8Encoding($false)))
    $r = Sql 'g' -file $broken
    Check 'G1 an error part-way (after the tables are created) leaves the database unchanged' ($r.Code -ne 0 -and $r.Out -match 'division by zero' -and (Dump 'g' 'schema') -eq $schemaG) $r.Out

    # K-N. Migration 006 (uniform keys, team names) on top of 005
    $m006 = Join-Path $mig '006_skills_matrix_live_keys.sql'
    $r006 = Join-Path $mig '006_skills_matrix_live_keys_rollback.sql'
    NewDb 'k' -With004 -WithImport
    if ((Sql 'k' -file $m005).Code) { throw 'K: 005 failed' }
    # an edit made before 006 must survive it
    Sql 'k' -cmd "UPDATE public.SkillMatrixLiveCells SET Level = 1, UpdatedAt = now(), UpdatedByName = 'before 006' WHERE SourceCellID = (SELECT CellID FROM public.SkillMatrixCells WHERE SourceCell = 'Lab!C5')" | Out-Null
    $schemaK = Dump 'k' 'schema'; $dataK = Dump 'k' 'data'
    $r = Sql 'k' -file $m006
    Check 'K1 migration 006 applies on 005' ($r.Code -eq 0) $r.Out
    $t = Sql 'k' -file (Join-Path $here 'tests_006.sql')
    $t.Out.Trim()
    $m = [regex]::Match($t.Out, 'RESULT (\d+)/(\d+)')
    Check 'K2 006 SQL test suite ran and every test passed' ($t.Code -eq 0 -and $m.Success -and $m.Groups[1].Value -eq $m.Groups[2].Value) "exit $($t.Code)"

    NewDb 'l' -With004 -WithImport
    if ((Sql 'l' -file $m005).Code) { throw 'L: 005 failed' }
    $schemaL = Dump 'l' 'schema'; $dataL = Dump 'l' 'data'
    foreach ($cycle in 1, 2) {
        if ((Sql 'l' -file $m006).Code) { throw "L: 006 failed (cycle $cycle)" }
        $r = Sql 'l' -file $r006
        Check "L$cycle-1 006 rollback succeeds (cycle $cycle)" ($r.Code -eq 0) $r.Out
        Check "L$cycle-2 schema identical to before 006, incl. 005's seed function (cycle $cycle)" ((Dump 'l' 'schema') -eq $schemaL)
        Check "L$cycle-3 data identical to before 006 (cycle $cycle)" ((Dump 'l' 'data') -eq $dataL)
    }

    NewDb 'm' -With004 -WithImport
    $schemaM = Dump 'm' 'schema'
    $r = Sql 'm' -file $m006
    Check 'M1 006 without 005 fails with a clear message and creates nothing' ($r.Code -ne 0 -and $r.Out -match 'Migration 005' -and (Dump 'm' 'schema') -eq $schemaM) $r.Out
    if ((Sql 'm' -file $m005).Code -or (Sql 'm' -file $m006).Code) { throw 'M: 005/006 failed' }
    $schemaM2 = Dump 'm' 'schema'
    $r = Sql 'm' -file $r005
    Check 'M2 005 rollback fails while 006 is installed, nothing dropped' ($r.Code -ne 0 -and (Dump 'm' 'schema') -eq $schemaM2) $r.Out
    $a = Sql 'm' -file $r006; $b2 = Sql 'm' -file $r005; $c = Sql 'm' -file $r004
    Check 'M3 rolling back 006, 005, 004 in order leaves no Skills Matrix objects' ($a.Code -eq 0 -and $b2.Code -eq 0 -and $c.Code -eq 0 -and
        (Scalar 'm' "SELECT COUNT(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relname LIKE 'skillmatrix%'") -eq '0' -and
        (Scalar 'm' "SELECT COUNT(*) FROM pg_proc WHERE pronamespace = 'public'::regnamespace AND proname LIKE 'skillmatrix%'") -eq '0') ($a.Out + $b2.Out + $c.Out)

    NewDb 'n' -With004 -WithImport
    if ((Sql 'n' -file $m005).Code -or (Sql 'n' -file $m006).Code) { throw 'N: 005/006 failed' }
    Sql 'n' -cmd "COMMENT ON TABLE public.SkillMatrixLiveLevels IS 'someone else''s table'" | Out-Null
    $r = Sql 'n' -file $r006
    Check 'N1 006 rollback refuses a table without the 006 marker and drops nothing' ($r.Code -ne 0 -and $r.Out -match 'marker missing' -and
        (Scalar 'n' "SELECT COUNT(*) FROM pg_class WHERE relnamespace = 'public'::regnamespace AND relname IN ('skillmatrixlivelevels','skillmatrixliveteamlabels')") -eq '2') $r.Out

    # P-S. Migration 007 (team managers from the workbook headings) on top of 006
    $m007 = Join-Path $mig '007_skills_matrix_team_managers.sql'
    $r007 = Join-Path $mig '007_skills_matrix_team_managers_rollback.sql'
    # stand-in people for the seed: one exact match, one wrong name, one archived
    $seedPeople = "INSERT INTO public.Employees (EmployeeID, FullName, IsArchived) VALUES (111, 'Dermot Leonard', FALSE), (23, 'Pat Burns', FALSE), (75, 'Darragh O''Dea', TRUE)"
    NewDb 'p' -With004 -WithImport
    if ((Sql 'p' -file $m005).Code -or (Sql 'p' -file $m006).Code) { throw 'P: 005/006 failed' }
    if ((Sql 'p' -cmd $seedPeople).Code) { throw 'P: stand-in people failed' }
    $r = Sql 'p' -file $m007
    Check 'P1 migration 007 applies on 006' ($r.Code -eq 0) $r.Out
    $t = Sql 'p' -file (Join-Path $here 'tests_007.sql')
    $t.Out.Trim()
    $m = [regex]::Match($t.Out, 'RESULT (\d+)/(\d+)')
    Check 'P2 007 SQL test suite ran and every test passed' ($t.Code -eq 0 -and $m.Success -and $m.Groups[1].Value -eq $m.Groups[2].Value) "exit $($t.Code)"
    $r = Sql 'p' -file $m007
    Check 'P3 running 007 a second time fails (table exists) and changes nothing' ($r.Code -ne 0 -and (Scalar 'p' 'SELECT COUNT(*) FROM public.SkillMatrixTeamManagers') -eq '1') $r.Out

    NewDb 'q' -With004 -WithImport
    if ((Sql 'q' -file $m005).Code -or (Sql 'q' -file $m006).Code -or (Sql 'q' -cmd $seedPeople).Code) { throw 'Q: setup failed' }
    $schemaQ = Dump 'q' 'schema'; $dataQ = Dump 'q' 'data'
    foreach ($cycle in 1, 2) {
        if ((Sql 'q' -file $m007).Code) { throw "Q: 007 failed (cycle $cycle)" }
        Sql 'q' -cmd "INSERT INTO public.SkillMatrixTeamManagers (HeadingText, EmployeeID, AddedBy) VALUES ('Senior', 3, 'x')" | Out-Null
        $r = Sql 'q' -file $r007
        Check "Q$cycle-1 007 rollback succeeds (cycle $cycle)" ($r.Code -eq 0) $r.Out
        Check "Q$cycle-2 schema identical to before 007 (cycle $cycle)" ((Dump 'q' 'schema') -eq $schemaQ)
        Check "Q$cycle-3 data identical to before 007 - Employees untouched (cycle $cycle)" ((Dump 'q' 'data') -eq $dataQ)
    }
    $r = Sql 'q' -file $r007
    Check 'Q4 007 rollback again when nothing is installed changes nothing' ($r.Code -eq 0 -and (Dump 'q' 'schema') -eq $schemaQ) $r.Out

    NewDb 'r' -With004 -WithImport
    $schemaR = Dump 'r' 'schema'
    $r = Sql 'r' -file $m007
    Check 'R1 007 without 005 fails with a clear message and creates nothing' ($r.Code -ne 0 -and $r.Out -match 'Migration 005' -and (Dump 'r' 'schema') -eq $schemaR) $r.Out

    NewDb 's' -With004 -WithImport
    if ((Sql 's' -file $m005).Code -or (Sql 's' -file $m007).Code) { throw 'S: 005/007 failed' }
    Sql 's' -cmd "COMMENT ON TABLE public.SkillMatrixTeamManagers IS 'someone else''s table'" | Out-Null
    $r = Sql 's' -file $r007
    Check 'S1 007 rollback refuses a table without the 007 marker and drops nothing' ($r.Code -ne 0 -and $r.Out -match 'marker missing' -and
        (Scalar 's' "SELECT to_regclass('public.skillmatrixteammanagers') IS NOT NULL") -eq 't') $r.Out

    # T-W. Migration 008 (certificate soft delete) - independent of the Skills Matrix migrations
    $m008 = Join-Path $mig '008_certificate_soft_delete.sql'
    $r008 = Join-Path $mig '008_certificate_soft_delete_rollback.sql'
    NewDb 't'
    $r = Sql 't' -file $m008
    Check 'T1 migration 008 applies' ($r.Code -eq 0) $r.Out
    $t = Sql 't' -file (Join-Path $here 'tests_008.sql')
    $t.Out.Trim()
    $m = [regex]::Match($t.Out, 'RESULT (\d+)/(\d+)')
    Check 'T2 008 SQL test suite ran and every test passed' ($t.Code -eq 0 -and $m.Success -and $m.Groups[1].Value -eq $m.Groups[2].Value) "exit $($t.Code)"
    $r = Sql 't' -file $m008
    Check 'T3 running 008 a second time fails with a clear message and changes nothing' ($r.Code -ne 0 -and $r.Out -match 'already') $r.Out

    NewDb 'u'
    $schemaU = Dump 'u' 'schema'; $dataU = Dump 'u' 'data'
    foreach ($cycle in 1, 2) {
        if ((Sql 'u' -file $m008).Code) { throw "U: 008 failed (cycle $cycle)" }
        $r = Sql 'u' -file $r008
        Check "U$cycle-1 008 rollback succeeds (cycle $cycle)" ($r.Code -eq 0) $r.Out
        Check "U$cycle-2 schema identical to before 008 (cycle $cycle)" ((Dump 'u' 'schema') -eq $schemaU)
        Check "U$cycle-3 data identical to before 008 (cycle $cycle)" ((Dump 'u' 'data') -eq $dataU)
    }
    if ((Sql 'u' -file $m008).Code) { throw 'U: 008 failed' }
    Sql 'u' -cmd "UPDATE public.TrainingCertificates SET DeletedAt = now(), DeletedReason = 'DeletedDirectly', IsMarkedForDeletion = TRUE WHERE CertificateID = 4" | Out-Null
    $schemaU2 = Dump 'u' 'schema'; $dataU2 = Dump 'u' 'data'
    $r = Sql 'u' -file $r008
    Check 'U4 008 rollback refuses while a certificate is soft-deleted, and changes nothing' ($r.Code -ne 0 -and $r.Out -match 'soft-deleted' -and
        (Dump 'u' 'schema') -eq $schemaU2 -and (Dump 'u' 'data') -eq $dataU2) $r.Out

    NewDb 'w'
    if ((Sql 'w' -file $m008).Code) { throw 'W: 008 failed' }
    Sql 'w' -cmd "COMMENT ON COLUMN public.TrainingCertificates.DeletedByName IS 'someone else''s column'" | Out-Null
    $r = Sql 'w' -file $r008
    Check 'W1 008 rollback refuses a column without the 008 marker and drops nothing' ($r.Code -ne 0 -and $r.Out -match 'marker missing' -and
        (Scalar 'w' "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'trainingcertificates' AND column_name LIKE 'deleted%'") -eq '4') $r.Out

    # X-Z. Migration 009 (Skills Matrix cell evidence) - needs 005 (live copy) and 008 (certificate soft delete)
    $m009 = Join-Path $mig '009_skills_matrix_cell_evidence.sql'
    $r009 = Join-Path $mig '009_skills_matrix_cell_evidence_rollback.sql'
    NewDb 'x' -With004 -WithImport
    if ((Sql 'x' -file $m005).Code -or (Sql 'x' -file $m008).Code) { throw 'X: 005/008 failed' }
    $r = Sql 'x' -file $m009
    Check 'X1 migration 009 applies on 005 + 008' ($r.Code -eq 0) $r.Out
    $t = Sql 'x' -file (Join-Path $here 'tests_009.sql')
    $t.Out.Trim()
    $m = [regex]::Match($t.Out, 'RESULT (\d+)/(\d+)')
    Check 'X2 009 SQL test suite ran and every test passed' ($t.Code -eq 0 -and $m.Success -and $m.Groups[1].Value -eq $m.Groups[2].Value) "exit $($t.Code)"
    $r = Sql 'x' -file $m009
    Check 'X3 running 009 a second time fails (table exists) and changes nothing' ($r.Code -ne 0 -and (Scalar 'x' 'SELECT COUNT(*) FROM public.SkillMatrixCellEvidence') -eq '2') $r.Out
    $schemaX = Dump 'x' 'schema'; $dataX = Dump 'x' 'data'
    $r = Sql 'x' -file $r009
    Check 'X4 009 rollback refuses while links exist, and changes nothing' ($r.Code -ne 0 -and $r.Out -match 'evidence link' -and
        (Dump 'x' 'schema') -eq $schemaX -and (Dump 'x' 'data') -eq $dataX) $r.Out

    NewDb 'y' -With004 -WithImport
    if ((Sql 'y' -file $m005).Code -or (Sql 'y' -file $m008).Code) { throw 'Y: 005/008 failed' }
    $schemaY = Dump 'y' 'schema'; $dataY = Dump 'y' 'data'
    foreach ($cycle in 1, 2) {
        if ((Sql 'y' -file $m009).Code) { throw "Y: 009 failed (cycle $cycle)" }
        $r = Sql 'y' -file $r009
        Check "Y$cycle-1 009 rollback succeeds (cycle $cycle)" ($r.Code -eq 0) $r.Out
        Check "Y$cycle-2 schema identical to before 009 (cycle $cycle)" ((Dump 'y' 'schema') -eq $schemaY)
        Check "Y$cycle-3 data identical to before 009 (cycle $cycle)" ((Dump 'y' 'data') -eq $dataY)
    }

    NewDb 'z' -With004 -WithImport
    $schemaZ = Dump 'z' 'schema'
    $r = Sql 'z' -file $m009
    Check 'Z1 009 without 005 fails with a clear message and creates nothing' ($r.Code -ne 0 -and $r.Out -match 'Migration 005' -and (Dump 'z' 'schema') -eq $schemaZ) $r.Out
    if ((Sql 'z' -file $m005).Code) { throw 'Z: 005 failed' }
    $schemaZ2 = Dump 'z' 'schema'
    $r = Sql 'z' -file $m009
    Check 'Z2 009 without 008 fails with a clear message and creates nothing' ($r.Code -ne 0 -and $r.Out -match 'Migration 008' -and (Dump 'z' 'schema') -eq $schemaZ2) $r.Out
    if ((Sql 'z' -file $m008).Code -or (Sql 'z' -file $m009).Code) { throw 'Z: 008/009 failed' }
    Sql 'z' -cmd "COMMENT ON TABLE public.SkillMatrixCellEvidence IS 'someone else''s table'" | Out-Null
    $r = Sql 'z' -file $r009
    Check 'Z3 009 rollback refuses a table without the 009 marker and drops nothing' ($r.Code -ne 0 -and $r.Out -match 'marker missing' -and
        (Scalar 'z' "SELECT to_regclass('public.skillmatrixcellevidence') IS NOT NULL") -eq 't') $r.Out

    # H. The application's live-copy service (C#) against this database: reads, saves, conflicts, permissions
    if (-not $SkipAppTests) {
        NewDb 'live' -With004 -WithImport
        if ((Sql 'live' -file $m005).Code) { throw 'H: 005 failed' }
        if ((Sql 'live' -file $m006).Code) { throw 'H: 006 failed' }
        if ((Sql 'live' -file $m007).Code) { throw 'H: 007 failed' }
        if ((Sql 'live' -file $m008).Code) { throw 'H: 008 failed' }
        if ((Sql 'live' -file $m009).Code) { throw 'H: 009 failed' }
        Sql 'live' -cmd 'CREATE TABLE public.disposable_test_sentinel (x INT)' | Out-Null
        $repo = Split-Path -Parent (Split-Path -Parent $here)
        $bin = Join-Path $WorkDir 'testbin'
        $ErrorActionPreference = 'Continue'
        $b = & dotnet build (Join-Path $repo 'Tests\SkillsMatrixTests\SkillsMatrixTests.csproj') -c Debug -nologo -v q -o $bin 2>&1 | Out-String
        $buildCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        Check 'H0 test program builds' ($buildCode -eq 0) $b
        if ($buildCode -eq 0) {
            $ErrorActionPreference = 'Continue'
            $out = & (Join-Path $bin 'SkillsMatrixTests.exe') --live-test "Host=localhost;Port=$Port;Database=live;Username=postgres" 2>&1 | Out-String
            $liveCode = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            $out.Trim()
            Check 'H1 live-copy service tests (C#) all passed' ($liveCode -eq 0 -and $out -match 'LIVE \d+ passed, 0 failed') "exit $liveCode"
        }
    }
}
finally {
    if (Test-Path (Join-Path $data 'postmaster.pid')) { PgCtl @('-D', $data, '-m', 'fast', '-w', 'stop') | Out-Null }
    Remove-Item -Recurse -Force $WorkDir -ErrorAction SilentlyContinue
}

''
$failed = @($checks | Where-Object { -not $_.Passed })
"RUNNER {0}/{1} scenario checks passed" -f ($checks.Count - $failed.Count), $checks.Count
if ($failed.Count) { exit 1 }
