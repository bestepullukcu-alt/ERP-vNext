/* WP-VW-W4 (W4-WEB-b) - DOM smoke of potential-thresholds.js in Node: a fake document, a stub fetch, stub bootstrap.
   Run by VwW4PharmacyThresholdsWebTests with the CRM script folder as argv[2], a JSON file of the Turkish script texts
   (the real resx) as argv[3] and the mode as argv[4] ('manage' | 'readonly'); prints one JSON object.
   manage: the table (default first) → edit a product → High below Low is refused before any call → a valid pair → the
   save confirm (changed rows) → PUT with the version → the table and the history reload (the person named); a new row
   offers only products without one and needs a product.
   readonly: no edit button, nothing written. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manage';
const nodes = {};
const listeners = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('pt-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-me': 'user-me', 'data-can-manage': mode === 'manage' ? 'true' : 'false', 'data-can-read-products': 'true' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
['pt-err-low', 'pt-err-high', 'pt-err-product', 'pt-product-field', 'pt-edit-error', 'pt-load-error'].forEach(id => el(id).classList.add('d-none'));
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };

let dto = { default: { productId: null, lowBelow: 5, highAbove: 20, source: 'default', version: 2, updatedBy: 'user-me', updatedAt: '2026-10-12T09:00:00Z' },
  products: [{ productId: 'P1', lowBelow: 10, highAbove: 40, source: 'product', version: 1, updatedBy: 'u2', updatedAt: '2026-10-11T09:00:00Z' }] };
let history = { items: [{ at: '2026-10-11T09:00:00Z', by: 'u2', productId: 'P1', field: 'lowBelow', oldValue: '8', newValue: '10' }], total: 1, page: 1, pageSize: 100, displayNames: { u2: 'Ayşe Demir' } };
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null, status = 200;
  if (url === '/api/thresholds' && method === 'GET') d = dto;
  else if (url.indexOf('/api/thresholds/history') === 0) d = history;
  else if (url === '/api/lookups/products') d = [{ id: 'P1', code: 'URO', name: 'Ürofleks' }, { id: 'P2', code: 'GLK', name: 'Glukofit' }];
  else if (url.indexOf('/api/thresholds/') === 0 && method === 'PUT') {
    const b = JSON.parse(o.body);
    dto = { default: dto.default, products: [{ productId: 'P1', lowBelow: b.lowBelow, highAbove: b.highAbove, source: 'product', version: 2, updatedBy: 'user-me', updatedAt: '2026-10-13T10:00:00Z' }] };
    history = Object.assign({}, history, { items: history.items.concat([{ at: '2026-10-13T10:00:00Z', by: 'user-me', productId: 'P1', field: 'lowBelow', oldValue: '10', newValue: String(b.lowBelow) }]) });
    d = dto.products[0];
  }
  return Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [], hidden = [], toasts = [];
global.window = { document: global.document, showToast: m => toasts.push(m),
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) },
    Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) } } };
require(dir + '/VisitPlanning/format.js');
global.window.PotentialThresholdsCore = require(dir + '/PotentialThresholds/thresholds-core.js');
global.window.PotentialThresholdsL10n = texts;
const out = { mode };
try { require(dir + '/PotentialThresholds/potential-thresholds.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const editTap = key => listeners.click({ target: { closest: q => (q === '[data-edit]' ? { getAttribute: () => key } : null) } });

(async () => {
  await tick(); await tick(); await tick();
  out.rows = el('pt-rows').innerHTML;
  out.historyRows = el('pt-history-rows').innerHTML;
  if (mode === 'manage') {
    editTap('P1'); await tick();
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
    el('pt-save-confirm').l.click(); await tick(); await tick(); await tick();
    out.put = calls.filter(c => c.method === 'PUT').map(c => [c.url, c.body]);
    out.toasts = toasts.slice();
    out.rowsAfter = el('pt-rows').innerHTML;
    out.historyAfter = el('pt-history-rows').innerHTML;
    // a new row: only the products without one; a product is required
    el('pt-add').l.click(); await tick();
    out.newOptions = el('pt-product').innerHTML;
    el('pt-low').value = '3'; el('pt-high').value = '9';
    const before = calls.length;
    el('pt-save').l.click(); await tick();
    out.productError = el('pt-err-product').textContent;
    out.callsWithoutProduct = calls.length - before;
  } else {
    editTap('P1'); await tick();
    out.drawerShown = shown.includes('pt-edit-drawer');
    out.writes = calls.filter(c => c.method !== 'GET').length;
  }
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
