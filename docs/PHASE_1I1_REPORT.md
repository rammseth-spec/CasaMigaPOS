# CASA MIGA POS — REPORTE FINAL FASE 1I.1

Fecha: 2026-09-12. El commit que contiene este documento fija el checkpoint oficial de 1I.1.

1. **FASE 1I:** abierta únicamente en su subfase 1I.1.
2. **1I.1:** completada técnicamente.
3. **Modelo:** Codex configurado para la sesión; no hubo escalamiento adicional de modelo.
4. **HEAD:** inicial `71a4f60159ca167cbc316956c848b2082cdf84a5`; final, commit que contiene este reporte.
5. **Commit:** uno, acotado a Domain, pruebas y documentación de 1I.1.
6. **Git final:** debe quedar limpio después del commit; sin remoto ni push.
7. **Archivos nuevos:** modelo/política, pruebas, ADR, reporte y evidencias 1I.1.
8. **Archivos modificados:** Inventory.cs y continuidad documental; ningún archivo de Checkout o Infrastructure.
9. **SALE_CONSUMPTION Domain:** reservado y construible sólo desde un plan validado; schema 10 aún lo rechaza.
10. **Factory:** `SaleConsumptionOperationFactory` produce exclusivamente deltas negativos y rechaza planes sin consumo.
11. **Plan:** resultado puro NoConsumption/ConsumptionRequired con detalle y agregados.
12. **TrackInventory/link:** false+sin link no consume; true+sin link y false+con link son configuración inválida.
13. **Producto sin link:** vende sin consumo únicamente cuando TrackInventory=false.
14. **Link incoherente:** bloqueado mediante error estructurado.
15. **Item inactivo:** InventoryItemInactive.
16. **No inicializado:** InventoryNotInitialized; null se distingue de saldo cero.
17. **Stock insuficiente:** bloquea el plan completo e informa requerido/disponible.
18. **Cálculo:** multiplicación y suma Int64 checked, sin float/double.
19. **Agregación:** InventoryLocation+InventoryItem, sin depender del orden del carrito.
20. **Snapshot:** posición, producto, cantidad, tracking, link, item, location, dimensión, factor y total.
21. **Orden:** líneas por posición; agregados por LocationId y luego InventoryItemId.
22. **Errores:** configuración, no inicializado, insuficiencia, item inactivo y overflow.
23. **Cashier:** se documenta que sales.complete autorizará el efecto automático sin permisos administrativos de inventario.
24. **Idempotencia futura:** CheckoutRequest.OperationId será raíz; algoritmo derivado queda para 1I.2.
25. **Feature flag:** disabled/enabled/paused documentados; no implementados.
26. **Preflight:** requisitos documentados; no implementado.
27. **Migración 011:** necesaria y pendiente; no creada.
28. **Pruebas focalizadas:** 10/10 nuevas y 62/62 ampliadas.
29. **Suite:** 300 aprobadas, 0 fallidas, 0 omitidas.
30. **Build:** Release correcto, 0 errores, 0 advertencias.
31. **Migraciones:** 001–010 intactas y hashes verificados.
32. **Checkout:** CheckoutService sin cambios funcionales.
33. **ExpectedCash:** sin cambios.
34. **Tickets/impresión:** snapshots, impresión y reimpresión sin cambios; impresora no utilizada.
35. **Riesgos:** 1I.2 deberá resolver schema, triggers, transacción compartida, activación e idempotencia persistente.
36. **Pendiente 1I.2:** diseño exacto de tablas, estado de activación, algoritmo de OperationId y orden SQL atómico.
37. **1I.2:** no iniciada.
38. **Próximo paso:** revisar y autorizar 1I.2 antes de crear migración 011.
39. **Modelo recomendado para 1I.2:** Astra, por migración estructural y frontera transaccional Checkout–Inventory.

FASE 1I no está completa. No existe persistencia ni consumo automático en Checkout.
