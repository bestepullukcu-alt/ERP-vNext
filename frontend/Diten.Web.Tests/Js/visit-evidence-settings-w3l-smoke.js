/* WP-VW-W3L (W3-WEB-e) - DOM smoke of visit-evidence-settings.js with the scope layers, in Node: a fake document, a stub
   fetch (the proxy's /policy/scopes*, /scope-options/*, effective, roster), stub bootstrap and stub tables (the options the
   page hands settings-tables.js are captured, so their query / cells are called as the real tables would).
   Run by VwW3LScopeWebTests with the CRM script folder as argv[2], a JSON file of the Turkish script texts (the real resx)
   as argv[3] and the mode as argv[4] ('manage' | 'readonly'); prints one JSON object. */
const fs = require('fs');
const dir = process.argv[2];
const texts = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const mode = process.argv[4] || 'manage';
const ME = 'user-me';
const LE = 'aaaaaaaa-1111-2222-3333-444444444444';
const LE2 = 'bbbbbbbb-1111-2222-3333-444444444444';
const nodes = {};
const listeners = {};
const docListeners = {};
const mk = id => ({ id, innerHTML: '', textContent: '', value: '', disabled: false, min: '', dataset: {}, style: {}, attrs: {}, className: '',
  classList: { s: new Set(), add(c) { this.s.add(c); }, remove(c) { this.s.delete(c); }, toggle(c, on) { if (on === undefined ? !this.s.has(c) : on) this.s.add(c); else this.s.delete(c); }, contains(c) { return this.s.has(c); } },
  setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; },
  addEventListener(ev, fn) { (this.l = this.l || {})[ev] = fn; }, closest() { return null; },
  kids: [], appendChild(c) { this.kids.push(c); c.parent = this; return c; }, remove() { if (this.parent) { this.parent.kids = this.parent.kids.filter(k => k !== this); } },
  querySelector() { return null; } });
const el = id => nodes[id] || (nodes[id] = mk(id));
const root = el('ve-root');
Object.assign(root.attrs, { 'data-api-base': '/api', 'data-me': ME,
  'data-can-manage-policy': mode === 'manage' ? 'true' : 'false', 'data-can-read-exceptions': 'true', 'data-can-manage-exceptions': mode === 'manage' ? 'true' : 'false' });
root.addEventListener = (ev, fn) => { listeners[ev] = fn; };
['ve-save-bar', 've-scope-remove', 've-inherit-note', 've-layer-empty', 've-effective', 've-eff-chain-box', 've-add-none'].forEach(id => el(id).classList.add('d-none'));
global.document = { documentElement: { lang: 'tr' }, getElementById: el, querySelectorAll: () => [], addEventListener: (ev, fn) => { docListeners[ev] = fn; }, createElement: () => mk('x') };

const policy = { locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true,
  durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', qrMode: 'off', antiFraudMode: 'off',
  isDefault: false, version: 4, updatedBy: ME, updatedAt: '2026-10-12T09:00:00Z', repCount: 148 };
const FIELDS = ['locationMode', 'radiusMeters', 'maxAccuracyMeters', 'photoMode', 'doctorConfirmation', 'durationConsistency', 'shortVisitSeconds', 'accountWithoutLocation', 'qrMode', 'antiFraudMode'];
const detail = (type, ref, version, set) => ({ scopeType: type, scopeRef: ref, status: 'active', version, updatedAt: '2026-10-15T08:00:00Z', updatedBy: ME,
  fields: FIELDS.map(f => ({ field: f, value: set[f] === undefined ? null : set[f] })) });
