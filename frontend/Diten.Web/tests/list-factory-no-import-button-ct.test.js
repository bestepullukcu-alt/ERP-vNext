const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * NO IMPORT BUTTON ON LIST SCREENS — owner decision 2026-09-24 (BL-441), CT guard.
 *
 * Until today `createList` put an "İçe aktar" entry into every list's Action menu that only raised a "coming soon"
 * toast. The owner removed it and chose the SAP/Oracle shape instead: importing is a central module (template,
 * validation, error report, audit trail), never a per-page button — and never a promise of one. Measured on the
 * production factory, the way the server-mode suite measures it: build a list, capture what the factory hands
 * DtDefaults.exportButtons, and read the keys.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const SOURCE_PATH = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "diten-datatable.js");

describe("the list factory offers no import entry (BL-441)", () => {
  let D;
  let sandbox;

  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    sandbox = {
      window, document, $: window.jQuery, jQuery: window.jQuery, console, setTimeout, clearTimeout,
      requestAnimationFrame: (fn) => fn(),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } },
      DataTable: function () {}
    };
    vm.runInNewContext(fs.readFileSync(SOURCE_PATH, "utf8"), sandbox, { filename: SOURCE_PATH });
    D = window.DitenDataTable;
  });

  const build = async () => {
    let buttons = null;
    window.DtDefaults = {
      create: (cfg) => cfg,
      exportButtons: (_text, _attr, extraButtons) => { buttons = extraButtons; return []; },
      updateVisualState() {}, refreshButtonGroupRadii() {}
    };
    window.personalizationClient = { getViews: async () => [], saveView: async (p) => ({ data: p }), updateView: async (_i, p) => ({ data: p }) };
    window.jQuery.fn.dataTable = { ext: { search: [] } };
    window.showToast = () => { throw new Error("no toast may be wired by the factory's toolbar defaults"); };
    document.body.innerHTML =
      '<button class="dt-save-filter-btn d-none"></button>' +
      '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse"><button id="btnFilterApply"></button><button id="btnFilterReset"></button></div></div>' +
      '<div class="card"><div class="card-datatable"><div class="dt-search"><input></div><table id="dt-x"><thead><tr><th>C</th></tr></thead></table></div></div>';
    sandbox.DataTable = function () { this.on = () => this; this.draw = () => {}; this.order = () => [[0, "asc"]]; this.search = () => ""; this.column = () => ({ visible: () => true }); this.columns = { adjust() {} }; this.table = () => ({ container: () => document.querySelector(".card-datatable") }); };
    await D.createList({
      tableEl: document.getElementById("dt-x"), dataMode: "client",
      filters: { fields: [] },
      savedView: { moduleKey: "M", pageKey: "P", saveViewColumnIndexes: [0], defaultVisibleColumnIndexes: [0], baseOrder: [[0, "asc"]] },
      config: { columns: [{ data: "code" }] }
    });
    return buttons;
  };

  test("the toolbar defaults the factory hands DtDefaults carry filter and save-view — and no import", async () => {
    const buttons = await build();
    expect(buttons).not.toBeNull();
    expect(Object.keys(buttons)).toEqual(expect.arrayContaining(["filterBtn", "saveFilterBtn"]));
    expect(Object.keys(buttons)).not.toContain("importBtn");
    // Nor under any other name: no default renders the import glyph or promises "coming soon".
    for (const [key, btn] of Object.entries(buttons)) {
      expect(`${key}: ${btn.text || ""}`).not.toMatch(/bx-import/);
      expect(String(btn.action || "")).not.toMatch(/ComingSoon/);
    }
  });

  test("the production factory source has no import default left to switch back on", () => {
    const source = fs.readFileSync(SOURCE_PATH, "utf8");
    expect(source).not.toMatch(/importBtn\s*:/);
    expect(source).not.toMatch(/bx-import/);
  });
});
