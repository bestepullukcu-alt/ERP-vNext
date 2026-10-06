const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * THE LIST FACTORY'S STATE MACHINE IS THE STATE STANDARD — BL-440 package 2 (owner decision 2026-09-23).
 *
 * `.antigravity/rules/frontend-datatable-template.md` §"DataTable v2 State Standard" was, until now, a paragraph
 * every list re-implemented by hand: 79 of the 108 top-level names in the Users screen were the Save View machine
 * copied from Golden Slim. Where copies diverged, bugs were born silently — a view that saved wrong, a dirty state
 * that never showed its button. This suite measures the ONE implementation, `DitenDataTable.createViewState`,
 * against the standard's own sentences, and pins the fail-closed data mode of `createList`.
 *
 * Every assertion runs against the production file loaded in a browser-shaped sandbox (no `global`, no `require`).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SOURCE = web("wwwroot", "assets", "js", "diten-datatable.js");

function loadFactory(extra = {}) {
  const win = { L10n: {} };
  const sandbox = { window: win, document: { addEventListener() {}, querySelector: () => null, getElementById: () => null }, console, setTimeout, clearTimeout, requestAnimationFrame: (fn) => fn(), ...extra };
  vm.runInNewContext(fs.readFileSync(SOURCE, "utf8"), sandbox, { filename: SOURCE });
  return win.DitenDataTable;
}

const SPEC = {
  fields: [{ id: "filterStatus", key: "status", kind: "multi" }, { id: "filterPriority", key: "priority", kind: "single" }],
  saveViewColumnIndexes: [2, 3, 4],
  defaultVisibleColumnIndexes: [2, 3],
  totalColumnCount: 6,
  baseOrder: [[2, "asc"]]
};

describe("dataMode is fail-closed (the same discipline as the shell's DataMode)", () => {
  const D = loadFactory();

  test("missing → throws, naming the two allowed values", () => {
    expect(() => D.assertDataMode(undefined)).toThrow(/dataMode is required/);
    expect(() => D.assertDataMode("")).toThrow(/dataMode is required/);
  });

  test("a typo → throws; 'client' and 'server' → accepted (server mode is package 3, WP-UI-LIST-SERVER-01)", () => {
    expect(() => D.assertDataMode("clint")).toThrow(/got 'clint'/);
    expect(() => D.assertDataMode("Server")).toThrow(/got 'Server'/);
    expect(D.assertDataMode("client")).toBe("client");
    expect(D.assertDataMode("server")).toBe("server");
  });

  test("createList refuses before touching anything when dataMode is missing; 'server' passes the mode gate", async () => {
    await expect(D.createList({ tableEl: { id: "t", dataset: {} } })).rejects.toThrow(/dataMode is required/);
    // Past the mode gate the next requirement is DtDefaults — proof the mode itself was accepted.
    await expect(D.createList({ tableEl: { id: "t", dataset: {} }, dataMode: "server" })).rejects.toThrow(/DtDefaults is required/);
  });

  test("createCrudTable (the 84 legacy callers) validates a dataMode it is handed, never invents one", () => {
    expect(() => D.createCrudTable({ tableEl: { id: "t", dataset: {} }, dataMode: "serv" })).toThrow(/got 'serv'/);
    expect(() => D.createCrudTable({ tableEl: { id: "t", dataset: {} }, dataMode: "server" })).toThrow(/DtDefaults is required/);
    expect(() => D.createCrudTable({ tableEl: { id: "t", dataset: {} } })).toThrow(/DtDefaults is required/);
  });
});

