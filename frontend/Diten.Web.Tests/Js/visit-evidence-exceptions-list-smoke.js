/* WP-VW-W3E · W3E-E4R2-FIX-b - the time-boxed exceptions list (index.js) in Node: a fake document, a stub list factory
   (DitenDataTable.createList records what the page asks of it), the real format.js + evidence-core.js. Run by
   VisitEvidenceSettingsListWebTests with the CRM script folder as argv[2], a JSON file of the Turkish texts (the real resx,
   the window.L10n payload) as argv[3] and the mode as argv[4] ('manage' | 'read'); prints one JSON object. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manage';
const ME = 'user-me';
const nodes = {};
const mk = id => ({ id, attrs: {}, l: {}, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l[ev] = this.l[ev] || []).push(fn); }, querySelector() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
Object.assign(el('ve-root').attrs, { 'data-me': ME, 'data-can-manage-exceptions': mode === 'manage' ? 'true' : 'false' });
const table = mk('dt-visitevidenceexceptions');
const docListeners = {};
global.document = { documentElement: { lang: 'tr' }, readyState: 'complete', getElementById: el,
  querySelector: q => (q === '.datatables-visitevidenceexceptions' ? table : null),
  addEventListener: (ev, fn) => { (docListeners[ev] = docListeners[ev] || []).push(fn); } };

let options = null;
const reloads = [];
const handle = { appliedFilters: {}, dt: { ajax: { reload: (cb, keep) => reloads.push(keep) }, columns: { adjust() {} }, responsive: { recalc() {} } } };
const pageCalls = [];
global.window = {
  document: global.document,
  L10n: texts,
  DitenDataTable: { createList: async o => { options = o; return handle; }, getAuthHeaders: () => ({ Authorization: 'Bearer t' }), renderActions: items => items.map(i => '<a data-row-action="' + i.key + '" class="' + (i.className || '') + '" title="' + (i.attrs && i.attrs.title || '') + '"></a>').join('') },
  VisitEvidenceSettingsPage: { openNew: () => pageCalls.push(['openNew']), openException: (id, o) => pageCalls.push(['openException', id, !!(o && o.row)]) }
};
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceCore = require(dir + '/VisitEvidenceSettings/evidence-core.js');
const out = { mode };
try { require(dir + '/VisitEvidenceSettings/index.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));

(async () => {
  await tick();
  const o = options || {};
  out.dataMode = o.dataMode;
  out.tableIsTheListShells = o.tableEl === table;
  out.ajaxUrl = o.ajax && o.ajax.url;
  out.ajaxHeaders = o.ajax && o.ajax.headers;
  out.serverExport = o.export === undefined ? null : o.export;
  // the proxy query follows the APPLIED filters
  out.queryBaseline = o.ajax.data();
  handle.appliedFilters = { status: 'in_review', phase: '' };
  out.queryStatus = o.ajax.data();
  handle.appliedFilters = { status: '', phase: 'active' };
  out.queryPhase = o.ajax.data();
  handle.appliedFilters = {};
  // the filter fields (the same values filter the rows on the table)
  out.fields = (o.filters.fields || []).map(f => [f.id, f.key, f.kind]);
  const [status, phase] = o.filters.fields;
  out.matches = [status.matches({ status: 'draft' }, 'draft'), status.matches({ status: 'draft' }, 'in_review'), status.matches({ status: 'draft' }, ''),
    phase.matches({ status: 'approved', phase: 'active' }, 'active'), phase.matches({ status: 'draft', phase: null }, 'active')];
  out.filterHost = [o.filters.hostId, o.filters.collapseId];
  out.savedView = o.savedView;
  out.quickView = { id: o.quickView.offcanvasId, key: o.quickView.actionKey };
  o.quickView.populate({ id: 'x-1', status: 'draft' });
  out.toolbar = { addNewText: o.toolbar.addNewText, exportColumns: o.toolbar.exportColumns, colvisColumns: o.toolbar.colvisColumns };
  o.toolbar.onAddNew();
  out.config = { order: o.config.order, emptyTable: o.config.language.emptyTable, columns: o.config.columns.map(c => c.data) };

  // the read: CRM names + the rows
  const today = new Date().toISOString().slice(0, 10);
  const rows = o.ajax.dataSrc({ data: {
    items: [
      { id: 'x-1', resourceId: 'rep-1', repDisplayName: 'Okan Tekin', rules: ['location_info', 'photo_optional'], startDate: '2026-10-10', endDate: '2026-10-13', days: 4,
        reason: 'GPS arızalı', status: 'in_review', phase: null, createdBy: ME, approvedBy: null },
      { id: 'x-2', resourceId: 'rep-2', repDisplayName: 'Ayşe Demir', rules: ['location_info'], startDate: today, endDate: today, days: 1,
        reason: 'Yeni kat', status: 'approved', phase: 'active', createdBy: 'mgr-1', approvedBy: 'mgr-2' }],
    statusCounts: { in_review: 1, approved: 1 }, phaseCounts: { active: 1 }, displayNames: { 'MGR-1': 'Deniz Er', 'mgr-2': 'Selin Ak' } } });
  out.rowCount = rows.length;
  const defs = o.config.columnDefs;
  const cell = (target, type, row) => { const d = defs.find(c => c.targets === target); return d.render(row[o.config.columns[target === -1 ? 7 : target].data], type, row); };
  const [a, b] = rows;
  out.exportRow = [1, 2, 3, 4, 5, 6].map(t => cell(t, 'export', a));
  out.exportRow2 = [1, 5, 6].map(t => cell(t, 'export', b));
  out.sortRange = cell(3, 'sort', a);
  out.displayRep = cell(1, 'display', a);
  out.displayStatus = cell(6, 'display', b);
  out.actions = cell(-1, 'display', a);
  out.priorities = defs.map(d => [d.targets, d.className || '', d.responsivePriority == null ? null : d.responsivePriority]);

  // Apply / Reset re-read with the new query; showing the tab re-measures the table
  (el('btnFilterApply').l.click || []).forEach(fn => fn());
  (el('btnFilterReset').l.click || []).forEach(fn => fn());
  await tick();
  out.reloads = reloads;
  out.reloadApi = typeof (global.window.VisitEvidenceExceptionsList || {}).reload;
  out.pageCalls = pageCalls;
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
