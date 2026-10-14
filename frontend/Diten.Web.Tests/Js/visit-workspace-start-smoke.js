/* WP-VW-W3 (WEB-a) - DOM smoke of the visit start (E3) in Node: a fake document, a stub fetch, a stub DitenCalendar, a
   stub geolocation / canvas / Image / FormData / location. Run by VisitWorkspaceStartWebTests with the CRM script folder
   as argv[2] and a JSON file of the Turkish script texts (the real resx) as argv[3]; prints one JSON object.
   Flow: the in_progress card; the detail panel's Start → E3 opens; the location refused → its text + Start disabled;
   a photo file → a JPEG (longest edge 1600, quality 0.85); the location again → Start enabled; a too-far refusal →
   its text; success → the presentation page. Then the in_progress visit's Back to presentation / Finish. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const nodes = {};
const listeners = { doc: {}, root: {} };
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; }, removeAttribute(k) { delete this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; }, click() { this.clicked = (this.clicked || 0) + 1; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vw-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-can-manage': 'true', 'data-can-record': 'true', 'data-can-apply': 'true', 'data-can-search-contacts': 'true',
  'data-can-execute': 'true', 'data-presentation-url': '/CRM/VisitPresentation' });
root.addEventListener = (ev, fn) => { listeners.root[ev] = fn; };
const jpegs = [];
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { listeners.doc[ev] = fn; },
  createElement: tag => {
    if (tag !== 'canvas') { return mk('x'); }
    const c = { width: 0, height: 0, getContext: () => ({ drawImage() {} }),
      toBlob: (cb, type, quality) => { const b = { size: 2048, type, quality, width: c.width, height: c.height }; jpegs.push(b); setTimeout(() => cb(b), 0); } };
    return c;
  } };
const today = new Date().toISOString().slice(0, 10);
const C0 = require(dir + '/VisitWorkspace/workspace-core.js');
const monday = C0.mondayOf(today);
const startedAt = new Date(Date.now() - 7 * 60000 - 5000).toISOString();
const visits = [
  { plannedVisitId: 'v1', plannedDate: today, weekStart: monday, startTime: '11:30', endTime: '11:55', targetType: 'contact', targetId: 't1', contactId: 't1', accountId: 'a1',
    targetDisplayName: 'Dr. Kerem Aslan', accountDisplayName: 'Acıbadem Kozyatağı', workStatus: 'today', plannedContent: [{ productId: 'p1', productName: 'Tutukon' }] },
  { plannedVisitId: 'v2', plannedDate: today, weekStart: monday, startTime: '10:30', endTime: '10:55', targetType: 'contact', targetId: 't2', contactId: 't2', accountId: 'a1',
    targetDisplayName: 'Dr. Selin Kaya', accountDisplayName: 'Acıbadem Kozyatağı', workStatus: 'in_progress', executionId: 'e2', executionStatus: 'started', startedAt: startedAt,
    plannedContent: [{ productId: 'p1', productName: 'Tutukon' }] }];
const data = { visits, weeks: [{ weekStart: monday, weekNumber: 41, state: 'approved', planningSessionId: null, canApprove: false, canReopen: false, capacityMinutes: 2400, plannedMinutes: 600, visitCount: 2, unplacedCount: 0 }],
  days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(monday, i), kind: 'working', isHoliday: false, capacityMinutes: 480, plannedMinutes: 60, freeMinutes: 420 })) };
const calls = [];
let startReply = { status: 409, body: { errors: ['You are 3400 m from the institution (at most 300 m).', 'visit_location_too_far', 'distance_meters=3400'] } };
global.fetch = (url, o) => {
  calls.push({ url, method: (o && o.method) || 'GET', body: o && o.body, headers: o && o.headers });
  let d = null;
  if (url.includes('/execution/start')) { const r = startReply; return Promise.resolve({ ok: r.status < 300, status: r.status, text: () => Promise.resolve(JSON.stringify(r.body)) }); }
  if (url.includes('/execution/contract')) d = { moduleId: 'MOD-0155-VX', policy: { locationMode: 'required', photoMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150 }, maxEvidenceBytes: 10485760 };
  else if (url.includes('/calendar')) d = data; else if (url.includes('/contract')) d = { maxNoteLength: 500 };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
class FakeFormData { constructor() { this.fields = []; } append(name, value, file) { this.fields.push([name, value, file]); } }
class FakeImage { set src(v) { this._src = v; this.naturalWidth = 4000; this.naturalHeight = 3000; setTimeout(() => this.onload && this.onload(), 0); } }
let geoMode = 'denied';
const geo = { getCurrentPosition: (ok, fail) => setTimeout(() => (geoMode === 'ok'
  ? ok({ coords: { latitude: 41.0123, longitude: 28.9876, accuracy: 15.4 } })
  : fail({ code: geoMode === 'denied' ? 1 : geoMode === 'timeout' ? 3 : 2 })), 0) };
const shown = [];
const assigned = [];
const cal = { data: null };
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {}, showToast: () => {},
  FormData: FakeFormData, Image: FakeImage, URL: { createObjectURL: () => 'blob:x', revokeObjectURL: () => {} },
  navigator: { geolocation: geo, mediaDevices: { getUserMedia: () => Promise.reject({ name: 'NotAllowedError' }) } },
  location: { assign: u => assigned.push(u) },
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) }, Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() {} }) } },
  DitenCalendar: { create: (host, opts) => { cal.opts = opts;
    try { (opts.events || []).forEach(e => opts.renderExtras(e)); cal.createError = null; } catch (e) { cal.createError = String(e); }
    return { calendar: { setOption: (k, v) => { cal[k] = v; }, gotoDate: () => {} }, setData: (ev, days) => { cal.data = ev; cal.days = days; }, date: () => monday }; } } };
global.window.VisitWorkspaceCore = C0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = texts;
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = () => new Promise(r => setTimeout(r, 20));
const click = (sel, attrs) => listeners.doc.click({ target: { closest: q => (q === sel ? { getAttribute: k => attrs[k] } : null) } });
const visible = id => !el(id).classList.contains('d-none');
(async () => {
  await tick(); await tick();
  const out = {};
  out.createError = cal.createError === undefined ? 'not-called' : cal.createError;
  out.liveCard = cal.opts.renderExtras({ id: 'v2' });

  // the today visit: Start → E3
  cal.opts.onEventClick('v1'); await tick();
  out.todayActions = el('vw-detail-actions').innerHTML;
  out.todayBand = el('vw-detail-band').innerHTML;
  click('[data-start]', {}); await tick(); await tick();
  out.e3Shown = shown.includes('vw-e3');
  out.target = el('vw-e3-target').innerHTML;
  out.photoNeed = el('vw-e3-photo-need').textContent;
  // the location refused (geolocation error code 1)
  out.deniedChip = el('vw-e3-loc-chip').textContent;
  out.deniedText = el('vw-e3-loc-text').textContent;
  out.deniedRetry = visible('vw-e3-loc-retry');
  out.deniedHint = el('vw-e3-hint').textContent;
  out.deniedBeginDisabled = el('vw-e3-begin').disabled;
  // the webcam refused: its text, the upload stays
  el('vw-e3-webcam').l.click(); await tick();
  out.camDeniedText = el('vw-e3-photo-error').innerHTML;
  // a photo FILE → a JPEG at most 1600 px, quality 0.85
  el('vw-e3-file').l.change({ target: { files: [{ type: 'image/png', name: 'giris.png' }] } }); await tick(); await tick();
  out.jpeg = jpegs[jpegs.length - 1] || null;
  out.photoTaken = visible('vw-e3-taken');
  out.photoHint = el('vw-e3-hint').textContent;
  out.photoBeginDisabled = el('vw-e3-begin').disabled;
  // the location again (now granted)
  geoMode = 'ok';
  el('vw-e3-loc-retry').l.click(); await tick(); await tick();
  out.okChip = el('vw-e3-loc-chip').textContent;
  out.okText = el('vw-e3-loc-text').textContent;
  out.readyHint = el('vw-e3-hint').textContent;
  out.readyBeginEnabled = !el('vw-e3-begin').disabled;
  // Start → 409 too far (its text, the hint), nothing opened
  const before = calls.length;
  el('vw-e3-begin').l.click(); await tick(); await tick();
  const startCall = calls.slice(before).find(c => c.url.includes('/execution/start'));
  out.startUrl = startCall ? startCall.method + ' ' + startCall.url : null;
  out.startContentType = startCall && startCall.headers ? startCall.headers['Content-Type'] || null : null;
  out.startFields = startCall ? startCall.body.fields.map(f => [f[0], typeof f[1] === 'object' ? 'blob:' + f[1].type + ':' + f[1].width + 'x' + f[1].height : f[1], f[2] || null]) : null;
  out.farError = el('vw-e3-error').innerHTML;
  out.farErrorShown = visible('vw-e3-error');
  out.assignedAfterFar = assigned.length;
  // Start → success → the presentation page
  startReply = { status: 200, body: { data: { id: 'e1', plannedVisitId: 'v1', status: 'started' } } };
  el('vw-e3-begin').l.click(); await tick(); await tick();
  out.assigned = assigned.slice();

  // the in_progress visit: back to the presentation / finish, the band "N min · started at"
  cal.opts.onEventClick('v2'); await tick();
  out.liveActions = el('vw-detail-actions').innerHTML;
  out.liveBand = el('vw-detail-band').innerHTML;
  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
