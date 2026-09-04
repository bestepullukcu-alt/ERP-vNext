const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * "IS THIS RESPONSE JSON?" — ASKED ONCE, ASKED CORRECTLY.
 *
 * WHAT WAS WRONG. Four scripts asked this question and all four asked it as a SUBSTRING TEST:
 * `contentType.includes('application/json')`. Measured 2026-08-30:
 * `'application/problem+json'.includes('application/json')` is FALSE — the substring simply does not occur.
 *
 * `+json` is RFC 6839's structured syntax suffix, the standard way a media type says "I am carried as JSON".
 * `application/problem+json` (RFC 9457 problem details) is the one this repository actually serves. So the
 * substring test was ALREADY WRONG before any server changed: it rejects a body it can parse perfectly well.
 *
 * WHY THIS MATTERED HERE AND NOT ONLY IN THEORY. The Platform Administrators screen reads its error messages
 * through that gate, and the gateway's TenantResolutionMiddleware refuses its routes with problem+json. The
 * gate said "not JSON", the code fell through to `response.text()`, and the user got a RAW JSON DOCUMENT in
 * the toast instead of the localized sentence inside it. That is the one real user-visible behaviour this
 * round could have broken, so it is measured end-to-end below rather than assumed.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const loadRule = () => {
  delete window.DitenHttp;
  loadScript("wwwroot/assets/js/shared/http-media-type.js");
};

describe("DitenHttp.isJsonMediaType — the one shared rule", () => {
  beforeEach(loadRule);

  /*
   * ⚠ THE FIRST TWO ROWS ARE THE WHOLE POINT. `application/json` must keep working (nothing regresses) and
   * `application/problem+json` must start working (the defect). The vendor row proves the rule is the SUFFIX,
   * not a hardcoded list of two known types.
   */
  test.each([
    ["application/json", true],
    ["application/problem+json", true],
    ["application/vnd.foo+json", true],
    ["application/json; charset=utf-8", true],
    ["application/problem+json; charset=utf-8", true],
    ["APPLICATION/PROBLEM+JSON", true],
    ["text/html", false],
    ["application/xml", false],
    ["", false],
    // ⚠ THE BOUNDARY. A looser rule — a substring `json` test, or /json/ — says YES to this. JSONP is a
    // DIFFERENT format that must never be handed to JSON.parse, so the suffix rule is the correct one and
    // this row is what stops the next person from "simplifying" it into a substring test again.
    ["application/jsonp", false]
  ])("%s -> %s", (contentType, expected) => {
    expect(window.DitenHttp.isJsonMediaType(contentType)).toBe(expected);
  });

  test("a missing header is not JSON, and does not throw", () => {
    expect(window.DitenHttp.isJsonMediaType(null)).toBe(false);
    expect(window.DitenHttp.isJsonMediaType(undefined)).toBe(false);
  });

  test("a bare word is not a media type", () => {
    expect(window.DitenHttp.isJsonMediaType("json")).toBe(false);
    expect(window.DitenHttp.isJsonMediaType("application/")).toBe(false);
  });
});

/*
 * EVERY CALLER USES THE SHARED RULE — no script keeps a private copy.
 *
 * This is the test that stops the defect from growing back. It was four independent substring tests that made
 * this a four-site problem in the first place; a fifth would be just as invisible.
 */
