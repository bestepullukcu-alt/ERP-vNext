import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

const dom = new JSDOM(`<!doctype html><html><body>
  <div id="offcanvasCreateEdit"></div>
  <div id="offcanvasDetailsPreview"></div>
  <div id="ppm-table-alert" class="d-none"></div>
  <div id="ppm-form-alert" class="d-none"></div>
  <div id="offcanvasCreateEditLabel"></div>
  <form id="formPpm"><input name="__RequestVerificationToken" value="token"></form>
  <input id="ppmId"><input id="ppmVersion">
  <select id="ppmLifecycleState"><option value="Draft">Draft</option></select>
  <div class="dt-container">
    <button type="button" class="add-new">Add</button>
    <table class="datatables-ppm"><tbody><tr data-ppm-loading-row><td>Loading</td></tr></tbody></table>
  </div>
</body></html>`, { runScripts: 'outside-only', url: 'http://localhost/ppm/portfolios' });

const { window } = dom;
let showCount = 0;
window.bootstrap = {
    Offcanvas: { getOrCreateInstance: () => ({ show: () => { showCount += 1; }, hide: () => { } }) },
    Collapse: { getOrCreateInstance: () => ({ toggle: () => { }, hide: () => { } }) }
};
window.DtDefaults = {
    exportButtons: () => [],
    create: (config) => config
};
const dt = {
    on: () => dt,
    search: value => value === undefined ? '' : dt,
    columns: () => ({ visible: () => ({ toArray: () => [] }) }),
    column: () => ({ visible: () => dt }),
    row: () => ({ data: () => null }),
    order: value => value === undefined ? [] : dt,
    draw: () => dt,
    colReorder: { order: () => [] },
    ajax: { reload: callback => callback?.() }
};
let crudOptions;
window.DitenDataTable = {
    createCrudTable: (options) => { crudOptions = options; return dt; },
    renderActions: () => ''
};

const jquery = () => ({
    val: () => jquery(),
    trigger: () => jquery(),
    prop: () => jquery(),
    select2: () => jquery()
});
jquery.fn = { dataTable: { ext: { search: [] } } };
window.$ = jquery;
window.jQuery = null;

window.eval(readFileSync(
    new URL('../../wwwroot/assets/js/PPM/ppm-crud.js', import.meta.url),
    'utf8'
));

await window.PpmCrud.mount({
    resource: 'portfolios',
    defaultLifecycle: 'Draft',
    transitions: { Draft: [] }
});

window.document.querySelector('.add-new').click();
await Promise.resolve();

assert.equal(showCount, 1, 'Add New must open the create offcanvas exactly once.');
assert.equal(
    window.document.querySelector('.datatables-ppm').dataset.ppmAddNewBound,
    'true',
    'The delegated handler must be marked as bound on its PPM table.'
);

crudOptions.ajax.error({ status: 404 });
assert.equal(
    window.document.querySelector('[data-ppm-loading-row]'),
    null,
    'The initial loading row must be removed after a failed request.'
);
assert.equal(
    window.document.querySelector('.datatables-ppm tbody').classList.contains('d-none'),
    true,
    'The failed table body must be hidden while the honest error banner is shown.'
);

const initiativeScript = readFileSync(
    new URL('../../wwwroot/assets/js/PPM/Initiatives/index.js', import.meta.url),
    'utf8'
);
const sharedCrud = readFileSync(
    new URL('../../wwwroot/assets/js/PPM/ppm-crud.js', import.meta.url),
    'utf8'
);

