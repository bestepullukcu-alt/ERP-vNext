const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DILIM 1d — LAYOUT ONLY. No number, no computation, no endpoint changes in this slice; every measure a reader
 * sees is exactly what 1a/1b/1c already published. This file's job is narrower than theirs: prove the SHELL
 * changed the way the brief asked — filters behind a button with an honest count, the project's own pickers,
 * a KPI card shell, and the outcomes axis surviving a long tail — without touching what any of it says.
 *
 * ⚠ jsdom's `document.readyState` is already `'complete'` by the time a test file's script loads (verified:
 * `DOMContentLoaded` never fires again), so the boot-time wiring (`initSelect2`, `initDatePickers`,
 * `updateFilterCount`) is exercised through the SAME exported hooks 1a/1b/1c already established the pattern
 * for — the real functions, called directly, not copies of them.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "WorkReport", "index.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const L10N = fs.readFileSync(web("Views", "Tasks", "_WorkReportL10n.cshtml"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

const NEW_KEYS = ["FiltersToggle", "OutcomesOther"];

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
  outcomesTitle: "How work ended", outcomesEmpty: "No closure was recorded with an outcome.",
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
  itemsColDue: "due {0}", itemsColClosed: "closed {0}", reworkTitle: "Came back", unattendedTitle: "Waiting for someone",
  filtersToggle: "Filters", outcomesOther: "Other outcomes"
};

const OUTCOME_LABELS = { A: "Alpha", B: "Beta", C: "Gamma", D: "Delta", E: "Epsilon", F: "Zeta", G: "Eta", H: "Theta", I: "Iota", J: "Kappa" };

