const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * THE USERS SCREEN ON THE WIRE — WP-UI-USERS-LIST-01 (BL-440 package 5, 2026-09-24).
 *
 * governance-users-list-component.test.js records what the page HANDS the factory. This file runs the whole chain the
 * browser runs — the vendored jQuery and DataTables, the real diten-datatable.js factory, the real Users index.js and
 * the filter markup lifted out of the real _Filter.cshtml — and replaces only the NETWORK, with a transport that
 * answers like AuthService's GET api/users (server contract, WP-AUTH-USERS-LIST-QUERY-01) and GET api/roles.
 *
 * What only this can show: the query that actually leaves the browser (the service's whitelist: orderBy=email, the
 * status WORD Inactive, role IDS), that the KPI cards come from `summary` of the response DataTables consumed, and
 * that a filter is applied by a NEW request rather than by hiding rows of the page in hand.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

const ROLE_ADMIN = "3f2c1a8e-0000-4000-8000-000000000001";
const ROLE_AUDITOR = "3f2c1a8e-0000-4000-8000-000000000002";

/** _Filter.cshtml with the Razor stripped: the localizer calls become their key names, the markup stays as written. */
const filterMarkup = () => read("Views", "Governance", "Users", "_Filter.cshtml")
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^@(using|inject)[^\n]*\n/gm, "")
  .replace(/@(?:Shared)?Localizer\["(\w+)"\]/g, "$1");

describe("Users through the vendored DataTables + jQuery + the real factory (only the network is fake)", () => {
  let ctx;
  const requests = [];

  beforeAll(() => {
    document.body.innerHTML = filterMarkup()
      + ["kpi-users-total", "kpi-users-active", "kpi-users-passive", "kpi-users-norole"].map((id) => `<h5 id="${id}">0</h5>`).join("")
      + '<div class="card"><div class="card-datatable"><table id="dt-users" data-dt-standard="v2" data-dt-data-mode="server" class="datatables-users table border-top">'
      + "<thead><tr><th></th><th>Email</th><th>FirstName</th><th>LastName</th><th>Roles</th><th>AccountKind</th><th>Status</th><th>Actions</th></tr></thead></table></div></div>";

    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }), getInstance: () => null }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } }
    });
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    run("wwwroot/assets/js/diten-datatable.js");

    // The network. /api/users answers with the server contract; the summary is tenant-wide, not the page's.
    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        requests.push(options.url);
        const draw = Number(/[?&]draw=(\d+)/.exec(options.url)?.[1] || 0);
        const body = JSON.stringify({
          success: true,
          data: {
            items: [
              { id: `u${draw}a`, email: "metin.aydin@ditenpharma.test", firstName: "Metin", lastName: "Aydın", isActive: true, status: "Active", roles: ["Admin"], accountKind: "Unknown", mustChangePassword: false },
              { id: `u${draw}b`, email: "elif.cetin@ditenpharma.test", firstName: "Elif", lastName: "Çetin", isActive: false, status: "Invited", roles: [], accountKind: "Human", mustChangePassword: true }
            ],
            total: 1204, filteredTotal: 1204,
            summary: { total: 1204, active: 1001, passive: 40, invited: 163, noRole: 12 }
          }
        });
        setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
    // /api/roles goes through fetch (the page's own lookup), not through the DataTables transport.
    ctx.fetch = async (url) => {
      requests.push(String(url));
      return { ok: true, json: async () => ({ data: [{ id: ROLE_AUDITOR, name: "Auditor" }, { id: ROLE_ADMIN, name: "Admin" }] }) };
    };

    window.API = { auth: "http://gw" };
    window.L10n = { Active: "Active", Passive: "Passive", StatusInvited: "Invited", AddNew: "Add", Actions: "Actions" };
    window.Permissions = { has: () => true };
    window.DtDefaults = { create: (cfg) => cfg, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {} };
    window.personalizationClient = { getViews: async () => [] };
    run("wwwroot/assets/js/Governance/Users/index.js");
    vm.runInContext("UsersList.init()", ctx);
  });

  const usersRequests = () => requests.filter((u) => u.startsWith("http://gw/api/users")).map(decodeURIComponent);

  test("the first request is the service's query: paged, sorted by email, no cap, no positional dump", async () => {
    await until(() => usersRequests().length >= 1 && document.getElementById("kpi-users-total").textContent !== "0");
    const first = usersRequests()[0];
    expect(first).toMatch(/^http:\/\/gw\/api\/users\?start=0&length=10&orderBy=email&orderDir=asc&draw=1(&_=\d+)?$/);
    expect(first, "the 1000-row cap is back").not.toMatch(/pageSize|page=/);
    expect(first).not.toMatch(/columns\[|order\[|search\[/);
  });

  test("the KPI cards are the response's summary, and the rows are the page DataTables drew", async () => {
    await until(() => document.getElementById("kpi-users-total").textContent === "1204");
    expect(document.getElementById("kpi-users-active").textContent).toBe("1001");
    expect(document.getElementById("kpi-users-passive").textContent, "Passive counts the Inactive, never the invited").toBe("40");
    expect(document.getElementById("kpi-users-norole").textContent).toBe("12");
    const table = document.getElementById("dt-users");
    expect(table.querySelectorAll("tbody tr").length).toBe(2);
    expect(table.textContent).toContain("metin.aydin@ditenpharma.test");
    expect(table.textContent, "the invited row reads its own state, not Passive").toContain("Invited");
  });

  test("the role filter is filled from api/roles with role IDS, sorted by name", async () => {
    await until(() => document.querySelectorAll("#filterRoles option").length === 2);
    const options = [...document.querySelectorAll("#filterRoles option")].map((o) => [o.value, o.textContent]);
    expect(options).toEqual([[ROLE_ADMIN, "Admin"], [ROLE_AUDITOR, "Auditor"]]);
  });

  test("Apply sends a NEW request carrying the service's words: status=Inactive, roleId=<guid>, accountKind=<name>", async () => {
    const before = usersRequests().length;
    window.jQuery("#filterStatus").val(["Inactive", "Invited"]);
    window.jQuery("#filterRoles").val([ROLE_ADMIN]);
    window.jQuery("#filterAccountKind").val(["Human"]);
    document.getElementById("btnFilterApply").click();
    await until(() => usersRequests().length > before);
    const last = usersRequests().at(-1);
    expect(last).toContain("&status=Inactive&status=Invited");
    expect(last).toContain(`&roleId=${ROLE_ADMIN}`);
    expect(last).toContain("&accountKind=Human");
    expect(last, "the service refuses Passive (USERS_LIST_STATUS_INVALID)").not.toMatch(/status=Passive/);
    expect(last).toMatch(/start=0&length=10&orderBy=email&orderDir=asc/);
  });
});
