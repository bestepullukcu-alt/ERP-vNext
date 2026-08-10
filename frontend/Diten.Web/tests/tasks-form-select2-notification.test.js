const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 — the conditional fields stopped opening, and the reason is a CLASS of bug, not one wire.
 *
 * Measured: choosing "Bir kişi" left "Atanan kişi" hidden; same for the pool; only "Kendim" worked. The cause is
 * the seam between two libraries:
 *
 *   select2 announces a change through jQuery      →  $(select).trigger('change')
 *   the page listened natively                     →  select.addEventListener('change', …)
 *
 * and jQuery's trigger does NOT run native listeners. The previous round swapped every picker to select2 — the
 * producer changed, the consumers did not, and every native `change` listener on a select2-bound control went
 * deaf at once. Three listeners were affected today; a fourth would be affected silently tomorrow.
 *
 * So the fix belongs where the binding happens (TaskForm.enhanceSelects), and so does this test: the notification
 * path is exercised with REAL jQuery and REAL select2, driven the way select2 drives it. A test that dispatches a
 * native event instead would pass against the broken code — which is exactly how this shipped.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");
const FORM_JS = () => read("wwwroot", "assets", "js", "Tasks", "form.js");

/** jQuery and select2 are the real vendored files: the seam under test IS their behaviour. */
const loadRealStack = () => {
  ["TaskForm", "jQuery", "$"].forEach((key) => { delete global[key]; });
  loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
  loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
  loadScript("wwwroot/assets/js/Tasks/form.js");
};

