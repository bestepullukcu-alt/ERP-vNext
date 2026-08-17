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
      <button id="objective-save" type="button"></button>

      <input id="objective-id" />
      <select id="objective-parent-goal" class="form-select select2"><option value="">Search goal by ID or name</option></select>
      <input id="objective-name" />
      <select id="objective-type"><option value="">Select type</option></select>
      <textarea id="objective-statement"></textarea>
      <select id="objective-priority"><option value="">Select priority</option></select>
      <input id="objective-status-readonly" />
      <input id="objective-status" />
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

function toDmy(iso) {
  const [year, month, day] = String(iso || "").split("-");
  return year && month && day ? `${day}/${month}/${year}` : "";
}

async function boot() {
  buildWorkspaceDom();
  window.scrollTo = vi.fn();
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback,
    confirm: vi.fn().mockResolvedValue(true)
  };
  window.enterpriseModalFormUtils = {
    blockEnterSubmit: vi.fn(),
    setFieldError: vi.fn()
  };
  window.esbpHorizonDates = {
    initIn: vi.fn(),
    setInputIso: vi.fn((input, iso) => {
      input.value = toDmy(iso);
    }),
    getIsoFromInput: vi.fn((input) => {
      const value = String(input?.value || "").trim();
      const parts = value.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
      return parts ? `${parts[3]}-${parts[2]}-${parts[1]}` : value;
    })
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect,
    fillDatalist: vi.fn(),
    userOptions: () => [{ value: "user-1", label: "User One" }],
    userDisplayName: (id) => ({ "user-1": "User One" }[id] || id),
    userId: (id) => ({ "User One": "user-1" }[id] || id),
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
      { companyId: "comp-2", companyName: "Northwind Health" },
      { companyId: "comp-3", companyName: "Mefar Ilac Sanayii A.S." }
    ],
    companyLabel: (company) => company.companyName || company.companyId,
    companyOptions: () => [
      { value: "comp-1", label: "Grand Medical Group" },
      { value: "comp-2", label: "Northwind Health" },
      { value: "comp-3", label: "Mefar Ilac Sanayii A.S." }
    ],
    goalObjectiveTypes: ["Growth"],
    priorities: ["Critical", "High", "Medium"],
    businessUnits: [],
    regions: [],
    unitOfMeasure: []
  };
  window.strategyEnterpriseMetaApi = {
    runtimeIdPreview: vi.fn().mockResolvedValue({ objectiveId: "O-000001" })
  };
  window.strategyObjectivesApi = {
    list: vi.fn().mockResolvedValue({ items: [] }),
    get: vi.fn()
  };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({
      items: [{
        goalId: "G-1001",
        goalTitle: "Expand Market",
        strategyPeriodId: "sp-1",
        ownerRole: "Chief Executive Officer",
        ownerCompanyId: "comp-1",
        applicableCompanyIds: ["comp-1", "comp-2"],
        planningHorizonStart: "2026-03-24",
        planningHorizonEnd: "2026-04-24",
        businessUnitId: "Corporate",
        regionId: "Global",
        relatedEntityScope: "Company: comp-1 | BU: Corporate | Region: Global"
      }]
    }),
    get: vi.fn().mockResolvedValue({
      goalId: "G-1001",
      goalTitle: "Expand Market",
      strategyPeriodId: "sp-1",
      planningHorizonStart: "2026-03-24",
      planningHorizonEnd: "2026-04-24",
      applicableCompanyIds: ["comp-1", "comp-2"],
      ownerCompanyId: "comp-1",
      businessUnitId: "Corporate",
      regionId: "Global",
      relatedEntityScope: "Company: comp-1 | BU: Corporate | Region: Global"
    }),
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
  window.strategyKpisApi = { list: vi.fn().mockResolvedValue({ items: [] }) };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/objectives.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("objective parent goal inheritance", () => {
  beforeEach(async () => {
    await boot();
  });

  it("inherits strategy period, horizon dates, and supports checkbox-style applicable companies selection", async () => {
    const parentGoal = document.getElementById("objective-parent-goal");
    parentGoal.value = "G-1001";
    parentGoal.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const planning = document.getElementById("objective-planning-cycle");
    const start = document.getElementById("objective-horizon-start-date");
    const end = document.getElementById("objective-horizon-end-date");
    const display = document.getElementById("objective-applicable-companies-display");
    const unlock = document.getElementById("objective-inherit-company-scope");
    const toggle = document.getElementById("objective-applicable-companies-toggle");
    const panel = document.getElementById("objective-applicable-companies-panel");

    expect(planning.value).toBe("sp-1");
    expect(planning.selectedOptions[0].textContent).toContain("SP-26 - FY26 | 2026-2026");
    expect(start.value).toBe("2026-03-24");
    expect(end.value).toBe("2026-04-24");
    expect(display.textContent).toContain("Grand Medical Group");
    expect(toggle.disabled).toBe(true);

    unlock.checked = true;
    unlock.dispatchEvent(new Event("change", { bubbles: true }));
    toggle.click();

    expect(panel.classList.contains("d-none")).toBe(false);
    const optionButtons = Array.from(panel.querySelectorAll(".es-company-multi-select-option"));
    expect(optionButtons.length).toBe(3);

    optionButtons[2].click();
    const selectedValues = Array.from(document.getElementById("objective-applicable-companies").selectedOptions).map((opt) => opt.value);

    expect(selectedValues).toEqual(["comp-1", "comp-2", "comp-3"]);
    expect(display.textContent).toContain("3 selected");
  });
});
