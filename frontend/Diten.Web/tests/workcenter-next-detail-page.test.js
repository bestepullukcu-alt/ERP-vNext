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

/*
 * The gates card. It REPORTS what must happen before work can proceed — and never offers a way to decide it.
 * The decision belongs to MOD-0023 (charter Binding A); MOD-0024 has already been caught growing a second
 * approval engine once, so the boundary is asserted rather than assumed.
 */
describe("the gates card reports governance without deciding it", () => {
  const withGates = (gates) => projectionItem({ gates });

  it("says so when nothing is required", async () => {
    await bootDetailPage(withGates({
      approval: { required: false, status: "notRequired" },
      review: { required: false, status: "notRequired" }
    }));

    expect(app().textContent).toContain("GatesLabel");
    // "No approval needed" is an answer the holder wants — it is not the same as a gate that is satisfied.
    expect(app().textContent).toContain("GateStatusNotRequired");
  });

  it("names who an outstanding approval is waiting on", async () => {
    await bootDetailPage(withGates({
      approval: {
        required: true,
        status: "pending",
        decider: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" }
      },
      review: { required: false, status: "notRequired" }
    }));

    expect(app().textContent).toContain("GateStatusPending");
    expect(app().textContent).toContain("Deniz Koç");
  });

  it("reports a required review that has not been reached yet", async () => {
    await bootDetailPage(withGates({
      approval: { required: false, status: "notRequired" },
      review: { required: true, status: "required" }
    }));

    expect(app().textContent).toContain("GateStatusRequired");
  });

  // BOUNDARY: reporting a gate must never become operating one.
  it("offers no approve or reject control", async () => {
    await bootDetailPage(withGates({
      approval: { required: true, status: "pending" },
      review: { required: true, status: "pending" }
    }));

    const gates = app().querySelector(".wcn-gates");
    expect(gates).not.toBeNull();
    expect(gates.querySelector("button")).toBeNull();
    expect(app().querySelector('[data-wcn-action="approve"]')).toBeNull();
    expect(app().querySelector('[data-wcn-action="reject"]')).toBeNull();
  });

  it("prints no card at all when the provider sends no gates", async () => {
    await bootDetailPage(projectionItem());
    expect(app().querySelector(".wcn-gates")).toBeNull();
  });
});

describe("a cancelled subtask is not reported as unstarted work", () => {
  it("labels it cancelled", async () => {
    await bootDetailPage(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "İptal edilen iş", status: "cancelled" }] }
    }));

    const status = app().querySelector(".wcn-subtask-status");
    expect(status.textContent).toBe("SubtaskStatusCancelled");
    expect(status.textContent).not.toBe("SubtaskStatusNotStarted");
  });
});

/*
 * The layout: content on the left, everything actionable on the right.
 *
 * The design's point is that reading and acting are different jobs. With actions inline among the content
 * cards, the one control the page exists for sat wherever its card happened to land.
 */
describe("the detail page separates what the work IS from what you can DO", () => {
  beforeEach(async () => { await bootDetailPage(projectionItem()); });

  it("puts the action rail in the right column and the content in the left", () => {
    const rail = app().querySelector(".wcn-detail-rail");
    const content = app().querySelector(".wcn-detail-content");
    expect(rail).not.toBeNull();
    expect(content).not.toBeNull();
    expect(rail.querySelector(".wcn-actrail")).not.toBeNull();
    // Actions must not also be sitting among the content cards.
    expect(content.querySelector(".wcn-actrail")).toBeNull();
  });

  it("stacks instead of vanishing on a narrow screen", () => {
    // col-12 is what makes the rail a full-width block below the content rather than a squeezed sliver.
    expect(app().querySelector(".wcn-detail-rail").className).toContain("col-12");
  });

  it("leads the rail with the primary action at full size", () => {
    const first = app().querySelector(".wcn-actrail .wcn-act");
    expect(first.className).toContain("wcn-act-primary");
  });
});

describe("each action says what it will do", () => {
  it("explains the primary action", async () => {
    await bootDetailPage(projectionItem());
    // "Complete" alone does not say where the work goes.
    expect(app().textContent).toContain("OutcomeComplete");
  });

  it("shows nothing and warns once for an action it has no outcome text for", async () => {
    const warnings = [];
    const original = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));
    try {
      await bootDetailPage(projectionItem({
        actions: [{
          code: "teleport",
          label: { kind: "display", text: "Teleport", locale: "und" },
          semanticType: "teleport",
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
        primaryActionCode: "teleport"
      }));
    } finally {
      console.warn = original;
    }

    // The button still renders — it is the provider's action — but its consequence is not guessed at.
    expect(app().querySelector(".wcn-act-btn")).not.toBeNull();
    expect(app().querySelector(".wcn-act-outcome")).toBeNull();
    expect(warnings.some((w) => w.includes("No outcome text for action \"teleport\""))).toBe(true);
  });
});

