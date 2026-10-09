const { loadScript } = require("./load-script");

/*
 * WP-WF-ENGINE-CANDIDATES-01 D2 fix round 2, item 2 — "SAVE AGAIN" IS A RETRY, NOT A COLLISION WITH ITSELF.
 *
 * An edit that switches approval (or review) off is WRITTEN first; withdrawing the running MOD-0023 workflow comes
 * after. When that withdraw fails, the server answers 409 TASK_APPROVAL_CANCEL_FAILED / TASK_REVIEW_CANCEL_FAILED and
 * the sentence on screen says "your changes are saved … save again to retry". The edit DID land, so the task's
 * version moved — and the form kept the old one in `data-task-version`. The second save then lost to the first one:
 * TASK_CONCURRENCY_CONFLICT, every time.
 *
 * The refusal now carries the version the edit landed at (`data.version`, the envelope's own data field), and the
 * form keeps it. No sentence changes here: the message is still the one TasksApi maps for the code.
 */

const TASK_ID = "11111111-2222-3333-4444-555555555555";

const FORM_HTML = `
  <button type="submit" form="taskForm" id="taskSubmit">save</button>
  <form id="taskForm" data-task-mode="edit" data-task-id="${TASK_ID}" data-task-version="3">
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

describe("a save refused AFTER it landed hands the form the version it landed at", () => {
  let puts;
  let answers;
  let modals;

  const stubFetch = () => {
    global.fetch = async (url, init) => {
      const ok = (data, status = 200) => ({ ok: true, status, json: async () => ({ data }) });
      if (url === `/Tasks/api/${TASK_ID}`) {
        if (init?.method === "PUT") {
          puts.push(JSON.parse(init.body));
          return answers.shift();
        }
        return ok({ id: TASK_ID, version: 3, title: "Dosya", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned" });
      }
      if (url === "/Tasks/api/assignable-people") { return ok({ people: [], excluded: null }); }
      if (url === "/Tasks/api/decision-makers") { return ok({ people: [] }); }
      if (url.startsWith("/Tasks/api/assignment-direction")) { return ok({ isUpward: false }); }
      return ok([]);
    };
  };

  const refusal = (reasonCode, data) => ({
    ok: false, status: 409, json: async () => ({ reason_code: reasonCode, data, errors: ["refused"] })
  });

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

  const settle = async () => {
    for (let i = 0; i < 5; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  };

  const boot = async () => {
    document.body.innerHTML = FORM_HTML;
    stubJQuery();
    delete global.TaskForm;
    delete global.TasksApi;
    loadScript("wwwroot/assets/js/backbone-shell.js");
    loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    await settle();
  };

  const save = async () => {
    document.getElementById("taskTitle").value = "Dosya hazırla";
    document.getElementById("taskDueAt").value = "2026-09-01";
    document.getElementById("taskSubmit").click();
    await settle();
  };

  beforeEach(() => {
    puts = [];
    answers = [];
    modals = [];
    window.sessionStorage.clear();
    stubFetch();
    window.HTMLElement.prototype.scrollIntoView = function scrollIntoView() {};
    global.TasksL10n = { t: (key) => key };
    global.DitenModal = {
      success: async (opts) => { modals.push(["success", opts]); },
      error: (opts) => { modals.push(["error", opts]); },
      warning: async (opts) => { modals.push(["warning", opts]); }
    };
    global.showToast = () => {};
    global.location = { get href() { return ""; }, set href(value) { /* navigation is not under test */ } };
  });

  it.each(["TASK_APPROVAL_CANCEL_FAILED", "TASK_REVIEW_CANCEL_FAILED"])(
    "after %s the second save sends the version the first one landed at", async (code) => {
      answers.push(refusal(code, { version: 4 }), { ok: true, status: 204, json: async () => { throw new Error("no body"); } });
      await boot();

      await save();
      expect(puts[0].expectedVersion).toBe(3);
      expect(modals.map(([kind]) => kind)).toContain("error"); // the refusal is still shown, as before
      expect(document.getElementById("taskForm").getAttribute("data-task-version")).toBe("4");

      await save();
      expect(puts[1].expectedVersion, "the retry collided with the edit it is retrying").toBe(4);
    });

  it("a refusal that carries no version leaves the form's version alone", async () => {
    answers.push(refusal("TASK_CONCURRENCY_CONFLICT", null));
    await boot();

    await save();

    expect(document.getElementById("taskForm").getAttribute("data-task-version")).toBe("3");
  });
});
