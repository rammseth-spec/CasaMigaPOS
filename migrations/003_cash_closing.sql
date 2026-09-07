INSERT INTO permission VALUES('cash.close.other');
INSERT INTO role_permission VALUES('Administrator','cash.close.other'),('Manager','cash.close.other');
CREATE TABLE cash_count(
 session_id TEXT PRIMARY KEY NOT NULL REFERENCES cash_session(id),
 counted_by TEXT NOT NULL REFERENCES app_user(id),
 counted_utc TEXT NOT NULL,
 counted_cents INTEGER NOT NULL CHECK(counted_cents>=0)
) STRICT;
CREATE TRIGGER count_open_only BEFORE INSERT ON cash_count
WHEN NOT EXISTS(SELECT 1 FROM cash_session WHERE id=NEW.session_id AND state='OPEN')
BEGIN SELECT RAISE(ABORT,'Count requires open session'); END;
CREATE TRIGGER count_no_update BEFORE UPDATE ON cash_count
BEGIN SELECT RAISE(ABORT,'Confirmed counts are immutable'); END;
CREATE TRIGGER count_no_delete BEFORE DELETE ON cash_count
BEGIN SELECT RAISE(ABORT,'Confirmed counts are immutable'); END;
CREATE TRIGGER closed_no_update BEFORE UPDATE ON cash_session WHEN OLD.state='CLOSED'
BEGIN SELECT RAISE(ABORT,'Closed sessions are immutable'); END;
CREATE TRIGGER closed_no_delete BEFORE DELETE ON cash_session WHEN OLD.state='CLOSED'
BEGIN SELECT RAISE(ABORT,'Closed sessions are immutable'); END;
CREATE TRIGGER close_financials BEFORE UPDATE ON cash_session WHEN NEW.state='CLOSED' AND
 (NEW.expected_cents IS NULL OR NEW.counted_cents IS NULL OR NEW.difference_cents IS NULL
 OR NEW.expected_cents<0 OR NEW.counted_cents<0
 OR NEW.difference_cents<>NEW.counted_cents-NEW.expected_cents)
BEGIN SELECT RAISE(ABORT,'Invalid closing amounts'); END;
CREATE UNIQUE INDEX ux_audit_cash_count ON audit_entry(entity_id) WHERE action='cash.count';
CREATE UNIQUE INDEX ux_audit_cash_close ON audit_entry(entity_id) WHERE action='cash.close';
