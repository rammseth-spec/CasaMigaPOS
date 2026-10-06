-- Base central Casa Miga (Neon PostgreSQL). Recibe la cola local sync_outbox de cada terminal (docs/SYNC_PLAN.md).
-- Inmutable una vez aplicada: agregar 002_*.sql para cambios.
CREATE TABLE schema_version(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());

CREATE TABLE branch(
  id uuid PRIMARY KEY,
  name text NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 100),
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE terminal(
  id uuid PRIMARY KEY,
  branch_id uuid NOT NULL REFERENCES branch(id),
  name text NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 100),
  key_sha256 text NOT NULL UNIQUE CHECK (key_sha256 ~ '^[0-9a-f]{64}$'),
  registered_at timestamptz NOT NULL DEFAULT now(),
  key_rotated_at timestamptz,
  revoked_at timestamptz,
  last_seen_at timestamptz
);

-- Registro inmutable de lo recibido; (terminal, seq) hace idempotente cualquier reenvío.
CREATE TABLE sync_event(
  terminal_id uuid NOT NULL REFERENCES terminal(id),
  seq bigint NOT NULL CHECK (seq >= 1),
  branch_id uuid NOT NULL REFERENCES branch(id),
  entity text NOT NULL CHECK (entity IN ('cash_session','cash_count','sale','sale_refund','audit_entry')),
  entity_id text NOT NULL CHECK (length(entity_id) BETWEEN 1 AND 64),
  change text NOT NULL CHECK (change IN ('insert','update')),
  enqueued_at timestamptz NOT NULL,
  payload jsonb NOT NULL,
  received_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (terminal_id, seq)
);
CREATE INDEX ix_sync_event_branch_entity ON sync_event(branch_id, entity, enqueued_at);
CREATE INDEX ix_sync_event_entity_id ON sync_event(entity, entity_id);

CREATE FUNCTION sync_event_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'sync_event is immutable'; END $$;
CREATE TRIGGER sync_event_no_update BEFORE UPDATE OR DELETE ON sync_event FOR EACH ROW EXECUTE FUNCTION sync_event_immutable();

INSERT INTO schema_version(version) VALUES (1);
