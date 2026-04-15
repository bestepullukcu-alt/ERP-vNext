(function (window, document) {
  "use strict";

  const id = window.initiativeDetailId;
  const workbook = window.enterpriseWorkbookOptions || {};
  if (!id) return;

  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
  const cleanText = (value) => String(value ?? "").trim();
  const isoDate = (value) => cleanText(value).slice(0, 10);
  const pill = (text) => `<span class="initiative-detail-pill">${escapeHtml(text)}</span>`;
  const stat = (label, value) => `
    <div class="initiative-detail-stat">
      <div class="initiative-detail-stat-label">${escapeHtml(label)}</div>
      <div class="initiative-detail-stat-value">${escapeHtml(value || "-")}</div>
    </div>`;
  const listHtml = (items) => (items && items.length ? `<ul class="mb-0">${items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>` : "<div class=\"text-muted\">None</div>");

  async function load() {
    try {
      const detail = await window.initiativeStrategyApi.get(id);
      const link = detail?.strategyLink || {};
      const readiness = detail?.readiness || link?.readiness || {};
      const projects = detail?.projects || [];

      document.getElementById("initiative-detail-title").textContent = link.initiativeName || detail?.initiative?.initiativeName || id;
      document.getElementById("initiative-detail-subtitle").innerHTML = [
        pill(link.readinessStatus || readiness.readinessStatus || "Draft"),
        pill(link.strategyLinkStatus || "Linked"),
        pill(link.contributionPlanGranularity || "No contribution plan"),
      ].join("");

      document.getElementById("initiative-detail-anchor").innerHTML = `
        <div class="row g-3">
          <div class="col-12 col-md-6">${stat("Parent Objective", `${link.parentObjectiveId || "-"} ${link.parentObjectiveName ? `- ${link.parentObjectiveName}` : ""}`)}</div>
          <div class="col-12 col-md-6">${stat("Parent Goal", `${link.parentGoalId || "-"} ${link.parentGoalName ? `- ${link.parentGoalName}` : ""}`)}</div>
          <div class="col-12 col-md-4">${stat("Objective Granularity", link.objectiveTargetGranularity || "-")}</div>
          <div class="col-12 col-md-4">${stat("Contribution Metric", link.contributionMetricName || "-")}</div>
          <div class="col-12 col-md-4">${stat("Contribution Timing", link.contributionTiming || "-")}</div>
        </div>`;

      document.getElementById("initiative-detail-ownership").innerHTML = `
        <div class="row g-3">
          <div class="col-12 col-md-6">${stat("Delivery Owner", workbook.userDisplayName?.(link.deliveryOwnerPersonId || link.owner) || link.owner || link.deliveryOwnerPersonId || "-")}</div>
          <div class="col-12 col-md-6">${stat("Executive Sponsor", workbook.userDisplayName?.(link.executiveSponsor) || link.executiveSponsor || "-")}</div>
          <div class="col-12 col-md-6">${stat("Owner Company / Org", workbook.companyDisplayName?.(link.deliveryOwnerCompanyId) || link.deliveryOwnerCompanyId || "-")}</div>
          <div class="col-12 col-md-6">${stat("Sponsoring Company", workbook.companyDisplayName?.(link.sponsoringCompanyId) || link.sponsoringCompanyId || "-")}</div>
          <div class="col-12 col-md-4">${stat("Start Date", isoDate(link.startDate) || "-")}</div>
          <div class="col-12 col-md-4">${stat("End Date", isoDate(link.endDate) || "-")}</div>
          <div class="col-12 col-md-4">${stat("Reporting Frequency", link.reportingFrequency || "-")}</div>
        </div>`;

      document.getElementById("initiative-detail-contribution-summary").innerHTML = `
        ${pill(`Contribution Plan: ${link.contributionPlanGranularity || "-"}`)}
        ${pill(`Rows: ${readiness.contributionPlanRowsCount ?? (link.contributionPlanValues || []).length}`)}
        ${pill(`Missing Planned Values: ${readiness.missingContributionValuesCount ?? 0}`)}
        <div class="mt-2 text-muted">${escapeHtml(link.benefitHypothesis || "No benefit / contribution hypothesis recorded yet.")}</div>`;

      const planRows = Array.isArray(link.contributionPlanValues) ? link.contributionPlanValues : [];
      document.getElementById("initiative-detail-contribution-plan").innerHTML = planRows.length
        ? planRows.map((row) => `
            <tr>
              <td><strong>${escapeHtml(row.periodLabel || row.periodKey || "-")}</strong></td>
              <td>${escapeHtml(isoDate(row.periodStart) || "-")}</td>
              <td>${escapeHtml(isoDate(row.periodEnd) || "-")}</td>
              <td>${escapeHtml(row.plannedValue ?? "-")}</td>
              <td>${escapeHtml(row.forecastValue ?? "-")}</td>
              <td>${escapeHtml(row.actualValue ?? "-")}</td>
              <td>${escapeHtml(row.commentary || "-")}</td>
            </tr>`).join("")
        : '<tr><td colspan="7" class="text-center text-muted py-3">No contribution plan rows saved yet.</td></tr>';

      document.getElementById("initiative-detail-governance").innerHTML = `
        <div class="row g-3">
          <div class="col-12 col-md-4">${stat("Budget Envelope", link.budgetEnvelope || "-")}</div>
          <div class="col-12 col-md-4">${stat("Budget Amount", link.budgetAmount ?? "-")}</div>
          <div class="col-12 col-md-4">${stat("Currency", link.currencyCode || "-")}</div>
          <div class="col-12 col-md-4">${stat("Governance Stage", link.governanceStage || "-")}</div>
          <div class="col-12 col-md-4">${stat("Decision Reference", link.decisionReference || "-")}</div>
          <div class="col-12 col-md-4">${stat("Evidence Reference", link.evidenceReference || "-")}</div>
          <div class="col-12">${stat("Entity Scope", link.entityScope || "-")}</div>
          <div class="col-12"><div class="initiative-detail-stat"><div class="initiative-detail-stat-label">Governance Notes</div><div class="initiative-detail-stat-value">${escapeHtml(link.governanceNotes || link.notes || "-")}</div></div></div>
        </div>`;

      document.getElementById("initiative-detail-readiness").innerHTML = `
        <div class="mb-2">${pill(`Draft: ${readiness.draftReady ? "Ready" : "Blocked"}`)}${pill(`Planning: ${readiness.planningReady ? "Ready" : "Blocked"}`)}${pill(`Publish: ${readiness.publishReady ? "Ready" : "Blocked"}`)}</div>
        <div class="mb-3 text-muted">${escapeHtml(readiness.readinessStatus || link.readinessStatus || "Blocked")}</div>
        <div class="mb-3"><strong class="d-block mb-1">Missing required items</strong>${listHtml(readiness.missing)}</div>
        <div class="mb-3"><strong class="d-block mb-1">Blocking issues</strong>${listHtml(readiness.blockers)}</div>
        <div><strong class="d-block mb-1">Warnings</strong>${listHtml(readiness.warnings)}</div>`;

      document.getElementById("initiative-detail-downstream").innerHTML = `
        <div class="text-muted mb-2">Projects and dependency networks are intentionally not editable here yet. This initiative module is focused on objective-linked delivery planning first.</div>
        <div class="mb-2">${pill(`Linked Projects: ${projects.length}`)}</div>
        <div>${projects.length ? projects.map((project) => `<div><a href="/management-governance/delivery-execution/projects/${encodeURIComponent(project.projectId)}">${escapeHtml(project.projectId)}</a> - ${escapeHtml(project.projectName || project.strategyLinkStatus || "")}</div>`).join("") : "<div class=\"text-muted\">No linked projects</div>"}</div>`;
    } catch (error) {
      document.getElementById("initiative-detail-anchor").innerHTML = `<div class="alert alert-warning mb-0">${escapeHtml(window.enterpriseStrategyUi?.getErrorMessage?.(error, "Unable to load initiative detail.") || "Unable to load initiative detail.")}</div>`;
    }
  }

  load().catch(() => {});
})(window, document);
