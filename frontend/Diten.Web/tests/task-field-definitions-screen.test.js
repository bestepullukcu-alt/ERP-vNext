const fs = require("fs");
const path = require("path");

/*
 * Task Field Definitions — the screen was COPIED from GoldenReferenceCompact and the copy was never finished.
 * index.js says so in its own header ("A COPY of the GoldenReferenceCompact index script").
 *
 * What that cost, live: the script read #filterReferenceType / #filterCategory / #filterOwner / #filterPriority
 * — four selects this screen has never rendered — so loadLookupOptions() returned early every time and the
 * Value type and Section filters sat there permanently empty and inert. The delete dialog printed the word
 * "undefined" because it read row.name, a field the copied entity had and this one does not. And 41 lines of
 * offcanvas code sat unreachable behind a rule that forbids it on a Compact Index at all.
 *
 * These tests are deliberately mostly STRUCTURAL, and that is the point rather than a compromise: every defect
 * above was a broken BINDING between two files — markup id vs script id, entity field vs row contract, resx key
 * vs the key a view asks for. None of them is visible from inside one file, and none needs a browser to detect.
 * The first test below is the general form of the whole class: both directions, so neither an unread control nor
 * an unrendered read can survive.
 */
const root = path.resolve(__dirname, "..");
const read = (rel) => fs.readFileSync(path.resolve(root, rel), "utf8");

const INDEX_JS = "wwwroot/assets/js/Tasks/FieldDefinitions/index.js";
const L10N_JS = "wwwroot/assets/js/Tasks/FieldDefinitions/index.l10n.js";
const FORM_JS = "wwwroot/assets/js/Tasks/FieldDefinitions/form.js";
const FILTER_VIEW = "Views/Tasks/FieldDefinitions/_Filter.cshtml";
const DETAILS_VIEW = "Views/Tasks/FieldDefinitions/Details.cshtml";
const FORM_VIEW = "Views/Tasks/FieldDefinitions/_Form.cshtml";
const L10N_VIEW = "Views/Tasks/FieldDefinitions/_IndexL10n.cshtml";

const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
const moduleResx = (locale) =>
  read(`Resources/Views/Tasks/FieldDefinitions/TaskFieldDefinitionsIndex.${locale}.resx`);

