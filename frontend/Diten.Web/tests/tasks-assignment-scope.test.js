const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-057 (company scope) + BL-072 (why the list is short) — the FORM side.
 *
 * The form draws four person pickers from one list, and the whole risk of this round lives in that fact:
 *
 *   taskAssignee         → work RECEIVER   → scope-limited
 *   taskWatchers         → sees the work   → scope-limited (a data-access decision, see below)
 *   taskReviewer         → DECIDES         → scope-EXEMPT
 *   taskApprovalManager  → DECIDES         → scope-EXEMPT
 *
 * Filtering "the list" would have been one line and would have silently killed intra-group approval: a task
 * produced in GMG TR is legitimately approved in GMG AZ by somebody neither above nor below the author. That is
 * why two endpoints exist and why the two groups are asserted separately here.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const FORM_PAGE_JS = () => read("wwwroot", "assets", "js", "Tasks", "form-page.js");
const API_JS = () => read("wwwroot", "assets", "js", "Tasks", "api.js");
const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");

const loadForm = () => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
  return global.TaskForm;
};

// ── which picker drinks from which tap ──────────────────────────────────────

describe("the four person pickers are NOT all filled from the same list", () => {
  test("the API client offers both lists", () => {
    const api = API_JS();
    expect(api, "the scope-limited list is gone").toMatch(/assignablePeople/);
    expect(api, "there is no scope-exempt list for approver/reviewer").toMatch(/decisionMakers/);
    expect(api).toMatch(/\/decision-makers/);
  });

  test("the web tier actually proxies the new route — an unproxied path 404s inside the web tier", () => {
    const controller = read("Controllers", "TasksController.cs");
    expect(controller).toContain("api/decision-makers");
    expect(controller).toContain("lookups/decision-makers");
  });

  test("assignee and watchers come from the SCOPE-LIMITED list", () => {
    const source = FORM_PAGE_JS();
    for (const id of ["taskAssignee", "taskWatchers"]) {
      const call = new RegExp(`renderPersonOptions\\([^)]*${id}[^)]*\\)`).exec(source);
      expect(call, `${id} is never populated`).toBeTruthy();
    }
    // The rows they are filled from must be the assignable (scoped) ones.
    expect(source).toMatch(/assignablePeople\(\)/);
  });

  test("reviewer and approval manager come from the SCOPE-EXEMPT list", () => {
    /*
     * The single most tempting mistake in this change is one filter for four pickers. This test is the guard:
     * the two decision pickers must be filled from a DIFFERENT variable than the assignee.
     */
    const source = FORM_PAGE_JS();
    expect(source, "the decision list is never fetched").toMatch(/decisionMakers\(\)/);

    const rowsFor = (id) => {
      const call = new RegExp(`renderPersonOptions\\(\\s*el\\('${id}'\\),\\s*([A-Za-z0-9_]+)`).exec(source);
      expect(call, `${id} is not filled by renderPersonOptions`).toBeTruthy();
      return call[1];
    };

    const assignee = rowsFor("taskAssignee");
    expect(rowsFor("taskReviewer"), "the reviewer shares the assignee's scoped list")
      .not.toBe(assignee);
    expect(rowsFor("taskApprovalManager"), "the approval manager shares the assignee's scoped list")
      .not.toBe(assignee);
    // Watchers deliberately DO share it — watching is receiving visibility, not deciding.
    expect(rowsFor("taskWatchers")).toBe(assignee);
  });

  test("the exemption is explained where it is made, not only in the backlog", () => {
    // A future reader who "tidies" the two lists into one would reintroduce the defect. The reason lives here.
    const source = FORM_PAGE_JS();
    expect(source).toMatch(/BL-057/);
    expect(source.toLowerCase()).toMatch(/approv|onay/);
  });
});

// ── BL-072: the hint ────────────────────────────────────────────────────────