describe("a select2-bound control still announces its change to the page", () => {
  beforeEach(() => {
    document.body.innerHTML = `
      <form id="taskForm">
        <select class="select2 form-select" id="taskAssignmentTarget">
          <option value="SelfAssigned" selected>self</option>
          <option value="Person">person</option>
          <option value="PositionPool">pool</option>
        </select>
      </form>`;
    loadRealStack();
  });

  const control = () => document.getElementById("taskAssignmentTarget");

  test("the defect's own path: jQuery's trigger reaches a native listener after enhancement", () => {
    const seen = [];
    control().addEventListener("change", (event) => seen.push(event.target.value));

    global.TaskForm.enhanceSelects(document);

    // EXACTLY what select2 does when the user picks an option. Not a native dispatch — that is the assertion
    // that passed while the form was broken.
    global.jQuery(control()).val("Person").trigger("change");

    expect(seen, "select2's change never reached the page's own listener").toEqual(["Person"]);
  });

  test("a listener registered AFTER enhancement is reached too", () => {
    global.TaskForm.enhanceSelects(document);

    const seen = [];
    control().addEventListener("change", () => seen.push(control().value));
    global.jQuery(control()).val("PositionPool").trigger("change");

    expect(seen).toEqual(["PositionPool"]);
  });

  test("the bridge does not loop, and a plain native change still fires once", () => {
    global.TaskForm.enhanceSelects(document);

    let count = 0;
    control().addEventListener("change", () => { count += 1; });

    control().value = "Person";
    control().dispatchEvent(new window.Event("change", { bubbles: true }));

    expect(count, "a native change was re-broadcast — the bridge is feeding itself").toBe(1);
  });

  test("the bridge is part of BINDING, not something each listener opts into", () => {
    /*
     * The structural claim. If the notification path were wired per-listener, a fourth conditional field would
     * arrive deaf and nothing would say so — which is precisely how three of them broke at once.
     */
    const source = FORM_JS();
    const enhance = source.slice(source.indexOf("const enhanceSelects"), source.indexOf("const enhanceDates"));

    expect(enhance, "enhanceSelects does not listen for select2's change").toMatch(/\.on\(\s*['"]change/);
    expect(enhance, "enhanceSelects never re-emits a native event").toMatch(/dispatchEvent/);
  });
});

describe("the page's conditional fields open when the user picks, end to end", () => {
  /*
   * The symptom itself, on the real page: form-page.js booted against real jQuery + real select2, driven through
   * select2's own notification path. Every conditional field the form has is checked, because they all share the
   * one seam.
   */
  const FORM_HTML = `
    <form id="taskForm" data-task-mode="create" data-task-id="">
      <input id="taskTitle" />
      <select class="select2 form-select" id="taskAssignmentTarget">
        <option value="SelfAssigned" selected>self</option>
        <option value="Person">person</option>
        <option value="PositionPool">pool</option>
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

  const PERSON = "dddddddd-dddd-dddd-dddd-dddddddddddd";
  let sent;

  const boot = async () => {
    document.body.innerHTML = FORM_HTML;
    ["TaskForm", "TasksApi", "jQuery", "$"].forEach((key) => { delete global[key]; });

    sent = null;
    global.TasksL10n = { t: (key) => key };
    global.DitenModal = { success: async () => {}, error: () => {}, warning: () => {} };
    global.location = { href: "" };
    global.fetch = async (url, init) => {
      const body = init?.body ? JSON.parse(init.body) : null;
      const ok = (data) => ({ ok: true, status: 200, json: async () => ({ data }) });
      if (url.endsWith("/assignable-people")) {
        return ok([{
          userId: PERSON, displayName: "Selin Aras", positionId: "p1", positionCode: "QA-1",
          positionName: "QA Specialist", organizationUnitId: "u1", organizationUnitCode: "FAC-A",
          organizationUnitName: "Facility A"
        }]);
      }
      if (url.endsWith("/assignable-positions")) { return ok([]); }
      if (url.endsWith("/field-definitions")) { return ok([]); }
      if (init?.method === "POST") { sent = body; return ok({ id: "created" }); }
      return ok([]);
    };

    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    for (let i = 0; i < 6; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  const hidden = (field) =>
    document.querySelector(`[data-task-field="${field}"]`).classList.contains("d-none");

  /** Pick a value the way select2 does. */
  const pick = (id, value) => global.jQuery(`#${id}`).val(value).trigger("change");
  // The switches are plain checkboxes, not select2 — a real user's click is a NATIVE change, so that is what is
  // simulated here. They are in this suite as a regression pin: the seam that broke the selects must not be
  // "fixed" in a way that also rewires the controls that were working.
  const toggle = (id) => document.getElementById(id).click();

  test("choosing a person opens the assignee picker", async () => {
    await boot();
    expect(hidden("assignee"), "the assignee field started visible").toBe(true);

    pick("taskAssignmentTarget", "Person");

    expect(hidden("assignee"), "choosing a person did not open the assignee picker").toBe(false);
    expect(hidden("poolPosition")).toBe(true);
  });

  test("choosing a pool opens the pool picker", async () => {
    await boot();
    pick("taskAssignmentTarget", "PositionPool");

    expect(hidden("poolPosition"), "choosing a pool did not open the pool picker").toBe(false);
    expect(hidden("assignee")).toBe(true);
  });

  test("the review and approval switches open their pickers", async () => {
    await boot();
    expect(hidden("reviewer")).toBe(true);
    expect(hidden("approvalManager")).toBe(true);

    toggle("taskReviewRequired");
    toggle("taskApprovalRequired");

    expect(hidden("reviewer"), "the review switch did not open the reviewer picker").toBe(false);
    expect(hidden("approvalManager"), "the approval switch did not open the manager picker").toBe(false);
  });

  test("a task really can be assigned to a person again", async () => {
    await boot();

    document.getElementById("taskTitle").value = "Dosya hazırla";
    document.getElementById("taskDueAt").value = "2026-09-01";
    pick("taskAssignmentTarget", "Person");

    /*
     * The user cannot pick someone in a field they cannot see, so the flow is asserted at the step the human
     * actually takes. Without this line the test sets the assignee programmatically and passes against a form
     * whose assignee field never opens — the same false green that let this defect reach the screen.
     */
    expect(hidden("assignee"), "the assignee picker never opened, so no user could have filled it").toBe(false);
    pick("taskAssignee", PERSON);

    document.getElementById("taskSubmit").click();
    for (let i = 0; i < 3; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }

    expect(sent, "the form never posted").not.toBeNull();
    expect(sent.assignmentTarget).toBe("Person");
    expect(sent.assigneeUserId, "the task was posted with nobody assigned").toBe(PERSON);
  });
});

// ── 2. card headings ────────────────────────────────────────────────────────────────────────────────────────

describe("every card heading is styled the same way", () => {
  test("the governance headings match the reference's heading classes", () => {
    const golden = read("Views", "DevEnablement", "GoldenReferenceCompact", "_Form.cshtml");
    // Take the reference's own heading recipe rather than restating one.
    const goldenHeading = /<h6 class="([^"]*)"/.exec(golden)[1];
    expect(goldenHeading).toContain("text-uppercase");

    const headings = [...TASK_FORM().matchAll(/<h6 class="([^"]*)"/g)].map((m) => m[1]);
    expect(headings.length).toBeGreaterThanOrEqual(9);

    const odd = headings.filter((cls) => !cls.includes("text-uppercase"));
    expect(odd, `headings not using the reference's case:\n${odd.join("\n")}`).toHaveLength(0);
  });
});

// ── 3. review type ──────────────────────────────────────────────────────────────────────────────────────────

describe("the review type has a place, with the meeting option disabled and explained", () => {
  test("quick review is the default and the meeting option is disabled", () => {
    const form = TASK_FORM();
    const card = form.slice(form.indexOf('id="taskReviewRequired"') - 2000, form.indexOf('id="taskApprovalRequired"'));

    expect(card, "there is no review-type choice").toMatch(/id="taskReviewTypeQuick"/);
    expect(card, "there is no meeting option").toMatch(/id="taskReviewTypeMeeting"/);

    const meeting = card.slice(card.indexOf('id="taskReviewTypeMeeting"'));
    expect(meeting.slice(0, 300), "the meeting option is offered as if it worked").toContain("disabled");

    const quick = card.slice(card.indexOf('id="taskReviewTypeQuick"'));
    expect(quick.slice(0, 300), "quick review is not the default").toContain("checked");
  });

  test("the disabled option SAYS why, rather than looking broken", () => {
    const form = TASK_FORM();
    const meeting = form.slice(form.indexOf('id="taskReviewTypeMeeting"'));
    // DEC-001: a dead control with no explanation gets reported as a bug. The reason is localized, never literal.
    expect(meeting.slice(0, 600)).toMatch(/ReviewTypeMeetingDisabledReason/);
  });

  test("nothing new goes on the wire for it", () => {
    /*
     * There is exactly one review type today, so the choice is a NAME for current behaviour — not a new field.
     * Sending an unused property would put a value in the contract that no server code reads and every future
     * reader has to interpret.
     */
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    const payload = global.TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned", reviewRequired: true,
      reviewType: "Quick"
    });

    expect(Object.keys(payload)).not.toContain("reviewType");
  });
});

