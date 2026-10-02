const fs = require("fs");
const path = require("path");

/*
 * WP-USERS-ERROR-CODES-01 (BL-450) — every refusal of a Users-screen action reads as ONE sentence in the reader's
 * language. AuthService tags the refusal with a stable code; the screen (index.js ERROR_CODE_KEYS) turns the code into
 * a UsersIndex resx sentence. A refusal WITHOUT a code never shows the service's English text: the general error
 * goes on screen and the gap goes to the console. These tests run the PRODUCTION code sliced out of index.js against
 * the real resx files — never a copy of the map.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const js = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const resxValues = (lang) => {
  const xml = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
  const values = {};
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    values[m[1]] = m[2].replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">");
  }
  return values;
};

/** The module's own ERROR_CODE_KEYS … localizedErrors, evaluated in isolation with a given label set and console. */
const loadBridge = (labels, consoleStub) => {
  const source = js();
  const start = source.indexOf("const ERROR_CODE_KEYS");
  const end = source.indexOf("const submitForm");
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  // eslint-disable-next-line no-new-func
  return new Function("L", "console", source.slice(start, end) + "; return { ERROR_CODE_KEYS, localizedErrors };")(() => labels, consoleStub || console);
};

const quietConsole = () => ({ warn: vi.fn(), error: vi.fn(), log: vi.fn() });
const GENERAL = "«general error»";
const ENGLISH = "User has already completed setup.";

describe("a coded refusal reads in the reader's language", () => {
  const codes = Object.entries(loadBridge({}, quietConsole()).ERROR_CODE_KEYS);

  test("the screen knows more than the three codes it started with", () => {
    expect(codes.length).toBeGreaterThan(3);
  });

  test.each(LANGS)("[%s] every code becomes its own sentence — never the service's English, never the code", (lang) => {
    const labels = Object.assign({}, resxValues(lang), { ErrorOccurred: GENERAL });
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);
    const seen = new Set();

    for (const [code, key] of codes) {
      expect(labels[key], `UsersIndex.${lang}.resx lacks ${key} (${code})`).toBeTruthy();

      // The proxy's shape (create, edit, kebab actions) …
      const [viaProxy] = localizedErrors({ success: false, errors: [ENGLISH], errorCode: code, errorParams: null, rawText: true });
      // … and AuthService's own envelope (the delete goes straight to the gateway).
      const [viaEnvelope] = localizedErrors({ isSuccessful: false, statusCode: 409, errors: [ENGLISH], errorCodes: [{ code }] });

      expect(viaProxy, `${code} via the proxy`).toBe(labels[key]);
      expect(viaEnvelope, `${code} via the envelope`).toBe(labels[key]);
      expect(viaProxy).not.toContain(code);
      expect(viaProxy).not.toBe(GENERAL);
      expect(seen.has(viaProxy), `${code} shares its sentence with another code in ${lang}`).toBe(false);
      seen.add(viaProxy);
    }
    expect(consoleStub.warn).not.toHaveBeenCalled();
  });
});

describe("a refusal without a code never shows the service's sentence", () => {
  const labels = { ErrorOccurred: GENERAL };

  test("relayed English text from the proxy → the general error, and a console warning that names the gap", () => {
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);

    const errors = localizedErrors({ success: false, errors: [ENGLISH], errorCode: null, errorParams: null, rawText: true });

    expect(errors).toEqual([GENERAL]);
    expect(consoleStub.warn).toHaveBeenCalledTimes(1);
    expect(JSON.stringify(consoleStub.warn.mock.calls[0])).toContain(ENGLISH); // findable, just not on screen
  });

  test("a codeless envelope straight from the gateway (the delete) → the general error", () => {
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);

    expect(localizedErrors({ isSuccessful: false, statusCode: 400, errors: [ENGLISH] })).toEqual([GENERAL]);
    expect(localizedErrors({})).toEqual([GENERAL]); // a 403 with no body
    expect(consoleStub.warn).toHaveBeenCalledTimes(2);
  });

  test("a code the screen does not know → the general error, and the code is in the warning", () => {
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);

    expect(localizedErrors({ success: false, errors: [ENGLISH], errorCode: "USER_SOMETHING_NEW", rawText: true })).toEqual([GENERAL]);
    expect(JSON.stringify(consoleStub.warn.mock.calls[0])).toContain("USER_SOMETHING_NEW");
  });

  test("the proxy's OWN localized sentences (form validation, Unauthorized) still reach the reader", () => {
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);

    expect(localizedErrors({ success: false, errors: ["E-posta zorunludur."] })).toEqual(["E-posta zorunludur."]);
    expect(localizedErrors({ success: false, errors: ["Yetkisiz"], errorCode: null, errorParams: null, rawText: false })).toEqual(["Yetkisiz"]);
    expect(consoleStub.warn).not.toHaveBeenCalled();
  });

  test("the proxy says which text is relayed: gateway failures and exceptions carry rawText", () => {
    const controller = read("Controllers", "UsersController.cs");
    expect(controller).toMatch(/rawText = await IsRelayedTextAsync\(response\)/);
    expect(controller, "an exception message is raw text too").not.toMatch(/errors = BuildExceptionErrors\(ex\) \}\)/);
  });
});

describe("every door of the screen goes through the bridge", () => {
  test("a kebab action refused as 200 { success: false } is a failure, not a success toast", () => {
    const source = js();
    const run = source.slice(source.indexOf("const runAdminAction"), source.indexOf("const adminAction ="));
    expect(run).toMatch(/if \(!res\.ok \|\| json\.success === false\) throw new Error\(localizedErrors\(json\)\[0\]/);
  });

  test("the delete reads the refusal's code instead of always saying the general error", () => {
    const source = js();
    const del = source.slice(source.indexOf("const deleteRow"), source.indexOf("const renderRowActions"));
    expect(del).toMatch(/if \(!res\.ok\) throw new Error\(localizedErrors\(await res\.json\(\)\.catch\(\(\) => \(\{\}\)\)\)\[0\]/);
    expect(del).toMatch(/showToast\?\.\(error\.message \|\| L\(\)\.ErrorOccurred, 'error'\)/);
    expect(del, "a hard-coded English failure").not.toContain("'Delete failed.'");
  });

  test("the form shows the bridge's sentence", () => {
    const source = js();
    const submit = source.slice(source.indexOf("const submitForm"), source.indexOf("const showInviteLink"));
    expect(submit).toMatch(/if \(!json\.success\) return Object\.assign\(\{\}, json, \{ errors: localizedErrors\(json\) \}\);/);
  });
});
