const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 — the detailed task form measured against the GOLDEN REFERENCE COMPACT form, which is the pattern
 * every full-page form in this app is supposed to follow. Nine deviations were found by LOOKING at the screen
 * (CT, 2026-08-10); not one of them was visible to a test, because "the form works" and "the form follows the
 * pattern" were never the same assertion.
 *
 * The reference is parsed, not restated: if the golden form changes its own structure, this test starts talking
 * about the new structure rather than pinning yesterday's.
 *
 * The one that actually needed a test rather than an eye is SELECT2 COVERAGE. The configurable-field controls are
 * BUILT BY JAVASCRIPT after the page loads, so a fix that only enhances the markup's selects leaves every
 * tenant-defined dropdown bare — and it looks finished, because the static half of the form is correct. So the
 * last case here renders the generated controls for real and counts what got bound.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);

const TASK_FORM = web("Views", "Tasks", "_Form.cshtml");
const TASK_CREATE = web("Views", "Tasks", "Create.cshtml");
const TASK_EDIT = web("Views", "Tasks", "Edit.cshtml");
const GOLDEN_FORM = web("Views", "DevEnablement", "GoldenReferenceCompact", "_Form.cshtml");
const TASK_FORM_JS = web("wwwroot", "assets", "js", "Tasks", "form.js");
const TASK_FORM_PAGE_JS = web("wwwroot", "assets", "js", "Tasks", "form-page.js");

const read = (file) => fs.readFileSync(file, "utf8");
const count = (source, pattern) => (source.match(pattern) || []).length;
// Only element openings that START a line — a <section class="card"> named inside a comment is prose, not a card.
const SECTION_CARD = /^\s*<section class="card/gm;

describe("the task form follows the golden reference structure", () => {
  test("every select in the markup is a select2, like the reference", () => {
    const golden = read(GOLDEN_FORM);
    // Establish from the REFERENCE what a select looks like there, so this is not a hardcoded expectation.
    expect(golden).toMatch(/<select[^>]*class="select2 form-select"/);

    const source = read(TASK_FORM);
    const selects = source.match(/<select[^>]*>/g) || [];
    expect(selects.length).toBeGreaterThan(0);

    const bare = selects.filter((tag) => !/class="[^"]*\bselect2\b/.test(tag));
    expect(bare, `selects without the select2 class:\n${bare.join("\n")}`).toHaveLength(0);
  });

  test("the form is split into section cards, each with its own heading", () => {
    const golden = read(GOLDEN_FORM);
    const goldenSections = count(golden, SECTION_CARD);
    expect(goldenSections).toBeGreaterThanOrEqual(4);

    const source = read(TASK_FORM);
    const sections = count(source, SECTION_CARD);
    expect(sections).toBeGreaterThanOrEqual(4);
    // A heading per card, as in the reference — a card with no caption is a box, not a section.
    expect(count(source, /<h6[^>]*>/g)).toBeGreaterThanOrEqual(sections);
  });

  test("the configurable fields get a card of their own", () => {
    const source = read(TASK_FORM);
    const customFieldsBlock = /<section class="card[\s\S]*?id="taskCustomFields"/.test(source)
      || /id="taskCustomFields"[\s\S]{0,400}?<\/section>/.test(source);
    expect(customFieldsBlock, "#taskCustomFields is not inside its own <section class=\"card\">").toBe(true);
  });

  test("the page header is a title plus a breadcrumb, not a description sentence", () => {
    // In the reference the header lives in the shared _Form partial, so Create and Edit carry one identical
    // header instead of two that drift.
    expect(read(GOLDEN_FORM)).toMatch(/<nav aria-label="breadcrumb">/);

    const form = read(TASK_FORM);
    expect(form, "_Form.cshtml has no breadcrumb").toMatch(/<nav aria-label="breadcrumb">/);
    expect(form, "_Form.cshtml has no title heading").toMatch(/<h5 class="mb-0">/);

    for (const file of [TASK_CREATE, TASK_EDIT]) {
      const source = read(file);
      expect(source, `${path.basename(file)} still prints the description sentence`)
        .not.toMatch(/PageDescription/);
      // No second header: the partial owns it.
      expect(count(source, /<h5/g), `${path.basename(file)} prints its own competing title`).toBe(0);
    }
  });

  test("save and cancel sit in the header, and appear exactly once", () => {
    const source = read(TASK_FORM);

    // The reference puts them in the header row and points the submit at the form by id.
    expect(read(GOLDEN_FORM)).toMatch(/<button type="submit" form="[^"]+"/);
    expect(source).toMatch(/<button type="submit" form="taskForm"/);

    // Exactly one save and one cancel in the whole form — two of the same action asks the user which one is real.
    expect(count(source, /id="taskSubmit"/g)).toBe(1);
    expect(count(source, /ActionCancel/g)).toBe(1);
  });
});

