<#
.SYNOPSIS
  Builds Overspray and, optionally, drops it into GTA V.

.DESCRIPTION
  Uses the self-contained Roslyn compiler rather than `dotnet build`, because the
  machine SDK is not reliable here and this needs no MSBuild anyway -- one library,
  no NuGet, no project file.

  The toolchain is NOT in this repo. It is ~174 MB of compiler and reference
  assemblies, and there is already a copy on this machine under the hoodrich
  project, so this looks there rather than carrying a second one. Point -Tools
  somewhere else, or drop a tools\ folder in beside this script, and it will use
  that instead.

.EXAMPLE
  .\build.ps1
  .\build.ps1 -Deploy
  .\build.ps1 -Deploy -Target Both
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$Deploy,

    [ValidateSet('Legacy', 'Enhanced', 'Both')]
    [string]$Target = 'Both',

    [string]$GtaDir = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V',
    [string]$EnhancedDir = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced',

    # Where the compiler lives. Its own tools\ first, then the one next door.
    [string]$Tools = ''
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# --- the toolchain ----------------------------------------------------------
if (-not $Tools) {
    $candidates = @(
        (Join-Path $root 'tools'),
        (Join-Path (Split-Path $root -Parent) 'hoodrich\tools')
    )

    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c 'roslyn\tasks\net472\csc.exe')) { $Tools = $c; break }
    }
}

if (-not $Tools) {
    throw "No compiler found. Looked in .\tools\ and ..\hoodrich\tools\. Pass -Tools <path>."
}

$csc    = Join-Path $Tools 'roslyn\tasks\net472\csc.exe'
$refDir = Join-Path $Tools 'refasm\build\.NETFramework\v4.8'

if (-not (Test-Path $csc))    { throw "Compiler missing: $csc" }
if (-not (Test-Path $refDir)) { throw "net48 reference assemblies missing: $refDir" }

$srcDir = Join-Path $root 'src\Overspray'
$outDir = Join-Path $root 'build'
$outDll = Join-Path $outDir 'Overspray.dll'

$shvdn = Join-Path $GtaDir 'ScriptHookVDotNet3.dll'
if (-not (Test-Path $shvdn)) { throw "ScriptHookVDotNet3.dll not found under: $GtaDir" }

New-Item -ItemType Directory -Force $outDir | Out-Null

# --- references -------------------------------------------------------------
# Same rule as hoodrich: the BCL and SHVDN, nothing else. A mod with no external
# dependencies cannot lose a version fight with another mod in scripts\.
$refNames = @(
    'mscorlib.dll'
    'System.dll'
    'System.Core.dll'
    'System.Drawing.dll'
    'System.Windows.Forms.dll'
    'System.Numerics.dll'
)

$refs = @()
foreach ($n in $refNames) {
    $p = Join-Path $refDir $n
    if (-not (Test-Path $p)) { throw "Reference assembly missing: $p" }
    $refs += "/reference:`"$p`""
}
$refs += "/reference:`"$shvdn`""

# --- sources ----------------------------------------------------------------
$sources = Get-ChildItem $srcDir -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object { $_.FullName }

if (-not $sources) { throw "No .cs sources found under $srcDir" }

# --- compiler options -------------------------------------------------------
$opts = @(
    '/target:library'
    '/platform:x64'
    '/langversion:9.0'
    '/nologo'
    '/warnaserror-'
    '/warn:4'
    '/nostdlib+'
    '/utf8output'
    "/out:`"$outDll`""
)

if ($Configuration -eq 'Debug') {
    $opts += '/debug:portable', '/define:DEBUG;TRACE', '/optimize-'
} else {
    $opts += '/debug-', '/optimize+'
}

$rsp = Join-Path $outDir 'build.rsp'
($opts + $refs + ($sources | ForEach-Object { "`"$_`"" })) | Set-Content -Path $rsp -Encoding UTF8

Write-Host "Compiling $($sources.Count) source files -> $outDll ($Configuration)" -ForegroundColor Cyan
$sw = [Diagnostics.Stopwatch]::StartNew()
& $csc "@$rsp"
$exit = $LASTEXITCODE
$sw.Stop()

if ($exit -ne 0) { throw "Compilation failed (csc exit $exit)." }
Write-Host ("OK  {0:N0} bytes in {1:N1}s" -f (Get-Item $outDll).Length, $sw.Elapsed.TotalSeconds) -ForegroundColor Green

# --- deploy -----------------------------------------------------------------
function Deploy-To([string]$dir, [string]$label) {
    if (-not (Test-Path $dir)) { Write-Host "  skip   $label (not installed)" -ForegroundColor DarkGray; return }

    $scripts = Join-Path $dir 'scripts'
    New-Item -ItemType Directory -Force $scripts | Out-Null

    Copy-Item $outDll (Join-Path $scripts 'Overspray.dll') -Force

    $iniSrc = Join-Path $root 'Overspray.ini'
    $iniDst = Join-Path $scripts 'Overspray.ini'

    if (Test-Path $iniSrc) {
        if (Test-Path $iniDst) {
            # Never overwritten: it is the one file a player hand-edits.
            Write-Host "  keep   Overspray.ini" -ForegroundColor DarkGray
        } else {
            Copy-Item $iniSrc $iniDst
            Write-Host "  new    Overspray.ini" -ForegroundColor Green
        }
    }

    # --- art -----------------------------------------------------------------
    #
    # Always overwritten, unlike the ini. Nobody hand-edits a PNG in place, and a stale
    # wordmark from three builds ago is a bug that looks like a rendering fault.
    $artSrc = Join-Path $root 'data\icons'
    $artDst = Join-Path $scripts 'Overspray\icons'

    if (Test-Path $artSrc) {
        New-Item -ItemType Directory -Force $artDst | Out-Null

        $n = 0
        foreach ($f in Get-ChildItem $artSrc -Filter *.png) {
            $to = Join-Path $artDst $f.Name

            # Only when it actually differs, so a deploy that changed no art says so rather
            # than printing a list of files every time.
            if ((Test-Path $to) -and (Get-Item $to).Length -eq $f.Length -and
                (Get-FileHash $to).Hash -eq (Get-FileHash $f.FullName).Hash) { continue }

            Copy-Item $f.FullName $to -Force
            $n++
        }

        if ($n -gt 0) { Write-Host "  art    $n file(s)" -ForegroundColor Green }
        else          { Write-Host "  art    up to date" -ForegroundColor DarkGray }
    }

    Write-Host "  ok     $label" -ForegroundColor Green
}

if ($Deploy) {
    $running = Get-Process GTA5, GTA5_Enhanced -ErrorAction SilentlyContinue
    if ($running) { throw "GTA V is running - close it before deploying." }

    if ($Target -in 'Legacy', 'Both')   { Deploy-To $GtaDir      'Legacy' }
    if ($Target -in 'Enhanced', 'Both') { Deploy-To $EnhancedDir 'Enhanced' }

    Write-Host "Deploy complete." -ForegroundColor Green
}
