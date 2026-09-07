# PROYECTO: CASA MIGA POS

Quiero que trabajes como arquitecto de software senior y desarrollador principal de un sistema POS de escritorio para Windows llamado provisionalmente **Casa Miga POS**.

Este será un proyecto real utilizado en una panadería real, por lo que la prioridad absoluta será:

1. Integridad de datos.
2. Confiabilidad.
3. Simplicidad de operación.
4. Funcionamiento offline.
5. Mantenibilidad.
6. Compatibilidad con hardware POS estándar.
7. Capacidad de crecimiento.
8. Buena experiencia de usuario.
9. Documentación persistente.
10. Seguridad.

No quiero un prototipo desechable.

Quiero construir una base profesional que pueda utilizarse diariamente y evolucionar durante años.

---

# 1. CONTEXTO DEL NEGOCIO

Casa Miga es una panadería tradicional mexicana.

La producción inicial será pequeña y el sistema comenzará operando probablemente con una sola computadora/caja, pero debe diseñarse correctamente para poder evolucionar posteriormente.

Productos iniciales previstos:

* Concha de vainilla.
* Concha de chocolate.
* Posible concha especial.
* Cuernito de mantequilla.
* Dona.
* Polvorón.
* Puerquito de piloncillo.
* Rol de canela.
* Bolillo.
* Cuernito relleno de jamón y queso.
* Café de olla.

El sistema no debe diseñarse como un restaurante genérico ni como un ERP gigantesco.

Debe estar optimizado específicamente para la operación cotidiana de una panadería.

---

# 2. VISIÓN DEL SISTEMA

Casa Miga POS deberá crecer progresivamente para manejar:

* Punto de venta.
* Productos.
* Categorías.
* Caja.
* Tickets.
* Impresora térmica.
* Cajón de dinero.
* Usuarios.
* Permisos.
* Ingredientes.
* Recetas.
* Producción por lotes.
* Inventario de materias primas.
* Inventario de producto terminado.
* Merma.
* Costeo.
* Compras.
* Proveedores.
* Reportes.
* Auditoría.
* Respaldos.
* Posteriormente multiterminal.
* Posteriormente multisucursal.
* Eventualmente facturación electrónica mexicana.

No implementar todo inmediatamente.

Construirlo por fases.

---

# 3. FILOSOFÍA DE DESARROLLO

NO quiero que empieces escribiendo cientos o miles de líneas de código.

Primero:

1. Analiza los requisitos.
2. Investiga documentación oficial cuando sea necesario.
3. Compara alternativas técnicas.
4. Documenta las decisiones.
5. Diseña arquitectura.
6. Define criterios de aceptación.
7. Implementa una pieza pequeña.
8. Compila.
9. Ejecuta pruebas.
10. Valida.
11. Documenta el resultado.
12. Continúa.

Evita soluciones improvisadas.

Cuando exista una solución estándar, madura y comprobada, priorízala.

Distingue claramente entre:

* Hechos comprobados.
* Decisiones tomadas.
* Hipótesis.
* Experimentos.
* Pendientes.

---

# 4. USO EFICIENTE DEL CONTEXTO DE CODEX

Este proyecto será largo.

Por ello quiero minimizar la dependencia del historial de conversación.

El estado verdadero del proyecto debe vivir dentro del repositorio.

Crea documentación persistente que permita a una nueva sesión de Codex comprender rápidamente:

* Qué es Casa Miga POS.
* Arquitectura actual.
* Stack.
* Decisiones.
* Estado.
* Qué funciona.
* Qué falta.
* Problemas conocidos.
* Próxima tarea.

No cargues todo el repositorio innecesariamente para cada tarea.

Lee únicamente los archivos relevantes más la documentación central.

La calidad tiene prioridad sobre el ahorro de tokens.

---

# 5. REFERENCIAS OPEN SOURCE

Hemos identificado como referencias:

* Floreant POS.
* uniCenta oPOS.
* Odoo Community.

