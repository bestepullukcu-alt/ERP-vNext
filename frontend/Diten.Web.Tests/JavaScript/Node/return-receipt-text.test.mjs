// MOD-0186 DL-2 (Q504, R34-3) — the receipt refusal and line-state texts, run by Node against the SHIPPED Returns index.js.
//
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/return-receipt-text.test.mjs
// ReturnReceiptTextTests.cs runs it from `dotnet test`. Every L10n value is a sentinel naming its key, so an assertion proves
// WHICH text was chosen; that the key has a text in all 7 languages is proven by ReturnReceiptTextTests.cs.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js'), 'utf8');

// Pack MOD-0183 r7 §23.3 closed inventoryCode list, written out by hand — not read from the code under test.
const closedList = ['FEFO_BYPASS_REASON_REQUIRED', 'FEFO_ORDER_VIOLATION', 'INSUFFICIENT_AVAILABLE', 'INSUFFICIENT_STOCK',
    'ITEM_NOT_ACTIVE', 'LOT_BELOW_MIN_REMAINING_SHELF_LIFE', 'LOT_BLOCKED', 'LOT_EXPIRED', 'LOT_NOT_ALLOWED', 'LOT_REQUIRED',
    'LOT_RETEST_DUE', 'LOT_SKU_MISMATCH', 'LOT_UNDER_RECALL', 'PRODUCT_MASTER_DATA_INCOMPLETE', 'RESERVATION_CONFLICT',
    'SERIAL_ALREADY_EXISTS', 'SERIAL_COUNT_MISMATCH', 'SERIAL_LOT_MISMATCH', 'SERIAL_NOT_AVAILABLE', 'SKU_ITEM_MISMATCH',
    'SKU_LEVEL_NOT_STOCKABLE', 'UNKNOWN_ITEM', 'UNKNOWN_LOCATION', 'UNKNOWN_LOT', 'UOM_CONVERSION_MISSING'];
const permanent = ['ITEM_NOT_ACTIVE', 'SKU_LEVEL_NOT_STOCKABLE', 'UNKNOWN_ITEM'];
const pascal = (code) => code.toLowerCase().split('_').map((part) => part[0].toUpperCase() + part.slice(1)).join('');
const reasonKey = (code) => `InventoryRefused${pascal(code)}`;

const L10n = new Proxy({
    ErrInventoryRefused: 'ErrInventoryRefused[{0}]', ErrReturnSourceMovementMissing: 'ErrReturnSourceMovementMissing[{0}]',
    ErrReturnLineNotUnpostable: 'ErrReturnLineNotUnpostable[{0}]', InventoryRefusedUnknown: 'InventoryRefusedUnknown[{0}]',
    ReceiptStoredValues: 'ReceiptStoredValues[{0}|{1}|{2}]'
}, { get: (target, key) => (typeof key === 'string' ? target[key] ?? key : undefined), has: () => true, ownKeys: () => ['ErrInventoryRefused'],
    getOwnPropertyDescriptor: () => ({ enumerable: true, configurable: true, value: 'x' }) });

const fields = {};
const elementStub = (id) => (id in fields ? { value: fields[id] } : null);
const context = vm.createContext({
    document: { getElementById: elementStub, querySelector: () => null, addEventListener: () => {} },
    window: { L10n }, Intl, crypto, console
});
const { failureText, readFailure, permanentRefusal, receiptLineCells, buildTransitionBody } = vm.runInContext(`${source}\n;ReturnsList`, context);
const answer = async (status, code, details) => readFailure({
    status, headers: { get: () => null },
    json: async () => ({ error: { code, details, correlationId: 'aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa' }, contractVersion: 'v1' })
});
const text = async (status, code, details) => failureText(await answer(status, code, details));

test('every closed-list inventoryCode shows the line and its own reason', async () => {
    assert.equal(closedList.length, 25);
    for (const code of closedList) {
        const hint = permanent.includes(code) ? '' : ' ReceiptPartialRetryHint';
        assert.equal(await text(422, 'INVENTORY_REFUSED', { shipmentLineNumber: '2', inventoryCode: code }),
            `ErrInventoryRefused[2] ${reasonKey(code)}${hint}`, code);
    }
});

test('an inventoryCode outside the list shows the generic reason with the code; prototype names are not codes', async () => {
    for (const code of ['INVALID_MOVEMENT_TYPE', 'constructor', '__proto__', 'toString', 'hasOwnProperty']) {
        assert.equal(await text(422, 'INVENTORY_REFUSED', { shipmentLineNumber: '1', inventoryCode: code }),
            `ErrInventoryRefused[1] InventoryRefusedUnknown[${code}] ReceiptPartialRetryHint`, code);
    }
    assert.equal(await text(422, 'INVENTORY_REFUSED', {}), 'ErrInventoryRefused[NotProvided] InventoryRefusedUnknown[NotProvided] ReceiptPartialRetryHint');
});

test('an unrecognized 422 code shows the generic reason, never the validation text', async () => {
    assert.equal(await text(422, 'NEW_RECEIPT_RULE'), 'InventoryRefusedUnknown[NEW_RECEIPT_RULE]');
    assert.equal(await text(422, ''), 'ErrInvalidRequest');
});

