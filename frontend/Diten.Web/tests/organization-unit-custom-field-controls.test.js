const fs = require("fs");
const path = require("path");

/*
 * ══ MOD-0288-FU03 — CUSTOM FIELD CONTROLS LOOK LIKE THE REST OF THE FORM ═════════════════════════════════
 *
 * WHY THIS FILE EXISTS. The owner opened the unit form and saw it: the Custom fields section showed empty
 * boxes. Three defects, and every one of them was green.
 *
 *   1. `select2` appeared ZERO times in organization-unit-custom-fields.js. The binding lives in
 *      `form.js:initSelect2` and walks a FIXED ID LIST, which dynamic ids never join — and it runs while the
 *      page loads, before the definitions have even been fetched.
 *   2. The empty first option of every generated select had `textContent = ''`. That is not cosmetic:
 *      `form.js:initSelect2` reads the empty option's text AS THE SELECT2 PLACEHOLDER, so an empty string
 *      there leaves the control blank whether or not select2 is bound.
 *   3. The text/number/date/textarea branch set no `placeholder` at all, while every fixed field on the same
 *      form carries one.
 *
 * The unit tests, the resx parity test and 183 green .NET tests saw none of it. A person looking at the
 * screen saw all three in a second. These assertions are what would have gone red.
 *
 * ⚠ THEY READ THE SHIPPING FILE. The rule is not re-implemented here — a test that measures its own copy of a
 * rule proves only that the copy works, which is how this repository has been bitten before.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (f) => fs.readFileSync(f, "utf8");

const CUSTOM_FIELDS_JS = web("wwwroot", "assets", "js", "Organization", "organization-unit-custom-fields.js");
const FORM_JS = web("wwwroot", "assets", "js", "Organization", "OrganizationUnits", "form.js");
const FORM_L10N = web("Views", "Organization", "OrganizationUnits", "_FormL10n.cshtml");

/*
 * Comments explain these rules, so a naive scan finds the forbidden shapes inside the prose that forbids
 * them. Every assertion below reads code only.
 */
const codeOnly = (file) =>
  read(file)
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .filter((line) => !line.trim().startsWith("//"))
    .join("\n");

