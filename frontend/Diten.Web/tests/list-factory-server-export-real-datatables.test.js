const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-452 package 1 — THE FILE IS THE SCREEN, through the whole browser chain: the vendored jQuery, DataTables and its
 * Buttons, the REAL dt-defaults.js toolbar, the REAL diten-datatable.js factory and the REAL Users index.js. Only the
 * NETWORK is fake: the list answers through a jQuery transport, the export through `fetch`.
 *
 * What is measured is the owner's decision (2026-09-24): on a server-mode list the Excel/CSV entry of the Action menu
 * asks the service for EVERY matching row — the columns the reader sees, in their order, the applied filter, the search
 * and the sort — and never the page DataTables holds (start/length). A client-mode list keeps DataTables' own buttons.
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

describe("server export through the vendored DataTables + Buttons, the real toolbar, factory and Users page", () => {
  let ctx;
  const listRequests = [];
  const fetches = [];
  const downloads = [];
  const toasts = [];
  let exportStatus = 200;

  const usersList = () => ctx.UsersListHandle;
  const exportFetches = () => fetches.filter((f) => f.url.startsWith("http://gw/api/users/export"));
  const excel = () => usersList().dt.button(".dt-server-export[data-export-format='xlsx']");
  const csv = () => usersList().dt.button(".dt-server-export[data-export-format='csv']");

  beforeAll(async () => {
    document.body.innerHTML = filterMarkup()
      + ["kpi-users-total", "kpi-users-active", "kpi-users-passive", "kpi-users-norole"].map((id) => `<h5 id="${id}">0</h5>`).join("")
      + '<div class="card"><div class="card-datatable"><table id="dt-users" data-dt-standard="v2" data-dt-data-mode="server" class="datatables-users table border-top">'
      + "<thead><tr><th></th><th>Email</th><th>FirstName</th><th>LastName</th><th>Roles</th><th>AccountKind</th><th>Status</th><th>Actions</th></tr></thead></table></div></div>"
      + '<table id="dt-client"><thead><tr><th></th><th></th><th>Code</th><th>Name</th><th>Actions</th></tr></thead><tbody></tbody></table>';

    class Modal { show() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Modal(); } }
    class Tooltip { constructor() {} dispose() {} hide() {} static getInstance() { return null; } static getOrCreateInstance() { return new Tooltip(); } }
    ctx = vm.createContext({
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
    // The tenant shell's DataTables scripts, in its order (_LayoutTenantShell.cshtml): the column-visibility buttons
    // and ColReorder are separate files there too.
    run("wwwroot/assets/vendor/libs/datatables-buttons/buttons.colVis.js");
    run("wwwroot/assets/vendor/libs/datatables-colreorder-bs5/dataTables.colReorder.min.js");
    run("wwwroot/assets/js/dt-defaults.js");
    run("wwwroot/assets/js/diten-datatable.js");

    // The list: AuthService's server contract.
    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        listRequests.push(options.url);
        const body = JSON.stringify({
          success: true,
          data: {
            items: [{ id: "u1", email: "ali@t.test", firstName: "Ali", lastName: "Kaya", isActive: false, status: "Invited", roles: [], accountKind: "Human" }],
            total: 9, filteredTotal: 9, summary: { total: 9, active: 5, passive: 1, invited: 3, noRole: 2 }
          }
        });
        setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));

    // The export (and the page's role lookup) go through fetch.
    ctx.fetch = async (url, init) => {
      fetches.push({ url: String(url), init: init || {} });
      if (String(url).includes("/api/users/export")) {
        return {
          ok: exportStatus >= 200 && exportStatus < 300,
          status: exportStatus,
          headers: { get: (h) => (h.toLowerCase() === "content-disposition" ? "attachment; filename=users-20260925-1412.xlsx; filename*=UTF-8''users-20260925-1412.xlsx" : null) },
          blob: async () => new window.Blob(["file"])
        };
      }
      return { ok: true, status: 200, json: async () => ({ data: [] }) };
    };
    window.fetch = ctx.fetch;
    window.URL.createObjectURL = () => "blob:export";
    window.URL.revokeObjectURL = () => {};
    window.HTMLAnchorElement.prototype.click = function () { downloads.push({ href: this.href, download: this.download }); };
    window.showToast = (key, type) => toasts.push([key, type]);

    window.API = { auth: "http://gw" };
    window.CurrentLanguage = "tr";
    window.CurrentUser = { tenantId: "t-1" };
    window.L10n = { Active: "Active", Passive: "Passive", StatusInvited: "Invited", AddNew: "Add", Actions: "Actions" };
    window.Permissions = { has: () => true };
    window.personalizationClient = { getViews: async () => [] };

    // Keep the handle the page builds, so the test can drive the table the way the reader does.
    const createList = window.DitenDataTable.createList;
    window.DitenDataTable.createList = async (options) => { const handle = await createList(options); ctx.UsersListHandle = handle; return handle; };
    run("wwwroot/assets/js/Governance/Users/index.js");
    vm.runInContext("UsersList.init()", ctx);
    window.DitenDataTable.createList = createList;
    await until(() => ctx.UsersListHandle && listRequests.length >= 1);
  });

  test("the Excel entry is the server export: the service's whole list with the visible columns, not the page", async () => {
    expect(usersList().exportMode).toBe("server");
    expect(excel().node(), "the Action menu carries a server Excel entry").toBeTruthy();

    excel().trigger();
    await until(() => exportFetches().length === 1 && downloads.length === 1);

    const call = exportFetches()[0];
    const url = decodeURIComponent(call.url);
    expect(url).toBe("http://gw/api/users/export?format=xlsx&columns=email,firstName,lastName,roles,accountKind,status&orderBy=email&orderDir=asc");
    expect(url, "the page on screen never limits the file").not.toMatch(/[?&](start|length|draw)=/);
    expect(call.init.credentials, "the auth cookie travels").toBe("include");
    expect(call.init.headers["X-Tenant-Id"]).toBe("t-1");
    expect(call.init.headers["Accept-Language"], "the file is written in the reader's language").toBe("tr");
    expect(downloads[0]).toEqual({ href: "blob:export", download: "users-20260925-1412.xlsx" });
  });

  test("page size 5, Last Name hidden, Status = Invited, a search and a sort → exactly that goes to the export", async () => {
    const dt = usersList().dt;
    dt.page.len(5).draw(false);
    dt.column(3).visible(false);
    window.jQuery("#filterStatus").val(["Invited"]);
    document.getElementById("btnFilterApply").click();
    dt.search("ali").order([1, "desc"]).draw();
    await until(() => decodeURIComponent(listRequests.at(-1)).includes("search=ali"));
    const lastList = decodeURIComponent(listRequests.at(-1));
    expect(lastList, "the list itself pages by 5").toMatch(/start=0&length=5/);

    const before = exportFetches().length;
    csv().trigger();
    await until(() => exportFetches().length === before + 1);

    const url = decodeURIComponent(exportFetches().at(-1).url);
    expect(url).toBe("http://gw/api/users/export?format=csv&columns=email,firstName,roles,accountKind,status&search=ali&orderBy=email&orderDir=desc&status=Invited");
    expect(url, "a hidden column is not asked for").not.toMatch(/lastName/);
    expect(url).not.toMatch(/[?&](start|length|draw)=/);
  });

  test("413 EXPORT_TOO_LARGE is said to the reader and nothing is saved", async () => {
    exportStatus = 413;
    const saved = downloads.length;
    excel().trigger();
    await until(() => toasts.some((t) => t[0] === "ExportTooLarge"));
    expect(toasts.at(-1)).toEqual(["ExportTooLarge", "warning"]);
    expect(downloads.length).toBe(saved);
    exportStatus = 200;
  });

  test("CONTROL — a client-mode list keeps DataTables' own csv/excel buttons, built exactly as before", () => {
    const features = window.DtDefaults.exportButtons("Add", {}, {}, { exportColumns: [2, 3], colvisColumns: [2, 3] });
    const collection = features[0].buttons[0].buttons;
    const byText = (t) => collection.find((b) => String(b.text).includes(t));
    expect(byText("CSV").extend).toBe("csv");
    expect(byText("Excel").extend).toBe("excel");
    expect(typeof byText("CSV").exportOptions.columns, "K16 visible-column selector still in place").toBe("function");
    expect(collection.some((b) => String(b.className).includes("dt-server-export"))).toBe(false);
  });

  test("CONTROL — the factory refuses a server export on a client list, an unknown mode, or no url", async () => {
    const base = { tableEl: document.getElementById("dt-client"), config: { columns: [{ data: "id" }, { data: "id" }, { data: "code" }, { data: "name" }, { data: "id" }] } };
    await expect(window.DitenDataTable.createList({ ...base, dataMode: "client", export: { mode: "server", url: "/x/export" } })).rejects.toThrow(/needs dataMode 'server'/);
    await expect(window.DitenDataTable.createList({ ...base, dataMode: "server", export: { mode: "browser", url: "/x/export" } })).rejects.toThrow(/export\.mode must be 'server'/);
    await expect(window.DitenDataTable.createList({ ...base, dataMode: "server", export: { mode: "server" } })).rejects.toThrow(/export\.url is required/);
  });
});
