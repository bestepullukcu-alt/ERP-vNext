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
  // The form reads the option lists first (AC-34, WP-0230-DL7B), then the record.
  const optionsAnswer = () => answer(200, { reportTypes: ["Spontaneous"], productRoles: ["Suspect"], unknownCountry: "unknown" });

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
      optionsAnswer(),
      answer(200, { outcome: "Succeeded", items: [{ intakeDraftId: "D-1", version: 7 }] }),
      answer(409, conflict)
    ]);
    await until(() => document.getElementById("ExpectedVersion").value === "7");
    expect(editCalls[1].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/detail/D-1");
    expect(document.getElementById("saveButton").disabled).toBe(false);

    submit(document.getElementById("pvg-case-intake-form"));
    await until(() => editCalls.length === 3 && !document.getElementById("pvg-form-alert").classList.contains("d-none"));
    expect(editCalls[2].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/update/D-1");
    expect(editCalls[2].init.body.get("ExpectedVersion")).toBe("7");
    expect(editCalls[2].init.body.get("SourceReference")).toBe("REF-1");
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
    run("form.js", failedCalls, [optionsAnswer(), answer(500, {})]);
    await until(() => !document.getElementById("pvg-form-alert").classList.contains("d-none"));
    expect(document.getElementById("saveButton").disabled).toBe(true);
    submit(document.getElementById("pvg-case-intake-form"));
    await new Promise(resolve => setTimeout(resolve, 30));
    expect(failedCalls).toHaveLength(2);

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
    const values = culture => new Map(
      [...read(pvgResourceFile(culture)).matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([^<]*)<\/value>/g)].map(match => [match[1], match[2]])
    );
    const normalized = value => value.trim().replace(/\s+/g, " ").toLowerCase();
    const base = values("en");
    // AC-15, CEVIRI-01 C-3: the only values that stay as in English are the ones the approved prompt names (CT decisions
    // A-1 and A-3): real words of that language. The worker never adds to this list.
    const sameAsEnglish = [
      "es:ChannelPortal", "tr:ChannelPortal", "fr:TriageTitle", "fr:Actions", "fr:SourceSection", "fr:PriorityRoutine",
      "fr:ProductRoleSuspect", "fr:ProductRoleConcomitant", "fr:ReporterProfession"
    ];
    expect(sameAsEnglish).toHaveLength(9);

    for (const culture of pvgResourceCultures) {
      expect([...resourceKeys(culture)].sort(), culture).toEqual([...english].sort());
      if (culture === "en") continue;
      const translated = values(culture);
      for (const key of english) {
        const pair = `${culture}:${key}`;
        expect(translated.get(key)?.trim(), pair).toBeTruthy();
        expect(normalized(translated.get(key)) === normalized(base.get(key)), pair).toBe(sameAsEnglish.includes(pair));
      }
    }

    expect(l10nBridgeKeys().has("VersionConflict")).toBe(true);
  });
});

