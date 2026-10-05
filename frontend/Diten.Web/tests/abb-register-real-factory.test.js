const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * WP-MDM-ABB-LIST-01 FIX1 — the ABB register page through the REAL list component: the vendored jQuery and DataTables,
 * the real diten-datatable.js and the page's own index.js. Only the network (a jQuery transport) and the saved-view store
 * (personalizationClient) are fakes. A mocked, already-resolved createList cannot show what the review found: the factory
 * hands its handle back BEFORE the table's init, and its own start then writes the saved filter into the bar.
 *
 *   item 1 — a Task Center link (?globalProductId=) is the product the list asks for, over a saved view of another one;
 *   item 2 — a refused list read speaks the register's sentence, not DtDefaults' English;
 *   item 3 — an old (eight-column) default view is carried over to the versioned key with its indexes mapped;
 *   item 7 — the product's display name never travels on the list request.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};
const settle = () => new Promise((r) => setTimeout(r, 120));

const LINKED = "11111111-1111-4111-8111-111111111111";
const SAVED = "22222222-2222-4222-8222-222222222222";

function pageDom() {
  document.body.innerHTML =
    '<div id="product-abbreviation-register" data-can-audit="true" data-can-select-product="false">' +
    '<div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse"><form id="filterForm">' +
    '<select id="filterGlobalProduct"></select>' +
    '<input type="hidden" id="filterGlobalProductText" /><input type="hidden" id="filterGlobalProductId" />' +
    '<button type="button" id="btnFilterApply"></button><button type="button" id="btnFilterReset"></button></form></div></div>' +
    '<div id="abbListTruncated" class="alert d-none"></div>' +
    '<div class="card"><div class="card-datatable"><table id="dt-product-abbreviation-register">' +
    "<thead><tr><th></th><th>Product</th><th>Code</th><th>State</th><th>Version</th><th>Retiring</th><th>Actions</th></tr></thead>" +
    "<tbody></tbody></table></div></div></div>";
}

/**
 * One page load. `views` = the saved-view store by page key; `answer(url)` = the list proxy's answer.
 * Returns the recorded list requests, the store's writes and the toasts.
 */
function loadPage({ search = "", views = {}, saveFails = false, answer } = {}) {
  pageDom();
  window.history.replaceState(null, "", "/MDM/ProductAbbreviationRegister" + search);
  const requests = [];
  const saves = [];
  const toasts = [];
  const ctx = vm.createContext({
    window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
    setInterval, clearInterval, requestAnimationFrame: (fn) => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
    URLSearchParams, fetch: () => Promise.reject(new Error("no fetch in this test")),
    bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) }, Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) } }
  });
  Object.getOwnPropertyNames(window).filter((k) => /^[A-Z]/.test(k) && typeof window[k] === "function" && !(k in ctx)).forEach((k) => { ctx[k] = window[k]; });
  ctx.self = ctx;
  const run = (rel) => vm.runInContext(read(...rel.split("/")), ctx, { filename: rel });
  run("wwwroot/assets/vendor/libs/jquery/jquery.js");
  ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
  run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
  ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
  run("wwwroot/assets/js/diten-datatable.js");

  window.DtDefaults = { create: (cfg) => cfg, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {}, handleUnauthorized() {} };
  window.L10n = { ErrorForbidden: "fixture: forbidden sentence", ErrorGateway: "fixture: unavailable sentence", SavedViewNotCarried: "fixture: view not carried" };
  window.showToast = (message, type) => toasts.push([message, type]);
  window.personalizationClient = {
    getViews: async (moduleKey, pageKey) => (views[pageKey] || []).map((v) => ({ ...v })),
    saveView: async (payload) => {
      saves.push(JSON.parse(JSON.stringify(payload)));
      if (saveFails) throw new Error("fixture: store unavailable");
      views[payload.pageKey] = [{ id: "v-new", isDefault: true, viewDefinition: payload.viewDefinition }];
      return payload;
    },
    updateView: async (id, payload) => payload
  };

  ctx.jQuery.ajaxTransport("+*", (options) => ({
    send(_headers, complete) {
      requests.push(decodeURIComponent(options.url));
      const [status, body] = (answer || (() => [200, { isSuccessful: true, data: { items: [], total: 0, filteredTotal: 0, hasOpenEntry: false, truncated: false } }]))(options.url);
      setTimeout(() => complete(status, status === 200 ? "OK" : "Refused", { text: JSON.stringify(body) }, "Content-Type: application/json"), 0);
    },
    abort() {}
  }));

  vm.runInContext(read("wwwroot", "assets", "js", "MDM", "ProductAbbreviationRegister", "index.js")
    .replace("document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());", "")
    + "\nglobalThis.__abbPage = ProductAbbreviationRegisterList;", ctx, { filename: "index.js" });
  const ready = ctx.__abbPage.init();
  return { ctx, ready, requests, saves, toasts, views };
}

const savedView = (filters) => ({
  id: "v-saved", isDefault: true,
  viewDefinition: { filters, search: "", colVis: { 1: true, 2: true, 3: true, 4: true, 5: true }, columnOrder: [0, 1, 2, 3, 4, 5, 6], order: [[2, "asc"]] }
});

