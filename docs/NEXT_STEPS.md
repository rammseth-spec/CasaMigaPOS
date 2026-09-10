# Próximos pasos

Completar únicamente aceptación de FASE 1E; no iniciar otra fase. Actualización 2026-09-10: seguir PHASE_1E_MANUAL_SMOKE.md en work/phase1e-visual-20260910. El diálogo quedó preparado con total $53.00; no hay venta ni checkout.pending. PHASE_1E_VISUAL_ACCEPTANCE.md registra los 24 pasos y sus límites. No repetir preparaciones ni asumir que ya se cobró.

Pendiente: smoke manual de la aplicación publicada. En instalación sintética, crear carrito de $53.00 (2 conchas de $19.00 y 3 bolillos de $5.00); recibido $50.00 debe deshabilitar confirmación; $100.00 debe mostrar $47.00 de cambio. Confirmar una sola vez, comprobar folio/total/recibido/cambio; Volver al POS debe vaciar carrito. Reiniciar y verificar venta conservada. Con fondo cero, contar $53.00 debe producir esperado $53.00 y diferencia cero. Confirmar cierre. Registrar evidencia visual y comparar la base. El recorrido por servicios/SQLite ya pasó, pero no equivale a smoke completo de UI.

El bloqueo observado fue de la herramienta de Windows: activar el destino padre quitaba foco al diálogo y set_value devolvía elemento no disponible. No se alteró el diseño para eludir esa limitación. Reintentar con control funcional o realizar la comprobación manual; no asumir un defecto de binding sin evidencia.

Después, actualizar reporte/estado con resultado real y cerrar 1E. La autorización de 1E ya existe; no pedirla otra vez. Nuevas funcionalidades requieren alcance y autorización posteriores.

Mantener inmutables migraciones 001–006 y catalog_key_v1. Baseline 1D: 92c5ef7. Nunca ejecutar 1D contra una base v6. Recuperar fuentes con git archive en otra carpeta y datos desde .cmbackup compatible.

Antes de producción siguen pendientes administración/recuperación de usuarios, reglas comerciales definitivas, correcciones formales de conteo, retención/segunda copia/cifrado, activación de restauración, pruebas físicas y licencia/distribución. No editar producción para eludir reglas.
