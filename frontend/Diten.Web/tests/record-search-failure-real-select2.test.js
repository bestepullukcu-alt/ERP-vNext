const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * ATT-FIX1 E4 — a record-backed custom field whose source FAILS says "the search could not be done", never "no
 * results". Driven through each page's OWN search function (Tasks/form-page.js searchRecords, WorkCenterNext/app.js
 * searchClosureFieldRecords) and its OWN wiring of the sentence (recordSearchText), on the REAL select2 4.0.13 — so
 * `return result` turned back into `return []`, or the sentence left unwired, is red here.
 */

const typeInto = async (control, text) => {
  global.jQuery(control).select2("open");
  const box = document.querySelector(".select2-container--open .select2-search__field");
  expect(box, "the picker's search box did not open").toBeTruthy();
  box.value = text;
  global.jQuery(box).trigger("input");
  await new Promise((resolve) => setTimeout(resolve, 450));
  for (let i = 0; i < 6; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  const message = document.querySelector(".select2-results__message");
  return message ? message.textContent : null;
};

const realSelect2 = () => {
  delete global.$;
  delete global.jQuery;
  loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
  loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
};

const RECORD_FIELD = {
  code: "delivery.department", labelText: "Departman", valueType: "Reference", section: "Delivery",
  importance: "Secondary", isRequired: false, sortOrder: 10, optionsSourceKind: "ModuleRecord",
  optionsSourceKey: "organization-unit", appliesToModuleCode: null, isActive: true
};

describe("ATT-FIX1 E4 — the task form's record field says a failed search", () => {
  const FORM_HTML = `
    <button type="submit" form="taskForm" id="taskSubmit">save</button>
    <form id="taskForm" data-task-mode="create" data-task-id="">
      <input id="taskTitle" />
      <select id="taskAssignmentTarget"><option value="SelfAssigned" selected>self</option></select>
      <select id="taskAssignee"></select><select id="taskPoolPosition"></select>
      <input id="taskDueAt" type="date" />
      <input type="checkbox" id="taskReviewRequired" /><input type="checkbox" id="taskApprovalRequired" />
      <input type="checkbox" id="taskEmailNotifications" checked /><input type="checkbox" id="taskDelegationAllowed" />
      <div class="d-none" id="taskCustomFields"><div id="taskCustomFieldsRow"></div></div>
    </form>`;

  it.each([[503, "searchFailed"], [429, "errorPeopleSearchRateLimited"]])(
    "a %i from the record source reads as its sentence, never 'no results'", async (status, sentence) => {
      global.fetch = async (url) => {
        const ok = (data) => ({ ok: true, status: 200, json: async () => ({ data }) });
        if (url.startsWith("/Tasks/api/field-definitions/delivery.department/records")) {
          return url.includes("term=")
            ? { ok: false, status, json: async () => ({}) }
            : ok([{ value: "3f1b2a2c-0000-4000-8000-000000000001", label: "Kalite Güvence", secondary: "QA-01" }]);
        }
        if (url === "/Tasks/api/field-definitions") { return ok([RECORD_FIELD]); }
        if (url === "/Tasks/api/assignable-positions") { return ok([]); }
        if (url === "/Tasks/api/assignable-people") { return ok({ people: [], excluded: null }); }
        return ok(null);
      };
      document.body.innerHTML = FORM_HTML;
      realSelect2();
      global.TasksL10n = { t: (key) => key };
      global.DitenModal = { success: async () => {}, error: () => {}, warning: () => {} };
      global.location = { href: "" };
      window.HTMLElement.prototype.scrollIntoView = function scrollIntoView() {};
      delete global.TaskForm;
      delete global.TasksApi;
      loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
      loadScript("wwwroot/assets/js/shared/diten-people-search.js");
      loadScript("wwwroot/assets/js/Tasks/form.js");
      loadScript("wwwroot/assets/js/Tasks/api.js");
      loadScript("wwwroot/assets/js/Tasks/form-page.js");
      for (let i = 0; i < 10; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }

      const control = document.querySelector('[data-custom-field="delivery.department"]');
      expect(control, "the record field was not drawn").toBeTruthy();
      expect(await typeInto(control, "ka")).toBe(sentence);
      delete global.fetch;
    });
});

describe("ATT-FIX1 E4 — the Task Center's closure record field says a failed search", () => {
  const TASK_ID = "6c1b8e2a-6a7f-4b7a-9a7a-1d9b7a2f6e11";
  const item = {
    fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
    timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Sapma incelemesi", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
    lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution"],
    actions: [{
      code: "complete", label: { kind: "display", text: "Tamamla", locale: "und" }, semanticType: "complete", enabled: true,
      source: "provider", disabledReasonCode: null, disabledReason: null, requiresConfirmation: true, requiresReason: false,
      requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
    }],
    concurrency: { kind: "version", token: "5" }, waitingContext: null, escalation: null, dueAt: "2026-07-30T00:00:00+00:00"
  };

  it("a 503 from the closure record source reads 'the search could not be done'", async () => {
    await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [item],
      wcn: { t: (key) => key, tf: (key, ...args) => `${key}:${args.join(",")}`, tn: (key) => key }
    });
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    global.TasksL10n = { t: (key) => key };
    global.TasksApi.fieldDefinitions = async () => ({ ok: true, status: 200, data: [Object.assign({}, RECORD_FIELD, { code: "closure.dept", stage: "Closure", section: "Closure" })] });
    global.TasksApi.fieldOptions = async () => ({ ok: true, status: 200, data: [] });
    global.TasksApi.fieldRecords = async (code, query) => (query && query.term
      ? { ok: false, status: 503, reasonCode: null, data: null }
      : { ok: true, status: 200, data: [{ value: "3f1b2a2c-0000-4000-8000-000000000001", label: "Kalite Güvence", secondary: "QA-01" }] });
    global.TasksApi.assignablePeople = async () => ({ ok: true, status: 200, data: [] });
    const fired = [];
    global.Swal = { fire: (opts) => { fired.push(opts); return Promise.resolve({ isConfirmed: false }); }, showValidationMessage: () => {} };
    realSelect2();

    app().querySelector('[data-wcn-action="complete"]').click();
    for (let i = 0; i < 6; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
    expect(fired).toHaveLength(1);
    document.body.insertAdjacentHTML("beforeend", fired[0].html);
    fired[0].didOpen?.(document.body);
    for (let i = 0; i < 6; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }

    const control = document.querySelector('[data-custom-field="closure.dept"]');
    expect(control, "the closure record field was not drawn").toBeTruthy();
    expect(await typeInto(control, "ka")).toBe("searchFailed");
    delete global.Swal;
  });
});
