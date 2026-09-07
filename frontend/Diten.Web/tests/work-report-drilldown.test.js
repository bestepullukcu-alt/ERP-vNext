const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DILIM 1c — FROM A NUMBER TO THE WORK ITSELF, ON THE SCREEN.
 *
 * ⚠ THE BACKEND'S ACCEPTANCE CRITERION WAS AN IDENTITY (list length === the number). This file's job is
 * narrower and different: prove that every clickable number on screen actually SENDS that identity's bucket,
 * SAME FILTERS as the numbers on screen, and NOTHING invented — not that the identity holds (Platform's own
 * WorkReportItemsTests owns that).
 *
 * ⚠ PRESENCE FIRST, as every file in this module insists on. A "the wrong thing did NOT happen" assertion is
 * worthless unless the RIGHT thing is proven to happen first in the same test.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "WorkReport", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const L10N = fs.readFileSync(web("Views", "Tasks", "_WorkReportL10n.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

const NEW_KEYS = [
  "ItemsClose", "ItemsEmpty", "ItemsLoadFailed", "ItemsCount", "ItemsShowMore", "ItemsStatusOpen",
  "ItemsUnassigned", "ItemsSubtitleRange", "ItemsSubtitleWithGroup",
  "ItemsAging0to7", "ItemsAging8to30", "ItemsAging30plus", "ItemsColDue", "ItemsColClosed"
];

const LABELS = {
  summaryTitle: "Period summary", summaryPeriod: "Period", summaryDays: "{0} days",
  summaryScopeNote: "{0} · {1}",

  unitDays: "days", unitTasks: "tasks", unitOpenWork: "open",
  reworkQualifier: "work sent back to its owner", unattendedQualifier: "nobody has taken these on",
  labelMedian: "Median", labelUntilCancelled: "Until cancelled", labelCancelled: "Cancelled",
  labelTotalReturns: "Total returns", labelEstimated: "Estimated",
  agingUpTo7Label: "0-7 days", agingFrom8To30Label: "8-30 days", agingOlderThan30Label: "30+ days",
  effortSpentUnit: "h spent", hours: "{0} h",

  scopeScoped: "Your scope", scopeTenant: "Whole tenant",
  scopeScopedHint: "Counts only the work you are entitled to see.",
  scopeTenantHint: "Counts every task in the tenant.",
  noData: "No work was opened or closed in this period.",
  noDataScoped: "No work you can see was opened or closed in this period.",
  loading: "Loading…", loadFailed: "The report could not be loaded.",
  periodInvalid: "The end of the period must come after its start.",
  flowTitle: "Opened and closed", opened: "Opened", closed: "Closed", completed: "Completed", cancelled: "Cancelled",
  onTime: "On time", late: "Late", withoutDueDate: "No deadline set", timelinessTitle: "Against the deadline",
  sharePercent: "{0}%",
  outcomesEmpty: "No closure was recorded with an outcome.",
  groupsTitle: "By {0}", groupUnnamed: "Not set", groupByOrganizationUnit: "Organisation unit",
  cycleTimeDays: "{0} days", cycleTimeOver: "over {0} closed",
  effortOver: "over {0} tasks",
  notMeasured: "Not measured", groupByLegalEntity: "Company", groupUnassigned: "Company unknown",
  groupOther: "All other groups", groupsTruncated: "Showing the busiest {0}; {1} more are folded in.",
  filterAny: "Any", priorityHigh: "High", priorityMedium: "Medium", priorityLow: "Low",
  trendSame: "same as the previous period", trendUp: "+{0} against the previous period (was {1})",
  trendDown: "-{0} against the previous period (was {1})",
  itemsClose: "Close", itemsEmpty: "No tasks match this cell.",
  itemsEmptyHint: "No task in the selected period falls into this measure.", itemsLoadFailed: "The list could not be loaded.",
  itemsCount: "Showing {0} of {1}", itemsShowMore: "Show {0} more", itemsStatusOpen: "Open",
  itemsUnassigned: "Unassigned", itemsSubtitleRange: "{0} – {1}", itemsSubtitleWithGroup: "{0} · {1}",
  itemsAging0to7: "Open 0-7 days", itemsAging8to30: "Open 8-30 days", itemsAging30plus: "Open 30+ days",
  itemsColDue: "due {0}", itemsColClosed: "closed {0}", reworkTitle: "Came back", unattendedTitle: "Waiting for someone"
};

const OUTCOME_LABELS = { CORRECTED: "Corrected", OUT_OF_SCOPE: "Out of scope" };

const MARKUP = `
  <form id="workReportFilter">
    <input type="date" id="wrFrom" value="2026-06-01" /><input type="date" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option><option value="OrganizationUnit">Unit</option></select>
    <button type="submit">Apply</button>
  </form>
  <div data-wr-scope hidden><span data-wr-scope-badge></span><span data-wr-scope-hint></span></div>
  <p data-wr-status></p>
  <div data-wr-skeleton-tiles hidden></div>
  <div data-wr-skeleton-charts hidden></div>
  <div data-wr-tiles hidden>
    <p data-wr-cycle-value></p><p data-wr-cycle-unit></p><p data-wr-cycle-over></p>
    <dl data-wr-cycle-facts></dl><p data-wr-cycle-trend hidden></p>
    <p data-wr-rework-tasks data-wr-click="Returned" role="button" tabindex="0"></p><dl data-wr-rework-facts></dl><p data-wr-rework-trend hidden></p>
    <p data-wr-unattended-value data-wr-click="Unattended" role="button" tabindex="0"></p><dl data-wr-aging hidden></dl>
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
      <p data-wr-items-count></p>
      <p data-wr-items-loading hidden></p>
      <div data-wr-items-empty hidden><p>${LABELS.itemsEmpty}</p><p>${LABELS.itemsEmptyHint}</p></div>
      <p data-wr-items-error hidden>${LABELS.itemsLoadFailed}</p>
      <div data-wr-items-list></div>
      <button type="button" data-wr-items-more hidden></button>
    </div>
  </div>
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">${JSON.stringify(OUTCOME_LABELS)}</script>
`;

let drawn = [];
let fetchCalls = [];
let fetchResponse = null;

/** A minimal, faithful ApexCharts double that ALSO fires the click handler this file wires up — the harness
 *  used by the earlier WorkReport suites never needed to, because nothing was clickable before this slice. */
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

  fetchResponse = null;
  global.fetch = (url) => {
    fetchCalls.push(url);
    /*
     * ⚠ THE DEFAULT IS STILL A FAILED FETCH. Every test written before this one asserts on the REQUEST and
     * relies on nothing rendering; only a test that sets `fetchResponse` gets a body back, so adding one
     * cannot quietly change what the others are measuring.
     */
    return fetchResponse
      ? Promise.resolve({ ok: true, json: () => Promise.resolve(fetchResponse) })
      : Promise.resolve({ ok: false, json: () => Promise.resolve(null) });
  };

  loadScript("wwwroot/assets/js/Tasks/WorkReport/index.js");
  return window.WorkReportScreen;
};

/** Fires a chart's own click handler exactly as ApexCharts would — through the SHIPPED options object, not a
 *  copy of the mapping logic (that mapping is the thing under test). */
const clickChart = (selector, seriesIndex, dataPointIndex) => {
  const chart = drawn.find((d) => d.host === document.querySelector(selector));
  if (!chart) { throw new Error(`no chart drawn at ${selector}`); }
  chart.options.chart.events.dataPointSelection(null, null, { seriesIndex, dataPointIndex });
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

const report = (over) => Object.assign({
  from: "2026-06-01T00:00:00+00:00", to: "2026-07-01T00:00:00+00:00",
  scopeApplied: "scoped", groupBy: "None", totals: bucket(), groups: [], groupsTruncated: 0, previous: null
}, over || {});

const busy = (over) => report(Object.assign({
  totals: bucket({
    flow: { opened: 12, closed: 9, completed: 7, cancelled: 2, unattended: 4 },
    cycleTime: { averageDays: 11.33, medianDays: 10, count: 7 },
    cancellationTime: { averageDays: 8.75, medianDays: 8, count: 2 },
    aging: { upTo7Days: 2, from8To30Days: 1, olderThan30Days: 5 },
    timeliness: { onTime: 5, late: 3, withoutDueDate: 1 },
    outcomes: [{ code: "CORRECTED", count: 6 }, { code: "OUT_OF_SCOPE", count: 2 }],
    rework: { tasksReturned: 3, totalReturns: 5 }
  })
}, over || {}));

const text = (selector) => document.querySelector(selector).textContent;
const hidden = (selector) => document.querySelector(selector).hidden;

const parseQuery = (url) => {
  const q = url.slice(url.indexOf("?") + 1);
  const out = {};
  q.split("&").forEach((pair) => {
    const [k, v] = pair.split("=");
    out[decodeURIComponent(k)] = decodeURIComponent(v || "");
  });
  return out;
};

describe("(1) every published cell is reachable, and only through the shared door", () => {
  it("opens the tiles' own kind — unattended and returned", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    document.querySelector("[data-wr-unattended-value]").click();
    expect(fetchCalls.length, "clicking the tile issued no request").toBe(1);
    expect(parseQuery(fetchCalls[0]).bucket).toBe("Unattended");

    document.querySelector("[data-wr-rework-tasks]").click();
    expect(parseQuery(fetchCalls[1]).bucket).toBe("Returned");
  });

  it("opens the three ageing bands SEPARATELY, one row each", () => {
    /*
     * ⚠ THESE WERE THREE NUMBERS INSIDE ONE LOCALIZED SENTENCE UNTIL THE CARDS GOT ONE SKELETON, and what this
     * test guards did not change with the shape: three bands, three DIFFERENT buckets, in the order the card
     * lists them. The old failure it was written against — one handler wired to all three, so every band
     * opened the first one — is just as possible in rows as it was in spans.
     */
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    const rows = [...document.querySelectorAll("[data-wr-aging] dt")].map((dt) => dt.textContent);
    expect(rows, "the bands stopped being named").toEqual(["0-7 days", "8-30 days", "30+ days"]);

    const spans = document.querySelectorAll("[data-wr-aging] [data-wr-click]");
    expect(spans, "no clickable spans were built inside the sentence").toHaveLength(3);
    expect(Array.from(spans).map((el) => el.getAttribute("data-wr-click")))
      .toEqual(["AgingUpTo7Days", "AgingFrom8To30Days", "AgingOlderThan30Days"]);

    spans[2].click();
    expect(parseQuery(fetchCalls[0]).bucket).toBe("AgingOlderThan30Days");
  });

  it("opens the FLOW chart's own bars, in the order the bars are drawn", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    clickChart("[data-wr-chart-flow]", 0, 2);   // the "Completed" bar
    expect(parseQuery(fetchCalls[0]).bucket).toBe("Completed");
  });

  it("EVERY slice of the timeliness ring opens its OWN band — not just the one that happens to be first", () => {
    /*
     * ⚠ THIS TEST GREW A CASE WHEN THE CHART BECAME A DONUT, AND THE REASON IS THE WHOLE POINT.
     * As a stacked bar each band was its own SERIES and the handler read `seriesIndex`; as a donut the three
     * bands are three POINTS of one series and apex reports them in `dataPointIndex`. A handler left reading
     * the old field throws nothing and logs nothing — `seriesIndex` is 0 for every slice of a donut, so every
     * click would have opened "on time", and a reader chasing late work would have been handed the opposite
     * of what they asked for. One case per slice is what makes that failure visible.
     */
    const bands = [[0, "OnTime"], [1, "Late"], [2, "WithoutDueDate"]];
    bands.forEach(([index, bucket]) => {
      const screen = boot();
      screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
      screen.render(busy());
      fetchCalls = [];   // each slice is measured on its own, not on the tail of the previous one

      clickChart("[data-wr-chart-timeliness]", 0, index);
      expect(parseQuery(fetchCalls[0]).bucket, `slice ${index} opened the wrong band`).toBe(bucket);
    });
  });

  it("opens an OUTCOME slice with the RAW CODE, never the translated label", () => {
    /*
     * The chart's own `series`/`labels` are built from `rows` in display order — the click handler must read
     * the CODE off that same array rather than reversing the translation, which would break for a tenant
     * outcome that has no entry in the outcome map at all.
     */
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    clickChart("[data-wr-chart-outcomes]", 0, 1);   // second slice = OUT_OF_SCOPE, per `busy()`'s order
    expect(parseQuery(fetchCalls[0]).bucket).toBe("Outcome");
    expect(parseQuery(fetchCalls[0]).argument).toBe("OUT_OF_SCOPE");
    expect(parseQuery(fetchCalls[0]).argument, "a translated label leaked onto the wire")
      .not.toBe(OUTCOME_LABELS.OUT_OF_SCOPE);
  });

  it("opens a GROUP bar with the group's own KEY, and the series decides Opened vs Closed", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy({
      groupBy: "OrganizationUnit",
      groups: [
        bucket({ key: "unit-a", label: "Finance", flow: { opened: 6, closed: 4, completed: 4, cancelled: 0, unattended: 0 } }),
        bucket({ key: "__unassigned__", flow: { opened: 2, closed: 1, completed: 1, cancelled: 0, unattended: 0 } })
      ]
    }));

    clickChart("[data-wr-chart-groups]", 1, 0);   // series 1 = Closed, point 0 = "unit-a"
    expect(parseQuery(fetchCalls[0]).bucket).toBe("Closed");
    expect(parseQuery(fetchCalls[0]).groupKey).toBe("unit-a");

    clickChart("[data-wr-chart-groups]", 0, 1);   // series 0 = Opened, point 1 = the reserved key
    expect(parseQuery(fetchCalls[1]).groupKey).toBe("__unassigned__");
  });

  it("does no fetching of its own outside the one shared function", () => {
    /*
     * ⚠ THE STRUCTURAL GUARD — the same shape as Platform's `The_items_read_issues_NO_QUERY_OF_ITS_OWN`. Every
     * click handler in this file is REQUIRED to call `openItems`; a chart wired to its own inline `fetch` would
     * pass every test above on the day it was written and drift the day either copy was edited next.
     *
     * ⚠ BRACE-BALANCED EXTRACTION, NOT A NAIVE REGEX. A first version of this guard used
     * `/dataPointSelection:\s*function[^}]*\{[^}]*\}/`, which stops at the FIRST `}` — so the moment Dilim 1d
     * gave the outcomes handler an early-return `if (...) { return; }` before its own `openItems(...)` call,
     * the regex captured only the `if` block and reported a false bypass. The handler body is read the same way
     * `WorkReportItemsTests.MethodBody` reads a C# method on the backend: find the opening brace, count depth,
     * stop when it returns to zero.
     */
    const handlerStarts = [];
    const marker = /dataPointSelection:\s*function[^{]*\{/g;
    let match;
    while ((match = marker.exec(SCRIPT))) { handlerStarts.push(match.index + match[0].length - 1); }

    expect(handlerStarts.length, "no chart click handlers were found — the guard is pointing at nothing")
      .toBeGreaterThanOrEqual(4);

    const handlerBody = (openBraceIndex) => {
      let depth = 0;
      for (let i = openBraceIndex; i < SCRIPT.length; i++) {
        if (SCRIPT[i] === "{") depth++;
        else if (SCRIPT[i] === "}" && --depth === 0) { return SCRIPT.slice(openBraceIndex, i + 1); }
      }
      throw new Error("unbalanced braces after a dataPointSelection handler");
    };

    handlerStarts.map(handlerBody).forEach((handler) => {
      expect(handler, "a chart click handler bypasses openItems").toMatch(/openItems\(/);
      expect(handler, "a chart click handler calls fetch directly").not.toMatch(/\bfetch\(/);
    });
  });
});

describe("(1c) the period summary repeats figures, never meanings", () => {
  it("publishes the flow the tiles and bars already publish, from the same payload", () => {
    const screen = boot();
    screen.render(busy());

    // busy(): opened 12, closed 9, completed 7, cancelled 2, unattended 4.
    expect(text("[data-wr-summary-opened]")).toBe("12");
    expect(text("[data-wr-summary-closed]")).toBe("9");
    expect(text("[data-wr-summary-completed]")).toBe("7");
    expect(text("[data-wr-summary-cancelled]")).toBe("2");
    expect(text("[data-wr-summary-unattended]")).toBe("4");
    expect(hidden("[data-wr-summary]"), "the summary never appeared").toBe(false);
  });

  it("counts the period's days with `to` EXCLUSIVE, the way the query sends it", () => {
    /*
     * ⚠ `to` IS THE FIRST DAY NOT COUNTED — that is what the picker's value means and what `load()` sends.
     * A plain difference is therefore already the number of days IN the period; the tempting "+1 to include
     * both ends" would report 31 for a month and disagree with every count beside it.
     */
    const screen = boot();
    screen.render(busy({ from: "2026-06-01T00:00:00Z", to: "2026-07-01T00:00:00Z" }));
    expect(text("[data-wr-summary-days]")).toBe("30 days");
  });

  it("names the scope the SERVER applied, not the one a chip asked for", () => {
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));
    expect(text("[data-wr-summary-note]")).toContain(LABELS.scopeTenant);

    screen.render(busy({ scopeApplied: "scoped" }));
    expect(text("[data-wr-summary-note]"), "the note kept a scope the report was not computed under")
      .toContain(LABELS.scopeScoped);
  });

  it("opens the SAME bucket the tile beside it opens — every row of it", async () => {
    /*
     * ⚠ THIS IS WHAT PAYS FOR THE REPETITION. Every figure in this panel is also somewhere else on the page,
     * which is only safe while the two cannot disagree about WHICH ROWS they mean. A summary row wired to a
     * bucket of its own — or to none — would be a second, unwatched answer to a question the tiles already
     * answer.
     */
    const rows = [["opened", "Opened"], ["closed", "Closed"], ["completed", "Completed"],
                  ["cancelled", "Cancelled"], ["unattended", "Unattended"]];

    /*
     * ⚠ THE SHIPPED MARKUP IS CHECKED FIRST, AND THE FIRST DRAFT OF THIS TEST DID NOT. The buckets are static
     * attributes in the view; the jsdom fixture carries its OWN copy of them, so a row mis-wired in
     * `WorkReport.cshtml` — cancelled pointing at "Closed", say — left this test measuring the fixture's
     * agreement with itself and passing. Live-sabotaged and confirmed green before this block existed. The
     * fixture proves the CLICK reaches `openItems`; only the view can prove it names the right bucket.
     */
    rows.forEach(([hook, bucket]) => {
      const tag = new RegExp(`data-wr-summary-${hook}[^>]*`).exec(VIEW);
      expect(tag, `the ${hook} row is gone from the view`).toBeTruthy();
      expect(tag[0], `the ${hook} row is wired to the wrong bucket in the shipped markup`)
        .toContain(`data-wr-click="${bucket}"`);
    });

    for (const [hook, bucket] of rows) {
      const screen = boot();
      screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
      screen.render(busy());
      fetchCalls = [];

      document.querySelector(`[data-wr-summary-${hook}]`).click();
      expect(parseQuery(fetchCalls[0]).bucket, `the ${hook} row opened the wrong work`).toBe(bucket);
    }
  });

  it("computes no rate — closed over opened is not a completion rate", () => {
    /*
     * The design this panel came from carried "completion rate 19% · 16/83". The work that CLOSED in a period
     * is mostly not the work that OPENED in it, so that division is not a rate of anything: it falls when
     * demand rises even if the team finished twice as much. Checked in the shipped renderer, because it is a
     * one-line convenience away from coming back.
     */
    const fn = SCRIPT.slice(SCRIPT.indexOf("var renderSummary"), SCRIPT.indexOf("var renderTiles"));
    expect(fn, "the summary started dividing published figures")
      .not.toMatch(/flow\.(opened|closed|completed)\s*\/|\/\s*flow\.(opened|closed)/);
    expect(fn, "a rate or a share appeared in the summary").not.toMatch(/rate|percent|share/i);
  });
});

