const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

// MOD-0018 Users invitation (FE): the shared ResetPassword JS must POST to the configurable
// endpoint so /account/set-password (tenant) and /platform/reset-password (platform) share one file,
// and the Add-User form must no longer collect a password.

function renderResetForm() {
    document.body.innerHTML = `
        <form id="resetPasswordForm">
            <input type="hidden" id="email" value="invitee@acme.test" />
            <input type="hidden" id="token" value="tok-123" />
            <input type="password" id="newPassword" />
            <input type="password" id="confirmPassword" />
            <button type="submit">Set</button>
        </form>`;
    document.getElementById("newPassword").value = "BrandN3w!";
    document.getElementById("confirmPassword").value = "BrandN3w!";
}

function bootResetPasswordPage() {
    // Script is loaded once (const-scoped); re-dispatch DOMContentLoaded so init() re-binds
    // the submit handler to the freshly-rendered form for this test.
    document.dispatchEvent(new Event("DOMContentLoaded"));
}

async function submitAndFlush() {
    document.getElementById("resetPasswordForm").dispatchEvent(new Event("submit", { cancelable: true }));
    // let the async submit handler run (fetch + awaited Swal)
    await new Promise((r) => setTimeout(r, 0));
    await new Promise((r) => setTimeout(r, 0));
}

describe("set-password screen — configurable POST target", () => {
    let fetchMock;

    beforeAll(() => {
        loadScript("wwwroot/assets/js/Account/reset-password.js");
    });

    beforeEach(() => {
        renderResetForm();
        window.L10n = {};
        window.Swal = { fire: () => Promise.resolve({ isConfirmed: true }) };
        fetchMock = vi.fn(() =>
            Promise.resolve({ ok: true, json: () => Promise.resolve({ success: true, redirectUrl: "/account/login" }) })
        );
        global.fetch = fetchMock;
        window.fetch = fetchMock;
        // Keep jsdom from complaining about navigation after success.
        delete window.location;
        window.location = { href: "" };
    });

    it("tenant config posts to /account/set-password with email+token+newPassword", async () => {
        window.ResetPasswordConfig = {
            isForcedChange: false,
            email: "invitee@acme.test",
            token: "tok-123",
            setPasswordUrl: "/account/set-password",
            backToLoginUrl: "/account/login",
        };
        bootResetPasswordPage();
        await submitAndFlush();

        expect(fetchMock).toHaveBeenCalledTimes(1);
        const [url, opts] = fetchMock.mock.calls[0];
        expect(url).toBe("/account/set-password");
        expect(opts.method).toBe("POST");
        const body = JSON.parse(opts.body);
        expect(body).toMatchObject({ email: "invitee@acme.test", token: "tok-123", newPassword: "BrandN3w!" });
    });

    it("platform reset still works (falls back when no setPasswordUrl)", async () => {
        window.ResetPasswordConfig = { isForcedChange: false, email: "p@x.test", token: "t" };
        bootResetPasswordPage();
        await submitAndFlush();

        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(fetchMock.mock.calls[0][0]).toBe("/platform/reset-password");
    });
});

describe("Add User form — no password field (invitation flow)", () => {
    it("_CreateEditOffcanvas.cshtml has no password input", () => {
        const cshtml = fs.readFileSync(
            path.resolve(__dirname, "..", "Views/Governance/Users/_CreateEditOffcanvas.cshtml"),
            "utf8"
        );
        expect(cshtml).not.toMatch(/id="userPassword"/);
        expect(cshtml).not.toMatch(/name="Password"/);
        expect(cshtml).toMatch(/id="userInviteHint"/); // invitation hint present instead
    });
});
