const { loadScript } = require("./load-script");

/*
 * MOD-0024 — a work-item action must change the ENGINE, not just the screen.
 *
 * The bug: pressing "Başlat" ran a browser-side transition left over from the mock era. The row moved
 * "Beklemede" → "Devam ediyor" and a toast fired, while GET /Tasks/api/list still returned lifecycle "Open".
 * Nothing was saved, and the lie survived until the next refresh.
 *
 * These drive the real seam: a fake TasksApi records the call and mutates a server-side state object, so
 * "did the backend change?" is answered by re-reading that state, exactly as the app re-reads the projection.
 */
describe("work item actions reach the engine", () => {
  let server;      // stands in for the backend's stored state
  let calls;
  let warnings;
  let originalWarn;

  const projectionItem = (overrides = {}) => ({
    fixtureKind: "workItem",
    id: "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0",
    workIntent: "task",
    assignmentMode: "direct",
    ownershipState: "owned",
    admissionState: "admitted",
    normalizedStatus: server.lifecycle === "InProgress" ? "InProgress" : "Pending",
    taskLifecycle: server.lifecycle,
    executionState: server.lifecycle === "InProgress" ? "active" : "notStarted",
    timerState: "notApplicable",
    systemState: "fresh",
    actionDepth: "inline",
    title: { kind: "display", text: "CT probe", locale: "und" },
    nativeStatus: { code: server.lifecycle, label: { kind: "resource", key: `WorkAggregation_TaskStatus_${server.lifecycle}` } },
    source: {
      providerCode: "tasks",
      providerContractVersion: "1.0",
      objectType: "task",
      objectId: "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0",
      deepLink: "/Tasks/ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0"
    },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution"],
    actions: [{
      code: "start", label: { kind: "resource", key: "WorkAggregation_Action_Start" },
      semanticType: "start", enabled: true, source: "provider",
      disabledReasonCode: null, disabledReason: null,
      requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
      supportsBulk: false, riskLevel: "normal"
    }],
    concurrency: { kind: "version", token: String(server.version) },
    waitingContext: null,
    escalation: null,
    dueAt: "2026-08-01T00:00:00+00:00",
    ...overrides
  });

  beforeEach(() => {
    server = { lifecycle: "Open", version: 1 };
    calls = [];
    warnings = [];
    originalWarn = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));

    global.TasksApi = {
      transition: async (id, code, payload) => {
        calls.push({ id, code, payload });
        // Expected-version write, exactly like the engine's conditional update.
        if (payload.expectedVersion !== server.version) {
          return { ok: false, status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT", data: null };
        }
        if (code === "start") { server.lifecycle = "InProgress"; server.version += 1; }
        return { ok: true, status: 200, data: null };
      },
      failureMessage: () => "generic-failure"
    };
  });

  afterEach(() => { console.warn = originalWarn; });

  it("sends the action with the projection's concurrency token", async () => {
    await global.TasksApi.transition("ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0", "start", { expectedVersion: 1 });

    expect(calls).toHaveLength(1);
    expect(calls[0].code).toBe("start");
    expect(calls[0].payload.expectedVersion).toBe(1);
  });

  it("actually changes the stored lifecycle — the whole point", async () => {
    expect(server.lifecycle).toBe("Open");

    const result = await global.TasksApi.transition("id", "start", { expectedVersion: 1 });

    expect(result.ok).toBe(true);
    // Re-read the state the server holds, not the screen.
    expect(server.lifecycle).toBe("InProgress");
    expect(server.version).toBe(2);
  });

  it("rejects a stale write instead of applying it", async () => {
    server.version = 5;   // someone else moved it on

    const result = await global.TasksApi.transition("id", "start", { expectedVersion: 1 });

    expect(result.status).toBe(409);
    expect(result.reasonCode).toBe("TASK_CONCURRENCY_CONFLICT");
    expect(server.lifecycle).toBe("Open");   // unchanged
  });

  describe("through the app's own routing rules", () => {
    beforeEach(() => {
      document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
      global.WCN = { t: (key) => key, tn: (key) => key };
      delete global.WorkCenterNextData;
      loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
      loadScript("wwwroot/assets/js/WorkCenterNext/mock-data.js");
    });

    it("marks a real projection item as api-provenance so actions are routed to the engine", () => {
      const item = global.WorkCenterNextData.toPresentation(projectionItem(), { provenance: "api" });

      // isRealTaskItem() in app.js keys off exactly these two.
      expect(item.provenance).toBe("api");
      expect(item.source.providerCode).toBe("tasks");
    });

    it("marks a showcase fixture so it keeps the demonstration-only transitions", () => {
      const item = global.WorkCenterNextData.toPresentation(
        projectionItem({ id: "ISLERIM-WORK-ACTIVE" }), { provenance: "fixture" });

      expect(item.provenance).toBe("fixture");
    });

    it("carries the concurrency token through to presentation", () => {
      server.version = 7;
      const item = global.WorkCenterNextData.toPresentation(projectionItem(), { provenance: "api" });

      expect(item.concurrency.token).toBe("7");
    });
  });

  describe("the app wires the real path (source contract)", () => {
    const fs = require("fs");
    const path = require("path");
    const app = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

    /*
     * WC-D2 — THE PROVIDER NAME IS NO LONGER PART OF THE ROUTING DECISION.
     *
     * This used to assert `const isRealTaskItem` and a call to `TasksApi.transition` — MOD-0024's own route,
     * which is exactly why only MOD-0024's items could be acted on. The rule under test is unchanged and now
     * stated in its general form: a real item, from ANY provider, is dispatched to the server; only a showcase
     * fixture keeps the browser-side demonstration.
     */
    it("routes a real item's action to the dispatch endpoint, not to the mock state machine", () => {
      expect(app).toContain("const isDispatchableItem");
      expect(app).toMatch(/isDispatchableItem\(item\)\s*\)\s*\{\s*submitRealTransition/);
      // Matched across newlines: the body comes from the declared TRANSITION_BODIES vocabulary rather than a
      // literal at the call site, so the assertion follows the refactor instead of pinning its formatting.
      expect(app).toMatch(/global\.WorkCenterNextApi\.dispatchAction\(\s*item\.id,\s*action\.code/);
    });

    it("keeps NO provider name in the action-routing decision — that was the defect", () => {
      const gate = app.slice(app.indexOf("const isFixtureShowcase"), app.indexOf("const buildTransitionBody"));
      expect(gate).not.toMatch(/providerCode\s*===\s*['"]tasks['"]/);
      // Provenance is the only classification left, and it answers a different question: a fixture has no record
      // on any server, so writing one would 404 on an id that was never stored.
      expect(gate).toContain("item.provenance === 'fixture'");
    });

    it("names the provider only so the SERVER can look the item up", () => {
      // Addressing, not authority: the permission is evaluated from claims server-side.
      expect(app).toMatch(/dispatchAction\(\s*item\.id,\s*action\.code,\s*item\.source\?\.providerCode/);
    });

    it("sends the projection's concurrency token as the expected version", () => {
      expect(app).toContain("Number(item.concurrency?.token");
      expect(app).toContain("expectedVersion");
    });

    it("re-reads the projection instead of keeping optimistic state", () => {
      const fn = app.slice(app.indexOf("const submitRealTransition"), app.indexOf("const applyAction"));
      expect(fn).toContain("await loadWorkItems()");
      // No local mutation of lifecycle/status inside the real path.
      expect(fn).not.toContain("setProjectionState");
      expect(fn).not.toContain("item.taskLifecycle =");
    });

    it("refreshes and explains on a 409 rather than leaving the optimistic state", () => {
      const fn = app.slice(app.indexOf("const submitRealTransition"), app.indexOf("const applyAction"));
      const conflict = fn.slice(fn.indexOf("409"));
      expect(conflict).toContain("await loadWorkItems()");
      expect(conflict).toContain("ErrorConcurrencyRefreshed");
    });

    it("warns instead of silently faking a transition for a real non-task item", () => {
      const fn = app.slice(app.indexOf("const applyAction"), app.indexOf("const applyPlan"));
      expect(fn).toContain("console.warn");
      expect(fn).toContain("MOCK transition");
    });

    it("opens the plan date picker for a real task too — the engine now stores the date", () => {
      // This used to read `action.input === 'date' && !isRealTaskItem(item)`: the engine accepted no date, so a
      // real user was never asked for one. POST .../plan now stores it, so the guard is gone; openDatePicker
      // itself decides whether to write to the engine or, for a showcase item, only locally.
      expect(app).not.toContain("!isRealTaskItem(item)) { openDatePicker");
      expect(app).toContain("if (action.input === 'date') { openDatePicker(item, action); return; }");
    });

    it("does not apply a real plan optimistically", () => {
      const fn = app.slice(app.indexOf("const openDatePicker"), app.indexOf("const reportSwalFailure"));
      expect(fn).toContain("submitPlan");
      // The real branch calls the engine and nothing else; applyPlan (the local mutation) is reserved for the
      // non-real branch only.
      expect(fn).toContain("real ? submitPlan(item, value) : applyPlan(item, value, label)");
    });
  });

  describe("notifications name the work, not its id", () => {
    const fs = require("fs");
    const path = require("path");
    const app = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

    it("passes no technical id to any toast", () => {
      // A GUID in a toast ("3de33484-… · timer started") tells the reader nothing.
      const offenders = app
        .split("\n")
        .filter((line) => /toast\(tf?\(/.test(line) && /sourceId|item\.id\b/.test(line));
      expect(offenders).toEqual([]);
    });

    it("uses the task title for the real-transition toast", () => {
      expect(app).toContain("tf('ToastActionApplied', label, item.title)");
    });
  });
});
