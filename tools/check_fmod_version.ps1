<#
Checks that the fmod.dll about to be packaged matches the bindings that will call it.

FMOD compares its header against its library in System::init and refuses to start on a mismatch
(ERR_HEADER_MISMATCH), so getting this wrong produces a game that installs perfectly and then dies
on the first line of Main. That is exactly what shipped in 3.0b1: the bindings had moved to 2.03.14
and the dll in the payload was still 2.02.14, because CopyToOutputDirectory=PreserveNewest compares
timestamps and the new dll carried FMOD's own build date, which was older than the stale copy.

The build now copies that file every time, so this should never fire again. It is here because the
failure is silent until launch and costs a release when it is missed.

    tools\check_fmod_version.ps1 -Dll installer\payload\lib\fmod.dll
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Dll,
    [string]$Bindings = 'BebooGarden.Core\Fmod\fmod.cs'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Dll)) {
    Write-Host "  fmod.dll not found at $Dll"
    exit 1
}

if (-not (Test-Path $Bindings)) {
    Write-Host "  bindings not found at $Bindings; version unchecked"
    exit 0
}

# public const int number = 0x00020314;
$match = Select-String -Path $Bindings -Pattern 'public const int\s+number\s*=\s*0x([0-9A-Fa-f]+)' |
    Select-Object -First 1
if (-not $match) {
    Write-Host "  could not read VERSION.number from $Bindings; version unchecked"
    exit 0
}

$number = [Convert]::ToUInt32($match.Matches[0].Groups[1].Value, 16)

# FMOD writes its version as the hex digits read literally: 0x00020314 is 2.03.14, and the dll
# calls that "2.3.14". So format each part as hex, not as decimal.
$major = ($number -shr 16) -band 0xFFFF
$minor = ($number -shr 8) -band 0xFF
$patch = $number -band 0xFF
$expected = '{0:X}.{1:X}.{2:X}' -f $major, $minor, $patch

$actual = (Get-Item $Dll).VersionInfo.FileVersion
if (-not $actual) {
    Write-Host "  $Dll has no version resource; unchecked"
    exit 0
}

# The dll reports "2.3.14 (build 164239)".
$actualShort = ($actual -split ' ')[0]

if ($actualShort -ne $expected) {
    Write-Host ""
    Write-Host "  *** FMOD MISMATCH - this build would not start."
    Write-Host "  ***   bindings expect $expected  ($Bindings)"
    Write-Host "  ***   dll supplies    $actualShort  ($Dll)"
    Write-Host "  *** FMOD refuses to initialise across a version gap (ERR_HEADER_MISMATCH),"
    Write-Host "  *** so the game would install and then die on startup."
    Write-Host ""
    exit 1
}

Write-Host "        fmod $actualShort matches the bindings"
exit 0
