const { bootSurface, app } = require("./wcn-boot");

/*
 * BL-045 — the chip counter and the segment counters stop describing different populations.
 *
 * THE DEFECT (measured live): the "SLA riski" chip said 3; clicking it filtered the list down to 2 rows. The
 * third was in the Bekleyen segment, and the segment counters — Aktif 9 · Bekleyen 1 · Planlı 1 — did not move
 * when the chip went on. The reader saw a 3, could reach 2, and had nothing on screen telling them where the
 * third one went.
 *
 * THE DECISION (CT, unchanged across two rounds): recompute the SEGMENT counters under the active chip — faceted
 * search, the behaviour every serious worklist has. The rejected alternative was narrowing the CHIP count to the
 * active segment: a signal is an axis independent of status, and folding it under status breaks the axis law in
 * the signal's disfavour.
 *
 * So the rule below is deliberately NOT symmetric, and the asymmetry is the product decision:
 *   segment counters  → recomputed under every OTHER axis (chips, search, advanced filters)
 *   chip counters     → recomputed under every other axis EXCEPT the segment, which they stay independent of
 *
 * All three counter paths (segment, type, signal) — plus the "Tümü" chip that belongs to the type axis — move
 * together. This was left undone twice because a HALF-faceted counter only moves the inconsistency somewhere
 * else, which is the same lesson BL-046 taught twice on a live screen.
 */

const ID = (n) => `4a8c2f61-3d70-4b95-9e18-5c7d0e2f9b1${n}`;

/** A REAL projection item, owned and active — the "Mine · Aktif" cell. */
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
  dueAt: "2026-08-20T00:00:00+00:00",
  slaState: "on-track"
}, overrides);

/** Parked on someone else — the Bekleyen segment. */
const waiting = (n, overrides) => item(n, Object.assign({
  normalizedStatus: "Waiting",
  taskLifecycle: "Waiting",
  executionState: "paused",
  waitingContext: {
    type: "approval",
    waitingOn: { id: "cccccccc-cccc-cccc-cccc-cccccccccccc", isCurrentUser: false },
    reason: null,
    since: "2026-07-25T09:00:00+00:00",
    expectedUntil: null
  }
}, overrides || {}));

/** Scheduled, not started — the Planlı segment. */
const planned = (n, overrides) => item(n, Object.assign({
  normalizedStatus: "Pending",
  taskLifecycle: "Planned",
  executionState: "notStarted"
}, overrides || {}));

/** Carries the "SLA riski" signal, whatever segment it sits in. */
const atRisk = { slaState: "overdue", dueAt: "2026-07-01T00:00:00+00:00" };

// ── Harness ──────────────────────────────────────────────────────────────────

const settle = () => new Promise((resolve) => { setTimeout(resolve, 0); });

const bootOnMine = async (items) => {
  const result = await bootSurface({ items });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await settle();
  return result;
};

const segmentCounts = () => {
  const counts = {};
  app().querySelectorAll("[data-wcn-seg]").forEach((el) => {
    counts[el.getAttribute("data-wcn-seg")] = Number(el.querySelector(".wcn-seg-count").textContent.trim());
  });
  return counts;
};

const chipCount = (selector) => {
  const el = app().querySelector(selector);
  return el ? Number(el.querySelector(".wcn-fchip-count").textContent.trim()) : null;
};

const signalChipCount = (sig) => chipCount(`[data-wcn-sigchip="${sig}"]`);
const typeChipCount = (ty) => chipCount(`[data-wcn-typechip="${ty}"]`);

const rowCount = () => app().querySelectorAll("[data-wcn-row]").length;

const clickSignalChip = async (sig) => {
  app().querySelector(`[data-wcn-sigchip="${sig}"]`).click();
  await settle();
};

const clickSegment = async (seg) => {
  app().querySelector(`[data-wcn-seg="${seg}"]`).click();
  await settle();
};

/** The measured population: 2 at-risk active, 1 at-risk waiting, plus calm work in every segment. */
const THE_MEASURED_SHAPE = () => [
  item(1, atRisk),
  item(2, atRisk),
  waiting(3, atRisk),
  item(4),
  waiting(5),
  planned(6)
];

