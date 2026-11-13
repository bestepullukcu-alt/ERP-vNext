/* WP-VW-W3 (W3-WEB-d) - DOM smoke of visit-evidence-settings.js in Node: a fake document, a stub fetch, stub bootstrap.
   Run by VisitEvidenceSettingsWebTests with the CRM script folder as argv[2], a JSON file of the Turkish script texts
   (the real resx) as argv[3] and the mode as argv[4] ('manage' | 'readonly'); prints one JSON object.
   manage: the form → a change → the save confirm (changed rows, "N reps") → PUT; a 31-day exception is refused; a valid
   one is created → sent for approval; an exception in review the caller wrote shows the SoD hint and a disabled Approve.
   readonly: the save bar stays hidden, the controls are disabled, a click changes nothing, "New exception" opens nothing.
   W3E-E4R2-FIX-b: the list is index.js's (the Golden Compact DataTable); this page answers its "New exception" and its
   quick view through window.VisitEvidenceSettingsPage and writes the shared tab card's corner count. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manage';
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
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-me': ME,
  'data-can-manage-policy': mode === 'manage' ? 'true' : 'false', 'data-can-read-exceptions': 'true', 'data-can-manage-exceptions': mode === 'manage' ? 'true' : 'false' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
el('ve-save-bar').classList.add('d-none'); // as the view renders it
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };

const today = new Date().toISOString().slice(0, 10);
const plus = n => { const d = new Date(today + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };
const policy = { locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true,
  durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', isDefault: false, version: 4,
  updatedBy: ME, updatedAt: '2026-10-12T09:00:00Z', repCount: 148 };
const own = { id: 'x-own', resourceId: 'rep-1', repDisplayName: 'Okan Tekin', rules: ['location_info'], startDate: plus(1), endDate: plus(4), days: 4,
  reason: 'GPS arızalı', status: 'in_review', phase: null, createdBy: ME, createdAt: '2026-10-12T08:00:00Z', submittedBy: ME, submittedAt: '2026-10-12T08:05:00Z',
  reviewRounds: [{ roundNo: 1, submittedBy: ME, submittedAt: '2026-10-12T08:05:00Z' }], decisions: [],
  abilities: { canEdit: false, canSubmit: false, canWithdraw: true, canDecide: false, canCancel: false, canEndEarly: false }, version: 2 };
const active = Object.assign({}, own, { id: 'x-act', status: 'approved', phase: 'active', createdBy: 'other', submittedBy: 'other',
  abilities: { canEdit: false, canSubmit: false, canWithdraw: false, canDecide: false, canCancel: false, canEndEarly: true } });
const list = { items: [own, active], statusCounts: { draft: 0, in_review: 1, approved: 1, cancelled: 0, ended_early: 0 }, phaseCounts: { planned: 0, active: 1, expired: 0 } };
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null, status = 200;
  if (url === '/api/policy' && method === 'GET') d = policy;
  else if (url === '/api/policy' && method === 'PUT') d = Object.assign({}, policy, { version: 5 });
  else if (url.indexOf('/api/policy/reps') === 0) d = [{ resourceId: 'rep-1', displayName: 'Okan Tekin' }, { resourceId: 'rep-2', displayName: 'Ayşe Demir' }];
  else if (url.indexOf('/api/policy/effective') === 0) d = { resourceId: 'rep-1', fields: [{ field: 'locationMode', value: 'required', source: 'tenant' }, { field: 'photoMode', value: 'optional', source: 'tenant' }, { field: 'accountWithoutLocation', value: 'allow_flagged', source: 'tenant' }], activeException: null, relaxedRules: [], scopeChain: { note: 'unresolved' } };
  else if (url.indexOf('/api/policy/history') === 0) d = { items: [], total: 0, page: 1, pageSize: 100 };
  else if (url === '/api/exceptions' && method === 'GET') d = list;
  else if (url === '/api/exceptions' && method === 'POST') { d = Object.assign({}, own, { id: 'x-new', status: 'draft', createdBy: ME, submittedBy: null, reviewRounds: [] }); status = 201; }
  else if (url === '/api/exceptions/x-new/submit') d = Object.assign({}, own, { id: 'x-new' });
  else if (url === '/api/exceptions/x-own') d = own;
  return Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [], hidden = [], toasts = [];
global.window = { document: global.document, showToast: m => toasts.push(m),
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) },
    Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) } } };
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceCore = require(dir + '/VisitEvidenceSettings/evidence-core.js');
global.window.VisitEvidenceSettingsL10n = texts;
const out = { mode };
try { require(dir + '/VisitEvidenceSettings/visit-evidence-settings.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const target = map => ({ closest: q => (map[q] ? Object.assign({ disabled: false, getAttribute: k => map[q][k] }, map[q].props || {}) : null) });

(async () => {
  await tick(); await tick();
  out.groups = el('ve-groups').innerHTML;
  out.nodeMeta = el('ve-node-meta').textContent;
  out.pvLines = el('ve-pv-lines').innerHTML;
  out.saveBarHidden = el('ve-save-bar').classList.contains('d-none');
  const badge = el('ve-tab-exceptions').querySelector('.wcn-tab-count');
  out.badge = badge ? badge.textContent : null;
  out.badgeClass = badge ? badge.className : null;
  out.pageApi = typeof (window.VisitEvidenceSettingsPage || {}).openNew + ',' + typeof (window.VisitEvidenceSettingsPage || {}).openException;

  // a change: location → info (a click on the segmented option)
  listeners.click({ target: target({ 'button[data-opt]': { 'data-field': 'locationMode', 'data-opt': 'info' } }) });
  out.unsavedShown = !el('ve-unsaved').classList.contains('d-none');
  out.saveEnabled = !el('ve-save').disabled;
  out.pvAfterChange = el('ve-pv-lines').innerHTML;
  if (el('ve-save').l) { el('ve-save').l.click(); }
  out.saveModalShown = shown.includes('ve-save-modal');
  out.saveText = el('ve-save-text').textContent;
  out.saveDiff = el('ve-save-diff').innerHTML;
  if (el('ve-save-confirm').l && out.saveModalShown) { el('ve-save-confirm').l.click(); await tick(); await tick(); }
  out.put = (calls.find(c => c.method === 'PUT') || {}).body || null;

  // the exceptions list's "New exception" (index.js's add-new action calls the page)
  window.VisitEvidenceSettingsPage.openNew(); await tick();
  out.newShown = shown.includes('ve-new-drawer');

  if (mode === 'manage') {
    out.rulesBeforeRep = el('ve-nx-rules').innerHTML;
    el('ve-nx-rep').l.change({ target: { value: 'rep-1' } }); await tick(); await tick();
    out.rulesWithRep = el('ve-nx-rules').innerHTML;
    el('ve-nx-rules').l.change({ target: { closest: q => (q === '[data-rule]' ? { getAttribute: () => 'location_info', checked: true } : null) } });
    el('ve-nx-why').l.input({ target: { value: 'Tablet GPS modülü arızalı' } });
    // 31 days (both ends included) → refused, nothing posted
    el('ve-nx-to').l.change({ target: { value: plus(30) } });
    out.dayCounter31 = el('ve-nx-days').textContent;
    const before = calls.length;
    el('ve-nx-submit').l.click(); await tick();
    out.tooLongError = el('ve-nx-err-to').textContent;
    out.postsAfter31 = calls.slice(before).filter(c => c.method === 'POST').length;
    // 30 days → created, then sent for approval
    el('ve-nx-to').l.change({ target: { value: plus(29) } });
    out.dayCounter30 = el('ve-nx-days').textContent;
    const b2 = calls.length;
    el('ve-nx-submit').l.click(); await tick(); await tick(); await tick();
    out.submitCalls = calls.slice(b2).filter(c => c.method === 'POST').map(c => [c.url, c.body]);
    out.submittedToast = toasts.slice();
    out.detailShown = shown.includes('offcanvasDetailsPreview');

    // the caller's own exception in review (the list's quick view): the SoD hint + a disabled Approve
    window.VisitEvidenceSettingsPage.openException('x-own', { row: own }); await tick(); await tick();
    out.ownBody = el('oc-details-list').innerHTML;
    out.ownFoot = el('oc-actions').innerHTML;
    out.ownSubtitle = el('oc-subtitle').innerHTML;
  } else {
    const putBefore = calls.filter(c => c.method === 'PUT').length;
    out.readonlyPuts = putBefore;
  }
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
