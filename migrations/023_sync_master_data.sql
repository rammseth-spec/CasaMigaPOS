-- Versión web (docs/WEB.md): la cola de sincronización también sube catálogo con fotos, usuarios, permisos,
-- movimientos de caja, charolas, inventario, recetas y producción. Cada entrada sólo dice qué registro cambió;
-- el contenido se lee al enviar, así que lo que llega a la nube es siempre el estado más reciente.
-- Un hijo (renglón de charola habitual, unidad de insumo, ingrediente…) encola a su padre.
DROP TRIGGER sync_cash_session_insert;
DROP TRIGGER sync_cash_session_update;
DROP TRIGGER sync_cash_count_insert;
DROP TRIGGER sync_sale_insert;
DROP TRIGGER sync_sale_refund_insert;
DROP TRIGGER sync_audit_entry_insert;
DROP TRIGGER sync_outbox_no_delete_pending;
CREATE TABLE sync_outbox_v23(
 seq INTEGER PRIMARY KEY AUTOINCREMENT,
 entity TEXT NOT NULL CHECK(entity IN ('cash_session','cash_count','sale','sale_refund','audit_entry',
   'branch','app_user','role_permission','category','product','product_photo','cash_movement',
   'consignment_store','consignment_price','consignment_delivery','consignment_payment',
   'measurement_unit','inventory_location','inventory_item','inventory_operation',
   'formula','production_profile','production_batch')),
 entity_id TEXT NOT NULL CHECK(length(entity_id)>0),
 change TEXT NOT NULL CHECK(change IN ('insert','update','delete')),
 enqueued_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 sent_utc TEXT,
 attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts>=0),
 last_error TEXT
) STRICT;
INSERT INTO sync_outbox_v23(seq,entity,entity_id,change,enqueued_utc,sent_utc,attempts,last_error)
SELECT seq,entity,entity_id,change,enqueued_utc,sent_utc,attempts,last_error FROM sync_outbox;
DROP TABLE sync_outbox;
ALTER TABLE sync_outbox_v23 RENAME TO sync_outbox;
CREATE INDEX ix_sync_outbox_pending ON sync_outbox(seq) WHERE sent_utc IS NULL;
CREATE TRIGGER sync_outbox_no_delete_pending BEFORE DELETE ON sync_outbox
WHEN OLD.sent_utc IS NULL
BEGIN SELECT RAISE(ABORT,'Pending sync entries cannot be deleted'); END;

-- Operación comercial (igual que 019).
CREATE TRIGGER sync_cash_session_insert AFTER INSERT ON cash_session
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_session',NEW.id,'insert'); END;
CREATE TRIGGER sync_cash_session_update AFTER UPDATE ON cash_session
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_session',NEW.id,'update'); END;
CREATE TRIGGER sync_cash_count_insert AFTER INSERT ON cash_count
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_count',NEW.session_id,'insert'); END;
CREATE TRIGGER sync_sale_insert AFTER INSERT ON sale
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('sale',NEW.id,'insert'); END;
CREATE TRIGGER sync_sale_refund_insert AFTER INSERT ON sale_refund
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('sale_refund',NEW.id,'insert'); END;
CREATE TRIGGER sync_audit_entry_insert AFTER INSERT ON audit_entry
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('audit_entry',NEW.id,'insert'); END;
CREATE TRIGGER sync_cash_movement_insert AFTER INSERT ON cash_movement
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_movement',NEW.id,'insert'); END;

-- Sucursal, usuarios y permisos. Iniciar sesión (intentos, bloqueo) no encola.
CREATE TRIGGER sync_branch_update AFTER UPDATE OF name ON branch
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('branch',NEW.id,'update'); END;
CREATE TRIGGER sync_app_user_insert AFTER INSERT ON app_user
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('app_user',NEW.id,'insert'); END;
CREATE TRIGGER sync_app_user_update AFTER UPDATE OF username,display_name,password_hash,role_name,active,must_change_password ON app_user
WHEN OLD.username IS NOT NEW.username OR OLD.display_name IS NOT NEW.display_name OR OLD.password_hash IS NOT NEW.password_hash
  OR OLD.role_name IS NOT NEW.role_name OR OLD.active IS NOT NEW.active OR OLD.must_change_password IS NOT NEW.must_change_password
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('app_user',NEW.id,'update'); END;
CREATE TRIGGER sync_role_permission_insert AFTER INSERT ON role_permission
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('role_permission','all','update'); END;
CREATE TRIGGER sync_role_permission_delete AFTER DELETE ON role_permission
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('role_permission','all','update'); END;

