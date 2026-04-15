/*
Implementation summary / commit notes:
- Replaced the legacy Projects page behavior with an anchored six-step Project wizard.
- Added validation hardening for parent-first gating, locked lineage, template compatibility, field-level date rules, and budget governance on non-draft progression.
- Inherited fields: Parent Initiative anchor, Parent Objective, Parent Goal, Parent Type, and EntityScope.
- Template-driven fields: Delivery Type, Stage / initial Phase, Delivery Methodology, Complexity / Size, Readiness, Risk, scope scaffold, governance route, and budget metadata defaults.
- Budget-governed fields: Budget Required, Budget Amount, Currency, Budget Type, Budget Basis, Funding Source, Cost Center, Budget Owner, Approval Route, Financial Notes, and No-Budget Reason.
*/
(function (window, document) {
  "use strict";

  const workbook = window.enterpriseWorkbookOptions || {};
  const ui = window.enterpriseStrategyUi || {};
  const byId = (id) => document.getElementById(id);
  const wizardEl = byId("projectWizardModal");
  if (!wizardEl) return;

  const modal = window.bootstrap?.Modal ? new window.bootstrap.Modal(wizardEl) : null;
  const createUrl = "/management-governance/delivery-execution/projects/new";
  const state = {
    rows: [],
    initiatives: [],
    initiativeById: new Map(),
    selectedProjectIds: new Set(),
    templates: [],
    currentStep: 1,
    draftProjectId: "",
    draftVersion: 0,
    saving: false,
    previousParentInitiativeId: "",
    pendingTemplateAction: null,
    selectedTemplateDefaults: {},
    appliedTemplateValues: {},
    appliedParentDefaults: {},
    templateDrivenFields: new Set(),
    pageLoadError: "",
  };

  const listEls = {
    search: byId("project-search"),
    statusFilter: byId("project-filter-status"),
    createBtn: byId("project-create-btn"),
    bulkActionsToggle: byId("project-bulk-actions-toggle"),
    bulkExportCsvBtn: byId("project-bulk-export-csv"),
    bulkExportXlsxBtn: byId("project-bulk-export-xlsx"),
    bulkClearSelectionBtn: byId("project-bulk-clear-selection"),
    bulkActivateBtn: byId("project-bulk-activate"),
    bulkArchiveBtn: byId("project-bulk-archive"),
    headerRow: byId("projects-header-row"),
    body: document.querySelector("#projects-table tbody"),
    meta: byId("projects-list-meta"),
  };

  const wizardEls = {
    error: byId("project-wizard-error"),
    stepButtons: Array.from(document.querySelectorAll("#project-wizard-steps [data-step]")),
    panes: Array.from(document.querySelectorAll(".project-step-pane")),
    back: byId("project-step-back"),
    next: byId("project-step-next"),
    saveDraft: byId("project-save-draft"),
    create: byId("project-create-submit"),
    createOpen: byId("project-create-open"),
    templateHost: byId("project-template-host"),
    templateFilterNote: byId("project-template-filter-note"),
    templatePicker: byId("project-template-picker"),
    templatePreview: byId("project-template-preview"),
    templatePrefillList: byId("project-template-prefill-list"),
    templateReapply: byId("project-template-reapply"),
    budgetBanner: byId("project-budget-governance-banner"),
    budgetYesGroup: byId("project-budget-required-yes-group"),
    budgetNoGroup: byId("project-budget-required-no-group"),
    identityReview: byId("project-review-identity"),
    anchorReview: byId("project-review-anchor"),
    ownershipReview: byId("project-review-ownership"),
    planningReview: byId("project-review-planning"),
    controlsReview: byId("project-review-controls"),
    budgetReview: byId("project-review-budget"),
    blockers: byId("project-review-blockers"),
    warnings: byId("project-review-warnings"),
  };

  const fieldIds = {
    projectId: "project-id",
    parentInitiativeId: "project-parent-initiative",
    parentObjectiveName: "project-parent-objective",
    parentGoalName: "project-parent-goal",
    parentType: "project-parent-type",
    entityScope: "project-entity-scope",
    creationMode: "project-creation-mode",
    sourceTemplateId: "project-template-select",
    projectName: "project-name",
    description: "project-description",
    ownerPm: "project-owner-pm",
    sponsor: "project-executive-sponsor",
    businessOwner: "project-business-owner",
    deliveryCompanyId: "project-delivery-company",
    fundingCompanyId: "project-funding-company",
    owningFunctionDepartment: "project-owning-function",
    deliveryPartnerVendor: "project-delivery-partner",
    scopeSummary: "project-scope-summary",
    outOfScopeNote: "project-out-of-scope",
    status: "project-status",
    phase: "project-phase",
    deliveryType: "project-delivery-type",
    deliveryMethodology: "project-delivery-methodology",
    priority: "project-priority",
    complexitySize: "project-complexity",
    startDate: "project-start-date",
    endDate: "project-end-date",
    goLiveDate: "project-go-live",
    reportingCadence: "project-reporting-cadence",
    successMetric: "project-success-metric",
    metricBaseline: "project-baseline",
    metricTarget: "project-target",
    readinessStatus: "project-readiness-status",
    riskRating: "project-risk-rating",
    overallHealth: "project-health",
    complianceRegulatoryImpact: "project-compliance-impact",
    dependencyFlag: "project-dependency-flag",
    evidenceRequiredFlag: "project-evidence-required",
    budgetRequired: "project-budget-required",
    budgetAmount: "project-budget-amount",
    currencyCode: "project-currency",
    budgetType: "project-budget-type",
    budgetBasis: "project-budget-basis",
    fundingSource: "project-funding-source",
    costCenter: "project-cost-center",
    budgetOwner: "project-budget-owner",
    approvalRoute: "project-approval-route",
    financialNotes: "project-financial-notes",
    noBudgetReason: "project-no-budget-reason",
  };

  const stepFields = {
    1: ["parentInitiativeId", "creationMode", "sourceTemplateId", "projectName", "description"],
    2: ["ownerPm", "sponsor", "deliveryCompanyId", "scopeSummary"],
    3: ["status", "phase", "deliveryType", "deliveryMethodology", "priority", "startDate", "endDate", "goLiveDate"],
    4: ["readinessStatus", "riskRating"],
    5: ["budgetRequired", "budgetAmount", "currencyCode", "budgetType", "budgetBasis", "budgetOwner", "approvalRoute", "fundingCompanyId", "noBudgetReason"],
    6: [],
  };

  const fieldLabels = {
    projectName: "Project Name",
    description: "Project Description",
    ownerPm: "Project Owner / PM",
    sponsor: "Executive Sponsor",
    businessOwner: "Business Owner / Benefit Owner",
    deliveryCompanyId: "Delivery Company",
    fundingCompanyId: "Funding / Owning Company",
    owningFunctionDepartment: "Owning Function / Department",
    deliveryPartnerVendor: "Delivery Partner / Vendor",
    scopeSummary: "Scope Summary",
    outOfScopeNote: "Out-of-Scope Note",
    status: "Project Status",
    phase: "Stage / initial Phase",
    deliveryType: "Delivery Type",
    deliveryMethodology: "Delivery Methodology",
    priority: "Priority",
    complexitySize: "Complexity / Size",
    startDate: "Start Date",
    endDate: "End Date",
    goLiveDate: "Go-Live / Target Milestone",
    reportingCadence: "Reporting Cadence",
    successMetric: "Success Metric",
    metricBaseline: "Baseline",
    metricTarget: "Target",
    readinessStatus: "Readiness Status",
    riskRating: "Risk Rating",
    overallHealth: "Overall Health / RAG",
    complianceRegulatoryImpact: "Compliance / Regulatory Impact",
    dependencyFlag: "Dependency Flag",
    evidenceRequiredFlag: "Evidence Required Flag",
    budgetRequired: "Budget Required",
    budgetAmount: "Budget Amount",
    currencyCode: "Currency",
    budgetType: "Budget Type",
    budgetBasis: "Budget Basis",
    fundingSource: "Funding Source",
    costCenter: "Cost Center",
    budgetOwner: "Budget Owner",
    approvalRoute: "Approval Route",
    financialNotes: "Financial Notes",
    noBudgetReason: "No-Budget Reason",
  };

  const parentDefaultFieldMap = {
    deliveryCompanyId: (initiative) => initiative?.deliveryOwnerCompanyId || "",
    fundingCompanyId: (initiative) => initiative?.sponsoringCompanyId || "",
    sponsor: (initiative) => initiative?.executiveSponsor || "",
    owningFunctionDepartment: (initiative) => initiative?.accountableSponsorRole || "",
    reportingCadence: (initiative) => initiative?.reportingFrequency || "",
    complianceRegulatoryImpact: (initiative) => initiative?.governanceNotes || initiative?.strategyAlignmentNote || "",
    evidenceRequiredFlag: (initiative) => initiative?.evidenceReference ? "true" : "false",
  };

  const templateFieldMap = {
    projectName: (template) => template.name || "",
    description: (template) => template.description || "",
    deliveryType: (template) => template.deliveryType || "",
    phase: (template) => template.phase || "",
    deliveryMethodology: (template) => template.deliveryMethodology || "",
    complexitySize: (template) => template.complexitySize || "",
    readinessStatus: (template) => template.readinessStatus || "",
    riskRating: (template) => template.riskRating || "",
    scopeSummary: (template) => template.scopeSummaryTemplate || "",
    approvalRoute: (template) => template.approvalRoute || "",
    budgetType: (template) => template.budgetType || "",
    budgetBasis: (template) => template.budgetBasis || "",
    fundingSource: (template) => template.fundingSource || "",
    costCenter: (template) => template.costCenter || "",
  };

  const selectCatalog = {
    status: ["Draft", "Planned", "Approved", "Active", "On Hold", "Closed"],
    phase: workbook.projectStageValues || ["Discover", "Plan", "Mobilize", "Build", "Test", "Deploy", "Stabilize", "Close"],
    deliveryType: workbook.projectDeliveryValues || ["Business Change", "Technology", "Operations", "Compliance", "Transformation"],
    deliveryMethodology: workbook.deliveryMethodologyValues || ["Agile", "Waterfall", "Hybrid", "Iterative", "Kanban"],
    priority: workbook.priorityValues || ["Low", "Medium", "High", "Critical"],
    complexitySize: workbook.complexityValues || ["Low", "Medium", "High", "Enterprise"],
    reportingCadence: workbook.reportingCadenceValues || ["Weekly", "Biweekly", "Monthly", "Quarterly"],
    readinessStatus: workbook.readinessValues || ["Not Started", "In Progress", "Ready", "Blocked"],
    riskRating: workbook.complexityRiskScale || ["Low", "Medium", "High", "Critical"],
    overallHealth: workbook.healthValues || ["Green", "Amber", "Red"],
    currencyCode: workbook.currencyOptions?.() || ["USD", "EUR", "GBP", "TRY"],
  };

  const nonDraftStatuses = new Set(["planned", "approved", "active", "onhold", "closed"]);

  function notify(message, kind) {
    if (!message) return;
    const normalized = kind === "danger" ? "error" : (kind || "info");
    if (typeof ui.notify === "function") ui.notify(message, normalized);
    else if (typeof window.notify === "function") window.notify(message, normalized);
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

  function isNonDraftStatus(status) {
    return nonDraftStatuses.has(normalizeText(status));
  }

  function unique(values) {
    return [...new Set((values || []).filter(Boolean))];
  }

  function normalizeOption(item) {
    if (item == null) return null;
    if (typeof item === "string") return { value: item, label: item };
    const value = String(item.value ?? item.id ?? item.ownerId ?? item.companyId ?? item.code ?? item.name ?? item.label ?? "").trim();
    if (!value) return null;
    const label = String(item.label ?? item.displayName ?? item.name ?? item.companyName ?? item.text ?? value).trim();
    return { value, label };
  }

  function fillSelect(selectEl, items, placeholder, keepCurrent) {
    if (!selectEl) return;
    const current = keepCurrent ? String(selectEl.value || "") : "";
    const options = [];
    const seen = new Set();
    (items || []).forEach((item) => {
      const normalized = normalizeOption(item);
      if (!normalized || seen.has(normalized.value.toLowerCase())) return;
      seen.add(normalized.value.toLowerCase());
      options.push(normalized);
    });
    selectEl.innerHTML = "";
    if (typeof placeholder === "string") {
      const option = document.createElement("option");
      option.value = "";
      option.textContent = placeholder;
      selectEl.appendChild(option);
    }
    options.forEach((optionValue) => {
      const option = document.createElement("option");
      option.value = optionValue.value;
      option.textContent = optionValue.label;
      selectEl.appendChild(option);
    });
    if (current && options.some((item) => item.value === current)) selectEl.value = current;
  }

  function inputValue(id) {
    return String(byId(fieldIds[id])?.value || "").trim();
  }

  function setInputValue(id, value) {
    const el = byId(fieldIds[id]);
    if (!el) return;
    el.value = value == null ? "" : value;
  }

  function normalizeFieldValue(field, value) {
    if (field === "dependencyFlag" || field === "evidenceRequiredFlag") {
      if (value === true) return "true";
      if (value === false) return "false";
      const normalized = String(value || "").trim().toLowerCase();
      return normalized === "true" ? "true" : "false";
    }
    if (field === "budgetRequired") {
      if (value === true) return "true";
      if (value === false) return "false";
      const normalized = String(value || "").trim().toLowerCase();
      return normalized === "true" || normalized === "false" ? normalized : "";
    }
    if (field === "budgetAmount") {
      if (value == null || value === "") return "";
      const parsed = Number(value);
      return Number.isFinite(parsed) ? String(parsed) : "";
    }
    return String(value || "").trim();
  }

  function parseNullableBoolean(value) {
    const normalized = String(value || "").trim().toLowerCase();
    if (normalized === "true") return true;
    if (normalized === "false") return false;
    return null;
  }

  function parseNullableNumber(value) {
    const raw = String(value || "").trim();
    if (!raw) return null;
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : null;
  }

  function formatDate(value) {
    if (!value) return "Not set";
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return String(value);
    return parsed.toLocaleDateString();
  }

  function formatPeriod(startDate, endDate) {
    const start = formatDate(startDate);
    const end = formatDate(endDate);
    if (start === "Not set" && end === "Not set") return "No period";
    if (start === "Not set") return `Until ${end}`;
    if (end === "Not set") return `From ${start}`;
    return `${start} to ${end}`;
  }

  function formatInitiativeOptionLabel(initiative) {
    const name = initiative?.initiativeName || "Untitled Initiative";
    const type = getInitiativeParentType(initiative) || "No type";
    const entityScope = String(initiative?.entityScope || "").trim() || "No entity scope";
    const period = formatPeriod(initiative?.startDate, initiative?.endDate);
    return `${name} | ${type} | ${entityScope} | ${period}`;
  }

  function summarizeBudget(row) {
    if (row.budgetSummary) return row.budgetSummary;
    if (row.budgetRequired === false) return `No budget required${row.noBudgetReason ? `: ${row.noBudgetReason}` : ""}`;
    if (row.budgetRequired === true) {
      const parts = [];
      if (row.budgetAmount != null) parts.push(`${row.currencyCode || ""} ${Number(row.budgetAmount).toLocaleString()}`.trim());
      if (row.budgetType) parts.push(row.budgetType);
      if (row.budgetBasis) parts.push(row.budgetBasis);
      return parts.length ? parts.join(" | ") : "Budget required";
    }
    return "Pending budget decision";
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

  function renderSummaryList(items) {
    return `<div class="project-summary-list">${items.map((item) => `<div><strong>${escapeHtml(item.label)}:</strong> ${escapeHtml(item.value || "Not set")}</div>`).join("")}</div>`;
  }

  function showWizardError(message) {
    if (!wizardEls.error) return;
    if (!message) {
      wizardEls.error.className = "d-none";
      wizardEls.error.innerHTML = "";
      return;
    }
    wizardEls.error.className = "alert alert-danger mb-3";
    wizardEls.error.textContent = message;
  }

  function clearFieldError(fieldId) {
    const el = byId(fieldIds[fieldId] || fieldId);
    if (!el) return;
    el.classList.remove("is-invalid");
    const feedback = el.parentElement?.querySelector(".invalid-feedback[data-field-feedback]");
    if (feedback) feedback.remove();
  }

  function setFieldError(fieldId, messages) {
    const el = byId(fieldIds[fieldId] || fieldId);
    if (!el) return;
    clearFieldError(fieldId);
    if (!(messages || []).length) return;
    el.classList.add("is-invalid");
    const feedback = document.createElement("div");
    feedback.className = "invalid-feedback";
    feedback.dataset.fieldFeedback = "true";
    feedback.textContent = messages[0];
    el.parentElement?.appendChild(feedback);
  }

  function clearValidation() {
    Object.keys(fieldIds).forEach(clearFieldError);
    showWizardError("");
  }

  function getSelectedInitiative() {
    return state.initiativeById.get(inputValue("parentInitiativeId")) || null;
  }

  function getInitiativeParentType(initiative) {
    return String(initiative?.type || initiative?.normalizedType || "").trim();
  }

  function getInitiativeParentTypeFilter(initiative) {
    return String(initiative?.normalizedType || initiative?.type || "").trim();
  }

  function getSelectedTemplate() {
    const selectedId = inputValue("sourceTemplateId");
    return state.templates.find((item) => item.templateId === selectedId) || null;
  }

  function computeParentDefaults(initiative) {
    const defaults = {};
    Object.entries(parentDefaultFieldMap).forEach(([field, resolver]) => {
      defaults[field] = normalizeFieldValue(field, resolver(initiative));
    });
    return defaults;
  }

  function computeTemplateDefaults(template) {
    const defaults = {};
    Object.entries(templateFieldMap).forEach(([field, resolver]) => {
      defaults[field] = normalizeFieldValue(field, resolver(template));
    });
    return defaults;
  }

  function showDefaultsPreview(defaults) {
    const entries = Object.entries(defaults).filter(([, value]) => String(value || "").trim());
    wizardEls.templatePreview?.classList.toggle("d-none", !entries.length);
    if (!wizardEls.templatePrefillList) return;
    wizardEls.templatePrefillList.innerHTML = entries.length
      ? entries.map(([field, value]) => `<li><strong>${escapeHtml(fieldLabels[field] || field)}:</strong> ${escapeHtml(value)}</li>`).join("")
      : "";
  }

  function getConflictFields(defaults, previousApplied, fieldsFilter) {
    const conflicts = [];
    Object.entries(defaults || {}).forEach(([field, nextValue]) => {
      if (fieldsFilter && !fieldsFilter.includes(field)) return;
      const nextNormalized = normalizeFieldValue(field, nextValue);
      const current = normalizeFieldValue(field, byId(fieldIds[field])?.value || "");
      const previous = normalizeFieldValue(field, previousApplied[field] || "");
      if (!nextNormalized) return;
      if (!current || current === previous || current === nextNormalized) return;
      conflicts.push(field);
    });
    return conflicts;
  }

  function applyDefaults(defaults, previousApplied, options) {
    const overwriteFields = new Set(options?.overwriteFields || []);
    const applied = {};
    Object.entries(defaults || {}).forEach(([field, nextValue]) => {
      const el = byId(fieldIds[field]);
      if (!el) return;
      const current = normalizeFieldValue(field, el.value);
      const previous = normalizeFieldValue(field, previousApplied[field] || "");
      const nextNormalized = normalizeFieldValue(field, nextValue);
      const shouldAutoApply = !current || current === previous || overwriteFields.has(field);
      if (!shouldAutoApply) return;
      el.value = nextNormalized;
      applied[field] = nextNormalized;
      clearFieldError(field);
    });
    return applied;
  }

  function confirmOverwrite(title, fields, extraText) {
    if (!fields.length) return true;
    const message = `${title}\n\n${fields.map((field) => `- ${fieldLabels[field] || field}`).join("\n")}${extraText ? `\n\n${extraText}` : ""}`;
    return window.confirm(message);
  }

  function setPendingTemplateAction(action) {
    state.pendingTemplateAction = action || null;
  }

  function renderTemplatePicker() {
    if (!wizardEls.templatePicker) return;
    const selectedId = inputValue("sourceTemplateId");
    if (!state.templates.length) {
      wizardEls.templatePicker.innerHTML = `
        <div class="project-step-copy">
          No Project Templates match the selected Parent Initiative type. Use Blank mode or create a compatible template.
        </div>`;
      return;
    }

    wizardEls.templatePicker.innerHTML = state.templates.map((template) => `
      <button type="button" class="project-template-card ${template.templateId === selectedId ? "active" : ""}" data-template-id="${escapeHtml(template.templateId)}">
        <div class="project-template-card-title">${escapeHtml(template.name)}</div>
        <div class="project-template-card-copy">${escapeHtml(template.description || "Compatible active template")}</div>
        <div class="project-template-badges">
          <span class="badge bg-label-info">${escapeHtml(template.parentType || "No parent type")}</span>
          <span class="badge bg-label-primary">${escapeHtml(template.entityScope || "No scope")}</span>
          <span class="badge bg-label-success">${escapeHtml(template.lifecycleStatus || "Active")}</span>
        </div>
      </button>
    `).join("");

    wizardEls.templatePicker.querySelectorAll("[data-template-id]").forEach((button) => {
      button.addEventListener("click", () => {
        const selectEl = byId(fieldIds.sourceTemplateId);
        if (!selectEl) return;
        selectEl.value = button.dataset.templateId || "";
        handleTemplateSelectionChange();
      });
    });
  }

  function updateTemplateBadges() {
    Object.keys(templateFieldMap).forEach((field) => {
      const label = document.querySelector(`label[for="${fieldIds[field]}"]`);
      if (!label) return;
      let badge = label.querySelector(".project-template-live-badge");
      const active = state.templateDrivenFields.has(field);
      if (active && !badge) {
        badge = document.createElement("span");
        badge.className = "badge bg-label-warning ms-1 project-template-live-badge";
        badge.textContent = "Template Default";
        label.appendChild(badge);
      }
      if (!active && badge) badge.remove();
      byId(fieldIds[field])?.classList.toggle("project-templated-field", active);
    });
  }

  function clearTemplateDefaults(options) {
    const hadTemplate = Boolean(inputValue("sourceTemplateId") || Object.keys(state.appliedTemplateValues).length);
    state.selectedTemplateDefaults = {};
    state.appliedTemplateValues = {};
    state.templateDrivenFields.clear();
    showDefaultsPreview({});
    updateTemplateBadges();
    wizardEls.templateReapply && (wizardEls.templateReapply.disabled = true);
    if (options?.markCleared && hadTemplate) setPendingTemplateAction("Cleared");
  }

  function applySelectedTemplateDefaults(forceReapply) {
    const template = getSelectedTemplate();
    if (!template) {
      clearTemplateDefaults({ markCleared: forceReapply });
      renderTemplatePicker();
      return;
    }

    const defaults = computeTemplateDefaults(template);
    state.selectedTemplateDefaults = defaults;
    showDefaultsPreview(defaults);
    const conflicts = getConflictFields(defaults, state.appliedTemplateValues);
    let overwriteFields = [];

    if (conflicts.length) {
      const confirmed = confirmOverwrite(
        forceReapply
          ? "Reapplying this template will overwrite edited fields."
          : "Applying this template will overwrite edited fields unless you keep the current values.",
        conflicts,
        forceReapply
          ? "Choose OK to reapply the template defaults to these fields."
          : "Choose OK to replace these values with the new template defaults, or Cancel to keep your current values and only fill safe blanks."
      );
      if (confirmed) overwriteFields = conflicts;
    }

    const applied = applyDefaults(defaults, state.appliedTemplateValues, { overwriteFields });
    state.appliedTemplateValues = applied;
    state.templateDrivenFields = new Set(Object.keys(applied));
    updateTemplateBadges();
    syncBudgetControls();
    wizardEls.templateReapply && (wizardEls.templateReapply.disabled = false);
    if (forceReapply) setPendingTemplateAction("Reapplied");
    else if (!state.pendingTemplateAction || state.pendingTemplateAction === "Cleared") setPendingTemplateAction("Applied");
    renderTemplatePicker();
    if (state.currentStep === 6) renderReview();
  }

  async function loadCompatibleTemplates() {
    const initiative = getSelectedInitiative();
    state.templates = [];
    fillSelect(byId(fieldIds.sourceTemplateId), [], "Select compatible template", false);
    if (!initiative) {
      if (wizardEls.templateFilterNote) wizardEls.templateFilterNote.textContent = "Select Parent Initiative first.";
      renderTemplatePicker();
      return;
    }

    const parentType = getInitiativeParentTypeFilter(initiative);
    if (!parentType) {
      if (wizardEls.templateFilterNote) wizardEls.templateFilterNote.textContent = "Compatible templates will load after Parent Initiative derivation completes.";
      renderTemplatePicker();
      return;
    }

    try {
      if (!window.projectStrategyApi?.compatibleTemplates) {
        throw new Error("Compatible template service is unavailable.");
      }
      state.templates = await window.projectStrategyApi.compatibleTemplates(parentType, initiative.entityScope || "") || [];
      fillSelect(
        byId(fieldIds.sourceTemplateId),
        state.templates.map((template) => ({ value: template.templateId, label: template.name })),
        state.templates.length ? "Select compatible template" : "No compatible templates found",
        false
      );
      renderTemplatePicker();
      if (wizardEls.templateFilterNote) {
        wizardEls.templateFilterNote.textContent = state.templates.length
          ? `Showing Project Templates whose type matches Parent Initiative type "${parentType}".`
          : "No Project Templates match the selected Parent Initiative type. Use Blank mode or create a compatible template.";
      }
    } catch (err) {
      renderTemplatePicker();
      if (wizardEls.templateFilterNote) wizardEls.templateFilterNote.textContent = "Template catalog is unavailable right now.";
      notify(getErrorMessage(err, "Compatible Project Templates could not be loaded."), "error");
    }
  }

  function syncCreationMode() {
    const initiativeSelected = Boolean(inputValue("parentInitiativeId"));
    const creationModeEl = byId(fieldIds.creationMode);
    const templateSelectEl = byId(fieldIds.sourceTemplateId);
    const isTemplateMode = inputValue("creationMode") === "Template";

    creationModeEl.disabled = !initiativeSelected;
    wizardEls.templateHost?.classList.toggle("d-none", !isTemplateMode);
    templateSelectEl.disabled = !initiativeSelected || !isTemplateMode || !state.templates.length;
    wizardEls.templateReapply && (wizardEls.templateReapply.disabled = !initiativeSelected || !isTemplateMode || !getSelectedTemplate());

    if (!initiativeSelected) {
      creationModeEl.value = "Blank";
      templateSelectEl.value = "";
      clearTemplateDefaults({ markCleared: false });
    }

    if (!isTemplateMode) {
      const hadTemplate = Boolean(inputValue("sourceTemplateId"));
      templateSelectEl.value = "";
      clearTemplateDefaults({ markCleared: hadTemplate });
      renderTemplatePicker();
    }
  }

  function syncAnchor() {
    const initiative = getSelectedInitiative();
    setInputValue("parentObjectiveName", initiative?.parentObjectiveName || "");
    setInputValue("parentGoalName", initiative?.parentGoalName || "");
    setInputValue("parentType", getInitiativeParentType(initiative));
    setInputValue("entityScope", initiative?.entityScope || "");

    if (!initiative) {
      clearTemplateDefaults({ markCleared: false });
      state.templates = [];
      fillSelect(byId(fieldIds.sourceTemplateId), [], "Select compatible template", false);
      renderTemplatePicker();
      if (wizardEls.templateFilterNote) wizardEls.templateFilterNote.textContent = "Select Parent Initiative first.";
    }

    syncCreationMode();
  }

  function applyParentDefaults(initiative, forceOverwrite) {
    const defaults = computeParentDefaults(initiative);
    const conflicts = getConflictFields(defaults, state.appliedParentDefaults, [
      "deliveryCompanyId",
      "fundingCompanyId",
      "sponsor",
      "owningFunctionDepartment",
      "reportingCadence",
      "complianceRegulatoryImpact",
      "evidenceRequiredFlag",
    ]);

    let overwriteFields = [];
    if (conflicts.length && forceOverwrite) {
      overwriteFields = conflicts;
    }

    applyDefaults(defaults, state.appliedParentDefaults, { overwriteFields });
    state.appliedParentDefaults = defaults;
  }

  function syncBudgetControls() {
    const budgetRequired = parseNullableBoolean(inputValue("budgetRequired"));
    const yesFields = ["budgetAmount", "currencyCode", "budgetType", "budgetBasis", "budgetOwner", "approvalRoute", "fundingSource", "costCenter"];
    const noFields = ["noBudgetReason"];

    yesFields.forEach((field) => {
      const el = byId(fieldIds[field]);
      if (!el) return;
      const disabled = budgetRequired !== true;
      el.disabled = disabled;
      el.closest(".col-12")?.classList.toggle("project-field-muted", disabled);
    });

    noFields.forEach((field) => {
      const el = byId(fieldIds[field]);
      if (!el) return;
      const disabled = budgetRequired !== false;
      el.disabled = disabled;
      el.closest(".col-12")?.classList.toggle("project-field-muted", disabled);
    });

    wizardEls.budgetYesGroup?.classList.toggle("d-none", budgetRequired !== true);
    wizardEls.budgetNoGroup?.classList.toggle("d-none", budgetRequired !== false);
    renderBudgetGovernanceBanner();
  }

  function updateFooter() {
    wizardEls.back.classList.toggle("d-none", state.currentStep === 1);
    wizardEls.next.classList.toggle("d-none", state.currentStep === 6);
    wizardEls.create.classList.toggle("d-none", state.currentStep !== 6);
    wizardEls.createOpen.classList.toggle("d-none", state.currentStep !== 6);
    wizardEls.saveDraft.disabled = state.saving;
    wizardEls.next.disabled = state.saving;
    wizardEls.back.disabled = state.saving;
    wizardEls.create.disabled = state.saving;
    wizardEls.createOpen.disabled = state.saving;
    wizardEls.saveDraft.textContent = state.saving ? "Saving..." : (state.draftProjectId ? "Update Draft" : "Save Draft");

    wizardEls.stepButtons.forEach((button) => {
      const step = Number(button.dataset.step);
      button.classList.toggle("active", step === state.currentStep);
      button.classList.toggle("completed", step < state.currentStep);
    });
    wizardEls.panes.forEach((pane) => pane.classList.toggle("d-none", Number(pane.dataset.step) !== state.currentStep));
  }

  function goToStep(step) {
    state.currentStep = Math.max(1, Math.min(6, Number(step) || 1));
    updateFooter();
    if (state.currentStep === 6) renderReview();
  }

  function collectFormData() {
    const selectedTemplate = getSelectedTemplate();
    return {
      projectId: state.draftProjectId || inputValue("projectId"),
      parentInitiativeId: inputValue("parentInitiativeId"),
      parentInitiativeName: getSelectedInitiative()?.initiativeName || "",
      parentObjectiveName: inputValue("parentObjectiveName"),
      parentGoalName: inputValue("parentGoalName"),
      parentType: inputValue("parentType"),
      entityScope: inputValue("entityScope"),
      creationMode: inputValue("creationMode") || "Blank",
      sourceTemplateId: inputValue("sourceTemplateId") || null,
      sourceTemplateName: selectedTemplate?.name || null,
      sourceTemplateVersion: selectedTemplate?.version ?? null,
      sourceTemplateType: selectedTemplate ? "ProjectTemplate" : null,
      createdFromLibrary: Boolean(selectedTemplate),
      templateApplicationMode: state.pendingTemplateAction,
      projectName: inputValue("projectName"),
      description: inputValue("description"),
      ownerPm: inputValue("ownerPm"),
      sponsor: inputValue("sponsor"),
      businessOwner: inputValue("businessOwner"),
      deliveryCompanyId: inputValue("deliveryCompanyId"),
      fundingCompanyId: inputValue("fundingCompanyId") || null,
      owningFunctionDepartment: inputValue("owningFunctionDepartment"),
      deliveryPartnerVendor: inputValue("deliveryPartnerVendor"),
      scopeSummary: inputValue("scopeSummary"),
      outOfScopeNote: inputValue("outOfScopeNote"),
      status: inputValue("status") || "Draft",
      phase: inputValue("phase"),
      deliveryType: inputValue("deliveryType"),
      deliveryMethodology: inputValue("deliveryMethodology"),
      priority: inputValue("priority"),
      complexitySize: inputValue("complexitySize"),
      startDate: inputValue("startDate") || null,
      endDate: inputValue("endDate") || null,
      goLiveDate: inputValue("goLiveDate") || null,
      reportingCadence: inputValue("reportingCadence"),
      successMetric: inputValue("successMetric"),
      metricBaseline: inputValue("metricBaseline"),
      metricTarget: inputValue("metricTarget"),
      readinessStatus: inputValue("readinessStatus"),
      riskRating: inputValue("riskRating"),
      overallHealth: inputValue("overallHealth"),
      complianceRegulatoryImpact: inputValue("complianceRegulatoryImpact"),
      dependencyFlag: parseNullableBoolean(inputValue("dependencyFlag")) === true,
      evidenceRequiredFlag: parseNullableBoolean(inputValue("evidenceRequiredFlag")) === true,
      budgetRequired: parseNullableBoolean(inputValue("budgetRequired")),
      budgetAmount: parseNullableNumber(inputValue("budgetAmount")),
      currencyCode: inputValue("currencyCode"),
      budgetType: inputValue("budgetType"),
      budgetBasis: inputValue("budgetBasis"),
      fundingSource: inputValue("fundingSource"),
      costCenter: inputValue("costCenter"),
      budgetOwner: inputValue("budgetOwner"),
      approvalRoute: inputValue("approvalRoute"),
      financialNotes: inputValue("financialNotes"),
      noBudgetReason: inputValue("noBudgetReason"),
    };
  }

  function addError(errors, field, message) {
    if (!errors[field]) errors[field] = [];
    if (!errors[field].includes(message)) errors[field].push(message);
  }

  function getBudgetGovernanceErrors(payload, mode) {
    const errors = {};
    const effectiveStatus = mode === "draft" ? "Draft" : (payload.status || "Draft");

    if (!isNonDraftStatus(effectiveStatus)) return errors;

    if (payload.budgetRequired == null) addError(errors, "budgetRequired", "Budget Required must be set before the Project can move beyond Draft.");

    if (payload.budgetRequired === true) {
      if (payload.budgetAmount == null || payload.budgetAmount <= 0) addError(errors, "budgetAmount", "Budget Amount is required when Budget Required is Yes.");
      if (!payload.currencyCode) addError(errors, "currencyCode", "Currency is required when Budget Required is Yes.");
      if (!payload.budgetType) addError(errors, "budgetType", "Budget Type is required when Budget Required is Yes.");
      if (!payload.budgetBasis) addError(errors, "budgetBasis", "Budget Basis is required when Budget Required is Yes.");
      if (!payload.budgetOwner) addError(errors, "budgetOwner", "Budget Owner is required when Budget Required is Yes.");
      if (!payload.approvalRoute) addError(errors, "approvalRoute", "Approval Route is required when Budget Required is Yes.");
    }

    if (payload.budgetRequired === false && !payload.noBudgetReason) {
      addError(errors, "noBudgetReason", "No-Budget Reason is required when Budget Required is No.");
    }

    return errors;
  }

  function renderBudgetGovernanceBanner() {
    if (!wizardEls.budgetBanner) return;

    const payload = collectFormData();
    const status = payload.status || "Draft";
    const governanceErrors = getBudgetGovernanceErrors(payload, "final");
    const messages = Object.values(governanceErrors).flat();

    if (!isNonDraftStatus(status)) {
      wizardEls.budgetBanner.className = "alert alert-info mb-3";
      wizardEls.budgetBanner.textContent = "Draft Projects may save partial financial data. Budget governance becomes mandatory before status progression beyond Draft.";
      return;
    }

    if (!messages.length) {
      wizardEls.budgetBanner.className = "alert alert-success mb-3";
      wizardEls.budgetBanner.textContent = "Budgeting & Financial Governance is complete for the selected non-draft status.";
      return;
    }

    wizardEls.budgetBanner.className = "alert alert-warning mb-3";
    wizardEls.budgetBanner.innerHTML = `<strong>Budget governance warning:</strong> ${escapeHtml(messages[0])}`;
  }

  function validatePayload(payload, mode) {
    const errors = {};
    const effectiveStatus = mode === "draft" ? "Draft" : (payload.status || "Draft");

    if (!payload.parentInitiativeId) addError(errors, "parentInitiativeId", "Select a Parent Initiative before saving or creating a Project.");
    if (payload.creationMode === "Template" && !payload.sourceTemplateId) addError(errors, "sourceTemplateId", "Select a compatible Project Template or switch back to Blank mode.");
    if (payload.startDate && payload.endDate && payload.endDate < payload.startDate) addError(errors, "endDate", "End Date must be on or after Start Date.");
    if (payload.startDate && payload.goLiveDate && payload.goLiveDate < payload.startDate) addError(errors, "goLiveDate", "Go-Live / Target Milestone must be on or after Start Date.");

    if (mode !== "draft") {
      if (!payload.projectName) addError(errors, "projectName", "Project Name is required before the Project can be created.");
      if (!payload.description) addError(errors, "description", "Project Description is required before the Project can be created.");
    }

    if (!isNonDraftStatus(effectiveStatus)) return errors;

    if (!payload.ownerPm) addError(errors, "ownerPm", "Project Owner / PM is required when status is not Draft.");
    if (!payload.sponsor) addError(errors, "sponsor", "Executive Sponsor is required when status is not Draft.");
    if (!payload.deliveryCompanyId) addError(errors, "deliveryCompanyId", "Delivery Company is required when status is not Draft.");
    if (!payload.scopeSummary) addError(errors, "scopeSummary", "Scope Summary is required when status is not Draft.");
    if (!payload.phase) addError(errors, "phase", "Stage / Phase is required when status is not Draft.");
    if (!payload.deliveryType) addError(errors, "deliveryType", "Delivery Type is required when status is not Draft.");
    if (!payload.deliveryMethodology) addError(errors, "deliveryMethodology", "Delivery Methodology is required when status is not Draft.");
    if (!payload.priority) addError(errors, "priority", "Priority is required when status is not Draft.");
    if (!payload.startDate) addError(errors, "startDate", "Start Date is required when status is not Draft.");
    if (!payload.endDate) addError(errors, "endDate", "End Date is required when status is not Draft.");
    if (!payload.readinessStatus) addError(errors, "readinessStatus", "Readiness Status is required when status is not Draft.");
    if (!payload.riskRating) addError(errors, "riskRating", "Risk Rating is required when status is not Draft.");
    Object.entries(getBudgetGovernanceErrors(payload, mode)).forEach(([field, messages]) => {
      (messages || []).forEach((message) => addError(errors, field, message));
    });

    return errors;
  }

  function getStepErrors(step, mode) {
    const payload = collectFormData();
    const fullErrors = validatePayload(payload, mode);
    if (step === 6) return fullErrors;
    const result = {};
    (stepFields[step] || []).forEach((field) => {
      if (fullErrors[field]?.length) result[field] = fullErrors[field];
    });
    if (step === 3 && fullErrors.goLiveDate?.length) result.goLiveDate = fullErrors.goLiveDate;
    return result;
  }

  function applyValidation(errors, focusField) {
    clearValidation();
    const keys = Object.keys(errors || {});
    keys.forEach((field) => setFieldError(field, errors[field]));
    if (keys.length && focusField) byId(fieldIds[keys[0]])?.focus();
    return keys.length === 0;
  }

  function buildWarnings(payload) {
    const warnings = [];
    if (!payload.projectName) warnings.push("Project Name is still blank. Draft save is allowed, but final create should complete identity.");
    if (!payload.description) warnings.push("Project Description is still blank. Draft save is allowed, but final create should complete identity.");
    if (!isNonDraftStatus(payload.status || "Draft")) warnings.push("Draft status allows partial completion. Non-draft statuses will enforce full operating and budget controls.");
    if (payload.creationMode === "Blank") warnings.push("Blank mode keeps all editable fields user-authored. Only lineage fields are inherited.");
    if (payload.creationMode === "Template" && !payload.sourceTemplateId) warnings.push("Template mode is selected without a compatible Project Template.");
    if (payload.creationMode === "Template" && payload.sourceTemplateId) warnings.push("Template defaults apply only to allowed editable fields. Locked lineage and system values are never overridden.");
    const budgetWarnings = Object.values(getBudgetGovernanceErrors(payload, "final")).flat();
    if (budgetWarnings.length) warnings.push(...budgetWarnings);
    return warnings;
  }

  function renderReview() {
    const payload = collectFormData();
    const blockers = validatePayload(payload, "final");
    const warnings = buildWarnings(payload);
    const template = getSelectedTemplate();

    wizardEls.identityReview.innerHTML = renderSummaryList([
      { label: "Project ID", value: payload.projectId || "Assigned on first save" },
      { label: "Project Name", value: payload.projectName || "Untitled draft" },
      { label: "Description", value: payload.description || "Not set" },
      { label: "Creation Mode", value: payload.creationMode },
      { label: "Template", value: template?.name || "Blank" },
    ]);

    wizardEls.anchorReview.innerHTML = renderSummaryList([
      { label: "Parent Initiative", value: getSelectedInitiative()?.initiativeName || payload.parentInitiativeId || "Not selected" },
      { label: "Parent Objective", value: payload.parentObjectiveName || "Derived after anchor selection" },
      { label: "Parent Goal", value: payload.parentGoalName || "Derived after anchor selection" },
      { label: "Parent Type", value: payload.parentType || "Derived after anchor selection" },
      { label: "EntityScope", value: payload.entityScope || "Derived after anchor selection" },
    ]);

    wizardEls.ownershipReview.innerHTML = renderSummaryList([
      { label: "Project Owner / PM", value: payload.ownerPm || "Not set" },
      { label: "Executive Sponsor", value: payload.sponsor || "Not set" },
      { label: "Business Owner", value: payload.businessOwner || "Not set" },
      { label: "Delivery Company", value: payload.deliveryCompanyId || "Not set" },
      { label: "Scope Summary", value: payload.scopeSummary || "Not set" },
    ]);

    wizardEls.planningReview.innerHTML = renderSummaryList([
      { label: "Status", value: payload.status },
      { label: "Stage / Phase", value: payload.phase || "Not set" },
      { label: "Delivery Type", value: payload.deliveryType || "Not set" },
      { label: "Methodology", value: payload.deliveryMethodology || "Not set" },
      { label: "Priority", value: payload.priority || "Not set" },
      { label: "Timeline", value: `${formatDate(payload.startDate)} to ${formatDate(payload.endDate)}` },
    ]);

    wizardEls.controlsReview.innerHTML = renderSummaryList([
      { label: "Readiness", value: payload.readinessStatus || "Not set" },
      { label: "Risk", value: payload.riskRating || "Not set" },
      { label: "Compliance / Regulatory Impact", value: payload.complianceRegulatoryImpact || "Not set" },
      { label: "Evidence Required", value: payload.evidenceRequiredFlag ? "Yes" : "No" },
      { label: "Approval Route", value: payload.approvalRoute || "Not set" },
      { label: "Success Metric", value: payload.successMetric || "Not set" },
    ]);

    wizardEls.budgetReview.innerHTML = renderSummaryList([
      { label: "Budget Required", value: payload.budgetRequired == null ? "Not set" : (payload.budgetRequired ? "Yes" : "No") },
      { label: "Budget Summary", value: summarizeBudget(payload) },
      { label: "Funding / Owning Company", value: payload.fundingCompanyId || "Not set" },
      { label: "Funding Source", value: payload.fundingSource || "Not set" },
      { label: "Cost Center", value: payload.costCenter || "Not set" },
      { label: "Budget Owner", value: payload.budgetOwner || "Not set" },
      { label: "Approval Route", value: payload.approvalRoute || "Not set" },
    ]);

    const blockerItems = Object.values(blockers).flat();
    wizardEls.blockers.innerHTML = blockerItems.length
      ? blockerItems.map((message) => `<li>${escapeHtml(message)}</li>`).join("")
      : '<li class="text-success">No blocking validations for the selected status.</li>';

    wizardEls.warnings.innerHTML = warnings.length
      ? warnings.map((message) => `<li>${escapeHtml(message)}</li>`).join("")
      : '<li class="text-muted">No warnings.</li>';
  }

  function hydrateReferenceOptions() {
    const users = workbook.userOptions?.() || unique([
      ...state.rows.map((row) => row.ownerPm),
      ...state.rows.map((row) => row.sponsor),
      ...state.rows.map((row) => row.businessOwner),
      ...state.initiatives.map((row) => row.executiveSponsor),
    ]);
    const companies = workbook.companyOptions?.() || unique([
      ...state.rows.map((row) => row.deliveryCompanyId),
      ...state.rows.map((row) => row.fundingCompanyId),
      ...state.initiatives.map((row) => row.deliveryOwnerCompanyId),
      ...state.initiatives.map((row) => row.sponsoringCompanyId),
    ]);

    fillSelect(byId(fieldIds.ownerPm), users, "Select owner / PM", true);
    fillSelect(byId(fieldIds.sponsor), users, "Select executive sponsor", true);
    fillSelect(byId(fieldIds.businessOwner), users, "Select business owner", true);
    fillSelect(byId(fieldIds.budgetOwner), users, "Select budget owner", true);
    fillSelect(byId(fieldIds.deliveryCompanyId), companies, "Select delivery company", true);
    fillSelect(byId(fieldIds.fundingCompanyId), companies, "Select funding / owning company", true);

    fillSelect(byId(fieldIds.status), selectCatalog.status, null, true);
    fillSelect(byId(fieldIds.phase), selectCatalog.phase, "Select stage / phase", true);
    fillSelect(byId(fieldIds.deliveryType), selectCatalog.deliveryType, "Select delivery type", true);
    fillSelect(byId(fieldIds.deliveryMethodology), selectCatalog.deliveryMethodology, "Select methodology", true);
    fillSelect(byId(fieldIds.priority), selectCatalog.priority, "Select priority", true);
    fillSelect(byId(fieldIds.complexitySize), selectCatalog.complexitySize, "Select complexity / size", true);
    fillSelect(byId(fieldIds.reportingCadence), selectCatalog.reportingCadence, "Select cadence", true);
    fillSelect(byId(fieldIds.readinessStatus), selectCatalog.readinessStatus, "Select readiness", true);
    fillSelect(byId(fieldIds.riskRating), selectCatalog.riskRating, "Select risk", true);
    fillSelect(byId(fieldIds.overallHealth), selectCatalog.overallHealth, "Select health", true);
    fillSelect(byId(fieldIds.currencyCode), selectCatalog.currencyCode, "Select currency", true);
    fillSelect(listEls.statusFilter, unique([...selectCatalog.status, ...state.rows.map((row) => row.status)]), "All statuses", true);
  }

  function hydrateInitiativeOptions() {
    fillSelect(
      byId(fieldIds.parentInitiativeId),
      state.initiatives.map((initiative) => ({
        value: initiative.initiativeId,
        label: formatInitiativeOptionLabel(initiative),
      })),
      "Select Parent Initiative",
      true
    );
  }

  function filteredRows() {
    const search = String(listEls.search?.value || "").trim().toLowerCase();
    const status = String(listEls.statusFilter?.value || "").trim();
    return state.rows
      .filter((row) => {
        if (status && row.status !== status) return false;
        if (!search) return true;
        const blob = [
          row.projectId,
          row.projectName,
          row.parentInitiativeName,
          row.parentGoalName,
          row.ownerPm,
          row.sponsor,
          row.status,
          row.entityScope,
        ].join(" ").toLowerCase();
        return blob.includes(search);
      })
      .sort((a, b) => String(a.projectName || a.projectId).localeCompare(String(b.projectName || b.projectId)));
  }

  function updateBulkActionsState() {
    if (!listEls.bulkActionsToggle) return;
    const count = state.selectedProjectIds.size;
    listEls.bulkActionsToggle.disabled = count === 0;
    listEls.bulkActionsToggle.textContent = count ? `Bulk Actions (${count})` : "Bulk Actions";
  }

  function getSelectedProjects() {
    return state.rows.filter((row) => state.selectedProjectIds.has(String(row.projectId || "")));
  }

  function clearProjectSelection({ rerender = true } = {}) {
    state.selectedProjectIds.clear();
    updateBulkActionsState();
    if (rerender) renderList();
  }

  function pruneSelectedProjects() {
    const validIds = new Set((state.rows || []).map((row) => String(row.projectId || "")));
    [...state.selectedProjectIds].forEach((id) => {
      if (!validIds.has(id)) state.selectedProjectIds.delete(id);
    });
    updateBulkActionsState();
  }

  function toProjectSheetRows(rows) {
    return (rows || []).map((row) => ({
      "Project ID": row.projectId || "",
      "Project Name": row.projectName || "",
      "Parent Initiative": row.parentInitiativeName || row.parentInitiativeId || "",
      "Parent Goal": row.parentGoalName || row.parentGoalId || "",
      "Status": row.status || "",
      "PM / Owner": workbook.userDisplayName?.(row.ownerPm) || row.ownerPm || "",
      "Executive Sponsor": workbook.userDisplayName?.(row.sponsor) || row.sponsor || "",
      "Budget": summarizeBudget(row),
      "Entity Scope": row.entityScope || "",
      "Parent Type": row.parentType || "",
      "Start Date": row.startDate || "",
      "End Date": row.endDate || "",
      "Currency": row.currencyCode || "",
      "Budget Amount": row.budgetAmount ?? "",
      "Budget Type": row.budgetType || "",
      "Budget Basis": row.budgetBasis || ""
    }));
  }

  async function updateSelectedProjectStatuses(nextStatus) {
    const selected = getSelectedProjects();
    if (!selected.length) {
      notify("Select one or more projects first.", "warning");
      return;
    }
    const confirmed = await ui.confirm?.({
      title: `${nextStatus} selected projects?`,
      message: `Apply status ${nextStatus} to ${selected.length} selected project(s)?`,
      confirmLabel: nextStatus,
      confirmKind: nextStatus === "Archived" ? "danger" : "primary"
    });
    if (confirmed === false) return;
    let updated = 0;
    for (const row of selected) {
      try {
        if (window.projectStrategyApi?.status) {
          await window.projectStrategyApi.status(row.projectId, nextStatus, row.version || 0);
        }
        row.status = nextStatus;
        updated++;
      } catch (err) {
        console.warn("project bulk status update failed", row.projectId, err);
      }
    }
    clearProjectSelection({ rerender: false });
    await loadPageData();
    notify(`${nextStatus} applied to ${updated} project(s).`, updated ? "success" : "warning");
  }

  function renderList() {
    if (!listEls.headerRow || !listEls.body) return;
    const rows = filteredRows();
    const visibleIds = rows.map((row) => String(row.projectId || "")).filter(Boolean);
    const selectedVisibleCount = visibleIds.filter((id) => state.selectedProjectIds.has(id)).length;
    listEls.headerRow.innerHTML = [
      '<th class="text-center"><input type="checkbox" class="form-check-input m-0" id="projects-select-all" aria-label="Select all visible projects" ' + (visibleIds.length && selectedVisibleCount === visibleIds.length ? "checked" : "") + " /></th>",
      "<th>Project</th>",
      "<th>Parent Initiative</th>",
      "<th>Parent Goal</th>",
      "<th>Status</th>",
      "<th>PM / Owner</th>",
      "<th>Budget</th>",
      '<th class="text-end">Actions</th>',
    ].join("");

    listEls.body.innerHTML = rows.length
      ? rows.map((row) => `
        <tr>
          <td class="text-center align-middle">
            <input type="checkbox" class="form-check-input m-0 project-row-select" data-id="${escapeHtml(row.projectId || "")}" aria-label="Select ${escapeHtml(row.projectName || row.projectId || "project")}" ${state.selectedProjectIds.has(String(row.projectId || "")) ? "checked" : ""} />
          </td>
          <td>
            <div class="fw-semibold">${escapeHtml(row.projectName || "Untitled draft")}</div>
            <div class="small text-muted">${escapeHtml(row.projectId || "-")}</div>
          </td>
          <td>
            <div>${escapeHtml(row.parentInitiativeName || row.parentInitiativeId || "-")}</div>
            <div class="small text-muted">${escapeHtml(row.entityScope || "No scope")}</div>
          </td>
          <td>
            <div>${escapeHtml(row.parentGoalName || row.parentGoalId || "-")}</div>
            <div class="small text-muted">${escapeHtml(row.parentType || "No type")}</div>
          </td>
          <td><span class="badge ${statusBadgeClass(row.status)}">${escapeHtml(row.status || "Draft")}</span></td>
          <td>
            <div>${escapeHtml(workbook.userDisplayName?.(row.ownerPm) || row.ownerPm || "-")}</div>
            <div class="small text-muted">${escapeHtml(workbook.userDisplayName?.(row.sponsor) || row.sponsor || "-")}</div>
          </td>
          <td>${escapeHtml(summarizeBudget(row))}</td>
          <td class="text-end">
            <a class="btn btn-sm btn-outline-primary" href="/management-governance/delivery-execution/projects/${encodeURIComponent(row.projectId)}">Open Detail</a>
          </td>
        </tr>
      `).join("")
      : `<tr><td colspan="8" class="text-center ${state.pageLoadError ? "text-danger" : "text-muted"} py-4">${
        escapeHtml(state.pageLoadError || "No Projects match the current filter.")
      }</td></tr>`;

    listEls.body.querySelectorAll(".project-row-select").forEach((checkbox) => {
      checkbox.addEventListener("change", (event) => {
        const id = String(event.target.dataset.id || "");
        if (!id) return;
        if (event.target.checked) state.selectedProjectIds.add(id);
        else state.selectedProjectIds.delete(id);
        updateBulkActionsState();
        renderList();
      });
      checkbox.addEventListener("click", (event) => event.stopPropagation());
    });

    const selectAll = byId("projects-select-all");
    if (selectAll) {
      selectAll.indeterminate = selectedVisibleCount > 0 && selectedVisibleCount < visibleIds.length;
      selectAll.addEventListener("change", (event) => {
        if (event.target.checked) visibleIds.forEach((id) => state.selectedProjectIds.add(id));
        else visibleIds.forEach((id) => state.selectedProjectIds.delete(id));
        updateBulkActionsState();
        renderList();
      });
    }

    if (listEls.meta) {
      listEls.meta.textContent = state.pageLoadError
        ? `${rows.length} anchored project${rows.length === 1 ? "" : "s"} visible | data load issue`
        : `${rows.length} anchored project${rows.length === 1 ? "" : "s"} visible`;
    }
  }

  async function loadPageData() {
    state.pageLoadError = "";

    await Promise.allSettled([
      workbook.ensureLookupsLoaded?.(),
      workbook.ensureUsersLoaded?.(),
      workbook.ensureCompaniesLoaded?.(),
    ]);

    const [projectsResult, initiativesResult] = await Promise.allSettled([
      window.projectStrategyApi?.list?.() || Promise.resolve({ items: [] }),
      window.initiativeStrategyApi?.list?.() || Promise.resolve({ items: [] }),
    ]);

    if (projectsResult.status === "fulfilled") {
      state.rows = projectsResult.value?.items || [];
    } else {
      state.rows = [];
      state.pageLoadError = getErrorMessage(projectsResult.reason, "Projects could not be loaded.");
      notify(state.pageLoadError, "error");
    }

    if (initiativesResult.status === "fulfilled") {
      state.initiatives = (initiativesResult.value?.items || []).filter((item) => item.initiativeId);
    } else {
      state.initiatives = [];
      const initiativeMessage = getErrorMessage(initiativesResult.reason, "Parent Initiatives could not be loaded.");
      notify(initiativeMessage, "error");
      if (!state.pageLoadError) state.pageLoadError = initiativeMessage;
    }

    state.initiativeById = new Map(state.initiatives.map((item) => [item.initiativeId, item]));
    pruneSelectedProjects();
    hydrateReferenceOptions();
    hydrateInitiativeOptions();
    renderList();
  }

  function resetWizard() {
    clearValidation();
    state.currentStep = 1;
    state.draftProjectId = "";
    state.draftVersion = 0;
    state.templates = [];
    state.previousParentInitiativeId = "";
    state.pendingTemplateAction = null;
    state.selectedTemplateDefaults = {};
    state.appliedTemplateValues = {};
    state.appliedParentDefaults = {};
    state.templateDrivenFields = new Set();

    Object.keys(fieldIds).forEach((key) => {
      const el = byId(fieldIds[key]);
      if (!el) return;
      if (el.tagName === "SELECT") {
        if (key === "creationMode") el.value = "Blank";
        else if (key === "status") el.value = "Draft";
        else if (key === "dependencyFlag" || key === "evidenceRequiredFlag") el.value = "false";
        else el.value = "";
      } else {
        el.value = "";
      }
      el.disabled = false;
    });

    syncAnchor();
    syncCreationMode();
    syncBudgetControls();
    renderTemplatePicker();
    showDefaultsPreview({});
    updateTemplateBadges();
    renderReview();
    renderBudgetGovernanceBanner();
    updateFooter();
  }

  async function handleParentInitiativeChange() {
    clearFieldError("parentInitiativeId");
    const newParentId = inputValue("parentInitiativeId");
    const previousParentId = state.previousParentInitiativeId;
    const previousTemplateId = inputValue("sourceTemplateId");
    const initiative = state.initiativeById.get(newParentId) || null;

    if (previousParentId && newParentId && previousParentId !== newParentId) {
      const nextDefaults = computeParentDefaults(initiative);
      const parentConflicts = getConflictFields(nextDefaults, state.appliedParentDefaults);
      const needsTemplateResetWarning = Boolean(previousTemplateId || Object.keys(state.appliedTemplateValues).length);
      if ((parentConflicts.length || needsTemplateResetWarning) && !confirmOverwrite(
        "Changing the Parent Initiative will recalculate inherited lineage, clear the selected Project Template, and may replace dependent defaults.",
        parentConflicts,
        needsTemplateResetWarning
          ? "The current Project Template selection will be cleared and compatibility will be recalculated."
          : ""
      )) {
        setInputValue("parentInitiativeId", previousParentId);
        return;
      }
      applyParentDefaults(initiative, true);
    } else {
      applyParentDefaults(initiative, false);
    }

    setInputValue("sourceTemplateId", "");
    clearTemplateDefaults({ markCleared: Boolean(previousTemplateId) });
    syncAnchor();
    if (newParentId) await loadCompatibleTemplates();
    syncCreationMode();
    state.previousParentInitiativeId = newParentId;
    renderReview();
  }

  function handleTemplateSelectionChange() {
    clearFieldError("sourceTemplateId");
    showWizardError("");
    applySelectedTemplateDefaults(false);
    renderReview();
  }

  async function saveProject(options) {
    const mode = options?.forceDraft ? "draft" : "final";
    const payload = collectFormData();
    if (options?.forceDraft) payload.status = "Draft";
    const errors = validatePayload(payload, mode);

    if (!applyValidation(errors, true)) {
      showWizardError(options?.forceDraft
        ? "Save Draft is blocked until the Parent Initiative gate and basic integrity checks are satisfied."
        : "Create Project is blocked until all required controls for the selected status are satisfied.");
      return;
    }

    state.saving = true;
    updateFooter();
    showWizardError("");

    try {
      if (!window.projectStrategyApi?.create || !window.projectStrategyApi?.update) {
        throw new Error("Project save service is unavailable on this page.");
      }
      const result = state.draftProjectId
        ? await window.projectStrategyApi.update(state.draftProjectId, payload, state.draftVersion)
        : await window.projectStrategyApi.create(payload);

      state.draftProjectId = result.projectId || state.draftProjectId;
      state.draftVersion = result.version || state.draftVersion;
      state.pendingTemplateAction = null;
      state.previousParentInitiativeId = inputValue("parentInitiativeId");
      setInputValue("projectId", state.draftProjectId);
      if (result.status) setInputValue("status", result.status);

      await loadPageData();

      if (options?.openDetail) {
        window.location.href = `/management-governance/delivery-execution/projects/${encodeURIComponent(state.draftProjectId)}`;
        return;
      }

      if (options?.forceDraft) notify(state.draftProjectId ? `Draft ${state.draftProjectId} saved.` : "Draft saved.", "success");
      else {
        notify(`Project ${result.projectId} created.`, "success");
        modal.hide();
      }
      renderReview();
    } catch (err) {
      const apiErrors = err?.payload?.error?.details || err?.payload?.errors || {};
      const normalizedErrors = {};
      Object.entries(apiErrors).forEach(([field, messages]) => {
        const match = Object.keys(fieldIds).find((key) => key.toLowerCase() === String(field || "").toLowerCase()) || field;
        normalizedErrors[match] = Array.isArray(messages) ? messages.map(String) : [String(messages)];
      });
      applyValidation(normalizedErrors, true);
      showWizardError(getErrorMessage(err, options?.forceDraft ? "Draft save failed." : "Project create failed."));
    } finally {
      state.saving = false;
      updateFooter();
    }
  }

  function bindEvents() {
    listEls.search?.addEventListener("input", renderList);
    listEls.statusFilter?.addEventListener("change", renderList);
    listEls.bulkExportCsvBtn?.addEventListener("click", () => {
      const selected = getSelectedProjects();
      if (!selected.length) return notify("Select one or more projects first.", "warning");
      window.enterpriseWorkbookIo?.exportCsv?.("projects_selected.csv", toProjectSheetRows(selected));
    });
    listEls.bulkExportXlsxBtn?.addEventListener("click", () => {
      const selected = getSelectedProjects();
      if (!selected.length) return notify("Select one or more projects first.", "warning");
      window.enterpriseWorkbookIo?.exportWorkbook?.("projects_selected.xlsx", { Projects_List: toProjectSheetRows(selected) });
    });
    listEls.bulkClearSelectionBtn?.addEventListener("click", () => clearProjectSelection());
    listEls.bulkActivateBtn?.addEventListener("click", async () => { await updateSelectedProjectStatuses("Active"); });
    listEls.bulkArchiveBtn?.addEventListener("click", async () => { await updateSelectedProjectStatuses("Archived"); });
    listEls.createBtn?.addEventListener("click", (event) => {
      event.preventDefault();
      window.location.href = listEls.createBtn.getAttribute("href") || createUrl;
    });

    wizardEls.stepButtons.forEach((button) => {
      button.addEventListener("click", () => {
        const targetStep = Number(button.dataset.step);
        if (targetStep <= state.currentStep) {
          goToStep(targetStep);
          return;
        }
        const errors = getStepErrors(state.currentStep, "final");
        if (!applyValidation(errors, true)) return;
        goToStep(targetStep);
      });
    });

    wizardEls.back?.addEventListener("click", () => goToStep(state.currentStep - 1));
    wizardEls.next?.addEventListener("click", () => {
      const errors = getStepErrors(state.currentStep, "final");
      if (!applyValidation(errors, true)) return;
      goToStep(state.currentStep + 1);
    });
    wizardEls.saveDraft?.addEventListener("click", () => saveProject({ forceDraft: true }));
    wizardEls.create?.addEventListener("click", () => saveProject({ forceDraft: false }));
    wizardEls.createOpen?.addEventListener("click", () => saveProject({ forceDraft: false, openDetail: true }));
    wizardEls.templateReapply?.addEventListener("click", () => {
      if (!getSelectedTemplate()) return;
      applySelectedTemplateDefaults(true);
      notify("Template defaults reapplied.", "success");
    });

    byId(fieldIds.parentInitiativeId)?.addEventListener("change", () => { handleParentInitiativeChange().catch(() => {}); });
    byId(fieldIds.creationMode)?.addEventListener("change", () => {
      syncCreationMode();
      renderReview();
    });
    byId(fieldIds.sourceTemplateId)?.addEventListener("change", handleTemplateSelectionChange);
    byId(fieldIds.budgetRequired)?.addEventListener("change", () => {
      syncBudgetControls();
      renderReview();
    });

    Object.entries(fieldIds).forEach(([field, domId]) => {
      const el = byId(domId);
      if (!el) return;
      const eventName = el.tagName === "SELECT" ? "change" : "input";
      el.addEventListener(eventName, () => {
        clearFieldError(field);
        showWizardError("");
        if (field === "status" || field === "budgetRequired" || field === "budgetAmount" || field === "currencyCode" || field === "budgetType" || field === "budgetBasis" || field === "budgetOwner" || field === "approvalRoute" || field === "noBudgetReason") {
          renderBudgetGovernanceBanner();
        }
        if (state.currentStep === 6) renderReview();
      });
    });

    wizardEl.addEventListener("hidden.bs.modal", resetWizard);
  }

  function init() {
    bindEvents();
    hydrateReferenceOptions();
    resetWizard();
    renderList();
    loadPageData().catch(() => {});
  }

  init();
})(window, document);
