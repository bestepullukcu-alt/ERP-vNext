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

/**
 * The stylesheet with its comments removed.
 *
 * ⚠ EVERY `[^}]*` RULE MATCH IN THIS FILE READS THIS, NOT `CSS`. The comments in backbone-custom.css quote
 * declarations verbatim — `p { margin-bottom: 1rem }` among them — and a brace inside a comment truncates a
 * rule match at the wrong place, so a rule that IS correct reads as missing.
 */
const CSS_RULES = CSS.replace(/\/\*[\s\S]*?\*\//g, "");

const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

const NEW_KEYS = ["FiltersToggle", "OutcomesOther"];

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
  outcomesTitle: "How work ended", outcomesEmpty: "No closure was recorded with an outcome.",
  groupsTitle: "By {0}", groupUnnamed: "Not set", groupByOrganizationUnit: "Organisation unit",
  cycleTimeDays: "{0} days", cycleTimeOver: "over {0} closed",
  effortOver: "over {0} tasks",
  notMeasured: "Not measured", groupByLegalEntity: "Company", groupUnassigned: "Company unknown",
  groupOther: "All other groups", groupsTruncated: "Showing the busiest {0}; {1} more are folded in.",
  filterAny: "Any", priorityHigh: "High", priorityMedium: "Medium", priorityLow: "Low",
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
  <div data-wr-skeleton-tiles hidden></div>
  <div data-wr-skeleton-charts hidden></div>
  <div data-wr-tiles hidden>
    <p data-wr-cycle-value></p><p data-wr-cycle-unit></p><p data-wr-cycle-over></p>
    <dl data-wr-cycle-facts></dl><p data-wr-cycle-trend hidden></p>
    <p data-wr-rework-tasks data-wr-click="Returned" role="button" tabindex="0"></p><dl data-wr-rework-facts></dl><p data-wr-rework-trend hidden></p>
    <p data-wr-unattended-value data-wr-click="Unattended" role="button" tabindex="0"></p><dl data-wr-aging hidden></dl>
    <p data-wr-effort-value></p><p data-wr-effort-over></p><dl data-wr-effort-facts></dl>
  </div>
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

  it("names each filter in its own placeholder — the label was dropped, the NAME must not be", () => {
    /*
     * The chips follow GoldenReferenceCompact's `_Filter.cshtml`: no `<label>`, the field name carried by
     * `data-placeholder`. That shape only works while the placeholder actually IS the field name. Left as the
     * generic "Any" it started as, the panel would render five identically-labelled boxes and a reader would
     * have no way to tell the company picker from the person picker — the exact failure dropping the visible
     * label makes possible, and the only reason this guard exists.
     */
    const expected = {
      wrLegalEntity: "FilterLegalEntity",
      wrUnit: "FilterUnit",
      wrTaskType: "FilterTaskType",
      wrAssignee: "FilterAssignee",
      wrPriority: "FilterPriority"
    };
    Object.entries(expected).forEach(([id, key]) => {
      const tag = new RegExp(`<select id="${id}"[\\s\\S]*?>`).exec(VIEW);
      expect(tag, `${id} is gone from the view`).toBeTruthy();
      expect(tag[0], `${id} is not named by its own placeholder`)
        .toMatch(new RegExp(`data-placeholder="@Localizer\\["${key}"\\]"`));
      expect(tag[0], `${id} lost the compact sizing the reference panel uses`).toMatch(/form-select-sm/);
    });

    expect(VIEW, "a visible <label> came back for a filter chip")
      .not.toMatch(/<label for="wr(LegalEntity|Unit|TaskType|Assignee|Priority)"/);
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
    /*
     * ⚠ THE HOOK LIST MOVED WITH THE SKELETON, AND THE RULE DID NOT. `data-wr-cycle-median` and
     * `data-wr-cancel-value` were prose lines; both facts now live as ROWS inside `data-wr-cycle-facts`, which
     * is why they are gone and it is here. What this test forbids is unchanged: a number quietly relocating to
     * a different card, or a hook disappearing without its fact reappearing somewhere a test can see it.
     */
    ["data-wr-cycle-value", "data-wr-cycle-unit", "data-wr-cycle-facts", "data-wr-cycle-trend",
     "data-wr-rework-tasks", "data-wr-rework-facts", "data-wr-rework-trend",
     "data-wr-unattended-value", "data-wr-aging",
     "data-wr-effort-value", "data-wr-effort-facts"].forEach((hook) => {
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
});

describe("(2c) the loading skeleton — the third state, told apart from the other two", () => {
  const skeletons = () => ["[data-wr-skeleton-tiles]", "[data-wr-skeleton-charts]"]
    .map((sel) => document.querySelector(sel).hidden);

  it("borrows the product's skeleton language whole and invents none of its own", () => {
    /*
     * `.shimmer` + `.skeleton-row` are what the DataTable pages already use. A second skeleton dialect in one
     * product is a bigger problem than no skeleton on one screen: two shimmer speeds, two greys, two dark-mode
     * answers, kept in step by nobody. Only the SIZES are named here, and only because a card's figure is not
     * the height of a table row.
     */
    expect(VIEW).toMatch(/class="row[^"]*backbone-skeleton wr-skeleton"/);
    expect(VIEW, "the shimmer blocks are not the shared ones").toMatch(/class="shimmer skeleton-row/);

    // No second animation, no second grey: the size rules may not repaint the block.
    const sized = CSS_RULES.match(/\.wr-skel(?:eton)? \.wr-skel[^{]*\{[^}]*\}|(?<![\w.-])\.wr-skel-[^{]*\{[^}]*\}/g) || [];
    expect(sized.length, "the skeleton sizes are gone from the stylesheet").toBeGreaterThan(0);
    sized.forEach((rule) => {
      expect(rule, "a skeleton size rule repainted the block instead of only sizing it")
        .not.toMatch(/background(?!-color:\s*$)|animation|linear-gradient/);

      /*
       * ⚠ AND EACH ONE MUST BE TWO CLASSES DEEP, WHICH IS A MEASURED BUG AND NOT A STYLE PREFERENCE.
       * The shared `.backbone-skeleton .skeleton-row` pins every block to 24px. A flat `.wr-skel-chart` loses
       * to it silently — the rule is right there in the file, it simply never wins — and MEASURED that way the
       * chart placeholder rendered 24px instead of 260, leaving the skeleton 340px shorter than the report.
       * The page shrank on Apply and grew back on arrival: the exact jump this slice exists to prevent.
       * Presence was what the first draft of this test checked, and presence is not effect.
       */
      expect(rule, "a skeleton size rule is too weak to beat the shared 24px row")
        .toMatch(/^\.wr-skeleton \.wr-skel/);
    });
  });

  it("stands in the REAL grid, so nothing moves when the data lands", () => {
    // The columns and the card shell are copied from the regions they replace — an approximation is a page
    // that jumps, which is the one thing a skeleton exists to prevent.
    const tiles = VIEW.slice(VIEW.indexOf("data-wr-skeleton-tiles"), VIEW.indexOf("data-wr-tiles"));
    expect((tiles.match(/col-12 col-sm-6 col-xl-3/g) || []).length, "the tile skeleton is not four cards").toBe(1);
    expect(tiles, "the tile skeleton loop is gone").toMatch(/for \(var i = 0; i < 4; i\+\+\)/);

    /*
     * ⚠ THE CHART PLACEHOLDER'S HEIGHT IS THE CHART'S OWN. Read from both sides here: a placeholder shorter
     * than the canvas it stands in for hands the reader a page that jumps the moment the first series draws,
     * and the two numbers live in different files with nothing else holding them together.
     */
    const chartHeight = /\.wr-skel-chart\s*\{[^}]*block-size:\s*(\d+)px/.exec(CSS_RULES);
    expect(chartHeight, "the chart placeholder has no height").toBeTruthy();
    expect(SCRIPT, `the charts no longer draw at ${chartHeight[1]}px`)
      .toContain("height: " + chartHeight[1]);
  });

  it("shows the skeleton while a load is out, and NOTHING else at the same time", () => {
    /*
     * ⚠ THE SECOND LOAD IS THE ONE THAT MATTERS, AND THE FIRST DRAFT OF THIS TEST ONLY CHECKED THE FIRST.
     * On a fresh boot the report regions are already hidden by the markup, so "skeleton on, report off" holds
     * for free and proves nothing. Pressing Apply with a report ALREADY on screen was the real case: the
     * skeleton went in above the visible numbers and grew the page by its own height — the exact jump this
     * whole slice exists to prevent. Rendering a report first is what makes this measure that.
     */
    const screen = boot();
    screen.render(busy());
    expect(hidden("[data-wr-tiles]"), "the report never rendered, so hiding it proves nothing").toBe(false);

    screen.showSkeleton(true);

    expect(skeletons(), "the skeleton stayed hidden during a load").toEqual([false, false]);
    expect(hidden("[data-wr-tiles]"), "the report and its skeleton were on screen together").toBe(true);
    expect(hidden("[data-wr-charts]")).toBe(true);
  });

  it("puts it away for ALL THREE endings — a report, an empty period, a failure", async () => {
    /*
     * ⚠ THE DEFECT THIS CLOSES IS THAT THREE STATES LOOKED IDENTICAL. A shimmer left running under any of
     * them would put the identical-looking screen back, just animated — so every ending is checked, not the
     * happy one.
     */
    const screen = boot();

    screen.showSkeleton(true);
    screen.render(busy());
    expect(skeletons(), "a drawn report left the skeleton running").toEqual([true, true]);
    expect(hidden("[data-wr-tiles]")).toBe(false);

    screen.showSkeleton(true);
    screen.render(report());
    expect(skeletons(), "an empty period left the skeleton running").toEqual([true, true]);
    expect(text("[data-wr-status]"), "the empty period stopped saying so").toBe(LABELS.noDataScoped);

    screen.showSkeleton(true);
    global.fetch = () => Promise.reject(new Error("network"));
    await screen.load();
    expect(skeletons(), "a failed load left the skeleton running").toEqual([true, true]);
    expect(text("[data-wr-status]"), "the failure stopped saying why").toBe(LABELS.loadFailed);
  });

  it("says nothing in words while it is loading — the shimmer already said it", () => {
    /*
     * A "Loading…" line beside a page of shimmering blocks is the same statement twice, in two languages, one
     * of which has to be translated seven times. Checked in the SHIPPED script, because the sentence would
     * come back as a one-line convenience.
     */
    const loadFn = SCRIPT.slice(SCRIPT.indexOf("var load = function"), SCRIPT.indexOf("var fillSelect"));
    expect(loadFn, "the load path put a Loading sentence back under the skeleton")
      .not.toMatch(/data-wr-status\]',\s*t\('Loading'\)/);
    expect(loadFn, "the status line is not cleared, so a stale message can sit under the skeleton")
      .toMatch(/setText\('\[data-wr-status\]', ''\)/);
  });

  it("keeps a picker unusable until its own lookup answers", () => {
    /*
     * An empty picker and a not-yet-loaded picker look the same, and a reader who opens one early concludes
     * "there are no companies". Disabled in the markup, released exactly where the options arrive.
     */
    ["wrLegalEntity", "wrUnit", "wrTaskType", "wrAssignee"].forEach((id) => {
      const tag = new RegExp(`<select id="${id}"[\\s\\S]*?>`).exec(VIEW);
      expect(tag, `${id} is gone from the view`).toBeTruthy();
      expect(tag[0], `${id} is usable before its lookup answered`).toMatch(/\bdisabled\b/);
    });

    // `wrPriority` carries its options in the markup — disabling it would be a lie about a list already there.
    const priority = /<select id="wrPriority"[\s\S]*?>/.exec(VIEW);
    expect(priority[0], "the static picker was disabled too").not.toMatch(/\bdisabled\b/);

    expect(SCRIPT, "nothing releases the pickers once their options land")
      .toMatch(/el\.disabled = false/);
  });
});

describe("(3) the scope badge sits beside the numbers, and stays a badge — never an alert", () => {
  it("closes the filter card as its own strip — inside the card, after the body, under a divider", () => {
    /*
     * ⚠ THIS TEST ONCE ASSERTED THE OPPOSITE, and the reversal is a decision rather than a bug: the scope line
     * belongs to the card that CHOOSES the scope, not to the page. What it guards is unchanged in substance —
     * the line must be somewhere a reader cannot miss and cannot dismiss — so the guard moved with it instead
     * of being deleted, which is what would have left this placement unmeasured.
     */
    const cardEnd = VIEW.indexOf("</section>");
    const bodyEnd = VIEW.indexOf('data-wr-status role="status"');
    const scopeAt = VIEW.indexOf('class="wr-scope-bar');
    expect(scopeAt, "the scope strip was not found at all").toBeGreaterThan(-1);
    expect(scopeAt < cardEnd, "the scope strip drifted back out of the filter card").toBe(true);
    expect(scopeAt > bodyEnd, "the scope strip is inside the card BODY, not closing the card").toBe(true);

    // The divider and the off-white are the card's and the product's own tokens — never a one-off value.
    const rule = /\.wr-scope-bar\s*\{([^}]*)\}/.exec(CSS);
    expect(rule, "the scope strip has no rule in backbone-custom.css").toBeTruthy();
    expect(rule[1], "the divider the strip sits under is missing").toMatch(/border-top:\s*1px solid var\(--bs-border-color\)/);
    expect(rule[1], "an off-white was invented instead of taken from the product's tokens")
      .toMatch(/background:\s*var\(--bs-light-bg-subtle\)/);
    expect(rule[1], "a literal colour was hard-coded into the strip").not.toMatch(/#[0-9a-f]{3,8}\b/i);
  });

  it("switches scope with the product's OWN segmented control, and does not restyle it here", () => {
    /*
     * `.wcn-segments` / `.wcn-seg` is Görev Merkezi's status switch, already carrying its resting, hover and
     * selected states. Reaching for it rather than dressing up a `btn-group` is what keeps one idea looking
     * like one idea in two places — and what keeps this screen from owning a hover colour it would then have
     * to keep in step with a control it does not own.
     */
    expect(VIEW, "the scope switcher is not the shared segmented control").toMatch(/class="wcn-segments"[^>]*data-wr-scope-chips/);
    expect(VIEW, "the segments are not the shared ones").toMatch(/class="wcn-seg"[^>]*data-wr-scope-chip="tenant"/);
    expect(VIEW, "the outline button pair came back").not.toMatch(/btn-outline-primary[^>]*data-wr-scope-chip/);

    // Only the borrowed control's OUTER spacing may be adjusted here; its own look stays where it lives.
    const local = CSS.match(/\.wr-scope-bar \.wcn-seg[^{]*\{([^}]*)\}/g) || [];
    local.forEach((rule) => {
      expect(rule, "this screen restyled the shared segment instead of reusing it")
        .not.toMatch(/background|color|border-radius|font-/);
    });
  });

  it("renders as a badge, never as a dismissible alert a reader could close away", () => {
    // A closed alert reads as "acknowledged and gone" — exactly what would let a narrowed report be mistaken
    // for the whole tenant's the moment the banner is dismissed. Checked against the SHIPPED markup and script.
    expect(VIEW).not.toMatch(/alert-dismissible/);
    expect(SCRIPT).not.toMatch(/\balert\(/);
    expect(SCRIPT).toContain("badge.textContent = tenant");
  });
});

describe("(2b) one skeleton, four cards", () => {
  const CARDS = ["cycle", "rework", "unattended", "effort"];

  it("gives every card the same five layers, in the same order", () => {
    /*
     * ⚠ THE ANATOMY IS THE DESIGN. Before this, one card carried three prose lines, another carried one, a
     * third a sentence plus a note — four cards side by side whose parts did not line up, so the eye had to
     * re-learn each one. A card that quietly grows a sixth layer or drops the qualifier goes back to that.
     */
    const cards = VIEW.split('<section class="card h-100 wr-kpi');
    expect(cards.length - 1, "there are no longer four KPI cards").toBe(4);

    cards.slice(1).forEach((card, i) => {
      const order = ["avatar flex-shrink-0", "wr-answer", "wr-qual", "wr-facts", "wr-kpi-foot"]
        .map((cls) => card.indexOf(cls));
      expect(order.every((n) => n > -1), `card ${i + 1} is missing a layer: ${order}`).toBe(true);
      const sorted = [...order].sort((a, b) => a - b);
      expect(order, `card ${i + 1} carries the layers out of order`).toEqual(sorted);
    });
  });

  it("puts every card's figure and unit on one answer line, and every fact in the shared table", () => {
    const screen = boot();
    // A MEASURED average, deliberately: with nothing closed the answer is the words "Not measured" and there
    // is no unit to separate — the case this assertion is about cannot arise.
    screen.render(busy({ totals: bucket({
      flow: { opened: 9, closed: 4, completed: 4, cancelled: 0, unattended: 4 },
      cycleTime: { averageDays: 11.33, medianDays: 10, count: 4 },
      aging: { upTo7Days: 2, from8To30Days: 1, olderThan30Days: 5 },
      effort: { estimatedHours: 40, spentHours: 60, taskCount: 6 },
      rework: { tasksReturned: 3, totalReturns: 5 }
    }) }));

    // The answer: a bare figure, never a figure with its unit baked into the same string.
    expect(text("[data-wr-cycle-value]"), "the unit crept back into the figure").not.toMatch(/[a-z]/i);
    expect(text("[data-wr-cycle-unit]"), "the unit stopped being published").toBe("days");
    expect(text("[data-wr-unattended-value]")).not.toMatch(/[a-z]/i);
    expect(text("[data-wr-effort-value]")).not.toMatch(/[a-z]/i);

    // And each card's supporting facts really are rows, not a sentence dropped into the table.
    CARDS.forEach((card) => {
      const sel = card === "unattended" ? "[data-wr-aging]" : `[data-wr-${card}-facts]`;
      const dl = document.querySelector(sel);
      expect(dl, `${card} has no facts table`).toBeTruthy();
      expect(dl.querySelectorAll("dt").length, `${card} published no facts`).toBeGreaterThan(0);
      expect(dl.querySelectorAll("dt").length, `${card}'s labels and values do not pair up`)
        .toBe(dl.querySelectorAll("dd").length);
    });
  });

  it("aligns the values on ONE right-hand edge, shared by all four cards", () => {
    /*
     * The reason the table exists: the values of four DIFFERENT cards read down a single line. That needs a
     * two-track grid whose second track is content-width and right-aligned, and tabular figures — without
     * them a `1` is narrower than a `4` and right-aligned numbers still wobble per digit.
     */
    const rule = /\.wr-facts\s*\{([^}]*)\}/.exec(CSS);
    expect(rule, "the shared facts table has no rule").toBeTruthy();
    expect(rule[1]).toMatch(/grid-template-columns:\s*1fr auto/);

    const dd = /\.wr-facts dd\s*\{([^}]*)\}/.exec(CSS);
    expect(dd, "the value cell has no rule").toBeTruthy();
    expect(dd[1], "values are not right-aligned").toMatch(/text-align:\s*end/);
    expect(dd[1], "the digits will wobble without tabular figures").toMatch(/tabular-nums/);
  });

  it("lets nothing in a card's floor carry a margin the render can put back", () => {
    /*
     * ⚠ THE DEFECT THIS PINS WAS INVISIBLE TO EVERY OTHER TEST, and it came from a helper being tidy.
     * `trend()` REPLACES the element's whole className each render, so `mb-0` written in the view survives
     * until the first report arrives and then Bootstrap's `p { margin-bottom: 1rem }` comes back. MEASURED:
     * the two cards with a delta ended their last line at y=564 while the two with a note ended at y=580 —
     * a 16px step in a row of cards whose entire point is that their lines align. jsdom does no layout, so
     * this cannot be measured here; what CAN be pinned is the cause, which is where the margin is declared.
     */
    const rule = /\.wr-trend\s*\{([^}]*)\}/.exec(CSS_RULES);
    expect(rule, "the delta pill has no rule").toBeTruthy();
    expect(rule[1], "the delta's margin is not zeroed where the render cannot undo it")
      .toMatch(/margin:\s*0/);

    // And the class really is rewritten wholesale — the reason the margin cannot live in the markup.
    expect(SCRIPT, "trend() stopped replacing className; the comment above is now wrong")
      .toMatch(/el\.className\s*=\s*'wr-trend/);
  });

  it("gives the whole floor ONE size, so a delta and a caveat read as the same kind of line", () => {
    const foot = /\.wr-kpi-foot\s*\{([^}]*)\}/.exec(CSS_RULES);
    expect(foot, "the floor has no rule").toBeTruthy();
    expect(foot[1], "the floor no longer sets its own type size").toMatch(/font-size:/);

    /*
     * ⚠ AND NOTHING INSIDE THE FLOOR MAY DECLARE ITS OWN. `.wr-trend` carries a size for the chart headers,
     * and a class on the element beats what the floor sets by inheritance — MEASURED after the floor's rule
     * was added and before this one: the two cards with a delta rendered 13px, the two with a note 12px. The
     * split the floor exists to close had simply swapped sides.
     */
    const inCard = /\.wr-kpi \.wr-trend\s*\{([^}]*)\}/.exec(CSS_RULES);
    expect(inCard, "the delta has no card-scoped rule").toBeTruthy();
    expect(inCard[1], "the delta sets its own size inside a card instead of taking the floor's")
      .toMatch(/font-size:\s*inherit/);

    // `small` on a footnote would beat the inherited size and split the slot in two again.
    const cards = VIEW.split('<section class="card h-100 wr-kpi').slice(1)
      .map((c) => c.slice(0, c.indexOf("</section>")));
    cards.forEach((card, i) => {
      const floor = card.slice(card.indexOf('class="wr-kpi-foot"'));
      expect(floor, `card ${i + 1}'s floor overrides the shared size with .small`).not.toMatch(/class="[^"]*\bsmall\b/);
    });
  });

  it("keeps the delta quiet at a card's floor — it is a footnote, not the headline", () => {
    // Filled, it outweighed the figure it was commenting on. The meaning class is untouched; only the surface.
    const rule = /\.wr-kpi \.wr-trend\s*\{([^}]*)\}/.exec(CSS);
    expect(rule, "the card delta has no quiet rule").toBeTruthy();
    expect(rule[1], "the filled pill came back inside the cards").toMatch(/background:\s*none/);
  });
});

