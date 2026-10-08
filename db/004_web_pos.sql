-- Punto de venta en la web (docs/POS_WEB.md): cajas web con su propio turno, folio y ventas.
-- Las ventas y turnos se guardan como los sube la caja de escritorio (sync_event + entity_state), así todos los
-- reportes los cuentan igual; aquí sólo vive lo que hace atómica cada operación.
-- Inmutable una vez aplicada: agregar 005_*.sql para cambios.
ALTER TABLE terminal ADD COLUMN kind text NOT NULL DEFAULT 'desktop' CHECK (kind IN ('desktop', 'web'));

-- Una fila por caja web. Cada venta, apertura, conteo o cierre es UNA sentencia que actualiza esta fila
-- (consecutivos y turno abierto) y escribe sus registros: dos dispositivos en la misma caja se forman en fila.
CREATE TABLE web_till(
  terminal_id uuid PRIMARY KEY REFERENCES terminal(id),
  branch_id uuid NOT NULL REFERENCES branch(id),
  sale_seq bigint NOT NULL DEFAULT 0 CHECK (sale_seq >= 0),
  event_seq bigint NOT NULL DEFAULT 0 CHECK (event_seq >= 0),
  open_session_id uuid,
  session_counted boolean NOT NULL DEFAULT false,
  created_by text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  CHECK (open_session_id IS NOT NULL OR NOT session_counted)
);

-- Identificador de operación de cada cobro: reintentar el mismo cobro devuelve la misma venta, nunca otra.
CREATE TABLE web_operation(
  operation_id uuid PRIMARY KEY,
  terminal_id uuid NOT NULL REFERENCES terminal(id),
  kind text NOT NULL CHECK (kind IN ('sale')),
  entity_id text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO schema_version(version) VALUES (4);
