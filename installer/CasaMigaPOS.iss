; Instalador de Casa Miga POS (Inno Setup 6). Generar con scripts/build-installer.ps1.
; Los datos viven en %LOCALAPPDATA%\CasaMigaPOS de cada usuario: instalar, actualizar o desinstalar NUNCA los toca.
#ifndef AppVersion
  #define AppVersion "0.9.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef ToolsDir
  #define ToolsDir "..\artifacts\tools"
#endif

[Setup]
; AppId fijo: identifica la instalación para actualizar en lugar de duplicar. No cambiarlo.
AppId={{09807D4A-35DD-4D75-B33F-769D8D07A8FC}
AppName=Casa Miga POS
AppVersion={#AppVersion}
AppVerName=Casa Miga POS {#AppVersion}
AppPublisher=Casa Miga
DefaultDirName={autopf}\Casa Miga POS
DefaultGroupName=Casa Miga POS
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=CasaMigaPOS-{#AppVersion}-setup
SetupIconFile=..\src\CasaMiga.Presentation\Assets\CasaMiga.ico
UninstallDisplayIcon={app}\CasaMiga.Presentation.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
; Permite también instalar sólo para el usuario actual (sin permisos de administrador).
PrivilegesRequiredOverridesAllowed=dialog commandline
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Cierra la app abierta antes de actualizar (Restart Manager) y no la reabre sola: la caja debe revisarse al volver a abrir.
CloseApplications=force
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
; Los archivos de una copia de desarrollo (modo de prueba y entrada automática) nunca entran en el instalador.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "development.settings.json,dev-autologin.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ToolsDir}\*"; DestDir: "{app}\tools"; Excludes: "development.settings.json,dev-autologin.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Archivos de la versión anterior se reemplazan completos para no mezclar ensamblados.
Type: filesandordirs; Name: "{app}\*.dll"
Type: filesandordirs; Name: "{app}\tools"
; Una instalación comercial nunca arranca en modo de prueba aunque alguien haya copiado ese archivo a mano.
Type: files; Name: "{app}\development.settings.json"

[Icons]
Name: "{group}\Casa Miga POS"; Filename: "{app}\CasaMiga.Presentation.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar Casa Miga POS"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Casa Miga POS"; Filename: "{app}\CasaMiga.Presentation.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\CasaMiga.Presentation.exe"; Description: "Abrir Casa Miga POS"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
spanish.FinishedLabel=Casa Miga POS quedó instalado. Tus datos de caja y ventas se conservan en la carpeta del usuario de Windows.
