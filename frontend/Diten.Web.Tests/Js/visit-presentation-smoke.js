/* WP-VW-W3 (W3-WEB-b) - DOM smoke of visit-presentation.js in Node: a fake document, a stub fetch and controlled timers.
   Run by VisitPresentationWebTests with the CRM script folder as argv[2] and the scenario as argv[3]
   (run | tablet | summary | notStarted); prints one JSON object. */
const dir = process.argv[2];
const scenario = process.argv[3] || 'run';
const nodes = {};
// CT (live E4): a fixed wall clock — the live chip counts the visit from its server start (2026-10-15T09:00Z + 5 min)
Date.now = () => Date.parse('2026-10-15T09:05:00Z');
// the browser's Blob / FormData (Node 16 has neither as a global)
if (typeof Blob === 'undefined') { global.Blob = require('buffer').Blob; }
if (typeof FormData === 'undefined') { global.FormData = class { constructor() { this.m = new Map(); } append(k, v) { this.m.set(k, v); } get(k) { return this.m.get(k); } }; }
const listeners = { doc: {}, root: {}, win: {} };
const mk = id => ({
  id, innerHTML: '', textContent: '', value: '', checked: false, disabled: false, dataset: {}, style: {}, attrs: {}, options: [],
  width: 700, height: 160,
  classList: { s: new Set(), add(...c) { c.forEach(x => this.s.add(x)); }, remove(...c) { c.forEach(x => this.s.delete(x)); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; },
  getContext() { return { beginPath() {}, moveTo() {}, lineTo() {}, stroke() {}, clearRect() {} }; },
  getBoundingClientRect() { return { left: 0, top: 0, width: 700, height: 160 }; },
  toBlob(cb) { cb(new Blob([new Uint8Array([137, 80, 78, 71])], { type: 'image/png' })); }
});
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('vp-root');
Object.assign(root.attrs, {
  'data-api-base': '/api', 'data-workspace-url': '/ws', 'data-report-url': '/report', 'data-planned-visit-id': 'pv1',
  'data-phase': scenario === 'summary' ? 'summary' : 'run', 'data-can-record': 'true', 'data-can-read-visit': 'true', 'data-can-pick-people': 'true'
});
root.addEventListener = (ev, fn) => { listeners.root[ev] = fn; };
global.document = {
  documentElement: { lang: 'tr', dir: 'ltr' }, visibilityState: 'visible', getElementById: el,
  addEventListener: (ev, fn) => { listeners.doc[ev] = fn; }, createElement: () => mk('x')
};

// controlled timers: the page's debounce / long press run only when the smoke flushes them
const timeouts = [];
let intervalFn = null;
global.setTimeout = (fn, ms) => { timeouts.push({ fn, ms }); return timeouts.length; };
global.clearTimeout = id => { if (id && timeouts[id - 1]) { timeouts[id - 1].fn = null; } };
const flush = () => { const run = timeouts.splice(0); run.forEach(t => t.fn && t.fn()); };
const tick = () => new Promise(r => setImmediate(r));
const settle = async () => { for (let i = 0; i < 8; i++) { await tick(); } };

const A = 'aaaaaaaa-0000-0000-0000-00000000000a', B = 'bbbbbbbb-0000-0000-0000-00000000000b';
const presentation = {
  plannedVisitId: 'pv1',
  slides: [
    { index: 0, productId: A, productCode: 'TUTU', productName: 'Tutukon', role: 'promo', stepId: 's1', contentId: 'c1', title: 'Gece idrarı', subtitle: 'Alt başlık', bullets: ['Onaylı iddia 1'], references: ['Ref 2024'], asset: { kind: 'none', url: null },
      presentation: { keyMessage: 'SECRET KEY MESSAGE', talkTrack: 'SECRET TALK TRACK', targetSeconds: 90, transition: 'SECRET TRANSITION' },
      objections: [{ id: 'o1', categoryCode: 'price', categoryLabel: 'Fiyat', question: 'Pahalı değil mi?', answer: 'SECRET ANSWER', references: [] }] },
    { index: 1, productId: A, productCode: 'TUTU', productName: 'Tutukon', role: 'promo', stepId: 's2', contentId: 'c2', title: 'Klinik veri', subtitle: null, bullets: [], references: [], asset: { kind: 'none', url: null }, presentation: { targetSeconds: 30 }, objections: [] },
    { index: 2, productId: B, productCode: 'ALM', productName: 'Almiba', role: 'non-promo', stepId: 's3', contentId: 'c3', title: 'Hatırlatma', subtitle: null, bullets: [], references: [], asset: { kind: 'none', url: null }, presentation: {}, objections: [] }
  ],
  objectionCategories: [{ code: 'price', label: 'Fiyat' }, { code: 'efficacy', label: 'Etkinlik' }],
  warnings: []
};
const started = {
  id: 'ex1', plannedVisitId: 'pv1', status: 'started', kind: 'in_person', startedAt: '2026-10-15T09:00:00Z',
  productTimings: [{ productId: A, seconds: 40 }], slideTimings: [{ productId: A, stepId: 's1', seconds: 40 }],
  marks: [], note: null, personalNotes: [{ productId: A, text: 'kendi notum' }], coVisitors: [], signature: { present: false }, flags: []
};
const finished = Object.assign({}, started, {
  status: 'finished', finishedAt: '2026-10-15T09:20:00Z', durationSeconds: 1200, shortVisit: false,
  marks: [{ productId: B, interest: 'interested', flags: ['sample_request'], objectionCategory: null }], note: 'Not metni',
  coVisitors: [{ userId: 'u1', displayName: 'Selin Karaca' }]
});
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body });
  let data = null;
  if (url.startsWith('/api/presentation/')) data = presentation;
  else if (url === '/api/execution/by-plan/pv1') data = scenario === 'notStarted' ? null : started;
  else if (url.endsWith('/brief')) data = { plannedVisitId: 'pv1', lastVisitDate: '2026-10-01', outcomeCode: 'positive', followUpNotes: 'Literatür gönder', samples: [{ quantity: 2 }] };
  else if (url === '/api/execution/contract') data = { policy: { doctorConfirmation: true, durationConsistency: true, shortVisitSeconds: 120 } };
  else if (url === '/api/visits/pv1') data = { targetDisplayName: 'Dr. Ayşe Kaya', accountDisplayName: 'Hacettepe' };
  else if (url === '/api/people') data = { people: [{ userId: 'u1', displayName: 'Selin Karaca' }] };
  else if (url.endsWith('/capture')) data = started;
  else if (url.endsWith('/finish')) data = finished;
  else if (url.endsWith('/signature')) data = Object.assign({}, finished, { signature: { present: true, context: 'sample' } });
  return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data })) });
};
global.window = {
  document: global.document, innerWidth: scenario === 'tablet' ? 1000 : 1440, location: { href: '' },
  addEventListener: (ev, fn) => { listeners.win[ev] = fn; }, setInterval: fn => { intervalFn = fn; return 1; }, showToast: () => {},
  DitenPersonPicker: { renderPersonOptions: (select, rows) => { select.options = rows.map(r => ({ value: r.userId, selected: false })); } }
};
global.window.VisitPresentationCore = require(dir + '/VisitPresentation/presentation-core.js');
global.window.VisitPresentationL10n = {
  NotStartedTitle: 'Ziyaret başlatılmamış.', NotStartedText: 'Çalışma alanından başlatın.', BackToWorkspace: 'Çalışma alanı',
  TabNotes: 'Notlar', TabMarks: 'İşaretler', TabNote: 'Not al', TalkTrack: 'Konuşma metni', KeyMessage: 'Anahtar mesaj',
  ThisSlide: 'Bu slayt {0}', TargetLabel: 'hedef ≈ {0}', TargetSeconds: '{0} sn', TargetMinutes: '{0} dk', LiveChip: 'Devam ediyor · {0}',
  Flag_objection: 'İtiraz etti', ObjectionWith: '{0}: {1}', ForThisDoctor: 'Bu doktor için', BriefLastVisit: 'Son ziyaret: {0}',
  SignatureTitleSample: 'Numune teslimi · doktor imzası', SignatureTitleApproval: 'Doktor onayı', SummaryTitle: 'Ziyaret bitti', FinishVisit: 'Ziyareti bitir'
};
require(dir + '/VisitPresentation/visit-presentation.js');

