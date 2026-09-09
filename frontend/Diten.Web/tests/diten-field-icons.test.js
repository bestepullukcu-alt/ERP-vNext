const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * Leading icons, and the second half of the alignment claim.
 *
 * ── 1. WHERE THE TEXT STARTS ────────────────────────────────────────────────
 * The previous round matched the tag box's HEIGHT to its neighbours and stopped there. Measured on the running
 * page: the tag editor's text began 9px in (1px on the box + 8px of Tagify's own input padding) while every
 * .form-control beside it begins at the theme's own inset. Same row, two different left edges.
 *
 * The tests below DERIVE that inset from the theme's `.form-control` rule instead of restating a pixel value —
 * a hard-coded 14 would keep passing after a theme change that moved everything else.
 *
 * ── 2. WHICH FIELDS GET AN ICON ─────────────────────────────────────────────
 * An icon carries INFORMATION here — "something other than typing happens in this box" — so it goes only where
 * that is true: the two dates (they open a calendar) and the tags (Enter turns text into a chip). Title,
 * description, estimate and the tenant's own text fields are plain typing and get none. Put one everywhere and
 * it says nothing.
 *
 * select2 controls are OUT OF SCOPE this round: nine of them, and select2 renders its own container, so it is a
 * template job with its own decision.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css");
const THEME = () => read("wwwroot", "assets", "vendor", "css", "core.css");
const FORM = () => read("Views", "Tasks", "_Form.cshtml");
const FORM_JS = () => read("wwwroot", "assets", "js", "Tasks", "form.js");
/*
 * The icon->picker binding MOVED here. It used to live in Tasks/form.js and nowhere else, which is precisely
 * why the pattern reached one form out of thirty: a screen that copied the golden reference's markup got the
 * glyph and none of the wiring, and shipped an icon that opens nothing.
 */
const DATEFIELD_JS = () => read("wwwroot", "assets", "js", "shared", "diten-datefield.js");
const ICONS = () => read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");

/** The theme's OWN background for a form control — read from the vendor rule, never restated. */
const themeFieldBackground = () => {
  const rule = /\.form-control\s*\{[^}]*?background-color:\s*([^;]+);/.exec(THEME());
  return rule ? rule[1].trim() : null;
};

/** The theme's OWN horizontal inset for a form control — read from the vendor rule, never restated. */
const themeFieldInset = () => {
  const theme = THEME();
  const rule = /\.form-control\s*\{[^}]*?padding:\s*([^;]+);/.exec(theme);
  if (!rule) { return null; }
  const parts = rule[1].trim().split(/\s+/);
  return parts.length > 1 ? parts[1] : parts[0];
};

// ── 1. the text starts where its neighbours' text starts ────────────────────

