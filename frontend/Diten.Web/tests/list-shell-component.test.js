const fs = require("fs");
const path = require("path");

/*
 * THE TABLE CARD IS USED, NOT COPIED — BL-440 package 1 (owner decision 2026-09-23).
 *
 * Measured the same day: 138 list screens hand-write the card, the placeholder, the `<table data-dt-standard="v2">`,
 * the selection column and the actions column. 26 have a selection column, 6 the shared placeholder; the Users
 * screen binds bulk selection in JS into a column its markup never draws, so its bulk bar cannot appear. One line
 * in the shared dt-defaults.js took all 138 pages down at once — a shared layer existed, but no SHELL stood on it.
 *
 * Package 1 is the markup half: `_ListShell.cshtml` + `DataTableListShellViewModel`, the same pattern as
 * `_BulkActionBar.cshtml`. The page describes the card (already-localized headers, a data mode, whether it selects)
 * and the shell renders it. The two golden references are the first users, so what new screens copy from is the
 * component call, not a `<table>`.
 *
 * Three claims are pinned here (the render-equality claim, the `required` members and the fail-closed data mode
 * live in Diten.Web.Tests/Lists/ListShellRenderEqualityTests.cs, because they need the Razor engine and reflection):
 *   1. the shell carries the four list markers the verifier and the CSS key off;
 *   2. the two golden `_DataTable.cshtml` partials write no raw `<table` and call the shell;
 *   3. the shell produces no text of its own — every header comes from the page, so no resx can be missing.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
/** Razor comments removed — a note ABOUT a marker is not the marker. */
const withoutRazorComments = (text) => text.replace(/@\*[\s\S]*?\*@/g, "");

const SHELL = () => withoutRazorComments(read("Views", "Shared", "Components", "DataTable", "_ListShell.cshtml"));
const MODEL = () => read("Models", "DataTable", "DataTableListShellViewModel.cs");
// [area, folder, table id, data mode] — two references, two modes since BL-440 package 3 (Compact = server).
const GOLDEN = [
  ["DevEnablement", "GoldenReferenceSlim", "dt-goldenreferenceslim", "client"],
  ["DevEnablement", "GoldenReferenceCompact", "dt-goldenreferencecompact", "server"]
];
const SHELL_CALL = /<partial\s+name="~\/Views\/Shared\/Components\/DataTable\/_ListShell\.cshtml"\s+model="\w+"\s*\/>/;

describe("the list shell carries the four markers every list stands on", () => {
  test("v2 contract, shaped placeholder, select-all column, data mode", () => {
    const shell = SHELL();
    expect(shell, "the v2 marker is gone — dt-defaults.js and the verifier both key off it").toMatch(/data-dt-standard="v2"/);
    expect(shell, "the shared shaped placeholder is gone").toContain('<partial name="_TableSkeleton" />');
    expect(shell, "the select-all header is gone — the bulk bar selects into nothing").toContain("dt-checkboxes-select-all");
    expect(shell, "the data mode attribute is gone — the list is undeclared again").toMatch(/data-dt-data-mode="@Model\.DataMode"/);
  });

  test("the markers sit in the golden order: card → placeholder → wrapper → table → thead", () => {
    // A sibling CSS rule hides the table while the placeholder stands; order is the mechanism, not decoration.
    const shell = SHELL();
    const at = (needle) => shell.indexOf(needle);
    const order = [
      '<div class="card">',
      '<partial name="_TableSkeleton" />',
      '<div class="card-datatable table-responsive">',
      "<table id=\"@Model.TableId\"",
      "<thead>"
    ].map(at);
    order.forEach((position, i) => expect(position, `marker ${i} is missing`).toBeGreaterThan(-1));
    expect([...order].sort((a, b) => a - b), "the markers are out of order").toEqual(order);
  });

  test("selection and actions are the page's choice — both are conditional on the model", () => {
    const shell = SHELL();
    expect(shell).toMatch(/@if \(Model\.HasSelection\)[\s\S]*dt-checkboxes-select-all/);
    expect(shell).toMatch(/@if \(Model\.HasActions\)[\s\S]*cell-fit text-end pe-3 all/);
  });

  test("it produces no text of its own — every visible string is bound from the model", () => {
    // Only lines that emit markup can emit text; on those, strip the tags and the bound @expressions.
    // Whatever is left would be a hard-coded word in one language.
    const text = SHELL()
      .split("\n")
      .filter((line) => /<\w/.test(line))
      .map((line) => line.replace(/<[^>]+>/g, "").replace(/@[\w.]+/g, "").trim())
      .join("");
    expect(text, `hard-coded text inside the shell: "${text}"`).toBe("");
    expect(SHELL(), "the shell must not localize — the page does").not.toMatch(/Localizer|@inject/);
  });

  test("the model is fail-closed on data mode and its allowed values are exactly server | client", () => {
    const model = MODEL();
    expect(model).toMatch(/AllowedDataModes\s*=\s*\["server",\s*"client"\]/);
    expect(model).toMatch(/public required string TableId/);
    expect(model).toMatch(/public required string DataMode/);
    expect(SHELL(), "the shell no longer throws on an invalid data mode").toMatch(/if \(!Model\.IsDataModeValid\)[\s\S]*throw new InvalidOperationException/);
  });
});

describe("the golden references USE the shell — the first two of 138", () => {
  test.each(GOLDEN)("%s/%s writes no raw <table and calls the shell", (area, folder, tableId, dataMode) => {
    const source = withoutRazorComments(read("Views", area, folder, "_DataTable.cshtml"));
    expect(source, `${folder} still hand-writes its table`).not.toMatch(/<table\b/i);
    expect(source, `${folder} still hand-writes the placeholder`).not.toMatch(/_TableSkeleton|skeleton-loader/);
    expect(source, `${folder} still hand-writes the selection column`).not.toMatch(/dt-checkboxes-select-all/);
    expect(source, `${folder} does not call the shell`).toMatch(SHELL_CALL);
    // The model setup the verifier reads: id, mode, selection — and the page's own localized headers.
    expect(source).toMatch(/new DataTableListShellViewModel\s*\{/);
    expect(source).toContain(`TableId = "${tableId}"`);
    expect(source).toContain(`DataMode = "${dataMode}"`);
    expect(source).toMatch(/HasSelection = true/);
    expect(source).toMatch(/ActionsHeader = Localizer\["Actions"\]\.Value/);
    expect(source).toMatch(/Header = SharedLocalizer\["Status"\]\.Value/);
  });

  test("the shell still reads the page's model, not a copy of the golden headers", () => {
    // MUTATION GUARD: a shell that hard-coded the golden columns would pass render-equality and fail every other list.
    const shell = SHELL();
    expect(shell).toMatch(/@foreach \(var column in Model\.Columns\)/);
    expect(shell).not.toMatch(/Localizer\["(Code|Name|ReferenceType|Priority|Actions)"\]/);
  });
});
