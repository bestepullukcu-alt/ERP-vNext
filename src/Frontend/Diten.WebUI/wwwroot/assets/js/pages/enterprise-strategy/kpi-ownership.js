(function (window, document) {
  "use strict";

  const workbook = window.enterpriseWorkbookOptions || {};
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();
  const tableBody = document.getElementById("kpi-ownership-table");
  const filters = {
    owner: document.getElementById("kpi-ownership-owner"),
    missing: document.getElementById("kpi-ownership-missing"),
    category: document.getElementById("kpi-ownership-category"),
    status: document.getElementById("kpi-ownership-status"),
    company: document.getElementById("kpi-ownership-company"),
    apply: document.getElementById("kpi-ownership-apply")
  };

  let ownershipRows = [];
  let kpiRows = [];

  function unique(values) {
    return [...new Set((values || []).filter(Boolean).map((x) => String(x).trim()))];
  }

  function applyFilters() {
    const owner = String(filters.owner?.value || "");
    const missing = String(filters.missing?.value || "");
    const category = String(filters.category?.value || "");
    const status = String(filters.status?.value || "");
    const company = String(filters.company?.value || "");

    const categoryByKpiId = new Map(kpiRows.map((x) => [x.id, x.category]));
    return ownershipRows.filter((row) => {
      if (owner && resolveUserId(row.owner) !== owner) return false;
      if (missing === "yes" && String(row.owner || "").trim()) return false;
      if (missing === "no" && !String(row.owner || "").trim()) return false;
      if (status && row.status !== status) return false;
      if (category && categoryByKpiId.get(row.kpiId) !== category) return false;
      if (company && !String(row.companyScope || "").includes(company)) return false;
      return true;
    });
  }

  function updateKpis(rows) {
    document.getElementById("own-kpi-total").textContent = String(rows.length);
    document.getElementById("own-kpi-missing").textContent = String(rows.filter((x) => !String(x.owner || "").trim()).length);
    const duplicates = rows.reduce((acc, row) => {
      const key = `${resolveUserId(row.owner) || ""}::${row.companyScope || ""}`;
      acc[key] = (acc[key] || 0) + 1;
      return acc;
    }, {});
    document.getElementById("own-kpi-duplicate").textContent = String(Object.values(duplicates).filter((n) => n > 1).length);
    document.getElementById("own-kpi-risk").textContent = String(rows.filter((x) => x.status !== "Active" || !x.owner).length);
  }

  function render(rows) {
    tableBody.innerHTML = rows.map((row) => {
      const actions = window.enterpriseRowActionsMenu?.render?.(row.kpiId, [
        { action: "view", label: "View", href: `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(row.kpiId)}` },
        { action: "edit", label: "Edit", href: `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(row.kpiId)}/edit` }
      ]) || "";
      return `<tr>
        <td>${row.kpiName || row.kpiId}</td>
        <td>${resolveUserName(row.owner) || "-"}</td>
        <td>${resolveUserName(row.backupOwner) || "-"}</td>
        <td>${row.reportingFrequency || "-"}</td>
        <td>${row.aggregationMethod || "-"}</td>
        <td>${row.companyScope || "-"}</td>
        <td>${row.usedByCount || 0}</td>
        <td><span class="badge bg-label-${row.status === "Active" ? "success" : "secondary"}">${row.status || "-"}</span></td>
        <td class="text-end es-row-actions-col">${actions}</td>
      </tr>`;
    }).join("");
    updateKpis(rows);
  }

  async function load() {
    await workbook.ensureUsersLoaded?.();
    const [ownership, catalog] = await Promise.all([
      window.strategyKpisApi.ownership(),
      window.strategyKpisApi.list()
    ]);
    ownershipRows = Array.isArray(ownership) ? ownership : (ownership?.items || []);
    kpiRows = Array.isArray(catalog?.items) ? catalog.items : [];
    workbook.fillSelect?.(filters.owner, workbook.userOptions?.() || [], { placeholder: "Owner" });
    workbook.fillSelect?.(filters.category, unique(kpiRows.map((x) => x.category)), { placeholder: "KPI Category" });
    workbook.fillSelect?.(filters.status, unique(ownershipRows.map((x) => x.status)), { placeholder: "Status" });
    workbook.fillSelect?.(filters.company, workbook.companyOptions?.() || [], { placeholder: "Company" });
    render(applyFilters());
  }

  filters.apply?.addEventListener("click", () => render(applyFilters()));
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load metric ownership.") || "Unable to load metric ownership.";
    tableBody.innerHTML = `<tr><td colspan="9" class="text-danger">${message}</td></tr>`;
  });
})(window, document);
