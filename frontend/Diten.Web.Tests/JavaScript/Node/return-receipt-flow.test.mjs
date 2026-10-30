// MOD-0186 DL-2 (Q504 review 01 F-2..F-4) — the Received panel wired end to end: the SHIPPED index.js runs against a small
// fake DOM whose ids and initial d-none states are read from the SHIPPED Index.cshtml, so a renamed id goes red here.
//
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/return-receipt-flow.test.mjs
// ReturnReceiptTextTests.cs runs it from `dotnet test`. Layout and focus are not proven here; see the Q504 live check.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js'), 'utf8');
const view = readFileSync(path.join(repo, 'frontend/Diten.Web/Views/SupplyChain/Returns/Index.cshtml'), 'utf8');

class FakeElement {
    constructor(hidden) {
        this.value = ''; this.textContent = ''; this.children = []; this.dataset = {}; this.style = {}; this.attributes = {}; this.handlers = {}; this.disabled = false;
        const classes = new Set(hidden ? ['d-none'] : []);
        this.classList = {
            add: (c) => classes.add(c), remove: (c) => classes.delete(c), contains: (c) => classes.has(c),
            toggle: (c, on) => { if (on ?? !classes.has(c)) classes.add(c); else classes.delete(c); }
        };
    }
    get hidden() { return this.classList.contains('d-none'); }
    addEventListener(name, handler) { (this.handlers[name] ||= []).push(handler); }
    click() { (this.handlers.click || []).forEach((handler) => handler({ preventDefault() {} })); }
    setAttribute(name, value) { this.attributes[name] = value; }
    focus() {}
    contains() { return true; }
    append(...nodes) { this.children.push(...nodes); }
    appendChild(node) { this.children.push(node); return node; }
    replaceChildren(...nodes) {
        this.children = nodes;
        const chosen = nodes.find((node) => node.selected);
        if (chosen) this.value = chosen.value;
    }
    get text() { return [this.textContent, ...this.children.map((child) => child.text ?? child.textContent ?? '')].join(''); }
}

const ids = new Map();
for (const match of view.matchAll(/<[a-z]+\b[^>]*\bid="([A-Za-z][\w-]*)"[^>]*>/g)) {
    ids.set(match[1], new FakeElement(/class="[^"]*\bd-none\b/.test(match[0])));
}
const element = (id) => {
    assert.ok(ids.has(id), `${id} is not in Index.cshtml`);
    return ids.get(id);
};
ids.get('returns-permissions').textContent = JSON.stringify({ canReceive: true });

const RETURN = '66666666-6666-4666-8666-666666666666';
const L10n = new Proxy({
    ErrInventoryRefused: 'ErrInventoryRefused[{0}]', InventoryRefusedUnknown: 'InventoryRefusedUnknown[{0}]',
    ReceiptStoredValues: 'ReceiptStoredValues[{0}|{1}|{2}]', ConfirmReceiveWithoutPosting: 'Confirm[{0}]'
}, { get: (target, key) => (typeof key === 'string' ? target[key] ?? key : undefined), ownKeys: () => ['Status'],
    getOwnPropertyDescriptor: () => ({ enumerable: true, configurable: true, value: 'x' }) });

const sent = [];
let answers = [];
const respond = (status, body) => ({ ok: status < 400, status, headers: { get: () => null }, json: async () => body });
const fetchStub = async (url, options) => {
    if (!options?.method) return respond(200, { items: [{ returnId: RETURN, rmaNumber: 'RMA-1', shipmentId: '44444444-4444-4444-8444-444444444444', status: 'InTransit' }], total: 1 });
    sent.push({ key: options.headers['Idempotency-Key'], body: JSON.parse(options.body), text: options.body });
    const next = answers.shift();
    if (next === 'network') throw new Error('network');
    return respond(next.status, next.body);
};
const context = vm.createContext({
    document: { getElementById: (id) => ids.get(id) ?? null, querySelector: () => null, querySelectorAll: () => [],
        addEventListener: () => {}, createElement: () => new FakeElement(false), createTextNode: (text) => ({ textContent: text }), body: {}, activeElement: null },
    window: { L10n, showConfirm: (_title, callback, options) => { context.lastConfirm = options; callback(); }, showToast: () => {} },
    bootstrap: { Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } },
    fetch: fetchStub, URLSearchParams, Intl, crypto, console: { error() {}, warn() {} }
});
const list = vm.runInContext(`${source}\n;ReturnsList`, context);
list.bindEvents();
await list.loadReturns({}, () => {});

const settle = () => new Promise((resolve) => setTimeout(resolve, 5));
const refused = (code) => ({ status: 422, body: { error: { code: 'INVENTORY_REFUSED', details: { shipmentLineNumber: '2', inventoryCode: code } } } });
const open = () => {
    sent.length = 0;
    list.openTransition(RETURN, 'Received');
    element('transitionToWarehouseId').value = 'wh-01';
};
const submit = async () => { element('btnSubmitTransition').click(); await settle(); };
const exit = async () => { element('btnReceiveWithoutPosting').click(); await settle(); };

