; Tone And Beats By Hostility - Installer Script
; Version is auto-generated from CMakeLists.txt via version_info.iss

#include "version_info.iss"

#define MyAppName "Tone And Beats By Hostility"
#define MyAppPublisher "Hostility"
#define MyAppURL "https://www.hostilitymusic.com"
#define MyAppExeName "Tone And Beats By Hostility.exe"

[Setup]
AppId={{D3E64E3D-2F3B-4C9E-9A5A-7C1C9A9C9C9C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
;PrivilegesRequired=lowest
OutputDir=..\Installers
OutputBaseFilename=ToneAndBeats_v{#MyAppVersion}_Installer
SetupIconFile=..\Assets\icono.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\build\ToneAndBeats_artefacts\Release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
