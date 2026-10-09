/* WP-VW-W2 (WEB-a) - DOM smoke of visit-workspace.js in Node: a fake document, a stub fetch and a stub DitenCalendar.
   Run by VisitWorkspaceWebTests.A8 with the CRM script folder as argv[2]; prints one JSON object. */
const dir = process.argv[2];
const nodes = {};
const listeners = { doc: {}, root: {} };
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {},
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vw-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-can-manage': 'true', 'data-can-record': 'true', 'data-can-apply': 'true', 'data-can-search-contacts': 'true' });
root.addEventListener = (ev, fn) => { listeners.root[ev] = fn; };
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { listeners.doc[ev] = fn; }, createElement: () => mk('x') };
const today = new Date().toISOString().slice(0, 10);
const C0 = require(dir + '/VisitWorkspace/workspace-core.js');
const monday = C0.mondayOf(today);
const visits = [
  { plannedVisitId: 'a1', plannedDate: monday, weekStart: monday, startTime: '09:30', endTime: '10:00', targetType: 'contact', targetId: 't1', accountId: 'acc1', contactId: 't1', targetDisplayName: 'Dr. Ayşe', workStatus: 'missed', reportDeadline: new Date(Date.now() + 3600e3 * 20).toISOString(), plannedContent: [{ productId: 'p1', productName: 'Tutukon' }, { productId: 'p2', productCode: 'ALFA' }], source: 'unplanned', isPinned: true, pinnedTime: '09:30', rescheduledFromPlannedVisitId: 'x' },
  { plannedVisitId: null, previewKey: 'k1', plannedDate: today, weekStart: monday, targetType: 'contact', targetId: 't2', targetDisplayName: '018 KLİNİK', workStatus: 'draft', plannedContent: [] }];
const data = { visits, weeks: [{ weekStart: monday, weekNumber: 41, state: 'draft', planningSessionId: 's1', canApprove: true, canReopen: false, capacityMinutes: 2400, plannedMinutes: 2600, visitCount: 2, unplacedCount: 3 }],
  days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(monday, i), kind: 'working', isHoliday: i === 3, holidayName: null, capacityMinutes: i === 3 ? 0 : 480, plannedMinutes: 60, freeMinutes: 420 })) };
const calls = [];
global.fetch = (url, o) => { calls.push({ url, method: (o && o.method) || 'GET', body: o && o.body });
  let d = null; if (url.includes('/calendar')) d = data; else if (url.includes('/reasons')) d = { items: [{ code: 'other', label: 'Diğer', requiresNote: true }] }; else if (url.includes('/contract')) d = { maxNoteLength: 500 };
  else if (url.includes('/reschedule-options')) d = { days: [{ date: '2099-01-05', plannedCount: 2, capacityMinutes: 480, plannedMinutes: 60, isHoliday: false }] };
  else if (url.includes('/reports/')) d = { executionOutcome: 'completed', feedback: { doctorFeedback: 'ok' } }; else if (url.includes('/targets')) d = { doctors: [{ contactId: 't1', status: { done: 1, requiredVisitCount: 4, planned: 2 } }] };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) }); };
const cal = { data: null };
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {}, showToast: () => {},
  DitenCalendar: { create: (host, opts) => { cal.opts = opts; return { calendar: { setOption: (k, v) => { cal[k] = v; }, gotoDate: () => {} }, setData: (ev, days) => { cal.data = ev; cal.days = days; }, date: () => monday }; } } };
global.window.VisitWorkspaceCore = C0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = { WeekLabel: '{0}. hafta', Unplaced: '⚠ {0}', Status_missed: 'Kaçırıldı', Status_draft: 'Taslak', CountdownLeft: '{0} sa {1} dk', HoursShort: '{0} sa', DayLoad: '{0} · {1}', CapacityLabel: '{0} / {1}' };
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = () => new Promise(r => setTimeout(r, 20));
(async () => {
  await tick(); await tick();
  const out = {};
  out.events = (cal.data || []).length;
  out.cardHtml = cal.opts.renderExtras({ id: 'a1' });
  out.header = cal.dayHeaderContent({ date: new Date(C0.addDays(monday, 3) + 'T00:00:00Z') }).html;
  out.weekTitle = el('vw-week-title').textContent; out.unplaced = el('vw-unplaced').textContent; out.approveHidden = el('vw-approve').classList.contains('d-none');
  out.weekends = cal.weekends;
  cal.opts.onEventClick('a1'); await tick(); await tick();
  out.actions = el('vw-detail-actions').innerHTML; out.content = el('vw-detail-content').innerHTML; out.prev = el('vw-detail-previous-body').innerHTML; out.freq = el('vw-detail-frequency').textContent;
  // E2 reschedule: open, pick 'other' (requiresNote), no note = Save disabled, note + day = Save, two calls in order
  const click = (sel, attrs) => listeners.doc.click({ target: { closest: q => (q === sel ? { getAttribute: k => attrs[k] } : null) } });
  click('[data-dialog]', { 'data-dialog': 'reschedule' }); await tick(); await tick();
  out.reasonsHtml = el('vw-dlg-reasons').innerHTML;
  listeners.doc.change({ target: { name: 'vw-reason', value: 'other' } });
  out.saveDisabledWithoutNote = el('vw-dlg-save').disabled;
  out.noteRequiredShown = !el('vw-dlg-note-required').classList.contains('d-none');
  el('vw-dlg-note').value = 'Kongre'; el('vw-dlg-note').l.input();
  out.saveDisabledWithoutDay = el('vw-dlg-save').disabled;
  click('.vw-day-option', { 'data-date': '2099-01-05' });
  out.saveEnabled = !el('vw-dlg-save').disabled;
  const before = calls.length;
  el('vw-dlg-save').l.click(); await tick(); await tick(); await tick();
  out.saveCalls = calls.slice(before).filter(c => c.method === 'POST').map(c => [c.url, JSON.parse(c.body)]);
  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
