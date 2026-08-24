const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THREE PLACES WHERE TEXT WAS FLOATING, ONE ILLNESS (2026-08-24).
 *
 * The owner reported them in one sitting and they are one decision: a sentence that belongs to something —
 * a prohibition, the object of an action, an empty list — has to sit in the product's own box for that kind of
 * thing, not hang in the card as bare prose.
 *
 * Each fix below reuses a shape THIS PRODUCT ALREADY HAS. Nothing was designed: the alert the subtask gate
 * wears, the badge ten other call sites already pass, and the hint line the checklist card next door already
 * uses for exactly this.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";
const item = (overrides) => Object.assign({
  fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
  admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress",
  executionState: "active", timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Q3 nakit akış projeksiyonu", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}` },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution", "subtasks"],
  subtasks: { mode: "full", items: [] },
  actions: [], concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null
}, overrides);

const bootDetail = (overrides) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item(overrides)]
});

// ── A1 ────────────────────────────────────────────────────────────────────────
describe("a refusal wears the same box wherever it appears", () => {
  const disabledAction = {
    code: "reassign", label: { kind: "resource", key: "WorkAggregation_Action_Reassign" },
    semanticType: "reassign", enabled: false, source: "provider",
    disabledReasonCode: "TASK_DELEGATION_NOT_ALLOWED",
    disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_DelegationNotAllowed" },
    requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
    riskLevel: "normal"
  };

  it("gives the rail's reason the alert the subtask gate already wore", async () => {
    /*
     * MEASURED BEFORE: the gate (`.wcn-subtask-gate`) was `alert alert-warning`; this sentence — the same kind
     * of sentence about the same kind of button — had no background, no border and no padding at all.
     *
     * MUTATION GUARD: return it to a bare <p> and this goes red.
     */
    await bootDetail({ actions: [disabledAction] });
    const reason = app().querySelector(".wcn-act-reason");
    expect(reason, "the rail draws no reason at all").not.toBeNull();
    expect(reason.className).toContain("alert");
    expect(reason.className).toContain("alert-warning");
    expect(reason.querySelector("i").className, "the lock states the prohibition").toContain("bx-lock-alt");
  });

  it("fixes the sibling on the narrow-screen bar in the same breath", () => {
    // Its twin carried the identical bare treatment. Fixing one and leaving the other is the mistake this
    // session made three times; the source is asserted because the bar only renders under 992px.
    const bar = APP.slice(APP.indexOf("wcn-actionbar-reason") - 400, APP.indexOf("wcn-actionbar-reason") + 200);
    expect(bar).toContain("alert alert-warning");
  });

  it("takes its surface from the theme, and only its size from us", () => {
    // Colour, border and radius come from `alert-warning`; these two rules do what `.wcn-subtask-gate` does.
    const rule = CSS.slice(CSS.indexOf("\n.wcn-act-reason {"), CSS.indexOf("}", CSS.indexOf("\n.wcn-act-reason {")));
    expect(rule).toContain("padding: .625rem .875rem");
    expect(rule).toContain("font-size: .8125rem");
    expect(rule, "a colour is being re-declared over the alert's own").not.toContain("color:");
  });
});

// ── A2 ────────────────────────────────────────────────────────────────────────
describe("the object of a confirmation is named once, in a box", () => {
  it("uses the badge the rest of the product uses", () => {
    /*
     * MEASURED: the product had two ways to name the record a confirm is about — `entityName` (ten call sites
     * across six files) and a title quoted inside the sentence (this module only). Never both at once, but two
     * mechanisms for one job. The badge won: it exists, it is the majority, and it is already the framed box.
     *
     * MUTATION GUARD: stop passing `entityName` and this goes red.
     */
    const onAction = APP.slice(APP.indexOf("const stillOpen = action.code === 'complete'"), APP.indexOf("const executeTriggerAction"));
    expect(onAction).toContain("entityName: esc(item.title)");
    const bridge = APP.slice(APP.indexOf("const sharedConfirm"), APP.indexOf("const sharedConfirm") + 4000);
    expect(bridge).toContain("entityName: options.entityName");
  });

  it("stops quoting the same name inside the sentence", () => {
    const onAction = APP.slice(APP.indexOf("const body = item.delegator"), APP.indexOf("const body = item.delegator") + 300);
    expect(onAction).toContain("t('ConfirmBody')");
    expect(onAction, "the title is still being interpolated into the prose").not.toContain("tf('ConfirmBody', item.title)");
  });

  it("says so in all seven languages, with no leftover placeholder", () => {
    LANGS.forEach((lang) => {
      const text = resx(lang);
      const body = (text.match(/name="ConfirmBody"[^>]*>\s*<value>([\s\S]*?)<\/value>/) || [])[1];
      expect(body, `${lang} has no ConfirmBody`).toBeTruthy();
      expect(body, `${lang} still interpolates a name`).not.toContain("{0}");
      // The on-behalf variant keeps ONE token — the delegator, who is not the object of the action.
      const onBehalf = (text.match(/name="ConfirmBodyOnBehalf"[^>]*>\s*<value>([\s\S]*?)<\/value>/) || [])[1];
      expect(onBehalf).toContain("{0}");
      expect(onBehalf, `${lang} still has two tokens`).not.toContain("{1}");
    });
  });
});

