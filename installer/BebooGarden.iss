; Installer for Beboo Garden: Enhanced Edition.
;
; Build it with:
;   dotnet publish BebooGarden\BebooGarden.csproj -c Release -r win-x64 --self-contained false ^
;       -o installer\payload
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\BebooGarden.iss
;
; The installer is written to dist\ at the root of the repository. PayloadDir and OutDir can both
; be pointed elsewhere without editing this file, e.g.
;   ISCC.exe /DPayloadDir="D:\build\payload" /DOutDir="D:\releases" installer\BebooGarden.iss
;
; This file is UTF-8 with a BOM. Inno needs the BOM to read the accented characters below.
;
; Two decisions worth knowing about:
;
; It installs for the whole machine, into Program Files, with the permissions Windows gives that
; folder and no loosening of them. That works because the game keeps nothing of the player's in its
; own folder: the save, the crash log and any mods they add live under %LocalAppData%\BebooGarden
; (see GamePaths.cs). Each account gets its own garden. Someone without administrator rights can
; still choose to install just for themselves, and everything works the same way.
;
; The build is framework dependent, and the .NET runtime is downloaded during setup only when the
; machine does not already have it. Most people never see that step, and nobody downloads a copy
; of a runtime they already have.

#define AppName "Beboo Garden: Enhanced Edition"
#define AppShortName "Beboo Garden"
#define AppVersion "3.0b3"
; Windows will only take four numbers in a file version resource, so a release named
; 3.0b1 needs a numeric one alongside the name people actually see.
#define AppVersionNumeric "3.0.0.0"
#define AppPublisher "Saladeuh"
#define AppURL "https://github.com/Saladeuh/BebooGarden"
#define AppExe "BebooGarden.exe"

; The runtime the game needs. The project sets RollForward=Major, so a later major works too.
; The aka.ms address always points at the newest patch of the 10.0 channel.
#define DotNetMajor "10"
#define DotNetUrl "https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe"
#define DotNetManualUrl "https://dotnet.microsoft.com/download/dotnet/10.0"

#ifndef PayloadDir
  #define PayloadDir "payload"
#endif
; Built installers land in dist\ at the root of the repository, which is gitignored.
#ifndef OutDir
  #define OutDir "..\dist"
#endif

