# CASA MIGA POS — FASE 1C

La FASE 1B ha sido revisada y queda APROBADA.

Continúa exclusivamente con:

# FASE 1C — CATÁLOGO BASE: CATEGORÍAS Y PRODUCTOS

El objetivo de esta fase es construir el catálogo comercial que posteriormente utilizará el POS.

NO implementar todavía:

* carrito;
* ventas;
* pagos;
* tickets;
* impresora;
* cajón;
* inventario;
* ingredientes;
* recetas;
* producción;
* compras;
* proveedores;
* reportes completos;
* CFDI.

La prioridad es obtener un catálogo confiable, administrable y listo para utilizarse posteriormente desde la pantalla de venta.

---

# 1. BASELINE

Antes de modificar código:

1. Lee:

   * README.md
   * docs/architecture.md
   * docs/CURRENT_STATE.md
   * docs/NEXT_STEPS.md
   * docs/domain.md
   * docs/database.md
   * ADRs relevantes
   * reporte/estado de FASE 1B.

2. Inspecciona el repositorio real.

3. Ejecuta:

   * clean;
   * restore;
   * build Release;
   * tests.

4. Confirma que las 49 pruebas actuales siguen pasando.

Si existe regresión previa, detente.

---

# 2. OBJETIVO FUNCIONAL

Al terminar FASE 1C debe ser posible:

INICIAR CASA MIGA POS

↓

LOGIN

↓

ENTRAR A ADMINISTRACIÓN DE PRODUCTOS

↓

CREAR CATEGORÍA

↓

CREAR PRODUCTO

↓

ASIGNAR PRECIO

↓

EDITAR PRODUCTO

↓

ACTIVAR / DESACTIVAR PRODUCTO

↓

CONSULTAR CATÁLOGO

↓

CERRAR APLICACIÓN

↓

REABRIR

↓

CONFIRMAR PERSISTENCIA

Todavía NO debe existir el proceso de venta.

---

# 3. CATEGORÍAS

Implementar entidad CATEGORY.

Como mínimo:

* Id.
* Name.
* Description opcional.
* SortOrder.
* IsActive.
* CreatedAt.
* UpdatedAt.

Reglas:

### CAT-1

El nombre es obligatorio.

### CAT-2

Evitar categorías duplicadas según una regla claramente definida y documentada.

Considerar normalización apropiada:

* espacios iniciales/finales;
* comparación razonable para mayúsculas/minúsculas.

No destruir el nombre visual ingresado por el usuario innecesariamente.

### CAT-3

Una categoría usada históricamente no debe eliminarse físicamente mediante operaciones normales.

Utilizar activación/desactivación.

### CAT-4

Una categoría inactiva no debe aparecer en futuras vistas normales del POS salvo que se solicite explícitamente.

### CAT-5

SortOrder debe permitir ordenar categorías manualmente en el futuro.

No implementar todavía drag-and-drop si no es necesario.

---

# 4. PRODUCTOS

Implementar entidad PRODUCT.

Como mínimo:

* Id.
* CategoryId.
* SKU opcional.
* Name.
* Description opcional.
* SalePrice.
* IsActive.
* TrackInventory.
* CreatedAt.
* UpdatedAt.

No implementar todavía stock real.

`TrackInventory` sólo prepara el modelo para fases posteriores.

---

# 5. PRECIO

Utilizar estrictamente la representación monetaria ya aprobada.

NO floating point.

Ejemplo:

$18.00 MXN

debe representarse internamente conforme a ADR-003-money.md.

La UI puede mostrar:

$18.00

pero el dominio/persistencia debe utilizar el modelo monetario seguro.

---

# 6. REGLAS DE PRODUCTO

Como mínimo:

### PROD-1

Nombre obligatorio.

### PROD-2

Precio de venta no puede ser negativo.

### PROD-3

Precio $0.00 puede permitirse técnicamente si existe una razón válida, pero documenta la decisión.

No crear reglas arbitrarias de negocio sin necesidad.

