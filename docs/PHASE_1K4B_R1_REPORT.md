# FASE 1K.4B-R1 — reporte técnico

1. **Modelo utilizado:** GPT-6 (Codex).
2. **HEAD inicial:** `c8b5a5642057a56ce34d0af07bd63f99f16f5a58`.
3. **Git inicial:** rama `master`, árbol limpio; `origin/QA` permanecía en `0d9953669ad7d0eceef50c38cec8f1a52c7fa96a`.
4. **Reproducción:** una prueba WPF materializó el catálogo, conservó tarjeta/contenido/scroll y ejecutó agregar, agregar repetido, +, −, eliminar y Limpiar. En baseline registró 12 transiciones `IsEnabled` y 13 notificaciones globales/de catálogo.
5. **Causa raíz:** cada mutación activaba `Busy` y emitía `PropertyChanged(null)` al iniciar y terminar. El `Grid` raíz se deshabilitaba, las tarjetas heredaban ese estado y `ProductTile` cambiaba opacidad; la notificación global reevaluaba además todo el POS.
6. **Archivo responsable:** `src/CasaMiga.Presentation/PosViewModel.cs`; el efecto visual se propagaba desde `src/CasaMiga.Presentation/PosView.xaml`.
7. **Solución:** notificaciones específicas para el carrito, referencia del catálogo preservada y estado `CartBusy` separado. El carrito/checkout y el cierre permanecen protegidos mientras el catálogo sigue estable.
8. **Sin hacks:** no hay delays, timers, ocultar/mostrar, cambios artificiales de opacidad, congelamiento ni doble buffering.
9. **ItemsSource/colección:** la prueba demuestra igualdad de referencia de `Products` e `ItemsSource`, sin notificación global ni de `Products`.
10. **Imágenes:** no se vuelven a cargar ni se recrean por mutaciones del carrito; el contador de carga queda constante y el contenido de tarjeta conserva referencia.
11. **Agregar producto:** actualiza una fila y los importes sin refrescar catálogo.
12. **Producto repetido:** consolida cantidad en la misma línea sin refrescar catálogo.
13. **Incrementar (+):** actualiza cantidad/totales sin refrescar catálogo.
14. **Disminuir (−):** actualiza cantidad/totales sin refrescar catálogo.
15. **Eliminar:** quita la línea sin refrescar catálogo.
16. **Limpiar:** vuelve al estado vacío mediante notificaciones exclusivas del carrito.
17. **Búsqueda:** valor preservado por toda la secuencia.
18. **Categoría:** selección preservada por toda la secuencia.
19. **Scroll:** desplazamiento preservado dentro de tolerancia de 0.5 DIP.
20. **Página:** etiqueta/página preservada.
21. **Totales:** pruebas vigentes validan productos, piezas, subtotal y total; los comandos concurrentes siguen serializados.
22. **Pruebas focales:** `PosCartUsabilityTests`, `PosCatalogUsabilityTests` y `PopupEscapePresentationTests`.
23. **Resultado focal:** 17/17 aprobadas, 0 fallidas y 0 omitidas.
24. **Suite completa:** `scripts/verify.ps1`; verificación final sobre el código definitivo.
25. **Resultado:** 732/732 (560 generales + 172 WPF), 0 fallidas y 0 omitidas. Una corrida previa tuvo un fallo intermitente ajeno en apertura de ComboBox; pasó 3/3 aislada antes del cierre limpio final.
26. **Release:** build Phase1K4B-R1 con 0 errores y 0 advertencias.
27. **git diff --check:** correcto.
28. **Hash inicial de base protegida:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
29. **Hash final:** `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
30. **Igualdad:** hashes idénticos; la base protegida no fue modificada.
31. **Schema:** 16 en la copia aislada verificada.
32. **Migración 017:** no existe.
33. **Domain:** sin cambios.
34. **Application:** sin cambios.
35. **Infrastructure:** sin cambios.
36. **Ventas:** ninguna venta creada o confirmada.
37. **Caja:** ninguna apertura, cierre o conteo ejecutado; los conteos protegidos de la fixture permanecen iguales.
38. **Impresión:** no se envió ningún trabajo.
39. **EXE R1:** `src/CasaMiga.Presentation/bin/Phase1K4B-R1/CasaMiga.Presentation.exe`; su hash final se registra en la evidencia local de entrega porque el build posterior al commit incorpora el HEAD definitivo.
40. **Launcher R1:** `work/Abrir-Casa-Miga-1K4B-R1.cmd`.
41. **DataDirectory:** `work/phase1k4b-r1-smoke-20261007`; credencial verificada, 28 fotos activas (25 PROVISIONAL, 3 REAL), placeholder y nombre largo.
42. **Título:** `Casa Miga POS · Caja · DESARROLLO · 1K.4B-R1`.
43. **Launcher → EXE:** el launcher apunta exclusivamente a `bin/Phase1K4B-R1/CasaMiga.Presentation.exe` y limpia `CASAMIGA_DATA_DIR` para usar la configuración embebida en la carpeta.
44. **Apertura automática:** se realiza después del commit y las verificaciones finales.
45. **Estado de apertura:** la aplicación se deja en login, sin iniciar sesión ni escribir credenciales.
46. **Commit local:** `fix: prevent catalog flicker during cart updates`.
47. **HEAD final:** se registra al crear el commit local.
48. **Git final:** debe quedar limpio tras el commit.
49. **Push:** no se realiza.
50. **Próximo checkpoint GitHub:** después de 1K.4D, conforme a la autorización.
51. **Riesgo pendiente:** observación humana breve del parpadeo en el equipo; la causa técnica está cubierta automáticamente.
52. **Checklist manual:** login; POS; agregar A; repetir A; agregar B; +; −; eliminar B; verificar catálogo estable; no cobrar; salir.
53. **1K.4C:** no se inicia automáticamente.
