const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * CT-SHELL-FIX1 item 8 — Reference Data's own "empty" notice must not say "no records" beside the list component's
 * "could not be loaded". Measured through the whole browser chain: the vendored jQuery, DataTables, Buttons and
 * ColReorder, the REAL dt-defaults.js, the REAL diten-datatable.js and the REAL Reference Data index.js with the
 * production table partial. Only the network is fake (the sets endpoint answers 500).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 5000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};
const settle = (ms = 40) => new Promise((r) => setTimeout(r, ms));

const razor = (rel) => read(...rel.split("/"))
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^@(using|inject|model)[^\n]*\n/gm, "")
  .replace(/@(?:Shared)?Localizer\["(\w+)"\]/g, "$1");

const TR = { DtLoadFailed: "Liste yüklenemedi.", DtLoadForbidden: "Bu listeyi görme yetkiniz yok.", DtRetry: "Yeniden Dene" };

describe("Reference Data when its list cannot be loaded", () => {
  let ctx;
  const setsRequests = [];

  beforeAll(async () => {
    document.head.innerHTML = `<script id="datatable-l10n" type="application/json">${JSON.stringify(TR)}</script>`;
    document.body.innerHTML =
      '<div id="reference-data-page" data-rd-permissions="true" data-can-create-set="false" data-can-import-preview="false">'
      + '<div id="rd-state-loading" class="alert alert-info d-none">Loading</div>'
      + '<div id="rd-state-error" class="alert alert-danger d-none">ErrorState</div>'
      + '<div id="rd-state-empty" class="alert alert-warning d-none">EmptyState</div>'
      + razor("Views/Platform/ReferenceData/_DataTable.cshtml")
      + "</div>";

    class Modal { show() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Modal(); } }
    class Tooltip { constructor() {} dispose() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Tooltip(); } }
    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Modal, Tooltip }
    });
    window.bootstrap = ctx.bootstrap;
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    ctx.DataTable.Responsive?.bootstrap?.(ctx.bootstrap);
    run("wwwroot/assets/vendor/libs/datatables-buttons/buttons.colVis.js");
    run("wwwroot/assets/vendor/libs/datatables-colreorder-bs5/dataTables.colReorder.min.js");
    run("wwwroot/assets/js/dt-defaults.js");
    run("wwwroot/assets/js/diten-datatable.js");

    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        if (String(options.url).includes("/sets")) setsRequests.push(options.url);
        setTimeout(() => complete(500, "Error", { text: "{}" }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
    window.showToast = () => {};
    window.L10n = {};
    window.ReferenceDataApi = { getScopeTypes: async () => [] };
    window.CurrentUser = { tenantId: "t-1" };

    run("wwwroot/assets/js/Platform/ReferenceData/index.js");
    document.dispatchEvent(new window.Event("DOMContentLoaded"));
    await until(() => document.querySelector("#dt-reference-data-sets tbody [data-dt-load-error]"));
  });

  const emptyNotice = () => document.getElementById("rd-state-empty");
  const errorBox = () => document.querySelector("#dt-reference-data-sets tbody [data-dt-load-error]");

  test("the table says the list could not be loaded, and the page's empty notice is down", () => {
    expect(setsRequests.length).toBeGreaterThan(0);
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(emptyNotice().classList.contains("d-none")).toBe(true);
  });

  test("a redraw (a search, a sort) keeps both: the failure in the table, the empty notice down", async () => {
    const dt = ctx.jQuery("#dt-reference-data-sets").DataTable();
    dt.search("x").draw();
    dt.order([2, "desc"]).draw();
    await settle();

    expect(errorBox()).not.toBeNull();
    expect(emptyNotice().classList.contains("d-none")).toBe(true);
  });
});
