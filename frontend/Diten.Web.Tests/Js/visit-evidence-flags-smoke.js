/* WP-VW-W3E (W3E-WEB-b) - DOM smoke of visit-evidence-flags.js in Node: a fake document, a stub fetch, stub bootstrap.
   Run by VisitEvidenceFlagsWebTests with the CRM script folder as argv[2], a JSON file of the Turkish script texts (the
   real resx) as argv[3] and the mode as argv[4]; prints one JSON object.
   team  (crm.visit-evidence-flags.read, no photo keys): M1 the summary (started, flagged + rate, top-6 bars + "Other"),
         the first list call (last 7 days, only flagged, page 1), the chips with their values + "QR not scanned" (never a
         flag filter option, never sent), the exception link, paging; a flag type → flags=; a custom range > 92 days → the
         error and NO call; M3 a rep row → resourceId=; M2 the drawer (location, QR, device, duration + slides, "photo
         present · not allowed", the exception, "open the visit").
   all   (.read-all + the photo keys): "all reps", the drawer's photo thumbnail from the existing evidence path.
   empty (no flagged visit): M4. */
const fs = require('fs');
// CT-3 (W3E-FIX) — THE CLOCK IS PINNED: "today" is 2026-10-14 (12:00 UTC) on every run, so the first call's period
// (the last 7 days = 08–14 Oct) and every date the page derives from it are the same tomorrow as today. Without the pin
// C5 went red the day after it was written. Only the no-argument `new Date()` and `Date.now()` move; a given date is as it is.
const PINNED_NOW = Date.UTC(2026, 9, 14, 12, 0, 0);
const RealDate = Date;
class PinnedDate extends RealDate {
  constructor(...args) { if (args.length === 0) { super(PINNED_NOW); } else { super(...args); } }
  static now() { return PINNED_NOW; }
}
global.Date = PinnedDate;
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'team';
const nodes = {};
const listeners = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', checked: false, disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vf-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-can-read-all': mode === 'all' ? 'true' : 'false', 'data-can-see-photos': mode === 'all' ? 'true' : 'false',
  'data-visit-url': '/CRM/VisitReport', 'data-exception-url': '/CRM/VisitEvidenceSettings' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
['vf-custom', 'vf-empty', 'vf-pane-reps', 'vf-range-error', 'vf-error', 'vf-flag-menu'].forEach(id => el(id).classList.add('d-none'));
el('vf-only').checked = true;
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };

const ACC = 'acc-0000-0000-0000-000000000001', EXC = 'exc-0000-0000-0000-000000000001';
const row = (id, extra) => Object.assign({
  executionId: id, plannedVisitId: 'pv-' + id, startedAt: '2026-10-14T07:42:00Z', resourceId: 'rep-1', repName: 'Sema Kara',
  target: { type: 'contact', targetId: 'c1', name: 'Dr. Hakan Öztürk', accountId: ACC, accountName: 'Kardiyoloji Polikliniği' }, kind: 'in_person',
  flags: [], location: { distanceMeters: 95, accuracyMeters: 12, radiusMeters: 300, mode: 'required', status: 'ok' },
  device: { platform: 'web', appVersion: 'Safari 18 · iPadOS', mockLocation: null, integrityProvider: null, integrityVerdict: null, tokenProvided: false, verified: false },
  durationSeconds: 1080, exceptionId: null, hasPhoto: true,
  qr: { status: 'ok', accountId: ACC, accountName: 'Kardiyoloji Polikliniği', version: 3, scannedAt: '2026-10-14T07:41:00Z', matchedVia: 'self' } }, extra || {});
const rows = mode === 'empty' ? [] : [
  row('e1', { flags: ['device_unverified'] }),
  row('e2', { repName: 'Murat Demir', resourceId: 'rep-2', flags: ['location_far'], location: { distanceMeters: 820, accuracyMeters: 18, radiusMeters: 300, mode: 'info', status: 'far' } }),
  row('e4', { flags: ['short_visit', 'qr_not_issued'], durationSeconds: 100, qr: { status: 'not_issued', accountId: null, accountName: null, version: null, scannedAt: null, matchedVia: null },
    target: { type: 'pharmacy', targetId: 'ph1', name: 'Eczane Yıldız (Kadıköy)', accountId: 'acc-2', accountName: 'Eczane Yıldız (Kadıköy)' } }),
  row('e7', { flags: ['started_with_exception'], exceptionId: EXC, qr: { status: 'missing', accountId: null, accountName: null, version: null, scannedAt: null, matchedVia: null } }),
  // W3E-FIX — G-8: a far start (198 290 m → "198,3 km"); G-7: two institutions with the SAME name in different cities
  row('e8', { flags: ['location_far'], location: { distanceMeters: 198290, accuracyMeters: 15, radiusMeters: 300, mode: 'info', status: 'far' },
    target: { type: 'pharmacy', targetId: 'ph3', name: '100.YIL ECZANESİ', accountId: 'acc-3', accountName: '100.YIL ECZANESİ' } }),
  row('e9', { flags: ['device_unverified'], target: { type: 'pharmacy', targetId: 'ph4', name: '100.YIL ECZANESİ', accountId: 'acc-4', accountName: '100.YIL ECZANESİ' } })
];
const summary = { from: '2026-10-08', to: '2026-10-14', started: mode === 'empty' ? 12 : 412, flagged: mode === 'empty' ? 0 : 37, flaggedRate: 0.09,
  byFlag: mode === 'empty' ? {} : { device_unverified: 12, location_far: 7, short_visit: 5, qr_not_issued: 4, started_with_exception: 3, location_inaccurate: 3, qr_revoked: 2, mock_location: 2, location_missing: 1, qr_wrong_account: 1 },
  reps: [{ resourceId: 'rep-1', repName: 'Sema Kara', started: 148, flagged: 14, topFlag: 'device_unverified' }, { resourceId: 'rep-2', repName: 'Murat Demir', started: 136, flagged: 15, topFlag: 'location_far' }] };
