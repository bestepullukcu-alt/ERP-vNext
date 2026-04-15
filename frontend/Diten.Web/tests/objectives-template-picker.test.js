const { loadScript } = require("./load-script");

function buildWorkspaceDom() {
  document.body.innerHTML = `
    <div id="objective-create-workspace">
      <div id="objective-form-error"></div>
      <h1 id="objective-modal-title"></h1>
      <div id="objective-modal-subtitle"></div>
      <div id="objective-creation-mode">
        <select id="objective-creation-mode-select">
          <option value="Blank">Blank</option>
          <option value="Template">From Objective Template</option>
        </select>
        <button id="objective-browse-source" type="button">Browse</button>
        <button id="objective-clear-source" type="button">Clear</button>
      <div id="objective-source-summary"></div>
      </div>
      <div id="objectiveSourcePickerModal"></div>
      <div id="objective-template-picker-current-goal"></div>
      <div id="objective-template-picker-current-goal-template"></div>
      <div id="objective-template-picker-current-type"></div>
      <div id="objective-template-picker-current-scope"></div>
      <div id="objective-template-picker-current-template"></div>
      <div id="objective-template-picker-context-warning" class="d-none"></div>
      <input id="objective-source-picker-search" />
      <select id="objective-source-picker-type"><option value="">All types</option></select>
      <select id="objective-source-picker-entity-scope"><option value="">All entity scopes</option></select>
      <input id="objective-source-picker-parent-goal-name" />
      <input id="objective-source-picker-parent-goal-template" />
      <div id="objective-source-picker-helper"></div>
      <table><tbody id="objective-source-picker-tbody"></tbody></table>
      <div id="objective-wizard-steps">
        <button type="button" class="objective-wizard-step-btn active" data-step="1"></button>
        <button type="button" class="objective-wizard-step-btn" data-step="2"></button>
        <button type="button" class="objective-wizard-step-btn" data-step="3"></button>
        <button type="button" class="objective-wizard-step-btn" data-step="4"></button>
      </div>
      <section class="objective-wizard-step-pane" data-step="1"></section>
      <section class="objective-wizard-step-pane d-none" data-step="2"></section>
      <section class="objective-wizard-step-pane d-none" data-step="3"></section>
      <section class="objective-wizard-step-pane d-none" data-step="4"></section>
      <button id="objective-step-back" type="button"></button>
      <button id="objective-step-next" type="button"></button>
      <button id="objective-save" type="button"></button>

      <input id="objective-id" />
      <select id="objective-parent-goal" class="form-select select2"><option value="">Search goal by ID or name</option></select>
      <input id="objective-name" />
      <select id="objective-type"><option value="">Select type</option></select>
      <textarea id="objective-statement"></textarea>
      <select id="objective-priority"><option value="">Select priority</option></select>
      <input id="objective-status-readonly" />
      <input id="objective-status" value="Draft" />
      <select id="objective-strategic-theme"><option value="">Select strategic theme</option></select>
      <input id="objective-theme-override" type="checkbox" />
      <select id="objective-owner"><option value="">Select owner</option></select>
      <select id="objective-owner-company"><option value="">Select owner company / org</option></select>
      <div id="objective-owner-company-help"></div>
      <select id="objective-owner-position"><option value="">Select owner company / org first</option></select>
      <div id="objective-owner-position-help"></div>
      <input id="objective-current-owner-person-display" />
      <input id="objective-current-owner-person" type="hidden" />
      <div id="objective-current-owner-person-help"></div>
      <input id="objective-accountability-summary" />
      <select id="objective-planning-cycle" disabled><option value="">Select parent goal first</option></select>
      <input id="objective-horizon-start-date" type="date" />
      <input id="objective-horizon-end-date" type="date" />
      <input id="objective-inherit-company-scope" type="checkbox" />
      <select id="objective-primary-company"><option value="">Select primary company</option></select>
      <select id="objective-applicable-companies" multiple></select>
      <div id="objective-applicable-companies-picker" data-placeholder="Search and select applicable companies...">
        <button type="button" id="objective-applicable-companies-toggle"></button>
        <span id="objective-applicable-companies-display"></span>
        <div id="objective-applicable-companies-panel" class="d-none">
          <button type="button" id="objective-applicable-companies-select-all"></button>
          <button type="button" id="objective-applicable-companies-clear-all"></button>
          <input id="objective-applicable-companies-search" />
          <div id="objective-applicable-companies-options"></div>
        </div>
      </div>
      <select id="objective-business-unit"><option value="">Select business unit</option></select>
      <select id="objective-region"><option value="">Select region</option></select>
      <input id="objective-entity-scope-summary" />
      <select id="objective-primary-kpi"><option value="">Select primary KPI / metric</option></select>
      <select id="objective-kpi-uom"><option value="">Select unit</option></select>
      <select id="objective-direction"><option value="">Select direction</option></select>
      <select id="objective-reporting-frequency"><option value="">Select frequency</option></select>

      <div id="objective-planning-context-helper"></div>
      <div id="objective-allowed-horizon-helper"></div>
      <div id="objective-parent-metric-summary"></div>
      <span id="objective-scope-inherited-badge"></span>
      <datalist id="objective-company-list"></datalist>
      <datalist id="objective-filter-company-list"></datalist>
    </div>
  `;
}