describe("the lifecycle strip only draws steps this task will actually reach", () => {
  const gated = (approval, review) => projectionItem({
    gates: {
      approval: { required: approval, status: approval ? "pending" : "notRequired" },
      review: { required: review, status: review ? "required" : "notRequired" }
    }
  });

  it("omits the review step when no review is required", async () => {
    await bootDetailPage(gated(false, false));

    // A step you can see is a step you expect to reach.
    expect(app().textContent).not.toContain("StepReview");
    expect(app().textContent).toContain("StepInProgress");
  });

  it("draws the review step when a review IS required", async () => {
    await bootDetailPage(gated(false, true));
    expect(app().textContent).toContain("StepReview");
  });

  it("puts approval first when it is required, because it gates starting", async () => {
    await bootDetailPage(gated(true, false));

    const labels = Array.from(app().querySelectorAll(".wcn-step-label")).map((n) => n.textContent);
    expect(labels[0]).toBe("StepApproval");
  });

  it("omits the approval step entirely when none is required", async () => {
    await bootDetailPage(gated(false, false));
    expect(app().textContent).not.toContain("StepApproval");
  });

  it("marks Planned as an optional step rather than a station on the main path", async () => {
    await bootDetailPage(gated(false, false));
    expect(app().querySelector(".wcn-step-optional")).not.toBeNull();
  });

  /*
   * Waiting is a PAUSE on the current step, not a step of its own — a healthy task never passes through it, so
   * drawing it as a station would promise a stop that should not happen.
   */
  it("shows waiting as a pause badge, never as a step", async () => {
    await bootDetailPage(projectionItem({
      normalizedStatus: "Waiting",
      taskLifecycle: "Waiting",
      waitingContext: {
        type: "externalInformation",
        reason: { kind: "display", text: "Muhasebeden ekstre bekleniyor", locale: "und" },
        since: "2026-07-26T10:00:00+00:00"
      }
    }));

    expect(app().querySelector(".wcn-step-paused")).not.toBeNull();
    expect(app().textContent).toContain("Muhasebeden ekstre bekleniyor");
    const labels = Array.from(app().querySelectorAll(".wcn-step-label")).map((n) => n.textContent);
    expect(labels).not.toContain("StepWaiting");
  });
});

describe("subtask rows carry who has it and when it is due", () => {
  it("shows the holder and the date when they exist", async () => {
    await bootDetailPage(projectionItem({
      subtasks: {
        mode: "full",
        items: [{
          id: "s1", title: "Ekstreyi iste", status: "in-progress",
          assignee: { id: "u1", displayName: "Merve Şahin" }, dueAt: "2026-08-03T00:00:00+00:00"
        }]
      }
    }));

    expect(app().querySelector(".wcn-subtask-assignee").textContent).toContain("Merve Şahin");
    expect(app().querySelector(".wcn-subtask-due").textContent).toContain("2026-08-03");
  });

  it("shows nothing at all when a subtask has no holder", async () => {
    await bootDetailPage(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "Sahipsiz iş", status: "not-started" }] }
    }));

    // Absent, not a dash: a dash claims the field was checked and found empty.
    expect(app().querySelector(".wcn-subtask-assignee")).toBeNull();
    expect(app().querySelector(".wcn-subtask-due")).toBeNull();
    expect(app().querySelector(".wcn-subtask-title")).not.toBeNull();
  });

  it("opens the subtask's own page rather than an inline editor", async () => {
    await bootDetailPage(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "Alt iş", status: "not-started" }] }
    }));

    expect(app().querySelector('[data-wcn-open-task="s1"]')).not.toBeNull();
  });
});

/*
 * ONE place per action, and one notice that names the right wait.
 *
 * Both defects here are the same shape as several others this week: a new mechanism was added and the old one
 * was left in place. The command card kept its own action row after the rail was built, so every action rendered
 * twice — and two copies disagree the moment one is changed.
 */
