const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * CT walked a REAL task (`370ab18b`) end to end and came back with two findings. This file holds both.
 *
 *   BL-238 — the subtask checkbox could only ever produce an error. It called `complete` on a child nobody had
 *            started, and the server refused (409 TASK_INVALID_STATE). Correctly. The route was the defect.
 *   BL-237 — "Duraklat" was offered in the showcase and did not exist on the server. The button changed local
 *            state and nothing else, so it looked like it worked.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const PARENT_ID = "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0";
const CHILD_ID = "bd76df2e-8e4b-57d4-b4fa-0e9d65f7c3c1";

const parent = (childStatus) => ({
  fixtureKind: "workItem",
  id: PARENT_ID,
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
  title: { kind: "display", text: "Month-end close", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: PARENT_ID, deepLink: `/Tasks/${PARENT_ID}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "subtasks"],
  actions: [{
    code: "complete", label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete", enabled: true, source: "provider",
    disabledReasonCode: null, disabledReason: null,
    requiresConfirmation: true, requiresReason: false, requiresEvidence: false,
    supportsBulk: false, riskLevel: "normal"
  }],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-08-01T00:00:00+00:00",
  subtasks: { mode: "full", items: [{ id: CHILD_ID, title: "Import balances", status: childStatus }] }
});

/**
 * Boots the detail page and returns the transition calls the checkbox made, in order.
 *
 * The version the second call must use is NOT hard-coded here: `get` answers with a token that CHANGES after
 * `start`, exactly as the server's does. A test that answered with one fixed token could not tell a re-read
 * from a reuse.
 */
const tick = async ({ childStatus = "not-started", startOk = true, completeOk = true } = {}) => {
  const calls = [];
  const said = [];
  // The product's own toast seam (MOD-0013). Recorded rather than rendered, so what the reader is TOLD is
  // assertable — a write that reports nothing is the failure mode this whole file exists for.
  global.showToast = (message, type) => { said.push({ message, type }); };
  let version = 3;
  const boot = await bootSurface({
    rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${PARENT_ID}"`,
    items: [parent(childStatus)]
  });
  global.TasksApi.get = () => Promise.resolve({
    ok: true, status: 200, data: { id: CHILD_ID, version }
  });
  global.TasksApi.transition = (id, code, body) => {
    calls.push({ id, code, expectedVersion: body && body.expectedVersion });
    if (code === "start") {
      if (!startOk) { return Promise.resolve({ ok: false, status: 409 }); }
      // The server bumps the token on every accepted write. So does this.
      version = 4;
      return Promise.resolve({ ok: true, status: 204 });
    }
    return Promise.resolve(completeOk ? { ok: true, status: 204 } : { ok: false, status: 409 });
  };

  const box = app().querySelector("[data-wcn-subtask]");
  expect(box, "the subtask row has no checkbox to tick").not.toBeNull();
  box.dispatchEvent(new global.Event("click", { bubbles: true }));
  await new Promise((resolve) => setTimeout(resolve, 0));
  return { calls, said, boot };
};

