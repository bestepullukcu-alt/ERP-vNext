const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-040 / BL-048 — a DERIVED validation reason code becomes a sentence in the reader's own language.
 *
 * THE CHAIN, and every link is real somewhere:
 *   validator rule  →  ValidationFailure(PropertyName, ErrorCode)
 *                   →  ValidationReasonCode.From  →  "VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH"
 *                   →  reason_code on the wire     (asserted in ValidationReasonCodeTests, C# side)
 *                   →  REASON_CODE_MESSAGE_KEYS    (asserted here)
 *                   →  resx, seven languages       (asserted here)
 *
 * THE LESSON THIS FILE APPLIES: the server half being right is not the same as the reader getting it. A code
 * that reaches the browser and maps to nothing shows the generic "an error occurred" — better than untranslatable
 * English, but not the sentence the user needs. So the assertions below are on the CONSUMER's dictionary and on
 * the resx files, not on the payload.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const API_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Tasks", "api.js");
const PAYLOAD = path.join(repoRoot, "frontend", "Diten.Web", "Views", "Tasks", "_IndexL10n.cshtml");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Tasks");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** The derived codes the client claims to understand, with the message key each maps to. */
const derivedMappings = () => {
  const source = fs.readFileSync(API_JS, "utf8");
  const start = source.indexOf("REASON_CODE_MESSAGE_KEYS");
  expect(start, "REASON_CODE_MESSAGE_KEYS is not declared in Tasks/api.js").toBeGreaterThan(-1);

  return Array.from(source.matchAll(/\b(VALIDATION_[A-Z0-9_]+)\s*:\s*'([A-Za-z0-9_]+)'/g))
    .map(([, code, messageKey]) => ({ code, messageKey }));
};

/** camelCase message key → the PascalCase resx name the payload reads it from. */
const resxName = (messageKey) => messageKey.charAt(0).toUpperCase() + messageKey.slice(1);

const bootApi = () => {
  delete global.TasksApi;
  // Echoes the key back, so an assertion naming a message key asserts the key the code chose rather than a
  // translation that could drift independently.
  global.TasksL10n = { t: (key) => key };
  loadScript("wwwroot/assets/js/Tasks/api.js");
};

describe("BL-040: the client understands a derived validation code", () => {
  beforeEach(bootApi);

  it("turns the title-length code into its own message, not the generic one", () => {
    /*
     * BL-048's exact measurement, from the other end. The server sentence that carried the untranslated field
     * name ("'Request Title', 200 karakterden küçük veya eşit olmalıdır") is never rendered — failureMessage
     * shows only what the code maps to — so the raw name cannot reach the reader through this path.
     */
    const message = global.TasksApi.failureMessage({
      ok: false, status: 400, reasonCode: "VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH"
    });

    expect(message).toBe("errorTitleTooLong");
    expect(message).not.toBe("errorOccurred");
  });

  it("tells a missing title apart from an over-long one", () => {
    // Two rules on one field are two sentences in every language; one code for both would make them
    // indistinguishable on screen.
    const empty = global.TasksApi.failureMessage({
      ok: false, status: 400, reasonCode: "VALIDATION_REQUEST_TITLE_NOT_EMPTY"
    });

    expect(empty).toBe("errorTitleRequired");
    expect(empty).not.toBe("errorTitleTooLong");
  });

  it("still degrades LOUDLY for a derived code nobody has mapped yet", () => {
    /*
     * NON-VACUITY, and the designed behaviour. 150 validators now emit codes and only a few are mapped, so the
     * unmapped path is the common one — it must stay generic-but-noisy rather than silently look like an
     * ordinary failure. This is what tells the next person which code to map.
     */
    const warnings = [];
    const originalWarn = global.console.warn;
    global.console.warn = (message) => warnings.push(String(message));

    try {
      const message = global.TasksApi.failureMessage({
        ok: false, status: 400, reasonCode: "VALIDATION_REQUEST_DESCRIPTION_MAXIMUM_LENGTH"
      });

      expect(message).toBe("errorOccurred");
      expect(warnings.join(" ")).toContain("VALIDATION_REQUEST_DESCRIPTION_MAXIMUM_LENGTH");
    } finally {
      global.console.warn = originalWarn;
    }
  });
});

describe("BL-040: every mapped code is actually deliverable", () => {
  it("maps at least one derived code, so the block below is not empty", () => {
    // Non-vacuity for the per-code checks: an empty mapping list would make all of them pass.
    expect(derivedMappings().length).toBeGreaterThan(0);
  });

  it.each(derivedMappings())("reaches the page dictionary: $code", ({ messageKey }) => {
    /*
     * The delivery link that has bitten this repository twice: a key that exists in the resx but is never put
     * into the page payload reaches the browser as its own name. The payload is server-rendered, so this is the
     * only place a JS test can see it.
     */
    const payload = fs.readFileSync(PAYLOAD, "utf8");
    expect(payload).toContain(`${resxName(messageKey)} = Localizer["${resxName(messageKey)}"]`);
  });

  it.each(derivedMappings())("is translated in all seven languages: $code", ({ messageKey }) => {
    const missing = LANGUAGES.filter((language) => {
      const file = path.join(RESX_DIR, `TasksIndex.${language}.resx`);
      return !fs.readFileSync(file, "utf8").includes(`name="${resxName(messageKey)}"`);
    });

    expect(missing, `${messageKey} missing in: ${missing.join(", ")}`).toEqual([]);
  });
});
