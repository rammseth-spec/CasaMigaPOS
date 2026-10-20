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

## Pendiente

- Sin firma: SmartScreen mostrará «editor desconocido» (Más información → Ejecutar de todas formas).
- Smoke en Windows 10 x64 y Windows 11 x64 limpios, actualización sobre versión previa e impresión física.
- `src/Directory.Build.props` fija `RuntimeIdentifiers=win-x64` para que los lock files sean iguales con y sin `-r win-x64`.
