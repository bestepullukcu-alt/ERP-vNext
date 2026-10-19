const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * WP-WCN-TASK-REQUEST-01 FIX4 (item 6) — "nobody eligible" is two refusals with two sentences:
 *   TASK_REVIEW_NO_ELIGIBLE_REVIEWER  → submitting for review: "choose another reviewer";
 *   WORKFLOW_NO_ELIGIBLE_APPROVER     → the engine's own code, also answered to an APPROVER whose decision would hand the
 *                                       next step to the starter alone: a general sentence, never "choose a reviewer".
 * Measured on the production map, bridge and resx, and on the Task Center's own approve path.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const ID = "11111111-1111-1111-1111-111111111111";
const resxValue = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("two codes, two sentences", () => {
  beforeEach(() => {
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  it("maps each code to its own key, both blocking", () => {
    const keys = global.TasksApi.REASON_CODE_MESSAGE_KEYS;
    expect(keys.TASK_REVIEW_NO_ELIGIBLE_REVIEWER).toBe("errorNoEligibleReviewer");
    expect(keys.WORKFLOW_NO_ELIGIBLE_APPROVER).toBe("errorNoEligibleApprover");
    for (const code of ["TASK_REVIEW_NO_ELIGIBLE_REVIEWER", "WORKFLOW_NO_ELIGIBLE_APPROVER"]) {
      expect(global.TasksApi.isTransitionBlocked({ status: 409, reasonCode: code }), code).toBe(true);
    }
  });

  it("translates both ×7, through the bridge, and the two sentences differ in every language", () => {
    const bridge = fs.readFileSync(path.join(WEB, "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    for (const name of ["ErrorNoEligibleReviewer", "ErrorNoEligibleApprover"]) {
      expect(bridge).toContain(`${name} = Localizer["${name}"]`);
    }
    for (const language of LANGUAGES) {
      const file = path.join(WEB, "Resources", "Views", "Tasks", `TasksIndex.${language}.resx`);
      const reviewer = resxValue(file, "ErrorNoEligibleReviewer");
      const approver = resxValue(file, "ErrorNoEligibleApprover");
      expect(reviewer, `ErrorNoEligibleReviewer missing in ${language}`).toBeTruthy();
      expect(approver, `ErrorNoEligibleApprover missing in ${language}`).toBeTruthy();
      expect(approver, `the two sentences are the same in ${language}`).not.toBe(reviewer);
    }
    const english = (key) => resxValue(path.join(WEB, "Resources", "Views", "Tasks", "TasksIndex.en.resx"), key);
    for (const language of LANGUAGES.filter((l) => l !== "en")) {
      const file = path.join(WEB, "Resources", "Views", "Tasks", `TasksIndex.${language}.resx`);
      expect(resxValue(file, "ErrorNoEligibleApprover")).not.toBe(english("ErrorNoEligibleApprover"));
      expect(resxValue(file, "ErrorNoEligibleReviewer")).not.toBe(english("ErrorNoEligibleReviewer"));
    }
  });
});

describe("the Task Center's approve shows the general sentence", () => {
  const approval = {
    fixtureKind: "workItem", id: ID, workIntent: "approval", assignmentMode: "approval",
    ownershipState: "notApplicable", admissionState: "notApplicable", normalizedStatus: "Pending",
    taskLifecycle: "notApplicable", executionState: "notApplicable", timerState: "notApplicable",
    systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "İddia CLM-42 ödemesi", locale: "und" },
    nativeStatus: { code: "WaitingApproval", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
    source: { providerCode: "workflow", providerContractVersion: "1.0", objectType: "claim", objectId: "CLM-42", deepLink: null },
    lifecycleOwner: "workflow", workItemCapabilities: [],
    actions: [{
      code: "approve", label: { kind: "resource", key: "WorkAggregation_Action_Approve" }, semanticType: "approve",
      enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null, requiresConfirmation: true,
      requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
    }],
    concurrency: { kind: "version", token: "3" }, waitingContext: null, escalation: null, dueAt: null
  };

  let toasts;
  let confirms;
  beforeEach(() => {
    toasts = [];
    confirms = [];
    global.showToast = (message, type) => { toasts.push({ message, type }); };
    global.showConfirm = (title, onConfirm, options) => { confirms.push({ title, onConfirm, options }); };
    global.Swal = { fire: () => new Promise(() => {}), showValidationMessage: () => {} };
  });

  afterEach(() => {
    delete global.showToast;
    delete global.showConfirm;
    delete global.Swal;
  });

  it("a decision refused with WORKFLOW_NO_ELIGIBLE_APPROVER says the general sentence, not 'choose a reviewer'", async () => {
    await bootSurface({ items: [approval] });
    // The production client from here on: the boot harness stubs it, the refusal path is what is measured.
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    global.WorkCenterNextApi.dispatchAction = () => Promise.resolve(
      { ok: false, status: 409, reasonCode: "WORKFLOW_NO_ELIGIBLE_APPROVER", data: null, errors: [] });

    const button = app().querySelector('[data-wcn-action="approve"]');
    expect(button, "no approve button drawn").toBeTruthy();
    button.click();
    await settle();
    expect(confirms, "no confirmation was asked").toHaveLength(1);
    confirms[0].onConfirm("");
    for (let i = 0; i < 4; i++) { await settle(); }

    const errors = toasts.filter((entry) => entry.type === "error").map((entry) => entry.message);
    expect(errors).toContain("t:errorNoEligibleApprover");
    expect(errors).not.toContain("t:errorNoEligibleReviewer");
  });
});
