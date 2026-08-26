const { loadScript } = require("./load-script");

/*
 * ONE LAW: if there is no real data for a field, the field is NOT shown — it is never invented.
 *
 * Five bugs of this one class shipped before it was written down (the catalogVisible allowlist dropping real
 * items, an invented pool queue name, the mock user's job title, phantom delegators, and a frozen "today").
 * Every case below is one of those, pinned so it cannot come back quietly.
 *
 * The clock is INJECTED. A test that measured against the real wall clock would start failing tomorrow, which is
 * the same "answers drift with the calendar" defect it is meant to catch.
 */
const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

// 2026-07-26 local — two days after the day the showcase fixtures are authored against.
const PINNED_NOW = () => new Date(2026, 6, 26, 10, 0, 0);

const realItem = (overrides) => Object.assign({
  fixtureKind: "workItem",
  id: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
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
  title: { kind: "display", text: "Banka kredi başvurusu dosyasını gözden geçir", locale: "und" },
  nativeStatus: { code: "Open", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Open" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
    deepLink: "/Tasks/3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234"
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
  dueAt: "2026-07-30T00:00:00+00:00"
}, overrides);

const loadRealMode = () => {
  delete global.WorkCenterNextData;
  delete global.WorkCenterNextApi;
  delete global.WorkCenterNextContract;
  delete global.WorkCenterNextFixtures;
  global.WCN = { t: (key) => key, tn: (key) => key };
  // No data-wcn-fixtures → the real surface, exactly as production renders it.
  document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
  loadScript(scriptRoot + "fixture-contract.js");
  loadScript(scriptRoot + "mock-data.js");
  loadScript(scriptRoot + "work-items-api.js");
  global.WorkCenterNextData.setNowProvider(PINNED_NOW);
};

describe("the clock: real items are measured from the real today", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  /*
   * The reported symptom: due 2026-07-30 read "6 days left" because it was measured from the fixture reference
   * day (2026-07-24) instead of the real one (2026-07-26). Every real date was two days optimistic, and the gap
   * grew by a day every day.
   *
   * WC-2 CHANGED WHAT THIS TEST GUARDS, and the change is worth stating. The day COUNT is still derived here,
   * late, from the absolute due date — that is the half this test was written for and it still holds. The
   * STATE is no longer derived here at all; it is the server's answer, asserted below. So this now pins the
   * count and nothing else.
   */
  it("counts the days from now, not from the day the fixtures were authored", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([realItem()]).items;

    expect(item.slaDiffDays).toBe(4);
    expect(item.slaDiffDays).not.toBe(6);
  });

  it("still counts backwards for work that is already late", () => {
    // The dangerous direction: the wording must say how late, not how soon.
    const [item] = global.WorkCenterNextApi
      .mapPayload([realItem({ dueAt: "2026-07-22T00:00:00+00:00" })]).items;

    expect(item.slaDiffDays).toBe(-4);
  });

  it("reports today as the real today", () => {
    expect(global.WorkCenterNextData.todayIso).toBe("2026-07-26");
    expect(global.WorkCenterNextData.todayIso).not.toBe("2026-07-24");
  });
});

describe("identity: nothing about the user is invented", () => {
  beforeEach(() => {
    loadRealMode();
    global.CurrentUser = {
      id: "11111111-2222-3333-4444-555555555555",
      firstName: "Diten",
      lastName: "Admin",
      email: "admin@diten.com"
    };
  });
  afterEach(() => {
    delete global.CurrentUser;
    global.WorkCenterNextData?.setNowProvider(null);
  });

  it("uses the signed-in user, not the showcase persona", () => {
    expect(global.WorkCenterNextData.currentUser.name).toBe("Diten Admin");
    expect(global.WorkCenterNextData.currentUser.name).not.toBe("Selin Aras");
    expect(global.WorkCenterNextData.currentUser.id).toBe("11111111-2222-3333-4444-555555555555");
  });

  // There is no source for a position/title on the client. Absent beats plausible-but-wrong: the real user was
  // being shown "Operasyon PMO Lideri", which is someone else's job.
  it("shows no job title, because there is no source for one", () => {
    expect(global.WorkCenterNextData.currentUser.title).toBeNull();
    expect(global.WorkCenterNextData.currentUser.title).not.toBe("Operasyon PMO Lideri");
  });

  it("falls back to the email rather than to a made-up name", () => {
    global.CurrentUser = { id: "x", email: "admin@diten.com" };
    expect(global.WorkCenterNextData.currentUser.name).toBe("admin@diten.com");
  });

  // Platform exposes no "who delegated to me" seam, so the scope selector must not offer colleagues who do not
  // exist. The code path stays wired — this asserts the DATA is empty, not that the feature was deleted.
  it("offers no delegation scopes while there is no delegation data", () => {
    expect(global.WorkCenterNextData.delegators).toEqual([]);
  });
});

