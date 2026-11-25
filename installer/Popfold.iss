; Instalador do Popfold (Inno Setup 6).
; Gere com tools\build-installer.ps1 (publica o app e compila este script). Saída: dist\Popfold-Setup-<versão>.exe

#define AppName "Popfold"
#define AppExe "Popfold.exe"
#define Publish "..\publish\win-x64"
#define AppVersion GetVersionNumbersString(Publish + "\" + AppExe)
#define ShortVersion Copy(AppVersion, 1, RPos(".", AppVersion) - 1)

[Setup]
AppId={{95C110DC-3880-411B-BFEF-949439384231}
AppName={#AppName}
AppVersion={#ShortVersion}
AppVerName={#AppName} {#ShortVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
; Administrador: o item "Novo > Popfold" do botão direito precisa ser registrado para a máquina (HKLM).
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=Popfold-Setup-{#ShortVersion}
SetupIconFile=..\Popfold.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=..\branding\installer\wizard-large.bmp,..\branding\installer\wizard-large@2.5x.bmp
WizardSmallImageFile=..\branding\installer\wizard-small.bmp,..\branding\installer\wizard-small@2.5x.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ChangesAssociations=yes
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
pt.StartWithWindows=Abrir o Popfold quando o Windows iniciar
en.StartWithWindows=Open Popfold when Windows starts
pt.NewMenuTip=Dica: a opção Novo > Popfold, no botão direito da área de trabalho, aparece depois de reiniciar o computador.
en.NewMenuTip=Tip: the New > Popfold option in the desktop right-click menu shows up after you restart the computer.
pt.DeleteData=Apagar também as suas pastas do Popfold e as configurações?%n%nEscolha Não para mantê-las caso instale de novo.
en.DeleteData=Also delete your Popfold folders and settings?%n%nChoose No to keep them in case you reinstall.

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"

[Files]
Source: "{#Publish}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Modelo vazio copiado pelo Explorer em "Novo > Popfold" (oculto + sistema: a cópia temporária não pisca na tela).
Source: "Popfold.template"; DestDir: "{app}"; Attribs: hidden system; Flags: ignoreversion
Source: "..\fonts\OFL-*.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Novo > Popfold
Root: HKLM; Subkey: "Software\Classes\.popfold"; ValueType: string; ValueName: ""; ValueData: "Popfold.Folder"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\.popfold\ShellNew"; ValueType: string; ValueName: "FileName"; ValueData: "{app}\Popfold.template"
Root: HKLM; Subkey: "Software\Classes\Popfold.Folder"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\Popfold.Folder\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"",0"
Root: HKLM; Subkey: "Software\Classes\Popfold.Folder\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" --new ""%1"""
; Iniciar com o Windows (o próprio app também liga/desliga isso nas Opções)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Popfold"; ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
; Atalhos que o app cria para fixar Popfolds na barra de tarefas (menu Iniciar > Popfold)
Type: filesandordirs; Name: "{userprograms}\Popfold"

[Code]
procedure ClosePopfold;
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  ClosePopfold;  { atualização: o Popfold aberto trava os arquivos }
  Result := '';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('NewMenuTip');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    ClosePopfold;
    { Valores que o próprio app grava para o usuário }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Popfold');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\.popfold');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Popfold.Folder');
  end;
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\Popfold'), True, True, True);
end;
