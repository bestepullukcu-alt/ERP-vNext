const { loadScript } = require("./load-script");

/*
 * BL-521 FIX5 item 14 — THE E-MAIL FIELD IS READ-ONLY IN EDIT MODE: BEHAVIOUR, NOT A SOURCE GREP. FIX4 made an administrator's
 * e-mail immutable (Platform answers 409 PLATFORM_ADMINISTRATOR_EMAIL_IMMUTABLE) and pinned "the field is read-only while
 * editing" with a test that searched the script's TEXT for `emailInput.readOnly = true` — a test that stays green when the
 * line is moved into dead code, set on the wrong element or never reached. This one runs the page's REAL script in jsdom over
 * the form's real field ids and drives the two doors that matter: "add new" (the address is typed) and the edit offcanvas
 * (the address is shown, locked, with its reason under it, and still sent with the form because a read-only field is
 * submitted while a disabled one is not).
 */

const ADMIN = {
  id: "4b1d3b0e-8f0a-4c52-9b41-0f3a8f8f0a11",
  email: "existing.admin@bl521.test",
  userName: "existing.admin",
  displayName: "Existing Admin",
  actorType: "PlatformAdmin",
  status: "Active",
  invitationStatus: "Accepted",
  version: 7,
  roles: ["SupportAdmin"],
  allowedTenantIds: []
};

const FORM = `
  <span id="kpi-total"></span><span id="kpi-active"></span><span id="kpi-suspended"></span><span id="kpi-pending"></span>
  <div id="offcanvasCreateEdit"></div>
  <h5 id="offcanvasCreateEditLabel"></h5>
  <span id="createRecommendedBadge" class="d-none"></span>
  <button type="button" class="add-new" id="btnAddAdministrator">Add</button>
  <button type="button" id="oc-btn-edit" data-edit-id="${ADMIN.id}">Edit</button>
  <form id="formAdministrators">
    <div id="formAdministratorsAlert" class="d-none"></div>
    <input type="hidden" id="administratorId" name="Id" />
    <input type="hidden" id="administratorVersion" name="Version" value="0" />
    <input type="hidden" id="administratorStatus" name="Status" value="Active" />
    <div id="administratorEditStatusSection" class="d-none">
      <span id="administratorEditStatusBadge"></span>
      <span id="administratorEditInvitationBadge"></span>
    </div>
    <input type="email" id="administratorEmail" name="Email" required />
    <div id="administratorEmailImmutableHelp" class="form-text d-none">The e-mail cannot be changed.</div>
    <input type="text" id="administratorUserName" name="UserName" />
    <input type="text" id="administratorDisplayName" name="DisplayName" />
    <select id="administratorActorType" name="ActorType">
      <option value="PlatformAdmin">Platform</option>
      <option value="PartnerAdmin">Partner</option>
    </select>
    <input type="text" id="administratorPartnerId" name="PartnerId" />
    <textarea id="administratorAllowedTenantIds" name="AllowedTenantIds"></textarea>
    <div id="administratorRolesGroup">
      <input type="checkbox" id="roleReadOnly" name="Roles" value="ReadOnly" />
      <input type="checkbox" id="roleSupportAdmin" name="Roles" value="SupportAdmin" />
    </div>
    <div class="js-password-management d-none"><div id="wrapperResendInvite" class="d-none"></div></div>
    <button type="button" id="btnSaveAdministrator">Save</button>
  </form>`;

const flush = async () => {
  for (let i = 0; i < 10; i += 1) {
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

describe("Platform administrators form — the e-mail address", () => {
  // The page script declares a top-level const (the Golden Reference shape), so it is loaded ONCE for the file.
  beforeAll(async () => {
    document.body.innerHTML = FORM;
    window.L10n = { FormTitleCreate: "New administrator", FormTitleEdit: "Edit administrator", Save: "Save", Update: "Update", ErrorOccurred: "Error" };
    window.bootstrap = { Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } };
    globalThis.bootstrap = window.bootstrap;
    globalThis.fetch = vi.fn(async (url) => {
      if (String(url).includes("/get/")) {
        return { ok: true, json: async () => ({ success: true, data: ADMIN }) };
      }
      return { ok: false, json: async () => ({}) }; // the KPI call: nothing to show
    });
    loadScript("wwwroot/assets/js/Platform/Administrators/index.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();
  });

  const email = () => document.getElementById("administratorEmail");
  const help = () => document.getElementById("administratorEmailImmutableHelp");

  it("is typed for a NEW administrator: not read-only, no explanation shown", async () => {
    document.querySelector(".add-new").click();
    await flush();

    expect(email().readOnly).toBe(false);
    expect(email().value).toBe("");
    expect(help().classList.contains("d-none")).toBe(true);
  });

  it("is shown LOCKED when an existing administrator is edited, with the reason under it, and is still part of the form", async () => {
    document.getElementById("oc-btn-edit").click();
    await flush();

    expect(email().value).toBe(ADMIN.email);
    expect(email().readOnly).toBe(true);
    // read-only, never disabled: a disabled field is not submitted and Platform compares the submitted address
    expect(email().disabled).toBe(false);
    expect(new FormData(document.getElementById("formAdministrators")).get("Email")).toBe(ADMIN.email);
    expect(help().classList.contains("d-none")).toBe(false);
  });

  it("is typed again when the next thing opened is a NEW administrator (the lock does not outlive the edit)", async () => {
    document.getElementById("oc-btn-edit").click();
    await flush();
    expect(email().readOnly).toBe(true);

    document.querySelector(".add-new").click();
    await flush();

    expect(email().readOnly).toBe(false);
    expect(email().value).toBe("");
    expect(help().classList.contains("d-none")).toBe(true);
  });
});
