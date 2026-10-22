-- «Por medir»: una versión borrador con datos pendientes de confirmar (rendimiento, cantidades, unidades) no puede publicarse.
-- Se marca y desmarca al guardar el borrador; desmarcarla nunca publica. Una versión publicada nunca está «Por medir».
ALTER TABLE formula_version ADD COLUMN pending_measure INTEGER NOT NULL DEFAULT 0 CHECK(pending_measure IN(0,1));
-- Estado inicial explícito y seguro: ningún borrador existente se da por verificado; quien confirme sus datos quita la marca.
-- Las versiones publicadas no cambian (son inmutables) y quedan en 0.
UPDATE formula_version SET pending_measure=1 WHERE state='DRAFT';
CREATE TRIGGER formula_version_publish_pending_measure BEFORE UPDATE OF state ON formula_version
WHEN NEW.state='PUBLISHED' AND (OLD.pending_measure=1 OR NEW.pending_measure=1)
BEGIN SELECT RAISE(ABORT,'Pending measure'); END;