describe("(1b) one result, one card — what a row actually says", () => {
  const openWith = async (items) => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());
    fetchResponse = { data: { total: items.length, skip: 0, hasMore: false, items } };
    await screen.openItems("Closed");
    return [...document.querySelectorAll("[data-wr-items-list] a")];
  };

  it("renders each result as the product's OWN work-item row, linking to the task", async () => {
    /*
     * ⚠ THE ROW IS BORROWED, NOT BUILT. `.wcn-row` is Görev Merkezi's work-item card — padding, radius,
     * hover tint, focus ring and skin-aware border/shadow already decided. This panel lists work items too,
     * so a bespoke row here would be a second answer to a question the product already answered, drifting
     * from it the first time either side is touched.
     */
    const rows = await openWith([
      { id: "t1", title: "Regression — open subtask", lifecycle: "Done", assigneeUserId: "u1",
        dueAt: "2026-09-01T00:00:00Z", closedAt: "2026-08-25T00:00:00Z" }
    ]);

    expect(rows).toHaveLength(1);
    expect(rows[0].className, "the row stopped being the shared work-item card").toContain("wcn-row");
    expect(rows[0].getAttribute("href"), "the row stopped opening the task").toBe("/WorkCenterNext/Details/t1");
  });

  it("says the state in WORDS as well as in colour, and the two always agree", async () => {
    /*
     * ⚠ COLOUR ALONE EXCLUDES ANYONE WHO CANNOT SEPARATE THE TWO HUES, so the badge spells the state out and
     * the bar is a scanning aid on top of a label. And they are read from ONE table in the script: a row
     * whose bar said "done" while its badge said "cancelled" would be worse than a row with neither, because
     * a reader trusts a colour faster than they read a word and would be trusting the wrong one.
     */
    const rows = await openWith([
      { id: "d", title: "Finished", lifecycle: "Done" },
      { id: "c", title: "Abandoned", lifecycle: "Cancelled" },
      { id: "o", title: "Still going", lifecycle: "InProgress" }
    ]);

    const read = (row) => ({
      word: row.querySelector(".badge").textContent,
      badge: row.querySelector(".badge").className,
      accent: row.querySelector(".wcn-row-accent").className
    });

    expect(rows.map(read)).toEqual([
      { word: LABELS.completed, badge: "badge bg-label-success", accent: "wcn-row-accent wcn-row-accent-success" },
      { word: LABELS.cancelled, badge: "badge bg-label-danger", accent: "wcn-row-accent wcn-row-accent-danger" },
      // Every non-terminal state reads as one word here; which one it is in detail is the detail page's question.
      { word: LABELS.itemsStatusOpen, badge: "badge bg-label-secondary", accent: "wcn-row-accent wcn-row-accent-secondary" }
    ]);
  });

  it("stacks the facts instead of stringing them into one sentence", async () => {
    /*
     * ⚠ THE DEFECT THIS PINS. Status, person and two dates used to be one line of dot-separated clauses that
     * broke wherever the panel's width happened to fall, so no two rows stood the same height and nothing
     * lined up down the list. Title and state on the first line, person and dates on the second.
     */
    const rows = await openWith([
      { id: "t1", title: "Regression — open subtask", lifecycle: "Done", assigneeUserId: "u1",
        dueAt: "2026-09-01T00:00:00Z", closedAt: "2026-08-25T00:00:00Z" }
    ]);

    expect(rows[0].querySelector(".wr-item-title").textContent).toBe("Regression — open subtask");
    // The person is named in full beside the monogram — two letters alone would identify nobody.
    expect(rows[0].querySelector(".wr-item-who").textContent).toContain("u1");
    expect(rows[0].querySelector(".wr-item-avatar").textContent).toBe("U1");
    expect(rows[0].querySelector(".wr-item-dates").textContent).toMatch(/due .+ · closed .+/);

    // And the state is NOT in the meta line any more — that is what "stacked" means here.
    expect(rows[0].querySelector(".wr-item-meta").textContent).not.toContain(LABELS.completed);
  });

  it("draws an empty cell as a state, with both of its lines", async () => {
    // As one muted sentence pinned to the top-left it read as a failure notice. PRESENCE FIRST: the rows above
    // prove the list renders, so an empty result here is a state rather than a panel that never worked.
    const rows = await openWith([]);
    expect(rows).toHaveLength(0);

    const empty = document.querySelector("[data-wr-items-empty]");
    expect(empty.hidden, "the empty state stayed hidden on an empty cell").toBe(false);
    expect(empty.textContent).toContain(LABELS.itemsEmpty);
    expect(empty.textContent, "the empty state lost its second line").toContain(LABELS.itemsEmptyHint);
  });
});

