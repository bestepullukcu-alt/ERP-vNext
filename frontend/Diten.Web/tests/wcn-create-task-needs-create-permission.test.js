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
  // BL-589 — "+ Yeni" may now be absent, so the header itself is the arrival signal.
  await until(() => !!app().querySelector(".wcn-header"));
};

afterEach(() => {
  delete global.Permissions;
  delete global.__permissionSnapshot;
});

describe("+ Yeni ▸ Görev follows platform.tasks.create", () => {
  test("is shown to a user who holds the key — and it is the menu's only item", async () => {
    withSnapshot(["platform.tasks.read", "platform.tasks.create"]);
    await bootHeader();

    expect(app().querySelector("[data-wcn-new-menu]")).not.toBeNull();
    expect(app().querySelector('[data-wcn-new="task"]')).not.toBeNull();
    expect([...app().querySelectorAll("[data-wcn-new]")].map((x) => x.getAttribute("data-wcn-new"))).toEqual(["task"]);
    expect(app().querySelector(".wcn-header-actions hr.dropdown-divider")).toBeNull();
  });

  test("is not rendered at all for a user without the key — hidden, not disabled", async () => {
    withSnapshot(["platform.tasks.read", "platform.tasks.update"]);
    await bootHeader();

    expect(app().querySelector('[data-wcn-new="task"]')).toBeNull();
  });

  test("is hidden when the page carries no permission snapshot at all (fail-closed)", async () => {
    withSnapshot(undefined);
    await bootHeader();

    expect(app().querySelector('[data-wcn-new="task"]')).toBeNull();
  });
});

/*
 * BL-589 (WP-WCN-FIX-01, UAS-001 §6, BL-208) — "+ Yeni ▸ Kaynak modülde oluştur" was a mock: drawn without a
 * permission check, its module list built from the user's own work items, its confirm only toasting "(mock)". It is
 * removed, and "+ Yeni" itself is not drawn when no item is left: a user with no create permission sees no button.
 */
describe("BL-589: no mock create-in-source, no empty + Yeni", () => {
  test("a user without platform.tasks.create (e.g. no role) gets no + Yeni button at all", async () => {
    withSnapshot(["platform.tasks.read"]);
    await bootHeader();

    expect(app().querySelector("[data-wcn-new-menu]")).toBeNull();
    expect(app().querySelectorAll("[data-wcn-new]").length).toBe(0);
  });

  test("no permission snapshot: no + Yeni button (fail-closed)", async () => {
    withSnapshot(undefined);
    await bootHeader();

    expect(app().querySelector("[data-wcn-new-menu]")).toBeNull();
  });

  test.each([[["platform.tasks.read", "platform.tasks.create"]], [["platform.tasks.read"]]])(
    "the create-in-source entry is never drawn (permissions %j)", async (keys) => {
      withSnapshot(keys);
      await bootHeader();

      expect(app().querySelector('[data-wcn-new="source"]')).toBeNull();
    });

  test("the mock dialog is gone from the source: no openCreateInSource, no create-in-source dispatch", () => {
    const appJs = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
    expect(appJs).not.toMatch(/const openCreateInSource\s*=/);
    expect(appJs).not.toMatch(/kind === 'source'/);
    expect(appJs).not.toMatch(/createItem\('source'/);
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
