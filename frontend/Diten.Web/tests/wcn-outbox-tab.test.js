const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ BL-016 · "AHMET'E ATADIĞIM GÖREVİ NEREDE GÖRÜRÜM?" — THE SHELL'S HALF ═════════════════════════════════
 *
 * The provider's half is pinned in TaskOutboxTests.cs: what the three reads return, and which actions survive.
 * This file pins what the READER gets — that a row marked `viewerRelation: "initiator"` lands in the fifth
 * ownership tab, that nothing else does, and that the tab does not offer an act the reader cannot perform.
 *
 * ⚠ EVERY ASSERTION HERE HAS A NON-VACUITY PARTNER. "The row is not in Havuz" is trivially true of a page that
 * rendered nothing, and this repository has already shipped guards that stayed green while seeing almost
 * nothing. So each negative is paired with the positive that says where the row DID go, by id.
 */

const ID = (n) => `4c1a77b2-88fe-4a6e-9a52-1b0e5c73d0a${n}`;

/** A REAL projection item — the shape TaskWorkItemProvider emits, not a showcase fixture. */
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
  actions: [action("complete")],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

function action(code, overrides) {
  return Object.assign({
    code,
    label: { kind: "resource", key: `WorkAggregation_Action_${code}` },
    semanticType: code,
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }, overrides);
}

/**
 * Guard (a)'s subject — work the reader OPENED that somebody else is carrying, exactly as the provider projects
 * it: the reader is the requester, is NOT the assignee, and the row carries the two requester acts and no more.
 */
const initiatedItem = (n, overrides) => item(n, Object.assign({
  viewerRelation: "initiator",
  requester: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  assignee: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  actions: [action("reassign", { requiresReason: true }),
    action("cancel", { requiresConfirmation: true, riskLevel: "destructive" })],
  primaryActionCode: "reassign",
  overflowActionCodes: ["cancel"]
}, overrides));

/** Unclaimed pool work — nobody holds it, and the reader may take it. */
const poolItem = (n) => item(n, {
  pool: { id: `pos-${n}`, label: { kind: "display", text: `Kuyruk ${n}`, locale: "und" } },
  admissionState: "pendingClaim",
  assignmentMode: "groupQueue",
  ownershipState: "unowned",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  assignee: null,
  actions: [action("claim")]
});

// ── harness ──────────────────────────────────────────────────────────────────

const OUTBOX = "baslattiklarim";
const tabButton = (key) => app().querySelector(`[data-wcn-tab="${key}"]`);
const rowIds = () => Array.from(app().querySelectorAll("[data-wcn-row]"))
  .map((el) => el.getAttribute("data-wcn-row"));
const actionCodes = () => Array.from(app().querySelectorAll("[data-wcn-action]"))
  .map((el) => el.getAttribute("data-wcn-action"));

const clickTab = async (key) => {
  tabButton(key).click();
  await new Promise((resolve) => { setTimeout(resolve, 0); });
};

const bootOn = async (tab, items) => {
  await bootSurface({ rootAttrs: "", items });
  await clickTab(tab);
};

// ── non-vacuity, first ───────────────────────────────────────────────────────

describe("the Outbox tab exists and paints", () => {
  it("is the fifth ownership tab and carries a resource-keyed name", async () => {
    await bootSurface({ rootAttrs: "", items: [initiatedItem(1)] });

    // The translator echoes keys, so this asserts the KEY the code chose — not a translation free to drift.
    expect(tabButton(OUTBOX)).not.toBeNull();
    expect(tabButton(OUTBOX).textContent).toContain("TabInitiated");
  });

  it("says its OWN sentence when nothing is in it, not a generic blank", async () => {
    /*
     * The empty state is the tab's answer to a reader who opened it expecting something. It has to be the
     * tab's own sentence — an unlabelled blank teaches nothing, and the generic "no match" line would be a
     * lie here (nothing was filtered out; there is nothing to filter).
     *
     * Asserted through the KEY, so this proves the string is WIRED. Its presence in all seven resx is a
     * different guard's job (workcenter-next-l10n-key-guard), and one without the other is half a check.
     */
    await bootOn(OUTBOX, [item(1)]);

    const empty = app().querySelector(".wcn-empty");
    expect(empty).not.toBeNull();
    expect(empty.textContent).toContain("EmptyInitiatedTitle");
    expect(empty.textContent).toContain("EmptyInitiatedDesc");
  });

  it("actually paints a row per initiated item — nothing below means anything without this", async () => {
    await bootOn(OUTBOX, [initiatedItem(1), initiatedItem(2)]);

    expect(rowIds()).toEqual([ID(1), ID(2)]);
    expect(app().textContent).toContain("Görev 1");
  });
});

// ── guard (a) ────────────────────────────────────────────────────────────────

