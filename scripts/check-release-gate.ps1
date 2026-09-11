#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Tag = 'v0.1',
    [switch]$PrintFingerprint
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifest = [IO.File]::ReadAllLines((Join-Path $PSScriptRoot 'publication-allowlist.txt'))
# Cover every distribution input and test, including the installer toolchain and
# release workflow. The validation record itself is deliberately excluded.
$sourceFiles = @($manifest | Where-Object { $_ -match '^(GameGarage/|tests/|benchmarks/|scripts/|packaging/|\.github/workflows/|global\.json$|Directory\.Build\.|README\.md$|LICENSE$|NOTICES\.md$)' } | Sort-Object)
$hashLines = @($sourceFiles | ForEach-Object {
    $sourcePath = Join-Path $repositoryRoot $_
    if ($_ -match '\.(ico|png|jpg)$') { $fileHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant() }
    else {
        # Git's checkout line endings do not invalidate evidence for unchanged source.
        $normalizedText = [IO.File]::ReadAllText($sourcePath).Replace("`r`n", "`n")
        $fileSha = [Security.Cryptography.SHA256]::Create()
        try { $fileHash = ([BitConverter]::ToString($fileSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalizedText)))).Replace('-', '').ToLowerInvariant() } finally { $fileSha.Dispose() }
    }
    "$fileHash  $_"
})
$sha = [Security.Cryptography.SHA256]::Create()
try { $fingerprint = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($hashLines -join "`n") + "`n")))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
if ($PrintFingerprint) { Write-Output $fingerprint; return }
$record = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/release-validation.json') -Raw | ConvertFrom-Json
if ($Tag -ne "v$($record.version)" -or $Tag -ne 'v0.1') { throw 'Tag does not match the prepared beta version.' }
if ($record.sourceSha256 -ne $fingerprint) { throw 'Release evidence is missing or predates a source/build change. Repeat affected validation and update sourceSha256.' }
foreach ($check in @('windows11EnglishSmoke', 'cleanMachineWithoutDotNet', 'keyboardAndDisplayScaling', 'repairConfirmationsAndDisposableMaintenance', 'packageAndSourceAudit', 'installerLifecycleSmoke', 'portableDistributionSmoke')) {
    if ($record.PSObject.Properties.Name -notcontains $check -or $record.$check -isnot [bool] -or $record.$check -ne $true) { throw "Release acceptance remains incomplete: $check" }
}
if ($null -eq $record.baselineMedianMilliseconds -or $null -eq $record.candidateMedianMilliseconds -or $record.baselineMedianMilliseconds -le 0 -or $record.candidateMedianMilliseconds -le 0) { throw 'Positive measured baseline and candidate medians are required.' }
$ratio = [double]$record.candidateMedianMilliseconds / [double]$record.baselineMedianMilliseconds
if ($ratio -gt 1.10) { throw "RAM scan performance gate failed: candidate/baseline = $ratio (maximum 1.10)." }
if ($record.pairedBenchmarkRuns -lt 5 -or $record.benchmarkMemoryMiB -le 0 -or $record.benchmarkThreads -le 0 -or $record.benchmarkPasses -ne 10) { throw 'Record at least five alternating, warmed benchmark pairs with positive equal memory/thread settings and ten updates.' }
if ([string]::IsNullOrWhiteSpace($record.machineAndEvidence) -or $record.machineAndEvidence -match '^Pending') { throw 'Machine configuration and acceptance evidence must be recorded.' }
Write-Host "Release acceptance record passed for $Tag. RAM candidate/baseline: $ratio."
