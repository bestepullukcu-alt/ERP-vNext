/*
Implementation summary / commit notes:
- Rebuilt the Project Detail shell around the anchored Project model and post-create routing flow.
- Added explicit visibility for inherited lineage, template metadata, budget governance, and audit history.
- Audit tab now surfaces create, anchor, template, status, and budget events as a first-class timeline/table.
*/
(function (window, document) {
  "use strict";

  const projectId = window.projectDetailId;
  if (!projectId) return;

  const workbook = window.enterpriseWorkbookOptions || {};
  const ui = window.enterpriseStrategyUi || {};
  const byId = (id) => document.getElementById(id);

  function notify(message, kind) {
    if (!message) return;
    if (typeof ui.notify === "function") ui.notify(message, kind || "info");
    else if (typeof window.notify === "function") window.notify(message, kind || "info");
  }

  function getErrorMessage(err, fallback) {
    return ui.getErrorMessage?.(err, fallback) || err?.message || fallback || "Request failed.";
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function normalizeText(value) {
    return String(value || "").trim().toLowerCase().replace(/[^a-z0-9]/g, "");
  }

  function statusBadgeClass(status) {
    const normalized = normalizeText(status);
    if (normalized === "draft") return "text-bg-secondary";
    if (normalized === "planned" || normalized === "approved") return "text-bg-info";
    if (normalized === "active") return "text-bg-success";
    if (normalized === "onhold") return "text-bg-warning";
    if (normalized === "closed") return "text-bg-dark";
    return "text-bg-secondary";
  }

  function formatDate(value) {
    if (!value) return "Not set";
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return String(value);
    return parsed.toLocaleDateString();
  }

  function formatTimestamp(value) {
    if (!value) return "Unknown";
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return String(value);
    return parsed.toLocaleString();
  }

  function summarizeBudget(project) {
    if (project.budgetSummary) return project.budgetSummary;
    if (project.budgetRequired === false) return `No budget required${project.noBudgetReason ? `: ${project.noBudgetReason}` : ""}`;
    if (project.budgetRequired === true) {
      const parts = [];
      if (project.budgetAmount != null) parts.push(`${project.currencyCode || ""} ${Number(project.budgetAmount).toLocaleString()}`.trim());
      if (project.budgetType) parts.push(project.budgetType);
      if (project.budgetBasis) parts.push(project.budgetBasis);
      return parts.length ? parts.join(" | ") : "Budget required";
    }
    return "Pending budget decision";
  }

  function summaryGrid(items) {
    return `<div class="project-detail-grid">${items.map((item) => `
      <div class="project-detail-item">
        <div class="project-detail-label">${escapeHtml(item.label)}</div>
        <div class="project-detail-value">${escapeHtml(item.value || "Not set")}</div>
      </div>
    `).join("")}</div>`;
  }

  function renderHeader(project, detail) {
    byId("project-detail-title").textContent = project.projectName || "Untitled draft";
    byId("project-detail-code").textContent = project.projectId || projectId;
    byId("project-detail-status").className = `badge ${statusBadgeClass(project.status)}`;
    byId("project-detail-status").textContent = project.status || "Draft";
    byId("project-detail-subtitle").textContent = detail.traceabilitySummary || "Anchored Delivery Project";
    byId("project-detail-meta").innerHTML = `
      <span class="badge bg-label-info">Inherited Anchor</span>
      <span>${escapeHtml(project.parentInitiativeName || project.parentInitiativeId || "No initiative")}</span>
      <span class="badge bg-label-warning">${escapeHtml(project.creationMode || "Blank")}</span>
      <span>${escapeHtml(project.sourceTemplateName || "No template")}</span>
      <span class="badge bg-label-primary">${escapeHtml(project.entityScope || "No scope")}</span>
    `;
  }

  function renderOverview(project) {
    byId("project-detail-overview").innerHTML = `
      <div class="project-section-stack">
        <div class="card"><div class="card-body">
          <div class="d-flex flex-wrap gap-2 mb-3">
            <span class="badge bg-label-info">Inherited</span>
            <span class="badge bg-label-warning">Template-Driven</span>
            <span class="badge bg-label-primary">Editable</span>
          </div>
          ${summaryGrid([
            { label: "Project Name", value: project.projectName || "Untitled draft" },
            { label: "Description", value: project.description || "Not set" },
            { label: "Parent Initiative", value: project.parentInitiativeName || project.parentInitiativeId },
            { label: "Parent Objective", value: project.parentObjectiveName || project.parentObjectiveId },
            { label: "Parent Goal", value: project.parentGoalName || project.parentGoalId },
            { label: "Parent Type", value: project.parentType || "Not set" },
            { label: "EntityScope", value: project.entityScope || "Not set" },
            { label: "Creation Mode", value: project.creationMode || "Blank" },
            { label: "Project Template", value: project.sourceTemplateName || "Blank" },
            { label: "Project Owner / PM", value: workbook.userDisplayName?.(project.ownerPm) || project.ownerPm || "Not set" },
            { label: "Executive Sponsor", value: workbook.userDisplayName?.(project.sponsor) || project.sponsor || "Not set" },
            { label: "Business Owner", value: workbook.userDisplayName?.(project.businessOwner) || project.businessOwner || "Not set" },
          ])}
        </div></div>
      </div>
    `;
  }

  function renderPlanning(project) {
    byId("project-detail-planning").innerHTML = `
      <div class="card"><div class="card-body">
        ${summaryGrid([
          { label: "Status", value: project.status || "Draft" },
          { label: "Stage / Phase", value: project.phase || "Not set" },
          { label: "Delivery Type", value: project.deliveryType || "Not set" },
          { label: "Delivery Methodology", value: project.deliveryMethodology || "Not set" },
          { label: "Priority", value: project.priority || "Not set" },
          { label: "Complexity / Size", value: project.complexitySize || "Not set" },
          { label: "Start Date", value: formatDate(project.startDate) },
          { label: "End Date", value: formatDate(project.endDate) },
          { label: "Go-Live / Target Milestone", value: formatDate(project.goLiveDate) },
          { label: "Reporting Cadence", value: project.reportingCadence || "Not set" },
          { label: "Delivery Company", value: project.deliveryCompanyId || "Not set" },
          { label: "Funding / Owning Company", value: project.fundingCompanyId || "Not set" },
        ])}
      </div></div>
    `;
  }

  function renderBudget(project) {
    byId("project-detail-budget").innerHTML = `
      <div class="project-section-stack">
        <div class="card"><div class="card-body">
          ${summaryGrid([
            { label: "Budget Required", value: project.budgetRequired == null ? "Not set" : (project.budgetRequired ? "Yes" : "No") },
            { label: "Budget Summary", value: summarizeBudget(project) },
            { label: "Funding / Owning Company", value: project.fundingCompanyId || "Not set" },
            { label: "Funding Source", value: project.fundingSource || "Not set" },
            { label: "Cost Center", value: project.costCenter || "Not set" },
            { label: "Budget Owner", value: project.budgetOwner || "Not set" },
            { label: "Approval Route", value: project.approvalRoute || "Not set" },
            { label: "Financial Notes", value: project.financialNotes || "Not set" },
            { label: "No-Budget Reason", value: project.noBudgetReason || "Not set" },
          ])}
        </div></div>
        <div class="card"><div class="card-body">
          <div class="project-detail-label mb-2">Budget Workspace Scope</div>
          <div class="project-inline-note">Detailed financial planning remains outside the creation wizard and is intended for this dedicated Budget area.</div>
          ${summaryGrid([
            { label: "Versioned budget baselines", value: "Budget workspace placeholder" },
            { label: "Revisions / change requests", value: "Budget workspace placeholder" },
            { label: "Periodized allocations", value: "Budget workspace placeholder" },
            { label: "Forecast vs actual", value: "Budget workspace placeholder" },
            { label: "Cost category breakdown", value: "Budget workspace placeholder" },
            { label: "Evidence attachments", value: "Budget workspace placeholder" },
          ])}
        </div></div>
      </div>
    `;
  }

  function renderRisks(project) {
    byId("project-detail-risks").innerHTML = `
      <div class="card"><div class="card-body">
        ${summaryGrid([
          { label: "Success Metric", value: project.successMetric || "Not set" },
          { label: "Baseline", value: project.metricBaseline || "Not set" },
          { label: "Target", value: project.metricTarget || "Not set" },
          { label: "Readiness Status", value: project.readinessStatus || "Not set" },
          { label: "Risk Rating", value: project.riskRating || "Not set" },
          { label: "Overall Health / RAG", value: project.overallHealth || "Not set" },
          { label: "Dependency Flag", value: project.dependencyFlag ? "Yes" : "No" },
          { label: "Evidence Required Flag", value: project.evidenceRequiredFlag ? "Yes" : "No" },
          { label: "Compliance / Regulatory Impact", value: project.complianceRegulatoryImpact || "Not set" },
          { label: "Scope Summary", value: project.scopeSummary || "Not set" },
          { label: "Out-of-Scope Note", value: project.outOfScopeNote || "Not set" },
          { label: "Upstream Lineage", value: project.parentGoalId && project.parentObjectiveId && project.parentInitiativeId ? `${project.parentGoalId} -> ${project.parentObjectiveId} -> ${project.parentInitiativeId}` : "Not set" },
        ])}
      </div></div>
    `;
  }

  function renderEvidence(project) {
    byId("project-detail-evidence").innerHTML = `
      <div class="card"><div class="card-body">
        ${summaryGrid([
          { label: "Decision Reference", value: project.decisionReference || "Not set" },
          { label: "Evidence Reference", value: project.evidenceReference || "Not set" },
          { label: "Contribution Note", value: project.contributionNote || "Not set" },
          { label: "Source Template Type", value: project.sourceTemplateType || "Not set" },
          { label: "Source Template ID", value: project.sourceTemplateId || "Not set" },
          { label: "Source Template Version", value: project.sourceTemplateVersion == null ? "Not set" : String(project.sourceTemplateVersion) },
          { label: "Created From Library", value: project.createdFromLibrary ? "Yes" : "No" },
          { label: "Created At", value: formatTimestamp(project.createdAt) },
          { label: "Updated At", value: formatTimestamp(project.updatedAt) },
          { label: "Sync Freshness", value: project.syncFreshness || "Not set" },
        ])}
      </div></div>
    `;
  }

  function renderAudit(events) {
    byId("project-detail-audit").innerHTML = events.length
      ? `
        <div class="card"><div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-sm align-middle mb-0">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Action</th>
                  <th>Actor</th>
                  <th>Before</th>
                  <th>After</th>
                </tr>
              </thead>
              <tbody>
                ${events.map((eventRow) => `
                  <tr>
                    <td>${escapeHtml(formatTimestamp(eventRow.timestampUtc))}</td>
                    <td><span class="badge bg-label-info">${escapeHtml(eventRow.action || "-")}</span></td>
                    <td>${escapeHtml(eventRow.actor || "-")}</td>
                    <td>${escapeHtml(eventRow.beforeSummary || "-")}</td>
                    <td>${escapeHtml(eventRow.afterSummary || "-")}</td>
                  </tr>
                `).join("")}
              </tbody>
            </table>
          </div>
        </div></div>
      `
      : '<div class="card"><div class="card-body text-muted">No audit entries were recorded for this Project yet.</div></div>';
  }

  async function load() {
    try {
      const [detail, auditTrail] = await Promise.all([
        window.projectStrategyApi.get(projectId),
        window.projectStrategyApi.auditTrail(projectId).catch(() => null),
      ]);
      const project = detail?.project || {};
      const audit = Array.isArray(auditTrail) ? auditTrail : (detail?.auditTrail || []);

      renderHeader(project, detail || {});
      renderOverview(project);
      renderPlanning(project);
      renderBudget(project);
      renderRisks(project);
      renderEvidence(project);
      renderAudit(audit);
    } catch (err) {
      byId("project-detail-error").className = "alert alert-warning";
      byId("project-detail-error").textContent = getErrorMessage(err, "Project Detail could not be loaded.");
      notify(getErrorMessage(err, "Project Detail could not be loaded."), "warning");
    }
  }

  load().catch(() => {});
})(window, document);
