const { bootSurface, app } = require("./wcn-boot");
const { loadScript } = require("./load-script");

/*
 * ══ REQ-WCN-01 (WP-WCN-APPROVAL-UX-01) — FOUR THINGS AN APPROVAL COULD NOT SAY ═══════════════════════════════
 *
 *  W-1  which STEP of the approval this is            → a badge on the row and on the detail page, never the title
 *  W-2  "I approve, and here is a note"               → an OPTIONAL note in the confirm, offered only when the
 *                                                        server flags the action; a REQUIRED reason is untouched
 *  W-3  who it waits on when no person is named       → "Onay bekleyen: {position names}" instead of "unassigned"
 *  W-4  what happens after the decision               → back to the list with the success said there, instead of
 *                                                        "not found" on a page whose item was just decided
 *
 * Everything is real but the network seam and the two dialog globals (showConfirm / Swal), which are the host
 * page's, not this module's.
 */

const ID = "11111111-1111-1111-1111-111111111111";
const TASK_ID = "22222222-2222-2222-2222-222222222222";

const STRINGS = {
  ApprovalAwaitingPositions: "Onay bekleyen: {0}",
  ApprovalNoteLabel: "Not (isteğe bağlı)",
  ApprovalNotePlaceholder: "Kayda geçecek bir not ekleyin…",
  ApprovalStepBadgeTitle: "Onay adımı",
  SummaryUnassigned: "Atanmamış",
  ToastActionApplied: "{0} uygulandı: {1}",
  WorkAggregation_Action_Approve: "Onayla",
  WorkAggregation_Action_Delegate: "Devret"
};
const lookup = (key) => (key in STRINGS ? STRINGS[key] : key);
const translator = {
  t: lookup,
  tf: (key, ...args) => args.reduce((text, value, index) => text.split(`{${index}}`).join(String(value)), lookup(key)),
  tn: (key, args) => Object.keys(args || {}).reduce(
    (text, name) => text.split(`{${name}}`).join(String(args[name])), lookup(key))
};

const action = (code, overrides) => Object.assign({
  code,
  label: { kind: "resource", key: `WorkAggregation_Action_${code.charAt(0).toUpperCase()}${code.slice(1)}` },
  semanticType: code,
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
  actions: [action("approve", { acceptsNote: true })],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: null
}, overrides);

const position = (text) => ({ kind: "display", text, locale: "und" });

const DETAIL = `data-wcn-page="detail" data-wcn-item-id="${ID}"`;

// ── dialog doubles ────────────────────────────────────────────────────────────────────────────────────────
let confirms;   // every window.showConfirm call: { title, onConfirm, options }
let fired;      // every raw Swal.fire config
let toasts;
let dispatched; // every dispatchAction call

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

const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

const press = async (code) => {
  const button = app().querySelector(`[data-wcn-action="${code}"]`);
  expect(button, `no "${code}" button was drawn; every assertion below would be vacuous`).toBeTruthy();
  button.click();
  await settle();
};

beforeEach(() => {
  installDialogs();
  global.sessionStorage.clear();
});

afterEach(() => {
  delete global.showConfirm;
  delete global.Swal;
  delete global.showToast;
});

