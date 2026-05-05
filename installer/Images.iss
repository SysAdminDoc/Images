; =============================================================================
; Images — Inno Setup installer script
;
; Builds a single-file .exe installer that sits alongside the portable zip in
; every GitHub Release. Installs per-machine (Program Files) with admin elevation
; so upgrades can remove stale per-user installs and provision Windows OCR.
;
; Compile:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion=0.2.10 installer\Images.iss
;
; The workflow passes the version on the command line. Default below matches
; src/Images/Images.csproj so a local test compile without /D still produces a
; sensibly-versioned installer.
; =============================================================================

#ifndef MyAppVersion
  #define MyAppVersion "0.2.10"
#endif

#define MyAppName        "Images"
#define MyAppPublisher   "SysAdminDoc"
#define MyAppURL         "https://github.com/SysAdminDoc/Images"
#define MyAppExeName     "Images.exe"
#define MyAppDescription "Windows 7-style classic image viewer, reimagined in dark mode"
#define MyAppProgID      "Images.File"

[Setup]
; A stable AppId lets future installers auto-upgrade this one rather than
; installing a second copy side-by-side. Generated once with [guid]::NewGuid()
; for the first installer release and must never change.
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

PrivilegesRequired=admin

; Modern 64-bit only. Anything < 1809 (17763) lacks several WIC and shell
; integration paths the app uses; the release itself is self-contained.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

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
Name: "fileassoc"; Description: "Add to ""Open with"" menu for supported image, design, RAW, and document formats"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
; ..\publish is the self-contained dotnet publish output the workflow generates
; right before invoking ISCC. A local compile needs to run
; `dotnet publish ... --self-contained true -o publish` from the repo root first.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Install-OcrCapability.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

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
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}"; ValueType: string; ValueName: ""; ValueData: "Image"; Flags: uninsdeletekey; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\{#MyAppProgID}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Check: ShouldInstallFileAssociations