-- Catálogo y fotos.
CREATE TRIGGER sync_category_insert AFTER INSERT ON category
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('category',NEW.id,'insert'); END;
CREATE TRIGGER sync_category_update AFTER UPDATE ON category
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('category',NEW.id,'update'); END;
CREATE TRIGGER sync_product_insert AFTER INSERT ON product
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product',NEW.id,'insert'); END;
CREATE TRIGGER sync_product_update AFTER UPDATE ON product
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product',NEW.id,'update'); END;
CREATE TRIGGER sync_product_link_insert AFTER INSERT ON product_inventory_link
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product',NEW.product_id,'update'); END;
CREATE TRIGGER sync_product_link_update AFTER UPDATE ON product_inventory_link
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product',NEW.product_id,'update'); END;
CREATE TRIGGER sync_product_photo_insert AFTER INSERT ON product_photo
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product_photo',NEW.product_id,'insert'); END;
CREATE TRIGGER sync_product_photo_update AFTER UPDATE ON product_photo
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('product_photo',NEW.product_id,'update'); END;

-- Charolas.
CREATE TRIGGER sync_consignment_store_insert AFTER INSERT ON consignment_store
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_store',NEW.id,'insert'); END;
CREATE TRIGGER sync_consignment_store_update AFTER UPDATE ON consignment_store
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_store',NEW.id,'update'); END;
CREATE TRIGGER sync_consignment_template_insert AFTER INSERT ON consignment_template_item
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_store',NEW.store_id,'update'); END;
CREATE TRIGGER sync_consignment_template_update AFTER UPDATE ON consignment_template_item
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_store',NEW.store_id,'update'); END;
CREATE TRIGGER sync_consignment_template_delete AFTER DELETE ON consignment_template_item
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_store',OLD.store_id,'update'); END;
CREATE TRIGGER sync_consignment_price_insert AFTER INSERT ON consignment_price
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_price',NEW.product_id,'insert'); END;
CREATE TRIGGER sync_consignment_price_update AFTER UPDATE ON consignment_price
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_price',NEW.product_id,'update'); END;
CREATE TRIGGER sync_consignment_price_delete AFTER DELETE ON consignment_price
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_price',OLD.product_id,'delete'); END;
CREATE TRIGGER sync_consignment_delivery_insert AFTER INSERT ON consignment_delivery
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_delivery',NEW.id,'insert'); END;
CREATE TRIGGER sync_consignment_settlement_insert AFTER INSERT ON consignment_settlement
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_delivery',NEW.delivery_id,'update'); END;
CREATE TRIGGER sync_consignment_void_insert AFTER INSERT ON consignment_void
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_delivery',NEW.delivery_id,'update'); END;
CREATE TRIGGER sync_consignment_payment_insert AFTER INSERT ON consignment_payment
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('consignment_payment',NEW.id,'insert'); END;

-- Inventario.
CREATE TRIGGER sync_measurement_unit_insert AFTER INSERT ON measurement_unit
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('measurement_unit','all','update'); END;
CREATE TRIGGER sync_inventory_location_insert AFTER INSERT ON inventory_location
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_location',NEW.id,'insert'); END;
CREATE TRIGGER sync_inventory_location_update AFTER UPDATE ON inventory_location
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_location',NEW.id,'update'); END;
CREATE TRIGGER sync_inventory_item_insert AFTER INSERT ON inventory_item
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.id,'insert'); END;
CREATE TRIGGER sync_inventory_item_update AFTER UPDATE ON inventory_item
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.id,'update'); END;
CREATE TRIGGER sync_inventory_unit_insert AFTER INSERT ON inventory_item_unit
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
CREATE TRIGGER sync_inventory_unit_update AFTER UPDATE ON inventory_item_unit
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
CREATE TRIGGER sync_inventory_balance_insert AFTER INSERT ON inventory_balance
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
CREATE TRIGGER sync_inventory_balance_update AFTER UPDATE ON inventory_balance
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_item',NEW.item_id,'update'); END;
CREATE TRIGGER sync_inventory_operation_insert AFTER INSERT ON inventory_operation
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_operation',NEW.id,'insert'); END;
CREATE TRIGGER sync_inventory_operation_update AFTER UPDATE ON inventory_operation
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('inventory_operation',NEW.id,'update'); END;

