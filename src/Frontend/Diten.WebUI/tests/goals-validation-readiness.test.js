const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
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
    <button id="goal-step-back" type="button"></button>
    <button id="goal-step-next" type="button"></button>
    <button id="goal-save"></button>
    <button id="goal-save-draft"></button>
    <button id="goal-validate"></button>
    <button id="goal-add-metric" type="button">Add KPI</button>
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

    <input id="goal-id" />
    <input id="goal-name" />
    <select id="goal-category"><option value="">Select</option><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="">Select</option><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="">Select</option><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="">Select</option><option value="comp-001">comp-001</option></select>
    <select id="goal-owner-person"><option value="">Select</option></select>
    <input id="goal-owner-accountable-display" />
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="">Select</option><option value="High">High</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="">Select</option><option value="sp-001">SP</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <div class="form-control-validation">
      <select id="goal-scope-mode">
        <option value="">Select</option>
        <option value="Enterprise">Enterprise</option>
        <option value="AppliesToSelectedCompanies">Selected Companies</option>
      </select>
      <div id="goal-company-mode-hint"></div>
    </div>
    <div class="form-control-validation">
      <select id="goal-primary-company"><option value="">Select</option><option value="comp-001">comp-001</option><option value="comp-002">comp-002</option></select>
      <div id="goal-primary-company-hint"></div>
    </div>
    <div class="form-control-validation">
      <select id="goal-applicable-companies" multiple></select>
      <div id="goal-applicable-companies-picker" data-placeholder="Search and select applicable companies...">
        <button type="button" id="goal-applicable-companies-toggle"><span id="goal-applicable-companies-display"></span></button>
        <div id="goal-applicable-companies-panel" class="d-none">
          <button type="button" id="goal-applicable-companies-select-all">Select all</button>
          <button type="button" id="goal-applicable-companies-clear-all">Clear all</button>
          <input id="goal-applicable-companies-search" />
          <div id="goal-applicable-companies-options"></div>
        </div>
      </div>
      <div id="goal-applicable-companies-hint"></div>
    </div>
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
      <option value="ebitdaTarget">EBITDA Target</option>
      <option value="capexEnvelope">Capex Envelope</option>
      <option value="opexEnvelope">Opex Envelope</option>
      <option value="savingsTarget">Savings Target</option>
      <option value="fundingPoolEnvelope">Funding Pool</option>
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
    lifecycleStatus: ["Draft", "Active"],
    companyOptions: () => [{ value: "comp-001", label: "comp-001" }, { value: "comp-002", label: "comp-002" }],
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
    listActiveByScope: vi.fn().mockResolvedValue({
      items: [{
        id: "sp-001",
        status: "Active",
        startDate: "2026-01-01",
        endDate: "2028-12-31"
      }]
    })
  };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };
  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

function setDraftMinimum() {
  document.getElementById("goal-name").value = "Goal A";
  document.getElementById("goal-category").value = "Growth";
  document.getElementById("goal-strategic-theme").value = "Growth";
  document.getElementById("goal-owner-role").value = "Chief Executive Officer";
  document.getElementById("goal-owner-company").value = "comp-001";
  document.getElementById("goal-status").value = "Draft";
  document.getElementById("goal-priority").value = "High";
  document.getElementById("goal-statement").value = "Statement";
  document.getElementById("goal-strategy-period").value = "sp-001";
  document.getElementById("goal-scope-mode").value = "Enterprise";
  document.getElementById("goal-entity-scope").value = "Enterprise";
  document.getElementById("goal-related-entity-scope-summary").value = "Enterprise";
  document.getElementById("goal-planning-start-year").value = "2026-01-01";
  document.getElementById("goal-planning-end-year").value = "2028-12-31";
  document.getElementById("goal-planning-start-year").dispatchEvent(new Event("change", { bubbles: true }));
  document.getElementById("goal-planning-end-year").dispatchEvent(new Event("change", { bubbles: true }));
}

