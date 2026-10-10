const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UX-COMMON-01 (BL-586) — Enter runs a side panel's primary action, and only where that is what the reader means.
 *
 * Measured through the real shared script against Bootstrap-shaped markup (`.offcanvas.show`), plus a scan of EVERY
 * offcanvas in Views/ so a new panel that the rule cannot read is a red test, not a silent dead key.
 */

const VIEWS = path.resolve(__dirname, "..", "Views");

beforeAll(() => {
  loadScript("wwwroot/assets/js/shared/diten-enter-primary.js");
});

afterEach(() => {
  document.body.innerHTML = "";
});

const panel = (inner, { open = true } = {}) => {
  document.body.innerHTML = `<div class="offcanvas offcanvas-end${open ? " show" : ""}" id="p">
    <div class="offcanvas-body">${inner}</div></div>`;
  const clicks = [];
  document.querySelectorAll("button").forEach((b) => b.addEventListener("click", () => clicks.push(b.id)));
  return clicks;
};

const press = (target, init = {}) => {
  const event = new KeyboardEvent("keydown", Object.assign({ key: "Enter", bubbles: true, cancelable: true }, init));
  target.dispatchEvent(event);
  return event;
};

const SAVE = '<button type="button" id="save" class="btn btn-primary">Save</button>';
const CANCEL = '<button type="button" id="cancel" class="btn btn-label-secondary">Cancel</button>';

describe("BL-586: Enter in a side panel runs its primary action", () => {
  test("Enter in a single-line field clicks the panel's one .btn-primary", () => {
    const clicks = panel(`<input id="name" type="text">${CANCEL}${SAVE}`);
    const event = press(document.getElementById("name"));
    expect(clicks).toEqual(["save"]);
    expect(event.defaultPrevented).toBe(true);
  });

  test("a data-dt-primary mark wins over .btn-primary (End-relationship panels save with btn-warning)", () => {
    const clicks = panel(`<input id="d" type="date"><button type="button" id="end" class="btn btn-warning" data-dt-primary>End</button>
      <button type="button" id="other" class="btn btn-primary">Other</button>`);
    press(document.getElementById("d"));
    expect(clicks).toEqual(["end"]);
  });

  test("of two marked buttons that take turns, the one shown now is primary (VisitExecution submit / amend)", () => {
    const clicks = panel(`<input id="n" type="text">
      <button type="button" id="submit" class="btn btn-success d-none" data-dt-primary>Submit</button>
      <button type="button" id="amend" class="btn btn-outline-warning" data-dt-primary>Amend</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["amend"]);
  });

  test("two unmarked .btn-primary: Enter is left alone (the panel needs a mark)", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}<button type="button" id="save2" class="btn btn-primary">Again</button>`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("a panel with no form fields has no Enter-primary (a quick view's single 'Edit' button is not a save)", () => {
    panel('<button type="button" id="edit" class="btn btn-primary">Edit</button>');
    expect(window.DitenEnterPrimary.primaryOf(document.getElementById("p"))).toBeNull();
  });

  test("a closed panel is not answered", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`, { open: false });
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });
});

describe("BL-586: where Enter is NOT save", () => {
  test("textarea: Enter is a new line — nothing is clicked and the key is not taken", () => {
    const clicks = panel(`<textarea id="desc"></textarea>${SAVE}`);
    const event = press(document.getElementById("desc"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("textarea: Ctrl+Enter and Cmd+Enter save", () => {
    const clicks = panel(`<textarea id="desc"></textarea>${SAVE}`);
    press(document.getElementById("desc"), { ctrlKey: true });
    press(document.getElementById("desc"), { metaKey: true });
    expect(clicks).toEqual(["save", "save"]);
  });

  test("a disabled primary button: nothing is clicked (no double submission), and no native submit either", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary" disabled>Save</button>`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(true);
  });

  test("an aria-disabled / .disabled primary is disabled too", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary disabled" aria-disabled="true">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("IME composition: Enter confirms the composed text, never saves", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"), { isComposing: true });
    press(document.getElementById("n"), { keyCode: 229 });
    expect(clicks).toEqual([]);
  });

  test("an open select2 / date picker / dropdown: Enter picks the entry, never saves", () => {
    ["select2-container select2-container--open", "flatpickr-calendar open", "dropdown-menu show"].forEach((cls) => {
      const clicks = panel(`<input id="n" type="text">${SAVE}<div class="${cls}"></div>`);
      press(document.getElementById("n"));
      expect(clicks, cls).toEqual([]);
    });
  });

  test("a field whose own list is expanded (aria-expanded) keeps Enter", () => {
    const clicks = panel(`<input id="n" type="text" aria-expanded="true">${SAVE}`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("a held key repeats nothing", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"), { repeat: true });
    expect(clicks).toEqual([]);
  });

  test("checkbox, button and select keep their own Enter", () => {
    const clicks = panel(`<input id="c" type="checkbox"><select id="s"><option>1</option></select>${CANCEL}${SAVE}`);
    press(document.getElementById("c"));
    press(document.getElementById("s"));
    press(document.getElementById("cancel"));
    expect(clicks).toEqual([]);
  });

  test("a page that gives Enter its own meaning (handler on document calls preventDefault) is respected", () => {
    const clicks = panel(`<input id="sub" type="text" data-wcn-subtask-input>${SAVE}`);
    const own = (event) => { if (event.target.id === "sub") { event.preventDefault(); } };
    document.addEventListener("keydown", own);
    try {
      press(document.getElementById("sub"));
    } finally {
      document.removeEventListener("keydown", own);
    }
    expect(clicks).toEqual([]);
  });
});

/*
 * Every offcanvas in Views/ that carries form fields must name its primary for this rule: exactly one shown
 * `data-dt-primary`, or no mark and exactly one `.btn-primary`. A panel the rule cannot read must be listed here,
 * with its reason — the list only shrinks.
 */
const UNRECOGNIZED_PANELS = {
  // Filter drawers on the FROZEN _Layout: they have no save; "Apply" is `btn-filter-apply`. The Enter module is not
  // loaded on _Layout (frozen), so these never see it.
  "DeliveryExecutionManagement/Initiatives.cshtml": "filter drawer, no save, FROZEN _Layout",
  "EnterpriseStrategyBusinessPerformance/Initiatives.cshtml": "filter drawer, no save, FROZEN _Layout",
  "EnterpriseStrategyBusinessPerformance/Objectives.cshtml": "filter drawer, no save, FROZEN _Layout"
};

const cshtmlFiles = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
  const full = path.join(dir, entry.name);
  if (entry.isDirectory()) { return cshtmlFiles(full); }
  return entry.name.endsWith(".cshtml") ? [full] : [];
});

