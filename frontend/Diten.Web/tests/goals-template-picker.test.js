const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
    <div id="goalEditorModal"></div>
    <button id="goal-save"></button>
    <button id="goal-save-draft"></button>
    <button id="goal-validate"></button>
    <button id="goal-add-metric" type="button">Add KPI</button>
    <button id="goal-step-back" type="button"></button>
    <button id="goal-step-next" type="button"></button>
    <div id="goal-wizard-steps">
      <button type="button" class="goal-wizard-step-btn active" data-step="1"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="2"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="3"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="4"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="5"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="6"></button>
      <button type="button" class="goal-wizard-step-btn" data-step="7"></button>
    </div>
    <div class="goal-wizard-step-pane" data-step="1"></div>
    <div class="goal-wizard-step-pane d-none" data-step="2"></div>
    <div class="goal-wizard-step-pane d-none" data-step="3"></div>
    <div class="goal-wizard-step-pane d-none" data-step="4"></div>
    <div class="goal-wizard-step-pane d-none" data-step="5"></div>
    <div class="goal-wizard-step-pane d-none" data-step="6"></div>
    <div class="goal-wizard-step-pane d-none" data-step="7"></div>
    <div id="goal-metrics-editor"></div>
    <div id="goal-form-error"></div>
    <div id="goal-readiness-panel"></div>
    <div id="goal-publish-readiness-indicator"></div>
    <div id="goal-publish-readiness-text"></div>
    <div id="goal-validation-mode-indicator"></div>
    <div id="goal-backend-alignment-indicator"></div>
    <div id="goal-readiness-kpi-count"></div>
    <div id="goal-readiness-kpi-missing-yearly"></div>
    <div id="goal-readiness-budget-enabled"></div>
    <div id="goal-readiness-governance-missing"></div>
    <ul id="goal-readiness-missing-required"></ul>
    <ul id="goal-readiness-publish-blockers"></ul>
    <select id="goal-creation-mode-select">
      <option value="Blank">Blank</option>
      <option value="Template">From Goal Template</option>
    </select>
    <button id="goal-browse-source" type="button">Browse</button>
    <button id="goal-clear-source" type="button">Clear</button>
    <div id="goal-source-summary"></div>
    <div id="goalSourcePickerModal"></div>
    <div id="goal-template-picker-current-goal"></div>
    <div id="goal-template-picker-current-type"></div>
    <div id="goal-template-picker-current-scope"></div>
    <div id="goal-template-picker-current-status"></div>
    <div id="goal-template-picker-current-template"></div>
    <div id="goal-template-picker-context-warning" class="d-none"></div>
    <div id="goal-template-picker-helper"></div>
    <input id="goal-source-picker-search" />
    <select id="goal-source-picker-type"><option value="">All types</option></select>
    <select id="goal-source-picker-entity-scope"><option value="">All entity scopes</option></select>
    <table><tbody id="goal-source-picker-tbody"></tbody></table>
    <input id="goal-id" />
    <input id="goal-name" />
    <select id="goal-category"><option value="">Select</option><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="">Select</option><option value="theme-1">Growth</option></select>
    <select id="goal-owner-role"><option value="">Select</option><option value="CEO">CEO</option></select>
    <select id="goal-owner-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option></select>
    <select id="goal-owner-person"><option value="">Select</option><option value="user-1">User One</option></select>
    <input id="goal-owner" />
    <input id="goal-owner-accountable-display" />
    <select id="goal-status"><option value="Draft">Draft</option></select>
    <input id="goal-status-readonly" />
    <select id="goal-priority"><option value="">Select</option><option value="High">High</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="">Select</option><option value="sp-1">SP-1</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <input id="goal-planning-scope-preview" />
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option></select>
    <select id="goal-primary-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option></select>
    <select id="goal-applicable-companies" multiple><option value="comp-001">Grand Medical Group</option></select>
    <div id="goal-applicable-companies-picker" data-placeholder="Select applicable companies...">
      <button type="button" id="goal-applicable-companies-toggle"><span id="goal-applicable-companies-display"></span></button>
      <div id="goal-applicable-companies-panel" class="d-none">
        <button type="button" id="goal-applicable-companies-select-all">Select all</button>
        <button type="button" id="goal-applicable-companies-clear-all">Clear all</button>
        <input id="goal-applicable-companies-search" />
        <div id="goal-applicable-companies-options"></div>
      </div>
    </div>
    <input id="goal-applies-to-all-companies" type="checkbox" />
    <input id="goal-change-log-ref" />
    <input id="goal-decision-reference" />
    <input id="goal-evidence-reference" />
    <input id="goal-version" />
    <input id="goal-budget-enabled" type="checkbox" />
    <div id="goal-budget-disabled-note"></div>
    <div id="goal-budget-content"></div>
    <select id="goal-budget-helper-column"><option value="revenueTarget">Revenue Target</option></select>
    <button id="goal-budget-fill-column" type="button"></button>
    <button id="goal-budget-interpolate" type="button"></button>
    <button id="goal-budget-copy-down" type="button"></button>
    <button id="goal-budget-clear-column" type="button"></button>
    <table id="goal-budget-year-table"><tbody id="goal-budget-year-rows"></tbody></table>
  `;
}

async function boot() {
  setupDom();
  window.history.pushState({}, "", "/management-governance/enterprise-strategy-business-performance/goals/new");
  const modalSpy = { show: vi.fn(), hide: vi.fn() };
  global.bootstrap = {
    Modal: function () { return modalSpy; },
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
    thresholdModels: ["None"],
    reportingFrequencies: ["Yearly"],
    goalObjectiveTypes: ["Growth"],
    priorities: ["High"],
    lifecycleStatus: ["Draft"],
    positionOptions: () => [{ value: "CEO", label: "CEO" }],
    companyOptions: () => [{ value: "comp-001", label: "Grand Medical Group" }],
    userOptions: () => [{ value: "user-1", label: "User One", companyName: "Grand Medical Group" }],
    companyDisplayName: (id) => ({ "comp-001": "Grand Medical Group" }[id] || id),
    companyLabel: (company) => company.companyName || company.companyId,
    userDisplayName: (id) => ({ "user-1": "User One" }[id] || id),
    userId: (id) => id,
    fillSelect: (el, items, options = {}) => {
      if (!el) return;
      const current = el.multiple ? Array.from(el.selectedOptions || []).map((option) => option.value) : [el.value];
      el.innerHTML = el.multiple ? "" : `<option value="">${options.placeholder || "Select"}</option>`;
      (items || []).forEach((item) => {
        const option = document.createElement("option");
        option.value = typeof item === "string" ? item : item.value;
        option.textContent = typeof item === "string" ? item : item.label;
        if (current.includes(option.value)) option.selected = true;
        el.appendChild(option);
      });
    },
    fillDatalist: vi.fn()
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
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    create: vi.fn(),
    update: vi.fn(),
    archive: vi.fn(),
    get: vi.fn()
  };
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyPlanningApi = {
    listStrategyPeriods: vi.fn().mockResolvedValue({ items: [{ id: "sp-1", status: "Active", startDate: "2026-01-01", endDate: "2026-12-31" }] }),
    listActiveByScope: vi.fn().mockResolvedValue({ items: [] })
  };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };
  window.strategyLibraryApi = {
    catalog: vi.fn().mockResolvedValue({
      items: [
        { id: "GT-1", name: "Growth", category: "Growth", categoryOrType: "Growth", statement: "Grow revenue", templateType: "Goal", owner: "CEO", entityScope: "Enterprise", status: "Published", version: 3 },
        { id: "BP-1", name: "Pack", category: "Pack", statement: "Should be filtered", templateType: "BlueprintPack", version: 1 }
      ]
    }),
    template: vi.fn().mockResolvedValue({
      name: "Growth",
      version: 3,
      attributes: { statement: "Grow revenue", category: "Growth", strategicThemeId: "theme-1" },
      goalPrefill: {
        name: "Growth",
        statement: "Grow revenue",
        category: "Growth",
        strategicThemeId: "theme-1",
        priority: "High",
        planningStartYear: "2026-01-01",
        planningEndYear: "2026-12-31"
      },
      goalMetrics: [],
      goalYearlyBudgets: []
    })
  };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 20));
  return { modalSpy };
}

describe("goal template picker", () => {
  it("opens the goal template modal and filters the catalog to goal templates", async () => {
    const { modalSpy } = await boot();

    document.getElementById("goal-creation-mode-select").value = "Template";
    document.getElementById("goal-creation-mode-select").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("goal-browse-source").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(window.strategyLibraryApi.catalog).toHaveBeenCalled();
    expect(modalSpy.show).toHaveBeenCalled();
    expect(document.getElementById("goal-source-picker-tbody").textContent).toContain("Growth");
    expect(document.getElementById("goal-source-picker-tbody").textContent).toContain("Enterprise");
    expect(document.getElementById("goal-source-picker-tbody").textContent).not.toContain("Pack");
    expect(document.getElementById("goal-source-picker-type").textContent).toContain("Growth");
  });
});
