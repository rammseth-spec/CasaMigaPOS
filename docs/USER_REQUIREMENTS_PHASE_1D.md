# CASA MIGA POS — FASE 1D

La FASE 1C ha sido revisada y queda APROBADA.

Continúa exclusivamente con:

# FASE 1D — PANTALLA POS + CATÁLOGO OPERATIVO + CARRITO EN MEMORIA

IMPORTANTE:

En esta fase NO debe existir todavía una venta comercial persistida.

NO implementar todavía:

* Sale persistida;
* SaleItem persistido;
* Payment;
* cobro;
* efectivo;
* tarjeta;
* ticket;
* impresora;
* cajón;
* movimientos de inventario;
* ingredientes;
* recetas;
* producción;
* compras;
* impuestos;
* CFDI.

El objetivo es construir y validar la experiencia cotidiana del cajero antes de introducir operaciones financieras.

---

# 1. ANTES DE TODO — BASELINE

Antes de modificar código:

1. Lee:

   * README.md
   * docs/architecture.md
   * docs/CURRENT_STATE.md
   * docs/NEXT_STEPS.md
   * docs/domain.md
   * docs/database.md
   * ADRs relevantes;
   * documentación de FASE 1C.

2. Inspecciona el repositorio real.

3. Ejecuta:

   * clean;
   * restore;
   * build Release;
   * tests.

4. Confirma que las 82 pruebas existentes continúan pasando.

Si existe regresión previa:

DETENTE y repórtala.

---

# 2. CORREGIR PRIMERO EL RIESGO WPF DETECTADO

FASE 1C detectó que una excepción fatal de WPF fue capturada por Windows/.NET Runtime pero no por el logging técnico de Casa Miga.

Antes de construir la pantalla POS, corrige este riesgo.

Implementa captura apropiada de excepciones no controladas relevantes para una aplicación WPF.

Investiga documentación oficial de .NET/WPF antes de implementar.

Considera correctamente, según corresponda:

* DispatcherUnhandledException;
* AppDomain.CurrentDomain.UnhandledException;
* TaskScheduler.UnobservedTaskException.

NO intentes "seguir ejecutando" después de excepciones potencialmente corruptoras sólo para evitar que la aplicación cierre.

El objetivo es:

1. registrar información técnica útil;
2. evitar registrar secretos;
3. mostrar al usuario un mensaje comprensible cuando sea posible;
4. terminar de forma segura cuando no sea razonable continuar.

Documenta la política.

Añade pruebas donde sea técnicamente razonable.

---

# 3. GIT

El proyecto todavía no tiene commits.

NO inventes nombre ni correo del autor.

Verifica el estado de Git.

Si falta identidad:

reporta claramente los comandos que debe ejecutar el usuario para configurar:

git config user.name
git config user.email

No utilices datos inventados.

Una vez exista identidad válida, el estado aprobado hasta FASE 1C debería convertirse en el primer baseline histórico antes de continuar desarrollos importantes.

Si la identidad todavía no está configurada, NO bloquees la implementación de 1D por esto, pero mantenlo como riesgo operativo visible.

---

# 4. OBJETIVO FUNCIONAL

Al concluir 1D debe poder hacerse:

LOGIN

↓

ABRIR / RECUPERAR CAJA

↓

ENTRAR AL POS

↓

VER CATEGORÍAS ACTIVAS

↓

VER PRODUCTOS ACTIVOS

↓

AGREGAR PRODUCTOS AL CARRITO

↓

MODIFICAR CANTIDADES

↓

ELIMINAR PARTIDAS

↓

VER SUBTOTAL / TOTAL PRELIMINAR

↓

LIMPIAR CARRITO

Sin crear todavía una venta persistida.

Al cerrar la aplicación:

el carrito puede perderse en esta fase.

Debe quedar explícitamente documentado.

---

# 5. REQUISITO DE CAJA ABIERTA

La pantalla POS operativa requiere una sesión de caja OPEN.

Si no existe caja abierta:

el usuario puede consultar catálogo según permisos,

pero NO debe comenzar un carrito operativo de venta.

La validación no debe depender únicamente de ocultar botones.

Application debe comprobar el estado cuando corresponda.

---

# 6. PANTALLA POS

Construir la primera versión real de la pantalla que posteriormente utilizará Casa Miga.

Diseño conceptual:

CASA MIGA

