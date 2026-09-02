const { loadScript } = require("./load-script");

// MOD-0024 — the Task Center owns the ONE quick-create surface. These cover the behaviour that made the
// duplicate-list decision safe: the draft handover to the detailed form (K9/DEV-1), the real POST, and the
// event that lets app.js stay the sole owner of the work-item list.
describe("Task Center quick create", () => {
  let created;
  let originalLocation;

  const setupDom = () => {
    document.body.innerHTML = `
      <div id="taskQuickCreate">
        <input id="quickTitle" />
        <select id="quickTarget">
          <option value="SelfAssigned" selected></option>
          <option value="Person"></option>
          <option value="PositionPool"></option>
        </select>
        <div data-task-field="assignee"><input id="quickAssignee" /></div>
        <div data-task-field="poolPosition"><select id="quickPoolPosition"></select></div>
        <input id="quickDueAt" type="date" />
        <select id="quickPriority"><option value="Medium" selected></option></select>
        <button id="quickSubmit"></button>
        <button id="quickMoreOptions"></button>
      </div>`;
  };

  beforeEach(() => {
    setupDom();
    delete global.WcnQuickCreate;
    delete global.TaskForm;

    created = [];
    global.TasksL10n = { t: (key) => key };
    // The real api.js is loaded below so failureMessage() (reason-code → message) is exercised, not stubbed;
    // only the transport is replaced.
    global.fetch = async () => { throw new Error("no network in tests"); };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    global.TasksApi.create = async (payload) => {
      created.push(payload);
      /*
       * MEASURED against the running engine 2026-09-02: a create answers
       *     { "data": "69c76120-c48a-40cb-9762-25d209f4b0f0", "statusCode": 201, ... }
       * `data` is the new task's ID as a STRING. This double used to hand back
       * `{ id, title }` -- an object the server never sends -- so the assertion below
       * ("announces the new task") passed while the live toast rendered
       * "· İşlerim'e eklendi": a sentence opening on its own separator, because the
       * listener read `.title` off a string. A double kinder than the wire makes its
       * own test vacuous; this one now says what the server says.
       */
      return { ok: true, status: 201, data: "69c76120-c48a-40cb-9762-25d209f4b0f0" };
    };
    global.TasksApi.assignablePositions = async () => ({ ok: true, data: [] });
    global.DitenModal = {
      calls: [],
      warning(opts) { this.calls.push({ type: "warning", ...opts }); },
      error(opts) { this.calls.push({ type: "error", ...opts }); },
      success(opts) { this.calls.push({ type: "success", ...opts }); return Promise.resolve({}); }
    };
    // Bootstrap offcanvas stub — records show/hide without needing the real widget.
    global.shown = [];
    global.bootstrap = {
      Offcanvas: {
        getOrCreateInstance: () => ({
          show: () => global.shown.push("show"),
          hide: () => global.shown.push("hide")
        })
      }
    };

    originalLocation = window.location;
    delete window.location;
    window.location = { href: "http://localhost/WorkCenterNext" };

    // The real TaskForm: payload building and the shared draft must be exercised, not mocked away.
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/quick-create.js");
  });

  afterEach(() => {
    window.location = originalLocation;
  });

  describe("opening", () => {
    it("shows the offcanvas and defaults the required due date", () => {
      expect(global.WcnQuickCreate.open()).toBe(true);
      expect(global.shown).toContain("show");
      // A due date is mandatory for all three targets, so an empty required field is not a good starting state.
      expect(document.getElementById("quickDueAt").value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    });

    it("does not clobber a due date the user already picked", () => {
      document.getElementById("quickDueAt").value = "2026-09-09";
      global.WcnQuickCreate.open();
      expect(document.getElementById("quickDueAt").value).toBe("2026-09-09");
    });

    it("reports failure instead of throwing when the offcanvas is absent", () => {
      document.body.innerHTML = "";
      expect(global.WcnQuickCreate.open()).toBe(false);
    });
  });

  describe("submitting", () => {
    const fillValid = () => {
      document.getElementById("quickTitle").value = "  Inspect line 3  ";
      document.getElementById("quickDueAt").value = "2026-08-01";
    };

    it("refuses an incomplete draft without calling the API", async () => {
      await global.WcnQuickCreate.submit();

      expect(created).toEqual([]);
      expect(global.DitenModal.calls.map((c) => c.type)).toEqual(["warning"]);
    });

    it("posts a payload built by TaskForm, not a hand-rolled one", async () => {
      fillValid();
      await global.WcnQuickCreate.submit();

      expect(created).toHaveLength(1);
      const payload = created[0];
      expect(payload.title).toBe("Inspect line 3");
      expect(payload.assignmentTarget).toBe("SelfAssigned");
      // The server owns these; sending them would be a contract violation.
      expect(payload).not.toHaveProperty("lifecycle");
      expect(payload).not.toHaveProperty("spentHours");
    });

    it("sends no assignee for a pool task", async () => {
      fillValid();
      document.getElementById("quickTarget").value = "PositionPool";
      document.getElementById("quickAssignee").value = "11111111-1111-1111-1111-111111111111";
      document.getElementById("quickPoolPosition").innerHTML =
        '<option value="99999999-9999-9999-9999-999999999999" selected></option>';

      await global.WcnQuickCreate.submit();

      expect(created[0].poolPositionId).toBe("99999999-9999-9999-9999-999999999999");
      expect(created[0].assigneeUserId).toBeNull();
    });

    it("announces the new task instead of mutating Task Center state", async () => {
      fillValid();
      const events = [];
      document.addEventListener("wcn:task-created", (e) => events.push(e.detail));

      await global.WcnQuickCreate.submit();

      expect(events).toHaveLength(1);
      expect(events[0].title).toBe("Inspect line 3");
      expect(global.shown).toContain("hide");
    });

    it("distinguishes a permission failure from a generic one", async () => {
      fillValid();
      global.TasksApi.create = async () => ({ ok: false, status: 403, data: null });

      await global.WcnQuickCreate.submit();

      const modal = global.DitenModal.calls.at(-1);
      expect(modal.type).toBe("error");
      expect(modal.title).toBe("errorNoAccess");
    });

    it("names the real cause when no organization unit could be resolved", async () => {
      // The failure a user without a position actually hits — a generic "an error occurred" here cost hours.
      fillValid();
      global.TasksApi.create = async () => ({
        ok: false, status: 400, reasonCode: "ORGANIZATION_UNIT_UNRESOLVED", data: null
      });

      await global.WcnQuickCreate.submit();

      const modal = global.DitenModal.calls.at(-1);
      expect(modal.type).toBe("error");
      expect(modal.title).toBe("errorOrganizationUnitUnresolved");
    });

    it("falls back to a generic message for an unmapped failure", async () => {
      fillValid();
      global.TasksApi.create = async () => ({ ok: false, status: 500, reasonCode: "SOMETHING_NEW", data: null });

      await global.WcnQuickCreate.submit();

      expect(global.DitenModal.calls.at(-1).title).toBe("errorOccurred");
    });

    it("emits no created event when the API rejects", async () => {
      fillValid();
      global.TasksApi.create = async () => ({ ok: false, status: 500, data: null });
      const events = [];
      document.addEventListener("wcn:task-created", (e) => events.push(e.detail));

      await global.WcnQuickCreate.submit();

      expect(events).toEqual([]);
    });
  });

  describe("handover to the detailed form (K9 / DEV-1)", () => {
    it("carries the current draft to /Tasks/Create without losing it", () => {
      document.getElementById("quickTitle").value = "Half typed";
      document.getElementById("quickTarget").value = "Person";
      document.getElementById("quickAssignee").value = "11111111-1111-1111-1111-111111111111";
      document.getElementById("quickDueAt").value = "2026-08-05";

      global.WcnQuickCreate.openDetailed();

      const draft = global.TaskForm.readDraft();
      expect(draft.title).toBe("Half typed");
      expect(draft.assignmentTarget).toBe("Person");
      expect(draft.assigneeUserId).toBe("11111111-1111-1111-1111-111111111111");
      expect(draft.dueAt).toBe("2026-08-05");
      expect(window.location.href).toBe("/Tasks/Create");
    });
  });
});
