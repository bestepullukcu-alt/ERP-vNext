const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 — the CLOSING round on the create form. Six items, five of them behavioural.
 *
 * The thread running through all of them is the same one this project has now corrected six times: a control the
 * user can operate must correspond to something the system actually does with it. Four different shapes of the
 * same defect are pinned here.
 *
 *  1. A field the BACKEND owns was offered to the user, and the value they typed won the cascade — so a task
 *     created for a person in Finance could be filed under a unit they do not belong to, with no warning.
 *  2. Planning fields belonging to the DOER were shown to the REQUESTER, who cannot know them.
 *  3. `plannedDate` is the Plan TRANSITION's output; writing it at create produces a task that carries a planned
 *     date while not being Planned — the same fact in two places, contradicting itself at birth.
 *  4. Four of five notification checkboxes could not produce an email in the state they were offered in.
 *  6. "Zorunlu" named no field, on a nine-card form.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");
const FORM_JS = () => read("wwwroot", "assets", "js", "Tasks", "form.js");
const FORM_PAGE_JS = () => read("wwwroot", "assets", "js", "Tasks", "form-page.js");

const loadForm = () => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
  return global.TaskForm;
};

const draft = (over) => ({
  title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned", ...over
});

// ── 1. the organization unit is DERIVED, never typed ────────────────────────────────────────────────────────

describe("the organization unit is the backend's answer, not a question to the user", () => {
  /*
   * CreateTaskItemHandler's own rule (pack §12 K6): every task HAS a unit and the user never picks one; the
   * cascade is request → the assignee's position's unit → the tenant root. The form put a raw GUID box at step
   * ONE of that cascade, so a typed value silently overrode the person's real unit. The information the box asked
   * for is already on screen: every person and position option reads "Name — Position — Unit".
   */
  test("the SERVER still derives the unit from the person or the position", () => {
    const handler = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "Handlers", "CommandHandlers", "CreateTaskItemHandler.cs");

    // The cascade is what makes the form field unnecessary — if it ever goes, this field has to come back.
    expect(handler, "the pool no longer inherits its position's unit")
      .toContain("position.OrganizationUnitId");
    expect(handler, "the assignee's unit is no longer resolved server-side")
      .toMatch(/ResolveUnitForUserAsync\(assigneeUserId/);
    expect(handler, "the root-unit fallback is gone").toContain("ResolveTenantRootUnitAsync");
  });

  test("the form has no organization-unit control at all", () => {
    const form = TASK_FORM();

    expect(form, "the raw GUID box is still on the form").not.toContain('id="taskOrganizationUnit"');
    expect(form, "the unit field still has a slot to be shown in")
      .not.toContain('data-task-field="organizationUnit"');
    expect(form, "the removed label is still resolved").not.toContain("FieldOrganizationUnit");
  });

  test("the payload carries no organizationUnitId, whatever a stale draft holds", () => {
    /*
     * Not merely "the control is gone": a handed-over quick-create draft, or a hydrated edit, can still carry the
     * key. The payload is where the rule has to hold, because that is what reaches the cascade.
     */
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload(
      draft({ organizationUnitId: "22222222-2222-2222-2222-222222222222" }));

    expect(Object.prototype.hasOwnProperty.call(payload, "organizationUnitId"),
      "the form still sends a unit, and it wins the server's cascade").toBe(false);
  });

  test("nothing on the page reads the removed control", () => {
    expect(FORM_PAGE_JS(), "the page still reads the removed control")
      .not.toContain("taskOrganizationUnit");
    expect(FORM_JS(), "the visibility rule still has an organizationUnit branch")
      .not.toContain("organizationUnit:");
  });

  test("the CONTRACT keeps the property — a system integration may still send one", () => {
    // Only the form is withdrawn. An integration that genuinely knows the unit must still be able to say so.
    const models = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "TaskModels.cs");
    expect(models, "the nullable contract property was removed as well")
      .toMatch(/Guid\?\s+OrganizationUnitId/);
  });
});

// ── 2. planning belongs to the doer ─────────────────────────────────────────────────────────────────────────

