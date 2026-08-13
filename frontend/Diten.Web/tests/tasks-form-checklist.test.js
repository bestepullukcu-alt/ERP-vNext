const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * The checklist on the CREATE form — the way in that never existed.
 *
 * Everything behind a checklist had shipped: the entities, the add/tick commands, the projection, the tick
 * control, and the completion gate that refuses to close a task with an open BLOCKING item. There was no screen
 * anywhere that could write one, so a checklist could stop a task from closing and nobody could create the thing
 * doing the stopping.
 */
const FORM = fs.readFileSync(
  path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml"), "utf8");

describe("the create form has a checklist card at all", () => {
  it("draws the card, its input and its list", () => {
    expect(FORM).toContain('id="taskChecklistCard"');
    expect(FORM).toContain('id="taskChecklistInput"');
    expect(FORM).toContain('id="taskChecklistItems"');
  });

  it("uses the form's own 38px field shape, not a bespoke one", () => {
    // .diten-field with the icon INSIDE — the same control every other field on this form uses. A box that
    // looked different here would read as belonging to a different screen.
    const card = FORM.slice(FORM.indexOf('id="taskChecklistCard"'), FORM.indexOf('id="taskCustomFields"'));
    expect(card).toContain("diten-field");
    expect(card).toContain("diten-field-icon");
  });

  it("draws NO template button, because nothing can list templates", () => {
    /*
     * ChecklistTemplate exists in the model and IChecklistTemplateRepository.ListActiveAsync has ZERO callers —
     * no query handler, no controller endpoint, no management screen. A "from a template" button would open a
     * list that cannot be fetched, which is the dead control this project has removed more than once.
     */
    const card = FORM
      .slice(FORM.indexOf('id="taskChecklistCard"'), FORM.indexOf('id="taskCustomFields"'))
      // Razor comments stripped: the card's own comment EXPLAINS the absent button at length, and scanning
      // prose would fail on the explanation for the very thing it is asserting.
      .replace(/@\*[\s\S]*?\*@/g, "");

    expect(card).not.toMatch(/[Tt]emplate/);
  });

  it("is always visible rather than hidden until used", () => {
    // The configurable-fields card below it is born `d-none` because its content is tenant configuration the
    // author cannot create here. This card is the ONLY place the capability can be discovered, and a feature
    // nobody can find is the state this round exists to end.
    const openingTag = FORM.slice(FORM.indexOf('<section class="card mb-4" id="taskChecklistCard"'));
    expect(openingTag.slice(0, 80)).not.toContain("d-none");
  });
});

describe("the checklist editor builds rows and reads them back", () => {
  let TaskForm;
  const t = (key) => key;

  beforeEach(() => {
    document.body.innerHTML = '<ul id="list"></ul>';
    delete global.TaskForm;
    // The row component, loaded exactly as every page that draws a row loads it. form.js delegates to it, so a
    // harness without it is testing a form that could not run.
    loadScript("wwwroot/assets/js/shared/diten-checkitem.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    TaskForm = global.TaskForm;
  });

  const list = () => document.getElementById("list");
  const render = (items) => TaskForm.renderChecklistItems(list(), items, t);

  it("defaults a new item to Optional, and says why in the code", () => {
    /*
     * A Blocking default manufactures tasks that cannot be closed by people who never chose that. Optional is
     * wrong in the least damaging direction: a missed expectation is visible and fixable, a task nobody can
     * close is neither.
     */
    expect(TaskForm.CHECKLIST_DEFAULT_LEVEL).toBe("Optional");
  });

  it("carries the levels in the ENGINE's spelling, because they cross the wire", () => {
    // Lowercase or localized here is a 400 before the handler runs — the enum-on-the-wire defect this module
    // has already shipped twice.
    expect(TaskForm.CHECKLIST_LEVELS).toEqual(["Optional", "Required", "Blocking"]);
  });

  it("cycles weakest-first so pressing walks toward the strict end", () => {
    expect(TaskForm.nextChecklistLevel("Optional")).toBe("Required");
    expect(TaskForm.nextChecklistLevel("Required")).toBe("Blocking");
    expect(TaskForm.nextChecklistLevel("Blocking")).toBe("Optional");
    // A value from a stale draft must not strand the control.
    expect(TaskForm.nextChecklistLevel("nonsense")).toBe("Optional");
  });

  it("reads back exactly what was rendered, in order", () => {
    render([
      { text: "Fatura eki yüklendi", requirement: "Blocking", evidenceRequired: true },
      { text: "Muhasebe onayı alındı", requirement: "Required" },
      { text: "Arşive kopya bırakıldı" }
    ]);

    expect(TaskForm.readChecklistItems(list())).toEqual([
      { text: "Fatura eki yüklendi", requirement: "Blocking", evidenceRequired: true },
      { text: "Muhasebe onayı alındı", requirement: "Required", evidenceRequired: false },
      { text: "Arşive kopya bırakıldı", requirement: "Optional", evidenceRequired: false }
    ]);
  });

  it("the DOM's order IS the order — moving a row moves the data", () => {
    // There is no parallel array to keep in step, which is the whole reason the list is the single source of
    // truth: two copies of one fact disagree the first time a row moves.
    render([{ text: "bir" }, { text: "iki" }]);
    const rows = list().querySelectorAll("[data-diten-check-row]");
    list().insertBefore(rows[1], rows[0]);

    expect(TaskForm.readChecklistItems(list()).map((i) => i.text)).toEqual(["iki", "bir"]);
  });

  it("an item's text is TEXT — markup in it can never become markup", () => {
    render([{ text: "<img src=x onerror=alert(1)>" }]);

    const row = list().querySelector(".diten-checkitem-text");
    expect(row.querySelector("img")).toBeNull();
    expect(row.textContent).toBe("<img src=x onerror=alert(1)>");
  });

  it("drops blank rows and repairs a level a stale draft cannot justify", () => {
    // A restored draft is JSON from another session and may hold anything; an unknown level must collapse here
    // rather than travel to the server to be rejected there.
    const items = TaskForm.normalizeChecklistItems([
      { text: "  " },
      { text: "gerçek", requirement: "Whatever" },
      null
    ]);

    expect(items).toEqual([{ text: "gerçek", requirement: "Optional", evidenceRequired: false }]);
  });

  it("names the level on the chip, including Optional", () => {
    // A chip that showed only the strict states would leave Optional blank, and a blank reads as "not set"
    // rather than as a deliberate choice.
    render([{ text: "a", requirement: "Optional" }, { text: "b", requirement: "Blocking" }]);

    const chips = [...list().querySelectorAll("[data-diten-check-level]")].map((c) => c.textContent);
    expect(chips).toEqual(["checklistLevelOptional", "checklistLevelBlocking"]);
  });

  it("keeps the very same row element when only its level changes", () => {
    /*
     * FOUND BY A LIVE CLICK, not by a test.
     *
     * The toggles used to rebuild the whole list, which replaces every row — so the node under the pointer was
     * detached the instant it was used, and a SECOND press landed on an element no longer in the document.
     * Pressing the level chip twice left it one step short of Blocking, and pressing evidence straight after a
     * level change did nothing at all. Nothing about the list changes here, so nothing about it may be rebuilt.
     *
     * Asserted by IDENTITY: the element has to be the same object afterwards, which no amount of re-rendering
     * to the same markup can fake.
     */
    render([{ text: "a", requirement: "Optional" }]);
    const row = list().querySelector("[data-diten-check-row]");
    const chip = row.querySelector("[data-diten-check-level]");

    // What the page's handler does on a level press.
    row.dataset.requirement = TaskForm.nextChecklistLevel(row.dataset.requirement);

    expect(list().querySelector("[data-diten-check-row]")).toBe(row);
    expect(row.querySelector("[data-diten-check-level]")).toBe(chip);
    expect(row.isConnected).toBe(true);
  });

  it("offers the evidence toggle on every row, pressed only where it is set", () => {
    // Visible either way: a control that appears only once it is ON cannot be discovered before it is used.
    render([{ text: "a", evidenceRequired: true }, { text: "b" }]);

    const toggles = [...list().querySelectorAll("[data-diten-check-evidence]")];
    expect(toggles).toHaveLength(2);
    expect(toggles.map((el) => el.getAttribute("aria-pressed"))).toEqual(["true", "false"]);
  });
});

describe("the payload carries the checklist with the task", () => {
  let TaskForm;

  beforeEach(() => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    TaskForm = global.TaskForm;
  });

  const draft = (extra) => Object.assign({
    title: "Ay sonu kapanış",
    assignmentTarget: "SelfAssigned",
    dueAt: "2026-08-20"
  }, extra);

  it("sends the items in the create body — no follow-up call", () => {
    /*
     * THE HALF-CREATED DECISION. A second call has a failure mode with no good answer: the task is written, the
     * checklist is not, and the user is looking at a success message. One request removes that question rather
     * than answering it.
     */
    const payload = TaskForm.buildCreatePayload(draft({
      checklistItems: [
        { text: "Fatura eki yüklendi", requirement: "Blocking", evidenceRequired: true },
        { text: "Arşive kopya bırakıldı" }
      ]
    }));

    expect(payload.checklistItems).toEqual([
      { text: "Fatura eki yüklendi", requirement: "Blocking", evidenceRequired: true },
      { text: "Arşive kopya bırakıldı", requirement: "Optional", evidenceRequired: false }
    ]);
  });

  it("sends no sort field — the array's order is the order", () => {
    // A payload carrying its own sort key can contradict its own list, and then two readers disagree about
    // what "first" means.
    const payload = TaskForm.buildCreatePayload(draft({ checklistItems: [{ text: "bir" }, { text: "iki" }] }));

    payload.checklistItems.forEach((item) => {
      expect(Object.keys(item).sort()).toEqual(["evidenceRequired", "requirement", "text"]);
    });
  });

  it("sends an empty list rather than something the server has to interpret", () => {
    expect(TaskForm.buildCreatePayload(draft()).checklistItems).toEqual([]);
  });
});

/*
 * ITEM 15 — rows can be dragged, and the buttons stay.
 *
 * The grip is the mouse path; the up/down buttons are the WCAG 2.2 §2.5.7 single-pointer alternative AND the
 * whole keyboard story, because Sortable has no keyboard interaction of its own. Removing them the day drag
 * arrived would have traded an accessible interaction for an inaccessible one and called it an upgrade.
 */
describe("a row can be dragged without losing the way to move it by keyboard", () => {
  let TaskForm;
  const t = (key) => key;

  beforeEach(() => {
    document.body.innerHTML = '<ul id="list"></ul>';
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    TaskForm = global.TaskForm;
    TaskForm.renderChecklistItems(document.getElementById("list"), [{ text: "bir" }, { text: "iki" }], t);
  });

  const row = () => document.querySelector("[data-diten-check-row]");

  it("gives every row a grip AND both move buttons", () => {
    expect(row().querySelector("[data-diten-check-grip]")).not.toBeNull();
    expect(row().querySelectorAll("[data-diten-check-move]")).toHaveLength(2);
  });

  it("keeps the move controls as real focusable buttons — the keyboard path", () => {
    // <button> is the whole accessibility story here: Tab reaches it, Enter and Space fire it, with no extra
    // code. A div with a click handler would look identical and be unreachable.
    [...row().querySelectorAll("[data-diten-check-move]")].forEach((el) => {
      expect(el.tagName).toBe("BUTTON");
      expect(el.getAttribute("aria-label")).toBeTruthy();
    });
  });

  it("does not announce the grip, which Enter cannot operate", () => {
    // It is a surface to grab, not a second control. Announcing a handle no key can work would promise an
    // interaction that does not exist.
    const grip = row().querySelector("[data-diten-check-grip]");
    expect(grip.tagName).not.toBe("BUTTON");
    expect(grip.getAttribute("aria-hidden")).toBe("true");
    expect(grip.hasAttribute("tabindex")).toBe(false);
  });
});

describe("the create page loads Sortable from the vendor copy, not a CDN", () => {
  const CREATE = fs.readFileSync(
    path.resolve(__dirname, "..", "Views", "Tasks", "Create.cshtml"), "utf8");

  it("references the local file", () => {
    expect(CREATE).toContain("assets/vendor/libs/sortablejs/sortable.js");
  });

  it("pulls it from no external host", () => {
    /*
     * An air-gapped install breaks silently on a CDN script — the page renders, the drag simply never works —
     * and a third-party script is a supply-chain surface besides. _Layout.cshtml still does this for the
     * navigation-settings screen; that is its own backlog item, not something to copy here.
     */
    expect(CREATE).not.toMatch(/src="https?:\/\//);
  });

  it("does not load it on the edit page, where the card is removed", () => {
    const EDIT = fs.readFileSync(
      path.resolve(__dirname, "..", "Views", "Tasks", "Edit.cshtml"), "utf8");
    expect(EDIT).not.toContain("sortablejs");
  });
});

/*
 * C1 — the move controls were permanently enabled, in every row, always.
 *
 * Measured live on /Tasks/Create: a ONE-item list offered ↑ and ↓ that could do nothing; in a three-item list
 * the first row's ↑ and the last row's ↓ were both live and inert. Nothing on the card was ever disabled.
 *
 * These tests DERIVE the expectation from each row's position rather than naming indices, so they hold for a
 * list of any length and a list that has just been reordered — which is the case that actually breaks, because
 * a reorder deliberately moves nodes instead of rebuilding them.
 */
describe("a move control is live only where it has somewhere to go", () => {
  let TaskForm;
  const t = (key) => key;

  beforeEach(() => {
    document.body.innerHTML = '<ul id="list"></ul>';
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    TaskForm = global.TaskForm;
  });

  const list = () => document.getElementById("list");
  const render = (n) =>
    TaskForm.renderChecklistItems(list(), Array.from({ length: n }, (_, i) => ({ text: `m${i}` })), t);

  /** What each row's arrows SHOULD be, read off its position — never off a remembered index. */
  const expectedStates = () => {
    const rows = [...list().querySelectorAll("[data-diten-check-row]")];
    return rows.map((_, index) => ({ up: index === 0, down: index === rows.length - 1 }));
  };

  const actualStates = () =>
    [...list().querySelectorAll("[data-diten-check-row]")].map((row) => ({
      up: row.querySelector('[data-diten-check-move="up"]').disabled,
      down: row.querySelector('[data-diten-check-move="down"]').disabled
    }));

  it.each([2, 3, 5])("disables exactly the arrows with nowhere to go (%i rows)", (n) => {
    render(n);
    expect(actualStates()).toEqual(expectedStates());
  });

  it("hides the move controls entirely when there is only one row", () => {
    /*
     * Hidden, not disabled. A greyed arrow still says "this list can be reordered", which a one-item list
     * cannot do — and dragging a single row is equally meaningless, so the grip goes with them.
     */
    render(1);
    const row = list().querySelector("[data-diten-check-row]");

    expect(row.querySelector(".diten-checkitem-move").classList.contains("d-none")).toBe(true);
    expect(row.querySelector("[data-diten-check-grip]").classList.contains("d-none")).toBe(true);
  });

  it("brings them back as soon as a second row exists", () => {
    // Non-vacuity for the rule above: hiding them permanently would pass that test and break the card.
    render(2);

    [...list().querySelectorAll("[data-diten-check-row]")].forEach((row) => {
      expect(row.querySelector(".diten-checkitem-move").classList.contains("d-none")).toBe(false);
      expect(row.querySelector("[data-diten-check-grip]").classList.contains("d-none")).toBe(false);
    });
  });

  it("re-derives the states after a reorder, without rebuilding the rows", () => {
    /*
     * THE CASE THAT ACTUALLY BREAKS. A reorder moves nodes rather than replacing them (so the button under the
     * pointer survives a second press), which means nothing recalculates on its own: the row pushed to the top
     * would keep a live ↑ that can no longer do anything.
     */
    render(3);
    const rows = [...list().querySelectorAll("[data-diten-check-row]")];
    const last = rows[2];

    // What the page's move handler does: physically relocate the node, then re-derive.
    list().insertBefore(last, rows[0]);
    TaskForm.applyChecklistPositions(list());

    expect(actualStates()).toEqual(expectedStates());
    // …and it is the SAME element, not a rebuilt one.
    expect(list().querySelector("[data-diten-check-row]")).toBe(last);
  });

  it("re-derives them after a removal too", () => {
    // Removing down to a single row has to withdraw the controls, and removing the last row has to move the
    // disabled ↓ up to whoever is last now.
    render(3);
    [...list().querySelectorAll("[data-diten-check-row]")][2].remove();
    TaskForm.applyChecklistPositions(list());
    expect(actualStates()).toEqual(expectedStates());

    [...list().querySelectorAll("[data-diten-check-row]")][1].remove();
    TaskForm.applyChecklistPositions(list());
    expect(list().querySelector(".diten-checkitem-move").classList.contains("d-none")).toBe(true);
  });
});

/*
 * C2 / C3 — the two presentation rules, asserted on the markup that ships.
 */
describe("the evidence notice is an alert, and every card names itself with a glyph", () => {
  const CREATE_FORM = fs.readFileSync(
    path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml"), "utf8");

  it("renders the evidence notice as an alert rather than body text", () => {
    // It reports a condition the reader did not create and cannot yet act on — body text makes such a sentence
    // read as description. Same shape the completion gate and the history gap already use.
    const hint = /<div class="alert[^"]*dt-inline-alert[^"]*"[^>]*data-task-checklist-evidence-hint/.exec(CREATE_FORM);
    expect(hint).not.toBeNull();
    expect(CREATE_FORM).toContain("ChecklistEvidenceHint");
  });

  it("gives every card heading on the form an icon", () => {
    /*
     * The right-hand governance cards have had one since they were built; the left column had none, so the two
     * halves of one form spoke different languages. Derived from the markup: every <h6> that heads a card must
     * carry a glyph, so a card added later without one fails here.
     */
    const headings = [...CREATE_FORM.matchAll(/<h6[^>]*class="[^"]*(?:text-heading|card-section-title)[^"]*"[^>]*>([\s\S]*?)<\/h6>/g)];
    expect(headings.length).toBeGreaterThan(8);

    headings.forEach(([, inner]) => {
      expect(inner, `a card heading has no icon: ${inner.trim().slice(0, 60)}`).toMatch(/<i class="bx bx-[a-z-]+/);
    });
  });

  it("never repeats a FIELD's glyph as the CARD's glyph in the same card", () => {
    /*
     * The rule the field icons were given a round earlier, applied one level up: the card's icon says WHICH
     * QUESTION this answers, a field's says WHICH VALUE goes here. The same picture for both makes the heading
     * read as a repeat of the first row.
     */
    const cards = CREATE_FORM.split(/<section class="card/).slice(1);
    expect(cards.length).toBeGreaterThan(5);

    cards.forEach((card) => {
      const head = /<h6[\s\S]*?<\/h6>/.exec(card);
      if (!head) { return; }
      const cardGlyph = /<i class="bx (bx-[a-z-]+)/.exec(head[0]);
      if (!cardGlyph) { return; }

      const fieldGlyphs = [...card.slice(head[0].length).matchAll(/<i class="bx (bx-[a-z-]+)[^"]*diten-field-icon/g)]
        .map((m) => m[1]);
      expect(fieldGlyphs, `${cardGlyph[1]} is both the card's icon and a field's`).not.toContain(cardGlyph[1]);
    });
  });
});
