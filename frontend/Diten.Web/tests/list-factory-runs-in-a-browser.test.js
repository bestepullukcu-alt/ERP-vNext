const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * THE FACTORY MUST RUN WHERE IT IS SHIPPED — the dt-defaults-runs-in-a-browser.test.js pattern, applied to
 * diten-datatable.js now that every migrated list stands on it (BL-440 package 2).
 *
 * The lesson it copies: a `global.document` written into dt-defaults.js on 2026-09-23 passed every vitest test
 * (Node has `global`) and threw ReferenceError on every list page (a browser does not). The factory is loaded here in
 * a FRESH vm context that carries what a browser page carries and nothing Node adds, and its entry points are
 * exercised there.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SOURCE_PATH = web("wwwroot", "assets", "js", "diten-datatable.js");

describe("diten-datatable.js (the list factory) runs with a browser's globals and nothing more", () => {
  let DitenDataTable;
  let sandbox;

  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    sandbox = {
      window, document, $: window.jQuery, jQuery: window.jQuery, console, setTimeout, clearTimeout,
      requestAnimationFrame: (fn) => fn(),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } },
      DataTable: function () {}
    };
    // NOT runInThisContext: a new context has no `global`, no `process`, no `require` — a page's world.
    vm.runInNewContext(fs.readFileSync(SOURCE_PATH, "utf8"), sandbox, { filename: SOURCE_PATH });
    DitenDataTable = window.DitenDataTable;
  });

  test("the file loads at all in that world, with the factory entry points on window.DitenDataTable", () => {
    expect(typeof DitenDataTable?.createList).toBe("function");
    expect(typeof DitenDataTable?.createViewState).toBe("function");
    expect(typeof DitenDataTable?.createCrudTable).toBe("function");
  });

  test("the pure state machine runs there", () => {
    const state = DitenDataTable.createViewState({ fields: [{ key: "a", kind: "single" }], saveViewColumnIndexes: [2], totalColumnCount: 3 });
    expect(() => state.serializeView({ filters: { a: 1 } })).not.toThrow();
    expect(state.isDirty({ filters: { a: "1" } }, { filters: { a: 1 } })).toBe(false);
  });

  test("createList's fail-closed exits run there too (they must not reach for anything Node-only)", async () => {
    await expect(DitenDataTable.createList({ tableEl: document.createElement("table"), dataMode: "server" })).rejects.toThrow(/package 3/);
    await expect(DitenDataTable.createList({ tableEl: document.createElement("table") })).rejects.toThrow(/required/);
  });

  test("createList builds a client-mode list end to end on a page with the golden markup", async () => {
    document.body.innerHTML =
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse"><select id="filterStatus" multiple></select><select id="filterPriority"></select>' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><table id="dt-x" class="datatables-x"><thead><tr><th></th><th></th><th>A</th><th>B</th></tr></thead></table></div></div>';
    const tableEl = document.getElementById("dt-x");
    const api = {
      column: () => ({ visible: () => true }), order: () => [[2, "asc"]], search: () => "", draw() {}, columns: { adjust() {} },
      table: () => ({ container: () => document.querySelector(".card-datatable") }), on() { return this; }, row: () => ({ data: () => null })
    };
    let captured = null;
    window.DtDefaults = {
      create: (cfg) => { captured = cfg; return cfg; },
      exportButtons: () => [],
      updateVisualState() {},
      refreshButtonGroupRadii() {}
    };
    // The factory names `DataTable` bare, as a page does; in the sandbox that is the context's own global.
    sandbox.DataTable = function (el, cfg) { Object.assign(this, api); this.cfg = cfg; };
    window.personalizationClient = { getViews: async () => [] };

    const list = await DitenDataTable.createList({
      tableEl, dataMode: "client",
      filters: { fields: [{ id: "filterStatus", key: "status", kind: "multi" }, { id: "filterPriority", key: "priority", kind: "single" }] },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [2, 3], defaultVisibleColumnIndexes: [2, 3], baseOrder: [[2, "asc"]] },
      config: { columns: [{ data: "id" }, { data: "id" }, { data: "a" }, { data: "b" }] }
    });

    expect(list.dt).toBeTruthy();
    expect(captured.stateSave, "stateSave must be written false explicitly").toBe(false);
    expect(captured.colReorder).toEqual({ columns: ":gt(1):not(:last-child)" });
    expect(list.state.totalColumnCount, "column count comes from the page's columns").toBe(4);
    expect(() => captured.initComplete.call({ api: () => api }, {}, {})).not.toThrow();
    expect(() => captured.drawCallback.call({ api: () => api }, {})).not.toThrow();
  });

  test("the source names no Node-only global (the fast explanation of the tests above)", () => {
    const source = fs.readFileSync(SOURCE_PATH, "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    expect(source, "a browser has no `global`; use `window` or `document`").not.toMatch(/\bglobal\s*\./);
    expect(source).not.toMatch(/\bprocess\s*\./);
    expect(source).not.toMatch(/\brequire\s*\(/);
    expect(source, "no ESM: the file is a plain script both layouts load with <script>").not.toMatch(/^\s*(import|export)\b/m);
  });
});
