const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * The quick-create offcanvas, measured against its OWN reference.
 *
 * ⚠ THE REFERENCE IS SLIM, NOT COMPACT. The Compact standard PROHIBITS create/edit offcanvases; this surface
 * exists as an EA-approved deviation (the view's own header says so), so the shape it must match is
 * `Views/DevEnablement/GoldenReferenceSlim/_CreateEditOffcanvas.cshtml`. Comparing it to Compact would be
 * comparing it to a thing that does not exist there.
 *
 * The structural claims are DERIVED from that reference file rather than restated, so a change to the golden
 * shape moves this test with it instead of leaving it asserting last year's classes.
 *
 * The heavier half is not styling: the two surfaces share ONE draft, so pressing "Detaylı form" must not hand
 * the same data to a different KIND of control. The date was a browser-native `<input type="date">` here and a
 * flatpickr there; the selects were raw here and select2 there.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const GOLDEN = () => read("Views", "DevEnablement", "GoldenReferenceSlim", "_CreateEditOffcanvas.cshtml");
const QUICK = () => read("Views", "Tasks", "_QuickCreateOffcanvas.cshtml");
const QUICK_JS = () => read("wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js");

/** The class list the golden reference puts on one of its three regions — read, never restated. */
const goldenClasses = (region) => {
  const rule = new RegExp(`<div class="(offcanvas-${region}[^"]*)"`).exec(GOLDEN());
  return rule ? rule[1].split(/\s+/) : null;
};

const quickClasses = (region) => {
  const rule = new RegExp(`<div class="(offcanvas-${region}[^"]*)"`).exec(QUICK());
  return rule ? rule[1].split(/\s+/) : null;
};

// ── 1. structure ────────────────────────────────────────────────────────────

describe("the offcanvas has the golden SLIM shape", () => {
  test("the reference can be read — guards against a vacuous comparison", () => {
    expect(goldenClasses("header"), "the golden header could not be parsed").toBeTruthy();
    expect(goldenClasses("body"), "the golden body could not be parsed").toBeTruthy();
    expect(goldenClasses("footer"), "the golden footer could not be parsed").toBeTruthy();
  });

  test("header and body carry every class the reference gives them", () => {
    ["header", "body"].forEach((region) => {
      const golden = goldenClasses(region);
      const mine = quickClasses(region);
      expect(mine, `there is no offcanvas-${region}`).toBeTruthy();
      const missing = golden.filter((c) => !mine.includes(c));
      expect(missing, `offcanvas-${region} is missing: ${missing.join(", ")}`).toHaveLength(0);
    });
  });

  test("there is a FOOTER, and the buttons live in it", () => {
    /*
     * Measured: no footer at all — both buttons sat inside the body, so they scrolled with the form and both
     * hugged the left edge. The reference puts them in a bordered footer with the cancel and the primary at
     * opposite ends.
     */
    const golden = goldenClasses("footer");
    const mine = quickClasses("footer");

    expect(mine, "the offcanvas has no footer — the buttons are still in the body").toBeTruthy();
    const missing = golden.filter((c) => !mine.includes(c));
    expect(missing, `the footer is missing: ${missing.join(", ")}`).toHaveLength(0);

    const footer = QUICK().slice(QUICK().indexOf('class="offcanvas-footer'));
    expect(footer, "the primary action is not in the footer").toContain("quickSubmit");
  });

  test("cancel sits at the inline start and the primary action at the end", () => {
    // `justify-content-between` is what the reference uses, and it is what puts them at opposite ends.
    const quick = QUICK();
    const footer = quick.slice(quick.indexOf('class="offcanvas-footer'));

    expect(footer).toContain("justify-content-between");
    // Order in the markup decides which end each lands on.
    expect(footer.indexOf("data-bs-dismiss"), "cancel is not first")
      .toBeLessThan(footer.indexOf("quickSubmit"));
  });

  test('"Detaylı form" is grouped with CANCEL, not with the primary action', () => {
    /*
     * DECISION, recorded here because the brief asked for one.
     *
     * It is a NAVIGATION, not a commit: it saves nothing and leaves the surface. Standing beside "Oluştur" it
     * would read as a second way to finish, and two buttons at the primary end make the user choose between
     * them. Grouped with cancel it reads as what it is — "leave here, continue elsewhere" — and the footer
     * keeps exactly one primary action, which is the reference's shape.
     */
    const quick = QUICK();
    const footer = quick.slice(quick.indexOf('class="offcanvas-footer'));

    expect(footer, "the handover button left the footer").toContain("quickMoreOptions");
    expect(footer.indexOf("quickMoreOptions"), "the handover sits on the primary side")
      .toBeLessThan(footer.indexOf("quickSubmit"));
  });

  test("labels carry the reference's weight", () => {
    const golden = /class="form-label([^"]*)"/.exec(GOLDEN())[1].trim();
    expect(golden, "the golden label class changed shape").toBeTruthy();

    const labels = [...QUICK().matchAll(/<label class="([^"]*)"/g)].map((m) => m[1]);
    expect(labels.length, "there are no labels to check").toBeGreaterThan(3);
    labels.forEach((cls) => {
      expect(cls, `a label is missing "${golden}"`).toContain(golden);
    });
  });
});

// ── 2. the SAME controls as the full form ───────────────────────────────────

