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
const browserJsSource = () => [
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/details.js",
  "wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js"
].map(read).join("\n");

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

    for (const forbidden of ["delete", "bulk-delete", "archive", "void", "export"]) {
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

    expect(source).not.toMatch(/MOD-0231|MOD-0232|MOD-0234/i);
    expect(source).not.toMatch(/CaseProcessing|Meddra|MedDRA|SignalManagement|signal-management/i);
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
});
