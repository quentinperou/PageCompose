; PageCompose installer (Inno Setup 6), in English and French (picked from the Windows display language).
; Built by scripts\build.ps1, which prepares dist\app first:
;   ISCC.exe /DAppVersion=1.0.0 installer\PageCompose.iss
; The app also runs it to install an update (src\PageCompose.App\Services\Updater.cs): silent, closing the
; running app, /RELAUNCH=1.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

; Windows file versions are numbers only: 1.1.0 for a 1.1.0-beta.1 pre-release.
#if Pos("-", AppVersion) > 0
  #define AppFileVersion Copy(AppVersion, 1, Pos("-", AppVersion) - 1)
#else
  #define AppFileVersion AppVersion
#endif

#define AppName "PageCompose"
#define AppExe "PageCompose.exe"

[Setup]
AppId={{76079F6D-B1BF-47D2-978C-5BFECA259217}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=QuentinPerou
AppPublisherURL=https://github.com/quentinperou
AppSupportURL=https://github.com/quentinperou/PageCompose
AppUpdatesURL=https://github.com/quentinperou/PageCompose/releases
VersionInfoVersion={#AppFileVersion}
DefaultDirName={autopf}\PageCompose
DisableProgramGroupPage=yes
; Admin rights: installs for every user of the PC (shop computers), and lets the .NET runtime be installed.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=PageCompose-Setup-{#AppVersion}
SetupIconFile=..\src\PageCompose.App\Assets\PageCompose.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
; Preset files (.PageComposePreset) and projects (.PageComposeProject) open in the app: Explorer is told to refresh their icons.
ChangesAssociations=yes
; Only ask for the language when Windows uses one the installer does not have.
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
english.RuntimeMissing=PageCompose needs the Microsoft ".NET 10 Desktop Runtime", which is not installed on this PC.%nInstall it now?
french.RuntimeMissing=PageCompose a besoin du « .NET 10 Desktop Runtime » (Microsoft), absent de ce PC.%nL'installer maintenant ?
english.RuntimeInstalling=Installing the .NET 10 Desktop Runtime...
french.RuntimeInstalling=Installation du .NET 10 Desktop Runtime…
english.RuntimeFailed=The automatic installation failed. The download page will open:%nchoose ".NET Desktop Runtime 10" (x64).
french.RuntimeFailed=L'installation automatique a échoué. La page de téléchargement va s'ouvrir :%nchoisissez « .NET Desktop Runtime 10 » (x64).
english.DeleteUserData=Also delete your PageCompose settings and presets?%n%nKeep them if you are going to install PageCompose again.
french.DeleteUserData=Supprimer aussi vos réglages et vos préréglages PageCompose ?%n%nGardez-les si vous allez réinstaller PageCompose.
english.PresetFileType=PageCompose presets
french.PresetFileType=Préréglages PageCompose
english.ProjectFileType=PageCompose project
french.ProjectFileType=Projet PageCompose

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\PageCompose.App\Assets\PresetFile.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\PageCompose.App\Assets\ProjectFile.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Preset files exported from Settings: their own icon, and a double click imports them (PresetStore.FileExtension).
; Up to 0.6.0 they ended in .PageCompose: that association goes (the author wants no compatibility).
Root: HKA; Subkey: "Software\Classes\.PageCompose"; ValueType: none; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\.PageComposePreset"; ValueType: string; ValueName: ""; ValueData: "PageCompose.Presets"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.PageComposePreset\OpenWithProgids"; ValueType: string; ValueName: "PageCompose.Presets"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PageCompose.Presets"; ValueType: string; ValueName: ""; ValueData: "{cm:PresetFileType}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PageCompose.Presets\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\PresetFile.ico"
Root: HKA; Subkey: "Software\Classes\PageCompose.Presets\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
; Projects saved with an export (ProjectStore.FileExtension): their own icon, and a double click opens them.
Root: HKA; Subkey: "Software\Classes\.PageComposeProject"; ValueType: string; ValueName: ""; ValueData: "PageCompose.Project"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.PageComposeProject\OpenWithProgids"; ValueType: string; ValueName: "PageCompose.Project"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PageCompose.Project"; ValueType: string; ValueName: ""; ValueData: "{cm:ProjectFileType}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PageCompose.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ProjectFile.ico"
Root: HKA; Subkey: "Software\Classes\PageCompose.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[UninstallDelete]
; Temporary files of the app: PDFs and photos converted to pictures, projects opened, flattened exports, the last update
; downloaded.
Type: filesandordirs; Name: "{%TEMP}\PageCompose"
Type: filesandordirs; Name: "{%TEMP}\PageCompose-Update"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser
; Update installed from the app (silent, /RELAUNCH=1): start the app again, as the user, not elevated.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser skipifnotsilent; Check: RelaunchRequested

[Code]
{ Set by the app when it installs an update (Updater.InstallerArguments). }
function RelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

{ The app is framework-dependent: it needs the .NET 10 Desktop Runtime. }
function IsDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

procedure EnsureDesktopRuntime;
var
  ResultCode: Integer;
begin
  if IsDesktopRuntimeInstalled then
    Exit;
  if MsgBox(CustomMessage('RuntimeMissing'), mbConfirmation, MB_YESNO) <> IDYES then
    Exit;
  WizardForm.StatusLabel.Caption := CustomMessage('RuntimeInstalling');
  if not Exec('winget.exe', 'install --id Microsoft.DotNet.DesktopRuntime.10 -e --silent --accept-source-agreements --accept-package-agreements',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or not IsDesktopRuntimeInstalled then
  begin
    MsgBox(CustomMessage('RuntimeFailed'), mbInformation, MB_OK);
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    EnsureDesktopRuntime;
end;

{ Settings and presets (%LOCALAPPDATA%\PageCompose, of the account that uninstalls): kept unless the user says
  otherwise, "No" being the default, so that a reinstall finds them. A silent uninstall keeps them. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent and
     (MsgBox(CustomMessage('DeleteUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    DelTree(ExpandConstant('{localappdata}\PageCompose'), True, True, True);
end;
