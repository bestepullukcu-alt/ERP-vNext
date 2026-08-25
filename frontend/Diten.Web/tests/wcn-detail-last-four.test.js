const fs = require("fs");
const path = require("path");

/*
 * THE DETAIL PAGE'S LAST FOUR (2026-08-24). Four small things the owner saw, one file each.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const RAZOR = fs.readFileSync(web("Views", "Tasks", "Details.cshtml"), "utf8");
const PAGE = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "details-page.js"), "utf8");
const GOLDEN = fs.readFileSync(
  web("Views", "DevEnablement", "GoldenReferenceCompact", "Details.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (xml, key) => {
  const at = xml.indexOf(`name="${key}"`);
  return at < 0 ? null : xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
};
/** Source with comments stripped — an assertion must read the CODE, not the note explaining it. */
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
const rule = (needle) => {
  const at = CSS.indexOf(needle);
  return at < 0 ? "" : CSS.slice(at, CSS.indexOf("}", at) + 1);
};

describe("① a called-off subtask recedes once, not twice", () => {
  it("drops the opacity and keeps the strike", () => {
    /*
     * MUTATION GUARD #1: put `opacity` back on the row and this goes red.
     *
     * MEASURED, three states and their signal counts:
     *   bekliyor 1 · tamamlandı 5 (fill + muted title + strike + green toggle + tick glyph)
     *   iptal edildi 4 → strike + OPACITY + muted toggle + x glyph
     * Two of the cancelled row's four said the same thing, and `opacity` said it by dimming the TEXT — a row
     * that is still readable history. It now recedes the way the completed row does: the theme's disabled
     * tone on the title. Told apart by the two signals that genuinely differ — no completed fill, x glyph.
     */
    const block = CSS.slice(CSS.indexOf(".wcn-subtask-cancelled .wcn-subtask-title"),
      CSS.indexOf(".wcn-subtask-cancelled .wcn-subtask-title") + 400);
    expect(block).toContain("text-decoration: line-through");
    /*
     * ⚠ THE TOKEN CHANGED AGAIN (2026-08-24, Tur A). It was `--bs-secondary-color` — one mechanism instead of
     * two, which was the point of ① — but measured on the new lighter surface it still read **2.29**, under
     * the 3.0 floor. `--bs-body-color` measures **5.19** here and **6.54** in dark.
     *
     * What ① actually claimed is unchanged and still asserted below: ONE mechanism, not two. The strike-through
     * plus the `bx-x-square` glyph carry "called off"; the colour was never the thing saying it.
     */
    expect(block).toContain("color: var(--bs-body-color)");
    expect(CSS, "the row is dimmed by opacity again").not.toContain(".wcn-subtask-cancelled { opacity");
    // …and the tone is the THEME's, never a hand-picked grey that would dodge the contrast check.
    expect(block, "a literal colour appeared").not.toMatch(/#[0-9a-f]{3,6}/i);
  });

  it("keeps the row visible and in its place", () => {
    // The owner asked and it was decided: "not done" and "never wanted" are different facts, and the second
    // is often the answer to "why". No collapsing, no hiding — and cancelled rows already sort to the bottom.
    expect(APP, "cancelled subtasks were filtered out of the list").not.toMatch(/filter\([^)]*!isCancelledSubtask[^)]*\)\s*\.map\(\s*subtaskRow/);
    expect(APP).toContain("isCancelledSubtask");
  });
});

