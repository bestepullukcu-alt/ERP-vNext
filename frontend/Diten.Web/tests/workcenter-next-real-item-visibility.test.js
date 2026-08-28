const { loadScript } = require("./load-script");

/*
 * MOD-0024 — a genuinely created task must SURVIVE the whole client chain and appear in the Task Center.
 *
 * The bug this covers: `catalogVisible` was computed as `VISIBLE_CATALOG_IDS.has(item.id)` — an allowlist of
 * SHOWCASE FIXTURE ids — and that gate was applied to real projection items too. A real item has a GUID, so it
 * was never in the list, so every created task was filtered out of every tab with no error anywhere.
 *
 * The payload below is the VERBATIM serialized output of TaskWorkItemProvider (captured from the running
 * provider, not hand-written), so this test fails if either side of the contract drifts. The C# counterpart is
 * TaskWorkItemProviderWireContractTests, which pins the same shape from the server side.
 */
const REAL_PROJECTION_ITEM = {
  fixtureKind: "workItem",
  id: "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0",
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: "CT probe", locale: "und" },
  nativeStatus: { code: "Open", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Open" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0",
    deepLink: "/Tasks/ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0"
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [{
    code: "start",
    label: { kind: "resource", key: "WorkAggregation_Action_Start" },
    semanticType: "start",
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
  dueAt: "2026-08-01T00:00:00+00:00"
};

describe("a real task reaches the Task Center", () => {
  let warnings;
  let originalWarn;

  beforeEach(() => {
    warnings = [];
    originalWarn = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));

    delete global.WorkCenterNextData;
    delete global.WorkCenterNextApi;
    delete global.WorkCenterNextFixtures;
    /*
     * The resx bridge IS loaded — WCN.moduleLabel lives in it and the source chip resolves through it — but its
     * store is empty in jsdom and the identity translator sits on top, so resource labels still legitimately
     * fall back to their key, which is what the rest of this file asserts against.
     */
    delete global.WCN;
    loadScript("wwwroot/assets/js/WorkCenterNext/l10n.js");
    Object.assign(global.WCN, { t: (key) => key, tn: (key) => key });

    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/mock-data.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  });

  afterEach(() => {
    console.warn = originalWarn;
  });

  it("passes the executable contract (otherwise the client drops it silently)", () => {
    const result = global.WorkCenterNextContract.validateWorkItem(REAL_PROJECTION_ITEM);
    expect(result.errors).toEqual([]);
    expect(result.valid).toBe(true);
  });

  it("survives the catalog filter — the bug that hid every created task", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]);

    expect(errors).toEqual([]);
    expect(items).toHaveLength(1);
    // `inTab` in app.js excludes anything with catalogVisible === false.
    expect(items[0].catalogVisible).not.toBe(false);
  });

  it("shows the title the user typed, not a resource key", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]).items;

    expect(item.titleText).toBe("CT probe");
    expect(item.title).toBe("CT probe");
    expect(item.title).not.toMatch(/^WorkAggregation_/);
  });

  it("lands in İşlerim / aktif so the user can actually find it", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]).items;

    expect(item.tab).toBe("islerim");
    expect(item.dismissed).toBe(false);
    expect(global.WorkCenterNextData.segmentFor(item)).toBe("aktif");
  });

  it("names its owning module instead of leaking a provider code", () => {
    // The module name comes from the resx now, not a hardcoded Turkish map, so the l10n stub has to answer for
    // the derived key (tasks → ModuleTasks). The rest of this file leaves t() as identity on purpose.
    Object.assign(global.WCN, { t: (key) => (key === "ModuleTasks" ? "Görevler" : key), tn: (key) => key });

    const [item] = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]).items;

    // providerCode "tasks" matches the manifest ModuleCode, so the shell can label it.
    expect(item.sourceModule).toBe("Görevler");
    expect(item.sourceModule).not.toBe("task-engine");
    expect(item.sourceModule).not.toBe("tasks");
  });

  it("is not warned about — a real item must never hit the showcase filter", () => {
    global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]);
    expect(warnings.filter((w) => w.includes("hidden by the showcase catalog filter"))).toEqual([]);
  });

  describe("the showcase filter still curates fixtures, and says so", () => {
    it("hides a parked fixture and explains why", () => {
      const parked = { ...REAL_PROJECTION_ITEM, id: "NOT-IN-THE-CATALOG" };

      const item = global.WorkCenterNextData.toPresentation(parked, { provenance: "fixture" });

      expect(item.catalogVisible).toBe(false);
      const warning = warnings.find((w) => w.includes("hidden by the showcase catalog filter"));
      expect(warning).toBeDefined();
      expect(warning).toContain("NOT-IN-THE-CATALOG");
      expect(warning).toContain("sourceModule=");
    });

    it("keeps an allowlisted fixture visible", () => {
      const listed = { ...REAL_PROJECTION_ITEM, id: "ISLERIM-WORK-ACTIVE" };
      const item = global.WorkCenterNextData.toPresentation(listed, { provenance: "fixture" });
      expect(item.catalogVisible).toBe(true);
    });

    it("defaults to visible when provenance is not stated", () => {
      // Chosen deliberately: an unstated provenance must not hide real work.
      const item = global.WorkCenterNextData.toPresentation(REAL_PROJECTION_ITEM);
      expect(item.catalogVisible).toBe(true);
    });
  });

  describe("a missing resource label is reported", () => {
    it("warns once and names the key, instead of silently rendering gibberish", () => {
      const withResourceTitle = {
        ...REAL_PROJECTION_ITEM,
        title: { kind: "resource", key: "WorkAggregation_Title_Task" }
      };

      const item = global.WorkCenterNextData.toPresentation(withResourceTitle, { provenance: "api" });

      expect(item.titleText).toBe("WorkAggregation_Title_Task");
      const warning = warnings.find((w) => w.includes('"WorkAggregation_Title_Task"'));
      expect(warning).toBeDefined();
      expect(warning).toContain("display");
    });
  });
  describe("who the work belongs to", () => {
    it("still passes the executable contract with the person fields present", () => {
      const result = global.WorkCenterNextContract.validateWorkItem(REAL_PROJECTION_ITEM);
      expect(result.errors).toEqual([]);
      expect(result.valid).toBe(true);
    });

    it("shows 'Me' for the caller instead of leaving the assignee blank", () => {
      // The detail pane rendered "ATANAN —" because the projection carried no assignee at all.
      const [item] = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]).items;

      expect(item.assignee).toBe("PersonSelf");
      expect(item.assignee).not.toBe("");
    });

    it("never renders a raw user id", () => {
      const [item] = global.WorkCenterNextApi.mapPayload([REAL_PROJECTION_ITEM]).items;

      // Someone else's name is not resolvable yet, but a GUID must never be shown as a person.
      expect(item.requester).toBe("PersonNameUnavailable");
      expect(item.requester).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}/);
      expect(item.assignee).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}/);
    });

    it("prefers a real display name when the provider can supply one", () => {
      const named = {
        ...REAL_PROJECTION_ITEM,
        assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", displayName: "Selin Aras", isCurrentUser: true }
      };

      const [item] = global.WorkCenterNextApi.mapPayload([named]).items;
      expect(item.assignee).toBe("Selin Aras");
    });

    it("leaves the field empty when there is genuinely no person (unclaimed pool)", () => {
      const pooled = { ...REAL_PROJECTION_ITEM };
      delete pooled.assignee;

      const [item] = global.WorkCenterNextApi.mapPayload([pooled]).items;
      expect(item.assignee).toBe("");
    });
  });
});