describe("normalize() — the standard's mechanics, measured on the production function", () => {
  const D = loadFactory();
  const state = D.createViewState(SPEC);

  test("null | undefined | '' → '' and strings are trimmed", () => {
    expect(D.normalizeScalar(null)).toBe("");
    expect(D.normalizeScalar(undefined)).toBe("");
    expect(D.normalizeScalar("")).toBe("");
    expect(D.normalizeScalar("  a ")).toBe("a");
  });

  test("1 ≡ \"1\" and boolean → \"true\"/\"false\"", () => {
    expect(D.normalizeScalar(1)).toBe("1");
    expect(D.normalizeScalar(true)).toBe("true");
    expect(D.normalizeScalar(false)).toBe("false");
    expect(state.serializeView({ filters: { priority: 1 } })).toBe(state.serializeView({ filters: { priority: "1" } }));
    expect(state.serializeView({ filters: { status: [true] } })).toBe(state.serializeView({ filters: { status: ["true"] } }));
  });

  test("multi values dedupe and drop empties; a scalar handed to a multi becomes a one-item list", () => {
    expect(D.normalizeArray(["a", " a", "", null, "b"])).toEqual(["a", "b"]);
    expect(state.normalizeFilters({ status: "Active" }).status).toEqual(["Active"]);
  });

  test("sorting: dir lower-cased, index numeric; unknown → baseOrder", () => {
    expect(state.normalizeViewState({ order: [["3", "DESC"]] }).order).toEqual([[3, "desc"]]);
    expect(state.normalizeViewState({ order: "garbage" }).order).toEqual([[2, "asc"]]);
  });

  test("columnOrder must carry every column index exactly once, else the identity order", () => {
    expect(state.normalizeViewState({ columnOrder: [5, 4, 3, 2, 1, 0] }).columnOrder).toEqual([5, 4, 3, 2, 1, 0]);
    expect(state.normalizeViewState({ columnOrder: [0, 0, 1, 2, 3, 4] }).columnOrder).toEqual([0, 1, 2, 3, 4, 5]);
    expect(state.normalizeViewState({ columnOrder: [0, 1] }).columnOrder).toEqual([0, 1, 2, 3, 4, 5]);
  });

  test("colVis accepts the index-based array AND the object form; default = the init visibility", () => {
    expect(state.defaultColVis()).toEqual({ 2: true, 3: true, 4: false });
    expect(state.normalizeColVis([false, false, true, false, true])).toEqual({ 2: true, 3: false, 4: true });
    expect(state.normalizeColVis({ 3: false })).toEqual({ 3: false });
    expect(state.normalizeViewState({}).colVis).toEqual({ 2: true, 3: true, 4: false });
  });

  test("keys are sorted before stringify, so insertion order cannot make two equal views differ", () => {
    expect(state.serializeView({ filters: { priority: "1", status: ["a"] } }))
      .toBe(state.serializeView({ filters: { status: ["a"], priority: "1" } }));
  });
});

