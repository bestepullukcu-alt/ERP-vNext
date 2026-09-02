const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * ══ THE SUBTASK PANELS ARE MADE OF THE PRODUCT'S FIELDS ═══════════════════════════════════════════════════
 *
 * THE DEFECT THIS REPLACES A HUMAN FOR. "Yeni alt görev" drew the browser's own date box, raw `<select>`s and
 * no field icons, while `Views/Tasks/_QuickCreateOffcanvas.cshtml` — the surface that creates a task one click
 * away, from the same five values — drew a flatpickr field, select2 pickers and a `.diten-field` icon on every
 * one of them. The browser's date control takes its format from the OPERATING SYSTEM, so the reader saw
 * `gg.aa.yyyy` on a Turkish page and would have seen it on an Arabic one too; the page's language never
 * entered into it. It was reported from a screenshot, not by any of the ~2000 green tests.
 *
 * ⚠ THE RULE ALREADY EXISTED AND COULD NOT SEE THIS. tasks-quick-create-golden.test.js carries "the date is a
 * flatpickr field, not the browser's own control" — but it reads a .cshtml file, and these panels are built as
 * template strings inside `WorkCenterNext/app.js`. A written rule that greps the wrong file is not a guard, so
 * this one is pointed at the surface the other one was missing, and its native-date ban covers the WHOLE of
 * app.js rather than one function inside it.
 *
 * ⚠ MARKUP IS ONLY HALF OF A CONTROL. `.flatpickr-date` with nothing bound to it is a calendar icon over a
 * plain text box — the exact defect `shared/diten-datefield.js` was extracted to stop repeating, and one this
 * project has already shipped once. So the classes are read from the source AND the binding is proved by
 * opening the panel for real.
 *
 * WHAT IS DERIVED RATHER THAN RESTATED: every glyph below is read out of the standard's own markup. A change
 * to the standard therefore moves this test with it, instead of leaving it asserting last round's icons.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const APP = () => read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
/** The STANDARD: the quick-create shortcut, whose controls are the full form's (its own header says why). */
const QUICK = () => read("Views", "Tasks", "_QuickCreateOffcanvas.cshtml");
/** …and the full form itself, for the one field the shortcut does not have. */
const FORM = () => read("Views", "Tasks", "_Form.cshtml");

/*
 * Comments are STRIPPED for the structural claims. The create panel now explains its own icon map at length,
 * and a guard that reads prose passes on a SENTENCE about a class instead of the class — a trap this repo has
 * fallen into three times. The native-date ban is the deliberate mirror image: it greps the RAW file, and the
 * file keeps that attribute out of its prose (as the .cshtml does) so that the strictest reading can stand.
 */
