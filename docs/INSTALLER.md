# Instalador de Windows

Estado: **implementado; sin firma de código** (decisión del usuario, 06/10/2026). Fecha: 06/10/2026.

## Generar

Requisitos: SDK .NET 10 e Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

```powershell
./scripts/build-installer.ps1 -Version 0.9.0
```

Salida: `artifacts/installer/CasaMigaPOS-<versión>-setup.exe` y su `.sha256`. `artifacts/` no se versiona.

## Qué hace

- Publica la app y `CasaMiga.Maintenance` autocontenidas (`win-x64`) y precompiladas (ReadyToRun): las cajas no necesitan .NET instalado y arrancan con menos compilación al abrir.
- Instala en `Program Files\Casa Miga POS` (o sólo para el usuario con `/CURRENTUSER`), con acceso en Inicio y, opcional, en el escritorio. La herramienta queda en `tools\`.
- Actualizar: mismo `AppId`, cierra la app abierta y reemplaza los ensamblados completos; no la reabre sola.
- **Nunca toca datos.** Caja, ventas y respaldos viven en `%LOCALAPPDATA%\CasaMigaPOS` de cada usuario de Windows; desinstalar los conserva.
- Recuperar administrador en un equipo instalado: `"C:\Program Files\Casa Miga POS\tools\CasaMiga.Maintenance.exe" reset-admin %LOCALAPPDATA%\CasaMigaPOS` con la app cerrada.

## Verificación realizada

Instalación silenciosa por usuario en carpeta temporal, presencia de app/herramienta/desinstalador, `init` y `reset-admin` (rechazo correcto sin administrador) desde la instalación, desinstalación silenciosa con datos conservados.

## Computadoras modestas (25/11/2026)

- **Requisitos de la caja:** Windows 10 versión 1809 o posterior, o Windows 11, de 64 bits (`MinVersion=10.0.17763`, `ArchitecturesAllowed=x64compatible`). .NET 10 no corre en Windows 7 ni 8.
- **ReadyToRun** (`-p:PublishReadyToRun=true` en ambos `dotnet publish`): el programa trae su código ya compilado; en procesadores viejos es lo que más se nota al abrir.
- **Fotos:** se decodifican al tamaño en que se ven (lado corto de 320 px al 100 % de escala de pantalla, más con 125 % o 150 %) y no a los 512-768 px de la miniatura (`ProductPhotoUiGateway.Decode`).
- **Modo ligero automático:** sin aceleración gráfica (WPF `RenderCapability.Tier` < 2, también por escritorio remoto) se quitan sombras y el desvanecido de menús y las animaciones van a 30 cuadros por segundo (`LightRendering.cs`). Pantallas, botones y textos no cambian. Para soporte, la variable de entorno `CASAMIGA_MODO_LIGERO` lo fuerza: `1` encendido, `0` apagado. No hay opción en pantalla.
- **32 bits:** no se genera. Si se confirma una caja con Windows de 32 bits, se puede agregar una publicación `win-x86` y un segundo instalador; antes hay que revisar que SQLite y la impresión funcionen ahí.

## Protección contra el modo de prueba (20/10/2026)

`build-installer.ps1` se detiene si la publicación trae `development.settings.json` o `dev-autologin.json`; el `.iss` los excluye de la copia y, al instalar o actualizar, borra un `development.settings.json` que alguien hubiera dejado en la carpeta del programa. Así una instalación comercial nunca arranca en modo de prueba.

**Excepción pedida por el dueño (22/10/2026, «por el momento»): «Recuérdame».** En la pantalla de acceso de la caja normal, la casilla «Recuérdame en esta computadora» guarda la sesión en la **carpeta de datos**.
- Se guarda en `recordarme.dat`, cifrado con Windows: sólo el mismo usuario de Windows de esa computadora puede leerlo.
- Las siguientes veces entra sola con el login normal y la contraseña real. Si la contraseña ya no sirve, muestra la pantalla de acceso con un aviso. Cada intento fallido cuenta y, a los 5, la cuenta se bloquea 1 minuto.
- Entrar sin marcar la casilla (o borrar el archivo) vuelve a pedir contraseña.
- También se acepta `autologin-caja.json` (`{"Username":"…","Password":"…"}`) escrito a mano, sin cifrar; usar «Recuérdame» lo reemplaza.
- El instalador nunca crea estos archivos. Una copia de desarrollo no los lee: usa `dev-autologin.json`.

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

## Catálogo de Casa Miga y sus fotos con IA (13/11/2026)

Pedido del dueño: las fotos provisionales hechas con IA van como archivos en el instalador.
- `CasaMiga.Presentation.csproj` copia, junto al programa y en la publicación, la carpeta **`catalogo\`**: `catalogo-autorizado-v2.csv`, `renombres.json` y `fotos\CM-001.jpg … CM-047.jpg` con su `manifest.json` (unos 5 MB). El instalador la deja en `C:\Program Files\Casa Miga POS\catalogo` sin cambios en el `.iss`.
- **Catálogo → «Cargar catálogo Casa Miga (panes y fotos)»** (administrador; pide confirmación):
  - agrega los panes que falten, con su foto provisional y su categoría;
  - renombra los que cambiaron de nombre (`renombres.json`) en lugar de dar de alta otro;
  - pone foto a los panes que no tienen.
  - Nunca borra panes, nunca cambia precios ni categorías de los que ya existen y nunca reemplaza una foto real. Volver a usarlo no repite nada.
- Sirve igual en una caja recién instalada (después de crear el administrador) y en la caja de la tienda (después de actualizarla con respaldo, `docs/ACTUALIZAR_CAJA.md`).
- **Caja recién instalada (24/11/2026):** la base se crea sola al abrir el programa y, la primera vez que el administrador entra a una base sin ningún pan, el catálogo se carga solo con sus 47 fotos (`MainViewModel.CatalogoInicial.cs`, mensaje «Preparando el catálogo de Casa Miga…»). Una base restaurada o con panes nunca se toca; las copias de desarrollo y las pruebas (`IsDevelopment`) no lo hacen. Si falla, entra igual y queda el botón.
- Prueba: `CasaMigaCatalogTests` (PhotoTests).

## Impresora de tickets (24/11/2026)

Pedido del dueño: que el instalador deje lista la impresora, sin buscar ni descargar el paquete del fabricante.
- La caja manda el ticket en crudo (`WindowsRawPrintTransport`, tipo `RAW`): Windows no lo interpreta, así que basta el driver **«Generic / Text Only»** que ya trae Windows. No se descarga ni se reparte el driver de la Qian.
- Casilla **«Configurar la impresora de tickets POS-80»** (marcada por omisión), con la térmica conectada por USB y encendida:
  - como administrador, `installer/impresora/configurar-impresora.ps1 -Parte cola` crea la cola **`POS-80`** en el puerto USB de la impresora y, sólo si la acaba de crear, imprime el ticket de prueba de `CasaMiga.Maintenance print-test` (perfil «PRUEBA CASA MIGA» en una carpeta temporal: nunca abre la base);
  - como el usuario que instaló, `-Parte ticket` copia `printing.casamiga.json` (48 columnas, ibm850 / tabla 2, corte parcial, sin cajón, dirección, WhatsApp y redes de Casa Miga) como `printing.json` en su carpeta de datos.
  - Nunca cambia una impresora `POS-80` ni un `printing.json` que ya existan: actualizar encima de una caja que ya imprime no la toca.
- Menú Inicio → **«Configurar impresora de tickets»**: hace las dos cosas (pide permiso de administrador sólo si falta la impresora) e imprime la prueba. Sirve si la impresora no estaba conectada al instalar.
- El script también funciona suelto, con `printing.casamiga.json` al lado, en una caja que ya tenga el programa: `powershell -NoProfile -ExecutionPolicy Bypass -File .\configurar-impresora.ps1`.
- Para pasar las ventas de otra caja con `restore-installation`, restaurar **antes** de configurar la impresora o de abrir el programa: el destino debe ser una carpeta nueva y el respaldo ya trae su propio `printing.json`.
- **Pasar la caja a otra computadora** (`installer/caja/`, en `tools\caja` y en el menú Inicio):
  - **«Respaldar la caja para otra computadora»** (`respaldar-caja.ps1`), en la de antes, con el corte hecho y el programa cerrado: saca el `.cmbackup` completo con `CasaMiga.Maintenance backup` (encuentra `CasaMiga-Ventas\caja` o `%LOCALAPPDATA%\CasaMigaPOS`) y lo copia, verificado, al Escritorio y a la USB. Sólo lee la caja. Con `-SubirAGitHub` (GitHub CLI con sesión) además lo sube como **borrador** de release `traspaso-caja-<fecha>` al repositorio privado, para bajarlo en la nueva sin USB: sólo lo ven quienes tienen permiso de escritura, cada respaldo reemplaza al borrador anterior y hay que borrarlo cuando la caja nueva funcione (trae ventas y usuarios).
  - **«Traer la caja de otra computadora»** (`traer-caja.ps1`), en la nueva: toma el `.cmbackup` más reciente de la USB, el Escritorio o Descargas, aparta (no borra) lo que hubiera en la carpeta de datos, restaura con `restore-installation` y corre «Configurar impresora de tickets». Si la restauración falla, deja la computadora como estaba.
- Pendiente: probarlo en Windows (compilar con `build-installer.ps1`, instalar con la térmica conectada y revisar el papel). El formato aprobado en la tienda se validó con el driver de la Qian; con «Generic / Text Only» los bytes que llegan son los mismos, pero falta verlo impreso.

## Pendiente

- Sin firma: SmartScreen mostrará «editor desconocido» (Más información → Ejecutar de todas formas).
- Smoke en Windows 10 x64 y Windows 11 x64 limpios, actualización sobre versión previa e impresión física.
- `src/Directory.Build.props` fija `RuntimeIdentifiers=win-x64` para que los lock files sean iguales con y sin `-r win-x64`.