function fillSelect(el, items, options = {}) {
  if (!el) return;
  const current = el.multiple ? Array.from(el.selectedOptions || []).map((option) => option.value) : [String(el.value || "")];
  el.innerHTML = el.multiple ? "" : `<option value="">${options.placeholder || ""}</option>`;
  (items || []).forEach((item) => {
    const option = document.createElement("option");
    if (typeof item === "string") {
      option.value = item;
      option.textContent = item;
    } else {
      option.value = item.value;
      option.textContent = item.label;
    }
    if (current.includes(option.value)) option.selected = true;
    el.appendChild(option);
  });
  if (!el.multiple && options.defaultValue && Array.from(el.options).some((opt) => opt.value === options.defaultValue)) {
    el.value = options.defaultValue;
  }
}

async function boot(options = {}) {
  buildWorkspaceDom();
  window.history.pushState({}, "", "/management-governance/enterprise-strategy-business-performance/objectives/new");
  window.scrollTo = vi.fn();
  const modalSpy = { show: vi.fn(), hide: vi.fn() };
  global.bootstrap = {
    Modal: function () { return modalSpy; },
    Offcanvas: { getInstance: () => null, getOrCreateInstance: () => ({ hide() { } }) }
  };
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback,
    confirm: vi.fn().mockResolvedValue(true)
  };
  window.enterpriseModalFormUtils = {
    blockEnterSubmit: vi.fn(),
    setFieldError: vi.fn(),
    clearFieldError: vi.fn(),
    focusFirstInvalid: vi.fn(),
    setSubmitting: vi.fn()
  };
  window.esbpHorizonDates = {
    initIn: vi.fn(),
    setInputIso: vi.fn((input, iso) => { input.value = iso; }),
    getIsoFromInput: vi.fn((input) => String(input?.value || "").trim())
  };
  const goalList = options.goalList || {
    items: [{
      goalId: "G-1001",
      goalTitle: "Expand Market",
      strategyPeriodId: "sp-1",
      ownerCompanyId: "comp-1",
      sourceTemplateId: "GT-PARENT-1",
      sourceTemplateType: "Template",
      applicableCompanyIds: ["comp-1", "comp-2"],
      planningHorizonStart: "2026-03-24",
      planningHorizonEnd: "2026-04-24",
      businessUnitId: "Corporate",
      regionId: "Global",
      relatedEntityScope: "Primary: Grand Medical Group | Applicable: Grand Medical Group, Northwind Health | BU: Corporate | Region: Global",
      category: "Growth",
      strategicThemeId: "theme-growth"
    }]
  };
  const goalDetail = options.goalDetail || {
    goalId: "G-1001",
    goalTitle: "Expand Market",
    strategyPeriodId: "sp-1",
    ownerCompanyId: "comp-1",
    sourceTemplateId: "GT-PARENT-1",
    sourceTemplateType: "Template",
    planningHorizonStart: "2026-03-24",
    planningHorizonEnd: "2026-04-24",
    applicableCompanyIds: ["comp-1", "comp-2"],
    businessUnitId: "Corporate",
    regionId: "Global",
    relatedEntityScope: "Primary: Grand Medical Group | Applicable: Grand Medical Group, Northwind Health | BU: Corporate | Region: Global",
    category: "Growth",
    strategicThemeId: "theme-growth"
  };
  const catalog = options.catalog || {
    items: [
      {
        id: "OT-100",
        name: "Improve Data Quality",
        parentGoalTemplateId: "GT-PARENT-1",
        statement: "Increase trust in scorecard reporting",
        categoryOrType: "Growth",
        owner: "Chief Strategy Officer",
        priority: "Critical",
        entityScope: "Enterprise",
        status: "Published",
        templateType: "Objective",
        timeHorizonStart: "2026-01-01",
        timeHorizonEnd: "2026-12-31",
        version: 4
      },
      {
        id: "GT-OTHER",
        name: "Not An Objective Template",
        statement: "Should not appear",
        categoryOrType: "Growth",
        status: "Published",
        templateType: "Goal"
      }
    ]
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect,
    fillDatalist: vi.fn(),
    userOptions: () => [{ value: "user-7", label: "Alex Tan" }],
    userDisplayName: (id) => ({ "user-7": "Alex Tan" }[id] || id),
    userId: (id) => ({ "Alex Tan": "user-7" }[id] || id),
    positionLoadState: () => ({ status: "ready", error: "" }),
    positionOptionsForCompany: (companyId) => companyId === "comp-1"
      ? [{ value: "Chief Strategy Officer", label: "Chief Strategy Officer" }]
      : [],
    positionOptions: () => [{ value: "Chief Strategy Officer", label: "Chief Strategy Officer" }],
    usersForOwnershipContext: (companyId, positionId) => (
      companyId === "comp-1" && positionId === "Chief Strategy Officer"
        ? [{ id: "user-7", value: "user-7", fullName: "Alex Tan", label: "Alex Tan" }]
        : []
    ),
    companies: [
      { companyId: "comp-1", companyName: "Grand Medical Group", businessUnit: "Corporate", region: "Global" },
      { companyId: "comp-2", companyName: "Northwind Health", businessUnit: "Retail", region: "Europe" }
    ],
    companyLabel: (company) => company.companyName || company.companyId,
    companyDisplayName: (id) => ({ "comp-1": "Grand Medical Group", "comp-2": "Northwind Health" }[id] || id),
    companyOptions: () => [
      { value: "comp-1", label: "Grand Medical Group" },
      { value: "comp-2", label: "Northwind Health" }
    ],
    goalObjectiveTypes: options.goalObjectiveTypes || ["Growth", "Transformation"],
    priorities: ["Critical", "High", "Medium"],
    strategicThemes: [{ value: "theme-growth", label: "Growth" }],
    businessUnits: ["Corporate", "Retail"],
    regions: ["Global", "Europe"],
    unitOfMeasure: ["Currency"],
    reportingFrequencies: ["Quarterly"],
    directionOfPerformance: ["Increase"]
  };
  window.strategyEnterpriseMetaApi = {
    runtimeIdPreview: vi.fn().mockResolvedValue({ objectiveId: "O-000777" })
  };
  window.strategyObjectivesApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    get: vi.fn(),
    create: vi.fn(),
    update: vi.fn()
  };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue(goalList),
    get: vi.fn().mockResolvedValue(goalDetail),
    getPlanningContext: vi.fn().mockResolvedValue({
      strategyPeriodId: "sp-1",
      strategyPeriodName: "FY26",
      strategyPeriodCode: "SP-26",
      startDate: "2026-03-24",
      endDate: "2026-04-24"
    })
  };
  window.strategyPlanningApi = {
    listStrategyPeriods: vi.fn().mockResolvedValue([
      { id: "sp-1", code: "SP-26", name: "FY26", startDate: "2026-03-24", endDate: "2026-04-24" }
    ])
  };
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [{ id: "kpi-1", name: "Scorecard Data Quality Index" }] }) };
  window.strategyLibraryApi = {
    catalog: vi.fn().mockResolvedValue(catalog),
    template: vi.fn().mockResolvedValue({
      name: "Improve Data Quality",
      status: "Published",
      version: 4,
      objectivePrefill: {
        templateId: "OT-100",
        parentGoalTemplateId: "GT-PARENT-1",
        name: "Improve Data Quality",
        statement: "Increase trust in scorecard reporting",
        owner: "Chief Strategy Officer",
        type: "Growth",
        priority: "Critical",
        entityScope: "Enterprise",
        lifecycleStatus: "Published",
        timeHorizonStart: "2026-01-01",
        timeHorizonEnd: "2026-12-31",
        dependencyNotes: "Coordinate with scorecard governance."
      }
    })
  };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/objectives.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
  return { modalSpy };
}

