// WP-MVP1-FIX-LIVE3 (live finding 1) — the LOT cell of the movement history, run by Node against the SHIPPED
// frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/index.js with the list factory faked at its edge: a registered lot shows its
// TRACE number (a link to the lot for a reader of lots, the id only as the title); a registered lot whose number could not be read says
// "unknown lot" with the id only as the title — never the id as text; a typed lot text stays text. StockMovementsLotTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/index.js'), 'utf8');
const LOT = 'dfb1b684-0000-4000-8000-000000000001';

const lotRender = async (keys) => {
    let options = null;
    let ready = null;
    const window = {
        L10n: { UnknownLot: 'Bilinmeyen lot', LotUnregisteredBadge: 'Kayıtsız', LotUnregisteredHint: 'Kayıtsız lot' },
        Permissions: { has: (key) => keys.includes(key) },
        DataTable: { isDataTable: () => false },
        DitenDataTable: { getAuthHeaders: () => ({}), renderActions: () => '', createList: async (o) => { options = o; return { dt: null }; } },
        StockMovementPeople: { html: () => '' }
    };
    const document = {
        querySelector: (s) => (s === '.datatables-stockmovements' ? {} : null),
        getElementById: () => null,
        documentElement: { lang: 'tr' },
        body: {},
        addEventListener: (event, fn) => { if (event === 'DOMContentLoaded') ready = fn; }
    };
    const fetch = async () => ({ ok: false, json: async () => null });
    vm.runInContext(source, vm.createContext({ window, document, fetch, console, setTimeout, Intl, URLSearchParams }));
    ready();
    await new Promise((resolve) => setTimeout(resolve, 0));
    const def = options.config.columnDefs.find((d) => d.targets === 8);
    assert.equal(options.config.columns[8].data, 'lotId');
    return (lotId, full) => def.render(lotId, 'display', { lotId, ...full });
};

test('a registered lot shows its number, a link to the lot for a reader of lots, the id only as the title', async () => {
    const render = await lotRender(['inventory.stock.read', 'trace.lots.read']);
    assert.equal(render(LOT, { lotNumber: 'AMX-2511' }), `<a href="/Lots/Details?lotId=${LOT}" title="${LOT}">AMX-2511</a>`);
    const plain = await lotRender(['inventory.stock.read']);
    assert.equal(plain(LOT, { lotNumber: 'AMX-2511' }), `<span title="${LOT}">AMX-2511</span>`);
});

test('a registered lot whose number could not be read says unknown lot — the id only as the title, never as text', async () => {
    const render = await lotRender(['trace.lots.read']);
    const html = render(LOT, { lotUnresolved: true });
    assert.equal(html, `<span title="${LOT}">Bilinmeyen lot</span>`);
    assert.ok(!html.replace(/<[^>]*>/g, '').includes(LOT.slice(0, 8)));
});

test('a typed lot text stays text with the unregistered badge; no lot → empty', async () => {
    const render = await lotRender(['trace.lots.read']);
    assert.match(render('L-OLD-7', {}), /^L-OLD-7 <span class="badge bg-label-secondary ms-1" title="Kayıtsız lot">Kayıtsız<\/span>$/);
    assert.equal(render('', {}), '');
});