describe("the tag box's text starts on the same line as its neighbours'", () => {
  test("the theme's inset can be read — guards against a vacuous derivation", () => {
    // If this ever returns null the two tests below would compare against nothing and pass silently.
    expect(themeFieldInset(), "the theme's .form-control padding could not be parsed").toBeTruthy();
  });

  test("the box declares the THEME's inset, not a number of its own", () => {
    const inset = themeFieldInset();
    // Anchored at line start: `.diten-field tags.diten-tags {` also contains the substring, and an unanchored
    // match reads that rule instead — a false red that would tempt someone to loosen the assertion.
    const box = /^\.diten-tags\s*\{([^}]*)\}/m.exec(CSS());

    expect(box, "there is no .diten-tags rule").toBeTruthy();
    expect(box[1], `the box does not use the theme inset (${inset})`)
      .toContain(`padding-inline: ${inset}`);
  });

  test("Tagify's own editor padding is zeroed, or it adds a second inset on top", () => {
    /*
     * The measured defect was ADDITIVE: 1px on the box plus 8px inside the editor. Setting the box alone would
     * have moved the text to the wrong place again, just differently wrong.
     */
    const css = CSS();
    const rule = /tags\.diten-tags\s+\.tagify__input\s*\{([^}]*)\}/.exec(css);

    expect(rule, "nothing controls the editor's own padding").toBeTruthy();
    expect(rule[1], "the editor still adds its own inset").toMatch(/padding-inline:\s*0\b/);

    /*
     * ELEMENT-QUALIFIED, and that is the whole point. tagify.css:621 carries
     * `.tagify__input:first-child { padding-inline-start: calc(.9375rem - 2px - 5px) }` — specificity (0,2,0),
     * exactly the same as `.diten-tags .tagify__input`, and it loads later, so the tie went to the vendor and
     * the 8px survived. Measured on screen: the editor's text sat 8px right of the date field's.
     */
    expect(rule[0].startsWith("tags."), "the rule ties tagify.css's specificity and loses the cascade").toBe(true);
  });

  test("the chips start at the BOX'S EDGE, not at the text", () => {
    /*
     * ⚠ THIS REVERSES THE PREVIOUS ROUND, and the reason is worth keeping.
     *
     * That round aligned the chips with the field's TEXT. Measured on screen afterwards: box edge 42px, strip
     * edge 42px, first chip 82px — the chips sat 40px in (the icon's width) and read as an indented, separate
     * thing. The strip is OUTSIDE the box; it is a sibling block of the same width, so "aligned with the box"
     * means NO inset of its own. Text alignment belongs to the editor, which is inside the box and after the
     * icon.
     *
     * So the two are now deliberately DIFFERENT: the text starts after the icon, the chips start at the edge.
     * Derived, not numeric: the strip and the box share a parent and a width, so equality is the absence of a
     * padding rather than a matching pixel count.
     */
    const css = CSS();
    const strip = /^\.diten-tags-strip\s*\{([^}]*)\}/m.exec(css);

    expect(strip, "there is no strip rule").toBeTruthy();
    expect(strip[1], "the strip still insets its chips away from the box edge")
      .not.toMatch(/padding-inline-start:\s*calc/);

    // …and the icon variant must not reintroduce one either.
    const iconStrip = /\.diten-field\s+\.diten-tags-strip\s*\{([^}]*)\}/.exec(css);
    if (iconStrip) {
      expect(iconStrip[1], "the icon variant pushes the chips in by the icon width")
        .not.toMatch(/padding-inline-start:\s*calc\(1px/);
    }
  });

  test("the box's background is the THEME's, so it does not read as disabled", () => {
    /*
     * Measured: the tag box painted `--bs-body-bg` (rgb(245,245,249)) while every .form-control beside it is
     * transparent. It was the only filled box on the form, and a filled box among outlined ones reads as
     * "disabled" — reported as exactly that.
     *
     * Derived from the vendor rule rather than restated, for the same reason the inset is.
     */
    const background = themeFieldBackground();
    expect(background, "the theme's .form-control background could not be parsed").toBeTruthy();

    const box = /^\.diten-tags\s*\{([^}]*)\}/m.exec(CSS());
    expect(box[1], `the box does not use the theme background (${background})`)
      .toMatch(new RegExp(`background-color:\\s*${background}`));
  });
});

// ── 2. the icons ────────────────────────────────────────────────────────────

/*
 * THE BINDING MAP. Owner-decided, with three deliberate departures where the brief's glyph repeated the icon
 * already in that field's own card heading (see the "never repeats its own card header" test).
 */
const ICON_MAP = {
  taskTitle: "bx-text",
  taskPriority: "bx-flag",
  /*
   * DCP-005 slice 1. `bx-purchase-tag` — a LABEL you attach to work, which is what a type is. Deliberately not
   * `bx-purchase-tag-alt`, which the TAGS field already wears: tags are the reader's own free words, the type
   * is a controlled classification, and two glyphs one pixel apart would say they are the same kind of thing.
   */
  taskTypeId: "bx-purchase-tag",
  /*
   * DCP-005 slice 3. `bx-file` — a DOCUMENT, which is literally what is being searched for. Deliberately not
   * `bx-search`: the glyph names what the box is FOR, and a magnifier would say "search" on a form where three
   * other pickers also search and wear the noun they look for.
   */
  taskDocumentSearch: "bx-file",
  taskDescription: "bx-align-left",
  taskAssignmentTarget: "bx-directions",
  taskAssignee: "bx-user",
  taskPoolPosition: "bx-group",
  taskDueAt: "bx-calendar",
  taskStartAt: "bx-calendar",
  taskEstimateHours: "bx-time-five",
  taskTags: "bx-purchase-tag-alt",
  taskSpentHours: "bx-time-five",
  taskRemainingHours: "bx-hourglass",
  // Differentiated from their card headings (bx-search-alt / bx-user-check / bx-show):
  taskReviewer: "bx-user-voice",
  taskApprovalManager: "bx-user",
  taskWatchers: "bx-group",
  taskReminderLeadDays: "bx-bell",
  /*
   * The checklist's add box is NOT listed here any more — not because the rule lapsed, but because the markup
   * left this file. The row is drawn by assets/js/shared/diten-checkitem.js now, once for the create form and
   * the task detail page both, so a guard that reads _Form.cshtml would pass by finding nothing.
   *
   * The rule follows the markup: see "the shared add row keeps the form's field shape" below.
   */
};