describe("BL-238 — ticking the box finishes the subtask instead of reporting a rule", () => {
  it("starts a not-started child first, then completes it", async () => {
    const { calls } = await tick({ childStatus: "not-started" });
    /*
     * MUTATION GUARD: make the checkbox call only `complete` again and this goes red — one call, named
     * `complete`, against a child the server knows has not begun. That is the exact 409 CT photographed.
     */
    expect(calls.map((c) => c.code), "the box did not start the child before finishing it")
      .toEqual(["start", "complete"]);
    expect(calls.every((c) => c.id === CHILD_ID)).toBe(true);
  });

  it("gives `complete` the version `start` left behind, not the one it began with", async () => {
    const { calls } = await tick({ childStatus: "not-started" });
    /*
     * MUTATION GUARD: pass the pre-`start` token to `complete` and this goes red. Reusing it makes the second
     * call fail as a concurrency conflict — about a change the reader themselves had just made one call earlier.
     */
    expect(calls[0].expectedVersion, "start used the token read before it").toBe(3);
    expect(calls[1].expectedVersion, "complete reused a token `start` had already spent").toBe(4);
  });

  it("leaves a child already in progress alone — one write, not two", async () => {
    const { calls } = await tick({ childStatus: "in-progress" });
    // An unnecessary `start` is a second write, a second audit entry and a second chance to fail.
    expect(calls.map((c) => c.code)).toEqual(["complete"]);
  });

  it("says both halves out loud when it started but could not finish", async () => {
    const { calls, said } = await tick({ childStatus: "not-started", completeOk: false });
    expect(calls.map((c) => c.code)).toEqual(["start", "complete"]);
    /*
     * The child is running now and NOBODY ASKED FOR THAT. Silence here would leave the list showing a change
     * the reader did not choose, with no way to tell it from a no-op. `t`/`tf` echo the key, so this asserts
     * the sentence the code reached for — not a translation that could drift on its own.
     */
    expect(said.map((s) => s.message), "the half-done outcome was never said")
      .toContain("SubtaskStartedNotCompleted");
    expect(said[said.length - 1].type, "a half-done write reported as a success").toBe("warning");
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      expect(resx, `${lang} cannot say the half-done sentence`).toContain('name="SubtaskStartedNotCompleted"');
    });
  });

  it("does not reach for `complete` at all when `start` is refused", async () => {
    const { calls } = await tick({ childStatus: "not-started", startOk: false });
    // Nothing moved: the ordinary refusal path, with the server's own reason. A `complete` here would be a
    // second failure reported on top of the first.
    expect(calls.map((c) => c.code)).toEqual(["start"]);
  });
});

describe("the checkbox's own sentence is complete, not a two-argument one short", () => {
  it("never hands a two-argument string one argument", async () => {
    const { said } = await tick({ childStatus: "not-started" });
    /*
     * MEASURED LIVE (2026-08-25): the reader was shown a literal "{1} — 'BL-238 …' işlemi tamamlandı".
     * `ToastActionApplied` takes the action label AND the title; this call site had only the title.
     *
     * MUTATION GUARD: point the checkbox back at `ToastActionApplied` and this goes red — the placeholder
     * count no longer matches the arguments the call site can supply.
     */
    expect(said.map((s) => s.message)).toContain("ToastSubtaskCompleted");
    expect(said.map((s) => s.message), "borrowing a two-argument sentence again")
      .not.toContain("ToastActionApplied");
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      const value = resx.split('name="ToastSubtaskCompleted"')[1].split("</data>")[0];
      expect(value, `${lang} is missing the subtask sentence`).toContain("{0}");
      expect(value, `${lang} asks for an argument the call site does not have`).not.toContain("{1}");
    });
  });
});

describe("BL-237 — nothing offers a pause the server has never heard of", () => {
  const shipped = () => {
    const out = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "node_modules") { walk(p); } }
      else if (/\.js$/.test(e.name)) { out.push(p); }
    });
    walk(web("wwwroot", "assets", "js", "WorkCenterNext"));
    return out;
  };
  // Comments may DISCUSS the removal — that is the record of why. Code may not perform it.
  const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

  it("has no `pause` transition anywhere under WorkCenterNext", () => {
    /*
     * MUTATION GUARD: restore `case 'pause'` — or an `action('pause')` in any fixture — and this goes red with
     * the file that brought it back.
     */
    const offenders = shipped().filter((p) => code(fs.readFileSync(p, "utf8")).includes("'pause'"));
    expect(offenders.map((p) => path.basename(p)), "something offers a pause again").toEqual([]);
  });

  it("keeps no label for a button that cannot exist", () => {
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      expect(resx, `${lang} still carries the Duraklat button label`).not.toContain('name="ActPause"');
      expect(resx, `${lang} still carries the paused-timer toast`).not.toContain('name="ToastTimerPaused"');
    });
  });

  it("keeps the paused STATE, because the server still emits one", () => {
    /*
     * ⚠ NOT A LEFTOVER. `ResolveExecutionState` maps Waiting and PendingReview to "paused" — a task really can
     * stand paused, it just cannot be paused BY A BUTTON. Deleting this branch alongside the transition would
     * blank the state line on every waiting task.
     */
    const provider = fs.readFileSync(web("..", "..", "services", "Diten.Platform", "src",
      "Diten.Platform.Application", "Features", "Tasks", "Providers", "TaskWorkItemProvider.cs"), "utf8");
    expect(provider).toContain('TaskLifecycle.Waiting or TaskLifecycle.PendingReview => "paused"');
    expect(APP).toContain("TimerStatePaused");
  });
});

