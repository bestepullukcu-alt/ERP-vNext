const fs = require("fs");
const path = require("path");

/*
 * BL-459 — the plan's user limit on the tenant Users screen. AuthService refuses a create at the limit with
 * USER_QUOTA_EXCEEDED and, when Platform told it the numbers, params { max, current }; the Users proxy hands both over
 * (errorCode, errorParams). These tests run the PRODUCTION localizedErrors from index.js (sliced out of the IIFE, the
 * way governance-users-invited-lifecycle.test.js runs userStatusOf) against the real resx texts in all seven languages.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const resxValue = (lang, key) => {
  const xml = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
  const match = xml.match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">") : null;
};

/** The module's own ERROR_CODE_KEYS … localizedErrors, evaluated in isolation with a given label set. */
const loadLocalizedErrors = (labels) => {
  const source = read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
  const start = source.indexOf("const ERROR_CODE_KEYS");
  const end = source.indexOf("const submitForm");
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  // eslint-disable-next-line no-new-func
  return new Function("L", source.slice(start, end) + "; return localizedErrors;")(() => labels);
};

const labelsFor = (lang) => ({
  ErrorOccurred: "error",
  ErrorUserQuotaExceeded: resxValue(lang, "ErrorUserQuotaExceeded"),
  ErrorUserQuotaUsage: resxValue(lang, "ErrorUserQuotaUsage")
});

describe("USER_QUOTA_EXCEEDED on the Users screen", () => {
  test.each(LANGS)("[%s] the refusal reads in the reader's language, with the numbers", (lang) => {
    const labels = labelsFor(lang);
    expect(labels.ErrorUserQuotaExceeded, `UsersIndex.${lang}.resx ErrorUserQuotaExceeded`).toBeTruthy();
    expect(labels.ErrorUserQuotaUsage, `UsersIndex.${lang}.resx ErrorUserQuotaUsage`).toMatch(/\{current\}[\s\S]*\{max\}|\{max\}[\s\S]*\{current\}/);

    const [text] = loadLocalizedErrors(labels)({
      success: false,
      ownMessages: [], // the proxy relays no service sentence (WP-USERS-ERROR-CODES-01): the code and its params only
      errorCode: "USER_QUOTA_EXCEEDED",
      errorParams: { max: "25", current: "25" }
    });

    expect(text.startsWith(labels.ErrorUserQuotaExceeded)).toBe(true);
    expect(text).toContain("25");
    expect(text).not.toMatch(/\{(max|current)\}/);
    if (lang !== "en") expect(text).not.toContain("subscription plan's user limit is reached"); // not the English fallback
  });

  test("without the numbers the sentence stands alone — never a raw placeholder", () => {
    const labels = labelsFor("tr");
    const [text] = loadLocalizedErrors(labels)({ success: false, ownMessages: [], errorCode: "USER_QUOTA_EXCEEDED", errorParams: null });

    expect(text).toBe(labels.ErrorUserQuotaExceeded);
  });

  test("the screen publishes both keys and the proxy hands the params over", () => {
    const bridge = read("Views", "Governance", "Users", "_IndexL10n.cshtml");
    expect(bridge).toMatch(/ErrorUserQuotaExceeded = Localizer\["ErrorUserQuotaExceeded"\]\.Value/);
    expect(bridge).toMatch(/ErrorUserQuotaUsage = Localizer\["ErrorUserQuotaUsage"\]\.Value/);
    expect(read("Controllers", "UsersController.cs")).toMatch(/errorParams = codes\.Count > 0 \? codes\[0\]\.Params : null/);
  });
});
