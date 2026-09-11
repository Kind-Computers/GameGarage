#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ArtifactsDirectory = 'artifacts',
    [string]$Version = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# This script installs and removes software. It intentionally has no local override.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Distribution lifecycle checks run only on a disposable GitHub-hosted Actions runner.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    -not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess -or
    [Environment]::OSVersion.Version.Build -lt 22000) {
    throw 'Distribution checks require a supported x64 Windows hosted runner (build 22000 or later).'
}
if ([Globalization.CultureInfo]::InstalledUICulture.TwoLetterISOLanguageName -ne 'en') {
    throw 'Distribution checks require English-language Windows.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try { $isAdministrator = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
finally { $identity.Dispose() }
if (-not $isAdministrator) { throw 'Distribution checks require the hosted runner administrator account.' }

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactBoundary = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$artifactRoot = if ([IO.Path]::IsPathRooted($ArtifactsDirectory)) {
    [IO.Path]::GetFullPath($ArtifactsDirectory)
} else { [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ArtifactsDirectory)) }
if ($artifactRoot -ne $artifactBoundary -and
    -not $artifactRoot.StartsWith($artifactBoundary + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ArtifactsDirectory must be the repository artifacts directory or one of its children.'
}
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'Build the distribution artifacts first.' }
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$buildProperties = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'Directory.Build.props'))
    $Version = [string]$buildProperties.Project.PropertyGroup.InformationalVersion
}
if ($Version -notmatch '^\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid distribution version.' }

