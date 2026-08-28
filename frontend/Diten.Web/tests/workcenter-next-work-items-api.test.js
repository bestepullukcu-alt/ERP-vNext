const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

// A representative WC-1 projection item, exactly as the backend serializes it (camelCase, resource labels).
const projectionItem = (overrides = {}) => Object.assign({
  fixtureKind: "workItem",
  id: "11111111-1111-1111-1111-111111111111",
  workIntent: "approval",
  assignmentMode: "approval",
  ownershipState: "notApplicable",
  admissionState: "notApplicable",
  normalizedStatus: "Pending",
  taskLifecycle: "notApplicable",
  executionState: "notApplicable",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "resource", key: "WorkAggregation_Title_Approval", args: { objectType: "invoice", objectId: "INV-42" } },
  nativeStatus: { code: "WaitingApproval", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
  source: { providerCode: "workflow", providerContractVersion: "1.0", objectType: "invoice", objectId: "INV-42", deepLink: null },
  lifecycleOwner: "workflow",
  workItemCapabilities: [],
  actions: [
    {
      code: "approve",
      label: { kind: "resource", key: "WorkAggregation_Action_Approve" },
      semanticType: "approve",
      enabled: true,
      source: "provider",
      disabledReasonCode: null,
      disabledReason: null,
      requiresConfirmation: true,
      requiresReason: false,
      requiresEvidence: false,
      supportsBulk: true,
      riskLevel: "normal"
    }
  ],
  concurrency: { kind: "version", token: "17" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

describe("WorkCenterNext work-items API seam (WC-1b)", () => {
  beforeEach(() => {
    delete global.WorkCenterNextContract;
    delete global.WorkCenterNextData;
    delete global.WorkCenterNextApi;
    delete global.WorkCenterNextFixtures;
    delete global.WCN;
    ["l10n.js", "fixture-contract.js", "mock-data.js", "work-items-api.js"]
      .forEach((file) => loadScript(scriptRoot + file));
  });

  it("maps a canonical projection item through the SHARED presentation mapper", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([projectionItem()]);

    expect(errors).toEqual([]);
    expect(items).toHaveLength(1);
    const item = items[0];
    // Presentation fields produced by the existing mapper — proof the real path reuses it.
    expect(item.itemType).toBe("approval");
    expect(item.status).toBe("Pending");
    expect(item.tab).toBe("inbox");
    expect(item.sourceType).toBe("invoice");
    expect(item.sourceId).toBe("INV-42");
    expect(item.actions[0].code).toBe("approve");
    expect(item.actions[0].enabled).toBe(true);
  });

  it("passes the executable contract (validateWorkItem) for every projected item", () => {
    const result = global.WorkCenterNextContract.validateWorkItem(
      global.WorkCenterNextApi.adaptProjection(projectionItem()));
    expect(result.errors).toEqual([]);
    expect(result.valid).toBe(true);
  });

  it("folds the additive escalation object onto the escalated signal (DEC-2)", () => {
    const { items } = global.WorkCenterNextApi.mapPayload([
      projectionItem({ escalation: { escalated: true, level: 2, since: null } })
    ]);
    expect(items[0].escalated).toBe(true);

    const plain = global.WorkCenterNextApi.mapPayload([projectionItem()]);
    expect(plain.items[0].escalated).toBe(false);
  });

  it("normalizes a full ISO dueAt to the date-only form the SLA maths expects", () => {
    expect(global.WorkCenterNextApi.toDateOnly("2026-07-30T12:00:00+03:00")).toBe("2026-07-30");
    expect(global.WorkCenterNextApi.toDateOnly(null)).toBeNull();

    const { items } = global.WorkCenterNextApi.mapPayload([
      projectionItem({ dueAt: "2026-07-30T12:00:00+03:00" })
    ]);
    expect(items[0].slaState).toBeDefined();
    expect(items[0].slaDiffDays).not.toBeNaN();
  });

  it("drops an item that violates the contract instead of rendering it broken", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([
      projectionItem(),
      projectionItem({ id: "bad-1", normalizedStatus: "NotARealStatus" })
    ]);
    expect(items).toHaveLength(1);
    expect(errors.length).toBeGreaterThan(0);
  });

  it("unwraps the Response<T> envelope and bare arrays alike", () => {
    const api = global.WorkCenterNextApi;
    expect(api.unwrap({ data: [1, 2], isSuccessful: true })).toEqual([1, 2]);
    expect(api.unwrap([1, 2])).toEqual([1, 2]);
    expect(api.unwrap(null)).toEqual([]);
  });

  it("classifies degraded responses so the shell can show the right state", () => {
    const api = global.WorkCenterNextApi;
    expect(api.classify(401)).toBe(api.STATUS.UNAUTHORIZED);
    expect(api.classify(403)).toBe(api.STATUS.FORBIDDEN);
    expect(api.classify(503)).toBe(api.STATUS.UNAVAILABLE);
    expect(api.classify(500)).toBe(api.STATUS.ERROR);
  });

  it("calls the SAME-ORIGIN proxy and never a service port", async () => {
    const calls = [];
    global.fetch = (url, opts) => {
      calls.push({ url, opts });
      return Promise.resolve({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ data: [projectionItem()], isSuccessful: true })
      });
    };

    const result = await global.WorkCenterNextApi.fetchWorkItems();

    expect(result.status).toBe(global.WorkCenterNextApi.STATUS.OK);
    expect(result.items).toHaveLength(1);
    expect(calls[0].url).toBe("/WorkCenterNext/api/work-items");
    expect(calls[0].url).not.toMatch(/5056|5057|localhost/);
    expect(calls[0].opts.credentials).toBe("same-origin");
  });

  it("surfaces 403 as forbidden with no items (expected before the permission grant)", async () => {
    global.fetch = () => Promise.resolve({ ok: false, status: 403, json: () => Promise.resolve({}) });
    const result = await global.WorkCenterNextApi.fetchWorkItems();
    expect(result.status).toBe(global.WorkCenterNextApi.STATUS.FORBIDDEN);
    expect(result.items).toEqual([]);
  });

  it("treats a network failure as dependency-unavailable rather than crashing", async () => {
    global.fetch = () => Promise.reject(new Error("network down"));
    const result = await global.WorkCenterNextApi.fetchWorkItems();
    expect(result.status).toBe(global.WorkCenterNextApi.STATUS.UNAVAILABLE);
  });

  /*
   * ── WC-D3 (DCP-004 §2 D3) — A PARTIAL BOARD REACHES THE SHELL AS A PARTIAL BOARD ──────────────────────────
   *
   * The server answers `{ items, unavailableSources }` now, because a provider that failed or timed out used to
   * leave nothing behind: the list simply got shorter and looked exactly as complete as a healthy one.
   */
  it("reads the BOARD envelope: items arrive AND the missing sources are carried, not dropped", async () => {
    global.fetch = () => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({
        isSuccessful: true,
        data: {
          items: [projectionItem()],
          unavailableSources: [{ providerCode: "tasks", reasonCode: "TIMEOUT" }]
        }
      })
    });

    const result = await global.WorkCenterNextApi.fetchWorkItems();

    // A partial board is a SUCCESS with rows on it — the rows that did arrive are never thrown away.
    expect(result.status).toBe(global.WorkCenterNextApi.STATUS.OK);
    expect(result.items).toHaveLength(1);
    expect(result.unavailableSources).toEqual([{ providerCode: "tasks", reasonCode: "TIMEOUT" }]);
  });

  it("reports NO missing source when every provider answered", async () => {
    global.fetch = () => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({
        isSuccessful: true,
        data: { items: [projectionItem()], unavailableSources: [] }
      })
    });

    const result = await global.WorkCenterNextApi.fetchWorkItems();

    expect(result.items).toHaveLength(1);
    expect(result.unavailableSources).toEqual([]);   // the state the shell reads as "complete"
  });

  // The older shapes still have to render: the fixture path and these tests feed bare arrays, and a proxy in
  // front of an older Platform must not produce an empty page.
  it("still unwraps a bare array and a data-as-array envelope", () => {
    const api = global.WorkCenterNextApi;
    expect(api.unwrap([1, 2])).toEqual([1, 2]);
    expect(api.unwrap({ data: [1] })).toEqual([1]);
    expect(api.unwrap({ data: { items: [1, 2, 3] } })).toEqual([1, 2, 3]);
    expect(api.unwrap({ data: null })).toEqual([]);
    expect(api.unwrapUnavailable({ data: [1] })).toEqual([]);      // legacy shape: nothing claimed missing
  });

  /*
   * An unknown reason CODE is kept, a nameless source is dropped. "A source is missing and we cannot say why"
   * is still true and still worth showing; a blank provider name is not information, it is furniture.
   */
  it("keeps an unrecognised reason code but drops an entry with no provider code", async () => {
    global.fetch = () => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({
        isSuccessful: true,
        data: {
          items: [],
          unavailableSources: [
            { providerCode: "tasks", reasonCode: "SOMETHING_NEW" },
            { reasonCode: "TIMEOUT" }
          ]
        }
      })
    });

    const result = await global.WorkCenterNextApi.fetchWorkItems();

    expect(result.unavailableSources).toEqual([{ providerCode: "tasks", reasonCode: "SOMETHING_NEW" }]);
  });

  // DEC-1 — production must have NO client-reachable path to showcase fixture data. The switch is the
  // server-rendered attribute; without it the fixture source yields nothing no matter what the client does.
  it("yields no fixture data unless the SERVER enabled the showcase catalog", () => {
    document.body.innerHTML = '<div id="wcnApp"></div>';          // production render
    expect(global.WorkCenterNextData.showcaseFixturesEnabled()).toBe(false);
    expect(global.WorkCenterNextData.buildItems()).toEqual([]);
    expect(global.WorkCenterNextData.buildTriggers()).toEqual([]);
    expect(global.WorkCenterNextData.buildMeetings()).toEqual([]);
    /*
     * ⚠ `buildNotes` IS NOT MISSING FROM THIS LIST BY OVERSIGHT (2026-08-25, BL-244). It fed the global notes
     * PANEL, which was deleted a round earlier along with its render; the builder and its fixture went with the
     * handlers this round. There is no builder left to gate.
     *
     * ⚠ The detail page's PERSONAL note card is a different thing and is not affected: it is served by
     * `TasksApi.addPersonalNote`, never by a fixture builder, so it was never on this list to begin with.
     *
     * The RULE this test states is unchanged and still enforced for every builder that exists: nothing yields
     * fixture data unless the server-rendered attribute says so.
     */

    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures="showcase"></div>'; // Development opt-in
    expect(global.WorkCenterNextData.showcaseFixturesEnabled()).toBe(true);
  });
});
