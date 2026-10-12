# Siguiente: piloto en sucursal (10/10/2026)

Caja y web completas (docs/PLAN_COMPLETAR.md: W1–W8, D1–D5, I1). Estabilización hecha: pruebas en navegador, de punta a punta, de volumen y simulacro de respaldo ([VOLUME.md](VOLUME.md)). **No se agregan funciones**: lo que sigue es usarlo.

1. **Usuario:** desplegar la nube en Vercel (rama `casamiga-web`; migraciones de la nube hasta 009) y capturar las variables ([WEB.md](WEB.md)).
2. **Usuario:** revisar por qué falló el CI de `main` en 1cb85cc (el CI ahora anota la prueba fallida en el correo) o correr `gh auth login`.
3. Instalador 1.0 para la caja (`scripts/build-installer.ps1 -Version 1.0.0`), sin firma salvo nueva decisión.
4. Piloto de una a dos semanas con [PILOTO.md](PILOTO.md) y el [MANUAL.md](MANUAL.md); corregir lo que salga.
5. Pendiente técnico: actualizar `@vercel/blob` a 2.x (avisos de seguridad de `undici`), probando respaldos.

Decisiones abiertas (no se hacen sin autorización): vender sin Internet en la web, impresora y cajón desde el navegador, consumo de inventario de las ventas web, firma del instalador.

---

# Registro anterior

## Smoke pendiente heredado de QA (integrado el 12/10/2026)

Tras la integración, pantalla completa es el botón de ícono de la barra (ayuda «Pantalla completa · F11») y el Centro operativo se abre con el reloj de la barra.

### Smoke manual de FASE 1K.4H-A-R1

Usar exclusivamente `work/Abrir-Casa-Miga-1K4H-A-R1.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4H-A-R1`, DataDirectory `work/phase1k4h-a-r1-smoke-20261010` y las credenciales verificadas entregadas en la conversación.

1. Iniciar sesión y comprobar `Pantalla completa · F11`; entrar con clic, confirmar `Salir de pantalla completa · F11` y salir con F11.
2. Abrir **Reporte diario** y confirmar 10/10/2026 con EN CURSO, $70.00, 2 tickets, 5 piezas y ticket promedio $35.00.
3. Revisar Ventas por hora: primera 09:00, última 17:00 y pico 09:00–09:59; comprobar tooltips/lectura accesible.
4. Revisar caja abierta: Apertura 08:00, Cierre En curso, responsable humano, Contado Pendiente y Diferencia Pendiente.
5. Consultar 06/10/2026 y confirmar «No hubo ventas registradas este día» sin gráfico vacío.
6. Consultar 07/10/2026 y revisar cierre exacto, hora de cierre y duración.
7. Consultar 08/10/2026 y revisar faltante de $2.00 con nota.
8. Consultar 09/10/2026 y confirmar «2 sesiones en el día» con apertura, cierre, responsables y métricas por sesión.
9. Revisar Productos más vendidos y el encabezado `Producto`.
10. Confirmar los textos deliberados de Producción, Materias primas y Preparación para mañana.
11. Revisar 1280×820, 1920×1080, 3440×1440, F11 y Escape; comprobar scroll vertical y ausencia de clipping/scroll horizontal global.
12. No imprimir, vender, cerrar caja, usar cajón ni modificar inventario/recetas/producción.

Esperar aprobación humana. No push, no PR, no 1K.4H-B, no 1K.4G-B/C y no 1K.5. Si se autoriza el siguiente desarrollo, 1K.4H-B debe comenzar con auditoría de confiabilidad de merma/producción; modelo recomendado GPT-6 Sol, esfuerzo alto.

## Registro anterior — smoke de 1K.4H-A

## Registro anterior — auditoría 1K.4G-A

# Próximo paso — responder auditoría 1K.4G-A y autorizar 1K.4G-B

Revisar `PHASE_1K4G_AUDIT.md` y `PHASE_1K4G_PRODUCT_MATRIX.md`. Completar `PHASE_1K4G_RECIPE_QUESTIONNAIRE.md` con datos reales de Casa Miga, empezando por:

1. identificar qué productos se elaboran y cuáles son reventa;
2. nombrar cada masa/preparación base y asignarle productos;
3. indicar rendimiento, ingredientes y cantidades exactas;
4. proporcionar gramaje/cantidad por pieza y extras;
5. definir huevo/COUNT y alternativas de levadura;
6. decidir qué productos terminados llevarán inventario;
7. proporcionar reservas mínimas y confirmar si 1.0 requiere muestra/cortesía y uso interno.

No convertir precios, fotos, nombres o el saldo de Harina del fixture en recetas confirmadas. No insertar datos, crear migración 018, iniciar 1K.4G-B, 1K.4H o reportes hasta autorización posterior. Si las respuestas confirman reservas, costos o destinos no venta persistentes, diseñar primero el alcance mínimo de 018 y detenerse antes de implementarlo.

Modelo recomendado para 1K.4G-B: **GPT-6 Astra** por la reconciliación de datos comerciales con versionado, ledger y posible migración; **GPT-6 Sol** sólo si el bloque autorizado se limita a captura de datos usando schema 17 sin cambios estructurales.

## Registro anterior — smoke conjunto 1K.4F-R2 + 1K.4E-R2

# Próximo paso — smoke conjunto 1K.4F-R2 + 1K.4E-R2

Usar exclusivamente `work/Abrir-Casa-Miga-1K4F-R2.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4F-R2`, DataDirectory `work/phase1k4f-r2-smoke-20261009`. La apertura automática queda en login y no equivale a aprobación humana.

1. Iniciar sesión; revisar a 1280×820, 1920×1080, ultrawide y F11 el orden Caja, resumen operativo cuando exista, reloj y F11.
2. Confirmar Caja abierta en la fixture; cerrar sesión sólo si se desea comprobar que el login no expone información operativa. No cerrar la caja.
3. Abrir el Centro operativo desde el reloj y desde el resumen; comprobar R1, Escape, temporizadores/listos/urgentes y ausencia de ceros sin actividad.
4. Desde Historial localizar `Venta #000017`. Sólo cuando el usuario autorice la prueba física, reimprimir en POS-80 y revisar dirección, icono, «Pedidos por WhatsApp», `461 276 7429`, separador, marca REIMPRESIÓN, productos, TOTAL, redes, agradecimiento, corazón, seis LF y corte.
5. La prueba de original requiere una venta sintética manual con autorización expresa; usar «Cobrar + imprimir ticket». Confirmar cero apertura de cajón y ausencia de duplicación.

No push, no PR, no iniciar 1K.4G/1K.5 y no modificar driver, puerto, codepage, corte o cajón.

## Registro anterior — smoke manual final de FASE 1K.4F-R1

Usar exclusivamente `work/Abrir-Casa-Miga-1K4F-R1.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4F-R1`, DataDirectory `work/phase1k4f-r1-smoke-20261009`. La apertura automática queda en login y no equivale a aprobación humana.

1. Revisar login; iniciar sesión con las credenciales verificadas entregadas en la conversación y abrir Centro operativo.
2. Confirmar que los editores de Temporizadores y Notas están cerrados y que la lista aparece primero.
3. Pulsar `+ Nuevo`, cancelar, volver a abrir y crear un timer; comprobar que el editor se cierra y el timer aparece. Crear tres y revisar densidad a 1280×820.
4. Terminar uno; confirmar `¡Listo!`, Atender, Reiniciar y Eliminar sin pérdida de jerarquía.
5. Pulsar `+ Nueva`, cancelar, crear una nota y editarla; comprobar rótulo «Editar nota», guardado y autocierre.
6. Crear un recordatorio, dejarlo vencer, pulsar Posponer y comprobar que sólo entonces aparecen 5/10/15 min; elegir 5 min y confirmar que se ocultan.
7. Dejar dos eventos vencer juntos y confirmar una sola alerta breve, doble pulso/micro-shake de 600 ms, sonido y estado persistente. Repetir con la ventana detrás/minimizada para revisar taskbar sin robo de foco.
8. Comprobar primer Escape sobre editor/desplegable y segundo Escape sobre panel; revisar F11, Alt+Tab, minimizar/restaurar, 1280×820 y 1920×1080.
9. Cerrar completamente y reabrir el mismo launcher; comprobar timer activo por deadline, notas y recordatorios. Observar parpadeos/CPU.
10. Cerrar sesión y confirmar retorno a login.

