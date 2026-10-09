# Fixture aislada de Caja 1K.4D

Herramienta de desarrollo autorizada para esta entrega. No usar sobre instalaciones comerciales.

Recibe un origen protegido y un destino que NO exista. Exige el SHA-256 autorizado del origen, obtiene exclusividad sobre su lock existente, rechaza WAL pendiente y copia la base, terminal, configuración de impresión y fotos. La copia se migra a schema 17; el origen permanece byte-idéntico en schema 16.

La credencial de revisión se recibe exclusivamente por la variable de proceso `CASAMIGA_FIXTURE_PASSWORD`. No se incluye en código, manifiestos o documentación versionada. La herramienta valida esa credencial en la copia, no la crea ni la restablece.

Prepara 28 sesiones sintéticas con fondo $500 y un pago CASH aplicado de $1,250 por sesión. Alterna conteo $1,750, $1,780 y $1,700. Usa las reglas reales de conteo y cierre, con nota explícita en sobrantes/faltantes. Si la copia ya tiene una sesión abierta, la cierra mediante los servicios existentes para comenzar el smoke sin sesión abierta. No elimina históricos.

Las ventas sintéticas se insertan mediante el contrato de agregado existente, exclusivamente para esta fixture. No usa checkout, transporte de impresión ni hardware. Verifica que inventario, producción y registros de impresión no cambien. Genera `cash-fixture-manifest.json` con hashes, schema, escenarios y conteos antes/después.

Ejecutar sólo con los argumentos documentados en `docs/PHASE_1K4D_REPORT.md`, después de comprobar que Casa Miga no usa esos directorios. La herramienta falla si el destino ya existe; nunca reinicia silenciosamente una revisión manual.
