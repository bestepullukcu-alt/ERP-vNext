const { loadScript } = require("./load-script");

function buildDom() {
  document.body.innerHTML = `
    <div id="planning-cycle-form-error"></div>
    <input id="planning-cycle-name" />
    <input id="planning-cycle-code" />
    <select id="planning-cycle-type"><option value="">Select</option><option value="Annual Plan">Annual Plan</option></select>
    <select id="planning-cycle-status"><option value="Draft">Draft</option></select>
    <select id="planning-cycle-owner-company"><option value="">Select</option><option value="comp-1">Grand Medical Group</option></select>
    <div id="planning-cycle-owner-company-help"></div>
    <select id="planning-cycle-owner-position"><option value="">Select</option></select>
    <div id="planning-cycle-owner-position-help"></div>
    <input id="planning-cycle-current-owner-person" type="hidden" />
    <input id="planning-cycle-current-owner-person-display" />
    <div id="planning-cycle-current-owner-person-help"></div>
    <input id="planning-cycle-accountability-summary" />
    <textarea id="planning-cycle-description"></textarea>
    <input id="planning-cycle-effective-from" />
    <input id="planning-cycle-effective-to" />
    <div id="planning-cycle-readiness-indicator"></div>
    <div id="planning-cycle-readiness-text"></div>
    <ul id="planning-cycle-readiness-missing"></ul>
    <ul id="planning-cycle-readiness-blockers"></ul>
    <div id="planning-cycle-sec-identity-state"></div>
    <div id="planning-cycle-sec-horizon-state"></div>
    <button id="planning-cycle-save" type="button"></button>
    <button id="planning-cycle-refresh" type="button"></button>
    <button id="planning-cycle-reset" type="button"></button>
    <button id="planning-cycle-create" type="button"></button>
    <input id="planning-cycle-search" />
    <select id="planning-cycle-status-filter"></select>
    <select id="planning-cycle-type-filter"></select>
    <select id="planning-cycle-owner-filter"></select>
    <table id="planning-cycles-table"><tbody></tbody></table>
  `;
}

function fillSelect(el, items, options = {}) {
  if (!el) return;
  const current = String(el.value || "");
  el.innerHTML = options.placeholder === null ? "" : `<option value="">${options.placeholder || ""}</option>`;
  (items || []).forEach((item) => {
    const option = document.createElement("option");
    if (typeof item === "string") {
      option.value = item;
      option.textContent = item;
    } else {
      option.value = item.value;
      option.textContent = item.label;
    }
    el.appendChild(option);
  });
  if (current && Array.from(el.options).some((opt) => opt.value === current)) el.value = current;
}

async function boot() {
  buildDom();
  global.bootstrap = { Modal: function () { return { show() {}, hide() {} }; } };
  window.planningCycleEditorMode = "create-page";
  window.enterpriseStrategyUi = { notify: vi.fn(), getErrorMessage: (err, fallback) => err?.message || fallback };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    ensurePositionsLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect,
    fillUserSelect: vi.fn(),
    companyOptions: () => [{ value: "comp-1", label: "Grand Medical Group" }],
    companyDisplayName: (id) => ({ "comp-1": "Grand Medical Group" }[id] || id),
    userDisplayName: (id) => ({ "user-1": "User One" }[id] || id),
    positionDisplayName: (id) => ({ "3": "HEAD OF COUNTRY" }[id] || id),
    positionLoadState: () => ({ status: "success", error: "" }),
    positionOptionsForCompany: () => [],
    positionOptions: () => [{ value: "3", label: "HEAD OF COUNTRY" }],
    usersForOwnershipContext: () => []
  };
  window.strategyPlanningApi = {
    listCycles: vi.fn().mockResolvedValue([]),
    createCycle: vi.fn().mockResolvedValue({}),
    updateCycle: vi.fn().mockResolvedValue({}),
    getCycle: vi.fn().mockResolvedValue({}),
    activateCycle: vi.fn().mockResolvedValue({}),
    archiveCycle: vi.fn().mockResolvedValue({})
  };
  window.strategyEnterpriseMetaApi = { lookups: vi.fn().mockResolvedValue({ planningCycleTypes: ["Annual Plan"], planningLifecycleStatuses: ["Draft"] }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/planning-cycles.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("planning cycle owner position", () => {
  beforeEach(async () => {
    await boot();
  });

  it("falls back to API position list when company-scoped positions are unavailable", async () => {
    const companyEl = document.getElementById("planning-cycle-owner-company");
    companyEl.value = "comp-1";
    companyEl.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const positionEl = document.getElementById("planning-cycle-owner-position");
    expect(Array.from(positionEl.options).some((opt) => opt.value === "3")).toBe(true);
    expect(document.getElementById("planning-cycle-owner-position-help").textContent).toContain("Showing API position list");
    expect(positionEl.disabled).toBe(false);
  });
});
