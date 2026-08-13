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

  it("gives the WORKING mode a tick box and the AUTHORING mode a grip", () => {
    // The tick is the only control the create form lacks, and for a plain reason: there is no task yet to have
    // made progress on. The grip is the mouse's drag surface, which an unsaved list is the only place to need.
    const working = DitenCheckItem.row(
      { id: "t:1", text: "x", requirement: "Optional" }, { mode: "working", labels: LABELS });
    const authoring = DitenCheckItem.row(
      { text: "x", requirement: "Optional" }, { mode: "authoring", labels: LABELS });

    expect(working.querySelector(".diten-checkitem-box")).not.toBeNull();
    expect(working.querySelector("[data-diten-check-grip]")).toBeNull();
    expect(authoring.querySelector(".diten-checkitem-box")).toBeNull();
    expect(authoring.querySelector("[data-diten-check-grip]")).not.toBeNull();
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

  it("keeps the wire vocabulary — the level values the server stores — untouched", () => {
    // The display word changed to "expected" a round ago; `Required` on the wire did not, and this round adds
    // three endpoints that send it back. A rename here would be a migration, not a fix.
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    expect(app).toContain("const order = ['Optional', 'Required', 'Blocking'];");
  });
});
