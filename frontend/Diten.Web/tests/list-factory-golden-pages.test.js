const fs = require("fs");
const path = require("path");

/*
 * THE GOLDEN PAGES WRITE ONLY THEIR OWN BUSINESS — BL-440 package 2 (owner decision 2026-09-23).
 *
 * MEASURED before this package: Golden Slim index.js 991 lines, Compact 685, Users 1162; 79 of the 108 top-level
 * names in Users were plumbing copied from Slim. The list is a component now: the page hands the factory its
 * columns, renderers, `populate`, form fields and endpoints, and the 79 names below live in ONE place,
 * diten-datatable.js. A golden page that grows any of them back is the first step of the next 138 copies —
 * which is why this test names them one by one, with the family each belongs to.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const stripComments = (s) => s.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:'"`])\/\/.*$/gm, "$1");

// [folder, line budget, data mode] — two references, two modes (BL-440 package 3): Slim is the bounded client-mode
// set, Compact the server-mode list. Compact's budget fell to 200 when its row matchers left for the service.
const GOLDEN = [
  ["GoldenReferenceSlim", 400, "client"],
  ["GoldenReferenceCompact", 200, "server"]
];

/** The 79 copied names, by family — the same list the work package measured on the Users screen. */
const PLUMBING = {
  "Save View state machine": ["getCurrentView", "serializeView", "normalizeViewState", "isDirtyComparedToDefault", "loadDefaultView", "saveDefaultView", "mapSavedViewToState", "getResetBaselineState", "setSaveFilterVisible", "applySavedTableState", "getSavedViewId", "getSavedViewName", "isSavedViewDefault", "unwrapViewResponse", "getSavedViewDef", "defaultViewRecord", "defaultViewState", "saveFilterArmed", "personalizationClient", "personalizationContext"],
  "column visibility and order": ["captureColVis", "applyColVis", "normalizeColVis", "captureColOrder", "applyColOrder", "normalizeColOrder", "defaultColVis", "saveViewColumnIndexes", "totalColumnCount", "defaultVisibleColumnIndexes", "baseOrder"],
  "filter normalisation": ["normalizeString", "normalizeArray", "sortNormalizedArray", "normalizeFilters", "normalizeFilterValue", "hasFilterValue", "emptyFilters", "matchesMultiFilter", "matchesSingleFilter", "matchesStatusFilter", "appliedFilters"],
  "inline filter bar": ["mountInlineFilter", "toggleInlineFilter", "bindInlineFilterA11y", "setupFilters", "syncFilterControls", "syncMultiSelectSummary", "getAppliedFilterCount", "registerTableFilters", "initSelect2Filters", "clampDropdown", "filterHostId", "filterCollapseId", "getSearchVal", "syncSearchInput"],
  "responsive modal return": ["closeResponsiveModal", "restoreResponsiveModalAfterCancel", "suppressResponsiveReturn", "responsiveReturnModalEl"],
  "form submission": ["submitCreateEditForm", "showFormErrors", "resetCreateEditForm", "getAntiForgeryToken", "openCreateOffcanvas", "openEditOffcanvas", "editingId"],
  "offcanvas / misc plumbing": ["getOcCreateEditInstance", "getOcDetailsInstance", "syncL10n", "tryParseRowJson", "reloadWithSuccessToast", "getSelectedIds", "extraButtons", "initDataTableLegacy", "bindQuickView", "appendOptions", "setText", "formatDateLegacy"]
};
const ALL_NAMES = Object.values(PLUMBING).flat();

describe("the 79 plumbing names live in the factory, not on the page", () => {
  test("the list really is 79 names (so nobody trims it quietly)", () => {
    expect(ALL_NAMES.length).toBe(79);
    expect(new Set(ALL_NAMES).size).toBe(79);
  });

  test.each(GOLDEN)("%s/index.js defines none of them with const|let|var|function", (folder) => {
    const source = stripComments(read("wwwroot", "assets", "js", "DevEnablement", folder, "index.js"));
    const offenders = [];
    Object.entries(PLUMBING).forEach(([family, names]) => {
      names.forEach((name) => {
        if (new RegExp(`\\b(?:const|let|var|function)\\s+${name}\\b`).test(source)) offenders.push(`${name} (${family})`);
      });
    });
    expect(offenders, `${folder} grew plumbing back: ${offenders.join(", ")}`).toEqual([]);
  });

  test("the factory itself carries every family (the names moved, they did not vanish)", () => {
    const factory = stripComments(read("wwwroot", "assets", "js", "diten-datatable.js"));
    ["createViewState", "createSavedViewStore", "createResponsiveReturn", "syncMultiSelectSummary", "mountInlineFilter", "bindInlineFilterA11y", "readStagedFilters", "writeFilterControls", "applyViewToTable", "captureView", "showFormErrors", "resetForm", "submitForm", "openEdit", "openCreate", "showQuickView"]
      .forEach((name) => expect(factory, `${name} is gone from the factory`).toContain(name));
  });
});

describe("the golden pages go through the factory and only the factory", () => {
  test.each(GOLDEN)("%s/index.js calls DitenDataTable.createList and stays under %i lines, in %s mode", (folder, maxLines, mode) => {
    const raw = read("wwwroot", "assets", "js", "DevEnablement", folder, "index.js");
    const source = stripComments(raw);
    expect(source).toMatch(/window\.DitenDataTable\.createList\(\{/);
    expect(source).toMatch(new RegExp(`dataMode: '${mode}'`));
    expect(raw.split("\n").length, `${folder} is longer than the ≤60% budget`).toBeLessThanOrEqual(maxLines);
  });

  test.each(GOLDEN)("%s/index.js registers no ext.search hook and never calls personalizationClient directly", (folder) => {
    const source = stripComments(read("wwwroot", "assets", "js", "DevEnablement", folder, "index.js"));
    expect(source, "the filter hook is the factory's").not.toMatch(/ext\.search\.push\(/);
    expect(source, "persistence is the factory's").not.toMatch(/personalizationClient/);
    expect(source, "the table is built by the factory").not.toMatch(/new\s+DataTable\s*\(|DtDefaults\.create\s*\(/);
    expect(source, "stateSave is the factory's decision").not.toMatch(/stateSave/);
  });

  test.each(GOLDEN)("%s/index.js keeps what is its own: columns, renderers, maps, filter fields, endpoints", (folder) => {
    const source = stripComments(read("wwwroot", "assets", "js", "DevEnablement", folder, "index.js"));
    expect(source).toMatch(/columns: \[/);
    expect(source).toMatch(/columnDefs: \[/);
    expect(source).toMatch(/getStatusMap|getReferenceTypeMap/);
    expect(source).toMatch(/filterFields = \[/);
    expect(source).toMatch(/onBulkAction/);
    expect(source).toMatch(/\/bulk`/);
  });

  test("Slim keeps populate and its form fields; Compact has no offcanvas form at all", () => {
    const slim = stripComments(read("wwwroot", "assets", "js", "DevEnablement", "GoldenReferenceSlim", "index.js"));
    const compact = stripComments(read("wwwroot", "assets", "js", "DevEnablement", "GoldenReferenceCompact", "index.js"));
    expect(slim).toMatch(/quickView: \{ offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas \}/);
    expect(slim).toMatch(/form: \{ formId: 'formGoldenReferenceSlim'[^\n]*submit: submitForm \}/);
    expect(compact).not.toMatch(/form: \{|quickView: \{/);
    expect(compact).toMatch(/window\.location\.href = `\/GoldenReferenceCompact\/Edit\/\$\{id\}`/);
  });
});
