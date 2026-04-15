const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <table id="initiatives-table"><thead><tr id="initiatives-header-row"></tr></thead><tbody></tbody></table>
    <div id="initiative-detail-card" class="d-none"></div>
    <div id="initiative-detail-content"></div>
    <button id="initiative-detail-link-btn"></button>
    <div id="initiative-link-warning"></div>
    <div id="initiative-ppm-error"></div>
    <div id="initiative-link-error"></div>
    <datalist id="ppm-objective-list"></datalist>
    <datalist id="link-initiative-list"></datalist>
    <div id="initiativePpmModal"></div>
    <div id="initiativeLinkModal"></div>
    <input id="initiative-search" />
    <select id="initiative-filter-owner"></select>
    <select id="initiative-filter-status"></select>
    <select id="initiative-filter-type"></select>
    <select id="initiative-filter-parent-goal"></select>
    <select id="initiative-filter-parent-objective"></select>
    <select id="initiative-filter-wave"></select>
    <select id="initiative-filter-priority"></select>
    <select id="initiative-filter-complexity"></select>
    <select id="initiative-filter-maturity"></select>
    <input id="initiative-filter-sponsoring-company" />
    <input id="initiative-filter-participating-company" />
    <select id="initiative-filter-class"></select>
    <input id="initiative-filter-scope" />
    <input id="initiative-link-objective" />
    <input id="initiative-link-goal" />
    <input id="initiative-link-weight" />
    <textarea id="initiative-link-notes"></textarea>
    <button id="initiative-apply-filters"></button>
    <button id="initiative-sync"></button>
    <button id="initiative-link-confirm"></button>
    <button id="initiative-link-unlink"></button>
  `;
}

describe("initiatives page", () => {
  beforeEach(() => {
    setupDom();
    global.bootstrap = { Modal: function ModalStub() { return { show() {}, hide() {} }; } };
    window.bootstrap = global.bootstrap;
    window.enterpriseTableControls = null;
    window.enterpriseTablePageUtils = null;
    window.enterpriseFilterDrawer = null;
    window.enterpriseWorkbookIo = null;
    window.enterpriseModalFormUtils = null;
    window.enterpriseRowActionsMenu = null;
    window.enterpriseWorkbookOptions = {};
    window.enterpriseStrategyUi = { getErrorMessage: (e, f) => f || String(e) };
    window.enterpriseStrategyPermissions = { can: () => true };
    window.initiativeStrategyApi = {
      list: vi.fn().mockResolvedValue({
        items: [
          {
            initiativeId: "init-001",
            initiativeName: "Initiative 1",
            sourceSystem: "PPM",
            strategyLinkStatus: "Linked",
            syncFreshness: "Fresh",
            warnings: [],
          },
        ],
      }),
      sync: vi.fn().mockResolvedValue({}),
      upsertLink: vi.fn().mockResolvedValue({}),
      unlink: vi.fn().mockResolvedValue({}),
    };
    window.strategyObjectivesApi = {
      list: vi.fn().mockResolvedValue({ items: [] }),
    };
    window.strategyGoalsApi = {
      list: vi.fn().mockResolvedValue({ items: [] }),
    };
  });

  it("renders initiative rows from list API", async () => {
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/initiatives.js");
    let rowText = "";
    for (let attempt = 0; attempt < 8; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      rowText = document.querySelector("#initiatives-table tbody tr")?.textContent || "";
      if (rowText.trim()) break;
    }
    expect(rowText).toContain("init-001");
    expect(rowText).toContain("Initiative 1");
  });

  it("renders empty state row when list request fails", async () => {
    window.initiativeStrategyApi.list = vi.fn().mockRejectedValue(new Error("down"));
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/initiatives.js");
    await new Promise((resolve) => setTimeout(resolve, 0));
    const text = document.querySelector("#initiatives-table tbody tr")?.textContent || "";
    expect(text).toContain("No initiatives found for the current filters.");
  });

  it("does not crash when permission helper denies access", async () => {
    window.enterpriseStrategyPermissions = { can: () => false };
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/initiatives.js");
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(document.getElementById("initiative-sync").hasAttribute("disabled")).toBe(false);
    expect(document.getElementById("initiative-link-confirm").hasAttribute("disabled")).toBe(false);
  });
});
