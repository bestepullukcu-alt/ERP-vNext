const fs = require("fs");
const path = require("path");

/*
 * MOD-0024 Phase 5 — the field-definition management surface.
 *
 * THE RULE THIS FILE EXISTS FOR: a raw resource key must never reach the screen. A tenant administrator cannot
 * add a line to our resx files, so a tenant-defined field carries the administrator's own words and a
 * system-defined one carries a key we ship translations for. Neither is a fallback for the other, and the CODE is
 * never printed as a label — that exact defect has shipped in this codebase before (task titles).
 *
 * The screen is a COPY of GoldenReferenceCompact by instruction: two management screens shipped in one week have
 * to read as one product. The structural assertions below pin that copy, so a later edit that quietly diverges
 * from the reference is visible.
 */
const ROOT = path.resolve(__dirname, "..");
const VIEWS = path.join(ROOT, "Views", "Tasks", "FieldDefinitions");
const REFERENCE = path.join(ROOT, "Views", "DevEnablement", "GoldenReferenceCompact");
const RESX = path.join(ROOT, "Resources", "Views", "Tasks", "FieldDefinitions");
const JS = path.join(ROOT, "wwwroot", "assets", "js", "Tasks", "FieldDefinitions");

const read = (file) => fs.readFileSync(file, "utf8");