Pueden utilizarse para estudiar:

* Flujos POS.
* Diseño de caja.
* Tickets.
* Inventarios.
* Recetas.
* Producción.
* Hardware.
* Arquitectura.
* Casos límite.

IMPORTANTE:

NO copies código indiscriminadamente.

No incorporar código GPL, MRPL u otra licencia copyleft al núcleo de Casa Miga sin revisar primero las consecuencias legales y obtener autorización explícita.

Preferimos aprender patrones y realizar una implementación propia.

Mantén registro de cualquier dependencia externa y su licencia.

---

# 6. STACK TECNOLÓGICO

NO asumas automáticamente el stack.

Antes de comenzar la aplicación, realiza una investigación técnica breve pero seria comparando las mejores alternativas actuales para una aplicación POS Windows offline-first.

Como mínimo evalúa:

* .NET moderno + tecnología UI apropiada para Windows.
* Tauri.
* Otras alternativas solamente si tienen una ventaja real.

Evalúa:

* Windows 11.
* Instalador .exe/MSI.
* SQLite.
* Impresoras ESC/POS.
* USB.
* Ethernet.
* Cajón de dinero.
* Rendimiento.
* Tamaño.
* Actualizaciones.
* Mantenibilidad.
* Interfaz táctil.
* Ecosistema.
* Testing.
* Seguridad.
* Posibilidad futura de servidor/PostgreSQL.
* Viabilidad de mantenimiento durante años.

No elijas por moda.

Documenta la decisión mediante un ADR.

Una implementación nativa .NET es una opción que considero especialmente interesante, pero debes validarla antes de adoptarla.

---

# 7. ARQUITECTURA

Quiero separación clara entre:

PRESENTATION

↓

APPLICATION

↓

DOMAIN

↓

INFRASTRUCTURE

La lógica de negocio no debe vivir en controles de interfaz.

Ejemplo conceptual:

CasaMigaPOS

* Presentation
* Application
* Domain
* Infrastructure
* Tests

La arquitectura debe ser limpia pero pragmática.

NO sobrearquitecturar.

Somos inicialmente una sola panadería y una terminal.

Diseñar puntos de extensión futuros sin construir infraestructura distribuida que todavía no necesitamos.

---

# 8. BASE DE DATOS

Para la primera versión evalúa y, salvo que exista una razón técnica fuerte en contra, utiliza:

SQLite.

La arquitectura debe permitir migrar posteriormente a PostgreSQL.

Todas las modificaciones del esquema deberán utilizar migraciones versionadas.

Nunca modificar manualmente la base de producción.

Activar integridad referencial.

Definir correctamente:

* claves primarias;
* claves foráneas;
* índices;
* restricciones;
* transacciones.

---

# 9. DINERO

Nunca utilizar floating point binario para cantidades monetarias.

Utilizar una representación segura.

Cuando sea apropiado:

centavos enteros.

Ejemplo:

$125.50 MXN = 12550 centavos.

Documenta esta decisión.

---

# 10. UNIDADES

Utilizar unidades base consistentes.

Peso:

gramos.

Volumen:

mililitros.

Conteo:

piezas.

Ejemplo:

Compra:

25 kg harina.

Internamente:

25,000 g.

Las conversiones deben realizarse de manera explícita y testeada.

---

# 11. PRODUCTOS

Entidad PRODUCT.

Debe contemplar como mínimo:

* ID.
* SKU opcional.
* Nombre.
* Categoría.
* Descripción.
* Precio de venta.
* Control de inventario.
* Estado activo/inactivo.
* Fechas de creación/modificación.

Los productos históricos no deben desaparecer porque posteriormente se modifique su ficha.

---

# 12. INGREDIENTES

Entidad INGREDIENT.

Ejemplos:

* Harina.
* Azúcar.
* Huevo.
* Levadura.
* Margarina.
* Manteca.
* Leche.
* Sal.
* Vainilla.
* Cocoa.
* Canela.

