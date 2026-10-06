const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-PSS-MOD0024-ATTACHMENTS-UX-01, item 3 — "Çıktı / Kanıt ekle" in the SAME raw "Tamamla" dialog
 * wcn-closure-fields-dialog.test.js already exercises (BL-146 exception: `sharedConfirm` holds a textarea and
 * nothing else). Files upload while the task is still open; the transition is dispatched only once every staged
 * upload has succeeded. Never for `cancel` — calling work off asks for nothing to attach.
 */
describe("the Tamamla window's attachment step", () => {
  const TASK_ID = "3d4a1c9e-2b6f-4a11-9c3d-8f2b6a7e5c10";

  const completeAction = () => ({
    code: "complete",
    label: { kind: "display", text: "Tamamla", locale: "und" },
    semanticType: "complete",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: true,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  });

  const cancelAction = () => ({
    code: "cancel",
    label: { kind: "display", text: "İptal", locale: "und" },
    semanticType: "cancel",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: true,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "danger"
  });

  const projectionItem = (overrides) => Object.assign({
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
    title: { kind: "display", text: "Envanter sayımı", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: {
      providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
      deepLink: `/Tasks/${TASK_ID}`
    },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution", "attachments"],
    attachments: { items: [] },
    actions: [completeAction()],
    concurrency: { kind: "version", token: "5" },
    waitingContext: null,
    escalation: null,
    dueAt: "2026-07-30T00:00:00+00:00"
  }, overrides);

  let swalCalls;
  let dispatched;
  let uploadCalls;

  const boot = async (item) => {
    const result = await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });

    global.TasksApi.fieldDefinitions = async () => ({ ok: true, status: 200, data: [] });

    uploadCalls = [];
    global.TasksApi.addAttachment = async (taskId, payload) => {
      uploadCalls.push({ taskId, kind: payload.kind, name: payload.file?.name });
      return { ok: true, status: 201, data: { id: "att-1" } };
    };

    dispatched = null;
    global.WorkCenterNextApi.dispatchAction = async (itemId, actionCode, providerCode, body) => {
      dispatched = { itemId, actionCode, providerCode, body };
      return { ok: true, status: 204 };
    };

    swalCalls = [];
    global.Swal = {
      fire: (opts) => { swalCalls.push(opts); return Promise.resolve({ isConfirmed: false }); },
      showValidationMessage: () => {}
    };

    return result;
  };

  afterEach(() => { delete global.Swal; });

  const putFile = (input, file) => {
    Object.defineProperty(input, "files", { value: [file], writable: false, configurable: true });
  };

  const openComplete = async (item) => {
    await boot(item);
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
  };

  it("offers the file and kind inputs, Deliverable selected by default", async () => {
    await openComplete(projectionItem());

    expect(swalCalls).toHaveLength(1);
    expect(swalCalls[0].html).toContain('id="wcnCompleteAttachFile"');
    expect(swalCalls[0].html).toMatch(/<option value="Deliverable"\s+selected>/);
    expect(swalCalls[0].html).toContain('<option value="Evidence">');
  });

  it("never appears on cancel — calling work off asks for nothing to attach", async () => {
    await boot(projectionItem({ actions: [cancelAction()] }));
    app().querySelector('[data-wcn-action="cancel"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(swalCalls.some((opts) => opts.html?.includes("wcnCompleteAttachFile"))).toBe(false);
  });

  it("uploads the staged file BEFORE dispatching the transition", async () => {
    await openComplete(projectionItem());
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    putFile(document.getElementById("wcnCompleteAttachFile"), new File(["x"], "rapor.pdf"));
    document.getElementById("wcnCompleteAttachKind").value = "Deliverable";

    const confirmed = swalCalls[0].preConfirm();
    expect(confirmed).not.toBe(false);
    expect(confirmed.attachment).toEqual({ file: expect.any(File), kind: "Deliverable" });

    global.Swal.fire = () => Promise.resolve({ isConfirmed: true, value: confirmed });
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(uploadCalls).toEqual([{ taskId: TASK_ID, kind: "Deliverable", name: "rapor.pdf" }]);
    expect(dispatched, "the transition never fired after a successful upload").not.toBeNull();
    expect(dispatched.actionCode).toBe("complete");
  });

  it("never dispatches the transition when nothing was staged — the common case, unchanged", async () => {
    await openComplete(projectionItem());
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);

    const confirmed = swalCalls[0].preConfirm();
    expect(confirmed.attachment).toBeNull();

    global.Swal.fire = () => Promise.resolve({ isConfirmed: true, value: confirmed });
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(uploadCalls).toHaveLength(0);
    expect(dispatched).not.toBeNull();
  });

  it("does NOT complete the task when the upload fails — the reader asked for both", async () => {
    await openComplete(projectionItem());
    global.TasksApi.addAttachment = async () => ({ ok: false, status: 400, reasonCode: "VALIDATION_FAILED", data: null });

    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    putFile(document.getElementById("wcnCompleteAttachFile"), new File(["x"], "rapor.pdf"));
    const confirmed = swalCalls[0].preConfirm();

    global.Swal.fire = () => Promise.resolve({ isConfirmed: true, value: confirmed });
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(dispatched, "the task completed despite the failed upload").toBeNull();
  });

  // ── the RequiresDeliverableOnCompletion gate (item 4's client-side courtesy) ───────────────────────────────

  it("blocks confirm with no file when the type requires a deliverable and none exists yet", async () => {
    await openComplete(projectionItem({ taskType: { id: "t1", code: "DLV", name: "Deliverable", requiresDeliverableOnCompletion: true } }));
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);

    expect(swalCalls[0].preConfirm()).toBe(false);
  });

  it("does not block when a Deliverable attachment already exists", async () => {
    await openComplete(projectionItem({
      taskType: { id: "t1", code: "DLV", name: "Deliverable", requiresDeliverableOnCompletion: true },
      attachments: { items: [{ id: "a1", kind: "Deliverable", fileName: "onceki.pdf" }] }
    }));
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);

    expect(swalCalls[0].preConfirm()).not.toBe(false);
  });

  it("does not block when the flag is off — the overwhelming majority of types today", async () => {
    await openComplete(projectionItem({ taskType: { id: "t1", code: "PLAIN", name: "Plain", requiresDeliverableOnCompletion: false } }));
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);

    expect(swalCalls[0].preConfirm()).not.toBe(false);
  });

  it("the required label names the requirement before it can be missed", async () => {
    await openComplete(projectionItem({ taskType: { id: "t1", code: "DLV", name: "Deliverable", requiresDeliverableOnCompletion: true } }));

    expect(swalCalls[0].html).toContain("CompleteAttachFileRequiredLabel");
  });
});
