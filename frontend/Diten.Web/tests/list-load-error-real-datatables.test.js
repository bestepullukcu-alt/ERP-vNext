const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-515 — A LIST THAT COULD NOT BE LOADED SAYS SO INSIDE THE TABLE, IN THE READER'S LANGUAGE.
 *
 * Through the VENDORED jQuery and DataTables and the real dt-defaults.js, in a browser-shaped vm context; only the
 * network is fake (a jQuery transport answering with the status under test, or holding the request open). The words
 * come from the shared DataTable payload (<script id="datatable-l10n">), here in Turkish, so an English sentence on
 * screen is a failure.
 *
 * CT-SHELL-FIX1 adds what the first delivery missed: a SERVER-SIDE list's first request (item 1), its processing
 * indicator (item 2), a cancelled request (item 3), the failure surviving later draws (item 4), a 200 answer that
 * carries `error` (item 7), and behaviour tests for the rules that only had a source-text check.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};
const settle = (ms = 40) => new Promise((r) => setTimeout(r, ms));

const TR = {
  DtLoadFailed: "Liste yüklenemedi.", DtLoadForbidden: "Bu listeyi görme yetkiniz yok.", DtRetry: "Yeniden Dene",
  DtEmptyTable: "Kayıt yok.", DtZeroRecords: "Eşleşen kayıt bulunamadı."
};
const rows = (n, from = 0) => Array.from({ length: n }, (_, i) => ({ code: `C${from + i + 1}`, name: `Row ${from + i + 1}` }));
const json = (body) => ({ status: 200, body: JSON.stringify(body) });

