const { loadScript } = require("./load-script");

/*
 * The shared WorkCenterNext boot harness (BL-033).
 *
 * app.js renders BOTH surfaces — the list and the full-page detail — from one bundle, choosing between them on
 * `root.dataset.wcnPage`. The two test harnesses therefore differ in exactly two things: the attributes on
 * #wcnApp, and how many items the projection answers with. Everything else (module load order, the network seam,
 * the TasksApi stub) is identical, so it lives here rather than being copied — a copied harness gets fixed on one
 * side and left stale on the other, which is the very drift this file exists to prevent.
 *
 * What is deliberately REAL: the module load order, the contract validation, mock-data's presentation mapping,
 * and app.js itself. The ONLY stub is the network, pinned at the `fetchWorkItems` seam. A harness that mocked
 * further in would stop proving anything about the code that actually ships.
 */
const SCRIPT_ROOT = "wwwroot/assets/js/WorkCenterNext/";

/**
 * Loads the WorkCenterNext modules in the order the host views use (Index.cshtml / Details.cshtml).
 *
 * `task-detail-resolver` is not optional even for the list: detailHtml bails to an "invalid" placeholder without
 * it, which would make every "this thing is absent" assertion pass for the wrong reason.
 */
const loadModules = () => {
  loadScript(SCRIPT_ROOT + "fixture-contract.js");
  loadScript(SCRIPT_ROOT + "task-detail-resolver.js");
  loadScript(SCRIPT_ROOT + "trigger-response-resolver.js");
  loadScript(SCRIPT_ROOT + "mock-data.js");
  loadScript(SCRIPT_ROOT + "work-items-api.js");
};

/**
 * Boots the real app.js against jsdom on one surface.
 *
 * @param {object}   config
 * @param {string}   config.rootAttrs          Extra attributes for #wcnApp — this is what selects the surface.
 * @param {object[]} config.items              Projection items the stubbed network answers with.
 * @param {boolean} [config.neverResolve]      Leave the fetch pending, so the loading state can be observed.
 * @param {boolean} [config.withoutTasksScripts] Reproduce a host view that forgot to load Tasks/api.js + form.js.
 * @returns {Promise<{created: object[], posted: object[]}>} What the write stubs recorded.
 */
const bootSurface = ({ rootAttrs = "", items = [], neverResolve = false, withoutTasksScripts = false } = {}) => {
  // A previous boot leaves its modules on `global`; app.js would then read the OLD data module and the new DOM.
  ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
    .forEach((key) => { delete global[key]; });

  /*
   * Reset the URL. app.js mirrors its state into the query string (syncUrl → history.replaceState) and reads it
   * back on boot (hydrateStateFromUrl), so without this a test that switched to "Mine" leaves `?tab=islerim`
   * behind and the NEXT test boots onto a different tab than it asked for. jsdom keeps one location per file, so
   * that leak is invisible when a test is run alone and only appears in a full run — which is the worst kind.
   */
  if (global.history && global.history.replaceState) {
    global.history.replaceState(null, "", "/WorkCenterNext");
  }

  // t/tf/tn echo the key back, so an assertion naming a resource key is asserting the key the code chose —
  // not a translation that could drift independently.
  global.WCN = { t: (key) => key, tf: (key) => key, tn: (key) => key };
  document.body.innerHTML = `<div id="wcnApp" class="wcn-app" ${rootAttrs} data-wcn-fixtures=""></div>`;

  loadModules();

  const mapped = global.WorkCenterNextApi.mapPayload(items);
  // The fixtures these harnesses hand in must satisfy the executable contract, or the test is describing an item
  // no provider could ever send.
  expect(mapped.errors).toEqual([]);
  // The network, and ONLY the network, is stubbed — at the module seam. Everything downstream is real code.
  global.WorkCenterNextApi.fetchWorkItems = neverResolve
    ? () => new Promise(() => { /* a request that never settles — the page must stay in its loading state */ })
    : () => Promise.resolve({ status: "ok", httpStatus: 200, items: mapped.items, errors: [] });

  const created = [];
  const posted = [];

  if (withoutTasksScripts) {
    delete global.TasksApi;
    delete global.TaskForm;
    loadScript(SCRIPT_ROOT + "app.js");
    return new Promise((resolve) => setTimeout(() => resolve({ created, posted }), 0));
  }

  global.TasksApi = {
    create: (payload) => { created.push(payload); return Promise.resolve({ ok: true, status: 201, data: { id: "new" } }); },
    get: () => Promise.resolve({ ok: true, status: 200, data: {} }),
    transition: () => Promise.resolve({ ok: true, status: 204 }),
    addComment: (taskId, payload) => { posted.push({ taskId, payload }); return Promise.resolve({ ok: true, status: 201, data: { id: "c1" } }); },
    // Individual tests override this to assert the exact call, or to simulate a refusal.
    plan: () => Promise.resolve({ ok: true, status: 204 }),
    isConcurrencyConflict: () => false,
    isTransitionBlocked: () => false,
    failureMessage: () => "error"
  };
  global.TaskForm = { buildCreatePayload: (draft) => Object.assign({}, draft) };

  loadScript(SCRIPT_ROOT + "app.js");
  // boot() is async (it awaits loadWorkItems); let its microtasks drain before anyone asserts on the DOM.
  return new Promise((resolve) => setTimeout(() => resolve({ created, posted }), 0));
};

const app = () => document.getElementById("wcnApp");

module.exports = { SCRIPT_ROOT, bootSurface, app };