Debe contemplar:

* Unidad base.
* Existencia.
* Existencia mínima.
* Último costo.
* Costo promedio.
* Proveedor preferente opcional.

---

# 13. RECETAS

Una receta relacionará un producto con ingredientes y cantidades.

Debe tener:

* Producto.
* Nombre.
* Versión.
* Rendimiento esperado.
* Ingredientes.
* Cantidades.
* Estado.

Las recetas deberán ser versionables.

No sobrescribir silenciosamente una receta histórica.

Un lote de producción deberá conservar qué versión de receta utilizó.

---

# 14. PRODUCCIÓN

La unidad principal será:

PRODUCTION BATCH.

Ejemplo:

Producto:
Concha vainilla.

Receta:
Concha v2.

Planeado:
60.

Real:
58.

Merma:
2.

Al completar producción:

1. Consumir materias primas.
2. Registrar movimientos.
3. Registrar producto terminado.
4. Conservar costos.
5. Conservar trazabilidad.

Todo esto deberá ser transaccional.

Si falla una parte crítica, no debe quedar media operación registrada.

---

# 15. INVENTARIO

REGLA FUNDAMENTAL:

El inventario debe derivarse de MOVIMIENTOS.

Nunca implementar simplemente:

stock = stock - X

sin registrar por qué ocurrió.

Tipos previstos:

* PURCHASE.
* PRODUCTION_CONSUMPTION.
* PRODUCTION_OUTPUT.
* SALE.
* WASTE.
* ADJUSTMENT.
* RETURN.
* TRANSFER en el futuro.

Cada movimiento debe tener:

* Fecha.
* Cantidad.
* Entidad.
* Tipo.
* Referencia de origen.
* Costo cuando corresponda.
* Usuario cuando corresponda.

Debe ser posible auditar cómo se llegó a una existencia.

---

# 16. COSTEO

El sistema deberá calcular:

* Costo por unidad de ingrediente.
* Costo por receta.
* Costo por lote.
* Costo por pieza.
* Margen bruto estimado.
* Evolución de costos.

Ejemplo:

25 kg harina = $429.

25,000 g.

Costo por gramo:

429 / 25,000.

Si una receta utiliza 1,000 g:

calcular el costo correspondiente.

NO permitas que cambiar hoy el precio de la harina modifique retroactivamente el costo histórico de una venta antigua.

Guardar snapshots de costos cuando corresponda.

---

# 17. VENTAS

Una venta debe incluir:

* Fecha/hora.
* Usuario.
* Sesión de caja.
* Terminal.
* Partidas.
* Cantidades.
* Precio.
* Descuento.
* Subtotal.
* Impuestos.
* Total.
* Pagos.
* Estado.

Estados iniciales:

* PENDING.
* COMPLETED.
* CANCELLED.
* REFUNDED.

Una venta completada NO debe borrarse.

Cancelaciones y devoluciones deben ser auditables.

---

# 18. SNAPSHOTS DE VENTA

Las partidas deben conservar los datos comerciales relevantes del momento de la venta.

Por ejemplo:

* Nombre del producto.
* Precio.
* Costo.
* Descuento.
* Impuestos cuando corresponda.

Si mañana cambia el nombre o precio del producto, un ticket histórico debe seguir representando correctamente lo ocurrido.

---

# 19. PAGOS

Inicialmente:

* Efectivo.
* Tarjeta.

Diseñar el modelo para permitir posteriormente:

* Transferencia.
* Otros métodos.

Una venta puede necesitar en el futuro múltiples pagos.

No diseñar el modelo suponiendo un único método para siempre.

---

# 20. CAJA

Implementar sesiones de caja.

Flujo:

APERTURA
↓
FONDO INICIAL
↓
VENTAS
↓
ENTRADAS/RETIROS
↓
CONTEO
↓
CIERRE

Registrar:

