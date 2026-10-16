const fs = require("fs");
const path = require("path");

/*
 * UXH-001 §2 (owner decision 2026-10-14) — the rule, the module pack standard and the real resx say the same thing about
 * the "?" help panel's text. The rule was written for the four-section panel and kept describing it after
 * WP-UX-HELP-PANEL-02 (three tabs, Audience, step titles, PageHelpCatalog); nothing turned red. This test is that red:
 *   · every page with text carries the WHOLE required set (Purpose, Audience, Step1..n without gaps, a title per step) in
 *     every language it owes (tenant 7, platform 2), the optional Immediate in all or none, the same steps everywhere;
 *   · every related page PageHelpCatalog lists for a page has its reason text, and no reason text exists without a row;
 *   · every Help.Panel.* key the rule names exists, the rule's table says what is required and what is optional, and the
 *     module pack standard asks for the same keys.
 * The ledger test (page-help-ledger.test.js) keeps measuring WHICH pages have text; this one measures that the text is whole.
 */

const WEB = path.resolve(__dirname, "..");
const REPO = path.resolve(WEB, "..", "..");
const TENANT_LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const PLATFORM_LANGS = ["en", "tr"];
const MAX_STEPS = 5;

const resxValues = (file) => {
  const xml = fs.readFileSync(file, "utf8");
  const out = new Map();
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) { out.set(m[1], m[2].trim()); }
  return out;
};

const help = Object.fromEntries(TENANT_LANGS.map((lang) => [lang,
  resxValues(path.join(WEB, "Resources", `PageHelpResource.${lang}.resx`))]));
const rule = fs.readFileSync(path.join(REPO, ".antigravity", "rules", "page-help-and-keyboard.md"), "utf8");
const packStandard = fs.readFileSync(path.join(REPO, ".antigravity", "rules", "module-pack-standard.md"), "utf8");
const catalogSource = fs.readFileSync(path.join(WEB, "Services", "Help", "PageHelpCatalog.cs"), "utf8");

const langsFor = (code) => (code.startsWith("PLATFORM") ? PLATFORM_LANGS : TENANT_LANGS);
const text = (lang, key) => help[lang].get(key) || "";
const pagesWithText = [...new Set([...help.en.keys()]
  .map((key) => /^Help\.([A-Z0-9]+)\.Purpose$/.exec(key))
  .filter(Boolean).map((m) => m[1]))].sort();

