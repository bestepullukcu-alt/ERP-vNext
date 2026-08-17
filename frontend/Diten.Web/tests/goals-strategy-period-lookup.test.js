const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
    <button id="goal-save"></button>
    <button id="goal-save-draft"></button>
    <button id="goal-validate"></button>
    <button id="goal-add-metric" type="button">Add KPI</button>
    <div id="goal-metrics-editor"></div>
    <div id="goal-form-error"></div>
    <input id="goal-id" />
    <input id="goal-name" />
    <select id="goal-category"><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="">Select</option><option value="comp-001">comp-001</option></select>
    <select id="goal-owner-person"><option value="">Select</option></select>
    <input id="goal-owner" />
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="Medium">Medium</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="">Select</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option><option value="AppliesToSelectedCompanies">AppliesToSelectedCompanies</option></select>
    <select id="goal-primary-company"><option value="">Select</option><option value="comp-001">comp-001</option><option value="comp-002">comp-002</option></select>
    <select id="goal-applicable-companies" multiple></select>
    <input id="goal-applies-to-all-companies" type="checkbox" />
    <input id="goal-change-log-ref" />
    <input id="goal-decision-reference" />
    <input id="goal-evidence-reference" />
    <input id="goal-version" />
    <input id="goal-budget-enabled" type="checkbox" />
    <div id="goal-budget-disabled-note"></div>
    <div id="goal-budget-content"></div>
    <select id="goal-budget-helper-column">
      <option value="revenueTarget">Revenue Target</option>
    </select>
    <button id="goal-budget-fill-column" type="button">Fill flat</button>
    <button id="goal-budget-interpolate" type="button">Interpolate</button>
    <button id="goal-budget-copy-down" type="button">Copy down</button>
    <button id="goal-budget-clear-column" type="button">Clear</button>
    <table id="goal-budget-year-table"><tbody id="goal-budget-year-rows"></tbody></table>
  `;
}

async function boot() {
  setupDom();
  global.bootstrap = {
    Modal: function () { return { show() { }, hide() { } }; },
    Offcanvas: { getInstance: () => null, getOrCreateInstance: () => ({ hide() { } }) }
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    goalMetricType: ["%"],
    unitOfMeasure: ["Percentage"],
    goalAggregation: ["Sum"],
    directionOfPerformance: ["HigherIsBetter"],
    thresholdModels: ["None", "Band"],
    reportingFrequencies: ["Yearly"],
    goalObjectiveTypes: ["Growth"],
    priorities: ["High", "Medium"],
    lifecycleStatus: ["Draft", "Active", "Archived"],
    companyOptions: () => [
      { value: "comp-001", label: "comp-001" },
      { value: "comp-002", label: "comp-002" }
    ],
    userOptions: () => [],
    companyDisplayName: (id) => id,
    userDisplayName: (id) => id,
    userId: (id) => id
  };
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback,
    confirm: vi.fn().mockResolvedValue(true)
  };
  window.enterpriseModalFormUtils = {
    clearFieldError: vi.fn(),
    setFieldError: vi.fn(),
    blockEnterSubmit: vi.fn(),
    setSubmitting: vi.fn(),
    backendErrors: vi.fn().mockReturnValue([]),
    focusFirstInvalid: vi.fn()
  };
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    create: vi.fn(),
    update: vi.fn(),
    archive: vi.fn(),
    get: vi.fn()
  };
  window.strategyPlanningApi = {
    listStrategyPeriods: vi.fn().mockResolvedValue({
      items: [
        { id: "sp-active", status: "Active", companyId: "comp-001", name: "Active Period", startDate: "2026-01-01", endDate: "2026-12-31" },
        { id: "sp-draft", status: "Draft", companyId: "comp-001", name: "Draft Period", startDate: "2027-01-01", endDate: "2027-12-31" },
        { id: "sp-archived", status: "Archived", companyId: "comp-002", name: "Archived Period", startDate: "2025-01-01", endDate: "2025-12-31" }
      ]
    }),
    listActiveByScope: vi.fn().mockResolvedValue({ items: [] })
  };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("goals strategy period lookup", () => {
  beforeEach(async () => {
    await boot();
  });

  it("shows the full strategy-period catalog while keeping only Active periods selectable", () => {
    expect(window.strategyPlanningApi.listStrategyPeriods).toHaveBeenCalled();
    expect(window.strategyPlanningApi.listActiveByScope).not.toHaveBeenCalled();

    const options = Array.from(document.querySelectorAll("#goal-strategy-period option"));
    expect(options.map((option) => option.value)).toEqual(["", "sp-active", "sp-draft", "sp-archived"]);
    expect(options.find((option) => option.value === "sp-active")?.disabled).toBe(false);
    expect(options.find((option) => option.value === "sp-draft")?.disabled).toBe(true);
    expect(options.find((option) => option.value === "sp-archived")?.disabled).toBe(true);
    expect(options.find((option) => option.value === "sp-draft")?.textContent).toContain("[Draft]");
    expect(options.find((option) => option.value === "sp-archived")?.textContent).toContain("[Archived]");
  });
});
