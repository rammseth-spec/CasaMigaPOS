# Evidencia — FASE 1K.4F, anexo de alertas e iconografía

Fecha: 09/10/2026.

## Resultado automatizado final

- Focales `FullyQualifiedName~OperationCenter`: **13/13**, 0 fallidas, 0 omitidas.
- Suite general: **576/576**.
- Suite WPF/Presentation: **200/200**.
- Total final: **776/776**, 0 fallidas, 0 omitidas.
- `scripts/verify.ps1`: Release correcto, 0 errores y 0 advertencias.
- Release manual identificado: `CasaMigaReviewBuild=1K.4F`, `bin/Phase1K4F`, 0 errores y 0 advertencias.

La primera ejecución completa encontró una aserción de conteo textual de `Stretch="Uniform"` en el shell. La familia nueva no alteraba los logos, pero el icono del header sumó una coincidencia. Se corrigió el icono cuadrado a `Stretch="Fill"`, la prueba específica aprobó y luego se repitió toda la suite con el resultado final anterior.

## Alertas

- Estado permanente posterior al aviso: `¡Listo!` y tarjeta destacada hasta Atender/Posponer.
- Efecto: doble pulso + microdesplazamiento máximo 3 DIP, 600 ms, sin bloqueo y sin animación continua.
- Sonido: `SystemSounds.Asterisk`, una vez por tick con uno o varios vencimientos, sin loop.
- Segundo plano: `FlashWindowEx` con `FLASHW_TRAY`, tres destellos de taskbar, sin activar/restaurar/robar foco.
- Se verificó minimizado + alerta, F11 + alerta, agrupación de tres vencimientos en una alerta y desuscripción al cerrar.

## Iconografía, prioridad y posponer

- Familia propia de `Geometry`/`Path`, línea limpia monocromática, sin emoji ni dependencia externa.
- Timers: 12 categorías; notas: ocho categorías; selección persistida en JSON v1 de forma retrocompatible.
- Prioridad: únicamente Normal/Urgente.
- Posponer: exclusivamente 5, 10 y 15 minutos, verificado en las tres rutas.
- Header: usa conteos acotados y nunca nombres de tareas; prioridad urgente antecede a listos/activos.

## Integridad

- Base protegida `work/phase1k2c-r2-smoke-20260928/casamiga.db`: SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
- Schema protegido: 16; fixture aislada: schema 17 existente.
- Migraciones presentes hasta 017; no existe 018.
- `Domain`, `Application`, `Infrastructure`, SQLite, impresión 1K.4E-R1, Qian, cajón y datos comerciales sin cambios.
- No se envió ningún trabajo a la impresora y no se ejecutaron operaciones comerciales.
