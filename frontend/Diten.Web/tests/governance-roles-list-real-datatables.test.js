const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * WP-ROLES-CLOSE-01 — the Roles screen on the list component, measured through the whole browser chain: the vendored
 * jQuery, DataTables and Buttons, the REAL dt-defaults.js toolbar, the REAL diten-datatable.js factory, the REAL
 * shared/diten-refusal.js and the REAL Roles index.js with its real filter and form markup. Only the network is fake.
 *
 *   C — parity with the screen before the component (rows, KPIs, system roles locked, no Add without the right) and the
 *       export standard: without auth.roles.export the Action menu is not drawn at all.
 *   A — a refusal is one sentence in the reader's language; a refusal without a code is the general sentence.
 *   D — the form's error box writes TEXT: a sentence carrying markup shows as characters, never as elements.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

const razor = (name) => read("Views", "Governance", "Roles", name)
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^@(using|inject|model)[^\n]*\n/gm, "")
  .replace(/@Html\.AntiForgeryToken\(\)/g, '<input name="__RequestVerificationToken" type="hidden" value="af-token" />')
  .replace(/@(?:Shared)?Localizer\["(\w+)"\]/g, "$1");

const ROLES = [
  { id: "r-admin", name: "Admin", displayName: "Administrator", description: "", isSystem: true, permissionCount: 7, userCount: 2, modulePermissions: { auth: 5, platform: 2 } },
  { id: "r-qa", name: "qa-reviewers", displayName: "QA Reviewers", description: "", isSystem: false, permissionCount: 1, userCount: 3, modulePermissions: { auth: 1 } },
  { id: "r-empty", name: "visitors", displayName: "Visitors", description: "", isSystem: false, permissionCount: 0, userCount: 0, modulePermissions: {} }
];

const L10N = {
  Actions: "İşlemler", AddNew: "Rol Ekle", Edit: "Düzenle", Delete: "Sil", QuickView: "Hızlı Bakış", AreYouSure: "Emin misiniz?",
  RoleTypeSystem: "Sistem", RoleTypeCustom: "Özel", SystemRoleLocked: "Sistem rolü", ManagePermissions: "İzinleri yönet",
  ErrorOccurred: "Bir hata oluştu.", FormValidationError: "Formu kontrol edin.", Save: "Kaydet", Update: "Güncelle",
  ErrorRoleNameTaken: "Bu adda bir rol zaten var. Farklı bir ad seçin.", ErrorRoleSystemNotDeletable: "Sistem rolleri silinemez.",
  ErrorRoleNotFound: "Bu rol artık yok. Sayfayı yenileyin.", ErrorRoleActorRequired: "Oturumunuz doğrulanamadı."
};

const ALL = ["auth.roles.read", "auth.roles.create", "auth.roles.update", "auth.roles.delete", "auth.roles.export"];

