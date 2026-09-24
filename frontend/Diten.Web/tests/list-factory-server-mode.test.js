const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * SERVER MODE OF THE LIST FACTORY — WP-UI-LIST-SERVER-01 (BL-440 package 3).
 *
 * `createList({ dataMode: 'server' })` hands DataTables' serverSide request to the service as ONE flat query and hands
 * the service's envelope back to DataTables. A wrong translation is silent on screen — the pager says "37 of 1001"
 * with the wrong number, a filter "applies" and the rows do not change, the browser filters the current page a second
 * time. So each translation is measured on the production file, in a browser-shaped sandbox, and each is paired with
 * the client-mode control that proves the probe can tell the two modes apart:
 *   1. the request: start/length/search/orderBy/orderDir/draw + applied filters by key, repeated for multi — and no
 *      `columns[` noise;
 *   2. the response: recordsTotal = total, recordsFiltered = filteredTotal (NOT total), data = items, draw kept;
 *   3. no ext.search hook in server mode (the control: client mode registers one);
 *   4. Apply → a redraw whose request carries the new filters;
 *   5. Save View serializes exactly as in client mode;
 *   6. the saved view's filters ride the FIRST request; lastResponse/onResponse expose the envelope.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const SOURCE_PATH = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "diten-datatable.js");
const tick = (ms = 5) => new Promise((r) => setTimeout(r, ms));

/** What DataTables 2 sends for a serverSide draw — including the positional `columns` dump the service must not see. */
const dtRequest = (over = {}) => Object.assign({
  draw: 3,
  start: 20,
  length: 10,
  search: { value: "  Alpha ", regex: false, fixed: [] },
  order: [{ column: 3, dir: "desc", name: "name" }],
  columns: [
    { data: "id", name: "control", searchable: false, orderable: false, search: { value: "", regex: false } },
    { data: "id", name: "checkbox", searchable: false, orderable: false, search: { value: "", regex: false } },
    { data: "code", name: "code", searchable: true, orderable: true, search: { value: "", regex: false } },
    { data: "name", name: "name", searchable: true, orderable: true, search: { value: "", regex: false } }
  ]
}, over);

const parseQuery = (qs) => qs.split("&").map((pair) => pair.split("=").map(decodeURIComponent));

