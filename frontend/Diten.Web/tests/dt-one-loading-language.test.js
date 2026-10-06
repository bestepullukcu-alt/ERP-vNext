const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * A LIST SPEAKS WITH ONE VOICE WHILE IT LOADS — owner report, 2026-09-21.
 *
 * MEASURED, in two rounds, and the second round is the one worth reading.
 *
 * ROUND 1 assumed `dt-defaults.js` was already showing the page's placeholder and only had to stop the theme's
 * `sk-fold` cube from spinning on top of it. It was not: the file set a `preXhr` option, and DataTables has no
 * such option. `preXhr` is an EVENT; the init options it registers as callbacks are drawCallback,
 * initComplete, preDrawCallback, rowCallback and the state ones — measured in the vendored library. That
 * function had never been called on any page, which is why every list opened with the cube and no placeholder,
 * and why the owner asked about it in the first place. ⚠ The round-1 tests were green throughout, because they
 * called `config.preXhr(...)` themselves. A test that invokes a hook production never invokes proves nothing.
 *
 * ROUND 2 takes the mechanism out of JavaScript: the shaped placeholder is rendered VISIBLE by the server, a
 * CSS sibling rule keeps the table — and the cube DataTables draws inside it — out of the page while that
 * element is present, and `initComplete` removes the element. Nothing has to fire for the placeholder to be
 * seen, so nothing can silently not fire.
 *
 * What is still asserted here is the part that is genuinely JavaScript's: the removal, the column recompute
 * that a never-rendered table needs, the untranslated "Loading..." staying quiet, and — the lesson of round 1 —
 * that this file does not go back to hanging its behaviour on a hook DataTables never calls.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");

