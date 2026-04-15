(function (window, document) {
  "use strict";
  const id = document.getElementById("kpi-pack-id")?.textContent?.trim();
  const nameEl = document.getElementById("kpi-pack-name");
  const metaEl = document.getElementById("kpi-pack-meta");
  const tbody = document.querySelector("#kpi-pack-items-table tbody");

  async function load() {
    const [pack, items] = await Promise.all([
      window.kpiLibraryApi.pack(id),
      window.kpiLibraryApi.packItems(id)
    ]);
    nameEl.textContent = `${pack.packCode} - ${pack.packName}`;
    metaEl.textContent = `${pack.packLevel} | ${pack.status} | ${pack.versionLabel} | owner: ${pack.defaultOwnerRole}`;
    tbody.innerHTML = (items || []).map((x) => `
      <tr>
        <td>${x.displayOrder || ""}</td>
        <td>${x.priorityClass || ""}</td>
        <td>${x.kpiTemplateCode || ""}</td>
        <td>${x.kpiTemplateName || ""}</td>
        <td>${x.rationale || ""}</td>
        <td class="text-end">
          ${(window.enterpriseRowActionsMenu?.render?.(x.id || `${x.packId}-${x.kpiTemplateCode}`, [
            { action: "open-template", label: "Open Template", href: `/management-governance/enterprise-strategy-business-performance/kpis/library/templates/${encodeURIComponent(x.kpiTemplateId || x.kpiTemplateCode)}` },
            { action: "instantiate-template", label: "Instantiate KPI (MVP)" }
          ]) || "")}
        </td>
      </tr>`).join("");
  }

  tbody?.addEventListener("click", (event) => {
    const btn = event.target.closest(".es-row-action-item");
    if (!btn) return;
    if ((btn.dataset.action || "") !== "instantiate-template") return;
    window.enterpriseStrategyUi?.notify?.("Template instantiation is available from KPI Library Catalog actions.");
  });

  load().catch((err) => {
    tbody.innerHTML = `<tr><td colspan="6" class="text-danger">${window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load pack detail") || "Unable to load pack detail"}</td></tr>`;
  });
})(window, document);
