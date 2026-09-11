INSERT INTO permission VALUES('sales.receipt.reprint');
INSERT INTO role_permission VALUES('Administrator','sales.receipt.reprint'),('Manager','sales.receipt.reprint');
CREATE TABLE receipt_snapshot(
 sale_id TEXT PRIMARY KEY NOT NULL REFERENCES sale(id),
 schema_version INTEGER NOT NULL CHECK(schema_version>=1),
 content_json TEXT NOT NULL CHECK(length(content_json)>0),
 content_sha256 TEXT NOT NULL CHECK(length(content_sha256)=64 AND content_sha256=upper(content_sha256) AND content_sha256 NOT GLOB '*[^0-9A-F]*'),
 created_utc TEXT NOT NULL
) STRICT;
CREATE TABLE receipt_print_request(
 id TEXT PRIMARY KEY NOT NULL,
 sale_id TEXT NOT NULL REFERENCES sale(id),
 kind TEXT NOT NULL CHECK(kind IN('ORIGINAL','RETRY','REPRINT')),
 parent_request_id TEXT REFERENCES receipt_print_request(id),
 requested_by TEXT NOT NULL REFERENCES app_user(id),
 requested_utc TEXT NOT NULL,
 queue_name TEXT CHECK(queue_name IS NULL OR length(trim(queue_name)) BETWEEN 1 AND 260),
 snapshot_sha256 TEXT CHECK(snapshot_sha256 IS NULL OR (length(snapshot_sha256)=64 AND snapshot_sha256=upper(snapshot_sha256) AND snapshot_sha256 NOT GLOB '*[^0-9A-F]*')),
 UNIQUE(id,sale_id),
 FOREIGN KEY(parent_request_id,sale_id) REFERENCES receipt_print_request(id,sale_id),
 CHECK(parent_request_id IS NOT NULL OR kind<>'RETRY'),
 CHECK(parent_request_id IS NULL OR kind<>'ORIGINAL'),
 CHECK(snapshot_sha256 IS NULL OR length(trim(queue_name))>0)
) STRICT;
CREATE TABLE receipt_print_event(
 id TEXT PRIMARY KEY NOT NULL,
 request_id TEXT NOT NULL REFERENCES receipt_print_request(id),
 sequence INTEGER NOT NULL CHECK(sequence>=1),
 state TEXT NOT NULL CHECK(state IN('REQUESTED','SUBMITTED','COMPLETED','FAILED','UNKNOWN')),
 occurred_utc TEXT NOT NULL,
 spooler_job_id INTEGER CHECK(spooler_job_id IS NULL OR spooler_job_id BETWEEN 1 AND 2147483647),
 error_code TEXT CHECK(error_code IS NULL OR (length(error_code) BETWEEN 1 AND 64 AND error_code NOT GLOB '*[^A-Z0-9_]*')),
 CHECK((state='REQUESTED' AND spooler_job_id IS NULL AND error_code IS NULL)
    OR (state IN('SUBMITTED','COMPLETED') AND spooler_job_id IS NOT NULL AND error_code IS NULL)
    OR (state IN('FAILED','UNKNOWN') AND error_code IS NOT NULL)),
 UNIQUE(request_id,sequence)
) STRICT;
CREATE UNIQUE INDEX ux_receipt_original ON receipt_print_request(sale_id) WHERE kind='ORIGINAL';
CREATE INDEX ix_receipt_request_sale ON receipt_print_request(sale_id,requested_utc);
CREATE INDEX ix_receipt_event_request ON receipt_print_event(request_id,sequence);
CREATE TRIGGER receipt_snapshot_no_update BEFORE UPDATE ON receipt_snapshot BEGIN SELECT RAISE(ABORT,'Receipt snapshots are immutable'); END;
CREATE TRIGGER receipt_snapshot_no_delete BEFORE DELETE ON receipt_snapshot BEGIN SELECT RAISE(ABORT,'Receipt snapshots are immutable'); END;
CREATE TRIGGER receipt_request_no_update BEFORE UPDATE ON receipt_print_request BEGIN SELECT RAISE(ABORT,'Receipt print requests are immutable'); END;
CREATE TRIGGER receipt_request_no_delete BEFORE DELETE ON receipt_print_request BEGIN SELECT RAISE(ABORT,'Receipt print requests are immutable'); END;
CREATE TRIGGER receipt_event_no_update BEFORE UPDATE ON receipt_print_event BEGIN SELECT RAISE(ABORT,'Receipt print events are immutable'); END;
CREATE TRIGGER receipt_event_no_delete BEFORE DELETE ON receipt_print_event BEGIN SELECT RAISE(ABORT,'Receipt print events are immutable'); END;
