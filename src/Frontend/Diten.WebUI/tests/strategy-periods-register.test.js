const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div class="strategy-periods-page">
      <div id="strategy-period-total"></div>
      <div id="strategy-period-active"></div>
      <div id="strategy-period-draft"></div>
      <div id="strategy-period-in-use"></div>
      <input id="strategy-period-search" />
      <select id="strategy-period-status-filter"><option value="">All</option></select>
      <select id="strategy-period-cycle-filter"><option value="">All</option></select>
      <select id="strategy-period-company-filter"><option value="">All</option></select>
      <select id="strategy-period-review-filter"><option value="">All</option></select>
      <button id="strategy-period-reset" type="button"></button>
      <button id="strategy-period-refresh" type="button"></button>
      <table id="strategy-periods-table"><tbody></tbody></table>

      <div id="strategy-period-readiness-indicator"></div>
      <div id="strategy-period-readiness-text"></div>
      <ul id="strategy-period-readiness-missing"></ul>
      <ul id="strategy-period-readiness-blockers"></ul>
      <div id="strategy-period-sec-identity-state"></div>
      <div id="strategy-period-sec-scope-state"></div>
      <div id="strategy-period-sec-timing-state"></div>
      <input id="strategy-period-scope-summary" />
      <div id="strategy-period-parent-horizon-hint"></div>

      <div id="strategyPeriodEditorModal"></div>
      <div id="strategy-period-form-error"></div>
      <input id="strategy-period-name" />
      <input id="strategy-period-code" />
      <select id="strategy-period-cycle"><option value="">Select</option></select>
      <select id="strategy-period-owner"><option value="">Select</option></select>
      <select id="strategy-period-company"><option value="">Select</option></select>
      <select id="strategy-period-bu"><option value="">Select</option></select>
      <select id="strategy-period-region"><option value="">Select</option></select>
      <input id="strategy-period-start" />
      <input id="strategy-period-end" />
      <select id="strategy-period-review"><option value="">Select</option></select>
      <input id="strategy-period-default" type="checkbox" />
      <textarea id="strategy-period-notes"></textarea>
      <button id="strategy-period-save" type="button"></button>
    </div>
  `;
}

async function boot() {
  setupDom();
  global.bootstrap = {
    Modal: function () { return { show() { }, hide() { } }; }
  };
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    ensurePositionsLoaded: vi.fn().mockResolvedValue(undefined),
    fillSelect: (el, items, options = {}) => {
      if (!el) return;
      const current = el.value;
      el.innerHTML = `<option value="">${options.placeholder || "Select"}</option>`;
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
      if (options.keepCurrent && current) el.value = current;
    },
    companyOptions: () => [{ value: "comp-001", label: "Grand Medical Group" }],
    companyDisplayName: (id) => id,
    userDisplayName: (id) => ({ "owner-1": "Beste Pullukcu" }[id] || id)
  };
  window.strategyCompaniesApi = {
    list: vi.fn().mockResolvedValue({ items: [{ companyId: "comp-001", companyName: "Grand Medical Group" }] })
  };
  window.strategyEnterpriseMetaApi = {
    lookups: vi.fn().mockResolvedValue({
      strategyPeriodLifecycleStatuses: ["Draft", "Active", "Archived"],
      reviewCadences: ["Monthly", "Annual"],
      businessUnits: ["Corporate", "Operations"],
      regions: ["Global"]
    })
  };
  window.strategyGoalsApi = {
    list: vi.fn().mockResolvedValue({
      items: [{ id: "goal-1", strategyPeriodId: "sp-active" }]
    })
  };
  window.strategyObjectivesApi = {
    list: vi.fn().mockResolvedValue({
      items: [{ id: "obj-1", parentGoalId: "goal-1" }]
    })
  };
  window.strategyPlanningApi = {
    listCycles: vi.fn().mockResolvedValue([
      {
        id: "cycle-1",
        name: "Extra new 2026",
        ownerId: "owner-1",
        effectiveFrom: "2026-01-01",
        effectiveTo: "2026-12-31"
      }
    ]),
    listStrategyPeriods: vi.fn().mockResolvedValue([
      {
        id: "sp-draft",
        planningCycleId: "cycle-1",
        name: "Draft Period",
        ownerEmployeeId: "owner-1",
        companyId: "comp-001",
        businessUnitId: "Operations",
        regionId: "Global",
        startDate: "2026-03-01",
        endDate: "2026-04-01",
        reviewCadence: "Monthly",
        status: "Draft"
      },
      {
        id: "sp-active",
        planningCycleId: "cycle-1",
        name: "Active Period",
        ownerEmployeeId: "owner-1",
        companyId: "comp-001",
        businessUnitId: "Corporate",
        regionId: "Global",
        startDate: "2026-01-01",
        endDate: "2026-12-31",
        reviewCadence: "Annual",
        status: "Active"
      },
      {
        id: "sp-archived",
        planningCycleId: "cycle-1",
        name: "Archived Period",
        ownerEmployeeId: "owner-1",
        companyId: "comp-001",
        businessUnitId: "Corporate",
        regionId: "Global",
        startDate: "2025-01-01",
        endDate: "2025-12-31",
        reviewCadence: "Annual",
        status: "Archived"
      }
    ]),
    getStrategyPeriod: vi.fn(),
    updateStrategyPeriod: vi.fn(),
    createStrategyPeriod: vi.fn(),
    activatePeriod: vi.fn(),
    archivePeriod: vi.fn()
  };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/row-actions-menu.js");
  loadScript("wwwroot/assets/js/pages/enterprise-strategy/strategy-periods.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("strategy periods register actions", () => {
  beforeEach(async () => {
    await boot();
  });

  it("renders goal-style 3-dot action menus with the correct items for each row status", () => {
    const rows = Array.from(document.querySelectorAll("#strategy-periods-table tbody tr"));
    expect(rows).toHaveLength(3);

    const draftRow = rows.find((row) => row.textContent.includes("Draft Period"));
    const activeRow = rows.find((row) => row.textContent.includes("Active Period"));
    const archivedRow = rows.find((row) => row.textContent.includes("Archived Period"));
    const actionLabels = (row) => Array.from(row.querySelectorAll(".strategy-period-col-actions .dropdown-item")).map((item) => item.textContent.trim());

    expect(draftRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(activeRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(archivedRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();

    expect(actionLabels(draftRow)).toEqual(expect.arrayContaining(["View", "Edit", "Activate", "Archive / Delete"]));

    expect(actionLabels(activeRow)).toEqual(expect.arrayContaining(["View", "Archive / Delete"]));
    expect(actionLabels(activeRow)).not.toContain("Edit");
    expect(actionLabels(activeRow)).not.toContain("Activate");

    expect(actionLabels(archivedRow)).toEqual(["View"]);
  });
});
