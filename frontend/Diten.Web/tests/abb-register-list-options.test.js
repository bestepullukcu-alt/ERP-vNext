const fs = require("fs");
const path = require("path");
const vm = require("vm");

/*
 * WP-MDM-ABB-LIST-01 (BL-440) — the ABB register stands on the list component in SERVER mode. Measured on the page's
 * own createList options (built by the production script) and the proxy's own sort whitelist (read from the
 * production controller), so neither side keeps a copy of the other.
 */
const WEB = path.resolve(__dirname, "..");
const SCRIPT = path.join(WEB, "wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js");
const CONTROLLER = fs.readFileSync(path.join(WEB, "Controllers/ProductAbbreviationRegisterController.cs"), "utf8");

const proxyWhitelist = () => {
  const block = CONTROLLER.slice(CONTROLLER.indexOf("SortableListColumns ="), CONTROLLER.indexOf("};", CONTROLLER.indexOf("SortableListColumns =")));
  return [...block.matchAll(/"([A-Za-z]+)"/g)].map((match) => match[1]);
};

let options;

beforeAll(() => {
  vm.runInThisContext(`${fs.readFileSync(SCRIPT, "utf8")}\n;globalThis.__abbRegisterList = ProductAbbreviationRegisterList;`,
    { filename: SCRIPT });
  options = globalThis.__abbRegisterList.buildListOptions();
});

describe("ABB register list options", () => {
  it("declares server mode to the list component", () => {
    expect(options.dataMode).toBe("server");
    expect(options.ajax.url).toBe("/MDM/ProductAbbreviationRegister/api/list");
  });

  it("asks the server only for sorts its whitelist accepts", () => {
    const whitelist = proxyWhitelist();
    expect(whitelist).toEqual(["abbreviation", "lifecycleStatus", "version", "retirementPending"]);
    const orderableData = options.config.columns
      .filter((column, index) => !options.config.columnDefs.some((def) => def.orderable === false
        && (def.targets === index || (def.targets === -1 && index === options.config.columns.length - 1))))
      .map((column) => column.data);
    expect(orderableData.length).toBeGreaterThan(0);
    orderableData.forEach((data) => expect(whitelist).toContain(data));
  });

  it("offers no bulk surface (GxP: an abbreviation is never changed in bulk)", () => {
    expect(options.bulk).toBeUndefined();
    expect(options.config.columns.map((column) => column.name)).not.toContain("checkbox");
  });

  it("keeps the saved-view keys, the quick view and the product filter", () => {
    expect(options.savedView.moduleKey).toBe("MasterDataManagement");
    // LIST-FIX1 item 3: versioned — the old eight-column views are carried over to it by the page.
    expect(options.savedView.pageKey).toBe("ProductAbbreviationRegister.v2");
    expect(options.quickView.actionKey).toBe("quickView");
    expect(options.filters.fields.map((field) => field.key)).toEqual(["globalProductText", "globalProductId"]);
    expect(Object.keys(options.actions.onRowAction).sort()).toEqual(["cancel", "correct", "retire", "taskCenter"]);
  });

  it.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])(
    "has every shared word the page's l10n bridge reads in %s (the list vocabulary included)", (lang) => {
      const partial = fs.readFileSync(path.join(WEB, "Views/MDM/ProductAbbreviationRegister/_IndexL10n.cshtml"), "utf8");
      const shared = fs.readFileSync(path.join(WEB, `Resources/SharedResource.${lang}.resx`), "utf8");
      const keys = [...partial.matchAll(/SharedLocalizer\["([A-Za-z]+)"\]\.Value/g)].map((match) => match[1]);
      // LIST-FIX1 item 7: RecordSaved — the list component's Save View toast.
      ["Active", "Passive", "Edit", "ViewDetails", "Import", "Status", "RecordSaved"].forEach((key) => expect(keys).toContain(key));
      for (const key of keys) {
        const match = shared.match(new RegExp(`<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`));
        expect([key, match ? match[1].trim() : null]).toEqual([key, expect.stringMatching(/\S/)]);
      }
    });

  it.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])(
    "has the page's own new words in %s: the truncation notice and the saved view that was not carried over", (lang) => {
      const partial = fs.readFileSync(path.join(WEB, "Views/MDM/ProductAbbreviationRegister/_IndexL10n.cshtml"), "utf8");
      const index = fs.readFileSync(path.join(WEB, "Views/MDM/ProductAbbreviationRegister/Index.cshtml"), "utf8");
      const page = fs.readFileSync(path.join(WEB,
        `Resources/Views/MDM/ProductAbbreviationRegister/ProductAbbreviationRegisterIndex.${lang}.resx`), "utf8");
      expect(partial).toContain('SavedViewNotCarried = Localizer["SavedViewNotCarried"].Value');
      expect(partial).toContain('LinkedProductNotOpened = Localizer["LinkedProductNotOpened"].Value');
      expect(index).toContain('@Localizer["ListTruncated"]');
      for (const key of ["ListTruncated", "SavedViewNotCarried", "LinkedProductNotOpened"]) {
        const match = page.match(new RegExp(`<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`));
        expect([key, match ? match[1].trim() : null]).toEqual([key, expect.stringMatching(/\S/)]);
      }
    });
});