const stripComments = (source) => source.replace(/\/\*[\s\S]*?\*\//g, "");

/**
 * The nearest icon wrapper that is still OPEN at the control — the same technique diten-field-icons.test.js
 * uses, and for the same reason: a plain backwards search finds the PREVIOUS field's wrapper, already closed,
 * and reports an icon this control does not have.
 */
const iconWrapper = (source, marker) => {
  const at = source.indexOf(marker);
  if (at < 0) { return null; }
  const openedAt = source.lastIndexOf('class="diten-field', at);
  if (openedAt < 0) { return null; }
  const between = source.slice(openedAt, at);
  return between.includes("</div>") ? null : between;
};

/** The glyph a surface puts on one field, READ from that surface. */
const glyphOf = (source, marker) => {
  const wrapper = iconWrapper(source, marker);
  const found = wrapper && /\bbx (bx-[a-z0-9-]+) diten-field-icon/.exec(wrapper);
  return found ? found[1] : null;
};

/**
 * The standard, field by field. Four come from the shortcut, which is the panel's twin; the description comes
 * from the full form, which is where the shortcut's glyphs come from in the first place.
 */
const standardGlyphs = () => ({
  title: glyphOf(QUICK(), 'id="quickTitle"'),
  assignee: glyphOf(QUICK(), 'id="quickAssignee"'),
  due: glyphOf(QUICK(), 'id="quickDueAt"'),
  priority: glyphOf(QUICK(), 'id="quickPriority"'),
  description: glyphOf(FORM(), 'id="taskDescription"')
});

/** One panel, cut out of app.js by its offcanvas id and ending where its body does. */
const panelSource = (id) => {
  const src = stripComments(APP());
  const at = src.indexOf(`id="${id}"`);
  if (at < 0) { return ""; }
  return src.slice(at, src.indexOf("offcanvas-footer", at));
};

const CREATE = () => panelSource("wcnSubtaskCreatePanel");
const EDIT = () => panelSource("wcnSubtaskPanel");

/** A function's source, from its declaration to the next one — the idiom wcn-detail-three-regions.test.js uses. */
const fnBody = (name, end) => {
  const src = stripComments(APP());
  const start = src.indexOf(`const ${name} =`);
  return start < 0 ? "" : src.slice(start, src.indexOf(`const ${end} =`, start));
};

// ── 1. the standard can be read ─────────────────────────────────────────────

describe("the standard this panel is measured against actually exists", () => {
  /*
   * NON-VACUITY. Every assertion below compares the panel to a glyph read out of another file. If that read
   * ever returns null — a renamed id, a restructured wrapper — the comparisons would quietly become
   * "undefined matches undefined" and this whole file would pass on a panel with no icons at all.
   */
  it("names a glyph for each of the five fields", () => {
    Object.entries(standardGlyphs()).forEach(([field, glyph]) => {
      expect(glyph, `the standard's ${field} glyph could not be read`).toBeTruthy();
      expect(glyph, `the standard's ${field} glyph is not a boxicon`).toMatch(/^bx-/);
    });
  });

  it("the panels can be found in app.js", () => {
    expect(CREATE(), "the create panel moved or was renamed").toContain("wcnNewSubtaskDue");
    expect(EDIT(), "the quick-edit panel moved or was renamed").toContain("wcnSubtaskDue");
  });
});

// ── 2. the fields of "Yeni alt görev" ───────────────────────────────────────

describe("the new-subtask panel is built from the product's own controls", () => {
  // control marker -> which glyph of the standard it must wear.
  const FIELDS = {
    'id="wcnNewSubtaskTitle"': "title",
    'id="wcnNewSubtaskAssignee"': "assignee",
    'id="wcnNewSubtaskDue"': "due",
    'id="wcnNewSubtaskPriority"': "priority",
    'id="wcnNewSubtaskDesc"': "description"
  };

  it("wraps every field in .diten-field and gives it the standard's glyph", () => {
    const panel = CREATE();
    const standard = standardGlyphs();
    Object.entries(FIELDS).forEach(([marker, field]) => {
      const wrapper = iconWrapper(panel, marker);
      expect(wrapper, `${marker} has no open .diten-field wrapper`).toBeTruthy();
      expect(wrapper, `${marker} has no .diten-field-icon`).toContain("diten-field-icon");
      expect(wrapper, `${marker} does not wear the standard's ${standard[field]}`)
        .toMatch(new RegExp(`${standard[field]}(?![a-z-])`));
    });
  });

  it("the date is a flatpickr field, not the browser's own control", () => {
    const panel = CREATE();
    // Both spellings of the same control: the attribute in markup, and the property set from script.
    expect(panel, "a native date control remains in the panel").not.toMatch(/type="date"/);
    const tag = /<input[^>]*id="wcnNewSubtaskDue"[\s\S]{0,240}?>/.exec(panel);
    expect(tag, "the due-date input is gone").toBeTruthy();
    expect(tag[0], "the due date is not a flatpickr field").toContain("flatpickr-date");
  });

  it("every picker is marked for select2, like the standard's", () => {
    const selects = [...CREATE().matchAll(/<select class="([^"]*)"[\s\S]{0,200}?id="(wcnNewSubtask[A-Za-z]+)"/g)];
    expect(selects.length, "the panel's pickers moved").toBeGreaterThanOrEqual(2);
    selects.forEach(([, cls, id]) => {
      expect(cls, `${id} is a raw form-select`).toContain("select2");
    });
  });

  it("the labels carry the standard's weight", () => {
    // Derived from the standard, never restated: a label class changed there has to change here too.
    const golden = /class="form-label([^"]*)"/.exec(QUICK())[1].trim();
    expect(golden, "the standard's label class changed shape").toBeTruthy();

    const labels = [...CREATE().matchAll(/<label class="([^"]*)"/g)].map((m) => m[1]);
    expect(labels.length, "there are no labels to check").toBe(5);
    labels.forEach((cls) => expect(cls, `a label is missing "${golden}"`).toContain(golden));
  });
});

