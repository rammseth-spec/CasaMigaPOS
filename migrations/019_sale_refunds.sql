-- Cancelaciones y devoluciones como operaciones compensatorias inmutables (docs/REFUNDS.md).
-- La venta original nunca se modifica. El efectivo devuelto sale de la caja abierta de la terminal.
INSERT INTO permission VALUES('sales.refund');
INSERT INTO role_permission VALUES('Administrator','sales.refund'),('Manager','sales.refund');

CREATE TABLE sale_refund(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 operation_id TEXT NOT NULL UNIQUE CHECK(length(operation_id)=36),
 sale_id TEXT NOT NULL REFERENCES sale(id),
 kind TEXT NOT NULL CHECK(kind IN('CANCELLATION','RETURN')),
 reason TEXT NOT NULL CHECK(length(trim(reason)) BETWEEN 3 AND 200),
 user_id TEXT NOT NULL REFERENCES app_user(id),
 terminal_id TEXT NOT NULL REFERENCES terminal(id),
 cash_session_id TEXT NOT NULL REFERENCES cash_session(id),
 refunded_utc TEXT NOT NULL,
 total_cents INTEGER NOT NULL CHECK(total_cents>0)
) STRICT;
CREATE INDEX ix_sale_refund_sale ON sale_refund(sale_id);
CREATE INDEX ix_sale_refund_session ON sale_refund(cash_session_id);

CREATE TABLE sale_refund_item(
 refund_id TEXT NOT NULL REFERENCES sale_refund(id) DEFERRABLE INITIALLY DEFERRED,
 sale_id TEXT NOT NULL,
 position INTEGER NOT NULL CHECK(position>=1),
 quantity INTEGER NOT NULL CHECK(quantity>=1),
 line_total_cents INTEGER NOT NULL CHECK(line_total_cents>=0),
 PRIMARY KEY(refund_id,position),
 FOREIGN KEY(sale_id,position) REFERENCES sale_item(sale_id,position)
) STRICT;

-- Las partidas se insertan antes del encabezado; el encabezado valida el conjunto completo.
CREATE TRIGGER sale_refund_item_valid BEFORE INSERT ON sale_refund_item
WHEN EXISTS(SELECT 1 FROM sale_refund WHERE id=NEW.refund_id)
 OR NOT EXISTS(SELECT 1 FROM sale_item i WHERE i.sale_id=NEW.sale_id AND i.position=NEW.position AND NEW.line_total_cents=i.unit_price_cents*NEW.quantity)
BEGIN SELECT RAISE(ABORT,'Invalid refund item'); END;

CREATE TRIGGER sale_refund_complete BEFORE INSERT ON sale_refund
WHEN NOT EXISTS(SELECT 1 FROM cash_session c WHERE c.id=NEW.cash_session_id AND c.terminal_id=NEW.terminal_id AND c.state='OPEN' AND c.opened_utc<=NEW.refunded_utc)
 OR EXISTS(SELECT 1 FROM cash_count WHERE session_id=NEW.cash_session_id)
 OR NOT EXISTS(SELECT 1 FROM sale WHERE id=NEW.sale_id AND confirmed_utc<=NEW.refunded_utc)
 OR NOT EXISTS(SELECT 1 FROM sale_refund_item WHERE refund_id=NEW.id)
 OR EXISTS(SELECT 1 FROM sale_refund_item WHERE refund_id=NEW.id AND sale_id<>NEW.sale_id)
 OR NEW.total_cents<>(SELECT SUM(line_total_cents) FROM sale_refund_item WHERE refund_id=NEW.id)
 -- Nunca devolver más piezas de las vendidas, sumando devoluciones anteriores.
 OR EXISTS(SELECT 1 FROM sale_item i WHERE i.sale_id=NEW.sale_id
     AND i.quantity<(SELECT COALESCE(SUM(ri.quantity),0) FROM sale_refund_item ri
                     WHERE ri.sale_id=i.sale_id AND ri.position=i.position
                       AND (ri.refund_id=NEW.id OR ri.refund_id IN (SELECT id FROM sale_refund WHERE sale_id=NEW.sale_id))))
 -- Cancelación: venta completa, sin devoluciones previas y dentro del mismo turno de caja.
 OR (NEW.kind='CANCELLATION' AND (
     EXISTS(SELECT 1 FROM sale_refund WHERE sale_id=NEW.sale_id)
     OR NEW.cash_session_id<>(SELECT cash_session_id FROM sale WHERE id=NEW.sale_id)
     OR NEW.total_cents<>(SELECT total_cents FROM sale WHERE id=NEW.sale_id)))
 -- No entregar más efectivo del que debe haber en caja.
 OR (SELECT opening_cents FROM cash_session WHERE id=NEW.cash_session_id)
    + (SELECT COALESCE(SUM(p.applied_cents),0) FROM payment p JOIN sale s ON s.id=p.sale_id WHERE s.cash_session_id=NEW.cash_session_id AND p.method='CASH')
    - (SELECT COALESCE(SUM(total_cents),0) FROM sale_refund WHERE cash_session_id=NEW.cash_session_id)
    - NEW.total_cents < 0