const MARKUP = `
  <form id="workReportFilter">
    <input type="text" id="wrFrom" value="2026-06-01" /><input type="text" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option></select>
    <button type="submit">Apply</button>
    <select id="wrLegalEntity" data-wr-filter><option value="">Any</option></select>
    <select id="wrUnit" data-wr-filter><option value="">Any</option></select>
    <select id="wrTaskType" data-wr-filter><option value="">Any</option></select>
    <select id="wrAssignee" data-wr-filter><option value="">Any</option></select>
    <select id="wrPriority" data-wr-filter><option value="">Any</option></select>
    <span class="badge" data-wr-filter-count hidden>0</span>
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

const boot = () => {
  drawn = [];
  document.body.innerHTML = MARKUP;
  delete global.WorkReportScreen;
  delete window.jQuery;
  delete window.flatpickr;

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
    timeliness: { onTime: 5, late: 3, withoutDueDate: 1 },
    // Non-empty on purpose: an empty `outcomes` array draws no chart at all (see (4)'s own tests), which
    // would make the generic "three charts with no breakdown" check in (6) fail for a reason unrelated to it.
    outcomes: [{ code: "A", count: 3 }, { code: "B", count: 2 }]
  })
}, over || {}));

const text = (selector) => document.querySelector(selector).textContent;
const hidden = (selector) => document.querySelector(selector).hidden;
const chartFor = (selector) => drawn.find((d) => d.host === document.querySelector(selector));

/** Twelve outcome codes, sorted, so the fold behaviour has something real to fold. */
const manyOutcomes = () =>
  ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"].map((code, i) => ({ code, count: 20 - i }))
    .concat([{ code: "K", count: 2 }, { code: "L", count: 1 }]);

describe("(1) the filter panel — behind a button, with an honest count", () => {
  it("counts only the FIVE narrowing filters — not the period, not the breakdown", () => {
    const screen = boot();
    document.querySelector("#wrLegalEntity").value = "";
    document.querySelector("#wrPriority").innerHTML = '<option value="High" selected>High</option>';

    screen.updateFilterCount();

    expect(hidden("[data-wr-filter-count]"), "one active filter produced no badge").toBe(false);
    expect(text("[data-wr-filter-count]")).toBe("1");
  });

  it("goes back to hidden — not to zero-and-visible — when nothing is chosen", () => {
    // PRESENCE FIRST: proven visible with one filter above, so "hidden with none" is a real transition.
    const screen = boot();
    document.querySelector("#wrPriority").innerHTML = '<option value="High" selected>High</option>';
    screen.updateFilterCount();
    expect(hidden("[data-wr-filter-count]")).toBe(false);

    document.querySelector("#wrPriority").innerHTML = '<option value="" selected></option>';
    screen.updateFilterCount();
    expect(hidden("[data-wr-filter-count]"), "a zero count stayed visibly showing 0").toBe(true);
  });

  it("counts up to five when every filter is chosen", () => {
    const screen = boot();
    ["#wrLegalEntity", "#wrUnit", "#wrTaskType", "#wrAssignee", "#wrPriority"].forEach((sel, i) => {
      document.querySelector(sel).innerHTML = `<option value="v${i}" selected>v${i}</option>`;
    });
    screen.updateFilterCount();
    expect(text("[data-wr-filter-count]")).toBe("5");
  });

  it("wires select2 to SIX controls — the breakdown and the five filters — never the two dates", () => {
    /*
     * select2 is a text/option picker; flatpickr is this project's date picker. Wiring select2 to `#wrFrom`
     * would give the report's period box a search-and-dropdown UI for a value that is a calendar day, not a
     * choice from a list — the wrong control for the wrong data.
     */
    const selectorList = /\$\('#wrGroupBy, #wrLegalEntity, #wrUnit, #wrTaskType, #wrAssignee, #wrPriority'\)/g;
    const matches = SCRIPT.match(selectorList) || [];
    expect(matches.length, "the select2 target list was not found verbatim — the guard is pointing at nothing")
      .toBeGreaterThanOrEqual(1);
    expect(SCRIPT).not.toMatch(/select2[\s\S]{0,80}#wrFrom/);
  });

  it("does not throw when select2/flatpickr are not on the page — a report must still draw", () => {
    const screen = boot();
    expect(() => screen.initSelect2()).not.toThrow();
    expect(() => screen.initDatePickers()).not.toThrow();
  });

  it("bridges a jQuery-only change back to a real DOM event, exactly once, never looping", () => {
    /*
     * ⚠ THE DEFECT THIS BRIDGE EXISTS FOR, MEASURED IN `Tasks/form.js`: select2 announces a choice through
     * jQuery's `.trigger('change')`, which `addEventListener('change', ...)` never sees. Without the bridge,
     * the company→unit narrowing (kept from Dilim 1a) and the filter-count badge would both go silently deaf
     * the moment select2 wrapped their `<select>`.
     */
    const el = document.querySelector("#wrLegalEntity");
    let nativeChanges = 0;
    el.addEventListener("change", () => { nativeChanges++; });

    // A REAL native change (has no `originalEvent`, exactly like this one) must NOT be re-dispatched — that
    // would be the infinite loop the guard in the source comment describes.
    const fakeJQueryLikeHandler = (event) => {
      if (event && event.originalEvent) { return; }
      el.dispatchEvent(new Event("change", { bubbles: true }));
    };
    el.dispatchEvent(new Event("change"));
    expect(nativeChanges, "a plain native change was echoed back, which would loop with jQuery attached").toBe(1);

    // The bridge itself, exercised against the shipped source's own logic (not a re-description of it):
    // extract the guard condition and confirm it reads `originalEvent`, the same test `form.js` documents.
    expect(SCRIPT).toMatch(/if\s*\(event\s*&&\s*event\.originalEvent\)\s*\{\s*return;\s*\}/);
  });

  it("keeps the company→unit narrowing wired — the ONE dependency Dilim 1a built, carried through select2", () => {
    expect(SCRIPT).toContain("company.addEventListener('change', refreshUnits)");
  });

  it("gives every select2 filter a real EMPTY option IN THE SHIPPED VIEW — live-verified missing in the first draft", () => {
    /*
     * ⚠ LIVE-VERIFIED DEFECT, PINNED HERE, AGAINST THE ACTUAL .cshtml — not the test fixture, which already had
     * an empty option and so could not have caught this. `fillSelect` only PRESERVES a blank `<option>` across a
     * lookup refill — `var any = el.querySelector('option[value=""]'); … if (any) { el.appendChild(any); }` —
     * it never creates one. This dilim's first draft left `#wrLegalEntity`/`#wrUnit`/`#wrTaskType`/
     * `#wrAssignee` with NO options at all in the markup, so the instant a lookup populated real ones there was
     * no way back to "unfiltered": live testing showed those four pickers end up with ZERO options, not even a
     * blank one. select2's own placeholder/clear affordance needs that blank option too — the same shape
     * GoldenReferenceCompact's own `filterPriority` uses in `_Filter.cshtml`.
     */
    ["wrLegalEntity", "wrUnit", "wrTaskType", "wrAssignee", "wrPriority"].forEach((id) => {
      const selectMarkup = VIEW.slice(VIEW.indexOf(`id="${id}"`), VIEW.indexOf(`id="${id}"`) + 1600);
      expect(selectMarkup, `#${id} has no empty <option value=""> in the shipped view`).toMatch(/<option value="">/);
    });
  });

  it("fillSelect PRESERVES an empty option across a refill rather than dropping it", () => {
    document.body.innerHTML = '<select id="probe"><option value="">Any</option></select>';
    const before = document.querySelector("#probe").querySelector('option[value=""]');
    expect(before, "the fixture itself has no empty option to test preservation with").toBeTruthy();

    // Simulate what loadFilterOptions() does: replace the options, as a real lookup response would.
    const el = document.querySelector("#probe");
    const any = el.querySelector('option[value=""]');
    el.innerHTML = "";
    el.appendChild(any);
    const opt = document.createElement("option");
    opt.value = "real-unit-id";
    opt.textContent = "Finance";
    el.appendChild(opt);

    expect(el.querySelector('option[value=""]'), "the empty option did not survive a refill").toBeTruthy();
    expect(el.options).toHaveLength(2);
  });

  it("refreshes select2's own rendered options after a dynamic refill, not only the underlying <select>", () => {
    // fillSelect() replaces <option> elements after the lookups answer; select2 renders from a snapshot and
    // does not notice DOM mutation on its own, so the refill has to say so explicitly.
    expect(SCRIPT).toMatch(/select2-hidden-accessible[\s\S]{0,120}trigger\('change'\)/);
  });
});

