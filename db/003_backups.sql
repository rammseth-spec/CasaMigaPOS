-- Respaldos en la nube (docs/BACKUPS.md): cada caja sube su base cada hora (si cambió) y sus fotos una sola vez;
-- la nube guarda cada noche una copia de su propia base. Todo cifrado (AES-256-GCM) antes de guardarse en Vercel Blob.
-- Aquí sólo el índice; si esta base se perdiera, cada respaldo deja además un manifiesto cifrado junto a sus archivos.
CREATE TABLE backup_object(
  id uuid PRIMARY KEY,
  branch_id uuid REFERENCES branch(id),
  terminal_id uuid REFERENCES terminal(id),
  kind text NOT NULL CHECK (kind IN ('database','photo','cloud')),
  sha256 text NOT NULL CHECK (sha256 ~ '^[0-9A-F]{64}$'),
  bytes bigint NOT NULL CHECK (bytes BETWEEN 1 AND 17179869184),
  parts integer NOT NULL CHECK (parts BETWEEN 1 AND 10000),
  created_at timestamptz NOT NULL DEFAULT now(),
  completed_at timestamptz,
  CHECK ((kind = 'cloud') = (branch_id IS NULL))
);
CREATE INDEX ix_backup_object_photo ON backup_object(branch_id, sha256) WHERE kind = 'photo' AND completed_at IS NOT NULL;
CREATE TABLE backup_part(
  object_id uuid NOT NULL REFERENCES backup_object(id) ON DELETE CASCADE,
  index integer NOT NULL CHECK (index >= 0),
  url text NOT NULL,
  bytes integer NOT NULL CHECK (bytes BETWEEN 1 AND 4194304),
  PRIMARY KEY (object_id, index)
);
CREATE TABLE backup_set(
  id uuid PRIMARY KEY,
  branch_id uuid REFERENCES branch(id),
  terminal_id uuid REFERENCES terminal(id),
  kind text NOT NULL CHECK (kind IN ('terminal','cloud')),
  database_object uuid NOT NULL REFERENCES backup_object(id),
  database_sha256 text NOT NULL,
  terminal_json text,
  schema_version integer,
  media jsonb NOT NULL DEFAULT '[]',
  bytes bigint NOT NULL,
  manifest_url text,
  created_at timestamptz NOT NULL DEFAULT now(),
  CHECK ((kind = 'cloud') = (terminal_id IS NULL))
);
CREATE INDEX ix_backup_set_terminal ON backup_set(terminal_id, created_at DESC);

INSERT INTO schema_version(version) VALUES (3);