function Assert-NoReparseAncestors {
    param([string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a reparse-point path: $current"
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($null -eq $parent) { $null } else { $parent.FullName }
    }
}
function Get-ChildPath {
    param([string]$Root, [string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw 'Expected a nonempty relative package path.'
    }
    foreach ($part in $RelativePath.Replace('\', '/').Split('/')) {
        if ($part -eq '' -or $part -eq '.' -or $part -eq '..' -or
            $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
            $part.TrimEnd([char[]]@(' ', '.')) -cne $part) { throw "Unsafe package path: $RelativePath" }
    }
    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $candidate = [IO.Path]::GetFullPath((Join-Path $resolvedRoot $RelativePath))
    if (-not $candidate.StartsWith($resolvedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path leaves its validated root: $RelativePath"
    }
    return $candidate
}
function Assert-NoReparseTree {
    param([string]$Path)
    Assert-NoReparseAncestors $Path
    if (Test-Path -LiteralPath $Path -PathType Container) {
        foreach ($item in Get-ChildItem -LiteralPath $Path -Recurse -Force) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a reparse point inside a test directory: $($item.FullName)"
            }
        }
    }
}
function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-Inventory {
    param([string]$PayloadRoot)
    Assert-NoReparseTree $PayloadRoot
    $manifestPath = Get-ChildPath $PayloadRoot 'FILES-SHA256.txt'
    $entries = [Collections.Generic.List[object]]::new()
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in [IO.File]::ReadAllLines($manifestPath)) {
        if ($line -notmatch '^(?<hash>[0-9a-fA-F]{64})  (?<path>.+)$') { throw 'Malformed payload hash inventory.' }
        $relative = $Matches.path.Replace('\', '/')
        $hash = $Matches.hash.ToLowerInvariant()
        $null = Get-ChildPath $PayloadRoot $relative
        if ($relative -eq 'FILES-SHA256.txt' -or -not $names.Add($relative)) { throw "Duplicate or self-referencing inventory path: $relative" }
        $entries.Add([pscustomobject]@{ Path = $relative; Sha256 = $hash })
    }
    foreach ($required in @('GameGarage.exe', 'GameGarage.dll', 'StabilityTest.exe', 'StabilityTest.dll',
        'GameGarage.runtimeconfig.json', 'StabilityTest.runtimeconfig.json', 'hostfxr.dll', 'coreclr.dll', 'LICENSE')) {
        if (-not $names.Contains($required)) { throw "Inventory omits required payload: $required" }
    }
    return [pscustomobject]@{ Entries = @($entries.ToArray()); ManifestSha256 = Get-Sha256 $manifestPath }
}
function Assert-Payload {
    param([string]$PayloadRoot, [object]$Inventory, [string[]]$AllowedExtras = @())
    Assert-NoReparseTree $PayloadRoot
    if ((Get-Sha256 (Get-ChildPath $PayloadRoot 'FILES-SHA256.txt')) -ne $Inventory.ManifestSha256) {
        throw 'Installed or relocated inventory differs from the verified portable inventory.'
    }
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $null = $expected.Add('FILES-SHA256.txt')
    foreach ($entry in $Inventory.Entries) {
        $path = Get-ChildPath $PayloadRoot $entry.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Sha256 $path) -ne $entry.Sha256) {
            throw "Payload hash mismatch or missing file: $($entry.Path)"
        }
        $null = $expected.Add($entry.Path)
    }
    foreach ($relative in $AllowedExtras) { $null = Get-ChildPath $PayloadRoot $relative; $null = $expected.Add($relative) }
    foreach ($file in Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File -Force) {
        $relative = $file.FullName.Substring($PayloadRoot.Length + 1).Replace('\', '/')
        if (-not $expected.Contains($relative)) { throw "Unexpected payload file: $relative" }
    }
}
function Get-AppRegistrations {
    $found = [Collections.Generic.List[object]]::new()
    foreach ($hive in @([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryHive]::CurrentUser)) {
        foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
            $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
            try {
                $uninstall = $baseKey.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall')
                if ($null -eq $uninstall) { continue }
                try {
                    foreach ($name in $uninstall.GetSubKeyNames()) {
                        $key = $uninstall.OpenSubKey($name)
                        if ($null -eq $key) { continue }
                        try {
                            $displayName = [string]$key.GetValue('DisplayName', '')
                            if ($name -eq 'KindComputers.GameGarage' -or $displayName -eq 'Game Garage') {
                                $values = @{}
                                foreach ($valueName in $key.GetValueNames()) { $values[$valueName] = $key.GetValue($valueName) }
                                $found.Add([pscustomobject]@{ Hive = $hive.ToString(); View = $view.ToString(); Key = $name; Values = $values })
                            }
                        } finally { $key.Dispose() }
                    }
                } finally { $uninstall.Dispose() }
            } finally { $baseKey.Dispose() }
        }
    }
    return $found.ToArray()
}
function Assert-Registration {
    $registrations = @(Get-AppRegistrations)
    if ($registrations.Count -ne 1 -or $registrations[0].Hive -ne 'LocalMachine' -or
        $registrations[0].View -ne 'Registry64' -or $registrations[0].Key -ne 'KindComputers.GameGarage') {
        throw 'Expected exactly one per-machine 64-bit Game Garage uninstall registration.'
    }
    $expectedValues = @{
        DisplayName = 'Game Garage'; Publisher = 'Kind Computers'; DisplayVersion = $Version
        InstallLocation = $installRoot; DisplayIcon = (Join-Path $installRoot 'GameGarage.exe')
        UninstallString = ('"' + $uninstallerPath + '"')
        QuietUninstallString = ('"' + $uninstallerPath + '" /S')
        NoModify = 1; NoRepair = 1
    }
    foreach ($name in $expectedValues.Keys) {
        if (-not $registrations[0].Values.ContainsKey($name) -or
            [string]$registrations[0].Values[$name] -cne [string]$expectedValues[$name]) {
            throw "Unexpected uninstall metadata: $name"
        }
    }
}
function Assert-DesktopSentinel {
    if (-not $desktopSentinelCreated -or -not (Test-Path -LiteralPath $desktopShortcut -PathType Leaf)) {
        throw 'The unrelated common Desktop sentinel is missing.'
    }
    Assert-NoReparseAncestors $desktopShortcut
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($desktopShortcut)) -cne $desktopSentinelBase64) {
        throw 'Installation or uninstall changed the unrelated common Desktop sentinel bytes.'
    }
}
function Assert-Shortcut {
    if (-not (Test-Path -LiteralPath $startMenuShortcut -PathType Leaf)) { throw 'Common Start Menu shortcut is missing.' }
    if ($desktopSentinelCreated) { Assert-DesktopSentinel }
    elseif (Test-Path -LiteralPath $desktopShortcut) { throw 'Default silent installation unexpectedly created a desktop shortcut.' }
    Assert-NoReparseAncestors $startMenuShortcut
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $null
    try {
        $shortcut = $shell.CreateShortcut($startMenuShortcut)
        if ($shortcut.TargetPath -ine (Join-Path $installRoot 'GameGarage.exe') -or
            -not [string]::IsNullOrWhiteSpace($shortcut.Arguments)) { throw 'Start Menu shortcut has an unexpected target or arguments.' }
    } finally {
        if ($null -ne $shortcut) { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
        $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}
function Invoke-SilentLifecycle {
    param([string]$Executable)
    Assert-NoReparseAncestors $Executable
    $process = Start-Process -FilePath $Executable -ArgumentList '/S' -WorkingDirectory $workingDirectory -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit(120000)) { throw 'Silent installer/uninstaller did not exit within two minutes.' }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "Silent installer/uninstaller failed with exit code $($process.ExitCode)." }
    } finally { $process.Dispose() }
}
function Assert-WorkerHelp {
    param([string]$PayloadRoot, [string]$Phase)
    $worker = Get-ChildPath $PayloadRoot 'StabilityTest.exe'
    $stdout = Get-ChildPath $testRoot ($Phase + '-help.stdout.txt')
    $stderr = Get-ChildPath $testRoot ($Phase + '-help.stderr.txt')
    # Exactly --help returns before the worker creates an allocator or scan options.
    $process = Start-Process -FilePath $worker -ArgumentList '--help' -WorkingDirectory $workingDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    try {
        if (-not $process.WaitForExit(30000)) { throw 'Packaged worker help did not exit within thirty seconds.' }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "Packaged worker help failed with exit code $($process.ExitCode)." }
    } finally { $process.Dispose() }
    $help = [IO.File]::ReadAllText($stdout)
    if ($help -notmatch 'Usage: StabilityTest\.exe' -or
        -not [string]::IsNullOrWhiteSpace([IO.File]::ReadAllText($stderr))) { throw 'Packaged worker did not produce clean help output.' }
}
function Expand-PortablePackage {
    param([string]$ZipPath, [string]$Destination)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if (-not $name.StartsWith('GameGarage/', [StringComparison]::Ordinal)) { throw 'Portable ZIP must have exactly one GameGarage/ top-level folder.' }
            if ($name -eq 'GameGarage/') { continue }
            $relative = $name.TrimEnd('/')
            $null = Get-ChildPath $Destination $relative
            if (-not $paths.Add($relative)) { throw "Duplicate portable ZIP path: $relative" }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Portable ZIP contains a symbolic link.' }
        }
        $entryCount = $zip.Entries.Count
    } finally { $zip.Dispose() }
    Assert-NoReparseAncestors $Destination
    [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $Destination)
    return $entryCount
}

