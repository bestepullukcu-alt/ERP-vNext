const fs = require("fs");
const path = require("path");

/*
 * WP-UX-COMMON-01 (BL-495) + FIX1 — every page either carries its "?" help text or is named in the debt ledger
 * (docs/roadmap/backlog/page-help-ledger.md), and the ledger only shrinks.
 *
 * The page sets are DERIVED FROM PRODUCTION, never listed here:
 *   · tenant pages  = the menu / Ctrl+K stable codes (`Nav.Page.{CODE}` in SharedResource) ∪ every page code the module
 *                     manifests register (services/**\/*ManifestProvider.cs) — child pages hidden from the menu too;
 *                     text = `Help.{CODE}.Purpose` in all 7 languages, non-empty;
 *   · platform admin pages = every Views/Platform folder drawn by _LayoutPlatformAdmin; code PLATFORM + folder; en + tr;
 *   · FROZEN views  = every non-partial view that falls to the frozen `_Layout` (no `Layout =`, outside Views/Platform,
 *                     whose _ViewStart sets _Layout): that shell has no help button, no Enter rule, no shortcut layer —
 *                     each is named in the ledger so "help on every page" is never claimed for them.
 *
 * The ceiling lives in the LEDGER's own header line, the one place git tracks with the list: raising it is a visible
 * change to that document in review. This test measures rows ≤ ceiling; it cannot see the previous commit's ceiling.
 */

const WEB = path.resolve(__dirname, "..");
const REPO = path.resolve(WEB, "..", "..");
const LEDGER = fs.readFileSync(path.join(REPO, "docs", "roadmap", "backlog", "page-help-ledger.md"), "utf8");
const TENANT_LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const PLATFORM_LANGS = ["en", "tr"];
const normalize = (code) => String(code).replace(/[^A-Za-z0-9]/g, "").toUpperCase();

const walk = (dir, filter, out = []) => {
  if (!fs.existsSync(dir)) { return out; }
  fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== "bin" && entry.name !== "obj" && entry.name !== "node_modules") { walk(full, filter, out); }
    } else if (filter(entry.name)) { out.push(full); }
  });
  return out;
};

const resxValues = (file) => {
  const xml = fs.readFileSync(file, "utf8");
  const out = new Map();
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) { out.set(m[1], m[2].trim()); }
  return out;
};

const help = Object.fromEntries(TENANT_LANGS.map((lang) => [lang,
  resxValues(path.join(WEB, "Resources", `PageHelpResource.${lang}.resx`))]));
const hasText = (code, langs) => langs.every((lang) => (help[lang].get(`Help.${code}.Purpose`) || "").length > 0);

const navCodes = [...resxValues(path.join(WEB, "Resources", "SharedResource.en.resx")).keys()]
  .filter((key) => key.startsWith("Nav.Page.")).map((key) => key.slice("Nav.Page.".length));

