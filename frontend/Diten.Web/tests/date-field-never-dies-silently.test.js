const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * ══ THE CALENDAR THAT DID NOT OPEN, AND THE SILENCE THAT LET IT SHIP ══════════════════════════════════════
 *
 * MEASURED (2026-09-02, management demo): in the Task Center's quick-create offcanvas, "+ Yeni ▸ Görev", the
 * due-date field opened no calendar. The user had to type the date by hand — in a box whose leading glyph is a
 * calendar icon, i.e. a control advertising something it could not do.
 *
 * ── WHAT IT WAS NOT ──────────────────────────────────────────────────────────────────────────────────────
 * Not flatpickr: _LayoutTenantShell loads the library for every tenant page. Not the offcanvas being added to
 * the DOM late either — the partial is server-rendered with the page, and quick-create.js calls
 * `TaskForm.enhanceDates` from its own `wire()`, after the pickers are filled.
 *
 * ── WHAT IT WAS ──────────────────────────────────────────────────────────────────────────────────────────
 * `Views/WorkCenterNext/Index.cshtml` loads Tasks/form.js but never loaded `shared/diten-datefield.js`, which
 * is where the flatpickr construction actually lives. `enhanceDates` therefore hit
 *
 *     if (!scope || !global.DitenDateField) { return 0; }
 *
 * …and returned zero. No error, no warning, no mark on the field. The screen could not say why the calendar
 * did not open because nothing in the chain ever said anything: the same defect family as the blank picker and
 * the empty tab — a surface that fails without telling anybody.
 *
 * ── SO TWO THINGS ARE PINNED HERE, AND THEY ARE NOT THE SAME CLAIM ───────────────────────────────────────
 *   1. BEHAVIOUR — with the markup present and the component missing, the failure is ANNOUNCED. That is the
 *      guard that survives the next screen to copy this markup, because the next screen will not have a test.
 *   2. DELIVERY — a view that loads Tasks/form.js also loads the component form.js delegates to. That is the
 *      dependency the Task Center broke, stated as a dependency; see the note above §2 for the wider claim
 *      that was tried first and was wrong.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);

const DATEFIELD = "wwwroot/assets/js/shared/diten-datefield.js";

/** A flatpickr stand-in with the ONE thing the component actually uses: the element-level constructor. */
const installFlatpickr = () => {
  const instances = [];
  window.HTMLElement.prototype.flatpickr = function flatpickrStub(config) {
    const instance = {
      config,
      opened: false,
      element: this,
      open() { this.opened = true; },
      setDate(value) { this.element.value = value; }
    };
    this._flatpickr = instance;
    instances.push(instance);
    return instance;
  };
  return instances;
};

const uninstallFlatpickr = () => { delete window.HTMLElement.prototype.flatpickr; };

/** The offcanvas's own due-date markup, field for field — wrapper, icon and control. */
const dateFieldMarkup = () => {
  document.body.innerHTML = `
    <div id="host">
      <div class="diten-field">
        <i class="bx bx-calendar diten-field-icon" aria-hidden="true"></i>
        <input type="text" class="form-control flatpickr-date" id="quickDueAt" />
      </div>
    </div>`;
  return document.getElementById("host");
};

// ══ 1. behaviour: a field that cannot be bound says so ═════════════════════════════════════════════════

