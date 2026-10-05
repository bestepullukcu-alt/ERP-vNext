const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * WP-MDM-ABB-LIST-01 FIX3 item 2 — the controlled copy's (PDF / print) "filters" line and ONE opt-out attribute.
 *
 * describeAppliedFilters names every applied filter by its control's label (data-placeholder, aria-label, <label for>) or,
 * failing that, its key. A field whose element carries data-dt-describe="false" is a wire value only (an id behind a named
 * field) and is left out. Measured here on the REAL factory, vendored jQuery and DataTables, for a list that is NOT the ABB
 * register: without the attribute — or with any other value — the line is exactly what it was before.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

describe("the controlled copy's filters line on the real list component", () => {
  let ctx;

  beforeAll(() => {
    document.body.innerHTML =
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse">' +
      '<label for="filterStatus">Status</label><select id="filterStatus" multiple><option value="Active">Active</option><option value="Passive">Passive</option></select>' +
      '<input type="hidden" id="filterOwnerText" aria-label="Owner" />' +
      '<input type="hidden" id="filterOwnerId" />' +
      '<input type="hidden" id="filterRegionId" data-dt-describe="true" />' +
      '<input type="hidden" id="filterHiddenId" data-dt-describe="false" />' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><table id="dt-describe"><thead><tr><th></th><th>Code</th></tr></thead><tbody></tbody></table></div></div>';
    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } }
    });
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    run("wwwroot/assets/js/diten-datatable.js");
    ctx.jQuery.ajaxTransport("+*", () => ({
      send(_headers, complete) {
        const body = JSON.stringify({ data: { items: [{ id: "r1", code: "C-1" }], total: 1, filteredTotal: 1 }, isSuccessful: true });
        setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
  });

  test("describes every field as before; leaves out only a field marked data-dt-describe=\"false\"", async () => {
    let buttons;
    window.DtDefaults = { create: (cfg) => cfg, exportButtons: (_t, _a, _e, options) => { buttons = options; return []; }, updateVisualState() {}, refreshButtonGroupRadii() {} };
    window.personalizationClient = {
      getViews: async () => [{ id: "v", isDefault: true, viewDefinition: {
        filters: { status: ["Active", "Passive"], ownerText: "Ada Lovelace", ownerId: "owner-7", regionId: "EU", hiddenId: "secret-1" } } }]
    };

    const list = await window.DitenDataTable.createList({
      tableEl: document.getElementById("dt-describe"),
      dataMode: "server",
      ajax: { url: "/api/describe", type: "GET" },
      filters: { fields: [
        { id: "filterStatus", key: "status", kind: "multi" },
        { id: "filterOwnerText", key: "ownerText", kind: "text" },
        { id: "filterOwnerId", key: "ownerId", kind: "text" },
        { id: "filterRegionId", key: "regionId", kind: "text" },
        { id: "filterHiddenId", key: "hiddenId", kind: "text" }
      ] },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [1], defaultVisibleColumnIndexes: [1], baseOrder: [[1, "asc"]] },
      config: { colReorder: false, layout: {}, columns: [{ data: "id", orderable: false }, { data: "code" }] }
    });
    await until(() => list.lastResponse !== null);

    expect(buttons.controlledCopy.meta().filters).toEqual([
      { label: "Status", values: ["Active", "Passive"] },        // <label for>, option texts — as before
      { label: "Owner", values: ["Ada Lovelace"] },              // aria-label — as before
      { label: "ownerId", values: ["owner-7"] },                 // no label: the key — as before (no attribute, no change)
      { label: "regionId", values: ["EU"] }                      // any value but "false" changes nothing
      // hiddenId: data-dt-describe="false" — left out
    ]);
  });
});