/** Mounts the Roles page for a reader holding `permissions`; `respond(url, init)` answers the screen's own fetches. */
const mountRoles = async (permissions, respond) => {
  document.body.innerHTML = razor("_Filter.cshtml")
    + ["kpi-roles-total", "kpi-roles-system", "kpi-roles-custom", "kpi-roles-users"].map((id) => `<h5 id="${id}">0</h5>`).join("")
    + '<div class="card"><div class="card-datatable"><table id="dt-roles" data-dt-standard="v2" data-dt-data-mode="client" class="datatables-roles table border-top">'
    + "<thead><tr><th></th><th>Name</th><th>DisplayName</th><th>RoleType</th><th>PermissionCount</th><th>UserCount</th><th>Actions</th></tr></thead></table></div></div>"
    + razor("_CreateEditOffcanvas.cshtml");

  class Modal { show() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Modal(); } }
  class Tooltip { constructor() {} dispose() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Tooltip(); } }
  const ctx = vm.createContext({
    window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
    setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
    bootstrap: {
      Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) },
      Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }), getInstance: () => null },
      Modal, Tooltip
    }
  });
  window.bootstrap = ctx.bootstrap;
  Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
  ctx.self = ctx;
  const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
  run("wwwroot/assets/vendor/libs/jquery/jquery.js");
  ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
  run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
  ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
  run("wwwroot/assets/vendor/libs/datatables-buttons/buttons.colVis.js");
  run("wwwroot/assets/vendor/libs/datatables-colreorder-bs5/dataTables.colReorder.min.js");
  run("wwwroot/assets/js/dt-defaults.js");
  run("wwwroot/assets/js/diten-datatable.js");
  run("wwwroot/assets/js/shared/diten-refusal.js");

  const listRequests = [];
  ctx.jQuery.ajaxTransport("+*", (options) => ({
    send(_headers, complete) {
      listRequests.push(options.url);
      const body = JSON.stringify({ isSuccessful: true, statusCode: 200, data: ROLES, errors: [] });
      setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
    },
    abort() {}
  }));
  const calls = [];
  ctx.fetch = async (url, init) => {
    calls.push({ url: String(url), method: init?.method || "GET" });
    const answer = respond ? respond(String(url), init) : null;
    const { status = 200, body = {} } = answer || {};
    return { ok: status >= 200 && status < 300, status, json: async () => body };
  };
  window.fetch = ctx.fetch;
  const toasts = [];
  window.showToast = (message, type) => toasts.push({ message, type });
  window.showConfirm = (_title, run_) => run_();
  window.API = { auth: "http://gw" };
  window.CurrentLanguage = "tr";
  window.CurrentUser = { tenantId: "t-1" };
  window.L10n = Object.assign({}, L10N);
  window.Permissions = { has: (key) => permissions.includes(key) };
  window.personalizationClient = { getViews: async () => [] };

  const createList = window.DitenDataTable.createList;
  window.DitenDataTable.createList = async (options) => { const handle = await createList(options); ctx.RolesListHandle = handle; ctx.RolesListOptions = options; return handle; };
  run("wwwroot/assets/js/Governance/Roles/index.js");
  vm.runInContext("RolesList.init()", ctx);
  window.DitenDataTable.createList = createList;
  await until(() => ctx.RolesListHandle && listRequests.length >= 1 && document.querySelectorAll("#dt-roles tbody tr").length >= ROLES.length);
  return { handle: ctx.RolesListHandle, options: ctx.RolesListOptions, calls, toasts, listRequests };
};

const menu = (dt) => ({
  print: dt.buttons("[data-export-format='print']").count(),
  csv: dt.buttons("[data-export-format='csv']").count(),
  excel: dt.buttons("[data-export-format='xlsx']").count(),
  pdf: dt.buttons("[data-export-format='pdf']").count(),
  copy: dt.buttons(".buttons-copy").count()
});
const rowOf = (name) => Array.from(document.querySelectorAll("#dt-roles tbody tr")).find((tr) => tr.textContent.includes(name));
const fillForm = (name, displayName) => {
  document.getElementById("roleName").value = name;
  document.getElementById("roleDisplayName").value = displayName;
};
const alertEl = () => document.getElementById("formRoleAlert");

