const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
    <button id="goal-add-metric" type="button">Add KPI</button>
    <div id="goal-metrics-editor"></div>
    <div id="goal-form-error"></div>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-name" />
    <select id="goal-category"><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="Growth">Growth</option></select>
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="Medium">Medium</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option></select>
    <select id="goal-strategy-period"></select>
    <input id="goal-related-entity-scope-summary" />
    <input id="goal-entity-scope" />
    <input id="goal-change-log-ref" />
    <input id="goal-decision-reference" />
    <input id="goal-evidence-reference" />
    <input id="goal-version" />
    <div id="goal-budget-year-rows"></div>
    <div id="goal-source-summary"></div>
    <div id="goal-primary-company"></div>
    <select id="goal-applicable-companies" multiple></select>
    <input id="goal-applies-to-all-companies" type="checkbox" />
  `;
}

async function bootScript() {
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
    companyOptions: () => [],
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
  window.strategyPlanningApi = { listActiveByScope: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

function setPlanningHorizon(startIso, endIso) {
  const startEl = document.getElementById("goal-planning-start-year");
  const endEl = document.getElementById("goal-planning-end-year");
  startEl.value = startIso;
  endEl.value = endIso;
  startEl.dispatchEvent(new Event("change", { bubbles: true }));
  endEl.dispatchEvent(new Event("change", { bubbles: true }));
}

function addMetricRow() {
  document.getElementById("goal-add-metric").click();
  const row = document.querySelector("#goal-metrics-editor .metric-row");
  expect(row).toBeTruthy();
  return row;
}

describe("goals yearly KPI grid", () => {
  beforeEach(async () => {
    await bootScript();
  });

  it("derives yearly rows from planning horizon", () => {
    setPlanningHorizon("2026-01-01", "2028-12-31");
    addMetricRow();
    const years = Array.from(document.querySelectorAll(".metric-year")).map((el) => Number(el.value));
    expect(years).toEqual([2026, 2027, 2028]);
  });

  it("supports fill flat, copy down, interpolate and clear actions", () => {
    setPlanningHorizon("2026-01-01", "2028-12-31");
    const row = addMetricRow();

    vi.spyOn(window, "prompt").mockReturnValueOnce("12.5");
    row.querySelector(".metric-fill-flat").click();
    expect(Array.from(row.querySelectorAll(".metric-year-target")).map((x) => x.value)).toEqual(["12.5", "12.5", "12.5"]);

    const thresholdModel = row.querySelector(".metric-threshold-model");
    thresholdModel.value = "Band";
    thresholdModel.dispatchEvent(new Event("change", { bubbles: true }));

    const yearRows = Array.from(row.querySelectorAll(".metric-year-rows tr"));
    yearRows[0].querySelector(".metric-year-target").value = "10";
    yearRows[0].querySelector(".metric-year-threshold-min").value = "1";
    yearRows[0].querySelector(".metric-year-threshold-max").value = "2";
    yearRows[0].querySelector(".metric-year-commentary").value = "seed";
    row.querySelector(".metric-copy-prev").click();
    expect(yearRows[1].querySelector(".metric-year-target").value).toBe("10");
    expect(yearRows[2].querySelector(".metric-year-threshold-min").value).toBe("1");
    expect(yearRows[2].querySelector(".metric-year-commentary").value).toBe("seed");

    vi.spyOn(window, "prompt").mockReturnValueOnce("10").mockReturnValueOnce("30");
    row.querySelector(".metric-fill-linear").click();
    expect(Array.from(row.querySelectorAll(".metric-year-target")).map((x) => Number(x.value))).toEqual([10, 20, 30]);

    row.querySelector(".metric-clear-years").click();
    expect(Array.from(row.querySelectorAll(".metric-year-target")).every((x) => x.value === "")).toBe(true);
    expect(Array.from(row.querySelectorAll(".metric-year-commentary")).every((x) => x.value === "")).toBe(true);
  });

  it("applies threshold fields conditionally by threshold model", () => {
    setPlanningHorizon("2026-01-01", "2027-12-31");
    const row = addMetricRow();
    const thresholdModel = row.querySelector(".metric-threshold-model");
    thresholdModel.value = "None";
    thresholdModel.dispatchEvent(new Event("change", { bubbles: true }));
    expect(Array.from(row.querySelectorAll(".metric-year-threshold-min")).every((x) => x.disabled)).toBe(true);
    expect(Array.from(row.querySelectorAll(".metric-threshold-col")).every((x) => x.classList.contains("d-none"))).toBe(true);

    thresholdModel.value = "Band";
    thresholdModel.dispatchEvent(new Event("change", { bubbles: true }));
    expect(Array.from(row.querySelectorAll(".metric-year-threshold-min")).every((x) => !x.disabled)).toBe(true);
    expect(Array.from(row.querySelectorAll(".metric-threshold-col")).every((x) => !x.classList.contains("d-none"))).toBe(true);
  });

  it("serializes yearly rows to yearlyTargets and StrategicGoalMetricYearlyTarget aliases", () => {
    setPlanningHorizon("2026-01-01", "2027-12-31");
    const row = addMetricRow();
    row.querySelector(".metric-name").value = "Revenue Growth";
    row.querySelector(".metric-type").value = "%";
    row.querySelector(".metric-unit").value = "Percentage";
    row.querySelector(".metric-aggregation").value = "Sum";
    row.querySelector(".metric-polarity").value = "HigherIsBetter";
    row.querySelector(".metric-threshold-model").value = "Band";
    row.querySelector(".metric-threshold-model").dispatchEvent(new Event("change", { bubbles: true }));
    row.querySelector(".metric-reporting-frequency").value = "Yearly";

    const yearRows = Array.from(row.querySelectorAll(".metric-year-rows tr"));
    yearRows[0].querySelector(".metric-year-target").value = "11.5";
    yearRows[0].querySelector(".metric-year-threshold-min").value = "8";
    yearRows[0].querySelector(".metric-year-threshold-max").value = "14";
    yearRows[1].querySelector(".metric-year-target").value = "13";
    yearRows[1].querySelector(".metric-year-threshold-min").value = "9";
    yearRows[1].querySelector(".metric-year-threshold-max").value = "15";

    const payload = window.__goalYearlyPlanTestHooks.collectCreateRequest();
    expect(payload.metrics).toHaveLength(1);
    expect(payload.metrics[0].yearlyTargets).toHaveLength(2);
    expect(payload.metrics[0].yearlyTargets[0]).toMatchObject({
      year: 2026,
      targetValue: 11.5,
      thresholdMin: 8,
      thresholdMax: 14
    });
    expect(payload.metrics[0].strategicGoalMetricYearlyTargets).toHaveLength(2);
    expect(payload.metrics[0].strategicGoalMetricYearlyTargets[1]).toMatchObject({
      year: 2027,
      targetValue: 13,
      thresholdMin: 9,
      thresholdMax: 15
    });
  });
});
