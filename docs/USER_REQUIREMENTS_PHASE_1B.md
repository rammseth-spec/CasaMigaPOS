# CASA MIGA POS — FASE 1B

La FASE 1A ha sido revisada y queda APROBADA.

Continúa únicamente con:

# FASE 1B — CONTEO, CIERRE Y CORTE BÁSICO DE CAJA

NO implementar todavía:

* ventas;
* catálogo;
* carrito;
* pagos reales;
* impresión;
* impresora Qian;
* cajón físico;
* inventario;
* recetas;
* producción;
* compras;
* reportes generales.

El objetivo es completar correctamente el ciclo de vida de una sesión de caja antes de introducir ventas.

---

# 1. BASELINE

Antes de modificar código:

1. Lee:

   * README.md
   * docs/architecture.md
   * docs/CURRENT_STATE.md
   * docs/NEXT_STEPS.md
   * ADRs relevantes
   * documentación de FASE 1A.

2. Inspecciona el estado real del repositorio.

3. Ejecuta:

   * clean;
   * restore;
   * build Release;
   * tests.

4. Confirma que las 30 pruebas existentes continúan pasando.

Si existe una regresión previa, detente y repórtala.

---

# 2. DECISIONES APROBADAS

## 2.1 Conteo ciego

El cierre deberá utilizar CONTEO CIEGO.

El cajero NO debe ver el efectivo esperado antes de capturar el efectivo contado.

Flujo:

CAJA ABIERTA
↓
CERRAR CAJA
↓
CAPTURAR EFECTIVO CONTADO
↓
CONFIRMAR CONTEO
↓
CALCULAR EFECTIVO ESPERADO
↓
MOSTRAR DIFERENCIA
↓
CONFIRMAR CIERRE
↓
CAJA CERRADA

El objetivo es evitar que el usuario modifique el conteo para hacerlo coincidir artificialmente con el sistema.

La UI no debe revelar indirectamente el valor esperado antes del conteo.

---

# 3. QUIÉN PUEDE CERRAR UNA CAJA

Regla inicial:

### Propietario de la sesión

Puede cerrar su propia sesión si posee:

`cash.close`

### Usuario diferente

Sólo podrá cerrar una sesión ajena si posee un permiso adicional:

`cash.close.other`

Inicialmente este permiso podrá asignarse a:

* Administrador.
* Encargado.

No al cajero normal.

Si un usuario cierra la sesión de otro:

* registrar usuario original;
* registrar usuario que ejecuta el cierre;
* generar auditoría explícita;
* registrar que fue un cierre por tercero.

No ocultar esta circunstancia.

---

# 4. MODELO DE CIERRE

La sesión de caja deberá soportar como mínimo:

* opened_by_user_id
* opened_at
* opening_amount
* terminal_id
* status

y al cerrar:

* closed_by_user_id
* closed_at
* counted_amount
* expected_amount
* difference_amount
* closed_by_other_user / mecanismo equivalente derivable
* closing_notes opcional si resulta razonable

No dupliques información si puede derivarse limpiamente.

---

# 5. EFECTIVO ESPERADO

En esta fase todavía NO existen ventas.

Por tanto define formalmente la fórmula base para que pueda crecer posteriormente.

Conceptualmente:

expected_cash =
opening_amount

* cash_inflows

- cash_outflows

* future_cash_sales

- future_cash_refunds

En FASE 1B:

future_cash_sales = 0

future_cash_refunds = 0

Si todavía no existen entradas/retiros de caja, puedes implementar el modelo mínimo necesario o dejar esos componentes en cero, siempre que la arquitectura permita agregarlos posteriormente sin romper el cierre.

NO simules ventas.

Documenta claramente la fórmula.

---

# 6. DIFERENCIA DE CAJA

Calcular:

difference =
counted_amount - expected_amount

Interpretación:

difference = 0
→ caja exacta

difference > 0
→ sobrante

difference < 0
→ faltante

Utiliza el modelo monetario aprobado.

No usar floating point.

---

# 7. REGLAS DE DOMINIO

Implementa y prueba como mínimo:

### C1

Una sesión OPEN puede cerrarse.

### C2

Una sesión CLOSED no puede cerrarse nuevamente.

### C3

El efectivo contado no puede ser negativo.

### C4

Un usuario sin `cash.close` no puede cerrar su propia sesión.

### C5

Un usuario diferente del propietario necesita `cash.close.other`.

