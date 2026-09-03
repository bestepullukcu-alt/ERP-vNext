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

const initiativeIndex = readFileSync(
    new URL('../../Views/PPM/Initiatives/Index.cshtml', import.meta.url),
    'utf8'
);
const sharedForm = readFileSync(
    new URL('../../Views/PPM/Shared/_CreateEditOffcanvas.cshtml', import.meta.url),
    'utf8'
);
const initiativeScript = readFileSync(
    new URL('../../wwwroot/assets/js/PPM/Initiatives/index.js', import.meta.url),
    'utf8'
);
const sharedCrud = readFileSync(
    new URL('../../wwwroot/assets/js/PPM/ppm-crud.js', import.meta.url),
    'utf8'
);

const initiativeDom = new JSDOM('<!doctype html><html><body></body></html>', {
    runScripts: 'outside-only',
    url: 'http://localhost/ppm/initiatives'
});
let initiativeConfig;
initiativeDom.window.PpmCrud = {
    mount: config => { initiativeConfig = config; }
};
initiativeDom.window.eval(initiativeScript);
initiativeDom.window.document.dispatchEvent(new initiativeDom.window.Event('DOMContentLoaded'));

assert.equal(initiativeConfig.resource, 'initiatives');
assert.equal(initiativeConfig.endpoint, '/PPM/Initiatives/api');
assert.equal(initiativeConfig.hasPortfolio, true);
assert.deepEqual(
    JSON.parse(JSON.stringify(initiativeConfig.transitions)),
    {
        Proposed: ['Active', 'Cancelled'],
        Active: ['OnHold', 'Completed', 'Cancelled'],
        OnHold: ['Active', 'Completed', 'Cancelled'],
        Completed: [],
        Cancelled: []
    }
);

assert.match(initiativeIndex, /Layout = "_LayoutTenantShell"/);
assert.match(initiativeIndex, /HasPortfolio = true/);
assert.match(initiativeIndex, /RequiresPortfolio = false/);
assert.match(initiativeIndex, /ShowsVisibilityPolicy = false/);
assert.match(sharedForm, /required="@\(Model\.RequiresPortfolio \? "required" : null\)"/);
assert.match(sharedForm, /@if \(Model\.ShowsVisibilityPolicy\)/);

assert.match(initiativeScript, /const endpoint = '\/PPM\/Initiatives\/api'/);
assert.doesNotMatch(initiativeScript, /localhost|5062|Bearer|document\.cookie/);
assert.match(initiativeScript, /Proposed: \['Active', 'Cancelled'\]/);
assert.match(initiativeScript, /Active: \['OnHold', 'Completed', 'Cancelled'\]/);
assert.match(initiativeScript, /OnHold: \['Active', 'Completed', 'Cancelled'\]/);
assert.match(initiativeScript, /Completed: \[\], Cancelled: \[\]/);

for (const status of [401, 403, 404, 409, 503]) {
    assert.match(sharedCrud, new RegExp(`${status}:`));
}
assert.match(sharedCrud, /state\.lookupBlocked \|\| !form\.checkValidity\(\)/);
assert.match(sharedCrud, /credentials: 'same-origin'/);
assert.doesNotMatch(sharedCrud, /init-001|prj-001/i);

console.log('PPM Add New delegated-click jsdom test: PASS');
