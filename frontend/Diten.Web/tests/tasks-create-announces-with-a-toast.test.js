const { loadScript } = require("./load-script");

/*
 * ══ "GÖREV OLUŞTURULDU" IS NEWS, NOT A QUESTION ═══════════════════════════════════════════════════════════
 *
 * MEASURED (2026-09-02, management demo): saving on /Tasks/Create put a dialog in the middle of the screen. A
 * modal takes the whole page hostage and waits — that is its job, and its job is to ask. "Created" asks
 * nothing; it reports. The product already knows this, and says so everywhere else: the Task Center announces
 * through `showToast` (app.js), and every Organization register announces through
 * `DitenDataTable.reloadWithToast`. The detailed task form was the one surface still stopping the reader to
 * tell them something they had just done on purpose.
 *
 * ── THE PART THAT IS NOT "SWAP THE CALL" ─────────────────────────────────────────────────────────────────
 * This save NAVIGATES (to the Task Center, or back to wherever the reader came from). A toast raised on a page
 * that is about to be replaced is a toast nobody sees — and that is precisely why the modal was there, with a
 * comment saying "Awaited so the acknowledgement is actually seen; navigating immediately would kill it".
 * Awaiting a dialog is one answer to that; it is not the only one. The message is HANDED OVER the navigation
 * and raised on the destination — the pattern six Organization screens already use through sessionStorage
 * (Positions/form.js:152, OrganizationUnits/form.js:179, PositionAssignments/form.js:169 …).
 *
 * Those screens each flush their own private key on their own list page, which only works because each of them
 * knows exactly one destination. This save does not: it lands on the Task Center, or on any local returnUrl.
 * So the flush goes where every tenant page passes anyway — backbone-shell.js, next to `handleTempDataToasts`,
 * which is the SERVER-side twin of the very same idea.
 *
 * ── SCOPE ───────────────────────────────────────────────────────────────────────────────────────────────
 * Only the acknowledgement changes. Every REFUSAL on this form stays a modal: a refusal names a field or a
 * reason the reader has to act on, and a message that fades is the wrong carrier for one.
 */