const click = (act, attrs) => listeners.root.click({ target: { closest: q => (q === '[data-act]' ? { getAttribute: k => (k === 'data-act' ? act : (attrs || {})[k]) } : null) } });
const change = (field, props) => listeners.root.change({ target: Object.assign({ getAttribute: k => (k === 'data-field' ? field : null) }, props) });
const puts = () => calls.filter(c => c.method === 'PUT');
const posts = suffix => calls.filter(c => c.method === 'POST' && c.url.endsWith(suffix));
const docHtml = () => ['vp-ptabs', 'vp-slide', 'vp-ref', 'vp-dots', 'vp-slide-no', 'vp-doc-hint'].map(id => el(id).innerHTML + el(id).textContent).join('|');

(async () => {
  await settle();
  const out = { scenario };
  out.notice = el('vp-notice').innerHTML;
  out.noticeShown = !el('vp-notice').classList.contains('d-none');
  out.splitShown = !el('vp-split').classList.contains('d-none');
  out.rootClasses = Array.from(root.classList.s);

  if (scenario === 'notStarted') { process.stdout.write(JSON.stringify(Object.assign(out, { calls: calls.map(c => c.method + ' ' + c.url) }))); return; }

  if (scenario === 'summary') {
    out.finishCalls = posts('/finish').length;
    out.summaryShown = !el('vp-summary').classList.contains('d-none');
    out.summary = el('vp-summary').innerHTML;
    process.stdout.write(JSON.stringify(out));
    return;
  }

  if (scenario === 'tablet') {
    out.panelOpenAtStart = root.classList.contains('vp-panel-open');
    click('doctor-mode');
    ['1', '2', '3', '4'].forEach(d => click('pin-digit', { 'data-digit': d }));
    out.confirmPhase = el('vp-pin').innerHTML.length > 0;
    ['1', '2', '3', '4'].forEach(d => click('pin-digit', { 'data-digit': d }));
    out.lockedPanelOpen = root.classList.contains('vp-panel-open');
    out.handleShown = !el('vp-handle').classList.contains('d-none');
    // the long press on the handle → the PIN pad → the rep panel; a wrong PIN first
    listeners.root.pointerdown({ target: { closest: q => (q === '[data-hold]' ? { getAttribute: () => 'unlock' } : null) }, clientX: 0 });
    out.holdMs = timeouts.length ? timeouts[timeouts.length - 1].ms : null;
    flush();
    out.unlockPadShown = !el('vp-pin').classList.contains('d-none');
    ['9', '9', '9', '9'].forEach(d => click('pin-digit', { 'data-digit': d }));
    out.wrongPinPanelOpen = root.classList.contains('vp-panel-open');
    ['1', '2', '3', '4'].forEach(d => click('pin-digit', { 'data-digit': d }));
    out.unlockedPanelOpen = root.classList.contains('vp-panel-open');
    out.finishCallsAfterExit = posts('/finish').length; // the exit never finishes the visit
    click('relock');
    out.relockedPanelOpen = root.classList.contains('vp-panel-open');
    process.stdout.write(JSON.stringify(out));
    return;
  }

  // ── desktop run ──
  out.topbarName = el('vp-visit-name').textContent;
  out.doc = docHtml();
  out.rep = el('vp-rep').innerHTML;
  out.myNoteRestored = out.rep.includes('kendi notum');
  // timers: two visible seconds count, hidden seconds do not
  intervalFn(); intervalFn();
  out.liveAfterTwo = el('vp-live-text').textContent;
  out.totalAfterTwo = el('vp-total').textContent;
  global.document.visibilityState = 'hidden';
  listeners.doc.visibilitychange();
  await settle();
  intervalFn(); intervalFn(); intervalFn();
  out.liveWhileHidden = el('vp-live-text').textContent;
  out.totalWhileHidden = el('vp-total').textContent;
  out.putsOnHide = puts().length;
  global.document.visibilityState = 'visible';
  listeners.doc.visibilitychange();
  await settle();

  // marks: "objected" asks the category first
  click('tab', { 'data-tab': 'marks' });
  click('flag', { 'data-flag': 'objection' });
  out.categoryPicker = el('vp-rep').innerHTML.includes('data-act="category"');
  click('category', { 'data-category': 'price' });
  out.flagLabel = el('vp-rep').innerHTML.includes('İtiraz etti: Fiyat');
  click('next');
  out.docAfterNext = el('vp-slide').innerHTML;
  flush(); // the debounced capture after the slide change
  await settle();
  const put = puts()[puts().length - 1];
  out.captureBody = put ? JSON.parse(put.body) : null;
  click('skip');
  out.curAfterSkip = el('vp-slide-no').textContent;

  // finish → confirmation → POST finish → E4c with the signature card
  click('finish');
  out.confirmShown = !el('vp-confirm').classList.contains('d-none');
  out.finishBeforeConfirm = posts('/finish').length;
  click('confirm-yes');
  await settle();
  out.finishCalls = posts('/finish').length;
  out.summaryShown = !el('vp-summary').classList.contains('d-none');
  out.summary = el('vp-summary').innerHTML;
  // the signature: draw, confirm, save
  const canvas = el('vp-sig');
  if (canvas.l && canvas.l.pointerup) { canvas.l.pointerup({}); }
  change('sig-confirm', { checked: true });
  click('save-signature');
  await settle();
  const sig = posts('/signature')[0];
  out.signatureCall = sig ? { isForm: typeof FormData !== 'undefined' && sig.body instanceof FormData, context: sig.body.get('context'), confirmed: sig.body.get('confirmed'), file: !!sig.body.get('file') } : null;
  out.summaryAfterSign = el('vp-summary').innerHTML;
  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
