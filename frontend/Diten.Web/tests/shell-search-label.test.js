const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * BL-520 — the top-bar search box: its label in the reader's language, its shortcut by the name the reader's machine
 * uses. The functions are taken out of main.js itself (the shipped file), not copied; and the palette itself is built
 * by running the whole shipped main.js against a stub `autocomplete`, so what is asserted is the placeholder the
 * palette is actually given.
 */
const web = (...p) => path.join(__dirname, "..", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const MAIN = read("wwwroot", "assets", "js", "main.js");

const helpers = () => {
  const start = MAIN.indexOf("function searchShortcutName");
  const end = MAIN.indexOf("let searchData", start);
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  return Function(`${MAIN.slice(start, end)}; return { searchPlaceholder, searchShortcutName };`)();
};
const root = (placeholder) => ({ dataset: placeholder === undefined ? {} : { searchPlaceholder: placeholder } });

/** Runs the shipped main.js in a fresh browser-shaped context and returns the config the palette was built with. */
const runMain = async ({ template, nav }) => {
  document.documentElement.setAttribute("data-shell", "tenant");
  if (template === undefined) document.documentElement.removeAttribute("data-search-placeholder");
  else document.documentElement.setAttribute("data-search-placeholder", template);
  document.body.innerHTML = '<span id="autocomplete"></span>';
  const noop = () => undefined;
  window.Helpers = new Proxy({}, { get: () => noop });
  window.matchMedia = () => ({ matches: false, addEventListener: noop, removeEventListener: noop });
  const palettes = [];
  const ctx = vm.createContext({
    window, document, navigator: nav, console, setTimeout, clearTimeout, localStorage: window.localStorage,
    templateName: "vertical-menu-template", assetsPath: "/assets/",
    fetch: async () => ({ ok: true, json: async () => ({ suggestions: {} }) }),
    autocomplete: (config) => { palettes.push(config); return {}; }
  });
  vm.runInContext(MAIN, ctx, { filename: "main.js" });
  const end = Date.now() + 2000;
  while (!palettes.length && Date.now() < end) await new Promise((r) => setTimeout(r, 5));
  expect(palettes).toHaveLength(1);
  return palettes[0];
};

describe("the shell search box label", () => {
  test("comes from the shell's dictionary, with the shortcut by the machine's name for it", () => {
    const { searchPlaceholder } = helpers();
    expect(searchPlaceholder(root("Ara ({0})"), { platform: "MacIntel" })).toBe("Ara (⌘K)");
    expect(searchPlaceholder(root("Ara ({0})"), { platform: "Win32" })).toBe("Ara (Ctrl+K)");
    expect(searchPlaceholder(root("بحث ({0})"), { userAgentData: { platform: "macOS" } })).toBe("بحث (⌘K)");
  });

  test.each([["iPhone"], ["iPad"], ["iPod touch"]])("an Apple %s calls the shortcut ⌘K", (platform) => {
    const { searchShortcutName } = helpers();
    expect(searchShortcutName({ platform })).toBe("⌘K");
    // The user agent alone, without "Mac" anywhere in it: the device name has to be enough.
    expect(searchShortcutName({ userAgent: `Mozilla/5.0 (${platform}; CPU OS 17_0)` })).toBe("⌘K");
  });

  test("never falls back to an English word", () => {
    const { searchPlaceholder } = helpers();
    expect(searchPlaceholder(root(undefined), { platform: "Linux x86_64" })).toBe("Ctrl+K");
    expect(MAIN).not.toContain("Search [CTRL + K]");
  });

  test.each([
    ["Ara ({0})", { platform: "MacIntel", userAgent: "Mozilla/5.0 (Macintosh)" }, "Ara (⌘K)"],
    ["Поиск ({0})", { platform: "Win32", userAgent: "Mozilla/5.0 (Windows NT 10.0)" }, "Поиск (Ctrl+K)"],
    [undefined, { platform: "Linux x86_64", userAgent: "Mozilla/5.0 (X11; Linux)" }, "Ctrl+K"]
  ])("the palette main.js builds is given the label %s — shown as %s on %j", async (template, nav, expected) => {
    const palette = await runMain({ template, nav });
    expect(palette.container).toBe("#autocomplete");
    expect(palette.placeholder).toBe(expected);
  });

  // `_Layout.cshtml` is the FROZEN archive layout (AGENTS.md): it keeps the bare shortcut, never edited for BL-520.
  test.each(["_LayoutTenantShell.cshtml", "_LayoutPlatformAdmin.cshtml"])("%s hands the localized label to main.js (a missing resource omits it)", (layout) => {
    expect(read("Views", "Shared", layout)).toContain('data-search-placeholder="@Diten.Web.Services.Navigation.ShellSearchLabel.Template(SharedLocalizer)"');
  });

  test.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])("the label exists in %s and keeps its {0}", (lang) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const match = xml.match(/<data name="ShellSearchPlaceholder" xml:space="preserve">\s*<value>([^<]*)<\/value>/);
    expect(match, `ShellSearchPlaceholder missing in ${lang}`).not.toBeNull();
    expect(match[1]).toContain("{0}");
  });
});
