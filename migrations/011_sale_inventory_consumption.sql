-- Executed inside Database.Initialize transaction; foreign_keys remains ON.
-- Preserve child rows while rebuilding parent. No balance writes.
DROP TRIGGER inventory_operation_insert_pending;
DROP TRIGGER inventory_operation_confirmed_no_update;
DROP TRIGGER inventory_operation_confirmed_no_delete;
DROP TRIGGER inventory_operation_only_confirm;
DROP TRIGGER inventory_operation_confirm_guard;
DROP TRIGGER inventory_movement_pending_only;
DROP TRIGGER inventory_movement_opening_first;
DROP TRIGGER inventory_movement_requires_opening;
DROP TRIGGER inventory_movement_confirmed_no_update;
DROP TRIGGER inventory_movement_confirmed_no_delete;
CREATE TABLE inventory_movement_011_backup AS SELECT * FROM inventory_movement;
CREATE TABLE inventory_operation_011(
 id TEXT PRIMARY KEY NOT NULL,
 request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64 AND request_sha256 NOT GLOB '*[^0-9A-F]*'),
 kind TEXT NOT NULL CHECK(kind IN('OPENING_BALANCE','RECEIPT','ADJUSTMENT_GAIN','ADJUSTMENT_LOSS','WASTE','SALE_CONSUMPTION')),
 state TEXT NOT NULL CHECK(state IN('PENDING','CONFIRMED')),
 actor_id TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
 terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
 reason_code TEXT,
 note TEXT CHECK(note IS NULL OR length(trim(note)) BETWEEN 1 AND 500),
 external_reference TEXT CHECK(external_reference IS NULL OR length(trim(external_reference)) BETWEEN 1 AND 200),
 expected_line_count INTEGER NOT NULL CHECK(expected_line_count BETWEEN 1 AND 1000),
 created_utc TEXT NOT NULL,
 confirmed_utc TEXT,
 CHECK((state='PENDING' AND confirmed_utc IS NULL) OR (state='CONFIRMED' AND confirmed_utc IS NOT NULL)),
 CHECK(
   (kind='WASTE' AND reason_code IS NOT NULL AND reason_code IN('DAMAGED','EXPIRED','PRODUCTION_ERROR','SPILL','OTHER'))
   OR (kind IN('ADJUSTMENT_GAIN','ADJUSTMENT_LOSS') AND reason_code IS NOT NULL AND reason_code IN('PHYSICAL_COUNT_VARIANCE','DATA_CORRECTION','OTHER'))
   OR (kind='SALE_CONSUMPTION' AND reason_code IS NULL AND note IS NULL AND external_reference IS NULL)
   OR (kind='OPENING_BALANCE' AND reason_code IS NULL AND note IS NULL) OR (kind='RECEIPT' AND reason_code IS NULL)
 ),
 CHECK(reason_code<>'OTHER' OR note IS NOT NULL)
) STRICT;
INSERT INTO inventory_operation_011 SELECT * FROM inventory_operation;
DROP TABLE inventory_movement;
DROP TABLE inventory_operation;
ALTER TABLE inventory_operation_011 RENAME TO inventory_operation;
CREATE TABLE inventory_movement(
 id TEXT PRIMARY KEY NOT NULL,
 operation_id TEXT NOT NULL REFERENCES inventory_operation(id) ON DELETE RESTRICT,
 sequence INTEGER NOT NULL CHECK(sequence BETWEEN 1 AND 1000),
 item_id TEXT NOT NULL,
 location_id TEXT NOT NULL REFERENCES inventory_location(id) ON DELETE RESTRICT,
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 is_opening INTEGER NOT NULL CHECK(is_opening IN(0,1)),
 delta_atomic INTEGER NOT NULL CHECK(delta_atomic>-9223372036854775808),
 balance_before_atomic INTEGER NOT NULL CHECK(balance_before_atomic>=0),
 balance_after_atomic INTEGER NOT NULL CHECK(balance_after_atomic>=0),
 capture_unit_code TEXT NOT NULL CHECK(length(trim(capture_unit_code)) BETWEEN 1 AND 80),
 captured_quantity TEXT NOT NULL CHECK(length(trim(captured_quantity)) BETWEEN 1 AND 80),
 capture_atomic_factor INTEGER NOT NULL CHECK(capture_atomic_factor>0),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 UNIQUE(operation_id,sequence),
 UNIQUE(operation_id,item_id,location_id),
 CHECK(
   (is_opening=1 AND delta_atomic>=0 AND balance_before_atomic=0 AND balance_after_atomic=delta_atomic)
   OR (is_opening=0 AND delta_atomic>0 AND balance_after_atomic>=balance_before_atomic
       AND balance_after_atomic-balance_before_atomic=delta_atomic)
   OR (is_opening=0 AND delta_atomic<0 AND balance_before_atomic>=balance_after_atomic
       AND balance_before_atomic-balance_after_atomic=-delta_atomic)
 )
) STRICT;
INSERT INTO inventory_movement SELECT * FROM inventory_movement_011_backup;
DROP TABLE inventory_movement_011_backup;
CREATE INDEX ix_inventory_operation_time ON inventory_operation(confirmed_utc,id);
CREATE INDEX ix_inventory_operation_actor ON inventory_operation(actor_id,confirmed_utc);
CREATE UNIQUE INDEX ux_inventory_opening ON inventory_movement(item_id,location_id) WHERE is_opening=1;
CREATE INDEX ix_inventory_movement_item ON inventory_movement(item_id,location_id,operation_id);
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
    OR (OLD.kind IN('RECEIPT','ADJUSTMENT_GAIN') AND (m.is_opening<>0 OR m.delta_atomic<=0))
    OR (OLD.kind IN('ADJUSTMENT_LOSS','WASTE','SALE_CONSUMPTION') AND (m.is_opening<>0 OR m.delta_atomic>=0))
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
CREATE TRIGGER inventory_movement_pending_only BEFORE INSERT ON inventory_movement
WHEN NOT EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.operation_id AND state='PENDING')
BEGIN SELECT RAISE(ABORT,'Movements require a pending operation'); END;
CREATE TRIGGER inventory_movement_opening_first BEFORE INSERT ON inventory_movement
WHEN NEW.is_opening=1 AND EXISTS(
 SELECT 1 FROM inventory_movement WHERE item_id=NEW.item_id AND location_id=NEW.location_id)
