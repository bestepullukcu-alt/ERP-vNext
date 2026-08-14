const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * The WorkCenterNext FULL-PAGE detail view, driven through the real DOM.
 *
 * app.js had no test harness at all (BL-033), which is why two of its defects were only ever found by someone
 * looking at a screen: cards printed for capabilities the provider never declared, and "add subtask" answering
 * "enter a title" for a title that had been entered. This boots the real module against jsdom and drives it the
 * way a person does — type, click — so those are reproducible instead of argued about.
 */
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

/**
 * Boots the real app.js on a detail page holding one projection item.
 *
 * The surface is selected by `data-wcn-page="detail"` alone — everything else (module order, the network seam,
 * the TasksApi stub) is shared with the list harness through wcn-boot, so the two cannot drift apart.
 */
const bootDetailPage = (item, options) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item],
  neverResolve: !!(options && options.neverResolve),
  // The Details view used to omit these entirely; `withoutTasksScripts` reproduces that page exactly.
  withoutTasksScripts: !!(options && options.withoutTasksScripts)
});

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
    /*
     * The card still speaks — but WC-1 changed WHICH sentence is honest here. This item is engine-backed and
     * carries no `created` event, which means the log did not cover its beginning; "nothing has been recorded
     * yet" would be true about the record and false about the task. The gap line says the accurate thing, and
     * the two never appear together (they would contradict each other).
     */
    expect(app().querySelector(".wcn-audit-gap")).not.toBeNull();
    expect(app().textContent).toContain("ActivityHistoryStartsHere");
    expect(app().textContent).not.toContain("ActivityEmpty");
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

    // ENTER is the quick-add now: the button was removed when the add row moved to the top of the card and
    // adopted the page's search-input pattern. The write path behind it is unchanged.
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
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
    input.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
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
      // Any click that reaches the async handler will do; the quick-add is keyboard-driven now, so this uses
      // the other control on the same row.
      app().querySelector("[data-wcn-subtask-add-detailed]").click();
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
    // Golden Reference Compact header markup: h5.mb-0 above the breadcrumb, no bespoke title class.
    const heading = app().querySelector(".wcn-details-page h5.mb-0");
    expect(heading).not.toBeNull();
    expect(heading.textContent).toBe("Yeni maliyet merkezi açılış talebi");
  });

  it("wears the Golden Reference detail header, not a header of its own invention", () => {
    // Same block the reference detail page uses. A detail page that styles its own header drifts from every
    // other detail page in the tenant, which is exactly what this pass was for.
    const header = app().querySelector(".wcn-details-page .d-flex.align-items-center.justify-content-between.mb-3");
    expect(header).not.toBeNull();
    expect(header.querySelector("h5.mb-0")).not.toBeNull();
    expect(header.querySelector("nav .breadcrumb.mb-0")).not.toBeNull();
    // The page type is said once, by the breadcrumb's active item — the heading is the task.
    // The harness's t() echoes the key back (there is no dictionary in jsdom), so the KEY is what to assert.
    expect(header.querySelector(".breadcrumb-item.active").textContent.trim()).toBe("DetailPageTitle");
    expect(header.querySelector(".breadcrumb-item.active").textContent).not.toContain("Yeni maliyet");
  });

  it("offers ONE way back, not a Back button and a breadcrumb for the same place", () => {
    const links = Array.from(app().querySelectorAll('a[href^="/WorkCenterNext"]'));
    expect(links).toHaveLength(1);
  });

  it("returns to the list as the user left it", async () => {
    // The list stores where it was on the way out; the crumb has to honour it.
    window.sessionStorage.setItem("wcn:list-return-url", "/WorkCenterNext?tab=havuz&segment=bekleyen");
    await bootDetailPage(projectionItem());

    const crumb = app().querySelector('.wcn-details-page .breadcrumb a');
    expect(crumb.getAttribute("href")).toBe("/WorkCenterNext?tab=havuz&segment=bekleyen");
    window.sessionStorage.removeItem("wcn:list-return-url");
  });

  it("refuses a stored return URL that points somewhere else", async () => {
    window.sessionStorage.setItem("wcn:list-return-url", "https://evil.example/steal");
    await bootDetailPage(projectionItem());

    expect(app().querySelector('.wcn-details-page .breadcrumb a').getAttribute("href")).toBe("/WorkCenterNext");
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

  it("says NOTHING when nothing is required — the card does not appear at all", async () => {
    /*
     * ⚠ THIS REVERSES THIS TEST'S OWN EARLIER CLAIM, on the owner's decision (2026-08-12), and the reason is a
     * measurement rather than a preference. The old rule — "no approval needed is an answer the holder wants" —
     * produced, on a real task, a full-height card whose entire content was the word "Gerekmiyor" twice, above
     * the fold, pushing the state that DID apply below it. A gate that never applied is not part of "where does
     * this stand"; it is the absence of a gate. Gates and dates are now one status card, and a task with
     * neither renders none.
     */
    await bootDetailPage(withGates({
      approval: { required: false, status: "notRequired" },
      review: { required: false, status: "notRequired" }
    }));

    expect(app().textContent).not.toContain("GateStatusNotRequired");
    expect(app().querySelector(".wcn-gates")).toBeNull();
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

    /*
     * A2 made the row two layers: the holder and the date share ONE quiet line under the title instead of
     * being two chips beside it. What they SAY is the claim; which element carries them is layout.
     */
    const meta = app().querySelector(".wcn-subtask-body .wcn-subtask-meta");
    expect(meta.textContent).toContain("Merve Şahin");
    expect(meta.textContent).toContain("2026-08-03");
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

/*
 * Subtask creation, cancellation and the weight cancelled rows carry.
 *
 * Quick-add inherits the parent's holder, which is why it cannot hand a subtask to someone ELSE — the whole
 * reason the detailed panel exists.
 */
describe("creating a subtask in detail", () => {
  /*
   * ⚠ TWO TICKS, NOT ONE — and the reason is the defect this round fixed.
   *
   * The panel used to be drawn immediately and then RE-RENDERED once the assignable-people lookup returned.
   * That second render replaced the node the Bootstrap Offcanvas instance was bound to, so the panel could be
   * opened exactly once per page load and never again (measured live: node #2 at t=83014 with an instance,
   * node #3 at t=83077 without one).
   *
   * The lookup is awaited BEFORE the panel is drawn now, so there is exactly one render — and the node appears
   * a microtask later than it used to. Waiting for the promise chain is what this extra tick is.
   */
  const openCreate = async () => {
    app().querySelector("[data-wcn-subtask-add-detailed]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));
  };

  it("keeps quick-add and offers the detailed panel beside it", async () => {
    await bootDetailPage(projectionItem());

    expect(app().querySelector("[data-wcn-subtask-add]")).not.toBeNull();
    expect(app().querySelector("[data-wcn-subtask-add-detailed]")).not.toBeNull();
  });

  it("can assign the new subtask to somebody else", async () => {
    const { created } = await bootDetailPage(projectionItem());
    await openCreate();

    const title = app().querySelector('[data-wcn-newsubtask-field="title"]');
    title.value = "Ekstreyi Merve istesin";
    title.dispatchEvent(new window.Event("input", { bubbles: true }));

    const assignee = app().querySelector('[data-wcn-newsubtask-field="assigneeUserId"]');
    // The picker offers whoever the server would accept; the option list arrives from assignablePeople.
    assignee.innerHTML = '<option value="u-merve">Merve</option>';
    assignee.value = "u-merve";
    assignee.dispatchEvent(new window.Event("change", { bubbles: true }));

    app().querySelector("[data-wcn-newsubtask-save]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(created.length).toBeGreaterThan(0);
    const payload = created[created.length - 1];
    expect(payload.title).toBe("Ekstreyi Merve istesin");
    expect(payload.assigneeUserId).toBe("u-merve");
    // Quick-add inherits and so can only ever assign to the parent's holder — this is the path that cannot.
    expect(payload.assignmentTarget).toBe("Person");
  });

  it("links the new subtask to the task being viewed, and does not let that be changed", async () => {
    const { created } = await bootDetailPage(projectionItem());
    await openCreate();

    const title = app().querySelector('[data-wcn-newsubtask-field="title"]');
    title.value = "Alt iş";
    title.dispatchEvent(new window.Event("input", { bubbles: true }));
    app().querySelector("[data-wcn-newsubtask-save]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(created[created.length - 1].parentTaskItemId).toBe(TASK_ID);
    // Moving a task under a different parent is a different operation with its own rules.
    expect(app().querySelector('[data-wcn-newsubtask-field="parentTaskItemId"]')).toBeNull();
  });

  it("refuses to create without a title", async () => {
    const { created } = await bootDetailPage(projectionItem());
    await openCreate();

    app().querySelector("[data-wcn-newsubtask-save]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(created).toHaveLength(0);
  });

  it("is covered by the write-dependency check, so a page missing its scripts says so", async () => {
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));
    try {
      await bootDetailPage(projectionItem(), { withoutTasksScripts: true });
    } finally {
      console.error = original;
    }

    expect(errors.some((line) => line.includes("Missing required script"))).toBe(true);
  });
});

describe("cancelling a subtask from its row", () => {
  const withSubtask = (overrides) => projectionItem({
    subtasks: {
      mode: "full",
      items: [Object.assign({ id: "s1", title: "Ekstreyi iste", status: "in-progress" }, overrides)]
    }
  });

  it("offers cancel when the server says this actor may", async () => {
    await bootDetailPage(withSubtask({ canCancel: true }));
    expect(app().querySelector("[data-wcn-subtask-cancel]")).not.toBeNull();
  });

  // The authority is the server's to state; a row must not offer what the server will refuse.
  it("offers nothing when the actor may not", async () => {
    await bootDetailPage(withSubtask({ canCancel: false }));
    expect(app().querySelector("[data-wcn-subtask-cancel]")).toBeNull();
  });

  it("asks before doing it, through the one confirm the app has", async () => {
    const asked = [];
    global.showConfirm = (message) => { asked.push(message); };
    await bootDetailPage(withSubtask({ canCancel: true }));

    app().querySelector("[data-wcn-subtask-cancel]").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    /*
     * Content, not count: every boot in this file leaves another delegated document listener behind (jsdom keeps
     * one document per file), so a single click reaches every instance loaded so far. What matters is that the
     * click asked BEFORE acting, through the app's single MOD-0013 confirm rather than a page-local dialog.
     */
    expect(asked.length).toBeGreaterThan(0);
    asked.forEach((message) => expect(message).toContain("SubtaskCancelConfirm"));
    delete global.showConfirm;
  });

  // It cancels; it does not delete. A subtask is a task, its history stays, and BL-035 will rest on that.
  it("offers no permanent delete on the row", async () => {
    await bootDetailPage(withSubtask({ canCancel: true }));
    expect(app().querySelector("[data-wcn-subtask-delete]")).toBeNull();
  });
});

describe("cancelled subtasks do not read as work", () => {
  it("marks them and sinks them below the live ones", async () => {
    await bootDetailPage(projectionItem({
      subtasks: {
        mode: "full",
        items: [
          { id: "c1", title: "İptal 1", status: "cancelled" },
          { id: "c2", title: "İptal 2", status: "cancelled" },
          { id: "a1", title: "Canlı iş", status: "in-progress" }
        ]
      }
    }));

    const rows = Array.from(app().querySelectorAll(".wcn-subtask"));
    expect(rows).toHaveLength(3);
    // The live one leads; three cancelled rows at the top read as a queue of things to do.
    expect(rows[0].className).toContain("wcn-subtask-in-progress");
    expect(rows[1].className).toContain("wcn-subtask-cancelled");
    expect(rows[2].className).toContain("wcn-subtask-cancelled");
  });
});


/*
 * The Golden Reference alignment pass: this page is one of the tenant's detail pages and has to be built out of
 * the same parts as the rest of them, not out of lookalikes. Reference:
 * Views/DevEnablement/GoldenReferenceCompact/Details.cshtml.
 */
describe("the detail page is built from Golden Reference parts", () => {
  beforeEach(async () => { await bootDetailPage(projectionItem()); });

  it("states the source context in reference preview fields, with icons", () => {
    const fields = Array.from(app().querySelectorAll(".backbone-preview-field"));
    expect(fields.length).toBeGreaterThan(0);
    fields.forEach((field) => {
      expect(field.querySelector("i.bx")).not.toBeNull();
      expect(field.querySelector(".backbone-preview-label")).not.toBeNull();
      expect(field.querySelector(".backbone-preview-value")).not.toBeNull();
    });
  });

  it("omits a field it has no value for instead of printing a dash", async () => {
    // The reference prints "-" for an empty column because its row set is fixed. Here the row set is not fixed:
    // an absent requester means the projection did not state one, and a dash would dress that up as an answer.
    await bootDetailPage(projectionItem({ requester: null }));

    const labels = Array.from(app().querySelectorAll(".backbone-preview-label")).map((el) => el.textContent);
    expect(labels).not.toContain("DetailRequester");
    Array.from(app().querySelectorAll(".backbone-preview-value"))
      .forEach((el) => { expect(el.textContent.trim()).not.toBe("-"); });
  });

  it("uses the reference section headings, not a heading class of its own", () => {
    const headings = Array.from(app().querySelectorAll(".wcn-detail-section h6"));
    expect(headings.length).toBeGreaterThan(0);
    headings.forEach((h) => {
      expect(h.className).toContain("text-uppercase");
      expect(h.className).toContain("text-heading");
      expect(h.className).toContain("fw-semibold");
    });
    expect(app().querySelector(".wcn-detail-h6")).toBeNull();
  });
});

describe("the subtask card is a list card", () => {
  const withSubtasks = (items) => projectionItem({ subtasks: { mode: "full", items } });
  const child = (overrides) => Object.assign({
    id: "11111111-1111-1111-1111-111111111111",
    title: "Bütçe kalemini doğrula",
    status: "not-started",
    assignee: null,
    dueAt: null,
    canCancel: true
  }, overrides);

  it("heads the card with its count and its add controls", async () => {
    await bootDetailPage(withSubtasks([child(), child({ id: "22222222-2222-2222-2222-222222222222" })]));

    const heading = Array.from(app().querySelectorAll(".wcn-detail-section h6"))
      .find((h) => h.textContent.includes("SubtasksLabel"));
    expect(heading).not.toBeNull();
    // The total moved into a badge (and out of the right-hand reading, which now says only how many are done):
    // "ALT GÖREVLER 5" beside "1 / 5 tamam" printed the same number twice.
    expect(heading.querySelector(".wcn-subtask-count").textContent.trim()).toBe("2");

    /*
     * The two add controls used to sit in the card's top-right corner, detached from the input they drove. A2
     * moved them into ONE add row directly under the progress bar; the header now carries the count and the
     * progress reading, which is what a checklist header is for.
     */
    const card = heading.closest(".wcn-detail-section");
    expect(card.querySelector(".wcn-subtask-add [data-wcn-subtask-input]")).not.toBeNull();
    expect(card.querySelector(".wcn-subtask-add [data-wcn-subtask-add-detailed]")).not.toBeNull();
    expect(card.querySelector(".wcn-subtask-progress")).not.toBeNull();
  });

  // Deliberate: a task carries a handful of subtasks. A filter earns its space at fifteen, and this is not that.
  it("carries no search box", async () => {
    await bootDetailPage(withSubtasks([child()]));

    expect(app().querySelector('input[type="search"]')).toBeNull();
    Array.from(app().querySelectorAll("input")).forEach((input) => {
      expect((input.getAttribute("placeholder") || "").toLowerCase()).not.toContain("search");
      expect((input.getAttribute("placeholder") || "")).not.toContain("Ara");
    });
  });

  it("keeps exactly ONE quick-add control, and it is the input itself", async () => {
    /*
     * There used to be an input plus a header button that only focused it — two controls for one action. The
     * input now carries the parent id and Enter submits, so the thing you type into IS the thing that adds.
     */
    await bootDetailPage(withSubtasks([child()]));

    const inputs = app().querySelectorAll("[data-wcn-subtask-input]");
    expect(inputs).toHaveLength(1);
    expect(inputs[0].getAttribute("data-wcn-subtask-add")).toBe(TASK_ID);
    expect(app().querySelectorAll("button[data-wcn-subtask-add]"), "the old add button is back").toHaveLength(0);
  });
});

describe("the action rail hides only what is destructive", () => {
  const action = (overrides) => Object.assign({
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
  }, overrides);

  const cancelAction = action({
    code: "cancel",
    label: { kind: "resource", key: "WorkAggregation_Action_Cancel" },
    semanticType: "cancel",
    riskLevel: "destructive"
  });

  it("draws a destructive action in the open, in its own tier — not folded into a menu", async () => {
    /*
     * ⚠ THIS ASSERTION REVERSED, deliberately, and the reasoning is worth keeping.
     *
     * It used to require `cancel` to live inside a "Diğer aksiyonlar" kebab and to be absent from the open
     * rail. The intent was safety. Measured against what the menu actually bought: the reader who wants to
     * cancel hunts for it, and the reader who does not is protected by the CONFIRM DIALOG, not by the menu —
     * which is also where the action's warning sentence now leads. What the menu really bought was a page that
     * could cancel a task without ever showing the word.
     *
     * So the destructive tier is visible, last, under a rule, in the danger colour. Still not duplicated: it
     * appears exactly once, which is the half of the old assertion that was always right.
     */
    await bootDetailPage(projectionItem({ actions: [action(), cancelAction] }));

    expect(app().querySelector(".wcn-actrail-menu"), "the kebab came back").toBeNull();
    /*
     * Scoped to the CARD, not the `<ul>`: the destructive tier became its own block outside the action list when
     * the card's padding moved down to the blocks, so the rule between them could reach the card's edges.
     */
    const drawn = app().querySelectorAll('.wcn-acts [data-wcn-action="cancel"]');
    expect(drawn, "the destructive action is not drawn exactly once").toHaveLength(1);
    expect(drawn[0].closest(".wcn-act").className).toContain("wcn-act-destructive");
    // Bare, not tinted: in this theme a `btn-label-*` tint reads as DISABLED, which is how the card's most
    // important control came to look switched off. Exactly one button carries a fill and it is the primary.
    expect(drawn[0].className).toContain("wcn-act-bare-danger");
  });

  it("leaves the ordinary actions open, with the sentence that says what they do", async () => {
    await bootDetailPage(projectionItem({ actions: [action(), cancelAction] }));

    const open = app().querySelector('.wcn-actrail [data-wcn-action="complete"]');
    expect(open).not.toBeNull();
    expect(app().querySelector('.dropdown-menu [data-wcn-action="complete"]')).toBeNull();
    // The outcome line is the reason the rail is open in the first place; a kebab would swallow it.
    expect(open.closest(".wcn-act").querySelector(".wcn-act-outcome")).not.toBeNull();
  });

  it("grows no overflow menu when nothing on offer is destructive", async () => {
    await bootDetailPage(projectionItem({ actions: [action()] }));

    expect(app().querySelector(".wcn-actrail-menu")).toBeNull();
  });
});

describe("the guidance banner says what the task needs from the reader", () => {
  const bannerText = () => {
    const banner = app().querySelector(".wcn-guidance");
    return banner ? banner.textContent.trim() : null;
  };

  it("asks an unaccepted task's holder to accept it", async () => {
    await bootDetailPage(projectionItem({
      admissionState: "pendingAcceptance",
      assignmentMode: "offered",
      ownershipState: "assigned",
      normalizedStatus: "Pending",
      taskLifecycle: "Open",
      executionState: "notStarted"
    }));
    expect(bannerText()).toBe("GuidancePendingAcceptance");
  });

  it("tells a pooled task's reader to claim it", async () => {
    await bootDetailPage(projectionItem({
      admissionState: "pendingClaim",
      assignmentMode: "groupQueue",
      ownershipState: "unowned",
      normalizedStatus: "Pending",
      taskLifecycle: "Open",
      executionState: "notStarted",
      assignee: null,
      // WC-3: groupQueue work must name its queue, or the contract rejects it and mapPayload drops the item.
      pool: { id: "7c1e5a90-3f2b-4d18-9e77-2a5b6c8d0e13", label: { kind: "display", text: "CFO — Genel Merkez", locale: "und" } }
    }));
    expect(bannerText()).toBe("GuidancePendingClaim");
  });

  it("names the holder's own reason when the task is paused with one", async () => {
    await bootDetailPage(projectionItem({
      normalizedStatus: "Waiting",
      taskLifecycle: "Waiting",
      executionState: "paused",
      waitingContext: {
        type: "approval",
        waitingOn: { id: "cccccccc-cccc-cccc-cccc-cccccccccccc", isCurrentUser: false },
        reason: { kind: "display", text: "Bütçe onayı bekleniyor", locale: "tr" },
        since: "2026-07-25T09:00:00+00:00",
        expectedUntil: null
      }
    }));
    // The "...because X" wording, not the bare one — the reason is the whole point when there is one.
    expect(bannerText()).toBe("GuidanceWaitingBecause");
  });

  it("falls back to the bare wording when the pause carries no reason", async () => {
    await bootDetailPage(projectionItem({
      normalizedStatus: "Waiting",
      taskLifecycle: "Waiting",
      executionState: "paused",
      waitingContext: {
        type: "approval",
        waitingOn: { id: "cccccccc-cccc-cccc-cccc-cccccccccccc", isCurrentUser: false },
        reason: null,
        since: "2026-07-25T09:00:00+00:00",
        expectedUntil: null
      }
    }));
    expect(bannerText()).toBe("GuidanceWaiting");
  });

  // A banner for every state would be noise; a banner that guesses would be a lie. Silence is the correct output.
  it("says nothing for a state it has no guidance for", async () => {
    await bootDetailPage(projectionItem());
    expect(app().querySelector(".wcn-guidance")).toBeNull();
  });

  it("says nothing for an admission state outside its map", async () => {
    await bootDetailPage(projectionItem({
      admissionState: "pendingOffer",
      assignmentMode: "offered",
      ownershipState: "unowned",
      normalizedStatus: "Pending",
      taskLifecycle: "Open",
      executionState: "notStarted",
      assignee: null
    }));
    expect(app().querySelector(".wcn-guidance")).toBeNull();
  });

  // Tenant-side strings ship in all seven languages or they ship broken for five of them.
  it("carries its wording in every tenant language", () => {
    const keys = [
      "GuidancePendingAcceptance", "GuidancePendingClaim", "GuidanceApprovalPending",
      "GuidanceReviewPending", "GuidanceWaiting", "GuidanceWaitingBecause"
    ];
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((locale) => {
      const xml = fs.readFileSync(
        path.join(__dirname, "..", "Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${locale}.resx`),
        "utf8");
      keys.forEach((key) => {
        const match = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(xml);
        expect(match, `${key} missing from ${locale}`).not.toBeNull();
        expect(match[1].trim().length).toBeGreaterThan(0);
      });
    });
  });
});

/*
 * BL-028 on screen: what is stopping this work, said in words, with the blocked control still visible.
 *
 * The banner used to read `reasonKey` and `blockedBy` — fields the contract has never declared and no provider
 * has ever sent — so a correctly-shaped blockedState produced a red box with an empty sentence in it.
 */
describe("the blocked banner says what is in the way", () => {
  const blockedItem = (overrides) => projectionItem(Object.assign({
    normalizedStatus: "Pending",
    taskLifecycle: "Open",
    executionState: "notStarted",
    workItemCapabilities: ["planning", "execution", "subtasks", "dependencies"],
    dependencies: [{
      id: "DEP-1",
      title: { kind: "display", text: "Sözleşme imzası", locale: "und" },
      type: "FinishToStart",
      state: "in-progress",
      direction: "pred"
    }],
    actions: [{
      code: "start",
      label: { kind: "resource", key: "WorkAggregation_Action_Start" },
      semanticType: "start",
      enabled: false,
      source: "provider",
      disabledReasonCode: "DEPENDENCY_BLOCKED",
      disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_DependencyBlocked" },
      requiresConfirmation: false,
      requiresReason: false,
      requiresEvidence: false,
      supportsBulk: false,
      riskLevel: "normal"
    }],
    blockedState: {
      blocked: true,
      affectedActionCodes: ["start"],
      blockers: [{
        code: "DEPENDENCY_BLOCKED",
        label: { kind: "display", text: "Sözleşme imzası", locale: "und" },
        taskItemId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        dependencyType: "FinishToStart",
        affectedActionCode: "start"
      }]
    }
  }, overrides));

  it("counts the blockers and names each one", async () => {
    await bootDetailPage(blockedItem());

    const banner = app().querySelector(".wcn-blocked");
    expect(banner).not.toBeNull();
    expect(banner.querySelector(".wcn-blocked-title").textContent).toContain("BlockedBannerCount");

    const row = banner.querySelector(".wcn-blocked-item");
    expect(row).not.toBeNull();
    // The typed sentence, keyed by edge type — not a generic "blocked" line.
    expect(row.querySelector(".wcn-blocked-why").textContent).toContain("BlockerFinishToStart");
    // ...and WHICH act it stops.
    expect(row.querySelector(".wcn-blocked-affects").textContent).toBe("BlockedAffectsStart");
    // The edge type is shown as its display abbreviation, never the wire spelling.
    expect(row.querySelector(".wcn-dep-type").textContent).toBe("FS");
  });

  it("keeps the blocked action visible and disabled", async () => {
    await bootDetailPage(blockedItem());

    const button = app().querySelector('.wcn-actrail [data-wcn-action="start"]');
    expect(button).not.toBeNull();
    expect(button.disabled).toBe(true);
  });

  it("prints no banner when nothing is blocking", async () => {
    await bootDetailPage(projectionItem());

    expect(app().querySelector(".wcn-blocked")).toBeNull();
  });

  it("lists the dependency with its type and state", async () => {
    await bootDetailPage(blockedItem());

    const dep = app().querySelector(".wcn-dep");
    expect(dep).not.toBeNull();
    expect(dep.querySelector(".wcn-dep-title").textContent).toBe("Sözleşme imzası");
    expect(dep.querySelector(".wcn-dep-type").textContent).toBe("FS");
    expect(dep.querySelector(".wcn-badge").textContent).toBe("DepInProgress");
  });

  it("de-emphasises a cancelled predecessor instead of dropping it", async () => {
    // Called-off work blocks nothing, but it is still part of the record — hiding it would erase the history.
    await bootDetailPage(blockedItem({
      dependencies: [{
        id: "DEP-1",
        title: { kind: "display", text: "Sözleşme imzası", locale: "und" },
        type: "FinishToStart",
        state: "cancelled",
        direction: "pred"
      }],
      blockedState: null,
      actions: []
    }));

    const dep = app().querySelector(".wcn-dep");
    expect(dep.className).toContain("is-cancelled");
    expect(dep.querySelector(".wcn-badge").textContent).toBe("DepCancelled");
    expect(app().querySelector(".wcn-blocked")).toBeNull();
  });
});

describe("priority is shown where it exists and nowhere else (BL-032)", () => {
  it("renders the chip for a ranked item", async () => {
    await bootDetailPage(projectionItem({ priority: "High" }));

    const chip = Array.from(app().querySelectorAll(".wcn-chip"))
      .find((el) => el.textContent.includes("PriorityHigh"));
    expect(chip).not.toBeUndefined();
    // Colour carries the rank, and it has to come from the map the contract's spelling keys.
    expect(chip.className).toContain("wcn-chip-danger");
  });

  it("shows nothing at all when the provider did not rank the work", async () => {
    await bootDetailPage(projectionItem());

    // The FLAG CHIP itself must be absent, not merely empty of text: an unranked item used to render the chip
    // with no label and no colour, which is what got the whole column hidden.
    const flagChips = Array.from(app().querySelectorAll(".wcn-chip"))
      .filter((el) => el.querySelector("i.bx-flag"));
    expect(flagChips).toHaveLength(0);
  });
});

/*
 * BL-035 on screen. The banner mechanism was already there for dependencies; a subtask blocker has to reach it
 * with a sentence of its own rather than falling through to a bare label.
 */
describe("an open subtask is named in the blocked banner", () => {
  const withOpenSubtask = () => projectionItem({
    workItemCapabilities: ["planning", "execution", "subtasks"],
    subtasks: {
      mode: "full",
      items: [
        { id: "11111111-1111-1111-1111-111111111111", title: "Bütçe kalemini doğrula", status: "in-progress", assignee: null, dueAt: null, canCancel: true },
        { id: "33333333-3333-3333-3333-333333333333", title: "Kapanış", status: "cancelled", assignee: null, dueAt: null, canCancel: false }
      ]
    },
    actions: [{
      code: "complete",
      label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
      semanticType: "complete",
      enabled: false,
      source: "provider",
      disabledReasonCode: "SUBTASK_BLOCKED",
      disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_SubtaskBlocked" },
      requiresConfirmation: false,
      requiresReason: false,
      requiresEvidence: false,
      supportsBulk: false,
      riskLevel: "normal"
    }],
    blockedState: {
      blocked: true,
      affectedActionCodes: ["complete"],
      blockers: [{
        code: "SUBTASK_BLOCKED",
        label: { kind: "display", text: "Bütçe kalemini doğrula", locale: "und" },
        taskItemId: "11111111-1111-1111-1111-111111111111",
        affectedActionCode: "complete"
      }]
    }
  });

  it("collapses a subtask blocker to one sentence and points at the list that names it", async () => {
    /*
     * ⚠ THIS ASSERTION CHANGED, and the reason is worth keeping.
     *
     * It used to require a per-blocker ROW: the subtask's own sentence plus which act it stops. That was right
     * while the banner was the only place a blocker was named. It is not any more — measured on a live blocked
     * task, the banner printed a title and three rows each ending in the same words, naming three subtasks that
     * the Subtasks card listed directly below with their own controls. Four sentences for one fact.
     *
     * So the banner now states the fact once and LINKS to the list. The per-blocker sentence is not lost, it
     * moved to where the subtask already lived.
     *
     * ⚠ WHAT THIS COSTS, said plainly: with a single blocker the old banner NAMED it ("Bütçe kalemini doğrula")
     * and this one does not — it says "1 subtask" and offers the link. Recorded as BL-104; if the owner wants
     * the name back for n === 1, that is a branch here and not a redesign.
     *
     * The dependency-typed path is untouched and still renders full rows — see the companion assertion in
     * wcn-detail-three-regions: those blockers appear nowhere else, so collapsing them would delete rather than
     * de-duplicate.
     */
    await bootDetailPage(withOpenSubtask());

    const banner = app().querySelector(".wcn-blocked");
    expect(banner.classList.contains("wcn-blocked-oneline")).toBe(true);
    expect(banner.textContent).toContain("BlockedSubtaskOneLine");
    expect(app().querySelector(".wcn-blocked-item"), "the repeated per-blocker row survived").toBeNull();
    expect(app().querySelector("[data-wcn-goto-subtasks]"), "no route to the list that names it").not.toBeNull();
  });

  it("keeps completion visible and disabled", async () => {
    await bootDetailPage(withOpenSubtask());

    const button = app().querySelector('.wcn-actrail [data-wcn-action="complete"]');
    expect(button).not.toBeNull();
    expect(button.disabled).toBe(true);
  });

  it("counts only the open subtasks in the card's notice", async () => {
    await bootDetailPage(withOpenSubtask());

    /*
     * One in-progress, one cancelled: the cancelled one is not open and must not be counted. The notice is an
     * ALERT now rather than a grey hint — it is the sentence that says why "Complete" will refuse, and it read
     * as ordinary body text. Only its appearance changed; the wording and the condition did not.
     */
    const notice = Array.from(app().querySelectorAll(".wcn-subtask-gate"))
      .find((el) => el.textContent.includes("SubtasksBlockingNotice"));
    expect(notice).not.toBeUndefined();
    expect(notice.className).toContain("alert");
  });

  it("says nothing about blocking when every subtask is closed", async () => {
    await bootDetailPage(projectionItem({
      workItemCapabilities: ["planning", "execution", "subtasks"],
      subtasks: {
        mode: "full",
        items: [
          { id: "11111111-1111-1111-1111-111111111111", title: "Bütçe kalemini doğrula", status: "done", assignee: null, dueAt: null, canCancel: false },
          { id: "33333333-3333-3333-3333-333333333333", title: "Kapanış", status: "cancelled", assignee: null, dueAt: null, canCancel: false }
        ]
      }
    }));

    const notice = Array.from(app().querySelectorAll(".wcn-block-hint"))
      .find((el) => el.textContent.includes("SubtasksBlockingNotice"));
    expect(notice).toBeUndefined();
    expect(app().querySelector(".wcn-blocked")).toBeNull();
  });
});

/*
 * BL-034 item 7 — the composer, wired.
 *
 * Both halves of this feed existed behind the `activity` capability that no provider declared, so neither had
 * ever rendered. The write half is the one that matters here: a post that is not awaited swallows its own
 * rejection and looks exactly like a button nobody wired, which is how the subtask writer shipped broken.
 */
describe("the comment composer writes to the engine", () => {
  const withActivity = (overrides) => projectionItem(Object.assign({
    workItemCapabilities: ["planning", "execution", "subtasks", "activity"],
    activity: []
  }, overrides));

  it("offers the composer on an open task", async () => {
    await bootDetailPage(withActivity());

    expect(app().querySelector("[data-wcn-comment-input]")).not.toBeNull();
    expect(app().querySelector("[data-wcn-comment-post]")).not.toBeNull();
  });

  it("hides the composer on a closed task but still shows what was said", async () => {
    await bootDetailPage(withActivity({
      normalizedStatus: "Done",
      taskLifecycle: "Done",
      executionState: "notApplicable",
      actions: [],
      activity: [{ id: "C1", kind: "comment", text: "Kapanmadan önce söylenmiş", actor: "Deniz Koç", at: "2026-07-24T09:10:00+00:00" }]
    }));

    expect(app().querySelector("[data-wcn-comment-post]")).toBeNull();
    // History is finished, not sealed.
    expect(app().textContent).toContain("Kapanmadan önce söylenmiş");
  });

  it("posts the typed text to the engine and re-reads the projection", async () => {
    const { posted } = await bootDetailPage(withActivity());

    app().querySelector("[data-wcn-comment-input]").value = "Bütçe onayını bekliyoruz.";
    app().querySelector("[data-wcn-comment-post]").click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    /*
     * CONTENT, not count: every bootDetailPage in this file leaves another delegated document listener behind, so
     * one click reaches all of them and each posts through the CURRENT stub. What matters is that the click
     * produced a post at all and that it carried the typed text — the "refuses an empty comment" case below is
     * what proves a post is not unconditional.
     */
    expect(posted.length).toBeGreaterThan(0);
    posted.forEach((entry) => {
      expect(entry).toEqual({ taskId: TASK_ID, payload: { text: "Bütçe onayını bekliyoruz." } });
    });
  });

  it("does not tell the user a real write was a mock", async () => {
    // The real path used to share 'ToastCommentPosted' with the fixture path, which says "(mock)" in all seven
    // languages — a lie for a comment that really reached the engine. t()/tf() echo the key verbatim in this
    // harness, so the toast message IS the key.
    const toasts = [];
    global.showToast = (message) => { toasts.push(message); };
    await bootDetailPage(withActivity());

    app().querySelector("[data-wcn-comment-input]").value = "Bütçe onayını bekliyoruz.";
    app().querySelector("[data-wcn-comment-post]").click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(toasts).toContain("ToastCommentPostedReal");
    expect(toasts).not.toContain("ToastCommentPosted");
    delete global.showToast;
  });

  it("refuses an empty comment without troubling the engine", async () => {
    const { posted } = await bootDetailPage(withActivity());

    app().querySelector("[data-wcn-comment-input]").value = "   ";
    app().querySelector("[data-wcn-comment-post]").click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(posted).toHaveLength(0);
  });

  it("refuses a comment past the length limit locally, and the server refuses it too", async () => {
    // The client check is a courtesy that saves a round trip; the rule lives on the server (TaskCommentLimits).
    const { posted } = await bootDetailPage(withActivity());

    app().querySelector("[data-wcn-comment-input]").value = "x".repeat(2001);
    app().querySelector("[data-wcn-comment-post]").click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(posted).toHaveLength(0);
  });

  it("renders an entry's time from its absolute instant, not from a server-sent day count", async () => {
    // A "3 days ago" computed anywhere but here goes stale while the tab stays open.
    await bootDetailPage(withActivity({
      activity: [{ id: "C1", kind: "comment", text: "dün", actor: "Deniz Koç", at: "2026-07-24T09:10:00+00:00" }]
    }));

    // WC-1 split the comment row: the author now has its own line above the message, so the meta line carries
    // the time alone. Both facts are still on screen — this asserts where each one lives now.
    expect(app().querySelector(".wcn-audit-author").textContent).toContain("Deniz Koç");
    const meta = app().querySelector(".wcn-audit-meta");
    expect(meta).not.toBeNull();
    // TimeToday / TimeYesterday / TimeDaysAgo — whichever, it must be DERIVED and not blank.
    expect(meta.textContent).toMatch(/Time(Today|Yesterday|DaysAgo)/);
  });

  it("names an author the server could not resolve, rather than printing nothing", async () => {
    await bootDetailPage(withActivity({
      activity: [{ id: "C1", kind: "comment", text: "anonim", at: "2026-07-24T09:10:00+00:00" }]
    }));

    expect(app().querySelector(".wcn-audit-author").textContent).toContain("CommentAuthorUnknown");
  });
});

/*
 * BL-034-adjacent: the personal plan date now actually reaches the engine.
 *
 * Before this, the picker was never shown to a real user at all — `!isRealTaskItem(item)` kept it fixture-only,
 * because `/plan` accepted no date and asking for one we then discarded would have been a new lie. Now the
 * engine stores it, so the same picker opens for real work and writes through TasksApi rather than a local push.
 */
describe("the plan date picker writes to the engine for a real task", () => {
  // A minimal but faithful SweetAlert stand-in: it runs `didOpen` SYNCHRONOUSLY (so the seeded input can be
  // inspected the instant the click returns, exactly as real SweetAlert would have it in the DOM by then) and
  // resolves with a fixed confirm value rather than re-implementing SweetAlert's own confirm/cancel machinery.
  const stubSwal = (confirmed) => {
    global.Swal = {
      fire: (options) => {
        const container = document.createElement("div");
        container.innerHTML = options.html || "";
        document.body.appendChild(container);
        if (options.didOpen) { options.didOpen(); }
        return Promise.resolve(confirmed);
      }
    };
  };

  afterEach(() => {
    // Several OTHER action flows in this module (reject/return/inquire) also branch on `global.Swal` presence;
    // leaving a stub behind would silently change their behaviour in a later, unrelated test.
    delete global.Swal;
  });

  const planAction = () => ({
    code: "plan",
    label: { kind: "resource", key: "WorkAggregation_Action_Plan" },
    semanticType: "plan",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  });

  const clickPlan = async () => {
    app().querySelector('[data-wcn-action="plan"]').click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });
  };

  it("opens the picker for a real task and seeds it with the existing planned date", async () => {
    stubSwal({ isConfirmed: false });
    await bootDetailPage(projectionItem({
      actions: [planAction()],
      plannedDate: "2026-08-01",
      dueAt: "2026-08-10T00:00:00+00:00"
    }));

    app().querySelector('[data-wcn-action="plan"]').click();
    // onClick runs via a queued microtask (Promise.resolve().then(...)), not synchronously inside click() — the
    // input does not exist yet the instant click() returns.
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    // The existing plan wins over the source due date, matching openDatePicker's own fallback order.
    expect(document.getElementById("wcnPlanDate").value).toBe("2026-08-01");
  });

  it("seeds correctly even when the wire sends a full instant, not a bare date", async () => {
    // The engine's PlannedDate is a DateTimeOffset and serializes with a time and an offset. adaptProjection has
    // to normalize it the same way it already normalizes dueAt, or a type="date" input rejects the value outright
    // (an invalid value for that input type sets .value to "", which a same-day midnight fixture would not catch).
    stubSwal({ isConfirmed: false });
    await bootDetailPage(projectionItem({
      actions: [planAction()],
      plannedDate: "2026-08-01T14:30:00+03:00",
      dueAt: null
    }));

    app().querySelector('[data-wcn-action="plan"]').click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    expect(document.getElementById("wcnPlanDate").value).toBe("2026-08-01");
  });

  it("falls back to the source due date when there is no plan yet", async () => {
    stubSwal({ isConfirmed: false });
    await bootDetailPage(projectionItem({
      actions: [planAction()],
      plannedDate: null,
      dueAt: "2026-08-10T00:00:00+00:00"
    }));

    app().querySelector('[data-wcn-action="plan"]').click();
    await new Promise((resolve) => { setTimeout(resolve, 0); });

    // dueAt is normalized to date-only by adaptProjection before openDatePicker ever sees it (the SLA math and
    // the date input both expect a date, not a full instant) — so the seed is already just the date.
    expect(document.getElementById("wcnPlanDate").value).toBe("2026-08-10");
  });

  it("posts the chosen date to the engine, not a local mutation", async () => {
    const planCalls = [];
    stubSwal({ isConfirmed: true, value: "2026-08-20" });
    // A token no other test in this file uses, so a call carrying it can only have come from THIS item —
    // decisive even though the same click also reaches every other "plan"-capable instance booted so far (see
    // the "content, not count" note used throughout this file for the same accumulated-listener reason).
    await bootDetailPage(projectionItem({ actions: [planAction()], concurrency: { kind: "version", token: "424242" } }));
    global.TasksApi.plan = (taskId, payload) => {
      planCalls.push({ taskId, payload });
      return Promise.resolve({ ok: true, status: 204 });
    };

    await clickPlan();

    const mine = planCalls.filter((call) => call.payload.expectedVersion === 424242);
    expect(mine).toHaveLength(1);
    expect(mine[0]).toEqual({ taskId: TASK_ID, payload: { expectedVersion: 424242, plannedDate: "2026-08-20" } });
  });

  it("shows no plan date at all when the write is refused", async () => {
    // No optimistic apply: a refused write must leave the screen exactly as it was, or a rejected write would
    // look identical to an accepted one.
    stubSwal({ isConfirmed: true, value: "2026-08-20" });
    await bootDetailPage(projectionItem({
      actions: [planAction()],
      plannedDate: null,
      dueAt: null
    }));
    global.TasksApi.plan = () => Promise.resolve({
      ok: false, status: 400, reasonCode: "TASK_PLAN_DATE_REQUIRED"
    });

    await clickPlan();

    const cell = Array.from(app().querySelectorAll(".wcn-date-cell"))
      .find((el) => el.textContent.includes("PlannedDateLabel"));
    // No dueAt and no plannedDate means renderPlanDates prints nothing at all for this item — the empty state,
    // not a value that was never actually stored.
    expect(cell).toBeUndefined();
  });
});

/*
 * The showcase/fixture side of `plan` cannot be exercised through bootDetailPage: that harness always projects
 * through the real API path (mapPayload → provenance "api"), and forcing a raw `provenance: "fixture"` field
 * onto its item is exactly the mistake toPresentation's own guard warns about ("an item that changes origin also
 * changes which guards apply to it"). These two facts are pinned at the SOURCE level instead — the same
 * technique this file already uses for other code-shape guarantees (grep the "carries its wording" tests above).
 */
describe("the fixture-only plan path (source-level, no engine call)", () => {
  const app = fs.readFileSync(
    path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
  const applyPlan = app.slice(app.indexOf("const applyPlan"), app.indexOf("const submitPlan"));

  it("never calls the engine — it is a local mutation only", () => {
    expect(applyPlan).not.toContain("TasksApi");
  });

  it("carries no pre-computed relative time on the event it writes", () => {
    // ACTIVITY_RELATIVE_TIME_FORBIDDEN: an `ago` written at plan-time freezes and goes stale the moment the tab
    // stays open. Same shape the real (engine) comment path already had to fix.
    expect(applyPlan).not.toMatch(/ago:\s*0/);
    expect(applyPlan).toContain("atMs");
  });
});

/*
 * The runtime half of ACTIVITY_RELATIVE_TIME_FORBIDDEN.
 *
 * fixture-contract.js already forbids a pre-computed `ago` on an activity entry — but validateWorkItem runs
 * exactly twice, both at INGESTION: work-items-api's mapPayload when a real projection arrives, and
 * task-detail-resolver when a fixture is resolved. Every writer in app.js mutates an item that was already
 * validated (`item.activity.push(...)`), so the contract never sees what they wrote and cannot reject it.
 *
 * Six such writers existed and were converted to `atMs`. This is a SOURCE SCAN rather than a behavioural test
 * because there is no seam to observe: the offending value would simply render as a frozen "today" forever, on a
 * surface no assertion currently watches. Guarding the source is what keeps the seventh from being written.
 */
describe("no activity writer freezes its own timestamp", () => {
  const source = fs.readFileSync(
    path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

  it("writes no `ago` field anywhere in app.js", () => {
    // Matches `ago:` as an object KEY — `ago: 0`, `ago : 3`, `ago:someVar`. Deliberately not anchored to 0: any
    // pre-computed relative count is the same defect, whatever its value.
    const offenders = source
      .split("\n")
      .map((line, index) => ({ line: line.trim(), number: index + 1 }))
      .filter((entry) => /(^|[{,\s])ago\s*:/.test(entry.line));

    expect(offenders).toEqual([]);
  });

  it("still writes an absolute instant, so the guard above is not passing by writing nothing", () => {
    // Non-vacuity: "no ago" would also be satisfied by an app.js that recorded no timestamps at all.
    expect(source).toContain("atMs: data.referenceDate(");
  });
});

/*
 * BL-038 on the DETAIL surface.
 *
 * The rule lives in one place — mock-data's getActions, which app.js's `itemActions` wraps — so the list rows
 * and this page's action rail read the same filtered set. These two cases are the list's cases repeated here:
 * that is the whole point of the ticket, and a rule written on one surface only is how the two drift.
 */
describe("a closed task's actions on the detail page (BL-038)", () => {
  const closedTask = (actions) => projectionItem({
    normalizedStatus: "Done",
    taskLifecycle: "Done",
    executionState: "notApplicable",
    actions
  });

  const disabledInline = {
    code: "complete",
    label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete",
    enabled: false,
    source: "provider",
    disabledReasonCode: "TERMINAL",
    disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_PermissionDenied" },
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  };

  it("offers no inline action, even when the provider sends a disabled one", async () => {
    await bootDetailPage(closedTask([disabledInline]));

    expect(app().querySelectorAll("[data-wcn-action]")).toHaveLength(0);
  });

  it("still offers a deeplink action — the source record stays reachable", async () => {
    await bootDetailPage(closedTask([Object.assign({}, disabledInline, {
      code: "openSource",
      semanticType: "openSource",
      enabled: true,
      disabledReasonCode: null,
      disabledReason: null,
      depth: "deeplink"
    })]));

    expect(app().querySelectorAll("[data-wcn-action]").length).toBeGreaterThan(0);
  });

  it("still renders the task itself — the rule removes buttons, not the page", async () => {
    // Same reasoning as the list: the filter must never cost the reader the record.
    await bootDetailPage(closedTask([disabledInline]));

    expect(app().textContent).toContain("Yeni maliyet merkezi");
  });

  it("leaves an OPEN task's inline actions alone", async () => {
    // Non-vacuity for all three above: the filter must be about being closed, not about this selector never
    // matching. projectionItem() is InProgress and carries one enabled inline action.
    await bootDetailPage(projectionItem());

    expect(app().querySelectorAll("[data-wcn-action]").length).toBeGreaterThan(0);
  });
});
