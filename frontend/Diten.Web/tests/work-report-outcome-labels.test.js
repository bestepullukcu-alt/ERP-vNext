const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * ── DILIM 1g — THE TWO REMAINING DEFECTS ────────────────────────────────────────────────────────────────────
 *
 * (1) A control that did nothing. The "Action" button opened a dropdown whose menu had zero children —
 *     measured live. It is gone; `.antigravity/rules/frontend-standards.md` UI-027 forbids the shape, and the
 *     last block here is that rule's sentry over this view.
 *
 * (2) Raw identities printed at a reader. The outcomes axis read `OUT_OF_SCOPE · CORRECTED · CAPA_RAISED` —
 *     the codes the engine stored — because the API published a code and no words, so the screen had nothing
 *     to show but the code. The API now carries a `label` for the outcomes THIS SERVER can name, exactly as
 *     `WorkReportBucket.label` has done for the breakdown axis since 1a.
 *
 * ⚠ EVERY TEST BELOW DRIVES THE SHIPPED FILE. `loadScript` executes
 * `wwwroot/assets/js/Tasks/WorkReport/index.js` itself and the chart click is fired through the options object
 * that file handed ApexCharts — there is no re-implementation of the label rule or the click mapping here.
 * This module has twice shipped a test that measured its own copy (1a, 1c); this one names the risk and
 * refuses it.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

const LABELS = {
  outcomesTitle: "How work ended", outcomesEmpty: "No closure was recorded with an outcome.",
  outcomesOther: "All other outcomes",
  loading: "Loading…", loadFailed: "The report could not be loaded.",
  noData: "No work was opened or closed in this period.",
  noDataScoped: "No work you can see was opened or closed in this period.",
  scopeScoped: "Your scope", scopeTenant: "Whole tenant",
  scopeScopedHint: "Counts only the work you are entitled to see.",
  scopeTenantHint: "Counts every task in the tenant.",
  itemsSubtitleRange: "{0} – {1}", itemsCount: "Showing {0} of {1}", itemsLoadFailed: "Failed",
  itemsEmpty: "No tasks match this cell.", itemsEmptyHint: "Nothing falls into this measure."
};

/*
 * ⚠ ONLY THE SYSTEM CODES, exactly as `_WorkReportL10n.cshtml` ships them. A tenant's own outcome is
 * deliberately absent — that absence is the whole reason the API has to carry its words.
 */
const OUTCOME_LABELS = {
  COMPLETED_AS_REQUESTED: "Completed as requested",
  COMPLETED_PARTIALLY: "Completed partially"
};

const MARKUP = `
  <form id="workReportFilter">
    <input type="date" id="wrFrom" value="2026-06-01" /><input type="date" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option></select>
    <button type="submit">Apply</button>
  </form>
  <div data-wr-scope hidden><span data-wr-scope-badge></span><span data-wr-scope-hint></span></div>
  <p data-wr-status></p>
  <div data-wr-skeleton-tiles hidden></div>
  <div data-wr-skeleton-charts hidden></div>
  <div data-wr-tiles hidden>
    <p data-wr-cycle-value></p><p data-wr-cycle-unit></p><p data-wr-cycle-over></p>
    <dl data-wr-cycle-facts></dl><p data-wr-cycle-trend hidden></p>
    <p data-wr-rework-tasks data-wr-click="Returned" role="button" tabindex="0"></p>
    <dl data-wr-rework-facts></dl><p data-wr-rework-trend hidden></p>
    <p data-wr-unattended-value data-wr-click="Unattended" role="button" tabindex="0"></p>
    <dl data-wr-aging hidden></dl>
    <p data-wr-effort-value></p><p data-wr-effort-over></p><dl data-wr-effort-facts></dl>
  </div>
  <select id="wrLegalEntity"><option value="">Any</option></select>
  <select id="wrUnit"><option value="">Any</option></select>
  <select id="wrTaskType"><option value="">Any</option></select>
  <select id="wrAssignee"><option value="">Any</option></select>
  <select id="wrPriority"><option value="">Any</option></select>
  <div data-wr-summary hidden>
    <span data-wr-summary-days></span>
    <span data-wr-summary-opened data-wr-click="Opened"></span>
    <span data-wr-summary-closed data-wr-click="Closed"></span>
    <span data-wr-summary-completed data-wr-click="Completed"></span>
    <span data-wr-summary-cancelled data-wr-click="Cancelled"></span>
    <span data-wr-summary-unattended data-wr-click="Unattended"></span>
    <span data-wr-summary-note></span>
  </div>
  <div data-wr-charts hidden>
    <div data-wr-chart-flow></div><p data-wr-flow-trend hidden></p>
    <div data-wr-chart-outcomes></div><p data-wr-outcomes-empty hidden></p>
    <div data-wr-chart-timeliness></div><ul data-wr-timeliness-legend></ul><p data-wr-late-trend hidden></p>
    <div data-wr-groups-card hidden><h6 data-wr-groups-title></h6><div data-wr-chart-groups></div>
      <p data-wr-groups-truncated hidden></p></div>
  </div>
  <div class="offcanvas" id="wrItemsOffcanvas">
    <div class="offcanvas-header"><h5 data-wr-items-title>-</h5><small data-wr-items-subtitle>-</small></div>
    <div class="offcanvas-body">
      <p data-wr-items-count></p><p data-wr-items-loading hidden></p>
      <div data-wr-items-empty hidden><p>x</p><p>y</p></div>
      <p data-wr-items-error hidden>z</p>
      <div data-wr-items-list></div>
      <button type="button" data-wr-items-more hidden></button>
    </div>
  </div>
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">${JSON.stringify(OUTCOME_LABELS)}</script>
`;

