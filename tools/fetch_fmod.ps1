<#
Puts FMOD's Android native libraries where the build expects them.

FMOD is not redistributable, so the archive itself cannot live in this repository and cannot be
downloaded without an account - fmod.com puts every download behind a login. So this does not
pretend to fetch it unattended. What it does:

  - says nothing and exits if the libraries are already in place;
  - finds an FMOD Android archive you have already downloaded, anywhere obvious, and unpacks the
    right .so for each ABI out of it;
  - otherwise tells you exactly what to get and opens the download page.

Give it a path if the archive is somewhere unusual:
    tools\fetch_fmod.ps1 -Archive D:\stuff\fmodstudioapi20228android.tar.gz
#>

[CmdletBinding()]
param(
    [string]$Archive,
    [switch]$Force,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$libRoot = Join-Path $repo 'BebooGarden.Android\lib\android'
$abis = @('arm64-v8a', 'armeabi-v7a', 'x86_64')

function Write-Step($text) { if (-not $Quiet) { Write-Host $text } }

# --- Already done? -----------------------------------------------------------
$present = $abis | Where-Object { Test-Path (Join-Path $libRoot "$_\libfmod.so") }
if ($present.Count -gt 0 -and -not (Test-Path (Join-Path $libRoot 'fmod.jar'))) {
    Write-Host "  libfmod.so is here but fmod.jar is not; FMOD would fail with ERR_INTERNAL."
    Write-Host "  Re-unpacking to pick it up."
    $present = @()
}
if ($present.Count -gt 0) {
    Write-Step "  FMOD already in place for: $($present -join ', ')"
    exit 0
}

# --- Find an archive ---------------------------------------------------------
if (-not $Archive) {
    $searchIn = @(
        "$env:USERPROFILE\Downloads"
        "$env:USERPROFILE\Desktop"
        $repo
    ) | Where-Object { Test-Path $_ }

    $Archive = Get-ChildItem $searchIn -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'fmod.*android.*\.(zip|tar\.gz|tgz)$' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $Archive -or -not (Test-Path $Archive)) {
    Write-Host ""
    Write-Host "  FMOD's Android libraries are missing, and they cannot be downloaded for you:"
    Write-Host "  every fmod.com download needs an account, and the licence does not allow"
    Write-Host "  shipping them in this repository."
    Write-Host ""
    Write-Host "    1. Sign in (free) at https://www.fmod.com/download"
    Write-Host "    2. Take FMOD Engine, platform Android"
    Write-Host "    3. Leave the archive in your Downloads folder"
    Write-Host "    4. Run this again - it will find it and unpack it"
    Write-Host ""
    Write-Host "  Or point it straight at the file:"
    Write-Host "    tools\fetch_fmod.ps1 -Archive <path to the archive>"
    Write-Host ""
    Write-Host "  Without it the game still builds, installs, talks and unpacks its sounds."
    Write-Host "  It fails only when it asks FMOD for an audio system."
    Write-Host ""
    if (-not $Quiet) { Start-Process 'https://www.fmod.com/download#fmodengine' }
    exit 2
}

Write-Step "  Using $Archive"

# --- Does it match the binding? ----------------------------------------------
# FMOD checks the header version against the library version in System::init and
# returns ERR_HEADER_MISMATCH when the minor line differs. A 2.03 library against
# a 2.02 binding builds and installs perfectly and then has no audio at all, with
# nothing on screen to say why - so catch it here, where it is still cheap.
# Read it off the Windows fmod.dll's file version rather than by loading the managed
# binding. Loading FmodAudio.dll needs its own dependencies resolvable from wherever
# this happens to be run, and when that fails the reflection returns null and the check
# skips - so the fragile way to do this is also the way that silently passes.
# fmod.dll reports e.g. "2.2.14 (build 133546)", where 2.2 is the 2.02 line.
$bindingLine = $null
$bindingProblem = $null
try {
    $fmodDll = [System.IO.Path]::GetFullPath((Join-Path $repo 'BebooGarden\lib\fmod.dll'))
    if (Test-Path $fmodDll) {
        $fileVersion = (Get-Item $fmodDll).VersionInfo.FileVersion
        if ($fileVersion -match '^(\d+)\.(\d+)\.') {
            $bindingLine = "{0}.{1:D2}" -f [int]$Matches[1], [int]$Matches[2]
        }
        else { $bindingProblem = "could not parse '$fileVersion'" }
    }
    else { $bindingProblem = "fmod.dll not found at $fmodDll" }
}
catch { $bindingProblem = $_.Exception.Message }

# Say so rather than skipping in silence. A check that quietly does nothing when it
# cannot run is worse than no check, because it still looks like it passed.
if (-not $bindingLine) {
    Write-Host "  Note: could not read the expected FMOD version, so it is unchecked."
    if ($bindingProblem) { Write-Host "        ($bindingProblem)" }
}

if ($bindingLine -and ($Archive -match 'fmodstudioapi(\d)(\d\d)(\d\d)')) {
    $archiveLine = "$($Matches[1]).$($Matches[2])"

    if ($archiveLine -ne $bindingLine) {
        Write-Host ""
        Write-Host "  Wrong FMOD line: that archive is $archiveLine, the binding needs $bindingLine."
        Write-Host ""
        Write-Host "  FMOD compares the two in System::init and refuses to start on a mismatch"
        Write-Host "  (ERR_HEADER_MISMATCH). The apk would build and install and simply have no"
        Write-Host "  sound, which is a miserable thing to debug on a phone."
        Write-Host ""
        Write-Host "  On fmod.com the download page has a version dropdown - pick $bindingLine.x"
        Write-Host "  (the Windows build in this repo is on $bindingLine too, so both platforms"
        Write-Host "  stay on one version)."
        Write-Host ""
        Write-Host "  To use it anyway: -Force"
        Write-Host ""
        if (-not $Force) { exit 4 }
        Write-Host "  -Force given; continuing with the mismatched version."
    }
}

# --- Unpack it ---------------------------------------------------------------
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "fmod-android-$(Get-Random)"
New-Item -ItemType Directory -Path $staging -Force | Out-Null

try {
    if ($Archive -match '\.zip$') {
        Expand-Archive -Path $Archive -DestinationPath $staging -Force
    }
    else {
        # Windows' own bsdtar, by full path, not whatever "tar" happens to resolve to. Run from a
        # Git Bash shell, PATH puts GNU tar first, and GNU tar reads "C:\Users\..." as a remote
        # host called C - "Cannot connect to C: resolve failed" - which looks like a broken archive
        # rather than the wrong tar.
        $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
        if (-not (Test-Path $tar)) { $tar = 'tar' }
        & $tar -xf $Archive -C $staging
        if ($LASTEXITCODE -ne 0) { throw "tar could not read $Archive" }
    }

    # libfmodL.so is the logging build - bigger, slower, and not what ships.
    $found = Get-ChildItem $staging -Recurse -File -Filter 'libfmod.so' -ErrorAction SilentlyContinue

    if (-not $found) {
        Write-Host "  No libfmod.so inside that archive. Is it the Android build of FMOD Engine?"
        exit 3
    }

    $copied = 0
    foreach ($abi in $abis) {
        # The ABI is the name of the folder the library sits in, inside api/core/lib/<abi>/.
        $source = $found | Where-Object { (Split-Path $_.DirectoryName -Leaf) -eq $abi } | Select-Object -First 1
        if (-not $source) { continue }

        $target = Join-Path $libRoot $abi
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Copy-Item $source.FullName (Join-Path $target 'libfmod.so') -Force
        Write-Step ("  {0,-14} {1:N1} MB" -f $abi, ($source.Length / 1MB))
        $copied++
    }

    if ($copied -eq 0) {
        Write-Host "  Found libfmod.so but not for any ABI this build uses ($($abis -join ', '))."
        exit 3
    }

    # FMOD on Android is half native and half Java. org.fmod.FMOD has to be handed the Context
    # before a system can be created - without it System_Create returns ERR_INTERNAL and says
    # nothing more useful than that - so the jar is every bit as required as the .so.
    $jar = Get-ChildItem $staging -Recurse -File -Filter 'fmod.jar' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($jar) {
        Copy-Item $jar.FullName (Join-Path $libRoot 'fmod.jar') -Force
        Write-Step ("  {0,-14} {1:N1} KB" -f 'fmod.jar', ($jar.Length / 1KB))
    }
    else {
        Write-Host "  WARNING: no fmod.jar in that archive. Android needs it as well as the .so;"
        Write-Host "           without it FMOD fails to start with ERR_INTERNAL."
    }

    Write-Step "  FMOD ready for $copied ABI(s)."
    exit 0
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
