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
CREATE TABLE inventory_movement_010_backup AS SELECT * FROM inventory_movement;
CREATE TABLE inventory_operation_010(
 id TEXT PRIMARY KEY NOT NULL,
 request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64 AND request_sha256 NOT GLOB '*[^0-9A-F]*'),
 kind TEXT NOT NULL CHECK(kind IN('OPENING_BALANCE','RECEIPT','ADJUSTMENT_GAIN','ADJUSTMENT_LOSS','WASTE')),
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
   OR (kind='OPENING_BALANCE' AND reason_code IS NULL AND note IS NULL) OR (kind='RECEIPT' AND reason_code IS NULL)
 ),
 CHECK(reason_code<>'OTHER' OR note IS NOT NULL)
) STRICT;
INSERT INTO inventory_operation_010 SELECT * FROM inventory_operation;
DROP TABLE inventory_movement;
DROP TABLE inventory_operation;
ALTER TABLE inventory_operation_010 RENAME TO inventory_operation;
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
INSERT INTO inventory_movement SELECT * FROM inventory_movement_010_backup;
DROP TABLE inventory_movement_010_backup;
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
    OR (OLD.kind IN('ADJUSTMENT_LOSS','WASTE') AND (m.is_opening<>0 OR m.delta_atomic>=0))
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