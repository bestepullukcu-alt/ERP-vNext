const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <table id="goals-table">
      <thead><tr id="goals-header-row"></tr></thead>
      <tbody></tbody>
    </table>
    <button id="goal-columns-btn" type="button">Columns</button>
    <div id="goal-active-filters"></div>
    <input id="goal-search" />
    <select id="goal-filter-category"></select>
    <select id="goal-filter-owner"></select>
    <select id="goal-filter-status"></select>
    <select id="goal-filter-priority"></select>
    <select id="goal-filter-scope-mode"></select>
    <input id="goal-filter-company" />
    <input id="goal-filter-year-range" />
    <input id="goal-filter-company-list" />
    <input id="goal-filter-scope" />
    <button id="goal-apply-filters" type="button">Apply</button>
    <button id="goal-reset-filters" type="button">Reset</button>
    <button id="goal-density-default" type="button">Default</button>
    <button id="goal-density-compact" type="button">Compact</button>
    <button id="goal-export-csv" type="button">CSV</button>
    <button id="goal-export-xlsx" type="button">XLSX</button>
    <button id="goal-export-workbook" type="button">Workbook</button>
    <button id="goal-bulk-clear-selection" type="button">Clear</button>
    <button id="goal-bulk-archive" type="button">Archive</button>
    <button id="goal-budget-fill-column" type="button"></button>
    <button id="goal-budget-interpolate" type="button"></button>
    <button id="goal-budget-copy-down" type="button"></button>
    <button id="goal-budget-clear-column" type="button"></button>
    <select id="goal-budget-helper-column"><option value="revenueTarget">Revenue</option></select>
    <input id="goal-name" />
    <select id="goal-category"><option value="Growth">Growth</option></select>
    <select id="goal-strategic-theme"><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="">Select</option><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option></select>
    <select id="goal-owner-person"><option value="">Select</option></select>
    <input id="goal-owner" />
    <select id="goal-status"><option value="Draft">Draft</option></select>
    <input id="goal-status-readonly" />
    <select id="goal-priority"><option value="Medium">Medium</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="">Select</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <select id="goal-scope-mode"><option value="Enterprise">Enterprise</option><option value="AppliesToSelectedCompanies">AppliesToSelectedCompanies</option></select>
    <select id="goal-primary-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option></select>
    <select id="goal-applicable-companies" multiple></select>
    <input id="goal-applies-to-all-companies" type="checkbox" />
    <input id="goal-change-log-ref" />
    <input id="goal-decision-reference" />
    <input id="goal-evidence-reference" />
    <input id="goal-version" />
    <input id="goal-budget-enabled" type="checkbox" />
    <div id="goal-budget-disabled-note"></div>
    <div id="goal-budget-content"></div>
    <table id="goal-budget-year-table"><tbody id="goal-budget-year-rows"></tbody></table>
    <div id="goal-form-error"></div>
    <div id="goal-metrics-editor"></div>
  `;
}

async function boot() {
  setupDom();
  global.bootstrap = {
    Modal: function () { return { show() {}, hide() {} }; },
    Offcanvas: { getInstance: () => null, getOrCreateInstance: () => ({ hide() {} }) }
  };
  window.enterpriseTableControls = {
    create: vi.fn().mockReturnValue({
      getVisibleColumns: () => [
        { key: "id", label: "Goal ID" },
        { key: "name", label: "Goal" },
        { key: "actions", label: "Actions" }
      ],
      getFilters: () => ({}),
      setFilters: vi.fn(),
      sortRows: (rows) => rows,
      sortIndicator: () => "",
      cycleSort: vi.fn()
    })
  };
  window.enterpriseTablePageUtils = {
    createPager: vi.fn().mockReturnValue({
      paginate: (rows) => rows,
      resetToFirstPage: vi.fn()
    }),
    ensureResetButton: vi.fn(),
    applyDensity: vi.fn()
  };
  window.enterpriseRowActionsMenu = {
    render: vi.fn((rowId, actions) => actions
      .filter((item) => item.action === "edit" || item.action === "duplicate")
      .map((item) => `<a href="#" class="es-row-action-item" data-action="${item.action}" data-row-id="${rowId}">${item.label}</a>`)
      .join(""))
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    ensurePositionsLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect: vi.fn(),
    fillUserSelect: vi.fn(),
    fillDatalist: vi.fn(),
    goalObjectiveTypes: ["Growth"],
    priorities: ["Medium"],
    lifecycleStatus: ["Draft"],
    strategicThemes: ["Growth"],
    companyOptions: () => []
  };
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback,
    confirm: vi.fn().mockResolvedValue(true)
  };
  window.enterpriseModalFormUtils = {
    blockEnterSubmit: vi.fn(),
    clearFieldError: vi.fn(),
    setFieldError: vi.fn(),
    setSubmitting: vi.fn(),
    backendErrors: vi.fn().mockReturnValue([]),
    focusFirstInvalid: vi.fn()
  };
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({
      items: [{
        goalId: "goal-42",
        goalTitle: "Expand Market Share",
        category: "Growth",
        status: "Draft",
        priority: "Medium"
      }]
    }),
    archive: vi.fn()
  };
  window.strategyPlanningApi = { listStrategyPeriods: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };
  window.enterpriseWorkbookIo = { exportCsv: vi.fn(), exportWorkbook: vi.fn() };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 10));
}

describe("goal list actions", () => {
  beforeEach(async () => {
    await boot();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("normalizes goalId rows before the action menu is rendered", async () => {
    for (let i = 0; i < 5 && !window.enterpriseRowActionsMenu.render.mock.calls.length; i += 1) {
      // Let the list page finish async lookup + render work.
      // eslint-disable-next-line no-await-in-loop
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    expect(window.enterpriseRowActionsMenu.render).toHaveBeenCalledWith("goal-42", expect.any(Array));
    expect(document.querySelector("#goals-table tbody tr td:nth-child(3)")?.textContent || "").toContain("Expand Market Share");
    expect(document.querySelector('.es-row-action-item[data-action="edit"]')?.dataset.rowId).toBe("goal-42");
    expect(document.querySelector('.es-row-action-item[data-action="duplicate"]')?.dataset.rowId).toBe("goal-42");
  });
});
