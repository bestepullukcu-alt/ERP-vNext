const { bootSurface, app, SCRIPT_ROOT } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * BL-046 — a finished task stops getting later.
 *
 * THE DEFECT, IN THREE MEASURED STAGES (all of them on a live screen, none of them caught by a test):
 *   1. History read "Completed · 11 days late", and "12 days late" the next morning. Finished work does not
 *      keep slipping; the count was derived from dueAt against TODAY.
 *   2. The server half shipped alone (the state froze at closing time, the count did not) and the label read
 *      "-2 days LEFT" — unreadable, and worse than the wrong number it replaced.
 *   3. A negative guard was added, and it routed EVERY past-dated item to "late" regardless of the state the
 *      server sent. A task the server called on-track — closed on time — then read "1 day late". The screen
 *      contradicted the projection.
 *
 * The whole cure is three things that only work together, which is why they land in one slice:
 *   (a) the projection carries the CLOSING INSTANT (`closedAt`) and the contract knows the word;
 *   (b) a terminal item's day count is dueAt ↔ closedAt, with no reference to today at all;
 *   (c) the client never overrules the server's slaState on a terminal item.
 *
 * The badge is FROZEN, not removed: closing late is exactly the fact reporting wants to keep.
 */

const PINNED_NOW = () => new Date(2026, 7, 1, 10, 0, 0);   // 2026-08-01, the day the defect was measured
const A_WEEK_LATER = () => new Date(2026, 7, 8, 10, 0, 0); // the same screen, seven days on

const ID = (n) => `7c1e40aa-9b52-4d18-8f30-1a2b3c4d5e6${n}`;

