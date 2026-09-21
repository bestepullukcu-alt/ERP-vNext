const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-WCN-KANBAN-01 Dilim 1 — performAction now resolves Promise<{ outcome: 'done' | 'cancelled' | 'refused',
 * reasonCode? }> for every branch it can take (plain apply, requiresConfirmation, requiresReason, the closure/
 * attachment dialog). The button path is UNCHANGED: nothing in the click handler reads the resolved value, so
 * every assertion here that is not specifically about the returned outcome is the SAME assertion the pre-existing
 * suites already make (dispatch body, toast, re-render). What is new is `global.__wcnLastActionOutcome` — the
 * test-only seam the click handler assigns performAction's Promise to (app.js has no module exports, so a DOM
 * click has no other way to hand a caller the resolved value).
 */
describe("performAction resolves an outcome for every branch", () => {
  const TASK_ID = "9a9a9a9a-1111-2222-3333-444444444444";

  const action = (overrides) => Object.assign({
    code: "start",
    label: { kind: "display", text: "Başlat", locale: "und" },
    semanticType: "start",
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

  const projectionItem = (overrides) => Object.assign({
    fixtureKind: "workItem",
    id: TASK_ID,
    workIntent: "task",
    assignmentMode: "direct",
    ownershipState: "owned",
    admissionState: "admitted",
    normalizedStatus: "Pending",
    taskLifecycle: "Open",
    executionState: "notStarted",
    timerState: "notApplicable",
    systemState: "fresh",
    actionDepth: "inline",
    title: { kind: "display", text: "Kalite raporu", locale: "und" },
    nativeStatus: { code: "Open", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Open" } },
    source: {
      providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
      deepLink: `/Tasks/${TASK_ID}`
    },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution"],
    actions: [action()],
    concurrency: { kind: "version", token: "5" },
    waitingContext: null,
    escalation: null,
    dueAt: "2026-07-30T00:00:00+00:00"
  }, overrides);

  let dispatched;
  let dispatchResult;

  const boot = async (item) => {
    await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });

    dispatched = null;
    dispatchResult = { ok: true, status: 204 };
    global.WorkCenterNextApi.dispatchAction = async (itemId, actionCode, providerCode, body) => {
      dispatched = { itemId, actionCode, providerCode, body };
      return dispatchResult;
    };
    global.TasksApi.assignablePeople = async () => ({ ok: true, data: [] });
  };

  const click = async (code) => {
    app().querySelector(`[data-wcn-action="${code}"]`).click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    return global.__wcnLastActionOutcome;
  };

  afterEach(() => { delete global.Swal; delete global.showConfirm; delete global.__wcnLastActionOutcome; });

  // ── plain apply — no dialog at all (start: requiresConfirmation=false, requiresReason=false) ──────────────

  it("a plain apply (no dialog) resolves 'done' once the dispatch succeeds", async () => {
    await boot(projectionItem());
    const outcome = await click("start");

    expect(dispatched.actionCode).toBe("start");
    expect(outcome).toEqual({ outcome: "done" });
  });

  it("a plain apply resolves 'refused' with the server's reasonCode when the dispatch is rejected", async () => {
    await boot(projectionItem());
    dispatchResult = { ok: false, status: 409, reasonCode: "APPROVAL_PENDING" };
    const outcome = await click("start");

    expect(outcome).toEqual({ outcome: "refused", reasonCode: "APPROVAL_PENDING" });
  });

  // ── requiresConfirmation (cancel routes through sharedConfirm/showConfirm, never Swal.fire directly) ───────

  describe("requiresConfirmation", () => {
    const cancelAction = () => action({
      code: "cancel", semanticType: "cancel", requiresConfirmation: true, riskLevel: "destructive"
    });

    it("confirming resolves 'done'", async () => {
      await boot(projectionItem({ actions: [cancelAction()] }));
      global.Swal = {};
      global.showConfirm = (title, callback) => { callback(); };
      const outcome = await click("cancel");

      expect(dispatched.actionCode).toBe("cancel");
      expect(outcome).toEqual({ outcome: "done" });
    });

    it("dismissing the dialog resolves 'cancelled' and sends no request", async () => {
      await boot(projectionItem({ actions: [cancelAction()] }));
      global.Swal = {};
      global.showConfirm = (title, callback, options) => { options.onCancel?.(); };
      const outcome = await click("cancel");

      expect(dispatched).toBeNull();
      expect(outcome).toEqual({ outcome: "cancelled" });
    });

    it("a server refusal resolves 'refused' with its reasonCode", async () => {
      await boot(projectionItem({ actions: [cancelAction()] }));
      dispatchResult = { ok: false, status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT" };
      global.Swal = {};
      global.showConfirm = (title, callback) => { callback(); };
      const outcome = await click("cancel");

      expect(outcome).toEqual({ outcome: "refused", reasonCode: "TASK_CONCURRENCY_CONFLICT" });
    });
  });

  // ── requiresReason (inquire: a mandatory-rationale Swal.fire textarea) ──────────────────────────────────────

  describe("requiresReason", () => {
    const inquireAction = () => action({ code: "inquire", semanticType: "inquire", requiresReason: true });

    it("confirming with a reason resolves 'done'", async () => {
      await boot(projectionItem({ actions: [inquireAction()] }));
      global.Swal = {
        fire: () => Promise.resolve({ isConfirmed: true, value: { reason: "Bekleniyor", waitingOnUserId: "" } })
      };
      const outcome = await click("inquire");

      expect(dispatched.actionCode).toBe("inquire");
      expect(dispatched.body.reason).toBe("Bekleniyor");
      expect(outcome).toEqual({ outcome: "done" });
    });

    it("dismissing the reason dialog resolves 'cancelled' and sends no request", async () => {
      await boot(projectionItem({ actions: [inquireAction()] }));
      global.Swal = { fire: () => Promise.resolve({ isConfirmed: false }) };
      const outcome = await click("inquire");

      expect(dispatched).toBeNull();
      expect(outcome).toEqual({ outcome: "cancelled" });
    });

    it("a server refusal resolves 'refused' with its reasonCode", async () => {
      await boot(projectionItem({ actions: [inquireAction()] }));
      dispatchResult = { ok: false, status: 409, reasonCode: "TASK_ASSIGNEE_NOT_ASSIGNABLE" };
      global.Swal = {
        fire: () => Promise.resolve({ isConfirmed: true, value: { reason: "Bekleniyor", waitingOnUserId: "" } })
      };
      const outcome = await click("inquire");

      expect(outcome).toEqual({ outcome: "refused", reasonCode: "TASK_ASSIGNEE_NOT_ASSIGNABLE" });
    });
  });

  // ── the closure/attachment dialog (complete: its own Swal.fire route, BL-146 exception) ─────────────────────

  describe("the closure dialog (complete)", () => {
    const completeAction = () => action({
      code: "complete", semanticType: "complete", requiresConfirmation: true
    });

    it("confirming resolves 'done'", async () => {
      await boot(projectionItem({
        actions: [completeAction()], workItemCapabilities: ["planning", "execution", "attachments"],
        attachments: { items: [] }
      }));
      global.TasksApi.fieldDefinitions = async () => ({ ok: true, status: 200, data: [] });
      global.Swal = {
        fire: () => Promise.resolve({ isConfirmed: true, value: { outcomeCode: "", reason: "", attachment: null } }),
        showValidationMessage: () => {}
      };
      const outcome = await click("complete");

      expect(dispatched.actionCode).toBe("complete");
      expect(outcome).toEqual({ outcome: "done" });
    });

    it("dismissing the closure dialog resolves 'cancelled' and sends no request", async () => {
      await boot(projectionItem({
        actions: [completeAction()], workItemCapabilities: ["planning", "execution", "attachments"],
        attachments: { items: [] }
      }));
      global.TasksApi.fieldDefinitions = async () => ({ ok: true, status: 200, data: [] });
      global.Swal = { fire: () => Promise.resolve({ isConfirmed: false }), showValidationMessage: () => {} };
      const outcome = await click("complete");

      expect(dispatched).toBeNull();
      expect(outcome).toEqual({ outcome: "cancelled" });
    });
  });

  // ── the button path is unchanged: dispatch body/shape match the pre-existing suites' own assertions ────────

  it("the button path still dispatches the same body it always did (byte for byte)", async () => {
    await boot(projectionItem());
    await click("start");

    expect(dispatched).toEqual({
      itemId: TASK_ID,
      actionCode: "start",
      providerCode: "tasks",
      body: { expectedVersion: 5, reasonCode: null, note: null }
    });
  });
});
