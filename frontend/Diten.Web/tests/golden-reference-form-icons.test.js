const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * THE GOLDEN REFERENCE CARRIES THE PATTERN, or the pattern does not travel.
 *
 * ── WHAT WAS MEASURED, and why this file exists ─────────────────────────────────────────────────────────────
 * The task form applies the leading-icon contract to 18 fields out of 18. Across the product, ONE form part out
 * of thirty had adopted it — and that one was the task form itself. The two GoldenReference forms, which are the
 * thing a developer is told to copy when starting a new screen, carried ZERO icons.
 *
 * That is not thirty people getting it wrong. It is a pattern with no channel to travel down: the CSS was ready,
 * the shared behaviour was ready, and the one document everybody copies did not show either. So the rule below
 * is asserted HERE, on the reference, and not only on the module that happened to invent it.
 *
 * ── WHAT IS PINNED ──────────────────────────────────────────────────────────────────────────────────────────
 *   1. every field is wrapped, and carries the glyph the map assigns it
 *   2. the wrapper holds ICON + CONTROL and nothing else (label above, feedback after)
 *   3. a textarea's glyph is top-aligned; a select2's needs no extra class
 *   4. the two surfaces AGREE — Slim's glyphs are derived from Compact's, never restated
 *   5. a switch is deliberately OUT (there is no text inset to mark)
 *   6. section headings use ONE idiom, and carry a glyph that does not repeat a field's inside the same card
 *   7. the calendar glyph OPENS the calendar — asserted by clicking it, not by reading for a string
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const COMPACT = () => read("Views", "DevEnablement", "GoldenReferenceCompact", "_Form.cshtml");
const SLIM = () => read("Views", "DevEnablement", "GoldenReferenceSlim", "_CreateEditOffcanvas.cshtml");
const ICONS = () => read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");

/*
 * THE BINDING MAP for the full-page reference. Keyed by the asp-for MODEL PROPERTY rather than by a rendered id,
 * because the id is generated and a test that reads for one is asserting Razor's behaviour, not the form's.
 *
 * Every glyph names WHAT THE FIELD IS:
 *   Code           bx-hash          an identifier, not prose
 *   ReferenceType  bx-purchase-tag  a CONTROLLED classification — the same glyph the task type wears, and
 *                                   deliberately NOT bx-purchase-tag-alt, which means the reader's own free tags
 *   Name           bx-text          prose, same as the task title
 *   Category       bx-category
 *   Description    bx-align-left    (top-aligned; see the textarea case)
 *   GroupKey       bx-collection    it puts this record in a set with others
 *   SourceSystem   bx-server        the system it came FROM, not a person
 *   Owner          bx-user          a person, same as the assignee
 *   Version        bx-history       a revision in a line of revisions
 *   Effective/Expiration  bx-calendar   both, exactly as the task form's due/start pair
 *   Priority       bx-flag
 */
const COMPACT_ICONS = {
  Code: "bx-hash",
  ReferenceType: "bx-purchase-tag",
  Name: "bx-text",
  Category: "bx-category",
  Description: "bx-align-left",
  GroupKey: "bx-collection",
  SourceSystem: "bx-server",
  Owner: "bx-user",
  Version: "bx-history",
  EffectiveDate: "bx-calendar",
  ExpirationDate: "bx-calendar",
  Priority: "bx-flag"
};

/* The offcanvas reference's five fields, paired with the full-page control each mirrors. */
const SLIM_SHARED = {
  slimCode: "Code",
  slimName: "Name",
  slimReferenceType: "ReferenceType",
  slimPriority: "Priority",
  slimDescription: "Description"
};

/**
 * The wrapper text between the nearest still-OPEN `.diten-field` and the control.
 * Searching backwards for the marker alone is not enough: two fields in a row would let a naive lastIndexOf find
 * the PREVIOUS field's wrapper, already closed, and report an icon this control does not have.
 */
const wrapperBefore = (source, needle) => {
  const at = source.indexOf(needle);
  if (at < 0) { return null; }
  const openedAt = source.lastIndexOf('class="diten-field', at);
  if (openedAt < 0) { return null; }
  const between = source.slice(openedAt, at);
  return between.includes("</div>") ? null : between;
};

