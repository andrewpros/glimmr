[Setup]
AppName=Glimmr
AppVersion=1.2.6
DefaultDirName={autopf}\Glimmr
DefaultGroupName=Glimmr
SetupIconFile=appIcon.ico
UninstallDisplayIcon=appIcon.ico
OutputDir=bin
OutputBaseFilename=Glimmr-Windows-installer

[Files]
Source: "bin\win-x86\*"; DestDir: "{app}";Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Glimmr"; Filename: "{app}\Glimmr.exe"; WorkingDir: "{app}"
Name: "{group}\Glimmr Tray"; Filename: "{app}\GlimmrTray.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Glimmr"; Filename: "{uninstallexe}"

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "Glimmr Tray"; \
    ValueData: """{app}\GlimmrTray.exe"" /login";Tasks:StartMenuEntry;
    
[Tasks]
Name: "StartMenuEntry" ; Description: "Start Glimmr when Windows starts." ; GroupDescription: "Windows Startup"; MinVersion: 4,4;
