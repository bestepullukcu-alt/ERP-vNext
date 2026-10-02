const { loadScript } = require("./load-script");

/*
 * WP-ROLES-CLOSE-01 — shared/diten-refusal.js: one way to say a refusal in the reader's language. A coded refusal
 * becomes the screen's own sentence; a refusal without a known code never shows the server's sentence — the general
 * one is shown and the console is told. The production file is loaded as the browser loads it (a classic script).
 */
describe("DitenRefusal.message", () => {
  const KEYS = { ROLE_NAME_TAKEN: "ErrorRoleNameTaken" };
  const L = { ErrorOccurred: "Bir hata oluştu.", ErrorRoleNameTaken: "Bu adda bir rol zaten var. Farklı bir ad seçin." };
  let warn;

  beforeEach(() => {
    delete window.DitenRefusal;
    loadScript("wwwroot/assets/js/shared/diten-refusal.js");
    warn = vi.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => warn.mockRestore());

  test("a proxy answer with errorCode is said in the reader's language, not the server's", () => {
    const text = window.DitenRefusal.message({ success: false, errors: ["Role name is already in use."], errorCode: "ROLE_NAME_TAKEN" }, KEYS, L, "Roles");

    expect(text).toBe(L.ErrorRoleNameTaken);
    expect(warn).not.toHaveBeenCalled();
  });

  test("the Response envelope of a direct call (errorCodes[0].code) is read the same way", () => {
    const text = window.DitenRefusal.message({ isSuccessful: false, errors: ["Role name is already in use."], errorCodes: [{ code: "ROLE_NAME_TAKEN" }] }, KEYS, L, "Roles");

    expect(text).toBe(L.ErrorRoleNameTaken);
  });

  test("a refusal WITHOUT a code shows the general sentence, never the raw one, and warns", () => {
    const text = window.DitenRefusal.message({ success: false, errors: ["Something else."] }, KEYS, L, "Roles");

    expect(text).toBe(L.ErrorOccurred);
    expect(text).not.toContain("Something else.");
    expect(warn).toHaveBeenCalledTimes(1);
  });

  test("a code the screen does not map is treated as no code", () => {
    const text = window.DitenRefusal.message({ success: false, errors: ["x"], errorCode: "ROLE_SOMETHING_NEW" }, KEYS, L, "Roles");

    expect(text).toBe(L.ErrorOccurred);
    expect(warn).toHaveBeenCalledTimes(1);
  });

  test("a mapped code whose label is missing falls back to the general sentence", () => {
    expect(window.DitenRefusal.message({ errorCode: "ROLE_NAME_TAKEN", errors: ["x"] }, KEYS, { ErrorOccurred: "Genel" }, "Roles")).toBe("Genel");
  });

  test("`local: true` — this application's own, already localized sentence — is shown as it is", () => {
    const text = window.DitenRefusal.message({ success: false, errors: ["Oturumunuz sona erdi."], errorCode: null, local: true }, KEYS, L, "Roles");

    expect(text).toBe("Oturumunuz sona erdi.");
  });

  test("nothing at all (a network body that was not JSON) is the general sentence", () => {
    expect(window.DitenRefusal.message({}, KEYS, L, "Roles")).toBe(L.ErrorOccurred);
    expect(window.DitenRefusal.message(null, KEYS, L, "Roles")).toBe(L.ErrorOccurred);
  });
});

describe("DitenRefusal.messages — a refusal with several codes says every one of them", () => {
  const KEYS = { ROLE_NAME_REQUIRED: "ErrorRoleNameRequired", ROLE_DISPLAY_NAME_TOO_LONG: "ErrorRoleDisplayNameTooLong" };
  const L = { ErrorOccurred: "Bir hata oluştu.", ErrorRoleNameRequired: "Rol adını girin.", ErrorRoleDisplayNameTooLong: "Görünen ad en fazla 100 karakter olabilir." };
  let warn;

  beforeEach(() => {
    delete window.DitenRefusal;
    loadScript("wwwroot/assets/js/shared/diten-refusal.js");
    warn = vi.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => warn.mockRestore());

  test("the proxy's list of codes becomes one sentence per code, in order", () => {
    const texts = window.DitenRefusal.messages(
      { success: false, errors: ["Doğrulama başarısız."], errorCode: "ROLE_NAME_REQUIRED", errorCodes: ["ROLE_NAME_REQUIRED", "ROLE_DISPLAY_NAME_TOO_LONG"], local: true }, KEYS, L, "Roles");

    expect(texts).toEqual([L.ErrorRoleNameRequired, L.ErrorRoleDisplayNameTooLong]);
  });

  test("the envelope's list ([{ code }]) is read the same way, and a repeated code is said once", () => {
    const texts = window.DitenRefusal.messages(
      { isSuccessful: false, errors: ["x"], errorCodes: [{ code: "ROLE_NAME_REQUIRED" }, { code: "ROLE_NAME_REQUIRED" }, { code: "ROLE_DISPLAY_NAME_TOO_LONG" }] }, KEYS, L, "Roles");

    expect(texts).toEqual([L.ErrorRoleNameRequired, L.ErrorRoleDisplayNameTooLong]);
  });

  test("with no code the screen knows, it is the single sentence message() gives", () => {
    expect(window.DitenRefusal.messages({ success: false, errors: ["Oturumunuz sona erdi."], errorCodes: [], local: true }, KEYS, L, "Roles")).toEqual(["Oturumunuz sona erdi."]);
    expect(window.DitenRefusal.messages({ success: false, errors: ["raw"], errorCodes: ["ROLE_UNKNOWN"] }, KEYS, L, "Roles")).toEqual([L.ErrorOccurred]);
  });
});