describe("(2) the list is asked under the SAME filters the numbers were computed under", () => {
  it("carries the period and the group-by exactly as the loaded report used", async () => {
    const screen = boot();
    document.querySelector("#wrTaskType").innerHTML = '<option value="DEV" selected>Dev</option>';
    global.fetch = (url) => {
      fetchCalls.push(url);
      return Promise.resolve({ ok: true, json: () => Promise.resolve(busy()) });
    };
    await screen.load();

    fetchCalls = [];
    document.querySelector("[data-wr-unattended-value]").click();

    const q = parseQuery(fetchCalls[0]);
    expect(q.from).toBe("2026-06-01T00:00:00Z");
    expect(q.to).toBe("2026-07-01T00:00:00Z");
    expect(q.taskTypeCode).toBe("DEV");
  });

  it("keeps using the LOADED query even if a picker changed afterward without Apply", () => {
    /*
     * ⚠ THE PROPERTY THAT MAKES THE PANEL TRUSTWORTHY. If a reader changes the priority filter but has not
     * pressed Apply, the numbers on screen are still the OLD query's — a click that read the picker's CURRENT
     * value would open a list for a report that is not the one being looked at.
     */
    const screen = boot();
    screen.setLastQuery({
      from: "2026-06-01", to: "2026-07-01", groupBy: "None",
      legalEntityId: "", organizationUnitId: "", taskTypeCode: "", assigneeUserId: "", priority: "High"
    });
    screen.setLastReport(busy());

    document.querySelector("#wrPriority").innerHTML = '<option value="Low" selected>Low</option>';

    screen.openItems("Unattended");
    expect(parseQuery(fetchCalls[0]).priority).toBe("High");
  });

  it("sends only the filters that were actually chosen — an untouched picker is absent, not empty", () => {
    const screen = boot();
    screen.setLastQuery({
      from: "2026-06-01", to: "2026-07-01", groupBy: "None",
      legalEntityId: "", organizationUnitId: "", taskTypeCode: "", assigneeUserId: "", priority: ""
    });
    screen.setLastReport(busy());

    screen.openItems("Late");
    const q = parseQuery(fetchCalls[0]);
    expect(fetchCalls[0]).not.toContain("priority=");
    expect(fetchCalls[0]).not.toContain("assigneeUserId=");
    expect(q.bucket).toBe("Late");
  });
});

