# CASA MIGA POS — CONTINUACIÓN DEL PROYECTO

La FASE 0 ha sido revisada y queda APROBADA.

Puedes continuar con la siguiente etapa, pero NO desarrolles todavía toda la FASE 1 de una sola vez.

Trabajaremos FASE 1 por incrementos pequeños, verificables y reversibles.

La siguiente etapa será:

# FASE 1A — IDENTIDAD, PERMISOS MÍNIMOS Y APERTURA DE CAJA

Antes de modificar código:

1. Lee:

   * README.md
   * docs/architecture.md
   * docs/CURRENT_STATE.md
   * docs/NEXT_STEPS.md
   * ADRs existentes
   * modelo de dominio/base de datos relevante.

2. Revisa el estado real del repositorio y no asumas que la conversación representa el estado actual.

3. Ejecuta baseline:

   * restore;
   * build Release;
   * tests.

4. Si existe cualquier regresión previa, detente y repórtala antes de implementar nuevas funciones.

---

# OBJETIVO DE ESTA ETAPA

Al finalizar FASE 1A debe ser posible ejecutar Casa Miga POS y completar confiablemente este flujo:

INICIAR APLICACIÓN
↓
IDENTIFICAR USUARIO
↓
VERIFICAR PERMISOS
↓
ABRIR CAJA
↓
CAPTURAR FONDO INICIAL
↓
CREAR SESIÓN DE CAJA
↓
MOSTRAR ESTADO "CAJA ABIERTA"
↓
CERRAR APLICACIÓN
↓
VOLVER A ABRIR
↓
RECUPERAR CORRECTAMENTE LA SESIÓN ABIERTA

Todavía NO implementar ventas.

Todavía NO implementar impresión.

Todavía NO implementar cajón físico.

Todavía NO implementar inventario.

---

# 1. USUARIOS

Implementa solamente lo necesario para soportar correctamente autenticación local y propiedad/auditoría de sesiones de caja.

Debe existir inicialmente soporte para:

* Administrador.
* Encargado.
* Cajero.

No necesitas construir todavía una pantalla completa de administración de usuarios.

Puedes utilizar seed/bootstrap controlado para crear el primer usuario administrador cuando la base se inicialice.

Nunca almacenar contraseñas en texto plano.

Utiliza un algoritmo moderno, mantenido y apropiado para almacenamiento de contraseñas.

Antes de elegir librería o algoritmo:

* revisa documentación oficial;
* revisa mantenimiento;
* revisa licencia;
* documenta la decisión si constituye una decisión arquitectónica relevante.

No inventes criptografía propia.

---

# 2. PERMISOS

Implementa el mecanismo mínimo de permisos sin sobrearquitecturar.

Permisos iniciales relevantes:

* cash.open
* cash.close
* cash.view
* cash.manual_open_future

El último permiso se define pensando en la futura apertura manual del cajón, pero NO debe controlar hardware todavía.

La autorización debe ocurrir en Application/Domain según corresponda.

La interfaz NO debe ser la única barrera de seguridad.

Ocultar/desactivar botones en UI está bien como experiencia de usuario, pero la operación debe validar el permiso internamente.

---

# 3. TERMINAL

Introduce el concepto de TERMINAL de manera ligera.

Aunque actualmente existirá una sola caja, una sesión de caja debe poder quedar asociada a:

* usuario;
* terminal.

No implementar todavía red ni multiterminal.

Puedes tener una terminal local configurada inicialmente como:

CASA-MIGA-CAJA-01

pero evita hardcodear dependencias innecesarias.

La identidad de terminal debe poder configurarse posteriormente.

---

# 4. SESIÓN DE CAJA

Implementar la entidad/aggregate apropiado para representar una sesión.

Debe incluir como mínimo:

* ID.
* Terminal.
* Usuario que abrió.
* Fecha/hora de apertura.
* Fondo inicial.
* Estado.
* Fecha/hora de cierre opcional.
* Usuario que cerró cuando corresponda.
* Efectivo esperado futuro.
* Efectivo contado futuro.
* Diferencia futura.

No es obligatorio exponer todavía todos los campos de cierre en UI si corresponden a FASE 1B.

---

# 5. REGLAS DEL DOMINIO

Implementar y probar al menos estas reglas:

### R1

No puede abrirse una caja con fondo inicial negativo.

### R2

