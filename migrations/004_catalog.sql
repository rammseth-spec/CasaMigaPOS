ALTER TABLE category ADD COLUMN description TEXT NOT NULL DEFAULT '';
ALTER TABLE category ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;
ALTER TABLE category ADD COLUMN created_utc TEXT NOT NULL DEFAULT '';
ALTER TABLE category ADD COLUMN modified_utc TEXT NOT NULL DEFAULT '';
UPDATE category SET created_utc=(SELECT applied_utc FROM schema_migration WHERE version=1),
 modified_utc=(SELECT applied_utc FROM schema_migration WHERE version=1);
CREATE UNIQUE INDEX ux_category_name ON category(catalog_key_v1(name));
CREATE UNIQUE INDEX ux_product_sku_normalized ON product(catalog_key_v1(sku)) WHERE sku IS NOT NULL;
CREATE INDEX ix_category_order ON category(active,sort_order,name COLLATE CATALOG_KEY_V1);
CREATE INDEX ix_product_active_name ON product(active,name COLLATE CATALOG_KEY_V1,id);
CREATE TRIGGER category_keep_history BEFORE DELETE ON category
BEGIN SELECT RAISE(ABORT,'Deactivate categories instead of deleting'); END;
CREATE TRIGGER product_keep_history BEFORE DELETE ON product
BEGIN SELECT RAISE(ABORT,'Deactivate products instead of deleting'); END;
CREATE TRIGGER category_active_children BEFORE UPDATE OF active ON category
WHEN NEW.active=0 AND EXISTS(SELECT 1 FROM product WHERE category_id=OLD.id AND active=1)
BEGIN SELECT RAISE(ABORT,'Category contains active products'); END;
CREATE TRIGGER product_active_category_insert BEFORE INSERT ON product
WHEN NEW.active=1 AND NOT EXISTS(SELECT 1 FROM category WHERE id=NEW.category_id AND active=1)
BEGIN SELECT RAISE(ABORT,'Active product requires active category'); END;
CREATE TRIGGER product_active_category_update BEFORE UPDATE ON product
WHEN NEW.active=1 AND NOT EXISTS(SELECT 1 FROM category WHERE id=NEW.category_id AND active=1)
BEGIN SELECT RAISE(ABORT,'Active product requires active category'); END;
INSERT INTO permission VALUES('catalog.view'),('categories.create'),('categories.edit'),('categories.deactivate'),
 ('products.create'),('products.edit'),('products.deactivate');
INSERT INTO role_permission SELECT 'Administrator',name FROM permission WHERE name='catalog.view' OR name LIKE 'categories.%' OR name LIKE 'products.%';
INSERT INTO role_permission SELECT 'Manager',name FROM permission WHERE name='catalog.view' OR name LIKE 'categories.%' OR name LIKE 'products.%';
INSERT INTO role_permission VALUES('Cashier','catalog.view');
-- Refuse pre-existing incoherent data rather than silently cascading or inventing corrections.
CREATE TABLE catalog_migration_guard(value INTEGER NOT NULL CHECK(value=0)) STRICT;
INSERT INTO catalog_migration_guard SELECT 1 WHERE EXISTS(SELECT 1 FROM product p JOIN category c ON c.id=p.category_id WHERE p.active=1 AND c.active=0);
DROP TABLE catalog_migration_guard;

