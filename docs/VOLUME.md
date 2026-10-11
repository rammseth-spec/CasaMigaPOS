# Prueba de volumen — 10/10/2026

Un año de operación de una sucursal: **109,500 ventas** (300 al día, 1 a 4 panes por venta, 85 % efectivo, 10 % tarjeta, 5 % transferencia), un turno por día con conteo y cierre y 0.5 % de devoluciones en la nube. Se mide lo que tarda cada pantalla con los mismos servicios que usa la app.

Cómo repetirla:

- Caja (C#): `CASAMIGA_VOLUME=1` (y opcional `CASAMIGA_VOLUME_PER_DAY`, `CASAMIGA_VOLUME_OUT`) y `dotnet test tests/CasaMiga.Tests --filter VolumeTests`. Sin la variable la prueba se omite; con ella falla si una pantalla tarda 3 s o más.
- Web: `cd cloud && node --max-old-space-size=6144 --import tsx scripts/volume.ts 300 365`. Mide por HTTP y lista las consultas de más de 150 ms. Usa PGlite (Postgres compilado a WebAssembly, un solo hilo): es más lento que Neon, sirve para comparar y encontrar consultas que recorren todo.

## Qué se encontró y se corrigió

- **Caja:** con D5, los turnos del reporte sumaban ventas a través de las vistas `report_*` y SQLite recorría todas las ventas por cada turno (Resumen de un año: 100 s). Ahora cada consulta corre por separado sobre las tablas locales y las web y se suma; el turno se calcula con sus índices. **Migración 030:** índices por fecha de venta, de devolución y de apertura de turno.
- **Web:** cada consulta convertía la fecha de todas las ventas y revisaba cancelaciones venta por venta; una CTE usada dos veces se materializaba completa. **Migración de la nube 009:** `cm_utc` (lectura fija de la hora ISO, indexable) e índices por hora de venta, de devolución y de apertura, venta→turno, devolución→venta, movimiento→turno, conteo→turno y folio. Las consultas filtran el periodo como instantes UTC (`inPeriod`, `SALES_IN`, `REFUNDS_IN`, `SESSIONS_IN`).

## Caja de escritorio (SQLite, base de 241 MB)

| Pantalla | Periodo | Antes | Ahora |
|---|---|---|---|
| Reportes · Resumen | hoy | 2062 ms | 24 ms |
| Reportes · Comparativo | hoy | 4016 ms | 30 ms |
| Reportes · Ventas por hora | hoy | 19 ms | 19 ms |
| Reportes · Control de caja | hoy | 2078 ms | 24 ms |
| Reportes · Ganancia | hoy | 2145 ms | 54 ms |
| Historial · por fechas | hoy | 9 ms | 9 ms |
| Historial · tarjeta + producto | hoy | 17 ms | 15 ms |
| Reportes · Resumen | 30 días | 10362 ms | 139 ms |
| Reportes · Comparativo | 30 días | 20005 ms | 302 ms |
| Reportes · Ventas por hora | 30 días | 22 ms | 20 ms |
| Reportes · Control de caja | 30 días | 9902 ms | 147 ms |
| Reportes · Ganancia | 30 días | 9971 ms | 168 ms |
| Historial · por fechas | 30 días | 31 ms | 34 ms |
| Historial · tarjeta + producto | 30 días | 115 ms | 115 ms |
| Reportes · Resumen | 92 días | 26690 ms | 427 ms |
| Reportes · Comparativo | 92 días | 53622 ms | 868 ms |
| Reportes · Ventas por hora | 92 días | 54 ms | 45 ms |
| Reportes · Control de caja | 92 días | 26754 ms | 409 ms |
| Reportes · Ganancia | 92 días | 26906 ms | 432 ms |
| Historial · por fechas | 92 días | 87 ms | 86 ms |
| Historial · tarjeta + producto | 92 días | 230 ms | 224 ms |
| Reportes · Resumen | 365 días | 100766 ms | 1636 ms |
| Reportes · Comparativo | 365 días | 102090 ms | 1704 ms |
| Reportes · Ventas por hora | 365 días | 156 ms | 111 ms |
| Reportes · Control de caja | 365 días | 102030 ms | 1617 ms |
| Reportes · Ganancia | 365 días | 102232 ms | 1650 ms |
| Historial · por fechas | 365 días | 321 ms | 318 ms |
| Historial · tarjeta + producto | 365 días | 597 ms | 584 ms |
| Historial · Todos | — | 41 ms | 39 ms |
| Historial · última página | — | 421 ms | 418 ms |
| Producción · pronóstico | 12 semanas | 2268 ms | 2254 ms |

## Web (PGlite en memoria; en Neon debe ser más rápido)

| Pantalla | Periodo | Antes | Ahora |
|---|---|---|---|
| Reportes · Resumen | hoy | 3111 ms | 78 ms |
| Reportes · Resumen | 30 días | 17760 ms | 1889 ms |
| Reportes · Resumen | 92 días (máximo) | 50816 ms | 8718 ms |
| Reportes · Comparativo | hoy | 1220 ms | 25 ms |
| Reportes · Comparativo | 30 días | 1283 ms | 388 ms |
| Reportes · Comparativo | 92 días (máximo) | 1455 ms | 1168 ms |
| Reportes · Ventas por hora | hoy | 138 ms | 14 ms |
| Reportes · Ventas por hora | 30 días | 176 ms | 158 ms |
| Reportes · Ventas por hora | 92 días (máximo) | 265 ms | 476 ms |
| Reportes · Control de caja | hoy | 7 ms | 6 ms |
| Reportes · Control de caja | 30 días | 6 ms | 6 ms |
| Reportes · Control de caja | 92 días (máximo) | 7 ms | 7 ms |
| Reportes · Ganancia | hoy | 141 ms | 17 ms |
| Reportes · Ganancia | 30 días | 327 ms | 264 ms |
| Reportes · Ganancia | 92 días (máximo) | 718 ms | 775 ms |
| Inicio (tablero) | hoy | 829 ms | 22 ms |
| Inicio (tablero) | 30 días | 3627 ms | 443 ms |
| Inicio (tablero) | 92 días (máximo) | 9417 ms | 2757 ms |
| Ventas (historial, 1.ª página) | hoy | 163 ms | 33 ms |
| Ventas (historial, 1.ª página) | 30 días | 916 ms | 793 ms |
| Ventas (historial, 1.ª página) | 92 días (máximo) | 2557 ms | 2357 ms |
| Catálogo | — | 5 ms | 5 ms |
| Usuarios | — | 3 ms | 4 ms |
| Producción · pronóstico | — | 23788 ms | 78 ms |

Lo que sigue tardando en la web crece con el tamaño del periodo (92 días son unas 27 mil ventas leídas dos veces: el periodo y el anterior para comparar). El periodo máximo de la web es 92 días; el de la caja, 366.
