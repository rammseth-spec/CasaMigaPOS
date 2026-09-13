-- Additive contract marker: preserves every historical sale and incoming FK.
ALTER TABLE sale ADD COLUMN inventory_evaluation_contract_version INTEGER NOT NULL DEFAULT 0
 CHECK(inventory_evaluation_contract_version IN(0,1));
ALTER TABLE sale ADD COLUMN inventory_evaluation_id TEXT
 REFERENCES sale_inventory_evaluation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED
 CHECK((inventory_evaluation_contract_version=0 AND inventory_evaluation_id IS NULL)
 OR (inventory_evaluation_contract_version=1 AND inventory_evaluation_id IS NOT NULL));
CREATE UNIQUE INDEX ux_sale_inventory_evaluation ON sale(inventory_evaluation_id)
 WHERE inventory_evaluation_id IS NOT NULL;
CREATE TRIGGER sale_inventory_contract_required BEFORE INSERT ON sale
WHEN NEW.inventory_evaluation_contract_version<>1 OR NEW.inventory_evaluation_id IS NULL
BEGIN SELECT RAISE(ABORT,'New sales require inventory evaluation contract V1'); END;
CREATE TRIGGER sale_inventory_contract_owner BEFORE INSERT ON sale
WHEN EXISTS(SELECT 1 FROM sale_inventory_evaluation e WHERE e.id=NEW.inventory_evaluation_id
 AND (e.sale_id<>NEW.id OR e.checkout_operation_id<>NEW.operation_id))
BEGIN SELECT RAISE(ABORT,'Sale evaluation belongs to another sale'); END;
CREATE TRIGGER sale_inventory_evaluation_contract BEFORE INSERT ON sale_inventory_evaluation
WHEN NOT EXISTS(SELECT 1 FROM sale s WHERE s.id=NEW.sale_id AND s.operation_id=NEW.checkout_operation_id
 AND s.inventory_evaluation_contract_version=1 AND s.inventory_evaluation_id=NEW.id)
BEGIN SELECT RAISE(ABORT,'Evaluation must match the sale contract'); END;
-- Existing sale_no_update/no_delete protect the added columns too.