const manifestFiles = walk(path.join(REPO, "services"), (name) => name.endsWith("ManifestProvider.cs"));
const manifestCodes = manifestFiles.flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  const consts = Object.fromEntries([...source.matchAll(/const string (\w+)\s*=\s*"([^"]+)"/g)].map((m) => [m[1], m[2]]));
  const named = [...source.matchAll(/PageCode:\s*(?:"([^"]+)"|(\w+))/g)].map((m) => m[1] || consts[m[2]]).filter(Boolean);
  const positional = [...source.matchAll(/new ModuleManifestPage\(\s*"([^"]+)"/g)].map((m) => m[1]);
  return [...named, ...positional];
}).map(normalize);

const tenantCodes = [...new Set([...navCodes, ...manifestCodes])].sort();

const platformCodes = fs.readdirSync(path.join(WEB, "Views", "Platform"), { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .filter((entry) => {
    const dir = path.join(WEB, "Views", "Platform", entry.name);
    const views = fs.readdirSync(dir).filter((file) => file.endsWith(".cshtml") && !file.startsWith("_"));
    const shellOrNone = views.filter((file) =>
      /Layout = ("_LayoutTenantShell"|null)/.test(fs.readFileSync(path.join(dir, file), "utf8")));
    return views.length > 0 && shellOrNone.length < views.length;
  })
  .map((entry) => `PLATFORM${normalize(entry.name)}`)
  .sort();

const VIEWS = path.join(WEB, "Views");
const rootViewStart = fs.readFileSync(path.join(VIEWS, "_ViewStart.cshtml"), "utf8");
const frozenViews = walk(VIEWS, (name) => name.endsWith(".cshtml") && !name.startsWith("_"))
  .filter((file) => {
    const source = fs.readFileSync(file, "utf8");
    const layout = /Layout\s*=\s*(null|"([^"]+)")/.exec(source);
    if (layout) { return layout[2] === "_Layout" || layout[2] === "~/Views/Shared/_Layout.cshtml"; }
    return !file.startsWith(path.join(VIEWS, "Platform") + path.sep);
  })
  .map((file) => path.relative(VIEWS, file).split(path.sep).join("/"))
  .sort();

const section = (heading, pattern) => {
  const start = LEDGER.indexOf(heading);
  if (start < 0) { return []; }
  const rest = LEDGER.slice(start + heading.length);
  const next = rest.search(/\n## /);
  return [...(next < 0 ? rest : rest.slice(0, next)).matchAll(pattern)].map((m) => m[1]);
};
const tenantRows = section("## Kiracı sayfaları", /^\| ([A-Z0-9]+) \|/gm);
const platformRows = section("## Platform yönetici sayfaları", /^\| ([A-Z0-9]+) \|/gm);
const frozenRows = section("## FROZEN `_Layout` görünümleri", /^\| `([^`]+)` \|/gm);

const ceiling = (() => {
  const m = /\*\*Tavan \(yalnız azalır\): kiracı (\d+) · platform (\d+) · frozen (\d+)\*\*/.exec(LEDGER);
  return m ? { tenant: Number(m[1]), platform: Number(m[2]), frozen: Number(m[3]) } : null;
})();


/*
 * WP-LANE-MAIN-SYNC-02 (owner decision 2026-10-14, BL-638) — the tenant pages that came with main-sync 2026-10-14 and carry
 * no help text. NOT ledger rows (the ledger's ceiling stays where it was): a named list, owner + reason per page. The
 * ceiling of this list is the list itself — MAIN_SYNC_2_PAGE_KEYS pins its exact keys, so a 33rd page is red, and a page
 * whose text is written (or that no longer exists) is red until its row leaves (the list only shrinks).
 */
const { STOCK, MANUFACTURING, ESIGN, CRM, PLANNING, MAIN_SYNC_2_WHY, MAIN_SYNC_4_WHY } = require("./main-sync-owners");
const MAIN_SYNC_2_PAGES = {
  // WP-MVP1-FU-CLEAN2 (UXH-001): the 31 MVP-1 rows (Stok / e-imza) left — their help text is written; only BOMS (Manufacturing) remains
  BOMS: { owner: MANUFACTURING, why: MAIN_SYNC_2_WHY }
};
const MAIN_SYNC_2_PAGE_KEYS = [
  "BOMS"
];

/*
 * WP-LANE-MAIN-SYNC-03 (main-sync 2026-10-14, origin/main 19497c934, BL-642) — the same rule for the third sync: CRM W4 visit
 * report v2 pages (COMPETITORPRODUCTS is the W4 competitor list on MDM's menu, same owner). Pinned keys, named, only shrinks.
 */
const MAIN_SYNC_3_PAGES = {
  COMPETITORPRODUCTS: { owner: CRM, why: MAIN_SYNC_2_WHY },
  CRMDOCTORINSIGHTTHRESHOLDS: { owner: CRM, why: MAIN_SYNC_2_WHY },
  OBJECTIONRESPONSES: { owner: CRM, why: MAIN_SYNC_2_WHY },
  VISITEVIDENCESETTINGS: { owner: CRM, why: MAIN_SYNC_2_WHY },
  VISITWORKSPACE: { owner: CRM, why: MAIN_SYNC_2_WHY }
};
const MAIN_SYNC_3_PAGE_KEYS = ["COMPETITORPRODUCTS", "CRMDOCTORINSIGHTTHRESHOLDS", "OBJECTIONRESPONSES", "VISITEVIDENCESETTINGS", "VISITWORKSPACE"];

/*
 * WP-LANE-MAIN-SYNC-05 (main-sync 2026-10-16, origin/main 3912d568b = PR #146, owner decision in session) — the same rule
 * for the fourth sync. One page: MOD-0188 Demand Planning. (WP number 05, list number 4: WP-LANE-MAIN-SYNC-04 is
 * already the 2026-10-14 sync's, the finder on BL-645.)
 *
 * It arrived with `Help.DEMANDPLANNING.Purpose` in ZERO of the seven languages — measured, not assumed — and its page code
 * comes from services/Diten.PlanningService/src/Diten.PlanningService.Api/ModuleRegistration/DemandPlanningManifestProvider.cs
 * (PageCode "DEMANDPLANNING", RoutePath "/SupplyChain/DemandPlanning").
 *
 * Why this list and not a ledger row: the ledger's ceiling ("Tavan (yalnız azalır): kiracı 246 …") says in its own words
 * that every new row after WP-LANE-UX-GUARDS-01 is red again, so a row there is a Control Tower ceiling decision. Why not
 * write the text: the page is MOD-0188's and its user-facing wording is its owner's to choose (XMC-001).
 */
const MAIN_SYNC_4_PAGES = {
  DEMANDPLANNING: { owner: PLANNING, why: MAIN_SYNC_4_WHY }
};
const MAIN_SYNC_4_PAGE_KEYS = ["DEMANDPLANNING"];


describe("page help: text or a ledger row, and the ledger only shrinks", () => {
  test("the scan reaches the pages (guards against a vacuous pass)", () => {
    expect(navCodes.length).toBeGreaterThan(150);
    expect(manifestFiles.length).toBeGreaterThan(10);
    expect(tenantCodes).toEqual(expect.arrayContaining(["USERS", "ROLES", "TASKS", "MYTIMESHEET", "PERMISSIONS"]));
    expect(tenantCodes.length, "the manifests added no page the menu lacks").toBeGreaterThan(navCodes.length);
    expect(platformCodes).toEqual(expect.arrayContaining(["PLATFORMTENANTS", "PLATFORMMODULECATALOG"]));
    expect(platformCodes).not.toContain("PLATFORMREFERENCEDATA"); // drawn by the tenant shell
    expect(rootViewStart).toMatch(/Layout\s*=\s*"_Layout"/);
    expect(frozenViews.length).toBeGreaterThan(50);
  });

  test("every tenant page — menu or manifest — has its help text in 7 languages or is named in the ledger", () => {
    const missing = tenantCodes.filter((code) => !hasText(code, TENANT_LANGS) && !tenantRows.includes(code)
      && !MAIN_SYNC_2_PAGES[code] && !MAIN_SYNC_3_PAGES[code] && !MAIN_SYNC_4_PAGES[code]);
    expect(missing, "pages with no help text and no ledger row").toEqual([]);
  });

  test("WP-LANE-MAIN-SYNC-05: the fourth sync's page list is exactly its pinned keys, named, and only shrinks", () => {
    expect(Object.keys(MAIN_SYNC_4_PAGES).sort(), "the list's keys ≠ its pin (adding or removing a row edits the pin too)")
      .toEqual([...MAIN_SYNC_4_PAGE_KEYS].sort());
    expect(Object.entries(MAIN_SYNC_4_PAGES).filter(([, row]) => !row || row.owner !== PLANNING || row.why !== MAIN_SYNC_4_WHY)
      .map(([code]) => code), "a row without MOD-0188 as its owner or the fourth sync's reason").toEqual([]);
    expect(Object.keys(MAIN_SYNC_4_PAGES).filter((code) => !tenantCodes.includes(code) || hasText(code, TENANT_LANGS)
      || tenantRows.includes(code) || MAIN_SYNC_2_PAGES[code] || MAIN_SYNC_3_PAGES[code]),
    "rows whose page has its text now, is in the ledger or an earlier sync's list, or no longer exists — remove them").toEqual([]);
  });

  test("WP-LANE-MAIN-SYNC-03: the third sync's page list is exactly its pinned keys, named, and only shrinks", () => {
    expect(Object.keys(MAIN_SYNC_3_PAGES).sort()).toEqual([...MAIN_SYNC_3_PAGE_KEYS].sort());
    expect(Object.entries(MAIN_SYNC_3_PAGES).filter(([, row]) => !row || row.owner !== CRM || row.why !== MAIN_SYNC_2_WHY)
      .map(([code]) => code)).toEqual([]);
    expect(Object.keys(MAIN_SYNC_3_PAGES).filter((code) => !tenantCodes.includes(code) || hasText(code, TENANT_LANGS)
      || tenantRows.includes(code) || MAIN_SYNC_2_PAGES[code])).toEqual([]);
  });

  test("WP-LANE-MAIN-SYNC-02: the main-sync 2026-10-14 page list is exactly its pinned keys, named, and only shrinks", () => {
    expect(Object.keys(MAIN_SYNC_2_PAGES).sort(), "the list's keys ≠ its pin (adding or removing a row edits the pin too)")
      .toEqual([...MAIN_SYNC_2_PAGE_KEYS].sort());
    const owners = { STOCK, MANUFACTURING, ESIGN };
    const misnamed = Object.entries(MAIN_SYNC_2_PAGES).filter(([, row]) => !row || !Object.values(owners).includes(row.owner)
      || row.why !== MAIN_SYNC_2_WHY).map(([code]) => code);
    expect(misnamed, "a row without its owner or the main-sync reason").toEqual([]);
    const stale = Object.keys(MAIN_SYNC_2_PAGES).filter((code) => !tenantCodes.includes(code) || hasText(code, TENANT_LANGS)
      || tenantRows.includes(code));
    expect(stale, "rows whose page has its text now, is in the ledger, or no longer exists — remove them").toEqual([]);
  });

  test("every platform admin page has its help text in en + tr or is named in the ledger", () => {
    expect(platformCodes.filter((code) => !hasText(code, PLATFORM_LANGS) && !platformRows.includes(code))).toEqual([]);
  });

  test("every view on the FROZEN _Layout is named in the ledger (it cannot show help)", () => {
    expect(frozenViews.filter((view) => !frozenRows.includes(view))).toEqual([]);
  });

  test("a page whose text is written is out of the ledger", () => {
    expect(tenantRows.filter((code) => hasText(code, TENANT_LANGS))).toEqual([]);
    expect(platformRows.filter((code) => hasText(code, PLATFORM_LANGS))).toEqual([]);
  });

  test("the ledger names only real pages and views, each once", () => {
    expect(tenantRows.filter((code) => !tenantCodes.includes(code))).toEqual([]);
    expect(platformRows.filter((code) => !platformCodes.includes(code))).toEqual([]);
    expect(frozenRows.filter((view) => !frozenViews.includes(view))).toEqual([]);
    [tenantRows, platformRows, frozenRows].forEach((rows) => expect(new Set(rows).size).toBe(rows.length));
  });

  test("the ledger's header names its row counts EXACTLY: every added or removed row edits the header too", () => {
    // UX-COMMON-FIX2 (R2) — equality, not "at or under": a removed row must lower the header in the same change.
    expect(ceiling, "the ledger lost its ceiling line").not.toBeNull();
    expect(tenantRows.length, "tenant rows ≠ the header's count").toBe(ceiling.tenant);
    expect(platformRows.length, "platform rows ≠ the header's count").toBe(ceiling.platform);
    expect(frozenRows.length, "frozen rows ≠ the header's count").toBe(ceiling.frozen);
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
