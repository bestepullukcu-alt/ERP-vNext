const { loadScript } = require("./load-script");

/*
 * The WorkCenterNext FULL-PAGE detail view, driven through the real DOM.
 *
 * app.js had no test harness at all (BL-033), which is why two of its defects were only ever found by someone
 * looking at a screen: cards printed for capabilities the provider never declared, and "add subtask" answering
 * "enter a title" for a title that had been entered. This boots the real module against jsdom and drives it the
 * way a person does — type, click — so those are reproducible instead of argued about.
 */
const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

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
  title: { kind: "display", text: "Yeni maliyet merkezi açılış talebi", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  // EXACTLY what the live projection declared for this task — no timeTracking, no activity, no checklist.
  workItemCapabilities: ["planning", "execution", "subtasks"],
  subtasks: { mode: "full", items: [] },
  actions: [{
    code: "complete",
    label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-30T00:00:00+00:00"
}, overrides);

/** Boots the real app.js on a detail page holding one projection item. */
const bootDetailPage = (item, options) => {
  ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
    .forEach((key) => { delete global[key]; });

  global.WCN = { t: (key) => key, tf: (key) => key, tn: (key) => key };
  document.body.innerHTML =
    `<div id="wcnApp" data-wcn-page="detail" data-wcn-item-id="${TASK_ID}" data-wcn-fixtures=""></div>`;

  loadScript(scriptRoot + "fixture-contract.js");
  // detailHtml bails to an "invalid" placeholder without the resolver, which would make every
  // "this card is absent" assertion below pass for the wrong reason.
  loadScript(scriptRoot + "task-detail-resolver.js");
  loadScript(scriptRoot + "trigger-response-resolver.js");
  loadScript(scriptRoot + "mock-data.js");
  loadScript(scriptRoot + "work-items-api.js");

  const mapped = global.WorkCenterNextApi.mapPayload([item]);
  expect(mapped.errors).toEqual([]);
  // Stub the network at the module seam; everything downstream is the real code.
  global.WorkCenterNextApi.fetchWorkItems = (options && options.neverResolve)
    ? () => new Promise(() => { /* a request that never settles — the page must stay in its loading state */ })
    : () => Promise.resolve({ status: "ok", httpStatus: 200, items: mapped.items, errors: [] });

  const created = [];
  // The Details view used to omit these entirely; `withoutTasksScripts` reproduces that page exactly.
  if (options && options.withoutTasksScripts) {
    delete global.TasksApi;
    delete global.TaskForm;
    loadScript(scriptRoot + "app.js");
    return new Promise((resolve) => setTimeout(() => resolve({ created }), 0));
  }
  global.TasksApi = {
    create: (payload) => { created.push(payload); return Promise.resolve({ ok: true, status: 201, data: { id: "new" } }); },
    get: () => Promise.resolve({ ok: true, status: 200, data: {} }),
    transition: () => Promise.resolve({ ok: true, status: 204 }),
    isConcurrencyConflict: () => false,
    isTransitionBlocked: () => false,
    failureMessage: () => "error"
  };
  global.TaskForm = { buildCreatePayload: (draft) => Object.assign({}, draft) };

  loadScript(scriptRoot + "app.js");
  // boot() is async (it awaits loadWorkItems); let its microtasks drain.
  return new Promise((resolve) => setTimeout(() => resolve({ created }), 0));
};

const app = () => document.getElementById("wcnApp");

describe("the detail page shows only what the provider declared", () => {
  beforeEach(async () => { await bootDetailPage(projectionItem()); });

  // Non-vacuity guard: every "card absent" assertion below would pass on a page that failed to render at all,
  // which is exactly what happened while this harness was missing a script.
  it("actually rendered the task", () => {
    expect(app().querySelector(".wcn-detail-command")).not.toBeNull();
    expect(app().textContent).toContain("Yeni maliyet merkezi");
  });

  /*
   * The live report: this task declared planning/execution/subtasks and the page printed a "Time 0h 0m" card, an
   * "Activity & comments" heading and an empty "Summary" card anyway. The checklist card was correctly absent,
   * which is what proved the gate existed and had simply not been applied to the others.
   */
  it("prints no time card when timeTracking is not declared", () => {
    expect(app().querySelector(".wcn-timesheet")).toBeNull();
  });

  it("prints no activity section when activity is not declared", () => {
    // The HEADING is the assertion: checking only for the list would still pass on a section that renders its
    // "nothing yet" hint, which is the declared-but-empty state and a different thing entirely.
    expect(app().textContent).not.toContain("ActivityLabel");
    expect(app().querySelector(".wcn-audit")).toBeNull();
    expect(app().querySelector(".wcn-composer")).toBeNull();
  });

  it("prints no summary card when the task has no description", () => {
    expect(app().querySelector(".wcn-detail-summary")).toBeNull();
  });

  it("still prints the subtasks card, which IS declared", () => {
    expect(app().querySelector(".wcn-subtask-add")).not.toBeNull();
  });

  it("still prints no checklist card — the gate that already worked", () => {
    expect(app().textContent).not.toContain("ChecklistLabel");
  });
});

describe("the detail page shows a declared capability even when its data is empty", () => {
  // Declared-but-empty is a DIFFERENT state from not-declared, and must not be silently hidden.
  beforeEach(async () => {
    await bootDetailPage(projectionItem({
      workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
      activity: []
    }));
  });

  it("explains an empty activity list instead of hiding the section", () => {
    expect(app().textContent).toContain("ActivityEmpty");
  });
});

describe("adding a subtask reads the title the user typed", () => {
  let created;

  beforeEach(async () => { ({ created } = await bootDetailPage(projectionItem())); });

  /*
   * The reported defect: "it says enter a title, I enter a title, I press Add, nothing happens." The click
   * handler read the input with a document-wide querySelector, so this test drives the real DOM to establish
   * whether the value actually arrives.
   */
  it("sends the typed title to the engine", async () => {
    const input = app().querySelector("[data-wcn-subtask-input]");
    expect(input).not.toBeNull();

    input.value = "Banka ekstresini iste";
    input.dispatchEvent(new window.Event("input", { bubbles: true }));

    app().querySelector("[data-wcn-subtask-add]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    /*
     * The count is deliberately not asserted: each boot in this file adds another delegated document listener
     * (jsdom keeps ONE document per file and app.js binds on it), so a single click reaches every instance
     * loaded so far. What matters is that the click reached addSubtask AT ALL and carried the typed text —
     * which is precisely what "it says enter a title" would disprove.
     */
    expect(created.length).toBeGreaterThan(0);
    created.forEach((payload) => expect(payload.title).toBe("Banka ekstresini iste"));
  });
});

describe("the Details route can actually write", () => {
  const bootWithoutTasksScripts = async (item) => {
    const booted = await bootDetailPage(item);
    return booted;
  };

  it("adds a subtask: the typed title reaches the engine", async () => {
    const { created } = await bootWithoutTasksScripts(projectionItem());

    const input = app().querySelector("[data-wcn-subtask-input]");
    input.value = "CT ikinci deneme";
    app().querySelector("[data-wcn-subtask-add]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(created.length).toBeGreaterThan(0);
    created.forEach((payload) => expect(payload.title).toBe("CT ikinci deneme"));
  });

  it("warns loudly at boot when the host view forgot the write scripts", async () => {
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));
    try {
      await bootDetailPage(projectionItem(), { withoutTasksScripts: true });
    } finally {
      console.error = original;
    }

    // The silence is the defect. A page that cannot write must say so before a user discovers it.
    expect(errors.some((line) => line.includes("Missing required script"))).toBe(true);
    expect(errors.some((line) => line.includes("TasksApi"))).toBe(true);
  });

  it("a failed click is reported instead of vanishing into an unhandled rejection", async () => {
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));
    try {
      await bootDetailPage(projectionItem(), { withoutTasksScripts: true });
      const input = app().querySelector("[data-wcn-subtask-input]");
      input.value = "CT ikinci deneme";
      app().querySelector("[data-wcn-subtask-add]").click();
      await new Promise((resolve) => setTimeout(resolve, 10));
    } finally {
      console.error = original;
    }

    expect(errors.some((line) => line.includes("click handler failed"))).toBe(true);
  });
});

