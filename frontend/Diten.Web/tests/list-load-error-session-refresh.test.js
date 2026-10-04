const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * CT-SHELL-FIX1 item 6 — a session refresh that "works" while the list keeps answering 401 must not reload the page
 * forever (the JWT 7.x ↔ 8.16 incident: /account/refresh succeeded, every API call still said 401).
 *
 * Through the vendored jQuery and DataTables and the real dt-defaults.js. A browser's `location` cannot be watched in
 * jsdom (its members are unforgeable), so the page's `window` is the jsdom window seen through a proxy that answers
 * `location` and `sessionStorage` with recorders — everything else is the real window.
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

const TR = { DtLoadFailed: "Liste yüklenemedi.", DtLoadForbidden: "Bu listeyi görme yetkiniz yok.", DtRetry: "Yeniden Dene" };

describe("a list that keeps answering 401 after a successful session refresh", () => {
  let ctx;
  let answer;
  let storageBroken = false;
  const requests = [];
  const refreshCalls = [];
  const store = new Map();
  const fakeLocation = {
    hostname: "localhost", pathname: "/Governance/Users", search: "", href: "", reloads: 0,
    reload() { this.reloads += 1; }
  };
  const fakeStorage = {
    getItem(key) { if (storageBroken) throw new Error("SecurityError"); return store.has(key) ? store.get(key) : null; },
    setItem(key, value) { if (storageBroken) throw new Error("SecurityError"); store.set(key, String(value)); },
    removeItem(key) { store.delete(key); }
  };

  beforeAll(() => {
    // jsdom's own window methods check their receiver; anything a script put on window (jQuery itself) is left alone.
    const WINDOW_METHODS = new Set(["addEventListener", "removeEventListener", "dispatchEvent", "getComputedStyle",
      "matchMedia", "setTimeout", "clearTimeout", "setInterval", "clearInterval", "requestAnimationFrame",
      "cancelAnimationFrame", "getSelection", "scrollTo", "open", "focus", "blur", "postMessage"]);
    const pageWindow = new Proxy(window, {
      get(target, prop) {
        if (prop === "location") return fakeLocation;
        if (prop === "sessionStorage") return fakeStorage;
        const value = Reflect.get(target, prop, target);
        return typeof value === "function" && WINDOW_METHODS.has(prop) ? value.bind(target) : value;
      },
      set(target, prop, value) { return Reflect.set(target, prop, value, target); }
    });
    ctx = vm.createContext({
      window: pageWindow, document, navigator: window.navigator, location: fakeLocation, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0),
      getComputedStyle: window.getComputedStyle.bind(window),
      bootstrap: { Modal: Object.assign(function Modal() { this.show = () => {}; this.hide = () => {}; },
          { getInstance: () => null, getOrCreateInstance: () => ({ show() {}, hide() {} }) }),
        Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Tooltip: function () {} }
    });
    Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx))
      .forEach((k) => { ctx[k] = window[k]; });
    ctx.self = ctx;
    window.alert = () => { throw new Error("no alert box"); };
    // /account/refresh succeeds every time — exactly the incident.
    window.fetch = (url) => {
      refreshCalls.push(String(url));
      return Promise.resolve({ ok: true, status: 200, headers: { get: () => "application/json" }, json: async () => ({}) });
    };
    ctx.fetch = window.fetch;
    window.DitenHttp = { isJsonMediaType: () => true };
    const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    ctx.DataTable.Responsive?.bootstrap?.(ctx.bootstrap);
    run("wwwroot/assets/js/dt-defaults.js");

    ctx.jQuery.ajaxTransport("+*", (options) => ({
      send(_headers, complete) {
        requests.push(options.url);
        const { status, body } = answer();
        setTimeout(() => complete(status, status < 400 ? "OK" : "Error", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
  });

  beforeEach(() => {
    store.clear();
    storageBroken = false;
    fakeLocation.reloads = 0;
    fakeLocation.href = "";
    refreshCalls.length = 0;
  });

  const page = () => {
    document.head.innerHTML = `<script id="datatable-l10n" type="application/json">${JSON.stringify(TR)}</script>`;
    document.body.innerHTML =
      '<div class="card"><div class="card-datatable"><table id="dt-refresh"><thead><tr><th>Code</th><th>Name</th></tr></thead><tbody></tbody></table></div></div>';
    return document.getElementById("dt-refresh");
  };
  const COLUMNS = [{ data: "code" }, { data: "name" }];
  // A reload is a fresh page: dt-defaults.js runs again (its in-memory state starts over; sessionStorage does not).
  // The table is built WITHOUT create(): its 401 reaches the library's error channel, which has no request to retry.
  const openPage = async () => {
    vm.runInContext(read("wwwroot", "assets", "js", "dt-defaults.js"), ctx, { filename: "dt-defaults.js" });
    const before = refreshCalls.length;
    new ctx.DataTable(page(), { ajax: "/api/refresh-probe", columns: COLUMNS });
    await until(() => refreshCalls.length > before);
    await settle();
  };
  const errorBox = () => document.querySelector("#dt-refresh tbody [data-dt-load-error]");

  test("the first time the page reloads once; the second time it goes to the sign-in page instead of reloading again", async () => {
    answer = () => ({ status: 401, body: "{}" });

    await openPage();
    expect(fakeLocation.reloads).toBe(1);
    expect(fakeLocation.href).toBe("");

    await openPage();   // the reloaded page: the same 401, the same successful refresh
    expect(fakeLocation.reloads).toBe(1);
    expect(fakeLocation.href).toBe("/account/login?returnUrl=%2FGovernance%2FUsers");
  });

  test("sent straight back by the sign-in page, the page stops navigating and says so in the table", async () => {
    answer = () => ({ status: 401, body: "{}" });
    await openPage();
    await openPage();
    fakeLocation.href = "";

    await openPage();   // the sign-in page accepted the cookie and returned the reader here
    expect(fakeLocation.reloads).toBe(1);
    expect(fakeLocation.href).toBe("");
    await until(() => errorBox());
    expect(errorBox().textContent).toContain(TR.DtLoadFailed);
  });

  test("two lists on one page that both answer 401 reload the page once — the second is not counted as a second time", async () => {
    answer = () => ({ status: 401, body: "{}" });
    vm.runInContext(read("wwwroot", "assets", "js", "dt-defaults.js"), ctx, { filename: "dt-defaults.js" });
    page();
    document.querySelector(".card-datatable").insertAdjacentHTML("beforeend",
      '<table id="dt-refresh-2"><thead><tr><th>Code</th><th>Name</th></tr></thead><tbody></tbody></table>');
    new ctx.DataTable(document.getElementById("dt-refresh"), { ajax: "/api/refresh-probe-a", columns: COLUMNS });
    new ctx.DataTable(document.getElementById("dt-refresh-2"), { ajax: "/api/refresh-probe-b", columns: COLUMNS });
    await until(() => refreshCalls.length >= 1);
    await settle(80);

    expect(fakeLocation.reloads).toBe(1);
    expect(fakeLocation.href).toBe("");
  });

  test("without a usable sessionStorage the page cannot count, so it never reloads blind: it goes to the sign-in page", async () => {
    answer = () => ({ status: 401, body: "{}" });
    storageBroken = true;

    await openPage();
    expect(fakeLocation.reloads).toBe(0);
    expect(fakeLocation.href).toBe("/account/login?returnUrl=%2FGovernance%2FUsers");
  });

  test("a create() list whose ajax was a URL string retries the same request after the refresh — it does not reload the page", async () => {
    let calls = 0;
    answer = () => { calls += 1; return calls === 1 ? { status: 401, body: "{}" } : { status: 200, body: JSON.stringify({ data: [{ code: "R1", name: "Retried" }] }) }; };
    vm.runInContext(read("wwwroot", "assets", "js", "dt-defaults.js"), ctx, { filename: "dt-defaults.js" });
    const before = requests.length;
    new ctx.DataTable(page(), ctx.window.DtDefaults.create({
      ajax: "/api/refresh-string", layout: {}, buttons: [], columns: COLUMNS, stateSave: false, order: [[0, "asc"]]
    }));
    await until(() => document.querySelector("#dt-refresh tbody").textContent.includes("Retried"));

    expect(requests.slice(before)).toEqual([expect.stringContaining("/api/refresh-string"), expect.stringContaining("/api/refresh-string")]);
    expect(refreshCalls).toEqual(["/account/refresh"]);
    expect(fakeLocation.reloads).toBe(0);
  });
});
