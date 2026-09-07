INSERT INTO permission VALUES('sales.complete');
INSERT INTO role_permission VALUES('Administrator','sales.complete'),('Manager','sales.complete'),('Cashier','sales.complete');
CREATE TABLE sale_sequence(
 terminal_id TEXT PRIMARY KEY NOT NULL REFERENCES terminal(id),
 last_value INTEGER NOT NULL CHECK(last_value>=1)
) STRICT;
CREATE TABLE sale(
 id TEXT PRIMARY KEY NOT NULL,
 operation_id TEXT NOT NULL UNIQUE,
 user_id TEXT NOT NULL REFERENCES app_user(id),
 terminal_id TEXT NOT NULL REFERENCES terminal(id),
 cash_session_id TEXT NOT NULL REFERENCES cash_session(id),
 sequence INTEGER NOT NULL CHECK(sequence>=1),
 confirmed_utc TEXT NOT NULL,
 subtotal_cents INTEGER NOT NULL CHECK(subtotal_cents>=0),
 total_cents INTEGER NOT NULL CHECK(total_cents=subtotal_cents),
 UNIQUE(terminal_id,sequence)
) STRICT;
CREATE TABLE sale_item(
 sale_id TEXT NOT NULL REFERENCES sale(id) DEFERRABLE INITIALLY DEFERRED,
 position INTEGER NOT NULL CHECK(position>=1),
 product_id TEXT NOT NULL REFERENCES product(id),
 product_name TEXT NOT NULL CHECK(length(trim(product_name))>0),
 unit_price_cents INTEGER NOT NULL CHECK(unit_price_cents>=0),
 quantity INTEGER NOT NULL CHECK(quantity BETWEEN 1 AND 2147483647),
 line_total_cents INTEGER NOT NULL CHECK(line_total_cents>=0),
 PRIMARY KEY(sale_id,position), UNIQUE(sale_id,product_id),
 CHECK(CASE WHEN unit_price_cents=0 THEN line_total_cents=0
 WHEN quantity>9223372036854775807/unit_price_cents THEN 0
 ELSE line_total_cents=unit_price_cents*quantity END)
) STRICT;
CREATE TABLE payment(
 id TEXT PRIMARY KEY NOT NULL,
 sale_id TEXT NOT NULL UNIQUE REFERENCES sale(id) DEFERRABLE INITIALLY DEFERRED,
 method TEXT NOT NULL CHECK(method='CASH'),
 applied_cents INTEGER NOT NULL CHECK(applied_cents>=0),
 received_cents INTEGER NOT NULL CHECK(received_cents>=applied_cents),
 change_cents INTEGER NOT NULL CHECK(change_cents=received_cents-applied_cents)
) STRICT;
CREATE INDEX ix_sale_cash ON sale(cash_session_id);
CREATE INDEX ix_sale_actor ON sale(user_id,confirmed_utc);
CREATE INDEX ix_sale_product ON sale_item(product_id);
CREATE UNIQUE INDEX ux_audit_sale_completed ON audit_entry(entity_id) WHERE action='sale.completed';
-- Children are inserted before the header in one explicit transaction. Deferred FKs
-- prohibit orphan commits; the final header validates the complete commercial aggregate.
CREATE TRIGGER sale_complete BEFORE INSERT ON sale BEGIN
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM cash_session c WHERE c.id=NEW.cash_session_id AND c.terminal_id=NEW.terminal_id AND c.state='OPEN' AND c.opened_utc<=NEW.confirmed_utc)
 OR EXISTS(SELECT 1 FROM cash_count WHERE session_id=NEW.cash_session_id)
 THEN RAISE(ABORT,'Sale requires open uncounted cash session') END;
 SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM sale_item WHERE sale_id=NEW.id)
 OR (SELECT SUM(line_total_cents) FROM sale_item WHERE sale_id=NEW.id)<>NEW.total_cents
 OR NOT EXISTS(SELECT 1 FROM payment WHERE sale_id=NEW.id AND applied_cents=NEW.total_cents)
 THEN RAISE(ABORT,'Incomplete sale') END;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM sale_item i JOIN product p ON p.id=i.product_id JOIN category c ON c.id=p.category_id WHERE i.sale_id=NEW.id AND (p.active<>1 OR c.active<>1))
 THEN RAISE(ABORT,'Inactive product in sale') END;
END;
CREATE TRIGGER sale_no_update BEFORE UPDATE ON sale BEGIN SELECT RAISE(ABORT,'Sales are immutable'); END;
CREATE TRIGGER sale_no_delete BEFORE DELETE ON sale BEGIN SELECT RAISE(ABORT,'Sales are immutable'); END;
CREATE TRIGGER sale_item_no_update BEFORE UPDATE ON sale_item BEGIN SELECT RAISE(ABORT,'Sale items are immutable'); END;
CREATE TRIGGER sale_item_no_delete BEFORE DELETE ON sale_item BEGIN SELECT RAISE(ABORT,'Sale items are immutable'); END;
CREATE TRIGGER sale_item_no_append BEFORE INSERT ON sale_item WHEN EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id)
BEGIN SELECT RAISE(ABORT,'Cannot append to completed sale'); END;
CREATE TRIGGER payment_no_update BEFORE UPDATE ON payment BEGIN SELECT RAISE(ABORT,'Payments are immutable'); END;
CREATE TRIGGER payment_no_delete BEFORE DELETE ON payment BEGIN SELECT RAISE(ABORT,'Payments are immutable'); END;
CREATE TRIGGER payment_no_append BEFORE INSERT ON payment WHEN EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id)
BEGIN SELECT RAISE(ABORT,'Cannot append to completed sale'); END;
