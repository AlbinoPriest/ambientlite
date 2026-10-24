#define AppVersion "0.3.1"

[Setup]
AppId={{20EE0678-1A5E-4872-91F6-70B9106B0B8A}
AppName=AmbientLite
AppVersion={#AppVersion}
AppPublisher=AlbinoPriest
AppPublisherURL=https://github.com/AlbinoPriest/ambientlite
AppSupportURL=https://github.com/AlbinoPriest/ambientlite/issues
AppUpdatesURL=https://github.com/AlbinoPriest/ambientlite/releases/latest
DefaultDirName={localappdata}\Programs\AmbientLite
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=dist
OutputBaseFilename=AmbientLiteSetup
SetupIconFile=AmbientLite.ico
UninstallDisplayIcon={app}\AmbientLite.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark
WizardSizePercent=110
DisableWelcomePage=no
DisableReadyPage=yes
SetupMutex=Local\AmbientLite.Setup
CloseApplications=no
RestartApplications=no
UninstallDisplayName=AmbientLite
VersionInfoDescription=AmbientLite Setup
VersionInfoProductName=AmbientLite
VersionInfoVersion=0.3.1.0

[Tasks]
Name: desktopicon; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: AmbientLite.exe; DestDir: "{app}"; Flags: ignoreversion
Source: AmbientLite.exe; DestName: AmbientLite.Shutdown.exe; Flags: dontcopy
Source: AmbientLite.Diagnostics.exe; DestDir: "{app}"; Flags: ignoreversion
Source: LICENSE; DestDir: "{app}"; Flags: ignoreversion
Source: SwooshDrop-LICENSE.txt; DestDir: "{app}"; Flags: ignoreversion
Source: README.md; DestDir: "{app}"; Flags: ignoreversion
Source: docs\*; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\AmbientLite"; Filename: "{app}\AmbientLite.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\AmbientLite"; Filename: "{app}\AmbientLite.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AmbientLite.exe"; Description: "Open AmbientLite"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\installed.marker"

[Messages]
WelcomeLabel1=Your glow, ready when you are.
WelcomeLabel2=AmbientLite brings the colours on your screen into your room.%n%nSetup installs the app for your Windows account. No administrator access or background service is needed.%n%nYou can enable Start with Windows inside the app whenever you like.
FinishedLabel=AmbientLite is ready.%n%nExit SyncLight, check Strip setup, and choose Screen sync or Static colour. Windows startup is optional in the Startup page.

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('AmbientLite needs Microsoft .NET Framework 4.8 or later. Install it from Microsoft, then run setup again.', mbCriticalError, MB_OK, IDOK);
end;

function StopInstalledApp(ShutdownHelper: String): String;
var
  ResultCode, Attempt, Line: Integer;
  Exe, TraceFile: String;
  TraceLines: TArrayOfString;
begin
  Result := '';
  Exe := ExpandConstant('{app}\AmbientLite.exe');
  TraceFile := ExpandConstant('{tmp}\AmbientLite-shutdown.log');
  if FileExists(Exe) then begin
    if not Exec(ShutdownHelper, '--shutdown --lifecycle-log "' + TraceFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Result := 'Please exit AmbientLite from its tray controls, then try again.'
    else if ResultCode <> 0 then
      Result := 'AmbientLite is still closing. Exit it from its tray controls, then try again.';
    if LoadStringsFromFile(TraceFile, TraceLines) then
      for Line := 0 to GetArrayLength(TraceLines) - 1 do Log(TraceLines[Line]);
  end;
  if Result = '' then begin
    for Attempt := 1 to 50 do begin
      if not CheckForMutexes('Local\AmbientLite.Robobloq') then Exit;
      Sleep(100);
    end;
    Result := 'Please exit the running copy of AmbientLite from its tray controls, then try again.';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { Use this installer's fixed helper even when upgrading an older app. }
  ExtractTemporaryFile('AmbientLite.Shutdown.exe');
  Result := StopInstalledApp(ExpandConstant('{tmp}\AmbientLite.Shutdown.exe'));
end;

function InitializeUninstall: Boolean;
var
  Problem: String;
begin
  Problem := StopInstalledApp(ExpandConstant('{app}\AmbientLite.exe'));
  Result := Problem = '';
  if not Result and not UninstallSilent then MsgBox(Problem, mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not SaveStringToFile(ExpandConstant('{app}\installed.marker'), 'AmbientLite installed edition', False) then
      RaiseException('Could not create the installation marker.');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usPostUninstall then begin
    if RegQueryStringValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AmbientLite', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\AmbientLite.exe') + '" --startup') = 0 then
        RegDeleteValue(HKCU64, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AmbientLite');
  end;
end;
