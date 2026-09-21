-- Configuration and historical production storage only; no seeds or confirmation workflow.
CREATE TABLE formula(
 id TEXT PRIMARY KEY NOT NULL, code TEXT NOT NULL COLLATE CATALOG_KEY_V1 UNIQUE,
 name TEXT NOT NULL CHECK(length(trim(name))>0), created_utc TEXT NOT NULL,
 active_version_id TEXT REFERENCES formula_version(id) ON DELETE RESTRICT,
 revision INTEGER NOT NULL DEFAULT 1 CHECK(revision>0), CHECK(length(trim(code))>0)
) STRICT;
CREATE TABLE formula_version(
 id TEXT PRIMARY KEY NOT NULL, formula_id TEXT NOT NULL REFERENCES formula(id) ON DELETE RESTRICT,
 version INTEGER NOT NULL CHECK(version>0), name TEXT NOT NULL CHECK(length(trim(name))>0),
 state TEXT NOT NULL CHECK(state IN('DRAFT','PUBLISHED')),
 yield_dimension TEXT NOT NULL CHECK(yield_dimension IN('MASS','VOLUME','COUNT')),
 yield_atomic INTEGER NOT NULL CHECK(yield_atomic>0), notes TEXT,
 created_utc TEXT NOT NULL, published_utc TEXT, revision INTEGER NOT NULL DEFAULT 1 CHECK(revision>0),
 UNIQUE(formula_id,version), UNIQUE(id,yield_dimension),
 CHECK((state='DRAFT' AND published_utc IS NULL) OR (state='PUBLISHED' AND published_utc IS NOT NULL))
) STRICT;
CREATE TABLE formula_ingredient(
 version_id TEXT NOT NULL REFERENCES formula_version(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence>0), item_id TEXT NOT NULL, dimension TEXT NOT NULL,
 quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>0), count_policy TEXT NOT NULL,
 PRIMARY KEY(version_id,sequence), UNIQUE(version_id,item_id),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 CHECK((dimension='COUNT' AND count_policy IN('EXACT_WHOLE_COUNT','CEILING_WHOLE_COUNT','UNSPECIFIED'))
 OR (dimension IN('MASS','VOLUME') AND count_policy='NOT_APPLICABLE'))
) STRICT;
CREATE TABLE formula_alternative_group(
 version_id TEXT NOT NULL REFERENCES formula_version(id) ON DELETE RESTRICT,
 id TEXT NOT NULL, sequence INTEGER NOT NULL CHECK(sequence>0), name TEXT,
 PRIMARY KEY(version_id,id), UNIQUE(version_id,sequence)
) STRICT;
CREATE TABLE formula_alternative_option(
 version_id TEXT NOT NULL, group_id TEXT NOT NULL, id TEXT NOT NULL,
 sequence INTEGER NOT NULL CHECK(sequence>0), item_id TEXT NOT NULL, dimension TEXT NOT NULL,
 quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>0), count_policy TEXT NOT NULL,
 preference_rank INTEGER CHECK(preference_rank>0),
 PRIMARY KEY(version_id,group_id,id), UNIQUE(version_id,group_id,sequence), UNIQUE(version_id,item_id),
 UNIQUE(version_id,group_id,preference_rank),
 FOREIGN KEY(version_id,group_id) REFERENCES formula_alternative_group(version_id,id) ON DELETE RESTRICT,
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 CHECK((dimension='COUNT' AND count_policy IN('EXACT_WHOLE_COUNT','CEILING_WHOLE_COUNT','UNSPECIFIED'))
 OR (dimension IN('MASS','VOLUME') AND count_policy='NOT_APPLICABLE'))
) STRICT;
CREATE TABLE production_profile(
 id TEXT PRIMARY KEY NOT NULL, product_id TEXT NOT NULL UNIQUE REFERENCES product(id) ON DELETE RESTRICT,
 finished_item_id TEXT NOT NULL REFERENCES inventory_item(id) ON DELETE RESTRICT,
 active_version_id TEXT REFERENCES production_profile_version(id) ON DELETE RESTRICT,
 created_utc TEXT NOT NULL, revision INTEGER NOT NULL DEFAULT 1 CHECK(revision>0)
) STRICT;
CREATE INDEX ix_production_profile_item ON production_profile(finished_item_id);
CREATE TABLE production_profile_version(
 id TEXT PRIMARY KEY NOT NULL, profile_id TEXT NOT NULL REFERENCES production_profile(id) ON DELETE RESTRICT,
 version INTEGER NOT NULL CHECK(version>0), name TEXT NOT NULL CHECK(length(trim(name))>0),
 state TEXT NOT NULL CHECK(state IN('DRAFT','PUBLISHED')),
 created_utc TEXT NOT NULL, published_utc TEXT, revision INTEGER NOT NULL DEFAULT 1 CHECK(revision>0),
 UNIQUE(profile_id,version),
 CHECK((state='DRAFT' AND published_utc IS NULL) OR (state='PUBLISHED' AND published_utc IS NOT NULL))
) STRICT;
CREATE TABLE profile_formula_requirement(
 profile_version_id TEXT NOT NULL REFERENCES production_profile_version(id) ON DELETE RESTRICT,
 formula_version_id TEXT NOT NULL, sequence INTEGER NOT NULL CHECK(sequence>0),
 dimension TEXT NOT NULL, quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>0),
 PRIMARY KEY(profile_version_id,formula_version_id), UNIQUE(profile_version_id,sequence),
 FOREIGN KEY(formula_version_id,dimension) REFERENCES formula_version(id,yield_dimension) ON DELETE RESTRICT
) STRICT;
CREATE TABLE profile_direct_ingredient(
 profile_version_id TEXT NOT NULL REFERENCES production_profile_version(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence>0), item_id TEXT NOT NULL, dimension TEXT NOT NULL,
 quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>0), count_policy TEXT NOT NULL,
 PRIMARY KEY(profile_version_id,sequence), UNIQUE(profile_version_id,item_id),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 CHECK((dimension='COUNT' AND count_policy IN('EXACT_WHOLE_COUNT','CEILING_WHOLE_COUNT','UNSPECIFIED'))
 OR (dimension IN('MASS','VOLUME') AND count_policy='NOT_APPLICABLE'))
) STRICT;
CREATE TABLE production_batch(
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
 AND consumption_operation_id IS NOT NULL AND output_operation_id IS NOT NULL
 AND consumption_operation_id<>output_operation_id))
) STRICT;
CREATE INDEX ix_production_batch_time ON production_batch(created_utc,id);
CREATE TABLE production_output_plan(
 batch_id TEXT NOT NULL REFERENCES production_batch(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence>0), product_id TEXT NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
 finished_item_id TEXT NOT NULL REFERENCES inventory_item(id) ON DELETE RESTRICT,
 profile_version_id TEXT NOT NULL REFERENCES production_profile_version(id) ON DELETE RESTRICT,
 product_name TEXT NOT NULL, item_name TEXT NOT NULL, profile_name TEXT NOT NULL,
 planned_count INTEGER NOT NULL CHECK(planned_count>0), actual_count INTEGER CHECK(actual_count>=0),
 actual_policy_pending INTEGER NOT NULL DEFAULT 0 CHECK(actual_policy_pending IN(0,1)),
 PRIMARY KEY(batch_id,sequence), UNIQUE(batch_id,product_id), UNIQUE(batch_id,finished_item_id),
 UNIQUE(batch_id,profile_version_id),
 CHECK(actual_count IS NULL OR actual_count BETWEEN 1 AND planned_count OR actual_policy_pending=1)
) STRICT;
CREATE TABLE production_formula_snapshot(
 batch_id TEXT NOT NULL REFERENCES production_batch(id) ON DELETE RESTRICT,
 formula_version_id TEXT NOT NULL, formula_name TEXT NOT NULL,
 dimension TEXT NOT NULL, required_atomic INTEGER NOT NULL CHECK(required_atomic>0),
 numerator INTEGER NOT NULL CHECK(numerator>0), denominator INTEGER NOT NULL CHECK(denominator>0),
 PRIMARY KEY(batch_id,formula_version_id),
 FOREIGN KEY(formula_version_id,dimension) REFERENCES formula_version(id,yield_dimension) ON DELETE RESTRICT
) STRICT;
CREATE TABLE production_input_snapshot(
 batch_id TEXT NOT NULL REFERENCES production_batch(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence>0), item_id TEXT NOT NULL, item_name TEXT NOT NULL,
 dimension TEXT NOT NULL, required_atomic INTEGER NOT NULL CHECK(required_atomic>0),
 source_type TEXT NOT NULL CHECK(source_type IN('FORMULA','DIRECT')),
 formula_version_id TEXT, profile_version_id TEXT, group_id TEXT, option_id TEXT,
 count_policy TEXT NOT NULL,
 exact_numerator INTEGER NOT NULL CHECK(exact_numerator>0), exact_denominator INTEGER NOT NULL CHECK(exact_denominator>0),
 PRIMARY KEY(batch_id,sequence),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 FOREIGN KEY(batch_id,formula_version_id) REFERENCES production_formula_snapshot(batch_id,formula_version_id) ON DELETE RESTRICT,
 FOREIGN KEY(batch_id,profile_version_id) REFERENCES production_output_plan(batch_id,profile_version_id) ON DELETE RESTRICT,
 FOREIGN KEY(formula_version_id,group_id,option_id) REFERENCES formula_alternative_option(version_id,group_id,id) ON DELETE RESTRICT,
 CHECK((source_type='FORMULA' AND formula_version_id IS NOT NULL AND profile_version_id IS NULL)
 OR (source_type='DIRECT' AND formula_version_id IS NULL AND profile_version_id IS NOT NULL AND group_id IS NULL AND option_id IS NULL)),
 CHECK((group_id IS NULL AND option_id IS NULL) OR (group_id IS NOT NULL AND option_id IS NOT NULL)),
 CHECK((dimension='COUNT' AND count_policy IN('EXACT_WHOLE_COUNT','CEILING_WHOLE_COUNT'))
 OR (dimension IN('MASS','VOLUME') AND count_policy='NOT_APPLICABLE'))
) STRICT;
CREATE UNIQUE INDEX ux_production_formula_input ON production_input_snapshot(batch_id,formula_version_id,item_id) WHERE source_type='FORMULA';
CREATE UNIQUE INDEX ux_production_direct_input ON production_input_snapshot(batch_id,profile_version_id,item_id) WHERE source_type='DIRECT';
CREATE UNIQUE INDEX ux_production_alternative_choice ON production_input_snapshot(batch_id,formula_version_id,group_id) WHERE group_id IS NOT NULL;
-- Rebuild only the operation parent with incoming FKs deferred until commit.
-- Foreign keys remain enabled; no ledger, balance or sale is deleted/rewritten.
PRAGMA defer_foreign_keys=ON;
CREATE TEMP TABLE operation_013_backup AS SELECT * FROM inventory_operation;
DROP TABLE inventory_operation;CREATE TABLE inventory_operation(
 id TEXT PRIMARY KEY NOT NULL,
 request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64 AND request_sha256 NOT GLOB '*[^0-9A-F]*'),
 kind TEXT NOT NULL CHECK(kind IN('OPENING_BALANCE','RECEIPT','ADJUSTMENT_GAIN','ADJUSTMENT_LOSS','WASTE','SALE_CONSUMPTION','PRODUCTION_CONSUMPTION','PRODUCTION_OUTPUT')),
 state TEXT NOT NULL CHECK(state IN('PENDING','CONFIRMED')),
 actor_id TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
 terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
 reason_code TEXT,
 note TEXT CHECK(note IS NULL OR length(trim(note)) BETWEEN 1 AND 500),
 external_reference TEXT CHECK(external_reference IS NULL OR length(trim(external_reference)) BETWEEN 1 AND 200),
 expected_line_count INTEGER NOT NULL CHECK(expected_line_count BETWEEN 1 AND 1000),
 created_utc TEXT NOT NULL,
 confirmed_utc TEXT,
 sale_evaluation_id TEXT REFERENCES sale_inventory_evaluation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
 production_batch_id TEXT,
 production_batch_status TEXT,
 FOREIGN KEY(production_batch_id,production_batch_status) REFERENCES production_batch(id,status) DEFERRABLE INITIALLY DEFERRED,
 CHECK((kind IN('PRODUCTION_CONSUMPTION','PRODUCTION_OUTPUT') AND production_batch_id IS NOT NULL AND production_batch_status IS NOT NULL AND production_batch_status='CONFIRMED')
 OR (kind NOT IN('PRODUCTION_CONSUMPTION','PRODUCTION_OUTPUT') AND production_batch_id IS NULL AND production_batch_status IS NULL)),
 CHECK((state='PENDING' AND confirmed_utc IS NULL) OR (state='CONFIRMED' AND confirmed_utc IS NOT NULL)),
 CHECK(
   (kind='WASTE' AND reason_code IS NOT NULL AND reason_code IN('DAMAGED','EXPIRED','PRODUCTION_ERROR','SPILL','OTHER'))
   OR (kind IN('ADJUSTMENT_GAIN','ADJUSTMENT_LOSS') AND reason_code IS NOT NULL AND reason_code IN('PHYSICAL_COUNT_VARIANCE','DATA_CORRECTION','OTHER'))
   OR (kind IN('SALE_CONSUMPTION','PRODUCTION_CONSUMPTION','PRODUCTION_OUTPUT') AND reason_code IS NULL AND note IS NULL AND external_reference IS NULL)
   OR (kind='OPENING_BALANCE' AND reason_code IS NULL AND note IS NULL) OR (kind='RECEIPT' AND reason_code IS NULL)
 ),
 CHECK(reason_code<>'OTHER' OR note IS NOT NULL)
) STRICT;
INSERT INTO inventory_operation(id,request_sha256,kind,state,actor_id,terminal_id,reason_code,note,external_reference,expected_line_count,created_utc,confirmed_utc,sale_evaluation_id)
 SELECT * FROM operation_013_backup;