const TASK_ID = "11111111-2222-3333-4444-555555555555";
const PENDING_TOAST_KEY = "diten-pending-toast";

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
  </form>`;

describe("the detailed task form reports a save; it does not stop to ask about it", () => {
  let sent;
  let modals;
  let toasts;
  let navigated;

  const stubFetch = ({ createFails = false } = {}) => {
    global.fetch = async (url, init) => {
      const body = init?.body ? JSON.parse(init.body) : null;
      const ok = (data, status = 200) => ({ ok: true, status, json: async () => ({ data }) });

      if (url === "/Tasks/api/field-definitions") { return ok([]); }
      if (url === "/Tasks/api/assignable-positions") { return ok([]); }
      if (url === "/Tasks/api/assignable-people") { return ok({ people: [], excluded: null }); }
      if (url === "/Tasks/api/decision-makers") { return ok({ people: [] }); }
      if (url === `/Tasks/api/${TASK_ID}`) {
        if (init?.method === "PUT") { sent = body; return ok(null, 204); }
        return ok({ id: TASK_ID, version: 3, title: "Dosya", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned" });
      }
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

  const boot = async (mode, taskId) => {
    document.body.innerHTML = FORM_HTML.replace("MODE", mode).replace("TASKID", taskId || "");
    stubJQuery();
    delete global.TaskForm;
    delete global.TasksApi;
    // The tenant shell, loaded exactly where the layout loads it: BEFORE the page's own scripts. It owns the
    // hand-over key, so a harness without it would be testing a page no reader ever gets.
    loadScript("wwwroot/assets/js/backbone-shell.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  const fillAndSave = async () => {
    document.getElementById("taskTitle").value = "Dosya hazırla";
    document.getElementById("taskDueAt").value = "2026-09-01";
    document.getElementById("taskSubmit").click();
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  beforeEach(() => {
    sent = null;
    modals = [];
    toasts = [];
    navigated = [];
    window.sessionStorage.clear();
    stubFetch();

    // jsdom implements neither; the page calls both when it focuses a missing field, and an unhandled
    // TypeError there would be reported as a failure of whatever test happened to be running.
    window.HTMLElement.prototype.scrollIntoView = function scrollIntoView() {};

    global.TasksL10n = { t: (key) => key };
    // Every modal channel the page can reach, recorded rather than rendered — the assertions are about WHICH
    // channel a message went down, so none of them may be silently unavailable.
    global.DitenModal = {
      success: async (opts) => { modals.push(["success", opts]); },
      error: (opts) => { modals.push(["error", opts]); },
      warning: async (opts) => { modals.push(["warning", opts]); }
    };
    global.showToast = (message, type) => { toasts.push([message, type]); };
    global.location = {
      get href() { return navigated[navigated.length - 1] || ""; },
      set href(value) { navigated.push(value); }
    };
  });

  // ── non-vacuity ───────────────────────────────────────────────────────────────────────────────────────

  it("actually saves — every assertion below is about a save that happened", async () => {
    await boot("create");
    await fillAndSave();

    expect(sent, "the form never posted").not.toBeNull();
    expect(sent.title).toBe("Dosya hazırla");
    expect(navigated, "the page did not navigate after saving").not.toHaveLength(0);
  });

  // ── the claim ─────────────────────────────────────────────────────────────────────────────────────────

  it("raises NO modal when a create succeeds", async () => {
    await boot("create");
    await fillAndSave();

    expect(
      modals,
      `a successful create still opened ${modals.map(([kind]) => kind).join(", ")} — "created" is information, `
      + "and information does not take the screen hostage"
    ).toHaveLength(0);
  });

  it("hands the acknowledgement over the navigation instead of holding the page for it", async () => {
    await boot("create");
    await fillAndSave();

    const pending = JSON.parse(window.sessionStorage.getItem(PENDING_TOAST_KEY));
    expect(pending, "nothing was handed over — the reader lands on the next page with no word of the save")
      .not.toBeNull();
    expect(pending.message).toBe("toastCreated");
    expect(pending.type).toBe("success");
  });

  it("the destination page raises it, exactly once", async () => {
    await boot("create");
    await fillAndSave();

    // The next page: the tenant shell runs, finds the handover and spends it.
    document.body.innerHTML = "";
    loadScript("wwwroot/assets/js/backbone-shell.js");
    global.BackboneShell.init();

    expect(toasts).toEqual([["toastCreated", "success"]]);

    // Spent, not left behind: a reload must not re-announce a save that already happened.
    expect(window.sessionStorage.getItem(PENDING_TOAST_KEY)).toBeNull();
    global.BackboneShell.init();
    expect(toasts).toHaveLength(1);
  });

  it("an EDIT says it was saved, not that it was created", async () => {
    await boot("edit", TASK_ID);
    await fillAndSave();

    expect(modals).toHaveLength(0);
    expect(JSON.parse(window.sessionStorage.getItem(PENDING_TOAST_KEY)).message).toBe("toastSaved");
  });

  // ── the boundary: a refusal is not news ───────────────────────────────────────────────────────────────

  it("a REFUSED save still stops the reader — it names a cause they have to act on", async () => {
    stubFetch({ createFails: true });
    await boot("create");
    await fillAndSave();

    expect(modals.map(([kind]) => kind), "the refusal was demoted to a toast and can be missed").toContain("error");
    // …and nothing was handed over, because nothing happened.
    expect(window.sessionStorage.getItem(PENDING_TOAST_KEY)).toBeNull();
    expect(navigated, "a refused save navigated away from the form the reader must fix").toHaveLength(0);
  });

  it("a missing required field still stops the reader, and never reaches the server", async () => {
    await boot("create");
    document.getElementById("taskSubmit").click();
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }

    expect(modals.map(([kind]) => kind)).toContain("warning");
    expect(sent, "an invalid draft was posted").toBeNull();
    expect(window.sessionStorage.getItem(PENDING_TOAST_KEY)).toBeNull();
  });
});
