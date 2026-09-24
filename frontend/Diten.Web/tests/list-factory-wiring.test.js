const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * THE FACTORY'S WIRING — CT guards (2026-09-23, package 2 acceptance).
 *
 * The state suite measures the pure machine and the sandbox suite proves the file runs in a browser. Four
 * behaviours sat between them, measured by sabotage: each of the four lines below could be deleted from the
 * factory and 49/49 tests stayed green. They are the seams where a page's experience actually breaks:
 *   1. a search or a sort must recompute the dirty state (else Save View never appears after a search);
 *   2. the quick view must hand the row to the page's `populate` (else an empty panel opens);
 *   3. the saved-view store must pick the record marked default, not whichever came first;
 *   4. the client-side filter hook must be scoped to ITS table (else one list's filters empty every other
 *      table on the page — the ext.search array is global to DataTables).
 * Each test drives the production factory in the browser-shaped sandbox with a recording DataTable stub.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SOURCE_PATH = web("wwwroot", "assets", "js", "diten-datatable.js");
const tick = (ms = 5) => new Promise((r) => setTimeout(r, ms));

describe("the list factory's wiring (the seams the pure tests cannot see)", () => {
  let DitenDataTable;
  let sandbox;

  const page = () => {
    document.body.innerHTML =
      '<button class="dt-save-filter-btn d-none"></button>' +
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse">' +
      '<select id="filterStatus" multiple><option value="Active">A</option><option value="Passive">P</option></select>' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><table id="dt-w" class="datatables-w"><thead><tr><th></th><th></th><th>A</th><th>B</th></tr></thead></table></div></div>' +
      '<div class="card"><div class="card-datatable"><table id="dt-other"></table></div></div>' +
      '<div id="ocq"></div>';
    return document.getElementById("dt-w");
  };

  // A DataTable stub that RECORDS what the factory wires: event handlers and search() calls.
  const stub = () => {
    const handlers = {};
    const searchCalls = [];
    const api = {
      _handlers: handlers, _searchCalls: searchCalls,
      column: () => ({ visible: () => true }), order: () => [[2, "asc"]],
      search: (v) => { if (v !== undefined) { searchCalls.push(v); api._search = v; return api; } return api._search || ""; },
      // A real draw fires search/order events (that is how DataTables reports a redraw); the dirty recompute hangs on them.
      draw() { api.emit("search.dt"); api.emit("order.dt"); }, columns: { adjust() {} },
      table: () => ({ container: () => document.querySelector(".card-datatable") }),
      on(events, fn) { String(events).split(/\s+/).forEach((e) => { (handlers[e] = handlers[e] || []).push(fn); }); return api; },
      emit(e) { (handlers[e] || []).forEach((fn) => fn()); },
      row: () => ({ data: () => null })
    };
    return api;
  };

  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    sandbox = {
      window, document, $: window.jQuery, jQuery: window.jQuery, console, setTimeout, clearTimeout,
      requestAnimationFrame: (fn) => fn(),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } },
      DataTable: function () {}
    };
    vm.runInNewContext(fs.readFileSync(SOURCE_PATH, "utf8"), sandbox, { filename: SOURCE_PATH });
    DitenDataTable = window.DitenDataTable;
  });

  const build = async (api, extra = {}) => {
    let captured = null;
    window.DtDefaults = { create: (cfg) => { captured = cfg; return cfg; }, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {} };
    sandbox.DataTable = function () { Object.assign(this, api); };
    window.jQuery.fn.dataTable = window.jQuery.fn.dataTable || { ext: { search: [] } };
    const tableEl = page();
    const list = await DitenDataTable.createList(Object.assign({
      tableEl, dataMode: "client",
      filters: { fields: [{ id: "filterStatus", key: "status", kind: "multi" }] },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [2, 3], defaultVisibleColumnIndexes: [2, 3], baseOrder: [[2, "asc"]] },
      config: { columns: [{ data: "id" }, { data: "id" }, { data: "a" }, { data: "b" }] }
    }, extra));
    captured.initComplete.call({ api: () => api }, {}, {});
    await tick(); // the factory arms dirty-tracking one tick after initComplete, and setupFilters is async
    return { list, tableEl, captured };
  };

  test("1. a search recomputes the dirty state — the Save View button appears", async () => {
    window.personalizationClient = { getViews: async () => [] };
    const api = stub();
    const { list } = await build(api);
    const btn = document.querySelector(".dt-save-filter-btn");
    expect(btn.classList.contains("d-none"), "clean list: hidden").toBe(true);

    api._search = "ali"; // what the reader typed, as api.search() now reports it
    api.emit("search.dt");

    expect(btn.classList.contains("d-none"), "a search changed the applied state; Save View must show").toBe(false);
    expect(list.dt._handlers["order.dt"]?.length, "a sort must be wired the same way").toBeGreaterThan(0);
  });

  test("2. the quick view hands the row to the page's populate", async () => {
    window.personalizationClient = { getViews: async () => [] };
    const populated = [];
    const { list } = await build(stub(), { quickView: { offcanvasId: "ocq", populate: (row) => populated.push(row) } });

    list.showQuickView({ id: 7, name: "seven" });

    expect(populated, "the panel opened empty").toEqual([{ id: 7, name: "seven" }]);
  });

  test("3. the saved-view store applies the record marked DEFAULT, not the first one returned", async () => {
    window.personalizationClient = {
      getViews: async () => [
        { id: "first", isDefault: false, viewDefinition: { search: "first" } },
        { id: "chosen", isDefault: true, viewDefinition: { search: "chosen" } }
      ]
    };
    const api = stub();
    await build(api);
    await tick(20);

    expect(api._searchCalls, "the saved view applied to the table must be the default one").toContain("chosen");
    expect(api._searchCalls).not.toContain("first");
  });

  test("3b. a page that opens ON its saved view is not dirty — the Save View button stays hidden (CT, measured live)", async () => {
    // Live on Golden Slim: save a view with status=[Passive], reload → the filter was applied, the badge said 1, and the
    // Save View button was VISIBLE although captured == saved (serialized byte-equal). Cause: applyState assigned
    // `appliedFilters` AFTER applyViewToTable's draw, and that draw's events recomputed dirty against the OLD filters.
    window.personalizationClient = {
      getViews: async () => [{ id: "v", isDefault: true, viewDefinition: { filters: { status: ["Passive"] }, search: "" } }]
    };
    const api = stub();
    // The page's lookup fetch is what puts applyState AFTER arming on a real page; without an await here the saved
    // view is applied synchronously inside initComplete, before dirty-tracking is armed, and the bug cannot show.
    await build(api, { filters: { fields: [{ id: "filterStatus", key: "status", kind: "multi" }], loadOptions: async () => { await tick(); } } });
    await tick(20);

    const btn = document.querySelector(".dt-save-filter-btn");
    expect(btn.classList.contains("d-none"), "applied == saved, so nothing to save").toBe(true);
  });

  test("4. the filter hook is scoped to ITS table — another table on the page is never filtered by it", async () => {
    window.personalizationClient = { getViews: async () => [] };
    window.jQuery.fn.dataTable = { ext: { search: [] } };
    const api = stub();
    const { tableEl } = await build(api);
    const hook = window.jQuery.fn.dataTable.ext.search.at(-1);
    expect(typeof hook, "the factory registers the client-side filter hook").toBe("function");

    window.jQuery("#filterStatus").val(["Active"]);
    document.getElementById("btnFilterApply").click(); // staged → applied

    const other = document.getElementById("dt-other");
    expect(hook({ nTable: tableEl }, null, 0, { status: "Passive" }), "this table: the applied filter rejects the row").toBe(false);
    expect(hook({ nTable: other }, null, 0, { status: "Passive" }), "another table: untouched by this list's filters").toBe(true);
  });
});