describe("goals validation readiness", () => {
  beforeEach(async () => {
    await boot();
    setDraftMinimum();
  });

  it("uses lighter draft validation and strict publish validation", () => {
    const hooks = window.__goalYearlyPlanTestHooks;
    const payload = hooks.collectCreateRequest();
    const draftErrors = hooks.validate(payload, { mode: "draft" });
    const publishErrors = hooks.validate(payload, { mode: "publish" });

    expect(draftErrors.some((x) => String(x).toLowerCase().includes("primary kpi"))).toBe(true);
    expect(draftErrors.some((x) => String(x).toLowerCase().includes("changelogref"))).toBe(false);
    expect(draftErrors.some((x) => String(x).toLowerCase().includes("decisionreference"))).toBe(false);

    expect(publishErrors.some((x) => String(x).toLowerCase().includes("active kpi"))).toBe(true);
    expect(publishErrors.some((x) => String(x).toLowerCase().includes("changelogref"))).toBe(true);
    expect(publishErrors.some((x) => String(x).toLowerCase().includes("decisionreference"))).toBe(true);
  });

  it("updates readiness panel facts for KPI and budget state", () => {
    document.getElementById("goal-budget-enabled").checked = true;
    document.getElementById("goal-budget-enabled").dispatchEvent(new Event("change", { bubbles: true }));

    document.getElementById("goal-add-metric").click();
    const row = document.querySelector("#goal-metrics-editor .metric-row");
    row.querySelector(".metric-name").value = "Revenue Growth";
    row.querySelector(".metric-name").dispatchEvent(new Event("input", { bubbles: true }));

    const hooks = window.__goalYearlyPlanTestHooks;
    const payload = hooks.collectCreateRequest();
    const snapshot = hooks.computeValidationSnapshot(payload);
    hooks.renderValidationReadiness(snapshot, "draft");

    expect(document.getElementById("goal-readiness-kpi-count").textContent).toContain("KPI count: 1 (active: 1)");
    expect(document.getElementById("goal-readiness-kpi-missing-yearly").textContent).toContain("KPI rows missing yearly targets: 1");
    expect(document.getElementById("goal-readiness-budget-enabled").textContent).toContain("enabled");
  });

  it("shows backend alignment as OK for core draft/publish rules", () => {
    const hooks = window.__goalYearlyPlanTestHooks;
    const payload = hooks.collectCreateRequest();
    const snapshot = hooks.computeValidationSnapshot(payload);
    hooks.renderValidationReadiness(snapshot, "draft");
    expect(document.getElementById("goal-backend-alignment-indicator").textContent).toContain("Backend alignment: OK");
  });

  it("uses a wizard flow and reveals the create action only on the last step", () => {
    expect(document.getElementById("goal-save").classList.contains("d-none")).toBe(true);
    document.getElementById("goal-step-next").click();
    expect(document.querySelector('.goal-wizard-step-pane[data-step="2"]').classList.contains("d-none")).toBe(false);
  });

  it("keeps applies-to-all derived from mode and leaves company selection available for selected-company applicability", () => {
    const scopeMode = document.getElementById("goal-scope-mode");
    const appliesAll = document.getElementById("goal-applies-to-all-companies");
    const applicable = document.getElementById("goal-applicable-companies");
    const applicableToggle = document.getElementById("goal-applicable-companies-toggle");
    const applicableHost = applicable.closest(".form-control-validation");
    const hooks = window.__goalYearlyPlanTestHooks;

    scopeMode.value = "AppliesToSelectedCompanies";
    scopeMode.dispatchEvent(new Event("change", { bubbles: true }));

    expect(appliesAll.checked).toBe(false);
    expect(appliesAll.disabled).toBe(true);
    expect(applicable.disabled).toBe(false);
    expect(applicableToggle.disabled).toBe(false);
    expect(applicableHost.classList.contains("d-none")).toBe(false);
    expect(Array.from(applicable.options).map((o) => o.value)).toEqual(["comp-001", "comp-002"]);

    applicable.options[0].selected = true;
    applicable.dispatchEvent(new Event("change", { bubbles: true }));

    const payload = hooks.collectCreateRequest();
    expect(payload.companyScope.scopeModeCode).toBe("MultiCompany");
    expect(payload.companyScope.appliesToAllCompaniesFlag).toBe(false);
    expect(payload.companyScope.applicableCompanyIds).toEqual(["comp-001"]);
  });
});
