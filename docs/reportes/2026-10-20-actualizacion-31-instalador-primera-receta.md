# Reporte de bloque — actualización a la versión 31, recuperación, instalador aislado, documentos y primera receta

Fecha: 20/10/2026. Rama `main`.

## 1. Resultado

**Terminado.** Las cinco partes del bloque están hechas y comprobadas. Quedan dos cosas fuera de él:
- Instalar sobre la caja real: sigue sin autorización, como se pidió.
- Probar el instalador en una computadora con Windows 10/11 limpio: requiere otra computadora.

## 2. Qué cambió y qué puedes hacer ahora

- **Actualizar la caja real a la versión 31 con un procedimiento probado**, y volver atrás si algo falla (`docs/ACTUALIZAR_CAJA.md`).
- **Instalar Casa Miga desde un instalador** (`CasaMigaPOS-1.0.0-setup.exe`):
  - instala, actualiza encima y desinstala sin tocar los datos;
  - nunca lleva el modo de prueba.
- **Completar la primera receta real (polvorón) con sólo 4 datos.** En cuanto lleguen, una herramienta hace el recorrido completo en una copia.
- **El recetario quedó coherente** con el soporte nuevo:
  - la tanda de 13 roles y la masa enriquecida van como preparaciones dentro de otras, sin repartos por pieza;
  - el cargador nunca manda una cantidad «por medir».

## 3. Dónde está