describe("the due date is the requester's, the plan is the doer's", () => {
  /*
   * Due date = "when I need it by", and a requester always owns that. Start date and estimate = "how I will fit
   * it in", which only the person doing the work can answer. Planning someone else's week for them is the ERP
   * distinction SAP and Oracle both draw: the requester gives a deadline, the resource builds the schedule.
   */
  test("start and estimate are target-driven slots, the due date is not", () => {
    const form = TASK_FORM();

    expect(form, "the start date has no visibility slot").toMatch(/data-task-field="startAt"/);
    expect(form, "the estimate has no visibility slot").toMatch(/data-task-field="estimateHours"/);

    // The due date must NEVER be conditional — it is required for all three targets.
    const dueBlock = form.slice(form.indexOf('for="taskDueAt"') - 400, form.indexOf('id="taskDueAt"'));
    expect(dueBlock, "the due date became conditional").not.toContain("data-task-field=");
    expect(form, "the due date is no longer required").toMatch(/id="taskDueAt"[^>]*required/);
  });

  test("the existing target mechanism drives them — no second mechanism was invented", () => {
    const TaskForm = loadForm();

    expect(TaskForm.visibleFieldsFor("SelfAssigned")).toMatchObject({ startAt: true, estimateHours: true });
    expect(TaskForm.visibleFieldsFor("Person")).toMatchObject({ startAt: false, estimateHours: false });
    expect(TaskForm.visibleFieldsFor("PositionPool")).toMatchObject({ startAt: false, estimateHours: false });
  });

  test("assigning to someone else hides them in the DOM, by class", () => {
    const TaskForm = loadForm();
    document.body.innerHTML = `
      <div data-task-field="startAt"><input id="taskStartAt" /></div>
      <div data-task-field="estimateHours"><input id="taskEstimateHours" /></div>`;

    TaskForm.applyTargetVisibility(document, "Person");
    const start = document.querySelector('[data-task-field="startAt"]');
    expect(start.classList.contains("d-none")).toBe(true);
    expect(start.getAttribute("style"), "FG-003: an inline style was written").toBeNull();

    TaskForm.applyTargetVisibility(document, "SelfAssigned");
    expect(start.classList.contains("d-none")).toBe(false);
  });

  test("a HIDDEN planning field sends no value — the reminder-lead defect must not repeat", () => {
    /*
     * Its own test, because this exact shape shipped once already this week: the control was hidden and its value
     * travelled anyway, so the server stored a plan nobody made.
     */
    const TaskForm = loadForm();

    const other = TaskForm.buildCreatePayload(draft({
      assignmentTarget: "Person", assigneeUserId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
      startAt: "2026-08-20", estimateHours: "6"
    }));
    expect(other.startAt, "a start date was planned on the assignee's behalf").toBeNull();
    expect(other.estimateHours, "an estimate was made on the assignee's behalf").toBeNull();

    const mine = TaskForm.buildCreatePayload(draft({ startAt: "2026-08-20", estimateHours: "6" }));
    expect(mine.startAt, "my own plan was dropped").toBe("2026-08-20");
    expect(mine.estimateHours).toBe(6);
  });

  test("the due date survives every target", () => {
    const TaskForm = loadForm();
    ["SelfAssigned", "Person", "PositionPool"].forEach((target) => {
      expect(TaskForm.buildCreatePayload(draft({ assignmentTarget: target })).dueAt).toBe("2026-09-01");
    });
  });
});

// ── 3. plannedDate is the Plan transition's output ──────────────────────────────────────────────────────────

describe("a task cannot be born with a planned date it has not been planned into", () => {
  test("the Plan TRANSITION is what owns the field, and still does", () => {
    const transitions = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "Handlers", "CommandHandlers", "TaskItemTransitionHandlers.cs");
    const plan = transitions.slice(transitions.indexOf("PlanTaskItemCommand"));

    expect(plan, "the Plan transition no longer writes the planned date").toContain("PlannedDate");
    expect(plan, "the Plan transition no longer moves the task to Planned")
      .toMatch(/TaskLifecycle\.Planned/);
  });

  test("the create form no longer offers it", () => {
    const form = TASK_FORM();
    expect(form, "the planned-date control is still on the create form").not.toContain('id="taskPlannedDate"');
    expect(form, "the removed label is still resolved").not.toContain("FieldPlannedDate");
    expect(FORM_PAGE_JS(), "the page still reads the removed control").not.toContain("taskPlannedDate");
  });

  test("the create payload carries no plannedDate", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload(draft({ plannedDate: "2026-08-11" }));

    expect(Object.prototype.hasOwnProperty.call(payload, "plannedDate"),
      "create still writes a planned date, so the task is born contradicting itself").toBe(false);
  });

  test("an EDIT does not silently CLEAR a date the Plan transition set", () => {
    /*
     * The form is shared by create and edit, and the update handler assigns `task.PlannedDate =
     * request.PlannedDate` unconditionally. So a payload that simply omits the field would wipe the date a
     * planned task already carries — removing a control must not delete data behind it.
     */
    const TaskForm = loadForm();

    const payload = TaskForm.buildUpdatePayload(draft(), 4, { plannedDate: "2026-08-11" });

    expect(payload.plannedDate, "the stored planned date was dropped on save").toBe("2026-08-11");
    expect(payload.expectedVersion).toBe(4);
  });

  test("an EDIT keeps a hidden start/estimate the doer entered later", () => {
    // Same rule, same reason: the requester reopening a delegated task must not erase the doer's own plan.
    const TaskForm = loadForm();

    const payload = TaskForm.buildUpdatePayload(
      draft({ assignmentTarget: "Person", assigneeUserId: "dddddddd-dddd-dddd-dddd-dddddddddddd" }),
      2, { startAt: "2026-08-20", estimateHours: 6 });

    expect(payload.startAt).toBe("2026-08-20");
    expect(payload.estimateHours).toBe(6);
  });
});

