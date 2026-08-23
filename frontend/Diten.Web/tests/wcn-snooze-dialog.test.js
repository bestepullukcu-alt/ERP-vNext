const fs = require("fs");
const path = require("path");

/*
 * THE SNOOZE DIALOG SAYS WHAT SNOOZING DOES.
 *
 * It used to be a raw `Swal.fire`: the word "Ertele", a bare date box, and no sentence anywhere explaining what
 * would happen — which is a lot to ask of someone who has never used it. Everything it lacked was already in the
 * product's one confirm; the only thing keeping it out was a hard-coded `input: 'textarea'`.
 *
 * ⚠ WHAT THE DESCRIPTION MAY CLAIM WAS MEASURED. Three clauses are enforced by the server and the contract
 * (`SNOOZE_MUST_NOT_CREATE_WAITING`): the status does not move, the due date is untouched, the requester sees
 * nothing. A fourth — "it disappears from your inbox" — is NOT true today: nothing filters a snoozed item out of
 * any list. The sentence therefore does not say it, and the last test here is what keeps it from creeping back
 * in before the filtering exists.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const LANGS = ["tr", "en", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (lang, key) => {
  const m = resx(lang).match(new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return m ? m[1] : null;
};

/** The body of `toggleSnooze`. The dialog lives inside it. */
const toggleSnooze = () => {
  const start = APP.indexOf("const toggleSnooze");
  return APP.slice(start, APP.indexOf("\n    const ", start + 40));
};

/**
 * The same body with its comments removed.
 *
 * A guard that greps the source must not match the prose EXPLAINING the thing it forbids — the comment above
 * `toggleSnooze` says the words "diten-field" precisely to warn the next reader off them. This session has been
 * caught by that four times; stripping first is the fix that keeps working.
 */