let drawn = [];
let fetchCalls = [];

const boot = () => {
  drawn = [];
  fetchCalls = [];
  document.body.innerHTML = MARKUP;
  delete global.WorkReportScreen;

  global.ApexCharts = function (host, options) {
    this.host = host;
    this.options = options;
    this.render = () => { drawn.push({ host, options }); };
    this.destroy = () => { drawn = drawn.filter((d) => d.host !== host); };
  };
  window.ApexCharts = global.ApexCharts;

  window.bootstrap = window.bootstrap || {};
  window.bootstrap.Offcanvas = {
    _shown: null,
    getOrCreateInstance: (el) => ({
      show: () => { window.bootstrap.Offcanvas._shown = el; },
      hide: () => { window.bootstrap.Offcanvas._shown = null; }
    })
  };

  global.fetch = (url) => {
    fetchCalls.push(url);
    return Promise.resolve({ ok: false, json: () => Promise.resolve(null) });
  };

  loadScript("wwwroot/assets/js/Tasks/WorkReport/index.js");
  return window.WorkReportScreen;
};

const bucket = (over) => Object.assign({
  key: null, label: null,
  flow: { opened: 0, closed: 0, completed: 0, cancelled: 0, unattended: 0 },
  cycleTime: { averageDays: null, medianDays: null, count: 0 },
  cancellationTime: { averageDays: null, medianDays: null, count: 0 },
  aging: { upTo7Days: 0, from8To30Days: 0, olderThan30Days: 0 },
  timeliness: { onTime: 0, late: 0, withoutDueDate: 0 },
  effort: { estimatedHours: 0, spentHours: 0, taskCount: 0 },
  outcomes: [], rework: { tasksReturned: 0, totalReturns: 0 }
}, over || {});

/** A period with work in it — `hasWork` short-circuits the whole render otherwise, charts included. */
const report = (outcomes) => ({
  from: "2026-06-01T00:00:00+00:00", to: "2026-07-01T00:00:00+00:00",
  scopeApplied: "scoped", groupBy: "None", groups: [], groupsTruncated: 0, previous: null,
  totals: bucket({
    flow: { opened: 12, closed: 9, completed: 7, cancelled: 2, unattended: 4 },
    outcomes: outcomes
  })
});

/** The axis the reader actually sees, off the options object the screen handed ApexCharts. */
const axis = () => {
  const chart = drawn.find((d) => d.host === document.querySelector("[data-wr-chart-outcomes]"));
  if (!chart) { throw new Error("the outcomes chart was never drawn"); }
  return chart.options.xaxis.categories;
};

const clickOutcome = (index) => {
  const chart = drawn.find((d) => d.host === document.querySelector("[data-wr-chart-outcomes]"));
  chart.options.chart.events.dataPointSelection(null, null, { seriesIndex: 0, dataPointIndex: index });
};

const parseQuery = (url) => {
  const out = {};
  url.slice(url.indexOf("?") + 1).split("&").forEach((pair) => {
    const [k, v] = pair.split("=");
    out[decodeURIComponent(k)] = decodeURIComponent(v || "");
  });
  return out;
};

