/* WP-VW-W3E (W3E-WEB-a) - DOM smoke of the institution QR at the visit start (S1–S8) in Node, on the REAL
   visit-workspace.js / workspace-core.js / format.js: a fake document, a stub fetch (contract, calendar, qr-check, start),
   a stub camera + BarcodeDetector. argv[2] = the CRM script folder, argv[3] = the Turkish script texts (the real resx),
   argv[4] = the scenario: required | requiredNoCode | optional | off | denied. Prints one JSON object. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const scenario = process.argv[4] || 'required';
const nodes = {};
const listeners = { doc: {}, root: {} };
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '', readyState: 4, videoWidth: 640, videoHeight: 480,
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; }, removeAttribute(k) { delete this.attrs[k]; },
  play() { return Promise.resolve(); }, addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; }, click() { this.clicked = (this.clicked || 0) + 1; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vw-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-can-manage': 'true', 'data-can-record': 'true', 'data-can-apply': 'true', 'data-can-search-contacts': 'true',
  'data-can-execute': 'true', 'data-presentation-url': '/CRM/VisitPresentation' });
root.addEventListener = (ev, fn) => { listeners.root[ev] = fn; };
el('vw-e3').attrs['data-jsqr'] = '/assets/vendor/libs/jsqr/jsQR.js';
el('vw-e3-qr').classList.add('d-none');
el('vw-e3-device').classList.add('d-none');
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { listeners.doc[ev] = fn; },
  head: { appendChild: () => {} }, createElement: () => mk('x') };
const today = new Date().toISOString().slice(0, 10);
const C0 = require(dir + '/VisitWorkspace/workspace-core.js');
const monday = C0.mondayOf(today);
const visits = [{ plannedVisitId: 'v1', plannedDate: today, weekStart: monday, startTime: '10:30', endTime: '10:50', targetType: 'contact', targetId: 't1', contactId: 't1', accountId: 'a1',
  targetDisplayName: 'Dr. Hakan Öztürk', accountDisplayName: 'Göğüs Hastalıkları Polikliniği', workStatus: 'today', plannedContent: [] }];
const data = { visits, weeks: [{ weekStart: monday, weekNumber: 41, state: 'approved', planningSessionId: null, canApprove: false, canReopen: false, capacityMinutes: 2400, plannedMinutes: 600, visitCount: 1, unplacedCount: 0 }],
  days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(monday, i), kind: 'working', isHoliday: false, capacityMinutes: 480, plannedMinutes: 60, freeMinutes: 420 })) };
const qrMode = scenario === 'off' ? 'off' : scenario === 'optional' ? 'optional' : 'required';
const policy = { locationMode: 'info', photoMode: 'optional', radiusMeters: 300, maxAccuracyMeters: 150, qrMode, antiFraudMode: scenario === 'off' ? 'off' : 'flag' };
const first = scenario === 'requiredNoCode'
  ? { qrMode, status: 'not_issued', wouldRefuse: false, errorCode: null, flag: 'qr_not_issued', expectedHasActiveCode: false }
  : { qrMode, status: 'missing', wouldRefuse: qrMode === 'required', errorCode: qrMode === 'required' ? 'visit_qr_required' : null, expectedHasActiveCode: true };
const scans = [
  { qrMode, status: 'wrong_account', wouldRefuse: true, errorCode: 'visit_qr_wrong_account', accountId: 'x', accountName: 'Ankara Şehir Hastanesi', expectedHasActiveCode: true },
  { qrMode, status: 'ok', wouldRefuse: false, accountId: 'p', accountName: 'Acıbadem Altunizade Hastanesi', matchedVia: 'parent', expectedHasActiveCode: true }];
const calls = [];
global.fetch = (url, o) => {
  calls.push({ url, method: (o && o.method) || 'GET', body: o && o.body });
  let d = null;
  if (url.includes('/execution/start')) return Promise.resolve({ ok: true, status: 201, text: () => Promise.resolve(JSON.stringify({ data: { id: 'e1' } })) });
  if (url.includes('/execution/qr-check')) {
    const body = JSON.parse(o.body);
    d = body.qrPayload ? scans.shift() : first;
  } else if (url.includes('/execution/contract')) d = { moduleId: 'MOD-0155-VX', policy, maxEvidenceBytes: 10485760 };
  else if (url.includes('/calendar')) d = data; else if (url.includes('/contract')) d = { maxNoteLength: 500 };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
class FakeFormData { constructor() { this.fields = []; } append(name, value, file) { this.fields.push([name, value, file]); } }
const geo = { getCurrentPosition: (ok) => setTimeout(() => ok({ coords: { latitude: 41.0123, longitude: 28.9876, accuracy: 15 } }), 0) };
const stream = { stopped: 0, getTracks() { return [{ stop: () => { stream.stopped++; } }]; } };
const constraints = [];
const shown = [];
const assigned = [];
const cal = { data: null };
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {}, showToast: () => {},
  FormData: FakeFormData, URL: { createObjectURL: () => 'blob:x', revokeObjectURL: () => {} },
  navigator: { geolocation: geo, mediaDevices: { getUserMedia: c => { constraints.push(c); return scenario === 'denied' ? Promise.reject({ name: 'NotAllowedError' }) : Promise.resolve(stream); } } },
  BarcodeDetector: class { static getSupportedFormats() { return Promise.resolve(['qr_code']); } detect() { return Promise.resolve([{ rawValue: 'DQR1.read-as-is' }]); } },
  setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: t => clearTimeout(t),
  location: { assign: u => assigned.push(u) },
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) }, Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) } },
  DitenCalendar: { create: (host, opts) => { cal.opts = opts; return { calendar: { setOption: () => {}, gotoDate: () => {} }, setData: (ev) => { cal.data = ev; }, date: () => monday }; } } };
global.window.VisitWorkspaceCore = C0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = texts;
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = () => new Promise(r => setTimeout(r, 30));
const click = (sel, attrs) => listeners.doc.click({ target: { closest: q => (q === sel ? { getAttribute: k => attrs[k] } : null) } });
const visible = id => !el(id).classList.contains('d-none');
const qr = action => el('vw-e3-qr').l.click({ preventDefault() {}, target: { closest: q => (q === '[data-qr]' ? { getAttribute: () => action } : null) } });
const snap = () => ({ box: el('vw-e3-qr-box').innerHTML, hint: el('vw-e3-hint').textContent, beginDisabled: el('vw-e3-begin').disabled,
  cam: visible('vw-e3-qr-cam'), section: visible('vw-e3-qr'), device: visible('vw-e3-device') });
(async () => {
  await tick(); await tick();
  const out = { scenario };
  cal.opts.onEventClick('v1'); await tick();
  click('[data-start]', {}); await tick(); await tick(); await tick();
  out.need = el('vw-e3-qr-need').textContent;
  out.open = snap();
  if (scenario === 'off') { out.calls = calls.map(c => c.method + ' ' + c.url); }
  if (scenario === 'required' || scenario === 'optional') {
    qr('nocode'); await tick();
    out.nocode = snap();
    qr(scenario === 'optional' ? 'skip' : 'back'); await tick();
    out.after = snap();
    qr('scan'); await tick(); await tick(); await tick(); await tick();
    out.wrong = snap();
    qr('scan'); await tick(); await tick(); await tick(); await tick();
    out.ok = snap();
  }

  if (scenario === 'denied') { qr('scan'); await tick(); await tick(); out.denied = snap(); }
  out.constraints = constraints;
  out.streamStopped = stream.stopped;
  const before = calls.length;
  el('vw-e3-begin').l.click(); await tick(); await tick();
  const start = calls.slice(before).find(c => c.url.includes('/execution/start'));
  out.startFields = start ? start.body.fields.map(f => [f[0], typeof f[1] === 'object' ? 'blob' : f[1]]) : null;
  out.qrChecks = calls.filter(c => c.url.includes('/execution/qr-check')).map(c => JSON.parse(c.body));
  out.assigned = assigned.slice();
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