No imprimir, usar cajón, vender, abrir/cerrar caja ni modificar inventario/recetas/producción. `operations-hub.json` sigue fuera de `.cmbackup`. Esperar aprobación humana; no push y no iniciar 1K.4G/1K.5.

## Registro anterior — smoke 1K.4F

El build 1K.4F fue sustituido por R1 debido al espacio vertical ocupado por sus formularios permanentes.

## Registro anterior — 1K.4E-R1

# Próximo paso — smoke físico manual de ticket 1K.4E-R1

Usar exclusivamente `work/Abrir-Casa-Miga-1K4E-R1.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4E-R1`, DataDirectory `work/phase1k4e-r1-smoke-20261008` (schema 17). No usar accesos anclados ni builds anteriores.

1. Iniciar sesión con las credenciales verificadas entregadas en la conversación.
2. Abrir Historial, localizar `Venta #000017` y revisar $72.00, Efectivo, recibido $100.00 y cambio $28.00.
3. Reimprimir sólo cuando el usuario decida ejecutar la prueba física. Confirmar el orden: «Hecho en:», Variante 04 reducida, dirección, separador, REIMPRESIÓN, venta, productos, totales, pago, «Síguenos en:», logotipos de Facebook/Instagram, cuentas, agradecimiento, corazón, espacio final y corte.
4. Confirmar que no aparecen las palabras Facebook/Instagram, pero sí sus logotipos y `Casa Miga   @casamiga126`.
5. Confirmar legibilidad del nombre largo en dos líneas, TOTAL destacado, seis LF de separación, ausencia de apertura de cajón y ausencia de mutaciones comerciales.
6. Para probar ORIGINAL físicamente, realizar una venta exclusivamente sintética desde POS sólo con autorización humana expresa y usar «Cobrar + imprimir ticket».
7. Reportar el resultado físico antes de cerrar 1K.4E-R1.

No iniciar 1K.4F, migración 018, instalador, cambio de driver/puerto/codepage/corte o activación de cajón. No hacer push.

## Registro anterior — 1K.4E

# Próximo paso anterior — smoke físico manual de ticket 1K.4E

Usar exclusivamente `work/Abrir-Casa-Miga-1K4E.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4E`, DataDirectory `work/phase1k4e-smoke-20261008` (schema 17). No usar accesos anclados ni builds anteriores.

1. Iniciar sesión con las credenciales verificadas entregadas en la conversación.
2. Abrir Historial, localizar `Venta #000017` y revisar $72.00, Efectivo, recibido $100.00 y cambio $28.00; no debe aparecer GUID, `CASH` ni `Aplicado`.
3. Revisar snapshot: nombre corto, nombre largo, acentos y cantidades múltiples.
4. Sólo cuando el usuario decida hacer la prueba física, reimprimir esa venta. Confirmar «Hecho en:», Variante 04 nítida, dirección sin teléfono, TOTAL destacado, Facebook, Instagram, mensaje, corazón y corte parcial.
5. Confirmar marca `REIMPRESIÓN`, que el cajón no abre y que no cambian venta, pago, caja, inventario ni producción.
6. Para probar ORIGINAL físicamente, realizar una venta exclusivamente sintética desde el POS sólo con autorización humana expresa y usar «Cobrar + imprimir ticket». La preparación automática no crea requests de impresión.
7. Salir. Reportar el resultado físico antes de cerrar formalmente 1K.4E.

No iniciar 1K.4F, migración 018, instalador, cambio de driver/puerto/codepage/corte o activación de cajón. No hacer push durante esta fase.

# FASE 1K.4D-R1 aprobada — siguiente trabajo pendiente de autorización

El usuario aprobó el smoke visual final del build `work/Abrir-Casa-Miga-1K4D-R1.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4D-R1`, con DataDirectory aislado `work/phase1k4d-r1-smoke-20261008` (schema 17).

La fase queda cerrada. Publicar el checkpoint aprobado únicamente en `origin/QA` mediante avance normal y conservar `main` sin cambios. No crear ramas. No iniciar otra fase, migración, instalador ni promoción a producción hasta recibir autorización expresa.

La copia de revisión permanece como evidencia local. No imprimir ni accionar cajón desde ella.

## Registro anterior — smoke manual de Caja 1K.4D

# Próximo paso — smoke manual de Caja 1K.4D

Usar exclusivamente `work/Abrir-Casa-Miga-1K4D.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4D`, DataDirectory `work/phase1k4d-smoke-20261008` (schema 17). La base protegida sigue en schema 16, intacta. Las instrucciones antiguas quedan como registro histórico.

1. Iniciar sesión con las credenciales verificadas entregadas en la conversación. Confirmar CAJA CERRADA, 30 cierres y paginación 25+5.
2. Revisar detalles sintéticos: fondo $500 + ventas $1,250 = esperado $1,750; exacto $1,750, sobrante $1,780 (+$30) y faltante $1,700 (−$50). Notas visibles y sólo lectura.
3. En esta copia, abrir con $500. Contar $500: el esperado no aparece antes de confirmar conteo; después fondo $500 + ventas $0 = esperado $500. Cerrar sin nota.
4. En sesiones nuevas independientes con fondo $500, contar $530 y $450 respectivamente. Ver obligatoriedad de nota, escribir observación sintética y confirmar cierre definitivo. No intentar cambiar un conteo confirmado.
5. Consultar los cierres creados, responsables y notas. Revisar 1280×820, 1920×1080, F11 y Escape.
6. No vender desde UI, imprimir, activar cajón, modificar inventario ni producción. Las ventas requeridas ya se prepararon mediante tooling en históricos sintéticos.

Ver el checklist detallado en `PHASE_1K4D_REPORT.md`. Apertura automática en login no equivale a aprobación humana. Después del smoke esperar aprobación explícita antes del checkpoint GitHub. Sin push durante esta ejecución; no iniciar fase siguiente.

## Registro anterior — 1K.4C-R3

# Próximo paso — smoke manual de 1K.4C-R3

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.4C-R3`, abierta desde `work/Abrir-Casa-Miga-1K4C-R3.cmd`, con DataDirectory `work/phase1k4c-r3-smoke-20261007`. No usar el acceso anclado ni builds anteriores.

1. Iniciar sesión y entrar en Historial de ventas.
2. En Desde, escribir sólo `07102026` y confirmar el resultado progresivo/final `07/10/2026`; repetir en Hasta y aplicar.
3. Probar Backspace, Delete, selección parcial y Ctrl+A + `32102026`: debe quedar `32/10/2026`, conservarse y mostrar el error humano sin cambiar resultados.
4. Probar pegado `07102026` y `07/10/2026`, además del calendario; todas las rutas deben producir `07/10/2026`.
5. Revisar Todos, Hoy, Ayer y Rango; comprobar fechas `dd/MM/yyyy · HH:mm`, conteos y día final inclusivo.
6. Elegir Sesión actual: 57 ventas, primera página 50 y segunda siete; revisar `Venta #NNNNNN` y Caja separada.
7. Abrir una venta y confirmar el detalle financiero. Verificar que Reimprimir aparece según permiso, sin pulsarlo.
8. Revisar 1280×820, 1920×1080 y F11/Escape. Salir sin operar.

No vender, cobrar, abrir/cerrar/contar caja, modificar inventario/producción ni imprimir. Después del smoke, esperar confirmación explícita. No iniciar 1K.4D ni publicar: el siguiente checkpoint GitHub permanece después de 1K.4D.

