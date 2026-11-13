/* WP-VW-W4 (W4-WEB-b) · W3E-E4R2-FIX-c - DOM smoke of the potential thresholds page (index.js + history.js) in Node: a fake
   document, a stub fetch, stub bootstrap, the REAL list factory's wire pieces (diten-datatable.js: renderActions,
   toServerQuery, toDataTablesResponse, toExportQuery) with the two table builders replaced by fakes that run the page's own
   ajax mapping and column renderers. Run by VwW4PharmacyThresholdsWebTests with the CRM script folder as argv[2], a JSON
   file of the Turkish page texts (the real resx) as argv[3] and the mode as argv[4] ('manage' | 'readonly'); prints one
   JSON object.
   manage: the list (client mode, default first) → the Edit row action opens the drawer → High below Low is refused before
   any call → a valid pair → the save confirm (changed rows) → PUT with the version → the list reloads with the standard
   toast and the history reloads (server mode: start / length on the wire, the person named); "add a product" (the
   toolbar's Add New) offers only products without a row and needs a product.
   readonly: no Edit action, no Add New, nothing written.
   W3E-E4R2-FIX-d (7): the list's order the way DataTables builds it (orderFixed.pre BEFORE the reader's order, each column
   compared by its 'sort' render value) for every sortable column, both directions — the tenant default must stay first;
   (8): the medium band (cell + band preview) carries the left-to-right isolate (LRI…PDI). */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manage';
const nodes = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('pt-root');
Object.assign(root.attrs, { 'data-me': 'user-me', 'data-can-manage': mode === 'manage' ? 'true' : 'false', 'data-can-add': mode === 'manage' ? 'true' : 'false', 'data-can-read-products': 'true' });
['pt-err-low', 'pt-err-high', 'pt-err-product', 'pt-product-field', 'pt-edit-error', 'pt-load-error'].forEach(id => el(id).classList.add('d-none'));
const docListeners = {};
const tables = { '.datatables-potentialthresholds': mk('dt-potentialthresholds'), '.datatables-pt-history': mk('dt-pt-history') };
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelector: q => tables[q] || null, querySelectorAll: () => [],
  addEventListener: (ev, fn) => { docListeners[ev] = fn; }, createElement: () => mk('x'), body: { appendChild() {} } };

let dto = { default: { productId: null, lowBelow: 5, highAbove: 20, source: 'default', version: 2, updatedBy: 'user-me', updatedAt: '2026-10-12T09:00:00Z' },
  products: [{ productId: 'P1', lowBelow: 10, highAbove: 40, source: 'product', version: 1, updatedBy: 'u2', updatedAt: '2026-10-11T09:00:00Z' }] };
let history = { items: [{ at: '2026-10-11T09:00:00Z', by: 'u2', productId: 'P1', field: 'lowBelow', oldValue: '8', newValue: '10' }], total: 1, filteredTotal: 1, displayNames: { u2: 'Ayşe Demir' } };
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null;
  if (url === '/CRM/PotentialThresholds/api/thresholds' && method === 'GET') d = dto;
  else if (url.indexOf('/CRM/PotentialThresholds/api/thresholds/history') === 0) d = history;
  else if (url === '/CRM/PotentialThresholds/api/lookups/products') d = [{ id: 'P1', code: 'URO', name: 'Ürofleks' }, { id: 'P2', code: 'GLK', name: 'Glukofit' }];
  else if (url.indexOf('/CRM/PotentialThresholds/api/thresholds/') === 0 && method === 'PUT') {
    const b = JSON.parse(o.body);
    dto = { default: dto.default, products: [{ productId: 'P1', lowBelow: b.lowBelow, highAbove: b.highAbove, source: 'product', version: 2, updatedBy: 'user-me', updatedAt: '2026-10-13T10:00:00Z' }] };
    history = Object.assign({}, history, { total: 2, filteredTotal: 2, items: [{ at: '2026-10-13T10:00:00Z', by: 'user-me', productId: 'P1', field: 'lowBelow', oldValue: '10', newValue: String(b.lowBelow) }].concat(history.items) });
    d = dto.products[0];
  }
  const text = JSON.stringify({ data: d });
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(text), json: () => Promise.resolve(JSON.parse(text)) });
};
const shown = [], hidden = [], toasts = [];
global.window = { document: global.document, showToast: m => toasts.push(m),
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) },
    Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) } } };
