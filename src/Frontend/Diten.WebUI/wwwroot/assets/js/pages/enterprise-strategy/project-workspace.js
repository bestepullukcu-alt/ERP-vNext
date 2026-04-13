/*
Implementation summary / commit notes:
- Added a dedicated Delivery & Execution Project create workspace to match the Initiative create-page pattern.
- Preserved the governed six-step Project wizard with parent-first anchoring, lineage inheritance, template compatibility, and budget governance.
- Inherited fields: Parent Initiative, Parent Objective, Parent Goal, Parent Type, and EntityScope.
- Template-driven fields: Delivery Type, Stage / Phase, Delivery Methodology, Complexity / Size, Readiness, Risk, scope scaffold, approval route, and budget metadata defaults.
- Budget-governed fields: Budget Required, Budget Amount, Currency, Budget Type, Budget Basis, Funding Source, Cost Center, Budget Owner, Approval Route, Financial Notes, and No-Budget Reason.
*/
(function (window, document) {
  "use strict";

  const root = document.getElementById("project-create-workspace");
  if (!root) return;

  const workbook = window.enterpriseWorkbookOptions || {};
  const ui = window.enterpriseStrategyUi || {};
  const byId = (id) => document.getElementById(id);
  const templateModalEl = byId("projectTemplatePickerModal");
  const templateModal = templateModalEl && window.bootstrap?.Modal ? new window.bootstrap.Modal(templateModalEl) : null;

  const state = {
    initiatives: [],
    initiativeById: new Map(),
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
    prefillApplied: false,
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
    templatePreview: byId("project-template-preview"),
    templatePrefillList: byId("project-template-prefill-list"),
    templateBrowse: byId("project-template-browse"),
    templateClear: byId("project-template-clear"),
    templateReapply: byId("project-template-reapply"),
    templatePickerSearch: byId("project-template-picker-search"),
    templatePickerType: byId("project-template-picker-type"),
    templatePickerEntityScope: byId("project-template-picker-entity-scope"),
    templatePickerHelper: byId("project-template-picker-helper"),
    templatePickerBody: byId("project-template-picker-tbody"),
    templatePickerCurrentInitiative: byId("project-template-picker-current-initiative"),
    templatePickerCurrentObjective: byId("project-template-picker-current-objective"),
    templatePickerCurrentGoal: byId("project-template-picker-current-goal"),
    templatePickerCurrentType: byId("project-template-picker-current-type"),
    templatePickerCurrentScope: byId("project-template-picker-current-scope"),
    templatePickerContextWarning: byId("project-template-picker-context-warning"),
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
    sourceSummary: byId("project-source-summary"),
    sourceSummaryName: byId("project-source-summary-name"),
    sourceSummaryNote: byId("project-source-summary-note"),
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
    phase: "Stage / Phase",
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
  const parentInitiativeLookupReservedValues = new Set(["archive", "status", "initiatives"]);
  const listUrl = "/management-governance/delivery-execution/projects";
  const detailUrl = (projectId) => `${listUrl}/${encodeURIComponent(String(projectId || "").trim())}`;

  function notify(message, kind) {
    if (!message) return;
    const normalized = kind === "danger" ? "error" : (kind || "info");
    if (typeof ui.notify === "function") ui.notify(message, normalized);
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

  function isUsableParentInitiativeLookupRow(initiative) {
    const id = String(initiative?.initiativeId || "").trim();
    const name = String(initiative?.initiativeName || "").trim();
    if (!id || !name) return false;
    if (parentInitiativeLookupReservedValues.has(normalizeText(id))) return false;
    if (parentInitiativeLookupReservedValues.has(normalizeText(name))) return false;
    return true;
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

  function selectedParentInitiativeSummary() {
    const initiative = getSelectedInitiative();
    if (!initiative) return "";
    const name = initiative.initiativeName || "Selected Parent Initiative";
    const type = getInitiativeParentType(initiative) || "No type";
    const entityScope = String(initiative.entityScope || "").trim() || "No entity scope";
    return `${name} | ${type} | ${entityScope}`;
  }

  function renderSourceSummary() {
    if (!wizardEls.sourceSummary || !wizardEls.sourceSummaryName || !wizardEls.sourceSummaryNote) return;
    const mode = inputValue("creationMode") || "Blank";
    const template = getSelectedTemplate();
    const initiativeSummary = selectedParentInitiativeSummary();

    if (mode !== "Template") {
      wizardEls.sourceSummary.classList.add("is-empty");
      wizardEls.sourceSummaryName.textContent = "Blank Project create";
      wizardEls.sourceSummaryNote.textContent = "Blank starts without template defaults, but the Project still must remain anchored to a Parent Initiative.";
      return;
    }

    if (!template) {
      wizardEls.sourceSummary.classList.add("is-empty");
      wizardEls.sourceSummaryName.textContent = "From Project Template";
      wizardEls.sourceSummaryNote.innerHTML = initiativeSummary
        ? `Parent Initiative context: <code>${escapeHtml(initiativeSummary)}</code>. Choose a Project Template that matches this Parent Initiative anchor and delivery governance chain.`
        : "Select Parent Initiative first, then browse and choose a Project Template to prefill safe Project fields.";
      return;
    }

    wizardEls.sourceSummary.classList.remove("is-empty");
    wizardEls.sourceSummaryName.textContent = template.name || "Project Template";
    wizardEls.sourceSummaryNote.innerHTML = [
      initiativeSummary ? `Parent Initiative context: <code>${escapeHtml(initiativeSummary)}</code>` : "",
      `Mode: <strong>From Project Template</strong>`,
      `Template ID: <code>${escapeHtml(template.templateId || "")}</code>`,
      template.parentType ? `Type: <code>${escapeHtml(template.parentType)}</code>` : "",
      template.entityScope ? `Entity Scope: <code>${escapeHtml(template.entityScope)}</code>` : "",
    ].filter(Boolean).join(" | ");
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

  function updateTemplatePickerContext() {
    const initiative = getSelectedInitiative();
    if (wizardEls.templatePickerCurrentInitiative) {
      wizardEls.templatePickerCurrentInitiative.textContent = initiative
        ? formatInitiativeOptionLabel(initiative)
        : "Select Parent Initiative first";
    }
    if (wizardEls.templatePickerCurrentObjective) {
      wizardEls.templatePickerCurrentObjective.textContent = inputValue("parentObjectiveName") || "-";
    }
    if (wizardEls.templatePickerCurrentGoal) {
      wizardEls.templatePickerCurrentGoal.textContent = inputValue("parentGoalName") || "-";
    }
    if (wizardEls.templatePickerCurrentType) {
      wizardEls.templatePickerCurrentType.textContent = inputValue("parentType") || "-";
    }
    if (wizardEls.templatePickerCurrentScope) {
      wizardEls.templatePickerCurrentScope.textContent = inputValue("entityScope") || "-";
    }

    if (!wizardEls.templatePickerContextWarning) return;
    if (!initiative) {
      wizardEls.templatePickerContextWarning.textContent = "Select Parent Initiative first. Project Templates are gated by the selected Parent Initiative.";
      wizardEls.templatePickerContextWarning.classList.remove("d-none");
      return;
    }

    if (!state.templates.length) {
      wizardEls.templatePickerContextWarning.textContent = "No compatible Project Templates are currently available for this Parent Initiative type.";
      wizardEls.templatePickerContextWarning.classList.remove("d-none");
      return;
    }

    wizardEls.templatePickerContextWarning.textContent = "";
    wizardEls.templatePickerContextWarning.classList.add("d-none");
  }

  function hydrateTemplatePickerFilters() {
    fillSelect(
      wizardEls.templatePickerType,
      unique(state.templates.map((template) => template.parentType).filter(Boolean)),
      "All types",
      true
    );
    fillSelect(
      wizardEls.templatePickerEntityScope,
      unique(state.templates.map((template) => template.entityScope).filter(Boolean)),
      "All entity scopes",
      true
    );
  }

  function renderTemplatePicker() {
    updateTemplatePickerContext();
    if (!wizardEls.templatePickerBody) return;

    const search = String(wizardEls.templatePickerSearch?.value || "").trim().toLowerCase();
    const parentTypeFilter = String(wizardEls.templatePickerType?.value || "").trim();
    const entityScopeFilter = String(wizardEls.templatePickerEntityScope?.value || "").trim();
    const selectedId = inputValue("sourceTemplateId");
    const filtered = state.templates.filter((template) => {
      if (search) {
        const haystack = [
          template.templateId,
          template.name,
          template.description,
          template.parentType,
          template.ownerPm,
          template.sponsor,
          template.entityScope,
          template.status,
          template.lifecycleStatus,
        ].join(" ").toLowerCase();
        if (!haystack.includes(search)) return false;
      }
      if (parentTypeFilter && template.parentType !== parentTypeFilter) return false;
      if (entityScopeFilter && template.entityScope !== entityScopeFilter) return false;
      return true;
    });

    if (wizardEls.templatePickerHelper) {
      const parentType = inputValue("parentType") || "selected Parent Initiative";
      wizardEls.templatePickerHelper.textContent = state.templates.length
        ? `Showing Project Templates whose type matches Parent Initiative type "${parentType}"${filtered.length !== state.templates.length ? " and the current picker filters" : ""}.`
        : "No Project Templates match the selected Parent Initiative type. Use Blank mode or create a compatible template.";
    }

    wizardEls.templatePickerBody.innerHTML = filtered.length
      ? filtered.map((template) => `
        <tr class="project-template-picker-row ${template.templateId === selectedId ? "table-active" : ""}">
          <td>${escapeHtml(template.templateId)}</td>
          <td>${escapeHtml(template.name || "-")}</td>
          <td>${escapeHtml(template.description || "-")}</td>
          <td>${escapeHtml(template.parentType || "-")}</td>
          <td>${escapeHtml(template.ownerPm || "-")}</td>
          <td>${escapeHtml(template.sponsor || "-")}</td>
          <td>${escapeHtml(template.entityScope || "-")}</td>
          <td>${escapeHtml(template.status || "-")}</td>
          <td>${escapeHtml(template.lifecycleStatus || "-")}</td>
          <td><button type="button" class="btn btn-sm btn-outline-primary" data-template-id="${escapeHtml(template.templateId)}">Use</button></td>
        </tr>
      `).join("")
      : '<tr><td colspan="10" class="text-center text-muted py-3">No matching Project Templates found.</td></tr>';

    wizardEls.templatePickerBody.querySelectorAll("[data-template-id]").forEach((button) => {
      button.addEventListener("click", () => {
        const selectEl = byId(fieldIds.sourceTemplateId);
        if (!selectEl) return;
        selectEl.value = button.dataset.templateId || "";
        handleTemplateSelectionChange();
        templateModal?.hide();
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
    renderSourceSummary();
    updateTemplateBadges();
    if (wizardEls.templateReapply) wizardEls.templateReapply.disabled = true;
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
    renderSourceSummary();
    updateTemplateBadges();
    syncBudgetControls();
    if (wizardEls.templateReapply) wizardEls.templateReapply.disabled = false;
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
      hydrateTemplatePickerFilters();
      renderTemplatePicker();
      return;
    }

    const parentType = getInitiativeParentTypeFilter(initiative);
    if (!parentType) {
      if (wizardEls.templateFilterNote) wizardEls.templateFilterNote.textContent = "Compatible templates will load after Parent Initiative derivation completes.";
      hydrateTemplatePickerFilters();
      renderTemplatePicker();
      return;
    }

    try {
      if (!window.projectStrategyApi?.compatibleTemplates) throw new Error("Compatible template service is unavailable.");
      state.templates = await window.projectStrategyApi.compatibleTemplates(parentType, initiative.entityScope || "") || [];
      fillSelect(
        byId(fieldIds.sourceTemplateId),
        state.templates.map((template) => ({ value: template.templateId, label: template.name })),
        state.templates.length ? "Select compatible template" : "No compatible templates found",
        false
      );
      hydrateTemplatePickerFilters();
      renderTemplatePicker();
      if (wizardEls.templateFilterNote) {
        const initiativeName = initiative.initiativeName || "selected Parent Initiative";
        wizardEls.templateFilterNote.textContent = state.templates.length
          ? `Showing Project Templates compatible with Parent Initiative ${initiativeName} when available.`
          : `No Project Templates match Parent Initiative ${initiativeName}. Use Blank mode or create a compatible template.`;
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
    if (!creationModeEl || !templateSelectEl) return;
    const isTemplateMode = inputValue("creationMode") === "Template";

    creationModeEl.disabled = !initiativeSelected;
    wizardEls.templateHost?.classList.toggle("d-none", !isTemplateMode);
    templateSelectEl.disabled = !initiativeSelected || !isTemplateMode || !state.templates.length;
    if (wizardEls.templateBrowse) wizardEls.templateBrowse.disabled = !initiativeSelected || !isTemplateMode;
    if (wizardEls.templateClear) wizardEls.templateClear.disabled = !initiativeSelected || !isTemplateMode || !inputValue("sourceTemplateId");
    if (wizardEls.templateReapply) wizardEls.templateReapply.disabled = !initiativeSelected || !isTemplateMode || !getSelectedTemplate();

    if (!initiativeSelected) {
      creationModeEl.value = "Blank";
      templateSelectEl.value = "";
      clearTemplateDefaults({ markCleared: false });
      templateModal?.hide();
    }

    if (!isTemplateMode) {
      const hadTemplate = Boolean(inputValue("sourceTemplateId"));
      templateSelectEl.value = "";
      clearTemplateDefaults({ markCleared: hadTemplate });
      templateModal?.hide();
      renderTemplatePicker();
    }

    renderSourceSummary();
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
    if (conflicts.length && forceOverwrite) overwriteFields = conflicts;
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
      const parent = el.closest("[class*='col-']");
      if (parent) parent.classList.toggle("project-field-muted", disabled);
    });

    noFields.forEach((field) => {
      const el = byId(fieldIds[field]);
      if (!el) return;
      const disabled = budgetRequired !== false;
      el.disabled = disabled;
      const parent = el.closest("[class*='col-']");
      if (parent) parent.classList.toggle("project-field-muted", disabled);
    });

    wizardEls.budgetYesGroup?.classList.toggle("d-none", budgetRequired !== true);
    wizardEls.budgetNoGroup?.classList.toggle("d-none", budgetRequired !== false);
    renderBudgetGovernanceBanner();
  }

  function updateActions() {
    if (wizardEls.back) {
      wizardEls.back.classList.toggle("d-none", state.currentStep === 1);
      wizardEls.back.disabled = state.saving;
    }
    if (wizardEls.next) {
      wizardEls.next.classList.toggle("d-none", state.currentStep === 6);
      wizardEls.next.disabled = state.saving;
    }
    if (wizardEls.create) {
      wizardEls.create.classList.toggle("d-none", state.currentStep !== 6);
      wizardEls.create.disabled = state.saving;
    }
    if (wizardEls.createOpen) {
      wizardEls.createOpen.classList.toggle("d-none", state.currentStep !== 6);
      wizardEls.createOpen.disabled = state.saving;
    }
    if (wizardEls.saveDraft) {
      wizardEls.saveDraft.disabled = state.saving;
      wizardEls.saveDraft.textContent = state.saving ? "Saving..." : (state.draftProjectId ? "Update Draft" : "Save Draft");
    }

    wizardEls.stepButtons.forEach((button) => {
      const step = Number(button.dataset.step);
      button.classList.toggle("active", step === state.currentStep);
      button.classList.toggle("completed", step < state.currentStep);
    });
    wizardEls.panes.forEach((pane) => pane.classList.toggle("d-none", Number(pane.dataset.step) !== state.currentStep));
  }

  function goToStep(step) {
    state.currentStep = Math.max(1, Math.min(6, Number(step) || 1));
    updateActions();
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

    if (wizardEls.identityReview) {
      wizardEls.identityReview.innerHTML = renderSummaryList([
        { label: "Project ID", value: payload.projectId || "Assigned on first save" },
        { label: "Project Name", value: payload.projectName || "Untitled draft" },
        { label: "Description", value: payload.description || "Not set" },
        { label: "Creation Mode", value: payload.creationMode },
        { label: "Template", value: template?.name || "Blank" },
      ]);
    }

    if (wizardEls.anchorReview) {
      wizardEls.anchorReview.innerHTML = renderSummaryList([
        { label: "Parent Initiative", value: getSelectedInitiative()?.initiativeName || payload.parentInitiativeId || "Not selected" },
        { label: "Parent Objective", value: payload.parentObjectiveName || "Derived after anchor selection" },
        { label: "Parent Goal", value: payload.parentGoalName || "Derived after anchor selection" },
        { label: "Parent Type", value: payload.parentType || "Derived after anchor selection" },
        { label: "EntityScope", value: payload.entityScope || "Derived after anchor selection" },
      ]);
    }

    if (wizardEls.ownershipReview) {
      wizardEls.ownershipReview.innerHTML = renderSummaryList([
        { label: "Project Owner / PM", value: payload.ownerPm || "Not set" },
        { label: "Executive Sponsor", value: payload.sponsor || "Not set" },
        { label: "Business Owner", value: payload.businessOwner || "Not set" },
        { label: "Delivery Company", value: payload.deliveryCompanyId || "Not set" },
        { label: "Scope Summary", value: payload.scopeSummary || "Not set" },
      ]);
    }

    if (wizardEls.planningReview) {
      wizardEls.planningReview.innerHTML = renderSummaryList([
        { label: "Status", value: payload.status },
        { label: "Stage / Phase", value: payload.phase || "Not set" },
        { label: "Delivery Type", value: payload.deliveryType || "Not set" },
        { label: "Methodology", value: payload.deliveryMethodology || "Not set" },
        { label: "Priority", value: payload.priority || "Not set" },
        { label: "Timeline", value: `${formatDate(payload.startDate)} to ${formatDate(payload.endDate)}` },
      ]);
    }

    if (wizardEls.controlsReview) {
      wizardEls.controlsReview.innerHTML = renderSummaryList([
        { label: "Readiness", value: payload.readinessStatus || "Not set" },
        { label: "Risk", value: payload.riskRating || "Not set" },
        { label: "Compliance / Regulatory Impact", value: payload.complianceRegulatoryImpact || "Not set" },
        { label: "Evidence Required", value: payload.evidenceRequiredFlag ? "Yes" : "No" },
        { label: "Approval Route", value: payload.approvalRoute || "Not set" },
        { label: "Success Metric", value: payload.successMetric || "Not set" },
      ]);
    }

    if (wizardEls.budgetReview) {
      wizardEls.budgetReview.innerHTML = renderSummaryList([
        { label: "Budget Required", value: payload.budgetRequired == null ? "Not set" : (payload.budgetRequired ? "Yes" : "No") },
        { label: "Budget Summary", value: summarizeBudget(payload) },
        { label: "Funding / Owning Company", value: payload.fundingCompanyId || "Not set" },
        { label: "Funding Source", value: payload.fundingSource || "Not set" },
        { label: "Cost Center", value: payload.costCenter || "Not set" },
        { label: "Budget Owner", value: payload.budgetOwner || "Not set" },
        { label: "Approval Route", value: payload.approvalRoute || "Not set" },
      ]);
    }

    const blockerItems = Object.values(blockers).flat();
    if (wizardEls.blockers) {
      wizardEls.blockers.innerHTML = blockerItems.length
        ? blockerItems.map((message) => `<li>${escapeHtml(message)}</li>`).join("")
        : '<li class="text-success">No blocking validations for the selected status.</li>';
    }

    if (wizardEls.warnings) {
      wizardEls.warnings.innerHTML = warnings.length
        ? warnings.map((message) => `<li>${escapeHtml(message)}</li>`).join("")
        : '<li class="text-muted">No warnings.</li>';
    }
  }

  function hydrateReferenceOptions() {
    const users = workbook.userOptions?.() || unique([
      ...state.initiatives.map((row) => row.executiveSponsor),
      ...state.initiatives.map((row) => row.owner),
    ]);
    const companies = workbook.companyOptions?.() || unique([
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

  async function loadPageData() {
    await Promise.allSettled([
      workbook.ensureLookupsLoaded?.(),
      workbook.ensureUsersLoaded?.(),
      workbook.ensureCompaniesLoaded?.(),
    ]);

    try {
      const initiativesPage = await (window.initiativeStrategyApi?.list?.() || Promise.resolve({ items: [] }));
      state.initiatives = (initiativesPage?.items || []).filter(isUsableParentInitiativeLookupRow);
      state.initiativeById = new Map(state.initiatives.map((item) => [item.initiativeId, item]));
      hydrateReferenceOptions();
      hydrateInitiativeOptions();
      applyPrefillParentInitiative();
    } catch (err) {
      notify(getErrorMessage(err, "Parent Initiatives could not be loaded."), "error");
    }
  }

  function applyPrefillParentInitiative() {
    if (state.prefillApplied) return;
    const prefillParentInitiativeId = String(root.dataset.prefillParentInitiativeId || "").trim();
    if (!prefillParentInitiativeId || !state.initiativeById.has(prefillParentInitiativeId)) {
      state.prefillApplied = true;
      return;
    }

    setInputValue("parentInitiativeId", prefillParentInitiativeId);
    state.previousParentInitiativeId = prefillParentInitiativeId;
    state.prefillApplied = true;
    handleParentInitiativeChange().catch(() => {});
  }

  function resetWorkspace() {
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
    renderSourceSummary();
    updateTemplateBadges();
    renderReview();
    renderBudgetGovernanceBanner();
    updateActions();
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
    syncCreationMode();
    renderReview();
  }

  async function openTemplatePicker() {
    if (!inputValue("parentInitiativeId")) {
      notify("Select Parent Initiative first.", "error");
      return;
    }

    if (inputValue("creationMode") !== "Template") {
      setInputValue("creationMode", "Template");
      syncCreationMode();
    }

    await loadCompatibleTemplates();
    renderSourceSummary();
    templateModal?.show();
  }

  function clearSelectedTemplate() {
    const hadTemplate = Boolean(inputValue("sourceTemplateId"));
    setInputValue("sourceTemplateId", "");
    setInputValue("creationMode", "Blank");
    clearTemplateDefaults({ markCleared: hadTemplate });
    syncCreationMode();
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
    updateActions();
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

      if (options?.openDetail) {
        window.location.href = detailUrl(state.draftProjectId);
        return;
      }

      if (options?.forceDraft) {
        notify(state.draftProjectId ? `Draft ${state.draftProjectId} saved.` : "Draft saved.", "success");
      } else {
        window.location.href = listUrl;
        return;
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
      updateActions();
    }
  }

  function bindEvents() {
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
    wizardEls.templateBrowse?.addEventListener("click", () => { openTemplatePicker().catch(() => {}); });
    wizardEls.templateClear?.addEventListener("click", clearSelectedTemplate);
    wizardEls.templateReapply?.addEventListener("click", () => {
      if (!getSelectedTemplate()) return;
      applySelectedTemplateDefaults(true);
      notify("Template defaults reapplied.", "success");
    });

    byId(fieldIds.parentInitiativeId)?.addEventListener("change", () => { handleParentInitiativeChange().catch(() => {}); });
    byId(fieldIds.creationMode)?.addEventListener("change", () => {
      syncCreationMode();
      if (inputValue("creationMode") === "Template" && inputValue("parentInitiativeId")) {
        openTemplatePicker().catch(() => {});
      }
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

    ["project-template-picker-search", "project-template-picker-type", "project-template-picker-entity-scope"].forEach((id) => {
      byId(id)?.addEventListener("input", renderTemplatePicker);
      byId(id)?.addEventListener("change", renderTemplatePicker);
    });
  }

  function init() {
    bindEvents();
    resetWorkspace();
    hydrateReferenceOptions();
    loadPageData().catch(() => {});
  }

  init();
})(window, document);