No puede existir simultáneamente más de una sesión OPEN para la misma terminal.

### R3

Un usuario sin permiso `cash.open` no puede abrir caja.

### R4

Una sesión CLOSED no puede volver a abrirse modificando su estado directamente.

### R5

Abrir una caja debe ser una operación transaccional.

### R6

Una sesión abierta debe sobrevivir al reinicio de la aplicación.

### R7

La fecha/hora debe provenir de una abstracción testeable y no estar dispersa mediante llamadas directas al reloj del sistema dentro del dominio.

### R8

Las cantidades monetarias deben respetar el modelo definido en ADR-003-money.md.

### R9

La apertura debe generar auditoría apropiada.

---

# 6. RELOJ / TIEMPO

Si todavía no existe, implementa una abstracción ligera para tiempo.

Ejemplo conceptual:

IClock

SystemClock

FakeClock/TestClock

No quiero dependencias innecesarias sólo para esto.

La finalidad es:

* poder probar timestamps;
* evitar tests frágiles;
* permitir auditoría determinista.

---

# 7. TRANSACCIÓN DE APERTURA

La operación lógica debe parecerse conceptualmente a:

OpenCashSessionCommand

↓

VALIDAR USUARIO/PERMISO

↓

VALIDAR TERMINAL

↓

COMPROBAR QUE NO EXISTE OTRA SESIÓN ABIERTA

↓

VALIDAR FONDO INICIAL

↓

CREAR CASH SESSION

↓

CREAR AUDIT ENTRY

↓

COMMIT

Cualquier fallo crítico debe provocar rollback.

No dejar registros parciales.

---

# 8. CONCURRENCIA

Aunque SQLite y una sola terminal hagan improbable una carrera ahora, analiza correctamente la regla:

"solamente una sesión OPEN por terminal".

No confíes únicamente en:

SELECT
↓
if none
↓
INSERT

si existe posibilidad de violar la regla mediante concurrencia.

Cuando SQLite/ORM permita una protección a nivel de persistencia adecuada, úsala.

Documenta cómo se garantiza la invariante.

No construyas todavía infraestructura distribuida.

---

# 9. INTERFAZ

Construye solamente la interfaz necesaria para FASE 1A.

La aplicación debe comenzar a mostrar la identidad visual y filosofía de Casa Miga.

Queremos:

* WPF limpio.
* Moderno.
* Cálido.
* Profesional.
* Touch-friendly.
* Botones suficientemente grandes.
* Tipografía legible.
* Buena separación visual.
* Sin apariencia de ERP antiguo.

No dediques todavía demasiado tiempo a animaciones o decoración.

---

# 10. PANTALLA INICIAL

Diseña un flujo sencillo.

Si NO existe sesión abierta:

CASA MIGA

Usuario autenticado

CAJA CERRADA

[ ABRIR CAJA ]

Al pulsar:

ABRIR CAJA

Fondo inicial:
$ ________

[ CANCELAR ] [ ABRIR CAJA ]

Después de confirmar:

CAJA ABIERTA

Usuario
Terminal
Hora apertura
Fondo inicial

Y una indicación clara de que el sistema está listo para operar.

Todavía no mostrar catálogo ni venta.

---

# 11. LOGIN

Implementa un login local mínimo pero correcto.

No necesitamos todavía:

* recuperación por email;
* cloud identity;
* OAuth;
* Microsoft Account.

Debe ser adecuado para una aplicación local de panadería.

La UI debe evitar revelar detalles innecesarios cuando una autenticación falle.

---

# 12. EXPERIENCIA TÁCTIL

Durante esta etapa comienza a establecer reglas reutilizables de UI:

* targets táctiles suficientemente grandes;
* espaciado consistente;
* estados disabled claros;
* foco visible;
* soporte razonable para teclado;
* no depender exclusivamente del hover;
* controles importantes fáciles de pulsar.

Documenta los tokens/recursos visuales reutilizables si resulta apropiado.

---

# 13. IDENTIDAD CASA MIGA

No inventes ni redibujes el logotipo.

Si todavía no existe un asset oficial dentro del proyecto:

crea un placeholder claramente identificado como temporal.

Prepara la estructura para que posteriormente podamos colocar los assets oficiales sin reescribir la UI.

La paleta visual debe ser configurable mediante recursos centralizados y no mediante colores hardcodeados en cada control.

