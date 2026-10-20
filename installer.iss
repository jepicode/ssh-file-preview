#define AppName "SSH File Preview"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "Jepi Usuluddin"
#define AppURL "https://github.com/jepicode/ssh-file-preview"
#define AppExeName "SshFilePreview.exe"

[Setup]
AppId={{91d022b1-2db1-46c0-9c80-4f5f1e02e1d6}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={localappdata}\Programs\SSH File Preview
DefaultGroupName={#AppName}
DisableProgramGroupPage=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=app.ico
OutputDir=installer-output
OutputBaseFilename=SSH-File-Preview-Setup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "installer-stage\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#AppName}"; Filename: "{app}\\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: postinstall nowait skipifsilent
