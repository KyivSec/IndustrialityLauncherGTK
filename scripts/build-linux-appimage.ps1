param(
    [string]$Configuration = "Release",
    [string]$Project = "src/Industriality.UI.Gtk/Industriality.UI.Gtk.csproj",
    [string]$PublishOutputRoot = "publish/launcher",
    [string]$AppImageRoot = "publish/appimage/linux-x64",
    [string]$InstallerOutputRoot = "publish/installers/linux-x64",
    [string]$AppImageFileName = "IndustrialityLauncher-x86_64.AppImage",
    [string]$AppImageTool = "appimagetool",
    [switch]$SkipPackage
)

$ErrorActionPreference = "Stop"

$rid = "linux-x64"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot (Join-Path $PublishOutputRoot $rid)
$appDir = Join-Path $repoRoot (Join-Path $AppImageRoot "AppDir")
$usrBinDir = Join-Path $appDir "usr/bin"
$usrShareAppDir = Join-Path $appDir "usr/share/applications"
$usrShareIconDir = Join-Path $appDir "usr/share/icons/hicolor/256x256/apps"
$installerOut = Join-Path $repoRoot $InstallerOutputRoot
$appImagePath = Join-Path $installerOut $AppImageFileName
$iconSource = Join-Path $repoRoot "src/Industriality.UI.Gtk/Assets/icon.png"
$gtkRuntime = [Environment]::GetEnvironmentVariable("GTK_RUNTIME_LINUX_X64")

function ConvertTo-WslPath([string]$WindowsPath) {
    $result = (& wsl.exe wslpath -a $WindowsPath 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($result)) {
        throw "Failed to convert path for WSL: $WindowsPath"
    }
    return $result.Trim()
}

function Invoke-WslChecked([string]$Command) {
    & wsl.exe sh -lc $Command
    if ($LASTEXITCODE -ne 0) {
        throw "WSL command failed: $Command"
    }
}

function Test-WslAvailable {
    & cmd.exe /c "wsl.exe --status >NUL 2>NUL"
    return $LASTEXITCODE -eq 0
}

function Write-AsciiLfFile([string]$Path, [string]$Content) {
    $normalized = $Content.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($Path, $normalized, [System.Text.Encoding]::ASCII)
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir, $usrBinDir, $usrShareAppDir, $usrShareIconDir, $installerOut -Force | Out-Null

$publishArgs = @(
    "publish", $Project,
    "-c", $Configuration,
    "-r", $rid,
    "--no-restore",
    "--self-contained", "true",
    "-o", $publishDir
)

& dotnet restore $Project -r $rid
if ($LASTEXITCODE -ne 0) { throw "Restore failed for $rid" }

if ([string]::IsNullOrWhiteSpace($gtkRuntime)) {
    Write-Host "[$rid] no GTK_RUNTIME_LINUX_X64 set; AppImage will rely on system GTK."
    $publishArgs += "/p:BundleGtkRuntime=false"
}
else {
    if (-not (Test-Path -LiteralPath $gtkRuntime -PathType Container)) {
        throw "GTK_RUNTIME_LINUX_X64 points to '$gtkRuntime', but that directory does not exist."
    }

    Write-Host "[$rid] bundling GTK runtime from: $gtkRuntime"
    $publishArgs += "/p:BundleGtkRuntime=true"
    $publishArgs += "/p:GtkRuntimeSourceDir=$gtkRuntime"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed for $rid" }

Copy-Item -Path (Join-Path $publishDir "*") -Destination $usrBinDir -Recurse -Force
Copy-Item -Path $iconSource -Destination (Join-Path $appDir "industrialitylauncher.png") -Force
Copy-Item -Path $iconSource -Destination (Join-Path $usrShareIconDir "industrialitylauncher.png") -Force

$desktopEntry = @"
[Desktop Entry]
Name=Industriality Launcher
Exec=IndustrialityLauncher
Icon=industrialitylauncher
Type=Application
Categories=Game;
"@
$desktopPath = Join-Path $appDir "IndustrialityLauncher.desktop"
Write-AsciiLfFile -Path $desktopPath -Content $desktopEntry
Copy-Item -Path $desktopPath -Destination (Join-Path $usrShareAppDir "IndustrialityLauncher.desktop") -Force

$appRun = @'
#!/bin/sh
set -eu

if [ -z "${APPDIR:-}" ]; then
  APPDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
fi

RUNTIME_ROOT="$APPDIR/usr/bin/gtk-runtime"
if [ -d "$RUNTIME_ROOT" ]; then
  if [ -d "$RUNTIME_ROOT/bin" ]; then
    export PATH="$RUNTIME_ROOT/bin:${PATH:-}"
  fi
  if [ -d "$RUNTIME_ROOT/lib" ]; then
    export LD_LIBRARY_PATH="$RUNTIME_ROOT/lib:${LD_LIBRARY_PATH:-}"
  fi
  if [ -d "$RUNTIME_ROOT/share" ]; then
    export XDG_DATA_DIRS="$RUNTIME_ROOT/share:${XDG_DATA_DIRS:-/usr/local/share:/usr/share}"
  fi
  if [ -d "$RUNTIME_ROOT/share/glib-2.0/schemas" ]; then
    export GSETTINGS_SCHEMA_DIR="$RUNTIME_ROOT/share/glib-2.0/schemas"
  fi
  if [ -d "$RUNTIME_ROOT/lib/gdk-pixbuf-2.0/2.10.0/loaders" ]; then
    export GDK_PIXBUF_MODULEDIR="$RUNTIME_ROOT/lib/gdk-pixbuf-2.0/2.10.0/loaders"
  fi
  if [ -f "$RUNTIME_ROOT/lib/gdk-pixbuf-2.0/2.10.0/loaders.cache" ]; then
    export GDK_PIXBUF_MODULE_FILE="$RUNTIME_ROOT/lib/gdk-pixbuf-2.0/2.10.0/loaders.cache"
  fi
fi

exec "$APPDIR/usr/bin/IndustrialityLauncher" "$@"
'@
$appRunPath = Join-Path $appDir "AppRun"
Write-AsciiLfFile -Path $appRunPath -Content $appRun

Write-Host "AppDir staged at: $appDir"

if ($SkipPackage) {
    Write-Host "Skipping AppImage packaging because -SkipPackage was provided."
    return
}

if (-not (Test-WslAvailable)) {
    Write-Warning "WSL is not available. AppDir was staged, but AppImage packaging was skipped."
    Write-Warning "Install WSL and appimagetool, then rerun this script to produce $appImagePath."
    return
}

$appImageToolCheck = (& wsl.exe sh -lc "command -v '$AppImageTool'" 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appImageToolCheck)) {
    Write-Warning "WSL is available, but '$AppImageTool' was not found in WSL PATH."
    Write-Warning "Install appimagetool in WSL or pass -AppImageTool <path-or-command>. AppDir is ready at $appDir."
    return
}

$wslAppDir = ConvertTo-WslPath $appDir
$wslOut = ConvertTo-WslPath $appImagePath
Invoke-WslChecked "chmod +x '$wslAppDir/AppRun' '$wslAppDir/usr/bin/IndustrialityLauncher'"
Invoke-WslChecked "ARCH=x86_64 '$AppImageTool' '$wslAppDir' '$wslOut'"

if (-not (Test-Path $appImagePath -PathType Leaf)) {
    throw "AppImage tool completed but output was not found: $appImagePath"
}

Write-Host "AppImage published to: $appImagePath" -ForegroundColor Green
