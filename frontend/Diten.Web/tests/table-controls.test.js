const { loadScript } = require("./load-script");

function setupDom() {
  document.body.innerHTML = `<button id="cols-btn" type="button">Columns</button>`;
}

describe("enterprise table controls", () => {
  beforeEach(() => {
    setupDom();
    loadScript("wwwroot/assets/js/pages/enterprise-strategy/table-controls.js");
  });

  it("supports visibility, sorting, reorder, and page size persistence", () => {
    const onChange = vi.fn();
    const controls = window.enterpriseTableControls.create({
      pageKey: "test-page",
      storageKey: "testTablePrefs",
      columnsButtonId: "cols-btn",
      defaultPageSize: 50,
      columns: [
        { key: "id", label: "ID", required: true, defaultVisible: true },
        { key: "name", label: "Name", defaultVisible: true },
        { key: "status", label: "Status", defaultVisible: true },
        { key: "actions", label: "Actions", defaultVisible: true }
      ],
      onChange
    });

    expect(controls.getVisibleColumns().map((c) => c.key)).toEqual(["id", "name", "status", "actions"]);
    controls.cycleSort("status");
    expect(controls.state.sort).toEqual({ key: "status", dir: "asc" });
    controls.setPageSize(100);
    expect(controls.getPageSize()).toBe(100);

    const panel = document.getElementById("test-page-columns-panel");
    panel.querySelector('[data-toggle-col="status"]').click();
    expect(controls.getVisibleColumns().map((c) => c.key)).not.toContain("status");
    expect(controls.state.sort).toEqual({ key: "", dir: "" });

    panel.querySelector('[data-col-up="actions"]').click();
    expect(controls.state.order.indexOf("actions")).toBe(2);
    expect(onChange).toHaveBeenCalled();

    expect(controls.getPageSize()).toBe(100);
    expect(controls.state.order.indexOf("actions")).toBe(2);
  });

  it("reset restores defaults and clears filters", () => {
    const controls = window.enterpriseTableControls.create({
      pageKey: "test-page-2",
      storageKey: "testTablePrefs2",
      columnsButtonId: "cols-btn",
      defaultPageSize: 25,
      columns: [
        { key: "id", label: "ID", required: true, defaultVisible: true },
        { key: "name", label: "Name", defaultVisible: true },
        { key: "scope", label: "Scope", defaultVisible: false }
      ],
      onChange: vi.fn()
    });

    controls.setFilters({ search: "abc", scope: "global" });
    controls.setPageSize(100);
    controls.cycleSort("name");
    controls.reset();

    expect(controls.getFilters()).toEqual({});
    expect(controls.getPageSize()).toBe(25);
    expect(controls.state.sort).toEqual({ key: "", dir: "" });
    expect(controls.getVisibleColumns().map((c) => c.key)).toEqual(["id", "name"]);
  });
});
