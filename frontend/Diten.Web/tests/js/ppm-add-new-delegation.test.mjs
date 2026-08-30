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

console.log('PPM Add New delegated-click jsdom test: PASS');
