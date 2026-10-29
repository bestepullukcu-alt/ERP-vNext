const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = relativePath => fs.readFileSync(path.join(root, relativePath), "utf8");

const pvgUiFiles = [
  "Controllers/PharmacovigilanceCaseIntakeTriageController.cs",
  "Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/_DataTable.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/_Filter.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/_Form.cshtml",
  "Views/Pharmacovigilance/CaseIntakeTriage/_IndexL10n.cshtml",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/details.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js"
];

const allPvgUiSource = () => pvgUiFiles.map(read).join("\n");
const indexViewSource = () => read("Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml");
const dataTableViewSource = () => read("Views/Pharmacovigilance/CaseIntakeTriage/_DataTable.cshtml");
const detailViewSource = () => read("Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml");
const indexJsSource = () => read("wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js");
const detailJsSource = () => read("wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/details.js");
const formJsSource = () => read("wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js");
const browserJsSource = () => [
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/details.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js"
].map(read).join("\n");
const indexL10nSource = () => read("Views/Pharmacovigilance/CaseIntakeTriage/_IndexL10n.cshtml");
const pvgResourceCultures = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
const pvgResourceDirectory = "Resources/Views/Pharmacovigilance/CaseIntakeTriage";
const pvgResourceFile = culture => `${pvgResourceDirectory}/CaseIntakeTriageIndex.${culture}.resx`;
const safeUiStateKeys = [
  "ControlledBlock",
  "SessionExpired",
  "NotAuthorized",
  "Forbidden",
  "InvalidProxyEndpoint",
  "ReasonCode",
  "Loading",
  "NoRecords"
];
const forbiddenPvgOperations = ["delete", "bulk-delete", "bulk", "archive", "void", "export"];
const forbiddenDownstreamUiPatterns = [
  /MOD-0231|MOD-0232|MOD-0234/i,
  /CaseProcessing|case-processing/i,
  /MeddraCoding|meddra-coding/i,
  /SignalManagement|signal-management/i,
  /\bMedDRA\b[\s\S]*\b(dictionary|import|search)\b/i,
  /\b(dictionary|import|search)\b[\s\S]*\bMedDRA\b/i,
  /\bAI\b|artificial intelligence|artificial-intelligence/i
];
const resourceKeys = culture => new Set(
  [...read(pvgResourceFile(culture)).matchAll(/<data name="([^"]+)"/g)].map(match => match[1])
);
const l10nBridgeKeys = () => new Set(
  [...indexL10nSource().matchAll(/^\s*([A-Za-z][A-Za-z0-9_]*)\s*=/gm)].map(match => match[1])
);
const sharedL10nBridgeKeys = () => new Set(
  [...indexL10nSource().matchAll(/^\s*([A-Za-z][A-Za-z0-9_]*)\s*=\s*SharedLocalizer\[/gm)].map(match => match[1])
);
const browserL10nReferences = () => new Set(
  [...[
    indexJsSource(),
    formJsSource(),
    detailJsSource()
  ].join("\n").matchAll(/\bt\(['"`]([A-Za-z][A-Za-z0-9_]*)['"`]\)/g)].map(match => match[1])
);

describe("PVG case intake triage UI static guardrails", () => {
  it("uses the same-origin MVC proxy instead of direct Gateway or service calls", () => {
    const source = allPvgUiSource();

    expect(source).toContain("/Pharmacovigilance/CaseIntakeTriage/api");
    expect(source).not.toMatch(/https?:\/\/(?:localhost|127\.0\.0\.1):(?:5000|5011)\b/i);
    expect(source).not.toMatch(/fetch\(['"`]\/api\/pv-case-intake-triage/i);
    expect(source).not.toMatch(/fetch\(['"`]\/api\/v1\/pv-case-intake-triage/i);
  });

  it("does not expose forbidden PVG actions in UI routes or scripts", () => {
    const source = allPvgUiSource();

    for (const forbidden of forbiddenPvgOperations) {
      expect(source).not.toMatch(new RegExp(`CaseIntakeTriage/(?:api/)?${forbidden}`, "i"));
      expect(source).not.toMatch(new RegExp(`data-action=["']${forbidden}["']`, "i"));
      expect(source).not.toMatch(new RegExp(`\\b${forbidden}\\s*\\(`, "i"));
    }
  });

  it("does not create browser auth or token forwarding surfaces", () => {
    const source = browserJsSource();

    expect(source).not.toMatch(/\bwindow\.API\b/);
    expect(source).not.toMatch(/\bdocument\.cookie\b/);
    expect(source).not.toMatch(/\bAuthorization\b/);
    expect(source).not.toMatch(/\bBearer\b/);
  });

  it("does not introduce downstream MOD-0231/MOD-0232/MOD-0234 runtime UI exposure", () => {
    const source = allPvgUiSource();

    for (const forbiddenPattern of forbiddenDownstreamUiPatterns) {
      expect(source).not.toMatch(forbiddenPattern);
    }
  });

  it("keeps explicit empty error and loading state hooks", () => {
    const source = allPvgUiSource();

    expect(source).toContain("pvg-list-status");
    expect(source).toContain("pvg-detail-status");
    expect(source).toContain("showLoading");
    expect(source).toContain("setCommandDisabled");
    expect(source).toContain("NoRecords");
    expect(source).toContain("ErrorOccurred");
  });

  it("declares accessible alert and status regions for list and detail surfaces", () => {
    const indexView = indexViewSource();
    const detailView = detailViewSource();

    expect(indexView).toMatch(/id="pvg-list-alert"[^>]*role="alert"/);
    expect(indexView).toMatch(/id="pvg-list-status"[^>]*aria-live="polite"/);
    expect(detailView).toMatch(/id="pvg-detail-alert"[^>]*role="alert"/);
    expect(detailView).toMatch(/id="pvg-detail-status"[^>]*aria-live="polite"/);
    expect(detailView).toMatch(/id="pvgDetailStatus"[^>]*aria-live="polite"/);
  });

  it("keeps list loading hooks wired for success and error paths", () => {
    const source = indexJsSource();

    expect(source).toMatch(/preXhr\.dt[\s\S]*showLoading\(\)[\s\S]*setListStatus\(t\('Loading'\)\)/);
    expect(source).toMatch(/xhr\.dt[\s\S]*hideLoading\(\)/);
    expect(source).toMatch(/initComplete[\s\S]*hideLoading\(\)/);
    expect(source).toMatch(/showAlert\(message\)[\s\S]*hideLoading\(\)/);
    expect(source).toContain("setListStatus(items.length ? '' : t('NoRecords'))");
  });

  it("keeps detail commands disabled until detail load succeeds", () => {
    const source = detailJsSource();

    expect(source).toMatch(/setCommandDisabled\(true\);[\s\S]*loadDetail\(\)/);
    expect(source).toMatch(/!response\.ok \|\| isBlocked\(body\) \|\| !item[\s\S]*setCommandDisabled\(true\)/);
    expect(source).toMatch(/catch \(error\)[\s\S]*setCommandDisabled\(true\)/);
    expect(source).toMatch(/setDetailStatus\(''\);[\s\S]*setCommandDisabled\(false\)/);
    expect(detailViewSource()).toMatch(/<form id="pvg-triage-form" data-pvg-command-form>/);
    expect(detailViewSource()).toMatch(/<form id="pvg-route-form" data-pvg-command-form>/);
  });

  it("keeps command and form submit controls disabled during in-flight submissions", () => {
    const details = detailJsSource();
    const form = formJsSource();

    expect(details).toMatch(/try \{[\s\S]*setCommandDisabled\(true\);[\s\S]*fetch\(url/);
    expect(details).toMatch(/finally \{[\s\S]*setCommandDisabled\(false\)/);
    expect(details).toMatch(/querySelectorAll\('button, input, select, textarea'\)/);
    expect(form).toMatch(/setSubmitting\(true\);[\s\S]*postForm\(url, new FormData\(form\)\)/);
    expect(form).toMatch(/postForm\(url, new FormData\(form\)\);[\s\S]*setSubmitting\(false\)/);
    expect(form).toMatch(/submitButton\.disabled = isSubmitting/);
    expect(form).toMatch(/aria-busy/);
  });

  it("keeps localized loading text and empty table states visible to users and assistive tech", () => {
    const index = indexJsSource();
    const indexView = indexViewSource();
    const detailView = detailViewSource();

    expect(index).toMatch(/emptyTable:\s*t\('NoRecords'\)/);
    expect(index).toMatch(/loadingRecords:\s*t\('Loading'\)/);
    expect(index).toMatch(/processing:\s*t\('Loading'\)/);
    expect(index).toContain("setListStatus(t('Loading'))");
    expect(index).toContain("setListStatus(items.length ? '' : t('NoRecords'))");
    expect(indexView).toMatch(/id="pvg-list-status"[^>]*aria-live="polite"/);
    // The shaped placeholder is the shared partial (it renders id="skeleton-loader"), not a hand-drawn block.
    expect(dataTableViewSource()).toMatch(/<partial name="_TableSkeleton" \/>/);
    expect(dataTableViewSource()).not.toMatch(/class="shimmer skeleton-row/);
    expect(detailView).toMatch(/id="pvg-detail-status"[^>]*aria-live="polite"[^>]*>@Localizer\["Loading"\]/);
  });

  it("keeps alert visibility controlled by safe message helpers", () => {
    const index = indexJsSource();
    const details = detailJsSource();
    const form = formJsSource();

    for (const source of [index, details, form]) {
      expect(source).toMatch(/function showAlert\(message\)[\s\S]*alertEl\.textContent = message/);
      expect(source).toMatch(/function showAlert\(message\)[\s\S]*alertEl\.classList\.remove\('d-none'\)/);
    }

    expect(index).toMatch(/function hideAlert\(\)[\s\S]*alertEl\?\.classList\.add\('d-none'\)/);
    expect(details).toMatch(/function hideAlert\(\)[\s\S]*alertEl\?\.classList\.add\('d-none'\)/);
    expect(indexViewSource()).toMatch(/id="pvg-list-alert"[^>]*class="[^"]*d-none[^"]*"[^>]*role="alert"/);
    expect(detailViewSource()).toMatch(/id="pvg-detail-alert"[^>]*class="[^"]*d-none[^"]*"[^>]*role="alert"/);
  });

  it("keeps safe reason-code display sanitized and bounded", () => {
    for (const source of [indexJsSource(), detailJsSource(), formJsSource()]) {
      expect(source).toMatch(/function safeMessage\(body, statusCode\)[\s\S]*status === 401[\s\S]*t\('SessionExpired'\)/);
      expect(source).toMatch(/function safeMessage\(body, statusCode\)[\s\S]*status === 403[\s\S]*t\('NotAuthorized'\)/);
      expect(source).toMatch(/reasonCode \|\| body\?\.ReasonCode \|\| body\?\.reason_code/);
      expect(source).toMatch(/validationReasonCodes \|\| body\?\.ValidationReasonCodes/);
      expect(source).toMatch(/t\('ControlledBlock'\)[\s\S]*t\('ReasonCode'\)/);
      expect(source).toMatch(/function safeCode\(value\)[\s\S]*replace\(\/\[\^A-Za-z0-9\._-\]\/g, ''\)\.slice\(0, 96\)/);
    }
  });

  it("keeps strict same-origin MVC proxy behavior on list detail and form paths", () => {
    const list = indexJsSource();
    const details = detailJsSource();
    const form = formJsSource();

    for (const source of [list, details, form]) {
      expect(source).toContain("const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api'");
      expect(source).toMatch(/function safeProxyUrl|const safeProxyUrl = path =>/);
      expect(source).toMatch(/!path\.startsWith\(`\$\{endpoint\}\/`\)/);
      expect(source).toMatch(/path\.includes\(':\/\/'\)/);
      expect(source).toMatch(/path\.startsWith\('\/\/'\)/);
      expect(source).not.toMatch(/https?:\/\/(?:localhost|127\.0\.0\.1):(?:5000|5011)\b/i);
    }

    expect(list).toMatch(/url:\s*safeProxyUrl\(`\$\{endpoint\}\/list`\)/);
    expect(details).toMatch(/fetch\(safeProxyUrl\(`\$\{endpoint\}\/detail\//);
    expect(details).toMatch(/fetch\(url,[\s\S]*credentials:\s*'same-origin'/);
    expect(form).toMatch(/postForm\(url, new FormData\(form\)\)/);
    expect(form).toMatch(/fetch\(url,[\s\S]*credentials:\s*'same-origin'/);
  });

  it("keeps command buttons disabled around detail loading and command submission", () => {
    const details = detailJsSource();

    expect(details).toMatch(/setCommandDisabled\(true\);[\s\S]*setDetailStatus\(t\('Loading'\)\);[\s\S]*loadDetail\(\)/);
    expect(details).toMatch(/!response\.ok \|\| isBlocked\(body\) \|\| !item[\s\S]*setCommandDisabled\(true\)/);
    expect(details).toMatch(/setDetailStatus\(''\);[\s\S]*setCommandDisabled\(false\)/);
    expect(details).toMatch(/try \{[\s\S]*setCommandDisabled\(true\);[\s\S]*setDetailStatus\(t\('Loading'\)\)/);
    expect(details).toMatch(/finally \{[\s\S]*setCommandDisabled\(false\)/);
  });

  it("keeps all seven Case Intake/Triage resource files present", () => {
    for (const culture of pvgResourceCultures) {
      expect(fs.existsSync(path.join(root, pvgResourceFile(culture)))).toBe(true);
    }
  });

  it("keeps safe UI state keys in every Case Intake/Triage resource file", () => {
    for (const culture of pvgResourceCultures) {
      const keys = resourceKeys(culture);

      for (const key of safeUiStateKeys) {
        expect(keys.has(key)).toBe(true);
      }
    }
  });

  it("exposes safe UI state keys through the Case Intake/Triage localization bridge", () => {
    const source = indexL10nSource();
    const browserBridge = read("wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js");
    const bridgeKeys = l10nBridgeKeys();

    expect(browserBridge).toContain("window.PvgCaseIntakeTriageL10n");
    for (const key of safeUiStateKeys) {
      expect(bridgeKeys.has(key)).toBe(true);
      expect(source).toMatch(new RegExp(`\\b${key}\\s*=\\s*Localizer\\["${key}"\\]\\.Value`));
    }
  });

  it("keeps browser localization references backed by PVG resources or shared localizer keys", () => {
    const bridgeKeys = l10nBridgeKeys();
    const sharedKeys = sharedL10nBridgeKeys();
    const references = browserL10nReferences();

    for (const key of references) {
      expect(bridgeKeys.has(key)).toBe(true);

      if (sharedKeys.has(key)) {
        continue;
      }

      for (const culture of pvgResourceCultures) {
        expect(resourceKeys(culture).has(key)).toBe(true);
      }
    }
  });
});

// ── MOD-0230 WP-0230-DL7A (AC-20, AC-15, FG-003) — the list component, the record version and the seven languages ──
const vm = require("vm");

const proxyList = "/Pharmacovigilance/CaseIntakeTriage/api/list";
const until = async (predicate, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    if (predicate()) return;
    await new Promise(resolve => setTimeout(resolve, 10));
  }
  throw new Error("timed out");
};

// The list THROUGH the real jQuery, the real DataTables and the real diten-datatable.js: only the network is replaced.
describe("PVG case intake list on the list component (real DataTables, fake network)", () => {
  const requests = [];
  let answer = () => "{}";
  let handle = null;
  let ctx;

  beforeAll(() => {
    ctx = vm.createContext({
      window, document, navigator: window.navigator, location: window.location, console, setTimeout, clearTimeout,
      setInterval, clearInterval, requestAnimationFrame: fn => setTimeout(fn, 0), getComputedStyle: window.getComputedStyle.bind(window),
      Node: window.Node, HTMLElement: window.HTMLElement, Element: window.Element, Event: window.Event, CustomEvent: window.CustomEvent,
      bootstrap: {
        Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) },
        Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) },
        Modal: { getInstance: () => null, getOrCreateInstance: () => ({ show() {} }) }
      }
    });
    Object.getOwnPropertyNames(window)
      .filter(key => /^[A-Z]/.test(key) && typeof window[key] === "function" && !(key in ctx))
      .forEach(key => { ctx[key] = window[key]; });
    ctx.self = ctx;
    const run = (relativePath) => vm.runInContext(read(relativePath), ctx, { filename: relativePath });
    run("wwwroot/assets/vendor/libs/jquery/jquery.js");
    ctx.$ = ctx.jQuery = ctx.window.jQuery || ctx.jQuery;
    run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
    ctx.DataTable = ctx.DataTable || ctx.jQuery.fn.dataTable;
    run("wwwroot/assets/js/diten-datatable.js");

    window.DtDefaults = { create: config => config, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {} };
    window.personalizationClient = { getViews: async () => [] };
    const createList = window.DitenDataTable.createList;
    window.DitenDataTable.createList = async options => {
      handle = await createList(options);
      return handle;
    };

    ctx.jQuery.ajaxTransport("+*", options => ({
      send(_headers, complete) {
        requests.push(decodeURIComponent(options.url));
        const body = answer(decodeURIComponent(options.url));
        setTimeout(() => complete(200, "OK", { text: body }, "Content-Type: application/json"), 0);
      },
      abort() {}
    }));
  });

  const item = number => ({ intakeDraftId: `draft-${String(number).padStart(3, "0")}`, status: "IntakeCreated", version: 1 });
  const envelope = (items, total, filteredTotal) => JSON.stringify({ data: { items, total, filteredTotal } });

  async function mount(respond) {
    requests.length = 0;
    handle = null;
    answer = respond;
    document.body.innerHTML = `
      <div id="pvg-list-alert" class="d-none" role="alert"></div>
      <div id="pvg-list-status" aria-live="polite"></div>
      <div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse">
        <select id="pvgStatusFilter"><option value="">All</option><option value="Triaged">Triaged</option></select>
        <button id="pvgApplyFilter"></button><button id="pvgResetFilter"></button>
      </div></div>
      <div class="card"><div id="skeleton-loader"></div><div class="card-datatable">
        <table id="dt-pvg-case-intake-triage" class="datatables-pvg-case-intake-triage"><thead><tr><th></th><th>Id</th><th>Status</th><th></th></tr></thead><tbody></tbody></table>
      </div></div>`;
    window.PvgCaseIntakeTriageL10n = { Loading: "Loading", NoRecords: "No records", Details: "Details", Edit: "Edit", ErrorOccurred: "Error" };
    vm.runInContext(read("wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js"), ctx, { filename: "index.js" });
    await until(() => handle !== null && handle.lastResponse !== undefined && requests.length >= 1);
    return handle;
  }

  const lastRequest = () => requests[requests.length - 1];
  const drawn = list => list.dt.rows().data().toArray().map(row => row.intakeDraftId);

  it("pvg list sends the requested page number and page size", async () => {
    const list = await mount(() => envelope([item(1)], 250, 250));

    expect(requests[0]).toMatch(new RegExp(`^${proxyList}\\?start=0&length=10&draw=1`));
    expect(requests[0], "DataTables' own positional dump must not travel").not.toMatch(/columns\[|order\[|search\[/);

    list.dt.page.len(100).draw();
    await until(() => /length=100/.test(lastRequest()));
    const before = requests.length;
    list.dt.page(1).draw("page");
    await until(() => requests.length > before);
    expect(lastRequest()).toMatch(new RegExp(`^${proxyList}\\?start=100&length=100&draw=\\d+`));

    list.dt.page.len(25).draw();
    await until(() => /length=25/.test(lastRequest()));
    const beforeThird = requests.length;
    list.dt.page(2).draw("page");
    await until(() => requests.length > beforeThird);
    expect(lastRequest()).toMatch(new RegExp(`^${proxyList}\\?start=50&length=25&draw=\\d+`));

    const source = indexJsSource();
    for (const forbidden of ["serverSide", "ajax.data", "buildListUrl", "pageNumber=1", "pageSize=100"]) {
      expect(source, forbidden).not.toContain(forbidden);
    }
    expect(source).toContain("/Pharmacovigilance/CaseIntakeTriage/api");
    expect(source).toContain("DitenDataTable.createList(");
    expect(source).toMatch(/dataMode:\s*'server'/);
    expect(requests.every(url => url.startsWith(proxyList))).toBe(true);
  });

  it("pvg list reaches the 101st record on page 2", async () => {
    const first = Array.from({ length: 100 }, (_, index) => item(index + 1));
    const list = await mount(url => (/start=100/.test(url) ? envelope([item(101)], 101, 101) : envelope(first, 101, 101)));

    list.dt.page.len(100).draw();
    await until(() => /length=100/.test(lastRequest()) && drawn(list).length === 100);
    expect(drawn(list)).toHaveLength(100);

    list.dt.page(1).draw("page");
    await until(() => /start=100&length=100/.test(lastRequest()) && drawn(list).length === 1);

    expect(drawn(list)).toEqual(["draft-101"]);
    expect(document.querySelector("#dt-pvg-case-intake-triage tbody").textContent).toContain("draft-101");
  });

  it("pvg list shows the API total and filtered counts", async () => {
    const items = Array.from({ length: 25 }, (_, index) => item(index + 1));
    const list = await mount(() => envelope(items, 240, 37));

    await until(() => list.dt.page.info().recordsDisplay === 37);
    const info = list.dt.page.info();

    expect(info.recordsTotal).toBe(240);
    expect(info.recordsDisplay).toBe(37);
    expect(info.recordsDisplay).not.toBe(25);
    const infoText = document.querySelector(".dt-info")?.textContent || "";
    expect(infoText).toContain("37");
    expect(infoText).toContain("240");
    expect(infoText).not.toMatch(/\b25 entries\b/);
    expect(info.pages).toBe(Math.ceil(37 / info.length));
  });

  it("sends the applied status filter through the factory's own Apply, still as start and length", async () => {
    const list = await mount(() => envelope([item(1)], 1, 1));
    // The factory wires Apply (and writes the filter controls) once its options are set up, a tick after the first
    // draw: choose and apply until the request carries the filter.
    await until(() => {
      if (!/status=Triaged/.test(lastRequest())) {
        window.jQuery("#pvgStatusFilter").val("Triaged");
        document.getElementById("pvgApplyFilter").click();
      }
      return /status=Triaged/.test(lastRequest());
    }, 8000);

    expect(lastRequest()).toMatch(/^\/Pharmacovigilance\/CaseIntakeTriage\/api\/list\?start=0&length=\d+&draw=\d+&status=Triaged/);
    expect(drawn(list)).toEqual(["draft-001"]);
  });
});

// The three browser forms, executed against a fake network: the record version, and a conflict as a controlled failure.
describe("PVG case intake record version on save, triage and route (executed)", () => {
  const conflict = { outcome: "Blocked", reasonCode: "PVG_INTAKE_DRAFT_VERSION_CONFLICT", items: [] };
  const texts = { VersionConflict: "The record changed after you read it.", ErrorOccurred: "Something went wrong.", ControlledBlock: "Blocked", ReasonCode: "Reason", Triaged: "Triaged", Routed: "Routed", Loading: "Loading" };
  const answer = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

  function run(script, calls, replies) {
    const location = { assign: vi.fn() };
    const fetch = vi.fn(async (url, init) => {
      calls.push({ url, init });
      return replies.shift() ?? answer(500, {});
    });
    vm.runInNewContext(read(`wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/${script}`), {
      window: { PvgCaseIntakeTriageL10n: texts, location },
      document, fetch, FormData: window.FormData, Number, Array, String, Promise, Error, URL, console
    });
    return { location, fetch };
  }

  const submit = form => form.dispatchEvent(new window.Event("submit", { bubbles: true, cancelable: true }));

  it("pvg form sends the record version and shows a conflict as a controlled failure", async () => {
    // ── Edit form: the version read on open travels with the save; a conflict is a sentence, not a success.
    document.body.innerHTML = `
      <div id="pvg-form-alert" class="alert d-none" role="alert"></div>
      <button type="submit" form="pvg-case-intake-form" id="saveButton">Save</button>
      <form id="pvg-case-intake-form" data-mode="edit" data-intake-draft-id="D-1">
        <input type="hidden" name="ExpectedVersion" id="ExpectedVersion" />
        <input name="SourceReference" value="REF-1" required />
      </form>`;
    const editCalls = [];
    const edit = run("form.js", editCalls, [
      answer(200, { outcome: "Succeeded", items: [{ intakeDraftId: "D-1", version: 7 }] }),
      answer(409, conflict)
    ]);
    await until(() => document.getElementById("ExpectedVersion").value === "7");
    expect(editCalls[0].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/detail/D-1");
    expect(document.getElementById("saveButton").disabled).toBe(false);

    submit(document.getElementById("pvg-case-intake-form"));
    await until(() => editCalls.length === 2 && !document.getElementById("pvg-form-alert").classList.contains("d-none"));
    expect(editCalls[1].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/update/D-1");
    expect(editCalls[1].init.body.get("ExpectedVersion")).toBe("7");
    expect(editCalls[1].init.body.get("SourceReference")).toBe("REF-1");
    const alert = document.getElementById("pvg-form-alert");
    expect(alert.textContent).toBe(texts.VersionConflict);
    expect(alert.getAttribute("role")).toBe("alert");
    expect(edit.location.assign).not.toHaveBeenCalled();
    expect(document.getElementById("saveButton").disabled).toBe(false);
    expect(document.querySelector("[name=SourceReference]").value).toBe("REF-1");

    // ── Edit form whose detail cannot be read: nothing safe to save, so Save stays off and the reader is told.
    document.body.innerHTML = `
      <div id="pvg-form-alert" class="alert d-none" role="alert"></div>
      <button type="submit" form="pvg-case-intake-form" id="saveButton">Save</button>
      <form id="pvg-case-intake-form" data-mode="edit" data-intake-draft-id="D-2">
        <input type="hidden" name="ExpectedVersion" id="ExpectedVersion" />
      </form>`;
    const failedCalls = [];
    run("form.js", failedCalls, [answer(500, {})]);
    await until(() => !document.getElementById("pvg-form-alert").classList.contains("d-none"));
    expect(document.getElementById("saveButton").disabled).toBe(true);
    submit(document.getElementById("pvg-case-intake-form"));
    await new Promise(resolve => setTimeout(resolve, 30));
    expect(failedCalls).toHaveLength(1);

    // ── Details: triage and route send the version read on open, and the one a command answers with.
    document.body.innerHTML = `
      <div id="pvg-detail-alert" class="alert d-none" role="alert"></div>
      <div id="pvg-detail-status" aria-live="polite"></div>
      <div id="pvg-case-intake-detail" data-intake-draft-id="D-3"><span id="pvgDetailStatus"></span></div>
      <form id="pvg-triage-form" data-pvg-command-form>
        <input type="hidden" name="ExpectedVersion" id="TriageExpectedVersion" />
        <select name="TriageOutcome" required><option value="">-</option><option value="Triaged" selected>T</option></select>
        <button type="submit">Triage</button>
      </form>
      <form id="pvg-route-form" data-pvg-command-form>
        <input type="hidden" name="ExpectedVersion" id="RouteExpectedVersion" />
        <select name="RouteTargetQueue" required><option value="PV_SAFETY_REVIEW" selected>S</option></select>
        <button type="submit">Route</button>
      </form>`;
    const detailCalls = [];
    run("details.js", detailCalls, [
      answer(200, { outcome: "Succeeded", items: [{ intakeDraftId: "D-3", status: "IntakeCreated", version: 7 }] }),
      answer(200, { outcome: "Succeeded", version: 8 }),
      answer(409, conflict)
    ]);
    await until(() => document.querySelector("#pvg-triage-form button").disabled === false);
    expect(document.getElementById("TriageExpectedVersion").value).toBe("7");
    expect(document.getElementById("RouteExpectedVersion").value).toBe("7");

    submit(document.getElementById("pvg-triage-form"));
    await until(() => detailCalls.length === 2 && document.getElementById("pvgDetailStatus").textContent === texts.Triaged);
    expect(detailCalls[1].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/triage/D-3");
    expect(detailCalls[1].init.body.get("ExpectedVersion")).toBe("7");
    expect(detailCalls[1].init.body.get("TriageOutcome")).toBe("Triaged");
    expect(document.getElementById("RouteExpectedVersion").value).toBe("8");

    await until(() => document.querySelector("#pvg-route-form button").disabled === false);
    submit(document.getElementById("pvg-route-form"));
    await until(() => detailCalls.length === 3 && !document.getElementById("pvg-detail-alert").classList.contains("d-none"));
    expect(detailCalls[2].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/route/D-3");
    expect(detailCalls[2].init.body.get("ExpectedVersion")).toBe("8");
    expect(document.getElementById("pvg-detail-alert").textContent).toBe(texts.VersionConflict);
    expect(document.getElementById("pvgDetailStatus").textContent).not.toBe(texts.Routed);
    await until(() => document.querySelector("#pvg-route-form button").disabled === false);
  });
});

describe("PVG screen rules", () => {
  it("pvg screen has no inline styles", () => {
    const views = pvgUiFiles.filter(file => file.endsWith(".cshtml"));
    for (const file of views) {
      expect(read(file), file).not.toMatch(/\sstyle\s*=/i);
    }

    for (const file of pvgUiFiles.filter(name => name.endsWith(".js"))) {
      const source = read(file);
      expect(source, file).not.toMatch(/\.style\./);
      expect(source, file).not.toMatch(/setAttribute\(\s*['"]style['"]/);
    }
  });

  it("pvg resources carry the same key set in seven languages without english echo", () => {
    const english = resourceKeys("en");
    const englishValues = culture => {
      const text = read(pvgResourceFile(culture));
      return new Map([...text.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([^<]*)<\/value>/g)].map(match => [match[1], match[2]]));
    };
    const base = englishValues("en");
    // Keys added by this work: a real translation in every language, never the English text.
    const newKeys = ["VersionConflict"];

    for (const culture of pvgResourceCultures) {
      expect([...resourceKeys(culture)].sort(), culture).toEqual([...english].sort());
      if (culture === "en") continue;
      const values = englishValues(culture);
      for (const key of newKeys) {
        expect(values.get(key), `${culture}:${key}`).toBeTruthy();
        expect(values.get(key), `${culture}:${key}`).not.toBe(base.get(key));
      }
    }

    for (const key of newKeys) {
      expect(l10nBridgeKeys().has(key)).toBe(true);
    }
  });
});