* Usuario.
* Terminal.
* Apertura.
* Fondo inicial.
* Ventas.
* Movimientos.
* Efectivo esperado.
* Efectivo contado.
* Diferencia.
* Cierre.

---

# 21. CORTE

Debe calcular como mínimo:

* Fondo inicial.
* Ventas efectivo.
* Ventas tarjeta.
* Entradas.
* Retiros.
* Efectivo esperado.
* Efectivo contado.
* Diferencia.
* Ventas totales.
* Tickets.
* Ticket promedio.
* Cancelaciones.
* Devoluciones.

---

# 22. MERMA

La merma NO debe ser simplemente un ajuste de inventario.

Debe ser una entidad/operación explícita.

Ejemplos:

* Producto quemado.
* Producto dañado.
* Producto no vendido.
* Error de producción.
* Caída.
* Caducidad.
* Consumo interno.

Registrar:

* Producto/ingrediente.
* Cantidad.
* Motivo.
* Usuario.
* Fecha.
* Costo estimado.

---

# 23. COMPRAS Y PROVEEDORES

Diseñar:

SUPPLIER
↓
PURCHASE
↓
PURCHASE ITEMS
↓
INVENTORY MOVEMENT
↓
COST UPDATE

Proveedor:

* Nombre.
* Contacto.
* Teléfono.
* Email.
* Dirección.
* Notas.
* Estado.

Compra:

* Proveedor.
* Fecha.
* Folio.
* Ingredientes.
* Cantidades.
* Unidad.
* Precio.
* Total.

---

# 24. USUARIOS Y PERMISOS

Roles iniciales:

ADMINISTRADOR.

ENCARGADO.

CAJERO.

No implementar seguridad mediante condicionales dispersos como:

if role == admin.

Crear permisos explícitos.

Ejemplos:

sales.create
sales.cancel
cash.open
cash.close
products.edit
inventory.adjust
purchases.create
reports.view
users.manage

Las contraseñas nunca deben almacenarse en texto plano.

Utilizar un algoritmo moderno y apropiado de hashing de contraseñas.

---

# 25. AUDITORÍA

Registrar operaciones sensibles:

* Cancelaciones.
* Devoluciones.
* Ajustes.
* Cambios de precio.
* Cambios de receta.
* Cierres de caja.
* Cambios administrativos.
* Acciones sensibles de usuarios.

Guardar cuando corresponda:

* Usuario.
* Timestamp.
* Acción.
* Entidad.
* ID.
* Estado anterior.
* Estado nuevo.

---

# 26. HARDWARE POS

Este proyecto utilizará hardware real.

La impresora elegida actualmente es:

**Qian QOP-T80UL-RI-02**

Características relevantes que deben verificarse nuevamente contra documentación oficial antes de implementar:

* Térmica.
* Papel de 80 mm.
* ESC/POS.
* USB.
* Ethernet/LAN.
* Autocortador.
* Puerto para cajón.
* Windows.

No acoples el dominio a esta impresora.

Crear abstracciones similares a:

IReceiptPrinter
ICashDrawer

y adaptadores de infraestructura.

Ejemplo:

EscPosReceiptPrinter.

El objetivo es poder reemplazar la impresora en el futuro sin modificar ventas.

---

# 27. CAJÓN DE DINERO

El cajón se conectará preferentemente a la impresora mediante su interfaz estándar de cajón.

El POS deberá poder realizar:

COBRAR EFECTIVO
↓
CONFIRMAR TRANSACCIÓN
↓
IMPRIMIR
↓
CORTAR
↓
ABRIR CAJÓN

Pero investiga y prueba cuidadosamente el comportamiento ESC/POS real.

No supongas comandos o pinouts sin documentación.

También deberá existir apertura manual autorizada desde el POS con auditoría.

No permitir aperturas silenciosas sin registro cuando se trate de una operación iniciada desde el sistema.

---

# 28. TICKETS

Separar:

TicketData

de:

TicketRenderer

de:

Printer.

Esto deberá permitir en el futuro:

* Ticket térmico.
* Reimpresión.
* PDF.
* Ticket digital.

El ticket inicial deberá contemplar:

CASA MIGA

PANADERÍA TRADICIONAL MEXICANA

Folio.

Fecha/hora.

Productos.

Cantidad.

Precio.

Subtotal.

Total.

Método de pago.

Efectivo recibido.

Cambio.

Mensaje final.

No asumir todavía requisitos fiscales de CFDI.

---

# 29. IDENTIDAD VISUAL

La aplicación pertenece a Casa Miga.

La identidad visual oficial deberá poder incorporarse mediante assets independientes.

No recrees ni alteres el logotipo desde código.

La interfaz debe sentirse:

* Cálida.
* Profesional.
* Moderna.
* Limpia.
* Fácil de usar.
* Adecuada para pantalla táctil.

No hacer una interfaz genérica gris tipo ERP antiguo.

Tampoco sacrificar usabilidad por decoración.

---

# 30. POS PRINCIPAL

Diseñar aproximadamente este flujo:

CATEGORÍAS
↓
PRODUCTOS
↓
TICKET ACTUAL
↓
TOTAL
↓
MÉTODO DE PAGO
↓
COBRAR

Debe ser extremadamente rápido.

Venta típica:

2 conchas
+
3 bolillos
+
1 rol

debe requerir muy pocas pulsaciones.

Botones grandes y adecuados para touch.

---

# 31. BACKUPS

Implementar desde temprano:

* Backup manual.
* Backup automático.
* Historial.
* Validación.
* Restauración.

La restauración debe probarse.

No considerar que existe una estrategia de backup hasta comprobar que un backup realmente puede restaurarse.

Nunca sobrescribir automáticamente el único backup existente.

---

# 32. LOGGING Y ERRORES

Implementar logging estructurado.

Los errores técnicos deberán quedar registrados sin mostrar mensajes incomprensibles al cajero.

La interfaz deberá mostrar errores útiles y seguros.

No registrar contraseñas ni información sensible innecesaria.

---

# 33. TESTS

Las reglas críticas deben tener pruebas automatizadas.

Prioridad especialmente alta:

* Dinero.
* Cambio.
* Totales.
* Costos.
* Conversiones.
* Inventarios.
* Producción.
* Caja.
* Cancelaciones.
* Transacciones.
* Migraciones.

No considerar terminado un módulo crítico sólo porque “funciona visualmente”.

---

# 34. TRANSACCIONES

Operaciones compuestas críticas deberán ser atómicas.

Ejemplo:

Completar venta:

* Crear venta.
* Crear partidas.
* Crear pagos.
* Crear movimientos de inventario.
* Actualizar caja.

Todo debe completarse correctamente o revertirse.

Lo mismo para producción y compras.

---

# 35. CONCURRENCIA FUTURA

Inicialmente probablemente habrá una sola terminal.

Sin embargo:

* No utilizar IDs dependientes de posición.
* No asumir que siempre habrá un único usuario.
* Identificar terminales.
* Mantener timestamps.
* Diseñar repositorios correctamente.

No implementar todavía sincronización distribuida.

Sólo evitar decisiones que hagan imposible crecer.

---

# 36. FUNCIONES QUE NO DEBEMOS IMPLEMENTAR TODAVÍA

No implementar en la primera fase:

* CFDI.
* Integración SAT.
* Contabilidad completa.
* Nómina.
* CRM.
* E-commerce.
* Delivery.
* Aplicación móvil.
* IA.
* Cloud complejo.
* Multisucursal.
* Programa de lealtad.

Primero necesitamos una caja impecable.

---

# 37. ROADMAP

## FASE 0 — FUNDACIÓN

Crear:

* Repositorio.
* Estructura.
* Documentación.
* ADRs.
* Stack.
* Base de datos.
* Migraciones.
* Logging.
* Configuración.
* Tests.
* Backup inicial.

