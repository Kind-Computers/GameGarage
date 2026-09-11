#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repositoryRoot
try {
    $gitRoot = & git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot) -ne $repositoryRoot) {
        throw 'Run this audit inside the independent Game Garage repository.'
    }
    $allowlistPath = Join-Path $PSScriptRoot 'publication-allowlist.txt'
    $allowed = @([IO.File]::ReadAllLines($allowlistPath) | Where-Object { $_ -and -not $_.StartsWith('#') })
    if ($allowed.Count -ne @($allowed | Sort-Object -Unique).Count) { throw 'Publication allowlist contains duplicate paths.' }
    $candidates = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate public source files.' }
    $unexpected = @($candidates | Where-Object { $_ -notin $allowed })
    $missing = @($allowed | Where-Object { $_ -notin $candidates })
    if ($unexpected.Count -or $missing.Count) {
        throw "Publication manifest mismatch. Unexpected: [$($unexpected -join ', ')]. Missing: [$($missing -join ', ')]. Review every path before updating the allowlist."
    }
    $forbiddenProjects = '(?i)(liblisa|gnosworks|guardware|ILGPU|FileCorrupter|HDFragmenter)'
    $forbiddenFiles = '(?i)(^|/)(bin|obj|\.git|\.vs|\.agents|\.codex|node_modules)(/|$)|\.(exe|dll|so|dylib|pdb|zip|7z|pfx|p12|pem|key|user|suo)$|(^|/)\.env($|\.)'
    $secretPatterns = @(
        '-----BEGIN [A-Z ]*PRIVATE KEY-----',
        '[g]hp_[A-Za-z0-9]{36}',
        '[g]ithub_pat_[A-Za-z0-9_]{70,}',
        'A[K]IA[0-9A-Z]{16}',
        '[x]ox[baprs]-[0-9A-Za-z-]{20,}'
    )
    foreach ($relative in $candidates) {
        if ($relative -match $forbiddenProjects -or $relative -match $forbiddenFiles) { throw "Excluded publication path: $relative" }
        $fullPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $relative))
        if (-not $fullPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path leaves repository: $relative" }
        $item = Get-Item -LiteralPath $fullPath -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Publication source must not contain links: $relative" }
        if ($item.Extension -eq '.ico') { continue }
        $content = [IO.File]::ReadAllText($fullPath)
        foreach ($pattern in $secretPatterns) { if ($content -match $pattern) { throw "Potential secret in $relative. Inspect locally; the value is not printed." } }
        if ($item.Extension -in @('.cs', '.csproj', '.sln', '.props', '.targets') -and $content -match $forbiddenProjects) { throw "Excluded dependency or source reference in $relative" }
        if ($item.Extension -in @('.csproj', '.props', '.targets') -and $content -match 'Microsoft\.Management\.Infrastructure') { throw "Unapproved redistributable management dependency in $relative" }
    }
    $stageEntries = @(& git ls-files --stage)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged file modes.' }
    if (@($stageEntries | Where-Object { $_ -match '^(120000|160000) ' }).Count) { throw 'Publication tree contains a symlink or submodule.' }
    $historicalPaths = @(& git -c core.quotepath=false log --all --format= --name-only)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect publication history.' }
    foreach ($relative in $historicalPaths) {
        if ($relative -and ($relative -match $forbiddenProjects -or $relative -match $forbiddenFiles)) { throw "Excluded path appears in Git history: $relative" }
    }
    $historyPatch = (& git log --all --format= --no-ext-diff --no-color -p) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect publication history content.' }
    foreach ($pattern in $secretPatterns) { if ($historyPatch -match $pattern) { throw 'Potential secret in Git history. Inspect locally; the value is not printed.' } }
    Write-Host "Publication audit passed: $($candidates.Count) explicitly allowed source files; no excluded paths or common secret formats in local history."
} finally { Pop-Location }