describe("guard (a): work I opened and somebody else holds shows up in Başlattıklarım", () => {
  it("routes the row to the Outbox and nowhere else", async () => {
    await bootOn(OUTBOX, [initiatedItem(1)]);
    expect(rowIds()).toEqual([ID(1)]);

    // …and it is in NO other ownership tab. Asserted by id after the positive above, so an empty page cannot
    // make this pass.
    for (const other of ["inbox", "islerim", "havuz", "history"]) {
      await clickTab(other);
      expect(rowIds()).not.toContain(ID(1));
    }
  });

  it("counts on the Outbox badge, so the reader can see it without opening the tab", async () => {
    await bootSurface({ rootAttrs: "", items: [initiatedItem(1), initiatedItem(2), initiatedItem(3)] });

    const badge = tabButton(OUTBOX).querySelector(".wcn-tab-count");
    expect(badge).not.toBeNull();
    expect(Number(badge.textContent.trim())).toBe(3);
  });
});

// ── guard (b) ────────────────────────────────────────────────────────────────

describe("guard (b): work I opened and I HOLD stays in İşlerim", () => {
  it("is my own work, not something I am watching somebody else do", async () => {
    // The provider never marks this `initiator` — it is held work. Stated here as the SHELL's rule too, because
    // the routing must not start guessing from `requester.isCurrentUser`.
    const mine = item(1, { requester: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true } });

    await bootOn("islerim", [mine]);
    expect(rowIds()).toEqual([ID(1)]);

    await clickTab(OUTBOX);
    expect(rowIds()).toEqual([]);
  });
});

// ── guard (c) ────────────────────────────────────────────────────────────────

describe("guard (c): work somebody else opened never reaches the Outbox", () => {
  it("leaves another person's request in the tab its own state puts it in", async () => {
    await bootOn("islerim", [item(1)]);
    expect(rowIds()).toEqual([ID(1)]);

    await clickTab(OUTBOX);
    expect(rowIds()).toEqual([]);
  });

  it("keeps claimable pool work in Havuz, where the claim button is", async () => {
    /*
     * The precedence the SERVER applies, pinned on the shell so a future edit cannot re-derive it here. A pooled
     * row that reaches the browser without `viewerRelation` is claimable, and claimable work belongs where the
     * act lives — not in an observational tab under a button that answers 403.
     */
    await bootOn("havuz", [poolItem(1)]);
    expect(rowIds()).toEqual([ID(1)]);

    await clickTab(OUTBOX);
    expect(rowIds()).toEqual([]);
  });
});

// ── guard (d) ────────────────────────────────────────────────────────────────

describe("guard (d): the Outbox offers no act that requires holding the work", () => {
  const HOLDER_ACTS = ["accept", "claim", "start", "complete", "submitReview", "plan", "inquire", "release", "return"];

  it("draws the requester's acts and none of the holder's", async () => {
    await bootOn(OUTBOX, [initiatedItem(1)]);

    // NON-VACUITY: the row really did render buttons — so "no complete button" is a fact about this tab and not
    // about a page that painted nothing.
    const codes = actionCodes();
    expect(codes).toContain("reassign");
    expect(codes).toContain("cancel");
    HOLDER_ACTS.forEach((act) => { expect(codes).not.toContain(act); });
  });

  it("does not offer recall — v1.5, and no endpoint answers it today", async () => {
    await bootOn(OUTBOX, [initiatedItem(1)]);

    expect(actionCodes()).not.toContain("recall");
    expect(app().textContent).not.toContain("recall");
  });
});

// ── the axis law is unchanged ────────────────────────────────────────────────

describe("adding an ownership tab did not move any other axis", () => {
  it("puts no segment bar under the Outbox — status is still the segment, and only under İşlerim", async () => {
    await bootOn(OUTBOX, [initiatedItem(1)]);

    expect(Array.from(app().querySelectorAll("[data-wcn-seg]"))).toEqual([]);
  });

  it("still shows the segment bar under İşlerim, so the assertion above is about the Outbox", async () => {
    await bootOn("islerim", [item(1)]);

    expect(Array.from(app().querySelectorAll("[data-wcn-seg]")).length).toBeGreaterThan(0);
  });
});

// ── the contract refuses a relation it cannot route ──────────────────────────

describe("an unknown viewerRelation is a contract error, not a shrug", () => {
  it("is rejected rather than silently routed to the wrong tab", async () => {
    await bootSurface({ rootAttrs: "", items: [] });
    const result = global.WorkCenterNextContract.validateWorkItem(
      initiatedItem(1, { viewerRelation: "delegate" }));

    expect(result.valid).toBe(false);
    expect(result.errors.map((e) => e.code)).toContain("VIEWER_RELATION_INVALID");
  });

  it("accepts the one relation the provider emits", async () => {
    await bootSurface({ rootAttrs: "", items: [] });
    const result = global.WorkCenterNextContract.validateWorkItem(initiatedItem(1));

    expect(result.errors).toEqual([]);
    expect(result.valid).toBe(true);
  });
});
