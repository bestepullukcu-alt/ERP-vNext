const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * ══ WP-WCN-DELEGATE-LOGTIME-01 ═══════════════════════════════════════════════════════════════════════════════
 *
 *  A  BL-491  "Devret" on a MOD-0023 approval asks WHO (mandatory) and takes an optional note; the person goes out
 *             as `targetPrincipalId`. Which action asks for a person — and whom it must not offer — is the SERVER's
 *             statement on the action, never derived from the action code.
 *  B  BL-485  the fake "log time" dialog is gone: code, the seven languages' keys, the fixture's dead fields.
 *  C  BL-486  Start / Stop is drawn once on the detail page — by the time card, not again by the rail and its ···.
 *
 * Everything is real but the network seam, the people lookup and the two dialog globals (showConfirm / Swal), which
 * are the host page's, not this module's.
 */

const ID = "11111111-1111-1111-1111-111111111111";
const ME = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const STARTER = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const AYSE = "cccccccc-cccc-cccc-cccc-cccccccccccc";
const MEHMET = "dddddddd-dddd-dddd-dddd-dddddddddddd";

const webRoot = path.resolve(__dirname, "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const read = (relative) => fs.readFileSync(path.join(webRoot, relative), "utf8");
const APP = read("wwwroot/assets/js/WorkCenterNext/app.js");
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
const resx = (lang) => read(`Resources/Views/WorkCenterNext/WorkCenterNextIndex.${lang}.resx`);

const STRINGS = {
  ActionDisabledWithName: "{0}: {1}",
  ApprovalNoteLabel: "Not (isteğe bağlı)",
  ApprovalNotePlaceholder: "Kayda geçecek bir not ekleyin…",
  DelegateNoEligiblePeople: "Bu onayın devredilebileceği kimse yok.",
  DelegateTargetLabel: "Devredilecek kişi",
  DelegateTargetRequired: "Onayın kime devredileceğini seçin.",
  ReasonLabel: "Gerekçe",
  ToastActionApplied: "{0} uygulandı: {1}",
  WorkAggregation_Action_Delegate: "Devret"
};
const lookup = (key) => (key in STRINGS ? STRINGS[key] : key);
const translator = {
  t: lookup,
  tf: (key, ...args) => args.reduce((text, value, index) => text.split(`{${index}}`).join(String(value)), lookup(key)),
  tn: (key, args) => Object.keys(args || {}).reduce(
    (text, name) => text.split(`{${name}}`).join(String(args[name])), lookup(key))
};

const action = (actionCode, overrides) => Object.assign({
  code: actionCode,
  label: { kind: "resource", key: `WorkAggregation_Action_${actionCode.charAt(0).toUpperCase()}${actionCode.slice(1)}` },
  semanticType: actionCode,
  enabled: true,
  source: "provider",
  disabledReasonCode: null,
  disabledReason: null,
  requiresConfirmation: true,
  requiresReason: false,
  requiresEvidence: false,
  supportsBulk: false,
  riskLevel: "normal"
}, overrides);

// What the server sends for Devret (WorkItemProjectionService.BuildActionableActions).
const delegate = (overrides) => action("delegate", Object.assign({
  acceptsNote: true,
  requiresTargetPerson: true,
  excludedTargetPrincipalIds: [ME, STARTER]
}, overrides));

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
  title: { kind: "display", text: "İddia CLM-42 ödemesi", locale: "und" },
  nativeStatus: { code: "WaitingApproval", label: { kind: "resource", key: "WorkAggregation_NativeStatus_WaitingApproval" } },
  source: { providerCode: "workflow", providerContractVersion: "1.0", objectType: "claim", objectId: "CLM-42", deepLink: null },
  lifecycleOwner: "workflow",
  workItemCapabilities: [],
  actions: [delegate()],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

const person = (userId, displayName) => ({ userId, displayName, positionName: "Uzman" });
const EVERYBODY = [person(ME, "Ben"), person(STARTER, "Başlatan Kişi"), person(AYSE, "Ayşe Kaya"), person(MEHMET, "Mehmet Öz")];

const DETAIL = `data-wcn-page="detail" data-wcn-item-id="${ID}"`;

// ── doubles ───────────────────────────────────────────────────────────────────────────────────────────────
let confirms;
let fired;
let toasts;
let dispatched;
let peopleCalls;
let assignmentListCalls;

const installDialogs = () => {
  confirms = [];
  fired = [];
  toasts = [];
  global.showConfirm = (title, onConfirm, options) => { confirms.push({ title, onConfirm, options }); };
  global.Swal = {
    fire: (config) => { fired.push(config); return new Promise(() => { /* left open */ }); },
    showValidationMessage: (message) => { global.Swal.validation = message; }
  };
  global.showToast = (message, type) => { toasts.push({ message, type }); };
};

const stubDispatch = (answer) => {
  dispatched = [];
  global.WorkCenterNextApi.dispatchAction = (itemId, actionCode, providerCode, payload) => {
    dispatched.push({ itemId, actionCode, providerCode, payload });
    return Promise.resolve(answer || { ok: true, status: 200, reasonCode: null, data: null, errors: [] });
  };
};

/*
 * CT acceptance — TWO lists, as TasksApi has them: `decisionMakers` (who may DECIDE — not limited to the reader's
 * company) and `assignablePeople` (who may RECEIVE a task). Both answer the bare array (TasksApi opens the envelope,
 * nobody else does). The doubles answer DIFFERENT people, so a caller that reads the wrong list is seen.
 */
const stubPeople = (people, failure) => {
  peopleCalls = 0;
  assignmentListCalls = 0;
  global.TasksApi.decisionMakers = () => {
    peopleCalls += 1;
    return Promise.resolve(failure || { ok: true, status: 200, data: people });
  };
  global.TasksApi.assignablePeople = () => {
    assignmentListCalls += 1;
    return Promise.resolve({ ok: true, status: 200, data: [person(MEHMET, "Mehmet Öz")] });
  };
};

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

const press = async (actionCode) => {
  const button = app().querySelector(`[data-wcn-action="${actionCode}"]`);
  expect(button, `no "${actionCode}" button was drawn; every assertion below would be vacuous`).toBeTruthy();
  button.click();
  await settle();
  await settle();
};

const boot = async (item, people = EVERYBODY, rootAttrs = "") => {
  await bootSurface({ rootAttrs, items: [item], wcn: translator });
  stubDispatch();
  stubPeople(people);
};

// The window's own markup, put on the page the way SweetAlert would, so `preConfirm` reads real fields.
const mountWindow = () => {
  const host = document.createElement("div");
  host.id = "wcnTestWindow";
  host.innerHTML = fired[0].html;
  document.body.appendChild(host);
  return host;
};

const offeredIds = (host) => Array.from(host.querySelectorAll("#wcnReassignAssignee option"))
  .map((option) => option.value).filter(Boolean);

beforeEach(() => {
  installDialogs();
  global.sessionStorage.clear();
});

afterEach(() => {
  document.getElementById("wcnTestWindow")?.remove();
  delete global.showConfirm;
  delete global.Swal;
  delete global.showToast;
});

// ══ A — BL-491 ════════════════════════════════════════════════════════════════════════════════════════════
describe("A — Devret asks who, and sends the person", () => {
  it("opens a window with a person picker, not the plain confirm", async () => {
    await boot(approval());
    await press("delegate");

    expect(confirms, "Devret still opens the plain confirm — nobody is asked").toHaveLength(0);
    expect(fired).toHaveLength(1);
    const host = mountWindow();
    expect(host.querySelector("select#wcnReassignAssignee"), "the window has no person picker").toBeTruthy();
    expect(host.querySelector('label[for="wcnReassignAssignee"]').textContent).toBe("Devredilecek kişi");
  });

  it("cannot be confirmed without a person", async () => {
    await boot(approval());
    await press("delegate");
    mountWindow();

    expect(fired[0].preConfirm()).toBe(false);
    expect(global.Swal.validation).toBe("Onayın kime devredileceğini seçin.");
    expect(dispatched).toHaveLength(0);
  });

  it("can be confirmed once a person is chosen, with the note left empty", async () => {
    await boot(approval());
    await press("delegate");
    const host = mountWindow();
    host.querySelector("#wcnReassignAssignee").value = AYSE;

    expect(fired[0].preConfirm()).toEqual({ reason: "", assigneeUserId: AYSE });
  });

  it("sends the chosen person as targetPrincipalId and the note in the field the dispatcher reads", async () => {
    await boot(approval());
    const outcome = pressAndConfirm({ person: AYSE, note: "  İzindeyim, Ayşe Hanım bakacak.  " });
    await outcome;

    expect(dispatched).toHaveLength(1);
    expect(dispatched[0].actionCode).toBe("delegate");
    expect(dispatched[0].providerCode).toBe("workflow");
    expect(dispatched[0].payload.targetPrincipalId).toBe(AYSE);
    expect(dispatched[0].payload.note).toBe("İzindeyim, Ayşe Hanım bakacak.");
  });

  it.each([[""], ["   "]])("sends no note when the box is left empty (%j) — and still sends the person", async (typed) => {
    await boot(approval());
    await pressAndConfirm({ person: MEHMET, note: typed });

    expect(dispatched).toHaveLength(1);
    expect(dispatched[0].payload.targetPrincipalId).toBe(MEHMET);
    expect(dispatched[0].payload.note).toBeNull();
  });

  it("the note is optional: its box has the optional label and no validator stops an empty one", async () => {
    await boot(approval());
    await press("delegate");
    const host = mountWindow();

    expect(host.querySelector('label[for="wcnReasonText"]').textContent).toBe("Not (isteğe bağlı)");
    host.querySelector("#wcnReassignAssignee").value = AYSE;
    expect(fired[0].preConfirm()).not.toBe(false);
  });

  it("draws no note box when the server did not flag one", async () => {
    await boot(approval({ actions: [delegate({ acceptsNote: undefined })] }));
    await press("delegate");
    const host = mountWindow();

    expect(host.querySelector("#wcnReassignAssignee")).toBeTruthy();
    expect(host.querySelector("#wcnReasonText")).toBeNull();
  });

  it("never offers the reader or the person who started the workflow", async () => {
    await boot(approval());
    await press("delegate");

    expect(peopleCalls, "the decision-makers list was not read exactly once").toBe(1);
    expect(offeredIds(mountWindow())).toEqual([AYSE, MEHMET]);
  });

  // CT acceptance — approval authority belongs to the process, not to the reader's company (BL-057).
  it("offers the people who may DECIDE, never the task-assignment list", async () => {
    await boot(approval());
    await press("delegate");

    expect(assignmentListCalls, "the delegation read the company-scoped assignment list").toBe(0);
    // The assignment double offers Mehmet alone; Ayşe is on the page only if the decision list was read.
    expect(offeredIds(mountWindow())).toContain(AYSE);
  });

  // CT acceptance — a read that failed is not an empty list.
  it.each([
    [403, "errorNoAccess"],
    [0, "errorUnavailable"],
    [500, "errorOccurred"]
  ])("a people read that failed (%i) says what failed — not that there is nobody — and opens nothing", async (status, key) => {
    await bootSurface({ rootAttrs: "", items: [approval()], wcn: translator });
    stubDispatch();
    stubPeople([], { ok: false, status, reasonCode: null, data: null });
    // TasksApi's own rule for a failed call (Tasks/api.js failureMessage), so the sentence asserted is the product's.
    global.TasksApi.failureMessage = (result) =>
      (result?.status === 403 ? "errorNoAccess" : result?.status === 0 ? "errorUnavailable" : "errorOccurred");
    await press("delegate");

    expect(fired, "a window with an empty picker was opened over a failed read").toHaveLength(0);
    expect(dispatched).toHaveLength(0);
    expect(toasts).toHaveLength(1);
    expect(toasts[0].type).toBe("error");
    expect(toasts[0].message).not.toBe("Bu onayın devredilebileceği kimse yok.");
    expect(toasts[0].message).toBe(key);
  });

  it("matches the excluded ids whatever their letter case", async () => {
    await boot(approval({ actions: [delegate({ excludedTargetPrincipalIds: [ME.toUpperCase(), STARTER.toUpperCase()] })] }));
    await press("delegate");

    expect(offeredIds(mountWindow())).toEqual([AYSE, MEHMET]);
  });

  it("does not open when nobody is left to offer, and says so", async () => {
    await boot(approval(), [person(ME, "Ben"), person(STARTER, "Başlatan Kişi")]);
    await press("delegate");

    expect(fired, "a window that cannot be confirmed was opened").toHaveLength(0);
    expect(confirms).toHaveLength(0);
    expect(dispatched).toHaveLength(0);
    expect(toasts).toEqual([{ message: "Bu onayın devredilebileceği kimse yok.", type: "error" }]);
  });

  it("uses the SAME picker the reassign window uses — one select, one binder", () => {
    const source = code(APP);
    expect((source.match(/id="wcnReassignAssignee"/g) || []).length, "a second person picker was written").toBe(1);
    expect((source.match(/bindDialogSelect2\(document\.getElementById\('wcnReassignAssignee'\), popup\)/g) || []).length).toBe(1);
  });
});

// The whole path a reader takes: press, choose, type, confirm. `Swal.fire` resolves as a confirmed window would.
function pressAndConfirm({ person: chosen, note }) {
  return new Promise((resolve, reject) => {
    global.Swal.fire = (config) => {
      fired.push(config);
      const host = mountWindow();
      host.querySelector("#wcnReassignAssignee").value = chosen;
      const box = host.querySelector("#wcnReasonText");
      if (box) { box.value = note; }
      const value = config.preConfirm();
      return Promise.resolve(value === false ? { isConfirmed: false } : { isConfirmed: true, value });
    };
    press("delegate").then(settle).then(settle).then(resolve, reject);
  });
}

describe("A — the person window follows the server's flag, never the action code", () => {
  it("a delegate the server did not flag keeps the plain confirm and sends no person", async () => {
    await boot(approval({ actions: [action("delegate")] }));
    await press("delegate");

    expect(fired, "the picker was derived from the action code").toHaveLength(0);
    expect(confirms).toHaveLength(1);
    confirms[0].onConfirm("");
    await settle();
    expect(dispatched).toHaveLength(1);
    expect("targetPrincipalId" in dispatched[0].payload).toBe(false);
    expect(peopleCalls).toBe(0);
  });

  it("any action the server flags gets the picker, whatever it is called", async () => {
    const flagged = action("handOver", {
      requiresTargetPerson: true, label: { kind: "display", text: "Aktar", locale: "und" } });
    await boot(approval({ actions: [flagged] }));
    await press("handOver");

    expect(confirms).toHaveLength(0);
    expect(fired).toHaveLength(1);
    expect(offeredIds(mountWindow()), "no exclusions were sent, so nobody is dropped").toEqual([ME, STARTER, AYSE, MEHMET]);
  });

  it("an action that names nobody sends a body without targetPrincipalId", async () => {
    await boot(approval({ actions: [action("approve", { acceptsNote: true })] }));
    await press("approve");
    confirms[0].onConfirm("not");
    await settle();

    expect(dispatched).toHaveLength(1);
    expect(Object.keys(dispatched[0].payload).sort())
      .toEqual(["closureFieldValues", "expectedVersion", "note", "reasonCode"]);
  });
});

describe("A — after a delegation the item is no longer the reader's", () => {
  const FLASH = "wcn:flash-toast";
  const flashMessage = () => {
    const stored = global.sessionStorage.getItem(FLASH);
    return stored === null ? null : JSON.parse(stored).message;
  };

  it("on the detail page: back to the list with the success said there, never 'not found'", async () => {
    await boot(approval(), EVERYBODY, DETAIL);
    global.sessionStorage.setItem("wcn:list-return-url", "/WorkCenterNext?tab=inbox");
    global.WorkCenterNextApi.fetchWorkItems = () => Promise.resolve(
      { status: "ok", httpStatus: 200, items: [], errors: [], unavailableSources: [] });
    global.WorkCenterNextApi.fetchWorkItem = () => Promise.resolve({ status: "error", httpStatus: 404, item: null, errors: [] });

    await pressAndConfirm({ person: AYSE, note: "" });
    await settle();

    expect(dispatched).toHaveLength(1);
    expect(flashMessage()).toBe("Devret uygulandı: İddia CLM-42 ödemesi");
    expect(app().textContent).not.toContain("DetailItemNotFound");
    expect(toasts, "the success belongs to the list page, not to the page being left").toHaveLength(0);
  });

  it("a refused delegation stays on the page and hands nothing on", async () => {
    await boot(approval(), EVERYBODY, DETAIL);
    stubDispatch({ ok: false, status: 409, reasonCode: "WORKFLOW_ASSIGNMENT_MISMATCH", data: null, errors: [] });

    await pressAndConfirm({ person: AYSE, note: "" });

    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
    expect(toasts.some((entry) => entry.type === "error")).toBe(true);
  });
});

describe("A — the executable contract and the presentation mapper", () => {
  const codes = (item) => {
    delete global.WorkCenterNextContract;
    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
    return global.WorkCenterNextContract.validateWorkItem(item).errors.map((e) => e.code);
  };

  it("accepts the flag and the excluded ids", () => {
    expect(codes(approval())).toEqual([]);
  });

  it("refuses nothing older: an action without the new fields is still valid", () => {
    expect(codes(approval({ actions: [action("delegate")] }))).toEqual([]);
  });

  it("refuses a flag that is not a boolean, and ids that are not a list of ids", () => {
    expect(codes(approval({ actions: [delegate({ requiresTargetPerson: "yes", excludedTargetPrincipalIds: undefined })] })))
      .toContain("ACTION_TARGET_PERSON_INVALID");
    expect(codes(approval({ actions: [delegate({ excludedTargetPrincipalIds: ME })] })))
      .toContain("ACTION_EXCLUDED_TARGETS_INVALID");
    expect(codes(approval({ actions: [delegate({ excludedTargetPrincipalIds: [""] })] })))
      .toContain("ACTION_EXCLUDED_TARGETS_INVALID");
  });

  it("refuses excluded ids on an action that names nobody", () => {
    expect(codes(approval({ actions: [action("approve", { excludedTargetPrincipalIds: [ME] })] })))
      .toContain("ACTION_EXCLUDED_TARGETS_WITHOUT_TARGET");
  });

  it("the mapper carries both, and gives false and an empty list to an action without them", async () => {
    await bootSurface({ items: [], wcn: translator });
    const mapped = global.WorkCenterNextData.toPresentation(
      approval({ actions: [delegate(), action("approve")] }), { provenance: "api" });

    expect(mapped.actions[0].targetPerson).toBe(true);
    expect(mapped.actions[0].excludedTargetIds).toEqual([ME, STARTER]);
    expect(mapped.actions[1].targetPerson).toBe(false);
    expect(mapped.actions[1].excludedTargetIds).toEqual([]);
  });

  it("the window reads the mapped action, never the raw projection", () => {
    const source = code(APP);
    expect(source).not.toMatch(/requiresTargetPerson|excludedTargetPrincipalIds/);
  });
});

describe("A — the three new sentences exist in all seven tenant languages", () => {
  it.each(["DelegateTargetLabel", "DelegateTargetRequired", "DelegateNoEligiblePeople"])("%s", (key) => {
    expect(code(APP), `${key} is not used`).toContain(`'${key}'`);
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      const at = xml.indexOf(`name="${key}"`);
      expect(at, `${lang} is missing ${key}`).toBeGreaterThan(-1);
      expect(xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at)).trim(), `${lang}/${key} is empty`)
        .not.toBe("");
    });
  });

  it("no language repeats the English sentence (a copy is not a translation)", () => {
    ["DelegateTargetRequired", "DelegateNoEligiblePeople"].forEach((key) => {
      const value = (lang) => {
        const xml = resx(lang);
        const at = xml.indexOf(`name="${key}"`);
        return xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at)).trim();
      };
      LANGS.filter((lang) => lang !== "en").forEach((lang) =>
        expect(value(lang), `${lang}/${key} is the English text`).not.toBe(value("en")));
    });
  });
});