describe("a date field that cannot be enhanced is never left quietly dead", () => {
  afterEach(() => {
    uninstallFlatpickr();
    delete global.DitenDateField;
    delete global.TaskForm;
  });

  test("with the library present the control really is bound — the premise for every negative below", () => {
    installFlatpickr();
    loadScript(DATEFIELD);
    const host = dateFieldMarkup();

    expect(global.DitenDateField.enhance(host)).toBe(1);
    expect(document.getElementById("quickDueAt")._flatpickr).toBeTruthy();
  });

  test("the leading glyph OPENS the calendar — the icon is a control, not a picture", () => {
    installFlatpickr();
    loadScript(DATEFIELD);
    const host = dateFieldMarkup();
    global.DitenDateField.enhance(host);

    host.querySelector(".diten-field-icon").click();

    expect(document.getElementById("quickDueAt")._flatpickr.opened).toBe(true);
  });

  test("with flatpickr missing it reports the field it could not bind, instead of returning 0 in silence", () => {
    // No installFlatpickr() — this is a page that carries the markup and not the library.
    loadScript(DATEFIELD);
    const host = dateFieldMarkup();
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));

    try {
      expect(global.DitenDateField.enhance(host)).toBe(0);
    } finally {
      console.error = original;
    }

    expect(errors, "the field silently stayed a plain text box and nothing said so").not.toHaveLength(0);
    expect(errors.join(" ")).toContain("flatpickr");
  });

  test("a page with no date field at all stays quiet — the alarm is about dead controls, not about absence", () => {
    loadScript(DATEFIELD);
    document.body.innerHTML = `<div id="host"><input type="text" class="form-control" id="plain" /></div>`;
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));

    try {
      expect(global.DitenDateField.enhance(document.getElementById("host"))).toBe(0);
    } finally {
      console.error = original;
    }

    expect(errors).toHaveLength(0);
  });

  test("TaskForm.enhanceDates says which SCREEN forgot the component, not just that one is missing", () => {
    // The Task Center's exact failure: form.js loaded, diten-datefield.js not.
    loadScript("wwwroot/assets/js/Tasks/form.js");
    const host = dateFieldMarkup();
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));

    try {
      expect(global.TaskForm.enhanceDates(host)).toBe(0);
    } finally {
      console.error = original;
    }

    expect(
      errors.join(" "),
      "enhanceDates found a date field, had no component to bind it with, and returned 0 without a word"
    ).toContain("diten-datefield.js");
  });
});

// ══ 2. delivery: a view that leans on the component ships it ══════════════════════════════════════════

/*
 * ⚠ THE CLAIM IS A DEPENDENCY ONE, NOT "every date field must use this component".
 *
 * A first draft of this guard asserted that every view drawing a `.flatpickr-date` loads diten-datefield.js —
 * and turned 18 views red across CRM, MasterData, Organization and the recurrence rules. It was wrong about
 * them: those screens construct flatpickr in their OWN module JS (Organization/Positions/form.js:94,
 * CRM/Segments/form.js:572, …), so their calendars open. Whether they SHOULD all share the component is a real
 * question and a separate piece of work; it is not this defect.
 *
 * What is measurable, and what actually broke, is narrower and exact: `Tasks/form.js` binds no date itself.
 * Its `enhanceDates` delegates to `DitenDateField` and returns 0 when there is none. So any view that loads
 * form.js and expects a bound date field MUST also load the component form.js delegates to — the Task Center
 * did not, and its calendar was dead.
 */
const viewsLoadingTaskForm = () => {
  const found = [];
  const walk = (dir) => {
    fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) { walk(full); return; }
      if (!entry.name.endsWith(".cshtml")) { return; }

      const source = fs.readFileSync(full, "utf8");
      if (!source.includes("assets/js/Tasks/form.js")) { return; }
      found.push(path.relative(web(), full).split(path.sep).join("/"));
    });
  };
  walk(web("Views"));
  return found;
};

describe("a view that loads Tasks/form.js ships what form.js delegates to", () => {
  test("the sweep finds those views at all — otherwise the loop below asserts nothing", () => {
    const views = viewsLoadingTaskForm();
    expect(views.length, "no view was found loading Tasks/form.js").toBeGreaterThan(0);
    // The page the defect was reported on. If the sweep stops seeing it, the guard is blind to the regression.
    expect(views).toContain("Views/WorkCenterNext/Index.cshtml");
  });

  test.each(viewsLoadingTaskForm())("%s loads shared/diten-datefield.js", (viewPath) => {
    const source = fs.readFileSync(web(viewPath), "utf8");
    // Vacuity guard: prove the file really is the one the sweep matched before asserting about it.
    expect(source).toContain("assets/js/Tasks/form.js");

    expect(
      source,
      `${viewPath} loads Tasks/form.js, whose enhanceDates does nothing on its own — it delegates to `
      + "DitenDateField and returns 0 when the component is absent. Without shared/diten-datefield.js the date "
      + "field ships as a plain text box under a calendar icon that opens nothing."
    ).toContain("shared/diten-datefield.js");
  });

  test.each(viewsLoadingTaskForm())("%s has flatpickr itself available", (viewPath) => {
    const layout = fs.readFileSync(web("Views/Shared/_LayoutTenantShell.cshtml"), "utf8");
    expect(fs.readFileSync(web(viewPath), "utf8") + layout).toContain("flatpickr/flatpickr.js");
  });
});
