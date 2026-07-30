const { bootSurface, app } = require("./wcn-boot");

/*
 * The WorkCenterNext LIST surface, driven through the real DOM (BL-033, narrowed).
 *
 * The detail page has had a boot harness for a while; the list — the surface people actually live in — had none.
 * Every WorkCenterNext test either called a module in isolation, scanned source text, or built #wcnApp without
 * loading app.js, so app.js's list branch (`root.dataset.wcnPage !== 'detail'`) never executed under test. That
 * is the structural reason every defect this session was found by someone looking at a screen rather than by a
 * test.
 *
 * This file weaves the net. It pins the rules that are load-bearing and currently unguarded — the axis law, the
 * counters, and which tab an item belongs to — for REAL items, since fixtures are a development surface and the
 * rules are about real work.
 *
 * NOT covered here on purpose: kanban and calendar (BL-015, deliberately last — the harness renders them only to
 * prove they do not throw), and any new product behaviour. Where this file found a defect it says so and pins the
 * current truth rather than fixing it.
 */

// ── Item builders ────────────────────────────────────────────────────────────

const ID = (n) => `98d1f94e-1848-4539-8a99-77e72651b8a${n}`;

/**
 * A REAL projection item — the shape TaskWorkItemProvider actually emits. Every rule pinned below is a rule
 * about real work, so nothing here opts into the showcase catalogue.
 */
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
  actions: [{
    code: "complete",
    label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

/** Unaccepted personal work — belongs in the Inbox until someone takes it on. */
const inboxItem = (n) => item(n, {
  admissionState: "pendingAcceptance",
  assignmentMode: "offered",
  ownershipState: "assigned",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  actions: [{
    code: "accept",
    label: { kind: "resource", key: "WorkAggregation_Action_Accept" },
    semanticType: "accept",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }]
});

/** A queue identity, as the projection now emits it (WC-3 / BL-031). */
const pool = (id, label) => ({
  id,
  label: label === null ? null : { kind: "display", text: label, locale: "und" }
});

/** Unowned pool work — nobody has claimed it. */
const poolItem = (n, poolIdentity) => item(n, Object.assign({
  pool: poolIdentity === undefined ? pool(`pos-${n}`, `Kuyruk ${n}`) : poolIdentity
}, {
  admissionState: "pendingClaim",
  assignmentMode: "groupQueue",
  ownershipState: "unowned",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  assignee: null,
  actions: [{
    code: "claim",
    label: { kind: "resource", key: "WorkAggregation_Action_Claim" },
    semanticType: "claim",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }]
}));

/** Closed work — history. A terminal item offers no state-changing action (contract rule). */
const historyItem = (n, lifecycle = "Done") => item(n, {
  normalizedStatus: lifecycle,
  taskLifecycle: lifecycle,
  executionState: "notApplicable",
  actions: []
});

/**
 * A DISABLED inline action on closed work — what the contract permits (it forbids only ENABLED inline actions on
 * a terminal item) and therefore exactly the shape that used to leak a button into History.
 */
const terminalInlineAction = () => ({
  code: "complete",
  label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
  semanticType: "complete",
  enabled: false,
  source: "provider",
  disabledReasonCode: "TERMINAL",
  disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_PermissionDenied" },
  requiresConfirmation: false,
  requiresReason: false,
  requiresEvidence: false,
  supportsBulk: false,
  riskLevel: "normal"
});

/** "Open in the source" — allowed on closed work, because reading a finished record is not acting on it. */
const terminalDeeplinkAction = () => Object.assign(terminalInlineAction(), {
  code: "openSource",
  semanticType: "openSource",
  enabled: true,
  disabledReasonCode: null,
  disabledReason: null,
  depth: "deeplink"
});

/** Parked on someone else — the "Bekleyen" segment. */
const waitingItem = (n) => item(n, {
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
});

/** Planned but not started — the "Planlı" segment. */
const plannedItem = (n) => item(n, {
  normalizedStatus: "Pending",
  taskLifecycle: "Planned",
  executionState: "notStarted"
});

// ── Harness ──────────────────────────────────────────────────────────────────

/**
 * Boots the LIST surface. The production view (Index.cshtml) sets no `data-wcn-page` at all — app.js treats
 * anything that is not "detail" as the list — so the harness omits it too rather than inventing a value the real
 * page never carries.
 */
const bootListPage = (items) => bootSurface({ rootAttrs: "", items });

const tabButton = (key) => app().querySelector(`[data-wcn-tab="${key}"]`);
const tabCount = (key) => {
  const badge = tabButton(key).querySelector(".wcn-tab-count");
  return badge ? Number(badge.textContent.trim()) : 0;
};
const segmentButtons = () => Array.from(app().querySelectorAll("[data-wcn-seg]"));
const rowIds = () => Array.from(app().querySelectorAll("[data-wcn-row]"))
  .map((el) => el.getAttribute("data-wcn-row"));

/** Clicks a tab and lets the delegated async click handler drain. */
const clickTab = async (key) => {
  tabButton(key).click();
  await new Promise((resolve) => { setTimeout(resolve, 0); });
};

/**
 * Boots and switches to "Mine". The surface deliberately LANDS on the Inbox (triage before own work — pinned
 * below), so every assertion about owned items has to move there first rather than assuming it starts there.
 */
const bootOnMine = async (items) => {
  const result = await bootListPage(items);
  await clickTab("islerim");
  return result;
};

describe("the list surface renders at all", () => {
  /*
   * NON-VACUITY, first and deliberately. Every "X is absent" assertion in this file would also pass on a page
   * that rendered nothing — which is exactly what happened while the detail harness was missing a script and its
   * assertions all passed against an error placeholder. Nothing below is trustworthy without this.
   */
  it("actually paints the list, with a row per item", async () => {
    // Inbox items, because that is the tab the surface opens on — see the landing test below.
    await bootListPage([inboxItem(1), inboxItem(2), inboxItem(3)]);

    expect(app().querySelector(".wcn-tabs")).not.toBeNull();
    expect(rowIds()).toHaveLength(3);
    expect(app().textContent).toContain("Görev 1");
  });

  it("lands on the Inbox — triage before own work", async () => {
    // Not an incidental default: the Inbox is where work arrives needing a decision, so it is what you see
    // first. It also explains why every "Mine" assertion in this file clicks there first.
    await bootListPage([inboxItem(1), item(2)]);

    expect(tabButton("inbox").getAttribute("aria-selected")).toBe("true");
    expect(tabButton("islerim").getAttribute("aria-selected")).toBe("false");
    // ...and it shows the Inbox item, not the owned one.
    expect(rowIds()).toEqual([ID(1)]);
  });

  it("boots the LIST branch, not the detail branch", async () => {
    // The surfaces are chosen by one attribute; if this harness accidentally booted the detail page, every rule
    // below would be describing the wrong screen.
    await bootListPage([item(1)]);

    expect(app().querySelector(".wcn-tabs")).not.toBeNull();
    expect(app().querySelector(".wcn-details-page")).toBeNull();
  });
});

/*
 * THE AXIS LAW (spec v3), the most basic rule this screen has and — until now — the one with no test at all:
 *   tab     = OWNERSHIP  (Inbox · Mine · Pool · History)
 *   segment = STATUS     (Active · Waiting · Planned), at most three, and only under "Mine"
 *   chip    = TYPE + SIGNAL
 * Mixing the axes is how a task-centre turns into an unreadable grid, so each axis is pinned separately.
 */
describe("the axis law: ownership is the tab, status is the segment", () => {
  it("offers exactly the four ownership tabs, in order", async () => {
    await bootListPage([item(1)]);

    const keys = Array.from(app().querySelectorAll("[data-wcn-tab]"))
      .map((el) => el.getAttribute("data-wcn-tab"));

    expect(keys).toEqual(["inbox", "islerim", "havuz", "history"]);
  });

  it("offers at most three status segments, and only under Mine", async () => {
    await bootOnMine([item(1)]);

    const segs = segmentButtons().map((el) => el.getAttribute("data-wcn-seg"));
    expect(segs).toEqual(["aktif", "bekleyen", "planli"]);
    // A fourth segment is the failure this pins: the bar is a status axis, not a dumping ground.
    expect(segs.length).toBeLessThanOrEqual(3);
  });

  it("shows no segment bar on the other three tabs", async () => {
    // Status is only a meaningful sub-division of work you OWN. On Inbox/Pool/History it would be a second
    // ownership axis wearing a status label.
    await bootListPage([inboxItem(1), poolItem(2), historyItem(3)]);

    for (const tab of ["inbox", "havuz", "history"]) {
      await clickTab(tab);
      expect(segmentButtons(), `${tab} must not carry a segment bar`).toHaveLength(0);
    }
  });
});

describe("the counters agree with what the list actually holds", () => {
  it("counts each item in exactly ONE tab", async () => {
    /*
     * The manual test document's red-flagged item: an item appearing under two tabs makes every count a guess
     * and the whole surface untrustworthy. Ownership is exclusive by construction, so the sum of the four tab
     * counters must equal the number of items — no more.
     */
    const items = [inboxItem(1), item(2), poolItem(3), historyItem(4)];
    await bootListPage(items);

    const total = ["inbox", "islerim", "havuz", "history"].reduce((sum, key) => sum + tabCount(key), 0);

    expect(total).toBe(items.length);
  });

  it("keeps an item out of its origin tab when its two status fields disagree", async () => {
    /*
     * The double-count guard, at the only place it can actually be reached.
     *
     * `tabFor` decides the tab from `normalizedStatus` alone, while `isTerminal` also consults the task
     * lifecycle. An item whose two fields disagree — normalized says InProgress, lifecycle says Done — is
     * therefore claimed by BOTH "Mine" (via item.tab) and "History" (via isTerminal), and would be counted twice.
     * That is what the `!isTerminal(item)` clause in inTab exists to stop, and no consistently-shaped fixture can
     * exercise it.
     */
    const disagreeing = item(1, {
      normalizedStatus: "InProgress",
      taskLifecycle: "Done",
      executionState: "notStarted",
      actions: []
    });
    await bootListPage([disagreeing]);

    const total = ["inbox", "islerim", "havuz", "history"].reduce((sum, key) => sum + tabCount(key), 0);

    // Once, not twice — and History wins, because "finished" is the stronger claim.
    expect(total).toBe(1);
    expect(tabCount("history")).toBe(1);
    expect(tabCount("islerim")).toBe(0);
  });

  it("makes the tab counter equal the rows that tab shows", async () => {
    await bootListPage([item(1), item(2), inboxItem(3)]);

    // Inbox first, because that is where it lands.
    expect(tabCount("inbox")).toBe(1);
    expect(rowIds()).toHaveLength(1);

    await clickTab("islerim");
    expect(tabCount("islerim")).toBe(2);
    expect(rowIds()).toHaveLength(2);
  });

  it("makes the segment counters sum to the tab counter", async () => {
    // One of each status, all owned: 1 active + 1 waiting + 1 planned = the Mine counter.
    await bootOnMine([item(1), waitingItem(2), plannedItem(3)]);

    const segTotal = segmentButtons()
      .reduce((sum, el) => sum + Number(el.querySelector(".wcn-seg-count").textContent.trim()), 0);

    expect(segTotal).toBe(tabCount("islerim"));
    expect(segTotal).toBe(3);
  });

  it("puts each status under its own segment", async () => {
    // Non-vacuity for the sum above: 3 = 3 would also hold if all three landed in one segment.
    await bootOnMine([item(1), waitingItem(2), plannedItem(3)]);

    const counts = {};
    segmentButtons().forEach((el) => {
      counts[el.getAttribute("data-wcn-seg")] = Number(el.querySelector(".wcn-seg-count").textContent.trim());
    });

    expect(counts).toEqual({ aktif: 1, bekleyen: 1, planli: 1 });
  });
});

describe("which tab an item belongs to", () => {
  it("keeps unaccepted personal work in the Inbox", async () => {
    await bootListPage([inboxItem(1)]);

    expect(tabCount("inbox")).toBe(1);
    expect(tabCount("islerim")).toBe(0);
  });

  it("keeps accepted work under Mine", async () => {
    await bootListPage([item(1)]);

    expect(tabCount("islerim")).toBe(1);
    expect(tabCount("inbox")).toBe(0);
  });

  it("keeps unowned pool work in the Pool", async () => {
    await bootListPage([poolItem(1)]);

    expect(tabCount("havuz")).toBe(1);
    expect(tabCount("islerim")).toBe(0);
  });

  it("moves closed work to History, whether it was completed or cancelled", async () => {
    await bootListPage([historyItem(1, "Done"), historyItem(2, "Cancelled")]);

    expect(tabCount("history")).toBe(2);
    // Terminal work must not also sit in the tab it came from.
    expect(tabCount("islerim")).toBe(0);
  });

  it("shows History read-only for work as the engine actually projects it", async () => {
    /*
     * A terminal task carries NO actions on the wire — the provider returns an empty set for it and the contract
     * independently rejects an enabled inline action on a Done/Cancelled item. So the closed work a user really
     * sees offers nothing to press.
     *
     * This pins the end result. What it does NOT prove is that the list itself would refuse one — see the
     * measurement below, which found that it would not.
     */
    await bootListPage([historyItem(1), historyItem(2, "Cancelled")]);
    await clickTab("history");

    expect(rowIds()).toHaveLength(2);
    expect(app().querySelectorAll("[data-wcn-action]")).toHaveLength(0);
  });

  it("renders action buttons on OPEN work, so the History assertion above means something", async () => {
    // Non-vacuity for the test above: "no action buttons" has to be a fact about History, not about this
    // selector never matching anything.
    await bootOnMine([item(1)]);

    expect(app().querySelectorAll("[data-wcn-action]").length).toBeGreaterThan(0);
  });

  it("strips a closed item's INLINE action, even when the provider sends one (BL-038)", async () => {
    /*
     * This assertion used to pin the OPPOSITE, as a measured finding: History was read-only only because
     * TaskWorkItemProvider happens to send an empty action set for terminal work, and the surface itself would
     * render a disabled button if one ever arrived. BL-038 made the rule the surface's own — it now lives in
     * getActions, the single point both surfaces read actions through.
     *
     * The action here is DISABLED, which the contract permits: it only forbids ENABLED inline actions on a
     * terminal item, so this is precisely the case that used to leak through.
     */
    const closedWithDisabledAction = historyItem(1);
    closedWithDisabledAction.actions = [terminalInlineAction()];
    await bootListPage([closedWithDisabledAction]);
    await clickTab("history");

    expect(app().querySelectorAll("[data-wcn-action]")).toHaveLength(0);
  });

  it("keeps a closed item's DEEPLINK action — opening the source is still legitimate", async () => {
    /*
     * The direction half of the rule, and the reason it is a filter rather than a blanket "no actions on closed
     * work": a finished task's source record is a real thing to want to open. Without this case, a mutation that
     * filtered EVERYTHING would pass the test above and nobody would notice.
     */
    const closedWithDeeplink = historyItem(1);
    closedWithDeeplink.actions = [terminalDeeplinkAction()];
    await bootListPage([closedWithDeeplink]);
    await clickTab("history");

    expect(app().querySelectorAll("[data-wcn-action]").length).toBeGreaterThan(0);
  });

  it("still SHOWS the closed item — the rule removes its button, never the row", async () => {
    /*
     * The reason BL-038 put this rule in getActions instead of the contract. validateItems DROPS an item that
     * fails validation, so a contract rule would have made a mis-projected task disappear from History
     * altogether. A lost task is worse than a leaked disabled button, and this codebase has already lost real
     * items exactly that way once (catalogVisible).
     */
    const closedWithDisabledAction = historyItem(1);
    closedWithDisabledAction.actions = [terminalInlineAction()];
    await bootListPage([closedWithDisabledAction]);
    await clickTab("history");

    expect(rowIds()).toEqual([ID(1)]);
    expect(tabCount("history")).toBe(1);
  });
});

/*
 * runBulk / runBulkWithProgress — MEASURED, not assumed.
 *
 * The finding: the bulk path is UNREACHABLE from the UI. `bulkBar` is defined and never called; no markup
 * anywhere emits `data-wcn-check` or `data-wcn-check-all`, so `state.tableSelected` can never be filled by a
 * user; with it empty `bulkBar` returns '' anyway, so no `data-wcn-bulk` button is ever produced and
 * performBulk → runBulkWithProgress → runBulk cannot be entered.
 *
 * This is pinned rather than fixed (the ticket asks for a report, not a repair). It matters because runBulk
 * contains a real guard — a real item is refused rather than simulated — and if selection is ever wired up, that
 * guard is what stops a bulk action from changing the screen while the database keeps the old state. These tests
 * exist so that wiring it up FAILS here first, and whoever does it re-reads that guard.
 */
describe("bulk selection is not reachable from the list (measured, not fixed)", () => {
  it("renders no row selection checkbox", async () => {
    await bootOnMine([item(1), item(2)]);

    expect(app().querySelectorAll("[data-wcn-check]")).toHaveLength(0);
    expect(app().querySelectorAll("[data-wcn-check-all]")).toHaveLength(0);
  });

  it("renders no bulk action bar", async () => {
    await bootOnMine([item(1), item(2)]);

    expect(app().querySelector(".wcn-bulkbar")).toBeNull();
    expect(app().querySelectorAll("[data-wcn-bulk]")).toHaveLength(0);
  });

  it("still renders the rows the checkboxes would have belonged to", async () => {
    // Non-vacuity: "no checkbox" must not be satisfied by an empty list.
    await bootOnMine([item(1), item(2)]);

    expect(rowIds()).toHaveLength(2);
  });
});

describe("the other views render without throwing (BL-015 stops here)", () => {
  /*
   * Kanban and Calendar are deliberately left for BL-015, so this asserts only that switching to them does not
   * blow up and take the page with it. Pinning their behaviour now would freeze a design that is about to change.
   */
  it("survives a switch to table view", async () => {
    await bootOnMine([item(1), waitingItem(2)]);

    app().querySelector('[data-wcn-view="table"]').click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(app().querySelector(".wcn-tabs")).not.toBeNull();
  });
});

/*
 * WC-3 / BL-031 — the Pool tab answers "which queue is this in".
 *
 * The projection carries `pool: { id, label }` now. Before it did, the screen filled the silence with a
 * fabricated team name for every pooled item; workcenter-next-pool-group.test.js guards that name's grave and is
 * deliberately untouched. These tests cover the replacement: the real identity, and the fact that NOTHING is
 * invented when it is missing.
 */
describe("the Pool tab names the queue each item waits in", () => {
  const groupButtons = () => Array.from(app().querySelectorAll("[data-wcn-group],[data-wcn-group-unnamed]"))
    .map((el) => el.textContent.trim());

  const bootOnPool = async (items) => {
    const result = await bootListPage(items);
    await clickTab("havuz");
    return result;
  };

  it("shows each queue as its own group — three pools do not collapse into one", async () => {
    /*
     * BL-031 (b). The fabricated label gave every pooled item the SAME group, so the tab could not tell a CFO
     * queue from an accounting one. Three real queues must stay three.
     */
    await bootOnPool([
      poolItem(1, pool("pos-cfo", "CFO — Genel Merkez")),
      poolItem(2, pool("pos-acc", "Muhasebe Müdürü — Genel Merkez")),
      poolItem(3, pool("pos-eng", "E2E Engineer — Genel Merkez"))
    ]);

    expect(rowIds()).toHaveLength(3);
    expect(groupButtons()).toEqual([
      "GroupAll", "CFO — Genel Merkez", "Muhasebe Müdürü — Genel Merkez", "E2E Engineer — Genel Merkez"
    ]);
  });

  it("filters the list down to one queue when that queue is picked", async () => {
    // Non-vacuity for the group buttons: they have to DO something, not just be rendered.
    await bootOnPool([
      poolItem(1, pool("pos-cfo", "CFO — Genel Merkez")),
      poolItem(2, pool("pos-acc", "Muhasebe Müdürü — Genel Merkez"))
    ]);

    app().querySelector('[data-wcn-group="CFO — Genel Merkez"]').click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(rowIds()).toEqual([ID(1)]);
  });

  it("invents nothing when the queue has no name — no group selector at all", async () => {
    /*
     * An unresolvable position arrives with an id and no label. The screen must show NO queue name rather than
     * a placeholder or a GUID; with nothing to tell apart, there is nothing to select between either.
     */
    await bootOnPool([poolItem(1, pool("pos-unknown", null))]);

    expect(rowIds()).toHaveLength(1);
    expect(groupButtons()).toEqual([]);
    expect(app().textContent).not.toContain("pos-unknown");
  });

  it("offers an explicit bucket for unnamed queues when named ones exist beside them", async () => {
    // Mixed: without its own bucket, the unnamed item would be reachable only through "all".
    await bootOnPool([
      poolItem(1, pool("pos-cfo", "CFO — Genel Merkez")),
      poolItem(2, pool("pos-unknown", null))
    ]);

    expect(groupButtons()).toEqual(["GroupAll", "CFO — Genel Merkez", "GroupUnnamed"]);

    app().querySelector("[data-wcn-group-unnamed]").click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(rowIds()).toEqual([ID(2)]);
  });

  it("still LISTS an item whose queue has no name — the row is never the price of a missing label", async () => {
    /*
     * Written before the contract rule exists, and kept afterwards. `validateItems` drops what it cannot
     * validate, so requiring a pool identity is one mis-projection away from making a task disappear from the
     * Pool tab. The label may be absent; the row may not.
     */
    await bootOnPool([poolItem(1, pool("pos-unknown", null)), poolItem(2, pool("pos-cfo", "CFO"))]);

    expect(rowIds()).toHaveLength(2);
    expect(tabCount("havuz")).toBe(2);
  });

  it("does not offer a queue that belongs to another tab's work", async () => {
    /*
     * A CLAIMED pool task keeps assignmentMode "groupQueue" and its pool identity — that is how it arrived — but
     * it lives under Mine now. Listing its queue in the Pool selector would offer a filter matching no pool row.
     */
    const claimed = poolItem(2, pool("pos-claimed", "Devralınmış Kuyruk"));
    claimed.admissionState = "admitted";
    claimed.ownershipState = "owned";
    claimed.assignee = { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true };
    claimed.actions = [];

    await bootOnPool([poolItem(1, pool("pos-cfo", "CFO")), claimed]);

    expect(groupButtons()).toEqual(["GroupAll", "CFO"]);
  });
});
