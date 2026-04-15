(function (window, document) {
  "use strict";
  const id = document.getElementById("kpi-template-id")?.textContent?.trim();
  const fields = document.getElementById("kpi-template-fields");
  const title = document.getElementById("kpi-template-name");
  const instantiateBtn = document.getElementById("kpi-template-instantiate");

  function row(label, value) {
    return `<dt class="col-sm-3">${label}</dt><dd class="col-sm-9">${value ?? ""}</dd>`;
  }

  async function load() {
    const t = await window.kpiLibraryApi.template(id);
    title.textContent = `${t.templateCode} - ${t.name}`;
    fields.innerHTML = [
      row("Category", t.category),
      row("Strategic Perspective", t.strategicPerspective),
      row("KPI Type", t.type),
      row("Object Level", t.objectLevel),
      row("Description", t.description),
      row("Business Question", t.businessQuestion),
      row("Polarity", t.polarity),
      row("Unit", t.unitOfMeasure),
      row("Aggregation", t.aggregationMethod),
      row("Reporting Frequency", t.reportingFrequency),
      row("Formula Type", t.formulaType),
      row("Formula", t.formulaExpression),
      row("Threshold Model", t.thresholdModelCode),
      row("Default Owner", t.defaultOwnerRole),
      row("Review Role", t.reviewRole),
      row("Version", t.versionLabel),
      row("Status", t.status),
      row("Usage Count", t.usageCount),
      row("Last Used", t.lastUsedAt ? `${new Date(t.lastUsedAt).toISOString()} by ${t.lastUsedBy || "n/a"}` : "Never")
    ].join("");
  }

  instantiateBtn?.addEventListener("click", async () => {
    try {
      await window.strategyKpisApi.instantiateFromLibrary(id, false);
      window.enterpriseStrategyUi?.notify?.("KPI instantiated to catalog.");
    } catch (err) {
      window.enterpriseStrategyUi?.notify?.(window.enterpriseStrategyUi?.getErrorMessage?.(err, "Instantiate failed") || "Instantiate failed", "error");
    }
  });

  load().catch((err) => {
    fields.innerHTML = `<dt class="col-12 text-danger">${window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load template") || "Unable to load template"}</dt>`;
  });
})(window, document);
