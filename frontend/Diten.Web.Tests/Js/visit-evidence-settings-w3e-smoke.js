/* WP-VW-W3E (W3E-WEB-b) - DOM smoke of visit-evidence-settings.js for the W3E screens E1-E3 in Node (the W3-WEB-d
   visit-evidence-settings-smoke.js set-up). Run by VisitEvidenceFlagsWebTests with the CRM script folder as argv[2], a JSON
   file of the Turkish script texts (the real resx) as argv[3] and the mode as argv[4] ('qr' = with crm.visit-qr.read |
   'noqr'); prints one JSON object.
   E1: the "additional evidence" card (QR: off / optional / required cards; device: off / flag + a DISABLED "block · soon");
       a click on "optional" changes the draft, "block" can never be taken (neither its disabled button nor a forged
       click), the PUT carries qrMode + antiFraudMode and never "block"; the manage link only with a QR key.
   E2: the exception window offers qr_optional ("new" badge + description) only when the rep's QR is required.
   E3: the effective rules show the two new rows ("new"), the reps pane the four columns with "· exception", the history
       names the fields and values in the user's language (never "qrMode" / "antiFraudMode").
   W3E-E4R2-FIX-d: the inner panes come from the shared tab card's diten:tab-change (form · history · reps); the history and
       the rep roster are the page's second / third tables (settings-tables.js) on the REAL factory's wire pieces
       (toServerQuery / toDataTablesResponse) with the table builder faked: each draw asks the proxy with the list contract
       (start · length · search · draw), the fake proxy pages 26 reps (the picker read stops at 20) — page 1, page 2, a
       search; columns.adjust is counted when a pane shows. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'qr';
const ME = 'user-me';
const nodes = {};
const listeners = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, min: '', dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; },
  // W3E-E4R2-FIX-b — the tab's corner count is a span the script appends to the shared tab card's button
  kids: [], appendChild(c) { this.kids.push(c); c.parent = this; return c; }, remove() { if (this.parent) { this.parent.kids = this.parent.kids.filter(k => k !== this); } },
  querySelector(q) { return q === '.wcn-tab-count' ? (this.kids.find(k => String(k.className).split(' ').indexOf('wcn-tab-count') > -1) || null) : null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('ve-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-me': ME, 'data-can-manage-policy': 'true', 'data-can-read-exceptions': 'true',
  'data-can-manage-exceptions': 'true', 'data-can-read-qr-codes': mode === 'qr' ? 'true' : 'false', 'data-qr-codes-url': '/CRM/VisitQrCodes' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
el('ve-save-bar').classList.add('d-none');
const docListeners = {};
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { docListeners[ev] = fn; },
  createElement: () => mk('x'), body: { appendChild() {} } };
const tabChange = key => docListeners['diten:tab-change']({ detail: { key } });

const today = new Date().toISOString().slice(0, 10);
const policy = { locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true,
  durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', qrMode: 'required', antiFraudMode: 'flag',
  isDefault: false, version: 7, updatedBy: ME, updatedAt: '2026-10-14T09:12:00Z', repCount: 3 };
const qrException = { id: 'x-qr', resourceId: 'rep-3', repDisplayName: 'Elif Şahin', rules: ['location_info', 'qr_optional'], startDate: today, endDate: today, days: 1,
  reason: 'Yeni kat', status: 'approved', phase: 'active', createdBy: 'other', abilities: {}, version: 3 };
const list = { items: [qrException], statusCounts: { approved: 1 }, phaseCounts: { active: 1 } };
const effective = { resourceId: 'rep-3', fields: [
  { field: 'locationMode', value: 'info', source: 'exception', exceptionId: 'x-qr' }, { field: 'photoMode', value: 'optional', source: 'tenant' },
  { field: 'accountWithoutLocation', value: 'allow_flagged', source: 'tenant' }, { field: 'qrMode', value: 'optional', source: 'exception', exceptionId: 'x-qr' },
  { field: 'antiFraudMode', value: 'flag', source: 'tenant' }], activeException: qrException, relaxedRules: ['location_info', 'qr_optional'], scopeChain: { note: 'unresolved' } };
const effectiveOff = { resourceId: 'rep-1', fields: [{ field: 'locationMode', value: 'required', source: 'tenant' }, { field: 'qrMode', value: 'off', source: 'tenant' }], activeException: null, relaxedRules: [] };
const effectiveReq = { resourceId: 'rep-2', fields: [{ field: 'locationMode', value: 'required', source: 'tenant' }, { field: 'qrMode', value: 'required', source: 'tenant' }], activeException: null, relaxedRules: [] };
const history = { items: [
  { at: '2026-10-14T09:12:00Z', by: ME, field: 'qrMode', oldValue: 'off', newValue: 'required', kind: 'set' },
  { at: '2026-10-14T09:12:00Z', by: ME, field: 'antiFraudMode', oldValue: 'off', newValue: 'flag', kind: 'set' },
  { at: '2026-09-28T16:40:00Z', by: ME, field: 'shortVisitSeconds', oldValue: '90', newValue: '120', kind: 'set' }], total: 3, filteredTotal: 3, page: 1, pageSize: 100, displayNames: { [ME]: 'Deniz Er' } };
// W3E-E4R2-FIX-d — the Web proxy's list contract over 26 reps (the three named + 23 more, by name like the CRM)
const roster = [{ resourceId: 'rep-1', displayName: 'Sema Kara' }, { resourceId: 'rep-2', displayName: 'Murat Demir' }, { resourceId: 'rep-3', displayName: 'Elif Şahin' }]
  .concat(Array.from({ length: 23 }, (_, i) => ({ resourceId: 'rep-x' + String(i + 1).padStart(2, '0'), displayName: 'Temsilci ' + String(i + 1).padStart(2, '0') })))
  .sort((a, b) => a.displayName.localeCompare(b.displayName, 'tr'));
function rosterPage(url) {
  const q = new URLSearchParams(url.slice(url.indexOf('?') + 1));
  const start = Number(q.get('start')) || 0, length = Number(q.get('length')) || 10, search = (q.get('search') || '').toLocaleLowerCase('tr');
  const hit = roster.filter(r => !search || r.displayName.toLocaleLowerCase('tr').indexOf(search) > -1);
  return { items: hit.slice(start, start + length), total: roster.length, filteredTotal: hit.length };
}
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null;
  if (url === '/api/policy' && method === 'GET') d = policy;
  else if (url === '/api/policy' && method === 'PUT') d = Object.assign({}, policy, { version: 8 });
  else if (url.indexOf('/api/policy/reps/roster') === 0) d = rosterPage(url);
  else if (url.indexOf('/api/policy/reps') === 0) d = [{ resourceId: 'rep-1', displayName: 'Sema Kara' }, { resourceId: 'rep-2', displayName: 'Murat Demir' }, { resourceId: 'rep-3', displayName: 'Elif Şahin' }];
  else if (url.indexOf('/api/policy/effective') === 0) d = url.indexOf('rep-3') > -1 ? effective : url.indexOf('rep-2') > -1 ? effectiveReq : effectiveOff;
  else if (url.indexOf('/api/policy/history') === 0) d = history;
  else if (url === '/api/exceptions' && method === 'GET') d = list;
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [];
global.window = { document: global.document, location: { search: '' }, showToast: () => {},
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) }, Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) } } };
// W3E-E4R2-FIX-d — the REAL factory (its wire pieces); the table builder becomes a fake that draws through the proxy
require(dir + '/../diten-datatable.js');
const real = global.window.DitenDataTable;
const tables = {};
const renderRows = (config, rows) => rows.map(row => '<tr>' + config.columns.map((c, i) => {
  const def = config.columnDefs.find(d => d.targets === i);
  const cls = def && def.className ? ' class="' + def.className + '"' : '';
  return '<td' + cls + '>' + (def && def.render ? def.render(row[c.data], 'display', row) : (row[c.data] == null ? '' : row[c.data])) + '</td>';
}).join('') + '</tr>').join('');
async function draw(t, start, length, search) {
  const o = t.options;
  const query = o.ajax.data({ draw: t.urls.length + 1, start, length, search: { value: search || '' }, order: [], columns: o.config.columns.map(c => ({ data: c.data, orderable: false })) });
  const url = o.ajax.url + '?' + query;
  t.urls.push(url);
  const r = await fetch(url, { method: 'GET' });
  const json = JSON.parse(o.ajax.dataFilter.call({ url: url }, await r.text()));
  t.last = { html: renderRows(o.config, json.data.map(x => x)), draw: json.draw, recordsTotal: json.recordsTotal, recordsFiltered: json.recordsFiltered, rows: json.data.length };
  return t.last;
}
global.window.DitenDataTable = Object.assign({}, real, {
  createCrudTable: o => {
    const t = tables[o.tableEl.id] = { options: o, urls: [], adjusted: 0, last: null };
    t.drawn = draw(t, 0, 10, '');
    return { ajax: { reload: () => { t.drawn = draw(t, 0, 10, ''); } }, columns: Object.assign(() => ({ indexes: () => ({ toArray: () => [] }) }), { adjust: () => { t.adjusted++; } }) };
  }
});
global.window.DtDefaults = { exportButtons: (addNew, attr, extra, options) => ({ options }) };
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceCore = require(dir + '/VisitEvidenceSettings/evidence-core.js');
global.window.VisitEvidenceSettingsL10n = texts;
const out = { mode };
try {
  require(dir + '/VisitEvidenceSettings/settings-tables.js');
  require(dir + '/VisitEvidenceSettings/visit-evidence-settings.js');
  out.createError = null;
} catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const target = map => ({ closest: q => (map[q] ? Object.assign({ disabled: !!map[q].disabled, getAttribute: k => map[q][k] }, map[q].props || {}) : null) });

(async () => {
  await tick(); await tick();
  out.extra = el('ve-extra').innerHTML;
  // E1 — "optional" is taken; the "block" button has no data-opt (a click finds nothing); a FORGED block click is refused
  listeners.click({ target: target({ 'button[data-opt]': { 'data-field': 'qrMode', 'data-opt': 'optional' } }) });
  out.extraAfterOptional = el('ve-extra').innerHTML;
  listeners.click({ target: target({}) });
  listeners.click({ target: target({ 'button[data-opt]': { 'data-field': 'antiFraudMode', 'data-opt': 'block' } }) });
  out.extraAfterBlock = el('ve-extra').innerHTML;
  if (el('ve-save').l) { el('ve-save').l.click(); }
  out.saveDiff = el('ve-save-diff').innerHTML;
  if (el('ve-save-confirm').l) { el('ve-save-confirm').l.click(); await tick(); await tick(); }
  out.put = (calls.find(c => c.method === 'PUT') || {}).body || null;

  // E3 — the effective rules of a rep under the QR exception
  el('ve-look').l.change({ target: { value: 'rep-3' } }); await tick(); await tick();
  out.effective = el('ve-eff-rows').innerHTML;
  // E3 — the reps pane (the inner tab card's diten:tab-change); nothing is built before its pane shows
  out.builtBeforePane = Object.keys(tables);
  tabChange('reps'); await tick(); await tick();
  const reps = tables['dt-ve-reps'];
  out.repsShown = !!reps;
  if (reps) {
    await reps.drawn;
    out.reps = reps.last.html;
    out.repsFirst = { url: reps.urls[0], draw: reps.last.draw, total: reps.last.recordsTotal, filtered: reps.last.recordsFiltered, rows: reps.last.rows };
    out.repsDataMode = reps.options.dataMode; out.repsServerSide = reps.options.config.serverSide; out.repsSearching = reps.options.config.searching;
    out.repsExportPermitted = reps.options.config.buttons.options.exportPermitted;
    const p3 = await draw(reps, 20, 10, '');
    out.repsPage3 = { url: reps.urls[reps.urls.length - 1], rows: p3.rows, total: p3.recordsTotal, html: p3.html };
    const s = await draw(reps, 0, 10, 'elif');
    out.repsSearch = { url: reps.urls[reps.urls.length - 1], rows: s.rows, total: s.recordsTotal, filtered: s.recordsFiltered, html: s.html };
    out.repsAdjusted = reps.adjusted;
  }
  // E3 — the history pane
  tabChange('history'); await tick(); await tick();
  const hist = tables['dt-ve-history'];
  if (hist) {
    await hist.drawn;
    out.history = hist.last.html;
    out.historyFirst = { url: hist.urls[0], draw: hist.last.draw, total: hist.last.recordsTotal };
    out.historyDataMode = hist.options.dataMode; out.historyServerSide = hist.options.config.serverSide; out.historySearching = hist.options.config.searching;
    out.historyAdjusted = hist.adjusted;
  }
  // back to the form, then the outer tabs: coming back to settings measures the pane's table again
  tabChange('reps'); tabChange('exceptions'); tabChange('settings');
  out.repsAdjustedAfterSettings = reps ? reps.adjusted : 0;
  out.tablesBuilt = Object.keys(tables);

  // E2 — the exception window: qr_optional offered for a rep whose QR is required, not for one whose QR is off
  window.VisitEvidenceSettingsPage.openNew(); await tick();
  el('ve-nx-rep').l.change({ target: { value: 'rep-2' } }); await tick(); await tick();
  out.rulesRequired = el('ve-nx-rules').innerHTML;
  el('ve-nx-rep').l.change({ target: { value: 'rep-1' } }); await tick(); await tick();
  out.rulesOff = el('ve-nx-rules').innerHTML;
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
