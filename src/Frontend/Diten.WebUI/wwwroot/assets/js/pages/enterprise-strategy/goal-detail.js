(function (window, document) {
  "use strict";

  const goalId = window.goalDetailId;
  if (!goalId) return;

  const workbook = window.enterpriseWorkbookOptions || {};
  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");

  function contributionSummary(initiative) {
    const planRows = Array.isArray(initiative?.contributionPlanValues) ? initiative.contributionPlanValues : [];
    const plannedRows = planRows.filter((row) => row?.plannedValue !== null && row?.plannedValue !== undefined).length;
    const parts = [];
    if (initiative?.contributionMetricName) parts.push(initiative.contributionMetricName);
    if (initiative?.contributionPlanGranularity) parts.push(initiative.contributionPlanGranularity);
    if (plannedRows > 0) parts.push(`${plannedRows} planned row${plannedRows === 1 ? "" : "s"}`);
    return parts.length ? parts.join(" | ") : "No contribution plan summary yet.";
  }

  function initiativeCard(initiative, objectiveName) {
    return `
      <div class="border rounded p-2 mb-2">
        <div class="d-flex justify-content-between align-items-start gap-2">
          <div>
            <strong>${escapeHtml(initiative.initiativeName || initiative.initiativeId)}</strong>
            <div class="small text-muted">${escapeHtml(initiative.initiativeId || "-")} | Objective: ${escapeHtml(objectiveName || initiative.parentObjectiveId || "-")}</div>
            <div class="small text-muted">Status: ${escapeHtml(initiative.status || initiative.readinessStatus || "-")} | Owner: ${escapeHtml(workbook.userDisplayName?.(initiative.deliveryOwnerPersonId || initiative.owner) || initiative.owner || initiative.deliveryOwnerPersonId || "-")}</div>
            <div class="small text-muted">Contribution: ${escapeHtml(contributionSummary(initiative))}</div>
          </div>
          <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/${encodeURIComponent(initiative.initiativeId)}">Open in Delivery</a>
        </div>
      </div>`;
  }

  async function load() {
    const [detail, objectives, summary] = await Promise.all([
      window.strategyGoalsApi.get(goalId),
      window.strategyGoalsApi.objectives(goalId),
      window.strategyGoalsApi.summary(goalId),
    ]);

    const goal = detail.goal || {};
    const objectiveRows = objectives || [];
    const initiativeGroups = await Promise.all(
      objectiveRows.map(async (objective) => ({
        objective,
        initiatives: await window.strategyObjectivesApi.initiatives(objective.id).catch(() => [])
      }))
    );

    const initiatives = initiativeGroups.flatMap((group) =>
      (group.initiatives || []).map((initiative) => ({
        ...initiative,
        objectiveName: group.objective?.name || ""
      }))
    );

    document.getElementById("goal-detail-overview").innerHTML = `
      <div><strong>${escapeHtml(goal.name || "-")}</strong></div>
      <div class="small text-muted">${escapeHtml(goal.statement || "")}</div>
      <div class="mt-2">Status: <span class="badge bg-label-info">${escapeHtml(goal.status || "-")}</span></div>`;

    document.getElementById("goal-detail-metrics").innerHTML = (goal.metrics || []).map((metric) =>
      `<div>${escapeHtml(metric.metricName)}: ${escapeHtml(metric.baselineValue)} -> ${escapeHtml(metric.targetValue)}</div>`
    ).join("") || "No metrics";

    document.getElementById("goal-detail-objectives").innerHTML = objectiveRows.map((objective) =>
      `<div><a href="/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(objective.id)}">${escapeHtml(objective.name || objective.id)}</a></div>`
    ).join("") || "No linked objectives";

    document.getElementById("goal-detail-execution").innerHTML = `
      <div class="mb-2">Aligned initiatives: ${summary.linkedInitiativesCount || initiatives.length || 0}</div>
      <div class="mb-2">Aligned projects: ${summary.linkedProjectsCount || 0}</div>
      <div class="small text-muted mt-2">Initiatives remain delivery-owned records. ES&amp;BP keeps only the strategy-to-delivery reference layer here.</div>
      <div class="mt-3 d-flex gap-2 flex-wrap">
        <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives">Open Delivery Initiatives</a>
        <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/projects">Open Delivery Projects</a>
      </div>
      <div class="mt-3">
        ${initiatives.length ? initiatives.map((initiative) => initiativeCard(initiative, initiative.objectiveName)).join("") : '<div class="small text-muted">No aligned initiatives linked yet.</div>'}
      </div>`;

    document.getElementById("goal-detail-evidence").innerHTML = `<div>Decision: ${escapeHtml(goal.decisionReference || "-")}</div><div>Evidence: ${escapeHtml(goal.evidenceReference || "-")}</div>`;
    document.getElementById("goal-detail-audit").innerHTML = escapeHtml(summary.auditSummary || "No audit summary.");
  }

  load().catch(() => {
    document.getElementById("goal-detail-overview").innerHTML = '<div class="alert alert-warning">Failed to load goal detail.</div>';
  });
})(window, document);
