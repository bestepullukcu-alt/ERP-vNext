const fs = require("fs");
const path = require("path");

/*
 * A2 — SEVEN DEFECTS THE OWNER PHOTOGRAPHED, THREE ROOT CAUSES, TWO DEAD ENDS (2026-08-24).
 *
 *   1·3·4·5  four labels floated in the middle of the popup   → ONE line in the appearance package
 *   6        "Devam etmek istediğinize emin misiniz?"          → the shared default reaching input prompts
 *   2·6·7    a missing icon and a meaningless one              → the icon was built INSIDE `showConfirm`
 *
 * And two of the eight dialogs were not defects to fix but features that never existed: the quick note and the
 * meeting form wrote to browser memory and nowhere else.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (xml, key) => {
  const at = xml.indexOf(`name="${key}"`);
  return at < 0 ? null : xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
};

/** The wrapper's script, executed against a Swal stub — the function the browser actually runs. */
const loadWrapper = (stub) => {
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const sandbox = { window: {} };
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(sandbox.window, stub, console, () => false);
  return sandbox.window;
};
const capture = () => {
  const seen = {};
  return { seen, stub: { fire: (config) => { seen.config = config; return { then: () => {} }; }, showValidationMessage: () => {} } };
};

describe("a label belongs to its field, not to the dialog", () => {
  it("left-aligns every generated label, in one line, for all of them at once", () => {
    /*
     * MUTATION GUARD #1: drop `text-start` and this goes red. Four of the owner's seven reports — "Bir tarih
     * seçin", "Dakika", "Toplantı tarihi ve saati", "Hangi modül?" — are this single class, because all four
     * labels come from the same `inputLabel` slot.
     */
    const { stub, seen } = capture();
    const w = loadWrapper(stub);
    w.showConfirm("Süre gir", () => {}, { showInput: true, inputLabel: "Dakika" });
    expect(seen.config.customClass.inputLabel).toBe("form-label d-block w-100 text-start");
    expect(seen.config.customClass.inputLabel, "the label is not left-aligned").toContain("text-start");
  });

  it("keeps the title and the description centred — they address the dialog, not the box", () => {
    const { stub, seen } = capture();
    loadWrapper(stub).showConfirm("Süre gir", () => {}, { showInput: true, inputLabel: "Dakika" });
    expect(seen.config.customClass.title).toContain("text-center");
    expect(seen.config.customClass.htmlContainer).toContain("text-center");
  });

  it("makes the hand-written DIALOG labels and the generated one the same two classes", () => {
    /*
     * Scoped to the DIALOG, deliberately. The offcanvas panels also write `class="form-label"` and are right to:
     * a panel is left-aligned already, so `text-start` there would be a class that changes nothing. What must
     * not differ is the labels that sit in ONE POPUP side by side — the generated one and the hand-written ones
     * in the reason+assignee dialog, where a difference is visible in a single glance.
     */
    const dialog = APP.slice(APP.indexOf("const assigneeField"), APP.indexOf("const assigneeField") + 2200);
    const handWritten = dialog.match(/class="form-label[^"]*"/g) || [];
    expect(handWritten.length, "the dialog stopped writing labels by hand").toBeGreaterThan(1);
    handWritten.forEach((cls) =>
      expect(cls, `a hand-written dialog label drifted from the package: ${cls}`).toContain("text-start"));
  });
});

describe("an input prompt is not a confirmation", () => {
  it("prints no generic sentence when a dialog deliberately has none", () => {
    /*
     * MUTATION GUARD #2: put the default back (`options.subtext || default`) and this goes red. "Devam etmek
     * istediğinize emin misiniz?" was printed over "Kaç dakika?", "Ne zaman?" and "Hangi modül?".
     */
    const { stub, seen } = capture();
    loadWrapper(stub).showConfirm("Süre gir", () => {}, { showInput: true, subtext: "" });
    expect(seen.config.html, "the generic confirmation sentence survived").not.toContain("ConfirmAction");
    expect(seen.config.html.trim()).toBe("");
  });

  it("still gives a REAL confirmation its default sentence", () => {
    // Delete / cancel / irreversible: unchanged, and this is what keeps the other eleven modules still.
    const { stub, seen } = capture();
    loadWrapper(stub).showConfirm("AreYouSure", () => {}, { entityName: "Bir kayıt", type: "danger" });
    expect(seen.config.html).toContain("ConfirmAction");
  });

  it("routes the module's input prompts to silence and its confirmations to the default", () => {
    expect(APP).toContain("subtext: options.subtext !== undefined ? options.subtext : (options.input ? '' : undefined)");
  });

  it("gives the three named dialogs a sentence that says what the box cannot", () => {
    ["LogTimeSubtext", "MeetingWhenSubtext", "NewInSourceSubtext"].forEach((key) => {
      expect(APP, `${key} is not wired`).toContain(`t('${key}')`);
      LANGS.forEach((lang) => {
        const v = String(value(resx(lang), key) || "").trim();
        expect(v, `${lang}/${key} is missing`).not.toBe("");
        /*
         * It must say something, and something OTHER than the field's own label. A character-count floor was
         * tried and rejected: 17 characters of Chinese carry the whole sentence, so the threshold would have
         * measured the writing system rather than the writing.
         */
        expect(v, `${lang}/${key} just repeats a label`).not.toBe(value(resx(lang), "LogTimeLabel"));
        expect(v, `${lang}/${key} just repeats a label`).not.toBe(value(resx(lang), "MeetingWhenLabel"));
        expect(v, `${lang}/${key} just repeats a label`).not.toBe(value(resx(lang), "NewPickModuleLabel"));
      });
    });
  });
});

