/* WP-VW-W2 (WEB-c) - DOM smoke of the design fit: visit-workspace.js in Node with a fake document, a stub fetch and a
   stub DitenCalendar (the first events are drawn INSIDE create, like FullCalendar). Run by
   VisitWorkspaceDesignFitWebTests with the CRM script folder as argv[2]; prints one JSON object. */
const dir = process.argv[2];
const nodes = {};
const listeners = { doc: {}, root: {} };
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, dataset: {}, style: {}, attrs: {}, className: '',
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
const nextMonday = C0.addDays(monday, 7);
const friday = C0.addDays(nextMonday, 4); // next week: the actions are open (not a past day)
const visits = [
  { plannedVisitId: 'a1', plannedDate: friday, weekStart: nextMonday, startTime: '10:15', endTime: '10:45', durationMinutes: 30, targetType: 'contact', targetId: 't1', accountId: 'acc1', contactId: 't1',
    targetDisplayName: 'Dr. Ayşe Kaya', accountDisplayName: 'Hacettepe Hastanesi', accountAddress: 'Sıhhiye, Ankara', specialtyCode: 'SPEC_CARD_X', specialtyLabel: 'Kardiyoloji', badges: ['KOL'],
    workStatus: 'planned', plannedContent: [{ productId: 'p1', productName: 'Tutukon', role: 'promo', steps: [{ title: 'Açılış' }, { title: 'Klinik' }] }, { productId: 'p2', productName: 'Almiba', role: 'reminder', steps: [] }], isPinned: true, pinnedTime: '10:15' },
  { plannedVisitId: 'a3', plannedDate: C0.addDays(nextMonday, 1), weekStart: nextMonday, startTime: '09:00', endTime: '09:20', targetType: 'contact', targetId: 't3', contactId: 't3', targetDisplayName: 'Dr. Can',
    workStatus: 'missed', reportDeadline: new Date(Date.now() + 3600e3 * 3).toISOString(), plannedContent: [{ productId: 'p1', productName: 'Tutukon' }] },
  { plannedVisitId: null, previewKey: 'k1', plannedDate: friday, weekStart: nextMonday, targetType: 'contact', targetId: 't2', targetDisplayName: '018 KLİNİK', workStatus: 'draft', plannedContent: [] }];
const data = { periodName: '2026 · 4. Dönem', visits,
  weeks: [{ weekStart: monday, weekNumber: 41, state: 'past', canApprove: false, canReopen: false, capacityMinutes: 2400, plannedMinutes: 0, visitCount: 0, unplacedCount: 0 },
    { weekStart: nextMonday, weekNumber: 42, state: 'approved', planningSessionId: 's1', canApprove: false, canReopen: true, capacityMinutes: 2400, plannedMinutes: 600, visitCount: 2, unplacedCount: 2 }],
  days: [0, 1, 2, 3, 4].map(i => ({ date: C0.addDays(nextMonday, i), kind: 'working', isHoliday: false, holidayName: null, capacityMinutes: 480, plannedMinutes: i === 4 ? 30 : 0, freeMinutes: i === 4 ? 450 : 480 })) };
const calls = [];
global.fetch = (url, o) => { calls.push({ url, method: (o && o.method) || 'GET', body: o && o.body });
  let d = null; if (url.includes('/calendar')) d = data; else if (url.includes('/reasons')) d = { items: [{ code: 'other', label: 'Diğer', requiresNote: false }] }; else if (url.includes('/contract')) d = { maxNoteLength: 500 };
  else if (url.includes('/reschedule-options')) d = { days: [0, 1, 2, 3, 4, 5].map(i => ({ date: '2099-01-0' + (i + 1), plannedCount: i, capacityMinutes: 480, plannedMinutes: 60 * i, isHoliday: false })) };
  else if (url.includes('/targets')) d = { doctors: [{ contactId: 't1', status: { done: 1, requiredVisitCount: 4, planned: 2 } }] };
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: d })) }); };
const cal = { opts: {}, views: [] };
const TYPES = { timeGridDay: 'day', timeGridWeek: 'week', dayGridMonth: 'month' };
global.window = { document: global.document, innerWidth: 1280, localStorage: { getItem: () => null, setItem: () => {} }, addEventListener: () => {}, showToast: () => {},
  DitenCalendar: { create: (host, opts) => { cal.opts = opts; cal.createView = opts.view;
    try { (opts.events || []).forEach(e => opts.renderExtras(e)); cal.createError = null; } catch (e) { cal.createError = String(e); }
    const fc = { setOption: (k, v) => { cal[k] = v; }, gotoDate: () => {}, updateSize: () => {},
      prev: () => cal.views.push('prev'), next: () => { cal.views.push('next'); opts.onRangeChange({ view: 'week', from: nextMonday, to: friday, date: nextMonday }); }, today: () => cal.views.push('today'),
      changeView: (type, date) => { cal.views.push(type + (date ? '@' + date : ''));
        opts.onRangeChange({ view: TYPES[type], from: date || monday, to: date || friday, date: date || monday }); } };
    return { calendar: fc, setEditable: v => { cal.editable = !!v; }, setData: (ev, days) => { cal.data = ev; cal.days = days; }, date: () => monday }; } } };
