-- Existing closed sessions deliberately retain NULL; no financial data is rewritten.
ALTER TABLE cash_session ADD COLUMN discrepancy_note TEXT NULL
 CHECK(discrepancy_note IS NULL OR (length(discrepancy_note) BETWEEN 1 AND 500 AND discrepancy_note=trim(discrepancy_note)));
-- closed_no_update from 003 covers ALL columns, including discrepancy_note.
CREATE TRIGGER cash_close_note_required BEFORE UPDATE ON cash_session
WHEN OLD.state='OPEN' AND NEW.state='CLOSED' AND NEW.difference_cents<>0
 AND (NEW.discrepancy_note IS NULL OR length(trim(NEW.discrepancy_note))=0)
BEGIN SELECT RAISE(ABORT,'Cash discrepancy requires a note'); END;
