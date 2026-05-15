param(
    [string]$Configuration = "Release",
    [string]$LauncherProject = "src/Industriality.UI.Gtk/Industriality.UI.Gtk.csproj",
    [string]$InstallerProject = "src/Industriality.Installer/Industriality.Installer.csproj",
    [string]$LauncherOutputRoot = "publish/launcher",
    [string]$InstallerOutputRoot = "publish/installers",
    [string[]]$Rids = @("win-x64","win-arm64","linux-x64","osx-x64","osx-arm64")
)

$ErrorActionPreference = "Stop"

$repoRoot     = Resolve-Path (Join-Path $PSScriptRoot "..")
$payloadDir   = Join-Path $repoRoot "src/Industriality.Installer/Payload"
$payloadZip   = Join-Path $payloadDir "payload.zip"
$payloadVer   = Join-Path $payloadDir "payload.version"

$gtkEnvMap = @{
    "win-x64"   = "GTK_RUNTIME_WIN_X64"
    "win-arm64" = "GTK_RUNTIME_WIN_ARM64"
    "linux-x64" = "GTK_RUNTIME_LINUX_X64"
    "osx-x64"   = "GTK_RUNTIME_OSX_X64"
    "osx-arm64" = "GTK_RUNTIME_OSX_ARM64"
}

$builtRids = New-Object System.Collections.Generic.List[string]
$skippedRids = New-Object System.Collections.Generic.List[string]

# Unix-executable mode bits packed into ZIP ExternalAttributes upper 16 bits.
# 0o100755 = regular file, rwxr-xr-x
$ExecExternalAttributes = (0x81ED) -shl 16

function Get-PayloadVersion {
    try {
        $sha = (& git -C $repoRoot rev-parse --short HEAD 2>$null).Trim()
        if ($LASTEXITCODE -eq 0 -and $sha) {
            $stamp = (Get-Date -Format "yyyyMMddHHmmss")
            return "$stamp-$sha"
        }
    } catch { }
    return (Get-Date -Format "yyyyMMddHHmmss")
}

function Get-GtkRuntime([string]$Rid) {
    $envName = $gtkEnvMap[$Rid]
    if ([string]::IsNullOrWhiteSpace($envName)) {
        throw "Unsupported RID '$Rid'. Supported RIDs: $($gtkEnvMap.Keys -join ', ')"
    }

    $gtkRuntime = [Environment]::GetEnvironmentVariable($envName)
    if ([string]::IsNullOrWhiteSpace($gtkRuntime)) {
        return $null
    }

    if (-not (Test-Path -LiteralPath $gtkRuntime -PathType Container)) {
        Write-Warning "[$Rid] $envName points to '$gtkRuntime', but that directory does not exist; skipping."
        return $null
    }

    return (Resolve-Path -LiteralPath $gtkRuntime).Path
}

function Publish-Launcher([string]$Rid, [string]$OutputDir, [string]$GtkRuntime) {
    if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }

    $publishArgs = @(
        "publish", $LauncherProject,
        "-c", $Configuration,
        "-r", $Rid,
        "--self-contained", "true",
        "-o", $OutputDir
    )

    Write-Host "[$Rid] bundling GTK runtime from: $GtkRuntime"
    $publishArgs += "/p:BundleGtkRuntime=true"
    $publishArgs += "/p:GtkRuntimeSourceDir=$GtkRuntime"

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed for $Rid" }
}

function New-PayloadZip([string]$Rid, [string]$SourceDir, [string]$ZipPath) {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $applyExec = -not $Rid.StartsWith("win-")
    $sourceFull = (Resolve-Path $SourceDir).Path
    $prefix = $sourceFull
    if (-not $prefix.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $prefix = $prefix + [System.IO.Path]::DirectorySeparatorChar
    }
    $stream = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::CreateNew)
    $archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = Get-ChildItem -LiteralPath $sourceFull -Recurse -File
        foreach ($file in $files) {
            $full = $file.FullName
            if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "File '$full' is not under source root '$prefix'"
            }
            $relative = $full.Substring($prefix.Length).Replace('\','/')
            $entry = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $relative,
                [System.IO.Compression.CompressionLevel]::Optimal)
            if ($applyExec -and (Test-IsExecutableEntry $relative $file.Name)) {
                $entry.ExternalAttributes = $ExecExternalAttributes
            }
        }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }
}

function Test-IsExecutableEntry([string]$RelativePath, [string]$FileName) {
    if ($FileName -eq "IndustrialityLauncher") { return $true }
    if ($FileName -like "*.so")   { return $true }
    if ($FileName -like "*.so.*") { return $true }
    if ($FileName -like "*.dylib"){ return $true }
    $dir = Split-Path $RelativePath -Parent
    if ($dir -and ((Split-Path $dir -Leaf) -eq "bin")) { return $true }
    return $false
}

