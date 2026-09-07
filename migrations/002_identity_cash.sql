CREATE TABLE app_role(name TEXT PRIMARY KEY NOT NULL) STRICT;
CREATE TABLE permission(name TEXT PRIMARY KEY NOT NULL) STRICT;
CREATE TABLE role_permission(
 role_name TEXT NOT NULL REFERENCES app_role(name),
 permission_name TEXT NOT NULL REFERENCES permission(name),
 PRIMARY KEY(role_name,permission_name)
) STRICT;
INSERT INTO app_role VALUES('Administrator'),('Manager'),('Cashier');
INSERT INTO permission VALUES('cash.open'),('cash.close'),('cash.view'),('cash.manual_open_future');
INSERT INTO role_permission SELECT 'Administrator',name FROM permission;
INSERT INTO role_permission SELECT 'Manager',name FROM permission;
INSERT INTO role_permission VALUES('Cashier','cash.open'),('Cashier','cash.view');
CREATE TABLE app_user(
 id TEXT PRIMARY KEY NOT NULL,
 username TEXT NOT NULL UNIQUE CHECK(length(username) BETWEEN 1 AND 80),
 display_name TEXT NOT NULL CHECK(length(trim(display_name)) BETWEEN 1 AND 100),
 password_hash TEXT NOT NULL CHECK(length(password_hash)>0),
 role_name TEXT NOT NULL REFERENCES app_role(name),
 active INTEGER NOT NULL DEFAULT 1 CHECK(active IN(0,1)),
 failed_attempts INTEGER NOT NULL DEFAULT 0 CHECK(failed_attempts>=0),
 locked_until_utc TEXT,
 created_utc TEXT NOT NULL
) STRICT;
CREATE TABLE cash_session(
 id TEXT PRIMARY KEY NOT NULL,
 terminal_id TEXT NOT NULL REFERENCES terminal(id),
 opened_by TEXT NOT NULL REFERENCES app_user(id),
 opened_utc TEXT NOT NULL,
 opening_cents INTEGER NOT NULL CHECK(opening_cents>=0),
 state TEXT NOT NULL CHECK(state IN('OPEN','CLOSED')),
 closed_utc TEXT,
 closed_by TEXT REFERENCES app_user(id),
 expected_cents INTEGER,
 counted_cents INTEGER CHECK(counted_cents IS NULL OR counted_cents>=0),
 difference_cents INTEGER,
 CHECK((state='OPEN' AND closed_utc IS NULL AND closed_by IS NULL AND counted_cents IS NULL AND difference_cents IS NULL)
 OR (state='CLOSED' AND closed_utc IS NOT NULL AND closed_by IS NOT NULL))
) STRICT;
CREATE UNIQUE INDEX ux_cash_one_open_terminal ON cash_session(terminal_id) WHERE state='OPEN';
CREATE INDEX ix_cash_opened_by ON cash_session(opened_by);
CREATE TRIGGER cash_no_reopen BEFORE UPDATE OF state ON cash_session
WHEN OLD.state='CLOSED' AND NEW.state<>'CLOSED'
BEGIN SELECT RAISE(ABORT,'Closed sessions cannot reopen'); END;
CREATE TABLE audit_entry(
 id TEXT PRIMARY KEY NOT NULL,
 actor_id TEXT NOT NULL REFERENCES app_user(id),
 terminal_id TEXT REFERENCES terminal(id),
 action TEXT NOT NULL,
 entity_id TEXT NOT NULL,
 occurred_utc TEXT NOT NULL,
 details_json TEXT NOT NULL
) STRICT;
CREATE UNIQUE INDEX ux_audit_cash_open ON audit_entry(entity_id) WHERE action='cash.open';
CREATE INDEX ix_audit_time ON audit_entry(occurred_utc);
CREATE TRIGGER audit_no_update BEFORE UPDATE ON audit_entry
BEGIN SELECT RAISE(ABORT,'Audit entries are immutable'); END;
CREATE TRIGGER audit_no_delete BEFORE DELETE ON audit_entry
BEGIN SELECT RAISE(ABORT,'Audit entries are immutable'); END;