describe("custom field selects offer a readable empty state", () => {
  test("no branch of buildControl creates an option with empty text", () => {
    /*
     * ⚠ SCOPED TO buildControl, not the whole file. `showCustomFieldErrors` legitimately clears feedback text
     * with `el.textContent = ''`, and a file-wide scan flagged it — a guard that fires on innocent code gets
     * loosened until it fires on nothing.
     */
    const source = codeOnly(CUSTOM_FIELDS_JS);
    const build = source.match(/const buildControl\s*=\s*\(definition,\s*kind,\s*options\)\s*=>\s*\{([\s\S]*?)\n        control\.id\s*=/);
    expect(build).not.toBeNull();

    /*
     * The exact shape the defect had, in all three select branches:
     *     blank.textContent = '';
     * and the boolean branch's tuple form:
     *     [['', ''], ...]
     */
    expect(build[1]).not.toMatch(/\.textContent\s*=\s*(''|"")\s*;/);
    expect(build[1]).not.toMatch(/\[\s*(''|"")\s*,\s*(''|"")\s*\]/);
  });

  test("the empty option's text comes from the localized placeholder", () => {
    const source = codeOnly(CUSTOM_FIELDS_JS);

    // One helper builds it, so the three select branches cannot drift apart again.
    const helper = source.match(/const blankOption\s*=\s*\(options\)\s*=>\s*\{([\s\S]*?)\n    \};/);
    expect(helper).not.toBeNull();
    expect(helper[1]).toMatch(/textContent\s*=\s*options\.selectPlaceholder/);

    // And every select branch uses it rather than hand-rolling its own blank.
    const uses = source.match(/appendChild\(blankOption\(options\)\)/g) || [];
    expect(uses.length).toBe(3); // boolean, select, reference
  });

  test("free-text controls carry a type-derived placeholder", () => {
    const source = codeOnly(CUSTOM_FIELDS_JS);

    const helper = source.match(/const placeholderFor\s*=\s*\(kind,\s*options\)\s*=>\s*\{([\s\S]*?)\n    \};/);
    expect(helper).not.toBeNull();
    expect(helper[1]).toMatch(/numberPlaceholder/);
    expect(helper[1]).toMatch(/datePlaceholder/);
    expect(helper[1]).toMatch(/textPlaceholder/);

    // Both non-select branches set it: the textarea and the input.
    const assignments = source.match(/\.placeholder\s*=\s*placeholderFor\(kind,\s*options\)/g) || [];
    expect(assignments.length).toBe(2);
  });

  test("the placeholders reach the module through the channel that already existed", () => {
    // ⚠ No second l10n transport. `booleanYes`/`booleanNo` already travel _FormL10n → form.js → options,
    // and these ride the same road.
    const l10n = read(FORM_L10N);
    ["CustomFieldSelectPlaceholder", "CustomFieldTextPlaceholder",
     "CustomFieldNumberPlaceholder", "CustomFieldDatePlaceholder"].forEach((key) => {
      expect(l10n).toMatch(new RegExp(`${key}\\s*=\\s*Localizer\\["${key}"\\]`));
    });

    const form = codeOnly(FORM_JS);
    const options = form.match(/renderCustomFields\(host,\s*definitions,\s*\{([\s\S]*?)\}\);/);
    expect(options).not.toBeNull();
    expect(options[1]).toMatch(/selectPlaceholder:\s*L\.CustomFieldSelectPlaceholder/);
    expect(options[1]).toMatch(/textPlaceholder:\s*L\.CustomFieldTextPlaceholder/);
    expect(options[1]).toMatch(/numberPlaceholder:\s*L\.CustomFieldNumberPlaceholder/);
    expect(options[1]).toMatch(/datePlaceholder:\s*L\.CustomFieldDatePlaceholder/);
  });
});

describe("custom field selects are bound to select2, like the rest of the form", () => {
  test("the module binds select2 itself", () => {
    // Measured before the fix: the word `select2` appeared ZERO times in this file.
    const source = codeOnly(CUSTOM_FIELDS_JS);
    expect(source).toMatch(/select2/);

    const enhancer = source.match(/const enhanceSelects\s*=\s*\(container\)\s*=>\s*\{([\s\S]*?)\n    \};/);
    expect(enhancer).not.toBeNull();
    expect(enhancer[1]).toMatch(/select\[data-ou-custom-field\]/);
  });

  test("the binding runs AFTER the controls are in the DOM", () => {
    /*
     * ⚠ ORDER IS THE WHOLE FIX. `form.js:initSelect2` runs at page load against a fixed id list; these
     * controls do not exist until the definitions have been fetched. Binding therefore belongs at the end of
     * the code that renders them — which is what this asserts.
     */
    const source = codeOnly(CUSTOM_FIELDS_JS);
    const render = source.match(/const renderCustomFields\s*=\s*\(container,\s*definitions,\s*options\)\s*=>\s*\{([\s\S]*?)\n        return rendered;/);
    expect(render).not.toBeNull();

    const body = render[1];
    expect(body).toMatch(/enhanceSelects\(container\)/);
    // ...and after the append loop, not before it.
    expect(body.indexOf("container.appendChild(column)")).toBeLessThan(body.indexOf("enhanceSelects(container)"));
  });

  test("it is the SAME select2 binding form.js uses, not a second style", () => {
    const custom = codeOnly(CUSTOM_FIELDS_JS);
    const form = codeOnly(FORM_JS);

    // width '100%' and the select's own empty-option text as the placeholder — form.js's pattern verbatim.
    expect(form).toMatch(/option\[value=""\]/);
    expect(form).toMatch(/width:\s*'100%'/);
    expect(custom).toMatch(/option\[value=""\]/);
    expect(custom).toMatch(/width:\s*'100%'/);
  });

  test("a stored value still reaches a select2-bound control", () => {
    /*
     * Binding select2 makes a bare `control.value = x` invisible: select2 draws its own element and redraws
     * only on a jQuery change. Without this the fix would have hidden every stored custom value on the edit
     * form — empty on screen, and empty on the next save.
     */
    const source = codeOnly(CUSTOM_FIELDS_JS);
    expect(source).toMatch(/const setControlValue\s*=\s*\(control,\s*value\)/);
    expect(source).toMatch(/setControlValue\(control,\s*value\)/);

    const setter = source.match(/const setControlValue\s*=\s*\(control,\s*value\)\s*=>\s*\{([\s\S]*?)\n    \};/);
    expect(setter[1]).toMatch(/select2-hidden-accessible/);
    expect(setter[1]).toMatch(/trigger\('change'\)/);
  });
});
