const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-515 — A LIST THAT COULD NOT BE LOADED SAYS SO INSIDE THE TABLE, IN THE READER'S LANGUAGE.
 *
 * Through the VENDORED jQuery and DataTables and the real dt-defaults.js, in a browser-shaped vm context; only the
 * network is fake (a jQuery transport answering with the status under test). The words come from the shared
 * DataTable payload (<script id="datatable-l10n">), here in Turkish, so an English sentence on screen is a failure.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

const TR = { DtLoadFailed: "Liste yüklenemedi.", DtLoadForbidden: "Bu listeyi görme yetkiniz yok.", DtRetry: "Yeniden dene" };

describe("a list whose request fails (vendored DataTables + jQuery, only the network is fake)", () => {
  let ctx;
  let answer;
  const requests = [];
  const alerts = [];
  const refreshCalls = [];

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
    window.fetch = (url) => { refreshCalls.push(String(url)); return new Promise(() => {}); };
    ctx.fetch = window.fetch;
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
        requests.push(options.url);
        const { status, body } = answer();
        setTimeout(() => (status === 0
          ? complete(0, "error")
          : complete(status, status < 400 ? "OK" : "Error", { text: body }, "Content-Type: application/json")), 0);
      },
      abort() {}
    }));
  });

  const page = () => {
    document.head.innerHTML = `<script id="datatable-l10n" type="application/json">${JSON.stringify(TR)}</script>`;
    document.body.innerHTML =
      '<div class="card"><div class="card-datatable"><table id="dt-bl515"><thead><tr><th>Code</th><th>Name</th></tr></thead><tbody></tbody></table></div></div>';
    alerts.length = 0;
    refreshCalls.length = 0;
    return document.getElementById("dt-bl515");
  };

  const build = (ajax) => new ctx.DataTable(page(), ctx.window.DtDefaults.create({
    ajax, layout: {}, buttons: [], columns: [{ data: "code" }, { data: "name" }]
  }));
  const errorBox = () => document.querySelector("#dt-bl515 tbody [data-dt-load-error]");

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
    answer = () => ({ status: 200, body: JSON.stringify({ data: [{ code: "A1", name: "Alpha" }] }) });
    errorBox().querySelector("[data-dt-retry]").click();
    await until(() => document.querySelector("#dt-bl515 tbody").textContent.includes("Alpha"));
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

  test("a list whose ajax is a URL string is handled the same way", async () => {
    answer = () => ({ status: 503, body: "{}" });
    build("/api/bl515-string");
    await until(() => errorBox());

    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(alerts).toEqual([]);
  });

  test("a table built WITHOUT create() — the library's own error path — shows the same sentence, not an alert", async () => {
    answer = () => ({ status: 500, body: "{}" });
    new ctx.DataTable(page(), { ajax: "/api/bl515-direct", columns: [{ data: "code" }, { data: "name" }] });
    await until(() => errorBox());

    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
    expect(alerts).toEqual([]);
  });
});

describe("the three pages that had their own handler now leave the failure to the component", () => {
  const js = (rel) => read("wwwroot", "assets", "js", ...rel.split("/"));

  test("Reference Data: its dead xhr/error listeners are gone; its ajax.error only takes its loading notice down", () => {
    const source = js("Platform/ReferenceData/index.js");
    expect(source).not.toMatch(/dt\.on\('xhr\.dt'/);
    expect(source).not.toMatch(/dt\.on\('error\.dt'/);
    expect(source).toContain("error: () => { show(loadingEl, false); return false; }");
  });

  test("Pharmacovigilance case intake: keeps only its controlled-refusal sentence and hands every other failure over", () => {
    const source = js("Pharmacovigilance/CaseIntakeTriage/index.js");
    expect(source).toContain("if (!hasReasonCode(body)) return false;");
    expect(source).not.toMatch(/showAlert\(safeMessage\(tryParseJson\(xhr\?\.responseText\), xhr\?\.status\)\)/);
  });

  test("a page handler that returns false is handed back to the component (the delegation the two pages rely on)", () => {
    const source = js("dt-defaults.js");
    expect(source).toContain("return handled === false ? defaultAjaxError.call(this, xhr, textStatus, errorThrown) : handled;");
    expect(source).not.toMatch(/showToast\('Permission denied\.'/);
    expect(source).not.toMatch(/DataTables Ajax error \(HTTP/);
  });
});

describe("the list's failure sentences ship in every language", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const KEYS = ["DtLoadFailed", "DtLoadForbidden", "DtRetry"];
  const value = (lang, key) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const match = xml.match(new RegExp(`<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`));
    return match ? match[1].trim() : null;
  };

  test.each(LANGS)("%s carries all three", (lang) => {
    for (const key of KEYS) expect([key, value(lang, key)]).toEqual([key, expect.stringMatching(/\S/)]);
  });

  test("the shared payload delivers them to every list", () => {
    const partial = read("Views", "Shared", "_DataTableL10n.cshtml");
    for (const key of KEYS) expect(partial).toContain(`"${key}"`);
  });
});
