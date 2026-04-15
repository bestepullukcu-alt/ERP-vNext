(function (window, document) {
  "use strict";
  const tbody = document.querySelector("#kpi-pack-table tbody");
  const search = document.getElementById("kpi-pack-search");

  function render(rows) {
    const q = String(search?.value || "").trim().toLowerCase();
    const list = (rows || []).filter((x) => !q || `${x.packCode} ${x.packName} ${x.description}`.toLowerCase().includes(q));
    tbody.innerHTML = list.map((x) => `
      <tr>
        <td>${x.packCode || ""}</td>
        <td>${x.packName || ""}</td>
        <td>${x.packLevel || ""}</td>
        <td>${x.description || ""}</td>
        <td>${x.status || ""}</td>
        <td>${x.versionLabel || ""}</td>
        <td>${x.publishDate ? new Date(x.publishDate).toISOString().slice(0, 10) : ""}</td>
        <td>${x.defaultOwnerRole || ""}</td>
        <td>${x.kpiCount || 0}</td>
        <td class="text-end">
          ${(window.enterpriseRowActionsMenu?.render?.(x.id, [
            { action: "open-pack", label: "Open Pack", href: `/management-governance/enterprise-strategy-business-performance/kpis/scorecard-packs/${encodeURIComponent(x.id)}` },
            { action: "instantiate-pack", label: "Instantiate Pack (MVP)" }
          ]) || "")}
        </td>
      </tr>`).join("");
  }

  async function load() {
    const data = await window.kpiLibraryApi.packs({ page: 1, pageSize: 5000 });
    const rows = data?.items || [];
    render(rows);
    document.getElementById("kpi-pack-apply")?.addEventListener("click", () => render(rows));
  }

  tbody?.addEventListener("click", (event) => {
    const btn = event.target.closest(".es-row-action-item");
    if (!btn) return;
    if ((btn.dataset.action || "") !== "instantiate-pack") return;
    window.enterpriseStrategyUi?.notify?.("Pack instantiation placeholder ready for next phase.");
  });

  load().catch((err) => {
    tbody.innerHTML = `<tr><td colspan="10" class="text-danger">${window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load scorecard packs") || "Unable to load scorecard packs"}</td></tr>`;
  });
})(window, document);