## Registro anterior — smoke de 1K.4C-R2

R2 fue aprobada manualmente en clipping, validación, calendario, filtros, Historial, paginación y navegación. R3 sustituye únicamente su escritura manual por la máscara numérica.

## Registro anterior — smoke de 1K.4C-R1

R1 fue sustituida porque el smoke humano confirmó clipping real y borrado silencioso de fechas inválidas. No usar ese build para aprobar fechas.

## Registro anterior — smoke corto de 1K.4B-R1

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.4B-R1`, abierta desde `work/Abrir-Casa-Miga-1K4B-R1.cmd`, con DataDirectory `work/phase1k4b-r1-smoke-20261007`. No usar el acceso anclado ni builds anteriores.

1. Iniciar sesión y entrar al POS; observar el catálogo.
2. Agregar el producto A y confirmar que imágenes y tarjetas no parpadean.
3. Agregar A nuevamente y confirmar que la misma fila aumenta a dos piezas sin refrescar el catálogo.
4. Agregar B; usar + y −; eliminar B.
5. Confirmar durante todo el recorrido que búsqueda, categoría, página y scroll del catálogo permanecen estables.
6. No cobrar. Limpiar el carrito si hace falta y salir.

Si el recorrido no muestra parpadeo, cerrar formalmente 1K.4B. No iniciar 1K.4C, 1K.4D, Pedidos/Tiendas, instalador o migración 017. No publicar esta R1: el siguiente checkpoint GitHub permanece previsto después de 1K.4D.

## Registro anterior — smoke manual de 1K.4B

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.4B`, abierta desde `work/Abrir-Casa-Miga-1K4B.cmd`, con DataDirectory `work/phase1k4b-smoke-20261006`. No usar el acceso anclado ni builds anteriores.

1. Iniciar sesión, entrar al POS y confirmar el carrito vacío: `0 productos · 0 piezas`, Subtotal/TOTAL en $0.00 y las tres acciones deshabilitadas.
2. Agregar un producto: revisar nombre, precio unitario, importe, `1 producto · 1 pieza` y total. Pulsar la misma tarjeta otra vez y confirmar una sola fila con dos piezas.
3. Agregar un producto distinto y confirmar `2 productos · 3 piezas`; revisar que cada importe y el total sumen una sola vez.
4. Probar + y −. Al llegar a una pieza, − debe quedar deshabilitado; usar el icono con tooltip `Eliminar producto del carrito` para quitar la fila.
5. Agregar cinco o más productos. Confirmar al menos cuatro filas cómodas a 1280×820, scroll sólo en las partidas, desplazamiento mínimo al agregar una fila fuera de vista y pie fijo.
6. Probar nombre largo, Limpiar con confirmación y vuelta al estado vacío. No abrir el diálogo de cobro si no se realizará una venta autorizada.
7. Revisar 1920×1080 y F11; comprobar además búsqueda en vivo, × Limpiar, categoría y Escape de 1K.4A. Salir sin confirmar venta ni imprimir.

No ejecutar ventas, pagos, caja, impresión, inventario o producción. Tras el smoke, esperar confirmación explícita del usuario. No iniciar 1K.4C, 1K.4D, Pedidos/Tiendas, instalador o migración 017. No publicar 1K.4B en GitHub: el siguiente checkpoint remoto está previsto después de 1K.4D.

## Registro anterior — smoke manual de 1K.4A

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.4A`, abierta desde `work/Abrir-Casa-Miga-1K4A.cmd`, con DataDirectory `work/phase1k4a-smoke-20261006`. No usar el acceso anclado ni builds anteriores.

1. Login y POS: elegir Pan salado; buscar `Bolillo mantequilla`, luego `CM-003`; comprobar respuesta breve, $8 y categoría preservada.
2. Pulsar × Limpiar; buscar un texto sin coincidencias; con texto, pulsar Esc y confirmar que limpia sin navegar. Repetir en F11.
3. Revisar `SMOKE-1K4A-LARGO`: nombre hasta dos líneas y precio visible. Revisar `SMOKE-1K4A-SINFOTO`: placeholder `Sin imagen`.
4. Catálogo: cambiar rápidamente Todos/Pan dulce/Pan salado, activar Mostrar inactivos y probar búsqueda por nombre/SKU; confirmar que no reaparecen resultados de un filtro anterior.
5. Nuevo producto: llenar datos sintéticos, elegir categoría existente y abrir `+ Nueva categoría`; confirmar que el borrador se conserva y la categoría nueva queda seleccionada. Cancelar cualquier alta que no deba persistir.
6. Editar un producto con foto provisional; confirmar `Origen de la imagen` y `Foto provisional`. No reemplazar ni quitar fotos durante este smoke.
7. Revisar 1280×820, 1920×1080 y F11; salir.

No ejecutar ventas, caja, impresión, inventario o producción. No modificar la base protegida para crear Pan salado. Después del smoke, esperar confirmación explícita del usuario; no iniciar 1K.4B, Historial, Carrito, Pedidos/Tiendas ni migración 017.

## Registro anterior — smoke final corto de 1K.3D-R1

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.3D-R1`, abierta desde `work/Abrir-Casa-Miga-1K3D-R1.cmd`, con DataDirectory `work/phase1k3d-r1-smoke-20261006`. No usar el acceso anclado ni builds anteriores.

1. Iniciar sesión, entrar en Producción y abrir Historial.
2. Confirmar que `5 planeadas · 4 reales` es legible; abrir el detalle y revisar `Plan de producción`, `Preparaciones del plan`, `Ingredientes consumidos` y cantidades pluralizadas.
3. Nueva producción: elegir el perfil sintético, capturar 5 piezas y calcular necesidades. Revisar cantidades MASS/VOLUME exactas y piezas en singular/plural.
4. Abrir la ayuda de Vista previa y confirmar Necesario/Disponible/Faltante/Estado, además de que calcular no modifica inventario.
5. Preparar el borrador, cambiar el resultado real de 5 a 4 y comprobar `1 pieza menos de lo planeado`.
6. No registrar esa producción; volver y salir.

Si este recorrido pasa, cerrar formalmente FASE 1K.3D. No iniciar Pedidos, Tiendas, migración 017 ni cambios en POS/Carrito/ticket.

## Registro anterior — smoke manual corto de 1K.3D

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.3D`, abierta desde `work/Abrir-Casa-Miga-1K3D.cmd`, con DataDirectory `work/phase1k3d-smoke-20261006`. No usar el acceso anclado ni builds anteriores.

1. Iniciar sesión y entrar en Producción.
2. Revisar el historial sintético: 5 piezas planeadas, 4 reales, fecha/hora local y cantidades humanas.
3. Nueva producción → seleccionar `Producto terminado sintético · SMOKE-PROFILE-001 · Versión 1` → capturar 5 piezas.
4. Calcular necesidades; revisar preparaciones, MASS/VOLUME/COUNT, Disponible, Faltante y Estado. Confirmar en Inventario que el preview no alteró saldos.
5. Abrir las ayudas de cantidad planeada, vista previa y resultado real; comprobar overlay, Esc, Alt+Tab/minimizar y ausencia de movimiento del formulario.
6. Preparar el registro, capturar el resultado real y registrar una sola producción sintética autorizada. Revisar resultado e historial.
7. Crear otro plan de 1,000 piezas sólo para preview; comprobar el faltante de COUNT, que no permite preparar el registro y que inventario permanece intacto. Cancelar.
8. Revisar 1280×820, 1920×1080 y F11. Salir sin tocar otros módulos.

No usar recetas comerciales, imprimir, vender, abrir/cerrar caja, crear migración 017 ni iniciar Pedidos/Tiendas. Tras el smoke, esperar confirmación explícita del usuario.

## Registro anterior — smoke final corto de 1K.3C-R3
Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R3`, abierta desde `work/Abrir-Casa-Miga-1K3C-R3.cmd`, con DataDirectory `work/phase1k3c-r3-smoke-20261006`. No usar el acceso anclado ni builds anteriores.

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Abrir Rendimiento; confirmar overlay compacto, asociado al `?` y sin movimiento del formulario. Esc debe cerrarlo.
3. Entrar en F11, abrir Rendimiento y pulsar Esc: debe cerrar la ayuda y conservar fullscreen.
4. Abrirla otra vez y usar Alt+Tab: debe desaparecer. Al volver a Casa Miga debe seguir cerrada y permitir una apertura manual nueva.
5. Abrirla otra vez, minimizar y restaurar: no debe quedar sobre el escritorio ni reaparecer al restaurar.
6. Revisar una ayuda de Ingredientes directos o Política de cantidad y cancelar el editor sin guardar.

