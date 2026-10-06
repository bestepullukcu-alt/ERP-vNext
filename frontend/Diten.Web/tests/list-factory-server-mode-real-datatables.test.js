const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * SERVER MODE THROUGH THE REAL DataTables AND THE REAL jQuery — WP-UI-LIST-SERVER-01 (BL-440 package 3).
 *
 * list-factory-server-mode.test.js measures the factory's translations against a DataTables STUB. What a stub cannot
 * prove is that the vendored DataTables and jQuery actually honour them: that a STRING returned from `ajax.data` is
 * sent as-is (DataTables' _fnBuildAjax uses a returned value "alone"), that jQuery runs `ajax.dataFilter` on the raw
 * body and that DataTables then reads `recordsTotal/recordsFiltered/data` from what the filter returned. So here the
 * vendored jquery.js and datatables-bootstrap5.js run in a browser-shaped context and only the NETWORK is replaced —
 * by a jQuery transport that records the URL and answers with the service's real envelope shape.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 2000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

describe("server mode through the vendored DataTables + jQuery (only the network is fake)", () => {
  let ctx;
  const requests = [];

  beforeAll(() => {
    document.body.innerHTML =
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse">' +
      '<select id="filterStatus" multiple><option value="Active">A</option><option value="Passive">P</option></select>' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><table id="dt-real"><thead><tr><th></th><th></th><th>Code</th><th>Name</th></tr></thead><tbody></tbody></table></div></div>';

    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      Node: window.Node, HTMLElement: window.HTMLElement, Element: window.Element, Event: window.Event, CustomEvent: window.CustomEvent,
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } }
    });
    // Every DOM constructor the page world has (Option, HTMLSelectElement, …) — DataTables' length menu builds <option>s.
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    run("wwwroot/assets/js/diten-datatable.js");

    // The network: a transport that answers like GET /api/golden-reference-compact does in server mode.
    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        requests.push(options.url);
        const draw = Number(/[?&]draw=(\d+)/.exec(options.url)?.[1] || 0);
        const body = JSON.stringify({
          data: { items: [{ id: `r${draw}a`, code: "GRC-001", name: "Alpha" }, { id: `r${draw}b`, code: "GRC-002", name: "Beta" }], total: 1001, filteredTotal: 37 },
          statusCode: 200, isSuccessful: true, errors: []
        });
        setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
  });

  test("the request leaves as the flat query and the pager reads total/filteredTotal from the envelope", async () => {
    window.DtDefaults = { create: (cfg) => cfg, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {} };
    window.personalizationClient = { getViews: async () => [] };

    const list = await window.DitenDataTable.createList({
      tableEl: document.getElementById("dt-real"),
      dataMode: "server",
      ajax: { url: "/api/golden-reference-compact", type: "GET" },
      filters: { fields: [{ id: "filterStatus", key: "status", kind: "multi" }] },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [2, 3], defaultVisibleColumnIndexes: [2, 3], baseOrder: [[2, "asc"]] },
      config: {
        colReorder: false,
        buttons: [],
        layout: {},
        columns: [{ data: "id", orderable: false }, { data: "id", orderable: false }, { data: "code" }, { data: "name" }]
      }
    });

    await until(() => list.lastResponse !== null && list.dt.page.info().recordsDisplay === 37);

    const first = decodeURIComponent(requests[0]);
    expect(first).toMatch(/^\/api\/golden-reference-compact\?start=0&length=10&orderBy=code&orderDir=asc&draw=1(&_=\d+)?$/);
    expect(first, "DataTables' own positional dump must not travel").not.toMatch(/columns\[|order\[|search\[/);

    const info = list.dt.page.info();
    expect(info.recordsTotal, "recordsTotal = total").toBe(1001);
    expect(info.recordsDisplay, "recordsFiltered = filteredTotal").toBe(37);
    expect(list.dt.rows().data().toArray().map((r) => r.code)).toEqual(["GRC-001", "GRC-002"]);
    expect(list.lastResponse.data.total).toBe(1001);

    // Apply → the next request carries the filter as a repeated parameter.
    window.jQuery("#filterStatus").val(["Active", "Passive"]);
    document.getElementById("btnFilterApply").click();
    await until(() => requests.length >= 2 && /status=Passive/.test(decodeURIComponent(requests.at(-1))));
    expect(decodeURIComponent(requests.at(-1))).toMatch(/&status=Active&status=Passive/);
  });
});
