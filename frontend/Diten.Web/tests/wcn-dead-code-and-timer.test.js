const fs = require("fs");
const path = require("path");

/*
 * TUR B (2026-08-24) — nine uncalled renders, two dead ends, two empty panels, and one card that had a readout
 * but no controls.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CONTRACT = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "fixture-contract.js"), "utf8");
const MAPPER = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "mock-data.js"), "utf8");
const MODELS = fs.readFileSync(web("..", "..", "services", "Diten.Platform", "src",
  "Diten.Platform.Application", "Features", "WorkAggregation", "WorkAggregationModels.cs"), "utf8");
const PROVIDER = fs.readFileSync(web("..", "..", "services", "Diten.Platform", "src",
  "Diten.Platform.Application", "Features", "Tasks", "Providers", "TaskWorkItemProvider.cs"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

/** Every shipped front-end file — a deletion is only real if nothing anywhere still names it. */
const shipped = () => {
  const out = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "node_modules") { walk(p); } }
    else if (/\.(js|cshtml|css)$/.test(e.name)) { out.push(p); }
  });
  walk(web("wwwroot", "assets"));
  walk(web("Views"));
  return out;
};

describe("the dead renders are gone, not just unwired", () => {
  /*
   * MUTATION GUARD: paste any of these back and this goes red WITH ITS NAME. Half-deleted code is worse than
   * code left alone — it reads as maintained and nobody dares touch it.
   */
  const DELETED = [
    // No provider, no data — measured at zero matches in the api and the fixtures.
    "renderReviewContext", "renderExceptionContext", "renderMeetingContext",
    // Redundant: the comment/activity card already does this job.
    "renderThread",
    // List-page view modes, not detail cards — they take a list, sort it and emit cards.
    "renderCalendar", "renderKanban", "renderSplit",
    // Unreachable: a Bootstrap dropdown replaced this menu, so no click could arrive.
    "openNew",
    /*
     * The bulk strip: `data-wcn-check` was read in four places and drawn in none.
     * ⚠ `bulkBar` IS NOT ON THIS LIST, deliberately. It is also a local variable in `diten-datatable.js` —
     * the shared DataTable infrastructure every module uses, and nothing to do with this one. A deletion
     * guard that matches a generic name across the whole tree reports another module's healthy code as a
     * leftover; it is checked inside WorkCenterNext instead, below.
     */
    "runBulk", "runBulkWithProgress", "performBulk",
    // Permanently empty once the code that fed them was deleted a round earlier.
    "renderNotes", "renderAgenda"
  ];

  it("names none of them anywhere under wwwroot or Views", () => {
    const files = shipped();
    DELETED.forEach((fn) => {
      const hits = files.filter((f) => {
        const text = fs.readFileSync(f, "utf8");
        // The vendor date-range picker has its own `renderCalendar`; only OUR files count.
        return f.indexOf("vendor") < 0 && new RegExp(`\\b${fn}\\b`).test(code(text));
      }).map((f) => path.relative(web(), f));
      expect(hits, `${fn} still has a caller or a definition`).toEqual([]);
    });
  });

  it("removes the strip's own renderer from this module", () => {
    // Scoped to WorkCenterNext for the reason given above — the name is not unique in the product.
    expect(code(APP), "the bulk bar renderer came back").not.toContain("const bulkBar");
  });

  it("leaves no orphaned handler behind", () => {
    const stripped = code(APP);
    ["data-wcn-bulk", "data-wcn-check-all"].forEach((attr) =>
      expect(stripped, `${attr} is still dispatched to nothing`).not.toContain(attr));
  });
});