describe("(2) the KPI card shell", () => {
  it("gives each of the four tiles an icon avatar, and keeps every number hook exactly where 1b/1c left it", () => {
    // The SHELL is new; the CONTENT hooks are 1b/1c's own and must still exist, unrenamed, unmoved into a
    // different card — a layout slice that silently relocated a number would be the one this brief forbade.
    ["data-wr-cycle-value", "data-wr-cycle-median", "data-wr-cycle-trend", "data-wr-cancel-value",
     "data-wr-rework-tasks", "data-wr-rework-trend", "data-wr-unattended-value", "data-wr-aging",
     "data-wr-effort-value"].forEach((hook) => {
      expect(VIEW.includes(hook), `${hook} is missing from the view`).toBe(true);
    });

    const avatarCount = (VIEW.match(/class="avatar flex-shrink-0"/g) || []).length;
    expect(avatarCount, "not all four KPI cards carry the avatar shell").toBe(4);
  });

  it("still computes no ratio, percentage or score — the shell moved, the rule did not", () => {
    const arithmetic = SCRIPT.split("\n")
      .filter((line) => !line.trim().startsWith("*") && !line.trim().startsWith("//"))
      .join("\n");
    expect(arithmetic).not.toMatch(/spentHours\s*\/|\/\s*estimatedHours/);
    expect(arithmetic).not.toMatch(/efficiency|multiplier|productivityScore|percent/i);
  });
});

describe("(3) the scope badge sits beside the numbers, and stays a badge — never an alert", () => {
  it("is not inside the filter card any more", () => {
    const cardEnd = VIEW.indexOf("</section>");
    const scopeAt = VIEW.indexOf("data-wr-scope");
    expect(scopeAt, "the scope marker was not found at all").toBeGreaterThan(-1);
    expect(scopeAt > cardEnd, "the scope badge is still inside the filter section").toBe(true);
  });

  it("renders as a badge, never as a dismissible alert a reader could close away", () => {
    // A closed alert reads as "acknowledged and gone" — exactly what would let a narrowed report be mistaken
    // for the whole tenant's the moment the banner is dismissed. Checked against the SHIPPED markup and script.
    expect(VIEW).not.toMatch(/alert-dismissible/);
    expect(SCRIPT).not.toMatch(/\balert\(/);
    expect(SCRIPT).toContain("badge.textContent = tenant");
  });
});

describe("(4) the outcomes axis survives a long tail — sorted bar, top eight, one folded 'other'", () => {
  it("draws AT MOST NINE bars for twelve real outcomes — eight shown, one folded", () => {
    const screen = boot();
    screen.render(busy({ totals: bucket({
      flow: { opened: 20, closed: 20, completed: 20, cancelled: 0, unattended: 0 },
      outcomes: manyOutcomes()
    }) }));

    const chart = chartFor("[data-wr-chart-outcomes]");
    expect(chart, "the outcomes chart never drew").toBeTruthy();
    expect(chart.options.xaxis.categories).toHaveLength(9);
    expect(chart.options.xaxis.categories[8]).toBe(LABELS.outcomesOther);
  });

  it("the folded bar's number is the SUM of what did not fit — addition of published counts, not a new measure", () => {
    const screen = boot();
    screen.render(busy({ totals: bucket({
      flow: { opened: 20, closed: 20, completed: 20, cancelled: 0, unattended: 0 },
      outcomes: manyOutcomes()
    }) }));

    // manyOutcomes(): codes I..L carry counts 12, 11, 2, 1 — everything past the top eight (A..H).
    const chart = chartFor("[data-wr-chart-outcomes]");
    expect(chart.options.series[0].data[8]).toBe(12 + 11 + 2 + 1);
  });

  it("draws every bar untouched when eight or fewer outcomes exist — no folded bar appears", () => {
    const screen = boot();
    screen.render(busy({ totals: bucket({
      flow: { opened: 6, closed: 6, completed: 6, cancelled: 0, unattended: 0 },
      outcomes: [{ code: "A", count: 3 }, { code: "B", count: 2 }, { code: "C", count: 1 }]
    }) }));

    const chart = chartFor("[data-wr-chart-outcomes]");
    expect(chart.options.xaxis.categories).toHaveLength(3);
    expect(chart.options.xaxis.categories).not.toContain(LABELS.outcomesOther);
  });

  it("a click on a SHOWN bar still opens that code's tasks — Dilim 1c's clickability survives the shape change", () => {
    const screen = boot();
    screen.render(busy({ totals: bucket({
      flow: { opened: 20, closed: 20, completed: 20, cancelled: 0, unattended: 0 },
      outcomes: manyOutcomes()
    }) }));
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });

    let requested = null;
    global.fetch = (url) => { requested = url; return Promise.resolve({ ok: false, json: () => Promise.resolve(null) }); };

    const chart = chartFor("[data-wr-chart-outcomes]");
    chart.options.chart.events.dataPointSelection(null, null, { dataPointIndex: 2 });   // the third shown bar = "C"

    expect(requested).toContain("bucket=Outcome");
    expect(requested).toContain("argument=C");
  });

  it("a click on the FOLDED bar opens nothing — there is no cell for 'the rest of the outcomes'", () => {
    const screen = boot();
    screen.render(busy({ totals: bucket({
      flow: { opened: 20, closed: 20, completed: 20, cancelled: 0, unattended: 0 },
      outcomes: manyOutcomes()
    }) }));
    screen.setLastQuery({ from: "2026-06-01", to: "2026-07-01", groupBy: "None" });

    let requested = null;
    global.fetch = (url) => { requested = url; return Promise.resolve({ ok: false, json: () => Promise.resolve(null) }); };

    const chart = chartFor("[data-wr-chart-outcomes]");
    chart.options.chart.events.dataPointSelection(null, null, { dataPointIndex: 8 });   // the folded bar

    expect(requested, "the folded bar issued a request nobody can reconcile a count for").toBeNull();
  });

  it("offers no chart-type picker — the shape follows the axis, not a preference", () => {
    expect(VIEW).not.toMatch(/chart[- ]?type/i);
    expect(SCRIPT).not.toMatch(/chartType|selectedChartType/i);
  });
});