describe("a short list says WHY it is short", () => {
  test("the form has a slot for the hint, and it is class-toggled (FG-003)", () => {
    const form = TASK_FORM();
    expect(form, "there is nowhere to render the exclusion hint")
      .toContain('data-task-field="assigneeExcluded"');
    expect(form, "the form gained an inline style").not.toMatch(/style="/);
  });

  test("the count and the breakdown come from the SERVER, never guessed", () => {
    const source = FORM_PAGE_JS();
    expect(source, "nothing reads the server's exclusion summary").toMatch(/excluded/i);
    // The client must not infer "someone is hidden" from a short list.
    expect(source).toMatch(/\.excluded/);
  });

  test("the hint text is composed from the reasons the server reports", () => {
    const TaskForm = loadForm();
    const t = (key) => ({
      excludedHint: "{0} kişi listelenmedi:",
      excludedNoActivePosition: "{0} aktif pozisyonu yok",
      excludedPositionNotActive: "{0} pozisyonu aktif değil",
      excludedOutOfScope: "{0} kapsam dışı"
    }[key] || key);

    const text = TaskForm.describeExcludedCandidates(
      { total: 3, noActivePosition: 2, positionNotActive: 0, outOfScope: 1 }, t);

    expect(text).toContain("3 kişi listelenmedi:");
    expect(text).toContain("2 aktif pozisyonu yok");
    expect(text).toContain("1 kapsam dışı");
    // A reason with a zero count is not listed — "0 pozisyonu aktif değil" is noise.
    expect(text).not.toContain("pozisyonu aktif değil");
  });

  test("nothing to report renders NOTHING — an empty hint is its own kind of noise", () => {
    const TaskForm = loadForm();
    expect(TaskForm.describeExcludedCandidates(
      { total: 0, noActivePosition: 0, positionNotActive: 0, outOfScope: 0 }, (k) => k)).toBe("");
    expect(TaskForm.describeExcludedCandidates(null, (k) => k)).toBe("");
  });

  test("the hint NEVER carries a name or an identity — it is a security boundary", () => {
    /*
     * The point of the scope rule is that those people are not visible. A hint that named them would hand back
     * exactly what the rule withholds. The client is given counts only, and this asserts it cannot render more
     * even when handed more.
     */
    const TaskForm = loadForm();

    const text = TaskForm.describeExcludedCandidates(
      {
        total: 1, noActivePosition: 0, positionNotActive: 0, outOfScope: 1,
        // A hostile/naive payload: the renderer must ignore anything that is not a count.
        names: ["Fahreddin Bey"], userIds: ["f0000000-0000-0000-0000-00000000000f"]
      },
      (key) => ({ excludedHint: "{0} kişi listelenmedi:", excludedOutOfScope: "{0} kapsam dışı" }[key] || key));

    expect(text).not.toContain("Fahreddin");
    expect(text).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}/i);
  });
});

// ── the lookup response shape changed; the client must follow it ────────────

describe("the people lookup answers an OBJECT now, not a bare array", () => {
  test("the client reads .people rather than treating the payload as a list", () => {
    /*
     * The server had to start returning { people, excluded } — only it knows why somebody is missing. A client
     * that still did `data.map(...)` would render an empty picker with no error at all, which is the exact
     * failure mode this whole round exists to remove.
     */
    const source = FORM_PAGE_JS();
    expect(source).toMatch(/\.people/);
  });

  test("a person row carries its legal entity, so the client can tell two companies apart", () => {
    // BL-057 §2: the position DTO always had it, the person DTO did not.
    const models = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "TaskModels.cs");
    const dto = models.slice(models.indexOf("record AssignablePersonDto"));
    expect(dto.slice(0, dto.indexOf(");")), "AssignablePersonDto still has no LegalEntityId")
      .toContain("LegalEntityId");
  });
});

// ── l10n ────────────────────────────────────────────────────────────────────

describe("the hint exists in all seven languages", () => {
  const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const KEYS = [
    "ExcludedHint", "ExcludedNoActivePosition", "ExcludedPositionNotActive", "ExcludedOutOfScope"
  ];

  test("every key is present in every language", () => {
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`);
      KEYS.forEach((key) => {
        expect(xml, `${locale} has no ${key}`).toContain(`name="${key}"`);
      });
    });
  });

  test("every key reaches the browser through the bridge", () => {
    const bridge = read("Views", "Tasks", "_IndexL10n.cshtml");
    KEYS.forEach((key) => expect(bridge, `${key} never reaches the browser`).toContain(key));
  });

  test("each sentence carries the count placeholder", () => {
    // A count that cannot be interpolated turns "3 kişi listelenmedi" into "{0} kişi listelenmedi".
    const tr = read("Resources", "Views", "Tasks", "TasksIndex.tr.resx");
    KEYS.forEach((key) => {
      const entry = new RegExp(`name="${key}"[^>]*><value>([^<]*)</value>`).exec(tr);
      expect(entry, `${key} missing from tr`).toBeTruthy();
      expect(entry[1], `${key} has no {0} placeholder`).toContain("{0}");
    });
  });
});