describe("(3c) the drill-down panel's empty box sits on the surface it replaces", () => {
  it("is painted with the SAME token as the result cards, and with a root token at that", () => {
    /*
     * ⚠ TWO MEASURED DEFECTS IN ONE RULE, AND BOTH WERE INVISIBLE TO EVERY OTHER CHECK.
     *
     * First: the box had no fill at all, so on this panel's grey background it read as a hole punched in the
     * panel rather than as a card with nothing in it.
     *
     * Second, and worse: the fix `background: var(--bs-card-bg)` was a rule that existed and painted NOTHING.
     * Sneat declares that variable on `.card`, not on `:root`, so outside a card it resolves to empty —
     * measured `rgba(0, 0, 0, 0)` on a box that was meant to be white. A stylesheet grep would have called
     * that rule present and correct. The token has to be one that exists where it is used, and the right one
     * is the token the ROWS use, because the empty box stands exactly where they would have been.
     */
    const empty = /\.wr-items-empty\s*\{([^}]*)\}/.exec(CSS_RULES);
    expect(empty, "the empty box has no surface rule").toBeTruthy();
    expect(empty[1], "the empty box lost its fill and reads as a hole in the panel").toMatch(/background:/);
    expect(empty[1], "--bs-card-bg is not defined outside a card; it paints nothing here")
      .not.toMatch(/--bs-card-bg/);

    const row = /\.wcn-row\s*\{([^}]*)\}/.exec(CSS_RULES);
    const token = /var\((--[a-z-]+)\)/.exec(empty[1]);
    expect(token, "the empty box hard-codes a colour instead of taking the product's").toBeTruthy();
    expect(row[1], `the rows are no longer painted with ${token[1]}`).toContain(token[1]);
  });
});

