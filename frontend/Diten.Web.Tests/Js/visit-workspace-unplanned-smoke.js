/* WP-VW-W4 (W4-FIX-c) - DOM smoke of the Visit Workspace UNPLANNED visit in Node: a fake document, a stub fetch and a stub
   DitenCalendar (the visit-workspace-smoke.js set-up). Run by VisitWorkspaceWebTests with the CRM script folder as argv[2] and
   'true' | 'false' (crm.account.read) as argv[3]; prints one JSON object.
   G-4: a doctor with a time → the body carries plannedStartTime + plannedEndTime (start + 20 min), the CRM answers 201.
   G-3: the target type "Pharmacy" → the pharmacy search (accountType=pharmacy proxy) → a pharmacy target, no time → both
   times null, 201; the same pharmacy again today → 409 planned_visit_duplicate_same_day_type → its text, never a code.
   Without crm.account.read the type choice is hidden and the doctor flow is unchanged. */
const dir = process.argv[2];
const pharmacies = process.argv[3] !== 'false';
const nodes = {};
const listeners = { doc: {}, root: {} };
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, checked: false, dataset: {}, style: {}, attrs: {},
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vw-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-can-manage': 'true', 'data-can-record': 'true', 'data-can-apply': 'true',
  'data-can-search-contacts': 'true', 'data-can-search-pharmacies': String(pharmacies) });
root.addEventListener = (ev, fn) => { listeners.root[ev] = fn; };
el('vw-unp-type-box').classList.add('d-none'); // as the view renders it
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { listeners.doc[ev] = fn; }, createElement: () => mk('x') };
const today = new Date().toISOString().slice(0, 10);
const C0 = require(dir + '/VisitWorkspace/workspace-core.js');
const monday = C0.mondayOf(today);
const data = { visits: [], weeks: [{ weekStart: monday, weekNumber: 41, state: 'draft', planningSessionId: 's1', canApprove: false, canReopen: false, capacityMinutes: 2400, plannedMinutes: 0, visitCount: 0, unplacedCount: 0 }],
  days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(monday, i), kind: 'working', isHoliday: false, capacityMinutes: 480, plannedMinutes: 0, freeMinutes: 480 })) };
const calls = [];
let creates = 0;
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null, status = 200, errors;
  if (url.includes('/calendar')) d = data;
  else if (url.startsWith('/api/contacts/search')) d = [{ id: 'C1', displayName: 'Dr. Ayşe Kaya', contactType: 'doctor', status: 'active' }];
  else if (url.startsWith('/api/pharmacies/search')) d = { items: [{ id: 'A7', accountName: 'Hayat Eczanesi', accountCode: 'ECZ-7', accountType: 'pharmacy', territoryNodeName: 'Kadıköy' }], totalCount: 1 };
  else if (url === '/api/planned-visits/unplanned' && method === 'POST') {
    creates++;
    if (creates === 3) { status = 409; errors = ['A visit of this type already exists for this target on this day.', 'planned_visit_duplicate_same_day_type']; }
    else { status = 201; d = 'pv-' + creates; }
  }
  const payload = errors ? { isSuccessful: false, errors } : { data: d };
  return Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify(payload)) });
};
const toasts = [];
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {}, showToast: m => toasts.push(m),
  DitenCalendar: { create: () => ({ calendar: { setOption: () => {}, gotoDate: () => {} }, setData: () => {}, date: () => monday }) } };
global.window.VisitWorkspaceCore = C0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = {
  WeekLabel: '{0}. hafta', Saved: 'Kaydedildi.', NoMatches: 'Eşleşme yok', ActionFailed: 'İşlem başarısız',
  UnpDoctorLabel: 'Doktor', UnpDoctorSearch: 'Doktoru ada göre arayın (en az 2 harf)', UnpPharmacyLabel: 'Eczane', UnpPharmacySearch: 'Eczaneyi ada göre arayın (en az 2 harf)',
  Err_planned_visit_duplicate_same_day_type: 'Bu hedefe bugün aynı türde bir ziyaret zaten var.', Err_planned_visit_time_window_invalid: 'Ziyaret saati geçersiz; başka bir başlangıç saati seçin.'
};
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = (ms) => new Promise(r => setTimeout(r, ms || 20));
const pick = attr => listeners.doc.click({ target: { closest: q => (q.indexOf('[' + attr + ']') > -1 ? { getAttribute: k => (k === attr ? (attr === 'data-contact' ? 'C1' : 'A7') : null) } : null) } });
const creates_ = () => calls.filter(c => c.url === '/api/planned-visits/unplanned');
(async () => {
  await tick(); await tick();
  const out = { pharmacies };
  el('vw-unplanned-open').l.click();
  out.typeBoxHidden = el('vw-unp-type-box').classList.contains('d-none');
  out.searchLabel = el('vw-unp-search-label').textContent;
  // G-4 — a doctor at 10:30
  el('vw-unp-search').value = 'Ay'; el('vw-unp-search').l.input(); await tick(300);
  out.doctorResults = el('vw-unp-results').innerHTML;
  out.saveDisabledBeforePick = el('vw-unp-save').disabled;
  pick('data-contact');
  el('vw-unp-time').value = '10:30';
  el('vw-unp-save').l.click(); await tick(); await tick();
  out.doctorBody = (creates_()[0] || {}).body || null;
  out.toasts = toasts.slice();
  if (pharmacies) {
    // G-3 — a pharmacy, no time
    el('vw-unplanned-open').l.click();
    listeners.doc.change({ target: { name: 'vw-unp-type', value: 'pharmacy' } });
    out.pharmacyLabel = el('vw-unp-search-label').textContent;
    out.pharmacyPlaceholder = el('vw-unp-search').getAttribute('placeholder');
    el('vw-unp-search').value = 'Hayat'; el('vw-unp-search').l.input(); await tick(300);
    out.pharmacySearchUrl = (calls.filter(c => c.url.startsWith('/api/pharmacies/search')).pop() || {}).url || null;
    out.pharmacyResults = el('vw-unp-results').innerHTML;
    pick('data-pharmacy');
    el('vw-unp-save').l.click(); await tick(); await tick();
    out.pharmacyBody = (creates_()[1] || {}).body || null;
    // the same pharmacy again today → 409 duplicate → its text
    el('vw-unp-save').l.click(); await tick(); await tick();
    out.duplicateError = el('vw-unp-error').textContent;
    out.duplicateShown = !el('vw-unp-error').classList.contains('d-none');
  }
  out.creates = creates_().length;
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
