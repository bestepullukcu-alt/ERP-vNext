/* WP-VW-W4 (W4-WEB-b) - DOM smoke of pharmacy-report.js in Node: a fake document, a stub fetch, a stub FormData.
   Run by VwW4PharmacyThresholdsWebTests with the CRM script folder as argv[2], a JSON file of the Turkish script texts
   (the real resx) as argv[3] and the mode as argv[4] ('submit' | 'readonly' | 'window'); prints one JSON object.
   submit: the GSKU rows are seeded from the plan products only; a gif is refused before any call; a jpeg goes multipart and
   its photoEvidenceId lands in the draft; the pre-order button is passive ("soon"); without a trend the submit stays local
   (summary, no POST); with every answer the submit posts the pharmacy body.
   readonly: no draft / submit buttons, a click changes nothing, no write call.
   window: a sent report → "Change" → submit answered 409 edit_window_closed → the reasoned correction (no reason = no call;
   a reason = POST amend).
   W4-FIX-c (G-6) — the options come in the REAL pre-fill contract (CRM VisitReportOptionDto: the BRD value's English label
   + its attributes as labels: { label_tr, label_fr, … }); G-7 the header's pharmacy (the target proxy); G-10 the doctor
   results with specialty / institution / city. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'submit';
const nodes = {};
const listeners = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, checked: false, dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; }, click() { this.clicked = (this.clicked || 0) + 1; }, focus() {} });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('ph-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-planned-visit-id': 'PV1', 'data-can-record': mode === 'readonly' ? 'false' : 'true',
  'data-can-amend': 'true', 'data-can-read-gskus': 'true', 'data-can-read-contacts': 'true', 'data-me': 'user-me' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
['ph-sent', 'ph-fix', 'ph-error', 'ph-draft', 'ph-submit', 'ph-prev'].forEach(id => el(id).classList.add('d-none')); // as the view renders them
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: () => {}, createElement: () => mk('x') };

const opt = (code, tr, en) => ({ code, label: en, labels: { label_tr: tr, label_fr: en + ' (fr)', note: 'not a label' } });
const prefill = {
  plannedVisitId: 'PV1', reportKind: 'pharmacy', contactId: null, accountId: 'A1', visitReportId: mode === 'window' ? 'R1' : null,
  reportStatus: mode === 'window' ? 'submitted' : null, version: mode === 'window' ? 3 : 0, executionStatus: 'finished',
  startedAt: '2026-10-13T08:31:00Z', finishedAt: '2026-10-13T08:52:00Z', sampleSigned: false,
  body: { general: { visitKindCode: 'in_person', coVisitorUserIds: [], metContactId: null, metRoleCode: mode === 'window' ? 'pharmacist' : null, generalNote: '' },
    productSections: [], doctor: null,
    pharmacy: mode === 'window'
      ? { stock: [{ gskuId: 'G1', levelCode: 'in_stock' }], shelf: { positionCode: null, materialStand: false, photoEvidenceId: null }, sales: [{ gskuId: 'G1', trendCode: 'up', boxesPerMonth: 12 }], competitors: [], noCompetitor: true, recommendation: { levelCode: null, linkedContactIds: [] }, training: { given: null, requests: [] } }
      : { stock: [], shelf: null, sales: [], competitors: [], noCompetitor: false, recommendation: null, training: null },
    safety: { adverseEvent: mode === 'window' ? false : null, adverseEventNote: null, offLabelRequest: null }, followUp: null },
  previous: null, profile: null, potentials: [], indicationRows: [],
  competitors: [{ productId: 'P1', available: true, items: [{ id: 'C1', name: 'Tensilox', company: 'Rakip AŞ', activeIngredients: ['tamsulosin'], formCode: 'capsule', strength: '0.4 mg', status: 'active',
    country: { marketCode: 'TR', boxPrice: 120.5, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed' } }] }],
  competitorsUnavailable: false,
  optionSets: {
    'crm-pharmacy-met-role': [opt('pharmacist', 'Eczacı', 'Pharmacist'), opt('assistant', 'Kalfa', 'Assistant')],
    'crm-pharmacy-stock-level': [opt('in_stock', 'Var', 'In stock'), opt('low', 'Az', 'Low'), opt('out', 'Yok', 'Out of stock')],
    'crm-shelf-position': [opt('eye_level', 'Göz hizası', 'Eye level'), opt('bottom', 'Alt raf', 'Bottom shelf')],
    'crm-sales-trend': [opt('up', 'Artıyor', 'Rising'), opt('flat', 'Sabit', 'Flat'), opt('down', 'Azalıyor', 'Falling')],
    'crm-recommendation-level': [opt('low', 'Az', 'Low'), opt('medium', 'Orta', 'Medium'), opt('high', 'Çok', 'High')],
    'crm-visit-request-type': [opt('material', 'Materyal', 'Material')],
    'crm-competitor-advantage': [opt('price', 'Fiyat', 'Price')]
  }
};
const gskus = [{ id: 'G1', code: 'URO-30', productId: 'P1', productName: 'Ürofleks', pack: '30 TAB' }, { id: 'G9', code: 'OTHER-1', productId: 'P9', productName: 'Başka', pack: '' }];
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  let body = null;
  if (o && o.body) { body = typeof o.body === 'string' ? JSON.parse(o.body) : { multipart: true, parts: o.body.parts }; }
  calls.push({ url, method, body, headers: (o && o.headers) || {} });
  let d = null, status = 200, errors;
  if (url === '/api/PV1/prefill') d = prefill;
  else if (url.indexOf('/api/lookups/gskus') === 0) d = gskus;
  else if (url === '/api/PV1/target') d = { name: 'Hayat Eczanesi', city: 'İstanbul' };
  else if (url === '/api/reference-data/mdm-reimbursement-status/values') d = { setCode: 'mdm-reimbursement-status', items: [{ code: 'reimbursed', label: 'Reimbursed', isActive: true, sortOrder: 1, attributes: { label_tr: 'Geri ödemede' } }] };
  else if (url.indexOf('/api/lookups/contacts') === 0) d = [{ id: 'D1', name: 'Dr. Ali Yılmaz', specialtyCode: 'cardiology', specialty: 'Kardiyoloji', city: 'Ankara', institution: 'Şehir Hastanesi' },
    { id: 'D2', name: 'Dr. Ali Yılmaz', specialtyCode: 'urology', specialty: 'Üroloji', city: null, institution: null }];
  else if (url === '/api/PV1/shelf-photo' && method === 'POST') { d = { photoEvidenceId: 'E1' }; status = 201; }
  else if (url === '/api/draft') d = { visitReportId: 'R1', version: 1, reportStatus: 'draft', missing: [], warnings: [] };
  else if (url === '/api/submit' && mode === 'window') { status = 409; errors = ['The correction window has closed.', 'visit_report_edit_window_closed']; }
  else if (url === '/api/submit') d = { visitReportId: 'R1', version: 2, reportStatus: 'submitted', missing: [], warnings: [] };
  else if (url === '/api/R1/amend') d = { visitReportId: 'R1', version: 4, reportStatus: 'amended', missing: [], warnings: [] };
  const payload = errors ? { isSuccessful: false, errors } : { data: d };
  return Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify(payload)) });
};
const toasts = [];
class FakeFormData { constructor() { this.parts = []; } append(name, file, fileName) { this.parts.push([name, fileName, file.type]); } }
global.window = { document: global.document, showToast: m => toasts.push(m), FormData: FakeFormData, URL: { createObjectURL: () => 'blob:shelf' } };
require(dir + '/VisitPlanning/format.js');
global.window.PharmacyReportL10n = texts;
global.window.ReportPharmacyCore = require(dir + '/PharmacyReport/report-pharmacy-core.js');
const out = { mode };
try { require(dir + '/PharmacyReport/pharmacy-report.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const page = () => global.window.PharmacyReportPage;
const tap = (attrs) => listeners.click({ target: { closest: q => (q === '[data-act]' ? { disabled: false, getAttribute: k => (attrs[k] == null ? null : attrs[k]) } : null) } });
const writes = () => calls.filter(c => c.method !== 'GET');

(async () => {
  await tick(); await tick(); await tick();
  const st = page().state;
  out.stockRows = st.body.pharmacy.stock.map(r => r.gskuId);
  out.salesRows = st.body.pharmacy.sales.map(r => r.gskuId);
  out.validChip = el('ph-chip-valid').innerHTML;
  out.headerSub = el('ph-sub').innerHTML;
  out.generalHtml0 = el('ph-general').innerHTML;

  if (mode === 'submit') {
    // the photo: a gif is refused before any call; a jpeg is posted multipart, its id kept and the draft saved
    const before = calls.length;
    await page().uploadPhoto({ type: 'image/gif', name: 'x.gif' });
    out.gifCalls = calls.length - before;
    out.gifError = el('ph-error-text').textContent;
    await page().uploadPhoto({ type: 'image/jpeg', name: 'shelf.jpg' }); await tick(); await tick();
    const photoCall = calls.find(c => c.url === '/api/PV1/shelf-photo');
    out.photoMultipart = !!(photoCall && photoCall.body && photoCall.body.multipart);
    out.photoContentType = photoCall ? (photoCall.headers['Content-Type'] || null) : 'none';
    out.photoParts = photoCall ? photoCall.body.parts : null;
    out.draftPhoto = ((calls.find(c => c.url === '/api/draft') || {}).body || {}).pharmacy ? calls.find(c => c.url === '/api/draft').body.pharmacy.shelf.photoEvidenceId : null;

    // the answers, except the trend
    tap({ 'data-act': 'met', 'data-val': 'pharmacist' });
    tap({ 'data-act': 'stock', 'data-val': 'in_stock', 'data-gsku': 'G1' });
    tap({ 'data-act': 'comp', 'data-id': 'C1' });
    tap({ 'data-act': 'adv', 'data-idx': '0', 'data-code': 'price' });
    tap({ 'data-act': 'ae', 'data-val': 'no' });
    await page().goStep('order'); await tick();
    out.orderHtml = el('ph-order').innerHTML;
    const b2 = writes().length;
    await page().submit(); await tick();
    out.postsWithoutTrend = writes().slice(b2).filter(c => c.url === '/api/submit').length;
    out.stepAfterTry = st.step;
    out.summaryHtml = el('ph-summary').innerHTML;
    await page().goStep('stock'); await tick();
    out.stockHtml = el('ph-stock').innerHTML;
    await page().goStep('market'); await tick();
    listeners.input({ target: { closest: q => (q === '[data-act]' ? { getAttribute: () => 'contact-q', value: 'Ali' } : null) } });
    await new Promise(r => setTimeout(r, 400)); await tick();
    out.marketHtml = el('ph-market').innerHTML;
    // the trend → the submit goes
    tap({ 'data-act': 'trend', 'data-val': 'up', 'data-gsku': 'G1' });
    out.missingAfterTrend = global.window.ReportPharmacyCore.missing(st.body, { competitorsListed: true });
    await page().submit(); await tick(); await tick();
    const submitCall = calls.find(c => c.url === '/api/submit');
    out.submitBody = submitCall ? submitCall.body : null;
    out.sentShown = !el('ph-sent').classList.contains('d-none');
    out.toasts = toasts.slice();
  } else if (mode === 'readonly') {
    tap({ 'data-act': 'met', 'data-val': 'pharmacist' });
    out.metAfterTap = st.body.general.metRoleCode;
    out.draftHidden = el('ph-draft').classList.contains('d-none');
    out.submitHidden = el('ph-submit').classList.contains('d-none');
    await page().goStep('summary');
    await page().submit(); await tick();
    out.writes = writes().length;
    out.generalHtml = (await (async () => { await page().goStep('general'); return el('ph-general').innerHTML; })());
  } else {
    out.sentShown = !el('ph-sent').classList.contains('d-none');
    el('ph-unlock').l.click(); await tick();
    out.editing = st.editing;
    await page().goStep('summary');
    await page().submit(); await tick(); await tick();
    out.modeAfter409 = st.mode;
    out.fixShown = !el('ph-fix').classList.contains('d-none');
    const b3 = writes().length;
    await page().submit(); await tick();
    out.amendWithoutReason = writes().slice(b3).length;
    out.fixError = el('ph-fix-err').textContent;
    listeners.input({ target: { closest: q => (q === '[data-act]' ? { getAttribute: () => 'fix-why', value: 'Stok yanlış girildi' } : null) } });
    await page().submit(); await tick(); await tick();
    const amendCall = calls.find(c => c.url === '/api/R1/amend');
    out.amendBody = amendCall ? amendCall.body : null;
    out.modeAfterAmend = st.mode;
  }
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
