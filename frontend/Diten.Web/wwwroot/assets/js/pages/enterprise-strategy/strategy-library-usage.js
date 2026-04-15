(function (window, document) {
  "use strict";

  const summaryHost = document.getElementById("library-usage-summary-cards");
  const templatesTbody = document.querySelector("#library-usage-templates-table tbody");
  const blueprintsTbody = document.querySelector("#library-usage-blueprints-table tbody");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);

  function renderSummary(summary) {
    summaryHost.innerHTML = `
      <div class="col"><div class="border rounded p-2"><div class="small text-muted">Total Templates</div><div class="h5 mb-0">${summary.totalTemplates ?? 0}</div></div></div>
      <div class="col"><div class="border rounded p-2"><div class="small text-muted">Published Templates</div><div class="h5 mb-0">${summary.publishedTemplates ?? 0}</div></div></div>
      <div class="col"><div class="border rounded p-2"><div class="small text-muted">Blueprint Packs</div><div class="h5 mb-0">${summary.totalBlueprintPacks ?? 0}</div></div></div>
      <div class="col"><div class="border rounded p-2"><div class="small text-muted">Instantiations</div><div class="h5 mb-0">${summary.totalInstantiations ?? 0}</div></div></div>`;
  }

  function renderRows(tbody, rows, emptyText) {
    tbody.innerHTML = "";
    (rows || []).forEach((x) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td><code>${x.id || ""}</code></td>
        <td>${x.name || ""}</td>
        <td>${x.usageCount ?? 0}</td>
        <td>${x.lastInstantiatedBy || ""}</td>
        <td>${x.lastInstantiatedAt || ""}</td>`;
      tbody.appendChild(tr);
    });
    if (!(rows || []).length) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="5" class="text-center text-muted py-3">${emptyText}</td>`;
      tbody.appendChild(tr);
    }
  }

  async function load() {
    try {
      const [summary, templates, blueprints] = await Promise.all([
        window.strategyLibraryApi.usageSummary(),
        window.strategyLibraryApi.usageTemplates(),
        window.strategyLibraryApi.usageBlueprints()
      ]);
      renderSummary(summary || {});
      renderRows(templatesTbody, templates || [], "No template usage yet.");
      renderRows(blueprintsTbody, blueprints || [], "No blueprint usage yet.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Usage load failed") || "Usage load failed", "error");
    }
  }

  load();
})(window, document);
