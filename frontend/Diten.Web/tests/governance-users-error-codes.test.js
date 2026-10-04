const fs = require("fs");
const path = require("path");

/*
 * WP-USERS-ERROR-CODES-01 (BL-450) — every refusal of a Users-screen action reads in the reader's language.
 * AuthService tags a refusal with stable codes; the MVC proxy hands over ALL of them and nothing it did not write
 * ({ success:false, ownMessages:[its own sentences], errorCodes:[{code,params}], uncoded, status }); the screen
 * (index.js ERROR_CODE_KEYS) turns each code into a UsersIndex resx sentence. A failure without a code is never
 * dropped and never shown in English: the general sentence goes on screen, the gap goes to the console — by code and
 * HTTP status only. These tests run the PRODUCTION code sliced out of index.js against the real resx files; the
 * proxy shapes used here are the ones UsersLifecycleErrorCodeProxyTests pins on the real controller.
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

const slice = (from, to) => {
  const source = js();
  const start = source.indexOf(from);
  const end = source.indexOf(to);
  expect(start, `index.js lacks "${from}"`).toBeGreaterThan(-1);
  expect(end, `index.js lacks "${to}"`).toBeGreaterThan(start);
  return source.slice(start, end);
};
const BRIDGE = () => slice("const ERROR_CODE_KEYS", "const submitForm");

/** The module's own ERROR_CODE_KEYS … localizedErrors, evaluated in isolation with a given label set and console. */
const loadBridge = (labels, consoleStub) =>
  // eslint-disable-next-line no-new-func
  new Function("L", "console", BRIDGE() + "; return { ERROR_CODE_KEYS, localizedErrors };")(() => labels, consoleStub || console);

const quietConsole = () => ({ warn: vi.fn(), error: vi.fn(), log: vi.fn() });
const GENERAL = "«general error»";
const ENGLISH = "User has already completed setup.";
/** What the proxy answers for a refused hop (UsersController.Refusal). */
const proxied = (codes, extra) => Object.assign({
  success: false, ownMessages: [], errorCode: codes.length ? codes[0] : null, errorParams: null,
  errorCodes: codes.map((code) => ({ code, params: null })), uncoded: false, status: 409
}, extra || {});

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
      const viaProxy = localizedErrors(proxied([code]));
      // … and AuthService's own envelope (the delete goes straight to the gateway).
      const viaEnvelope = localizedErrors({ isSuccessful: false, statusCode: 409, errors: [ENGLISH], errorCodes: [{ code }] });

      expect(viaProxy, `${code} via the proxy`).toEqual([labels[key]]);
      expect(viaEnvelope, `${code} via the envelope`).toEqual([labels[key]]);
      expect(viaProxy[0]).not.toContain(code);
      expect(seen.has(viaProxy[0]), `${code} shares its sentence with another code in ${lang}`).toBe(false);
      seen.add(viaProxy[0]);
    }
    expect(consoleStub.warn).not.toHaveBeenCalled();
  });

  test.each(LANGS)("[%s] the form's own refusal (three spaces typed as the first name) is the first-name sentence", (lang) => {
    const labels = Object.assign({}, resxValues(lang), { ErrorOccurred: GENERAL });
    // Exactly what UsersController.FormRefusal answers for FirstName = "   ".
    const answer = { success: false, ownMessages: [], errorCode: "USER_FIRST_NAME_REQUIRED", errorParams: null, errorCodes: [{ code: "USER_FIRST_NAME_REQUIRED", params: null }], uncoded: false, status: null };

    expect(loadBridge(labels, quietConsole()).localizedErrors(answer)).toEqual([labels.ErrorUserFirstNameRequired]);
    if (lang !== "en") expect(labels.ErrorUserFirstNameRequired).not.toBe(resxValues("en").ErrorUserFirstNameRequired);
  });
});

