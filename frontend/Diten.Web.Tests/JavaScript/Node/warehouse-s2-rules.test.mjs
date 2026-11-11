// MOD-0178 DL-6 — the S2 screen rules, run by Node against the SHIPPED files frontend/Diten.Web/wwwroot/assets/js/Warehouse/{OutboundShipments,
// PickTasks,Packing}/rules.js (+ the scripts that must use them); WarehouseS2RulesNodeTests.cs runs it from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (folder, file) => readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Warehouse', folder, file), 'utf8');
globalThis.window = {};
const { OutboundRules: outbound } = vm.runInThisContext(`${js('OutboundShipments', 'rules.js')}\n;({ OutboundRules })`);
const { PickRules: pick } = vm.runInThisContext(`${js('PickTasks', 'rules.js')}\n;({ PickRules })`);
const { PackingRules: packing } = vm.runInThisContext(`${js('Packing', 'rules.js')}\n;({ PackingRules })`);

const OUTBOUND_ALL = new Set(['warehouse.outbound-shipments.read', 'warehouse.outbound-shipments.create', 'warehouse.outbound-shipments.update',
    'warehouse.outbound-shipments.cancel', 'warehouse.outbound-shipments.release', 'warehouse.packages.read', 'warehouse.packages.create', 'warehouse.packages.update']);
// AC-34: the seven keys of WarehouseOperator (§14.2) — no outbound read
const OPERATOR = new Set(['warehouse.putaway-tasks.read', 'warehouse.putaway-tasks.confirm', 'warehouse.pick-tasks.read', 'warehouse.pick-tasks.confirm',
    'warehouse.packages.read', 'warehouse.packages.create', 'warehouse.packages.update']);
const canAll = (k) => OUTBOUND_ALL.has(k);
const canOperator = (k) => OPERATOR.has(k);
const actions = (o, can = canAll) => outbound.menu(o, can).map((m) => m.action);

test('AC-34 — the outbound status menu is §8.18 exactly: Packed → only "Sevke hazır" and "İptal"', () => {
    assert.deepEqual(actions({ status: 'Allocated' }), ['Cancel']);
    assert.deepEqual(actions({ status: 'Picked' }), ['Pack', 'Cancel']);
    assert.deepEqual(actions({ status: 'Packed' }), ['Release', 'Cancel']);
    assert.deepEqual(actions({ status: 'ReadyToShip' }), ['SyncDispatch']);
    assert.deepEqual(actions({ status: 'Shipped' }), []);
    assert.deepEqual(actions({ status: 'Cancelled' }), []);
    assert.deepEqual(actions({ status: 'Allocating' }), []); // internal states are never listed (§8.18)
    // "Düzenle" is the header button on Allocated / Picked / Packed — never after ReadyToShip
    assert.deepEqual(['Allocated', 'Picked', 'Packed', 'ReadyToShip', 'Shipped', 'Cancelled'].map((s) => outbound.canEdit({ status: s }, canAll)), [true, true, true, false, false, false]);
});

test('AC-34 — a key the reader lacks hides the entry; the Cancel entry is the dangerous one', () => {
    const noRelease = (k) => k !== 'warehouse.outbound-shipments.release' && canAll(k);
    assert.deepEqual(actions({ status: 'Packed' }, noRelease), ['Cancel']);
    assert.deepEqual(actions({ status: 'Picked' }, (k) => k !== 'warehouse.packages.create' && canAll(k)), ['Cancel']);
    assert.deepEqual(actions({ status: 'Packed' }, canOperator), []);
    assert.equal(outbound.menu({ status: 'Packed' }, canAll).find((m) => m.action === 'Cancel').dangerous, true);
    assert.equal(outbound.canEdit({ status: 'Picked' }, (k) => k !== 'warehouse.outbound-shipments.update'), false);
});

test('AC-33 (vi) — an Item outbound keeps "Sevke hazır" on the menu, DISABLED with its reason word; a SKU outbound has it enabled', () => {
    const item = outbound.menu({ status: 'Packed', itemLevel: true }, canAll).find((m) => m.action === 'Release');
    assert.deepEqual([item.enabled, item.reasonCode], [false, 'ItemLevelNotPublishable']);
    assert.equal(outbound.reasonWord(item.reasonCode), 'Warehouse.Outbound.Disabled.ItemLevelNotPublishable');
    assert.equal(outbound.menu({ status: 'Packed', itemLevel: false }, canAll).find((m) => m.action === 'Release').enabled, true);
    // the list and the Details page draw the disabled entry with that reason (never the button alone)
    assert.match(js('OutboundShipments', 'index.js'), /rules\.reasonWord\(state\.reasonCode\)/);
    assert.match(js('OutboundShipments', 'details.js'), /rules\.reasonWord\(state\.reasonCode\)[\s\S]*data-d-reason/);
    assert.match(js('OutboundShipments', 'details.js'), /if \(!state \|\| !state\.enabled\) return/);
});