; OpenWithProgids — one line per extension. uninsdeletevalue cleans the single
; value we added without touching any sibling entries (so the user's default
; survives uninstall intact).
Root: HKA; Subkey: "Software\Classes\.jpg\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jfif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.png\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.gif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.heic\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.heif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.avif\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jxl\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.tif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.bmp\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ico\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.psd\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpe\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dib\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rle\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.cur\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.hdp\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jxr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.wdp\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.hif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.psb\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.tga\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.targa\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pcx\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dds\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.qoi\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.exr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.hdr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pic\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dpx\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.cin\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.sgi\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rgb\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rgba\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.bw\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jp2\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.j2k\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.j2c\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpc\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpx\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.xpm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.xbm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pbm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pgm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ppm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pnm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pam\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pfm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.miff\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.mng\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jng\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dcm\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dicom\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.fits\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.fit\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.fts\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.xcf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ora\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pict\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pct\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ras\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.sun\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.xwd\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.fax\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.g3\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.g4\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.svg\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.svgz\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.emf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.wmf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pdf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ps\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ps2\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ps3\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.eps\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.epsf\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.epsi\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.epi\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ept\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ai\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.cr2\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.cr3\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.crw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.nef\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.nrw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.arw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.srf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.sr2\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dng\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.raf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rw2\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.orf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pef\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.3fr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.erf\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.mef\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.mrw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.x3f\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rwl\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.iiq\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.kdc\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dcr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.srw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.mos\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.fff\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.gpr\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.bay\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.cap\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jif\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.apng\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.wbmp\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jpt\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.jps\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pgx\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.dcx\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.rle\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.otb\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pcd\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pcds\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.picon\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pix\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pwp\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.sfw\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.tim\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.vicar\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.viff\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.vips\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.xv\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.six\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.sixel\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.farbfeld\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ff\OpenWithProgids";   ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.wpg\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.mvg\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.msvg\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.pdfa\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.epdf\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ept2\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.ept3\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\Classes\.k25\OpenWithProgids";  ValueType: string; ValueName: "{#MyAppProgID}"; ValueData: ""; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations

; Registered Applications — surfaces Images in Settings > Default Apps. The
; user can then pick it as the default per-extension without us touching that
; choice ourselves.
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#MyAppDescription}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heic"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heif"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avif"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jxl";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psd";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".psb";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tga";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dds";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".qoi";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".exr";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".hdr";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dpx";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jp2";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dcm";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".fits"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".xcf";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ora";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".svg";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".eps";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ps";   ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ai";   ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cr2";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".cr3";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".nef";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".arw";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dng";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".raf";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rw2";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".orf";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pef";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jif";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".apng"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wbmp"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpt";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jps";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pgx";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dcx";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rle";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".otb";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pcd";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pcds"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".picon"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pix";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pwp";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sfw";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tim";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vicar"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".viff"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vips"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".xv";   ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".six";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".sixel"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations

Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".farbfeld"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ff";   ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wpg";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mvg";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".msvg"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdfa"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".epdf"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ept2"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ept3"; ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".k25";  ValueData: "{#MyAppProgID}"; Check: ShouldInstallFileAssociations

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  CarryFileAssociations: Boolean;

function IsImagesAssociatedInRoot(RootKey: Integer): Boolean;
var
  ProgId: String;
begin
  Result :=
    RegValueExists(RootKey, 'Software\RegisteredApplications', '{#MyAppName}') or
    RegKeyExists(RootKey, 'Software\Classes\{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.jpg\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.jpeg\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.png\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.gif\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.webp\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.tif\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.tiff\OpenWithProgids', '{#MyAppProgID}') or
    RegValueExists(RootKey, 'Software\Classes\.bmp\OpenWithProgids', '{#MyAppProgID}');

  if not Result then
  begin
    if RegQueryStringValue(RootKey, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpg\UserChoice', 'ProgId', ProgId) and
       (CompareText(ProgId, '{#MyAppProgID}') = 0) then
    begin
      Result := True;
    end;
  end;

  if not Result then
  begin
    if RegQueryStringValue(RootKey, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.png\UserChoice', 'ProgId', ProgId) and
       (CompareText(ProgId, '{#MyAppProgID}') = 0) then
    begin
      Result := True;
    end;
  end;
end;

function ShouldInstallFileAssociations: Boolean;
begin
  Result := CarryFileAssociations or WizardIsTaskSelected('fileassoc');
end;

function ExtractExeFromCommand(Command: String): String;
var
  EndQuote: Integer;
  ExePos: Integer;
  Tail: String;
begin
  Result := '';
  Command := Trim(Command);
  if Command = '' then
  begin
    Exit;
  end;

  if Copy(Command, 1, 1) = '"' then
  begin
    Tail := Copy(Command, 2, Length(Command) - 1);
    EndQuote := Pos('"', Tail);
    if EndQuote > 0 then
    begin
      Result := Copy(Tail, 1, EndQuote - 1);
    end;
  end
  else
  begin
    ExePos := Pos('.exe', Lowercase(Command));
    if ExePos > 0 then
    begin
      Result := Copy(Command, 1, ExePos + 3);
    end;
  end;
end;

function StartsWithPath(Path: String; Prefix: String): Boolean;
begin
  Path := AddBackslash(Lowercase(Path));
  Prefix := AddBackslash(Lowercase(Prefix));
  Result := Copy(Path, 1, Length(Prefix)) = Prefix;
end;

function IsTrustedMachineUninstaller(UninstallerPath: String): Boolean;
var
  FileName: String;
begin
  FileName := Lowercase(ExtractFileName(UninstallerPath));
  Result :=
    FileExists(UninstallerPath) and
    (Copy(FileName, 1, 5) = 'unins') and
    (StartsWithPath(UninstallerPath, ExpandConstant('{pf}\{#MyAppName}')) or
     StartsWithPath(UninstallerPath, ExpandConstant('{pf32}\{#MyAppName}')));
end;

function RunMachineUninstallerFromKey(RootKey: Integer; SubkeyName: String): Boolean;
var
  UninstallCommand: String;
  UninstallerPath: String;
  ResultCode: Integer;
begin
  Result := True;

  if not RegQueryStringValue(RootKey, SubkeyName, 'UninstallString', UninstallCommand) then
  begin
    Exit;
  end;

  UninstallerPath := ExtractExeFromCommand(UninstallCommand);
  if not IsTrustedMachineUninstaller(UninstallerPath) then
  begin
    Log('Refusing to run untrusted Images uninstall command: ' + UninstallCommand);
    Result := False;
    Exit;
  end;

  Log('Uninstalling existing machine-wide Images install: ' + UninstallerPath);
  if not Exec(UninstallerPath, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Failed to launch existing Images uninstaller.');
    Result := False;
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Log('Existing Images uninstaller failed with exit code ' + IntToStr(ResultCode) + '.');
    Result := False;
  end;
end;

procedure RemovePerUserInstallShadow;
var
  UserInstallDir: String;
begin
  UserInstallDir := ExpandConstant('{localappdata}\Programs\{#MyAppName}');
  if DirExists(UserInstallDir) then
  begin
    Log('Removing stale per-user Images install directory: ' + UserInstallDir);
    DelTree(UserInstallDir, True, True, True);
  end;

  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C6B0A2A4-4F3E-4F6B-8F9A-1A2B3C4D5E6F}_is1');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\{#MyAppProgID}');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Applications\{#MyAppExeName}');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\{#MyAppName}\Capabilities');
  RegDeleteValue(HKCU, 'Software\RegisteredApplications', '{#MyAppName}');
  DeleteFile(ExpandConstant('{userprograms}\{#MyAppName}\{#MyAppName}.lnk'));
  DeleteFile(ExpandConstant('{userprograms}\{#MyAppName}\{cm:UninstallProgram,{#MyAppName}}.lnk'));
  DelTree(ExpandConstant('{userprograms}\{#MyAppName}'), True, True, True);
end;

function InitializeSetup: Boolean;
begin
  CarryFileAssociations := IsImagesAssociatedInRoot(HKCU) or IsImagesAssociatedInRoot(HKLM);
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not RunMachineUninstallerFromKey(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C6B0A2A4-4F3E-4F6B-8F9A-1A2B3C4D5E6F}_is1') then
  begin
    Result := 'Could not uninstall the existing machine-wide Images installation.';
    Exit;
  end;

  RemovePerUserInstallShadow;
end;

procedure InstallOcrCapability;
var
  ResultCode: Integer;
  PowerShellPath: String;
  ScriptPath: String;
  LogPath: String;
  Parameters: String;
begin
  if not IsAdminInstallMode then
  begin
    Log('Skipping Windows OCR optional capability install because this is a non-administrative install.');
    Exit;
  end;

  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  ScriptPath := ExpandConstant('{tmp}\Install-OcrCapability.ps1');
  LogPath := ExpandConstant('{app}\Images-OCR-capability.log');
  Parameters := '-NoProfile -ExecutionPolicy RemoteSigned -File "' + ScriptPath + '" -LogPath "' + LogPath + '"';

  Log('Installing Windows OCR optional capability.');
  if not Exec(PowerShellPath, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    RaiseException('Could not start Windows OCR optional capability installer.');
  end;

  if ResultCode <> 0 then
  begin
    RaiseException('Windows OCR optional capability installation failed. See ' + LogPath + ' for details.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallOcrCapability;
  end;
end;
