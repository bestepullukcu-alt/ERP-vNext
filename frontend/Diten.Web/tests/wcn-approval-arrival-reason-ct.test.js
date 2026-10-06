const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * CT guards (BL-437 acceptance, 2026-09-24). Two sabotages left the agent's 8 row tests green: the arrival
 * sentence rendered UNESCAPED (a requester named "<b>x</b>" would inject markup into every approver's inbox), and
 * the Turkish sentence losing its {name} placeholder (the key guard only checks presence, not the argument).
 */
const ID = "11111111-1111-1111-1111-111111111111";
const TASK_ID = "22222222-2222-2222-2222-222222222222";
const STRINGS = {
  WorkAggregation_ArrivalReason_SentForApproval: "{name} bu görevi onayına gönderdi",
  WorkAggregation_ArrivalReason_SentForApprovalUnnamed: "Bu görev onayına gönderildi",
  DetailOpenSource: "Kaynak kaydını aç"
};
const translator = {
  t: (key) => (key in STRINGS ? STRINGS[key] : key),
  tf: (key) => (key in STRINGS ? STRINGS[key] : key),
  tn: (key, args) => Object.keys(args || {}).reduce((text, name) => text.split(`{${name}}`).join(String(args[name])), key in STRINGS ? STRINGS[key] : key)
};
const approval = (name) => ({
  fixtureKind: "workItem", id: ID, workIntent: "approval", assignmentMode: "approval", ownershipState: "notApplicable",
  admissionState: "notApplicable", normalizedStatus: "Pending", taskLifecycle: "notApplicable", executionState: "notApplicable",
  timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Q3 bütçe revizyonunu hazırla", locale: "und" },
  nativeStatus: { code: "WaitingApproval", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
  source: { providerCode: "workflow", providerContractVersion: "1.0", objectType: "task-review", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
  requester: { id: "33333333-3333-3333-3333-333333333333", displayName: name, isCurrentUser: false },
  arrivalReason: { kind: "resource", key: "WorkAggregation_ArrivalReason_SentForApproval", args: { name } },
  lifecycleOwner: "workflow", workItemCapabilities: [],
  actions: [{ code: "approve", label: { kind: "resource", key: "WorkAggregation_Action_Approve" }, semanticType: "approve", enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null, requiresConfirmation: true, requiresReason: false, requiresEvidence: false, supportsBulk: true, riskLevel: "normal" }],
  concurrency: { kind: "version", token: "3" }, waitingContext: null, escalation: null, dueAt: null
});

describe("the arrival sentence is text, never markup (CT)", () => {
  it("a requester whose name carries markup is shown as text — nothing is injected into the row", async () => {
    await bootSurface({ items: [approval("<b>Ayşe</b><img src=x onerror=alert(1)>")], wcn: translator });
    const row = app().querySelector(`[data-wcn-row="${ID}"]`);
    const summary = row.querySelector(".wcn-row-summary");
    expect(summary.querySelector("b, img"), "the name was rendered as HTML").toBeNull();
    expect(summary.textContent).toContain("<b>Ayşe</b>");
    expect(summary.innerHTML).toContain("&lt;b&gt;");
  });
});

describe("the sentence keeps its argument in every language (CT)", () => {
  const dir = path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext");
  const value = (locale, key) => {
    const xml = fs.readFileSync(path.join(dir, `WorkCenterNextIndex.${locale}.resx`), "utf8");
    const m = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]*)</value>`).exec(xml);
    return m ? m[1] : null;
  };
  for (const locale of ["en", "tr", "fr", "es", "zh", "ar", "ru"]) {
    it(`${locale}: the named sentence carries {name}; the nameless one carries no placeholder`, () => {
      expect(value(locale, "WorkAggregation_ArrivalReason_SentForApproval"), `${locale} lost its {name}`).toContain("{name}");
      const unnamed = value(locale, "WorkAggregation_ArrivalReason_SentForApprovalUnnamed");
      expect(unnamed, `${locale} nameless sentence missing`).toBeTruthy();
      expect(unnamed).not.toMatch(/\{\w+\}/);
    });
  }
});
