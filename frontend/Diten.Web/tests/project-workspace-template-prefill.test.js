const { loadScript } = require("./load-script");

function buildDom() {
  document.body.innerHTML = `
    <div id="project-create-workspace" data-prefill-parent-initiative-id="">
      <div id="project-wizard-error"></div>
      <div id="project-wizard-steps">
        <button type="button" data-step="1"></button>
        <button type="button" data-step="2"></button>
        <button type="button" data-step="3"></button>
        <button type="button" data-step="4"></button>
        <button type="button" data-step="5"></button>
        <button type="button" data-step="6"></button>
      </div>
      <section class="project-step-pane" data-step="1"></section>
      <section class="project-step-pane d-none" data-step="2"></section>
      <section class="project-step-pane d-none" data-step="3"></section>
      <section class="project-step-pane d-none" data-step="4"></section>
      <section class="project-step-pane d-none" data-step="5"></section>
      <section class="project-step-pane d-none" data-step="6"></section>
      <button id="project-step-back" type="button"></button>
      <button id="project-step-next" type="button"></button>
      <button id="project-save-draft" type="button"></button>
      <button id="project-create-submit" type="button"></button>
      <button id="project-create-open" type="button"></button>
      <div id="project-template-host"></div>
      <div id="project-template-filter-note"></div>
      <div id="project-template-preview"></div>
      <ul id="project-template-prefill-list"></ul>
      <button id="project-template-browse" type="button"></button>
      <button id="project-template-clear" type="button"></button>
      <button id="project-template-reapply" type="button"></button>
      <input id="project-template-picker-search" />
      <select id="project-template-picker-type"><option value="">All types</option></select>
      <select id="project-template-picker-entity-scope"><option value="">All entity scopes</option></select>
      <div id="project-template-picker-helper"></div>
      <table><tbody id="project-template-picker-tbody"></tbody></table>
      <div id="project-template-picker-current-initiative"></div>
      <div id="project-template-picker-current-objective"></div>
      <div id="project-template-picker-current-goal"></div>
      <div id="project-template-picker-current-type"></div>
      <div id="project-template-picker-current-scope"></div>
      <div id="project-template-picker-context-warning"></div>
      <div id="project-budget-governance-banner"></div>
      <div id="project-budget-required-yes-group"></div>
      <div id="project-budget-required-no-group"></div>
      <div id="project-review-identity"></div>
      <div id="project-review-anchor"></div>
      <div id="project-review-ownership"></div>
      <div id="project-review-planning"></div>
      <div id="project-review-controls"></div>
      <div id="project-review-budget"></div>
      <ul id="project-review-blockers"></ul>
      <ul id="project-review-warnings"></ul>
      <div id="project-source-summary"></div>
      <div id="project-source-summary-name"></div>
      <div id="project-source-summary-note"></div>
      <select id="project-parent-initiative"><option value="">Select Parent Initiative</option></select>
      <input id="project-parent-objective" />
      <input id="project-parent-goal" />
      <input id="project-parent-type" />
      <input id="project-entity-scope" />
      <select id="project-creation-mode">
        <option value="Blank">Blank</option>
        <option value="Template">From Project Template</option>
      </select>
      <select id="project-template-select"><option value="">Select compatible template</option></select>
      <label for="project-name">Project Name</label>
      <input id="project-name" />
      <label for="project-description">Project Description</label>
      <textarea id="project-description"></textarea>
      <select id="project-owner-pm"><option value="">Select owner / PM</option></select>
      <select id="project-executive-sponsor"><option value="">Select executive sponsor</option></select>
      <select id="project-business-owner"><option value="">Select business owner</option></select>
      <select id="project-delivery-company"><option value="">Select delivery company</option></select>
      <select id="project-funding-company"><option value="">Select funding / owning company</option></select>
      <input id="project-owning-function" />
      <input id="project-delivery-partner" />
      <textarea id="project-scope-summary"></textarea>
      <textarea id="project-out-of-scope"></textarea>
      <select id="project-status"><option value="Draft">Draft</option></select>
      <select id="project-phase"><option value="">Select stage / phase</option></select>
      <select id="project-delivery-type"><option value="">Select delivery type</option></select>
      <select id="project-delivery-methodology"><option value="">Select methodology</option></select>
      <select id="project-priority"><option value="">Select priority</option></select>
      <select id="project-complexity"><option value="">Select complexity / size</option></select>
      <input id="project-start-date" type="date" />
      <input id="project-end-date" type="date" />
      <input id="project-go-live" type="date" />
      <select id="project-reporting-cadence"><option value="">Select cadence</option></select>
      <input id="project-success-metric" />
      <input id="project-baseline" />
      <input id="project-target" />
      <select id="project-readiness-status"><option value="">Select readiness</option></select>
      <select id="project-risk-rating"><option value="">Select risk</option></select>
      <select id="project-health"><option value="">Select health</option></select>
      <textarea id="project-compliance-impact"></textarea>
      <select id="project-dependency-flag"><option value="false">No</option><option value="true">Yes</option></select>
      <select id="project-evidence-required"><option value="false">No</option><option value="true">Yes</option></select>
      <select id="project-budget-required"><option value="">Select</option><option value="true">Yes</option><option value="false">No</option></select>
      <input id="project-budget-amount" />
      <select id="project-currency"><option value="">Select currency</option></select>
      <select id="project-budget-type"><option value="">Select budget type</option></select>
      <select id="project-budget-basis"><option value="">Select budget basis</option></select>
      <input id="project-funding-source" />
      <input id="project-cost-center" />
      <select id="project-budget-owner"><option value="">Select budget owner</option></select>
      <input id="project-approval-route" />
      <textarea id="project-financial-notes"></textarea>
      <textarea id="project-no-budget-reason"></textarea>
      <input id="project-id" />
    </div>
    <div id="projectTemplatePickerModal"></div>
  `;
}

