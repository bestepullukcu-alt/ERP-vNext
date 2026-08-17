(function (window, document) {
  "use strict";

  const idEl = document.getElementById("library-blueprint-id");
  const nameEl = document.getElementById("library-blueprint-name");
  const descEl = document.getElementById("library-blueprint-description");
  const statusEl = document.getElementById("library-blueprint-status");
  const versionEl = document.getElementById("library-blueprint-version");
  const tbody = document.querySelector("#library-blueprint-hierarchy-table tbody");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);

  function renderRows(rows) {
    tbody.innerHTML = "";
    rows.forEach((r) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td><code>${r.goalTemplateId || "-"}</code></td>
        <td><code>${r.objectiveTemplateId || "-"}</code></td>
        <td><code>${r.initiativeTemplateId || "-"}</code></td>
        <td><code>${r.projectTemplateId || "-"}</code></td>
        <td>${r.aggregationMethod || "-"}</td>
        <td>${r.planningYearStart || "-"} - ${r.planningYearEnd || "-"}</td>`;
      tbody.appendChild(tr);
    });
    if (!rows.length) {
      const tr = document.createElement("tr");
      tr.innerHTML = '<td colspan="6" class="text-center text-muted py-3">No hierarchy rows found.</td>';
      tbody.appendChild(tr);
    }
  }

  async function load() {
    const id = String(idEl?.textContent || "").trim();
    if (!id) return;
    try {
      const detail = await window.strategyLibraryApi.blueprint(id);
      nameEl.textContent = detail?.name || id;
      descEl.textContent = detail?.description || "";
      statusEl.textContent = detail?.status || "-";
      versionEl.textContent = String(detail?.version ?? 0);
      renderRows(detail?.hierarchyRows || []);
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Blueprint load failed") || "Blueprint load failed", "error");
    }
  }

  load();
})(window, document);
