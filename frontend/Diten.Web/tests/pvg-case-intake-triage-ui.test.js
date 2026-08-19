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
    expect(indexView).toMatch(/id="skeleton-loader"/);
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

    expect(list).toMatch(/url:\s*buildListUrl\(\)/);
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