-- Recetas (fórmulas compartidas y perfiles de producto) y producción.
CREATE TRIGGER sync_formula_insert AFTER INSERT ON formula
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('formula',NEW.id,'insert'); END;
CREATE TRIGGER sync_formula_update AFTER UPDATE ON formula
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('formula',NEW.id,'update'); END;
CREATE TRIGGER sync_formula_version_insert AFTER INSERT ON formula_version
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('formula',NEW.formula_id,'update'); END;
CREATE TRIGGER sync_formula_version_update AFTER UPDATE ON formula_version
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('formula',NEW.formula_id,'update'); END;
CREATE TRIGGER sync_formula_ingredient_insert AFTER INSERT ON formula_ingredient
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=NEW.version_id; END;
CREATE TRIGGER sync_formula_ingredient_delete AFTER DELETE ON formula_ingredient
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=OLD.version_id; END;
CREATE TRIGGER sync_formula_group_insert AFTER INSERT ON formula_alternative_group
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=NEW.version_id; END;
CREATE TRIGGER sync_formula_group_delete AFTER DELETE ON formula_alternative_group
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=OLD.version_id; END;
CREATE TRIGGER sync_formula_option_insert AFTER INSERT ON formula_alternative_option
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=NEW.version_id; END;
CREATE TRIGGER sync_formula_option_delete AFTER DELETE ON formula_alternative_option
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',formula_id,'update' FROM formula_version WHERE id=OLD.version_id; END;
CREATE TRIGGER sync_production_profile_insert AFTER INSERT ON production_profile
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_profile',NEW.id,'insert'); END;
CREATE TRIGGER sync_production_profile_update AFTER UPDATE ON production_profile
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_profile',NEW.id,'update'); END;
CREATE TRIGGER sync_profile_version_insert AFTER INSERT ON production_profile_version
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_profile',NEW.profile_id,'update'); END;
CREATE TRIGGER sync_profile_version_update AFTER UPDATE ON production_profile_version
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_profile',NEW.profile_id,'update'); END;
CREATE TRIGGER sync_profile_ingredient_insert AFTER INSERT ON profile_direct_ingredient
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_profile',profile_id,'update' FROM production_profile_version WHERE id=NEW.profile_version_id; END;
CREATE TRIGGER sync_profile_ingredient_delete AFTER DELETE ON profile_direct_ingredient
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_profile',profile_id,'update' FROM production_profile_version WHERE id=OLD.profile_version_id; END;
CREATE TRIGGER sync_profile_requirement_insert AFTER INSERT ON profile_formula_requirement
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_profile',profile_id,'update' FROM production_profile_version WHERE id=NEW.profile_version_id; END;
CREATE TRIGGER sync_profile_requirement_delete AFTER DELETE ON profile_formula_requirement
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_profile',profile_id,'update' FROM production_profile_version WHERE id=OLD.profile_version_id; END;
CREATE TRIGGER sync_production_batch_insert AFTER INSERT ON production_batch
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_batch',NEW.id,'insert'); END;
CREATE TRIGGER sync_production_batch_update AFTER UPDATE ON production_batch
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_batch',NEW.id,'update'); END;
CREATE TRIGGER sync_production_output_insert AFTER INSERT ON production_output_plan
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_batch',NEW.batch_id,'update'); END;
CREATE TRIGGER sync_production_output_update AFTER UPDATE ON production_output_plan
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('production_batch',NEW.batch_id,'update'); END;

-- Lo que ya existe se sube una vez, en orden de dependencia.
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'branch',id,'insert' FROM branch;
INSERT INTO sync_outbox(entity,entity_id,change) VALUES('role_permission','all','insert');
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'app_user',id,'insert' FROM app_user ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'category',id,'insert' FROM category ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'product',id,'insert' FROM product ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'product_photo',product_id,'insert' FROM product_photo ORDER BY modified_utc,product_id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'cash_movement',id,'insert' FROM cash_movement ORDER BY occurred_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'consignment_store',id,'insert' FROM consignment_store ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'consignment_price',product_id,'insert' FROM consignment_price ORDER BY modified_utc,product_id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'consignment_delivery',id,'insert' FROM consignment_delivery ORDER BY delivered_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'consignment_payment',id,'insert' FROM consignment_payment ORDER BY received_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) VALUES('measurement_unit','all','insert');
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'inventory_location',id,'insert' FROM inventory_location ORDER BY id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'inventory_item',id,'insert' FROM inventory_item ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'inventory_operation',id,'insert' FROM inventory_operation ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'formula',id,'insert' FROM formula ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_profile',id,'insert' FROM production_profile ORDER BY created_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'production_batch',id,'insert' FROM production_batch ORDER BY created_utc,id;
