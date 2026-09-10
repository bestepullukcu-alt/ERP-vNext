const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DILIM 1f — "TÜM KİRACI" ↔ "SİZİN KAPSAMINIZ", ON THE SCREEN.
 *
 * ⚠ THE BACKEND'S OWN ACCEPTANCE CRITERION IS A NARROW-ONLY RULE (`WorkReportScopePreferenceTests`, Platform).
 * This file's job is different: prove the SCREEN sends the chosen preference, remembers it across a reload,
 * threads it into a drill-down exactly the way 1c's filters already do, and — the part a backend test cannot
 * see at all — shows the RIGHT UI SHAPE: two chips when tenant-wide has been proven reachable, the ONE quiet
 * badge from before this slice otherwise. A chip that cannot change anything when clicked reads as broken, so
 * "no chips for a reader who can't use them" is as much this slice's job as "chips that work for one who can".
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "WorkReport", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");

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
  trendDown: "-{0} against the previous period (was {1})"
};

const MARKUP = `
  <form id="workReportFilter">
    <input type="text" id="wrFrom" value="2026-06-01" /><input type="text" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option></select>
    <button type="submit">Apply</button>
  </form>
  <div data-wr-scope hidden>
    <span data-wr-scope-badge></span>
    <div data-wr-scope-chips hidden>
      <button data-wr-scope-chip="tenant" aria-pressed="false">${LABELS.scopeTenant}</button>
      <button data-wr-scope-chip="own" aria-pressed="false">${LABELS.scopeScoped}</button>
    </div>
    <span data-wr-scope-hint></span>
  </div>
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
      <p data-wr-items-empty hidden></p>
      <p data-wr-items-error hidden></p>
      <div data-wr-items-list></div>
      <button type="button" data-wr-items-more hidden></button>
    </div>
  </div>
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">${JSON.stringify({})}</script>
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
  window.bootstrap.Offcanvas = { getOrCreateInstance: () => ({ show: () => {}, hide: () => {} }) };

  global.fetch = (url) => { fetchCalls.push(url); return Promise.resolve({ ok: false, json: () => Promise.resolve(null) }); };

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

const report = (over) => Object.assign({
  from: "2026-06-01T00:00:00+00:00", to: "2026-07-01T00:00:00+00:00",
  scopeApplied: "scoped", groupBy: "None", totals: bucket(), groups: [], groupsTruncated: 0, previous: null
}, over || {});

const busy = (over) => report(Object.assign({
  totals: bucket({ flow: { opened: 12, closed: 9, completed: 7, cancelled: 2, unattended: 4 } })
}, over || {}));

const text = (selector) => document.querySelector(selector).textContent;
const hidden = (selector) => document.querySelector(selector).hidden;
const parseQuery = (url) => {
  const q = url.slice(url.indexOf("?") + 1);
  const out = {};
  q.split("&").forEach((pair) => { const [k, v] = pair.split("="); out[decodeURIComponent(k)] = decodeURIComponent(v || ""); });
  return out;
};

describe("(1) the two-chip / one-badge shape — learned, never guessed", () => {
  it("shows the ONE quiet badge, no chips, for a reader who has never been proven tenant-wide", () => {
    const screen = boot();
    screen.render(busy({ scopeApplied: "scoped" }));

    expect(hidden("[data-wr-scope-badge]"), "the badge never appeared").toBe(false);
    expect(hidden("[data-wr-scope-chips]"), "a chip pair appeared for a reader who cannot use it").toBe(true);
  });

  it("switches to two chips the FIRST time a response proves tenant-wide is reachable", () => {
    // PRESENCE FIRST: proven badge-only above, so the switch here is a real transition, not a fixture default.
    const screen = boot();
    screen.render(busy({ scopeApplied: "scoped" }));
    expect(hidden("[data-wr-scope-chips]")).toBe(true);

    screen.render(busy({ scopeApplied: "tenant" }));
    expect(hidden("[data-wr-scope-badge]"), "the static badge stayed visible once tenant-wide was proven").toBe(true);
    expect(hidden("[data-wr-scope-chips]")).toBe(false);
  });

  it("keeps both chips visible even after the reader picks 'your scope' — capability, once learned, is not forgotten", () => {
    /*
     * ⚠ THE REGRESSION THIS TEST NAMES. Learning capability from `scopeApplied === 'tenant'` and then re-basing
     * that flag off every response would make it FALSE the moment the reader chose "your scope" (which
     * legitimately returns `scopeApplied: 'scoped'`) — and the OTHER chip, "whole tenant", would vanish from
     * under them with no way back to it.
     */
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));
    expect(hidden("[data-wr-scope-chips]")).toBe(false);

    screen.render(busy({ scopeApplied: "scoped" }));
    expect(hidden("[data-wr-scope-chips]"), "the chip pair disappeared after switching to 'your scope'").toBe(false);
    expect(hidden("[data-wr-scope-badge]")).toBe(true);
  });

  it("marks the chip matching the APPLIED scope as pressed, never the other one", () => {
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));
    screen.render(busy({ scopeApplied: "scoped" }));

    expect(document.querySelector('[data-wr-scope-chip="own"]').getAttribute("aria-pressed")).toBe("true");
    expect(document.querySelector('[data-wr-scope-chip="tenant"]').getAttribute("aria-pressed")).toBe("false");

    /*
     * ⚠ AND IT LOOKS PRESSED, NOT ONLY READS AS PRESSED. `aria-pressed` alone was the whole of this test, and
     * when the control changed from an outline button pair to the product's shared segmented control the
     * class that DRAWS the selection changed with it — while this test stayed green throughout, because it
     * never looked at one. A segmented control whose selected half is not marked is two identical halves: a
     * sighted reader cannot tell which scope the numbers above are counting, which is the one thing this
     * strip exists to say. `.active` is `.wcn-seg.active` in backbone-custom.css; nothing here styles it.
     */
    expect(document.querySelector('[data-wr-scope-chip="own"]').classList.contains("active"),
      "the applied scope's segment is not drawn as the selected one").toBe(true);
    expect(document.querySelector('[data-wr-scope-chip="tenant"]').classList.contains("active"),
      "both segments are drawn as selected at once").toBe(false);
  });

  it("still changes the description sentence with the scope, chips or no chips", () => {
    // The hint is the part of Faz 5a's own protective intent this slice must not disturb.
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));
    expect(text("[data-wr-scope-hint]")).toBe(LABELS.scopeTenantHint);

    screen.render(busy({ scopeApplied: "scoped" }));
    expect(text("[data-wr-scope-hint]")).toBe(LABELS.scopeScopedHint);
  });
});

describe("(2) picking a scope sends the preference, and only the preference the reader chose", () => {
  /*
   * ⚠ jsdom's `document.readyState` is already `'complete'` when a test file's script loads, so the click
   * listener wired inside `DOMContentLoaded` (`chip.addEventListener('click', ...)`) never attaches in this
   * harness — the same reason 1d's `initSelect2`/`updateFilterCount` are exercised through exported hooks
   * rather than a real `.click()`. `setScopePreference` + `load()` is exactly what that listener's own body
   * does (see the structural guard below for proof the shipped listener really does just that); a REAL click
   * is exercised live, in the browser, in this slice's live verification.
   */
  it("choosing 'your scope' issues a request carrying scope=own", () => {
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));

    screen.setScopePreference("own");
    return screen.load().then(() => {
      expect(parseQuery(fetchCalls[fetchCalls.length - 1]).scope).toBe("own");
    });
  });

  it("choosing 'whole tenant' issues a request carrying scope=tenant", () => {
    const screen = boot();
    screen.render(busy({ scopeApplied: "scoped" }));

    screen.setScopePreference("tenant");
    return screen.load().then(() => {
      expect(parseQuery(fetchCalls[fetchCalls.length - 1]).scope).toBe("tenant");
    });
  });

  it("sends NO scope parameter at all until a preference has been chosen — the backward-compatibility shape", () => {
    /*
     * ⚠ THE FRONTEND HALF OF THE BACKEND'S OWN REGRESSION GUARD. Every page load before this slice never sent
     * this parameter; an omitted parameter is what keeps that caller's behaviour identical after 1f ships.
     */
    const screen = boot();
    return screen.load().then(() => {
      expect(fetchCalls[0]).not.toContain("scope=");
    });
  });

  it("the shipped click listener sets the preference and reloads — nothing more, nothing else", () => {
    // The structural half of the tests above: proves the handler jsdom cannot fire really does what the two
    // behavioural tests assume it does, the same pattern 1d's own "company→unit narrowing" guard uses.
    const handlerMatch = SCRIPT.match(/chip\.addEventListener\('click',\s*function\s*\(\)\s*\{([\s\S]*?)\}\);/);
    expect(handlerMatch, "the chip click listener was not found — the guard is pointing at nothing").toBeTruthy();
    expect(handlerMatch[1]).toMatch(/scopePreference\s*=\s*chip\.getAttribute\('data-wr-scope-chip'\)/);
    expect(handlerMatch[1]).toMatch(/load\(\)/);
  });
});

describe("(3) the preference survives into a drill-down — the 1c identity, one axis further", () => {
  it("carries the SAME scope preference the loaded report used into an items request", () => {
    const screen = boot();
    screen.setLastQuery({
      from: "2026-06-01", to: "2026-07-01", groupBy: "None", scopePreference: "own",
      legalEntityId: "", organizationUnitId: "", taskTypeCode: "", assigneeUserId: "", priority: ""
    });
    screen.setLastReport(busy({ scopeApplied: "scoped" }));

    screen.openItems("Unattended");

    expect(parseQuery(fetchCalls[0]).scope).toBe("own");
  });

  it("sends no scope parameter for a drill-down opened before any chip was ever clicked", () => {
    const screen = boot();
    screen.setLastQuery({
      from: "2026-06-01", to: "2026-07-01", groupBy: "None",
      legalEntityId: "", organizationUnitId: "", taskTypeCode: "", assigneeUserId: "", priority: ""
    });
    screen.setLastReport(busy());

    screen.openItems("Unattended");

    expect(fetchCalls[0]).not.toContain("scope=");
  });
});

describe("(4) no second scope resolver, no new number, no ratio", () => {
  it("names IWorkReportScopeSource as the one place scope is asked about — no client-side permission check", () => {
    expect(SCRIPT, "the screen is deciding scope from a permission of its own").not.toMatch(/Perms|hasPermission|work-report\.read/);
    expect(SCRIPT).toContain("report.scopeApplied === 'tenant'");
  });

  it("computes no ratio, percentage or score — unaffected by this slice", () => {
    const arithmetic = SCRIPT.split("\n").filter((l) => !l.trim().startsWith("*") && !l.trim().startsWith("//")).join("\n");
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

  it("never writes the words 'only me' or 'yalnızca ben' anywhere the chip's own label could pick them up", () => {
    // The chip reuses ScopeScoped/ScopeScopedHint verbatim — this guards against a future edit narrowing the
    // WORDING to "only mine" the way the brief explicitly forbade, since "your scope" includes a manager's team.
    expect(VIEW.toLowerCase()).not.toMatch(/only me\b|just me\b|yaln[ıi]zca ben/);
  });
});
