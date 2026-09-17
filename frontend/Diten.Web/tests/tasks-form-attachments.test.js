const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-PSS-MOD0024-ATTACHMENTS-UX-01 — item 1. Files can now be staged on the CREATE form itself: the sender of
 * "fill in this Excel" no longer has to save the task first, reopen it, and add the file from its own detail
 * page. `AddTaskAttachmentHandler` still takes a TaskItemId the create form does not have until `TasksApi.create`
 * answers, so nothing is a multipart create (YAPMA) — the flow is create, THEN upload each staged file in order
 * against the new id, exactly the sequence `openAttachmentDialog`'s own `uploadAttachment` already performs on
 * the detail page (shared logic, never a second upload dialog).
 */

const FORM = fs.readFileSync(
  path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml"), "utf8");

describe("the create form has an Attachments card", () => {
  it("draws the card, its pending list and the add row", () => {
    expect(FORM).toContain('id="taskAttachmentsCard"');
    expect(FORM).toContain('id="taskAttachmentsPending"');
    expect(FORM).toContain('id="taskAttachmentFile"');
    expect(FORM).toContain('id="taskAttachmentKind"');
    expect(FORM).toContain("data-task-attachment-add");
  });

  it("offers Attachment as the default kind, with Evidence and Deliverable selectable", () => {
    const card = FORM.slice(FORM.indexOf('id="taskAttachmentsCard"'), FORM.indexOf('id="taskCustomFields"'));
    expect(card).toMatch(/<option value="Attachment"\s+selected>/);
    expect(card).toContain('<option value="Evidence">');
    expect(card).toContain('<option value="Deliverable">');
  });

  it("no longer tells the reader to go elsewhere — the stale hint is gone with the gap it described", () => {
    expect(FORM).not.toContain("ChecklistAttachmentsAfterSaveHint");
  });
});

const TASK_ID = "11111111-2222-3333-4444-555555555555";

const FORM_HTML = `
  <button type="submit" form="taskForm" id="taskSubmit">save</button>
  <form id="taskForm" data-task-mode="MODE" data-task-id="TASKID" data-task-version="3">
    <input id="taskTitle" />
    <select id="taskAssignmentTarget"><option value="SelfAssigned" selected>self</option></select>
    <select id="taskAssignee"></select>
    <select id="taskPoolPosition"></select>
    <input id="taskDueAt" type="date" />
    <input type="checkbox" id="taskReviewRequired" />
    <input type="checkbox" id="taskApprovalRequired" />
    <input type="checkbox" id="taskEmailNotifications" checked />
    <input type="checkbox" id="taskDelegationAllowed" />
    <div class="d-none" id="taskCustomFields"><div id="taskCustomFieldsRow"></div></div>
    <ul id="taskChecklistItems"></ul>
    <section id="taskAttachmentsCard">
      <ul id="taskAttachmentsPending"></ul>
      <div id="taskAttachmentAddRow">
        <input type="file" id="taskAttachmentFile" />
        <select id="taskAttachmentKind">
          <option value="Attachment" selected>attachment</option>
          <option value="Evidence">evidence</option>
          <option value="Deliverable">deliverable</option>
        </select>
        <button type="button" data-task-attachment-add>add</button>
      </div>
    </section>
  </form>`;

describe("staging and uploading attachments from the create form", () => {
  let sent;
  let uploads;
  let modals;
  let navigated;

  // jsdom's <input type="file">.files is read-only and this build has no DataTransfer to build a FileList
  // through — defining the property directly is the portable workaround (same technique
  // workcenter-next-attachments.test.js uses), made configurable so ONE input can stage several files in turn.
  const putFile = (input, file) => {
    Object.defineProperty(input, "files", { value: [file], writable: false, configurable: true });
  };

  const stubFetch = ({ createFails = false } = {}) => {
    global.fetch = async (url, init) => {
      const ok = (data, status = 200) => ({ ok: true, status, json: async () => ({ data }) });
      const body = init?.body ? JSON.parse(init.body) : null;

      if (url === "/Tasks/api/field-definitions") { return ok([]); }
      if (url === "/Tasks/api/assignable-positions") { return ok([]); }
      if (url === "/Tasks/api/assignable-people") { return ok({ people: [], excluded: null }); }
      if (url === "/Tasks/api/decision-makers") { return ok({ people: [] }); }
      if (url === "/Tasks/api" && init?.method === "POST") {
        sent = body;
        return createFails
          ? { ok: false, status: 422, json: async () => ({ reasonCode: "TASK_ORG_UNIT_MISSING" }) }
          : ok(TASK_ID, 201);
      }
      if (url.startsWith("/Tasks/api/assignment-direction")) { return ok({ isUpward: false }); }
      return ok(null);
    };
  };

  /*
   * `TasksApi.addAttachment` is overridden directly, not exercised through `fetch`/`FormData` — the multipart
   * body's own shape is `TasksApi`'s own contract (see api.js's own tests); what this file is about is WHEN and
   * WITH WHAT ARGUMENTS form-page.js calls it, which is visible without going anywhere near a wire format.
   */
  const stubAddAttachment = (failNames = []) => {
    global.TasksApi.addAttachment = async (taskId, payload) => {
      uploads.push({ taskId, name: payload.file?.name, kind: payload.kind });
      return failNames.includes(payload.file?.name)
        ? { ok: false, status: 400, reasonCode: "VALIDATION_FAILED", data: null }
        : { ok: true, status: 201, data: { id: `att-${uploads.length}` } };
    };
  };

  const stubJQuery = () => {
    const jq = (selectorOrNode) => {
      const nodes = typeof selectorOrNode === "string"
        ? Array.from(document.querySelectorAll(selectorOrNode))
        : [selectorOrNode];
      const api = {
        length: nodes.length,
        each(cb) { nodes.forEach((n, i) => cb.call(n, i, n)); return api; },
        wrap() { return api; }, parent() { return api; },
        hasClass() { return false; }, on() { return api; },
        select2() { return api; }
      };
      return api;
    };
    global.$ = jq;
    global.jQuery = jq;
  };

  const boot = async (mode) => {
    document.body.innerHTML = FORM_HTML.replace("MODE", mode).replace("TASKID", "");
    stubJQuery();
    delete global.TaskForm;
    delete global.TasksApi;
    loadScript("wwwroot/assets/js/backbone-shell.js");
    loadScript("wwwroot/assets/js/shared/diten-checkitem.js");
    loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    stubAddAttachment();
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  const stageFile = (name, kind) => {
    putFile(document.getElementById("taskAttachmentFile"), new File(["x"], name));
    if (kind) { document.getElementById("taskAttachmentKind").value = kind; }
    document.querySelector("[data-task-attachment-add]").click();
  };

  const save = async () => {
    document.getElementById("taskTitle").value = "Excel'i doldur";
    document.getElementById("taskDueAt").value = "2026-09-20";
    document.getElementById("taskSubmit").click();
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  beforeEach(() => {
    sent = null;
    uploads = [];
    modals = [];
    navigated = [];
    window.sessionStorage.clear();
    stubFetch();
    window.HTMLElement.prototype.scrollIntoView = function scrollIntoView() {};
    global.TasksL10n = { t: (key) => key };
    global.DitenModal = {
      success: async (opts) => { modals.push(["success", opts]); },
      error: async (opts) => { modals.push(["error", opts]); },
      warning: async (opts) => { modals.push(["warning", opts]); }
    };
    global.showToast = (message, type) => { void message; void type; };
    global.location = {
      get href() { return navigated[navigated.length - 1] || ""; },
      set href(value) { navigated.push(value); }
    };
  });

  it("stages a file without touching the network — nothing uploads before the task exists", async () => {
    await boot("create");
    stageFile("talimat.xlsx", "Deliverable");

    expect(uploads).toHaveLength(0);
    expect(document.querySelectorAll("[data-task-attachment-row]")).toHaveLength(1);
  });

  it("a staged file can be withdrawn before saving", async () => {
    await boot("create");
    stageFile("talimat.xlsx");
    const row = document.querySelector("[data-task-attachment-row]");
    row.querySelector("[data-task-attachment-remove]").click();

    expect(document.querySelectorAll("[data-task-attachment-row]")).toHaveLength(0);
  });

  it("creates the task first, THEN uploads each staged file in order — never a multipart create", async () => {
    await boot("create");
    stageFile("talimat.xlsx", "Attachment");
    stageFile("kanit.pdf", "Evidence");
    await save();

    expect(sent, "the create body carried a file").not.toHaveProperty("file");
    expect(uploads).toEqual([
      { taskId: TASK_ID, name: "talimat.xlsx", kind: "Attachment" },
      { taskId: TASK_ID, name: "kanit.pdf", kind: "Evidence" }
    ]);
  });

  it("a create with nothing staged behaves exactly as before — zero upload calls", async () => {
    await boot("create");
    await save();

    expect(sent).not.toBeNull();
    expect(uploads).toHaveLength(0);
    expect(modals).toHaveLength(0);
    expect(navigated).not.toHaveLength(0);
  });

  it("a refused create never attempts to upload — there is no task id to attach to", async () => {
    stubFetch({ createFails: true });
    await boot("create");
    stageFile("talimat.xlsx");
    await save();

    expect(uploads).toHaveLength(0);
    expect(modals.map(([kind]) => kind)).toContain("error");
  });

  // ── the partial-failure contract (WP AC1) ────────────────────────────────────────────────────────────────

  it("keeps the task on a partial upload failure — the create is never rolled back", async () => {
    await boot("create");
    stubAddAttachment(["kanit.pdf"]);
    stageFile("talimat.xlsx");
    stageFile("kanit.pdf");
    await save();

    expect(sent, "the create never happened").not.toBeNull();
    // BOTH were attempted — a later failure must not stop earlier or later files from being tried.
    expect(uploads.map((u) => u.name)).toEqual(["talimat.xlsx", "kanit.pdf"]);
  });

  it("names which file failed and sends the reader to the task's own page to retry", async () => {
    await boot("create");
    stubAddAttachment(["kanit.pdf"]);
    stageFile("talimat.xlsx");
    stageFile("kanit.pdf");
    await save();

    const [kind, opts] = modals[modals.length - 1];
    expect(kind).toBe("error");
    expect(opts.message).toContain("kanit.pdf");
    expect(opts.message).not.toContain("talimat.xlsx");
    expect(navigated[navigated.length - 1]).toBe(`/Tasks/${TASK_ID}`);
  });

  it("a fully failed upload set still keeps the task and still offers the retry link", async () => {
    await boot("create");
    stubAddAttachment(["talimat.xlsx"]);
    stageFile("talimat.xlsx");
    await save();

    expect(sent).not.toBeNull();
    expect(modals.map(([kind]) => kind)).toContain("error");
    expect(navigated[navigated.length - 1]).toBe(`/Tasks/${TASK_ID}`);
  });

  // ── edit mode never offers this — an edit posts no attachment list ──────────────────────────────────────

  it("removes the card on the edit page", async () => {
    await boot("edit");
    expect(document.getElementById("taskAttachmentsCard")).toBeNull();
  });
});
