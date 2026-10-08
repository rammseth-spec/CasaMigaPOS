-- Cambios hechos en la versión web que esta caja ya aplicó o rechazó (docs/WEB.md). Uno por cambio: aplicar dos veces no repite nada.
-- El cursor de lectura es el seq más alto; reported_utc marca lo que ya se informó a la nube.
CREATE TABLE cloud_command(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 seq INTEGER NOT NULL UNIQUE CHECK(seq>=1),
 kind TEXT NOT NULL CHECK(length(kind) BETWEEN 1 AND 60),
 requested_by TEXT NOT NULL CHECK(length(requested_by) BETWEEN 1 AND 120),
 status TEXT NOT NULL CHECK(status IN('applied','rejected')),
 message TEXT CHECK(message IS NULL OR length(message)<=300),
 applied_utc TEXT NOT NULL,
 reported_utc TEXT
) STRICT;
CREATE INDEX ix_cloud_command_unreported ON cloud_command(seq) WHERE reported_utc IS NULL;
CREATE TRIGGER cloud_command_no_delete BEFORE DELETE ON cloud_command BEGIN SELECT RAISE(ABORT,'Cloud commands are kept'); END;
CREATE TRIGGER cloud_command_result_immutable BEFORE UPDATE OF id,seq,kind,requested_by,status,message,applied_utc ON cloud_command
BEGIN SELECT RAISE(ABORT,'Cloud command results are immutable'); END;