┌──────────────────────────────────────────────────────┐
│ Categorías                                           │
│ [PAN DULCE] [PAN SALADO] [BEBIDAS]                  │
├─────────────────────────────┬────────────────────────┤
│                             │                        │
│ PRODUCTOS                   │ CARRITO                │
│                             │                        │
│ [Concha vainilla]           │ Concha vainilla   x2  │
│ [Concha chocolate]          │ Bolillo           x3  │
│ [Rol canela]                │                        │
│ [Dona]                      │                        │
│                             │                        │
│                             │ SUBTOTAL          $XX  │
│                             │ TOTAL             $XX  │
├─────────────────────────────┴────────────────────────┤
│ [LIMPIAR]                         [COBRAR - FUTURO]   │
└──────────────────────────────────────────────────────┘

NO habilitar todavía COBRAR.

Puede mostrarse deshabilitado o no mostrarse.

Preferiblemente no mostrar funciones inexistentes salvo que exista una razón UX clara.

---

# 7. EXPERIENCIA TÁCTIL

Ésta es la primera pantalla donde touch será especialmente importante.

Diseñar para:

* botones grandes;
* distancia adecuada entre controles;
* información legible;
* uso rápido;
* sin depender de hover;
* categorías fácilmente pulsables;
* productos fácilmente pulsables;
* cantidad modificable con pocos pasos.

Debe seguir funcionando con:

* mouse;
* teclado.

Pantalla táctil física continúa pendiente.

---

# 8. CATÁLOGO OPERATIVO

La pantalla POS debe consumir una consulta optimizada del catálogo.

Sólo mostrar:

* categorías activas;
* productos activos;
* categorías con productos disponibles según política definida;
* precio comercial actual.

NO reutilizar innecesariamente el ViewModel administrativo.

Administración y operación POS tienen necesidades distintas.

Puedes reutilizar Application/query services apropiados.

Evita duplicar reglas.

---

# 9. PRODUCTOS INACTIVOS

Un producto inactivo:

NO aparece en POS.

Una categoría inactiva:

NO aparece en POS.

La consulta POS debe aplicar esta regla en persistencia/Application y no depender sólo de filtrado visual.

---

# 10. CARRITO

Crear un modelo de carrito separado de las entidades persistentes de venta.

Ejemplo conceptual:

Cart

CartLine

CartLine debe conservar en memoria al menos:

* ProductId;
* ProductName;
* UnitPrice;
* Quantity;
* LineTotal.

Esto es deliberadamente similar a los futuros snapshots comerciales, pero NO es todavía SaleItem.

No persistirlo.

---

# 11. SNAPSHOT TEMPORAL

Cuando se agrega un producto al carrito:

captura su:

* ID;
* nombre;
* precio actual.

Si posteriormente el catálogo cambia mientras el carrito está abierto, define y documenta una política clara.

Para FASE 1D recomiendo:

El carrito conserva el nombre/precio capturado al momento de agregar la partida.

Pero como todavía no existe venta, al futuro proceso de cobro se deberá revalidar según la política que definamos en FASE 1E.

NO diseñar aún la persistencia de SaleItem.

---

# 12. CANTIDADES

Para productos físicos iniciales:

cantidad entera positiva.

Ejemplos:

1 concha.
2 bolillos.
6 donas.

No implementar todavía ventas fraccionarias.

Arquitectura futura puede contemplar cantidades decimales para productos pesables, pero no necesitamos resolverlas ahora.

Reglas:

quantity >= 1.

Incrementar cantidad.

Disminuir cantidad.

Si disminuye desde 1:

definir comportamiento claro.

Recomiendo eliminar la línea después de confirmación o mediante botón explícito de eliminar.

No permitir cero/negativos dentro del carrito.

---

# 13. AGREGAR PRODUCTO REPETIDO

Si el mismo producto se agrega nuevamente:

NO crear una nueva línea separada.

Incrementar cantidad de la existente.

Ejemplo:

Concha
+
Concha
+
Concha

=

Concha x3.

---

# 14. MONEY

Continuar utilizando Money/centavos Int64.

Operaciones checked.

Nunca:

double.

Nunca:

float.

Debe probarse overflow.

---

# 15. TOTALES

En esta fase:

LineTotal =
UnitPrice × Quantity

Subtotal =
Σ LineTotal

Como todavía no hay impuestos ni descuentos:

Total preliminar = Subtotal.

La UI puede mostrar:

TOTAL