describe("the effort card was connected, not invented", () => {
  it("carries spent hours all the way to the client", () => {
    /*
     * MUTATION GUARD: drop `SpentHours` from the DTO and this goes red.
     *
     * MEASURED before: the create form collected both figures (`FieldEstimateHours`, `FieldSpentHours`), the
     * entity stored both (`TaskItem.EstimateHours`, `SpentHours`), and the DTO carried only the estimate. The
     * card had existed from the start and had never once rendered.
     */
    expect(MODELS).toContain("decimal? SpentHours = null");
    expect(PROVIDER).toContain("SpentHours:");
    expect(MAPPER, "the flat pair is never assembled into what the card reads").toContain("item.effort =");
  });

  it("gates on a capability the contract actually knows", () => {
    // The renderer already read `taskContext`; the contract had never heard of it, so nothing could declare it.
    expect(CONTRACT).toContain("'taskContext'");
    expect(CONTRACT).toContain("taskContext: ['effort']");
    expect(PROVIDER).toContain('capabilities.Add("taskContext")');
    // Declared only when there are figures — a capability is a promise the card has something to show.
    expect(PROVIDER).toMatch(/estimateHours is not null \|\| spentHours != 0/);
  });

  it("draws only the half it has data for", () => {
    /*
     * The card used to render an assignment history too. Measured: `assignmentHistory` has ZERO matches in the
     * mapper, the contract and the whole backend. Half a card with data and half with an empty sub-heading is
     * worse than a card that shows what it knows — the reader cannot tell "nobody reassigned this" from "we do
     * not track that". The field list is in the backlog.
     */
    const fn = APP.slice(APP.indexOf("const renderTaskContext"), APP.indexOf("const renderTaskContext") + 1400);
    expect(code(fn), "the history came back without its data").not.toContain("assignmentHistory");
    expect(fn).toContain("EffortSpent");
  });

  it("sizes its bar with the product's own step classes, never an inline style", () => {
    // FG-003. The exact figure survives in `aria-valuenow` and in the "spent / estimate" reading beside it.
    const fn = APP.slice(APP.indexOf("const renderTaskContext"), APP.indexOf("const renderTaskContext") + 1400);
    expect(code(fn), "an inline width came back").not.toContain('style="width:');
    expect(fn).toContain("wcn-progress-${step}");
    expect(fn).toContain("aria-valuenow");
  });
});

describe("the timesheet card gained a control, not an authority", () => {
  it("draws Log time, and the rail no longer does", () => {
    /*
     * MUTATION GUARD: put `logTime` back in the rail and this goes red.
     *
     * Logging minutes changes no state — it is a personal measurement, not a lifecycle move — so standing it
     * beside Complete and Pause misfiled it. One action, one home.
     */
    const card = APP.slice(APP.indexOf("const renderTimesheet"), APP.indexOf("const renderTimesheet") + 3000);
    expect(card).toContain("wcn-ts-log");
    expect(card).toContain("a.key === 'logTime'");
    expect(code(APP), "the rail draws it again").toContain("if (a.key === 'logTime') { return false; }");
  });

  it("does NOT move start or pause into the card", () => {
    /*
     * ⚠ THE POINT OF THE WHOLE ITEM. The timer is a SIDE EFFECT of the task's state: `start` runs it, `pause`
     * and `complete` fold it. A start/pause button here would open a second way to change the task's
     * lifecycle from inside a card that reads as a readout — the same "second authority" this session refused
     * for document approval.
     */
    const card = APP.slice(APP.indexOf("const renderTimesheet"), APP.indexOf("const renderTimesheet") + 3000);
    expect(code(card), "the card started driving the lifecycle").not.toMatch(/data-wcn-action="(start|pause|complete)"/);
  });

  it("says what the timer is doing, in all seven languages", () => {
    ["TimerStateRunning", "TimerStatePaused", "TimerFollowsStatusHint"].forEach((key) => {
      /*
       * ⚠ BY NAME, NOT BY CALL SHAPE. Two of the three are chosen through a computed `stateKey` and reach the
       * translator as `t(stateKey)`, so asserting on `t('TimerStateRunning')` would fail on working code —
       * and would have pushed the next reader to inline the ternary just to satisfy a test.
       */
      expect(APP, `${key} is not referenced`).toContain(key);
      LANGS.forEach((lang) => {
        const xml = resx(lang);
        const at = xml.indexOf(`name="${key}"`);
        expect(at, `${lang} is missing ${key}`).toBeGreaterThan(-1);
        const v = xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
        expect(v.trim(), `${lang}/${key} is empty`).not.toBe("");
      });
    });
  });
});