BEGIN SELECT RAISE(ABORT,'Opening balance must be first'); END;
CREATE TRIGGER inventory_movement_requires_opening BEFORE INSERT ON inventory_movement
WHEN NEW.is_opening=0 AND NOT EXISTS(
 SELECT 1 FROM inventory_movement m JOIN inventory_operation o ON o.id=m.operation_id
 WHERE m.item_id=NEW.item_id AND m.location_id=NEW.location_id AND m.is_opening=1 AND o.state='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Inventory position is not initialized'); END;
CREATE TRIGGER inventory_movement_confirmed_no_update BEFORE UPDATE ON inventory_movement
WHEN EXISTS(SELECT 1 FROM inventory_operation WHERE id=OLD.operation_id AND state='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Confirmed inventory movements are immutable'); END;
CREATE TRIGGER inventory_movement_confirmed_no_delete BEFORE DELETE ON inventory_movement
WHEN EXISTS(SELECT 1 FROM inventory_operation WHERE id=OLD.operation_id AND state='CONFIRMED')
BEGIN SELECT RAISE(ABORT,'Confirmed inventory movements are immutable'); END;
-- Evaluation headers seal their complete snapshots, inserted first with deferred FKs.
CREATE TABLE sale_inventory_evaluation(
 id TEXT PRIMARY KEY NOT NULL,
 sale_id TEXT NOT NULL UNIQUE REFERENCES sale(id) ON DELETE RESTRICT,
 checkout_operation_id TEXT NOT NULL UNIQUE REFERENCES sale(operation_id) ON DELETE RESTRICT,
 policy_version INTEGER NOT NULL CHECK(policy_version=1),
 status TEXT NOT NULL CHECK(status IN('DISABLED','NO_CONSUMPTION','CONSUMED')),
 inventory_operation_id TEXT UNIQUE REFERENCES inventory_operation(id) ON DELETE RESTRICT,
 identity_version INTEGER NOT NULL CHECK(identity_version=1),
 evaluation_sha256 TEXT NOT NULL CHECK(length(evaluation_sha256)=64 AND evaluation_sha256 NOT GLOB '*[^0-9A-F]*'),
 confirmed_utc TEXT NOT NULL,
 CHECK((status='CONSUMED' AND inventory_operation_id IS NOT NULL) OR
       (status IN('DISABLED','NO_CONSUMPTION') AND inventory_operation_id IS NULL))
) STRICT;
ALTER TABLE inventory_operation ADD COLUMN sale_evaluation_id TEXT
 REFERENCES sale_inventory_evaluation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
