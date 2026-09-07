# ADR-010 — Commit de efectivo y recuperación del resultado

Estado: decisión autorizada para FASE 1E, implementada. Fecha: 2026-09-07.

El carrito transitorio no representa una venta pendiente persistida. La confirmación de efectivo crea un agregado inmutable Sale/SaleItem/Payment y auditoría en una sola transacción SQLite inmediata. Una venta sólo aparece cuando el commit tiene éxito. Folio por terminal/secuencia y OperationId únicos. Cero válido; Money Int64 checked y MXN, sin float/double.

Conservar nombre/precio capturados en carrito; revalidar producto/categoría activos al confirmar. Recibido >= total, aplicado=total, cambio=recibido−total. ExpectedCash suma aplicado a fondo. Conteo confirmado impide ventas posteriores. Revalidar usuario activo, catalog.view, pos.use y sales.complete.

Un fallo al devolver el resultado puede ocurrir después del commit. Por ello, guardar antes un UUID durable en checkout.pending, sin partidas/credenciales. Ante excepción consultar por UUID desde una unidad independiente. Encontrado: devolver venta original, no repetir inserts ni efectivo. Ausente: liberar intento, conservar carrito. Sin lectura fiable: bloquear para recuperación.

El marcador se conserva hasta que el operador acepta el resultado mostrado. Esto cubre reinicio después de commit y antes de mostrar folio. Reinicio no reconstruye carrito; sólo recupera el resultado financiero. Un retry de contenido distinto se rechaza y no elimina un marcador anterior. Un resultado antiguo no borra un carrito nuevo.

Inmutabilidad reforzada con triggers y restricciones SQL; hijos con FK diferidas se insertan antes del encabezado, que valida integridad del agregado. Auditoría falla → rollback completo, incluida secuencia. No se promete resistencia a administradores que alteren el esquema ni a pérdida física del almacenamiento.

Consecuencias: mientras un resultado está pendiente no se permiten modificaciones POS ni conteo/cierre. Recuperar exige el actor original autorizado. La recuperación administrativa de cuentas queda pendiente de otro alcance. El backup conserva la venta en SQLite, pero no el marcador; restaurar es un procedimiento distinto de resolver un intento. Conciliar cobros posteriores al backup antes de reanudar operación. No se implementan cancelaciones/devoluciones ni estado financiero editable.