// ══ W-1 ═══════════════════════════════════════════════════════════════════════════════════════════════════
describe("W-1 — the approval step is a badge, on the row and on the detail page", () => {
  it("draws the step name as a badge beside the row title", async () => {
    await bootSurface({ items: [approval({ stepName: position("Finans Onayı") })], wcn: translator });
    const row = app().querySelector(`[data-wcn-row="${ID}"]`);
    const badge = row.querySelector("[data-wcn-step-badge]");
    expect(badge, "the row has no step badge").toBeTruthy();
    expect(badge.textContent).toBe("Finans Onayı");
    expect(badge.classList.contains("wcn-badge")).toBe(true);
  });

  it("never adds the step name to the title", async () => {
    await bootSurface({ items: [approval({ stepName: position("Finans Onayı") })], wcn: translator });
    const row = app().querySelector(`[data-wcn-row="${ID}"]`);
    expect(row.querySelector(".wcn-row-title").textContent).toBe("İddia CLM-42 ödemesi");
  });

  it("draws the same badge on the detail page", async () => {
    await bootSurface({ rootAttrs: DETAIL, items: [approval({ stepName: position("Finans Onayı") })], wcn: translator });
    const badge = app().querySelector("[data-wcn-step-badge]");
    expect(badge, "the detail page has no step badge").toBeTruthy();
    expect(badge.textContent).toBe("Finans Onayı");
    // Beside the heading, never inside it: the heading is still the task's name alone.
    expect(app().querySelector(".wcn-details-page h5").textContent).toBe("İddia CLM-42 ödemesi");
  });

  it("draws no badge for an item whose provider names no step", async () => {
    await bootSurface({ items: [approval()], wcn: translator });
    expect(app().querySelector(`[data-wcn-row="${ID}"]`)).toBeTruthy();
    expect(app().querySelector("[data-wcn-step-badge]")).toBeNull();
  });

  /*
   * CT acceptance (2026-10-01). A step name is typed by whoever designs the tenant's workflow template, and a position
   * name by whoever maintains the organisation — both are other people's text on this reader's screen. They are drawn
   * as TEXT on the row, on the detail heading and in the "waiting for" line; markup in a name is shown, never run.
   */
  it("draws a step name that contains markup as text, on the row and on the detail page", async () => {
    const hostile = '<img src=x onerror="window.__wcnStepXss=1">Onay';
    await bootSurface({ items: [approval({ stepName: position(hostile) })], wcn: translator });
    const rowBadge = app().querySelector(`[data-wcn-row="${ID}"] [data-wcn-step-badge]`);
    expect(rowBadge.textContent).toBe(hostile);
    expect(rowBadge.querySelector("img")).toBeNull();

    await bootSurface({ rootAttrs: DETAIL, items: [approval({ stepName: position(hostile) })], wcn: translator });
    const detailBadge = app().querySelector("[data-wcn-step-badge]");
    expect(detailBadge.textContent).toBe(hostile);
    expect(app().querySelector("img[src='x']")).toBeNull();
  });

  it("draws a position name that contains markup as text", async () => {
    const hostile = '<img src=x onerror="window.__wcnPosXss=1">Müdür';
    await bootSurface({
      rootAttrs: DETAIL,
      items: [approval({ candidatePositions: [position(hostile)] })],
      wcn: translator
    });
    expect(app().textContent).toContain(hostile);
    expect(app().querySelector("img[src='x']")).toBeNull();
  });

  it("carries no inline style (FG-003)", async () => {
    await bootSurface({ items: [approval({ stepName: position("Finans Onayı") })], wcn: translator });
    expect(app().querySelector("[data-wcn-step-badge]").hasAttribute("style")).toBe(false);
  });
});

