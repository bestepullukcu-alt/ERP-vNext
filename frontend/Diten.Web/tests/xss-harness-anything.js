/*
 * ATT-FIX2 — a permissive stand-in for the page globals a DataTables list script touches while it boots (jQuery,
 * DtDefaults, the filter registry …): every property is a callable that returns itself, so the script runs to the
 * point that matters here — the column renderers it hands DitenDataTable.createCrudTable.
 */
const anything = new Proxy(function anythingFn() {}, {
  get: (target, key) => {
    if (key === Symbol.toPrimitive) { return () => ""; }
    if (key === "then") { return undefined; }   // never mistaken for a promise
    if (key === "length") { return 0; }
    return anything;
  },
  apply: () => anything
});

/** Boots `script` on a page with `tableClass`; answers the options its table was created with. */
const bootDataTableList = async (loadScript, script, tableClass) => {
  document.body.innerHTML = `<table class="${tableClass}"></table>`;
  let captured = null;
  global.$ = anything;
  global.jQuery = anything;
  global.DtDefaults = anything;
  global.L10n = {};
  global.DitenDataTable = new Proxy({}, {
    get: (target, key) => (key === "createCrudTable"
      ? (options) => { captured = options; return anything; }
      : anything)
  });
  loadScript(script);
  document.dispatchEvent(new Event("DOMContentLoaded"));
  for (let i = 0; i < 6; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
  return captured;
};

module.exports = { anything, bootDataTableList };
