const fs = require("fs");
const path = require("path");
const vm = require("vm");
let dataTable;

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
      <table id="planning-cycles-table" class="planning-cycles-table"><tbody></tbody></table>

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
  const context = vm.createContext({
    window, document, navigator: window.navigator, location: window.location, console,
    setTimeout, clearTimeout, setInterval, clearInterval,
    getComputedStyle: window.getComputedStyle.bind(window)
  });
  for (const key of Object.getOwnPropertyNames(window)) {
    if (/^[A-Z]/.test(key) && typeof window[key] === "function") context[key] = window[key];
  }
  context.self = context;
  const run = (file) => vm.runInContext(fs.readFileSync(path.resolve(__dirname, "..", file), "utf8"), context, { filename: file });
  run("wwwroot/assets/vendor/libs/jquery/jquery.js");
  context.$ = context.jQuery = window.jQuery;
  run("wwwroot/assets/vendor/libs/datatables-bs5/datatables-bootstrap5.js");
  context.DataTable = context.DataTable || context.jQuery.fn.dataTable;
  context.DataTable.ext.errMode = "throw";
  global.bootstrap = {
    Modal: function () { return { show() { }, hide() { } }; }
  };
  context.bootstrap = global.bootstrap;
  context.DataTable.Responsive.bootstrap(global.bootstrap);
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
    getUsersByTenantId: vi.fn().mockResolvedValue([]),
    lookups: vi.fn().mockResolvedValue({
      planningLifecycleStatuses: ["Draft", "Active", "Archived"],
      planningCycleTypes: ["Annual Plan"],
      ownerReferences: [{ ownerId: "owner-1", displayName: "Beste Pullukcu" }]
    })
  };
  window.strategyPlanningApi = {
    getAllPositions: vi.fn().mockResolvedValue([]),
    listCycles: vi.fn().mockResolvedValue([
      {
        id: "pc-draft",
        code: "PC-ANN-2026-0001",
        name: "Draft Cycle",
        planningCycleType: "Annual Plan",
        ownerId: "owner-1",
        ownerCompanyId: "comp-1",
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
        ownerCompanyId: "comp-1",
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
        ownerCompanyId: "comp-1",
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

  run("wwwroot/assets/js/pages/enterprise-strategy/planning-cycles.js");
  await window.planningCyclesApp.initPage();
  await vi.waitFor(() => expect(document.querySelectorAll("#planning-cycles-table tbody tr")).toHaveLength(3));
  dataTable = context.jQuery("#planning-cycles-table").DataTable();
}

describe("planning cycles register actions", () => {
  beforeEach(async () => {
    await boot();
  });
  afterEach(() => dataTable?.destroy());

  it("renders DataTable view links and lifecycle-specific action items", () => {
    const rows = Array.from(document.querySelectorAll("#planning-cycles-table tbody tr"));
    expect(rows).toHaveLength(3);

    const draftRow = rows.find((row) => row.textContent.includes("Draft Cycle"));
    const activeRow = rows.find((row) => row.textContent.includes("Active Cycle"));
    const archivedRow = rows.find((row) => row.textContent.includes("Archived Cycle"));
    const actionLabels = (row) => Array.from(row.querySelectorAll(".dropdown-item")).map((item) => item.textContent.trim());

    expect(draftRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(activeRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();
    expect(archivedRow.querySelector(".bx-dots-vertical-rounded")).not.toBeNull();

    for (const [row, id] of [[draftRow, "pc-draft"], [activeRow, "pc-active"], [archivedRow, "pc-archived"]]) {
      expect(row.querySelector(".bx-show").closest("a").getAttribute("href")).toBe(
        `/management-governance/enterprise-strategy-business-performance/planning/cycles/${id}`);
    }
    expect(actionLabels(draftRow)).toEqual(["Edit", "Activate", "Archive / Delete"]);
    expect(actionLabels(activeRow)).toEqual(["Archive / Delete"]);
    expect(actionLabels(activeRow)).not.toContain("Edit");
    // Re-archiving is currently accepted as a same-state transition by PlanningCycleService.
    expect(actionLabels(archivedRow)).toEqual(["Archive / Delete"]);
  });
});
