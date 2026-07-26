const { loadScript } = require("./load-script");

/*
 * BL-031 (c) — the Havuz tab must not invent a team.
 *
 * The bug this covers: toPresentation set `group = "Operasyon Kuyruğu"` for ANY item whose assignmentMode was
 * "groupQueue". No such queue exists, and the projection carries no pool identity at all — a real item says only
 * THAT it is pool work, never WHICH pool — so genuine CFO-pool items were labelled with a fabricated team name.
 * Naming the queue for real items is WC-3 contract work (BL-031 a/b); until then a real item carries no group.
 */
const REAL_POOL_ITEM = {
  fixtureKind: "workItem",
  id: "0b4f6d21-9d4a-4c7e-9a1f-1b2c3d4e5f60",
  workIntent: "task",
  assignmentMode: "groupQueue",
  ownershipState: "unowned",
  admissionState: "pendingClaim",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: "Yatırımcı sunumu için finansal özet hazırla", locale: "und" },
  nativeStatus: { code: "Open", label: { kind: "resource", key: "WorkAggregation_TaskStatus_Open" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: "0b4f6d21-9d4a-4c7e-9a1f-1b2c3d4e5f60",
    deepLink: "/Tasks/0b4f6d21-9d4a-4c7e-9a1f-1b2c3d4e5f60"
  },
  assignee: null,
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [{
    code: "claim",
    label: { kind: "resource", key: "WorkAggregation_Action_Claim" },
    semanticType: "claim",
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

describe("the pool never shows a queue the projection does not name", () => {
  beforeEach(() => {
    delete global.WorkCenterNextData;
    delete global.WorkCenterNextApi;
    delete global.WorkCenterNextFixtures;
    global.WCN = { t: (key) => key, tn: (key) => key };

    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/mock-data.js");
    loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  });

  it("passes the executable contract, so the assertions below are about a real payload", () => {
    const result = global.WorkCenterNextContract.validateWorkItem(REAL_POOL_ITEM);
    expect(result.errors).toEqual([]);
    expect(result.valid).toBe(true);
  });

  it("gives a real group-queue item NO group name", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([REAL_POOL_ITEM]);

    expect(errors).toEqual([]);
    expect(items).toHaveLength(1);
    expect(items[0].assignmentMode).toBe("groupQueue");
    // The regression: this used to be "Operasyon Kuyruğu".
    expect(items[0].group).toBeNull();
  });

  it("never puts the showcase queue name on real work", () => {
    const [item] = global.WorkCenterNextApi.mapPayload([REAL_POOL_ITEM]).items;

    expect(item.group).not.toBe("Operasyon Kuyruğu");
    expect(JSON.stringify(item)).not.toContain("Operasyon Kuyruğu");
  });

  it("leaves nothing for the Havuz group selector to render", () => {
    const { items } = global.WorkCenterNextApi.mapPayload([REAL_POOL_ITEM]);

    // buildGroupSelector collects distinct truthy item.group values and renders nothing when the list is empty,
    // which is how the tab stops asserting a team it cannot know.
    const groups = items.map((i) => i.group).filter(Boolean);
    expect(groups).toEqual([]);
  });
});

describe("showcase fixtures may still name their own queue", () => {
  beforeEach(() => {
    delete global.WorkCenterNextData;
    delete global.WorkCenterNextApi;
    delete global.WorkCenterNextFixtures;
    global.WCN = { t: (key) => key, tn: (key) => key };

    delete global.WorkCenterNextContract;
    delete global.WorkCenterNextFixtureFactory;
    delete global.WorkCenterNextMigrationAdapter;
    // Showcase mode — the Development catalog, gated by the same server-set flag Development uses.
    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures="showcase"></div>';
    [
      "fixture-contract.js",
      "fixtures/canonical-fixtures.js",
      "fixtures/inbox-showcase-fixtures.js",
      "fixtures/edge-case-fixtures.js",
      "fixtures/provider-examples/enterprise-strategy-fixtures.js",
      "fixtures/provider-examples/documentation-fixtures.js",
      "fixtures/trigger-only-fixtures.js",
      "fixtures/migration-fixtures.js",
      "migration-fixture-adapter.js",
      "mock-data.js"
    ].forEach((file) => loadScript("wwwroot/assets/js/WorkCenterNext/" + file));
  });

  it("does not invent one for them either", () => {
    const items = global.WorkCenterNextData.buildItems();

    // The showcase catalog declares no `group` on any fixture today, so nothing should carry one — least of all
    // the name that used to be derived from assignmentMode. A fixture that DOES declare a group keeps it; that
    // path is the `item.group ||` branch in toPresentation.
    expect(items.length).toBeGreaterThan(0);
    expect(items.filter((i) => i.group)).toEqual([]);
    expect(JSON.stringify(items)).not.toContain("Operasyon Kuyruğu");
  });
});
