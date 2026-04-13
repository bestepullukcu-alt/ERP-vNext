(function (window, document) {
  "use strict";
  const tbody = document.querySelector("#kpi-threshold-table tbody");

  async function load() {
    const rows = await window.kpiLibraryApi.thresholdModels();
    tbody.innerHTML = (rows || []).map((x) => `
      <tr>
        <td>${x.modelCode || ""}</td>
        <td>${x.modelName || ""}</td>
        <td>${x.metricUnit || ""}</td>
        <td>${x.polarity || ""}</td>
        <td>${x.interpretation || ""}</td>
        <td>${x.redFloor ?? ""}</td>
        <td>${x.amberFloor ?? ""}</td>
        <td>${x.greenTarget ?? ""}</td>
        <td>${x.greenStretch ?? ""}</td>
        <td>${x.upperControlLimit ?? ""}</td>
        <td>${x.status || ""}</td>
        <td>${x.versionLabel || ""}</td>
      </tr>`).join("");
  }

  load().catch((err) => {
    tbody.innerHTML = `<tr><td colspan="12" class="text-danger">${window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load threshold models") || "Unable to load threshold models"}</td></tr>`;
  });
})(window, document);