## FASE 1 — POS MVP

Crear:

* Categorías.
* Productos.
* POS.
* Carrito/ticket.
* Venta.
* Efectivo.
* Tarjeta.
* Apertura de caja.
* Cierre.
* Ticket.

## FASE 2 — HARDWARE REAL

Integrar y validar:

Qian QOP-T80UL-RI-02.

Probar:

* USB.
* ESC/POS.
* Texto.
* Logo.
* QR.
* Corte.
* Cajón.

Crear una utilidad/diagnóstico de hardware independiente si resulta útil.

## FASE 3 — PRODUCCIÓN

* Ingredientes.
* Unidades.
* Recetas.
* Versiones.
* Lotes.
* Producción.
* Merma.

## FASE 4 — INVENTARIO/COSTOS

* Movimientos.
* Compras.
* Proveedores.
* Costo promedio.
* Costo por receta.
* Costo por producto.
* Márgenes.

## FASE 5 — ADMINISTRACIÓN

* Usuarios.
* Permisos.
* Auditoría.
* Reportes.
* Backups avanzados.

## FASE 6 — ESCALABILIDAD

Solamente cuando sea necesario:

* Multiterminal.
* PostgreSQL.
* Servidor.
* Sincronización.
* Multisucursal.

---

# 38. DOCUMENTACIÓN OBLIGATORIA

Crea inicialmente una estructura similar a:

CasaMigaPOS/
README.md

```
docs/
    architecture.md
    domain.md
    database.md
    roadmap.md
    CURRENT_STATE.md
    NEXT_STEPS.md

    decisions/
        ADR-001-technology-stack.md
        ADR-002-database.md
        ADR-003-money.md
        ADR-004-inventory-ledger.md
        ADR-005-hardware-abstraction.md

research/
    pos-reference-notes.md
    hardware/
        qian-qop-t80ul-ri-02.md

src/

tests/

migrations/

scripts/
```

Puedes mejorar esta estructura si existe una razón técnica.

---

# 39. CURRENT_STATE.md

Este archivo es especialmente importante.

Debe actualizarse después de cada etapa significativa.

Debe indicar brevemente:

* Versión/estado.
* Qué funciona.
* Qué está parcialmente implementado.
* Qué no funciona.
* Decisiones vigentes.
* Problemas conocidos.
* Últimas pruebas realizadas.
* Próximo paso recomendado.

Debe permitir continuar el proyecto en una nueva sesión sin depender de la conversación anterior.

---

# 40. NEXT_STEPS.md

Mantén una lista corta y priorizada.

No convertirla en un cementerio de cientos de tareas.

Debe indicar qué debería hacer la siguiente sesión.

---

# 41. DEFINITION OF DONE

Una tarea no está terminada únicamente porque existe código.

Cuando corresponda deberá cumplir:

* Compila.
* Tests pasan.
* Manejo de errores.
* Persistencia correcta.
* Integridad de datos.
* Documentación actualizada.
* Sin warnings importantes nuevos.
* Sin secretos hardcodeados.
* Sin dependencias innecesarias.
* Sin regresiones conocidas.

---

# 42. REGLA SOBRE CAMBIOS

Antes de cambios grandes:

1. Explica el problema.
2. Investiga.
3. Propón solución.
4. Evalúa riesgos.
5. Implementa incrementalmente.
6. Prueba.
7. Revisa resultados.
8. Documenta.

No hagas refactors masivos simplemente porque el código “podría verse mejor”.

---

# 43. REGLA SOBRE DEPENDENCIAS

Antes de agregar una dependencia importante:

Verifica:

* Proyecto oficial.
* Mantenimiento.
* Licencia.
* Seguridad.
* Compatibilidad.
* Necesidad real.

No agregar librerías para resolver trivialidades.

Mantén un inventario de dependencias importantes y licencias.

---

# 44. SEGURIDAD