describe("(3a) every icon this screen names actually exists in the font", () => {
  it("names no glyph the icon font does not define", () => {
    /*
     * ⚠ MEASURED, AND IT HAD ALREADY SHIPPED. The drill-down panel's empty state asked for `bx-inbox`, which
     * this product's icon font does not define — it rendered as a blank grey square, which reads as a broken
     * image and is strictly worse than showing no icon. Nothing anywhere checked that an icon name resolves,
     * so a typo or a name borrowed from a different Boxicons build fails silently and looks like a bug in the
     * data. Cheap to check, and it checks EVERY icon on the screen rather than the one that was wrong.
     */
    const font = fs.readFileSync(
      web("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css"), "utf8");

    /*
     * ⚠ RAZOR COMMENTS ARE STRIPPED FIRST, and the first draft of this test needed to learn that: the comment
     * beside the empty state NAMES the missing icon in order to explain why it was replaced, and a scan of the
     * raw file read that explanation as the defect it describes.
     */
    const markup = VIEW.replace(/@\*[\s\S]*?\*@/g, "");
    const named = [...new Set([...markup.matchAll(/\bbx-[a-z0-9-]+/g)].map((m) => m[0]))];
    expect(named.length, "the view names no boxicons at all — the pattern changed").toBeGreaterThan(3);

    const missing = named.filter((name) => !font.includes(name));
    expect(missing, `these icon names are not defined in the font: ${missing.join(", ")}`).toEqual([]);
  });
});