describe("② the source record page wears the product's own pattern", () => {
  it("carries the golden reference's header — breadcrumb, Back, Edit", () => {
    /*
     * MUTATION GUARD #2: remove the breadcrumb from the Razor and this goes red.
     *
     * MEASURED live before: breadcrumb absent, no Back button, and the one button said "Kaydet" while being
     * an `<a href=".../Edit">` — a Save control on a read-only page that saved nothing.
     */
    expect(RAZOR).toContain('aria-label="breadcrumb"');
    expect(RAZOR).toContain('class="breadcrumb mb-0"');
    expect(RAZOR).toContain('SharedLocalizer["Back"]');
    expect(RAZOR).toContain('SharedLocalizer["Edit"]');
    expect(RAZOR, "the button says Save on a read-only page again").not.toContain('actionSave');

    // The golden reference is the source of the shape — if IT loses the pattern, this test is measuring nothing.
    expect(GOLDEN).toContain('aria-label="breadcrumb"');
    expect(GOLDEN).toContain('SharedLocalizer["Back"]');
  });

  it("draws its fields the way the golden reference draws them", () => {
    expect(PAGE).toContain("backbone-preview-field");
    expect(PAGE).toContain("backbone-preview-label");
    expect(PAGE).toContain("backbone-preview-value mt-1");
    expect(PAGE).toContain("col-12 col-md-6");
    expect(code(PAGE), "the definition list came back").not.toContain("<dl");
    expect(PAGE, "a field disappears when empty — two records of one type would look different")
      .toContain("'-'");
  });

  it("stays read-only — no action was added", () => {
    // The page's own comment: "read-only detail … this page is the module's own record view."
    expect(PAGE, "a write reached the read-only page").not.toMatch(/TasksApi\.(transition|plan|create|addComment)/);
    const links = RAZOR.match(/<a [^>]*class="btn[^"]*"/g) || [];
    expect(links.length, "the header grew a third control").toBe(2);
  });
});