/** A REAL projection item — the shape TaskWorkItemProvider emits. */
const item = (n, overrides) => Object.assign({
  fixtureKind: "workItem",
  id: ID(n),
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
  title: { kind: "display", text: `Görev ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: ID(n),
    deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-31T00:00:00+00:00"
}, overrides);

/** Closed work — the History tab. `status` is Done or Cancelled; both are terminal. */
const closedItem = (n, overrides) => item(n, Object.assign({
  normalizedStatus: "Done",
  taskLifecycle: "Done",
  executionState: "notApplicable",
  nativeStatus: { code: "Done", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Done" } }
}, overrides));

// ── Data-layer harness (no DOM): the mapper alone ────────────────────────────

const loadRealMode = () => {
  ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
    .forEach((key) => { delete global[key]; });
  global.WCN = { t: (key) => key, tf: (key) => key, tn: (key) => key };
  document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
  loadScript(SCRIPT_ROOT + "fixture-contract.js");
  loadScript(SCRIPT_ROOT + "mock-data.js");
  loadScript(SCRIPT_ROOT + "work-items-api.js");
};

const mapOne = (fixture) => global.WorkCenterNextApi.mapPayload([fixture]).items[0];

/** The same item, mapped on two different days. If the count moves, it was never frozen. */
const mapOnBothDays = (fixture) => {
  global.WorkCenterNextData.setNowProvider(PINNED_NOW);
  const today = mapOne(fixture);
  global.WorkCenterNextData.setNowProvider(A_WEEK_LATER);
  const nextWeek = mapOne(fixture);
  return { today, nextWeek };
};

describe("BL-046(a): the projection carries the closing instant, and the contract knows the word", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("KEEPS a closed item that carries closedAt", () => {
    /*
     * The BL-032 rule, restated: a field the contract has not been told about is a field that slides silently.
     * Being told about it must not cost the item its place on the surface (BL-038) — so this is asserted first.
     */
    const { items, errors } = global.WorkCenterNextApi.mapPayload([
      closedItem(1, { closedAt: "2026-07-30T14:00:00+00:00" })]);

    expect(errors).toEqual([]);
    expect(items).toHaveLength(1);
  });

  it("KEEPS a closed item whose provider sent no closedAt at all", () => {
    // MOD-0023's approval provider has no closing timestamp to give. Silence must stay legal.
    const { items, errors } = global.WorkCenterNextApi.mapPayload([closedItem(1)]);

    expect(errors).toEqual([]);
    expect(items).toHaveLength(1);
  });

  it("rejects a closing instant on work that is still open", () => {
    // Open work has not closed. A timestamp saying otherwise is a contradiction on the wire, not a nuance.
    const { errors } = global.WorkCenterNextApi.mapPayload([item(1, { closedAt: "2026-07-30T14:00:00+00:00" })]);

    expect(errors.some((e) => /CLOSED_AT_ON_OPEN_ITEM/.test(JSON.stringify(e)))).toBe(true);
  });

  it("rejects a closing instant that is not a timestamp", () => {
    const { errors } = global.WorkCenterNextApi.mapPayload([closedItem(1, { closedAt: "yakında" })]);

    expect(errors.some((e) => /CLOSED_AT_INVALID/.test(JSON.stringify(e)))).toBe(true);
  });
});

describe("BL-046(b): a closed item's day count is measured from when it closed", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("counts dueAt ↔ closedAt, not dueAt ↔ today", () => {
    // Due on the 18th, closed on the 20th: two days late, forever.
    const mapped = mapOne(closedItem(1, {
      dueAt: "2026-07-18T00:00:00+00:00",
      closedAt: "2026-07-20T16:30:00+00:00",
      slaState: "overdue"
    }));

    expect(mapped.slaDiffDays).toBe(-2);
  });

  it("does not move the count when tomorrow comes", () => {
    /*
     * THE test for this item. The screen said 11 days one morning and 12 the next; nothing about that task
     * changed overnight. "Today" is injectable precisely so this can be asserted rather than eyeballed.
     */
    const { today, nextWeek } = mapOnBothDays(closedItem(1, {
      dueAt: "2026-07-18T00:00:00+00:00",
      closedAt: "2026-07-20T16:30:00+00:00",
      slaState: "overdue"
    }));

    expect(today.slaDiffDays).toBe(-2);
    expect(nextWeek.slaDiffDays).toBe(-2);
  });

  it("still moves the count for work that is OPEN", () => {
    /*
     * Non-vacuity. Freezing everything would satisfy the test above and break the live surface: an open
     * deadline genuinely IS one day nearer tomorrow, which is why the count is derived late in the first place.
     */
    const { today, nextWeek } = mapOnBothDays(item(1, {
      dueAt: "2026-08-10T00:00:00+00:00",
      slaState: "on-track"
    }));

    expect(today.slaDiffDays).toBe(9);
    expect(nextWeek.slaDiffDays).toBe(2);
  });

  it("stops the showcase catalogue painting an on-time close as late", () => {
    /*
     * The demo surface plays the part of a server, so it has to answer this question the way one does. A History
     * fixture whose own activity log says it closed on time was still measured against the showcase's today —
     * the catalogue contradicting itself on the one screen the owner walks first.
     */
    const shown = global.WorkCenterNextData.toPresentation(
      closedItem(1, { dueAt: "2026-07-18", closedAt: "2026-07-18T16:40:00+03:00" }),
      { provenance: "fixture" });

    // Not "on-track" specifically: closing ON the due day lands inside the catalogue's own demo warning window.
    // What matters is the only distinction the badge makes about finished work — late or not.
    expect(shown.slaState).not.toBe("overdue");
    expect(shown.slaDiffDays).toBe(0);
  });

  it("still lets the showcase paint a genuinely late close as late", () => {
    // Non-vacuity: freezing the demo clock must not turn every archived fixture green.
    const shown = global.WorkCenterNextData.toPresentation(
      closedItem(1, { dueAt: "2026-07-17", closedAt: "2026-07-20T10:05:00+03:00" }),
      { provenance: "fixture" });

    expect(shown.slaState).toBe("overdue");
    expect(shown.slaDiffDays).toBe(-3);
  });

  it("leaves a closed item with no closing instant honestly unfrozen", () => {
    // Old data, and the honest failure mode: no closing timestamp means no frozen fact to state. What it must
    // NOT do is invent one — the label test below shows it stops claiming a number instead.
    const mapped = mapOne(closedItem(1, { dueAt: "2026-07-18T00:00:00+00:00", slaState: "overdue" }));

    expect(mapped.closedAt).toBeFalsy();
  });
});

// ── Render-layer harness: the words on the badge ─────────────────────────────

/*
 * The default harness echoes the resource KEY back, which is what makes an assertion name the key the code chose
 * rather than a translation. Here the ARGUMENT matters too — a frozen count is only frozen if the number baked
 * into the sentence stops moving — so this file asks for a translator that echoes both.
 */
const KEY_AND_ARGS = {
  t: (key) => key,
  tf: (key, ...args) => `${key}(${args.join(",")})`,
  tn: (key) => key
};

const bootHistory = async (items, now) => {
  const result = await bootSurface({ items, wcn: KEY_AND_ARGS, now });
  app().querySelector('[data-wcn-tab="history"]').click();
  await new Promise((resolve) => { setTimeout(resolve, 0); });
  return result;
};

const badges = () => Array.from(app().querySelectorAll(".wcn-chip, .wcn-row-chip"))
  .map((el) => el.textContent.trim());

describe("BL-046(c): the badge reports the close, and never overrules the server", () => {
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("does NOT call a task the server said was on-track late", async () => {
    /*
     * The exact pair measured on 2026-08-01: server slaState on-track, deadline in the past because the task was
     * closed before it. The screen said "1 gün gecikmiş". The projection is the authority on this question.
     */
    await bootHistory([closedItem(1, {
      dueAt: "2026-07-31T00:00:00+00:00",
      closedAt: "2026-07-31T09:00:00+00:00",
      slaState: "on-track"
    })], PINNED_NOW);

    const text = app().textContent;
    expect(text).not.toContain("SlaOverdueByDays");
    expect(text).toContain("SlaClosedOnTime");
  });

  it("does not call a CANCELLED task closed on time late either", async () => {
    // The second half of the measured pair. Cancelled is terminal too; "finished" is the claim, not "succeeded".
    await bootHistory([closedItem(1, {
      normalizedStatus: "Cancelled",
      taskLifecycle: "Cancelled",
      nativeStatus: { code: "Cancelled", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Cancelled" } },
      dueAt: "2026-07-30T00:00:00+00:00",
      closedAt: "2026-07-29T11:00:00+00:00",
      slaState: "on-track"
    })], PINNED_NOW);

    expect(app().textContent).not.toContain("SlaOverdueByDays");
  });

  it("KEEPS the late badge on work that genuinely closed late, with the frozen count", async () => {
    // Not deleted — frozen. A late close is exactly what reporting reads History for.
    await bootHistory([closedItem(1, {
      dueAt: "2026-07-18T00:00:00+00:00",
      closedAt: "2026-07-20T16:30:00+00:00",
      slaState: "overdue"
    })], PINNED_NOW);

    expect(badges().join(" | ")).toContain("SlaClosedLateByDays(2)");
  });

  it("says the same thing a week later", async () => {
    // Same item, same screen, later day. The sentence — number included — must be identical.
    const late = closedItem(1, {
      dueAt: "2026-07-18T00:00:00+00:00",
      closedAt: "2026-07-20T16:30:00+00:00",
      slaState: "overdue"
    });

    await bootHistory([late], PINNED_NOW);
    const onTheDay = badges().join(" | ");

    await bootHistory([late], A_WEEK_LATER);
    const aWeekOn = badges().join(" | ");

    expect(onTheDay).toContain("SlaClosedLateByDays(2)");
    expect(aWeekOn).toBe(onTheDay);
  });

  it("states lateness WITHOUT a count when nothing said when it closed", async () => {
    // The honest fallback: a number derived from today would be the original defect wearing a new label.
    await bootHistory([closedItem(1, {
      dueAt: "2026-07-18T00:00:00+00:00",
      slaState: "overdue"
    })], PINNED_NOW);

    const text = app().textContent;
    expect(text).toContain("SlaClosedLate");
    expect(text).not.toContain("SlaClosedLateByDays");
  });

  it("never writes '…days left' about finished work", async () => {
    // The stage-2 regression, pinned so it cannot come back through any branch.
    await bootHistory([
      closedItem(1, { dueAt: "2026-07-18T00:00:00+00:00", closedAt: "2026-07-20T16:30:00+00:00", slaState: "overdue" }),
      closedItem(2, { dueAt: "2026-07-31T00:00:00+00:00", closedAt: "2026-07-31T09:00:00+00:00", slaState: "on-track" })
    ], PINNED_NOW);

    expect(app().textContent).not.toContain("SlaDueInDays");
  });
});

describe("BL-046: OPEN work still reads as a live countdown", () => {
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("keeps saying how many days are left on work in flight", async () => {
    /*
     * Non-vacuity for the whole render block. If the terminal branch had swallowed every item, every assertion
     * above would pass on a surface that had stopped saying anything at all.
     */
    await bootSurface({
      items: [item(1, {
        admissionState: "pendingAcceptance",
        assignmentMode: "offered",
        ownershipState: "assigned",
        normalizedStatus: "Pending",
        taskLifecycle: "Open",
        executionState: "notStarted",
        dueAt: "2026-08-06T00:00:00+00:00",
        slaState: "on-track"
      })],
      wcn: KEY_AND_ARGS,
      now: PINNED_NOW
    });

    expect(app().textContent).toContain("SlaDueInDays(5)");
  });
});