/*
 * The detail page's own header and its conditional sections.
 *
 * Design pass (Figma, CT-approved): the page states the TASK, returns to the list the user left, and shows a
 * loading state instead of claiming the task does not exist while the projection is still in flight.
 */
describe("the detail page header states the task, not the page type", () => {
  beforeEach(async () => { await bootDetailPage(projectionItem()); });

  it("puts the task name in the heading", () => {
    const heading = app().querySelector(".wcn-detail-pagetitle");
    expect(heading).not.toBeNull();
    expect(heading.textContent).toBe("Yeni maliyet merkezi açılış talebi");
  });

  it("offers ONE way back, not a Back button and a breadcrumb for the same place", () => {
    const links = Array.from(app().querySelectorAll('a[href^="/WorkCenterNext"]'));
    expect(links).toHaveLength(1);
  });

  it("returns to the list as the user left it", async () => {
    // The list stores where it was on the way out; the crumb has to honour it.
    window.sessionStorage.setItem("wcn:list-return-url", "/WorkCenterNext?tab=havuz&segment=bekleyen");
    await bootDetailPage(projectionItem());

    const crumb = app().querySelector('.wcn-detail-breadcrumb a');
    expect(crumb.getAttribute("href")).toBe("/WorkCenterNext?tab=havuz&segment=bekleyen");
    window.sessionStorage.removeItem("wcn:list-return-url");
  });

  it("refuses a stored return URL that points somewhere else", async () => {
    window.sessionStorage.setItem("wcn:list-return-url", "https://evil.example/steal");
    await bootDetailPage(projectionItem());

    expect(app().querySelector('.wcn-detail-breadcrumb a').getAttribute("href")).toBe("/WorkCenterNext");
    window.sessionStorage.removeItem("wcn:list-return-url");
  });

  // Pinning means "keep this near the top of MY list"; on a page showing one task there is no list to order.
  it("offers no pin control", () => {
    expect(app().querySelector("[data-wcn-pin]")).toBeNull();
  });
});