describe("③ one treatment for the FS abbreviation, wherever it appears", () => {
  it("gives the blocked banner the dependency card's footnote, not a chip", () => {
    /*
     * MUTATION GUARD #3: put `wcn-chip wcn-chip-danger wcn-dep-type` back and this goes red.
     *
     * The card solved this — sentence first, abbreviation demoted to a small muted footnote whose `title` is
     * a bonus rather than the only carrier. The banner kept the loud red pill with a tooltip-only expansion.
     */
    expect(APP, "the banner draws a chip again").not.toContain("wcn-dep-type");
    const banner = code(APP.slice(APP.indexOf("const renderBlocked"), APP.indexOf("const renderBlocked") + 3600));
    expect(banner).toContain('class="wcn-dep-abbr"');
    // The SENTENCE still comes first; the footnote follows it.
    expect(banner.indexOf("wcn-blocked-why")).toBeLessThan(banner.indexOf("wcn-dep-abbr"));
  });

  it("keeps ONE abbreviation class in the whole module", () => {
    /*
     * ⚠ Only the ABBREVIATION's classes. `wcn-dep-title` is the dependency card's SENTENCE and belongs to a
     * different question; matching every `wcn-dep-*` would fail on it and prove nothing.
     */
    const abbrClasses = [...new Set((APP.match(/class="wcn-dep-(abbr|type)[^"]*"/g) || []))];
    expect(abbrClasses, "a second abbreviation treatment appeared").toEqual(['class="wcn-dep-abbr"']);
  });

  it("does NOT reuse the card's sentences here, and says why", () => {
    /*
     * The card's `DepSentence*` describe the RELATIONSHIP — true whether or not it currently bites. The banner
     * describes a LIVE block and pairs its sentence with the clause naming which act is stopped. Swapping them
     * would state the rule twice and drop the half that matters right now.
     */
    const banner = code(APP.slice(APP.indexOf("const renderBlocked"), APP.indexOf("const renderBlocked") + 3600));
    expect(banner).toContain("BLOCKER_SENTENCE_KEY");
    expect(banner, "the banner started using the card's sentence").not.toContain("DEP_SENTENCE_KEY");
    expect(banner).toContain("BLOCKED_AFFECTS_KEY");
  });
});

describe("④ the reference dialog obeys its own placeholder rule", () => {
  it("labels the field and puts a real example inside it", () => {
    /*
     * MUTATION GUARD #4: put the label's own words back as the placeholder and this goes red.
     *
     * The snooze dialog is what every other dialog in this session was measured against, and it was breaking
     * the rule it set: placeholder = REAL EXAMPLE, never the field's own name. It had no label at all and
     * used "Hangi tarihe kadar" as the placeholder.
     */
    /*
     * The dialog is anchored on its own title key rather than on an enclosing function name — the function
     * has been renamed once already this session, and a slice keyed to a name that moves measures nothing.
     */
    const at = APP.indexOf("t('SnoozeTitle')");
    expect(at, "the snooze dialog vanished — the slice is measuring nothing").toBeGreaterThan(-1);
    const fn = APP.slice(at, at + 3000);
    expect(fn).toContain("label: t('SnoozeUntilLabel')");
    expect(fn).toContain("placeholder: t('DatePlaceholder')");
    expect(fn, "the label's words are the placeholder again").not.toContain("placeholder: t('SnoozeUntilLabel')");
  });

  it("uses the SAME example every other date box uses", () => {
    // Planla and the meeting scheduler already say `DatePlaceholder`; a second date format would be a second
    // answer to one question.
    expect((APP.match(/placeholder: t\('DatePlaceholder'\)/g) || []).length).toBeGreaterThanOrEqual(2);
    LANGS.forEach((lang) => {
      const v = String(value(resx(lang), "DatePlaceholder") || "").trim();
      expect(v, `${lang}: DatePlaceholder missing`).not.toBe("");
      expect(v, `${lang}: the placeholder repeats the label`)
        .not.toBe(String(value(resx(lang), "SnoozeUntilLabel") || "").trim());
    });
  });
});

describe("⑤ the two row types are one row — same height, readable when finished", () => {
  /*
   * TUR A, iş 2. Two things the owner photographed, one measurement each.
   */
  it("gives the completed row a surface its own text can be read on", () => {
    /*
     * MUTATION GUARD: put `--bs-secondary-bg` back and this goes red.
     *
     * MEASURED: `--bs-secondary-bg` renders rgb(228,230,232) and the muted title on it gave **1.83** contrast
     * — under 3.0, the floor for interface elements, never mind 4.5 for text. The row meant to read as
     * finished history was the hardest row on the card to read.
     *
     * ⚠ THE TOKEN IS NOT TOUCHED. `--bs-secondary-bg` is used across the whole product; repainting it here
     * would change screens nobody looked at. Only these two rules move — and they move TOGETHER, because the
     * checklist item and the subtask row are one row language (the stylesheet says so three rules above) and
     * a split would put two completed states on one page.
     */
    const done = rule(".diten-checkitem.done,");
    expect(done).toContain("background: var(--bs-light-bg-subtle)");
    expect(done, "the dark surface came back").not.toContain("background: var(--bs-secondary-bg)");
    // Both row types, one rule — a selector that lost its sibling would split the language.
    expect(CSS).toContain(".diten-checkitem.done,\n.wcn-subtasks > li.wcn-subtask-done {");
    // The hover state follows the same surface, or hovering would look like a different row.
    expect(CSS).toContain("color-mix(in sRGB, rgba(var(--bs-primary-rgb), .03), var(--bs-light-bg-subtle))");
  });

  it("stops the subtask row importing a 38px control height", () => {
    /*
     * MUTATION GUARD: put `btn btn-icon` back and this goes red.
     *
     * MEASURED: the subtask row stood 52px against the checklist row's 44px with identical padding, radius and
     * background. The whole difference was one button — `.btn` carries the theme's 38px control height, and a
     * 38px control inside a 6px-padded row sets the row's height from the inside.
     *
     * The checklist's own action (`diten-checkitem-btn`) is not a `.btn` at all, and `.wcn-subtask-rowaction`
     * already declares everything this control needs. Nothing was removed: same glyph, same title, same
     * aria-label, same handler.
     */
    const at = APP.indexOf('class="wcn-subtask-rowaction"');
    expect(at, "the row action markup vanished — the slice measures nothing").toBeGreaterThan(-1);
    const markup = APP.slice(at - 60, at + 300);
    expect(markup, "the row action imported the theme's control height again").not.toContain("btn btn-icon");
    expect(markup).toContain('class="wcn-subtask-rowaction"');
    // The control is still a control — nothing was traded away for the height.
    expect(markup).toContain("aria-label=");
    expect(markup).toContain("title=");
  });
});
