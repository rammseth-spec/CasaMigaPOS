# Smoke manual 1J.5 — aprobado

Autorización final del usuario reconoce smokes completados y aprobados. Datos sintéticos. Origen: work/phase1j5-smoke-20260926; copia: work/phase1j5-restored-20260926.

1. Manager: fila incompleta rechazada; preview A8/B3 = 1.25 kg, 625 g, 125 ml, 14 piezas. DRAFT persistió tras cierre/reinicio; confirmado reales A8/B3.
2. Lote A2/B3 confirmado A1/B4: menor/mayor que plan, preparación 650 g, consumo 325 g/65 ml/8 piezas.
3. A2/B3 confirmado 0/0 tras advertencia explícita; histórico conserva cero y consumo.
4. DRAFT 18:52 A2/B3 creado con stock suficiente. Administrator registró conteos masa/líquido cero. Manager recibió ambos faltantes (325 g y 65 ml); quedó pendiente. Atomicidad complementada por prueba automatizada comparando todas las filas.
5. Selector de inventario solicitado por usuario, instalado y confirmado visible; usado para elegir artículos.
6. Aplicación cerrada para backup. Restore nuevo aprobado nativamente; todas las tablas idénticas antes del login. Usuario observó cuatro confirmados y DRAFT 18:52 con plan, creador y terminal conservados.
7. Cashier restaurado mostró sólo Caja/Catálogo/POS. Capturas en conversación.

Cero parcial, concurrencia y commit incierto fueron pruebas automatizadas, no manuales. Sin prueba física de impresora. Evidencia: phase1j5-persistent-restore.json; phase1j5-manual-progress.json es snapshot intermedio, no estado final.