describe("the detail page while the projection is still loading", () => {
  it("shows the loading state instead of claiming the task does not exist", async () => {
    // A fetch that never settles leaves the page in its loading state, which is what a slow network produces.
    await bootDetailPage(projectionItem(), { neverResolve: true });

    expect(app().querySelector(".wcn-skeleton")).not.toBeNull();
    // The defect: an error about data that had simply not arrived yet.
    expect(app().textContent).not.toContain("DetailItemNotFound");
  });
});

describe("subtasks and checklist say what they mean", () => {
  it("shows each subtask's status in words", async () => {
    await bootDetailPage(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "Alt iş", status: "in-progress" }] }
    }));

    const row = app().querySelector(".wcn-subtask-status");
    expect(row).not.toBeNull();
    expect(row.textContent).toBe("SubtaskStatusInProgress");
  });

  it("says a blocking checklist blocks completion", async () => {
    await bootDetailPage(projectionItem({
      workItemCapabilities: ["planning", "execution", "subtasks", "checklist"],
      checklist: { version: 1, items: [{ id: "c1", label: { kind: "display", text: "Adım" }, completed: false, required: true, blocking: true, evidenceRequired: false }] }
    }));

    expect(app().textContent).toContain("ChecklistBlocksCompletion");
    expect(app().textContent).not.toContain("ChecklistDoesNotBlock");
  });

  it("says a guidance checklist does not", async () => {
    await bootDetailPage(projectionItem({
      workItemCapabilities: ["planning", "execution", "subtasks", "checklist"],
      checklist: { version: 1, items: [{ id: "c1", label: { kind: "display", text: "Adım" }, completed: false, required: true, blocking: false, evidenceRequired: false }] }
    }));

    expect(app().textContent).toContain("ChecklistDoesNotBlock");
  });
});
