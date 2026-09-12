CREATE TABLE measurement_unit(
 code TEXT PRIMARY KEY NOT NULL CHECK(code IN('mg','g','kg','uL','ml','L','piece')),
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 symbol TEXT NOT NULL CHECK(length(symbol) BETWEEN 1 AND 10),
 atomic_units_per_unit INTEGER NOT NULL CHECK(atomic_units_per_unit>0)
) STRICT;
INSERT INTO measurement_unit VALUES
 ('mg','MASS','mg',1),('g','MASS','g',1000),('kg','MASS','kg',1000000),
 ('uL','VOLUME','µL',1),('ml','VOLUME','ml',1000),('L','VOLUME','L',1000000),
 ('piece','COUNT','pieza',1);

CREATE TABLE inventory_item(
 id TEXT PRIMARY KEY NOT NULL,
 code TEXT NOT NULL CHECK(length(trim(code)) BETWEEN 1 AND 80),
 name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 160),
 kind TEXT NOT NULL CHECK(kind IN('RAW_MATERIAL','FINISHED_GOOD','PACKAGING','SUPPLY')),
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 active INTEGER NOT NULL CHECK(active IN(0,1)),
 created_utc TEXT NOT NULL,
 modified_utc TEXT NOT NULL,
 UNIQUE(id,dimension)
) STRICT;
CREATE UNIQUE INDEX ux_inventory_item_code ON inventory_item(catalog_key_v1(code));
CREATE INDEX ix_inventory_item_list ON inventory_item(active,kind,name COLLATE CATALOG_KEY_V1,id);

CREATE TABLE inventory_item_unit(
 id TEXT PRIMARY KEY NOT NULL,
 item_id TEXT NOT NULL,
 code TEXT NOT NULL CHECK(length(trim(code)) BETWEEN 1 AND 80),
 name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 100),
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 atomic_units_per_whole_unit INTEGER NOT NULL CHECK(atomic_units_per_whole_unit>0),
 active INTEGER NOT NULL CHECK(active IN(0,1)),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT,
 UNIQUE(item_id,code)
) STRICT;
CREATE INDEX ix_inventory_item_unit_item ON inventory_item_unit(item_id,active);
CREATE UNIQUE INDEX ux_inventory_item_unit_code ON inventory_item_unit(item_id,catalog_key_v1(code));

CREATE TABLE inventory_location(
 id TEXT PRIMARY KEY NOT NULL,
 code TEXT NOT NULL UNIQUE CHECK(length(trim(code)) BETWEEN 1 AND 40),
 name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 100),
 active INTEGER NOT NULL CHECK(active IN(0,1))
) STRICT;
INSERT INTO inventory_location VALUES('10000000-0000-0000-0000-000000000001','MAIN','Almacén principal',1);

CREATE TABLE inventory_operation(
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
   OR (kind IN('OPENING_BALANCE','RECEIPT') AND reason_code IS NULL AND note IS NULL)
 ),
 CHECK(reason_code<>'OTHER' OR note IS NOT NULL)
) STRICT;
CREATE INDEX ix_inventory_operation_time ON inventory_operation(confirmed_utc,id);
CREATE INDEX ix_inventory_operation_actor ON inventory_operation(actor_id,confirmed_utc);

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
CREATE UNIQUE INDEX ux_inventory_opening ON inventory_movement(item_id,location_id) WHERE is_opening=1;
CREATE INDEX ix_inventory_movement_item ON inventory_movement(item_id,location_id,operation_id);

