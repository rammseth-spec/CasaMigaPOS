# FASE 1G Bloque E — checkpoint previo a implementación

Fecha: 2026-09-11. Estado: **DETENIDO ANTES DE IMPLEMENTAR; LOGOTIPO OFICIAL APROBADO REQUERIDO**.

## Punto de partida

- HEAD revisado al iniciar: `48f2895431a5282b7102714ba0598e5a0a9d9372`.
- Bloque D validado físicamente: caracteres españoles, legibilidad, tamaños, alineación, jerarquía, ausencia de basura y corte automático parcial correctos.
- Baseline fijo: .NET `ibm850`/CP850, `ESC t 2` y un único `GS V 1` final.
- Evidencia física preservada sin transformación en `docs/evidence/phase1g-block-d-physical.png`, SHA-256 `FC956953C1BA7707954ED0CB3148C39993B45CC784B033C75CE9FC508CF09040`.

## Resultado de la búsqueda del asset

La búsqueda dirigida en el repositorio no encontró un archivo identificado como logotipo oficial aprobado. `src/CasaMiga.Presentation/Assets/README.md` declara expresamente que el logo oficial está pendiente y ordena no generarlo ni redibujarlo.

Por esa razón no se inició el renderer raster, no se modificó el ticket, no se creó configuración comercial, no se ejecutaron pruebas nuevas y no se envió otra impresión. Código, pruebas y migraciones permanecen intactos.

## Insumo requerido para continuar

Se necesita el archivo fuente oficial y aprobado del logotipo Casa Miga, preferiblemente:

- PNG monocromático con transparencia;
- alta resolución;
- proporciones originales;
- tipografía, estrella, vapor y geometría originales intactos.

Una fuente vectorial oficial también puede conservarse como original, pero la integración ESC/POS requerirá derivar de ella un raster monocromático reproducible. Cualquier conversión debe conservar el archivo fuente y permitir sustituir el asset sin cambios en la lógica.

## Alcance que sigue pendiente

Cuando exista el asset autorizado, el Bloque E podrá implementar el raster de 1 bit centrado con ancho máximo de 576 dots, datos reales configurables sin inventarlos, ticket válido aun sin logo, pruebas deterministas y una sola impresión física sintética controlada. No se ha iniciado cajón, LAN, fiscalidad, QR, promociones, inventario, rediseño, fullscreen, instalador ni una fase posterior.
