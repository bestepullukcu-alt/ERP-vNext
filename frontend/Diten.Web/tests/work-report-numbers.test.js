const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DILIM 1b ON THE SCREEN — the median, the cancellation span, ageing, and the direction against the previous
 * period.
 *
 * ⚠ WHAT THIS SLICE IS ABOUT IS NOT NEW PIXELS. Four of the five things below existed as numbers the server had
 * always been able to compute and the screen had no way to say. The one that is genuinely new is a REPAIR: the
 * cycle-time card used to average abandoned work in with finished work, so "how long our work takes" was partly
 * "how long before we gave up". The tests that matter here are the ones that would go red if that folding came
 * back.
 *
 * ⚠ AND NO LAYOUT CHANGED. Every number below goes into a card that already existed. Layout is Dilim 1d, and a
 * test in this file pins the card count so a redesign cannot arrive early by accident.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "WorkReport", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const L10N = fs.readFileSync(web("Views", "Tasks", "_WorkReportL10n.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

/** The keys THIS slice added — every one of them has to exist, translated, in all seven files. */
/*
 * ⚠ THE KEYS 1b OWNS, RENAMED WITH THE SKELETON. `CycleTimeMedian`, `CancelTime` and `AgingBuckets` were
 * whole sentences with `{0}` slots; the four cards now share a two-column facts table, so each of those
 * sentences became a LABEL beside its own value. The rule this list enforces is unchanged — every string 1b
 * put on screen exists in all seven languages and is actually translated — only the strings changed shape.
 */
const NEW_KEYS = ["LabelMedian", "LabelUntilCancelled", "LabelCancelled",
  "AgingUpTo7Label", "AgingFrom8To30Label", "AgingOlderThan30Label",
  "TrendSame", "TrendUp", "TrendDown"];

const LABELS = {
  summaryTitle: "Period summary", summaryPeriod: "Period", summaryDays: "{0} days",
  summaryScopeNote: "{0} · {1}",

  unitDays: "days", unitTasks: "tasks", unitOpenWork: "open",
  reworkQualifier: "work sent back to its owner", unattendedQualifier: "nobody has taken these on",
  labelMedian: "Median", labelUntilCancelled: "Until cancelled", labelCancelled: "Cancelled",
  labelTotalReturns: "Total returns", labelEstimated: "Estimated",
  agingUpTo7Label: "0-7 days", agingFrom8To30Label: "8-30 days", agingOlderThan30Label: "30+ days",
  effortSpentUnit: "h spent", hours: "{0} h",

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
  sharePercent: "{0}%",
  outcomesEmpty: "No closure was recorded with an outcome.",
  groupsTitle: "By {0}",
  groupUnnamed: "Not set",
  groupByOrganizationUnit: "Organisation unit",
  cycleTimeDays: "{0} days",
  cycleTimeOver: "over {0} closed",
  effortOver: "over {0} tasks",
  notMeasured: "Not measured",
  groupByLegalEntity: "Company",
  groupUnassigned: "Company unknown",
  groupOther: "All other groups",
  groupsTruncated: "Showing the busiest {0}; {1} more are folded in.",
  filterAny: "Any",
  priorityHigh: "High",
  priorityMedium: "Medium",
  priorityLow: "Low",
  trendSame: "same as the previous period",
  trendUp: "+{0} against the previous period (was {1})",
  trendDown: "-{0} against the previous period (was {1})"
};

const OUTCOME_LABELS = {
  COMPLETED_AS_REQUESTED: "Completed as requested",
  CANCELLED_SUPERSEDED: "Superseded by other work"
};

/** The regions the .cshtml renders, including the four hooks this slice added to the EXISTING cards. */
const MARKUP = `
  <form id="workReportFilter">
    <input type="date" id="wrFrom" value="2026-06-01" />
    <input type="date" id="wrTo" value="2026-07-01" />
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
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">${JSON.stringify(OUTCOME_LABELS)}</script>
`;

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

/** The response shape 1b publishes — `count`, `cancellationTime` and `aging` are this slice's additions. */

/**
 * The facts table of one card, read back as [label, value] pairs.
 *
 * ⚠ THE PAIRS ARE THE POINT. These used to be prose lines, and a test could only assert the whole sentence;
 * as a two-column list each fact can be named and checked on its own, so a row that quietly disappeared while
 * the sentence still read fine cannot pass.
 */
const facts = (selector) => {
  const dl = document.querySelector(selector);
  if (!dl) { return null; }
  const out = [];
  [...dl.children].forEach((el) => {
    if (el.tagName === "DT") { out.push([el.textContent, ""]); }
    else if (out.length) { out[out.length - 1][1] = el.textContent; }
  });
  return out;
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

/*
 * THE FIXTURE IS THE DEFECT, WRITTEN DOWN.
 *
 * Seven tasks finished, averaging 11.33 days; two were abandoned after 8.75 days. Before this slice both went
 * into one figure and the card said 3.99 — a number that was neither of them and flattered the team by mixing
 * in work they had stopped doing. `count` is 7, not 9: the denominator now matches the numerator.
 */
const busy = (over) => report(Object.assign({
  totals: bucket({
    flow: { opened: 12, closed: 9, completed: 7, cancelled: 2, unattended: 4 },
    cycleTime: { averageDays: 11.33, medianDays: 10, count: 7 },
    cancellationTime: { averageDays: 8.75, medianDays: 8, count: 2 },
    aging: { upTo7Days: 2, from8To30Days: 1, olderThan30Days: 5 },
    timeliness: { onTime: 5, late: 3, withoutDueDate: 1 },
    effort: { estimatedHours: 40, spentHours: 60, taskCount: 6 },
    outcomes: [{ code: "COMPLETED_AS_REQUESTED", count: 6 }, { code: "CANCELLED_SUPERSEDED", count: 2 }],
    rework: { tasksReturned: 3, totalReturns: 5 }
  })
}, over || {}));

/** The previous period, as the server returns it: totals only, no groups, no comparison of its own. */
const previously = (over) => ({
  from: "2026-05-02T00:00:00+00:00",
  to: "2026-06-01T00:00:00+00:00",
  totals: bucket(Object.assign({
    flow: { opened: 10, closed: 6, completed: 6, cancelled: 0, unattended: 0 },
    cycleTime: { averageDays: 14.33, medianDays: 13, count: 6 },
    timeliness: { onTime: 4, late: 2, withoutDueDate: 0 },
    rework: { tasksReturned: 2, totalReturns: 2 }
  }, over || {}))
});

const text = (selector) => document.querySelector(selector).textContent;
const hidden = (selector) => document.querySelector(selector).hidden;
const cls = (selector) => document.querySelector(selector).className;

describe("(1) the median stands BESIDE the average, never instead of it", () => {
  it("prints both, from the two figures the server measured", () => {
    /*
     * ⚠ WHY BOTH. An average alone is dragged by one task that sat for a year; a median alone hides that the
     * task existed. The GAP between them is the finding — 11.33 against 10 here says there is a tail — and a
     * reader can only see a gap if both numbers are on the card.
     */
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-cycle-value]")).toBe("11.33");
    expect(text("[data-wr-cycle-unit]"), "the unit left the answer line").toBe("days");
    expect(facts("[data-wr-cycle-facts]")).toContainEqual(["Median", "10 days"]);
  });

  it("computes neither of them in the browser", () => {
    /*
     * A median needs the whole ordered set. The browser is sent totals, so a median computed here would be a
     * median of nothing — and the moment two places computed it, one of them would be wrong.
     */
    const code = SCRIPT.split("\n")
      .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
      .join("\n");

    expect(code, "the screen is computing a median itself").not.toMatch(/\.sort\(|median\s*=\s*[^t]|Math\.floor\(.*length/);
    expect(code).toContain("cycle.medianDays");
  });

  it("says NOT MEASURED for both when nothing closed, rather than zero", () => {
    // A zero median reads as "half our work closed instantly", which is worse than the average's version of
    // the same lie because it sounds more precise.
    const screen = boot();
    screen.render(busy({
      totals: bucket({
        flow: { opened: 4, closed: 0, completed: 0, cancelled: 0, unattended: 0 },
        cycleTime: { averageDays: null, medianDays: null, count: 0 }
      })
    }));

    expect(text("[data-wr-cycle-value]")).toBe(LABELS.notMeasured);
    // ⚠ AND NO UNIT BESIDE IT. "Not measured days" is not a sentence anybody means.
    expect(text("[data-wr-cycle-unit]"), "an unmeasured average kept its unit").toBe("");
    expect(facts("[data-wr-cycle-facts]")).toContainEqual(["Median", LABELS.notMeasured]);
    expect(document.body.textContent, "an absent duration was drawn as a zero").not.toMatch(/\b0 days\b/);
  });
});

describe("(2) K-1 — abandoned work is read SEPARATELY from finished work", () => {
  it("keeps the cancellation span on its own line and out of the cycle time", () => {
    /*
     * ⚠ THE DEFECT THIS SLICE REPAIRS, PINNED AT THE SCREEN. The two spans answer different questions —
     * "how long does our work take" and "how long before we gave up" — and averaged together they answered
     * neither. The old, wrong figure for this fixture was 3.99 days; if the folding ever comes back, that
     * number reappears on the card and this test goes red.
     */
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-cycle-value]")).toBe("11.33");
    const rows = facts("[data-wr-cycle-facts]");
    expect(rows, "the cancellation span was never shown").toContainEqual(["Until cancelled", "8.75 days"]);
    expect(rows).toContainEqual(["Cancelled", "2"]);

    expect(document.body.textContent, "the two spans were averaged back together").not.toContain("3.99");
  });

  it("stays silent when nothing was cancelled, instead of claiming a zero-day abandonment", () => {
    /*
     * PRESENCE FIRST — the line is proved to render above, so its absence here is a decision rather than a
     * hook that never worked. "We abandoned work after 0 days" is a sentence nobody means.
     */
    const screen = boot();
    screen.render(busy({
      totals: bucket({
        flow: { opened: 8, closed: 6, completed: 6, cancelled: 0, unattended: 0 },
        cycleTime: { averageDays: 9, medianDays: 9, count: 6 },
        cancellationTime: { averageDays: null, medianDays: null, count: 0 }
      })
    }));

    // The ROW is gone, not zeroed — the median row beside it proves the table still rendered.
    const rows = facts("[data-wr-cycle-facts]");
    expect(rows.map((r) => r[0]), "a cancellation row appeared with nothing cancelled")
      .not.toContain("Until cancelled");
    expect(rows.map((r) => r[0]), "the facts table stopped rendering entirely").toContain("Median");
  });
});

describe("(3) K-2 — the denominator is the one the average was actually taken over", () => {
  it("counts the CLOSED-AND-MEASURED tasks, not everything that closed", () => {
    /*
     * ⚠ THE QUIETEST OF THE TWO DEFECTS. Nine tasks closed; seven of them completed and contributed a span.
     * Printing "over 9" beside an average of seven numbers is a footnote that misstates the sample — the kind
     * of error that survives review precisely because both numbers are individually true.
     */
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-cycle-over]")).toBe("over 7 closed");
    expect(text("[data-wr-cycle-over]"), "the denominator is still every closure").not.toContain("9");
  });

  it("reads the denominator from the contract's own field rather than adding up the flow", () => {
    // `completed + cancelled` happens to equal the right number in some periods and not in others — a span is
    // dropped whenever a task's closure predates its creation in the data. Only the server knows the real count.
    const code = SCRIPT.split("\n").filter((l) => !l.trim().startsWith("*") && !l.trim().startsWith("//")).join("\n");
    expect(code).toContain("cycle.count");
    expect(code, "the screen recomputed the denominator from the flow")
      .not.toMatch(/completed\s*\+\s*.*cancelled/);
  });
});

describe("(4) ageing — measured at the PERIOD'S END, which is what makes it evidence", () => {
  it("shows the three buckets, and they add up to the open work", () => {
    const screen = boot();
    screen.render(busy());

    expect(hidden("[data-wr-aging]")).toBe(false);
    /*
     * ⚠ THIS ASSERTED ONE LOCALIZED SENTENCE UNTIL THE CARDS GOT ONE SKELETON. The three bands are now rows of
     * the same facts table every other card uses, so each band is named and checked on its own — a band that
     * silently stopped rendering used to leave a sentence that still read as a sentence.
     */
    expect(facts("[data-wr-aging]")).toEqual([
      ["0-7 days", "2"], ["8-30 days", "1"], ["30+ days", "5"]
    ]);
  });

  it("does not compute an age in the browser from a date and a clock", () => {
    /*
     * ⚠ THE PROPERTY THAT MATTERS. A report gets reopened in a review months later beside a decision somebody
     * already took, and it has to say the same thing. An age computed in the browser drifts every task across
     * the 7- and 30-day boundaries as time passes — the page still renders, the numbers just stop matching the
     * copy that was printed.
     */
    const code = SCRIPT.split("\n").filter((l) => !l.trim().startsWith("*") && !l.trim().startsWith("//")).join("\n");

    expect(code, "the screen is reading the clock to age work").not.toMatch(/Date\.now|new Date\(\)/);
    expect(code).toContain("aging.upTo7Days");
  });

  it("stays hidden when nothing is open, rather than drawing three zeroes", () => {
    const screen = boot();
    screen.render(busy({
      totals: bucket({
        flow: { opened: 3, closed: 3, completed: 3, cancelled: 0, unattended: 0 },
        cycleTime: { averageDays: 2, medianDays: 2, count: 3 },
        aging: { upTo7Days: 0, from8To30Days: 0, olderThan30Days: 0 }
      })
    }));

    expect(hidden("[data-wr-aging]")).toBe(true);
  });
});

describe("(5) the comparison against the previous period", () => {
  it("ASKS for it — and lets the server decide which days 'previous' means", () => {
    /*
     * ⚠ THE DEFINITION LIVES IN ONE PLACE. 1–30 September compares against 2–31 August; a browser that worked
     * that out for itself would drift by a day the first time somebody reasoned about month lengths, and then
     * two figures on this page would disagree with no way to tell which was right.
     */
    expect(SCRIPT).toContain("comparePrevious=true");

    const code = SCRIPT.split("\n").filter((l) => !l.trim().startsWith("*") && !l.trim().startsWith("//")).join("\n");
    expect(code, "the screen is deriving the previous period itself")
      .not.toMatch(/setMonth|setDate|getTime\(\)\s*-|previousFrom/);
  });

  it("draws a direction from the two real numbers, in the right sense for each", () => {
    /*
     * ⚠ "BETTER" IS NOT "BIGGER". Cycle time fell from 14.33 to 11.33 — an improvement, shown as good. More
     * work closed (6 → 9) is also an improvement. One helper with a per-caller direction, rather than a colour
     * chosen by the sign of a subtraction, is what keeps those two from contradicting each other.
     */
    const screen = boot();
    screen.render(busy({ previous: previously() }));

    expect(hidden("[data-wr-cycle-trend]")).toBe(false);
    expect(text("[data-wr-cycle-trend]")).toBe("-3 against the previous period (was 14.33)");
    expect(cls("[data-wr-cycle-trend]"), "a shorter cycle time was drawn as a regression").toContain("text-success");

    expect(text("[data-wr-flow-trend]")).toBe("+3 against the previous period (was 6)");
    expect(cls("[data-wr-flow-trend]"), "closing more work was drawn as a regression").toContain("text-success");

    // Late work rose 2 → 3. Same arithmetic, opposite verdict.
    expect(text("[data-wr-late-trend]")).toBe("+1 against the previous period (was 2)");
    expect(cls("[data-wr-late-trend]"), "more late work was drawn as an improvement").toContain("text-danger");
  });

  it("says 'unchanged' in words rather than showing a zero-length arrow", () => {
    const screen = boot();
    screen.render(busy({ previous: previously({ cycleTime: { averageDays: 11.33, medianDays: 10, count: 5 } }) }));

    expect(text("[data-wr-cycle-trend]")).toBe(LABELS.trendSame);
    expect(cls("[data-wr-cycle-trend]")).toContain("text-muted");
  });

  it("draws NOTHING when no comparison was returned", () => {
    /*
     * ⚠ PRESENCE FIRST — the arrow is proved to render above. A missing comparison is not a previous period of
     * zeroes: treating it as one would show a triumphant green arrow on every report whose comparison failed
     * to load.
     */
    const screen = boot();
    screen.render(busy({ previous: null }));

    expect(hidden("[data-wr-cycle-trend]")).toBe(true);
    expect(hidden("[data-wr-flow-trend]")).toBe(true);

    // Hidden AND empty: a `hidden` attribute somebody later removes for a layout reason must not uncover a
    // sentence about a period that was never measured.
    ["[data-wr-cycle-trend]", "[data-wr-flow-trend]", "[data-wr-late-trend]", "[data-wr-rework-trend]"]
      .forEach((sel) => expect(text(sel), `${sel} still carries a direction`).toBe(""));
  });

  it("clears a stale arrow when the next period is empty", () => {
    // Otherwise a reader who narrows the dates keeps looking at last query's direction under a "no data" line.
    const screen = boot();
    screen.render(busy({ previous: previously() }));
    expect(hidden("[data-wr-cycle-trend]")).toBe(false);

    screen.render(report());
    expect(hidden("[data-wr-cycle-trend]"), "a stale direction survived an empty period").toBe(true);
    expect(hidden("[data-wr-aging]")).toBe(true);
    expect(facts("[data-wr-cycle-facts]").map((r) => r[0]),
      "a stale cancellation row survived an empty period").not.toContain("Until cancelled");
  });
});

describe("(6) this slice added numbers, not layout", () => {
  it("added no card — the new figures went into the ones that were already there", () => {
    /*
     * ⚠ LAYOUT IS DILIM 1d. Four tiles and four chart slots, unchanged. A redesign that arrived early inside a
     * "numbers" slice would be a change nobody reviewed as a design.
     */
    const screen = boot();
    screen.render(busy({ previous: previously() }));

    expect(drawn).toHaveLength(3);
    expect((VIEW.match(/data-wr-chart-/g) || []).length).toBe(4);
    expect(VIEW, "a KPI card was restyled inside a numbers slice").not.toMatch(/style="/);
  });

  it("keeps every new line inside an existing card rather than after it", () => {
    // The hooks sit between the card's own value and its closing tag, so nothing floats loose in the grid.
    const card = VIEW.slice(VIEW.indexOf("data-wr-cycle-value"), VIEW.indexOf("data-wr-rework-tasks"));
    ["data-wr-cycle-unit", "data-wr-cycle-over", "data-wr-cycle-facts", "data-wr-cycle-trend"].forEach((hook) => {
      expect(card.includes(hook), `${hook} escaped the cycle-time card`).toBe(true);
    });
  });

  it("still computes no ratio, percentage or score", () => {
    // Pack §8 travels with every slice. A "median vs average" percentage would be the same forbidden thing
    // wearing statistics.
    const code = SCRIPT.split("\n").filter((l) => !l.trim().startsWith("*") && !l.trim().startsWith("//")).join("\n");
    expect(code).not.toMatch(/spentHours\s*\/|\/\s*estimatedHours|medianDays\s*\//);
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
    expect(code).not.toMatch(/efficiency|multiplier|productivityScore/i);
  });
});

describe("(7) seven languages, and no raw key on screen", () => {
  it("defines all six new labels in all seven files", () => {
    LANGS.forEach((lang) => {
      NEW_KEYS.forEach((key) => {
        expect(resx(lang).includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
      });
    });
  });

  it("actually translates them rather than shipping the English seven times", () => {
    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1].trim() : null;
    };

    NEW_KEYS.forEach((key) => {
      const english = value("en", key);
      expect(english, `${key} has no English text`).toBeTruthy();
      LANGS.filter((l) => l !== "en").forEach((lang) => {
        expect(value(lang, key), `${key}/${lang} is still the English text`).not.toBe(english);
      });
    });
  });

  it("carries every new label across the bridge, so none of them reaches the script as a key", () => {
    NEW_KEYS.forEach((key) => {
      expect(L10N.includes(`Localizer["${key}"]`), `${key} never crosses the l10n bridge`).toBe(true);
    });

    const screen = boot();
    screen.render(busy({ previous: previously() }));
    NEW_KEYS.forEach((key) => {
      expect(document.body.textContent, `the raw key ${key} is on screen`).not.toContain(key);
    });
  });

  it("keeps the placeholders the translations need", () => {
    // A translator who drops {0} silently deletes the number the sentence is about.
    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1] : "";
    };

    LANGS.forEach((lang) => {
      /*
       * ⚠ THE SENTENCES THAT CARRIED {0}..{2} ARE GONE, AND THAT IS WHY THIS LIST SHRANK. A facts-table LABEL
       * takes no placeholder — its value is a separate cell — so demanding one would fail for every language.
       * What still carries slots is checked here, and the labels are checked NOT to carry one: a `{0}` left in
       * a label would render the brace to the reader.
       */
      ["LabelMedian", "LabelUntilCancelled", "LabelCancelled",
       "AgingUpTo7Label", "AgingFrom8To30Label", "AgingOlderThan30Label"].forEach((key) => {
        expect(value(lang, key), `${key}/${lang} is empty`).toBeTruthy();
        expect(value(lang, key), `${key}/${lang} still carries a placeholder`).not.toContain("{0}");
      });
      expect(value(lang, "CycleTimeDays"), `CycleTimeDays/${lang}`).toContain("{0}");
      expect(value(lang, "CycleTimeOver"), `CycleTimeOver/${lang}`).toContain("{0}");
      ["{0}", "{1}"].forEach((token) => {
        expect(value(lang, "TrendUp").includes(token), `TrendUp/${lang} lost ${token}`).toBe(true);
        expect(value(lang, "TrendDown").includes(token), `TrendDown/${lang} lost ${token}`).toBe(true);
      });
    });
  });
});
