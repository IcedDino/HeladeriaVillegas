#ifndef PublishDir
  #define PublishDir "..\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
#endif
#ifndef PrerequisitesDir
  #define PrerequisitesDir "..\artifacts\prerequisites"
#endif
#define AppExe "HeladeriaPOS.Maui.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{22FBCFCE-05ED-4EAC-843E-52C0EAE7E98C}
AppName=Heladería Villegas POS
AppVersion={#AppVersion}
AppPublisher=Heladería Villegas
DefaultDirName={autopf}\Heladeria Villegas POS
DefaultGroupName=Heladería Villegas POS
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=HeladeriaVillegas-Setup-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PrerequisitesDir}\vc_redist.x64.exe"; Flags: dontcopy
Source: "{#PrerequisitesDir}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Heladería Villegas POS"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\Heladería Villegas POS"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir Heladería Villegas POS"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  DependencyRestart: Boolean;

function HasWebView2: Boolean;
var
  Version: String;
  Key: String;
begin
  Key := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := (RegQueryStringValue(HKLM32, Key, 'pv', Version) or
             RegQueryStringValue(HKCU, Key, 'pv', Version)) and
            (Version <> '') and (Version <> '0.0.0.0');
end;

function HasVisualCpp: Boolean;
var
  Installed, Major, Minor: Cardinal;
  Key: String;
begin
  Key := 'Software\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
  Result := RegQueryDWordValue(HKLM64, Key, 'Installed', Installed) and
    RegQueryDWordValue(HKLM64, Key, 'Major', Major) and
    RegQueryDWordValue(HKLM64, Key, 'Minor', Minor) and
    (Installed = 1) and ((Major > 14) or ((Major = 14) and (Minor >= 38)));
end;

function InstallDependency(FileName, Parameters, DisplayName: String): String;
var
  ExitCode: Integer;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := 'Instalando ' + DisplayName + '...';
  ExtractTemporaryFile(FileName);
  if not Exec(ExpandConstant('{tmp}\') + FileName, Parameters, '', SW_HIDE,
      ewWaitUntilTerminated, ExitCode) then
    Result := 'No se pudo iniciar la instalación de ' + DisplayName + '.'
  else if (ExitCode = 3010) or (ExitCode = 1641) then
    DependencyRestart := True
  else if ExitCode <> 0 then
    Result := 'No se pudo instalar ' + DisplayName + '. Código: ' + IntToStr(ExitCode);
  Log(DisplayName + ': ' + Result);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not HasVisualCpp then
    Result := InstallDependency('vc_redist.x64.exe', '/install /quiet /norestart', 'Microsoft Visual C++ x64');
  if (Result = '') and not HasWebView2 then
    Result := InstallDependency('MicrosoftEdgeWebView2RuntimeInstallerX64.exe', '/silent /install', 'Microsoft Edge WebView2');
  NeedsRestart := DependencyRestart;
end;

function NeedRestart: Boolean;
begin
  Result := DependencyRestart;
end;
