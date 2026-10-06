const fs = require("fs");
const path = require("path");
const vm = require("node:vm");

/*
 * WP-MDM-BRAND-ACCEPT-01 FIX1 — the Brand register, measured on the shipped files:
 *  - item 6: it stands on the list shell and the list component (server mode, no selection), the saved view points at
 *    the declared columns, every export entry says "current page only";
 *  - item 7: every MDM reason code has a sentence in all seven languages, the page warns for an unmapped one;
 *  - item 3: the archive confirmation says it cannot be undone and how many products are in use;
 *  - item 8: a reference without a master reads "could not be resolved", never as its id;
 *  - item 11: the archive carries the anti-forgery token;
 *  - item 13: dates in the interface language; the row menu offers only what the controller allows.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (language) => read("Resources", "Views", "MasterData", "Brands", `BrandsIndex.${language}.resx`);
const resxValue = (language, key) =>
  (resx(language).match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`)) || [])[1];

const REASON_CODES = (() => {
  const source = fs.readFileSync(path.join(repoRoot, "services", "Diten.MdmService", "src", "Diten.MdmService.Application",
    "Features", "BrandProductContract", "BrandProductSharedModels.cs"), "utf8");
  const constants = Object.fromEntries([...source.matchAll(/public const string (\w+) = "([a-z_]+)";/g)].map((m) => [m[1], m[2]]));
  const all = source.match(/public static readonly IReadOnlyList<string> All =\s*\[([\s\S]*?)\];/)[1];
  return all.split(",").map((x) => x.trim()).filter(Boolean).map((name) => constants[name]);
})();

/** Runs the shipped index.js with an isolated DOM double and exposes its closure functions. */
const loadRegister = ({ flags = {}, lang = "tr" } = {}) => {
  const L = new Proxy({}, { get: (_, key) => (typeof key === "string" ? `L:${key}` : undefined) });
  const warnings = [];
  const register = { getAttribute: (name) => (flags[name.replace("data-", "")] ? "true" : "false") };
  const document = {
    documentElement: { lang },
    getElementById: (id) => (id === "brandRegister" ? register : null),
    querySelector: () => null,
    addEventListener: () => {}
  };
  const source = read("wwwroot", "assets", "js", "MasterData", "Brands", "index.js")
    .replace("return { init };", "return { init, failureSentence, renderActions, archiveSentence, unresolvedReference, formatDate, buildListOptions };");
  const context = {
    window: { BrandL10n: L },
    document,
    console: { warn: (...args) => warnings.push(args), error: () => {} },
    Intl,
    Date,
    Number,
    String,
    Array,
    URLSearchParams
  };
  vm.runInNewContext(`${source}\n;globalThis.__register = BrandsList;`, context);
  return { register: context.__register, warnings };
};

describe("Brand register — the list component in server mode (item 6)", () => {
  test("stands on the list shell and the list component without selection surfaces", () => {
    const table = read("Views", "MasterData", "Brands", "_DataTable.cshtml");
    expect(table).toContain('partial name="~/Views/Shared/Components/DataTable/_ListShell.cshtml"');
    expect(table).toMatch(/DataMode = "server"/);
    expect(table).toMatch(/HasSelection = false/);
    const js = read("wwwroot", "assets", "js", "MasterData", "Brands", "index.js");
    expect(js).toContain("window.DitenDataTable.createList(buildListOptions())");
    expect(js).not.toContain("new DataTable(");
  });

  test("hands the list component saved-view indexes that are exactly the declared data columns", () => {
    const declared = (read("Views", "MasterData", "Brands", "_DataTable.cshtml").match(/new DataTableColumnViewModel/g) || []).length;
    const { register } = loadRegister();
    const options = register.buildListOptions();
    expect(options.dataMode).toBe("server");
    expect(options.savedView.moduleKey).toBe("MasterData");
    expect(options.savedView.pageKey).toBe("Brands");
    // control + every declared column + actions
    expect(options.config.columns.length).toBe(declared + 2);
    expect([...options.savedView.saveViewColumnIndexes]).toEqual(Array.from({ length: declared }, (_, i) => i + 1));
    expect([...options.toolbar.exportColumns]).toEqual([...options.savedView.saveViewColumnIndexes]);
  });

  test("sends as a sort key only what MDM sorts by", () => {
    const sortable = ["brandCode", "brandName", "brandStatus", "effectiveFrom", "effectiveTo", "isArchived", "updatedAt"];
    const backend = fs.readFileSync(path.join(repoRoot, "services", "Diten.MdmService", "src", "Diten.MdmService.Application",
      "Features", "Brand", "Queries", "GetBrandListQuery.cs"), "utf8");
    sortable.forEach((field) => expect(backend).toContain(`"${field}"`));
    const { register } = loadRegister();
    const options = register.buildListOptions();
    const unorderable = new Set(options.config.columnDefs.filter((d) => d.orderable === false)
      .flatMap((d) => (Array.isArray(d.targets) ? d.targets : [d.targets])));
    options.config.columns.forEach((column, index) => {
      const target = index === options.config.columns.length - 1 ? -1 : index;
      if (unorderable.has(index) || unorderable.has(target)) return;
      expect(sortable, `${column.data} is sent as a sort key MDM does not accept`).toContain(column.data);
    });
  });

  test("labels every export entry of the list as current page only", () => {
    const js = read("wwwroot", "assets", "js", "MasterData", "Brands", "index.js");
    expect(js).toContain("initComplete: function () { labelExportsCurrentPageOnly(this.api()); }");
    LANGUAGES.forEach((language) => expect(resxValue(language, "CurrentPageOnly"), language).toBeTruthy());
  });
});

