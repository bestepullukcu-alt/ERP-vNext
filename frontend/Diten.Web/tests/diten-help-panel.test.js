const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UX-COMMON-01 (BL-495, BL-438) — the "?" help panel and the shared keyboard layer on every shell page.
 *
 *   · both shells (tenant, platform admin) load _DitenShortcuts and the help panel ONCE, after _GlobalConfirmation and
 *     before the page's own scripts; the help button is the first item of the top bar;
 *   · the pages that used to load the layer themselves no longer load it a second time;
 *   · diten-help.js fills the page's registered shortcuts as TEXT, shows no shortcut section when there are none,
 *     hides "Start the tour" when no tour can run, and `?` opens the panel instead of the bare list.
 */

const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");
const SHELLS = [
  ["tenant shell", read("Views", "Shared", "_LayoutTenantShell.cshtml")],
  ["platform admin shell", read("Views", "Shared", "_LayoutPlatformAdmin.cshtml")]
];
const SHORTCUTS_PARTIAL = '<partial name="~/Views/Shared/_DitenShortcuts.cshtml" />';
const count = (text, needle) => text.split(needle).length - 1;

describe("both shells load the keyboard layer and the help panel once", () => {
  it.each(SHELLS)("%s: once each, after _GlobalConfirmation, before the page's scripts", (_name, shell) => {
    expect(count(shell, SHORTCUTS_PARTIAL)).toBe(1);
    expect(count(shell, '<partial name="~/Views/Shared/_PageHelpPanel.cshtml"')).toBe(1);
    expect(count(shell, '<partial name="~/Views/Shared/_PageHelpButton.cshtml"')).toBe(1);

    const confirmation = shell.indexOf("_GlobalConfirmation.cshtml");
    const layer = shell.indexOf(SHORTCUTS_PARTIAL);
    const panel = shell.indexOf("_PageHelpPanel.cshtml");
    const scripts = shell.indexOf('RenderSectionAsync("Scripts"');
    expect(confirmation).toBeGreaterThan(-1);
    expect(layer).toBeGreaterThan(confirmation);
    expect(panel).toBeGreaterThan(layer);
    expect(scripts).toBeGreaterThan(panel);
  });

  it.each(SHELLS)("%s: the help button is the first item of the top bar, before the language menu", (_name, shell) => {
    const button = shell.indexOf("_PageHelpButton.cshtml");
    const language = shell.indexOf("<!-- Language -->");
    expect(button).toBeGreaterThan(-1);
    expect(language).toBeGreaterThan(button);
    expect(shell.slice(button - 400, button)).toMatch(/<ul class="navbar-nav flex-row/);
  });

  it("the FROZEN _Layout is untouched by this package", () => {
    const frozen = read("Views", "Shared", "_Layout.cshtml");
    expect(frozen).not.toContain("_PageHelp");
    expect(frozen).not.toContain(SHORTCUTS_PARTIAL);
  });

  it.each(["Tasks/Create", "Tasks/Edit", "Tasks/Details", "WorkCenterNext/Index", "WorkCenterNext/Details"])(
    "%s does not load the layer a second time", (view) => {
      expect(read("Views", ...`${view}.cshtml`.split("/"))).not.toContain(SHORTCUTS_PARTIAL);
    });

  it("the help partials draw nothing on the unauthorized face", () => {
    ["_PageHelpButton.cshtml", "_PageHelpPanel.cshtml"].forEach((file) => {
      expect(read("Views", "Shared", file), file).toContain("CurrentPage.IsAccessDenied(Context)");
    });
    expect(read("Views", "Shared", "_AccessDenied.cshtml")).toContain("CurrentPage.MarkAccessDenied(Context)");
  });
});

// The markup _PageHelpPanel.cshtml renders, as the browser receives it (WP-UX-HELP-PANEL-02: tabs, cards, footer;
// PageHelpShellTests measures the real render).
const PANEL = `
  <button type="button" id="diten-help-toggle" data-diten-help-open>?</button>
  <div class="offcanvas offcanvas-end diten-help-panel" id="diten-help-panel" data-page-code="USERS">
    <div class="offcanvas-body">
      <ul class="nav diten-help-tabs" role="tablist" data-diten-help-tabs>
        <li class="nav-item"><button type="button" class="nav-link active" role="tab" data-diten-help-tab="overview">O</button></li>
        <li class="nav-item"><button type="button" class="nav-link" role="tab" data-diten-help-tab="steps">S</button></li>
        <li class="nav-item"><button type="button" class="nav-link" role="tab" data-diten-help-tab="keys">K</button></li>
      </ul>
      <div class="tab-content">
        <div class="tab-pane" id="diten-help-pane-keys" data-diten-help-keys>
          <section data-diten-help-everywhere></section>
          <section data-diten-help-shortcuts hidden><div data-diten-help-shortcut-list></div></section>
        </div>
      </div>
    </div>
    <div class="diten-help-footer">
      <div data-diten-help-tour hidden><button type="button" data-diten-help-start-tour>Start</button></div>
    </div>
  </div>`;

describe("diten-help.js", () => {
  let shown;
  let tabsShown;

  const boot = () => {
    if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
    delete window.DitenShortcuts;
    delete window.DitenHelp;
    delete window.DitenTour;
    document.body.innerHTML = PANEL;
    shown = [];
    tabsShown = [];
    window.bootstrap = {
      Offcanvas: { getOrCreateInstance: (el) => ({ show: () => shown.push(el.id), hide: () => {} }) },
      Tab: { getOrCreateInstance: (el) => ({ show: () => tabsShown.push(el.getAttribute("data-diten-help-tab")) }) }
    };
    loadScript("wwwroot/assets/js/shared/diten-shortcuts.js");
    loadScript("wwwroot/assets/js/shared/diten-tour.js");
    loadScript("wwwroot/assets/js/shared/diten-help.js");
  };

  afterEach(() => {
    delete window.bootstrap;
    document.body.innerHTML = "";
  });

  it("the top-bar button opens the panel", () => {
    boot();
    document.getElementById("diten-help-toggle").click();
    expect(shown).toEqual(["diten-help-panel"]);
    expect(window.DitenHelp.pageCode()).toBe("USERS");
  });

  it("a page with no registered shortcuts gets no shortcut section", () => {
    boot();
    window.DitenHelp.open();
    expect(document.querySelector("[data-diten-help-shortcuts]").hidden).toBe(true);
  });

  it("the page's registered shortcuts are listed — as text, never markup — and the global `?` row is not the page's", () => {
    boot();
    window.DitenShortcuts.register("users", [
      { keys: "n", actionKey: "Users.New", description: "<b>New</b> user", handler: () => {} }
    ]);
    window.DitenHelp.open();

    const section = document.querySelector("[data-diten-help-shortcuts]");
    expect(section.hidden).toBe(false);
    expect(section.querySelectorAll("[data-diten-help-scope]")).toHaveLength(1);
    expect(section.querySelector("kbd").textContent).toBe("n");
    expect(section.querySelector("td").textContent).toBe("<b>New</b> user");
    expect(section.querySelector("td b")).toBeNull();
  });

  it("`?` opens the help panel (it carries the list) instead of the bare list dialog", () => {
    boot();
    const fire = vi.fn();
    window.Swal = { fire, isVisible: () => false };
    document.body.dispatchEvent(new KeyboardEvent("keydown", { key: "?", bubbles: true, cancelable: true }));
    expect(shown).toEqual(["diten-help-panel"]);
    expect(fire).not.toHaveBeenCalled();
    delete window.Swal;
  });

  it("without the tour library, 'Start the tour' stays hidden and nothing throws", () => {
    boot();
    document.body.insertAdjacentHTML("beforeend", '<div data-tour="users-list"></div>');
    window.DitenTour.register("USERS", { version: 1, steps: [{ target: "users-list", titleKey: "a", textKey: "b" }] });
    const errors = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(window.DitenHelp.open()).toBe(true);
    expect(document.querySelector("[data-diten-help-tour]").hidden).toBe(true);
    expect(window.DitenHelp.startTour()).toBe(false);
    expect(errors).not.toHaveBeenCalled();
    errors.mockRestore();
  });

  // WP-UX-HELP-PANEL-02 — the tab is not remembered: every opening, by button or `?`, starts on "Overview". (The old
  // grey Enter hint is gone: the Keyboard tab's "On every page" card carries Enter and Ctrl+Enter on every page.)
  it("every opening starts on the Overview tab", () => {
    // (opened by open() and by `?`; the button's click listener is document-wide and piles up across boots here)
    boot();
    window.DitenHelp.open();
    document.body.dispatchEvent(new KeyboardEvent("keydown", { key: "?", bubbles: true, cancelable: true }));
    expect(shown).toEqual(["diten-help-panel", "diten-help-panel"]);
    expect(tabsShown).toEqual(["overview", "overview"]);
  });

  it("the 'On this page' card sits in the Keyboard tab beside 'On every page'", () => {
    boot();
    const keys = document.querySelector("[data-diten-help-keys]");
    expect(keys.querySelector("[data-diten-help-everywhere]")).not.toBeNull();
    expect(keys.querySelector("[data-diten-help-shortcuts]")).not.toBeNull();
  });

  it("`?` over an open edit panel does not stack the help panel on it", () => {
    boot();
    document.body.insertAdjacentHTML("beforeend", '<div class="offcanvas offcanvas-end show" id="offcanvasCreateEdit"></div>');
    document.body.dispatchEvent(new KeyboardEvent("keydown", { key: "?", bubbles: true, cancelable: true }));
    expect(shown).toEqual([]);
  });

  it("with no panel on the page (unauthorized face), open() answers false and `?` falls back to the list", () => {
    boot();
    document.getElementById("diten-help-panel").remove();
    expect(window.DitenHelp.open()).toBe(false);
  });
});
