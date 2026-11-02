// MOD-0178 DL-3 — the screen rules of Yerleştirme Görevleri, run by Node against the SHIPPED file
// frontend/Diten.Web/wwwroot/assets/js/Warehouse/PutawayTasks/rules.js; WarehousePutawayRulesNodeTests.cs runs it from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const dir = path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Warehouse/PutawayTasks');
globalThis.window = {}; // the shipped file registers itself on window; loaded in THIS realm so deepEqual compares plain objects
const { PutawayRules: rules } = vm.runInThisContext(`${readFileSync(path.join(dir, 'rules.js'), 'utf8')}\n;({ PutawayRules })`);
const index = readFileSync(path.join(dir, 'index.js'), 'utf8');

const ALL = new Set(['warehouse.putaway-tasks.read', 'warehouse.putaway-tasks.update', 'warehouse.putaway-tasks.confirm', 'warehouse.putaway-tasks.cancel', 'warehouse.putaway-tasks.resolve']);
const OPERATOR = new Set(['warehouse.putaway-tasks.read', 'warehouse.putaway-tasks.confirm']);
const canAll = (k) => ALL.has(k);
const canOperator = (k) => OPERATOR.has(k);

// the service's answers for the three AC-39 / AC-43 states (PutawayTaskReads.Actions)
const noConfirmation = {
    allowedActions: ['Confirm', 'Update', 'Claim', 'Cancel'],
    disabledActions: [{ action: 'CloseShort', reasonCode: 'NoPostedConfirmation' }, { action: 'Retry', reasonCode: 'NoPendingConfirmation' }]
};
const withPosted = {
    allowedActions: ['Confirm', 'Update', 'CloseShort'],
    disabledActions: [{ action: 'Cancel', reasonCode: 'HasPostedConfirmations' }, { action: 'Claim', reasonCode: 'AlreadyAssigned' }, { action: 'Retry', reasonCode: 'NoPendingConfirmation' }]
};
const pending = {
    allowedActions: ['Retry'],
    disabledActions: ['Confirm', 'Update', 'Claim', 'Cancel', 'CloseShort'].map((action) => ({ action, reasonCode: 'ConfirmationPending' }))
};
const state = (task, action, can = canAll) => rules.actionState(task, action, can);

test('AC-43 — with no confirmation only Cancel closes the task; with a Posted one only Close remainder; the disabled one says why', () => {
    assert.equal(state(noConfirmation, 'Cancel').enabled, true);
    assert.equal(state(noConfirmation, 'CloseShort').enabled, false);
    assert.equal(rules.reasonWord(state(noConfirmation, 'CloseShort').reasonCode), 'Warehouse.Disabled.NoPostedConfirmation');
    assert.equal(state(withPosted, 'CloseShort').enabled, true);
    assert.equal(state(withPosted, 'Cancel').enabled, false);
    assert.equal(rules.reasonWord(state(withPosted, 'Cancel').reasonCode), 'Warehouse.Disabled.HasPostedConfirmations');
    // the row menu keeps the disabled entry (visible, with its reason) — it is not hidden
    const menu = rules.menu(withPosted, canAll, rules.RowMenu);
    assert.deepEqual(menu.map((m) => [m.action, m.enabled]), [['Confirm', true], ['Update', true], ['Claim', false], ['CloseShort', true], ['Cancel', false]]);
});

test('AC-39 — a Pending confirmation locks confirm / edit / claim / close / cancel with its reason; only Retry works', () => {
    for (const action of ['Confirm', 'Update', 'Claim', 'CloseShort', 'Cancel']) {
        const s = state(pending, action);
        assert.equal(s.visible, true);
        assert.equal(s.enabled, false, action);
        assert.equal(s.reasonCode, 'ConfirmationPending');
    }
    assert.equal(state(pending, 'Retry').enabled, true);
});

test('a key the reader lacks hides the control (absent, not disabled); a dangerous action is marked', () => {
    assert.equal(state(noConfirmation, 'Cancel', canOperator).visible, false);
    assert.equal(state(noConfirmation, 'Update', canOperator).visible, false);
    assert.equal(state(noConfirmation, 'Confirm', canOperator).visible, true);
    assert.deepEqual(rules.menu(noConfirmation, canOperator, rules.RowMenu).map((m) => m.action), ['Confirm', 'Claim', 'CloseShort']);
    assert.equal(state({ allowedActions: ['ResolveIntegrity'], disabledActions: [] }, 'ResolveIntegrity', canOperator).visible, false);
    assert.equal(state({ allowedActions: ['ResolveIntegrity'], disabledActions: [] }, 'ResolveIntegrity').dangerous, true);
    assert.equal(rules.ActionKeys.ResolveIntegrity, 'warehouse.putaway-tasks.resolve');
});