// ── A5 ────────────────────────────────────────────────────────────────────────
describe("an empty subtask card is still a card", () => {
  beforeEach(async () => { await bootDetail(); });

  it("keeps its head and its counter", () => {
    /*
     * MEASURED BEFORE: an empty list replaced the whole card — head and all — with a one-line
     * `wcn-empty-line`. The card the reader was looking at vanished the moment its last child went.
     *
     * MUTATION GUARD: hide the head again and this goes red.
     */
    const card = app().querySelector("#wcn-subtasks-card");
    expect(card, "the card is gone entirely").not.toBeNull();
    expect(card.querySelector("h6").textContent).toContain("SubtasksLabel");
    expect(card.querySelector(".wcn-subtask-count").textContent).toBe("0");
  });

  it("keeps the add row where it always is", () => {
    const card = app().querySelector("#wcn-subtasks-card");
    expect(card.querySelector(".wcn-subtask-add"), "the add row moved or vanished").not.toBeNull();
    expect(app().querySelector(".wcn-empty-line"), "the old one-line empty state is back").toBeNull();
  });

  it("puts the sentence BELOW the add row, as a hint and not an alert", () => {
    /*
     * The owner said this outright: no alert. An empty list is not a warning. `.wcn-block-hint` is the
     * checklist card's own answer to the same question, and it already sits under THIS card's add row.
     */
    const card = app().querySelector("#wcn-subtasks-card");
    const hints = [...card.querySelectorAll(".wcn-block-hint")];
    expect(hints.map((h) => h.textContent.trim())).toContain("SubtasksEmpty");
    expect(card.querySelector(".alert"), "the empty state is shouting").toBeNull();
    // Order: the add row is declared before the sentence, so the sentence lands where a deleted row would be.
    const html = card.innerHTML;
    expect(html.indexOf("wcn-subtask-add")).toBeLessThan(html.indexOf("SubtasksEmpty"));
  });

  it("says nothing it cannot measure", () => {
    // No 0-of-0 progress bar: an empty bar claims the card is tracking something it is not.
    expect(app().querySelector("#wcn-subtasks-card progress")).toBeNull();
  });
});


