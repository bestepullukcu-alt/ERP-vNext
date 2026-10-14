/* WP-VW-W3 (W3-WEB-c) - DOM smoke of the objection library pages and the studio's "Sunum notları" tab in Node: a fake
   document, a stub fetch. Run by ObjectionLibraryWebTests with the CRM script folder as argv[2] and the scenario as argv[3]
   (list | create | submit | return | retire | studio); prints one JSON object. */
const dir = process.argv[2];
const scenario = process.argv[3] || 'list';
const nodes = {};
const listeners = { doc: {}, win: {} };
const mk = id => {
  const node = {
    id, innerHTML: '', textContent: '', value: '', checked: false, disabled: false, readOnly: false, dataset: {}, style: {},
    attrs: {}, options: [], selectedOptions: [], children: {},
    classList: { s: new Set(), add(...c) { c.forEach(x => this.s.add(x)); }, remove(...c) { c.forEach(x => this.s.delete(x)); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
    setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
    addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; },
    querySelector(sel) { return this.children[sel] || (this.children[sel] = mk(id + ' ' + sel)); },
    querySelectorAll() { return []; }, insertAdjacentHTML(pos, html) { this.innerHTML += html; }
  };
  Object.defineProperty(node, 'className', { get() { return Array.from(node.classList.s).join(' '); }, set(v) { node.classList.s = new Set(String(v).split(/\s+/).filter(Boolean)); } });
  return node;
};
const el = id => nodes[id] || (nodes[id] = mk(id));
const shown = id => !el(id).classList.contains('d-none');

const ID = '11111111-1111-1111-1111-111111111111';
const NEW_ID = '22222222-2222-2222-2222-222222222222';
const P1 = 'aaaaaaaa-0000-0000-0000-00000000000a';
const P2 = 'bbbbbbbb-0000-0000-0000-00000000000b';
const C1 = 'cccccccc-0000-0000-0000-000000000001', C2 = 'cccccccc-0000-0000-0000-000000000002', C3 = 'cccccccc-0000-0000-0000-000000000003';
const O1 = 'dddddddd-0000-0000-0000-000000000001', O2 = 'dddddddd-0000-0000-0000-000000000002', O3 = 'dddddddd-0000-0000-0000-000000000003';

const base = status => ({
  id: ID, productId: P1, productName: 'ÜROFLEKS', productForm: null, categoryCode: 'price', question: 'Fiyatı yüksek.',
  answer: 'Geri ödeme kapsamında.', claimIds: [C1], status, createdBy: 'u-author', createdAt: '2026-10-09T11:05:00Z',
  updatedBy: 'u-author', updatedAt: '2026-10-10T09:18:00Z', version: 3, usageCount: 2, reviewRounds: [], decisions: [],
  displayNames: { 'u-author': 'Elif Kaya', 'u-approver': 'Dr. Murat Aydın' },
  abilities: { canEdit: false, canSubmit: false, canWithdraw: false, canDecide: false, canRetire: false, canCopy: false }
});
const records = {
  submit: Object.assign(base('draft'), { abilities: { canEdit: true, canSubmit: true, canWithdraw: false, canDecide: false, canRetire: true, canCopy: true } }),
  afterSubmit: Object.assign(base('in_review'), { submittedBy: 'u-author', submittedAt: '2026-10-10T09:18:00Z',
    reviewRounds: [{ roundNo: 1, submittedBy: 'u-author', submittedAt: '2026-10-10T09:18:00Z', outcome: null, closedAt: null }],
    abilities: { canEdit: false, canSubmit: false, canWithdraw: true, canDecide: false, canRetire: false, canCopy: false } }),
  return: Object.assign(base('in_review'), { submittedBy: 'u-author', submittedAt: '2026-10-10T09:18:00Z',
    reviewRounds: [{ roundNo: 1, submittedBy: 'u-author', submittedAt: '2026-10-10T09:18:00Z', outcome: null, closedAt: null }],
    abilities: { canEdit: false, canSubmit: false, canWithdraw: false, canDecide: true, canRetire: false, canCopy: false } }),
  retire: Object.assign(base('approved'), { approvedBy: 'u-approver', approvedAt: '2026-09-19T16:42:00Z',
    abilities: { canEdit: false, canSubmit: false, canWithdraw: false, canDecide: false, canRetire: true, canCopy: true } })
};
let current = records[scenario] || null;

const calls = [];
const respond = (status, body) => Promise.resolve({ ok: status >= 200 && status < 300, status, json: () => Promise.resolve(body), text: () => Promise.resolve(JSON.stringify(body)) });
const fetchStub = (url, o) => {
  const method = (o && o.method) || 'GET';
  const body = o && o.body ? JSON.parse(o.body) : undefined;
  calls.push({ url, method, body });
  if (url.endsWith('/lookups/categories')) return respond(200, { data: [{ code: 'price', label: 'Fiyat' }, { code: 'side_effect', label: 'Yan etki' }] });
  if (url.endsWith('/lookups/products')) return respond(200, { data: [{ id: P1, code: 'URO', name: 'ÜROFLEKS' }, { id: P2, code: 'TUT', name: 'TUTUKON' }] });
  if (url.includes('/lookups/claims')) return respond(200, { data: [
    { id: C1, code: 'URF-CL-01', name: 'Klinik', text: '12 hafta IPSS', status: 'approved', productId: P1, isArchived: false },
    { id: C2, code: 'URF-GO-01', name: 'SUT', text: 'Geri ödeme', status: 'approved', productId: P1, isArchived: false },
    { id: C3, code: 'URF-OLD', name: 'Eski', text: 'Taslak iddia', status: 'draft', productId: P1, isArchived: false }] });
  if (url.endsWith('/usages')) return respond(200, { data: [
    { pathId: 'p1', pathName: 'ÜROFLEKS · BPH', pathVersion: '3', pathStatus: 'published', stepId: 's1', stepOrder: 2, stepTitle: 'Klinik kanıt' },
    { pathId: 'p2', pathName: 'ÜROFLEKS · BPH', pathVersion: '4', pathStatus: 'draft', stepId: 's2', stepOrder: 2, stepTitle: 'Klinik kanıt' }] });
  if (method === 'POST' && url.endsWith('/objection-responses')) return respond(200, { data: NEW_ID });
  if (method === 'POST' && url.endsWith('/submit')) { current = records.afterSubmit; return respond(200, { data: current }); }
  if (method === 'POST' && url.endsWith('/decision')) return respond(200, { data: current });
  if (method === 'POST' && url.endsWith('/retire')) return respond(204, null);
  if (url.includes('/lookups/path-objections')) return respond(200, { disabled: false, reason: null, productId: P1, options: [
    { id: O1, productId: P1, status: 'approved', categoryCode: 'price', categoryLabel: 'Fiyat', question: 'Pahalı', answer: 'Geri ödeme var', claimCount: 1 },
    { id: O2, productId: P1, status: 'approved', categoryCode: 'side_effect', categoryLabel: 'Yan etki', question: 'Hipotansiyon?', answer: 'Nadir', claimCount: 2 },
    { id: O3, productId: P1, status: 'retired', categoryCode: 'price', categoryLabel: 'Fiyat', question: 'Eski', answer: 'x', claimCount: 0 }] });
  if (method === 'PUT' && url.includes('/steps/')) return respond(200, { data: true });
  if (url.includes('/objection-responses/') && method === 'GET') return respond(200, { data: current });
  return respond(200, { data: null });
};

global.document = {
  documentElement: { lang: 'tr', dir: 'ltr' }, getElementById: el,
  querySelector: sel => (sel === '.datatables-objectionresponses' ? el('dt-objectionresponses') : null),
  querySelectorAll: () => [], addEventListener: (ev, fn) => { listeners.doc[ev] = fn; }, createElement: () => mk('x')
};
const jq = { handlers: {} };
global.window = {
  document: global.document, location: { href: '' }, fetch: fetchStub,
  addEventListener: (ev, fn) => { listeners.win[ev] = fn; }, showToast: () => {}, confirm: () => true,
  showConfirm: (text, go) => go(), open: (url, target) => { global.window.opened = [url, target]; }
};
global.fetch = fetchStub;
global.window.ObjectionCore = require(dir + '/ObjectionResponses/objection-core.js');
global.window.ObjectionL10n = {
  Status_draft: 'Taslak', Status_in_review: 'Onay bekliyor', Status_approved: 'Onaylı', Status_retired: 'Emekli',
  PendingLine: 'Onay bekliyor · {0}, {1} tarihinde onaya gönderdi.', ApprovedNote: 'Onaylı yanıt değiştirilemez.',
  RetireUsed: '{0} adımda kullanılıyor.', ClaimsSelected: '{0} seçili (en çok {1})', UnknownUser: 'Bilinmeyen',
  UsageCount: '{0} adım', UsageStepLine: '{0} · {1}', EventLine: '{0} · {1}', Saved: 'Kaydedildi', ActionFailed: 'Hata'
};
const tick = () => new Promise(r => setImmediate(r));
const settle = async () => { for (let i = 0; i < 12; i++) { await tick(); } };
const alertText = () => (shown('orAlert') ? el('orAlert').textContent : null);

(async () => {
  const out = { scenario };

  if (scenario === 'list') {
    let options = null;
    global.window.jQuery = target => ({ on: (ev, fn) => { jq.handlers[ev] = fn; return this; } });
    global.window.DitenDataTable = { createList: async o => { options = o; return {}; }, renderActions: a => a.map(x => x.key).join(',') };
    Object.assign(el('orListRoot').dataset, { apiBase: '/api', canManage: 'true', canReadProducts: 'true' });
    require(dir + '/ObjectionResponses/objection-list.js');
    await listeners.doc.DOMContentLoaded();
    const fills = {};
    await options.filters.loadOptions({ fillSelect: (select, rows) => { fills[select.id] = rows.map(r => r.value); } });
    out.ajax = options.ajax.url;
    out.dataMode = options.dataMode;
    out.columns = options.config.columns.map(c => c.data);
    out.filterKeys = options.filters.fields.map(f => f.key);
    out.fills = fills;
    const row = Object.assign(base('in_review'), { abilities: { canEdit: false } });
    const defs = options.config.columnDefs;
    out.category = defs[2].render('price', 'display', row);
    out.status = defs[6].render('in_review', 'display', row);
    out.claims = defs[4].render([C1, C2], 'sort', row);
    out.actionsReadOnly = defs[defs.length - 1].render(null, 'display', row);
    out.actionsDraft = defs[defs.length - 1].render(null, 'display', Object.assign(base('draft'), { abilities: { canEdit: true } }));
    jq.handlers['xhr.dt']({}, {}, { recordsTotal: 0, recordsFiltered: 0, data: [] });
    out.emptyWhenNone = [shown('orEmpty'), shown('orTable')];
    jq.handlers['xhr.dt']({}, {}, { recordsTotal: 11, recordsFiltered: 0, data: [] });
    out.tableWhenFilteredEmpty = [shown('orEmpty'), shown('orTable')];
  }

  if (scenario === 'create') {
    Object.assign(el('orEditRoot').dataset, { apiBase: '/api', id: '', productId: P1, canSubmit: 'true', canReadClaims: 'true', canReadProducts: 'true' });
    require(dir + '/ObjectionResponses/objection-common.js');
    require(dir + '/ObjectionResponses/objection-edit.js');
    await settle();
    el('orProduct').value = P1;
    el('orCategory').value = 'price';
    el('orQuestion').value = '  Fiyatı yüksek.  ';
    el('orAnswer').value = '';
    const st = global.window.ObjectionEdit.state;
    out.offered = global.window.ObjectionCore.claimOptions(st.claims, st.productId).map(c => c.id);
    st.selected = [C1];
    await el('orSaveDraft').l.click();
    await settle();
    const post = calls.find(c => c.method === 'POST' && c.url === '/api/objection-responses');
    out.createBody = post && post.body;
    out.createError = alertText();
    out.location = global.window.location.href;
    out.noApproveButtonInEditor = !nodes.orApprove;
  }

  if (scenario === 'submit' || scenario === 'return' || scenario === 'retire') {
    Object.assign(el('orDetailsRoot').dataset, { apiBase: '/api', id: ID, canReadClaims: 'true' });
    require(dir + '/ObjectionResponses/objection-common.js');
    require(dir + '/ObjectionResponses/objection-details.js');
    await settle();
    const act = global.window.ObjectionDetails.act;
    out.actionsHtml = el('orActions').innerHTML.match(/data-action="[a-z]+"/g) || [];
    if (scenario === 'submit') {
      act('submit');
      await settle();
      out.submitCalls = calls.filter(c => c.method === 'POST' && c.url.endsWith('/submit')).length;
      out.bandAfter = el('orBandText').textContent;
      out.statusAfter = el('orStatusChips').innerHTML;
      out.actionsAfter = el('orActions').innerHTML.match(/data-action="[a-z]+"/g) || [];
      out.sodHint = shown('orSodHint');
    }
    if (scenario === 'return') {
      act('return');
      out.modalOpen = el('orReturnModal').classList.contains('show');
      out.confirmDisabledEmpty = el('orReturnConfirm').disabled;
      el('orReturnNote').value = '   ';
      el('orReturnNote').l.input();
      out.confirmDisabledBlank = el('orReturnConfirm').disabled;
      el('orReturnConfirm').l.click();
      await settle();
      out.decisionCallsAfterBlank = calls.filter(c => c.url.endsWith('/decision')).length;
      el('orReturnNote').value = '  Kaynak eksik  ';
      el('orReturnNote').l.input();
      out.confirmDisabledNote = el('orReturnConfirm').disabled;
      el('orReturnConfirm').l.click();
      await settle();
      out.decision = (calls.find(c => c.url.endsWith('/decision')) || {}).body;
    }
    if (scenario === 'retire') {
      act('retire');
      out.retireOpen = el('orRetireModal').classList.contains('show');
      out.retireUsed = [el('orRetireUsed').textContent, shown('orRetireUsed')];
      el('orRetireConfirm').l.click();
      await settle();
      out.retire = (calls.find(c => c.url.endsWith('/retire')) || {}).body;
      out.usages = el('orUsages').innerHTML.includes('/CRM/KnowledgePaths/p1');
    }
    out.loadError = alertText();
  }

  if (scenario === 'studio') {
    Object.assign(el('kpStudioRoot').dataset, { pathId: 'path1' });
    const strings = { PnBar: 'Sunum notları: {0} / {1} adım dolu', PnObjections: 'Beklenen itirazlar · {0} / {1}', PnStepOf: 'Adım {0} / {1}', PnTargetHint: 'Boşsa {0} sn' };
    let reloaded = 0;
    const put = [];
    global.window.KpStudio = {
      t: (k, ...a) => (strings[k] || k).replace(/\{(\d)\}/g, (m, i) => a[i]),
      esc: v => String(v == null ? '' : v),
      api: { get: u => fetchStub('/CRM/KnowledgePaths/api' + u).then(r => r.json()), put: (u, b) => { put.push({ u, b }); return Promise.resolve({}); } },
      showAlert: (e, err) => { e.textContent = (err && err.message) || 'x'; e.classList.remove('d-none'); }, hideAlert: e => e.classList.add('d-none')
    };
    global.window.KpWorkspace = { reload: async () => { reloaded++; } };
    require(dir + '/KnowledgePaths/presentation-notes.js');
    const item = (id, order, presentation, extra) => Object.assign({ kind: 'content', stepId: id, stepOrder: order, stepCode: 'S-' + id, stepTitle: 'T' + id,
      stepType: 'core-message', contentId: 'c' + id, isRequired: true, versionPinPolicy: 'latest', completionRule: 'viewed', durationMinutes: 2,
      notes: null, branchConditions: [], position: 0, title: 'Adım ' + id, presentation }, extra || {});
    const model = {
      canEdit: true, canReadObjections: true, canManageObjections: true, isLegacyUnapproved: false,
      context: { productName: 'ÜROFLEKS', productId: P1 },
      branches: [{ code: 'main', slots: [
        { chainStepId: 'k1', name: 'Giriş', items: [item('s1', 10, { keyMessage: 'Anahtar', talkTrack: 'Metin', targetSeconds: 75, transition: null, objectionResponseIds: [O1, O3] })] },
        { chainStepId: 'k2', name: 'Kapanış', items: [item('s2', 20, null), { kind: 'claim', claimId: 'cl1' }] }] }]
    };
    listeners.doc['kp:workspace']({ detail: model });
    out.bar = el('kpNotesBarText').textContent;
    out.barMissing = (el('kpNotesBarMissing').innerHTML.match(/data-step-id="([^"]+)"/g) || []);
    await listeners.doc.click({ target: { closest: sel => (sel === '.js-notes-open' ? { dataset: { stepId: 's1' } } : null) } });
    await settle();
    out.objectionsTitle = el('pnObjectionsTitle').textContent;
    out.unusableShown = el('pnObjectionList').innerHTML.includes(O3);
    el('pnTalkTrack').value = '  Yeni metin  ';
    el('pnTarget').value = '';
    el('pnTalkTrack').l.input();
    await el('pnSave').l.click();
    await settle();
    out.put = put[0] && { url: put[0].u, presentation: put[0].b.presentation, keys: Object.keys(put[0].b.presentation), arrangement: put[0].b.arrangement, stepCode: put[0].b.stepCode };
    out.reloaded = reloaded;
    out.studioError = shown('pnAlert') ? el('pnAlert').textContent : null;
  }

  out.calls = calls.map(c => c.method + ' ' + c.url);
  process.stdout.write(JSON.stringify(out));
})().catch(e => { process.stderr.write(String(e && e.stack || e)); process.exit(1); });
