const { loadScript } = require("./load-script");

function buildDom() {
  document.body.innerHTML = `
    <div id="strategy-period-form-error"></div>
    <div id="strategy-period-readiness-indicator"></div>
    <div id="strategy-period-readiness-text"></div>
    <ul id="strategy-period-readiness-missing"></ul>
    <ul id="strategy-period-readiness-blockers"></ul>
    <div id="strategy-period-sec-identity-state"></div>
    <div id="strategy-period-sec-scope-state"></div>
    <div id="strategy-period-sec-ownership-state"></div>
    <div id="strategy-period-sec-timing-state"></div>
    <input id="strategy-period-scope-summary" />
    <div id="strategy-period-parent-horizon-hint"></div>
    <select id="strategy-period-cycle"></select>
    <input id="strategy-period-name" />
    <input id="strategy-period-code" />
    <select id="strategy-period-status"><option value="Draft">Draft</option></select>
    <select id="strategy-period-company"><option value="">Select</option><option value="comp-1">Grand Medical Group</option></select>
    <select id="strategy-period-owner-company"><option value="">Select</option></select>
    <div id="strategy-period-owner-company-help"></div>
    <select id="strategy-period-owner-position"><option value="">Select</option></select>
    <div id="strategy-period-owner-position-help"></div>
    <input id="strategy-period-current-owner-person" type="hidden" />
    <input id="strategy-period-current-owner-person-display" />
    <div id="strategy-period-current-owner-person-help"></div>
    <input id="strategy-period-accountability-summary" />
    <select id="strategy-period-bu"><option value="">Select</option></select>
    <select id="strategy-period-region"><option value="">Select</option></select>
    <input id="strategy-period-start" />
    <input id="strategy-period-end" />
    <select id="strategy-period-review"><option value="">Select</option></select>
    <input id="strategy-period-default" type="checkbox" />
    <textarea id="strategy-period-notes"></textarea>
    <button id="strategy-period-save" type="button"></button>
    <button id="strategy-period-refresh" type="button"></button>
    <button id="strategy-period-reset" type="button"></button>
    <button id="strategy-period-create" type="button"></button>
    <input id="strategy-period-search" />
    <select id="strategy-period-status-filter"></select>
    <select id="strategy-period-cycle-filter"></select>
    <select id="strategy-period-company-filter"></select>
    <select id="strategy-period-review-filter"></select>
    <table id="strategy-periods-table"><tbody></tbody></table>
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
  window.strategyPeriodEditorMode = "create-page";
  window.enterpriseStrategyUi = { notify: vi.fn(), getErrorMessage: (err, fallback) => err?.message || fallback };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    ensurePositionsLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect,
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
    listCycles: vi.fn().mockResolvedValue([{ id: "cycle-1", name: "Cycle 1", ownerId: "user-1", effectiveFrom: "2026-03-01", effectiveTo: "2026-03-31" }]),
    listStrategyPeriods: vi.fn().mockResolvedValue([]),
    getStrategyPeriod: vi.fn().mockResolvedValue({}),
    createStrategyPeriod: vi.fn().mockResolvedValue({}),
    updateStrategyPeriod: vi.fn().mockResolvedValue({}),
    activatePeriod: vi.fn().mockResolvedValue({}),
    archivePeriod: vi.fn().mockResolvedValue({})
  };
  window.strategyCompaniesApi = { list: vi.fn().mockResolvedValue({ items: [{ companyId: "comp-1", companyName: "Grand Medical Group" }] }) };
  window.strategyEnterpriseMetaApi = {
    lookups: vi.fn().mockResolvedValue({
      reviewCadences: ["Monthly"],
      strategyPeriodLifecycleStatuses: ["Draft"],
      businessUnits: [],
      regions: []
    })
  };
  window.strategyGoalsApi = { list: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyObjectivesApi = { list: vi.fn().mockResolvedValue({ items: [] }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-periods.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("strategy period owner position", () => {
  beforeEach(async () => {
    await boot();
  });

  it("falls back to API position list when scoped positions are unavailable", async () => {
    const companyEl = document.getElementById("strategy-period-company");
    companyEl.value = "comp-1";
    companyEl.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const positionEl = document.getElementById("strategy-period-owner-position");
    expect(Array.from(positionEl.options).some((opt) => opt.value === "3")).toBe(true);
    expect(document.getElementById("strategy-period-owner-position-help").textContent).toContain("Showing API position list");
    expect(positionEl.disabled).toBe(false);
  });
});
