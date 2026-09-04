const fs = require("fs");
const path = require("path");

/*
 * A3 — EVERY DIALOG IN THIS MODULE SPEAKS THE PRODUCT'S ONE VISUAL LANGUAGE (2026-08-24, owner decision (b)).
 *
 * MEASURED, side by side with the snooze dialog (the reference):
 *      raw Swal.fire        snooze (shared component)
 *   title      38px         18px
 *   body       18px         13px
 *   popup      512px        400px
 *   dismiss    RED          neutral grey
 *   icon       none         moon
 *
 * The cause was not carelessness at the call sites: the product's appearance lived INSIDE `showConfirm`, as a
 * `customClass` literal, so a dialog that could not be a confirmation could not have the look either. The
 * `btn-label-danger` on the dismiss button is this theme's own global default — the package was the only thing
 * overruling it.
 *
 * The owner's decision: PUBLISH the package (`window.DitenDialogAppearance`), do not grow `showConfirm` a
 * "custom fields" seam. Four dialogs that ask for ONE value moved into the shared component; four that are not
 * confirmations at all take the package and keep their own body.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (xml, key) => {
  const at = xml.indexOf(`name="${key}"`);
  if (at < 0) { return null; }
  return xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
};

/*
 * ⚠ BOTH CALL FORMS. `showConfirm(...)` and `showConfirm?.(...)` are the same call; a regex that sees only the
 * first undercounted this product's dialogs by a factor of five for an entire session. Any census in this file
 * uses THIS constant — never a fresh inline regex.
 */
const CALL = /showConfirm\s*\??\.?\s*\(/;
const CALL_G = /showConfirm\s*\??\.?\s*\(/g;

/** Every shipped source file that could hold a copy of the appearance. */
const sourceFiles = () => {
  const files = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "node_modules") { walk(p); } }
    else if (/\.(js|cshtml|css)$/.test(e.name)) { files.push(p); }
  });
  walk(web("wwwroot", "assets", "js"));
  walk(web("Views"));
  return files;
};

describe("the appearance has exactly one definition", () => {
  it("declares the package once, in the shared component", () => {
    /*
     * MUTATION GUARD #1: copy the `customClass` block into a second file — or back into a `Swal.fire` — and
     * this goes red. Two copies of a look agree the day they are written and drift within a fortnight; that
     * drift is what the owner photographed.
     */
    const FINGERPRINT = "popup: 'rounded-4 shadow-lg'";
    const hits = sourceFiles().filter((f) => fs.readFileSync(f, "utf8").includes(FINGERPRINT))
      .map((f) => path.relative(repoRoot, f)).sort();
    /*
     * ⚠ MEASURED AND REPORTED RATHER THAN QUIETLY WIDENED (2026-08-24): four OTHER files already carried their
     * own copy of this class string before this round — `shared/premium-modal.js` and the three Account
     * screens. They are outside this module and outside A3's scope, so they are LISTED here (a change to one
     * of them still trips this test) and filed as BL-215 rather than edited in a WorkCenter round.
     *
     * What this test actually locks is the thing the round decided: the WorkCenter module holds ZERO copies,
     * and the shared component holds exactly one.
     */
    const KNOWN_PRIOR_COPIES = [
      "frontend/Diten.Web/wwwroot/assets/js/Account/forgot-password.js",
      "frontend/Diten.Web/wwwroot/assets/js/Account/login.js",
      "frontend/Diten.Web/wwwroot/assets/js/Account/reset-password.js",
      "frontend/Diten.Web/wwwroot/assets/js/shared/premium-modal.js"
    ];
    expect(hits).toEqual(KNOWN_PRIOR_COPIES.concat([
      "frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml"
    ]).sort());
    expect(hits.filter((f) => f.includes("WorkCenterNext")),
      "the WorkCenter module copied the appearance instead of reading it").toEqual([]);

    // …and it is a NAMED, published value rather than a local one, or nobody outside could read it.
    expect(VIEW).toContain("window.DitenDialogAppearance = function");
    expect(VIEW).toContain("window.DitenDialogAppearance.description");
  });

  it("makes the shared confirm a CONSUMER of the package, not a second author", () => {
    // `showConfirm` must not restate the look; it reads it and names only the confirm button's colour.
    const fn = VIEW.slice(VIEW.indexOf("window.showConfirm = function"));
    expect(fn).toContain("window.DitenDialogAppearance({ confirmVariant: confirmBtnClass");
    expect(fn, "the wrapper is writing its own classes again").not.toContain("cancelButton: 'btn btn-label-secondary");
  });

  it("keeps the neutral dismiss button in the package, where the theme's red default is overruled", () => {
    const pkg = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"),
      VIEW.indexOf("window.DitenDialogAppearance.description"));
    // ⚠ `px-5` dropped with option B — see global-confirm-input-type.test.js. The CLAIM is the neutral tone.
    expect(pkg).toContain("cancelButton: 'btn btn-label-secondary waves-effect'");
    expect(pkg, "the title lost the product's 18px heading size").toContain("title: 'fs-5 fw-bold text-heading");
    expect(pkg, "the popup width drifted from the reference dialog's 400px").toContain("'400px'");
    expect(pkg, "the form-label (13px) stopped reaching the input")
      .toContain("inputLabel: 'form-label d-block w-100 text-start'");
    expect(VIEW, "the description stopped being the product's 13px secondary copy")
      .toContain("window.DitenDialogAppearance.description = 'mb-2 text-muted-500 small'");
  });
});