pero internamente/documentalmente dejar claro que es total preliminar del carrito, no una venta final.

---

# 16. PRECIO CERO

Producto con precio $0.00 puede agregarse al carrito porque FASE 1C permitió cero técnicamente.

No inventar regla diferente aquí.

Posteriormente podremos definir restricciones comerciales adicionales.

---

# 17. PERMISOS

Utilizar:

catalog.view

y agregar un permiso operacional si es apropiado, por ejemplo:

pos.use

Define de manera explícita quién lo recibe.

Recomendación:

Administrator:
sí.

Manager:
sí.

Cashier:
sí.

La pantalla POS requiere:

usuario activo
+
permiso apropiado
+
caja abierta.

No depender sólo de navegación UI.

---

# 18. SESIÓN DE USUARIO

El carrito pertenece conceptualmente a:

usuario autenticado
+
terminal
+
sesión de caja.

Aunque sea in-memory, conserva esta asociación en el modelo/application state cuando sea apropiado.

Si cambia el usuario:

el carrito NO debe pasar silenciosamente al nuevo usuario.

Política recomendada:

logout con carrito no vacío
→ advertir y requerir limpiar/descartar.

No persistir carrito al cambiar usuario.

---

# 19. CIERRE DE CAJA CON CARRITO

No permitir que un carrito en memoria provoque cambios financieros porque todavía no es venta.

Pero evita estados confusos.

Si intenta cerrar caja con carrito no vacío:

mostrar advertencia y exigir:

descartar carrito

antes del cierre.

No guardar el carrito como venta.

---

# 20. CONTROLES DEL CARRITO

Como mínimo:

Agregar producto.

Incrementar cantidad.

Disminuir cantidad.

Eliminar línea.

Limpiar carrito.

El botón Limpiar debe pedir confirmación si existen partidas.

No pedir confirmación excesiva para acciones triviales como aumentar cantidad.

---

# 21. BÚSQUEDA EN POS

Añade búsqueda rápida si puede hacerse sin complicar la pantalla.

Debe permitir buscar al menos por:

* nombre;
* SKU.

Pensado para:

teclado
+
operación rápida.

No debe reemplazar botones de categorías/productos.

Si la búsqueda complica significativamente 1D, puede implementarse después, pero documenta la decisión.

---

# 22. ORDEN VISUAL

Categorías:

usar SortOrder.

Para productos, define un orden estable.

Inicialmente puede ser:

nombre.

Pero analiza si conviene introducir un campo POS display order futuro.

NO hagas una migración sólo por especulación si todavía no es necesario.

---

# 23. RENDIMIENTO

Aunque Casa Miga inicialmente tenga pocos productos:

la pantalla no debe realizar consultas individuales repetidas por cada botón.

Evitar N+1.

La carga del catálogo POS debe ser eficiente.

Prueba con un catálogo razonablemente grande.

No necesitas benchmark extremo.

---

# 24. CAMBIOS DE CATÁLOGO

Si un producto se desactiva desde administración mientras ya está en carrito:

el carrito en memoria conserva su línea hasta que sea descartado.

Pero antes de convertir ese carrito en una venta futura, FASE 1E deberá revalidar comerciabilidad.

Documentar este futuro requisito.

No introducir aún lógica de venta.

---

# 25. UI RESPONSIVA

La ventana debe comportarse bien en resoluciones comunes de POS.

Como mínimo probar visualmente:

1920×1080.

También probar una resolución menor razonable, por ejemplo:

1366×768.

No asumir monitor ultrawide.

La interfaz principal debe seguir siendo utilizable.

---

# 26. IDENTIDAD VISUAL

Continuar usando recursos centralizados.

No inventar ni redibujar logotipo.

Si los assets oficiales aún no están dentro del repositorio:

mantener placeholder explícito.

No bloquear el desarrollo por esto.

---

# 27. ACCESSIBILITY / TECLADO

Verificar:

Tab.

Enter.

Escape.

Foco visible.

No depender exclusivamente de color para estados críticos.

Textos legibles.

Botones con contenido comprensible.

---

# 28. ERRORES WPF

Después de implementar captura global de errores:

provocar en entorno de prueba un error controlado/de desarrollo para comprobar que:

* el logger recibe el evento;
* no registra secretos;
* el usuario recibe comportamiento seguro.

No provoques corrupción de datos para probarlo.

Documentar evidencia.

---

# 29. NO PERSISTIR CART