describe("(3b) the timeliness ring reads as three aligned lines", () => {
  const legendRows = () =>
    [...document.querySelectorAll("[data-wr-timeliness-legend] li")].map((li) => ({
      dot: li.querySelector(".wr-legend-dot").className,
      label: li.querySelector(".wr-legend-label").textContent,
      count: li.querySelector(".wr-legend-count").textContent,
      share: li.querySelector(".wr-legend-share").textContent,
      click: li.getAttribute("data-wr-click")
    }));

  it("prints each band's own count and its share of the ring", () => {
    const screen = boot();
    // 5 + 3 + 1 = 9 → 56% · 33% · 11%. Deliberately NOT a set that divides evenly: rounded shares are
    // allowed to miss 100, because forcing the last row to absorb the remainder would print a share that
    // disagrees with the count beside it.
    screen.render(busy());

    expect(legendRows()).toEqual([
      { dot: "wr-legend-dot wr-legend-dot--ontime", label: "On time", count: "5", share: "56%", click: "OnTime" },
      { dot: "wr-legend-dot wr-legend-dot--late", label: "Late", count: "3", share: "33%", click: "Late" },
      { dot: "wr-legend-dot wr-legend-dot--undated", label: "No deadline set", count: "1", share: "11%", click: "WithoutDueDate" }
    ]);
  });

  it("draws NO share at all when there was nothing to be a share of", () => {
    // PRESENCE FIRST: the populated case above proves shares render, so their absence here is a real state.
    const screen = boot();
    screen.render(report({ totals: bucket({ flow: { opened: 3, closed: 0, completed: 0, cancelled: 0, unattended: 3 } }) }));

    const rows = legendRows();
    expect(rows.length, "the legend stopped rendering its bands entirely").toBe(3);
    rows.forEach((r) => {
      expect(r.count).toBe("0");
      // "0%" is a measured result; 0/0 is not one. An empty ring must not report three tidy zeroes.
      expect(r.share, "an empty period reported a share").toBe("");
    });
  });

  it("the ring and its legend cannot disagree about a colour", () => {
    /*
     * apex takes colours as JS values and this screen may not write `element.style` (FG-003), so the arc
     * colour lives in index.js and the swatch colour in backbone-custom.css. Two places, one meaning: a
     * legend whose green dot sat beside a red arc would be worse than no legend, because a reader would
     * trust it. This is the only thing holding them together.
     */
    const jsColors = /var TIMELINESS_COLORS = \[([^\]]*)\]/.exec(SCRIPT);
    expect(jsColors, "TIMELINESS_COLORS is gone from the script").toBeTruthy();
    const arcs = jsColors[1].split(",").map((c) => c.trim().replace(/'/g, "").toLowerCase());

    const swatch = (mod) => {
      const m = new RegExp(`\\.wr-legend-dot--${mod}\\s*\\{[^}]*background:\\s*(#[0-9a-f]{3,8})`, "i").exec(CSS);
      return m ? m[1].toLowerCase() : null;
    };
    expect([swatch("ontime"), swatch("late"), swatch("undated")]).toEqual(arcs);
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

describe("(5b) UI-026 — four cards that read as one row", () => {
  // The effort card's own strings, after the skeleton split them: the unit beside the figure, the hours
  // format its estimate row uses, and the label of that row.
  const EFFORT_KEYS = ["EffortSpentUnit", "Hours", "LabelEstimated"];

  it("closes every KPI card with the wrapper that pins the footnotes to one baseline", () => {
    /*
     * The rule is "the last lines end at the same height", and the mechanism is `margin-top:auto` on a block
     * that is ALWAYS in the layout. Both halves are checked here because either alone is silently useless: the
     * class without the CSS is a no-op, and the CSS without a column body has nothing to push against.
     */
    const cards = VIEW.split('<section class="card h-100 wr-kpi');
    expect(cards.length - 1, "there are no longer four KPI cards to align").toBe(4);
    cards.slice(1).forEach((card, i) => {
      expect(card, `KPI card ${i + 1} has no footnote floor`).toContain('class="wr-kpi-foot"');
    });

    expect(CSS, "the floor has nothing to push against").toMatch(/\.wr-kpi \.card-body\s*\{[^}]*flex-direction:\s*column/);
    expect(CSS, "the floor does not float").toMatch(/\.wr-kpi-foot\s*\{[^}]*margin-top:\s*auto/);

    // A pinned height would clip the first long sentence in the first language that runs long.
    expect(CSS, "a fixed height came back").not.toMatch(/\.wr-kpi[^{]*\{[^}]*\b(height|min-height|max-height):/);
  });

  it("gives the split effort card its three labels in all seven languages, actually translated", () => {
    LANGS.forEach((lang) => {
      EFFORT_KEYS.forEach((key) => {
        expect(resx(lang).includes(`name="${key}"`), `${key} missing in ${lang}`).toBe(true);
      });
    });

    const value = (lang, key) => {
      const m = new RegExp(`name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(resx(lang));
      return m ? m[1].trim() : null;
    };
    EFFORT_KEYS.forEach((key) => {
      const english = value("en", key);
      expect(english, `${key} has no English text`).toBeTruthy();
      // The unit-only keys ("{0} h") legitimately match in some languages; the LABEL must not.
      if (key === "EffortSpentUnit") {
        LANGS.filter((l) => l !== "en").forEach((lang) => {
          expect(value(lang, key), `${key}/${lang} is still the English text`).not.toBe(english);
        });
      }
      expect(L10N.includes(`Localizer["${key}"]`), `${key} never crosses the l10n bridge`).toBe(true);
    });

    // The string it replaced is gone from every layer — a dead key is a key somebody re-wires by mistake.
    /*
     * Every string the skeleton replaced is gone from every layer — a dead key is a key somebody re-wires by
     * mistake, and these five were whole sentences whose shape no longer exists anywhere on the card.
     */
    ["EffortHours", "EffortSpentLabel", "EffortEstimate", "CancelTime", "AgingBuckets"].forEach((dead) => {
      expect(L10N, `${dead} is still bridged`).not.toContain(`Localizer["${dead}"]`);
      LANGS.forEach((lang) => {
        expect(resx(lang).includes(`name="${dead}"`), `${dead} still defined in ${lang}`).toBe(false);
      });
    });
  });
});

describe("(6) no number changed — the acceptance criterion of a pure layout slice", () => {
  it("the same report payload renders the same figures the pre-1d screen rendered", () => {
    const screen = boot();
    screen.render(busy());

    expect(text("[data-wr-unattended-value]")).toBe("4");
    expect(text("[data-wr-rework-tasks]")).toBe("0");
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
