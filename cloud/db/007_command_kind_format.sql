-- Los tipos de cambio de la web los valida el código (lib/commands.ts, KINDS); la base sólo exige el formato
-- «área.acción» (por ejemplo «product.photo.remove»). Así un tipo nuevo no necesita otra migración.
-- Inmutable una vez aplicada: agregar 008_*.sql para cambios.
ALTER TABLE branch_command DROP CONSTRAINT branch_command_kind_check;
ALTER TABLE branch_command ADD CONSTRAINT branch_command_kind_check CHECK (kind ~ '^[a-z]+(\.[a-z_]+)+$' AND length(kind) <= 60);

INSERT INTO schema_version(version) VALUES (7);
