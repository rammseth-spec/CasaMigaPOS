# FASE 1E — ejecución autorizada

## Punto de recuperación 1D

Baseline definitivo: `92c5ef795b6da8e1cdf7f34ec9fb1b01cce4a5d2`.
Sin cambios funcionales respecto de la entrega local aprobada. Antes del commit se ejecutó verify.ps1: 120 aprobadas, 0 fallidas/omitidas, 0 errores/warnings de compilación. Se volvió a ejecutar desde una exportación del baseline definitivo con el mismo resultado. Las migraciones conservan todos sus bytes originales; .gitattributes excluye su normalización de texto.

Identidad configurada sólo localmente: Rammseth, `rammstudioworkshop\@gmail.com`, exactamente como solicitó el propietario. No se configuró remoto.

Para inspeccionar sin alterar trabajo actual: `git show 92c5ef7:docs/PHASE_1D_REPORT.md`. Para recuperar fuentes en otra carpeta: `git archive --format=zip --output=work/phase1d-baseline.zip 92c5ef7`. El commit no contiene bases ni backups; usar el paquete .cmbackup correspondiente para datos. Nunca abrir una base migrada a 1E con el ejecutable 1D ni hacer reset destructivo del trabajo actual.

## Orden y alcance

1E.1 Domain e invariantes; 1E.2 migración 006/persistencia; 1E.3 checkout, idempotencia y recuperación; 1E.4 ExpectedCash; 1E.5 UI mínima/smoke; cierre con suite, publicación, restauración y reporte.

Venta sólo al confirmar; Sale/SaleItem/Payment inmutables; efectivo únicamente; Money Int64 checked. Nombre/precio del carrito se conservan, con producto/categoría activos al cobrar. Venta/partidas/pago/auditoría en una transacción; rollback conserva carrito y commit confirmado permite vaciarlo. Sin stock, descuentos, promociones, combos, devoluciones, cancelaciones, tarjetas, fiscalidad, clientes, hardware, rediseño o fullscreen.

Estado vigente: 1E.1–1E.4 verificadas, 1E.5 implementada con smoke WPF parcial. Suite final de 153 aprobadas; publicación y backup/restore verificados. Falta completar aceptación visual de cobro; no declarar 1E completamente cerrada. Ver PHASE_1E_REPORT.md.

Actualización de recuperación: el commit raíz acb3604 y el ajuste .gitattributes 5b98863 preceden al baseline definitivo 92c5ef7, que conserva los finales mixtos originales de migraciones 003/004 sin normalización. Se exportó el commit definitivo y se ejecutó verify.ps1 desde la copia: 120 aprobadas, 0 errores/warnings de compilación. Las cinco migraciones exportadas coinciden por SHA-256 con las aplicadas. El baseline sigue sin funcionalidad de 1E.

1E.1: 7 pruebas de dominio aprobadas. 1E.2: 6 pruebas iniciales de persistencia aprobadas. 1E.3: 9 pruebas iniciales de checkout aprobadas. 1E.4: 5 pruebas de cierre/backup aprobadas. Checkpoint antes de UI: 147 aprobadas. Después se reforzó el marcador durable hasta aceptación del resultado y se agregaron pruebas de reinicio real de UserSession, concurrencia, cero y upgrade desde v5. UI mínima en preparación para smoke. Estos resultados no sustituyen la validación final.