describe("(1) the outcomes axis says what the outcome IS, not what it is filed under", () => {
  it("prints the API's own label for a TENANT outcome the reader's resx has never heard of", () => {
    const screen = boot();
    screen.render(report([
      { code: "OUT_OF_SCOPE", count: 6, label: "Kapsam dışı" },
      { code: "CAPA_RAISED", count: 2, label: "CAPA açıldı" }
    ]));

    expect(axis(), "the axis is still printing the engine's identities at a reader")
      .toEqual(["Kapsam dışı", "CAPA açıldı"]);
    expect(axis().join(" ")).not.toMatch(/OUT_OF_SCOPE|CAPA_RAISED/);
  });

  it("prefers the API's label over the resx map when BOTH can name the same code", () => {
    /*
     * The order matters and this is the test that pins it: the server names an outcome from the type's own
     * closure dictionary — the words an administrator may since have edited — and the resx map is a static
     * copy of the five outcomes we ship. The live answer wins.
     */
    const screen = boot();
    screen.render(report([{ code: "COMPLETED_PARTIALLY", count: 4, label: "Kısmen tamamlandı" }]));

    expect(axis()).toEqual(["Kısmen tamamlandı"]);
    expect(axis()[0], "the static resx copy overrode the live answer")
      .not.toBe(OUTCOME_LABELS.COMPLETED_PARTIALLY);
  });

  it("falls to the reader's OWN resx for a SYSTEM outcome, which the server cannot translate", () => {
    /*
     * ⚠ A NULL LABEL IS THE SERVER BEING HONEST, NOT A GAP. A system outcome names itself with a
     * `LabelResourceKey`, and Platform has no localizer at all — so it sends nothing and the reader's
     * language answers here. Sending the key would put `WorkAggregation_ClosureOutcome_*` on this axis.
     */
    const screen = boot();
    screen.render(report([{ code: "COMPLETED_AS_REQUESTED", count: 5, label: null }]));

    expect(axis()).toEqual([OUTCOME_LABELS.COMPLETED_AS_REQUESTED]);
    expect(axis()[0]).not.toMatch(/COMPLETED_AS_REQUESTED|WorkAggregation_/);
  });

  it("shows the CODE when neither side can name it — and invents nothing", () => {
    const screen = boot();
    screen.render(report([{ code: "NOT_RECURRING", count: 3, label: null }]));

    expect(axis()).toEqual(["NOT_RECURRING"]);
  });

  it("labels change no number: the counts drawn are exactly the ones published", () => {
    const screen = boot();
    screen.render(report([
      { code: "OUT_OF_SCOPE", count: 6, label: "Kapsam dışı" },
      { code: "NOT_RECURRING", count: 3, label: null }
    ]));

    const chart = drawn.find((d) => d.host === document.querySelector("[data-wr-chart-outcomes]"));
    expect(chart.options.series[0].data).toEqual([6, 3]);
  });
});

describe("(2) the label is display only — the identity is what travels", () => {
  it("opens the slice with the CODE even when a label is sitting right beside it", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(report([{ code: "OUT_OF_SCOPE", count: 6, label: "Kapsam dışı" }]));

    clickOutcome(0);
    expect(fetchCalls.length, "the click issued no request at all").toBe(1);
    const query = parseQuery(fetchCalls[0]);
    expect(query.bucket).toBe("Outcome");
    expect(query.argument).toBe("OUT_OF_SCOPE");
    expect(query.argument, "the display label leaked onto the wire").not.toBe("Kapsam dışı");
  });

  it("titles the drill-down panel with the same words the axis used", () => {
    const screen = boot();
    screen.render(report([{ code: "OUT_OF_SCOPE", count: 6, label: "Kapsam dışı" }]));

    expect(screen.cellTitle("Outcome", "OUT_OF_SCOPE")).toBe("Kapsam dışı");
  });

  it("forgets a previous report's words rather than carrying them into the next one", () => {
    const screen = boot();
    screen.render(report([{ code: "OUT_OF_SCOPE", count: 6, label: "Kapsam dışı" }]));
    screen.render(report([{ code: "OUT_OF_SCOPE", count: 1, label: null }]));

    expect(axis(), "a stale label outlived the response it came from").toEqual(["OUT_OF_SCOPE"]);
  });
});

describe("(3) UI-027 — nothing on this page opens onto an empty menu", () => {
  it("has no dropdown menu without entries, and no toggle without a menu", () => {
    // Presence first: this really is the report's own toolbar being read.
    expect(VIEW).toContain("data-bs-target=\"#wrFilterCollapse\"");

    const menus = VIEW.match(/<ul[^>]*dropdown-menu[^>]*>([\s\S]*?)<\/ul>/g) || [];
    menus.forEach((menu) => {
      const inner = menu.replace(/^<ul[^>]*>/, "").replace(/<\/ul>$/, "").trim();
      expect(inner, `an empty dropdown menu is on the page: ${menu}`).not.toBe("");
    });

    const toggles = (VIEW.match(/data-bs-toggle="dropdown"/g) || []).length;
    expect(toggles, "a dropdown toggle with no menu behind it").toBe(menus.length);
  });

  it("dropped the button's string from all seven languages with the button itself", () => {
    // A translated word nobody renders is how the next empty control gets its label for free.
    LANGS.forEach((lang) => {
      expect(resx(lang), `${lang} still carries the removed Action string`).not.toContain('name="Action"');
    });
  });
});
