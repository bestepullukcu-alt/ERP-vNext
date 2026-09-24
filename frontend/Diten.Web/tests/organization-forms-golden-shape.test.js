const fs = require("fs");
const path = require("path");

/*
 * THE ORGANIZATION MODULE'S FOUR FORMS LOOK LIKE THE REFERENCE FORM — owner report, 2026-09-21.
 *
 * Two defects, reported together because they were seen together while entering the first field definition:
 *
 *   (A) NO FIELD ICONS. `GoldenReferenceCompact/_Form.cshtml` is the shape every full-page form in this product
 *       copies: label, then the control inside `<div class="diten-field">` with a `.diten-field-icon` in front
 *       of it. MEASURED before this round: the four Organization forms carried ZERO of them.
 *
 *   (B) THE "KALDIR" BUTTON ON AN ADDED OPTION ROW WAS EMPTY. `form.js` builds each new row's button with
 *       `L.OptionRemove || ''`, and the create/edit pages published NO l10n payload at all — so the dictionary
 *       was empty, the button had no text, and the added rows' placeholder was missing too. The details page in
 *       the same folder already loaded the pair of lines that fixes it.
 *
 * Both guards are written against the real views, and (A) also checks each glyph EXISTS: BL-430 recorded that a
 * boxicons class with no rule paints a solid square, which looks like a design decision rather than a typo.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const FORMS = [
  ["FieldDefinitions", "_Form.cshtml"],
  ["OrganizationUnits", "Form.cshtml"],
  ["Positions", "Form.cshtml"],
  ["PositionAssignments", "Form.cshtml"]
];

/** Razor comments removed: a note ABOUT a control is not a control. */
const formSource = (folder, file) => read("Views", "Organization", folder, file).replace(/@\*[\s\S]*?\*@/g, "");

/**
 * Every control a user types into. Hidden inputs carry no label and switches are their own shape in the
 * reference form, so neither is wrapped there and neither is required here.
 */
const visibleControls = (source) =>
  (source.match(/<(?:input|select|textarea)\b[^>]*>/g) || [])
    .filter((tag) => !/type="hidden"/.test(tag))
    .filter((tag) => !/form-check-input/.test(tag));

describe("(A) every control sits in the reference form's field wrapper", () => {
  FORMS.forEach(([folder, file]) => {
    test(`${folder}/${file}`, () => {
      const source = formSource(folder, file);
      const controls = visibleControls(source);
      expect(controls.length, "the form has no controls — did the scan break?").toBeGreaterThan(5);

      // Each control's opening tag must be preceded by the wrapper and its icon, with nothing between them
      // but whitespace. A wrapper somewhere else in the file would not help the control the user sees.
      const unwrapped = controls.filter((tag) => {
        const at = source.indexOf(tag);
        const before = source.slice(Math.max(0, at - 220), at);
        /*
         * A control inside an `.input-group` is excluded, and it is the one honest exception: the group owns
         * the row's shape (the box and the button share one border and one radius), and nesting the absolute
         * icon wrapper inside it breaks both. The reference form has no repeater row, so it decides nothing
         * here — the option editor's own button is what distinguishes that row.
         */
        if (/<div class="input-group[^"]*"[^>]*>\s*$/.test(before)) { return false; }
        return !/<div class="diten-field">\s*<i class="bx [a-z0-9-]+ diten-field-icon[^"]*" aria-hidden="true"><\/i>\s*$/.test(before);
      });
      expect(unwrapped.map((t) => t.slice(0, 60)), "these controls have no icon wrapper").toEqual([]);
    });
  });

  test("a textarea's icon rides the top, as in the reference form", () => {
    FORMS.forEach(([folder, file]) => {
      const source = formSource(folder, file);
      (source.match(/<div class="diten-field">[\s\S]{0,200}?<textarea/g) || []).forEach((block) => {
        expect(block, `${folder}: a multi-line box centres its icon against five rows of text`)
          .toContain("diten-field-icon--top");
      });
    });
  });

  test("every glyph used exists in the icon font (BL-430: a missing one paints a solid square)", () => {
    const iconCss = read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");
    const used = new Set();
    FORMS.forEach(([folder, file]) =>
      (formSource(folder, file).match(/class="bx (bx-[a-z0-9-]+) diten-field-icon/g) || [])
        .forEach((m) => used.add(m.replace(/class="bx (bx-[a-z0-9-]+).*/, "$1"))));
    expect(used.size, "no icons were found to check").toBeGreaterThan(8);
    const missing = [...used].filter((icon) => !new RegExp(`\\.${icon}\\b`).test(iconCss));
    expect(missing, "these glyphs would render as a filled square").toEqual([]);
  });
});

describe("(B) the field-definition form's own strings reach the browser", () => {
  const formJs = () => read("wwwroot", "assets", "js", "Organization", "FieldDefinitions", "form.js");
  /** Read the requirement off the script itself, so a new `L.Something` is covered the day it is written. */
  const keysTheScriptNeeds = () =>
    [...new Set((formJs().match(/\bL\.[A-Za-z]+/g) || []).map((k) => k.slice(2)))];

  test("the script's whole dictionary is published by the bridge it is given", () => {
    const bridge = read("Views", "Organization", "FieldDefinitions", "_IndexL10n.cshtml");
    const missing = keysTheScriptNeeds().filter((key) => !new RegExp(`\\b${key} = `).test(bridge));
    expect(missing, "form.js reads these, the bridge publishes none of them").toEqual([]);
  });

  ["Create.cshtml", "Edit.cshtml"].forEach((page) => {
    test(`${page} loads the bridge AND the loader, before form.js`, () => {
      const source = read("Views", "Organization", "FieldDefinitions", page);
      const bridge = source.indexOf("_IndexL10n.cshtml");
      const loader = source.indexOf("FieldDefinitions/index.l10n.js");
      const script = source.indexOf("FieldDefinitions/form.js");
      expect(bridge, "no l10n payload on this page — every string form.js writes comes out empty").toBeGreaterThan(-1);
      expect(loader, "the payload is published but nothing reads it into window.L10n").toBeGreaterThan(bridge);
      expect(script, "form.js runs before its dictionary exists").toBeGreaterThan(loader);
    });
  });

  test("the loader warns when the two option strings go missing", () => {
    const loader = read("wwwroot", "assets", "js", "Organization", "FieldDefinitions", "index.l10n.js");
    ["OptionRemove", "OptionPlaceholder"].forEach((key) =>
      expect(loader, `${key} is not in the required list, so its absence would be silent`).toContain(`'${key}'`));
  });
});
