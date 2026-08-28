const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-047, second half — the DataTable language pack reaches EVERY table, not just WorkCenterNext's.
 *
 * THE DEFECT (CT live, 2026-08-10, Turkish page): /Tasks/FieldDefinitions renders
 *   "No data available in table" · "Showing 0 to 0 of 0 entries"
 * The six Dt* strings exist in SharedResource in all seven languages. What was missing was the DELIVERY PATH:
 * BL-047 seeded them onto `window.L10n` from the WorkCenterNext payload alone, so a management screen — its own
 * page, its own l10n payload — got nothing. That is the same shape as the first half of BL-047: the supply was
 * fixed, the consumer was still reading a different dictionary, and the screen stayed English.
 *
 * SO THE TEST IS ON THE CONSUMER. Every assertion below goes through `DtDefaults.create()` — the real function
 * every one of the 61 table pages calls — and reads the config DataTables will actually be handed. "The payload
 * contains the key" is NOT evidence; it was true the whole time the screen was English.
 *
 * DECISION: (b) — dt-defaults itself takes its language defaults from ONE shared payload the layout emits. Not
 * (a) — a partial each management page includes — because "each page must remember" IS the defect: the screen
 * measured today is the second page to forget, and a third and fourth would follow.
 */

const SEED_ID = "datatable-l10n";

/** The six strings, as the shared payload carries them. Turkish, because that is where the defect was seen. */
const TR = {
  DtInfo: "_TOTAL_ kayıttan _START_ - _END_ arasındaki kayıtlar gösteriliyor",
  DtInfoEmpty: "0 kayıttan 0 - 0 arasındaki kayıtlar gösteriliyor",
  DtInfoFiltered: "(_MAX_ kayıt içerisinden bulunan)",
  DtEmptyTable: "Tabloda veri bulunmuyor",
  DtNoRecords: "Eşleşen kayıt bulunamadı",
  DtZeroRecords: "Eşleşen kayıt bulunamadı"
};

/** Writes the shared payload into the document, exactly as the layout partial does. */
const seedSharedPayload = (values) => {
  const el = document.createElement("script");
  el.id = SEED_ID;
  el.type = "application/json";
  el.textContent = JSON.stringify(values);
  document.head.appendChild(el);
};

/*
 * A REAL deep extend. dt-defaults merges with `$.extend(true, {}, baseConfig, userConfig)`, and a shallow stub
 * would drop `language` wholesale — every assertion below would then be measuring the stub, not the code.
 */
const deepExtend = (deep, target, ...sources) => {
  if (deep !== true) { return Object.assign(target, deep, ...sources); }
  sources.forEach((source) => {
    Object.keys(source || {}).forEach((key) => {
      const value = source[key];
      if (value && typeof value === "object" && !Array.isArray(value)) {
        target[key] = deepExtend(true, Object.assign({}, target[key]), value);
      } else {
        target[key] = value;
      }
    });
  });
  return target;
};

const bootDtDefaults = () => {
  document.head.innerHTML = "";
  document.body.innerHTML = "";
  delete window.DtDefaults;
  delete window.L10n;

  window.DataTable = { Responsive: { display: { modal: () => {} } } };
  window.$ = window.jQuery = () => ({
    removeClass: () => window.$(), addClass: () => window.$(), css: () => window.$(),
    each: () => {}, find: () => window.$(), contents: () => window.$(), unwrap: () => window.$(),
    filter: () => window.$(), fadeOut: () => {}, fadeIn: () => {}, length: 0
  });
  window.$.extend = deepExtend;
  window.$.map = (arr, cb) => arr.map(cb);
  window.$.ajax = () => {};
};

/** What DataTables is actually handed. */
const language = () => window.DtDefaults.create({}).language;

