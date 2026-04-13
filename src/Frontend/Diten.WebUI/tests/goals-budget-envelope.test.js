const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
    <button id="goal-add-metric" type="button">Add KPI</button>
    <div id="goal-metrics-editor"></div>
    <div id="goal-form-error"></div>
    <input id="goal-id" />
    <input id="goal-name" />
    <select id="goal-category"><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="comp-001">comp-001</option></select>
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="High">High</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="sp-001">SP</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option></select>
    <select id="goal-primary-company"><option value="comp-001">comp-001</option></select>
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
    companyOptions: () => [{ value: "comp-001", label: "comp-001" }],
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

function setRequiredFields() {
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
}

function setHorizon(startIso, endIso) {
  const start = document.getElementById("goal-planning-start-year");
  const end = document.getElementById("goal-planning-end-year");
  start.value = startIso;
  end.value = endIso;
  start.dispatchEvent(new Event("change", { bubbles: true }));
  end.dispatchEvent(new Event("change", { bubbles: true }));
}

describe("goals budget envelope block", () => {
  beforeEach(async () => {
    await boot();
    setRequiredFields();
    setHorizon("2026-01-01", "2028-12-31");
  });

  it("is optional and excluded from payload when disabled", () => {
    const hooks = window.__goalYearlyPlanTestHooks;
    expect(hooks.isBudgetEnvelopeEnabled()).toBe(false);
    const payloadDisabled = hooks.collectCreateRequest();
    expect(payloadDisabled.yearlyBudgets).toEqual([]);
    expect(payloadDisabled.budgetEnvelopes).toEqual([]);

    const toggle = document.getElementById("goal-budget-enabled");
    toggle.checked = true;
    toggle.dispatchEvent(new Event("change", { bubbles: true }));
    document.querySelector(".budget-rev").value = "100";
    const payloadEnabled = hooks.collectCreateRequest();
    expect(payloadEnabled.yearlyBudgets.length).toBeGreaterThan(0);
    expect(payloadEnabled.budgetEnvelopes.length).toBe(payloadEnabled.yearlyBudgets.length);

    toggle.checked = false;
    toggle.dispatchEvent(new Event("change", { bubbles: true }));
    const payloadDisabledAgain = hooks.collectCreateRequest();
    expect(payloadDisabledAgain.yearlyBudgets).toEqual([]);
  });

  it("supports quick-fill actions on selected column", () => {
    const toggle = document.getElementById("goal-budget-enabled");
    toggle.checked = true;
    toggle.dispatchEvent(new Event("change", { bubbles: true }));

    const select = document.getElementById("goal-budget-helper-column");
    select.value = "revenueTarget";

    vi.spyOn(window, "prompt").mockReturnValueOnce("12.5");
    document.getElementById("goal-budget-fill-column").click();
    expect(Array.from(document.querySelectorAll(".budget-rev")).map((x) => x.value)).toEqual(["12.5", "12.5", "12.5"]);

    const rows = Array.from(document.querySelectorAll("#goal-budget-year-rows tr"));
    rows[0].querySelector(".budget-rev").value = "9";
    document.getElementById("goal-budget-copy-down").click();
    expect(rows[1].querySelector(".budget-rev").value).toBe("9");
    expect(rows[2].querySelector(".budget-rev").value).toBe("9");

    vi.spyOn(window, "prompt").mockReturnValueOnce("10").mockReturnValueOnce("30");
    document.getElementById("goal-budget-interpolate").click();
    expect(Array.from(document.querySelectorAll(".budget-rev")).map((x) => Number(x.value))).toEqual([10, 20, 30]);

    document.getElementById("goal-budget-clear-column").click();
    expect(Array.from(document.querySelectorAll(".budget-rev")).every((x) => x.value === "")).toBe(true);
  });

  it("validates budget years only when budget rows exist", () => {
    const hooks = window.__goalYearlyPlanTestHooks;
    const payload = hooks.collectCreateRequest();
    const errorsWithoutBudget = hooks.validate(payload);
    expect(errorsWithoutBudget.some((x) => String(x).toLowerCase().includes("yearly budget must include exactly"))).toBe(false);

    payload.yearlyBudgets = [{
      year: 2025,
      revenueTarget: 100,
      ebitdaTarget: null,
      capexEnvelope: null,
      opexEnvelope: null,
      savingsTarget: null,
      fundingPoolEnvelope: null,
      commentary: null
    }];
    const errorsWithOutOfRangeBudget = hooks.validate(payload);
    expect(errorsWithOutOfRangeBudget.some((x) => String(x).toLowerCase().includes("out-of-range year"))).toBe(true);
  });
});
