# ADR-023 — Servicios de producción y confirmación atómica

Fecha: 2026-09-22. FASE 1J.3. Complementa ADR-021/022 y las correcciones 014/015. Schema 15 sin modificación.

## Frontera y autorización

ProductionService ofrece PrepareProduction, CreateProduction, GetProduction, ListProductions y ConfirmProduction. RecipeService ofrece lectura, guardar draft, publicar y activar fórmulas/perfiles. Los puertos y modelos de lectura pertenecen a Application; Infrastructure implementa IRecipeStore/IProductionUnitOfWork. Domain no depende de estas capas.

Cada llamada revalida usuario activo, terminal y HasPermission persistido dentro de la transacción: production.create para preview/create, production.view para consulta, production.confirm para confirmar y recuperar, recipe.view/configure para recetas. No cambia la matriz 015: Administrator cinco permisos, Manager cuatro sin configure, Cashier ninguno. No se registra UI ni comandos de producción en Presentation.

## Plan congelado

Se eligen IDs de versiones publicadas exactas al preparar/crear. Domain agrega demanda de fórmulas compartidas antes de escalar, aplica políticas COUNT y alternativas explícitas/preferencias persistidas. El fallback no depende del nombre ni del stock. MASS/VOLUME no representable conserva problema estructurado con identidad y racional; DRAFT pendiente sin inputs inventados.

Preview consulta MAIN sin reserva ni escritura. Incluye stock, faltantes, inactivos y saldos no inicializados. Confirmación vuelve a leerlos bajo BEGIN IMMEDIATE. Se exige saldo inicializado también para los outputs del plan. No hay override de stock negativo.

El DRAFT funcional no tiene edición del plan: cambiar plan o alternativas requiere otro lote. Schema 15 no contiene revisión numérica del lote: ExpectedRevision es SHA-256 del contrato de plan con actuals cero, estable entre DRAFT/CONFIRMED. No se inventa una columna. La confirmación conserva creador y terminal del lote, como requieren los guards 013; transferencia entre operadores no pertenece a este servicio.

## Contrato V1

ProductionContractVersion e IdentityVersion son 1. JSON System.Text.Json compacto, UTF-8, SHA-256 hexadecimal mayúsculo. Orden de propiedades fijado en ProductionContract.Hash. UUID en formato D; enteros/racionales Int64, sin locale. Outputs ordenados por ProductId; fórmulas por FormulaVersionId; inputs por FormulaVersionId, ProfileVersionId, GroupId, OptionId, ItemId mediante orden Guid de .NET (nullable primero). Dimensión y política se codifican mediante nombres enum actuales, parte del contrato V1.

Incluye lote, operación raíz, actor, terminal, MAIN, versiones, planned/actual, insumos, opciones, políticas, factores racionales e identidades derivadas. Excluye nombres visuales, notas, UTC y estado. Cambiar codificación requiere otra versión formal.

UUIDv5 sobre OperationId raíz en formato D: namespace consumo 70de5260-9c49-50ce-a30c-14d27e575228; salida 5909cf55-b0cc-51f0-86a0-9a0ad456f250. Sólo se materializa ID de salida si existe actual positivo.

## Transacción y recuperación

Una sola transacción agrupa permiso, lookup, revisión, stock, actuals/hash, consumo, salida opcional, balances, auditoría y CONFIRMED. SqliteInventoryStore.WriteOperation sigue siendo el único escritor del ledger/saldo, con origen de producción explícito. Su API genérica rechaza operaciones productivas. Las FKs diferidas y guards históricos siguen vigentes.

Inputs se consumen según PlannedCount; outputs usan ActualCount >= 0, incluso mayor al plan. Cero omite esa línea. Todos cero: consumo obligatorio, OutputOperationId NULL, sin operación de salida, movimiento cero ni WASTE automático. El campo heredado actual_policy_pending conserva la representación exigida por el CHECK de 013 para cero/superior al plan; no significa que esas cantidades requieran una aprobación futura en este servicio.

Confirm recupera primero el lote por OperationId y compara contrato histórico, sin consultar catálogo vivo. Comprueba hashes, IDs, origen, operaciones confirmadas y movimientos. No duplica auditoría. Errores inesperados tras commit se resuelven con otra transacción una vez dispuesta la original. Si no hay confirmación persistida se conserva el error. No se promete disponibilidad frente a un almacenamiento que continúa fallando: el cliente debe reintentar la misma identidad, nunca generar una nueva.

Reintentar creación compara plan, actor, terminal, notas y alternativas completas usando las mismas versiones publicadas inmutables; una creación no equivale a confirmación. Cambios semánticos generan conflicto. BEGIN IMMEDIATE serializa escritores; stock insuficiente o busy/locked producen rechazo estable.

## Verificación y límites

Pruebas de servicios reales: cantidades cero/menor/igual/mayor, multi-output, alternativas, escala no representable, versiones nuevas, permisos, rollback en siete puntos, commit incierto, concurrencia, overflow, backup/restauración positiva/cero y reconciliación con venta. Ver reporte y evidencia 1J.3 para resultados ejecutados.

No UI, impresión, migración 016, datos productivos ni cambios de checkout/caja. Desarrollo, no listo para producción. 1J.4 requiere autorización y diseño de experiencia para conservar identidad/revisión y presentar problemas estructurados.
