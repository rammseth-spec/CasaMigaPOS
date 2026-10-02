# FASE 1K.3A — base global de usabilidad, localización y navegación

Fecha: 01/10/2026. FASE 1K continúa abierta. Alcance: Presentation, pruebas y documentación. La revisión manual del usuario de esta subfase queda pendiente; no inicia 1K.3B ni carga de datos reales.

## Baseline y alcance

- Modelo utilizado: Codex basado en GPT-6. No se afirma haber cambiado a Astra Ultra; es la recomendación del usuario, no un cambio verificable desde esta sesión.
- HEAD inicial comprobado: `6942c061bf6a68a65d42f57db5b00fbe567d1548`, `fix: prevent inventory detail crash after presentation load`.
- Estado Git inicial limpio. Se revisaron README, CURRENT_STATE, NEXT_STEPS, instrucciones locales, código y diff completo. La corrección anterior de Inventario se conserva; sus regresiones se vuelven a ejecutar.
- Domain, Application, Infrastructure, SQLite y migraciones: sin modificaciones. Presentation: sí. Schema final: 16; no migración 017. Identidad gráfica, impresión, permisos, reglas de negocio, idempotencia y recuperación conservados.

## Causa y corrección de scroll

El ScrollViewer principal contiene páginas con DataGrid de altura fija y ScrollViewer internos. WPF consume MouseWheel en el ScrollViewer interno incluso vacío o en su límite; el evento no llega a la página. Además, un ComboBox cerrado con foco puede convertir la rueda en cambio de selección. Se reprodujeron ambos comportamientos sin el comportamiento nuevo, antes de comprobar la corrección.

Referencias primarias revisadas: [ScrollViewer.OnMouseWheel](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ScrollViewer.cs), [ComboBox.OnMouseWheel](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ComboBox.cs). La implementación es propia; no se copió código de terceros.

`NestedScrollBehavior` se habilita explícitamente sólo en InventoryView, RecipeView y ProductionView. No existe interceptación global del tema o ventana. Busca el ScrollViewer más cercano que pueda desplazarse en la dirección solicitada:

- Si el control interno puede avanzar, conserva su evento nativo, unidades y virtualización.
- Si está vacío o en el límite, transfiere el desplazamiento al ancestro que pueda avanzar.
- ComboBox cerrado: desplaza la página sin alterar la selección, incluso con foco.
- ComboBox abierto/captura del popup: conserva su rueda nativa y selección.
- No intercepta teclado, arrastre de barras ni eventos táctiles. Para transferencia a contenedor por píxeles conserva deltas pequeños y la preferencia de líneas del sistema.

WPF real comprobado: tabla vacía; DataGrid con 60 filas; límites superior/inferior; rueda sobre ComboBox cerrado con foco; popup abierto desplazable; tecla End; ScrollToVerticalOffset; delta -15 además de -120. No se certifica touchpad físico ni gesto horizontal: requieren revisión en el equipo del usuario. El uso programático de offset no equivale a probar físicamente el arrastre de la barra.

## Localización y datos técnicos

`UiLabels` separa etiquetas visibles de enums/códigos persistidos. Traducciones nuevas exactas:

| Valor interno | Texto visible |
|---|---|
| Mass | Masa |
| Volume | Volumen |
| Count | Piezas |
| DAMAGED | Dañado |
| EXPIRED | Caducado |
| PRODUCTION_ERROR | Error de producción |
| SPILL | Derrame |
| OTHER | Otro |
| PHYSICAL_COUNT_VARIANCE | Diferencia de conteo físico |
| DATA_CORRECTION | Corrección de datos |
| ProductionConsumption | Consumo de producción |
| ProductionOutput | Salida de producción |
| Return | Devolución |
| Transfer | Transferencia |

Valor desconocido: «Dimensión no disponible», «Motivo no disponible» o «Movimiento no disponible»; motivo ausente: «—». Los seis movimientos ya traducidos conservan Saldo inicial, Entrada, Ajuste positivo, Ajuste negativo, Merma y Venta. Inventario y Recetas comparten Masa/Volumen/Piezas. El selector conserva SelectedValue de tipo InventoryDimension.

«Configuración habilitada» pasa a «Puedes crear y editar recetas y perfiles». El aviso de reconciliación sustituye «saldo materializado/ledger» por «existencia registrada/historial de movimientos», manteniendo explícito que no se realizó reparación. Los términos reservados Cantidad absoluta, Saldo MAIN, Delta/Antes/Después, Planeado/real, Sin salida, Alternativas, Política, Preferencia y Rendimiento se conservan.

OperationId deja de aparecer en Saldo inicial, Entrada, Merma y Conteo físico y se retira su columna del historial de movimientos. El identificador, su generación y los mecanismos internos permanecen. Se retira ProductId del selector de productos para asociar porque ya muestra Nombre/SKU. La lista de asociaciones existentes conserva ProductId: sustituirlo por nombre es un pendiente explícito de 1K.3B. No se realizó una eliminación masiva de IDs fuera de los casos observados.

## Estados vacíos y acciones