const leDetail = detail('legal-entity', LE, 3, { photoMode: 'required', radiusMeters: '500' });
const buDetail = detail('business-unit', 'HOSP', 1, { qrMode: 'required' });
const scopes = { items: [
  { scopeType: 'tenant', scopeRef: null, overriddenFields: 10, version: 4, updatedAt: '2026-10-12T09:00:00Z', updatedBy: ME, repCount: 148, repCountNote: null, isDefault: false },
  { scopeType: 'business-unit', scopeRef: 'HOSP', overriddenFields: 1, version: 1, updatedAt: '2026-10-15T08:00:00Z', updatedBy: ME, repCount: 2, repCountNote: null },
  { scopeType: 'legal-entity', scopeRef: LE, overriddenFields: 2, version: 3, updatedAt: '2026-10-15T08:00:00Z', updatedBy: ME, repCount: 5, repCountNote: null }
], totalReps: 148, displayNames: { [ME]: 'Ali Tufan' } };
const effective = { resourceId: 'rep-1', activeException: null, relaxedRules: [],
  // every field (the CRM answers all ten): two from the legal entity layer, the rest from the tenant
  fields: FIELDS.map(f => f === 'photoMode' ? { field: f, value: 'required', source: 'legal-entity:' + LE }
    : f === 'radiusMeters' ? { field: f, value: '500', source: 'legal-entity:' + LE }
    : { field: f, value: String(policy[f]), source: 'tenant' }),
  scopeChain: { legalEntity: LE, businessUnit: null, note: 'bu-ambiguous', legalEntityNote: 'resolved', businessUnitNote: 'bu-ambiguous' } };
const calls = [];
global.fetch = (url, o) => {
  const method = (o && o.method) || 'GET';
  calls.push({ url, method, body: o && o.body ? JSON.parse(o.body) : null });
  let d = null, status = 200;
  if (url === '/api/policy' && method === 'GET') d = policy;
  else if (url === '/api/policy/scopes') d = scopes;
  else if (url === '/api/policy/scopes/legal-entity/' + LE && method === 'GET') d = leDetail;
  else if (url === '/api/policy/scopes/business-unit/HOSP' && method === 'GET') d = buDetail;
  else if (url.indexOf('/api/policy/scopes/') === 0 && method === 'PUT') { const b = JSON.parse(o.body); d = detail(url.split('/')[4], decodeURIComponent(url.split('/')[5]), (b.expectedVersion || 0) + 1, {}); }
  else if (url.indexOf('/api/policy/scopes/') === 0 && method === 'DELETE') d = Object.assign({}, leDetail, { status: 'archived', version: 4 });
  else if (url === '/api/scope-options/legal-entities') d = { available: true, items: [{ ref: LE, name: 'Diten İlaç A.Ş.' }, { ref: LE2, name: 'Diten Uzbekistan LLC' }] };
  else if (url === '/api/scope-options/business-units') d = { available: true, items: [{ ref: 'HOSP', name: 'Hastane Ekibi' }, { ref: 'KAM', name: 'KAM Ekibi' }] };
  else if (url.indexOf('/api/policy/reps') === 0) d = [{ resourceId: 'rep-1', displayName: 'Okan Tekin' }];
  else if (url.indexOf('/api/policy/effective') === 0) d = effective;
  else if (url === '/api/exceptions' && method === 'GET') d = { items: [], statusCounts: {}, phaseCounts: {} };
  return Promise.resolve({ ok: status < 300, status, text: () => Promise.resolve(JSON.stringify({ data: d })) });
};
const shown = [], hidden = [], toasts = [], tables = {};
global.window = { document: global.document, showToast: m => toasts.push(m),
  VisitEvidenceSettingsTables: {
    history: o => { tables.history = o; let reloads = 0; return { reload() { reloads++; tables.historyReloads = reloads; }, table: null }; },
    reps: o => { tables.reps = o; return { reload() {}, redraw() {}, table: null }; }
  },
  bootstrap: { Modal: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) },
    Offcanvas: { getOrCreateInstance: m => ({ show() { shown.push(m.id); }, hide() { hidden.push(m.id); } }) } } };
require(dir + '/VisitPlanning/format.js');
global.window.VisitEvidenceCore = require(dir + '/VisitEvidenceSettings/evidence-core.js');
global.window.VisitEvidenceSettingsL10n = texts;
const out = { mode };
try { require(dir + '/VisitEvidenceSettings/visit-evidence-settings.js'); out.createError = null; } catch (e) { out.createError = String(e && e.stack || e); }
const tick = () => new Promise(r => setTimeout(r, 15));
const settle = async () => { for (let i = 0; i < 6; i++) { await tick(); } };
const target = map => ({ closest: q => (map[q] ? Object.assign({ disabled: false, getAttribute: k => map[q][k] }, map[q].props || {}) : null) });
const click = map => listeners.click({ target: target(map), preventDefault() {} });
const visible = id => !el(id).classList.contains('d-none');

