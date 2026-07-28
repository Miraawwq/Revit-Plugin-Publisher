#define MyAppName "BIMLEADERS Sheets"
#define MyAppVersion "1.3.0"
#define MyAppPublisher "BIMLEADERS"
#define PluginFolder "BIMLEADERS Sheets"
#define PluginAssembly "BIMLEADERS.Sheets.dll"
#define PluginManifest "BIMLEADERS.Sheets.addin"

[Setup]
AppId={{4E2A699D-B977-4C61-9ED3-E75F5CC56B19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://bimleaders.com
CreateAppDir=no
UninstallFilesDir={userappdata}\BIMLEADERS\Sheets\Uninstall
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=BIMLEADERS-Sheets-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\BIMLEADERS-Sheets.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no
UninstallDisplayName={#MyAppName} for Revit 2025-2026
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer for Autodesk Revit 2025 and 2026
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
english.VersionPageTitle=Autodesk Revit versions
english.VersionPageDescription=Select the Revit versions where BIMLEADERS Sheets will be installed.
english.Revit2025=Autodesk Revit 2025
english.Revit2026=Autodesk Revit 2026
english.SelectVersionError=Select at least one Revit version.
english.CloseRevitError=Close Autodesk Revit before installing BIMLEADERS Sheets, then run Setup again.
russian.VersionPageTitle=Версии Autodesk Revit
russian.VersionPageDescription=Выберите версии Revit, для которых будет установлен BIMLEADERS Sheets.
russian.Revit2025=Autodesk Revit 2025
russian.Revit2026=Autodesk Revit 2026
russian.SelectVersionError=Выберите хотя бы одну версию Revit.
russian.CloseRevitError=Закройте Autodesk Revit перед установкой BIMLEADERS Sheets и запустите установщик снова.
ukrainian.VersionPageTitle=Версії Autodesk Revit
ukrainian.VersionPageDescription=Виберіть версії Revit, для яких буде встановлено BIMLEADERS Sheets.
ukrainian.Revit2025=Autodesk Revit 2025
ukrainian.Revit2026=Autodesk Revit 2026
ukrainian.SelectVersionError=Виберіть хоча б одну версію Revit.
ukrainian.CloseRevitError=Закрийте Autodesk Revit перед встановленням BIMLEADERS Sheets і запустіть інсталятор знову.

[InstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\KrakhmalovSheets.addin"; Check: Install2025
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\KrakhmalovSheets"; Check: Install2025
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginManifest}"; Check: Install2025
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginFolder}"; Check: Install2025
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\KrakhmalovSheets.addin"; Check: Install2026
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\KrakhmalovSheets"; Check: Install2026
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginManifest}"; Check: Install2026
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginFolder}"; Check: Install2026

[Files]
Source: "..\bin\Release\Revit2025\net8.0-windows\{#PluginAssembly}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginFolder}"; Flags: ignoreversion; Check: Install2025
Source: "..\{#PluginManifest}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; DestName: "{#PluginManifest}"; Flags: ignoreversion; Check: Install2025
Source: "..\bin\Release\Revit2026\net8.0-windows\{#PluginAssembly}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginFolder}"; Flags: ignoreversion; Check: Install2026
Source: "..\{#PluginManifest}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; DestName: "{#PluginManifest}"; Flags: ignoreversion; Check: Install2026

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginManifest}"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginFolder}"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginManifest}"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginFolder}"

[Code]
var
  VersionPage: TWizardPage;
  Revit2025Check: TNewCheckBox;
  Revit2026Check: TNewCheckBox;

function IsRevitRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  Exec(ExpandConstant('{cmd}'), '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe" > nul', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;

function IsRevitInstalled(Version: String): Boolean;
begin
  Result := DirExists(ExpandConstant('{autopf}\Autodesk\Revit ' + Version));
end;

function InitializeSetup: Boolean;
begin
  Result := not IsRevitRunning;
  if not Result then
    MsgBox(CustomMessage('CloseRevitError'), mbError, MB_OK);
end;

procedure InitializeWizard;
begin
  VersionPage := CreateCustomPage(
    wpWelcome,
    CustomMessage('VersionPageTitle'),
    CustomMessage('VersionPageDescription'));

  Revit2025Check := TNewCheckBox.Create(VersionPage);
  Revit2025Check.Parent := VersionPage.Surface;
  Revit2025Check.Left := 0;
  Revit2025Check.Top := ScaleY(18);
  Revit2025Check.Width := VersionPage.SurfaceWidth;
  Revit2025Check.Caption := CustomMessage('Revit2025');
  Revit2025Check.Checked := IsRevitInstalled('2025');

  Revit2026Check := TNewCheckBox.Create(VersionPage);
  Revit2026Check.Parent := VersionPage.Surface;
  Revit2026Check.Left := 0;
  Revit2026Check.Top := ScaleY(52);
  Revit2026Check.Width := VersionPage.SurfaceWidth;
  Revit2026Check.Caption := CustomMessage('Revit2026');
  Revit2026Check.Checked := IsRevitInstalled('2026');

  if (not Revit2025Check.Checked) and (not Revit2026Check.Checked) then
  begin
    Revit2025Check.Checked := True;
    Revit2026Check.Checked := True;
  end;
end;

function Install2025: Boolean;
begin
  Result := Revit2025Check.Checked;
end;

function Install2026: Boolean;
begin
  Result := Revit2026Check.Checked;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = VersionPage.ID) and
     (not Revit2025Check.Checked) and
     (not Revit2026Check.Checked) then
  begin
    MsgBox(CustomMessage('SelectVersionError'), mbError, MB_OK);
    Result := False;
  end;
end;
