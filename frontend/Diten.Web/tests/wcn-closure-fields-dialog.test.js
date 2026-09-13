const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-PSS-MOD0024-CLOSURE-ENVELOPE-2A-REST-01 — the "Tamamla" window draws the task TYPE's CLOSURE-stage fields,
 * through the SAME renderer the create form uses (Tasks/form.js, already loaded by this view — never a second
 * one), blocks an empty required field on the client, and — once confirmed — the value reaches the dispatch
 * payload as `closureFieldValues`.
 *
 * `wcn-boot.js` wires the real app.js against a real DOM; its own `TasksApi`/`TaskForm` doubles are replaced
 * here with slightly fuller ones (closure-specific methods + the REAL renderer), never a SECOND implementation
 * of anything app.js itself owns.
 */
describe("the Tamamla window and its closure-stage fields", () => {
  const TASK_ID = "6c1b8e2a-6a7f-4b7a-9a7a-1d9b7a2f6e11";

  const completeAction = (overrides) => ({
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
    riskLevel: "normal",
    ...overrides
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
    title: { kind: "display", text: "Sapma incelemesi", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: {
      providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
      deepLink: `/Tasks/${TASK_ID}`
    },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution"],
    actions: [completeAction()],
    concurrency: { kind: "version", token: "5" },
    waitingContext: null,
    escalation: null,
    dueAt: "2026-07-30T00:00:00+00:00"
  }, overrides);

  const CLOSURE_FIELD = {
    id: "f1", code: "closure.note", labelText: "Kapanış açıklaması", valueType: "Text",
    section: "Closure", isActive: true, appliesToModuleCode: null, stage: "Closure", isRequired: true
  };

  let swalCalls;
  let dispatched;

  const boot = async (item, fieldDefinitions) => {
    const result = await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });

    // The REAL renderer — loaded exactly as Details.cshtml/Index.cshtml already load it, replacing wcn-boot's
    // minimal `{ buildCreatePayload }` double with the actual Tasks/form.js.
    loadScript("wwwroot/assets/js/Tasks/form.js");

    // A fuller TasksApi double: wcn-boot's own stub has no field-catalogue methods at all. Closure-specific
    // methods only — every OTHER method wcn-boot already wired (addComment, addAttachment, …) is PRESERVED,
    // because the dispatcher path this test exercises calls `dispatchAction`, not `TasksApi.transition`.
    global.TasksApi.fieldDefinitions = async () => ({ ok: true, status: 200, data: fieldDefinitions || [] });
    global.TasksApi.fieldOptions = async () => ({ ok: true, status: 200, data: [] });
    global.TasksApi.fieldRecords = async () => ({ ok: true, status: 200, data: [] });
    // `data` is the array itself — TasksApi.assignablePeople unwraps `{ people, excluded }` internally (BL-113).
    global.TasksApi.assignablePeople = async () => ({ ok: true, status: 200, data: [] });

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

  it("draws the Text closure field through TaskForm.renderCustomFields, not a second renderer", async () => {
    await boot(projectionItem(), [CLOSURE_FIELD]);
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(swalCalls).toHaveLength(1);
    expect(swalCalls[0].html).toContain('id="wcnClosureFieldsRow"');

    // Render it for real, the same way the popup would, and check the REAL control landed.
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    swalCalls[0].didOpen?.(document.body);
    const control = document.querySelector('[data-custom-field="closure.note"]');
    expect(control, "renderCustomFields did not draw the field").not.toBeNull();
    expect(control.getAttribute("data-custom-field-type")).toBe("Text");
  });

  it("blocks confirm on an empty REQUIRED closure field — the server-side courtesy", async () => {
    await boot(projectionItem(), [CLOSURE_FIELD]);
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    swalCalls[0].didOpen?.(document.body);

    expect(swalCalls[0].preConfirm()).toBe(false);
  });

  it("carries the filled value through to the dispatch payload as closureFieldValues", async () => {
    await boot(projectionItem(), [CLOSURE_FIELD]);
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    swalCalls[0].didOpen?.(document.body);
    document.querySelector('[data-custom-field="closure.note"]').value = "Kök neden bulundu";

    const confirmed = swalCalls[0].preConfirm();
    expect(confirmed).not.toBe(false);
    expect(confirmed.closureFieldValues).toEqual([
      { definitionCode: "closure.note", valueType: "Text", value: "Kök neden bulundu" }
    ]);

    global.Swal.fire = () => Promise.resolve({ isConfirmed: true, value: confirmed });
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(dispatched).not.toBeNull();
    expect(dispatched.actionCode).toBe("complete");
    expect(dispatched.body.closureFieldValues).toEqual([
      { definitionCode: "closure.note", valueType: "Text", value: "Kök neden bulundu" }
    ]);
  });

  it("a type with NO closure field draws the SAME plain confirm as before this slice", async () => {
    await boot(projectionItem(), []);
    app().querySelector('[data-wcn-action="complete"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(swalCalls).toHaveLength(0); // the plain confirm wrapper, not a raw Swal.fire — untouched by this slice
  });

  it("never fetches the closure catalogue for cancel — the pack's own boundary", async () => {
    let fieldDefinitionsCalls = 0;
    const item = projectionItem({
      actions: [{
        code: "cancel", label: { kind: "display", text: "İptal", locale: "und" }, semanticType: "cancel",
        enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
        requiresConfirmation: true, requiresReason: false, requiresEvidence: false, supportsBulk: false,
        riskLevel: "danger"
      }]
    });
    await boot(item, [CLOSURE_FIELD]);
    const originalFieldDefinitions = global.TasksApi.fieldDefinitions;
    global.TasksApi.fieldDefinitions = async (...args) => {
      fieldDefinitionsCalls += 1;
      return originalFieldDefinitions(...args);
    };

    app().querySelector('[data-wcn-action="cancel"]').click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(fieldDefinitionsCalls).toBe(0);
  });
});
