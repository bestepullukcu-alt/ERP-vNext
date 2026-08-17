const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `
    <div id="toolbar"></div>
    <div class="card">
      <table id="sample-table"></table>
    </div>`;
}

describe("enterprise table page utils", () => {
  beforeEach(() => {
    setupDom();
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/table-page-utils.js");
  });

  it("renders active filter summary chips", () => {
    const host = window.enterpriseTablePageUtils.ensureFilterSummaryHost(document.getElementById("toolbar"), "sample");
    window.enterpriseTablePageUtils.renderFilterSummary(host, { search: "revenue", owner: "", missingPlan: true });
    expect(host.textContent).toContain("search: revenue");
    expect(host.textContent).toContain("missingPlan: true");
  });

  it("paginates rows with page controls", () => {
    const controls = { getPageSize: () => 15, setPageSize: vi.fn() };
    const pager = window.enterpriseTablePageUtils.createPager({
      pageKey: "sampleTable",
      tableEl: document.getElementById("sample-table"),
      tableControls: controls,
      onChange: vi.fn()
    });
    const rows = Array.from({ length: 40 }, (_, i) => ({ id: i + 1 }));
    const page1 = pager.paginate(rows);
    expect(page1).toHaveLength(15);
    expect(document.getElementById("sampleTable-pager-count").textContent).toContain("40 filtered rows");
  });
});
