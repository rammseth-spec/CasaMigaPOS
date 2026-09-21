-- Corrective storage migration only. All previous SQL resources remain immutable.
-- Only production_batch is rebuilt; deferred FKs keep incoming references intact.
PRAGMA defer_foreign_keys=ON;
CREATE TEMP TABLE batch_014_backup AS SELECT * FROM production_batch;
DROP TABLE production_batch;
CREATE TABLE production_batch_014(
 id TEXT PRIMARY KEY NOT NULL, operation_id TEXT NOT NULL UNIQUE,
 status TEXT NOT NULL CHECK(status IN('DRAFT','CONFIRMED')),
 created_by TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
 terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
 created_utc TEXT NOT NULL, confirmed_utc TEXT, notes TEXT,
 planning_issues_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(planning_issues_json) AND json_type(planning_issues_json)='array'),
 contract_version INTEGER NOT NULL CHECK(contract_version=1), identity_version INTEGER NOT NULL CHECK(identity_version=1),
 request_sha256 TEXT CHECK(request_sha256 IS NULL OR (length(request_sha256)=64 AND request_sha256 NOT GLOB '*[^0-9A-F]*')),
 consumption_operation_id TEXT UNIQUE REFERENCES inventory_operation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
 output_operation_id TEXT UNIQUE REFERENCES inventory_operation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
 UNIQUE(id,status),
 CHECK((status='DRAFT' AND confirmed_utc IS NULL) OR
 (status='CONFIRMED' AND confirmed_utc IS NOT NULL AND request_sha256 IS NOT NULL
 AND consumption_operation_id IS NOT NULL
 AND (output_operation_id IS NULL OR consumption_operation_id<>output_operation_id)))
) STRICT;
-- Restore the final parent name before reinserting rows to resolve deferred FK counters.
PRAGMA legacy_alter_table=ON;
ALTER TABLE production_batch_014 RENAME TO production_batch;
PRAGMA legacy_alter_table=OFF;
INSERT INTO production_batch SELECT * FROM batch_014_backup;
DROP TABLE batch_014_backup;
CREATE INDEX ix_production_batch_time ON production_batch(created_utc,id);
CREATE TRIGGER production_batch_insert_draft BEFORE INSERT ON production_batch
WHEN NEW.status<>'DRAFT' OR NEW.consumption_operation_id IS NOT NULL OR NEW.output_operation_id IS NOT NULL
BEGIN SELECT RAISE(ABORT,'Batch starts as draft without operations'); END;
CREATE TRIGGER production_batch_immutable BEFORE UPDATE ON production_batch WHEN OLD.status='CONFIRMED'
BEGIN SELECT RAISE(ABORT,'Confirmed batch immutable'); END;
CREATE TRIGGER production_batch_no_delete BEFORE DELETE ON production_batch WHEN OLD.status='CONFIRMED'
BEGIN SELECT RAISE(ABORT,'Confirmed batch cannot be deleted'); END;
CREATE TRIGGER production_batch_identity BEFORE UPDATE ON production_batch
WHEN NEW.id<>OLD.id OR NEW.operation_id<>OLD.operation_id OR NEW.created_by<>OLD.created_by OR NEW.terminal_id<>OLD.terminal_id
 OR NEW.created_utc<>OLD.created_utc OR NEW.contract_version<>OLD.contract_version OR NEW.identity_version<>OLD.identity_version
BEGIN SELECT RAISE(ABORT,'Batch identity immutable'); END;
CREATE TRIGGER production_batch_confirm BEFORE UPDATE OF status ON production_batch WHEN NEW.status='CONFIRMED'
BEGIN
 SELECT CASE WHEN json_array_length(NEW.planning_issues_json)<>0 THEN RAISE(ABORT,'Unresolved planning issues') END;
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM production_output_plan WHERE batch_id=NEW.id)
 OR EXISTS(SELECT 1 FROM production_output_plan WHERE batch_id=NEW.id AND actual_count IS NULL)
 OR NOT EXISTS(SELECT 1 FROM production_input_snapshot WHERE batch_id=NEW.id)
 THEN RAISE(ABORT,'Incomplete production snapshots') END;
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.consumption_operation_id AND kind='PRODUCTION_CONSUMPTION'
 AND state='CONFIRMED' AND production_batch_id=NEW.id AND actor_id=NEW.created_by AND terminal_id=NEW.terminal_id)
 OR (NEW.output_operation_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.output_operation_id AND kind='PRODUCTION_OUTPUT'
 AND state='CONFIRMED' AND production_batch_id=NEW.id AND actor_id=NEW.created_by AND terminal_id=NEW.terminal_id))
 THEN RAISE(ABORT,'Incomplete reciprocal production operations') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.consumption_operation_id
 AND -m.delta_atomic IS NOT (SELECT SUM(required_atomic) FROM production_input_snapshot i WHERE i.batch_id=NEW.id AND i.item_id=m.item_id AND i.dimension=m.dimension))
 OR EXISTS(SELECT 1 FROM production_input_snapshot i WHERE i.batch_id=NEW.id AND NOT EXISTS(
 SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.consumption_operation_id AND m.item_id=i.item_id AND m.dimension=i.dimension))
 THEN RAISE(ABORT,'Consumption mismatches input snapshot') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.output_operation_id AND m.delta_atomic IS NOT
 (SELECT actual_count FROM production_output_plan o WHERE o.batch_id=NEW.id AND o.finished_item_id=m.item_id))
 OR EXISTS(SELECT 1 FROM production_output_plan o WHERE o.batch_id=NEW.id AND o.actual_count>0 AND NOT EXISTS(
 SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.output_operation_id AND m.item_id=o.finished_item_id AND m.delta_atomic=o.actual_count))
 THEN RAISE(ABORT,'Output mismatches actual quantities') END;
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM audit_entry WHERE action='production.confirmed' AND entity_id=NEW.id)
 THEN RAISE(ABORT,'Production requires audit') END;
