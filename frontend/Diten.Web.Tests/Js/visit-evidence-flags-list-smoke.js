/* WP-VW-W3E · W3E-FLAGS-STD — the evidence flags page on the REAL index.js + rep-summary.js + flags-core.js + format.js in
   Node, with a fake list factory (DitenDataTable.createList / createCrudTable record what the page hands them) and a small
   fake DOM. Run by VisitEvidenceFlagsWebTests with the CRM script folder as argv[2], a JSON file of the page's L10n words
   (the real resx, the reader's culture) as argv[3] and the mode as argv[4]:
     team  (crm.visit-evidence-flags.read, no photo keys) · all (.read-all + the photo keys) · ar (team, Arabic, dir=rtl).
   The clock is pinned to 2026-10-14 (CT-3) whatever day the suite runs. Prints one JSON object. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'team';
const NOW = Date.UTC(2026, 9, 14, 9, 0, 0);
Date.now = () => NOW;

// ── a small fake DOM ─────────────────────────────────────────────────────────────────────────────────────────
const els = {};
function mk(id, tag) {
  const e = {
    id: id, tagName: String(tag || 'div').toUpperCase(), value: '', checked: false, textContent: '', attrs: {}, children: [], listeners: [], _cls: new Set(), _html: '',
    get innerHTML() { return this._html; },
    set innerHTML(v) { this._html = String(v); if (v === '') { this.children = []; } },
    get options() { return this.children; },
    setAttribute(k, v) { this.attrs[k] = String(v); }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
    addEventListener(type, fn, opts) { this.listeners.push({ type: type, fn: fn, capture: opts === true || !!(opts && opts.capture) }); },
    appendChild(c) { this.children.push(c); return c; },
    closest() { return null; },
    querySelector(q) { return q === 'tbody td.dt-empty' ? get('dt-empty-cell') : null; },
    click() { return dispatch(this, 'click'); }
  };
  e.classList = {
    add: c => e._cls.add(c), remove: c => e._cls.delete(c), contains: c => e._cls.has(c),
    toggle: (c, on) => { if (on === undefined ? !e._cls.has(c) : on) { e._cls.add(c); } else { e._cls.delete(c); } return e._cls.has(c); }
  };
  return e;
}
function get(id) { if (!els[id]) { els[id] = mk(id); } return els[id]; }
function dispatch(el, type, extra) {
  let stopped = false;
  const ev = Object.assign({ type: type, target: el, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopImmediatePropagation() { stopped = true; }, stopPropagation() {} }, extra || {});
  const order = el.listeners.filter(l => l.type === type && l.capture).concat(el.listeners.filter(l => l.type === type && !l.capture));
  for (const l of order) { if (stopped) { break; } l.fn(ev); }
  return ev;
}
const flagsTable = get('dt-visitevidenceflags');
const repsTable = get('dt-vf-reps');
const root = get('vf-root');
Object.assign(root.attrs, { 'data-can-read-all': mode === 'all' ? 'true' : 'false', 'data-can-see-photos': mode === 'all' ? 'true' : 'false',
  'data-visit-url': '/CRM/VisitReport', 'data-exception-url': '/CRM/VisitEvidenceSettings' });
get('filterRange').classList.add('d-none');
get('vf-only').checked = true;
let ready = null;
global.document = {
  documentElement: { lang: mode === 'ar' ? 'ar' : 'tr', dir: mode === 'ar' ? 'rtl' : 'ltr' },
  getElementById: get,
  querySelector: q => (q === '.datatables-visitevidenceflags' ? flagsTable : q === '.datatables-vf-reps' ? repsTable : null),
  createElement: tag => mk(null, tag),
  addEventListener: (type, fn) => { if (type === 'DOMContentLoaded') { ready = fn; } }
};

// ── the fake factory + the world ──────────────────────────────────────────────────────────────────────────
let listOptions = null;
const crud = [];
const loads = [];
const calls = [];
const toasts = [];
const assigned = [];
const shownTabs = [];
let applied = { period: '', from: '', to: '', resourceId: '', flags: [], accountId: '', scope: '' };
let draw = 0;
// the factory's toServerQuery: start, length, draw, then each applied filter by its key (a multi one repeated, empty ones left out)
const paramsOf = a => ['start=0', 'length=10', 'draw=' + (++draw)].concat(Object.keys(a).reduce((out, k) => out.concat(
  (Array.isArray(a[k]) ? a[k] : [a[k]]).filter(v => v !== '' && v != null).map(v => encodeURIComponent(k) + '=' + encodeURIComponent(v))), [])).join('&');
const repDt = { ajax: { url: u => { loads.push(u); return { load() {} }; } }, row: tr => ({ data: () => (tr && tr._row) || null }) };
const ACC = 'acc-1', EXC = 'exc-1';
const row = (id, extra) => Object.assign({
  executionId: id, plannedVisitId: 'pv-' + id, startedAt: '2026-10-14T07:42:00Z', resourceId: 'rep-1', repName: 'Sema Kara',
  target: { type: 'contact', targetId: 'c-' + id, name: 'Dr. Hakan Öztürk', accountId: ACC, accountName: 'Kardiyoloji Polikliniği' }, kind: 'in_person',
  flags: [], location: { distanceMeters: 95, accuracyMeters: 12, radiusMeters: 300, mode: 'required', status: 'ok' },
  device: { platform: 'web', appVersion: 'Safari 18 · iPadOS', mockLocation: null, integrityVerdict: null }, durationSeconds: 1080, exceptionId: null, hasPhoto: true,
  qr: { status: 'ok', accountId: ACC, accountName: 'Kardiyoloji Polikliniği', version: 3, scannedAt: '2026-10-14T07:41:00Z', matchedVia: 'self' } }, extra || {});
const rows = [
  row('e1', { flags: ['device_unverified'] }),
  row('e2', { repName: 'Murat Demir', resourceId: 'rep-2', flags: ['location_far'], location: { distanceMeters: 820, accuracyMeters: 18, radiusMeters: 300, status: 'far' } }),
  row('e3', { flags: ['location_far'], location: { distanceMeters: 198290, accuracyMeters: 20, radiusMeters: 300, status: 'far' },
    target: { type: 'pharmacy', name: '100.YIL ECZANESİ', accountId: 'acc-3', accountName: '100.YIL ECZANESİ' } }),
  row('e4', { flags: ['short_visit', 'qr_not_issued'], durationSeconds: 100, qr: { status: 'not_issued' },
    target: { type: 'pharmacy', name: '100.YIL ECZANESİ', accountId: 'acc-4', accountName: '100.YIL ECZANESİ' } }),
  row('e7', { flags: ['started_with_exception'], exceptionId: EXC, qr: { status: 'missing' } })
];
const detail = { row: rows[4], exception: { id: EXC, rules: ['qr_optional'], startDate: '2026-10-12', endDate: '2026-10-16', approvedAt: '2026-10-11T08:00:00Z' },
  slideSeconds: 710, slideCount: 8, photoCapturedAt: '2026-10-14T07:41:00Z' };
const summary = { from: '2026-10-08', to: '2026-10-14', started: 412, flagged: 37, flaggedRate: 0.09,
  byFlag: { device_unverified: 12, location_far: 7, short_visit: 5, qr_not_issued: 4, started_with_exception: 3, location_inaccurate: 3, qr_revoked: 2, mock_location: 2, location_missing: 1, qr_wrong_account: 1 },
  reps: [{ resourceId: 'rep-1', repName: 'Sema Kara', started: 148, flagged: 14, topFlag: 'device_unverified' }, { resourceId: 'rep-2', repName: 'Murat Demir', started: 136, flagged: 15, topFlag: 'location_far' }] };
global.fetch = (url, init) => {
  calls.push({ url: url, headers: init && init.headers });
  let d = null;
  if (url.indexOf('/CRM/VisitEvidenceFlags/api/account-labels') === 0) { d = [{ accountId: 'acc-3', city: 'Konya', code: 'E3' }, { accountId: 'acc-4', city: 'İstanbul', code: 'E4' }]; }
  else if (url.indexOf('/CRM/VisitEvidenceFlags/api/flags/') === 0) { d = detail; }
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
global.window = {
  document: global.document,
  L10n: texts,
  DitenDataTable: {
    getAuthHeaders: () => ({ 'X-Test-Auth': '1' }),
    renderActions: acts => acts.filter(a => a.visible !== false).map(a => '[' + a.key + ']').join(''),
    // like the real factory: handle.appliedFilters is a SNAPSHOT of the first moment (Object.assign copies the getter's
    // value); the live state is the last request's query (dt.ajax.params(), the factory's own string)
    createList: async o => { listOptions = o; return { appliedFilters: Object.assign({}, applied), dt: { ajax: { params: () => paramsOf(applied) } } }; },
    createCrudTable: o => { crud.push(o); return repDt; }
  },
  bootstrap: { Tab: { getOrCreateInstance: el => ({ show() { shownTabs.push(el.id); } }) } },
  showToast: (m, t) => toasts.push([m, t]),
  location: { assign: u => assigned.push(u) }
};
require(dir + '/VisitPlanning/format.js');
window.VisitEvidenceFlagsCore = require(dir + '/VisitEvidenceFlags/flags-core.js');
require(dir + '/VisitEvidenceFlags/rep-summary.js');
require(dir + '/VisitEvidenceFlags/index.js');
const tick = () => new Promise(r => setTimeout(r, 15));

(async () => {
  ready(); await tick(); await tick();
  const o = listOptions;
  const defs = o.config.columnDefs;
  const def = t => defs.find(d => d.targets === t);
  const out = { mode: mode };
  out.options = {
    dataMode: o.dataMode, ajax: o.ajax.url, exportMode: o.export.mode, exportUrl: o.export.url, onResponse: typeof o.onResponse,
    quickView: o.quickView.offcanvasId + '/' + o.quickView.actionKey, fields: o.filters.fields.map(f => f.id + ':' + f.key + ':' + f.kind).join(','),
    savedView: o.savedView.moduleKey + '/' + o.savedView.pageKey, baseOrder: JSON.stringify(o.savedView.baseOrder), saveViewColumns: JSON.stringify(o.savedView.saveViewColumnIndexes),
    addNew: o.toolbar.addNewText, exportColumns: JSON.stringify(o.toolbar.exportColumns), searching: o.config.searching, order: JSON.stringify(o.config.order),
    anySortable: defs.some(d => d.orderable === true) || !defs.some(d => d.targets === '_all' && d.orderable === false),
    columns: o.config.columns.map(c => c.data).join(','), serverSideWritten: 'serverSide' in o.config || 'serverSide' in o, ajaxData: typeof o.ajax.data
  };

  // the lookups (loadOptions with the factory's fillSelect)
  const filled = {};
  await o.filters.loadOptions({ fillSelect: (sel, items, opts) => { filled[sel.id] = { showAll: opts && opts.showAllText, items: (items || []).map(i => i.value + '=' + i.text) }; } });
  out.lookups = filled;

  // the cells
  const cell = (t, r) => def(t).render(null, 'display', r);
  out.cells = rows.map(r => ({ start: cell(1, r), rep: cell(2, r), target: cell(3, r), kind: cell(4, r), flags: cell(5, r), duration: cell(6, r), exception: cell(7, r), actions: def(-1).render(null, 'display', r) }));

  // a list answer → the institutions feed the filter (G-7 read once), the summary follows the APPLIED state
  o.onResponse({ data: { items: rows, total: 412, filteredTotal: 37 } }); await tick(); await tick(); await tick();
  out.repTable = crud.length ? { count: crud.length, dataMode: crud[0].dataMode, url: crud[0].ajax.url, savedView: 'savedView' in crud[0], filters: 'filters' in crud[0],
    paging: crud[0].config.paging, searching: crud[0].config.searching, columns: crud[0].config.columns.map(c => c.data).join(',') } : null;
  out.accountOptions = get('filterAccount').children.map(c => c.value + '=' + c.textContent);
  out.labelCalls = calls.filter(c => c.url.indexOf('/account-labels') > -1).map(c => c.url);
  out.authHeaders = calls.every(c => c.headers && c.headers['X-Test-Auth'] === '1');

  // the summary read answers the card AND the rep table (one request)
  const repRows = crud[0].ajax.dataSrc({ data: summary });
  out.repRows = repRows.length;
  out.card = { started: get('vf-sum-started').textContent, flagged: get('vf-sum-flagged').textContent, rate: get('vf-sum-rate').textContent, range: get('vf-sum-range').textContent, bars: get('vf-bars').innerHTML };
  out.repOptions = get('filterRep').children.map(c => c.value + '=' + c.textContent);
  const rdefs = crud[0].config.columnDefs;
  const rdef = t => rdefs.find(d => d.targets === t);
  out.repCells = { rep: rdef(1).render(null, 'display', summary.reps[1]), started: rdef(2).render(136, 'display', summary.reps[1]), rate: rdef(4).render(null, 'display', summary.reps[1]),
    top: rdef(5).render('location_far', 'display', summary.reps[1]), action: rdef(-1).render(null, 'display', summary.reps[1]) };

  // Save View: a saved state is the APPLIED state → the card + rep table read it; the same state twice reads once
  applied = { period: 'last30', from: '2026-01-01', to: '2026-01-31', resourceId: 'rep-2', flags: ['location_far'], accountId: ACC, scope: 'all' };
  o.onResponse({ data: { items: [], total: 0, filteredTotal: 0 } }); await tick();
  o.onResponse({ data: { items: [], total: 0, filteredTotal: 0 } }); await tick();
  applied = { period: 'custom', from: '2026-09-01', to: '2026-09-30', resourceId: '', flags: [], accountId: '', scope: '' };
  o.onResponse({ data: { items: [], total: 0, filteredTotal: 0 } }); await tick();
  out.summaryLoads = loads.slice();

  // M4 — the empty state in the standard empty row (the title follows the applied scope)
  applied = { period: '', from: '', to: '', resourceId: '', flags: [], accountId: '', scope: '' };
  const emptyApi = { api: () => ({ rows: () => ({ count: () => 0 }) }) };
  o.config.drawCallback.call(emptyApi); out.emptyFlagged = get('dt-empty-cell').innerHTML;
  applied = Object.assign({}, applied, { scope: 'all' });
  o.config.drawCallback.call(emptyApi); out.emptyAll = get('dt-empty-cell').innerHTML;
  get('dt-empty-cell').innerHTML = 'untouched';
  o.config.drawCallback.call({ api: () => ({ rows: () => ({ count: () => 3 }) }) }); out.emptyWithRows = get('dt-empty-cell').innerHTML;

  // the 92-day rule on Apply: the capture guard runs BEFORE the factory's Apply and stops a bad range
  let factoryApplies = 0;
  get('btnFilterApply').addEventListener('click', () => { factoryApplies++; });
  get('filterPeriod').value = 'custom'; dispatch(get('filterPeriod'), 'change');
  out.rangeShownForCustom = !get('filterRange').classList.contains('d-none');
  get('vf-from').value = '2026-06-01'; get('vf-to').value = '2026-09-28';                       // 120 days
  const refused = get('btnFilterApply').click();
  out.guardLong = { applies: factoryApplies, stopped: refused.defaultPrevented, toast: toasts[toasts.length - 1], invalid: get('vf-from').classList.contains('is-invalid') };
  get('vf-from').value = '2026-07-14'; get('vf-to').value = '2026-10-14';                       // 93 days
  get('btnFilterApply').click();
  out.guard93 = { applies: factoryApplies, toast: toasts[toasts.length - 1] && toasts[toasts.length - 1][0] };
  get('vf-from').value = '2026-07-15'; get('vf-to').value = '2026-10-14';                       // 92 days
  const toastsBefore = toasts.length;
  get('btnFilterApply').click();
  out.guard92 = { applies: factoryApplies, toasts: toasts.length - toastsBefore, invalid: get('vf-from').classList.contains('is-invalid') };
  get('filterPeriod').value = 'last30'; dispatch(get('filterPeriod'), 'change');               // a preset clears the stale range
  get('vf-from').value = '2026-01-01';
  get('btnFilterApply').click();
  out.guardPreset = { applies: factoryApplies, from: get('vf-from').value, to: get('vf-to').value, hidden: get('filterRange').classList.contains('d-none') };

  // "only flagged" — the switch writes the hidden scope; a restored scope moves the switch
  get('vf-only').checked = false; dispatch(get('vf-only'), 'change');
  out.scopeFromSwitch = get('filterScope').value;
  get('filterScope').value = ''; dispatch(get('filterScope'), 'change');
  out.switchFromScope = get('vf-only').checked;

  // M3 — the rep summary filters the visits: the action button and a click on the row
  const before = factoryApplies;
  crud[0].actions.onRowAction.filterRep({ row: { resourceId: 'rep-9' } });
  out.repAction = { value: get('filterRep').value, applies: factoryApplies - before, tab: shownTabs[shownTabs.length - 1], added: get('filterRep').children.some(c => c.value === 'rep-9') };
  const tr = { _row: { resourceId: 'rep-2' } };
  dispatch(repsTable, 'click', { target: { closest: q => (q === 'tbody tr' ? tr : null) } });
  out.repRowClick = { value: get('filterRep').value, applies: factoryApplies - before };

  // M2 — the quick view: the row at once, then the detail (slides, exception, photo time)
  o.quickView.populate(rows[4]);
  out.quickFirst = get('oc-details-list').innerHTML;
  await tick(); await tick();
  out.quick = { sub: get('oc-subtitle').innerHTML, target: get('oc-target').innerHTML, body: get('oc-details-list').innerHTML, open: get('oc-btn-edit').getAttribute('href') };
  o.quickView.populate(rows[2]); out.quickFar = get('oc-details-list').innerHTML;
  o.actions.onRowAction.open({ row: rows[1] });
  out.assigned = assigned.slice();
  out.calls = calls.map(c => c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e && e.stack || e); process.exit(1); });
