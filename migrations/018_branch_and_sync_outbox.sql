-- Identidad de sucursal de esta instalación y cola local de sincronización (docs/SYNC_PLAN.md, fase 1).
-- Una base local pertenece a una sola sucursal.
CREATE TABLE branch(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 100),
 singleton INTEGER NOT NULL DEFAULT 1 UNIQUE CHECK(singleton=1),
 created_utc TEXT NOT NULL
) STRICT;
INSERT INTO branch(id,name,created_utc)
SELECT lower(substr(h,1,8)||'-'||substr(h,9,4)||'-4'||substr(h,14,3)||'-'||substr('89ab',1+(abs(random())%4),1)||substr(h,18,3)||'-'||substr(h,21,12)),
       'Sucursal principal', strftime('%Y-%m-%dT%H:%M:%fZ','now')
FROM (SELECT hex(randomblob(16)) AS h);
CREATE TRIGGER branch_no_delete BEFORE DELETE ON branch
BEGIN SELECT RAISE(ABORT,'Branch identity cannot be deleted'); END;
CREATE TRIGGER branch_id_immutable BEFORE UPDATE OF id ON branch
BEGIN SELECT RAISE(ABORT,'Branch identity is immutable'); END;

CREATE TABLE sync_outbox(
 seq INTEGER PRIMARY KEY AUTOINCREMENT,
 entity TEXT NOT NULL CHECK(entity IN ('cash_session','cash_count','sale','audit_entry')),
 entity_id TEXT NOT NULL CHECK(length(entity_id)>0),
 change TEXT NOT NULL CHECK(change IN ('insert','update')),
 enqueued_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 sent_utc TEXT,
 attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts>=0),
 last_error TEXT
) STRICT;
CREATE INDEX ix_sync_outbox_pending ON sync_outbox(seq) WHERE sent_utc IS NULL;
CREATE TRIGGER sync_outbox_no_delete_pending BEFORE DELETE ON sync_outbox
WHEN OLD.sent_utc IS NULL
BEGIN SELECT RAISE(ABORT,'Pending sync entries cannot be deleted'); END;

-- Historial existente: se encola una sola vez, en orden de dependencia.
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'cash_session',id,'insert' FROM cash_session ORDER BY opened_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'cash_count',session_id,'insert' FROM cash_count ORDER BY counted_utc,session_id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'sale',id,'insert' FROM sale ORDER BY confirmed_utc,id;
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'audit_entry',id,'insert' FROM audit_entry ORDER BY occurred_utc,id;

-- Cambios nuevos: mismo commit que la operación comercial.
CREATE TRIGGER sync_cash_session_insert AFTER INSERT ON cash_session
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_session',NEW.id,'insert'); END;
CREATE TRIGGER sync_cash_session_update AFTER UPDATE ON cash_session
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_session',NEW.id,'update'); END;
CREATE TRIGGER sync_cash_count_insert AFTER INSERT ON cash_count
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('cash_count',NEW.session_id,'insert'); END;
CREATE TRIGGER sync_sale_insert AFTER INSERT ON sale
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('sale',NEW.id,'insert'); END;
CREATE TRIGGER sync_audit_entry_insert AFTER INSERT ON audit_entry
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) VALUES('audit_entry',NEW.id,'insert'); END;
