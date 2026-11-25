# Rendimiento en computadoras modestas — 25/11/2026

Pedido del dueño: que la caja de escritorio funcione bien en computadoras no tan nuevas. Sólo rendimiento: mismas pantallas y mismo comportamiento. Rama `perf/equipos-modestos` sobre `main` `efce5ce` (1.0.2).

## Cambios

1. **ReadyToRun** en ambos `dotnet publish` de `scripts/build-installer.ps1` (app y `CasaMiga.Maintenance`). Los ensamblados de Casa Miga ya van compilados (p. ej. `CasaMiga.Presentation.dll` 1.7 → 3.2 MB); los de .NET ya lo estaban.
2. **Fotos al tamaño en que se ven.** `ProductPhotoUiGateway.Decode` lee sólo el encabezado de la miniatura y la decodifica con el lado corto en 320 px (el recuadro más grande es la tarjeta de Venta, ~300 px, y el Catálogo de 240), sin agrandar nunca y conservando la proporción. Al abrir, la ventana ajusta ese lado a la escala de pantalla (125 % → 400 px, 150 % → 480 px). Es el único punto de carga de fotos: Venta, Catálogo, Charolas y Consignación. Sigue la caché LRU de 64 MB que ya existía; ahora caben las 47 fotos con holgura.
3. **Listas.** Ya estaban paginadas (Venta, Ventas, Catálogo, Inventario: 50–100 por página) y los `DataGrid` virtualizados. Se agregó `VirtualizingPanel.VirtualizationMode=Recycling` al estilo de `DataGrid` (reutiliza filas al desplazarse). La cuadrícula de Venta usa su propio panel (`TileGrid`) y no virtualiza, pero muestra una página a la vez.
4. **Modo ligero automático** (`LightRendering.cs`): si WPF no tiene aceleración gráfica (`RenderCapability.Tier` < 2; también pasa por escritorio remoto), quita las sombras (`DropShadowEffect`) y el desvanecido de los menús y limita las animaciones a 30 cuadros por segundo. Las sombras pasaron a recursos del tema (`ShadowSubtle`, `ShadowMenu`, `ShadowDialog`, `ShadowHelp`) usados con `DynamicResource`; con aceleración se ven igual que antes. **Opción manual:** no se agregó a la pantalla (el usuario prefiere no sumar funciones); para soporte, la variable de entorno `CASAMIGA_MODO_LIGERO=1` lo fuerza y `0` lo apaga.
5. **32 bits:** no se agregó. Anotado en `docs/INSTALLER.md` como opción si aparece una caja con Windows de 32 bits.

## Verificación

- `scripts/verify.ps1` completo: CasaMiga.Tests 778 correctas y 1 omitida (la de siempre); PhotoTests 256 correctas (6 nuevas en `LowEndPerformanceTests`: tamaño de decodificación, proporción y no agrandar, escala de pantalla, decisión del modo ligero, sombras del tema quitadas y que no quede ninguna sombra en línea).
- `scripts/build-installer.ps1 -Version 1.0.3`: `CasaMigaPOS-1.0.3-setup.exe`, SHA-256 `22301AF6255F0CB6998973B0E720571B3E1C26BBCF927D793A576FC5E9F1222C`. No se publicó release.
- **Prueba aislada** con `CASAMIGA_DATA_DIR` en una carpeta temporal (nunca datos reales ni `%LOCALAPPDATA%\CasaMigaPOS`): un sembrador de prueba creó un administrador de prueba, abrió caja con fondo de prueba y escribió `autologin-caja.json`; al abrir 1.0.3 entró solo, cargó el catálogo de Casa Miga (47 panes, 47 fotos en `product-images`) y quedó en Venta con las fotos ([captura](../evidence/rendimiento-2026-11-25/venta-1.0.3.png)). Con `CASAMIGA_MODO_LIGERO=1` Venta se ve igual.
- No se manejó la pantalla: el control de la ventana no fue autorizado; la captura se tomó por código (PrintWindow) de la ventana de prueba.

## Mediciones

Equipo de desarrollo (Ryzen 9 7940HS, gráficos dedicados, escala 100 %), misma carpeta aislada copiada para cada versión, 5 arranques cada una, mediana. «1 núcleo» limita la caja a un núcleo del procesador para acercarse a una computadora modesta. Script: [medir-arranque.ps1](../evidence/rendimiento-2026-11-25/medir-arranque.ps1).

| | 1.0.2 | 1.0.3 | Cambio |
|---|---|---|---|
| Ventana visible | 1.19 s | 1.17 s | igual |
| Sesión iniciada (autologin) | 7.18 s | 6.22 s | −0.96 s (−13 %) |
| Sesión iniciada, 1 núcleo | 7.56 s | 6.54 s | −1.02 s (−13 %) |
| Lista en Venta (CPU quieta), 1 núcleo | 9.39 s | 8.55 s | −0.84 s |
| Memoria privada con Venta abierta | 236.5 MB | 189.0 MB | −47.5 MB (−20 %) |
| Memoria en uso (working set) con Venta abierta | 301.8 MB | 254.0 MB | −47.8 MB |
| Memoria privada con Venta abierta, 1 núcleo | 227.0 MB | 179.2 MB | −47.8 MB |
| Instalador | 80.9 MB (77.2 MiB) | 83.6 MB (79.7 MiB) | +2.6 MB |

«Lista en Venta» con todos los núcleos varió mucho entre arranques (9.5–13.7 s) y no se usa como comparación.

## Hallazgo (sin cambiar, requiere decisión)

Cerca de 4 s de cada arranque, con la ventana ya abierta y antes de iniciar sesión, son **dos respaldos completos de instalación** (`FoundationRuntime.Start`: uno antes de migrar y otro después; ~7 MB cada uno con las fotos) y en esta prueba ocupan la mayor parte del tiempo hasta iniciar sesión. En una computadora con disco mecánico pesará más. Es una protección de datos existente; no se tocó. Opción para decidir: hacer el respaldo previo sólo cuando haya migraciones pendientes.

## Pendiente

- Probar 1.0.3 en una computadora modesta real (y por escritorio remoto, donde se activa el modo ligero).
- Decidir sobre el doble respaldo al abrir.
