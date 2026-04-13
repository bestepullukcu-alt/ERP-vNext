(function (window, document) {
  "use strict";
  const exceptionsBody = document.querySelector("#gov-exceptions-table tbody");
  const actionsBody = document.querySelector("#gov-actions-table tbody");

  function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = String(value ?? 0);
  }

  async function load() {
    const [summary, exceptions, actions] = await Promise.all([
      window.kpiLibraryApi.governanceSummary(),
      window.kpiLibraryApi.governanceExceptions(),
      window.kpiLibraryApi.governanceActions()
    ]);

    setText("gov-total", summary.totalTemplates);
    setText("gov-draft", summary.draft);
    setText("gov-review", summary.inReview);
    setText("gov-approved", summary.approved);
    setText("gov-published", summary.published);
    setText("gov-retired", summary.retired);
    setText("gov-missing-owner", summary.missingOwner);
    setText("gov-missing-threshold", summary.missingThreshold);
    setText("gov-missing-formula", summary.missingFormula);

    exceptionsBody.innerHTML = (exceptions || []).map((x) => `
      <tr>
        <td>${x.templateCode || ""}</td>
        <td>${x.name || ""}</td>
        <td>${x.status || ""}</td>
        <td>${x.exceptionType || ""}</td>
        <td>${x.message || ""}</td>
        <td class="text-end">
          ${(window.enterpriseRowActionsMenu?.render?.(x.templateId, [
            { action: "open-template", label: "Open Template", href: `/management-governance/enterprise-strategy-business-performance/kpis/library/templates/${encodeURIComponent(x.templateId)}` },
            { action: "open-library", label: "Open KPI Library", href: "/management-governance/enterprise-strategy-business-performance/kpis/library" }
          ]) || "")}
        </td>
      </tr>`).join("");

    actionsBody.innerHTML = (actions || []).map((x) => `
      <tr>
        <td>${x.at ? new Date(x.at).toISOString().replace("T", " ").slice(0, 19) : ""}</td>
        <td>${x.entityType || ""}:${x.entityId || ""}</td>
        <td>${x.action || ""}</td>
        <td>${x.beforeStatus || ""}</td>
        <td>${x.afterStatus || ""}</td>
        <td>${x.actor || ""}</td>
      </tr>`).join("");
  }

  load().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load KPI governance") || "Unable to load KPI governance";
    exceptionsBody.innerHTML = `<tr><td colspan="6" class="text-danger">${msg}</td></tr>`;
    actionsBody.innerHTML = `<tr><td colspan="6" class="text-danger">${msg}</td></tr>`;
  });
})(window, document);
