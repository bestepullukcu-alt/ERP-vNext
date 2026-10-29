// MOD-0183 DL-2 (S23-7) — the Shipment Details error and dispatch-state texts, run by Node against the SHIPPED details.js.
//
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-details-error-text.test.mjs
// ShipmentDispatchTextTests.cs runs it from `dotnet test`. Every L10n value is a sentinel naming its payload property, so
// an assertion proves WHICH text was chosen; that the payload property is filled from a resx key in all 7 languages is
// proven by ShipmentDispatchTextTests.cs. The DOM (the alert, the status cell) is not executed here; see ../../README.md.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js'), 'utf8');

// Pack MOD-0183 r7 §23.3 closed inventoryCode list, written out by hand — not read from the code under test.
const closedList = ['FEFO_BYPASS_REASON_REQUIRED', 'FEFO_ORDER_VIOLATION', 'INSUFFICIENT_AVAILABLE', 'INSUFFICIENT_STOCK',
    'ITEM_NOT_ACTIVE', 'LOT_BELOW_MIN_REMAINING_SHELF_LIFE', 'LOT_BLOCKED', 'LOT_EXPIRED', 'LOT_NOT_ALLOWED', 'LOT_REQUIRED',
    'LOT_RETEST_DUE', 'LOT_SKU_MISMATCH', 'LOT_UNDER_RECALL', 'PRODUCT_MASTER_DATA_INCOMPLETE', 'RESERVATION_CONFLICT',
    'SERIAL_ALREADY_EXISTS', 'SERIAL_COUNT_MISMATCH', 'SERIAL_LOT_MISMATCH', 'SERIAL_NOT_AVAILABLE', 'SKU_ITEM_MISMATCH',
    'SKU_LEVEL_NOT_STOCKABLE', 'UNKNOWN_ITEM', 'UNKNOWN_LOCATION', 'UNKNOWN_LOT', 'UOM_CONVERSION_MISSING'];

const L10n = {
    unknown: 'unknown', validationError: 'validationError', accessDenied: 'accessDenied', notFound: 'notFound',
    persistenceUnavailable: 'persistenceUnavailable', internalError: 'internalError',
    inventoryRefused: 'inventoryRefused[{0}]', lineStockIncomplete: 'lineStockIncomplete[{0}]',
    dispatchInProgress: 'dispatchInProgress', dispatchCompensationFailed: 'dispatchCompensationFailed',
    dependencyUnavailable: 'dependencyUnavailable', inventoryCodeUnknown: 'inventoryCodeUnknown[{0}]',
    inventoryCodes: Object.fromEntries(closedList.map((code) => [code, `reason:${code}`])),
    statuses: { Planned: 'status:Planned' },
    dispatchStates: { InProgress: 'state:InProgress', CompensationFailed: 'state:CompensationFailed' }
};
const context = vm.createContext({ document: { getElementById: () => null, addEventListener: () => {} }, window: { L10n }, Intl, crypto });
const { errorText, statusText } = vm.runInContext(`${source}\n;ShipmentDetails`, context);
const body = (code, details) => ({ error: { code, details, correlationId: 'aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa' }, contractVersion: 'v1' });

test('every closed-list inventoryCode shows the line and its own reason', () => {
    assert.equal(closedList.length, 25);
    for (const code of closedList) {
        assert.equal(errorText(422, body('INVENTORY_REFUSED', { lineNumber: '2', inventoryCode: code })),
            `inventoryRefused[2] reason:${code}`, code);
    }
});

test('an inventoryCode outside the list shows the generic reason with the code itself', () => {
    assert.equal(errorText(422, body('INVENTORY_REFUSED', { lineNumber: '1', inventoryCode: 'NEW_INVENTORY_RULE' })),
        'inventoryRefused[1] inventoryCodeUnknown[NEW_INVENTORY_RULE]');
    // Prototype names are not codes of the list: they must not resolve to Object.prototype members.
    for (const code of ['constructor', '__proto__', 'toString', 'hasOwnProperty']) {
        assert.equal(errorText(422, body('INVENTORY_REFUSED', { lineNumber: '1', inventoryCode: code })),
            `inventoryRefused[1] inventoryCodeUnknown[${code}]`, code);
    }
    assert.equal(errorText(422, body('INVENTORY_REFUSED', {})), 'inventoryRefused[unknown] inventoryCodeUnknown[unknown]');
    // Review F-2: replacement patterns are inserted literally, and line 0 is a line, not "unknown".
    assert.equal(errorText(422, body('INVENTORY_REFUSED', { lineNumber: 0, inventoryCode: "$&$'" })),
        "inventoryRefused[0] inventoryCodeUnknown[$&$']");
    assert.equal(errorText(422, body('SHIPMENT_LINE_STOCK_INCOMPLETE', { lineNumber: '$&' })), 'lineStockIncomplete[$&]');
});

test('the module codes have their own text and never the validation text', () => {
    assert.equal(errorText(422, body('SHIPMENT_LINE_STOCK_INCOMPLETE', { lineNumber: '3' })), 'lineStockIncomplete[3]');
    assert.equal(errorText(409, body('DISPATCH_IN_PROGRESS')), 'dispatchInProgress');
    assert.equal(errorText(409, body('DISPATCH_COMPENSATION_FAILED', { lines: ['1'] })), 'dispatchCompensationFailed');
    assert.equal(errorText(503, body('DEPENDENCY_UNAVAILABLE')), 'dependencyUnavailable');
    for (const code of ['INVENTORY_REFUSED', 'SHIPMENT_LINE_STOCK_INCOMPLETE', 'DISPATCH_IN_PROGRESS', 'DISPATCH_COMPENSATION_FAILED', 'DEPENDENCY_UNAVAILABLE']) {
        for (const status of [400, 409, 422, 503]) {
            assert.ok(!errorText(status, body(code, { lineNumber: '1', inventoryCode: 'X' })).includes('validationError'), `${code} ${status}`);
        }
    }
});

test('an INVENTORY 403 keeps the existing permission text; other answers are unchanged', () => {
    assert.equal(errorText(403, body('INVALID_REQUEST', { inventoryCode: 'CROSS_LE_FORBIDDEN' })), 'accessDenied');
    assert.equal(errorText(500, body('INTERNAL_ERROR')), 'internalError');
    assert.equal(errorText(503, body('PERSISTENCE_UNAVAILABLE')), 'persistenceUnavailable');
    assert.equal(errorText(404, null), 'notFound');
    assert.equal(errorText(400, body('INVALID_REQUEST', { field: 'occurredAt' })), 'validationError');
});

test('dispatchState labels appear beside the status; None, null and unknown values add nothing', () => {
    assert.equal(statusText('Planned', 'InProgress'), 'status:Planned · state:InProgress');
    assert.equal(statusText('Planned', 'CompensationFailed'), 'status:Planned · state:CompensationFailed');
    for (const state of ['None', null, undefined, 'constructor']) assert.equal(statusText('Planned', state), 'status:Planned', String(state));
});
