; Inno Setup Script for Tone & Beats by Hostility
; Version: 1.0.12
; Last Updated: 2026-04-13

#define MyAppName "Tone & Beats by Hostility"
#define MyAppVersion "1.0.12"
#define MyAppPublisher "Hostility Music"
#define MyAppURL "https://www.hostilitymusic.com"
#define MyAppExeName "ToneAndBeatsByHostility.exe"
#define SourceDir "..\src\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=no
OutputDir=..\installer\output
OutputBaseFilename=ToneAndBeatsByHostility_Setup_v{#MyAppVersion}
SetupIconFile=..\src\Assets\HOSTBLANCO.ico
SetupLogging=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=compiler:WizModernImage.bmp
WizardSmallImageFile=compiler:WizModernSmallImage.bmp
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
ShowLanguageDialog=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Code]
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Self-contained executable (all dependencies embedded)
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Supporting files (if any additional content is needed)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; License and documentation
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Messages]
BeveledLabel=Tone & Beats by Hostility v{#MyAppVersion}
SetupAppTitle=Instalar {#MyAppName}
SetupWindowTitle=Configuración de {#MyAppName} {#MyAppVersion}
WelcomeLabel1=Bienvenido a la instalación de [name]
WelcomeLabel2=[name/ver]%n%nEsta aplicación detecta BPM y tonalidad de archivos de audio.%n%n¡Disfruta!