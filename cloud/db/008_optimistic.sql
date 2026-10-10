-- I1 (docs/PLAN_COMPLETAR.md): lo que se cambia en la web (catálogo, usuarios, tiendas) se ve al momento en la web, aunque la caja esté apagada.
-- pending_command: el cambio web que produjo este contenido y que la caja todavía no confirma. Se limpia cuando la caja lo aplica o sube
-- su propia versión; si la caja lo rechaza, el registro vuelve a lo último que subió la caja (sync_event).
-- Inmutable una vez aplicada: agregar 009_*.sql para cambios.
ALTER TABLE entity_state ADD COLUMN pending_command uuid REFERENCES branch_command(id);
CREATE INDEX ix_entity_state_pending ON entity_state(pending_command) WHERE pending_command IS NOT NULL;

INSERT INTO schema_version(version) VALUES (8);