describe("every code of a refusal is said, and no failure is dropped in silence", () => {
  const labels = Object.assign({}, resxValues("tr"), { ErrorOccurred: GENERAL });

  test("two codes → two sentences, in order", () => {
    const consoleStub = quietConsole();
    const errors = loadBridge(labels, consoleStub).localizedErrors(proxied(["USER_FIRST_NAME_REQUIRED", "USER_LAST_NAME_REQUIRED"], { status: 400 }));

    expect(errors).toEqual([labels.ErrorUserFirstNameRequired, labels.ErrorUserLastNameRequired]);
    expect(consoleStub.warn).not.toHaveBeenCalled();
  });

  test("a coded failure plus an uncoded one → its sentence AND 'one more problem' (not the system-failure sentence)", () => {
    const consoleStub = quietConsole();
    const errors = loadBridge(labels, consoleStub).localizedErrors(proxied(["USER_FIRST_NAME_REQUIRED"], { uncoded: true, status: 400 }));

    expect(errors).toEqual([labels.ErrorUserFirstNameRequired, labels.ErrorUserOneMoreProblem]);
    expect(errors).not.toContain(GENERAL);
    expect(consoleStub.warn).toHaveBeenCalledTimes(1);
  });

  test("a code the screen does not know → the known sentence, the general one, and the code in the warning", () => {
    const consoleStub = quietConsole();
    const errors = loadBridge(labels, consoleStub).localizedErrors(proxied(["USER_NOT_FOUND", "USER_SOMETHING_NEW"]));

    expect(errors).toEqual([labels.ErrorUserNotFound, labels.ErrorUserOneMoreProblem]);
    expect(JSON.stringify(consoleStub.warn.mock.calls[0])).toContain("USER_SOMETHING_NEW");
  });

  test.each(LANGS)("[%s] 'one more problem' has its own sentence — neither the general error nor a copy of English", (lang) => {
    const values = resxValues(lang);
    expect(values.ErrorUserOneMoreProblem, `UsersIndex.${lang}.resx lacks ErrorUserOneMoreProblem`).toBeTruthy();
    if (lang !== "en") expect(values.ErrorUserOneMoreProblem).not.toBe(resxValues("en").ErrorUserOneMoreProblem);

    const labelsOf = Object.assign({}, values, { ErrorOccurred: GENERAL });
    expect(loadBridge(labelsOf, quietConsole()).localizedErrors(proxied(["USER_LAST_NAME_REQUIRED"], { uncoded: true })))
      .toEqual([values.ErrorUserLastNameRequired, values.ErrorUserOneMoreProblem]);
    // Standing alone (nothing coded was said), the gap is still the general sentence.
    expect(loadBridge(labelsOf, quietConsole()).localizedErrors(proxied([], { uncoded: true }))).toEqual([GENERAL]);
  });

  test("the bridge publishes the sentence to the screen", () => {
    expect(read("Views", "Governance", "Users", "_IndexL10n.cshtml")).toMatch(/ErrorUserOneMoreProblem = Localizer\["ErrorUserOneMoreProblem"\]\.Value/);
  });
});

describe("the screen never reads `errors`", () => {
  const labels = { ErrorOccurred: GENERAL };
  const SERVICE_TEXT = "English service text from an upstream";

  test.each([
    ["the proxy's shape", { success: false, errors: [SERVICE_TEXT], ownMessages: [], errorCode: null, errorParams: null, errorCodes: [], uncoded: true, status: 400 }],
    ["the proxy's shape, claiming nothing is uncoded", { success: false, errors: [SERVICE_TEXT], ownMessages: [], errorCodes: [], uncoded: false, status: 400 }],
    ["a bare { success:false, errors }", { success: false, errors: [SERVICE_TEXT] }],
    ["AuthService's envelope", { isSuccessful: false, statusCode: 409, errors: [SERVICE_TEXT], errorCodes: [] }]
  ])("%s: a sentence in `errors` reaches neither the screen nor the console", (_name, answer) => {
    const consoleStub = quietConsole();

    const shown = loadBridge(labels, consoleStub).localizedErrors(answer);

    expect(shown).toEqual([GENERAL]);
    expect(JSON.stringify(consoleStub.warn.mock.calls)).not.toContain(SERVICE_TEXT);
    expect(JSON.stringify(consoleStub.error.mock.calls)).not.toContain(SERVICE_TEXT);
  });

  test("next to a code it is still not shown: the code's sentence only", () => {
    const tr = Object.assign({}, resxValues("tr"), { ErrorOccurred: GENERAL });
    const answer = Object.assign(proxied(["USER_NOT_FOUND"]), { errors: [SERVICE_TEXT] });

    expect(loadBridge(tr, quietConsole()).localizedErrors(answer)).toEqual([tr.ErrorUserNotFound]);
  });

  test("the proxy has no `errors` field to fill, and its own sentences are LocalizedString only", () => {
    const controller = read("Controllers", "UsersController.cs");
    expect(controller).not.toMatch(/\berrors = /);
    expect(controller).toMatch(/private JsonResult Refusal\(IReadOnlyList<GatewayErrorCode> codes, bool uncoded, int\? status, params LocalizedString\[\] ownMessages\)/);
  });
});

