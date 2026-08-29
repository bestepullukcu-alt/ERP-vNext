const { loadScript } = require("./load-script");

function buildDom() {
  document.body.innerHTML = `
    <div id="cycle-period-form-error"></div>
    <div id="cycle-period-readiness-indicator"></div>
    <div id="cycle-period-readiness-text"></div>
    <ul id="cycle-period-readiness-missing"></ul>
    <ul id="cycle-period-readiness-blockers"></ul>
    <div id="cycle-period-sec-identity-state"></div>
    <div id="cycle-period-sec-scope-state"></div>
    <div id="cycle-period-sec-ownership-state"></div>
    <div id="cycle-period-sec-timing-state"></div>
    <input id="cycle-period-scope-summary" />
    <div id="cycle-period-parent-horizon-hint"></div>
    <select id="cycle-period-cycle"></select>
    <input id="cycle-period-name" />
    <input id="cycle-period-code" />
    <select id="cycle-period-status"><option value="Draft">Draft</option></select>
    <select id="cycle-period-company"><option value="">Select</option><option value="comp-1">Grand Medical Group</option></select>
    <select id="cycle-period-owner-company"><option value="">Select</option></select>
    <div id="cycle-period-owner-company-help"></div>
    <select id="cycle-period-owner-position"><option value="">Select</option></select>
    <div id="cycle-period-owner-position-help"></div>
    <input id="cycle-period-current-owner-person" type="hidden" />
    <input id="cycle-period-current-owner-person-display" />
    <div id="cycle-period-current-owner-person-help"></div>
    <input id="cycle-period-accountability-summary" />
    <select id="cycle-period-bu"><option value="">Select</option></select>
    <select id="cycle-period-region"><option value="">Select</option></select>
    <input id="cycle-period-start" />
    <input id="cycle-period-end" />
    <select id="cycle-period-review"><option value="">Select</option></select>
    <input id="cycle-period-default" type="checkbox" />
    <textarea id="cycle-period-notes"></textarea>
    <button id="cycle-period-save" type="button"></button>
    <button id="cycle-period-refresh" type="button"></button>
    <button id="cycle-period-reset" type="button"></button>
    <button id="cycle-period-create" type="button"></button>
    <input id="cycle-period-search" />
    <select id="cycle-period-status-filter"></select>
    <select id="cycle-period-cycle-filter"></select>
    <select id="cycle-period-company-filter"></select>
    <select id="cycle-period-review-filter"></select>
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

function installJQueryHarness() {
  const jquery = (target) => {
    const elements = typeof target === "string"
      ? Array.from(document.querySelectorAll(target))
      : (target ? [target] : []);
    const api = {
      length: elements.length,
      removeAttr(name) { elements.forEach((element) => element.removeAttribute(name)); return api; }
    };
    return api;
  };
  jquery.fn = {};
  global.$ = window.$ = jquery;
}

async function boot() {
  buildDom();
  global.bootstrap = { Modal: function () { return { show() {}, hide() {} }; } };
  installJQueryHarness();
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
    getAllPositions: vi.fn().mockResolvedValue([{ PositionId: "3", PositionName: "HEAD OF COUNTRY" }]),
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
  await window.strategyPeriodsApp.initPage();
}

describe("strategy period owner position", () => {
  beforeEach(async () => {
    await boot();
  });

  it("falls back to API position list when scoped positions are unavailable", async () => {
    const companyEl = document.getElementById("cycle-period-company");
    companyEl.value = "comp-1";
    companyEl.dispatchEvent(new Event("change", { bubbles: true }));
    for (let i = 0; i < 10 && !Array.from(document.getElementById("cycle-period-owner-position").options).some((opt) => opt.value === "3"); i += 1) {
      // Wait for the asynchronous company-pair and position refresh handlers.
      // eslint-disable-next-line no-await-in-loop
      await new Promise((resolve) => setTimeout(resolve, 0));
    }

    const positionEl = document.getElementById("cycle-period-owner-position");
    expect(Array.from(positionEl.options).some((opt) => opt.value === "3")).toBe(true);
    expect(window.strategyPlanningApi.getAllPositions).toHaveBeenCalledTimes(1);
    expect(positionEl.disabled).toBe(false);
  });
});