Assert-NoReparseAncestors $artifactRoot
$installerPath = Get-ChildPath $artifactRoot "GameGarage-$Version-win-x64-setup.exe"
$portablePath = Get-ChildPath $artifactRoot "GameGarage-$Version-win-x64-portable.zip"
$checksumsPath = Get-ChildPath $artifactRoot 'SHA256SUMS.txt'
$reportPath = Get-ChildPath $artifactRoot 'distribution-validation.json'
$programFilesRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
$installRoot = Get-ChildPath $programFilesRoot 'Game Garage'
$uninstallerPath = Get-ChildPath $installRoot 'uninstall.exe'
$startMenuRoot = Get-ChildPath ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)) 'Game Garage'
$startMenuShortcut = Get-ChildPath $startMenuRoot 'Game Garage.lnk'
$desktopShortcut = Get-ChildPath ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonDesktopDirectory)) 'Game Garage.lnk'
$runId = [Guid]::NewGuid().ToString('N')
$testRoot = Get-ChildPath $artifactRoot ("distribution smoke " + $runId)
$workingDirectory = Get-ChildPath $testRoot 'Unrelated working directory'
$sentinelName = "unrelated-ci-file-$runId.txt"
$sentinelPath = Get-ChildPath $installRoot $sentinelName
$sentinelContent = "This unrelated file belongs to distribution smoke $runId."
$desktopSentinelBytes = [Text.Encoding]::UTF8.GetBytes("Unrelated common Desktop fixture for smoke $runId.")
$desktopSentinelBase64 = [Convert]::ToBase64String($desktopSentinelBytes)
$desktopSentinelCreated = $false
$report = [ordered]@{
    schemaVersion = 1; version = $Version; sourceCommit = $env:GITHUB_SHA
    installerLifecycle = $false; portableLayout = $false
    platform = [ordered]@{ osVersion = [Environment]::OSVersion.Version.ToString(); architecture = 'x64'
        installedUiCulture = [Globalization.CultureInfo]::InstalledUICulture.Name; githubHosted = $true }
    artifactHashes = [ordered]@{}; counts = [ordered]@{ payloadFiles = 0; portableZipEntries = 0; reinstallRegistrations = 0 }
    checks = [ordered]@{}; error = $null; completedAtUtc = $null
}
$testRootCreated = $false
try {
    # A failed run must never claim a fresh-machine test by reusing a prior installation.
    if (@(Get-AppRegistrations).Count -ne 0 -or (Test-Path -LiteralPath $installRoot) -or
        (Test-Path -LiteralPath $startMenuRoot) -or (Test-Path -LiteralPath $desktopShortcut)) {
        throw 'The hosted runner already has a Game Garage installation, registration, or shortcut.'
    }
    Assert-NoReparseAncestors $installRoot
    Assert-NoReparseAncestors $startMenuRoot
    Assert-NoReparseAncestors $desktopShortcut
    $report.checks.freshMachine = $true

    $artifactHashes = @{}
    foreach ($line in [IO.File]::ReadAllLines($checksumsPath)) {
        if ($line -notmatch '^(?<hash>[0-9a-fA-F]{64})  (?<path>.+)$') { throw 'Malformed distribution SHA256SUMS.txt.' }
        $name = $Matches.path
        $expectedHash = $Matches.hash.ToLowerInvariant()
        if ($artifactHashes.ContainsKey($name)) { throw 'Duplicate distribution checksum entry.' }
        $artifactPath = Get-ChildPath $artifactRoot $name
        if ((Get-Sha256 $artifactPath) -ne $expectedHash) { throw "Distribution checksum mismatch: $name" }
        $artifactHashes[$name] = $expectedHash
    }
    foreach ($artifact in @($installerPath, $portablePath)) {
        $name = [IO.Path]::GetFileName($artifact)
        if (-not $artifactHashes.ContainsKey($name)) { throw "Distribution checksum is missing: $name" }
        $report.artifactHashes[$name] = $artifactHashes[$name]
    }
    $report.artifactHashes['SHA256SUMS.txt'] = Get-Sha256 $checksumsPath
    $report.checks.artifactChecksums = $true
    if (Test-Path -LiteralPath $testRoot) { throw 'Fresh test directory unexpectedly exists.' }
    [IO.Directory]::CreateDirectory($workingDirectory) | Out-Null
    $testRootCreated = $true

    $extractionRoot = Get-ChildPath $testRoot 'Portable extracted with spaces'
    $report.counts.portableZipEntries = Expand-PortablePackage $portablePath $extractionRoot
    $portablePayload = Get-ChildPath $extractionRoot 'GameGarage'
    $inventory = Get-Inventory $portablePayload
    $report.counts.payloadFiles = $inventory.Entries.Count
    $report.artifactHashes['FILES-SHA256.txt'] = $inventory.ManifestSha256
    Assert-Payload $portablePayload $inventory
    Assert-WorkerHelp $portablePayload 'portable'
    $report.checks.portableExtractionAndHashes = $true
    $report.checks.portableWorkerHelp = $true

    $relocationParent = Get-ChildPath $testRoot 'Portable relocated with spaces'
    $relocatedPayload = Get-ChildPath $relocationParent 'GameGarage'
    # Verify both absolute move targets remain inside this exact fresh test directory.
    $null = Get-ChildPath $testRoot $portablePayload.Substring($testRoot.Length + 1)
    $null = Get-ChildPath $testRoot $relocatedPayload.Substring($testRoot.Length + 1)
    Assert-NoReparseTree $portablePayload
    Assert-NoReparseAncestors $relocatedPayload
    [IO.Directory]::CreateDirectory($relocationParent) | Out-Null
    Move-Item -LiteralPath $portablePayload -Destination $relocatedPayload
    if (Test-Path -LiteralPath $portablePayload) { throw 'Portable relocation left the old payload path behind.' }
    Assert-Payload $relocatedPayload $inventory
    Assert-WorkerHelp $relocatedPayload 'relocated'
    $report.checks.portableRelocationAndHashes = $true
    $report.checks.relocatedWorkerHelp = $true
    if (@(Get-AppRegistrations).Count -ne 0 -or (Test-Path -LiteralPath $installRoot) -or
        (Test-Path -LiteralPath $startMenuRoot) -or (Test-Path -LiteralPath $desktopShortcut)) {
        throw 'Portable help unexpectedly created an installation or shortcut.'
    }
    $report.portableLayout = $true

    Invoke-SilentLifecycle $installerPath
    Assert-Payload $installRoot $inventory @('uninstall.exe')
    if (-not (Test-Path -LiteralPath $uninstallerPath -PathType Leaf)) { throw 'Installed uninstaller is missing.' }
    Assert-Registration
    Assert-Shortcut
    Assert-WorkerHelp $installRoot 'installed'
    $report.checks.installAndHashes = $true
    $report.checks.uninstallMetadata = $true
    $report.checks.commonStartMenuShortcut = $true
    $report.checks.installedWorkerHelp = $true

    # Default installation did not own the desktop path. A later unrelated file must survive.
    Assert-NoReparseAncestors $desktopShortcut
    $desktopSentinelStream = [IO.File]::Open($desktopShortcut, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $desktopSentinelStream.Write($desktopSentinelBytes, 0, $desktopSentinelBytes.Length) }
    finally { $desktopSentinelStream.Dispose() }
    $desktopSentinelCreated = $true
    Assert-DesktopSentinel
    [IO.File]::WriteAllText($sentinelPath, $sentinelContent, [Text.UTF8Encoding]::new($false))
    Invoke-SilentLifecycle $installerPath
    Assert-Payload $installRoot $inventory @('uninstall.exe', $sentinelName)
    Assert-Registration
    Assert-Shortcut
    if ([IO.File]::ReadAllText($sentinelPath) -cne $sentinelContent) { throw 'Same-version reinstallation changed an unrelated file.' }
    Assert-WorkerHelp $installRoot 'reinstalled'
    $report.counts.reinstallRegistrations = @(Get-AppRegistrations).Count
    $report.checks.sameVersionReinstallAndHashes = $true
    $report.checks.singleRegistrationAfterReinstall = $true
    $report.checks.unrelatedFilePreservedAfterReinstall = $true
    $report.checks.unrelatedDesktopFilePreservedAfterReinstall = $true

    Invoke-SilentLifecycle $uninstallerPath
    # NSIS may hand off to a temporary uninstaller. Wait for its observable cleanup as well.
    $uninstallWait = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-Path -LiteralPath $uninstallerPath) -or (Test-Path -LiteralPath $startMenuShortcut) -or @(Get-AppRegistrations).Count -ne 0) {
        if ($uninstallWait.Elapsed.TotalSeconds -gt 120) { throw 'Uninstaller cleanup did not finish within two minutes.' }
        Start-Sleep -Milliseconds 200
    }
    foreach ($entry in $inventory.Entries) {
        if (Test-Path -LiteralPath (Get-ChildPath $installRoot $entry.Path)) { throw "Uninstall left an owned payload file: $($entry.Path)" }
    }
    if (Test-Path -LiteralPath (Get-ChildPath $installRoot 'FILES-SHA256.txt')) { throw 'Uninstall left the owned inventory.' }
    Assert-DesktopSentinel
    Assert-NoReparseTree $installRoot
    if ([IO.File]::ReadAllText($sentinelPath) -cne $sentinelContent) { throw 'Uninstall removed or changed the unrelated sentinel file.' }
    if (Test-Path -LiteralPath $startMenuRoot) { throw 'Uninstall left the common Start Menu directory.' }
    $remaining = @(Get-ChildItem -LiteralPath $installRoot -Force)
    if ($remaining.Count -ne 1 -or $remaining[0].FullName -ine $sentinelPath) { throw 'Uninstall left unexpected files alongside the unrelated sentinel.' }
    $report.checks.uninstallRemovedOwnedPayload = $true
    $report.checks.uninstallRemovedRegistrationAndShortcuts = $true
    $report.checks.unrelatedFilePreservedAfterUninstall = $true
    $report.checks.unrelatedDesktopFilePreservedAfterUninstall = $true

    # Remove only the exact sentinel this script created, then an empty installation root.
    $validatedSentinel = Get-ChildPath $installRoot $sentinelName
    if ($validatedSentinel -ine $sentinelPath) { throw 'Sentinel cleanup target changed.' }
    Remove-Item -LiteralPath $validatedSentinel -Force
    if (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -eq 0) { Remove-Item -LiteralPath $installRoot -Force }
    $report.installerLifecycle = $true
} catch {
    $report.error = $_.Exception.Message
    throw
} finally {
    $cleanupFailure = $null
    try {
        if ($desktopSentinelCreated) {
            $validatedDesktopSentinel = Get-ChildPath ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonDesktopDirectory)) 'Game Garage.lnk'
            if ($validatedDesktopSentinel -ine $desktopShortcut) { throw 'Desktop sentinel cleanup target changed.' }
            # Remove this fixture only when its exact original bytes are still present.
            if (Test-Path -LiteralPath $validatedDesktopSentinel) {
                Assert-DesktopSentinel
                Remove-Item -LiteralPath $validatedDesktopSentinel -Force
            }
            $report.checks.desktopSentinelCleanup = $true
        }
        if ($testRootCreated) {
            # Never recursively remove Program Files, Start Menu, or any caller-provided directory.
            $validatedTestRoot = Get-ChildPath $artifactRoot ("distribution smoke " + $runId)
            if ($validatedTestRoot -ine $testRoot) { throw 'Test cleanup target changed.' }
            Assert-NoReparseTree $validatedTestRoot
            Remove-Item -LiteralPath $validatedTestRoot -Recurse -Force
            $report.checks.testDirectoryCleanup = $true
        }
    } catch {
        $cleanupFailure = $_
        $report.installerLifecycle = $false
        $report.portableLayout = $false
        $report.checks.testDirectoryCleanup = $false
        $report.error = @($report.error, $_.Exception.Message) -join ' '
    } finally {
        $report.completedAtUtc = [DateTime]::UtcNow.ToString('o')
        [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    }
    if ($null -ne $cleanupFailure) { throw $cleanupFailure }
}
Write-Host 'Distribution checks passed: portable extraction/relocation, install, same-version reinstall, and uninstall. Only worker --help was launched.'