describe("a refusal without a code never shows — or logs — the service's sentence", () => {
  const labels = { ErrorOccurred: GENERAL };

  test("the proxy's codeless answer → the general error; the console names the HTTP status, nothing else", () => {
    const consoleStub = quietConsole();
    const errors = loadBridge(labels, consoleStub).localizedErrors(proxied([], { uncoded: true, status: 400 }));

    expect(errors).toEqual([GENERAL]);
    expect(consoleStub.warn).toHaveBeenCalledTimes(1);
    expect(consoleStub.warn.mock.calls[0][1]).toEqual({ codes: [], status: 400 });
  });

  test("a codeless envelope straight from the gateway (the delete) → the general error, its English text in neither place", () => {
    const consoleStub = quietConsole();
    const { localizedErrors } = loadBridge(labels, consoleStub);

    expect(localizedErrors({ isSuccessful: false, statusCode: 400, errors: [ENGLISH] })).toEqual([GENERAL]);
    expect(localizedErrors({})).toEqual([GENERAL]); // a 403 with no body
    expect(consoleStub.warn).toHaveBeenCalledTimes(2);
    expect(JSON.stringify(consoleStub.warn.mock.calls)).not.toContain(ENGLISH);
    expect(consoleStub.warn.mock.calls[0][1]).toEqual({ codes: [], status: 400 });
  });

  test("the proxy's OWN localized sentence (401 → Unauthorized) reaches the reader as it is, with no general sentence", () => {
    const consoleStub = quietConsole();
    // UsersController.GatewayFailureAsync on a 401.
    const answer = { success: false, ownMessages: ["Yetkisiz"], errorCode: null, errorParams: null, errorCodes: [], uncoded: false, status: 401 };

    expect(loadBridge(labels, consoleStub).localizedErrors(answer)).toEqual(["Yetkisiz"]);
    expect(consoleStub.warn).not.toHaveBeenCalled();
  });

  test("the proxy carries no relayed text: no rawText flag, no exception message, no service sentence", () => {
    const controller = read("Controllers", "UsersController.cs");
    expect(controller).not.toMatch(/rawText/);
    expect(controller).not.toMatch(/GetBaseException\(\)\.Message/);
    expect(controller).not.toMatch(/ExtractGatewayErrorsAsync/);
    expect(controller).not.toMatch(/Users dependency unavailable|message = "Unauthorized"/);
    expect(js()).not.toMatch(/rawText/);
  });
});

