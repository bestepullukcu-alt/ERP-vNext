/* WP-VW-W4 (W4-WEB-c) - DOM smoke of competitor-pages.js in Node: a fake document, a stub fetch and controlled timers.
   Run by CompetitorProductsWebTests with the script folder as argv[2] and the scenario as argv[3]; prints one JSON object.
   Scenarios: list | readonly | empty | proposals | complete | template | pmerge | merge | edit | conflict | preparer | approver | details | create */
const dir = process.argv[2];
const scenario = process.argv[3] || 'list';
const nodes = {};
Date.now = () => Date.parse('2026-10-15T12:00:00Z');
const mk = id => ({
  id, innerHTML: '', textContent: '', value: '', attrs: {},
  classList: { s: new Set(), add(...c) { c.forEach(x => this.s.add(x)); }, remove(...c) { c.forEach(x => this.s.delete(x)); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; }, addEventListener() {}
});
const el = id => nodes[id] || (nodes[id] = mk(id));
const listeners = {};
const root = el('cp-root');
const modeOf = { list: 'records', readonly: 'records', empty: 'records', proposals: 'proposals', complete: 'complete', template: 'complete', pmerge: 'proposal-merge', merge: 'merge',
  edit: 'edit', conflict: 'edit', preparer: 'proposal', approver: 'proposal', details: 'details', create: 'create' };
const R1 = 'rrrrrrrr-0000-0000-0000-000000000001', R2 = 'rrrrrrrr-0000-0000-0000-000000000002', P1 = 'pppppppp-0000-0000-0000-000000000001', P2 = 'pppppppp-0000-0000-0000-000000000002';
const TUTU = 'aaaaaaaa-0000-0000-0000-00000000000a', UROF = 'aaaaaaaa-0000-0000-0000-00000000000b', IND = 'iiiiiiii-0000-0000-0000-000000000001';
const ME = 'u-marketing', OTHER = 'u-salesops';
Object.assign(root.attrs, {
  'data-mode': modeOf[scenario], 'data-api-base': '/api', 'data-base': '/cp', 'data-id': ['merge', 'edit', 'conflict', 'details'].includes(scenario) ? R2 : '',
  'data-proposal-id': ['complete', 'template', 'pmerge', 'preparer', 'approver'].includes(scenario) ? P1 : '',
  'data-actor': scenario === 'approver' ? OTHER : ME, 'data-can-manage': scenario === 'readonly' ? 'false' : 'true', 'data-can-read-products': 'true',
  'data-can-read-forms': 'true', 'data-can-read-indications': 'true', 'data-can-read-usage': 'true', 'data-can-read-people': 'true'
});
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
el('cp-alert').classList.add('d-none'); el('cp-modal').classList.add('d-none'); // the shell's initial classes
global.document = { documentElement: { lang: 'tr', dir: 'ltr' }, getElementById: el, createElement: () => ({ click() {} }) };
const timeouts = [];
global.setTimeout = (fn, ms) => { timeouts.push({ fn, ms }); return timeouts.length; };
global.clearTimeout = id => { if (id && timeouts[id - 1]) { timeouts[id - 1].fn = null; } };
const flush = () => { const run = timeouts.splice(0); run.forEach(t => t.fn && t.fn()); };
const tick = () => new Promise(r => setImmediate(r));
const settle = async () => { for (let i = 0; i < 14; i++) { await tick(); } };

const record = (id, name, extra) => Object.assign({
  id, name, aliases: [], company: 'Medira İlaç', activeIngredients: ['trimetazidin'], formCode: 'modified-release-tablet', strength: '35 mg', atcCode: 'C01EB15',
  competesWithGlobalProductIds: [TUTU], indicationNodeIds: [IND], status: 'active', mergedIntoId: null, survivor: null,
  countryPrices: [{ marketCode: 'TR', boxPrice: 186.3, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed', validFrom: '2026-07-01T00:00:00Z', source: 'SGK' }],
  countryPriceHistory: [{ marketCode: 'TR', boxPrice: 172.4, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed', validFrom: '2026-01-01T00:00:00Z', validTo: '2026-07-01T00:00:00Z', reason: 'changed' }],
  mergedFrom: [], changeLog: [{ at: '2026-10-08T09:00:00Z', by: OTHER, field: 'countryPrices.TR', oldValue: '172.4 TRY', newValue: '186.3 TRY' }],
  createdBy: ME, createdAt: '2026-09-01T00:00:00Z', updatedBy: OTHER, updatedAt: '2026-10-08T09:00:00Z', version: 3
}, extra || {});
const koronex = record(R1, 'Koronex 100', { aliases: ['Koroneks'], mergedFrom: [{ sourceId: 'x', sourceName: 'Koroneks', proposedBy: 'u-rep', mergedAt: '2026-10-01T00:00:00Z' }] });
const tamsulin = record(R2, 'Tamsulin', { company: 'Atlas Generik', activeIngredients: ['tamsulosin'], formCode: 'modified-release-capsule', strength: '0,4 mg', competesWithGlobalProductIds: [UROF],
  countryPrices: [{ marketCode: 'TR', boxPrice: 186.3, currencyCode: 'TRY', reimbursementStatusCode: 'reimbursed', validFrom: '2026-07-01T00:00:00Z', source: null }] });
const proposal = (id, name, decision) => ({ proposal: record(id, name, { status: 'proposed', company: 'Atlas', countryPrices: [{ marketCode: 'AZ', boxPrice: 9.8, currencyCode: 'AZN', reimbursementStatusCode: 'unknown', validFrom: '2026-10-01T00:00:00Z' }],
  countryPriceHistory: [], proposedBy: 'u-rep', proposedAt: '2026-10-06T00:00:00Z', proposedForProductId: UROF }), similar: [{ id: R2, name: 'Tamsulin', company: 'Atlas Generik', status: 'active', score: 100, likelySame: true }], decision });
const draft = { id: 'd1', proposalId: P2, action: 'merge', targetId: R2, moveMarketCodes: [], status: 'draft', createdBy: ME, updatedBy: ME, version: 1, reviewRounds: [], decisions: [], abilities: { canEdit: true, canSubmit: true, canWithdraw: false, canDecide: false } };
const inReview = { id: 'd0', proposalId: P1, action: 'reject', reason: 'Türkiye pazarında yok', status: 'in_review', createdBy: ME, updatedBy: ME, submittedBy: ME, submittedAt: '2026-10-15T10:00:00Z', version: 2,
  reviewRounds: [{ roundNo: 1, submittedBy: ME, submittedAt: '2026-10-15T10:00:00Z', outcome: null, closedAt: null }], decisions: [],
  abilities: { canEdit: false, canSubmit: false, canWithdraw: scenario === 'preparer', canDecide: true } }; // a STALE canDecide for the preparer: the page must still refuse
const returned = { id: 'd3', proposalId: 'p3', action: 'complete', status: 'returned', createdBy: ME, updatedBy: ME, version: 4,
  reviewRounds: [{ roundNo: 1, submittedBy: ME, outcome: 'rejected', closedAt: '2026-10-14T10:00:00Z', completedBy: OTHER }], decisions: [{ roundNo: 1, by: OTHER, outcome: 'return', comment: 'ATC eksik', at: '2026-10-14T10:00:00Z' }],
  abilities: { canEdit: true, canSubmit: true, canWithdraw: false, canDecide: false } };
const proposals = [proposal(P1, 'Prostavit', null), proposal(P2, 'Tamsulin', draft), proposal('p3', 'koronex', returned), proposal('p4', 'Diaform', Object.assign({}, inReview, { proposalId: 'p4' }))];

const calls = [];
const toasts = [];
let records = scenario === 'empty' ? [] : [koronex, tamsulin];
let putConflict = scenario === 'conflict';
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  const body = o && o.body ? JSON.parse(o.body) : null;
  calls.push({ url, method, body });
  let status = 200, data = null, errors = [];
  const path = url.split('?')[0];
  if (path === '/api/products') data = { items: [{ id: TUTU, canonicalCode: 'TUTU', globalProductName: 'TUTUKON' }, { id: UROF, canonicalCode: 'UROF', globalProductName: 'ÜROFLEKS' }] };
  else if (path === '/api/reference-data/mdm-reimbursement-status/values') data = { items: [{ valueCode: 'reimbursed', displayName: 'Reimbursed', attributes: { label_tr: 'Geri ödemede' } }, { valueCode: 'unknown', displayName: 'Unknown', attributes: { label_tr: 'Bilinmiyor' } }] };
  else if (path === '/api/reference-data/market/values') data = { items: [{ valueCode: 'TR', displayName: 'Türkiye' }, { valueCode: 'AE', displayName: 'UAE' }, { valueCode: 'AZ', displayName: 'Azerbaijan' }] };
  else if (path === '/api/people') data = { people: [{ userId: ME, displayName: 'Elif Kaya' }, { userId: OTHER, displayName: 'Zeynep Koç' }, { userId: 'u-rep', displayName: 'Murat Demir' }] };
  else if (path === '/api/forms') data = { dosageForms: ['tablet', 'modified-release-tablet', 'modified-release-capsule'] };
  else if (path.startsWith('/api/indications/')) data = { productId: TUTU, items: [{ indicationNodeId: IND, indicationName: 'Kronik stabil angina' }], reason: null };
  else if (path === '/api/usage') data = { months: 3, items: [{ competitorProductId: R1, doctorCount: '38', reportCount: '61', byProduct: [{ productId: TUTU, count: '61' }], byCountry: [{ country: 'TR', count: '41' }, { country: 'AZ', count: '<5' }] },
    { competitorProductId: R2, doctorCount: '<5', reportCount: '<5', byProduct: [], byCountry: [] }, { competitorProductId: P1, doctorCount: '<5', reportCount: '4', byProduct: [], byCountry: [] }] };
  else if (path === '/api/records' && method === 'GET') data = { items: records, total: records.length, page: 1, pageSize: 25 };
  else if (path === '/api/records' && method === 'POST') { status = 201; data = 'nnnnnnnn-0000-0000-0000-000000000001'; }
  else if (path === '/api/records/' + R2 && method === 'GET') data = tamsulin;
  else if (path === '/api/records/' + R1 && method === 'GET') data = koronex;
  else if (path === '/api/records/' + R2 && method === 'PUT') { if (putConflict) { status = 409; errors = ['concurrency_conflict', 'The record was modified by another writer; reload and retry.']; } else { data = tamsulin; } }
  else if (path === '/api/records/' + R2 + '/merge') data = koronex;
  else if (path === '/api/records/' + R2 + '/deactivate') data = Object.assign({}, tamsulin, { status: 'inactive' });
  else if (path === '/api/merge-candidates') data = [{ id: R1, name: 'Koronex 100', company: 'Medira İlaç', status: 'active', score: 41, likelySame: false }, { id: R2, name: 'Tamsulin', company: 'Atlas Generik', status: 'active', score: 100, likelySame: true }];
  else if (path === '/api/proposals') data = proposals;
  else if (path === '/api/proposals/' + P1 && method === 'GET') {
    const d = scenario === 'preparer' || scenario === 'approver' ? inReview : scenario === 'complete' || scenario === 'template' || scenario === 'pmerge' ? null : null;
    data = Object.assign({}, proposal(P1, 'Prostavit', d), {});
  }
  else if (path.endsWith('/decision') && method === 'PUT') data = Object.assign({}, draft, { proposalId: P1, action: body.action });
  else if (path.endsWith('/decision/submit')) { if (scenario === 'template') { status = 409; errors = ['workflow_template_missing', 'The approval workflow template is not available (not created or not published).']; } else { data = Object.assign({}, inReview); } }
  else if (path.endsWith('/decision/withdraw')) data = Object.assign({}, inReview, { status: 'returned' });
  else if (path.endsWith('/decision/approve-reject')) data = Object.assign({}, inReview, { status: body.decision === 'approve' ? 'applied' : 'returned' });
  return Promise.resolve({ ok: status < 400, status, text: () => Promise.resolve(JSON.stringify(status < 400 ? { data } : { data, errors })) });
};
global.window = { document: global.document, location: { href: '' }, showToast: (m, t) => toasts.push({ m, t }), addEventListener() {} };
global.window.CompetitorCore = require(dir + '/competitor-core.js');
global.window.CompetitorL10n = {
  Title: 'Rakip ürünler', NewButton: 'Yeni rakip ürün', ReadOnly: 'Salt okuma', TabRecords: 'Rakip ürünler', TabProposals: 'Temsilci önerileri', EmptyTitle: 'Henüz rakip ürün yok',
  Status_active: 'Aktif', Status_proposed: 'Önerilen', Status_inactive: 'Pasif', State_waiting: 'Karar bekliyor', State_draft: 'Taslak karar · {0}', State_in_review: 'Onay bekliyor · {0}, {1}',
  State_returned: 'Taslağa geri gönderildi · {0}', Decision_merge: 'Birleştir', Decision_reject: 'Reddet', Decision_complete: 'Tamamla', SendForApproval: 'Onaya gönder', SaveDecision: 'Kararı kaydet',
  ActComplete: 'Onayla ve tamamla', ActMerge: 'Var olan kayıtla birleştir', ActReject: 'Reddet', ErrReasonRequired: 'Gerekçe zorunlu.', SodNote: 'Bu kararı siz hazırladınız; Görev Merkezi\'nde başka biri onaylar.',
  WithdrawButton: 'Geri çek', ApproveButton: 'Onayla', ReturnToDraft: 'Taslağa geri gönder', ErrNoteRequired: 'Not zorunlu.', MergeButton: 'Birleştir', AckText: 'Etkisini okudum, birleştirmeyi onaylıyorum.',
  Err_workflow_template_missing: 'Onay akışı (MDM-COMP-PROPOSAL) henüz kurulmamış.', ConflictTitle: 'Kayıt başkası tarafından değiştirildi, yenileyin.', RefreshButton: 'Yenile',
  NewPeriod: 'Yeni dönem açılacak: {0} → {1} {2}.', ErrMarkets_duplicate: 'Aynı ülke iki kez eklenemez.', InReports: '{0} raporda', MarketSoon: 'Pazar verisi · yakında',
  UsagePrivacy: 'Doktor kimliği gösterilmez', Field_countryPrices: 'Ülke fiyatı · {0}', HistoryCount: 'Geçmiş ({0})', DifferentTag: 'farklı', DeactivateButton: 'Pasife al',
  Form_modified_release_tablet: 'değiştirilmiş salımlı tablet', PricedText: '{0}/{1} fiyatlı', ActionFailed: 'İşlem başarısız'
};
require(dir + '/competitor-pages.js');

const target = attrs => ({ closest: q => (q === '[data-act]' ? { getAttribute: k => (attrs[k] == null ? null : attrs[k]) } : null) });
const click = async attrs => { await listeners.click({ target: target(attrs) }); await settle(); };
const change = async (attrs, props) => { await listeners.change({ target: Object.assign({ tagName: 'INPUT', value: '', checked: false, getAttribute: k => (attrs[k] == null ? null : attrs[k]) }, props) }); await settle(); };
const input = (attrs, props) => listeners.input({ target: Object.assign({ tagName: 'INPUT', value: '', getAttribute: k => (attrs[k] == null ? null : attrs[k]) }, props) });
const html = () => el('cp-body').innerHTML;
const modal = () => el('cp-modal').innerHTML;
const req = (m, suffix) => calls.filter(c => c.method === m && c.url.split('?')[0].endsWith(suffix));

(async () => {
  await settle();
  const out = { scenario, page: html() };
  try {
    if (scenario === 'list' || scenario === 'readonly' || scenario === 'empty') {
      if (scenario === 'list') {
        await change({ 'data-filter': 'productId' }, { tagName: 'SELECT', value: TUTU });
        out.filterCall = calls.filter(c => c.url.startsWith('/api/records?')).pop().url;
        await click({ 'data-act': 'export' });
      }
      out.calls = calls.map(c => c.method + ' ' + c.url);
    } else if (scenario === 'proposals') {
      await click({ 'data-act': 'reject-open', 'data-id': P1 });
      out.modal = modal();
      await click({ 'data-act': 'decision-send', 'data-kind': 'reject' });
      out.putsWithoutReason = req('PUT', '/decision').length;
      out.modalAfterEmpty = modal();
      input({ 'data-local': 'reason' }, { value: 'Türkiye pazarında bulunmuyor' });
      await click({ 'data-act': 'decision-send', 'data-kind': 'reject' });
      out.rejectBody = (req('PUT', '/decision')[0] || {}).body || null;
      out.submits = req('POST', '/decision/submit').map(c => c.url);
      out.location = global.window.location.href;
    } else if (scenario === 'complete' || scenario === 'template') {
      await click({ 'data-act': 'decision-send', 'data-kind': 'complete' });
      out.putsInvalid = req('PUT', '/decision').length; // no ingredient yet → refused on the page
      out.afterInvalid = html();
      listeners.keydown({ target: { getAttribute: k => (k === 'data-tag' ? 'activeIngredients' : null), value: 'tamsulosin' }, key: 'Enter', preventDefault() {} });
      await settle();
      await click({ 'data-act': 'decision-send', 'data-kind': 'complete' });
      out.completeBody = (req('PUT', '/decision')[0] || {}).body || null;
      out.submits = req('POST', '/decision/submit').length;
      out.alert = el('cp-alert').textContent;
      out.alertShown = !el('cp-alert').classList.contains('d-none');
      out.location = global.window.location.href;
    } else if (scenario === 'pmerge' || scenario === 'merge') {
      out.step1 = html();
      await click({ 'data-act': 'merge-next' });
      out.stepAfterNoTarget = html().indexOf('cp-mstep active" data-step="1"') > -1;
      await click({ 'data-act': 'pick-target', 'data-id': scenario === 'merge' ? R1 : R2 });
      await click({ 'data-act': 'merge-next' });
      out.step2 = html();
      if (scenario === 'pmerge') { await change({ 'data-act-change': 'move', 'data-code': 'AZ' }, { type: 'checkbox', checked: true }); }
      await click({ 'data-act': 'merge-next' });
      out.step3 = html();
      await click({ 'data-act': scenario === 'merge' ? 'merge-do' : 'decision-send', 'data-kind': 'merge' });
      out.callsBeforeAck = req('POST', '/merge').length + req('PUT', '/decision').length;
      await change({ 'data-act-change': 'ack' }, { type: 'checkbox', checked: true });
      out.step3Ack = html();
      await click({ 'data-act': scenario === 'merge' ? 'merge-do' : 'decision-send', 'data-kind': 'merge' });
      out.mergeBody = scenario === 'merge' ? (req('POST', '/merge')[0] || {}).body : (req('PUT', '/decision')[0] || {}).body;
      out.submits = req('POST', '/decision/submit').length;
      out.location = global.window.location.href;
    } else if (scenario === 'edit' || scenario === 'conflict') {
      out.form = html();
      await change({ 'data-row': '0', 'data-k': 'boxPrice' }, { value: '199,90' });
      out.period = html();
      await click({ 'data-act': 'add-open' });
      await click({ 'data-act': 'add-country', 'data-code': 'TR' });
      out.dupError = html().indexOf('Aynı ülke iki kez eklenemez.') > -1;
      await click({ 'data-act': 'add-country', 'data-code': 'AZ' });
      out.rowsAfterAdd = (html().match(/class="cp-crow"/g) || []).length;
      await change({ 'data-row': '1', 'data-k': 'boxPrice' }, { value: '9,80' });
      await change({ 'data-row': '1', 'data-k': 'currencyCode' }, { value: 'azn' });
      await click({ 'data-act': 'save' });
      out.putBody = (req('PUT', '/records/' + R2)[0] || {}).body || null;
      out.afterSave = html();
      out.location = global.window.location.href;
      if (scenario === 'conflict') {
        const before = calls.length;
        await click({ 'data-act': 'refresh' });
        out.refreshCalls = calls.slice(before).map(c => c.method + ' ' + c.url);
        out.afterRefresh = html();
      }
    } else if (scenario === 'preparer' || scenario === 'approver') {
      if (scenario === 'preparer') {
        await click({ 'data-act': 'approve' });
        out.approveCalls = req('POST', '/approve-reject').length; // the stale ability never decides for the preparer
        await click({ 'data-act': 'withdraw' });
        out.withdraws = req('POST', '/decision/withdraw').length;
      } else {
        await click({ 'data-act': 'return-open' });
        await click({ 'data-act': 'return-do' });
        out.returnWithoutNote = req('POST', '/approve-reject').length;
        out.modalError = modal();
        input({ 'data-local': 'note' }, { value: 'Gerekçe belirsiz' });
        await click({ 'data-act': 'return-do' });
        out.returnBody = (req('POST', '/approve-reject')[0] || {}).body || null;
      }
    } else if (scenario === 'details') {
      await click({ 'data-act': 'hist', 'data-code': 'TR' });
      out.withHistory = html();
      await click({ 'data-act': 'deactivate-open' });
      out.modal = modal();
      await click({ 'data-act': 'deactivate-do' });
      out.deactivate = (req('POST', '/deactivate')[0] || {}).body || null;
    } else if (scenario === 'create') {
      input({ 'data-form': 'name' }, { value: 'Valsen Plus' });
      input({ 'data-form': 'company' }, { value: 'Selen İlaç' });
      listeners.keydown({ target: { getAttribute: k => (k === 'data-tag' ? 'activeIngredients' : null), value: 'valsartan' }, key: 'Enter', preventDefault() {} });
      await settle();
      await click({ 'data-act': 'toggle-product', 'data-id': TUTU });
      out.indications = html().indexOf('Kronik stabil angina') > -1;
      await click({ 'data-act': 'toggle-indication', 'data-id': IND });
      await click({ 'data-act': 'add-open' });
      await click({ 'data-act': 'add-country', 'data-code': 'AE' });
      await change({ 'data-row': '0', 'data-k': 'boxPrice' }, { value: '42' });
      out.preview = html();
      await click({ 'data-act': 'save' });
      out.postBody = (req('POST', '/records')[0] || {}).body || null;
      out.createError = (toasts.find(t => t.t === 'error') || {}).m || (el('cp-alert').classList.contains('d-none') ? null : el('cp-alert').textContent);
      out.location = global.window.location.href;
    }
    out.toasts = toasts.map(t => t.m);
    out.final = html();
  } catch (e) { out.crash = String(e && e.stack || e); }
  process.stdout.write(JSON.stringify(out));
})();
