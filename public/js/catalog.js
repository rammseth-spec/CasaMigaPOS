// Catálogo como la caja de escritorio (CatalogView y PhotoCropDialog de QAv1): pestañas Productos / Categorías,
// tabla con búsqueda, filtro, inactivos y páginas de 50; editores en la misma pantalla; foto con origen, recorte manual y «Quitar foto».
// Los cambios se mandan a la caja (lib/commands.ts) y la caja los aplica con sus reglas.
import { api, can, command, dialog, fmt, h, icon, pesos, render, state, toast } from './app.js';

const PAGE = 50;
const BREAD = 'M4,5 L16,5 18,8 18,17 2,17 2,8 Z M7,5 L8.5,2 11.5,2 13,5 M10,9 A3,3 0 1 1 9.99,9';
const cat = { tab: 'products', screen: 'list', q: '', applied: '', category: '', inactive: false, page: 0, selected: null, selectedCategory: null, editing: null, form: null, message: '', ok: false };
const plain = text => String(text ?? '').normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
const uuid = () => crypto.randomUUID();
const branchName = id => state.branches.find(b => b.id === id)?.name ?? '';
const multi = () => !state.branch && state.branches.length > 1;
const say = (message, ok = false) => { cat.message = message; cat.ok = ok; };

export async function catalogo(ctx) {
  const data = await api('catalog', { branch: state.branch });
  const tabs = h('div', { class: 'report-tabs' },
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(cat.tab === 'products' && cat.screen === 'list'), onclick: () => { Object.assign(cat, { tab: 'products', screen: 'list' }); say(''); render(); } }, 'Productos'),
    h('button', { type: 'button', class: 'tab', 'aria-pressed': String(cat.tab === 'categories' && cat.screen === 'list'), onclick: () => { Object.assign(cat, { tab: 'categories', screen: 'list' }); say(''); render(); } }, 'Categorías'));
  const body = cat.screen === 'product' ? productEditor(data, ctx) : cat.screen === 'category' ? categoryEditor(data, ctx)
    : cat.tab === 'categories' ? categoriesList(data) : productsList(data, ctx);
  return h('div', { class: 'cash-card catalog' }, cat.screen === 'list' ? tabs : null, body,
    cat.message ? h('p', { class: cat.ok ? 'success' : 'error', role: 'status', text: cat.message, style: { marginTop: '12px' } }) : null);
}

