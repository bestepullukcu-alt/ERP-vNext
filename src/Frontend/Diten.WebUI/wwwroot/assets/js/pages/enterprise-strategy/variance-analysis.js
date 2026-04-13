(function (window, document) {
  "use strict";

  const el = {
    goal: document.getElementById("variance-goal"),
    objective: document.getElementById("variance-objective"),
    company: document.getElementById("variance-company"),
    period: document.getElementById("variance-period"),
    kpi: document.getElementById("variance-kpi"),
    apply: document.getElementById("variance-apply"),
    table: document.getElementById("variance-table")
  };
  const workbook = window.enterpriseWorkbookOptions || {};

  const num = (v) => Number(v || 0);

  function render(rows) {
    el.table.innerHTML = rows.map((row) => {
      const actions = window.enterpriseRowActionsMenu?.render?.(`${row.kpiId}-${row.alignmentRowId}`, [
        { action: "kpi", label: "Open KPI", href: `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(row.kpiId)}` },
        { action: "objective", label: "Open Objective", href: `/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(row.objectiveId)}` },
        { action: "alignment", label: "Open Alignment Row", href: "/management-governance/enterprise-strategy-business-performance/connections" }
      ]) || "";
      return `<tr>
        <td>${row.goalId}</td>
        <td>${row.objectiveId}</td>
        <td>${row.kpiName}</td>
        <td>${workbook.companyDisplayName?.(row.companyId) || row.companyId || "-"}</td>
        <td>${row.timePeriod}</td>
        <td class="text-end">${num(row.targetValue).toFixed(2)}</td>
        <td class="text-end">${num(row.currentValue).toFixed(2)}</td>
        <td class="text-end ${num(row.varianceAmount) < 0 ? "text-danger" : "text-success"}">${num(row.varianceAmount).toFixed(2)}</td>
        <td class="text-end ${num(row.variancePercent) < 0 ? "text-danger" : "text-success"}">${num(row.variancePercent).toFixed(2)}%</td>
        <td>${row.trend}</td>
        <td>${row.status}</td>
        <td class="text-end es-row-actions-col">${actions}</td>
      </tr>`;
    }).join("");
  }

  async function load() {
    const query = {
      goalId: String(el.goal?.value || "").trim(),
      objectiveId: String(el.objective?.value || "").trim(),
      company: String(el.company?.value || "").trim(),
      period: String(el.period?.value || "").trim(),
      kpiId: String(el.kpi?.value || "").trim()
    };
    const rows = await window.strategyCascadeApi.variance(query);
    render(Array.isArray(rows) ? rows : []);
  }

  el.apply?.addEventListener("click", load);
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load variance analysis.") || "Unable to load variance analysis.";
    el.table.innerHTML = `<tr><td colspan="12" class="text-danger">${message}</td></tr>`;
  });
})(window, document);
