const fs = require("fs");
const path = require("path");

/*
 * THE SHARED CONFIRM'S INPUT TYPE — a constant became a parameter, and nothing else moved.
 *
 * `_GlobalConfirmation.cshtml` is the ONE confirm dialog in this product; every module's "are you sure" goes
 * through it. It offered an input box and hard-coded that box to be a `textarea`, which is why every dialog that
 * needed a date, a number or a choice stayed a raw `Swal.fire` with none of the wrapper's title, description,
 * width or validation (BL-146).
 *
 * The change is one line: `options.inputType || 'textarea'`. No capability was added — the type this block
 * always wrote is now the type a caller may name, and the default is exactly today's behaviour.
 *
 * <b>HOW THIS IS TESTED.</b> The wrapper's script is EXTRACTED from the Razor view and executed against a Swal
 * stub, so the assertions below are made against the function the browser actually runs — not against a regex on
 * its source. The Razor expressions (`@Json.Serialize(...)`) are the only thing replaced, with the strings they
 * would render. Everything else is the shipped code.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const VIEW = fs.readFileSync(
  path.join(repoRoot, "frontend", "Diten.Web", "Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");

/** The view's script body, with Razor's localizer calls resolved to plain strings. */
const loadWrapper = (swalStub) => {
  /*
   * ⚠ THE SLICE STARTS AT THE APPEARANCE PACKAGE NOW (2026-08-24, A3). `showConfirm` no longer declares what a
   * dialog looks like — it READS `window.DitenDialogAppearance`, which is declared just above it in the same
   * script. Slicing from `window.showConfirm` left that declaration out and the wrapper called undefined.
   */
  const script = VIEW.slice(VIEW.indexOf("window.DitenDialogAppearance = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const sandbox = { window: {}, Swal: swalStub, console, confirm: () => false };
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(sandbox.window, swalStub, console, sandbox.confirm);
  return sandbox.window.showConfirm;
};

const captureConfig = () => {
  const seen = {};
  const stub = { fire: (config) => { seen.config = config; return { then: () => {} }; }, showValidationMessage: () => {} };
  return { seen, stub };
};

describe("the callers that were already using the input box", () => {
  /*
   * MEASURED: six call sites across four modules pass `showInput` today —
   *   Platform/Tenants/details.js  ×2      Platform/Tenants/index.js      ×1
   *   Platform/AuditLog/index.js   ×1      DocumentManagement/TemplateMasters/index.js ×1
   *   WorkCenterNext/app.js        ×1  (the module seam, which forwards whatever its caller asked for)
   * Not one of them names a type, so not one of them may change. This file is the whole product's shared
   * component: one module's round must not move another module's dialog.
   */
  it("is still six, and none of them names a type", () => {
    const roots = [path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js")];
    const files = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== "vendor") { walk(p); } }
      else if (e.name.endsWith(".js")) { files.push(p); }
    });
    roots.forEach(walk);

    const callers = files.filter((f) => /showInput\s*:/.test(fs.readFileSync(f, "utf8")));
    const occurrences = callers.reduce((n, f) =>
      n + (fs.readFileSync(f, "utf8").match(/showInput\s*:/g) || []).length, 0);
    expect(occurrences).toBe(6);

    // The one file that DOES pass a type is the WorkCenterNext seam, and it passes whatever its own caller said —
    // `undefined` for every prose dialog, which is what keeps them on the default.
    const namingAType = callers.filter((f) => /inputType\s*:/.test(fs.readFileSync(f, "utf8")));
    expect(namingAType.map((f) => path.basename(f))).toEqual(["app.js"]);
  });

  it("still gets a textarea when it asks for an input without naming a type", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Suspend", () => {}, { showInput: true, inputPlaceholder: "why?" });
    // MUTATION GUARD: drop the `|| 'textarea'` default and this is `undefined` — every existing caller loses
    // its box, in four modules that were not part of this round.
    expect(seen.config.input).toBe("textarea");
    expect(seen.config.inputAttributes.rows).toBe(3);
  });

  it("keeps the label, the validator and the required check exactly as they were", () => {
    const { seen, stub } = captureConfig();
    let validated = null;
    loadWrapper(stub)("Suspend", () => {}, {
      showInput: true, inputLabel: "Reason", inputRequired: true,
      inputValidator: (v) => { validated = v; return v === "bad" ? "no" : null; }
    });
    expect(seen.config.inputLabel).toBe("Reason");
    expect(typeof seen.config.preConfirm).toBe("function");
    seen.config.preConfirm("ok");
    expect(validated).toBe("ok");
    expect(seen.config.preConfirm("bad")).toBe(false);
  });
});

