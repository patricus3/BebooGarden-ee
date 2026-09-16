@echo off
rem ============================================================================
rem  Builds everything that ships: the game, the standalone mods, the installer,
rem  and the Android head once that project exists.
rem
rem    build_all.bat            incremental build
rem    build_all.bat clean      wipe the installer payload first
rem
rem  Why a clean switch: "dotnet publish -o" copies files in but never takes
rem  removed ones out, so a file deleted from Content stays in the payload and
rem  goes on being packaged into every installer after it. Clean costs a full
rem  re-copy of ~470 MB of audio, which is why it is not the default.
rem ============================================================================

setlocal enabledelayedexpansion
cd /d "%~dp0"

set "CONFIG=Release"
set "PAYLOAD=installer\payload"
set "DIST=dist"
set "ANDROID_PROJ=BebooGarden.Android\BebooGarden.Android.csproj"
set "BUILT_ANDROID=no"

if /i "%~1"=="clean" (
  echo [clean] Removing %PAYLOAD%
  if exist "%PAYLOAD%" rmdir /s /q "%PAYLOAD%"
)

if not exist "%DIST%" mkdir "%DIST%"

rem --- Version sanity ------------------------------------------------------
rem The version lives in two places by hand. They disagreeing means an installer
rem that announces the wrong version in Add/Remove Programs, which is the kind of
rem thing nobody notices until a player reports a bug against the wrong build.
rem No pipes and no double quotes inside these one-liners on purpose: cmd's parser
rem mangles both inside for /f backquotes, and the error lands in PowerShell where
rem it makes no sense.
rem InformationalVersion when there is one, because that is the name of the build - a release may
rem be called 3.0b1, and an assembly version may not have a letter in it.
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "[xml]$x=Get-Content 'BebooGarden\BebooGarden.csproj'; $i=@($x.Project.PropertyGroup.InformationalVersion); $n=@($x.Project.PropertyGroup.Version); if ($i.Count -gt 0 -and $i[0]) { $i[0] } else { $n[0] }"`) do set "GAMEVER=%%v"
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$l=@(Select-String -Path 'installer\BebooGarden.iss' -Pattern '^#define AppVersion')[0].Line; $l.Split([char]34)[1]"`) do set "ISSVER=%%v"

echo.
echo   game  version: %GAMEVER%
echo   setup version: %ISSVER%
if not "%GAMEVER%"=="%ISSVER%" (
  echo.
  echo   *** WARNING: BebooGarden.csproj says %GAMEVER%, BebooGarden.iss says %ISSVER%.
  echo   *** The installer will be built and named with %ISSVER%.
)

rem --- 1. The game ---------------------------------------------------------
echo.
echo [1/5] Publishing the game to %PAYLOAD%
dotnet publish BebooGarden\BebooGarden.csproj -c %CONFIG% -r win-x64 --self-contained false -o "%PAYLOAD%" --nologo
if errorlevel 1 goto :fail

rem --- 2. Standalone mods --------------------------------------------------
rem Each mod writes its dll straight into its own folder next to mod.json, so the
rem packaged mod is that one file. Loop, so a new mod needs no edit here.
echo.
echo [2/5] Building standalone mods
for /d %%m in (mods-standalone\*) do (
  if exist "%%m\src\*.csproj" (
    echo        - %%~nxm
    dotnet build "%%m\src" -c %CONFIG% --nologo -v quiet
    if errorlevel 1 goto :fail
  )
)

rem --- 3. Mods to dist -----------------------------------------------------
echo.
echo [3/5] Copying mods to %DIST%
for /d %%m in (mods-standalone\*) do (
  if exist "%%m\%%~nxm.dll" (
    copy /y "%%m\%%~nxm.dll" "%DIST%\" >nul
    if errorlevel 1 goto :fail
    echo        - %%~nxm.dll
  )
)

rem --- 4. Installer --------------------------------------------------------
echo.
echo [4/5] Building the installer
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC for /f "usebackq delims=" %%i in (`where ISCC 2^>nul`) do set "ISCC=%%i"