END;
CREATE TRIGGER production_batch_snapshot_coverage BEFORE UPDATE OF status ON production_batch WHEN NEW.status='CONFIRMED'
BEGIN
 SELECT CASE WHEN EXISTS(
 SELECT 1 FROM production_output_plan o JOIN profile_formula_requirement r ON r.profile_version_id=o.profile_version_id
 WHERE o.batch_id=NEW.id AND NOT EXISTS(SELECT 1 FROM production_formula_snapshot s WHERE s.batch_id=NEW.id AND s.formula_version_id=r.formula_version_id))
 OR EXISTS(SELECT 1 FROM production_formula_snapshot s WHERE s.batch_id=NEW.id AND NOT EXISTS(
 SELECT 1 FROM production_output_plan o JOIN profile_formula_requirement r ON r.profile_version_id=o.profile_version_id
 WHERE o.batch_id=NEW.id AND r.formula_version_id=s.formula_version_id))
 THEN RAISE(ABORT,'Formula snapshot coverage incomplete') END;
 SELECT CASE WHEN EXISTS(
 SELECT 1 FROM production_formula_snapshot s JOIN formula_ingredient r ON r.version_id=s.formula_version_id
 WHERE s.batch_id=NEW.id AND NOT EXISTS(SELECT 1 FROM production_input_snapshot i WHERE i.batch_id=NEW.id
 AND i.formula_version_id=s.formula_version_id AND i.item_id=r.item_id AND i.group_id IS NULL))
 OR EXISTS(SELECT 1 FROM production_formula_snapshot s JOIN formula_alternative_group g ON g.version_id=s.formula_version_id
 WHERE s.batch_id=NEW.id AND NOT EXISTS(SELECT 1 FROM production_input_snapshot i WHERE i.batch_id=NEW.id
 AND i.formula_version_id=s.formula_version_id AND i.group_id=g.id))
 OR EXISTS(SELECT 1 FROM production_output_plan o JOIN profile_direct_ingredient d ON d.profile_version_id=o.profile_version_id
 WHERE o.batch_id=NEW.id AND NOT EXISTS(SELECT 1 FROM production_input_snapshot i WHERE i.batch_id=NEW.id
 AND i.profile_version_id=o.profile_version_id AND i.item_id=d.item_id))
 THEN RAISE(ABORT,'Input snapshot coverage incomplete') END;
END;
-- Positive outputs and the output operation must exist together, in both directions.
CREATE TRIGGER production_batch_output_presence BEFORE UPDATE OF status ON production_batch
WHEN NEW.status='CONFIRMED'
BEGIN
 SELECT CASE WHEN EXISTS(SELECT 1 FROM production_output_plan WHERE batch_id=NEW.id AND actual_count>0)
                 <> (NEW.output_operation_id IS NOT NULL)
 THEN RAISE(ABORT,'Output operation presence must match positive actual outputs') END;
 SELECT CASE WHEN NEW.output_operation_id IS NULL AND EXISTS(
 SELECT 1 FROM inventory_operation WHERE production_batch_id=NEW.id AND kind='PRODUCTION_OUTPUT')
 THEN RAISE(ABORT,'Zero output batch cannot have an output operation') END;
END;
CREATE TRIGGER inventory_production_output_requires_actual BEFORE INSERT ON inventory_operation
WHEN NEW.kind='PRODUCTION_OUTPUT' AND NOT EXISTS(
 SELECT 1 FROM production_output_plan WHERE batch_id=NEW.production_batch_id AND actual_count>0)
BEGIN SELECT RAISE(ABORT,'Production output requires positive actual output'); END;
-- Fail this migration transaction, rather than merely displaying FK violations.
CREATE TEMP TABLE migration_014_fk_check(violations INTEGER CHECK(violations=0));
INSERT INTO migration_014_fk_check SELECT count(*) FROM pragma_foreign_key_check;
DROP TABLE migration_014_fk_check;
