const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * THE USERS SCREEN STANDS ON THE LIST COMPONENT, IN SERVER MODE — BL-440 package 5 (WP-UI-USERS-LIST-01, 2026-09-24).
 *
 * MEASURED before this package: Users/index.js 1162 lines; 79 of its 108 top-level names were plumbing copied from
 * Golden Slim; `pageSize=1000` fetched a capped set and filtered it in the browser (the 1001st user never appeared,
 * the KPI cards counted the capped set); the JS bound bulk selection into a column the markup never drew; the status
 * filter sent the screen's word "Passive", which the server contract refuses; the verifier reported 17 deviations.
 *
 * Two kinds of guard, both against the PRODUCTION files:
 *   1. static — the page defines none of the 79 names (the list is read out of list-factory-golden-pages.test.js,
 *      never re-typed here), declares server mode in markup AND JS, and draws no selection column on purpose;
 *   2. behavioural — index.js is RUN in a sandbox whose createList records what the page hands the factory, so the
 *      KPI source, the kebab's permission gates and the delete sentence are measured on what the page does.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const stripComments = (s) => s.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:'"`])\/\/.*$/gm, "$1");
const withoutRazorComments = (text) => text.replace(/@\*[\s\S]*?\*@/g, "");
const JS_PATH = ["wwwroot", "assets", "js", "Governance", "Users", "index.js"];
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** The 79 plumbing names, taken from the ONE place they are listed (the golden-pages guard). */
const loadPlumbing = () => {
  const source = fs.readFileSync(path.join(__dirname, "list-factory-golden-pages.test.js"), "utf8");
  const from = source.indexOf("const PLUMBING = {");
  const to = source.indexOf("\n};", from);
  expect(from, "the 79-name list moved out of list-factory-golden-pages.test.js").toBeGreaterThan(-1);
  // eslint-disable-next-line no-new-func
  return new Function(source.slice(from, to + 3) + "\nreturn PLUMBING;")();
};

