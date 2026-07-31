const fs = require("fs");
const path = require("path");

/*
 * BL-044 and BL-047 — two ways the screen told the user something untrue.
 *
 * BL-044: "kapanış" found the task, "KAPANIŞ" found nothing. Invariant lowercasing maps I→i (dotted), while the
 * text on screen carries ı (dotless), so every Turkish word containing that letter disappeared from search the
 * moment it was typed in capitals. Caps lock and mobile auto-capitalisation make that ordinary. The user reads
 * "search is broken" and cannot discover why.
 *
 * BL-047: the DataTable chrome stayed English on a Turkish page — "Showing 1 to 9 of 9 entries", the pager, the
 * empty-table text. It comes from the vendor's own defaults, so no resx gate could see it.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const APP_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const DT_DEFAULTS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "dt-defaults.js");
const L10N_VIEW = path.join(repoRoot, "frontend", "Diten.Web", "Views", "WorkCenterNext", "_L10n.cshtml");

const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** The production fold, lifted out of app.js and executed — not a re-implementation. */
const foldForSearch = (() => {
  const source = fs.readFileSync(APP_JS, "utf8");
  const start = source.indexOf("const foldForSearch = (value) =>");
  if (start < 0) { throw new Error("foldForSearch is not declared in app.js"); }
  const end = source.indexOf(";", source.indexOf(".toLowerCase()", start));
  // eslint-disable-next-line no-eval
  return eval(`(${source.slice(start + "const foldForSearch = ".length, end)})`);
})();

describe("search finds the word whatever case or accents it is typed in", () => {
  /*
   * Executing the REAL function rather than asserting on its source. A source assertion would pass on a fold
   * that is spelled right and behaves wrong, and behaviour is the entire complaint here.
   */
  const matches = (needle, haystack) => foldForSearch(haystack).includes(foldForSearch(needle));

  it("tr: the dotless/dotted I no longer splits a word from itself", () => {
    // THE reported defect: this is false with plain toLowerCase(), because 'KAPANIŞ' folds to a DOTTED i.
    expect(matches("KAPANIŞ", "Haziran kapanış dosyası")).toBe(true);
    expect(matches("kapanış", "Haziran kapanış dosyası")).toBe(true);
  });

  it("tr: typing without Turkish characters at all still finds the word", () => {
    // The second half of the report — `kapanis` returned 0. Accent folding closes it for free.
    expect(matches("kapanis", "Haziran kapanış dosyası")).toBe(true);
    expect(matches("KAPANIS", "Haziran kapanış dosyası")).toBe(true);
  });

  it("de/fr: umlauts and accents fold in both directions", () => {
    expect(matches("uberprufung", "Überprüfung der Buchung")).toBe(true);
    expect(matches("ÜBERPRÜFUNG", "Überprüfung der Buchung")).toBe(true);
    expect(matches("reglement", "Règlement des factures")).toBe(true);
    expect(matches("RÈGLEMENT", "Règlement des factures")).toBe(true);
  });

  it("ru and ar keep working — the fold must not be a Turkish special case", () => {
    /*
     * The reason toLocaleLowerCase('tr') was refused: it fixes Turkish by breaking the other six languages. A
     * fold has to be symmetric, so the non-Latin scripts are asserted rather than assumed.
     */
    expect(matches("ЗАКРЫТИЕ", "Закрытие периода")).toBe(true);
    expect(matches("закрытие", "Закрытие периода")).toBe(true);
    expect(matches("إغلاق", "إغلاق الفترة")).toBe(true);
  });

  it("still tells different words apart", () => {
    // Non-vacuity: a fold that collapsed everything would pass every test above and make search useless.
    expect(matches("kapanış", "Haziran açılış dosyası")).toBe(false);
    expect(matches("uberprufung", "Règlement des factures")).toBe(false);
  });

  it("plain toLowerCase would FAIL the Turkish case, which is why the fold exists", () => {
    /*
     * The proof that these tests are not vacuous: the old implementation, run here, gets the reported answer
     * wrong. If this ever starts passing, invariant lowercasing has become adequate and the fold can go.
     */
    const oldWay = (needle, haystack) => haystack.toLowerCase().includes(needle.toLowerCase());

    expect(oldWay("KAPANIŞ", "Haziran kapanış dosyası")).toBe(false);
    expect(oldWay("kapanis", "Haziran kapanış dosyası")).toBe(false);
  });

  it("folds BOTH sides at every search site, never just the needle", () => {
    // Folding one side only leaves the haystack unmatchable — a subtler version of the same bug.
    const source = fs.readFileSync(APP_JS, "utf8");
    const searchSites = source.split("\n").filter((line) => /state\.search\.trim\(\)/.test(line));

    expect(searchSites.length, "no search site found — the scan is looking at nothing").toBeGreaterThan(0);
    searchSites.forEach((line) => {
      expect(line, `a search site still lowercases instead of folding: ${line.trim()}`).toContain("foldForSearch");
    });
  });
});

describe("the DataTable's own chrome speaks the reader's language", () => {
  it("dt-defaults reads the Dt* keys off the page payload", () => {
    // The mechanism was already central and already correct; only the supply was missing.
    const source = fs.readFileSync(DT_DEFAULTS, "utf8");
    ["DtInfo", "DtInfoEmpty", "DtInfoFiltered", "DtZeroRecords"].forEach((key) => {
      expect(source, `dt-defaults does not consume ${key}`).toContain(key);
    });
  });

  it("WorkCenterNext's payload actually SUPPLIES them", () => {
    /*
     * THE BL-047 defect. This file enumerated the module resx alone, so the shared Dt* keys never reached the
     * client and the central mechanism had nothing to work with — "Showing 1 to 9 of 9 entries" on a Turkish page.
     */
    const view = fs.readFileSync(L10N_VIEW, "utf8");

    expect(view, "the payload does not inject the Dt* keys").toContain("dtKeys");
    ["DtInfo", "DtInfoEmpty", "DtInfoFiltered", "DtEmptyTable", "DtNoRecords", "DtZeroRecords"]
      .forEach((key) => expect(view, `${key} is not in the payload`).toContain(key));
    expect(view, "the payload never reads SharedResource").toContain("SharedLocalizer");
  });

  it.each(LOCALES)("SharedResource.%s translates every Dt* key", (locale) => {
    const resx = fs.readFileSync(
      path.join(repoRoot, "frontend", "Diten.Web", "Resources", `SharedResource.${locale}.resx`), "utf8");

    ["DtInfo", "DtInfoEmpty", "DtInfoFiltered", "DtEmptyTable", "DtNoRecords", "DtZeroRecords"]
      .forEach((key) => expect(resx, `${key} missing in ${locale}`).toContain(`name="${key}"`));
  });

  it("does not leave the Dt* strings in English outside en", () => {
    // The l10n gate applied to the one class of string it could never see before: vendor chrome.
    const value = (locale, key) => {
      const resx = fs.readFileSync(
        path.join(repoRoot, "frontend", "Diten.Web", "Resources", `SharedResource.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx);
      return match ? match[1].trim() : null;
    };

    ["DtInfo", "DtZeroRecords"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();
      LOCALES.filter((l) => l !== "en").forEach((locale) => {
        expect(value(locale, key), `${key}/${locale} is still the English text`).not.toBe(english);
      });
    });
  });
});
