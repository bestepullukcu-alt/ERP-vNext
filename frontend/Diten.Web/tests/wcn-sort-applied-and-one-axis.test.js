const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * ITEM A — WorkCenter's last four.
 *
 *   ① the list sort control wrote state, wrote the URL, and never reordered a row
 *   ② a calendar day with many items — measured, not changed
 *   ③ the type axis drawn by two code paths
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

const id = (n) => `dddddddd-0000-0000-0000-${String(n).padStart(12, "0")}`;
const row = (n, { title, priority = "Medium", pinned = false, sla = "no-sla" } = {}) => ({
  fixtureKind: "workItem",
  id: id(n),
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "InProgress",
  taskLifecycle: "InProgress",
  executionState: "active",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: title, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "display", text: "InProgress", locale: "und" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: id(n), deepLink: `/Tasks/${id(n)}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["execution"],
  actions: [{
    code: "complete", label: { kind: "display", text: "Tamamla", locale: "und" },
    semanticType: "complete", enabled: true, source: "provider",
    disabledReasonCode: null, disabledReason: null,
    requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
    supportsBulk: false, riskLevel: "normal"
  }],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2090-01-01T00:00:00+00:00",
  slaState: sla,
  priority,
  personal: pinned ? { pinned: true } : undefined
});

const boot = async (items) => {
  const r = await bootSurface({ rootAttrs: 'data-wcn-page="list"', items });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await new Promise((x) => setTimeout(x, 0));
  return r;
};
const settle = () => new Promise((x) => setTimeout(x, 0));
/** Titles in the order the ROWS are drawn — the surface actually under test. */
const titles = () => [...app().querySelectorAll(".wcn-row .wcn-row-title")].map((e) => e.textContent.trim());
const sortBy = async (key) => {
  app().querySelector(".wcn-sortbtn").click();
  await settle();
  app().querySelector(`[data-wcn-sort="${key}"]`).click();
  await settle();
};

describe("① the chosen order reaches the rows", () => {
  const SET = [row(1, { title: "Ceyiz" }), row(2, { title: "Ahmet" }), row(3, { title: "Berk" })];

  it("reorders the LIST ROWS, not just the state", async () => {
    /*
     * MEASURED live on `?sort=priority&dir=desc`: High·High·High·Medium·Low·High — a High AFTER a Low, i.e.
     * no ordering at all. `renderList` sorted with a hard-coded `bySla` and ignored `state.sortKey`.
     *
     * ⚠ THE PREVIOUS ROUND "VERIFIED" THIS BY READING `aria-sort`, a TABLE attribute the list does not have.
     * The check passed against a surface that was not under test. This assertion reads the ROW TITLES.
     *
     * MUTATION GUARD: put `sort(bySla)` back and this goes red.
     */
    await boot(SET);
    await sortBy("title");
    expect(titles()).toEqual(["Ahmet", "Berk", "Ceyiz"]);
  });

  it("reverses on the second click, and says so in the URL", async () => {
    await boot(SET);
    await sortBy("title");
    await sortBy("title");
    expect(titles()).toEqual(["Ceyiz", "Berk", "Ahmet"]);
    expect(new URL(global.location.href).searchParams.get("dir")).toBe("desc");
  });

  it("reads the order out of the URL as well as out of the control", () => {
    /*
     * ⚠ ASSERTED ON THE HYDRATE PATH, NOT BY BOOTING A URL — and the reason is worth writing down: the shared
     * harness RESETS `location` before every boot (`replaceState(null, "", "/WorkCenterNext")`) so that one
     * test cannot leak its query string into the next. A test that set the URL first would silently measure
     * the default order and pass for the wrong reason.
     *
     * The round trip itself was measured live instead: `?tab=islerim&sort=priority&dir=desc` produced
     * Low → Medium → Medium … read from the RAW projection, not from the chips on screen.
     */
    const hydrate = APP.split("const hydrateStateFromUrl")[1].split("const syncUrl")[0];
    expect(hydrate).toContain("Object.prototype.hasOwnProperty.call(SORTERS, sortKey)");
    expect(hydrate).toContain("setIfAllowed('sortDir', 'dir', 'sortDir')");
  });

  it("does not leave the Inbox as the one tab the control cannot reach", async () => {
    /*
     * The Inbox had its own "approvals first, then SLA" and would have ignored the control entirely — the same
     * defect, surviving on a quarter of the surface. Approvals still lead, banded like pinned work.
     */
    const fn = APP.split("const renderList = ")[1].split("\n    const ")[0];
    expect(fn).toContain("const inboxSorter = SORTERS[state.sortKey] || SORTERS.sla");
    expect(fn, "approvals stopped leading").toContain("filter((i) => i.itemType === 'approval')");
  });

  it("floats pinned work, and still sorts INSIDE each band", async () => {
    /*
     * THE DECISION, since the brief asked for one: a pin means "I will come back to this", so an order that
     * buries it defeats the only thing a pin does — but silently overriding the order just chosen would be its
     * own lie. Banding answers both: pinned first, unpinned after, each band in the chosen order.
     *
     * MUTATION GUARD: drop the band split and "Zeynep" sorts to the bottom.
     */
    await boot([row(1, { title: "Ahmet" }), row(2, { title: "Berk" }), row(3, { title: "Zeynep", pinned: true })]);
    await sortBy("title");
    expect(titles()).toEqual(["Zeynep", "Ahmet", "Berk"]);
  });

  it("uses SORTERS — there is no second sorter", async () => {
    const fn = APP.split("const renderList = ")[1].split("\n    const ")[0];
    expect(fn).toContain("SORTERS[state.sortKey] || SORTERS.sla");
    // Neither branch may pin the order any more — the Inbox had its own copy.
    expect((fn.match(/SORTERS\[state\.sortKey\]/g) || []).length, "a branch stopped consulting the sorter").toBe(2);
  });
});

describe("③ one type axis, one implementation", () => {
  it("emits a single attribute for every tab's type chips", () => {
    /*
     * MUTATION GUARD: split the axis in two again and this goes red.
     *
     * The Inbox drew `data-wcn-inbox-type` and the other three drew `data-wcn-typechip` — one axis, two
     * renderers, two handlers, two copies of the count-and-hide rule.
     */
    const code = APP.replace(/\/\*[\s\S]*?\*\//g, "");
    expect(code, "the second attribute is back").not.toContain("data-wcn-inbox-type");
    expect(code).toContain('data-wcn-typechip="${esc(key)}"');
  });

  it("keeps ONE chip builder and ONE handler", () => {
    const code = APP.replace(/\/\*[\s\S]*?\*\//g, "");
    expect((code.match(/const typeChipHtml = /g) || []).length).toBe(1);
    expect((code.match(/closest\('\[data-wcn-typechip\]'\)/g) || []).length).toBe(1);
  });

  it("KEEPS the two selection modes, because they are a product decision", () => {
    /*
     * The Inbox is single-select (pick one, the rest clear, "Tümü" resets); the other tabs are multi-select
     * toggles. That difference is about two different reading tasks — so it survives as one predicate rather
     * than as two implementations.
     */
    expect(APP).toContain("const typesAreSingleSelect = () => state.tab === 'inbox'");
    const handler = APP.split("closest('[data-wcn-typechip]')")[1].split("render(); return;")[0];
    expect(handler).toContain("typesAreSingleSelect()");
    expect(handler, "the multi-select branch was lost").toContain("state.typeFilter.delete(ty)");
  });

  it("keeps Tümü drawn at zero — it clears the axis, it is not a type", () => {
    const fn = APP.split("const buildInboxChips = ")[1].split("const buildDefaultChips")[0];
    const all = fn.split("const allChip")[1].split("const mainChips")[0];
    expect(all).not.toContain("!count && !on");
  });

  it("hides a zero chip through the ONE rule that now exists", () => {
    const fn = APP.split("const typeChipHtml = ")[1].split("\n    const ")[0];
    expect(fn).toContain("if (!count && !on) { return ''; }");
  });

  it("does not change the URL vocabulary — old links keep working", () => {
    // Both paths always wrote `types=`; nothing about the parameter moved.
    expect(APP).toContain("put('types', Array.from(state.typeFilter).sort().join(','), '')");
    expect(APP).toContain("state.typeFilter = new Set((params.get('types') || '')");
  });
});