---

# 14. DATOS DE DESARROLLO

Puedes proporcionar un mecanismo seguro de DEVELOPMENT SEED para poder probar el sistema.

Debe distinguirse claramente de datos de producción.

No introducir contraseñas reales ni secretos en Git.

Documenta cómo crear/inicializar el primer administrador.

---

# 15. PRUEBAS AUTOMATIZADAS

Agrega pruebas para los casos importantes.

Como mínimo:

1. Apertura válida.
2. Fondo inicial cero permitido.
3. Fondo negativo rechazado.
4. Usuario sin permiso rechazado.
5. Segunda sesión abierta en misma terminal rechazada.
6. Terminal diferente tratada según el diseño.
7. Persistencia de sesión.
8. Recuperación tras reinicio lógico.
9. Creación de auditoría.
10. Rollback ante error simulado cuando sea razonable.
11. Hash/verificación de contraseña.
12. Autenticación fallida.
13. Timestamp determinista mediante reloj de prueba.
14. Restricción de persistencia para sesión única OPEN cuando corresponda.

No escribas tests que sólo comprueben implementaciones triviales.

Prioriza reglas de negocio y persistencia.

---

# 16. MIGRACIONES

Si debes modificar el esquema:

* crea nueva migración;
* no edites silenciosamente una migración ya aplicada salvo que el proyecto siga inequívocamente en una etapa donde eso esté documentado como permitido;
* documenta cualquier cambio.

Prueba migración desde base vacía.

Cuando sea viable, prueba también actualización desde la versión anterior.

---

# 17. LOGGING

Registrar eventos técnicos relevantes como:

* inicialización;
* errores de base;
* fallo de migración;
* apertura de sesión;
* errores inesperados.

No registrar:

* contraseña;
* hash completo;
* información sensible innecesaria.

La auditoría de negocio y el logging técnico son conceptos diferentes.

Mantenerlos separados.

---

# 18. UI Y DOMINIO

Regla:

WPF NO debe contener reglas centrales como:

if openingAmount < 0

como única validación.

La UI puede realizar validaciones inmediatas para UX, pero Application/Domain debe proteger las invariantes.

---

# 19. NO IMPLEMENTAR AÚN

En esta iteración NO hagas:

* catálogo POS;
* productos;
* carrito;
* ventas;
* pagos;
* ticket;
* Qian;
* ESC/POS;
* cajón físico;
* inventario;
* recetas;
* producción;
* proveedores;
* compras;
* reportes completos;
* cierre completo de caja si excede el alcance definido;
* instalador definitivo.

Mantener el alcance controlado.

---

# 20. VALIDACIÓN FINAL

Al terminar:

1. Clean.
2. Restore.
3. Build Release.
4. Ejecuta todas las pruebas.
5. Verifica warnings.
6. Ejecuta la aplicación.
7. Realiza smoke test del flujo.
8. Verifica persistencia cerrando y abriendo la aplicación.
9. Revisa logs.
10. Revisa cambios Git.

No ignores warnings nuevos importantes.

---

# 21. ACTUALIZAR DOCUMENTACIÓN

Actualiza:

docs/CURRENT_STATE.md

docs/NEXT_STEPS.md

docs/roadmap.md si corresponde.

ADRs solamente si surgió una verdadera decisión arquitectónica.

No crear ADRs innecesarios para detalles triviales.

---

# 22. REPORTE AL FINAL

Al concluir FASE 1A, detente.

NO continúes automáticamente a ventas.

Entrégame:

## Resumen

Qué se implementó.

## Arquitectura

Componentes creados/modificados.

## Base de datos

Migraciones y restricciones nuevas.

## Seguridad

Cómo se almacenan/verifican contraseñas y cómo funcionan permisos.

## Caja

Cómo se garantiza una sola sesión OPEN por terminal.

## Transacciones

Qué operaciones están protegidas.

## UI

Pantallas creadas y estado de la validación táctil.

## Tests

Número total y resultados.

## Build

Errores y warnings.

## Smoke test

Flujo probado manualmente.

## Git

Archivos principales modificados.

## Riesgos

Problemas o deuda detectada.

## Decisiones

Cualquier decisión que necesite mi autorización.

## Próximo paso

Recomienda el alcance concreto de FASE 1B.

DETENTE DESPUÉS DE ESE REPORTE.
