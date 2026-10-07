-- Charolas a consignación en tiendas cliente (docs/CONSIGNMENT.md).
-- Precio de charola por producto, igual para todas las tiendas. Cada entrega congela el precio de charola y el de mostrador.
-- Al recoger, lo vendido se carga a la cuenta de la tienda y lo que regresa se aparta para hacer Piedra.
-- Los cobros a tiendas no tocan la caja del mostrador. Entregas, recolecciones, anulaciones y cobros son inmutables.
INSERT INTO permission VALUES('consignment.view'),('consignment.manage');
INSERT INTO role_permission VALUES('Administrator','consignment.view'),('Administrator','consignment.manage'),('Manager','consignment.view'),('Manager','consignment.manage');

CREATE TABLE consignment_price(
 product_id TEXT PRIMARY KEY NOT NULL REFERENCES product(id),
 price_cents INTEGER NOT NULL CHECK(price_cents>=0),
 modified_utc TEXT NOT NULL
) STRICT;

CREATE TABLE consignment_store(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 120),
 contact TEXT NOT NULL DEFAULT '' CHECK(length(contact)<=120),
 phone TEXT NOT NULL DEFAULT '' CHECK(length(phone)<=40),
 address TEXT NOT NULL DEFAULT '' CHECK(length(address)<=200),
 active INTEGER NOT NULL DEFAULT 1 CHECK(active IN(0,1)),
 created_utc TEXT NOT NULL,
 modified_utc TEXT NOT NULL
) STRICT;
CREATE UNIQUE INDEX ux_consignment_store_name ON consignment_store(catalog_key_v1(name));
CREATE TRIGGER consignment_store_no_delete BEFORE DELETE ON consignment_store
BEGIN SELECT RAISE(ABORT,'Stores are deactivated, not deleted'); END;

-- Charola habitual de cada tienda: se propone al entregar para no capturar todo cada día.
CREATE TABLE consignment_template_item(
 store_id TEXT NOT NULL REFERENCES consignment_store(id),
 product_id TEXT NOT NULL REFERENCES product(id),
 quantity INTEGER NOT NULL CHECK(quantity BETWEEN 1 AND 999),
 PRIMARY KEY(store_id,product_id)
) STRICT;

CREATE TABLE consignment_delivery(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 store_id TEXT NOT NULL REFERENCES consignment_store(id),
 terminal_id TEXT NOT NULL REFERENCES terminal(id),
 delivered_by TEXT NOT NULL REFERENCES app_user(id),
 delivered_utc TEXT NOT NULL,
 note TEXT NOT NULL DEFAULT '' CHECK(length(note)<=200)
) STRICT;
CREATE INDEX ix_consignment_delivery_store ON consignment_delivery(store_id,delivered_utc);
CREATE INDEX ix_consignment_delivery_date ON consignment_delivery(delivered_utc);

CREATE TABLE consignment_delivery_item(
 delivery_id TEXT NOT NULL REFERENCES consignment_delivery(id) DEFERRABLE INITIALLY DEFERRED,
 position INTEGER NOT NULL CHECK(position>=1),
 product_id TEXT NOT NULL REFERENCES product(id),
 product_name TEXT NOT NULL CHECK(length(trim(product_name))>0),
 unit_price_cents INTEGER NOT NULL CHECK(unit_price_cents>=0),
 retail_price_cents INTEGER NOT NULL CHECK(retail_price_cents>=0),
 quantity INTEGER NOT NULL CHECK(quantity BETWEEN 1 AND 999),
 PRIMARY KEY(delivery_id,position),
 UNIQUE(delivery_id,product_id)
) STRICT;

-- Las partidas se insertan antes del encabezado; el encabezado valida el conjunto.
CREATE TRIGGER consignment_delivery_item_valid BEFORE INSERT ON consignment_delivery_item
WHEN EXISTS(SELECT 1 FROM consignment_delivery WHERE id=NEW.delivery_id)
BEGIN SELECT RAISE(ABORT,'Invalid consignment delivery item'); END;
CREATE TRIGGER consignment_delivery_complete BEFORE INSERT ON consignment_delivery
WHEN NOT EXISTS(SELECT 1 FROM consignment_delivery_item WHERE delivery_id=NEW.id)
 OR NOT EXISTS(SELECT 1 FROM consignment_store WHERE id=NEW.store_id AND active=1)
BEGIN SELECT RAISE(ABORT,'Invalid consignment delivery'); END;

-- Anular sólo una entrega que no se ha recogido (captura equivocada).
CREATE TABLE consignment_void(
 delivery_id TEXT PRIMARY KEY NOT NULL REFERENCES consignment_delivery(id),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 voided_by TEXT NOT NULL REFERENCES app_user(id),
 voided_utc TEXT NOT NULL,
 reason TEXT NOT NULL CHECK(length(trim(reason)) BETWEEN 3 AND 200)
) STRICT;

