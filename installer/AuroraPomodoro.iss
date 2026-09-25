; AuroraPomodoro Inno Setup script (v0.1.0 baseline).
; Tracked packaging source. Build output goes to build/installer/ (ignored).

#define AppName "AuroraPomodoro"
#define AppVersion "0.1.0"
#define AppPublisher "VeiQiuLab"
#define AppExeName "AuroraPomodoro.exe"

; Stable AppId — MUST remain identical across all future versions.
#define AppId "{{6E2B0F3A-9C4D-4E7B-8A1F-2D5C7B9E4A31}"

; Source = Step 22D verified installer manifest (10 runtime files, no PDB).
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
OutputBaseFilename=AuroraPomodoro-0.1.0-win-x64-setup
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
Source: "{#SourceDir}\AuroraGlass.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\AuroraGlassWpfInterop.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\shaders\background.hlsl"; DestDir: "{app}\shaders"; Flags: ignoreversion
Source: "{#SourceDir}\shaders\blur.hlsl"; DestDir: "{app}\shaders"; Flags: ignoreversion
Source: "{#SourceDir}\shaders\fullscreen_triangle.hlsl"; DestDir: "{app}\shaders"; Flags: ignoreversion
Source: "{#SourceDir}\shaders\glass.hlsl"; DestDir: "{app}\shaders"; Flags: ignoreversion

[Icons]
Name: "{group}\AuroraPomodoro"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,AuroraPomodoro}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AuroraPomodoro"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,AuroraPomodoro}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0';
  VcRedistDownloadUrl = 'https://aka.ms/vc14/vc_redist.x64.exe';

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

// --- VC++ x64 Redistributable detection (official registry evidence) ---
function IsVcRedistX64Installed(): Boolean;
var
  Installed: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) then
    Result := (Installed = 1);
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

  if not IsVcRedistX64Installed() then
  begin
    if MsgBox('AuroraPomodoro requires the Microsoft Visual C++ x64 Redistributable.' + #13#10 +
              'Please install the latest supported x64 package, then run Setup again.' + #13#10 + #13#10 +
              'Open the official download now?',
              mbCriticalError, MB_YESNO) = IDYES then
      ShellExec('open', VcRedistDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
    Result := False;
    Exit;
  end;
end;