// ── 4. the email events answer to the rest of the page ──────────────────────────────────────────────────────

describe("an email checkbox is offered only where an email can be produced", () => {
  /*
   * Measured on a "Kendim" task: four of the five boxes cannot produce an email, and all five arrive ticked.
   *   assigned  — recipient is the actor, and the service excludes the actor
   *   claimed   — ClaimTaskItemHandler REFUSES a non-pool task outright
   *   completed — recipient is the creator, who is the actor
   *   approvalrequested — the approval switch is off
   *   duesoon   — the only one that fires
   *
   * The three KINDS get three different answers, and conflating them is how a real future email gets switched
   * off in silence:
   *   (a) structurally impossible in this shape   → hide (claimed: only a pool task is ever claimed)
   *   (b) governed by another control on the page → follow it (approvalrequested ↔ the approval switch)
   *   (c) dead TODAY, alive after a handover      → keep (assigned/completed fire on delegate or reassign)
   */
  test("claiming is refused outright for anything but a pool task", () => {
    const handler = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "Handlers", "CommandHandlers", "ClaimTaskItemHandler.cs");
    expect(handler, "claiming a non-pool task is no longer refused")
      .toMatch(/AssignmentTarget\s*!=\s*TaskAssignmentTarget\.PositionPool/);
  });

  test("(a) the claim event is a pool-only slot", () => {
    const form = TASK_FORM();
    const slot = form.slice(form.indexOf('data-task-field="notifyClaimed"'));
    expect(form, "the claim event has no visibility slot").toContain('data-task-field="notifyClaimed"');
    expect(slot.slice(0, 500), "the slot does not hold the claim checkbox")
      .toContain("platform.tasks.claimed");

    const TaskForm = loadForm();
    expect(TaskForm.visibleFieldsFor("PositionPool").notifyClaimed).toBe(true);
    expect(TaskForm.visibleFieldsFor("Person").notifyClaimed).toBe(false);
    expect(TaskForm.visibleFieldsFor("SelfAssigned").notifyClaimed).toBe(false);
  });

  test("(b) the approval event follows the approval switch, like the manager field does", () => {
    const form = TASK_FORM();
    expect(form, "the approval event has no visibility slot")
      .toContain('data-task-field="notifyApprovalRequested"');

    // The SAME mechanism the reminder lead uses to follow the due-soon box — not a new one.
    const page = FORM_PAGE_JS();
    expect(page, "nothing ties the approval event to the approval switch")
      .toMatch(/notifyApprovalRequested[\s\S]{0,300}taskApprovalRequired|taskApprovalRequired[\s\S]{0,300}notifyApprovalRequested/);
  });

  test("(c) assigned and completed STAY — hiding them would mute a real future email", () => {
    /*
     * Deliberate, and pinned so the next round does not "finish the job". Both fire the moment the task is
     * delegated or reassigned, which the delegation switch on this very page allows. A checkbox that is quiet
     * today and loud tomorrow is not a false promise; hiding it silently opts the owner out of a real email.
     */
    const form = TASK_FORM();
    expect(form).toContain('value="platform.tasks.assigned"');
    expect(form).toContain('value="platform.tasks.completed"');

    // The BOX each one sits in, not the block around all five: the block is legitimately conditional (it hides
    // with the master switch), and reading 400 characters backwards would find that wrapper instead.
    const boxOf = (id) => {
      const at = form.indexOf(`id="${id}"`);
      return form.slice(form.lastIndexOf('<div class="form-check choice-box', at), at);
    };
    ["taskNotifyAssigned", "taskNotifyCompleted"].forEach((id) => {
      expect(boxOf(id), `${id} was made conditional`).not.toContain("data-task-field=");
    });
  });

  test("all five stay ticked by default", () => {
    const form = TASK_FORM();
    const boxes = [...form.matchAll(/<input[^>]*name="notifyOnEvents"[^>]*>/g)].map((m) => m[0]);
    expect(boxes).toHaveLength(5);
    boxes.forEach((box) => expect(box, `${box} is not ticked by default`).toContain("checked"));
  });

  test("a HIDDEN event never reaches the payload", () => {
    const TaskForm = loadForm();

    // Self-assigned, no approval: neither claim nor approval-request can happen.
    const self = TaskForm.buildCreatePayload(draft({
      emailNotificationsEnabled: true,
      notifyOnEvents: ["platform.tasks.assigned", "platform.tasks.claimed",
        "platform.tasks.duesoon", "platform.tasks.completed", "platform.tasks.approvalrequested"]
    }));
    expect(self.notifyOnEvents).toEqual([
      "platform.tasks.assigned", "platform.tasks.duesoon", "platform.tasks.completed"
    ]);

    // A pool task can be claimed; approval is still off.
    const pool = TaskForm.buildCreatePayload(draft({
      assignmentTarget: "PositionPool", poolPositionId: "p1", emailNotificationsEnabled: true,
      notifyOnEvents: ["platform.tasks.claimed", "platform.tasks.approvalrequested"]
    }));
    expect(pool.notifyOnEvents).toEqual(["platform.tasks.claimed"]);

    // Approval requested: the event becomes real.
    const governed = TaskForm.buildCreatePayload(draft({
      approvalRequired: true, approvalManagerUserId: "m1", emailNotificationsEnabled: true,
      notifyOnEvents: ["platform.tasks.approvalrequested", "platform.tasks.claimed"]
    }));
    expect(governed.notifyOnEvents).toEqual(["platform.tasks.approvalrequested"]);
  });

  test("the switched-off channel still sends nothing at all", () => {
    // The existing rule is untouched: null means "not chosen", and filtering must not turn it into a list.
    const TaskForm = loadForm();
    expect(TaskForm.buildCreatePayload(draft({
      emailNotificationsEnabled: false, notifyOnEvents: ["platform.tasks.duesoon"]
    })).notifyOnEvents).toBeNull();

    expect(TaskForm.buildCreatePayload(draft({
      emailNotificationsEnabled: true, notifyOnEvents: null
    })).notifyOnEvents).toBeNull();
  });
});

