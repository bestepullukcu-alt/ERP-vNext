const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ ABB FIX3 item 12 — "ANOTHER APPROVER DECIDED THIS FIRST", SAID ON THE PAGE WHERE THE RACE HAPPENS ════════════
 *
 * Two approvers on one ABB item send the SAME idempotency key (the key carries no person), so the second one is
 * refused by MDM with ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER — and that refusal lands HERE, in the Task Center, not
 * on the register page that already had its sentence. Before, it read as the generic failure text.
 *
 * Everything is real but the network seam and the host page's dialog / toast globals.
 */

const ID = "6b0f5d43-2c1e-4d8a-9a57-0c2f6f1e7a10";
const SENTENCE = "Başka bir onaycı bunu zaten karara bağladı.";
const STRINGS = {
  ErrorAbbreviationDecidedByAnother: SENTENCE,
  WorkAggregation_Action_Approve: "Onayla"
};
const lookup = (key) => (key in STRINGS ? STRINGS[key] : key);
const translator = {
  t: lookup,
  tf: (key, ...args) => args.reduce((text, value, index) => text.split(`{${index}}`).join(String(value)), lookup(key)),
  tn: (key, args) => Object.keys(args || {}).reduce(
    (text, name) => text.split(`{${name}}`).join(String(args[name])), lookup(key))
};

const abbApproval = () => ({
  fixtureKind: "workItem",
  id: ID,
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
  title: { kind: "display", text: "ABC · GP-000000000001", locale: "und" },
  nativeStatus: { code: "REQUESTED", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
  source: {
    providerCode: "mdm-product-abbreviations", providerContractVersion: "1.0",
    objectType: "productAbbreviationAllocationRequest", objectId: ID, deepLink: null
  },
  lifecycleOwner: "mdm-product-abbreviations",
  workItemCapabilities: [],
  actions: [{
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
    supportsBulk: false,
    riskLevel: "normal"
  }],
  concurrency: { kind: "version", token: "0" },
  waitingContext: null,
  escalation: null,
  dueAt: null
});

let confirms;
let toasts;
let reads;

beforeEach(() => {
  confirms = [];
  toasts = [];
  global.showConfirm = (title, onConfirm, options) => { confirms.push({ title, onConfirm, options }); };
  global.Swal = { fire: () => new Promise(() => { /* left open */ }), showValidationMessage: () => {} };
  global.showToast = (message, type) => { toasts.push({ message, type }); };
});

afterEach(() => {
  delete global.showConfirm;
  delete global.Swal;
  delete global.showToast;
});

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

const decideAndLose = async (reasonCode) => {
  await bootSurface({ items: [abbApproval()], wcn: translator });
  reads = 0;
  global.WorkCenterNextApi.dispatchAction = () =>
    Promise.resolve({ ok: false, status: 409, reasonCode, data: null, errors: ["refused"] });
  global.WorkCenterNextApi.fetchWorkItems = () => {
    reads++;
    return Promise.resolve({ status: "ok", httpStatus: 200, items: [], errors: [], unavailableSources: [] });
  };
  const button = app().querySelector('[data-wcn-action="approve"]');
  expect(button, "no approve button was drawn; every assertion below would be vacuous").toBeTruthy();
  button.click();
  await settle();
  confirms[0].onConfirm("");
  await settle();
  await settle();
};

describe("ABB FIX3 item 12 — an ABB decision lost to another approver", () => {
  it("says that another approver decided it, as an error, and re-reads the board", async () => {
    await decideAndLose("ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER");

    expect(toasts).toEqual([{ message: SENTENCE, type: "error" }]);
    expect(reads, "the item has left the board; the list must be read again").toBeGreaterThan(0);
  });

  it("keeps every other refusal on the path it had", async () => {
    await decideAndLose("ABBREVIATION_NOT_PENDING");

    expect(toasts.map((entry) => entry.message)).not.toContain(SENTENCE);
    expect(toasts.some((entry) => entry.type === "error")).toBe(true);
  });

  it("does not take a code named like a built-in object property for one of its own", async () => {
    // FIX4 item 7 — the lookup is an own-key lookup: "constructor" is not a provider sentence.
    await decideAndLose("constructor");

    expect(toasts.length).toBeGreaterThan(0);
    expect(toasts.every((entry) => typeof entry.message === "string")).toBe(true);
    expect(toasts.map((entry) => entry.message)).not.toContain(SENTENCE);
  });

  it("has the sentence in all seven Task Center languages", () => {
    for (const language of ["en", "tr", "fr", "es", "zh", "ar", "ru"]) {
      const resx = fs.readFileSync(path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext",
        `WorkCenterNextIndex.${language}.resx`), "utf8");
      const match = resx.match(/<data name="ErrorAbbreviationDecidedByAnother"[^>]*>\s*<value>([^<]+)<\/value>/);
      expect(match, `${language} has no ErrorAbbreviationDecidedByAnother`).toBeTruthy();
      expect(match[1].trim().length).toBeGreaterThan(10);
    }
  });
});