[Setup]
AppId={{8E6A0F2C-4B1D-4D2A-9A3E-7C51B0E6D914}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersionNumeric}
VersionInfoProductName={#AppName}

; All users by default. Anyone without administrator rights is offered a just-for-me install
; instead, which lands in %LocalAppData%\Programs; both are correct now that nothing the player
; owns is kept in the install folder. {autopf} follows whichever mode is in force.
PrivilegesRequired=admin
; dialog: the wizard offers a just-for-me install to anyone without administrator rights.
; commandline: /CURRENTUSER does the same unattended, which is also what makes the components
; testable without an elevation prompt.
PrivilegesRequiredOverridesAllowed=commandline dialog
DefaultDirName={autopf}\{#AppShortName}
DefaultGroupName={#AppShortName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

OutputDir={#OutDir}
OutputBaseFilename=BebooGarden-EnhancedEdition-v{#AppVersion}-setup
SetupIconFile=..\BebooGarden\Icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; A blind player is the expected user. The classic wizard is the one screen readers handle best,
; and every page it can skip is a page nobody has to tab through.
WizardStyle=classic
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
; An entry with no language prefix is the fallback for any language that does not override it.
PrereqTitle=.NET runtime
PrereqSubtitle=Beboo Garden needs the Microsoft .NET runtime, which this computer does not have yet. Setup will download it.
InstallingDotNet=Installing the .NET runtime. Windows will ask for permission.
DownloadFailed=The .NET runtime could not be downloaded. You can install it yourself afterwards from %1 - the game will not start until it is there.%n%nInstall Beboo Garden anyway?
DotNetFailed=The .NET runtime was not installed. You can install it yourself afterwards from %1 - the game will not start until it is there.%n%nInstall Beboo Garden anyway?

french.PrereqTitle=Environnement .NET
french.PrereqSubtitle=Beboo Garden a besoin de l'environnement Microsoft .NET, qui n'est pas encore installé sur cet ordinateur. Le programme d'installation va le télécharger.
french.InstallingDotNet=Installation de l'environnement .NET. Windows va demander une autorisation.
french.DownloadFailed=Impossible de télécharger l'environnement .NET. Vous pourrez l'installer vous-même depuis %1 - le jeu ne démarrera pas tant qu'il ne sera pas là.%n%nInstaller Beboo Garden quand même ?
french.DotNetFailed=L'environnement .NET n'a pas été installé. Vous pourrez l'installer vous-même depuis %1 - le jeu ne démarrera pas tant qu'il ne sera pas là.%n%nInstaller Beboo Garden quand même ?

german.PrereqTitle=.NET-Laufzeitumgebung
german.PrereqSubtitle=Beboo Garden benötigt die Microsoft .NET-Laufzeitumgebung, die auf diesem Computer noch fehlt. Das Setup lädt sie herunter.
german.InstallingDotNet=Die .NET-Laufzeitumgebung wird installiert. Windows fragt gleich nach einer Berechtigung.
german.DownloadFailed=Die .NET-Laufzeitumgebung konnte nicht heruntergeladen werden. Sie können sie später selbst von %1 installieren - bis dahin startet das Spiel nicht.%n%nBeboo Garden trotzdem installieren?
german.DotNetFailed=Die .NET-Laufzeitumgebung wurde nicht installiert. Sie können sie später selbst von %1 installieren - bis dahin startet das Spiel nicht.%n%nBeboo Garden trotzdem installieren?

polish.PrereqTitle=Środowisko .NET
polish.PrereqSubtitle=Beboo Garden potrzebuje środowiska Microsoft .NET, którego nie ma jeszcze na tym komputerze. Instalator je pobierze.
polish.InstallingDotNet=Instalowanie środowiska .NET. Windows poprosi o zgodę.
polish.DownloadFailed=Nie udało się pobrać środowiska .NET. Możesz zainstalować je później samodzielnie ze strony %1 - do tego czasu gra się nie uruchomi.%n%nZainstalować mimo to Beboo Garden?
polish.DotNetFailed=Środowisko .NET nie zostało zainstalowane. Możesz zainstalować je później samodzielnie ze strony %1 - do tego czasu gra się nie uruchomi.%n%nZainstalować mimo to Beboo Garden?

brazilianportuguese.PrereqTitle=Runtime do .NET
brazilianportuguese.PrereqSubtitle=O Beboo Garden precisa do runtime do Microsoft .NET, que ainda não existe neste computador. O instalador vai baixá-lo.
brazilianportuguese.InstallingDotNet=Instalando o runtime do .NET. O Windows vai pedir permissão.
brazilianportuguese.DownloadFailed=Não foi possível baixar o runtime do .NET. Você pode instalá-lo depois a partir de %1 - o jogo não abre enquanto ele não estiver lá.%n%nInstalar o Beboo Garden mesmo assim?
brazilianportuguese.DotNetFailed=O runtime do .NET não foi instalado. Você pode instalá-lo depois a partir de %1 - o jogo não abre enquanto ele não estiver lá.%n%nInstalar o Beboo Garden mesmo assim?
TypeCustom=Choose what to install
CompGame=Beboo Garden
CompModding=Modding reference (for writing mods)
ModdingFiles=Modding files
french.TypeCustom=Choisissez ce qu'il faut installer
french.CompGame=Beboo Garden
french.CompModding=Documentation de moddage (pour écrire des mods)
french.ModdingFiles=Fichiers de moddage
german.TypeCustom=Wählen Sie aus, was installiert wird
german.CompGame=Beboo Garden
german.CompModding=Modding-Dokumentation (zum Schreiben von Mods)
german.ModdingFiles=Modding-Dateien
polish.TypeCustom=Wybierz, co zainstalować
polish.CompGame=Beboo Garden
polish.CompModding=Dokumentacja modów (do pisania modów)
polish.ModdingFiles=Pliki modów
brazilianportuguese.TypeCustom=Escolha o que instalar
brazilianportuguese.CompGame=Beboo Garden
brazilianportuguese.CompModding=Documentação de mods (para criar mods)
brazilianportuguese.ModdingFiles=Arquivos de mods

[Types]
; One type, freely editable, so the components page is a plain list of checkboxes rather than a
; combo box a screen reader has to be walked through first.
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "game"; Description: "{cm:CompGame}"; Types: custom; Flags: fixed
; Not in any type, so it starts unticked: most people are not writing mods.
Name: "modding"; Description: "{cm:CompModding}"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The game. BebooGarden.ModApi.dll is in here rather than with the reference material: the game
; itself runs against it, so it is not optional. What is optional is the documentation for it.
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "mods\MODDING.md,BebooGarden.ModApi.xml"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: game
; The modding reference: how to write one, and the API documentation your editor reads.
Source: "{#PayloadDir}\mods\MODDING.md"; DestDir: "{app}\mods"; Flags: ignoreversion; Components: modding
Source: "{#PayloadDir}\BebooGarden.ModApi.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Components: modding

[Icons]
Name: "{group}\{#AppShortName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppShortName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppShortName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; A folder rather than the file: a .md has no handler on a clean Windows, and a shortcut that opens
; nothing is worse than one more click.
Name: "{group}\{cm:ModdingFiles}"; Filename: "{app}\mods"; Components: modding

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppShortName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only what setup put here. Saves live in %LocalAppData%\BebooGarden and are left alone entirely -
; uninstalling should not take somebody's beboos away, and reinstalling should find them again.
Type: dirifempty; Name: "{app}\mods"
Type: dirifempty; Name: "{app}"

[Code]
const
  DotNetInstaller = 'dotnet-runtime-win-x64.exe';

var
  DownloadPage: TDownloadWizardPage;
  NeedDotNet: Boolean;

{ Where the shared runtimes live. The runtime installer normally records this, but not every
  machine has the key - one that arrived with Visual Studio may not - so fall back to the
  default location. }
function DotNetRoot(): String;
begin
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64',
      'InstallLocation', Result) or (Result = '') then
    Result := ExpandConstant('{commonpf64}\dotnet');
end;

{ True when a runtime the game can use is already here. Every subfolder of
  shared\Microsoft.NETCore.App is named for its version, so the major number is all we need: the
  project sets RollForward=Major, so anything from 10 upwards will run it. }
function HasDotNet(): Boolean;
var
  Dir, Name: String;
  Rec: TFindRec;
  Dot: Integer;
begin
  Result := False;
  Dir := DotNetRoot + '\shared\Microsoft.NETCore.App';
  if not DirExists(Dir) then Exit;
  if not FindFirst(Dir + '\*', Rec) then Exit;
  try
    repeat
      if (Rec.Attributes and FILE_ATTRIBUTE_DIRECTORY) = 0 then Continue;
      Name := Rec.Name;
      Dot := Pos('.', Name);
      if Dot < 2 then Continue;
      if StrToIntDef(Copy(Name, 1, Dot - 1), 0) >= {#DotNetMajor} then Result := True;
    until Result or (not FindNext(Rec));
  finally
    FindClose(Rec);
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard();
begin
  DownloadPage := CreateDownloadPage(CustomMessage('PrereqTitle'), CustomMessage('PrereqSubtitle'),
      @OnDownloadProgress);
end;

{ The download happens after the last question and before anything is written, so backing out
  here still leaves the machine untouched. A silent install never reaches this: it is handled in
  PrepareToInstall instead. }
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then Exit;

  NeedDotNet := not HasDotNet;
  if not NeedDotNet then Exit;

  DownloadPage.Clear;
  DownloadPage.Add('{#DotNetUrl}', DotNetInstaller, '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      { No connection, or Microsoft is having a day. The game installs fine without the runtime,
        it just will not start, so let the player decide rather than dead-ending them here. }
      NeedDotNet := False;
      Result := SuppressibleMsgBox(FmtMessage(CustomMessage('DownloadFailed'), ['{#DotNetManualUrl}']),
          mbError, MB_YESNO, IDYES) = IDYES;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Ok: Boolean;
begin
  Result := '';

  { A silent install skips every wizard page, NextButtonClick included, so the check and the
    download have to happen here as well or an unattended install would quietly end up with a game
    that cannot start. No page to show, so fetch the file directly. }
  if WizardSilent and not HasDotNet then
  begin
    try
      DownloadTemporaryFile('{#DotNetUrl}', DotNetInstaller, '', nil);
      NeedDotNet := True;
    except
      Log('Could not download the .NET runtime: ' + GetExceptionMessage);
    end;
  end;

  if not NeedDotNet then Exit;

  WizardForm.PreparingLabel.Caption := CustomMessage('InstallingDotNet');
  { The game needs no administrator rights, but Microsoft's runtime installer does, so this is the
    one point in setup where Windows asks. 'runas' is what raises that prompt. }
  Ok := ShellExec('runas', ExpandConstant('{tmp}\' + DotNetInstaller), '/install /quiet /norestart',
      '', SW_SHOW, ewWaitUntilTerminated, Code);
  { 1641 and 3010 both mean it worked and wants a reboot. }
  if Ok and ((Code = 1641) or (Code = 3010)) then NeedsRestart := True;
  if Ok and ((Code = 0) or (Code = 1641) or (Code = 3010)) then Exit;

  { Declined the prompt, or it failed outright. Same offer as a failed download: carry on, or stop. }
  if SuppressibleMsgBox(FmtMessage(CustomMessage('DotNetFailed'), ['{#DotNetManualUrl}']),
      mbError, MB_YESNO, IDYES) = IDNO then
    Result := FmtMessage(CustomMessage('DotNetFailed'), ['{#DotNetManualUrl}']);
end;