Esto es importante:

NO crear todavía:

sales
sale_items
payments

si todavía no existen.

Si existen tablas preparatorias de migraciones anteriores, NO empezar a utilizarlas como ventas reales en 1D.

El carrito debe permanecer fuera de la base.

---

# 30. PRUEBAS AUTOMATIZADAS

Añadir pruebas significativas.

Como mínimo:

### Carrito

1. carrito nuevo vacío;
2. agregar producto;
3. mismo producto incrementa cantidad;
4. agregar productos distintos;
5. incrementar;
6. disminuir;
7. cantidad no puede ser cero;
8. eliminar;
9. limpiar;
10. total correcto;
11. precio cero;
12. overflow protegido;
13. snapshot temporal de nombre/precio;
14. producto inactivo no disponible desde consulta POS;
15. categoría inactiva no disponible;
16. usuario sin permiso rechazado;
17. caja cerrada rechaza operación POS;
18. cambio de usuario no hereda carrito;
19. cierre de caja exige descartar carrito cuando corresponda.

### Consulta POS

20. sólo activos;
21. categorías ordenadas;
22. productos estables;
23. búsqueda nombre;
24. búsqueda SKU, si se implementa;
25. catálogo grande sin N+1 evidente según infraestructura disponible.

### Excepciones

26. política de logging WPF probada en componente testeable donde sea posible.

Mantener todas las 82 pruebas anteriores.

---

# 31. SMOKE TEST REAL

Usar aplicación WPF publicada de desarrollo.

Datos sintéticos.

### Caso A

Abrir caja.

Entrar a POS.

PAN DULCE.

Agregar:

Concha vainilla x2.

Verificar total.

### Caso B

Agregar:

Bolillo x3.

Confirmar dos líneas y total correcto.

### Caso C

Incrementar/decrementar cantidades.

Confirmar actualización inmediata.

### Caso D

Eliminar Bolillo.

Confirmar subtotal.

### Caso E

Limpiar carrito.

Confirmar vacío.

### Caso F

Intentar usar POS sin caja abierta.

Debe rechazarse correctamente.

### Caso G

Carrito con productos.

Intentar logout/cerrar caja.

Confirmar política de descarte.

### Caso H

Cerrar y reabrir aplicación.

Confirmar que no reaparece carrito antiguo.

Éste es el comportamiento esperado en 1D.

---

# 32. RESOLUCIONES

Smoke visual:

1920×1080.

1366×768.

Verificar:

* botones visibles;
* carrito utilizable;
* totales visibles;
* ninguna función crítica fuera de pantalla;
* scroll razonable cuando corresponda.

---

# 33. BACKUP

Como esta fase no introduce nuevos datos persistentes importantes aparte de configuración/documentación:

no modificar formato .cmbackup salvo necesidad real.

Comprueba que catálogo/caja existentes siguen restaurándose.

El carrito no debe aparecer en backups porque es temporal.

---

# 34. MIGRACIONES

Evitar nueva migración si 1D puede implementarse con el esquema actual.

Si realmente necesitas una:

justifica claramente por qué.

NO agregar columnas "por si acaso".

---

# 35. DOCUMENTACIÓN

Actualizar:

* CURRENT_STATE.md;
* NEXT_STEPS.md;
* architecture.md si corresponde;
* domain.md;
* roadmap.md;
* documentación de UI/operación cuando sea útil.

Documentar explícitamente:

"Cart is transient and not a sale."

---

# 36. VALIDACIÓN FINAL

Ejecutar:

1. clean;
2. restore;
3. build Release;
4. tests;
5. verify.ps1;
6. publicación de desarrollo;
7. smoke tests;
8. prueba 1920×1080;
9. prueba 1366×768;
10. revisión de logs;
11. revisión Git.

Reportar warnings y errores reales.

---

# 37. NO INICIAR 1E

Al concluir:

DETENTE.

No implementar:

* venta persistida;
* cobro;
* pago;
* ticket;
* hardware.

Entrega reporte:

## Resumen

## Captura global de errores

## Pantalla POS

## Catálogo operativo

## Carrito

## Money/totales

## Permisos

## Caja/sesión

## UI/touch readiness

## Resoluciones probadas

## Tests

## Build

## Smoke test

## Backup

## Git

## Riesgos

## Decisiones

## Próximo paso recomendado

DETENTE DESPUÉS DEL REPORTE.