// ── MOD-0230 WP-0230-DL7B (AC-32, AC-34) — the revision 04 field inputs and the option source, on the real _Form.cshtml ──
describe("PVG case intake form fields and option source (executed against the real form)", () => {
  const formView = "Views/Pharmacovigilance/CaseIntakeTriage/_Form.cshtml";
  const controllerFile = "Controllers/PharmacovigilanceCaseIntakeTriageController.cs";
  const viewModelFile = "Models/CaseIntakeTriage/PvgCaseIntakeFormViewModel.cs";
  const moduleJsDirectory = "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage";
  const neverPosted = [
    "ReporterSource", "ReporterSourceId", "ReporterCrmSearchState", "ValidityResult", "ValidityMissingCriteria",
    "GlobalProductId", "PresentationGskuId", "CompanyRecipientPersonId"
  ];
  const texts = {
    ErrorOccurred: "Something went wrong.", ControlledBlock: "Blocked", ReasonCode: "Reason", VersionConflict: "Changed.",
    OptionsLoadFailed: "The lists could not be read.", CountryUnknown: "Unknown country"
  };
  const optionsBody = () => ({
    reportTypes: ["Study", "Spontaneous", "SyntheticType"],
    productRoles: ["Suspect", "Concomitant"],
    unknownCountry: "SyntheticUnknown"
  });
  const answer = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body });
  const options = () => answer(200, optionsBody());
  const saved = () => answer(200, { outcome: "Succeeded", intakeDraftId: "D-1", items: [] });

  // The Razor markup with its localizer calls reduced to their keys: the test reads the view, not a copy of it.
  const renderedForm = (mode, id) => read(formView)
    .replace(/^@(?:using|model|inject)[^\n]*\n/gm, "")
    .replace(/^@\{[\s\S]*?^\}\n/m, "")
    .replace('@(isEdit ? Localizer["UpdateDraft"] : Localizer["SaveDraft"])', "Save")
    .replace(/@title/g, "Title")
    .replace("@Html.AntiForgeryToken()", '<input name="__RequestVerificationToken" type="hidden" value="synthetic-token" />')
    .replace('data-mode="@mode"', `data-mode="${mode}"`)
    .replace('data-intake-draft-id="@intakeDraftId"', `data-intake-draft-id="${id}"`)
    .replace(/@Localizer\["([A-Za-z0-9_]+)"\]/g, "$1");

  function mount({ mode = "create", id = "", replies = [], l10n = texts }) {
    document.body.innerHTML = renderedForm(mode, id);
    const calls = [];
    const location = { assign: vi.fn() };
    const fetch = vi.fn(async (url, init) => {
      calls.push({ url, init });
      const next = replies.shift();
      if (next instanceof Error) throw next;
      return next ?? answer(500, {});
    });
    vm.runInNewContext(read(`${moduleJsDirectory}/form.js`), {
      window: { PvgCaseIntakeTriageL10n: l10n, location },
      document, fetch, FormData: window.FormData, Number, Array, String, Promise, Error, URL, console
    });
    return { calls, location };
  }

  const byId = id => document.getElementById(id);
  const form = () => byId("pvg-case-intake-form");
  const saveButton = () => document.querySelector('button[type="submit"][form="pvg-case-intake-form"]');
  const alertBox = () => byId("pvg-form-alert");
  const alertShown = () => !alertBox().classList.contains("d-none");
  const hidden = element => element.closest(".d-none") !== null;
  const submit = () => form().dispatchEvent(new window.Event("submit", { bubbles: true, cancelable: true }));
  const tick = () => new Promise(resolve => setTimeout(resolve, 30));
  const rows = () => [...document.querySelectorAll("[data-product-line]")];
  const lineInput = (row, field) => row.querySelector(`[name$=".${field}"]`);
  const addLine = values => {
    byId("pvg-add-product-line").click();
    const row = rows()[rows().length - 1];
    if (values) {
      lineInput(row, "ProductRole").value = values.role ?? "";
      lineInput(row, "ProductNameAsReported").value = values.name ?? "";
      lineInput(row, "LotNumber").value = values.lot ?? "";
    }
    return row;
  };
  const typeInto = (element, value) => {
    element.value = value;
    element.dispatchEvent(new window.Event("input", { bubbles: true }));
  };
  const optionValues = select => [...select.options].map(option => option.value);
  const optionTexts = select => [...select.options].map(option => option.textContent);
  const fillRecordFields = () => {
    const values = {
      IntakeChannel: "Portal", SourceType: "Partner", SourceReference: "REF-1", ReceivedAtUtc: "2026-10-20T10:00",
      ReporterType: "Consumer", PatientSubjectCode: "SUBJ-1", AdverseEventNarrative: "synthetic-narrative",
      Seriousness: "Serious", IntakePriority: "Routine"
    };
    Object.entries(values).forEach(([id, value]) => { byId(id).value = value; });
  };
  const fillCreateFields = () => {
    fillRecordFields();
    byId("AwarenessAtUtc").value = "2026-10-20T09:00";
    byId("ReportType").value = "Study";
    byId("EventCountry").value = "SyntheticUnknown";
  };
  const ready = () => until(() => !saveButton().disabled);
  const selectBlock = (source, name) => {
    const match = source.match(new RegExp(`<select\\b[^>]*name="${name.replace(/[[\]]/g, "\\$&")}"[^>]*>[\\s\\S]*?</select>`));
    expect(match, name).not.toBeNull();
    return match[0];
  };
  const onlyThePlaceholder = block => {
    const offered = [...block.matchAll(/<option\b[^>]*>/g)].map(match => match[0]);
    expect(offered).toEqual(['<option value="">']);
  };
  const parameters = (source, record) => {
    const match = source.match(new RegExp(`record\\s+${record}\\(([\\s\\S]*?)\\)\\s*(?:\\{|;)`));
    expect(match, `the record ${record} is in the controller file`).not.toBeNull();
    return match[1].split(",").map(part => part.trim()).filter(Boolean).map(part => {
      const parameter = part.match(/^(\S+)\s+([A-Za-z0-9_]+)(?:\s*=.*)?$/);
      return { type: parameter[1], name: parameter[2] };
    });
  };
  const anyForbiddenKey = body => [...body.keys()].filter(key => [...neverPosted, "SuspectProductText"].some(name => key.includes(name)));

  it("pvg form posts the product lines and the reporter selection reference", async () => {
    // ── Source: the view and the proxy records answer each other, in both directions, with no list written here.
    const view = read(formView);
    const controller = read(controllerFile);
    for (const source of [view, read(viewModelFile), controller, read(`${moduleJsDirectory}/form.js`)]) {
      expect(source).not.toContain("SuspectProductText");
    }

    const template = view.match(/<template\b[\s\S]*?<\/template>/)?.[0];
    expect(template).toBeTruthy();
    for (const field of ["ProductRole", "ProductNameAsReported", "LotNumber"]) {
      expect(template).toContain(`name="ProductLines[__index__].${field}"`);
    }

    const topLevel = parameters(controller, "CaseIntakeFormInput");
    const lineRecord = topLevel.find(parameter => parameter.name === "ProductLines")?.type.match(/<([A-Za-z0-9_]+)>/)?.[1];
    expect(lineRecord).toBeTruthy();
    const lineLevel = parameters(controller, lineRecord);
    const linePrefix = "ProductLines[__index__].";
    const viewNames = [...view.matchAll(/\bname="([^"]+)"/g)].map(match => match[1]);
    // Every proxy field has its input. Adding a name here takes a new version of the approved prompt.
    const fieldsWithoutAnInput = [];
    expect(fieldsWithoutAnInput).toHaveLength(0);
    expect(fieldsWithoutAnInput.filter(name => neverPosted.includes(name))).toEqual([]);

    for (const name of viewNames) {
      if (name.startsWith(linePrefix)) {
        expect(lineLevel.map(parameter => parameter.name), name).toContain(name.slice(linePrefix.length));
      } else {
        expect(topLevel.map(parameter => parameter.name), name).toContain(name);
      }
    }

    for (const { name } of topLevel.filter(parameter => parameter.name !== "ProductLines" && !fieldsWithoutAnInput.includes(parameter.name))) {
      expect(viewNames, name).toContain(name);
    }

    for (const { name } of lineLevel) {
      expect(viewNames, name).toContain(`${linePrefix}${name}`);
    }

    expect(view).toMatch(/<input\b(?=[^>]*name="AwarenessAtUtc")(?=[^>]*type="datetime-local")(?=[^>]*step="1")(?=[^>]*\brequired\b)[^>]*>/);
    expect(template).toMatch(/<input\b(?=[^>]*\.ProductNameAsReported")(?=[^>]*\brequired\b)[^>]*>/);
    for (const select of [selectBlock(template, `${linePrefix}ProductRole`), selectBlock(view, "ReportType"), selectBlock(view, "EventCountry")]) {
      expect(select.match(/<select\b[^>]*>/)[0]).toMatch(/\brequired\b/);
    }

    // ── Source: the line buttons are real buttons that never submit, named from the page resources.
    const removeButton = template.match(/<(\w+)\b[^>]*data-action="remove-product-line"[^>]*>/);
    const outside = view.replace(template, "");
    const addButton = outside.match(/<(\w+)\b[^>]*data-action="add-product-line"[^>]*>/);
    for (const button of [removeButton, addButton]) {
      expect(button).not.toBeNull();
      expect(button[1]).toBe("button");
      expect(button[0]).toContain('type="button"');
      const key = button[0].match(/aria-label="@Localizer\["([A-Za-z0-9_]+)"\]"/)?.[1];
      expect(key, button[0]).toBeTruthy();
      for (const culture of pvgResourceCultures) {
        const value = read(pvgResourceFile(culture)).match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]*)</value>`))?.[1];
        expect(value?.trim(), `${culture}:${key}`).toBeTruthy();
      }
    }

    // ── Two lines travel with their numbers; a manual reporter posts neither the pair nor the marker.
    let { calls, location } = mount({ replies: [options(), saved()] });
    await ready();
    fillCreateFields();
    addLine({ role: "Suspect", name: "synthetic-a", lot: "L-1" });
    addLine({ role: "Concomitant", name: "synthetic-b" });
    submit();
    await until(() => calls.length === 2);
    let body = calls[1].init.body;
    expect(calls[1].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/create");
    expect(body.get("ProductLines[0].ProductRole")).toBe("Suspect");
    expect(body.get("ProductLines[0].ProductNameAsReported")).toBe("synthetic-a");
    expect(body.get("ProductLines[0].LotNumber")).toBe("L-1");
    expect(body.get("ProductLines[1].ProductRole")).toBe("Concomitant");
    expect(body.get("ProductLines[1].ProductNameAsReported")).toBe("synthetic-b");
    expect(body.get("EventCountry")).toBe("SyntheticUnknown");
    expect(body.get("ReportType")).toBe("Study");
    expect(body.has("ReporterSelectionDirectory")).toBe(false);
    expect(body.has("ReporterSelectionId")).toBe(false);
    expect(body.has("RemoveReporterLink")).toBe(false);
    expect(anyForbiddenKey(body)).toEqual([]);
    await until(() => location.assign.mock.calls.length === 1);

    const lineIndexes = formData => [...new Set([...formData.keys()]
      .map(key => key.match(/^ProductLines\[(.+?)\]/)?.[1])
      .filter(Boolean))];

    // ── An untouched empty line does not block the save and does not travel; a line of spaces is empty too.
    ({ calls } = mount({ replies: [options(), saved()] }));
    await ready();
    fillCreateFields();
    addLine();
    addLine({ role: "", name: "   ", lot: "  " });
    submit();
    await until(() => calls.length === 2);
    expect(lineIndexes(calls[1].init.body)).toEqual([]);

    // ── The middle line emptied: the numbers that travel have no gap.
    ({ calls } = mount({ replies: [options(), saved()] }));
    await ready();
    fillCreateFields();
    addLine({ role: "Suspect", name: "synthetic-a" });
    const middle = addLine({ role: "Suspect", name: "synthetic-b" });
    addLine({ role: "Concomitant", name: "synthetic-c" });
    lineInput(middle, "ProductRole").value = "";
    lineInput(middle, "ProductNameAsReported").value = "";
    submit();
    await until(() => calls.length === 2);
    expect(lineIndexes(calls[1].init.body)).toEqual(["0", "1"]);
    expect(calls[1].init.body.get("ProductLines[1].ProductNameAsReported")).toBe("synthetic-c");
    expect(calls[1].init.body.has("ProductLines[2].ProductRole")).toBe(false);

    // ── The middle line removed: the same. Removing numbers nothing: only the save does.
    ({ calls } = mount({ replies: [options(), saved()] }));
    await ready();
    fillCreateFields();
    addLine({ role: "Suspect", name: "synthetic-a" });
    const removed = addLine({ role: "Suspect", name: "synthetic-b" });
    const last = addLine({ role: "Concomitant", name: "synthetic-c" });
    const nameBefore = lineInput(last, "ProductRole").name;
    removed.querySelector('[data-action="remove-product-line"]').click();
    expect(rows()).toHaveLength(2);
    expect(lineInput(last, "ProductRole").name).toBe(nameBefore);
    submit();
    await until(() => calls.length === 2);
    expect(lineIndexes(calls[1].init.body)).toEqual(["0", "1"]);
    expect(calls[1].init.body.get("ProductLines[1].ProductNameAsReported")).toBe("synthetic-c");

    // ── Twenty lines at most.
    mount({ replies: [options()] });
    await ready();
    for (let index = 0; index < 20; index++) {
      expect(byId("pvg-add-product-line").disabled).toBe(false);
      addLine();
    }
    expect(rows()).toHaveLength(20);
    expect(byId("pvg-add-product-line").disabled).toBe(true);
    byId("pvg-add-product-line").click();
    expect(rows()).toHaveLength(20);

    // ── A named line without a role blocks the save.
    ({ calls } = mount({ replies: [options(), saved()] }));
    await ready();
    fillCreateFields();
    addLine({ role: "", name: "synthetic-a" });
    submit();
    await tick();
    expect(calls).toHaveLength(1);
    expect(alertShown()).toBe(true);

    // ── An empty line is back on after every way out of a save, and travels once it is filled.
    const emptyLineIsOn = row => ["ProductRole", "ProductNameAsReported", "LotNumber"].forEach(field => {
      expect(lineInput(row, field).disabled, field).toBe(false);
    });

    // (i) another required field is empty.
    ({ calls } = mount({ replies: [options()] }));
    await ready();
    fillCreateFields();
    byId("SourceReference").value = "";
    let empty = addLine();
    submit();
    expect(calls).toHaveLength(1);
    emptyLineIsOn(empty);

    // (ii) 409, (iii) 400, (iv) 500, (v) the network fails.
    for (const failure of [
      () => answer(409, { outcome: "Blocked", reasonCode: "PVG_INTAKE_DRAFT_VERSION_CONFLICT", items: [] }),
      () => answer(400, { outcome: "Invalid", reasonCode: "VALIDATION_FAILED" }),
      () => answer(500, {}),
      () => new Error("network")
    ]) {
      ({ calls } = mount({ replies: [options(), failure(), saved()] }));
      await ready();
      fillCreateFields();
      empty = addLine();
      submit();
      await until(() => calls.length === 2 && alertShown() && !saveButton().disabled);
      emptyLineIsOn(empty);

      lineInput(empty, "ProductRole").value = "Suspect";
      lineInput(empty, "ProductNameAsReported").value = "synthetic-later";
      submit();
      await until(() => calls.length === 3);
      expect(calls[2].init.body.get("ProductLines[0].ProductNameAsReported")).toBe("synthetic-later");
    }

    // (vi) Edit, sent with no version read: the record read failed, only the save gate turns the send back.
    ({ calls } = mount({ mode: "edit", id: "D-1", replies: [options(), answer(500, {})] }));
    await until(() => calls.length === 2 && alertShown());
    fillRecordFields();
    byId("AwarenessAtUtc").value = "2026-10-20T09:00";
    byId("ReportType").value = "Study";
    byId("EventCountry").value = "SyntheticUnknown";
    empty = addLine();
    expect(saveButton().disabled).toBe(true);
    submit();
    await tick();
    expect(calls).toHaveLength(2);
    emptyLineIsOn(empty);

    // ── EK-2: the awareness moment follows the received moment until the user sets one.
    mount({ replies: [options()] });
    await ready();
    typeInto(byId("ReceivedAtUtc"), "2026-10-20T10:00");
    expect(byId("AwarenessAtUtc").value).toBe("2026-10-20T10:00");
    typeInto(byId("AwarenessAtUtc"), "2026-10-19T09:00");
    typeInto(byId("ReceivedAtUtc"), "2026-10-21T11:00");
    expect(byId("AwarenessAtUtc").value).toBe("2026-10-19T09:00");
  });

  const linkedDraft = overrides => ({
    reporterSource: "Crm",
    reporterSourceId: "c-1",
    awarenessAtUtc: "2026-10-20T10:00:30+03:00",
    eventCountry: "SyntheticUnknown",
    reportType: "SyntheticType",
    productLines: [{ productRole: "SyntheticRole", productNameAsReported: "synthetic-product", lotNumber: "synthetic-lot", globalProductId: null, presentationGskuId: null }],
    reporterNameOrInitials: "S.Y.",
    reporterProfession: "synthetic-profession",
    reporterCountry: "SyntheticUnknown",
    ...overrides
  });
  const recordAnswer = draft => answer(200, { outcome: "Succeeded", items: [{ intakeDraftId: "D-1", version: 7 }], draft });
  // jsdom answers a datetime-local value with seconds as "...:30.000"; a browser gives its shortest form, "...:30".
  const asTheBrowserWrites = value => value.replace(/\.000$/, "");
  const syntheticOptions = () => answer(200, { reportTypes: ["SyntheticType"], productRoles: ["SyntheticRole"], unknownCountry: "SyntheticUnknown" });

  it("pvg edit form sends back the stored reporter selection", async () => {
    let { calls } = mount({ mode: "edit", id: "D-1", replies: [syntheticOptions(), recordAnswer(linkedDraft()), saved()] });
    await ready();
    expect(asTheBrowserWrites(byId("AwarenessAtUtc").value)).toBe("2026-10-20T07:00:30");
    for (const id of ["ReporterNameOrInitials", "ReporterProfession", "ReporterCountry"]) {
      expect(byId(id).disabled, id).toBe(false);
      expect(byId(id).hasAttribute("readonly"), id).toBe(false);
    }

    fillRecordFields();
    byId("ReporterNameOrInitials").value = "S.Z.";
    submit();
    await until(() => calls.length === 3);
    expect(calls.map(call => call.url)).toEqual([
      "/Pharmacovigilance/CaseIntakeTriage/api/options",
      "/Pharmacovigilance/CaseIntakeTriage/api/detail/D-1",
      "/Pharmacovigilance/CaseIntakeTriage/api/update/D-1"
    ]);
    let body = calls[2].init.body;
    expect(body.get("ReporterSelectionDirectory")).toBe("Crm");
    expect(body.get("ReporterSelectionId")).toBe("c-1");
    expect(body.has("RemoveReporterLink")).toBe(false);
    expect(body.get("ExpectedVersion")).toBe("7");
    expect(asTheBrowserWrites(body.get("AwarenessAtUtc"))).toBe("2026-10-20T07:00:30");
    expect(body.get("EventCountry")).toBe("SyntheticUnknown");
    expect(body.get("ReportType")).toBe("SyntheticType");
    expect(body.get("ProductLines[0].ProductRole")).toBe("SyntheticRole");
    expect(body.get("ProductLines[0].ProductNameAsReported")).toBe("synthetic-product");
    expect(body.get("ProductLines[0].LotNumber")).toBe("synthetic-lot");
    expect(body.get("ReporterNameOrInitials")).toBe("S.Z.");
    expect(body.get("ReporterProfession")).toBe("synthetic-profession");
    expect(body.get("ReporterCountry")).toBe("SyntheticUnknown");
    expect(anyForbiddenKey(body)).toEqual([]);

    // The stored name, unedited, goes back as it was read.
    ({ calls } = mount({ mode: "edit", id: "D-1", replies: [syntheticOptions(), recordAnswer(linkedDraft()), saved()] }));
    await ready();
    fillRecordFields();
    submit();
    await until(() => calls.length === 3);
    expect(calls[2].init.body.get("ReporterNameOrInitials")).toBe("S.Y.");

    // A stored value the option list no longer holds stays selected and goes back as it is.
    ({ calls } = mount({
      mode: "edit", id: "D-1",
      replies: [syntheticOptions(), recordAnswer(linkedDraft({ reportType: "LegacyType", reporterCountry: "LegacyCountry" })), saved()]
    }));
    await ready();
    expect(optionValues(byId("ReportType"))).toContain("LegacyType");
    expect(byId("ReportType").value).toBe("LegacyType");
    expect(byId("ReporterCountry").value).toBe("LegacyCountry");
    fillRecordFields();
    submit();
    await until(() => calls.length === 3);
    expect(calls[2].init.body.get("ReportType")).toBe("LegacyType");
    expect(calls[2].init.body.get("ReporterCountry")).toBe("LegacyCountry");

    // A manual reporter sends neither.
    ({ calls } = mount({
      mode: "edit", id: "D-1",
      replies: [syntheticOptions(), recordAnswer(linkedDraft({ reporterSource: "Manual", reporterSourceId: null })), saved()]
    }));
    await ready();
    fillRecordFields();
    submit();
    await until(() => calls.length === 3);
    body = calls[2].init.body;
    expect(body.has("ReporterSelectionDirectory")).toBe(false);
    expect(body.has("ReporterSelectionId")).toBe(false);
  });

  it("pvg remove link action posts RemoveReporterLink without the selection pair", async () => {
    let { calls } = mount({ mode: "edit", id: "D-1", replies: [syntheticOptions(), recordAnswer(linkedDraft()), saved()] });
    await ready();
    const button = byId("pvg-remove-reporter-link");
    const note = byId("pvg-reporter-link-removal-note");
    expect(hidden(button)).toBe(false);
    expect(hidden(note)).toBe(true);

    button.click();
    expect(hidden(button)).toBe(true);
    expect(hidden(note)).toBe(false);
    for (const id of ["ReporterNameOrInitials", "ReporterProfession", "ReporterCountry"]) {
      expect(byId(id).disabled, id).toBe(false);
      expect(byId(id).hasAttribute("readonly"), id).toBe(false);
    }
    expect(byId("ReporterNameOrInitials").value).toBe("S.Y.");

    fillRecordFields();
    submit();
    await until(() => calls.length === 3);
    const body = calls[2].init.body;
    expect(body.get("RemoveReporterLink")).toBe("true");
    expect(body.has("ReporterSelectionDirectory")).toBe(false);
    expect(body.has("ReporterSelectionId")).toBe(false);

    // No record with no link offers the action.
    ({ calls } = mount({
      mode: "edit", id: "D-1",
      replies: [syntheticOptions(), recordAnswer(linkedDraft({ reporterSource: "Manual", reporterSourceId: null }))]
    }));
    await ready();
    expect(hidden(byId("pvg-remove-reporter-link"))).toBe(true);
  });

  it("pvg option lists come only from the proxy", async () => {
    // ── Drawn from the answer, in the answer's order, with a code only this test knows.
    let { calls } = mount({ replies: [options()] });
    expect(saveButton().disabled, "before the answer").toBe(true);
    await ready();
    expect(calls[0].url).toBe("/Pharmacovigilance/CaseIntakeTriage/api/options");
    expect(optionValues(byId("ReportType"))).toEqual(["", "Study", "Spontaneous", "SyntheticType"]);
    expect(optionValues(lineInput(addLine(), "ProductRole"))).toEqual(["", "Suspect", "Concomitant"]);

    // ── Source: nothing to fall back on in the markup, in any shape, in the module's scripts, view or models.
    const view = read(formView);
    const template = view.match(/<template\b[\s\S]*?<\/template>/)[0];
    onlyThePlaceholder(selectBlock(view, "ReportType"));
    onlyThePlaceholder(selectBlock(template, "ProductLines[__index__].ProductRole"));

    const codeWords = ["Spontaneous", "Study", "Literature", "Solicited", "Other", "Suspect", "Concomitant"];
    const modelDirectory = "Models/CaseIntakeTriage";
    const scanned = [
      ...fs.readdirSync(path.join(root, moduleJsDirectory)).filter(name => name.endsWith(".js")).map(name => `${moduleJsDirectory}/${name}`),
      ...fs.readdirSync(path.join(root, modelDirectory)).map(name => `${modelDirectory}/${name}`),
      formView
    ];
    for (const file of scanned) {
      const source = file === formView
        ? read(file).replace(/<select\b[^>]*name="SourceType"[^>]*>[\s\S]*?<\/select>/, "")
        : read(file);
      const found = codeWords.filter(word => new RegExp(`\\b${word}\\b`).test(source));
      expect(found.length < 2, `${file}: ${found.join(", ")}`).toBe(true);
    }

    // ── A failed read: the option inputs and Save off, the reader told, the record not read, no list made up.
    const optionInputs = () => ["ReportType", "EventCountry", "ReporterCountry"].map(byId);
    const closedAfterTheFailure = async (calls, expectedCalls) => {
      await until(() => alertShown());
      await tick();
      expect(calls).toHaveLength(expectedCalls);
      expect(alertBox().textContent).toBe(texts.OptionsLoadFailed);
      expect(saveButton().disabled).toBe(true);
      expect(byId("pvg-add-product-line").disabled).toBe(true);
      expect(rows()).toHaveLength(0);
      for (const input of optionInputs()) {
        expect(input.disabled, input.id).toBe(true);
        expect(optionValues(input), input.id).toEqual([""]);
      }
    };

    ({ calls } = mount({ replies: [answer(500, {})] }));
    expect(saveButton().disabled, "before the answer").toBe(true);
    await closedAfterTheFailure(calls, 1);

    // A direct submit is closed too, and leaves the inputs off.
    fillRecordFields();
    byId("AwarenessAtUtc").value = "2026-10-20T09:00";
    submit();
    await tick();
    expect(calls).toHaveLength(1);
    for (const input of optionInputs()) {
      expect(input.disabled, input.id).toBe(true);
    }

    ({ calls } = mount({ mode: "edit", id: "D-1", replies: [answer(500, {}), recordAnswer(linkedDraft())] }));
    await closedAfterTheFailure(calls, 1);

    // ── A 200 with a broken body is a failed read.
    for (const broken of [
      { reportTypes: ["Study"], unknownCountry: "unknown" },
      { reportTypes: ["Study"], productRoles: ["Suspect"], unknownCountry: 5 }
    ]) {
      ({ calls } = mount({ replies: [answer(200, broken)] }));
      await closedAfterTheFailure(calls, 1);
    }
  });

  it("pvg option without a resx name shows the code itself", async () => {
    mount({ replies: [options()] });
    await ready();
    const shown = (select, value) => [...select.options].find(option => option.value === value)?.textContent;
    expect(shown(byId("ReportType"), "SyntheticType")).toBe("SyntheticType");

    // The codes the service holds today each have their name in the bridge and in the seven resource files.
    const optionSets = fs.readFileSync(path.resolve(root, "../../services/Diten.PvgService/src/Diten.PvgService.Domain/RegPvBase/PvgIntakeOptionSets.cs"), "utf8");
    const codesOf = set => [...optionSets.match(new RegExp(`${set}\\s*\\{ get; \\}[^{]*\\{([^}]*)\\}`))[1].matchAll(/"([^"]+)"/g)].map(match => match[1]);
    const reportTypes = codesOf("ReportTypes");
    const productRoles = codesOf("ProductRoles");
    expect(reportTypes).toHaveLength(5);
    expect(productRoles).toHaveLength(2);
    const nameKeys = [...reportTypes.map(code => `ReportType${code}`), ...productRoles.map(code => `ProductRole${code}`), "CountryUnknown"];
    for (const key of nameKeys) {
      expect(l10nBridgeKeys().has(key), key).toBe(true);
      expect(indexL10nSource(), key).toContain(`${key} = Localizer["${key}"].Value`);
      for (const culture of pvgResourceCultures) {
        expect(resourceKeys(culture).has(key), `${culture}:${key}`).toBe(true);
      }
    }

    // A real code shows the page's name for it, not the code.
    const turkishName = read(pvgResourceFile("tr")).match(/<data name="ReportTypeSpontaneous"[^>]*>\s*<value>([^<]*)<\/value>/)[1];
    mount({
      replies: [answer(200, { reportTypes: ["Spontaneous"], productRoles: ["Suspect"], unknownCountry: "unknown" })],
      l10n: { ...texts, ReportTypeSpontaneous: turkishName }
    });
    await ready();
    expect(shown(byId("ReportType"), "Spontaneous")).toBe(turkishName);
    expect(turkishName).not.toBe("Spontaneous");
  });

  it("pvg event country offers only unknown while no country list exists", async () => {
    mount({ replies: [options()] });
    await ready();

    const eventCountry = byId("EventCountry");
    expect(optionValues(eventCountry)).toEqual(["", "SyntheticUnknown"]);
    expect(optionTexts(eventCountry)[1]).toBe(texts.CountryUnknown);
    expect(eventCountry.value).toBe("");

    const reporterCountry = byId("ReporterCountry");
    expect(reporterCountry.tagName).toBe("SELECT");
    expect(optionValues(reporterCountry)).toEqual(["", "SyntheticUnknown"]);
    expect(reporterCountry.value).toBe("");

    const view = read(formView);
    onlyThePlaceholder(selectBlock(view, "EventCountry"));
    onlyThePlaceholder(selectBlock(view, "ReporterCountry"));
    for (const name of ["EventCountry", "ReporterCountry"]) {
      expect(view, name).not.toMatch(new RegExp(`<(?:input|textarea)\\b[^>]*name="${name}"`));
    }
  });

  it("pvg bridge from the server's camelCase payload reaches the screen translated", async () => {
    // The server's shape (W13 measures it on the real view): every key with a lower-case first letter, each value its own.
    const said = key => `bridge value of ${key}`;
    const serverPayload = Object.fromEntries(
      [...l10nBridgeKeys()].map(key => [key.charAt(0).toLowerCase() + key.slice(1), said(key)])
    );
    document.body.innerHTML = `<script id="pvg-case-intake-triage-l10n" type="application/json">${JSON.stringify(serverPayload)}</script>`;
    const loaded = {};
    vm.runInNewContext(read(`${moduleJsDirectory}/index.l10n.js`), { window: loaded, document, JSON, Object, console });
    const l10n = loaded.PvgCaseIntakeTriageL10n;

    // Through the real form: the names the reader sees are the bridge's, and a code without one stays itself.
    mount({
      replies: [answer(200, { reportTypes: ["Spontaneous", "SyntheticType"], productRoles: ["Suspect"], unknownCountry: "unknown" })],
      l10n
    });
    await ready();
    const shown = (select, value) => [...select.options].find(option => option.value === value)?.textContent;
    expect(shown(byId("ReportType"), "Spontaneous")).toBe(said("ReportTypeSpontaneous"));
    expect(shown(byId("ReportType"), "SyntheticType")).toBe("SyntheticType");
    expect(shown(lineInput(addLine(), "ProductRole"), "Suspect")).toBe(said("ProductRoleSuspect"));
    expect(shown(byId("EventCountry"), "unknown")).toBe(said("CountryUnknown"));
    expect(shown(byId("ReporterCountry"), "unknown")).toBe(said("CountryUnknown"));

    // A failed read is a sentence, not the name of a key.
    mount({ replies: [answer(500, {})], l10n });
    await until(() => alertShown());
    expect(alertBox().textContent).toBe(said("OptionsLoadFailed"));
    expect(alertBox().textContent).not.toBe("OptionsLoadFailed");

    // Every key the module's three scripts read (form.js, index.js, details.js) comes out of the loader.
    for (const key of browserL10nReferences()) {
      expect(l10n[key], key).toBe(said(key));
    }
  });
});