describe("the list factory in server mode (BL-440 package 3)", () => {
  let D;
  let sandbox;

  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    sandbox = {
      window, document, $: window.jQuery, jQuery: window.jQuery, console, setTimeout, clearTimeout,
      requestAnimationFrame: (fn) => fn(),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } },
      DataTable: function () {}
    };
    vm.runInNewContext(fs.readFileSync(SOURCE_PATH, "utf8"), sandbox, { filename: SOURCE_PATH });
    D = window.DitenDataTable;
  });

  const FIELDS = [
    { id: "filterStatus", key: "status", kind: "multi" },
    { id: "filterPriority", key: "priority", kind: "single" },
    { id: "filterOwner", key: "owner", kind: "multi" }
  ];

  const page = () => {
    document.body.innerHTML =
      '<button class="dt-save-filter-btn d-none"></button>' +
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse">' +
      '<select id="filterStatus" multiple><option value="Active">A</option><option value="Passive">P</option></select>' +
      '<select id="filterPriority"><option value=""></option><option value="70">70</option></select>' +
      '<select id="filterOwner" multiple><option value="Ops">Ops</option></select>' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><div class="dt-search"><input></div><table id="dt-s"><thead><tr><th></th><th></th><th>C</th><th>N</th></tr></thead></table></div></div>';
    return document.getElementById("dt-s");
  };

  const stubApi = () => {
    const handlers = {};
    const api = {
      draws: [], pages: [],
      column: () => ({ visible: () => true }), order: () => [[2, "asc"]],
      search: (v) => { if (v !== undefined) { api._search = v; return api; } return api._search || ""; },
      draw(mode) { api.draws.push(mode); (handlers["search.dt"] || []).forEach((fn) => fn()); },
      page(n) { api.pages.push(n); return api; },
      columns: { adjust() {} },
      table: () => ({ container: () => document.querySelector(".card-datatable") }),
      on(events, fn) { String(events).split(/\s+/).forEach((e) => { (handlers[e] = handlers[e] || []).push(fn); }); return api; },
      row: () => ({ data: () => null })
    };
    return api;
  };

  /** Builds a list the way a page does and returns what the factory handed DataTables. */
  const build = async (dataMode, extra = {}, views = []) => {
    let captured = null;
    let buttons = null;
    const saves = [];
    window.DtDefaults = {
      create: (cfg) => { captured = cfg; return cfg; },
      exportButtons: (_text, _attr, extraButtons) => { buttons = extraButtons; return []; },
      updateVisualState() {}, refreshButtonGroupRadii() {}
    };
    window.personalizationClient = {
      getViews: async () => views,
      saveView: async (payload) => { saves.push(JSON.parse(JSON.stringify(payload))); return { data: Object.assign({ id: "v1" }, payload) }; },
      updateView: async (_id, payload) => { saves.push(JSON.parse(JSON.stringify(payload))); return { data: payload }; }
    };
    window.jQuery.fn.dataTable = { ext: { search: [] } };
    const api = stubApi();
    sandbox.DataTable = function () { Object.assign(this, api); };
    const tableEl = page();
    const list = await D.createList(Object.assign({
      tableEl, dataMode,
      ajax: { url: "/api/golden-reference-compact", type: "GET" },
      filters: { fields: FIELDS },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [2, 3], defaultVisibleColumnIndexes: [2, 3], baseOrder: [[2, "asc"]] },
      config: { columns: [{ data: "id" }, { data: "id" }, { data: "code" }, { data: "name" }] }
    }, extra));
    return { list, api, captured, buttons: () => buttons, saves, tableEl };
  };

  const init = async (built) => {
    built.captured.initComplete.call({ api: () => built.api }, {}, {});
    await tick();
  };

  // ── 1. the request ─────────────────────────────────────────────────────────────────────────────────

  test("1. the request is the flat contract: start/length/search/orderBy/orderDir/draw + filters by key, repeated for multi", () => {
    const qs = D.toServerQuery(dtRequest(), FIELDS, { status: ["Active", "Passive"], priority: "70", owner: [] });

    expect(parseQuery(qs)).toEqual([
      ["start", "20"], ["length", "10"], ["search", "Alpha"], ["orderBy", "name"], ["orderDir", "desc"], ["draw", "3"],
      ["status", "Active"], ["status", "Passive"], ["priority", "70"]
    ]);
  });

  test("1a. a column declared not orderable (control/checkbox/actions) is never sent as orderBy", () => {
    const qs = D.toServerQuery(dtRequest({ order: [{ column: 0, dir: "asc" }] }), FIELDS, {});
    expect(qs).not.toMatch(/orderBy|orderDir/);
  });

  test("1b. DataTables' positional noise never reaches the service", () => {
    const qs = decodeURIComponent(D.toServerQuery(dtRequest(), FIELDS, { status: ["Active"] }));
    expect(qs, "columns[i][…] is DataTables' own bookkeeping").not.toMatch(/columns\[/);
    expect(qs).not.toMatch(/order\[|search\[|regex/);
  });

  test("1c. the wired list sends exactly that query, with the APPLIED filters", async () => {
    const built = await build("server");
    expect(typeof built.captured.ajax.data, "server mode maps the request").toBe("function");
    expect(built.captured.ajax.data(dtRequest({ search: { value: "" } }))).toBe("start=20&length=10&orderBy=name&orderDir=desc&draw=3");
  });

  // ── 2. the response ────────────────────────────────────────────────────────────────────────────────

  const envelope = { isSuccessful: true, statusCode: 200, data: { items: [{ id: 1, code: "A" }, { id: 2, code: "B" }], total: 1001, filteredTotal: 37 } };

  test("2. the response: recordsTotal = total, recordsFiltered = filteredTotal, data = items, draw kept", () => {
    expect(D.toDataTablesResponse(envelope, 5)).toEqual({ draw: 5, recordsTotal: 1001, recordsFiltered: 37, data: envelope.data.items });
  });

  test("2b. the wired dataFilter reads the draw back from ITS request and exposes the envelope (lastResponse, onResponse)", async () => {
    const seen = [];
    const built = await build("server", { onResponse: (json) => seen.push(json) });
    const out = JSON.parse(built.captured.ajax.dataFilter.call({ url: "/api/golden-reference-compact?start=0&length=10&draw=7&_=1" }, JSON.stringify(envelope)));

    expect(out).toEqual({ draw: 7, recordsTotal: 1001, recordsFiltered: 37, data: envelope.data.items });
    expect(built.captured.ajax.dataSrc).toBe("data");
    expect(built.list.lastResponse, "the page reads its summary from here").toEqual(envelope);
    expect(seen).toEqual([envelope]);
  });

  // ── 3. no browser-side filtering ───────────────────────────────────────────────────────────────────

  test("3. server mode registers NO ext.search hook — the control, client mode, registers one", async () => {
    const server = await build("server");
    expect(window.jQuery.fn.dataTable.ext.search, "server mode: the service filters").toHaveLength(0);
    expect(server.tableEl.dataset.ditenFilterBound).toBeUndefined();

    await build("client");
    expect(window.jQuery.fn.dataTable.ext.search, "client mode: the browser filters").toHaveLength(1);
  });

  test("3b. the config: serverSide + processing on, the rest of the standard unchanged; client mode untouched", async () => {
    const server = (await build("server")).captured;
    expect(server.serverSide).toBe(true);
    expect(server.processing).toBe(true);
    expect(server.stateSave).toBe(false);
    expect(server.colReorder).toEqual({ columns: ":gt(1):not(:last-child)" });

    const client = (await build("client")).captured;
    expect(client.serverSide, "client mode never turns serverSide on").toBeUndefined();
    expect(client.ajax?.data, "client mode sends no mapped query").toBeUndefined();
    expect(client.ajax?.dataFilter).toBeUndefined();
  });

  // ── 4. Apply / Reset ───────────────────────────────────────────────────────────────────────────────

  test("4. Apply → a fresh draw whose request carries the new filters", async () => {
    const built = await build("server");
    await init(built);
    const request = () => parseQuery(built.captured.ajax.data(dtRequest({ start: 0, search: { value: "" } })));
    expect(request().filter(([k]) => k === "status"), "nothing applied yet").toEqual([]);

    window.jQuery("#filterStatus").val(["Passive"]);
    window.jQuery("#filterPriority").val("70");
    const drawsBefore = built.api.draws.length;
    document.getElementById("btnFilterApply").click();

    expect(built.api.draws.length, "Apply must ask the server again").toBeGreaterThan(drawsBefore);
    expect(request().filter(([k]) => k === "status" || k === "priority")).toEqual([["status", "Passive"], ["priority", "70"]]);
  });

  test("4b. Reset → back to page one with the baseline query", async () => {
    const built = await build("server");
    await init(built);
    window.jQuery("#filterStatus").val(["Passive"]);
    document.getElementById("btnFilterApply").click();

    document.getElementById("btnFilterReset").click();

    expect(built.api.pages).toContain(0);
    expect(parseQuery(built.captured.ajax.data(dtRequest({ search: { value: "" } }))).filter(([k]) => k === "status")).toEqual([]);
  });

  // ── 5. Save View ───────────────────────────────────────────────────────────────────────────────────

  test("5. Save View writes the same view definition in server mode as in client mode", async () => {
    const definitions = {};
    for (const mode of ["client", "server"]) {
      const built = await build(mode);
      await init(built);
      window.jQuery("#filterStatus").val(["Active", "Passive"]);
      window.jQuery("#filterPriority").val("70");
      document.getElementById("btnFilterApply").click();
      built.api._search = "alpha";
      await built.buttons().saveFilterBtn.action(null, built.api);
      expect(built.saves, `${mode}: one save`).toHaveLength(1);
      definitions[mode] = built.saves[0];
    }

    expect(definitions.server).toEqual(definitions.client);
    expect(definitions.server.viewDefinition.filters).toEqual({ status: ["Active", "Passive"], priority: "70", owner: [] });
    expect(definitions.server.viewDefinition.search).toBe("alpha");
  });

  // ── 6. the saved view rides the first request ──────────────────────────────────────────────────────

  test("6a. with no saved view the first request is ordered by the page's baseOrder, never by DataTables' column 0", async () => {
    const built = await build("server");
    expect(built.captured.order).toEqual([[2, "asc"]]);
  });

  test("6. a saved view's filters, search and order are in the FIRST request, before initComplete", async () => {
    const built = await build("server", {}, [{ id: "v", isDefault: true, viewDefinition: { filters: { status: ["Passive"] }, search: "grc", order: [[3, "desc"]] } }]);

    expect(parseQuery(built.captured.ajax.data(dtRequest({ search: { value: "" } }))).filter(([k]) => k === "status")).toEqual([["status", "Passive"]]);
    expect(built.captured.search).toEqual({ search: "grc" });
    expect(built.captured.order).toEqual([[3, "desc"]]);
  });
});
