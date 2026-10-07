-- Gastos, retiros y entradas de efectivo de la caja abierta (docs/CASH_MOVEMENTS.md).
-- Registros inmutables que cambian el efectivo esperado del corte; un error se corrige con el movimiento contrario.
-- Cajero: gastos y entradas. Retiros (efectivo a resguardo): sólo encargado y administrador.
INSERT INTO permission VALUES('cash.movement'),('cash.withdrawal');
INSERT INTO role_permission VALUES('Administrator','cash.movement'),('Manager','cash.movement'),('Cashier','cash.movement'),
 ('Administrator','cash.withdrawal'),('Manager','cash.withdrawal');

CREATE TABLE cash_movement(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 cash_session_id TEXT NOT NULL REFERENCES cash_session(id),
 terminal_id TEXT NOT NULL REFERENCES terminal(id),
 user_id TEXT NOT NULL REFERENCES app_user(id),
 kind TEXT NOT NULL CHECK(kind IN('EXPENSE','WITHDRAWAL','DEPOSIT')),
 category TEXT CHECK(category IS NULL OR category IN('SUPPLIES','GAS','CLEANING','TRANSPORT','MAINTENANCE','OTHER')),
 amount_cents INTEGER NOT NULL CHECK(amount_cents>0),
 note TEXT NOT NULL CHECK(length(note)<=200),
 occurred_utc TEXT NOT NULL,
 -- Sólo los gastos llevan categoría; «Otro» exige explicar en la nota.
 CHECK((kind='EXPENSE')=(category IS NOT NULL)),
 CHECK(category IS NULL OR category<>'OTHER' OR length(trim(note))>=3)
) STRICT;
CREATE INDEX ix_cash_movement_session ON cash_movement(cash_session_id);

-- Sólo con la caja abierta de esa terminal y antes del conteo ciego; nunca sacar más efectivo del que debe haber.
CREATE TRIGGER cash_movement_valid BEFORE INSERT ON cash_movement
WHEN NOT EXISTS(SELECT 1 FROM cash_session c WHERE c.id=NEW.cash_session_id AND c.terminal_id=NEW.terminal_id AND c.state='OPEN' AND c.opened_utc<=NEW.occurred_utc)
 OR EXISTS(SELECT 1 FROM cash_count WHERE session_id=NEW.cash_session_id)
 OR (NEW.kind<>'DEPOSIT' AND
     (SELECT opening_cents FROM cash_session WHERE id=NEW.cash_session_id)
     + (SELECT COALESCE(SUM(p.applied_cents),0) FROM payment p JOIN sale s ON s.id=p.sale_id WHERE s.cash_session_id=NEW.cash_session_id AND p.method='CASH')
     - (SELECT COALESCE(SUM(r.total_cents),0) FROM sale_refund r JOIN payment p ON p.sale_id=r.sale_id WHERE r.cash_session_id=NEW.cash_session_id AND p.method='CASH')
     + (SELECT COALESCE(SUM(CASE kind WHEN 'DEPOSIT' THEN amount_cents ELSE -amount_cents END),0) FROM cash_movement WHERE cash_session_id=NEW.cash_session_id)
     - NEW.amount_cents < 0)
BEGIN SELECT RAISE(ABORT,'Invalid cash movement'); END;
CREATE TRIGGER cash_movement_no_update BEFORE UPDATE ON cash_movement BEGIN SELECT RAISE(ABORT,'Cash movements are immutable'); END;
CREATE TRIGGER cash_movement_no_delete BEFORE DELETE ON cash_movement BEGIN SELECT RAISE(ABORT,'Cash movements are immutable'); END;
CREATE UNIQUE INDEX ux_audit_cash_movement ON audit_entry(entity_id) WHERE action IN('cash.expense','cash.withdrawal','cash.deposit');

-- Devoluciones (regla de 020): el efectivo disponible ahora también resta gastos y retiros y suma entradas.
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
    + (SELECT COALESCE(SUM(CASE kind WHEN 'DEPOSIT' THEN amount_cents ELSE -amount_cents END),0) FROM cash_movement WHERE cash_session_id=NEW.cash_session_id)
    - NEW.total_cents < 0)
BEGIN SELECT RAISE(ABORT,'Invalid refund'); END;