describe("the two surfaces share a draft, so they share the controls", () => {
  test("the date is a flatpickr field, not the browser's own control", () => {
    /*
     * `<input type="date">` renders in the OPERATING SYSTEM's locale — an Arabic page still showed gg.aa.yyyy —
     * and it is a different control from the one the full form uses for the same value. The full form fixed
     * this once; the shortcut kept the native input.
     */
    const quick = QUICK();
    expect(quick.match(/type="date"/g), "a native date input remains").toBeNull();

    const tag = /<input[^>]*id="quickDueAt"[\s\S]{0,200}?\/>/.exec(quick);
    expect(tag, "the due-date input is gone").toBeTruthy();
    expect(tag[0], "the due date is not a flatpickr field").toContain("flatpickr-date");
  });

  test("every select is marked for select2, like the full form's", () => {
    const quick = QUICK();
    const selects = [...quick.matchAll(/<select class="([^"]*)"[^>]*id="(quick[A-Za-z]+)"/g)];

    expect(selects.length, "the selects moved").toBeGreaterThanOrEqual(4);
    selects.forEach(([, cls, id]) => {
      expect(cls, `${id} is a raw form-select`).toContain("select2");
    });
  });

  test("the date carries the SAME icon rule the full form uses", () => {
    // .diten-field is the mechanism the previous round built; a second one here would be a second truth.
    const quick = QUICK();
    const at = quick.indexOf('id="quickDueAt"');
    const wrapper = quick.lastIndexOf('class="diten-field', at);

    expect(wrapper, "the due date has no icon wrapper").toBeGreaterThan(-1);
    expect(quick.slice(wrapper, at), "the due date has no calendar icon").toMatch(/bx-calendar\b/);
  });

  test("the page calls the FULL FORM's enhancers rather than rolling its own", () => {
    const source = QUICK_JS();
    expect(source, "select2 is never bound here").toMatch(/TaskForm\.enhanceSelects/);
    expect(source, "flatpickr is never bound here").toMatch(/TaskForm\.enhanceDates/);
    expect(source, "the offcanvas builds its own picker").not.toMatch(/new\s+Tagify|\.flatpickr\(\{/);
  });
});

// ── 3. placeholders ─────────────────────────────────────────────────────────

describe("every field says what goes in it", () => {
  const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];

  test("the two date/number-ish fields and the selects have a prompt", () => {
    const quick = QUICK();
    // The due date is typed as well as picked, so it needs the format prompt the full form has.
    const due = /<input[^>]*id="quickDueAt"[\s\S]{0,200}?\/>/.exec(quick)[0];
    expect(due, "the due date has no placeholder").toMatch(/placeholder="/);

    // A select's prompt is its empty option; the two people pickers get one from renderPersonOptions, and the
    // two fixed lists are pre-selected by design (target defaults to Self, priority to Medium).
    expect(quick, "the assignee picker lost its hint").toContain("AssigneeHint");
  });

  test("the new prompt ships in all seven languages, sets identical", () => {
    const KEY = "FieldDueAtPlaceholder";
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`);
      const hits = [...xml.matchAll(new RegExp(`name="${KEY}"`, "g"))].length;
      expect(hits, `${locale} has ${hits} copies of ${KEY}`).toBe(1);
    });

    const keysOf = (locale) => [...read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]).sort();
    const base = keysOf("en");
    LOCALES.forEach((locale) => expect(keysOf(locale), `${locale} drifted from en`).toEqual(base));
  });
});

// ── 4. a hidden field sends nothing ─────────────────────────────────────────

describe("a hidden field contributes no value", () => {
  const loadStack = () => {
    ["TaskForm", "WcnQuickCreate"].forEach((k) => { delete global[k]; });
    loadScript("wwwroot/assets/js/Tasks/form.js");
  };

  test("the assignee is dropped when the target is not a person", () => {
    /*
     * The exact trap the full form fell into once (the reminder lead time): a control hidden by
     * applyTargetVisibility still held a value, and the payload carried it. Here the picker keeps whatever was
     * chosen before the user switched target.
     */
    loadStack();

    const payload = global.TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "PositionPool",
      assigneeUserId: "dddddddd-dddd-dddd-dddd-dddddddddddd", poolPositionId: "p1"
    });

    expect(payload.assigneeUserId, "a hidden assignee was sent").toBeNull();
    expect(payload.poolPositionId).toBe("p1");
  });

  test("the pool position is dropped when the target is a person", () => {
    loadStack();

    const payload = global.TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "Person",
      assigneeUserId: "dddddddd-dddd-dddd-dddd-dddddddddddd", poolPositionId: "p1"
    });

    expect(payload.poolPositionId, "a hidden pool position was sent").toBeNull();
  });

  test("switching to Self drops BOTH", () => {
    loadStack();

    const payload = global.TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
      assigneeUserId: "dddddddd-dddd-dddd-dddd-dddddddddddd", poolPositionId: "p1"
    });

    expect(payload.assigneeUserId).toBeNull();
    expect(payload.poolPositionId).toBeNull();
  });
});

// ── 5. the regression the BL-057 round left behind ──────────────────────────

describe("the assignee picker is actually filled", () => {
  test("the people lookup's OBJECT shape is unwrapped here too", () => {
    /*
     * A LIVE REGRESSION introduced by the BL-057 round and missed by its tests. That change made
     * `assignablePeople()` answer `{ people, excluded }` instead of a bare array, and updated the full form —
     * but this file still passed `people.data` straight to renderPersonOptions. An object is not an array, so
     * the quick-create assignee picker renders its "nobody holds a position" empty state on every load, on a
     * tenant that has plenty of people.
     */
    const source = QUICK_JS();
    expect(source, "quick-create still treats the lookup answer as an array")
      .toMatch(/data\?\.people|data\.people/);
  });
});
