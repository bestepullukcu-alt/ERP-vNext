const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div class="planning-cycles-page">
      <div id="planning-cycle-total"></div>
      <div id="planning-cycle-active"></div>
      <div id="planning-cycle-draft"></div>
      <div id="planning-cycle-archived"></div>
      <input id="planning-cycle-search" />
      <select id="planning-cycle-status-filter"><option value="">All</option></select>
      <select id="planning-cycle-type-filter"><option value="">All</option></select>
      <select id="planning-cycle-owner-filter"><option value="">All</option></select>
      <button id="planning-cycle-reset" type="button"></button>
      <button id="planning-cycle-refresh" type="button"></button>
      <table id="planning-cycles-table"><tbody></tbody></table>

      <div id="planning-cycle-readiness-indicator"></div>
      <div id="planning-cycle-readiness-text"></div>
      <ul id="planning-cycle-readiness-missing"></ul>
      <ul id="planning-cycle-readiness-blockers"></ul>
      <div id="planning-cycle-sec-identity-state"></div>
      <div id="planning-cycle-sec-horizon-state"></div>

      <div id="planningCycleEditorModal"></div>
      <div id="planning-cycle-form-error"></div>
      <div id="planning-cycle-modal-title"></div>
      <input id="planning-cycle-name" />
      <input id="planning-cycle-code" />
      <select id="planning-cycle-type"><option value="">Select</option></select>
      <select id="planning-cycle-owner"><option value="">Select</option></select>
      <input id="planning-cycle-effective-from" />
      <input id="planning-cycle-effective-to" />
      <button id="planning-cycle-save" type="button"></button>
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
    userDisplayName: (id) => ({ "owner-1": "Beste Pullukcu" }[id] || id)
  };
  window.strategyEnterpriseMetaApi = {
    lookups: vi.fn().mockResolvedValue({
      planningLifecycleStatuses: ["Draft", "Active", "Archived"],
      planningCycleTypes: ["Annual Plan"],
      ownerReferences: [{ ownerId: "owner-1", displayName: "Beste Pullukcu" }]
    })
  };
  window.strategyPlanningApi = {
    listCycles: vi.fn().mockResolvedValue([
      {
        id: "pc-draft",
        code: "PC-ANN-2026-0001",
        name: "Draft Cycle",
        planningCycleType: "Annual Plan",
        ownerId: "owner-1",
        status: "Draft",
        effectiveFrom: "2026-01-01",
        effectiveTo: "2026-12-31",
        updatedOn: "2026-03-28"
      },
      {
        id: "pc-active",
        code: "PC-ANN-2025-0001",
        name: "Active Cycle",
        planningCycleType: "Annual Plan",
        ownerId: "owner-1",
        status: "Active",
        effectiveFrom: "2025-01-01",
        effectiveTo: "2025-12-31",
        updatedOn: "2025-12-31"
      },
      {
        id: "pc-archived",
        code: "PC-ANN-2024-0001",
        name: "Archived Cycle",
        planningCycleType: "Annual Plan",
        ownerId: "owner-1",
        status: "Archived",
        effectiveFrom: "2024-01-01",
        effectiveTo: "2024-12-31",
        updatedOn: "2024-12-31"
      }
    ]),
    getCycle: vi.fn(),
    updateCycle: vi.fn(),
    createCycle: vi.fn(),
    activateCycle: vi.fn(),
    archiveCycle: vi.fn()
  };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/row-actions-menu.js");
  loadScript("wwwroot/assets/js/pages/enterprise-strategy/planning-cycles.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe("planning cycles register actions", () => {
  beforeEach(async () => {
    await boot();
  });

  it("renders goal-style 3-dot action menus with lifecycle-specific items", () => {
    const rows = Array.from(document.querySelectorAll("#planning-cycles-table tbody tr"));
    expect(rows).toHaveLength(3);

    const draftRow = rows.find((row) => row.textContent.includes("Draft Cycle"));
    const activeRow = rows.find((row) => row.textContent.includes("Active Cycle"));
    const archivedRow = rows.find((row) => row.textContent.includes("Archived Cycle"));
    const actionLabels = (row) => Array.from(row.querySelectorAll(".dropdown-item")).map((item) => item.textContent.trim());

    expect(draftRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(activeRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(archivedRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();

    expect(actionLabels(draftRow)).toEqual(expect.arrayContaining(["View", "Edit", "Activate", "Archive / Delete"]));
    expect(actionLabels(activeRow)).toEqual(expect.arrayContaining(["View", "Archive / Delete"]));
    expect(actionLabels(activeRow)).not.toContain("Edit");
    expect(actionLabels(archivedRow)).toEqual(["View"]);
  });
});