describe("field definition filters are bound to controls that exist", () => {
  const declaredIds = () => {
    const ids = new Set();
    // <select> only: the wrapping <form id="filterForm"> is the panel, not a filter control.
    const re = /<select[^>]*\sid="(filter[A-Za-z]+)"/g;
    let match;
    while ((match = re.exec(read(FILTER_VIEW))) !== null) { ids.add(match[1]); }
    return ids;
  };

  const readIds = () => {
    const ids = new Set();
    const re = /#(filter[A-Za-z]+)|getElementById\(\s*'(filter[A-Za-z]+)'/g;
    let match;
    while ((match = re.exec(read(INDEX_JS))) !== null) { ids.add(match[1] || match[2]); }
    return ids;
  };

  it("reads no filter control the markup does not render", () => {
    // THE defect. Four ids were read that no .cshtml declares, which is silent: getElementById returns null,
    // the guard returns early, and the filters simply never work with nothing logged anywhere.
    const declared = declaredIds();
    const missing = [...readIds()].filter((id) => !declared.has(id));

    expect(missing).toEqual([]);
  });

  it("renders no filter control the script never reads", () => {
    /*
     * The other direction, and the half that was actually broken here: #filterValueType and #filterSection were
     * both rendered and both ignored. A control nobody reads is worse than a missing one — the user selects a
     * value, presses Apply, and the table does not change.
     */
    const read_ = readIds();
    const ignored = [...declaredIds()].filter((id) => !read_.has(id));

    expect(ignored).toEqual([]);
  });

  it("filters on this catalogue's own fields, not the copied entity's", () => {
    const source = read(INDEX_JS);

    ["valueType", "section"].forEach((field) => {
      expect(source).toContain(`appliedFilters.${field}`);
    });
    ["referenceType", "category", "owner", "priority"].forEach((field) => {
      expect(source).not.toContain(`appliedFilters.${field}`);
    });
  });

  it("builds its filter options without calling a sibling module's endpoint", () => {
    // /GoldenReferenceCompact/lookups belongs to another module and returns fields a field definition has none
    // of. Options now come from the rows the table already holds.
    const source = read(INDEX_JS);

    expect(source).not.toMatch(/fetch\(\s*['"]\/GoldenReferenceCompact/);
    expect(source).toContain("populateFilterOptions");
  });
});

describe("the copy left nothing unreachable behind", () => {
  it("carries no offcanvas code, which Compact forbids on an Index anyway", () => {
    const source = read(INDEX_JS);

    // Both were defined and never called; populateDetailsOffcanvas addressed 13 element ids that no view here
    // renders. frontend-datatable-template.md's Create/Edit Surface Rule bans the surface they belonged to.
    expect(source).not.toContain("populateDetailsOffcanvas");
    expect(source).not.toContain("closeResponsiveModal");
    expect(source).not.toContain("getReferenceTypeMap");
    expect(source).not.toContain("oc-priority-dot");
  });

  it("names a field the row actually has in the delete confirmation", () => {
    /*
     * row.name printed literally "undefined" in the confirm dialog — TaskFieldDefinition has code, labelText,
     * labelResourceKey, valueType, section, isRequired, isActive, and no name.
     */
    const source = read(INDEX_JS);

    expect(source).toContain("entityName: row.code");
    expect(source).not.toContain("entityName: row.name");
  });

  it("uses the module pattern rather than a bare DOMContentLoaded listener", () => {
    // frontend-standards.md §4.1.
    const source = read(FORM_JS);

    expect(source).toMatch(/\(function \(\)\s*\{/);
    expect(source.trimEnd()).toMatch(/\}\)\(\);$/);
  });
});

describe("every visible string resolves", () => {
  it("does not ask SharedResource for a key it does not define", () => {
    /*
     * Details.cshtml asked for SharedLocalizer["Edit"]. SharedResource defines EditRecord, not Edit — and a
     * missing key renders the KEY, so the button read the literal "Edit" in all seven languages including the
     * five that do not use the Latin alphabet.
     */
    const shared = read("Resources/SharedResource.en.resx");
    const details = read(DETAILS_VIEW);

    const asked = [...details.matchAll(/SharedLocalizer\["([A-Za-z]+)"\]/g)].map((m) => m[1]);
    expect(asked.length).toBeGreaterThan(0);

    asked.forEach((key) => {
      expect(shared, `SharedResource has no key "${key}", so the view renders the key itself`)
        .toContain(`name="${key}"`);
    });
  });

  it("labels the submit button for what it will do", () => {
    // _Form.cshtml already computed isEdit for the heading and then always said "Save" on the button.
    const form = read(FORM_VIEW);

    expect(form).toContain('@(isEdit ? SharedLocalizer["Update"] : SharedLocalizer["Save"])');
  });

  it("routes every link through the controller rather than a typed path", () => {
    // frontend-standards.md §1 — a hand-typed href survives a route change silently.
    [DETAILS_VIEW, FORM_VIEW].forEach((view) => {
      expect(read(view)).not.toMatch(/<a\s+href="\/Tasks/);
    });
  });

  it("warns about no l10n key that the payload does not carry", () => {
    /*
     * index.l10n.js required 'Category' — a field this screen does not have — so every single page load logged
     * a missing-key warning. A warning that is always there is a warning nobody reads.
     */
    const required = [...read(L10N_JS).matchAll(/'([A-Za-z]+)'/g)].map((m) => m[1]);
    const payload = read(L10N_VIEW);

    required.forEach((key) => {
      expect(payload, `index.l10n.js requires "${key}" but _IndexL10n.cshtml never sends it`)
        .toMatch(new RegExp(`\\b${key}\\s*=`));
    });
  });
});

describe("the mandated module keys exist in all seven languages", () => {
  // l10n-agent.md §4: a DataTable list page must carry AddNew{Module}, Actions, EditBtn and QuickView in the
  // MODULE resx. This screen had AddNew (wrong name) and no EditBtn at all.
  const MANDATED = ["AddNewFieldDefinitions", "Actions", "EditBtn", "QuickView"];

  it.each(LOCALES)("%s defines every mandated key", (locale) => {
    const resx = moduleResx(locale);
    MANDATED.forEach((key) => expect(resx).toContain(`name="${key}"`));
  });

  it("translates the new keys rather than leaving English in place", () => {
    /*
     * The l10n gate. Copying the English value into fr/es/zh/ar/ru/tr is explicitly forbidden — a reader sees a
     * language they did not choose and concludes the system is broken.
     */
    const value = (locale, key) => {
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(moduleResx(locale));
      return match ? match[1].trim() : null;
    };

    ["AddNewFieldDefinitions", "EditBtn"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();

      LOCALES.filter((locale) => locale !== "en").forEach((locale) => {
        expect(value(locale, key), `${key}/${locale} is missing`).toBeTruthy();
        expect(value(locale, key), `${key}/${locale} is still the English text`).not.toBe(english);
      });
    });
  });
});

describe("required means required, and optional means optional", () => {
  it("marks the ValueType select required, like its siblings", () => {
    // UI-020 parity: the label shows an asterisk and the model says [Required], so the control must say it too.
    const form = read(FORM_VIEW);

    expect(form).toMatch(/<select asp-for="ValueType"[^>]*\srequired[\s>]/);
  });

  it("lets SortOrder be left blank", () => {
    /*
     * A non-nullable int makes MVC emit data-val-required, so a field the form presents as optional refuses to
     * submit when empty — a required rule nobody wrote and nobody can see.
     */
    const model = read("Models/TaskFieldDefinitions/TaskFieldDefinitionViewModels.cs");

    expect(model).toContain("public int? SortOrder { get; set; }");
    expect(model).not.toMatch(/public int SortOrder \{ get; set; \}/);
  });
});

describe("the screen is known to the module catalogue", () => {
  it("is registered as a page in MOD-0024's manifest", () => {
    /*
     * NOT a hand-written <li> in _LayoutTenantShell.cshtml: this area's menu is rendered data-driven from the
     * module catalog (see the WC-1b note beside the Task Center entry), so a hard-coded link would be a second,
     * unmanaged entry that Menu Settings could neither reorder nor hide.
     */
    const manifest = fs.readFileSync(path.resolve(
      root, "..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "SelfRegistration", "TaskManifestProvider.cs"), "utf8");

    expect(manifest).toContain('RoutePath: "/Tasks/FieldDefinitions"');
    expect(manifest).toContain("TaskPermissions.FieldDefinitionsManage");

    /*
     * NOT asserted: IsNavigationVisible: true. TaskManifestProviderTests asserts every page in this manifest is
     * nav-invisible, on the recorded ground that the Task Center is the single personal entry point — so the
     * screen is catalogued but still not in the menu. Making it visible means editing that recorded decision,
     * which is CT's call, not a fix. Registration is the half that is unambiguously right.
     */
  });
});