describe("select2 covers the generated controls too", () => {
  /*
   * The real form.js is loaded into a real DOM, the configurable fields are rendered from real definition shapes,
   * and then the real enhancement runs with jQuery stubbed so every .select2() call is recorded. The claim under
   * test is a COUNT: enhanced selects === selects present. A fix that only touches the .cshtml fails here.
   */
  const buildWindow = () => {
    document.body.innerHTML = `
      <form id="taskForm">
        <select class="select2 form-select" id="taskPriority"></select>
        <select class="select2 form-select" id="taskAssignmentTarget"></select>
        <div class="row" id="taskCustomFieldsRow"></div>
      </form>`;

    // jQuery stubbed at the seam select2 is reached through, so every binding is recorded rather than performed.
    const enhanced = [];
    const settings = [];
    const jq = (selectorOrNode) => {
      const nodes = typeof selectorOrNode === "string"
        ? Array.from(document.querySelectorAll(selectorOrNode))
        : [selectorOrNode];
      const api = {
        length: nodes.length,
        each(callback) { nodes.forEach((node, i) => callback.call(node, i, node)); return api; },
        wrap() { return api; },
        parent() { return api; },
        hasClass() { return false; },
        on() { return api; },
        select2(options) {
          nodes.forEach((node) => { enhanced.push(node); settings.push([node, options || {}]); });
          return api;
        }
      };
      return api;
    };
    global.$ = jq;
    global.jQuery = jq;

    loadScript("wwwroot/assets/js/Tasks/form.js");
    return { window: global, enhanced, settings };
  };

  const DEFINITIONS = [
    { code: "SEVERITY", valueType: "Status", isActive: true, isRequired: true, labelText: "Severity",
      optionsSourceKind: "Lookup", optionsSourceKey: "severity", sortOrder: 10 },
    { code: "OWNER", valueType: "Person", isActive: true, labelText: "Owner", sortOrder: 20 },
    { code: "NOTE", valueType: "Text", isActive: true, labelText: "Note", sortOrder: 30 }
  ];

  const OPTIONS = {
    SEVERITY: [{ value: "HIGH", label: "High" }],
    OWNER: [{ value: "u1", label: "Ada" }]
  };

  test("every generated select carries the select2 class", () => {
    const { window } = buildWindow();
    const row = window.document.getElementById("taskCustomFieldsRow");

    window.TaskForm.renderCustomFields(row, DEFINITIONS, OPTIONS, { optionPlaceholder: "—" });

    const generated = Array.from(row.querySelectorAll("select"));
    expect(generated.length).toBeGreaterThan(0);
    const bare = generated.filter((node) => !node.classList.contains("select2"));
    expect(bare.map((n) => n.id), "generated selects missing the select2 class").toHaveLength(0);
  });

  test("enhancing the form binds EVERY select, generated ones included", () => {
    const { window, enhanced } = buildWindow();
    const row = window.document.getElementById("taskCustomFieldsRow");
    window.TaskForm.renderCustomFields(row, DEFINITIONS, OPTIONS, { optionPlaceholder: "—" });

    expect(typeof window.TaskForm.enhanceSelects,
      "TaskForm.enhanceSelects is missing — nothing binds the generated controls").toBe("function");

    window.TaskForm.enhanceSelects(window.document);

    const all = Array.from(window.document.querySelectorAll("select"));
    expect(enhanced.length,
      `bound ${enhanced.length} of ${all.length} selects — the generated controls stay bare`)
      .toBe(all.length);
  });

  test("the page wires the enhancement AFTER the configurable fields are rendered", () => {
    const source = read(TASK_FORM_PAGE_JS);
    const rendered = source.indexOf("renderCustomFields");
    const enhanced = source.indexOf("enhanceSelects");
    expect(rendered, "renderCustomFields is not called from the page").toBeGreaterThan(-1);
    expect(enhanced, "enhanceSelects is never called from the page").toBeGreaterThan(-1);
    // Binding before the controls exist is the partial fix this whole test exists to prevent.
    expect(enhanced).toBeGreaterThan(rendered);
  });

  /*
   * A record field keeps its own search box because that search runs on the SERVER: the control holds one page of
   * a source that can have thousands of rows, so select2's built-in search — which filters only the options
   * already in the DOM — would quietly search the page instead of the source and report "no results" for records
   * that exist. Two search boxes on one control is the confusion the owner asked about, so the record control
   * must suppress select2's own.
   */
  test("a record picker keeps the server search and suppresses select2's own", () => {
    const { window, settings } = buildWindow();
    const row = window.document.getElementById("taskCustomFieldsRow");

    window.TaskForm.renderCustomFields(row, [{
      code: "DEPARTMENT", valueType: "Reference", isActive: true, labelText: "Department",
      optionsSourceKind: "ModuleRecord", optionsSourceKey: "organization/units", sortOrder: 10
    }], { DEPARTMENT: [{ value: "d1", label: "QA" }] }, { optionPlaceholder: "—" });

    const control = row.querySelector('[data-custom-field="DEPARTMENT"]');
    expect(control, "the record control was not rendered").toBeTruthy();
    expect(row.querySelector("[data-custom-field-search]"), "the server search box is gone").toBeTruthy();
    expect(control.getAttribute("data-select2-search"),
      "the record control does not declare that select2's search is off").toBe("off");

    // And the declaration is HONOURED at bind time — the attribute alone would be decoration.
    window.TaskForm.enhanceSelects(row);
    const recordSettings = settings.find(([node]) => node === control);
    expect(recordSettings, "the record control was never enhanced").toBeTruthy();
    expect(recordSettings[1].minimumResultsForSearch,
      "select2 still renders its own search box on a server-searched control").toBe(Infinity);
  });
});

