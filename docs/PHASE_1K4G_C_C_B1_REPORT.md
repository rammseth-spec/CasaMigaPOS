# FASE 1K.4G-C-C-B1 — Preparación del inventario comercial real

Fecha: 18/10/2026. Estado técnico: **COMPLETADA; smoke humano de consulta pendiente**.

## Alcance y protección de datos

Se trabajó sobre una nueva copia aislada creada mediante `SQLiteConnection.BackupDatabase`, por lo que el WAL del origen quedó incluido sin escribir en la copia C-B. El origen fue `work/phase1k4g-c-b-review-20261018/casamiga.db`, SHA-256 `F16ED3CFA7CDF94B0DC20B8E790DCC3B27A69F065A1FAB35BCE122050B282576`. El destino es `work/phase1k4g-c-c-b1-review-20261018/casamiga.db`, SHA-256 `B40CCD0C50024B433F698953875F5D08A5F85356B70FE91511B2AA3B41F87CAF`.

La base protegida `work/phase1k2c-r2-smoke-20260928/casamiga.db` conserva SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`. El origen C-B y la base protegida conservaron sus hashes después de la preparación. No se reutilizó ni modificó una fixture cerrada.

La operación usó `InventoryService.CreateInventoryItem`, identidad y permisos existentes. El manifiesto local `work/phase1k4g-c-c-b1-review-20261018/b1-inventory-manifest.json` registra IDs, correspondencias y digests. La comparación de todas las tablas antes/después permitió cambios únicamente en `inventory_item` y `audit_entry`; ventas, pagos, caja, productos, precios, SKU, fotos, saldos, movimientos, recetas, producción, vínculos y configuración de consumo conservaron sus digests.

## Resultado de inventario

La copia contiene 67 artículos activos: 31 materias primas y 36 productos terminados. Harina ya existía y se reutilizó; se crearon 30 materias primas y 36 terminados. No hay códigos o nombres duplicados.

### Materias primas

| Código | Nombre | Dimensión | Estado |
|---|---|---|---|
| MP-HAR-001 | Harina | MASS | Heredada; presentación Bulto 25 kg y saldo previo preservados |
| MP-AZU-EST | Azúcar estándar | MASS | Creada |
| MP-AZU-GLA | Azúcar glass | MASS | Creada |
| MP-MAR-001 | Margarina | MASS | Creada |
| MP-MAN-VEG | Manteca vegetal | MASS | Creada |
| MP-MAN-001 | Mantequilla | MASS | Creada |
| MP-SAL-001 | Sal | MASS | Creada |
| MP-AGU-001 | Agua | VOLUME | Creada |
| MP-HUE-001 | Huevo | COUNT | Creada |
| MP-LEV-FRE | Levadura fresca | MASS | Creada |
| MP-TRA-011 | Tradi Pan · sobre de 11 g | COUNT | Creada |
| MP-LEV-SEC | Levadura seca | MASS | Creada |
| MP-ACE-VEG | Aceite vegetal | VOLUME | Creada |
| MP-CAC-001 | Cacao en polvo | MASS | Creada |
| MP-CAN-001 | Canela en polvo | MASS | Creada |
| MP-CHO-NEG | Chocolate negro de cobertura | MASS | Creada |
| MP-CHO-BLA | Chocolate blanco de cobertura | MASS | Creada |
| MP-NUE-001 | Nuez | MASS | Creada |
| MP-ALM-001 | Almendra fileteada | MASS | Creada |
| MP-COC-001 | Coco rallado | MASS | Creada |
| MP-AJO-001 | Ajonjolí | MASS | Creada |
| MP-CRE-PAS | Crema pastelera comprada | MASS | Creada |
| MP-REL-ZAR | Relleno de zarzamora comprado | MASS | Creada |
| MP-CHI-COL | Chispas de colores | MASS | Creada |
| MP-QUE-CRE | Queso crema | MASS | Creada |
| MP-LEC-CON | Leche condensada | MASS | Creada |
| MP-LEC-EVA | Leche evaporada | VOLUME | Creada |
| MP-FEC-MAI | Fécula de maíz | MASS | Creada |
| MP-LEC-PRO | Leche para producción | VOLUME | Creada |
| MP-POL-HOR | Polvo para hornear (Royal) | MASS | Creada |
| MP-NAR-RAL | Naranja para ralladura | COUNT | Creada |

No se inventaron presentaciones para los artículos nuevos. MASS usa las unidades estándar mg/g/kg, VOLUME usa µL/ml/L y COUNT usa pieza. No se cargaron costos, proveedores o cantidades disponibles.

### Productos terminados

Cada producto activo tiene un artículo independiente `PT-{SKU}`, tipo `FINISHED_GOOD`, dimensión COUNT. La correspondencia se conserva en el manifiesto para revisión, pero **no** se creó `ProductInventoryLink` y por tanto no existe descuento operativo por venta.

| SKU | Producto | ProductId | Artículo |
|---|---|---|---|
| CM-001 | Concha de vainilla | e4749ad8-0671-4a90-b616-f096a667af45 | PT-CM-001 |
| CM-002 | Concha de chocolate | 3ac35bb4-414c-4f90-ad47-65807101f556 | PT-CM-002 |
| CM-003 | Bolillo de mantequilla | f9d8569d-765a-4152-bc61-36cc3f88ef24 | PT-CM-003 |
| CM-004 | Cuernito de mantequilla | 059d9f77-e474-4fc2-bb7a-762a0cb977f2 | PT-CM-004 |
| CM-005 | Cuernito de almendra (bigote) | 9d2f58bc-5e76-44c3-9e67-8c46048dd696 | PT-CM-005 |
| CM-006 | Rol de canela | 1fde7b16-c2bc-4f2e-ac37-cf7dd2e5404e | PT-CM-006 |
| CM-007 | Dona de chocolate negro | a792f462-e335-4d8b-a215-37625c36777a | PT-CM-007 |
| CM-008 | Dona de azúcar | 77a5b8a8-53f9-42bb-a9ab-c607d0f1054b | PT-CM-008 |
| CM-010 | Polvorón tradicional | ac1d3c85-b798-4087-90bf-a751bec30f42 | PT-CM-010 |
| CM-011 | Polvorón de nuez | 8c72c1c6-c3a3-4f3d-bc87-c11a15f6fab3 | PT-CM-011 |
| CM-013 | Pan de muerto pequeño | 7119bef9-735a-4170-8f96-8d2962a1fc79 | PT-CM-013 |
| CM-014 | Muffin sencillo | b8c445c4-6197-4d9b-be8b-1d7f07850bce | PT-CM-014 |
| CM-015 | Muffin de nuez | 8e8fedbe-a1b3-47e4-8709-cbc7f098d3ec | PT-CM-015 |
| CM-016 | Danés con crema pastelera | 6578f84a-8568-4772-94be-0dc03688af0a | PT-CM-016 |
| CM-017 | Bollo relleno de crema pastelera | 8b0711ef-b6e6-47dc-b7f2-01ace22149f2 | PT-CM-017 |
| CM-019 | Bollo relleno de zarzamora | c6ba8dd7-8176-4f6f-80db-69971bb34923 | PT-CM-019 |
| CM-020 | Dona glaseada | b19768a9-684c-4d46-878f-fb396194bf88 | PT-CM-020 |
| CM-021 | Dona de chocolate blanco | a26997fb-46df-4fe9-bd30-173ae115c675 | PT-CM-021 |
| CM-023 | Galleta con chispitas de colores | 644edb70-10c4-4858-bfa9-99c66401bbaa | PT-CM-023 |
| CM-024 | Vaso de café | 63198b97-7df4-4e00-82e6-063a772b5a44 | PT-CM-024 |
| CM-025 | Leche Kirkland entera | 6b11d309-148f-4103-97f8-cf4d2f02168c | PT-CM-025 |
| CM-026 | Leche Kirkland deslactosada light | c2c87410-7766-4551-87c6-32d205f54c9f | PT-CM-026 |
| CM-027 | Rosquita de ajonjolí | 14030080-0244-4806-8a04-6499bf96597c | PT-CM-027 |
| CM-028 | Bollo con ajonjolí | 5e9876c0-d589-4f75-91a0-709263e1a8d4 | PT-CM-028 |
| CM-029 | Baguette con ajonjolí | 4274e78f-3c15-49c9-977b-f474e9824e8e | PT-CM-029 |
| CM-030 | Dona de chocolate negro con chispas | aabb7fc6-23a8-4fcf-bb44-82f97a5dee5e | PT-CM-030 |
| CM-031 | Dona de chocolate blanco con coco | b38153df-ee04-4c55-ad82-8ba8714fb97d | PT-CM-031 |
| CM-032 | Dona de chocolate con nuez | 7e9cb3f7-a40a-45ca-a91c-1df15b7291f9 | PT-CM-032 |
| CM-033 | Dona combinada de chocolate negro y blanco | 71c324c4-40ff-4181-9079-10565a61f056 | PT-CM-033 |
| CM-034 | Berlinesa rellena de crema pastelera | a31694a4-d410-421b-89f0-de749193be64 | PT-CM-034 |
| CM-035 | Berlinesa rellena de zarzamora | ba082fc9-d81f-4a05-b7ba-88a8778abfff | PT-CM-035 |
| CM-036 | Galleta de nuez | 46496b9b-0da4-42df-a6bb-058a47333ec1 | PT-CM-036 |
| CM-037 | Galleta de coco | 7592364d-eaff-4222-b5b8-f8705413e097 | PT-CM-037 |
| CM-038 | Galleta multigrano | f532a645-e89a-480d-93d7-d4f2cf3067b8 | PT-CM-038 |
| CM-039 | Pay de queso individual | 0364ccd2-6c83-41bf-9a5d-477514dfa7a1 | PT-CM-039 |
| CM-040 | Pan de muerto grande | a67359c8-e885-4764-8a97-6051cc985fe4 | PT-CM-040 |

Los cinco productos históricos inactivos no recibieron artículo terminado.

## Pendientes y límites deliberados

- **Esencia de vainilla:** pendiente. El recetario la expresa en MASS y VOLUME; no se creó un artículo ni una conversión masa–volumen inventada.
- **Mezcla de semillas multigrano:** pendiente hasta confirmar identidad y composición comercial.
- Los rendimientos, aproximaciones, preparaciones derivadas, consumos fijos por tanda, barnices, aceite reutilizable, sobrantes, degustación y consumo interno siguen bloqueados según la auditoría C-C-A.
- No se crearon `Formula`, `FormulaVersion`, `ProductionProfile`, preparaciones vendibles ni fórmulas incompletas.
- La única existencia es el saldo heredado de Harina. Se conservan un `inventory_balance`, una operación y un movimiento previos; los artículos B1 tienen cero saldos iniciales nuevos.
- `product_inventory_link=0` y `inventory_sale_consumption_config=DISABLED`. El riesgo de doble consumo permanece controlado porque una venta no puede descontar estos artículos.

## Validación de datos

- Catálogo: 41 productos, 36 activos y cinco inactivos.
- Identidades, ProductId, SKU, nombres, precios, categorías y `track_inventory`: sin cambios por digest.
- Inventario: 67 artículos; 31 RAW_MATERIAL y 36 FINISHED_GOOD; un solo saldo heredado.
- Vínculos producto–inventario: 0.
- Fórmulas/versiones/perfiles/lotes: 0.
- Ventas 20, pagos 20 y sesiones de caja 2: sin cambios por digest.
- Fotos: 28 asignaciones y 31 assets en SQLite, sin cambios por digest; los archivos multimedia se copiaron sin edición.
- `PRAGMA integrity_check=ok`; `foreign_key_check=0`; schema 17; no existe migración 018.
- Consumo automático por venta: `DISABLED`.

## Compilación y pruebas

`scripts/verify.ps1` aprobó la suite oficial completa: **799/799** (584 generales + 215 WPF/Presentation), cero fallidas y cero omitidas. TRX: `TestResults/foundation_net10.0_20261018234017.trx` y `TestResults/foundation_net10.0_20261018234242.trx`. Release estándar y build identificado B1: **0 errores y 0 advertencias**.

Dos intentos iniciales dentro del sandbox fueron anulados porque `vstest.console` no pudo establecer IPC con `testhost`; no se contabilizan como evidencia aprobada. La ejecución oficial fuera de ese aislamiento terminó correctamente sin cambiar código ni datos.

Build de revisión: `src/CasaMiga.Presentation/bin/Phase1K4G-C-C-B1/CasaMiga.Presentation.exe`, SHA-256 `24266BF99AD9C1052BAFE71398EF08F6874DA37CB54B0B659D2FDFB2611E8A89`; DLL SHA-256 `37A370C4D7A0095D611BEB9CCD4CDE70F70D4219D6AFE34346C3EB2E7BD956A9`. Launcher: `work/Abrir-Casa-Miga-1K4G-C-C-B1.cmd`, SHA-256 `CB440276FCC7A7CDE4FCD17F0AF3408F08F5848FBCFB7187AA965CEC5065A1F8`. `development.settings.json` fija exclusivamente el DataDirectory B1 y el título esperado es `Casa Miga POS · Caja · DESARROLLO · 1K.4G-C-C-B1`.

## Smoke humano propuesto

1. Abrir únicamente `work/Abrir-Casa-Miga-1K4G-C-C-B1.cmd` y confirmar el identificador B1 del título.
2. Iniciar sesión con las credenciales entregadas en la conversación y abrir Inventario.
3. Comprobar 67 artículos: 31 materias primas y 36 terminados.
4. Revisar ejemplos MASS (Azúcar estándar), VOLUME (Agua), COUNT (Huevo) y un terminado (Concha de vainilla).
5. Confirmar que los nuevos artículos muestran “sin saldo inicial”, que Harina conserva su presentación y que no aparecen asociaciones de producto.
6. No registrar entradas, saldos, mermas, conteos, ventas, producción, vínculos ni cambios de configuración.

## Plan de C-C-B2

C-C-B2 debe comenzar sólo después de aprobar este smoke. Debe resolver primero vainilla, mezcla multigrano y los bloqueos técnicos del recetario; luego proponer una carga por etapas de fórmulas/perfiles exactos sobre otra copia aislada. Cada etapa debe demostrar consolidación sin doble consumo, preview sin escritura, producción atómica y consumo por venta todavía desactivado. No debe publicar rendimientos, aproximaciones o preparaciones derivadas que el motor actual no represente fielmente.

No hubo cambios de Domain, Application, Infrastructure, Presentation, pruebas o migraciones. La base y el manifiesto permanecen locales e ignorados por Git. No se hizo push y no se inició C-C-B2.
