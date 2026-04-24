; =============================================================================
; Images — Inno Setup installer script
;
; Builds a single-file .exe installer that sits alongside the portable zip in
; every GitHub Release. Default install is per-machine (Program Files) with
; admin elevation; users can override to per-user at the UAC prompt via
; PrivilegesRequiredOverridesAllowed.
;
; Compile:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion=0.1.4 installer\Images.iss
;
; The workflow passes the version on the command line. Default below matches
; src/Images/Images.csproj so a local test compile without /D still produces a
; sensibly-versioned installer.
; =============================================================================

#ifndef MyAppVersion
  #define MyAppVersion "0.1.5"
#endif

#define MyAppName        "Images"
#define MyAppPublisher   "SysAdminDoc"
#define MyAppURL         "https://github.com/SysAdminDoc/Images"
#define MyAppExeName     "Images.exe"
#define MyAppDescription "Windows 7-style classic image viewer, reimagined in dark mode"
#define MyAppProgID      "Images.File"

[Setup]
; A stable AppId lets future installers auto-upgrade this one rather than
; installing a second copy side-by-side. Generate once and never change.
AppId={{C6B0A2A4-4F3E-4F6B-8F9A-1A2B3C4D5E6F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} v{#MyAppVersion}

; Admin by default; the user can pick per-user at the elevation prompt
; (flips {autopf} to %LOCALAPPDATA%\Programs and registry writes to HKCU).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline

; Modern 64-bit only. Anything < 1809 (17763) lacks the WIC HEIF/AVIF codec
; path we lean on and cannot install the .NET 9 Desktop Runtime anyway.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

LicenseFile=..\LICENSE
SetupIconFile=..\src\Images\Resources\icon.ico
OutputDir=output
OutputBaseFilename=Images-v{#MyAppVersion}-setup-win-x64

; Keep the wizard clean — the portable zip is documented for users who want
; zero install, so the welcome page stays minimal.
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Add to ""Open with"" menu for common image types (JPG, PNG, GIF, WEBP, HEIC, AVIF, TIFF, BMP, ICO)"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
; ..\publish is the dotnet publish output the workflow generates right before
; invoking ISCC. A local compile needs to run `dotnet publish ... -o publish`
; from the repo root first.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; -----------------------------------------------------------------------------
; File association strategy — non-destructive.
;
; We register a ProgID ("Images.File") and add it to the "OpenWithProgids"
; subkey for each extension. This makes Images appear in the "Open with" menu
; and in Settings > Default Apps without overwriting whatever the user
; currently has as the default for that extension. Same pattern Photoshop,
; GIMP, and IrfanView use.
; -----------------------------------------------------------------------------

; ProgID itself
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}"; ValueType: string; ValueName: ""; ValueData: "Image"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Tasks: fileassoc

; OpenWithProgids — one line per extension. uninsdeletevalue cleans the single
; value we added without touching any sibling entries (so the user's default
; survives uninstall intact).
Root: HKA; Subkey: "Software\Classes\.jpg\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.jfif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.png\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.gif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.heic\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.heif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.avif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.jxl\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.tif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.bmp\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.ico\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.psd\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

; Registered Applications — surfaces Images in Settings > Default Apps. The
; user can then pick it as the default per-extension without us touching that
; choice ourselves.
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#MyAppDescription}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heic"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heif"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avif"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jxl";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psd";  ValueData: "{#MyAppProgID}"; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// -----------------------------------------------------------------------------
// .NET 9 Desktop Runtime prerequisite check.
// The framework-dependent publish needs Microsoft.WindowsDesktop.App 9.x.
// We probe the on-disk shared-framework directory — it's the authoritative
// source that both the CLI and the runtime loader agree on, and it skips the
// registry quirks between per-machine vs per-user dotnet installs.
// -----------------------------------------------------------------------------

function IsDotNet9DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
  SearchPath: String;
  Found: Boolean;
begin
  Found := False;
  // Per-machine x64 install (most common).
  SearchPath := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(SearchPath) then
  begin
    if FindFirst(SearchPath + '\9.*', FindRec) then
    begin
      try
        Found := True;
      finally
        FindClose(FindRec);
      end;
    end;
  end;

  // Fallback: some enterprise images ship the runtime under the ProgramFiles
  // redirection layer (32-bit view on 64-bit) or as a user-profile install.
  if not Found then
  begin
    SearchPath := ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App');
    if DirExists(SearchPath) then
    begin
      if FindFirst(SearchPath + '\9.*', FindRec) then
      begin
        try
          Found := True;
        finally
          FindClose(FindRec);
        end;
      end;
    end;
  end;

  Result := Found;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  Response: Integer;
begin
  Result := True;
  if IsDotNet9DesktopRuntimeInstalled then
    Exit;

  Response := MsgBox(
    '{#MyAppName} needs the .NET 9 Desktop Runtime to run.' + #13#10 + #13#10 +
    'Click OK to open the Microsoft download page. After installing the runtime, run this setup again.' + #13#10 + #13#10 +
    'Choose "Desktop Runtime" (not ASP.NET or the SDK) and match your CPU architecture (x64).',
    mbInformation, MB_OKCANCEL);

  if Response = IDOK then
  begin
    ShellExec('open',
      'https://dotnet.microsoft.com/download/dotnet/9.0/runtime?cid=getdotnetcore&runtime=desktop',
      '', '', SW_SHOW, ewNoWait, ErrorCode);
  end;

  Result := False;
end;