describe("(3) the panel itself", () => {
  it("opens, shows the cell's title and the period, and asks for page one", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    global.fetch = (url) => {
      fetchCalls.push(url);
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve({
          data: {
            total: 3, skip: 0, hasMore: false,
            items: [
              { id: "t1", title: "Investigate the Finance backlog", lifecycle: "Open", assigneeUserId: null, dueAt: null, closedAt: null }
            ]
          }
        })
      });
    };

    return screen.openItems("Late").then(() => {
      expect(window.bootstrap.Offcanvas._shown, "the offcanvas was never shown").toBeTruthy();
      expect(text("[data-wr-items-title]")).toBe(LABELS.late);
      expect(text("[data-wr-items-subtitle]")).not.toBe("");
      expect(document.querySelector("[data-wr-items-list]").innerHTML).toContain("Investigate the Finance backlog");
    });
  });

  it("says NOTHING MATCHES rather than showing an empty list silently", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    global.fetch = () => Promise.resolve({
      ok: true, json: () => Promise.resolve({ data: { total: 0, skip: 0, hasMore: false, items: [] } })
    });

    return screen.openItems("Cancelled").then(() => {
      expect(hidden("[data-wr-items-empty]"), "the empty state never appeared").toBe(false);
      // ⚠ `toContain`, NOT `toBe` — the empty state grew a second, explanatory line when it stopped being one
      // muted sentence pinned to the top-left and became a state. What this test guards is that the panel
      // SAYS something rather than showing a blank list; both lines are asserted by name in (1b).
      expect(text("[data-wr-items-empty]")).toContain(LABELS.itemsEmpty);
    });
  });

  it("says the total is bigger than the page, never the page's own length", () => {
    /*
     * ⚠ THE SAME SUBSTITUTION `WorkReportTally.Page`'s own doc-comment warns against, one layer up: a screen
     * that read `rows.length` instead of the response's `total` would silently rewrite "83 opened" to "50
     * opened" — the exact defect the identity test on the backend exists to prevent, reachable again if the
     * screen ever stopped trusting the field.
     */
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    const fiftyRows = Array.from({ length: 50 }, (_, i) => ({ id: "t" + i, title: "Task " + i, lifecycle: "Open" }));
    global.fetch = () => Promise.resolve({
      ok: true, json: () => Promise.resolve({ data: { total: 83, skip: 0, hasMore: true, items: fiftyRows } })
    });

    return screen.openItems("Opened").then(() => {
      expect(text("[data-wr-items-count]")).toBe("Showing 50 of 83");
      expect(hidden("[data-wr-items-more]"), "the cut was made silently").toBe(false);
      expect(document.querySelector("[data-wr-items-more]").textContent).toContain("33");
    });
  });

  it("pressing SHOW MORE appends a second page rather than replacing the first — AND NEVER DUPLICATES it", () => {
    /*
     * ⚠ LIVE-VERIFIED BUG, PINNED HERE. The first version of this screen re-rendered the WHOLE cumulative
     * `itemsState.rows` and then appended that onto whatever HTML was already in the DOM — so a first page of
     * 50 followed by "show more" left 106 rows on screen (50 shown once, then the full 56 rendered again on
     * top). A `.toContain` assertion does not see a duplicate; only a ROW COUNT does.
     */
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());

    let call = 0;
    global.fetch = () => {
      call++;
      const items = call === 1
        ? [{ id: "a", title: "First page row", lifecycle: "Open" }]
        : [{ id: "b", title: "Second page row", lifecycle: "Open" }];
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve({ data: { total: 2, skip: 0, hasMore: call === 1, items } })
      });
    };

    return screen.openItems("Opened").then(() => {
      expect(document.querySelectorAll("[data-wr-items-list] a")).toHaveLength(1);
      expect(document.querySelector("[data-wr-items-list]").innerHTML).toContain("First page row");
      document.querySelector("[data-wr-items-more]").click();
      return new Promise((resolve) => setTimeout(resolve, 0));
    }).then(() => {
      const rows = document.querySelectorAll("[data-wr-items-list] a");
      const html = document.querySelector("[data-wr-items-list]").innerHTML;

      expect(rows, "the row count does not equal total pages combined — a duplicate or a loss").toHaveLength(2);
      expect(html, "the first page was lost on 'show more'").toContain("First page row");
      expect(html).toContain("Second page row");
      expect(
        html.split("First page row").length - 1,
        "the first page's row was rendered more than once"
      ).toBe(1);
    });
  });

  it("shows a failure sentence rather than a stuck spinner when the request fails", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    global.fetch = () => Promise.reject(new Error("network"));

    return screen.openItems("Opened").then(() => {
      expect(hidden("[data-wr-items-error]")).toBe(false);
      expect(hidden("[data-wr-items-loading]"), "the spinner never turned off").toBe(true);
    });
  });

  it("escapes a task title that carries HTML — a title is text somebody typed, not markup", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    global.fetch = () => Promise.resolve({
      ok: true,
      json: () => Promise.resolve({
        data: {
          total: 1, skip: 0, hasMore: false,
          items: [{ id: "x", title: '<img src=x onerror=alert(1)>', lifecycle: "Open" }]
        }
      })
    });

    return screen.openItems("Opened").then(() => {
      const html = document.querySelector("[data-wr-items-list]").innerHTML;
      expect(html).not.toContain("<img");
      expect(html).toContain("&lt;img");
    });
  });
});