### PROD-4

Producto debe pertenecer a una categoría válida.

### PROD-5

Producto activo no debería poder asignarse a una categoría inactiva sin una política explícita.

Evalúa la opción más coherente y documenta la regla.

### PROD-6

SKU es opcional.

Si existe:

* normalizar apropiadamente;
* garantizar unicidad.

### PROD-7

Un producto ya utilizado históricamente en el futuro no deberá borrarse físicamente.

Desde ahora utiliza desactivación.

### PROD-8

Cambiar precio NO debe modificar datos históricos de ventas futuras existentes.

Todavía no existen ventas, pero el diseño debe asumir que posteriormente `SaleItem` almacenará snapshot del precio.

No intentes resolver ahora las ventas.

---

# 7. IDENTIFICADORES

No utilizar nombres como clave primaria.

Utilizar el modelo de IDs ya establecido por el proyecto.

No cambiar estrategia de identificadores sin justificación arquitectónica.

---

# 8. SKU

SKU debe ser opcional porque una panadería pequeña puede operar inicialmente sin códigos.

Ejemplos futuros posibles:

CON-VAI
CON-CHO
BOL-TRA

Pero NO rellenes automáticamente todos los SKU salvo para datos demo.

Si implementas normalización:

* documentarla;
* probarla;
* evitar resultados sorprendentes.

---

# 9. DATOS INICIALES

Proporcionar datos DEMO/DEVELOPMENT de forma controlada.

Categorías sugeridas:

PAN DULCE

PAN SALADO

BEBIDAS

Productos de demostración:

* Concha vainilla.
* Concha chocolate.
* Cuernito mantequilla.
* Dona.
* Polvorón.
* Puerquito piloncillo.
* Rol canela.
* Bolillo.
* Cuernito jamón y queso.
* Café de olla.

IMPORTANTE:

Estos datos son para desarrollo/demostración.

No asumir precios reales.

No inventar precios definitivos de Casa Miga.

Si necesitas precios para probar UI, utilizar valores claramente etiquetados como DEMO.

No introducirlos automáticamente en una instalación de producción sin decisión explícita.

---

# 10. ADMINISTRACIÓN DEL CATÁLOGO

Crear una interfaz sencilla de administración.

Debe permitir:

CATEGORÍAS

* listar;
* crear;
* editar;
* activar/desactivar.

PRODUCTOS

* listar;
* filtrar por categoría;
* crear;
* editar;
* activar/desactivar.

No construir todavía un ERP.

La interfaz debe ser rápida y clara.

---

# 11. PERMISOS

Agregar permisos explícitos, por ejemplo:

catalog.view

categories.create
categories.edit
categories.deactivate

products.create
products.edit
products.deactivate

Puedes simplificar la granularidad si existe una razón convincente, pero no regreses a autorización basada únicamente en roles dentro de la UI.

Application debe validar permisos.

Roles iniciales sugeridos:

Administrator:
todos.

Manager:
administración de catálogo.

Cashier:
sólo catalog.view cuando posteriormente necesite usar POS.

No conceder edición de catálogo al cajero por defecto.

---

# 12. AUDITORÍA

Auditar como mínimo:

* creación de categoría;
* edición relevante de categoría;
* activación/desactivación;
* creación de producto;
* cambio de nombre;
* cambio de categoría;
* cambio de precio;
* activación/desactivación;
* cambio de SKU.

Especial atención a cambios de precio.

Debe ser posible responder posteriormente:

¿Quién cambió el precio de la concha?

¿De cuánto a cuánto?

¿Cuándo?

No registrar ruido innecesario por abrir pantallas o consultar listas.

---

# 13. HISTORIAL DE PRECIO

NO necesito todavía un módulo completo de historial comercial.

Pero analiza si el audit log existente es suficiente para reconstruir cambios de precio o si conviene desde ahora una estructura simple y específica.

No sobrearquitectures.

Si audit log es suficiente para esta etapa:

úsalo.