describe("C — the Roles list on the component", () => {
  test("declares client data, asks for the whole set once and never a fixed page size", async () => {
    const { handle, options, listRequests } = await mountRoles(ALL);

    expect(options.dataMode).toBe("client");
    expect(options.export, "client mode: the browser holds every row").toBeUndefined();
    expect(listRequests[0].split("?")[0]).toBe("http://gw/api/roles"); // DataTables adds only its cache-buster
    expect(listRequests[0]).not.toMatch(/pageSize=|length=|start=/);
    expect(handle.dt.settings()[0].oFeatures.bServerSide).toBe(false);
    handle.dt.destroy();
  });

  test("shows every role, counts the KPI cards from the whole set and says the type in the reader's words", async () => {
    const { handle } = await mountRoles(ALL);

    expect(document.querySelectorAll("#dt-roles tbody tr").length).toBe(3);
    expect(document.getElementById("kpi-roles-total").textContent).toBe("3");
    expect(document.getElementById("kpi-roles-system").textContent).toBe("1");
    expect(document.getElementById("kpi-roles-custom").textContent).toBe("2");
    expect(document.getElementById("kpi-roles-users").textContent).toBe("5");
    expect(rowOf("Administrator").textContent).toContain("Sistem");
    expect(rowOf("QA Reviewers").textContent).toContain("Özel");

    // Search still narrows the rows; the KPI cards keep counting the whole set.
    handle.dt.search("QA").draw();
    expect(document.querySelectorAll("#dt-roles tbody tr").length).toBe(1);
    expect(document.getElementById("kpi-roles-total").textContent).toBe("3");
    handle.dt.destroy();
  });

  test("the file carries the screen's words: the type label and the modules behind the permission count", async () => {
    const { handle } = await mountRoles(ALL);

    // "export" is the orthogonal type the file buttons ask for ("filter" would also fold the accents for searching).
    const cells = handle.dt.cells(null, [3, 4]).render("export").toArray();
    expect(cells).toEqual(expect.arrayContaining(["Sistem", "Özel", "auth 5, platform 2", "auth 1", "0"]));
    expect(cells).not.toContain("System");
    // …while the column still sorts by the number.
    expect(handle.dt.cells(null, 4).render("sort").toArray().sort()).toEqual([0, 1, 7]);
    handle.dt.destroy();
  });

  test("a system role offers neither Edit nor Delete and wears the lock; a custom role offers both", async () => {
    const { handle } = await mountRoles(ALL);

    const system = rowOf("Administrator");
    expect(system.querySelector(".js-edit-item")).toBeNull();
    expect(system.querySelector(".delete-record")).toBeNull();
    expect(system.querySelector(".bx-lock-alt")).not.toBeNull();
    expect(system.querySelector(".js-quick-view")).not.toBeNull();
    expect(system.querySelector(".js-manage-perms").getAttribute("href")).toBe("/RoleAssignments?roleId=r-admin");

    const custom = rowOf("QA Reviewers");
    expect(custom.querySelector(".js-edit-item")).not.toBeNull();
    expect(custom.querySelector(".delete-record")).not.toBeNull();
    expect(custom.querySelector(".bx-lock-alt")).toBeNull();
    handle.dt.destroy();
  });

  test("without the rights there is no Add, no Edit and no Delete anywhere", async () => {
    const { handle } = await mountRoles(["auth.roles.read"]);

    expect(document.querySelector(".add-new")).toBeNull();
    expect(document.querySelector("#dt-roles .js-edit-item")).toBeNull();
    expect(document.querySelector("#dt-roles .delete-record")).toBeNull();
    handle.dt.destroy();
  });

  test("the type filter keeps only the chosen kind", async () => {
    const { handle, options } = await mountRoles(ALL);
    const type = options.filters.fields.find((f) => f.key === "type");

    expect(ROLES.filter((r) => type.matches(r, ["System"])).map((r) => r.name)).toEqual(["Admin"]);
    expect(ROLES.filter((r) => type.matches(r, ["Custom"])).map((r) => r.name)).toEqual(["qa-reviewers", "visitors"]);
    expect(ROLES.filter((r) => type.matches(r, [])).length).toBe(3);
    handle.dt.destroy();
  });
});