// ---------- Productos ----------
function productsList(data, ctx) {
  const categories = new Map(data.categories.map(c => [c.id, c]));
  const text = plain(cat.applied);
  const list = data.products.filter(p => (cat.inactive || p.active) && (!cat.category || p.categoryId === cat.category) && (!text || plain(`${p.name} ${p.sku ?? ''}`).includes(text)));
  const pages = Math.max(1, Math.ceil(list.length / PAGE));
  cat.page = Math.min(cat.page, pages - 1);
  const page = list.slice(cat.page * PAGE, (cat.page + 1) * PAGE);
  // Se busca al hacer clic: elegir una fila no vuelve a dibujar la pantalla.
  const selected = () => data.products.find(p => p.id === cat.selected) ?? null;
  const search = h('input', { maxlength: '160', value: cat.q, 'aria-label': 'Buscar nombre o SKU', oninput: e => { cat.q = e.target.value; },
    onkeydown: e => { if (e.key === 'Enter') apply(); if (e.key === 'Escape') clear(); } });
  const apply = () => { cat.applied = cat.q; cat.page = 0; cat.selected = null; render({ silent: true }); };
  const clear = () => { cat.q = ''; cat.applied = ''; cat.page = 0; render({ silent: true }); };
  const edit = p => { if (!p) { say('Selecciona un producto.'); return render({ silent: true }); } openProduct(p, data); };
  const rows = page.map(p => {
    const tr = h('tr', { class: 'click' + (cat.selected === p.id ? ' selected' : '') + (p.active ? '' : ' off'), tabindex: '0',
      onclick: () => { if (cat.selected === p.id && can('products.edit')) return edit(p); cat.selected = p.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); },
      ondblclick: () => can('products.edit') && edit(p) },
      h('td', { 'data-label': 'Producto' }, thumb(p), h('span', { text: p.name + (multi() ? ` (${branchName(p.branchId)})` : '') })),
      h('td', { 'data-label': 'SKU', text: p.sku ?? '' }), h('td', { 'data-label': 'Categoría', text: categories.get(p.categoryId)?.name ?? '' }),
      h('td', { class: 'num', 'data-label': 'Precio', text: fmt(p.price) }), h('td', { 'data-label': 'Estado', text: p.active ? 'Activo' : 'Inactivo' }));
    return tr;
  });
  const empty = cat.applied ? 'No encontramos productos con ese nombre o SKU.' : cat.category ? 'No hay productos en esta categoría con los filtros actuales.' : 'No hay productos para mostrar.';
  return h('div', {},
    h('div', { class: 'catalog-filters' },
      h('label', { class: 'history-label' }, 'Buscar nombre o SKU', search),
      h('label', { class: 'history-label' }, 'Filtrar por categoría', h('select', { 'aria-label': 'Filtrar por categoría', onchange: e => { cat.category = e.target.value; cat.page = 0; render({ silent: true }); } },
        h('option', { value: '', text: 'Todas las categorías' }), data.categories.map(c => h('option', { value: c.id, text: c.name + (multi() ? ` (${branchName(c.branchId)})` : ''), selected: cat.category === c.id }))))),
    h('div', { class: 'catalog-apply' },
      h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: cat.inactive, onchange: e => { cat.inactive = e.target.checked; cat.page = 0; render({ silent: true }); } }), 'Mostrar inactivos'),
      h('button', { type: 'button', class: 'secondary', onclick: apply }, 'Buscar / aplicar'),
      cat.applied ? h('button', { type: 'button', class: 'ghost', onclick: clear }, '× Limpiar') : null),
    page.length ? h('div', { class: 'table-wrap history-table catalog-table' }, h('table', { class: 'cards', 'aria-label': 'Productos' },
      h('thead', {}, h('tr', {}, ['Producto', 'SKU', 'Categoría', 'Precio', 'Estado'].map((c, i) => h('th', { class: i === 3 ? 'num' : null, text: c })))),
      h('tbody', {}, rows))) : h('div', { class: 'catalog-empty', text: empty }),
    h('div', { class: 'catalog-actions' },
      h('button', { type: 'button', class: 'primary', disabled: !can('products.create'), onclick: () => openProduct(null, data, ctx.branchId()) }, 'Nuevo producto'),
      h('button', { type: 'button', class: 'secondary', disabled: !can('products.edit'), onclick: () => edit(selected()) }, 'Administrar foto'),
      h('button', { type: 'button', class: 'secondary', disabled: !can('products.edit'), onclick: () => edit(selected()) }, 'Editar producto'),
      h('button', { type: 'button', class: 'secondary', disabled: !can('products.deactivate'), onclick: () => toggleProduct(selected()) }, 'Activar / desactivar')),
    h('p', { class: 'catalog-page', text: `Página ${cat.page + 1} · ${list.length} productos · hasta ${PAGE} por página` }),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', disabled: cat.page === 0, onclick: () => { cat.page--; render({ silent: true }); } }, 'Anterior'),
      h('button', { type: 'button', class: 'secondary', disabled: (cat.page + 1) * PAGE >= list.length, onclick: () => { cat.page++; render({ silent: true }); } }, 'Siguiente')));
}
const photoUrl = p => `/api/web/photo?product=${p.id}&branch=${p.branchId}&v=${p.photo.sha}`;
const thumb = p => h('span', { class: 'pic xs catalog-thumb' }, p.photo ? h('img', { src: photoUrl(p), alt: '', loading: 'lazy' }) : null);

async function toggleProduct(p) {
  if (!p) { say('Selecciona un producto.'); return render({ silent: true }); }
  try { await command(p.branchId, 'product.active', { id: p.id, active: !p.active }); say(`${p.active ? 'Desactivación' : 'Activación'} de «${p.name}» enviada a la caja.`, true); }
  catch (error) { say(error.message); }
  render({ silent: true });
}