DROP TABLE operation_013_backup;
CREATE INDEX ix_inventory_operation_time ON inventory_operation(confirmed_utc,id);
CREATE INDEX ix_inventory_operation_actor ON inventory_operation(actor_id,confirmed_utc);
CREATE UNIQUE INDEX ux_inventory_sale_evaluation ON inventory_operation(sale_evaluation_id) WHERE sale_evaluation_id IS NOT NULL;
CREATE UNIQUE INDEX ux_inventory_production_origin ON inventory_operation(production_batch_id,kind) WHERE production_batch_id IS NOT NULL;
CREATE TRIGGER inventory_operation_insert_pending BEFORE INSERT ON inventory_operation
WHEN NEW.state<>'PENDING' OR NEW.confirmed_utc IS NOT NULL
BEGIN SELECT RAISE(ABORT,'Inventory operations start pending'); END;
CREATE TRIGGER inventory_operation_confirmed_no_update BEFORE UPDATE ON inventory_operation
WHEN OLD.state='CONFIRMED'
BEGIN SELECT RAISE(ABORT,'Confirmed inventory operations are immutable'); END;
CREATE TRIGGER inventory_operation_confirmed_no_delete BEFORE DELETE ON inventory_operation
WHEN OLD.state='CONFIRMED'
BEGIN SELECT RAISE(ABORT,'Confirmed inventory operations are immutable'); END;
CREATE TRIGGER inventory_operation_only_confirm BEFORE UPDATE ON inventory_operation
WHEN OLD.state='PENDING' AND (
 NEW.state<>'CONFIRMED' OR NEW.confirmed_utc IS NULL
 OR NEW.request_sha256<>OLD.request_sha256 OR NEW.kind<>OLD.kind
 OR NEW.actor_id<>OLD.actor_id OR NEW.terminal_id<>OLD.terminal_id
 OR NEW.reason_code IS NOT OLD.reason_code OR NEW.note IS NOT OLD.note
 OR NEW.external_reference IS NOT OLD.external_reference
 OR NEW.expected_line_count<>OLD.expected_line_count OR NEW.created_utc<>OLD.created_utc
)
BEGIN SELECT RAISE(ABORT,'Pending inventory operation may only be confirmed'); END;
CREATE TRIGGER inventory_operation_confirm_guard BEFORE UPDATE OF state ON inventory_operation
WHEN OLD.state='PENDING' AND NEW.state='CONFIRMED'
BEGIN
 SELECT CASE WHEN (SELECT count(*) FROM inventory_movement WHERE operation_id=OLD.id)<>OLD.expected_line_count
  THEN RAISE(ABORT,'Incomplete inventory operation') END;
 SELECT CASE WHEN EXISTS(
   SELECT 1 FROM inventory_movement m
   WHERE m.operation_id=OLD.id AND (
    (OLD.kind='OPENING_BALANCE' AND (m.is_opening<>1 OR m.delta_atomic<0))
    OR (OLD.kind IN('RECEIPT','ADJUSTMENT_GAIN','PRODUCTION_OUTPUT') AND (m.is_opening<>0 OR m.delta_atomic<=0))
    OR (OLD.kind IN('ADJUSTMENT_LOSS','WASTE','SALE_CONSUMPTION','PRODUCTION_CONSUMPTION') AND (m.is_opening<>0 OR m.delta_atomic>=0))
   ))
  THEN RAISE(ABORT,'Inventory movement sign does not match operation') END;
 SELECT CASE WHEN EXISTS(
   SELECT 1 FROM inventory_movement m
   LEFT JOIN inventory_balance b ON b.item_id=m.item_id AND b.location_id=m.location_id
   WHERE m.operation_id=OLD.id
    AND (b.item_id IS NULL OR b.quantity_atomic<>m.balance_after_atomic OR b.dimension<>m.dimension))
  THEN RAISE(ABORT,'Inventory balance projection does not match operation') END;
 SELECT CASE WHEN NOT EXISTS(
   SELECT 1 FROM audit_entry WHERE action='inventory.operation.confirmed' AND entity_id=OLD.id)
  THEN RAISE(ABORT,'Inventory operation requires audit') END;
