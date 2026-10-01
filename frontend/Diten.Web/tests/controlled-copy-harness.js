const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-452 package 2 — the browser chain the controlled-copy tests run through: the vendored jQuery, the vendored
 * DataTables bundle (DataTables 2.1.8 + Buttons 3.2.0 + pdfmake 0.2.15 + its Roboto vfs), the tenant shell's colVis and
 * ColReorder files, the REAL dt-defaults.js and diten-datatable.js, and a REAL golden page. Only the network is fake
 * (the list through a jQuery transport, the export and lookups through `fetch`) and the print window (an iframe, the
 * document a real `window.open('')` hands back).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const until = async (predicate, ms = 4000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (predicate()) return; await new Promise((r) => setTimeout(r, 10)); }
  throw new Error("timed out");
};

// A Razor partial as the browser receives it: comments and directives gone, each localizer call → its word.
const razor = (text, words = {}) => text
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^@(using|inject)[^\n]*\n/gm, "")
  .replace(/@(?:Shared)?Localizer\["(\w+)"\]/g, (_, key) => words[key] || key);

const resxValue = (lang, key) => {
  const xml = read("Resources", `SharedResource.${lang}.resx`);
  const m = new RegExp(`<data name="${key.replace(/\./g, "\\.")}"[^>]*>\\s*<value>([^<]*)</value>`).exec(xml);
  return m ? m[1] : undefined;
};

// The tenant shell's bridge keys (bridge name → SharedResource key), parsed from _LayoutTenantShell.cshtml itself.
const bridgeEntries = () => {
  const layout = read("Views", "Shared", "_LayoutTenantShell.cshtml");
  return [...layout.matchAll(/\["(\w+)"\] = SharedLocalizer\["([\w.]+)"\]\.Value/g)].map((m) => ({ bridge: m[1], resx: m[2] }));
};

// window.L10n as the tenant shell would render it in `lang` — built from the layout's bridge and the resx, not a copy.
const bridgeL10n = (lang) => Object.fromEntries(bridgeEntries().map(({ bridge, resx }) => [bridge, resxValue(lang, resx)]));

function bootBrowser({ network }) {
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

  // The bundle publishes pdfmake on the global it ran in (the vm context); the page reads window.pdfMake.
  const pdfMake = ctx.pdfMake || window.pdfMake;
  window.pdfMake = pdfMake;
  const pdfs = [];
  pdfMake.createPdf = (docDefinition) => {
    const entry = { docDefinition, downloadedAs: null };
    pdfs.push(entry);
    return { download: (name) => { entry.downloadedAs = name; } };
  };

  const listRequests = [];
  ctx.jQuery.ajaxTransport("+*", (options) => ({
    send(_headers, complete) {
      listRequests.push(options.url);
      const body = JSON.stringify(network.list(options.url));
      setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
    },
    abort() {}
  }));

  const fetches = [];
  ctx.fetch = async (url, init) => {
    fetches.push({ url: String(url), init: init || {} });
    return network.fetch(String(url), init || {});
  };
  window.fetch = ctx.fetch;

  // The print window: a real document in an iframe, what window.open('') gives a page.
  const opened = [];
  window.open = () => {
    const frame = document.createElement("iframe");
    document.body.appendChild(frame);
    const win = frame.contentWindow;
    win.print = () => { win.__printed = true; };
    win.focus = () => {};
    win.close = () => { win.__closed = true; };
    opened.push(win);
    return win;
  };

  const toasts = [];
  window.showToast = (key, type) => toasts.push([key, type]);
  window.URL.createObjectURL = () => "blob:export";
  window.URL.revokeObjectURL = () => {};

  return { ctx, run, pdfs, listRequests, fetches, opened, toasts };
}

/*
 * Renders a pdfmake doc-definition with the REAL vendored pdfmake + Roboto vfs, to prove it is a valid document.
 * ⚠ Not in the jsdom realm: pdfmake's font parser never returns there (a one-word document hung the worker, measured
 * 2026-09-25 — jsdom's typed arrays). The same vendored bundle is evaluated in a realm with Node's own typed arrays;
 * its pdfmake module initialises before the DataTables part (which needs a real jQuery) stops.
 */
let nodePdfMake = null;
const loadNodePdfMake = () => {
  if (nodePdfMake) return nodePdfMake;
  // Only host FUNCTIONS go in: the realm's Promise and typed arrays are its own intrinsics. Handing it the host Promise
  // let the bundle's polyfill patch vitest's Promise (measured: every later test died on `.finally is not a function`).
  const realm = vm.createContext({ console, setTimeout, clearTimeout, setImmediate, queueMicrotask, navigator: { userAgent: "node" } });
  realm.self = realm; realm.window = realm; realm.global = realm;
  realm.jQuery = function () {}; realm.jQuery.fn = {};
  realm.document = { createElement: () => ({}), addEventListener() {} };
  try { vm.runInContext(read("wwwroot", "assets", "vendor", "libs", "datatables-bs5", "datatables-bootstrap5.js"), realm); } catch (e) { /* the DataTables part needs jQuery.extend */ }
  if (!realm.pdfMake) throw new Error("the vendored bundle did not publish pdfMake");
  nodePdfMake = realm.pdfMake;
  return nodePdfMake;
};
const renderPdf = (docDefinition) => new Promise((resolve, reject) => {
  const timer = setTimeout(() => reject(new Error("pdfmake gave no buffer in 10s")), 10000);
  try {
    loadNodePdfMake().createPdf(docDefinition).getBuffer((buffer) => { clearTimeout(timer); resolve(Buffer.from(buffer.buffer ? new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength) : buffer)); });
  } catch (e) { clearTimeout(timer); reject(e); }
});

module.exports = { read, web, until, razor, resxValue, bridgeEntries, bridgeL10n, bootBrowser, renderPdf };