/** The wrapper in front of the CONTROL for a model property — the label carries asp-for as well, so skip it. */
const compactField = (prop) => {
  const source = COMPACT();
  // The control is the second `asp-for="Prop"` on a field: label, then input/select/textarea.
  const re = new RegExp(`<(?:input|select|textarea)[^>]*asp-for="${prop}"`);
  const m = re.exec(source);
  if (!m) { return null; }
  return wrapperBefore(source, m[0]);
};

const slimField = (id) => wrapperBefore(SLIM(), `id="${id}"`);

// ── 1. every field is wrapped and marked ────────────────────────────────────

describe("the full-page golden reference carries the field pattern", () => {
  test("every mapped field has a wrapper and a glyph", () => {
    Object.keys(COMPACT_ICONS).forEach((prop) => {
      const wrapper = compactField(prop);
      expect(wrapper, `${prop} has no .diten-field wrapper`).toBeTruthy();
      expect(wrapper, `${prop} has no icon`).toMatch(/<i class="[^"]*\bbx\b/);
    });
  });

  test("each field carries the glyph the map assigns it", () => {
    Object.entries(COMPACT_ICONS).forEach(([prop, glyph]) => {
      expect(compactField(prop), `${prop} does not carry ${glyph}`)
        .toMatch(new RegExp(`${glyph}(?![a-z-])`));
    });
  });

  test("the count is pinned — a field cannot be added without a decision about its glyph", () => {
    const wrappers = [...COMPACT().matchAll(/class="diten-field[ "]/g)].length;
    expect(wrappers, "a field was added or removed without updating the icon map")
      .toBe(Object.keys(COMPACT_ICONS).length);
  });

  test("the icon is hidden from assistive tech — the label already names the field", () => {
    const icons = [...COMPACT().matchAll(/<i class="bx [^"]*diten-field-icon[^"]*"[^>]*>/g)].map((m) => m[0]);
    expect(icons.length).toBe(Object.keys(COMPACT_ICONS).length);
    icons.forEach((tag) => expect(tag, `not hidden: ${tag}`).toMatch(/aria-hidden="true"/));
  });

  test("every glyph the reference asks for EXISTS in the icon set", () => {
    // `bx-hand-right` once rendered as a blank 17x17 gap because it is not in this set. Same guard, same reason.
    const set = ICONS();
    const used = [...new Set([
      ...[...COMPACT().matchAll(/class="bx (bx-[a-z0-9-]+)/g)].map((m) => m[1]),
      ...[...SLIM().matchAll(/class="bx (bx-[a-z0-9-]+)/g)].map((m) => m[1])
    ])];
    expect(used.length).toBeGreaterThan(0);
    const missing = used.filter((name) => !set.includes(`.${name}`));
    expect(missing, `icon names with nothing to draw them:\n${missing.join("\n")}`).toHaveLength(0);
  });
});

// ── 2. the wrapper holds the CONTROL and nothing else ───────────────────────

describe("the wrapper holds icon + control, and nothing else", () => {
  test("the label stays OUTSIDE and above the wrapper", () => {
    const source = COMPACT();
    Object.keys(COMPACT_ICONS).forEach((prop) => {
      const labelAt = source.indexOf(`<label asp-for="${prop}"`);
      expect(labelAt, `${prop} has no label`).toBeGreaterThan(-1);
      const wrapperAt = source.indexOf('class="diten-field"', labelAt);
      expect(wrapperAt, `${prop}'s wrapper is not after its label`).toBeGreaterThan(labelAt);
      // …and the label is not swallowed by a wrapper opened before it.
      const openedBefore = source.lastIndexOf('class="diten-field"', labelAt);
      if (openedBefore > -1) {
        expect(source.slice(openedBefore, labelAt).includes("</div>"),
          `${prop}'s label sits INSIDE a .diten-field`).toBe(true);
      }
    });
  });

  test("validation feedback comes AFTER the wrapper, never inside it", () => {
    const source = COMPACT();
    const validations = [...source.matchAll(/<span asp-validation-for="(\w+)"/g)].map((m) => m[1]);
    expect(validations.length, "the reference lost its validation spans").toBeGreaterThan(0);
    validations.forEach((prop) => {
      const at = source.indexOf(`<span asp-validation-for="${prop}"`);
      const openedAt = source.lastIndexOf('class="diten-field"', at);
      expect(source.slice(openedAt, at).includes("</div>"),
        `${prop}'s validation span is inside the .diten-field wrapper`).toBe(true);
    });
  });

  test("a SWITCH gets no field icon — it is a labelled control, not a box with a text inset", () => {
    const source = COMPACT();
    const at = source.indexOf('asp-for="IsActive" class="form-check-input"');
    expect(at, "the reference lost its switch").toBeGreaterThan(-1);
    expect(wrapperBefore(source, 'asp-for="IsActive" class="form-check-input"'),
      "the switch was wrapped in a .diten-field — there is no inline start to mark").toBeNull();
    expect(wrapperBefore(SLIM(), 'id="slimIsActive"'),
      "the offcanvas switch was wrapped in a .diten-field").toBeNull();
  });
});

// ── 3. the shapes that need something extra ─────────────────────────────────

describe("a textarea and a select2 are placed differently", () => {
  test("the textarea's glyph aligns with the FIRST LINE, not with the middle", () => {
    /*
     * .diten-field-icon centres on a 38px control. A textarea is eight lines tall, so without the modifier the
     * glyph parks beside line four, pointing at nothing.
     */
    [["Compact", compactField("Description")], ["Slim", slimField("slimDescription")]].forEach(([name, w]) => {
      expect(w, `${name}'s description field lost its wrapper`).toBeTruthy();
      expect(w, `${name}'s textarea glyph is centred on a 38px line it does not have`)
        .toMatch(/diten-field-icon--top/);
    });

    // …and no non-textarea wears the modifier, which would push its glyph above its own control.
    const tops = [...COMPACT().matchAll(/diten-field-icon--top[\s\S]{0,300}?<(input|select|textarea)/g)];
    tops.forEach((m) => expect(m[1], "--top is on a control that is not a textarea").toBe("textarea"));
  });

  test("a select2 needs NO extra class — the same wrapper, and the CSS pads what select2 draws", () => {
    const compact = compactField("ReferenceType");
    expect(compact, "the reference's select lost its wrapper").toBeTruthy();
    expect(compact, "a select2 was given the textarea modifier").not.toMatch(/--top/);
    expect(COMPACT(), "the reference's select is no longer a select2")
      .toMatch(/<select asp-for="ReferenceType" class="select2 form-select"/);

    const slim = slimField("slimReferenceType");
    expect(slim, "the offcanvas select lost its wrapper").toBeTruthy();
    expect(slim, "the offcanvas select was given the textarea modifier").not.toMatch(/--top/);
  });
});

// ── 4. the two references agree ─────────────────────────────────────────────

describe("the offcanvas reference uses the SAME glyphs as the full page", () => {
  test("every shared field carries the full page's glyph, DERIVED not restated", () => {
    /*
     * Two surfaces, one contract. A glyph set that drifts between them is exactly the defect this round is
     * correcting one level up: the same value wearing a different mark depending on which screen opened it.
     */
    Object.entries(SLIM_SHARED).forEach(([slimId, compactProp]) => {
      const wrapper = slimField(slimId);
      expect(wrapper, `${slimId} has no .diten-field wrapper`).toBeTruthy();
      expect(wrapper, `${slimId} does not carry ${COMPACT_ICONS[compactProp]}`)
        .toMatch(new RegExp(`${COMPACT_ICONS[compactProp]}(?![a-z-])`));
    });
  });

  test("the offcanvas count is pinned too", () => {
    expect([...SLIM().matchAll(/class="diten-field[ "]/g)].length)
      .toBe(Object.keys(SLIM_SHARED).length);
  });

  test("the offcanvas invents NO section heading — owner decision, recorded here", () => {
    /*
     * An offcanvas has no <section class="card"> and no <h6> today. Adding one is not "adding an icon", it is
     * inventing a structure the surface does not have, so the icon round deliberately stopped at the fields.
     * Pinned so a later round makes that decision on purpose rather than by drift.
     */
    const slim = SLIM();
    expect(slim, "the offcanvas grew a section heading").not.toMatch(/<h6/);
    expect(slim, "the offcanvas grew a section card").not.toMatch(/<section class="card/);
  });
});

// ── 5. ONE section-heading idiom ────────────────────────────────────────────

describe("section headings use one idiom, and their glyph says something the fields do not", () => {
  const headings = () => [...COMPACT().matchAll(/<h6([^>]*)>([\s\S]*?)<\/h6>/g)];

  test("every card heading is .card-section-title and carries a glyph", () => {
    const found = headings();
    expect(found.length, "the reference lost its section headings").toBe(4);
    found.forEach(([full, attrs, body]) => {
      expect(attrs, `a heading is not the canonical idiom: ${full.slice(0, 90)}`)
        .toMatch(/class="card-section-title/);
      expect(body, `a heading has no glyph: ${full.slice(0, 90)}`).toMatch(/<i class="bx bx-[a-z0-9-]+"><\/i>/);
    });
  });

  test("the dead idiom is gone — no h5.card-title, no hand-assembled helper stack", () => {
    /*
     * Three idioms produced the same picture. `.card-section-title` is the one that survives: it carries the
     * whole look under a single NAME, so a heading is copied as one class instead of five helpers that a later
     * edit can half-drop.
     */
    const source = COMPACT();
    expect(source, "the reference regrew the h5.card-title idiom").not.toMatch(/<h5 class="card-title/);
    expect(source, "the reference regrew the helper-stack idiom")
      .not.toMatch(/<h6 class="text-uppercase text-heading fw-semibold/);
  });

  test("a heading's glyph never repeats a glyph from a field inside the same card", () => {
    /*
     * Two identical marks a few pixels apart say the header and the field are the same thing. Measured per CARD,
     * because a glyph reused in a DIFFERENT card is saying the same thing about a different subject, which is
     * the point of a shared vocabulary.
     */
    const cards = COMPACT().split(/<section class="card/).slice(1);
    expect(cards.length).toBe(4);
    cards.forEach((card) => {
      const heading = /<h6[^>]*>\s*<i class="bx (bx-[a-z0-9-]+)"/.exec(card);
      expect(heading, "a card lost its heading glyph").toBeTruthy();
      const fields = [...card.matchAll(/class="bx (bx-[a-z0-9-]+) diten-field-icon/g)].map((m) => m[1]);
      expect(fields, `${heading[1]} is repeated by a field in its own card`).not.toContain(heading[1]);
    });
  });
});

// ── 5b. the offcanvas select still SAYS "choose one" ────────────────────────

describe("both references DECLARE the select placeholder, from the empty option", () => {
  /*
   * ⚠ TWO DEFECTS, OPPOSITE SHAPES, ONE CAUSE — and both were found by LOOKING at the running page
   * (owner, 2026-08-27), which is why they are pinned here now.
   *
   *   · the OFFCANVAS passed `placeholder: $el.data('placeholder') || ''` and the markup has no
   *     data-placeholder. An EMPTY placeholder is not "no placeholder": select2 renders
   *     `<span class="select2-selection__placeholder"></span>` INSTEAD of the option's text, so the localized
   *     "Seçiniz…" never reached the screen — a blank box with an arrow.
   *
   *   · the FULL PAGE passed NO placeholder at all, so select2 treated the empty option as an ordinary
   *     SELECTION and painted it in the BODY colour — measured rgb(56,69,81) against rgb(167,172,178) for every
   *     plain input's placeholder in the same card. An empty field looked exactly like a filled one.
   *
   * Both are the same omission: the placeholder was left for select2 to infer. Declaring it from the option's
   * own text fixes both and keeps ONE localized source — the resx behind the markup — instead of a second copy
   * in a data- attribute no language file would ever update.
   */
  const declaresFromOption = (js) => /placeholder:[^\n]*option\[value=""\]'\)\.text\(\)/.test(js);

  test("the full page declares it — an empty select must not read as a filled one", () => {
    const js = read("wwwroot", "assets", "js", "DevEnablement", "GoldenReferenceCompact", "form.js");
    expect(declaresFromOption(js), "the full-page select2 declares no placeholder").toBe(true);
    expect(COMPACT(), "the placeholder option is gone or was hard-coded")
      .toMatch(/<option value="">@SharedLocalizer\["SelectPlaceholder"\]<\/option>/);
  });

  test("the placeholder is read from the empty option, not from a data- attribute", () => {
    /*
     * FOUND ON THE RUNNING PAGE (owner, 2026-08-27), not by a test — which is why one is here now.
     *
     * The offcanvas built select2 with `placeholder: $el.data('placeholder') || ''`, and the markup carries no
     * data-placeholder. An EMPTY placeholder is not "no placeholder": select2 then renders
     * `<span class="select2-selection__placeholder"></span>` INSTEAD of the empty option's own text, so the
     * localized "Seçiniz…" in `<option value="">` never reached the screen and the field read as a blank box
     * with an arrow — beside four fields that all showed their hint.
     *
     * The fix reads the option's text, which keeps ONE localized source (the resx behind the markup) instead of
     * a second copy in an attribute no language file would ever update.
     */
    const js = read("wwwroot", "assets", "js", "DevEnablement", "GoldenReferenceSlim", "index.js");
    expect(js, "the offcanvas select2 no longer falls back to the empty option's text")
      .toMatch(/placeholder:[^\n]*option\[value=""\]'\)\.text\(\)/);

    // …and the option that carries it is still a localized resource, not a literal.
    expect(SLIM(), "the placeholder option is gone or was hard-coded")
      .toMatch(/<option value="">@SharedLocalizer\["SelectPlaceholder"\]<\/option>/);
  });
});

// ── 6. the calendar glyph is a control, not a picture ───────────────────────

describe("clicking the reference's calendar glyph opens the calendar", () => {
  test("the reference's pages LOAD the shared date component", () => {
    /*
     * The mutation this catches: delete the icon->open() binding (or stop loading the file that owns it) and the
     * glyph goes dead while the page still renders perfectly. Nothing on the golden reference guarded that until
     * now — which is how a form could copy the markup and ship a dead control.
     */
    for (const page of ["Create.cshtml", "Edit.cshtml"]) {
      expect(read("Views", "DevEnablement", "GoldenReferenceCompact", page),
        `GoldenReferenceCompact/${page} does not load the shared date component`)
        .toMatch(/shared\/diten-datefield\.js/);
    }
    expect(read("wwwroot", "assets", "js", "DevEnablement", "GoldenReferenceCompact", "form.js"),
      "the reference re-implements flatpickr instead of using the shared component")
      .toMatch(/DitenDateField\.enhance/);
  });

  test("the click reaches flatpickr's own open() — on the reference's OWN markup", () => {
    delete global.DitenDateField;
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");

    document.body.innerHTML = `
      <div class="diten-field">
        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
        <input class="form-control flatpickr-date" id="EffectiveDate" />
      </div>
      <div class="diten-field">
        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
        <input class="form-control flatpickr-date" id="ExpirationDate" />
      </div>`;

    const opened = [];
    ["EffectiveDate", "ExpirationDate"].forEach((id) => {
      const input = document.getElementById(id);
      input.flatpickr = () => {
        const instance = { open: () => opened.push(id) };
        input._flatpickr = instance;
        return instance;
      };
    });

    expect(global.DitenDateField.enhance(document), "the component found no date fields").toBe(2);
    document.querySelectorAll(".diten-field-icon").forEach((icon) => icon.click());

    expect(opened, "a calendar glyph is decoration — clicking it does nothing")
      .toEqual(["EffectiveDate", "ExpirationDate"]);
  });

  test("the glyph is found through the WRAPPER, so an enhancer between them cannot break it", () => {
    /*
     * select2 (and the reference's own init) insert a container between .diten-field and the control. A binding
     * that walked node.parentElement found the inserted div and no icon — silently, on exactly the forms that
     * use a picker inside an enhanced field.
     */
    delete global.DitenDateField;
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");

    document.body.innerHTML = `
      <div class="diten-field">
        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
        <div class="position-relative">
          <input class="form-control flatpickr-date" id="nested" />
        </div>
      </div>`;

    const input = document.getElementById("nested");
    const opened = [];
    input.flatpickr = () => {
      const instance = { open: () => opened.push("open") };
      input._flatpickr = instance;
      return instance;
    };

    global.DitenDateField.enhance(document);
    document.querySelector(".diten-field-icon").click();

    expect(opened, "the binding only works while the markup stays flat").toEqual(["open"]);
  });
});
