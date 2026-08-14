/**
 * One checklist row, one component, two modes.
 *
 * The same object was drawn twice — `task-checklist-*` in the create form, `wcn-check*` on the task detail page —
 * and the two had drifted exactly as far as you would expect. The create form could level an item, flag it for
 * evidence, reorder it and remove it; the detail page could tick a box and nothing else, and its paperclip was
 * not a control at all, only a mark reporting a decision made somewhere the reader could not reach.
 *
 * Copying one screen's markup into the other would have closed the gap for a day. This session paid that bill
 * twice already: the tag box drawn in two bundles until they disagreed, and four separate visual defects that
 * turned out to be one class name colliding with a component nobody had opened in months.
 *
 * So these tests are about SHAPE, not pixels: that both screens go through one factory, that neither keeps a
 * private copy of the row, and that the mode is the only thing that differs.
 */
const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

const read = (p) => fs.readFileSync(path.resolve(__dirname, "..", p), "utf8");
const COMPONENT = "wwwroot/assets/js/shared/diten-checkitem.js";

const LABELS = {
  optional: "Optional", required: "Expected", blocking: "Blocking",
  levelHint: "hint", moveUp: "Up", moveDown: "Down",
  evidenceToggle: "Evidence", remove: "Remove", toggle: "Tick"
};

// The component loaded exactly as every page loads it — not a re-implementation of it in the test.
const load = () => {
  loadScript(COMPONENT);
  return global.DitenCheckItem || window.DitenCheckItem;
};

