const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE LIST PAGE'S FOURTH ROUND — the two view modes that were arranging the work wrongly.
 *
 *   ① the calendar drew one month, offered no way out of it, and said nothing about the rest
 *   ② the board changed shape with the tab: lifecycle columns on some, a single segment column on others
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const id = (n) => `cccccccc-0000-0000-0000-${String(n).padStart(12, "0")}`;
const row = (n, { due, status = "InProgress", planned = null } = {}) => ({
  fixtureKind: "workItem",
  id: id(n),
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: status,
  /*
   * ⚠ NOT THE SAME VOCABULARY. `normalizedStatus` is Pending|InProgress|Waiting|Done|Cancelled; `taskLifecycle`
   * spells the pending state "Open". Copying one into the other passed review by eye and was refused by the
   * contract on the first run — which is the contract doing its job.
   */
  taskLifecycle: status === "Pending" ? "Open" : status,
  executionState: status === "InProgress" ? "active" : "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: `Satır ${n}`, locale: "und" },
  nativeStatus: { code: status, label: { kind: "display", text: status, locale: "und" } },
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
  dueAt: due,
  plannedDate: planned,
  slaState: "no-sla"
});

/*
 * "Today" is pinned so the calendar's default month is a fact of the test rather than of the wall clock.
 * ⚠ The provider yields a DATE OBJECT, not an ISO string — `localIsoDate` calls `getFullYear()` on it. Passing
 * the string looked right and failed on the first run.
 */
const TODAY = "2026-08-25";
const boot = async (items) => {
  const r = await bootSurface({
    rootAttrs: 'data-wcn-page="list"', items, now: () => new Date(TODAY + "T12:00:00")
  });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await new Promise((x) => setTimeout(x, 0));
  return r;
};
const view = async (v) => {
  app().querySelector(`[data-wcn-view="${v}"]`).click();
  await new Promise((x) => setTimeout(x, 0));
};
const click = async (sel) => {
  app().querySelector(sel).click();
  await new Promise((x) => setTimeout(x, 0));
};

const THIS_MONTH = [row(1, { due: "2026-08-10T00:00:00+00:00" }), row(2, { due: "2026-08-20T00:00:00+00:00" })];
const NEXT_MONTH = [row(3, { due: "2026-09-05T00:00:00+00:00" }), row(4, { due: "2026-09-06T00:00:00+00:00" }),
  row(5, { due: "2026-09-07T00:00:00+00:00" })];

