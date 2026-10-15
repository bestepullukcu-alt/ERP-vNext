/* WP-VW-W4 (W4-WEB-a) - DOM smoke of visit-report.js in Node: a fake document, a stub fetch and controlled timers.
   Run by VisitReportWebTests with the CRM script folder as argv[2], the scenario as argv[3] and the page language as argv[4] (tr)
   (doctor | amend | unlock | incomplete | noind); prints one JSON object.
   noind (W4-FIX-b, F-5): the product has NO approved indication rows and the server's pre-fill flag says
   potentialAsked=false — the B card shows the note only (no "no patients" button, no star), the preview is "—". */
const dir = process.argv[2];
const scenario = process.argv[3] || 'doctor';
const pageLang = process.argv[4] || 'tr'; // W4-FIX-c — 'en': the English label of a BRD option is its `label`
const nodes = {};
Date.now = () => Date.parse('2026-10-15T12:00:00Z');
const mk = id => ({
  id, innerHTML: '', textContent: '', value: '', dataset: {}, attrs: {},
  classList: { s: new Set(), add(...c) { c.forEach(x => this.s.add(x)); }, remove(...c) { c.forEach(x => this.s.delete(x)); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; }, addEventListener() {}
});
const el = id => nodes[id] || (nodes[id] = mk(id));
const listeners = {};
const root = el('vr-root');
Object.assign(root.attrs, {
  'data-api-base': '/api', 'data-workspace-url': '/ws', 'data-planned-visit-id': 'pv1', 'data-can-record': 'true', 'data-can-amend': 'true',
  'data-can-read-visit': 'true', 'data-can-read-execution': 'true', 'data-can-read-insights': 'true', 'data-can-lookup-competitors': 'true',
  'data-can-propose-competitors': 'true', 'data-can-read-gskus': 'true', 'data-can-pick-products': 'true'
});
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
global.document = { documentElement: { lang: pageLang, dir: 'ltr' }, getElementById: el };

const timeouts = [];
global.setTimeout = (fn, ms) => { timeouts.push({ fn, ms }); return timeouts.length; };
global.clearTimeout = id => { if (id && timeouts[id - 1]) { timeouts[id - 1].fn = null; } };
const flush = () => { const run = timeouts.splice(0); run.forEach(t => t.fn && t.fn()); };
const tick = () => new Promise(r => setImmediate(r));
const settle = async () => { for (let i = 0; i < 12; i++) { await tick(); } };

const A = 'aaaaaaaa-0000-0000-0000-00000000000a', B = 'bbbbbbbb-0000-0000-0000-00000000000b';
const IND = '11111111-0000-0000-0000-000000000001', PROF = '11111111-0000-0000-0000-000000000002';
const GS = 'gggggggg-0000-0000-0000-00000000000a';
const opt = (code, tr) => ({ code, label: code.toUpperCase(), labels: { label_tr: tr } });
const submitted = scenario === 'amend' || scenario === 'unlock';
const body = {
  general: { visitKindCode: 'in_person', coVisitorUserIds: ['u1'], metContactId: 'c1', metRoleCode: null, generalNote: submitted ? 'Kayıtlı not' : null },
  productSections: [
    { productId: A, unplanned: false, presentedSteps: [{ stepId: 's1', seconds: 120 }], matchedPlan: true, minutes: 2, interestCode: 'interested',
      objections: [{ categoryCode: 'price', objectionResponseId: null, note: null, resolved: null }], requests: [{ typeCode: 'sample', dueDate: null, note: null }],
      competitors: [], nextMessageSuggestion: null }
  ],
  doctor: {}, safety: {}, followUp: {}
};
if (submitted) {
  // a sent report is complete: every answer given
  Object.assign(body.productSections[0], { visitSuccessCode: 'reached', commitmentCode: 'none', adoptionStageCode: 'aware', shareBucketCode: 'none',
    potential: { rows: [{ indicationNodeId: IND, profileNodeId: PROF, monthlyPatients: 12 }], noPatientsInIndications: null, updated: false },
    noCompetitor: true, requests: [], noRequest: true, sample: null, noSample: true, nextMessage: 'Hatırlat',
    objections: [{ categoryCode: 'price', objectionResponseId: null, note: null, resolved: 'yes' }] });
  body.safety = { adverseEvent: false, adverseEventNote: null, offLabelRequest: false };
}
const prefill = {
  plannedVisitId: 'pv1', reportKind: scenario === 'pharmacy' ? 'pharmacy' : 'doctor', contactId: 'c1', accountId: 'a1', visitReportId: submitted ? 'r1' : null,
  reportStatus: submitted ? 'submitted' : null, version: submitted ? 4 : 0, executionStatus: 'finished',
  startedAt: '2026-10-15T09:00:00Z', finishedAt: '2026-10-15T09:21:00Z', sampleSigned: true, body,
  previous: { visitReportId: 'r0', executedAt: '2026-07-01T09:00:00Z', productSections: [{ productId: A, adoptionStageCode: 'aware' }], doctor: null },
  profile: { contactId: 'c1', adoptionTypologyCode: 'early_adopter', communicationStyleCode: null, kolLevelCode: 'local', totalMonthlyPatients: 40, totalPatientsAsOf: '2026-09-01T00:00:00Z', totalIsDue: false, version: 2 },
  potentials: [{ productId: A, indicationRows: [{ indicationNodeId: IND, profileNodeId: PROF, monthlyPatients: 12 }], noPatientsInIndications: false, asOf: '2026-05-01T00:00:00Z', isDue: true,
    shareBucketCode: 'occasional', indicationTotal: 12, potentialClass: 'medium', opportunityCode: 'grow', threshold: { lowBelow: 10, highAbove: 30, source: 'default' }, version: 1 }],
  indicationRows: [{ productId: A, items: [{ indicationNodeId: IND, indicationName: 'Gece idrarı', profileNodeId: PROF, profileName: '50+ erkek' }], reason: null }],
  competitors: [{ productId: A, available: true, items: [{ id: 'k1', name: 'Uroflo', company: 'Rakip AŞ', activeIngredients: ['tamsulosin'], formCode: 'modified-release-capsule', strength: '0,4 mg', status: 'active',
    country: { marketCode: 'TR', boxPrice: 123.5, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed' } }] }],
  competitorsUnavailable: false,
  optionSets: {
    'crm-visit-success': [opt('reached', 'Ulaşıldı'), opt('partial', 'Kısmen'), opt('not_reached', 'Ulaşılamadı')],
    'crm-doctor-commitment': [opt('will_try', 'Deneyecek'), opt('none', 'Taahhüt yok')],
    'crm-visit-interest': [opt('interested', 'İlgili'), opt('neutral', 'Nötr'), opt('not_interested', 'İlgisiz')],
    'crm-adoption-stage': ['unaware', 'aware', 'evaluating', 'trying', 'regular', 'advocate'].map(c => opt(c, 'TR-' + c)),
    'crm-share-bucket': [opt('none', 'Hiç'), opt('occasional', 'Ara sıra'), opt('half', 'Yarı yarıya'), opt('mostly', 'Çoğunlukla'), opt('always', 'Hep')],
    'crm-visit-request-type': [opt('sample', 'Numune'), opt('literature', 'Literatür')],
    'crm-competitor-advantage': [opt('efficacy', 'Etkinlik'), opt('price', 'Fiyat')],
    'visit-objection-category': [opt('price', 'Fiyat')],
    'crm-adoption-typology': [opt('early_adopter', 'Erken benimseyen'), opt('innovator', 'Yenilikçi')],
    'crm-communication-style': [opt('analytical', 'Analitik')], 'crm-kol-level': [opt('local', 'Yerel'), opt('national', 'Ulusal')]
  }
};
if (scenario === 'noind') {
  prefill.indicationRows = [{ productId: A, items: [], reason: 'no_concept_graph' }];
  prefill.body.productSections[0].potentialAsked = false;
  prefill.body.productSections[0].potential = null;
}
const visit = { targetDisplayName: 'Dr. Ayşe Kaya', contactDisplayName: 'Dr. Ayşe Kaya', accountDisplayName: 'Hacettepe', plannedDate: '2026-10-15', reportDeadline: '2026-10-17T12:00:00Z',
  contentItems: [{ productId: A, productCode: 'UROF', productName: 'Ürofleks', role: 'promo', steps: [{ stepId: 's1', title: 'Gece idrarı mesajı' }] }] };
const calls = [];
const toasts = [];
let submits = 0;
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let status = 200, data = null, errors = [];
  if (url.startsWith('/api/prefill/')) data = prefill;
  else if (url === '/api/visits/pv1') data = visit;
  else if (url === '/api/execution/by-plan/pv1') data = { note: 'Sunumdaki not', coVisitors: [{ userId: 'u1', displayName: 'Selin Karaca' }] };
  else if (url.startsWith('/api/presentation/')) data = { slides: [{ productId: A, stepId: 's1', title: 'Gece idrarı mesajı', objections: [{ id: 'or1', categoryCode: 'price', question: 'Pahalı değil mi?' }] }] };
  // W4-FIX-c — the REAL Web proxy contract (Platform BusinessReferenceDataPublishedValuesModel via CrmReferenceSetReader)
  else if (url.startsWith('/api/reference-data/')) data = { setCode: 'mdm-reimbursement-status', versionNumber: 1, items: [{ code: 'reimbursed', label: 'Reimbursed', isActive: true, sortOrder: 1, attributes: { label_tr: 'Geri ödemede' } },
    { code: 'not_reimbursed', label: 'Not reimbursed', isActive: false, sortOrder: 2, attributes: { label_tr: 'Geri ödeme dışı' } }] };
  else if (url === '/api/reports/r1') data = { submittedAt: scenario === 'unlock' ? '2026-10-15T11:50:00Z' : '2026-10-15T09:30:00Z', reportStatus: 'submitted', version: 4,
    amendments: scenario === 'amend' && calls.some(c => c.url.endsWith('/amend')) ? [{ at: '2026-10-15T12:00:00Z', reason: 'Yanlış pay', changes: [{ path: 'productSections[a].shareBucketCode', old: '"none"', new: '"half"' }] }] : [] };
  else if (url.startsWith('/api/doctors/')) data = { profile: prefill.profile, history: [{ at: '2026-09-01T00:00:00Z', field: 'kolLevelCode', oldValue: null, newValue: 'local', source: 'report:r0' }],
    availability: [{ weekday: 'Tuesday', startTime: '09:00', endTime: '12:00' }], preferences: [{ preferenceType: 'preferred-channel', channel: 'Yüz yüze', priority: 1 }] };
  else if (url.startsWith('/api/products')) data = { items: [{ id: B, canonicalCode: 'TUTU', globalProductName: 'Tutukon' }] };
  else if (url.startsWith('/api/gskus')) data = { items: [{ id: GS, globalProductId: A, globalProductName: 'Ürofleks', canonicalCode: 'G-1', packQuantity: 28, packUomCode: 'tablet', lifecycleStatus: 'Active' }] };
  else if (url.startsWith('/api/competitors/lookup')) data = [{ id: 'k1', name: 'Uroflo', company: 'Rakip AŞ' }];
  else if (url === '/api/competitors/proposals') data = { id: 'np1', status: 'proposed', existing: false };
  else if (url === '/api/draft') data = { visitReportId: 'r9', version: 1, reportStatus: 'draft', missing: [], warnings: [] };
  else if (url === '/api/submit') {
    submits++;
    if (scenario === 'incomplete' && submits === 1) { status = 400; errors = ['1 required answer(s) are missing.', 'report_incomplete']; data = { missing: [{ section: 'product', productId: A, field: 'nextMessage' }] }; }
    else data = { visitReportId: 'r9', version: 2, reportStatus: 'submitted', missing: [], warnings: ['competitor_proposal_pending:Yeni Rakip'] };
  }
  else if (url.endsWith('/amend')) data = { visitReportId: 'r1', version: 5, reportStatus: 'amended', missing: [], warnings: [] };
  return Promise.resolve({ ok: status < 400, status, text: () => Promise.resolve(JSON.stringify({ data, errors })) });
};
const redirects = [];
global.window = { document: global.document, showToast: (m, t) => toasts.push({ m, t }), scrollTo() {},
  location: { replace: u => redirects.push(u) } };
global.window.VisitReportCore = require(dir + '/VisitReport/report-core.js');
global.window.VisitReportL10n = {
  NoIndicationList: 'Endikasyon listesi tanımlı değil', NoPatients: 'Bu endikasyonlarda hasta görmüyor', IndicationPatients: 'Endikasyon başına hasta',
  StepGeneral: 'Genel', StepSafety: 'Takip ve güvenlik', StepDoctor: 'Doktor profili', StepSummary: 'Özet ve gönder', StepMissing: '{0} eksik',
  FromPresentation: 'sunumdan', MatchedPlan: 'plana uydu', MissingChip: '{0} alan eksik', ReadyChip: 'Gönderime hazır', ProposalPending: 'temsilci ekledi · onay bekliyor',
  TotalExceeded: 'Toplam {0} / {1}', SentTitle: 'Rapor gönderildi', SubmittedTitle: 'Rapor gönderildi.', FixButton: 'Düzelt', UnlockButton: 'Değiştir ({0} dk)',
  FixTitle: 'Gerekçeli düzeltme', ServerMissing: 'Sunucu {0} eksik buldu', Field_nextMessage: 'sonraki mesaj', Field_adverseEvent: 'advers olay sorusu',
  Field_amendReason: 'düzeltme gerekçesi', Field_potentialExceedsTotal: 'toplamı aşıyor', SuggestStage: 'Sonraki aşama: {0}', Suggestion: 'Öneri: {0}',
  Suggest_commitment_will_try: 'Nasıl gitti? Deneme hastalarını sor', AmendmentsTitle: 'Düzeltmeler', Opp_grow: 'Büyüt', Opp_opportunity: 'Fırsat',
  Class_high: 'Çok', Class_medium: 'Orta', ShowHistory: 'Profil geçmişi', OnRecord: 'kayıtta', ActionFailed: 'İşlem başarısız', SameButton: 'Aynısı', MissingToast: '{0} eksik: {1}'
};
require(dir + '/VisitReport/visit-report.js');

const target = attrs => ({ closest: q => (q === '[data-act]' ? { getAttribute: k => (attrs[k] == null ? null : attrs[k]) } : null) });
const click = attrs => listeners.click({ target: target(attrs) });
const change = (attrs, props) => listeners.change({ target: Object.assign({ tagName: 'INPUT', type: 'text', value: '', checked: false, getAttribute: k => (attrs[k] == null ? null : attrs[k]) }, props) });
const input = (attrs, props) => listeners.input({ target: Object.assign({ tagName: 'INPUT', type: 'text', value: '', getAttribute: k => (attrs[k] == null ? null : attrs[k]) }, props) });
const set = (path, value) => click({ 'data-act': 'set', 'data-path': path, 'data-value': value });
const bool = (path, value) => click({ 'data-act': 'bool', 'data-path': path, 'data-value': String(value) });
const step = i => click({ 'data-act': 'step', 'data-step': String(i) });
const html = () => el('vr-body').innerHTML;
const posts = suffix => calls.filter(c => c.method === 'POST' && c.url.endsWith(suffix));
const missingChip = () => el('vr-chips').innerHTML;

(async () => {
  await settle();
  const out = { scenario };
  if (scenario === 'pharmacy') {                     // CT (W4 K13 I1): the wizard hands a pharmacy visit to its own page
    out.redirects = redirects;
    out.calls = calls.map(c => c.method + ' ' + c.url);
    out.body = html();
    process.stdout.write(JSON.stringify(out));
    return;
  }
  out.steps = el('vr-steps').innerHTML;
  out.general = html();
  out.title = el('vr-sub').textContent;

  if (scenario === 'amend' || scenario === 'unlock') {
    out.banner = el('vr-banner').innerHTML;
    if (scenario === 'unlock') { process.stdout.write(JSON.stringify(out)); return; }
    out.fieldsetDisabled = html().indexOf('disabled') > -1;
    click({ 'data-act': 'fix' });
    out.fixBanner = el('vr-banner').innerHTML;
    click({ 'data-act': 'submit' });
    await settle();
    out.amendWithoutReason = posts('/amend').length;
    out.toastNoReason = (toasts[toasts.length - 1] || {}).m || '';
    input({ 'data-reason': '1' }, { value: 'Yanlış pay girilmişti' });
    // the change itself: the share bucket half (amend mode is editable)
    step(1);
    set('productSections.0.shareBucketCode', 'half');
    step(5);
    click({ 'data-act': 'submit' });
    await settle();
    const amend = posts('/amend')[0];
    out.amendBody = amend ? amend.body : null;
    out.amendUrl = amend ? amend.url : null;
    out.afterAmend = html();
    out.drafts = calls.filter(c => c.url === '/api/draft').length;
    process.stdout.write(JSON.stringify(out));
    return;
  }

  // ── doctor: general (pre-fill) ──
  out.metChecked = out.general.indexOf('data-act="met" checked') > -1;
  out.noteFromPresentation = out.general.indexOf('Sunumdaki not') > -1 && out.general.indexOf('sunumdan') > -1;
  out.missingAtStart = missingChip();

  // ── the product step ──
  step(1);
  out.productA = html();
  if (scenario === 'noind') {
    out.missingChip = missingChip();
    process.stdout.write(JSON.stringify(out));
    return;
  }
  set('productSections.0.visitSuccessCode', 'reached');
  set('productSections.0.commitmentCode', 'will_try');
  set('productSections.0.adoptionStageCode', 'evaluating');
  set('productSections.0.shareBucketCode', 'none');
  out.suggestion = html().indexOf('Nasıl gitti? Deneme hastalarını sor') > -1;
  // the indication row (due): on + 50 patients → above the doctor's 40 → the total rule
  change({ 'data-act': 'ind', 'data-p': '0', 'data-k': '0' }, { type: 'checkbox', checked: true });
  change({ 'data-path': 'productSections.0.potential.rows.0.monthlyPatients', 'data-type': 'number' }, { value: '50' });
  out.totalWarning = html().indexOf('Toplam 50 / 40') > -1;
  out.preview = html().match(/vr-opp vr-opp-([a-z_]+)/) ? html().match(/vr-opp vr-opp-([a-z_]+)/)[1] : null;
  // submit while the total rule fails → no POST
  step(5);
  out.summaryAtBlock = html();
  click({ 'data-act': 'submit' });
  await settle();
  out.submitsBlockedByTotal = posts('/submit').length;
  out.blockedToast = (toasts[toasts.length - 1] || {}).m || '';
  // fix the number: 30 ≤ 40
  step(1);
  change({ 'data-path': 'productSections.0.potential.rows.0.monthlyPatients', 'data-type': 'number' }, { value: '30' });
  out.totalWarningAfter = html().indexOf('Toplam') > -1;
  // the competitor: the rep's proposal (name + company) → MDM proposals → "awaiting approval"
  click({ 'data-act': 'prop-open', 'data-p': '0' });
  input({ 'data-local': 'prop', 'data-p': '0', 'data-k': 'name' }, { value: 'Yeni Rakip' });
  input({ 'data-local': 'prop', 'data-p': '0', 'data-k': 'company' }, { value: 'Başka AŞ' });
  click({ 'data-act': 'prop-add', 'data-p': '0' });
  await settle();
  out.proposalBody = (posts('/competitors/proposals')[0] || {}).body || null;
  out.proposalCard = html().indexOf('temsilci ekledi · onay bekliyor') > -1;
  // the MDM card of a listed competitor (TR price + reimbursement label)
  click({ 'data-act': 'comp', 'data-p': '0', 'data-id': 'k1' });
  out.compCard = html();
  click({ 'data-act': 'adv', 'data-p': '0', 'data-c': '1', 'data-code': 'efficacy' });
  click({ 'data-act': 'adv', 'data-p': '0', 'data-c': '1', 'data-code': 'price' });
  click({ 'data-act': 'adv', 'data-p': '0', 'data-c': '1', 'data-code': 'price' });
  // C: the objection row (library answer), resolved; the request; the sample; the next message
  change({ 'data-path': 'productSections.0.objections.0.objectionResponseId' }, { tagName: 'SELECT', value: 'or1' });
  set('productSections.0.objections.0.resolved', 'partly');
  out.libraryShown = html().indexOf('Pahalı değil mi?') > -1;
  change({ 'data-act-change': 'sample', 'data-p': '0' }, { tagName: 'SELECT', value: GS });
  out.sampleSigned = html().indexOf('bx-pen') > -1;
  // "the none options": no request instead of the pre-filled one
  click({ 'data-act': 'row-rm', 'data-path': 'productSections.0.requests', 'data-k': '0' });
  click({ 'data-act': 'toggle', 'data-path': 'productSections.0.noRequest' });
  out.noRequestActive = html().indexOf('vr-none active') > -1;
  click({ 'data-act': 'use-sug', 'data-p': '0' });
  out.missingAfterProduct = missingChip();

  // ── safety: the adverse event is required; "yes" needs the note ──
  step(2);
  out.safety = html();
  bool('safety.adverseEvent', true);
  out.aeNoteAsked = html().indexOf('safety.adverseEventNote') > -1;
  bool('safety.adverseEvent', false);
  bool('safety.offLabelRequest', false);

  // ── the doctor profile card ──
  step(3);
  out.doctor = html();
  click({ 'data-act': 'prof', 'data-key': 'kolLevelCode', 'data-value': 'national' });
  out.doctorChanged = html().indexOf('vr-tile changed') > -1;
  click({ 'data-act': 'history' });
  out.history = html().indexOf('vr-history') > -1;

  // ── the draft saves itself ──
  flush();
  await settle();
  out.drafts = calls.filter(c => c.url === '/api/draft' && c.method === 'PUT').length;
  out.draftBody = (calls.filter(c => c.url === '/api/draft').pop() || {}).body || null;
  out.createError = (toasts.find(t => t.t === 'error') || {}).m || null;

  // ── summary + submit ──
  step(4);
  out.summary = html();
  out.missingBeforeSubmit = missingChip();
  click({ 'data-act': 'submit' });
  await settle();
  if (scenario === 'incomplete') {
    out.serverMissing = html().indexOf('Sunucu 1 eksik buldu') > -1;
    out.stepAfter400 = el('vr-steps').innerHTML.indexOf('aria-selected="true" data-act="step" data-step="4"') > -1;
    click({ 'data-act': 'goto', 'data-key': 'p:' + A });
    out.gotoProduct = html().indexOf('vr-pname') > -1;
  }
  out.submitBody = (posts('/submit')[0] || {}).body || null;
  out.banner = el('vr-banner').innerHTML;
  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { process.stdout.write(JSON.stringify({ crash: String(e && e.stack || e) })); });
