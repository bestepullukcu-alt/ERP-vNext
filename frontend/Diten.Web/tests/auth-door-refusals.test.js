const { loadScript } = require("./load-script");

/*
 * WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — the sign-in page and the password page say a refusal through the shared
 * bridge (shared/diten-refusal.js). The Web door already answers in the reader's language ({ errors:[sentence], local:true });
 * anything else — an HTML error page, an empty body, a bare English `detail` — is the general sentence, never the
 * service's English and never a hard-coded fallback. The production files are loaded as the browser loads them.
 */
const L = {
  LoginErrorTitle: "Hata",
  LoginErrorRequired: "E-posta ve şifre zorunludur.",
  LoginLoading: "Giriş yapılıyor...",
  ErrorOccurred: "Bir hata oluştu.",
  ConfirmButton: "Tamam",
  Required: "Tüm alanlar zorunludur.",
  Mismatch: "Şifreler eşleşmiyor.",
  Loading: "Lütfen bekleyin...",
  ErrorTitle: "Hata"
};

function answer(status, body, contentType = "application/json") {
  return Promise.resolve(new Response(typeof body === "string" ? body : JSON.stringify(body), {
    status,
    headers: { "Content-Type": contentType }
  }));
}

async function shownSentence() {
  await vi.waitFor(() => expect(window.Swal.fire).toHaveBeenCalled());
  return window.Swal.fire.mock.calls.at(-1)[0];
}

describe("the sign-in page says every refusal through the shared bridge", () => {
  beforeAll(() => {
    document.body.innerHTML = `
      <form id="loginForm"><input id="email" value="a@b.test"><input id="password" value="x">
        <input type="checkbox" id="remember-me"><button type="submit">Giriş</button></form>`;
    window.L10n = L;
    window.AuthMode = "tenant";
    window.DevDefaultTenantId = "00000000-0000-0000-0000-000000000001";
    loadScript("wwwroot/assets/js/shared/diten-refusal.js");
    loadScript("wwwroot/assets/js/Account/login.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
  });

  beforeEach(() => {
    window.L10n = L;
    window.Swal = { fire: vi.fn(() => Promise.resolve({})) };
    vi.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => vi.restoreAllMocks());

  const submit = () => document.getElementById("loginForm").dispatchEvent(new Event("submit", { cancelable: true }));

  test("the door's own sentence (already the reader's language) is the one shown", async () => {
    const sentence = "E-posta veya şifre hatalı.";
    global.fetch = vi.fn(() => answer(401, { detail: sentence, errors: [sentence], local: true }));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toContain(sentence);
    expect(dialog.confirmButtonText).toBe("Tamam");
  });

  test("a bare English detail without the local mark is never shown — the general sentence is", async () => {
    global.fetch = vi.fn(() => answer(401, { detail: "Invalid email or password." }));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toContain(L.ErrorOccurred);
    expect(dialog.text).not.toContain("Invalid email or password.");
  });

  test("FIX2 — a failure that is not a refusal (the network) is the general sentence, never its English message", async () => {
    global.fetch = vi.fn(() => Promise.reject(new TypeError("Failed to fetch")));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toBe(L.ErrorOccurred);
  });

  test("FIX2 — a sentence is shown as TEXT, never as HTML", async () => {
    const sentence = '<img src=x onerror="window.__pwned=1">';
    global.fetch = vi.fn(() => answer(401, { detail: sentence, errors: [sentence], local: true }));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toBe(sentence);
    expect(dialog.html).toBeUndefined();
  });

  test("an answer that is not JSON (a proxy's error page) is the general sentence, not a crash", async () => {
    global.fetch = vi.fn(() => answer(502, "<html>Bad gateway</html>", "text/html"));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toContain(L.ErrorOccurred);
    expect(dialog.text).not.toContain("Bad gateway");
  });
});

describe("the password page says every refusal through the shared bridge", () => {
  beforeAll(() => {
    document.body.innerHTML = `
      <form id="resetPasswordForm"><input id="newPassword" value="N3w!Password"><input id="confirmPassword" value="N3w!Password">
        <button type="submit">Kaydet</button></form>`;
    window.ResetPasswordConfig = { isForcedChange: false, email: "a@b.test", token: "t", setPasswordUrl: "/account/set-password" };
    loadScript("wwwroot/assets/js/Account/reset-password.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
  });

  beforeEach(() => {
    window.L10n = L;
    window.Swal = { fire: vi.fn(() => Promise.resolve({})) };
    vi.spyOn(console, "warn").mockImplementation(() => {});
  });
  afterEach(() => vi.restoreAllMocks());

  const submit = () => document.getElementById("resetPasswordForm").dispatchEvent(new Event("submit", { cancelable: true }));

  test("a deactivated account's refusal is the door's localized sentence", async () => {
    const sentence = "Hesabınız bir yönetici tarafından pasifleştirildi. Yöneticinize başvurun.";
    global.fetch = vi.fn(() => answer(409, { detail: sentence, errors: [sentence], local: true }));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toContain(sentence);
  });

  test("FIX2 — a network failure on the password page is the general sentence, shown as text", async () => {
    global.fetch = vi.fn(() => Promise.reject(new TypeError("Failed to fetch")));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toBe(L.ErrorOccurred);
    expect(dialog.html).toBeUndefined();
  });

  test("an empty answer is the general sentence — never the old English 'Password could not be changed.'", async () => {
    global.fetch = vi.fn(() => answer(500, ""));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toContain(L.ErrorOccurred);
    expect(dialog.text).not.toContain("Password could not be changed");
  });
});

describe("FIX3 — the forgot-password page says every refusal as text, never HTML", () => {
  beforeAll(() => {
    document.body.innerHTML = `
      <form id="forgotPasswordForm"><input id="email" value="a@b.test"><button type="submit">Gönder</button></form>`;
    loadScript("wwwroot/assets/js/Account/forgot-password.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
  });

  beforeEach(() => {
    window.L10n = { ...L, ErrorGeneric: L.ErrorOccurred };
    window.Swal = { fire: vi.fn(() => Promise.resolve({})) };
  });
  afterEach(() => vi.restoreAllMocks());

  const submit = () => document.getElementById("forgotPasswordForm").dispatchEvent(new Event("submit", { cancelable: true }));

  test("the door's own sentence is shown as TEXT (markup stays text)", async () => {
    const sentence = '<img src=x onerror="window.__pwned=1"> Çok fazla istek.';
    global.fetch = vi.fn(() => answer(429, { detail: sentence, errors: [sentence], local: true }));

    submit();
    const dialog = await shownSentence();

    expect(dialog.text).toBe(sentence);
    expect(dialog.html).toBeUndefined();
  });

  test("a raw detail (not the door's own) and a network failure are the general sentence", async () => {
    global.fetch = vi.fn(() => answer(503, { detail: "Upstream exploded at 10.0.0.7" }));
    submit();
    expect((await shownSentence()).text).toBe(L.ErrorOccurred);

    window.Swal.fire.mockClear();
    global.fetch = vi.fn(() => Promise.reject(new TypeError("Failed to fetch")));
    submit();
    expect((await shownSentence()).text).toBe(L.ErrorOccurred);
  });
});