describe("every door of the screen goes through the bridge", () => {
  const labels = Object.assign({}, resxValues("tr"), { ErrorOccurred: GENERAL, AreYouSure: "?" });

  /** The production runAdminAction, run with a fake fetch; the confirm answers "yes" at once. */
  const runKebab = async (response) => {
    const toast = vi.fn();
    const list = { reload: vi.fn(), dt: { ajax: { reload: vi.fn() } } };
    const windowStub = { showConfirm: (_q, onYes) => { windowStub.done = onYes(); }, showToast: toast };
    const source = BRIDGE() + slice("const runAdminAction", "const adminAction =");
    // eslint-disable-next-line no-new-func
    const run = new Function("L", "console", "window", "fetch", "list", "postHeaders", "showInviteLink", source + "; return runAdminAction;")(
      () => labels, quietConsole(), windowStub, async () => response, list, () => ({}), vi.fn());
    run({ url: () => "/Users/disable/1", toast: "UserDisabled", type: "warning", icon: "bx-minus-circle", text: "Disable" })({ id: "1", row: { email: "a@b.test" } });
    await windowStub.done;
    return { toast, list };
  };

  test("200 { success: true } is the only success", async () => {
    const { toast, list } = await runKebab({ ok: true, json: async () => ({ success: true, setupUrl: null }) });
    expect(list.reload).toHaveBeenCalledWith("UserDisabled");
    expect(toast).not.toHaveBeenCalled();
  });

  test("200 { success: false } with a code is the code's sentence, not a success toast", async () => {
    const { toast, list } = await runKebab({ ok: true, json: async () => proxied(["USER_DEACTIVATE_SELF"]) });
    expect(list.reload).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(labels.ErrorUserDeactivateSelf, "error");
  });

  test("200 that is not the proxy's JSON (a sign-in page after the session ended) is a failure — the general sentence", async () => {
    const { toast, list } = await runKebab({ ok: true, json: async () => { throw new SyntaxError("Unexpected token <"); } });
    expect(list.reload).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(GENERAL, "error");
  });

  test("200 with JSON that does not say success (an empty object) is a failure too", async () => {
    const { toast, list } = await runKebab({ ok: true, json: async () => ({}) });
    expect(list.reload).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(GENERAL, "error");
  });

  /** SharedResource's own general sentence in a language (the screen's L().ErrorOccurred). */
  const sharedErrorOccurred = (lang) => {
    const xml = read("Resources", `SharedResource.${lang}.resx`);
    const match = xml.match(/<data name="ErrorOccurred"[^>]*>\s*<value>([\s\S]*?)<\/value>/);
    return match ? match[1] : null;
  };
  const BROWSER_SENTENCES = ["Failed to fetch", "Load failed", "NetworkError when attempting to fetch resource."];

  // Item 1 — a rejected fetch speaks the BROWSER's English; the reader gets the screen's general sentence instead.
  test.each(LANGS)("[%s] a network failure on a kebab action is the general sentence, never the browser's own words", async (lang) => {
    const general = sharedErrorOccurred(lang);
    expect(general, `SharedResource.${lang}.resx ErrorOccurred`).toBeTruthy();
    for (const browserText of BROWSER_SENTENCES) {
      const toast = vi.fn();
      const list = { reload: vi.fn(), dt: { ajax: { reload: vi.fn() } } };
      const windowStub = { showConfirm: (_q, onYes) => { windowStub.done = onYes(); }, showToast: toast };
      const source = BRIDGE() + slice("const runAdminAction", "const adminAction =");
      // eslint-disable-next-line no-new-func
      const run = new Function("L", "console", "window", "fetch", "list", "postHeaders", "showInviteLink", source + "; return runAdminAction;")(
        () => Object.assign({}, resxValues(lang), { ErrorOccurred: general }), quietConsole(), windowStub,
        async () => { throw new TypeError(browserText); }, list, () => ({}), vi.fn());
      run({ url: () => "/Users/disable/1", toast: "UserDisabled", type: "warning", icon: "x", text: "Disable" })({ id: "1", row: { email: "a@b.test" } });
      await windowStub.done;

      expect(toast).toHaveBeenCalledTimes(1);
      expect(toast).toHaveBeenCalledWith(general, "error");
      expect(list.reload).not.toHaveBeenCalled();
    }
  });

  test.each(LANGS)("[%s] a network failure on the delete is the general sentence too", async (lang) => {
    const general = sharedErrorOccurred(lang);
    const toast = vi.fn();
    const list = { reload: vi.fn() };
    const windowStub = { showConfirm: (_q, onYes) => { windowStub.done = onYes(); }, showToast: toast };
    const source = BRIDGE() + slice("const deleteRow", "const renderRowActions");
    // eslint-disable-next-line no-new-func
    const del = new Function("L", "console", "window", "fetch", "list", "apiUrl", "getAuthHeaders", source + "; return deleteRow;")(
      () => Object.assign({}, resxValues(lang), { ErrorOccurred: general }), quietConsole(), windowStub,
      async () => { throw new TypeError("Failed to fetch"); }, list, "http://gw", () => ({}));

    del({ row: { id: "1", email: "a@b.test" } });
    await windowStub.done;

    expect(toast).toHaveBeenCalledWith(general, "error");
    expect(list.reload).not.toHaveBeenCalled();
  });

  test("a network failure on the form is handed to the list component, which says the general sentence", async () => {
    // eslint-disable-next-line no-new-func
    const submit = new Function("L", "console", "window", "fetch", "showInviteLink", BRIDGE() + slice("const submitForm", "/*\n     * Dev-only") + "; return submitForm;")(
      () => labels, quietConsole(), {}, async () => { throw new TypeError("Failed to fetch"); }, vi.fn());

    // The page neither swallows the failure nor turns its message into `errors` …
    await expect(submit(new FormData(), false, { editingId: null, headers: {} })).rejects.toThrow("Failed to fetch");
    // … and the factory's catch shows its own general sentence, not the error's message.
    const factory = read("wwwroot", "assets", "js", "diten-datatable.js");
    const catchBlock = factory.slice(factory.indexOf("Form submit failed."), factory.indexOf("Form submit failed.") + 200);
    expect(catchBlock).toMatch(/showFormErrors\(\[L\(\)\.ErrorOccurred\]\)/);
    expect(catchBlock).not.toMatch(/error\.message/);
  });

  test("the delete says the refusal's sentence, read from AuthService's own envelope", async () => {
    const toast = vi.fn();
    const list = { reload: vi.fn() };
    const windowStub = { showConfirm: (_q, onYes) => { windowStub.done = onYes(); }, showToast: toast };
    const envelope = { isSuccessful: false, statusCode: 409, errors: ["This is the last account that can create users; …"], errorCodes: [{ code: "USER_DELETE_LAST_STEWARD" }] };
    const source = BRIDGE() + slice("const deleteRow", "const renderRowActions");
    // eslint-disable-next-line no-new-func
    const del = new Function("L", "console", "window", "fetch", "list", "apiUrl", "getAuthHeaders", source + "; return deleteRow;")(
      () => labels, quietConsole(), windowStub, async () => ({ ok: false, json: async () => envelope }), list, "http://gw", () => ({}));

    del({ row: { id: "1", email: "a@b.test" } });
    await windowStub.done;

    expect(list.reload).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith(labels.ErrorUserDeleteLastSteward, "error");
  });

  test("the form shows the bridge's sentences", () => {
    expect(slice("const submitForm", "const showInviteLink"))
      .toMatch(/if \(!json\.success\) return Object\.assign\(\{\}, json, \{ errors: localizedErrors\(json\) \}\);/);
  });
});

