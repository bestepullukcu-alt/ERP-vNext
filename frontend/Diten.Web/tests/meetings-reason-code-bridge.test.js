const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0357 S3, AC6 — the BL-040/BL-351 pattern (validation-reason-code-bridge.test.js), applied to
 * Diten.Platform's own MEETING_* and MEETING_TYPE_* reason codes instead of a derived VALIDATION_* code.
 *
 * THE CHAIN:
 *   MeetingReasonCodes (C#, Diten.Platform)  →  REASON_CODE_MESSAGE_KEYS (Meetings/api.js)
 *                                             →  the l10n payload (_IndexL10n.cshtml)
 *                                             →  resx, all seven languages
 *
 * Every one of Platform's own reason codes must reach the reader as a real sentence — a code the client does
 * not recognise degrades to "an error occurred", which is honest but not what BL-351 calls a fix twice already:
 * a code silently dropped from the map is indistinguishable from one that was never written.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const API_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Meetings", "api.js");
const PAYLOAD = path.join(repoRoot, "frontend", "Diten.Web", "Views", "Meetings", "_IndexL10n.cshtml");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Meetings");
const PLATFORM_MODELS = path.join(
  repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application",
  "Features", "Meetings", "MeetingModels.cs"
);
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** Every `public const string X = "MEETING_..."` / `"MEETING_TYPE_..."` value Platform's MeetingReasonCodes
 * class actually declares — read from the C# source itself, never hand-copied, so a new code added there is
 * covered here the moment it exists. */
const platformReasonCodes = () => {
  const source = fs.readFileSync(PLATFORM_MODELS, "utf8");
  const classStart = source.indexOf("class MeetingReasonCodes");
  expect(classStart, "MeetingReasonCodes is not declared in MeetingModels.cs").toBeGreaterThan(-1);
  const classBody = source.slice(classStart, source.indexOf("\n}", classStart));

  return Array.from(classBody.matchAll(/public const string \w+ = "([A-Z_]+)";/g)).map(([, code]) => code);
};

/** The client's own map: reason code → message key, read straight from api.js. */
const clientReasonCodeMap = () => {
  const source = fs.readFileSync(API_JS, "utf8");
  const start = source.indexOf("REASON_CODE_MESSAGE_KEYS");
  expect(start, "REASON_CODE_MESSAGE_KEYS is not declared in Meetings/api.js").toBeGreaterThan(-1);
  const objectText = source.slice(start, source.indexOf("};", start));

  const entries = Array.from(objectText.matchAll(/\b([A-Z_]+):\s*'([A-Za-z0-9_]+)'/g))
    .map(([, code, messageKey]) => ({ code, messageKey }));
  return entries;
};

const resxName = (messageKey) => messageKey.charAt(0).toUpperCase() + messageKey.slice(1);

const bootApi = () => {
  delete global.MeetingsApi;
  global.MeetingsL10n = { t: (key) => key };
  loadScript("wwwroot/assets/js/Meetings/api.js");
};

describe("AC6: every Platform MEETING_*/MEETING_TYPE_* reason code reaches the client", () => {
  it("declares at least one reason code, so the exhaustiveness check below is not vacuous", () => {
    expect(platformReasonCodes().length).toBeGreaterThan(0);
  });

  it("maps EVERY reason code Platform declares — none silently unmapped", () => {
    const platform = platformReasonCodes();
    const client = clientReasonCodeMap().map((e) => e.code);
    const missing = platform.filter((code) => !client.includes(code));

    expect(missing, `Platform declares these codes but Meetings/api.js does not map them: ${missing.join(", ")}`)
      .toEqual([]);
  });

  it("declares NO reason code the client maps but Platform does not (a stale/typo'd code)", () => {
    const platform = platformReasonCodes();
    const client = clientReasonCodeMap().map((e) => e.code);
    const stale = client.filter((code) => !platform.includes(code));

    expect(stale, `Meetings/api.js maps these codes but MeetingReasonCodes does not declare them: ${stale.join(", ")}`)
      .toEqual([]);
  });

  it("never maps the SAME reason code twice (BL-351: a repeated key silently drops the first sentence)", () => {
    const source = fs.readFileSync(API_JS, "utf8");
    const start = source.indexOf("REASON_CODE_MESSAGE_KEYS");
    const objectText = source.slice(start, source.indexOf("};", start));
    const codes = Array.from(objectText.matchAll(/\b([A-Z_]+):\s*'[A-Za-z0-9_]+'/g)).map(([, code]) => code);
    const duplicates = codes.filter((code, index) => codes.indexOf(code) !== index);

    expect(duplicates, `these reason codes are mapped more than once: ${duplicates.join(", ")}`).toEqual([]);
  });
});

describe("AC6: every mapped message key is actually deliverable", () => {
  beforeEach(bootApi);

  it("turns a mapped reason code into its own sentence, not the generic one", () => {
    const message = global.MeetingsApi.failureMessage({ ok: false, status: 400, reasonCode: "MEETING_END_BEFORE_START" });
    expect(message).toBe("errorEndBeforeStart");
    expect(message).not.toBe("errorOccurred");
  });

  it.each(clientReasonCodeMap())("reaches the page payload: $code", ({ messageKey }) => {
    const payload = fs.readFileSync(PAYLOAD, "utf8");
    expect(payload).toContain(`${resxName(messageKey)} = Localizer["${resxName(messageKey)}"]`);
  });

  it.each(clientReasonCodeMap())("is translated in all seven languages: $code", ({ messageKey }) => {
    const missing = LANGUAGES.filter((language) => {
      const file = path.join(RESX_DIR, `MeetingsIndex.${language}.resx`);
      return !fs.readFileSync(file, "utf8").includes(`name="${resxName(messageKey)}"`);
    });

    expect(missing, `${messageKey} missing in: ${missing.join(", ")}`).toEqual([]);
  });
});