Aunque inicialmente sea local:

* Validar entradas.
* Usar queries parametrizadas/ORM seguro.
* Hash seguro de contraseñas.
* Principio de mínimo privilegio.
* No guardar secretos innecesarios.
* Proteger operaciones administrativas.
* Auditar operaciones sensibles.
* Evitar exposición de stack traces al usuario.
* Diseñar backups de manera segura.

---

# 45. DATOS INICIALES DE PRUEBA

Cuando lleguemos a seed/demo data, utilizar ejemplos realistas:

Categorías:

PAN DULCE
PAN SALADO
BEBIDAS

Productos:

Concha vainilla
Concha chocolate
Cuernito mantequilla
Dona
Polvorón
Puerquito piloncillo
Rol canela
Bolillo
Cuernito jamón y queso
Café de olla

No asumir que estos datos son definitivos.

---

# 46. PRIMERA PRUEBA REAL DE HARDWARE

Cuando tengamos físicamente la impresora Qian, quiero crear una prueba controlada antes de integrarla completamente al POS.

Objetivo:

Imprimir algo similar a:

CASA MIGA
PANADERÍA TRADICIONAL MEXICANA

PRUEBA CASA MIGA POS

Concha vainilla
Bolillo

TOTAL

PAN DE FAMILIA

Después:

* cortar papel;
* y, cuando esté conectado el cajón compatible, abrirlo.

Esta prueba deberá realizarse mediante nuestra abstracción de hardware siempre que sea razonable.

---

# 47. PRIMER HITO FUNCIONAL

Casa Miga POS v0.1 será exitoso cuando podamos realizar confiablemente:

ABRIR CAJA
↓
SELECCIONAR PRODUCTOS
↓
CREAR VENTA
↓
COBRAR
↓
IMPRIMIR
↓
CORTAR
↓
ABRIR CAJÓN CUANDO CORRESPONDA
↓
REGISTRAR VENTA
↓
CONSULTAR VENTAS
↓
CONTAR EFECTIVO
↓
CERRAR CAJA
↓
GENERAR CORTE

Sin Internet.

---

# 48. IMPORTANTE: NO IMPLEMENTES TODO AHORA

Tu primera tarea NO es construir todo Casa Miga POS.

Haz solamente la FASE 0.

Procedimiento:

1. Inspecciona el entorno actual.
2. Si el directorio está vacío, prepara correctamente el repositorio.
3. Investiga las opciones de stack utilizando principalmente documentación oficial.
4. Compara especialmente .NET moderno frente a Tauri para ESTE proyecto.
5. Verifica compatibilidad con Windows, SQLite, testing, empaquetado e impresión ESC/POS.
6. Recomienda un stack.
7. Documenta la decisión en ADR-001.
8. Diseña la arquitectura inicial.
9. Diseña el modelo inicial de datos.
10. Crea la estructura documental.
11. Prepara la solución/proyecto mínimo.
12. Configura Git correctamente.
13. Configura tests.
14. Compila.
15. Ejecuta los tests.
16. Registra los resultados.
17. Actualiza CURRENT_STATE.md.
18. Actualiza NEXT_STEPS.md.

NO avances automáticamente a construir todo el POS.

Al terminar la FASE 0, detente y entrégame un reporte indicando:

* Qué investigaste.
* Fuentes oficiales utilizadas.
* Stack recomendado y por qué.
* Qué alternativas descartaste y por qué.
* Arquitectura creada.
* Archivos creados.
* Base de datos propuesta.
* Tests ejecutados.
* Resultado de compilación.
* Riesgos encontrados.
* Decisiones que todavía requieren mi autorización.
* Próximo paso recomendado.

No ocultes errores ni los resuelvas mediante hacks.

Si encuentras una decisión importante que pueda comprometer la arquitectura futura, detente antes de asumirla.

El objetivo no es terminar rápido.

El objetivo es construir correctamente **Casa Miga POS**.
