// WP-MVP1-FU-CLEAN2 (f, StockMovements DataTable conformance) — the movement history talks to its table through the DataTables v2 API only,
// run by Node against the SHIPPED frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/index.js with the list factory faked at its
// edge (DitenDataTable.createList / getAuthHeaders, window.DataTable): a failed FIRST load removes the export menu through `new
// DataTable.Api(table)` (the jQuery plugin is never called), and the list keeps its behaviour — server paging, no sorting, the responsive
// control column, the "Kaydeden" column, the row actions. StockMovementsV2Tests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/index.js'), 'utf8');

const load = ({ failFirstLoad }) => {
    const seen = { apiTables: [], removed: [], jquery: 0, options: null, fetches: [] };
    const tableEl = { nodeName: 'TABLE', id: 'dt-stockmovements' };
    let ready = null;
    const window = {
        L10n: { UnknownUser: 'Bilinmeyen kullanıcı' },
        Permissions: { has: () => true },
        jQuery: () => { seen.jquery++; throw new Error('the jQuery DataTable plugin was called'); },
        DataTable: Object.assign(function DataTable() { throw new Error('the list is built by the factory, not here'); }, {
            isDataTable: (el) => el === tableEl,
            Api: function Api(el) { seen.apiTables.push(el); return { buttons: (selector) => ({ remove: () => seen.removed.push(selector) }) }; }
        }),
        DitenDataTable: {
            getAuthHeaders: (json) => Object.assign({ 'X-Tenant-Id': 'tenant-1' }, json ? { 'Content-Type': 'application/json' } : {}),
            renderActions: () => '',
            createList: async (options) => {
                seen.options = options;
                if (failFirstLoad) options.ajax.error({ responseText: '{"error":{"message":"Servis yanıt vermedi"}}' });
                return { dt: null };
            }
        },
        StockMovementPeople: { html: () => '' }
    };
    const document = {
        querySelector: (selector) => (selector === '.datatables-stockmovements' ? tableEl : null),
        getElementById: () => null,
        documentElement: { lang: 'tr' },
        body: {},
        addEventListener: (event, fn) => { if (event === 'DOMContentLoaded') ready = fn; }
    };
    const fetch = async (url, init) => { seen.fetches.push({ url, init }); return { ok: false, json: async () => null }; };
    vm.runInContext(source, vm.createContext({ window, document, fetch, console, setTimeout, Intl }));
    ready();
    return { seen, tableEl };
};

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

test('a failed first load removes the export menu through the DataTables v2 Api, never the jQuery plugin', async () => {
    const { seen, tableEl } = load({ failFirstLoad: true });
    await settle();
    assert.equal(seen.jquery, 0);
    assert.deepEqual(seen.apiTables, [tableEl]);
    assert.deepEqual(seen.removed, ['.dt-export-collection-btn']);
});

test('the list keeps its behaviour: server paging, no sorting or search, the control column, Kaydeden, the row actions', async () => {
    const { seen } = load({ failFirstLoad: false });
    await settle();
    const o = seen.options;
    assert.equal(o.dataMode, 'server');
    assert.equal(o.ajax.url, '/StockMovements/api');
    assert.equal(o.config.ordering, false);
    assert.equal(o.config.searching, false);
    const columns = o.config.columns.map((c) => c.name);
    assert.equal(columns[0], 'control');
    assert.equal(columns[11], 'recordedBy');
    assert.equal(o.config.columns[11].data, 'createdByName');
    assert.equal(columns.at(-1), 'action');
    assert.ok(o.config.columnDefs.some((d) => d.targets === 0 && d.className === 'control'), 'the responsive row-detail control column');
    assert.equal(typeof o.actions.onRowAction, 'object');
    assert.equal(seen.jquery, 0);
});

test('the script holds no jQuery DataTable plugin call and sends the factory headers on its own calls', () => {
    assert.doesNotMatch(source, /\.(?:DataTable|dataTable)\s*\(/);
    assert.match(source, /new window\.DataTable\.Api\(tableEl\)/);
    assert.match(source, /const getAuthHeaders = \(includeJson = false\) => window\.DitenDataTable\?\.getAuthHeaders\?\.\(includeJson\) \|\| \{\};/);
    assert.equal((source.match(/headers: getAuthHeaders\(\)/g) || []).length, 2);
    assert.match(source, /headers: Object\.assign\(getAuthHeaders\(true\), \{ 'RequestVerificationToken': antiforgeryToken\(\) \}\)/);
});
