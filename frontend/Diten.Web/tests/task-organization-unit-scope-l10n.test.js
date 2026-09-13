const fs = require("fs");
const path = require("path");

/*
 * WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01 · BL-355 — the two new refusal codes reach the reader in their own
 * language, the same code→resx bridge every other Tasks refusal already goes through
 * (wcn-detail-three-regions.test.js's own pattern for ErrorTaskInvalidState).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const read = (...parts) => fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", ...parts), "utf8");

const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const KEYS = ["ErrorOrganizationUnitNotFound", "ErrorOrganizationUnitOutOfScope"];

describe("BL-355 organization-unit refusals are mapped and translated", () => {
  it("api.js maps both new server codes to a resx key", () => {
    const api = read("wwwroot", "assets", "js", "Tasks", "api.js");
    expect(api).toContain("TASK_ORGANIZATION_UNIT_NOT_FOUND: 'errorOrganizationUnitNotFound'");
    expect(api).toContain("TASK_ORGANIZATION_UNIT_OUT_OF_SCOPE: 'errorOrganizationUnitOutOfScope'");
  });

  it("_IndexL10n.cshtml exposes both keys to the client payload", () => {
    const bridge = read("Views", "Tasks", "_IndexL10n.cshtml");
    expect(bridge).toContain("ErrorOrganizationUnitNotFound");
    expect(bridge).toContain("ErrorOrganizationUnitOutOfScope");
  });

  it("all seven languages carry both keys with a real sentence", () => {
    const englishValues = extractValues(read("Resources", "Views", "Tasks", "TasksIndex.en.resx"));

    LANGS.forEach((lang) => {
      const resx = read("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`);
      const values = extractValues(resx);
      KEYS.forEach((key) => {
        expect(resx, `${lang} is missing ${key}`).toContain(`name="${key}"`);
        expect(values[key], `${lang}:${key} has no value`).toBeTruthy();
        expect(values[key], `${lang}:${key} still reads as the key`).not.toBe(key);
        if (lang !== "en") {
          expect(values[key], `${lang}:${key} is still the English text`).not.toBe(englishValues[key]);
        }
      });
    });
  });
});

function extractValues(resx) {
  const values = {};
  const re = /<data name="([^"]+)"[^>]*><value>([^<]*)<\/value><\/data>/g;
  let match;
  while ((match = re.exec(resx))) {
    values[match[1]] = match[2];
  }
  return values;
}