// ── 6. the required-field warning names the field ───────────────────────────────────────────────────────────

describe('"Zorunlu" says WHICH field, on a nine-card form', () => {
  test("the missing fields are named, in the page's language", () => {
    const TaskForm = loadForm();
    const t = (key) => ({ fieldTitle: "Başlık", fieldDueAt: "Bitiş tarihi", fieldAssignee: "Atanan" }[key] || key);

    const missing = TaskForm.missingRequiredFields(
      { valid: false, errors: ["title", "dueAt", "assigneeUserId"] }, { valid: true, errors: [] }, [], t);

    expect(missing.map((entry) => entry.label)).toEqual(["Başlık", "Bitiş tarihi", "Atanan"]);
    // An anchor per entry, so the first one can be focused and scrolled to.
    expect(missing.map((entry) => entry.id)).toEqual(["taskTitle", "taskDueAt", "taskAssignee"]);
  });

  test("a tenant's own configurable field is named by ITS label, not its code", () => {
    const TaskForm = loadForm();
    const definitions = [{ code: "PHASE", valueType: "Text", isActive: true, labelText: "Faz" }];

    const missing = TaskForm.missingRequiredFields(
      { valid: true, errors: [] }, { valid: false, errors: ["PHASE"] }, definitions, (k) => k);

    expect(missing).toEqual([{ id: "taskCustomField_PHASE", label: "Faz" }]);
  });

  test("the warning carries the names and the page focuses the first one", () => {
    const page = FORM_PAGE_JS();
    expect(page, "the missing fields are never computed").toContain("missingRequiredFields");
    expect(page, "the dialog body is still empty — only the title says 'Zorunlu'")
      .toMatch(/requiredFieldsMissing/);
    // Optional-call form included: a select2 control's own <select> is hidden, so the focus goes to the rendered
    // sibling, which may not exist in every fixture.
    expect(page, "nothing focuses the first missing field").toMatch(/\.focus\??\.?\(/);
    expect(page, "nothing scrolls the first missing field into view").toMatch(/scrollIntoView/);
  });

  test("the sentence exists in all seven languages", () => {
    const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`);
      expect(xml, `${locale} has no RequiredFieldsMissing`).toContain('name="RequiredFieldsMissing"');
    });
    expect(read("Views", "Tasks", "_IndexL10n.cshtml"),
      "the new string never reaches the browser").toContain("RequiredFieldsMissing");
  });
});