| Qué | Dónde |
|---|---|
| Código y documentos | `C:\Users\ramms\Documents\ChatGPT\CasaMigaPOS-main`, rama `main` (commits en el punto 9) |
| Instalador | `C:\Users\ramms\Documents\ChatGPT\CasaMigaPOS-main\artifacts\installer\CasaMigaPOS-1.0.0-setup.exe` (no se guarda en Git) |
| Copia de revisión, abierta con «DESARROLLO · RECETAS-c29bd63» | Lanzador `C:\Users\ramms\CasaMiga-Revision\Abrir-Casa-Miga-recetas-copia.vbs`; datos en `C:\Users\ramms\CasaMiga-Revision\recetas-copia-20261020` |
| Copias de la prueba de actualización | `C:\Users\ramms\CasaMiga-Revision\prueba-v31-arranque` (en 31), `…\prueba-v31-arranque-antes` (en 30, usada para volver atrás), `…\prueba-v31-20261020` (respaldo restaurado) |
| Instalación aislada | `C:\Users\ramms\CasaMiga-Revision\instalador-prueba`: programa ya desinstalado; quedan sus carpetas de datos de prueba |
| Respaldos de la caja real | `C:\Users\ramms\CasaMiga-Ventas\respaldos\` (`20261020-completo`, `20261020-v31-prueba`) |
| **Caja real** | **Sin cambios**: programa `4e8ec88` en `C:\Users\ramms\CasaMiga-Ventas\programa`, base en versión 30, 23 ventas ($1,633), caja del 19/10 abierta |

## 4. Qué verifiqué y qué no

**Con datos reales** (copias de la caja con sus 23 ventas):
- Respaldo oficial y restauración en una carpeta nueva; también restaura la configuración de la impresora.
- Arranque del programa nuevo sobre una copia en versión 30: respalda antes y después y sube a 31.
- Todas las tablas comparadas renglón por renglón con la base real: sólo cambian el registro de la migración 31 y la tabla nueva, que queda vacía.
- Vuelta atrás:
  - la versión anterior no abre una base en 31 y no la daña;
  - sí abre la copia de la carpeta tomada antes de actualizar.
- Una limitación encontrada: la herramienta anterior **no** restaura un respaldo hecho por la versión nueva. Por eso se vuelve con la copia de la carpeta.
- Instalador sobre una copia de la caja en versión 30: la sube a 31 y quedan iguales las ventas, la caja abierta y los demás datos.
- La tanda de rol se capturó con sus dos preparaciones en la copia de revisión, y su publicación se rechazó mientras sus masas siguen en borrador.

**Con datos vacíos o de prueba:**
- Instalación aislada desde cero: sin «DESARROLLO», «Versión 1.0.0», pide crear el primer administrador.
- Instalar encima (borra un `development.settings.json` dejado a mano) y desinstalar (los datos quedan).
- Herramienta del recorrido del polvorón: probada en una copia desechable con el rendimiento **reportado** de 1,800 g, que no está confirmado. Produjo 18 piezas y descontó exactamente lo planeado. **No valida la receta**; la copia se borró.
- Pruebas automáticas (punto 9).

**No verifiqué:**
- El instalador en Windows 10/11 limpios ni en la computadora de la tienda.
- El ticket en papel.
- Cancelar un trabajo real de la cola de impresión.
- El CI de GitHub (falta `gh`).
- Nada sobre la caja real, que no se tocó.

## 5. Recetas, producción e inventario

| | Caja real | Copia de revisión |
|---|---|---|
| Preparaciones | 10 en borrador, 0 publicadas | Las mismas 10, más la tanda de rol en borrador |
| Recetas de pan | 0 | Polvorón en borrador (100 g de masa de polvorón por pieza) |
| Producciones | 0 | 0 |
| Existencias | 0 | 0 |
| Descuento por venta | Apagado | Apagado |

- **Recetario** (`docs/catalog/recetario-borrador.json`): 12 preparaciones y 31 panes.
  - La tanda de rol rinde 13 piezas con 1,100 g de masa dulce y 140 g de relleno.
  - La masa enriquecida lleva masa dulce «por medir» y 80 g de mantequilla. Cuernito, danés y bigote la usan y esperan ese dato.
- **Ninguna receta real está publicada.** La primera candidata es el **polvorón**; sus 4 datos están en el punto 7.
- **Sigue apagado** el descuento por venta, a la espera de recetas aprobadas y del conteo real.

## 6. Avance frente al plan

**Cerrado en este bloque:**
1. Actualización a la versión 31 probada sobre una copia reciente, conservando todos los datos.
2. Forma de volver a la versión anterior, probada y documentada.
3. Instalador generado y probado en una instalación aislada.
4. Documentos del recetario coherentes con el soporte de tandas y preparaciones dentro de otras:
   - `RECETARIO_PARA_COMPLETAR.md`;
   - recetario maestro, matriz y auditoría técnica;
   - `recetario-borrador.json`;
   - `recipe-production-ui.md` y `MANUAL.md`.
5. Lista de datos mínimos para la primera receta real.

**Sigue pendiente** (detalle en `docs/PLAN_SEGUIMIENTO.md`):
- Copia del respaldo fuera del equipo.
- Cierre de la caja del 19/10 con tus cantidades reales.
- Cambiar la contraseña del administrador y desactivar los usuarios de prueba.
- Recetas reales: polvorón primero.
- Degustación, consumo interno y aceite de fritura (necesitan tu decisión).
- Conteo de inventario y, después, encender el descuento por venta.
- Instalar `main` (versión 31) en la caja real, con autorización.
- Instalador en Windows 10/11 limpios.
- Pruebas físicas.
- Publicar la nube y revisar el CI.
- Decisiones abiertas: descuentos e impuestos, pagos mixtos, corte impreso, cajón, compras y proveedores, pedidos, reglas de devolución y fotos reales.

## 7. Qué necesito de ti

1. **Los 4 datos del polvorón:**
   - peso de la masa cruda de una media tanda, pesada completa: ____ g;
   - con menos de una media tanda, ¿huevo y naranjas se redondean hacia arriba, o sólo se hacen medias tandas completas?;
   - sabor por omisión: ¿naranja o vainilla?;
   - cuántos polvorones salen de esa media tanda.
2. **Autorización y día para actualizar la caja real a la versión 31.** Conviene al inicio del día, antes de vender.
3. **Las cantidades reales para cerrar la caja del 19/10.**

## 8. Siguiente bloque

- **Con los 4 datos:** recorrido completo del polvorón en una copia (publicar, existencias de prueba, producir, comprobar descuento y entrada de pan). Después, con tu aprobación, en la caja real.
- **Con la autorización:** actualizar la caja real con `docs/ACTUALIZAR_CAJA.md` y dejarla abierta para revisión.
- **Mientras respondes, sin datos tuyos:**
  - ordenar los documentos que se contradicen (`AGENTS.md`, `README.md`, `roadmap.md`, `RELEASE_NOTES_1.0.md`);
  - preparar en la copia las recetas de los rellenos completos (rol y pay) para cuando sus masas tengan datos.

## 9. Respaldo técnico

**Commits en `main`:**
- `c1a84b4`: actualización probada, herramienta de la primera receta y recetario coherente.
- `65244de`: instalador probado.
- El commit de este reporte (más abajo).

**Pruebas:**
- `scripts/verify.ps1` sobre `2604333` (último cambio de código de la caja): 760/762 generales (2 omitidas, que requieren la nube real) y 245/245 WPF.
- En este bloque no cambió código de la caja.
- Nube: `npm test` 77/77, navegador 11/11, `tsc` limpio, `carga.test.ts` 2/2 con la carga por etapas.

**Instalador:**
- `CasaMigaPOS-1.0.0-setup.exe`, 72 MB, SHA-256 `B3BD35F4EAC676293921514068E54E107849D1272AB80E8F5E941C3330BA45CD`.
- Generado con Inno Setup 6.7.3, instalado para el usuario con winget.

**Archivos:**
- `docs/ACTUALIZAR_CAJA.md`, `docs/INSTALLER.md`, `docs/RECETAS_ESTADO.md` y `docs/PLAN_SEGUIMIENTO.md`.
- `tools/recorrido-receta/`.
- `cloud/scripts/carga-plan.ts`, `cloud/scripts/recetario.ts` y `cloud/scripts/build-demo-data.ts`.
- `docs/catalog/recetario-borrador.json`.
- Este reporte: `docs/reportes/2026-10-20-actualizacion-31-instalador-primera-receta.md`.
