const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0024 — second alignment round on the detailed task form. Four measured defects, one of them functional:
 *
 *  1. THE PERSON FIELDS DID NOT WORK. Reviewer, approval manager and watchers were bare text inputs with no
 *     picker behind them, while the server declares Guid? ReviewerCandidateUserId / Guid? ApprovalManagerUserId
 *     and IReadOnlyList<TaskWatcherRequest>. Whatever was typed went straight out, so the only way to fill them
 *     correctly was to know and type a user GUID. Watchers was worse than unusable: readForm produced a STRING
 *     and buildCreatePayload only forwards an ARRAY, so every watcher the user typed was silently dropped.
 *  2. A record-backed configurable field showed TWO controls — a hand-rolled server search box above a select2
 *     whose own search was switched off. select2's `ajax` is the feature for exactly this, and the repo had
 *     never used it.
 *  3. The three dates were <input type="date">, whose format follows the OPERATING SYSTEM's locale — an Arabic
 *     page still showed gg.aa.yyyy. The golden reference uses flatpickr.
 *  4. Governance was one card holding five unrelated decisions.
 *
 * What must NOT change is the wire: the payload the server already accepts (identities, watcher rows, and the
 * YYYY-MM-DD dates) is pinned here, because every one of these fixes is a control swap underneath it.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");
const FORM_PAGE_JS = () => read("wwwroot", "assets", "js", "Tasks", "form-page.js");
const GOLDEN_CREATE = () => read("Views", "DevEnablement", "GoldenReferenceCompact", "Create.cshtml");

const ALICE = "dddddddd-dddd-dddd-dddd-dddddddddddd";
const BOB = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

const person = (overrides = {}) => ({
  userId: ALICE,
  displayName: "Selin Aras",
  positionId: "11111111-1111-1111-1111-111111111111",
  positionCode: "QA-1",
  positionName: "QA Specialist",
  organizationUnitId: "22222222-2222-2222-2222-222222222222",
  organizationUnitCode: "FAC-A",
  organizationUnitName: "Facility A",
  ...overrides
});

const PERSON_LABELS = { placeholder: "Kişi seçin…", empty: "Kimse yok", nameUnavailable: "Ad yok" };

const loadForm = () => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
  return global.TaskForm;
};

// ── 1. person fields ────────────────────────────────────────────────────────────────────────────────────────