describe("eight dialogs, four moved and four dressed", () => {
  it("moves the four single-value dialogs into the shared component", () => {
    /*
     * Plan date, meeting time, logged minutes, module choice. Each asks for ONE value, so each is a
     * confirmation, so each belongs to the component that owns what a confirmation looks like.
     */
    ["const openDatePicker", "const openMeetingScheduler", "const openLogTime", "const openCreateInSource"]
      .forEach((name) => {
        const fn = APP.slice(APP.indexOf(name), APP.indexOf(name) + 2600);
        expect(fn.indexOf(name), `${name} vanished`).toBe(0);
        expect(fn, `${name} still opens a raw dialog`).not.toContain("global.Swal.fire({");
        expect(fn, `${name} does not go through the shared confirm`).toContain("sharedConfirm({");
      });
  });

  it("gives the appearance to the four that cannot be confirmations", () => {
    /*
     * MUTATION GUARD #2: drop `dialogLook()` from any one of these and this goes red — that dialog would ship
     * a 38px title and a red dismiss button again.
     *
     * A menu with no confirm button, a four-field meeting form, a two-field reason+assignee form (the one the
     * owner photographed), and a progress readout that asks nothing. None can be a confirmation; all can look
     * like the product.
     */
    const stripped = APP.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
    const raw = stripped.match(/Swal\.fire\(/g) || [];
    const dressed = stripped.match(/dialogLook\(\)/g) || [];
    /*
     * ⚠ THREE, NOT FOUR (2026-08-24): the four-field MEETING FORM was deleted, not restyled — it wrote to
     * `state.meetings` and nowhere else, so everything it produced vanished on reload. What remains is the
     * "+ Yeni" menu, the reason+assignee form, and the bulk progress readout.
     */
    /*
     * ⚠ ONE (2026-08-24, Tur B). Two of the three were DEAD and went with the code they belonged to: the
     * "+ Yeni" Swal menu (unreachable — a Bootstrap dropdown replaced it) and the bulk progress readout (its
     * selection column is drawn nowhere). Both had just been given this appearance, which is dead code at its
     * most convincing. The survivor is the reason+assignee form, which genuinely cannot be a confirmation.
     */
    /*
     * ⚠ TWO (closure slice). The second is the closure OUTCOME PICKER: a select plus a reason box whose label
     * changes with the choice. `showConfirm` supports a textarea and nothing else (BL-146), so this is the same
     * category as the survivor above — a shape the shared wrapper cannot express — and it is DRESSED with the
     * declared package rather than inventing an appearance, which is what this test is actually protecting.
     */
    expect(raw, "a raw dialog appeared or disappeared without this test being told").toHaveLength(2);
    expect(dressed, "a raw dialog is drawing itself again").toHaveLength(2);
    // Each raw call is an `Object.assign(...)`, which is the only shape that can carry the package.
    expect((stripped.match(/Swal\.fire\(Object\.assign\(/g) || []),
      "a raw dialog opened without the appearance").toHaveLength(2);
  });

  it("reads the package instead of copying it", () => {
    expect(APP).toContain("global.DitenDialogAppearance(options)");
    expect(APP, "the module started writing its own dialog classes")
      .not.toContain("rounded-4 shadow-lg");
  });

  it("forwards the select's options — the seventh parameter, and the last", () => {
    expect(APP).toContain("inputOptions: options.input && options.input.options");
    expect(VIEW).toContain("if (options.inputOptions)");
  });

  it("leaves every existing showConfirm caller untouched", () => {
    /*
     * ⚠ THIS TEST USED TO ASSERT `toBe(15)` AND IT WAS WRONG — CORRECTED 2026-08-24.
     *
     * Its regex was `/showConfirm\(/`, which never matches the OPTIONAL-CHAINING form `showConfirm?.(`. That
     * form is how most of this product calls the shared confirm: measured 16 plain against 58 optional, i.e.
     * the census reported 15 while the real surface was 74 calls across 53 files. Every "backwards
     * compatibility measured" claim made against this number covered a fifth of the dialogs it named.
     *
     * ⚠ AND THE FIX IS NOT `toBe(74)`. A hard count breaks on every legitimate new caller, which teaches the
     * next person to bump the number rather than to look — the same reflex that let 15 survive. What is
     * actually being claimed here is a RULE: an opt-in parameter reaches nobody who does not name it. So the
     * rule is what gets asserted, and the count is only reported.
     */
    const callers = sourceFiles().filter((f) => f.endsWith(".js") && CALL.test(fs.readFileSync(f, "utf8")));
    expect(callers.length, "nobody calls the shared confirm — the scan is broken").toBeGreaterThan(20);

    // THE RULE: `inputOptions` is opt-in, so no caller outside this module may be passing it.
    const passing = callers.filter((f) => /inputOptions/.test(fs.readFileSync(f, "utf8")));
    expect(passing.map((f) => path.relative(repoRoot, f)),
      "a caller outside WorkCenterNext started passing inputOptions").toEqual([
      "frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js"
    ]);
  });
});

describe("every box carries a real example", () => {
  /*
   * (A) The owner's rule: a placeholder is an EXAMPLE, never the field's own name repeated.
   *   good  "YYYY-AA-GG"  ·  "örn. 30"  ·  "Denetim izi için zorunlu gerekçeyi yazın…"
   *   bad   "Tarih giriniz"  ·  "Süre"  ·  "Yorum"
   */
  const BOXES = [
    { dialog: "Planla", key: "DatePlaceholder", labelKey: "PlanDateLabel" },
    { dialog: "Toplantı zamanı", key: "DateTimePlaceholder", labelKey: "MeetingWhenLabel" },
    { dialog: "Süre gir", key: "LogTimePlaceholder", labelKey: "LogTimeLabel" },
    { dialog: "Aksiyon onayı — gerekçe", key: "ReasonPlaceholder", labelKey: "ReasonLabel" }
  ];

  it("wires a placeholder into every text, date and number box", () => {
    // MUTATION GUARD #3: delete one `placeholder:` / `placeholder="` and this goes red.
    expect(APP).toContain("placeholder: t('DatePlaceholder')");
    expect(APP).toContain("placeholder: t('DateTimePlaceholder')");
    expect(APP).toContain("placeholder: t('LogTimePlaceholder')");
    // The meeting FORM's two boxes went with the form itself (deleted); its strings stay in the resx for the
    // deferred feature and are checked by the l10n block below, not here.
    expect(APP).toContain(`placeholder="${"$"}{esc(t('ReasonPlaceholder'))}"`);
  });

  it("never repeats the field's own name back at the reader", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      BOXES.forEach(({ dialog, key, labelKey }) => {
        const ph = value(xml, key);
        expect(ph, `${lang}: ${key} is missing`).not.toBeNull();
        expect(String(ph).trim(), `${lang}: ${key} is empty`).not.toBe("");
        expect(String(ph).trim().toLowerCase(),
          `${lang}: ${dialog} repeats its own label as a placeholder`)
          .not.toBe(String(value(xml, labelKey) || "").trim().toLowerCase());
      });
    });
  });

  it("declares the six new strings in all seven languages", () => {
    ["DatePlaceholder", "DateTimePlaceholder", "MeetingWhenLabel", "MeetingTitlePlaceholder",
      "MeetingLocationPlaceholder", "NewPickModuleLabel"].forEach((key) =>
      LANGS.forEach((lang) =>
        expect(String(value(resx(lang), key) || "").trim(), `${lang}/${key}`).not.toBe("")));
  });
});

