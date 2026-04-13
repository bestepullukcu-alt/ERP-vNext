(function (window, document) {
  "use strict";

  const tableBody = document.querySelector("#goals-table tbody");
  const saveBtn = document.getElementById("goal-save");
  const saveDraftBtn = document.getElementById("goal-save-draft");
  const validateBtn = document.getElementById("goal-validate");
  const workspaceCancelBtn = document.getElementById("goal-workspace-cancel");
  const addMetricBtn = document.getElementById("goal-add-metric");
  const metricHost = document.getElementById("goal-metrics-editor");
  const errorEl = document.getElementById("goal-form-error");
  const readinessPanelEl = document.getElementById("goal-readiness-panel");
  const publishReadinessIndicatorEl = document.getElementById("goal-publish-readiness-indicator");
  const publishReadinessTextEl = document.getElementById("goal-publish-readiness-text");
  const validationModeIndicatorEl = document.getElementById("goal-validation-mode-indicator");
  const backendAlignmentIndicatorEl = document.getElementById("goal-backend-alignment-indicator");
  const readinessKpiCountEl = document.getElementById("goal-readiness-kpi-count");
  const readinessKpiMissingYearlyEl = document.getElementById("goal-readiness-kpi-missing-yearly");
  const readinessBudgetEnabledEl = document.getElementById("goal-readiness-budget-enabled");
  const readinessGovernanceMissingEl = document.getElementById("goal-readiness-governance-missing");
  const readinessMissingRequiredEl = document.getElementById("goal-readiness-missing-required");
  const readinessPublishBlockersEl = document.getElementById("goal-readiness-publish-blockers");
  const workspaceRoot = document.getElementById("goal-create-workspace");
  const isWorkspaceMode = Boolean(workspaceRoot);
  const goalListUrl = "/management-governance/enterprise-strategy-business-performance/goals";
  const goalCreateUrl = `${goalListUrl}/new`;
  const goalEditUrl = (goalId) => `${goalListUrl}/${encodeURIComponent(String(goalId || "").trim())}/edit`;
  const goalDuplicateUrl = (goalId) => `${goalCreateUrl}?duplicateFrom=${encodeURIComponent(String(goalId || "").trim())}`;
  const modalEl = document.getElementById("goalEditorModal");
  const modal = modalEl && modalEl.classList.contains("modal") ? new bootstrap.Modal(modalEl) : null;
  const modalTitle = document.getElementById("goal-modal-title");
  const modalSubtitle = document.getElementById("goal-modal-subtitle");
  const wizardStepButtons = Array.from(document.querySelectorAll("#goal-wizard-steps .goal-wizard-step-btn"));
  const wizardStepPanes = Array.from(document.querySelectorAll(".goal-wizard-step-pane"));
  const wizardBackBtn = document.getElementById("goal-step-back");
  const wizardNextBtn = document.getElementById("goal-step-next");
  const totalWizardSteps = wizardStepButtons.length || 7;
  const headerRow = document.getElementById("goals-header-row");
  const moreFiltersPanel = document.getElementById("goal-more-filters");
  const moreFiltersToggle = document.querySelector('[data-bs-target="#goal-more-filters"]');
  const filters = {
    search: document.getElementById("goal-search"),
    category: document.getElementById("goal-filter-category"),
    owner: document.getElementById("goal-filter-owner"),
    status: document.getElementById("goal-filter-status"),
    priority: document.getElementById("goal-filter-priority"),
    scopeMode: document.getElementById("goal-filter-scope-mode"),
    company: document.getElementById("goal-filter-company"),
    yearRange: document.getElementById("goal-filter-year-range"),
    scope: document.getElementById("goal-filter-scope"),
    apply: document.getElementById("goal-apply-filters"),
    reset: document.getElementById("goal-reset-filters")
  };
  const budgetFillColumnBtn = document.getElementById("goal-budget-fill-column");
  const budgetInterpolateBtn = document.getElementById("goal-budget-interpolate");
  const budgetCopyDownBtn = document.getElementById("goal-budget-copy-down");
  const budgetClearColumnBtn = document.getElementById("goal-budget-clear-column");
  const budgetHelperColumn = document.getElementById("goal-budget-helper-column");
  const importFileInput = document.getElementById("goal-import-file");
  const importWorkbookInput = document.getElementById("goal-import-workbook-file");
  const importPageActionBtn = document.getElementById("goal-data-import-page");
  const importWorkbookActionBtn = document.getElementById("goal-data-import-workbook");
  let goalSearchValue = "";
  let currentVersion = 0;
  let isEditMode = false;
  let isDirty = false;
  let hasSubmitAttempt = false;
  let activeValidationMode = "draft";
  let allowModalClose = false;
  let previousStartYearRaw = "";
  let previousEndYearRaw = "";
  let previousStrategyPeriodIdRaw = "";
  let cachedItems = [];
  let filteredItems = [];
  let filterSummaryHost = null;
  let goalsDt = null;
  const selectedGoalIds = new Set();
  let applicableCompaniesPickerActiveIndex = -1;
  let creationModeCode = "Blank";
  let sourceTemplateId = "";
  let sourceTemplateVersion = null;
  let selectedSourceMeta = null;
  let pickerCatalogRows = [];

  let suppressAutoFilterEvents = false;
  let metricCatalogByName = new Map();
  let activeStrategyPeriods = [];
  let strategyPeriodsById = new Map();
  let selectedStrategyPeriodContext = null;
  let initialStrategyPeriodId = "";
  let currentWizardStep = 1;
  const goalBudgetTbody = document.getElementById("goal-budget-year-rows");
  const goalSourcePickerModalEl = document.getElementById("goalSourcePickerModal");
  const goalSourcePickerModal = goalSourcePickerModalEl && window.bootstrap?.Modal ? new window.bootstrap.Modal(goalSourcePickerModalEl) : null;
  const fieldIds = [
    "goal-id", "goal-name", "goal-category", "goal-strategic-theme", "goal-owner-role", "goal-owner-company", "goal-owner-person", "goal-owner-person-display", "goal-owner", "goal-status", "goal-priority", "goal-statement",
    "goal-strategy-period", "goal-planning-start-year", "goal-planning-end-year", "goal-planning-scope-preview", "goal-entity-scope", "goal-change-log-ref",
    "goal-scope-mode", "goal-primary-company", "goal-applies-to-all-companies", "goal-applicable-companies", "goal-related-entity-scope-summary", "goal-decision-reference", "goal-evidence-reference",
    "goal-version", "goal-budget-year-table", "goal-metrics-editor", "goal-business-unit", "goal-region"
  ];
  const sectionByField = {
    "goal-id": "goal-sec-identity",
    "goal-name": "goal-sec-identity",
    "goal-category": "goal-sec-identity",
    "goal-strategic-theme": "goal-sec-identity",
    "goal-statement": "goal-sec-identity",
    "goal-status": "goal-sec-identity",
    "goal-priority": "goal-sec-identity",
    "goal-owner-role": "goal-sec-ownership",
    "goal-owner-company": "goal-sec-ownership",
    "goal-owner-person": "goal-sec-ownership",
    "goal-owner-person-display": "goal-sec-ownership",
    "goal-owner": "goal-sec-ownership",
    "goal-strategy-period": "goal-sec-planning",
    "goal-planning-start-year": "goal-sec-planning",
    "goal-planning-end-year": "goal-sec-planning",
    "goal-planning-scope-preview": "goal-sec-planning",
    "goal-entity-scope": "goal-sec-company",
    "goal-change-log-ref": "goal-sec-governance",
    "goal-scope-mode": "goal-sec-company",
    "goal-primary-company": "goal-sec-company",
    "goal-applies-to-all-companies": "goal-sec-company",
    "goal-applicable-companies": "goal-sec-company",
    "goal-business-unit": "goal-sec-company",
    "goal-region": "goal-sec-company",
    "goal-related-entity-scope-summary": "goal-sec-company",
    "goal-decision-reference": "goal-sec-governance",
    "goal-evidence-reference": "goal-sec-governance",
    "goal-version": "goal-sec-governance",
    "goal-budget-year-table": "goal-sec-budget",
    "goal-metrics-editor": "goal-sec-metrics"
  };
  const wizardStepBySection = {
    "goal-sec-identity": 1,
    "goal-sec-ownership": 2,
    "goal-sec-planning": 3,
    "goal-sec-company": 4,
    "goal-sec-metrics": 5,
    "goal-sec-budget": 6,
    "goal-sec-governance": 7
  };

  const yearRegex = /^\d{4}$/;
  const workbook = window.enterpriseWorkbookOptions || {};
  const uniq = (values) => [...new Set((values || []).filter(Boolean).map((v) => String(v).trim()))];
  const nonEmpty = (values, fallback) => (Array.isArray(values) && values.length ? values : fallback);
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);
  const debounce = (fn, wait = 250) => {
    let timerId = null;
    return (...args) => {
      if (timerId) clearTimeout(timerId);
      timerId = setTimeout(() => fn(...args), wait);
    };
  };
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();
  const userOptions = () => workbook.userOptions?.() || [];
  const fallbackColumns = [
    { key: "id", label: "Goal ID" }, { key: "name", label: "Goal" }, { key: "category", label: "Category" },
    { key: "owner", label: "Owner" }, { key: "status", label: "Status" }, { key: "priority", label: "Priority" },
    { key: "scopeMode", label: "Applicability Mode" }, { key: "primaryCompanyId", label: "Primary Company" }, { key: "applicableCompanyIds", label: "Applicable Companies" },
    { key: "startYear", label: "Start Year" }, { key: "endYear", label: "End Year" }, { key: "metricCount", label: "Metric Count" },
    { key: "version", label: "Version" }, { key: "actions", label: "Actions" }
  ];
  let tableControls = null;
  if (tableBody) {
    try {
      tableControls = window.enterpriseTableControls?.create({
        pageKey: "goals",
        storageKey: "goalsTableLayout",
        columnsButtonId: "goal-columns-btn",
        columns: [
          { key: "id", label: "Goal ID", defaultVisible: false },
          { key: "name", label: "Goal", defaultVisible: true },
          { key: "category", label: "Category", defaultVisible: true },
          { key: "owner", label: "Owner", defaultVisible: true },
          { key: "status", label: "Status", defaultVisible: true },
          { key: "priority", label: "Priority", defaultVisible: true },
          { key: "scopeMode", label: "Applicability Mode", defaultVisible: false },
          { key: "primaryCompanyId", label: "Primary Company", defaultVisible: false },
          { key: "applicableCompanyIds", label: "Applicable Companies", defaultVisible: false },
          { key: "startYear", label: "Start Year", defaultVisible: true },
          { key: "endYear", label: "End Year", defaultVisible: true },
          { key: "metricCount", label: "Metric Count", defaultVisible: false },
          { key: "entityScope", label: "Entity Scope", defaultVisible: false },
          { key: "changeLogRef", label: "Change Log Ref", defaultVisible: false },
          { key: "version", label: "Version", defaultVisible: false },
          { key: "actions", label: "Actions", defaultVisible: true }
        ],
        onChange: () => renderFiltered()
      }) || null;
    } catch (err) {
      console.error("goals table controls init failed", err);
    }
  }
  filterSummaryHost = document.getElementById("goal-active-filters");
  const parseYear = (v) => {
    const s = String(v ?? "").trim();
    if (!s) return null;
    if (/^\d{4}$/.test(s)) {
      const n = Number(s);
      return Number.isInteger(n) ? n : null;
    }
    const dmy = s.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
    if (dmy) {
      const day = Number(dmy[1]);
      const month = Number(dmy[2]);
      const year = Number(dmy[3]);
      const dt = new Date(year, month - 1, day);
      if (dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day) return year;
      return null;
    }
    const iso = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (iso) {
      const year = Number(iso[1]);
      const month = Number(iso[2]);
      const day = Number(iso[3]);
      const dt = new Date(year, month - 1, day);
      if (dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day) return year;
      return null;
    }
    return null;
  };
  const parseDecimal = (value) => {
    const text = String(value ?? "").trim();
    if (!text) return null;
    if (!/^[+\-]?[0-9.,\s]+$/.test(text)) return null;

    let normalized = text.replace(/\s+/g, "");
    const lastComma = normalized.lastIndexOf(",");
    const lastDot = normalized.lastIndexOf(".");
    if (lastComma >= 0 && lastDot >= 0) {
      if (lastComma > lastDot) {
        normalized = normalized.replace(/\./g, "").replace(",", ".");
      } else {
        normalized = normalized.replace(/,/g, "");
      }
    } else if (lastComma >= 0) {
      normalized = normalized.replace(/\./g, "").replace(",", ".");
    } else {
      normalized = normalized.replace(/,/g, "");
    }

    const num = Number(normalized);
    return Number.isFinite(num) ? num : null;
  };
  const formatDecimalForInput = (value) => {
    if (value === null || value === undefined || value === "") return "";
    const num = typeof value === "number" ? value : parseDecimal(value);
    if (!Number.isFinite(num)) return "";
    const fixed = num.toFixed(6).replace(/0+$/, "").replace(/\.$/, "");
    return fixed;
  };
  const normalizeGoalRow = (row) => {
    if (!row || typeof row !== "object") return null;
    const planning = row.planning || {};
    const companyScope = row.companyScope || {};
    const governance = row.governance || {};
    const audit = row.audit || row.traceability || {};
    const id = String(row.id || row.goalId || row.GoalId || "").trim();
    const applicableCompanyIdsRaw =
      Array.isArray(row.applicableCompanyIds) ? row.applicableCompanyIds
        : (Array.isArray(row.ApplicableCompanyIds) ? row.ApplicableCompanyIds
          : (Array.isArray(companyScope.applicableCompanyIds) ? companyScope.applicableCompanyIds : []));
    return {
      ...row,
      id,
      name: String(row.name || row.goalTitle || row.GoalTitle || "").trim(),
      category: String(row.category || row.goalType || row.goalTypeId || row.Category || "").trim(),
      strategicThemeId: String(row.strategicThemeId || row.strategicTheme || row.StrategicThemeId || "").trim(),
      statement: String(row.statement || row.goalStatement || row.GoalStatement || "").trim(),
      ownerRole: String(row.ownerPositionId || row.ownerRole || row.OwnerRole || row.ownerId || "").trim(),
      ownerCompanyId: String(row.ownerCompanyId || row.OwnerCompanyId || row.primaryCompanyId || companyScope.primaryCompanyId || "").trim(),
      primaryCompanyId: String(row.primaryCompanyId || row.PrimaryCompanyId || row.ownerCompanyId || row.OwnerCompanyId || companyScope.primaryCompanyId || "").trim(),
      ownerPersonId: String(row.currentOwnerPersonId || row.ownerPersonId || row.OwnerPersonId || "").trim(),
      ownerDisplayName: String(row.ownerDisplayName || row.OwnerDisplayName || "").trim(),
      status: String(row.status || row.statusCode || row.Status || "").trim(),
      priority: String(row.priority || row.priorityCode || row.Priority || "").trim(),
      strategyPeriodId: String(row.strategyPeriodId || planning.strategyPeriodId || row.StrategyPeriodId || "").trim(),
      planningHorizonStart: row.planningHorizonStart || row.startDate || planning.startDate || row.StartDate || null,
      planningHorizonEnd: row.planningHorizonEnd || row.endDate || planning.endDate || row.EndDate || null,
      entityScope: String(row.entityScope || row.relatedEntityScope || planning.relatedEntityScope || "").trim(),
      scopeMode: String(row.scopeMode || row.applicabilityMode || companyScope.scopeModeCode || companyScope.applicabilityMode || "").trim(),
      appliesToAllCompaniesFlag: Boolean(
        row.appliesToAllCompaniesFlag ??
        row.appliesToAllCompanies ??
        companyScope.appliesToAllCompaniesFlag ??
        companyScope.appliesToAllCompanies
      ),
      applicableCompanyIds: applicableCompanyIdsRaw.map((value) => String(value || "").trim()).filter(Boolean),
      relatedEntityScopeSummary: String(row.relatedEntityScopeSummary || companyScope.relatedEntityScopeSummary || planning.relatedEntityScopeSummary || "").trim(),
      changeLogRef: String(row.changeLogRef || planning.changeLogRef || governance.changeLogRef || audit.changeLogRef || "").trim(),
      decisionReference: String(row.decisionReference || governance.decisionReference || audit.decisionReference || "").trim(),
      evidenceReference: String(row.evidenceReference || row.evidenceLink || governance.evidenceReference || governance.evidenceLink || audit.evidenceReference || audit.evidenceLink || "").trim(),
      metrics: Array.isArray(row.metrics) ? row.metrics : [],
      version: Number(row.version ?? row.Version ?? governance.version ?? audit.version ?? 0) || 0
    };
  };
  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
  const parseYearRange = (value) => {
    const text = String(value || "").trim();
    if (!text) return null;
    const parts = text.split(/\s*[-–:]\s*/).map((x) => x.trim()).filter(Boolean);
    if (parts.length === 1) {
      const year = parseYear(parts[0]);
      return year ? { from: year, to: year } : null;
    }
    if (parts.length === 2) {
      const from = parseYear(parts[0]);
      const to = parseYear(parts[1]);
      if (!from || !to || to < from) return null;
      return { from, to };
    }
    return null;
  };
  const parseWorkspaceRouteContext = () => {
    if (!isWorkspaceMode) return { mode: "list", goalId: "" };
    const path = String(window.location.pathname || "");
    const editMatch = path.match(/\/goals\/([^/]+)\/edit\/?$/i);
    if (editMatch) {
      return {
        mode: "edit",
        goalId: decodeURIComponent(String(editMatch[1] || "").trim())
      };
    }
    const search = new URLSearchParams(window.location.search || "");
    const duplicateId = String(search.get("duplicateFrom") || "").trim();
    if (duplicateId) return { mode: "duplicate", goalId: duplicateId };
    const editId = String(search.get("id") || search.get("goalId") || "").trim();
    if (editId) return { mode: "edit", goalId: editId };
    return { mode: "create", goalId: "" };
  };
  const navigateToGoalWorkspace = (mode, goalId) => {
    const id = String(goalId || "").trim();
    if (mode === "edit" && id) {
      window.location.assign(goalEditUrl(id));
      return;
    }
    if (mode === "duplicate" && id) {
      window.location.assign(goalDuplicateUrl(id));
      return;
    }
    window.location.assign(goalCreateUrl);
  };
  const normalizeValidationMode = (mode, payload) => {
    const requested = String(mode || "").trim().toLowerCase();
    if (requested === "draft" || requested === "publish") return requested;
    const status = String(payload?.statusCode || getEl("goal-status")?.value || "").trim().toLowerCase();
    return status === "draft" ? "draft" : "publish";
  };
  const isMetricActiveForValidation = (metric) => {
    if (!metric) return false;
    const status = String(metric.metricBindingStatus || "").trim().toLowerCase();
    if (status === "inactive" || status === "disabled" || status === "archived" || status === "removed") return false;
    return Boolean(String(metric.metricName || "").trim());
  };
  const pushUnique = (arr, text) => {
    if (!text) return;
    if (!arr.includes(text)) arr.push(text);
  };
  const fieldListHtml = (items) => {
    const list = (items || []).filter(Boolean);
    if (!list.length) return "<li class=\"text-muted\">None</li>";
    return list.map((item) => `<li>${escapeHtml(item)}</li>`).join("");
  };
  const getPlanningYears = () => {
    const start = planningYearFromInput("goal-planning-start-year");
    const end = planningYearFromInput("goal-planning-end-year");
    if (!start || !end || end < start) return [];
    const years = [];
    for (let y = start; y <= end; y++) years.push(y);
    return years;
  };
  const filterLabels = {
    search: "Search",
    category: "Category",
    owner: "Owner",
    status: "Status",
    priority: "Priority",
    scopeMode: "Applicability Mode",
    company: "Company",
    yearRange: "Year Range",
    scope: "Entity Scope"
  };

  function syncFilterUi() {
    if (!filters.scopeMode || !filters.company) return;
    const scopeMode = String(filters.scopeMode?.value || "").trim();
    const companyEnabled = scopeMode === "AppliesToSelectedCompanies" || scopeMode === "MultiCompany" || scopeMode === "SingleCompany";
    filters.company.disabled = !companyEnabled;
    if (!companyEnabled) filters.company.value = "";
  }

  function syncMoreFiltersPanel() {
    const hasAdvancedFilter = [filters.scopeMode, filters.company, filters.yearRange, filters.scope]
      .some((el) => String(el?.value || "").trim() !== "");
    if (!moreFiltersPanel || !moreFiltersToggle) return;
    moreFiltersPanel.classList.toggle("show", hasAdvancedFilter);
    moreFiltersToggle.setAttribute("aria-expanded", hasAdvancedFilter ? "true" : "false");
  }

  function applyFiltersAuto() {
    if (suppressAutoFilterEvents || !tableBody) return;
    renderFiltered(true);
  }

  function updateBulkActionsState() {
    const bulkActionsToggle = document.getElementById("goal-bulk-actions-toggle");
    if (bulkActionsToggle) bulkActionsToggle.disabled = selectedGoalIds.size === 0;
  }

  function getSelectedItems() {
    return cachedItems.filter((x) => selectedGoalIds.has(String(x.id || "")));
  }

  function clearSelection({ rerender = true } = {}) {
    selectedGoalIds.clear();
    updateBulkActionsState();
    if (rerender && tableBody) renderFiltered(false);
  }

  function exportSelectedCsv() {
    const rowsToExport = getSelectedItems();
    if (!rowsToExport.length) return notify("Select one or more rows first.", "warning");
    const sorted = tableControls?.sortRows?.(rowsToExport, getSortValue) || rowsToExport;
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    if (window.enterpriseTablePageUtils?.exportVisibleCsv) {
      window.enterpriseTablePageUtils.exportVisibleCsv("goals_selected.csv", sorted, cols, getExportValue);
    } else {
      const rows = toGoalSheetRows(sorted);
      exportGoalsCsvFallback(rows);
      notify("CSV exported with fallback mode.", "warning");
    }
  }

  function exportSelectedXlsx() {
    const rowsToExport = getSelectedItems();
    if (!rowsToExport.length) return notify("Select one or more rows first.", "warning");
    const sorted = tableControls?.sortRows?.(rowsToExport, getSortValue) || rowsToExport;
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    const rows = sorted.map((item) => {
      const out = {};
      cols.forEach((c) => { out[c.label] = getExportValue(item, c.key); });
      return out;
    });
    if (!window.enterpriseWorkbookIo?.exportWorkbook) return notify("Excel export engine not loaded. Please hard refresh and retry.", "error");
    window.enterpriseWorkbookIo.exportWorkbook("goals_selected.xlsx", { Goals_List: rows });
  }

  async function exportSelectedWorkbook() {
    try {
      const selected = getSelectedItems();
      if (!selected.length) return notify("Select one or more rows first.", "warning");
      const sheets = { Goals_List: toGoalSheetRows(selected) };
      if (!window.enterpriseWorkbookIo?.exportWorkbook) return notify("Workbook export engine not loaded. Please hard refresh and retry.", "error");
      window.enterpriseWorkbookIo.exportWorkbook("goals_selected_workbook.xlsx", sheets);
    } catch (err) {
      notify(window.enterpriseStrategyUi.getErrorMessage(err, "Workbook export failed"), "error");
    }
  }

  async function archiveSelectedGoals() {
    const selected = getSelectedItems();
    if (!selected.length) return notify("Select one or more rows first.", "warning");
    const confirmed = await window.enterpriseStrategyUi?.confirm?.({
      title: "Archive selected goals?",
      message: `Archive ${selected.length} selected goal(s)?`,
      confirmLabel: "Archive",
      confirmKind: "danger"
    });
    if (!confirmed) return;
    let archived = 0;
    for (const item of selected) {
      try {
        await window.strategyGoalsApi.archive(item.id, item.version || 0);
        archived++;
      } catch (_) { }
    }
    clearSelection({ rerender: false });
    await load();
    notify(`Archived ${archived} goal(s).`, archived ? "success" : "warning");
  }

  function renderActiveFilterChips(filterState) {
    if (!filterSummaryHost) return;
    const active = Object.entries(filterState || {}).filter(([, value]) => String(value ?? "").trim() !== "");
    if (!active.length) {
      filterSummaryHost.innerHTML = "";
      return;
    }
    filterSummaryHost.innerHTML =
      `<div class="d-flex flex-wrap align-items-center gap-2">` +
      active.map(([key, value]) => (
        `<span class="goal-filter-chip">` +
        `<span>${filterLabels[key] || key}: ${String(value)}</span>` +
        `<button type="button" class="goal-filter-chip-remove" data-key="${key}" aria-label="Remove ${key} filter">×</button>` +
        `</span>`
      )).join("") +
      `<button type="button" class="btn btn-link btn-sm p-0 text-decoration-none" id="goal-clear-all-filters">Clear all</button>` +
      `</div>`;
    filterSummaryHost.querySelectorAll(".goal-filter-chip-remove").forEach((btn) => {
      btn.addEventListener("click", () => {
        const key = String(btn.dataset.key || "");
        const el = filters[key];
        if (!el) return;
        el.value = "";
        if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) window.jQuery(el).trigger("change.select2");
        if (key === "scopeMode") syncFilterUi();
        renderFiltered(true);
      });
    });
    filterSummaryHost.querySelector("#goal-clear-all-filters")?.addEventListener("click", () => {
      Object.keys(filters).forEach((key) => {
        const el = filters[key];
        if (!el || key === "apply" || key === "reset") return;
        el.value = "";
        if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) window.jQuery(el).trigger("change.select2");
      });
      syncFilterUi();
      renderFiltered(true);
    });
  }

  function setTableDensity(mode) {
    const table = document.getElementById("goals-table");
    if (!table) return;
    table.classList.toggle("table-sm", mode === "compact");
  }

  function initFilterSelect2() {
    // Keep owner filter as native select to avoid duplicate label/search UI.
  }

  function isBudgetEnvelopeEnabled() {
    if (!isWorkspaceMode) return true;
    return Boolean(getEl("goal-budget-enabled")?.checked);
  }

  function syncBudgetEnvelopeUi() {
    const enabled = isBudgetEnvelopeEnabled();
    const content = getEl("goal-budget-content");
    const note = getEl("goal-budget-disabled-note");
    if (content) {
      content.classList.toggle("is-disabled", !enabled);
      content.setAttribute("aria-hidden", enabled ? "false" : "true");
    }
    if (note) note.classList.toggle("d-none", enabled);
    syncGoalHorizonUiState();
  }

  function renderBudgetYearRows(existing) {
    if (!goalBudgetTbody) return;
    const years = getPlanningYears();
    const map = new Map((existing || []).map((x) => [Number(x.year), x]));
    goalBudgetTbody.innerHTML = "";
    years.forEach((year) => {
      const row = map.get(year) || {};
      const tr = document.createElement("tr");
      tr.innerHTML =
        `<td><input class="form-control form-control-sm budget-year" value="${year}" readonly /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-rev text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.revenueTarget)}" /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-ebitda text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.ebitdaTarget)}" /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-capex text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.capexEnvelope)}" /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-opex text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.opexEnvelope)}" /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-savings text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.savingsTarget)}" /></td>` +
        `<td><input type="number" class="form-control form-control-sm budget-funding text-end" step="any" inputmode="decimal" value="${formatDecimalForInput(row.fundingPoolEnvelope ?? row.fundingPool)}" /></td>` +
        `<td><input class="form-control form-control-sm budget-commentary" maxlength="300" value="${String(row.commentary || "").replace(/"/g, "&quot;")}" /></td>`;
      tr.querySelectorAll("input").forEach((input) => {
        input.addEventListener("input", markDirty);
        input.addEventListener("change", markDirty);
        if (input.type === "number") {
          input.addEventListener("blur", () => normalizeMetricNumericInput(input));
        }
      });
      goalBudgetTbody.appendChild(tr);
    });
    syncBudgetEnvelopeUi();
  }

  function budgetSelectorForKey(key) {
    const selectorByKey = {
      revenue: ".budget-rev",
      revenueTarget: ".budget-rev",
      ebitda: ".budget-ebitda",
      ebitdaTarget: ".budget-ebitda",
      capex: ".budget-capex",
      capexEnvelope: ".budget-capex",
      opex: ".budget-opex",
      opexEnvelope: ".budget-opex",
      savings: ".budget-savings",
      savingsTarget: ".budget-savings",
      funding: ".budget-funding",
      fundingPoolEnvelope: ".budget-funding",
      fundingPool: ".budget-funding"
    };
    return selectorByKey[String(key || "").trim()] || "";
  }

  function promptDecimalValue(message) {
    const raw = window.prompt(message);
    if (raw == null) return null;
    const parsed = parseDecimal(raw);
    if (parsed === null && String(raw).trim() !== "") {
      notify("Enter a valid numeric value.", "warning");
      return undefined;
    }
    return parsed;
  }

  function fillBudgetColumn(key, value) {
    if (!isBudgetEnvelopeEnabled()) return;
    const selector = budgetSelectorForKey(key);
    if (!selector) {
      notify("Choose a valid budget column key.", "warning");
      return;
    }
    goalBudgetTbody?.querySelectorAll(selector).forEach((input) => {
      input.value = formatDecimalForInput(value);
    });
    markDirty();
  }

  function copyBudgetColumnDown(key) {
    if (!isBudgetEnvelopeEnabled()) return;
    const selector = budgetSelectorForKey(key);
    if (!selector) return;
    const rows = Array.from(goalBudgetTbody?.querySelectorAll("tr") || []);
    if (rows.length < 2) return;
    rows.slice(1).forEach((tr) => {
      const prev = tr.previousElementSibling;
      if (!prev) return;
      tr.querySelector(selector).value = prev.querySelector(selector)?.value || "";
    });
    markDirty();
  }

  function interpolateBudgetColumn(key) {
    if (!isBudgetEnvelopeEnabled()) return;
    const selector = budgetSelectorForKey(key);
    if (!selector) return;
    const rows = Array.from(goalBudgetTbody?.querySelectorAll("tr") || []);
    if (rows.length < 2) return;
    const startValue = promptDecimalValue("Start value:");
    if (startValue === null || startValue === undefined) return;
    const endValue = promptDecimalValue("End value:");
    if (endValue === null || endValue === undefined) return;
    const steps = rows.length - 1;
    rows.forEach((tr, idx) => {
      const value = steps === 0 ? startValue : startValue + ((endValue - startValue) * idx / steps);
      tr.querySelector(selector).value = Number.isFinite(value) ? formatDecimalForInput(value) : "";
    });
    markDirty();
  }

  function clearBudgetColumn(key) {
    if (!isBudgetEnvelopeEnabled()) return;
    const selector = budgetSelectorForKey(key);
    if (!selector) return;
    goalBudgetTbody?.querySelectorAll(selector).forEach((input) => {
      input.value = "";
    });
    markDirty();
  }

  function collectYearlyBudgetsFromDom(options = {}) {
    const { includeEmpty = false, ignoreToggle = false } = options;
    if (!ignoreToggle && !isBudgetEnvelopeEnabled()) return [];
    return Array.from(goalBudgetTbody?.querySelectorAll("tr") || []).map((tr) => ({
      year: Number(tr.querySelector(".budget-year")?.value || 0),
      revenueTarget: parseDecimal(tr.querySelector(".budget-rev")?.value),
      ebitdaTarget: parseDecimal(tr.querySelector(".budget-ebitda")?.value),
      capexEnvelope: parseDecimal(tr.querySelector(".budget-capex")?.value),
      opexEnvelope: parseDecimal(tr.querySelector(".budget-opex")?.value),
      savingsTarget: parseDecimal(tr.querySelector(".budget-savings")?.value),
      fundingPoolEnvelope: parseDecimal(tr.querySelector(".budget-funding")?.value),
      fundingPool: parseDecimal(tr.querySelector(".budget-funding")?.value),
      commentary: String(tr.querySelector(".budget-commentary")?.value || "").trim() || null
    })).filter((x) => Number.isInteger(x.year) && x.year > 0)
      .filter((x) => includeEmpty || budgetRowHasData(x));
  }

  function syncBudgetYearRowsWithHorizonChange(options = {}) {
    const { skipConfirm = false, skipMarkDirty = false } = options;
    const years = getPlanningYears();
    const enabled = isBudgetEnvelopeEnabled();
    const existing = collectYearlyBudgetsFromDom({ includeEmpty: true, ignoreToggle: true });
    if (enabled && !skipConfirm) {
      const removedRowsWithData = existing.some((row) => !years.includes(Number(row.year)) && budgetRowHasData(row));
      if (removedRowsWithData) {
        const ok = window.confirm("Changing planning horizon will remove yearly budget values outside the new horizon. Continue?");
        if (!ok) return false;
      }
    }
    renderBudgetYearRows(existing);
    if (!skipMarkDirty) markDirty();
    return true;
  }

  function updateSourceSummary() {
    const host = document.getElementById("goal-source-summary");
    if (!host) return;
    if (creationModeCode !== "Template") {
      host.className = "goal-source-summary-card is-empty";
      host.innerHTML = `<div class="goal-source-summary-name">Blank Goal create</div><div class="goal-source-summary-note">Start directly in the governed wizard, or choose a Goal Template to prefill the draft.</div>`;
      return;
    }
    if (!sourceTemplateId) {
      host.className = "goal-source-summary-card is-empty";
      host.innerHTML = `<div class="goal-source-summary-name">No Goal Template selected</div><div class="goal-source-summary-note">Browse the catalog to pick a Goal Template and prefill the wizard.</div>`;
      return;
    }
    const templateName = selectedSourceMeta?.name || sourceTemplateId;
    const templateCategory = selectedSourceMeta?.category || "";
    const templateVersion = selectedSourceMeta?.version ?? sourceTemplateVersion;
    const templateNote = selectedSourceMeta?.note || "Values prefilled from the selected Goal Template; adjust before save.";
    host.className = "goal-source-summary-card";
    host.innerHTML =
      `<div class="goal-source-summary-meta">` +
      `<span class="badge bg-label-primary">Goal Template</span>` +
      (templateVersion != null ? `<span class="badge bg-label-secondary">Version ${escapeHtml(templateVersion)}</span>` : "") +
      (templateCategory ? `<span class="badge bg-label-secondary">${escapeHtml(templateCategory)}</span>` : "") +
      `</div>` +
      `<div class="goal-source-summary-name">${escapeHtml(templateName)}</div>` +
      `<div class="small text-muted">Template ID: <code>${escapeHtml(sourceTemplateId)}</code></div>` +
      `<div class="goal-source-summary-note">${escapeHtml(templateNote)}</div>`;
  }

  function normalizeCatalogItems(raw) {
    if (Array.isArray(raw)) return raw;
    if (Array.isArray(raw?.items)) return raw.items;
    if (Array.isArray(raw?.Items)) return raw.Items;
    return [];
  }

  function normalizeCatalogRow(row) {
    if (!row || typeof row !== "object") return null;
    const rawType = String(row.type ?? row.Type ?? row.templateType ?? row.TemplateType ?? row.itemType ?? row.ItemType ?? "").trim();
    const goalType = String(row.categoryOrType ?? row.CategoryOrType ?? row.category ?? row.Category ?? "").trim();
    return {
      id: String(row.id ?? row.Id ?? row.templateCode ?? row.TemplateCode ?? "").trim(),
      name: String(row.name ?? row.Name ?? row.templateCode ?? row.TemplateCode ?? "").trim(),
      category: goalType,
      goalType,
      statement: String(row.statement ?? row.Statement ?? row.description ?? row.Description ?? "").trim(),
      templateType: String(row.templateType ?? row.TemplateType ?? rawType).trim(),
      itemType: String(row.itemType ?? row.ItemType ?? "").trim(),
      owner: String(row.owner ?? row.Owner ?? "").trim(),
      entityScope: String(row.entityScope ?? row.EntityScope ?? "").trim(),
      status: String(row.status ?? row.Status ?? row.lifecycleStatus ?? row.LifecycleStatus ?? "").trim(),
      lifecycleStatus: String(row.lifecycleStatus ?? row.LifecycleStatus ?? row.status ?? row.Status ?? "").trim(),
      version: row.version ?? row.Version ?? row.versionLabel ?? row.VersionLabel ?? null
    };
  }

  function isGoalTemplateType(row) {
    const type = String(row?.templateType || row?.itemType || "").trim().toLowerCase();
    return type === "goal" || type === "goaltemplate" || type === "goal template";
  }

  function normalizeSourceVersion(value) {
    const n = Number(value);
    return Number.isInteger(n) ? n : null;
  }

  function fillGoalSourceTypeFilter(rows) {
    const el = document.getElementById("goal-source-picker-type");
    if (!el) return;
    const previous = String(el.value || "");
    const values = [...new Set((rows || []).map((row) => String(row?.goalType || row?.category || "").trim()).filter(Boolean))];
    el.innerHTML = `<option value="">All types</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("")}`;
    if (previous && values.includes(previous)) el.value = previous;
  }

  function fillGoalSourceEntityScopeFilter(rows) {
    const el = document.getElementById("goal-source-picker-entity-scope");
    if (!el) return;
    const previous = String(el.value || "");
    const values = [...new Set((rows || []).map((row) => String(row?.entityScope || "").trim()).filter(Boolean))];
    el.innerHTML = `<option value="">All entity scopes</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("")}`;
    if (previous && values.includes(previous)) el.value = previous;
  }

  function updateGoalSourcePickerContext() {
    const currentGoalEl = document.getElementById("goal-template-picker-current-goal");
    const currentTypeEl = document.getElementById("goal-template-picker-current-type");
    const currentScopeEl = document.getElementById("goal-template-picker-current-scope");
    const currentStatusEl = document.getElementById("goal-template-picker-current-status");
    const currentTemplateEl = document.getElementById("goal-template-picker-current-template");
    const warningEl = document.getElementById("goal-template-picker-context-warning");

    const draftName = String(getEl("goal-name")?.value || "").trim();
    const draftType = String(getEl("goal-category")?.value || "").trim();
    const entityScope = String(getEl("goal-entity-scope")?.value || getEl("goal-related-entity-scope-summary")?.value || "").trim();
    const lifecycle = String(getEl("goal-status")?.value || getEl("goal-status-readonly")?.value || "Draft").trim() || "Draft";

    if (currentGoalEl) currentGoalEl.textContent = draftName || "New Goal draft";
    if (currentTypeEl) currentTypeEl.textContent = draftType || "-";
    if (currentScopeEl) currentScopeEl.textContent = entityScope || "-";
    if (currentStatusEl) currentStatusEl.textContent = lifecycle || "Draft";
    if (currentTemplateEl) currentTemplateEl.textContent = selectedSourceMeta?.name || sourceTemplateId || "None selected";

    if (!warningEl) return;
    if (!pickerCatalogRows.length) {
      warningEl.textContent = "No Goal Templates are currently available in the strategy library.";
      warningEl.classList.remove("d-none");
      return;
    }
    warningEl.textContent = "";
    warningEl.classList.add("d-none");
  }

  function applyGoalSourcePickerFilters() {
    const q = String(document.getElementById("goal-source-picker-search")?.value || "").trim().toLowerCase();
    const selectedType = String(document.getElementById("goal-source-picker-type")?.value || "").trim().toLowerCase();
    const selectedEntityScope = String(document.getElementById("goal-source-picker-entity-scope")?.value || "").trim().toLowerCase();
    const rows = pickerCatalogRows.filter((row) => {
      const haystack = `${row.id} ${row.name} ${row.goalType || row.category} ${row.statement} ${row.owner} ${row.entityScope} ${row.status} ${row.templateType} ${row.itemType}`.toLowerCase();
      if (q && !haystack.includes(q)) return false;
      if (selectedType && String(row.goalType || row.category || "").trim().toLowerCase() !== selectedType) return false;
      if (selectedEntityScope && String(row.entityScope || "").trim().toLowerCase() !== selectedEntityScope) return false;
      return true;
    });
    const helperEl = document.getElementById("goal-template-picker-helper");
    if (helperEl) {
      const typeFocus = selectedType || "all goal types";
      helperEl.textContent = pickerCatalogRows.length
        ? `Showing Goal Templates from the strategy library for ${typeFocus}${rows.length !== pickerCatalogRows.length ? " and the current picker filters." : "."}`
        : "No Goal Templates are currently available in the strategy library.";
    }
    renderGoalSourcePickerRows(rows);
  }

  async function applyGoalTemplateDetail(templateId) {
    const detail = await window.strategyLibraryApi.template(templateId);
    const attrs = detail.attributes || {};
    const prefill = detail.goalPrefill || detail.GoalPrefill || null;
    selectedSourceMeta = {
      ...(selectedSourceMeta || {}),
      id: templateId,
      name: detail.name || selectedSourceMeta?.name || templateId,
      category: String(detail.category || detail.Category || selectedSourceMeta?.category || "").trim(),
      version: detail.version ?? selectedSourceMeta?.version ?? sourceTemplateVersion,
      note: "Values prefilled from the selected Goal Template; adjust before save."
    };
    if (getEl("goal-name")) getEl("goal-name").value = prefill?.name || detail.name || "";
    if (getEl("goal-statement")) getEl("goal-statement").value = prefill?.statement || attrs.Statement || attrs.statement || "";
    if (getEl("goal-category") && (prefill?.category || attrs.Category || attrs.category)) {
      getEl("goal-category").value = prefill?.category || attrs.Category || attrs.category;
    }
    if (getEl("goal-strategic-theme") && (prefill?.strategicThemeId || attrs.StrategicThemeId || attrs.strategicThemeId)) {
      getEl("goal-strategic-theme").value = prefill?.strategicThemeId || attrs.StrategicThemeId || attrs.strategicThemeId;
    }
    if (getEl("goal-entity-scope")) getEl("goal-entity-scope").value = prefill?.entityScope || detail.entityScope || "";
    if (getEl("goal-priority")) getEl("goal-priority").value = prefill?.priority || detail.priority || "";
    if (getEl("goal-change-log-ref")) getEl("goal-change-log-ref").value = prefill?.changeLogRef || "";
    if (getEl("goal-decision-reference")) getEl("goal-decision-reference").value = prefill?.decisionReference || "";
    if (getEl("goal-evidence-reference")) getEl("goal-evidence-reference").value = prefill?.evidenceReference || "";
    const ownerRoleSelect = getOwnerRoleEl();
    if (ownerRoleSelect && prefill?.owner) {
      const needle = String(prefill.owner || "").trim().toLowerCase();
      const ownerByValue = Array.from(ownerRoleSelect.options || []).find((o) => String(o.value || "").trim().toLowerCase() === needle);
      const ownerByText = Array.from(ownerRoleSelect.options || []).find((o) => String(o.textContent || "").trim().toLowerCase() === needle);
      if (ownerByValue) ownerRoleSelect.value = ownerByValue.value;
      else if (ownerByText) ownerRoleSelect.value = ownerByText.value;
    }
    if (getOwnerCompanyEl() && (prefill?.ownerCompanyId || detail?.ownerCompanyId)) {
      getOwnerCompanyEl().value = resolveCompanyId(prefill?.ownerCompanyId || detail?.ownerCompanyId || "") || "";
    }
    if (getOwnerPersonEl() && (prefill?.ownerPersonId || detail?.ownerPersonId)) {
      getOwnerPersonEl().value = resolveUserId(prefill?.ownerPersonId || detail?.ownerPersonId || "");
    }
    setPlanningInputFromRaw("goal-planning-start-year", prefill?.planningStartYear || "", false);
    setPlanningInputFromRaw("goal-planning-end-year", prefill?.planningEndYear || "", true);
    previousStartYearRaw = String(getEl("goal-planning-start-year")?.value || "").trim();
    previousEndYearRaw = String(getEl("goal-planning-end-year")?.value || "").trim();

    const gm = detail.goalMetrics || detail.GoalMetrics || [];
    if (metricHost) {
      metricHost.innerHTML = "";
      gm.forEach((m) => {
        const metric = {
          metricName: m.metricName || m.MetricName,
          metricType: m.metricType || m.MetricType,
          unitOfMeasure: m.unitOfMeasure || m.UnitOfMeasure,
          aggregationMethod: m.aggregationMethod || m.AggregationMethod,
          polarityCode: m.polarityCode || m.PolarityCode || "",
          thresholdModelCode: m.thresholdModelCode || m.ThresholdModelCode || "",
          reportingFrequencyCode: m.reportingFrequencyCode || m.ReportingFrequencyCode || "",
          cascadeMetric: m.cascadeMetric !== false,
          metricOrigin: m.metricOrigin || m.MetricOrigin || "Local",
          metricRole: m.metricRole || m.MetricRole || "Strategic",
          restrictionMode: m.restrictionMode || m.RestrictionMode || "GoalGovernedStructure",
          rollupEligible: m.rollupEligible !== false,
          yearlyValues: m.yearlyValues || m.YearlyValues || m.yearlyTargets || m.YearlyTargets || []
        };
        metricHost.appendChild(metricRow(metric));
      });
      collapseOtherMetrics(null);
    }

    const yearlyBudgets = detail.goalYearlyBudgets || detail.GoalYearlyBudgets || [];
    renderBudgetYearRows(yearlyBudgets.map((row) => ({
      year: row.year ?? row.Year,
      revenueTarget: row.revenueTarget ?? row.RevenueTarget,
      ebitdaTarget: row.ebitdaTarget ?? row.EbitdaTarget,
      capexEnvelope: row.capexEnvelope ?? row.CapexEnvelope,
      opexEnvelope: row.opexEnvelope ?? row.OpexEnvelope,
      savingsTarget: row.savingsTarget ?? row.SavingsTarget,
      fundingPoolEnvelope: row.fundingPoolEnvelope ?? row.FundingPoolEnvelope
    })));

    initGoalSelect2();
    fillOwnerPersonSelect({ keepCurrent: true });
    syncOwnerAccountableDisplay();
    syncGoalCompanyScopeUi();
    syncRelatedEntityScopeSummary();
    markDirty();
  }

  function renderGoalSourcePickerRows(rows) {
    const tbody = document.getElementById("goal-source-picker-tbody");
    if (!tbody) return;
    updateGoalSourcePickerContext();
    tbody.innerHTML = "";
    if (!(rows || []).length) {
      const tr = document.createElement("tr");
      tr.innerHTML = '<td colspan="9" class="text-center text-muted py-3">No matching Goal Templates found.</td>';
      tbody.appendChild(tr);
      return;
    }
    (rows || []).forEach((row) => {
      const tr = document.createElement("tr");
      tr.className = `goal-template-picker-row ${String(row.id || "").trim() === sourceTemplateId ? "table-active" : ""}`;
      tr.innerHTML = [
        `<td>${escapeHtml(row.id || "-")}</td>`,
        `<td>${escapeHtml(row.name || "-")}</td>`,
        `<td>${escapeHtml(row.statement || "-")}</td>`,
        `<td>${escapeHtml(row.goalType || row.category || "-")}</td>`,
        `<td>${escapeHtml(row.owner || "-")}</td>`,
        `<td>${escapeHtml(row.entityScope || "-")}</td>`,
        `<td>${escapeHtml(row.lifecycleStatus || row.status || "-")}</td>`,
        `<td>${escapeHtml(row.version ?? "-")}</td>`,
        `<td><button type="button" class="btn btn-sm btn-outline-primary goal-pick-source"${row.id ? "" : " disabled"}>Use</button></td>`
      ].join("");
      tr.querySelector(".goal-pick-source")?.addEventListener("click", async () => {
        try {
          sourceTemplateId = String(row.id || "").trim();
          sourceTemplateVersion = normalizeSourceVersion(row.version);
          selectedSourceMeta = {
            id: sourceTemplateId,
            name: row.name || sourceTemplateId,
            category: row.goalType || row.category || "",
            version: row.version ?? null,
            note: "Values prefilled from the selected Goal Template; adjust before save."
          };
          await applyGoalTemplateDetail(sourceTemplateId);
          updateSourceSummary();
          goalSourcePickerModal?.hide();
        } catch (err) {
          notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Load failed") || "Load failed", "error");
        }
      });
      tbody.appendChild(tr);
    });
  }

  async function loadGoalSourcePickerCatalog() {
    try {
      const data = await window.strategyLibraryApi.catalog({ page: 1, pageSize: 200, templateType: "Goal" }, { skipCache: true });
      pickerCatalogRows = normalizeCatalogItems(data).map(normalizeCatalogRow).filter(Boolean).filter((row) => isGoalTemplateType(row));
      fillGoalSourceTypeFilter(pickerCatalogRows);
      fillGoalSourceEntityScopeFilter(pickerCatalogRows);
      applyGoalSourcePickerFilters();
    } catch (err) {
      pickerCatalogRows = [];
      fillGoalSourceTypeFilter([]);
      fillGoalSourceEntityScopeFilter([]);
      renderGoalSourcePickerRows([]);
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Catalog failed") || "Catalog failed", "error");
    }
  }
  const getMultiValues = (el) => Array.from(el?.selectedOptions || []).map((x) => String(x.value || "").trim()).filter(Boolean);
  const getApplicableCompaniesElements = () => ({
    select: getEl("goal-applicable-companies"),
    root: getEl("goal-applicable-companies-picker"),
    toggle: getEl("goal-applicable-companies-toggle"),
    display: getEl("goal-applicable-companies-display"),
    panel: getEl("goal-applicable-companies-panel"),
    search: getEl("goal-applicable-companies-search"),
    options: getEl("goal-applicable-companies-options"),
    selectAll: getEl("goal-applicable-companies-select-all"),
    clearAll: getEl("goal-applicable-companies-clear-all")
  });
  const applicableCompaniesPlaceholder = () => {
    const { root } = getApplicableCompaniesElements();
    return String(root?.dataset?.placeholder || "Select applicable companies...").trim();
  };
  const isApplicableCompaniesPanelOpen = () => {
    const { panel } = getApplicableCompaniesElements();
    return Boolean(panel && !panel.classList.contains("d-none"));
  };
  const visibleApplicableOptionButtons = () => {
    const { options } = getApplicableCompaniesElements();
    return Array.from(options?.querySelectorAll(".es-company-multi-select-option") || []);
  };
  const setApplicableCompaniesPanelOpen = (open) => {
    const { root, toggle, panel, search, options } = getApplicableCompaniesElements();
    if (!root || !toggle || !panel) return;
    const allowOpen = open && !toggle.disabled;
    panel.classList.toggle("d-none", !allowOpen);
    toggle.classList.toggle("is-open", allowOpen);
    toggle.setAttribute("aria-expanded", allowOpen ? "true" : "false");
    if (allowOpen) {
      syncApplicableCompaniesPickerFromSelect();
      if (search) {
        search.focus();
        search.select?.();
      } else {
        options?.focus();
      }
      return;
    }
    applicableCompaniesPickerActiveIndex = -1;
  };
  const ensureApplicableOptionInView = (btn) => {
    if (!btn) return;
    const { options } = getApplicableCompaniesElements();
    if (!options) return;
    const top = options.scrollTop;
    const bottom = top + options.clientHeight;
    const itemTop = btn.offsetTop;
    const itemBottom = itemTop + btn.offsetHeight;
    if (itemTop < top) options.scrollTop = itemTop;
    if (itemBottom > bottom) options.scrollTop = itemBottom - options.clientHeight;
  };
  const setApplicableActiveIndex = (nextIndex) => {
    const buttons = visibleApplicableOptionButtons();
    if (!buttons.length) {
      applicableCompaniesPickerActiveIndex = -1;
      return;
    }
    const bounded = Math.max(0, Math.min(nextIndex, buttons.length - 1));
    applicableCompaniesPickerActiveIndex = bounded;
    buttons.forEach((btn, idx) => {
      const isActive = idx === bounded;
      btn.classList.toggle("is-active", isActive);
      if (isActive) ensureApplicableOptionInView(btn);
    });
  };
  const applyApplicableSelections = (values, dispatchChange = true) => {
    const { select } = getApplicableCompaniesElements();
    if (!select) return;
    const selected = new Set((values || []).map((v) => String(v || "").trim()).filter(Boolean));
    let changed = false;
    Array.from(select.options || []).forEach((opt) => {
      const shouldBeSelected = selected.has(String(opt.value || "").trim());
      if (opt.selected !== shouldBeSelected) {
        opt.selected = shouldBeSelected;
        changed = true;
      }
    });
    if (dispatchChange && changed) {
      select.dispatchEvent(new Event("change", { bubbles: true }));
    } else if (!dispatchChange || !changed) {
      syncApplicableCompaniesPickerFromSelect();
    }
  };
  const toggleApplicableCompanyValue = (value) => {
    const { select } = getApplicableCompaniesElements();
    if (!select) return;
    const targetValue = String(value || "").trim();
    if (!targetValue) return;
    const targetOption = Array.from(select.options || []).find((opt) => String(opt.value || "").trim() === targetValue);
    if (!targetOption) return;
    targetOption.selected = !targetOption.selected;
    select.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const syncApplicableCompaniesDisplay = () => {
    const { select, display, toggle } = getApplicableCompaniesElements();
    if (!display || !select) return;
    const selectedOptions = Array.from(select.selectedOptions || []);
    const names = selectedOptions.map((opt) => String(opt.textContent || "").trim()).filter(Boolean);
    const count = names.length;
    if (!count) {
      display.textContent = applicableCompaniesPlaceholder();
      if (toggle) toggle.title = "";
      return;
    }
    if (count <= 2) {
      display.textContent = names.join(", ");
    } else {
      const preview = names.slice(0, 2).join(", ");
      display.textContent = `${preview} +${count - 2} more (${count} selected)`;
    }
    if (toggle) toggle.title = names.join(", ");
  };
  const renderApplicableCompaniesOptions = () => {
    const { select, search, options } = getApplicableCompaniesElements();
    if (!select || !options) return;
    const query = String(search?.value || "").trim().toLowerCase();
    const all = Array.from(select.options || []).map((opt) => ({
      value: String(opt.value || "").trim(),
      label: String(opt.textContent || "").trim(),
      selected: Boolean(opt.selected)
    }));
    const filtered = all.filter((row) => {
      if (!row.value || !row.label) return false;
      return !query || row.label.toLowerCase().includes(query);
    });
    options.innerHTML = "";
    if (!filtered.length) {
      const empty = document.createElement("div");
      empty.className = "es-company-multi-select-empty";
      empty.textContent = "No matching companies.";
      options.appendChild(empty);
      applicableCompaniesPickerActiveIndex = -1;
      return;
    }
    filtered.forEach((row, idx) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "es-company-multi-select-option";
      btn.dataset.companyValue = row.value;
      btn.setAttribute("role", "option");
      btn.setAttribute("aria-selected", row.selected ? "true" : "false");
      btn.innerHTML = `<input type="checkbox" class="form-check-input" ${row.selected ? "checked" : ""} tabindex="-1" aria-hidden="true" /><span>${escapeHtml(row.label)}</span>`;
      if (idx === 0) btn.classList.add("is-active");
      options.appendChild(btn);
    });
    setApplicableActiveIndex(0);
  };
  const syncApplicableCompaniesPickerFromSelect = () => {
    const { select, toggle } = getApplicableCompaniesElements();
    if (!select || !toggle) return;
    const isDisabled = Boolean(select.disabled);
    toggle.disabled = isDisabled;
    toggle.classList.toggle("disabled", isDisabled);
    if (isDisabled) setApplicableCompaniesPanelOpen(false);
    syncApplicableCompaniesDisplay();
    if (isApplicableCompaniesPanelOpen()) renderApplicableCompaniesOptions();
  };
  function onApplicableCompaniesPickerKeyDown(event) {
    const { search } = getApplicableCompaniesElements();
    const open = isApplicableCompaniesPanelOpen();
    if (!open && (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      setApplicableCompaniesPanelOpen(true);
      return;
    }
    if (!open) return;
    const buttons = visibleApplicableOptionButtons();
    if (!buttons.length) {
      if (event.key === "Escape") {
        event.preventDefault();
        setApplicableCompaniesPanelOpen(false);
      }
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setApplicableActiveIndex(applicableCompaniesPickerActiveIndex + 1);
      return;
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      setApplicableActiveIndex(applicableCompaniesPickerActiveIndex - 1);
      return;
    }
    if (event.key === "Home") {
      event.preventDefault();
      setApplicableActiveIndex(0);
      return;
    }
    if (event.key === "End") {
      event.preventDefault();
      setApplicableActiveIndex(buttons.length - 1);
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      if (event.target === search && event.key === " ") return;
      event.preventDefault();
      const activeBtn = buttons[applicableCompaniesPickerActiveIndex] || buttons[0];
      const value = String(activeBtn?.dataset?.companyValue || "").trim();
      if (value) toggleApplicableCompanyValue(value);
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      setApplicableCompaniesPanelOpen(false);
      const { toggle } = getApplicableCompaniesElements();
      toggle?.focus();
    }
  }
  function initApplicableCompaniesPicker() {
    const { select, root, toggle, panel, search, options, selectAll, clearAll } = getApplicableCompaniesElements();
    if (!select || !root || !toggle || !panel || !options) return;
    if (root.dataset.initialized === "1") {
      syncApplicableCompaniesPickerFromSelect();
      return;
    }
    root.dataset.initialized = "1";
    toggle.addEventListener("click", () => {
      setApplicableCompaniesPanelOpen(!isApplicableCompaniesPanelOpen());
    });
    toggle.addEventListener("keydown", onApplicableCompaniesPickerKeyDown);
    panel.addEventListener("keydown", onApplicableCompaniesPickerKeyDown);
    search?.addEventListener("input", () => {
      renderApplicableCompaniesOptions();
    });
    options.addEventListener("click", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      if (!btn) return;
      const value = String(btn.dataset.companyValue || "").trim();
      if (!value) return;
      toggleApplicableCompanyValue(value);
    });
    options.addEventListener("mousemove", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      if (!btn) return;
      const buttons = visibleApplicableOptionButtons();
      const idx = buttons.indexOf(btn);
      if (idx >= 0) setApplicableActiveIndex(idx);
    });
    selectAll?.addEventListener("click", () => {
      const values = Array.from(select.options || []).map((opt) => String(opt.value || "").trim()).filter(Boolean);
      applyApplicableSelections(values, true);
      search?.focus();
    });
    clearAll?.addEventListener("click", () => {
      applyApplicableSelections([], true);
      search?.focus();
    });
    select.addEventListener("change", () => {
      syncApplicableCompaniesPickerFromSelect();
    });
    document.addEventListener("mousedown", (event) => {
      if (!isApplicableCompaniesPanelOpen()) return;
      const target = event.target;
      if (root.contains(target)) return;
      setApplicableCompaniesPanelOpen(false);
    });
    modalEl?.addEventListener("hide.bs.modal", () => {
      setApplicableCompaniesPanelOpen(false);
    });
    syncApplicableCompaniesPickerFromSelect();
  }
  const isValidAbsoluteUrl = (value) => {
    const text = String(value || "").trim();
    if (!text) return true;
    try {
      const parsed = new URL(text);
      return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch {
      return false;
    }
  };
  function companyLabelById(id) {
    return String(workbook.companyDisplayName?.(id) || "").trim();
  }
  function resolveCompanyId(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const options = workbook.companyOptions?.() || [];
    const byValue = options.find((x) => String(x.value || "").toLowerCase() === raw.toLowerCase());
    if (byValue?.value) return String(byValue.value);
    const byLabel = options.find((x) => String(x.label || "").toLowerCase() === raw.toLowerCase());
    if (byLabel?.value) return String(byLabel.value);
    return "";
  }
  function resolveCompanyIdFromElement(el) {
    if (!el) return "";
    const direct = resolveCompanyId(el.value);
    if (direct) return direct;
    const selectedText = String(el.selectedOptions?.[0]?.text || "").trim();
    const mapped = resolveCompanyId(selectedText);
    if (mapped) return mapped;
    return "";
  }
  function resolveCompanyIds(values) {
    return (values || []).map(resolveCompanyId).filter(Boolean);
  }
  function normalizeCompanyScopeForMode(scopeMode, primaryCompanyId, applicableCompanyIds) {
    const mode = String(scopeMode || "").trim();
    const primary = String(primaryCompanyId || "").trim();
    let applicable = resolveCompanyIds(applicableCompanyIds || []);
    if (mode === "Enterprise") {
      return { primaryCompanyId: null, applicableCompanyIds: [] };
    }
    if (mode === "SingleCompany") {
      if (!primary) return { primaryCompanyId: null, applicableCompanyIds: [] };
      return { primaryCompanyId: primary, applicableCompanyIds: [primary] };
    }
    if (mode === "MultiCompany" || mode === "AppliesToSelectedCompanies") {
      return { primaryCompanyId: null, applicableCompanyIds: applicable };
    }
    return { primaryCompanyId: null, applicableCompanyIds: applicable };
  }
  function toStoredScopeMode(scopeModeUi) {
    const mode = String(scopeModeUi || "").trim();
    return mode === "Enterprise" ? "Enterprise" : "MultiCompany";
  }
  function toUiScopeMode(scopeModeStored) {
    const mode = String(scopeModeStored || "").trim();
    return mode === "Enterprise" ? "Enterprise" : "AppliesToSelectedCompanies";
  }
  const toStartDateIso = (year) => (year ? `${year}-01-01` : null);
  const toEndDateIso = (year) => (year ? `${year}-12-31` : null);
  const fromDateToYear = (value) => {
    if (value == null || value === "") return "";
    if (typeof value === "number" && Number.isFinite(value)) {
      const d = new Date(value);
      return Number.isNaN(d.getTime()) ? "" : String(d.getUTCFullYear());
    }
    const s = String(value).trim();
    if (yearRegex.test(s.slice(0, 4))) return s.slice(0, 4);
    const d = new Date(s);
    return Number.isNaN(d.getTime()) ? "" : String(d.getUTCFullYear());
  };
  const parseDmyToIso = (value) => {
    const s = String(value || "").trim();
    const m = s.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
    if (!m) return "";
    const day = Number(m[1]);
    const month = Number(m[2]);
    const year = Number(m[3]);
    const dt = new Date(year, month - 1, day);
    if (dt.getFullYear() !== year || dt.getMonth() !== month - 1 || dt.getDate() !== day) return "";
    return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  };
  const formatIsoToDmy = (value) => {
    const iso = normalizeIsoDate(value);
    if (!iso) return "";
    const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) return "";
    return `${m[3]}/${m[2]}/${m[1]}`;
  };
  const formatIsoRangeToDmy = (startIso, endIso) => {
    const start = formatIsoToDmy(startIso);
    const end = formatIsoToDmy(endIso);
    if (!start && !end) return "";
    if (start && end) return `${start} - ${end}`;
    return start || end;
  };
  const normalizeIsoDate = (value) => {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const isoPrefix = raw.match(/^(\d{4})-(\d{2})-(\d{2})/);
    const isoCandidate = isoPrefix ? `${isoPrefix[1]}-${isoPrefix[2]}-${isoPrefix[3]}` : "";
    const iso = parseDmyToIso(raw) || isoCandidate || (/^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : "");
    if (!iso) return "";
    const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (!m) return "";
    const y = Number(m[1]);
    const mo = Number(m[2]);
    const d = Number(m[3]);
    const dt = new Date(y, mo - 1, d);
    if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return "";
    return iso;
  };
  const goalHorizonIsoFromInput = (id) => {
    const el = getEl(id);
    if (!el) return "";
    return normalizeIsoDate(el.value);
  };
  const planningYearFromInput = (id) => parseYear(goalHorizonIsoFromInput(id));
  const setPlanningInputIso = (id, iso, triggerChange = false) => {
    const el = getEl(id);
    if (!el) return;
    el.value = normalizeIsoDate(iso);
    if (triggerChange) el.dispatchEvent(new Event("change", { bubbles: true }));
  };
  const setPlanningInputFromYear = (id, year, isEnd, triggerChange = false) => {
    const y = parseYear(year);
    if (!y) {
      setPlanningInputIso(id, "", triggerChange);
      return;
    }
    const iso = isEnd ? `${y}-12-31` : `${y}-01-01`;
    setPlanningInputIso(id, iso, triggerChange);
  };
  const setPlanningInputFromRaw = (id, raw, isEnd, triggerChange = false) => {
    const text = String(raw || "").trim();
    if (!text) {
      setPlanningInputIso(id, "", triggerChange);
      return;
    }
    const iso = normalizeIsoDate(text);
    if (iso) {
      setPlanningInputIso(id, iso, triggerChange);
      return;
    }
    const year = parseYear(text);
    if (year) {
      setPlanningInputFromYear(id, year, isEnd, triggerChange);
      return;
    }
    setPlanningInputIso(id, "", triggerChange);
  };
  const setPlanningInputBounds = (id, bounds, isEnd) => {
    const el = getEl(id);
    if (!el) return;
    const minIso = bounds?.minIso || "1900-01-01";
    const maxIso = bounds?.maxIso || "2100-12-31";
    if (el._flatpickr) {
      el._flatpickr.set("minDate", minIso);
      el._flatpickr.set("maxDate", maxIso);
      return;
    }
    el.min = minIso;
    el.max = maxIso;
    if (isEnd && el.placeholder === "dd/mm/yyyy") {
      // no-op; keeps intent explicit for end-date input
    }
  };
  const strategyPeriodLabel = (period) => {
    const name = String(period?.name || "").trim();
    const type = String(
      period?.periodType ||
      period?.planningCycleType ||
      period?.scenarioType ||
      period?.type ||
      ""
    ).trim();
    const bounds = strategyPeriodDateBounds(period);
    const startYear = bounds?.minYear ? String(bounds.minYear) : fromDateToYear(period?.startDate);
    const endYear = bounds?.maxYear ? String(bounds.maxYear) : fromDateToYear(period?.endDate);
    const dateRange = bounds ? formatIsoRangeToDmy(bounds.minIso, bounds.maxIso) : "";
    const periodRange = startYear && endYear ? `${startYear}\u2013${endYear}` : "No period";
    const owner = String(
      period?.ownerDisplayName ||
      period?.ownerName ||
      period?.owner ||
      period?.ownerId ||
      ""
    ).trim() || "Unassigned";
    const envelope = startYear && endYear ? `${startYear}\u2013${endYear} Strategy Period` : "Strategy Period";
    const envelopeWithDates = dateRange ? `${envelope} (${dateRange})` : envelope;
    return [envelopeWithDates, name || "Unnamed", type || "N/A", owner].join(" | ");
  };
  const strategyPeriodDateBounds = (period) => {
    const startYear = parseYear(period?.startYear);
    const endYear = parseYear(period?.endYear);
    const minIso =
      normalizeIsoDate(period?.startDate) ||
      normalizeIsoDate(period?.effectiveFrom) ||
      (startYear ? `${startYear}-01-01` : "");
    const maxIso =
      normalizeIsoDate(period?.endDate) ||
      normalizeIsoDate(period?.effectiveTo) ||
      (endYear ? `${endYear}-12-31` : "");
    if (!minIso || !maxIso || maxIso < minIso) return null;
    return {
      minIso,
      maxIso,
      minYear: parseYear(minIso),
      maxYear: parseYear(maxIso)
    };
  };
  const isGoalAssignableStrategyPeriodStatus = (value) => {
    const s = String(value || "").trim().toLowerCase();
    return s === "active";
  };
  const strategyPeriodStatusLabel = (value) => {
    const text = String(value || "").trim();
    if (!text) return "Inactive";
    return text.charAt(0).toUpperCase() + text.slice(1).toLowerCase();
  };
  const strategyPeriodMatchesGoalScope = (period, companyId) => {
    const scopedCompanyId = String(companyId || "").trim();
    if (!scopedCompanyId) return true;
    return String(period?.companyId || "").trim().toLowerCase() === scopedCompanyId.toLowerCase();
  };
  const strategyPeriodOptionLabel = (period, { includeCurrent = false } = {}) => {
    const tags = [];
    if (includeCurrent) tags.push("Current");
    if (!isGoalAssignableStrategyPeriodStatus(period?.status)) {
      tags.push(strategyPeriodStatusLabel(period?.status));
    }
    return tags.length ? `${strategyPeriodLabel(period)} [${tags.join(", ")}]` : strategyPeriodLabel(period);
  };
  async function listGoalStrategyPeriodsForLookup(query = {}) {
    const api = window.strategyPlanningApi || {};
    const scopeCompanyId = String(query.companyId || "").trim();

    if (typeof api.listStrategyPeriods === "function") {
      const response = await api.listStrategyPeriods(undefined, undefined, undefined);
      const rows = Array.isArray(response) ? response : (response?.items || []);
      return (rows || []).filter((period) => strategyPeriodMatchesGoalScope(period, scopeCompanyId));
    }

    if (typeof api.listActiveByScope === "function") {
      const response = await api.listActiveByScope(scopeCompanyId || undefined, null, null, null);
      return Array.isArray(response) ? response : (response?.items || []);
    }

    return [];
  };
  function getStrategyPeriodScopeQuery() {
    const scopeMode = String(getEl("goal-scope-mode")?.value || "Enterprise").trim();
    const ownerCompanyId = resolveCompanyIdFromElement(getOwnerCompanyEl()) || "";
    const primaryCompanyId = resolveCompanyIdFromElement(getEl("goal-primary-company")) || ownerCompanyId || "";
    const applicableCompanyIds = resolveCompanyIds(getMultiValues(getEl("goal-applicable-companies")));
    const companyId = scopeMode === "Enterprise" ? "" : (primaryCompanyId || applicableCompanyIds[0] || "");
    return { companyId: companyId || undefined };
  }
  function applyGoalStrategyPeriodConstraints({ applyDefaults = false } = {}) {
    const helper = getEl("goal-strategy-period-helper");
    const summary = getEl("goal-allowed-horizon-summary");
    const startEl = getEl("goal-planning-start-year");
    const endEl = getEl("goal-planning-end-year");
    const bounds = strategyPeriodDateBounds(selectedStrategyPeriodContext);
    setPlanningInputBounds("goal-planning-start-year", bounds, false);
    setPlanningInputBounds("goal-planning-end-year", bounds, true);
    if (helper) {
      helper.textContent = bounds
        ? "Start Date and End Date are derived from the selected Strategy Period envelope."
        : "Select an Active Strategy Period to derive Goal Start and End Date. Draft and Archived periods are reference-only.";
    }
    if (summary) {
      summary.textContent = bounds
        ? `Allowed horizon: ${formatIsoToDmy(bounds.minIso)} to ${formatIsoToDmy(bounds.maxIso)}. You may narrow, but not extend.`
        : "Allowed horizon: ---. You may narrow, but not extend.";
    }
    syncGoalHorizonUiState();
    if (!applyDefaults || !bounds) return;
    let changed = false;
    const startIso = goalHorizonIsoFromInput("goal-planning-start-year");
    const endIso = goalHorizonIsoFromInput("goal-planning-end-year");
    if (startEl && startIso !== bounds.minIso) {
      setPlanningInputIso("goal-planning-start-year", bounds.minIso, false);
      changed = true;
    }
    if (endEl && endIso !== bounds.maxIso) {
      setPlanningInputIso("goal-planning-end-year", bounds.maxIso, false);
      changed = true;
    }
    if (changed) {
      previousStartYearRaw = String(startEl?.value || "").trim();
      previousEndYearRaw = String(endEl?.value || "").trim();
      startEl?.dispatchEvent(new Event("change", { bubbles: true }));
      endEl?.dispatchEvent(new Event("change", { bubbles: true }));
    }

    const period = selectedStrategyPeriodContext || {};
    const periodScopeParts = [];
    const periodCompany = String(period.companyName || period.companyCode || period.companyId || "").trim();
    const periodBu = String(period.businessUnitName || period.businessUnitCode || period.businessUnitId || "").trim();
    const periodRegion = String(period.regionName || period.regionCode || period.regionId || "").trim();
    if (periodCompany) periodScopeParts.push(`Company: ${periodCompany}`);
    if (periodBu) periodScopeParts.push(`BU/Function: ${periodBu}`);
    if (periodRegion) periodScopeParts.push(`Region: ${periodRegion}`);
    const derivedScope = periodScopeParts.join(" | ");
    if (derivedScope) {
      const previewEl = getEl("goal-planning-scope-preview");
      if (previewEl && !String(previewEl.value || "").trim()) previewEl.value = derivedScope;
    }
    syncRelatedEntityScopeSummary();
    syncOwnerAccountableDisplay();
  }
  function syncSelectedGoalStrategyPeriod({ applyDefaults = false } = {}) {
    const selectedId = String(getEl("goal-strategy-period")?.value || "").trim();
    selectedStrategyPeriodContext = strategyPeriodsById.get(selectedId) || activeStrategyPeriods.find((x) => String(x.id || "") === selectedId) || null;
    applyGoalStrategyPeriodConstraints({ applyDefaults });
  }
  async function ensureStrategyPeriodInLookup(selectEl, periodId) {
    const id = String(periodId || "").trim();
    if (!selectEl || !id || strategyPeriodsById.has(id)) return;
    try {
      const period = await window.strategyPlanningApi?.getStrategyPeriod?.(id);
      if (!period) return;
      strategyPeriodsById.set(id, period);
      const option = document.createElement("option");
      option.value = id;
      option.textContent = strategyPeriodOptionLabel(period, { includeCurrent: !isGoalAssignableStrategyPeriodStatus(period.status) });
      selectEl.appendChild(option);
    } catch {
      // If period lookup fails, keep only active catalog options.
    }
  }
  async function refreshGoalStrategyPeriodLookup({ applyDefaults = false, preserveId = "" } = {}) {
    const selectEl = getEl("goal-strategy-period");
    if (!selectEl) return;
    const selectedBefore = String(preserveId || selectEl.value || "").trim();
    const query = getStrategyPeriodScopeQuery();
    let lookupRows = [];
    try {
      lookupRows = await listGoalStrategyPeriodsForLookup(query);
    } catch {
      lookupRows = [];
    }
    lookupRows = (lookupRows || []).filter(Boolean);
    lookupRows.sort((left, right) => {
      const leftActive = isGoalAssignableStrategyPeriodStatus(left?.status) ? 1 : 0;
      const rightActive = isGoalAssignableStrategyPeriodStatus(right?.status) ? 1 : 0;
      return rightActive - leftActive;
    });
    activeStrategyPeriods = lookupRows.filter((period) => isGoalAssignableStrategyPeriodStatus(period?.status));
    strategyPeriodsById = new Map();
    selectEl.innerHTML = '<option value="">Select strategy period (Active only)...</option>';
    lookupRows.forEach((period) => {
      const periodId = String(period.id || "").trim();
      if (!periodId) return;
      strategyPeriodsById.set(periodId, period);
      const option = document.createElement("option");
      option.value = periodId;
      option.textContent = strategyPeriodOptionLabel(period);
      if (!isGoalAssignableStrategyPeriodStatus(period.status) && periodId !== selectedBefore) {
        option.disabled = true;
      }
      selectEl.appendChild(option);
    });
    if (selectedBefore && strategyPeriodsById.has(selectedBefore)) {
      selectEl.value = selectedBefore;
    } else if (selectedBefore) {
      await ensureStrategyPeriodInLookup(selectEl, selectedBefore);
      if (strategyPeriodsById.has(selectedBefore)) {
        selectEl.value = selectedBefore;
      } else {
        selectEl.value = "";
      }
    } else {
      selectEl.value = "";
    }
    if (window.jQuery && window.jQuery(selectEl).hasClass("select2-hidden-accessible")) {
      window.jQuery(selectEl).trigger("change.select2");
    }
    syncSelectedGoalStrategyPeriod({ applyDefaults });
  }

  function applyMetricCatalogDefaults(row) {
    const metricName = String(row.querySelector(".metric-name")?.value || "").trim().toLowerCase();
    if (!metricName || !metricCatalogByName.size) return;
    const hit = metricCatalogByName.get(metricName);
    if (!hit) return;
    if (row.querySelector(".metric-unit") && !row.querySelector(".metric-unit").value) row.querySelector(".metric-unit").value = hit.unitOfMeasure || "";
    if (row.querySelector(".metric-aggregation") && !row.querySelector(".metric-aggregation").value) row.querySelector(".metric-aggregation").value = hit.aggregationMethod || "";
    if (row.querySelector(".metric-threshold-model") && !row.querySelector(".metric-threshold-model").value) row.querySelector(".metric-threshold-model").value = hit.thresholdModel || "";
    if (row.querySelector(".metric-reporting-frequency") && !row.querySelector(".metric-reporting-frequency").value) row.querySelector(".metric-reporting-frequency").value = hit.reportingFrequency || "";
    if (row.querySelector(".metric-polarity") && !row.querySelector(".metric-polarity").value) row.querySelector(".metric-polarity").value = hit.polarity || "";
    if (row.querySelector(".metric-def-id") && !row.querySelector(".metric-def-id").value) row.querySelector(".metric-def-id").value = hit.id || "";
  }

  function markDirty() {
    isDirty = true;
    activeValidationMode = normalizeValidationMode("auto", collectCreateRequest());
    applyValidation();
  }

  function deriveRelatedEntityScopeSummary() {
    const businessUnit = String(getEl("goal-business-unit")?.value || "").trim();
    const region = String(getEl("goal-region")?.value || "").trim();
    const scopeParts = [];
    if (businessUnit) scopeParts.push(`BU/Function: ${businessUnit}`);
    if (region) scopeParts.push(`Region: ${region}`);
    const baseScope = scopeParts.join(" | ");
    const hiddenScopeEl = getEl("goal-entity-scope");
    if (hiddenScopeEl) hiddenScopeEl.value = baseScope;
    const scopeModeUi = String(getEl("goal-scope-mode")?.value || "Enterprise").trim();
    const appliesAll = Boolean(getEl("goal-applies-to-all-companies")?.checked);
    const applicableIds = resolveCompanyIds(getMultiValues(getEl("goal-applicable-companies")));
    const companyParts = [];
    if (scopeModeUi === "Enterprise" || appliesAll) {
      companyParts.push("All Companies");
    } else if (applicableIds.length > 0) {
      if (applicableIds.length <= 2) {
        companyParts.push(applicableIds.map((id) => ownerCompanyLabelByValue(id) || id).join(", "));
      } else {
        companyParts.push(`${applicableIds.length} companies selected`);
      }
    } else {
      const ownerCompanyId = String(getOwnerCompanyEl()?.value || "").trim();
      if (ownerCompanyId) companyParts.push(ownerCompanyLabelByValue(ownerCompanyId) || ownerCompanyId);
    }
    const left = baseScope || "Scope not specified";
    const right = companyParts.join(" | ").trim();
    return right ? `${left} | ${right}` : left;
  }

  function syncRelatedEntityScopeSummary() {
    const el = getEl("goal-related-entity-scope-summary");
    const previewEl = getEl("goal-planning-scope-preview");
    const summary = deriveRelatedEntityScopeSummary();
    if (el) el.value = summary;
    if (previewEl) previewEl.value = summary || "Company applicability will define the strategic scope summary.";
  }

  function syncGoalCompanyScopeUi() {
    const mode = String(getEl("goal-scope-mode")?.value || "Enterprise");
    const appliesAll = getEl("goal-applies-to-all-companies");
    const primary = getEl("goal-primary-company");
    const ownerCompany = getOwnerCompanyEl();
    const applicable = getEl("goal-applicable-companies");
    const modeHint = getEl("goal-company-mode-hint");
    const primaryHint = getEl("goal-primary-company-hint");
    const applicableHint = getEl("goal-applicable-companies-hint");
    if (!applicable) return;
    const primaryHost = primary?.closest(".form-control-validation") || primary?.parentElement;
    const applicableHost = applicable.closest(".form-control-validation") || applicable.parentElement;
    const isEnterprise = mode === "Enterprise";
    const isSelectedCompanies = mode === "MultiCompany" || mode === "AppliesToSelectedCompanies";
    if (appliesAll) {
      appliesAll.checked = isEnterprise;
      appliesAll.disabled = true;
    }
    if (primary && ownerCompany && ownerCompany !== primary) {
      const ownerCompanyId = String(ownerCompany.value || "").trim();
      if (ownerCompanyId) primary.value = ownerCompanyId;
    }
    const disablePrimary = true;
    const hideApplicable = isEnterprise;
    const disableApplicable = isEnterprise || hideApplicable || !isSelectedCompanies;
    if (primary) primary.disabled = disablePrimary;
    if (primaryHost) primaryHost.classList.toggle("opacity-75", disablePrimary);
    applicable.disabled = disableApplicable;
    if (applicableHost) applicableHost.classList.toggle("opacity-75", disableApplicable);
    if (applicableHost) applicableHost.classList.toggle("d-none", hideApplicable);
    if (isEnterprise || hideApplicable) {
      Array.from(applicable.options || []).forEach((o) => { o.selected = false; });
    }
    const selectedApplicableCount = getMultiValues(applicable).length;
    if (modeHint) {
      modeHint.textContent = isEnterprise
        ? "Enterprise applicability metadata only."
        : "Selected companies are applicability metadata only; commitments are managed at Objective level.";
    }
    if (primaryHint) {
      primaryHint.textContent = "Primary Company is not a controlling master field at Goal level.";
    }
    if (applicableHint) {
      applicableHint.textContent = (isEnterprise || hideApplicable)
        ? "Applicable Companies is disabled for Enterprise applicability."
          : selectedApplicableCount > 0
            ? `${selectedApplicableCount} compan${selectedApplicableCount === 1 ? "y" : "ies"} selected.`
            : "Select one or more companies for selected-company applicability.";
    }
    if (window.jQuery && primary) window.jQuery(primary).trigger("change.select2");
    syncApplicableCompaniesPickerFromSelect();
    syncRelatedEntityScopeSummary();
    syncOwnerAccountableDisplay();
  }

  function getEl(id) {
    return document.getElementById(id);
  }

  function getOwnerRoleEl() {
    return getEl("goal-owner-role") || getEl("goal-owner");
  }

  function getOwnerCompanyEl() {
    return getEl("goal-owner-company") || getEl("goal-primary-company");
  }

  function getOwnerPersonEl() {
    return getEl("goal-owner-person");
  }

  function getOwnerPersonDisplayEl() {
    return getEl("goal-owner-person-display");
  }

  function ownerRoleFieldId() {
    return getEl("goal-owner-role") ? "goal-owner-role" : "goal-owner";
  }

  function ownerCompanyFieldId() {
    return getEl("goal-owner-company") ? "goal-owner-company" : "goal-primary-company";
  }

  function selectedOptionLabel(el) {
    return String(el?.selectedOptions?.[0]?.textContent || "").trim();
  }

  function ensureSelectOption(selectEl, value, label) {
    const v = String(value || "").trim();
    if (!selectEl || !v) return;
    const exists = Array.from(selectEl.options || []).some((o) => String(o.value || "").trim() === v);
    if (exists) return;
    const opt = document.createElement("option");
    opt.value = v;
    opt.textContent = String(label || v).trim() || v;
    selectEl.appendChild(opt);
  }

  function ownerRoleLabelByValue(value) {
    const roleValue = String(value || "").trim();
    if (!roleValue) return "";
    const mappedPosition = String(workbook.positionDisplayName?.(roleValue) || "").trim();
    if (mappedPosition) return mappedPosition;
    const el = getOwnerRoleEl();
    const hit = Array.from(el?.options || []).find((o) => String(o.value || "").trim() === roleValue);
    return String(hit?.textContent || roleValue).trim();
  }

  function ownerCompanyLabelByValue(value) {
    const companyId = String(value || "").trim();
    if (!companyId) return "";
    return companyLabelById(companyId) || selectedOptionLabel(getOwnerCompanyEl()) || companyId;
  }

  function deriveOwnerDisplay() {
    const companyValue = String(getOwnerCompanyEl()?.value || "").trim();
    const roleValue = String(getOwnerRoleEl()?.value || "").trim();
    const personValue = String(getOwnerPersonEl()?.value || "").trim();
    const roleLabel = ownerRoleLabelByValue(roleValue);
    const companyLabel = ownerCompanyLabelByValue(companyValue);
    const personLabel = String(getOwnerPersonDisplayEl()?.value || "").trim() || resolveUserName(personValue);
    return [companyLabel || "-", roleLabel || "-", personLabel || "-"].join(" -> ");
  }

  function syncOwnerAccountableDisplay() {
    const display = getEl("goal-owner-accountable-display");
    if (!display) return;
    display.value = deriveOwnerDisplay();
  }

  function ownerRoleOptions() {
    const companyId = String(getOwnerCompanyEl()?.value || "").trim();
    const scoped = typeof workbook.positionOptionsForCompany === "function"
      ? (workbook.positionOptionsForCompany(companyId) || [])
      : [];
    const normalizedScoped = scoped
      .map((row) => ({
        value: String(row?.value || row?.positionId || row?.id || row?.label || "").trim(),
        label: String(row?.label || row?.positionName || row?.name || row?.value || "").trim()
      }))
      .filter((row) => row.value && row.label);
    if (normalizedScoped.length) return normalizedScoped;

    return (workbook.positionOptions?.() || [])
      .map((row) => ({
        value: String(row?.value || row?.positionId || row?.id || row?.label || "").trim(),
        label: String(row?.label || row?.positionName || row?.name || row?.value || "").trim()
      }))
      .filter((row) => row.value && row.label);
  }

  function resolveCurrentOwnerState() {
    const companyId = String(getOwnerCompanyEl()?.value || "").trim();
    const positionId = String(getOwnerRoleEl()?.value || "").trim();
    const personId = String(getOwnerPersonEl()?.value || "").trim();
    const incumbents = companyId && positionId
      ? (workbook.usersForOwnershipContext?.(companyId, positionId, { activeOnly: true }) || [])
      : [];
    const validUsers = companyId && positionId
      ? (workbook.usersForOwnershipContext?.(companyId, positionId, { activeOnly: false }) || [])
      : [];
    const currentMatches = personId
      ? validUsers.some((user) => String(user?.id || user?.value || "").trim() === personId)
      : false;
    return {
      companyId,
      positionId,
      personId,
      incumbents,
      incumbent: incumbents[0] || null,
      validUsers,
      currentMatches,
      requiresNamedOwner: Boolean(positionId && incumbents.length)
    };
  }

  function syncCurrentOwnerPersonSelection(options = {}) {
    const { keepCurrent = true } = options;
    const personEl = getOwnerPersonEl();
    const personDisplayEl = getOwnerPersonDisplayEl();
    if (!personEl) return;
    const state = resolveCurrentOwnerState();
    const current = keepCurrent ? state.personId : "";
    const personHint = getEl("goal-owner-person-hint");
    if (!state.companyId || !state.positionId) {
      if (personHint) personHint.textContent = "Select Owner Company / Org and Owner Position to resolve the current incumbent.";
      personEl.value = "";
      if (personDisplayEl) personDisplayEl.value = "";
      syncOwnerAccountableDisplay();
      return;
    }
    if (state.incumbent) {
      const incumbentId = String(state.incumbent?.id || state.incumbent?.value || "").trim();
      const incumbentLabel = String(state.incumbent?.fullName || state.incumbent?.label || incumbentId).trim();
      if (!current || !state.currentMatches) {
        personEl.value = incumbentId;
        if (personDisplayEl) personDisplayEl.value = incumbentLabel;
      } else if (current) {
        personEl.value = current;
        if (personDisplayEl) personDisplayEl.value = resolveUserName(current) || incumbentLabel || current;
      }
      if (personHint) personHint.textContent = `Active incumbent resolved for ${ownerRoleLabelByValue(state.positionId)}.`;
    } else {
      if (current) {
        personEl.value = current;
        if (personDisplayEl) personDisplayEl.value = resolveUserName(current) || current;
      } else {
        personEl.value = "";
        if (personDisplayEl) personDisplayEl.value = "";
      }
      if (personHint) personHint.textContent = "No active incumbent exists for the selected company or org and position.";
    }
    syncOwnerAccountableDisplay();
  }

  function fillOwnerRoleSelect() {
    const roleEl = getEl("goal-owner-role");
    if (!roleEl) return;
    const current = String(roleEl.value || "").trim();
    const roles = ownerRoleOptions();
    workbook.fillSelect?.(roleEl, roles, { placeholder: "Select position..." });
    if (current && Array.from(roleEl.options || []).some((o) => String(o.value || "") === current)) {
      roleEl.value = current;
    }
  }

  function fillOwnerPersonSelect({ keepCurrent = true } = {}) {
    syncCurrentOwnerPersonSelection({ keepCurrent });
  }

  function expandErrorSections(fieldMap) {
    Array.from(fieldMap.keys()).forEach((fieldId) => {
      const sectionId = sectionByField[fieldId];
      if (!sectionId) return;
      revealSection(sectionId);
    });
  }

  function initGoalSelect2() {
    if (!window.jQuery || !window.jQuery.fn?.select2) return;
    const $ = window.jQuery;
    const $dropdownParent = isWorkspaceMode ? $("#goal-create-workspace") : $("#goalEditorModal");
    const $legacyApplicable = $("#goal-applicable-companies");
    if ($legacyApplicable.length && $legacyApplicable.hasClass("select2-hidden-accessible")) {
      try { $legacyApplicable.select2("destroy"); } catch (_) { }
    }
    ["#goal-owner-role", "#goal-owner-company", "#goal-owner", "#goal-primary-company", "#goal-strategy-period"].forEach((selector) => {
      const $el = $(selector);
      if (!$el.length) return;
      if ($el.hasClass("select2-hidden-accessible")) {
        try { $el.select2("destroy"); } catch (_) { }
      }
      $el.select2({
        width: "100%",
        dropdownParent: $dropdownParent.length ? $dropdownParent : undefined,
        placeholder: $el.data("placeholder") || ($el.attr("multiple") ? "Search and select..." : "Select..."),
        allowClear: !$el.attr("multiple"),
        closeOnSelect: !$el.attr("multiple")
      });
      $el.off("select2:select select2:unselect select2:clear");
      $el.on("select2:select select2:unselect select2:clear", function () {
        this.dispatchEvent(new Event("change", { bubbles: true }));
      });
    });
  }

  function updateWizardStepStates() {
    if (!isWorkspaceMode || !wizardStepButtons.length) return;
    wizardStepButtons.forEach((btn) => {
      const step = Number(btn.dataset.step || 0);
      btn.classList.toggle("is-complete", step < currentWizardStep);
    });
  }

  function setWizardStep(step) {
    if (!isWorkspaceMode || !wizardStepButtons.length) return;
    const safeStep = Math.min(totalWizardSteps, Math.max(1, Number(step) || 1));
    currentWizardStep = safeStep;
    wizardStepButtons.forEach((btn) => {
      const active = Number(btn.dataset.step || 0) === safeStep;
      btn.classList.toggle("active", active);
      btn.setAttribute("aria-selected", active ? "true" : "false");
    });
    wizardStepPanes.forEach((pane) => {
      pane.classList.toggle("d-none", Number(pane.dataset.step || 0) !== safeStep);
    });
    if (wizardBackBtn) wizardBackBtn.disabled = safeStep === 1;
    if (wizardNextBtn) wizardNextBtn.classList.toggle("d-none", safeStep === totalWizardSteps);
    if (saveBtn) saveBtn.classList.toggle("d-none", !isEditMode && safeStep !== totalWizardSteps);
    updateWizardStepStates();
  }

  function revealSection(sectionId) {
    const step = wizardStepBySection[sectionId];
    if (step) setWizardStep(step);
    const section = document.getElementById(sectionId);
    if (section && "open" in section) section.open = true;
  }

  function canAdvanceWizard(step) {
    if (!isWorkspaceMode) return true;
    const payload = collectCreateRequest();
    const mode = normalizeValidationMode(activeValidationMode, payload);
    const map = fieldErrorMap(payload, { mode });
    const stepMap = new Map();
    map.forEach((message, fieldId) => {
      const sectionId = sectionByField[fieldId];
      if (wizardStepBySection[sectionId] === step) stepMap.set(fieldId, message);
    });
    if (!stepMap.size) return true;
    hasSubmitAttempt = true;
    applyFieldErrors(payload, stepMap, { mode });
    showErrors([...new Set(Array.from(stepMap.values()).filter(Boolean))], stepMap, { mode });
    const firstFieldId = stepMap.keys().next().value;
    const target = firstFieldId ? getEl(firstFieldId) : null;
    target?.focus?.();
    return false;
  }

  function buildDefaultYearlyTargets(metric, years) {
    const fallbackTarget = metric?.targetValue ?? null;
    return years.map((year, idx) => ({
      year,
      targetValue: fallbackTarget,
      actualValue: null,
      forecastValue: null,
      thresholdMin: null,
      thresholdMax: null,
      commentary: "",
      thresholdCommentary: ""
    }));
  }

  function normalizeYearlyTargets(metric, years) {
    const existing = Array.isArray(metric?.yearlyValues)
      ? metric.yearlyValues
      : (Array.isArray(metric?.yearlyTargets) ? metric.yearlyTargets : []);
    const map = new Map(existing.map((x) => [Number(x.year), x]));
    const normalized = years.map((year) => {
      const found = map.get(Number(year));
      return {
        year,
        targetValue: found?.targetValue ?? null,
        actualValue: found?.actualValue ?? null,
        forecastValue: found?.forecastValue ?? null,
        thresholdMin: found?.thresholdMin ?? null,
        thresholdMax: found?.thresholdMax ?? null,
        commentary: found?.commentary ?? found?.thresholdCommentary ?? "",
        thresholdCommentary: found?.thresholdCommentary ?? found?.commentary ?? ""
      };
    });
    const hasAny = normalized.some((x) =>
      x.targetValue !== null ||
      x.actualValue !== null ||
      x.forecastValue !== null ||
      x.thresholdMin !== null ||
      x.thresholdMax !== null ||
      String(x.commentary || "").trim().length > 0 ||
      String(x.thresholdCommentary || "").trim().length > 0);
    if (hasAny || existing.length) return normalized;
    return buildDefaultYearlyTargets(metric, years);
  }

  function metricThresholdsRequired(modelCode) {
    const normalized = String(modelCode || "").trim().toLowerCase();
    if (!normalized) return false;
    return !["none", "no threshold", "n/a", "na", "not applicable", "disabled", "off"].includes(normalized);
  }

  function syncMetricThresholdFields(row, options = {}) {
    const { clearWhenDisabled = true } = options;
    if (!row) return;
    const required = metricThresholdsRequired(row.querySelector(".metric-threshold-model")?.value);
    row.dataset.thresholdRequired = required ? "true" : "false";
    row.querySelectorAll(".metric-threshold-col").forEach((el) => {
      el.classList.toggle("d-none", !required);
    });
    row.querySelectorAll(".metric-year-threshold-min, .metric-year-threshold-max").forEach((input) => {
      input.disabled = !required;
      input.required = required;
      if (!required && clearWhenDisabled) input.value = "";
    });
  }

  function normalizeMetricNumericInput(input) {
    if (!input) return;
    const parsed = parseDecimal(input.value);
    const text = String(input.value || "").trim();
    if (!text) return;
    if (parsed === null) return;
    input.value = formatDecimalForInput(parsed);
  }

  function metricGridColumnsInRow(row) {
    const firstRow = row?.querySelector(".metric-year-rows tr");
    if (!firstRow) return [];
    return Array.from(firstRow.querySelectorAll(".metric-year-grid-input"))
      .filter((input) => !input.disabled && !input.closest(".d-none"))
      .map((input) => String(input.dataset.gridCol || "").trim())
      .filter(Boolean);
  }

  function focusMetricGridCell(row, rowIndex, colKey) {
    const rows = Array.from(row?.querySelectorAll(".metric-year-rows tr") || []);
    if (!rows.length) return false;
    const clampedIndex = Math.max(0, Math.min(rows.length - 1, Number(rowIndex) || 0));
    const targetRow = rows[clampedIndex];
    if (!targetRow) return false;
    const byCol = targetRow.querySelector(`.metric-year-grid-input[data-grid-col="${colKey}"]:not(:disabled)`);
    const fallback = targetRow.querySelector(".metric-year-grid-input:not(:disabled)");
    const input = byCol || fallback;
    if (!input) return false;
    input.focus();
    input.select?.();
    return true;
  }

  function bindMetricYearGridKeyboard(row) {
    const tbody = row?.querySelector(".metric-year-rows");
    if (!tbody || tbody.dataset.keyboardBound === "true") return;
    tbody.dataset.keyboardBound = "true";
    tbody.addEventListener("keydown", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) return;
      if (!target.classList.contains("metric-year-grid-input")) return;
      const tr = target.closest("tr");
      if (!tr) return;
      const rowIndex = Number(tr.dataset.gridRow || 0);
      const colKey = String(target.dataset.gridCol || "").trim();
      if (!colKey) return;

      if ((event.ctrlKey || event.metaKey) && event.key === "Home") {
        event.preventDefault();
        focusMetricGridCell(row, 0, colKey);
        return;
      }
      if ((event.ctrlKey || event.metaKey) && event.key === "End") {
        event.preventDefault();
        const rows = row.querySelectorAll(".metric-year-rows tr");
        focusMetricGridCell(row, rows.length - 1, colKey);
        return;
      }
      if ((event.key === "ArrowDown" && (event.ctrlKey || event.metaKey)) || (event.key === "Enter" && !event.shiftKey)) {
        event.preventDefault();
        focusMetricGridCell(row, rowIndex + 1, colKey);
        return;
      }
      if ((event.key === "ArrowUp" && (event.ctrlKey || event.metaKey)) || (event.key === "Enter" && event.shiftKey)) {
        event.preventDefault();
        focusMetricGridCell(row, rowIndex - 1, colKey);
        return;
      }
      if ((event.key === "ArrowRight" || event.key === "ArrowLeft") && (event.ctrlKey || event.metaKey)) {
        const columns = metricGridColumnsInRow(row);
        if (!columns.length) return;
        const idx = columns.indexOf(colKey);
        if (idx < 0) return;
        const delta = event.key === "ArrowRight" ? 1 : -1;
        const next = columns[idx + delta];
        if (!next) return;
        event.preventDefault();
        focusMetricGridCell(row, rowIndex, next);
      }
    });
  }

  function renderMetricYearRows(row, years, metricData) {
    const tbody = row.querySelector(".metric-year-rows");
    if (!tbody) return;
    const showRuntime = isEditMode || row.dataset.showRuntime === "true";
    syncMetricRuntimeFields(row);
    const targets = normalizeYearlyTargets(metricData || {}, years);
    tbody.innerHTML = "";
    targets.forEach((entry, idx) => {
      const tr = document.createElement("tr");
      tr.dataset.gridRow = String(idx);
      tr.innerHTML =
        `<td><input class="form-control form-control-sm metric-year" value="${entry.year}" readonly /></td>` +
        `<td><input type="number" step="any" inputmode="decimal" class="form-control form-control-sm metric-year-target metric-year-grid-input metric-year-number text-end" data-grid-col="target" value="${formatDecimalForInput(entry.targetValue)}" /></td>` +
        `<td class="metric-runtime-col ${showRuntime ? "" : "d-none"}"><input type="number" step="any" inputmode="decimal" class="form-control form-control-sm metric-year-actual metric-year-grid-input metric-year-number text-end" data-grid-col="actual" value="${formatDecimalForInput(entry.actualValue)}" /></td>` +
        `<td class="metric-runtime-col ${showRuntime ? "" : "d-none"}"><input type="number" step="any" inputmode="decimal" class="form-control form-control-sm metric-year-forecast metric-year-grid-input metric-year-number text-end" data-grid-col="forecast" value="${formatDecimalForInput(entry.forecastValue)}" /></td>` +
        `<td class="metric-threshold-col"><input type="number" step="any" inputmode="decimal" class="form-control form-control-sm metric-year-threshold-min metric-year-grid-input metric-year-number text-end" data-grid-col="thresholdMin" value="${formatDecimalForInput(entry.thresholdMin)}" /></td>` +
        `<td class="metric-threshold-col"><input type="number" step="any" inputmode="decimal" class="form-control form-control-sm metric-year-threshold-max metric-year-grid-input metric-year-number text-end" data-grid-col="thresholdMax" value="${formatDecimalForInput(entry.thresholdMax)}" /></td>` +
        `<td><input class="form-control form-control-sm metric-year-commentary metric-year-grid-input" data-grid-col="commentary" maxlength="300" value="${String(entry.commentary || entry.thresholdCommentary || "").replace(/"/g, "&quot;")}" /></td>`;
      tr.querySelectorAll("input").forEach((input) => {
        input.addEventListener("input", markDirty);
        input.addEventListener("change", markDirty);
        if (input.classList.contains("metric-year-number")) {
          input.addEventListener("blur", () => normalizeMetricNumericInput(input));
        }
      });
      tbody.appendChild(tr);
    });
    syncMetricThresholdFields(row, { clearWhenDisabled: false });
    bindMetricYearGridKeyboard(row);
  }

  function collectYearlyTargetsFromRow(row) {
    const thresholdRequired = row?.dataset?.thresholdRequired === "true";
    return Array.from(row.querySelectorAll(".metric-year-rows tr")).map((tr) => ({
      year: Number(tr.querySelector(".metric-year")?.value || 0),
      targetValue: parseDecimal(tr.querySelector(".metric-year-target")?.value),
      actualValue: parseDecimal(tr.querySelector(".metric-year-actual")?.value),
      forecastValue: parseDecimal(tr.querySelector(".metric-year-forecast")?.value),
      thresholdMin: thresholdRequired ? parseDecimal(tr.querySelector(".metric-year-threshold-min")?.value) : null,
      thresholdMax: thresholdRequired ? parseDecimal(tr.querySelector(".metric-year-threshold-max")?.value) : null,
      commentary: String(tr.querySelector(".metric-year-commentary")?.value || "").trim(),
      thresholdCommentary: String(tr.querySelector(".metric-year-commentary")?.value || "").trim()
    })).filter((x) => Number.isInteger(x.year) && x.year > 0);
  }

  function metricYearValueHasData(value) {
    if (!value) return false;
    return value.targetValue !== null && value.targetValue !== undefined ||
      value.actualValue !== null && value.actualValue !== undefined ||
      value.forecastValue !== null && value.forecastValue !== undefined ||
      value.thresholdMin !== null && value.thresholdMin !== undefined ||
      value.thresholdMax !== null && value.thresholdMax !== undefined ||
      String(value.commentary || value.thresholdCommentary || "").trim().length > 0;
  }

  function budgetRowHasData(row) {
    if (!row) return false;
    return row.revenueTarget !== null && row.revenueTarget !== undefined ||
      row.ebitdaTarget !== null && row.ebitdaTarget !== undefined ||
      row.capexEnvelope !== null && row.capexEnvelope !== undefined ||
      row.opexEnvelope !== null && row.opexEnvelope !== undefined ||
      row.savingsTarget !== null && row.savingsTarget !== undefined ||
      row.fundingPoolEnvelope !== null && row.fundingPoolEnvelope !== undefined ||
      row.fundingPool !== null && row.fundingPool !== undefined ||
      String(row.commentary || "").trim().length > 0;
  }

  function refreshMetricYearRowsWithHorizonChange(options = {}) {
    const { skipConfirm = true } = options;
    const years = getPlanningYears();
    const rows = Array.from(metricHost?.querySelectorAll(".metric-row") || []);
    if (!rows.length) return true;

    if (!skipConfirm) {
      const removedYears = rows.some((row) => {
        const existing = collectYearlyTargetsFromRow(row);
        return existing.some((v) => !years.includes(v.year) && metricYearValueHasData(v));
      });
      if (removedYears) {
        notify("Planning horizon updated. Metric yearly values outside the new horizon were removed.", "warning");
      }
    }
    rows.forEach((row) => {
      const existing = collectYearlyTargetsFromRow(row);
      renderMetricYearRows(row, years, { yearlyValues: existing });
      updateMetricSummary(row);
    });
    if (!options.skipMarkDirty) markDirty();
    return true;
  }

  function syncGoalHorizonDrivenRows(options = {}) {
    const { skipConfirm = false, markAsDirty = true } = options;
    const newYears = getPlanningYears();
    const prevBudget = collectYearlyBudgetsFromDom({ includeEmpty: true, ignoreToggle: true });
    const metricRows = Array.from(metricHost?.querySelectorAll(".metric-row") || []);
    const metricLoss = metricRows.some((row) => {
      const existing = collectYearlyTargetsFromRow(row);
      return existing.some((v) => metricYearValueHasData(v) && (!newYears.length || !newYears.includes(v.year)));
    });
    const budgetLoss = isBudgetEnvelopeEnabled() && prevBudget.some((b) => budgetRowHasData(b) && (!newYears.length || !newYears.includes(b.year)));
    if (!skipConfirm && (metricLoss || budgetLoss)) {
      notify("Planning horizon updated. KPI/budget values outside the new year range were removed.", "warning");
    }
    refreshMetricYearRowsWithHorizonChange({ skipConfirm: true, skipMarkDirty: true });
    renderBudgetYearRows(prevBudget);
    if (markAsDirty) markDirty();
    return true;
  }

  function syncGoalHorizonUiState() {
    const hasHorizon = getPlanningYears().length > 0;
    const budgetEnabled = isBudgetEnvelopeEnabled();
    if (addMetricBtn) addMetricBtn.disabled = !hasHorizon;
    [budgetFillColumnBtn, budgetInterpolateBtn, budgetCopyDownBtn, budgetClearColumnBtn, budgetHelperColumn]
      .forEach((el) => { if (el) el.disabled = !hasHorizon || !budgetEnabled; });
  }

  function syncMetricInheritedLock(row) {
    const origin = String(row.querySelector(".metric-origin")?.value || "Local");
    const inherited = origin.toLowerCase() === "inherited";
    row.querySelectorAll(".metric-def-lock").forEach((el) => {
      el.disabled = inherited;
      el.classList.toggle("bg-light", inherited);
    });
  }

  function getMetricHorizonMeta(row) {
    const years = collectYearlyTargetsFromRow(row).map((x) => Number(x.year)).filter(Number.isInteger);
    const source = years.length ? years : getPlanningYears();
    if (!source.length) return { range: "No horizon", count: "No years" };
    const start = Math.min(...source);
    const end = Math.max(...source);
    return {
      range: start === end ? `${start}` : `${start}\u2013${end}`,
      count: `${source.length} year${source.length === 1 ? "" : "s"}`
    };
  }

  function updateMetricSummary(row) {
    const horizon = getMetricHorizonMeta(row);
    const setText = (selector, value) => {
      const el = row.querySelector(selector);
      if (el) el.textContent = value;
    };
    setText(".metric-summary-name", row.querySelector(".metric-name")?.value.trim() || "Untitled metric");
    setText(".metric-summary-type", row.querySelector(".metric-type")?.value.trim() || "Type not set");
    setText(".metric-summary-unit", row.querySelector(".metric-unit")?.value.trim() || "Unit not set");
    setText(".metric-summary-role", row.querySelector(".metric-role")?.value.trim() || "Strategic");
    setText(".metric-summary-range", horizon.range);
    setText(".metric-summary-count", horizon.count);
    const cascadeBadge = row.querySelector(".metric-summary-cascade");
    const rollupBadge = row.querySelector(".metric-summary-rollup");
    if (cascadeBadge) {
      const enabled = String(row.querySelector(".metric-cascade")?.value || "true") === "true";
      cascadeBadge.textContent = enabled ? "Cascade Enabled" : "Cascade Disabled";
      cascadeBadge.className = `badge metric-summary-cascade ${enabled ? "bg-label-success" : "bg-label-secondary"}`;
    }
    if (rollupBadge) {
      const eligible = String(row.querySelector(".metric-rollup")?.value || "true") === "true";
      rollupBadge.textContent = eligible ? "Roll-up Eligible" : "Roll-up Off";
      rollupBadge.className = `badge metric-summary-rollup ${eligible ? "bg-label-info" : "bg-label-secondary"}`;
    }
  }

  function setMetricExpanded(row, expanded) {
    const editor = row.querySelector(".goal-metric-editor");
    const btn = row.querySelector(".metric-toggle");
    if (editor) editor.classList.toggle("d-none", !expanded);
    if (btn) btn.textContent = expanded ? "Collapse" : "Expand";
    row.dataset.expanded = expanded ? "true" : "false";
  }

  function setMetricPinned(row, pinned) {
    row.dataset.pinned = pinned ? "true" : "false";
    const btn = row.querySelector(".metric-pin");
    if (btn) {
      btn.textContent = pinned ? "Unpin" : "Pin open";
      btn.classList.toggle("btn-outline-primary", pinned);
      btn.classList.toggle("btn-outline-secondary", !pinned);
    }
  }

  function collapseOtherMetrics(activeRow) {
    Array.from(metricHost?.querySelectorAll(".metric-row") || []).forEach((row) => {
      const keepOpen = row === activeRow || (row.dataset.pinned === "true" && row.dataset.expanded === "true");
      setMetricExpanded(row, keepOpen);
    });
  }

  function activateMetricTab(row, tabKey) {
    row.querySelectorAll(".goal-metric-tab").forEach((btn) => {
      btn.classList.toggle("active", btn.dataset.tab === tabKey);
    });
    row.querySelectorAll(".goal-metric-panel").forEach((panel) => {
      panel.classList.toggle("d-none", panel.dataset.panel !== tabKey);
    });
  }

  function syncMetricRuntimeFields(row) {
    const showRuntime = isEditMode || row.dataset.showRuntime === "true";
    row.querySelectorAll(".metric-runtime-col").forEach((el) => el.classList.toggle("d-none", !showRuntime));
    const toggle = row.querySelector(".metric-toggle-runtime");
    if (toggle) {
      toggle.classList.toggle("d-none", isEditMode);
      toggle.textContent = showRuntime ? "Hide advanced yearly fields" : "Advanced yearly fields";
    }
  }

  function applyMetricFlatFill(row) {
    const value = promptDecimalValue("Flat fill Target Value for all years:");
    if (value === null || value === undefined) return;
    const formatted = formatDecimalForInput(value);
    row.querySelectorAll(".metric-year-target").forEach((input) => { input.value = formatted; });
    updateMetricSummary(row);
    markDirty();
  }

  function interpolateMetricTargets(row) {
    const rows = Array.from(row.querySelectorAll(".metric-year-rows tr"));
    if (rows.length < 2) return;
    const startValue = promptDecimalValue("Start Target Value:");
    if (startValue === null || startValue === undefined) return;
    const endValue = promptDecimalValue("End Target Value:");
    if (endValue === null || endValue === undefined) return;
    const steps = rows.length - 1;
    rows.forEach((tr, idx) => {
      const value = steps === 0 ? startValue : startValue + ((endValue - startValue) * idx / steps);
      tr.querySelector(".metric-year-target").value = Number.isFinite(value) ? formatDecimalForInput(value) : "";
    });
    updateMetricSummary(row);
    markDirty();
  }

  function copyMetricPreviousRows(row) {
    const rows = Array.from(row.querySelectorAll(".metric-year-rows tr"));
    const thresholdRequired = row?.dataset?.thresholdRequired === "true";
    rows.slice(1).forEach((tr, idx) => {
      const prev = rows[idx];
      tr.querySelector(".metric-year-target").value = prev.querySelector(".metric-year-target").value;
      if (thresholdRequired) {
        tr.querySelector(".metric-year-threshold-min").value = prev.querySelector(".metric-year-threshold-min").value;
        tr.querySelector(".metric-year-threshold-max").value = prev.querySelector(".metric-year-threshold-max").value;
      }
      tr.querySelector(".metric-year-commentary").value = prev.querySelector(".metric-year-commentary").value;
      const actual = tr.querySelector(".metric-year-actual");
      const forecast = tr.querySelector(".metric-year-forecast");
      const prevActual = prev.querySelector(".metric-year-actual");
      const prevForecast = prev.querySelector(".metric-year-forecast");
      if (actual && prevActual) actual.value = prevActual.value;
      if (forecast && prevForecast) forecast.value = prevForecast.value;
    });
    updateMetricSummary(row);
    markDirty();
  }

  function clearMetricYearRows(row) {
    row.querySelectorAll(".metric-year-target, .metric-year-actual, .metric-year-forecast, .metric-year-threshold-min, .metric-year-threshold-max, .metric-year-commentary").forEach((input) => {
      if (input.disabled) return;
      input.value = "";
    });
    updateMetricSummary(row);
    markDirty();
  }

  function duplicateMetricRow(row) {
    const metric = {
      metricName: `${row.querySelector(".metric-name")?.value.trim() || "Metric"} (Copy)`,
      metricDefId: row.querySelector(".metric-def-id")?.value || "",
      metricType: row.querySelector(".metric-type")?.value || "",
      unitOfMeasure: row.querySelector(".metric-unit")?.value || "",
      aggregationMethod: row.querySelector(".metric-aggregation")?.value || "",
      polarityCode: row.querySelector(".metric-polarity")?.value || "",
      thresholdModelCode: row.querySelector(".metric-threshold-model")?.value || "",
      reportingFrequencyCode: row.querySelector(".metric-reporting-frequency")?.value || "",
      cascadeMetric: String(row.querySelector(".metric-cascade")?.value || "true") === "true",
      metricOrigin: row.querySelector(".metric-origin")?.value || "Local",
      metricRole: row.querySelector(".metric-role")?.value || "Strategic",
      restrictionMode: row.querySelector(".metric-restriction")?.value || "GoalGovernedStructure",
      rollupEligible: String(row.querySelector(".metric-rollup")?.value || "true") === "true",
      yearlyValues: collectYearlyTargetsFromRow(row)
    };
    const clone = metricRow(metric);
    metricHost?.appendChild(clone);
    collapseOtherMetrics(clone);
    updateMetricSummary(clone);
    markDirty();
  }

  function metricRow(metric) {
    const metricTypeOpts = (workbook.goalMetricType || ["%"]).map((v) => `<option value="${v}">${v}</option>`).join("");
    const uomOpts = (workbook.unitOfMeasure || ["Percentage"]).map((v) => `<option value="${v}">${v}</option>`).join("");
    const aggOpts = (workbook.goalAggregation || ["Managed", "Sum"]).map((v) => `<option value="${v}">${v}</option>`).join("");
    const restrictionOpts = [
      ["GoalGovernedStructure", "Goal-governed structure"],
      ["LocalEditable", "Local editable"],
      ["ParentGovernedStructure", "Parent-governed structure"]
    ].map(([v, l]) => `<option value="${v}">${l}</option>`).join("");
    const row = document.createElement("div");
    row.className = "metric-row goal-metric-card";
    row.innerHTML =
      '<div class="goal-metric-summary">' +
      '<div class="goal-metric-summary-main">' +
      '<div class="goal-metric-summary-name metric-summary-name">Untitled metric</div>' +
      '<div class="goal-metric-summary-meta">' +
      '<span class="goal-metric-meta-chip metric-summary-type">Type not set</span>' +
      '<span class="goal-metric-meta-chip metric-summary-unit">Unit not set</span>' +
      '<span class="goal-metric-meta-chip metric-summary-role">Strategic</span>' +
      '<span class="goal-metric-meta-chip metric-summary-range">No horizon</span>' +
      '<span class="goal-metric-meta-chip metric-summary-count">No years</span>' +
      '<span class="badge bg-label-success metric-summary-cascade">Cascade Enabled</span>' +
      '<span class="badge bg-label-info metric-summary-rollup">Roll-up Eligible</span>' +
      '</div></div>' +
      '<div class="goal-metric-actions">' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-toggle">Expand</button>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-duplicate">Duplicate</button>' +
      '<button type="button" class="btn btn-sm btn-outline-danger metric-remove">Remove</button>' +
      '</div></div>' +
      '<div class="invalid-feedback es-inline-error metric-card-error d-none"></div>' +
      '<div class="goal-metric-editor d-none">' +
      '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">' +
      '<div class="goal-metric-tabs mb-0">' +
      '<button type="button" class="goal-metric-tab active" data-tab="definition">Definition</button>' +
      '<button type="button" class="goal-metric-tab" data-tab="governance">Governance</button>' +
      '<button type="button" class="goal-metric-tab" data-tab="yearly">Yearly Plan</button>' +
      '</div>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-pin">Pin open</button>' +
      '</div>' +
      '<div class="goal-metric-panel" data-panel="definition">' +
      '<div class="goal-metric-band mt-0">' +
      '<div class="goal-metric-band-title">Metric Definition</div>' +
      '<div class="row g-2">' +
      '<div class="col-12 col-lg-6"><label class="form-label">Metric *</label><input class="form-control form-control-sm metric-name metric-def-lock" aria-label="Metric" /></div>' +
      '<div class="col-12 col-lg-6"><label class="form-label">Metric Definition ID</label><input class="form-control form-control-sm metric-def-id metric-def-lock" aria-label="Metric Definition ID" /></div>' +
      `<div class="col-12 col-lg-6"><label class="form-label">Goal Metric Type *</label><select class="form-select form-select-sm metric-type metric-def-lock" aria-label="Goal Metric Type"><option value="">Select</option>${metricTypeOpts}</select></div>` +
      `<div class="col-12 col-lg-6"><label class="form-label">Unit of Measure *</label><select class="form-select form-select-sm metric-unit metric-def-lock" aria-label="Unit of Measure"><option value="">Select</option>${uomOpts}</select></div>` +
      `<div class="col-12 col-lg-6"><label class="form-label">Aggregation Method *</label><select class="form-select form-select-sm metric-aggregation metric-def-lock" aria-label="Aggregation Method"><option value="">Select</option>${aggOpts}</select></div>` +
      `<div class="col-12 col-lg-6"><label class="form-label">Direction / Polarity *</label><select class="form-select form-select-sm metric-polarity metric-def-lock" aria-label="Polarity"><option value="">Select</option>${(workbook.directionOfPerformance || []).map((v) => `<option value="${v}">${v}</option>`).join("")}</select></div>` +
      `<div class="col-12 col-lg-6"><label class="form-label">Threshold Model *</label><select class="form-select form-select-sm metric-threshold-model metric-def-lock" aria-label="Threshold Model"><option value="">Select</option>${(workbook.thresholdModels || []).map((v) => `<option value="${v}">${v}</option>`).join("")}</select></div>` +
      `<div class="col-12 col-lg-6"><label class="form-label">Reporting Frequency *</label><select class="form-select form-select-sm metric-reporting-frequency metric-def-lock" aria-label="Reporting Frequency"><option value="">Select</option>${(workbook.reportingFrequencies || []).map((v) => `<option value="${v}">${v}</option>`).join("")}</select></div>` +
      '</div></div></div>' +
      '<div class="goal-metric-panel d-none" data-panel="governance">' +
      '<div class="goal-metric-band mt-0">' +
      '<div class="goal-metric-band-title">Metric Governance</div>' +
      '<div class="row g-2">' +
      '<div class="col-12 col-lg-4"><label class="form-label">Cascade Metric *</label><select class="form-select form-select-sm metric-cascade"><option value="true">Enabled</option><option value="false">Disabled</option></select></div>' +
      '<div class="col-12 col-lg-4"><label class="form-label">Metric Origin *</label><select class="form-select form-select-sm metric-origin" aria-label="Metric Origin"><option value="Local">Local</option><option value="Inherited">Inherited</option></select></div>' +
      '<div class="col-12 col-lg-4"><label class="form-label">Metric Role *</label><select class="form-select form-select-sm metric-role" aria-label="Metric Role"><option value="Strategic">Strategic</option></select></div>' +
      `<div class="col-12 col-lg-6"><label class="form-label">Restriction Mode *</label><select class="form-select form-select-sm metric-restriction" aria-label="Restriction Mode"><option value="">Select</option>${restrictionOpts}</select></div>` +
      '<div class="col-12 col-lg-6"><label class="form-label">Roll-up Eligible *</label><select class="form-select form-select-sm metric-rollup"><option value="true">Eligible — roll-up allowed</option><option value="false">Not eligible — no roll-up</option></select></div>' +
      '</div></div></div>' +
      '<div class="goal-metric-panel d-none" data-panel="yearly">' +
      '<div class="goal-metric-band mt-0">' +
      '<div class="goal-metric-band-title">Yearly Plan</div>' +
      '<div class="metric-year-tools">' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-toggle-runtime">Advanced yearly fields</button>' +
      '<div class="metric-year-action-buttons">' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-fill-flat">Fill flat</button>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-copy-prev">Copy down</button>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-fill-linear">Interpolate</button>' +
      '<button type="button" class="btn btn-sm btn-outline-secondary metric-clear-years">Clear yearly values</button>' +
      '</div>' +
      '</div>' +
      '<div class="table-responsive metric-year-wrap"><table class="table table-sm table-bordered metric-year-table goal-kpi-year-table mb-0"><thead><tr><th>Year</th><th>Target Value *</th><th class="metric-runtime-col">Actual Value</th><th class="metric-runtime-col">Forecast Value</th><th class="metric-threshold-col">Threshold Min</th><th class="metric-threshold-col">Threshold Max</th><th>Commentary</th></tr></thead><tbody class="metric-year-rows"></tbody></table></div>' +
      '</div></div></div>';
    row.querySelector(".metric-remove").addEventListener("click", () => {
      row.remove();
      collapseOtherMetrics(null);
      markDirty();
    });
    row.querySelector(".metric-toggle").addEventListener("click", () => {
      const expand = row.dataset.expanded !== "true";
      collapseOtherMetrics(expand ? row : null);
      if (!expand) setMetricExpanded(row, false);
    });
    row.querySelector(".metric-pin").addEventListener("click", () => {
      setMetricPinned(row, row.dataset.pinned !== "true");
    });
    row.querySelector(".metric-duplicate").addEventListener("click", () => duplicateMetricRow(row));
    row.querySelector(".metric-toggle-runtime").addEventListener("click", () => {
      row.dataset.showRuntime = row.dataset.showRuntime === "true" ? "false" : "true";
      syncMetricRuntimeFields(row);
    });
    row.querySelector(".metric-fill-flat").addEventListener("click", () => applyMetricFlatFill(row));
    row.querySelector(".metric-fill-linear").addEventListener("click", () => interpolateMetricTargets(row));
    row.querySelector(".metric-copy-prev").addEventListener("click", () => copyMetricPreviousRows(row));
    row.querySelector(".metric-clear-years").addEventListener("click", () => clearMetricYearRows(row));
    row.querySelectorAll(".goal-metric-tab").forEach((btn) => {
      btn.addEventListener("click", () => activateMetricTab(row, btn.dataset.tab));
    });
    row.querySelectorAll("input, select").forEach((input) => input.addEventListener("input", () => {
      updateMetricSummary(row);
      markDirty();
    }));
    row.querySelectorAll("input, select").forEach((input) => input.addEventListener("change", () => {
      updateMetricSummary(row);
      markDirty();
    }));
    row.querySelectorAll("input, select").forEach((input) => input.addEventListener("focus", () => {
      if (row.dataset.expanded !== "true") {
        collapseOtherMetrics(row);
        setMetricExpanded(row, true);
      }
      const panel = input.closest(".goal-metric-panel");
      const tabKey = String(panel?.dataset?.panel || "");
      if (tabKey) activateMetricTab(row, tabKey);
    }));
    row.querySelectorAll("input, select").forEach((input) => input.addEventListener("focus", () => {
      if (row.dataset.expanded !== "true") {
        collapseOtherMetrics(row);
        setMetricExpanded(row, true);
      }
      const panel = input.closest(".goal-metric-panel");
      const tabKey = String(panel?.dataset?.panel || "");
      if (tabKey) activateMetricTab(row, tabKey);
    }));
    row.querySelector(".metric-origin")?.addEventListener("change", () => {
      syncMetricInheritedLock(row);
      updateMetricSummary(row);
      markDirty();
    });
    row.querySelector(".metric-threshold-model")?.addEventListener("change", () => {
      syncMetricThresholdFields(row);
      updateMetricSummary(row);
      markDirty();
    });
    row.querySelector(".metric-name")?.addEventListener("change", () => {
      applyMetricCatalogDefaults(row);
      updateMetricSummary(row);
      markDirty();
    });
    if (metric) {
      row.dataset.metricAssignmentId = String(metric.metricAssignmentId || metric.id || metric.metricId || "").trim();
      const mo = (metric.metricOrigin || "Local").toLowerCase() === "strategic" ? "Local" : (metric.metricOrigin || "Local");
      row.querySelector(".metric-name").value = metric.metricName || "";
      row.querySelector(".metric-def-id").value = metric.metricDefId || metric.metricDefinitionId || "";
      row.querySelector(".metric-type").value = metric.metricType || metric.metricTypeCode || "";
      row.querySelector(".metric-unit").value = metric.unitOfMeasure || metric.unitOfMeasureCode || "";
      row.querySelector(".metric-aggregation").value = metric.aggregationMethod || metric.aggregationMethodCode || "";
      row.querySelector(".metric-polarity").value = metric.polarityCode || metric.directionPolarity || "";
      row.querySelector(".metric-threshold-model").value = metric.thresholdModelCode || metric.thresholdModel || "";
      row.querySelector(".metric-reporting-frequency").value = metric.reportingFrequencyCode || metric.reportingFrequency || "";
      row.querySelector(".metric-cascade").value = String(metric.cascadeMetric !== false);
      row.querySelector(".metric-origin").value = mo === "Inherited" ? "Inherited" : "Local";
      row.querySelector(".metric-role").value = metric.metricRole || "Strategic";
      row.querySelector(".metric-restriction").value = metric.restrictionMode || "GoalGovernedStructure";
      row.querySelector(".metric-rollup").value = String(metric.rollupEligible !== false);
    }
    syncMetricInheritedLock(row);
    row.dataset.showRuntime = "false";
    setMetricPinned(row, false);
    activateMetricTab(row, "definition");
    renderMetricYearRows(row, getPlanningYears(), metric || {});
    syncMetricRuntimeFields(row);
    syncMetricThresholdFields(row, { clearWhenDisabled: false });
    updateMetricSummary(row);
    return row;
  }

  async function refreshGoalIdPreview() {
    const el = getEl("goal-id");
    if (!el || isEditMode) return;
    el.readOnly = true;
    el.placeholder = "Loading preview…";
    try {
      const p = await window.strategyEnterpriseMetaApi?.runtimeIdPreview?.();
      el.value = p?.goalId || "";
      el.placeholder = "";
    } catch {
      el.value = "";
      el.placeholder = "Assigned when you save";
    }
  }

  function openEditor(item) {
    if (!isWorkspaceMode) {
      const id = String(item?.id || "").trim();
      if (id) {
        navigateToGoalWorkspace("edit", id);
      } else {
        navigateToGoalWorkspace("create");
      }
      return;
    }
    isEditMode = Boolean(item?.id);
    if (modalTitle) {
      if (isWorkspaceMode) modalTitle.textContent = isEditMode ? "Edit Strategic Goal" : "Create Strategic Goal";
      else modalTitle.textContent = isEditMode ? "Edit Goal" : "Create Goal";
    }
    if (modalSubtitle && isWorkspaceMode) {
      modalSubtitle.textContent = isEditMode
        ? "Update the strategic goal draft with accountable ownership, horizon, applicability, KPI targets, and governance traceability."
        : "Create a draft strategic goal with governance ownership, planning horizon, company applicability, KPI targets, and traceable strategic context.";
    }
    if (saveBtn) {
      if (isWorkspaceMode) saveBtn.textContent = isEditMode ? "Save Strategic Goal" : "Create Goal";
      else saveBtn.textContent = isEditMode ? "Save Changes" : "Create Goal";
    }

    if (getEl("goal-id")) {
      if (isEditMode) {
        getEl("goal-id").value = item?.id || "";
        getEl("goal-id").readOnly = true;
        getEl("goal-id").placeholder = "";
      } else {
        getEl("goal-id").value = "";
        void refreshGoalIdPreview();
      }
    }
    if (getEl("goal-name")) getEl("goal-name").value = item?.name || "";
    if (getEl("goal-category")) getEl("goal-category").value = item?.category || "";
    const ownerRoleValue = String(item?.ownerRole || item?.ownerId || item?.owner || "").trim();
    const ownerCompanyValue = resolveCompanyId(item?.ownerCompanyId || item?.primaryCompanyId || item?.companyScope?.primaryCompanyId || "") || "";
    const ownerPersonValue = String(item?.ownerPersonId || item?.ownerDisplayName || "").trim();
    if (getEl("goal-owner-role")) {
      ensureSelectOption(getEl("goal-owner-role"), ownerRoleValue, ownerRoleValue);
      getEl("goal-owner-role").value = ownerRoleValue;
    }
    if (getEl("goal-owner-company")) {
      ensureSelectOption(getEl("goal-owner-company"), ownerCompanyValue, ownerCompanyLabelByValue(ownerCompanyValue) || ownerCompanyValue);
      getEl("goal-owner-company").value = ownerCompanyValue;
    }
    if (getEl("goal-owner-person")) {
      const ownerPersonId = resolveUserId(ownerPersonValue);
      getEl("goal-owner-person").value = ownerPersonId;
    }
    if (getOwnerPersonDisplayEl()) {
      getOwnerPersonDisplayEl().value = resolveUserName(ownerPersonValue) || ownerPersonValue || "";
    }
    if (getEl("goal-owner")) getEl("goal-owner").value = ownerRoleValue || resolveUserId(ownerPersonValue);
    if (getEl("goal-status")) {
      const statusValue = String(item?.status || "Draft").trim();
      ensureSelectOption(getEl("goal-status"), statusValue, statusValue);
      getEl("goal-status").value = statusValue;
    }
    if (getEl("goal-status-readonly")) getEl("goal-status-readonly").value = String(item?.status || "Draft").trim() || "Draft";
    if (getEl("goal-priority")) getEl("goal-priority").value = item?.priority || "Medium";
    if (getEl("goal-statement")) getEl("goal-statement").value = item?.statement || "";
    if (getEl("goal-strategic-theme")) {
      const themeValue = String(item?.strategicThemeId || item?.strategicTheme || "").trim();
      ensureSelectOption(getEl("goal-strategic-theme"), themeValue, themeValue);
      getEl("goal-strategic-theme").value = themeValue;
    }
    const strategyPeriodId = String(item?.strategyPeriodId || item?.planning?.strategyPeriodId || "").trim();
    initialStrategyPeriodId = strategyPeriodId;
    previousStrategyPeriodIdRaw = strategyPeriodId;
    if (getEl("goal-strategy-period")) getEl("goal-strategy-period").value = strategyPeriodId;
    setPlanningInputFromRaw("goal-planning-start-year", item?.planningHorizonStart || String(item?.startYear || ""), false);
    setPlanningInputFromRaw("goal-planning-end-year", item?.planningHorizonEnd || String(item?.endYear || ""), true);
    previousStartYearRaw = String(getEl("goal-planning-start-year")?.value || "").trim();
    previousEndYearRaw = String(getEl("goal-planning-end-year")?.value || "").trim();
    if (getEl("goal-entity-scope")) getEl("goal-entity-scope").value = item?.entityScope || "";
    if (getEl("goal-change-log-ref")) getEl("goal-change-log-ref").value = item?.changeLogRef || "";
    const rawScopeModeValue = item?.scopeMode || item?.companyScope?.scopeModeCode || "Enterprise";
    const scopeModeValue = toUiScopeMode(rawScopeModeValue);
    if (getEl("goal-scope-mode")) getEl("goal-scope-mode").value = scopeModeValue;
    if (getEl("goal-primary-company")) {
      getEl("goal-primary-company").value = ownerCompanyValue || resolveCompanyId(item?.primaryCompanyId || item?.companyScope?.primaryCompanyId || "") || "";
    }
    if (getEl("goal-applies-to-all-companies")) getEl("goal-applies-to-all-companies").checked = scopeModeValue === "Enterprise";
    if (getEl("goal-applicable-companies")) {
      const selected = new Set(resolveCompanyIds(item?.applicableCompanyIds || item?.companyScope?.applicableCompanyIds || []));
      Array.from(getEl("goal-applicable-companies").options || []).forEach((o) => { o.selected = selected.has(o.value); });
    }
    if (getEl("goal-business-unit")) getEl("goal-business-unit").value = item?.businessUnit || "";
    if (getEl("goal-region")) getEl("goal-region").value = item?.region || "";
    if (getEl("goal-related-entity-scope-summary")) getEl("goal-related-entity-scope-summary").value = item?.relatedEntityScopeSummary || item?.companyScope?.relatedEntityScopeSummary || item?.entityScope || "";
    if (getEl("goal-planning-scope-preview")) getEl("goal-planning-scope-preview").value = getEl("goal-related-entity-scope-summary")?.value || "Company applicability will define the strategic scope summary.";
    if (getEl("goal-decision-reference")) getEl("goal-decision-reference").value = item?.decisionReference || "";
    if (getEl("goal-evidence-reference")) getEl("goal-evidence-reference").value = item?.evidenceReference || "";
    if (getEl("goal-version")) getEl("goal-version").value = item?.version || 0;
    currentVersion = item?.version || 0;

    if (metricHost) {
      metricHost.innerHTML = "";
      (item?.metrics || []).forEach((m) => metricHost.appendChild(metricRow(m)));
      collapseOtherMetrics(null);
    }
    const budgetSource = item?.yearlyBudgets || item?.budgetEnvelopes || [];
    renderBudgetYearRows(budgetSource);
    const budgetEnabledEl = getEl("goal-budget-enabled");
    if (budgetEnabledEl) {
      const hasBudgetData = (budgetSource || []).some((row) => budgetRowHasData(row));
      budgetEnabledEl.checked = Boolean(hasBudgetData || item?.budgetEnvelopeEnabled);
    }
    syncBudgetEnvelopeUi();
    updateSourceSummary();
    if (errorEl) {
      errorEl.classList.add("d-none");
      errorEl.textContent = "";
    }
    isDirty = false;
    hasSubmitAttempt = false;
    ["goal-sec-identity", "goal-sec-ownership", "goal-sec-planning", "goal-sec-company", "goal-sec-metrics", "goal-sec-budget", "goal-sec-governance"]
      .forEach((id) => { const el = document.getElementById(id); if (el) el.open = true; });
    setWizardStep(1);
    initGoalSelect2();
    fillOwnerPersonSelect({ keepCurrent: true });
    syncOwnerAccountableDisplay();
    syncGoalCompanyScopeUi();
    syncGoalHorizonUiState();
    void refreshGoalStrategyPeriodLookup({ applyDefaults: !isEditMode, preserveId: strategyPeriodId });
    activeValidationMode = normalizeValidationMode("auto", { statusCode: String(getEl("goal-status")?.value || "") });
    applyValidation();
    updateSectionCompletionStates();
    if (modal) modal.show();
  }

  function getMetrics() {
    if (!metricHost) return [];
    return Array.from(metricHost.querySelectorAll(".metric-row"))
      .map((row, idx) => ({
        id: String(row.dataset.metricAssignmentId || row.dataset.metricId || `gm-${idx + 1}`),
        metricAssignmentId: String(row.dataset.metricAssignmentId || row.dataset.metricId || "").trim(),
        metricDefId: row.querySelector(".metric-def-id")?.value.trim() || "",
        metricName: row.querySelector(".metric-name").value.trim(),
        metricType: row.querySelector(".metric-type").value.trim(),
        unitOfMeasure: row.querySelector(".metric-unit").value.trim(),
        aggregationMethod: row.querySelector(".metric-aggregation").value.trim(),
        polarityCode: row.querySelector(".metric-polarity")?.value.trim() || "",
        thresholdModelCode: row.querySelector(".metric-threshold-model")?.value.trim() || "",
        reportingFrequencyCode: row.querySelector(".metric-reporting-frequency")?.value.trim() || "",
        cascadeMetric: String(row.querySelector(".metric-cascade")?.value || "true") === "true",
        metricOrigin: String(row.querySelector(".metric-origin")?.value || "Local").trim() || "Local",
        metricRole: String(row.querySelector(".metric-role")?.value || "Strategic").trim() || "Strategic",
        restrictionMode: String(row.querySelector(".metric-restriction")?.value || "GoalGovernedStructure").trim() || "GoalGovernedStructure",
        rollupEligible: String(row.querySelector(".metric-rollup")?.value || "true") === "true",
        yearlyValues: collectYearlyTargetsFromRow(row),
        metricBindingStatus: "Bound"
      }))
      .map((m) => {
        const sorted = (m.yearlyValues || []).slice().sort((a, b) => a.year - b.year);
        const baseline = sorted.length ? (sorted[0].targetValue ?? 0) : 0;
        const target = sorted.length ? (sorted[sorted.length - 1].targetValue ?? 0) : 0;
        return { ...m, baselineValue: baseline, targetValue: target };
      })
      .filter((m) => m.metricName || m.metricType || m.unitOfMeasure || m.aggregationMethod || (m.yearlyValues || []).length > 0);
  }

  function collectLegacyPayload() {
    const startYear = planningYearFromInput("goal-planning-start-year");
    const endYear = planningYearFromInput("goal-planning-end-year");
    const metrics = getMetrics();
    const yearlyBudgets = collectYearlyBudgetsFromDom();
    const budgetEnabled = isBudgetEnvelopeEnabled();
    const ownerRoleValue = String(getOwnerRoleEl()?.value || "").trim();
    const ownerCompanyValue = resolveCompanyIdFromElement(getOwnerCompanyEl()) || "";
    const ownerPersonValue = String(getOwnerPersonEl()?.value || "").trim();
    return {
      id: isEditMode ? String(getEl("goal-id")?.value || "").trim() : "",
      name: String(getEl("goal-name")?.value || "").trim(),
      category: String(getEl("goal-category")?.value || "").trim(),
      ownerId: ownerPersonValue || ownerRoleValue,
      owner: ownerPersonValue ? resolveUserName(ownerPersonValue) : ownerRoleLabelByValue(ownerRoleValue),
      ownerRole: ownerRoleValue,
      ownerCompanyId: ownerCompanyValue,
      ownerPersonId: ownerPersonValue || null,
      statement: String(getEl("goal-statement")?.value || "").trim(),
      status: String(getEl("goal-status")?.value || "Active"),
      priority: String(getEl("goal-priority")?.value || "Medium"),
      strategyPeriodId: String(getEl("goal-strategy-period")?.value || "").trim() || null,
      planningHorizonStart: toStartDateIso(startYear),
      planningHorizonEnd: toEndDateIso(endYear),
      entityScope: String(getEl("goal-entity-scope")?.value || "").trim(),
      relatedEntityScopeSummary: String(getEl("goal-related-entity-scope-summary")?.value || "").trim(),
      scopeMode: toStoredScopeMode(String(getEl("goal-scope-mode")?.value || "Enterprise").trim()),
      appliesToSelectedCompaniesFlag: getMultiValues(getEl("goal-applicable-companies")).length > 0,
      appliesToAllCompaniesFlag: String(getEl("goal-scope-mode")?.value || "Enterprise").trim() === "Enterprise",
      primaryCompanyId: ownerCompanyValue || resolveCompanyIdFromElement(getEl("goal-primary-company")) || null,
      applicableCompanyIds: resolveCompanyIds(getMultiValues(getEl("goal-applicable-companies"))),
      changeLogRef: String(getEl("goal-change-log-ref")?.value || "").trim(),
      decisionReference: String(getEl("goal-decision-reference")?.value || "").trim() || null,
      evidenceReference: String(getEl("goal-evidence-reference")?.value || "").trim() || null,
      metrics: metrics.map((m) => ({
        ...m,
        baselineValue: m.baselineValue,
        targetValue: m.targetValue
      })),
      yearlyBudgets: yearlyBudgets.map((b) => ({
        year: b.year,
        revenueTarget: b.revenueTarget,
        ebitdaTarget: b.ebitdaTarget,
        capexEnvelope: b.capexEnvelope,
        opexEnvelope: b.opexEnvelope,
        savingsTarget: b.savingsTarget,
        fundingPoolEnvelope: b.fundingPoolEnvelope,
        fundingPool: b.fundingPoolEnvelope,
        commentary: b.commentary || null
      })),
      budgetEnvelopes: yearlyBudgets.map((b) => ({
        year: b.year,
        revenueTarget: b.revenueTarget,
        ebitdaTarget: b.ebitdaTarget,
        capexEnvelope: b.capexEnvelope,
        opexEnvelope: b.opexEnvelope,
        savingsTarget: b.savingsTarget,
        fundingPool: b.fundingPoolEnvelope,
        commentary: b.commentary || null
      })),
      budgetEnvelopeEnabled: budgetEnabled,
      version: currentVersion || 1,
      _startYearRaw: String(getEl("goal-planning-start-year")?.value || "").trim(),
      _endYearRaw: String(getEl("goal-planning-end-year")?.value || "").trim()
    };
  }

  function collectCreateRequest() {
    const metrics = getMetrics();
    const yearlyBudgets = collectYearlyBudgetsFromDom();
    const scopeModeUi = String(getEl("goal-scope-mode")?.value || "").trim();
    const scopeModeCode = toStoredScopeMode(scopeModeUi);
    const ownerRoleValue = String(getOwnerRoleEl()?.value || "").trim();
    const ownerCompanyValue = resolveCompanyIdFromElement(getOwnerCompanyEl()) || "";
    const ownerPersonValue = String(getOwnerPersonEl()?.value || "").trim();
    const normalizedScope = normalizeCompanyScopeForMode(
      scopeModeCode,
      ownerCompanyValue || resolveCompanyIdFromElement(getEl("goal-primary-company")) || null,
      getMultiValues(getEl("goal-applicable-companies"))
    );
    const startIso = goalHorizonIsoFromInput("goal-planning-start-year");
    const endIso = goalHorizonIsoFromInput("goal-planning-end-year");
    return {
      goal: String(getEl("goal-name")?.value || "").trim(),
      goalTitle: String(getEl("goal-name")?.value || "").trim(),
      goalTypeId: String(getEl("goal-category")?.value || "").trim(),
      categoryCode: String(getEl("goal-category")?.value || "").trim(),
      strategicThemeId: String(getEl("goal-strategic-theme")?.value || "").trim(),
      ownerId: ownerPersonValue || ownerRoleValue,
      ownerRole: ownerRoleValue,
      ownerPositionId: ownerRoleValue,
      ownerCompanyId: ownerCompanyValue || null,
      ownerOrgId: ownerCompanyValue || null,
      ownerPersonId: ownerPersonValue || null,
      currentOwnerPersonId: ownerPersonValue || null,
      accountableOwnerDisplay: deriveOwnerDisplay(),
      statusCode: String(getEl("goal-status")?.value || "Draft").trim() || "Draft",
      priorityCode: String(getEl("goal-priority")?.value || "").trim(),
      goalStatement: String(getEl("goal-statement")?.value || "").trim(),
      planning: {
        startDate: startIso || null,
        endDate: endIso || null,
        // Do not submit legacy startYear/endYear alongside startDate/endDate.
        // CreateGoalPlanningDto legacy setters can overwrite exact dates to
        // Jan-01 / Dec-31 when both shapes are present.
        startYear: null,
        endYear: null,
        strategyPeriodId: String(getEl("goal-strategy-period")?.value || "").trim() || null,
        relatedEntityScope: String(getEl("goal-entity-scope")?.value || "").trim(),
        changeLogRef: String(getEl("goal-change-log-ref")?.value || "").trim()
      },
      businessUnit: String(getEl("goal-business-unit")?.value || "").trim(),
      region: String(getEl("goal-region")?.value || "").trim(),
      companyScope: {
        scopeModeCode,
        appliesToSelectedCompaniesFlag: normalizedScope.applicableCompanyIds.length > 0,
        appliesToAllCompaniesFlag: scopeModeCode === "Enterprise",
        primaryCompanyId: ownerCompanyValue || normalizedScope.primaryCompanyId,
        applicableCompanyIds: normalizedScope.applicableCompanyIds,
        relatedEntityScopeSummary: String(getEl("goal-related-entity-scope-summary")?.value || deriveRelatedEntityScopeSummary()).trim()
      },
      yearlyBudgets: yearlyBudgets,
      budgetEnvelopes: yearlyBudgets.map((b) => ({
        year: b.year,
        revenueTarget: b.revenueTarget,
        ebitdaTarget: b.ebitdaTarget,
        capexEnvelope: b.capexEnvelope,
        opexEnvelope: b.opexEnvelope,
        savingsTarget: b.savingsTarget,
        fundingPool: b.fundingPoolEnvelope ?? b.fundingPool ?? null,
        commentary: b.commentary || null
      })),
      budgetEnvelopeEnabled: isBudgetEnvelopeEnabled(),
      applicabilityMode: scopeModeCode,
      appliesToAllCompanies: scopeModeCode === "Enterprise",
      applicableCompanyIds: normalizedScope.applicableCompanyIds,
      metrics: metrics.map((m, i) => ({
        metricAssignmentId: String(m.metricAssignmentId || m.id || "").trim() || null,
        metricDefId: m.metricDefId || null,
        metricDefinitionId: m.metricDefId || null,
        metricName: m.metricName,
        metricTypeCode: m.metricType,
        metricType: m.metricType,
        baselineValue: m.baselineValue ?? null,
        targetValue: m.targetValue ?? null,
        unitOfMeasureCode: m.unitOfMeasure,
        unitOfMeasure: m.unitOfMeasure,
        aggregationMethodCode: m.aggregationMethod,
        aggregationMethod: m.aggregationMethod,
        polarityCode: m.polarityCode || "",
        directionPolarity: m.polarityCode || "",
        thresholdModelCode: m.thresholdModelCode || "",
        thresholdModel: m.thresholdModelCode || "",
        reportingFrequencyCode: m.reportingFrequencyCode || "",
        reportingFrequency: m.reportingFrequencyCode || "",
        cascadeMetric: m.cascadeMetric !== false,
        metricOrigin: m.metricOrigin || "Local",
        metricRole: m.metricRole || "Strategic",
        restrictionMode: m.restrictionMode || "GoalGovernedStructure",
        rollupEligible: m.rollupEligible !== false,
        yearlyValues: (m.yearlyValues || []).map((y) => ({
          year: Number(y.year),
          baselineValue: y.baselineValue ?? null,
          targetValue: y.targetValue ?? null,
          actualValue: y.actualValue ?? null,
          forecastValue: y.forecastValue ?? null,
          thresholdMin: y.thresholdMin ?? null,
          thresholdMax: y.thresholdMax ?? null,
          commentary: String(y.commentary || "").trim() || null,
          thresholdCommentary: String(y.commentary || y.thresholdCommentary || "").trim() || null
        })),
        yearlyTargets: (m.yearlyValues || []).map((y) => ({
          goalMetricId: String(m.metricAssignmentId || m.id || "").trim() || null,
          year: Number(y.year),
          targetValue: y.targetValue ?? null,
          thresholdMin: y.thresholdMin ?? null,
          thresholdMax: y.thresholdMax ?? null,
          commentary: String(y.commentary || "").trim() || null
        })),
        strategicGoalMetricYearlyTargets: (m.yearlyValues || []).map((y) => ({
          goalMetricId: String(m.metricAssignmentId || m.id || "").trim() || null,
          year: Number(y.year),
          targetValue: y.targetValue ?? null,
          thresholdMin: y.thresholdMin ?? null,
          thresholdMax: y.thresholdMax ?? null,
          commentary: String(y.commentary || "").trim() || null
        })),
        sortOrder: i + 1
      })),
      governance: {
        decisionReference: String(getEl("goal-decision-reference")?.value || "").trim() || null,
        evidenceLink: String(getEl("goal-evidence-reference")?.value || "").trim() || null
      },
      _startYearRaw: String(getEl("goal-planning-start-year")?.value || "").trim(),
      _endYearRaw: String(getEl("goal-planning-end-year")?.value || "").trim()
    };
  }

  function createRequestToGoalDto(payload) {
    const p = payload || collectCreateRequest();
    const startIso = normalizeIsoDate(p?.planning?.startDate || p?._startYearRaw);
    const endIso = normalizeIsoDate(p?.planning?.endDate || p?._endYearRaw);
    const startYear = parseYear(startIso) || Number(p?.planning?.startYear || 0) || null;
    const endYear = parseYear(endIso) || Number(p?.planning?.endYear || 0) || null;
    return {
      id: isEditMode ? String(getEl("goal-id")?.value || "").trim() : "",
      name: String(p.goal || "").trim(),
      goalTitle: String(p.goalTitle || p.goal || "").trim(),
      goalTypeId: String(p.goalTypeId || p.categoryCode || "").trim(),
      category: String(p.categoryCode || "").trim(),
      strategicThemeId: String(p.strategicThemeId || "").trim(),
      ownerId: String(p.ownerId || "").trim(),
      ownerRole: String(p.ownerRole || p.ownerId || "").trim(),
      ownerPositionId: String(p.ownerPositionId || p.ownerRole || p.ownerId || "").trim(),
      ownerOrgId: String(p.ownerOrgId || p.ownerCompanyId || p?.companyScope?.primaryCompanyId || "").trim(),
      ownerCompanyId: String(p.ownerCompanyId || p?.companyScope?.primaryCompanyId || "").trim(),
      ownerPersonId: String(p.ownerPersonId || "").trim() || null,
      currentOwnerPersonId: String(p.currentOwnerPersonId || p.ownerPersonId || "").trim() || null,
      owner: String(p.ownerPersonId || "").trim()
        ? resolveUserName(p.ownerPersonId || "")
        : ownerRoleLabelByValue(p.ownerRole || p.ownerId || ""),
      ownerDisplayName: String(p.ownerPersonId || "").trim()
        ? resolveUserName(p.ownerPersonId || "")
        : deriveOwnerDisplay(),
      status: String(p.statusCode || "").trim(),
      priority: String(p.priorityCode || "").trim(),
      statement: String(p.goalStatement || "").trim(),
      strategyPeriodId: String(p?.planning?.strategyPeriodId || "").trim() || null,
      planningHorizonStart: startIso || (startYear ? toStartDateIso(startYear) : null),
      planningHorizonEnd: endIso || (endYear ? toEndDateIso(endYear) : null),
      entityScope: String(p?.planning?.relatedEntityScope || "").trim(),
      changeLogRef: String(p?.planning?.changeLogRef || "").trim(),
      scopeMode: String(p?.companyScope?.scopeModeCode || "").trim(),
      appliesToSelectedCompaniesFlag: Boolean(p?.companyScope?.appliesToSelectedCompaniesFlag),
      appliesToAllCompaniesFlag: Boolean(p?.companyScope?.appliesToAllCompaniesFlag),
      primaryCompanyId: String(p.ownerCompanyId || p?.companyScope?.primaryCompanyId || "").trim() || null,
      applicableCompanyIds: resolveCompanyIds(p?.companyScope?.applicableCompanyIds || []),
      relatedEntityScopeSummary: String(p?.companyScope?.relatedEntityScopeSummary || "").trim(),
      businessUnit: String(p.businessUnit || "").trim(),
      region: String(p.region || "").trim(),
      yearlyBudgets: (p.yearlyBudgets || []).map((b) => ({
        year: Number(b.year),
        revenueTarget: b.revenueTarget ?? null,
        ebitdaTarget: b.ebitdaTarget ?? null,
        capexEnvelope: b.capexEnvelope ?? null,
        opexEnvelope: b.opexEnvelope ?? null,
        savingsTarget: b.savingsTarget ?? null,
        fundingPoolEnvelope: b.fundingPoolEnvelope ?? b.fundingPool ?? null,
        fundingPool: b.fundingPoolEnvelope ?? b.fundingPool ?? null,
        commentary: String(b.commentary || "").trim() || null
      })),
      budgetEnvelopes: (p.yearlyBudgets || []).map((b) => ({
        year: Number(b.year),
        revenueTarget: b.revenueTarget ?? null,
        ebitdaTarget: b.ebitdaTarget ?? null,
        capexEnvelope: b.capexEnvelope ?? null,
        opexEnvelope: b.opexEnvelope ?? null,
        savingsTarget: b.savingsTarget ?? null,
        fundingPool: b.fundingPoolEnvelope ?? b.fundingPool ?? null,
        commentary: String(b.commentary || "").trim() || null
      })),
      metrics: (p.metrics || []).map((m, i) => ({
        id: String(m.metricAssignmentId || m.id || `gm-${i + 1}`),
        metricAssignmentId: String(m.metricAssignmentId || m.id || "").trim(),
        metricDefId: String(m.metricDefId || m.metricDefinitionId || "").trim(),
        metricName: String(m.metricName || "").trim(),
        metricType: String(m.metricTypeCode || m.metricType || "").trim(),
        baselineValue: m.baselineValue ?? null,
        targetValue: m.targetValue ?? null,
        unitOfMeasure: String(m.unitOfMeasureCode || m.unitOfMeasure || "").trim(),
        aggregationMethod: String(m.aggregationMethodCode || m.aggregationMethod || "").trim(),
        polarityCode: String(m.polarityCode || m.directionPolarity || "").trim(),
        thresholdModelCode: String(m.thresholdModelCode || m.thresholdModel || "").trim(),
        reportingFrequencyCode: String(m.reportingFrequencyCode || m.reportingFrequency || "").trim(),
        cascadeMetric: m.cascadeMetric !== false,
        metricOrigin: String(m.metricOrigin || "Local").trim() || "Local",
        metricRole: String(m.metricRole || "Strategic").trim() || "Strategic",
        restrictionMode: String(m.restrictionMode || "GoalGovernedStructure").trim() || "GoalGovernedStructure",
        rollupEligible: m.rollupEligible !== false,
        yearlyValues: ((m.yearlyValues && m.yearlyValues.length ? m.yearlyValues : m.yearlyTargets) || []).map((y) => ({
          year: Number(y.year),
          targetValue: y.targetValue ?? null,
          actualValue: y.actualValue ?? null,
          forecastValue: y.forecastValue ?? null,
          thresholdMin: y.thresholdMin ?? null,
          thresholdMax: y.thresholdMax ?? null,
          commentary: String(y.commentary || "").trim() || null,
          thresholdCommentary: String(y.thresholdCommentary || y.commentary || "").trim() || null
        })),
        sortOrder: Number(m.sortOrder || (i + 1)),
        metricBindingStatus: String(m.metricBindingStatus || "Bound")
      })),
      decisionReference: String(p?.governance?.decisionReference || "").trim() || null,
      evidenceReference: String(p?.governance?.evidenceLink || "").trim() || null,
      version: currentVersion || 1
    };
  }

  async function fetchGoalForEdit(item) {
    const fallback = normalizeGoalRow(item) || {};
    const id = String(fallback.id || "").trim();
    if (!id) return fallback;
    try {
      const detail = await window.strategyGoalsApi.get(id);
      const goal = detail?.goal || detail?.Goal || detail;
      if (goal && typeof goal === "object") {
        return {
          ...fallback,
          ...normalizeGoalRow(goal),
          decisionReference: String(goal.decisionReference || goal.governance?.decisionReference || goal.governance?.reviewDecisionReference || goal.audit?.decisionReference || goal.traceability?.decisionReference || fallback.decisionReference || "").trim(),
          evidenceReference: String(goal.evidenceReference || goal.evidenceLink || goal.governance?.evidenceReference || goal.governance?.evidenceLink || goal.audit?.evidenceReference || goal.audit?.evidenceLink || goal.traceability?.evidenceReference || goal.traceability?.evidenceLink || fallback.evidenceReference || "").trim(),
          budgetEnvelopes: Array.isArray(goal.budgetEnvelopes) ? goal.budgetEnvelopes : (Array.isArray(goal.yearlyBudgets) ? goal.yearlyBudgets : (fallback.budgetEnvelopes || [])),
          yearlyBudgets: Array.isArray(goal.yearlyBudgets) ? goal.yearlyBudgets : (Array.isArray(goal.budgetEnvelopes) ? goal.budgetEnvelopes : (fallback.yearlyBudgets || [])),
          metrics: Array.isArray(goal.metrics) ? goal.metrics : (fallback.metrics || [])
        };
      }
    } catch (_) { }
    return fallback;
  }

  function collectDraftRequiredMissing(payload) {
    const missing = [];
    const ownershipState = resolveCurrentOwnerState();
    const ownerCompanyCandidate = String(payload?.ownerCompanyId || payload?.ownerOrgId || "").trim();
    if (!String(payload?.goal || "").trim()) pushUnique(missing, "Goal Title");
    if (!String(payload?.categoryCode || "").trim()) pushUnique(missing, "Goal Type");
    if (!String(payload?.strategicThemeId || "").trim()) pushUnique(missing, "Strategic Theme / Pillar");
    if (!ownerCompanyCandidate) pushUnique(missing, "Owner Company / Org");
    if (!String(payload?.ownerRole || "").trim()) pushUnique(missing, "Owner Position");
    if (ownershipState.requiresNamedOwner && !String(payload?.ownerPersonId || "").trim()) pushUnique(missing, "Current Owner Person");
    if (!String(payload?.priorityCode || "").trim()) pushUnique(missing, "Priority");
    if (!String(payload?.goalStatement || "").trim()) pushUnique(missing, "Goal Statement");
    if (!String(payload?.planning?.strategyPeriodId || payload?.strategyPeriodId || "").trim()) pushUnique(missing, "Strategy Period");
    if (!String(payload?.companyScope?.scopeModeCode || "").trim()) pushUnique(missing, "Applicability Mode");
    if (!(payload?.metrics || []).some((metric) => isMetricActiveForValidation(metric))) pushUnique(missing, "Primary KPI / Metric");
    return missing;
  }

  function collectPublishGovernanceMissing(payload) {
    const missing = [];
    const changeLogRef = String(getEl("goal-change-log-ref")?.value || payload?.planning?.changeLogRef || "").trim();
    const decisionReference = String(getEl("goal-decision-reference")?.value || payload?.governance?.decisionReference || "").trim();
    const versionValue = Number(getEl("goal-version")?.value || currentVersion || payload?.version || 0);
    if (!changeLogRef) pushUnique(missing, "Change Log Ref");
    if (!decisionReference) pushUnique(missing, "Decision Reference");
    if (isEditMode && (!(Number.isFinite(versionValue)) || versionValue <= 0)) {
      pushUnique(missing, "Version (system-managed)");
    }
    return missing;
  }

  function collectKpiRowsMissingYearlyTargets(payload) {
    return (payload?.metrics || []).filter((metric) => {
      if (!isMetricActiveForValidation(metric)) return false;
      const yearly = (metric.yearlyValues || []).filter((x) => Number.isInteger(Number(x.year)));
      if (!yearly.length) return true;
      return yearly.some((x) => x.targetValue === null || x.targetValue === undefined || Number.isNaN(Number(x.targetValue)));
    });
  }

  function validate(payload, options = {}) {
    const errors = [];
    const mode = normalizeValidationMode(options.mode, payload);
    const strictPublish = mode === "publish";
    if (!payload.goal) errors.push("Goal is required.");
    if (!payload.categoryCode) errors.push("Goal Type is required.");
    if (!String(payload.strategicThemeId || "").trim()) errors.push("Strategic Theme / Pillar is required.");
    if (!String(payload.ownerRole || "").trim()) errors.push("Owner Position is required.");
    const ownerCompanyCandidate = String(payload.ownerCompanyId || payload.ownerOrgId || "").trim();
    if (!ownerCompanyCandidate) errors.push("Owner Company / Org is required.");
    if (!payload.priorityCode) errors.push("Priority is required.");
    if (!payload.goalStatement) errors.push("Goal Statement is required.");
    const ownershipState = resolveCurrentOwnerState();
    if (ownershipState.requiresNamedOwner && !String(payload.ownerPersonId || "").trim()) {
      errors.push("Current Owner Person is required when an active incumbent exists.");
    } else if (String(payload.ownerPersonId || "").trim() && !ownershipState.currentMatches && ownershipState.validUsers.length) {
      errors.push("Current Owner Person must match the selected company or org and position context.");
    }
    const startIso = normalizeIsoDate(payload._startYearRaw);
    const endIso = normalizeIsoDate(payload._endYearRaw);
    const start = parseYear(startIso);
    const end = parseYear(endIso);
    if (!startIso) errors.push("Start Date is required.");
    if (!endIso) errors.push("End Date is required.");
    if (startIso && endIso && endIso < startIso) errors.push("End Date must be on or after Start Date.");
    const selectedStrategyPeriodId = String(payload?.planning?.strategyPeriodId || payload?.strategyPeriodId || "").trim();
    if (!selectedStrategyPeriodId) {
      errors.push("Strategy Period is required.");
    } else {
      const selectedPeriod = strategyPeriodsById.get(selectedStrategyPeriodId) || activeStrategyPeriods.find((x) => String(x.id || "").trim() === selectedStrategyPeriodId) || null;
      if ((!selectedPeriod || !isGoalAssignableStrategyPeriodStatus(selectedPeriod.status)) && !(isEditMode && selectedStrategyPeriodId === initialStrategyPeriodId)) {
        errors.push("Select an Active Strategy Period.");
      } else if (selectedPeriod) {
        const bounds = strategyPeriodDateBounds(selectedPeriod);
        if (bounds && startIso && (startIso < bounds.minIso || startIso > bounds.maxIso)) {
          errors.push(`Start Date must be within Strategy Period range ${formatIsoToDmy(bounds.minIso)} to ${formatIsoToDmy(bounds.maxIso)}.`);
        }
        if (bounds && endIso && (endIso < bounds.minIso || endIso > bounds.maxIso)) {
          errors.push(`End Date must be within Strategy Period range ${formatIsoToDmy(bounds.minIso)} to ${formatIsoToDmy(bounds.maxIso)}.`);
        }
        if (bounds && startIso && endIso && (startIso < bounds.minIso || endIso > bounds.maxIso)) {
          errors.push("Goal horizon cannot exceed Strategy Period horizon.");
        }
        const periodCompanyId = String(selectedPeriod.companyId || "").trim();
        if (periodCompanyId) {
          const scopeIds = new Set(
            [payload?.companyScope?.primaryCompanyId]
              .concat(payload?.companyScope?.applicableCompanyIds || [])
              .map((x) => String(x || "").trim())
              .filter(Boolean)
          );
          if (scopeIds.size > 0 && !scopeIds.has(periodCompanyId)) {
            errors.push("Goal scope must remain compatible with selected Strategy Period scope.");
          }
        }
      }
    }
    const scopeMode = payload.companyScope?.scopeModeCode || "";
    const appliesAllCompanies = scopeMode === "Enterprise";
    const applicable = payload.companyScope?.applicableCompanyIds || [];
    if (!scopeMode) errors.push("Applicability Mode is required.");
    if (scopeMode === "MultiCompany" && !appliesAllCompanies && !applicable.length) errors.push("At least one Applicable Company is required for selected-company applicability.");
    if (scopeMode === "Enterprise" && applicable.length) errors.push("Applicable Companies must be empty for Enterprise applicability.");
    if (appliesAllCompanies && applicable.length) errors.push("Applicable Companies must be empty when Applies To All Companies is enabled.");
    if (!String(payload?.companyScope?.relatedEntityScopeSummary || "").trim()) errors.push("Company applicability must produce a valid Related Entity Scope Summary.");
    if (!isValidAbsoluteUrl(payload.governance?.evidenceLink || "")) errors.push("Evidence Link must be a valid URL.");
    const metricValidationRows = buildMetricValidationRows(payload, start, end, { mode });
    metricValidationRows.forEach((row) => {
      row.messages.forEach((message) => errors.push(`${row.label}: ${message}`));
    });
    const activeMetrics = (payload.metrics || []).filter((m) => isMetricActiveForValidation(m));
    if (!activeMetrics.length) errors.push("Primary KPI / Metric is required.");
    const bud = payload.yearlyBudgets || [];
    if (bud.length > 0) {
      const byYear = new Set(bud.map((x) => Number(x.year)));
      if (byYear.size !== bud.length) errors.push("Goal yearly budget has duplicate years.");
      if (start && end && end >= start) {
        const outOfRange = bud
          .map((x) => Number(x.year))
          .filter((y) => Number.isInteger(y) && (y < start || y > end));
        if (outOfRange.length) errors.push(`Goal yearly budget contains out-of-range year(s): ${[...new Set(outOfRange)].join(", ")}.`);
      }
    }
    if (strictPublish) {
      if (activeMetrics.length < 1) errors.push("At least one active KPI is required for publish.");
      const publishGovernanceMissing = collectPublishGovernanceMissing(payload);
      publishGovernanceMissing.forEach((name) => {
        if (name.toLowerCase().includes("change log")) errors.push("ChangeLogRef is required for publish.");
        if (name.toLowerCase().includes("decision")) errors.push("DecisionReference is required for publish.");
        if (name.toLowerCase().includes("version")) errors.push("Version is required for publish.");
      });
    }
    return errors;
  }

  function buildMetricValidationRows(payload, startYear, endYear, options = {}) {
    const mode = normalizeValidationMode(options.mode, payload);
    const strictPublish = mode === "publish";
    const originsOk = ["local", "inherited"];
    const rolesOk = ["strategic"];
    const restrOk = ["goalgovernedstructure", "localeditable", "parentgovernedstructure"];
    return (payload.metrics || []).map((m, i) => {
      const metricActive = isMetricActiveForValidation(m);
      const label = String(m.metricName || "").trim() || `Metric row ${i + 1}`;
      const messages = [];
      const selectors = new Set();
      if (metricActive && !String(m.metricName || "").trim()) {
        messages.push("Goal Metric is required.");
        selectors.add(".metric-name");
      }
      if (metricActive && !String(m.metricTypeCode || m.metricType || "").trim()) {
        messages.push("Goal Metric Type is required.");
        selectors.add(".metric-type");
      }
      if (metricActive && !String(m.unitOfMeasureCode || m.unitOfMeasure || "").trim()) {
        messages.push("Unit of Measure is required.");
        selectors.add(".metric-unit");
      }
      if (metricActive && !String(m.aggregationMethodCode || m.aggregationMethod || "").trim()) {
        messages.push("Aggregation Method is required.");
        selectors.add(".metric-aggregation");
      }
      if (metricActive && !String(m.polarityCode || m.directionPolarity || "").trim()) {
        messages.push("Direction / Polarity is required.");
        selectors.add(".metric-polarity");
      }
      if (metricActive && !String(m.thresholdModelCode || m.thresholdModel || "").trim()) {
        messages.push("Threshold Model is required.");
        selectors.add(".metric-threshold-model");
      }
      if (metricActive && !String(m.reportingFrequencyCode || m.reportingFrequency || "").trim()) {
        messages.push("Reporting Frequency is required.");
        selectors.add(".metric-reporting-frequency");
      }
      if (!originsOk.includes(String(m.metricOrigin || "").toLowerCase())) {
        messages.push("Metric Origin must be Local or Inherited.");
        selectors.add(".metric-origin");
      }
      if (!rolesOk.includes(String(m.metricRole || "").toLowerCase())) {
        messages.push("Metric Role must be Strategic.");
        selectors.add(".metric-role");
      }
      if (!restrOk.includes(String(m.restrictionMode || "").toLowerCase())) {
        messages.push("Restriction Mode is invalid.");
        selectors.add(".metric-restriction");
      }
      const years = (m.yearlyValues || []).map((x) => Number(x.year)).filter((x) => Number.isInteger(x));
      if (metricActive && !years.length) {
        messages.push("Yearly Plan is empty.");
        selectors.add(".metric-year-table");
      } else if (metricActive && startYear && endYear) {
        const missingYears = [];
        for (let year = startYear; year <= endYear; year++) {
          if (!years.includes(year)) missingYears.push(year);
        }
        if (missingYears.length) {
          messages.push(`Yearly Plan missing year(s): ${missingYears.join(", ")}.`);
          selectors.add(".metric-year-table");
        }
        const outOfRangeYears = years.filter((y) => y < startYear || y > endYear);
        if (outOfRangeYears.length) {
          messages.push(`Yearly Plan contains out-of-range year(s): ${[...new Set(outOfRangeYears)].join(", ")}.`);
          selectors.add(".metric-year-table");
        }
      }
      if (years.length) {
        const duplicateYears = years.filter((year, idx) => years.indexOf(year) !== idx);
        if (duplicateYears.length) {
          messages.push(`Duplicate yearly rows found: ${[...new Set(duplicateYears)].join(", ")}.`);
          selectors.add(".metric-year-table");
        }
      }
      const yearlyRows = Array.isArray(m.yearlyValues) ? m.yearlyValues : [];
      if (metricActive && yearlyRows.length) {
        const yearsMissingTarget = yearlyRows
          .filter((x) => Number.isInteger(Number(x.year)) && (x.targetValue === null || x.targetValue === undefined || Number.isNaN(Number(x.targetValue))))
          .map((x) => Number(x.year));
        if (yearsMissingTarget.length) {
          messages.push(`Target Value is required for year(s): ${yearsMissingTarget.join(", ")}.`);
          selectors.add(".metric-year-table");
          selectors.add(".metric-year-target");
        }
      }
      const thresholdRequired = metricThresholdsRequired(m.thresholdModelCode || m.thresholdModel || "");
      if (metricActive && thresholdRequired && yearlyRows.length) {
        const yearsMissingThresholdMin = yearlyRows
          .filter((x) => Number.isInteger(Number(x.year)) && (x.thresholdMin === null || x.thresholdMin === undefined || Number.isNaN(Number(x.thresholdMin))))
          .map((x) => Number(x.year));
        if (yearsMissingThresholdMin.length) {
          messages.push(`Threshold Min is required for year(s): ${yearsMissingThresholdMin.join(", ")}.`);
          selectors.add(".metric-year-table");
          selectors.add(".metric-year-threshold-min");
        }
        const yearsMissingThresholdMax = yearlyRows
          .filter((x) => Number.isInteger(Number(x.year)) && (x.thresholdMax === null || x.thresholdMax === undefined || Number.isNaN(Number(x.thresholdMax))))
          .map((x) => Number(x.year));
        if (yearsMissingThresholdMax.length) {
          messages.push(`Threshold Max is required for year(s): ${yearsMissingThresholdMax.join(", ")}.`);
          selectors.add(".metric-year-table");
          selectors.add(".metric-year-threshold-max");
        }
      }
      return { index: i, label, messages, selectors: [...selectors] };
    });
  }

  function clearMetricCardErrors() {
    Array.from(metricHost?.querySelectorAll(".metric-row") || []).forEach((row) => {
      row.classList.remove("metric-row-error");
      row.querySelectorAll(".metric-field-invalid").forEach((el) => el.classList.remove("metric-field-invalid"));
      row.querySelectorAll(".is-invalid").forEach((el) => el.classList.remove("is-invalid"));
      const box = row.querySelector(".metric-card-error");
      if (box) {
        box.textContent = "";
        box.classList.add("d-none");
        box.style.display = "";
      }
    });
  }

  function applyMetricCardErrors(payload, options = {}) {
    clearMetricCardErrors();
    const startYear = parseYear(payload?._startYearRaw);
    const endYear = parseYear(payload?._endYearRaw);
    const rows = buildMetricValidationRows(payload || {}, startYear, endYear, options);
    const metricEls = Array.from(metricHost?.querySelectorAll(".metric-row") || []);
    let openedInvalidRow = false;
    rows.forEach((entry) => {
      if (!entry.messages.length) return;
      const rowEl = metricEls[entry.index];
      if (!rowEl) return;
      rowEl.classList.add("metric-row-error");
      if (!openedInvalidRow) {
        revealSection("goal-sec-metrics");
        collapseOtherMetrics(rowEl);
        setMetricExpanded(rowEl, true);
        const selectorsText = (entry.selectors || []).join(" ");
        if (selectorsText.includes("metric-year")) activateMetricTab(rowEl, "yearly");
        else if (selectorsText.includes(".metric-cascade") || selectorsText.includes(".metric-origin") || selectorsText.includes(".metric-role") || selectorsText.includes(".metric-restriction") || selectorsText.includes(".metric-rollup")) activateMetricTab(rowEl, "governance");
        else activateMetricTab(rowEl, "definition");
        openedInvalidRow = true;
      }
      entry.selectors.forEach((selector) => {
        rowEl.querySelectorAll(selector).forEach((el) => {
          el.classList.add("is-invalid");
          const host = el.closest(".col-12, .col-lg-6, td, .metric-year-wrap, .goal-metric-band");
          if (host) host.classList.add("metric-field-invalid");
        });
      });
      const box = rowEl.querySelector(".metric-card-error");
      if (!box) return;
      box.innerHTML = `<strong>${entry.label}:</strong> ${entry.messages.join(" ")}`;
      box.classList.remove("d-none");
      box.style.display = "block";
    });
  }

  function sectionTitle(sectionId) {
    const titles = {
      "goal-sec-identity": "Strategic Identity",
      "goal-sec-ownership": "Ownership & Accountability",
      "goal-sec-planning": "Planning & Horizon",
      "goal-sec-company": "Company Applicability",
      "goal-sec-metrics": "Strategic KPI & Yearly Targets",
      "goal-sec-budget": "Strategic Budget Envelope",
      "goal-sec-governance": "Governance & Traceability"
    };
    return titles[sectionId] || "Form Section";
  }

  function buildErrorShortcuts(fieldMap) {
    if (!(fieldMap instanceof Map) || fieldMap.size === 0) return "";
    const seenSections = new Set();
    const links = [];
    fieldMap.forEach((_, fieldId) => {
      const sectionId = sectionByField[fieldId];
      if (!sectionId || seenSections.has(sectionId)) return;
      seenSections.add(sectionId);
      links.push(
        `<button type="button" class="goal-error-jump btn btn-sm btn-outline-danger" data-section-id="${sectionId}" data-field-id="${fieldId}">${sectionTitle(sectionId)}</button>`
      );
    });
    if (!links.length) return "";
    return `<div class="goal-error-shortcuts mt-2"><span class="small me-2">Go to:</span>${links.join("")}</div>`;
  }

  function mergeFieldMaps(primary, fallback) {
    const out = new Map();
    (primary instanceof Map ? primary : new Map()).forEach((v, k) => out.set(k, v));
    (fallback instanceof Map ? fallback : new Map()).forEach((v, k) => {
      if (!out.has(k)) out.set(k, v);
    });
    return out;
  }

  function inferFieldMapFromMessages(messages, payload) {
    const out = new Map();
    const list = (messages || []).map((x) => String(x || "").toLowerCase());
    list.forEach((text) => {
      if (text.includes("metric")) out.set("goal-metrics-editor", "Check metric definition/yearly plan fields.");
      if (text.includes("budget") || text.includes("funding") || text.includes("capex") || text.includes("opex")) out.set("goal-budget-year-table", "Check yearly budget rows.");
      if (text.includes("scope mode")) out.set("goal-scope-mode", "Applicability Mode is required.");
      if (text.includes("applicable compan")) out.set("goal-applicable-companies", "Applicable Companies are required.");
      if (text.includes("strategy period")) out.set("goal-strategy-period", "Strategy Period selection is invalid.");
      if (text.includes("start year") || text.includes("start date")) out.set("goal-planning-start-year", "Start Date is required.");
      if (text.includes("end year") || text.includes("end date")) out.set("goal-planning-end-year", "End Date is required.");
      if (text.includes("owner position")) out.set(ownerRoleFieldId(), "Owner Position is required.");
      if (text.includes("owner company") || text.includes("owner org")) out.set(ownerCompanyFieldId(), "Owner Company / Org is required.");
      if (text.includes("current owner person")) out.set("goal-owner-person-display", "Current Owner Person is required.");
      if (text.includes("owner")) out.set(ownerRoleFieldId(), "Owner Position is required.");
      if (text.includes("goal type")) out.set("goal-category", "Goal Type is required.");
      if (text.includes("strategic theme")) out.set("goal-strategic-theme", "Strategic Theme / Pillar is required.");
      if (text.includes("goal statement")) out.set("goal-statement", "Goal Statement is required.");
      if (text.includes("evidence")) out.set("goal-evidence-reference", "Evidence Link is invalid or missing.");
    });
    if (!out.size) {
      const payloadMap = fieldErrorMap(payload || collectCreateRequest());
      if (payloadMap.size) return payloadMap;
    }
    return out;
  }

  function enrichStrategyPeriodBoundaryMessage(message, payload) {
    const text = String(message || "").trim();
    if (!text) return text;
    const selectedStrategyPeriodId = String(payload?.planning?.strategyPeriodId || payload?.strategyPeriodId || "").trim();
    if (!selectedStrategyPeriodId) return text;
    const selectedPeriod =
      strategyPeriodsById.get(selectedStrategyPeriodId) ||
      activeStrategyPeriods.find((x) => String(x.id || "").trim() === selectedStrategyPeriodId) ||
      null;
    const bounds = strategyPeriodDateBounds(selectedPeriod);
    if (!bounds) return text;
    const startDmy = formatIsoToDmy(bounds.minIso);
    const endDmy = formatIsoToDmy(bounds.maxIso);
    const lc = text.toLowerCase();
    if (lc.includes("start date must be on or after the strategy period start date")) {
      return `${text} (Strategy Period Start: ${startDmy})`;
    }
    if (lc.includes("end date must be on or before the strategy period end date")) {
      return `${text} (Strategy Period End: ${endDmy})`;
    }
    return text;
  }

  function showErrors(errors, fieldMap, options = {}) {
    if (!errorEl) return;
    const mode = normalizeValidationMode(options.mode || activeValidationMode, collectCreateRequest());
    if (!errors.length) {
      errorEl.classList.add("d-none");
      errorEl.textContent = "";
      return;
    }
    errorEl.classList.remove("d-none");
    errorEl.innerHTML =
      `<strong>Please fix the following (${mode === "publish" ? "Publish / strict" : "Draft / light"}):</strong>` +
      `<ul class="mb-0">${errors.map((e) => `<li>${e}</li>`).join("")}</ul>${buildErrorShortcuts(fieldMap)}`;
    errorEl.querySelectorAll(".goal-error-jump").forEach((btn) => {
      btn.addEventListener("click", () => {
        revealSection(btn.dataset.sectionId || "");
        const section = document.getElementById(btn.dataset.sectionId || "");
        const field = getEl(btn.dataset.fieldId || "");
        const target = field || section;
        target?.scrollIntoView?.({ behavior: "smooth", block: "center" });
        if (field && typeof field.focus === "function") field.focus();
      });
    });
    const e0 = errors.join(" ").toLowerCase();
    if (e0.includes("budget")) {
      revealSection("goal-sec-budget");
    }
  }

  function fieldErrorMap(payload, options = {}) {
    const out = new Map();
    const mode = normalizeValidationMode(options.mode, payload);
    const strictPublish = mode === "publish";
    if (!payload.goal) out.set("goal-name", "Goal is required.");
    if (!payload.categoryCode) out.set("goal-category", "Goal Type is required.");
    if (!String(payload.strategicThemeId || "").trim()) out.set("goal-strategic-theme", "Strategic Theme / Pillar is required.");
    if (!String(payload.ownerRole || "").trim()) out.set(ownerRoleFieldId(), "Owner Position is required.");
    const ownerCompanyCandidate = String(payload.ownerCompanyId || payload.ownerOrgId || "").trim();
    if (!ownerCompanyCandidate) out.set(ownerCompanyFieldId(), "Owner Company / Org is required.");
    if (!payload.priorityCode) out.set("goal-priority", "Priority is required.");
    if (!String(payload.goalStatement || "").trim()) out.set("goal-statement", "Goal Statement is required.");
    const ownershipState = resolveCurrentOwnerState();
    if (ownershipState.requiresNamedOwner && !String(payload.ownerPersonId || "").trim()) {
      out.set("goal-owner-person-display", "Current Owner Person is required when an active incumbent exists.");
    } else if (String(payload.ownerPersonId || "").trim() && !ownershipState.currentMatches && ownershipState.validUsers.length) {
      out.set("goal-owner-person-display", "Current Owner Person must match the selected company or org and position context.");
    }
    const startIso = normalizeIsoDate(payload._startYearRaw);
    const endIso = normalizeIsoDate(payload._endYearRaw);
    const start = parseYear(startIso);
    const end = parseYear(endIso);
    if (!startIso) out.set("goal-planning-start-year", "Start Date is required.");
    if (!endIso) out.set("goal-planning-end-year", "End Date is required.");
    if (startIso && endIso && endIso < startIso) out.set("goal-planning-end-year", "End Date must be on or after Start Date.");
    const selectedStrategyPeriodId = String(payload?.planning?.strategyPeriodId || payload?.strategyPeriodId || "").trim();
    if (!selectedStrategyPeriodId) {
      out.set("goal-strategy-period", "Strategy Period is required.");
    } else {
      const selectedPeriod = strategyPeriodsById.get(selectedStrategyPeriodId) || activeStrategyPeriods.find((x) => String(x.id || "").trim() === selectedStrategyPeriodId) || null;
      if (!selectedPeriod || !isGoalAssignableStrategyPeriodStatus(selectedPeriod.status)) {
        if (!(isEditMode && selectedStrategyPeriodId === initialStrategyPeriodId)) {
          out.set("goal-strategy-period", "Select an Active Strategy Period.");
        }
      } else {
        const bounds = strategyPeriodDateBounds(selectedPeriod);
        if (bounds && startIso && (startIso < bounds.minIso || startIso > bounds.maxIso)) {
          out.set("goal-planning-start-year", `Start Date must be within Strategy Period range ${formatIsoToDmy(bounds.minIso)} to ${formatIsoToDmy(bounds.maxIso)}.`);
        }
        if (bounds && endIso && (endIso < bounds.minIso || endIso > bounds.maxIso)) {
          out.set("goal-planning-end-year", `End Date must be within Strategy Period range ${formatIsoToDmy(bounds.minIso)} to ${formatIsoToDmy(bounds.maxIso)}.`);
        }
        if (bounds && startIso && endIso && (startIso < bounds.minIso || endIso > bounds.maxIso)) {
          out.set("goal-planning-end-year", "Goal horizon cannot exceed Strategy Period horizon.");
        }
        const periodCompanyId = String(selectedPeriod.companyId || "").trim();
        if (periodCompanyId) {
          const scopeIds = new Set(
            [payload?.companyScope?.primaryCompanyId]
              .concat(payload?.companyScope?.applicableCompanyIds || [])
              .map((x) => String(x || "").trim())
              .filter(Boolean)
          );
          if (scopeIds.size > 0 && !scopeIds.has(periodCompanyId)) {
            out.set("goal-related-entity-scope-summary", "Goal scope must remain compatible with selected Strategy Period scope.");
          }
        }
      }
    }
    const scopeMode = payload.companyScope?.scopeModeCode || "";
    const appliesAllCompanies = scopeMode === "Enterprise";
    const applicable = payload.companyScope?.applicableCompanyIds || [];
    if (!scopeMode) out.set("goal-scope-mode", "Applicability Mode is required.");
    if (scopeMode === "MultiCompany" && !appliesAllCompanies && !applicable.length) out.set("goal-applicable-companies", "Applicable Companies are required for selected-company applicability.");
    if (scopeMode === "Enterprise" && applicable.length) out.set("goal-applicable-companies", "Applicable Companies must be empty for Enterprise applicability.");
    if (appliesAllCompanies && applicable.length) out.set("goal-applicable-companies", "Applicable Companies must be empty when Applies To All Companies is enabled.");
    if (!String(payload?.companyScope?.relatedEntityScopeSummary || "").trim()) out.set("goal-related-entity-scope-summary", "Company applicability must produce a valid scope summary.");
    if (!isValidAbsoluteUrl(payload.governance?.evidenceLink || "")) out.set("goal-evidence-reference", "Must be a valid URL.");
    const startYear = parseYear(payload._startYearRaw);
    const endYear = parseYear(payload._endYearRaw);
    const metricRows = payload.metrics || [];
    const activeMetricRows = metricRows.filter((m) => isMetricActiveForValidation(m));
    const hasMetricShapeError = activeMetricRows.some((m) =>
      !m.metricName || !m.metricTypeCode || !m.unitOfMeasureCode || !m.aggregationMethodCode || !m.polarityCode || !m.thresholdModelCode || !m.reportingFrequencyCode);
    const hasMetricYearsError = activeMetricRows.some((m) => {
      const years = (m.yearlyValues || []).map((x) => Number(x.year)).filter((x) => Number.isInteger(x));
      if (!years.length) return true;
      if (!(startYear && endYear)) return false;
      for (let year = startYear; year <= endYear; year++) {
        if (!years.includes(year)) return true;
      }
      return false;
    });
    if (activeMetricRows.length < 1 || hasMetricShapeError || hasMetricYearsError) {
      const metricIssues = buildMetricValidationRows(payload, startYear, endYear, { mode }).filter((x) => x.messages.length > 0);
      const hint = metricIssues.length
        ? `Check "${metricIssues[0].label}" — ${metricIssues[0].messages[0]}`
        : "Complete active KPI definition fields and yearly plan rows.";
      out.set("goal-metrics-editor", hint);
    }
    const bud = payload.yearlyBudgets || [];
    if (bud.length > 0) {
      const byYear = new Set(bud.map((x) => Number(x.year)));
      if (byYear.size !== bud.length) out.set("goal-budget-year-table", "Yearly budget rows must have unique years.");
      if (start && end && end >= start) {
        const outOfRange = bud
          .map((x) => Number(x.year))
          .filter((y) => Number.isInteger(y) && (y < start || y > end));
        if (outOfRange.length) out.set("goal-budget-year-table", `Yearly budget contains out-of-range years (${start}–${end} allowed).`);
      }
    }
    if (strictPublish) {
      const governanceMissing = collectPublishGovernanceMissing(payload);
      governanceMissing.forEach((item) => {
        if (item.toLowerCase().includes("change log")) out.set("goal-change-log-ref", "Change Log Ref is required for publish.");
        if (item.toLowerCase().includes("decision")) out.set("goal-decision-reference", "Decision Reference is required for publish.");
        if (item.toLowerCase().includes("version")) out.set("goal-version", "Version is required for publish.");
      });
    }
    return out;
  }

  function highlightErrorSections(map) {
    const sectionIds = [...new Set(Object.values(sectionByField))];
    sectionIds.forEach((id) => {
      const section = document.getElementById(id);
      if (!section) return;
      section.classList.remove("es-goal-section-error");
    });
    map.forEach((_, fieldId) => {
      const sectionId = sectionByField[fieldId];
      if (!sectionId) return;
      const section = document.getElementById(sectionId);
      if (section) section.classList.add("es-goal-section-error");
    });
  }

  function setSectionState(sectionId, state, label) {
    const badge = document.getElementById(`${sectionId}-state`)
      || document.querySelector(`#${sectionId} .es-goal-section-state`);
    if (!badge) return;
    badge.classList.remove("state-complete", "state-progress", "state-empty", "state-optional", "state-error");
    badge.classList.add(`state-${state}`);
    badge.textContent = label;
  }

  function metricRowHasDefinitionAndYears(metric, planningYears) {
    if (!metric) return false;
    const hasDefinition = Boolean(
      String(metric.metricName || "").trim() &&
      String(metric.metricType || metric.metricTypeCode || "").trim() &&
      String(metric.unitOfMeasure || metric.unitOfMeasureCode || "").trim() &&
      String(metric.aggregationMethod || metric.aggregationMethodCode || "").trim() &&
      String(metric.polarityCode || "").trim() &&
      String(metric.thresholdModelCode || "").trim() &&
      String(metric.reportingFrequencyCode || "").trim()
    );
    if (!hasDefinition) return false;
    const yearly = Array.isArray(metric.yearlyValues) ? metric.yearlyValues : [];
    if (!yearly.length) return false;
    if (!planningYears.length) return true;
    return planningYears.every((year) => yearly.some((row) => Number(row?.year) === year));
  }

  function computeValidationSnapshot(payload) {
    const draftErrors = validate(payload, { mode: "draft" });
    const publishErrors = validate(payload, { mode: "publish" });
    const draftRequiredMissing = collectDraftRequiredMissing(payload);
    const publishGovernanceMissing = collectPublishGovernanceMissing(payload);
    const metrics = payload?.metrics || [];
    const activeMetrics = metrics.filter((metric) => isMetricActiveForValidation(metric));
    const kpiRowsMissingYearlyTargets = collectKpiRowsMissingYearlyTargets(payload);
    const budgetEnabled = isBudgetEnvelopeEnabled();

    const publishBlockers = [];
    draftRequiredMissing.forEach((x) => pushUnique(publishBlockers, x));
    if (activeMetrics.length < 1) pushUnique(publishBlockers, "At least 1 active KPI");
    if (kpiRowsMissingYearlyTargets.length > 0) pushUnique(publishBlockers, `KPI rows missing yearly targets (${kpiRowsMissingYearlyTargets.length})`);
    publishGovernanceMissing.forEach((x) => pushUnique(publishBlockers, x));

    const draftFieldMap = fieldErrorMap(payload, { mode: "draft" });
    const publishFieldMap = fieldErrorMap(payload, { mode: "publish" });
    const alignmentMismatches = [];
    const requiredFieldByLabel = {
      "Goal Title": "goal-name",
      "Goal Type": "goal-category",
      "Strategic Theme / Pillar": "goal-strategic-theme",
      "Owner Company / Org": ownerCompanyFieldId(),
      "Owner Position": ownerRoleFieldId(),
      "Current Owner Person": "goal-owner-person-display",
      "Priority": "goal-priority",
      "Goal Statement": "goal-statement",
      "Strategy Period": "goal-strategy-period",
      "Applicability Mode": "goal-scope-mode",
      "Primary KPI / Metric": "goal-metrics-editor"
    };
    draftRequiredMissing.forEach((label) => {
      const fieldId = requiredFieldByLabel[label];
      if (!fieldId || !draftFieldMap.has(fieldId)) alignmentMismatches.push(`Draft coverage missing for ${label}.`);
    });
    if (activeMetrics.length < 1 && !publishFieldMap.has("goal-metrics-editor")) {
      alignmentMismatches.push("Publish coverage missing for active KPI requirement.");
    }
    if (kpiRowsMissingYearlyTargets.length > 0 && !publishFieldMap.has("goal-metrics-editor")) {
      alignmentMismatches.push("Publish coverage missing for KPI yearly-target requirement.");
    }
    publishGovernanceMissing.forEach((label) => {
      if (label.includes("Change Log") && !publishFieldMap.has("goal-change-log-ref")) alignmentMismatches.push("Publish coverage missing for ChangeLogRef.");
      if (label.includes("Decision") && !publishFieldMap.has("goal-decision-reference")) alignmentMismatches.push("Publish coverage missing for DecisionReference.");
      if (label.includes("Version") && !publishFieldMap.has("goal-version")) alignmentMismatches.push("Publish coverage missing for Version.");
    });

    return {
      draftErrors,
      publishErrors,
      draftRequiredMissing,
      publishGovernanceMissing,
      metricsCount: metrics.length,
      activeKpiCount: activeMetrics.length,
      kpiRowsMissingYearlyTargets,
      budgetEnabled,
      publishBlockers,
      backendAlignmentOk: alignmentMismatches.length === 0,
      backendAlignmentNotes: alignmentMismatches
    };
  }

  function renderValidationReadiness(snapshot, mode) {
    if (!readinessPanelEl || !snapshot) return;
    const publishReady = snapshot.publishErrors.length === 0;
    if (publishReadinessIndicatorEl) {
      publishReadinessIndicatorEl.classList.remove("state-ready", "state-blocked");
      publishReadinessIndicatorEl.classList.add(publishReady ? "state-ready" : "state-blocked");
      publishReadinessIndicatorEl.textContent = publishReady ? "Goal Readiness: Ready" : "Goal Readiness: Blocked";
    }
    if (publishReadinessTextEl) {
      publishReadinessTextEl.textContent = `${snapshot.draftErrors.length} draft issue(s), ${snapshot.publishErrors.length} publish issue(s).`;
    }
    if (validationModeIndicatorEl) {
      validationModeIndicatorEl.textContent = `Validation mode: ${mode === "publish" ? "Publish (strict)" : "Draft (light)"}`;
    }
    if (backendAlignmentIndicatorEl) {
      backendAlignmentIndicatorEl.textContent = snapshot.backendAlignmentOk
        ? "Backend alignment: OK (Draft/Publish core rules)"
        : `Backend alignment: ${snapshot.backendAlignmentNotes.length} issue(s)`;
      backendAlignmentIndicatorEl.classList.toggle("text-danger", !snapshot.backendAlignmentOk);
      backendAlignmentIndicatorEl.title = snapshot.backendAlignmentOk
        ? "Frontend validation coverage matches backend core rules."
        : snapshot.backendAlignmentNotes.join(" ");
    }
    if (readinessKpiCountEl) readinessKpiCountEl.textContent = `KPI count: ${snapshot.metricsCount} (active: ${snapshot.activeKpiCount})`;
    if (readinessKpiMissingYearlyEl) readinessKpiMissingYearlyEl.textContent = `KPI rows missing yearly targets: ${snapshot.kpiRowsMissingYearlyTargets.length}`;
    if (readinessBudgetEnabledEl) readinessBudgetEnabledEl.textContent = `Budget block: ${snapshot.budgetEnabled ? "enabled" : "disabled"}`;
    if (readinessGovernanceMissingEl) readinessGovernanceMissingEl.textContent = `Governance publish fields missing: ${snapshot.publishGovernanceMissing.length}`;
    if (readinessMissingRequiredEl) readinessMissingRequiredEl.innerHTML = fieldListHtml(snapshot.draftRequiredMissing);
    if (readinessPublishBlockersEl) readinessPublishBlockersEl.innerHTML = fieldListHtml(snapshot.publishBlockers);
  }

  function updateSectionCompletionStates(options = {}) {
    if (!isWorkspaceMode) return;
    const createPayload = options.payload || collectCreateRequest();
    const mode = normalizeValidationMode(options.mode || activeValidationMode, createPayload);
    const metrics = getMetrics();
    const planningYears = getPlanningYears();

    const identityFilled = [
      String(createPayload.goal || "").trim(),
      String(createPayload.categoryCode || "").trim(),
      String(createPayload.strategicThemeId || "").trim(),
      String(createPayload.priorityCode || "").trim(),
      String(createPayload.goalStatement || "").trim()
    ].filter(Boolean).length;
    if (identityFilled === 0) setSectionState("goal-sec-identity", "empty", "Empty");
    else if (identityFilled >= 5) setSectionState("goal-sec-identity", "complete", "Complete");
    else setSectionState("goal-sec-identity", "progress", "In progress");

    const ownerCompanyCandidate = String(createPayload.ownerCompanyId || createPayload.ownerOrgId || "").trim();
    const ownershipFilled = [
      ownerCompanyCandidate,
      String(createPayload.ownerRole || "").trim(),
      String(createPayload.ownerPersonId || "").trim()
    ].filter(Boolean).length;
    if (ownershipFilled === 0) setSectionState("goal-sec-ownership", "empty", "Empty");
    else if (ownershipFilled >= 3) setSectionState("goal-sec-ownership", "complete", "Complete");
    else setSectionState("goal-sec-ownership", "progress", "In progress");

    const planningFilled = [
      String(createPayload?.planning?.strategyPeriodId || "").trim(),
      normalizeIsoDate(createPayload?._startYearRaw),
      normalizeIsoDate(createPayload?._endYearRaw)
    ].filter(Boolean).length;
    if (planningFilled === 0) setSectionState("goal-sec-planning", "empty", "Empty");
    else if (planningFilled === 3) setSectionState("goal-sec-planning", "complete", "Complete");
    else setSectionState("goal-sec-planning", "progress", "In progress");

    const scopeMode = String(createPayload?.companyScope?.scopeModeCode || "").trim();
    const applicableIds = createPayload?.companyScope?.applicableCompanyIds || [];
    if (!scopeMode) {
      setSectionState("goal-sec-company", "empty", "Empty");
    } else if (scopeMode === "Enterprise" || applicableIds.length > 0) {
      setSectionState("goal-sec-company", "complete", "Complete");
    } else {
      setSectionState("goal-sec-company", "progress", "In progress");
    }

    if (!metrics.length) {
      setSectionState("goal-sec-metrics", "empty", "Empty");
    } else {
      const completeMetrics = metrics.filter((metric) => metricRowHasDefinitionAndYears(metric, planningYears)).length;
      if (completeMetrics === metrics.length) setSectionState("goal-sec-metrics", "complete", `${metrics.length} complete`);
      else setSectionState("goal-sec-metrics", "progress", `${completeMetrics}/${metrics.length} complete`);
    }

    const budgetEnabled = isBudgetEnvelopeEnabled();
    const budgets = collectYearlyBudgetsFromDom();
    const hasBudgetData = budgets.length > 0;
    if (!budgetEnabled) {
      setSectionState("goal-sec-budget", "optional", "Optional");
    } else if (!hasBudgetData) {
      setSectionState("goal-sec-budget", "progress", "Enabled");
    } else if (!planningYears.length || budgets.every((row) => Number.isInteger(Number(row.year)) && planningYears.includes(Number(row.year)))) {
      setSectionState("goal-sec-budget", "complete", "Complete");
    } else {
      setSectionState("goal-sec-budget", "progress", "In progress");
    }

    const governanceFields = [
      String(getEl("goal-change-log-ref")?.value || "").trim(),
      String(getEl("goal-decision-reference")?.value || "").trim(),
      String(getEl("goal-evidence-reference")?.value || "").trim(),
      String(getEl("goal-version")?.value || "").trim()
    ];
    const governanceFilled = governanceFields.filter(Boolean).length;
    if (!governanceFilled) setSectionState("goal-sec-governance", "optional", "Optional");
    else if (governanceFilled >= 3) setSectionState("goal-sec-governance", "complete", "Complete");
    else setSectionState("goal-sec-governance", "progress", "In progress");

    const blockingMap = options.fieldMap instanceof Map ? options.fieldMap : fieldErrorMap(createPayload, { mode });
    const blockedSections = new Set();
    blockingMap.forEach((_, fieldId) => {
      const sectionId = sectionByField[fieldId];
      if (sectionId) blockedSections.add(sectionId);
    });
    blockedSections.forEach((sectionId) => setSectionState(sectionId, "error", "Blocked"));
    if (wizardStepButtons.length) {
      wizardStepButtons.forEach((btn) => btn.classList.remove("is-blocked"));
      blockedSections.forEach((sectionId) => {
        const step = wizardStepBySection[sectionId];
        const btn = wizardStepButtons.find((item) => Number(item.dataset.step || 0) === step);
        if (btn && step !== currentWizardStep) btn.classList.add("is-blocked");
      });
      updateWizardStepStates();
    }

    const snapshot = options.snapshot || computeValidationSnapshot(createPayload);
    renderValidationReadiness(snapshot, mode);
  }

  function applyFieldErrors(payload, map = fieldErrorMap(payload), options = {}) {
    fieldIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(getEl(id)));
    map.forEach((message, id) => window.enterpriseModalFormUtils?.setFieldError?.(getEl(id), message));
    applyMetricCardErrors(payload, options);
    highlightErrorSections(map);
    const firstErrorFieldId = map.keys().next().value;
    if (firstErrorFieldId) {
      const sectionId = sectionByField[firstErrorFieldId];
      if (sectionId) revealSection(sectionId);
      const section = sectionId ? document.getElementById(sectionId) : null;
      const target = getEl(firstErrorFieldId) || section;
      target?.scrollIntoView?.({ behavior: "smooth", block: "center" });
    }
  }

  function applyValidation() {
    const payload = collectCreateRequest();
    const mode = normalizeValidationMode(activeValidationMode, payload);
    const snapshot = computeValidationSnapshot(payload);
    if (!hasSubmitAttempt) {
      fieldIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(getEl(id)));
      clearMetricCardErrors();
      highlightErrorSections(new Map());
      if (!isDirty) showErrors([]);
      if (saveBtn) saveBtn.disabled = false;
      updateSectionCompletionStates({ payload, mode, fieldMap: new Map(), snapshot });
      return;
    }
    const map = fieldErrorMap(payload, { mode });
    applyFieldErrors(payload, map, { mode });
    const errors = validate(payload, { mode });
    const inferredMap = inferFieldMapFromMessages(errors, payload);
    const finalMap = mergeFieldMaps(map, inferredMap);
    const finalErrors = errors.length ? errors : [...new Set(Array.from(finalMap.values()).filter(Boolean))];
    showErrors(finalErrors, finalMap, { mode });
    if (saveBtn) saveBtn.disabled = false;
    updateSectionCompletionStates({ payload, mode, fieldMap: finalMap, snapshot });
  }

  function getCell(item, key) {
    const startYear = fromDateToYear(item.planningHorizonStart);
    const endYear = fromDateToYear(item.planningHorizonEnd);
    const getStatusBadgeClass = (value) => {
      const normalized = String(value || "").trim().toLowerCase();
      if (["active", "published", "approved"].includes(normalized)) return "bg-label-success";
      if (["draft"].includes(normalized)) return "bg-label-warning";
      if (["in review", "pending", "pending approval", "proposed"].includes(normalized)) return "bg-label-warning";
      if (["archived", "retired", "inactive", "cancelled", "canceled"].includes(normalized)) return "bg-label-secondary";
      return "bg-label-info";
    };
    const getPriorityBadgeClass = (value) => {
      const normalized = String(value || "").trim().toLowerCase();
      if (["critical"].includes(normalized)) return "bg-label-danger";
      if (["high"].includes(normalized)) return "bg-label-warning";
      if (["medium"].includes(normalized)) return "bg-label-info";
      if (["low"].includes(normalized)) return "bg-label-secondary";
      return "bg-label-primary";
    };
    if (key === "id") return item.id || "";
    if (key === "name") return item.name || "";
    if (key === "category") return item.category || "-";
    if (key === "owner") {
      const role = String(item.ownerRole || item.ownerId || item.owner || "").trim();
      const company = ownerCompanyLabelByValue(item.ownerCompanyId || item.primaryCompanyId || "");
      const person = resolveUserName(item.ownerPersonId || item.ownerDisplayName || "");
      if (role && company) return `${escapeHtml(role)} — ${escapeHtml(company)}`;
      return escapeHtml(person || role || company || "-");
    }
    if (key === "status") return `<span class="badge ${getStatusBadgeClass(item.status)}">${escapeHtml(item.status || "-")}</span>`;
    if (key === "priority") return `<span class="badge ${getPriorityBadgeClass(item.priority)}">${escapeHtml(item.priority || "-")}</span>`;
    if (key === "scopeMode") return toUiScopeMode(item.scopeMode || "Enterprise");
    if (key === "primaryCompanyId") return companyLabelById(item.primaryCompanyId) || "-";
    if (key === "applicableCompanyIds") return (item.applicableCompanyIds || []).map(companyLabelById).join(", ") || "-";
    if (key === "startYear") return startYear || "-";
    if (key === "endYear") return endYear || "-";
    if (key === "metricCount") return String((item.metrics || []).length);
    if (key === "entityScope") return item.entityScope || "-";
    if (key === "changeLogRef") return item.changeLogRef || "-";
    if (key === "version") return String(item.version ?? 0);
    if (key === "actions") return window.enterpriseRowActionsMenu?.render?.(item.id, [
      { action: "view", label: "View", href: `/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(item.id)}` },
      { action: "edit", label: "Edit" },
      { action: "duplicate", label: "Duplicate" },
      { action: "archive", label: "Archive / Delete" },
      { divider: true },
      { action: "governance", label: "View governance" },
      { action: "exportRow", label: "Export row" }
    ]) || "";
    return "";
  }

  function getSortValue(item, key) {
    if (key === "startYear") return Number(fromDateToYear(item.planningHorizonStart) || 0);
    if (key === "endYear") return Number(fromDateToYear(item.planningHorizonEnd) || 0);
    if (key === "metricCount") return (item.metrics || []).length;
    if (key === "applicableCompanyIds") return (item.applicableCompanyIds || []).join(", ");
    return item[key] ?? "";
  }

  function getExportValue(item, key) {
    if (key === "startYear") return fromDateToYear(item.planningHorizonStart) || "";
    if (key === "endYear") return fromDateToYear(item.planningHorizonEnd) || "";
    if (key === "metricCount") return (item.metrics || []).length;
    if (key === "status") return item.status || "";
    return item[key] ?? "";
  }

  function render(items) {
    if (!tableBody) return;
    tableBody.innerHTML = "";
    const cols = tableControls?.getVisibleColumns?.() || fallbackColumns;
    const hdr = headerRow || document.querySelector("#goals-table thead tr");
    if (hdr) {
      hdr.innerHTML =
        `<th class="cell-fit text-center goal-select-col" style="width:42px;"><input type="checkbox" id="goal-select-all" aria-label="Select all visible rows" ${items.length && items.every((item) => selectedGoalIds.has(String(item.id || ""))) ? "checked" : ""} /></th>` +
        cols.map((c) => {
          if (c.key === "actions") return `<th data-col-key="${c.key}" class="cell-fit text-end es-row-actions-col"><span class="es-table-head-label">${c.label}</span></th>`;
          return `<th data-col-key="${c.key}"><span class="es-col-drag-handle me-1" title="Drag to reorder">⋮⋮</span><button type="button" class="btn btn-link btn-sm p-0 text-decoration-none es-table-head-label goal-sort" data-key="${c.key}">${c.label}${tableControls?.sortIndicator?.(c.key) || ""}</button></th>`;
        }).join("");
    }

    items.forEach((item) => {
      const tr = document.createElement("tr");
      const itemId = String(item.id || "");
      tr.innerHTML =
        `<td class="cell-fit text-center goal-select-col"><input type="checkbox" class="goal-row-select" data-id="${itemId}" aria-label="Select goal ${itemId}" ${selectedGoalIds.has(itemId) ? "checked" : ""} /></td>` +
        cols.map((c) => `<td class="${c.key === "actions" ? "text-end es-row-actions-col" : ""}">${getCell(item, c.key)}</td>`).join("");
      tr.querySelector(".goal-row-select")?.addEventListener("change", (event) => {
        if (event.target.checked) selectedGoalIds.add(itemId);
        else selectedGoalIds.delete(itemId);
        updateBulkActionsState();
        const selectAll = document.getElementById("goal-select-all");
        if (selectAll) {
          selectAll.checked = items.length > 0 && items.every((row) => selectedGoalIds.has(String(row.id || "")));
        }
      });
      tr.querySelectorAll(".es-row-action-item").forEach((el) => {
        el.addEventListener("click", async (event) => {
          const action = String(el.dataset.action || "");
          if (!action || action === "view") return;
          event.preventDefault();
          if (action === "edit") {
            navigateToGoalWorkspace("edit", item.id);
            return;
          }
          if (action === "duplicate") {
            navigateToGoalWorkspace("duplicate", item.id);
            return;
          }
          if (action === "archive") {
            try {
              await window.strategyGoalsApi.archive(item.id, item.version || 0);
              await load();
            } catch (err) {
              errorEl.textContent = window.enterpriseStrategyUi.getErrorMessage(err, "Archive failed");
            }
            return;
          }
          if (action === "governance") {
            window.location.assign(`/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(item.id)}`);
            return;
          }
          if (action === "exportRow") {
            window.enterpriseWorkbookIo?.exportCsv?.("goal_row.csv", toGoalSheetRows([item]));
          }
        });
      });
      tableBody.appendChild(tr);
    });
    if (!items.length) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="${cols.length + 1}" class="text-center text-muted py-3">No goals found for the current filters.</td>`;
      tableBody.appendChild(tr);
    }
    document.getElementById("goal-select-all")?.addEventListener("change", (event) => {
      if (event.target.checked) items.forEach((item) => selectedGoalIds.add(String(item.id || "")));
      else items.forEach((item) => selectedGoalIds.delete(String(item.id || "")));
      updateBulkActionsState();
      renderFiltered(false);
    });
    (hdr || document).querySelectorAll(".goal-sort").forEach((btn) => btn.addEventListener("click", () => tableControls?.cycleSort?.(btn.dataset.key)));
    window.enterpriseTablePageUtils?.bindHeaderColumnDrag?.(hdr, {
      onReorder: (fromKey, toKey) => tableControls?.moveColumnTo?.(fromKey, toKey)
    });
  }

  function destroyGoalsDataTable() {
    if (!goalsDt) return;
    try {
      const pageSize = Number(goalsDt.page?.len?.());
      if (Number.isFinite(pageSize) && pageSize > 0) {
        tableControls?.setPageSize?.(pageSize);
      }
      goalsDt.destroy();
    } catch (_) { }
    goalsDt = null;
  }

  function initGoalsDataTable() {
    if (!tableBody || typeof DataTable === "undefined") return;
    destroyGoalsDataTable();
    goalsDt = new DataTable("#goals-table", {
      paging: true,
      searching: true,
      info: true,
      ordering: false,
      responsive: true,
      autoWidth: false,
      pageLength: tableControls?.getPageSize?.() || 25,
      lengthMenu: [10, 25, 50, 100],
      layout: {
        topStart: {
          rowClass: "row m-3 justify-content-between",
          features: [
            {
              pageLength: {
                menu: [10, 25, 50, 100],
                text: "_MENU_"
              }
            }
          ]
        },
        topEnd: {
          rowClass: "row mx-3 justify-content-between",
          features: [
            {
              search: {
                placeholder: "Search goals",
                text: "_INPUT_"
              }
            },
            {
              buttons: [
                {
                  extend: "collection",
                  className: "btn btn-label-secondary dropdown-toggle",
                  text: '<i class="icon-base bx bx-export icon-sm me-2"></i>Export',
                  buttons: [
                    { text: "Export selected CSV", action: () => exportSelectedCsv() },
                    { text: "Export selected Excel", action: () => exportSelectedXlsx() },
                    { text: "Export selected workbook", action: () => exportSelectedWorkbook() }
                  ]
                },
                {
                  extend: "collection",
                  attr: { id: "goal-bulk-actions-toggle" },
                  className: "btn btn-sm btn-label-secondary dropdown-toggle",
                  text: '<i class="icon-base bx bx-check-square icon-sm me-2"></i>Bulk Actions',
                  buttons: [
                    { text: "Clear selection", action: () => clearSelection() },
                    { text: "Archive selected", action: () => archiveSelectedGoals() }
                  ]
                },
                {
                  extend: "collection",
                  className: "btn btn-sm btn-icon btn-label-secondary dropdown-toggle",
                  text: '<i class="icon-base bx bx-layout icon-sm"></i>',
                  buttons: [
                    { text: "Default density", action: () => setTableDensity("default") },
                    { text: "Compact density", action: () => setTableDensity("compact") }
                  ]
                },
                {
                  attr: { id: "goal-columns-trigger", "aria-label": "Columns" },
                  className: "btn btn-icon btn-label-secondary dt-eye-btn",
                  text: '<i class="icon-base bx bx-show icon-sm"></i>',
                  action: function (e) {
                    e?.preventDefault?.();
                    e?.stopPropagation?.();
                    const proxyBtn = document.getElementById("goal-columns-btn");
                    const visibleBtn = this.node?.();
                    if (!proxyBtn || !visibleBtn) return;
                    const rect = visibleBtn.getBoundingClientRect();
                    proxyBtn.style.left = `${rect.left}px`;
                    proxyBtn.style.top = `${rect.top}px`;
                    proxyBtn.style.width = `${Math.max(rect.width, 1)}px`;
                    proxyBtn.style.height = `${Math.max(rect.height, 1)}px`;
                    proxyBtn.click();
                  }
                },
                {
                  attr: { id: "goal-open-filters", "aria-label": "Filters" },
                  className: "btn btn-icon btn-label-secondary dt-filter-btn",
                  text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                  action: function () {
                    const filterEl = document.getElementById("goalFilterCollapse");
                    if (!filterEl || !window.bootstrap?.Collapse) return;
                    const bsCollapse = window.bootstrap.Collapse.getOrCreateInstance(filterEl);
                    bsCollapse.toggle();
                    this.node()?.classList.toggle("active");
                  }
                },
                {
                  className: "btn btn-sm btn-label-primary",
                  attr: { id: "goal-create-stepper" },
                  text: "Create Wizard",
                  action: () => window.location.assign("/management-governance/enterprise-strategy-business-performance/goals/new-stepper")
                },
                {
                  className: "btn btn-sm btn-primary esbp-create-btn",
                  attr: { id: "goal-create" },
                  text: "Create",
                  action: () => window.location.assign(goalCreateUrl)
                }
              ]
            }
          ]
        },
        bottomStart: "info",
        bottomEnd: "paging"
      }
    });
    const wrapper = document.querySelector("#goals-table_wrapper");
    const searchInput = wrapper?.querySelector(".dt-search input");
    if (searchInput) {
      searchInput.value = goalSearchValue;
      searchInput.addEventListener("input", (event) => {
        goalSearchValue = String(event.target?.value || "");
      });
      if (goalSearchValue) {
        goalsDt.search(goalSearchValue).draw();
      }
    }
    const lengthSelect = wrapper?.querySelector(".dt-length select");
    lengthSelect?.addEventListener("change", (event) => {
      const value = Number(event.target?.value || goalsDt?.page?.len?.());
      if (Number.isFinite(value) && value > 0) tableControls?.setPageSize?.(value);
    });
    updateBulkActionsState();
    fixGoalsDataTableLayout();
  }

  function fixGoalsDataTableLayout() {
    setTimeout(() => {
      const wrapper = document.getElementById("goals-table_wrapper");
      if (!wrapper) return;

      const elementsToModify = [
        { selector: ".dt-buttons .btn", classToRemove: "btn-secondary" },
        { selector: ".dt-search .form-control", classToRemove: "form-control-sm" },
        { selector: ".dt-length .form-select", classToRemove: "form-select-sm", classToAdd: "ms-0" },
        { selector: ".dt-length", classToAdd: "mb-md-6 mb-0" },
        { selector: ".dt-search", classToAdd: "mb-md-6 mb-2" },
        {
          selector: ".dt-layout-end",
          classToRemove: "justify-content-between",
          classToAdd: "d-flex gap-md-2 justify-content-md-end justify-content-center gap-2 flex-wrap mt-0"
        },
        { selector: ".dt-layout-start", classToAdd: "mt-0" },
        { selector: ".dt-buttons", classToAdd: "d-flex gap-2 mb-md-0 mb-6" },
        { selector: ".dt-layout-table", classToRemove: "row mt-2" },
        { selector: ".dt-layout-full", classToRemove: "col-md col-12", classToAdd: "table-responsive" }
      ];

      elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
        wrapper.querySelectorAll(selector).forEach((element) => {
          if (classToRemove) {
            classToRemove.split(" ").forEach((className) => element.classList.remove(className));
          }
          if (classToAdd) {
            classToAdd.split(" ").forEach((className) => element.classList.add(className));
          }
        });
      });

      const mountFilterPanel = () => {
        const host = document.getElementById("goalFilterCollapse");
        const filterBtn = wrapper.querySelector(".dt-filter-btn");
        if (!host || !filterBtn) return;

        const toolbarRow =
          filterBtn.closest(".dt-layout-row") ||
          filterBtn.closest(".row") ||
          filterBtn.closest(".dt-layout-end")?.parentElement;

        if (toolbarRow && host.previousElementSibling !== toolbarRow) {
          toolbarRow.insertAdjacentElement("afterend", host);
          host.classList.add("px-3");
        }
      };

      mountFilterPanel();

      const dtButtons = wrapper.querySelector(".dt-buttons");
      if (dtButtons) {
        const eyeBtn = dtButtons.querySelector(".dt-eye-btn");
        const filterBtn = dtButtons.querySelector(".dt-filter-btn");
        if (eyeBtn && !eyeBtn.dataset.goalColumnsBound) {
          eyeBtn.dataset.goalColumnsBound = "true";
          eyeBtn.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            event.stopImmediatePropagation?.();

            const proxyBtn = document.getElementById("goal-columns-btn");
            if (!proxyBtn) return;

            const rect = eyeBtn.getBoundingClientRect();
            proxyBtn.style.left = `${rect.left}px`;
            proxyBtn.style.top = `${rect.top}px`;
            proxyBtn.style.width = `${Math.max(rect.width, 1)}px`;
            proxyBtn.style.height = `${Math.max(rect.height, 1)}px`;
            proxyBtn.click();
          }, true);
        }
        if (eyeBtn && filterBtn && !eyeBtn.parentElement.classList.contains("btn-group")) {
          const group = document.createElement("div");
          group.className = "btn-group";
          eyeBtn.parentNode.insertBefore(group, eyeBtn);
          group.appendChild(eyeBtn);
          group.appendChild(filterBtn);

          [eyeBtn, filterBtn].forEach((btn) => {
            btn.classList.remove("ms-2", "mx-1", "mx-2", "mx-3", "mx-4", "ms-3");
            btn.style.margin = "0";
          });
        }
      }
    }, 100);
  }

  async function load() {
    await workbook.ensureLookupsLoaded?.();
    await workbook.ensureUsersLoaded?.();
    await workbook.ensureCompaniesLoaded?.();
    await workbook.ensurePositionsLoaded?.();
    metricCatalogByName = new Map();
    try {
      const kpis = await window.strategyKpisApi?.list?.();
      const items = kpis?.items || [];
      items.forEach((kpi) => {
        const name = String(kpi?.name || "").trim().toLowerCase();
        if (!name) return;
        metricCatalogByName.set(name, {
          id: String(kpi?.id || ""),
          unitOfMeasure: String(kpi?.unitOfMeasure || ""),
          aggregationMethod: String(kpi?.aggregationMethod || ""),
          thresholdModel: String(kpi?.thresholdModel || ""),
          reportingFrequency: String(kpi?.reportingFrequency || ""),
          polarity: String(kpi?.polarity || "")
        });
      });
    } catch (_) { }
    const ownerRoleEl = getEl("goal-owner-role");
    const ownerEl = document.getElementById("goal-owner");
    if (ownerRoleEl) fillOwnerRoleSelect();
    else if (ownerEl) workbook.fillUserSelect?.(ownerEl, { placeholder: "Select owner" });

    let items = [];
    if (tableBody) {
      try {
        const data = await window.strategyGoalsApi.list();
        items = (data?.items || []).map((row) => normalizeGoalRow(row)).filter((row) => row?.id);
      } catch (_) {
        items = [];
      }
    }

    const statusOptions = nonEmpty(
      uniq(["Draft"].concat(workbook.lifecycleStatus || []).concat(items.map((x) => x.status))),
      ["Draft", "Active", "On Hold", "Archived"]
    );
    const categoryOptions = nonEmpty(workbook.goalObjectiveTypes, uniq(items.map((x) => x.category)));
    const strategicThemeOptions = nonEmpty(
      workbook.strategicThemes,
      uniq(items.map((x) => x.strategicThemeId || x.strategicTheme || x.category))
    );
    const priorityOptions = nonEmpty(workbook.priorities, uniq(items.map((x) => x.priority)));
    cachedItems = tableBody ? items : [];

    if (filters.category) workbook.fillSelect?.(filters.category, categoryOptions, { placeholder: "Category" });
    if (filters.owner) workbook.fillUserSelect?.(filters.owner, { placeholder: "Owner" });
    if (filters.status) workbook.fillSelect?.(filters.status, statusOptions, { placeholder: "Status" });
    if (filters.priority) workbook.fillSelect?.(filters.priority, priorityOptions, { placeholder: "Priority" });
    if (filters.scopeMode) {
      workbook.fillSelect?.(filters.scopeMode, [
        { value: "Enterprise", label: "Enterprise" },
        { value: "AppliesToSelectedCompanies", label: "AppliesToSelectedCompanies" }
      ], { placeholder: "Applicability Mode" });
    }
    workbook.fillDatalist?.(document.getElementById("goal-filter-company-list"), (workbook.companyOptions?.() || []).map((x) => x.label));

    const categoryEl = document.getElementById("goal-category");
    const strategicThemeEl = document.getElementById("goal-strategic-theme");
    const priorityEl = document.getElementById("goal-priority");
    const scopeEl = document.getElementById("goal-entity-scope");
    workbook.fillSelect?.(categoryEl, categoryOptions, { placeholder: "Select goal type" });
    workbook.fillSelect?.(strategicThemeEl, strategicThemeOptions, { placeholder: "Select strategic theme" });
    workbook.fillSelect?.(document.getElementById("goal-status"), statusOptions, { placeholder: "Select status", defaultValue: "Draft" });
    workbook.fillSelect?.(priorityEl, priorityOptions, { placeholder: "Select priority", defaultValue: "Medium" });
    if (getEl("goal-status-readonly")) getEl("goal-status-readonly").value = String(getEl("goal-status")?.value || "Draft").trim() || "Draft";
    workbook.fillSelect?.(document.getElementById("goal-scope-mode"), [
      { value: "Enterprise", label: "Enterprise" },
      { value: "AppliesToSelectedCompanies", label: "Selected Companies" }
    ], { placeholder: "Select applicability mode", defaultValue: "Enterprise" });
    const fillCompanySelect = (id) => {
      const el = document.getElementById(id);
      if (!el) return;
      const current = el.multiple ? Array.from(el.selectedOptions || []).map((o) => o.value) : [el.value];
      el.innerHTML = el.multiple ? "" : '<option value=""></option>';
      (workbook.companyOptions?.() || []).forEach((c) => {
        const opt = document.createElement("option");
        opt.value = c.value || "";
        opt.textContent = c.label || "";
        if (current.includes(opt.value)) opt.selected = true;
        el.appendChild(opt);
      });
      if (id === "goal-applicable-companies") syncApplicableCompaniesPickerFromSelect();
    };
    fillCompanySelect("goal-primary-company");
    fillCompanySelect("goal-owner-company");
    fillCompanySelect("goal-applicable-companies");
    initApplicableCompaniesPicker();
    fillOwnerPersonSelect({ keepCurrent: true });
    syncOwnerAccountableDisplay();
    if (scopeEl && scopeEl.tagName === "INPUT") scopeEl.setAttribute("list", "goal-entity-scope-list");
    const scopeList = document.getElementById("goal-entity-scope-list") || (() => {
      const dl = document.createElement("datalist");
      dl.id = "goal-entity-scope-list";
      scopeEl?.insertAdjacentElement("afterend", dl);
      return dl;
    })();
    workbook.fillDatalist?.(scopeList, workbook.entityScopes || []);
    if (filters.scope) {
      filters.scope.setAttribute("list", "goal-filter-scope-list");
      const filterScopeList = document.getElementById("goal-filter-scope-list") || (() => {
        const dl = document.createElement("datalist");
        dl.id = "goal-filter-scope-list";
        filters.scope.insertAdjacentElement("afterend", dl);
        return dl;
      })();
      workbook.fillDatalist?.(filterScopeList, workbook.entityScopes || []);
    }
    initGoalSelect2();
    syncRelatedEntityScopeSummary();

    if (!tableBody) {
      const route = parseWorkspaceRouteContext();
      if (route.mode === "edit" && route.goalId) {
        try {
          const full = await fetchGoalForEdit({ id: route.goalId });
          openEditor(full || { id: route.goalId });
        } catch (err) {
          notify(window.enterpriseStrategyUi.getErrorMessage(err, "Failed to load goal for edit"), "error");
          openEditor(null);
        }
        return;
      }
      if (route.mode === "duplicate" && route.goalId) {
        try {
          const full = await fetchGoalForEdit({ id: route.goalId });
          const clone = structuredClone(full || {});
          clone.id = "";
          clone.name = `${String(full?.name || "").trim()} (Copy)`.trim();
          openEditor(clone);
        } catch (err) {
          notify(window.enterpriseStrategyUi.getErrorMessage(err, "Failed to load goal for duplicate"), "error");
          openEditor(null);
        }
        return;
      }
      openEditor(null);
      return;
    }

    const saved = tableControls?.getFilters?.() || {};
    Object.entries(saved).forEach(([k, v]) => {
      if (k === "search") {
        goalSearchValue = String(v || "");
        return;
      }
      if (filters[k]) filters[k].value = v;
    });
    syncFilterUi();
    syncMoreFiltersPanel();
    await refreshGoalStrategyPeriodLookup({ applyDefaults: false });
    initFilterSelect2();
    renderFiltered();
  }

  function renderFiltered(resetPage = true) {
    if (!tableBody) return;
    destroyGoalsDataTable();
    const yearRange = parseYearRange(filters.yearRange?.value);
    filteredItems = cachedItems.filter((x) => {
      const ownerId = resolveUserId(x.ownerId || x.owner);
      if (filters.category.value && x.category !== filters.category.value) return false;
      if (filters.owner.value && ownerId !== filters.owner.value) return false;
      if (filters.status.value && x.status !== filters.status.value) return false;
      if (filters.priority.value && x.priority !== filters.priority.value) return false;
      if (filters.scopeMode.value && toUiScopeMode(x.scopeMode) !== filters.scopeMode.value) return false;
      if (filters.company.value) {
        const companyNeedle = String(filters.company.value).toLowerCase();
        const companies = [x.primaryCompanyId].concat(x.applicableCompanyIds || [])
          .filter(Boolean)
          .flatMap((v) => [String(v).toLowerCase(), companyLabelById(v).toLowerCase()]);
        if (!companies.some((v) => v.includes(companyNeedle))) return false;
      }
      if (yearRange) {
        const rowStart = Number(fromDateToYear(x.planningHorizonStart) || 0);
        const rowEnd = Number(fromDateToYear(x.planningHorizonEnd) || 0);
        if (!rowStart || !rowEnd || rowEnd < yearRange.from || rowStart > yearRange.to) return false;
      }
      if (filters.scope.value && !String(x.entityScope || "").toLowerCase().includes(String(filters.scope.value).toLowerCase())) return false;
      return true;
    });
    tableControls?.setFilters?.({
      search: goalSearchValue,
      category: filters.category.value,
      owner: filters.owner.value,
      status: filters.status.value,
      priority: filters.priority.value,
      scopeMode: filters.scopeMode.value,
      company: filters.company.value,
      yearRange: filters.yearRange.value,
      scope: filters.scope.value
    });
    syncMoreFiltersPanel();
    renderActiveFilterChips(tableControls?.getFilters?.() || {});
    const sorted = tableControls?.sortRows?.(filteredItems, getSortValue) || filteredItems;
    render(sorted);
    initGoalsDataTable();
  }

  function toGoalSheetRows(items) {
    return (items || []).map((item) => {
      const startYear = fromDateToYear(item.planningHorizonStart);
      const endYear = fromDateToYear(item.planningHorizonEnd);
      const metric = (item.metrics || [])[0] || {};
      return {
        "Goal ID": item.id || "",
        "Goal": item.name || "",
        "Goal Metric": metric.metricName || "",
        "Goal Metric Type": metric.metricType || "",
        "Goal Owner": resolveUserName(item.ownerDisplayName || item.ownerId || item.owner),
        "Goal Owner ID": resolveUserId(item.ownerId || item.owner),
        "Goal Status": item.status || "",
        "Goal Category": item.category || "",
        "Scope Mode": toUiScopeMode(item.scopeMode || "Enterprise"),
        "Primary Company": item.primaryCompanyId || "",
        "Applicable Companies": (item.applicableCompanyIds || []).join(", "),
        "Priority": item.priority || "",
        "Start Year": startYear || "",
        "End Year": endYear || "",
        "Baseline Value": metric.baselineValue ?? "",
        "Target Value": metric.targetValue ?? "",
        "Unit of Measure": metric.unitOfMeasure || "",
        "Aggregation Method": metric.aggregationMethod || "",
        "Entity Scope": item.entityScope || "",
        "Decision Ref": item.decisionReference || "",
        "Evidence Ref": item.evidenceReference || "",
        "Version": item.version ?? 0
      };
    });
  }

  async function buildWorkbookSheets() {
    const [goals, objectives, initiatives, projects, connections] = await Promise.all([
      window.strategyGoalsApi.list(),
      window.strategyObjectivesApi.list(),
      window.initiativeStrategyApi.list(),
      window.projectStrategyApi.list(),
      window.strategyConnectionsApi.list()
    ]);
    const goalRows = toGoalSheetRows(goals?.items || []);
    const objectiveRows = (objectives?.items || []).map((x) => ({
      "Objective ID": x.id || "",
      "Objective": x.name || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Owner": resolveUserName(x.owner),
      "Status": x.status || "",
      "Type": x.type || "",
      "Priority": x.priority || "",
      "Contribution Type": x.contributionType || "",
      "Contribution Weight": x.contributionWeight ?? "",
      "Start Year": fromDateToYear(x.timeHorizonStart),
      "End Year": fromDateToYear(x.timeHorizonEnd),
      "Decision Ref": x.decisionReference || "",
      "Evidence Ref": x.evidenceReference || "",
      "Version": x.version ?? 0
    }));
    const initiativeRows = (initiatives?.items || []).map((x) => ({
      "Initiative ID": x.initiativeId || "",
      "Parent Objective ID": x.parentObjectiveId || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Initiative": x.initiativeName || "",
      "Owner": resolveUserName(x.owner),
      "Status": x.status || "",
      "Type": x.type || "",
      "Start Date": x.startDate || "",
      "End Date": x.endDate || "",
      "Planning Wave / Phase": x.waveOrPhase || "",
      "Primary KPI / Success Measure": x.primaryKpi || "",
      "Version": x.version ?? 0
    }));
    const projectRows = (projects?.items || []).map((x) => ({
      "Project ID": x.projectId || "",
      "Parent Initiative ID": x.parentInitiativeId || "",
      "Parent Objective ID": x.parentObjectiveId || "",
      "Parent Goal ID": x.parentGoalId || "",
      "Project": x.projectName || "",
      "Project Owner / PM": resolveUserName(x.ownerPm),
      "Project Status": x.status || "",
      "Stage / Phase": x.phase || "",
      "Start Date": x.startDate || "",
      "End Date": x.endDate || "",
      "Project Success Metric": x.successMetric || "",
      "Risk Rating": x.riskRating || "",
      "Version": x.version ?? 0
    }));
    const connectionRows = (connections?.items || []).map((x) => {
      const meta = JSON.parse(x.metricBindingsJson || "{}");
      const out = {
        "Goal ID": meta.goalId || "",
        "Goal": meta.goal || "",
        "Goal Metric": meta.goalMetric || "",
        "Objective": meta.objective || "",
        "Objective Metric": meta.objectiveMetric || "",
        "Initiative ID": meta.initiativeId || "",
        "Initiative": meta.initiative || "",
        "Initiative Metric": meta.initiativeMetric || "",
        "Project ID": meta.projectId || "",
        "Project": meta.project || "",
        "Project Metric": meta.projectMetric || "",
        "Metric Owner": resolveUserName(meta.metricOwner),
        "Aggregation Method": meta.aggregationMethod || "",
        "Baseline Year": meta.baselineYear || "",
        "Baseline Value": meta.baselineValue ?? "",
        "Target Year": meta.targetYear || "",
        "Target Value": meta.targetValue ?? "",
        "Entry Notes": ""
      };
      for (let y = 2027; y <= 2046; y++) out[String(y)] = meta[String(y)] ?? "";
      return out;
    });
    return {
      Goals_List: goalRows,
      Objectives_List: objectiveRows,
      Initiatives_List: initiativeRows,
      Projects_List: projectRows,
      Connection_Map: connectionRows
    };
  }

  async function importGoalRows(rows) {
    const existing = await window.strategyGoalsApi.list();
    const byId = new Map((existing?.items || []).map((x) => [x.id, x]));
    let created = 0;
    let updated = 0;
    let invalid = 0;
    for (const r of rows) {
      const id = String(r["Goal ID"] || "").trim();
      const name = String(r["Goal"] || "").trim();
      if (!name) {
        invalid++;
        continue;
      }
      const payload = {
        id,
        name,
        category: String(r["Goal Category"] || ""),
        ownerId: String(r["Goal Owner ID"] || r["Goal Owner"] || ""),
        owner: String(r["Goal Owner"] || ""),
        statement: String(r["Goal Statement"] || name),
        status: String(r["Goal Status"] || "Active"),
        scopeMode: String(r["Scope Mode"] || "Enterprise"),
        primaryCompanyId: String(r["Primary Company"] || "").trim() || null,
        applicableCompanyIds: String(r["Applicable Companies"] || "").split(",").map((x) => x.trim()).filter(Boolean),
        priority: String(r["Priority"] || "Medium"),
        planningHorizonStart: yearRegex.test(String(r["Start Year"] || "")) ? `${r["Start Year"]}-01-01` : null,
        planningHorizonEnd: yearRegex.test(String(r["End Year"] || "")) ? `${r["End Year"]}-12-31` : null,
        entityScope: String(r["Entity Scope"] || ""),
        changeLogRef: "",
        decisionReference: String(r["Decision Ref"] || "") || null,
        evidenceReference: String(r["Evidence Ref"] || "") || null,
        metrics: [{
          id: "gm-1",
          metricName: String(r["Goal Metric"] || ""),
          metricDefId: String(r["Goal Metric ID"] || ""),
          metricType: String(r["Goal Metric Type"] || ""),
          baselineValue: Number(r["Baseline Value"] || 0),
          targetValue: Number(r["Target Value"] || 0),
          unitOfMeasure: String(r["Unit of Measure"] || ""),
          aggregationMethod: String(r["Aggregation Method"] || ""),
          polarityCode: String(r["Direction / Polarity"] || workbook.directionOfPerformance?.[0] || "Increase"),
          thresholdModelCode: String(r["Threshold Model"] || workbook.thresholdModels?.[0] || "None"),
          reportingFrequencyCode: String(r["Reporting Frequency"] || workbook.reportingFrequencies?.[2] || "Quarterly"),
          cascadeMetric: true,
          metricOrigin: "Local",
          metricRole: "Strategic",
          restrictionMode: "GoalGovernedStructure",
          rollupEligible: true,
          yearlyValues: (() => {
            const sy = parseYear(r["Start Year"]);
            const ey = parseYear(r["End Year"]);
            if (!sy || !ey || ey < sy) return [];
            const rows = [];
            for (let y = sy; y <= ey; y++) {
              rows.push({
                year: y,
                targetValue: y === sy ? Number(r["Baseline Value"] || 0) : Number(r["Target Value"] || 0),
                actualValue: null,
                forecastValue: null,
                thresholdMin: null,
                thresholdMax: null,
                commentary: null,
                thresholdCommentary: null
              });
            }
            return rows;
          })(),
          metricBindingStatus: "Bound"
        }],
        version: Number(r["Version"] || 0)
      };
      const current = id ? byId.get(id) : null;
      try {
        if (current) {
          if (!id) {
            invalid++;
            continue;
          }
          await window.strategyGoalsApi.update(id, payload, current.version || 0);
          updated++;
        } else {
          await window.strategyGoalsApi.create({
            goal: payload.name,
            categoryCode: payload.category,
            ownerId: payload.ownerId || payload.owner,
            statusCode: payload.status,
            priorityCode: payload.priority,
            goalStatement: payload.statement,
            planning: {
              startYear: parseYear(r["Start Year"]),
              endYear: parseYear(r["End Year"]),
              relatedEntityScope: payload.entityScope,
              changeLogRef: payload.changeLogRef
            },
            companyScope: {
              scopeModeCode: payload.scopeMode,
              primaryCompanyId: payload.primaryCompanyId,
              applicableCompanyIds: payload.applicableCompanyIds
            },
            metrics: payload.metrics.map((m, i) => ({
              metricName: m.metricName,
              metricDefId: m.metricDefId || null,
              metricTypeCode: m.metricType,
              baselineValue: m.baselineValue,
              targetValue: m.targetValue,
              unitOfMeasureCode: m.unitOfMeasure,
              aggregationMethodCode: m.aggregationMethod,
              polarityCode: m.polarityCode || "Increase",
              thresholdModelCode: m.thresholdModelCode || "None",
              reportingFrequencyCode: m.reportingFrequencyCode || "Quarterly",
              cascadeMetric: m.cascadeMetric !== false,
              metricOrigin: m.metricOrigin || "Local",
              metricRole: m.metricRole || "Strategic",
              restrictionMode: m.restrictionMode || "GoalGovernedStructure",
              rollupEligible: m.rollupEligible !== false,
              yearlyValues: m.yearlyValues || [],
              sortOrder: i + 1
            })),
            governance: {
              decisionReference: payload.decisionReference,
              evidenceLink: payload.evidenceReference
            }
          });
          created++;
        }
      } catch {
        invalid++;
      }
    }
    return { created, updated, invalid };
  }

  function exportGoalsCsvFallback(rows) {
    const headers = Object.keys(rows[0] || { "Goal ID": "", Goal: "" });
    const lines = [headers.join(",")].concat(rows.map((r) => headers.map((h) => `"${String(r[h] ?? "").replace(/"/g, '""')}"`).join(",")));
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "goals_list.csv";
    a.click();
    URL.revokeObjectURL(url);
  }

  async function parseCsvFallback(file) {
    const text = await file.text();
    const lines = text.split(/\r?\n/).filter(Boolean);
    if (!lines.length) return [];
    const headers = lines[0].split(",").map((h) => h.trim().replace(/^"|"$/g, ""));
    return lines.slice(1).map((line) => {
      const vals = line.split(",");
      const row = {};
      headers.forEach((h, i) => { row[h] = String(vals[i] || "").trim().replace(/^"|"$/g, ""); });
      return row;
    });
  }

  addMetricBtn?.addEventListener("click", () => {
    if (!metricHost) return;
    const row = metricRow();
    metricHost.appendChild(row);
    refreshMetricYearRowsWithHorizonChange({ skipConfirm: true, skipMarkDirty: true });
    collapseOtherMetrics(row);
    activateMetricTab(row, "definition");
    markDirty();
  });

  fieldIds.forEach((id) => {
    const el = document.getElementById(id);
    el?.addEventListener("input", markDirty);
    el?.addEventListener("change", markDirty);
    el?.addEventListener("blur", () => {
      const payload = collectCreateRequest();
      const map = fieldErrorMap(payload, { mode: activeValidationMode });
      window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(id) || "");
    });
  });
  wizardStepButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      const step = Number(btn.dataset.step || 1);
      if (step <= currentWizardStep) setWizardStep(step);
    });
  });
  wizardBackBtn?.addEventListener("click", () => setWizardStep(currentWizardStep - 1));
  wizardNextBtn?.addEventListener("click", () => {
    if (!canAdvanceWizard(currentWizardStep)) return;
    setWizardStep(currentWizardStep + 1);
  });
  getEl("goal-scope-mode")?.addEventListener("change", () => {
    syncGoalCompanyScopeUi();
    void refreshGoalStrategyPeriodLookup({ applyDefaults: true });
    markDirty();
  });
  getOwnerRoleEl()?.addEventListener("change", () => {
    fillOwnerPersonSelect({ keepCurrent: true });
    syncOwnerAccountableDisplay();
    markDirty();
  });
  getOwnerCompanyEl()?.addEventListener("change", () => {
    const primary = getEl("goal-primary-company");
    const ownerCompanyId = String(getOwnerCompanyEl()?.value || "").trim();
    if (primary && ownerCompanyId) primary.value = ownerCompanyId;
    fillOwnerRoleSelect();
    fillOwnerPersonSelect({ keepCurrent: true });
    syncGoalCompanyScopeUi();
    void refreshGoalStrategyPeriodLookup({ applyDefaults: true });
    markDirty();
  });
  getOwnerPersonDisplayEl()?.addEventListener("change", () => {
    syncOwnerAccountableDisplay();
    markDirty();
  });
  getEl("goal-primary-company")?.addEventListener("change", () => {
    syncGoalCompanyScopeUi();
    void refreshGoalStrategyPeriodLookup({ applyDefaults: true });
    markDirty();
  });
  getEl("goal-applicable-companies")?.addEventListener("change", () => {
    syncGoalCompanyScopeUi();
    void refreshGoalStrategyPeriodLookup({ applyDefaults: true });
    markDirty();
  });
  getEl("goal-business-unit")?.addEventListener("input", () => {
    syncRelatedEntityScopeSummary();
    markDirty();
  });
  getEl("goal-region")?.addEventListener("input", () => {
    syncRelatedEntityScopeSummary();
    markDirty();
  });
  getEl("goal-strategy-period")?.addEventListener("change", () => {
    const priorPeriodId = previousStrategyPeriodIdRaw;
    const priorStart = previousStartYearRaw;
    const priorEnd = previousEndYearRaw;
    const priorBudget = collectYearlyBudgetsFromDom();
    syncSelectedGoalStrategyPeriod({ applyDefaults: true });
    const ok = refreshMetricYearRowsWithHorizonChange({ skipConfirm: false, skipMarkDirty: true });
    if (!ok) {
      if (getEl("goal-strategy-period")) getEl("goal-strategy-period").value = priorPeriodId;
      if (window.jQuery && window.jQuery(getEl("goal-strategy-period")).hasClass("select2-hidden-accessible")) {
        window.jQuery(getEl("goal-strategy-period")).trigger("change.select2");
      }
      syncSelectedGoalStrategyPeriod({ applyDefaults: false });
      setPlanningInputFromRaw("goal-planning-start-year", priorStart, false);
      setPlanningInputFromRaw("goal-planning-end-year", priorEnd, true);
      return;
    }
    const budgetOk = syncBudgetYearRowsWithHorizonChange({ skipConfirm: false, skipMarkDirty: true });
    if (!budgetOk) {
      if (getEl("goal-strategy-period")) getEl("goal-strategy-period").value = priorPeriodId;
      if (window.jQuery && window.jQuery(getEl("goal-strategy-period")).hasClass("select2-hidden-accessible")) {
        window.jQuery(getEl("goal-strategy-period")).trigger("change.select2");
      }
      syncSelectedGoalStrategyPeriod({ applyDefaults: false });
      setPlanningInputFromRaw("goal-planning-start-year", priorStart, false);
      setPlanningInputFromRaw("goal-planning-end-year", priorEnd, true);
      renderBudgetYearRows(priorBudget);
      return;
    }
    previousStrategyPeriodIdRaw = String(getEl("goal-strategy-period")?.value || "").trim();
    previousStartYearRaw = String(getEl("goal-planning-start-year")?.value || "").trim();
    previousEndYearRaw = String(getEl("goal-planning-end-year")?.value || "").trim();
    syncGoalHorizonUiState();
    markDirty();
  });
  filters.scopeMode?.addEventListener("change", () => {
    syncFilterUi();
    syncMoreFiltersPanel();
    applyFiltersAuto();
  });
  filters.category?.addEventListener("change", applyFiltersAuto);
  filters.owner?.addEventListener("change", applyFiltersAuto);
  filters.status?.addEventListener("change", applyFiltersAuto);
  filters.priority?.addEventListener("change", applyFiltersAuto);
  filters.company?.addEventListener("change", applyFiltersAuto);
  filters.yearRange?.addEventListener("change", applyFiltersAuto);
  filters.scope?.addEventListener("change", applyFiltersAuto);
  const debouncedApplyFiltersAuto = debounce(applyFiltersAuto, 220);
  filters.company?.addEventListener("input", debouncedApplyFiltersAuto);
  filters.yearRange?.addEventListener("input", debouncedApplyFiltersAuto);
  filters.scope?.addEventListener("input", debouncedApplyFiltersAuto);
  getEl("goal-creation-mode-select")?.addEventListener("change", () => {
    creationModeCode = getEl("goal-creation-mode-select")?.value || "Blank";
    if (creationModeCode !== "Template") {
      sourceTemplateId = "";
      sourceTemplateVersion = null;
      selectedSourceMeta = null;
    }
    updateSourceSummary();
    markDirty();
  });
  document.getElementById("goal-browse-source")?.addEventListener("click", async () => {
    creationModeCode = getEl("goal-creation-mode-select")?.value || "Blank";
    if (creationModeCode !== "Template") {
      notify("Choose From Goal Template first.", "warning");
      return;
    }
    const searchInput = document.getElementById("goal-source-picker-search");
    if (searchInput) searchInput.value = "";
    const typeInput = document.getElementById("goal-source-picker-type");
    if (typeInput) typeInput.value = "";
    const entityScopeInput = document.getElementById("goal-source-picker-entity-scope");
    if (entityScopeInput) entityScopeInput.value = "";
    await loadGoalSourcePickerCatalog();
    goalSourcePickerModal?.show();
  });
  document.getElementById("goal-clear-source")?.addEventListener("click", () => {
    sourceTemplateId = "";
    sourceTemplateVersion = null;
    selectedSourceMeta = null;
    updateSourceSummary();
    markDirty();
  });
  document.getElementById("goal-source-picker-search")?.addEventListener("input", applyGoalSourcePickerFilters);
  document.getElementById("goal-source-picker-type")?.addEventListener("change", applyGoalSourcePickerFilters);
  document.getElementById("goal-source-picker-entity-scope")?.addEventListener("change", applyGoalSourcePickerFilters);
  getEl("goal-applies-to-all-companies")?.addEventListener("change", () => {
    syncGoalCompanyScopeUi();
    markDirty();
  });
  getEl("goal-budget-enabled")?.addEventListener("change", () => {
    syncBudgetEnvelopeUi();
    applyValidation();
    updateSectionCompletionStates();
    markDirty();
  });
  getEl("goal-entity-scope")?.addEventListener("input", () => {
    syncRelatedEntityScopeSummary();
  });
  const onPlanningYearChanged = () => {
    const ok = syncGoalHorizonDrivenRows({ skipConfirm: false, markAsDirty: false });
    if (!ok) {
      setPlanningInputFromRaw("goal-planning-start-year", previousStartYearRaw, false);
      setPlanningInputFromRaw("goal-planning-end-year", previousEndYearRaw, true);
      return;
    }
    previousStartYearRaw = String(getEl("goal-planning-start-year")?.value || "").trim();
    previousEndYearRaw = String(getEl("goal-planning-end-year")?.value || "").trim();
    syncGoalHorizonUiState();
    markDirty();
  };
  getEl("goal-planning-start-year")?.addEventListener("change", onPlanningYearChanged);
  getEl("goal-planning-end-year")?.addEventListener("change", onPlanningYearChanged);

  modalEl?.addEventListener("hide.bs.modal", (event) => {
    if (!isDirty || allowModalClose) return;
    event.preventDefault();
    window.enterpriseStrategyUi?.confirm?.({
      title: "Discard changes?",
      message: "You have unsaved changes. Discard them?",
      confirmLabel: "Discard",
      confirmKind: "danger"
    }).then((confirmed) => {
      if (!confirmed) return;
      isDirty = false;
      hasSubmitAttempt = false;
      allowModalClose = true;
      modal?.hide();
      allowModalClose = false;
    });
  });
  modalEl?.addEventListener("shown.bs.modal", () => {
    initGoalSelect2();
  });

  function resolveSavedGoalIdentity(result, fallbackGoalId = "") {
    const data = result?.goal || result?.Goal || result?.data || result || {};
    const idCandidates = [
      data?.id, data?.goalId, data?.goalID,
      result?.id, result?.goalId, result?.goalID,
      fallbackGoalId
    ];
    const versionCandidates = [data?.version, result?.version];
    const id = idCandidates.map((x) => String(x || "").trim()).find(Boolean) || "";
    const version = versionCandidates
      .map((x) => Number(x))
      .find((x) => Number.isFinite(x) && x > 0) || null;
    return { id, version };
  }

  async function submitGoal(options = {}) {
    const forceDraft = Boolean(options.forceDraft);
    const mode = forceDraft ? "draft" : "publish";
    activeValidationMode = mode;
    const submitButton = forceDraft ? (saveDraftBtn || saveBtn) : saveBtn;
    const idleLabel = String(submitButton?.textContent || "").trim() || (forceDraft ? "Save Draft" : "Save");
    const statusEl = getEl("goal-status");
    if (forceDraft && statusEl) {
      ensureSelectOption(statusEl, "Draft", "Draft");
      statusEl.value = "Draft";
      if (window.jQuery && window.jQuery(statusEl).hasClass("select2-hidden-accessible")) {
        window.jQuery(statusEl).trigger("change.select2");
      }
    }
    const wasEditMode = isEditMode;
    const payload = collectCreateRequest();
    const submittedAsDraft = String(payload.statusCode || "").trim().toLowerCase() === "draft";
    const errors = validate(payload, { mode });
    const snapshot = computeValidationSnapshot(payload);
    let saveFailed = false;
    hasSubmitAttempt = true;
    const fieldMap = mergeFieldMaps(fieldErrorMap(payload, { mode }), inferFieldMapFromMessages(errors, payload));
    applyFieldErrors(payload, fieldMap, { mode });
    expandErrorSections(fieldMap);
    if (errors.length) {
      showErrors(errors, fieldMap, { mode });
      updateSectionCompletionStates({ payload, mode, fieldMap, snapshot });
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(modalEl || workspaceRoot || document);
      return;
    }
    delete payload._startYearRaw;
    delete payload._endYearRaw;
    try {
      window.enterpriseModalFormUtils?.setSubmitting?.(submitButton, true, idleLabel, "Saving...");
      let result = null;
      if (isEditMode) {
        const goalDto = createRequestToGoalDto(payload);
        result = await window.strategyGoalsApi.update(goalDto.id, goalDto, currentVersion || 0);
      } else {
        result = await window.strategyGoalsApi.create(payload);
      }
      isDirty = false;
      hasSubmitAttempt = false;
      const savedIdentity = resolveSavedGoalIdentity(result, String(getEl("goal-id")?.value || "").trim());
      if (savedIdentity.version) currentVersion = savedIdentity.version;
      if (savedIdentity.id) {
        isEditMode = true;
        if (getEl("goal-id")) getEl("goal-id").value = savedIdentity.id;
        if (!savedIdentity.version) {
          try {
            const persisted = await window.strategyGoalsApi.get(savedIdentity.id);
            const persistedVersion = Number(persisted?.version);
            if (Number.isFinite(persistedVersion) && persistedVersion > 0) currentVersion = persistedVersion;
          } catch (_) { }
        }
      }
      if (modal) {
        modal.hide();
        await load();
      } else if (isWorkspaceMode) {
        if (forceDraft || submittedAsDraft) {
          notify("Goal draft saved.", "success");
        } else {
          notify(wasEditMode ? "Goal updated and published." : "Goal created and published.", "success");
          if (savedIdentity.id) {
            window.location.assign(`/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(savedIdentity.id)}`);
            return;
          }
          window.location.assign(goalListUrl);
          return;
        }
      } else {
        await load();
      }
      applyValidation();
      updateSectionCompletionStates();
    } catch (err) {
      saveFailed = true;
      const rawBackendList = window.enterpriseModalFormUtils?.backendErrors?.(err, "Save failed") || [window.enterpriseStrategyUi.getErrorMessage(err, "Save failed")];
      const backendList = rawBackendList.map((msg) => enrichStrategyPeriodBoundaryMessage(msg, payload));
      window.enterpriseModalFormUtils?.applyBackendFieldErrors?.(err, {
        goal: getEl("goal-name"),
        name: getEl("goal-name"),
        category: getEl("goal-category"),
        categorycode: getEl("goal-category"),
        goaltypeid: getEl("goal-category"),
        strategictheme: getEl("goal-strategic-theme"),
        strategicthemeid: getEl("goal-strategic-theme"),
        ownerid: getOwnerRoleEl(),
        owner: getOwnerRoleEl(),
        ownerrole: getEl("goal-owner-role") || getOwnerRoleEl(),
        ownerpositionid: getEl("goal-owner-role") || getOwnerRoleEl(),
        ownercompanyid: getOwnerCompanyEl(),
        ownerorgid: getOwnerCompanyEl(),
        ownerpersonid: getOwnerPersonDisplayEl() || getEl("goal-owner-person"),
        currentownerpersonid: getOwnerPersonDisplayEl() || getEl("goal-owner-person"),
        goalstatement: getEl("goal-statement"),
        statement: getEl("goal-statement"),
        statuscode: getEl("goal-status"),
        prioritycode: getEl("goal-priority"),
        strategyperiodid: getEl("goal-strategy-period"),
        strategy_period_id: getEl("goal-strategy-period"),
        "planning.strategyperiodid": getEl("goal-strategy-period"),
        "planning.startdate": getEl("goal-planning-start-year"),
        "planning.enddate": getEl("goal-planning-end-year"),
        "planning.startyear": getEl("goal-planning-start-year"),
        "planning.endyear": getEl("goal-planning-end-year"),
        start_year: getEl("goal-planning-start-year"),
        end_year: getEl("goal-planning-end-year"),
        "metricassignments[0].yearlyvalues": getEl("goal-metrics-editor"),
        budgetyearlyvalues: getEl("goal-budget-year-table"),
        "companyscope.scopemodecode": getEl("goal-scope-mode"),
        "companyscope.primarycompanyid": getEl("goal-primary-company"),
        "companyscope.applicablecompanyids": getEl("goal-applicable-companies"),
        "companyscope.relatedentityscopesummary": getEl("goal-related-entity-scope-summary"),
        "governance.evidencelink": getEl("goal-evidence-reference"),
        version: getEl("goal-version")
      });
      const map = mergeFieldMaps(fieldErrorMap(collectCreateRequest(), { mode }), inferFieldMapFromMessages(backendList, payload));
      showErrors(backendList, map, { mode });
      expandErrorSections(map);
      updateSectionCompletionStates({ payload: collectCreateRequest(), mode, fieldMap: map, snapshot: computeValidationSnapshot(collectCreateRequest()) });
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(modalEl || workspaceRoot || document);
    } finally {
      window.enterpriseModalFormUtils?.setSubmitting?.(submitButton, false, idleLabel);
      if (!saveFailed) applyValidation();
    }
  }

  saveBtn?.addEventListener("click", async () => {
    await submitGoal({ forceDraft: false });
  });

  saveDraftBtn?.addEventListener("click", async () => {
    await submitGoal({ forceDraft: true });
  });

  validateBtn?.addEventListener("click", () => {
    hasSubmitAttempt = true;
    const payload = collectCreateRequest();
    activeValidationMode = normalizeValidationMode("auto", payload);
    const mode = activeValidationMode;
    const errors = validate(payload, { mode });
    const fieldMap = mergeFieldMaps(fieldErrorMap(payload, { mode }), inferFieldMapFromMessages(errors, payload));
    applyFieldErrors(payload, fieldMap, { mode });
    showErrors(errors, fieldMap, { mode });
    expandErrorSections(fieldMap);
    updateSectionCompletionStates({ payload, mode, fieldMap, snapshot: computeValidationSnapshot(payload) });
    if (!errors.length) notify("Validation passed.", "success");
  });

  workspaceCancelBtn?.addEventListener("click", async (event) => {
    if (!isWorkspaceMode || !isDirty) return;
    event.preventDefault();
    const confirmed = await window.enterpriseStrategyUi?.confirm?.({
      title: "Discard changes?",
      message: "You have unsaved changes. Discard and go back?",
      confirmLabel: "Discard",
      confirmKind: "danger"
    });
    if (!confirmed) return;
    isDirty = false;
    hasSubmitAttempt = false;
    window.location.assign(workspaceCancelBtn.getAttribute("href") || goalListUrl);
  });

  window.enterpriseModalFormUtils?.blockEnterSubmit?.(modalEl || workspaceRoot || document);

  budgetFillColumnBtn?.addEventListener("click", () => {
    const key = budgetHelperColumn?.value || "revenueTarget";
    const value = promptDecimalValue("Flat fill value:");
    if (value === null || value === undefined) return;
    fillBudgetColumn(key, value);
  });
  budgetInterpolateBtn?.addEventListener("click", () => interpolateBudgetColumn(budgetHelperColumn?.value || "revenueTarget"));
  budgetCopyDownBtn?.addEventListener("click", () => copyBudgetColumnDown(budgetHelperColumn?.value || "revenueTarget"));
  budgetClearColumnBtn?.addEventListener("click", () => clearBudgetColumn(budgetHelperColumn?.value || "revenueTarget"));

  importFileInput?.addEventListener("change", async () => {
    const file = importFileInput.files?.[0];
    if (!file) return;
    try {
      let rows = [];
      if (window.enterpriseWorkbookIo?.parseFile) {
        const parsed = await window.enterpriseWorkbookIo.parseFile(file);
        rows = parsed?.rows || [];
      } else if (file.name.toLowerCase().endsWith(".csv")) {
        rows = await parseCsvFallback(file);
        notify("CSV imported with fallback mode.", "warning");
      } else {
        notify("Import engine not loaded for Excel files. Please hard refresh and retry.", "error");
        return;
      }
      const result = await importGoalRows(rows);
      notify(`Goals import complete. Created ${result.created}, updated ${result.updated}, invalid ${result.invalid}.`);
      await load();
    } catch (err) {
      notify(window.enterpriseStrategyUi.getErrorMessage(err, "Import failed"), "error");
    } finally {
      importFileInput.value = "";
    }
  });

  importWorkbookInput?.addEventListener("change", async () => {
    const file = importWorkbookInput.files?.[0];
    if (!file) return;
    try {
      if (!window.enterpriseWorkbookIo?.parseFile) return notify("Workbook import engine not loaded. Please hard refresh and retry.", "error");
      const parsed = await window.enterpriseWorkbookIo.parseFile(file);
      const rows = parsed?.sheets?.Goals_List || [];
      const result = await importGoalRows(rows);
      notify(`Workbook goals import complete. Created ${result.created}, updated ${result.updated}, invalid ${result.invalid}.`);
      await load();
    } catch (err) {
      notify(window.enterpriseStrategyUi.getErrorMessage(err, "Workbook import failed"), "error");
    } finally {
      importWorkbookInput.value = "";
    }
  });
  importPageActionBtn?.addEventListener("click", () => importFileInput?.click());
  importWorkbookActionBtn?.addEventListener("click", () => importWorkbookInput?.click());
  filters.reset?.addEventListener("click", () => {
    suppressAutoFilterEvents = true;
    Object.keys(filters).forEach((key) => {
      const el = filters[key];
      if (!el || key === "apply" || key === "reset") return;
      el.value = "";
    });
    suppressAutoFilterEvents = false;
    syncFilterUi();
    renderFiltered(true);
  });
  filters.apply?.addEventListener("click", () => {
    renderFiltered(true);
    const collapseEl = document.getElementById("goalFilterCollapse");
    const collapse = collapseEl && window.bootstrap?.Collapse
      ? (window.bootstrap.Collapse.getInstance(collapseEl) || window.bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }))
      : null;
    collapse?.hide();
  });
  if (window.esbpHorizonDates?.initIn) {
    window.esbpHorizonDates.initIn(modalEl || document);
    modalEl?.addEventListener("shown.bs.modal", () => window.esbpHorizonDates.initIn(modalEl));
  }

  window.__goalYearlyPlanTestHooks = {
    parseDecimal,
    formatDecimalForInput,
    metricThresholdsRequired,
    syncMetricThresholdFields,
    renderMetricYearRows,
    collectYearlyTargetsFromRow,
    applyMetricFlatFill,
    copyMetricPreviousRows,
    interpolateMetricTargets,
    clearMetricYearRows,
    isBudgetEnvelopeEnabled,
    syncBudgetEnvelopeUi,
    renderBudgetYearRows,
    fillBudgetColumn,
    interpolateBudgetColumn,
    copyBudgetColumnDown,
    clearBudgetColumn,
    collectYearlyBudgetsFromDom,
    metricRow,
    getMetrics,
    collectCreateRequest,
    collectDraftRequiredMissing,
    collectPublishGovernanceMissing,
    collectKpiRowsMissingYearlyTargets,
    computeValidationSnapshot,
    renderValidationReadiness,
    validate,
    fieldErrorMap
  };

  load().catch(() => { });
})(window, document);