Documenta la decisión.

---

# 14. TRANSACCIONES

Operaciones de creación/edición deberán ser atómicas.

Ejemplo:

editar producto
+
registrar auditoría

debe confirmarse junto.

Si falla auditoría en una operación sensible:

rollback.

Mantener el patrón ya establecido.

---

# 15. CONCURRENCIA

Aunque inicialmente exista una sola instalación:

protege en persistencia invariantes como:

* SKU único.
* Nombre de categoría único según política elegida.

No dependas sólo de comprobaciones previas en código.

Cuando exista riesgo de carrera:

la base debe mantener integridad.

---

# 16. BÚSQUEDA Y FILTRO

La pantalla de productos debe permitir como mínimo:

* búsqueda por nombre;
* búsqueda por SKU cuando exista;
* filtro por categoría;
* opción para mostrar inactivos.

No hace falta implementar búsqueda avanzada.

Mantén buen rendimiento para miles de productos aunque inicialmente tengamos pocos.

---

# 17. ORDEN

Categorías:

SortOrder.

Productos:

no hace falta implementar todavía orden manual complejo.

Para administración puede utilizarse:

* nombre;
* categoría;
* estado.

La futura pantalla POS tendrá su propio orden de botones.

No mezclar aún ambas necesidades.

---

# 18. UI

Mantén el lenguaje visual ya establecido.

Debe funcionar razonablemente con:

* mouse;
* teclado;
* pantalla táctil futura.

La administración puede ser ligeramente más densa que la pantalla de venta, pero sigue evitando controles pequeños.

Utilizar recursos visuales centralizados.

No hardcodear estilos repetidos.

---

# 19. NAVEGACIÓN

Introduce navegación mínima y clara.

Ejemplo conceptual:

CASA MIGA

[ CAJA ]

[ CATÁLOGO ]

[ ...futuro ]

No añadas botones vacíos para diez módulos futuros.

Mostrar solamente funciones existentes o claramente etiquetadas cuando sea necesario.

---

# 20. VALIDACIONES DE UI

Ejemplos:

Nombre vacío
→ mensaje claro.

Precio negativo
→ rechazo claro.

SKU duplicado
→ mensaje comprensible.

Categoría inválida
→ no guardar.

La UI debe ayudar al usuario, pero Domain/Application sigue siendo la autoridad.

---

# 21. BASE DE DATOS

Si requiere nuevas tablas:

crear migración 004 o el siguiente número correcto.

NO modificar:

001
002
003

Probar:

* base nueva;
* actualización desde v3;
* persistencia;
* restricciones;
* índices.

---

# 22. POS FUTURO

Aunque todavía no construiremos la venta, prepara una consulta de lectura adecuada para que posteriormente la pantalla POS pueda obtener rápidamente:

categorías activas

↓

productos activos

↓

precio

↓

orden/datos necesarios.

No construyas carrito.

No construyas Sale.

Sólo evita diseñar un catálogo difícil de consumir posteriormente.

---

# 23. PRODUCTOS INACTIVOS

Al desactivar un producto:

* no borrarlo;
* conservar auditoría;
* conservar todos sus datos;
* excluirlo de consultas operativas normales.

Debe poder reactivarse si la política del dominio lo permite.

Auditar la reactivación.

---

# 24. CATEGORÍAS INACTIVAS

Define cuidadosamente qué sucede al desactivar una categoría que tiene productos activos.

NO permitir estados incoherentes silenciosamente.

Opciones razonables:

A)
rechazar la desactivación mientras existan productos activos;

o

B)
desactivar en cascada explícita y auditada.

Para esta fase prefiero:

**rechazar la desactivación si contiene productos activos.**

El usuario deberá desactivar/mover primero esos productos.

Implementa esa regla salvo que exista un problema técnico serio y documentado.

---

# 25. TRACK INVENTORY

Agregar el campo al modelo si corresponde.

Inicialmente:

TRUE para panes/productos físicos.

