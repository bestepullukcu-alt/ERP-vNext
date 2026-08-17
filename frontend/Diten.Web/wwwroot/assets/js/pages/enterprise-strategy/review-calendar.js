(function (window, document) {
  "use strict";

  const table = document.getElementById("review-calendar-table");

  function fmtDate(value) {
    if (!value) return "-";
    return new Date(value).toLocaleDateString();
  }

  function render(rows) {
    table.innerHTML = rows.map((row) => {
      const actions = window.enterpriseRowActionsMenu?.render?.(row.id, [
        { action: "pack", label: "Open Review Pack", href: `/management-governance/enterprise-strategy-business-performance/reviews/pack?reviewId=${encodeURIComponent(row.id)}` },
        { action: "history", label: "Open Review History", href: "/management-governance/enterprise-strategy-business-performance/reviews/history" },
        { action: "decisions", label: "Open Decisions & Actions", href: `/management-governance/enterprise-strategy-business-performance/reviews/decisions?reviewId=${encodeURIComponent(row.id)}` }
      ]) || "";
      return `<tr>
        <td>${fmtDate(row.reviewDate)}</td>
        <td>${row.reviewType}</td>
        <td>${row.goalId}</td>
        <td>${row.objectiveId}</td>
        <td>${row.scorecardScope}</td>
        <td>${row.facilitator}</td>
        <td>${row.status}</td>
        <td class="text-end es-row-actions-col">${actions}</td>
      </tr>`;
    }).join("");
  }

  window.strategyReviewsApi.calendar()
    .then((rows) => render(Array.isArray(rows) ? rows : []))
    .catch((err) => {
      const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load review calendar.") || "Unable to load review calendar.";
      table.innerHTML = `<tr><td colspan="8" class="text-danger">${message}</td></tr>`;
    });
})(window, document);