describe("two sentences that must say exactly what they mean", () => {
  test("PERM_DENIED is bound to the account-kind refusal only, and the code says so where it is bound", () => {
    expect(BRIDGE()).toMatch(/PERM_DENIED: 'ErrorUserAccountKindPermissionDenied'/);
    expect(slice("// WP-USERS-ERROR-CODES-01 (BL-450) — a refusal's stable code", "const ERROR_CODE_KEYS"))
      .toMatch(/PERM_DENIED is a GENERAL name bound here to ONE refusal/);
  });

  test("USER_PASSWORD_SETUP_PENDING does not claim the user never set a password (two of its three situations had one)", () => {
    expect(resxValues("en").ErrorUserPasswordSetupPending).not.toMatch(/has not set|not set their password|yet/i);
    expect(resxValues("tr").ErrorUserPasswordSetupPending).not.toMatch(/henüz|belirlemedi/i);
    const sentences = LANGS.map((lang) => resxValues(lang).ErrorUserPasswordSetupPending);
    expect(new Set(sentences).size).toBe(LANGS.length);
  });
});

describe("BL-529 — the reset confirm says what the reset does", () => {
  /** The production adminActions + runAdminAction; the confirm only records what it was asked to show. */
  const confirmOptionsFor = (key, labels) => {
    const seen = {};
    const windowStub = { showConfirm: (_q, _onYes, options) => { seen.options = options; }, showToast: vi.fn() };
    const source = slice("const adminActions = {", "const adminAction =");
    // eslint-disable-next-line no-new-func
    const { adminActions, runAdminAction } = new Function("L", "console", "window", "fetch", "list", "postHeaders", "showInviteLink",
      "sayFailure", "refusal", source + "; return { adminActions, runAdminAction };")(
      () => labels, quietConsole(), windowStub, vi.fn(), {}, () => ({}), vi.fn(), vi.fn(), vi.fn());
    runAdminAction(adminActions[key])({ id: "1", row: { email: "a@b.test" } });
    return seen.options;
  };

  test.each(LANGS)("[%s] the reset confirm carries the sentence that the old password and the sessions end", (lang) => {
    const labels = resxValues(lang);
    expect(labels.ResetPasswordConfirmText).toBeTruthy();
    expect(confirmOptionsFor("reset", labels).subtext).toBe(labels.ResetPasswordConfirmText);
  });

  test("the sentence names both effects, and every language has its own", () => {
    expect(resxValues("en").ResetPasswordConfirmText).toMatch(/current password stops working immediately/);
    expect(resxValues("en").ResetPasswordConfirmText).toMatch(/open sessions are signed out/);
    expect(resxValues("tr").ResetPasswordConfirmText).toMatch(/mevcut parolası hemen geçersiz olur/);
    expect(resxValues("tr").ResetPasswordConfirmText).toMatch(/açık oturumlarının tümü kapanır/);
    const sentences = LANGS.map((lang) => resxValues(lang).ResetPasswordConfirmText);
    expect(new Set(sentences).size).toBe(LANGS.length);
    expect(read("Views", "Governance", "Users", "_IndexL10n.cshtml")).toMatch(/ResetPasswordConfirmText = Localizer\["ResetPasswordConfirmText"\]\.Value/);
  });

  test("the other admin actions keep their confirm as it was (no sentence)", () => {
    const labels = resxValues("en");
    for (const key of ["disable", "enable", "resend"]) {
      expect(confirmOptionsFor(key, labels).subtext).toBeUndefined();
    }
  });
});
