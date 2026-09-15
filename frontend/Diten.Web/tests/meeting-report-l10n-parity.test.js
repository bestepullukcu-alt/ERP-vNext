const fs = require("fs");
const path = require("path");

/*
 * MOD-0357 S12 (pack §23.8) — the report screen's own resx set carries the SAME key set in all seven
 * languages, and the l10n payload (`_IndexL10n.cshtml`) actually reads every one of them — the same guard
 * shape `meetings-reason-code-bridge.test.js` already uses for the Meetings module's error-code bridge.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Meetings", "Report");
const L10N_CSHTML = path.join(repoRoot, "frontend", "Diten.Web", "Views", "Meetings", "Report", "_IndexL10n.cshtml");
const SHARED_RESOURCE_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const keysOf = (resxText) => Array.from(resxText.matchAll(/<data name="([^"]+)"/g)).map(([, name]) => name).sort();

describe("MOD-0357 S12: MeetingReportIndex resx carries the same key set in all seven languages", () => {
  const enKeys = keysOf(fs.readFileSync(path.join(RESX_DIR, "MeetingReportIndex.en.resx"), "utf8"));

  it("the English set is not empty, so the parity check below is not vacuous", () => {
    expect(enKeys.length).toBeGreaterThan(0);
  });

  it.each(LANGUAGES)("%s carries exactly the English key set", (language) => {
    const text = fs.readFileSync(path.join(RESX_DIR, `MeetingReportIndex.${language}.resx`), "utf8");
    expect(keysOf(text)).toEqual(enKeys);
  });

  it.each(enKeys)("_IndexL10n.cshtml reads Localizer[\"%s\"] or SharedLocalizer[\"%s\"]", (key) => {
    const payload = fs.readFileSync(L10N_CSHTML, "utf8");
    const readsFromModule = payload.includes(`Localizer["${key}"]`);
    expect(
      readsFromModule,
      `${key} exists in the resx but _IndexL10n.cshtml never reads it — a key that reaches nobody.`
    ).toBe(true);
  });
});

describe("MOD-0357 S12: the MEETING_REPORT nav page is localized in all seven languages", () => {
  it.each(LANGUAGES)("SharedResource.%s.resx carries Nav.Page.MEETINGREPORT", (language) => {
    const text = fs.readFileSync(path.join(SHARED_RESOURCE_DIR, `SharedResource.${language}.resx`), "utf8");
    expect(text).toContain('name="Nav.Page.MEETINGREPORT"');
  });
});
