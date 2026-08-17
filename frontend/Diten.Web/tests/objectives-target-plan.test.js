const { loadScript } = require("./load-script");

function buildWorkspaceDom() {
  document.body.innerHTML = `
    <div id="objective-create-workspace">
      <div id="objective-form-error"></div>
      <h1 id="objective-modal-title"></h1>
      <div id="objective-modal-subtitle"></div>
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
      <button id="objective-save" type="button" class="d-none"></button>

      <input id="objective-id" />
      <select id="objective-parent-goal" class="form-select select2"><option value="">Search goal</option></select>
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
      <select id="objective-planning-cycle"><option value="">Select parent goal first</option></select>
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
      <input id="objective-target-plan-strategy-period" />
      <select id="objective-target-plan-granularity">
        <option value="Yearly">Yearly</option>
        <option value="Quarterly">Quarterly</option>
        <option value="Monthly">Monthly</option>
        <option value="TotalStrategyPeriod">Total Strategy Period</option>
      </select>
      <select id="objective-reporting-frequency"><option value="">Select frequency</option></select>
      <div id="objective-target-plan-governance-warning" class="d-none"></div>
      <div id="objective-target-plan-governance-warning-text"></div>

      <div id="objective-planning-context-helper"></div>
      <div id="objective-allowed-horizon-helper"></div>
      <div id="objective-parent-metric-summary"></div>
      <div id="objective-parent-goal-kpi-context-fields"></div>
      <div id="objective-kpi-alignment-context"></div>
      <div id="objective-parent-goal-target-context-fields"></div>
      <div id="objective-target-plan-comparison"></div>
      <span id="objective-scope-inherited-badge"></span>
      <input id="objective-target-plan-anchor" />
      <div id="objective-target-plan-context"></div>
      <div id="objective-target-plan-empty"></div>
      <span id="objective-target-plan-status-chip"></span>
      <button type="button" id="objective-generate-target-plan"></button>
      <button type="button" id="objective-regenerate-target-plan"></button>
      <button type="button" id="objective-target-plan-fill-flat"></button>
      <button type="button" id="objective-target-plan-copy-down"></button>
      <button type="button" id="objective-target-plan-interpolate"></button>
      <button type="button" id="objective-target-plan-clear-values"></button>
      <table><tbody id="objective-goal-target-reference-body"></tbody></table>
      <table><tbody id="objective-target-plan-body"></tbody></table>

      <div id="objective-readiness-panel"></div>
      <div id="objective-readiness-indicator"></div>
      <div id="objective-readiness-text"></div>
      <ul id="objective-readiness-missing"></ul>
      <ul id="objective-readiness-blockers"></ul>
      <ul id="objective-readiness-warnings"></ul>
      <div id="objective-readiness-draft-chip"></div>
      <div id="objective-readiness-publish-chip"></div>
      <div id="objective-readiness-plan-chip"></div>
      <div id="objective-readiness-targets-chip"></div>

      <select id="objective-creation-mode-select"><option value="Blank">Blank</option><option value="Template">Template</option></select>
      <button id="objective-browse-source" type="button"></button>
      <button id="objective-clear-source" type="button"></button>
      <div id="objective-source-summary"></div>
      <div id="objectiveSourcePickerModal"></div>
      <input id="objective-source-picker-search" />
      <select id="objective-source-picker-type"></select>
      <select id="objective-source-picker-status"></select>
      <input id="objective-source-picker-parent-goal-template" />
      <div id="objective-source-picker-helper"></div>
      <table><tbody id="objective-source-picker-tbody"></tbody></table>

      <datalist id="objective-company-list"></datalist>
      <datalist id="objective-filter-company-list"></datalist>
    </div>
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
  else if (options.defaultValue && Array.from(el.options).some((opt) => opt.value === options.defaultValue)) el.value = options.defaultValue;
}

async function boot() {
  buildWorkspaceDom();
  window.scrollTo = vi.fn();
  window.prompt = vi.fn();
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback,
    confirm: vi.fn().mockResolvedValue(true)
  };
  window.enterpriseModalFormUtils = {
    blockEnterSubmit: vi.fn(),
    setFieldError: vi.fn(),
    clearFieldError: vi.fn(),
    setSubmitting: vi.fn(),
    focusFirstInvalid: vi.fn(),
    backendErrors: vi.fn(),
    applyBackendFieldErrors: vi.fn()
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect,
    fillDatalist: vi.fn(),
    userOptions: () => [{
      value: "user-1",
      label: "User One",
      userRoles: [{ positionName: "Chief Executive Officer" }]
    }],
    userDisplayName: (id) => ({ "user-1": "User One" }[id] || id),
    userId: (id) => ({ "User One": "user-1" }[id] || id),
    positionDisplayName: (value) => value?.positionName || value || "",
    positionLoadState: () => ({ status: "ready", error: "" }),
    positionOptionsForCompany: (companyId) => companyId === "comp-1" ? [{ value: "Chief Executive Officer", label: "Chief Executive Officer" }] : [],
    positionOptions: () => [{ value: "Chief Executive Officer", label: "Chief Executive Officer" }],
    usersForOwnershipContext: (companyId, positionId) => (
      companyId === "comp-1" && positionId === "Chief Executive Officer"
        ? [{ id: "user-1", value: "user-1", fullName: "User One", label: "User One" }]
        : []
    ),
    companies: [
      { companyId: "comp-1", companyName: "Grand Medical Group" },
      { companyId: "comp-2", companyName: "Northwind Health" }
    ],
    companyLabel: (company) => company.companyName || company.companyId,
    companyOptions: () => [
      { value: "comp-1", label: "Grand Medical Group" },
      { value: "comp-2", label: "Northwind Health" }
    ],
    goalObjectiveTypes: ["Growth", "Transformation"],
    priorities: ["Critical", "High", "Medium"],
    businessUnits: [{ value: "Corporate", label: "Corporate" }],
    regions: [{ value: "Global", label: "Global" }],
    unitOfMeasure: ["Currency", "Percentage"],
    strategicThemes: ["theme-growth"],
    directionOfPerformance: ["Increase", "Maintain"],
    reportingFrequencies: ["Monthly", "Quarterly", "Yearly", "Annual", "Real Time"]
  };
  window.strategyEnterpriseMetaApi = {
    runtimeIdPreview: vi.fn().mockResolvedValue({ objectiveId: "O-000901" })
  };
  window.strategyObjectivesApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    create: vi.fn().mockResolvedValue({
      objective: {
        objectiveId: "O-9001",
        version: 1
      }
    })
  };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({
      items: [{
        goalId: "G-2001",
        goalTitle: "Expand Market Position",
        strategyPeriodId: "sp-2030",
        strategicThemeId: "theme-growth",
        category: "Growth",
        ownerRole: "Chief Executive Officer",
        ownerCompanyId: "comp-1",
        applicableCompanyIds: ["comp-1", "comp-2"],
        planningHorizonStart: "2027-01-01",
        planningHorizonEnd: "2030-12-31",
        businessUnitId: "Corporate",
        regionId: "Global"
      }]
    }),
    get: vi.fn().mockResolvedValue({
      goalId: "G-2001",
      goalTitle: "Expand Market Position",
      strategyPeriodId: "sp-2030",
      strategicThemeId: "theme-growth",
      category: "Growth",
      planningHorizonStart: "2027-01-01",
      planningHorizonEnd: "2030-12-31",
      ownerCompanyId: "comp-1",
      applicableCompanyIds: ["comp-1", "comp-2"],
      businessUnitId: "Corporate",
      regionId: "Global",
      metrics: [{
        id: "GM-1",
        metricDefinitionId: "KPI-1",
        metricName: "Revenue Growth",
        unitOfMeasure: "Currency",
        directionPolarity: "Increase",
        reportingFrequency: "Quarterly",
        thresholdModel: "Range",
        yearlyValues: [
          { year: 2027, targetValue: 100, actualValue: 95, forecastValue: 98, thresholdMin: 90, thresholdMax: 110 },
          { year: 2028, targetValue: 120, actualValue: 0, forecastValue: 118, thresholdMin: 100, thresholdMax: 130 }
        ]
      }]
    }),
    getPlanningContext: vi.fn().mockResolvedValue({
      strategyPeriodId: "sp-2030",
      strategyPeriodName: "FY27-30",
      strategyPeriodCode: "SP-27-30",
      strategyPeriodStatus: "Active",
      startDate: "2027-01-01",
      endDate: "2030-12-31"
    })
  };
  window.strategyPlanningApi = {
    listStrategyPeriods: vi.fn().mockResolvedValue([
      { id: "sp-2030", code: "SP-27-30", name: "FY27-30", startDate: "2027-01-01", endDate: "2030-12-31", cadence: "Annual" }
    ])
  };
  window.strategyKpisApi = {
    list: vi.fn().mockResolvedValue({
      items: [{
        id: "KPI-1",
        name: "Revenue Growth",
        unitOfMeasure: "Currency",
        direction: "Increase",
        reportingFrequency: "Annual"
      }]
    })
  };

  window.history.replaceState({}, "", "/management-governance/enterprise-strategy-business-performance/objectives/new");
  loadScript("wwwroot/assets/js/pages/enterprise-strategy/objectives.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("objective target plan", () => {
  beforeEach(async () => {
    await boot();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("generates yearly target rows by default and sends them in the create payload", async () => {
    document.getElementById("objective-parent-goal").value = "G-2001";
    document.getElementById("objective-parent-goal").dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-name").value = "Grow Revenue";
    document.getElementById("objective-statement").value = "Deliver the committed annual revenue growth target.";
    document.getElementById("objective-type").value = "Growth";
    document.getElementById("objective-priority").value = "High";
    document.getElementById("objective-owner-position").value = "Chief Executive Officer";
    document.getElementById("objective-owner-position").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-primary-kpi").value = "KPI-1";
    document.getElementById("objective-primary-kpi").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-generate-target-plan").click();

    expect(document.querySelectorAll("#objective-target-plan-body tr")).toHaveLength(4);
    expect(document.getElementById("objective-readiness-indicator").textContent).toContain("Blocked");

    window.prompt.mockReturnValue("125");
    document.getElementById("objective-target-plan-fill-flat").click();
    document.getElementById("objective-save").classList.remove("d-none");
    document.getElementById("objective-save").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(window.strategyObjectivesApi.create).toHaveBeenCalledWith(expect.objectContaining({
      objectiveName: "Grow Revenue",
      targetPlanGranularity: "Yearly",
      primaryMetricId: "KPI-1",
      metricAssignments: [
        expect.objectContaining({
          metricId: "KPI-1",
          yearlyValues: [
            expect.objectContaining({ year: 2027, periodKey: "2027", targetValue: 125 }),
            expect.objectContaining({ year: 2028, periodKey: "2028", targetValue: 125 }),
            expect.objectContaining({ year: 2029, periodKey: "2029", targetValue: 125 }),
            expect.objectContaining({ year: 2030, periodKey: "2030", targetValue: 125 })
          ]
        })
      ]
    }));
  });

  it("generates quarterly target rows from strategy period and warns when reporting is weaker", async () => {
    document.getElementById("objective-parent-goal").value = "G-2001";
    document.getElementById("objective-parent-goal").dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-primary-kpi").value = "KPI-1";
    document.getElementById("objective-primary-kpi").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-reporting-frequency").value = "Yearly";
    document.getElementById("objective-reporting-frequency").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-target-plan-granularity").value = "Quarterly";
    document.getElementById("objective-target-plan-granularity").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-generate-target-plan").click();

    const rows = document.querySelectorAll("#objective-target-plan-body tr");
    expect(rows).toHaveLength(16);
    expect(rows[0].textContent).toContain("2027-Q1");
    expect(rows[15].textContent).toContain("2030-Q4");
    expect(document.getElementById("objective-target-plan-governance-warning").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("objective-target-plan-governance-warning-text").textContent).toContain("Reporting cadence is less frequent than target cadence");
  });

  it("shows read-only parent goal KPI and target reference context in step 4", async () => {
    document.getElementById("objective-parent-goal").value = "G-2001";
    document.getElementById("objective-parent-goal").dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-primary-kpi").value = "KPI-1";
    document.getElementById("objective-primary-kpi").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("objective-reporting-frequency").value = "Yearly";
    document.getElementById("objective-reporting-frequency").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("objective-target-plan-granularity").value = "Quarterly";
    document.getElementById("objective-target-plan-granularity").dispatchEvent(new Event("change", { bubbles: true }));

    expect(document.getElementById("objective-parent-goal-kpi-context-fields").textContent).toContain("Revenue Growth");
    expect(document.getElementById("objective-parent-goal-kpi-context-fields").textContent).toContain("Currency");
    expect(document.getElementById("objective-parent-goal-target-context-fields").textContent).toContain("Goal Target Row Count");
    expect(document.getElementById("objective-target-plan-comparison").textContent).toContain("Objective KPI Direction");
    expect(document.getElementById("objective-target-plan-comparison").textContent).toContain("differs from the Parent Goal reporting cadence");
    expect(document.querySelectorAll("#objective-goal-target-reference-body tr")).toHaveLength(2);
    expect(document.getElementById("objective-goal-target-reference-body").textContent).toContain("2027");
    expect(document.getElementById("objective-goal-target-reference-body").textContent).toContain("100");
  });

  it("does not regenerate target rows when only reporting frequency changes", async () => {
    document.getElementById("objective-parent-goal").value = "G-2001";
    document.getElementById("objective-parent-goal").dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));

    document.getElementById("objective-primary-kpi").value = "KPI-1";
    document.getElementById("objective-primary-kpi").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("objective-target-plan-granularity").value = "Monthly";
    document.getElementById("objective-target-plan-granularity").dispatchEvent(new Event("change", { bubbles: true }));
    document.getElementById("objective-generate-target-plan").click();

    const rowCountBefore = document.querySelectorAll("#objective-target-plan-body tr").length;
    expect(rowCountBefore).toBe(48);
    const firstTargetValue = document.querySelector('#objective-target-plan-body input[data-field="targetValue"]');
    firstTargetValue.value = "33";
    firstTargetValue.dispatchEvent(new Event("input", { bubbles: true }));

    document.getElementById("objective-reporting-frequency").value = "Quarterly";
    document.getElementById("objective-reporting-frequency").dispatchEvent(new Event("change", { bubbles: true }));

    const rowCountAfter = document.querySelectorAll("#objective-target-plan-body tr").length;
    expect(rowCountAfter).toBe(48);
    expect(document.querySelector('#objective-target-plan-body input[data-field="targetValue"]').value).toBe("33");
    expect(document.getElementById("objective-target-plan-status-chip").textContent).not.toContain("Needs regenerate");
  });
});
