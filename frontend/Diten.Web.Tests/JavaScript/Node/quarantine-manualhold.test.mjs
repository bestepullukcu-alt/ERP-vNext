// WP-MVP1-FU-CLEAN1 (FU-0175-UI-MANUALHOLD-01) — the rules of "Stoğu karantinaya al", run by Node against the SHIPPED file
// frontend/Diten.Web/wwwroot/assets/js/Quarantine/manual-hold.js (QuarantineManualHoldRules). QuarantineManualHoldTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Quarantine/manual-hold.js'), 'utf8');
const context = vm.createContext({ window: {}, console });
const R = vm.runInContext(`${source}\n;QuarantineManualHoldRules`, context);
const plain = (x) => JSON.parse(JSON.stringify(x));
const ROW = { itemId: 'i-1', skuId: 's-1', skuLevel: 'Lsku', warehouseId: 'w-1', locationId: 'l-1', lotId: 'lot-1', onHand: '6', baseUomId: 'EA',
    item: 'GP-0042 · Aspirin', warehouse: 'WH-1', location: 'A-1' };

test('the body is quarantineStock mode MANUAL with exactly one bucket of the chosen row (QuarantineStockRequest + Bucket)', () => {
    const body = plain(R.request(ROW, '2,5', '  Müşteri şikâyeti, inceleme  ', 'k-12345678'));
    assert.deepEqual(body, {
        mode: 'Manual',
        buckets: [{ itemId: 'i-1', skuId: 's-1', skuLevel: 'Lsku', warehouseId: 'w-1', locationId: 'l-1', quantity: 2.5, lotId: 'lot-1' }],
        reason: 'Müşteri şikâyeti, inceleme',
        idempotencyKey: 'k-12345678'
    });
});

test('the quantity is above 0 and at most the row on hand; no row → refused', () => {
    assert.equal(R.quantityError('6', ROW), false);
    assert.equal(R.quantityError('0.5', ROW), false);
    for (const bad of ['', '0', '-1', '6.01', 'abc']) assert.equal(R.quantityError(bad, ROW), true, bad);
    assert.equal(R.quantityError('1', undefined), true);
});

test('the reason is required: at least 10 characters after trimming', () => {
    assert.equal(R.reasonError(''), true);
    assert.equal(R.reasonError('   kısa    '), true);
    assert.equal(R.reasonError('Şikâyet DEV-1'), false);
});

test('the confirmation line names product, place and quantity (text; the dialog body is subtextHtml, the line goes in by textContent)', () => {
    assert.equal(R.line(ROW, '2'), 'GP-0042 · Aspirin — WH-1 · A-1 — 2 EA');
    assert.match(source, /body\.querySelector\('\[data-manual-line\]'\)\.textContent = rules\.line\(row, quantity\);/);
    assert.match(source, /subtextHtml: body\.outerHTML,/);
    assert.match(source, /inputValidator: \(value\) => \(rules\.reasonError\(value\) \? t\('Quarantine\.Common\.ReasonMin'\) : null\)/);
    assert.doesNotMatch(source, /\bsubtext:/);
});