test('§8.18 — "Hiçbir şey toplanmadı": Allocated, every pick task terminal, nothing picked', () => {
    const tasks = (...c) => c.map(([status, confirmed]) => ({ status, confirmed }));
    assert.equal(outbound.nothingPicked({ status: 'Allocated', pickTasks: tasks(['Completed', '0'], ['Completed', '0']) }), true);
    assert.equal(outbound.nothingPicked({ status: 'Allocated', pickTasks: tasks(['Completed', '0'], ['Open', '0']) }), false);
    assert.equal(outbound.nothingPicked({ status: 'Allocated', pickTasks: tasks(['Completed', '2']) }), false);
    assert.equal(outbound.nothingPicked({ status: 'Picked', pickTasks: tasks(['Completed', '0']) }), false);
    assert.equal(outbound.nothingPicked({ status: 'Allocated', pickTasks: [] }), false);
});

test('r22 — a 422 FEFO_BYPASS_REASON_REQUIRED becomes one reason row per deviating allocation; the release body carries them', () => {
    const o = { lines: [{ allocations: [{ allocSeq: 1, lotNumber: 'L1' }, { allocSeq: 2, lotNumber: 'L2' }] }] };
    const rows = outbound.deviations({ code: 'FEFO_BYPASS_REASON_REQUIRED', details: { deviations: [{ allocSeq: 2, lotId: 'x', firstEligibleExpiry: '2027-01-31' }] } }, o);
    assert.deepEqual(rows, [{ allocSeq: 2, lotNumber: 'L2', firstEligibleExpiry: '2027-01-31' }]);
    assert.deepEqual(outbound.releaseBody('k', 'W/"4"', [{ allocSeq: '2', reason: ' a reason of ten ' }]),
        { idempotencyKey: 'k', ifMatch: 'W/"4"', fefoBypassReasons: [{ allocSeq: 2, reason: 'a reason of ten' }] });
    assert.deepEqual(outbound.releaseBody('k', 'W/"4"', []), { idempotencyKey: 'k', ifMatch: 'W/"4"' });
});

test('§8.17 — a form line: SKU "Level:skuId", base unit, quantity, lot change; the chosen lot asks what the FEFO mode asks', () => {
    const row = { orderLineId: '1', itemId: 'i', sku: 'Item:i', uomId: 'KG', quantity: '30', lotId: '', reason: '' };
    assert.deepEqual(outbound.lineBody(row).line, { orderLineId: '1', itemId: 'i', skuId: 'i', skuLevel: 'Item', uomId: 'KG', quantity: '30' });
    assert.equal(outbound.lineBody({ ...row, uomId: '' }).error, 'ValidationLine');
    assert.equal(outbound.lineBody({ ...row, quantity: '0' }).error, 'ValidationLine');
    assert.deepEqual(outbound.lineBody({ ...row, lotId: 'L2', reason: 'reason' }).line.lotOverride, { lotId: 'L2', fefoBypassReason: 'reason' });
    const preview = (mode) => ({ mode, lots: [{ lotId: 'L1', fefoRank: 1 }, { lotId: 'L2', fefoRank: 2 }] });
    assert.equal(outbound.lotNeed(preview('RequireReason'), 'L1'), 'None');
    assert.equal(outbound.lotNeed(preview('RequireReason'), 'L2'), 'Required');
    assert.equal(outbound.lotNeed(preview('Warn'), 'L2'), 'Optional');
    assert.equal(outbound.lotNeed(preview('Block'), 'L2'), 'Blocked');
    assert.equal(outbound.lotNeed(preview('Block'), ''), 'None');
});