(async () => {
  await settle();
  // C1 — the tree from GET scopes, the names from the two lists
  out.tree = el('ve-tree-layers').innerHTML;
  out.historyOptions = el('ve-history-scope').innerHTML;

  // C2 — the legal entity selected: its form (2 overrides; the rest from above)
  click({ '[data-scope]': { 'data-scope': 'legal-entity:' + LE } }); await settle();
  out.leName = el('ve-scope-name').innerHTML;
  out.leType = el('ve-scope-type').textContent;
  out.leMeta = el('ve-scope-meta').textContent;
  out.leGroups = el('ve-groups').innerHTML;
  out.leExtra = el('ve-extra').innerHTML;
  out.leInheritNote = el('ve-inherit-text').textContent;
  out.leInheritShown = visible('ve-inherit-note');
  out.removeShown = visible('ve-scope-remove');
  out.pvPhoto = el('ve-pv-photo').textContent; // the preview: the scope's effective values (photo required here)
  out.historyQueryLe = tables.history ? null : 'not-built';

  // "Back to the setting above" on the radius (→ null), "Change in this scope" on the doctor confirmation (takes its value) + switch it off
  click({ '[data-drop]': { 'data-drop': 'radiusMeters' } });
  click({ '[data-take]': { 'data-take': 'doctorConfirmation' } });
  click({ '[data-switch]': { 'data-switch': 'doctorConfirmation' } });
  out.saveEnabled = !el('ve-save').disabled;
  if (el('ve-save').l) { el('ve-save').l.click(); }
  out.saveModalShown = shown.includes('ve-save-modal');
  out.saveText = el('ve-save-text').textContent;
  out.saveDiff = el('ve-save-diff').innerHTML;
  if (out.saveModalShown && el('ve-save-confirm').l) { el('ve-save-confirm').l.click(); await settle(); }
  const put = calls.find(c => c.method === 'PUT');
  out.putUrl = put ? put.url : null;
  out.put = put ? put.body : null;

  // the history: its pane builds the table; the filter follows the selected scope; "every scope" reads them all
  if (docListeners['diten:tab-change']) { docListeners['diten:tab-change']({ detail: { key: 'history' } }); }
  out.historyQuerySelected = tables.history ? tables.history.query() : null;
  el('ve-history-scope').l && el('ve-history-scope').l.change({ target: { value: '' } });
  out.historyQueryAll = tables.history ? tables.history.query() : null;
  out.historyScopeCell = tables.history ? tables.history.cells.scope({ scope: 'business-unit', scopeRef: 'HOSP' }, 'display') : null;
  out.historyChangeCleared = tables.history ? tables.history.cells.change({ scope: 'legal-entity', scopeRef: LE, field: 'radiusMeters', oldValue: '500', newValue: null, kind: 'cleared' }) : null;
  out.historyChangeArchived = tables.history ? tables.history.cells.change({ scope: 'legal-entity', scopeRef: LE, field: null, kind: 'archived' }) : null;

  // the reps: the chip of the applied scope(s) by name, the cells over the layers
  if (docListeners['diten:tab-change']) { docListeners['diten:tab-change']({ detail: { key: 'reps' } }); }
  const rep = { resourceId: 'rep-1', displayName: 'Okan Tekin', scopeChain: { legalEntity: LE, businessUnit: 'HOSP', note: 'resolved', legalEntityNote: 'resolved', businessUnitNote: 'resolved' } };
  const lone = { resourceId: 'rep-2', displayName: 'Ayşe Demir', scopeChain: { legalEntity: null, businessUnit: null, note: 'no-seat,bu-ambiguous', legalEntityNote: 'no-seat', businessUnitNote: 'bu-ambiguous' } };
  out.repChip = tables.reps ? tables.reps.cells.scope(rep, 'display') : null;
  out.loneChip = tables.reps ? tables.reps.cells.scope(lone, 'display') : null;
  out.repPhoto = tables.reps ? tables.reps.cells.value(rep, 'photoMode', 'display') : null;
  out.repQr = tables.reps ? tables.reps.cells.value(rep, 'qrMode', 'display') : null;
  out.lonePhoto = tables.reps ? tables.reps.cells.value(lone, 'photoMode', 'display') : null;

  // C1 — a rep's effective rules: the source by NAME, the chain and its note
  el('ve-look').l.change({ target: { value: 'rep-1' } }); await settle();
  out.effRows = el('ve-eff-rows').innerHTML;
  out.effChain = el('ve-eff-chain').innerHTML;
  out.effNotes = el('ve-eff-notes').innerHTML;
  out.effChainShown = visible('ve-eff-chain-box');
  el('ve-eff-close').l.click(); await settle();

  // "Add legal entity": the pick lists only the entities without a layer; it opens an EMPTY scope (nothing written)
  const putsBeforeAdd = calls.filter(c => c.method === 'PUT').length;
  click({ '[data-add]': { 'data-add': 'legal-entity' } });
  out.addShown = shown.includes('ve-add-modal');
  out.addTitle = el('ve-add-title').textContent;
  out.addOptions = el('ve-add-pick').innerHTML;
  el('ve-add-pick').value = LE2;
  el('ve-add-pick').l.change({ target: { value: LE2 } });
  out.addConfirmEnabled = !el('ve-add-confirm').disabled;
  el('ve-add-confirm').l.click(); await settle();
  out.putsAfterPick = calls.filter(c => c.method === 'PUT').length - putsBeforeAdd;
  out.newName = el('ve-scope-name').innerHTML;
  out.newTree = el('ve-tree-layers').innerHTML;
  out.newNote = el('ve-inherit-text').textContent;
  out.newSaveEnabledEmpty = !el('ve-save').disabled;
  out.newEmptyHint = visible('ve-layer-empty') ? el('ve-layer-empty-text').textContent : null;
  click({ '[data-take]': { 'data-take': 'photoMode' } });
  click({ 'button[data-opt]': { 'data-field': 'photoMode', 'data-opt': 'required' } });
  out.newSaveEnabled = !el('ve-save').disabled;
  if (el('ve-save').l) { el('ve-save').l.click(); }
  out.newSaveText = el('ve-save-text').textContent;
  if (el('ve-save-confirm').l) { el('ve-save-confirm').l.click(); await settle(); }
  const created = calls.filter(c => c.method === 'PUT').slice(-1)[0];
  out.newPutUrl = created ? created.url : null;
  out.newPut = created ? created.body : null;

  // "Remove the scope": the confirm says the reps return above; the DELETE carries the version
  click({ '[data-scope]': { 'data-scope': 'legal-entity:' + LE } }); await settle();
  if (el('ve-scope-remove').l) { el('ve-scope-remove').l.click(); }
  out.rmShown = shown.includes('ve-rm-modal');
  out.rmText = el('ve-rm-text').textContent;
  if (el('ve-rm-confirm').l) { el('ve-rm-confirm').l.click(); await settle(); }
  const del = calls.find(c => c.method === 'DELETE');
  out.deleteUrl = del ? del.url : null;
  out.toasts = toasts.slice();
  out.selAfterRemove = el('ve-scope-name').innerHTML;

  // the business unit selected (screen 7): its own note (an empty field comes from the rep's legal entity, else the tenant)
  click({ '[data-scope]': { 'data-scope': 'business-unit:HOSP' } }); await settle();
  out.buType = el('ve-scope-type').textContent;
  out.buNote = el('ve-inherit-text').textContent;
  out.buExtra = el('ve-extra').innerHTML;
  out.writes = calls.filter(c => c.method !== 'GET').length;
  process.stdout.write(JSON.stringify(out));
})().catch(e => { console.error(e); process.exit(1); });
