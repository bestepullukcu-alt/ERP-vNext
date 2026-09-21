const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * A LOADING LIST LOOKS LIKE A LIST — owner report, 2026-09-21.
 *
 * What the owner saw: "Rol İzinleri güzel görünüyor, alan tanımlarınınki anlaşılmıyor; sayfa geldikten sonra
 * yukarıdan aşağı yükleniyor." Both halves were real, and measurable:
 *
 *   · SHAPE — every list drew the same five bars, whatever was coming. A placeholder that is not the shape of
 *     the answer says only "wait", which is what a spinner already says. The Role Permissions screen has always
 *     drawn its own panels' shape; that is the half worth copying.
 *   · ORDER — the five bars sat ABOVE the real `<thead>`, and DataTables builds the toolbar and the pager only
 *     after the data lands. So the page arrived in three instalments, top to bottom.
 *
 * The cure is one shared partial (so the next list inherits it instead of inventing a sixth variant) plus, for
 * the lists that opt in, hiding the half-built table until the real one is ready.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

/** The partial with its Razor comments removed — a note ABOUT the vocabulary is not markup. */
const PARTIAL = () => read("Views", "Shared", "_TableSkeleton.cshtml").replace(/@\*[\s\S]*?\*@/g, "");
const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css");
/** Every list that has been migrated: the four under test, plus the two templates new screens are copied from. */
const LISTS = [
  ["Organization", "FieldDefinitions"],
  ["Organization", "OrganizationUnits"],
  ["Organization", "Positions"],
  ["Organization", "PositionAssignments"],
  ["DevEnablement", "GoldenReferenceSlim"],
  ["DevEnablement", "GoldenReferenceCompact"]
];

describe("the placeholder is shaped like the list it stands in for", () => {
  test("it draws a toolbar, a header, rows and a footer — not five identical bars", () => {
    const partial = PARTIAL();
    ["dt-skeleton-toolbar", "dt-skeleton-head", "dt-skeleton-row", "dt-skeleton-footer", "dt-skeleton-pager"]
      .forEach((part) => expect(partial, `the ${part} band is gone`).toContain(part));
    // More than a couple of body rows, or it reads as a form rather than a list.
    expect(partial).toMatch(/for \(var row = 0; row < [4-9]; row\+\+\)/);
  });

  test("it speaks the product's ONLY placeholder vocabulary", () => {
    const partial = PARTIAL();
    // Every visible block is still `.shimmer` + `.skeleton-row`; the new classes only place them.
    const blocks = partial.match(/class="[^"]*shimmer[^"]*"/g) || [];
    expect(blocks.length, "the blocks stopped being the shared ones").toBeGreaterThan(5);
    blocks.forEach((cls) => expect(cls, `a block without .skeleton-row: ${cls}`).toContain("skeleton-row"));
    // MUTATION GUARD: a second animation or a second grey is what "NO NEW SKELETON LANGUAGE" forbids.
    expect(partial).not.toMatch(/placeholder-glow|animation|background:/);
  });

  test("the layout rules carry no colour and no animation of their own", () => {
    const css = CSS();
    const block = css.slice(css.indexOf(".dt-skeleton .dt-skeleton-toolbar"), css.indexOf(".dt-skeleton-hidden"));
    expect(block.length, "the layout block is gone").toBeGreaterThan(200);
    expect(block, "a second grey entered through the layout rules").not.toMatch(/background|animation|linear-gradient/);
  });

  test("every migrated list uses the shared partial and keeps NO fragment of its old one", () => {
    /*
     * ⚠ THE SECOND ASSERTION IS THE ONE THAT MATTERS, AND IT EXISTS BECAUSE THE FIRST ONE PASSED WHILE THE
     * PAGE WAS BROKEN. The migration removed the block's opening tag and its first bar, and left four orphan
     * bars plus a stray `</div>` behind: the id was gone (so a check for the id was happy), the bars had no
     * wrapper to hide them so they showed permanently, and the stray closing tag shut the card early, which
     * pushed the toolbar and the table outside it. The owner's screenshot was that page. So: no placeholder
     * block outside the partial, and the tags must balance.
     */
    LISTS.forEach(([area, folder]) => {
      const source = read("Views", area, folder, "_DataTable.cshtml");
      expect(source, `${area}/${folder} still draws its own skeleton`).toContain('<partial name="_TableSkeleton" />');
      expect(source.replace(/_TableSkeleton/g, ""), `${area}/${folder} kept a fragment of the old block`)
        .not.toMatch(/skeleton/i);
      expect(source.match(/<div\b/g)?.length ?? 0, `${area}/${folder} has unbalanced <div> tags`)
        .toBe(source.match(/<\/div>/g)?.length ?? 0);
      // FG-003: the hiding lives in the stylesheet, never in a style attribute.
      expect(source).not.toMatch(/style="display:\s*none/);
    });
  });
});

describe("the half-built table does not stand beside its own placeholder", () => {
  let DtDefaults;

  beforeAll(() => {
    global.DataTable = {
      Responsive: { display: { modal: () => ({}) } },
      Api: function () { this.columns = { adjust: () => {} }; this.responsive = { recalc: () => {} }; }
    };
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/js/dt-defaults.js");
    DtDefaults = global.window.DtDefaults;
  });

  const stage = (marker) => {
    document.body.innerHTML =
      `<div id="skeleton-loader" ${marker ? "data-table-skeleton" : ""} style="display:none"></div>` +
      '<div class="dt-container"><table></table></div>';
    return { nTableWrapper: document.querySelector(".dt-container") };
  };
  const tableHidden = () => document.querySelector(".dt-container").classList.contains("dt-skeleton-hidden");

  test("a list with the shaped skeleton hides its table until the real one is ready", () => {
    const config = DtDefaults.create({});
    const settings = stage(true);

    config.preXhr(settings, {});
    expect(tableHidden(), "the real header stood under the placeholder again").toBe(true);

    config.initComplete(settings, {});
    expect(tableHidden(), "the table never came back").toBe(false);
  });

  test("a list still carrying the OLD block is left exactly as it was", () => {
    const config = DtDefaults.create({});
    const settings = stage(false);

    config.preXhr(settings, {});
    expect(tableHidden(), "a list that did not opt in had its table hidden").toBe(false);
  });

  test("the column widths are recomputed for the table that was hidden", () => {
    // Measured while hidden, a table's columns are worth nothing; this is the one call that fixes it.
    const source = read("wwwroot", "assets", "js", "dt-defaults.js");
    const initComplete = source.slice(source.indexOf("merged.initComplete = function"));
    expect(initComplete).toContain("columns.adjust()");
    expect(initComplete).toMatch(/wasHidden/);
  });
});
