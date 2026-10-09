-- Punto de venta web, fase 2 (docs/POS_WEB.md): devoluciones, cancelaciones y movimientos de efectivo también llevan
-- identificador de operación, para que reintentarlos nunca los duplique.
-- Inmutable una vez aplicada: agregar 006_*.sql para cambios.
ALTER TABLE web_operation DROP CONSTRAINT web_operation_kind_check;
ALTER TABLE web_operation ADD CONSTRAINT web_operation_kind_check CHECK (kind IN ('sale', 'refund', 'movement'));

INSERT INTO schema_version(version) VALUES (5);
