const fs = require("fs");
const path = require("path");

/*
 * WP-USERS-ERROR-CODES-01 — CONTROL TOWER acceptance of fix round 2. Three rules of the Users screen the delivered
 * tests did not hold (each acceptance sabotage stayed green):
 *
 *   1. WHICH sentence a code gets. The delivered tests take the key from the production map itself, so swapping two
 *      entries ("invalid e-mail" ⇄ "e-mail too long") changes nothing they can see. The table below is written out
 *      here, by hand, once — a code that moves to another sentence is red.
 *   2. A code the map knows but whose sentence was not published to the page reads as the general sentence, never as
 *      the text "undefined".
 *   3. The list component SHOWS a refused form submit. The page hands the factory `errors` (its localized sentences);
 *      the factory's submitForm is what puts them on screen — run here, sliced out of diten-datatable.js.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const js = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
const factory = () => read("wwwroot", "assets", "js", "diten-datatable.js");

const sliceOf = (source, from, to, name) => {
  const start = source.indexOf(from);
  const end = source.indexOf(to, start + from.length);
  expect(start, `${name} lacks "${from}"`).toBeGreaterThan(-1);
  expect(end, `${name} lacks "${to}" after "${from}"`).toBeGreaterThan(start);
  return source.slice(start, end);
};
const BRIDGE = () => sliceOf(js(), "const ERROR_CODE_KEYS", "const submitForm", "index.js");
const loadBridge = (labels, consoleStub) =>
  // eslint-disable-next-line no-new-func
  new Function("L", "console", BRIDGE() + "; return { ERROR_CODE_KEYS, localizedErrors };")(() => labels, consoleStub);
const quietConsole = () => ({ warn: vi.fn(), error: vi.fn(), log: vi.fn() });
const proxied = (codes, extra) => Object.assign({
  success: false, ownMessages: [], errorCode: codes.length ? codes[0] : null, errorParams: null,
  errorCodes: codes.map((code) => ({ code, params: null })), uncoded: false, status: 409
}, extra || {});

// The code → sentence table, written by hand. PERM_DENIED is deliberately the account-kind sentence (index.js says why).
const SENTENCE_OF = {
  USER_EMAIL_TAKEN: "ErrorUserEmailTaken",
  USER_INVITATION_PENDING: "ErrorUserInvitationPending",
  USER_QUOTA_EXCEEDED: "ErrorUserQuotaExceeded",
  USER_DELETE_SELF: "ErrorUserDeleteSelf",
  USER_DELETE_LAST_STEWARD: "ErrorUserDeleteLastSteward",
  USER_DEACTIVATE_SELF: "ErrorUserDeactivateSelf",
  USER_NOT_FOUND: "ErrorUserNotFound",
  USER_ACCOUNT_KIND_INVALID: "ErrorUserAccountKindInvalid",
  USER_SETUP_ALREADY_COMPLETED: "ErrorUserSetupAlreadyCompleted",
  USER_PASSWORD_SETUP_PENDING: "ErrorUserPasswordSetupPending",
  PERM_DENIED: "ErrorUserAccountKindPermissionDenied",
  USER_EMAIL_REQUIRED: "ErrorUserEmailRequired",
  USER_EMAIL_INVALID: "ErrorUserEmailInvalid",
  USER_EMAIL_TOO_LONG: "ErrorUserEmailTooLong",
  USER_FIRST_NAME_REQUIRED: "ErrorUserFirstNameRequired",
  USER_FIRST_NAME_TOO_LONG: "ErrorUserFirstNameTooLong",
  USER_LAST_NAME_REQUIRED: "ErrorUserLastNameRequired",
  USER_LAST_NAME_TOO_LONG: "ErrorUserLastNameTooLong"
};

describe("each code has ITS sentence", () => {
  test("the screen's map is exactly the table: no code moved to another sentence, none added without one here", () => {
    expect(loadBridge({}, quietConsole()).ERROR_CODE_KEYS).toEqual(SENTENCE_OF);
  });

  test.each(Object.entries(SENTENCE_OF))("%s is said with %s", (code, key) => {
    // Every label is its own key, so the sentence shown names the key it came from.
    const labels = Object.fromEntries(Object.values(SENTENCE_OF).map((k) => [k, `«${k}»`]));
    labels.ErrorOccurred = "«general»";

    expect(loadBridge(labels, quietConsole()).localizedErrors(proxied([code]))).toEqual([`«${key}»`]);
  });
});

describe("a known code whose sentence is not on the page", () => {
  test("reads as the general sentence and is reported to the console — never 'undefined'", () => {
    const consoleStub = quietConsole();
    // The page published only the general sentence (a resx key missing from _IndexL10n.cshtml would look like this).
    const errors = loadBridge({ ErrorOccurred: "«general»" }, consoleStub).localizedErrors(proxied(["USER_NOT_FOUND"]));

    expect(errors).toEqual(["«general»"]);
    expect(errors.join(" ")).not.toMatch(/undefined/);
    expect(consoleStub.warn).toHaveBeenCalledTimes(1);
  });
});

describe("the list component shows a refused form submit", () => {
  /** diten-datatable.js's own submitForm, run with the collaborators it closes over. */
  const loadSubmit = ({ submit, valid = true }) => {
    const body = sliceOf(factory(), "submitForm: async function () {", "\n        });\n\n        return handle;", "diten-datatable.js");
    const saveBtn = { disabled: false };
    const formEl = {
      classList: { add: vi.fn() },
      checkValidity: () => valid,
      querySelector: () => ({ value: "token" })
    };
    const stubs = {
      showFormErrors: vi.fn(),
      handle: { reload: vi.fn() },
      offcanvas: { hide: vi.fn() },
      responsive: { suppress: vi.fn() },
      console: quietConsole(),
      saveBtn
    };
    const documentStub = { getElementById: (id) => (id === "formUser" ? formEl : id === "btnSaveUser" ? saveBtn : null) };
    // eslint-disable-next-line no-new-func
    const api = new Function(
      "document", "form", "editingId", "getAuthHeaders", "responsive", "offcanvasOf", "handle", "showFormErrors", "L",
      "savedViewSpec", "tableEl", "console", "FormData",
      "return ({ " + body + " });"
    )(
      documentStub,
      { formId: "formUser", offcanvasId: "offcanvasCreateEdit", saveBtnId: "btnSaveUser", submit },
      null, () => ({}), stubs.responsive, () => stubs.offcanvas, stubs.handle, stubs.showFormErrors,
      () => ({ ErrorOccurred: "«general»", FormValidationError: "«check the form»" }),
      { pageKey: "users" }, { id: "usersTable" }, stubs.console, function FakeFormData() {}
    );
    return { submitForm: api.submitForm, stubs };
  };

  test("a refusal's sentences reach the form's alert, the list is not reloaded, the button comes back", async () => {
    const { submitForm, stubs } = loadSubmit({ submit: async () => ({ success: false, errors: ["«first»", "«second»"] }) });

    await submitForm();

    expect(stubs.showFormErrors).toHaveBeenCalledTimes(1);
    expect(stubs.showFormErrors).toHaveBeenCalledWith(["«first»", "«second»"]);
    expect(stubs.handle.reload).not.toHaveBeenCalled();
    expect(stubs.offcanvas.hide).not.toHaveBeenCalled();
    expect(stubs.saveBtn.disabled).toBe(false);
  });

  test("a success closes the form and reloads the list — and shows no error", async () => {
    // The control: the alert above is the refusal, not something submitForm always does.
    const { submitForm, stubs } = loadSubmit({ submit: async () => ({ success: true }) });

    await submitForm();

    expect(stubs.showFormErrors).not.toHaveBeenCalled();
    expect(stubs.handle.reload).toHaveBeenCalledWith("RecordCreated");
    expect(stubs.offcanvas.hide).toHaveBeenCalledTimes(1);
  });

  test("a submit that throws shows the general sentence, not the error's message", async () => {
    const { submitForm, stubs } = loadSubmit({ submit: async () => { throw new TypeError("Failed to fetch"); } });

    await submitForm();

    expect(stubs.showFormErrors).toHaveBeenCalledWith(["«general»"]);
    expect(stubs.saveBtn.disabled).toBe(false);
  });
});
