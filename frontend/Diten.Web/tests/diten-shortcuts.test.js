const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UI-SHORTCUTS-01 (BL-438) — the shared keyboard-shortcut layer, measured through the REAL file
 * (assets/js/shared/diten-shortcuts.js) loaded into jsdom exactly as the host views load it.
 *
 * The layer listens on `document` for every page that includes it. A key it takes by mistake is a letter
 * somebody meant to type — data loss — so the silence rule is pinned field by field and overlay by overlay, each
 * against a control press that proves the same key DOES fire when nothing silences it.
 */
const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");
const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const resxStrings = (locale) => {
  const xml = read("Resources", `SharedResource.${locale}.resx`);
  const out = {};
  for (const m of xml.matchAll(/<data name="(Shortcuts\.[^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    out[m[1]] = m[2];
  }
  return out;
};

/** A fresh layer: the previous instance's listener is removed and its registrations dropped. */
const freshLayer = (strings = resxStrings("en")) => {
  if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
  delete window.DitenShortcuts;
  document.body.innerHTML = `<script id="diten-shortcuts-l10n" type="application/json">${JSON.stringify(strings)}</script>`
    + `<button type="button" id="plain">plain</button>`;
  delete window.Swal;
  delete window.DitenDialogAppearance;
  loadScript("wwwroot/assets/js/shared/diten-shortcuts.js");
  return window.DitenShortcuts;
};

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const press = (key, target = document.getElementById("plain"), extra = {}) => {
  const event = new window.KeyboardEvent("keydown", Object.assign({ key, bubbles: true, cancelable: true }, extra));
  target.dispatchEvent(event);
  return event;
};

/** A SweetAlert stand-in that records what the layer asked it to show. */
const stubSwal = () => {
  const fired = [];
  window.Swal = { fire: (config) => { fired.push(config); return Promise.resolve({}); }, isVisible: () => false };
  window.DitenDialogAppearance = Object.assign((options) => ({ width: options && options.width, customClass: { popup: "p" } }),
    { iconHtml: () => "<i></i>", description: "note-class" });
  return fired;
};

afterAll(() => {
  if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
  delete window.DitenShortcuts;
});

describe("the silence rule — no shortcut eats a key meant for something else", () => {
  let layer;
  let calls;
  beforeEach(() => {
    layer = freshLayer();
    calls = 0;
    layer.register("test", [{ keys: ["j"], actionKey: "Wcn.Next", handler: () => { calls += 1; } }]);
  });

  it("fires on a plain element — the control every silence below is measured against", () => {
    press("j");
    expect(calls).toBe(1);
  });

  it.each([
    ["input", "<input type=\"text\" id=\"f\">"],
    ["textarea", "<textarea id=\"f\"></textarea>"],
    ["select", "<select id=\"f\"><option>1</option></select>"],
    ["contenteditable", "<div contenteditable=\"true\" id=\"f\"><span id=\"inner\">x</span></div>"]
  ])("stays silent while focus is in a %s", (_name, markup) => {
    document.body.insertAdjacentHTML("beforeend", markup);
    press("j", document.getElementById("f"));
    expect(calls).toBe(0);
    // Inside the editable region, not only on its root.
    const inner = document.getElementById("inner");
    if (inner) { press("j", inner); expect(calls).toBe(0); }
    // And the key was not swallowed: it still reaches the field as a character.
    expect(press("j", document.getElementById("f")).defaultPrevented).toBe(false);
  });

  it.each([
    ["an open Bootstrap modal", "<div class=\"modal show\"></div>"],
    ["an open offcanvas", "<div class=\"offcanvas show\"></div>"],
    ["an offcanvas still opening", "<div class=\"offcanvas showing\"></div>"],
    ["a SweetAlert on screen", "<div class=\"swal2-container\"></div>"]
  ])("stays silent while there is %s", (_name, markup) => {
    document.body.insertAdjacentHTML("beforeend", markup);
    press("j");
    expect(calls).toBe(0);
  });

  it("stays silent while SweetAlert reports itself visible", () => {
    window.Swal = { isVisible: () => true, fire: () => Promise.resolve({}) };
    press("j");
    expect(calls).toBe(0);
  });

  it("an overlay that is CLOSED does not silence anything", () => {
    document.body.insertAdjacentHTML("beforeend", "<div class=\"modal\"></div><div class=\"offcanvas\"></div>");
    press("j");
    expect(calls).toBe(1);
  });

  it.each(["ctrlKey", "metaKey", "altKey"])("stays silent with %s held — that chord is the browser's", (modifier) => {
    press("j", undefined, { [modifier]: true });
    expect(calls).toBe(0);
  });

  it("Shift does not silence, and a letter matches either case (as WorkCenterNext always did)", () => {
    press("J", undefined, { shiftKey: true });
    expect(calls).toBe(1);
  });
});

describe("`?` opens the list from anywhere except a form field", () => {
  it("opens over a modal and with a modifier held, but types into a textarea", () => {
    const layer = freshLayer();
    const fired = stubSwal();
    layer.register("test", [{ keys: ["j"], actionKey: "Wcn.Next", handler: () => {} }]);

    expect(press("?").defaultPrevented).toBe(true);
    expect(fired.length).toBe(1);

    document.body.insertAdjacentHTML("beforeend", "<div class=\"modal show\"></div>");
    press("?");
    press("?", undefined, { ctrlKey: true });
    expect(fired.length).toBe(3);

    document.body.insertAdjacentHTML("beforeend", "<textarea id=\"f\"></textarea>");
    expect(press("?", document.getElementById("f")).defaultPrevented).toBe(false);
    expect(fired.length).toBe(3);
  });

  it("is reserved — a screen cannot bind `?` to something else", () => {
    const layer = freshLayer();
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(layer.register("test", [{ keys: ["?"], actionKey: "Wcn.Next", handler: () => {} }])).toBe(0);
    expect(error).toHaveBeenCalled();
  });
});

describe("a conflict is loud and the first registration keeps its key", () => {
  it("refuses the second entry for the same key in the same scope", () => {
    const layer = freshLayer();
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    const hits = [];
    const accepted = layer.register("test", [
      { keys: ["j"], actionKey: "Wcn.Next", handler: () => hits.push("first") },
      { keys: ["x", "J"], actionKey: "Wcn.Previous", handler: () => hits.push("second") }
    ]);
    expect(accepted).toBe(1);
    expect(error.mock.calls.map((c) => c[0]).join(" ")).toMatch(/key "j" is already bound to "Wcn.Next"; refused "Wcn.Previous"/);

    press("j");
    press("x");   // the refused entry is refused WHOLE — none of its keys were bound
    expect(hits).toEqual(["first"]);
    const scope = layer.list().find((group) => group.scope === "test");
    expect(scope.entries.map((e) => e.actionKey)).toEqual(["Wcn.Next"]);
  });

  it("refuses a clash across two register calls for the same scope too", () => {
    const layer = freshLayer();
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    layer.register("test", [{ keys: ["e"], actionKey: "Task.Edit", handler: () => {} }]);
    expect(layer.register("test", [{ keys: ["e"], actionKey: "Task.Back", handler: () => {} }])).toBe(0);
    expect(error).toHaveBeenCalled();
  });

  it("the same key in two DIFFERENT scopes is allowed; the newest answers, and may pass it on", () => {
    const layer = freshLayer();
    const hits = [];
    let declines = false;
    layer.register("older", [{ keys: ["e"], actionKey: "Task.Edit", handler: () => hits.push("older") }]);
    layer.register("newer", [{ keys: ["e"], actionKey: "Task.Edit", handler: () => (declines ? false : hits.push("newer")) }]);
    press("e");
    declines = true;
    press("e");
    expect(hits).toEqual(["newer", "older"]);
  });

  it("unregister removes a scope's keys and its rows", () => {
    const layer = freshLayer();
    let calls = 0;
    layer.register("test", [{ keys: ["j"], actionKey: "Wcn.Next", handler: () => { calls += 1; } }]);
    layer.unregister("test");
    press("j");
    expect(calls).toBe(0);
    expect(layer.list().map((g) => g.scope)).toEqual(["global"]);
  });
});

describe("the `?` list prints every registered shortcut, scope by scope", () => {
  it("has one row per entry plus the list's own, with the translated descriptions", () => {
    const tr = resxStrings("tr");
    const layer = freshLayer(tr);
    const fired = stubSwal();
    const entries = [
      { keys: ["j"], actionKey: "Wcn.Next", handler: () => {} },
      { keys: ["k"], actionKey: "Wcn.Previous", handler: () => {} },
      { keys: ["Enter", "o"], actionKey: "Wcn.Open", handler: () => {} },
      { keys: [" "], actionKey: "Wcn.OpenFocused", handler: () => {} },
      { keys: ["Escape"], actionKey: "Wcn.ClearSelection", handler: () => {} }
    ];
    expect(layer.register("workcenter", entries, { titleKey: "Shortcuts.Scope.WorkCenter" })).toBe(entries.length);
    layer.register("task-details", [{ keys: ["e"], actionKey: "Task.Edit", handler: () => {} }],
      { titleKey: "Shortcuts.Scope.TaskDetails" });

    press("?");
    expect(fired.length).toBe(1);
    const host = document.createElement("div");
    host.innerHTML = fired[0].html;

    const rows = Array.from(host.querySelectorAll("[data-diten-shortcut]"));
    expect(rows.map((row) => row.getAttribute("data-diten-shortcut")))
      .toEqual(["ShowList", "Wcn.Next", "Wcn.Previous", "Wcn.Open", "Wcn.OpenFocused", "Wcn.ClearSelection", "Task.Edit"]);
    expect(host.querySelector('[data-diten-shortcut="Wcn.Next"] td').textContent).toBe(tr["Shortcuts.Wcn.Next"]);
    expect(host.querySelector('[data-diten-shortcut="Wcn.Open"] th').textContent).toBe("Enter/o");
    expect(host.querySelector('[data-diten-shortcut="Wcn.OpenFocused"] kbd').textContent).toBe(tr["Shortcuts.Key.Space"]);
    expect(Array.from(host.querySelectorAll(".diten-shortcuts-scope-title")).map((h) => h.textContent))
      .toEqual([tr["Shortcuts.Scope.Global"], tr["Shortcuts.Scope.WorkCenter"], tr["Shortcuts.Scope.TaskDetails"]]);
    expect(fired[0].title).toContain(tr["Shortcuts.DialogTitle"]);
    expect(fired[0].confirmButtonText).toBe(tr["Shortcuts.Close"]);
    expect(host.textContent).toContain(tr["Shortcuts.SilenceNote"]);
  });

  it("escapes what it prints", () => {
    const layer = freshLayer();
    layer.register("test", [{ keys: ["x"], actionKey: "Custom", description: "<img src=x onerror=alert(1)>", handler: () => {} }]);
    expect(layer.renderList()).not.toContain("<img");
  });
});

describe("the strings are SharedResource, complete in all seven languages", () => {
  /*
   * Every key the code can ask for, collected from the SOURCE — the layer's own `t('Shortcuts.…')` calls, and
   * every `actionKey` / `titleKey` a screen registers — so a new shortcut without its strings goes red here.
   */
  const SOURCES = [
    "wwwroot/assets/js/shared/diten-shortcuts.js",
    "wwwroot/assets/js/WorkCenterNext/app.js",
    "wwwroot/assets/js/Tasks/details-page.js"
  ];
  const usedKeys = () => {
    const keys = new Set();
    SOURCES.forEach((file) => {
      const src = read(file);
      for (const m of src.matchAll(/t\('(Shortcuts\.[A-Za-z.]+)'\)/g)) { keys.add(m[1]); }
      for (const m of src.matchAll(/actionKey: '([A-Za-z.]+)'/g)) { keys.add(`Shortcuts.${m[1]}`); }
      for (const m of src.matchAll(/titleKey: '([A-Za-z.]+)'/g)) { keys.add(m[1]); }
    });
    return [...keys].sort();
  };

  it("finds the keys it is about to check (non-vacuity)", () => {
    const keys = usedKeys();
    expect(keys).toContain("Shortcuts.Wcn.Next");
    expect(keys).toContain("Shortcuts.Task.Edit");
    expect(keys).toContain("Shortcuts.DialogTitle");
    expect(keys.length).toBeGreaterThanOrEqual(18);
  });

  it.each(LOCALES)("%s carries every used key, non-empty", (locale) => {
    const strings = resxStrings(locale);
    const missing = usedKeys().filter((key) => !strings[key] || !strings[key].trim());
    expect(missing, `${locale} is missing`).toEqual([]);
  });

  it("all seven carry the same Shortcuts.* key set", () => {
    const en = Object.keys(resxStrings("en")).sort();
    LOCALES.forEach((locale) => expect(Object.keys(resxStrings(locale)).sort(), locale).toEqual(en));
  });

  it("the partial emits the whole Shortcuts.* prefix from SharedResource, and loads the layer after it", () => {
    const partial = read("Views", "Shared", "_DitenShortcuts.cshtml");
    expect(partial).toContain("IHtmlLocalizer<Diten.Web.SharedResource>");
    expect(partial).toContain('StartsWith("Shortcuts."');
    expect(partial.indexOf('id="diten-shortcuts-l10n"')).toBeLessThan(partial.indexOf("diten-shortcuts.js"));
  });

  it("the WorkCenterNext resx is not where these live", () => {
    LOCALES.forEach((locale) => expect(
      read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${locale}.resx`)).not.toContain("Shortcuts."));
  });
});

describe("the layer runs with a browser's globals and nothing more, against the real SweetAlert2", () => {
  /*
   * The dt-defaults-runs-in-a-browser.test.js lesson: `global.document` passed every vitest run (Node has
   * `global`) and threw on every page (a browser does not). So the layer is loaded here in a FRESH vm context
   * carrying only what a page carries, and `?` opens the REAL vendor SweetAlert2 with the REAL product dialog
   * look (`DitenDialogAppearance`, taken from _GlobalConfirmation.cshtml, not re-typed here).
   */
  const vm = require("vm");
  const appearanceSource = () => {
    const view = read("Views", "Shared", "_GlobalConfirmation.cshtml");
    const start = view.indexOf("window.DitenDialogAppearance = function");
    const end = view.indexOf("window.showConfirm = function");
    expect(start).toBeGreaterThan(0);
    expect(end).toBeGreaterThan(start);
    const slice = view.slice(start, end);
    expect(slice).not.toMatch(/@[A-Za-z(]/);   // pure JS — no Razor to render first
    return slice;
  };

  beforeAll(() => {
    if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
    delete window.DitenShortcuts;
    document.body.innerHTML = `<script id="diten-shortcuts-l10n" type="application/json">${JSON.stringify(resxStrings("en"))}</script>`
      + `<button type="button" id="plain">plain</button>`;
    window.scrollTo = () => {};   // jsdom has no layout; SweetAlert2 restores the scroll position on close
    loadScript("wwwroot/assets/vendor/libs/sweetalert2/sweetalert2.js");
    vm.runInThisContext(appearanceSource());
    const source = read("wwwroot", "assets", "js", "shared", "diten-shortcuts.js");
    // NOT runInThisContext: a new context has no `global`, no `process`, no `require` — a page's world.
    vm.runInNewContext(source, { window, document, console }, { filename: "diten-shortcuts.js" });
  });

  afterAll(() => {
    if (window.Swal && window.Swal.close) { window.Swal.close(); }
    delete window.Swal;
    delete window.Sweetalert2;
    delete window.DitenDialogAppearance;
  });

  it("registers, dispatches, and opens the real dialog listing what is registered", async () => {
    let calls = 0;
    window.DitenShortcuts.register("workcenter", [{ keys: ["j"], actionKey: "Wcn.Next", handler: () => { calls += 1; } }],
      { titleKey: "Shortcuts.Scope.WorkCenter" });
    press("j");
    expect(calls).toBe(1);

    press("?");
    await tick();
    const popup = document.querySelector(".swal2-popup");
    expect(popup).not.toBeNull();
    expect(popup.querySelector('[data-diten-shortcut="Wcn.Next"]')).not.toBeNull();
    expect(popup.textContent).toContain(resxStrings("en")["Shortcuts.DialogTitle"]);

    // While the real dialog is up, the page's keys are silent.
    press("j");
    expect(calls).toBe(1);
  });
});