// ══ W-2 ═══════════════════════════════════════════════════════════════════════════════════════════════════
describe("W-2 — an optional note on approve, offered only when the server says so", () => {
  it("offers a note box, with no validator, when the action is flagged", async () => {
    await bootSurface({ items: [approval()], wcn: translator });
    stubDispatch();
    await press("approve");

    expect(confirms).toHaveLength(1);
    const { options } = confirms[0];
    expect(options.showInput).toBe(true);
    expect(options.inputLabel).toBe("Not (isteğe bağlı)");
    expect(options.inputPlaceholder).toBe("Kayda geçecek bir not ekleyin…");
    expect(options.inputValidator, "an optional note must not be validated").toBeFalsy();
    expect(fired, "the optional note must use the shared confirm, not a raw dialog").toHaveLength(0);
  });

  it("sends the note in the field the dispatcher reads", async () => {
    await bootSurface({ items: [approval()], wcn: translator });
    stubDispatch();
    await press("approve");
    confirms[0].onConfirm("  Belgeler tam, ödeme yapılabilir.  ");
    await settle();

    expect(dispatched).toHaveLength(1);
    expect(dispatched[0].actionCode).toBe("approve");
    expect(dispatched[0].providerCode).toBe("workflow");
    expect(dispatched[0].payload.note).toBe("Belgeler tam, ödeme yapılabilir.");
  });

  it.each([[""], ["   "], [undefined]])("sends no note when the box is left empty (%j)", async (typed) => {
    await bootSurface({ items: [approval()], wcn: translator });
    stubDispatch();
    await press("approve");
    confirms[0].onConfirm(typed);
    await settle();

    expect(dispatched).toHaveLength(1);
    expect(dispatched[0].payload.note).toBeNull();
  });

  it("offers no note box when the server did not flag the action", async () => {
    await bootSurface({ items: [approval({ actions: [action("approve")] })], wcn: translator });
    stubDispatch();
    await press("approve");

    expect(confirms).toHaveLength(1);
    expect(confirms[0].options.showInput).toBe(false);
  });

  it("does not derive the note box from the action code", async () => {
    // A `signoff` action that the server flags gets the box; an `approve` it does not flag (above) gets none.
    const flagged = approval({ actions: [action("signoff", { acceptsNote: true, label: { kind: "display", text: "İmzala", locale: "und" } })] });
    await bootSurface({ items: [flagged], wcn: translator });
    stubDispatch();
    await press("signoff");

    expect(confirms).toHaveLength(1);
    expect(confirms[0].options.showInput).toBe(true);
  });

  it("leaves the REQUIRED reason window exactly as it was", async () => {
    const required = approval({ actions: [action("approve", { requiresReason: true })] });
    await bootSurface({ items: [required], wcn: translator });
    stubDispatch();
    await press("approve");

    expect(confirms, "a required reason must not be downgraded to the optional confirm").toHaveLength(0);
    expect(fired).toHaveLength(1);
    expect(fired[0].html).toContain('id="wcnReasonText"');
    // Still mandatory: an empty reason is refused by the window itself.
    document.body.insertAdjacentHTML("beforeend", '<textarea id="wcnReasonText"></textarea>');
    expect(fired[0].preConfirm()).toBe(false);
    expect(global.Swal.validation).toBe("ReasonRequired");
    document.getElementById("wcnReasonText").remove();
  });

  // The box follows the server's FLAG, whatever the action is called. (The server's own Devret also carries the
  // person flag since BL-491 and opens the person window — wcn-delegate-picker-logtime.test.js; this is an action
  // flagged for a note ONLY.)
  it("gives any flagged action the same optional note, and adds no person field", async () => {
    const item = approval({ actions: [action("delegate", { acceptsNote: true })] });
    await bootSurface({ items: [item], wcn: translator });
    stubDispatch();
    await press("delegate");

    expect(confirms).toHaveLength(1);
    expect(confirms[0].options.showInput).toBe(true);
    expect(confirms[0].options.inputType, "the note is a textarea, not a picker").toBeFalsy();
  });
});

// ══ W-3 ═══════════════════════════════════════════════════════════════════════════════════════════════════
describe("W-3 — an approval nobody is named on says which positions it waits for", () => {
  const assigneeField = () => Array.from(app().querySelectorAll(".backbone-preview-field"))
    .find((node) => node.querySelector(".backbone-preview-label")?.textContent === "DetailAssignee");

  it("shows the position names instead of 'unassigned'", async () => {
    const item = approval({ candidatePositions: [position("Finans Müdürü"), position("Hukuk Müşaviri")] });
    await bootSurface({ rootAttrs: DETAIL, items: [item], wcn: translator });

    const value = assigneeField().querySelector(".backbone-preview-value").textContent;
    expect(value).toBe("Onay bekleyen: Finans Müdürü, Hukuk Müşaviri");
    expect(value).not.toContain("Atanmamış");
  });

  it("keeps today's word when the projection carries no candidate positions", async () => {
    await bootSurface({ rootAttrs: DETAIL, items: [approval()], wcn: translator });
    expect(assigneeField().querySelector(".backbone-preview-value").textContent).toBe("Atanmamış");
  });

  it("never uses the word 'Havuz' and never prints an id", async () => {
    const item = approval({ candidatePositions: [position("Finans Müdürü")] });
    await bootSurface({ rootAttrs: DETAIL, items: [item], wcn: translator });
    const text = assigneeField().textContent;
    expect(text).not.toMatch(/havuz/i);
    expect(text).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-/i);
  });

  it("a named assignee still wins over the candidate positions", async () => {
    const item = approval({
      assignee: { id: TASK_ID, displayName: "Ayşe Kaya", isCurrentUser: false },
      candidatePositions: [position("Finans Müdürü")]
    });
    await bootSurface({ rootAttrs: DETAIL, items: [item], wcn: translator });
    expect(assigneeField().querySelector(".backbone-preview-value").textContent).toBe("Ayşe Kaya");
  });
});

