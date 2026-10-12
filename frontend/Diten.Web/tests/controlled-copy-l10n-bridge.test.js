const { read, resxValue, bridgeEntries } = require("./controlled-copy-harness");

/*
 * BL-452 package 2 — the controlled copy speaks the reader's language, and the three places a word lives agree:
 *   dt-defaults.js (the keys it READS) ⇔ _LayoutTenantShell's l10n bridge (the keys it PUBLISHES) ⇔ SharedResource
 *   in all seven tenant languages (the words). A key read but not published prints English; a key published but not
 *   in a resx prints the key's own name. Both are measured here from the production files, not from a copy.
 */
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const TOOLBAR_KEYS = ["Action", "Print", "PDF", "Copy"];

const readKeys = () => {
  const src = read("wwwroot", "assets", "js", "dt-defaults.js");
  const block = /var CONTROLLED_COPY_FALLBACK = \{([\s\S]*?)\};/.exec(src);
  expect(block, "dt-defaults.js lost its controlled-copy key list").toBeTruthy();
  return [...block[1].matchAll(/(ControlledCopy\w+):/g)].map((m) => m[1]);
};

describe("controlled copy l10n: dt-defaults ⇔ tenant bridge ⇔ SharedResource × 7", () => {
  test("every key dt-defaults reads for the copy and the Action menu is published by the tenant shell's bridge", () => {
    const published = bridgeEntries().map((e) => e.bridge);
    const read = [...readKeys(), ...TOOLBAR_KEYS];
    expect(readKeys().length, "13 copy keys").toBe(13);
    read.forEach((key) => expect(published, `${key} is read by dt-defaults but not in the l10n bridge`).toContain(key));
  });

  test.each(LANGS)("%s: every bridge entry of this package has its SharedResource key, once, with a real word", (lang) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const ours = bridgeEntries().filter((e) => e.bridge.startsWith("ControlledCopy") || TOOLBAR_KEYS.includes(e.bridge));
    expect(ours.length).toBe(17);
    ours.forEach(({ bridge, resx }) => {
      const count = xml.split(`<data name="${resx}"`).length - 1;
      expect(count, `${lang}: ${resx} (bridge ${bridge}) appears ${count} times`).toBe(1);
      const value = resxValue(lang, resx);
      expect(value && value.trim(), `${lang}: ${resx} is empty`).toBeTruthy();
      // A dotted key echoed back is a missing resource ("PDF" and English "Action" are legitimately their own word).
      if (resx.includes(".")) expect(value, `${lang}: ${resx} is its own key`).not.toBe(resx);
    });
  });

  test("the two templates carry their placeholders in every language", () => {
    LANGS.forEach((lang) => {
      expect(resxValue(lang, "ControlledCopy.Uncontrolled"), lang).toContain("{0}");
      expect(resxValue(lang, "ControlledCopy.Page"), lang).toMatch(/\{0\}[\s\S]*\{1\}/);
    });
  });

  test("the six non-English files are translated, not English copies (Action + the copy's words)", () => {
    const en = (k) => resxValue("en", k);
    ["tr", "zh", "ar", "ru"].forEach((lang) => {
      ["Action", "ControlledCopy.Filters", "ControlledCopy.Uncontrolled", "ControlledCopy.Page"].forEach((k) => {
        expect(resxValue(lang, k), `${lang}: ${k} is still English`).not.toBe(en(k));
      });
    });
  });

  test("the old English print stamp is gone from the shared layer", () => {
    const src = read("wwwroot", "assets", "js", "dt-defaults.js");
    expect(src).not.toMatch(/'Generated '/);
    expect(src).not.toMatch(/customizePrintWindow/);
  });
});
