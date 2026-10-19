# FASE 1K.4G-C-C-B2 — validación operativa y visual con datos DEMO aislados

Fecha: 19/10/2026. Modelo: Claude Opus 5.5 (Claude Code). Estado: **aprobada técnicamente por el usuario el 19/10/2026**; cerrada con un commit local en la rama `phase/1k4g-c-c-b2-production-ux`, creada desde `master` en `e9e66c2a7cf1653323f477ee9cfa24c4117287c2`. El hash del commit se entrega en la conversación de cierre. Sin push, merge ni publicación. La prueba física de DPI y navegación sigue pendiente.

> Redefinición del alcance: el usuario autorizó B2 como **validación con datos sintéticos**, no como carga del recetario comercial. Los bloqueos del [recetario maestro](PHASE_1K4G_C_RECIPE_MASTER.md) siguen abiertos y ninguna receta comercial se publicó.

## Entorno aislado

- Fixture nueva, creada desde cero con `Database.Initialize()` (no copia B1/R1): `work/phase1k4g-c-c-b2-demo-20261019`. Identificador `B2-DEMO-D1A43AE6`. Schema 17, sin migración 018.
- Herramienta: `work/phase1k4g-c-c-b2-fixture-tool` (`build`, `validate`, `inspect`). Reutiliza el patrón de las fixtures 1K.3C y 1K.4G-B y sólo llama servicios públicos (`IdentityService`, `CatalogService`, `InventoryService`, `RecipeService`, `ProductionService`). No escribe SQL de negocio.
- Usuario `demo-admin` (Administrador DEMO - NO COMERCIAL); terminal `DEMO - NO COMERCIAL · Caja B2` en `terminal.json`. La contraseña llega por variable de entorno y no se guarda en archivos.
- Sin `printing.json` (impresión `CONFIG_MISSING`), consumo automático por venta `Disabled`, sin `product_inventory_link`.
- Reloj sintético monótono: fechas 13–18/10/2026 para historial legible.
- SHA-256 inicial (tras bootstrap) `853B4F4CD39CB524AF0B3410414E13A3E2A9CF0D5BE70B8CF75DED0A416CEE78`; final de la fixture entregable `5FA18342E3EADC5EAFCED8AC7DBBC50101EC34B5E0BD66657AB38FE0DFCE100E`. `integrity_check=ok`, 0 FK, ledger = saldos.

## Datos sintéticos

Todo nombre lleva «DEMO»; descripciones: «DEMO - NO COMERCIAL · receta sintética, no es fórmula aprobada de Casa Miga».

| Concepto | Cantidad |
| --- | --- |
| Categorías | 4 |
| Productos | 36: 24 listos, 6 receta incompleta, 6 sin receta |
| Artículos | 48: 18 materias primas (MASS/VOLUME/COUNT) y 30 terminados COUNT |
| Fórmulas / versiones | 11 / 13 (masa salada v1 y v2 publicadas, v3 borrador; pan de muerto sólo borrador) |
| Perfiles / versiones | 30 / 31 (Bolillo v1 y v2 publicadas, v3 borrador) |
| Producciones | 7 confirmadas, 3 borradores |
| Operaciones | 46 saldos iniciales, 7 consumos, 6 ingresos, 2 mermas; 102 movimientos |
| Ventas / caja / recibos | 0 |

Casos cubiertos: preparaciones compartidas (masa dulce y hojaldre), alternativas con preferencia (mantequilla/manteca y mantequilla/margarina), stock insuficiente (cacao, margarina), sin saldo inicial (piloncillo, terminado Trenza), insumo inactivo (nuez), ingrediente directo inactivo (queso), terminado inactivo (Garibaldi), perfil desactivado, perfil sin versiones, perfil sólo borrador y nombre largo. Producciones: igual, menor, mayor, salida cero, multiproducto, alternativa explícita, histórico con v1, merma de Dona glaseada (3) y Concha de chocolate (2). Borradores D1 (Bolillo/Telera/Baguette), D2 (Cuernito/Oreja/Moño) y D3 (Galleta de mantequilla/Beso), confirmables por `demo-admin` en esta terminal.

