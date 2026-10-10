-- Inventario en la web (docs/PLAN_COMPLETAR.md, W4): el estado del consumo automático por ventas viaja dentro de la sucursal,
-- así la web muestra DESACTIVADO / ACTIVO / PAUSADO con su versión y puede pedir Activar, Pausar o Reanudar.
CREATE TRIGGER sync_sale_consumption_update AFTER UPDATE ON inventory_sale_consumption_config
BEGIN INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'branch',id,'update' FROM branch; END;
-- El estado actual se sube una vez.
INSERT INTO sync_outbox(entity,entity_id,change) SELECT 'branch',id,'update' FROM branch;
