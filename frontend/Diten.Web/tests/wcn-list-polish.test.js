const fs = require("fs");
const path = require("path");

/*
 * THE LIST PAGE'S SECOND ROUND — the audit's "noise" class.
 *
 *   ① the row spoke a different visual language from the detail page's row
 *   ② sort and page never reached the URL, so a list could not be reloaded or shared
 *   ③ "SLA riski" appeared twice — measured, and NOT a duplicate
 *   ④ two columns that could not tell any two rows apart
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
/*
 * ⚠ ANCHORED AT THE START OF A LINE. A bare `indexOf(".wcn-row:hover {")` matched
 * `:root[data-skin=bordered] .wcn-row:hover` first — a DIFFERENT rule — and read its body instead. A selector
 * lookup that can silently land on a descendant match is not a lookup.
 */
const rule = (selector) => {
  const at = CSS.indexOf("\n" + selector + " {");
  return at === -1 ? "" : CSS.slice(at, CSS.indexOf("}", at));
};

describe("① one row language across the two surfaces", () => {
  const listRow = () => rule(".wcn-row");
  const detailRow = () => rule(".diten-checkitem");

  it("shares the radius and the alignment with the detail page's row", () => {
    /*
     * MUTATION GUARD: put `.5rem` / `stretch` back and this goes red.
     *
     * Safe to centre because the SLA accent bar carries its own `align-self: stretch` — it still runs the full
     * height, so nothing depended on the parent stretching.
     */
    expect(listRow(), "the radius drifted apart again").toContain("border-radius: .375rem");
    expect(detailRow()).toContain("border-radius: .375rem");
    expect(listRow(), "the row stopped centring its contents").toContain("align-items: center");
    expect(detailRow()).toContain("align-items: center");
  });

  it("keeps the padding and the height DIFFERENT, and says why", () => {
    /*
     * Not an oversight: this row is two lines (title + summary + chip strip) and the detail row is one.
     * Squeezing it to 6px/8px would crop content to win a number, and a height is an OUTCOME of content.
     */
    expect(listRow()).toContain("padding: .75rem .875rem");
    expect(detailRow()).toContain("padding: .375rem .5rem");
    expect(CSS, "the deliberate difference is undocumented").toContain("LEFT DIFFERENT, deliberately");
  });

  it("leaves the transparent border alone — it mirrors the skin toggle", () => {
    /*
     * MEASURED: `data-skin=bordered` colours it and drops the shadow, exactly as `.card` behaves, and `:hover`
     * colours it too. The detail row is opaque because it sits INSIDE a card; this one sits on the page and
     * carries a shadow. Two surfaces, two correct answers.
     */
    expect(listRow()).toContain("border: 1px solid transparent");
    expect(CSS).toContain(":root[data-skin=bordered] .wcn-row { border-color: var(--bs-border-color)");
  });

  it("already had the hover tint the audit reported missing", () => {
    /*
     * ⚠ THE AUDIT WAS WRONG HERE, and the reason is worth keeping: it dispatched a `mouseover` event, which
     * never raises `:hover`. Re-measured with a real pointer: rgba(105,108,255,.035) plus a border colour —
     * the 3% tint the detail page uses. Third time this session a synthetic event gave a false negative.
     */
    expect(rule(".wcn-row:hover")).toContain("background-color: rgba(var(--bs-primary-rgb), 0.035)");
  });
});