global.window.VisitWorkspaceCore = C0;
require(dir + '/VisitPlanning/format.js');
global.window.VisitWorkspaceL10n = { WeekLabel: '{0}. Hafta', Unplaced: '⚠ {0} ziyaret sığmadı', UnplacedDetail: '{0}', HoursShort: '{0} sa', MinutesShort: '{0} dk',
  CapacityWeekLine: 'Bu hafta {0} kapasite · {1} planlı', DayLoad: '{0} ziyaret · boş {1}', TodayBadge: 'Bugün', MonthVisits: '{0} ziyaret',
  Status_planned: 'Planlı', Status_draft: 'Taslak', WeekStateApproved: 'Onaylı', FilterAllStatuses: 'Tüm durumlar', FilterStatusCount: '{0} durum',
  LegendPinned: 'sabit', LegendUnplanned: 'plan dışı', LegendProduct: 'ÜRÜN', RolePromo: 'tanıtım', RoleReminder: 'hatırlatma',
  DetailPinned: 'Güne sabit', AlertTitle_planned: 'Hafta onaylı', AlertText_planned: 'Güne taşımak için haftayı yeniden açın.',
  EstimateLine: '{0} tanıtım + {1} hatırlatma + rapor ≈ {2} dk', DetailFrequency: 'Sıklık: dönemde {0} · {1}', ContentSteps: '{0} içerik adımı',
  DlgTab_cancel: 'İptal et', DlgTab_notDone: 'Yapılamadı', DlgTab_reschedule: 'Ertele', DlgMean_reschedule: 'Başka güne kaydırın; yeni tarih seçin.',
  DlgConfirm_reschedule: 'Ziyareti ertele', CardMarkLeft: 'İşaretlemek için {0} sa', DlgRescheduleTitle: 'Ertele', Action_reschedule: 'Ertele', Action_cancel: 'İptal et' };
require(dir + '/VisitWorkspace/visit-workspace.js');
const tick = () => new Promise(r => setTimeout(r, 20));
const rootClick = (sel, attrs) => listeners.root.click({ target: { closest: q => (q === sel ? { getAttribute: k => attrs[k] } : null) } });
const docClick = (sel, attrs) => listeners.doc.click({ target: { closest: q => (q === sel ? { getAttribute: k => attrs[k], disabled: !!attrs.disabled } : null) } });
(async () => {
  await tick(); await tick();
  const out = {};
  el('vw-next').l.click(); await tick(); await tick(); await tick();
  out.createError = cal.createError === undefined ? 'not-called' : cal.createError;
  out.stripTouched = Object.prototype.hasOwnProperty.call(nodes, 'vw-week-strip');
  out.headerToolbar = cal.headerToolbar; out.allDaySlot = cal.allDaySlot; out.slotMinTime = cal.slotMinTime; out.weekends = cal.weekends;
  out.events = (cal.data || []).map(e => ({ id: e.id, allDay: !!e.allDay, cls: e.classNames || [] }));
  out.period = el('vw-period-chip').innerHTML; out.periodHidden = el('vw-period-chip').classList.contains('d-none');
  out.rangeTitle = el('vw-range-title').textContent;
  out.weekTitle = el('vw-week-title').textContent; out.weekBadge = el('vw-week-badge').className; out.weekBadgeText = el('vw-week-badge').textContent;
  out.capacity = el('vw-capacity-text').textContent; out.unplaced = el('vw-unplaced').textContent; out.unplacedHidden = el('vw-unplaced').classList.contains('d-none');
  out.reopenHidden = el('vw-reopen').classList.contains('d-none');
  out.unplannedHidden = el('vw-unplanned-open').classList.contains('d-none');
  out.statusLabel = el('vw-filter-status-label').textContent;
  out.legend = el('vw-legend').innerHTML;
  out.card = cal.opts.renderExtras({ id: 'a1' });
  out.header = cal.dayHeaderContent({ date: new Date(friday + 'T00:00:00Z'), view: { type: 'timeGridWeek' } }).html;
  // the detail panel (D1–D4, D6)
  cal.opts.onEventClick('a1'); await tick(); await tick();
  for (const id of ['vw-detail-chips', 'vw-detail-title', 'vw-detail-tags', 'vw-detail-account', 'vw-detail-address', 'vw-detail-when', 'vw-detail-band', 'vw-detail-content', 'vw-detail-estimate', 'vw-detail-frequency', 'vw-detail-actions']) {
    out[id] = { html: el(id).innerHTML || el(id).textContent, hidden: el(id).classList.contains('d-none') };
  }
  // E2 — ONE dialog, three tabs (a missed visit: not done + reschedule; no cancel); reschedule = a 4-column grid of day cards
  out.compactCard = cal.opts.renderExtras({ id: 'a3' });
  cal.opts.onEventClick('a3'); await tick(); await tick();
  out.missedActions = el('vw-detail-actions').innerHTML;
  docClick('[data-dialog]', { 'data-dialog': 'reschedule' }); await tick(); await tick();
  out.dlgTabs = el('vw-dlg-tabs').innerHTML; out.dlgMean = el('vw-dlg-mean').textContent; out.dlgSave = el('vw-dlg-save').textContent;
  out.dlgDays = el('vw-dlg-days').innerHTML; out.dlgDaysClass = el('vw-dlg-days').className; out.dlgTarget = el('vw-dlg-target').textContent || el('vw-dlg-target').innerHTML;
  // the view switch: Month → one summary event per day; a month day → the Day view
  rootClick('[data-view]', { 'data-view': 'month' }); await tick(); await tick(); await tick();
  out.monthEvents = (cal.data || []).map(e => e.id);
  out.monthCell = cal.opts.renderExtras({ id: 'm:' + friday });
  out.monthWeekHeadHidden = el('vw-week-head').classList.contains('d-none');
  cal.opts.onEventClick('m:' + friday); await tick(); await tick();
  out.views = cal.views.slice();
  docClick('[data-tab]', { 'data-tab': 'cancel', disabled: true });
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