describe("MOD-0024 field definitions: the screen follows the reference", () => {
  it("ships the same eight files the reference does", () => {
    // The instruction was "file structure identical to that folder". A missing partial is a missing contract
    // marker, and the golden-reference checks key off those markers.
    ["Index.cshtml", "_DataTable.cshtml", "_Filter.cshtml", "_Form.cshtml",
     "Create.cshtml", "Edit.cshtml", "Details.cshtml", "_IndexL10n.cshtml"]
      .forEach((file) => {
        expect(fs.existsSync(path.join(VIEWS, file))).toBe(true);
        expect(fs.existsSync(path.join(REFERENCE, file))).toBe(true);
      });
  });

  it("carries the reference's DataTable contract markers", () => {
    const table = read(path.join(VIEWS, "_DataTable.cshtml"));

    expect(table).toContain('id="skeleton-loader"');
    expect(table).toContain('data-dt-standard="v2"');
    expect(table).toContain('id="dt-taskfielddefinitions"');
    expect(table).toContain("dt-checkboxes-select-all");
  });

  it("lists exactly the columns the ticket named", () => {
    const table = read(path.join(VIEWS, "_DataTable.cshtml"));

    ["Code", "Label", "ValueType", "Section", "Required"].forEach((key) => {
      expect(table).toContain(`@Localizer["${key}"]`);
    });
    expect(table).toContain('@SharedLocalizer["Status"]');
  });

  it("creates and edits on a FULL PAGE, never an offcanvas", () => {
    // Compact rather than Slim was chosen for exactly this reason: the field count does not fit an offcanvas.
    const create = read(path.join(VIEWS, "Create.cshtml"));
    const edit = read(path.join(VIEWS, "Edit.cshtml"));

    [create, edit].forEach((page) => {
      expect(page).toContain('Layout = "_LayoutTenantShell"');
      expect(page).toContain('<partial name="~/Views/Tasks/FieldDefinitions/_Form.cshtml"');
      expect(page).not.toContain("offcanvas");
    });
  });

  it("uses the reference's preview vocabulary on the details page", () => {
    const details = read(path.join(VIEWS, "Details.cshtml"));
    const reference = read(path.join(REFERENCE, "Details.cshtml"));

    ["backbone-preview-section", "backbone-preview-field", "backbone-preview-label", "backbone-preview-value",
     "text-uppercase text-heading fw-semibold"].forEach((token) => {
      expect(details).toContain(token);
      // …and the token is genuinely the reference's, not one this file invented.
      expect(reference).toContain(token);
    });
    expect(details).toContain('<i class="bx');
  });

  it("styles through classes only — no inline style, no element.style (FG-003)", () => {
    const files = fs.readdirSync(VIEWS).filter((f) => f.endsWith(".cshtml")).map((f) => read(path.join(VIEWS, f)));
    const scripts = fs.readdirSync(JS).map((f) => read(path.join(JS, f)));

    files.forEach((content) => expect(content).not.toMatch(/\sstyle\s*=\s*"/));
    scripts.forEach((content) => expect(content).not.toMatch(/\.style\.[a-zA-Z]/));
  });
});

describe("MOD-0024 field definitions: no raw key reaches the screen", () => {
  it("renders a TENANT definition from its own words", () => {
    const { renderLabel } = loadLabelRenderer({});

    expect(renderLabel({ code: "regulatory.phase", labelText: "Mevzuat Aşaması" }))
      .toBe("Mevzuat Aşaması");
  });

  it("renders a SYSTEM definition through the dictionary", () => {
    const { renderLabel } = loadLabelRenderer({ Tasks_Field_RegulatoryPhase: "Regulatory Phase" });

    expect(renderLabel({ code: "regulatory.phase", labelResourceKey: "Tasks_Field_RegulatoryPhase" }))
      .toBe("Regulatory Phase");
  });

  it("never prints the CODE as a label", () => {
    /*
     * The defect in its purest form. A definition with no label source cannot be created — the server refuses
     * it — but if one ever existed, falling back to the code would put "regulatory.phase" where a heading
     * belongs, which is what this whole two-source split exists to prevent.
     */
    const { renderLabel } = loadLabelRenderer({});

    expect(renderLabel({ code: "regulatory.phase" })).toBe("");
  });

  it("escapes a tenant's own words", () => {
    // Tenant text is CONTENT, and content is untrusted: it is the administrator's own words, not our markup.
    const { renderLabel } = loadLabelRenderer({});

    expect(renderLabel({ labelText: '<img src=x onerror="alert(1)">' }))
      .not.toContain("<img");
  });

  it("shows the untranslated key only when the dictionary genuinely lacks it", () => {
    // Non-vacuity for the system case: if renderLabel returned the key unconditionally the second test would
    // still pass. This pins that the dictionary is actually consulted.
    const { renderLabel } = loadLabelRenderer({ Tasks_Field_Other: "Other" });

    expect(renderLabel({ labelResourceKey: "Tasks_Field_RegulatoryPhase" }))
      .toBe("Tasks_Field_RegulatoryPhase");
  });

  it("prefers the tenant's words when a row somehow carries both", () => {
    // The server refuses both-at-once, so this is defence in depth: the tenant's own words are the safer of the
    // two to show, because a key is guaranteed to be meaningless to them.
    const { renderLabel } = loadLabelRenderer({ K: "System label" });

    expect(renderLabel({ labelResourceKey: "K", labelText: "Kendi etiketim" })).toBe("Kendi etiketim");
  });

  it("shows the tenant's own words on the details page rather than the code", () => {
    const details = read(path.join(VIEWS, "Details.cshtml"));
    const labelBlock = details.slice(details.indexOf('@Localizer["Label"]'), details.indexOf("ValueSection"));

    expect(labelBlock).toContain("Model.LabelText");
    expect(labelBlock).toContain("Model.LabelResourceKey");
    // The code is NOT the fallback — the block renders "-" instead.
    expect(labelBlock).not.toContain("Model.Code");
  });
});

describe("MOD-0024 field definitions: the form states the rules it cannot enforce", () => {
  const form = () => read(path.join(VIEWS, "_Form.cshtml"));

  it("makes the code read-only on edit and says why", () => {
    const content = form();

    expect(content).toContain('<input asp-for="Code" class="form-control" readonly />');
    expect(content).toContain('@Localizer["CodeImmutableHint"]');
  });

  it("tells the reader the section cap exists before they hit it", () => {
    expect(form()).toContain('@Localizer["SectionLimitHint"]');
  });

  it("says plainly that classification decides nothing today", () => {
    // BL-024 is not this package. Metadata that LOOKS protective and protects nothing is more dangerous than
    // none, so the screen says so out loud.
    expect(form()).toContain('@Localizer["ClassificationNotEnforcedHint"]');
  });

  it("offers both label fields, with the tenant one first", () => {
    const content = form();

    expect(content.indexOf('asp-for="LabelText"')).toBeGreaterThan(-1);
    expect(content.indexOf('asp-for="LabelResourceKey"')).toBeGreaterThan(-1);
    // The tenant field leads: it is the one a tenant administrator can actually use.
    expect(content.indexOf('asp-for="LabelText"'))
      .toBeLessThan(content.indexOf('asp-for="LabelResourceKey"'));
  });
});

describe("MOD-0024 field definitions: the bulk retire actually reaches a server", () => {
  /*
   * THE DEFECT: the button was live — checkboxes rendered, the bar appeared, the confirm fired — and the request
   * 404'd. Different from WorkCenterNext's dead bulk path (BL-039), where no checkbox was ever produced: here the
   * whole path worked and only the endpoint was missing.
   *
   * These drive the shipped handler, extracted from its own source, so a change to the request shape is a change
   * to this test.
   */
  const bulkHandler = () => {
    const source = read(path.join(JS, "index.js"));
    const start = source.indexOf("const bulkOptions");
    const end = source.indexOf("const reloadWithSuccessToast");
    expect(start).toBeGreaterThan(-1);
    expect(end).toBeGreaterThan(start);

    const calls = { fetches: [], toasts: [], reloads: [], pending: [] };
    const scope = {
      apiUrl: "/Tasks/api",
      L: { BulkDeleteConfirm: "{0} kayıt silinecek", Delete: "Sil", ErrorOccurred: "Hata" },
      getAuthHeaders: () => ({ "Content-Type": "application/json" }),
      /*
       * Fires the confirmation immediately and REMEMBERS the promise. The shell's showConfirm is fire-and-forget,
       * so the script's own `await` chain lives inside the callback — a harness that dropped it would assert on
       * an empty world and pass for the wrong reason.
       */
      showConfirm: (_text, onConfirm) => { calls.pending.push(onConfirm()); },
      showToast: (message, kind) => calls.toasts.push({ message, kind }),
      reloadWithSuccessToast: (key, value) => calls.reloads.push({ key, value }),
      fetchImpl: null
    };

    // eslint-disable-next-line no-new-func
    const factory = new Function(
      "apiUrl", "L", "getAuthHeaders", "window", "fetch", "reloadWithSuccessToast",
      `${source.slice(start, end)} return bulkOptions;`);

    return {
      calls,
      build: (fetchImpl) => factory(
        scope.apiUrl, scope.L, scope.getAuthHeaders,
        { showConfirm: scope.showConfirm, showToast: scope.showToast },
        async (...args) => { calls.fetches.push(args); return fetchImpl(...args); },
        (key, value) => calls.reloads.push({ key, value }))
    };
  };

  it("POSTs an envelope to the bulk-delete endpoint", async () => {
    /*
     * The shape decision, pinned. The script was copied sending a bare array over DELETE; MOD-0024's controller
     * already had POST bulk-delete with { ids }, and two bulk shapes in one controller costs more than these
     * few lines.
     */
    const harness = bulkHandler();
    const options = harness.build(async () => ({
      ok: true, json: async () => ({ data: { deactivated: 2, notFound: 0 } })
    }));

    await options.onBulkAction.delete({ ids: ["a", "b"] });
    await Promise.all(harness.calls.pending);

    const [url, init] = harness.calls.fetches[0];
    expect(url).toBe("/Tasks/api/field-definitions/bulk-delete");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ ids: ["a", "b"] });
  });

  it("tells the user what HAPPENED, not what was asked for", async () => {
    // Three selected, two actually retired. Echoing ids.length would repeat the server's own honesty back as a
    // lie — the exact trap the counted response exists to close.
    const harness = bulkHandler();
    const options = harness.build(async () => ({
      ok: true, json: async () => ({ data: { deactivated: 2, notFound: 1 } })
    }));

    await options.onBulkAction.delete({ ids: ["a", "b", "c"] });
    await Promise.all(harness.calls.pending);

    expect(harness.calls.reloads).toEqual([{ key: "BulkDeleteSuccess", value: "2" }]);
    expect(harness.calls.toasts).toEqual([]);
  });

  it("falls back to the selected count only when the server says nothing", async () => {
    // Non-vacuity for the test above: a client that ALWAYS echoed ids.length would pass it if the server
    // happened to agree. Here the payload carries no count and the old behaviour is the safe floor.
    const harness = bulkHandler();
    const options = harness.build(async () => ({ ok: true, json: async () => ({}) }));

    await options.onBulkAction.delete({ ids: ["a", "b", "c"] });
    await Promise.all(harness.calls.pending);

    expect(harness.calls.reloads).toEqual([{ key: "BulkDeleteSuccess", value: "3" }]);
  });

  it("surfaces a refusal instead of claiming success", async () => {
    // What live did on the 404, and still must do on any real refusal — the difference is that there is now a
    // success path for it to fail away from.
    const harness = bulkHandler();
    const options = harness.build(async () => ({ ok: false, status: 404, json: async () => ({}) }));

    await options.onBulkAction.delete({ ids: ["a"] });
    await Promise.all(harness.calls.pending);

    expect(harness.calls.reloads).toEqual([]);
    expect(harness.calls.toasts[0].kind).toBe("error");
  });

  it("sends nothing at all for an empty selection", async () => {
    const harness = bulkHandler();
    const options = harness.build(async () => ({ ok: true, json: async () => ({}) }));

    await options.onBulkAction.delete({ ids: [] });

    expect(harness.calls.fetches).toEqual([]);
  });

  it("keeps the bulk bar's declared endpoint in step with the fetch", () => {
    // Two places name the endpoint — the view model and the script. They drifted once already (that is this
    // ticket), so the pairing is asserted rather than assumed.
    const index = read(path.join(VIEWS, "Index.cshtml"));

    expect(index).toContain('Endpoint = "/Tasks/api/field-definitions/bulk-delete"');
    expect(index).toContain('Method = "POST"');
    expect(read(path.join(JS, "index.js"))).toContain("field-definitions/bulk-delete");
  });
});

