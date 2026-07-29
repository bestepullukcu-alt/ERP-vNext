const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 Phase 2 in the browser — checklist and subtasks against the REAL projection shape.
 *
 * The payload below mirrors what TaskWorkItemProvider serializes (capability + container together, checklist run
 * version on the container, subtask items in the contract's own vocabulary), so a drift on either side fails here
 * rather than rendering a broken block.
 */
const PARENT_ID = "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0";
const CHILD_ID = "bd76df2e-8e4b-57d4-b4fa-0e9d65f7c3c1";

const baseItem = (overrides = {}) => ({
  fixtureKind: "workItem",
  id: PARENT_ID,
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
  title: { kind: "display", text: "Month-end close", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: PARENT_ID, deepLink: `/Tasks/${PARENT_ID}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "checklist", "subtasks"],
  actions: [{
    code: "complete", label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete", enabled: true, source: "provider",
    disabledReasonCode: null, disabledReason: null,
    requiresConfirmation: true, requiresReason: false, requiresEvidence: false,
    supportsBulk: false, riskLevel: "normal"
  }],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-08-01T00:00:00+00:00",
  checklist: {
    version: 7,
    items: [
      {
        id: "reconcile",
        label: { kind: "resource", key: "WorkAggregation_Check_Reconcile" },
        completed: false, required: true, blocking: true, evidenceRequired: false
      },
      {
        id: "adhoc-1",
        label: { kind: "display", text: "Call the supplier back", locale: "und" },
        completed: true, required: false, blocking: false, evidenceRequired: false
      }
    ]
  },
  subtasks: { mode: "full", items: [{ id: CHILD_ID, title: "Import balances", status: "not-started" }] },
  ...overrides
});

describe("MOD-0024 Phase 2 — checklist and subtasks", () => {
  let warnings;
  let originalWarn;

  beforeEach(() => {
    warnings = [];
    originalWarn = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));

    delete global.WorkCenterNextData;
    delete global.WorkCenterNextApi;
    global.WCN = { t: (key) => key, tn: (key) => key };
    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';

    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/mock-data.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  });

  afterEach(() => { console.warn = originalWarn; });

  describe("the real projection shape survives the client chain", () => {
    it("passes the executable contract with both containers present", () => {
      const result = global.WorkCenterNextContract.validateWorkItem(baseItem());
      expect(result.errors).toEqual([]);
      expect(result.valid).toBe(true);
    });

    it("is rejected when a container arrives WITHOUT its capability", () => {
      // CAPABILITY_REQUIRED_FOR_DATA — the item would be dropped rather than rendered half-built.
      const bad = baseItem({ workItemCapabilities: ["planning", "execution"] });
      const result = global.WorkCenterNextContract.validateWorkItem(bad);

      expect(result.valid).toBe(false);
      expect(result.errors.map((e) => e.code)).toContain("CAPABILITY_REQUIRED_FOR_DATA");
    });

    it("is rejected when a capability is declared WITHOUT its container", () => {
      const bad = baseItem();
      delete bad.checklist;
      const result = global.WorkCenterNextContract.validateWorkItem(bad);

      expect(result.valid).toBe(false);
      expect(result.errors.map((e) => e.code)).toContain("CAPABILITY_CONTAINER_REQUIRED");
    });

    it("accepts a declared capability with an EMPTY container", () => {
      const empty = baseItem({ checklist: { version: 1, items: [] }, subtasks: { mode: "full", items: [] } });
      expect(global.WorkCenterNextContract.validateWorkItem(empty).valid).toBe(true);
    });
  });

  describe("mapping to presentation", () => {
    const mapped = (overrides) => global.WorkCenterNextApi.mapPayload([baseItem(overrides)]).items[0];

    it("resolves each item's label in its own form, never a raw key", () => {
      const item = mapped();

      // Template item → resource key resolved through the l10n bridge…
      expect(item.checklist.items[0].text).toBe("WorkAggregation_Check_Reconcile");
      // …ad-hoc item → the user's own words, verbatim.
      expect(item.checklist.items[1].text).toBe("Call the supplier back");
    });

    it("renders no raw resource key for user-typed text", () => {
      const item = mapped();
      const adhoc = item.checklist.items[1];
      expect(adhoc.text).not.toMatch(/^WorkAggregation_/);
    });

    it("carries the checklist RUN version, which the tick needs as its concurrency token", () => {
      // The run is a separate document from the task, so it has its own version.
      expect(mapped().checklist.version).toBe(7);
      expect(mapped().concurrency.token).toBe("3");
    });

    it("keeps the contract's completion flag and the blocking flag distinct", () => {
      const item = mapped();
      expect(item.checklist.items[0].done).toBe(false);
      expect(item.checklist.items[0].blocking).toBe(true);
      // `required` alone is an expectation, not a barrier.
      expect(item.checklist.items[1].blocking).toBe(false);
    });

    it("passes subtasks through in the contract's vocabulary", () => {
      const item = mapped();
      expect(item.subtasks.mode).toBe("full");
      expect(item.subtasks.items[0].title).toBe("Import balances");
      expect(item.subtasks.items[0].status).toBe("not-started");
    });

    it("carries the parent link on a subtask's own row", () => {
      const child = global.WorkCenterNextApi.mapPayload([baseItem({
        id: CHILD_ID,
        parentTaskItemId: PARENT_ID,
        // A subtask has no subtasks of its own, so neither capability nor container.
        workItemCapabilities: ["planning", "execution"],
        checklist: undefined,
        subtasks: undefined
      })]).items[0];

      expect(child.parentTaskItemId).toBe(PARENT_ID);
      expect(child.subtasks == null).toBe(true);
    });
  });

  describe("the app wires the real endpoints (source contract)", () => {
    const app = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

    it("ticking an item calls the engine and re-reads the projection", () => {
      const fn = app.slice(app.indexOf("const toggleChecklistItem"), app.indexOf("const completeSubtask"));
      expect(fn).toContain("global.TasksApi.setChecklistItemState");
      // Concurrency token comes from the RUN, not the task.
      expect(fn).toContain("item.checklist?.version");
      expect(app).toContain("await loadWorkItems()");
    });

    it("no interaction is left as a mock toast", () => {
      // The marker also appears in the RENDER code earlier in the file, so bound the slice from the handler
      // section forward rather than to the first occurrence.
      const start = app.indexOf("Depth-block interactions");
      const handlers = app.slice(start, app.indexOf("data-wcn-note-save", start));
      expect(handlers).not.toContain("ProviderCommandRequired");
      expect(handlers).toContain("toggleChecklistItem");
      expect(handlers).toContain("completeSubtask");
      expect(handlers).toContain("addSubtask");
    });

    it("completes a subtask through the ordinary task endpoint, not a child-only path", () => {
      const fn = app.slice(app.indexOf("const completeSubtask"), app.indexOf("const addSubtask"));
      expect(fn).toContain("global.TasksApi.transition(subtaskId, 'complete'");
    });

    it("creates a subtask through the ordinary create endpoint with a parent link", () => {
      const fn = app.slice(app.indexOf("const addSubtask"), app.indexOf("const applyAction"));
      expect(fn).toContain("payload.parentTaskItemId = parentId");
      expect(fn).toContain("global.TasksApi.create(payload)");
    });

    it("applies nothing optimistically and refreshes on a conflict", () => {
      const fn = app.slice(app.indexOf("const afterPhase2Write"), app.indexOf("const toggleChecklistItem"));
      expect(fn).toContain("await loadWorkItems()");
      expect(fn).toContain("ErrorConcurrencyRefreshed");
      expect(fn).not.toContain("item.checklist.items[");
    });

    it("warns instead of silently ignoring a non-engine item", () => {
      expect(app).toContain("Checklist toggle ignored for non-engine item");
    });

    it("states the blocked reason in the page, not only in a tooltip", () => {
      const fn = app.slice(app.indexOf("const renderChecklist"), app.indexOf("const SUBTASK_ICON"));
      expect(fn).toContain("WorkAggregation_ActionDisabled_ChecklistIncomplete");
      expect(fn).toContain('role="note"');
    });

    it("explains an empty checklist and an empty subtask list", () => {
      expect(app).toContain("ChecklistEmpty");
      expect(app).toContain("SubtasksEmpty");
    });

    it("says open subtasks BLOCK completion, and says how many", () => {
      /*
       * This assertion was inverted with BL-035 (owner decision, 2026-07-29). It used to require the notice to
       * report without blocking; the rule is now enforced server-side, and a screen that still called it a mere
       * notice would be lying about what pressing the button does.
       */
      const fn = app.slice(app.indexOf("const renderSubtasks"), app.indexOf("const DEP_TYPE_KEY"));
      expect(fn).toContain("SubtasksBlockingNotice");
      expect(fn).not.toContain("SubtasksOpenNotice");
      // A cancelled subtask is not open, so it must not be counted among the blockers.
      expect(fn).toContain("'cancelled'");
    });
  });

  describe("seven-language parity for the new keys", () => {
    const dir = path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext");
    const locales = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    const keysOf = (locale) => new Set(
      [...fs.readFileSync(path.join(dir, `WorkCenterNextIndex.${locale}.resx`), "utf8")
        .matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]));

    it("ships every Phase 2 key in all seven languages", () => {
      const required = [
        "ChecklistEmpty", "SubtasksEmpty", "SubtasksBlockingNotice",
        "WorkAggregation_ActionDisabled_ChecklistIncomplete",
        "ToastChecklistUpdated", "ToastSubtaskAdded", "SubtaskTitleRequired",
        "SubtaskOfNamed", "SubtaskOfUnnamed"
      ];
      const missing = [];
      locales.forEach((locale) => {
        const keys = keysOf(locale);
        required.forEach((key) => { if (!keys.has(key)) { missing.push(`${locale}/${key}`); } });
      });
      expect(missing).toEqual([]);
    });

    it("translates them rather than leaving English in place", () => {
      const valueOf = (locale, key) => {
        const xml = fs.readFileSync(path.join(dir, `WorkCenterNextIndex.${locale}.resx`), "utf8");
        const m = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
        return m ? m[1].trim() : null;
      };

      ["ChecklistEmpty", "SubtasksBlockingNotice", "SubtaskTitleRequired"].forEach((key) => {
        expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
      });
    });
  });
  describe("a subtask is a full citizen, not a checklist line", () => {
    const app = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
    const renderer = app.slice(app.indexOf("const renderSubtasks"), app.indexOf("const DEP_TYPE_KEY"));

    it("its title is a real control that opens its own detail", () => {
      // A subtask has its own lifecycle and detail page; a plain <span> made it unreachable.
      expect(renderer).toContain("data-wcn-open-task=");
      expect(renderer).not.toMatch(/<span class="wcn-subtask-title">/);
      expect(renderer).toContain('<button type="button" class="wcn-subtask-title');
    });

    it("is reachable and labelled for a keyboard or screen reader", () => {
      expect(renderer).toContain("SubtaskOpenAria");
      expect(renderer).toContain("SubtaskToggleAria");
    });

    it("opening is a DIFFERENT control from completing, so a click cannot do both", () => {
      const handlers = app.slice(app.indexOf("Depth-block interactions"),
        app.indexOf("data-wcn-note-save", app.indexOf("Depth-block interactions")));
      // Open is matched first and returns; the toggle keeps its own attribute.
      expect(handlers.indexOf("data-wcn-open-task")).toBeLessThan(handlers.indexOf("data-wcn-subtask]"));
      // The destination changed by design — a row now opens the quick-edit panel, and the panel carries the
      // way out to the full page. What this test guards is unchanged: opening and completing are separate
      // controls, so one click can never do both.
      expect(handlers).toContain("openSubtaskPanel(itemById(state.selectedId)");
      expect(renderer).toContain("data-wcn-subtask=");
    });

    it("navigates to the Task Center detail, not the source deep link", () => {
      // data-wcn-open opens the SOURCE system; a subtask lives here.
      expect(app).toContain("/WorkCenterNext/Details/");
      expect(renderer).not.toContain("data-wcn-open=");
    });

    it("inherits the parent's priority and assignee instead of guessing", () => {
      const fn = app.slice(app.indexOf("const addSubtask"), app.indexOf("const applyAction"));
      expect(fn).toContain("global.TasksApi.get(parentId)");
      expect(fn).toContain("parentTask.data.priority");
      expect(fn).toContain("parentTask.data.assigneeUserId");
      // A pooled parent has no holder to inherit from.
      expect(fn).toContain("assigneeUserId ? 'Person' : 'SelfAssigned'");
      // Due date inheritance was already correct.
      expect(fn).toContain("parent.dueAt");
    });

    it("still creates the subtask, with stated fallbacks, when the parent cannot be read", () => {
      const fn = app.slice(app.indexOf("const addSubtask"), app.indexOf("const applyAction"));
      expect(fn).toContain("console.warn");
      expect(fn).toContain("falling back to priority=Medium");
    });

    it("styles the title button through a class, never inline (FG-003)", () => {
      expect(renderer).not.toContain("style=");
      const css = fs.readFileSync(
        path.resolve(__dirname, "..", "wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
      expect(css).toContain(".wcn-linklike");
    });
  });
});