### C6

El cierre debe conservar quién abrió y quién cerró.

### C7

El cálculo de efectivo esperado debe realizarse en Application/Domain según corresponda, no exclusivamente en UI.

### C8

La diferencia debe calcularse de manera determinista.

### C9

El cierre debe ser transaccional.

### C10

Sesión + auditoría de cierre deben confirmarse juntas.

### C11

Si falla la auditoría, debe ocurrir rollback completo.

### C12

Una sesión cerrada debe permanecer cerrada tras reiniciar la aplicación.

### C13

No debe existir una transición directa arbitraria de CLOSED a OPEN.

### C14

Una vez cerrada, los valores financieros del cierre no deben poder mutarse mediante operaciones normales.

---

# 8. AUDITORÍA

El cierre normal debe generar una entrada de auditoría.

Debe permitir determinar:

* sesión;
* terminal;
* quién abrió;
* quién cerró;
* cuándo;
* fondo inicial;
* efectivo contado;
* efectivo esperado;
* diferencia.

Si el cierre fue ejecutado por un usuario distinto del propietario, la auditoría debe indicarlo claramente.

No registrar contraseñas ni secretos.

---

# 9. INTERFAZ — FLUJO

Cuando exista una caja abierta:

mostrar:

CASA MIGA

CAJA ABIERTA

Usuario que abrió
Terminal
Hora de apertura
Fondo inicial

[ CERRAR CAJA ]

Al pulsar:

# CONTEO DE CAJA

Efectivo contado:

$ __________

No mostrar todavía:

* efectivo esperado;
* diferencia.

Botones:

[ CANCELAR ]

[ CONTINUAR ]

Después de confirmar el conteo:

# RESULTADO DE CAJA

Fondo inicial: $X

Efectivo esperado: $X

Efectivo contado: $X

Diferencia:

$0.00
o
SOBRANTE $X
o
FALTANTE $X

Después:

[ VOLVER ]

[ CONFIRMAR CIERRE ]

No cerrar definitivamente antes de esta segunda confirmación.

---

# 10. CONFIRMACIÓN

La confirmación final debe dejar claro que:

* la sesión quedará CLOSED;
* el cierre será auditado;
* no podrá volver a abrirse la misma sesión.

Evita diálogos alarmistas innecesarios.

---

# 11. SESIÓN DE OTRO USUARIO

Si el usuario actual intenta cerrar una sesión que pertenece a otro usuario:

1. verifica permiso `cash.close.other`;

2. si no lo posee:

   * bloquear;
   * mostrar mensaje claro;
   * registrar técnicamente sólo lo necesario.

3. si lo posee:

   * permitir;
   * indicar en pantalla que la sesión fue abierta por otra persona;
   * auditar el cierre por tercero.

No implementar mecanismos inseguros de "forzar cierre" al margen del sistema de permisos.

---

# 12. REAPERTURA DE APLICACIÓN

Prueba:

1. abrir caja;
2. cerrar aplicación;
3. abrir;
4. autenticar;
5. recuperar caja abierta;
6. cerrar correctamente;
7. cerrar aplicación;
8. abrir;
9. autenticar;
10. confirmar que ya no existe sesión abierta.

La sesión histórica debe permanecer almacenada como CLOSED.

---

# 13. BACKUP DE CONFIGURACIÓN DE TERMINAL

En FASE 1A se detectó:

`terminal.json` no está cubierto todavía por el backup SQLite.

Corrige este riesgo durante FASE 1B.

La estrategia de respaldo deberá incluir la configuración necesaria para recuperar correctamente la identidad de la terminal.

Evalúa la solución apropiada.

Requisitos:

* no mezclar datos transaccionales arbitrariamente;
* preservar terminal ID;
* restauración comprobable;
* documentar qué archivos forman un backup completo;
* probar restauración.

Si necesitas modificar la utilidad de mantenimiento, hazlo.

---

# 14. RESTAURACIÓN

Agregar una prueba realista donde:

1. exista configuración de terminal;
2. exista base con sesión histórica;
3. se genere backup;
4. se altere/elimine el entorno de prueba;
5. se restaure;
6. se compruebe:

   * terminal correcta;
   * base correcta;
   * estado de sesión correcto.

No considerar solucionado el riesgo de `terminal.json` sólo porque ahora se copie.

Debes comprobar restauración.

---

# 15. ENTRADAS Y RETIROS