describe("MOD-0024 field definitions: seven languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

  const entries = (locale) => {
    const xml = read(path.join(RESX, `TaskFieldDefinitionsIndex.${locale}.resx`));
    const map = {};
    const pattern = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
    let match;
    while ((match = pattern.exec(xml)) !== null) { map[match[1]] = match[2]; }
    return map;
  };

  it("ships a resx for every supported tenant language", () => {
    LOCALES.forEach((locale) => {
      expect(fs.existsSync(path.join(RESX, `TaskFieldDefinitionsIndex.${locale}.resx`))).toBe(true);
    });
  });

  it("has an identical key set across all seven", () => {
    const base = Object.keys(entries("en")).sort();
    expect(base.length).toBeGreaterThan(0);

    LOCALES.filter((l) => l !== "en").forEach((locale) => {
      expect(Object.keys(entries(locale)).sort()).toEqual(base);
    });
  });

  it("translates rather than leaving English in place", () => {
    const en = entries("en");
    ["FieldDefinitionsTitle", "LabelText", "SectionLimitHint", "ClassificationNotEnforcedHint"].forEach((key) => {
      ["tr", "ru", "zh", "ar"].forEach((locale) => {
        expect(entries(locale)[key]).toBeTruthy();
        expect(entries(locale)[key]).not.toBe(en[key]);
      });
    });
  });

  it("covers every key the views actually ask for", () => {
    /*
     * The gate that matters: a Localizer call with no resx entry renders the KEY. Scanning the views is what
     * turns "we translated it" into "we translated what is used".
     */
    const used = new Set();
    fs.readdirSync(VIEWS)
      .filter((f) => f.endsWith(".cshtml"))
      .forEach((file) => {
        const content = read(path.join(VIEWS, file));
        for (const m of content.matchAll(/@Localizer\["([A-Za-z0-9_]+)"\]/g)) { used.add(m[1]); }
      });

    expect(used.size).toBeGreaterThan(20);
    const en = entries("en");
    const missing = [...used].filter((key) => !(key in en));
    expect(missing).toEqual([]);
  });

  it("covers every value-type option the form offers", () => {
    // Eleven types, and each one is a dropdown line somebody has to read.
    const en = entries("en");
    ["Text", "Number", "Currency", "Percentage", "Date", "DateTime", "Boolean", "Status", "Person",
     "Reference", "Link"].forEach((type) => {
      expect(en[`ValueType${type}`]).toBeTruthy();
    });
  });
});

/**
 * Loads the page script's label renderer against a given dictionary.
 *
 * The script is an IIFE that boots on DOMContentLoaded and needs the DataTable stack, so it is not loadable
 * whole in jsdom. The renderer is extracted from the file's own source instead — so this exercises the shipped
 * code rather than a copy of it, and a change to that function is a change to this test.
 */
function loadLabelRenderer(dictionary) {
  const source = read(path.join(JS, "index.js"));
  const start = source.indexOf("const renderLabel");
  const end = source.indexOf("const syncL10n");
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);

  const body = source.slice(start, end);
  // eslint-disable-next-line no-new-func
  const factory = new Function("L", `${body}; return { renderLabel, escapeHtml };`);
  return factory(dictionary);
}