Se añade un estilo discreto reutilizable para: búsquedas de inventario sin resultados; presentaciones, asociaciones e historial de movimientos vacíos; búsqueda de productos para asociar; recetas, perfiles y versiones vacíos; artículos terminados no disponibles; producción sin perfiles activos; borradores e historial de producción vacíos. No utiliza MessageBox.

Botones corregidos:

- Ver detalle sin selección en los tres módulos.
- Desvincular/Asociar sin selección en Inventario.
- Quitar ingrediente, opción, preparación e ingrediente directo sin selección en Recetas.
- Editar/Publicar sólo para borrador seleccionado; Activar sólo para versión publicada no activa, respetando permisos.
- Crear perfil sin producto/artículo terminado; agregar ingredientes/opciones/preparaciones sin catálogo disponible.
- Calcular necesidades con perfil/cantidad inválidos o perfil duplicado; Crear producción sin preview vigente/confirmable; Quitar seleccionado sin fila o con una única fila; Agregar producto sin perfiles activos.

Modificar productos, cantidades o alternativas invalida la vista previa de producción y deshabilita Crear hasta recalcular. No cambia el contrato de cálculo o confirmación. El selector Artículo terminado sólo ofrece activos de tipo Producto terminado y dimensión Piezas; no ofrece materias primas contables.

Disabled utiliza texto oscuro legible sobre la superficie deshabilitada existente, sin opacidad adicional; cursor de flecha. Los botones activos conservan el diseño aprobado.

`ContextHelp` queda implementado como componente WPF reutilizable: «?», expansión local, texto envolvente, nombre accesible y cierre con Esc. Sin navegador, HTML, paquetes externos ni manual completo; aún no se insertan ayudas operativas en las pantallas.

## Verificación

Focales: **52/52** ViewModel y **11/11** WPF, cero fallidas/omitidas. Suite completa mediante `scripts/verify.ps1 -Dotnet (Resolve-Path work/.dotnet/dotnet.exe) -OutputPath bin/Phase1K3A/`: **672/672** (556 generales + 116 fotos/WPF), cero fallidas/omitidas. Baseline anterior 653; incremento de 19 casos. Restore locked-mode correcto y Release con **0 errores y 0 advertencias**. `git diff --check` correcto. Evidencia portable en `evidence/phase1k3a-verification.md`; TRX completos locales bajo TestResults.

19 casos nuevos: 11 de ViewModel/localización y 8 WPF. Cubren los tres módulos, selecciones, permisos de Manager, plan inválido/obsoleto, navegación vacía, códigos de motivos, UI en español, OperationId interno pero no visible, ComboBox/DataGrid/scroll, legibilidad Disabled y ayuda reutilizable. Las pruebas utilizan bases temporales aisladas; no los datos de revisión del usuario. El host WPF existente se extrae a WpfTestHost para compartir una única Application/Dispatcher STA sin perder las regresiones del crash.

Transparencia: hubo fallos iniciales de dos expectativas antiguas (OperationId antes visible y texto de reconciliación) y del fixture de teclado/popup sin una fuente WPF cargada. Se actualizaron las expectativas al requerimiento y se cargó una ventana WPF de prueba. Una primera suite completa se interrumpió antes de finalizar para incorporar los dos detalles hallados en revisión; no se cuenta como aprobada. Los resultados finales proceden de una ejecución completa posterior, sin omisiones deliberadas.

## Protección de los datos

