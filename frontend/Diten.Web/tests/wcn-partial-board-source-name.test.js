const { bootSurface, app, SCRIPT_ROOT } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * WC-D3 — THE PARTIAL-BOARD BANNER NAMES ITS MISSING SOURCE IN THE READER'S LANGUAGE.
 *
 * The banner resolves the REASON through the resx but printed the provider CODE verbatim, so an Arabic or
 * Chinese screen read "workflow — <translated sentence>": half a sentence in English, in the one place on the
 * page whose whole job is to explain a gap. The reason bridge was built and the source half was left behind.
 *
 * The name now comes from WCN.moduleLabel (l10n.js) — the same derived-key rule the source chips use
 * (workflow → ModuleWorkflow), so one provider cannot be called two different things on one page.
 *
 * The second rule is the one that matters more: a provider code nobody has a resx entry for must still render.
 * It falls back to the raw code — an identifier a reader can quote to whoever fixes it — and the board draws.
 * A missing name is a labelling gap; it must never become a blank page.
 */

const ID = (n) => `98d1f94e-1848-4539-8a99-77e72651b8a${n}`;

const item = (n) => ({
  fixtureKind: "workItem",
  id: ID(n),
  workIntent: "task",
  // Unaccepted work, so the row lands in the DEFAULT tab (Inbox) and "the board still draws" can be asserted by
  // looking for the row itself rather than for chrome that would render either way.
  assignmentMode: "offered",
  ownershipState: "assigned",
  admissionState: "pendingAcceptance",
  normalizedStatus: "Pending",
  taskLifecycle: "Open",
  executionState: "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: `Görev ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: ID(n),
    deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
});

/*
 * A translator that answers only the keys a real resx answers: the module names and the reason sentences. Every
 * other key echoes back, exactly as the shared harness's default does, so an assertion naming a key is asserting
 * the key the code chose. `ModuleTasks` is deliberately NOT the raw code "tasks" — a test whose expected value
 * equalled the fallback could not tell the two apart.
 */
const TRANSLATOR = {
  t: (key) => ({
    ModuleWorkflow: "İş Akışı",
    ModuleTasks: "Görevler",
    SourceUnavailableTimeout: "zaman aşımı",
    SourceUnavailableError: "hata",
    SourceUnavailableUnknown: "bilinmeyen sebep",
    PartialBoardBanner: "Pano eksik"
  })[key] || key,
  tf: (key) => key,
  tn: (key) => key
};

const banner = () => app().querySelector(".wcn-partial-board-detail");

describe("partial-board banner — the missing source is named, not coded", () => {
  it("prints the localized module name and never the raw provider code", async () => {
    await bootSurface({
      items: [item(1)],
      wcn: TRANSLATOR,
      unavailableSources: [{ providerCode: "workflow", reasonCode: "TIMEOUT" }]
    });

    const text = banner().textContent;
    expect(text).toContain("İş Akışı");
    expect(text).toContain("zaman aşımı");
    // The defect this file exists for: the code itself must not reach the screen when a name exists.
    expect(text).not.toContain("workflow");
  });

  it("names every missing source, not just the first", async () => {
    await bootSurface({
      items: [item(1)],
      wcn: TRANSLATOR,
      unavailableSources: [
        { providerCode: "workflow", reasonCode: "TIMEOUT" },
        { providerCode: "tasks", reasonCode: "ERROR" }
      ]
    });

    const text = banner().textContent;
    expect(text).toContain("İş Akışı");
    expect(text).toContain("Görevler");
    expect(text).not.toContain("workflow");
    // "tasks" is a substring of nothing else here, so its absence is a real assertion.
    expect(text).not.toContain("tasks");
  });

  it("falls back to the raw code for an unknown provider, and still draws the board", async () => {
    await bootSurface({
      items: [item(1)],
      wcn: TRANSLATOR,
      unavailableSources: [{ providerCode: "brand-new-thing", reasonCode: "TIMEOUT" }]
    });

    // The name is missing, so the identifier is shown — a reader can still quote it to whoever fixes it.
    expect(banner().textContent).toContain("brand-new-thing");
    // …and the rest of the surface is unharmed: the row that DID arrive is on screen.
    expect(app().textContent).toContain("Görev 1");
  });

  it("derives the key from the code rather than reading a hand-written map", async () => {
    // master-data → ModuleMasterData. A map would have to gain an entry; the rule does not.
    await bootSurface({
      items: [item(1)],
      wcn: { ...TRANSLATOR, t: (key) => (key === "ModuleMasterData" ? "Ana Veri" : TRANSLATOR.t(key)) },
      unavailableSources: [{ providerCode: "master-data", reasonCode: "ERROR" }]
    });

    expect(banner().textContent).toContain("Ana Veri");
    expect(banner().textContent).not.toContain("master-data");
  });
});

