/* WP-VW-W2 (WEB-b) - DOM smoke of visit-workspace.js PLAN mode in Node: a fake document, a stub fetch and a stub
   DitenCalendar that draws the first events INSIDE create (like FullCalendar). Run by VisitWorkspacePlanModeWebTests with
   the CRM script folder as argv[2] and the week state ('draft' | 'approved') as argv[3]; prints one JSON object. */
const dir = process.argv[2];
const weekState = process.argv[3] || 'draft';
const nodes = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vw-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-planning-api-base': '/plan', 'data-can-manage': 'true', 'data-can-record': 'true',
  'data-can-apply': 'true', 'data-can-search-contacts': 'true', 'data-can-plan': 'true' });
root.addEventListener = () => {};
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };
const C0 = require(dir + '/VisitWorkspace/workspace-core.js');
const TC0 = require(dir + '/VisitPlanning/targets-core.js');
const today = new Date().toISOString().slice(0, 10);
const monday = C0.mondayOf(today);
const tue = C0.addDays(monday, 1);
const visits = [
  { plannedVisitId: 'a1', plannedDate: monday, weekStart: monday, startTime: '09:30', endTime: '10:00', targetType: 'contact', targetId: 'p1', contactId: 'p1', accountId: 'acc1',
    targetDisplayName: 'Dr. Ayşe Yılmaz', accountDisplayName: 'Acıbadem Taksim', workStatus: 'planned', plannedContent: [] },
  { plannedVisitId: null, previewKey: 'k1', plannedDate: tue, weekStart: monday, startTime: '11:00', endTime: '11:30', targetType: 'contact', targetId: 't2', contactId: 't2', accountId: 'acc2',
    targetDisplayName: 'Dr. Mert Kaya', workStatus: 'draft', plannedContent: [] }];
const week = { weekStart: monday, weekNumber: 42, state: weekState, planningSessionId: 's1', sessionVersion: 7, canApprove: weekState === 'draft', canReopen: weekState === 'approved',
  capacityMinutes: 2400, plannedMinutes: 600, visitCount: 2, unplacedCount: 1,
  unplaced: [{ targetType: 'contact', targetId: 'u1', displayName: 'Dr. Can Er', accountDisplayName: '018 KLİNİK', reason: 'pin_time_past_day_end' }] };
const data = { visits, weeks: [week], days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(monday, i), kind: 'working', isHoliday: false, capacityMinutes: 480, plannedMinutes: 60, freeMinutes: 420 })) };
const session = { planningSessionId: 's1', version: 7, selectedAccountIds: ['acc1'], selectedPharmacyIds: ['ph1'],
  selectedContacts: [{ contactId: 'p1', accountId: 'acc1', accountContactLinkId: 'l1', products: [{ productId: 'old', productCode: 'OLD', role: 'non-promo' }] }],
  weeks: [{ weekStart: monday, dayPins: [{ targetType: 'contact', targetId: 'x9', contactId: 'x9', date: monday, scope: 'visit', startTime: '09:00' }] }] };
const doctors = [
  { contactId: 'p1', displayName: 'Dr. Ayşe Yılmaz', specialty: 'cardiology', accountContactLinkId: 'l1', status: { requiredVisitCount: 4, dueThisWeek: false } },
  { contactId: 'o1', displayName: 'Dr. Oya Demir', specialty: 'cardiology', accountContactLinkId: 'l2', status: { frequencyDefault: 'weekly', frequencyStatus: 'unknown', dueThisWeek: true, segmentBadges: ['A'] } }];
const calls = [];
const toasts = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body });
  let body;
  if (url.startsWith('/api/calendar')) body = { data };
  else if (url.startsWith('/plan/sessions/s1/targets')) body = { data: { accounts: [{ accountId: 'acc1', accountName: 'Acıbadem Taksim' }] } };
  else if (url.startsWith('/plan/sessions/s1') && method === 'GET') body = { data: session };
  else if (url.startsWith('/plan/sessions/s1') && method === 'PUT') body = { data: true };
  else if (url.startsWith('/plan/my-accounts/acc1/doctors')) body = { data: { items: url.includes('quick=due') ? doctors.filter(d => d.status.dueThisWeek) : doctors } };
  else if (url.startsWith('/plan/my-accounts')) body = { data: { items: [{ accountId: 'acc3', accountName: 'Memorial Şişli' }] } };
  else if (url.startsWith('/plan/reference-labels')) body = { data: { specialties: { cardiology: 'Kardiyoloji' } } };
  else if (url.startsWith('/plan/products')) body = { disabled: false, options: [{ productId: 'pA', productCode: 'TUTU', productName: 'Tutukon' }, { productId: 'pB', productCode: 'ALM', productName: 'Almiba' }] };
  else if (url.startsWith('/api/contract')) body = { data: { maxNoteLength: 500 } };
  else body = { data: null };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify(body)) });
};
const cal = { editable: null };
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {},
  showToast: (m, t) => toasts.push([t || 'success', m]),
  DitenCalendar: { DRAG_TYPE: 'text/x-diten-calendar-item', create: (host, opts) => { cal.opts = opts; cal.editable = !!opts.editable;
    try { (opts.events || []).forEach(e => opts.renderExtras(e)); cal.createError = null; } catch (e) { cal.createError = String(e); }
    return { calendar: { setOption: () => {}, gotoDate: () => {}, updateSize: () => {} }, setEditable: v => { cal.editable = !!v; },
      setData: (ev) => { cal.events = ev; }, date: () => monday }; } } };
