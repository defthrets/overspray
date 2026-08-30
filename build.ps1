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
  .\build.ps1 -Package        # the mod on its own
  .\build.ps1 -Package -Full  # ...and ScriptHookVDotNet bundled with it
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [switch]$Deploy,

    # Builds the release zip in release\, with the tree a player unpacks.
    [switch]$Package,

    # Bundle ScriptHookVDotNet into the zip as well, so it merges straight over a clean
    # GTA V folder. Both editions ship the same SHVDN, so one copy serves either.
    [switch]$Full,

    [string]$ShvdnFrom = 'C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced',

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

# WHICH ScriptHookVDotNet, said out loud, every build.
#
# The compiler stamps the reference assembly's EXACT version into the output, so a mod built
# here against 3.9 is a mod that asks for 3.9 -- and a player on 3.7 gets a load failure with
# no log, because the thing that would have written the log is the thing that did not load.
#
# That is not a hypothetical. It is four "it does not work for me" reports on the mod page
# against a readme promising 3.6 or newer, written when 3.6 was what this machine had. The
# number moved when ScriptHookVDotNet updated and nothing said so.
$shvdnVer = [System.Reflection.AssemblyName]::GetAssemblyName($shvdn).Version
Write-Host "ScriptHookVDotNet reference: $shvdnVer  (players need this or newer)" -ForegroundColor DarkCyan

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


# --- package ----------------------------------------------------------------
#
# THE ZIP IS THE PRODUCT, and its shape is the whole install. Somebody who has never seen this
# repo has one job -- drag "scripts" into the GTA folder -- and every way that goes wrong is a
# folder in the wrong place. So this builds the tree explicitly and then CHECKS it, because a
# packaging script that quietly ships four files instead of five is a support thread.
if ($Package) {
    $ver = (Select-String -Path (Join-Path $root 'src\Overspray\Core\Log.cs') `
                          -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value

    $stage = Join-Path $root "build\pkg"
    $zip = Join-Path $root ("release\Overspray-" + $ver + ".zip")

    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Force (Join-Path $stage 'scripts\Overspray\icons') | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $root 'release') | Out-Null

    Copy-Item $outDll                          (Join-Path $stage 'scripts\Overspray.dll')
    Copy-Item (Join-Path $root 'Overspray.ini') (Join-Path $stage 'scripts\Overspray.ini')
    Copy-Item (Join-Path $root 'README.txt')          (Join-Path $stage 'README.txt')
    Copy-Item (Join-Path $root 'release\CHANGES.txt') (Join-Path $stage 'CHANGES.txt')

    foreach ($p in Get-ChildItem (Join-Path $root 'data\icons') -Filter *.png) {
        Copy-Item $p.FullName (Join-Path $stage 'scripts\Overspray\icons')
    }

    # Every file the mod actually reads, by the path it reads it from. Missing any one of
    # these is a different broken install, and all of them are silent.
    [string[]]$must = @(
        'README.txt',
        'CHANGES.txt',
        'scripts\Overspray.dll',
        'scripts\Overspray.ini',
        'scripts\Overspray\icons\logo.png',
        'scripts\Overspray\icons\can.png',
        'scripts\Overspray\icons\cap_thin.png',
        'scripts\Overspray\icons\cap_stock.png',
        'scripts\Overspray\icons\cap_fat.png',
        'scripts\Overspray\icons\logo_0.png',
        'scripts\Overspray\icons\logo_1.png',
        'scripts\Overspray\icons\logo_2.png',
        'scripts\Overspray\icons\logo_3.png',
        'scripts\Overspray\icons\logo_4.png',
        'scripts\Overspray\icons\logo_5.png',
        'scripts\Overspray\icons\logo_6.png',
        'scripts\Overspray\icons\logo_7.png'
    )

    # Everything needed to run it, when asked for.
    #
    # Without this the zip is the mod and nothing else, which is right for somebody who
    # already runs script mods and wrong for everybody else -- and 'it does not do anything'
    # with no ScriptHookVDotNet installed looks identical to a mod that is broken.
    #
    # ScriptHookV itself is NOT in here and cannot be: Alexander Blade's licence forbids
    # redistributing it. The README sends people to dev-c.com for that one file.
    if ($Full) {
        foreach ($f in @('ScriptHookVDotNet.asi', 'ScriptHookVDotNet2.dll',
                         'ScriptHookVDotNet3.dll', 'ScriptHookVDotNet.ini')) {
            $src = Join-Path $ShvdnFrom $f
            if (-not (Test-Path $src)) { throw "-Full needs $f, and it is not in $ShvdnFrom" }

            Copy-Item $src $stage
            $must += $f
        }

        $lic = Join-Path $ShvdnFrom 'Licenses'
        if (Test-Path $lic) { Copy-Item $lic $stage -Recurse }

        Copy-Item (Join-Path $root 'release\READ ME FIRST.txt') $stage
        $must += 'READ ME FIRST.txt'

        $zip = Join-Path $root ('release\Overspray-' + $ver + '-full.zip')
    }

    $missing = @()
    foreach ($m in $must) { if (-not (Test-Path (Join-Path $stage $m))) { $missing += $m } }

    if ($missing) { throw "Package is missing: $($missing -join ', ')" }

    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

    Write-Host ""
    Write-Host ("Packaged  {0}" -f (Split-Path $zip -Leaf)) -ForegroundColor Green
    foreach ($m in $must) {
        $f = Get-Item (Join-Path $stage $m)
        Write-Host ("  {0,-42} {1,9:N0} bytes" -f $m, $f.Length) -ForegroundColor DarkGray
    }
    Write-Host ("  {0,-42} {1,9:N0} bytes" -f '(zip)', (Get-Item $zip).Length)
}
