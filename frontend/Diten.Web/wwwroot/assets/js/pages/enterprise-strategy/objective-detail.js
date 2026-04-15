(function (window, document) {
  "use strict";

  const objectiveId = window.objectiveDetailId;
  if (!objectiveId) return;

  const workbook = window.enterpriseWorkbookOptions || {};

  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");

  const formatDate = (value) => {
    const text = String(value || "").trim();
    const match = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : "-";
  };

  const metricSummary = (objective) => {
    const metricName = objective.primaryKpiMetric || objective.primaryMetricId || "-";
    const unit = objective.unitOfMeasure || "-";
    const direction = objective.directionOfPerformance || "-";
    const frequency = objective.reportingFrequency || "-";
    return `
      <div class="row g-3">
        <div class="col-12 col-md-6"><div><strong>Primary KPI / Metric</strong></div><div class="text-muted">${escapeHtml(metricName)}</div></div>
        <div class="col-12 col-md-6"><div><strong>Unit of Measure</strong></div><div class="text-muted">${escapeHtml(unit)}</div></div>
        <div class="col-12 col-md-6"><div><strong>Direction</strong></div><div class="text-muted">${escapeHtml(direction)}</div></div>
        <div class="col-12 col-md-6"><div><strong>Reporting Frequency</strong></div><div class="text-muted">${escapeHtml(frequency)}</div></div>
      </div>
      <div class="alert alert-secondary mt-3 mb-0">Objective target rows remain strategy-governed here. Delivery-owned Initiative records keep the downstream execution and contribution plan details.</div>
    `;
  };

  function contributionSummary(initiative) {
    const rows = Array.isArray(initiative?.contributionPlanValues) ? initiative.contributionPlanValues : [];
    const plannedRows = rows.filter((row) => row?.plannedValue !== null && row?.plannedValue !== undefined).length;
    const parts = [];
    if (initiative?.contributionMetricName) parts.push(initiative.contributionMetricName);
    if (initiative?.contributionPlanGranularity) parts.push(initiative.contributionPlanGranularity);
    if (plannedRows > 0) parts.push(`${plannedRows} planned row${plannedRows === 1 ? "" : "s"}`);
    return parts.length ? parts.join(" | ") : "No contribution summary yet.";
  }

  function initiativeReferenceCard(initiative) {
    return `
      <div class="border rounded p-2 mb-2">
        <div class="d-flex justify-content-between align-items-start gap-2">
          <div>
            <strong>${escapeHtml(initiative.initiativeName || initiative.initiativeId)}</strong>
            <div class="small text-muted">${escapeHtml(initiative.initiativeId || "-")} | Status: ${escapeHtml(initiative.status || initiative.readinessStatus || "-")} | Readiness: ${escapeHtml(initiative.readinessStatus || "-")}</div>
            <div class="small text-muted">Owner: ${escapeHtml(workbook.userDisplayName?.(initiative.deliveryOwnerPersonId || initiative.owner) || initiative.owner || initiative.deliveryOwnerPersonId || "-")} | Sponsor: ${escapeHtml(workbook.companyDisplayName?.(initiative.sponsoringCompanyId) || initiative.sponsoringCompanyId || "-")}</div>
            <div class="small text-muted">Contribution: ${escapeHtml(contributionSummary(initiative))}</div>
          </div>
          <div class="d-flex flex-column gap-1">
            <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/${encodeURIComponent(initiative.initiativeId)}">Open in Delivery</a>
            <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/${encodeURIComponent(initiative.initiativeId)}/edit">Edit in Delivery</a>
          </div>
        </div>
      </div>`;
  }

  async function load() {
    const [detail, alignment, initiatives] = await Promise.all([
      window.strategyObjectivesApi.get(objectiveId),
      window.strategyObjectivesApi.alignmentSummary(objectiveId),
      window.strategyObjectivesApi.initiatives(objectiveId)
    ]);

    const objective = detail?.objective || {};
    const parentGoal = detail?.parentGoal || null;
    const summary = alignment || detail?.alignmentSummary || {};
    const linkedInitiatives = initiatives || detail?.linkedInitiatives || [];

    document.getElementById("objective-detail-overview").innerHTML = `
      <div><strong>${escapeHtml(objective.name || "-")}</strong></div>
      <div class="small text-muted">${escapeHtml(objective.statement || "")}</div>
      <div class="row g-3 mt-1">
        <div class="col-12 col-md-6">
          <div><strong>Status</strong></div>
          <div><span class="badge bg-label-info">${escapeHtml(objective.status || "-")}</span></div>
        </div>
        <div class="col-12 col-md-6">
          <div><strong>Parent Goal</strong></div>
          <div>${parentGoal ? `<a href="/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(parentGoal.id)}">${escapeHtml(parentGoal.name || parentGoal.id)}</a>` : "-"}</div>
        </div>
        <div class="col-12 col-md-6">
          <div><strong>Strategy Period</strong></div>
          <div class="text-muted">${escapeHtml(objective.planningCycle || objective.strategyPeriodId || "-")}</div>
        </div>
        <div class="col-12 col-md-6">
          <div><strong>Horizon</strong></div>
          <div class="text-muted">${formatDate(objective.timeHorizonStart || objective.startDate)} - ${formatDate(objective.timeHorizonEnd || objective.endDate)}</div>
        </div>
        <div class="col-12 col-md-6">
          <div><strong>Owner</strong></div>
          <div class="text-muted">${escapeHtml(objective.owner || objective.ownerId || "-")}</div>
        </div>
        <div class="col-12 col-md-6">
          <div><strong>Entity Scope</strong></div>
          <div class="text-muted">${escapeHtml(objective.entityScope || "-")}</div>
        </div>
      </div>`;

    document.getElementById("objective-detail-performance").innerHTML = metricSummary(objective);

    document.getElementById("objective-detail-alignment").innerHTML = `
      <div class="row g-3">
        <div class="col-12 col-md-4">
          <div><strong>Aligned Initiatives</strong></div>
          <div class="text-muted">${escapeHtml(String(summary.linkedInitiativesCount ?? linkedInitiatives.length ?? 0))}</div>
        </div>
        <div class="col-12 col-md-4">
          <div><strong>Aligned Projects</strong></div>
          <div class="text-muted">${escapeHtml(String(summary.linkedProjectsCount ?? 0))}</div>
        </div>
        <div class="col-12 col-md-4">
          <div><strong>Alignment Status</strong></div>
          <div>${summary.hasCoverageGap ? '<span class="badge bg-label-warning">Coverage gap</span>' : '<span class="badge bg-label-success">Aligned</span>'}</div>
        </div>
      </div>
      <div class="alert ${summary.hasCoverageGap ? "alert-warning" : "alert-secondary"} mt-3 mb-3">
        ${escapeHtml(summary.auditSummary || "Alignment stays in ES&BP, while Initiative record ownership and lifecycle management live in Delivery & Execution Management.")}
      </div>
      <div class="d-flex gap-2 flex-wrap mb-3">
        <a class="btn btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/objectives/alignment?objectiveId=${encodeURIComponent(objectiveId)}">Open Strategy Alignment Register</a>
        <a class="btn btn-outline-secondary" href="/management-governance/delivery-execution/initiatives?parentObjectiveId=${encodeURIComponent(objectiveId)}">Open Delivery Initiatives</a>
        <a class="btn btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/new?parentObjectiveId=${encodeURIComponent(objectiveId)}">Create Initiative in Delivery</a>
      </div>
      <div>
        ${linkedInitiatives.length ? linkedInitiatives.map(initiativeReferenceCard).join("") : '<div class="small text-muted">No aligned initiatives linked yet.</div>'}
      </div>`;

    document.getElementById("objective-detail-audit").innerHTML = escapeHtml(detail?.auditSummary || summary.auditSummary || "No audit summary.");
  }

  load().catch(() => {
    document.getElementById("objective-detail-overview").innerHTML = '<div class="alert alert-warning">Failed to load objective detail.</div>';
  });
})(window, document);