test('AC-46 — status and integration flag are two badges; the one flag the service sends is drawn; None draws nothing', () => {
    assert.deepEqual(rules.statusBadge('InProgress'), { word: 'Warehouse.Status.InProgress', cls: 'bg-label-primary' });
    assert.equal(rules.flagBadge('None'), null);
    assert.equal(rules.flagBadge(undefined), null);
    assert.equal(rules.flagBadge('PostingPendingLong').word, 'Warehouse.Flag.PostingPendingLong');
    assert.equal(rules.flagBadge('PostingIntegrityError').word, 'Warehouse.Flag.PostingIntegrityError');
    assert.equal(rules.flagBadge('PostingRefused').word, 'Warehouse.Flag.PostingRefused');
    assert.equal(rules.flagBadge('PostingPending').word, 'Warehouse.Flag.PostingPending');
    assert.notEqual(rules.flagBadge('PostingPending').cls, rules.flagBadge('PostingPendingLong').cls);
    // the list draws the flag in its own column, through the rule
    assert.match(index, /\{ data: 'integrationFlag', name: 'flag' \}/);
    assert.match(index, /const flagBadge = \(flag\) => badge\(rules\.flagBadge\(flag\)\);/);
});

test('AC-44 — only an exact code answer selects a bin; an unknown code is a field error and blocks the submit', () => {
    assert.deepEqual(rules.binVerdict('A-01', { match: { value: 'b1', text: 'A-01 · Aisle' }, available: true }), { ok: true, error: null, id: 'b1', label: 'A-01 · Aisle' });
    assert.equal(rules.binVerdict('A-0', { match: null, available: true }).error, 'BinNotFound');
    assert.equal(rules.binVerdict('A-01', { available: false }).error, 'BinLookupUnavailable');
    assert.equal(rules.binVerdict('A-01', null).ok, false);
    assert.equal(rules.binVerdict('  ', { match: null }).error, null);
    // the field resolves on Enter (a keyboard-emulating scanner ends with Enter) and on change; an unresolved code sends nothing
    assert.match(index, /if \(event\.key === 'Enter'\) \{ event\.preventDefault\(\); event\.stopPropagation\(\); input\.resolve\(\); \}/);
    assert.match(index, /if \(!to\) \{ if \(to === ''\) fieldError\('confirmBinError', W\('BinNotFound'\)\); return; \}/);
    assert.match(index, /if \(to === null\) return; \/\/ a typed code that is not a bin/);
});

test('AC-41 — a locked candidate stays in the list, disabled, with lock, reason and blocking document number', () => {
    const w = (k) => ({ 'Warehouse.Candidate.LotUnderRecall': 'Lot geri çağırmada', 'Warehouse.Blocking.RecallCase': 'Geri çağırma vakası' }[k] || k);
    const locked = rules.candidateOption({ key: 'k2', locationCode: 'RCV-01', sku: 'SKU-1', lotNumber: 'L-2', openQuantity: '5', uom: 'EA', eligible: false, reason: 'LotUnderRecall', blockingKind: 'RecallCase', blockingNumber: 'MG-RCL-000007' }, w);
    assert.equal(locked.disabled, true);
    assert.equal(locked.locked, true);
    assert.match(locked.text, /Lot geri çağırmada/);
    assert.match(locked.text, /Geri çağırma vakası MG-RCL-000007/);
    const open = rules.candidateOption({ key: 'k1', locationCode: 'RCV-01', sku: 'SKU-1', lotNumber: 'L-1', openQuantity: '10', uom: 'EA', eligible: true }, w);
    assert.deepEqual([open.disabled, open.text], [false, 'RCV-01 · SKU-1 · L-1 · 10 EA']);
});

