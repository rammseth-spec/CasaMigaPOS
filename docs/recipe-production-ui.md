## Hardening 1J.5 completada

Las listas de fórmulas/perfiles se refrescan al cargar el detalle después de guardar, publicar o activar; Volver ya no conserva el estado anterior. COUNT requiere escribir "pieza"; "pza" no es una unidad de captura admitida. La confirmación de otro usuario/terminal sigue rechazada; mismo usuario y terminal puede continuar con una sesión nueva.

# UI funcional de recetas y producción

Disponible desde 1J.4 para instalaciones de desarrollo. `Administrator` ve y configura Recetas, perfiles y Producción. `Manager` consulta Recetas en modo de sólo lectura y crea/confirma Producción. `Cashier` no recibe navegación ni acceso Application a estos módulos.

## Recetas

La lista separa fórmulas y perfiles de producto. Las versiones publicadas son históricas e inmutables; los cambios se realizan creando un nuevo borrador. El editor de fórmulas captura rendimiento, dimensión, ingredientes, cantidades operativas (`g`, `kg`, `ml`, `L`, `pieza`), política COUNT y grupos de alternativas con preferencia. El editor de perfiles vincula producto terminado, artículo terminado, fórmula publicada y requisitos directos. Los identificadores técnicos no son parte del flujo habitual.

## Producción

Nueva producción permite agregar varios productos con cantidad planeada y llama `PrepareProduction` mediante **Calcular necesidades**. El preview muestra preparaciones agregadas, insumos, necesario, disponible y todos los faltantes; no reserva inventario. **Crear producción** persiste un DRAFT bloqueado.

El detalle del borrador muestra creador, terminal, fecha, productos, preparaciones e ingredientes. El operador captura un resultado real entero no negativo por producto: menor, igual, mayor o cero. Si todos son cero, la UI advierte que los insumos se consumirán aunque no haya salida terminada. **Confirmar producción** deshabilita el envío durante la operación y llama al servicio; los errores de stock, inactividad, escala exacta o concurrencia se presentan en español.

El historial es de sólo lectura y muestra fecha, estado, creador, terminal, productos, planeado/real e indicador sin salida. El detalle confirmado conserva snapshots de preparaciones, consumo y alternativas. Los reintentos recuperados idempotentemente se muestran como éxito normal.

## Límites

El DRAFT no se edita; para cambiar el plan se crea uno nuevo. La confirmación continúa ligada al creador y terminal según el contrato 1J.3. No hay reservas, transferencias entre usuarios, recetas reales precargadas, impresión de producción, rediseño general ni fullscreen. Presentation no escribe SQL ni inventario y no recalcula recetas.
