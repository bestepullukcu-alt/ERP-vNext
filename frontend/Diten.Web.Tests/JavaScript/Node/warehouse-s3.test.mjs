// MOD-0178 DL-7 — the S3 screen rules, run by Node against the SHIPPED files frontend/Diten.Web/wwwroot/assets/js/Warehouse/{WarehouseExceptions,
// WarehouseActivity,WarehouseIntegration}/index.js; WarehouseS3NodeTests.cs runs it from `dotnet test`. No DOM: the scripts' pure parts (the link to a
// record's screen on the reader's keys, the one-line summary of a kind, the local-time text) are read through their exported objects.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (folder) => readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Warehouse', folder, 'index.js'), 'utf8');

const load = (folder, name, keys, words = {}) => {
    const context = {
        window: { Permissions: { has: (k) => keys.has(k) }, L10n: { Words: words } },
        document: { querySelector: () => null, querySelectorAll: () => [], getElementById: () => null, addEventListener: () => {} }
    };
    vm.createContext(context);
    vm.runInContext(`${js(folder)}\n;this.__exported = ${name};`, context);
    return context.__exported;
};

const ALL = new Set(['warehouse.putaway-tasks.read', 'warehouse.pick-tasks.read', 'warehouse.outbound-shipments.read', 'inventory.stock.read']);

test('§8.23 — a record links to its own screen, only when the reader holds that screen\'s key', () => {
    const box = load('WarehouseExceptions', 'WarehouseExceptionsList', ALL);
    assert.equal(box.linkOf({ kind: 'PostingRefused', taskKind: 'Putaway' }), '/Warehouse/PutawayTasks');
    assert.equal(box.linkOf({ kind: 'PostingIntegrityError', taskKind: 'Pick' }), '/Warehouse/PickTasks');
    assert.equal(box.linkOf({ kind: 'DispatchNotSeen', outboundId: 'o-1' }), '/Warehouse/OutboundShipments/Details/o-1');
    assert.equal(box.linkOf({ kind: 'ShortDispatchUnresolved', outboundId: 'o-2' }), '/Warehouse/OutboundShipments/Details/o-2');

    // Q-10a: a reader without the outbound read (the operator) gets no link to an outbound — never a page that refuses
    const operator = load('WarehouseExceptions', 'WarehouseExceptionsList', new Set(['warehouse.putaway-tasks.read', 'warehouse.pick-tasks.read']));
    assert.equal(operator.linkOf({ kind: 'ReservedLotUnderRecall', outboundId: 'o-3' }), null);
    assert.equal(operator.linkOf({ kind: 'PostingPendingLong', taskKind: 'Pick' }), '/Warehouse/PickTasks');
    const none = load('WarehouseExceptions', 'WarehouseExceptionsList', new Set());
    assert.equal(none.linkOf({ kind: 'PostingRefused', taskKind: 'Putaway' }), null);

    const trace = load('WarehouseActivity', 'WarehouseActivityList', ALL);
    assert.equal(trace.linkOf({ source: 'Movement' }), '/StockMovements');
    assert.equal(trace.linkOf({ source: 'Outbound', recordId: 'o-4' }), '/Warehouse/OutboundShipments/Details/o-4');
    assert.equal(load('WarehouseActivity', 'WarehouseActivityList', new Set()).linkOf({ source: 'PickTask' }), null);
});

test('§8.23 — a ShortDispatchUnresolved row says why its difference was not computed (r25 F-1), otherwise picked / issued', () => {
    const words = {
        SummaryShort: '{0} · picked {1}, issued {2}', SummaryDeferred: 'Difference not computed: {0}',
        'Warehouse.Exception.DeferredBy.UnsyncedReadyToShip': 'an outbound of this node is ready to ship and not synced yet'
    };
    const box = load('WarehouseExceptions', 'WarehouseExceptionsList', ALL, words);
    assert.equal(box.summary({ kind: 'ShortDispatchUnresolved', sku: 'SKU-1', expected: '20.000', issued: '15.000' }), 'SKU-1 · picked 20.000, issued 15.000');
    assert.equal(box.summary({ kind: 'ShortDispatchUnresolved', deferredBy: 'UnsyncedReadyToShip' }),
        'Difference not computed: an outbound of this node is ready to ship and not synced yet');
});

test('Integration Monitor — the script only turns a UTC instant into local text; a non-instant leaves the server\'s text', () => {
    const cards = load('WarehouseIntegration', 'WarehouseIntegrationCards', ALL);
    assert.equal(typeof cards.local('2026-11-10T09:18:00Z'), 'string');
    assert.notEqual(cards.local('2026-11-10T09:18:00Z'), null);
    assert.equal(cards.local('not a time'), null);
    assert.equal(cards.local(''), null);
    assert.ok(!js('WarehouseIntegration').includes('fetch('), 'the cards are drawn by the server — no client fetch, no loading state');
});
