-- Costo de referencia de cada insumo (docs/PROFIT.md): con las recetas da el costo estimado de cada pan.
-- Se guarda como «cost_cents por per_atomic unidades atómicas» (mg, µL o piezas), p. ej. $28.50 por kg = 2850 por 1 000 000 mg.
CREATE TABLE inventory_item_cost(
 item_id TEXT PRIMARY KEY NOT NULL REFERENCES inventory_item(id) ON DELETE RESTRICT,
 cost_cents INTEGER NOT NULL CHECK(cost_cents BETWEEN 0 AND 100000000),
 per_atomic INTEGER NOT NULL CHECK(per_atomic BETWEEN 1 AND 1000000000000),
 unit_label TEXT NOT NULL CHECK(length(trim(unit_label)) BETWEEN 1 AND 40),
 updated_utc TEXT NOT NULL,
 updated_by TEXT NOT NULL REFERENCES app_user(id)
) STRICT;
-- Viaja a la nube dentro del insumo.
CREATE TRIGGER sync_inventory_cost_insert AFTER INSERT ON inventory_item_cost
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
CREATE TRIGGER sync_inventory_cost_update AFTER UPDATE ON inventory_item_cost
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
