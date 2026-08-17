(function (window, document) {
  "use strict";

  function card(label, value, tone) {
    return `<div class="col-6 col-md-4"><div class="border rounded p-2 ${tone || ""}"><div class="small text-muted">${label}</div><div class="h5 mb-0">${value}</div></div></div>`;
  }

  async function load() {
    const [goals, objectives, gaps, kpis, calendar] = await Promise.all([
      window.strategyGoalsApi.list(),
      window.strategyObjectivesApi.list(),
      window.strategyConnectionsApi.coverageGaps(),
      window.strategyKpisApi?.list?.().catch(() => ({ items: [] })),
      window.strategyReviewsApi?.calendar?.().catch(() => []),
    ]);

    const goalItems = goals?.items || [];
    const objectiveItems = objectives?.items || [];
    const gapItems = gaps || [];
    const kpiItems = kpis?.items || [];
    const reviewItems = calendar || [];
    const cards = [
      card("Pending items", goalItems.filter((x) => x.status === "Draft").length + objectiveItems.filter((x) => x.status === "Draft").length),
      card("High-risk modules", 1),
      card("Implemented modules", 6),
      card("Active goals", goalItems.filter((x) => x.status === "Active").length),
      card("Active objectives", objectiveItems.filter((x) => x.status === "Active").length),
      card("Connection gaps", gapItems.length, "bg-label-warning"),
      card("Active KPIs", kpiItems.filter((x) => x.status === "Active").length),
      card("Planned reviews", reviewItems.filter((x) => x.status === "Planned").length),
    ];
    document.getElementById("overview-summary-cards").innerHTML = cards.join("");

    document.getElementById("overview-work-queue").innerHTML =
      `<div>- ${goalItems.filter((x) => x.status === "Draft").length} draft goals pending activation checks</div>` +
      `<div>- ${objectiveItems.filter((x) => x.status === "Draft").length} draft objectives pending alignment</div>`;

    document.getElementById("overview-health-snapshot").innerHTML =
      `<div>Goals healthy: ${goalItems.length - gapItems.length}</div><div>Coverage gaps: ${gapItems.length}</div>`;
  }

  load().catch((err) => {
    const message = window.enterpriseStrategyUi.getErrorMessage(err, "Unable to load summary");
    document.getElementById("overview-summary-cards").innerHTML = `<div class="col-12"><div class="alert alert-warning">Overview degraded state: ${message}</div></div>`;
  });
})(window, document);