describe("the icon is a thing a raw dialog can have too", () => {
  it("publishes the builder, so there is one circle and not two", () => {
    const w = loadWrapper(capture().stub);
    expect(typeof w.DitenDialogAppearance.iconHtml).toBe("function");
    // The type still owns the circle and its tint…
    expect(w.DitenDialogAppearance.iconHtml("danger")).toContain("bg-label-danger");
    expect(w.DitenDialogAppearance.iconHtml("delete")).toContain("bx-trash");
    // …and only the GLYPH may be named by a caller.
    const named = w.DitenDialogAppearance.iconHtml("info", "bx-conversation");
    expect(named).toContain("bx-conversation");
    expect(named, "naming a glyph changed the circle").toContain("bg-label-primary");
  });

  it("gives the reason+assignee dialog the SAME glyph its own button carries", () => {
    expect(APP).toContain("iconHtml: dialogIcon('info', inboxActionIcon(action))");
    expect(APP, "the module built its own circle").not.toContain("swal-icon-circle");
  });

  it("uses the parameter that already existed rather than opening a new one", () => {
    // `options.icon` was opened for the snooze moon; every caller names its glyph through it.
    expect(APP).toContain("icon: inboxActionIcon(action)");
    expect(APP).toContain("icon: 'bx-cube'");   // not action-driven: the "+ Yeni" module picker
    expect(APP).toContain("icon: 'bx-moon'");   // not action-driven: the snooze
  });
});

describe("two dialog selects become the product's own picker", () => {
  it("binds both through one binder, with the list parented INTO the popup", () => {
    /*
     * MUTATION GUARD #3: remove a `bindDialogSelect2` call and this goes red.
     *
     * ⚠⚠ `dropdownParent` IS THE Z-INDEX FIX, and it is a structural one. flatpickr's calendar shipped BEHIND
     * this same dialog earlier in this session (1074 against 1090) and every click reached the page behind it.
     * A descendant cannot be behind its ancestor, so the question is removed rather than answered with a number.
     */
    expect(APP).toContain("const bindDialogSelect2 =");
    // Three CALL SITES: the module picker, the assignee picker, the waiting-on picker. (The declaration reads
    // `const bindDialogSelect2 = (` and is not one of them.)
    expect((APP.match(/bindDialogSelect2\(/g) || []).length,
      "a dialog select lost its picker").toBe(3);
    const fn = APP.slice(APP.indexOf("const bindDialogSelect2 ="), APP.indexOf("const bindDialogSelect2 =") + 1400);
    expect(fn, "the list is parented to the body again — it will open behind the dialog")
      .toContain("dropdownParent");
    expect(fn).toContain("closest('.swal2-popup')");
    // The same configuration the filter panel uses; no second options object was invented.
    expect(fn).toContain("selectionCssClass: 'form-select'");
  });

  it("never wraps the dialog's own input slot", () => {
    // select2 hides the original in place and inserts its container as a SIBLING, so `Swal.getInput()` still
    // finds `.swal2-select`. Asserted live in the browser too — this pins the intent in code.
    const fn = APP.slice(APP.indexOf("const bindDialogSelect2 ="), APP.indexOf("const bindDialogSelect2 =") + 1400);
    expect(fn).not.toContain("wrap(");
    expect(fn).not.toContain("diten-field");
  });
});

describe("two dead ends were removed, not disabled", () => {
  it("drops the quick note and the meeting form entirely", () => {
    /*
     * MUTATION GUARD #4: put either menu entry back and this goes red.
     *
     * MEASURED: `state.notes.unshift(...)` and `state.meetings.push(...)`, no API call anywhere, and `state`
     * starts both as `[]` and never loads them. Everything either produced vanished on the next reload.
     * DISABLING was rejected: a disabled control with no reason is the defect this session filed as BL-208,
     * and there is no answer to "when, then?" to put beside it.
     */
    expect(APP, "the quick-note dialog came back").not.toContain("const openQuickNote");
    expect(APP, "the meeting form came back").not.toContain("const openMeetingForm");
    expect(APP, "the menu entry came back").not.toContain("createItem('note'");
    expect(APP, "the menu entry came back").not.toContain("createItem('meeting'");
    expect(APP, "the dispatch still routes to a deleted dialog").not.toContain("kind === 'note'");
    expect(APP, "the dispatch still routes to a deleted dialog").not.toContain("kind === 'meeting'");
    // The agenda panel's "+" was a SECOND door onto the same deleted form.
    expect(APP, "a control still calls the deleted form").not.toContain("data-wcn-meeting-add");
  });

  it("leaves the two REAL things alone", () => {
    // The detail page's personal note writes through the engine…
    expect(APP, "the real personal note was deleted by mistake").toContain("TasksApi.addPersonalNote");
    // …and the review-meeting ACTION has a contract behind it.
    expect(APP).toContain("const openMeetingScheduler");
    expect(APP).toContain("applyReviewMeeting");
  });

  it("keeps the two menu entries that DO something", () => {
    expect(APP).toContain("createItem('task'");
    expect(APP).toContain("createItem('source'");
  });
});
