-- Rendimiento con un año de ventas (docs/VOLUME.md): los reportes, el tablero, el historial y el pronóstico leían y convertían la fecha
-- de TODAS las ventas en cada consulta. cm_utc lee una hora ISO 8601 con zona (como las escriben la caja y la web) de forma fija,
-- así puede indexarse; lo que no tenga ese formato da NULL en vez de error. Las consultas filtran el periodo como instantes UTC.
-- Inmutable una vez aplicada: agregar 010_*.sql para cambios.
CREATE FUNCTION cm_utc(value text) RETURNS timestamptz LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE AS $$
BEGIN
  IF value ~ '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,9})?(Z|[+-]\d{2}:\d{2})$' THEN RETURN value::timestamptz; END IF;
  RETURN NULL;
END $$;

CREATE INDEX ix_es_sale_at ON entity_state (cm_utc(payload->'sale'->>'confirmed_utc')) WHERE entity = 'sale' AND NOT deleted;
CREATE INDEX ix_es_sale_session ON entity_state ((payload->'sale'->>'cash_session_id')) WHERE entity = 'sale' AND NOT deleted;
CREATE INDEX ix_es_sale_folio ON entity_state (terminal_id, ((payload->'sale'->>'sequence')::bigint)) WHERE entity = 'sale' AND NOT deleted;
CREATE INDEX ix_es_refund_at ON entity_state (cm_utc(payload->'refund'->>'refunded_utc')) WHERE entity = 'sale_refund' AND NOT deleted;
CREATE INDEX ix_es_refund_sale ON entity_state ((payload->'refund'->>'sale_id')) WHERE entity = 'sale_refund' AND NOT deleted;
CREATE INDEX ix_es_session_at ON entity_state (cm_utc(payload->>'opened_utc')) WHERE entity = 'cash_session' AND NOT deleted;
CREATE INDEX ix_es_movement_session ON entity_state ((payload->>'cash_session_id')) WHERE entity = 'cash_movement' AND NOT deleted;
CREATE INDEX ix_es_count_session ON entity_state ((payload->>'session_id')) WHERE entity = 'cash_count' AND NOT deleted;
ANALYZE entity_state;

INSERT INTO schema_version(version) VALUES (9);