END;
CREATE TRIGGER inventory_operation_sale_origin BEFORE INSERT ON inventory_operation
WHEN (NEW.kind='SALE_CONSUMPTION' AND NEW.sale_evaluation_id IS NULL)
 OR (NEW.kind<>'SALE_CONSUMPTION' AND NEW.sale_evaluation_id IS NOT NULL)
BEGIN SELECT RAISE(ABORT,'Sale consumption requires a unique evaluation origin'); END;
CREATE TRIGGER inventory_operation_origin_immutable BEFORE UPDATE OF id,sale_evaluation_id ON inventory_operation
WHEN NEW.id<>OLD.id OR NEW.sale_evaluation_id IS NOT OLD.sale_evaluation_id
BEGIN SELECT RAISE(ABORT,'Inventory identity is immutable'); END;
CREATE TRIGGER inventory_production_identity BEFORE UPDATE OF production_batch_id,production_batch_status ON inventory_operation
WHEN NEW.production_batch_id IS NOT OLD.production_batch_id OR NEW.production_batch_status IS NOT OLD.production_batch_status
BEGIN SELECT RAISE(ABORT,'Production origin immutable'); END;
CREATE TRIGGER inventory_production_origin BEFORE INSERT ON inventory_operation
WHEN NEW.production_batch_id IS NOT NULL
BEGIN
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.production_batch_id AND status='DRAFT'
 AND created_by=NEW.actor_id AND terminal_id=NEW.terminal_id
 AND ((NEW.kind='PRODUCTION_CONSUMPTION' AND consumption_operation_id=NEW.id)
 OR (NEW.kind='PRODUCTION_OUTPUT' AND output_operation_id=NEW.id)))
 THEN RAISE(ABORT,'Production operation requires reciprocal draft origin') END;