BEGIN SELECT RAISE(ABORT,'Invalid refund'); END;

CREATE TRIGGER sale_refund_no_update BEFORE UPDATE ON sale_refund BEGIN SELECT RAISE(ABORT,'Refunds are immutable'); END;
CREATE TRIGGER sale_refund_no_delete BEFORE DELETE ON sale_refund BEGIN SELECT RAISE(ABORT,'Refunds are immutable'); END;
CREATE TRIGGER sale_refund_item_no_update BEFORE UPDATE ON sale_refund_item BEGIN SELECT RAISE(ABORT,'Refunds are immutable'); END;
CREATE TRIGGER sale_refund_item_no_delete BEFORE DELETE ON sale_refund_item BEGIN SELECT RAISE(ABORT,'Refunds are immutable'); END;
CREATE UNIQUE INDEX ux_audit_sale_refund ON audit_entry(entity_id) WHERE action IN('sale.cancelled','sale.returned');

-- La cola de sincronización acepta devoluciones: se reconstruye conservando secuencias y estado.
DROP TRIGGER sync_cash_session_insert;
DROP TRIGGER sync_cash_session_update;
DROP TRIGGER sync_cash_count_insert;
DROP TRIGGER sync_sale_insert;
DROP TRIGGER sync_audit_entry_insert;
DROP TRIGGER sync_outbox_no_delete_pending;
CREATE TABLE sync_outbox_v19(
 seq INTEGER PRIMARY KEY AUTOINCREMENT,
 entity TEXT NOT NULL CHECK(entity IN ('cash_session','cash_count','sale','sale_refund','audit_entry')),
 entity_id TEXT NOT NULL CHECK(length(entity_id)>0),
 change TEXT NOT NULL CHECK(change IN ('insert','update')),
 enqueued_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 sent_utc TEXT,
 attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts>=0),
 last_error TEXT
) STRICT;
INSERT INTO sync_outbox_v19(seq,entity,entity_id,change,enqueued_utc,sent_utc,attempts,last_error)
SELECT seq,entity,entity_id,change,enqueued_utc,sent_utc,attempts,last_error FROM sync_outbox;
DROP TABLE sync_outbox;
ALTER TABLE sync_outbox_v19 RENAME TO sync_outbox;
CREATE INDEX ix_sync_outbox_pending ON sync_outbox(seq) WHERE sent_utc IS NULL;
CREATE TRIGGER sync_outbox_no_delete_pending BEFORE DELETE ON sync_outbox
WHEN OLD.sent_utc IS NULL
BEGIN SELECT RAISE(ABORT,'Pending sync entries cannot be deleted'); END;
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