describe("the refusal the checkbox actually met is named, not generalised", () => {
  /*
   * MEASURED LIVE (2026-08-25) on task 729ed081 with a review-gated child: `start` 204, `complete` 409
   * REVIEW_PENDING — and the reader was told "İşlem sırasında bir hata oluştu". The code was unmapped, so
   * `failureMessage` fell through to the generic sentence exactly as it is designed to (loudly, in the console).
   * The gap was the map, not the mechanism.
   */
  const API = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "api.js"), "utf8");
  const PAYLOAD = fs.readFileSync(web("Views", "Tasks", "_IndexL10n.cshtml"), "utf8");

  it("carries REVIEW_PENDING through map, blocking list, payload and all seven resx", () => {
    // MUTATION GUARD: drop any ONE of these four and this goes red naming which link broke. A reason code needs
    // all four or it silently degrades to "an error occurred".
    expect(API, "not in the message map").toContain("REVIEW_PENDING: 'errorReviewPending'");
    expect(API, "not treated as a rule, so it would read as a race").toContain("'REVIEW_PENDING'");
    expect(PAYLOAD, "the browser never receives the sentence").toContain("ErrorReviewPending");
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(web("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`), "utf8");
      expect(resx, `${lang} cannot name the review gate`).toContain('name="ErrorReviewPending"');
    });
  });

  it("does not reuse the approval sentence for it", () => {
    /*
     * The server's own doc-comment says why: the two gates are cleared by DIFFERENT PEOPLE. Telling a holder
     * "approval pending" while a reviewer holds the work sends them to the wrong person.
     */
    expect(API).not.toContain("REVIEW_PENDING: 'errorApprovalPending'");
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(web("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`), "utf8");
      const review = resx.split('name="ErrorReviewPending"')[1].split("</data>")[0];
      const approval = resx.split('name="ErrorApprovalPending"')[1].split("</data>")[0];
      expect(review, `${lang} says the same thing for both gates`).not.toBe(approval);
    });
  });
});

describe("a disabled action's reason is addressed to it, on every tier", () => {
  it("gives the primary's sentence an id and points the button at it", () => {
    /*
     * MEASURED LIVE (2026-08-25, task f5d31d28): the primary "Tamamla" was dimmed, the sentence "Bir alt görev
     * hâlâ açık" was on screen right beneath it — and `p.id` was "" while `button[aria-describedby]` was null.
     * A seeing reader was told why; a screen-reader user heard only "dimmed".
     *
     * MUTATION GUARD: send the primary back to `describedBy = ''` and this goes red.
     */
    expect(APP, "the primary tier is excluded from the association again")
      .not.toContain("const describedBy = variant === 'primary' ? '' :");
    expect(APP).toContain("const describedBy = reasonId;");
    // The id has to be ON the sentence too, or the button points at nothing — the failure the id helper's own
    // comment warns about.
    expect(APP).toContain('id="${esc(reasonId)}"');
  });
});

describe("the sticky bar's copy of the primary tells the same story as the card's", () => {
  const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");

  it("dims off the button's own state, so no render path can miss it", () => {
    /*
     * MEASURED LIVE (2026-08-25, task f5d31d28 at 900px): the card's blocked "Tamamla" was `opacity: .55`; the
     * sticky bar's copy of the SAME blocked button came back `opacity: 1` — full bright green, reading as
     * clickable directly under its own "Bir alt görev hâlâ açık". The dimming keyed off `.wcn-act-disabled` on
     * a wrapping `<li>`, and only the card's path writes that wrapper.
     *
     * MUTATION GUARD: take `opacity` back off the `:disabled` rule and this goes red.
     */
    expect(CSS).toContain(".wcn-act-btn:disabled { opacity: .55; cursor: not-allowed; }");
  });

  it("addresses its sentence with an id of its own, not the card's", () => {
    /*
     * The bar is `d-lg-none` — hidden above 992px but still in the DOM — so both sentences exist on every page.
     * MUTATION GUARD: drop the `-bar` suffix and this goes red; the document would carry a duplicate id and
     * `getElementById` would answer with the card's sentence, which is not the one beside this button.
     */
    expect(APP).toContain("`${actionReasonId(item, primary)}-bar`");
    expect(APP).toContain('reasonId ? ` aria-describedby="${esc(reasonId)}"` : \'\'');
  });
});