describe("a caller that does name a type", () => {
  it("gets that type instead", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputType: "text" });
    expect(seen.config.input).toBe("text");
  });

  it("keeps everything else the wrapper already gave it", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {
      showInput: true, inputType: "text", subtext: "what this does", entityName: "Q3",
      confirmButtonText: "Ertele", cancelButtonText: "Vazgeç", width: "400px"
    });
    expect(seen.config.html).toContain("what this does");
    expect(seen.config.html).toContain("Q3");
    expect(seen.config.confirmButtonText).toBe("Ertele");
    expect(seen.config.cancelButtonText).toBe("Vazgeç");
    expect(seen.config.showCancelButton).toBe(true);
  });

  it("reaches the caller's didOpen with the popup", () => {
    const { seen, stub } = captureConfig();
    let opened = null;
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputType: "text", didOpen: (p) => { opened = p; } });
    seen.config.didOpen({ tag: "popup" });
    expect(opened).toEqual({ tag: "popup" });
  });
});


describe("the dialog is written in the product's type scale", () => {
  /*
   * MEASURED, on a rendered page, before any change: the description printed at 18px, the input label at 16px
   * and the title at 24px. The product itself writes body copy at 13px and secondary copy at 12px — a census of
   * the detail page's 134 text nodes put 41 at 13px and 46 at 12px, with the page's own title at 18px.
   *
   * Nothing here invents a size. Each value below is a class the theme already ships:
   *   fs-5   → 18px, the theme's own h5 and the size a page titles itself with
   *   small  → 13px, the same size `.form-label` and `.backbone-preview-description` already use
   *   form-label → the product's form label, on every create form in the product
   *
   * ⚠ THIS FILE IS EVERY MODAL IN THE PRODUCT, which is why these are pinned here rather than in a module test.
   */
  it("titles at the product's heading size, not a size of its own", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {});
    expect(seen.config.customClass.title).toContain("fs-5");
    expect(seen.config.customClass.title, "24px — louder than the page behind it").not.toContain("fs-4");
  });

  it("writes its description at the product's body size", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { subtext: "what this does" });
    // MUTATION GUARD: drop `small` and the description goes back to the browser default of 18px — bigger than
    // the page title it opens over, on every modal in the product.
    expect(seen.config.html).toMatch(/class="[^"]*\bsmall\b[^"]*"/);
    expect(seen.config.html).toContain("what this does");
  });

  it("labels its input the way every form in the product labels a field", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputLabel: "Until" });
    /*
     * ⚠ `d-block text-start` JOINED IT (2026-08-24). A label belongs to its FIELD, not to the dialog: the popup
     * centres everything, so four dialogs printed a centred label above a full-width box. The two classes are
     * the ones the hand-written labels in this module already used.
     */
    expect(seen.config.customClass.inputLabel).toBe("form-label d-block w-100 text-start");
  });

  it("keeps the buttons neutral-cancel and the width fixed", () => {
    /*
     * ⚠ `px-5` IS GONE (2026-08-24, option B). It padded each button by 3rem so two of them filled the middle
     * of the popup; the reference the owner named — the create-task offcanvas footer — measured the theme's own
     * 20px inset and pushed dismiss and commit to opposite ends instead. The buttons now carry no extra inset.
     *
     * What this test actually protects is unchanged and still here: the dismiss button stays NEUTRAL (the
     * theme's global default for `.swal2-cancel` is `btn-label-danger`, so silence ships a red "Vazgeç"), and
     * the popup keeps one width.
     */
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {});
    expect(seen.config.customClass.confirmButton, "the button grew its own inset again").not.toContain("px-5");
    expect(seen.config.customClass.cancelButton).toContain("btn-label-secondary");
    expect(seen.config.customClass.cancelButton, "the button grew its own inset again").not.toContain("px-5");
    expect(seen.config.customClass.actions, "the buttons went back to the middle").toContain("justify-content-between");
    expect(seen.config.width).toBe("400px");
  });
});


/*
 * ⚠ THE ICON MOVED FROM `iconHtml` INTO `title` (2026-08-24, owner's option B).
 *
 * SweetAlert lays the popup out as a GRID with one slot per row — icon, title, html, actions. Option B puts
 * the 32px circle ON the title's line, and doing that by overriding the grid is the manoeuvre that has broken
 * twice in this file's history. Composing both into ONE slot needs no grid surgery, so `title` now carries
 * `iconHtml + '<span>' + title + '</span>'` and the icon slot is left empty.
 *
 * Every assertion below therefore reads `config.title` where it used to read `config.iconHtml`. WHAT IS BEING
 * CLAIMED IS UNCHANGED: the type owns the circle and its colour, the caller may name only the glyph.
 */