describe("every action is rendered exactly once", () => {
  const codesIn = (root) => Array.from(root.querySelectorAll("[data-wcn-action]"))
    .map((node) => node.getAttribute("data-wcn-action"));

  it("renders no action code more than once", async () => {
    await bootDetailPage(projectionItem({
      actions: ["start", "reassign", "cancel"].map((code) => ({
        code,
        label: { kind: "resource", key: `WorkAggregation_Action_${code}` },
        semanticType: code,
        enabled: true,
        source: "provider",
        disabledReasonCode: null,
        disabledReason: null,
        requiresConfirmation: false,
        requiresReason: false,
        requiresEvidence: false,
        supportsBulk: false,
        riskLevel: code === "cancel" ? "destructive" : "normal"
      })),
      primaryActionCode: "start"
    }));

    const codes = codesIn(app());
    const duplicated = codes.filter((code, index) => codes.indexOf(code) !== index);
    expect(duplicated).toEqual([]);
    // Non-vacuity: the assertion above is meaningless if nothing rendered.
    expect(codes).toContain("start");
    expect(codes).toContain("cancel");
  });

  it("keeps every action inside the rail, none in the command card", async () => {
    await bootDetailPage(projectionItem());

    const command = app().querySelector(".wcn-detail-command");
    expect(command).not.toBeNull();
    expect(command.querySelectorAll("[data-wcn-action]")).toHaveLength(0);
    expect(app().querySelector(".wcn-detail-rail [data-wcn-action]")).not.toBeNull();
  });

  // Snooze changes what the viewer sees, not what the task is, so it sits with the personal note — in ONE place.
  it("keeps the personal overlay in one place too", async () => {
    await bootDetailPage(projectionItem());

    expect(app().querySelectorAll("[data-wcn-snooze]")).toHaveLength(1);
    expect(app().querySelector(".wcn-detail-rail [data-wcn-snooze]")).not.toBeNull();
  });
});

describe("the waiting notice names the wait it actually is", () => {
  const waitingOn = (type) => projectionItem({
    normalizedStatus: "Waiting",
    taskLifecycle: "Waiting",
    waitingContext: { type, since: "2026-07-26T10:00:00+00:00" }
  });

  it("says approval when the task waits on an approval decision", async () => {
    await bootDetailPage(waitingOn("approval"));

    expect(app().textContent).toContain("NoticeWaitingApproval");
    // The defect: the external-input wording was used for every kind of wait, so the page contradicted its own
    // gates card ("Approval: waiting for a decision").
    expect(app().textContent).not.toContain("NoticeWaitingExternal");
  });

  it("says external input when that is what it is", async () => {
    await bootDetailPage(waitingOn("externalInformation"));
    expect(app().textContent).toContain("NoticeWaitingExternal");
  });

  it("says review when the task waits to be reviewed", async () => {
    await bootDetailPage(waitingOn("review"));
    expect(app().textContent).toContain("NoticeWaitingReview");
  });

  /*
   * An unknown type can no longer even reach the page: the executable contract declares the vocabulary now, so
   * mapPayload drops the item and reports it. That is a STRONGER guarantee than rendering silence, and it is
   * asserted at the contract level below. The resolver keeps its own warn as defence in depth.
   */
  it("is rejected by the contract before it can reach the page", async () => {
    await bootDetailPage(projectionItem());

    const result = global.WorkCenterNextContract.validateWorkItem(waitingOn("moonPhase"));

    expect(result.valid).toBe(false);
    expect(result.errors.map((e) => e.code)).toContain("WAITING_CONTEXT_TYPE_INVALID");
  });

  it("accepts every type the contract declares", async () => {
    await bootDetailPage(projectionItem());

    global.WorkCenterNextContract.enums.WAITING_CONTEXT_TYPES.forEach((type) => {
      const result = global.WorkCenterNextContract.validateWorkItem(waitingOn(type));
      expect(result.errors.filter((e) => e.code === "WAITING_CONTEXT_TYPE_INVALID")).toEqual([]);
    });
  });
});

/*
 * Subtask quick-edit panel.
 *
 * It is a WRITE surface, and this week the Details route shipped with every write silently broken because the
 * view had not loaded the scripts they need. So the panel's own dependency is declared, and its failure to open
 * is asserted to be loud rather than nothing.
 */
