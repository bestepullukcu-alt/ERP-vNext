(function (window, document) {
  "use strict";

  const bodyEl = document.getElementById("gap-table-body");
  const searchEl = document.getElementById("gap-search");
  const typeEl = document.getElementById("gap-type");
  const applyBtn = document.getElementById("gap-apply");

  const kpi = {
    total: document.getElementById("gap-kpi-total"),
    missingInitiative: document.getElementById("gap-kpi-missing-initiative"),
    missingProject: document.getElementById("gap-kpi-missing-project"),
    missingTarget: document.getElementById("gap-kpi-missing-target"),
    missingPlan: document.getElementById("gap-kpi-missing-plan"),
    invalidAnchor: document.getElementById("gap-kpi-invalid-anchor")
  };

  const YEARS = Array.from({ length: 20 }, (_, i) => 2027 + i);
  const yearRegex = /^\d{4}$/;
  let allGaps = [];

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function toGapRows(connections) {
    const rows = [];
    (connections || []).forEach((edge) => {
      const meta = JSON.parse(edge.metricBindingsJson || "{}");
      const annual = YEARS.map((y) => meta[String(y)]).filter((v) => String(v || "").trim() !== "");
      const row = {
        connectionId: edge.id,
        goalId: meta.goalId || "",
        goal: meta.goal || "",
        objective: meta.objective || "",
        initiativeId: meta.initiativeId || "",
        initiative: meta.initiative || "",
        projectId: meta.projectId || "",
        project: meta.project || "",
        baselineYear: String(meta.baselineYear || ""),
        targetYear: String(meta.targetYear || ""),
        targetValue: String(meta.targetValue || ""),
        companyScopeMode: String(edge.companyScopeMode || meta.companyScopeMode || "Derived"),
        companyId: String(edge.companyId || meta.companyId || ""),
        aggregationMethod: String(meta.aggregationMethod || ""),
        objectiveMetric: String(meta.objectiveMetric || ""),
        initiativeMetric: String(meta.initiativeMetric || ""),
        projectMetric: String(meta.projectMetric || "")
      };

      if (!row.initiativeId) rows.push({ ...row, category: "missing-initiative", issue: "Missing initiative mapping." });
      if (!row.projectId) rows.push({ ...row, category: "missing-project", issue: "Missing project mapping." });
      if (!row.targetValue) rows.push({ ...row, category: "missing-target", issue: "Missing target value." });
      if (annual.length === 0) rows.push({ ...row, category: "missing-plan-years", issue: "No annual plan values between 2027 and 2046." });
      if ((row.baselineYear && !yearRegex.test(row.baselineYear)) || (row.targetYear && !yearRegex.test(row.targetYear)) ||
          (yearRegex.test(row.baselineYear) && yearRegex.test(row.targetYear) && Number(row.targetYear) < Number(row.baselineYear))) {
        rows.push({ ...row, category: "invalid-year-anchors", issue: "Baseline/Target year anchors are invalid." });
      }
      if ((row.initiativeId && !row.initiativeMetric) || (row.projectId && !row.projectMetric)) {
        rows.push({ ...row, category: "metric-mismatch", issue: "Metric mismatch across lineage levels." });
      }
      if (row.companyScopeMode === "Explicit" && !row.companyId) {
        rows.push({ ...row, category: "company-scope", issue: "Company scope mode is Explicit without company selection." });
      }
    });
    return rows;
  }

  function updateKpi(gaps) {
    const byCategory = (name) => gaps.filter((x) => x.category === name).length;
    kpi.total.textContent = String(gaps.length);
    kpi.missingInitiative.textContent = String(byCategory("missing-initiative"));
    kpi.missingProject.textContent = String(byCategory("missing-project"));
    kpi.missingTarget.textContent = String(byCategory("missing-target"));
    kpi.missingPlan.textContent = String(byCategory("missing-plan-years"));
    kpi.invalidAnchor.textContent = String(byCategory("invalid-year-anchors"));
  }

  function applyFilters() {
    const q = String(searchEl?.value || "").trim().toLowerCase();
    const t = String(typeEl?.value || "");
    let rows = allGaps;
    if (t) rows = rows.filter((x) => x.category === t);
    if (q) rows = rows.filter((x) =>
      [x.goalId, x.goal, x.objective, x.initiativeId, x.initiative, x.projectId, x.project, x.issue].join(" ").toLowerCase().includes(q));
    render(rows);
    updateKpi(rows);
  }

  function render(rows) {
    bodyEl.innerHTML = rows.map((row) => `
      <tr>
        <td>${escapeHtml(row.category)}</td>
        <td>${escapeHtml(row.goal || row.goalId)}</td>
        <td>${escapeHtml(row.objective)}</td>
        <td>${escapeHtml(row.initiative || row.initiativeId || "-")}</td>
        <td>${escapeHtml(row.project || row.projectId || "-")}</td>
        <td>${escapeHtml(row.issue)}</td>
        <td class="text-end es-row-actions-col">
          ${window.enterpriseRowActionsMenu?.render?.(`${row.connectionId}-${row.category}`, [
            { action: "edit", label: "Edit Row", href: "/management-governance/enterprise-strategy-business-performance/connections" },
            { action: "openGoal", label: "Open Goal", href: row.goalId ? `/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(row.goalId)}` : "#" },
            { action: "openObjective", label: "Open Objective", href: "/management-governance/enterprise-strategy-business-performance/objectives/alignment" },
            { action: "openInitiative", label: "Open Initiative in Delivery", href: row.initiativeId ? `/management-governance/delivery-execution/initiatives/${encodeURIComponent(row.initiativeId)}` : "#" },
            { action: "openProject", label: "Open Project in Delivery", href: row.projectId ? `/management-governance/delivery-execution/projects/${encodeURIComponent(row.projectId)}` : "#" }
          ]) || ""}
        </td>
      </tr>
    `).join("");
    if (!rows.length) bodyEl.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-3">No gaps found.</td></tr>';
  }

  async function load() {
    try {
      const connections = await window.strategyConnectionsApi.list();
      allGaps = toGapRows(connections?.items || []);
      applyFilters();
    } catch (err) {
      bodyEl.innerHTML = `<tr><td colspan="7" class="text-center text-danger py-3">${escapeHtml(window.enterpriseStrategyUi?.getErrorMessage?.(err, "Failed to load gaps.") || "Failed to load gaps.")}</td></tr>`;
    }
  }

  applyBtn?.addEventListener("click", applyFilters);
  searchEl?.addEventListener("input", applyFilters);
  typeEl?.addEventListener("change", applyFilters);

  load();
})(window, document);
