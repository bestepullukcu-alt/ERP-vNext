const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * K16 (Kullanıcılar testi 2026-09-23) — THE FILE IS WHAT THE SCREEN SHOWS. A column the reader hid through
 * "Sütun görünürlüğü" still came out in CSV/Excel/PDF/print because dt-defaults exported a fixed index list.
 * Measured through the vendored DataTables + jQuery: the export's column selector, asked by the real API after a
 * column is hidden, must leave that column out — and bring it back once the column is visible again.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const findCsv = (node) => {
  if (!node || typeof node !== "object") return null;
  if (Array.isArray(node)) { for (const n of node) { const hit = findCsv(n); if (hit) return hit; } return null; }
  if (node.extend === "csv") return node;
  return findCsv(node.buttons);
};

describe("export follows the visible columns (K16, CT)", () => {
  let ctx;
  let api;
  let exportColumns;

  beforeAll(() => {
    document.body.innerHTML =
      '<table id="dt-k16"><thead><tr><th></th><th>Id</th><th>Code</th><th>Name</th><th>Status</th><th>Actions</th></tr></thead>' +
      '<tbody><tr><td></td><td>1</td><td>A-1</td><td>Alpha</td><td>Active</td><td></td></tr></tbody></table>';
    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      Node: window.Node, HTMLElement: window.HTMLElement, Element: window.Element, Event: window.Event, CustomEvent: window.CustomEvent,
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: class { show() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return { show() {}, hide() {} }; } } }
    });
    // The vendored bundle reads `window.bootstrap.Modal` at load; jsdom's window has no bootstrap of its own.
    window.bootstrap = ctx.bootstrap;
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    run("wwwroot/assets/js/dt-defaults.js");

    api = ctx.jQuery("#dt-k16").DataTable({ paging: false, searching: false, info: false, ordering: false });
    // What a page hands the toolbar: the exportable columns are Code, Name, Status (never the control/id/actions).
    const buttons = ctx.window.DtDefaults.exportButtons("", {}, {}, { exportColumns: [2, 3, 4] });
    const csv = findCsv(buttons);
    expect(csv, "the CSV button carries exportOptions").toBeTruthy();
    exportColumns = csv.exportOptions.columns;
  });

  const exported = () => api.columns(exportColumns).indexes().toArray();

  it("exports exactly the page's allowed columns while all of them are visible", () => {
    expect(exported()).toEqual([2, 3, 4]);
  });

  it("leaves a hidden column out of the export, and brings it back when it is shown again", () => {
    api.column(3).visible(false);
    expect(exported(), "Name is hidden, so it must not be in the file").toEqual([2, 4]);
    api.column(3).visible(true);
    expect(exported()).toEqual([2, 3, 4]);
  });

  it("never exports a column the page did not allow, visible or not", () => {
    expect(exported()).not.toContain(0);
    expect(exported()).not.toContain(1);
    expect(exported()).not.toContain(5);
  });
});