END;
CREATE TRIGGER inventory_movement_production BEFORE INSERT ON inventory_movement
WHEN EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.operation_id AND production_batch_id IS NOT NULL)
BEGIN
 SELECT CASE WHEN NEW.is_opening<>0 OR NEW.location_id<>'10000000-0000-0000-0000-000000000001'
 OR EXISTS(SELECT 1 FROM inventory_operation o WHERE o.id=NEW.operation_id AND
 ((o.kind='PRODUCTION_CONSUMPTION' AND NEW.delta_atomic>=0)
 OR (o.kind='PRODUCTION_OUTPUT' AND (NEW.delta_atomic<=0 OR NEW.dimension<>'COUNT'
 OR NOT EXISTS(SELECT 1 FROM inventory_item WHERE id=NEW.item_id AND kind='FINISHED_GOOD')))))
 THEN RAISE(ABORT,'Invalid production movement') END;
END;
CREATE TRIGGER formula_version_draft_insert BEFORE INSERT ON formula_version WHEN NEW.state<>'DRAFT'
BEGIN SELECT RAISE(ABORT,'Versions start draft'); END;
CREATE TRIGGER formula_version_immutable BEFORE UPDATE ON formula_version WHEN OLD.state='PUBLISHED'
BEGIN SELECT RAISE(ABORT,'Published version immutable'); END;
CREATE TRIGGER formula_version_no_delete BEFORE DELETE ON formula_version WHEN OLD.state='PUBLISHED'
BEGIN SELECT RAISE(ABORT,'Published version cannot be deleted'); END;
CREATE TRIGGER formula_version_identity BEFORE UPDATE ON formula_version
WHEN NEW.id<>OLD.id OR NEW.formula_id<>OLD.formula_id OR NEW.version<>OLD.version OR NEW.created_utc<>OLD.created_utc
BEGIN SELECT RAISE(ABORT,'Version identity immutable'); END;
CREATE TRIGGER production_profile_version_draft_insert BEFORE INSERT ON production_profile_version WHEN NEW.state<>'DRAFT'
BEGIN SELECT RAISE(ABORT,'Versions start draft'); END;
CREATE TRIGGER production_profile_version_immutable BEFORE UPDATE ON production_profile_version WHEN OLD.state='PUBLISHED'
BEGIN SELECT RAISE(ABORT,'Published version immutable'); END;
CREATE TRIGGER production_profile_version_no_delete BEFORE DELETE ON production_profile_version WHEN OLD.state='PUBLISHED'
BEGIN SELECT RAISE(ABORT,'Published version cannot be deleted'); END;
CREATE TRIGGER production_profile_version_identity BEFORE UPDATE ON production_profile_version
WHEN NEW.id<>OLD.id OR NEW.profile_id<>OLD.profile_id OR NEW.version<>OLD.version OR NEW.created_utc<>OLD.created_utc
BEGIN SELECT RAISE(ABORT,'Version identity immutable'); END;
CREATE TRIGGER formula_active_INSERT BEFORE INSERT ON formula
WHEN NEW.active_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.active_version_id AND formula_id=NEW.id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Active version must be published by same owner'); END;
CREATE TRIGGER formula_active_UPDATE BEFORE UPDATE ON formula
WHEN NEW.active_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.active_version_id AND formula_id=NEW.id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Active version must be published by same owner'); END;
CREATE TRIGGER production_profile_active_INSERT BEFORE INSERT ON production_profile
WHEN NEW.active_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.active_version_id AND profile_id=NEW.id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Active version must be published by same owner'); END;
CREATE TRIGGER production_profile_active_UPDATE BEFORE UPDATE ON production_profile
WHEN NEW.active_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.active_version_id AND profile_id=NEW.id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Active version must be published by same owner'); END;
CREATE TRIGGER formula_ingredient_sealed_INSERT BEFORE INSERT ON formula_ingredient WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_ingredient_sealed_UPDATE BEFORE UPDATE ON formula_ingredient WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_ingredient_sealed_DELETE BEFORE DELETE ON formula_ingredient WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_group_sealed_INSERT BEFORE INSERT ON formula_alternative_group WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_group_sealed_UPDATE BEFORE UPDATE ON formula_alternative_group WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_group_sealed_DELETE BEFORE DELETE ON formula_alternative_group WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_option_sealed_INSERT BEFORE INSERT ON formula_alternative_option WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_option_sealed_UPDATE BEFORE UPDATE ON formula_alternative_option WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_alternative_option_sealed_DELETE BEFORE DELETE ON formula_alternative_option WHEN EXISTS(SELECT 1 FROM formula_version WHERE id=OLD.version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_formula_requirement_sealed_INSERT BEFORE INSERT ON profile_formula_requirement WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_formula_requirement_sealed_UPDATE BEFORE UPDATE ON profile_formula_requirement WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=OLD.profile_version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_formula_requirement_sealed_DELETE BEFORE DELETE ON profile_formula_requirement WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=OLD.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_direct_ingredient_sealed_INSERT BEFORE INSERT ON profile_direct_ingredient WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_direct_ingredient_sealed_UPDATE BEFORE UPDATE ON profile_direct_ingredient WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=OLD.profile_version_id AND state='PUBLISHED') OR EXISTS(SELECT 1 FROM production_profile_version WHERE id=NEW.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER profile_direct_ingredient_sealed_DELETE BEFORE DELETE ON profile_direct_ingredient WHEN EXISTS(SELECT 1 FROM production_profile_version WHERE id=OLD.profile_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_output_plan_sealed_INSERT BEFORE INSERT ON production_output_plan WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_output_plan_sealed_UPDATE BEFORE UPDATE ON production_output_plan WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED') OR EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_output_plan_sealed_DELETE BEFORE DELETE ON production_output_plan WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_formula_snapshot_sealed_INSERT BEFORE INSERT ON production_formula_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_formula_snapshot_sealed_UPDATE BEFORE UPDATE ON production_formula_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED') OR EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_formula_snapshot_sealed_DELETE BEFORE DELETE ON production_formula_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_input_snapshot_sealed_INSERT BEFORE INSERT ON production_input_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_input_snapshot_sealed_UPDATE BEFORE UPDATE ON production_input_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED') OR EXISTS(SELECT 1 FROM production_batch WHERE id=NEW.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER production_input_snapshot_sealed_DELETE BEFORE DELETE ON production_input_snapshot WHEN EXISTS(SELECT 1 FROM production_batch WHERE id=OLD.batch_id AND status='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Historical child immutable'); END;
CREATE TRIGGER formula_identity BEFORE UPDATE OF id,code,created_utc ON formula
WHEN NEW.id<>OLD.id OR NEW.code<>OLD.code OR NEW.created_utc<>OLD.created_utc
BEGIN SELECT RAISE(ABORT,'Formula identity immutable'); END;
CREATE TRIGGER production_profile_identity BEFORE UPDATE OF id,product_id,finished_item_id,created_utc ON production_profile
WHEN NEW.id<>OLD.id OR NEW.product_id<>OLD.product_id OR NEW.finished_item_id<>OLD.finished_item_id OR NEW.created_utc<>OLD.created_utc
BEGIN SELECT RAISE(ABORT,'Profile identity immutable'); END;
CREATE TRIGGER production_profile_target BEFORE INSERT ON production_profile
WHEN NOT EXISTS(SELECT 1 FROM inventory_item WHERE id=NEW.finished_item_id AND kind='FINISHED_GOOD' AND dimension='COUNT')
BEGIN SELECT RAISE(ABORT,'Profile needs finished count item'); END;
CREATE TRIGGER formula_publish_guard BEFORE UPDATE OF state ON formula_version WHEN NEW.state='PUBLISHED'
BEGIN
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM formula_ingredient WHERE version_id=NEW.id)
 AND NOT EXISTS(SELECT 1 FROM formula_alternative_group WHERE version_id=NEW.id)
 THEN RAISE(ABORT,'Empty formula') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM formula_alternative_group g WHERE g.version_id=NEW.id
 AND (SELECT count(*) FROM formula_alternative_option o WHERE o.version_id=g.version_id AND o.group_id=g.id)<2)
 THEN RAISE(ABORT,'Alternative group needs options') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM formula_ingredient i JOIN formula_alternative_option o
 ON i.version_id=o.version_id AND i.item_id=o.item_id WHERE i.version_id=NEW.id)
 THEN RAISE(ABORT,'Duplicate formula ingredient') END;
