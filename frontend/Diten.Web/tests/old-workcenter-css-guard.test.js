const fs = require("fs");
const path = require("path");

/*
 * WP-WEB-BL619-02 (BL-619 follow-up) — the old Görev Merkezi (/WorkCenter) is gone, and with it the styles only it drew:
 * the task detail step bar (.task-step-*), the view switch, the "all work" type filter, the inbox pane and the MOD-0034
 * inbox search / filter block. They were removed from backbone-custom.css after a repository-wide search found no
 * view or script using them. WorkCenterNext and CRM's inbox rows (.inbox-row*) keep theirs.
 *
 * This guard keeps the removal honest in both directions: none of these names comes back in a view, a script or the
 * stylesheet. A screen that needs one again gets its own, named class — not a resurrected leftover.
 */
const WEB = path.resolve(__dirname, "..");

const REMOVED = [
  "task-step",                       // .task-step-bar / -label / -index / -connector / --done … / -offflow
  "work-center-view-switch",
  "workcenter-allwork-type-filter",
  "workcenter-inbox-pane",
  // MOD-0034 inbox search & filter
  "wc-inbox-search",
  "wc-search-wrap",
  "wc-search-suggestions",
  "wc-search-suggestion-item",
  "wc-search-suggestion-btn",
  "wc-filter-select",
  "inboxFilterBtn",
  "inboxFilterHost",
  "inboxStateContainer",
  "inboxDefaultState",
  "inboxBulkState"
];

const walk = (dir, accept, out = []) => {
  if (!fs.existsSync(dir)) { return out; }
  fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!["bin", "obj", "node_modules", "lib", "vendor"].includes(entry.name)) { walk(full, accept, out); }
    } else if (accept(entry.name)) { out.push(full); }
  });
  return out;
};

const views = [
  ...walk(path.join(WEB, "Views"), (name) => name.endsWith(".cshtml")),
  ...walk(path.join(WEB, "Areas"), (name) => name.endsWith(".cshtml")),
  ...walk(path.join(WEB, "Pages"), (name) => name.endsWith(".cshtml"))
];
const scripts = walk(path.join(WEB, "wwwroot", "assets", "js"), (name) => name.endsWith(".js"));
const stylesheet = path.join(WEB, "wwwroot", "assets", "css", "backbone-custom.css");

const uses = (files) => files.flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  return REMOVED.filter((name) => source.includes(name)).map((name) => `${path.relative(WEB, file)}: ${name}`);
});

describe("the old Görev Merkezi styles stay removed (WP-WEB-BL619-02)", () => {
  test("the scan reaches the views and scripts (guards against a vacuous pass)", () => {
    expect(views.length).toBeGreaterThan(300);
    expect(scripts.length).toBeGreaterThan(100);
    expect(views.some((file) => file.includes(`${path.sep}WorkCenterNext${path.sep}`))).toBe(true);
    // the inbox rows CRM uses are NOT part of the removal
    expect(fs.readFileSync(stylesheet, "utf8")).toContain(".inbox-row {");
  });

  test("no view uses a removed class or id", () => {
    expect(uses(views)).toEqual([]);
  });

  test("no script uses a removed class or id, built or literal", () => {
    expect(uses(scripts)).toEqual([]);
  });

  test("the stylesheet does not carry them again", () => {
    expect(uses([stylesheet])).toEqual([]);
  });
});
