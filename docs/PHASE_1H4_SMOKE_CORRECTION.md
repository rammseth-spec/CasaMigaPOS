# FASE 1H.4 — corrección de unidades y comprobación de Manager

FASE 1H abierta. 1H.4 completada y aprobada mediante smoke WPF. 1H.5 no inició. Versión de desarrollo, no lista para producción.

Baseline de este bloque: dd7d5b5c67638c75eca81e08a6fbb9184d9ff89c. La UI inicial está en 845b8a8; dd7d5b5 corrigió el binding bidireccional de OperationId que terminaba WPF al iniciar.

## Manager

La inspección de la instalación sintética work/phase1h4-ui-smoke-20260912 confirmó SMOKEMANAGER activo, rol Manager y permisos inventory.view/receive/waste, sin configure/adjust. SMOKE-H14 estaba activo e inicializado con 1500000 unidades atómicas de masa. Los eventos no mostraron un rechazo por permisos. El usuario confirmó posteriormente «SI FUNCIONA» al preguntar por Entrada dentro de Ver detalle. No se reprodujo un defecto de autorización y no se cambió la matriz ni Application.

La prueba nueva ejecuta RECEIPT y WASTE desde el ViewModel con Manager en una base temporal; verifica saldo, restricciones de configuración/opening/count y revalidación real tras revocar inventory.receive. Se conservan las pruebas Administrator/Cashier y de permisos Application.

## Unidades

InventoryDisplay convierte sólo la representación visual y captura de configuración con decimal checked. Masa ofrece g/kg, volumen ml/L, conteo pieza. mg y uL permanecen internos. Lista, detalle, historial, presentaciones y asociaciones muestran unidades humanas exactas. Desde 1000000 unidades atómicas se usa kg/L; por debajo, g/ml. No hay redondeo: 1 unidad atómica se representa como 0.001 g/ml. El rango Int64 cabe exactamente con hasta seis decimales en kg/L.

La configuración de presentaciones y asociaciones captura g/ml/pieza y convierte exactamente al contrato atómico existente; rechaza precisión no representable. Las presentaciones siguen capturándose en cantidades enteras al registrar movimientos, según Application. No se modificaron Domain, persistencia, ledger, InventoryBalance, migraciones 001–010, Checkout, Sale, Payment, ExpectedCash, tickets ni impresión.

## Verificación y aceptación final

35 pruebas focalizadas UI/Application aprobadas, ninguna fallida ni omitida. El verificador completo aprobó 290 pruebas, 0 fallidas y 0 omitidas; Release terminó con 0 errores y 0 advertencias. Resultado en docs/evidence/phase1h4-smoke-correction.txt.

El usuario declaró `SMOKE APROBADO`. La aceptación manual confirmó acceso y operación de Manager conforme a permisos y las unidades visibles aprobadas. La prueba automatizada confirma específicamente que Manager ejecuta RECEIPT y WASTE y que no puede configurar, abrir saldo ni ajustar; Cashier permanece sin acceso. No se cambió la matriz de permisos.

SQLite conserva `SMOKE-H14` activo, 1,500,000 mg internos mostrados como 1.5 kg, cuatro movimientos (`OPENING_BALANCE`, `RECEIPT`, `WASTE`, `ADJUSTMENT_GAIN`) y reconciliación exacta 1,500,000 = 1,500,000. No se generó una operación adicional sólo para cerrar documentación. No se usó impresora.

Archivos de producto de este bloque: InventoryDisplay.cs (nuevo), InventoryViewModel.cs e InventoryView.xaml. Pruebas: InventoryViewModelTests.cs y CasaMiga.Tests.csproj. Continuidad: README, CURRENT_STATE, NEXT_STEPS, FILES, este reporte y evidencia.