// ══ B — BL-485 ════════════════════════════════════════════════════════════════════════════════════════════
describe("B — the fake 'log time' dialog is gone", () => {
  const REMOVED_KEYS = ["ActLogTime", "LogTimeLabel", "LogTimePlaceholder", "LogTimeConfirm", "LogTimeSubtext", "ToastTimeLogged"];

  it("app.js has no log-time dialog, no dispatch to it and no button for it", () => {
    const source = code(APP);
    expect(source).not.toMatch(/openLogTime|logTime|wcn-ts-log|wcn-time-input/);
    expect(source).not.toContain("action.input === 'minutes'");
  });

  it("an action that asks for minutes opens no dialog and writes no fake activity", async () => {
    const item = approval({ actions: [action("logTime", {
      requiresConfirmation: false, input: "minutes", label: { kind: "display", text: "Süre gir", locale: "und" } })] });
    await boot(item);
    await press("logTime");

    expect(confirms, "the minutes dialog came back").toHaveLength(0);
    expect(fired).toHaveLength(0);
    expect(toasts.some((entry) => /mock/i.test(String(entry.message))), "a '(mock)' success was shown").toBe(false);
    // What is left is the one honest path: the action goes to its provider, which decides.
    expect(dispatched).toHaveLength(1);
  });

  it.each(REMOVED_KEYS)("%s is in none of the seven languages", (key) => {
    LANGS.forEach((lang) => expect(resx(lang), `${lang} still carries ${key}`).not.toContain(`name="${key}"`));
  });

  it("the showcase fixture carries no dead total and no log-time action", () => {
    const fixtures = code(read("wwwroot/assets/js/WorkCenterNext/fixtures/islerim-showcase-fixtures.js"));
    expect(fixtures).not.toMatch(/loggedMinutes|logTime/);
  });

  it("the clock painted on that dialog's box left the stylesheet with it", () => {
    expect(read("wwwroot/assets/css/backbone-custom.css")).not.toContain("wcn-time-input");
  });
});