describe("the person fields are pickers, not GUID boxes", () => {
  test("reviewer, approval manager and watchers are selects in the markup", () => {
    const form = TASK_FORM();

    for (const id of ["taskReviewer", "taskApprovalManager", "taskWatchers"]) {
      expect(form, `${id} is still a text input — it would demand a typed GUID`)
        .not.toMatch(new RegExp(`<input[^>]*id="${id}"`));
      expect(form, `${id} has no select`).toMatch(new RegExp(`<select[^>]*id="${id}"`));
    }

    // Watchers is a LIST on the wire (IReadOnlyList<TaskWatcherRequest>), so its control is multi-select.
    const watchers = form.slice(form.indexOf('id="taskWatchers"'));
    expect(watchers.slice(0, 200), "the watcher picker is not multiple").toContain("multiple");
  });

  test("all three are filled from the SAME people lookup the assignee uses", () => {
    const source = FORM_PAGE_JS();
    // One source of people, one renderer — a second vocabulary for the same concept is how the two lists drift.
    expect(source).toContain("assignablePeople()");
    for (const id of ["taskReviewer", "taskApprovalManager", "taskWatchers"]) {
      expect(source, `${id} is never populated from the people lookup`)
        .toMatch(new RegExp(`renderPersonOptions\\([^)]*${id}`));
    }
  });

  test("watchers reach the payload in the shape the server declares", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-08-12", assignmentTarget: "SelfAssigned",
      watchers: [ALICE, BOB]
    });

    // TaskWatcherRequest(Guid UserId, TaskWatcherRole Role, Guid? PositionId) — Watcher, never Consultant:
    // the consultant role is BL-053 and is not offered by this form.
    expect(payload.watchers).toEqual([
      { userId: ALICE, role: "Watcher", positionId: null },
      { userId: BOB, role: "Watcher", positionId: null }
    ]);
  });

  test("a watcher list that is already in wire shape is passed through untouched", () => {
    const TaskForm = loadForm();
    const rows = [{ userId: ALICE, role: "Watcher", positionId: null }];

    const payload = TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-08-12", assignmentTarget: "SelfAssigned", watchers: rows
    });

    expect(payload.watchers).toEqual(rows);
  });

  test("the edit form shows the stored watchers by NAME and keeps their identities", () => {
    const TaskForm = loadForm();
    document.body.innerHTML = '<select id="taskWatchers" multiple></select>';
    const select = document.getElementById("taskWatchers");

    TaskForm.renderPersonOptions(
      select, [person(), person({ userId: BOB, displayName: "Deniz Koç" })], PERSON_LABELS, { multiple: true });
    TaskForm.selectWatchers(select, [{ userId: BOB, role: "Watcher", positionId: null }]);

    const selected = [...select.selectedOptions];
    expect(selected).toHaveLength(1);
    expect(selected[0].value, "the identity is not what the control holds").toBe(BOB);
    expect(selected[0].textContent).toContain("Deniz Koç");
    expect(selected[0].textContent).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}/i);
  });

  test("a multi-select picker offers no placeholder row", () => {
    // A placeholder <option> in a multi-select is selectable, and selecting it would post an empty identity.
    const TaskForm = loadForm();
    document.body.innerHTML = '<select id="taskWatchers" multiple></select>';
    const select = document.getElementById("taskWatchers");

    TaskForm.renderPersonOptions(select, [person()], PERSON_LABELS, { multiple: true });

    expect([...select.options].map((o) => o.value)).toEqual([ALICE]);
  });
});

// ── 2. record fields: one control, searched on the server ───────────────────────────────────────────────────

