#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lock = Get-Content -LiteralPath (Join-Path $repositoryRoot 'packaging/windows/nsis-toolchain.json') -Raw | ConvertFrom-Json
$cache = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/toolchain'))
[IO.Directory]::CreateDirectory($cache) | Out-Null
$archivePath = Join-Path $cache $lock.archive
function Test-ArchiveHash {
    (Test-Path -LiteralPath $archivePath -PathType Leaf) -and
        (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $lock.sha256
}
if (-not (Test-ArchiveHash)) {
    $downloaded = $false
    $ProgressPreference = 'SilentlyContinue'
    foreach ($url in $lock.urls) {
        try {
            Write-Host "Fetching NSIS $($lock.version) from its official distribution."
            & (Join-Path $env:SystemRoot 'System32/curl.exe') --fail --location --retry 2 --max-time 45 --silent --show-error --output $archivePath -- $url
            if ($LASTEXITCODE -ne 0) { throw "Compiler download failed (curl exit $LASTEXITCODE)." }
            if (-not (Test-ArchiveHash)) { throw 'Compiler archive checksum mismatch (possibly an HTML download page).' }
            $downloaded = $true
            break
        } catch { Write-Host "NSIS mirror could not provide the pinned archive: $($_.Exception.Message)" }
    }
    if (-not $downloaded) { throw 'Cannot obtain the pinned NSIS archive. Check the network and rerun packaging.' }
}
# Always re-extract verified bytes; do not trust an old extracted compiler cache.
$compilerRoot = [IO.Path]::GetFullPath((Join-Path $cache 'compiler'))
if (-not $compilerRoot.StartsWith($cache + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Compiler cache leaves the intended artifact directory.'
}
if (Test-Path -LiteralPath $compilerRoot) {
    if ((Get-Item -LiteralPath $compilerRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Compiler cache cannot be a link.' }
    Remove-Item -LiteralPath $compilerRoot -Recurse -Force
}
Expand-Archive -LiteralPath $archivePath -DestinationPath $compilerRoot
$compiler = Join-Path $compilerRoot "nsis-$($lock.version)/makensis.exe"
$version = & $compiler /VERSION
if ($LASTEXITCODE -ne 0 -or ($version -join '').Trim() -ne "v$($lock.version)") { throw 'Unexpected NSIS compiler version.' }
Write-Output $compiler