Puede ser FALSE para elementos que eventualmente no requieran conteo.

Pero NO generar movimientos de inventario todavía.

Es únicamente metadato del producto.

---

# 26. IMÁGENES DE PRODUCTO

NO construir todavía almacenamiento complejo de fotografías.

Pero considera en arquitectura que posteriormente los botones del POS podrían tener:

* icono;
* fotografía;
* color.

No necesitamos esto para FASE 1C.

No añadir blobs a SQLite innecesariamente.

---

# 27. IMPUESTOS

No implementar lógica fiscal ni CFDI.

Si el modelo existente contempla tax fields futuros, no desarrollar un motor tributario ahora.

Los precios de demostración son precios comerciales simples.

---

# 28. PRUEBAS

Añadir pruebas significativas como mínimo para:

### Categorías

1. crear válida;
2. nombre vacío rechazado;
3. duplicada rechazada;
4. edición;
5. desactivación;
6. impedir desactivar categoría con productos activos;
7. reactivación;
8. permisos;
9. auditoría;
10. rollback.

### Productos

11. crear válido;
12. precio cero según política aprobada;
13. precio negativo rechazado;
14. categoría inexistente rechazada;
15. SKU opcional;
16. SKU duplicado rechazado;
17. edición de nombre;
18. cambio de precio;
19. cambio de categoría;
20. desactivación;
21. reactivación;
22. permisos;
23. auditoría de cambio de precio;
24. rollback si falla auditoría;
25. persistencia/reinicio;
26. filtro activos/inactivos;
27. búsqueda nombre;
28. búsqueda SKU.

Conservar todas las pruebas anteriores.

No persigas un número arbitrario de tests: prioriza invariantes reales.

---

# 29. SMOKE TEST

Realizar con UI real:

### Caso A

Crear:

PAN DULCE

Crear:

Concha vainilla

Precio DEMO:
$18.00

Confirmar aparición.

### Caso B

Editar precio DEMO:

$18.00
→
$19.00

Confirmar UI y auditoría.

### Caso C

Crear:

PAN SALADO

Crear:

Bolillo

Confirmar filtro por categoría.

### Caso D

Desactivar Bolillo.

Confirmar:

* permanece almacenado;
* desaparece de vista activa;
* aparece al habilitar "mostrar inactivos".

### Caso E

Intentar desactivar categoría con producto activo.

Confirmar rechazo.

### Caso F

Cerrar aplicación.

Reabrir.

Login.

Confirmar persistencia del catálogo.

Todos son datos sintéticos.

---

# 30. BACKUP

Comprueba que los nuevos datos del catálogo están correctamente incluidos en el backup existente por estar contenidos en la base SQLite.

No cambies el formato `.cmbackup` innecesariamente.

Realiza al menos una prueba de restauración donde el catálogo sobreviva.

---

# 31. DOCUMENTACIÓN

Actualizar:

* CURRENT_STATE.md
* NEXT_STEPS.md
* domain.md
* database.md
* roadmap.md si corresponde
* operations.md sólo si hay algo operativo nuevo.

Crear ADR únicamente si aparece una decisión arquitectónica relevante.

---

# 32. VALIDACIÓN FINAL

Ejecuta:

1. clean;
2. restore;
3. build Release;
4. todos los tests;
5. smoke test;
6. persistencia;
7. backup/restore;
8. logs;
9. revisión Git.

Reportar:

* errores;
* warnings;
* tests;
* comportamiento observado.

---

# 33. IMPORTANTE

NO iniciar FASE 1D automáticamente.

No construir ventas todavía.

Al terminar FASE 1C, detenerse.

Entregar reporte con:

## Resumen

## Categorías

## Productos

## Modelo monetario

## Permisos

## Auditoría

## Base de datos

## Transacciones

## UI

## Tests

## Build

## Smoke test

## Backup/restauración

## Git

## Riesgos

## Decisiones

## Próximo paso recomendado

DETENTE DESPUÉS DEL REPORTE.
