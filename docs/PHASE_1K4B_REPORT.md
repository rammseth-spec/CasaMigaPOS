# CASA MIGA POS — FASE 1K.4B

**Estado:** técnicamente completada; smoke manual pendiente. Este checkpoint no cierra FASE 1K, no inicia 1K.4C y no se publica todavía en GitHub.

1. **Modelo realmente utilizado:** GPT-6.
2. **HEAD inicial:** `0d9953669ad7d0eceef50c38cec8f1a52c7fa96a`.
3. **Git inicial:** rama `master`, working tree limpio y `origin/QA` en el mismo commit 1K.4A.
4. **Alcance:** se modificó únicamente Presentation, sus pruebas WPF y la documentación exigida.
5. **Auditoría inicial:** cada partida ocupaba tres bloques verticales, no mostraba resumen de productos/piezas y una nueva fila fuera de vista no recibía desplazamiento contextual.
6. **Estructura final:** el carrito ocupa su columna durante búsqueda, categorías y catálogo, aprovechando la altura operativa sin quitar espacio horizontal al catálogo.
7. **Densidad:** cada partida reserva 82 DIP, cercana al objetivo operativo de 85–100 DIP y sin recortar controles.
8. **Capacidad 1280×820:** la prueba usa el área real disponible tras shell y confirma al menos cuatro partidas completas antes de requerir scroll.
9. **Capacidad 1920×1080:** aumenta la cantidad visible y mantiene la misma jerarquía.
10. **Encabezado:** `Tu carrito` incorpora un resumen dinámico de productos distintos y piezas totales.
11. **Singular:** `1 producto · 1 pieza`.
12. **Plural:** ejemplos verificados `1 producto · 2 piezas` y `2 productos · 3 piezas`.
13. **Vacío:** `0 productos · 0 piezas`, mensaje humano y totales `$0.00`.
14. **Nombre:** máximo visual de dos renglones, wrap y ellipsis después del espacio reservado.
15. **Precio unitario:** conserva el formato monetario existente y el sufijo `/ pieza`.
16. **Importe de partida:** se mantiene destacado a la derecha y procede del `CartLine.LineTotal` existente.
17. **Cantidad:** queda centrada entre disminuir e incrementar.
18. **Disminuir:** botón compacto de 40×40 DIP, tooltip y nombre accesible.
19. **Incrementar:** botón compacto de 40×40 DIP, tooltip y nombre accesible.
20. **Eliminar:** icono de papelera, tooltip `Eliminar producto del carrito`, nombre accesible y objetivo de 40×40 DIP.
21. **Semántica al llegar a 1:** el botón menos queda deshabilitado; quitar la partida requiere Eliminar. No se cambió Domain.
22. **Producto repetido:** vuelve a incrementar la fila existente; nunca crea una segunda fila con el mismo ProductId.
23. **Producto distinto:** crea una fila independiente y actualiza ambos contadores.
24. **Nueva fila fuera de vista:** se llama `BringIntoView` sólo después de agregar un producto nuevo; el framework desplaza lo mínimo necesario.
25. **Fila ya existente:** incrementar no fuerza el scroll ni cambia la posición de lectura.
26. **Scroll:** sólo las partidas se desplazan verticalmente; el desplazamiento horizontal está deshabilitado.
27. **Pie fijo:** Subtotal, TOTAL y acciones permanecen inmóviles mientras se recorren las partidas.
28. **Subtotal:** conserva exactamente `Cart.Subtotal` y el formateo `es-MX` existente.
29. **TOTAL:** conserva exactamente `Cart.PreliminaryTotal`; no se introdujo descuento, impuesto o redondeo.
30. **Acciones:** Limpiar, Cobrar y Cobrar + imprimir permanecen visibles en una franja compacta.
31. **Carrito vacío:** las tres acciones quedan deshabilitadas.
32. **Limpiar:** conserva confirmación explícita y descarte temporal; no persiste una venta.
33. **Cobrar:** flujo existente intacto.
34. **Cobrar + imprimir:** flujo existente intacto; no se envió ningún trabajo físico.
35. **Clics rápidos:** las mutaciones del carrito se serializan en Presentation con un `SemaphoreSlim`; cada comando aceptado se ejecuta una vez y en orden.
36. **Prueba concurrente:** seis agregados concurrentes producen una sola fila con seis piezas y total `$78.00`; tres incrementos posteriores producen nueve piezas y `$117.00`.
37. **Overflow:** conserva el mensaje y la protección de aritmética checked existente.
38. **Búsqueda POS:** búsqueda en vivo, botón, Enter, × Limpiar y categoría preservada siguen intactos.
39. **Escape:** la regresión contextual del buscador y popups se mantiene.
40. **F11:** no se modificó MainWindow ni la lógica de pantalla completa.
41. **Catálogo visual:** cards, foto, nombre, precio, categorías y paginación no cambiaron funcionalmente.
42. **Archivos de producto:** `PosView.xaml`, `PosView.xaml.cs`, `PosViewModel.cs` y `Resources/Theme.xaml`.
43. **Pruebas nuevas:** `PosCartUsabilityTests.cs` cubre vacío, altas, repetición, cantidades, plurales, totales, menos en 1, quitar, limpiar, concurrencia, layout, scroll y pie fijo.
44. **Pruebas focales:** carrito 1K.4B más regresiones POS 1K.4A y Popup/Escape.
45. **Resultado focal:** 16/16, 0 fallidas y 0 omitidas.
46. **Suite completa:** `scripts/verify.ps1`.
47. **Resultado suite:** 731/731 = 560 generales + 171 WPF/Presentation; 0 fallidas y 0 omitidas.
48. **Release:** 0 errores y 0 advertencias.
49. **Salida:** `src/CasaMiga.Presentation/bin/Phase1K4B/` con metadata `CasaMigaReviewBuild=1K.4B`.
50. **Launcher:** `work/Abrir-Casa-Miga-1K4B.cmd`.
51. **DataDirectory:** `work/phase1k4b-smoke-20261006`.
52. **Título esperado:** `Casa Miga POS · Caja · DESARROLLO · 1K.4B`.
53. **Fixture:** copia aislada y coherente de 1K.4A; credencial verificada contra esa copia.
54. **Conteos protegidos en la fixture:** ventas 16/16, pagos 16/16, sesiones 2/2, conteos de caja 1/1, solicitudes de impresión 17/17, operaciones de inventario 1/1, movimientos 1/1 y producción 0/0.
55. **Base protegida inicial:** SHA-256 `CFEE3F12175EC5C7EAFF636CE5CD6599AE8C754958201A90D14C80E5B008EB6D`.
56. **Base protegida final:** el mismo SHA-256; byte-idéntica.
57. **Schema:** 16.
58. **Migración 017:** no existe y no se creó.
59. **Capas protegidas:** Domain, Application e Infrastructure sin cambios.
60. **Operaciones:** no se confirmó venta, pago, caja, inventario, producción, ledger ni impresión.
61. **GitHub:** no se hizo fetch, push ni PR durante 1K.4B; `origin/QA` continúa en 1K.4A. El próximo checkpoint remoto está previsto después de 1K.4D.
62. **Commit local:** único mensaje autorizado `feat: improve cart usability for phase 1K.4B`; HEAD final se entrega en la conversación tras crearlo.
63. **Apertura final:** después del commit se verifica launcher → EXE → DataDirectory → título y se deja la aplicación en login, sin autenticar ni operar.
64. **Pendiente:** smoke humano del carrito; no iniciar 1K.4C, 1K.4D, Pedidos/Tiendas, instalador o migración 017 sin nueva autorización.

## Datos para revisión

Las credenciales verificadas corresponden únicamente a la copia aislada y se entregan en la conversación, no en documentación versionada.