describe("dirty-state and Reset — the standard's two hardest sentences", () => {
  const D = loadFactory();
  const state = D.createViewState(SPEC);

  test("isDirty = normalize(applied) ≠ normalize(saved ‖ baseline)", () => {
    expect(state.isDirty({ filters: {} }, null)).toBe(false);
    expect(state.isDirty({ filters: { status: ["Active"] } }, null)).toBe(true);
    const saved = { filters: { status: ["Active"] }, search: "", colVis: null, columnOrder: null, order: null };
    expect(state.isDirty({ filters: { status: [" Active "] } }, saved)).toBe(false);
    expect(state.isDirty({ filters: { status: ["Active"] }, search: "x" }, saved)).toBe(true);
  });

  test("the baseline is the factory table state: empty filters, empty search, default colVis, identity order, baseOrder", () => {
    expect(state.baseline()).toEqual({
      filters: { status: [], priority: "" },
      search: "",
      colVis: { 2: true, 3: true, 4: false },
      columnOrder: [0, 1, 2, 3, 4, 5],
      order: [[2, "asc"]]
    });
  });

  test("Reset goes to the BASELINE, never back to the saved view (measured in the source, not a copy)", () => {
    const source = fs.readFileSync(SOURCE, "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    const reset = source.match(/resetBtn \|\| 'btnFilterReset'\)\?\.addEventListener\('click', function \(e\) \{([\s\S]*?)\n\s*\}\);/);
    expect(reset, "the Reset binding is gone from the factory").toBeTruthy();
    expect(reset[1]).toMatch(/e\.preventDefault\(\)/);
    expect(reset[1]).toMatch(/applyState\(api, state\.baseline\(\)\)/);
    expect(reset[1], "Reset must not restore store.saved").not.toMatch(/store\.saved/);
    expect(reset[1]).toMatch(/syncDirty\(api\)/);
  });

  test("Apply: staged → applied, redraw, panel closes, dirty follows the APPLIED state; a staged change alone does nothing", () => {
    const source = fs.readFileSync(SOURCE, "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    const apply = source.match(/applyBtn \|\| 'btnFilterApply'\)\?\.addEventListener\('click', function \(\) \{([\s\S]*?)\n\s*\}\);/);
    expect(apply, "the Apply binding is gone from the factory").toBeTruthy();
    expect(apply[1]).toMatch(/appliedFilters = state\.normalizeFilters\(readStagedFilters\(fields\)\)/);
    expect(apply[1]).toMatch(/api\.draw\(\)/);
    expect(apply[1]).toMatch(/syncDirty\(api\)/);
    expect(apply[1]).toMatch(/collapseOf\(collapseId\)\?\.hide\(\)/);
    // No filter-control `change` handler may touch the Save View button.
    expect(source).not.toMatch(/change[^\n]*\n[^\n]*setSaveVisible/);
    expect(source).not.toMatch(/change\.saveFilter/);
  });

  test("from a saved record: either casing, definition as object or JSON string", () => {
    const asObject = state.fromSavedRecord({ ViewDefinition: { filters: { status: ["A"] }, search: " q " } });
    const asString = state.fromSavedRecord({ viewDefinition: JSON.stringify({ filters: { status: ["A"] }, search: " q " }) });
    expect(asObject.filters).toEqual({ status: ["A"], priority: "" });
    expect(asObject.search).toBe("q");
    expect(state.serializeView(asObject)).toBe(state.serializeView(asString));
  });
});

describe("persistence decisions — measured in the factory source", () => {
  const source = () => fs.readFileSync(SOURCE, "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");

  test("stateSave:false is written explicitly on every list the factory builds", () => {
    const config = source().match(/var config = Object\.assign\(\{\}, pageConfig, \{([\s\S]*?)initComplete:/);
    expect(config, "the config block is gone").toBeTruthy();
    expect(config[1]).toMatch(/stateSave:\s*false/);
  });

  test("colReorder ':gt(1):not(:last-child)' is the default, the Save View button is ALWAYS rendered, and it starts hidden", () => {
    const s = source();
    expect(s).toMatch(/colReorder: pageConfig\.colReorder \|\| \{ columns: ':gt\(1\):not\(:last-child\)' \}/);
    expect(s).toMatch(/saveFilterBtn: \{[\s\S]*className: 'btn btn-label-primary d-none dt-save-filter-btn'/);
    expect(s).toMatch(/exportButtons\([\s\S]*extraButtons,/);
  });

  test("the saved view is loaded BEFORE the table is built, and persisted ONLY through personalizationClient", () => {
    const s = source();
    const loadAt = s.indexOf("await store.load()");
    const buildAt = s.indexOf("dt = createCrudTable({");
    expect(loadAt).toBeGreaterThan(-1);
    expect(buildAt).toBeGreaterThan(loadAt);
    expect(s).toMatch(/client\(\)\.saveView\(payload\)/);
    expect(s).toMatch(/client\(\)\.updateView\(existingId, payload\)/);
    expect(s, "no localStorage persistence").not.toMatch(/localStorage/);
  });

  test("the saved payload is filters+search+colVis+columnOrder+order and nothing about paging", () => {
    const s = source();
    expect(s).toMatch(/viewDefinition: normalized/);
    expect(s).toMatch(/return \{\s*filters: normalizeFilters\(view\?\.filters\),\s*search: normalizeScalar\(view\?\.search\),\s*colVis:[\s\S]*columnOrder:[\s\S]*order: normalizeOrder/);
    expect(s).not.toMatch(/pageLength|displayStart/);
  });
});
