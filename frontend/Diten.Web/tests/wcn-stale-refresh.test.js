const { bootSurface, app } = require("./wcn-boot");

/*
 * A LATE ANSWER MUST NOT OVERWRITE A NEWER ONE (BL-188).
 *
 * REPORTED: a snooze removed with "Kaldır" came back on screen a moment later; the server was right and a page
 * reload fixed it. DIAGNOSED in the code rather than guessed: thirteen call sites re-read the projection and
 * every write refreshes through `loadWorkItems`, which assigned `state.items` and rendered with no regard for
 * whether a NEWER read had been issued in the meantime. Two overlapping reads both painted, and the one that
 * ANSWERED last won — which is not the one that ASKED last.
 *
 * The cancel path was investigated first and cleared: cancelling a dialog runs no callback, touches no state and
 * renders nothing. The stale paint never came from the dialog; it came from a read that outlived the write.
 *
 * This drives the real module through the real seam, with the two answers deliberately delivered out of order.
 */
const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const item = (snoozedUntil) => ({
  fixtureKind: "workItem",
  id: TASK_ID,
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
  title: { kind: "display", text: "Q3 nakit akış projeksiyonu", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  personal: snoozedUntil ? { snoozedUntil } : undefined
});

// Far enough ahead that it is a snooze whatever day the suite runs.
const FUTURE = new Date(Date.now() + 30 * 864e5).toISOString();

const snoozeRow = () => app().querySelector(".wcn-snooze-row");
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
const drain = async () => { for (let i = 0; i < 6; i += 1) { await settle(); } };

describe("two refreshes that overlap", () => {
  let deliver;

  beforeEach(async () => {
    await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item(FUTURE)]
    });
    global.TasksApi.setSnooze = () => Promise.resolve({ ok: true, status: 204 });

    /*
     * The seam, made deliberately unfair: each refresh gets a promise this test resolves BY HAND, so the second
     * read can answer before the first. That is the whole defect — in a browser it happens when one request is
     * simply slower than the next.
     */
    const api = global.WorkCenterNextApi;
    /*
     * The team question is asked BEFORE the projection read and is not what this test is about; left real it
     * settles on its own schedule and makes the read order impossible to reason about.
     */
    api.fetchTeamAvailability = () => Promise.resolve({ hasReports: false, count: 0 });
    const pending = [];
    api.fetchWorkItems = () => new Promise((resolve) => { pending.push(resolve); });
    global.__pending = pending;
    deliver = (index, snoozed) => {
      const mapped = api.mapPayload([item(snoozed ? FUTURE : null)]);
      pending[index]({ status: "ok", httpStatus: 200, items: mapped.items, errors: [] });
      return drain();
    };
  });

  it("shows the snooze the page was given", () => {
    // Non-vacuity: without this, every "the row is gone" assertion below would pass on an empty page.
    expect(snoozeRow()).not.toBeNull();
    // t() echoes keys in the harness, so the key is what the row carries here.
    expect(snoozeRow().textContent).toContain("SnoozeClear");
  });

  it("lets the newest answer through", async () => {
    app().querySelector(".wcn-snooze-clear").click();
    await settle();
    app().querySelector(".wcn-snooze-clear")?.click();
    await settle();

    /*
     * ⚠ WHAT THIS HARNESS CAN AND CANNOT SHOW, measured rather than assumed.
     *
     * Each click here issues TWO projection reads, not one — `wcn-boot` loads app.js afresh per boot while the
     * PREVIOUS instance's document listeners are still attached, so two module instances share one DOM and each
     * answers the click. Each instance has its own generation counter and its own single in-flight read, so
     * neither of them ever sees a stale one: the out-of-order case cannot be staged here at all.
     *
     * So this file asserts what it genuinely can — that the guard does not silence the read that SHOULD speak —
     * and the out-of-order behaviour is proven in the browser instead, where one instance owns the page: a read
     * held for 3s behind a `fetch` shim, a removal issued after it, and the held answer landing last WITHOUT
     * repainting the removed row. That measurement is in the round's report; the structural guard below is what
     * keeps the mechanism from being deleted.
     */
    const newest = global.__pending.length - 1;
    expect(newest, "no refresh was issued at all").toBeGreaterThan(0);
    await deliver(newest, false);
    expect(snoozeRow(), "the newest answer never reached the screen").toBeNull();
  });

  it("still paints when the answers arrive in order", async () => {
    // The guard must not silence the normal case: the only read in flight is always allowed to speak.
    app().querySelector(".wcn-snooze-clear").click();
    await settle();
    await deliver(global.__pending.length - 1, false);
    expect(snoozeRow()).toBeNull();
  });
});

describe("the guard is written once, for every dialog", () => {
  it("lives in the shared read, not in one caller", () => {
    const fs = require("fs");
    const path = require("path");
    const src = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
    const fn = src.slice(src.indexOf("const loadWorkItems = async"), src.indexOf("const WRITE_DEPENDENCIES"));
    expect(fn).toContain("++loadGeneration");
    // Both exits — the fixtures branch and the real one — or half the reads stay unguarded.
    expect((fn.match(/isStale\(\)/g) || []).length).toBeGreaterThanOrEqual(2);
    // Plan, inquire, reassign, checklist and notes all refresh through this one function, so they are covered by
    // the same guard rather than each growing its own.
    expect(src).toContain("afterPhase2Write");
  });
});