describe("BL-047b: the shared language payload reaches the table config", () => {
  beforeEach(bootDtDefaults);

  it("localizes the empty-table and info strings on a page that has no L10n of its own", () => {
    /*
     * THE measured screen, reduced: a management page whose own payload knows nothing about Dt* keys. Before the
     * delivery path existed this produced DataTables' English defaults, which is what the owner read on screen.
     */
    seedSharedPayload(TR);
    loadScript("wwwroot/assets/js/dt-defaults.js");

    const l = language();

    expect(l.emptyTable).toBe(TR.DtEmptyTable);
    expect(l.info).toBe(TR.DtInfo);
    expect(l.infoEmpty).toBe(TR.DtInfoEmpty);
    expect(l.infoFiltered).toBe(TR.DtInfoFiltered);
    expect(l.zeroRecords).toBe(TR.DtZeroRecords);
  });

  it("leaves NO Dt slot unfilled, which is where the English came from", () => {
    /*
     * Stated as presence, not as absence of English — deliberately. The English sentences the owner read are
     * DataTables' own internal defaults, which appear when a slot is left undefined; they are never written into
     * this config. So "the rendered config contains no English" is true even while the screen is fully English,
     * and asserting it would have been the file's third vacuity this round.
     */
    seedSharedPayload(TR);
    loadScript("wwwroot/assets/js/dt-defaults.js");

    const l = language();

    ["emptyTable", "info", "infoEmpty", "infoFiltered", "zeroRecords"].forEach((slot) => {
      expect(l[slot], `${slot} left undefined → DataTables falls back to English`).toBeTruthy();
    });
  });

  it("lets a page's OWN wording win over the shared default", () => {
    /*
     * The precedence BL-047 already established for WorkCenterNext, kept: a screen that wants its own sentence
     * keeps it. Reversing this would silently overwrite wording somebody chose on purpose.
     */
    seedSharedPayload(TR);
    loadScript("wwwroot/assets/js/dt-defaults.js");
    window.L10n = { DtEmptyTable: "Bu ekranda henüz kural yok" };

    expect(language().emptyTable).toBe("Bu ekranda henüz kural yok");
  });

  it("falls back to the shared default for the keys a page did NOT override", () => {
    // Half a dictionary must not disable the other half — that is how one localized string and five English
    // ones end up on the same table.
    seedSharedPayload(TR);
    loadScript("wwwroot/assets/js/dt-defaults.js");
    window.L10n = { DtEmptyTable: "Bu ekranda henüz kural yok" };

    expect(language().info).toBe(TR.DtInfo);
  });

  it("reads the payload LATE, so the layout's order of script tags cannot break it", () => {
    /*
     * Non-obvious and load-bearing. dt-defaults.js is a <script> in the layout; a page's table is built later.
     * If the payload were read at load time, moving one tag would silently restore the English defaults — the
     * exact class of failure that made this a live defect rather than a caught one.
     */
    loadScript("wwwroot/assets/js/dt-defaults.js");
    seedSharedPayload(TR);

    expect(language().emptyTable).toBe(TR.DtEmptyTable);
  });

  it("says nothing when there is nothing to say", () => {
    /*
     * NON-VACUITY for the whole file. With no payload and no L10n, DataTables' own defaults must remain — if
     * this test could not tell the difference, every assertion above would pass against a hard-coded string.
     */
    loadScript("wwwroot/assets/js/dt-defaults.js");

    const l = language();

    expect(l.emptyTable).toBeUndefined();
    expect(l.info).toBeUndefined();
  });

  it("survives a payload that is not valid JSON", () => {
    // A broken payload must cost the page its translations, never its table.
    const el = document.createElement("script");
    el.id = SEED_ID;
    el.type = "application/json";
    el.textContent = "{ this is not json";
    document.head.appendChild(el);
    loadScript("wwwroot/assets/js/dt-defaults.js");

    expect(() => language()).not.toThrow();
  });
});

/*
 * The one link a jsdom test cannot execute: Razor writing the payload, and the layouts rendering it.
 *
 * These are SOURCE assertions and are named as such — they are weaker than the consumer tests above and are
 * here only because the alternative is no coverage at all on the wiring that made this a live defect twice.
 * The live steps in the backlog record are what actually close it.
 */
const read = (relative) => fs.readFileSync(path.join(__dirname, "..", relative), "utf8");

describe("BL-047b: the wiring that carries the payload to the page", () => {
  const LAYOUTS = [
    "Views/Shared/_LayoutTenantShell.cshtml",
    "Views/Shared/_LayoutPlatformAdmin.cshtml"
  ];

  it("emits every key dt-defaults reads, and no others", () => {
    // Supply and demand named in one assertion: a key added to one side and forgotten on the other is exactly
    // how BL-047's first half shipped with the screen still English.
    const partial = read("Views/Shared/_DataTableL10n.cshtml");
    const consumer = read("wwwroot/assets/js/dt-defaults.js");

    // Only the declared key list, not the prose around it — a comment naming a key is not an emission.
    const declaration = (partial.match(/dtKeys\s*=\s*\[([^\]]+)\]/) || [])[1] || "";
    const emitted = (declaration.match(/"Dt[A-Za-z]+"/g) || []).map((k) => k.replace(/"/g, "")).sort();
    const read_ = Array.from(new Set(consumer.match(/dtText\('(Dt[A-Za-z]+)'\)/g) || []))
      .map((m) => m.replace(/dtText\('|'\)/g, "")).sort();

    expect(emitted).toHaveLength(6);
    expect(emitted).toEqual(read_);
  });

  it("skips a key the resx does not have, instead of printing its name", () => {
    // A missing resource resolves to its own key. Writing that through puts "DtInfo" on screen — unreadable in
    // all seven languages, and worse than the English it replaced.
    expect(read("Views/Shared/_DataTableL10n.cshtml")).toContain("IsResourceNotFound");
  });

  LAYOUTS.forEach((layout) => {
    it(`renders the payload from ${path.basename(layout)}, before dt-defaults`, () => {
      /*
       * Both layouts, because the defect is not tenant-specific. Order matters less than it looks — the payload
       * is read lazily — but a layout that never renders it at all leaves every one of its tables English.
       */
      const source = read(layout);
      const partialAt = source.indexOf("_DataTableL10n.cshtml");
      const scriptAt = source.indexOf("assets/js/dt-defaults.js");

      expect(partialAt).toBeGreaterThan(-1);
      expect(scriptAt).toBeGreaterThan(-1);
      expect(partialAt).toBeLessThan(scriptAt);
    });
  });
});