function Publish-Installer([string]$Rid, [string]$OutputDir) {
    if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }

    $args = @(
        "publish", $InstallerProject,
        "-c", $Configuration,
        "-r", $Rid,
        "--self-contained", "true",
        "-o", $OutputDir
    )
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Installer publish failed for $Rid" }
}

function Resolve-IsccPath {
    if ($env:INNO_SETUP_PATH -and (Test-Path -LiteralPath $env:INNO_SETUP_PATH -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $env:INNO_SETUP_PATH).Path
    }
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c -PathType Leaf) { return $c }
    }
    throw "ISCC.exe (Inno Setup 6) not found. Install via 'winget install JRSoftware.InnoSetup' or set `$env:INNO_SETUP_PATH to ISCC.exe."
}

function Build-WindowsInstaller-Inno([string]$Rid, [string]$LauncherDir, [string]$OutputDir, [string]$AppVersion) {
    $iscc = Resolve-IsccPath
    $script = Join-Path $repoRoot "scripts\industriality-win.iss"

    $archMap = @{ "win-x64" = "x64"; "win-arm64" = "arm64" }
    $arch = $archMap[$Rid]
    if (-not $arch) { throw "No Inno arch mapping for RID '$Rid'." }

    if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    $sourceFull = (Resolve-Path -LiteralPath $LauncherDir).Path
    $outFull    = (Resolve-Path -LiteralPath $OutputDir).Path

    $isccArgs = @(
        "/Q",
        $script,
        "/DSourceDir=$sourceFull",
        "/DOutputDir=$outFull",
        "/DArch=$arch",
        "/DAppVersion=$AppVersion"
    )
    Write-Host "[$Rid] running ISCC.exe -> $arch installer"
    & $iscc @isccArgs
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed for $Rid (exit $LASTEXITCODE)." }
}

# Main loop
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null

foreach ($rid in $Rids) {
    Write-Host ""
    Write-Host "=== $rid ===" -ForegroundColor Cyan

    $launcherOut  = Join-Path $LauncherOutputRoot $rid
    $installerOut = Join-Path $InstallerOutputRoot $rid
    $gtkRuntime = Get-GtkRuntime -Rid $rid
    if ([string]::IsNullOrWhiteSpace($gtkRuntime)) {
        Write-Warning "[$rid] no usable $($gtkEnvMap[$rid]) configured; skipping installer to avoid a broken GTK payload."
        if (Test-Path $launcherOut) { Remove-Item $launcherOut -Recurse -Force }
        if (Test-Path $installerOut) { Remove-Item $installerOut -Recurse -Force }
        $skippedRids.Add($rid) | Out-Null
        continue
    }

    Publish-Launcher -Rid $rid -OutputDir $launcherOut -GtkRuntime $gtkRuntime

    $version = Get-PayloadVersion
    Write-Host "[$rid] payload version: $version"

    if ($rid -like "win-*") {
        Build-WindowsInstaller-Inno -Rid $rid -LauncherDir $launcherOut -OutputDir $installerOut -AppVersion $version
    }
    else {
        Set-Content -Path $payloadVer -Value $version -Encoding ascii -NoNewline
        New-PayloadZip -Rid $rid -SourceDir $launcherOut -ZipPath $payloadZip
        $zipSize = (Get-Item $payloadZip).Length
        Write-Host ("[$rid] payload.zip = {0:N0} bytes" -f $zipSize)

        # Force the .NET installer to rebuild so the new resource is embedded.
        $installerObj = Join-Path $repoRoot "src/Industriality.Installer/obj"
        $installerBin = Join-Path $repoRoot "src/Industriality.Installer/bin"
        if (Test-Path $installerObj) { Remove-Item $installerObj -Recurse -Force }
        if (Test-Path $installerBin) { Remove-Item $installerBin -Recurse -Force }

        Publish-Installer -Rid $rid -OutputDir $installerOut
    }

    Write-Host "[$rid] installer published to $installerOut" -ForegroundColor Green
    $builtRids.Add($rid) | Out-Null
}

# Clean payload staging so a fresh build is needed next time.
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
if (Test-Path $payloadVer) { Remove-Item $payloadVer -Force }

Write-Host ""
if ($builtRids.Count -gt 0) {
    Write-Host "Built installers: $($builtRids -join ', ')" -ForegroundColor Green
}
if ($skippedRids.Count -gt 0) {
    Write-Host "Skipped RIDs without usable GTK runtimes: $($skippedRids -join ', ')" -ForegroundColor Yellow
}
if ($builtRids.Count -eq 0) {
    throw "No installers were built. Set at least one GTK_RUNTIME_* environment variable to a relocatable GTK runtime root."
}

Write-Host "Installer build complete." -ForegroundColor Green