describe("the icon", () => {
  /*
   * A confirmation's icon carries WEIGHT: the red bin means "cannot be undone", the amber exclamation means
   * "careful". A dialog with none of that weight is misdescribed by every one of them, and the neutral default
   * is a question mark — which asks "are you sure?" over a dialog whose question is "until when?".
   *
   * ⚠ Measured on a live dialog before the flag existed: the icon carries no `aria-label`, no `role` and no
   * text, and the dialog is named by `aria-labelledby="swal2-title"`. Hiding it costs a screen reader nothing.
   */
  it("is drawn for every caller that does not ask otherwise", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("DeleteConfirmation", () => {}, { entityName: "Bir kayıt" });
    // MUTATION GUARD: make hiding the default and every dialog in four other modules loses its icon.
    expect(seen.config.title).toContain("swal-icon-circle");
    expect(seen.config.title).toContain("bx-trash");
  });

  it("is left out only when a caller asks", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { hideIcon: true });
    // `hideIcon` now means "a title with no glyph", not "an empty icon slot".
    expect(seen.config.title, "hideIcon still drew a circle").not.toContain("swal-icon-circle");
    expect(seen.config.iconHtml, "the icon slot must stay empty — the picture is in the title").toBeUndefined();
  });

  it("does not take the button colour with it", () => {
    /*
     * The glyph and `confirmBtnClass` are decided by ONE if-chain. The flag reads that chain's result and drops
     * only the picture — a destructive dialog that hides its icon is still a destructive dialog.
     */
    const shown = captureConfig();
    loadWrapper(shown.stub)("DeleteConfirmation", () => {}, {});
    const hidden = captureConfig();
    loadWrapper(hidden.stub)("DeleteConfirmation", () => {}, { hideIcon: true });
    expect(hidden.seen.config.customClass.confirmButton).toBe(shown.seen.config.customClass.confirmButton);
    expect(hidden.seen.config.customClass.confirmButton).toContain("btn-danger");

    const info = captureConfig();
    loadWrapper(info.stub)("Snooze", () => {}, { hideIcon: true });
    expect(info.seen.config.customClass.confirmButton).toContain("btn-primary");
  });
});


describe("the glyph, and only the glyph", () => {
  /*
   * ⚠ A CORRECTION. The previous round reported that the icon and the confirm button's colour "are decided
   * together in one chain, so opening the glyph means opening the colour too". That was wrong, and it was not
   * measured: `confirmBtnClass` and the icon markup were always two separate variables. One parameter is enough,
   * and the colour never leaves the type's hands.
   */
  it("can be named by a caller", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { icon: "bx-moon" });
    expect(seen.config.title).toContain("bx-moon");
    expect(seen.config.title, "the old glyph is still in there").not.toContain("bx-help-circle");
  });

  it("keeps the circle, its colour and its size — those belong to the type", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { icon: "bx-moon" });
    // The same circle the info type draws: same classes, so same background, border and 80px.
    expect(seen.config.title).toContain("swal-icon-circle bg-label-primary border-primary border-opacity-25");
    expect(seen.config.title).toContain("text-primary");
  });

  it("does not touch the confirm button's colour, whatever glyph is asked for", () => {
    const plain = captureConfig();
    loadWrapper(plain.stub)("DeleteConfirmation", () => {}, {});
    const swapped = captureConfig();
    loadWrapper(swapped.stub)("DeleteConfirmation", () => {}, { icon: "bx-moon" });
    expect(swapped.seen.config.customClass.confirmButton).toBe(plain.seen.config.customClass.confirmButton);
    expect(swapped.seen.config.customClass.confirmButton).toContain("btn-danger");
    // …and the destructive circle stays destructive: a moon on a delete dialog is still a delete dialog.
    expect(swapped.seen.config.title).toContain("text-danger");
  });

  it("leaves every existing caller with the glyph its type gives it", () => {
    // MUTATION GUARD: make the parameter's default anything but "the type's glyph" and this goes red for the
    // four modules that never asked for one.
    [["DeleteConfirmation", "bx-trash"], ["AreYouSure", "bx-help-circle"]].forEach(([key, glyph]) => {
      const { seen, stub } = captureConfig();
      loadWrapper(stub)(key, () => {}, {});
      expect(seen.config.title, `${key} lost its glyph`).toContain(glyph);
    });
    const warn = captureConfig();
    loadWrapper(warn.stub)("AreYouSure", () => {}, { type: "warning" });
    expect(warn.seen.config.title).toContain("bx-error");
    expect(warn.seen.config.title).toContain("text-warning");
  });
});