// Each `<div class="… offcanvas …">` block, closed by balancing its <div>s.
const offcanvasBlocks = () => cshtmlFiles(VIEWS).flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  const open = /<div\b[^>]*\bclass="([^"]*)"[^>]*>/g;
  const blocks = [];
  let match;
  while ((match = open.exec(source)) !== null) {
    if (!/(^|\s)offcanvas(\s|$)/.test(match[1])) { continue; }
    const tag = /<div\b|<\/div>/g;
    tag.lastIndex = match.index;
    let depth = 0;
    let end = source.length;
    let t;
    while ((t = tag.exec(source)) !== null) {
      depth += t[0] === "</div>" ? -1 : 1;
      if (depth === 0) { end = tag.lastIndex; break; }
    }
    blocks.push({
      file: path.relative(VIEWS, file).split(path.sep).join("/"),
      line: source.slice(0, match.index).split("\n").length,
      body: source.slice(match.index, end)
    });
  }
  return blocks;
});

describe("BL-586: every side panel with fields names its primary action", () => {
  const blocks = offcanvasBlocks();
  const withFields = blocks.filter((b) => /<input\b(?![^>]*type="hidden")|<textarea\b|<select\b/.test(b.body));

  const readable = (body) => {
    const marks = (body.match(/data-dt-primary/g) || []).length;
    const primaries = (body.match(/<button\b[^>]*class="[^"]*\bbtn-primary\b/g) || []).length
      + (body.match(/<input\b[^>]*type="submit"[^>]*class="[^"]*\bbtn-primary\b/g) || []).length;
    return marks >= 1 || primaries === 1;
  };

  test("the scan reaches the panels (guards against a vacuous pass)", () => {
    expect(withFields.length).toBeGreaterThan(50);
    expect(withFields.map((b) => b.file)).toEqual(expect.arrayContaining([
      "Governance/Users/_CreateEditOffcanvas.cshtml",
      "Governance/Roles/_CreateEditOffcanvas.cshtml",
      "CRM/Accounts/_RelationshipEndOffcanvas.cshtml"
    ]));
  });

  test("a panel the rule cannot read is listed with its reason; the list only shrinks", () => {
    const unreadable = [...new Set(withFields.filter((b) => !readable(b.body)).map((b) => b.file))].sort();
    expect(unreadable).toEqual(Object.keys(UNRECOGNIZED_PANELS).sort());
  });

  test("the panels that save with a non-primary colour carry the mark", () => {
    const byFile = (file) => withFields.find((b) => b.file === file).body;
    expect(byFile("CRM/Accounts/_ContactLinkEndOffcanvas.cshtml")).toMatch(/id="btnConfirmEndContactLink"[^>]*data-dt-primary/);
    expect(byFile("CRM/Accounts/_RelationshipEndOffcanvas.cshtml")).toMatch(/id="btnConfirmEndRelationship"[^>]*data-dt-primary/);
    const visit = withFields.filter((b) => b.file === "CRM/VisitExecution/Index.cshtml").map((b) => b.body).join("\n");
    expect(visit).toMatch(/id="ve-submit-report"[^>]*data-dt-primary/);
    expect(visit).toMatch(/id="ve-amend-report"[^>]*data-dt-primary/);
  });
});
