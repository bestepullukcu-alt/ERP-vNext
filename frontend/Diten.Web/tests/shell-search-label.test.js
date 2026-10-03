const fs = require("fs");
const path = require("path");

/*
 * BL-520 — the top-bar search box: its label in the reader's language, its shortcut by the name the reader's machine
 * uses. The functions are taken out of main.js itself (the shipped file), not copied.
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

describe("the shell search box label", () => {
  test("comes from the shell's dictionary, with the shortcut by the machine's name for it", () => {
    const { searchPlaceholder } = helpers();
    expect(searchPlaceholder(root("Ara ({0})"), { platform: "MacIntel" })).toBe("Ara (⌘K)");
    expect(searchPlaceholder(root("Ara ({0})"), { platform: "Win32" })).toBe("Ara (Ctrl+K)");
    expect(searchPlaceholder(root("بحث ({0})"), { userAgentData: { platform: "macOS" } })).toBe("بحث (⌘K)");
  });

  test("never falls back to an English word", () => {
    const { searchPlaceholder } = helpers();
    expect(searchPlaceholder(root(undefined), { platform: "Linux x86_64" })).toBe("Ctrl+K");
    expect(MAIN).not.toContain("Search [CTRL + K]");
  });

  test("is what the palette is built with", () => {
    expect(MAIN).toContain("placeholder: searchPlaceholder(document.documentElement, typeof navigator === 'undefined' ? null : navigator),");
  });

  test.each(["_LayoutTenantShell.cshtml", "_LayoutPlatformAdmin.cshtml"])("%s hands the localized label to main.js", (layout) => {
    expect(read("Views", "Shared", layout)).toContain('data-search-placeholder="@SharedLocalizer["ShellSearchPlaceholder"]"');
  });

  test.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])("the label exists in %s and keeps its {0}", (lang) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const match = xml.match(/<data name="ShellSearchPlaceholder" xml:space="preserve">\s*<value>([^<]*)<\/value>/);
    expect(match, `ShellSearchPlaceholder missing in ${lang}`).not.toBeNull();
    expect(match[1]).toContain("{0}");
  });
});
