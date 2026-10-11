# Piloto en sucursal — lista de revisión

Una o dos semanas de uso real en una sucursal antes de dar la versión 1.0 por buena. En el piloto **no se agregan funciones**: se anotan problemas y se corrigen. Manual del personal: [MANUAL.md](MANUAL.md).

## Antes del primer día (administrador)

- [ ] Nube desplegada en Vercel con Neon y Blob; variables `DATABASE_URL`, `SESSION_SECRET`, `ENROLLMENT_SECRET`, `BACKUP_SECRET` y `CRON_SECRET` capturadas por el administrador (nunca en el repositorio). `BACKUP_SECRET` guardado también fuera de Vercel.
- [ ] `https://<sitio>/api/health` responde y la web abre con el usuario administrador.
- [ ] Caja instalada con el instalador (`CasaMigaPOS-<versión>-setup.exe`) y su SHA-256 anotado.
- [ ] Caja registrada en **Nube** con el secreto de alta; estado «Nube: al día».
- [ ] **Respaldar ahora** en Nube y el respaldo aparece en la web (Nube → Respaldos).
- [ ] Usuarios reales creados (cada quien con su usuario; nadie comparte contraseña). Contraseñas temporales entregadas en persona.
- [ ] Catálogo revisado: precios, categorías, fotos y precios de charola.
- [ ] Impresora de tickets probada con un ticket de prueba (sin abrir el cajón si no está autorizado).
- [ ] Si se usará la web para vender: una caja web por dispositivo creada y probada con una venta.
- [ ] Plan B impreso: si la computadora de la caja falla, se vende desde la web (caja web) y luego se restaura la caja (MANUAL.md, sección 4).

## Cada día

- [ ] Apertura con fondo; ventas normales en efectivo, tarjeta y transferencia.
- [ ] Al menos un gasto o entrada registrado cuando aplique.
- [ ] Conteo ciego y cierre por el encargado; nota en cada diferencia.
- [ ] «Nube: al día» al cerrar (si no, anotar la hora y el mensaje).
- [ ] Revisar en la web que el Resumen del día coincide con el cierre de la caja (ventas, efectivo, devoluciones).
- [ ] Charolas: entregas y recogidas del día capturadas; saldo de cada tienda razonable.

## Cada semana

- [ ] Reportes → Comparativo y Ganancia de la semana: cifras que tengan sentido para el encargado.
- [ ] Nube → Respaldos: hay respaldos de todos los días.
- [ ] Nube → Cajas y cambios: ningún cambio «Rechazado» sin atender.
- [ ] Producción sugerida contra lo que realmente se horneó.

## Qué anotar de cada problema

Fecha y hora · quién · en qué pantalla (caja o web) · qué se intentó · qué pasó · folio o captura de pantalla. Un problema por renglón. Con eso se corrige sin adivinar.

## Criterio para cerrar el piloto

- Dos semanas sin diferencias de caja inexplicables por el sistema.
- Ningún problema abierto que impida vender, cobrar o cerrar.
- Respaldos diarios presentes y una restauración de prueba hecha.
- El encargado puede contestar con los reportes: cuánto se vendió, cuánto queda en tiendas y cuánto se ganó.