Si pasa este recorrido, 1K.3C puede cerrarse formalmente. No guardar cambios, crear datos reales, operar ventas/inventario/caja/producción ni imprimir. No iniciar 1K.3D, Pedidos, migración 017 o instalador.

## Registro anterior — smoke manual de 1K.3C-R2

Usar únicamente `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R2`, abierta desde `work/Abrir-Casa-Miga-1K3C-R2.cmd`, con DataDirectory `work/phase1k3c-r2-smoke-20261005`. No usar el acceso anclado ni builds anteriores.

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador.
2. Abrir Rendimiento: confirmar que flota, no mueve campos y es compacto; pulsar Esc y confirmar que cierra sin salir del editor.
3. Abrir Alternativas: confirmar que la tabla no se desplaza.
4. Abrir Ingredientes directos y Política de cantidad: confirmar que cada `?` está junto a su concepto; cancelar.
5. Perfiles de producto → Versión 2: revisar Preparaciones por pieza y después Ingredientes directos/Política; confirmar popover, Esc y colocación; cancelar.

Si todo pasa, 1K.3C puede cerrarse formalmente. No guardar cambios, crear datos reales, operar ventas/inventario/caja/producción ni imprimir. No iniciar 1K.3D, Pedidos, migración 017 o instalador.

## Registro anterior — smoke manual de 1K.3C-R1

Usar únicamente la instancia `Casa Miga POS · Caja · DESARROLLO · 1K.3C-R1`, abierta desde `work/Abrir-Casa-Miga-1K3C-R1.cmd`, con DataDirectory aislado `work/phase1k3c-r1-smoke-20261005`. No usar el acceso anclado ni el build 1K.3C anterior.

1. Login → Recetas → `SMOKE-REC-001` → Versión 2 → Editar borrador; confirmar COUNT/VOLUME/MASS visibles.
2. Buscar `SIN-RESULTADOS-999`; confirmar el mensaje y, sin limpiar, que las tres filas, cantidades, unidades y políticas siguen visibles.
3. Buscar `SMOKE-MASA-001`; confirmar que MASS es candidato y COUNT/VOLUME siguen visibles. Pulsar × Limpiar y confirmar todo intacto.
4. Confirmar el encabezado «Política de cantidad».
5. Abrir las ayudas de Rendimiento, Política de cantidad, Ingredientes directos y Grupos de alternativas; revisar ancho compacto, instrucciones y ejemplo sintético.
6. En Perfiles de producto, abrir la ayuda de Preparaciones y revisar el mismo criterio.
7. Cancelar, reabrir Versión 2 y confirmar contenido original intacto. Continuar después con el smoke pendiente de Perfiles de producto.

No guardar cambios, crear datos reales, operar ventas/inventario/caja/producción ni imprimir. No iniciar 1K.3D, Pedidos, carga real, migración 017 o instalador. FASE 1K.3C-R1 queda técnicamente completada, no aprobada ni cerrada, hasta recibir el smoke del usuario.

## Registro anterior — smoke manual de 1K.3C

1K.3B fue aprobada manualmente por el usuario. **1K.3C está técnicamente completada; su smoke manual sigue pendiente.** Usar únicamente la instancia `Casa Miga POS · Caja · DESARROLLO · 1K.3C`, abierta desde `work/Abrir-Casa-Miga-1K3C.cmd`, con DataDirectory `work/phase1k3c-smoke-20261005`. No depender del acceso anclado.

Revisión segura, sin guardar:

1. Recetas: comprobar lista vacía, Ver detalle deshabilitado y Nueva receta.
2. Crear el formulario de nueva receta sólo para revisar campos y botón deshabilitado; no confirmar la creación. Cancelar.
3. En una versión ya disponible en un fixture futuro, buscar `Harina` o `MP-HAR-001` sin Enter; comprobar × Limpiar y el mensaje sin coincidencias. En esta copia no existen recetas: no crear una para forzar el recorrido.
4. Comprobar Masa/Volumen/Piezas y que sus unidades sean g/kg, ml/L o pieza sin cruces.
5. Revisar ayudas de Rendimiento, Política, Alternativas, Preparaciones y publicación.
6. Revisar que Nuevo grupo de alternativas y Agregar opción al grupo sean acciones distintas, y que Quitar/Guardar/Publicar/Activar dependan de selección/validez.
7. Comprobar scroll a 1280×820, rueda sobre tablas/selectores, Esc con ComboBox abierto y F11/Esc normal.

No guardar recetas, versiones, perfiles, asociaciones ni operaciones. No cargar Masa dulce, Donas, Polvorón u otros datos reales. No 1K.3D, producción real, ventas, impresión, migración 017 o instalador. Tras el smoke, esperar confirmación explícita del usuario.

## Registro anterior — smoke manual de 1K.3B

1K.3A, R1 y 1K.3B fueron aprobadas por el usuario antes de 1K.3C. Focales 142/142, suite completa 702/702, Release 0 errores/advertencias. Ver `PHASE_1K3B_REPORT.md` para resultados y limitaciones.

Usar únicamente la nueva instancia identificada `Casa Miga POS · Caja · DESARROLLO · 1K.3B`, abierta automáticamente desde `work/Abrir-Casa-Miga-1K3B.cmd` al terminar la entrega. Carpeta aislada: `work/phase1k3b-smoke-20261004`. No depender del acceso anclado; las credenciales se entregan en conversación.

Revisión segura, sin guardar ni confirmar:

1. Escribir Harina o MP-HAR-001 y comprobar búsqueda automática; × Limpiar restaura lista/foco. Probar texto sin coincidencias.
2. Abrir Harina: 50 kg, Bulto 25 kg activo y equivalencia 1 Bulto 25 kg = 25 kg. No repetir Saldo inicial.
3. Entrada: 1 Bulto 25 kg → +25 kg → 75 kg. Abrir/cerrar ayuda y cancelar.
4. Merma: 2 kg → −2 kg → 48 kg. Cancelar.
5. Conteo físico: 48 kg → diferencia −2 kg → 48 kg. Cancelar. Una cantidad vacía/incorrecta no permite confirmar.
6. Revisar etiquetas, fechas locales sin zona, historial con signos y columnas humanas. Asociaciones muestran Producto/SKU, nunca ProductId; si Harina no tiene asociaciones, conservarla así.
7. Comprobar botones, desplazamiento, ComboBox cerrado/rueda, Esc con desplegable abierto y F11, sin registrar operaciones.

Después del smoke, esperar autorización. No 1K.3C/1K.3D, Pedidos, recetas/perfiles reales, producción, ventas, impresión, activación de consumo ni migración 017. FASE 1K sigue abierta.

## Registro anterior — smoke manual de 1K.3A-R1

**Corrección técnica completada; smoke manual R1 pendiente.** Usar la instancia que Codex deja abierta al terminar, identificada `Casa Miga POS · Caja · DESARROLLO · 1K.3A-R1`. No depender del acceso anclado. Launcher específico: `work/Abrir-Casa-Miga-1K3A-R1.cmd`; DataDirectory aislado: `work/phase1k3a-r1-smoke-20261003`. La base R2 original debe permanecer byte-idéntica.