const toggleSnoozeCode = () => toggleSnooze()
  .replace(/\/\*[\s\S]*?\*\//g, "")
  .replace(/(^|[^:])\/\/.*$/gm, "$1");

describe("the snooze dialog", () => {
  it("goes through the product's one confirm, not a raw Swal", () => {
    expect(toggleSnooze()).toContain("sharedConfirm(");
    expect(toggleSnooze(), "the raw dialog is still here").not.toContain("global.Swal.fire(");
  });

  it("carries a title and a description", () => {
    // MUTATION GUARD: delete the subtext and this goes red — a dialog that asks for a commitment without saying
    // what it does is the defect this round came to fix, and it would otherwise return silently.
    expect(toggleSnooze()).toContain("title: t('SnoozeTitle')");
    expect(toggleSnooze()).toContain("subtext: esc(t('SnoozeSubtext'))");
  });

  it("asks for a date through the picker the rest of the page uses", () => {
    const body = toggleSnooze();
    expect(body).toContain("type: 'text'");            // not a native date control — one date language per product
    expect(body).toContain("global.flatpickr");
    expect(body).toContain("minDate: data.todayIso");
  });

  it("keeps the date box where the library looks for it", () => {
    /*
     * ⚠ THE DEFECT THIS ROUND FIXED, pinned so it cannot come back. SweetAlert finds its input among the
     * popup's DIRECT CHILDREN, in a fixed slot list. A `.diten-field` wrapper made it a grandchild and
     * `Swal.getInput()` returned null — so the validator read '' and answered "you cannot pick a past date"
     * for a date in the FUTURE, the field never took focus, and Enter stopped confirming. One wrapper, three
     * wounds, and only the first was visible.
     *
     * MUTATION GUARD: wrap the input again and this goes red.
     */
    const code = toggleSnoozeCode();
    expect(code, "the input is being wrapped").not.toContain("diten-field");
    expect(code).toContain("classList.add('wcn-date-input')");
    expect(code, "an element is being inserted around the box").not.toContain("insertBefore");
  });

  it("shows the date format the box will actually hold", () => {
    // MUTATION GUARD: remove the placeholder and the reader is asked for a date with no clue which shape it
    // wants — the field is a text box, and the picker only helps the people who find it.
    expect(toggleSnooze()).toContain("placeholder: t('SnoozeDatePlaceholder')");
    // Not a new format: the same `Y-m-d` every date field in the product is pinned to.
    expect(toggleSnooze()).toContain("dateFormat: 'Y-m-d'");
    LANGS.forEach((lang) => {
      const p = value(lang, "SnoozeDatePlaceholder");
      expect(p, `${lang} has no placeholder`).toBeTruthy();
      /*
       * The mask the PRODUCT already uses, measured on the create form's two date fields
       * (`Views/Tasks/_Form.cshtml`), which spell it `YYYY-MM-DD` in every language. A second mask for the same
       * format would put two answers to one question in one product; the key stays per-language so a language
       * can diverge later without touching code.
       */
      expect(p, `${lang} invents a second date mask`).toBe("YYYY-MM-DD");
    });
  });

  it("refuses the PAST and accepts TODAY, which the server accepts too (BL-182)", () => {
    /*
     * The calendar offered today (`minDate: todayIso`) while the check rejected it — one field disagreeing with
     * itself. The server stores the snooze at 23:59:59 of the chosen day, so today means "for the rest of
     * today", which is a real request. The past is still refused here AND on the server (400).
     */
    expect(toggleSnooze()).toContain("value < data.todayIso");
    expect(toggleSnooze(), "today is still being rejected").not.toContain("value <= data.todayIso");
    expect(toggleSnooze()).toContain("t('SnoozeFuture')");
  });

  it("shows a snooze that runs to the END of today, having accepted one", () => {
    /*
     * MEASURED: with the picker accepting today, the server stored `2026-08-23T20:59:59Z` and the screen showed
     * NOTHING — no row, no chip, no banner — because four separate places asked "is it snoozed?" with `>`
     * against today's date. Accepting a value the screen then refuses to display is worse than refusing it.
     * One predicate now answers the question, and it answers `>=`.
     */
    expect(APP).toContain("const isSnoozed = (item) => !!item.snoozedUntil && item.snoozedUntil >= data.todayIso;");
    expect(APP.match(/isSnoozed\(item\)/g) || [], "a caller still asks this question its own way").toHaveLength(4);
    expect(APP, "an old `>` comparison survived").not.toContain("item.snoozedUntil > data.todayIso");
  });

  it("says what it actually refuses, in all seven languages", () => {
    // The message used to read "pick a FUTURE date" while today is now accepted — the words had to follow the
    // rule, not the other way round.
    LANGS.forEach((lang) => expect(value(lang, "SnoozeFuture"), `${lang}`).toBeTruthy());
    expect(value("tr", "SnoozeFuture")).not.toContain("Gelecek");
    expect(value("en", "SnoozeFuture").toLowerCase()).not.toContain("future");
  });

  it("does not offer a dismiss button that reads like 'cancel the task'", () => {
    /*
     * The wrapper's default dismiss word is the shared `Cancel` string — "İptal" in Turkish. This page carries an
     * ACTION called "Görevi iptal et" which calls the task off for everyone, so the two must not share a word.
     */
    expect(toggleSnooze()).toContain("cancelText: t('DialogDismiss')");
    expect(value("tr", "DialogDismiss")).not.toBe(value("tr", "ReasonCancel"));
    expect(value("tr", "DialogDismiss")).not.toContain("İptal");
  });
});

describe("the dialog's words", () => {
  ["SnoozeTitle", "SnoozeSubtext", "SnoozeUntilLabel", "DialogDismiss"].forEach((key) => {
    it(`${key} ships in all seven languages`, () => {
      LANGS.forEach((lang) => expect(value(lang, key), `${lang} has no ${key}`).toBeTruthy());
    });
  });

  it("says the description as ONE whole sentence per language, never assembled from parts", () => {
    LANGS.forEach((lang) => {
      const text = value(lang, "SnoozeSubtext");
      expect(text, `${lang} builds the sentence from placeholders`).not.toMatch(/\{\d+\}/);
      expect(text.length, `${lang} is too short to be the explanation`).toBeGreaterThan(20);
    });
  });

  it("does not promise the one thing snooze does not do yet", () => {
    /*
     * Nothing filters a snoozed item out of any list — not the provider, not `activeItems`. Until it does, the
     * sentence must not say the task disappears; a dialog that describes a feature the product lacks is worse
     * than one that says nothing.
     */
    expect(APP, "something now filters on the snooze — the sentence may say so")
      .not.toMatch(/filter[^\n]*snoozedUntil|snoozedUntil[^\n]*=> false/);
    ["görünmez", "disappear", "hidden from", "gelen kutunda"].forEach((claim) =>
      expect(value("tr", "SnoozeSubtext").toLowerCase() + value("en", "SnoozeSubtext").toLowerCase())
        .not.toContain(claim));
  });
});


describe("what the dialog hands the validator", () => {
  /*
   * The shipped defect was not in the validator's RULE, it was in the value reaching it: a wrapper hid the box
   * from the library, `Swal.getInput()` came back null, and the validator was handed '' — which its empty check
   * then reported as "you cannot pick a past date", over a date the user had picked in the FUTURE.
   *
   * So both halves are pinned here, by CALLING the validator the module actually builds: an empty value is
   * refused (that check is what turns a lost value into a visible refusal rather than a silent save), and a
   * future date passes. The harness captures `window.showConfirm`, which is the seam the module calls.
   */
  const { bootSurface, app } = require("./wcn-boot");
  const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";
  const FUTURE = new Date(Date.now() + 30 * 864e5).toISOString().slice(0, 10);

  const item = {
    fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress",
    executionState: "active", timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Q3", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
      deepLink: `/Tasks/${TASK_ID}` },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
    lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution"], actions: [],
    concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null
  };

  let captured;
  beforeEach(async () => {
    captured = null;
    // `toggleSnooze` falls back to an immediate snooze when there is no dialog library at all, so both seams
    // are present: a Swal object for the guard to pass, and the shared confirm the module actually calls.
    global.Swal = { fire: () => ({ then: () => {} }), showValidationMessage: () => {}, getInput: () => null };
    global.showConfirm = (title, callback, options) => { captured = { title, callback, options }; };
    await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`, items: [item] });
    app().querySelector("[data-wcn-snooze]").click();
    // The click handler is async (it awaits the item lookup before opening), so one microtask is not enough.
    await new Promise((r) => setTimeout(r, 50));
  });

  it("reaches the shared confirm at all", () => {
    // Non-vacuity: every assertion below would pass on a dialog that never opened.
    expect(captured, "the snooze dialog never opened").toBeTruthy();
    /*
     * What is captured here is what reaches the SHARED COMPONENT: the module's seam flattens its own
     * `input: {…}` into the wrapper's vocabulary, so the validator arrives as `inputValidator`. Asserting on
     * that name is deliberate — it is the contract between the module and the component.
     */
    expect(typeof captured.options.inputValidator).toBe("function");
    expect(captured.options.showInput).toBe(true);
    expect(captured.options.inputType).toBe("text");
  });

  it("refuses an empty value — the state a lost box produces", () => {
    // MUTATION GUARD: drop the empty check and a dialog whose value never arrived would save nothing, silently.
    expect(captured.options.inputValidator("")).toBeTruthy();
    expect(captured.options.inputValidator(undefined)).toBeTruthy();
  });

  it("accepts a date in the future", () => {
    expect(captured.options.inputValidator(FUTURE)).toBeNull();
  });

  it("refuses a date in the past, and accepts today", () => {
    expect(captured.options.inputValidator("2020-01-01")).toBeTruthy();
    const today = new Date().toISOString().slice(0, 10);
    expect(captured.options.inputValidator(today), "today is a legitimate choice (BL-182)").toBeNull();
  });
});