describe("C — the Roles Action menu follows auth.roles.export", () => {
  test("a reader WITHOUT the export key gets no Action menu at all — no Print, CSV, Excel, PDF, not even Copy", async () => {
    const { handle } = await mountRoles(["auth.roles.read", "auth.roles.create"]);

    expect(menu(handle.dt)).toEqual({ print: 0, csv: 0, excel: 0, pdf: 0, copy: 0 });
    expect(document.querySelector(".dt-export-collection-btn"), "the Action button itself is not drawn").toBeNull();
    handle.dt.destroy();
  });

  test("a reader WITH the export key sees all four file entries and Copy", async () => {
    const { handle } = await mountRoles(ALL);

    // Print, PDF and Copy are countable here; CSV and Excel are DataTables' own html5 buttons in client mode, which this
    // chain does not load — the control below reads them off the toolbar the page asked for.
    expect(menu(handle.dt)).toMatchObject({ print: 1, pdf: 1, copy: 1 });
    expect(document.querySelector(".dt-export-collection-btn")).not.toBeNull();
    handle.dt.destroy();
  });

  test("CONTROL — the toolbar the page asks for carries all five entries with the key and none without it", () => {
    const entriesFor = (exportPermitted) => window.DtDefaults
      .exportButtons("Add", {}, {}, { exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5], exportPermitted })
      .flatMap((group) => group.buttons || [])
      .filter((b) => String(b.className || "").includes("dt-export-collection-btn"))
      .flatMap((b) => b.buttons.map((e) => e.attr?.["data-export-format"] || e.extend));

    expect(entriesFor(true)).toEqual(["print", "csv", "excel", "pdf", "copy"]);
    expect(entriesFor(false)).toEqual([]);
  });
});

describe("A — refusals on the Roles screen", () => {
  test("ROLE_NAME_TAKEN from the proxy is one sentence in the reader's language, not the server's", async () => {
    const { handle } = await mountRoles(ALL, (url) => url === "/Roles/create"
      ? { body: { success: false, errors: ["Role name is already in use."], errorCode: "ROLE_NAME_TAKEN", local: false } }
      : null);
    handle.openCreate();
    fillForm("qa-reviewers", "QA Reviewers");

    await handle.submitForm();

    expect(alertEl().classList.contains("d-none")).toBe(false);
    expect(alertEl().textContent).toBe(L10N.ErrorRoleNameTaken);
    expect(alertEl().textContent).not.toContain("already in use");
    handle.dt.destroy();
  });

  test("a refusal without a code shows the general sentence and warns, never the raw one", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const { handle } = await mountRoles(ALL, (url) => url === "/Roles/create"
      ? { body: { success: false, errors: ["Some raw English sentence."], errorCode: null, local: false } }
      : null);
    handle.openCreate();
    fillForm("x", "X");

    await handle.submitForm();

    expect(alertEl().textContent).toBe(L10N.ErrorOccurred);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
    handle.dt.destroy();
  });

  test("deleting a system role by a stale row says ROLE_SYSTEM_NOT_DELETABLE in the reader's language", async () => {
    const { handle, options, toasts, calls } = await mountRoles(ALL, (url, init) => init?.method === "DELETE"
      ? { status: 403, body: { isSuccessful: false, statusCode: 403, errors: ["System roles cannot be deleted."], errorCodes: [{ code: "ROLE_SYSTEM_NOT_DELETABLE" }] } }
      : null);

    await options.actions.onRowAction.delete({ row: ROLES[1] });
    await until(() => toasts.length > 0);

    expect(calls.some((c) => c.method === "DELETE" && c.url === "http://gw/api/roles/r-qa")).toBe(true);
    expect(toasts).toEqual([{ message: L10N.ErrorRoleSystemNotDeletable, type: "error" }]);
    handle.dt.destroy();
  });
});

describe("D — the form's error box writes text, not markup", () => {
  test("a sentence carrying markup shows as characters; no element is created from it", async () => {
    const hostile = 'Rol "<img src=x onerror=alert(1)><b>QA</b>" kaydedilemedi.';
    const { handle } = await mountRoles(ALL, (url) => url === "/Roles/create"
      ? { body: { success: false, errors: [hostile], errorCode: null, local: true } }
      : null);
    handle.openCreate();
    fillForm("qa", "QA");

    await handle.submitForm();

    expect(alertEl().textContent).toBe(hostile);
    expect(alertEl().querySelector("img")).toBeNull();
    expect(alertEl().querySelector("b")).toBeNull();
    handle.dt.destroy();
  });

  test("the list cells escape a role's own name too", async () => {
    const { handle } = await mountRoles(ALL);

    const html = handle.dt.cell(0, 1).render("display");
    expect(String(html)).not.toMatch(/<(?!\/?span)/);
    handle.dt.destroy();
  });
});
