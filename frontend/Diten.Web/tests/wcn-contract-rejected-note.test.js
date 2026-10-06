const { bootSurface, app } = require("./wcn-boot");

/*
 * BL-379 — a contract-invalid work item must never disappear WITHOUT A TRACE. work-items-api.js's validateItems
 * already drops it from state.items (correctly — the validator is never loosened); this file is about the other
 * half: loadWorkItems reading result.errors and (1) naming the rejection on the console (fixtureId + code, so a
 * specific row can be traced) and (2) showing a non-blocking, visible note next to the existing partial-board
 * banner. Both are asserted against the REAL app.js — the only stub is the network, at fetchWorkItems, exactly
 * like every other wcn-boot.js harness test.
 */
const ID = (n) => `7a1c9e00-1111-4444-8888-00000000000${n}`;

const item = (n) => ({
  fixtureKind: "workItem",
  id: ID(n),
  workIntent: "task",
  // Unaccepted work, so the row lands in the DEFAULT tab (Inbox) — same choice as wcn-partial-board-source-name.
  assignmentMode: "offered",
  ownershipState: "assigned",
  admissionState: "pendingAcceptance",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: `Görev ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: ID(n),
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
  dueAt: null
});

const TRANSLATOR = {
  t: (key) => key,
  tf: (key, ...args) => (key === "WorkItemsContractRejected" ? `${args[0]} rejected` : key),
  tn: (key) => key
};

describe("BL-379 — a contract-rejected item is named, not silently dropped", () => {
  let warnSpy;

  beforeEach(() => {
    warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
  });

  afterEach(() => {
    warnSpy.mockRestore();
  });

  it("console.warns with the fixtureId and code for every rejected item", async () => {
    await bootSurface({
      items: [item(1)],
      wcn: TRANSLATOR,
      errors: [
        { fixtureId: "bad-1", code: "REVIEW_MEETING_ACTION_REQUIRED", path: "actions" },
        { fixtureId: "bad-2", code: "DISABLED_REASON_REQUIRED", path: "actions[0]" }
      ]
    });

    const messages = warnSpy.mock.calls.map((call) => call.join(" "));
    expect(messages.some((m) => m.includes("bad-1") && m.includes("REVIEW_MEETING_ACTION_REQUIRED"))).toBe(true);
    expect(messages.some((m) => m.includes("bad-2") && m.includes("DISABLED_REASON_REQUIRED"))).toBe(true);
  });

  it("shows a non-blocking note naming how many items were rejected — the board still renders", async () => {
    await bootSurface({
      items: [item(1)],
      wcn: TRANSLATOR,
      errors: [
        { fixtureId: "bad-1", code: "REVIEW_MEETING_ACTION_REQUIRED", path: "actions" },
        // Two errors, ONE fixture — the note counts ITEMS rejected, not error occurrences.
        { fixtureId: "bad-1", code: "DISABLED_REASON_REQUIRED", path: "actions[0]" },
        { fixtureId: "bad-2", code: "DISABLED_REASON_REQUIRED", path: "actions[1]" }
      ]
    });

    const note = app().querySelector(".wcn-contract-rejected");
    expect(note).not.toBeNull();
    expect(note.textContent).toContain("2 rejected");
    // The board itself is unharmed — the row that DID pass the contract is still on screen.
    expect(app().textContent).toContain("Görev 1");
  });

  it("shows no note at all when nothing was rejected", async () => {
    await bootSurface({ items: [item(1)], wcn: TRANSLATOR, errors: [] });

    expect(app().querySelector(".wcn-contract-rejected")).toBeNull();
  });
});
