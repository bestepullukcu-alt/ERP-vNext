const fs = require("fs");
const path = require("path");

describe("WorkCenterNext localization resources", () => {
  const resourceRoot = path.resolve(__dirname, "../Resources/Views/WorkCenterNext");
  const locales = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const names = (locale) => {
    const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
    return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
  };

  it("keeps exact seven-language key parity", () => {
    const baseline = names("en");
    locales.slice(1).forEach((locale) => expect(names(locale)).toEqual(baseline));
  });

  it("contains the canonical resolver and trigger surface keys", () => {
    const baseline = names("en");
    [
      "FixtureInvalidTitle", "MigrationAdaptedNotice", "ProviderCommandRequired",
      "SourceProjectionRequested", "TriggerOnlyLabel", "TriggerResponsesLabel",
      "NoticeWaiting", "NoticeSnoozed", "ActionDisabledStaleProjection"
    ].forEach((key) => expect(baseline).toContain(key));
  });
});