describe("(4) the house rules travel with this slice too", () => {
  it("styles through classes only — no inline style anywhere (FG-003)", () => {
    [["script", SCRIPT], ["view", VIEW], ["l10n", L10N]].forEach(([name, source]) => {
      expect(source, `${name} uses a style attribute`).not.toMatch(/style="/);
      expect(source, `${name} writes element.style`).not.toMatch(/\.style\./);
    });
  });

  it("defines every new label in all seven languages, and actually translates them", () => {
    LANGS.forEach((lang) => {
      NEW_KEYS.forEach((key) => {
        expect(resx(lang).includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
      });
    });

    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1].trim() : null;
    };

    // Only the ones that are genuinely SENTENCES are checked for having moved — the two range-punctuation
    // keys (ItemsSubtitleRange/WithGroup) are legitimately identical across languages that all use "–"/"·".
    ["ItemsEmpty", "ItemsLoadFailed", "ItemsCount", "ItemsShowMore", "ItemsStatusOpen", "ItemsUnassigned"]
      .forEach((key) => {
        const english = value("en", key);
        expect(english, `${key} has no English text`).toBeTruthy();
        LANGS.filter((l) => l !== "en").forEach((lang) => {
          expect(value(lang, key), `${key}/${lang} is still the English text`).not.toBe(english);
        });
      });
  });

  it("carries every new label across the l10n bridge", () => {
    NEW_KEYS.forEach((key) => {
      expect(L10N.includes(`Localizer["${key}"]`), `${key} never crosses the l10n bridge`).toBe(true);
    });
  });

  it("no raw resource key or reserved wire code reaches the rendered panel", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.setLastReport(busy());
    global.fetch = () => Promise.resolve({
      ok: true,
      json: () => Promise.resolve({
        data: { total: 1, skip: 0, hasMore: false, items: [{ id: "x", title: "T", lifecycle: "Open" }] }
      })
    });

    return screen.openItems("AgingUpTo7Days").then(() => {
      const panelText = document.getElementById("wrItemsOffcanvas").textContent;
      NEW_KEYS.forEach((key) => expect(panelText, `${key} leaked as a raw key`).not.toContain(key));
      expect(panelText).not.toContain("AgingUpTo7Days");
    });
  });

  it("still computes no ratio, percentage or score anywhere in the file", () => {
    const arithmetic = SCRIPT.split("\n")
      .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
      .join("\n");
    expect(arithmetic).not.toMatch(/spentHours\s*\/|\/\s*estimatedHours/);
    /*
     * ⚠ THE WORD BAN NARROWED, AND THE RULE DID NOT (2026-09-04). This line used to also forbid the substring
     * "percent" anywhere in the file. That was a net cast around a NAME, and the timeliness ring's legend —
     * which states the share each band is OF THE RING ALREADY DRAWN — tripped it on `SharePercent`, a
     * localisation key. A composition of one axis is not the forbidden figure: pack §8 excludes dividing
     * ESTIMATE by SPENT and attributing the result to a person, because that makes people inflate estimates
     * and corrupts the only planning input the system has.
     *
     * So the ban now names what it means. The division itself is still forbidden by the line above, the
     * forbidden figure's names are still forbidden here, and `The_effort_card_never_publishes_a_share` was
     * added to watch the actual card — a guard on the rendered output, which the substring net never was.
     */
    expect(arithmetic).not.toMatch(/efficiency|multiplier|productivityScore/i);
  });

  it("this slice added no card and no chart — only a panel that opens beside the report", () => {
    // Layout is Dilim 1d. Four tiles, four chart slots — unchanged since 1b.
    expect((VIEW.match(/data-wr-chart-/g) || []).length).toBe(4);
    expect((VIEW.match(/data-wr-tiles/g) || []).length).toBeGreaterThan(0);
  });
});
