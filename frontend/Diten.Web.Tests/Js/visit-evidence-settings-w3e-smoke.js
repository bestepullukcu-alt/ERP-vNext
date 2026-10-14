/* WP-VW-W3E (W3E-WEB-b) - DOM smoke of visit-evidence-settings.js for the W3E screens E1-E3 in Node (the W3-WEB-d
   visit-evidence-settings-smoke.js set-up). Run by VisitEvidenceFlagsWebTests with the CRM script folder as argv[2], a JSON
   file of the Turkish script texts (the real resx) as argv[3] and the mode as argv[4] ('qr' = with crm.visit-qr.read |
   'noqr'); prints one JSON object.
   E1: the "additional evidence" card (QR: off / optional / required cards; device: off / flag + a DISABLED "block · soon");
       a click on "optional" changes the draft, "block" can never be taken (neither its disabled button nor a forged
       click), the PUT carries qrMode + antiFraudMode and never "block"; the manage link only with a QR key.
   E2: the exception window offers qr_optional ("new" badge + description) only when the rep's QR is required.
   E3: the effective rules show the two new rows ("new"), the reps pane the four columns with "· exception", the history
       names the fields and values in the user's language (never "qrMode" / "antiFraudMode"). */
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
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('ve-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-me': ME, 'data-can-manage-policy': 'true', 'data-can-read-exceptions': 'true',
  'data-can-manage-exceptions': 'true', 'data-can-read-qr-codes': mode === 'qr' ? 'true' : 'false', 'data-qr-codes-url': '/CRM/VisitQrCodes' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
el('ve-save-bar').classList.add('d-none');
['ve-pane-reps', 've-pane-history'].forEach(id => el(id).classList.add('d-none'));
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };

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
  { at: '2026-09-28T16:40:00Z', by: ME, field: 'shortVisitSeconds', oldValue: '90', newValue: '120', kind: 'set' }], total: 3, page: 1, pageSize: 100, displayNames: { [ME]: 'Deniz Er' } };
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null;
  if (url === '/api/policy' && method === 'GET') d = policy;
  else if (url === '/api/policy' && method === 'PUT') d = Object.assign({}, policy, { version: 8 });
  else if (url.indexOf('/api/policy/reps') === 0) d = [{ resourceId: 'rep-1', displayName: 'Sema Kara' }, { resourceId: 'rep-2', displayName: 'Murat Demir' }, { resourceId: 'rep-3', displayName: 'Elif Şahin' }];
  else if (url.indexOf('/api/policy/effective') === 0) d = url.indexOf('rep-3') > -1 ? effective : url.indexOf('rep-2') > -1 ? effectiveReq : effectiveOff;
  else if (url.indexOf('/api/policy/history') === 0) d = history;
  else if (url === '/api/exceptions' && method === 'GET') d = list;
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [];
global.window = { document: global.document, location: { search: '' }, showToast: () => {},
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) }, Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) } } };
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceCore = require(dir + '/VisitEvidenceSettings/evidence-core.js');
global.window.VisitEvidenceSettingsL10n = texts;
const out = { mode };
try { require(dir + '/VisitEvidenceSettings/visit-evidence-settings.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
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
  // E3 — the reps pane
  listeners.click({ target: target({ '[data-pane]': { 'data-pane': 'reps' } }) }); await tick();
  out.repsShown = !el('ve-pane-reps').classList.contains('d-none');
  out.reps = el('ve-rep-rows').innerHTML;
  // E3 — the history pane
  listeners.click({ target: target({ '[data-pane]': { 'data-pane': 'history' } }) }); await tick(); await tick();
  out.history = el('ve-history-rows').innerHTML;

  // E2 — the exception window: qr_optional offered for a rep whose QR is required, not for one whose QR is off
  el('ve-new-exception').l.click(); await tick();
  el('ve-nx-rep').l.change({ target: { value: 'rep-2' } }); await tick(); await tick();
  out.rulesRequired = el('ve-nx-rules').innerHTML;
  el('ve-nx-rep').l.change({ target: { value: 'rep-1' } }); await tick(); await tick();
  out.rulesOff = el('ve-nx-rules').innerHTML;
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