CREATE TABLE inventory_balance(
 item_id TEXT NOT NULL,
 location_id TEXT NOT NULL REFERENCES inventory_location(id) ON DELETE RESTRICT,
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 quantity_atomic INTEGER NOT NULL CHECK(quantity_atomic>=0),
 version INTEGER NOT NULL CHECK(version>=1),
 updated_utc TEXT NOT NULL,
 PRIMARY KEY(item_id,location_id),
 FOREIGN KEY(item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT
) STRICT;
CREATE INDEX ix_inventory_balance_location ON inventory_balance(location_id,item_id);

CREATE TABLE product_inventory_link(
 id TEXT PRIMARY KEY NOT NULL,
 product_id TEXT NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
 inventory_item_id TEXT NOT NULL,
 dimension TEXT NOT NULL CHECK(dimension IN('MASS','VOLUME','COUNT')),
 quantity_per_product_unit_atomic INTEGER NOT NULL CHECK(quantity_per_product_unit_atomic>0),
 active INTEGER NOT NULL CHECK(active IN(0,1)),
 created_utc TEXT NOT NULL,
 modified_utc TEXT NOT NULL,
 FOREIGN KEY(inventory_item_id,dimension) REFERENCES inventory_item(id,dimension) ON DELETE RESTRICT
) STRICT;
CREATE UNIQUE INDEX ux_product_inventory_active ON product_inventory_link(product_id) WHERE active=1;
CREATE INDEX ix_product_inventory_item ON product_inventory_link(inventory_item_id,active);

CREATE TRIGGER inventory_item_no_delete BEFORE DELETE ON inventory_item
BEGIN SELECT RAISE(ABORT,'Deactivate inventory items instead of deleting'); END;
CREATE TRIGGER measurement_unit_no_update BEFORE UPDATE ON measurement_unit
BEGIN SELECT RAISE(ABORT,'Measurement units are immutable'); END;
CREATE TRIGGER measurement_unit_no_delete BEFORE DELETE ON measurement_unit
BEGIN SELECT RAISE(ABORT,'Measurement units are immutable'); END;
CREATE TRIGGER inventory_item_dimension_immutable BEFORE UPDATE OF dimension ON inventory_item
WHEN NEW.dimension<>OLD.dimension
BEGIN SELECT RAISE(ABORT,'Inventory item dimension is immutable'); END;
CREATE TRIGGER inventory_item_nonzero_deactivate BEFORE UPDATE OF active ON inventory_item
WHEN NEW.active=0 AND EXISTS(SELECT 1 FROM inventory_balance WHERE item_id=OLD.id AND quantity_atomic<>0)
BEGIN SELECT RAISE(ABORT,'Inventory item has stock'); END;
CREATE TRIGGER inventory_item_unit_no_delete BEFORE DELETE ON inventory_item_unit
BEGIN SELECT RAISE(ABORT,'Deactivate inventory units instead of deleting'); END;
CREATE TRIGGER inventory_item_unit_reserved_code BEFORE INSERT ON inventory_item_unit
WHEN EXISTS(SELECT 1 FROM measurement_unit WHERE code=NEW.code)
BEGIN SELECT RAISE(ABORT,'Item unit code conflicts with a standard unit'); END;
CREATE TRIGGER inventory_location_no_delete BEFORE DELETE ON inventory_location
BEGIN SELECT RAISE(ABORT,'Deactivate inventory locations instead of deleting'); END;
CREATE TRIGGER product_inventory_link_no_delete BEFORE DELETE ON product_inventory_link
BEGIN SELECT RAISE(ABORT,'Deactivate product inventory links instead of deleting'); END;

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

CREATE UNIQUE INDEX ux_audit_inventory_operation ON audit_entry(entity_id)
 WHERE action='inventory.operation.confirmed';

INSERT INTO permission VALUES('inventory.view'),('inventory.configure'),('inventory.receive'),('inventory.adjust'),('inventory.waste');
INSERT INTO role_permission VALUES
 ('Administrator','inventory.view'),('Administrator','inventory.configure'),('Administrator','inventory.receive'),
 ('Administrator','inventory.adjust'),('Administrator','inventory.waste'),
 ('Manager','inventory.view'),('Manager','inventory.receive'),('Manager','inventory.waste');
