const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ BL-437 — "IT COMES BACK TO ME AND I CANNOT TELL WHY" ═════════════════════
 *
 * The owner assigned a task to someone; when the work came back to them for approval, the Inbox row read
 * "Onay: task-review 3f2c…" — a type code and a GUID. The projection now sends the task's own title, who sent
 * it (`requester`), the task's address (`source.deepLink`) and one translated sentence saying why it arrived
 * (`arrivalReason`). This file holds the ROW to its half: the sentence is shown, the link is a real link, and
 * a row the server says nothing about renders exactly as before.
 *
 * The translator below substitutes named args for real (the default harness echoes keys back), because the
 * NAME inside the sentence is the thing under test — a key alone cannot tell "Ayşe sent this" from "sent".
 */

const ID = "11111111-1111-1111-1111-111111111111";
const TASK_ID = "22222222-2222-2222-2222-222222222222";
const HOLDER = "33333333-3333-3333-3333-333333333333";

const STRINGS = {
  WorkAggregation_ArrivalReason_SentForApproval: "{name} bu görevi onayına gönderdi",
  WorkAggregation_ArrivalReason_SentForApprovalUnnamed: "Bu görev onayına gönderildi",
  DetailOpenSource: "Kaynak kaydını aç"
};
const translator = {
  t: (key) => (key in STRINGS ? STRINGS[key] : key),
  tf: (key) => (key in STRINGS ? STRINGS[key] : key),
  tn: (key, args) => Object.keys(args || {}).reduce(
    (text, name) => text.split(`{${name}}`).join(String(args[name])),
    key in STRINGS ? STRINGS[key] : key)
};

const approval = (overrides) => Object.assign({
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
  title: { kind: "display", text: "Q3 bütçe revizyonunu hazırla", locale: "und" },
  nativeStatus: { code: "WaitingApproval", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
  source: {
    providerCode: "workflow", providerContractVersion: "1.0",
    objectType: "task-review", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}`
  },
  requester: { id: HOLDER, displayName: "Ayşe Kaya", isCurrentUser: false },
  arrivalReason: {
    kind: "resource", key: "WorkAggregation_ArrivalReason_SentForApproval", args: { name: "Ayşe Kaya" }
  },
  lifecycleOwner: "workflow",
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
    supportsBulk: true,
    riskLevel: "normal"
  }],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

// The Inbox is the surface an approval lands on, and it is the tab the page opens on.
const inboxRow = async (item) => {
  await bootSurface({ items: [item], wcn: translator });
  const row = app().querySelector(`[data-wcn-row="${ID}"]`);
  expect(row, "the approval row was not painted; every assertion below would be vacuous").toBeTruthy();
  return row;
};

describe("an approval row says why it is here and where it comes from (BL-437)", () => {
  it("shows the task's own title, never the object type or its id", async () => {
    const row = await inboxRow(approval());
    const title = row.querySelector(".wcn-row-title").textContent;
    expect(title).toBe("Q3 bütçe revizyonunu hazırla");
    expect(row.textContent).not.toContain("task-review");
    expect(title).not.toContain(TASK_ID);
  });

  it("names who sent it, in one translated sentence", async () => {
    const row = await inboxRow(approval());
    const line = row.querySelector(".wcn-row-summary");
    expect(line, "the row has no line for the reason").toBeTruthy();
    expect(line.textContent).toContain("Ayşe Kaya bu görevi onayına gönderdi");
  });

  it("links to the task itself, as a real link the row's click handler leaves alone", async () => {
    const row = await inboxRow(approval());
    const link = row.querySelector("[data-wcn-arrival-link]");
    expect(link, "no link to the task").toBeTruthy();
    expect(link.tagName, "a navigation control must be an anchor").toBe("A");
    expect(link.getAttribute("href")).toBe(`/Tasks/${TASK_ID}`);
    expect(link.textContent).toBe("Kaynak kaydını aç");
  });

  it("uses the whole nameless sentence when the server could not resolve a name", async () => {
    const row = await inboxRow(approval({
      requester: { id: HOLDER, isCurrentUser: false },
      arrivalReason: { kind: "resource", key: "WorkAggregation_ArrivalReason_SentForApprovalUnnamed" }
    }));
    const text = row.querySelector(".wcn-row-summary").textContent;
    expect(text).toContain("Bu görev onayına gönderildi");
    expect(text, "a user id leaked into the sentence").not.toContain(HOLDER);
  });

  it("draws no link when the server gave no address", async () => {
    const row = await inboxRow(approval({
      source: {
        providerCode: "workflow", providerContractVersion: "1.0",
        objectType: "task-review", objectId: TASK_ID, deepLink: null
      }
    }));
    expect(row.querySelector("[data-wcn-arrival-link]")).toBeNull();
    expect(row.querySelector(".wcn-row-summary").textContent).toContain("Ayşe Kaya bu görevi onayına gönderdi");
  });

  it("leaves a row the server says nothing about exactly as it was", async () => {
    const plain = approval({ arrivalReason: undefined, requester: undefined });
    delete plain.arrivalReason;
    delete plain.requester;
    const row = await inboxRow(plain);
    expect(row.querySelector("[data-wcn-arrival-link]")).toBeNull();
    expect(row.querySelector(".wcn-row-summary").textContent).not.toContain("onayına");
  });
});

describe("the executable contract accepts the reason only as a real label", () => {
  const { loadScript } = require("./load-script");
  beforeEach(() => {
    delete global.WorkCenterNextContract;
    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
  });

  const codes = (item) => global.WorkCenterNextContract.validateWorkItem(item).errors.map((e) => e.code);

  it("accepts a resource-label reason and an absent one", () => {
    expect(codes(approval())).not.toContain("ARRIVAL_REASON_INVALID");
    const without = approval();
    delete without.arrivalReason;
    expect(codes(without)).not.toContain("ARRIVAL_REASON_INVALID");
  });

  it("refuses a malformed reason instead of rendering a raw key or nothing", () => {
    expect(codes(approval({ arrivalReason: { kind: "resource", key: "" } }))).toContain("ARRIVAL_REASON_INVALID");
    expect(codes(approval({ arrivalReason: "Ayşe sent it" }))).toContain("ARRIVAL_REASON_INVALID");
  });
});
