# Recorrido del polvorón desde las pantallas — guía

Para hacerlo **en la copia de revisión**, no en la caja real, el día que tengas las 4 medidas (`docs/RECETAS_ESTADO.md`, «Primera receta real»). La copia ya está preparada:
- la masa de polvorón y la receta del pan, en borrador;
- existencias de **prueba** de sus ingredientes, marcadas «PRUEBA · recorrido del polvorón desde las pantallas»;
- el pan terminado en cero.

El mismo recorrido está comprobado con datos de prueba por la prueba automática `TheFirstRecipeRouteWorksFromTheNormalScreens`.

**Abrir:** `C:\Users\ramms\CasaMiga-Revision\Abrir-Casa-Miga-recetas-copia.vbs`. El título debe decir «DESARROLLO · RECETAS-…». Entra como administrador (el usuario y la contraseña se dan en la conversación).

## 1. La masa, con tu medida (Recetas)

1. **Recetas** → pestaña **Recetas** → selecciona **Masa de polvorón** → **Ver detalle**.
2. Selecciona la versión 1 (Borrador) → **Editar borrador**.
3. **Rendimiento:** escribe el peso medido de la media tanda. **Revisa la unidad:** el borrador muestra «1.8 kg». Si escribes gramos, cambia la unidad a **g**.
4. **Huevo** (ingredientes directos) → *Política de cantidad*:
   - **Redondear hacia arriba**, si con menos de una media tanda se pone el huevo completo;
   - **Sólo piezas completas**, si sólo se hacen medias tandas.
   - Lo mismo para la **Naranja** en *Grupos de alternativas*.
5. **Sabor por omisión** (*Grupos de alternativas*): la *Opción 1* es la que se usa. Para que sea la otra, selecciona la que hoy es Opción 1 → **Quitar opción**. Luego selecciona la que queda → **Agregar opción al grupo** y vuelve a capturar la que quitaste (artículo, cantidad y unidad).
6. **Guardar borrador** → selecciona la versión → **Publicar versión** → **Activar versión**.

## 2. La receta del pan (Recetas → Perfiles de producto)

1. Pestaña **Perfiles de producto** → selecciona **Polvorón** → **Ver detalle**.
2. Selecciona su versión 1 (Borrador; lleva 100 g de masa de polvorón por pieza) → **Publicar versión** → **Activar versión**.

## 3. Producir (Producción)

1. **Producción** → **Nueva producción** → busca **Polvorón**; debe decir «LISTO PARA PRODUCIR» → **Agregar producto seleccionado**.
2. Cantidad planeada: las piezas de una media tanda (por ejemplo 18).
3. **Calcular necesidades**: revisa que cada ingrediente diga «Suficiente» y que las cantidades sean las de una media tanda.
4. **Preparar registro de producción** → en el detalle, captura las piezas que **realmente** salieron → **Registrar producción**.

## 4. Comprobar (Inventario)

1. **Inventario**: cada ingrediente bajó lo que calculó Producción. Con una media tanda completa son harina 900 g, manteca vegetal 500 g, azúcar 500 g, Royal 40 g, 1 huevo y el sabor elegido. El **Polvorón** terminado subió las piezas registradas.
2. Si algo no cuadra, anótalo y no se publica en la caja real.

## Después

Si todo cuadra, se pide tu aprobación para hacer lo mismo en la caja real, cuando ya esté en la versión 31 (`docs/ACTUALIZAR_CAJA.md`). En la caja real **no** se usan existencias de prueba: primero va el conteo real de esos insumos.
