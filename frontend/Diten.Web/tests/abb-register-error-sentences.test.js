const fs = require("fs");
const path = require("path");

/*
 * WP-MDM-ABB-ACCEPT-01 FIX1 S4 — EVERY ABB REFUSAL HAS A SENTENCE, IN SEVEN LANGUAGES.
 *
 * Read from production files only: the code → key map in the page script, the keys the page's l10n partial emits,
 * and the seven resx files. (Which codes MDM can answer is measured on the MDM side:
 * ProductAbbreviationErrorSentenceContractTests reads MDM's sources and this same page script.)
 */
const WEB = path.resolve(__dirname, "..");
const JS = fs.readFileSync(path.join(WEB, "wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js"), "utf8");
const PARTIAL = fs.readFileSync(path.join(WEB, "Views/MDM/ProductAbbreviationRegister/_IndexL10n.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  path.join(WEB, `Resources/Views/MDM/ProductAbbreviationRegister/ProductAbbreviationRegisterIndex.${lang}.resx`), "utf8");
const value = (xml, key) => {
  const match = xml.match(new RegExp(`<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`));
  return match ? match[1].trim() : null;
};

const errorMap = () => {
  const block = JS.slice(JS.indexOf("const ERROR_KEYS = Object.freeze({"), JS.indexOf("});", JS.indexOf("const ERROR_KEYS")));
  return [...block.matchAll(/^\s*([A-Z_]+): '([A-Za-z]+)',?$/gm)].map((m) => ({ code: m[1], key: m[2] }));
};

describe("ABB register refusals", () => {
  it("map to sentence keys the page actually emits", () => {
    const pairs = errorMap();
    expect(pairs.length).toBeGreaterThan(25);
    for (const { key } of pairs) {
      expect(PARTIAL).toMatch(new RegExp(`\\b${key} = (Localizer|SharedLocalizer)\\["${key}"\\]\\.Value`));
    }
  });

  it.each(LANGS)("have every page-local sentence in %s", (lang) => {
    const xml = resx(lang);
    const pageKeys = [...PARTIAL.matchAll(/= Localizer\["([A-Za-z]+)"\]\.Value/g)].map((m) => m[1]);
    expect(pageKeys.length).toBeGreaterThan(40);
    for (const key of pageKeys) {
      expect([key, value(xml, key)]).toEqual([key, expect.stringMatching(/\S/)]);
    }
  });

  it("never tells the reader to reload for a refusal reloading cannot fix", () => {
    const keyOf = Object.fromEntries(errorMap().map(({ code, key }) => [code, key]));
    for (const code of ["ABBREVIATION_ALREADY_ALLOCATED", "ABBREVIATION_CORRECTION_VALUE_UNCHANGED", "ABBREVIATION_REQUEST_ALREADY_CLOSED"]) {
      expect(keyOf[code]).not.toBe("ErrorConflict");
      expect(value(resx("en"), keyOf[code])).not.toMatch(/reload/i);
    }
  });
});