## Validación funcional

`validate` copia la fixture con la API de backup SQLite a `work/phase1k4g-c-c-b2-validation-scratch-20261019` y ejecuta allí las pruebas destructivas: **34/34**. Incluye vista previa sin efectos, consolidación y cálculo exacto, faltantes y saldo inicial ausente, borrador sin descuentos, idempotencia de creación y conflicto de identidad, bloqueo por terminal distinta, siete puntos de falla inyectada (`before_snapshots` … `before_confirmed`) con reversión total, consumo según plan bloqueado con reales mayor/menor, ingreso igual al real, reconfirmación idempotente, rechazo de resultado distinto, recuperación de commit incierto, faltante al confirmar, revisión obsoleta, versiones publicadas inmutables y snapshot histórico v1 (sal 38.4 g), merma sin negativos ni duplicados, integridad y ausencia de ventas/caja. La fixture entregable mantuvo su hash.

No se encontraron defectos funcionales del motor.

## Correcciones visuales en Producción

Sólo Presentation; sin cambios de dominio, aplicación, infraestructura, migraciones o contratos.

1. Lista de productos: altura proporcional al formulario visible (`ViewportShareConverter`, 52 %, 250–640 DIP) en lugar de 180 DIP.
2. Filas visibles: 1920×1080 pasa de 2 a 5–6 completas; 1366×768 de 2 a 4; 1366×768 al 150 % de 0 a 4.
3. Columnas: Detalle 1.6*, Estado/Versión con ancho mínimo y scroll horizontal local cuando el ancho no alcanza.
4. Detalle: texto de receta lista abreviado; explicaciones largas en dos líneas con tooltip completo.
5. Filas altas: celdas de listas de borradores e historial limitadas a dos líneas (antes 88–173 DIP, ahora 48).
6. Scroll anidado: el mínimo del shell baja a 320 DIP para formularios y 440 para listas; tablas de plan, vista previa y resultado real crecen hasta un máximo antes de desplazarse.
7. Plan inicial vacío con estado explicativo; la última fila puede quitarse.
8. Acciones deshabilitadas explicadas con texto visible y tooltip (agregar, quitar, calcular, preparar registro, ver detalle, merma). Agregar quedó junto al buscador.

## Pruebas

- Funcional en copia scratch: 34/34.
- Flujo por UI sobre copia de captura: abrir D1, real inválido bloquea, confirmación con 2 operaciones, segundo clic sin efectos, merma excesiva rechazada y merma válida; 0 ventas/caja.
- Focales: 109/109 `RecipePersistenceTests` (incluye la prueba nueva de plan vacío y textos de acciones deshabilitadas) y 3/3 `ProductionBuilderLayoutTests` nuevas. Una corrida focal intermedia detectó 2 fallos propios de las pruebas nuevas (vista sin ventana anfitriona); se corrigieron hospedándolas en ventana fuera de pantalla.
- Pruebas adaptadas: cinco pruebas que asumían la fila inicial vacía ahora agregan el producto con la acción real «Agregar producto seleccionado»; la intención de cada aserción se conserva.
- `scripts/verify.ps1`: restore bloqueado, Release **0 errores / 0 advertencias**, **806/806** (585 generales + 221 WPF), 0 fallidas, 0 omitidas. Línea base anterior 802/802; +4 pruebas nuevas.
- `git diff --check` sin hallazgos. Bases protegidas con SHA-256 idéntico antes y después (protegida 1K.2C-R2, B1, R1, C-B, 1K.4G-B, 1K.3C y WAL vacíos).

## Build de revisión