global.window.L10n = texts;
// the REAL factory (its wire pieces and renderActions); the two table builders become fakes below
require(dir + '/../diten-datatable.js');
const real = global.window.DitenDataTable;
const renderRows = (config, rows) => rows.map(row => '<tr>' + config.columns.map((c, i) => {
  const def = config.columnDefs.find(d => d.targets === i || (d.targets === -1 && i === config.columns.length - 1));
  return '<td>' + (def && def.render ? def.render(row[c.data], 'display', row) : (row[c.data] == null ? '' : row[c.data])) + '</td>';
}).join('') + '</tr>').join('');
const out = { mode };
let listOptions = null, historyOptions = null;
const historyUrls = [];
const listHandle = {
  dt: { rows: () => ({ invalidate: () => ({ draw: () => { void drawList(); } }) }), ajax: { reload: () => { void drawList(); } } },
  reload: key => { void drawList().then(() => toasts.push(global.window.L10n[key])); }
};
let lastRows = [];
async function drawList() {
  const r = await fetch(listOptions.ajax.url, { method: 'GET' });
  const rows = listOptions.ajax.dataSrc(JSON.parse(await r.text()));
  lastRows = rows.slice();
  el('rows').innerHTML = renderRows(listOptions.config, rows);
}
async function drawHistory() {
  const o = historyOptions;
  const query = o.ajax.data({ draw: historyUrls.length + 1, start: 0, length: 10, search: { value: '' }, order: [], columns: o.config.columns.map(c => ({ data: c.data, orderable: false })) });
  const url = o.ajax.url + '?' + query;
  historyUrls.push(url);
  const r = await fetch(url, { method: 'GET' });
  const dtJson = JSON.parse(o.ajax.dataFilter.call({ url: url }, await r.text()));
  el('history').innerHTML = renderRows(o.config, dtJson.data);
  out.historyDraw = dtJson.draw; out.historyRecords = dtJson.recordsTotal;
}
global.window.DitenDataTable = Object.assign({}, real, {
  createList: async o => { listOptions = o; await drawList(); return listHandle; },
  createCrudTable: o => { historyOptions = o; void drawHistory(); return { ajax: { reload: () => { void drawHistory(); } }, columns: () => ({ indexes: () => ({ toArray: () => [] }) }) }; }
});
global.window.DtDefaults = { exportButtons: (addNew, attr, extra, options) => ({ options }) };
require(dir + '/VisitPlanning/format.js');
global.window.PotentialThresholdsCore = require(dir + '/PotentialThresholds/thresholds-core.js');
try {
  require(dir + '/PotentialThresholds/history.js');
  require(dir + '/PotentialThresholds/index.js');
  out.createError = null;
} catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));

(async () => {
  docListeners.DOMContentLoaded();
  await tick(); await tick(); await tick(); await tick();
  out.dataMode = listOptions.dataMode;
  out.addNewText = listOptions.toolbar.addNewText;
  out.historyMode = historyOptions.dataMode;
  out.historyServerSide = historyOptions.config.serverSide === true;
  out.historyServerExport = typeof historyOptions.config.buttons.options.serverExport === 'function';
  out.rows = el('rows').innerHTML;
  out.historyRows = el('history').innerHTML;
  out.historyUrl = historyUrls[0];
  // (7) the DataTables order: orderFixed.pre, then the reader's column; a column's 'sort' render value when it has a renderer
  const cfg = listOptions.config;
  const sortValue = (row, i) => {
    const c = cfg.columns[i];
    const def = cfg.columnDefs.find(d => d.targets === i);
    return def && def.render ? def.render(row[c.data], 'sort', row) : row[c.data];
  };
  const compare = (a, b) => (typeof a === 'number' && typeof b === 'number' ? a - b : String(a == null ? '' : a).localeCompare(String(b == null ? '' : b), 'tr'));
  const sorted = order => lastRows.slice().sort((x, y) => {
    for (const [col, dir] of order) { const c = compare(sortValue(x, col), sortValue(y, col)); if (c) { return dir === 'desc' ? -c : c; } }
    return 0;
  });
  const pre = (cfg.orderFixed && cfg.orderFixed.pre) || [];
  out.orderFixedPre = pre;
  out.controlSortValues = lastRows.map(row => [row.key, sortValue(row, 0)]);
  out.firstByOrder = {};
  cfg.columns.forEach((c, i) => {
    const def = cfg.columnDefs.find(d => d.targets === i);
    if (i === 0 || (def && def.orderable === false)) { return; }
    ['asc', 'desc'].forEach(dir => { out.firstByOrder[c.name + ':' + dir] = sorted(pre.concat([[i, dir]]))[0].key; });
  });
  out.rowCount = lastRows.length;
  if (mode === 'manage') {
    listOptions.actions.onRowAction.edit({ id: 'P1' }); await tick();
    out.drawerShown = shown.includes('pt-edit-drawer');
    out.editTitle = el('pt-edit-title').innerHTML;
    // High below Low → refused, no modal, no PUT
    el('pt-low').value = '50'; el('pt-high').value = '40';
    el('pt-high').l.input();
    el('pt-save').l.click(); await tick();
    out.orderError = el('pt-err-high').textContent;
    out.orderErrorShown = !el('pt-err-high').classList.contains('d-none');
    out.modalAfterInvalid = shown.includes('pt-save-modal');
    // a valid pair → the confirm → PUT
    el('pt-high').value = '60';
    el('pt-high').l.input();
    out.bands = el('pt-bands').innerHTML;
    el('pt-try').value = '55'; el('pt-try').l.input();
    out.tryClass = el('pt-try-class').innerHTML;
    el('pt-save').l.click(); await tick();
    out.modalShown = shown.includes('pt-save-modal');
    out.saveText = el('pt-save-text').textContent;
    out.saveDiff = el('pt-save-diff').innerHTML;
    el('pt-save-confirm').l.click(); await tick(); await tick(); await tick(); await tick();
    out.put = calls.filter(c => c.method === 'PUT').map(c => [c.url, c.body]);
    out.toasts = toasts.slice();
    out.rowsAfter = el('rows').innerHTML;
    out.historyAfter = el('history').innerHTML;
    out.historyReads = historyUrls.length;
    // a new row (the toolbar's Add New): only the products without one; a product is required
    listOptions.toolbar.onAddNew(); await tick();
    out.newOptions = el('pt-product').innerHTML;
    el('pt-low').value = '3'; el('pt-high').value = '9';
    const before = calls.length;
    el('pt-save').l.click(); await tick();
    out.productError = el('pt-err-product').textContent;
    out.callsWithoutProduct = calls.length - before;
  } else {
    listOptions.actions.onRowAction.edit({ id: 'P1' }); await tick();
    listOptions.toolbar.onAddNew(); await tick();
    out.drawerShown = shown.includes('pt-edit-drawer');
    out.writes = calls.filter(c => c.method !== 'GET').length;
  }
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