describe("BL-045: the chip's number and the segment numbers describe the same population", () => {
  it("reproduces the shape that was measured: a signal hiding in another segment", async () => {
    /*
     * NON-VACUITY FIRST. Every claim below is about a third at-risk item sitting outside the active segment; if
     * the fixture did not actually create that situation, the whole file would pass on an empty premise.
     */
    await bootOnMine(THE_MEASURED_SHAPE());

    expect(signalChipCount("sla-risk")).toBe(3);
    // The Aktif segment is where the surface lands, and it can only ever show two of them.
    expect(rowCount()).toBe(3);
  });

  it("recomputes the segment counters when the chip is on", async () => {
    /*
     * THE item. Chip says 3, Aktif shows 2 — and the segment bar must now say where the third one is, instead of
     * repeating the unfiltered 4 · 2 · 1 and leaving the reader to guess.
     */
    await bootOnMine(THE_MEASURED_SHAPE());
    expect(segmentCounts()).toEqual({ aktif: 3, bekleyen: 2, planli: 1 });

    await clickSignalChip("sla-risk");

    expect(segmentCounts()).toEqual({ aktif: 2, bekleyen: 1, planli: 0 });
  });

  it("makes the segment counters add up to the chip's number", async () => {
    // The reader's actual arithmetic: "SLA riski 3 — 2 burada, 1 Bekleyen'de".
    await bootOnMine(THE_MEASURED_SHAPE());
    await clickSignalChip("sla-risk");

    const counts = segmentCounts();
    const total = Object.values(counts).reduce((sum, n) => sum + n, 0);

    expect(total).toBe(signalChipCount("sla-risk"));
    expect(total).toBe(3);
  });

  it("makes the active segment's counter equal the rows on screen", async () => {
    // The original complaint, stated as an invariant: the number you are standing on is the list you can see.
    await bootOnMine(THE_MEASURED_SHAPE());
    await clickSignalChip("sla-risk");

    expect(segmentCounts().aktif).toBe(rowCount());

    await clickSegment("bekleyen");
    expect(segmentCounts().bekleyen).toBe(rowCount());
  });

  it("puts the counters back when the chip goes off", async () => {
    await bootOnMine(THE_MEASURED_SHAPE());
    await clickSignalChip("sla-risk");
    await clickSignalChip("sla-risk");

    expect(segmentCounts()).toEqual({ aktif: 3, bekleyen: 2, planli: 1 });
  });
});

describe("BL-045: the signal stays an axis of its own", () => {
  it("does NOT narrow the chip's number to the active segment", async () => {
    /*
     * The REJECTED alternative, pinned so it cannot arrive later as a "consistency" fix. Standing in Aktif, the
     * chip must still say 3 — that is the whole reason the reader knows to look elsewhere.
     */
    await bootOnMine(THE_MEASURED_SHAPE());
    await clickSignalChip("sla-risk");

    expect(signalChipCount("sla-risk")).toBe(3);

    await clickSegment("bekleyen");
    expect(signalChipCount("sla-risk")).toBe(3);
  });

  it("keeps the type chips segment-independent too", async () => {
    // Type is the same axis class as signal. Six tasks in three segments: the chip says six from anywhere.
    await bootOnMine(THE_MEASURED_SHAPE());

    expect(typeChipCount("task")).toBe(6);

    await clickSegment("planli");
    expect(typeChipCount("task")).toBe(6);
  });
});

describe("BL-045: all three counter paths move together", () => {
  it("recomputes the type chip under an active signal", async () => {
    /*
     * The third feed. Left alone, it would say "task 6" while the list under an active SLA chip holds 3 — the
     * same lie this item is about, moved one chip to the right.
     */
    await bootOnMine(THE_MEASURED_SHAPE());
    expect(typeChipCount("task")).toBe(6);

    await clickSignalChip("sla-risk");

    expect(typeChipCount("task")).toBe(3);
  });

  it("recomputes the counters under the search box as well", async () => {
    // Search is an in-tab filter like any other. A counter that ignores it is stale for exactly the same reason.
    await bootOnMine(THE_MEASURED_SHAPE());

    const search = app().querySelector("[data-wcn-search]");
    search.value = "Görev 3";
    search.dispatchEvent(new global.Event("input", { bubbles: true }));
    // The search box is debounced (180ms) before it re-renders — settling on the microtask queue alone would
    // assert against the pre-search DOM and pass for the wrong reason.
    await new Promise((resolve) => { setTimeout(resolve, 250); });

    expect(segmentCounts()).toEqual({ aktif: 0, bekleyen: 1, planli: 0 });
    expect(typeChipCount("task")).toBe(1);
  });

  it("leaves a signal chip counting its OWN axis unfiltered", async () => {
    /*
     * Faceted rule, stated: a facet's own counter never applies its own filter — otherwise every active chip
     * would count only itself and the reader could never see what turning it off would restore.
     */
    await bootOnMine(THE_MEASURED_SHAPE());
    await clickSignalChip("sla-risk");

    expect(signalChipCount("sla-risk")).toBe(3);
  });
});

describe("BL-045: the tab counters stay tab-scoped", () => {
  it("does not let an in-tab chip change what the tab badge says", async () => {
    /*
     * The line the comment at app.js:343 was really protecting, and it is still true: a tab badge is a claim
     * about that tab's whole load, and it is read from OTHER tabs, where the current tab's chip means nothing.
     */
    await bootOnMine(THE_MEASURED_SHAPE());
    const badge = () => Number(
      app().querySelector('[data-wcn-tab="islerim"] .wcn-tab-count').textContent.trim());

    expect(badge()).toBe(6);

    await clickSignalChip("sla-risk");

    expect(badge()).toBe(6);
  });
});