describe("Brand register — refusals in the reader's language (item 7)", () => {
  test("knows every MDM brand/product reason code", () => {
    expect(REASON_CODES.length).toBeGreaterThan(20);
    expect(REASON_CODES).toContain("version_conflict");
  });

  test.each(REASON_CODES)("%s has a sentence in all seven languages", (code) => {
    const english = resxValue("en", `Reason_${code}`);
    LANGUAGES.forEach((language) => {
      const value = resxValue(language, `Reason_${code}`);
      expect(value, `Reason_${code} missing in ${language}`).toBeTruthy();
      expect(value).not.toContain(code);
      if (language !== "en") expect(value, `Reason_${code} in ${language} is still English`).not.toBe(english);
    });
  });

  test("every status sentence the page falls back to exists in all seven languages and reaches the page", () => {
    const bridge = read("Views", "MasterData", "Brands", "_IndexL10n.cshtml");
    ["400", "403", "404", "409", "502", "503", "504", "Other"].forEach((status) => {
      expect(bridge).toContain(`"RefusedStatus_${status}"`);
      LANGUAGES.forEach((language) => expect(resxValue(language, `RefusedStatus_${status}`), `${status} ${language}`).toBeTruthy());
    });
  });

  test("shows the proxy's sentence, falls to the status sentence, and warns only for an unmapped refusal", () => {
    const { register, warnings } = loadRegister();
    expect(register.failureSentence({ errors: ["Bir cümle."] }, 409)).toBe("Bir cümle.");
    expect(warnings).toHaveLength(0);
    expect(register.failureSentence({ errors: [], unmapped: true }, 409)).toBe("L:RefusedStatus_409");
    expect(warnings).toHaveLength(1);
    expect(JSON.stringify(warnings[0])).not.toContain("errors");
  });
});

describe("Brand register — the archive (items 3, 11)", () => {
  test("the confirmation says it cannot be undone and how many products stay in use", () => {
    const { register } = loadRegister();
    expect(register.archiveSentence(4)).toBe("L:ArchiveBrandIrreversible L:ArchiveBrandLiveProducts");
    expect(register.archiveSentence(0)).toBe("L:ArchiveBrandIrreversible L:ArchiveBrandNoLiveProducts");
    LANGUAGES.forEach((language) => {
      expect(resxValue(language, "ArchiveBrandIrreversible"), language).toBeTruthy();
      expect(resxValue(language, "ArchiveBrandLiveProducts"), language).toContain("{0}");
    });
  });

  test("the archive request carries the page's anti-forgery token, on the list and on the detail", () => {
    ["index.js", "details.js"].forEach((file) => {
      const js = read("wwwroot", "assets", "js", "MasterData", "Brands", file);
      expect(js, file).toMatch(/RequestVerificationToken: antiForgeryToken\(\)/);
    });
    expect(read("Views", "MasterData", "Brands", "Index.cshtml")).toContain("@Html.AntiForgeryToken()");
    expect(read("Views", "MasterData", "Brands", "Details.cshtml")).toContain("@Html.AntiForgeryToken()");
  });
});

describe("Brand register — references and dates (items 8, 13)", () => {
  test("a reference with no master reads as could-not-be-resolved, never as its id", () => {
    const { register } = loadRegister();
    const id = "b0b0b0b0-1111-2222-3333-444444444444";
    const rendered = register.unresolvedReference(id);
    expect(rendered).toContain("L:Unresolved");
    expect(rendered).not.toContain(id);
    expect(register.unresolvedReference(null)).toBe("—");
  });

  test("no Brand view prints a reference or a person as an id", () => {
    ["Details.cshtml", "_Form.cshtml", "_DataTable.cshtml"].forEach((file) => {
      const view = read("Views", "MasterData", "Brands", file);
      expect(view, file).not.toMatch(/@Show\(b\.(OwnerCompanyId|BusinessUnitId|TherapeuticAreaId|CreatedBy|UpdatedBy|ArchivedBy)\)/);
      expect(view, file).not.toMatch(/<input asp-for="(BusinessUnitId|TherapeuticAreaId)" class="form-control"/);
    });
  });

  test("dates are written in the interface language, not the browser's", () => {
    ["index.js", "details.js"].forEach((file) => {
      const js = read("wwwroot", "assets", "js", "MasterData", "Brands", file);
      expect(js, file).not.toMatch(/toLocale(Date)?String\(\)/);
      expect(js, file).toContain("document.documentElement.lang");
    });
    const { register } = loadRegister({ lang: "tr" });
    expect(register.formatDate("2026-03-05T00:00:00Z")).toBe(new Intl.DateTimeFormat("tr", { dateStyle: "medium" }).format(new Date("2026-03-05T00:00:00Z")));
  });

  test("the row menu offers edit and archive only to whom the controller allows, and never on an archived brand", () => {
    const row = { brandId: "11111111-1111-1111-1111-111111111111", isArchived: false };
    const none = loadRegister().register.renderActions(row);
    expect(none).not.toContain("/Edit");
    expect(none).not.toContain('data-row-action="archive"');
    const all = loadRegister({ flags: { "can-update": true, "can-archive": true } }).register;
    expect(all.renderActions(row)).toContain("/Edit");
    expect(all.renderActions(row)).toContain('data-row-action="archive"');
    expect(all.renderActions({ ...row, isArchived: true })).not.toContain('data-row-action="archive"');
  });

  test("ships one key set in all seven languages", () => {
    const keys = (language) => [...resx(language).matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]).sort();
    const english = keys("en");
    LANGUAGES.forEach((language) => expect(keys(language), language).toEqual(english));
  });
});