describe("an icon marks the fields where typing is not the whole story", () => {
  const iconWrapper = (id) => {
    const form = FORM();
    const at = form.indexOf(`id="${id}"`);
    if (at < 0) { return null; }
    /*
     * The nearest icon wrapper that is still OPEN at the control. Searching backwards for the marker alone is
     * not enough: the estimate field follows the start-date field, so a naive lastIndexOf finds the date's
     * wrapper — already closed — and reports an icon the estimate does not have.
     */
    const openedAt = form.lastIndexOf('class="diten-field', at);
    if (openedAt < 0) { return null; }
    const between = form.slice(openedAt, at);
    if (between.includes("</div>")) { return null; }   // that wrapper closed before this control
    return between;
  };

  test("every mapped field has a wrapper and a glyph", () => {
    Object.keys(ICON_MAP).forEach((id) => {
      const wrapper = iconWrapper(id);
      expect(wrapper, `${id} has no icon wrapper`).toBeTruthy();
      expect(wrapper, `${id} has no icon`).toMatch(/<i class="[^"]*\bbx\b/);
    });
  });

  test("each field carries the glyph the map assigns it", () => {
    Object.entries(ICON_MAP).forEach(([id, glyph]) => {
      expect(iconWrapper(id), `${id} does not carry ${glyph}`)
        .toMatch(new RegExp(`${glyph}(?![a-z-])`));
    });
  });

  test("the estimate says what a value looks like", () => {
    // A number field with no unit and no example leaves the user guessing between 8, 8.5 and "1 gün".
    const form = FORM();
    const tag = /<input[^>]*id="taskEstimateHours"[\s\S]{0,200}?\/>/.exec(form);
    expect(tag, "the estimate input moved").toBeTruthy();
    expect(tag[0], "the estimate has no placeholder").toMatch(/placeholder="/);
    expect(tag[0], "the placeholder is a literal, not a resource")
      .toMatch(/placeholder="@Localizer\["FieldEstimateHoursPlaceholder"\]"/);
  });

  test("every icon the form asks for EXISTS in the icon set", () => {
    // `bx-hand-right` once rendered as a blank 17x17 gap because it is not in this set. Same guard, same reason.
    const icons = ICONS();
    const used = [...new Set([...FORM().matchAll(/class="bx (bx-[a-z0-9-]+)/g)].map((m) => m[1]))];

    const missing = used.filter((name) => !icons.includes(`.${name}`));
    expect(missing, `icon names with nothing to draw them:\n${missing.join("\n")}`).toHaveLength(0);
  });

  test("EVERY field carries an icon — the rule the owner replaced the old one with", () => {
    /*
     * ⚠ THIS REPLACES A DELETED TEST, deliberately and in place.
     *
     * The previous round pinned the opposite rule — "the card's PRIMARY fields get none", on the reasoning that
     * an icon is secondary chrome and would flatten title/description to the weight of everything else. The
     * OWNER decided against that: every field gets one. Deleting the old test without putting this one here
     * would have quietly narrowed the coverage to "the four fields that already had icons", so the successor
     * asserts the WHOLE map instead.
     *
     * Configurable fields stay out: they are rendered by renderCustomFields from tenant definitions, and their
     * value types are not fixed, so there is no glyph to pick per field.
     */
    Object.keys(ICON_MAP).forEach((id) => {
      expect(iconWrapper(id), `${id} has no icon — every field carries one now`).toBeTruthy();
    });

    // …and the count is pinned, so a field added later cannot slip through unnoticed.
    const wrappers = [...FORM().matchAll(/class="diten-field[ "]/g)].length;
    expect(wrappers, "a field was added or removed without updating the icon map")
      .toBe(Object.keys(ICON_MAP).length);
  });

  test("the icon says what the FIELD is, and never repeats its own card header", () => {
    /*
     * DECISION, asked for and recorded here.
     *
     * Three governance cards already carry a glyph in their heading — review = magnifier, approval =
     * user-check, watchers = eye — and the brief's map put the SAME glyph on the field inside each of them.
     * Measured on screen, the watchers card is a header, one line of description and one field: the same eye
     * twice inside ~60px.
     *
     * That is not reinforcement. The card icon names a DECISION ("is a review required?"); the field icon names
     * an INPUT ("which person?"). One glyph answering two different questions in one card is ambiguity, not
     * emphasis. So the three field glyphs are differentiated and the CARD glyphs are untouched.
     */
    const cardIcons = [...FORM().matchAll(/card-section-title[^>]*><i class="bx (bx-[a-z-]+)"/g)].map((m) => m[1]);
    expect(cardIcons.length, "the card headings lost their icons").toBeGreaterThanOrEqual(5);

    [["taskReviewer", "bx-search-alt"], ["taskApprovalManager", "bx-user-check"], ["taskWatchers", "bx-show"]]
      .forEach(([id, cardGlyph]) => {
        expect(cardIcons, `${cardGlyph} is no longer a card heading icon`).toContain(cardGlyph);
        expect(ICON_MAP[id], `${id} repeats its own card heading's glyph`).not.toBe(cardGlyph);
      });
  });

  test("the icon is hidden from assistive tech — the label already says what the field is", () => {
    ["taskDueAt", "taskStartAt", "taskTags"].forEach((id) => {
      expect(iconWrapper(id), `${id}'s icon is announced twice`).toMatch(/aria-hidden="true"/);
    });
  });

  test("the icon is quiet, and positioned with LOGICAL properties so RTL moves it", () => {
    const rule = /\.diten-field-icon\s*\{([^}]*)\}/.exec(CSS());

    expect(rule, "there is no icon rule").toBeTruthy();
    expect(rule[1], "the icon competes with the text").toMatch(/--bs-secondary-color/);
    expect(rule[1], "a physical offset leaves the icon on the left under RTL")
      .not.toMatch(/(^|[^-])left\s*:/);
    expect(rule[1], "the icon is not placed on the inline start").toMatch(/inset-inline-start/);
  });

  test("the icon is centred on the CONTROL, not on the wrapper the chips grew", () => {
    /*
     * Measured live and only visible once tags existed: the chip strip is rendered INSIDE `.diten-field`, so the
     * wrapper is taller than the control. `inset-block-start: 50%` then centres the icon in the whole wrapper —
     * icon centre 817px against a box centre of 798px, i.e. sitting under the field rather than in it.
     *
     * Pinned to the control's own height instead, so chips cannot move it.
     */
    const rule = /\.diten-field-icon\s*\{([^}]*)\}/.exec(CSS());

    expect(rule[1], "the icon is centred on the wrapper and drifts when chips appear")
      .not.toMatch(/inset-block-start:\s*50%/);
    expect(rule[1], "the icon is not pinned to the control's height")
      .toMatch(/inset-block-start:\s*calc\(38px/);
  });

  test("the control makes ROOM for the icon instead of overlapping it", () => {
    const css = CSS();
    const rule = /\.diten-field\s+\.form-control,[\s\S]{0,300}?\{([^}]*)\}/.exec(css);
    expect(rule, "nothing pads the control past the icon").toBeTruthy();
    expect(rule[1]).toMatch(/padding-inline-start:\s*calc\(/);

    /*
     * The FOCUS variant has to be in the same list. The tag box holds its own geometry on focus (against
     * tagify.css collapsing it) with a selector of equal specificity, so without this the icon room vanished
     * the moment the user clicked in — measured live as the text jumping from 72px to 48px.
     */
    expect(rule[0], "the icon room collapses on focus").toMatch(/:focus-within/);
  });

  test("focus does not move the text — the vendor collapses the inset, we hold it", () => {
    /*
     * `.tagify.tagify--focus { padding: 0; border-width: 2px }` in tagify.css would slide the text sideways the
     * instant the user clicks into the field. Only the colour should change on focus.
     */
    // Anchored at line start: the icon-room selector list also contains `.diten-field tags.diten-tags:focus-within,`
    // and an unanchored match reads that rule instead.
    const rule = /^tags\.diten-tags:focus-within,[\s\S]{0,80}?\{([^}]*)\}/m.exec(CSS());

    expect(rule, "the focus state is not held").toBeTruthy();
    expect(rule[1], "the inset collapses on focus").toMatch(/padding-inline:\s*0\.9375rem/);
    expect(rule[1], "the border thickens and shifts the text").toMatch(/border-width:\s*1px/);
  });

  test("the box is still 38px — the previous round's claim is not traded away", () => {
    const box = /^\.diten-tags\s*\{([^}]*)\}/m.exec(CSS());
    expect(box[1], "the icon changed the box height").toMatch(/block-size:\s*38px/);
  });
});