CREATE TABLE consignment_settlement(
 delivery_id TEXT PRIMARY KEY NOT NULL REFERENCES consignment_delivery(id),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 collected_by TEXT NOT NULL REFERENCES app_user(id),
 collected_utc TEXT NOT NULL,
 sold_cents INTEGER NOT NULL CHECK(sold_cents>=0),
 note TEXT NOT NULL DEFAULT '' CHECK(length(note)<=200)
) STRICT;
CREATE TABLE consignment_settlement_item(
 delivery_id TEXT NOT NULL REFERENCES consignment_settlement(delivery_id) DEFERRABLE INITIALLY DEFERRED,
 position INTEGER NOT NULL,
 sold INTEGER NOT NULL CHECK(sold>=0),
 returned INTEGER NOT NULL CHECK(returned>=0),
 line_total_cents INTEGER NOT NULL CHECK(line_total_cents>=0),
 PRIMARY KEY(delivery_id,position),
 FOREIGN KEY(delivery_id,position) REFERENCES consignment_delivery_item(delivery_id,position)
) STRICT;

-- Cada partida: vendidas + regresadas = entregadas; importe = vendidas × precio de charola congelado.
CREATE TRIGGER consignment_settlement_item_valid BEFORE INSERT ON consignment_settlement_item
WHEN EXISTS(SELECT 1 FROM consignment_settlement WHERE delivery_id=NEW.delivery_id)
 OR NOT EXISTS(SELECT 1 FROM consignment_delivery_item i WHERE i.delivery_id=NEW.delivery_id AND i.position=NEW.position
     AND NEW.sold+NEW.returned=i.quantity AND NEW.line_total_cents=i.unit_price_cents*NEW.sold)
BEGIN SELECT RAISE(ABORT,'Invalid consignment settlement item'); END;
-- Encabezado: entrega vigente y anterior a la recolección, todas sus partidas liquidadas y total coherente.
CREATE TRIGGER consignment_settlement_complete BEFORE INSERT ON consignment_settlement
WHEN EXISTS(SELECT 1 FROM consignment_void WHERE delivery_id=NEW.delivery_id)
 OR NOT EXISTS(SELECT 1 FROM consignment_delivery WHERE id=NEW.delivery_id AND delivered_utc<=NEW.collected_utc)
 OR (SELECT count(*) FROM consignment_settlement_item WHERE delivery_id=NEW.delivery_id)<>(SELECT count(*) FROM consignment_delivery_item WHERE delivery_id=NEW.delivery_id)
 OR NEW.sold_cents<>(SELECT COALESCE(SUM(line_total_cents),0) FROM consignment_settlement_item WHERE delivery_id=NEW.delivery_id)
BEGIN SELECT RAISE(ABORT,'Invalid consignment settlement'); END;
CREATE TRIGGER consignment_void_valid BEFORE INSERT ON consignment_void
WHEN EXISTS(SELECT 1 FROM consignment_settlement WHERE delivery_id=NEW.delivery_id)
 OR NOT EXISTS(SELECT 1 FROM consignment_delivery WHERE id=NEW.delivery_id AND delivered_utc<=NEW.voided_utc)
BEGIN SELECT RAISE(ABORT,'Settled deliveries cannot be voided'); END;

-- Cobros a la cuenta de la tienda. No forman parte del efectivo esperado de la caja.
CREATE TABLE consignment_payment(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 store_id TEXT NOT NULL REFERENCES consignment_store(id),
 received_by TEXT NOT NULL REFERENCES app_user(id),
 received_utc TEXT NOT NULL,
 amount_cents INTEGER NOT NULL CHECK(amount_cents>0),
 method TEXT NOT NULL CHECK(method IN('CASH','CARD','TRANSFER')),
 reference TEXT NULL CHECK(reference IS NULL OR length(trim(reference)) BETWEEN 1 AND 60),
 note TEXT NOT NULL DEFAULT '' CHECK(length(note)<=200)
) STRICT;
CREATE INDEX ix_consignment_payment_store ON consignment_payment(store_id,received_utc);

CREATE TRIGGER consignment_delivery_no_update BEFORE UPDATE ON consignment_delivery BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_delivery_no_delete BEFORE DELETE ON consignment_delivery BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_delivery_item_no_update BEFORE UPDATE ON consignment_delivery_item BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_delivery_item_no_delete BEFORE DELETE ON consignment_delivery_item BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_void_no_update BEFORE UPDATE ON consignment_void BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_void_no_delete BEFORE DELETE ON consignment_void BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_settlement_no_update BEFORE UPDATE ON consignment_settlement BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_settlement_no_delete BEFORE DELETE ON consignment_settlement BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_settlement_item_no_update BEFORE UPDATE ON consignment_settlement_item BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_settlement_item_no_delete BEFORE DELETE ON consignment_settlement_item BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_payment_no_update BEFORE UPDATE ON consignment_payment BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
CREATE TRIGGER consignment_payment_no_delete BEFORE DELETE ON consignment_payment BEGIN SELECT RAISE(ABORT,'Consignment records are immutable'); END;
