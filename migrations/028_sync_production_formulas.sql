-- Producción en la web (docs/PLAN_COMPLETAR.md, W6): cada producción ahora sube también sus preparaciones (production_formula_snapshot).
-- Las que ya estaban en la nube se vuelven a enviar una vez para que su detalle las muestre.
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_batch',id,'update' FROM production_batch ORDER BY created_utc,id;
