// MOD-0183 r9 DL-4 (§24.3, §24.4) — the list cells of the SHIPPED index.js, run by Node. SB-9: a resolved customer shows its
// name and code, never an id; SB-10: a failed read says "name unavailable" (warehouse with its reference, customer without);
// SB-11a: an old record shows its shipToReference as stored, marked; every value is HTML-escaped.
//
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-list-cells.test.mjs
// ShipmentListCellsNodeTests.cs runs it from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/index.js'), 'utf8');
const L10n = { oldRecord: 'OLD', nameNotFound: 'NOTFOUND', nameUnavailable: 'UNAVAILABLE', statuses: {} };
const context = vm.createContext({ document: { getElementById: () => null, addEventListener: () => {}, documentElement: {} }, window: { L10n }, Intl, crypto });
const { customerCell, placeCell } = vm.runInContext(`${source}\n;ShipmentList`, context);
const id = '14900000-0000-4000-8000-000000000001';

test('a resolved customer shows name and code and never an id', () => {
    const html = customerCell({ state: 'resolved', name: 'Acme <b>', code: 'C-1', id });
    assert.match(html, /Acme &lt;b&gt;/);
    assert.match(html, /C-1/);
    assert.doesNotMatch(html, new RegExp(id));
});

test('an old record shows its shipToReference as stored, marked', () => {
    const html = customerCell({ state: 'oldRecord', reference: 'warehouse-outbound:7:ship-to' });
    assert.match(html, /OLD/);
    assert.match(html, /warehouse-outbound:7:ship-to/);
});

test('not found and unavailable customers show only the localized text', () => {
    assert.match(customerCell({ state: 'notFound' }), /NOTFOUND/);
    assert.match(customerCell({ state: 'unavailable', reference: id }), /UNAVAILABLE/);
    assert.doesNotMatch(customerCell({ state: 'unavailable', reference: id }), new RegExp(id));
    assert.match(customerCell(undefined), /UNAVAILABLE/);
});

test('a warehouse that could not be read shows "name unavailable" and its reference, small and LTR', () => {
    const html = placeCell({ state: 'unavailable', reference: 'wh-01' });
    assert.match(html, /UNAVAILABLE/);
    assert.match(html, /<small class="text-muted" dir="ltr">wh-01<\/small>/);
    assert.equal(placeCell({ state: 'resolved', label: 'WH-1 · Main' }), 'WH-1 · Main');
});