describe("static: the page writes only its own business", () => {
  test("none of the 79 plumbing names is defined on the Users page", () => {
    const plumbing = loadPlumbing();
    const names = Object.values(plumbing).flat();
    expect(names.length).toBe(79);
    const source = stripComments(read(...JS_PATH));
    const offenders = [];
    Object.entries(plumbing).forEach(([family, list]) => list.forEach((name) => {
      if (new RegExp(`\\b(?:const|let|var|function)\\s+${name}\\b`).test(source)) offenders.push(`${name} (${family})`);
    }));
    expect(offenders, `Users grew plumbing back: ${offenders.join(", ")}`).toEqual([]);
  });

  test("it calls createList in server mode and stays within 450 lines", () => {
    const raw = read(...JS_PATH);
    const source = stripComments(raw);
    expect(source).toMatch(/window\.DitenDataTable\.createList\(\{/);
    expect(source).toMatch(/dataMode: 'server'/);
    expect(raw.split("\n").length, "Users/index.js is over its 450-line budget").toBeLessThanOrEqual(450);
  });

  test("no page cap, no browser-side filter hook, no direct persistence, no own DataTable", () => {
    const source = stripComments(read(...JS_PATH));
    expect(source, "a page cap silently cuts the list (the 1001st user)").not.toMatch(/pageSize=/);
    expect(source, "server mode: the service filters, never the browser").not.toMatch(/ext\.search/);
    expect(source, "a field with a row matcher is dead code in server mode").not.toMatch(/\bmatches\s*:/);
    expect(source, "persistence is the factory's").not.toMatch(/personalizationClient/);
    expect(source, "the table is built by the factory").not.toMatch(/new\s+DataTable\s*\(|DtDefaults\.create\s*\(|createCrudTable\s*\(/);
    expect(source, "stateSave is the factory's decision").not.toMatch(/stateSave/);
  });

  test("_Filter sends the SERVICE's status word: Inactive, never Passive", () => {
    const filter = withoutRazorComments(read("Views", "Governance", "Users", "_Filter.cshtml"));
    expect(filter).toMatch(/<option value="Inactive">@SharedLocalizer\["Passive"\]<\/option>/);
    expect(filter, "AuthService answers status=Passive with 400 USERS_LIST_STATUS_INVALID").not.toMatch(/value="Passive"/);
    ["Active", "Inactive", "Invited"].forEach((v) => expect(filter).toContain(`<option value="${v}">`));
  });

  test("_DataTable calls the shell, in server mode, with no selection column (K17: no bulk endpoint)", () => {
    const view = withoutRazorComments(read("Views", "Governance", "Users", "_DataTable.cshtml"));
    expect(view).toMatch(/<partial name="~\/Views\/Shared\/Components\/DataTable\/_ListShell\.cshtml" model="listShell" \/>/);
    expect(view, "a hand-written table is back").not.toMatch(/<table/);
    expect(view).toMatch(/TableId = "dt-users"/);
    expect(view).toMatch(/DataMode = "server"/);
    expect(view).toMatch(/Slug = "users"/);
    expect(view, "a selection column with no bulk endpoint selects into nothing").toMatch(/HasSelection = false/);
    expect(view).not.toMatch(/HasSelection = true/);
    // The JS agrees: it hands the factory no bulk options, and the page draws no bulk bar.
    expect(stripComments(read(...JS_PATH))).not.toMatch(/\bbulk\s*:/);
    expect(withoutRazorComments(read("Views", "Governance", "Users", "Index.cshtml"))).not.toMatch(/_BulkActionBar/);
  });

  test("the shell's columns and the JS columns are the same eight, in the same order", () => {
    const view = withoutRazorComments(read("Views", "Governance", "Users", "_DataTable.cshtml"));
    const headers = [...view.matchAll(/\{ Header = (?:Shared)?Localizer\["(\w+)"\]\.Value/g)].map((m) => m[1]);
    expect(headers).toEqual(["Email", "FirstName", "LastName", "Roles", "AccountKind", "Status"]);
    expect(view).toMatch(/ActionsHeader = Localizer\["Actions"\]\.Value/);
    const names = [...read(...JS_PATH).matchAll(/\{ data: '(\w+)', name: '(\w+)' \}/g)].map((m) => m[2]);
    // control + the six data columns + actions — no checkbox column between control and email.
    expect(names).toEqual(["control", "email", "firstName", "lastName", "roles", "accountKind", "status", "action"]);
  });

  describe("DeleteUserConfirmText (finding #10) — seven languages, the bridge and the loader", () => {
    const value = (lang) => {
      const m = /<data name="DeleteUserConfirmText" xml:space="preserve"><value>([^<]+)<\/value><\/data>/
        .exec(read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`));
      return m ? m[1].trim() : null;
    };
    test.each(LANGS)("UsersIndex.%s.resx carries it", (lang) => {
      expect(value(lang), `DeleteUserConfirmText missing in ${lang}`).toBeTruthy();
    });
    test("the six non-English values are translated, not copies", () => {
      const same = LANGS.filter((l) => l !== "en" && value(l) === value("en"));
      expect(same, `untranslated English in: ${same.join(", ")}`).toEqual([]);
    });
    test("the Turkish sentence is the owner's", () => {
      expect(value("tr")).toBe("Kullanıcı silinir, rolleri kaldırılır; hesap geri getirilemez. Aynı e-posta ile yeni bir hesap açılabilir.");
    });
    test("bridged and required", () => {
      expect(read("Views", "Governance", "Users", "_IndexL10n.cshtml")).toMatch(/DeleteUserConfirmText = Localizer\["DeleteUserConfirmText"\]\.Value/);
      expect(read("wwwroot", "assets", "js", "Governance", "Users", "index.l10n.js")).toContain("'DeleteUserConfirmText'");
    });
  });
});

/*
 * ── Behavioural: RUN the page and record what it hands the factory ─────────────────────────────────────────────
 * A sandbox with the real jsdom document and a stub `createList` that records its options. Nothing of the page is
 * copied: the KPI writer, the row-action renderer, the delete handler and the form's submit are the page's own
 * functions, reached through the options it passed.
 */
describe("behaviour: what the page hands the factory", () => {
  const L10N = {
    AddNew: "Add", Edit: "Edit", Delete: "Delete", QuickView: "Quick view", AreYouSure: "Sure?", ErrorOccurred: "Error",
    Disable: "Disable", Enable: "Enable", ResendInvitation: "Resend", ResetPassword: "Reset", InvitationPendingHint: "pending",
    DeleteUserConfirmText: "The user is deleted…", ErrorUserEmailTaken: "E-mail taken", Active: "Active", Passive: "Passive", StatusInvited: "Invited"
  };
  const ALL = ["auth.users.read", "auth.users.create", "auth.users.update", "auth.users.delete"];

  /** Loads index.js into a fresh sandbox and runs init(); returns what createList received plus the stubs. */
  const run = async (permissions, fetchImpl) => {
    document.body.innerHTML = '<table class="datatables-users"></table>'
      + ['kpi-users-total', 'kpi-users-active', 'kpi-users-passive', 'kpi-users-norole'].map((id) => `<h5 id="${id}">0</h5>`).join('');
    const calls = { createList: [], confirms: [], renderActions: [], reloads: [] };
    const handle = { reload: (key) => calls.reloads.push(key), openCreate() {}, openEdit() {}, dt: { ajax: { reload() {} } } };
    const win = {
      API: { auth: "http://gw" },
      L10n: L10N,
      Permissions: { has: (key) => permissions.includes(key) },
      DitenDataTable: {
        getAuthHeaders: () => ({}),
        renderActions: (actions) => { calls.renderActions.push(actions); return ""; },
        createList: async (options) => { calls.createList.push(options); return handle; }
      },
      showConfirm: (title, onConfirm, options) => calls.confirms.push({ title, onConfirm, options }),
      showToast() {}
    };
    const sandbox = vm.createContext({ window: win, document, console, setTimeout, fetch: fetchImpl || (async () => ({ ok: true, json: async () => ({}) })) });
    vm.runInContext(read(...JS_PATH), sandbox, { filename: "Users/index.js" });
    vm.runInContext("UsersList", sandbox).init();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(calls.createList.length, "init() did not build the list through createList").toBe(1);
    return { options: calls.createList[0], calls };
  };
  const actionsFor = async (permissions, row) => {
    const { options, calls } = await run(permissions);
    const render = options.config.columnDefs.find((d) => d.targets === -1).render;
    render(row.id, "display", row);
    return calls.renderActions.pop().map((a) => a.key);
  };
  const activeRow = { id: "u1", email: "a@x.io", isActive: true, status: "Active", mustChangePassword: false, roles: [] };
  const invitedRow = { id: "u2", email: "b@x.io", isActive: false, status: "Invited", mustChangePassword: true, roles: [] };

  test("server mode against /api/users, no cap, no bulk; filter keys are the service's parameters", async () => {
    const { options } = await run(ALL);
    expect(options.dataMode).toBe("server");
    expect(options.ajax.url).toBe("http://gw/api/users");
    expect(options.bulk).toBeUndefined();
    expect(options.filters.fields.map((f) => [f.id, f.key, f.kind])).toEqual([
      ["filterStatus", "status", "multi"], ["filterRoles", "roleId", "multi"], ["filterAccountKind", "accountKind", "multi"]
    ]);
    expect(options.filters.fields.some((f) => "matches" in f)).toBe(false);
    expect(options.savedView).toMatchObject({ moduleKey: "Governance", pageKey: "Users", baseOrder: [[1, "asc"]] });
    // The roles column is not a service sort key; every other data column's `data` is one.
    const roles = options.config.columnDefs.find((d) => d.targets === 4);
    expect(roles.orderable).toBe(false);
  });

  test("the four KPI cards are written from data.summary — not counted from the page of rows", async () => {
    const { options } = await run(ALL);
    expect(typeof options.onResponse, "the page hands the factory no onResponse: the cards stay 0").toBe("function");
    options.onResponse({ success: true, data: { items: [activeRow], total: 1234, filteredTotal: 1, summary: { total: 1234, active: 1000, passive: 34, invited: 193, noRole: 7 } } });
    expect(document.getElementById("kpi-users-total").textContent).toBe("1234");
    expect(document.getElementById("kpi-users-active").textContent).toBe("1000");
    // Passive is the service's Inactive count — the 193 invited are NOT in it, and there is no fifth card.
    expect(document.getElementById("kpi-users-passive").textContent).toBe("34");
    expect(document.getElementById("kpi-users-norole").textContent).toBe("7");
    expect(document.body.textContent).not.toContain("193");
  });

  test("the kebab follows the permissions: nothing but the quick view without them", async () => {
    expect(await actionsFor(ALL, activeRow)).toEqual(["quickView", "edit", "disable", "reset", "delete"]);
    expect(await actionsFor(["auth.users.read"], activeRow)).toEqual(["quickView"]);
  });

  test("no 'Delete' without auth.users.delete — and only that item goes", async () => {
    expect(await actionsFor(ALL.filter((k) => k !== "auth.users.delete"), activeRow)).toEqual(["quickView", "edit", "disable", "reset"]);
  });

  test("an invited account: resend only — never activate, never reset", async () => {
    expect(await actionsFor(ALL, invitedRow)).toEqual(["quickView", "edit", "resend", "delete"]);
  });

  test("no '+ Add' is drawn without auth.users.create (absent, not hidden)", async () => {
    expect((await run(ALL.filter((k) => k !== "auth.users.create"))).options.toolbar.addNewText).toBe("");
    expect((await run(ALL)).options.toolbar.addNewText).toBe("Add");
  });

  test("finding #10: the delete confirm says what happens, with the delete glyph", async () => {
    const { options, calls } = await run(ALL);
    options.actions.onRowAction.delete({ row: activeRow });
    const confirm = calls.confirms.pop();
    expect(confirm.options).toMatchObject({ entityName: "a@x.io", subtext: L10N.DeleteUserConfirmText, type: "danger", icon: "bx-trash" });
  });

  test("a refusal with a stable code reaches the form in the reader's language", async () => {
    const fetchImpl = async () => ({ ok: false, json: async () => ({ success: false, errorCode: "USER_EMAIL_TAKEN", errors: ["raw gateway text"] }) });
    const { options } = await run(ALL, fetchImpl);
    const result = await options.form.submit(new FormData(), false, { editingId: null, headers: {} });
    expect(result.errors).toEqual(["E-mail taken"]);
  });
});