describe("the subtask quick-edit panel", () => {
  const parentWithSubtask = () => projectionItem({
    subtasks: {
      mode: "full",
      items: [{
        id: "11111111-1111-1111-1111-111111111111",
        title: "Ekstreyi iste",
        status: "in-progress",
        assignee: { id: "u1", displayName: "Merve Şahin" },
        dueAt: "2026-08-03T00:00:00+00:00"
      }]
    }
  });

  const openPanel = async () => {
    app().querySelector("[data-wcn-open-task]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));
  };

  it("opens on the row with the subtask's current values", async () => {
    await bootDetailPage(parentWithSubtask());
    await openPanel();

    const panel = app().querySelector("#wcnSubtaskPanel");
    expect(panel).not.toBeNull();
    expect(panel.querySelector('[data-wcn-subtask-field="title"]').value).toBe("Ekstreyi iste");
    expect(panel.querySelector('[data-wcn-subtask-field="dueAt"]').value).toBe("2026-08-03");
  });

  /*
   * Assignee and status are SHOWN but not editable: assignment goes through /reassign, which demands a reason
   * and enforces who may do it, and status goes through the gated transitions. A quick panel that edited them
   * would either drop those rules or ask for a reason, and the first is how a surface starts lying.
   */
  it("shows assignee and status without offering to edit them here", async () => {
    await bootDetailPage(parentWithSubtask());
    await openPanel();

    const panel = app().querySelector("#wcnSubtaskPanel");
    expect(panel.textContent).toContain("Merve Şahin");
    expect(panel.querySelector('[data-wcn-subtask-field="assignee"]')).toBeNull();
    expect(panel.querySelector('[data-wcn-subtask-field="status"]')).toBeNull();
    expect(panel.textContent).toContain("SubtaskQuickEditScope");
  });

  // The panel holds fields that change often; its checklist, dependencies and activity stay on the full page,
  // because two surfaces rendering the same thing eventually disagree.
  it("does not duplicate the full page's deeper sections", async () => {
    await bootDetailPage(parentWithSubtask());
    await openPanel();

    const panel = app().querySelector("#wcnSubtaskPanel");
    expect(panel.querySelector(".wcn-checks")).toBeNull();
    expect(panel.querySelector(".wcn-audit")).toBeNull();
  });

  it("always offers the way out to the full page, with the subtask's own id", async () => {
    await bootDetailPage(parentWithSubtask());
    await openPanel();

    const link = app().querySelector("[data-wcn-open-task-full]");
    expect(link).not.toBeNull();
    expect(link.getAttribute("data-wcn-open-task-full")).toBe("11111111-1111-1111-1111-111111111111");
  });

  it("declares bootstrap as a write dependency, so a page without it says so", async () => {
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));
    try {
      await bootDetailPage(parentWithSubtask(), { withoutTasksScripts: true });
    } finally {
      console.error = original;
    }

    // The panel cannot open without bootstrap; silence would look exactly like a row that is not clickable.
    expect(errors.some((line) => line.includes("Missing required script"))).toBe(true);
    expect(errors.some((line) => line.includes("bootstrap"))).toBe(true);
  });
});

describe("the contract declares the shapes that kept drifting", () => {
  beforeEach(async () => { await bootDetailPage(projectionItem()); });

  const validate = (item) => global.WorkCenterNextContract.validateWorkItem(item);

  it("rejects a waitingOn that is a name with no identity behind it", () => {
    const result = validate(projectionItem({
      normalizedStatus: "Waiting",
      taskLifecycle: "Waiting",
      // The shape app.js's own writer used to produce.
      waitingContext: { type: "externalInformation", waitingOn: { displayName: "Deniz Koç" } }
    }));

    expect(result.valid).toBe(false);
    expect(result.errors.map((e) => e.code)).toContain("WAITING_CONTEXT_WAITING_ON_INVALID");
  });

  it("accepts a typed identity, and accepts none at all", () => {
    const withId = validate(projectionItem({
      normalizedStatus: "Waiting", taskLifecycle: "Waiting",
      waitingContext: { type: "approval", waitingOn: { id: "u1", displayName: "Deniz Koç" } }
    }));
    const withNone = validate(projectionItem({
      normalizedStatus: "Waiting", taskLifecycle: "Waiting",
      waitingContext: { type: "approval", waitingOn: null }
    }));

    expect(withId.errors.map((e) => e.code)).not.toContain("WAITING_CONTEXT_WAITING_ON_INVALID");
    expect(withNone.errors.map((e) => e.code)).not.toContain("WAITING_CONTEXT_WAITING_ON_INVALID");
  });

  it("rejects a subtask status nobody declared", () => {
    const result = validate(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "x", status: "half-done" }] }
    }));

    expect(result.errors.map((e) => e.code)).toContain("SUBTASK_STATUS_INVALID");
  });

  it("accepts every declared subtask status", () => {
    global.WorkCenterNextContract.enums.SUBTASK_STATUSES.forEach((status) => {
      const result = validate(projectionItem({
        subtasks: { mode: "full", items: [{ id: "s1", title: "x", status }] }
      }));
      expect(result.errors.filter((e) => e.code === "SUBTASK_STATUS_INVALID")).toEqual([]);
    });
  });

  it("rejects a subtask holder that is a name with no identity", () => {
    const result = validate(projectionItem({
      subtasks: { mode: "full", items: [{ id: "s1", title: "x", status: "done", assignee: { displayName: "X" } }] }
    }));

    expect(result.errors.map((e) => e.code)).toContain("SUBTASK_ASSIGNEE_INVALID");
  });
});
