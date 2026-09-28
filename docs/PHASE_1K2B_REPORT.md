# CASA MIGA POS — REPORTE FINAL FASE 1K.2B

Fecha de cierre: 28/09/2026. Product Photo Foundation completada y verificada. Este reporte documenta la implementación y sus límites; no declara el POS completo listo para producción.

## Estado y Git

1. **FASE 1K:** abierta; 1K.1 continúa cerrada/aprobada.
2. **1K.2:** abierta; 1K.2A aprobada, infraestructura 1K.2B terminada.
3. **1K.2B:** completada, sin UI fotográfica final.
4. **Modelo:** Codex basado en GPT-6. La sesión no expone una variante verificable; no se afirma cambio a Astra.
5. **HEAD inicial de producto:** `58bdcbd34692e14ced3f85fdeb82fca5d6b8b794`.
6. **Checkpoint documental 1K.2A:** `089066c04b8a9f69aa6a4468af6f8d146036d576`, `docs: define product photo architecture for phase 1K.2A`. Se comprobó primero que sólo había documentación/probe y Git quedó limpio tras ese commit.
7. **Commit de implementación 1K.2B:** `e872479fafd0389dbc32508f0a632ef331799bb9`, `feat: add recoverable product photo foundation and backup v2`. Cierre documental separado con mensaje `docs: close phase 1K.2B with verification and recovery guidance`.
8. **HEAD final:** commit de cierre documental hijo del commit de implementación; su SHA exacto se entrega en el reporte de conversación para evitar referencia circular del archivo a su propio commit.
9. **Git final:** cierre preparado para árbol limpio, con comprobación posterior al commit informada en conversación. Identidad local Rammseth / rammstudioworkshop@gmail.com. Sin push, remoto nuevo ni reescritura.
10. **Schema final:** 16 en compilación y fixtures/restores; no se migró manualmente la instalación del usuario.
11. **016 SHA-256:** `9223CAA06CD02FA32E874CC1516EE0C6DB798CB76E2FBE1BF267BAB9B695ED76`.
12. **001–015:** 15/15 hashes intactos frente a evidencia 1K.1. Registro completo: `evidence/phase1k2b-migration-hashes.txt`.

## Modelo, procesamiento y Application

13. **DDL:** dos tablas STRICT (`product_photo_asset`, `product_photo`) y cuatro triggers: metadata original inmutable, no borrado de metadata/selección y revisión consecutiva. FK compuesta protege propiedad. Diferencias autorizadas frente al diseño: provenance y original_name con constraints; la metadata histórica no implica retener archivos retirados.
14. **Product→Photo:** cero o una vigente, mismo ProductId durante todos los reemplazos; sin cambio a Domain/Product ni reglas comerciales.
15. **PROVISIONAL/REAL:** obligatorio en asset, consultable por API; sustitución por foto REAL crea otro asset del mismo Product.
16. **Original metadata:** nombre informativo sin ruta, formato detectado, SHA-256, bytes, width/height, orientación, crop, fechaUTC, actor/terminal y versiones. Original activo preservado byte por byte.
17. **Storage root:** `DataDirectory/product-images/v1/<PhotoId GUID N>/`; `original.jpg|png` y `pos-v1-<SHA256>.jpg`. Staging separado; ningún nombre personal/producto determina rutas.
18. **Crop:** X/Y/Edge enteros exactos, cuadrado y dentro de imagen orientada; mínimo 256 px. Persistido sin modificar original.
19. **EXIF:** orientaciones 1–8, giros/reflejos antes del crop. Las ocho variantes y crop rectangular orientado están probadas.
20. **Thumbnail:** JPEG cuadrado, máximo 512×512, sin upscale; original de 256 genera 256. Reapertura para verificar formato/dimensiones.
21. **Calidad:** JPEG 85, promedio de área para reducción, sin filtros estéticos.
22. **PNG alfa:** composición sobre Fondo Natural `#FDF8EF`; prueba de canales de color del JPEG. Derivado nuevo sin EXIF/GPS; original mantiene bytes.
23. **Import validation:** magic/decoder WIC, decode completo, un frame, PNG CRC/estructura, rechazo de APNG/corruptos/formatos no admitidos; no confiar en extensión. Perfil disponible convertido a sRGB; error de codec/perfil rechaza.
24. **Límites:** 25 MiB original, 48 MP, 12000 px/lado; derivado 2 MiB; activos 5 GiB con reserva conservadora y 10000 asociaciones. No certificación universal de seguridad de WIC.
25. **Application API:** Current, ReadThumbnail, Import(set/replace), Remove, Recover, Repair y Reconcile; puertos de metadata/archivos/codec. Codec Presentation no escribe SQLite/filesystem.
26. **Permisos:** catalog.view lectura; products.edit modificación/repair/conciliación. Usuario activo y terminal; revalidación dentro del commit. Inactivos permiten administración sin reactivación. Manager/Administrator editan; Cashier consulta.
27. **Audit:** product.photo.set/replace/remove/repair; OperationId como auditPK; IDs/hashes/metadata, sin bytes de imagen ni ruta fuente.

