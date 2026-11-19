const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0040-FU02 DL-2 AC-60 (Q-3 A) — the Organization Unit and Position forms after the code became system-assigned.
 * The REAL form scripts are booted in jsdom (only the network is stubbed and every write is recorded); the views,
 * the l10n bridges and the 7-language resource files are read as shipped.
 *
 *   create: the code box is read-only and empty, it is not a required field, and the POST carries no `code`
 *   edit:   the code box shows the stored value and the PUT echoes it unchanged
 *   errors: a record-code reason_code is shown in the user's language — never the service's English errors[]
 *   texts:  every key in both resource files, in all 7 languages, none left equal to English (localization-standard §14)
 */

const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");

// reason_code -> resource key (pack §11). The note shown under the create form has no reason code.
const REASONS = {
  RECORD_CODE_IMMUTABLE: "RecordCodeImmutable",
  RECORD_CODE_SYSTEM_ASSIGNED: "RecordCodeSystemAssigned",
  RECORD_CODE_COLLISION: "RecordCodeCollision",
  LEGAL_ENTITY_SHORT_CODE_CHANGED: "RecordCodeShortCodeChanged",
  LEGAL_ENTITY_SHORT_CODE_MISSING: "RecordCodeShortCodeMissing",
  LEGAL_ENTITY_NOT_FOUND: "RecordCodeLegalEntityNotFound",
  LEGAL_ENTITY_NOT_ELIGIBLE: "RecordCodeLegalEntityNotEligible",
  LEGAL_ENTITY_READ_PERMISSION_MISSING: "RecordCodeLegalEntityReadPermissionMissing",
  RECORD_CODE_PERMISSION_MISSING: "RecordCodePermissionMissing",
  RECORD_CODE_TENANT_NOT_ACTIVE: "RecordCodeTenantNotActive",
  RECORD_CODE_UNAVAILABLE: "RecordCodeUnavailable",
  LEGAL_ENTITY_SOURCE_UNAVAILABLE: "RecordCodeLegalEntitySourceUnavailable",
  RECORD_CODE_FORBIDDEN: "RecordCodeForbidden"
};
const KEYS = ["RecordCodeAssignedOnSave", ...Object.values(REASONS)];
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const FORMS = [
  { name: "OrganizationUnits form", script: "wwwroot/assets/js/Organization/OrganizationUnits/form.js",
    view: ["Views", "Organization", "OrganizationUnits", "Form.cshtml"],
    l10nView: ["Views", "Organization", "OrganizationUnits", "_FormL10n.cshtml"],
    resources: ["Resources", "Views", "Organization", "OrganizationUnits", "OrganizationUnitsIndex"],
    page: (mode, id) => `<div id="ou-form-page" data-ou-id="${id}" data-ou-mode="${mode}"></div>`,
    endpoint: "/OrganizationUnits/api", l10nId: "org-unit-form-l10n", alert: "ou-form-alert", submit: "ou-submit",
    code: "ouCode", required: { ouName: "Unit", ouLegalEntityId: "le-1" },
    stored: { code: "MYG-ORG-000001", name: "Unit", legalEntityId: "le-1" } },
  { name: "Positions form", script: "wwwroot/assets/js/Organization/Positions/form.js",
    view: ["Views", "Organization", "Positions", "Form.cshtml"],
    l10nView: ["Views", "Organization", "Positions", "_FormL10n.cshtml"],
    resources: ["Resources", "Views", "Organization", "Positions", "PositionsIndex"],
    page: (mode, id) => `<div id="p-form-page" data-p-id="${id}" data-p-mode="${mode}"></div>`,
    endpoint: "/Positions/api", l10nId: "position-form-l10n", alert: "p-form-alert", submit: "p-submit",
    code: "pCode", required: { pName: "Position", pOrgUnitId: "ou-1" },
    stored: { code: "MYG-POS-000001", name: "Position", organizationUnitId: "ou-1" } }
];

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const writes = () => global.fetch.mock.calls.filter((call) => call[1] && call[1].method && call[1].method !== "GET");
const l10nJson = () => JSON.stringify(Object.fromEntries([
  ["DateFormat", "d.m.Y"], ["RequiredField", "REQUIRED"], ["ErrorOccurred", "ERROR"],
  ...Object.values(REASONS).map((key) => [key, `L10N:${key}`])
]));

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
  delete window.DitenDateField;
  vi.restoreAllMocks();
});

