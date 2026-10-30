#ifndef AppVersion
#define AppVersion "0.16.16"
#endif

[Setup]
AppId={{866450A3-3BF8-4F28-A0D3-1AB28B569F81}
AppName=RESET EXPLODE
AppVersion={#AppVersion}
AppPublisher=RESET EXPLODE
DefaultDirName={autopf}\ResetExplode
DefaultGroupName=RESET EXPLODE
DisableProgramGroupPage=yes
OutputDir=..\..\ResetExplode-instalador
OutputBaseFilename=ResetExplode-Setup-{#AppVersion}-x64
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\ResetExplode.exe
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
InfoBeforeFile=LEIA-ME-INSTALADOR.txt
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; Flags: checkedonce

[Files]
Source: "..\..\ResetExplode-publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LEIA-ME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Drivers\LEIA-ME.md"; DestDir: "{app}"; DestName: "LICENCAS-DRIVERS.md"; Flags: ignoreversion
Source: "LEIA-ME-INSTALADOR.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "dependencies\PawnIO_setup.exe"; Flags: dontcopy
Source: "dependencies\VC_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\RESET EXPLODE"; Filename: "{app}\ResetExplode.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar RESET EXPLODE"; Filename: "{uninstallexe}"
Name: "{commondesktop}\RESET EXPLODE"; Filename: "{app}\ResetExplode.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ResetExplode.exe"; Description: "Abrir RESET EXPLODE"; Flags: postinstall nowait skipifsilent shellexec runascurrentuser; Check: CanLaunch

[Code]
var
  DependencyRestart: Boolean;

function CanLaunch: Boolean;
begin
  Result := not DependencyRestart;
end;

function NeedRestart: Boolean;
begin
  Result := DependencyRestart;
end;

function VCRuntimeReady: Boolean;
var
  Installed, Major, Minor, Build: Cardinal;
  Key: String;
begin
  Key := 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
  Result := False;
  if not RegQueryDWordValue(HKLM64, Key, 'Installed', Installed) then Exit;
  if Installed <> 1 then Exit;
  if not RegQueryDWordValue(HKLM64, Key, 'Major', Major) then Exit;
  if not RegQueryDWordValue(HKLM64, Key, 'Minor', Minor) then Exit;
  if not RegQueryDWordValue(HKLM64, Key, 'Bld', Build) then Exit;
  Result := (Major > 14) or ((Major = 14) and ((Minor > 44) or ((Minor = 44) and (Build >= 35211))));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := 'Preparando os componentes necessários...';
  if not VCRuntimeReady then
  begin
    ExtractTemporaryFile('VC_redist.x64.exe');
    if not Exec(ExpandConstant('{tmp}\VC_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    begin
      Result := 'Não foi possível iniciar a instalação do Microsoft Visual C++. Tente novamente.';
      Exit;
    end;
    if (ExitCode = 3010) or (ExitCode = 1641) then DependencyRestart := True
    else if (ExitCode <> 0) and (ExitCode <> 1638) then
    begin
      Result := 'Falha ao instalar o Microsoft Visual C++ (código ' + IntToStr(ExitCode) + ').';
      Exit;
    end;
  end;
  if not RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO') then
  begin
    WizardForm.StatusLabel.Caption := 'Instalando PawnIO para leitura dos sensores...';
    ExtractTemporaryFile('PawnIO_setup.exe');
    if not Exec(ExpandConstant('{tmp}\PawnIO_setup.exe'), '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    begin
      Result := 'Não foi possível iniciar a instalação do PawnIO. Tente novamente.';
      Exit;
    end;
    if (ExitCode = 3010) or (ExitCode = 1641) then DependencyRestart := True
    else if ExitCode <> 0 then
    begin
      Result := 'Falha ao instalar o PawnIO (código ' + IntToStr(ExitCode) + ').';
      Exit;
    end;
    if not RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO') then
      Result := 'O PawnIO não foi detectado após a instalação. Reinicie o Windows e tente novamente.';
  end;
end;
