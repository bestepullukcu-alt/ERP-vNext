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
  scopeScoped: "Your scope", scopeTenant: "Whole tenant",
  scopeScopedHint: "Counts only the work you are entitled to see.",
  scopeTenantHint: "Counts every task in the tenant.",
  noData: "No work was opened or closed in this period.",
  noDataScoped: "No work you can see was opened or closed in this period.",
  loading: "Loading…", loadFailed: "The report could not be loaded.",
  periodInvalid: "The end of the period must come after its start.",
  flowTitle: "Opened and closed", opened: "Opened", closed: "Closed", completed: "Completed", cancelled: "Cancelled",
  onTime: "On time", late: "Late", withoutDueDate: "No deadline set", timelinessTitle: "Against the deadline",
  outcomesEmpty: "No closure was recorded with an outcome.",
  groupsTitle: "By {0}", groupUnnamed: "Not set", groupByOrganizationUnit: "Organisation unit",
  cycleTimeDays: "{0} days", cycleTimeOver: "over {0} closed", reworkTasks: "{0} tasks",
  reworkReturns: "{0} returns in total", effortHours: "{0} h estimated · {1} h spent", effortOver: "over {0} tasks",
  notMeasured: "Not measured", groupByLegalEntity: "Company", groupUnassigned: "Company unknown",
  groupOther: "All other groups", groupsTruncated: "Showing the busiest {0}; {1} more are folded in.",
  filterAny: "Any", priorityHigh: "High", priorityMedium: "Medium", priorityLow: "Low",
  cycleTimeMedian: "median {0}", cancelTime: "Until cancelled: average {0} · median {1} ({2} cancelled)",
  agingBuckets: "Age of open work — 0-7 days: {0} · 8-30 days: {1} · 30+ days: {2}",
  trendSame: "same as the previous period", trendUp: "+{0} against the previous period (was {1})",
  trendDown: "-{0} against the previous period (was {1})",
  itemsClose: "Close", itemsEmpty: "No tasks match this cell.", itemsLoadFailed: "The list could not be loaded.",
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
  <div data-wr-tiles hidden>
    <p data-wr-cycle-value></p><p data-wr-cycle-median></p><p data-wr-cycle-over></p>
    <p data-wr-cycle-trend hidden></p><p data-wr-cancel-value hidden></p>
    <p data-wr-rework-tasks data-wr-click="Returned" role="button" tabindex="0"></p>
    <p data-wr-rework-returns></p><p data-wr-rework-trend hidden></p>
    <p data-wr-unattended-value data-wr-click="Unattended" role="button" tabindex="0"></p>
    <p data-wr-aging hidden></p>
    <p data-wr-effort-value></p><p data-wr-effort-over></p>
  </div>
  <select id="wrLegalEntity"><option value="">Any</option></select>
  <select id="wrUnit"><option value="">Any</option></select>
  <select id="wrTaskType"><option value="">Any</option></select>
  <select id="wrAssignee"><option value="">Any</option></select>
  <select id="wrPriority"><option value="">Any</option></select>
  <div data-wr-charts hidden>
    <div data-wr-chart-flow></div><p data-wr-flow-trend hidden></p>
    <div data-wr-chart-outcomes></div><p data-wr-outcomes-empty hidden></p>
    <div data-wr-chart-timeliness></div><p data-wr-late-trend hidden></p>
    <div data-wr-groups-card hidden><h6 data-wr-groups-title></h6><div data-wr-chart-groups></div>
      <p data-wr-groups-truncated hidden></p></div>
  </div>
  <div class="offcanvas" id="wrItemsOffcanvas">
    <div class="offcanvas-header"><h5 data-wr-items-title>-</h5><small data-wr-items-subtitle>-</small></div>
    <div class="offcanvas-body">
      <p data-wr-items-count></p>
      <p data-wr-items-loading hidden></p>
      <p data-wr-items-empty hidden>${LABELS.itemsEmpty}</p>
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

  global.fetch = (url) => {
    fetchCalls.push(url);
    return Promise.resolve({ ok: false, json: () => Promise.resolve(null) });
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

  it("opens the three ageing bands SEPARATELY, from inside one templated sentence", () => {
    /*
     * ⚠ THE SENTENCE ITSELF IS UNTOUCHED — Dilim 1b's own test reads `.textContent` of this element and
     * expects the full localized string. Wrapping the three numbers in clickable spans must not change that
     * concatenation, which is exactly what this test (and 1b's, run alongside it) proves.
     */
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    expect(text("[data-wr-aging]")).toBe("Age of open work — 0-7 days: 2 · 8-30 days: 1 · 30+ days: 5");

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

  it("opens the TIMELINESS chart's own series — one point each, series is the click", () => {
    const screen = boot();
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });
    screen.render(busy());

    clickChart("[data-wr-chart-timeliness]", 1, 0);   // the "Late" series
    expect(parseQuery(fetchCalls[0]).bucket).toBe("Late");
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
     */
    const clickHandlers = SCRIPT.match(/dataPointSelection:\s*function[^}]*\{[^}]*\}/g) || [];
    expect(clickHandlers.length, "no chart click handlers were found — the guard is pointing at nothing")
      .toBeGreaterThanOrEqual(4);
    clickHandlers.forEach((handler) => {
      expect(handler, "a chart click handler bypasses openItems").toMatch(/openItems\(/);
      expect(handler, "a chart click handler calls fetch directly").not.toMatch(/\bfetch\(/);
    });
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
      expect(text("[data-wr-items-empty]")).toBe(LABELS.itemsEmpty);
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
    expect(arithmetic).not.toMatch(/efficiency|multiplier|productivityScore|percent/i);
  });

  it("this slice added no card and no chart — only a panel that opens beside the report", () => {
    // Layout is Dilim 1d. Four tiles, four chart slots — unchanged since 1b.
    expect((VIEW.match(/data-wr-chart-/g) || []).length).toBe(4);
    expect((VIEW.match(/data-wr-tiles/g) || []).length).toBeGreaterThan(0);
  });
});