const boot = async (form, { mode = "create", answer } = {}) => {
  const id = mode === "edit" ? "rec-1" : "";
  global.fetch = vi.fn((url, init) => {
    if (init && init.method && init.method !== "GET" && answer) return Promise.resolve(answer);
    const body = mode === "edit" && String(url).endsWith(`${form.endpoint}/${id}`) ? { data: form.stored } : { data: [] };
    return Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(body), text: () => Promise.resolve("") });
  });
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
  const fields = Object.entries(form.required)
    .map(([field, value]) => `<select id="${field}"><option value="${value}" selected>${value}</option></select>`).join("");
  document.body.innerHTML = `${form.page(mode, id)}
    <script type="application/json" id="${form.l10nId}">${l10nJson()}</script>
    <div id="${form.alert}" class="d-none"></div>
    <input type="text" id="${form.code}" class="form-control" readonly>
    ${fields}
    <button type="button" class="btn btn-primary" id="${form.submit}">Save</button>`;
  vi.spyOn(console, "error").mockImplementation(() => {});
  loadScript(form.script);
  await tick();
  await tick();
};

const submit = async (form) => {
  document.getElementById(form.submit).click();
  await tick();
  await tick();
};

describe.each(FORMS)("$name", (form) => {
  test("the view: the code box is read-only, not required, unstarred, and the create note is there; the layout is unchanged", () => {
    const view = read(...form.view);
    const input = view.split("\n").find((line) => line.includes(`id="${form.code}"`) && line.includes("<input"));
    expect(input, "the code input was not found in the view").toBeTruthy();
    expect(input).toContain("readonly");
    expect(input).not.toMatch(/\brequired\b/);
    expect(input).not.toContain("data-required");
    const label = view.split("\n").find((line) => line.includes(`for="${form.code}"`));
    expect(label).not.toContain("text-danger");
    expect(view).toContain('@Localizer["RecordCodeAssignedOnSave"]');
    expect(view).toContain('Layout = "_LayoutTenantShell";');
  });

  test("create: an empty code is not a missing field and the POST carries no code — even if the box was filled", async () => {
    await boot(form);
    document.getElementById(form.code).value = "TYPED-BY-HAND";
    await submit(form);

    expect(writes()).toHaveLength(1);
    const [url, init] = writes()[0];
    expect(url).toBe(form.endpoint);
    expect(init.method).toBe("POST");
    expect(Object.prototype.hasOwnProperty.call(JSON.parse(init.body), "code"), "a create must not send a code").toBe(false);
    expect(document.getElementById(form.code).classList.contains("is-invalid")).toBe(false);
  });

  test("edit: the code box shows the stored code and the PUT echoes it unchanged", async () => {
    await boot(form, { mode: "edit" });
    expect(document.getElementById(form.code).value).toBe(form.stored.code);
    await submit(form);

    const put = writes().find(([, init]) => init.method === "PUT");
    expect(put, "the edit was not saved").toBeTruthy();
    expect(JSON.parse(put[1].body).code).toBe(form.stored.code);
  });

  test.each(Object.entries(REASONS))("a %s answer shows its own text, not the English errors[]", async (reason, key) => {
    await boot(form, {
      answer: { ok: false, status: 409, json: () => Promise.resolve({ reason_code: reason, errors: ["ENGLISH SERVICE TEXT"] }) }
    });
    await submit(form);

    const alert = document.getElementById(form.alert).textContent;
    expect(alert).toContain(`L10N:${key}`);
    expect(alert).not.toContain("ENGLISH SERVICE TEXT");
  });

  test("control: an answer without a record-code reason keeps today's errors[]", async () => {
    await boot(form, {
      answer: { ok: false, status: 409, json: () => Promise.resolve({ reason_code: "SOMETHING_ELSE", errors: ["SERVICE TEXT"] }) }
    });
    await submit(form);

    expect(document.getElementById(form.alert).textContent).toContain("SERVICE TEXT");
  });

  test("the l10n bridge hands every record-code text to the script", () => {
    const bridge = read(...form.l10nView);
    Object.values(REASONS).forEach((key) => expect(bridge).toContain(`${key} = Localizer["${key}"].Value`));
  });

  test("every key is in all 7 languages, filled, and no translation is left equal to English", () => {
    const valueOf = (lang, key) => {
      const file = read(...form.resources.slice(0, -1), `${form.resources[form.resources.length - 1]}.${lang}.resx`);
      const match = new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]*)</value></data>`).exec(file);
      return match ? match[1] : null;
    };
    KEYS.forEach((key) => {
      const english = valueOf("en", key);
      expect(english, `${key} is missing in en`).toBeTruthy();
      LANGUAGES.filter((lang) => lang !== "en").forEach((lang) => {
        const value = valueOf(lang, key);
        expect(value, `${key} is missing in ${lang}`).toBeTruthy();
        expect(value.trim(), `${key} is empty in ${lang}`).not.toBe("");
        expect(value, `${key} in ${lang} is the English text`).not.toBe(english);
      });
    });
  });
});