describe("objective template picker", () => {
  it("opens the Objective Template modal with real catalog rows filtered to objective templates", async () => {
    const { modalSpy } = await boot();

    const parentGoal = document.getElementById("objective-parent-goal");
    parentGoal.value = "G-1001";
    parentGoal.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-creation-mode-select").value = "Template";
    document.getElementById("objective-creation-mode-select").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("objective-browse-source").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(window.strategyLibraryApi.catalog).toHaveBeenCalledWith(
      expect.objectContaining({ templateType: "Objective", parentGoalTemplateId: "GT-PARENT-1" }),
      expect.objectContaining({ skipCache: true })
    );
    expect(modalSpy.show).toHaveBeenCalled();
    expect(document.getElementById("objective-source-picker-tbody").textContent).toContain("Improve Data Quality");
    expect(document.getElementById("objective-source-picker-tbody").textContent).not.toContain("Not An Objective Template");
  });

  it("prefills safe Objective fields from the selected template without overriding parent-goal horizon and scope", async () => {
    await boot();

    const parentGoal = document.getElementById("objective-parent-goal");
    parentGoal.value = "G-1001";
    parentGoal.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-browse-source").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.querySelector(".objective-pick-source").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(document.getElementById("objective-name").value).toBe("Improve Data Quality");
    expect(document.getElementById("objective-statement").value).toBe("Increase trust in scorecard reporting");
    expect(document.getElementById("objective-type").value).toBe("Growth");
    expect(document.getElementById("objective-priority").value).toBe("Critical");
    expect(document.getElementById("objective-owner-position").value).toBe("Chief Strategy Officer");
    expect(document.getElementById("objective-current-owner-person").value).toBe("user-7");
    expect(document.getElementById("objective-horizon-start-date").value).toBe("2026-03-24");
    expect(document.getElementById("objective-horizon-end-date").value).toBe("2026-04-24");
    expect(document.getElementById("objective-entity-scope-summary").value).toContain("Grand Medical Group");
    expect(document.getElementById("objective-source-summary").textContent).toContain("GT-PARENT-1");
    expect(document.getElementById("objective-source-summary").textContent).toContain("Chief Strategy Officer");
  });

  it("shows only Operations objective templates when the parent goal type is Operations", async () => {
    await boot({
      goalObjectiveTypes: ["Operations", "Capability", "Risk", "Transformation"],
      goalList: {
        items: [{
          goalId: "G-2002",
          goalTitle: "Operational Excellence",
          strategyPeriodId: "sp-1",
          ownerCompanyId: "comp-1",
          sourceTemplateId: "GT-OPS-1",
          sourceTemplateType: "Template",
          applicableCompanyIds: ["comp-1"],
          planningHorizonStart: "2026-03-24",
          planningHorizonEnd: "2026-04-24",
          businessUnitId: "Operations",
          regionId: "Global",
          relatedEntityScope: "Primary: Grand Medical Group | BU: Operations | Region: Global",
          category: "Operations",
          strategicThemeId: "theme-growth"
        }]
      },
      goalDetail: {
        goalId: "G-2002",
        goalTitle: "Operational Excellence",
        strategyPeriodId: "sp-1",
        ownerCompanyId: "comp-1",
        sourceTemplateId: "GT-OPS-1",
        sourceTemplateType: "Template",
        planningHorizonStart: "2026-03-24",
        planningHorizonEnd: "2026-04-24",
        applicableCompanyIds: ["comp-1"],
        businessUnitId: "Operations",
        regionId: "Global",
        relatedEntityScope: "Primary: Grand Medical Group | BU: Operations | Region: Global",
        category: "Operations",
        strategicThemeId: "theme-growth"
      },
      catalog: {
        items: [
          {
            id: "OT-OPS-1",
            name: "Optimize Cycle Efficiency",
            parentGoalTemplateId: "GT-OPS-1",
            statement: "Improve operational throughput",
            categoryOrType: "Operations",
            owner: "Chief Operating Officer",
            priority: "High",
            entityScope: "Enterprise",
            status: "Published",
            templateType: "Objective"
          },
          {
            id: "OT-CAP-1",
            name: "Build Shared Capability",
            parentGoalTemplateId: "GT-OPS-1",
            statement: "Improve enablement systems",
            categoryOrType: "Capability",
            owner: "Chief Operating Officer",
            priority: "High",
            entityScope: "Enterprise",
            status: "Published",
            templateType: "Objective"
          }
        ]
      }
    });

    const parentGoal = document.getElementById("objective-parent-goal");
    parentGoal.innerHTML = '<option value="">Search goal by ID or name</option><option value="G-2002">G-2002 - Operational Excellence</option>';
    parentGoal.value = "G-2002";
    parentGoal.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-creation-mode-select").value = "Template";
    document.getElementById("objective-creation-mode-select").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("objective-browse-source").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(window.strategyLibraryApi.catalog).toHaveBeenCalledWith(
      expect.objectContaining({ templateType: "Objective", parentGoalTemplateId: "GT-OPS-1", categoryOrType: "Operations" }),
      expect.objectContaining({ skipCache: true })
    );
    expect(document.getElementById("objective-source-picker-tbody").textContent).toContain("Optimize Cycle Efficiency");
    expect(document.getElementById("objective-source-picker-tbody").textContent).not.toContain("Build Shared Capability");
  });
});