function openProduct(p, data, branchId) {
  cat.editing = p?.id ?? null;
  cat.form = p ? { branchId: p.branchId, name: p.name, categoryId: p.categoryId, price: pesos(p.price), sku: p.sku ?? '', description: p.description ?? '', track: p.trackInventory, provenance: p.photo?.provisional ? 'PROVISIONAL' : 'REAL', inline: false, newCategory: '' }
    : { branchId: branchId || data.categories[0]?.branchId || state.me.branchId, name: '', categoryId: '', price: '0.00', sku: '', description: '', track: true, provenance: 'REAL', inline: false, newCategory: '' };
  cat.screen = 'product'; say(''); render({ silent: true });
}

function productEditor(data) {
  const f = cat.form;
  const product = data.products.find(p => p.id === cat.editing) ?? null;
  const options = data.categories.filter(c => c.branchId === f.branchId && (c.active || c.id === f.categoryId));
  const pending = cat.pendingCategory && cat.pendingCategory.branchId === f.branchId ? [cat.pendingCategory] : [];
  const bind = key => e => { f[key] = e.target.type === 'checkbox' ? e.target.checked : e.target.value; };
  const photo = photoSection(product, f);
  const save = async () => {
    say('');
    const price = String(f.price).trim().replace(',', '.');
    if (!f.name.trim()) { say('Escribe el nombre del producto.'); return render({ silent: true }); }
    if (!f.categoryId) { say('Elige la categoría.'); return render({ silent: true }); }
    if (!/^\d{1,6}(\.\d{1,2})?$/.test(price)) { say('El precio no puede ser negativo ni exceder el límite permitido.'); return render({ silent: true }); }
    try {
      await command(f.branchId, 'product.save', { id: cat.editing ?? uuid(), categoryId: f.categoryId, sku: f.sku.trim() || null, name: f.name.trim(), description: f.description.trim(),
        priceCents: Math.round(Number(price) * 100), trackInventory: f.track });
      cat.screen = 'list'; say(`«${f.name.trim()}» enviado a la caja.`, true);
    } catch (error) { say(error.message); }
    render({ silent: true });
  };
  return h('div', { class: 'catalog-editor' },
    h('h2', { class: 'detail-title', text: cat.editing ? 'Editar producto' : 'Crear producto' }),
    photo,
    !cat.editing && state.me.admin && state.branches.length > 1 ? h('label', { class: 'label' }, 'Sucursal', h('select', { onchange: e => { f.branchId = e.target.value; f.categoryId = ''; render({ silent: true }); } },
      state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null,
    h('label', { class: 'label' }, 'Nombre', h('input', { maxlength: '160', value: f.name, 'aria-label': 'Nombre de producto', oninput: bind('name') })),
    h('div', { class: 'catalog-two' },
      h('label', { class: 'label' }, 'Categoría', h('span', { class: 'catalog-category' },
        h('select', { 'aria-label': 'Categoría del producto', onchange: bind('categoryId') }, h('option', { value: '', text: 'Elige una categoría' }),
          [...options, ...pending].map(c => h('option', { value: c.id, text: c.name, selected: c.id === f.categoryId }))),
        h('button', { type: 'button', class: 'secondary', disabled: !can('categories.create'), onclick: () => { f.inline = true; f.newCategory = ''; render({ silent: true }); } }, '+ Nueva categoría'))),
      h('label', { class: 'label' }, 'Precio MXN · punto decimal', h('input', { maxlength: '20', inputmode: 'decimal', value: f.price, 'aria-label': 'Precio MXN', oninput: bind('price') }))),
    f.inline ? h('div', { class: 'inline-category' },
      h('strong', { text: 'Crear categoría sin salir del producto' }),
      h('p', { class: 'caption', text: 'El producto en captura conserva nombre, precio, SKU y descripción.', style: { margin: '2px 0 8px' } }),
      h('input', { maxlength: '80', value: f.newCategory, 'aria-label': 'Nombre de nueva categoría', oninput: bind('newCategory') }),
      h('div', { class: 'toolbar', style: { marginTop: '10px' } },
        h('button', { type: 'button', class: 'secondary', onclick: () => { f.inline = false; render({ silent: true }); } }, 'Cancelar categoría'),
        h('button', { type: 'button', class: 'primary', onclick: async () => {
          const name = f.newCategory.trim();
          if (!name) { say('Escribe el nombre de la categoría.'); return render({ silent: true }); }
          const id = uuid();
          try {
            await command(f.branchId, 'category.save', { id, name, description: '', sortOrder: 0 });
            cat.pendingCategory = { id, name: `${name} (nueva)`, branchId: f.branchId };
            f.categoryId = id; f.inline = false; say(`Categoría «${name}» enviada a la caja y seleccionada.`, true);
          } catch (error) { say(error.message); }
          render({ silent: true });
        } }, 'Crear y seleccionar'))) : null,
    h('label', { class: 'label' }, 'SKU opcional', h('input', { maxlength: '40', value: f.sku, 'aria-label': 'SKU', oninput: bind('sku') })),
    h('label', { class: 'label' }, 'Descripción opcional', h('input', { maxlength: '500', value: f.description, 'aria-label': 'Descripción de producto', oninput: bind('description') })),
    h('label', { class: 'check', style: { margin: '4px 0 12px' } }, h('input', { type: 'checkbox', checked: f.track, onchange: bind('track') }), 'Controlar inventario en el futuro (sólo metadato)'),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { cat.screen = 'list'; say(''); render({ silent: true }); } }, 'Cancelar'),
      h('button', { type: 'button', class: 'primary', disabled: !(cat.editing ? can('products.edit') : can('products.create')), onclick: save }, 'Guardar producto')));
}

