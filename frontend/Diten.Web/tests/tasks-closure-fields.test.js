const { loadScript } = require("./load-script");

/*
 * WP-PSS-MOD0024-CLOSURE-ENVELOPE-2A-01 — `TaskFieldDefinition.Stage` on the CREATE/EDIT form.
 *
 * `GET /field-definitions` answers ONE catalogue to both the create form and the closure window — there is no
 * second endpoint — so the create form's own filter, `applicableDefinitions`, is the ONLY thing standing between
 * a CLOSURE-stage field and the form the pack says it must never appear on (MOD-0024 Task Closure & Reporting
 * §4: "Closure alanları oluştur/düzenle formunda GÖRÜNMEZ").
 *
 * Part (a) below calls the real exported function directly. Part (b) drives the REAL page boot — the same
 * harness `tasks-form-select2-notification.test.js` proved out — so the leak is also checked at the one place it
 * would actually be seen: the rendered `#taskCustomFieldsRow`.
 */
describe("a Closure-stage field never reaches the create/edit form", () => {
  const definition = (over) => ({
    code: "regulatory.phase",
    labelText: "Faz",
    valueType: "Text",
    section: "Regulatory",
    isActive: true,
    appliesToModuleCode: null,
    stage: "Entry",
    ...over
  });

  describe("(a) the filter function itself", () => {
    beforeEach(() => {
      delete global.TaskFormPage;
      delete global.TaskForm;
      // form-page.js reads `TaskForm.CHECKLIST_DEFAULT_LEVEL` and calls `TasksApi.activeTaskTypes()`
      // unconditionally at load — both unrelated to what this suite measures, stubbed just enough not to throw.
      loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
      loadScript("wwwroot/assets/js/Tasks/form.js");
      global.TasksApi = { activeTaskTypes: () => Promise.resolve({ ok: false }) };
      loadScript("wwwroot/assets/js/Tasks/form-page.js");
    });

    it("keeps an Entry field and drops a Closure field from the same payload", () => {
      const rows = [definition({ code: "entry.one", stage: "Entry" }), definition({ code: "closure.one", stage: "Closure" })];

      expect(global.TaskFormPage.applicableDefinitions(rows).map((d) => d.code)).toEqual(["entry.one"]);
    });

    it("keeps a definition with NO stage at all — every row written before this slice", () => {
      // The entity default is Entry; a document written before the field existed deserialises without it. The
      // wire can therefore omit `stage` entirely, and the filter must read that exactly like "Entry".
      const legacy = definition({ code: "legacy.one" });
      delete legacy.stage;

      expect(global.TaskFormPage.applicableDefinitions([legacy]).map((d) => d.code)).toEqual(["legacy.one"]);
    });

    it("⚠ SABOTAGE: removing the stage check lets a Closure field leak onto the create form", () => {
      /*
       * Asserted against the REAL exported function — deleting the `stage !== 'Closure'` clause in
       * `applicableDefinitions` (wwwroot/assets/js/Tasks/form-page.js) turns this red, which is the proof the
       * guard above measures production code rather than a restated copy of the rule.
       */
      expect(global.TaskFormPage.applicableDefinitions([definition({ code: "closure.one", stage: "Closure" })]))
        .toHaveLength(0);
    });

    it("still drops an inactive definition and one claimed by another module — the two rules it already had", () => {
      const rows = [
        definition({ code: "inactive.one", isActive: false }),
        definition({ code: "other-module.one", appliesToModuleCode: "PPM" })
      ];

      expect(global.TaskFormPage.applicableDefinitions(rows)).toHaveLength(0);
    });
  });

  describe("(b) the rendered form, end to end", () => {
    const FORM_HTML = `
      <form id="taskForm" data-task-mode="create" data-task-id="">
        <input id="taskTitle" />
        <select class="select2 form-select" id="taskAssignmentTarget">
          <option value="SelfAssigned" selected>self</option>
        </select>
        <div class="d-none" data-task-field="assignee"><select class="select2 form-select" id="taskAssignee"></select></div>
        <div class="d-none" data-task-field="poolPosition"><select class="select2 form-select" id="taskPoolPosition"></select></div>
        <div class="d-none" data-task-field="organizationUnit"><input id="taskOrganizationUnit" /></div>
        <input type="checkbox" id="taskReviewRequired" />
        <div class="d-none" data-task-field="reviewer"><select class="select2 form-select" id="taskReviewer"></select></div>
        <input type="checkbox" id="taskApprovalRequired" />
        <div class="d-none" data-task-field="approvalManager"><select class="select2 form-select" id="taskApprovalManager"></select></div>
        <select class="select2 form-select" id="taskWatchers" multiple></select>
        <input class="flatpickr-date" id="taskDueAt" />
        <div class="d-none" id="taskCustomFields"><div id="taskCustomFieldsRow"></div></div>
        <button type="submit" id="taskSubmit">save</button>
      </form>`;

    const boot = async (fieldDefinitions) => {
      document.body.innerHTML = FORM_HTML;
      ["TaskForm", "TasksApi", "jQuery", "$"].forEach((key) => { delete global[key]; });

      global.TasksL10n = { t: (key) => key };
      global.DitenModal = { success: async () => {}, error: () => {}, warning: () => {} };
      global.location = { href: "" };
      global.fetch = async (url) => {
        const ok = (data) => ({ ok: true, status: 200, json: async () => ({ data }) });
        if (url.endsWith("/assignable-people") || url.endsWith("/decision-makers")) {
          return ok({ people: [], excluded: { total: 0, noActivePosition: 0, positionNotActive: 0, outOfScope: 0 } });
        }
        if (url.endsWith("/assignable-positions")) { return ok([]); }
        if (url.endsWith("/field-definitions")) { return ok(fieldDefinitions); }
        if (url.endsWith("/active")) { return ok([]); }
        return ok([]);
      };

      loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
      loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
      loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
      loadScript("wwwroot/assets/js/Tasks/form.js");
      loadScript("wwwroot/assets/js/Tasks/api.js");
      loadScript("wwwroot/assets/js/Tasks/form-page.js");
      for (let i = 0; i < 8; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
    };

    it("renders the Entry field and never draws the Closure one", async () => {
      await boot([
        definition({ code: "entry.one", stage: "Entry" }),
        definition({ code: "closure.one", labelText: "Kapanış nedeni", stage: "Closure" })
      ]);

      expect(document.querySelector('[data-custom-field="entry.one"]')).not.toBeNull();
      expect(document.querySelector('[data-custom-field="closure.one"]')).toBeNull();
      // The section is no longer empty, so the reserved container must have LOST its `d-none`.
      expect(document.getElementById("taskCustomFields").classList.contains("d-none")).toBe(false);
    });

    it("keeps the section hidden when every definition offered is Closure-stage", async () => {
      await boot([definition({ code: "closure.only", stage: "Closure" })]);

      expect(document.getElementById("taskCustomFields").classList.contains("d-none")).toBe(true);
    });
  });
});