Repetir únicamente las partes fallidas, sin guardar operaciones:

1. Inventario → Harina → Editar → Tipo abierto → Esc cierra sólo el desplegable, sin abandonar editor ni salir de fullscreen. Después comprobar Esc normal y F11. Volver sin guardar.
2. Recetas → texto «Puedes crear y editar recetas y perfiles», estado vacío y Ver detalle deshabilitado sin selección.
3. Nueva receta → Dimensión muestra Masa/Volumen/Piezas. Cancelar.
4. Producción → Nueva producción → estado vacío sin perfiles; Agregar/Quitar/Calcular/Crear deshabilitados, incluso Quitar con la única fila seleccionada. Cancelar.

Harina sigue con **50 kg** y presentación HAR-BULTO-25KG de 25 kg activa. No repetir Saldo inicial, cargar recetas/perfiles, crear producción, vender ni imprimir. Suite 679/679, focales 18/18, Release 0/0. Leer `PHASE_1K3A_R1_REPORT.md`, `evidence/phase1k3a-r1-verification.md` y la nueva regla permanente `review-builds.md`. La comparación de binarios no identifica con certeza qué versión abrió el usuario anteriormente.

Detenerse tras entrega R1. No iniciar 1K.3B ni declarar aprobación humana hasta confirmación explícita. Para una siguiente subfase se requiere nueva autorización.

## Registro anterior — smoke de 1K.3A

1K.3A implementada y verificada: 672/672, Release 0/0, schema 16 intacto. Leer `PHASE_1K3A_REPORT.md`, que conserva completo el backlog observado y el checklist. Abrir `work/Abrir-Casa-Miga-1K3A.cmd` para revisar la build equivalente a R2. Comprobar rueda sobre página/tablas vacías/tablas con filas, ComboBox cerrado y abierto, teclado/barras/touchpad, etiquetas españolas, ausencia de OperationId, estados vacíos y botones deshabilitados. No guardar ni confirmar operaciones durante esta revisión.

**Harina ya tiene 50 kg. No repetir Saldo inicial.** HAR-BULTO-25KG sigue activa y equivale a 25 kg. El saldo real fue capturado por el usuario antes de 1K.3A; el trabajo de esta subfase no modificó la base original.

Después del smoke y de una nueva autorización: FASE 1K.3B, usabilidad específica de Inventario (búsqueda/limpieza, cantidades/equivalencias, vista previa y ayudas), conservando idempotencia y datos existentes. Modelo recomendado GPT-6 Astra, Ultra. 1K.3C y 1K.3D permanecen posteriores; no cargar ingredientes/recetas/perfiles reales, crear producciones o activar consumo automático. FASE 1K continúa abierta. Detenerse después del reporte y commit único autorizado de 1K.3A; no push.

## Registro anterior — checkpoint de Inventario

El usuario autorizó el 01/10/2026 un único commit del bugfix ya verificado y validado manualmente. El checkpoint incluye código, regresión, reproducción, evidencia y documentación asociada; sin cambios funcionales posteriores, migraciones ni push. Revisión manual siguiente: Inventario → `MP-HAR-001` → Ver detalle → «Saldo inicial», en la copia R2 corregida. La revisión y eventual captura corresponden al usuario; este checkpoint no ejecuta ni inicializa inventario. Detenerse después del reporte. FASE 1K sigue abierta y las mejoras UX continúan pendientes.

## Registro previo al checkpoint Git

El 01/10/2026 el usuario confirmó «todo correcto» tras revisar la corrección y solicitó el reporte. Se registra esa aceptación para este defecto de Inventario; FASE 1K, el smoke global R2 y el checkpoint físico Qian no se cierran con ella. Entregar reporte y detenerse. No realizar commit/push ni iniciar mejoras pendientes automáticamente.

Referencia operativa: leer `INVENTORY_CRASH_FIX_20261001.md` y su evidencia. La compilación corregida es `src/CasaMiga.Presentation/bin/R2/CasaMiga.Presentation.exe`, actualizada y verificada (653/653). El detalle se consulta en Inventario → buscar `MP-HAR-001` → Ver detalle: `HAR-BULTO-25KG`, 25 kg, Activa marcada y sin saldo inicial. No recrear Harina/presentación ni inicializar stock para comprobar la corrección. Ante «Operación confirmada» con fallo de consulta, buscar nuevamente sin repetir guardado. Ante resultado incierto, no repetir: reiniciar y revisar artículo/historial antes de continuar.

Pendientes UX sólo documentados: búsqueda en vivo, X, ayuda ?, renombrar Saldo MAIN, etiquetas, estados vacíos y rediseño. No empezar esas mejoras, Recetas/Producción, fase siguiente o instalador. FASE 1K continúa abierta. Modelo recomendado Astra para revisión del cierre; detenerse tras reporte.

## Próximo paso anterior — smoke humano de 1K.2C-R2

Revisar R2 en `work/phase1k2c-r2-smoke-20260928` con la compilación Release de `src/CasaMiga.Presentation/bin/R2`. Credenciales en conversación. Validar catálogo completo, cards, Administrar foto → agregar/cambiar/quitar, preview y Cancelar; Cobrar sin ticket → Historial. Luego, con Qian disponible, una venta sintética mediante Cobrar + imprimir y una reimpresión desde Historial: logo Variante 04, centrado, nitidez, mismo folio, marca REIMPRESIÓN y corte. No se realizaron impresiones físicas automáticas. No considerar R2 aprobado por las capturas automatizadas.

La aplicación anterior ya está cerrada. R2 está preparada en salida separada y con su propio dataset para revisión. Leer `PHASE_1K2C_R2_REPORT.md`, `printing-variant04.md` y la evidencia de verificación.

Pedidos: sólo requisitos, en `orders-future-requirements.md`. Diseñar Order persistente anterior al cobro, pedidos diarios/plantillas, vínculo a Sale y políticas pendientes. Preguntar si alguna tienda recibe pan y paga después. No implementar crédito, reservas, Pedidos, migración 017 o instalador. FASE 1K continúa abierta; detenerse después del reporte.

## Registro anterior — R1

Revisar el smoke aislado preparado con 26 fotos PROVISIONAL, cards de 184 x 276 DIP con nombre/precio completos, rail POS contraído sólo con iconos centrados, carrito, ambos botones de cobro, administración de imagen y crop visual. Confirmar especialmente que al entrar al POS el catálogo comienza arriba y que expandir/contraer navegación no deja textos ni scrollbar recortados. Leer `PHASE_1K2C_R1_REPORT.md`, `product-photo-ui.md` y `phase1k2c-provisional-map.md`. No declarar aprobación visual hasta recibir confirmación explícita del usuario. FASE 1K permanece abierta; no cargar la instalación comercial, iniciar instalador ni cambiar 1K.2B.

Las 26 imágenes actuales son PROVISIONAL y requieren reemplazo futuro por fotografías reales proporcionadas por Casa Miga. El reemplazo debe usar el flujo administrativo REAL y conservar ProductId, precio, historial, receta e inventario.

Requerimiento futuro formal, no implementado: agregar `Tipo de venta` persistente en Sale con valores **Mostrador**, **Pedido** y **Pedido a tienda**. Pedido/Pedido a tienda debe admitir cliente, tienda o persona de referencia y nota opcional; debe aparecer en Historial, backup/restore, ticket y reimpresión. No debe ser texto temporal de Presentation. Requiere subfase separada con revisión de persistencia y modelo Astra.

## Registro anterior — revisión 1K.2B

Primero revisar la corrección de acceso del 28/09: en instalación nueva crear administrador con contraseña propia de 12–128 caracteres, confirmarla e iniciar sesión. No usar credenciales del backup de smoke en otra instalación. El formulario corregido, con login centrado y márgenes equilibrados, y la compilación Release están preparados para revisión. Esto no inicia 1K.2C.