global.window.VisitWorkspaceCore = C0;
global.window.VisitPlanningTargetsCore = TC0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = { WeekLabel: '{0}. Hafta', TargetsTitle: 'Hedefler · {0}', PlanReadOnlyWeek: '{0} onaylı — değiştirmek için Haftayı yeniden aç.',
  PlanDoctorsHeading: 'Planda ({0})', OtherDoctorsHeading: 'Diğer doktorlar', SelectAll: 'Tümünü seç ({0})', ApplyProducts: 'Ürün uygula ({0})',
  SelectionSummary: '{0} doktor · {1} eczane · {2} hesap', DueThisWeek: 'Bu hafta görülmeli', QuickDue: 'Bu hafta', QuickNever: 'Hiç', QuickAll: 'Tümü',
  FrequencyPerPeriod: 'dönemde {0}', FrequencyDefaultWeekly: 'haftada 1 (varsayılan)', RolePromo: 'tanıtım', RoleReminder: 'hatırlatma',
  Pin_pin_time_past_day_end: 'Ziyaret mesai bitişini aşıyor, erkene alındı', FilterAllAccounts: 'Tüm kurumlar', Unplaced: '⚠ {0}', UnplacedDetail: '{0}' };
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = (ms) => new Promise(r => setTimeout(r, ms || 20));
const puts = () => calls.filter(c => c.method === 'PUT').map(c => [c.url, JSON.parse(c.body)]);
(async () => {
  await tick(); await tick();
  const out = { state: weekState };
  out.createError = cal.createError === undefined ? 'not-called' : cal.createError;
  out.accountOptions = el('vw-filter-account').innerHTML;
  out.accountHidden = el('vw-filter-account').classList.contains('d-none');

  // P1 — Plan mode
  el('vw-mode-plan').l.click(); await tick(); await tick(); await tick();
  out.panelHidden = el('vw-plan-panel').classList.contains('d-none');
  out.editable = cal.editable;
  out.locked = el('vw-tg-locked').classList.contains('d-none') ? '' : el('vw-tg-locked').textContent;
  out.title = el('vw-tg-title').textContent;
  out.list = el('vw-tg-list').innerHTML;
  out.quick = el('vw-tg-quick').innerHTML;
  out.selectAll = el('vw-tg-select-all').textContent;
  out.apply = el('vw-tg-apply').textContent;
  out.summary = el('vw-tg-summary').textContent;
  out.draftEventEditable = (cal.events || []).filter(e => e.editable).map(e => e.id);

  // P3 — a Targets row dropped on Tuesday 10:37 (→ 10:30), and on the all-day row (→ no time)
  let before = calls.length;
  cal.opts.onExternalDrop({ itemId: C0.dragItem('o1', 'acc1'), allDay: false, date: tue, startUtc: tue + 'T10:37:00Z' }); await tick(); await tick(); await tick();
  out.dropPut = puts().slice(0)[0] || null;
  cal.opts.onExternalDrop({ itemId: C0.dragItem('p1', 'acc1'), allDay: true, date: tue }); await tick(); await tick(); await tick();
  out.allDayPut = puts()[1] || null;
  // a draft card moved to Tuesday 14:07 (→ 14:00)
  let reverted = false;
  cal.opts.onEventMove({ id: 'k1', allDay: false, startUtc: tue + 'T14:07:00Z', durationMinutes: 30, revert: () => { reverted = true; } }); await tick(); await tick(); await tick();
  out.movePut = puts()[2] || null;
  out.moveReverted = reverted;
  out.putCountAfterDrops = puts().length;

  // P4 — apply products to the institution's plan doctors: the first promo, the others reminders
  el('vw-tg-apply').l.click();
  el('vw-prod-search').value = 'tu'; el('vw-prod-search').l.input(); await tick(320);
  const clickProducts = (cls, pid) => el('vw-products-modal').l.click({ target: { closest: q => (q === cls ? { getAttribute: () => pid } : null) } });
  clickProducts('.vw-prod-add', 'pA'); clickProducts('.vw-prod-add', 'pB');
  out.picked = el('vw-prod-picked').innerHTML;
  el('vw-prod-apply').l.click(); await tick(); await tick(); await tick();
  out.productsPut = puts().slice(-1)[0] || null;

  // P5 — the unplaced list and approve with the read's sessionVersion (no session read)
  el('vw-unplaced').l.click();
  out.unplacedList = el('vw-unplaced-list').innerHTML;
  before = calls.length;
  el('vw-approve').l.click(); await tick(); await tick();
  out.approveCalls = calls.slice(before).map(c => c.method + ' ' + c.url + (c.body ? ' ' + c.body : ''));
  out.toasts = toasts;
  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