describe("both screens draw the row through one component", () => {
  let DitenCheckItem;
  beforeEach(() => { DitenCheckItem = load(); });

  it("gives the WORKING mode a tick box, and BOTH modes a grip", () => {
    /*
     * The tick is the only control the create form lacks, and for a plain reason: there is no task yet to have
     * made progress on.
     *
     * The grip used to be authoring-only, so the same row could be dragged on the create form and not on the
     * task detail page. Two components could have explained that; one cannot. The grip is drawn in both, and
     * each page decides whether to attach Sortable to it.
     */
    const working = DitenCheckItem.row(
      { id: "t:1", text: "x", requirement: "Optional" }, { mode: "working", labels: LABELS });
    const authoring = DitenCheckItem.row(
      { text: "x", requirement: "Optional" }, { mode: "authoring", labels: LABELS });

    expect(working.querySelector(".diten-checkitem-box")).not.toBeNull();
    expect(authoring.querySelector(".diten-checkitem-box")).toBeNull();
    ["working", "authoring"].forEach((mode) => {
      const el = mode === "working" ? working : authoring;
      expect(el.querySelector("[data-diten-check-grip]"), mode).not.toBeNull();
      // Not a second control: a handle Enter cannot operate must not be announced or tabbed to.
      expect(el.querySelector("[data-diten-check-grip]").getAttribute("aria-hidden"), mode).toBe("true");
      expect(el.querySelector("[data-diten-check-grip]").tagName, mode).toBe("SPAN");
    });
  });

  it("gives BOTH modes the level, the evidence toggle, move and remove", () => {
    /*
     * This is the whole point of the round. Three of these were create-form-only, so a level chosen before the
     * work started could never be revised by the person actually doing it — and `EvidenceRequired`,
     * `Requirement` and the item's own text were stored from birth and then frozen.
     */
    ["working", "authoring"].forEach((mode) => {
      const el = DitenCheckItem.row(
        { id: "t:1", text: "x", requirement: "Optional" }, { mode, labels: LABELS });
      expect(el.querySelector(".diten-checkitem-level"), mode).not.toBeNull();
      expect(el.querySelector(".diten-checkitem-evidence"), mode).not.toBeNull();
      expect(el.querySelectorAll("[data-diten-check-move]"), mode).toHaveLength(2);
      expect(el.querySelector(".diten-checkitem-remove"), mode).not.toBeNull();
    });
  });

  it("makes the evidence control a BUTTON on both, not a mark on one", () => {
    // The detail page's paperclip was an <i>. It reported a flag it had no way to write, which is the
    // "stored but inert" defect this module keeps having to fix.
    ["working", "authoring"].forEach((mode) => {
      const el = DitenCheckItem.row(
        { id: "t:1", text: "x", requirement: "Optional", evidenceRequired: true },
        { mode, labels: LABELS });
      const clip = el.querySelector(".diten-checkitem-evidence");
      expect(clip.tagName, mode).toBe("BUTTON");
      expect(clip.getAttribute("aria-pressed"), mode).toBe("true");
    });
  });

  it("names the level on the chip, including Optional", () => {
    // A chip that showed only the strict states would leave Optional blank, and a blank reads as "not set"
    // rather than as a choice somebody made.
    const of = (requirement) => DitenCheckItem.row(
      { id: "t:1", text: "x", requirement }, { mode: "working", labels: LABELS })
      .querySelector(".diten-checkitem-level").textContent;
    expect(of("Optional")).toBe("Optional");
    expect(of("Required")).toBe("Expected");
    expect(of("Blocking")).toBe("Blocking");
  });

  it("sets the text with textContent, so markup in it can never become markup", () => {
    const el = DitenCheckItem.row(
      { id: "t:1", text: '<img src=x onerror="boom()">', requirement: "Optional" },
      { mode: "working", labels: LABELS });
    expect(el.querySelector(".diten-checkitem-text").querySelector("img")).toBeNull();
    expect(el.querySelector(".diten-checkitem-text").textContent).toContain("onerror");
  });

  it("marks a template-owned item, whose text the server refuses to reword", () => {
    const el = DitenCheckItem.row(
      { id: "t:1", text: "x", requirement: "Optional", templateOwned: true },
      { mode: "working", labels: LABELS });
    expect(el.getAttribute("data-template-owned")).toBe("1");
  });

  it("writes no style attribute anywhere (FG-003)", () => {
    const el = DitenCheckItem.row(
      { id: "t:1", text: "x", requirement: "Blocking", evidenceRequired: true, done: true },
      { mode: "working", labels: LABELS });
    expect(el.querySelectorAll("[style]")).toHaveLength(0);
    expect(read(COMPONENT)).not.toMatch(/\.style\.|style="/);
  });
});

/*
 * THE LEVEL IS INFORMATION; CHANGING IT IS A PERMISSION.
 *
 * They were briefly the same thing. The ownership guard that stops a reader re-levelling somebody else's step was
 * implemented by not drawing the chip at all — so it also stopped them READING the level. The notice under the
 * list then said "4 expected items open" above rows that gave no way to tell which four, and a blocking step —
 * the one thing that can prevent the task closing — was equally silent about why.
 *
 * These tests hold the two halves apart. Every one of them fails if the read-only chip is "fixed" back into a
 * button, which is the mutation this block exists to catch.
 */
describe("somebody else's row still SAYS its level and its evidence flag", () => {
  let DitenCheckItem;
  beforeEach(() => { DitenCheckItem = load(); });

  const theirs = (extra = {}) => DitenCheckItem.row(
    { id: "t:1", text: "x", requirement: "Blocking", editable: false, ...extra },
    { mode: "working", labels: { ...LABELS, levelStatic: "Not yours", evidenceMark: "Evidence required" } });

  it("shows the level chip on a row this reader may not change", () => {
    const chip = theirs().querySelector(".diten-checkitem-level");
    expect(chip).not.toBeNull();
    expect(chip.textContent).toBe("Blocking");
  });

  it("makes that chip a SPAN — not a button, not a disabled button", () => {
    /*
     * A disabled button still offers itself: it says "this is yours, just not now", invites a hunt for the
     * permission that would light it up, and keeps an `aria-disabled` control in the tree. A span is not a
     * control at all and needs no word to say so.
     *
     * THIS IS THE MUTATION GUARD. Turning the span back into a <button> fails here, and again on the two
     * assertions below.
     */
    const chip = theirs().querySelector(".diten-checkitem-level");
    expect(chip.tagName).toBe("SPAN");
    expect(chip.hasAttribute("disabled")).toBe(false);
    expect(chip.hasAttribute("aria-disabled")).toBe(false);
  });

  it("leaves that chip unclickable and unreachable by Tab", () => {
    // Both screens' click handlers key off `data-diten-check-level`; its absence is what makes the chip inert,
    // so the chip cannot be wired up again by accident. A <span> with no tabindex takes no focus.
    const chip = theirs().querySelector(".diten-checkitem-level");
    expect(chip.hasAttribute("data-diten-check-level")).toBe(false);
    expect(chip.hasAttribute("tabindex")).toBe(false);
    expect(chip.matches("button, a, input, select, textarea, [tabindex]")).toBe(false);
  });

  it("still colours it by level, so a blocking step is findable at a glance", () => {
    // The colour comes off the ROW's data-requirement, which must survive the ownership branch — otherwise the
    // reader who cannot close the task still cannot see which row is stopping them.
    expect(theirs().getAttribute("data-requirement")).toBe("Blocking");
  });

  it("shows the paperclip as a MARK when the flag is on", () => {
    const clip = theirs({ evidenceRequired: true }).querySelector(".diten-checkitem-evidence");
    expect(clip).not.toBeNull();
    expect(clip.tagName).toBe("SPAN");
    expect(clip.hasAttribute("data-diten-check-evidence")).toBe(false);
    // Announced as the fact it states, not skipped as decoration — the flag is exactly what a screen-reader
    // user would otherwise never learn about somebody else's row.
    expect(clip.getAttribute("role")).toBe("img");
    expect(clip.getAttribute("aria-label")).toBe("Evidence required");
  });

  it("draws no paperclip at all when the flag is off", () => {
    // An off mark on a row you cannot write is a control offering itself and then refusing. The author's button
    // is the only place an off state means anything.
    expect(theirs({ evidenceRequired: false }).querySelector(".diten-checkitem-evidence")).toBeNull();
  });

  it("still withholds REMOVE, which has no informational half", () => {
    // The one act with nothing to show: you cannot "read" a deletion. Ownership still hides it outright.
    expect(theirs().querySelector(".diten-checkitem-remove")).toBeNull();
  });

  it("keeps the author's row fully operable", () => {
    // The other side of the same split — nothing above may quietly downgrade a row that IS yours.
    const mine = DitenCheckItem.row(
      { id: "t:1", text: "x", requirement: "Blocking", evidenceRequired: true, editable: true },
      { mode: "working", labels: LABELS });
    expect(mine.querySelector(".diten-checkitem-level").tagName).toBe("BUTTON");
    expect(mine.querySelector(".diten-checkitem-level").hasAttribute("data-diten-check-level")).toBe(true);
    expect(mine.querySelector(".diten-checkitem-evidence").tagName).toBe("BUTTON");
    expect(mine.querySelector(".diten-checkitem-remove")).not.toBeNull();
  });

  it("gives everyone the move controls, because order takes nothing from anybody", () => {
    // Reordering writes the WHOLE list's order, not one item's meaning (BL-095 holds the open question about
    // whether procedure order should itself be owned).
    expect(theirs().querySelectorAll("[data-diten-check-move]")).toHaveLength(2);
  });
});

describe("a move control is live only where it has somewhere to go", () => {
  let DitenCheckItem;
  const listOf = (n, opts = {}) => {
    const ul = document.createElement("ul");
    for (let i = 0; i < n; i += 1) {
      ul.appendChild(DitenCheckItem.row(
        { id: `t:${i}`, text: `row ${i}`, requirement: "Optional" },
        { mode: "working", labels: LABELS, ...opts }));
    }
    DitenCheckItem.applyMoveState(ul);
    return ul;
  };

  beforeEach(() => { DitenCheckItem = load(); });

  it("disables up on the first row and down on the last", () => {
    const ul = listOf(3);
    const up = [...ul.querySelectorAll('[data-diten-check-move="up"]')].map((b) => b.disabled);
    const down = [...ul.querySelectorAll('[data-diten-check-move="down"]')].map((b) => b.disabled);
    expect(up).toEqual([true, false, false]);
    expect(down).toEqual([false, false, true]);
  });

  it("takes a disabled arrow out of the tab order too", () => {
    // `disabled`, not a class: tabbing onto a control that cannot act is the same dead end as clicking it.
    const ul = listOf(2);
    const first = ul.querySelector('[data-diten-check-move="up"]');
    expect(first.disabled).toBe(true);
    expect(first.matches(":disabled")).toBe(true);
  });

  it("hides the controls entirely on a one-row list", () => {
    // A disabled arrow still says "this list can be reordered", which a one-item list cannot keep.
    const ul = listOf(1);
    expect(ul.querySelector(".diten-checkitem-move").classList.contains("d-none")).toBe(true);
  });

  it("keeps a CLOSED task's arrows disabled even after positions are re-derived", () => {
    /*
     * `applyMoveState` recomputes `disabled` from position on every reorder. If read-only were recorded only in
     * the property it is about to overwrite, the second render of a closed task would hand its arrows back.
     */
    const ul = listOf(3, { readOnly: true });
    DitenCheckItem.applyMoveState(ul);
    expect([...ul.querySelectorAll("[data-diten-check-move]")].every((b) => b.disabled)).toBe(true);
  });
});

describe("neither screen keeps a private copy of the row", () => {
  it("routes the create form and the detail page through the same factory", () => {
    const form = read("wwwroot/assets/js/Tasks/form.js");
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    expect(form).toContain("DitenCheckItem.row(");
    expect(app).toContain("DitenCheckItem.row(");
    /*
     * And neither hand-builds a row beside it: the old markup is gone, not merely unused. Read with COMMENTS
     * STRIPPED — the file explains at length what the row used to be, and a guard that reads prose cannot tell a
     * description of the old markup from the old markup.
     */
    const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
    expect(code(app)).not.toMatch(/<li class="wcn-check/);
    expect(code(form)).not.toMatch(/createElement\('li'\)/);
  });

  it("loads the component on every page that draws a row", () => {
    ["Views/Tasks/Create.cshtml", "Views/Tasks/Edit.cshtml", "Views/Tasks/Details.cshtml",
      "Views/WorkCenterNext/Index.cshtml", "Views/WorkCenterNext/Details.cshtml"]
      .forEach((view) => expect(read(view), view).toContain("shared/diten-checkitem.js"));
  });

  it("drags on BOTH screens, and keeps the arrows on both (WCAG 2.2 §2.5.7)", () => {
    /*
     * BL-094 said no to this and the decision changed: the reason it gave was that the two screens were two
     * components, and they are one now. The same row being draggable on the create form and not on the task
     * detail page is a difference nobody can explain to the person using it.
     *
     * The condition attached to the change is the load-bearing half: drag is ADDED to the arrows, never
     * substituted for them. The arrows are the single-pointer alternative §2.5.7 requires and the whole keyboard
     * story — Sortable has none — so a change that removes them fails here.
     */
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    const page = read("wwwroot/assets/js/Tasks/form-page.js");
    [["detail", app], ["create", page]].forEach(([name, src]) => {
      expect(src, name).toContain("Sortable.create(");
      expect(src, name).toContain("handle: '[data-diten-check-grip]'");
      // The drag image is Sortable's own: native HTML5 DnD ignores `ghostClass` and can only be driven by a
      // real hand, which makes the gesture unobservable by anything at all.
      expect(src, name).toContain("forceFallback: true");
    });
    // The component still draws both arrows in working mode — the assertion that a drag was not traded for them.
    const component = read(COMPONENT);
    expect(component).toContain("data-diten-check-move");
    expect(app).toContain("global.DitenCheckItem.applyMoveState(list)");
  });

  it("actually SHIPS the library to the pages that drag, from the local copy", () => {
    /*
     * MEASURED, and the reason this test exists: `window.Sortable` was `undefined` on the task detail page.
     * The CDN tag lives in `_Layout.cshtml`, and every one of these pages uses `_LayoutTenantShell` — so the
     * grip would have been drawn on a list nothing could grab. Binding code that never runs looks exactly like
     * working code from the inside.
     *
     * The LOCAL copy, as Views/Tasks/Create.cshtml already loads it: a second CDN consumer would deepen the
     * dependency BL-081 is about, and this way that item stays a one-line change about `_Layout` alone.
     */
    ["Views/WorkCenterNext/Details.cshtml", "Views/WorkCenterNext/Index.cshtml", "Views/Tasks/Create.cshtml"]
      .forEach((view) => {
        const src = read(view);
        expect(src, view).toContain("assets/vendor/libs/sortablejs/sortable.js");
        // Anchored on a real `src="…"`, not on the word "CDN" — these files EXPLAIN in prose why the CDN tag in
        // _Layout does not reach them, and a looser pattern reads that explanation as the thing it warns about.
        expect(src, `${view} must not reach a CDN for it`).not.toMatch(/src="https?:\/\/[^"]*sortable/i);
      });
  });

  it("computes the dropped order from the PROJECTION, not from the DOM", () => {
    // The DOM is a picture of the projection. Deriving the payload from the picture is how a rendering bug
    // becomes a stored fact — so the drop contributes an INDEX and nothing else.
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    expect(app).toMatch(/dropChecklistItem\s*=\s*\(taskId, code, newIndex\)/);
    expect(app).toContain("const codes = (item.checklist?.items || []).map((c) => c.id);");
    // The same guarded write path the arrows use: version, refusal and toast are not re-invented for the mouse.
    expect(app).toMatch(/dropChecklistItem[\s\S]{0,900}expectedVersion: Number\(item\.checklist\?\.version \?\? 0\)/);
  });

  it("keeps the wire vocabulary — the level values the server stores — untouched", () => {
    // The display word changed to "expected" a round ago; `Required` on the wire did not, and this round adds
    // three endpoints that send it back. A rename here would be a migration, not a fix.
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    expect(app).toContain("const order = ['Optional', 'Required', 'Blocking'];");
  });
});

describe("the add row takes both ways in, and keeps its level between adds", () => {
  let DitenCheckItem;
  const LABELS_ADD = { ...LABELS, addPlaceholder: "Type an item", addButton: "Add", addHint: "hint" };
  beforeEach(() => { DitenCheckItem = load(); });

  it("offers an icon, an input, a level chip, a button AND a hint — the union of the two old rows", () => {
    /*
     * Each screen had half of this and was missing the other half. The create form had the button and the hint
     * but no chip, so a level could only be set on the row AFTER adding it: one extra click per item. The task
     * detail page had the chip but neither button nor hint, so Enter was the only way to commit — and the only
     * thing that said so was the placeholder, which disappears the moment you start typing, on a keyboard where
     * Enter is not always to hand.
     */
    const el = DitenCheckItem.addRow({ id: "new", level: "Optional", labels: LABELS_ADD });
    expect(el.querySelector(".bx-list-plus")).not.toBeNull();
    expect(el.querySelector("[data-diten-check-input]")).not.toBeNull();
    expect(el.querySelector("[data-diten-check-draftlevel]")).not.toBeNull();
    expect(el.querySelector("[data-diten-check-add]").textContent).toBe("Add");
    expect(el.querySelector(".diten-checkitem-addhint").textContent).toBe("hint");
  });

  it("names the chosen level on the chip so the next add is predictable", () => {
    // Somebody entering three blocking steps chooses once, not three times — the caller keeps the value and
    // passes it back in, and the chip has to show which one is armed.
    const el = DitenCheckItem.addRow({ id: "new", level: "Blocking", labels: LABELS_ADD });
    const chip = el.querySelector("[data-diten-check-draftlevel]");
    expect(chip.textContent).toBe("Blocking");
    expect(chip.getAttribute("data-level")).toBe("Blocking");
  });

  it("sits BELOW the list on both screens", () => {
    /*
     * One component was being placed in two positions — the same drift the component was built to end, moved up
     * a layer. Below is the position both take, for two reasons:
     *
     *   PROXIMITY — with the box under the list, the item you just typed appears directly above the box you
     *   typed it in. With the box on top, it lands at the foot of a list growing away from you, and on a long
     *   list it lands off-screen.
     *   CONVENTION — Jira, Asana, Notion, Microsoft To Do and Fiori all put it at the foot, without exception.
     */
    const form = read("Views/Tasks/_Form.cshtml");
    expect(form.indexOf('id="taskChecklistItems"'))
      .toBeLessThan(form.indexOf('id="taskChecklistAddRow"'));

    // The detail page builds its card as one template string; the list must precede the add row there too.
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    const card = app.slice(app.indexOf('<ul class="wcn-checks">'));
    expect(card.indexOf('<ul class="wcn-checks">')).toBeLessThan(card.indexOf("checklistAddRow(item)"));
  });

  it("keeps the evidence notice LAST, under the add row, on both screens", () => {
    // It explains a paperclip that lives on the rows AND on the add row, so under both is where it reads as a
    // footnote rather than as an interruption cutting the card's one continuous action in half.
    const form = read("Views/Tasks/_Form.cshtml");
    expect(form.indexOf('id="taskChecklistAddRow"'))
      .toBeLessThan(form.indexOf("data-diten-check-evidence-hint"));

    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    const card = app.slice(app.indexOf('<ul class="wcn-checks">'));
    expect(card.indexOf("checklistAddRow(item)")).toBeLessThan(card.indexOf("${evidenceHint}"));
  });

  it("spaces that notice from ONE class, so the two screens cannot drift 8px apart", () => {
    // It was `mt-2` on the create form and a 1rem rule on the detail page: one sentence, two gaps, neither
    // chosen. `mt-2` here would put the difference straight back.
    const form = read("Views/Tasks/_Form.cshtml");
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    const css = read("wwwroot/assets/css/backbone-custom.css");
    expect(form).toContain("diten-checkitem-evidencehint");
    expect(app).toContain("diten-checkitem-evidencehint");
    expect(css).toContain(".diten-checkitem-evidencehint {");
    expect(form).not.toContain("dt-inline-alert mt-2");
  });

  it("keeps ENTER working on the create form after the chip is used", () => {
    /*
     * REGRESSION, found by pressing Enter on the live form and not by any test here.
     *
     * The add row is redrawn when the level chip changes, which replaces the input node. Enter had been bound
     * to that node directly, so it worked until the first chip click and then silently stopped — with the
     * button still working, so nothing looked broken. Both handlers are delegated at the CARD now, which is the
     * only part that survives the redraw.
     */
    const page = read("wwwroot/assets/js/Tasks/form-page.js");
    expect(page).toMatch(/el\('taskChecklistCard'\)\?\.addEventListener\('keydown'/);
    expect(page).not.toMatch(/input\.addEventListener\('keydown'/);
  });
});
