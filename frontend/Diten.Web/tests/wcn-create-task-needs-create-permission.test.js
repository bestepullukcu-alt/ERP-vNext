const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * DCP-004 "Decision amendment 2026-09-15" (BL-410), UAS-001 §6 — "+ Yeni ▸ Görev" is HIDDEN without
 * platform.tasks.create.
 *
 * Every tenant user opens the Task Center now, and most of them cannot create a task. A menu entry that can only end
 * in a refusal is the second error UAS-001 §6 forbids, so the entry is not rendered at all (not disabled). The
 * permission comes from the REAL permission helper (pages/governance/permissions.js) over the snapshot the host
 * view serializes, exactly as _PermissionBootstrap.cshtml loads it — not from a stub that could agree with anything.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);

const withSnapshot = (keys) => {
  delete global.Permissions;
  if (keys === undefined) {
    delete global.__permissionSnapshot;
    return;
  }
  global.__permissionSnapshot = keys;
  loadScript("wwwroot/assets/js/pages/governance/permissions.js");
};

const until = async (predicate, timeoutMs = 2000) => {
  const started = Date.now();
  while (!predicate()) {
    if (Date.now() - started > timeoutMs) { throw new Error("timed out waiting for the header"); }
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
};

const bootHeader = async () => {
  await bootSurface({ items: [] });
  // "Kaynağında oluştur" is rendered for everybody, so it is the header's arrival signal.
  await until(() => !!app().querySelector('[data-wcn-new="source"]'));
};

afterEach(() => {
  delete global.Permissions;
  delete global.__permissionSnapshot;
});

describe("+ Yeni ▸ Görev follows platform.tasks.create", () => {
  test("is shown, with its divider, to a user who holds the key", async () => {
    withSnapshot(["platform.tasks.read", "platform.tasks.create"]);
    await bootHeader();

    expect(app().querySelector('[data-wcn-new="task"]')).not.toBeNull();
    const sourceRow = app().querySelector('[data-wcn-new="source"]').closest("li");
    expect(sourceRow.previousElementSibling.querySelector("hr.dropdown-divider")).not.toBeNull();
  });

  test("is not rendered at all for a user without the key — hidden, not disabled", async () => {
    withSnapshot(["platform.tasks.read", "platform.tasks.update"]);
    await bootHeader();

    expect(app().querySelector('[data-wcn-new="task"]')).toBeNull();
    // The menu does not open on a dangling divider: the source entry is the first row.
    const sourceRow = app().querySelector('[data-wcn-new="source"]').closest("li");
    expect(sourceRow.previousElementSibling).toBeNull();
  });

  test("is hidden when the page carries no permission snapshot at all (fail-closed)", async () => {
    withSnapshot(undefined);
    await bootHeader();

    expect(app().querySelector('[data-wcn-new="task"]')).toBeNull();
  });
});

describe("both host views give app.js the permission snapshot", () => {
  test.each(["Index.cshtml", "Details.cshtml"])("%s loads _PermissionBootstrap before app.js", (view) => {
    const source = fs.readFileSync(web("Views", "WorkCenterNext", view), "utf8");
    const bootstrap = source.indexOf('<partial name="~/Views/Shared/_PermissionBootstrap.cshtml" />');
    const appJs = source.indexOf("assets/js/WorkCenterNext/app.js");

    expect(bootstrap, `${view} does not load the permission snapshot`).toBeGreaterThan(-1);
    expect(appJs).toBeGreaterThan(bootstrap);
  });
});
