(function (window, document) {
  "use strict";

  const filters = {
    goal: document.getElementById("scorecard-goal"),
    objective: document.getElementById("scorecard-objective"),
    company: document.getElementById("scorecard-company"),
    period: document.getElementById("scorecard-period"),
    apply: document.getElementById("scorecard-apply")
  };
  const workbook = window.enterpriseWorkbookOptions || {};

  const stateEl = document.getElementById("scorecard-state");
  const cardsEl = document.getElementById("scorecard-cards");
  const tableBody = document.getElementById("scorecard-table");

  function badge(status) {
    const tone = status === "On Track" ? "success" : status === "At Risk" ? "warning" : "secondary";
    return `<span class="badge bg-label-${tone}">${status}</span>`;
  }

  function trendIcon(value) {
    if (value === "Up") return "&#8593;";
    if (value === "Down") return "&#8595;";
    return "&#8594;";
  }

  function buildSummaryCard(row) {
    return `<div class="col-12 col-md-6 col-xl-4">
      <div class="border rounded p-2 h-100">
        <div class="d-flex justify-content-between align-items-start">
          <div>
            <div class="fw-semibold">${row.kpiName}</div>
            <div class="small text-muted">${row.goalId} / ${row.objectiveId}</div>
          </div>
          ${badge(row.status)}
        </div>
        <div class="small mt-2">
          <div>Current: <strong>${row.currentValue ?? "-"}</strong></div>
          <div>Target: <strong>${row.targetValue ?? "-"}</strong></div>
          <div>Variance: <strong>${row.variance ?? "-"}</strong></div>
        </div>
      </div>
    </div>`;
  }

  function render(snapshot) {
    const rows = snapshot?.rows || [];
    document.getElementById("scorecard-total").textContent = String(snapshot?.totalKpis ?? rows.length);
    document.getElementById("scorecard-ontrack").textContent = String(snapshot?.onTrackCount ?? rows.filter((x) => x.status === "On Track").length);
    document.getElementById("scorecard-risk").textContent = String(snapshot?.atRiskCount ?? rows.filter((x) => x.status === "At Risk").length);
    document.getElementById("scorecard-offtrack").textContent = String(snapshot?.offTrackCount ?? rows.filter((x) => x.status === "Off Track").length);
    cardsEl.innerHTML = rows.slice(0, 6).map(buildSummaryCard).join("") || '<div class="col-12 text-muted">No scorecard rows for current filters.</div>';
    tableBody.innerHTML = rows.map((row) => `<tr>
      <td><a href="/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(row.kpiId)}">${row.kpiName}</a></td>
      <td>${row.goalId || "-"}</td>
      <td>${row.objectiveId || "-"}</td>
      <td>${workbook.companyDisplayName?.(row.companyId) || row.companyId || "-"}</td>
      <td>${row.timePeriod || "-"}</td>
      <td class="text-end">${row.currentValue ?? "-"}</td>
      <td class="text-end">${row.baselineValue ?? "-"}</td>
      <td class="text-end">${row.targetValue ?? "-"}</td>
      <td class="text-end">${row.variance ?? "-"}</td>
      <td>${trendIcon(row.trend)} ${row.trend || "-"}</td>
      <td>${badge(row.status)}</td>
      <td>${row.createdFromLibrary ? `${row.sourceKpiTemplateCode || "Library"} ${row.sourceKpiTemplateVersion || ""}` : "Blank/Runtime"}</td>
    </tr>`).join("");
    stateEl.textContent = `Loaded ${rows.length} scorecard KPI rows.`;
  }

  async function load() {
    stateEl.textContent = "Loading scorecard...";
    const query = {
      goalId: String(filters.goal?.value || "").trim(),
      objectiveId: String(filters.objective?.value || "").trim(),
      company: String(filters.company?.value || "").trim(),
      period: String(filters.period?.value || "").trim()
    };
    const snapshot = await window.strategyKpisApi.scorecard(query);
    render(snapshot || {});
  }

  filters.apply?.addEventListener("click", load);
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load scorecard dashboard.") || "Unable to load scorecard dashboard.";
    stateEl.textContent = message;
    cardsEl.innerHTML = "";
    tableBody.innerHTML = "";
  });
})(window, document);