const detail = { row: rows[3] || row('e7'), exception: { id: EXC, rules: ['qr_optional'], startDate: '2026-10-12', endDate: '2026-10-16', approvedBy: 'u-9', approvedAt: '2026-10-11T08:00:00Z' },
  slideSeconds: 710, slideCount: 8, photoCapturedAt: '2026-10-14T07:41:00Z' };
const calls = [];
global.fetch = (url) => {
  calls.push(url);
  let d = null;
  const path = url.split('?')[0];
  if (path === '/api/flags') d = { items: rows, total: mode === 'empty' ? 0 : 37, page: Number((/page=(\d+)/.exec(url) || [])[1] || 1), pageSize: 10, from: '2026-10-08', to: '2026-10-14' };
  else if (path === '/api/summary') d = summary;
  else if (path.indexOf('/api/flags/') === 0) d = detail;
  else if (path === '/api/account-labels') d = [{ accountId: 'acc-3', code: 'ECZ-0042', city: 'Konya' }, { accountId: 'acc-4', code: 'ECZ-0107', city: 'İstanbul' }];
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [];
global.window = { document: global.document, bootstrap: { Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) } } };
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceFlagsCore = require(dir + '/VisitEvidenceFlags/flags-core.js');
global.window.VisitEvidenceFlagsL10n = texts;
const out = { mode };
try { require(dir + '/VisitEvidenceFlags/visit-evidence-flags.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const target = map => ({ closest: q => (map[q] ? Object.assign({ getAttribute: k => map[q][k] }, map[q].props || {}) : null) });

(async () => {
  await tick(); await tick(); await tick();
  out.today = new Date().toISOString().slice(0, 10);
  out.firstCalls = calls.slice();
  out.started = el('vf-sum-started').textContent;
  out.flagged = el('vf-sum-flagged').textContent;
  out.rate = el('vf-sum-rate').textContent;
  out.range = el('vf-sum-range').textContent;
  out.bars = el('vf-bars').innerHTML;
  out.period = el('vf-period').innerHTML;
  out.rep = el('vf-rep').innerHTML;
  out.flagText = el('vf-flag-text').textContent;
  out.flagMenu = el('vf-flag-menu').innerHTML;
  out.account = el('vf-account').innerHTML;
  out.rows = el('vf-rows').innerHTML;
  out.pageText = el('vf-page-text').textContent;
  out.pages = el('vf-pages').innerHTML;
  out.emptyShown = !el('vf-empty').classList.contains('d-none');
  out.emptyTitle = el('vf-empty-title').textContent;
  out.reps = el('vf-rep-rows').innerHTML;
  if (mode !== 'empty') {
    // a flag type → flags=location_far
    let b = calls.length;
    listeners.change({ target: { closest: q => (q === '[data-flagtype]' ? { getAttribute: () => 'location_far', checked: true } : null) } }); await tick(); await tick();
    out.flagCall = calls.slice(b).find(u => u.startsWith('/api/flags?')) || null;
    out.flagTextAfter = el('vf-flag-text').textContent;
    // a custom range of 120 days → the error, no call
    b = calls.length;
    el('vf-period').l.change({ target: { value: 'custom' } }); await tick();
    el('vf-from').l.change({ target: { value: '2026-06-01' } }); await tick();
    el('vf-to').l.change({ target: { value: '2026-09-28' } }); await tick();
    out.rangeError = el('vf-range-error').textContent;
    out.rangeErrorShown = !el('vf-range-error').classList.contains('d-none');
    out.callsAfterLongRange = calls.slice(b).filter(u => u.indexOf('2026-09-28') > -1).length;
    el('vf-period').l.change({ target: { value: 'last7' } }); await tick(); await tick();
    // M3 — a rep row filters the list
    b = calls.length;
    listeners.click({ target: target({ '[data-rep]': { 'data-rep': 'rep-2' } }) }); await tick(); await tick();
    out.repCall = calls.slice(b).find(u => u.startsWith('/api/flags?')) || null;
    // paging
    b = calls.length;
    listeners.click({ target: target({ '[data-page]': { 'data-page': '2' } }) }); await tick(); await tick();
    out.pageCall = calls.slice(b).find(u => u.startsWith('/api/flags?')) || null;
    // M2 — the drawer
    listeners.click({ target: target({ '[data-open]': { 'data-open': 'e7' } }) }); await tick(); await tick();
    out.drawerShown = shown.includes('vf-drawer');
    out.drawerSub = el('vf-drawer-sub').innerHTML;
    out.drawer = el('vf-drawer-body').innerHTML;
    out.openVisit = el('vf-open-visit').getAttribute('href');
  }
  out.allCalls = calls.slice();
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