test('AC-42 — the suggestion is offered, never applied by itself; SuggestionApplied only while the target is the applied suggestion', () => {
    assert.deepEqual(rules.suggestion({ suggestedToLocationId: 's1', suggestedToLocation: 'B-02', toLocationId: 't1' }), { id: 's1', label: 'B-02' });
    assert.equal(rules.suggestion({ suggestedToLocationId: 's1', toLocationId: 's1' }), null);
    assert.equal(rules.suggestion({}), null);
    assert.equal(rules.targetSource('s1', 's1'), 'SuggestionApplied');
    assert.equal(rules.targetSource('s1', 'other'), 'Manual');
    assert.equal(rules.targetSource(null, 's1'), 'Manual');
    // the form opens without filling the bin from the suggestion: only the "Uygula" handler sets it
    assert.match(index, /const applySuggestion = \(\) => \{/);
    assert.doesNotMatch(index.slice(index.indexOf('const openEdit'), index.indexOf('const applySuggestion')), /setBin\('putaway', s\.id/);
});

test('AC-45 — −/+ steps by 1, never below 0 nor above the remaining; a quantity has at most 6 decimals and no exponent', () => {
    assert.equal(rules.step('4', 1, '5'), '5');
    assert.equal(rules.step('5', 1, '5'), '5');
    assert.equal(rules.step('0.5', -1), '0');
    assert.equal(rules.step('', 1), '1');
    assert.equal(rules.step('2,5', 1), '3.5');
    assert.equal(rules.isQuantity('4.123456'), true);
    assert.equal(rules.isQuantity('4.1234567'), false);
    assert.equal(rules.isQuantity('1e3'), false);
    assert.equal(rules.isQuantity('0'), false);
    assert.equal(rules.needsDeviation('A', 'a'), false);
    assert.equal(rules.needsDeviation('A', 'B'), true);
    assert.equal(rules.needsDeviation(null, 'B'), false);
});

// DL-3 CT F-2 (§11.3a) — "Saklama koşulu bilinmiyor": the elements of the create form, as plain stand-ins (no jsdom in a clean checkout)
const element = (hidden = true) => {
    const classes = new Set(hidden ? ['d-none'] : []);
    return { disabled: false, title: '', textContent: '', classList: { toggle: (c, on) => (on ? classes.add(c) : classes.delete(c)), contains: (c) => classes.has(c) } };
};
const form = () => {
    const els = { putawayConditionsUnknown: element(), putawayBinCode: element(false), btnSavePutawayTask: element(false), putawaySaveReason: element() };
    return { els, byId: (id) => els[id] ?? null };
};
const words = (k) => ({ 'Warehouse.Candidate.StorageConditionsUnknown': 'Saklama koşulu bilinmiyor — ürün kartında tamamlanmalı (MDM)', 'Warehouse.Disabled.StorageConditionsUnknown': 'Ürünün saklama koşulu bilinmiyor' }[k] || k);

test('F-2 — an unknown storage condition: the candidate keeps its row with the badge; a known set (even empty) has none', () => {
    const unknown = rules.candidateOption({ key: 'k3', locationCode: 'RCV-01', sku: 'SKU-1', lotNumber: 'L-3', openQuantity: '4', uom: 'EA', eligible: true, conditionsKnown: false }, words);
    assert.equal(unknown.unknownConditions, true);
    assert.equal(unknown.disabled, false);                                         // not hidden, not locked: the reader sees it and why
    assert.match(unknown.text, /Saklama koşulu bilinmiyor/);
    const empty = rules.candidateOption({ key: 'k4', locationCode: 'RCV-01', sku: 'SKU-2', eligible: true, conditionsKnown: true }, words);
    assert.equal(empty.unknownConditions, false);
    assert.doesNotMatch(empty.text, /bilinmiyor/);
});

test('F-2 — the gate shows the badge, closes the bin and "Kaydet" with the reason; a known product opens them again', () => {
    const { els, byId } = form();
    assert.equal(rules.applyConditionGate(byId, true, words), true);
    assert.equal(els.putawayConditionsUnknown.classList.contains('d-none'), false);
    assert.equal(els.putawayBinCode.disabled, true);
    assert.equal(els.btnSavePutawayTask.disabled, true);
    assert.equal(els.btnSavePutawayTask.title, 'Ürünün saklama koşulu bilinmiyor');
    assert.equal(els.putawaySaveReason.textContent, 'Ürünün saklama koşulu bilinmiyor');
    assert.equal(els.putawaySaveReason.classList.contains('d-none'), false);

    assert.equal(rules.applyConditionGate(byId, false, words), false);
    assert.equal(els.putawayConditionsUnknown.classList.contains('d-none'), true);
    assert.equal(els.putawayBinCode.disabled, false);
    assert.equal(els.btnSavePutawayTask.disabled, false);
    assert.equal(els.putawaySaveReason.classList.contains('d-none'), true);
    // the page applies it on every pick and resets it on open; a closed "Kaydet" sends nothing
    assert.match(index, /if \(rules\.applyConditionGate\(byId, !!data\.unknownConditions, W\)\) setBin\('putaway', '', ''\);/);
    assert.match(index, /if \(!formState \|\| byId\('btnSavePutawayTask'\)\?\.disabled\) return;/);
});