/** Sección «IMAGEN DEL PRODUCTO»: foto actual, origen, Agregar/Cambiar foto (con recorte) y Quitar foto. */
function photoSection(product, f) {
  const has = !!product?.photo;
  const status = !product ? 'Guarda el producto antes de agregar una foto' : has ? (product.photo.provisional ? 'Foto provisional' : 'Foto real') : 'Sin imagen';
  const manage = !!product && can('products.edit');
  const choose = h('input', { type: 'file', accept: 'image/jpeg,image/png,image/webp,image/heic', hidden: true, onchange: async e => {
    const file = e.target.files[0]; e.target.value = '';
    if (!file) return;
    try { cropDialog(await orient(file), async image => {
      await command(product.branchId, 'product.photo', { productId: product.id, image, provenance: f.provenance });
      say('Foto enviada a la caja. Se verá aquí en cuanto la caja la aplique.', true); render({ silent: true });
    }); } catch (error) { toast(error.message); }
  } });
  return h('div', { class: 'photo-section' },
    h('div', { class: 'photo-large' }, has ? h('img', { src: photoUrl(product), alt: '' }) : h('span', { class: 'tile-fallback' }, icon(BREAD), h('small', { text: 'Sin imagen' }))),
    h('div', { class: 'photo-info' },
      h('p', { class: 'photo-eyebrow', text: 'IMAGEN DEL PRODUCTO' }),
      h('p', { class: 'photo-status', text: status }),
      h('p', { class: 'muted', text: 'Esta imagen aparece en la tarjeta del Punto de venta. Usa una foto real cuando esté disponible; las referencias de trabajo deben marcarse como provisionales.', style: { margin: '0 0 12px' } }),
      h('label', { class: 'label' }, 'Origen de la imagen', h('select', { disabled: !manage, onchange: e => { f.provenance = e.target.value; } },
        h('option', { value: 'REAL', text: 'Foto real', selected: f.provenance === 'REAL' }), h('option', { value: 'PROVISIONAL', text: 'Foto provisional', selected: f.provenance === 'PROVISIONAL' }))),
      h('div', { class: 'toolbar' },
        h('button', { type: 'button', class: 'primary', disabled: !manage, onclick: () => choose.click() }, has ? 'Cambiar foto' : 'Agregar foto'), choose,
        h('button', { type: 'button', class: 'secondary', disabled: !manage || !has, onclick: async () => {
          try { await command(product.branchId, 'product.photo.remove', { productId: product.id }); say('Foto quitada. El producto conserva todos sus datos.', true); }
          catch (error) { say(error.message); }
          render({ silent: true });
        } }, 'Quitar foto')),
      !product ? h('p', { class: 'caption', text: 'Guarda primero los productos nuevos; después podrás asociar su fotografía.' }) : null));
}