// ══ C — BL-486 (1) ════════════════════════════════════════════════════════════════════════════════════════
describe("C — Start / Stop is drawn once on the detail page", () => {
  const TASK_ID = "5b1b4a2e-0c3d-4e5f-8a9b-0c1d2e3f4a5b";
  const plain = (actionCode) => action(actionCode, {
    requiresConfirmation: false, label: { kind: "display", text: actionCode, locale: "und" } });
  const task = (overrides) => Object.assign({
    fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
    timerState: "inactive", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Batch record review", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "display", text: "Devam ediyor", locale: "und" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
    assignee: { id: ME, isCurrentUser: true },
    requester: { id: STARTER, isCurrentUser: false },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution", "timeTracking"],
    timeEntries: { draftMinutes: 30, submittedMinutes: 60, approvedMinutes: 125 },
    actions: [plain("complete"), plain("pause"), plain("startTimer")],
    primaryActionCode: "complete",
    overflowActionCodes: ["pause"],
    concurrency: { kind: "version", token: "3" },
    waitingContext: null, escalation: null, dueAt: "2026-10-30T00:00:00+00:00"
  }, overrides);
  const TASK_DETAIL = `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`;
  const timerButtons = (actionCode) => Array.from(app().querySelectorAll(`[data-wcn-action="${actionCode}"]`));

  it.each([["startTimer"], ["stopTimer"]])("%s is on the time card and nowhere else", async (timerCode) => {
    const running = timerCode === "stopTimer";
    await bootSurface({
      rootAttrs: TASK_DETAIL,
      items: [task({ timerState: running ? "running" : "inactive", actions: [plain("complete"), plain("pause"), plain(timerCode)] })],
      wcn: translator
    });
    await settle();

    const buttons = timerButtons(timerCode);
    expect(buttons, "the timer is drawn more than once (card + rail / ··· menu)").toHaveLength(1);
    expect(buttons[0].closest(".wcn-ts-actions"), "the one button is not the time card's").toBeTruthy();
    expect(app().querySelector(`.wcn-actionbar-more [data-wcn-action="${timerCode}"]`)).toBeNull();
  });

  // CT acceptance — the rail printed why a dimmed action is dimmed; the card that took the timer over owes the same.
  it("a timer the reader may not use says why, on the card", async () => {
    const refused = action("startTimer", {
      requiresConfirmation: false, label: { kind: "display", text: "Sayacı başlat", locale: "und" },
      enabled: false, disabledReasonCode: "PermissionDenied",
      disabledReason: { kind: "display", text: "Zaman çizelgesi yetkiniz yok.", locale: "und" }
    });
    await bootSurface({
      rootAttrs: TASK_DETAIL,
      items: [task({ actions: [plain("complete"), plain("pause"), refused] })],
      wcn: translator
    });
    await settle();

    const buttons = timerButtons("startTimer");
    expect(buttons).toHaveLength(1);
    expect(buttons[0].disabled).toBe(true);
    const card = buttons[0].closest(".wcn-detail-section");
    expect(card.textContent, "the card dims the timer and says nothing").toContain("Zaman çizelgesi yetkiniz yok.");
    const described = buttons[0].getAttribute("aria-describedby");
    expect(described, "the dimmed button points at no reason").toBeTruthy();
    expect(card.querySelector(`#${described}`), "aria-describedby points at an element that was never drawn").toBeTruthy();
  });

  it("an enabled timer draws no reason line and points at none", async () => {
    await bootSurface({ rootAttrs: TASK_DETAIL, items: [task()], wcn: translator });
    await settle();

    const [button] = timerButtons("startTimer");
    expect(button.hasAttribute("aria-describedby")).toBe(false);
    expect(button.closest(".wcn-detail-section").querySelector(".wcn-act-reason")).toBeNull();
  });

  it("the other actions are still on the rail — only the repeat went", async () => {
    await bootSurface({ rootAttrs: TASK_DETAIL, items: [task()], wcn: translator });
    await settle();

    expect(timerButtons("complete").length).toBeGreaterThan(0);
    expect(timerButtons("pause").length).toBeGreaterThan(0);
  });

  it("where there is no time card the timer action keeps its place", async () => {
    // Time is not tracked → no card → the rail is the action's only home, and it stays there.
    await bootSurface({
      rootAttrs: TASK_DETAIL,
      items: [task({ workItemCapabilities: ["planning", "execution"], timeEntries: undefined })],
      wcn: translator
    });
    await settle();

    expect(app().querySelector(".wcn-timesheet")).toBeNull();
    expect(timerButtons("startTimer").length).toBeGreaterThan(0);
  });
});