// PageHelpCatalog's related rows, read from the production source: ["USERS"] = [new("USERROLES", "…"), …]
const catalogRelated = Object.fromEntries([...catalogSource.matchAll(/\["([A-Z0-9]+)"\]\s*=\s*\[((?:\s*new\("[A-Z0-9]+",\s*"[^"]*"\),?)+)\s*\]/g)]
  .map((m) => [m[1], [...m[2].matchAll(/new\("([A-Z0-9]+)"/g)].map((r) => r[1])]));

describe("UXH-001 §2 — every page with help text carries the whole required set", () => {
  test("the resx has pages with text to measure", () => {
    expect(pagesWithText.length).toBeGreaterThan(0);
  });

  test.each(pagesWithText)("%s: Purpose and Audience are non-empty in every language it owes", (code) => {
    const missing = langsFor(code).flatMap((lang) => ["Purpose", "Audience"]
      .filter((field) => !text(lang, `Help.${code}.${field}`)).map((field) => `${lang}:${field}`));
    expect(missing).toEqual([]);
  });

  test.each(pagesWithText)("%s: steps 1..n without gaps (1 ≤ n ≤ 5), each with a title, the same n in every language", (code) => {
    const problems = [];
    const counts = langsFor(code).map((lang) => {
      const present = Array.from({ length: MAX_STEPS + 2 }, (_, i) => i + 1).filter((n) => text(lang, `Help.${code}.Step${n}`));
      const n = present.length;
      if (n < 1 || n > MAX_STEPS) { problems.push(`${lang}: ${n} steps`); }
      present.forEach((step, i) => { if (step !== i + 1) { problems.push(`${lang}: Step${step} after a gap`); } });
      present.forEach((step) => { if (!text(lang, `Help.${code}.Step${step}Title`)) { problems.push(`${lang}: Step${step}Title missing`); } });
      const orphanTitles = Array.from({ length: MAX_STEPS + 2 }, (_, i) => i + 1)
        .filter((s) => text(lang, `Help.${code}.Step${s}Title`) && !text(lang, `Help.${code}.Step${s}`));
      orphanTitles.forEach((s) => problems.push(`${lang}: Step${s}Title without Step${s}`));
      return n;
    });
    if (new Set(counts).size > 1) { problems.push(`step counts differ by language: ${counts.join(",")}`); }
    expect(problems).toEqual([]);
  });

  test.each(pagesWithText)("%s: the optional Immediate is in all its languages or in none", (code) => {
    const holders = langsFor(code).filter((lang) => text(lang, `Help.${code}.Immediate`));
    expect([0, langsFor(code).length]).toContain(holders.length);
  });
});

describe("UXH-001 §2 — related pages: PageHelpCatalog owns the list, the resx owns the reason", () => {
  test("the catalog source parses into rows (the guard is not empty by accident)", () => {
    expect(Object.keys(catalogRelated).length).toBeGreaterThan(0);
  });

  test("every catalog row has its reason text in every language the page owes", () => {
    const missing = Object.entries(catalogRelated).flatMap(([code, related]) => related.flatMap((target) =>
      langsFor(code).filter((lang) => !text(lang, `Help.${code}.Related.${target}`)).map((lang) => `${code}→${target} ${lang}`)));
    expect(missing).toEqual([]);
  });

  test("no reason text exists without a catalog row (the list is never written in the resx)", () => {
    const orphans = TENANT_LANGS.flatMap((lang) => [...help[lang].keys()]
      .map((key) => /^Help\.([A-Z0-9]+)\.Related\.([A-Z0-9]+)$/.exec(key)).filter(Boolean)
      .filter((m) => !(catalogRelated[m[1]] || []).includes(m[2])).map((m) => `${lang}:${m[0]}`));
    expect(orphans).toEqual([]);
  });
});

describe("UXH-001 — the rule and the module pack standard name only what the code has", () => {
  test("every Help.Panel.* key the rule names exists in all seven languages", () => {
    const named = [...new Set([...rule.matchAll(/`(Help\.Panel\.[A-Za-z]+)`/g)].map((m) => m[1]))];
    expect(named.length).toBeGreaterThan(0);
    const missing = named.flatMap((key) => TENANT_LANGS.filter((lang) => !help[lang].has(key)).map((lang) => `${lang}:${key}`));
    expect(missing).toEqual([]);
  });

  test("the rule's table marks Purpose, Audience, Step{n} and Step{n}Title required and Immediate optional", () => {
    const row = (key) => (rule.split("\n").find((line) => line.startsWith(`| \`Help.{KOD}.${key}\` |`)) || "");
    ["Purpose", "Audience", "Step{n}", "Step{n}Title"].forEach((key) => expect(row(key)).toMatch(/\*\*zorunlu\*\*/));
    expect(row("Immediate")).toMatch(/isteğe bağlı/);
  });

  test("the module pack standard asks for the same keys, in both its checklist and its template", () => {
    const lines = packStandard.split("\n").filter((line) => line.includes("Sayfa yardim metni"));
    expect(lines.length).toBe(2);
    lines.forEach((line) => {
      ["Help.{KOD}.Purpose", "Help.{KOD}.Audience", "Step{n}Title", "Help.{KOD}.Immediate", "PageHelpCatalog"]
        .forEach((key) => expect(line).toContain(key));
      expect(line).not.toMatch(/`Step1\.\.5`/);
    });
  });
});