describe("a reason gets the card's width, and brings its button with it", () => {
  /*
   * MEASURED (the defect the owner spotted in a screenshot): `.wcn-actrail-secondary` is a wrapping flex row
   * whose items size to their content, and the alert lives inside the same `<li>` as its button — so the
   * sentence was clipped to the button's width: 194px inside a 371px card, which reads as an indent.
   *
   * ⚠ THE BUTTON TRAVELS WITH THE SENTENCE, deliberately. This card can show TWO reasons at once (measured
   * live: "Bir alt görev hâlâ açık" under Tamamla and "Bu görev devredilemez." under Başkasına ata). Spreading
   * the alerts across the card alone would break the only thing that says which sentence belongs to which
   * button — sitting under it.
   */
  const withReason = (code, key) => ({
    code, label: { kind: "resource", key: `WorkAggregation_Action_${key}` }, semanticType: code,
    enabled: false, source: "provider", disabledReasonCode: "X",
    disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_DelegationNotAllowed" },
    requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false,
    riskLevel: "normal"
  });
  const plain = (code, key) => Object.assign(withReason(code, key), {
    enabled: true, disabledReasonCode: null, disabledReason: null
  });

  it("marks only the rows that carry one", async () => {
    // MUTATION GUARD: drop the marker (or put `flex: 0 1 auto` back) and the sentence returns to the button's
    // width — which is the reported defect, and it passed every test before this one existed.
    await bootDetail({ actions: [withReason("reassign", "Reassign"), plain("inquire", "Inquire")] });
    /*
     * Which TIER an action lands in is the projection's business (primary / secondary / destructive), so the
     * rows are read from the whole card rather than from one list — asserting on a tier here would be testing
     * the tiering, which has its own tests.
     */
    const rows = [...app().querySelectorAll(".wcn-act")];
    const withOne = rows.filter((li) => li.querySelector(".wcn-act-reason"));
    const without = rows.filter((li) => !li.querySelector(".wcn-act-reason"));
    expect(withOne.length, "no action carried a reason").toBeGreaterThan(0);
    expect(without.length, "every action carried a reason — the fixture proves nothing").toBeGreaterThan(0);
    // Only a SECONDARY row needs the marker: the primary tier is already a full-width row of its own.
    withOne.filter((li) => li.className.includes("wcn-act-secondary"))
      .forEach((li) => expect(li.className, "the reason row is not marked").toContain("wcn-act-hasreason"));
    without.forEach((li) => expect(li.className, "a reason-less action was widened too").not.toContain("wcn-act-hasreason"));
  });

  it("takes the whole row, button included, in the stylesheet", () => {
    const rule = CSS.slice(CSS.indexOf(".wcn-actrail-secondary .wcn-act-hasreason"),
      CSS.indexOf(".wcn-actrail-secondary .wcn-act-hasreason") + 220);
    expect(rule).toContain("flex: 1 0 100%");
    expect(rule, "the button stayed at its own width while the alert grew").toContain("inline-size: 100%");
  });

  it("keeps each sentence under its own button when there are two", async () => {
    await bootDetail({ actions: [withReason("reassign", "Reassign"), withReason("inquire", "Inquire")] });
    const rows = [...app().querySelectorAll(".wcn-act")].filter((li) => li.querySelector(".wcn-act-reason"));
    expect(rows).toHaveLength(2);
    rows.forEach((li) => {
      const btn = li.querySelector(".wcn-act-btn");
      const reason = li.querySelector(".wcn-act-reason");
      // Same <li>: the pairing is structural, not a matter of where they happen to land.
      expect(btn.parentElement).toBe(reason.parentElement);
    });
  });
});

describe("one dismiss word in a module whose actions are named 'iptal'", () => {
  /*
   * CENSUS (2026-08-24): fifteen `showConfirm` calls in twelve files. Twelve of them sit in modules whose
   * actions are Delete / Remove / Publish / Reactivate — there "İptal" can only mean "never mind", and they are
   * LEFT UNTOUCHED. This module is the exception: "Görevi iptal et" and "Alt görevi iptal et" are ACTIONS, so a
   * dismiss button saying "İptal" offers one word for both answers to one question.
   */
  it("says Vazgeç everywhere in this module, and never the action's own word", () => {
    // MUTATION GUARD: turn one back to `ReasonCancel` and this goes red.
    expect(APP, "a dialog still dismisses with the word an action is named after")
      .not.toContain("cancelButtonText: t('ReasonCancel')");
    const dismissals = (APP.match(/cancelButtonText: t\('DialogDismiss'\)/g) || []).length;
    expect(dismissals, "the module's dialogs do not share one dismiss word").toBeGreaterThan(6);
  });

  it("gives the subtask-cancel dialog two different words for its two answers", () => {
    const fn = APP.slice(APP.indexOf("const confirmDestructive"), APP.indexOf("const confirmDestructive") + 1200);
    expect(fn).toContain("confirmButtonText: t('SubtaskCancelConfirmYes')");
    expect(fn).toContain("cancelButtonText: t('DialogDismiss')");
  });

  it("leaves the other eleven modules alone", () => {
    const fs2 = require("fs");
    const path2 = require("path");
    const root = web("wwwroot", "assets", "js");
    const files = [];
    const walk = (dir) => fs2.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path2.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "WorkCenterNext") { walk(p); } }
      else if (e.name.endsWith(".js")) { files.push(p); }
    });
    walk(root);
    // Nobody outside this module was given the new word: their "İptal" is not ambiguous, so it stays.
    const touched = files.filter((p) => /DialogDismiss/.test(fs2.readFileSync(p, "utf8")));
    expect(touched.map((p) => path2.basename(p))).toEqual([]);
  });

  it("ships the word in all seven languages, distinct from the action's", () => {
    LANGS.forEach((lang) => {
      const text = resx(lang);
      const dismiss = (text.match(/name="DialogDismiss"[^>]*>\s*<value>([\s\S]*?)<\/value>/) || [])[1];
      const cancel = (text.match(/name="ReasonCancel"[^>]*>\s*<value>([\s\S]*?)<\/value>/) || [])[1];
      expect(dismiss, `${lang} has no DialogDismiss`).toBeTruthy();
      expect(dismiss, `${lang} uses the same word for both`).not.toBe(cancel);
    });
  });
});
