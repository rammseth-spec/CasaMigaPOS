# Fixture aislada de ticket Qian 1K.4E

Herramienta de desarrollo autorizada exclusivamente para preparar la revisión de 1K.4E. Recibe el directorio protegido y un destino nuevo. Exige el hash autorizado, exclusividad del lock y ausencia de WAL pendiente; después copia la base, terminal y fotografías a la nueva ubicación. Nunca escribe en el origen.

La contraseña se recibe sólo mediante `CASAMIGA_FIXTURE_PASSWORD`. La herramienta valida `catalog-admin`; no crea usuarios, no restablece credenciales y no persiste la contraseña.

La copia se migra a schema 17 y conserva una sesión de caja abierta. Configura el teléfono oficial de pedidos `461 276 7429`. Inserta una venta sintética de $72.00 con recibido de $100.00 y cambio de $28.00 mediante el agregado existente; captura el snapshot inmutable. Los tres productos usan identidades existentes, pero nombres y precios sintéticos guardados en las partidas históricas. No ejecuta checkout, consumo de inventario, transporte RAW ni servicio de impresión.

Genera dos payloads fuera de línea, `ticket-original-NO-ENVIADO.bin` y `ticket-reimpresion-NO-ENVIADO.bin`, sus equivalentes de texto y `ticket-fixture-manifest.json`. El manifiesto registra hashes, conteos ESC/POS, schema y ausencia de mutaciones en inventario, producción y solicitudes/eventos de impresión. Ningún archivo se envía a la cola `POS-80`; la impresión física requiere intervención humana desde la aplicación.