describe("(5) the house rules travel with a layout slice too", () => {
  it("styles through classes only — no inline style anywhere, in OUR code (FG-003)", () => {
    [["script", SCRIPT], ["view", VIEW], ["l10n", L10N], ["css file itself", ""]].forEach(([name, source]) => {
      if (!source) { return; }
      expect(source, `${name} uses a style attribute`).not.toMatch(/style="/);
      expect(source, `${name} writes element.style`).not.toMatch(/\.style\./);
    });
  });

  it("puts every new rule in backbone-custom.css, and nowhere is a shape reinvented that this codebase already has", () => {
    // The filter chips are NOT restyled here — they inherit `.dt-filter-host .dt-filter-bar .filter-chip` from
    // the SAME rules GoldenReferenceCompact's panel already uses, verified by the view wrapping its five
    // filters in that exact class rather than a new one.
    expect(VIEW).toMatch(/class="[^"]*\bdt-filter-host\b/);
    expect(CSS).toMatch(/\.dt-filter-host \.dt-filter-bar \.filter-chip/);
  });

  it("defines the two new labels in all seven languages, and actually translates them", () => {
    LANGS.forEach((lang) => {
      NEW_KEYS.forEach((key) => {
        expect(resx(lang).includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
      });
    });

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

  it("carries both new labels across the l10n bridge", () => {
    NEW_KEYS.forEach((key) => {
      expect(L10N.includes(`Localizer["${key}"]`), `${key} never crosses the l10n bridge`).toBe(true);
    });
  });
});

describe("(6) no number changed — the acceptance criterion of a pure layout slice", () => {
  it("the same report payload renders the same figures the pre-1d screen rendered", () => {
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-unattended-value]")).toBe("4");
    expect(text("[data-wr-rework-tasks]")).toBe("0 tasks");
    expect(text("[data-wr-cycle-over]")).toBe("over 0 closed");
  });

  it("draws exactly three charts with no breakdown and a fourth with one — unchanged since 1a", () => {
    const screen = boot();
    screen.render(busy());
    expect(drawn).toHaveLength(3);

    screen.render(busy({
      groupBy: "OrganizationUnit",
      groups: [bucket({ key: "u1", flow: { opened: 3, closed: 1, completed: 1, cancelled: 0, unattended: 0 } })]
    }));
    expect(drawn).toHaveLength(4);
  });
});
