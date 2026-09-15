const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ BL-414 — "I WAS SENT TO A TASK I MAY READ, AND THE PAGE SAID IT DOES NOT EXIST" ═══════════════════════
 *
 * MEASURED before this change: the detail page resolved the requested id ONLY out of the reader's own list
 * (GET work-items/mine — assigned, own-pool, opened). The task read rule is wider — watchers, the parent's
 * assignee/pool, org-unit scope, read-all — so every one of those readers got "not found" here while the
 * module's record page (/Tasks/{id}) opened for them.
 *
 * The fix is a SECOND READ, not a wider list: when the list does not hold the id, the page asks the server for
 * that one item. The server applies the read rule and answers a missing and an unreadable task with the SAME
 * 404, so the not-found answer below is unchanged — only an admitted reader sees more.
 *
 * The harness stubs that read at the module seam (`fetchWorkItem`) exactly as it stubs the list: `byId` is what
 * the server would admit THIS reader to, and anything else answers the 404.
 */

const ID = (n) => `b4140000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const ME = "dddddddd-dddd-dddd-dddd-dddddddddddd";
const MY_REPORT = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

const action = (code) => ({
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
});

/** The reader's own task — the kind the list already holds. */
const task = (n, overrides) => Object.assign({
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
    providerCode: "tasks", providerContractVersion: "1.0",
    objectType: "task", objectId: ID(n), deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: ME, isCurrentUser: true },
  requester: { id: ME, isCurrentUser: true },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [action("complete")],
  primaryActionCode: "complete",
  concurrency: { kind: "version", token: "1" },
  waitingContext: null, escalation: null, dueAt: null
}, overrides);

/**
 * Somebody else's task, as the projection answers it for a reader who neither holds nor requested it (a watcher,
 * a scope reader, a manager): no holder act on offer.
 */
const someoneElses = (n, title) => task(n, {
  title: { kind: "display", text: title, locale: "und" },
  assignee: { id: MY_REPORT, isCurrentUser: false },
  requester: { id: MY_REPORT, isCurrentUser: false },
  actions: [],
  primaryActionCode: null
});

const openDetail = (n, config) =>
  bootSurface(Object.assign({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${ID(n)}"` }, config));

const detailDrawn = () => app().querySelector(".wcn-detail-command");
const notFoundCard = () => app().querySelector(".wcn-detail-empty");

describe("the detail page opens a task that is not on the reader's own list (BL-414)", () => {
  it("draws a task the server admits the reader to, fetched by id", async () => {
    const { fetchedById } = await openDetail(1, {
      items: [task(2)],
      byId: [someoneElses(1, "İzlediğim görev")]
    });

    expect(fetchedById, "the page never asked the server for the task the list did not hold").toEqual([ID(1)]);
    expect(detailDrawn(), "the task was fetched but the detail was not drawn").not.toBeNull();
    expect(app().textContent).toContain("İzlediğim görev");
    expect(notFoundCard()).toBeNull();
    expect(app().textContent).not.toContain("DetailItemNotFound");
  });

  it("answers exactly as before when the server does not admit the reader either", async () => {
    const { fetchedById } = await openDetail(3, { items: [task(2)], byId: [] });

    expect(fetchedById).toEqual([ID(3)]);
    expect(detailDrawn()).toBeNull();
    // The key, not a translation: the harness echoes keys, so this pins the sentence the code chose.
    expect(notFoundCard(), "the not-found card is gone").not.toBeNull();
    expect(notFoundCard().textContent).toContain("DetailItemNotFound");
    expect(notFoundCard().querySelector("a").getAttribute("href")).toBe("/WorkCenterNext");
  });

  it("does not ask the server again for a task the list already holds", async () => {
    const { fetchedById } = await openDetail(2, { items: [task(2)], byId: [] });

    expect(fetchedById, "a task on the list was fetched a second time").toEqual([]);
    expect(detailDrawn()).not.toBeNull();
  });

  it("does not ask for an id the contract rejected from the list — BL-379's sentence stays", async () => {
    const { fetchedById } = await openDetail(4, {
      items: [task(2)],
      errors: [{ fixtureId: ID(4), code: "SOME_CONTRACT_RULE" }],
      byId: [someoneElses(4, "Reddedilen görev")]
    });

    expect(fetchedById).toEqual([]);
    expect(app().textContent).toContain("DetailItemRejectedByContract");
    expect(app().textContent).not.toContain("Reddedilen görev");
  });
});

/*
 * BL-023 Ekibim → detail. MEASURED: openDetailPage carries no scope in the URL, and boot() skips
 * hydrateStateFromUrl on the detail page, so the page reads the SELF list whatever the list page was showing.
 * A subordinate's own task is not on the manager's self list, so the page said "not found" — including for a
 * manager the read rule admits. The fallback read is what fixes it; a manager the read rule does NOT admit still
 * gets the unchanged not-found answer (second case above), because the list scope is not a read-rule leg.
 */
describe("Ekibim → detail: a manager opens a subordinate's own task", () => {
  it("draws the subordinate's task instead of 'not found'", async () => {
    await openDetail(5, {
      items: [task(6)],
      byId: [someoneElses(5, "Ekip üyesinin görevi")]
    });

    expect(notFoundCard(), "the manager still lands on 'not found'").toBeNull();
    expect(app().textContent).toContain("Ekip üyesinin görevi");
    // The manager holds nothing here: the projection offered no holder act, and the page draws none.
    expect(app().querySelector('[data-wcn-action="complete"]')).toBeNull();
  });
});