/*
 * The required-field badge is only worth having if it counts the CONFIGURABLE required fields too. A counter that
 * reads "2 / 2 — complete" while an untouched required tenant field sits below it is worse than no counter: it
 * states, with a green badge, something that is not true. The tracker re-scans on every update, but nothing used
 * to call it for controls created after load — this is that gap, measured.
 */
describe("the required-field badge counts fields that appear after load", () => {
  /*
   * The tracker is loaded ONCE for this file and re-armed per test by dispatching DOMContentLoaded, because
   * loading it twice would leave two independent trackers on one document — which is not how a page works, and
   * which produced two badges reacting to each other.
   */
  let trackerLoaded = false;
  const bootTracker = (html) => {
    document.body.innerHTML = html;
    global.$ = () => ({ hasClass: () => false, on() { return this; } });
    global.RequiredProgressText = "{0} / {1}";
    if (!trackerLoaded) {
      loadScript("wwwroot/assets/js/custom/required-fields-tracker.js");
      trackerLoaded = true;
    }
    document.dispatchEvent(new window.Event("DOMContentLoaded"));
    return () => document.querySelector(".required-text")?.textContent;
  };

  test("an input added to the form later is bound and recounted", async () => {
    const badgeText = bootTracker(`
      <form id="laterFields">
        <label for="a">A <span class="text-danger">*</span></label>
        <input id="a" required />
        <button type="submit">Save</button>
        <div id="slot"></div>
      </form>`);

    expect(badgeText(), "the badge did not render").toBe("0 / 1");

    // What renderCustomFields does: a required control appears inside the form after load.
    const slot = document.getElementById("slot");
    slot.innerHTML = '<label for="b">B <span class="text-danger">*</span></label><input id="b" required />';

    // MutationObserver callbacks are microtask-scheduled; let them run.
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(badgeText(), "the configurable required field was not counted").toBe("0 / 2");

    document.getElementById("b").value = "filled";
    document.getElementById("b").dispatchEvent(new window.Event("input"));
    expect(badgeText(), "typing in the added field did not update the badge").toBe("1 / 2");
  });

  test("a required field inside a card that is revealed later is counted", async () => {
    const badgeText = bootTracker(`
      <form id="revealLater">
        <label for="a">A <span class="text-danger">*</span></label>
        <input id="a" required />
        <button type="submit">Save</button>
        <section id="extra" class="d-none">
          <label for="b">B <span class="text-danger">*</span></label>
          <input id="b" required />
        </section>
      </form>`);

    // Hidden fields do not count — that rule is unchanged.
    expect(badgeText()).toBe("0 / 1");

    // The configurable-field card is rendered hidden and revealed once something rendered into it: no child is
    // added at that moment, only a class removed, so watching added nodes alone would miss it.
    document.getElementById("extra").classList.remove("d-none");
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(badgeText(), "revealing the card did not recount").toBe("0 / 2");
  });
});
