param(
    [string]$Configuration = "Release",
    [string]$Project = "src/Industriality.UI.Gtk/Industriality.UI.Gtk.csproj",
    [string]$OutputRoot = "publish/portable"
)

$ErrorActionPreference = "Stop"

$targets = @(
    @{ Rid = "win-x64";   GtkEnv = "GTK_RUNTIME_WIN_X64" },
    @{ Rid = "win-arm64"; GtkEnv = "GTK_RUNTIME_WIN_ARM64" },
    @{ Rid = "linux-x64"; GtkEnv = "GTK_RUNTIME_LINUX_X64" },
    @{ Rid = "osx-x64";   GtkEnv = "GTK_RUNTIME_OSX_X64" },
    @{ Rid = "osx-arm64"; GtkEnv = "GTK_RUNTIME_OSX_ARM64" }
)

foreach ($target in $targets) {
    $rid = $target.Rid
    $envName = $target.GtkEnv
    $gtkRuntime = [Environment]::GetEnvironmentVariable($envName)
    $output = Join-Path $OutputRoot $rid

    Write-Host "Publishing $rid -> $output"

    $args = @(
        "publish", $Project,
        "-c", $Configuration,
        "-r", $rid,
        "--self-contained", "true",
        "-o", $output
    )

    if ([string]::IsNullOrWhiteSpace($gtkRuntime)) {
        $args += "/p:BundleGtkRuntime=false"
        Write-Host "  No `$env:$envName set, skipping GTK runtime bundling for $rid."
    }
    else {
        $args += "/p:BundleGtkRuntime=true"
        $args += "/p:GtkRuntimeSourceDir=$gtkRuntime"
        Write-Host "  Bundling GTK runtime from: $gtkRuntime"
    }

    & dotnet @args
}

Write-Host "Done."