CREATE UNIQUE INDEX ux_inventory_sale_evaluation ON inventory_operation(sale_evaluation_id) WHERE sale_evaluation_id IS NOT NULL;
CREATE TRIGGER inventory_operation_sale_origin BEFORE INSERT ON inventory_operation
WHEN (NEW.kind='SALE_CONSUMPTION' AND NEW.sale_evaluation_id IS NULL)
 OR (NEW.kind<>'SALE_CONSUMPTION' AND NEW.sale_evaluation_id IS NOT NULL)
BEGIN SELECT RAISE(ABORT,'Sale consumption requires a unique evaluation origin'); END;
CREATE TRIGGER inventory_operation_origin_immutable BEFORE UPDATE OF id,sale_evaluation_id ON inventory_operation
WHEN NEW.id<>OLD.id OR NEW.sale_evaluation_id IS NOT OLD.sale_evaluation_id
BEGIN SELECT RAISE(ABORT,'Inventory identity is immutable'); END;
CREATE TRIGGER inventory_movement_sale_sign BEFORE INSERT ON inventory_movement
WHEN EXISTS(SELECT 1 FROM inventory_operation WHERE id=NEW.operation_id AND kind='SALE_CONSUMPTION')
 AND (NEW.delta_atomic>=0 OR NEW.is_opening<>0 OR NEW.location_id<>'10000000-0000-0000-0000-000000000001')
BEGIN SELECT RAISE(ABORT,'Sale consumption requires negative MAIN movement'); END;