test('Received shows the receiving place; the exit appears only after a permanent refusal', async () => {
    open();
    assert.equal(element('transitionReceiptGroup').hidden, false);
    assert.equal(element('btnReceiveWithoutPosting').hidden, true);
    answers = [refused('LOT_EXPIRED')];
    await submit();
    assert.equal(element('btnReceiveWithoutPosting').hidden, true);
    assert.ok(element('formReturnTransitionAlert').text.includes('InventoryRefusedLotExpired'));
    answers = [refused('UNKNOWN_ITEM')];
    await submit();
    assert.equal(element('btnReceiveWithoutPosting').hidden, false);
});

test('the exit sends unpostedLines and lines[] fills the result table', async () => {
    open();
    answers = [refused('UNKNOWN_ITEM'), { status: 200, body: { lines: [
        { shipmentLineNumber: '1', inventoryPosting: 'Posted', inventoryPostingReason: null, inventoryTransactionReferenceId: 'tx-1' },
        { shipmentLineNumber: '2', inventoryPosting: 'NotPosted', inventoryPostingReason: 'UNKNOWN_ITEM', inventoryTransactionReferenceId: null }] } }];
    await submit();
    await exit();
    assert.equal(context.lastConfirm.subtext, 'Confirm[2]');
    assert.deepEqual(JSON.parse(JSON.stringify(sent[1].body.unpostedLines)), [{ shipmentLineNumber: '2', reasonCode: 'UNKNOWN_ITEM' }]);
    assert.equal(element('transitionReceiptResult').hidden, false);
    assert.equal(element('formReturnTransition').classList.contains('d-none'), true);
    const rows = element('transitionReceiptLines').children;
    assert.equal(rows.length, 2);
    assert.ok(rows[0].text.includes('ReceiptLinePosted') && rows[0].text.includes('tx-1'));
    assert.ok(rows[1].text.includes('ReceiptLineNotPosted') && rows[1].text.includes('InventoryRefusedUnknownItem'));
});

for (const [name, unknown] of [['503', { status: 503, body: { error: { code: 'PERSISTENCE_UNAVAILABLE' } } }], ['a network failure', 'network']]) {
    test(`after ${name} the unposted request repeats with the same key and body; Submit cannot send another body`, async () => {
        open();
        answers = [refused('ITEM_NOT_ACTIVE'), unknown, { status: 200, body: { lines: [{ shipmentLineNumber: '2', inventoryPosting: 'NotPosted', inventoryPostingReason: 'ITEM_NOT_ACTIVE' }] } }];
        await submit();
        await exit();
        assert.equal(element('btnReceiveWithoutPosting').hidden, false);
        assert.equal(element('btnSubmitTransition').hidden, true);
        element('transitionToWarehouseId').value = 'edited-after-the-failure';
        await exit();
        assert.equal(sent.length, 3);
        assert.equal(sent[2].key, sent[1].key);
        assert.equal(sent[2].text, sent[1].text);
        assert.equal(element('transitionReceiptResult').hidden, false);
    });
}

test('a definite answer to the unposted request ends the retry', async () => {
    open();
    answers = [refused('UNKNOWN_ITEM'), { status: 503, body: { error: { code: 'PERSISTENCE_UNAVAILABLE' } } },
        { status: 422, body: { error: { code: 'RETURN_LINE_NOT_UNPOSTABLE', details: { shipmentLineNumber: '2' } } } }];
    await submit();
    await exit();
    await exit();
    assert.equal(element('btnReceiveWithoutPosting').hidden, true);
    assert.equal(element('btnSubmitTransition').hidden, false);
});

test('the 409 fills the stored values, shows the local time and resends the exact stored instant', async () => {
    open();
    const occurredAt = '2026-10-26T08:00:07.250Z';
    answers = [{ status: 409, body: { error: { code: 'RETURN_RECEIPT_IN_PROGRESS', details: { toWarehouseId: 'wh-stored', toLocationId: null, occurredAt } } } }, { status: 200, body: {} }];
    await submit();
    const local = new Date(occurredAt);
    const pad = (n) => String(n).padStart(2, '0');
    const field = `${local.getFullYear()}-${pad(local.getMonth() + 1)}-${pad(local.getDate())}T${pad(local.getHours())}:${pad(local.getMinutes())}:07`;
    assert.equal(element('transitionOccurredAt').value, field);
    assert.equal(element('transitionToWarehouseId').value, 'wh-stored');
    const alert = element('formReturnTransitionAlert');
    assert.ok(alert.text.includes(field.replace('T', ' ')), alert.text);
    assert.ok(!alert.text.includes(occurredAt));
    assert.ok(alert.children[1].children.some((node) => node.dir === 'ltr' && node.textContent === 'wh-stored'), 'stored value in a bdi');
    await submit();
    assert.equal(sent[1].body.occurredAt, occurredAt);
    assert.equal(sent[1].body.toWarehouseId, 'wh-stored');
});

test('an empty warehouse stops both paths and marks the field', async () => {
    open();
    answers = [refused('UNKNOWN_ITEM')];
    await submit();
    element('transitionToWarehouseId').value = ' ';
    await exit();
    assert.equal(sent.length, 1);
    assert.equal(element('transitionToWarehouseId').attributes['aria-invalid'], 'true');
});