describe("priority: the data precondition for hiding it", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  /*
   * SCOPE WARNING: this pins the DATA only — that a real item genuinely has no priority. The hiding itself
   * (priorityChip, the table column, the filter control) lives in app.js, which has no test harness in this repo
   * (see the note at the top of work-items-api.js). So the render half is covered by reading and by live
   * verification, NOT by this test. Do not read a green run here as proof the chip is gone.
   */
  it("leaves a real item with no priority at all", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([realItem()]).items;

    // Not "medium", not "" — the projection has no such field and the contract does not define one (BL-032).
    expect(item.priority).toBeUndefined();
    // This is exactly what hasPriority() keys off in app.js.
    expect(["high", "medium", "low"]).not.toContain(item.priority);
  });
});

describe("module names come from the resx, in every language", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("resolves the provider code through a resource key", () => {
    global.WCN = { t: (key) => (key === "ModuleTasks" ? "Tasks" : key), tn: (key) => key };

    const [item] = global.WorkCenterNextApi.mapPayload([realItem()]).items;

    expect(item.sourceModule).toBe("Tasks");
  });

  it("shows the raw code and warns when a provider has no resx entry", () => {
    const warnings = [];
    const originalWarn = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));
    try {
      const payload = realItem();
      payload.source = Object.assign({}, payload.source, { providerCode: "brand-new-module" });
      const [item] = global.WorkCenterNextApi.mapPayload([payload]).items;

      expect(item.sourceModule).toBe("brand-new-module");
      expect(warnings.some((w) => w.includes("ModuleBrandNewModule"))).toBe(true);
    } finally {
      console.warn = originalWarn;
    }
  });
});

describe("provenance cannot drift silently", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  it("warns when an item is re-projected as a different origin", () => {
    const warnings = [];
    const originalWarn = console.warn;
    console.warn = (...args) => warnings.push(args.join(" "));
    try {
      const [item] = global.WorkCenterNextApi.mapPayload([realItem()]).items;
      const fixtureShaped = Object.assign({}, item, { provenance: "fixture" });

      global.WorkCenterNextData.toPresentation(fixtureShaped, { provenance: "api" });

      expect(warnings.some((w) => w.includes("re-projected as provenance"))).toBe(true);
    } finally {
      console.warn = originalWarn;
    }
  });

  it("keeps the origin when the caller states it", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([realItem()]).items;
    const again = global.WorkCenterNextData.toPresentation(item, { provenance: item.provenance });

    expect(again.provenance).toBe("api");
  });
});

describe("the mock exports nothing the shell does not read", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  // These four had zero readers and were removed. An unused mock export is a standing invitation to start
  // reading it, which is how three of the five bugs began.
  it("no longer exposes the dead members", () => {
    ["onBehalfOf", "status", "computeSla", "computeBlocked"].forEach((member) => {
      expect(global.WorkCenterNextData[member]).toBeUndefined();
    });
  });
});

describe("showcase mode is unchanged", () => {
  beforeEach(() => {
    delete global.WorkCenterNextData;
    delete global.WorkCenterNextContract;
    delete global.WorkCenterNextFixtures;
    delete global.WorkCenterNextFixtureFactory;
    delete global.WorkCenterNextMigrationAdapter;
    global.WCN = { t: (key) => key, tn: (key) => key };
    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures="showcase"></div>';
    [
      "fixture-contract.js",
      "fixtures/canonical-fixtures.js",
      "fixtures/inbox-showcase-fixtures.js",
      "fixtures/islerim-showcase-fixtures.js",
      "fixtures/edge-case-fixtures.js",
      "fixtures/provider-examples/enterprise-strategy-fixtures.js",
      "fixtures/provider-examples/documentation-fixtures.js",
      "fixtures/trigger-only-fixtures.js",
      "fixtures/migration-fixtures.js",
      "migration-fixture-adapter.js",
      "mock-data.js"
    ].forEach((file) => loadScript(scriptRoot + file));
    global.WorkCenterNextData.setNowProvider(PINNED_NOW);
  });
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  // The demo catalogue is authored against a fixed day. Moving it onto the real clock would make every showcase
  // SLA badge drift out of the state it was written to demonstrate — so the split is by provenance, and this is
  // the half that must NOT change.
  it("keeps measuring fixtures from the authored reference day", () => {
    expect(global.WorkCenterNextData.todayIso).toBe("2026-07-24");
  });

  it("still produces the showcase catalogue with its demo people", () => {
    const items = global.WorkCenterNextData.buildItems();

    expect(items.length).toBeGreaterThan(0);
    expect(global.WorkCenterNextData.currentUser.name).toBe("Selin Aras");
    expect(global.WorkCenterNextData.delegators.length).toBeGreaterThan(0);
  });

  it("still gives fixtures the SLA state they were authored to show", () => {
    const dated = global.WorkCenterNextData.buildItems().filter((i) => i.dueAt);

    expect(dated.length).toBeGreaterThan(0);
    dated.forEach((item) => expect(["overdue", "due-soon", "on-track"]).toContain(item.slaState));
  });
});