CREATE TABLE sale_inventory_line_snapshot(
 evaluation_id TEXT NOT NULL REFERENCES sale_inventory_evaluation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
 sale_id TEXT NOT NULL,
 position INTEGER NOT NULL,
 product_id TEXT NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
 sold_quantity INTEGER NOT NULL CHECK(sold_quantity BETWEEN 1 AND 2147483647),
 track_inventory INTEGER NOT NULL CHECK(track_inventory IN(0,1)),
 link_id TEXT REFERENCES product_inventory_link(id) ON DELETE RESTRICT,
 item_id TEXT,
 location_id TEXT REFERENCES inventory_location(id) ON DELETE RESTRICT,
 dimension TEXT CHECK(dimension IN('MASS','VOLUME','COUNT')),
 quantity_per_product_unit_atomic INTEGER,
 total_consumed_atomic INTEGER NOT NULL CHECK(total_consumed_atomic>=0),
 policy_version INTEGER NOT NULL CHECK(policy_version=1),
 PRIMARY KEY(evaluation_id,position),
 FOREIGN KEY(sale_id,position) REFERENCES sale_item(sale_id,position) ON DELETE RESTRICT,
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 CHECK((track_inventory=0 AND link_id IS NULL AND item_id IS NULL AND location_id IS NULL
   AND dimension IS NULL AND quantity_per_product_unit_atomic IS NULL AND total_consumed_atomic=0)
 OR (track_inventory=1 AND link_id IS NOT NULL AND item_id IS NOT NULL
   AND location_id IS NOT NULL AND location_id='10000000-0000-0000-0000-000000000001' AND dimension IS NOT NULL
   AND quantity_per_product_unit_atomic IS NOT NULL AND quantity_per_product_unit_atomic>0
   AND total_consumed_atomic>0 AND CASE WHEN sold_quantity>9223372036854775807/quantity_per_product_unit_atomic
     THEN 0 ELSE total_consumed_atomic=sold_quantity*quantity_per_product_unit_atomic END))
) STRICT;
CREATE INDEX ix_sale_inventory_snapshot_sale_item ON sale_inventory_line_snapshot(sale_id,position);
CREATE INDEX ix_sale_inventory_snapshot_product ON sale_inventory_line_snapshot(product_id);
CREATE INDEX ix_sale_inventory_snapshot_link ON sale_inventory_line_snapshot(link_id) WHERE link_id IS NOT NULL;
CREATE TRIGGER sale_inventory_snapshot_no_append BEFORE INSERT ON sale_inventory_line_snapshot
WHEN EXISTS(SELECT 1 FROM sale_inventory_evaluation WHERE id=NEW.evaluation_id)
BEGIN SELECT RAISE(ABORT,'Evaluation is already sealed'); END;
CREATE TRIGGER sale_inventory_snapshot_matches_item BEFORE INSERT ON sale_inventory_line_snapshot
WHEN NOT EXISTS(SELECT 1 FROM sale_item WHERE sale_id=NEW.sale_id AND position=NEW.position
 AND product_id=NEW.product_id AND quantity=NEW.sold_quantity)
 OR (NEW.track_inventory=1 AND NOT EXISTS(SELECT 1 FROM product_inventory_link WHERE id=NEW.link_id
 AND product_id=NEW.product_id AND inventory_item_id=NEW.item_id AND dimension=NEW.dimension))