Base de revisión: `work/phase1k2c-r2-smoke-20260928/casamiga.db`. SHA-256 de entrada: `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.

Una copia byte a byte se abrió con SQLite `mode=ro` y `query_only=ON`; la original sólo se leyó como archivo para copia/hash. Evidencia: `evidence/phase1k3a-data.json`. Se confirma:

- MP-HAR-001, Harina, materia prima, masa, activa, única.
- Existencia: 50 000 000 unidades atómicas mg = 50 kg.
- HAR-BULTO-25KG, Bulto 25 kg, factor 25 000 000 mg, activa, única.
- El saldo inicial de 50 kg ya estaba registrado por el usuario antes de esta intervención. No se repitió.
- Cero asociaciones de Harina, consumo automático DISABLED, schema 16, sin violaciones FK en la copia.
- No se ejecutaron ventas, entradas, mermas, conteos, asociaciones, recetas, perfiles o producciones sobre la instalación del usuario. Ningún trabajo fue enviado a la impresora.

La comprobación de acceso usa otra copia aislada mediante la herramienta existente; no cambia contraseña, cuenta ni auditoría de la instalación original. No se arranca el POS contra datos reales desde Codex. El SHA-256 final de la base original coincide exactamente con el de entrada: no hubo mutaciones, incluidos movimientos, ventas, caja, recetas, perfiles ni producciones.

## Build de revisión y checklist manual

Salida Release equivalente a R2: `src/CasaMiga.Presentation/bin/Phase1K3A/CasaMiga.Presentation.exe`. Se prepara con copia exacta del development.settings.json local de R2, que apunta al mismo directorio de revisión; no se modifica R2 ni la configuración de impresión. Se entrega preparada, no iniciada. El lanzador local `work/Abrir-Casa-Miga-1K3A.cmd` utiliza el runtime .NET del proyecto y fija el directorio de revisión para evitar abrir otra instalación por una variable de entorno heredada. Si se ejecuta directamente el EXE, CASAMIGA_DATA_DIR conserva su prioridad existente sobre el archivo.

Revisar sin guardar ni confirmar operaciones:

1. Inventario → Harina → Ver detalle: rueda sobre contenido y tablas; comprobar 50 kg y Bulto 25 kg Activa.
2. Recetas: rueda sobre el editor largo y tablas vacías; al límite de una tabla con filas debe continuar la página.
3. ComboBox cerrado con foco: la rueda no debe cambiar selección; abierto: debe desplazar sus opciones normalmente.
4. Confirmar teclado, arrastre de barras y touchpad físico.
5. Editor de receta: Masa/Volumen/Piezas; entrar y Cancelar, sin crear receta.
6. Entrada/Merma/Conteo: no aparece OperationId. No guardar. Saldo inicial también está cubierto por prueba aislada; no repetir el de Harina.
7. Ver mensajes vacíos en Recetas, Perfiles, Nueva producción, Borradores e Historial según sus datos.
8. Ver detalle/Quitar/Calcular/Crear deben quedar deshabilitados cuando falta selección/configuración; texto legible.
9. Volver a los módulos y cerrar sesión sin registrar operaciones.

## Backlog conservado — no implementado

### 1K.3B — Inventario

Búsqueda en vivo/autocompletar; X para limpiar; Cantidad absoluta contextual; equivalencias como 1 bulto = 25 kg; preview existencia actual/movimiento/resultante; Referencia externa → Referencia/factura/nota; nombres humanos para Delta/Antes/Después; Saldo MAIN más claro; ocultar zona horaria técnica; advertencia/asociación de materias primas con ventas; nombres de producto en lugar de ProductId; ayuda específica de Saldo inicial/Entrada/Merma/Conteo; confirmaciones visuales más profesionales.

### 1K.3C — Recetas

Selector de ingredientes buscable; versión automática; unidad como selector validado y unidades compatibles por dimensión; explicación de Rendimiento; ayuda Política; alternativas visuales por grupos; separar Nuevo grupo de Agregar opción al grupo; UX de levadura fresca frente a Tradi Pan; textos de fórmulas publicadas; carga de recetas reales pendiente de autorización y datos.

### 1K.3D — Producción

Selector buscable; Producto/perfil activo → Producto a producir; necesidades/disponibilidad/faltantes; Planeado/real y Sin salida más claros; filtros de historial; ayuda contextual específica; carga/uso de datos reales pendiente de autorización.

### Datos reales y otros trabajos

Carga masiva de ingredientes, recetas reales, perfiles productivos reales y activación del consumo automático por ventas siguen pendientes. Rediseños estéticos adicionales requieren alcance propio. No iniciar carga de datos, producción real, impresión, instalador ni migración 017. Se preservan además los pendientes anteriores de fotografías reales, Pedidos/Tipo de venta y checkpoint físico Qian; no se cierran con 1K.3A.

## Archivos incluidos

Presentation:
`InventoryView.xaml`, `InventoryViewModel.cs`, `RecipeView.xaml`, `RecipeViewModel.cs`, `ProductionView.xaml`, `ProductionViewModel.cs`, `Resources/Theme.xaml`, `UiLabels.cs`, `NestedScrollBehavior.cs`, `ContextHelp.xaml`, `ContextHelp.xaml.cs`.

Pruebas:
`tests/CasaMiga.Tests/CasaMiga.Tests.csproj`, `InventoryViewModelTests.cs`, `Phase1K3AViewModelTests.cs`;
`tests/CasaMiga.PhotoTests/InventoryPresentationTests.cs`, `WpfTestHost.cs`, `UsabilityPresentationTests.cs`.

Documentación:
`docs/CURRENT_STATE.md`, `docs/NEXT_STEPS.md`, este reporte, `docs/evidence/phase1k3a-data.json`, `docs/evidence/phase1k3a-verification.md`.

## Checkpoint y continuación

Un único commit autorizado: `feat: improve global usability for phase 1K.3A`. El commit que contiene este reporte es hijo directo del HEAD inicial; el SHA final se entrega en conversación, evitando una referencia circular dentro del propio commit. Sin push, sin remoto agregado, sin cambio de identidad Git. Estado final limpio exigido antes de entregar.

Siguiente paso exacto: smoke manual de este checklist; después, autorizar separadamente FASE 1K.3B — usabilidad específica de Inventario, empezando por búsqueda/limpieza y explicación de cantidades/equivalencias, sin repetir el saldo inicial de Harina. Modelo recomendado: GPT-6 Astra, esfuerzo Ultra, por la relación entre cantidades, unidades y recuperación. No se inicia esa subfase automáticamente.

Riesgos/limitaciones pendientes: validación humana del scroll en mouse/touchpad y barras físicas, distintas escalas DPI/equipo final, aceptación visual, textos de ayuda específicos, ProductId en asociaciones y backlog enumerado. La suite no certifica operación comercial ni cierra FASE 1K.