// ── 3. the ban now covers the file the panels are built in ──────────────────

describe("no surface of the Task Center draws the browser's own date control", () => {
  it("app.js draws none in its markup", () => {
    /*
     * ⚠ THE RAW FILE, comments and all. The panels' own prose deliberately does not spell the attribute out —
     * the same discipline _QuickCreateOffcanvas.cshtml keeps — precisely so that this can be the strictest
     * possible reading. A guard that has to strip comments first is a guard a comment can fool.
     */
    expect(APP().match(/type="date"/g), "a native date input is back in app.js").toBeNull();
  });

  it("…and where a date type IS set from script, it is only ever flatpickr's fallback", () => {
    /*
     * MEASURED, and the reason this is a rule about ORDER rather than a second ban: three dialogs on this page
     * (Planla, Ertele, toplantı) construct flatpickr and set a native type only in the `else`, for a page that
     * never loaded the library. There the OS control is the better of two bad answers — the alternative is a
     * plain text box. What is forbidden is reaching for it FIRST, which is what the subtask panels did.
     */
    const src = APP();
    const assignments = [...src.matchAll(/\.type\s*=\s*['"](date|datetime-local)['"]/g)];
    expect(assignments.length, "the script-set date fields moved — this now checks nothing").toBeGreaterThan(0);

    assignments.forEach((match) => {
      const before = src.slice(Math.max(0, match.index - 500), match.index);
      expect(before, `a native ${match[1]} control is set with no flatpickr branch above it`)
        .toMatch(/if \(global\.flatpickr\) \{/);
      expect(before, `the native ${match[1]} control is not the fallback branch`).toMatch(/\} else \{/);
    });
  });

  it("…and the file still HAS date fields, so the ban is not passing on an empty file", () => {
    // Without this, deleting both panels' date inputs would turn the assertion above green.
    const fields = APP().match(/flatpickr-date/g) || [];
    expect(fields.length, "no date field is left to guard").toBeGreaterThanOrEqual(2);
  });

  it("the quick-EDIT panel keeps the same control, so the pair cannot drift apart", () => {
    /*
     * The two subtask panels sit 150 lines apart in one file and had the identical defect. Fixing one and
     * leaving the other is how a surface ends up with two date controls for one kind of value.
     */
    const wrapper = iconWrapper(EDIT(), 'id="wcnSubtaskDue"');
    expect(wrapper, "the quick-edit date has no icon wrapper").toBeTruthy();
    expect(wrapper, "the quick-edit date has no calendar icon")
      .toMatch(new RegExp(`${standardGlyphs().due}(?![a-z-])`));
    const tag = /<input[^>]*id="wcnSubtaskDue"[\s\S]{0,240}?>/.exec(EDIT());
    expect(tag[0], "the quick-edit date is not a flatpickr field").toContain("flatpickr-date");
  });
});

// ── 4. the classes are BOUND, not decoration ────────────────────────────────

describe("the panels reach the full form's enhancers instead of rolling their own", () => {
  it("the shared plumbing binds both, and the panels share that plumbing", () => {
    const plumbing = fnBody("showPanel", "SORTERS");
    expect(plumbing, "the panel is shown but nothing is enhanced").toContain("enhancePanelControls(");

    const enhancer = fnBody("enhancePanelControls", "showPanel");
    expect(enhancer, "select2 is never bound").toMatch(/TaskForm[\s\S]{0,120}enhanceSelects/);
    expect(enhancer, "flatpickr is never bound").toMatch(/TaskForm[\s\S]{0,160}enhanceDates/);
    expect(enhancer, "a page without Tasks/form.js would throw here").toMatch(/if \(!node \|\| !form\)/);

    // The second panel had its own copy of showPanel, which is how it would keep its own controls too.
    expect(stripComments(APP()), "the quick-edit panel plumbs itself again")
      .toMatch(/const showSubtaskPanel = \(\) => showPanel\(/);

    // Nothing is constructed here: the components own their construction (BL — diten-datefield.js).
    expect(stripComments(APP()), "the panel builds its own picker").not.toMatch(/\.flatpickr\(\{|new\s+Tagify/);
  });
});

/*
 * ── AND THE SAME CLAIM, MEASURED THROUGH THE DOM ───────────────────────────────────────────────────────────
 *
 * The assertions above read source. They would all stay green if `showPanel` were never called for this panel,
 * or if the enhancement ran against the wrong node — so the panel is opened for real below and asked what it
 * actually did.
 */
const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

/*
 * The projection item, copied from workcenter-next-detail-page.test.js because the fixture is not exported.
 * `subtasks` is the capability that draws the "+ ayrıntılı" door this file clicks; the contract check inside
 * bootSurface fails loudly if this shape ever stops being something a provider could send.
 */
const projectionItem = () => ({
  fixtureKind: "workItem",
  id: TASK_ID,
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "InProgress",
  taskLifecycle: "InProgress",
  executionState: "active",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: "Yeni maliyet merkezi açılış talebi", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "subtasks"],
  subtasks: { mode: "full", items: [] },
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-30T00:00:00+00:00"
});

/** Wait for a CONDITION with a ceiling — never for a fixed number of ticks (BL-168). */
const until = async (predicate, { timeout = 2000, step = 5 } = {}) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (predicate()) { return; }
    await new Promise((resolve) => setTimeout(resolve, step));
  }
  throw new Error("until(): condition never became true");
};

describe("opening the panel binds its controls, and a late lookup tells the picker", () => {
  let calls;

  const bootDetail = async ({ withEnhancers = true, people = [] } = {}) => {
    /*
     * The PREVIOUS boot's document listeners are removed first. Nothing else in the suite does this and nothing
     * else needs to: every other test asserts on the DOM, where a second live instance re-renders the same
     * fixture harmlessly. Here the claim is "how many times was the enhancer called, and did an event fire at
     * all", and a leftover instance answering the same click would make both numbers meaningless.
     */
    if (typeof global.__wcnTeardown === "function") { global.__wcnTeardown(); }

    // jsdom runs no Bootstrap, and `showPanel` refuses to do anything without it — which would make every
    // assertion below pass for the wrong reason. Only the two methods the panel plumbing actually calls.
    global.bootstrap = {
      Offcanvas: {
        getOrCreateInstance: () => ({ show: () => {}, hide: () => {} }),
        getInstance: () => null
      }
    };

    const recorded = await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
      items: [projectionItem()]
    });

    calls = { selects: [], dates: [] };
    global.TasksApi.assignablePeople = () => Promise.resolve({ ok: true, data: people });
    if (withEnhancers) {
      global.TaskForm = Object.assign({}, global.TaskForm, {
        enhanceSelects: (root) => {
          calls.selects.push(root);
          /*
           * The real `enhanceSelects` hands the node to select2, and select2 stamps `select2-hidden-accessible`
           * on it. That class is the flag `fillAssigneeSelect` keys on to decide whether anything needs telling,
           * so the stub reproduces exactly that and nothing else.
           */
          root.querySelectorAll("select.select2").forEach((node) => {
            node.classList.add("select2-hidden-accessible");
          });
        },
        enhanceDates: (root) => { calls.dates.push(root); }
      });
    }
    return recorded;
  };

  const openCreatePanel = async () => {
    app().querySelector("[data-wcn-subtask-add-detailed]").click();
    await until(() => !!document.querySelector('[data-wcn-newsubtask-field="title"]'));
  };

  it("hands the PANEL to both of the full form's enhancers", async () => {
    await bootDetail();
    await openCreatePanel();
    await until(() => calls.dates.length > 0);

    expect(calls.selects.map((node) => node.id), "select2 was never bound to the panel")
      .toEqual(["wcnSubtaskCreatePanel"]);
    expect(calls.dates.map((node) => node.id), "flatpickr was never bound to the panel")
      .toEqual(["wcnSubtaskCreatePanel"]);
  });

  it("tells the drawn picker when the people lookup lands underneath it", async () => {
    /*
     * The panel opens BEFORE the lookup resolves (deliberately — a slow people service must not leave the
     * reader with a button that does nothing), so select2 is bound to an empty picker and the options are
     * written into the <select> afterwards. select2 re-reads the option list on every open, but the box it
     * draws over the control is only redrawn from a `change` on the element. Without that event the reader
     * sees an empty picker over a full list.
     */
    const seen = [];
    const listener = (event) => {
      if (event.target && event.target.id === "wcnNewSubtaskAssignee") { seen.push(event.target.value); }
    };
    document.addEventListener("change", listener);
    try {
      await bootDetail({ people: [{ userId: "u-merve", displayName: "Merve" }] });
      await openCreatePanel();
      await until(() => !!document.querySelector('#wcnNewSubtaskAssignee option[value="u-merve"]'));

      expect(seen.length, "the drawn picker was never told the options changed").toBeGreaterThan(0);
    } finally {
      document.removeEventListener("change", listener);
    }
  });

  it("still sends the same plain ISO date the browser's control used to send", async () => {
    /*
     * ⚠ THE HALF A CONTROL SWAP CAN BREAK SILENTLY. The panel changed which KIND of control collects the due
     * date; what must not change is the value that leaves it, because the endpoint refuses anything else
     * (`400 VALIDATION_REQUEST_DUE_AT_NOT_NULL` for an empty one, and the create form sends `Y-m-d`).
     * DitenDateField constructs flatpickr with `dateFormat: 'Y-m-d'` for exactly this reason.
     *
     * The event is a `change`, not a keystroke: a PICKED date arrives that way — measured in the vendored
     * library, `triggerEvent('onChange')` dispatches `change` and then `input` on the wrapped element — and the
     * panel's draft is fed from both.
     */
    const { created } = await bootDetail();
    await openCreatePanel();

    const title = document.querySelector('[data-wcn-newsubtask-field="title"]');
    title.value = "Ekstre";
    title.dispatchEvent(new window.Event("input", { bubbles: true }));

    const due = document.getElementById("wcnNewSubtaskDue");
    due.value = "2026-09-30";
    due.dispatchEvent(new window.Event("change", { bubbles: true }));

    app().querySelector("[data-wcn-newsubtask-save]").click();
    await until(() => created.length > 0);

    expect(created[created.length - 1].dueAt, "the picked date never reached the payload").toBe("2026-09-30");
  });

  it("says nothing when no picker was drawn — the event is not fired blindly", async () => {
    /*
     * NON-VACUITY for the test above, and the reason the notification is conditional: on a page whose host view
     * never loaded Tasks/form.js there is no select2 over the control, and a change event nobody asked for is
     * an event some other listener will act on.
     */
    const seen = [];
    const listener = (event) => {
      if (event.target && event.target.id === "wcnNewSubtaskAssignee") { seen.push(event.target.value); }
    };
    document.addEventListener("change", listener);
    try {
      await bootDetail({ withEnhancers: false, people: [{ userId: "u-merve", displayName: "Merve" }] });
      await openCreatePanel();
      await until(() => !!document.querySelector('#wcnNewSubtaskAssignee option[value="u-merve"]'));

      expect(seen, "a change was announced for a picker that does not exist").toEqual([]);
    } finally {
      document.removeEventListener("change", listener);
    }
  });
});