describe("a field gets a glyph only when the glyph says something", () => {
  /*
   * (B) date → calendar, duration → clock, select → the theme's own caret (already there), free prose → nothing.
   * ⚠ NO WRAPPER, EVER: `.swal2-input` must stay a DIRECT CHILD of the popup or `Swal.getInput()` returns null
   * and the validator, the autofocus and the Enter key go with it. This shipped once.
   */
  it("paints the calendar and the clock ON the box, with no element added", () => {
    expect(APP).toContain("input.classList.add('wcn-date-input')");
    expect(APP).toContain("input.classList.add('wcn-time-input')");
    expect(CSS).toContain(".swal2-input.wcn-time-input");
    expect(CSS).toContain('[data-bs-theme="dark"] .swal2-container .swal2-modal.swal2-popup .swal2-input.wcn-time-input');
    // RTL follows the text to the other edge, same as the date box.
    expect(CSS).toContain('[dir="rtl"] .swal2-container .swal2-modal.swal2-popup .swal2-input.wcn-time-input');
    /*
     * The wrapper that broke it must not come back INSIDE A POPUP. `.diten-field` is right everywhere else in
     * this product, so the check is scoped to the dialog functions rather than to the whole file.
     */
    ["const openDatePicker", "const openMeetingScheduler", "const openLogTime", "const openCreateInSource",
      "const openNew"].forEach((name) => {
      const fn = APP.slice(APP.indexOf(name), APP.indexOf(name) + 2600);
      expect(fn, `${name} put a wrapper around a dialog input`).not.toContain("diten-field");
    });
  });

  it("leaves the select and the prose boxes without one", () => {
    const select = APP.slice(APP.indexOf("const openCreateInSource"), APP.indexOf("const openCreateInSource") + 1800);
    expect(select, "a second arrow was added to a select").not.toContain("classList.add('wcn-");
    const reason = APP.slice(APP.indexOf("wcnReasonText"), APP.indexOf("wcnReasonText") + 400);
    expect(reason, "a glyph was put on a free-text box").not.toContain("wcn-date-input");
  });

  it("takes the glyph from the ONE action dictionary, not from a hand", () => {
    /*
     * ⚠ CORRECTED (2026-08-24): these dialogs used to name `bx-calendar` and `bx-time-five` by hand, which is
     * how one action ended up with two pictures — the rail button drew `bx-user-pin` for "Yeniden ata" while
     * the dialog it opened drew a speech bubble. `inboxActionIcon` is the product's one dictionary and both
     * surfaces read it. Only the SNOOZE keeps a hand-named glyph, and it is not opened by an action.
     */
    expect((APP.match(/icon: inboxActionIcon\(action\)/g) || []).length,
      "an action dialog started choosing its own picture").toBe(3);
    expect(APP, "the snooze moon is not action-driven and stays").toContain("icon: 'bx-moon'");
  });
});

