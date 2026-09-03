const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * THE WORK REPORT SCREEN (MOD-0024 Faz 5b) — what it draws, and what it refuses to draw.
 *
 * ⚠ EVERY "IT IS ABSENT" ASSERTION BELOW PROVES SOMETHING WAS PRESENT FIRST. This session caught three tests
 * that passed because nothing had rendered at all, and the emptiest kind of green is the one that reads as
 * proof. So each absence test either renders a populated report in the same file, or asserts the presence of
 * the surrounding region before asking what is missing from it.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "WorkReport", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const L10N = fs.readFileSync(web("Views", "Tasks", "_WorkReportL10n.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

/** The l10n island the view renders, with the key casing MVC's serializer produces. */
const LABELS = {
  cycleTimeMedian: "median {0}",
  cancelTime: "Until cancelled: average {0} · median {1} ({2} cancelled)",
  agingBuckets: "Age of open work — 0-7 days: {0} · 8-30 days: {1} · 30+ days: {2}",
  trendSame: "same as the previous period",
  trendUp: "+{0} against the previous period (was {1})",
  trendDown: "-{0} against the previous period (was {1})",
  scopeScoped: "Your scope",
  scopeTenant: "Whole tenant",
  scopeScopedHint: "Counts only the work you are entitled to see.",
  scopeTenantHint: "Counts every task in the tenant.",
  noData: "No work was opened or closed in this period.",
  noDataScoped: "No work you can see was opened or closed in this period.",
  loading: "Loading…",
  loadFailed: "The report could not be loaded.",
  periodInvalid: "The end of the period must come after its start.",
  flowTitle: "Opened and closed",
  opened: "Opened",
  closed: "Closed",
  completed: "Completed",
  cancelled: "Cancelled",
  onTime: "On time",
  late: "Late",
  withoutDueDate: "No deadline set",
  timelinessTitle: "Against the deadline",
  outcomesEmpty: "No closure was recorded with an outcome.",
  groupsTitle: "By {0}",
  groupUnnamed: "Not set",
  groupByOrganizationUnit: "Organisation unit",
  cycleTimeDays: "{0} days",
  cycleTimeOver: "over {0} closed",
  reworkTasks: "{0} tasks",
  reworkReturns: "{0} returns in total",
  effortHours: "{0} h estimated · {1} h spent",
  effortOver: "over {0} tasks",
  notMeasured: "Not measured",
  groupByLegalEntity: "Company",
  groupUnassigned: "Company unknown",
  groupOther: "All other groups",
  groupsTruncated: "Showing the busiest {0}; {1} more are folded in.",
  filterAny: "Any",
  priorityHigh: "High",
  priorityMedium: "Medium",
  priorityLow: "Low"
};

/** The five SYSTEM outcome codes, translated — read from the WorkCenterNext resx by the real view. */
const OUTCOME_LABELS = {
  COMPLETED_AS_REQUESTED: "Completed as requested",
  COMPLETED_PARTIALLY: "Partially completed",
  CANCELLED_SUPERSEDED: "Superseded by other work"
};

/** The regions the .cshtml renders, reduced to what the script touches. */
const MARKUP = `
  <form id="workReportFilter">
    <input type="date" id="wrFrom" value="2026-06-01" />
    <input type="date" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option><option value="OrganizationUnit">Unit</option></select>
    <button type="submit">Apply</button>
  </form>
  <div data-wr-scope hidden><span data-wr-scope-badge></span><span data-wr-scope-hint></span></div>
  <p data-wr-status></p>
  <div data-wr-tiles hidden>
    <p data-wr-cycle-value></p><p data-wr-cycle-median></p><p data-wr-cycle-over></p>
    <p data-wr-cycle-trend hidden></p><p data-wr-cancel-value hidden></p>
    <p data-wr-rework-tasks></p><p data-wr-rework-returns></p><p data-wr-rework-trend hidden></p>
    <p data-wr-unattended-value></p><p data-wr-aging hidden></p>
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
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">${JSON.stringify(OUTCOME_LABELS)}</script>
`;

/** Records what ApexCharts was asked to draw — the real call, captured rather than mocked away. */
let drawn = [];

const boot = () => {
  drawn = [];
  document.body.innerHTML = MARKUP;
  delete global.WorkReportScreen;

  global.ApexCharts = function (host, options) {
    this.host = host;
    this.options = options;
    this.render = () => { drawn.push({ host, options }); };
    this.destroy = () => { drawn = drawn.filter((d) => d.host !== host); };
  };
  window.ApexCharts = global.ApexCharts;

  global.fetch = global.fetch || (() => Promise.resolve({ ok: false, json: () => Promise.resolve(null) }));

  loadScript("wwwroot/assets/js/Tasks/WorkReport/index.js");
  return window.WorkReportScreen;
};

const bucket = (over) => Object.assign({
  key: null,
  label: null,
  flow: { opened: 0, closed: 0, completed: 0, cancelled: 0, unattended: 0 },
  cycleTime: { averageDays: null, medianDays: null, count: 0 },
  cancellationTime: { averageDays: null, medianDays: null, count: 0 },
  aging: { upTo7Days: 0, from8To30Days: 0, olderThan30Days: 0 },
  timeliness: { onTime: 0, late: 0, withoutDueDate: 0 },
  effort: { estimatedHours: 0, spentHours: 0, taskCount: 0 },
  outcomes: [],
  rework: { tasksReturned: 0, totalReturns: 0 }
}, over || {});

const report = (over) => Object.assign({
  from: "2026-06-01T00:00:00+00:00",
  to: "2026-07-01T00:00:00+00:00",
  scopeApplied: "scoped",
  groupBy: "None",
  totals: bucket(),
  groups: [],
  groupsTruncated: 0,
  previous: null
}, over || {});

/** A period with real work in it — the fixture every "presence first" assertion leans on. */
const busy = (over) => report(Object.assign({
  totals: bucket({
    flow: { opened: 12, closed: 9, completed: 7, cancelled: 2, unattended: 4 },
    cycleTime: { averageDays: 11.33, medianDays: 10, count: 7 },
    cancellationTime: { averageDays: 8.75, medianDays: 8, count: 2 },
    timeliness: { onTime: 5, late: 3, withoutDueDate: 1 },
    effort: { estimatedHours: 40, spentHours: 60, taskCount: 6 },
    outcomes: [{ code: "COMPLETED_AS_REQUESTED", count: 6 }, { code: "CANCELLED_SUPERSEDED", count: 2 }],
    rework: { tasksReturned: 3, totalReturns: 5 }
  })
}, over || {}));

const text = (selector) => document.querySelector(selector).textContent;
const hidden = (selector) => document.querySelector(selector).hidden;
const chartFor = (selector) => drawn.find((d) => d.host === document.querySelector(selector));

describe("(a) the screen says WHAT the numbers cover", () => {
  it("names the caller's own scope, with a sentence explaining it", () => {
    /*
     * ⚠ THE FIELD 5a PUBLISHES THIS FOR. "There is no work" and "there is no work I may SEE" draw the identical
     * empty chart, and a reader takes the second for the first every time. A screen that swallowed
     * `scopeApplied` would kill the reason the field exists.
     */
    const screen = boot();
    screen.render(busy({ scopeApplied: "scoped" }));

    expect(hidden("[data-wr-scope]"), "the scope row never appeared").toBe(false);
    expect(text("[data-wr-scope-badge]")).toBe(LABELS.scopeScoped);
    expect(text("[data-wr-scope-hint]")).toBe(LABELS.scopeScopedHint);
  });

  it("says so differently when the report covers the whole tenant", () => {
    // Non-vacuity for the test above: the badge has to CHANGE, or it is a constant that happens to read right.
    const screen = boot();
    screen.render(busy({ scopeApplied: "tenant" }));

    expect(text("[data-wr-scope-badge]")).toBe(LABELS.scopeTenant);
    expect(text("[data-wr-scope-hint]")).toBe(LABELS.scopeTenantHint);
    expect(text("[data-wr-scope-badge]")).not.toBe(LABELS.scopeScoped);
  });

  it("reads the scope from the RESPONSE, never from a permission the browser guessed", () => {
    // The server decides scope. A screen that inferred it would eventually disagree with the numbers it draws.
    expect(SCRIPT).toContain("report.scopeApplied === 'tenant'");
    expect(SCRIPT, "the screen is guessing scope from a permission").not.toMatch(/Perms|hasPermission|work-report\.read/);
  });
});

describe("(b) an empty period says so instead of drawing zeroes", () => {
  it("shows a sentence and draws no chart at all", () => {
    /*
     * ⚠ PRESENCE FIRST. A busy report is rendered into the same DOM immediately below, so "no charts" here
     * cannot be passing because the harness never draws any.
     */
    const screen = boot();
    screen.render(report());

    expect(text("[data-wr-status]")).toBe(LABELS.noDataScoped);
    expect(drawn, "an empty period still drew charts").toHaveLength(0);
    expect(hidden("[data-wr-charts]")).toBe(true);
    expect(hidden("[data-wr-tiles]")).toBe(true);

    // …and the same screen DOES draw when there is work, which is what makes the assertion above a measurement.
    screen.render(busy());
    expect(drawn.length, "the harness cannot draw at all — the test above proved nothing").toBeGreaterThan(0);
  });

  it("tells the two empty periods apart", () => {
    // "No work" and "no work I can see" are different facts; only the reader knows which one matters to them.
    const screen = boot();
    screen.render(report({ scopeApplied: "tenant" }));
    expect(text("[data-wr-status]")).toBe(LABELS.noData);

    screen.render(report({ scopeApplied: "scoped" }));
    expect(text("[data-wr-status]")).toBe(LABELS.noDataScoped);
  });

  it("does not call a period busy just because a backlog is sitting in it", () => {
    /*
     * `unattended` counts open work as of NOW, not within the period. Counting it as work would make every
     * empty period look busy in a tenant with a standing backlog — and the reader would hunt for four tasks the
     * charts cannot show them.
     */
    const screen = boot();
    screen.render(report({ totals: bucket({ flow: { opened: 0, closed: 0, completed: 0, cancelled: 0, unattended: 9 } }) }));

    expect(text("[data-wr-status]")).toBe(LABELS.noDataScoped);
    expect(drawn).toHaveLength(0);
  });

  it("clears the previous period's charts when the next one is empty", () => {
    // Otherwise a reader who narrows the dates keeps looking at the old answer under a "no data" line.
    const screen = boot();
    screen.render(busy());
    expect(drawn.length).toBeGreaterThan(0);

    screen.render(report());
    expect(drawn, "stale charts survived an empty period").toHaveLength(0);
  });
});

describe("(c) outcome labels arrive translated, never as raw codes", () => {
  it("draws the bar with the words, not with COMPLETED_AS_REQUESTED", () => {
    /*
     * ⚠ SHAPE UPDATED IN DILIM 1d — the outcome axis draws as a sorted horizontal bar rather than a donut (a
     * long-tailed axis does not survive a wheel), so the assertions below moved from `.labels`/`.series` (the
     * donut's shape) to `.xaxis.categories`/`.series[0].data` (the bar's). The PROTECTIVE INTENT this test
     * exists for — translated words on the axis, never a raw code — is unchanged and re-asserted in the new
     * shape.
     */
    const screen = boot();
    screen.render(busy());

    const chart = chartFor("[data-wr-chart-outcomes]");
    expect(chart, "the outcome chart was never drawn").toBeTruthy();
    expect(chart.options.chart.type).toBe("bar");
    expect(chart.options.xaxis.categories).toEqual(["Completed as requested", "Superseded by other work"]);
    expect(chart.options.xaxis.categories.join(" "), "a raw code reached the chart").not.toMatch(/COMPLETED_|CANCELLED_/);
    expect(chart.options.series[0].data).toEqual([6, 2]);
  });

  it("falls back to the code for a TENANT outcome, which has nothing to translate", () => {
    /*
     * A tenant's own outcome carries only the words its administrator typed, in one language. The fallback is
     * the honest answer there — and for a SYSTEM code missing from the map it is a visible gap rather than a
     * blank slice nobody can chase.
     */
    const screen = boot();
    expect(screen.outcomeLabel("COMPLETED_PARTIALLY")).toBe("Partially completed");
    expect(screen.outcomeLabel("ESCALATED_TO_QA")).toBe("ESCALATED_TO_QA");
  });

  it("reads those five translations from the WorkCenterNext resx rather than copying them", () => {
    /*
     * `WorkAggregation_ClosureOutcome_*` already exists there in seven languages and the l10n guard holds the
     * prefix as a domain. A second copy would let the Task Center and this report disagree about what an
     * outcome is called the day one of them is corrected.
     */
    expect(L10N).toContain("WorkCenterNextIndex");
    expect(L10N).toContain("WorkAggregation_ClosureOutcome_CompletedAsRequested");
    expect(resx("en"), "an outcome label was duplicated into this screen's resx")
      .not.toContain("WorkAggregation_ClosureOutcome_");
  });

  it("says so in words when nothing closed with an outcome", () => {
    // An empty chart is a blank card that says nothing.
    const screen = boot();
    screen.render(busy({ totals: bucket({ flow: { opened: 3, closed: 1, completed: 1, cancelled: 0, unattended: 0 }, outcomes: [] }) }));

    expect(hidden("[data-wr-outcomes-empty]")).toBe(false);
    expect(chartFor("[data-wr-chart-outcomes]")).toBeUndefined();
    // …while the OTHER charts still drew, so this is about the outcomes chart and not about a dead render path.
    expect(chartFor("[data-wr-chart-flow]")).toBeTruthy();
  });
});

describe("(d) no efficiency percentage is computed anywhere", () => {
  it("prints estimated and spent side by side and never divides them", () => {
    /*
     * ⚠ PACK §8, AT THIS LAYER. 5a keeps the ratio out of the contract
     * (`There_is_no_efficiency_percentage_anywhere_in_the_contract`); a screen that divided two published
     * numbers would put it back one layer further from anyone who would notice. Turning estimate-versus-actual
     * into a personal score makes people inflate estimates, which corrupts the only planning input there is.
     */
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-effort-value]")).toBe("40 h estimated · 60 h spent");
    expect(text("[data-wr-effort-over]")).toBe("over 6 tasks");
    /*
     * 60/40 = 1.5 — the number a ratio would produce, and it must appear nowhere. The hours moved from 52 to 60
     * in Dilim 1b: 52/40 renders as 1.3, and the cycle-time average this slice put on the card is 11.33, so the
     * old guard would have been tripped by a legitimate duration rather than by a ratio.
     */
    expect(document.body.textContent).not.toMatch(/1\.5|150\s*%|0\.66|0\.67/);
  });

  it("has no division, percentage or score in the source at all", () => {
    const arithmetic = SCRIPT
      .split("\n")
      .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
      .join("\n");

    expect(arithmetic, "a ratio crept into the screen").not.toMatch(/spentHours\s*\/|\/\s*estimatedHours/);
    expect(arithmetic).not.toMatch(/efficiency|multiplier|productivityScore/i);
  });

  it("carries the note that says why, so nobody adds it back as a convenience", () => {
    expect(VIEW).toContain("EffortHint");
    LANGS.forEach((lang) => {
      expect(resx(lang).includes('name="EffortHint"'), `EffortHint missing in ${lang}`).toBe(true);
    });
  });
});

describe("each visual answers ONE question", () => {
  it("draws exactly three charts with no breakdown, and a fourth with one", () => {
    /*
     * Flow, outcomes, timeliness — three comparisons. Cycle time, rework, unattended and effort are single
     * numbers and get TILES: a chart for one figure is decoration, and this screen has a rule against it.
     */
    const screen = boot();
    screen.render(busy());
    expect(drawn).toHaveLength(3);
    expect(hidden("[data-wr-groups-card]")).toBe(true);

    screen.render(busy({
      groupBy: "OrganizationUnit",
      groups: [
        bucket({ key: "unit-a", flow: { opened: 7, closed: 5, completed: 5, cancelled: 0, unattended: 0 } }),
        bucket({ key: "", flow: { opened: 5, closed: 4, completed: 2, cancelled: 2, unattended: 0 } })
      ]
    }));

    expect(drawn).toHaveLength(4);
    expect(hidden("[data-wr-groups-card]")).toBe(false);
    expect(text("[data-wr-groups-title]")).toBe("By Organisation unit");
  });

  it("gives the unnamed group a word rather than a blank axis label", () => {
    /*
     * The API keeps a "" key on purpose — a task with no type, or one nobody holds — so the groups still add up
     * to the totals. A nameless axis label reads as a rendering bug.
     */
    const screen = boot();
    screen.render(busy({
      groupBy: "OrganizationUnit",
      groups: [bucket({ key: "", flow: { opened: 2, closed: 1, completed: 1, cancelled: 0, unattended: 0 } })]
    }));

    expect(chartFor("[data-wr-chart-groups]").options.xaxis.categories).toEqual([LABELS.groupUnnamed]);
  });

  it("does not pretend the flow chart is a time series", () => {
    /*
     * ⚠ MEASURED LIMIT OF THE ENDPOINT, not a design preference: 5a returns ONE period's totals and does no
     * sub-period bucketing, so there is no series to plot. A line drawn from four totals would be a picture of
     * nothing. Bucketing belongs to the query if it is ever wanted.
     */
    const screen = boot();
    screen.render(busy());

    const flow = chartFor("[data-wr-chart-flow]");
    expect(flow.options.chart.type).toBe("bar");
    expect(flow.options.series[0].data).toEqual([12, 9, 7, 2]);
  });

  it("keeps undated work as its own bar instead of folding it into on-time", () => {
    // Work nobody set a date for was not early. Counting it as on-time makes punctuality flattering and useless.
    const screen = boot();
    screen.render(busy());

    const names = chartFor("[data-wr-chart-timeliness]").options.series.map((s) => s.name);
    expect(names).toEqual([LABELS.onTime, LABELS.late, LABELS.withoutDueDate]);
  });

  it("prints an absent cycle-time average as words, never as zero", () => {
    // A zero reads as "everything closed instantly" — the most flattering lie a report can tell.
    const screen = boot();
    screen.render(busy({
      totals: bucket({
        flow: { opened: 4, closed: 0, completed: 0, cancelled: 0, unattended: 0 },
        cycleTime: { averageDays: null, medianDays: null, count: 0 }
      })
    }));

    expect(text("[data-wr-cycle-value]")).toBe(LABELS.notMeasured);
    expect(text("[data-wr-cycle-value]")).not.toBe("0 days");
  });
});

describe("the screen obeys the house rules", () => {
  it("styles through classes only — no inline style anywhere (FG-003)", () => {
    [["script", SCRIPT], ["view", VIEW], ["l10n", L10N]].forEach(([name, source]) => {
      expect(source, `${name} uses a style attribute`).not.toMatch(/style="/);
      expect(source, `${name} writes element.style`).not.toMatch(/\.style\./);
    });
  });

  it("loads apexcharts in the PAGE, never in the shared tenant shell", () => {
    /*
     * ⚠ MEASURED 2026-09-04: `_LayoutTenantShell` does not load apexcharts (0 matches); `_Layout` does. Adding
     * it to the shell would put a charting library on every tenant page to serve one.
     */
    expect(VIEW).toContain("assets/vendor/libs/apex-charts/apexcharts.js");
    expect(VIEW).toContain("@section Scripts");

    const shell = fs.readFileSync(web("Views", "Shared", "_LayoutTenantShell.cshtml"), "utf8");
    expect(shell, "apexcharts was added to the shared tenant shell").not.toMatch(/apex/i);
  });

  it("destroys a chart before redrawing it", () => {
    /*
     * ApexCharts appends to the element it is given; without a destroy, pressing Apply twice stacks a second
     * chart under the first and the card grows forever.
     */
    expect(SCRIPT).toContain("charts[slot].destroy()");

    const screen = boot();
    screen.render(busy());
    screen.render(busy());
    expect(drawn, "a redraw stacked a second set of charts").toHaveLength(3);
  });

  it("defines every label in all seven languages, and translates them", () => {
    const keys = ["Title", "From", "To", "GroupBy", "Apply", "ScopeScoped", "ScopeTenant",
      "NoData", "NoDataScoped", "FlowTitle", "OutcomesTitle", "TimelinessTitle", "EffortHint"];

    LANGS.forEach((lang) => {
      keys.forEach((key) => {
        expect(resx(lang).includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
      });
    });

    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1].trim() : null;
    };

    ["Title", "ScopeScoped", "NoData"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();
      LANGS.filter((l) => l !== "en").forEach((lang) => {
        expect(value(lang, key), `${key}/${lang} is still the English text`).not.toBe(english);
      });
    });
  });
});