async function boot() {
  buildDom();
  const modalSpy = { show: vi.fn(), hide: vi.fn() };
  global.bootstrap = {
    Modal: function () { return modalSpy; }
  };
  window.enterpriseStrategyUi = {
    notify: vi.fn(),
    getErrorMessage: (err, fallback) => err?.message || fallback
  };
  window.enterpriseWorkbookOptions = {
    ensureLookupsLoaded: vi.fn().mockResolvedValue(undefined),
    ensureUsersLoaded: vi.fn().mockResolvedValue(undefined),
    ensureCompaniesLoaded: vi.fn().mockResolvedValue(undefined),
    userOptions: () => [],
    companyOptions: () => []
  };
  window.initiativeStrategyApi = {
    list: vi.fn().mockResolvedValue({
      items: [
        { initiativeId: "archive", initiativeName: "archive" },
        { initiativeId: "status", initiativeName: "status" },
        { initiativeId: "initiatives", initiativeName: "initiatives" },
        {
          initiativeId: "INIT-1",
          initiativeName: "Tooling enablement for operational excellence score",
          parentObjectiveName: "Improve continuous-improvement index",
          parentGoalName: "Operational Excellence",
          type: "Operations",
          normalizedType: "Operations",
          entityScope: "Plant / Function / Process",
          startDate: "2026-03-24",
          endDate: "2026-04-24"
        }
      ]
    })
  };
  window.projectStrategyApi = {
    compatibleTemplates: vi.fn().mockResolvedValue([
      {
        templateId: "PR-OP-05-03-06-10",
        name: "Adoption impact review and corrective actions for batch acceptance rate",
        description: "Review adoption metrics, usage patterns, and operational feedback; deploy corrective actions where behavioral uptake is limiting results on batch acceptance rate.",
        parentType: "Operations",
        entityScope: "Plant / Function / Process",
        deliveryType: "Implementation",
        phase: "Stabilize"
      }
    ]),
    create: vi.fn(),
    update: vi.fn()
  };

  loadScript("wwwroot/assets/js/pages/enterprise-strategy/project-workspace.js");
  await new Promise((resolve) => setTimeout(resolve, 0));
  return { modalSpy };
}

describe("project workspace template prefill", () => {
  it("filters out malformed parent initiative lookup rows", async () => {
    await boot();

    const select = document.getElementById("project-parent-initiative");
    const labels = Array.from(select.options).map((opt) => opt.textContent.trim());
    const values = Array.from(select.options).map((opt) => opt.value);
    const validLabel = labels.find((label) => label.includes("Tooling enablement for operational excellence score"));

    expect(validLabel).toContain("Tooling enablement for operational excellence score");
    expect(validLabel).toContain("Operations");
    expect(validLabel).toContain("Plant / Function / Process");
    expect(values).toContain("INIT-1");
    expect(labels).not.toContain("archive");
    expect(labels).not.toContain("status");
    expect(labels).not.toContain("initiatives");
    expect(values).not.toContain("archive");
    expect(values).not.toContain("status");
    expect(values).not.toContain("initiatives");
  });

  it("fills project name and description from the selected project template", async () => {
    await boot();

    const parentInitiative = document.getElementById("project-parent-initiative");
    parentInitiative.value = "INIT-1";
    parentInitiative.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const creationMode = document.getElementById("project-creation-mode");
    creationMode.value = "Template";
    creationMode.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const templateSelect = document.getElementById("project-template-select");
    templateSelect.value = "PR-OP-05-03-06-10";
    templateSelect.dispatchEvent(new Event("change", { bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(document.getElementById("project-name").value).toBe("Adoption impact review and corrective actions for batch acceptance rate");
    expect(document.getElementById("project-description").value).toBe("Review adoption metrics, usage patterns, and operational feedback; deploy corrective actions where behavioral uptake is limiting results on batch acceptance rate.");
  });
});
