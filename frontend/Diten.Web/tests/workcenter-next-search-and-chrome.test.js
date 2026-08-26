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

/*
 * ══ BL-047, THE DELIVERY HALF ═════════════════════════════════════════════════════════════════════════════
 *
 * The previous round added the six Dt* keys to the payload and called it done. It was not: dt-defaults reads
 * `window.L10n` (dt-defaults.js:8), and app.js seeded only Search and Action into it. The strings existed at
 * both ends and never met — the table went on saying "Showing 1 to 9 of 9 entries" on a Turkish page while
 * every source-level assertion passed.
 *
 * So these assert the CONSUMER's dictionary, not the producer's file. Producing a value is not delivering it.
 */
describe("the table's dictionary actually receives the keys", () => {
  const app = () => fs.readFileSync(APP_JS, "utf8");

  it("seeds every Dt* key into the dictionary dt-defaults reads", () => {
    /*
     * dt-defaults reads window.L10n and nothing else. A key in #workcenternext-l10n that never reaches
     * window.L10n is invisible to it — which is exactly the state this test was written to end.
     */
    const source = app();
    const seedBlock = source.slice(source.indexOf("global.L10n = global.L10n || {}"));
    const scoped = seedBlock.slice(0, 900);

    ["DtInfo", "DtInfoEmpty", "DtInfoFiltered", "DtEmptyTable", "DtNoRecords", "DtZeroRecords"]
      .forEach((key) => expect(scoped, `${key} is never seeded into window.L10n`).toContain(key));
  });

  it("seeds them THROUGH the translator, so an untranslated key is not shipped as its own name", () => {
    // t(key) returning the key itself means the resx lacks it; writing that into L10n would put "DtInfo" on
    // screen. The guard is the reason the seeding is a filtered loop rather than a plain assignment.
    const source = app();
    const seedBlock = source.slice(source.indexOf("global.L10n = global.L10n || {}"), source.indexOf("const tableFilterButton"));

    expect(seedBlock).toMatch(/value !== key/);
  });

  it("dt-defaults still reads window.L10n — if that changes, the seeding above is pointing at nothing", () => {
    // Non-vacuity across the seam: the seeding is only correct while this is the dictionary being read.
    expect(fs.readFileSync(DT_DEFAULTS, "utf8")).toMatch(/window\.L10n/);
  });
});

/*
 * ══ BL-046, THE LABEL BOUNDARY ════════════════════════════════════════════════════════════════════════════
 *
 * Freezing slaState server-side without freezing the day count produced "-2 days left" in History — a
 * regression introduced by the previous round's own half-fix. The old text was wrong but readable; this was
 * not. The day count still needs a closing timestamp on the contract (recorded, not done); this closes the
 * hole that makes the wrong number unreadable, and the pre-existing "0 days left" case with it.
 */
describe("the SLA label never says a negative or zero number of days are left", () => {
  const slaLabelSource = () => {
    const source = fs.readFileSync(APP_JS, "utf8");
    const start = source.indexOf("const slaLabel = (item) =>");
    expect(start).toBeGreaterThan(-1);
    return source.slice(start, source.indexOf("};", start));
  };

  it("routes a past deadline to the overdue wording, never to 'days left'", () => {
    const body = slaLabelSource();

    expect(body, "on-track with d < 0 still prints 'days left'").toMatch(/d < 0/);
    expect(body).toContain("SlaOverdueByDays");
  });

  it("says 'due today' rather than '0 days left'", () => {
    expect(slaLabelSource()).toMatch(/d === 0[\s\S]{0,80}SlaDueToday/);
  });

  it("falls back to no-SLA when the day count is missing entirely", () => {
    // null is a real state (no due date). Arithmetic on it produced NaN in the label.
    expect(slaLabelSource()).toMatch(/d == null/);
  });
});

/*
 * ══ BL-049 — the raw GUID off the primary surface ═════════════════════════════════════════════════════════
 *
 * Detail showed "Kaynak kaydı 31a44983-40cc-…" as an ordinary field, beside human-readable facts and directly
 * above an "open the source record" button that already does the only thing the id is for. The id gives the
 * reader no capability; it just makes the useful fields harder to find.
 *
 * Not deleted — it IS what a support conversation needs. Moved to a support affordance instead.
 */
describe("the source reference is a support affordance, not a field to read", () => {
  const app = () => fs.readFileSync(APP_JS, "utf8");

  it("no longer renders the id as a plain preview field", () => {
    // THE defect, stated as its absence.
    expect(app(), "the raw id is still a preview field").not.toContain("previewField('bx-hash', 'DetailSourceId'");
  });

  it("shortens what is displayed but keeps the whole value reachable", () => {
    const source = app();

    expect(source).toContain("referenceField");
    // The full value on the title and on the button — a truncated id nobody can recover would be worse than
    // the noisy one it replaced.
    expect(source).toMatch(/title="\$\{esc\(full\)\}"/);
    expect(source).toMatch(/data-wcn-copy="\$\{esc\(full\)\}"/);
  });

  it("leaves a short business key alone rather than ellipsing something already readable", () => {
    // The truncation is for opaque ids. A provider that sends "INV-2026-0042" should not have it mangled.
    expect(app()).toMatch(/full\.length > 13/);
  });

  it("actually copies, and says so when it cannot", () => {
    /*
     * A copy button that does nothing is worse than no button. navigator.clipboard is absent over plain http, so
     * the failure path has to be visible rather than swallowed.
     */
    const source = app();

    expect(source).toContain("data-wcn-copy]");
    expect(source).toContain("clipboard");
    expect(source).toContain("ReferenceCopyFailed");
  });

  it.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])("%s translates all three new strings", (locale) => {
    const resx = fs.readFileSync(path.join(
      repoRoot, "frontend", "Diten.Web", "Resources", "Views", "WorkCenterNext",
      `WorkCenterNextIndex.${locale}.resx`), "utf8");

    ["CopyReference", "ReferenceCopied", "ReferenceCopyFailed"]
      .forEach((key) => expect(resx, `${key} missing in ${locale}`).toContain(`name="${key}"`));
  });
});
