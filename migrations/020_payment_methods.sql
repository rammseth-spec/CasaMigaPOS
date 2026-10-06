-- Formas de pago: efectivo, tarjeta (terminal externa) y transferencia; un método por venta (docs/PAYMENTS.md).
-- payment se reconstruye conservando todas las filas; las reglas de efectivo no cambian.
CREATE TABLE payment_020(
 id TEXT PRIMARY KEY NOT NULL,
 sale_id TEXT NOT NULL UNIQUE REFERENCES sale(id) DEFERRABLE INITIALLY DEFERRED,
 method TEXT NOT NULL CHECK(method IN('CASH','CARD','TRANSFER')),
 applied_cents INTEGER NOT NULL CHECK(applied_cents>=0),
 received_cents INTEGER NOT NULL CHECK(received_cents>=applied_cents),
 change_cents INTEGER NOT NULL CHECK(change_cents=received_cents-applied_cents),
 reference TEXT CHECK(reference IS NULL OR length(trim(reference)) BETWEEN 1 AND 40),
 CHECK((method='CASH' AND reference IS NULL) OR (method<>'CASH' AND received_cents=applied_cents AND change_cents=0))
) STRICT;
INSERT INTO payment_020(id,sale_id,method,applied_cents,received_cents,change_cents)
SELECT id,sale_id,method,applied_cents,received_cents,change_cents FROM payment;
DROP TABLE payment;
PRAGMA legacy_alter_table=ON;
ALTER TABLE payment_020 RENAME TO payment;
PRAGMA legacy_alter_table=OFF;
CREATE TRIGGER payment_no_update BEFORE UPDATE ON payment BEGIN SELECT RAISE(ABORT,'Payments are immutable'); END;
CREATE TRIGGER payment_no_delete BEFORE DELETE ON payment BEGIN SELECT RAISE(ABORT,'Payments are immutable'); END;
CREATE TRIGGER payment_no_append BEFORE INSERT ON payment WHEN EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id)
BEGIN SELECT RAISE(ABORT,'Cannot append to completed sale'); END;

-- Devoluciones: sólo las de ventas en efectivo salen de la caja; las demás regresan por el mismo medio.
DROP TRIGGER sale_refund_complete;
CREATE TRIGGER sale_refund_complete BEFORE INSERT ON sale_refund
WHEN NOT EXISTS(SELECT 1 FROM cash_session c WHERE c.id=NEW.cash_session_id AND c.terminal_id=NEW.terminal_id AND c.state='OPEN' AND c.opened_utc<=NEW.refunded_utc)
 OR EXISTS(SELECT 1 FROM cash_count WHERE session_id=NEW.cash_session_id)
 OR NOT EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id AND confirmed_utc<=NEW.refunded_utc)
 OR NOT EXISTS(SELECT 1 FROM sale_refund_item WHERE refund_id=NEW.id)
 OR EXISTS(SELECT 1 FROM sale_refund_item WHERE refund_id=NEW.id AND sale_id<>NEW.sale_id)
 OR NEW.total_cents<>(SELECT SUM(line_total_cents) FROM sale_refund_item WHERE refund_id=NEW.id)
 OR EXISTS(SELECT 1 FROM sale_item i WHERE i.sale_id=NEW.sale_id
     AND i.quantity<(SELECT COALESCE(SUM(ri.quantity),0) FROM sale_refund_item ri
                     WHERE ri.sale_id=i.sale_id AND ri.position=i.position
                       AND (ri.refund_id=NEW.id OR ri.refund_id IN (SELECT id FROM sale_refund WHERE sale_id=NEW.sale_id))))
 OR (NEW.kind='CANCELLATION' AND (
     EXISTS(SELECT 1 FROM sale_refund WHERE sale_id=NEW.sale_id)
     OR NEW.cash_session_id<>(SELECT cash_session_id FROM sale WHERE id=NEW.sale_id)
     OR NEW.total_cents<>(SELECT total_cents FROM sale WHERE id=NEW.sale_id)))
 OR ((SELECT method FROM payment WHERE sale_id=NEW.sale_id)='CASH' AND
    (SELECT opening_cents FROM cash_session WHERE id=NEW.cash_session_id)
    + (SELECT COALESCE(SUM(p.applied_cents),0) FROM payment p JOIN sale s ON s.id=p.sale_id WHERE s.cash_session_id=NEW.cash_session_id AND p.method='CASH')
    - (SELECT COALESCE(SUM(r.total_cents),0) FROM sale_refund r JOIN payment p ON p.sale_id=r.sale_id WHERE r.cash_session_id=NEW.cash_session_id AND p.method='CASH')
    - NEW.total_cents < 0)
BEGIN SELECT RAISE(ABORT,'Invalid refund'); END;
