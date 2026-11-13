/* WP-VW-W5 · W5-WEB — the sent visit e-mails page on the REAL index.js + settings.js + notifications-core.js + format.js in
   Node, with a fake list factory (DitenDataTable.createList records what the page hands it) and a small fake DOM. Run by
   VisitNotificationsWebTests with the CRM script folder as argv[2], a JSON file of the page's L10n words (the real resx, the
   reader's culture) as argv[3] and the mode as argv[4]:
     manager (read + settings.manage: the settings card exists and loads) · rep (read only: no card, no settings script)
     · ar (manager, Arabic, dir=rtl).
   The clock is pinned to 2026-10-20 whatever day the suite runs. Prints one JSON object. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manager';
const manage = mode !== 'rep';
const NOW = Date.UTC(2026, 9, 20, 9, 0, 0);
Date.now = () => NOW;

// ── a small fake DOM ─────────────────────────────────────────────────────────────────────────────────────────
const els = {};
function mk(id, tag) {
  const e = {
    id: id, tagName: String(tag || 'div').toUpperCase(), value: '', checked: false, disabled: false, textContent: '', className: '', attrs: {}, children: [], listeners: [], _cls: new Set(), _html: '',
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
const absent = new Set(manage ? [] : ['vn-settings-form', 'vn-settings', 'vn-pane-settings', 'vn-tab-settings', 'vn-pv-email', 'vn-msl-email', 'vn-settings-save']);
function get(id) { if (absent.has(id)) { return null; } if (!els[id]) { els[id] = mk(id); } return els[id]; }
function dispatch(el, type, extra) {
  let stopped = false;
  const ev = Object.assign({ type: type, target: el, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopImmediatePropagation() { stopped = true; }, stopPropagation() {} }, extra || {});
  const order = el.listeners.filter(l => l.type === type && l.capture).concat(el.listeners.filter(l => l.type === type && !l.capture));
  for (const l of order) { if (stopped) { break; } l.fn(ev); }
  return ev;
}
const table = get('dt-visitnotifications');
get('vn-root').attrs['data-can-manage'] = manage ? 'true' : 'false';
['filterRangeFrom', 'filterRangeTo'].forEach(id => get(id).classList.add('d-none'));
if (!manage) { get('filterRepChip').classList.add('d-none'); }
const ready = [];
const docListeners = {};
global.document = {
  documentElement: { lang: mode === 'ar' ? 'ar' : 'tr', dir: mode === 'ar' ? 'rtl' : 'ltr' },
  getElementById: get,
  querySelector: q => (q === '.datatables-visitnotifications' ? table : null),
  createElement: tag => mk(null, tag),
  addEventListener: (type, fn) => { if (type === 'DOMContentLoaded') { ready.push(fn); } else { (docListeners[type] = docListeners[type] || []).push(fn); } }
};

// ── the world: the rows, the details, the settings ─────────────────────────────────────────────────────────
const REP1 = 'rep-1', REP2 = 'rep-2';
const row = (id, extra) => Object.assign({
  id: id, eventCode: 'crm.visitreport.submitted', status: 'sent', plannedVisitId: 'pv-' + id, visitReportId: 'vr-' + id, resourceId: REP1, resourceName: 'Sema Kara',
  visitDate: '2026-10-08', createdAt: '2026-10-08T08:58:00Z', sentAt: '2026-10-08T08:58:30Z', attempts: 1, notes: [],
  target: { doctorName: 'Dr. Hakan Öztürk', institutionName: 'Kardiyoloji Polikliniği' },
  recipients: [{ role: 'to', kind: 'line-manager', userId: REP1, mailbox: null }], resolved: [{ role: 'to', userId: 'mgr-1', via: 'line-manager' }]
}, extra || {});
const rows = [
  row('n1'),
  // the CRM's pre-W5-FIX code (hyphenated); the PV mailbox not set → route pending, the manager moved up to "to"
  row('n2', { eventCode: 'crm.visit-report.adverse-event', status: 'route-pending', notes: ['pv-route-pending'], resourceId: REP2, resourceName: 'Murat Demir',
    recipients: [{ role: 'to', kind: 'mailbox', userId: null, mailbox: 'pv' }, { role: 'cc', kind: 'line-manager', userId: REP2, mailbox: null }],
    resolved: [{ role: 'to', userId: 'mgr-2', via: 'manager-chain' }] }),
  row('n3', { eventCode: 'crm.visitreport.offlabelrequest', recipients: [{ role: 'to', kind: 'mailbox', userId: null, mailbox: 'msl' }, { role: 'cc', kind: 'line-manager', userId: REP1, mailbox: null }],
    resolved: [{ role: 'to', userId: null, via: 'email' }, { role: 'cc', userId: 'mgr-1', via: 'line-manager' }] }),
  row('n4', { eventCode: 'crm.visitreport.overdue', status: 'failed', notes: ['platform-unavailable', 'attempts-exhausted'], attempts: 5, sentAt: null, visitReportId: null }),
  // a row whose detail could not be read: no target, no recipients → the roles derive from the event
  row('n5', { eventCode: 'crm.visit-report.adverse-event', status: 'no-recipient', target: undefined, recipients: undefined, resolved: undefined })
];
const variables = {
  repName: 'Murat Demir', doctorName: 'Dr. Hakan Öztürk', institutionName: 'Kardiyoloji Polikliniği', visitDate: '2026-10-08',
  reportUrl: '/CRM/VisitReport?plannedVisitId=pv-n2', products: 'UROFLEKS, TUTUKON',
  adverseEventNote: 'Hekim baş dönmesi aktardı; ayrıntı için dr.hakan@hastane.org adresine yazılabilir.'
};
const details = {
  n2: { row: rows[1], recipients: rows[1].recipients, resolved: rows[1].resolved, variables: variables },
  n1: { row: rows[0], recipients: rows[0].recipients, resolved: rows[0].resolved,
    variables: { repName: 'Sema Kara', doctorName: 'Dr. Hakan Öztürk', institutionName: 'Kardiyoloji Polikliniği', visitDate: '2026-10-08', reportUrl: 'javascript:alert(1)',
      products: 'UROFLEKS', note: 'Literatür istendi', adverseEvent: 'false', offLabelRequest: 'true', followUpDate: '2026-10-15' } },
  n4: { row: rows[3], recipients: rows[3].recipients, resolved: [], variables: { repName: 'Sema Kara', doctorName: 'Doç. Dr. Mehmet Yılmaz', institutionName: '', visitDate: '2026-10-05', reportUrl: '/CRM/VisitReport?plannedVisitId=pv-n4', deadline: '2026-10-07T10:02:00Z' } }
};
// the same e-mail under the catalogue's hyphen-less code
details.n2b = { row: Object.assign({}, rows[1], { id: 'n2b', eventCode: 'crm.visitreport.adverseevent' }), recipients: rows[1].recipients, resolved: rows[1].resolved, variables: variables };

let settings = { pvEmail: 'pv.guvenlik@firma.com', mslEmail: '', overdueReminders: true, reportSummary: false, version: 3, isDefault: false, updatedAt: '2026-10-19T12:00:00Z', updatedBy: 'c5769c62-0000-4000-8000-0000000000aa', updatedByName: 'Ayşe Yönetici' };
let putAnswer = null;
const calls = [];
global.fetch = (url, init) => {
  const method = (init && init.method) || 'GET';
  calls.push({ url: url, method: method, body: init && init.body ? JSON.parse(init.body) : null, headers: init && init.headers });
  const ok = (d) => Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
  if (url === '/CRM/VisitNotifications/api/settings' && method === 'GET') { return ok(settings); }
  if (url === '/CRM/VisitNotifications/api/settings' && method === 'PUT') {
    const a = putAnswer || { status: 200, body: { data: Object.assign({}, settings, JSON.parse(init.body), { version: settings.version + 1 }) } };
    return Promise.resolve({ ok: a.status < 300, status: a.status, text: () => Promise.resolve(JSON.stringify(a.body)) });
  }
  const m = /\/CRM\/VisitNotifications\/api\/list\/([^/?]+)$/.exec(url);
  if (m) { return ok(details[m[1]] || null); }
  return Promise.resolve({ ok: false, status: 404, text: () => Promise.resolve('') });
};

// ── the fake factory ──────────────────────────────────────────────────────────────────────────────────────
let listOptions = null;
const toasts = [];
const shownTabs = [];
global.window = {
  document: global.document,
  L10n: texts,
  DitenDataTable: {
    getAuthHeaders: () => ({ 'X-Test-Auth': '1' }),
    renderActions: acts => acts.filter(a => a.visible !== false).map(a => '[' + a.key + ']').join(''),
    createList: async o => { listOptions = o; return { dt: { columns: { adjust() {} }, responsive: { recalc() {} } } }; }
  },
  DitenTabs: { activate: el => { shownTabs.push(el.id); return true; } },
  DitenDateField: { guardAgainstSilentMisparse: () => {} },
  showToast: (m, t) => toasts.push([m, t]),
  location: { search: '', assign: () => {} }
};
require(dir + '/VisitPlanning/format.js');
window.VisitNotificationsCore = require(dir + '/VisitNotifications/notifications-core.js');
if (manage) { require(dir + '/VisitNotifications/settings.js'); }
require(dir + '/VisitNotifications/index.js');
const tick = () => new Promise(r => setTimeout(r, 15));
const EMAIL = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/;

(async () => {
  ready.forEach(fn => fn()); await tick(); await tick();
  const o = listOptions;
  const defs = o.config.columnDefs;
  const def = t => defs.find(d => d.targets === t);
  const out = { mode: mode, settingsScript: typeof window.VisitNotificationsSettings };
  out.options = {
    dataMode: o.dataMode, ajax: o.ajax.url, exportMode: o.export.mode, exportUrl: o.export.url, onResponse: typeof o.onResponse,
    quickView: o.quickView.offcanvasId + '/' + o.quickView.actionKey, fields: o.filters.fields.map(f => f.id + ':' + f.key + ':' + f.kind).join(','),
    savedView: o.savedView.moduleKey + '/' + o.savedView.pageKey, saveViewColumns: JSON.stringify(o.savedView.saveViewColumnIndexes),
    exportColumns: JSON.stringify(o.toolbar.exportColumns), searching: o.config.searching, order: JSON.stringify(o.config.order),
    anySortable: defs.some(d => d.orderable === true) || !defs.some(d => d.targets === '_all' && d.orderable === false),
    columns: o.config.columns.map(c => c.data).join(','), serverSideWritten: 'serverSide' in o.config || 'serverSide' in o, ajaxData: typeof o.ajax.data
  };

  const filled = {};
  await o.filters.loadOptions({ fillSelect: (sel, items, opts) => { filled[sel.id] = { showAll: opts && opts.showAllText, items: (items || []).map(i => i.value + '=' + i.text) }; } });
  out.lookups = filled;

  const cell = (t, r) => def(t).render(null, 'display', r);
  out.cells = rows.map(r => ({ date: cell(1, r), event: cell(2, r), target: cell(3, r), rep: cell(4, r), recipients: cell(5, r), status: cell(6, r), actions: def(-1).render(null, 'display', r),
    eventText: def(2).render(null, 'export', r), recipientsText: def(5).render(null, 'export', r), statusText: def(6).render(null, 'export', r) }));

  // the rep chip: hidden for a rep until the list shows more than one rep; the options come from the rows
  out.repChipHiddenBefore = get('filterRepChip').classList.contains('d-none');
  o.onResponse({ data: { items: rows.slice(0, 1) } });
  out.repChipHiddenOneRep = get('filterRepChip').classList.contains('d-none');
  o.onResponse({ data: { items: rows } });
  out.repChipHiddenTwoReps = get('filterRepChip').classList.contains('d-none');
  out.repOptions = get('filterRep').children.map(c => c.value + '=' + c.textContent);

  // the settings card (manager): it loads when its tab shows; the preview must not show its addresses either
  if (manage) {
    (docListeners['diten:tab-change'] || []).forEach(fn => fn({ detail: { key: 'settings' } }));
    await tick(); await tick();
    out.settingsLoaded = { pv: get('vn-pv-email').value, msl: get('vn-msl-email').value, summary: get('vn-report-summary').checked, overdue: get('vn-overdue-reminders').checked, meta: get('vn-settings-meta').textContent };
  }

  // the quick view: the preview of the adverse event e-mail (row at once, then the detail)
  const preview = async (r) => { o.quickView.populate(r); const first = get('oc-details-list').innerHTML; await tick(); await tick(); return { first: first, body: get('oc-details-list').innerHTML, sub: get('oc-subtitle').innerHTML, open: get('oc-btn-edit').attrs.href, openHidden: get('oc-btn-edit').classList.contains('d-none'), openText: get('oc-btn-edit').textContent }; };
  out.adverse = await preview(rows[1]);
  out.adverseHyphenless = await preview(details.n2b.row);
  out.summary = await preview(rows[0]);
  out.overdue = await preview(rows[3]);
  out.noRecipient = await preview(rows[4]);
  const all = [out.adverse, out.adverseHyphenless, out.summary, out.overdue, out.noRecipient].map(p => p.first + p.body + p.sub).join('\n') + out.cells.map(c => Object.values(c).join('')).join('');
  out.addressInPreview = (all.match(EMAIL) || [null])[0];
  out.previewCalls = calls.filter(c => c.url.indexOf('/api/list/') > -1).map(c => c.url);
  out.authHeaders = calls.every(c => c.headers && c.headers['X-Test-Auth'] === '1');

  // empty state
  o.config.drawCallback.call({ api: () => ({ rows: () => ({ count: () => 0 }) }) });
  out.empty = get('dt-empty-cell').innerHTML;

  // the custom range guard: reversed → stopped before the factory's Apply (toast), fine → passes
  let applies = 0;
  get('btnFilterApply').addEventListener('click', () => { applies++; });
  get('filterPeriod').value = 'custom'; dispatch(get('filterPeriod'), 'change');
  out.rangeShown = !get('filterRangeFrom').classList.contains('d-none');
  get('vn-from').value = '2026-10-10'; get('vn-to').value = '2026-10-01';
  get('btnFilterApply').click();
  out.guardReversed = { applies: applies, toast: toasts[toasts.length - 1] };
  get('vn-from').value = '2026-09-01'; get('vn-to').value = '2026-10-01';
  get('btnFilterApply').click();
  out.guardOk = { applies: applies };

  // the settings save (manager): a bad address is never sent; a good one sends the version read; 409 is said in words
  if (manage) {
    const puts = () => calls.filter(c => c.method === 'PUT');
    get('vn-pv-email').value = 'pv@firma'; get('vn-msl-email').value = 'a@b.com, c@d.com';
    await window.VisitNotificationsSettings.save();
    out.saveInvalid = { puts: puts().length, pvInvalid: get('vn-pv-email').classList.contains('is-invalid'), mslInvalid: get('vn-msl-email').classList.contains('is-invalid'), pvMsg: get('vn-pv-email-error').textContent };
    get('vn-pv-email').value = ' yeni.pv@firma.com '; get('vn-msl-email').value = ''; get('vn-report-summary').checked = true; get('vn-overdue-reminders').checked = false;
    await window.VisitNotificationsSettings.save(); await tick();
    out.saveOk = { body: puts()[0] && puts()[0].body, toast: toasts[toasts.length - 1], version: window.VisitNotificationsSettings.version(), pvInvalid: get('vn-pv-email').classList.contains('is-invalid') };
    putAnswer = { status: 409, body: { isSuccessful: false, errors: ['The settings changed since they were loaded. Reload and try again.', 'visit_execution_concurrency_conflict'] } };
    get('vn-pv-email').value = 'baska@firma.com';
    await window.VisitNotificationsSettings.save(); await tick();
    out.save409 = { alert: get('vn-settings-alert').textContent, alertClass: get('vn-settings-alert').className, kept: get('vn-pv-email').value, expected: puts()[1].body.expectedVersion };
    putAnswer = { status: 400, body: { isSuccessful: false, errors: ['pvEmail / mslEmail must each be one e-mail address.', 'visit_notification_email_invalid'] } };
    await window.VisitNotificationsSettings.save(); await tick();
    out.save400 = { alert: get('vn-settings-alert').textContent, pvInvalid: get('vn-pv-email').classList.contains('is-invalid') };
  }
  out.settingsCalls = calls.filter(c => c.url.indexOf('/api/settings') > -1).map(c => c.method);

  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e && e.stack || e); process.exit(1); });