END;
CREATE TRIGGER profile_publish_guard BEFORE UPDATE OF state ON production_profile_version WHEN NEW.state='PUBLISHED'
BEGIN
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM profile_formula_requirement WHERE profile_version_id=NEW.id)
 AND NOT EXISTS(SELECT 1 FROM profile_direct_ingredient WHERE profile_version_id=NEW.id)
 THEN RAISE(ABORT,'Empty profile') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM profile_formula_requirement r JOIN formula_version f ON f.id=r.formula_version_id
 WHERE r.profile_version_id=NEW.id AND f.state<>'PUBLISHED') THEN RAISE(ABORT,'Profile requires published formula') END;
END;
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
 OR NOT EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.output_operation_id AND kind='PRODUCTION_OUTPUT'
 AND state='CONFIRMED' AND production_batch_id=NEW.id AND actor_id=NEW.created_by AND terminal_id=NEW.terminal_id)
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
CREATE TRIGGER production_output_owner_INSERT BEFORE INSERT ON production_output_plan
WHEN NOT EXISTS(SELECT 1 FROM production_profile_version v JOIN production_profile p ON p.id=v.profile_id
 WHERE v.id=NEW.profile_version_id AND v.state='PUBLISHED' AND p.product_id=NEW.product_id AND p.finished_item_id=NEW.finished_item_id)