describe("the placeholder is on screen before any script runs", () => {
  let DtDefaults;

  beforeAll(() => {
    global.DataTable = {
      Responsive: { display: { modal: () => ({}) } },
      Api: function () { this.columns = { adjust: () => {} }; this.responsive = { recalc: () => {} }; }
    };
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/js/dt-defaults.js");
    DtDefaults = global.window.DtDefaults;
  });

  test("the partial carries no class that would hide it", () => {
    const partial = fs.readFileSync(web("Views", "Shared", "_TableSkeleton.cshtml"), "utf8");
    // `.backbone-skeleton` is `display: none` — wearing it is what made round 1 invisible.
    expect(partial, "the placeholder hides itself again and waits for a script to show it")
      .not.toMatch(/class="[^"]*backbone-skeleton/);
    expect(partial).toMatch(/data-table-skeleton/);
  });

  test("the table waits on a CSS sibling rule, not on a script", () => {
    const rule = CSS.replace(/\/\*[\s\S]*?\*\//g, "")
      .match(/\[data-table-skeleton\] ~ \.card-datatable,\s*\[data-table-skeleton\] ~ \.table-responsive \{([^}]*)\}/);
    expect(rule, "the table no longer waits for the placeholder to go").toBeTruthy();
    expect(rule[1]).toContain("display: none");
  });

  test("initComplete REMOVES the placeholder, which is what reveals the table", () => {
    document.body.innerHTML =
      '<div class="card"><div id="skeleton-loader" class="dt-skeleton" data-table-skeleton></div>' +
      '<div class="card-datatable"><div class="dt-container"><table></table></div></div></div>';
    const config = DtDefaults.create({});

    config.initComplete({ nTableWrapper: document.querySelector(".dt-container") }, {});

    expect(document.getElementById("skeleton-loader"), "the placeholder stayed, so the table stays hidden")
      .toBeNull();
  });

  /*
   * ⚠ THE REGRESSION THIS SUITE MISSED, AND THE OWNER FOUND (2026-09-23).
   *
   * The golden reference pages worked the day before and showed nothing the day after. Cause: their data comes
   * from a service that was down, DataTables does not call `initComplete` when the first ajax FAILS, and
   * `initComplete` was the only place that REMOVED the placeholder. Hiding it is not enough — the CSS rule that
   * keeps the table out of the page matches on the element being PRESENT. So the page stayed blank where it
   * used to show an empty table: the change turned "service down" into "page down".
   *
   * Both remaining exits now perform the same act.
   */
  test("a failed first load still reveals the table", () => {
    document.body.innerHTML =
      '<div class="card"><div id="skeleton-loader" class="dt-skeleton" data-table-skeleton></div>' +
      '<div class="card-datatable"><div class="dt-container"><table></table></div></div></div>';
    const config = DtDefaults.create({ ajax: { url: "/nowhere" } });

    // What DataTables calls when the request fails: the error handler, and no initComplete at all.
    config.ajax.error({ status: 503, responseText: "" }, "error", "Service Unavailable");

    expect(document.getElementById("skeleton-loader"),
      "the placeholder survived a failed load, so the table can never appear").toBeNull();
  });

  /*
   * ⚠ THE SECOND REGRESSION OF THE SAME FIX (owner, 2026-09-23, "iskelet eskisi gibi oldu").
   *
   * The round above added `drawCallback` as a third exit ("a draw happened — whatever is on screen is the
   * answer"). It is not: DataTables draws the table ONCE, EMPTY, before the ajax request is answered — measured
   * on the shipped 2.1.8 with a probe table: first drawCallback fires with the response still pending, then a
   * second draw, then initComplete. So the exit removed the shape at init, and every list opened on the three
   * dots again. A draw is not an answer; initComplete and ajax.error are.
   */
  test("the first draw does NOT reveal — DataTables draws once before the ajax answer", () => {
    document.body.innerHTML =
      '<div class="card"><div id="skeleton-loader" class="dt-skeleton" data-table-skeleton></div>' +
      '<div class="card-datatable"><div class="dt-container"><table></table></div></div></div>';
    const config = DtDefaults.create({});

    config.drawCallback({ nTableWrapper: document.querySelector(".dt-container") });

    expect(document.getElementById("skeleton-loader"),
      "the placeholder was removed by a draw, i.e. before any data or error arrived").not.toBeNull();
  });

  test("a page that brings its own ajax error handler still has the placeholder removed on failure", () => {
    document.body.innerHTML =
      '<div class="card"><div id="skeleton-loader" class="dt-skeleton" data-table-skeleton></div>' +
      '<div class="card-datatable"><div class="dt-container"><table></table></div></div></div>';
    let pageHandlerCalls = 0;
    const config = DtDefaults.create({ ajax: { url: "/nowhere", error: () => { pageHandlerCalls += 1; } } });

    config.ajax.error({ status: 503, responseText: "" }, "error", "Service Unavailable");

    expect(pageHandlerCalls, "the page's own handler was replaced instead of wrapped").toBe(1);
    expect(document.getElementById("skeleton-loader"), "the page's handler ran but the placeholder stayed").toBeNull();
  });

  test("a list still carrying the OLD hidden block is left exactly as it was", () => {
    document.body.innerHTML = '<div id="skeleton-loader" class="backbone-skeleton"></div>';
    const config = DtDefaults.create({});

    config.initComplete({ nTableWrapper: null }, {});

    // Hidden by the old path (fadeOut), not removed: nothing about that page changes.
    expect(document.getElementById("skeleton-loader"), "an unmigrated list had its markup removed").not.toBeNull();
  });

  test("the column widths are recomputed once the table is finally rendered", () => {
    const source = fs.readFileSync(web("wwwroot", "assets", "js", "dt-defaults.js"), "utf8");
    const initComplete = source.slice(source.indexOf("merged.initComplete = function"));
    expect(initComplete).toContain("columns.adjust()");
  });

  test("⚠ the file does not hang its behaviour on a hook DataTables never calls", () => {
    /*
     * THE ROUND-1 LESSON, AS A GUARD. `preXhr` is an event, not an init option; assigning it is a no-op that
     * looks like working code and passes any test that calls it directly.
     */
    const source = fs.readFileSync(web("wwwroot", "assets", "js", "dt-defaults.js"), "utf8")
      .replace(/\/\*[\s\S]*?\*\//g, "");
    expect(source, "merged.preXhr is assigned again — DataTables will never call it").not.toMatch(/merged\.preXhr\s*=/);
  });

  test("the body no longer says 'Loading...' in English behind the placeholder", () => {
    expect(DtDefaults.create({}).language.loadingRecords).toBe("");
    expect(DtDefaults.create({ language: { loadingRecords: "Yükleniyor" } }).language.loadingRecords).toBe("Yükleniyor");
  });
});