1K.2B terminó con 621/621 pruebas y Release limpio. Leer `PHASE_1K2B_REPORT.md`, `product-photos.md` y la adenda normativa en `PHASE_1K2A_DESIGN.md`.

No continuar automáticamente. 1K.2C podrá incorporar UI administrativa de importación/crop, ProductCard FOTO + NOMBRE + PRECIO, fallback, caché acotada y carga estructurada explícita del paquete provisional. Mantener ProductId al sustituir PROVISIONAL por REAL. CSV/JSON autorizados serán la autoridad para productos; no inferir datos desde fotos.

Pendiente: aprobación de alcance, fotografías reales, equipo Windows 10/LTSC exacto y validación visual/rendimiento en POS. No se autoriza ahora carga de 26 productos, instalador ni cambios de reglas comerciales. Modelo recomendado: Sol para integración visual sobre estos contratos; Astra si cambia persistencia/backup/recovery.

## Registro histórico — siguiente paso al terminar 1K.2A

Leer `PHASE_1K2A_DESIGN.md`. Autorizar por separado ProductPhoto, DDL 016 propuesto, originales retenidos, JPEG/PNG de entrada, derivado JPEG Q85/512, caché 64 MiB, backup v2 compatible con v1 y recuperación explícita de media perdida. No crear migración ni implementar automáticamente. El diseño y probe quedan sin commit por la restricción expresa de arquitectura no autorizada.

Para validar calidad antes de fijar importación: 3–5 fotografías reales con Product identificado; la falta del resto no bloquea un catálogo mixto con fallback. Confirmar edición/build de Windows 10/LTSC y resolución/DPI para validación en el equipo objetivo. Modelo recomendado para implementación: Astra por atomicidad, persistencia, backup y recuperación.

## Propuesta previa — sustituida por el diseño 1K.2A

1K.1 está aprobada y cerrada. No iniciar 1K.2 automáticamente: preparar un alcance separado y obtener autorización antes de cambiar persistencia, migraciones o catálogo visual.

Requisito formal reservado para 1K.2: el POS usará fotografías reales proporcionadas por Casa Miga, asociadas a Product, con fotografía original, derivado optimizado, tarjeta FOTO + NOMBRE + PRECIO, fallback sin foto, caché/rendimiento, backup/restore y cambio de fotografía. No usar IA, stock, Internet ni productos ajenos. 1K.1 no agrega persistencia ni migración para imágenes.

Modelo recomendado para 1K.2: **Sol** para diseño WPF/POS; detenerse y reconsiderar si el alcance exige persistencia, migración o cambios de negocio.

## Registro anterior

# Próximos trabajos posibles — FASE 1J CLOSED

No iniciar automáticamente ninguna fase. Requieren alcance y autorización separados:

1. Carga autorizada de recetas reales Casa Miga.
2. Completar datos pendientes: cocoa concha, rendimiento cobertura, relleno rol, Procali por dona, azúcar dona, toppings dona, toppings polvorón, cuernito relleno, bolillo y puerquito. No inferir ni seedear.
3. Rediseño visual profesional.
4. Fullscreen/F11.
5. Hardening general V1.
6. Instalador Windows.
7. Validación Windows 10/11.
8. Pruebas físicas finales.

Recomendación: revisar y autorizar el siguiente alcance con Astra. El cierre de recetas/producción no certifica preparación comercial global.

## Registro histórico (sustituido por el estado superior)

# Próximo paso — proponer y autorizar 1J.5 por separado

1J.4 está completada y aprobada. Revisar `PHASE_1J4_REPORT.md`, `PHASE_1J4_SMOKE.md` y `recipe-production-ui.md`. No continuar automáticamente.

La propuesta de 1J.5 debe centrarse en hardening operativo: evaluar la restricción creador/terminal del borrador, reintentos y recuperación ante resultado incierto, smoke de faltantes/cambios de stock y zero-output total, backup/restauración del flujo UI y criterios para introducir recetas reales mediante captura autorizada. Mantener schema 15 salvo autorización expresa de una necesidad estructural; no iniciar rediseño, fullscreen, compras/proveedores, impresión de producción ni datos reales.

Modelo recomendado para proponer y ejecutar 1J.5: **Astra** si el alcance incluye recuperación/concurrencia crítica; **Sol** sólo para ajustes UI aislados ya cubiertos por contratos. FASE 1J permanece abierta; desarrollo, NO listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisión de 1J.3 y autorización separada de 1J.4

Revisar PHASE_1J3_REPORT.md, ADR-023 y evidencia phase1j3-*. No iniciar automáticamente UI. Preparar propuesta de interacción para fórmulas/perfiles, alternativas, preview sin reserva, actuals cero/superiores al plan, confirmación y recuperación conservando OperationId/ExpectedRevision.

Límites actuales: MAIN; saldos inicializados; plan DRAFT sin edición funcional; confirmación por creador/terminal; sin transferencia entre usuarios. No datos productivos reales, impresión, rediseño ni fullscreen. FASE 1J ABIERTA, desarrollo NO listo para producción. Astra recomendado para revisar contrato crítico; Sol para UI tras autorización.

## Registro anterior (histórico)

# Próximo paso — revisar corrección 1J.3B

Revisar PHASE_1J3B_REPORT.md y evidencia phase1j3b-*. Permisos aprovisionados mediante migración 015, sin bypass ni inicialización paralela. Autorizar separadamente la reanudación de 1J.3 funcional; los servicios futuros deberán revalidar HasPermission con la matriz aprobada.

1J.3 sigue PENDIENTE; 1J.4 no iniciada. Modelo recomendado para reanudar: Astra, conforme a la autorización. FASE 1J ABIERTA; desarrollo, no listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisar corrección 1J.3A

Revisar PHASE_1J3A_REPORT.md y evidencia phase1j3a-*. El bloqueo de esquema por salida totalmente cero queda corregido en 014. 1J.3 funcional permanece pendiente de autorización de reanudación: no hay PrepareProduction, ConfirmProduction ni nuevos permisos/UI. No iniciar 1J.4 automáticamente.

Modelo recomendado para reanudar 1J.3: Astra. Mantener writer de InventoryBalance único, atomicidad, consumo obligatorio y salida sólo cuando haya cantidades reales positivas. FASE 1J ABIERTA; desarrollo, no listo para producción.

## Registro anterior (histórico)

# Próximo paso — revisar 1J.2 y autorizar 1J.3 separadamente

Revisar PHASE_1J2_REPORT.md, ADR-022 y evidencia phase1j2-*. La persistencia está preparada; no hay comando de confirmar producción. No continuar automáticamente.

Antes del primer movimiento deben aprobarse: política MASS/VOLUME no representable; actual=0 y actual>planned (incluido lote totalmente cero frente a salida positiva obligatoria); contrato canónico/IDs deterministas y recuperación; permisos y auditoría Application; revalidación de configuración, asociaciones comerciales y stock en la misma transacción del writer existente. No inferir recetas reales ni activar datos productivos. UI, impresión, rediseño y fullscreen no pertenecen a este checkpoint.

Modelo recomendado para 1J.3: Astra. FASE 1J ABIERTA; desarrollo, no listo para producción. 1J.3 no iniciada.

## Registro anterior (histórico)

# Próximo paso — revisar 1J.1 y autorizar 1J.2 separadamente

Revisar PHASE_1J1_REPORT.md y ADR-021. Diseñar persistencia para Formula/Version, perfiles, alternativas y contrato multi-output; no volver al modelo Recipe→un producto. Definir constraints de una versión activa, inmutabilidad, snapshots y recuperación antes de escribir migración. No iniciar 1J.2 sin autorización expresa; 013 sigue ausente.

Pendientes comerciales: resolución de escalas MASS/VOLUME no representables, actual=0 y actual>planned, datos faltantes de cobertura/dona/polvorón/toppings. No inferir reglas ni semillas. Permisos y UI sólo propuestos. Modelo recomendado: Astra. FASE 1J ABIERTA; versión de desarrollo, no lista para producción.

