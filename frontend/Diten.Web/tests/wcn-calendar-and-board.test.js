const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE LIST PAGE'S FOURTH ROUND — the two view modes that were arranging the work wrongly.
 *
 *   ① (moved) the calendar — now the shared component, see wcn-calendar-view.test.js
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

/*
 * ① WAS THE HAND-WRITTEN MONTH GRID (BL-256). It is gone (WP-UI-CALENDAR-VIEW-01): every tab's calendar is the
 * shared component now, and what this section proved — navigation, the month in the URL, the view's languages —
 * is proved against the real FullCalendar in wcn-calendar-view.test.js.
 */

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

  it("draws Tamamlandı/İptal as thin, cardless drop zones rather than leaving them out", async () => {
    /*
     * MEASURED, not assumed: `inTab` sorts terminal work into History and non-terminal work everywhere else,
     * so Done and Cancelled CANNOT hold a CARD on İşlerim — that has not changed. What changed
     * (WP-WCN-KANBAN-01 Dilim 2, owner decision 2026-09-16/17) is that the two columns are drawn anyway, narrow
     * and body-less, as the landing target Dilim 3's drag wires up: dropping a card here moves it to Geçmiş.
     * This used to assert exactly 3 columns (Pending/In Progress/Waiting) and nothing for Done/Cancelled at
     * all — that was the promise-of-a-population rule BEFORE a drop target existed to need one.
     */
    await boot(MIXED);
    await view("kanban");
    const cols = [...app().querySelectorAll(".wcn-kcol")];
    expect(cols.length, "İşlerim draws three real stages plus two drop zones").toBe(5);
    const dropZones = [...app().querySelectorAll(".wcn-kcol-dropzone")];
    expect(dropZones.length).toBe(2);
    // The harness's `t` echoes the resource key back (no real translation loaded) — so the assertion checks the
    // KEYS, the same thing every other label assertion in this file checks.
    const dropZoneLabels = dropZones.map((el) => el.querySelector(".wcn-kcol-head span").textContent.trim());
    expect(dropZoneLabels).toEqual(["StatusDone", "StatusCancelled"]);
    // A drop zone is cardless: never a card, never a count badge (it would always read 0). WP-WCN-KANBAN-01
    // Dilim 5 draws one thing inside the body now — the idle "drop here" hint — so the body is no longer
    // literally empty, but it is still exactly ONE element and that element is the hint, not a card.
    dropZones.forEach((el) => {
      const body = el.querySelector(".wcn-kcol-body");
      expect(body, "a drop zone needs a body for Sortable to bind to").not.toBeNull();
      expect(body.children.length, "only the idle hint lives here, never a second element").toBe(1);
      expect(body.querySelector(".wcn-kcol-drophint")).not.toBeNull();
      expect(el.querySelector(".wcn-kcard")).toBeNull();
      expect(el.querySelector(".wcn-kcol-count")).toBeNull();
    });
    expect(APP).toContain("const TERMINAL_STATES = ['Done', 'Cancelled']");
  });

  it("Havuz keeps today's shape — no terminal columns at all", async () => {
    // The drop-zone policy is İşlerim/Başlattıklarım only; Havuz is untouched (owner decision, same source).
    // `tabFor` (mock-data.js) routes a row to Havuz only via admissionState pendingClaim/pendingOffer — a plain
    // `row()` (direct assignment, admitted) lands on İşlerim, so the pool needs its own fixtures here.
    const pool = (n, status) => Object.assign(row(n, { status }), {
      assignmentMode: "groupQueue", admissionState: "pendingClaim",
      pool: { id: `pos-${n}`, label: { kind: "display", text: `Kuyruk ${n}`, locale: "und" } }
    });
    const MIXED_POOL = [pool(11, "Pending"), pool(12, "InProgress"), pool(13, "InProgress")];
    await bootSurface({ rootAttrs: 'data-wcn-page="list"', items: MIXED_POOL, now: () => new Date(TODAY + "T12:00:00") });
    app().querySelector('[data-wcn-tab="havuz"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await view("kanban");
    expect([...app().querySelectorAll(".wcn-kcol")].length).toBe(3);
    expect(app().querySelectorAll(".wcn-kcol-dropzone").length).toBe(0);
  });

  it("falls back to the empty-state sentence when every reachable stage is empty", () => {
    // Five headings over five empty boxes is not an answer; the product's own sentence is.
    const fn = APP.split("const renderKanban = ")[1].split("\n    const ")[0];
    expect(fn).toContain("if (!cols.some((col) => col.items.length)) { return emptyState(); }");
  });

  /*
   * ── THE SEGMENT DOES NOT FILTER THE BOARD (WP-WCN-KANBAN-01 Dilim 2, CT 2026-09-17) ────────────────────────
   *
   * Superseded "keeps the segment as a FILTER — it just stops deciding the columns": that sentence was true of
   * the BL-257 rule (segment still narrowed `activeItems()`, only stopped choosing the columns) and is false of
   * this one. A source-text grep for `const items = activeItems();` cannot tell the two rules apart — both call
   * that line — so this asserts the actual DOM difference instead: a Bekletiliyor item the "Aktif" segment would
   * hide from the LIST is still a CARD on the board, in its real column, count included.
   */
  it("does not filter the board by segment, even though the list still does", async () => {
    const waitingItem = Object.assign(row(4, { due: "2026-08-13T00:00:00+00:00", status: "Waiting" }), {
      waitingContext: { type: "externalInformation", waitingOn: null }
    });
    const withWaiting = MIXED.concat([waitingItem]);
    await boot(withWaiting);
    // Default segment on a fresh İşlerim boot is "Aktif" (state.segment initializes to 'aktif'), which excludes
    // Waiting (segmentFor: normalizedStatus === 'Waiting' → 'bekleyen') — MUTATION GUARD for the list half below.
    expect(app().querySelector('[data-wcn-seg="aktif"]').classList.contains("active")).toBe(true);

    await view("list");
    expect(app().querySelector('.wcn-row[data-wcn-row="cccccccc-0000-0000-0000-000000000004"]'),
      "the Aktif segment did not narrow the list").toBeNull();

    await view("kanban");
    const waitingCard = app().querySelector(
      '.wcn-kcol-body .wcn-kcard[data-wcn-row="cccccccc-0000-0000-0000-000000000004"]');
    expect(waitingCard, "the board applied the segment filter and hid a real column's card").not.toBeNull();
    const waitingCol = waitingCard.closest(".wcn-kcol");
    expect(waitingCol.querySelector(".wcn-kcol-head span").textContent.trim()).toBe("StatusWaiting");
    expect(Number(waitingCol.querySelector(".wcn-kcol-count").textContent)).toBe(1);
  });

  it("hides the segment chips in the board view, and restores them in the list view", async () => {
    await boot(MIXED);
    expect(app().querySelector(".wcn-segments"), "İşlerim's list view lost its segment bar").not.toBeNull();

    await view("kanban");
    expect(app().querySelector(".wcn-segments"), "the segment chips survived into the board view").toBeNull();

    await view("list");
    expect(app().querySelector(".wcn-segments"), "the segment bar did not come back in the list view")
      .not.toBeNull();
  });

  it("Başlattıklarım draws the same two cardless, countless drop zones as İşlerim", async () => {
    // tabFor (mock-data.js) routes a row to Başlattıklarım only via viewerRelation "initiator".
    const initiated = (n, status) => Object.assign(row(n, { status }), { viewerRelation: "initiator" });
    const MIXED_OUTBOX = [initiated(21, "Pending"), initiated(22, "InProgress")];
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items: MIXED_OUTBOX, now: () => new Date(TODAY + "T12:00:00")
    });
    app().querySelector('[data-wcn-tab="baslattiklarim"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await view("kanban");

    const dropZones = [...app().querySelectorAll(".wcn-kcol-dropzone")];
    expect(dropZones.length).toBe(2);
    const labels = dropZones.map((el) => el.querySelector(".wcn-kcol-head span").textContent.trim());
    expect(labels).toEqual(["StatusDone", "StatusCancelled"]);
    dropZones.forEach((el) => {
      const body = el.querySelector(".wcn-kcol-body");
      expect(body, "a drop zone needs a body for Sortable to bind to").not.toBeNull();
      expect(body.children.length, "only the idle hint lives here, never a second element").toBe(1);
      expect(body.querySelector(".wcn-kcol-drophint")).not.toBeNull();
      expect(el.querySelector(".wcn-kcard")).toBeNull();
      expect(el.querySelector(".wcn-kcol-count")).toBeNull();
    });
  });

  it("shows the read-only note on Geçmiş's board and nowhere else", async () => {
    await boot(MIXED);
    await view("kanban");
    expect(app().querySelector(".wcn-viewnote"), "İşlerim's board is not read-only, it has no drag yet — "
      + "but it is not History's ARCHIVE either, and the note must not claim it is").toBeNull();

    // A terminal item carries no state-changing action — the contract refuses one (TERMINAL_STATE_CHANGING_ACTION).
    const terminal = (n, status) => Object.assign(row(n, { status }), {
      actions: [], primaryActionCode: null, overflowActionCodes: []
    });
    const closed = [terminal(31, "Done"), terminal(32, "Cancelled")];
    await bootSurface({
      rootAttrs: 'data-wcn-page="list"', items: closed, now: () => new Date(TODAY + "T12:00:00")
    });
    app().querySelector('[data-wcn-tab="history"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await view("kanban");
    const note = app().querySelector(".wcn-viewnote");
    expect(note, "Geçmiş's board lost its read-only note").not.toBeNull();
    expect(note.textContent).toContain("KanbanReadonly");
  });
});