test('§8.19 — a pick task menu has no edit and no cancel; the service decides enabled / disabled with its reason', () => {
    const task = { allowedActions: ['Confirm', 'CloseShort'], disabledActions: [{ action: 'Claim', reasonCode: 'AlreadyAssigned' }, { action: 'Retry', reasonCode: 'NoPendingConfirmation' }] };
    const canPick = (k) => k.startsWith('warehouse.pick-tasks.');
    assert.deepEqual(pick.menu(task, canPick, pick.RowMenu).map((m) => [m.action, m.enabled]), [['Confirm', true], ['Claim', false], ['CloseShort', true]]);
    assert.ok(!pick.RowMenu.includes('Cancel') && !pick.RowMenu.includes('Update') && !pick.QuickViewMenu.includes('Cancel'));
    assert.equal(pick.reasonWord(pick.actionState(task, 'Claim', canPick).reasonCode), 'Warehouse.Disabled.AlreadyAssigned');
    // the operator holds confirm, not resolve
    assert.equal(pick.actionState({ allowedActions: ['ResolveIntegrity'] }, 'ResolveIntegrity', canOperator).visible, false);
});

test('AC-27 / §8.19 — the confirmation body is the quantity only: no target, no source', () => {
    assert.deepEqual(Object.keys(pick.confirmBody('k', 'W/"2"', ' 5 ')).sort(), ['idempotencyKey', 'ifMatch', 'quantity']);
    assert.match(js('PickTasks', 'index.js'), /rules\.confirmBody\(/);
    assert.doesNotMatch(js('PickTasks', 'index.js'), /toLocationId|fromLocationId/);
});

test('AC-34 (r21) — "Paketle" is a link to /Warehouse/Packing/{outboundId} when the outbound is Picked and the reader holds packages.read — the operator does', () => {
    const task = { outbound: { id: 'o-1', status: 'Picked' } };
    assert.equal(pick.packLink(task, canOperator), '/Warehouse/Packing/o-1');
    assert.equal(pick.packLink({ outbound: { id: 'o-1', status: 'Allocated' } }, canOperator), null);
    assert.equal(pick.packLink(task, (k) => k !== 'warehouse.packages.read'), null);
    assert.equal(pick.PackKey, 'warehouse.packages.read'); // never the outbound read (Q-10a)
    assert.equal(pick.originWord('ShortDispatch'), 'Warehouse.Origin.ShortDispatch');
});

test('§8.20 — packing: a package opens on Picked only; Edit / Close for an Open package and the update key; what is left to pack', () => {
    assert.deepEqual(packing.newPackage({ status: 'Picked' }, canOperator), { visible: true, enabled: true, reason: null });
    assert.deepEqual(packing.newPackage({ status: 'Allocated' }, canOperator), { visible: true, enabled: false, reason: 'NotPicked' });
    assert.deepEqual(packing.newPackage({ status: 'Packed' }, canOperator), { visible: true, enabled: false, reason: 'PackedDone' });
    assert.equal(packing.newPackage({ status: 'Picked' }, (k) => k !== 'warehouse.packages.create').visible, false);
    assert.deepEqual(packing.packageActions({ status: 'Open' }, canOperator), ['Edit', 'Close']);
    assert.deepEqual(packing.packageActions({ status: 'Closed' }, canOperator), []);
    assert.deepEqual(packing.packageActions({ status: 'Open' }, (k) => k !== 'warehouse.packages.update'), []);
    const allocations = [{ allocSeq: 1, picked: '30' }, { allocSeq: 2, picked: '5' }];
    const packages = [{ id: 'a', status: 'Closed', contents: [{ allocSeq: 1, quantity: '20' }] }, { id: 'b', status: 'Open', contents: [{ allocSeq: 1, quantity: '4' }] },
        { id: 'c', status: 'Cancelled', contents: [{ allocSeq: 2, quantity: '5' }] }];
    assert.deepEqual(packing.remaining(allocations, packages), { 1: 6, 2: 5 });
    assert.deepEqual(packing.remaining(allocations, packages, 'b'), { 1: 10, 2: 5 });
    assert.deepEqual(packing.contentsBody([{ allocSeq: '1', quantity: '6' }, { allocSeq: '2', quantity: '' }]), [{ allocSeq: 1, quantity: '6' }]);
    assert.equal(packing.isDimensions('40x30x20'), true);
    assert.equal(packing.isDimensions('12x30'), false);
    assert.equal(packing.dimensions('40 X 30 x 20'), '40x30x20');
});

test('AC-45 — the S2 steppers: steps of 1, never below 0, never above what is left', () => {
    assert.equal(pick.step('3', 1, '4'), '4');
    assert.equal(pick.step('4', 1, '4'), '4');
    assert.equal(packing.step('0', -1, '5'), '0');
    assert.equal(packing.step('2', 1, '2.5'), '2.5');
});