describe("a record-backed field is ONE control, searched through select2's ajax", () => {
  const RECORD_DEFINITION = {
    code: "DEPARTMENT", valueType: "Reference", isActive: true, labelText: "Department",
    optionsSourceKind: "ModuleRecord", optionsSourceKey: "organization/units", sortOrder: 10
  };
  const LOOKUP_DEFINITION = {
    code: "SEVERITY", valueType: "Status", isActive: true, labelText: "Severity",
    optionsSourceKind: "Lookup", optionsSourceKey: "severity", sortOrder: 20
  };

  const harness = () => {
    document.body.innerHTML = '<form id="taskForm"><div id="row"></div></form>';
    const settings = [];
    const jq = (selectorOrNode) => {
      const nodes = typeof selectorOrNode === "string"
        ? Array.from(document.querySelectorAll(selectorOrNode))
        : [selectorOrNode];
      const api = {
        length: nodes.length,
        each(cb) { nodes.forEach((n, i) => cb.call(n, i, n)); return api; },
        wrap() { return api; }, parent() { return api; },
        hasClass() { return false; }, on() { return api; },
        select2(options) { nodes.forEach((n) => settings.push([n, options || {}])); return api; }
      };
      return api;
    };
    global.$ = jq;
    global.jQuery = jq;
    const TaskForm = loadForm();
    return { TaskForm, settings, row: document.getElementById("row") };
  };

  const settingsFor = (settings, control) => (settings.find(([node]) => node === control) || [])[1];

  test("the hand-rolled search box is gone and select2's own search is back on", () => {
    const { TaskForm, row } = harness();

    TaskForm.renderCustomFields(
      row, [RECORD_DEFINITION], { DEPARTMENT: [{ value: "d1", label: "QA" }] }, { optionPlaceholder: "—" });

    expect(row.querySelector("[data-custom-field-search]"),
      "the second search box is still rendered").toBeNull();
    const control = row.querySelector('[data-custom-field="DEPARTMENT"]');
    expect(control.getAttribute("data-select2-search"),
      "select2's own search is still suppressed, so the control has no search at all").toBeNull();
  });

  test("only the server-paged control gets ajax; a fully loaded list keeps the local search", () => {
    const { TaskForm, settings, row } = harness();

    TaskForm.renderCustomFields(
      row, [RECORD_DEFINITION, LOOKUP_DEFINITION],
      { DEPARTMENT: [{ value: "d1", label: "QA" }], SEVERITY: [{ value: "HIGH", label: "High" }] },
      { optionPlaceholder: "—" });

    TaskForm.enhanceSelects(row, { searchRecords: async () => [] });

    const record = row.querySelector('[data-custom-field="DEPARTMENT"]');
    const lookup = row.querySelector('[data-custom-field="SEVERITY"]');

    expect(settingsFor(settings, record)?.ajax,
      "the record control has no ajax — its search would filter one page and lie").toBeTruthy();
    expect(settingsFor(settings, lookup)?.ajax,
      "a fully loaded option list must NOT round-trip: select2 already has every row").toBeFalsy();
  });

  test("the search is debounced and asks the server with the typed term", async () => {
    const { TaskForm, settings, row } = harness();
    TaskForm.renderCustomFields(
      row, [RECORD_DEFINITION], { DEPARTMENT: [{ value: "d1", label: "QA" }] }, { optionPlaceholder: "—" });

    const asked = [];
    TaskForm.enhanceSelects(row, {
      searchRecords: async (code, term) => { asked.push([code, term]); return [{ value: "d2", label: "REG" }]; }
    });

    const control = row.querySelector('[data-custom-field="DEPARTMENT"]');
    const ajax = settingsFor(settings, control).ajax;
    expect(ajax.delay, "the debounce that stopped one request per keystroke is gone").toBe(250);
    // The old search box's prompt now belongs to the picker — the string still has a job.
    expect(settingsFor(settings, control).placeholder, "the picker has no prompt").toBeTruthy();

    const results = await new Promise((resolve, reject) => {
      ajax.transport({ data: { term: "reg" } }, resolve, reject);
    });

    expect(asked).toEqual([["DEPARTMENT", "reg"]]);
    expect(results.results.map((r) => r.id)).toEqual(["d2"]);
    expect(results.results[0].text).toContain("REG");
  });

  test("a slow answer for an earlier term never overwrites a newer one", async () => {
    const { TaskForm, settings, row } = harness();
    TaskForm.renderCustomFields(
      row, [RECORD_DEFINITION], { DEPARTMENT: [{ value: "d1", label: "QA" }] }, { optionPlaceholder: "—" });

    const pending = {};
    TaskForm.enhanceSelects(row, {
      searchRecords: (code, term) => new Promise((resolve) => { pending[term] = resolve; })
    });

    const control = row.querySelector('[data-custom-field="DEPARTMENT"]');
    const ajax = settingsFor(settings, control).ajax;

    const delivered = [];
    ajax.transport({ data: { term: "Kal" } }, (r) => delivered.push(["Kal", r]), () => {});
    ajax.transport({ data: { term: "Kalite" } }, (r) => delivered.push(["Kalite", r]), () => {});

    // "Kalite" answers first, then the stale "Kal" arrives.
    pending.Kalite([{ value: "k2", label: "Kalite" }]);
    await new Promise((resolve) => setTimeout(resolve, 0));
    pending.Kal([{ value: "k1", label: "Kal" }]);
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(delivered.map(([term]) => term),
      "the stale answer was delivered — the user sees results for what they already finished typing")
      .toEqual(["Kalite"]);
  });
});

// ── 3. dates ────────────────────────────────────────────────────────────────────────────────────────────────

