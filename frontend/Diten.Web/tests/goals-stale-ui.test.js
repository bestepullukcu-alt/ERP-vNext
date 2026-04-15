const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="goal-create-workspace"></div>
    <button id="goal-create"></button>
    <button id="goal-save"></button>
    <button id="goal-save-draft"></button>
    <button id="goal-add-metric"></button>
    <div id="goal-metrics-editor"></div>
    <div id="goal-form-error"></div>
    <div id="goalEditorModal"></div>
    <input id="goal-id" />
    <input id="goal-name" />
    <select id="goal-category"><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="comp-001">comp-001</option></select>
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="High">High</option></select>
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option></select>
    <select id="goal-strategy-period"><option value="sp-001">2026 Strategy Period</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-related-entity-scope-summary" />
    <input id="goal-entity-scope" />
    <input id="goal-owner" />
    <input id="goal-statement" />
    <input id="goal-change-log-ref" />
    <input id="goal-decision-reference" />
    <input id="goal-version" />
    <table><tbody id="goal-budget-year-rows"><tr>
      <td><input class="budget-year" value="2026" /></td>
      <td><input class="budget-rev" value="" /></td>
      <td><input class="budget-ebitda" value="" /></td>
      <td><input class="budget-capex" value="" /></td>
      <td><input class="budget-opex" value="" /></td>
      <td><input class="budget-savings" value="" /></td>
      <td><input class="budget-funding" value="" /></td>
      <td><input class="budget-commentary" value="" /></td>
    </tr></tbody></table>
    <div id="goal-summary-cards"></div>
  `;
}

describe("goals stale conflict UI", () => {
  beforeEach(() => {
    setupDom();
    global.bootstrap = { Modal: function () { return { show() {}, hide() {} }; } };
    window.enterpriseStrategyUi = {
      getErrorMessage: (err, fallback) =>
        err?.payload?.error?.code === "STALE_VERSION" ? "Record has changed. Reload and retry." : fallback,
    };
    window.strategyGoalsApi = {
      list: vi.fn().mockResolvedValue({ items: [] }),
      create: vi.fn().mockRejectedValue({ payload: { error: { code: "STALE_VERSION" } } }),
      update: vi.fn().mockRejectedValue({ payload: { error: { code: "STALE_VERSION" } } }),
      archive: vi.fn().mockResolvedValue({}),
    };
    window.strategyPlanningApi = {
      listActiveByScope: vi.fn().mockResolvedValue({
        items: [{
          id: "sp-001",
          status: "Active",
          startDate: "2026-01-01",
          endDate: "2026-12-31"
        }]
      })
    };
    window.enterpriseWorkbookOptions = {
      ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
      ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
      ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
      ensurePositionsLoaded: vi.fn().mockResolvedValue(undefined),
      companyOptions: () => [{ value: "comp-001", label: "comp-001" }],
      companyDisplayName: (id) => id,
      goalMetricType: ["%"],
      unitOfMeasure: ["Percentage"],
      goalAggregation: ["Sum"],
      directionOfPerformance: ["HigherIsBetter"],
      thresholdModels: ["None", "Band"],
      reportingFrequencies: ["Yearly"],
      priorities: ["High"],
      goalObjectiveTypes: ["Growth"],
      lifecycleStatus: ["Draft", "Active"],
      userOptions: () => [],
      userDisplayName: (id) => id,
      userId: (id) => id
    };
  });

  it("renders stale version error message on save", async () => {
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
    await new Promise((resolve) => setTimeout(resolve, 0));
    document.getElementById("goal-id").value = "goal-1";
    document.getElementById("goal-name").value = "Goal A";
    document.getElementById("goal-category").value = "Growth";
    document.getElementById("goal-strategic-theme").value = "Growth";
    document.getElementById("goal-owner-role").value = "Chief Executive Officer";
    document.getElementById("goal-owner-company").value = "comp-001";
    document.getElementById("goal-status").value = "Draft";
    document.getElementById("goal-priority").value = "High";
    document.getElementById("goal-scope-mode").value = "Enterprise";
    document.getElementById("goal-strategy-period").value = "sp-001";
    document.getElementById("goal-planning-start-year").value = "2026-01-01";
    document.getElementById("goal-planning-end-year").value = "2026-12-31";
    document.getElementById("goal-related-entity-scope-summary").value = "Enterprise";
    document.getElementById("goal-entity-scope").value = "Enterprise";
    document.getElementById("goal-statement").value = "Statement";
    document.getElementById("goal-change-log-ref").value = "CL-001";
    document.getElementById("goal-decision-reference").value = "DEC-001";
    document.getElementById("goal-version").value = "1";
    const hooks = window.__goalYearlyPlanTestHooks;
    const row = hooks.metricRow();
    document.getElementById("goal-metrics-editor").appendChild(row);
    row.querySelector(".metric-name").value = "Revenue Growth";
    row.querySelector(".metric-type").value = "%";
    row.querySelector(".metric-unit").value = "Percentage";
    row.querySelector(".metric-aggregation").value = "Sum";
    row.querySelector(".metric-polarity").value = "HigherIsBetter";
    row.querySelector(".metric-threshold-model").value = "Band";
    row.querySelector(".metric-threshold-model").dispatchEvent(new Event("change", { bubbles: true }));
    row.querySelector(".metric-reporting-frequency").value = "Yearly";
    const yearRow = row.querySelector(".metric-year-rows tr");
    yearRow.querySelector(".metric-year-target").value = "11";
    yearRow.querySelector(".metric-year-threshold-min").value = "8";
    yearRow.querySelector(".metric-year-threshold-max").value = "14";
    document.getElementById("goal-id").dispatchEvent(new Event("input"));
    document.getElementById("goal-name").dispatchEvent(new Event("input"));
    document.getElementById("goal-statement").dispatchEvent(new Event("input"));
    document.getElementById("goal-save-draft").click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(document.getElementById("goal-form-error").textContent).toContain("Record has changed. Reload and retry.");
  });
});