BEGIN SELECT RAISE(ABORT,'Output must match published profile'); END;
CREATE TRIGGER production_formula_owner_INSERT BEFORE INSERT ON production_formula_snapshot
WHEN NOT EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.formula_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Formula snapshot requires published version'); END;
CREATE TRIGGER production_input_origin_INSERT BEFORE INSERT ON production_input_snapshot
BEGIN
 SELECT CASE WHEN NEW.source_type='DIRECT' AND NOT EXISTS(SELECT 1 FROM profile_direct_ingredient
 WHERE profile_version_id=NEW.profile_version_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid direct input source') END;
 SELECT CASE WHEN NEW.source_type='FORMULA' AND NEW.group_id IS NULL AND NOT EXISTS(SELECT 1 FROM formula_ingredient
 WHERE version_id=NEW.formula_version_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid formula input source') END;
 SELECT CASE WHEN NEW.group_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formula_alternative_option
 WHERE version_id=NEW.formula_version_id AND group_id=NEW.group_id AND id=NEW.option_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid alternative input source') END;
END;
CREATE TRIGGER production_formula_snapshot_rational_INSERT BEFORE INSERT ON production_formula_snapshot
BEGIN
 SELECT CASE WHEN (WITH RECURSIVE gcd(a,b) AS (SELECT NEW.numerator,NEW.denominator UNION ALL SELECT b,a%b FROM gcd WHERE b<>0)
 SELECT a FROM gcd WHERE b=0)<>1 THEN RAISE(ABORT,'Rational must be reduced') END;
END;
CREATE TRIGGER production_input_snapshot_rational_INSERT BEFORE INSERT ON production_input_snapshot
BEGIN
 SELECT CASE WHEN (WITH RECURSIVE gcd(a,b) AS (SELECT NEW.exact_numerator,NEW.exact_denominator UNION ALL SELECT b,a%b FROM gcd WHERE b<>0)
 SELECT a FROM gcd WHERE b=0)<>1 THEN RAISE(ABORT,'Rational must be reduced') END;
END;
CREATE TRIGGER production_output_owner_UPDATE BEFORE UPDATE ON production_output_plan
WHEN NOT EXISTS(SELECT 1 FROM production_profile_version v JOIN production_profile p ON p.id=v.profile_id
 WHERE v.id=NEW.profile_version_id AND v.state='PUBLISHED' AND p.product_id=NEW.product_id AND p.finished_item_id=NEW.finished_item_id)
BEGIN SELECT RAISE(ABORT,'Output must match published profile'); END;
CREATE TRIGGER production_formula_owner_UPDATE BEFORE UPDATE ON production_formula_snapshot
WHEN NOT EXISTS(SELECT 1 FROM formula_version WHERE id=NEW.formula_version_id AND state='PUBLISHED')
BEGIN SELECT RAISE(ABORT,'Formula snapshot requires published version'); END;
CREATE TRIGGER production_input_origin_UPDATE BEFORE UPDATE ON production_input_snapshot
BEGIN
 SELECT CASE WHEN NEW.source_type='DIRECT' AND NOT EXISTS(SELECT 1 FROM profile_direct_ingredient
 WHERE profile_version_id=NEW.profile_version_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid direct input source') END;
 SELECT CASE WHEN NEW.source_type='FORMULA' AND NEW.group_id IS NULL AND NOT EXISTS(SELECT 1 FROM formula_ingredient
 WHERE version_id=NEW.formula_version_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid formula input source') END;
 SELECT CASE WHEN NEW.group_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formula_alternative_option
 WHERE version_id=NEW.formula_version_id AND group_id=NEW.group_id AND id=NEW.option_id AND item_id=NEW.item_id AND dimension=NEW.dimension AND count_policy=NEW.count_policy)
 THEN RAISE(ABORT,'Invalid alternative input source') END;
END;
CREATE TRIGGER production_formula_snapshot_rational_UPDATE BEFORE UPDATE ON production_formula_snapshot
BEGIN
 SELECT CASE WHEN (WITH RECURSIVE gcd(a,b) AS (SELECT NEW.numerator,NEW.denominator UNION ALL SELECT b,a%b FROM gcd WHERE b<>0)
 SELECT a FROM gcd WHERE b=0)<>1 THEN RAISE(ABORT,'Rational must be reduced') END;
END;
CREATE TRIGGER production_input_snapshot_rational_UPDATE BEFORE UPDATE ON production_input_snapshot
BEGIN
 SELECT CASE WHEN (WITH RECURSIVE gcd(a,b) AS (SELECT NEW.exact_numerator,NEW.exact_denominator UNION ALL SELECT b,a%b FROM gcd WHERE b<>0)
 SELECT a FROM gcd WHERE b=0)<>1 THEN RAISE(ABORT,'Rational must be reduced') END;
END;

-- Structural coverage only; planning arithmetic and business confirmation remain outside SQL.
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
CREATE TRIGGER formula_publish_time BEFORE UPDATE OF state ON formula_version WHEN NEW.state='PUBLISHED'
BEGIN
 SELECT CASE WHEN julianday(NEW.published_utc) IS NULL OR julianday(NEW.created_utc) IS NULL
 OR julianday(NEW.published_utc)<julianday(NEW.created_utc) THEN RAISE(ABORT,'Invalid publication time') END;
END;
CREATE TRIGGER profile_publish_time BEFORE UPDATE OF state ON production_profile_version WHEN NEW.state='PUBLISHED'
BEGIN
 SELECT CASE WHEN julianday(NEW.published_utc) IS NULL OR julianday(NEW.created_utc) IS NULL
 OR julianday(NEW.published_utc)<julianday(NEW.created_utc) THEN RAISE(ABORT,'Invalid publication time') END;
END;
CREATE TRIGGER formula_publication_order BEFORE UPDATE OF state ON formula_version WHEN NEW.state='PUBLISHED'
AND EXISTS(SELECT 1 FROM formula_version WHERE formula_id=NEW.formula_id AND state='PUBLISHED' AND version>=NEW.version)
BEGIN SELECT RAISE(ABORT,'Publication version must increase'); END;
CREATE TRIGGER profile_publication_order BEFORE UPDATE OF state ON production_profile_version WHEN NEW.state='PUBLISHED'
AND EXISTS(SELECT 1 FROM production_profile_version WHERE profile_id=NEW.profile_id AND state='PUBLISHED' AND version>=NEW.version)
BEGIN SELECT RAISE(ABORT,'Publication version must increase'); END;
CREATE TRIGGER inventory_production_confirm_shape BEFORE UPDATE OF state ON inventory_operation
WHEN NEW.state='CONFIRMED' AND NEW.kind IN('PRODUCTION_CONSUMPTION','PRODUCTION_OUTPUT')
BEGIN
 SELECT CASE WHEN EXISTS(SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.id AND
 (m.location_id<>'10000000-0000-0000-0000-000000000001' OR m.is_opening<>0
 OR (NEW.kind='PRODUCTION_OUTPUT' AND (m.dimension<>'COUNT' OR NOT EXISTS(
 SELECT 1 FROM inventory_item i WHERE i.id=m.item_id AND i.kind='FINISHED_GOOD')))))
 THEN RAISE(ABORT,'Invalid production movement shape') END;
END;
