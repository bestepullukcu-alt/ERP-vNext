const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-452 standard 1 (WP-AUTH-PLATFORM-LINKS-01 v2, F3) — a reader without auth.users.export does not SEE the file
 * entries: no Print, CSV, Excel or PDF in the Action menu (Copy stays). Measured through the whole browser chain — the
 * vendored jQuery, DataTables and Buttons, the REAL dt-defaults.js toolbar, the REAL diten-datatable.js factory and
 * the REAL Users index.js; only the network is fake (the shape of list-factory-server-export-real-datatables.test.js).
 * The server's 403 + AccessDenied path stays for a deep-linked request; that is not what is measured here.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

const filterMarkup = () => read("Views", "Governance", "Users", "_Filter.cshtml")
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^@(using|inject)[^\n]*\n/gm, "")
  .replace(/@(?:Shared)?Localizer\["(\w+)"\]/g, "$1");

/** Mounts the Users page for a reader holding `permissions`; returns the list handle the page built. */
const mountUsers = async (permissions) => {
  document.body.innerHTML = filterMarkup()
    + ["kpi-users-total", "kpi-users-active", "kpi-users-passive", "kpi-users-norole"].map((id) => `<h5 id="${id}">0</h5>`).join("")
    + '<div class="card"><div class="card-datatable"><table id="dt-users" data-dt-standard="v2" data-dt-data-mode="server" class="datatables-users table border-top">'
    + "<thead><tr><th></th><th>Email</th><th>FirstName</th><th>LastName</th><th>Roles</th><th>AccountKind</th><th>Status</th><th>Actions</th></tr></thead></table></div></div>";

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

  const listRequests = [];
  ctx.jQuery.ajaxTransport("+*", (options) => ({
    send(_headers, complete) {
      listRequests.push(options.url);
      const body = JSON.stringify({
        success: true,
        data: { items: [], total: 0, filteredTotal: 0, summary: { total: 0, active: 0, passive: 0, invited: 0, noRole: 0 } }
      });
      setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
    },
    abort() {}
  }));
  ctx.fetch = async () => ({ ok: true, status: 200, json: async () => ({ data: [] }) });
  window.fetch = ctx.fetch;
  window.showToast = () => {};
  window.API = { auth: "http://gw" };
  window.CurrentLanguage = "tr";
  window.CurrentUser = { tenantId: "t-1" };
  window.L10n = { Active: "Active", Passive: "Passive", StatusInvited: "Invited", AddNew: "Add", Actions: "Actions" };
  window.Permissions = { has: (key) => permissions.includes(key) };
  window.personalizationClient = { getViews: async () => [] };

  const createList = window.DitenDataTable.createList;
  window.DitenDataTable.createList = async (options) => { const handle = await createList(options); ctx.UsersListHandle = handle; return handle; };
  run("wwwroot/assets/js/Governance/Users/index.js");
  vm.runInContext("UsersList.init()", ctx);
  window.DitenDataTable.createList = createList;
  await until(() => ctx.UsersListHandle && listRequests.length >= 1);
  return ctx.UsersListHandle;
};

/** The Action menu's entries as DataTables built them: print / csv / excel / pdf / copy. */
const menu = (dt) => ({
  // Package 2 (controlled copy) builds Print and PDF as its own entries, marked by data-export-format like CSV/Excel.
  print: dt.buttons("[data-export-format='print']").count(),
  csv: dt.buttons("[data-export-format='csv']").count(),
  excel: dt.buttons("[data-export-format='xlsx']").count(),
  pdf: dt.buttons("[data-export-format='pdf']").count(),
  copy: dt.buttons(".buttons-copy").count()
});

describe("the Users Action menu follows auth.users.export", () => {
  test("a reader WITHOUT the export key sees no Print, CSV, Excel or PDF — Copy stays", async () => {
    const handle = await mountUsers(["auth.users.read"]);

    expect(menu(handle.dt)).toEqual({ print: 0, csv: 0, excel: 0, pdf: 0, copy: 1 });
    handle.dt.destroy();
  });

  test("a reader WITH the export key sees all four file entries and Copy", async () => {
    const handle = await mountUsers(["auth.users.read", "auth.users.export"]);

    expect(menu(handle.dt)).toEqual({ print: 1, csv: 1, excel: 1, pdf: 1, copy: 1 });
    handle.dt.destroy();
  });

  test("CONTROL — a page that never passes exportPermitted keeps the whole menu", () => {
    const features = window.DtDefaults.exportButtons("Add", {}, {}, { exportColumns: [2, 3], colvisColumns: [2, 3] });
    const entries = features[0].buttons[0].buttons.map((b) => b.attr?.["data-export-format"] || b.extend);
    expect(entries).toEqual(["print", "csv", "excel", "pdf", "copy"]);
  });
});