// ══ W-4 ═══════════════════════════════════════════════════════════════════════════════════════════════════
describe("W-4 — after a decision on the detail page", () => {
  const FLASH = "wcn:flash-toast";

  /*
   * jsdom refuses real navigation and `location.assign` cannot be stubbed (the same limit wcn-kanban-drag and
   * wcn-calendar-view document). The redirect is observed through what the code does IMMEDIATELY BEFORE it: the
   * success sentence is handed to the next page through sessionStorage, and this page neither re-renders into
   * "not found" nor toasts.
   */
  const GONE = { status: "error", httpStatus: 404, item: null, errors: [] };
  const flashMessage = () => {
    const stored = global.sessionStorage.getItem(FLASH);
    return stored === null ? null : JSON.parse(stored).message;
  };
  const storeFlash = (message, ageMs = 0) =>
    global.sessionStorage.setItem(FLASH, JSON.stringify({ message, at: Date.now() - ageMs }));

  // `single` is what the single-item read answers when the list no longer holds the item; `unavailableSources` is
  // the board's own statement that one of its providers did not answer.
  const decideOnDetail = async ({ stillThere, single = GONE, unavailableSources = [] }) => {
    const item = approval();
    await bootSurface({ rootAttrs: DETAIL, items: [item], wcn: translator });
    global.sessionStorage.setItem("wcn:list-return-url", "/WorkCenterNext?tab=inbox&q=iddia");
    stubDispatch();
    // The re-read after the write: the item is gone (a decided approval) or still projected (a MOD-0024 task).
    const mapped = global.WorkCenterNextApi.mapPayload(stillThere ? [item] : []);
    global.WorkCenterNextApi.fetchWorkItems = () => Promise.resolve(
      { status: "ok", httpStatus: 200, items: mapped.items, errors: [], unavailableSources });
    global.WorkCenterNextApi.fetchWorkItem = () => Promise.resolve(single);
    await press("approve");
    confirms[0].onConfirm("");
    await settle();
    await settle();
  };

  it("hands the success to the list and does not draw 'not found'", async () => {
    await decideOnDetail({ stillThere: false });

    expect(dispatched).toHaveLength(1);
    expect(flashMessage()).toBe("Onayla uygulandı: İddia CLM-42 ödemesi");
    expect(app().textContent).not.toContain("DetailItemNotFound");
    expect(toasts, "the success belongs to the list page, not to the page being left").toHaveLength(0);
  });

  it("stays on the page when the item is still projected (a MOD-0024 task)", async () => {
    await decideOnDetail({ stillThere: true });

    expect(dispatched).toHaveLength(1);
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
    expect(toasts.map((entry) => entry.message)).toEqual(["Onayla uygulandı: İddia CLM-42 ödemesi"]);
    expect(app().querySelector(".wcn-detail"), "the detail page was replaced").toBeTruthy();
  });

  it("does not redirect when the re-read FAILED — the page says so instead", async () => {
    await bootSurface({ rootAttrs: DETAIL, items: [approval()], wcn: translator });
    stubDispatch();
    global.WorkCenterNextApi.fetchWorkItems = () => Promise.resolve(
      { status: "unavailable", httpStatus: 503, items: [], errors: [], unavailableSources: [] });
    await press("approve");
    confirms[0].onConfirm("");
    await settle();
    await settle();

    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
    expect(app().textContent, "a failed read is an error state, not a missing item").not.toContain("DetailItemNotFound");
  });

  /*
   * CT acceptance (2026-10-01, from the independent review). "The item is gone" is only true when somebody CHECKED.
   * A board that came back partial may be missing the item's own provider, and a single-item read that failed for
   * any reason other than a 404 says nothing about the item — in both the reader stays, and no success is handed on.
   */
  it("does not redirect when the item's OWN provider is the one that did not answer", async () => {
    await decideOnDetail({
      stillThere: false, unavailableSources: [{ providerCode: "workflow", reasonCode: "PROVIDER_TIMEOUT" }] });

    expect(dispatched).toHaveLength(1);
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
  });

  // Measured live on dev (2026-10-01): one unrelated provider was down, and nobody was ever returned to the list.
  it("still goes back to the list when ANOTHER provider is the one that did not answer", async () => {
    await decideOnDetail({
      stillThere: false, unavailableSources: [{ providerCode: "dev-reference", reasonCode: "PROVIDER_ERROR" }] });

    expect(dispatched).toHaveLength(1);
    expect(flashMessage()).toBe("Onayla uygulandı: İddia CLM-42 ödemesi");
  });

  it.each([
    ["the network failed", { status: "unavailable", httpStatus: 0, item: null, errors: [] }],
    ["the server answered 503", { status: "unavailable", httpStatus: 503, item: null, errors: [] }],
    ["the server answered 500", { status: "error", httpStatus: 500, item: null, errors: [] }]
  ])("does not redirect when the single-item read did not answer 404 (%s)", async (_label, single) => {
    await decideOnDetail({ stillThere: false, single });

    expect(dispatched).toHaveLength(1);
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
  });

  it("a refused decision does not redirect", async () => {
    await bootSurface({ rootAttrs: DETAIL, items: [approval()], wcn: translator });
    stubDispatch({ ok: false, status: 409, reasonCode: "WORKFLOW_ASSIGNMENT_MISMATCH", data: null, errors: [] });
    await press("approve");
    confirms[0].onConfirm("");
    await settle();

    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
    expect(toasts.some((entry) => entry.type === "error")).toBe(true);
  });

  it("an item opened by address that never existed still reads 'not found'", async () => {
    await bootSurface({ rootAttrs: DETAIL, items: [], wcn: translator });
    expect(app().textContent).toContain("DetailItemNotFound");
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
  });

  it("the list shows the handed-over success once, then forgets it", async () => {
    storeFlash("Onayla uygulandı: İddia CLM-42 ödemesi");
    await bootSurface({ items: [], wcn: translator });

    expect(toasts.map((entry) => entry.message)).toEqual(["Onayla uygulandı: İddia CLM-42 ödemesi"]);
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
  });

  it("the detail page does not consume a success meant for the list", async () => {
    storeFlash("bekleyen");
    await bootSurface({ rootAttrs: DETAIL, items: [approval()], wcn: translator });

    expect(toasts).toHaveLength(0);
    expect(flashMessage()).toBe("bekleyen");
  });

  // CT acceptance — a success that was never shown must not surface later on an unrelated visit to the list.
  it("a success older than its window is dropped unseen", async () => {
    storeFlash("Onayla uygulandı: eski", 21000);
    await bootSurface({ items: [], wcn: translator });

    expect(toasts).toHaveLength(0);
    expect(global.sessionStorage.getItem(FLASH)).toBeNull();
  });

  it.each([["not json"], ['{"message":42,"at":0}'], ['"just a string"']])(
    "a stored value that is not a success of ours is dropped unseen (%s)", async (stored) => {
      global.sessionStorage.setItem(FLASH, stored);
      await bootSurface({ items: [], wcn: translator });

      expect(toasts).toHaveLength(0);
      expect(global.sessionStorage.getItem(FLASH)).toBeNull();
    });

  /*
   * jsdom cannot observe `location.assign`, so the destination is pinned at the source: the redirect asks
   * `listUrlAfterDecision`, and that function never returns a detail address (a detail page opened from another
   * detail page remembers a DETAIL url as "the list").
   */
  it("goes to the list, never to another detail page", () => {
    const APP_SOURCE = require("fs").readFileSync(
      require("path").join(__dirname, "../wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    expect(APP_SOURCE).toContain("global.location.assign(listUrlAfterDecision());");
    const fn = APP_SOURCE.slice(
      APP_SOURCE.indexOf("const listUrlAfterDecision = () => {"),
      APP_SOURCE.indexOf("};", APP_SOURCE.indexOf("const listUrlAfterDecision = () => {")));
    expect(fn).toContain("url.startsWith('/WorkCenterNext/Details') ? '/WorkCenterNext' : url");
  });
});

// ══ the executable contract ═══════════════════════════════════════════════════════════════════════════════
describe("the executable contract knows the new optional fields and refuses nothing older", () => {
  beforeEach(() => {
    delete global.WorkCenterNextContract;
    loadScript("wwwroot/assets/js/WorkCenterNext/fixture-contract.js");
  });

  const codes = (item) => global.WorkCenterNextContract.validateWorkItem(item).errors.map((e) => e.code);

  it("accepts an item with none of the new fields (an older server)", () => {
    expect(codes(approval({ actions: [action("approve")] }))).toEqual([]);
  });

  it("accepts the step name, the candidate positions and the note flag", () => {
    expect(codes(approval({
      stepName: position("Finans Onayı"),
      candidatePositions: [position("Finans Müdürü")]
    }))).toEqual([]);
  });

  it("refuses a step name that is not a label", () => {
    expect(codes(approval({ stepName: "Finans Onayı" }))).toContain("STEP_NAME_INVALID");
  });

  it("refuses candidate positions that are not labels — a raw id never reaches the screen", () => {
    expect(codes(approval({ candidatePositions: [TASK_ID] }))).toContain("CANDIDATE_POSITIONS_INVALID");
    expect(codes(approval({ candidatePositions: "Finans Müdürü" }))).toContain("CANDIDATE_POSITIONS_INVALID");
  });

  it("refuses a note flag that is not a boolean, or that sits beside a required reason", () => {
    expect(codes(approval({ actions: [action("approve", { acceptsNote: "yes" })] })))
      .toContain("ACTION_ACCEPTS_NOTE_INVALID");
    expect(codes(approval({ actions: [action("approve", { acceptsNote: true, requiresReason: true })] })))
      .toContain("ACTION_NOTE_WITH_REQUIRED_REASON");
  });
});

// ══ the presentation mapper ═══════════════════════════════════════════════════════════════════════════════
describe("the presentation mapper carries the new fields, flattened", () => {
  it("resolves the step name and the position names to strings", async () => {
    await bootSurface({ items: [], wcn: translator });
    const item = global.WorkCenterNextData.toPresentation(approval({
      stepName: position("Finans Onayı"),
      candidatePositions: [position("Finans Müdürü"), position("Hukuk Müşaviri")]
    }), { provenance: "api" });

    expect(item.stepNameText).toBe("Finans Onayı");
    expect(item.candidatePositionNames).toEqual(["Finans Müdürü", "Hukuk Müşaviri"]);
    expect(item.actions[0].note).toBe(true);
  });

  it("gives null and an empty list — never undefined — to an item without them", async () => {
    await bootSurface({ items: [], wcn: translator });
    const item = global.WorkCenterNextData.toPresentation(
      approval({ actions: [action("approve")] }), { provenance: "api" });

    expect(item.stepNameText).toBeNull();
    expect(item.candidatePositionNames).toEqual([]);
    expect(item.actions[0].note).toBe(false);
  });
});
