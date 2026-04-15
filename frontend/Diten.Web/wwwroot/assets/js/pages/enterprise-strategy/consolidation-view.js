(function (window, document) {
  "use strict";

  const el = {
    goal: document.getElementById("consolidation-goal"),
    company: document.getElementById("consolidation-company"),
    apply: document.getElementById("consolidation-apply"),
    table: document.getElementById("consolidation-table")
  };
  const workbook = window.enterpriseWorkbookOptions || {};

  function toNumber(v) { return Number(v || 0); }

  function render(rows) {
    document.getElementById("cons-kpi-rows").textContent = String(rows.length);
    document.getElementById("cons-kpi-current").textContent = rows.reduce((a, x) => a + toNumber(x.currentValue), 0).toFixed(2);
    document.getElementById("cons-kpi-target").textContent = rows.reduce((a, x) => a + toNumber(x.targetValue), 0).toFixed(2);
    document.getElementById("cons-kpi-variance").textContent = rows.reduce((a, x) => a + toNumber(x.variance), 0).toFixed(2);
    el.table.innerHTML = rows.map((row) => {
      const actions = window.enterpriseRowActionsMenu?.render?.(`${row.goalId}-${row.objectiveId}`, [
        { action: "objective", label: "Open Objective", href: `/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(row.objectiveId)}` },
        { action: "connections", label: "Open Alignment Register", href: "/management-governance/enterprise-strategy-business-performance/connections" }
      ]) || "";
      return `<tr>
        <td>${row.goalName || row.goalId}</td>
        <td>${row.objectiveName || row.objectiveId}</td>
        <td>${workbook.companyDisplayName?.(row.companyId) || row.companyId || "-"}</td>
        <td class="text-end">${(toNumber(row.contributionTotal) * 100).toFixed(1)}%</td>
        <td class="text-end">${toNumber(row.currentValue).toFixed(2)}</td>
        <td class="text-end">${toNumber(row.targetValue).toFixed(2)}</td>
        <td class="text-end ${toNumber(row.variance) < 0 ? "text-danger" : "text-success"}">${toNumber(row.variance).toFixed(2)}</td>
        <td class="text-end es-row-actions-col">${actions}</td>
      </tr>`;
    }).join("");
  }

  async function load() {
    const query = { goalId: String(el.goal?.value || "").trim(), company: String(el.company?.value || "").trim() };
    const rows = await window.strategyCascadeApi.consolidation(query);
    render(Array.isArray(rows) ? rows : []);
  }

  el.apply?.addEventListener("click", load);
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load consolidation view.") || "Unable to load consolidation view.";
    el.table.innerHTML = `<tr><td colspan="8" class="text-danger">${message}</td></tr>`;
  });
})(window, document);
