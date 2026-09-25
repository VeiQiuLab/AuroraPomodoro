; AuroraPomodoro Inno Setup script (v0.1.1 baseline).
; Tracked packaging source. Build output goes to build/installer/ (ignored).

#define AppName "AuroraPomodoro"
#define AppVersion "0.1.1"
#define AppPublisher "VeiQiuLab"
#define AppExeName "AuroraPomodoro.exe"

; Stable AppId — MUST remain identical across all future versions.
#define AppId "{{6E2B0F3A-9C4D-4E7B-8A1F-2D5C7B9E4A31}"

; Source = verified installer manifest (pure WPF runtime files, no PDB).
#define SourceDir "..\build\installer-proof\AuroraPomodoro"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
; Per-user install, no elevation for AuroraPomodoro itself.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
DefaultDirName={localappdata}\Programs\AuroraPomodoro
DefaultGroupName=AuroraPomodoro
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
OutputDir=..\build\installer
OutputBaseFilename=AuroraPomodoro-0.1.1-win-x64-setup
; x64-only application.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Running-app guard via the product's single-instance mutex.
AppMutex=Local\AuroraPomodoro.SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\AuroraPomodoro.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\AuroraPomodoro.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\AuroraPomodoro.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\AuroraPomodoro.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AuroraPomodoro"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,AuroraPomodoro}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AuroraPomodoro"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,AuroraPomodoro}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0';

// --- .NET Desktop Runtime 10 (x64) detection ---
function IsDotNetDesktop10Installed(): Boolean;
var
  FindRec: TFindRec;
  Base: String;
begin
  Result := False;
  Base := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(Base) then
    Exit;
  if FindFirst(Base + '\10.*', FindRec) then
  begin
    try
      repeat
        // Any 10.x WindowsDesktop runtime directory counts.
        Result := True;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrCode: Integer;
begin
  Result := True;

  if not IsDotNetDesktop10Installed() then
  begin
    if MsgBox('AuroraPomodoro requires Microsoft .NET Desktop Runtime 10 (x64).' + #13#10 +
              'Please install it, then run Setup again.' + #13#10 + #13#10 +
              'Open the official .NET download page now?',
              mbCriticalError, MB_YESNO) = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
    Result := False;
    Exit;
  end;
end;

// --- Upgrade cleanup: remove stale 0.1.0 native AuroraGlass files ---
// 0.1.0 shipped a native HwndHost path (AuroraGlass.Wpf.dll,
// AuroraGlassWpfInterop.dll, shaders/). 0.1.1 no longer uses them; remove any
// leftovers so an in-place upgrade does not keep dead native binaries.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Stale: String;
begin
  if CurStep = ssPostInstall then
  begin
    Stale := ExpandConstant('{app}\AuroraGlass.Wpf.dll');
    if FileExists(Stale) then DeleteFile(Stale);

    Stale := ExpandConstant('{app}\AuroraGlassWpfInterop.dll');
    if FileExists(Stale) then DeleteFile(Stale);

    Stale := ExpandConstant('{app}\shaders');
    if DirExists(Stale) then DelTree(Stale, True, True, True);
  end;
end;
