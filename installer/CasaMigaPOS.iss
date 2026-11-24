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
; La caja imprime en crudo (ESC/POS RAW): basta el driver «Generic / Text Only» de Windows, no se descarga nada.
; Nunca cambia una impresora POS-80 ni un printing.json que ya existan, así que actualizar encima no toca una caja que ya imprime.
Name: "impresora"; Description: "Configurar la impresora de tickets POS-80 (conectada por USB y encendida; imprime un ticket de prueba)"; GroupDescription: "Impresora de tickets:"

[Files]
; Los archivos de una copia de desarrollo (modo de prueba y entrada automática) nunca entran en el instalador.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "development.settings.json,dev-autologin.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ToolsDir}\*"; DestDir: "{app}\tools"; Excludes: "development.settings.json,dev-autologin.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "impresora\*"; DestDir: "{app}\tools\impresora"; Flags: ignoreversion
; Pasar una caja completa a otra computadora: respaldar en la de antes y traer en la nueva (restaura y configura la impresora).
Source: "caja\*"; DestDir: "{app}\tools\caja"; Flags: ignoreversion

[InstallDelete]
; Archivos de la versión anterior se reemplazan completos para no mezclar ensamblados.
Type: filesandordirs; Name: "{app}\*.dll"
Type: filesandordirs; Name: "{app}\tools"
; Una instalación comercial nunca arranca en modo de prueba aunque alguien haya copiado ese archivo a mano.
Type: files; Name: "{app}\development.settings.json"

[Icons]
Name: "{group}\Casa Miga POS"; Filename: "{app}\CasaMiga.Presentation.exe"; WorkingDir: "{app}"
Name: "{group}\Configurar impresora de tickets"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\impresora\configurar-impresora.ps1"""; WorkingDir: "{app}\tools\impresora"; IconFilename: "{app}\CasaMiga.Presentation.exe"
Name: "{group}\Traer la caja de otra computadora"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\caja\traer-caja.ps1"""; WorkingDir: "{app}\tools\caja"; IconFilename: "{app}\CasaMiga.Presentation.exe"
Name: "{group}\Respaldar la caja para otra computadora"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\caja\respaldar-caja.ps1"""; WorkingDir: "{app}\tools\caja"; IconFilename: "{app}\CasaMiga.Presentation.exe"
Name: "{group}\Desinstalar Casa Miga POS"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Casa Miga POS"; Filename: "{app}\CasaMiga.Presentation.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Impresora de tickets: la cola POS-80 se crea como administrador; printing.json va a la carpeta de datos del usuario que instaló.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\impresora\configurar-impresora.ps1"" -Parte cola"; StatusMsg: "Configurando la impresora de tickets..."; Tasks: impresora; Check: IsAdminInstallMode; Flags: waituntilterminated
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\impresora\configurar-impresora.ps1"" -Parte ticket"; StatusMsg: "Guardando los datos del ticket..."; Tasks: impresora; Flags: runasoriginaluser waituntilterminated
Filename: "{app}\CasaMiga.Presentation.exe"; Description: "Abrir Casa Miga POS"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
spanish.FinishedLabel=Casa Miga POS quedó instalado. Tus datos de caja y ventas se conservan en la carpeta del usuario de Windows.