BEGIN SELECT RAISE(ABORT,'Snapshot does not match sale item or link'); END;
CREATE TRIGGER sale_inventory_snapshot_no_update BEFORE UPDATE ON sale_inventory_line_snapshot
BEGIN SELECT RAISE(ABORT,'Inventory snapshots are immutable'); END;
CREATE TRIGGER sale_inventory_snapshot_no_delete BEFORE DELETE ON sale_inventory_line_snapshot
BEGIN SELECT RAISE(ABORT,'Inventory snapshots are immutable'); END;
CREATE TRIGGER sale_inventory_evaluation_no_update BEFORE UPDATE ON sale_inventory_evaluation
BEGIN SELECT RAISE(ABORT,'Inventory evaluations are immutable'); END;
CREATE TRIGGER sale_inventory_evaluation_no_delete BEFORE DELETE ON sale_inventory_evaluation
BEGIN SELECT RAISE(ABORT,'Inventory evaluations are immutable'); END;
CREATE TRIGGER sale_inventory_evaluation_complete BEFORE INSERT ON sale_inventory_evaluation
BEGIN
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id AND operation_id=NEW.checkout_operation_id)
 THEN RAISE(ABORT,'Evaluation checkout identity mismatch') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM sale_inventory_line_snapshot WHERE evaluation_id=NEW.id
 AND (sale_id<>NEW.sale_id OR policy_version<>NEW.policy_version))
 THEN RAISE(ABORT,'Snapshot evaluation mismatch') END;
 SELECT CASE WHEN (NEW.status='DISABLED' AND EXISTS(SELECT 1 FROM sale_inventory_line_snapshot WHERE evaluation_id=NEW.id))
 OR (NEW.status<>'DISABLED' AND (SELECT count(*) FROM sale_inventory_line_snapshot WHERE evaluation_id=NEW.id)
 <>(SELECT count(*) FROM sale_item WHERE sale_id=NEW.sale_id))
 THEN RAISE(ABORT,'Incomplete evaluation snapshots') END;
 SELECT CASE WHEN NEW.status='NO_CONSUMPTION' AND EXISTS(SELECT 1 FROM sale_inventory_line_snapshot WHERE evaluation_id=NEW.id AND track_inventory=1)
 THEN RAISE(ABORT,'No consumption evaluation contains consumption') END;
 SELECT CASE WHEN NEW.status='CONSUMED' AND NOT EXISTS(
 SELECT 1 FROM inventory_operation o JOIN sale s ON s.id=NEW.sale_id
 WHERE o.id=NEW.inventory_operation_id AND o.sale_evaluation_id=NEW.id AND o.kind='SALE_CONSUMPTION'
 AND o.state='CONFIRMED' AND o.actor_id=s.user_id AND o.terminal_id=s.terminal_id)
 THEN RAISE(ABORT,'Consumption operation mismatch') END;
 SELECT CASE WHEN NEW.status<>'CONSUMED' AND EXISTS(SELECT 1 FROM inventory_operation WHERE sale_evaluation_id=NEW.id)
 THEN RAISE(ABORT,'Unexpected consumption operation') END;
 SELECT CASE WHEN NEW.status='CONSUMED' AND (
 NOT EXISTS(SELECT 1 FROM sale_inventory_line_snapshot WHERE evaluation_id=NEW.id AND track_inventory=1)
 OR EXISTS(SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.inventory_operation_id AND
 -m.delta_atomic IS NOT (SELECT SUM(l.total_consumed_atomic) FROM sale_inventory_line_snapshot l
 WHERE l.evaluation_id=NEW.id AND l.item_id=m.item_id AND l.location_id=m.location_id))
 OR EXISTS(SELECT 1 FROM sale_inventory_line_snapshot l WHERE l.evaluation_id=NEW.id AND l.track_inventory=1
 AND NOT EXISTS(SELECT 1 FROM inventory_movement m WHERE m.operation_id=NEW.inventory_operation_id
 AND m.item_id=l.item_id AND m.location_id=l.location_id AND m.dimension=l.dimension)))
 THEN RAISE(ABORT,'Consumption does not match snapshots') END;
END;

CREATE TABLE inventory_sale_consumption_config(
 id INTEGER PRIMARY KEY NOT NULL CHECK(id=1),
 state TEXT NOT NULL CHECK(state IN('DISABLED','ENABLED','PAUSED')),
 version INTEGER NOT NULL CHECK(version>=1),
 updated_utc TEXT NOT NULL,
 updated_by TEXT REFERENCES app_user(id) ON DELETE RESTRICT,
 CHECK(state='DISABLED' OR updated_by IS NOT NULL)
) STRICT;
INSERT INTO inventory_sale_consumption_config VALUES(1,'DISABLED',1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),NULL);
CREATE TRIGGER inventory_sale_config_no_delete BEFORE DELETE ON inventory_sale_consumption_config
BEGIN SELECT RAISE(ABORT,'Consumption configuration cannot be deleted'); END;
CREATE TRIGGER inventory_sale_config_transition BEFORE UPDATE ON inventory_sale_consumption_config
WHEN NEW.id<>OLD.id OR NEW.version<>OLD.version+1 OR NEW.updated_by IS NULL
 OR NOT ((OLD.state='DISABLED' AND NEW.state='ENABLED') OR (OLD.state='ENABLED' AND NEW.state='PAUSED')
 OR (OLD.state='PAUSED' AND NEW.state='ENABLED'))
BEGIN SELECT RAISE(ABORT,'Invalid consumption configuration transition'); END;
INSERT INTO permission VALUES('inventory.sale_consumption.configure');
INSERT INTO role_permission VALUES('Administrator','inventory.sale_consumption.configure');
