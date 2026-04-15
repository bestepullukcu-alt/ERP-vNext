(function (window, document) {
  "use strict";

  const table = document.getElementById("review-history-table");
  const filters = {
    goal: document.getElementById("history-goal"),
    objective: document.getElementById("history-objective"),
    period: document.getElementById("history-period"),
    apply: document.getElementById("history-apply")
  };

  function filterRows(rows) {
    const goal = String(filters.goal?.value || "").trim().toLowerCase();
    const objective = String(filters.objective?.value || "").trim().toLowerCase();
    const period = String(filters.period?.value || "").trim().toLowerCase();
    return rows.filter((row) => {
      if (goal && !String(row.reviewId || "").toLowerCase().includes(goal)) return false;
      if (objective && !String(row.reviewType || "").toLowerCase().includes(objective)) return false;
      if (period && !String(row.reviewDate || "").toLowerCase().includes(period)) return false;
      return true;
    });
  }

  function render(rows) {
    table.innerHTML = rows.map((row) => `<tr>
      <td>${row.reviewId}</td>
      <td>${row.reviewDate ? new Date(row.reviewDate).toLocaleDateString() : "-"}</td>
      <td>${row.reviewType}</td>
      <td class="text-end">${row.decisionsCount ?? 0}</td>
      <td class="text-end">${row.openActions ?? 0}</td>
      <td class="text-end">${row.closedActions ?? 0}</td>
      <td>${row.scorecardSnapshotRef || "-"}</td>
      <td>${row.cascadeSnapshotRef || "-"}</td>
    </tr>`).join("");
  }

  async function load() {
    const rows = await window.strategyReviewsApi.history();
    const list = Array.isArray(rows) ? rows : [];
    render(filterRows(list));
  }

  filters.apply?.addEventListener("click", load);
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load review history.") || "Unable to load review history.";
    table.innerHTML = `<tr><td colspan="8" class="text-danger">${message}</td></tr>`;
  });
})(window, document);
