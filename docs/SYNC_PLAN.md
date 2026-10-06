# Plan de sincronización en línea (offline-first)

Estado: **fases 1–3 implementadas; despliegue de la API pendiente de permisos en Vercel**. Fecha: 06/10/2026.

| Fase | Estado |
|---|---|
| 1 | Hecha (migraciones 018–019). |
| 2 | Base central en Neon `casamiga-pos` (`shiny-mouse-28561448`, aws-us-east-1) con `cloud/db/001_init.sql` aplicada. API en `cloud/` (TypeScript, Vercel Functions) probada localmente; **sin desplegar**: el conector de Vercel no tiene permiso para crear proyectos en el equipo. |
| 3 | Hecha en la app: `SyncEngine` + `HttpSyncTransport`, envío en segundo plano cada 15 s con espera creciente sin conexión (hasta 5 min), llave por terminal cifrada con DPAPI en `sync.json`, pantalla **Nube** (administrador) y estado en el encabezado. |
| 4–6 | Pendientes. |

## Objetivo

Cada computadora Windows vende siempre contra su base SQLite local, con o sin Internet. Cuando hay conexión, la información pendiente se sube automáticamente a una base central en línea, y desde ahí puede consultarse desde cualquier lugar.

## Decisiones del usuario (06/10/2026)

- Vender sin Internet es obligatorio; la sincronización ocurre después, en automático.
- Varias sucursales.
- Un solo administrador para todas las sucursales.
- Cada cajero pertenece a **una** sucursal.
- Los precios se definen **por sucursal**; pueden ser iguales o distintos.
- Los roles actuales (Administrator, Manager, Cashier) se conservan por ahora.
- Proveedor de nube: **Neon (PostgreSQL) + Vercel (API en TypeScript)**, decidido 06/10/2026.

## Principios

1. **La venta nunca depende de la red.** La base local sigue siendo la fuente de verdad de cada terminal.
2. **Cola en la misma transacción.** Cada cambio sincronizable se anota en `sync_outbox` mediante triggers SQL, dentro de la misma transacción que lo produce. Si la operación se revierte, la anotación también.
3. **Reenvío seguro.** Todo se identifica por UUID; la nube aplica cada registro de forma idempotente, así que reenviar después de un corte no duplica.
4. **Sin secretos de base de datos en las computadoras.** Las terminales hablan con una API propia (TypeScript en Vercel) usando una llave por terminal; nunca con la base central directamente.
5. **Una base local = una sucursal.** El precio local de cada producto es el precio de esa sucursal. La tabla de precios por sucursal existe sólo en la nube y cada sucursal descarga los suyos.

## Fases

| Fase | Alcance | Requiere nube |
|---|---|---|
| 1 | Migración 018: identidad de sucursal local (`branch`, una fila) y `sync_outbox` con triggers para venta, caja, conteo y auditoría; respaldo inicial de historial. Lectura de pendientes y marcado como enviados. | No |
| 2 | API y base central (PostgreSQL). Registro de sucursales y terminales con llave por terminal. | Sí |
| 3 | Envío automático en segundo plano: lotes, reintentos con espera creciente, indicador «Sincronizado / N pendientes». Pruebas de corte de red, duplicados y cierre de la app a mitad de envío. | Sí |
| 4 | Descarga: catálogo, precios por sucursal y usuarios (cajeros de la sucursal + administrador único). | Sí |
| 5 | Panel web de consulta multi-sucursal. | Sí |
| 6 | Inventario y producción (requiere política de conflictos entre sucursales). | Sí |

## Riesgos abiertos

- El contenido (`payload`) se lee al **enviar**, no al encolar: un evento `insert` de `cash_session` puede llegar ya con el estado cerrado. Cada cambio posterior tiene su propio `seq`, así que el estado final converge; la fase 5 debe proyectar por el `seq` más alto de cada registro.
- Intentos fallidos de «Cambiar contraseña» y solicitudes de «¿Olvidaste tu contraseña?» se auditan sin límite y se sincronizan; requiere acceso físico a la caja. Evaluar límite por usuario si aparece abuso.

- Primer arranque de una segunda sucursal: debe descargar el administrador existente en vez de crear otro (fase 4).
- Relojes de las computadoras: la nube debe registrar su propia hora de recepción además de la hora local.
- Restaurar un respaldo antiguo puede reenviar registros ya enviados; la idempotencia lo cubre.