describe("① the calendar can be navigated, and says what is off-screen", () => {
  it("opens on the month we are in", async () => {
    await boot(THIS_MONTH.concat(NEXT_MONTH));
    await view("calendar");
    expect(app().querySelectorAll(".wcn-cal-item").length, "the default month is not today's").toBe(2);
    // "Today" is where we already are, so the control that goes there is off.
    expect(app().querySelector('[data-wcn-cal-month="today"]').disabled).toBe(true);
  });

  it("moves a month at a time, and comes back", async () => {
    await boot(THIS_MONTH.concat(NEXT_MONTH));
    await view("calendar");
    // MUTATION GUARD: remove the month control and this goes red on the missing button.
    await click('[data-wcn-cal-month="next"]');
    expect(app().querySelectorAll(".wcn-cal-item").length, "next month is not reachable").toBe(3);
    await click('[data-wcn-cal-month="prev"]');
    expect(app().querySelectorAll(".wcn-cal-item").length).toBe(2);
  });

  it("carries the month in the URL, and leaves the default out of it", async () => {
    await boot(THIS_MONTH.concat(NEXT_MONTH));
    await view("calendar");
    const month = () => new URL(global.location.href).searchParams.get("month");
    expect(month(), "the default month cluttered the URL").toBeNull();
    await click('[data-wcn-cal-month="next"]');
    expect(month()).toBe("2026-09");
    await click('[data-wcn-cal-month="today"]');
    expect(month(), "returning to today left the parameter behind").toBeNull();
  });

  it("SAYS how many items are in other months — even though the arrows exist", async () => {
    /*
     * MEASURED before any of this: 30 items in the list, 6 in the grid, and nothing on screen about the other
     * 24. Navigation answers "let me look"; this answers "is there anything to look for", and a reader should
     * not have to click through months to find out.
     *
     * MUTATION GUARD: drop the sentence and this goes red.
     */
    await boot(THIS_MONTH.concat(NEXT_MONTH));
    await view("calendar");
    const said = app().querySelector(".wcn-cal-outside");
    expect(said, "the calendar went quiet about the rest of the list").not.toBeNull();
    expect(said.textContent).toContain("CalOutside");
  });

  it("counts ITEMS, not entries — one task can occupy two days", async () => {
    /*
     * A task with a personal plan on a different day draws TWICE (due + plan, legended apart). The sentence
     * counts the things, so "shown + elsewhere" equals the list. Measured live: July drew 4 entries for 3
     * items, and 3 + 27 = 30 = the list.
     */
    await boot([row(1, { due: "2026-08-10T00:00:00+00:00", planned: "2026-08-12" })].concat(NEXT_MONTH));
    await view("calendar");
    expect(app().querySelectorAll(".wcn-cal-item").length, "the plan marker is not drawn").toBe(2);
    const ids = [...app().querySelectorAll(".wcn-cal-item")].map((e) => e.getAttribute("data-wcn-row"));
    expect(new Set(ids).size).toBe(1);
  });

  it("names an undated item separately, because no navigation can ever reach it", () => {
    /*
     * MEASURED at ZERO on live data — every task carries a `dueAt` — which is precisely why it is a condition
     * in the code rather than an assumption. A task without a date cannot appear on any month.
     */
    const cal = APP.split("const renderCalendar = ")[1].split("\n    const ")[0];
    expect(cal).toContain("const dateless = items.filter((i) => !dateOf(i)).length");
    expect(cal).toContain("CalOutsideAndUndated");
  });

  it("speaks all of it in seven languages", () => {
    ["CalPrevMonth", "CalNextMonth", "CalToday", "CalOutside", "CalOutsideAndUndated"].forEach((key) => {
      LANGS.forEach((lang) => {
        const resx = fs.readFileSync(
          web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
        expect(resx, `${lang} is missing ${key}`).toContain(`name="${key}"`);
      });
    });
  });
});

describe("② the board's columns are the flow, on every tab", () => {
  const MIXED = [
    row(1, { due: "2026-08-10T00:00:00+00:00", status: "Pending" }),
    row(2, { due: "2026-08-11T00:00:00+00:00", status: "InProgress" }),
    row(3, { due: "2026-08-12T00:00:00+00:00", status: "InProgress" })
  ];

  it("draws lifecycle columns where it used to draw one segment column", async () => {
    /*
     * MEASURED on İşlerim: a single "Aktif 30" column — a board that is a list with extra furniture. A board
     * whose shape changes with the tab is two boards under one name.
     *
     * MUTATION GUARD: make the segment decide the columns again and this goes red.
     */
    await boot(MIXED);
    await view("kanban");
    const heads = [...app().querySelectorAll(".wcn-kcol-head span")].map((e) => e.textContent.trim());
    expect(heads.length, "the board collapsed back to one column").toBeGreaterThan(1);
    expect(APP, "the segment decides the columns again")
      .not.toContain("cols = [{ label: t(SEGMENT_KEY[state.segment]), items }]");
  });

  it("orders them by the flow, not alphabetically", async () => {
    await boot(MIXED);
    await view("kanban");
    const counts = [...app().querySelectorAll(".wcn-kcol-count")].map((e) => Number(e.textContent));
    // Pending(1) → In Progress(2) → Waiting(0): the order work moves in, which is the only reason to prefer a
    // board over a list.
    expect(counts).toEqual([1, 2, 0]);
    expect(APP).toContain("const FLOW = ['Pending', 'In Progress', 'Waiting', 'Done', 'Cancelled']");
  });

  it("draws a stage that is empty but POSSIBLE, so the flow stays readable", async () => {
    await boot(MIXED);
    await view("kanban");
    const heads = [...app().querySelectorAll(".wcn-kcol-count")].map((e) => Number(e.textContent));
    expect(heads, "an empty-but-reachable stage was dropped").toContain(0);
  });

  it("leaves out a stage this tab can never reach", async () => {
    /*
     * MEASURED, not assumed: `inTab` sorts terminal work into History and non-terminal work everywhere else,
     * so Done and Cancelled CANNOT occur on İşlerim. Drawing all five everywhere would put two permanently
     * empty columns on every board — the same promise-of-a-population this session removed from the chips and
     * the table's columns.
     */
    await boot(MIXED);
    await view("kanban");
    expect([...app().querySelectorAll(".wcn-kcol")].length, "impossible stages were drawn").toBe(3);
    expect(APP).toContain("const TERMINAL_STATES = ['Done', 'Cancelled']");
  });

  it("falls back to the empty-state sentence when every reachable stage is empty", () => {
    // Five headings over five empty boxes is not an answer; the product's own sentence is.
    const fn = APP.split("const renderKanban = ")[1].split("\n    const ")[0];
    expect(fn).toContain("if (!cols.some((col) => col.items.length)) { return emptyState(); }");
  });

  it("keeps the segment as a FILTER — it just stops deciding the columns", async () => {
    // `activeItems()` has already narrowed to the segment; the board arranges THAT by stage.
    const fn = APP.split("const renderKanban = ")[1].split("\n    const ")[0];
    expect(fn).toContain("const items = activeItems();");
  });
});