// ── 5. configurable-field grid ──────────────────────────────────────────────────────────────────────────────

describe("the configurable fields line up with the rest of the form", () => {
  test("a generated field takes the same column width the form uses", () => {
    const form = TASK_FORM();
    // What the form itself uses for a field — measured, not assumed.
    const widths = [...form.matchAll(/class="col-md-(\d+)/g)].map((m) => m[1]);
    expect(widths.length).toBeGreaterThan(0);
    const dominant = widths.sort((a, b) =>
      widths.filter((w) => w === b).length - widths.filter((w) => w === a).length)[0];
    expect(dominant).toBe("6");

    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    document.body.innerHTML = '<div id="row"></div>';
    const row = document.getElementById("row");
    global.TaskForm.renderCustomFields(row, [
      { code: "NOTE", valueType: "Text", isActive: true, labelText: "Note", sortOrder: 10 }
    ], {}, { optionPlaceholder: "—" });

    const column = row.querySelector("div");
    expect(column.className, "the generated field uses a different grid to everything around it")
      .toContain(`col-md-${dominant}`);
  });
});

// ── 6. tags ─────────────────────────────────────────────────────────────────────────────────────────────────

describe("tags are entered as chips, using the pattern the repo already has", () => {
  test("the pages load Tagify, the library the other tag inputs use", () => {
    // Established from an existing screen rather than chosen here.
    expect(read("Views", "Governance", "TenantSecuritySettings", "Index.cshtml")).toContain("tagify/tagify.css");

    for (const page of ["Create.cshtml", "Edit.cshtml"]) {
      const source = read("Views", "Tasks", page);
      expect(source, `${page} does not load the Tagify stylesheet`).toContain("tagify/tagify.css");
      expect(source, `${page} does not load Tagify`).toContain("tagify/tagify.js");
    }
  });

  test("the tag field is initialised, keeping the input comma-separated", () => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    document.body.innerHTML = '<input id="taskTags" />';

    const built = [];
    global.Tagify = function Tagify(input, options) { built.push([input, options]); this.input = input; };

    global.TaskForm.enhanceTags(document);

    expect(built, "the tag field was never turned into a chip input").toHaveLength(1);
    const format = built[0][1].originalInputValueFormat;
    expect(typeof format, "the underlying input's format is left to Tagify's default").toBe("function");
    // parseTags splits the input on commas, so the chips must write it back that way — same as the security screen.
    expect(format([{ value: "kalite" }, { value: "acil" }])).toBe("kalite,acil");
  });

  test("the wire shape is still an array of tags", () => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");

    const payload = global.TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned", tags: "kalite,acil"
    });

    expect(payload.tags).toEqual(["kalite", "acil"]);
  });
});
