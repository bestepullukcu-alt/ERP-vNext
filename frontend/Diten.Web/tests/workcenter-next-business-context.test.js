const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * Phase 5, the visible end — configurable values reaching the screen.
 *
 * THE DEFECT, and it was worse than it looked. The provider declared the `businessContext` capability whenever a
 * task had configurable values, and nothing ever produced the container. The contract couples the two, and
 * validateItems DROPS what it cannot validate — so such a task did not render its values badly, it DISAPPEARED
 * from the surface entirely while the API kept returning them. The first test below pins that mechanism, because
 * the diagnosis is worth keeping: this rule was deliberately left permissive twice (BL-038, BL-031) to avoid
 * losing items, and here its strictness was correct and the provider was wrong.
 */
const SCRIPT_ROOT = "wwwroot/assets/js/WorkCenterNext/";

const realItem = (overrides) => Object.assign({
  fixtureKind: "workItem",
  id: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
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
  title: { kind: "display", text: "Görev", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "K" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: "3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234",
    deepLink: "/Tasks/3f2b1a09-77c4-4f11-9a2d-0c5b8e6d1234"
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null,
  slaState: "no-sla"
}, overrides);

const loadRealMode = () => {
  ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
    .forEach((key) => { delete global[key]; });
  global.WCN = { t: (key) => key, tf: (key) => key, tn: (key) => key };
  document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures=""></div>';
  loadScript(SCRIPT_ROOT + "fixture-contract.js");
  loadScript(SCRIPT_ROOT + "mock-data.js");
  loadScript(SCRIPT_ROOT + "work-items-api.js");
};

describe("WC: capability without container is fatal, and that is correct", () => {
  beforeEach(loadRealMode);

  it("DROPS an item that declares businessContext and sends none", () => {
    /*
     * The exact state the provider shipped. This is not a cosmetic contract complaint — the item is gone, so a
     * task with configurable values was invisible while GET /Tasks/api/{id} still returned its fieldValues.
     */
    const { items, errors } = global.WorkCenterNextApi.mapPayload([
      realItem({ workItemCapabilities: ["planning", "execution", "businessContext"] })
    ]);

    expect(items).toHaveLength(0);
    expect(errors.map((e) => e.code)).toContain("CAPABILITY_CONTAINER_REQUIRED");
  });

  it("DROPS an item that sends the container and declares no capability", () => {
    // The other direction, equally fatal. "Half of it, never" is enforced both ways.
    const { items, errors } = global.WorkCenterNextApi.mapPayload([
      realItem({ businessContext: { sections: [] } })
    ]);

    expect(items).toHaveLength(0);
    expect(errors.map((e) => e.code)).toContain("CAPABILITY_REQUIRED_FOR_DATA");
  });

  it("KEEPS an item that sends both", () => {
    // Non-vacuity: if validateItems dropped everything, the two tests above would pass while the surface was
    // permanently empty.
    const { items, errors } = global.WorkCenterNextApi.mapPayload([
      realItem({
        workItemCapabilities: ["planning", "execution", "businessContext"],
        businessContext: { sections: [] }
      })
    ]);

    expect(items).toHaveLength(1);
    expect(errors).toEqual([]);
  });

  it("KEEPS an item that sends neither", () => {
    const { items, errors } = global.WorkCenterNextApi.mapPayload([realItem()]);

    expect(items).toHaveLength(1);
    expect(errors).toEqual([]);
  });
});

describe("WC: the detail surface renders the business context", () => {
  const withContext = (sections) => realItem({
    workItemCapabilities: ["planning", "execution", "businessContext"],
    businessContext: { sections }
  });

  const boot = (item) => bootSurface({
    rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${item.id}"`,
    items: [item]
  });

  it("paints a section per declared section", async () => {
    await boot(withContext([
      {
        title: { kind: "display", text: "Uyum", locale: "und" },
        fields: [{ label: { kind: "display", text: "Mevzuat Aşaması", locale: "und" }, valueType: "text", value: "Faz 2" }]
      },
      {
        title: { kind: "display", text: "Finans", locale: "und" },
        fields: [{ label: { kind: "display", text: "Tutar", locale: "und" }, valueType: "currency", value: "₺100" }]
      }
    ]));

    expect(app().querySelectorAll(".wcn-business-section").length).toBeGreaterThanOrEqual(2);
    expect(app().textContent).toContain("Uyum");
    expect(app().textContent).toContain("Finans");
  });

  it("shows a TENANT section's own name rather than a generic heading", async () => {
    /*
     * The gap this slice closed in the shell: sectionHead took a resource KEY and only a key, so a display title
     * was looked up, missed, and replaced by the generic "BusinessContextLabel". The administrator's own section
     * name vanished — the same raw-key/lost-label family of defect, one level up from the fields.
     */
    await boot(withContext([{
      title: { kind: "display", text: "Kiracının Kendi Bölümü", locale: "und" },
      fields: [{ label: { kind: "display", text: "Alan", locale: "und" }, valueType: "text", value: "v" }]
    }]));

    expect(app().textContent).toContain("Kiracının Kendi Bölümü");
    expect(app().textContent).not.toContain("BusinessContextLabel");
  });

  it("still resolves a SYSTEM section's resource key", async () => {
    // Non-vacuity for the test above: a renderer that only printed `text` would lose every system section.
    await boot(withContext([{
      title: { kind: "resource", key: "IsCtxFinancials" },
      fields: [{ label: { kind: "resource", key: "IsFactAmount" }, valueType: "currency", value: "₺1" }]
    }]));

    // The harness's t() echoes the key, so seeing it proves the resource path was taken.
    expect(app().textContent).toContain("IsCtxFinancials");
  });

  it("prints a tenant field's own label and its value", async () => {
    await boot(withContext([{
      title: { kind: "display", text: "Uyum", locale: "und" },
      fields: [{ label: { kind: "display", text: "Mevzuat Aşaması", locale: "und" }, valueType: "text", value: "Faz 2" }]
    }]));

    expect(app().textContent).toContain("Mevzuat Aşaması");
    expect(app().textContent).toContain("Faz 2");
  });

  it("never prints a field's definition code", async () => {
    // The code is an identifier, not a name. It does not travel in the container at all, and this pins that.
    await boot(withContext([{
      title: { kind: "display", text: "Uyum", locale: "und" },
      fields: [{ label: { kind: "display", text: "Mevzuat Aşaması", locale: "und" }, valueType: "text", value: "Faz 2" }]
    }]));

    expect(app().textContent).not.toContain("regulatory.phase");
  });

  it("says so when the capability is declared with nothing in it", async () => {
    // A legitimate state for a task whose only values were redacted — an empty card beats a missing one,
    // because the reader can see the section exists.
    await boot(withContext([]));

    expect(app().textContent).toContain("EmptyBusinessContext");
  });
});