describe("BL-205 — a close button is not a cancel button", () => {
  it("says Close on all four panel dismissers", () => {
    /*
     * MUTATION GUARD #4: turn one back to `ReasonCancel` and this goes red. A screen reader was announcing a
     * CLOSE button as "Vazgeç" on two of the four panels while the other two already said the right word.
     */
    expect(APP, "a panel close button is announced as a cancel").not.toContain("ReasonCancel");
    /*
     * ⚠ TWO, NOT FOUR (2026-08-24, Tur B). The notes and agenda panels were removed — both were permanently
     * empty after the code that fed them was deleted a round earlier. Their close buttons went with them.
     * The two that remain are the subtask edit and subtask create panels.
     */
    expect((APP.match(/aria-label="\$\{esc\(t\('PanelClose'\)\)\}"/g) || []).length,
      "a panel lost its close label").toBe(2);
  });
});

describe("BL-211 — the dependency badges join the product's status vocabulary", () => {
  /*
   * ⚠ THE FIRST DIAGNOSIS WAS WRONG and the owner re-measured it. `DepCancelled` ("İptal edildi") was NEVER the
   * outlier — the other three were, reading "tamam / devam / başlamadı" against the product's own
   * "Tamamlandı / Devam ediyor / Başlamadı".
   */
  const PAIRS = [["DepDone", "SubtaskStatusDone"], ["DepInProgress", "SubtaskStatusInProgress"],
    ["DepNotStarted", "SubtaskStatusNotStarted"]];

  it("says exactly what the rest of the product says, in all seven languages", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      PAIRS.forEach(([dep, product]) =>
        expect(value(xml, dep), `${lang}: ${dep} still speaks its own dialect`).toBe(value(xml, product)));
    });
  });

  it("leaves DepCancelled alone — it was right from the start", () => {
    expect(value(resx("tr"), "DepCancelled")).toBe("İptal edildi");
    expect(value(resx("en"), "DepCancelled")).not.toBe("");
    // The vocabulary maps themselves did not move; only the words behind three keys did.
    expect(APP).toContain("cancelled: 'DepCancelled'");
    expect(APP).toContain("cancelled: 'secondary'");
  });
});