/*
 * THE OTHER HALF OF "ONE IMPLEMENTATION".
 *
 * The banner is not the only place a provider code becomes a name: mock-data's toPresentation writes
 * sourceModule / sourceModuleName for the source CHIP on every row, fixture and real alike. Those two answers
 * came from two separate functions for exactly one turn, which is how a chip and a banner on the same screen
 * could disagree about what "workflow" is called.
 *
 * This pins that they now come from the same one. It runs the SHOWCASE catalogue — the fixture path — because
 * that is the side that used to own the rule and the side a regression would be quietest on.
 */
describe("the source chip resolves through the same one function", () => {
  const FIXTURE_TRANSLATOR = {
    t: (key) => ({ ModuleWorkflow: "İş Akışı", ModuleFinance: "Finans", ModuleQuality: "Kalite" })[key] || key,
    tf: (key) => key,
    tn: (key) => key
  };

  const buildShowcase = () => {
    ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
      .forEach((key) => { delete global[key]; });
    delete global.WCN;
    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures="showcase"></div>';
    loadScript(SCRIPT_ROOT + "l10n.js");
    Object.assign(global.WCN, FIXTURE_TRANSLATOR);
    loadScript(SCRIPT_ROOT + "fixture-contract.js");
    [
      "canonical-fixtures", "inbox-showcase-fixtures", "islerim-showcase-fixtures", "havuz-showcase-fixtures",
      "gecmis-showcase-fixtures", "edge-case-fixtures", "provider-examples/enterprise-strategy-fixtures",
      "provider-examples/documentation-fixtures", "trigger-only-fixtures", "migration-fixtures"
    ].forEach((file) => loadScript(SCRIPT_ROOT + "fixtures/" + file + ".js"));
    loadScript(SCRIPT_ROOT + "migration-fixture-adapter.js");
    loadScript(SCRIPT_ROOT + "task-detail-resolver.js");
    loadScript(SCRIPT_ROOT + "trigger-response-resolver.js");
    loadScript(SCRIPT_ROOT + "mock-data.js");
    return global.WorkCenterNextData.buildItems();
  };

  it("names a showcase item's module from the resx, and falls back to the code otherwise", () => {
    const chips = {};
    buildShowcase().forEach((item) => { chips[item.source && item.source.providerCode] = item.sourceModule; });

    // Answered by the translator → the name. Not answered → the code, never a blank and never a borrowed name.
    expect(chips.workflow).toBe("İş Akışı");
    expect(chips.finance).toBe("Finans");
    expect(chips.quality).toBe("Kalite");
    expect(chips["master-data"]).toBe("master-data");
  });

  it("is the bridge's function doing it — remove it and the chip degrades rather than throwing", () => {
    // Proof that mock-data delegates instead of keeping its own copy: with the bridge's moduleLabel taken away,
    // EVERY chip falls back to the raw code. A surviving private implementation would still answer "İş Akışı".
    const items = buildShowcase();
    expect(items.find((i) => i.source.providerCode === "workflow").sourceModule).toBe("İş Akışı");

    delete global.WCN.moduleLabel;
    const [degraded] = [global.WorkCenterNextData.toPresentation(
      items.find((i) => i.source.providerCode === "workflow"), { provenance: "fixture" })];

    expect(degraded.sourceModule).toBe("workflow");
  });
});