describe("② a sorted page can be reloaded and shared", () => {
  it("writes sort, direction and page into the URL", () => {
    const sync = APP.split("const syncUrl")[1].split("global.history.replaceState")[0];
    // MUTATION GUARD: drop any of the three and this goes red naming it.
    expect(sync, "sort is not serialised").toContain("put('sort', state.sortKey, 'sla')");
    expect(sync, "direction is not serialised").toContain("put('dir', state.sortDir, 'asc')");
    expect(sync, "page is not serialised").toContain("put('page'");
  });

  it("reads them back, validating the key against SORTERS itself", () => {
    const hydrate = APP.split("const hydrateStateFromUrl")[1].split("const syncUrl")[0];
    // A second hand-typed list of sortable columns is a second place to forget one.
    expect(hydrate).toContain("Object.prototype.hasOwnProperty.call(SORTERS, sortKey)");
    expect(hydrate).toContain("setIfAllowed('sortDir', 'dir', 'sortDir')");
    // 1-based in the URL, 0-based in state: `?page=0` is a link nobody would type.
    expect(hydrate).toContain("page > 0 ? page - 1 : 0");
  });

  it("listens to the thing that actually sorts", () => {
    /*
     * MEASURED: `data-wcn-sort=\"` is emitted ZERO times — `state.sortKey`, `SORTERS` and the click handler
     * were a whole sorting mechanism with no control to drive it, while the grid sorted through its own engine
     * and told nobody. Mirroring the grid's order INTO that state makes the existing machinery live instead of
     * serialising a value the reader cannot change.
     */
    expect(APP).toContain("workCenterDt.on('order.dt'");
    expect(APP, "the grid's opening order is still hard-coded").not.toContain("order: [[6, 'asc']],");
    expect(APP).toContain("TABLE_COLUMN_INDEX[state.sortKey]");
  });

  it("derives both index directions from ONE list", () => {
    // Two hand-maintained maps drift; this pair cannot.
    expect(APP).toContain("const TABLE_COLUMN_INDEX = TABLE_COLUMN_NAME.reduce");
  });
});

describe("③ the two SLA controls are not duplicates", () => {
  it("filters a different set from the signal chip, so both stay", () => {
    /*
     * MEASURED, before deciding anything:
     *   the CHIP  → `slaState === 'overdue' || slaState === 'due-soon'` — one fixed pair, one toggle
     *   the SELECT → a multi-select over FOUR values (overdue · due-soon · on-track · no-sla)
     *
     * The selector is strictly more expressive: "only overdue", "on track", "no date" cannot be asked with the
     * chip at all. The chip is a PRESET of the selector, not a copy of it — so removing either would take
     * something away. They compose under the ordinary axis rule (different questions intersect).
     */
    expect(APP).toContain("'sla-risk': (i) => i.slaState === 'overdue' || i.slaState === 'due-soon'");
    expect(APP).toContain("const SLA_ORDER = ['overdue', 'due-soon', 'on-track', 'no-sla']");
  });
});

describe("④ a column that cannot tell two rows apart is not drawn", () => {
  it("hides Tip and Modül when every row says the same thing", () => {
    /*
     * MEASURED across 76 live tasks: "Tip" read Görev on every row, "Modül" read Görevler on every row. Two of
     * nine columns carrying zero information.
     *
     * MUTATION GUARD: drop either guard and this goes red.
     */
    expect(APP).toContain("visible: state.tableColumnVisibility[1] && distinguishes(items, (i) => i.itemType)");
    expect(APP).toContain("visible: state.tableColumnVisibility[3] && distinguishes(items, (i) => i.sourceModule)");
  });

  it("treats an empty list as 'nothing to judge', not as 'no distinction'", () => {
    // With nothing to show there is nothing to judge, so the header the reader had a moment ago stays.
    const fn = APP.split("const distinguishes")[1].split("};")[0];
    expect(fn).toContain("if (items.length < 2) { return true; }");
  });

  it("says out loud that this is not permanent", () => {
    // The test is on the DATA, so a second provider brings the column back by itself. Written down so nobody
    // reports the absence as a defect — or "completes" the config thinking it was forgotten.
    expect(APP).toContain("NOT A PERMANENT REMOVAL");
  });

  it("KEEPS the pinned filter — pinning is something a person does, not data the provider sends", () => {
    /*
     * MEASURED: the filter reads `item.pinned`, and clicking the pin does flip it. It is zero today because
     * nobody has pinned anything, not because pinning is impossible — the opposite of the two columns above.
     *
     * ⚠ WHAT IS ALSO MEASURED, and recorded rather than fixed here: the pin writes to browser memory only and
     * is gone on reload. That is a separate finding (the fifth local-only path), not a reason to remove a
     * control that works.
     */
    expect(APP).toContain("if (state.pinnedFilter && !item.pinned) { return false; }");
    expect(APP).toContain("data-wcn-filter=\"pinned\"");
  });
});