describe("a list whose request fails (vendored DataTables + jQuery, only the network is fake)", () => {
  let ctx;
  let answer;
  const requests = [];
  const held = [];
  const alerts = [];
  const refreshCalls = [];

  const reply = (complete, { status, body }) => (status === 0
    ? complete(0, "error")
    : complete(status, status < 400 ? "OK" : "Error", { text: body }, "Content-Type: application/json"));

  beforeAll(() => {
    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0),
      getComputedStyle: window.getComputedStyle.bind(window),
      bootstrap: { Modal: Object.assign(function Modal() { this.show = () => {}; this.hide = () => {}; },
          { getInstance: () => null, getOrCreateInstance: () => ({ show() {}, hide() {} }) }),
        Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Tooltip: function () {} }
    });
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx))
      .forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    window.alert = (message) => alerts.push(String(message));
    ctx.alert = window.alert;
    // The session refresh answers "unavailable" (no navigation: neither a reload nor the sign-in page), so each 401 test
    // sees its own refresh call. The reload-once rule after a SUCCESSFUL refresh: list-load-error-session-refresh.test.js.
    window.fetch = (url) => {
      refreshCalls.push(String(url));
      return Promise.resolve({ ok: false, status: 503, headers: { get: () => "text/plain" } });
    };
    ctx.fetch = window.fetch;
    window.DitenHttp = window.DitenHttp || { isJsonMediaType: () => false };
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    // Responsive takes its Modal constructor from `window.bootstrap` at load; hand it the stub the page would have.
    ctx.DataTable.Responsive?.bootstrap?.(ctx.bootstrap);
    run("wwwroot/assets/js/dt-defaults.js");

    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        requests.push({ url: options.url, withCredentials: options.xhrFields?.withCredentials === true });
        const next = answer(options);
        if (next.hold) { held.push(complete); return; }
        setTimeout(() => reply(complete, next), 0);
      },
      abort() {}
    }));
  });

  afterEach(() => { vi.restoreAllMocks(); held.length = 0; });

  const page = () => {
    document.head.innerHTML = `<script id="datatable-l10n" type="application/json">${JSON.stringify(TR)}</script>`;
    document.body.innerHTML =
      '<div class="card"><div class="card-datatable"><table id="dt-bl515"><thead><tr><th>Code</th><th>Name</th></tr></thead><tbody></tbody></table></div></div>';
    alerts.length = 0;
    refreshCalls.length = 0;
    return document.getElementById("dt-bl515");
  };

  const COLUMNS = [{ data: "code" }, { data: "name" }];
  const build = (ajax, extra = {}) => new ctx.DataTable(page(), ctx.window.DtDefaults.create(Object.assign({
    ajax, layout: {}, buttons: [], columns: COLUMNS, stateSave: false, order: [[0, "asc"]]
  }, extra)));
  const buildServer = (ajax, extra = {}) => build(ajax, Object.assign({ serverSide: true }, extra));
  const errorBox = () => document.querySelector("#dt-bl515 tbody [data-dt-load-error]");
  const bodyText = () => document.querySelector("#dt-bl515 tbody").textContent;
  const processing = () => document.getElementById("dt-bl515_processing");
  const startOf = (url) => new URL(url, "http://x").searchParams.get("start");

  test("the library's warning channel is no longer a browser alert", () => {
    expect(ctx.DataTable.ext.errMode).not.toBe("alert");
    expect(typeof ctx.DataTable.ext.errMode).toBe("function");
  });

  test.each([[500], [0]])("HTTP %s: the table says the list could not be loaded, in Turkish, and offers a retry that asks again", async (status) => {
    answer = () => ({ status, body: "{}" });
    build({ url: "/api/bl515" });
    await until(() => errorBox());

    expect(errorBox().getAttribute("data-dt-load-error")).toBe("failed");
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(alerts).toEqual([]);
    const before = requests.length;
    answer = () => json({ data: [{ code: "A1", name: "Alpha" }] });
    errorBox().querySelector("[data-dt-retry]").click();
    await until(() => bodyText().includes("Alpha"));
    expect(requests.length).toBe(before + 1);
    expect(errorBox()).toBeNull();
  });

  test("HTTP 403: its own sentence, and no retry (asking again changes nothing)", async () => {
    answer = () => ({ status: 403, body: "{}" });
    build({ url: "/api/bl515" });
    await until(() => errorBox());

    expect(errorBox().getAttribute("data-dt-load-error")).toBe("forbidden");
    expect(errorBox().textContent).toContain(TR.DtLoadForbidden);
    expect(errorBox().querySelector("[data-dt-retry]")).toBeNull();
    expect(alerts).toEqual([]);
  });

  test("HTTP 401: today's rule — the session is refreshed (then retried or sent to sign-in), nothing is drawn", async () => {
    answer = () => ({ status: 401, body: "{}" });
    build({ url: "/api/bl515" });
    await until(() => refreshCalls.length > 0);

    expect(refreshCalls).toContain("/account/refresh");
    expect(errorBox()).toBeNull();
    expect(alerts).toEqual([]);
  });

  test("a list whose ajax is a URL string is handled the same way — and its request carries the credentials like any create() list", async () => {
    answer = () => ({ status: 503, body: "{}" });
    build("/api/bl515-string");
    await until(() => errorBox());

    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(alerts).toEqual([]);
    // dt-defaults.js turns the string into `{ url }`, so the request is built by the component (withCredentials, its
    // error handling) and not by the library's bare string path.
    expect(requests.at(-1)).toEqual({ url: expect.stringContaining("/api/bl515-string"), withCredentials: true });
  });

  test("a table built WITHOUT create() — the library's own error path — shows the same sentence, not an alert", async () => {
    answer = () => ({ status: 500, body: "{}" });
    new ctx.DataTable(page(), { ajax: "/api/bl515-direct", columns: COLUMNS });
    await until(() => errorBox());

    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(alerts).toEqual([]);
  });

  // ── untested rules from the first delivery, now measured by behaviour ────────────────────────────────────────

  test("a table built WITHOUT create(), HTTP 401: the library's error channel refreshes the session and draws nothing", async () => {
    answer = () => ({ status: 401, body: "{}" });
    new ctx.DataTable(page(), { ajax: "/api/bl515-direct-401", columns: COLUMNS });
    await until(() => refreshCalls.length > 0);
    await settle();

    expect(refreshCalls).toEqual(["/account/refresh"]);
    expect(errorBox()).toBeNull();
    expect(alerts).toEqual([]);
  });

  test("a table built WITHOUT create(), an answer that is not JSON (parsererror, tn 1): the failure is shown in the table", async () => {
    answer = () => ({ status: 200, body: "<html>not json</html>" });
    new ctx.DataTable(page(), { ajax: "/api/bl515-direct-html", columns: COLUMNS });
    await until(() => errorBox());

    expect(errorBox().getAttribute("data-dt-load-error")).toBe("failed");
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
  });

  test("a page's own ajax.error that returns false hands the failure to the component; one that returns nothing keeps it", async () => {
    const calls = [];
    answer = () => ({ status: 500, body: "{}" });
    build({ url: "/api/bl515-page-gives-up", error: () => { calls.push("gives-up"); return false; } });
    await until(() => errorBox());
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);

    build({ url: "/api/bl515-page-keeps", error: () => { calls.push("keeps"); } });
    await until(() => calls.includes("keeps"));
    await settle();
    expect(errorBox()).toBeNull();
    expect(calls).toEqual(["gives-up", "keeps"]);
  });

  test("the retry keeps the reader's page: a server-side list on page 3 asks for page 3 again", async () => {
    answer = () => json({ data: rows(10), recordsTotal: 50, recordsFiltered: 50 });
    const dt = buildServer({ url: "/api/bl515-paged" });
    await until(() => bodyText().includes("C1"));

    answer = () => ({ status: 500, body: "{}" });
    dt.page(2).draw("page");
    await until(() => errorBox());
    expect(startOf(requests.at(-1).url)).toBe("20");

    answer = () => json({ data: rows(10, 20), recordsTotal: 50, recordsFiltered: 50 });
    errorBox().querySelector("[data-dt-retry]").click();
    await until(() => bodyText().includes("C21"));
    expect(startOf(requests.at(-1).url)).toBe("20");
  });

  // ── item 1 + 2: a server-side list ───────────────────────────────────────────────────────────────────────────

  test.each([
    [500, "failed", TR.DtLoadFailed, true],
    [403, "forbidden", TR.DtLoadForbidden, false]
  ])("a SERVER-SIDE list whose FIRST request answers %s shows its sentence in the table, and the indicator goes", async (status, kind, sentence, retry) => {
    answer = () => ({ status, body: "{}" });
    buildServer({ url: "/api/bl515-server" });
    await until(() => errorBox());

    expect(errorBox().getAttribute("data-dt-load-error")).toBe(kind);
    expect(errorBox().textContent).toContain(sentence);
    expect(Boolean(errorBox().querySelector("[data-dt-retry]"))).toBe(retry);
    expect(processing().style.display).toBe("none");
    expect(alerts).toEqual([]);
  });

  test("a server-side list whose first request failed loads on retry, and the failure is gone", async () => {
    answer = () => ({ status: 500, body: "{}" });
    buildServer({ url: "/api/bl515-server" });
    await until(() => errorBox());

    answer = () => json({ data: [{ code: "S1", name: "Server row" }], recordsTotal: 1, recordsFiltered: 1 });
    errorBox().querySelector("[data-dt-retry]").click();
    await until(() => bodyText().includes("Server row"));
    expect(errorBox()).toBeNull();
  });

  test("a server-side list that fails while a sort click is loading: the failure row is there and the indicator is not", async () => {
    answer = () => json({ data: rows(10), recordsTotal: 30, recordsFiltered: 30 });
    buildServer({ url: "/api/bl515-server-sort" });
    await until(() => bodyText().includes("C1"));

    answer = () => ({ hold: true });
    const before = requests.length;
    document.querySelector("#dt-bl515 thead th").click();
    await until(() => requests.length > before);
    expect(processing().style.display).toBe("block");

    reply(held.shift(), { status: 500, body: "{}" });
    await until(() => errorBox());
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(processing().style.display).toBe("none");
  });

  // ── item 3: a cancelled request is not a failure ────────────────────────────────────────────────────────────

  test("reloading a list cancels the request in flight: nothing is drawn for it, the indicator stays for the new one", async () => {
    const logged = vi.spyOn(console, "error");
    answer = () => ({ hold: true });
    const dt = build({ url: "/api/bl515-first" });
    await until(() => held.length === 1);

    answer = () => ({ hold: true });
    dt.ajax.url("/api/bl515-second").load();   // the library aborts the first request (textStatus "abort")
    await until(() => held.length === 2);
    await settle();

    expect(errorBox()).toBeNull();
    expect(processing().style.display).toBe("block");
    expect(logged.mock.calls.some((call) => String(call[0]).includes("Ajax error"))).toBe(false);

    reply(held[1], json({ data: [{ code: "N1", name: "New answer" }] }));
    await until(() => bodyText().includes("New answer"));
    expect(errorBox()).toBeNull();
  });

  test("leaving the page while a list loads: the dropped request (status 0) draws nothing; back on the page, status 0 is a failure again", async () => {
    answer = () => ({ status: 0 });
    window.dispatchEvent(new window.Event("beforeunload"));
    try {
      const before = requests.length;
      build({ url: "/api/bl515-leaving" });
      await until(() => requests.length > before);
      await settle();
      expect(errorBox()).toBeNull();
    } finally {
      window.dispatchEvent(new window.Event("pageshow"));
    }

    build({ url: "/api/bl515-unreachable" });
    await until(() => errorBox());
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
  });

  // ── item 4: the failure survives every later draw until a load succeeds ─────────────────────────────────────

  test("after a failed load, a sort click, a search, a page length and a hidden column all keep the failure (no 'no records')", async () => {
    answer = () => ({ status: 500, body: "{}" });
    const dt = build({ url: "/api/bl515-client" });
    await until(() => errorBox());

    document.querySelector("#dt-bl515 thead th").click();
    await settle();
    expect(errorBox()).not.toBeNull();

    dt.search("Al").draw();
    expect(errorBox()).not.toBeNull();
    dt.page.len(25).draw();
    expect(errorBox()).not.toBeNull();
    dt.column(1).visible(false);
    expect(errorBox()).not.toBeNull();
    expect(bodyText()).not.toContain(TR.DtEmptyTable);
    expect(bodyText()).not.toContain(TR.DtZeroRecords);

    answer = () => json({ data: [{ code: "A1", name: "Alpha" }] });
    dt.search("").draw();
    errorBox().querySelector("[data-dt-retry]").click();
    await until(() => bodyText().includes("A1"));
    expect(errorBox()).toBeNull();

    // Loaded once: the state is gone, and the library's own empty sentence is true again.
    dt.search("zzz").draw();
    expect(errorBox()).toBeNull();
    expect(bodyText()).toContain(TR.DtZeroRecords);
  });

  // ── item 7: a 200 answer that says `error` ─────────────────────────────────────────────────────────────────

  test.each([
    ["client-side", false, { error: "DB timeout", data: [] }],
    ["server-side", true, { error: "DB timeout", data: [], recordsTotal: 0, recordsFiltered: 0 }]
  ])("a %s list whose 200 answer carries `error` shows the failure in the table — and keeps it on the next draw", async (_kind, serverSide, body) => {
    const logged = vi.spyOn(console, "error");
    answer = () => json(body);
    const dt = serverSide ? buildServer({ url: "/api/bl515-json-error" }) : build({ url: "/api/bl515-json-error" });
    await until(() => errorBox());

    expect(errorBox().getAttribute("data-dt-load-error")).toBe("failed");
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(logged.mock.calls.some((call) => JSON.stringify(call[1] || "").includes("DB timeout"))).toBe(true);
    expect(alerts).toEqual([]);

    if (!serverSide) {
      dt.search("x").draw();
      expect(errorBox()).not.toBeNull();
      expect(bodyText()).not.toContain(TR.DtEmptyTable);
    }
  });

  test("a developer warning (tn 4, a row without a column's field) stays in the console: the rows are drawn, no failure, no alert", async () => {
    const logged = vi.spyOn(console, "error");
    answer = () => json({ data: [{ code: "D1" }] });
    build({ url: "/api/bl515-tn4" });
    await until(() => bodyText().includes("D1"));
    await settle();

    expect(errorBox()).toBeNull();
    expect(alerts).toEqual([]);
    expect(logged.mock.calls.some((call) => call[1] && call[1].techNote === 4)).toBe(true);
  });
});