describe("ABB register on the real list component", () => {
  afterEach(() => { document.body.innerHTML = ""; });

  test("item 1 — a Task Center link is the product the list asks for, over a saved view of another product", async () => {
    const page = loadPage({
      search: "?globalProductId=" + LINKED,
      views: { "ProductAbbreviationRegister.v2": [savedView({ globalProductId: SAVED, globalProductText: "Saved product" })] }
    });
    await page.ready;

    await until(() => page.requests.some((url) => url.includes("globalProductId=" + LINKED)));
    await settle();

    const last = page.requests.at(-1);
    expect(last, "the last list request is the linked product's").toContain("globalProductId=" + LINKED);
    expect(document.getElementById("filterGlobalProductId").value).toBe(LINKED);
    // The factory's own start asks for the saved view (its first request and applyState's redraw); the link comes AFTER
    // every one of those, and nothing asks for the saved product again.
    const lastSaved = page.requests.map((url) => url.includes("globalProductId=" + SAVED)).lastIndexOf(true);
    const firstLinked = page.requests.findIndex((url) => url.includes("globalProductId=" + LINKED));
    expect(lastSaved, "every saved-product request precedes the linked one").toBeLessThan(firstLinked);
  });

  test("item 1 — without a link the saved view's product stays the list's", async () => {
    const page = loadPage({ views: { "ProductAbbreviationRegister.v2": [savedView({ globalProductId: SAVED, globalProductText: "Saved product" })] } });
    await page.ready;
    await until(() => page.requests.length >= 1);
    await settle();

    expect(page.requests.at(-1)).toContain("globalProductId=" + SAVED);
    expect(page.requests.every((url) => !url.includes(LINKED))).toBe(true);
  });

  test("item 7 — the product's display name never travels on the list request", async () => {
    const page = loadPage({ views: { "ProductAbbreviationRegister.v2": [savedView({ globalProductId: SAVED, globalProductText: "Saved product" })] } });
    await page.ready;
    await until(() => page.requests.length >= 1);
    await settle();

    expect(page.requests.length).toBeGreaterThan(0);
    page.requests.forEach((url) => {
      expect(url).not.toMatch(/globalProductText/);
      expect(url).toMatch(/^\/MDM\/ProductAbbreviationRegister\/api\/list\?start=0&length=10&/);
    });
  });

  test("item 2 — a refused list read speaks the register's own sentence", async () => {
    const page = loadPage({
      views: { "ProductAbbreviationRegister.v2": [savedView({ globalProductId: SAVED, globalProductText: "P" })] },
      answer: () => [403, { success: false, errors: ["denied"], errorCode: "ABBREVIATION_PERMISSION_DENIED" }]
    });
    await page.ready;
    await until(() => page.toasts.length >= 1);

    expect(page.toasts[0]).toEqual(["fixture: forbidden sentence", "error"]);
    expect(page.toasts.flat().join(" ")).not.toMatch(/Permission denied|DataTables Ajax error/);
  });

  test("item 3 — an old eight-column default view is carried over with its columns mapped, once", async () => {
    const legacy = {
      id: "v-old", isDefault: true, viewName: "Mine",
      viewDefinition: JSON.stringify({
        filters: { globalProductId: SAVED, globalProductText: "Saved product" }, search: "ab",
        // old: 0 control · 1 selection · 2 product · 3 code · 4 lifecycle · 5 version · 6 retirement · 7 actions
        colVis: [true, true, false, true, true], columnOrder: [0, 1, 2, 4, 3, 5, 6, 7], order: [[3, "desc"]]
      })
    };
    const page = loadPage({ views: { ProductAbbreviationRegister: [legacy] } });
    await page.ready;
    await until(() => page.requests.length >= 1);

    expect(page.saves).toHaveLength(1);
    const carried = page.saves[0];
    expect(carried.pageKey).toBe("ProductAbbreviationRegister.v2");
    expect(carried.viewName).toBe("Mine");
    expect(carried.viewDefinition).toEqual({
      filters: { globalProductId: SAVED, globalProductText: "Saved product" }, search: "ab",
      colVis: { 1: true, 2: true, 3: false, 4: true, 5: true },
      columnOrder: [0, 1, 3, 2, 4, 5, 6],
      order: [[2, "desc"]]
    });
    // The list opens on the CARRIED view: sorted by the code, descending — not by the lifecycle the old index 3 now names.
    expect(page.requests[0]).toMatch(/&search=ab&orderBy=abbreviation&orderDir=desc&/);

    // The next load finds the versioned view and writes nothing.
    const again = loadPage({ views: page.views });
    await again.ready;
    await until(() => again.requests.length >= 1);
    expect(again.saves).toHaveLength(0);
  });

  test("item 3 — a view the store will not take opens the list on its baseline, never on shifted columns, and says so", async () => {
    const legacy = { id: "v-old", isDefault: true, viewDefinition: { colVis: [false, false, false, false, false], columnOrder: [0, 1, 2, 3, 4, 5, 6, 7], order: [[4, "asc"]] } };
    const page = loadPage({ views: { ProductAbbreviationRegister: [legacy] }, saveFails: true });
    await page.ready;
    await until(() => page.requests.length >= 1);

    expect(page.toasts).toContainEqual(["fixture: view not carried", "warning"]);
    expect(page.requests[0]).toMatch(/&orderBy=abbreviation&orderDir=asc&/);
  });
});
