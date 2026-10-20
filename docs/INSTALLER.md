# Instalador de Windows

Estado: **implementado; sin firma de código** (decisión del usuario, 06/10/2026). Fecha: 06/10/2026.

## Generar

Requisitos: SDK .NET 10 e Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

```powershell
./scripts/build-installer.ps1 -Version 0.9.0
```

Salida: `artifacts/installer/CasaMigaPOS-<versión>-setup.exe` y su `.sha256`. `artifacts/` no se versiona.

## Qué hace

- Publica la app y `CasaMiga.Maintenance` autocontenidas (`win-x64`): las cajas no necesitan .NET instalado.
- Instala en `Program Files\Casa Miga POS` (o sólo para el usuario con `/CURRENTUSER`), con acceso en Inicio y, opcional, en el escritorio. La herramienta queda en `tools\`.
- Actualizar: mismo `AppId`, cierra la app abierta y reemplaza los ensamblados completos; no la reabre sola.
- **Nunca toca datos.** Caja, ventas y respaldos viven en `%LOCALAPPDATA%\CasaMigaPOS` de cada usuario de Windows; desinstalar los conserva.
- Recuperar administrador en un equipo instalado: `"C:\Program Files\Casa Miga POS\tools\CasaMiga.Maintenance.exe" reset-admin %LOCALAPPDATA%\CasaMigaPOS` con la app cerrada.

## Verificación realizada

Instalación silenciosa por usuario en carpeta temporal, presencia de app/herramienta/desinstalador, `init` y `reset-admin` (rechazo correcto sin administrador) desde la instalación, desinstalación silenciosa con datos conservados.

## Protección contra el modo de prueba (20/10/2026)

`build-installer.ps1` se detiene si la publicación trae `development.settings.json` o `dev-autologin.json`; el `.iss` los excluye de la copia y, al instalar o actualizar, borra un `development.settings.json` que alguien hubiera dejado en la carpeta del programa. Así una instalación comercial nunca arranca en modo de prueba ni con entrada automática.

## Instalador 1.0.0 probado en una instalación aislada (20/10/2026)

Generado en esta computadora con Inno Setup 6.7.3 (instalado por usuario con `winget install JRSoftware.InnoSetup --scope user`) desde `main` `c1a84b4`.

- Archivo: `artifacts/installer/CasaMigaPOS-1.0.0-setup.exe`, 72 MB.
- SHA-256: `B3BD35F4EAC676293921514068E54E107849D1272AB80E8F5E941C3330BA45CD`.

| Prueba | Resultado |
|---|---|
| Instalar sin preguntas sólo para el usuario actual en una carpeta de prueba (`/VERYSILENT /CURRENTUSER /DIR=… /TASKS=""`) | 646 archivos, versión 1.0.0, sin archivos del modo de prueba, registrado en «Programas» del usuario |
| Abrir con una carpeta de datos vacía | Sin «DESARROLLO», «Aplicación local · Versión 1.0.0», pide crear el primer administrador |
| Abrir con una copia de la caja real en versión 30 | Respalda antes y después, sube a 31, pide iniciar sesión (sin entrada automática); todas las tablas iguales a la base real salvo el registro de la migración 31 |
| Instalar otra vez encima, con un `development.settings.json` dejado a mano en la carpeta del programa | Lo borra, deja los mismos 646 archivos y un solo registro; abre normal con los datos existentes |
| Desinstalar sin preguntas | Quita la carpeta del programa, el registro y el menú Inicio; las carpetas de datos quedan intactas |

Los datos de prueba se pasaron con `CASAMIGA_DATA_DIR`; no se usó `%LOCALAPPDATA%CasaMigaPOS` de esta computadora.

## Pendiente

- Sin firma: SmartScreen mostrará «editor desconocido» (Más información → Ejecutar de todas formas).
- Smoke en Windows 10 x64 y Windows 11 x64 limpios, actualización sobre versión previa e impresión física.
- `src/Directory.Build.props` fija `RuntimeIdentifiers=win-x64` para que los lock files sean iguales con y sin `-r win-x64`.