test('the module codes have their own text and never the validation text', async () => {
    assert.equal(await text(422, 'RETURN_SOURCE_MOVEMENT_MISSING', { shipmentLineNumber: '3' }), 'ErrReturnSourceMovementMissing[3]');
    assert.equal(await text(422, 'RETURN_LINE_NOT_UNPOSTABLE', { shipmentLineNumber: '4', lastInventoryCode: 'UNKNOWN_LOCATION' }), 'ErrReturnLineNotUnpostable[4]');
    assert.equal(await text(409, 'RETURN_RECEIPT_IN_PROGRESS', { toWarehouseId: 'wh-01', toLocationId: 'loc-q1', occurredAt: '2026-10-26T08:00:00Z' }),
        'ErrReturnReceiptInProgress ReceiptStoredValues[wh-01|loc-q1|2026-10-26T08:00:00Z]');
    assert.equal(await text(409, 'RETURN_RECEIPT_IN_PROGRESS', { toWarehouseId: 'wh-01', toLocationId: null, occurredAt: '2026-10-26T08:00:00Z' }),
        'ErrReturnReceiptInProgress ReceiptStoredValues[wh-01|NotProvided|2026-10-26T08:00:00Z]');
    assert.equal(await text(409, 'RETURN_RECEIPT_IN_PROGRESS'), 'ErrReturnReceiptInProgress');
});

test('the other answers of Received keep their texts', async () => {
    assert.equal(await text(403, 'INVALID_REQUEST', { inventoryCode: 'CROSS_LE_FORBIDDEN' }), 'NoticeForbidden');
    assert.equal(await text(503, 'DEPENDENCY_UNAVAILABLE'), 'ErrDependencyUnavailable');
    assert.equal(await text(500, 'INTERNAL_ERROR'), 'ErrInternalError');
    assert.equal(await text(503, 'PERSISTENCE_UNAVAILABLE'), 'ErrPersistenceUnavailable');
    assert.equal(await text(400, 'INVALID_REQUEST'), 'ErrInvalidRequest');
});

test('the unposted exit is offered only for the three permanent codes of a named line', async () => {
    for (const code of closedList) {
        const failure = await answer(422, 'INVENTORY_REFUSED', { shipmentLineNumber: '2', inventoryCode: code });
        assert.equal(permanentRefusal(failure), permanent.includes(code), code);
    }
    assert.equal(permanentRefusal(await answer(422, 'INVENTORY_REFUSED', { inventoryCode: 'UNKNOWN_ITEM' })), false);
    assert.equal(permanentRefusal(await answer(422, 'INVENTORY_REFUSED', { shipmentLineNumber: '<b>', inventoryCode: 'UNKNOWN_ITEM' })), false);
    assert.equal(permanentRefusal(await answer(422, 'RETURN_SOURCE_MOVEMENT_MISSING', { shipmentLineNumber: '2', inventoryCode: 'UNKNOWN_ITEM' })), false);
});

test('line states: Posted, NotPosted and each reason have their own label; an unknown reason shows the code', () => {
    assert.deepEqual([...receiptLineCells({ shipmentLineNumber: '1', inventoryPosting: 'Posted', inventoryPostingReason: null, inventoryTransactionReferenceId: 'tx' })],
        ['1', 'ReceiptLinePosted', '', 'tx']);
    const reasons = { PRE_WP_P1: 'ReceiptReasonPreWpP1', INTEGRITY_FAULT: 'ReceiptReasonIntegrityFault', ITEM_NOT_ACTIVE: 'InventoryRefusedItemNotActive',
        SKU_LEVEL_NOT_STOCKABLE: 'InventoryRefusedSkuLevelNotStockable', UNKNOWN_ITEM: 'InventoryRefusedUnknownItem', NEW_REASON: 'InventoryRefusedUnknown[NEW_REASON]' };
    for (const [reason, label] of Object.entries(reasons)) {
        assert.deepEqual([...receiptLineCells({ shipmentLineNumber: '2', inventoryPosting: 'NotPosted', inventoryPostingReason: reason, inventoryTransactionReferenceId: null })],
            ['2', 'ReceiptLineNotPosted', label, ''], reason);
    }
});

test('Received sends the receiving place and never a client reference; the exit adds exactly one unposted line', () => {
    Object.assign(fields, { transitionTarget: 'Received', transitionOccurredAt: '2026-10-26T08:00', transitionInventoryReference: 'typed',
        transitionToWarehouseId: 'wh-01', transitionToLocationId: '' });
    const plain = JSON.parse(buildTransitionBody().bodyText);
    assert.equal(plain.toWarehouseId, 'wh-01');
    assert.ok(!('toLocationId' in plain) && !('inventoryTransactionReferenceId' in plain) && !('unpostedLines' in plain));
    fields.transitionToLocationId = 'loc-q1';
    const unposted = JSON.parse(buildTransitionBody({ shipmentLineNumber: '2', reasonCode: 'UNKNOWN_ITEM' }).bodyText);
    assert.equal(unposted.toLocationId, 'loc-q1');
    assert.deepEqual(unposted.unpostedLines, [{ shipmentLineNumber: '2', reasonCode: 'UNKNOWN_ITEM' }]);
    fields.transitionTarget = 'Dispositioned';
    const dispositioned = JSON.parse(buildTransitionBody({ shipmentLineNumber: '2', reasonCode: 'UNKNOWN_ITEM' }).bodyText);
    assert.equal(dispositioned.inventoryTransactionReferenceId, 'typed');
    assert.ok(!('toWarehouseId' in dispositioned) && !('unpostedLines' in dispositioned));
});
