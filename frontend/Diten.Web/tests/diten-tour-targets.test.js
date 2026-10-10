const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-UX-COMMON-01 (BL-495; UXH-001 §5) — every registered tour step points at a `data-tour` mark its page REALLY
 * draws, and every step's text exists in all seven languages. A deleted mark is a red test, not a tour that silently
 * skips a step.
 *
 *   · Timesheet + approvals: the marks are static Razor markup — read from the view.
 *   · Task Center: app.js draws the whole page — the REAL app.js is booted (wcn-boot) and the rendered DOM is read.
 */

const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const catalog = () => {
  delete window.DitenTour;
  delete window.DitenTourCatalog;
  document.body.innerHTML = "";
  loadScript("wwwroot/assets/js/shared/diten-tour.js");
  loadScript("wwwroot/assets/js/shared/diten-tour-catalog.js");
  return window.DitenTourCatalog;
};

const resx = Object.fromEntries(LANGS.map((lang) => {
  const xml = read("Resources", `PageHelpResource.${lang}.resx`);
  const values = new Map();
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) { values.set(m[1], m[2].trim()); }
  return [lang, values];
}));

const navCodes = new Set([...read("Resources", "SharedResource.en.resx").matchAll(/<data name="Nav\.Page\.([A-Z0-9]+)"/g)]
  .map((m) => m[1]));

const STATIC_PAGES = {
  MYTIMESHEET: ["Views", "TimeEntry", "Index.cshtml"],
  TIMEAPPROVALS: ["Views", "TimeEntry", "Approvals", "Index.cshtml"]
};

// One owned, in-progress row with an enabled action, so the list view draws a row and its action cluster.
const ROW = {
  fixtureKind: "workItem", id: "4f6f6a2e-0d7c-4c55-9a7b-000000000001", workIntent: "task", assignmentMode: "direct",
  ownershipState: "owned", admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress",
  executionState: "active", timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Row", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: "4f6f6a2e-0d7c-4c55-9a7b-000000000001", deepLink: "/Tasks/4f6f6a2e-0d7c-4c55-9a7b-000000000001" },
  lifecycleOwner: "tasks", workItemCapabilities: ["execution"],
  actions: [{ code: "complete", label: { kind: "display", text: "Complete", locale: "und" }, semanticType: "complete",
    enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null, requiresConfirmation: false,
    requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal" }],
  primaryActionCode: "complete", overflowActionCodes: [], concurrency: { kind: "version", token: "1" },
  waitingContext: null, escalation: null, dueAt: "2090-01-01T00:00:00+00:00", slaState: "no-sla", blockedState: null
};

describe("the tour catalog", () => {
  test("covers the Task Center and the Timesheet (owner and approver), by real page codes", () => {
    const tours = catalog();
    expect(Object.keys(tours).sort()).toEqual(["MYTIMESHEET", "TIMEAPPROVALS", "WORKCENTER"]);
    Object.keys(tours).forEach((code) => expect(navCodes.has(code), code).toBe(true));
    expect(tours.WORKCENTER.targets.length).toBeGreaterThanOrEqual(4);
    expect(tours.WORKCENTER.targets.length).toBeLessThanOrEqual(8);
  });

  test("the view that owns each code says so (the panel and the tour agree on the code even without the menu)", () => {
    expect(read("Views", "WorkCenterNext", "Index.cshtml")).toContain('ViewData["PageCode"] = "WORKCENTER"');
    expect(read(...STATIC_PAGES.MYTIMESHEET)).toContain('ViewData["PageCode"] = "MYTIMESHEET"');
    expect(read(...STATIC_PAGES.TIMEAPPROVALS)).toContain('ViewData["PageCode"] = "TIMEAPPROVALS"');
  });

  test("every step's title and text exist, non-empty, in all seven languages", () => {
    const tours = catalog();
    const gaps = [];
    Object.entries(tours).forEach(([code, tour]) => tour.targets.forEach((_target, index) => {
      ["Title", "Text"].forEach((part) => LANGS.forEach((lang) => {
        const key = `Tour.${code}.Step${index + 1}.${part}`;
        if (!(resx[lang].get(key) || "").length) { gaps.push(`${lang}/${key}`); }
      }));
    }));
    expect(gaps).toEqual([]);
  });
});

describe("every tour step's target is on its page", () => {
  test.each(Object.keys(STATIC_PAGES))("%s: each data-tour mark is in the view's markup", (code) => {
    const view = read(...STATIC_PAGES[code]);
    const missing = catalog()[code].targets.filter((target) => !view.includes(`data-tour="${target}"`));
    expect(missing).toEqual([]);
  });

  test("WORKCENTER: the real app.js draws each data-tour mark on the list", async () => {
    const targets = catalog().WORKCENTER.targets;
    await bootSurface({ rootAttrs: 'data-wcn-page="list"', items: [ROW] });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    const missing = targets.filter((target) => !app().querySelector(`[data-tour="${target}"]`));
    expect(missing).toEqual([]);
  });
});
