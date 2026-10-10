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
    let now = 1000000;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    const clicks = panel(`<textarea id="desc"></textarea>${SAVE}`);
    press(document.getElementById("desc"), { ctrlKey: true });
    now += 5000; // past the one-press-one-action window
    press(document.getElementById("desc"), { metaKey: true });
    expect(clicks).toEqual(["save", "save"]);
    vi.restoreAllMocks();
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

describe("UX-COMMON-FIX1: consequential actions, one press one action, pickers read before their own handlers", () => {
  const CHORD = '<button type="button" id="publish" class="btn btn-primary" data-dt-primary="chord">Publish</button>';

  test("a chord-marked primary: bare Enter does nothing (and no native submit); Ctrl+Enter clicks it once", () => {
    const clicks = panel(`<input id="n" type="text">${CHORD}`);
    const bare = press(document.getElementById("n"));
    expect(clicks, "a bare Enter published").toEqual([]);
    expect(bare.defaultPrevented, "a bare Enter fell through to a native form submission").toBe(true);
    press(document.getElementById("n"), { ctrlKey: true });
    expect(clicks).toEqual(["publish"]);
  });

  test("a chord-marked primary: Cmd+Enter (macOS) clicks it too", () => {
    const clicks = panel(`<input id="n" type="text">${CHORD}`);
    press(document.getElementById("n"), { metaKey: true });
    expect(clicks).toEqual(["publish"]);
  });

  test("a field with a datalist keeps Enter (it may commit a suggestion)", () => {
    const clicks = panel(`<input id="n" type="text" list="opts"><datalist id="opts"><option value="a"></option></datalist>${SAVE}`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("two quick Enters are ONE click (no double POST where the page has no busy lock)", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"));
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("an open allowInput flatpickr: Enter commits the date and closes the calendar — the panel does NOT save", () => {
    global.self = global.self || window;
    loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
    const clicks = panel(`<input id="due" type="text">${SAVE}`);
    const due = document.getElementById("due");
    const fp = window.flatpickr(due, { dateFormat: "Y-m-d", allowInput: true });
    due.focus();
    fp.open();
    expect(fp.isOpen).toBe(true);
    due.value = "2026-10-20";
    due.dispatchEvent(new Event("input", { bubbles: true }));
    press(due, { keyCode: 13 }); // flatpickr reads keyCode

    expect(fp.isOpen, "flatpickr did not handle its Enter").toBe(false);
    expect(due.value).toBe("2026-10-20");
    expect(clicks, "the panel saved while the reader was choosing a date").toEqual([]);
    fp.destroy();
  });

  test("an unmarked .btn-primary that is hidden right now (d-none, [hidden], an inactive tab) is not the primary", () => {
    const clicks = panel(`<input id="n" type="text">
      <div class="d-none"><button type="button" id="hiddenSave" class="btn btn-primary">Save</button></div>
      <div hidden><button type="button" id="hiddenSave2" class="btn btn-primary">Save</button></div>
      <button type="button" id="save" class="btn btn-primary">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("the only .btn-primary hidden in an inactive step: nothing is clicked", () => {
    const clicks = panel(`<input id="n" type="text"><div class="d-none"><button type="button" id="save" class="btn btn-primary">Save</button></div>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test.each(["email", "number", "search", "tel", "url", "password", "date", "time"])(
    "U3: Enter in an <input type=%s> runs the primary", (type) => {
      const clicks = panel(`<input id="n" type="${type}">${SAVE}`);
      press(document.getElementById("n"));
      expect(clicks).toEqual(["save"]);
    });

  test("U4a: a primary disabled ONLY by the .disabled class is not clicked", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary disabled">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("U4b: a primary disabled ONLY by aria-disabled is not clicked", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary" aria-disabled="true">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test.each([
    ["the autocomplete panel (.aa-Panel)", '<div class="aa-Panel"></div>'],
    ["a jQuery UI autocomplete list", '<ul class="ui-autocomplete"></ul>']
  ])("an open %s keeps Enter", (_label, markup) => {
    const clicks = panel(`<input id="n" type="text">${SAVE}${markup}`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("a closed jQuery UI autocomplete (display: none) does not hold Enter", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}<ul class="ui-autocomplete" style="display: none"></ul>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("a panel still sliding in (.offcanvas.showing) is answered", () => {
    document.body.innerHTML = `<div class="offcanvas offcanvas-end showing" id="p"><input id="n" type="text">${SAVE}</div>`;
    const clicks = [];
    document.getElementById("save").addEventListener("click", () => clicks.push("save"));
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
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

  /*
   * UX-COMMON-FIX1 (CT decision) — a primary whose label says it cannot be taken back must be chord-marked
   * (`data-dt-primary="chord"`: bare Enter does nothing, Ctrl / Cmd + Enter runs it). The words were measured from
   * the 70 panels' primary labels (resource key and English text); generic verbs that also name reversible work
   * (Run, Apply, Upload, Request, Record, Post) are left out — those panels carry the mark explicitly instead.
   */
  const CONSEQUENTIAL = /\b(Publish|Approve|Reject|Submit|Send|Confirm|End|Terminate|Retire|Deactivate|Delete|Delegate|Withdraw|Cancel|Suspend|Amend|Escalat\w*|Transition|Evidence|Reserve|Snapshot)/;
  const splitWords = (text) => text.replace(/([a-z])([A-Z])/g, "$1 $2");
  // A consequential-sounding label on a reversible save, by name and reason. The list only shrinks.
  const NOT_CONSEQUENTIAL = {
    "EnterpriseStrategyBusinessPerformance/Components/Planning/_PlanningCycleOffcanvas.cshtml":
      "hard-coded English 'Submit' on a plain create / update save of a planning cycle; FROZEN _Layout (no Enter rule there)",
    "EnterpriseStrategyBusinessPerformance/Components/Planning/_StrategyPeriodOffcanvas.cshtml":
      "hard-coded English 'Submit' on a plain create / update save of a strategy period; FROZEN _Layout"
  };
  const primaryMarkup = (body) => {
    const marked = [...body.matchAll(/<button\b[^>]*data-dt-primary[^>]*>([\s\S]*?)<\/button>/g)];
    if (marked.length) { return marked.map((m) => m[0]); }
    const primaries = [...body.matchAll(/<button\b[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>([\s\S]*?)<\/button>/g)];
    return primaries.length === 1 ? [primaries[0][0]] : [];
  };

  test("a primary whose label is consequential is chord-marked (Ctrl+Enter), or named as an exception", () => {
    const unmarked = [];
    withFields.forEach((block) => {
      if (NOT_CONSEQUENTIAL[block.file]) { return; }
      primaryMarkup(block.body).forEach((button) => {
        const label = splitWords(button.replace(/<button\b[^>]*>/, ""));
        if (CONSEQUENTIAL.test(label) && !/data-dt-primary="chord"/.test(button)) {
          unmarked.push(`${block.file}:${block.line} ${label.replace(/\s+/g, " ").trim()}`);
        }
      });
    });
    expect(unmarked).toEqual([]);
  });

  test("the measured consequential panels carry the chord mark (incl. those whose label is just 'Save')", () => {
    const chord = (file, id) => withFields.filter((b) => b.file === file)
      .some((b) => new RegExp(`id="${id}"[^>]*data-dt-primary="chord"|data-dt-primary="chord"[^>]*id="${id}"`).test(b.body));
    [
      ["Platform/Workflow/Index.cshtml", "wf-publish-submit"],
      ["Platform/Workflow/Tasks.cshtml", "wf-taskaction-submit"],
      ["Platform/Workflow/Escalations.cshtml", "wf-esc-run"],
      ["CRM/VisitExecution/Index.cshtml", "ve-submit-report"],
      ["CRM/VisitExecution/Index.cshtml", "ve-amend-report"],
      ["CRM/Accounts/_ContactLinkEndOffcanvas.cshtml", "btnConfirmEndContactLink"],
      ["CRM/Accounts/_RelationshipEndOffcanvas.cshtml", "btnConfirmEndRelationship"],
      ["CRM/Campaigns/_SnapshotPanel.cshtml", "submitSnapshot"],
      ["CRM/TerritoryManagement/_AccountAssignments.cshtml", "apply-selected-button"],
      ["DocumentManagement/ControlledDocuments/VersionHistory.cshtml", "btnSubmitVersion"],
      ["DocumentManagement/MasterRegister/Details.cshtml", "lifecycleModalSubmit"],
      ["DocumentManagement/MasterRegister/Details.cshtml", "approvalEvidenceSubmit"],
      ["MDM/ProductAbbreviationRegister/_CreateEditOffcanvas.cshtml", "btnRequestAbbreviation"],
      // "karar CT" (one Save, a reversible or an irreversible act by mode): chord until CT decides.
      ["MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml", "btnSaveGsku"],
      ["Platform/Tenants/Details.cshtml", "btnSubmitSubscriptionAction"]
    ].forEach(([file, id]) => expect(chord(file, id), `${file} #${id}`).toBe(true));
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
