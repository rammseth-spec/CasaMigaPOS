# Reporte de bloque — documentos coherentes, borradores del rol y el pay, y recorrido del polvorón desde las pantallas

Fecha: 20/10/2026. Rama `main`.

## 1. Resultado

**Terminado.** Las tres tareas autorizadas están hechas y comprobadas. El recorrido real del polvorón espera tus 4 medidas, y la instalación en la caja real sigue sin autorización.

## 2. Qué cambió y qué puedes hacer ahora

- **Documentos sin contradicciones.** `AGENTS.md`, `README.md`, `roadmap.md`, `RELEASE_NOTES_1.0.md`, `NEXT_STEPS.md` y `PILOTO.md` ya dicen lo mismo que `main`:
  - qué tiene, qué no se hace sin autorización y dónde está la caja real;
  - que la nube es opcional y no está publicada;
  - que el descuento por venta está apagado y pendiente, no «opcional».
- **La nube tiene su registro de decisión:** `docs/decisions/ADR-024-nube-y-version-web.md`.
- **Recetas del rol y del pay en borrador** en la copia de revisión:
  - el rol es 1 pieza de la tanda de 13;
  - el pay es 1 relleno + 20 g de masa de galleta.
- **Puedes hacer el recorrido del polvorón desde las pantallas normales**, en la copia, siguiendo `docs/GUIA_RECORRIDO_POLVORON.md`. La copia ya tiene existencias de prueba y el pan terminado en cero.

## 3. Dónde está

| Qué | Dónde |
|---|---|
| Documentos y prueba nueva | `C:\Users\ramms\Documents\ChatGPT\CasaMigaPOS-main` (`main`) |
| Copia de revisión, con los borradores del rol, el pay y el polvorón, y existencias de prueba | Datos en `C:\Users\ramms\CasaMiga-Revision\recetas-copia-20261020`; lanzador `C:\Users\ramms\CasaMiga-Revision\Abrir-Casa-Miga-recetas-copia.vbs` |
| Guía del recorrido | `docs/GUIA_RECORRIDO_POLVORON.md` |
| Herramienta (modos `preparar`, `rellenos`, `pantallas`, `recorrido`) | `tools/recorrido-receta/` |
| **Caja real** | **Sin cambios**: programa `4e8ec88`, base en versión 30, 23 ventas ($1,633), caja del 19/10 abierta |

## 4. Qué verifiqué y qué no

- **Con datos reales, en la copia de la caja:**
  - se crearon los borradores del rol y del pay con sus preparaciones reales: tanda de rol, relleno de pay y masa de galleta;
  - se cargaron existencias de **prueba** para el polvorón; cada movimiento dice «PRUEBA · recorrido del polvorón desde las pantallas».
- **Con datos de prueba:** la prueba automática nueva `TheFirstRecipeRouteWorksFromTheNormalScreens` recorre las mismas pantallas que usarás:
  - Recetas: crear la masa con sabor alternativo, editar el borrador con el rendimiento medido y el sabor por omisión, publicar y activar;
  - Perfiles: crear la receta del pan, publicar y activar;
  - Producción: calcular, crear y confirmar 18 piezas.
  - Resultado: cada ingrediente bajó lo esperado y entraron 18 panes.
- **Hallazgo de la prueba:** al editar un borrador, el rendimiento guardado de 1,800 g aparece como «1.8 kg». Si escribes gramos sin cambiar la unidad, se guarda en kilos. No es un error del programa, pero es una trampa fácil; la guía lo advierte en el paso 1.3.
- **No verifiqué:** el recorrido con tus medidas reales (faltan), ni nada en la caja real.

## 5. Recetas, producción e inventario

| | Caja real | Copia de revisión |
|---|---|---|
| Preparaciones | 10 en borrador | 10 + tanda de rol, todas en borrador |
| Recetas de pan | 0 | 3 en borrador: polvorón, rol de canela y pay de queso |
| Producciones | 0 | 0 |
| Existencias | 0 | Sólo de prueba, para los ingredientes del polvorón, y 0 de polvorón terminado |
| Descuento por venta | Apagado | Apagado |

Nada está publicado. Los datos sin confirmar siguen pendientes:
- del polvorón, las 4 medidas;
- de la masa dulce, el rendimiento y la unidad de la vainilla;
- de la masa enriquecida, cuánta masa dulce lleva;
- de la manteca, el tipo;
- del pay, cómo contar el huevo;
- de glaseado, polvorón, galleta, muerto, costra, masa salada y donas, el rendimiento.

## 6. Avance frente al plan

**Cerrado en este bloque:**
- Contradicciones entre documentos (lista en `PLAN_SEGUIMIENTO.md`, «Documentos que se contradicen»).
- ADR de la nube.
- Borradores del rol y del pay.
- Recorrido del polvorón listo en la copia, desde las pantallas y comprobado con datos de prueba.

**Sigue pendiente:**
- Puerquito y CM-018 activos aunque el recetario los da por retirados (decisión tuya).
- Copia del respaldo fuera del equipo.
- Cierre de la caja del 19/10.
- Contraseña del administrador y usuarios de prueba.
- Recetas reales (polvorón primero).
- Degustación, consumo interno y aceite de fritura.
- Conteo de inventario y descuento por venta.
- Instalar la versión 31 en la caja real.
- Instalador en Windows 10/11 limpios.
- Pruebas físicas.
- Publicar la nube y revisar el CI.
- Decisiones abiertas.

## 7. Qué necesito de ti

1. Las **4 medidas del polvorón** (`docs/RECETAS_ESTADO.md`).
2. **¿Desactivo Puerquito y el bollo CM-018?** El recetario los da por retirados y en la caja siguen activos.

## 8. Siguiente bloque

- **Con las medidas:** recorrido del polvorón en la copia, desde las pantallas (tú, con la guía) o con la herramienta (yo). Luego tu aprobación para la caja real.
- **Mientras respondes:**
  - preparar en la copia el borrador del pan de muerto y de las galletas, que ya tienen su receta por pieza documentada, para que sólo falte publicar al llegar los rendimientos;
  - revisar la ayuda de la pantalla Recetas para que avise de la unidad del rendimiento al editar.

## 9. Respaldo técnico

- **Commit:** ver el final del chat de este bloque.
- **Pruebas:**
  - `scripts/verify.ps1` completo: 762/763 generales (1 omitida, la de volumen de un año) y 245/245 WPF; compilación Release limpia;
  - prueba nueva `TheFirstRecipeRouteWorksFromTheNormalScreens`, incluida en lo anterior.
- **Archivos nuevos o cambiados:**
  - `AGENTS.md` y `README.md`;
  - en `docs/`: `roadmap.md`, `RELEASE_NOTES_1.0.md`, `NEXT_STEPS.md`, `PILOTO.md`, `PLAN_SEGUIMIENTO.md`, `RECETAS_ESTADO.md`, `GUIA_RECORRIDO_POLVORON.md` y `decisions/ADR-024-nube-y-version-web.md`;
  - `tools/recorrido-receta/Program.cs`;
  - `tests/CasaMiga.Tests/RecipeProductionViewModelTests.cs`.
- **Este reporte:** `docs/reportes/2026-10-20-documentos-rellenos-recorrido-pantallas.md`.
