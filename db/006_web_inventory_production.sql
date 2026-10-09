-- Punto de venta web, fase 3 (docs/POS_WEB.md): desde la web también se piden entradas, mermas y conteos de inventario,
-- producción hecha y la foto de un producto. La nube sólo los valida y los encola; la caja los aplica con sus reglas.
-- Inmutable una vez aplicada: agregar 007_*.sql para cambios.
ALTER TABLE branch_command DROP CONSTRAINT branch_command_kind_check;
ALTER TABLE branch_command ADD CONSTRAINT branch_command_kind_check CHECK (kind IN ('category.save','product.save','product.active',
  'user.create','user.update','user.active','user.reset_password',
  'consignment.store.save','consignment.template.save','consignment.deliver','consignment.collect','consignment.payment','consignment.void',
  'inventory.cost','inventory.receive','inventory.waste','inventory.count','production.record','product.photo'));

INSERT INTO schema_version(version) VALUES (6);