## Registro anterior
# FASE 1I — CERRADA

1I.5 completada. Usuario aprobó explícitamente el smoke con «SI» después de las capturas finales: Cashier vendió con consumo ACTIVO, stock 8→6, carrito vacío/$0 e Inventario consistente. Primera venta adicional fue Administrator y se conserva como evidencia, sin borrar ni repetir su intención. Tres ventas manuales: $38 + $19 + $38 = $95 CASH; apertura $0, ExpectedCash calculado $95. Caja sintética permanece OPEN, sin conteo/cierre manual. ENABLED v4, dos consumos -2 y saldo 6; checkout.pending ausente. Consulta final read-only: integrity_check ok, FK 0.

402/402 pruebas aprobadas, 0 fallidas/omitidas; Release 0 errores/advertencias. Backup y restore automatizados verificados separadamente del smoke manual. Schema 12; hashes 001–012 intactos; 013 ausente. Cierre exclusivamente documental tras producto b2eeb2e84952d9a924223285e6e9f24afc77d6a2; no repetir suite sin cambios de producto. El commit de este cierre constituye el baseline oficial recuperable.

Versión de desarrollo, NO lista para producción. Ninguna fase posterior iniciada. No recetas, producción, compras/proveedores, rediseño, fullscreen, instalador, LAN ni nuevos efectos físicos. Próximo paso: proponer alcance de una fase posterior y solicitar autorización; no implementarla. Modelo recomendado para planificar: Astra.

## Registro anterior (histórico, sustituido por este cierre)
# Próximo paso — smoke humano 1I.5

Seguir PHASE_1I5_SMOKE.md por bloques cortos, dando siempre usuario y contraseña sintética al solicitar login. Empezar con SMOKEADMIN: saldo 10, verificar configuración, activar. Esperar confirmación antes de vender. No declarar cierre hasta completar aceptación humana y evidencia. Después emitir reporte de los 65 puntos autorizados y detenerse; no iniciar fase posterior.

## Registro anterior

# Próximo paso — revisar 1I.4 y autorizar 1I.5 por separado

1I.4 completada con smoke administrativo humano confirmado. Revisar PHASE_1I4_REPORT.md y ADR-020. No continuar automáticamente: 1I.5 requiere autorización nueva para su smoke integral de venta, recuperación, backup y cierre. Modelo recomendado: Astra. FASE 1I sigue ABIERTA; versión de desarrollo, no lista para producción. Migraciones 001–012 inmutables; no 013.

## Registro anterior

# Próximo paso — revisar 1I.3 y autorizar 1I.4

1I.3 completada. Revisar PHASE_1I3_REPORT.md, ADR-019 y evidencia phase1i3-* antes de autorizar 1I.4 (preflight, activación/pausa/reanudación y auditoría de configuración). No iniciar automáticamente. Modelo recomendado: Astra por validación de precondiciones y límites transaccionales.

Mantener flag DISABLED en instalaciones, migraciones 001–012 inmutables, efectos físicos post-commit y sales.complete como permiso comercial. No habilitar consumo mediante edición manual de la base. No 013, recetas, producción, proveedores, devoluciones, overrides, rediseño, fullscreen, LAN, instalador ni cajón. Sistema de desarrollo, no listo para producción.

## Registro anterior — bloqueo ya resuelto por autorización de 012

# Próximo paso — resolver frontera histórica de recovery

1I.3 está BLOQUEADA. Revisar PHASE_1I3_REPORT.md y autorizar un diseño separado de marca persistente e inmutable de obligatoriedad de evaluación. Si requiere migración 012, autorizarla expresamente. No implementar heurísticas temporales, no asumir que toda ausencia de evaluación es histórica y no continuar 1I.3 hasta resolver esta condición. Mantener 001–011 intactas. 1I.4 no autorizada ni iniciada.

## Registro anterior

# Próximo paso — revisar y autorizar FASE 1I.3

1I.2 completada. No iniciar automáticamente 1I.3. Revisar PHASE_1I2_REPORT.md y ADR-018; proponer/confirmar integración funcional del flag y plan dentro de la transacción de Checkout, autorización sales.complete, recuperación/idempotencia y bloqueo sin efectos parciales. La infraestructura técnica existe, pero Checkout no la llama y el flag está DISABLED.

No tocar migraciones 001–011 una vez aplicadas; cualquier cambio estructural posterior requiere nueva migración. Preservar Sale/Payment/ExpectedCash/folios/tickets y efectos físicos post-commit. Preflight/activación/pausa/reanudación y auditoría de configuración corresponden a 1I.4, no están implementados. Sin recetas, producción, proveedores, devoluciones, overrides, rediseño, fullscreen, LAN, instalador ni cajón.

Modelo recomendado 1I.3: Astra por la frontera transaccional y recuperación. Esperar autorización expresa antes de implementarla.

## Registro anterior
# Próximo paso — proponer y autorizar FASE 1I.2

1I.1 está completada. No iniciar automáticamente 1I.2. La siguiente propuesta debe fijar la migración 011, tablas de evaluación/snapshot, estado disabled/enabled/paused, permiso de activación, algoritmo versionado de InventoryOperationId, persistencia sobre la misma transacción de Checkout, triggers, upgrade/rollback y pruebas.

Mantener sin cambios hasta autorización: CheckoutService, ILocalUnitOfWork, SqliteInventoryStore, migraciones 001–010, ventas, pagos, ExpectedCash, tickets e impresión. No habilitar consumo, no crear flag, no consumir ventas históricas y no implementar recetas, producción, devoluciones u override.

Modelo recomendado para 1I.2: Astra por la reconstrucción estructural y la frontera transaccional; detenerse si el diseño no puede garantizar commit único y recuperación idempotente.

## Registro anterior

# Próximo paso — planificar la fase posterior a 1H

FASE 1H está cerrada. No iniciar automáticamente otra fase. La próxima conversación debe proponer alcance, invariantes, riesgos, pruebas y exclusiones, y esperar autorización expresa antes de implementar.

Propuesta recomendada: definir primero la relación entre inventario y venta (`SALE_CONSUMPTION`) como un plan separado, incluyendo política de existencias insuficientes, reversos, recetas/versiones, productos elaborados, atomicidad con Checkout y migración aditiva. El cierre 1H no autoriza ninguna de esas decisiones.

Mantener fuera hasta nueva autorización: consumo automático por ventas, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, cajón, LAN, rediseño profesional, fullscreen e instalador. Modelo recomendado para planificar: Sol; reservar Astra para un defecto técnico complejo demostrado.

## Registro anterior

# Próximo paso — proponer FASE 1H.5

1H.4 está completada y aprobada. No iniciar automáticamente 1H.5. La siguiente conversación debe proponer su alcance, invariantes, pruebas y exclusiones para autorización expresa.

Mantener fuera hasta autorización: consumo de inventario por ventas, `SALE_CONSUMPTION`, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, impresión de inventario, cajón, LAN, rediseño profesional, fullscreen e instalador. Modelo recomendado para 1H.5: Sol por continuidad y costo; reservar Astra para un defecto complejo de concurrencia, recuperación o arquitectura.

## Registro anterior

# Próximo paso — proponer y autorizar FASE 1H.4

1H.3 está completada. FASE 1H permanece abierta. No iniciar automáticamente 1H.4.

La siguiente propuesta puede definir una UI WPF mínima para inventario sobre los servicios existentes, sin acceder a SQLite ni enviar deltas arbitrarios. Debe decidir el ciclo de vida del OperationId en formularios/reintentos, preparación y confirmación de movimientos, visualización de unidades atómicas/presentaciones, tratamiento de ConcurrencyConflict y resultado incierto, y aviso de reconciliación inconsistente sin reparación automática.

Mantener fuera consumo por ventas, SALE_CONSUMPTION, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, impresión, cajón, LAN, rediseño, fullscreen e instalador. Modelo recomendado para proponer 1H.4: SOL; usar Astra sólo ante un defecto técnico complejo.

