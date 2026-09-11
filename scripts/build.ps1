#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Package,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Build the Windows beta on Windows.' }
[xml]$buildMetadata = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'Directory.Build.props'))
$version = [string]$buildMetadata.Project.PropertyGroup.InformationalVersion
$fileVersion = [string]$buildMetadata.Project.PropertyGroup.FileVersion
if ($version -notmatch '^\d+\.\d+(\.\d+)?(-[A-Za-z0-9.-]+)?$' -or $fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Invalid distribution or Windows file version.'
}
$runtime = 'win-x64'
$runtimeVersion = '10.0.12'
$appProject = 'GameGarage/GameGarage/GameGarage.csproj'
$workerProject = 'GameGarage/StabilityTest/StabilityTest.csproj'
$testProjects = @('tests/GameGarage.RamTests/GameGarage.RamTests.csproj', 'tests/GameGarage.DiagnosticsTests/GameGarage.DiagnosticsTests.csproj', 'tests/GameGarage.UiTests/GameGarage.UiTests.csproj')
$buildProperties = @('-p:PublishAot=false', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', "-p:RuntimeFrameworkVersion=$runtimeVersion")
function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed (exit $LASTEXITCODE)." }
}
function Get-SafeArtifactPath {
    param([string]$RelativePath)
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
    $candidate = [IO.Path]::GetFullPath((Join-Path $artifactRoot $RelativePath))
    if (-not $candidate.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact path leaves artifacts directory.' }
    return $candidate
}
function Reset-ArtifactDirectory {
    param([string]$RelativePath)
    $candidate = Get-SafeArtifactPath $RelativePath
    if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate -Recurse -Force }
    [IO.Directory]::CreateDirectory($candidate) | Out-Null
    return $candidate
}
function Merge-PublishDirectory {
    param([string]$Source, [string]$Destination)
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relative = $file.FullName.Substring($Source.Length + 1)
        $target = Join-Path $Destination $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
                # Desktop supplies the implementation; the console runtime carries a compatibility facade.
                # Keep the app's implementation for this explicit, verified framework overlap.
                if ($relative -eq 'WindowsBase.dll') {
                    $desktopAssembly = [Reflection.AssemblyName]::GetAssemblyName($target)
                    $consoleAssembly = [Reflection.AssemblyName]::GetAssemblyName($file.FullName)
                    if ($desktopAssembly.Name -eq 'WindowsBase' -and $desktopAssembly.Version.Major -eq 10 -and
                        $consoleAssembly.Name -eq 'WindowsBase' -and $consoleAssembly.Version.Major -eq 4) { continue }
                }
                throw "Conflicting app/worker dependency: $relative"
            }
        } else { Copy-Item -LiteralPath $file.FullName -Destination $target }
    }
}
function Copy-RuntimeNotices {
    param([string]$Destination)
    $appAssets = Get-Content -LiteralPath 'GameGarage/GameGarage/obj/project.assets.json' -Raw | ConvertFrom-Json
    $packageRoots = @($appAssets.packageFolders.PSObject.Properties.Name)
    foreach ($packageName in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
        $packageFolder = $null
        foreach ($packageRoot in $packageRoots) {
            $candidate = Join-Path $packageRoot "$packageName/$runtimeVersion"
            if (Test-Path -LiteralPath $candidate -PathType Container) { $packageFolder = $candidate; break }
        }
        if (-not $packageFolder) { throw "Cannot locate restored notices for $packageName $runtimeVersion." }
        $notices = @(Get-ChildItem -LiteralPath $packageFolder -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if (-not @($notices | Where-Object { $_.Name -match '^LICENSE' }).Count) { throw "Runtime license missing: $packageName" }
        $noticeDestination = Join-Path $Destination "licenses/$packageName-$runtimeVersion"
        [IO.Directory]::CreateDirectory($noticeDestination) | Out-Null
        foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $noticeDestination $notice.Name) }
    }
}
Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot 'audit-publication.ps1')
    $expectedSdk = (Get-Content -LiteralPath 'global.json' -Raw | ConvertFrom-Json).sdk.version
    $actualSdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $expectedSdk) { throw "Install .NET SDK $expectedSdk (global.json)." }
    $nsisCompiler = $null
    if ($Package) { $nsisCompiler = & (Join-Path $PSScriptRoot 'get-nsis.ps1') }
    foreach ($project in @($appProject, $workerProject) + $testProjects) {
        if (-not (Test-Path -LiteralPath $project)) { throw "Required project missing: $project" }
        Invoke-DotNet (@('restore', $project, '-r', $runtime, '--nologo') + $buildProperties)
    }
    Invoke-DotNet (@('build', $appProject, '-c', $Configuration, '-r', $runtime, '--no-restore', '--nologo') + $buildProperties)
    foreach ($project in $testProjects) {
        Invoke-DotNet (@('build', $project, '-c', $Configuration, '-r', $runtime, '--no-restore', '--nologo') + $buildProperties)
        Invoke-DotNet @('run', '--project', $project, '-c', $Configuration, '-r', $runtime, '--no-build', '--no-restore')
    }
    if (-not $Package) { return }
    if ($Configuration -ne 'Release') { throw 'Distributable packages must use Release configuration.' }
    $appPublish = Reset-ArtifactDirectory 'publish-app'
    $workerPublish = Reset-ArtifactDirectory 'publish-worker'
    $bundleName = "GameGarage-$version-$runtime"
    $bundlePath = Reset-ArtifactDirectory $bundleName
    Invoke-DotNet (@('publish', $appProject, '-c', 'Release', '-r', $runtime, '--self-contained', 'true', '--no-restore', '--nologo', '-o', $appPublish) + $buildProperties)
    Invoke-DotNet (@('publish', $workerProject, '-c', 'Release', '-r', $runtime, '--self-contained', 'true', '--no-restore', '--nologo', '-o', $workerPublish) + $buildProperties)
    Merge-PublishDirectory $appPublish $bundlePath
    Merge-PublishDirectory $workerPublish $bundlePath
    foreach ($document in @('README.md', 'LICENSE', 'NOTICES.md')) { Copy-Item -LiteralPath $document -Destination (Join-Path $bundlePath $document) }
    Copy-RuntimeNotices $bundlePath
    foreach ($required in @('GameGarage.exe', 'GameGarage.dll', 'GameGarage.Core.dll', 'GameGarage.runtimeconfig.json', 'GameGarage.deps.json', 'StabilityTest.exe', 'StabilityTest.dll', 'StabilityTest.runtimeconfig.json', 'StabilityTest.deps.json', 'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'System.Private.CoreLib.dll', 'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll', 'LICENSE', 'NOTICES.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $bundlePath $required) -PathType Leaf)) { throw "Package missing $required" }
    }
    $unexpectedPayload = @(Get-ChildItem -LiteralPath $bundlePath -Recurse -File | Where-Object { $_.Name -match '(?i)(liblisa|gnosworks|guardware|ILGPU|FileCorrupter|HDFragmenter|Microsoft\.Management\.Infrastructure)|\.(cs|csproj|sln|pfx|p12|key|pem)$' })
    if ($unexpectedPayload.Count) { throw "Excluded package content: $($unexpectedPayload.Name -join ', ')" }
    foreach ($configurationName in @('GameGarage', 'StabilityTest')) {
        $runtimeConfig = Get-Content -LiteralPath (Join-Path $bundlePath "$configurationName.runtimeconfig.json") -Raw | ConvertFrom-Json
        if ($runtimeConfig.runtimeOptions.PSObject.Properties.Name -notcontains 'includedFrameworks') { throw "$configurationName is not self-contained." }
    }
    # Help validates worker discovery without allocating test memory. The caller's working directory is deliberately different.
    $workerExe = Join-Path $bundlePath 'StabilityTest.exe'
    Push-Location (Get-SafeArtifactPath 'publish-worker')
    try { & $workerExe --help; if ($LASTEXITCODE -ne 0) { throw 'Packaged worker help check failed.' } } finally { Pop-Location }
    $manifestLines = @(Get-ChildItem -LiteralPath $bundlePath -Recurse -File | Sort-Object FullName | ForEach-Object { $relative = $_.FullName.Substring($bundlePath.Length + 1).Replace('\', '/'); "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative" })
    [IO.File]::WriteAllText((Join-Path $bundlePath 'FILES-SHA256.txt'), (($manifestLines -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))
    # Build both distributions from this single audited payload.
    $installerWork = Reset-ArtifactDirectory 'installer-input'
    function Escape-NsisLiteral([string]$Value) {
        if ($Value.IndexOfAny(@([char]10, [char]13)) -ge 0) { throw 'Newline in NSIS input path.' }
        return $Value.Replace('$', '$$').Replace('"', '$\"')
    }
    $payloadFiles = @(Get-ChildItem -LiteralPath $bundlePath -Recurse -File | Sort-Object FullName)
    $payloadDirectories = @('') + @(Get-ChildItem -LiteralPath $bundlePath -Recurse -Directory | ForEach-Object {
        $_.FullName.Substring($bundlePath.Length + 1)
    } | Sort-Object)
    $installLines = @()
    $uninstallLines = @()
    foreach ($file in $payloadFiles) {
        $relative = $file.FullName.Substring($bundlePath.Length + 1)
        $relativeDirectory = [IO.Path]::GetDirectoryName($relative)
        $installLines += 'SetOutPath "$INSTDIR' + $(if ($relativeDirectory) { '\' + (Escape-NsisLiteral $relativeDirectory) } else { '' }) + '"'
        $installLines += 'File "$' + '{PAYLOAD_DIR}\' + (Escape-NsisLiteral $relative) + '"'
        $uninstallLines += '!insertmacro DeleteOwnedFile "' + (Escape-NsisLiteral $relative) + '"'
    }
    foreach ($directory in ($payloadDirectories | Where-Object { $_ } | Sort-Object Length -Descending)) {
        $uninstallLines += 'RMDir "$INSTDIR\' + (Escape-NsisLiteral $directory) + '"'
    }
    $includes = @{
        INSTALL_FILES = @('payload-install.nsh', $installLines)
        UNINSTALL_FILES = @('payload-uninstall.nsh', $uninstallLines)
        VALIDATE_FILES = @('payload-validate.nsh', (@($payloadDirectories | ForEach-Object { '!insertmacro ValidatePayloadDirectory "' + (Escape-NsisLiteral $_) + '"' }) + @($payloadFiles | ForEach-Object { '!insertmacro ValidatePayloadFile "' + (Escape-NsisLiteral $_.FullName.Substring($bundlePath.Length + 1)) + '"' })))
        UNVALIDATE_FILES = @('payload-unvalidate.nsh', (@($payloadDirectories | ForEach-Object { '!insertmacro un.ValidatePayloadDirectory "' + (Escape-NsisLiteral $_) + '"' }) + @($payloadFiles | ForEach-Object { '!insertmacro un.ValidatePayloadFile "' + (Escape-NsisLiteral $_.FullName.Substring($bundlePath.Length + 1)) + '"' })))
    }
    $installerPath = Get-SafeArtifactPath "$bundleName-setup.exe"
    $nsisArguments = @('/NOCONFIG', '/V3', '/WX', "/DPAYLOAD_DIR=$bundlePath", "/DOUTPUT_FILE=$installerPath",
        "/DAPP_VERSION=$version", "/DFILE_VERSION=$fileVersion")
    foreach ($name in ($includes.Keys | Sort-Object)) {
        if (@($includes[$name]).Count -ne 2) { throw "Invalid NSIS include record: $name" }
        $includePath = Join-Path $installerWork $includes[$name][0]
        [IO.File]::WriteAllText($includePath, (($includes[$name][1] -join [char]10) + [char]10), (New-Object Text.UTF8Encoding($false)))
        $nsisArguments += "/D$name=$includePath"
    }
    $nsisArguments += (Join-Path $repositoryRoot 'packaging/windows/GameGarage.nsi')
    & $nsisCompiler @nsisArguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath -PathType Leaf)) { throw 'NSIS installer compilation failed.' }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Get-SafeArtifactPath "$bundleName-portable.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
    $archive = New-Object IO.Compression.ZipArchive($zipStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $bundlePath -Recurse -File | Sort-Object FullName) {
            $relative = $file.FullName.Substring($bundlePath.Length + 1).Replace('\', '/')
            $entry = $archive.CreateEntry("GameGarage/$relative", [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(2024, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $inputStream = [IO.File]::OpenRead($file.FullName)
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        }
    } finally { $archive.Dispose(); $zipStream.Dispose() }
    $distributionPaths = @($installerPath, $zipPath)
    $checksum = (@($distributionPaths | ForEach-Object {
        "$((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))"
    }) -join [char]10) + [char]10
    [IO.File]::WriteAllText((Get-SafeArtifactPath 'SHA256SUMS.txt'), $checksum, (New-Object Text.UTF8Encoding($false)))
    Write-Host "Installer ready: $installerPath"
    Write-Host "Portable ZIP ready: $zipPath"
    Write-Host $checksum

} finally { Pop-Location }
