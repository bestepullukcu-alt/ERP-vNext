const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * A LIST SPEAKS WITH ONE VOICE WHILE IT LOADS — owner report, 2026-09-21.
 *
 * MEASURED before this round, in `dt-defaults.js`: `preXhr` faded the page's `#skeleton-loader` in on EVERY
 * request, and `processing: true` drew the theme's `sk-fold` cube over it at the same time. A list therefore
 * opened with grey placeholder rows AND a spinning cube on top of them — and DataTables' own untranslated
 * "Loading..." in the body behind both. Three voices for one wait.
 *
 * The product had already chosen: `backbone-custom.css` carries a "NO NEW SKELETON LANGUAGE" warning and 226
 * views ship the skeleton markup. So the rule is: the FIRST load is the skeleton's (the page is empty and its
 * shape is known), every LATER load is the cube's (the rows are on screen; replacing them with grey blocks
 * reads as going backwards).
 *
 * These tests drive the REAL `DtDefaults.create` and call the hooks DataTables would call. The only stub is the
 * vendor library's own namespace, which the module reads once while it is being defined.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");

describe("the first load is the skeleton's, every later load is the cube's", () => {
  let DtDefaults;

  beforeAll(() => {
    // `baseConfig` asks the vendor for its responsive modal renderer while the module is being defined; that is
    // the whole of the dependency, so it is the whole of the stub.
    global.DataTable = { Responsive: { display: { modal: () => ({}) } } };
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/js/dt-defaults.js");
    DtDefaults = global.window.DtDefaults;
  });

  /** The two DOM pieces the hooks touch: the page's skeleton and this table's own wrapper. */
  const stage = () => {
    document.body.innerHTML =
      '<div id="skeleton-loader" style="display: none;"></div>' +
      '<div class="dt-container"><div class="dt-processing">cube</div><table></table></div>';
    return { settings: { nTableWrapper: document.querySelector(".dt-container") } };
  };

  const skeletonShown = () => document.getElementById("skeleton-loader").style.display !== "none";
  const cubeSuppressed = () => document.querySelector(".dt-container").classList.contains("dt-first-load");

  test("the first request shows the skeleton and silences the cube", () => {
    const config = DtDefaults.create({});
    const { settings } = stage();

    config.preXhr(settings, {});
    // jQuery's fadeIn is animated; the display flip happens on the first frame, so ask jQuery, not the clock.
    global.jQuery("#skeleton-loader").stop(true, true);
    expect(skeletonShown(), "the skeleton never appeared on the first load").toBe(true);
    expect(cubeSuppressed(), "the cube would spin on top of the skeleton").toBe(true);
  });

  test("once the table has loaded, later requests leave the skeleton down and the cube alone", () => {
    const config = DtDefaults.create({});
    const { settings } = stage();

    config.preXhr(settings, {});
    config.initComplete(settings, {});
    global.jQuery("#skeleton-loader").stop(true, true).hide();
    expect(cubeSuppressed(), "the suppression outlived the first load — the cube would never show again").toBe(false);

    // A filter, a page change, a reload: the rows are on screen and stay there.
    config.preXhr(settings, {});
    global.jQuery("#skeleton-loader").stop(true, true);
    expect(skeletonShown(), "a refresh turned the rows back into grey blocks").toBe(false);
    expect(cubeSuppressed(), "the cube was silenced on a refresh, leaving no busy signal at all").toBe(false);
  });

  test("two tables on one screen do not share the flag", () => {
    const first = DtDefaults.create({});
    const second = DtDefaults.create({});
    const { settings } = stage();

    first.preXhr(settings, {});
    first.initComplete(settings, {});
    document.querySelector(".dt-container").classList.remove("dt-first-load");

    second.preXhr(settings, {});
    expect(cubeSuppressed(), "the second table inherited the first one's 'already loaded' state").toBe(true);
  });

  test("the body no longer says 'Loading...' in English behind the skeleton", () => {
    expect(DtDefaults.create({}).language.loadingRecords).toBe("");
    // A caller that wants its own word still gets it.
    expect(DtDefaults.create({ language: { loadingRecords: "Yükleniyor" } }).language.loadingRecords).toBe("Yükleniyor");
  });

  test("the CSS hides the cube only while the class is on, and only inside that table", () => {
    const rule = CSS.replace(/\/\*[\s\S]*?\*\//g, "")
      .match(/\.dt-first-load div\.dt-processing,\s*\.dt-first-load \.dataTables_processing \{([^}]*)\}/);
    expect(rule, "the first-load rule is gone — the cube is back on top of the skeleton").toBeTruthy();
    expect(rule[1]).toContain("display: none");
    // MUTATION GUARD: an unscoped version of this rule would hide the busy signal on every request, forever.
    const bare = CSS.replace(/\/\*[\s\S]*?\*\//g, "").match(/\n\s*div\.dt-processing\s*\{[^}]*display:\s*none/);
    expect(bare, "the cube was hidden product-wide, not just during the first load").toBeNull();
  });
});
