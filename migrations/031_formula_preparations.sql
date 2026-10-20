-- Preparaciones dentro de preparaciones (docs/PHASE_1K4G_C_RECIPE_TECHNICAL_AUDIT.md, B y F): la masa enriquecida usa masa dulce ya
-- preparada y la tanda de roles usa masa dulce y relleno. La cantidad va en la dimensión del rendimiento de la preparación usada.
CREATE TABLE formula_preparation(
 version_id TEXT NOT NULL REFERENCES formula_version(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence>0),
 preparation_version_id TEXT NOT NULL,
 dimension TEXT NOT NULL, quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>0),
 PRIMARY KEY(version_id,sequence), UNIQUE(version_id,preparation_version_id),
 CHECK(preparation_version_id<>version_id),
 FOREIGN KEY(preparation_version_id,dimension) REFERENCES formula_version(id,yield_dimension) ON DELETE RESTRICT
) STRICT;
CREATE INDEX ix_formula_preparation_used ON formula_preparation(preparation_version_id);
-- Igual que los ingredientes: una versión publicada queda cerrada.
CREATE TRIGGER formula_preparation_sealed_INSERT BEFORE INSERT ON formula_preparation WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Published version sealed'); END;
CREATE TRIGGER formula_preparation_sealed_UPDATE BEFORE UPDATE ON formula_preparation WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Published version sealed'); END;
CREATE TRIGGER formula_preparation_sealed_DELETE BEFORE DELETE ON formula_preparation WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Published version sealed'); END;
-- Sólo se publica una versión cuando todas las preparaciones que usa ya están publicadas; así nunca hay ciclos entre publicadas.
CREATE TRIGGER formula_version_publish_preparations BEFORE UPDATE OF state ON formula_version
WHEN NEW.state='PUBLISHED' AND EXISTS(SELECT 1 FROM formula_preparation p JOIN formula_version u ON u.id=p.preparation_version_id WHERE p.version_id=NEW.id AND u.state<>'PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Preparation not published'); END;
CREATE TRIGGER sync_formula_preparation_insert AFTER INSERT ON formula_preparation
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=NEW.version_id; END;
CREATE TRIGGER sync_formula_preparation_delete AFTER DELETE ON formula_preparation
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=OLD.version_id; END;