describe("no script re-implements the media-type rule", () => {
  const CALLERS = [
    ["dt-defaults.js"],
    ["personalization-client.js"],
    ["Platform", "Tenants", "details.js"],
    ["Platform", "Administrators", "index.js"]
  ];

  test.each(CALLERS)("%s asks DitenHttp instead of matching a substring", (...parts) => {
    const source = read("wwwroot", "assets", "js", ...parts);
    const code = source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");

    expect(code).toContain("DitenHttp.isJsonMediaType");
    expect(code).not.toMatch(/includes\(['"]application\/json/);
    expect(code).not.toMatch(/indexOf\(['"]application\/json/);
  });
});

/*
 * END TO END, ON THE REAL SOURCE — not a copy of it.
 *
 * `readErrorMessage` lives inside the AdministratorsList closure and is not exported, and booting that whole
 * screen needs DataTables plus the personalization client. So the two functions are lifted OUT OF THE SHIPPED
 * FILE BY TEXT and evaluated here. That keeps the thing under test the actual shipped code: if either function
 * is renamed or moved, the extraction fails loudly instead of quietly testing a stale duplicate.
 */
const extractErrorReading = () => {
  const source = read("wwwroot", "assets", "js", "Platform", "Administrators", "index.js");
  const start = source.indexOf("const readErrorMessage");
  const end = source.indexOf("const deleteOne");

  if (start < 0 || end < 0 || end <= start) {
    throw new Error(
      "Administrators/index.js no longer contains readErrorMessage..deleteOne — update this extraction."
    );
  }

  const snippet = source.slice(start, end);
  // eslint-disable-next-line no-new-func
  return new Function("L", `${snippet}\nreturn { readErrorMessage, localizeServerError };`);
};

describe("Platform Administrators error toast survives a problem+json refusal", () => {
  let readErrorMessage;

  const L = {
    ErrorOccurred: "Bir hata oluştu.",
    AdminSelfActionDenied: "Kendi hesabınızda bu işlemi yapamazsınız."
  };

  /** The refusal exactly as the gateway writes it and AdministratorsController forwards it. */
  const refusal = (contentType, body) => ({
    ok: false,
    status: 403,
    statusText: "Forbidden",
    headers: { get: (name) => (name.toLowerCase() === "content-type" ? contentType : null) },
    json: async () => JSON.parse(body),
    text: async () => body
  });

  const TENANT_REFUSAL = JSON.stringify({
    title: "Forbidden Actor",
    status: 403,
    detail: "Platform admin or partner admin token is required.",
    traceId: "trace-1"
  });

  beforeEach(() => {
    loadRule();
    ({ readErrorMessage } = extractErrorReading()(L));
  });

  /*
   * THE REGRESSION THIS ROUND COULD HAVE CAUSED. The server now declares problem+json; this asserts the
   * screen still reads the sentence out of the document instead of dumping the document at the user.
   */
  test("a problem+json refusal yields the detail, not the raw JSON document", async () => {
    const message = await readErrorMessage(refusal("application/problem+json", TENANT_REFUSAL), "fallback");

    expect(message).toBe("Platform admin or partner admin token is required.");

    // ⚠ Asserted separately and on purpose: "shows the raw document" is the exact failure mode of the old
    // substring gate, and it is not implied by the equality above once someone edits the message plumbing.
    expect(message).not.toContain("traceId");
    expect(message).not.toContain("{");
  });

  test("localization still runs on a problem+json refusal", async () => {
    const body = JSON.stringify({
      title: "Forbidden",
      status: 403,
      detail: "You cannot perform this action on your own account."
    });

    const message = await readErrorMessage(refusal("application/problem+json", body), "fallback");

    expect(message).toBe(L.AdminSelfActionDenied);
  });

  /* THE CONTROL — plain application/json was working before and must still work. */
  test("a plain application/json refusal is unchanged", async () => {
    const body = JSON.stringify({ errors: ["At least one active Super Admin must remain in the system."] });
    const message = await readErrorMessage(refusal("application/json; charset=utf-8", body), "fallback");

    expect(message).toBe("At least one active Super Admin must remain in the system.");
  });

  /*
   * THE OTHER CONTROL. Without it this suite would pass against a gate that simply said "always JSON" —
   * an HTML error page must still fall through to the text path rather than being handed to JSON.parse.
   */
  test("an HTML error page still takes the non-JSON path", async () => {
    const message = await readErrorMessage(refusal("text/html", "<html>502</html>"), "fallback");

    expect(message).toBe("<html>502</html>");
  });
});