// ── 3. the two shapes that are not a 38px single-line input ─────────────────

describe("a textarea and a select2 need the icon placed differently", () => {
  test("the textarea's icon aligns with the FIRST LINE, not with the middle", () => {
    /*
     * `inset-block-start: calc(38px / 2)` centres the glyph on a one-line control. A textarea is four rows
     * tall, so the same rule would park the icon in the middle of the paragraph — beside line three, pointing
     * at nothing. It belongs on the first line, where the text starts.
     *
     * A modifier CLASS rather than `:has(textarea)`: the markup already knows which control it wrapped, and an
     * explicit name is what the next reader can grep for.
     */
    const wrapper = (() => {
      const form = FORM();
      const at = form.indexOf('id="taskDescription"');
      return form.slice(form.lastIndexOf('class="diten-field', at), at);
    })();

    expect(wrapper, "the description's icon is not marked as top-aligned")
      .toMatch(/diten-field-icon--top/);

    const rule = /\.diten-field-icon--top\s*\{([^}]*)\}/.exec(CSS());
    expect(rule, "there is no top-aligned variant").toBeTruthy();
    expect(rule[1], "the variant still centres on a 38px control").not.toMatch(/calc\(38px/);
    expect(rule[1], "the variant does not undo the centring transform").toMatch(/transform:\s*none/);
  });

  test("a select2 control makes room for the icon in the box select2 renders", () => {
    /*
     * select2 replaces the <select> with its own container, so padding the original element does nothing: the
     * text the user sees lives in `.select2-selection__rendered`. Both shapes are covered — single for the
     * eight single pickers, multiple for the watcher list.
     */
    const css = CSS();
    const rule = /\.diten-field[^{]*select2-selection__rendered[^{]*\{([^}]*)\}/.exec(css);

    expect(rule, "select2's own text box is never padded past the icon").toBeTruthy();
    expect(rule[1]).toMatch(/padding-inline-start:\s*calc\(/);
    expect(rule[0], "the multi-select (watchers) is not covered").toMatch(/multiple/);

    /*
     * MEASURED: select2.css carries this at (0,4,0) —
     * `.select2-container.select2-container--default .select2-selection--single .select2-selection__rendered`
     * — so a three-class rule loses outright. The single pickers sat at 16px while the multi-select, whose
     * vendor rule is weaker, was already right at 40px. And the FOCUS/OPEN variants re-state the padding, so
     * without them the text slides the moment the picker is clicked.
     */
    expect(rule[0], "the rule cannot out-specify select2.css")
      .toMatch(/select2-container\.select2-container--default/);
    const focusRule = /\.diten-field[^{]*select2-container--focus[^{]*\{([^}]*)\}/.exec(css);
    expect(focusRule, "the focus/open state is not held").toBeTruthy();
    expect(focusRule[0], "the open state is not held").toMatch(/select2-container--open/);
    /*
     * …and it keeps the vendor's BORDER COMPENSATION. The theme thickens the focus border to 2px and subtracts
     * that width from the padding so the text stays put; an override that dropped the subtraction reintroduced
     * the shift — measured as the text stepping 40px → 41px on open.
     */
    expect(focusRule[1], "the border compensation was dropped and the text shifts on focus")
      .toMatch(/-\s*var\(--bs-select-border-width\)/);
  });

  test("RTL gets its own rule — select2's container carries its own dir", () => {
    /*
     * MEASURED: select2 stamps `dir="rtl"` on its container, so `padding-inline-start` resolves against THAT
     * element rather than the page. Computed `padding-inline-start` read 39px while the effective right-side
     * padding (the text side in RTL) stayed at select2's 35px — the select2 text sat 4px inside the plain
     * inputs next to it. Only the TEXT side is overridden; the arrow side stays select2's business.
     */
    const rule = /\.diten-field[^{]*select2-container\[dir="rtl"\][^{]*\{([^}]*)\}/.exec(CSS());

    expect(rule, "RTL falls back to the logical property and drifts").toBeTruthy();
    expect(rule[1], "the text side is not corrected under RTL").toMatch(/padding-right:\s*calc\(/);
    expect(rule[1], "the arrow side was overridden too").not.toMatch(/padding-left/);
  });

  test("select2's own arrow stays on the far side — the icon must not push it", () => {
    // The icon is on the inline START; select2's arrow is on the inline END. Padding the wrong side would
    // shove the arrow inward and leave a gap where the icon is.
    const rule = /\.diten-field[^{]*select2-selection__rendered[^{]*\{([^}]*)\}/.exec(CSS());
    expect(rule[1], "the arrow side was padded instead").not.toMatch(/padding-inline-end/);
  });
});

// ── 4. the shortcut carries the SAME icons ──────────────────────────────────

describe("the quick-create offcanvas uses the same glyphs", () => {
  const QUICK = () => read("Views", "Tasks", "_QuickCreateOffcanvas.cshtml");

  // The four fields the shortcut has, paired with the full form's control they mirror.
  const SHARED = {
    quickTitle: "taskTitle",
    quickTarget: "taskAssignmentTarget",
    quickAssignee: "taskAssignee",
    quickPoolPosition: "taskPoolPosition",
    quickPriority: "taskPriority",
    quickDueAt: "taskDueAt"
  };

  test("every shared field carries the FULL FORM's glyph, derived not restated", () => {
    /*
     * The two surfaces share one draft. An icon set that drifts between them is exactly the class of defect
     * these rounds keep correcting — the same value would wear a different mark on either side of one click.
     */
    const quick = QUICK();
    Object.entries(SHARED).forEach(([quickId, taskId]) => {
      const at = quick.indexOf(`id="${quickId}"`);
      expect(at, `${quickId} is gone`).toBeGreaterThan(-1);
      const openedAt = quick.lastIndexOf('class="diten-field', at);
      expect(openedAt, `${quickId} has no icon wrapper`).toBeGreaterThan(-1);
      const wrapper = quick.slice(openedAt, at);
      expect(wrapper.includes("</div>"), `${quickId}'s wrapper closed before the control`).toBe(false);
      expect(wrapper, `${quickId} does not carry ${ICON_MAP[taskId]}`)
        .toMatch(new RegExp(`${ICON_MAP[taskId]}(?![a-z-])`));
    });
  });
});

// ── 5. the calendar icon is not decoration ──────────────────────────────────

describe("clicking the calendar icon opens the calendar", () => {
  test("the page binds the icon to the picker", () => {
    /*
     * A dead icon is a dead button's sibling, and this project shipped one of those once. The icon sits over the
     * field, so a user WILL aim at it.
     */
    expect(DATEFIELD_JS(), "nothing opens the picker from the icon").toMatch(/diten-field-icon/);
    expect(DATEFIELD_JS(), "the icon is found but never bound").toMatch(/addEventListener\('click'/);

    /*
     * …and the page still REACHES it. Extracting the behaviour into a shared file is only an improvement while
     * the callers actually call it; a task form that quietly stopped delegating would pass the assertion above
     * and still ship a dead icon.
     */
    expect(FORM_JS(), "the task form no longer delegates to the shared date component")
      .toMatch(/DitenDateField\.enhance/);
    for (const page of ["Create.cshtml", "Edit.cshtml", "Details.cshtml"]) {
      expect(read("Views", "Tasks", page), `Tasks/${page} does not load the shared date component`)
        .toMatch(/shared\/diten-datefield\.js/);
    }
  });

  test("the click reaches flatpickr's own open()", () => {
    delete global.TaskForm;
    delete global.DitenDateField;
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");

    document.body.innerHTML = `
      <div class="diten-field">
        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
        <input class="form-control flatpickr-date" id="taskDueAt" />
      </div>`;

    const input = document.getElementById("taskDueAt");
    const opened = [];
    input.flatpickr = (options) => {
      const instance = { open: () => opened.push("open"), config: options };
      input._flatpickr = instance;
      return instance;
    };

    global.TaskForm.enhanceDates(document);
    document.querySelector(".diten-field-icon").click();

    expect(opened, "the icon is decoration — clicking it does nothing").toEqual(["open"]);
  });

  test("the icon does not swallow clicks meant for the field itself", () => {
    // pointer-events must stay ON for the date icon (it is a control) — the tag icon has nothing to open, so it
    // must NOT intercept the click that focuses the editor.
    const rule = /\.diten-tags-icon\s*\{([^}]*)\}/.exec(CSS());
    expect(rule, "the tag icon has no rule of its own").toBeTruthy();
    expect(rule[1], "the tag icon steals the click that should focus the editor")
      .toMatch(/pointer-events:\s*none/);
  });
});

describe("the shared add row keeps the form's field shape", () => {
  /*
   * MOVED, not dropped. `taskChecklistInput` used to sit in the map above and be checked against
   * _Form.cshtml's markup; the add row is now built in JS and shared with the task detail page, so the same
   * rule is asserted against the component that builds it.
   *
   * `bx-list-plus`, not the card heading's `bx-list-check`: the heading names the list, this one names the act
   * of adding to it — the same distinction the reviewer/approver/watcher fields draw against their headings.
   */
  it("wraps the input in a .diten-field carrying bx-list-plus", () => {
    const component = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot/assets/js/shared/diten-checkitem.js"), "utf8");
    expect(component).toContain("'diten-field flex-grow-1'");
    expect(component).toContain("'bx bx-list-plus diten-field-icon'");
  });
});

// ── 6. the pattern travels — a census, and a list that may only shrink ──────

/*
 * WHY THIS SECTION EXISTS, and what it answers.
 *
 * The owner asked a question this file could not answer: the Golden Slim offcanvas was updated, 23 other
 * offcanvases stayed on the old markup, and NOTHING went red. Measured: this file reads `Views/Tasks/_Form.cshtml`
 * and `_QuickCreateOffcanvas.cshtml`; `golden-reference-form-icons.test.js` reads the two reference forms. Between
 * them they cover four files. The other offcanvases were never in scope of any guard, so "the reference moved and
 * the product did not" was invisible by construction — not a test that failed, a test that was never asked.
 *
 * Widening the scope outright would produce 29 reds today, which teaches the reader to delete the guard. So the
 * census is pinned with a KNOWN list instead, the `mongo-indexing.md` shape:
 *
 *   - a file NOT on the list that carries form controls without .diten-field → RED (the pattern stops spreading)
 *   - a file ON the list that has since adopted the wrapper → RED (the list may only shrink)
 *
 * ⚠ Adding a line here to turn a red green is forbidden. Every line is a debt, not a licence.
 */
const VIEWS_ROOT = web("Views");

/** Every `*Offcanvas*.cshtml` under Views/ that actually holds an editable control. */
const offcanvasForms = () => {
  const out = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { walk(p); }
    else if (e.name.includes("Offcanvas") && e.name.endsWith(".cshtml")) { out.push(p); }
  });
  walk(VIEWS_ROOT);
  // A shell with no control has no field to mark; including it would be a red nobody can fix.
  return out.filter((f) => /class="[^"]*\b(?:form-control|form-select)\b/.test(fs.readFileSync(f, "utf8")));
};
const relView = (f) => path.relative(VIEWS_ROOT, f).split(path.sep).join("/");

/*
 * MEASURED 2026-09-08, after Governance/Roles and Governance/Users were ported. Each of these draws an editable
 * control with no `.diten-field` wrapper and therefore no icon. Grouped as the owner counted them:
 *   CRM 10 · EnterpriseStrategy 2 · MasterDataManagement 4 · MDM 1 · ManagementGovernance 1
 *   Organization 3 · PPM 2 · Platform 6
 */
const KNOWN_NO_ICONS = [
  "CRM/Accounts/_ContactLinkEndOffcanvas.cshtml",
  "CRM/Accounts/_ContactLinkOffcanvas.cshtml",
  "CRM/Accounts/_RelationshipEndOffcanvas.cshtml",
  "CRM/Accounts/_RelationshipOffcanvas.cshtml",
  "CRM/Campaigns/_TargetCreateEditOffcanvas.cshtml",
  "CRM/KnowledgeConcepts/_RelationshipCreateEditOffcanvas.cshtml",
  "CRM/KnowledgeConcepts/_TemplateCreateEditOffcanvas.cshtml",
  "CRM/KnowledgeConcepts/_TypeCreateEditOffcanvas.cshtml",
  "CRM/TerritoryManagement/_CreateEditOffcanvas.cshtml",
  "CRM/TerritoryManagement/_CreateNodeOffcanvas.cshtml",
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_PlanningCycleOffcanvas.cshtml",
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_StrategyPeriodOffcanvas.cshtml",
  "MDM/ProductAbbreviationRegister/_CreateEditOffcanvas.cshtml",
  "ManagementGovernance/ProcessModeling/_CreateEditOffcanvas.cshtml",
  "MasterDataManagement/FinishedGoods/_CreateEditOffcanvas.cshtml",
  "MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml",
  "MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml",
  "MasterDataManagement/Lskus/_CreateEditOffcanvas.cshtml",
  "Organization/OrganizationUnits/_CreateEditOffcanvas.cshtml",
  "Organization/PositionAssignments/_CreateEditOffcanvas.cshtml",
  "Organization/Positions/_CreateEditOffcanvas.cshtml",
  "PPM/Initiatives/_CreateEditOffcanvas.cshtml",
  "PPM/Shared/_CreateEditOffcanvas.cshtml",
  "Platform/Administrators/_CreateEditOffcanvas.cshtml",
  "Platform/DomainManagement/_CreateEditOffcanvas.cshtml",
  "Platform/ServiceManagement/_CreateEditOffcanvas.cshtml",
  "Platform/SubscriptionFeatures/_CategoryOffcanvas.cshtml",
  "Platform/Tenants/Commercial/_AddModuleEntitlementOffcanvas.cshtml",
  "Platform/WorkingCalendarImports/_CreateEditOffcanvas.cshtml"
];

describe("every offcanvas form carries the leading-icon pattern, or is a named debt", () => {
  test("the census sees a real population — guards against a vacuous sweep", () => {
    // A walk that found nothing would make both assertions below pass while measuring the empty set.
    expect(offcanvasForms().length, "no offcanvas form was found at all").toBeGreaterThan(30);
  });

  test("no NEW offcanvas ships without the wrapper", () => {
    const offenders = offcanvasForms()
      .filter((f) => !fs.readFileSync(f, "utf8").includes("diten-field"))
      .map(relView)
      .filter((f) => !KNOWN_NO_ICONS.includes(f))
      .sort();
    expect(offenders,
      "these offcanvas forms draw a control with no .diten-field wrapper — copy the golden reference "
      + "(Views/DevEnablement/GoldenReferenceSlim/_CreateEditOffcanvas.cshtml). Do NOT add them to KNOWN_NO_ICONS.")
      .toEqual([]);
  });

  test("the debt list may only shrink — a fixed file must leave it", () => {
    /*
     * The half that makes the list honest. Without it a ported screen keeps its exemption, and the next
     * regression in that same file walks in free.
     */
    const stale = KNOWN_NO_ICONS.filter((f) => {
      const full = path.join(VIEWS_ROOT, f);
      return !fs.existsSync(full) || fs.readFileSync(full, "utf8").includes("diten-field");
    });
    expect(stale, "these files now carry the pattern (or are gone) — delete their KNOWN_NO_ICONS entries")
      .toEqual([]);
  });
});

// ── 7. the two governance offcanvases, field by field ──────────────────────

describe("Roles and Users wear the reference's shape", () => {
  const wrapperIn = (source, id) => {
    const at = source.indexOf(`id="${id}"`);
    if (at < 0) { return null; }
    const openedAt = source.lastIndexOf('class="diten-field', at);
    if (openedAt < 0) { return null; }
    const between = source.slice(openedAt, at);
    return between.includes("</div>") ? null : between;
  };

  /*
   * Roles: the two "name" fields are deliberately DIFFERENT glyphs. `Name` is the immutable key (bx-hash, the
   * reference's own identifier glyph); `DisplayName` is the prose a human reads (bx-text). Two adjacent fields
   * both called a name are exactly where a reader needs the mark to disambiguate.
   *
   * Users: both name halves wear bx-user, deliberately the SAME. They are one kind of thing, and the reference
   * already pairs one glyph with two fields where that holds (EffectiveDate/ExpirationDate → bx-calendar).
   */
  const GOVERNANCE_ICONS = {
    "Governance/Roles/_CreateEditOffcanvas.cshtml": {
      roleName: "bx-hash",
      roleDisplayName: "bx-text",
      roleDescription: "bx-align-left"
    },
    "Governance/Users/_CreateEditOffcanvas.cshtml": {
      userEmail: "bx-envelope",
      userFirstName: "bx-user",
      userLastName: "bx-user"
    }
  };

  Object.entries(GOVERNANCE_ICONS).forEach(([file, map]) => {
    test(`${file} — every field has a wrapper and its glyph`, () => {
      const source = fs.readFileSync(path.join(VIEWS_ROOT, file), "utf8");
      Object.entries(map).forEach(([id, glyph]) => {
        const wrapper = wrapperIn(source, id);
        expect(wrapper, `${id} has no .diten-field wrapper`).toBeTruthy();
        expect(wrapper, `${id} does not carry ${glyph}`).toMatch(new RegExp(`${glyph}(?![a-z-])`));
        expect(wrapper, `${id}'s icon is announced to screen readers twice`).toMatch(/aria-hidden="true"/);
      });

      // Pinned count, same reason as the task form's: a field added later cannot slip through unmarked.
      // The Users switch is excluded on purpose — a switch has no text inset to mark (golden reference rule).
      const wrappers = [...source.matchAll(/class="diten-field[ "]/g)].length;
      expect(wrappers, "a field was added or removed without updating this map").toBe(Object.keys(map).length);
    });
  });

  test("the textarea's glyph is top-aligned, not parked beside line three", () => {
    const source = fs.readFileSync(path.join(VIEWS_ROOT, "Governance/Roles/_CreateEditOffcanvas.cshtml"), "utf8");
    expect(wrapperIn(source, "roleDescription"), "the description's icon centres on four rows")
      .toMatch(/diten-field-icon--top/);
  });

  test("the help texts the golden reference has no counterpart for are KEPT", () => {
    /*
     * "Imitate, do not copy." The reference has no immutable-name hint, and its absence there is not a verdict
     * on this form: these two lines are the only place the user is told the key cannot be changed later.
     */
    expect(fs.readFileSync(path.join(VIEWS_ROOT, "Governance/Roles/_CreateEditOffcanvas.cshtml"), "utf8"))
      .toContain("NameImmutableHint");
    expect(fs.readFileSync(path.join(VIEWS_ROOT, "Governance/Users/_CreateEditOffcanvas.cshtml"), "utf8"))
      .toContain("EmailImmutableHint");
  });

  test("the wrapper does not swallow the validation message", () => {
    /*
     * MEASURED IN THE THEME, not guessed. core.css shows an error with `.was-validated :invalid ~ .invalid-feedback`
     * — a SIBLING combinator. Wrapping the control makes the message a sibling of the wrapper instead, so adopting
     * the icon pattern silently disables every inline error on the form. The theme hit this with `.input-group`
     * and answered with `:has()`; backbone-custom.css must carry the same answer for `.diten-field`, or the two
     * screens ported in this round validate into the void.
     */
    const theme = THEME();
    expect(theme, "the theme no longer shows errors by sibling — re-derive this guard")
      .toMatch(/\.was-validated\s+:invalid\s*~\s*\.invalid-feedback/);

    const css = CSS();
    expect(css, "nothing restores the error message through the .diten-field wrapper")
      .toMatch(/\.diten-field:has\(:invalid\)\s*~\s*\.invalid-feedback/);
    expect(css, "the JS-set .is-invalid path is not covered")
      .toMatch(/\.diten-field:has\(\.is-invalid\)\s*~\s*\.invalid-feedback/);
  });
});
