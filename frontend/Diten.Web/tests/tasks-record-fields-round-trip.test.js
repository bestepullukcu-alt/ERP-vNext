const { loadScript } = require("./load-script");

/*
 * MOD-0024 — the WHOLE round trip for a field backed by another module's records, driven through the real page
 * script rather than through its helpers.
 *
 * This suite exists because unit tests on the helpers already passed while the round trip lost data. Yesterday's
 * round found exactly that on dates: `writeCustomFieldValues` was correct in isolation, and the edit form still
 * came back empty because the value the server sends and the value the control accepts are different shapes. The
 * only assertion that catches it is the one that goes all the way:
 *
 *     define → the form shows a searchable picker → choose → save → the SERVER receives the IDENTITY
 *                                                             → reopen → the NAME is on screen
 *
 * Everything below therefore boots `form-page.js` against a stubbed `fetch` and reads what the page actually
 * sent, not what a helper returned.
 */

const DEPARTMENT_A = "3f1b2a2c-0000-4000-8000-000000000001";
const DEPARTMENT_B = "3f1b2a2c-0000-4000-8000-000000000002";
const TASK_ID = "11111111-2222-3333-4444-555555555555";

const definition = {
  code: "delivery.department",
  labelText: "Departman",
  valueType: "Reference",
  section: "Delivery",
  importance: "Secondary",
  isRequired: false,
  sortOrder: 10,
  optionsSourceKind: "ModuleRecord",
  optionsSourceKey: "organization-unit",
  appliesToModuleCode: null,
  isActive: true
};

// The first page a search answers with. DEPARTMENT_B is deliberately ABSENT: a source holds more records than
// one page, and the stored value the edit form has to display routinely lives outside it.
const FIRST_PAGE = [{ value: DEPARTMENT_A, label: "Kalite Güvence", secondary: "QA-01" }];
const SEARCH_HIT = [{ value: DEPARTMENT_B, label: "Ruhsatlandırma", secondary: "REG-02" }];

// Mirrors the real page after the golden-reference alignment: a real <form>, with the actions in the header and
// the submit bound to it by id. The save is a form submit now, so a fixture built on a <div> would post nothing.
const FORM_HTML = `
  <button type="submit" form="taskForm" id="taskSubmit">save</button>
  <form id="taskForm" data-task-mode="MODE" data-task-id="TASKID">
    <input id="taskTitle" />
    <select id="taskAssignmentTarget"><option value="SelfAssigned" selected>self</option></select>
    <select id="taskAssignee"></select>
    <select id="taskPoolPosition"></select>
    <input id="taskOrganizationUnit" />
    <input id="taskDueAt" type="date" />
    <input type="checkbox" id="taskReviewRequired" />
    <input type="checkbox" id="taskApprovalRequired" />
    <input type="checkbox" id="taskEmailNotifications" checked />
    <input type="checkbox" id="taskDelegationAllowed" />
    <div class="d-none" id="taskCustomFields"><div id="taskCustomFieldsRow"></div></div>
  </form>`;