describe("a parked task shows why, and a blocked one says what blocks it", () => {
  beforeEach(loadRealMode);
  afterEach(() => global.WorkCenterNextData?.setNowProvider(null));

  const waiting = (overrides) => realItem(Object.assign({
    normalizedStatus: "Waiting",
    taskLifecycle: "Waiting",
    waitingContext: {
      type: "externalInformation",
      // WHY, as the user typed it. WHO is unknown, so waitingOn is absent rather than carrying the reason.
      reason: { kind: "display", text: "Muhasebeden banka ekstresi bekleniyor", locale: "und" },
      since: "2026-07-26T10:00:00+00:00"
    }
  }, overrides));

  it("renders the reason the holder typed, which used to be invisible", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([waiting()]).items;

    expect(item.waitingReason).toBe("Muhasebeden banka ekstresi bekleniyor");
    // waitingOn answers WHO. Nothing resolves an identity yet, so it stays empty instead of holding the reason.
    expect(item.waitingOn).toBeNull();
  });

  it("does not invent an end date for the wait", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([waiting()]).items;

    // Copying the task's own due date announced "waiting until 22 July" on a date already past.
    expect(item.waitingContext.expectedUntil).toBeUndefined();
  });

  it("still shows a real waiting-on person when one is known", () => {
    const payload = waiting();
    payload.waitingContext.waitingOn = { id: "USR-9", displayName: "Merve Şahin" };

    const [item] = global.WorkCenterNextApi.mapPayload([payload]).items;

    expect(item.waitingOn).toBe("Merve Şahin");
  });

  /*
   * The row-leading action. A disabled primary must stay in the lead carrying its reason: hiding it promoted
   * `cancel`, so an approval-blocked task read as "the only thing I can do is call this off".
   */
  it("keeps a disabled primary in the lead and never promotes a destructive action", () => {
    const payload = realItem({
      actions: [
        {
          code: "start",
          label: { kind: "resource", key: "WorkAggregation_Action_Start" },
          semanticType: "start",
          enabled: false,
          source: "provider",
          disabledReasonCode: "APPROVAL_PENDING",
          disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_ApprovalPending" },
          requiresConfirmation: false,
          requiresReason: false,
          requiresEvidence: false,
          supportsBulk: false,
          riskLevel: "normal"
        },
        {
          code: "cancel",
          label: { kind: "resource", key: "WorkAggregation_Action_Cancel" },
          semanticType: "cancel",
          enabled: true,
          source: "provider",
          disabledReasonCode: null,
          disabledReason: null,
          requiresConfirmation: true,
          requiresReason: false,
          requiresEvidence: false,
          supportsBulk: false,
          riskLevel: "destructive"
        }
      ],
      primaryActionCode: "start"
    });

    const [item] = global.WorkCenterNextApi.mapPayload([payload]).items;
    const start = item.actions.find((a) => a.code === "start");
    const cancel = item.actions.find((a) => a.code === "cancel");

    // The blocked action carries its own explanation…
    expect(start.disabled).toBe(true);
    expect(start.disabledReasonKey).toBe("WorkAggregation_ActionDisabled_ApprovalPending");
    // …and it is still the projected primary, so the shell can lead the row with it.
    expect(start.primary).toBe(true);
    expect(cancel.primary).toBe(false);
    // The engine says "destructive"; this file only ever tested for "danger", so the risk never reached the UI.
    expect(cancel.destructive).toBe(true);
    expect(cancel.kind).toBe("danger");
  });
});
