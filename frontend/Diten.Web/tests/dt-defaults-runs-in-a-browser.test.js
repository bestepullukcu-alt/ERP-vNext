const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * THE FILE MUST RUN WHERE IT IS SHIPPED — owner report, 2026-09-23 ("hiçbir DataTable sayfası çalışmıyor").
 *
 * MEASURED. The skeleton fix of 14:16 (3a4bb6f8f) wrote `global.document` inside `revealTable`. `dt-defaults.js` is
 * an IIFE with no `global` parameter, and a browser has no `global` — only Node does. So in vitest, whose scripts run
 * in Node's own context (`vm.runInThisContext`, see load-script.js), the identifier resolved and every test stayed
 * green; in the browser it threw ReferenceError from `initComplete`, DataTables abandoned the init, and every list on
 * the site sat on three blue dots for 45 minutes. `dt-one-loading-language.test.js` exercised the exact function
 * that was broken and could not see it, because the test environment had the very global the page lacks.
 *
 * This suite closes that gap the only honest way: it evaluates the file in a FRESH vm context that carries what a
 * browser page carries — window, document, jQuery, DataTable — and nothing Node adds. A reference to `global`,
 * `process` or `require` is a ReferenceError here exactly as it is in Chrome.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SOURCE_PATH = web("wwwroot", "assets", "js", "dt-defaults.js");

const skeletonPage = () => {
  document.body.innerHTML =
    '<div class="card"><div id="skeleton-loader" class="dt-skeleton" data-table-skeleton></div>' +
    '<div class="card-datatable"><div class="dt-container"><table></table></div></div></div>';
};

describe("dt-defaults.js runs with a browser's globals and nothing more", () => {
  let DtDefaults;

  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    const sandbox = {
      window,
      document,
      $: window.jQuery,
      jQuery: window.jQuery,
      DataTable: {
        Responsive: { display: { modal: () => ({}) } },
        Api: function () { this.columns = { adjust: () => {} }; this.responsive = { recalc: () => {} }; }
      },
      console,
      setTimeout,
      clearTimeout
    };
    // NOT runInThisContext: a new context has no `global`, no `process`, no `require` — a page's world.
    vm.runInNewContext(fs.readFileSync(SOURCE_PATH, "utf8"), sandbox, { filename: SOURCE_PATH });
    DtDefaults = window.DtDefaults;
  });

  test("the file loads at all in that world", () => {
    expect(typeof DtDefaults?.create).toBe("function");
  });

  test("initComplete reveals the table without reaching for anything Node-only", () => {
    skeletonPage();
    const config = DtDefaults.create({});

    // This is the call that threw `ReferenceError: global is not defined` on every list page.
    expect(() => config.initComplete({ nTableWrapper: document.querySelector(".dt-container") }, {})).not.toThrow();
    expect(document.getElementById("skeleton-loader")).toBeNull();
  });

  test("the other two exits are just as clean", () => {
    skeletonPage();
    expect(() => DtDefaults.create({}).drawCallback({ nTableWrapper: document.querySelector(".dt-container") })).not.toThrow();
    expect(document.getElementById("skeleton-loader")).toBeNull();

    skeletonPage();
    expect(() => DtDefaults.create({ ajax: { url: "/nowhere" } }).ajax.error({ status: 503, responseText: "" }, "error", "x")).not.toThrow();
    expect(document.getElementById("skeleton-loader")).toBeNull();
  });

  test("the source names no Node-only global (the fast explanation of the test above)", () => {
    const source = fs.readFileSync(SOURCE_PATH, "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    expect(source, "a browser has no `global`; use `window` or `document`").not.toMatch(/\bglobal\s*\./);
    expect(source).not.toMatch(/\bprocess\s*\./);
  });
});