if not defined ISCC (
  echo.
  echo   *** Inno Setup 6 not found - skipping the installer.
  echo   *** The published game is in %PAYLOAD% and is complete.
  echo   *** Get ISCC.exe from https://jrsoftware.org/isdl.php
) else (
  echo        using "!ISCC!"
  "!ISCC!" /Q installer\BebooGarden.iss
  if errorlevel 1 goto :fail
)

rem --- 5. Android ----------------------------------------------------------
rem Needs the Android SDK and a JDK the tooling accepts. The workload will fetch
rem both, but only when asked, and it accepts Google's SDK licences on your
rem behalf when it does - so that is a prompt rather than something this script
rem does quietly. A machine JDK is not used even when there is one: the tooling
rem wants 17 or 21, and a newer one makes it fail with a misleading error.
echo.
echo [5/5] Android
set "ANDROID_SDK=%LOCALAPPDATA%\Android\Sdk"
set "ANDROID_JDK=%LOCALAPPDATA%\Android\jdk"

if not exist "%ANDROID_PROJ%" (
  echo        no Android project yet - skipped
  goto :summary
)

if not exist "%ANDROID_SDK%\platform-tools" (
  echo.
  echo   The Android SDK is not installed, so the apk cannot be built.
  echo   Installing it downloads about a gigabyte from Google and accepts their
  echo   SDK licence agreements.
  echo.
  choice /c YN /n /m "   Install the Android SDK and a JDK now? [Y/N] "
  if errorlevel 2 (
    echo        skipped - the desktop build above is complete
    goto :summary
  )
  echo        installing, this takes a few minutes...
  dotnet build "%ANDROID_PROJ%" -t:InstallAndroidDependencies -f net10.0-android ^
    -p:AndroidSdkDirectory="%ANDROID_SDK%" -p:JavaSdkDirectory="%ANDROID_JDK%" ^
    -p:AcceptAndroidSDKLicenses=True --nologo -v quiet
  if errorlevel 1 goto :fail
)

rem FMOD's Android libraries are not redistributable, so they may well not be here.
rem Worth saying plainly, because the apk builds perfectly well without them and
rem then has no sound at all, which is a confusing thing to discover on a phone.
set "FMODFOUND="
if exist "BebooGarden.Android\lib\android\arm64-v8a\libfmod.so" set "FMODFOUND=yes"
if exist "BebooGarden.Android\lib\android\armeabi-v7a\libfmod.so" set "FMODFOUND=yes"
if exist "BebooGarden.Android\lib\android\x86_64\libfmod.so" set "FMODFOUND=yes"
if not defined FMODFOUND (
  echo.
  echo   *** No libfmod.so - the apk will build but will have no sound.
  echo   *** Run download_deps.bat to sort that out.
  echo.
)

dotnet publish "%ANDROID_PROJ%" -c %CONFIG% -f net10.0-android ^
  -p:AndroidSdkDirectory="%ANDROID_SDK%" -p:JavaSdkDirectory="%ANDROID_JDK%" ^
  -o "%DIST%\android" --nologo -v quiet
if errorlevel 1 goto :fail

rem The apk comes out named after the package id, which tells you nothing about
rem which build it is. Put a versioned copy next to the Windows installer so the
rem two releases sit together and are named the same way.
set "APKNAME=BebooGarden-EnhancedEdition-v%ISSVER%.apk"
for %%f in ("%DIST%\android\*-Signed.apk") do (
  copy /y "%%f" "%DIST%\%APKNAME%" >nul
  if errorlevel 1 goto :fail
  set "BUILT_ANDROID=yes"
)

:summary

rem --- Summary -------------------------------------------------------------
echo.
echo ============================================================
echo  Done.
echo.
echo   game      %PAYLOAD%\BebooGarden.exe
if defined ISCC echo   installer %DIST%\BebooGarden-EnhancedEdition-v%ISSVER%-setup.exe
if "%BUILT_ANDROID%"=="yes" echo   apk       %DIST%\%APKNAME%
if "%BUILT_ANDROID%"=="yes" if not defined FMODFOUND echo             ^(silent - run download_deps.bat for FMOD^)
echo ============================================================
echo.
endlocal
exit /b 0

:fail
echo.
echo ============================================================
echo  BUILD FAILED (exit code %errorlevel%^)
echo ============================================================
endlocal
exit /b 1
