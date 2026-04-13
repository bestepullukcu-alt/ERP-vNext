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
    <select id="goal-strategic-theme"><option value="">Select</option><option value="Growth">Growth</option></select>
    <select id="goal-owner-role"><option value="">Select</option><option value="Chief Executive Officer">Chief Executive Officer</option></select>
    <select id="goal-owner-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option><option value="comp-002">Northwind Health</option></select>
    <select id="goal-owner-person"><option value="">Select</option><option value="user-1">User One</option></select>
    <input id="goal-owner" />
    <input id="goal-owner-accountable-display" />
    <select id="goal-status"><option value="Draft">Draft</option><option value="Active">Active</option></select>
    <select id="goal-priority"><option value="High">High</option><option value="Medium">Medium</option></select>
    <textarea id="goal-statement"></textarea>
    <select id="goal-strategy-period"><option value="">Select</option></select>
    <input id="goal-planning-start-year" />
    <input id="goal-planning-end-year" />
    <input id="goal-entity-scope" />
    <input id="goal-related-entity-scope-summary" />
    <select id="goal-scope-mode">
      <option value="Enterprise">Enterprise</option>
      <option value="AppliesToSelectedCompanies">AppliesToSelectedCompanies</option>
    </select>
    <select id="goal-primary-company"><option value="">Select</option><option value="comp-001">Grand Medical Group</option><option value="comp-002">Northwind Health</option></select>
    <select id="goal-applicable-companies" multiple>
      <option value="comp-001">Grand Medical Group</option>
      <option value="comp-002">Northwind Health</option>
    </select>
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
  window.history.pushState({}, "", "/management-governance/enterprise-strategy-business-performance/goals/goal-1/edit");
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
    positionOptions: () => [{ value: "Chief Executive Officer", label: "Chief Executive Officer" }],
    companyOptions: () => [
      { value: "comp-001", label: "Grand Medical Group" },
      { value: "comp-002", label: "Northwind Health" }
    ],
    userOptions: () => [{ value: "user-1", label: "User One", companyName: "Grand Medical Group" }],
    companyDisplayName: (id) => ({ "comp-001": "Grand Medical Group", "comp-002": "Northwind Health" }[id] || id),
    companyLabel: (company) => company.companyName || company.companyId,
    userDisplayName: (id) => ({ "user-1": "User One" }[id] || id),
    userId: (id) => id,
    fillSelect: (el, items, options = {}) => {
      if (!el) return;
      const current = el.multiple
        ? Array.from(el.selectedOptions || []).map((option) => option.value)
        : [el.value];
      el.innerHTML = el.multiple ? "" : `<option value="">${options.placeholder || "Select"}</option>`;
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
      if (options.defaultValue && Array.from(el.options).some((option) => option.value === options.defaultValue)) {
        el.value = options.defaultValue;
      }
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
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [] }) };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    create: vi.fn(),
    update: vi.fn(),
    archive: vi.fn(),
    get: vi.fn().mockResolvedValue({
      goal: {
        goalId: "goal-1",
        goalTitle: "Expansion Goal",
        category: "Growth",
        ownerRole: "Chief Executive Officer",
        ownerCompanyId: "comp-001",
        ownerPersonId: "user-1",
        status: "Active",
        priority: "High",
        goalStatement: "Expand into new markets",
        strategyPeriodId: "sp-1",
        startDate: "2026-01-01",
        endDate: "2026-12-31",
        relatedEntityScope: "Corporate | Global",
        applicabilityMode: "AppliesToSelectedCompanies",
        appliesToAllCompanies: false,
        applicableCompanyIds: ["comp-001", "comp-002"],
        governance: {
          changeLogRef: "CHG-42",
          decisionReference: "DEC-100",
          evidenceReference: "https://example.com/evidence",
          version: 7
        },
        metrics: [],
        budgetEnvelopes: [{ year: 2026, revenueTarget: 10 }],
        version: 7
      }
    })
  };
  window.strategyPlanningApi = {
    listStrategyPeriods: vi.fn().mockResolvedValue({
      items: [
        { id: "sp-1", status: "Active", companyId: "comp-001", name: "FY26 Period", startDate: "2026-01-01", endDate: "2026-12-31" }
      ]
    }),
    listActiveByScope: vi.fn().mockResolvedValue({ items: [] })
  };
  window.strategyEnterpriseMetaApi = { runtimeIdPreview: vi.fn().mockResolvedValue({ goalId: "G-0001" }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/goals.js");
  await new Promise((resolve) => setTimeout(resolve, 10));
}

describe("goal edit hydration", () => {
  beforeEach(async () => {
    await boot();
  });

  it("hydrates edit fields from the goal detail payload", () => {
    expect(window.strategyGoalsApi.get).toHaveBeenCalledWith("goal-1");
    expect(document.getElementById("goal-name").value).toBe("Expansion Goal");
    expect(document.getElementById("goal-statement").value).toBe("Expand into new markets");
    expect(document.getElementById("goal-status").value).toBe("Active");
    expect(document.getElementById("goal-priority").value).toBe("High");
    expect(document.getElementById("goal-strategy-period").value).toBe("sp-1");
    expect(document.getElementById("goal-planning-start-year").value).toBe("2026-01-01");
    expect(document.getElementById("goal-planning-end-year").value).toBe("2026-12-31");
    expect(document.getElementById("goal-related-entity-scope-summary").value).toContain("Grand Medical Group");
    expect(document.getElementById("goal-scope-mode").value).toBe("AppliesToSelectedCompanies");
    expect(document.getElementById("goal-change-log-ref").value).toBe("CHG-42");
    expect(document.getElementById("goal-decision-reference").value).toBe("DEC-100");
    expect(document.getElementById("goal-evidence-reference").value).toBe("https://example.com/evidence");
    expect(document.getElementById("goal-version").value).toBe("7");
    expect(Array.from(document.getElementById("goal-applicable-companies").selectedOptions).map((option) => option.value))
      .toEqual(["comp-001", "comp-002"]);
  });

  it("keeps the primary save button visible while editing before the last wizard step", () => {
    expect(document.getElementById("goal-save").classList.contains("d-none")).toBe(false);
  });
});
