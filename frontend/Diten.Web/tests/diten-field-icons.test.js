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

  test("the two dates, the tags and the estimate carry one", () => {
    ["taskDueAt", "taskStartAt", "taskTags", "taskEstimateHours"].forEach((id) => {
      const wrapper = iconWrapper(id);
      expect(wrapper, `${id} has no icon wrapper`).toBeTruthy();
      expect(wrapper, `${id} has no icon`).toMatch(/<i class="[^"]*\bbx\b/);
    });
  });

  test("each icon says what the field DOES — calendar, tag, clock", () => {
    expect(iconWrapper("taskDueAt")).toMatch(/bx-calendar\b/);
    expect(iconWrapper("taskStartAt")).toMatch(/bx-calendar\b/);
    expect(iconWrapper("taskTags")).toMatch(/bx-purchase-tag-alt\b/);
    expect(iconWrapper("taskEstimateHours")).toMatch(/bx-time-five\b/);
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

  test("the card's PRIMARY fields get none — this is the rule, pinned", () => {
    /*
     * THE RULE, so the next round does not add icons "for consistency":
     *
     *   ICON     — due date, start date (open a calendar) · tags (Enter makes a chip) · estimate (a unit and a
     *              shape the user has to guess otherwise)
     *   NO ICON  — task title, description
     *
     * Title and description are the card's PRIMARY fields. An icon is secondary chrome, and putting one on
     * them drops them to the same visual weight as everything else — the opposite of what the layout is for.
     * An icon marks a field that carries something EXTRA, not every field.
     */
    ["taskTitle", "taskDescription"].forEach((id) => {
      expect(iconWrapper(id), `${id} is a primary field and must not carry an icon`).toBeNull();
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

// ── 3. the calendar icon is not decoration ──────────────────────────────────

describe("clicking the calendar icon opens the calendar", () => {
  test("the page binds the icon to the picker", () => {
    /*
     * A dead icon is a dead button's sibling, and this project shipped one of those once. The icon sits over the
     * field, so a user WILL aim at it.
     */
    expect(FORM_JS(), "nothing opens the picker from the icon").toMatch(/diten-field-icon/);
  });

  test("the click reaches flatpickr's own open()", () => {
    delete global.TaskForm;
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