const initiativeDom = new JSDOM(`<!doctype html><html><body>
  <div id="offcanvasCreateEdit"></div><div id="offcanvasDetailsPreview"></div>
  <div id="skeleton-loader"></div><div id="initiative-table-alert" class="d-none"></div>
  <div id="initiative-form-alert" class="d-none"></div><div id="offcanvasCreateEditLabel"></div>
  <form id="formInitiative"><input name="__RequestVerificationToken" value="token"></form>
  <button id="btnSaveInitiative"></button><button id="btnFilterApply"></button><button id="btnFilterReset"></button>
  <select id="filterLifecycle"><option value="">All</option></select>
  <select id="initiativeType"><option value="">Type</option></select>
  <select id="initiativePriority"><option value="">Priority</option></select>
  <select id="initiativePortfolio"><option value="">Portfolio</option></select>
  <input id="initiativeId"><input id="initiativeVersion" value="1"><input id="initiativeCode"><input id="initiativeName">
  <textarea id="initiativeDescription"></textarea><input id="initiativeStart"><input id="initiativeEnd">
  <table id="dt-initiatives"></table>
</body></html>`, {
    runScripts: 'outside-only',
    url: 'http://localhost/ppm/initiatives'
});
const iw = initiativeDom.window;
iw.L10n = { ClassificationUnavailable: 'classification unavailable', LifecycleUnavailable: 'lifecycle unavailable', States: {}, ViewDetails: 'View', Edit: 'Edit', Delete: 'Delete', CreateSuccessor: 'Successor' };
iw.bootstrap = window.bootstrap;
iw.DtDefaults = { exportButtons: () => [], refreshButtonGroupRadii: () => {} };
let initiativeCrudOptions;
let renderedActions;
iw.DitenDataTable = {
    createCrudTable: options => { initiativeCrudOptions = options; return dt; },
    renderActions: actions => { renderedActions = actions; return ''; }
};
const initiativeJquery = selector => {
    const nodes = typeof selector === 'string' ? [...iw.document.querySelectorAll(selector)] : [selector];
    const api = {
        val: value => value === undefined ? (nodes[0]?.value || []) : (nodes.forEach(node => { node.value = Array.isArray(value) ? value[0] || '' : value; }), api),
        trigger: () => api,
        prop: (name, value) => (nodes.forEach(node => { node[name] = value; }), api),
        select2: () => api,
        each: callback => (nodes.forEach((node, index) => callback.call(node, index, node)), api)
    };
    return api;
};
initiativeJquery.fn = { dataTable: { ext: { search: [] } } };
iw.$ = initiativeJquery;
let resolveLifecycle;
const lifecycleResponse = new Promise(resolve => { resolveLifecycle = resolve; });
const requestedUrls = [];
iw.fetch = async url => {
    requestedUrls.push(url);
    if (url.endsWith('/lifecycle-contracts/v2')) return lifecycleResponse;
    if (url.endsWith('/contracts/v2')) return { ok: false, status: 503, text: async () => '{}' };
    if (url === '/ppm/portfolios/api') return { ok: true, status: 200, text: async () => '[]' };
    throw new Error(`Unexpected fetch: ${url}`);
};
const initiativeReady = new Promise(resolve => iw.document.addEventListener('DOMContentLoaded', resolve, { once: true }));
iw.eval(initiativeScript);
await initiativeReady;
await Promise.resolve();

assert.deepEqual(requestedUrls, ['/PPM/Initiatives/api/lifecycle-contracts/v2']);
assert.equal(initiativeCrudOptions, undefined, 'Table initialization must wait for the lifecycle authority.');
resolveLifecycle({ ok: true, status: 200, text: async () => JSON.stringify({ allowedTargetStatesBySource: { Proposed: ['Active'] }, transitions: [] }) });
for (let i = 0; i < 8; i += 1) await new Promise(resolve => setTimeout(resolve, 0));

assert.deepEqual(requestedUrls, ['/PPM/Initiatives/api/lifecycle-contracts/v2', '/PPM/Initiatives/api/contracts/v2', '/ppm/portfolios/api']);
assert.ok(initiativeCrudOptions, 'Table initializes after the lifecycle authority resolves.');
assert.equal(iw.document.getElementById('filterLifecycle').options.length, 2, 'Lifecycle states must be populated from the endpoint payload.');
assert.equal(iw.document.getElementById('btnSaveInitiative').disabled, true);
assert.equal(iw.document.getElementById('initiativeType').disabled, true);
assert.equal(iw.document.getElementById('initiativePriority').disabled, true);
assert.equal(iw.document.getElementById('initiative-form-alert').textContent, 'classification unavailable');

const actionRenderer = initiativeCrudOptions.config.columnDefs.find(definition => definition.targets === 8).render;
actionRenderer(null, null, { id: 'initiative-1', lifecycleState: 'Proposed', version: 3, availableActions: [{ targetState: 'Active', availability: 'available', reasonCode: 'ready' }] });
assert.equal(renderedActions.filter(action => action.className.startsWith('js-initiative-transition')).length, 1);
assert.equal(renderedActions.find(action => action.className.startsWith('js-initiative-transition')).attrs['data-target'], 'Active');

assert.match(initiativeScript, /const endpoint = '\/PPM\/Initiatives\/api'/);
assert.doesNotMatch(initiativeScript, /localhost|5062|Bearer|document\.cookie/);
assert.match(initiativeScript, /row\.availableActions/);
assert.doesNotMatch(initiativeScript, /Proposed:\s*\[/);
assert.doesNotMatch(initiativeScript, /(?:const|let|var)\s+(?:cancellationReasons|holdReasons|completionOutcomes|closureReasons|benefitDispositions)\s*=/);

for (const status of [401, 403, 404, 409, 503]) {
    assert.match(sharedCrud, new RegExp(`${status}:`));
}
assert.match(sharedCrud, /state\.lookupBlocked \|\| !form\.checkValidity\(\)/);
assert.match(sharedCrud, /credentials: 'same-origin'/);
assert.doesNotMatch(sharedCrud, /init-001|prj-001/i);

console.log('PPM Add New delegated-click jsdom test: PASS');