describe("the pages that had their own handler now leave the failure to the component", () => {
  const js = (rel) => read("wwwroot", "assets", "js", ...rel.split("/"));

  test("Reference Data: its dead xhr/error listeners are gone; its ajax.error only takes its own notices down", () => {
    const source = js("Platform/ReferenceData/index.js");
    expect(source).not.toMatch(/dt\.on\('xhr\.dt'/);
    expect(source).not.toMatch(/dt\.on\('error\.dt'/);
    expect(source).toContain("error: () => { show(loadingEl, false); show(emptyEl, false); return false; }");
  });

  test("Pharmacovigilance case intake: keeps only its controlled-refusal sentence and hands every other failure over", () => {
    const source = js("Pharmacovigilance/CaseIntakeTriage/index.js");
    expect(source).toContain("if (!hasReasonCode(body)) return false;");
    expect(source).not.toMatch(/showAlert\(safeMessage\(tryParseJson\(xhr\?\.responseText\), xhr\?\.status\)\)/);
  });

  test("the component no longer carries its old English toasts", () => {
    const source = js("dt-defaults.js");
    expect(source).not.toMatch(/showToast\('Permission denied\.'/);
    expect(source).not.toMatch(/DataTables Ajax error \(HTTP/);
  });
});

describe("the list's failure sentences ship in every language", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const KEYS = ["DtLoadFailed", "DtLoadForbidden", "DtRetry"];
  const NEW_KEYS = ["DtLoadFailed", "DtLoadForbidden", "DtRetry", "ShellSearchPlaceholder"];
  const value = (lang, key) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const match = xml.match(new RegExp(`<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`));
    return match ? match[1].trim() : null;
  };

  test.each(LANGS)("%s carries all three", (lang) => {
    for (const key of KEYS) expect([key, value(lang, key)]).toEqual([key, expect.stringMatching(/\S/)]);
  });

  test.each(LANGS.filter((lang) => lang !== "en"))("%s translates each of the four new keys (its value is not the English one)", (lang) => {
    for (const key of NEW_KEYS) {
      expect([key, value(lang, key)]).toEqual([key, expect.stringMatching(/\S/)]);
      expect([key, value(lang, key)]).not.toEqual([key, value("en", key)]);
    }
  });

  test("the shared payload delivers them to every list", () => {
    const partial = read("Views", "Shared", "_DataTableL10n.cshtml");
    for (const key of KEYS) expect(partial).toContain(`"${key}"`);
  });
});
