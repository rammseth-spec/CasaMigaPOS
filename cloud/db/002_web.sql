-- Versión web (docs/WEB.md): todo lo que suben las cajas, el estado más reciente de cada registro,
-- los bloqueos de acceso web y los cambios hechos en la web que bajan a las cajas.
-- Inmutable una vez aplicada: agregar 003_*.sql para cambios.
ALTER TABLE sync_event DROP CONSTRAINT sync_event_entity_check;
ALTER TABLE sync_event ADD CONSTRAINT sync_event_entity_check CHECK (entity IN (
  'cash_session','cash_count','sale','sale_refund','audit_entry',
  'branch','app_user','role_permission','category','product','product_photo','cash_movement',
  'consignment_store','consignment_price','consignment_delivery','consignment_payment',
  'measurement_unit','inventory_location','inventory_item','inventory_operation',
  'formula','production_profile','production_batch'));
ALTER TABLE sync_event DROP CONSTRAINT sync_event_change_check;
ALTER TABLE sync_event ADD CONSTRAINT sync_event_change_check CHECK (change IN ('insert','update','delete'));

-- Último contenido de cada registro por sucursal (gana el encolado más reciente). Lo que consulta la web.
CREATE TABLE entity_state(
  branch_id uuid NOT NULL REFERENCES branch(id),
  entity text NOT NULL,
  entity_id text NOT NULL,
  terminal_id uuid NOT NULL REFERENCES terminal(id),
  seq bigint NOT NULL,
  enqueued_at timestamptz NOT NULL,
  deleted boolean NOT NULL DEFAULT false,
  payload jsonb NOT NULL,
  received_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (branch_id, entity, entity_id)
);
CREATE INDEX ix_entity_state_entity ON entity_state(entity, branch_id);
INSERT INTO entity_state(branch_id, entity, entity_id, terminal_id, seq, enqueued_at, deleted, payload, received_at)
SELECT DISTINCT ON (branch_id, entity, entity_id) branch_id, entity, entity_id, terminal_id, seq, enqueued_at, change = 'delete', payload, received_at
FROM sync_event ORDER BY branch_id, entity, entity_id, enqueued_at DESC, seq DESC;

-- Acceso web con el usuario de la caja: intentos fallidos por nombre de usuario.
CREATE TABLE web_lockout(
  username text PRIMARY KEY CHECK (length(username) BETWEEN 1 AND 80),
  failures integer NOT NULL DEFAULT 0 CHECK (failures >= 0),
  locked_until timestamptz,
  updated_at timestamptz NOT NULL DEFAULT now()
);

-- Cambios hechos en la web. Cada caja de la sucursal los aplica en orden (seq) con sus propias reglas y reporta el resultado.
CREATE TABLE branch_command(
  id uuid PRIMARY KEY,
  seq bigserial UNIQUE,
  branch_id uuid NOT NULL REFERENCES branch(id),
  kind text NOT NULL CHECK (kind IN ('category.save','product.save','product.active','user.create','user.update','user.active','user.reset_password',
    'consignment.store.save','consignment.template.save','consignment.deliver','consignment.collect','consignment.payment','consignment.void')),
  payload jsonb NOT NULL,
  requested_by_id text NOT NULL,
  requested_by_name text NOT NULL,
  requested_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_branch_command_branch ON branch_command(branch_id, seq);
CREATE TABLE command_result(
  command_id uuid NOT NULL REFERENCES branch_command(id),
  terminal_id uuid NOT NULL REFERENCES terminal(id),
  status text NOT NULL CHECK (status IN ('applied','rejected')),
  message text CHECK (message IS NULL OR length(message) <= 300),
  applied_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (command_id, terminal_id)
);

CREATE FUNCTION branch_command_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'branch_command is immutable'; END $$;
CREATE TRIGGER branch_command_no_update BEFORE UPDATE OR DELETE ON branch_command FOR EACH ROW EXECUTE FUNCTION branch_command_immutable();

INSERT INTO schema_version(version) VALUES (2);