describe("the dates use the golden reference's picker", () => {
  test("no native date input is left, and each one is a flatpickr text field", () => {
    const form = TASK_FORM();

    expect(form.match(/type="date"/g),
      "a native date input remains — its format follows the OS locale, not the page's").toBeNull();
    for (const id of ["taskDueAt", "taskStartAt", "taskPlannedDate"]) {
      const tag = new RegExp(`<input[^>]*id="${id}"[^>]*>`).exec(form)
        || new RegExp(`<input[^>]*id="${id}"[\\s\\S]{0,200}?/>`).exec(form);
      expect(tag, `${id} is missing`).toBeTruthy();
      expect(tag[0], `${id} is not a flatpickr field`).toContain("flatpickr-date");
    }
  });

  test("the pages load flatpickr, the way the reference does", () => {
    // Establish the asset paths from the REFERENCE rather than restating them.
    const golden = GOLDEN_CREATE();
    expect(golden).toContain("flatpickr/flatpickr.css");
    expect(golden).toContain("flatpickr/flatpickr.js");

    for (const page of ["Create.cshtml", "Edit.cshtml"]) {
      const source = read("Views", "Tasks", page);
      expect(source, `${page} does not load the flatpickr stylesheet`).toContain("flatpickr/flatpickr.css");
      expect(source, `${page} does not load flatpickr`).toContain("flatpickr/flatpickr.js");
    }
  });

  test("the picker writes the SAME format the wire already carries", () => {
    const TaskForm = loadForm();
    document.body.innerHTML = '<input id="taskDueAt" class="flatpickr-date" />';

    const configured = [];
    const input = document.getElementById("taskDueAt");
    input.flatpickr = (options) => { configured.push(options); return {}; };

    TaskForm.enhanceDates(document);

    expect(configured, "no date field was initialised").toHaveLength(1);
    // The old <input type="date"> submitted YYYY-MM-DD; anything else silently changes what the API receives.
    expect(configured[0].dateFormat).toBe("Y-m-d");
  });

  test("a date still travels to the API as YYYY-MM-DD", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload({
      title: "t", assignmentTarget: "SelfAssigned",
      dueAt: "2026-08-12", startAt: "2026-08-10", plannedDate: "2026-08-11"
    });

    expect(payload.dueAt).toBe("2026-08-12");
    expect(payload.startAt).toBe("2026-08-10");
    expect(payload.plannedDate).toBe("2026-08-11");
  });
});

// ── 4. governance split ─────────────────────────────────────────────────────────────────────────────────────

describe("governance is one card per decision", () => {
  const governanceColumn = (form) => form.slice(form.indexOf('col-12 col-lg-4'));

  test("each governance decision gets its own card with an icon, a title and a sentence", () => {
    const column = governanceColumn(TASK_FORM());
    const cards = column.split(/<section class="card/).slice(1);

    expect(cards.length, "governance is still one undivided card").toBeGreaterThanOrEqual(5);

    cards.forEach((card, index) => {
      expect(card, `governance card #${index + 1} has no icon`).toMatch(/<i class="[^"]*\bbx\b/);
      expect(card, `governance card #${index + 1} has no heading`).toMatch(/<h6[^>]*>/);
      // One sentence of explanation, from the resx — never a literal string in the view.
      expect(card, `governance card #${index + 1} has no description`).toMatch(/class="[^"]*text-muted[^"]*"/);
    });
  });

  test("the five decisions each have a home, and watchers left the assignment card", () => {
    const form = TASK_FORM();
    const column = governanceColumn(form);

    for (const anchor of ["taskReviewRequired", "taskApprovalRequired", "taskApprovalManager",
      "taskWatchers", "taskEmailNotifications", "taskDelegationAllowed"]) {
      expect(column, `${anchor} is not in the governance column`).toContain(anchor);
    }

    const assignment = form.slice(form.indexOf("SectionAssignment"), form.indexOf("SectionPlanning"));
    expect(assignment, "watchers is still in the assignment card as well").not.toContain("taskWatchers");
  });

  test("nothing from the out-of-scope mockup crept in", () => {
    const form = TASK_FORM();
    // The consultant role is BL-053 and the approval-flow diagram is not this round's work. Matched against
    // MARKUP — a Razor comment naming what was deliberately left out is the opposite of a leak.
    const markup = form.replace(/@\*[\s\S]*?\*@/g, "");
    expect(markup).not.toMatch(/Consultant|Danışman/i);
    expect(markup).not.toMatch(/ApprovalFlowDiagram|approval-flow|approvalFlow/i);
  });
});