## Integridad y recuperación

28. **Replace:** procesar→staging→Flush/cierre→hash→move bundle nuevo→transacción/reautorización/CAS/metadata/selección/audit→commit→limpieza anterior. Nunca publica puntero a carpeta aún no creada.
29. **Remove:** punteroNULL, revisión+1 y auditoría atómicos; Product intacto; cleanup de binario sin referencia después del commit. Retry recupera resultado sin duplicarlo.
30. **Fallos DB/filesystem:** rollback conserva foto previa; publicación sin commit deja huérfano; commit incierto se resuelve mediante Recover/OperationId sin requerir fuente de nuevo. No se promete ACID entre SQLite y filesystem ni supervivencia a todo corte eléctrico.
31. **Orphan cleanup:** preview/limpieza explícitos, gate, referencias autoritativas, staging separado y antigüedad mínima 24 h. Protege activos/operación en curso; limpia derivados viejos. Si DB falla, no purga. Sin retención perpetua de retirados ni cuarentena adicional.
32. **Concurrencia:** gate por instalación+DirectoryLease entre procesos; orden gate→DB; CAS de revisión. Probadas dos sustituciones, remove/replace, read/replace y backup/replace, además limpieza/publicación.
33. **Idempotencia:** fingerprint de intención y hash de fuente, actor/terminal/producto; retry con payload distinto falla. Resultado durable consultable aun después de cambio posterior. Simulada pérdida de respuesta tras commit sin duplicar asset/audit.
34. **Cache/invalidation:** sin caché de bitmaps en esta fundación; lectura obtiene puntero/bytes actuales verificados bajo gate. No hay entrada vieja que invalidar. Caché 64 MiB y UI se reservan para 1K.2C.

## Backup y restore

35. **Backup v2:** snapshot SQLite coordinado con gate; archivos enumerados desde asociaciones del snapshot; original y derivado activos; validación del ZIP .partial real antes de publicar .cmbackup.
36. **Compatibilidad v1:** se conserva lectura/restauración de paquetes históricos de tres archivos; prebackup de schema 15 sigue v1, postbackup 16 es v2. Probado arranque 15→16.
37. **Backup smoke:** `casamiga-20260928T063554922Z-5cba37a06550404f97b6f2eefb32aefa.cmbackup`, bajo `work/phase1k2b-smoke`.
38. **Tamaño:** 39,304 bytes.
39. **SHA-256:** `128740BB7F048E7BF22B6C1A9053DA61F04C40D6773B312A26C474E86B8C1ED5`.
40. **Manifest:** Version 2, SchemaVersion 16, TerminalId, Files con Path/Bytes/Sha256/Role. Copia íntegra en `evidence/phase1k2b-manifest.json`; cuatro archivos enumerados más el manifiesto.
41. **Media incluida:** originalPNG activo 2,267 bytes y thumbnail JPEG 4,994 bytes del PhotoId `9436f31e-9a25-414c-a8fd-f9a178d41474`; DB 806,912 bytes y terminal 72 bytes antes de compresión.
42. **Exclusión:** binarios retirados/huérfanos/staging no incluidos; audit/metadata histórica sigue dentro de DB. No respaldos anidados ni contraseñas en texto.
43. **Restore v1:** probado, valida/migra a 16, sin inventar fotografías.
44. **Restore v2:** probado, hashes/referencias/SQLite/FK/terminal/schema verificados, sólo destino nuevo; no sobrescribe instalación activa.
45. **Path traversal:** whitelist por rol, GUID/hash, rechazo de ../absolutos/drive/UNC/backslash/ADS/segmentos inválidos y duplicados case-insensitive. Streaming acotado, sin ExtractAll; manifiesto de 4 MiB, 20003 entradas, total 16 GiB/media 5 GiB.
46. **Media faltante/corrupta:** normal backup/restore falla explícitamente; sin éxito degradado. Arquitectura previa no tenía emergency; no se introduce .cmrecovery. Arranque puede detenerse en backup obligatorio si falta media; recuperación controlada documentada.
47. **Regeneración:** Repair explícito con products.edit, original verificado, crop/pipeline; no reescribe original. Publica archivo regenerado antes de actualizar descriptor/audit; rechaza original corrupto. No reparación silenciosa durante lectura.

