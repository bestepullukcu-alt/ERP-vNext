(function (window, document) {
  "use strict";

  const el = {
    goal: document.getElementById("cascade-goal"),
    metric: document.getElementById("cascade-metric"),
    company: document.getElementById("cascade-company"),
    apply: document.getElementById("cascade-apply"),
    state: document.getElementById("cascade-state"),
    goalName: document.getElementById("cascade-goal-name"),
    goalMetric: document.getElementById("cascade-goal-metric"),
    table: document.getElementById("cascade-table")
  };
  const workbook = window.enterpriseWorkbookOptions || {};

  function number(v) { return Number(v || 0); }

  function render(snapshot) {
    const rows = snapshot?.objectives || [];
    el.goalName.textContent = snapshot?.goalName || "-";
    el.goalMetric.textContent = snapshot?.goalMetric || "-";
    document.getElementById("cascade-parent-target").textContent = String(snapshot?.parentTarget ?? 0);
    document.getElementById("cascade-allocated-total").textContent = String(rows.reduce((a, x) => a + number(x.allocatedTarget), 0));
    document.getElementById("cascade-coverage-complete").textContent = String(rows.filter((x) => x.coverageStatus === "Complete").length);
    document.getElementById("cascade-coverage-warning").textContent = String(rows.filter((x) => String(x.warning || "").trim()).length);
    el.table.innerHTML = rows.map((r) => `<tr>
      <td><a href="/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(r.objectiveId)}">${r.objectiveName}</a></td>
      <td class="text-end">${(number(r.contributionWeight) * 100).toFixed(1)}%</td>
      <td class="text-end">${number(r.allocatedTarget).toFixed(2)}</td>
      <td>${r.coverageStatus || "-"}</td>
      <td>${workbook.companyDisplayName?.(r.companyId) || r.companyId || "-"}</td>
      <td>${r.warning || "-"}</td>
    </tr>`).join("");
    el.state.textContent = `Loaded ${rows.length} cascade rows.`;
  }

  async function load() {
    el.state.textContent = "Loading cascade snapshot...";
    const query = {
      goalId: String(el.goal?.value || "").trim(),
      metric: String(el.metric?.value || "").trim(),
      company: String(el.company?.value || "").trim()
    };
    const snapshot = await window.strategyCascadeApi.builder(query);
    render(snapshot);
  }

  el.apply?.addEventListener("click", load);
  load().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load cascade builder.") || "Unable to load cascade builder.";
    el.state.textContent = msg;
  });
})(window, document);