## Registro histórico anterior

# Próximo paso — retomar 1H.3 después de revisar el cierre 1H.2A

1H.2A está completada. Usar como baseline el commit documental de cierre cuyo padre es `76e654ce9b56132ff64248953931c95190858311`. No iniciar 1H.3 en este bloque.

Antes de exponer RECEIPT en Application, alinear su contrato Domain con el CHECK 010: reason_code NULL y observación opcional válida. Preservar el resto de razones e invariantes. Retomar únicamente el alcance autorizado de 1H.3 tras la revisión del reporte; modelo recomendado: SOL, reservando Astra para defectos complejos.

No activar descuento por venta, recetas, producción, compras, proveedores, UI, hardware ni funcionalidades posteriores.

## Registro histórico anterior
# Próximo paso — proponer y autorizar FASE 1H.3

FASE 1H está abierta y 1H.2 está completada. No iniciar automáticamente 1H.3.

La siguiente propuesta debe definir los casos de uso Application para configurar artículos y registrar OPENING_BALANCE, RECEIPT, ADJUSTMENT_GAIN, ADJUSTMENT_LOSS y WASTE. Debe envolver el puerto persistente ya probado, revalidar permisos, conservar motivos estructurados y decidir el flujo de preparación/confirmación y resultado incierto antes de diseñar UI.

No modificar Checkout ni activar SALE_CONSUMPTION. Mantener fuera recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, UI profesional y hardware.

## Registro histórico anterior

# Próximo paso — autorizar FASE 1H.2

FASE 1H está iniciada y 1H.1 está completada. No iniciar automáticamente 1H.2.

La siguiente subfase debe diseñar y autorizar la migración 009, el ledger inmutable y la proyección reconstruible `InventoryBalance`. Antes de escribir persistencia se debe escoger una sola autoridad transaccional para actualizar el saldo: Application/store o un mecanismo controlado de SQLite. No pueden coexistir dos mecanismos independientes.

Mantener fuera de 1H.2: Checkout, descuento por venta, recetas, producción, compras, proveedores, costos, lotes, caducidad, transferencias, UI y hardware. Preservar migraciones 001–008.

## Registro histórico anterior

# Próximo paso después de FASE 1G

FASE 1G está cerrada dentro del alcance autorizado. No iniciar automáticamente otra fase.

Siguiente acción recomendada: proponer y autorizar un bloque de preparación para producción. Si primero se adquiere el cajón, elaborar un bloque independiente con modelo, voltaje, cableado/conector y comando Qian documentado antes de habilitar cualquier pulso. LAN puede validarse en otro bloque opcional. Mantener `openCashDrawer=false` hasta entonces.

## Registro histórico anterior

# Continuidad — FASE 1G Bloque E

FASE 1G ABIERTA; Bloque E validado físicamente. Bloque E.1 fue sólo análisis: no hubo impresión ni cambio de producto. El raster empieza en offset 6 y no tiene feed previo; el PNG aporta 1.00 mm de blanco superior y el resto observado es principalmente mecánico. Ver PHASE_1G_BLOCK_E1_TOP_MARGIN_REPORT.md.

Siguiente decisión: aceptar el margen mecánico o autorizar en un bloque separado un derivado técnico que quite únicamente ocho filas superiores totalmente blancas, con mejora máxima aproximada de 1 mm. No probar backfeed ni comandos no documentados para la Qian.

Baseline Bloque D: CP850, ESC t 2, GS V 1. Fuente única: assets/brand/Opcion07. Paquetes anteriores deprecados. Sistema de desarrollo, no listo para producción. No se inició ninguna fase posterior.

## Registro histórico anterior, sustituido por el estado superior

# Próximos pasos — proporcionar logo oficial para FASE 1G Bloque E

Actualización 2026-09-11: **FASE 1G ABIERTA; BLOQUE D VALIDADO FÍSICAMENTE; BLOQUE E DETENIDO ANTES DE IMPLEMENTAR POR FALTA DEL LOGO OFICIAL**. Implementación Bloque D: `283cfa4`.

Estado verificable: migraciones 001–008 sin cambios; 188 pruebas aprobadas, ninguna fallida u omitida; build Release sin errores ni advertencias. Winspool aceptó un único payload PC850 de 889 bytes en POS-80 como JobId 5. `ESC t 2` ocupa los tres primeros bytes; un solo `GS V 01` ocupa los tres últimos; no hay cajón. La fotografía y confirmación del usuario validaron caracteres, jerarquía, alineación, legibilidad, ausencia de basura y corte parcial.

Siguiente acción: proporcionar el archivo del logotipo oficial aprobado. Formato preferido: PNG monocromático con fondo transparente, alta resolución y proporciones originales. Debe ser una fuente autorizada y conservar sin reconstrucción la tipografía, estrella, vapor y geometría originales. El diseño permitirá sustituir posteriormente el asset sin cambiar la lógica.

Cuando el asset esté disponible, reanudar el Bloque E desde el checkpoint `PHASE_1G_BLOCK_E_CHECKPOINT.md`. No imprimir otra prueba, modificar el baseline PC850/corte ni avanzar a cajón, LAN o reimpresión UI mientras falta el logo.

Antes de producción continúan pendientes datos reales, logo monocromático aprobado, administración/recuperación de usuarios, zona horaria comercial configurable, retención/segunda copia/cifrado, pruebas físicas, licencia y distribución. FASE 1G no está cerrada.

Checkpoint vigente 1I.5: reabrir instalación existente, login SMOKECASHIER y comprobar venta con consumo ACTIVO; principal anterior fue Administrator. No cerrar fase ni repetir esa intención.

Revisión adicional completada: logo completo, iconos, hover estable, ausencia de cambios visuales al bloquear la navegación y pestañas de Catálogo/Recetas con selección verde.
# Próximo paso — smoke manual de FASE 1K.4F

Usar exclusivamente `work/Abrir-Casa-Miga-1K4F.cmd`, título `Casa Miga POS · Caja · DESARROLLO · 1K.4F`, DataDirectory `work/phase1k4f-smoke-20261008`. La apertura automática queda en login y no equivale a aprobación humana.

1. Antes de autenticar, confirmar la composición Login opción 3: usuario/contraseña cómodos, reloj a la derecha, hora y fecha únicamente; no deben verse nombres, estados ni conteos operativos.
2. Iniciar sesión con las credenciales verificadas entregadas en la conversación. Revisar Header D, F11 y el botón completo del reloj.
3. Abrir Panel 4; confirmar ancho compacto, scroll interno, tarjetas y ausencia de parpadeo o reconstrucción del módulo actual.
4. Revisar los escenarios aislados ya preparados: timer activo, pausado, listo; nota sin recordatorio, futura y vencida. No son datos de producto.
5. Crear un timer de 1 minuto y otro adicional; pausar, reanudar, navegar al POS, cerrar el panel, esperar finalización, abrir desde `1 listo`, atender, reiniciar y eliminar.
6. Crear una nota, editarla, agregar recordatorio corto, esperar su vencimiento, atenderla y eliminarla.
7. Con el panel abierto probar Escape, Alt+Tab y minimizar/restaurar. F11 debe conservar su prioridad después de cerrar el panel.
8. Revisar 1280×820 y 1920×1080/F11; confirmar targets cómodos, encabezado limpio sin actividad y CPU estable sin refresh del catálogo o fotos.
9. Cerrar sesión: debe salir de fullscreen y volver al login sin mostrar información operativa.

No imprimir, no usar cajón, no vender, no abrir/cerrar caja ni modificar inventario/recetas/producción. `operations-hub.json` no está incluido en `.cmbackup`; reprogramación se realiza mediante Editar y guardar una nueva fecha/hora. Esperar aprobación del smoke antes de cualquier publicación. No hacer push y no iniciar 1K.5.

## Registro anterior — 1K.4E-R1