/** Imagen enderezada (EXIF) lista para recortar; mínimo 256 píxeles por lado. */
async function orient(file) {
  if (!window.createImageBitmap) throw new Error('Este navegador no puede preparar la foto; usa uno más reciente.');
  if (file.size > 25 * 1024 * 1024) throw new Error('Máximo 25 MiB.');
  let bitmap;
  try { bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' }); } catch { throw new Error('Selecciona un JPEG o PNG válido.'); }
  if (Math.min(bitmap.width, bitmap.height) < 256) throw new Error('La foto es muy chica (mínimo 256 × 256 píxeles).');
  return bitmap;
}

/**
 * «Ajustar fotografía» (PhotoCropDialog): cuadro que se arrastra y se agranda desde la esquina, vista previa y ajuste por coordenadas.
 * El archivo original no se modifica; se manda el recorte en JPEG de hasta 1024 px.
 */
function cropDialog(bitmap, use) {
  const W = bitmap.width, H = bitmap.height;
  const crop = { edge: Math.min(W, H) };
  crop.x = Math.round((W - crop.edge) / 2); crop.y = Math.round((H - crop.edge) / 2);
  // Cabe en la pantalla (en celular, el ancho disponible); el recorte se calcula sobre la imagen completa.
  const box = Math.min(560, window.innerWidth - 72), scale = Math.min(box / W, 460 / H);
  const stage = h('div', { class: 'crop-stage' });
  stage.style.width = `${W * scale}px`; stage.style.height = `${H * scale}px`;
  const source = h('canvas', { width: String(Math.round(W * scale)), height: String(Math.round(H * scale)) });
  source.getContext('2d').drawImage(bitmap, 0, 0, W * scale, H * scale);
  const square = h('div', { class: 'crop-square' }, h('span', { class: 'crop-handle', 'aria-hidden': 'true' }));
  stage.append(source, square);
  const preview = h('canvas', { class: 'crop-preview', width: '140', height: '140' });
  const labels = { x: h('span'), y: h('span'), edge: h('span') };
  const sliders = {
    x: h('input', { type: 'range', min: '0', 'aria-label': 'Posición horizontal' }), y: h('input', { type: 'range', min: '0', 'aria-label': 'Posición vertical' }),
    edge: h('input', { type: 'range', min: '256', max: String(Math.min(W, H)), 'aria-label': 'Tamaño del recorte' }),
  };
  const clamp = () => {
    crop.edge = Math.max(256, Math.min(Math.min(W, H), Math.round(crop.edge)));
    crop.x = Math.max(0, Math.min(W - crop.edge, Math.round(crop.x))); crop.y = Math.max(0, Math.min(H - crop.edge, Math.round(crop.y)));
  };
  const draw = () => {
    clamp();
    Object.assign(square.style, { left: `${crop.x * scale}px`, top: `${crop.y * scale}px`, width: `${crop.edge * scale}px`, height: `${crop.edge * scale}px` });
    preview.getContext('2d').drawImage(bitmap, crop.x, crop.y, crop.edge, crop.edge, 0, 0, 140, 140);
    sliders.x.max = String(W - crop.edge); sliders.y.max = String(H - crop.edge);
    sliders.x.value = String(crop.x); sliders.y.value = String(crop.y); sliders.edge.value = String(crop.edge);
    labels.x.textContent = `X: ${crop.x} px`; labels.y.textContent = `Y: ${crop.y} px`; labels.edge.textContent = `Lado: ${crop.edge} px`;
  };
  for (const key of ['x', 'y', 'edge']) sliders[key].addEventListener('input', e => { crop[key] = Number(e.target.value); draw(); });
  // Arrastrar el cuadro lo mueve; arrastrar la esquina inferior derecha cambia su tamaño.
  let drag = null;
  square.addEventListener('pointerdown', e => {
    e.preventDefault(); square.setPointerCapture(e.pointerId);
    drag = { resize: e.target.classList.contains('crop-handle'), x: e.clientX, y: e.clientY, start: { ...crop } };
  });
  square.addEventListener('pointermove', e => {
    if (!drag) return;
    const dx = (e.clientX - drag.x) / scale, dy = (e.clientY - drag.y) / scale;
    if (drag.resize) crop.edge = drag.start.edge + Math.max(dx, dy);
    else { crop.x = drag.start.x + dx; crop.y = drag.start.y + dy; }
    draw();
  });
  square.addEventListener('pointerup', () => { drag = null; });
  const node = h('dialog', { class: 'crop', 'aria-label': 'Ajustar foto' });
  const close = () => { node.close(); node.remove(); bitmap.close?.(); };
  const error = h('p', { class: 'error', role: 'alert' });
  node.append(h('div', { class: 'crop-card' },
    h('h2', { class: 'checkout-title', text: 'Ajustar fotografía' }),
    h('p', { class: 'muted', text: 'Arrastra el cuadro para moverlo. Arrastra la esquina inferior derecha para cambiar su tamaño. El archivo original no se modifica.', style: { margin: '0 0 14px' } }),
    h('div', { class: 'crop-body' }, stage,
      h('div', { class: 'crop-side' },
        h('strong', { text: 'Vista previa' }), preview, h('p', { class: 'caption', text: 'Así se verá en el catálogo.' }),
        h('details', { class: 'crop-advanced' }, h('summary', { text: 'Ajuste avanzado por coordenadas' }),
          h('label', { class: 'kpi-label' }, 'Posición horizontal', sliders.x, labels.x), h('label', { class: 'kpi-label' }, 'Posición vertical', sliders.y, labels.y),
          h('label', { class: 'kpi-label' }, 'Tamaño del recorte', sliders.edge, labels.edge)),
        h('p', { class: 'caption', text: `Imagen orientada: ${W} × ${H} px` }))),
    error,
    h('div', { class: 'toolbar crop-actions' },
      h('button', { type: 'button', class: 'secondary', onclick: close }, 'Cancelar'),
      h('button', { type: 'button', class: 'primary', onclick: async e => {
        e.target.disabled = true;
        try { await use(await encode(bitmap, crop)); close(); }
        catch (err) { error.textContent = err.message; e.target.disabled = false; }
      } }, 'Usar este recorte'))));
  node.addEventListener('cancel', e => { e.preventDefault(); close(); });
  document.body.append(node); node.showModal(); draw();
}
async function encode(bitmap, crop) {
  const edge = Math.min(1024, crop.edge);
  const canvas = h('canvas', { width: String(edge), height: String(edge) });
  canvas.getContext('2d').drawImage(bitmap, crop.x, crop.y, crop.edge, crop.edge, 0, 0, edge, edge);
  let blob = null;
  for (let quality = 0.86; quality > 0.4; quality -= 0.12) { blob = await new Promise(done => canvas.toBlob(done, 'image/jpeg', quality)); if (blob && blob.size <= 900_000) break; }
  if (!blob || blob.size > 1_000_000) throw new Error('No se pudo reducir la foto; prueba con otra.');
  return new Promise((done, fail) => { const reader = new FileReader(); reader.onload = () => done(String(reader.result).split(',')[1]); reader.onerror = fail; reader.readAsDataURL(blob); });
}

// ---------- Categorías ----------
function categoriesList(data) {
  const chosen = () => data.categories.find(c => c.id === cat.selectedCategory) ?? null;
  const open = c => { cat.editing = c?.id ?? null; cat.form = c ? { branchId: c.branchId, name: c.name, description: c.description ?? '', order: String(c.sortOrder) }
    : { branchId: state.branch || state.me.branchId || state.branches[0]?.id, name: '', description: '', order: '0' }; cat.screen = 'category'; say(''); render({ silent: true }); };
  const rows = data.categories.map(c => {
    const tr = h('tr', { class: 'click' + (cat.selectedCategory === c.id ? ' selected' : ''), tabindex: '0',
      onclick: () => { if (cat.selectedCategory === c.id && can('categories.edit')) return open(c); cat.selectedCategory = c.id; for (const r of tr.parentNode.children) r.classList.toggle('selected', r === tr); },
      ondblclick: () => can('categories.edit') && open(c) },
      h('td', { 'data-label': 'Nombre', text: c.name + (multi() ? ` (${branchName(c.branchId)})` : '') }), h('td', { class: 'num', 'data-label': 'Orden', text: String(c.sortOrder) }),
      h('td', { 'data-label': 'Activa' }, h('input', { type: 'checkbox', checked: c.active, disabled: true, 'aria-label': c.active ? 'Activa' : 'Inactiva' })));
    return tr;
  });
  return h('div', {},
    h('p', { text: 'Categorías · incluye inactivas', style: { margin: '0 0 8px' } }),
    h('div', { class: 'table-wrap history-table' }, h('table', { class: 'cards', 'aria-label': 'Categorías' },
      h('thead', {}, h('tr', {}, h('th', { text: 'Nombre' }), h('th', { class: 'num', text: 'Orden' }), h('th', { text: 'Activa' }))), h('tbody', {}, rows))),
    h('div', { class: 'catalog-actions' },
      h('button', { type: 'button', class: 'primary', disabled: !can('categories.create'), onclick: () => open(null) }, 'Nueva categoría'),
      h('button', { type: 'button', class: 'secondary', disabled: !can('categories.edit'), onclick: () => { const selected = chosen(); if (!selected) { say('Selecciona una categoría.'); return render({ silent: true }); } open(selected); } }, 'Editar categoría'),
      h('button', { type: 'button', class: 'secondary', disabled: !can('categories.deactivate'), onclick: async () => {
        const selected = chosen();
        if (!selected) { say('Selecciona una categoría.'); return render({ silent: true }); }
        try { await command(selected.branchId, 'category.save', { id: selected.id, name: selected.name, description: selected.description ?? '', sortOrder: selected.sortOrder, active: !selected.active });
          say(`${selected.active ? 'Desactivación' : 'Activación'} de «${selected.name}» enviada a la caja.`, true); }
        catch (error) { say(error.message); }
        render({ silent: true });
      } }, 'Activar / desactivar')));
}
function categoryEditor() {
  const f = cat.form;
  const bind = key => e => { f[key] = e.target.value; };
  return h('div', { class: 'catalog-editor' },
    h('h2', { class: 'detail-title', text: cat.editing ? 'Editar categoría' : 'Crear categoría' }),
    !cat.editing && state.me.admin && state.branches.length > 1 ? h('label', { class: 'label' }, 'Sucursal', h('select', { onchange: bind('branchId') },
      state.branches.map(b => h('option', { value: b.id, text: b.name, selected: b.id === f.branchId })))) : null,
    h('label', { class: 'label' }, 'Nombre', h('input', { maxlength: '80', value: f.name, 'aria-label': 'Nombre de categoría', oninput: bind('name') })),
    h('label', { class: 'label' }, 'Descripción opcional', h('input', { maxlength: '500', value: f.description, 'aria-label': 'Descripción de categoría', oninput: bind('description') })),
    h('label', { class: 'label' }, 'Orden', h('input', { maxlength: '4', inputmode: 'numeric', value: f.order, 'aria-label': 'Orden de categoría', oninput: bind('order') })),
    h('div', { class: 'toolbar' },
      h('button', { type: 'button', class: 'secondary', onclick: () => { cat.screen = 'list'; say(''); render({ silent: true }); } }, 'Cancelar'),
      h('button', { type: 'button', class: 'primary', onclick: async () => {
        if (!f.name.trim()) { say('Escribe el nombre de la categoría.'); return render({ silent: true }); }
        if (!/^\d{1,4}$/.test(f.order.trim())) { say('El orden es un número entero de 0 a 9999.'); return render({ silent: true }); }
        try { await command(f.branchId, 'category.save', { id: cat.editing ?? uuid(), name: f.name.trim(), description: f.description.trim(), sortOrder: Number(f.order) });
          cat.screen = 'list'; say(`Categoría «${f.name.trim()}» enviada a la caja.`, true); }
        catch (error) { say(error.message); }
        render({ silent: true });
      } }, 'Guardar categoría')));
}
