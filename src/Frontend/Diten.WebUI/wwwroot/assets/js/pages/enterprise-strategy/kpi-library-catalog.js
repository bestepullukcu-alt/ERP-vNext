(function (window, document) {
  "use strict";

  const tbody = document.querySelector("#kpi-lib-table tbody");
  const filters = {
    search: document.getElementById("kpi-lib-search"),
    category: document.getElementById("kpi-lib-category"),
    strategicPerspective: document.getElementById("kpi-lib-perspective"),
    type: document.getElementById("kpi-lib-type"),
    objectLevel: document.getElementById("kpi-lib-object-level"),
    reportingFrequency: document.getElementById("kpi-lib-frequency"),
    status: document.getElementById("kpi-lib-status"),
    thresholdModel: document.getElementById("kpi-lib-threshold"),
    polarity: document.getElementById("kpi-lib-polarity"),
    apply: document.getElementById("kpi-lib-apply")
  };

  let rows = [];

  function fillSelect(el, values, placeholder) {
    if (!el) return;
    const unique = [...new Set((values || []).filter(Boolean))];
    el.innerHTML = `<option value="">${placeholder}</option>${unique.map((x) => `<option>${x}</option>`).join("")}`;
  }

  function apply() {
    const q = String(filters.search?.value || "").trim().toLowerCase();
    const f = {
      category: filters.category?.value || "",
      strategicPerspective: filters.strategicPerspective?.value || "",
      type: filters.type?.value || "",
      objectLevel: filters.objectLevel?.value || "",
      reportingFrequency: filters.reportingFrequency?.value || "",
      status: filters.status?.value || "",
      thresholdModel: filters.thresholdModel?.value || "",
      polarity: filters.polarity?.value || ""
    };
    const filtered = rows.filter((x) => {
      if (q && !`${x.templateCode} ${x.name} ${x.tags}`.toLowerCase().includes(q)) return false;
      if (f.category && x.category !== f.category) return false;
      if (f.strategicPerspective && x.strategicPerspective !== f.strategicPerspective) return false;
      if (f.type && x.type !== f.type) return false;
      if (f.objectLevel && x.objectLevel !== f.objectLevel) return false;
      if (f.reportingFrequency && x.reportingFrequency !== f.reportingFrequency) return false;
      if (f.status && x.status !== f.status) return false;
      if (f.thresholdModel && x.thresholdModelCode !== f.thresholdModel) return false;
      if (f.polarity && x.polarity !== f.polarity) return false;
      return true;
    });
    render(filtered);
  }

  function render(list) {
    document.getElementById("kpi-lib-total").textContent = String(list.length);
    document.getElementById("kpi-lib-published").textContent = String(list.filter((x) => x.status === "Published").length);
    document.getElementById("kpi-lib-review").textContent = String(list.filter((x) => x.status === "In Review").length);
    document.getElementById("kpi-lib-usage").textContent = String(list.reduce((sum, x) => sum + Number(x.usageCount || 0), 0));
    tbody.innerHTML = list.map((x) => `
      <tr>
        <td>${x.templateCode || ""}</td><td>${x.name || ""}</td><td>${x.category || ""}</td><td>${x.strategicPerspective || ""}</td>
        <td>${x.type || ""}</td><td>${x.objectLevel || ""}</td><td>${x.polarity || ""}</td><td>${x.unitOfMeasure || ""}</td>
        <td>${x.aggregationMethod || ""}</td><td>${x.reportingFrequency || ""}</td><td>${x.thresholdModelCode || ""}</td>
        <td>${x.defaultOwnerRole || ""}</td><td>${x.versionLabel || ""}</td><td>${x.status || ""}</td><td>${x.usageCount || 0}</td>
        <td>${x.publishDate ? new Date(x.publishDate).toISOString().slice(0, 10) : ""}</td>
        <td class="text-end">
          ${(window.enterpriseRowActionsMenu?.render?.(x.id, [
            { action: "open-template", label: "Open Template", href: `/management-governance/enterprise-strategy-business-performance/kpis/library/templates/${encodeURIComponent(x.id)}` },
            { action: "instantiate", label: "Instantiate to KPI Catalog" },
            { action: "clone", label: "Clone Template" },
            { action: "view-versions", label: "View Versions", href: `/management-governance/enterprise-strategy-business-performance/kpis/governance` },
            { divider: true },
            { action: "submit-review", label: "Submit for Review" },
            { action: "publish", label: "Publish" },
            { action: "retire", label: "Retire" }
          ]) || "")}
        </td>
      </tr>`).join("");
  }

  async function load() {
    const data = await window.kpiLibraryApi.templates({ page: 1, pageSize: 5000 });
    rows = data?.items || [];
    fillSelect(filters.category, rows.map((x) => x.category), "KPI Category");
    fillSelect(filters.strategicPerspective, rows.map((x) => x.strategicPerspective), "Strategic Perspective");
    fillSelect(filters.type, rows.map((x) => x.type), "KPI Type");
    fillSelect(filters.objectLevel, rows.map((x) => x.objectLevel), "Object Level");
    fillSelect(filters.reportingFrequency, rows.map((x) => x.reportingFrequency), "Reporting Frequency");
    fillSelect(filters.status, rows.map((x) => x.status), "Status");
    fillSelect(filters.thresholdModel, rows.map((x) => x.thresholdModelCode), "Threshold Model");
    fillSelect(filters.polarity, rows.map((x) => x.polarity), "Polarity");
    apply();
  }

  filters.apply?.addEventListener("click", apply);
  tbody?.addEventListener("click", async (event) => {
    const btn = event.target.closest(".es-row-action-item");
    if (!btn) return;
    const id = btn.dataset.rowId;
    const action = btn.dataset.action || "";
    if (!action) return;
    try {
      if (action === "instantiate") await window.strategyKpisApi.instantiateFromLibrary(id, false);
      else if (action === "clone") await window.kpiLibraryApi.cloneTemplate(id);
      else if (action === "submit-review" || action === "publish" || action === "retire") await window.kpiLibraryApi.lifecycle(id, action);
      await load();
    } catch (err) {
      window.enterpriseStrategyUi?.notify?.(window.enterpriseStrategyUi?.getErrorMessage?.(err, "Operation failed") || "Operation failed", "error");
    }
  });

  load().catch((err) => {
    tbody.innerHTML = `<tr><td colspan="17" class="text-danger">${window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load KPI library") || "Unable to load KPI library"}</td></tr>`;
  });
})(window, document);
