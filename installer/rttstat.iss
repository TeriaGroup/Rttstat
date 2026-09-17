#define MyAppName "Rttstat"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Rttstat"
#define MyAppExeName "Rttstat.exe"
#define MyAppId "{{8F3C1A2B-6D4E-4B91-9C70-A1E2B3C4D5E6}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Rttstat
DefaultGroupName=Rttstat
OutputDir=..\publish\installer
OutputBaseFilename=RttstatSetup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
AppMutex=Local\Rttstat.SingleInstance
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "autostart"; Description: "Start Rttstat with Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Rttstat"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Rttstat"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Rttstat"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Rttstat"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := True;
  if MsgBox('Also delete Rttstat settings and logs in AppData?', mbConfirmation, MB_YESNO) = IDYES then
  begin
    DelTree(ExpandConstant('{userappdata}\Rttstat'), True, True, True);
  end;
end;