## Pruebas y smoke

48. **Smoke import:** Product sintético `3e53b52b-3fef-42ff-acae-366aa74f55c2`; original idéntico y derivado legible por decoder.
49. **Smoke replace:** nueva asociación/PhotoId comprobados; mismo Product y cleanup de anterior.
50. **Smoke remove:** sin asociación/null y posteriormente reimport; no eliminaciónProduct.
51. **Smoke backup/restore:** reimport→v2→destino nuevo→foto/hash/original iguales; PhotoId vigente/restaurado coinciden. Evidencia `evidence/phase1k2b-smoke.json`. Cero ventas reales.
52. **Focalizadas:** 86/86 fotografías,0 fallidas/omitidas; smoke final repetido 1/1 para retener paquete y evidencia actual. Incluye límites, EXIF, alfa, crop, permisos, SQLguards, rollback, retries, races y paquetes manipulados.
53. **Suite completa:** `scripts/verify.ps1` ejecutado:621/621 = 535 regresión + 86 fotos, 0 fallidas, 0 omitidas. TRX completos locales y resumen en `evidence/phase1k2b-verification.txt`.
54. **Release:** solución completa,0 errores, 0 warnings; restore locked exitoso. La app abierta bloqueó el primer intento; usuario la cerró y verificación final quedó limpia.
55. **Diff:** git diff --check y stagedcheck aprobados antes del commit producto; revisión final documental sin cambiar código probado.
56. **POS:** regresión dentro de las 535 pruebas existentes; flujo Add/Cart/Checkout sin modificación.
57. **Catalog:** regresión dentro de las 535 pruebas existentes; precio/categorías/actividad intactos; foto separada.
58. **Inventory:** regresión dentro de las 535 pruebas existentes; sin cambios de balances/reglas.
59. **Sales:** regresión dentro de las 535 pruebas existentes; sin cambios transaccionales de venta.
60. **Cash:** regresión dentro de las 535 pruebas existentes; sin cambios de apertura/conteo/cierre.
61. **Printing:** regresión dentro de las 535 pruebas existentes; sin cambio de impresión ni prueba física nueva.
62. **Recipes:** regresión dentro de las 535 pruebas existentes; sin modificación de recetas/versiones.
63. **Production:** regresión dentro de las 535 pruebas existentes; sin modificación Planned/Actual/confirmación/stock.

## Límites y siguiente paso

64. **Riesgos restantes:** validación de calidad con fotos reales, ICC diversos, equipo Windows 10/LTSC, rendimiento de futuro grid y fallos físicos de energía/disco no certificados. Gate serializa procesamiento/backup; puede bloquear otra operación de media. Hash detecta corrupción, no autentica paquetes. Falta original exige backup íntegro/sustitución autorizada.
65. **Restricciones:** desarrollo, sin UI de fotos, caché POS ni rollback histórico de fotos. No tocar migraciones aplicadas ni reparar datos reales a mano. No se abrió aplicación contra datos del usuario; smoke automatizado en instalación aislada, no revisión visual del POS fotográfico.
66. **Carga masiva:** NO realizada; cero importaciones de 26 productos. Sólo cartas técnicas sintéticas, sin fotos IA/stock/Internet.
67. **1K.2C:** NO iniciada; sin ProductCard, grid, categorías visuales ni touch polish.
68. **Próximo paso:** revisar este cierre y autorizar aparte 1K.2C; entonces integrar UI/fallback/crop/caché y datos estructurados/fotos autorizados, preservando estos contratos.
69. **Modelo recomendado 1K.2C:** Sol para UI sobre la fundación verificada; Astra si cambia persistencia, backup o recuperación.

Documentación actualizada: README, CURRENT_STATE, NEXT_STEPS, FILES, architecture, operations, database, ADR-007, adenda 1K.2A, product-photos y evidencia. Trabajo detenido después de este reporte; sin push.