NO hace falta construir todavía una UI completa de entradas/retiros.

Sin embargo revisa el diseño para que el cierre futuro pueda incorporar:

* CASH_IN
* CASH_OUT

sin reescribir el aggregate de sesión.

Si existe una implementación sencilla y necesaria para el modelo, puedes preparar la estructura interna, pero no ampliar el alcance innecesariamente.

---

# 16. PRUEBAS AUTOMATIZADAS

Añade como mínimo pruebas para:

1. cierre correcto;
2. cierre con diferencia cero;
3. cierre con sobrante;
4. cierre con faltante;
5. monto contado negativo rechazado;
6. sesión cerrada no puede cerrarse de nuevo;
7. propietario con permiso puede cerrar;
8. propietario sin permiso no puede cerrar;
9. tercero sin `cash.close.other` rechazado;
10. tercero con permiso autorizado;
11. cierre por tercero auditado;
12. timestamps deterministas;
13. persistencia de cierre;
14. recuperación después de reinicio;
15. rollback si falla auditoría;
16. expected cash calculado correctamente;
17. invariantes monetarias;
18. restauración de backup incluyendo terminal;
19. no reapertura directa de sesión cerrada.

Mantén las 30 pruebas existentes.

---

# 17. MIGRACIÓN

Si el esquema necesita cambios:

* crear migración nueva;
* mantener 001 y 002 intactas;
* probar base vacía;
* probar actualización desde el esquema actual;
* documentar restricciones.

---

# 18. UI / TOUCH

Mantén las reglas visuales establecidas en FASE 1A.

Verifica:

* foco con teclado;
* Tab;
* Enter;
* Escape donde corresponda;
* botones grandes;
* contraste;
* estados disabled;
* no dependencia de hover.

La pantalla táctil física sigue pendiente y no bloquea esta etapa.

---

# 19. NO INICIAR VENTAS

Aunque el cierre deje preparada la fórmula futura, NO empieces:

* Product;
* Sale;
* SaleItem;
* Payment;
* POS screen.

FASE 1B termina con caja cerrada correctamente.

---

# 20. VALIDACIÓN FINAL

Al terminar:

1. clean;
2. restore;
3. Release build;
4. todos los tests;
5. comprobar cero errores;
6. revisar warnings;
7. ejecutar aplicación;
8. smoke test completo;
9. revisar persistencia;
10. revisar logs;
11. probar backup/restauración;
12. revisar Git.

---

# 21. SMOKE TEST MANUAL

Como mínimo realiza:

### Caso A — exacto

Abrir con:

$500.25

Cerrar contando:

$500.25

Resultado:

diferencia $0.00

### Caso B — sobrante

Abrir nueva sesión.

Contar una cantidad mayor al esperado.

Verificar SOBRANTE.

### Caso C — faltante

Abrir nueva sesión.

Contar una cantidad menor.

Verificar FALTANTE.

### Caso D — persistencia

Abrir.

Cerrar aplicación.

Reabrir.

Recuperar.

Cerrar.

Reabrir.

Verificar ausencia de sesión OPEN.

---

# 22. DOCUMENTACIÓN

Actualizar:

* CURRENT_STATE.md
* NEXT_STEPS.md
* architecture.md si corresponde
* roadmap.md si corresponde
* documentación de backup
* documentación operativa.

Crear ADR sólo si aparece una decisión arquitectónica realmente significativa.

---

# 23. REPORTE FINAL

Al concluir FASE 1B:

DETENTE.

No inicies FASE 1C.

Entrega:

## Resumen

Qué se implementó.

## Cierre de caja

Flujo y reglas.

## Conteo ciego

Cómo se garantiza que no se muestra el esperado antes del conteo.

## Permisos

Cómo funciona cierre propio y cierre ajeno.

## Base de datos

Migración/restricciones.

## Transacciones

Cómo se garantiza atomicidad.

## Auditoría

Eventos registrados.

## Backup

Cómo se resolvió `terminal.json` y cómo se verificó restauración.

## Tests

Total, aprobados, fallidos y omitidos.

## Build

Errores y warnings.

## Smoke test

Resultados de casos A-D.

## UI

Qué se validó.

## Git

Estado.

## Riesgos

Pendientes reales.

## Decisiones

Cualquier autorización requerida.

## Próximo paso

Proponer FASE 1C.

DETENTE DESPUÉS DEL REPORTE.