Salida `src/CasaMiga.Presentation/bin/Phase1K4G-C-C-B2/` con `CasaMigaReviewBuild=1K.4G-C-C-B2`. EXE SHA-256 `B78C2CA6D7BA6A0F8860A09113D2C5C41D763284FD60355FD12F8FF41A9AC202`; DLL `929AD68BD75D7FF61F097E7C7B0DAEEC1497EED1DA1B7384F649E5FE24175AEC`. `development.settings.json` local apunta a la fixture DEMO. Launcher `work/Abrir-Casa-Miga-1K4G-C-C-B2.cmd` (limpia `CASAMIGA_DATA_DIR`, usa `work/.dotnet`). Título esperado `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-C-B2`.

## Evidencia visual

`work/phase1k4g-c-c-b2-evidence/ui-before` y `ui-final` (120 PNG + métricas JSON): MainWindow real fuera de pantalla sobre copia de la fixture, 1920×1080 y 1366×768, 100/125/150 %, pantalla completa y maximizada simulada. La escala se simula por tamaño DIP y DPI de render; no sustituye un monitor físico ni la escala de Windows aplicada en vivo.

## Limitaciones

- No se validó en un monitor físico con escala de Windows cambiada en vivo.
- En 1366×768 al 125–150 % el encabezado compacto del shell ocupa ~285 DIP; puede quedar un scroll exterior corto. Corregirlo requiere tocar el shell general (fuera de alcance).
- Las fechas sintéticas usan reloj controlado; la apertura humana registrará eventos con la hora real.

## Apertura del build de revisión

El primer lanzamiento terminó en «Casa Miga · Cierre por error» porque el `development.settings.json` generado tenía barras invertidas sin escapar (`JsonException` en `FoundationOptions.Load`). Fue un error de preparación del build, no del producto. La instancia se cerró sin tomar el DataDirectory; dejó un reporte de diagnóstico de 702 bytes en `%LOCALAPPDATA%\CasaMigaPOS\diagnostics` (sin datos sensibles) y la base de esa carpeta no cambió (última escritura 08/10/2026). Tras corregir el archivo, el launcher abrió `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-C-B2` sobre la fixture DEMO y quedó en acceso sin iniciar sesión. La apertura crea el backup de instalación habitual y el lock en la fixture.

## Revisión visual y cierre

La revisión de las capturas finales no encontró defectos bloqueantes. Selección reducida para revisión humana: `work/phase1k4g-c-c-b2-evidence/selected-review/` (diez copias; los originales no se modificaron).

Reinicio del desplazamiento al pulsar «Nueva producción»: presente en `ProductionView.xaml.cs`, en los binarios Release probados y en el build de revisión. Comprobación automática con `MainWindow` real sobre una copia de la fixture (`work/phase1k4g-c-c-b2-scroll-check`): tras desplazar 393.6 DIP, el clic en el botón real devuelve el formulario a 0.0 en dos rondas; fixture sin cambios. Evidencia: `work/phase1k4g-c-c-b2-evidence/scroll-reset-check.txt`. No sustituye el smoke físico.

La suite 806/806 corresponde al código final: la última edición de fuentes (12:05:19) es anterior a los binarios probados (12:05:34–12:05:38) y al build de revisión (12:11:55). Los cambios posteriores son sólo documentación.

## Pendientes no bloqueantes

1. Detalle de producción: a resoluciones pequeñas hay que desplazarse más allá de ingredientes para llegar a «Resultado real» y «Registrar».
2. Mensajes de estado poco visibles: texto pequeño al pie de la vista, lejos del botón (comportamiento previo a B2).
3. Selector de merma con texto redundante (perfil y versión de receta).
4. Shell con escalado alto: en 1366×768 al 125–150 % el encabezado compacto ocupa ~285 DIP y aparecen scroll doble y horizontal.
5. Prueba física de DPI (100/125/150 %) y navegación completa en monitor real.

Otros detalles para Premium UI: columnas redundantes en Borradores, fecha en dos líneas a 125 %, orden de productos en el detalle distinto al del plan y scroll interno de la tabla de insumos.
