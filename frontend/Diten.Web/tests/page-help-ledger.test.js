const fs = require("fs");
const path = require("path");

/*
 * WP-UX-COMMON-01 (BL-495) — every page either carries its "?" help text or is named in the debt ledger
 * (docs/roadmap/backlog/page-help-ledger.md), and the ledger only shrinks.
 *
 *   · tenant page  = every stable page code the menu / Ctrl+K localize (`Nav.Page.{CODE}` in SharedResource);
 *                    text = `Help.{CODE}.Purpose` in all 7 languages, non-empty;
 *   · platform admin page = every Views/Platform folder drawn by _LayoutPlatformAdmin; code PLATFORM + folder;
 *                    text = en + tr.
 * A ledger row for a page that HAS its text is red (remove the row); a row for a code that is no page is red; the
 * row count may not exceed the ceiling below (adding rows is not allowed — a new page brings its text).
 */

const WEB = path.resolve(__dirname, "..");
const REPO = path.resolve(WEB, "..", "..");
const LEDGER = fs.readFileSync(path.join(REPO, "docs", "roadmap", "backlog", "page-help-ledger.md"), "utf8");
const TENANT_LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const PLATFORM_LANGS = ["en", "tr"];

// The ledger's size the day it was written, minus what was paid since. Lower it when you remove rows; never raise it.
const TENANT_CEILING = 171;
const PLATFORM_CEILING = 18;

const resxValues = (file) => {
  const xml = fs.readFileSync(file, "utf8");
  const out = new Map();
  const entry = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let match;
  while ((match = entry.exec(xml)) !== null) { out.set(match[1], match[2].trim()); }
  return out;
};

const help = Object.fromEntries(TENANT_LANGS.map((lang) => [lang,
  resxValues(path.join(WEB, "Resources", `PageHelpResource.${lang}.resx`))]));
const hasText = (code, langs) => langs.every((lang) => (help[lang].get(`Help.${code}.Purpose`) || "").length > 0);

const tenantCodes = [...new Set([...resxValues(path.join(WEB, "Resources", "SharedResource.en.resx")).keys()]
  .filter((key) => key.startsWith("Nav.Page."))
  .map((key) => key.slice("Nav.Page.".length)))].sort();

const platformCodes = fs.readdirSync(path.join(WEB, "Views", "Platform"), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .filter((entry) => {
    const dir = path.join(WEB, "Views", "Platform", entry.name);
    const views = fs.readdirSync(dir).filter((file) => file.endsWith(".cshtml") && !file.startsWith("_"));
    const shellOrNone = views.filter((file) =>
      /Layout = ("_LayoutTenantShell"|null)/.test(fs.readFileSync(path.join(dir, file), "utf8")));
    return views.length > 0 && shellOrNone.length < views.length;
  })
  .map((entry) => `PLATFORM${entry.name.replace(/[^A-Za-z0-9]/g, "").toUpperCase()}`)
  .sort();

const section = (heading) => {
  const start = LEDGER.indexOf(heading);
  if (start < 0) { return []; }
  const rest = LEDGER.slice(start + heading.length);
  const next = rest.search(/\n## /);
  const body = next < 0 ? rest : rest.slice(0, next);
  return [...body.matchAll(/^\| ([A-Z0-9]+) \|/gm)].map((m) => m[1]);
};
const tenantRows = section("## Kiracı sayfaları");
const platformRows = section("## Platform yönetici sayfaları");

describe("page help: text or a ledger row, and the ledger only shrinks", () => {
  test("the scan reaches the pages (guards against a vacuous pass)", () => {
    expect(tenantCodes.length).toBeGreaterThan(150);
    expect(tenantCodes).toEqual(expect.arrayContaining(["USERS", "ROLES", "TASKS", "MYTIMESHEET"]));
    expect(platformCodes).toEqual(expect.arrayContaining(["PLATFORMTENANTS", "PLATFORMMODULECATALOG"]));
    expect(platformCodes).not.toContain("PLATFORMREFERENCEDATA"); // drawn by the tenant shell
  });

  test("every tenant page has its help text in 7 languages or is named in the ledger", () => {
    const missing = tenantCodes.filter((code) => !hasText(code, TENANT_LANGS) && !tenantRows.includes(code));
    expect(missing, "pages with no help text and no ledger row").toEqual([]);
  });

  test("every platform admin page has its help text in en + tr or is named in the ledger", () => {
    const missing = platformCodes.filter((code) => !hasText(code, PLATFORM_LANGS) && !platformRows.includes(code));
    expect(missing).toEqual([]);
  });

  test("a page whose text is written is out of the ledger", () => {
    expect(tenantRows.filter((code) => hasText(code, TENANT_LANGS))).toEqual([]);
    expect(platformRows.filter((code) => hasText(code, PLATFORM_LANGS))).toEqual([]);
  });

  test("the ledger names only real pages, each once", () => {
    expect(tenantRows.filter((code) => !tenantCodes.includes(code))).toEqual([]);
    expect(platformRows.filter((code) => !platformCodes.includes(code))).toEqual([]);
    expect(new Set(tenantRows).size).toBe(tenantRows.length);
    expect(new Set(platformRows).size).toBe(platformRows.length);
  });

  test("the ledger only shrinks (row count at or under its ceiling)", () => {
    expect(tenantRows.length).toBeLessThanOrEqual(TENANT_CEILING);
    expect(platformRows.length).toBeLessThanOrEqual(PLATFORM_CEILING);
  });

  test("a page's text is complete in every language it carries: purpose and steps in all 7, or none", () => {
    const keys = new Set(TENANT_LANGS.flatMap((lang) => [...help[lang].keys()])
      .filter((key) => /^Help\.[A-Z0-9]+\.(Purpose|Step[1-5])$/.test(key) && !key.startsWith("Help.PLATFORM")));
    const gaps = [];
    keys.forEach((key) => TENANT_LANGS.forEach((lang) => {
      if (!(help[lang].get(key) || "").length) { gaps.push(`${lang}/${key}`); }
    }));
    expect(gaps).toEqual([]);
  });
});
