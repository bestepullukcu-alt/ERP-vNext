const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * WP-WCN-TASK-REQUEST-01 FIX4 (item 7) — the reader's sentence depends on what they were doing:
 *   TASK_APPROVAL_CANCEL_FAILED / TASK_REVIEW_CANCEL_FAILED
 *     on an EDIT that switched the approval / review off         → "saved; save again to retry"   (the base map);
 *     on an ACT — delete, cancel, return, reassign, release      → "the linked approval or review could not be
 *                                                                   withdrawn; nothing was done"  (reasonOverridesFor);
 *   TASK_REVIEW_PENDING on submitReview (a review is still open)  → its own sentence, not the completion gate's.
 * Measured on the production map, bridge and resx, and on the Task Center's own dispatcher.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const ID = "22222222-2222-2222-2222-222222222222";
const resxValue = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
const WITHDRAW_CODES = ["TASK_APPROVAL_CANCEL_FAILED", "TASK_REVIEW_CANCEL_FAILED"];
const EDIT_KEY = { TASK_APPROVAL_CANCEL_FAILED: "errorApprovalCancelFailed", TASK_REVIEW_CANCEL_FAILED: "errorReviewCancelFailed" };

describe("the sentence follows the act", () => {
  beforeEach(() => {
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  it.each(WITHDRAW_CODES)("%s on an EDIT says the change is saved", (code) => {
    // The edit form passes no overrides (form-page.js).
    expect(global.TasksApi.failureMessage({ ok: false, status: 409, reasonCode: code })).toBe(`t:${EDIT_KEY[code]}`);
  });

  it.each(["delete", "cancel", "return", "reassign", "release"])("%s: both codes say nothing was done", (act) => {
    const overrides = global.TasksApi.reasonOverridesFor(act);
    for (const code of WITHDRAW_CODES) {
      expect(global.TasksApi.failureMessage({ ok: false, status: 409, reasonCode: code }, overrides))
        .toBe("t:errorWorkflowWithdrawFailedOnAct");
    }
  });

  it("submitReview's open-review refusal has its own sentence, and is not an act", () => {
    expect(global.TasksApi.reasonOverridesFor("submitReview")).toBeUndefined();
    expect(global.TasksApi.failureMessage({ ok: false, status: 409, reasonCode: "TASK_REVIEW_PENDING" }))
      .toBe("t:errorReviewAlreadyOpen");
    expect(global.TasksApi.failureMessage({ ok: false, status: 409, reasonCode: "REVIEW_PENDING" }))
      .not.toBe("t:errorReviewAlreadyOpen");
    for (const code of [...WITHDRAW_CODES, "TASK_REVIEW_PENDING"]) {
      expect(global.TasksApi.isTransitionBlocked({ status: 409, reasonCode: code }), code).toBe(true);
    }
  });

  it("translates every sentence ×7, through the bridge; the act sentence is never an edit sentence", () => {
    const bridge = fs.readFileSync(path.join(WEB, "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    const names = ["ErrorApprovalCancelFailed", "ErrorReviewCancelFailed", "ErrorWorkflowWithdrawFailedOnAct", "ErrorReviewAlreadyOpen"];
    for (const name of names) {
      expect(bridge).toContain(`${name} = Localizer["${name}"]`);
    }
    const english = (name) => resxValue(path.join(WEB, "Resources", "Views", "Tasks", "TasksIndex.en.resx"), name);
    for (const language of LANGUAGES) {
      const file = path.join(WEB, "Resources", "Views", "Tasks", `TasksIndex.${language}.resx`);
      const values = Object.fromEntries(names.map((name) => [name, resxValue(file, name)]));
      for (const name of names) {
        expect(values[name], `${name} missing in ${language}`).toBeTruthy();
        if (language !== "en") expect(values[name], `${name} in ${language} is still English`).not.toBe(english(name));
      }
      expect(values.ErrorWorkflowWithdrawFailedOnAct).not.toBe(values.ErrorApprovalCancelFailed);
      expect(values.ErrorWorkflowWithdrawFailedOnAct).not.toBe(values.ErrorReviewCancelFailed);
    }
  });
});

describe("the Task Center's dispatcher picks the sentence by the act", () => {
  const action = (code) => ({
    code, label: { kind: "resource", key: `WorkAggregation_Action_${code.charAt(0).toUpperCase()}${code.slice(1)}` },
    semanticType: code, enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
    requiresConfirmation: true, requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
  });
  const task = (code) => ({
    fixtureKind: "workItem", id: ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
    timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Bir iş", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: ID, deepLink: `/Tasks/${ID}` },
    assignee: { id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", isCurrentUser: true },
    requester: { id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", displayName: "Talep Eden" },
    lifecycleOwner: "tasks", workItemCapabilities: [], actions: [action(code)],
    concurrency: { kind: "version", token: "4" }, waitingContext: null, escalation: null, dueAt: null
  });

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

  const refuse = async (code, reasonCode) => {
    await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${ID}"`, items: [task(code)] });
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    global.WorkCenterNextApi.dispatchAction = () => Promise.resolve(
      { ok: false, status: 409, reasonCode, data: null, errors: [] });
    const button = app().querySelector(`[data-wcn-action="${code}"]`);
    const drawn = [...app().querySelectorAll("[data-wcn-action]")].map((el) => el.getAttribute("data-wcn-action"));
    expect(button, `no ${code} button drawn (drawn: ${drawn.join(", ")})`).toBeTruthy();
    button.click();
    await settle();
    if (confirms.length) { confirms[0].onConfirm(""); }
    for (let i = 0; i < 4; i++) { await settle(); }
    return toasts.filter((entry) => entry.type === "error").map((entry) => entry.message);
  };

  it("a cancel refused with TASK_APPROVAL_CANCEL_FAILED says nothing was done", async () => {
    const errors = await refuse("cancel", "TASK_APPROVAL_CANCEL_FAILED");
    expect(errors).toContain("t:errorWorkflowWithdrawFailedOnAct");
    expect(errors).not.toContain("t:errorApprovalCancelFailed");
  });

  it("a submitReview refused with TASK_REVIEW_PENDING says a review is already open", async () => {
    const errors = await refuse("submitReview", "TASK_REVIEW_PENDING");
    expect(errors).toContain("t:errorReviewAlreadyOpen");
  });
});