describe("Dilim 1a — filters, the company axis and readable labels", () => {
  it("sends only the filters that were actually chosen", () => {
    /*
     * An empty picker means "not asked", not "match nothing" — the server's contract makes every filter
     * nullable for exactly that reason. Sending `&priority=` would turn an untouched control into a constraint.
     */
    const screen = boot();
    document.querySelector("#wrPriority").innerHTML = '<option value="">Any</option><option value="High" selected>High</option>';

    const q = screen.query();
    expect(q.priority).toBe("High");
    expect(q.legalEntityId).toBe("");
    expect(q.assigneeUserId).toBe("");
  });

  it("builds the URL with the chosen filters and without the untouched ones", async () => {
    const screen = boot();
    let requested = "";
    global.fetch = (url) => {
      requested = url;
      return Promise.resolve({ ok: true, json: () => Promise.resolve(busy()) });
    };

    document.querySelector("#wrTaskType").innerHTML = '<option value="DEV" selected>Dev</option>';
    await screen.load();

    expect(requested).toContain("taskTypeCode=DEV");
    expect(requested, "an untouched filter was sent as an empty constraint").not.toContain("priority=");
    expect(requested, "an untouched filter was sent as an empty constraint").not.toContain("assigneeUserId=");
  });

  it("names the unassigned and the other bucket in words, not as reserved keys", () => {
    /*
     * `__unassigned__` and `__other__` cross the wire as reserved keys because the server has no sentence for
     * them that survives seven translations. On screen they MUST read as sentences — a raw `__other__` on an
     * axis is indistinguishable from a bug.
     */
    const screen = boot();
    expect(screen.groupLabel({ key: "__unassigned__" }, "LegalEntity")).toBe(LABELS.groupUnassigned);
    expect(screen.groupLabel({ key: "__other__" }, "LegalEntity")).toBe(LABELS.groupOther);
    expect(document.body.textContent).not.toContain("__unassigned__");
  });

  it("prefers the SERVER's label, and falls back to the identity rather than inventing one", () => {
    const screen = boot();
    expect(screen.groupLabel({ key: "abc", label: "Grand Medical" }, "LegalEntity")).toBe("Grand Medical");
    // No label, no lookup entry → the identity. Never a placeholder that matches nothing searchable.
    expect(screen.groupLabel({ key: "abc", label: null }, "LegalEntity")).toBe("abc");
  });

  it("resolves a PERSON from its own lookup, because Platform cannot name one", () => {
    /*
     * MEASURED 2026-09-04: Platform has no User entity and no auth client, so `label` is always null on the
     * assignee axis. The screen fills it from the user lookup this product already serves.
     */
    const screen = boot();
    screen.setPeople({ "u-1": "Ayşe Yılmaz" });

    expect(screen.groupLabel({ key: "u-1", label: null }, "Assignee")).toBe("Ayşe Yılmaz");
    expect(screen.groupLabel({ key: "u-2", label: null }, "Assignee")).toBe("u-2");
  });

  it("names a PRIORITY in the reader's language rather than echoing the enum", () => {
    // An enum is a code. A server-side English word would be a second, untranslated vocabulary.
    const screen = boot();
    expect(screen.groupLabel({ key: "High", label: null }, "Priority")).toBe(LABELS.priorityHigh);
  });

  it("draws the company breakdown with names on the axis", () => {
    const screen = boot();
    screen.render(busy({
      groupBy: "LegalEntity",
      groups: [
        bucket({ key: "co-a", label: "Grand Medical Poland", flow: { opened: 6, closed: 4, completed: 4, cancelled: 0, unattended: 0 } }),
        bucket({ key: "__unassigned__", flow: { opened: 2, closed: 1, completed: 1, cancelled: 0, unattended: 0 } })
      ]
    }));

    const axis = chartFor("[data-wr-chart-groups]").options.xaxis.categories;
    expect(axis).toEqual(["Grand Medical Poland", LABELS.groupUnassigned]);
    expect(axis.join(" "), "a raw GUID or reserved key reached the axis").not.toMatch(/__|co-a/);
  });

  it("says when the group list was capped, and stays quiet when it was not", () => {
    /*
     * ⚠ A SILENT CUT IS WORSE THAN NO CAP. A reader comparing units would conclude a unit has no work when it
     * simply did not place in the busiest fifty.
     */
    const screen = boot();
    const groups = [bucket({ key: "u1", flow: { opened: 3, closed: 1, completed: 1, cancelled: 0, unattended: 0 } })];

    screen.render(busy({ groupBy: "OrganizationUnit", groups, groupsTruncated: 0 }));
    expect(hidden("[data-wr-groups-truncated]")).toBe(true);

    screen.render(busy({ groupBy: "OrganizationUnit", groups, groupsTruncated: 7 }));
    expect(hidden("[data-wr-groups-truncated]"), "the cap was applied silently").toBe(false);
    expect(text("[data-wr-groups-truncated]")).toContain("7");
  });

  it("still computes no ratio anywhere, filters or not", () => {
    // Pack §8 travels with the screen: nothing this slice added may divide two published numbers.
    const arithmetic = SCRIPT.split("\n")
      .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
      .join("\n");
    expect(arithmetic).not.toMatch(/spentHours\s*\/|\/\s*estimatedHours/);
    expect(arithmetic).not.toMatch(/efficiency|multiplier|productivityScore/i);
  });
});
