<#
.SYNOPSIS
  run-validation.ps1 — fan the artefact-bundle harness across a list of source models,
  migrating each from a SRC server to a DST server via the harness `remote` command
  (pass -LegacyApi to fetch via the REST deserialize API instead of the DuckDB packfile download).

  Native PowerShell port of run-validation.sh (same logic). Runs in Windows PowerShell 5.1
  and PowerShell 7+, with no bash / WSL / Git-Bash dependency.

.DESCRIPTION
  Tokens are read from the environment ONLY and are never written to disk or echoed:
    SPECKLE_SRC_TOKEN  — auth for the SRC (read) server
    SPECKLE_DST_TOKEN  — auth for the DST (write) server

  Server URLs + the DST project/model are configured via env or parameters:
    SRC_SERVER   / -SrcServer   (e.g. https://app.speckle.systems)
    DST_SERVER   / -DstServer   (e.g. http://localhost:3000)
    DST_PROJECT  / -DstProject  destination projectId
    DST_MODEL    / -DstModel     destination modelId
    PARALLEL     / -Parallel    max concurrent harness runs (default 4)

  Source refs are `srcProjectId/srcModelId` tokens, supplied as:
    - positional args:            run-validation.ps1 proj1/modelA proj2/modelB
    - or a file (one per line):   run-validation.ps1 -RefsFile refs.txt

  NOTE: every source model is migrated INTO the single DST project/model given. The DST
  server creates a NEW version per upload, so repeated runs stack versions on that model.
  (Per-source destination mapping is intentionally out of scope — adjust if you need it.)

.EXAMPLE
  $env:SPECKLE_SRC_TOKEN = '...'; $env:SPECKLE_DST_TOKEN = '...'
  .\run-validation.ps1 -SrcServer https://app.speckle.systems -DstServer http://localhost:3000 `
    -DstProject abc123 -DstModel def456 p1/m1 p2/m2
#>
[CmdletBinding()]
param(
  [string]$SrcServer  = $env:SRC_SERVER,
  [string]$DstServer  = $env:DST_SERVER,
  [string]$DstProject = $env:DST_PROJECT,
  [string]$DstModel   = $env:DST_MODEL,
  [int]$Parallel      = $(if ($env:PARALLEL) { [int]$env:PARALLEL } else { 4 }),
  [string]$RefsFile,
  [switch]$LegacyApi,
  [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
  [string[]]$Refs
)

Set-StrictMode -Version Latest

function Write-Err([string]$msg) { [Console]::Error.WriteLine("error: $msg") }

$scriptDir = $PSScriptRoot
# dotnet is native here; a normal Windows path works directly (no cygpath/wslpath dance).
$csproj = Join-Path $scriptDir 'Speckle.Sdk.Artifacts.Harness.csproj'

# ── collect refs (positional + optional file) ────────────────────────────────────────────
$refList = New-Object System.Collections.Generic.List[string]
if ($Refs) {
  foreach ($r in $Refs) { if (-not [string]::IsNullOrWhiteSpace($r)) { $refList.Add($r) } }
}

# ── validate config (tokens stay in env; we only check presence) ─────────────────────────
$err = 0
if ([string]::IsNullOrWhiteSpace($env:SPECKLE_SRC_TOKEN)) { Write-Err 'SPECKLE_SRC_TOKEN not set'; $err = 1 }
if ([string]::IsNullOrWhiteSpace($env:SPECKLE_DST_TOKEN)) { Write-Err 'SPECKLE_DST_TOKEN not set'; $err = 1 }
if ([string]::IsNullOrWhiteSpace($SrcServer))  { Write-Err 'SRC_SERVER not set (env or -SrcServer)';  $err = 1 }
if ([string]::IsNullOrWhiteSpace($DstServer))  { Write-Err 'DST_SERVER not set (env or -DstServer)';  $err = 1 }
if ([string]::IsNullOrWhiteSpace($DstProject)) { Write-Err 'DST_PROJECT not set (env or -DstProject)'; $err = 1 }
if ([string]::IsNullOrWhiteSpace($DstModel))   { Write-Err 'DST_MODEL not set (env or -DstModel)';    $err = 1 }
if ($err -ne 0) { exit 2 }

if ($RefsFile) {
  foreach ($line in Get-Content -LiteralPath $RefsFile) {
    $l = ($line -replace '#.*$', '').Trim()   # strip trailing comments + whitespace
    if ($l) { $refList.Add($l) }
  }
}

if ($refList.Count -eq 0) {
  Write-Err "no source refs given (positional 'proj/model' tokens or -RefsFile <file>)"
  exit 2
}

# Build once up-front so parallel workers don't race the ILRepack target.
Write-Host 'Building harness (Release) ...'
& dotnet build -c Release $csproj 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
  Write-Err "build failed; run 'dotnet build -c Release `"$csproj`"' to see why"
  exit 1
}

# Per-ref worker (mirrors run_one in the bash version). Runs in a background job so several
# refs migrate concurrently; tokens are inherited from the parent process environment.
$runOne = {
  param($ref, $csproj, $srcServer, $dstServer, $dstProject, $dstModel, $resultsDir, $legacyApi)

  $srcProject = ($ref -split '/', 2)[0]
  $srcModel   = ($ref -split '/')[-1]
  $safe    = $ref -replace '/', '_'
  $logf    = Join-Path $resultsDir "$safe.log"
  $statusf = Join-Path $resultsDir "$safe.status"

  if ([string]::IsNullOrEmpty($srcProject) -or [string]::IsNullOrEmpty($srcModel) -or ($ref -notlike '*/*')) {
    $line = "FAIL  $ref  (malformed ref, expected projectId/modelId)"
    Set-Content -LiteralPath $statusf -Value $line
    Write-Output $line
    return
  }

  $harnessArgs = @(
    'remote', $srcServer, $srcProject, $srcModel,
    '--dest-server', $dstServer, '--dest-project', $dstProject, '--dest-model', $dstModel
  )
  if ($legacyApi) { $harnessArgs += '--legacy-api' }

  & dotnet run -c Release --no-build --project $csproj -- $harnessArgs `
      2>&1 | Out-File -LiteralPath $logf -Encoding utf8

  if ($LASTEXITCODE -eq 0) {
    $m = Select-String -LiteralPath $logf -Pattern 'versionId=([A-Za-z0-9]+)' | Select-Object -First 1
    $vid = if ($m) { $m.Matches[0].Groups[1].Value } else { '?' }
    $line = "PASS  $ref  -> version $vid"
    Set-Content -LiteralPath $statusf -Value $line
    Write-Output $line
  }
  else {
    $line = "FAIL  $ref  (see log: $logf)"
    Set-Content -LiteralPath $statusf -Value $line
    Write-Output $line
    Get-Content -LiteralPath $logf -Tail 5 | ForEach-Object { Write-Output "      $_" }
  }
}

$resultsDir = Join-Path ([System.IO.Path]::GetTempPath()) ("speckle-validation-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resultsDir | Out-Null

$exitCode = 1
try {
  Write-Host "Running $($refList.Count) model(s) with parallelism $Parallel ..."

  $jobs = New-Object System.Collections.Generic.List[object]
  foreach ($ref in $refList) {
    # Throttle: keep at most $Parallel workers running at once (the xargs -P equivalent).
    while (@($jobs | Where-Object { $_.State -eq 'Running' }).Count -ge $Parallel) {
      Start-Sleep -Milliseconds 200
    }
    $j = Start-Job -ScriptBlock $runOne -ArgumentList `
      $ref, $csproj, $SrcServer, $DstServer, $DstProject, $DstModel, $resultsDir, $LegacyApi.IsPresent
    $jobs.Add($j)
  }

  Wait-Job -Job $jobs | Out-Null
  foreach ($j in $jobs) { Receive-Job -Job $j | ForEach-Object { Write-Host $_ } }
  $jobs | Remove-Job -Force

  # ── summary ─────────────────────────────────────────────────────────────────────────
  Write-Host ''
  Write-Host '-------- SUMMARY --------'
  $pass = 0; $fail = 0
  foreach ($s in (Get-ChildItem -LiteralPath $resultsDir -Filter '*.status' -ErrorAction SilentlyContinue)) {
    $line = (Get-Content -LiteralPath $s.FullName -Raw).TrimEnd("`r", "`n")
    Write-Host $line
    if ($line -like 'PASS*') { $pass++ } else { $fail++ }
  }
  Write-Host '-------------------------'
  Write-Host "PASS=$pass  FAIL=$fail"
  $exitCode = if ($fail -eq 0) { 0 } else { 1 }
}
finally {
  if (Test-Path -LiteralPath $resultsDir) { Remove-Item -LiteralPath $resultsDir -Recurse -Force }
}

exit $exitCode