describe("MOD-0024 module-record field — the whole round trip", () => {
  let sent;
  let requested;

  /** Route one same-origin call to its canned answer, and remember every URL the page asked for. */
  const stubFetch = (taskOnEdit) => {
    global.fetch = async (url, init) => {
      requested.push(url);
      const body = init?.body ? JSON.parse(init.body) : null;
      const ok = (data, status = 200) => ({
        ok: true,
        status,
        json: async () => ({ data })
      });

      if (url.startsWith("/Tasks/api/field-definitions/delivery.department/records")) {
        // The EDIT path: identities already on the task, resolved back into records.
        if (url.includes("ids=")) {
          const ids = decodeURIComponent(url.split("ids=")[1].split("&")[0]).split(",");
          return ok([...FIRST_PAGE, ...SEARCH_HIT].filter((row) => ids.includes(row.value)));
        }
        if (url.includes("term=")) {
          const term = decodeURIComponent(url.split("term=")[1].split("&")[0]);
          return ok([...FIRST_PAGE, ...SEARCH_HIT].filter((row) => row.label.includes(term)));
        }
        return ok(FIRST_PAGE);
      }
      if (url === "/Tasks/api/field-definitions") { return ok([definition]); }
      if (url === "/Tasks/api/assignable-positions") { return ok([]); }
      if (url === "/Tasks/api/assignable-people") { return ok([]); }
      if (url === `/Tasks/api/${TASK_ID}`) { return ok(taskOnEdit); }
      if (url === "/Tasks/api" && init?.method === "POST") {
        sent = body;
        return ok({ id: TASK_ID }, 201);
      }
      if (url === `/Tasks/api/${TASK_ID}` && init?.method === "PUT") {
        sent = body;
        return ok(null, 204);
      }
      throw new Error(`unstubbed call: ${init?.method || "GET"} ${url}`);
    };
  };

  /** Boot the real page against the DOM and let its async load settle. */
  const boot = async (mode, taskId) => {
    document.body.innerHTML = FORM_HTML.replace("MODE", mode).replace("TASKID", taskId || "");
    delete global.TaskForm;
    delete global.TasksApi;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    // form-page.js boots on load and every step of it is awaited internally; three macrotask turns is more than
    // the chain needs and keeps the test from depending on how many awaits it happens to contain.
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  beforeEach(() => {
    sent = null;
    requested = [];
    // The page's own collaborators, stubbed to the minimum: the strings are the resx bridge's job (covered by
    // the 7-language test) and the modal is scenery here.
    global.TasksL10n = { t: (key) => key };
    global.DitenModal = { success: async () => {}, error: () => {}, warning: () => {} };
    global.location = { href: "" };
  });

  it("shows a searchable picker for the defined field, and hides no other section", async () => {
    await boot("create");

    const section = document.getElementById("taskCustomFields");
    expect(section.classList.contains("d-none"), "the extra-fields section stayed hidden").toBe(false);
    expect(document.querySelector('[data-custom-field-search="delivery.department"]')).not.toBeNull();
    expect(document.querySelector('[data-custom-field="delivery.department"]')).not.toBeNull();
  });

  it("saves the IDENTITY, never the label the user read", async () => {
    /*
     * The single most important assertion in this file. Storing the label would look identical on screen and
     * silently detach the task from the record: rename the department and the task keeps the old words forever.
     */
    await boot("create");

    document.getElementById("taskTitle").value = "Dosya hazırla";
    document.getElementById("taskDueAt").value = "2026-09-01";
    document.querySelector('[data-custom-field="delivery.department"]').value = DEPARTMENT_A;

    document.getElementById("taskSubmit").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(sent, "the form never posted").not.toBeNull();
    expect(sent.fieldValues).toEqual([
      { definitionCode: "delivery.department", valueType: "Reference", value: DEPARTMENT_A }
    ]);
    // Explicitly: what crossed the wire is the identity and NOT the words on screen.
    expect(JSON.stringify(sent)).not.toContain("Kalite Güvence");
  });

  it("reopens with the stored record NAMED, even though it was never on the first page", async () => {
    /*
     * The data-loss trap, end to end. The picker opens holding page one; this task points at DEPARTMENT_B,
     * which page one does not contain. A <select> handed a value with no matching <option> silently keeps its
     * old one — so without the id resolution the edit would display, and then save, a different department.
     */
    stubFetch({
      id: TASK_ID,
      version: 3,
      title: "Dosya hazırla",
      dueAt: "2026-09-01",
      assignmentTarget: "SelfAssigned",
      fieldValues: [
        { definitionCode: "delivery.department", valueType: "Reference", value: DEPARTMENT_B }
      ]
    });

    await boot("edit", TASK_ID);

    const control = document.querySelector('[data-custom-field="delivery.department"]');
    expect(control.value, "the stored department was dropped on reopen").toBe(DEPARTMENT_B);
    const selected = [...control.options].find((option) => option.value === DEPARTMENT_B);
    expect(selected.textContent).toContain("Ruhsatlandırma");
    expect(selected.textContent).toContain("REG-02");
    // BL-049: the identity is what the control CARRIES, never what it SHOWS.
    expect(selected.textContent).not.toContain(DEPARTMENT_B);
  });

  it("asks the SERVER to search, rather than filtering a page it already has", async () => {
    // Five thousand records must never cross the wire to be narrowed in the browser. The evidence is the
    // request, not the rendered list — a client-side filter would produce the same list from no request at all.
    await boot("create");
    requested.length = 0;

    const search = document.querySelector('[data-custom-field-search="delivery.department"]');
    search.value = "Ruhsat";
    search.dispatchEvent(new window.Event("input"));

    await new Promise((resolve) => setTimeout(resolve, 400));

    const searchCall = requested.find((url) => url.includes("/records?term="));
    expect(searchCall, "typing in the picker never reached the server").toBeTruthy();

    const control = document.querySelector('[data-custom-field="delivery.department"]');
    expect([...control.options].some((option) => option.value === DEPARTMENT_B)).toBe(true);
  });

  it("hides the field and says why when the module offers nothing", async () => {
    /*
     * The refusal that already governs the other two source kinds, reaching records unchanged. An empty picker
     * is the BL-050 defect; no field plus a console line is the behaviour.
     */
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const inner = global.fetch;
    global.fetch = async (url, init) =>
      url.startsWith("/Tasks/api/field-definitions/delivery.department/records")
        ? { ok: false, status: 404, json: async () => ({ reason_code: "FIELD_OPTIONS_UNRESOLVED" }) }
        : inner(url, init);

    await boot("create");

    expect(document.querySelector('[data-custom-field="delivery.department"]')).toBeNull();
    expect(document.getElementById("taskCustomFields").classList.contains("d-none")).toBe(true);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  beforeEach(() => stubFetch(null));
});
