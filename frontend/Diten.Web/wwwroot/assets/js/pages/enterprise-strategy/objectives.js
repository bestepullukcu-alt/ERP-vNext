(function (window, document) {
  "use strict";

  const workspaceRoot = document.getElementById("objective-create-workspace");
  const tableBody = document.querySelector("#objectives-table tbody");
  const headerRow = document.getElementById("objectives-header-row");
  const saveBtn = document.getElementById("objective-save");
  const errorEl = document.getElementById("objective-form-error");
  const modalTitle = document.getElementById("objective-modal-title");
  const modalSubtitle = document.getElementById("objective-modal-subtitle");
  const objectiveSourcePickerModalEl = document.getElementById("objectiveSourcePickerModal");
  const objectiveSourcePickerModal = objectiveSourcePickerModalEl && window.bootstrap?.Modal ? new window.bootstrap.Modal(objectiveSourcePickerModalEl) : null;
  const wizardStepButtons = Array.from(document.querySelectorAll("#objective-wizard-steps .objective-wizard-step-btn"));
  const wizardStepPanes = Array.from(document.querySelectorAll(".objective-wizard-step-pane"));
  const wizardBackBtn = document.getElementById("objective-step-back");
  const wizardNextBtn = document.getElementById("objective-step-next");
  const objectiveTargetPlanBody = document.getElementById("objective-target-plan-body");
  const objectiveTargetPlanEmptyEl = document.getElementById("objective-target-plan-empty");
  const objectiveTargetPlanContextEl = document.getElementById("objective-target-plan-context");
  const objectiveTargetPlanStatusChipEl = document.getElementById("objective-target-plan-status-chip");
  const objectiveTargetPlanStrategyPeriodEl = document.getElementById("objective-target-plan-strategy-period");
  const objectiveTargetPlanGranularityEl = document.getElementById("objective-target-plan-granularity");
  const objectiveTargetPlanGovernanceWarningEl = document.getElementById("objective-target-plan-governance-warning");
  const objectiveTargetPlanGovernanceWarningTextEl = document.getElementById("objective-target-plan-governance-warning-text");
  const objectiveParentGoalKpiContextFieldsEl = document.getElementById("objective-parent-goal-kpi-context-fields");
  const objectiveKpiAlignmentContextEl = document.getElementById("objective-kpi-alignment-context");
  const objectiveParentGoalTargetContextFieldsEl = document.getElementById("objective-parent-goal-target-context-fields");
  const objectiveTargetPlanComparisonEl = document.getElementById("objective-target-plan-comparison");
  const objectiveGoalTargetReferenceBodyEl = document.getElementById("objective-goal-target-reference-body");
  const objectiveReadinessIndicatorEl = document.getElementById("objective-readiness-indicator");
  const objectiveReadinessTextEl = document.getElementById("objective-readiness-text");
  const objectiveReadinessMissingEl = document.getElementById("objective-readiness-missing");
  const objectiveReadinessBlockersEl = document.getElementById("objective-readiness-blockers");
  const objectiveReadinessWarningsEl = document.getElementById("objective-readiness-warnings");
  const objectiveReadinessDraftChipEl = document.getElementById("objective-readiness-draft-chip");
  const objectiveReadinessPublishChipEl = document.getElementById("objective-readiness-publish-chip");
  const objectiveReadinessPlanChipEl = document.getElementById("objective-readiness-plan-chip");
  const objectiveReadinessTargetsChipEl = document.getElementById("objective-readiness-targets-chip");
  const totalWizardSteps = 4;
  const isWorkspaceMode = Boolean(workspaceRoot);
  const formRootEl = workspaceRoot || document;
  const workbook = window.enterpriseWorkbookOptions || {};
  const objectiveListUrl = "/management-governance/enterprise-strategy-business-performance/objectives";
  const objectiveCreateUrl = `${objectiveListUrl}/new`;
  const objectiveEditUrl = (objectiveId) => `${objectiveListUrl}/${encodeURIComponent(String(objectiveId || "").trim())}/edit`;
  const objectiveDuplicateUrl = (objectiveId) => `${objectiveCreateUrl}?duplicateFrom=${encodeURIComponent(String(objectiveId || "").trim())}`;
  const filterSummaryHost = document.getElementById("objective-active-filters");
  const bulkActionsToggle = document.getElementById("objective-bulk-actions-toggle");
  const bulkClearSelectionBtn = document.getElementById("objective-bulk-clear-selection");
  const bulkArchiveBtn = document.getElementById("objective-bulk-archive");
  const exportCsvBtn = document.getElementById("objective-export-csv");
  const exportXlsxBtn = document.getElementById("objective-export-xlsx");
  const exportWorkbookBtn = document.getElementById("objective-export-workbook");
  const densityDefaultBtn = document.getElementById("objective-density-default");
  const densityCompactBtn = document.getElementById("objective-density-compact");
  const filters = {
    search: document.getElementById("objective-search"),
    parent: document.getElementById("objective-filter-parent"),
    owner: document.getElementById("objective-filter-owner"),
    status: document.getElementById("objective-filter-status"),
    type: document.getElementById("objective-filter-type"),
    priority: document.getElementById("objective-filter-priority"),
    inheritCompanyScope: document.getElementById("objective-filter-inherit-company"),
    company: document.getElementById("objective-filter-company"),
    yearRange: document.getElementById("objective-filter-year-range"),
    scope: document.getElementById("objective-filter-scope"),
    apply: document.getElementById("objective-apply-filters")
  };
  const filterLabels = {
    search: "Search",
    parent: "Parent Goal",
    owner: "Owner",
    status: "Status",
    type: "Type",
    priority: "Priority",
    inheritCompanyScope: "Scope Mode",
    company: "Company",
    yearRange: "Year Range",
    scope: "Entity Scope"
  };
  const objectiveFieldIds = [
    "objective-id",
    "objective-parent-goal",
    "objective-name",
    "objective-type",
    "objective-statement",
    "objective-priority",
    "objective-status",
    "objective-strategic-theme",
    "objective-owner-company",
    "objective-owner-position",
    "objective-current-owner-person-display",
    "objective-planning-cycle",
    "objective-horizon-start-date",
    "objective-horizon-end-date",
    "objective-inherit-company-scope",
    "objective-primary-company",
    "objective-applicable-companies",
    "objective-business-unit",
    "objective-region",
    "objective-entity-scope-summary",
    "objective-primary-kpi",
    "objective-kpi-uom",
    "objective-direction",
    "objective-reporting-frequency",
    "objective-target-plan-granularity",
    "objective-theme-override",
    "objective-target-plan-anchor"
  ];
  const wizardStepRequiredFields = {
    1: ["objective-parent-goal", "objective-name", "objective-statement", "objective-type", "objective-priority", "objective-strategic-theme"],
    2: ["objective-owner-company", "objective-owner-position"],
    3: ["objective-planning-cycle", "objective-horizon-start-date", "objective-horizon-end-date"],
    4: ["objective-primary-kpi"]
  };
  const fallbackColumns = [
    { key: "id", label: "Objective ID" },
    { key: "name", label: "Objective" },
    { key: "parentGoalId", label: "Parent Goal" },
    { key: "owner", label: "Owner" },
    { key: "status", label: "Status" },
    { key: "type", label: "Type" },
    { key: "priority", label: "Priority" },
    { key: "startYear", label: "Start Year" },
    { key: "endYear", label: "End Year" },
    { key: "metricSummary", label: "KPI Summary" },
    { key: "entityScope", label: "Entity Scope" },
    { key: "actions", label: "Actions" }
  ];

  const uniq = (values) => [...new Set((values || []).filter(Boolean).map((v) => String(v).trim()))];
  const nonEmpty = (values, fallback) => (Array.isArray(values) && values.length ? values : fallback);
  const notify = (message, kind = "success") => window.enterpriseStrategyUi?.notify?.(message, kind);
  const cleanText = (value) => {
    const text = String(value ?? "").trim();
    if (!text) return "";
    const normalized = text.toLowerCase();
    if (normalized === "undefined" || normalized === "null" || normalized === "nan") return "";
    return text;
  };
  const debounce = (fn, wait = 250) => {
    let timerId = null;
    return (...args) => {
      if (timerId) clearTimeout(timerId);
      timerId = setTimeout(() => fn(...args), wait);
    };
  };
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();
  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");

  let currentVersion = 0;
  let isEditMode = false;
  let isDirty = false;
  let suppressDirtyTracking = false;
  let suppressLeavePrompt = false;
  let hasSubmitAttempt = false;
  let currentWizardStep = 1;
  let goalsCache = [];
  let strategyPeriodsById = new Map();
  let objectivesCache = [];
  let filteredRows = [];
  let metricOptionsCache = [];
  let filterDrawer = null;
  let parentGoalContextCache = new Map();
  let selectedParentGoalContext = null;
  let selectedGoalPlanningContext = null;
  let suppressOverrideTracking = false;
  let lastParentGoalId = "";
  let userOverrides = new Set();
  let objectiveApplicableCompaniesPickerActiveIndex = -1;
  let objectiveCreationModeCode = "Blank";
  let objectiveSourceTemplateId = "";
  let objectiveSourceTemplateVersion = null;
  let selectedObjectiveSourceMeta = null;
  let objectivePickerCatalogRows = [];
  let objectiveTemplateAppliedFields = new Map();
  let objectiveTemplateCatalogAvailable = true;
  let objectiveTargetPlanRows = [];
  let objectiveTargetPlanSignature = "";
  let objectiveMetricCatalogById = new Map();
  let objectiveMetricAssignmentSeed = null;
  const selectedObjectiveIds = new Set();

  const objectiveUsesTemplateCatalog = (mode = objectiveCreationModeCode) => cleanText(mode || "").toLowerCase() !== "blank";
  const objectiveCreationModeLabel = (mode = objectiveCreationModeCode) => {
    const normalized = cleanText(mode || "");
    if (normalized === "GoalTemplate") return "From Goal + Objective Template";
    if (normalized === "Template") return "From Objective Template";
    return "Blank";
  };

  const parseWorkspaceRouteContext = () => {
    if (!isWorkspaceMode) return { mode: "list", objectiveId: "" };
    const path = String(window.location.pathname || "");
    const editMatch = path.match(/\/objectives\/([^/]+)\/edit\/?$/i);
    if (editMatch) {
      return {
        mode: "edit",
        objectiveId: decodeURIComponent(String(editMatch[1] || "").trim())
      };
    }
    const search = new URLSearchParams(window.location.search || "");
    const duplicateId = String(search.get("duplicateFrom") || "").trim();
    if (duplicateId) return { mode: "duplicate", objectiveId: duplicateId };
    const editId = String(search.get("id") || search.get("objectiveId") || "").trim();
    if (editId) return { mode: "edit", objectiveId: editId };
    return { mode: "create", objectiveId: "" };
  };

  const navigateToObjectiveWorkspace = (mode, objectiveId) => {
    const id = String(objectiveId || "").trim();
    if (mode === "edit" && id) {
      window.location.assign(objectiveEditUrl(id));
      return;
    }
    if (mode === "duplicate" && id) {
      window.location.assign(objectiveDuplicateUrl(id));
      return;
    }
    window.location.assign(objectiveCreateUrl);
  };

  const parseYear = (value) => {
    const text = String(value || "").trim();
    if (!text) return null;
    if (/^\d{4}$/.test(text)) {
      const year = Number(text);
      return Number.isInteger(year) ? year : null;
    }
    const isoMatch = text.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (isoMatch) {
      const year = Number(isoMatch[1]);
      const month = Number(isoMatch[2]);
      const day = Number(isoMatch[3]);
      const dt = new Date(year, month - 1, day);
      return dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day ? year : null;
    }
    const dmyMatch = text.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
    if (!dmyMatch) return null;
    const day = Number(dmyMatch[1]);
    const month = Number(dmyMatch[2]);
    const year = Number(dmyMatch[3]);
    const dt = new Date(year, month - 1, day);
    return dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day ? year : null;
  };

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

  const parseDateIso = (value) => {
    const text = String(value || "").trim();
    const match = text.match(/^(\d{4}-\d{2}-\d{2})/);
    return match ? match[1] : "";
  };

  const parseDmyToIso = (value) => {
    const text = String(value || "").trim();
    const match = text.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
    if (!match) return "";
    const day = Number(match[1]);
    const month = Number(match[2]);
    const year = Number(match[3]);
    const dt = new Date(year, month - 1, day);
    if (dt.getFullYear() !== year || dt.getMonth() !== month - 1 || dt.getDate() !== day) return "";
    return `${String(year).padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  };

  const formatIsoForDisplay = (value) => {
    const iso = parseDateIso(value);
    const match = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : "";
  };

  const fromDateToYear = (value) => {
    const iso = parseDateIso(value);
    return iso ? iso.slice(0, 4) : "";
  };

  const objectiveHorizonIsoFromInput = (id) => {
    const el = document.getElementById(id);
    if (!el) return "";
    if (String(el.type || "").toLowerCase() === "date") {
      return parseDateIso(el.value);
    }
    if (window.esbpHorizonDates?.getIsoFromInput) {
      const iso = String(window.esbpHorizonDates.getIsoFromInput(el) || "").trim();
      if (iso) return iso;
    }
    return parseDmyToIso(el.value) || parseDateIso(el.value);
  };

  const setObjectiveHorizonInputFromRaw = (id, raw, isEnd, triggerChange = false) => {
    const el = document.getElementById(id);
    if (!el) return;
    const iso = parseDmyToIso(raw) || parseDateIso(raw);
    const year = parseYear(raw);
    const finalIso = iso || (year ? `${year}-${isEnd ? "12-31" : "01-01"}` : "");
    if (String(el.type || "").toLowerCase() === "date") {
      el.value = finalIso || "";
      if (triggerChange) el.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    if (window.esbpHorizonDates?.setInputIso) {
      window.esbpHorizonDates.setInputIso(el, finalIso, triggerChange);
      if (!String(el.value || "").trim() && finalIso) {
        el.value = formatIsoForDisplay(finalIso);
      }
      return;
    }
    el.value = finalIso ? formatIsoForDisplay(finalIso) : "";
    if (triggerChange) el.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const setObjectiveHorizonBounds = (id, hasBounds, minIsoDate, maxIsoDate) => {
    const el = document.getElementById(id);
    if (!el) return;
    const minIso = hasBounds ? String(minIsoDate || "") : "2000-01-01";
    const maxIso = hasBounds ? String(maxIsoDate || "") : "2200-12-31";
    if (el._flatpickr) {
      el._flatpickr.set("minDate", minIso);
      el._flatpickr.set("maxDate", maxIso);
      return;
    }
    el.min = minIso;
    el.max = maxIso;
  };

  const selectedGoalIdFromForm = () => cleanText(document.getElementById("objective-parent-goal")?.value || "");
  const selectedValues = (id) => Array.from(document.getElementById(id)?.selectedOptions || []).map((x) => String(x.value || "").trim()).filter(Boolean);
  const setSelectedValues = (id, values) => {
    const set = new Set((values || []).map((x) => String(x || "").trim()).filter(Boolean));
    const el = document.getElementById(id);
    if (!el) return;
    Array.from(el.options || []).forEach((opt) => {
      opt.selected = set.has(String(opt.value || "").trim());
    });
    if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) {
      window.jQuery(el).trigger("change.select2");
    }
    if (id === "objective-applicable-companies") {
      syncObjectiveApplicableCompaniesPickerFromSelect();
    }
  };

  const normalizedObjectiveCompanyOptions = () => {
    const preferred = typeof workbook.companyOptions === "function"
      ? (workbook.companyOptions() || [])
      : [];
    const source = preferred.length ? preferred : (Array.isArray(workbook.companies) ? workbook.companies : []);
    const seen = new Set();
    return source
      .map((company) => {
        const value = cleanText(company?.value || company?.companyId || company?.id || "");
        const label = cleanText(
          company?.label
          || (typeof workbook.companyLabel === "function" ? workbook.companyLabel(company) : "")
          || company?.companyName
          || company?.name
          || value
        );
        if (!value || !label) return null;
        const key = `${value}::${label}`;
        if (seen.has(key)) return null;
        seen.add(key);
        return { value, label };
      })
      .filter(Boolean);
  };

  const normalizedObjectiveStringOptions = (...sources) => {
    const seen = new Set();
    return sources
      .flat()
      .map((value) => {
        if (value && typeof value === "object" && !Array.isArray(value)) {
          const normalizedValue = cleanText(value.value || value.id || value.label || value.name || value.text || "");
          const normalizedLabel = cleanText(value.label || value.name || value.text || value.value || value.id || "");
          if (!normalizedValue || !normalizedLabel) return null;
          return { value: normalizedValue, label: normalizedLabel };
        }
        const text = cleanText(value);
        return text ? { value: text, label: text } : null;
      })
      .filter(Boolean)
      .filter((item) => {
        const key = `${item.value}::${item.label}`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      });
  };

  const normalizedObjectiveBusinessUnitOptions = () => normalizedObjectiveStringOptions(
    workbook.businessUnits || [],
    (workbook.companies || []).map((company) => company?.businessUnit || company?.businessUnitId || ""),
    goalsCache.map((goal) => goal?.businessUnitId || goal?.businessUnit || ""),
    objectivesCache.map((objective) => objective?.businessUnitId || objective?.businessUnit || ""),
    selectedParentGoalContext?.businessUnitId || "",
    document.getElementById("objective-business-unit")?.value || ""
  );

  const normalizedObjectiveRegionOptions = () => normalizedObjectiveStringOptions(
    workbook.regions || [],
    (workbook.companies || []).map((company) => company?.region || company?.regionId || company?.countryName || ""),
    goalsCache.map((goal) => goal?.regionId || goal?.region || ""),
    objectivesCache.map((objective) => objective?.regionId || objective?.region || ""),
    selectedParentGoalContext?.regionId || "",
    document.getElementById("objective-region")?.value || ""
  );

  const setValueIfPresent = (fieldId, value) => {
    const el = document.getElementById(fieldId);
    if (el) el.value = value;
  };

  const parseNullableDecimal = (value) => {
    if (value === null || value === undefined) return null;
    const text = String(value).trim();
    if (!text) return null;
    const normalized = text.replace(/,/g, "");
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  };

  const displayNumericValue = (value) => {
    if (value === null || value === undefined || value === "") return "";
    return String(value);
  };

  const normalizeObjectiveYearlyValue = (row) => {
    const source = row || {};
    return {
      year: Number(source.year ?? source.Year ?? 0) || 0,
      periodKey: cleanText(source.periodKey ?? source.PeriodKey ?? ""),
      periodLabel: cleanText(source.periodLabel ?? source.PeriodLabel ?? ""),
      periodStart: parseDateIso(source.periodStart ?? source.PeriodStart ?? ""),
      periodEnd: parseDateIso(source.periodEnd ?? source.PeriodEnd ?? ""),
      periodGranularity: normalizeObjectiveTargetPlanGranularity(source.periodGranularity ?? source.PeriodGranularity ?? ""),
      sortOrder: Number(source.sortOrder ?? source.SortOrder ?? 0) || 0,
      targetValue: parseNullableDecimal(source.targetValue ?? source.TargetValue),
      actualValue: parseNullableDecimal(source.actualValue ?? source.ActualValue),
      forecastValue: parseNullableDecimal(source.forecastValue ?? source.ForecastValue),
      thresholdMin: parseNullableDecimal(source.thresholdMin ?? source.ThresholdMin),
      thresholdMax: parseNullableDecimal(source.thresholdMax ?? source.ThresholdMax),
      commentary: cleanText(source.commentary ?? source.Commentary ?? "")
    };
  };

  const normalizeGoalMetricYearValue = (row) => {
    const source = row || {};
    const year = Number(source.year ?? source.Year ?? 0) || 0;
    const periodKey = cleanText(source.periodKey ?? source.PeriodKey ?? (year ? String(year) : ""));
    return {
      year,
      periodKey,
      periodLabel: cleanText(source.periodLabel ?? source.PeriodLabel ?? periodKey),
      periodStart: parseDateIso(source.periodStart ?? source.PeriodStart ?? ""),
      periodEnd: parseDateIso(source.periodEnd ?? source.PeriodEnd ?? ""),
      targetValue: parseNullableDecimal(source.targetValue ?? source.TargetValue),
      actualValue: parseNullableDecimal(source.actualValue ?? source.ActualValue),
      forecastValue: parseNullableDecimal(source.forecastValue ?? source.ForecastValue),
      thresholdMin: parseNullableDecimal(source.thresholdMin ?? source.ThresholdMin),
      thresholdMax: parseNullableDecimal(source.thresholdMax ?? source.ThresholdMax),
      commentary: cleanText(source.commentary ?? source.Commentary ?? "")
    };
  };

  const normalizeGoalMetricAssignment = (metric) => {
    const row = metric || {};
    const yearlySource = Array.isArray(row.yearlyValues)
      ? row.yearlyValues
      : (Array.isArray(row.YearlyValues)
        ? row.YearlyValues
        : (Array.isArray(row.yearlyTargets) ? row.yearlyTargets : []));
    return {
      ...row,
      id: cleanText(row.id || row.Id || row.metricAssignmentId || row.MetricAssignmentId || ""),
      goalId: cleanText(row.goalId || row.GoalId || ""),
      metricDefinitionId: cleanText(row.metricDefinitionId || row.MetricDefinitionId || row.metricDefId || row.MetricDefId || ""),
      metricName: cleanText(row.metricName || row.MetricName || row.metricDefinitionId || row.MetricDefinitionId || ""),
      unitOfMeasure: cleanText(row.unitOfMeasure || row.UnitOfMeasure || ""),
      directionPolarity: cleanText(row.directionPolarity || row.DirectionPolarity || row.polarityCode || row.PolarityCode || ""),
      reportingFrequency: cleanText(row.reportingFrequency || row.ReportingFrequency || row.reportingFrequencyCode || row.ReportingFrequencyCode || ""),
      thresholdModel: cleanText(row.thresholdModel || row.ThresholdModel || row.thresholdModelCode || row.ThresholdModelCode || ""),
      targetValue: parseNullableDecimal(row.targetValue ?? row.TargetValue),
      yearlyValues: yearlySource
        .map(normalizeGoalMetricYearValue)
        .filter((item) => item.year > 0 || item.periodKey)
        .sort((left, right) => left.year - right.year)
    };
  };

  const currentParentGoalMetrics = () => Array.isArray(selectedParentGoalContext?.metrics) ? selectedParentGoalContext.metrics : [];

  const currentParentGoalMetric = () => {
    const metrics = currentParentGoalMetrics();
    if (!metrics.length) return null;
    const objectiveMetricId = cleanText(document.getElementById("objective-primary-kpi")?.value || "");
    if (!objectiveMetricId) return metrics[0] || null;
    return metrics.find((metric) => {
      const ids = [
        metric.id,
        metric.metricDefinitionId,
        metric.metricName
      ].map(cleanText).filter(Boolean);
      return ids.some((candidate) => candidate.localeCompare(objectiveMetricId, undefined, { sensitivity: "accent" }) === 0);
    }) || metrics[0] || null;
  };

  const normalizeObjectiveMetricAssignment = (metric) => {
    const row = metric || {};
    const yearlySource = Array.isArray(row.yearlyValues)
      ? row.yearlyValues
      : (Array.isArray(row.YearlyValues)
        ? row.YearlyValues
        : (Array.isArray(row.yearlyTargets) ? row.yearlyTargets : []));
    return {
      ...row,
      parentMetricAssignmentId: cleanText(row.parentMetricAssignmentId || row.ParentMetricAssignmentId || ""),
      metricDefId: cleanText(row.metricDefId || row.MetricDefId || row.metricDefinitionId || row.MetricDefinitionId || ""),
      metricId: cleanText(row.metricId || row.MetricId || row.metricDefId || row.MetricDefId || ""),
      metricName: cleanText(row.metricName || row.MetricName || row.metricId || row.MetricId || ""),
      metricClass: cleanText(row.metricClass || row.MetricClass || "Inherited") || "Inherited",
      metricRole: cleanText(row.metricRole || row.MetricRole || "Contribution") || "Contribution",
      aggregationMethod: cleanText(row.aggregationMethod || row.AggregationMethod || row.aggregationMethodId || row.AggregationMethodId || ""),
      aggregationMethodId: cleanText(row.aggregationMethodId || row.AggregationMethodId || row.aggregationMethod || row.AggregationMethod || ""),
      thresholdTolerance: cleanText(row.thresholdTolerance || row.ThresholdTolerance || ""),
      thresholdValue: parseNullableDecimal(row.thresholdValue ?? row.ThresholdValue),
      thresholdModelCode: cleanText(row.thresholdModelCode || row.ThresholdModelCode || row.thresholdModel || row.ThresholdModel || ""),
      reportingFrequencyCode: cleanText(row.reportingFrequencyCode || row.ReportingFrequencyCode || row.reportingFrequency || row.ReportingFrequency || ""),
      unitOfMeasureId: cleanText(row.unitOfMeasureId || row.UnitOfMeasureId || row.unitOfMeasure || row.UnitOfMeasure || ""),
      unitOfMeasure: cleanText(row.unitOfMeasure || row.UnitOfMeasure || row.unitOfMeasureId || row.UnitOfMeasureId || ""),
      direction: cleanText(row.direction || row.Direction || ""),
      polarityCode: cleanText(row.polarityCode || row.PolarityCode || row.direction || row.Direction || ""),
      metricBindingStatus: cleanText(row.metricBindingStatus || row.MetricBindingStatus || ""),
      rollupEligibleFlag: row.rollupEligibleFlag !== false && row.RollupEligibleFlag !== false,
      yearlyValues: yearlySource
        .map(normalizeObjectiveYearlyValue)
        .filter((item) => item.year > 0 || item.periodKey)
        .sort((left, right) => {
          const leftOrder = Number.isFinite(left.sortOrder) ? left.sortOrder : Number.MAX_SAFE_INTEGER;
          const rightOrder = Number.isFinite(right.sortOrder) ? right.sortOrder : Number.MAX_SAFE_INTEGER;
          if (leftOrder !== rightOrder) return leftOrder - rightOrder;
          return cleanText(left.periodStart || left.periodKey || left.year).localeCompare(cleanText(right.periodStart || right.periodKey || right.year));
        })
    };
  };

  const currentObjectiveMetricCatalogEntry = () => {
    const metricId = cleanText(document.getElementById("objective-primary-kpi")?.value || "");
    return metricId ? (objectiveMetricCatalogById.get(metricId) || null) : null;
  };

  const normalizeObjectiveTargetPlanGranularity = (value) => {
    const normalized = cleanText(value).toLowerCase().replace(/\s+/g, "");
    if (normalized === "quarterly") return "Quarterly";
    if (normalized === "monthly") return "Monthly";
    if (normalized === "totalstrategyperiod" || normalized === "totalperiod" || normalized === "total") return "TotalStrategyPeriod";
    return "Yearly";
  };

  const currentObjectiveTargetPlanGranularity = () => normalizeObjectiveTargetPlanGranularity(objectiveTargetPlanGranularityEl?.value || "Yearly");

  const normalizeReportingFrequency = (value) => {
    const normalized = cleanText(value).toLowerCase().replace(/\s+/g, "");
    if (normalized === "realtime" || normalized === "daily" || normalized === "weekly" || normalized === "monthly") return "Monthly";
    if (normalized === "quarterly") return "Quarterly";
    if (normalized === "annually" || normalized === "annual" || normalized === "yearly") return "Yearly";
    return cleanText(value);
  };

  const planningCadenceRank = (granularity) => {
    const normalized = normalizeObjectiveTargetPlanGranularity(granularity);
    if (normalized === "Monthly") return 4;
    if (normalized === "Quarterly") return 3;
    if (normalized === "Yearly") return 2;
    if (normalized === "TotalStrategyPeriod") return 1;
    return 0;
  };

  const reportingCadenceRank = (frequency) => {
    const normalized = normalizeReportingFrequency(frequency);
    if (normalized === "Monthly") return 4;
    if (normalized === "Quarterly") return 3;
    if (normalized === "Yearly") return 2;
    return 0;
  };

  const objectiveStrategyPeriodContextRange = () => {
    const strategyPeriodId = cleanText(selectedGoalPlanningContext?.strategyPeriodId || document.getElementById("objective-planning-cycle")?.value || "");
    const strategyPeriod = strategyPeriodsById.get(strategyPeriodId) || null;
    return {
      strategyPeriodId,
      strategyPeriod,
      startIso: parseDateIso(selectedGoalPlanningContext?.strategyPeriodStartDate || strategyPeriod?.startDate || ""),
      endIso: parseDateIso(selectedGoalPlanningContext?.strategyPeriodEndDate || strategyPeriod?.endDate || "")
    };
  };

  const objectiveEffectivePlanningRange = () => {
    const strategyPeriodRange = objectiveStrategyPeriodContextRange();
    const horizonStart = objectiveHorizonIsoFromInput("objective-horizon-start-date");
    const horizonEnd = objectiveHorizonIsoFromInput("objective-horizon-end-date");
    const startCandidates = [strategyPeriodRange.startIso, horizonStart].filter(Boolean).sort();
    const endCandidates = [strategyPeriodRange.endIso, horizonEnd].filter(Boolean).sort();
    const startIso = startCandidates.length ? startCandidates[startCandidates.length - 1] : "";
    const endIso = endCandidates.length ? endCandidates[0] : "";
    return {
      strategyPeriodId: strategyPeriodRange.strategyPeriodId,
      strategyPeriod: strategyPeriodRange.strategyPeriod,
      strategyPeriodStartIso: strategyPeriodRange.startIso,
      strategyPeriodEndIso: strategyPeriodRange.endIso,
      horizonStartIso: horizonStart,
      horizonEndIso: horizonEnd,
      startIso,
      endIso,
      hasRange: Boolean(startIso && endIso && endIso >= startIso)
    };
  };

  const currentObjectivePlanSignature = () => {
    const planningRange = objectiveEffectivePlanningRange();
    const metricId = cleanText(document.getElementById("objective-primary-kpi")?.value || "");
    return [planningRange.strategyPeriodId, planningRange.startIso, planningRange.endIso, currentObjectiveTargetPlanGranularity(), metricId].join("|");
  };

  const deriveObjectivePlanPeriods = () => {
    const planningRange = objectiveEffectivePlanningRange();
    const { strategyPeriodId, strategyPeriod, startIso, endIso, hasRange } = planningRange;
    if (!strategyPeriodId || !hasRange) return [];
    const granularity = currentObjectiveTargetPlanGranularity();
    const periods = [];
    const pushPeriod = (period) => periods.push({
      key: period.key,
      year: period.year,
      label: period.label,
      granularity,
      periodStart: period.periodStart,
      periodEnd: period.periodEnd,
      sortOrder: periods.length
    });
    const startDate = new Date(`${startIso}T00:00:00`);
    const endDate = new Date(`${endIso}T00:00:00`);
    if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime()) || endDate < startDate) return [];

    if (granularity === "TotalStrategyPeriod") {
      pushPeriod({
        key: `${startIso}_${endIso}`,
        year: Number(startIso.slice(0, 4)),
        label: "Total Strategy Period",
        periodStart: startIso,
        periodEnd: endIso
      });
      return periods;
    }

    if (granularity === "Monthly") {
      const cursor = new Date(startDate.getFullYear(), startDate.getMonth(), 1);
      while (cursor <= endDate) {
        const monthStart = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
        const monthEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0);
        const boundedStart = monthStart < startDate ? startDate : monthStart;
        const boundedEnd = monthEnd > endDate ? endDate : monthEnd;
        const year = cursor.getFullYear();
        const month = String(cursor.getMonth() + 1).padStart(2, "0");
        pushPeriod({
          key: `${year}-${month}`,
          year,
          label: `${year}-${cursor.toLocaleString("en-US", { month: "short" })}`,
          periodStart: parseDateIso(boundedStart.toISOString()),
          periodEnd: parseDateIso(boundedEnd.toISOString())
        });
        cursor.setMonth(cursor.getMonth() + 1);
      }
      return periods;
    }

    if (granularity === "Quarterly") {
      const startQuarterMonth = Math.floor(startDate.getMonth() / 3) * 3;
      const cursor = new Date(startDate.getFullYear(), startQuarterMonth, 1);
      while (cursor <= endDate) {
        const quarter = Math.floor(cursor.getMonth() / 3) + 1;
        const quarterStart = new Date(cursor.getFullYear(), cursor.getMonth(), 1);
        const quarterEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 3, 0);
        const boundedStart = quarterStart < startDate ? startDate : quarterStart;
        const boundedEnd = quarterEnd > endDate ? endDate : quarterEnd;
        pushPeriod({
          key: `${cursor.getFullYear()}-Q${quarter}`,
          year: cursor.getFullYear(),
          label: `${cursor.getFullYear()}-Q${quarter}`,
          periodStart: parseDateIso(boundedStart.toISOString()),
          periodEnd: parseDateIso(boundedEnd.toISOString())
        });
        cursor.setMonth(cursor.getMonth() + 3);
      }
      return periods;
    }

    const startYear = Number(startIso.slice(0, 4));
    const endYear = Number(endIso.slice(0, 4));
    if (!Number.isInteger(startYear) || !Number.isInteger(endYear) || endYear < startYear) return [];
    for (let year = startYear; year <= endYear; year += 1) {
      pushPeriod({
        key: String(year),
        year,
        label: String(year),
        periodStart: year === startYear ? startIso : `${String(year).padStart(4, "0")}-01-01`,
        periodEnd: year === endYear ? endIso : `${String(year).padStart(4, "0")}-12-31`
      });
    }
    return periods;
  };

  const objectiveTargetPlanMatchesCurrentPeriods = (rows = objectiveTargetPlanRows) => {
    const periods = deriveObjectivePlanPeriods();
    if (!rows.length || !periods.length) return rows.length === 0 && periods.length === 0;
    if (rows.length !== periods.length) return false;
    return rows.every((row, index) => cleanText(row.periodKey || row.year || "") === cleanText(periods[index]?.key || ""));
  };

  const objectiveTargetPlanNeedsRegeneration = () => {
    if (!objectiveTargetPlanRows.length) return false;
    if (!objectiveTargetPlanMatchesCurrentPeriods()) return true;
    if (!objectiveTargetPlanSignature) return false;
    return objectiveTargetPlanSignature !== currentObjectivePlanSignature();
  };

  const objectiveTargetPlanHasValues = () => objectiveTargetPlanRows.some((row) => (
    row.targetValue !== null
    || row.actualValue !== null
    || row.forecastValue !== null
    || row.thresholdMin !== null
    || row.thresholdMax !== null
    || cleanText(row.commentary)
  ));

  const objectiveThresholdsRequired = () => {
    const currentMetric = currentObjectiveMetricCatalogEntry();
    const model = cleanText(
      currentMetric?.thresholdModel
      || currentMetric?.thresholdModelCode
      || currentMetric?.thresholdModelId
      || objectiveMetricAssignmentSeed?.thresholdModelCode
      || ""
    ).toLowerCase();
    return model.includes("range") || model.includes("band") || model.includes("between");
  };

  const buildObjectiveTargetPlanRows = ({ existingRows = objectiveTargetPlanRows, preserveValues = true } = {}) => {
    const rowsByKey = new Map((existingRows || []).map((row) => [cleanText(row.periodKey || row.year || ""), row]));
    return deriveObjectivePlanPeriods().map((period) => {
      const existing = rowsByKey.get(cleanText(period.key)) || {};
      return {
        year: period.year,
        periodKey: period.key,
        periodLabel: period.label,
        periodGranularity: period.granularity,
        periodStart: period.periodStart,
        periodEnd: period.periodEnd,
        sortOrder: period.sortOrder,
        targetValue: preserveValues ? parseNullableDecimal(existing.targetValue) : null,
        actualValue: preserveValues ? parseNullableDecimal(existing.actualValue) : null,
        forecastValue: preserveValues ? parseNullableDecimal(existing.forecastValue) : null,
        thresholdMin: preserveValues ? parseNullableDecimal(existing.thresholdMin) : null,
        thresholdMax: preserveValues ? parseNullableDecimal(existing.thresholdMax) : null,
        commentary: preserveValues ? cleanText(existing.commentary) : ""
      };
    });
  };

  const serializeObjectiveTargetPlanRows = (rows = objectiveTargetPlanRows) => rows.map((row) => ({
    year: Number(row.year || 0),
    periodKey: cleanText(row.periodKey || ""),
    periodLabel: cleanText(row.periodLabel || ""),
    periodStart: parseDateIso(row.periodStart || ""),
    periodEnd: parseDateIso(row.periodEnd || ""),
    periodGranularity: normalizeObjectiveTargetPlanGranularity(row.periodGranularity || currentObjectiveTargetPlanGranularity()),
    sortOrder: Number(row.sortOrder || 0) || 0,
    targetValue: parseNullableDecimal(row.targetValue),
    actualValue: parseNullableDecimal(row.actualValue),
    forecastValue: parseNullableDecimal(row.forecastValue),
    thresholdMin: parseNullableDecimal(row.thresholdMin),
    thresholdMax: parseNullableDecimal(row.thresholdMax),
    commentary: cleanText(row.commentary)
  })).filter((row) => row.year > 0);

  const objectivePlanningPrerequisiteState = () => {
    const planningRange = objectiveEffectivePlanningRange();
    const metricId = cleanText(document.getElementById("objective-primary-kpi")?.value || "");
    return {
      parentGoalId: selectedGoalIdFromForm(),
      strategyPeriodId: planningRange.strategyPeriodId,
      startIso: planningRange.startIso,
      endIso: planningRange.endIso,
      metricId,
      hasPrerequisites: Boolean(selectedGoalIdFromForm() && planningRange.strategyPeriodId && planningRange.hasRange && metricId)
    };
  };

  const objectiveTargetPlanListHtml = (items) => {
    if (!items?.length) return '<li class="text-muted">None</li>';
    return items.map((item) => `<li>${escapeHtml(item)}</li>`).join("");
  };

  const syncObjectiveKpiMetadataFromCatalog = () => {
    const metric = currentObjectiveMetricCatalogEntry();
    if (!metric) return;
    const unit = cleanText(metric.unitOfMeasure || metric.unitOfMeasureId || metric.uom || "");
    const direction = cleanText(metric.directionOfPerformance || metric.performanceDirection || metric.direction || "");
    const frequency = cleanText(metric.reportingFrequency || metric.reportingFrequencyId || metric.frequency || "");
    if (unit) {
      ensureSelectOption("objective-kpi-uom", unit, unit);
      setValueIfPresent("objective-kpi-uom", unit);
    }
    if (direction) {
      ensureSelectOption("objective-direction", direction, direction);
      setValueIfPresent("objective-direction", direction);
    }
    if (frequency) {
      ensureSelectOption("objective-reporting-frequency", frequency, frequency);
      setValueIfPresent("objective-reporting-frequency", frequency);
    }
  };

  const objectiveTargetPlanGovernanceWarnings = (payload = collectPayload()) => {
    const warnings = [];
    const granularity = currentObjectiveTargetPlanGranularity();
    const reportingFrequency = normalizeReportingFrequency(payload.reportingFrequency);
    const granularityRank = planningCadenceRank(granularity);
    const reportingRank = reportingCadenceRank(reportingFrequency);
    if (granularityRank > 0 && reportingRank > 0 && granularityRank > reportingRank) {
      warnings.push("Reporting cadence is less frequent than target cadence. Monitoring discipline may be weak.");
    }
    if (granularity === "Monthly" && !["Transformation", "Operations", "Operational", "Capability"].includes(cleanText(payload.type))) {
      warnings.push("Monthly target planning can feel too operational for a strategic ES&BP Objective.");
    }
    if (granularity === "TotalStrategyPeriod" && reportingRank >= 4) {
      warnings.push("Total Strategy Period planning with highly frequent reporting may indicate that intermediate phasing is missing.");
    }
    if (granularity === "Quarterly" && reportingFrequency === "Yearly") {
      warnings.push("Quarterly target plan with yearly reporting is a weak governance combination.");
    }
    if (granularity === "Monthly" && reportingFrequency === "Quarterly") {
      warnings.push("Monthly target plan with quarterly reporting is a weak governance combination.");
    }
    return [...new Set(warnings)];
  };

  const updateObjectiveTargetPlanGovernanceWarningBanner = (warnings) => {
    if (!objectiveTargetPlanGovernanceWarningEl || !objectiveTargetPlanGovernanceWarningTextEl) return;
    const items = Array.isArray(warnings) ? warnings.filter(Boolean) : [];
    objectiveTargetPlanGovernanceWarningEl.classList.toggle("d-none", items.length === 0);
    objectiveTargetPlanGovernanceWarningTextEl.textContent = items.join(" ");
  };

  const syncObjectiveTargetPlanSettingsUi = () => {
    if (objectiveTargetPlanGranularityEl) {
      const normalizedGranularity = currentObjectiveTargetPlanGranularity();
      if (objectiveTargetPlanGranularityEl.value !== normalizedGranularity) objectiveTargetPlanGranularityEl.value = normalizedGranularity;
      objectiveTargetPlanGranularityEl.dataset.previousValue = normalizedGranularity;
    }
    if (objectiveTargetPlanStrategyPeriodEl) {
      const strategyPeriodId = cleanText(selectedGoalPlanningContext?.strategyPeriodId || document.getElementById("objective-planning-cycle")?.value || "");
      const strategyPeriod = strategyPeriodsById.get(strategyPeriodId) || selectedGoalPlanningContext || null;
      objectiveTargetPlanStrategyPeriodEl.value = strategyPeriodId
        ? strategyPeriodDisplayLabel(strategyPeriod || { strategyPeriodCode: strategyPeriodId, strategyPeriodName: "Strategy Period" })
        : "";
    }
  };

  const companyLabelById = (id) => {
    const normalizedId = cleanText(id);
    if (!normalizedId) return "";
    const optionMatch = normalizedObjectiveCompanyOptions().find((item) => cleanText(item.value).toLowerCase() === normalizedId.toLowerCase());
    if (optionMatch) return optionMatch.label;
    const workbookDisplay = cleanText(workbook.companyDisplayName?.(normalizedId) || "");
    if (workbookDisplay) return workbookDisplay;
    return normalizedId;
  };

  const normalizeGoalRow = (goal) => {
    const row = goal || {};
    const metrics = (Array.isArray(row.metrics) ? row.metrics : (Array.isArray(row.Metrics) ? row.Metrics : []))
      .map(normalizeGoalMetricAssignment);
    return {
      ...row,
      id: cleanText(row.id || row.goalId || row.goal_id || row.GoalId || row.ID || ""),
      name: cleanText(row.name || row.goalTitle || row.goalName || row.goal_name || row.GoalTitle || row.Name || ""),
      category: cleanText(row.category || row.Category || ""),
      type: cleanText(row.type || row.category || row.Type || row.Category || ""),
      strategicThemeId: cleanText(row.strategicThemeId || row.StrategicThemeId || row.category || row.Category || ""),
      status: cleanText(row.status || row.Status || ""),
      priority: cleanText(row.priority || row.Priority || ""),
      ownerRole: cleanText(row.ownerRole || row.ownerId || row.OwnerRole || row.OwnerId || ""),
      ownerCompanyId: cleanText(row.ownerCompanyId || row.primaryCompanyId || row.OwnerCompanyId || row.PrimaryCompanyId || ""),
      ownerPersonId: cleanText(row.ownerPersonId || row.ownerDisplayName || row.OwnerPersonId || row.OwnerDisplayName || ""),
      sourceTemplateId: cleanText(row.sourceTemplateId || row.SourceTemplateId || ""),
      sourceTemplateType: cleanText(row.sourceTemplateType || row.SourceTemplateType || ""),
      strategyPeriodId: cleanText(row.strategyPeriodId || row.planningCycle || row.planningCycleId || row.StrategyPeriodId || row.PlanningCycleId || ""),
      strategyPeriodCode: cleanText(row.strategyPeriodCode || row.StrategyPeriodCode || ""),
      strategyPeriodName: cleanText(row.strategyPeriodName || row.StrategyPeriodName || ""),
      startDate: row.startDate || row.planningHorizonStart || null,
      endDate: row.endDate || row.planningHorizonEnd || null,
      planningHorizonStart: row.planningHorizonStart || row.startDate || null,
      planningHorizonEnd: row.planningHorizonEnd || row.endDate || null,
      primaryCompanyId: cleanText(row.primaryCompanyId || row.ownerCompanyId || row.PrimaryCompanyId || row.OwnerCompanyId || ""),
      applicableCompanyIds: Array.isArray(row.applicableCompanyIds)
        ? row.applicableCompanyIds
        : (Array.isArray(row.ApplicableCompanyIds) ? row.ApplicableCompanyIds : []),
      appliesToAllCompanies: Boolean(row.appliesToAllCompanies ?? row.AppliesToAllCompanies ?? row.appliesToAllCompaniesFlag ?? row.AppliesToAllCompaniesFlag),
      entityScope: cleanText(row.entityScope || row.relatedEntityScope || row.EntityScope || row.RelatedEntityScope || ""),
      businessUnit: cleanText(row.businessUnit || row.businessUnitId || row.BusinessUnit || row.BusinessUnitId || ""),
      businessUnitId: cleanText(row.businessUnitId || row.businessUnit || row.BusinessUnitId || row.BusinessUnit || ""),
      region: cleanText(row.region || row.regionId || row.Region || row.RegionId || ""),
      regionId: cleanText(row.regionId || row.region || row.RegionId || row.Region || ""),
      metrics
    };
  };

  const normalizeObjectiveRow = (objective) => {
    const row = objective || {};
    const metrics = (Array.isArray(row.metrics) ? row.metrics : (Array.isArray(row.metricAssignments) ? row.metricAssignments : []))
      .map(normalizeObjectiveMetricAssignment);
    return {
      ...row,
      id: String(row.id || row.objectiveId || "").trim(),
      name: String(row.name || row.objectiveName || "").trim(),
      parentGoalId: String(row.parentGoalId || row.goalId || row.goal_id || "").trim(),
      statement: String(row.statement || row.objectiveStatement || "").trim(),
      owner: String(row.owner || row.ownerId || "").trim(),
      ownerCompanyId: String(row.ownerCompanyId || row.OwnerCompanyId || row.primaryCompanyId || "").trim(),
      ownerPositionId: String(row.ownerPositionId || row.OwnerPositionId || "").trim(),
      currentOwnerPersonId: String(row.currentOwnerPersonId || row.CurrentOwnerPersonId || row.owner || row.ownerId || "").trim(),
      strategicTheme: String(row.strategicTheme || row.strategicThemeId || "").trim(),
      status: String(row.status || row.lifecycleState || "").trim(),
      type: String(row.type || row.objectiveTypeId || "").trim(),
      planningCycle: String(row.planningCycle || row.planningCycleId || row.strategyPeriodId || "").trim(),
      priority: String(row.priority || "").trim(),
      primaryKpiMetric: String(row.primaryKpiMetric || row.primaryMetricId || "").trim(),
      unitOfMeasure: String(row.unitOfMeasure || row.unitOfMeasureId || "").trim(),
      directionOfPerformance: String(row.directionOfPerformance || row.performanceDirection || "").trim(),
      targetPlanGranularity: normalizeObjectiveTargetPlanGranularity(row.targetPlanGranularity || row.TargetPlanGranularity || "Yearly"),
      reportingFrequency: String(row.reportingFrequency || row.reportingFrequencyId || "").trim(),
      timeHorizonStart: row.timeHorizonStart || row.startDate || null,
      timeHorizonEnd: row.timeHorizonEnd || row.endDate || null,
      inheritCompanyScope: !(row.inheritCompanyScope === false || row.inheritScopeFromParentGoal === false || row.InheritCompanyScope === false),
      sourceTemplateType: String(row.sourceTemplateType || row.SourceTemplateType || "").trim(),
      sourceTemplateId: String(row.sourceTemplateId || row.SourceTemplateId || "").trim(),
      sourceTemplateVersion: Number(row.sourceTemplateVersion ?? row.SourceTemplateVersion ?? 0) || 0,
      primaryCompanyId: String(row.primaryCompanyId || "").trim(),
      applicableCompanyIds: Array.isArray(row.applicableCompanyIds)
        ? row.applicableCompanyIds
        : (Array.isArray(row.ApplicableCompanyIds) ? row.ApplicableCompanyIds : []),
      businessUnit: String(row.businessUnit || row.businessUnitId || "").trim(),
      region: String(row.region || row.regionId || "").trim(),
      entityScope: String(row.entityScope || "").trim(),
      metrics,
      metricAssignments: metrics,
      version: Number(row.version ?? row.Version ?? 0) || 0
    };
  };

  const normalizeObjectiveCatalogItems = (raw) => {
    if (Array.isArray(raw)) return raw;
    if (Array.isArray(raw?.items)) return raw.items;
    if (Array.isArray(raw?.Items)) return raw.Items;
    return [];
  };

  const normalizeObjectiveCatalogRow = (row) => {
    if (!row || typeof row !== "object") return null;
    return {
      id: cleanText(row.id || row.Id || row.templateCode || row.TemplateCode || ""),
      name: cleanText(row.name || row.Name || ""),
      parentGoalTemplateId: cleanText(row.parentGoalTemplateId || row.ParentGoalTemplateId || ""),
      statement: cleanText(row.statement || row.Statement || row.description || row.Description || ""),
      type: cleanText(row.categoryOrType || row.CategoryOrType || row.type || row.Type || row.category || row.Category || ""),
      owner: cleanText(row.owner || row.Owner || ""),
      priority: cleanText(row.priority || row.Priority || ""),
      entityScope: cleanText(row.entityScope || row.EntityScope || ""),
      status: cleanText(row.status || row.Status || row.lifecycleStatus || row.LifecycleStatus || ""),
      templateType: cleanText(row.templateType || row.TemplateType || row.itemType || row.ItemType || ""),
      itemType: cleanText(row.itemType || row.ItemType || ""),
      timeHorizonStart: parseDateIso(row.timeHorizonStart || row.TimeHorizonStart || ""),
      timeHorizonEnd: parseDateIso(row.timeHorizonEnd || row.TimeHorizonEnd || ""),
      version: row.version ?? row.Version ?? null
    };
  };

  const isObjectiveTemplateType = (row) => {
    const type = cleanText(row?.templateType || row?.itemType || "").toLowerCase();
    return type === "objective" || type === "objectivetemplate" || type === "objective template";
  };

  const normalizeObjectiveSourceVersion = (value) => {
    const n = Number(value);
    return Number.isInteger(n) && n > 0 ? n : null;
  };

  const normalizeTemplateTextKey = (value) => cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, "");

  const currentParentGoalType = () => cleanText(selectedParentGoalContext?.goalType || "");
  const currentParentGoalTemplateId = () => cleanText(selectedParentGoalContext?.sourceTemplateId || "");
  const currentCompatibleObjectiveTypes = () => compatibleObjectiveTypes(currentParentGoalType(), workbook.goalObjectiveTypes || []);
  const requiresExactObjectiveTemplateTypeMatch = (goalType = currentParentGoalType()) => normalizeTemplateTextKey(goalType) === "operations";
  const objectiveTemplateTypeMatchesGoalExactly = (templateType, goalType = currentParentGoalType()) => {
    const normalizedTemplateType = normalizeTemplateTextKey(templateType);
    const normalizedGoalType = normalizeTemplateTextKey(goalType);
    if (!normalizedTemplateType || !normalizedGoalType) return true;
    return normalizedTemplateType === normalizedGoalType;
  };

  const objectiveTemplateHorizonHint = (startValue, endValue) => {
    const start = formatIsoForDisplay(startValue);
    const end = formatIsoForDisplay(endValue);
    if (start && end) return `${start} - ${end}`;
    return start || end || "";
  };

  const objectiveTypeCompatibleWithGoal = (type) => {
    const normalizedType = cleanText(type || "");
    const goalType = currentParentGoalType();
    if (!normalizedType || !goalType) return true;
    if (requiresExactObjectiveTemplateTypeMatch(goalType)) {
      return objectiveTemplateTypeMatchesGoalExactly(normalizedType, goalType);
    }
    return currentCompatibleObjectiveTypes().includes(normalizedType);
  };

  const currentObjectiveTemplateCompatibility = (meta = selectedObjectiveSourceMeta) => {
    if (!meta) return { state: "neutral", message: "" };
    if (!selectedGoalIdFromForm()) {
      return { state: "blocked", message: "Select a Parent Goal to load compatible Objective Templates." };
    }

    const templateParentGoalTemplateId = cleanText(meta?.parentGoalTemplateId || "");
    const parentGoalTemplateId = currentParentGoalTemplateId();
    const templateType = cleanText(meta?.type || "");
    const allowedTypes = currentCompatibleObjectiveTypes();
    const templateTypeCompatible = !templateType || objectiveTypeCompatibleWithGoal(templateType);
    const linkageCompatible = !templateParentGoalTemplateId || !parentGoalTemplateId
      ? true
      : templateParentGoalTemplateId.localeCompare(parentGoalTemplateId, undefined, { sensitivity: "accent" }) === 0;

    if (linkageCompatible && templateTypeCompatible) {
      if (templateParentGoalTemplateId && parentGoalTemplateId) {
        return { state: "compatible", message: `Compatible with Parent Goal Template ${parentGoalTemplateId}.` };
      }
      if (templateType && selectedParentGoalContext?.goalType) {
        return { state: "compatible", message: `Compatible with Parent Goal type ${selectedParentGoalContext.goalType}.` };
      }
      return { state: "compatible", message: "Compatible with the selected Parent Goal context." };
    }

    if (!linkageCompatible) {
      return {
        state: "mismatch",
        message: `Template expects Parent Goal Template ${templateParentGoalTemplateId}, but the current Parent Goal source is ${parentGoalTemplateId}.`
      };
    }

    return {
      state: "mismatch",
      message: requiresExactObjectiveTemplateTypeMatch()
        ? `Template type ${templateType} is not compatible with Parent Goal type ${currentParentGoalType() || "-"}. Only ${currentParentGoalType() || "matching"} templates are allowed in this picker.`
        : (allowedTypes.length
          ? `Template type ${templateType} is not compatible with Parent Goal type ${selectedParentGoalContext?.goalType || "-"}. Allowed types: ${allowedTypes.join(", ")}.`
          : `Template type ${templateType} is not compatible with the selected Parent Goal.`)
    };
  };

  const objectiveTemplateRowMatchesCurrentGoal = (row) => currentObjectiveTemplateCompatibility(row).state !== "mismatch";

  const syncObjectiveTemplateBrowseState = () => {
    const browseBtn = document.getElementById("objective-browse-source");
    const clearBtn = document.getElementById("objective-clear-source");
    const helperEl = document.getElementById("objective-browse-source-help");
    const canBrowse = Boolean(selectedGoalIdFromForm() && window.strategyLibraryApi?.catalog && window.strategyLibraryApi?.template);
    if (browseBtn) browseBtn.disabled = !canBrowse;
    if (clearBtn) clearBtn.disabled = !objectiveSourceTemplateId;
    if (helperEl) {
      if (!selectedGoalIdFromForm()) {
        helperEl.textContent = "Select Parent Goal first to load compatible Objective templates.";
      } else if (!objectiveUsesTemplateCatalog()) {
        helperEl.textContent = "Blank mode keeps template defaults off, but you can still browse after Parent Goal is anchored if you decide to apply a compatible Objective Template.";
      } else {
        helperEl.textContent = "Objective Template browsing is filtered by Parent Goal compatibility, Objective Type fit, and active library lifecycle.";
      }
    }
  };

  const updateObjectiveCreationModeUi = () => {
    const helpEl = document.getElementById("objective-creation-mode-help");
    if (!helpEl) return;
    const mode = cleanText(objectiveCreationModeCode || "Blank");
    if (mode === "GoalTemplate") {
      helpEl.textContent = "Goal + Objective Template starts from the selected Parent Goal context, then layers a compatible Objective Template on top.";
    } else if (mode === "Template") {
      helpEl.textContent = "Objective Template mode still requires Parent Goal first so the picker can load only compatible templates.";
    } else {
      helpEl.textContent = "Blank starts without template defaults, but the Objective still must be anchored to a Parent Goal.";
    }
  };

  const updateObjectiveSourcePickerParentHint = (message = "") => {
    const parentField = document.getElementById("objective-source-picker-parent-goal-template");
    const parentGoalNameField = document.getElementById("objective-source-picker-parent-goal-name");
    const helperEl = document.getElementById("objective-source-picker-helper");
    const currentGoalEl = document.getElementById("objective-template-picker-current-goal");
    const currentGoalTemplateEl = document.getElementById("objective-template-picker-current-goal-template");
    const currentTypeEl = document.getElementById("objective-template-picker-current-type");
    const currentScopeEl = document.getElementById("objective-template-picker-current-scope");
    const currentTemplateEl = document.getElementById("objective-template-picker-current-template");
    const warningEl = document.getElementById("objective-template-picker-context-warning");
    if (parentField) parentField.value = currentParentGoalTemplateId();
    if (parentGoalNameField) {
      const goalId = cleanText(selectedParentGoalContext?.goalId || selectedGoalIdFromForm());
      const goalName = cleanText(selectedParentGoalContext?.goalName || goalLabel(goalId));
      parentGoalNameField.value = goalId ? `${goalId}${goalName ? ` - ${goalName.replace(`${goalId} - `, "")}` : ""}` : "";
      if (currentGoalEl) currentGoalEl.textContent = parentGoalNameField.value || "Select Parent Goal first";
    }
    if (currentGoalTemplateEl) currentGoalTemplateEl.textContent = currentParentGoalTemplateId() || "-";
    if (currentTypeEl) currentTypeEl.textContent = currentParentGoalType() || "-";
    if (currentScopeEl) currentScopeEl.textContent = cleanText(selectedParentGoalContext?.entityScope || "") || "-";
    if (currentTemplateEl) currentTemplateEl.textContent = cleanText(selectedObjectiveSourceMeta?.name || objectiveSourceTemplateId || "") || "None selected";
    if (helperEl && message) helperEl.textContent = message;
    if (warningEl) {
      const showWarning = !selectedGoalIdFromForm();
      warningEl.textContent = showWarning
        ? "Select Parent Goal first. Objective Templates are filtered by the selected Parent Goal template and Goal type."
        : "";
      warningEl.classList.toggle("d-none", !showWarning);
    }
  };

  const setObjectiveTemplateAppliedField = (fieldId, value) => {
    const normalizedValue = cleanText(value);
    if (!fieldId || !normalizedValue) return;
    objectiveTemplateAppliedFields.set(fieldId, normalizedValue);
  };

  const setObjectivePrefillField = (fieldId, value, label = "") => {
    const el = document.getElementById(fieldId);
    const normalizedValue = cleanText(value);
    if (!el || !normalizedValue) return false;
    if (el.tagName === "SELECT") {
      ensureSelectOption(fieldId, normalizedValue, label || normalizedValue);
      el.value = normalizedValue;
      if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) {
        window.jQuery(el).trigger("change.select2");
      }
    } else {
      el.value = normalizedValue;
    }
    setObjectiveTemplateAppliedField(fieldId, normalizedValue);
    return true;
  };

  const clearObjectiveTemplateAppliedFields = ({ preserveUserEdits = true } = {}) => {
    let ownerPositionCleared = false;
    objectiveTemplateAppliedFields.forEach((appliedValue, fieldId) => {
      const el = document.getElementById(fieldId);
      if (!el) return;
      if (preserveUserEdits && userOverrides.has(fieldId)) return;
      const currentValue = el.multiple ? selectedValues(fieldId).join("|") : cleanText(el.value);
      if (currentValue && currentValue !== appliedValue) return;
      if (el.tagName === "SELECT") {
        el.value = "";
        if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) {
          window.jQuery(el).trigger("change.select2");
        }
      } else {
        el.value = "";
      }
      if (fieldId === "objective-owner-position") ownerPositionCleared = true;
    });
    objectiveTemplateAppliedFields = new Map();
    if (ownerPositionCleared) syncObjectiveCurrentOwnerPerson();
  };

  async function resolveObjectiveTemplateOwnerPosition(ownerText) {
    const owner = cleanText(ownerText);
    if (!owner) return null;
    let positionState = workbook.positionLoadState?.() || { status: "idle", error: "" };
    const hasLoadedOptions = (workbook.positionOptions?.() || []).length > 0;
    if ((!hasLoadedOptions || positionState.status === "idle" || positionState.status === "loading") && typeof workbook.ensurePositionsLoaded === "function") {
      try {
        await workbook.ensurePositionsLoaded();
      } catch (_) {
      }
      positionState = workbook.positionLoadState?.() || positionState;
    }
    if (positionState.status === "error") return null;
    const ownerCompanyId = cleanText(document.getElementById("objective-owner-company")?.value || selectedParentGoalContext?.primaryCompanyId || "");
    const scopedOptions = ownerCompanyId ? (workbook.positionOptionsForCompany?.(ownerCompanyId) || []) : [];
    const options = scopedOptions.length ? scopedOptions : (workbook.positionOptions?.() || []);
    const ownerKey = normalizeTemplateTextKey(owner);
    const match = options.find((option) => {
      const valueKey = normalizeTemplateTextKey(option?.value || "");
      const labelKey = normalizeTemplateTextKey(option?.label || "");
      return ownerKey && (ownerKey === valueKey || ownerKey === labelKey);
    });
    if (!match) return null;
    return {
      value: cleanText(match.value || ""),
      label: cleanText(match.label || match.value || "")
    };
  }

  async function syncObjectiveTemplateOwnerSuggestion() {
    if (!objectiveUsesTemplateCatalog() || !objectiveSourceTemplateId || !selectedObjectiveSourceMeta?.owner) return;
    const ownerCompanyId = cleanText(document.getElementById("objective-owner-company")?.value || "");
    if (!ownerCompanyId) {
      selectedObjectiveSourceMeta = {
        ...selectedObjectiveSourceMeta,
        ownerResolutionNote: "Template Owner will be matched to Owner Position after Owner Company / Org is known."
      };
      updateObjectiveSourceSummary();
      return;
    }
    const resolved = await resolveObjectiveTemplateOwnerPosition(selectedObjectiveSourceMeta.owner);
    if (!resolved?.value) {
      selectedObjectiveSourceMeta = {
        ...selectedObjectiveSourceMeta,
        ownerResolutionNote: `Template Owner '${selectedObjectiveSourceMeta.owner}' could not be resolved to a valid Owner Position in the current company / org context.`
      };
      updateObjectiveSourceSummary();
      return;
    }
    withSuppressedOverrideTracking(() => {
      setObjectivePrefillField("objective-owner-position", resolved.value, resolved.label);
    });
    selectedObjectiveSourceMeta = {
      ...selectedObjectiveSourceMeta,
      ownerPositionId: resolved.value,
      ownerPositionLabel: resolved.label,
      ownerResolutionNote: `Template Owner resolved to Owner Position ${resolved.label}.`
    };
    syncObjectiveCurrentOwnerPerson();
    updateObjectiveSourceSummary();
  }

  const updateObjectiveSourceSummary = () => {
    const host = document.getElementById("objective-source-summary");
    if (!host) return;
    if (!objectiveUsesTemplateCatalog()) {
      host.className = "goal-source-summary-card is-empty";
      host.innerHTML = `<div class="goal-source-summary-name">Blank Objective create</div><div class="goal-source-summary-note">Blank starts without template defaults, but the Objective still must be anchored to a Parent Goal before progression or save.</div>`;
      return;
    }
    if (!objectiveSourceTemplateId) {
      host.className = "goal-source-summary-card is-empty";
      host.innerHTML = `<div class="goal-source-summary-name">No Objective Template selected</div><div class="goal-source-summary-note">${selectedGoalIdFromForm() ? "Browse the catalog to pick a compatible Objective Template. Name, statement, type, and priority prefill safely while Parent Goal inheritance stays authoritative." : "Select Parent Goal first to load compatible Objective templates."}</div>`;
      return;
    }
    const meta = selectedObjectiveSourceMeta || {};
    const compatibility = currentObjectiveTemplateCompatibility(meta);
    const type = cleanText(meta.type || "");
    const priority = cleanText(meta.priority || "");
    const status = cleanText(meta.status || "");
    const horizon = objectiveTemplateHorizonHint(meta.timeHorizonStart, meta.timeHorizonEnd);
    const detailLines = [
      `Template ID: <code>${escapeHtml(objectiveSourceTemplateId)}</code>`,
      meta.parentGoalTemplateId ? `Parent Goal Template ID: <code>${escapeHtml(meta.parentGoalTemplateId)}</code>` : "",
      type ? `Type: ${escapeHtml(type)}` : "",
      priority ? `Priority: ${escapeHtml(priority)}` : "",
      meta.owner ? `Owner source: ${escapeHtml(meta.owner)}` : "",
      meta.entityScope ? `Entity scope hint: ${escapeHtml(meta.entityScope)}` : "",
      horizon ? `Template horizon hint: ${escapeHtml(horizon)}` : "",
      status ? `Source status: ${escapeHtml(status)}` : "",
      meta.ownerResolutionNote ? escapeHtml(meta.ownerResolutionNote) : "",
      compatibility.message ? escapeHtml(compatibility.message) : "",
      meta.dependencyNotes ? `Dependency note: ${escapeHtml(meta.dependencyNotes)}` : ""
    ].filter(Boolean);
    host.className = `goal-source-summary-card${compatibility.state === "mismatch" ? " border border-warning" : ""}`;
    host.innerHTML =
      `<div class="goal-source-summary-meta">` +
      `<span class="badge bg-label-primary">${escapeHtml(objectiveCreationModeLabel())}</span>` +
      `<span class="badge bg-label-secondary">Source Type: Objective Template</span>` +
      (meta.version != null ? `<span class="badge bg-label-secondary">Version ${escapeHtml(meta.version)}</span>` : "") +
      (type ? `<span class="badge bg-label-secondary">${escapeHtml(type)}</span>` : "") +
      (status ? `<span class="badge ${compatibility.state === "mismatch" ? "bg-label-warning" : "bg-label-secondary"}">${escapeHtml(status)}</span>` : "") +
      `</div>` +
      `<div class="goal-source-summary-name">${escapeHtml(meta.name || objectiveSourceTemplateId)}</div>` +
      `<div class="goal-source-summary-note">${detailLines.join("<br />")}</div>`;
  };

  const fillObjectiveSourceTypeFilter = (rows) => {
    const el = document.getElementById("objective-source-picker-type");
    if (!el) return;
    const previous = cleanText(el.value);
    const values = [...new Set((rows || []).map((row) => cleanText(row?.type || "")).filter(Boolean))];
    el.innerHTML = `<option value="">All types</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("")}`;
    if (previous && values.includes(previous)) el.value = previous;
  };

  const fillObjectiveSourceEntityScopeFilter = (rows) => {
    const el = document.getElementById("objective-source-picker-entity-scope");
    if (!el) return;
    const previous = cleanText(el.value);
    const values = [...new Set((rows || []).map((row) => cleanText(row?.entityScope || "")).filter(Boolean))];
    el.innerHTML = `<option value="">All entity scopes</option>${values.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("")}`;
    if (previous && values.includes(previous)) el.value = previous;
  };

  const renderObjectiveSourcePickerRows = (rows, emptyMessage = "No matching Objective Templates found.") => {
    const tbody = document.getElementById("objective-source-picker-tbody");
    if (!tbody) return;
    tbody.innerHTML = "";
    if (!(rows || []).length) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="10" class="text-center text-muted py-3">${escapeHtml(emptyMessage)}</td>`;
      tbody.appendChild(tr);
      return;
    }
    (rows || []).forEach((row) => {
      const compatibility = currentObjectiveTemplateCompatibility(row);
      const tr = document.createElement("tr");
      tr.classList.add("objective-template-picker-row");
      if (compatibility.state === "compatible") tr.classList.add("table-active");
      tr.innerHTML = [
        `<td>${escapeHtml(row.id || "-")}</td>`,
        `<td>${escapeHtml(row.parentGoalTemplateId || "-")}</td>`,
        `<td>${escapeHtml(row.name || "-")}</td>`,
        `<td>${escapeHtml(row.statement || "-")}</td>`,
        `<td>${escapeHtml(row.type || "-")}</td>`,
        `<td>${escapeHtml(row.owner || "-")}</td>`,
        `<td>${escapeHtml(row.priority || "-")}</td>`,
        `<td>${escapeHtml(row.entityScope || "-")}</td>`,
        `<td>${escapeHtml(row.status || "-")}</td>`,
        `<td class="text-end"><button type="button" class="btn btn-sm btn-outline-primary objective-pick-source"${row.id ? "" : " disabled"}>Use</button></td>`
      ].join("");
      tr.querySelector(".objective-pick-source")?.addEventListener("click", async () => {
        try {
          await applyObjectiveTemplateDetail(row.id, row);
          objectiveSourcePickerModal?.hide();
        } catch (err) {
          notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Load failed") || "Load failed", "error");
        }
      });
      tbody.appendChild(tr);
    });
  };

  const applyObjectiveSourcePickerFilters = () => {
    const q = cleanText(document.getElementById("objective-source-picker-search")?.value || "").toLowerCase();
    const selectedType = cleanText(document.getElementById("objective-source-picker-type")?.value || "").toLowerCase();
    const selectedEntityScope = cleanText(document.getElementById("objective-source-picker-entity-scope")?.value || "").toLowerCase();
    const parentGoalTemplateId = currentParentGoalTemplateId().toLowerCase();
    const rows = objectivePickerCatalogRows
      .filter((row) => {
        const haystack = `${row.id} ${row.name} ${row.statement} ${row.type} ${row.owner} ${row.priority} ${row.entityScope} ${row.status} ${row.parentGoalTemplateId}`.toLowerCase();
        if (q && !haystack.includes(q)) return false;
        if (selectedType && cleanText(row.type).toLowerCase() !== selectedType) return false;
        if (selectedEntityScope && cleanText(row.entityScope).toLowerCase() !== selectedEntityScope) return false;
        if (!objectiveTemplateRowMatchesCurrentGoal(row)) return false;
        return true;
      })
      .sort((left, right) => {
        const leftCompatible = parentGoalTemplateId && cleanText(left.parentGoalTemplateId).toLowerCase() === parentGoalTemplateId ? 1 : 0;
        const rightCompatible = parentGoalTemplateId && cleanText(right.parentGoalTemplateId).toLowerCase() === parentGoalTemplateId ? 1 : 0;
        if (leftCompatible !== rightCompatible) return rightCompatible - leftCompatible;
        return cleanText(left.name).localeCompare(cleanText(right.name));
      });
    const emptyMessage = selectedGoalIdFromForm()
      ? "No active Objective Templates matched the selected Parent Goal context and current picker filters."
      : "Select Parent Goal first to load compatible Objective Templates.";
    renderObjectiveSourcePickerRows(rows, emptyMessage);
  };

  const loadObjectiveSourcePickerCatalog = async () => {
    const tbody = document.getElementById("objective-source-picker-tbody");
    if (!selectedGoalIdFromForm()) {
      objectivePickerCatalogRows = [];
      fillObjectiveSourceTypeFilter([]);
      fillObjectiveSourceEntityScopeFilter([]);
      renderObjectiveSourcePickerRows([], "Select Parent Goal first to load compatible Objective Templates.");
      updateObjectiveSourcePickerParentHint("Select Parent Goal first to load compatible Objective templates.");
      syncObjectiveTemplateBrowseState();
      return;
    }
    if (tbody) tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted py-3">Loading Objective Templates...</td></tr>';
    const parentGoalTemplateId = currentParentGoalTemplateId();
    const parentGoalType = currentParentGoalType();
    updateObjectiveSourcePickerParentHint(parentGoalTemplateId
      ? `Loading active Objective Templates compatible with Parent Goal Template ${parentGoalTemplateId}.`
      : "Loading active Objective Templates compatible with the selected Parent Goal context.");
    try {
      const baseQuery = { page: 1, pageSize: 5000, templateType: "Objective", publishedOnly: true };
      if (parentGoalTemplateId) baseQuery.parentGoalTemplateId = parentGoalTemplateId;
      if (requiresExactObjectiveTemplateTypeMatch(parentGoalType)) baseQuery.categoryOrType = parentGoalType;
      const data = await window.strategyLibraryApi.catalog(baseQuery, { skipCache: true });
      const rows = normalizeObjectiveCatalogItems(data)
        .map(normalizeObjectiveCatalogRow)
        .filter(Boolean)
        .filter((row) => isObjectiveTemplateType(row));
      objectivePickerCatalogRows = rows;
      objectiveTemplateCatalogAvailable = true;
      fillObjectiveSourceTypeFilter(rows);
      fillObjectiveSourceEntityScopeFilter(rows);
      applyObjectiveSourcePickerFilters();
      updateObjectiveSourcePickerParentHint(parentGoalTemplateId
        ? `Showing active Objective Templates compatible with Parent Goal Template ${parentGoalTemplateId} and Parent Goal type ${selectedParentGoalContext?.goalType || "-"}.`
        : `Showing active Objective Templates compatible with Parent Goal type ${selectedParentGoalContext?.goalType || "-"}.`);
      syncObjectiveTemplateBrowseState();
    } catch (err) {
      objectivePickerCatalogRows = [];
      objectiveTemplateCatalogAvailable = false;
      fillObjectiveSourceTypeFilter([]);
      fillObjectiveSourceEntityScopeFilter([]);
      renderObjectiveSourcePickerRows([], window.enterpriseStrategyUi?.getErrorMessage(err, "Objective Template catalog is unavailable."));
      updateObjectiveSourcePickerParentHint("Objective Template catalog is unavailable right now.");
      syncObjectiveTemplateBrowseState();
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Catalog failed") || "Catalog failed", "error");
    }
  };

  const clearObjectiveTemplateSelection = ({ preserveUserEdits = true, updateMode = true } = {}) => {
    clearObjectiveTemplateAppliedFields({ preserveUserEdits });
    objectiveSourceTemplateId = "";
    objectiveSourceTemplateVersion = null;
    selectedObjectiveSourceMeta = null;
    if (updateMode) {
      objectiveCreationModeCode = "Blank";
      const creationModeEl = document.getElementById("objective-creation-mode-select");
      if (creationModeEl) creationModeEl.value = "Blank";
    }
    updateObjectiveCreationModeUi();
    syncObjectiveTemplateBrowseState();
    updateObjectiveSourceSummary();
    applyValidation();
  };

  async function applyObjectiveTemplateDetail(templateId, catalogRow = null, options = {}) {
    if (!templateId) return;
    const prefillFields = options.prefillFields !== false;
    const detail = await window.strategyLibraryApi.template(templateId);
    const attrs = detail?.attributes || detail?.Attributes || {};
    const prefill = detail?.objectivePrefill || detail?.ObjectivePrefill || null;
    objectiveCreationModeCode = objectiveUsesTemplateCatalog() ? objectiveCreationModeCode : "Template";
    objectiveSourceTemplateId = cleanText(templateId);
    objectiveSourceTemplateVersion = normalizeObjectiveSourceVersion(detail?.version ?? detail?.Version ?? catalogRow?.version);
    selectedObjectiveSourceMeta = {
      id: objectiveSourceTemplateId,
      name: cleanText(prefill?.name || detail?.name || catalogRow?.name || objectiveSourceTemplateId),
      parentGoalTemplateId: cleanText(prefill?.parentGoalTemplateId || attrs.ParentGoalTemplateId || catalogRow?.parentGoalTemplateId || ""),
      type: cleanText(prefill?.type || attrs.Type || catalogRow?.type || ""),
      owner: cleanText(prefill?.owner || detail?.owner || attrs.Owner || catalogRow?.owner || ""),
      priority: cleanText(prefill?.priority || detail?.priority || attrs.Priority || catalogRow?.priority || ""),
      entityScope: cleanText(prefill?.entityScope || detail?.entityScope || attrs.EntityScope || catalogRow?.entityScope || ""),
      status: cleanText(prefill?.lifecycleStatus || detail?.status || attrs.LifecycleStatus || attrs.Status || catalogRow?.status || ""),
      timeHorizonStart: parseDateIso(prefill?.timeHorizonStart || attrs.TimeHorizonStart || catalogRow?.timeHorizonStart || ""),
      timeHorizonEnd: parseDateIso(prefill?.timeHorizonEnd || attrs.TimeHorizonEnd || catalogRow?.timeHorizonEnd || ""),
      dependencyNotes: cleanText(prefill?.dependencyNotes || attrs.DependencyNotes || ""),
      decisionReference: cleanText(prefill?.decisionReference || attrs.DecisionReference || ""),
      evidenceReference: cleanText(prefill?.evidenceReference || attrs.EvidenceReference || ""),
      version: objectiveSourceTemplateVersion
    };
    const creationModeEl = document.getElementById("objective-creation-mode-select");
    if (creationModeEl) creationModeEl.value = objectiveCreationModeCode;
    if (prefillFields) {
      objectiveTemplateAppliedFields = new Map();
      withSuppressedOverrideTracking(() => {
        setObjectivePrefillField("objective-name", prefill?.name || detail?.name || "");
        setObjectivePrefillField("objective-statement", prefill?.statement || attrs.Statement || "");
        setObjectivePrefillField("objective-type", prefill?.type || attrs.Type || "", prefill?.type || attrs.Type || "");
        setObjectivePrefillField("objective-priority", prefill?.priority || detail?.priority || "", prefill?.priority || detail?.priority || "");
      });
    }
    applyObjectiveTypeFilterByGoal(selectedParentGoalContext?.goalType || "");
    await syncObjectiveTemplateOwnerSuggestion();
    updateObjectiveCreationModeUi();
    syncObjectiveTemplateBrowseState();
    updateObjectiveSourceSummary();
    markDirty();
  }

  const ensureSelectOption = (fieldId, value, label) => {
    const selectEl = document.getElementById(fieldId);
    const normalizedValue = String(value || "").trim();
    if (!selectEl || !normalizedValue) return;
    const existing = Array.from(selectEl.options || []).find((opt) => String(opt.value || "").trim() === normalizedValue);
    if (existing) {
      if (label) existing.textContent = label;
      return;
    }
    const option = document.createElement("option");
    option.value = normalizedValue;
    option.textContent = label || normalizedValue;
    selectEl.appendChild(option);
  };

  const goalLabel = (goalId) => {
    const goal = goalsCache.find((x) => String(x.id || "") === String(goalId || ""));
    return goal ? `${goal.id} - ${goal.name || goal.id}` : String(goalId || "");
  };

  const goalOwnerLabel = (goal) => {
    const role = cleanText(goal?.ownerRole);
    const person = resolveUserName(goal?.ownerPersonId || "");
    const company = companyLabelById(goal?.ownerCompanyId || goal?.primaryCompanyId || "");
    const base = role || person || "";
    if (base && company && company !== "-") return `${base} - ${company}`;
    return base || company || "";
  };

  const objectiveOwnerCompanyLabel = (value) => companyLabelById(value || "");

  const objectiveOwnerPositionLabel = (value) => cleanText(workbook.positionDisplayName?.(value) || value);

  const objectiveOwnerState = () => {
    const companyId = cleanText(document.getElementById("objective-owner-company")?.value || "");
    const positionId = cleanText(document.getElementById("objective-owner-position")?.value || "");
    const personId = cleanText(document.getElementById("objective-current-owner-person")?.value || "");
    const incumbents = companyId && positionId
      ? (workbook.usersForOwnershipContext?.(companyId, positionId, { activeOnly: true }) || [])
      : [];
    const validPeople = companyId && positionId
      ? (workbook.usersForOwnershipContext?.(companyId, positionId, { activeOnly: false }) || [])
      : [];
    return {
      companyId,
      positionId,
      personId,
      incumbents,
      incumbent: incumbents[0] || null,
      currentMatches: personId ? validPeople.some((user) => cleanText(user.id || user.value) === personId) : false,
      requiresNamedOwner: Boolean(positionId && incumbents.length)
    };
  };

  const syncObjectiveAccountabilitySummary = () => {
    const summaryEl = document.getElementById("objective-accountability-summary");
    if (!summaryEl) return;
    const state = objectiveOwnerState();
    summaryEl.value = [
      objectiveOwnerCompanyLabel(state.companyId) || "-",
      objectiveOwnerPositionLabel(state.positionId) || "-",
      cleanText(document.getElementById("objective-current-owner-person-display")?.value || "") || "-"
    ].join(" -> ");
  };

  const syncObjectiveOwnerPosition = () => {
    const legacyOwnerEl = document.getElementById("objective-owner");
    if (!legacyOwnerEl) return;
    const currentOwnerId = cleanText(document.getElementById("objective-current-owner-person")?.value || "");
    const currentOwnerName = cleanText(document.getElementById("objective-current-owner-person-display")?.value || "");
    if (!currentOwnerId) {
      legacyOwnerEl.value = "";
      return;
    }
    ensureSelectOption("objective-owner", currentOwnerId, currentOwnerName || resolveUserName(currentOwnerId) || currentOwnerId);
    legacyOwnerEl.value = currentOwnerId;
  };

  const setObjectiveOwnershipHelp = (fieldId, message) => {
    const el = document.getElementById(fieldId);
    if (el) el.textContent = message;
  };

  const syncObjectiveCurrentOwnerPerson = () => {
    const hiddenEl = document.getElementById("objective-current-owner-person");
    const displayEl = document.getElementById("objective-current-owner-person-display");
    if (!hiddenEl || !displayEl) return;
    const state = objectiveOwnerState();
    if (!state.companyId || !state.positionId) {
      hiddenEl.value = "";
      displayEl.value = "";
      setObjectiveOwnershipHelp("objective-current-owner-person-help", "Current Owner Person is resolved automatically from the active incumbent.");
      syncObjectiveAccountabilitySummary();
      syncObjectiveOwnerPosition();
      return;
    }
    if (state.incumbent) {
      hiddenEl.value = cleanText(state.incumbent.id || state.incumbent.value);
      displayEl.value = cleanText(state.incumbent.fullName || state.incumbent.label);
      setObjectiveOwnershipHelp("objective-current-owner-person-help", `Active incumbent resolved for ${objectiveOwnerPositionLabel(state.positionId)}.`);
    } else {
      hiddenEl.value = "";
      displayEl.value = "";
      setObjectiveOwnershipHelp("objective-current-owner-person-help", "No current incumbent found.");
    }
    syncObjectiveAccountabilitySummary();
    syncObjectiveOwnerPosition();
  };

  const refreshObjectiveOwnerPositionOptions = async () => {
    const companyId = cleanText(document.getElementById("objective-owner-company")?.value || "");
    const positionEl = document.getElementById("objective-owner-position");
    if (!positionEl) return;
    const current = cleanText(positionEl.value);
    if (!companyId) {
      workbook.fillSelect?.(positionEl, [], { placeholder: "Select owner company / org first", keepCurrent: false });
      positionEl.disabled = true;
      positionEl.value = "";
      setObjectiveOwnershipHelp("objective-owner-position-help", "Select Owner Company / Org to load valid positions.");
      syncObjectiveCurrentOwnerPerson();
      return;
    }
    let positionState = workbook.positionLoadState?.() || { status: "idle", error: "" };
    const hasLoadedOptions = (workbook.positionOptions?.() || []).length > 0;
    if ((!hasLoadedOptions || positionState.status === "idle" || positionState.status === "loading") && typeof workbook.ensurePositionsLoaded === "function") {
      positionEl.disabled = true;
      setObjectiveOwnershipHelp("objective-owner-position-help", "Loading available positions...");
      try {
        await workbook.ensurePositionsLoaded();
      } catch (_) {
      }
      positionState = workbook.positionLoadState?.() || positionState;
    }
    if (positionState.status === "error") {
      workbook.fillSelect?.(positionEl, [], { placeholder: "Position service unavailable", keepCurrent: false });
      positionEl.disabled = true;
      positionEl.value = "";
      setObjectiveOwnershipHelp("objective-owner-position-help", positionState.error || "Position data could not be loaded.");
      syncObjectiveCurrentOwnerPerson();
      return;
    }
    setObjectiveOwnershipHelp("objective-owner-position-help", "Loading available positions...");
    const scopedOptions = workbook.positionOptionsForCompany?.(companyId) || [];
    const options = scopedOptions.length ? scopedOptions : (workbook.positionOptions?.() || []);
    if (current && !options.some((option) => cleanText(option.value) === current)) {
      options.unshift({ value: current, label: objectiveOwnerPositionLabel(current) || current });
    }
    workbook.fillSelect?.(positionEl, options, { placeholder: options.length ? "Select owner position" : "No positions available", keepCurrent: false });
    positionEl.disabled = options.length === 0;
    if (current && options.some((option) => cleanText(option.value) === current)) {
      positionEl.value = current;
    } else {
      positionEl.value = "";
    }
    setObjectiveOwnershipHelp(
      "objective-owner-position-help",
      options.length
        ? (scopedOptions.length
          ? `Owner Position is filtered by ${objectiveOwnerCompanyLabel(companyId)}.`
          : `Showing API position list for ${objectiveOwnerCompanyLabel(companyId)}.`)
        : `No positions available from the Position API.`
    );
    syncObjectiveCurrentOwnerPerson();
  };

  const goalCompanyScopeLabel = (goal) => {
    if (goal?.appliesToAllCompanies) return "All Companies";
    const applicable = (goal?.applicableCompanyIds || []).map(companyLabelById).filter(Boolean);
    if (applicable.length) return applicable.join(", ");
    const primary = companyLabelById(goal?.ownerCompanyId || goal?.primaryCompanyId || "");
    return primary || "";
  };

  const goalStrategyPeriodLabel = (goal) => {
    const periodId = cleanText(goal?.strategyPeriodId);
    const period = strategyPeriodsById.get(periodId) || null;
    return strategyPeriodDisplayLabel({
      strategyPeriodCode: period?.code || goal?.strategyPeriodCode || periodId,
      strategyPeriodName: period?.name || goal?.strategyPeriodName || "Strategy Period",
      strategyPeriodStartDate: period?.startDate || goal?.startDate || goal?.planningHorizonStart || "",
      strategyPeriodEndDate: period?.endDate || goal?.endDate || goal?.planningHorizonEnd || ""
    });
  };

  const labelOrDash = (value) => cleanText(value) || "-";

  const parentGoalMetricGranularity = (metric) => {
    const rows = Array.isArray(metric?.yearlyValues) ? metric.yearlyValues : [];
    if (!rows.length) return "";
    return "Yearly";
  };

  const parentGoalMetricTargetSummary = (metric) => {
    const rows = Array.isArray(metric?.yearlyValues) ? metric.yearlyValues : [];
    if (!rows.length) return "No Parent Goal target rows are available yet.";
    const populatedTargets = rows.filter((row) => row.targetValue !== null);
    if (!populatedTargets.length) return `${rows.length} Goal target row(s) exist, but target values are still empty.`;
    const first = populatedTargets[0];
    const last = populatedTargets[populatedTargets.length - 1];
    return `${rows.length} Goal target row(s). First target ${displayNumericValue(first.targetValue)} in ${first.periodLabel || first.periodKey || first.year}; last target ${displayNumericValue(last.targetValue)} in ${last.periodLabel || last.periodKey || last.year}.`;
  };

  const goalHorizonLabel = (goal = selectedParentGoalContext) => {
    const start = formatIsoForDisplay(goal?.goalStartDate || "");
    const end = formatIsoForDisplay(goal?.goalEndDate || "");
    return start || end ? `${start || "-"} - ${end || "-"}` : "-";
  };

  const goalPeriodSummaryLabel = (goal = selectedParentGoalContext) => {
    const period = goalStrategyPeriodLabel({
      strategyPeriodId: goal?.strategyPeriodId || "",
      strategyPeriodCode: goal?.strategyPeriodCode || "",
      strategyPeriodName: goal?.strategyPeriodName || "",
      startDate: goal?.strategyPeriodStartDate || "",
      endDate: goal?.strategyPeriodEndDate || ""
    });
    return period || "-";
  };

  const goalThemeTypeSummaryLabel = (goal = selectedParentGoalContext) => {
    const theme = cleanText(goal?.strategicThemeId || "");
    const goalType = cleanText(goal?.goalType || "");
    return [theme, goalType].filter(Boolean).join(" | ") || "-";
  };

  const goalScopeDefaultsSummaryLabel = (goal = selectedParentGoalContext) => {
    if (!goal) return "-";
    const primary = companyLabelById(goal?.primaryCompanyId || "");
    const applicable = (goal?.applicableCompanyIds || []).map(companyLabelById).filter(Boolean);
    const businessUnit = cleanText(goal?.businessUnitId || "");
    const region = cleanText(goal?.regionId || "");
    const parts = [];
    if (primary) parts.push(`Primary: ${primary}`);
    if (applicable.length) parts.push(`Applicable: ${applicable.join(", ")}`);
    if (businessUnit) parts.push(`BU: ${businessUnit}`);
    if (region) parts.push(`Region: ${region}`);
    if (cleanText(goal?.entityScope)) parts.push(goal.entityScope);
    return parts.join(" | ") || "-";
  };

  const goalKpiGovernanceDefaultsLabel = (goal = selectedParentGoalContext) => {
    const metrics = Array.isArray(goal?.metrics) ? goal.metrics : [];
    const metricName = cleanText(metrics[0]?.metricName || metrics[0]?.metricDefinitionId || "");
    const metricCount = metrics.length;
    const status = cleanText(goal?.status || "");
    const parts = [];
    if (metricName) parts.push(`Primary KPI: ${metricName}`);
    if (metricCount > 1) parts.push(`${metricCount} linked KPIs`);
    if (status) parts.push(`Goal status: ${status}`);
    return parts.join(" | ") || "No KPI defaults available";
  };

  const renderParentGoalInheritedContext = () => {
    const host = document.getElementById("objective-parent-goal-context");
    const helperEl = document.getElementById("objective-parent-goal-context-helper");
    const periodEl = document.getElementById("objective-parent-context-period");
    const horizonEl = document.getElementById("objective-parent-context-horizon");
    const typeEl = document.getElementById("objective-parent-context-type");
    const scopeEl = document.getElementById("objective-parent-context-scope");
    const kpiEl = document.getElementById("objective-parent-context-kpi");
    if (!host) return;
    const hasParent = Boolean(selectedParentGoalContext?.goalId && selectedGoalIdFromForm());
    host.classList.toggle("is-empty", !hasParent);
    if (!hasParent) {
      if (helperEl) helperEl.textContent = "Select Parent Goal first to load inherited Strategy Period, horizon, theme, scope defaults, and available Goal context.";
      if (periodEl) periodEl.value = "-";
      if (horizonEl) horizonEl.value = "-";
      if (typeEl) typeEl.value = "-";
      if (scopeEl) scopeEl.value = "-";
      if (kpiEl) kpiEl.value = "-";
      return;
    }
    const goalName = cleanText(selectedParentGoalContext.goalName || "");
    const goalId = cleanText(selectedParentGoalContext.goalId || "");
    if (helperEl) {
      helperEl.textContent = `Parent Goal anchors Strategy Period, theme, horizon, and scope defaults for this Objective.${goalId ? ` Current anchor: ${goalId}${goalName ? ` - ${goalName}` : ""}.` : ""}`;
    }
    if (periodEl) periodEl.value = goalPeriodSummaryLabel(selectedParentGoalContext);
    if (horizonEl) horizonEl.value = goalHorizonLabel(selectedParentGoalContext);
    if (typeEl) typeEl.value = goalThemeTypeSummaryLabel(selectedParentGoalContext);
    if (scopeEl) scopeEl.value = goalScopeDefaultsSummaryLabel(selectedParentGoalContext);
    if (kpiEl) kpiEl.value = goalKpiGovernanceDefaultsLabel(selectedParentGoalContext);
  };

  const currentObjectivePerformanceContext = () => ({
    metricId: cleanText(document.getElementById("objective-primary-kpi")?.value || ""),
    unit: cleanText(document.getElementById("objective-kpi-uom")?.value || ""),
    direction: cleanText(document.getElementById("objective-direction")?.value || ""),
    reportingFrequency: cleanText(document.getElementById("objective-reporting-frequency")?.value || ""),
    granularity: currentObjectiveTargetPlanGranularity()
  });

  const goalObjectiveAlignmentWarnings = (payload = collectPayload()) => {
    const warnings = [];
    const goalMetric = currentParentGoalMetric();
    if (!selectedParentGoalContext || !goalMetric) return warnings;
    const goalGranularity = parentGoalMetricGranularity(goalMetric) || "";
    const objectiveGranularity = currentObjectiveTargetPlanGranularity();
    const goalGranularityRank = planningCadenceRank(goalGranularity);
    const objectiveGranularityRank = planningCadenceRank(objectiveGranularity);
    const goalReporting = normalizeReportingFrequency(goalMetric.reportingFrequency || "");
    const objectiveReporting = normalizeReportingFrequency(payload.reportingFrequency || document.getElementById("objective-reporting-frequency")?.value || "");
    const goalUnit = cleanText(goalMetric.unitOfMeasure || "");
    const objectiveUnit = cleanText(payload.unitOfMeasure || document.getElementById("objective-kpi-uom")?.value || "");
    const goalDirection = cleanText(goalMetric.directionPolarity || "");
    const objectiveDirection = cleanText(payload.directionOfPerformance || document.getElementById("objective-direction")?.value || "");

    if (goalGranularityRank > 0 && objectiveGranularityRank > goalGranularityRank) {
      warnings.push("Objective target cadence is more granular than the Parent Goal target cadence.");
    }
    if (goalReporting && objectiveReporting && goalReporting !== objectiveReporting) {
      warnings.push(`Objective reporting cadence (${objectiveReporting}) differs from the Parent Goal reporting cadence (${goalReporting}).`);
    }
    if (goalUnit && objectiveUnit && goalUnit !== objectiveUnit) {
      warnings.push(`Objective KPI unit (${objectiveUnit}) differs from the Parent Goal unit (${goalUnit}).`);
    }
    if (goalDirection && objectiveDirection && goalDirection !== objectiveDirection) {
      warnings.push(`Objective KPI direction (${objectiveDirection}) differs from the Parent Goal direction (${goalDirection}).`);
    }
    return warnings;
  };

  const renderParentGoalKpiContext = () => {
    if (!objectiveParentGoalKpiContextFieldsEl && !objectiveKpiAlignmentContextEl) return;
    const parentGoalId = selectedGoalIdFromForm();
    const parentGoalName = selectedParentGoalContext?.goalName || goalsCache.find((goal) => goal.id === parentGoalId)?.name || "";
    const goalMetric = currentParentGoalMetric();
    const objectiveContext = currentObjectivePerformanceContext();
    const goalMetricName = cleanText(goalMetric?.metricName || goalMetric?.metricDefinitionId || "");
    const goalMetricRows = Array.isArray(goalMetric?.yearlyValues) ? goalMetric.yearlyValues : [];
    const goalMetricSummary = parentGoalMetricTargetSummary(goalMetric);

    if (objectiveParentGoalKpiContextFieldsEl) {
      if (!selectedParentGoalContext || !parentGoalId) {
        objectiveParentGoalKpiContextFieldsEl.innerHTML = "Select a Parent Goal to load read-only Goal KPI context.";
      } else {
        objectiveParentGoalKpiContextFieldsEl.innerHTML = `
          <div><strong>Parent Goal</strong>: ${escapeHtml(parentGoalId)}${parentGoalName ? ` - ${escapeHtml(parentGoalName)}` : ""}</div>
          <div><strong>Parent Goal KPI / Metric</strong>: ${escapeHtml(labelOrDash(goalMetricName))}</div>
          <div><strong>Unit of Measure</strong>: ${escapeHtml(labelOrDash(goalMetric?.unitOfMeasure))} <span class="badge bg-label-info ms-1">Goal Source</span></div>
          <div><strong>Direction of Good Performance</strong>: ${escapeHtml(labelOrDash(goalMetric?.directionPolarity))} <span class="badge bg-label-info ms-1">Goal Source</span></div>
          <div><strong>Reporting Frequency</strong>: ${escapeHtml(labelOrDash(goalMetric?.reportingFrequency))} <span class="badge bg-label-info ms-1">Goal Source</span></div>
          <div><strong>Strategy Period / Horizon</strong>: ${escapeHtml(goalPeriodSummaryLabel(selectedParentGoalContext))} | ${escapeHtml(goalHorizonLabel(selectedParentGoalContext))}</div>
          <div><strong>Goal Target Granularity</strong>: ${escapeHtml(labelOrDash(parentGoalMetricGranularity(goalMetric) || "Not yet defined"))}</div>
          <div><strong>Goal Target Summary</strong>: ${escapeHtml(goalMetricSummary)}</div>
          <div><strong>Threshold Model</strong>: ${escapeHtml(labelOrDash(goalMetric?.thresholdModel))}${goalMetricRows.length ? ` | <strong>Target Row Count</strong>: ${escapeHtml(String(goalMetricRows.length))}` : ""}</div>
        `;
      }
    }

    if (objectiveKpiAlignmentContextEl) {
      if (!selectedParentGoalContext || !parentGoalId) {
        objectiveKpiAlignmentContextEl.innerHTML = "Goal vs Objective KPI comparison appears here once a Parent Goal is selected.";
      } else {
        const objectiveMetricLabel = labelOrDash(objectiveContext.metricId);
        const sameMetric = Boolean(goalMetric && objectiveContext.metricId && [
          goalMetric.id,
          goalMetric.metricDefinitionId,
          goalMetric.metricName
        ].map(cleanText).filter(Boolean).some((candidate) => candidate.localeCompare(objectiveContext.metricId, undefined, { sensitivity: "accent" }) === 0));
        objectiveKpiAlignmentContextEl.innerHTML = `
          <div><strong>Goal KPI</strong>: ${escapeHtml(labelOrDash(goalMetricName))} <span class="badge bg-label-info ms-1">Inherited</span></div>
          <div><strong>Objective KPI</strong>: ${escapeHtml(objectiveMetricLabel)} <span class="badge bg-label-warning ms-1">Objective Local</span></div>
          <div><strong>Relationship</strong>: ${escapeHtml(sameMetric ? "Objective KPI matches Parent Goal KPI." : "Objective KPI differs from Parent Goal KPI and should be justified.")}</div>
          <div><strong>Goal Unit / Objective Unit</strong>: ${escapeHtml(labelOrDash(goalMetric?.unitOfMeasure))} / ${escapeHtml(labelOrDash(objectiveContext.unit))}</div>
          <div><strong>Goal Direction / Objective Direction</strong>: ${escapeHtml(labelOrDash(goalMetric?.directionPolarity))} / ${escapeHtml(labelOrDash(objectiveContext.direction))}</div>
        `;
      }
    }
  };

  const renderParentGoalTargetContext = () => {
    if (!objectiveParentGoalTargetContextFieldsEl && !objectiveTargetPlanComparisonEl && !objectiveGoalTargetReferenceBodyEl) return;
    const goalMetric = currentParentGoalMetric();
    const goalRows = Array.isArray(goalMetric?.yearlyValues) ? goalMetric.yearlyValues : [];
    const objectiveContext = currentObjectivePerformanceContext();
    const goalGranularity = parentGoalMetricGranularity(goalMetric) || "Not yet defined";
    const warnings = goalObjectiveAlignmentWarnings();

    if (objectiveParentGoalTargetContextFieldsEl) {
      if (!selectedParentGoalContext) {
        objectiveParentGoalTargetContextFieldsEl.innerHTML = "Select a Parent Goal to review Goal target context and allowed planning horizon.";
      } else {
        objectiveParentGoalTargetContextFieldsEl.innerHTML = `
          <div><strong>Parent Goal Strategy Period</strong>: ${escapeHtml(goalPeriodSummaryLabel(selectedParentGoalContext))} <span class="badge bg-label-info ms-1">Inherited</span></div>
          <div><strong>Parent Goal Start / End</strong>: ${escapeHtml(goalHorizonLabel(selectedParentGoalContext))}</div>
          <div><strong>Goal Target Granularity</strong>: ${escapeHtml(goalGranularity)}</div>
          <div><strong>Goal Reporting Frequency</strong>: ${escapeHtml(labelOrDash(goalMetric?.reportingFrequency))}</div>
          <div><strong>Goal Target Values Summary</strong>: ${escapeHtml(parentGoalMetricTargetSummary(goalMetric))}</div>
          <div><strong>Allowed Objective Planning Window</strong>: ${escapeHtml(goalHorizonLabel(selectedParentGoalContext))}</div>
          <div><strong>Goal Target Row Count</strong>: ${escapeHtml(String(goalRows.length || 0))}</div>
          <div class="mt-2">Objective target plan should support the Parent Goal target trajectory. Goal target rows are shown as reference only.</div>
        `;
      }
    }

    if (objectiveTargetPlanComparisonEl) {
      if (!selectedParentGoalContext) {
        objectiveTargetPlanComparisonEl.innerHTML = "Goal and Objective cadence comparisons appear here once a Parent Goal is selected.";
      } else {
        objectiveTargetPlanComparisonEl.innerHTML = `
          <div><strong>Goal Target Plan Granularity</strong>: ${escapeHtml(goalGranularity)} | <strong>Objective Target Plan Granularity</strong>: ${escapeHtml(labelOrDash(objectiveContext.granularity))}</div>
          <div><strong>Goal Reporting Frequency</strong>: ${escapeHtml(labelOrDash(goalMetric?.reportingFrequency))} | <strong>Objective Reporting Frequency</strong>: ${escapeHtml(labelOrDash(objectiveContext.reportingFrequency))}</div>
          <div><strong>Goal KPI Direction</strong>: ${escapeHtml(labelOrDash(goalMetric?.directionPolarity))} | <strong>Objective KPI Direction</strong>: ${escapeHtml(labelOrDash(objectiveContext.direction))}</div>
          <div><strong>Goal Unit</strong>: ${escapeHtml(labelOrDash(goalMetric?.unitOfMeasure))} | <strong>Objective Unit</strong>: ${escapeHtml(labelOrDash(objectiveContext.unit))}</div>
          <div class="mt-2">${warnings.length ? warnings.map((warning) => `• ${escapeHtml(warning)}`).join("<br />") : "Goal and Objective KPI/target settings are currently aligned or acceptable for governance."}</div>
        `;
      }
    }

    if (objectiveGoalTargetReferenceBodyEl) {
      if (!goalRows.length) {
        objectiveGoalTargetReferenceBodyEl.innerHTML = '<tr><td colspan="6" class="text-muted">Parent Goal target rows are not available yet. Objective target rows remain editable below.</td></tr>';
      } else {
        objectiveGoalTargetReferenceBodyEl.innerHTML = goalRows.map((row) => `
          <tr>
            <td>${escapeHtml(row.periodLabel || row.periodKey || String(row.year || "-"))}</td>
            <td>${escapeHtml(formatIsoForDisplay(row.periodStart) || "-")}</td>
            <td>${escapeHtml(formatIsoForDisplay(row.periodEnd) || "-")}</td>
            <td>${escapeHtml(displayNumericValue(row.targetValue) || "-")}</td>
            <td>${escapeHtml(displayNumericValue(row.actualValue) || "-")}</td>
            <td>${escapeHtml(displayNumericValue(row.forecastValue) || "-")}</td>
          </tr>
        `).join("");
      }
    }
  };

  const parentGoalLookupReservedValues = new Set(["archive", "status", "goals"]);

  const isUsableParentGoalLookupRow = (goal) => {
    const id = cleanText(goal?.id);
    const name = cleanText(goal?.name);
    if (!id || !name) return false;
    if (parentGoalLookupReservedValues.has(id.toLowerCase())) return false;
    if (parentGoalLookupReservedValues.has(name.toLowerCase())) return false;
    return true;
  };

  const buildParentGoalOptions = () => goalsCache
    .filter(isUsableParentGoalLookupRow)
    .map((goal) => {
      const id = cleanText(goal.id);
      const name = cleanText(goal.name);
      const period = goalStrategyPeriodLabel(goal);
      const owner = goalOwnerLabel(goal);
      const companies = goalCompanyScopeLabel(goal);
      const parts = [
        name ? `${name} [${id}]` : id,
        period,
        owner,
        companies
      ].filter(Boolean);
      return {
        value: id,
        label: parts.join(" | ")
      };
    })
    .filter((option) => option.value && option.label);

  const metricSummaryText = (item) => {
    const metricId = String(item.primaryMetricId || item.primaryKpiMetric || "").trim();
    const metric = (item.metrics || [])[0] || {};
    const metricName = metric.metricName || metricId || "-";
    const unit = item.unitOfMeasure || metric.unitOfMeasure || "";
    const direction = item.directionOfPerformance || metric.direction || "";
    return [metricName, unit, direction].filter(Boolean).join(" | ");
  };

  const statusBadgeHtml = (status) => `<span class="badge bg-label-info">${escapeHtml(status || "-")}</span>`;

  const setTableDensity = (mode) => {
    const table = document.getElementById("objectives-table");
    if (!table) return;
    table.classList.toggle("table-sm", mode === "compact");
  };

  const updateBulkActionsState = () => {
    if (bulkActionsToggle) bulkActionsToggle.disabled = selectedObjectiveIds.size === 0;
  };

  const getSelectedItems = () => objectivesCache.filter((item) => selectedObjectiveIds.has(String(item.id || "")));

  const clearSelection = ({ rerender = true } = {}) => {
    selectedObjectiveIds.clear();
    updateBulkActionsState();
    if (rerender && tableBody) renderFiltered(false);
  };

  let tableControls = null;
  if (tableBody) {
    try {
      tableControls = window.enterpriseTableControls?.create({
        pageKey: "objectives",
        storageKey: "objectivesTableLayout",
        columnsButtonId: "objective-columns-btn",
        columns: [
          { key: "id", label: "Objective ID", defaultVisible: false },
          { key: "name", label: "Objective", defaultVisible: true },
          { key: "parentGoalId", label: "Parent Goal", defaultVisible: true },
          { key: "owner", label: "Owner", defaultVisible: true },
          { key: "status", label: "Status", defaultVisible: true },
          { key: "type", label: "Type", defaultVisible: true },
          { key: "priority", label: "Priority", defaultVisible: true },
          { key: "startYear", label: "Start Year", defaultVisible: true },
          { key: "endYear", label: "End Year", defaultVisible: true },
          { key: "metricSummary", label: "KPI Summary", defaultVisible: true },
          { key: "inheritCompanyScope", label: "Scope Mode", defaultVisible: false },
          { key: "primaryCompanyId", label: "Primary Company", defaultVisible: false },
          { key: "applicableCompanyIds", label: "Applicable Companies", defaultVisible: false },
          { key: "entityScope", label: "Entity Scope", defaultVisible: false },
          { key: "actions", label: "Actions", defaultVisible: true }
        ],
        onChange: () => renderFiltered()
      }) || null;
    } catch (err) {
      console.error("objectives table controls init failed", err);
    }
  }

  const pager = tableBody ? window.enterpriseTablePageUtils?.createPager?.({
    pageKey: "objectivesTable",
    tableEl: document.getElementById("objectives-table"),
    tableControls,
    defaultPageSize: 25,
    onChange: () => renderFiltered(false)
  }) : null;

  const withSuppressedOverrideTracking = (fn) => {
    suppressOverrideTracking = true;
    try {
      fn();
    } finally {
      suppressOverrideTracking = false;
    }
  };

  const withSuppressedDirtyTracking = async (fn) => {
    suppressDirtyTracking = true;
    try {
      return await fn();
    } finally {
      suppressDirtyTracking = false;
    }
  };

  const trackUserOverride = (fieldId) => {
    if (suppressOverrideTracking || isEditMode) return;
    userOverrides.add(fieldId);
  };

  const ensureObjectivePlanningOption = (periodId, label) => {
    const selectEl = document.getElementById("objective-planning-cycle");
    if (!selectEl || !periodId) return;
    const normalizedId = String(periodId).trim();
    const existing = Array.from(selectEl.options || []).find((opt) => String(opt.value || "").trim() === normalizedId);
    if (existing) {
      existing.textContent = label || existing.textContent;
      return;
    }
    const option = document.createElement("option");
    option.value = normalizedId;
    option.textContent = label || normalizedId;
    selectEl.appendChild(option);
  };

  const strategyPeriodDisplayLabel = (period) => {
    const code = String(period?.strategyPeriodCode || period?.code || "").trim();
    const name = String(period?.strategyPeriodName || period?.name || "").trim() || "Strategy Period";
    const startYear = fromDateToYear(period?.strategyPeriodStartDate || period?.startDate || "");
    const endYear = fromDateToYear(period?.strategyPeriodEndDate || period?.endDate || "");
    const range = startYear && endYear ? `${startYear}-${endYear}` : "Year envelope unavailable";
    const title = [code, name].filter(Boolean).join(" - ");
    return `${title || "Inherited Strategy Period"} | ${range}`;
  };

  const deriveAllowedHorizonBounds = (parentContext, planningContext) => {
    const goalStart = parseDateIso(parentContext?.goalStartDate || parentContext?.startDate || parentContext?.planningHorizonStart || "");
    const goalEnd = parseDateIso(parentContext?.goalEndDate || parentContext?.endDate || parentContext?.planningHorizonEnd || "");
    const periodStart = parseDateIso(planningContext?.strategyPeriodStartDate || planningContext?.startDate || parentContext?.strategyPeriodStartDate || "");
    const periodEnd = parseDateIso(planningContext?.strategyPeriodEndDate || planningContext?.endDate || parentContext?.strategyPeriodEndDate || "");
    const allowedStart = goalStart;
    const allowedEnd = goalEnd;
    return {
      goalStart,
      goalEnd,
      periodStart,
      periodEnd,
      allowedStart,
      allowedEnd,
      hasAllowedBounds: Boolean(allowedStart && allowedEnd && allowedEnd >= allowedStart)
    };
  };

  function getObjectiveApplicableCompaniesElements() {
    return {
      select: document.getElementById("objective-applicable-companies"),
      root: document.getElementById("objective-applicable-companies-picker"),
      toggle: document.getElementById("objective-applicable-companies-toggle"),
      display: document.getElementById("objective-applicable-companies-display"),
      panel: document.getElementById("objective-applicable-companies-panel"),
      search: document.getElementById("objective-applicable-companies-search"),
      options: document.getElementById("objective-applicable-companies-options"),
      selectAll: document.getElementById("objective-applicable-companies-select-all"),
      clearAll: document.getElementById("objective-applicable-companies-clear-all")
    };
  }

  function objectiveApplicableCompaniesPlaceholder() {
    const { root } = getObjectiveApplicableCompaniesElements();
    return String(root?.dataset?.placeholder || "Select applicable companies...").trim();
  }

  function isObjectiveApplicableCompaniesPanelOpen() {
    const { panel } = getObjectiveApplicableCompaniesElements();
    return Boolean(panel && !panel.classList.contains("d-none"));
  }

  function visibleObjectiveApplicableOptionButtons() {
    const { options } = getObjectiveApplicableCompaniesElements();
    return Array.from(options?.querySelectorAll(".es-company-multi-select-option") || []);
  }

  function setObjectiveApplicableCompaniesPanelOpen(open) {
    const { root, toggle, panel, search, options } = getObjectiveApplicableCompaniesElements();
    if (!root || !toggle || !panel) return;
    const allowOpen = open && !toggle.disabled;
    panel.classList.toggle("d-none", !allowOpen);
    toggle.classList.toggle("is-open", allowOpen);
    toggle.setAttribute("aria-expanded", allowOpen ? "true" : "false");
    if (!allowOpen) {
      objectiveApplicableCompaniesPickerActiveIndex = -1;
      return;
    }
    syncObjectiveApplicableCompaniesPickerFromSelect();
    if (search) {
      search.focus();
      search.select?.();
      return;
    }
    options?.focus();
  }

  function ensureObjectiveApplicableOptionInView(btn) {
    if (!btn) return;
    const { options } = getObjectiveApplicableCompaniesElements();
    if (!options) return;
    const top = options.scrollTop;
    const bottom = top + options.clientHeight;
    const itemTop = btn.offsetTop;
    const itemBottom = itemTop + btn.offsetHeight;
    if (itemTop < top) options.scrollTop = itemTop;
    if (itemBottom > bottom) options.scrollTop = itemBottom - options.clientHeight;
  }

  function setObjectiveApplicableActiveIndex(nextIndex) {
    const buttons = visibleObjectiveApplicableOptionButtons();
    if (!buttons.length) {
      objectiveApplicableCompaniesPickerActiveIndex = -1;
      return;
    }
    const bounded = Math.max(0, Math.min(nextIndex, buttons.length - 1));
    objectiveApplicableCompaniesPickerActiveIndex = bounded;
    buttons.forEach((btn, idx) => {
      const active = idx === bounded;
      btn.classList.toggle("is-active", active);
      if (active) ensureObjectiveApplicableOptionInView(btn);
    });
  }

  function applyObjectiveApplicableSelections(values, dispatchChange = true) {
    const { select } = getObjectiveApplicableCompaniesElements();
    if (!select) return;
    const selected = new Set((values || []).map((value) => String(value || "").trim()).filter(Boolean));
    let changed = false;
    Array.from(select.options || []).forEach((opt) => {
      const shouldSelect = selected.has(String(opt.value || "").trim());
      if (opt.selected !== shouldSelect) {
        opt.selected = shouldSelect;
        changed = true;
      }
    });
    if (dispatchChange && changed) {
      select.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    syncObjectiveApplicableCompaniesPickerFromSelect();
  }

  function toggleObjectiveApplicableCompanyValue(value) {
    const { select } = getObjectiveApplicableCompaniesElements();
    if (!select) return;
    const normalizedValue = String(value || "").trim();
    const option = Array.from(select.options || []).find((opt) => String(opt.value || "").trim() === normalizedValue);
    if (!option) return;
    option.selected = !option.selected;
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function syncObjectiveApplicableCompaniesDisplay() {
    const { select, display, toggle } = getObjectiveApplicableCompaniesElements();
    if (!select || !display) return;
    const names = Array.from(select.selectedOptions || []).map((opt) => String(opt.textContent || "").trim()).filter(Boolean);
    if (!names.length) {
      display.textContent = objectiveApplicableCompaniesPlaceholder();
      if (toggle) toggle.title = "";
      return;
    }
    if (names.length <= 2) {
      display.textContent = names.join(", ");
    } else {
      display.textContent = `${names.slice(0, 2).join(", ")} +${names.length - 2} more (${names.length} selected)`;
    }
    if (toggle) toggle.title = names.join(", ");
  }

  function renderObjectiveApplicableCompaniesOptions() {
    const { select, search, options } = getObjectiveApplicableCompaniesElements();
    if (!select || !options) return;
    const query = String(search?.value || "").trim().toLowerCase();
    const rows = Array.from(select.options || []).map((opt) => ({
      value: String(opt.value || "").trim(),
      label: String(opt.textContent || "").trim(),
      selected: Boolean(opt.selected)
    })).filter((row) => row.value && row.label && (!query || row.label.toLowerCase().includes(query)));
    options.innerHTML = "";
    if (!rows.length) {
      const empty = document.createElement("div");
      empty.className = "es-company-multi-select-empty";
      empty.textContent = "No matching companies.";
      options.appendChild(empty);
      objectiveApplicableCompaniesPickerActiveIndex = -1;
      return;
    }
    rows.forEach((row, idx) => {
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
    setObjectiveApplicableActiveIndex(0);
  }

  function syncObjectiveApplicableCompaniesPickerFromSelect() {
    const { select, toggle } = getObjectiveApplicableCompaniesElements();
    if (!select || !toggle) return;
    const disabled = Boolean(select.disabled);
    toggle.disabled = disabled;
    toggle.classList.toggle("disabled", disabled);
    if (disabled) setObjectiveApplicableCompaniesPanelOpen(false);
    syncObjectiveApplicableCompaniesDisplay();
    if (isObjectiveApplicableCompaniesPanelOpen()) renderObjectiveApplicableCompaniesOptions();
  }

  function onObjectiveApplicableCompaniesPickerKeyDown(event) {
    const { search, toggle } = getObjectiveApplicableCompaniesElements();
    const open = isObjectiveApplicableCompaniesPanelOpen();
    if (!open && (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      setObjectiveApplicableCompaniesPanelOpen(true);
      return;
    }
    if (!open) return;
    const buttons = visibleObjectiveApplicableOptionButtons();
    if (!buttons.length) {
      if (event.key === "Escape") {
        event.preventDefault();
        setObjectiveApplicableCompaniesPanelOpen(false);
        toggle?.focus();
      }
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setObjectiveApplicableActiveIndex(objectiveApplicableCompaniesPickerActiveIndex + 1);
      return;
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      setObjectiveApplicableActiveIndex(objectiveApplicableCompaniesPickerActiveIndex - 1);
      return;
    }
    if (event.key === "Home") {
      event.preventDefault();
      setObjectiveApplicableActiveIndex(0);
      return;
    }
    if (event.key === "End") {
      event.preventDefault();
      setObjectiveApplicableActiveIndex(buttons.length - 1);
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      if (event.target === search && event.key === " ") return;
      event.preventDefault();
      const activeBtn = buttons[objectiveApplicableCompaniesPickerActiveIndex] || buttons[0];
      const value = String(activeBtn?.dataset?.companyValue || "").trim();
      if (value) toggleObjectiveApplicableCompanyValue(value);
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      setObjectiveApplicableCompaniesPanelOpen(false);
      toggle?.focus();
    }
  }

  function initObjectiveApplicableCompaniesPicker() {
    const { select, root, toggle, panel, search, options, selectAll, clearAll } = getObjectiveApplicableCompaniesElements();
    if (!select || !root || !toggle || !panel || !options) return;
    if (root.dataset.initialized === "1") {
      syncObjectiveApplicableCompaniesPickerFromSelect();
      return;
    }
    root.dataset.initialized = "1";
    toggle.addEventListener("click", () => setObjectiveApplicableCompaniesPanelOpen(!isObjectiveApplicableCompaniesPanelOpen()));
    toggle.addEventListener("keydown", onObjectiveApplicableCompaniesPickerKeyDown);
    panel.addEventListener("keydown", onObjectiveApplicableCompaniesPickerKeyDown);
    search?.addEventListener("input", () => renderObjectiveApplicableCompaniesOptions());
    options.addEventListener("click", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      const value = String(btn?.dataset?.companyValue || "").trim();
      if (value) toggleObjectiveApplicableCompanyValue(value);
    });
    options.addEventListener("mousemove", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      if (!btn) return;
      const buttons = visibleObjectiveApplicableOptionButtons();
      const idx = buttons.indexOf(btn);
      if (idx >= 0) setObjectiveApplicableActiveIndex(idx);
    });
    selectAll?.addEventListener("click", () => {
      const values = Array.from(select.options || []).map((opt) => String(opt.value || "").trim()).filter(Boolean);
      applyObjectiveApplicableSelections(values, true);
      search?.focus();
    });
    clearAll?.addEventListener("click", () => {
      applyObjectiveApplicableSelections([], true);
      search?.focus();
    });
    select.addEventListener("change", () => syncObjectiveApplicableCompaniesPickerFromSelect());
    document.addEventListener("mousedown", (event) => {
      if (!isObjectiveApplicableCompaniesPanelOpen()) return;
      if (root.contains(event.target)) return;
      setObjectiveApplicableCompaniesPanelOpen(false);
    });
    syncObjectiveApplicableCompaniesPickerFromSelect();
  }

  const applyObjectivePlanningContextUi = () => {
    const helperEl = document.getElementById("objective-planning-context-helper");
    const allowedHelperEl = document.getElementById("objective-allowed-horizon-helper");
    const planningEl = document.getElementById("objective-planning-cycle");
    const startEl = document.getElementById("objective-horizon-start-date");
    const endEl = document.getElementById("objective-horizon-end-date");
    const parentGoalId = selectedGoalIdFromForm();
    const hasParent = Boolean(parentGoalId);
    const bounds = deriveAllowedHorizonBounds(selectedParentGoalContext, selectedGoalPlanningContext);
    const allowedStartIso = bounds.allowedStart || "";
    const allowedEndIso = bounds.allowedEnd || "";
    const hasBounds = Boolean(bounds.hasAllowedBounds && allowedStartIso && allowedEndIso && allowedEndIso >= allowedStartIso);

    if (planningEl) {
      planningEl.disabled = true;
      planningEl.closest(".col-12, .col-md-6, .col-md-8")?.classList.toggle("es-inherited-scope", hasParent);
    }
    if (startEl) {
      setObjectiveHorizonBounds("objective-horizon-start-date", hasBounds, allowedStartIso, allowedEndIso);
      startEl.disabled = !hasParent;
      startEl.closest(".col-12, .col-md-6, .col-md-8")?.classList.toggle("es-inherited-scope", hasParent);
    }
    if (endEl) {
      setObjectiveHorizonBounds("objective-horizon-end-date", hasBounds, allowedStartIso, allowedEndIso);
      endEl.disabled = !hasParent;
      endEl.closest(".col-12, .col-md-6, .col-md-8")?.classList.toggle("es-inherited-scope", hasParent);
    }

    if (!hasParent) {
      if (helperEl) helperEl.textContent = "Strategy Period is inherited from Parent Goal.";
      if (allowedHelperEl) allowedHelperEl.textContent = "Allowed horizon: select Parent Goal first.";
      syncObjectiveTargetPlanSettingsUi();
      renderObjectiveTargetPlanTable();
      return;
    }

    if (helperEl) {
      helperEl.textContent = selectedGoalPlanningContext?.strategyPeriodId
        ? "Inherited from Parent Goal. Strategy Period cannot be edited here."
        : "Strategy Period is inherited from Parent Goal.";
    }
    if (allowedHelperEl) {
      allowedHelperEl.textContent = hasBounds
        ? `Inherited bounds: ${formatIsoForDisplay(allowedStartIso)} - ${formatIsoForDisplay(allowedEndIso)}. Narrowing is allowed inside this range only.`
        : "Inherited bounds are unavailable from Parent Goal.";
    }
    if (hasBounds) {
      const currentStartIso = objectiveHorizonIsoFromInput("objective-horizon-start-date");
      const currentEndIso = objectiveHorizonIsoFromInput("objective-horizon-end-date");
      if (!currentStartIso) setObjectiveHorizonInputFromRaw("objective-horizon-start-date", allowedStartIso, false);
      if (!currentEndIso) setObjectiveHorizonInputFromRaw("objective-horizon-end-date", allowedEndIso, true);
    }
    syncObjectiveTargetPlanSettingsUi();
    renderObjectiveTargetPlanTable();
  };

  const setInheritedFieldState = (fieldId, inherited) => {
    const el = document.getElementById(fieldId);
    if (!el) return;
    el.disabled = inherited;
    const host = el.closest(".col-12, .col-md-4, .col-md-6, .col-md-8");
    host?.classList.toggle("es-inherited-scope", inherited);
    if (window.jQuery && el.tagName === "SELECT" && window.jQuery(el).hasClass("select2-hidden-accessible")) {
      window.jQuery(el).trigger("change.select2");
    }
  };

  const syncThemeOverrideUi = () => {
    const hasParent = Boolean(selectedGoalIdFromForm());
    const overrideEnabled = document.getElementById("objective-theme-override")?.checked === true;
    const themeEl = document.getElementById("objective-strategic-theme");
    const overrideEl = document.getElementById("objective-theme-override");
    const helpEl = document.getElementById("objective-strategic-theme-help");
    const badge = document.getElementById("objective-strategic-theme-badge");
    if (badge) {
      const isInherited = hasParent && !overrideEnabled;
      badge.textContent = isInherited ? "Inherited" : (overrideEnabled ? "Override" : "Await Parent Goal");
      badge.classList.toggle("bg-label-info", isInherited);
      badge.classList.toggle("bg-label-warning", overrideEnabled);
      badge.classList.toggle("bg-label-secondary", !hasParent);
    }
    if (overrideEl) overrideEl.disabled = !hasParent;
    if (helpEl) {
      helpEl.textContent = !hasParent
        ? "Objective must be anchored to a Parent Goal first so Strategic Theme / Pillar can be inherited."
        : (overrideEnabled
          ? "Override is enabled. Use only when the objective intentionally diverges from the Parent Goal pillar."
          : "Strategic Theme / Pillar is inherited from Parent Goal by default.");
    }
    if (themeEl && !hasParent) {
      themeEl.disabled = true;
      themeEl.closest(".col-12, .col-md-4, .col-md-6, .col-md-8")?.classList.add("es-inherited-scope");
      if (window.jQuery && window.jQuery(themeEl).hasClass("select2-hidden-accessible")) {
        window.jQuery(themeEl).trigger("change.select2");
      }
      return;
    }
    setInheritedFieldState("objective-strategic-theme", hasParent && !overrideEnabled);
  };

  const updateEntityScopeSummary = () => {
    const summaryEl = document.getElementById("objective-entity-scope-summary");
    if (!summaryEl) return;
    const unlocked = document.getElementById("objective-inherit-company-scope")?.checked === true;
    if (!unlocked) {
      summaryEl.value = selectedParentGoalContext?.entityScope || "Inherited from parent goal";
      return;
    }
    const primary = document.getElementById("objective-primary-company")?.value || "";
    const applicable = selectedValues("objective-applicable-companies");
    const businessUnit = document.getElementById("objective-business-unit")?.value || "";
    const region = document.getElementById("objective-region")?.value || "";
    const parts = [];
    if (primary) parts.push(`Primary: ${companyLabelById(primary)}`);
    if (applicable.length) parts.push(`Applicable: ${applicable.map(companyLabelById).join(", ")}`);
    if (businessUnit) parts.push(`BU: ${businessUnit}`);
    if (region) parts.push(`Region: ${region}`);
    summaryEl.value = parts.join(" | ");
  };

  const syncCompanyOverrideUi = () => {
    const unlocked = document.getElementById("objective-inherit-company-scope")?.checked === true;
    const inheritedBadge = document.getElementById("objective-scope-inherited-badge");
    const readOnly = !unlocked;
    ["objective-primary-company", "objective-applicable-companies", "objective-business-unit", "objective-region"].forEach((fieldId) => {
      setInheritedFieldState(fieldId, readOnly);
    });
    if (inheritedBadge) {
      inheritedBadge.textContent = readOnly ? "Inherited / Locked" : "Override";
      inheritedBadge.classList.toggle("bg-label-info", readOnly);
      inheritedBadge.classList.toggle("bg-label-warning", !readOnly);
    }
    syncObjectiveApplicableCompaniesPickerFromSelect();
    updateEntityScopeSummary();
  };

  const refreshParentMetricSummary = () => {
    const summary = document.getElementById("objective-parent-metric-summary");
    if (!summary) {
      renderParentGoalKpiContext();
      renderParentGoalTargetContext();
      return;
    }
    const parentGoalId = selectedGoalIdFromForm();
    const parentGoalName = selectedParentGoalContext?.goalName || goalsCache.find((goal) => goal.id === parentGoalId)?.name || "";
    const parentLabel = parentGoalId ? `${parentGoalId}${parentGoalName ? ` - ${parentGoalName}` : ""}` : "-";
    const goalMetric = currentParentGoalMetric();
    const metricValue = document.getElementById("objective-primary-kpi")?.value || goalMetric?.metricName || "-";
    const unit = document.getElementById("objective-kpi-uom")?.value || goalMetric?.unitOfMeasure || "-";
    const direction = document.getElementById("objective-direction")?.value || goalMetric?.directionPolarity || "-";
    const frequency = document.getElementById("objective-reporting-frequency")?.value || goalMetric?.reportingFrequency || "-";
    summary.innerHTML = [
      '<span class="badge bg-label-primary">Goal Lineage</span>',
      `<span>Parent Goal ${escapeHtml(parentLabel)} | KPI ${escapeHtml(metricValue)} | Unit ${escapeHtml(unit)} | Direction ${escapeHtml(direction)} | Frequency ${escapeHtml(frequency)}</span>`
    ].join(" ");
    renderParentGoalKpiContext();
    renderParentGoalTargetContext();
    syncObjectiveTargetPlanSettingsUi();
  };

  const objectiveReadinessSnapshot = (payload = collectPayload()) => {
    const missing = [];
    const blockers = [];
    const warnings = [];
    const targetRows = objectiveTargetPlanRows.slice();
    const periods = deriveObjectivePlanPeriods();
    const planningRange = objectiveEffectivePlanningRange();
    const thresholdsRequired = objectiveThresholdsRequired();
    const ownerState = objectiveOwnerState();
    const fieldMap = fieldErrorMap(payload, { includeTargetPlan: false });

    if (!payload.parentGoalId) missing.push("Parent Goal is required.");
    if (!payload.planningCycle) missing.push("Strategy Period is required.");
    if (!planningRange.strategyPeriodStartIso || !planningRange.strategyPeriodEndIso) missing.push("Strategy Period bounds are required.");
    if (!payload.primaryKpiMetric) missing.push("Primary KPI / Metric is required.");
    if (!payload.timeHorizonStart || !payload.timeHorizonEnd) missing.push("Objective horizon dates are required.");
    if (fieldMap.get("objective-owner-position")) missing.push("Owner Position is required.");
    if (ownerState.requiresNamedOwner && !payload.currentOwnerPersonId) missing.push("Current Owner Person must resolve from the selected position.");

    if (!targetRows.length) {
      blockers.push("Objective Target Plan rows have not been generated.");
    }
    if (targetRows.length && periods.length && targetRows.length !== periods.length) {
      blockers.push("Target Plan row count does not match the current objective horizon.");
    }
    targetRows.forEach((row) => {
      if (periods.length) {
        const matchingPeriod = periods.find((period) => cleanText(period.key) === cleanText(row.periodKey || row.year || ""));
        if (!matchingPeriod) blockers.push(`Target row ${row.periodLabel || row.periodKey || row.year} is outside the current planning window.`);
      }
      if (row.periodStart && planningRange.strategyPeriodStartIso && row.periodStart < planningRange.strategyPeriodStartIso) {
        blockers.push(`Target row ${row.periodLabel || row.periodKey || row.year} starts before the Strategy Period.`);
      }
      if (row.periodEnd && planningRange.strategyPeriodEndIso && row.periodEnd > planningRange.strategyPeriodEndIso) {
        blockers.push(`Target row ${row.periodLabel || row.periodKey || row.year} ends after the Strategy Period.`);
      }
      if (row.periodStart && payload.timeHorizonStart && row.periodStart < payload.timeHorizonStart) {
        blockers.push(`Target row ${row.periodLabel || row.periodKey || row.year} starts before the parent Goal horizon.`);
      }
      if (row.periodEnd && payload.timeHorizonEnd && row.periodEnd > payload.timeHorizonEnd) {
        blockers.push(`Target row ${row.periodLabel || row.periodKey || row.year} ends after the parent Goal horizon.`);
      }
      if (thresholdsRequired && (row.thresholdMin === null || row.thresholdMax === null)) {
        blockers.push(`Threshold Min and Threshold Max are required for ${row.periodLabel || row.periodKey || row.year}.`);
      }
    });
    const missingTargetValues = targetRows.filter((row) => row.targetValue === null).map((row) => row.periodLabel || row.periodKey || String(row.year || ""));
    if (targetRows.length && missingTargetValues.length) {
      blockers.push(`Target Value is required for periods: ${missingTargetValues.join(", ")}.`);
    }
    if (objectiveTargetPlanNeedsRegeneration()) {
      warnings.push("Target Plan is out of sync with the current KPI, Strategy Period, or horizon. Regenerate to realign it.");
      blockers.push("Regenerate or reset the Objective Target Plan before treating this objective as planning-ready.");
    }
    if (objectiveUsesTemplateCatalog() && objectiveSourceTemplateId) {
      const templateCompatibility = currentObjectiveTemplateCompatibility();
      if (templateCompatibility.state === "blocked" || templateCompatibility.state === "neutral") {
        warnings.push(templateCompatibility.message || "Objective Template compatibility could not be fully verified yet.");
      }
    }
    if (payload.parentGoalId && !payload.inheritCompanyScope && !payload.primaryCompanyId && !payload.applicableCompanyIds.length) {
      warnings.push("Unlocked scope is empty. Add a Primary Company or Applicable Companies before alignment.");
    }
    objectiveTargetPlanGovernanceWarnings(payload).forEach((warning) => warnings.push(warning));
    goalObjectiveAlignmentWarnings(payload).forEach((warning) => warnings.push(warning));

    const draftReady = missing.length === 0 && !fieldMap.get("objective-horizon-end-date");
    const planningReady = draftReady && blockers.length === 0 && targetRows.length > 0 && missingTargetValues.length === 0;
    const publishRequiredMissing = [];
    if (!payload.unitOfMeasure) publishRequiredMissing.push("Unit of Measure");
    if (!payload.directionOfPerformance) publishRequiredMissing.push("Direction of Good Performance");
    if (!payload.reportingFrequency) publishRequiredMissing.push("Reporting Frequency");
    const publishReady = planningReady && publishRequiredMissing.length === 0;
    if (publishRequiredMissing.length) warnings.push(`Publish readiness still needs: ${publishRequiredMissing.join(", ")}.`);

    return {
      missing,
      blockers: [...new Set(blockers)],
      warnings: [...new Set(warnings)],
      draftReady,
      planningReady,
      publishReady,
      targetRowsCount: targetRows.length,
      missingTargetValuesCount: missingTargetValues.length,
      thresholdsRequired
    };
  };

  const renderObjectiveReadinessPanel = () => {
    if (!objectiveReadinessIndicatorEl) return;
    const snapshot = objectiveReadinessSnapshot();
    renderParentGoalTargetContext();
    objectiveReadinessIndicatorEl.className = `es-status-pill ${snapshot.planningReady ? "is-ready" : "is-blocked"}`;
    objectiveReadinessIndicatorEl.textContent = snapshot.planningReady ? "Planning Readiness: Ready" : "Planning Readiness: Blocked";
    if (objectiveReadinessTextEl) {
      objectiveReadinessTextEl.textContent = snapshot.planningReady
        ? "Objective target planning is complete. Downstream initiative and project alignment can proceed in Strategy Alignment Register."
        : "Complete KPI setup and the Objective Target Plan before this objective is treated as planning-ready.";
    }
    if (objectiveReadinessMissingEl) objectiveReadinessMissingEl.innerHTML = objectiveTargetPlanListHtml(snapshot.missing);
    if (objectiveReadinessBlockersEl) objectiveReadinessBlockersEl.innerHTML = objectiveTargetPlanListHtml(snapshot.blockers);
    if (objectiveReadinessWarningsEl) objectiveReadinessWarningsEl.innerHTML = objectiveTargetPlanListHtml(snapshot.warnings);
    if (objectiveReadinessDraftChipEl) objectiveReadinessDraftChipEl.textContent = `Draft readiness: ${snapshot.draftReady ? "Ready" : "Blocked"}`;
    if (objectiveReadinessPublishChipEl) objectiveReadinessPublishChipEl.textContent = `Publish readiness: ${snapshot.publishReady ? "Ready" : "Blocked"}`;
    if (objectiveReadinessPlanChipEl) objectiveReadinessPlanChipEl.textContent = `Target plan rows: ${snapshot.targetRowsCount}`;
    if (objectiveReadinessTargetsChipEl) objectiveReadinessTargetsChipEl.textContent = `Rows missing target values: ${snapshot.missingTargetValuesCount}`;
    updateObjectiveTargetPlanGovernanceWarningBanner(snapshot.warnings.filter((warning) => /cadence|operational|phasing/i.test(warning)));
  };

  const updateObjectiveTargetPlanActions = () => {
    const prerequisiteState = objectivePlanningPrerequisiteState();
    const hasRows = objectiveTargetPlanRows.length > 0;
    const actionDisabled = !prerequisiteState.hasPrerequisites && !hasRows;
    const setDisabled = (id, disabled) => {
      const btn = document.getElementById(id);
      if (btn) btn.disabled = disabled;
    };
    setDisabled("objective-generate-target-plan", !prerequisiteState.hasPrerequisites);
    setDisabled("objective-regenerate-target-plan", !prerequisiteState.hasPrerequisites && !hasRows);
    setDisabled("objective-target-plan-fill-flat", actionDisabled || !hasRows);
    setDisabled("objective-target-plan-copy-down", actionDisabled || objectiveTargetPlanRows.length < 2);
    setDisabled("objective-target-plan-interpolate", actionDisabled || objectiveTargetPlanRows.length < 2);
    setDisabled("objective-target-plan-clear-values", !hasRows);
  };

  const renderObjectiveTargetPlanTable = () => {
    if (!objectiveTargetPlanBody) return;
    const prerequisiteState = objectivePlanningPrerequisiteState();
    const hasRows = objectiveTargetPlanRows.length > 0;
    const granularity = currentObjectiveTargetPlanGranularity();
    renderParentGoalTargetContext();
    if (objectiveTargetPlanEmptyEl) objectiveTargetPlanEmptyEl.classList.toggle("d-none", hasRows);
    if (objectiveTargetPlanContextEl) {
      if (!prerequisiteState.hasPrerequisites) {
        objectiveTargetPlanContextEl.textContent = "Select Parent Goal, Strategy Period, horizon dates, and KPI to generate the target plan.";
      } else if (objectiveTargetPlanNeedsRegeneration()) {
        objectiveTargetPlanContextEl.textContent = "Target rows exist, but the Strategy Period, target granularity, KPI, or horizon changed. Regenerate to realign the plan.";
      } else if (hasRows) {
        const periods = deriveObjectivePlanPeriods();
        objectiveTargetPlanContextEl.textContent = `Using ${periods.length} ${granularity === "TotalStrategyPeriod" ? "total-period" : granularity.toLowerCase()} target row(s) inside the allowed Strategy Period and Goal horizon.`;
      } else {
        objectiveTargetPlanContextEl.textContent = `Ready to generate ${granularity === "TotalStrategyPeriod" ? "a total-period" : granularity.toLowerCase()} target plan from the current Strategy Period and Goal horizon.`;
      }
    }
    if (objectiveTargetPlanStatusChipEl) {
      objectiveTargetPlanStatusChipEl.className = `es-status-pill ${!hasRows ? "is-info" : (objectiveTargetPlanNeedsRegeneration() ? "is-blocked" : "is-ready")}`;
      objectiveTargetPlanStatusChipEl.textContent = !hasRows
        ? "Plan: Not generated"
        : (objectiveTargetPlanNeedsRegeneration() ? "Plan: Needs regenerate" : "Plan: Ready");
    }
    if (!hasRows) {
      objectiveTargetPlanBody.innerHTML = "";
      updateObjectiveTargetPlanActions();
      renderObjectiveReadinessPanel();
      return;
    }
    objectiveTargetPlanBody.innerHTML = objectiveTargetPlanRows.map((row, index) => `
      <tr data-row-index="${index}">
        <td>
          <span class="objective-target-plan-period">${escapeHtml(row.periodLabel || row.periodKey || String(row.year || ""))}</span>
          <span class="objective-target-plan-subtext">${escapeHtml(row.periodGranularity || granularity)}</span>
        </td>
        <td>${escapeHtml(formatIsoForDisplay(row.periodStart) || "-")}</td>
        <td>${escapeHtml(formatIsoForDisplay(row.periodEnd) || "-")}</td>
        <td><input class="form-control form-control-sm" type="number" step="any" data-row-index="${index}" data-field="targetValue" value="${escapeHtml(displayNumericValue(row.targetValue))}" /></td>
        <td><input class="form-control form-control-sm" type="number" step="any" data-row-index="${index}" data-field="actualValue" value="${escapeHtml(displayNumericValue(row.actualValue))}" /></td>
        <td><input class="form-control form-control-sm" type="number" step="any" data-row-index="${index}" data-field="forecastValue" value="${escapeHtml(displayNumericValue(row.forecastValue))}" /></td>
        <td><input class="form-control form-control-sm" type="number" step="any" data-row-index="${index}" data-field="thresholdMin" value="${escapeHtml(displayNumericValue(row.thresholdMin))}" /></td>
        <td><input class="form-control form-control-sm" type="number" step="any" data-row-index="${index}" data-field="thresholdMax" value="${escapeHtml(displayNumericValue(row.thresholdMax))}" /></td>
        <td><textarea class="form-control form-control-sm" rows="1" data-row-index="${index}" data-field="commentary">${escapeHtml(row.commentary || "")}</textarea></td>
      </tr>
    `).join("");
    updateObjectiveTargetPlanActions();
    renderObjectiveReadinessPanel();
  };

  const adoptObjectiveMetricAssignment = (metricRow) => {
    objectiveMetricAssignmentSeed = metricRow ? normalizeObjectiveMetricAssignment(metricRow) : null;
  };

  const hydrateObjectiveTargetPlanFromMetrics = (objective) => {
    const metrics = Array.isArray(objective?.metrics)
      ? objective.metrics
      : (Array.isArray(objective?.metricAssignments) ? objective.metricAssignments : []);
    const primaryMetricId = cleanText(objective?.primaryKpiMetric || objective?.primaryMetricId || "");
    const seededMetric = metrics.find((metric) => cleanText(metric.metricId || metric.metricDefId || metric.metricName) === primaryMetricId)
      || metrics[0]
      || null;
    adoptObjectiveMetricAssignment(seededMetric);
    objectiveTargetPlanRows = (seededMetric?.yearlyValues || [])
      .map((row) => normalizeObjectiveYearlyValue(row))
      .filter((row) => row.year > 0 || row.periodKey)
      .sort((left, right) => {
        const leftOrder = Number.isFinite(left.sortOrder) ? left.sortOrder : Number.MAX_SAFE_INTEGER;
        const rightOrder = Number.isFinite(right.sortOrder) ? right.sortOrder : Number.MAX_SAFE_INTEGER;
        if (leftOrder !== rightOrder) return leftOrder - rightOrder;
        return cleanText(left.periodStart || left.periodKey || left.year).localeCompare(cleanText(right.periodStart || right.periodKey || right.year));
      });
    objectiveTargetPlanSignature = currentObjectivePlanSignature();
  };

  const confirmObjectiveTargetPlanReset = async (message, confirmLabel = "Regenerate") => {
    if (window.enterpriseStrategyUi?.confirm) {
      return window.enterpriseStrategyUi.confirm({
        title: "Replace Objective Target Plan?",
        message,
        confirmLabel,
        cancelLabel: "Keep existing rows",
        confirmKind: "warning"
      });
    }
    return window.confirm(message);
  };

  const ensureObjectiveTargetPlanPrerequisites = () => {
    const prerequisiteState = objectivePlanningPrerequisiteState();
    const missing = [];
    if (!prerequisiteState.parentGoalId) missing.push("Parent Goal");
    if (!prerequisiteState.strategyPeriodId) missing.push("Strategy Period");
    if (!prerequisiteState.startIso || !prerequisiteState.endIso) missing.push("Objective horizon");
    if (!prerequisiteState.metricId) missing.push("Primary KPI / Metric");
    if (missing.length) {
      notify(`Complete ${missing.join(", ")} before generating the Objective Target Plan.`, "warning");
      return false;
    }
    return true;
  };

  const generateObjectiveTargetPlan = async ({ preserveValues = true, force = false } = {}) => {
    if (!ensureObjectiveTargetPlanPrerequisites()) return;
    if (force && objectiveTargetPlanRows.length && objectiveTargetPlanHasValues()) {
      const confirmed = await confirmObjectiveTargetPlanReset("Regenerating the Objective Target Plan realigns rows to the current Strategy Period, target plan granularity, KPI, and allowed horizon. Existing values may be reset for periods that no longer match.");
      if (!confirmed) return;
    } else if (!force && objectiveTargetPlanRows.length && objectiveTargetPlanHasValues()) {
      notify("Objective Target Plan already has values. Use Regenerate if you need to rebuild it for the latest Strategy Period, target plan granularity, KPI, or horizon.", "info");
      return;
    }
    objectiveTargetPlanRows = buildObjectiveTargetPlanRows({
      existingRows: objectiveTargetPlanRows,
      preserveValues: preserveValues && objectiveTargetPlanRows.length > 0
    });
    objectiveTargetPlanSignature = currentObjectivePlanSignature();
    renderObjectiveTargetPlanTable();
    markDirty();
  };

  const fillObjectiveTargetPlanFlat = () => {
    if (!objectiveTargetPlanRows.length) {
      notify("Generate Objective Target Plan rows first.", "warning");
      return;
    }
    const value = window.prompt("Fill all Target Value rows with:", displayNumericValue(objectiveTargetPlanRows[0]?.targetValue));
    if (value === null) return;
    const parsed = parseNullableDecimal(value);
    if (parsed === null) {
      notify("Enter a numeric Target Value to fill the plan.", "warning");
      return;
    }
    objectiveTargetPlanRows = objectiveTargetPlanRows.map((row) => ({ ...row, targetValue: parsed }));
    renderObjectiveTargetPlanTable();
    markDirty();
  };

  const copyDownObjectiveTargetPlan = () => {
    if (objectiveTargetPlanRows.length < 2) {
      notify("At least two target rows are required for copy down.", "warning");
      return;
    }
    let carry = objectiveTargetPlanRows.find((row) => row.targetValue !== null)?.targetValue ?? null;
    if (carry === null) {
      notify("Enter at least one Target Value before using copy down.", "warning");
      return;
    }
    objectiveTargetPlanRows = objectiveTargetPlanRows.map((row, index) => {
      if (index === 0 && row.targetValue !== null) {
        carry = row.targetValue;
        return row;
      }
      if (row.targetValue !== null) {
        carry = row.targetValue;
        return row;
      }
      return { ...row, targetValue: carry };
    });
    renderObjectiveTargetPlanTable();
    markDirty();
  };

  const interpolateObjectiveTargetPlan = () => {
    if (objectiveTargetPlanRows.length < 2) {
      notify("At least two target rows are required to interpolate.", "warning");
      return;
    }
    const firstRow = objectiveTargetPlanRows[0];
    const lastRow = objectiveTargetPlanRows[objectiveTargetPlanRows.length - 1];
    const firstPromptDefault = displayNumericValue(firstRow?.targetValue);
    const lastPromptDefault = displayNumericValue(lastRow?.targetValue);
    const firstValueInput = firstRow?.targetValue !== null ? firstPromptDefault : window.prompt(`Enter Target Value for ${firstRow?.periodLabel || firstRow?.periodKey || firstRow?.year}:`, firstPromptDefault);
    if (firstValueInput === null) return;
    const lastValueInput = lastRow?.targetValue !== null ? lastPromptDefault : window.prompt(`Enter Target Value for ${lastRow?.periodLabel || lastRow?.periodKey || lastRow?.year}:`, lastPromptDefault);
    if (lastValueInput === null) return;
    const startValue = parseNullableDecimal(firstValueInput);
    const endValue = parseNullableDecimal(lastValueInput);
    if (startValue === null || endValue === null) {
      notify("Interpolation requires numeric start and end Target Values.", "warning");
      return;
    }
    const distance = Math.max(1, objectiveTargetPlanRows.length - 1);
    objectiveTargetPlanRows = objectiveTargetPlanRows.map((row, index) => {
      const ratio = index / distance;
      const value = startValue + ((endValue - startValue) * ratio);
      return { ...row, targetValue: Math.round(value * 10000) / 10000 };
    });
    renderObjectiveTargetPlanTable();
    markDirty();
  };

  const clearObjectiveTargetPlanValues = async () => {
    if (!objectiveTargetPlanRows.length) return;
    const confirmed = await confirmObjectiveTargetPlanReset("Clear Target Value, Actual, Forecast, Thresholds, and Commentary from the current Objective Target Plan?", "Clear values");
    if (!confirmed) return;
    objectiveTargetPlanRows = objectiveTargetPlanRows.map((row) => ({
      ...row,
      targetValue: null,
      actualValue: null,
      forecastValue: null,
      thresholdMin: null,
      thresholdMax: null,
      commentary: ""
    }));
    renderObjectiveTargetPlanTable();
    markDirty();
  };

  const initObjectiveSelect2 = () => {
    if (!window.jQuery || !window.jQuery.fn?.select2 || !workspaceRoot) return;
    const $ = window.jQuery;
    const $root = $(workspaceRoot);
    $root.find("select.select2").each(function () {
      const $el = $(this);
      if ($el.hasClass("select2-hidden-accessible")) {
        try { $el.select2("destroy"); } catch (_) { }
      }
      $el.select2({
        width: "100%",
        dropdownParent: $root,
        placeholder: $el.attr("multiple") ? "Search and select..." : "Select...",
        closeOnSelect: !$el.attr("multiple")
      });
      $el.off("select2:select select2:unselect select2:clear");
      $el.on("select2:select select2:unselect select2:clear", function () {
        this.dispatchEvent(new Event("change", { bubbles: true }));
      });
    });
  };

  const compatibleObjectiveTypes = (goalType, allTypes) => {
    const compatibility = {
      Growth: ["Growth", "Financial", "Market", "Customer", "Portfolio"],
      Operations: ["Operations", "Capability", "Risk", "Transformation"],
      Transformation: ["Transformation", "Innovation", "Capability", "People", "Portfolio"],
      Risk: ["Risk", "Operations", "Capability"],
      Financial: ["Financial", "Growth", "Portfolio", "Risk"]
    };
    const allowed = compatibility[String(goalType || "").trim()];
    if (!allowed?.length) return allTypes;
    const filtered = allTypes.filter((item) => allowed.includes(item));
    return filtered.length ? filtered : allTypes;
  };

  const applyObjectiveTypeFilterByGoal = (goalType) => {
    const allTypes = workbook.goalObjectiveTypes || [];
    const filtered = compatibleObjectiveTypes(goalType, allTypes);
    const selectEl = document.getElementById("objective-type");
    const helpEl = document.getElementById("objective-type-help");
    if (!selectEl) return;
    const hasParent = Boolean(selectedGoalIdFromForm());
    const currentValue = selectEl?.value || "";
    const options = filtered.slice();
    const preferredValue = cleanText(selectedObjectiveSourceMeta?.type || currentValue);
    if (!hasParent) {
      workbook.fillSelect?.(selectEl, [], { placeholder: "Select Parent Goal first" });
      selectEl.value = "";
      selectEl.disabled = true;
      if (helpEl) {
        helpEl.textContent = "Objective must be anchored to a Parent Goal first so compatible Objective Type values can be derived.";
      }
      if (window.jQuery && window.jQuery(selectEl).hasClass("select2-hidden-accessible")) {
        window.jQuery(selectEl).trigger("change.select2");
      }
      return;
    }
    workbook.fillSelect?.(selectEl, options, { placeholder: goalType ? "Select compatible type" : "Select type" });
    if (preferredValue && options.includes(preferredValue)) {
      selectEl.value = preferredValue;
    } else if (currentValue && options.includes(currentValue)) {
      selectEl.value = currentValue;
    } else if (options.length === 1) {
      selectEl.value = options[0];
    } else if (!cleanText(selectEl.value) && options.length) {
      selectEl.value = options[0];
    }
    selectEl.disabled = Boolean(goalType && options.length === 1);
    if (helpEl) {
      helpEl.textContent = goalType
        ? (options.length === 1
          ? `Objective Type is derived from Parent Goal type ${goalType} and is locked because only one compatible value is available.`
          : `Objective Type options are constrained by Parent Goal type ${goalType}.`)
        : "Objective Type is available after Parent Goal is anchored.";
    }
    if (window.jQuery && window.jQuery(selectEl).hasClass("select2-hidden-accessible")) {
      window.jQuery(selectEl).trigger("change.select2");
    }
  };

  const clearParentInheritedValues = () => {
    selectedGoalPlanningContext = null;
    selectedParentGoalContext = null;
    withSuppressedOverrideTracking(() => {
      const themeOverrideEl = document.getElementById("objective-theme-override");
      if (themeOverrideEl) themeOverrideEl.checked = false;
      const planningEl = document.getElementById("objective-planning-cycle");
      if (planningEl) planningEl.value = "";
      const themeEl = document.getElementById("objective-strategic-theme");
      if (themeEl) themeEl.value = "";
      setObjectiveHorizonInputFromRaw("objective-horizon-start-date", "", false);
      setObjectiveHorizonInputFromRaw("objective-horizon-end-date", "", true);
      document.getElementById("objective-owner-company").value = "";
      document.getElementById("objective-owner-position").value = "";
      document.getElementById("objective-current-owner-person").value = "";
      document.getElementById("objective-current-owner-person-display").value = "";
      document.getElementById("objective-primary-company").value = "";
      document.getElementById("objective-business-unit").value = "";
      document.getElementById("objective-region").value = "";
      setSelectedValues("objective-applicable-companies", []);
      document.getElementById("objective-entity-scope-summary").value = "";
    });
    syncThemeOverrideUi();
    syncCompanyOverrideUi();
    refreshObjectiveOwnerPositionOptions();
    applyObjectivePlanningContextUi();
    renderParentGoalInheritedContext();
    refreshParentMetricSummary();
    updateObjectiveSourcePickerParentHint("Select Parent Goal first to load compatible Objective templates.");
    syncObjectiveTemplateBrowseState();
    updateObjectiveSourceSummary();
  };

  const getParentGoalContext = async (goalId) => {
    const key = String(goalId || "").trim();
    if (!key) return null;
    if (parentGoalContextCache.has(key)) return parentGoalContextCache.get(key);
    const listed = goalsCache.find((goal) => String(goal.id || "").toLowerCase() === key.toLowerCase());
    let detail = null;
    let planningContext = null;
    try {
      detail = await window.strategyGoalsApi.get(key);
    } catch (_) {
      detail = listed || null;
    }
    try {
      planningContext = await window.strategyGoalsApi.getPlanningContext?.(key);
    } catch (_) {
      planningContext = null;
    }
    const goal = { ...(listed || {}), ...(detail || {}) };
    const metrics = (Array.isArray(goal.metrics) ? goal.metrics : (Array.isArray(goal.Metrics) ? goal.Metrics : []))
      .map(normalizeGoalMetricAssignment);
    const strategyPeriodId = String(planningContext?.strategyPeriodId || goal.strategyPeriodId || goal.planningCycle || "").trim();
    const strategyPeriod = strategyPeriodsById.get(strategyPeriodId) || null;
    const context = {
      goalId: goal.id || key,
      goalName: goal.name || "",
      goalType: goal.type || goal.category || "",
      strategicThemeId: goal.strategicThemeId || goal.category || "",
      sourceTemplateId: goal.sourceTemplateId || "",
      sourceTemplateType: goal.sourceTemplateType || "",
      strategyPeriodId,
      strategyPeriodCode: planningContext?.strategyPeriodCode || strategyPeriod?.code || goal.strategyPeriodCode || "",
      strategyPeriodName: planningContext?.strategyPeriodName || strategyPeriod?.name || goal.strategyPeriodName || "",
      strategyPeriodStatus: planningContext?.strategyPeriodStatus || "",
      strategyPeriodStartDate: parseDateIso(planningContext?.startDate || strategyPeriod?.startDate || ""),
      strategyPeriodEndDate: parseDateIso(planningContext?.endDate || strategyPeriod?.endDate || ""),
      goalStartDate: parseDateIso(goal.startDate || goal.planningHorizonStart || ""),
      goalEndDate: parseDateIso(goal.endDate || goal.planningHorizonEnd || ""),
      primaryCompanyId: goal.primaryCompanyId || goal.ownerCompanyId || "",
      applicableCompanyIds: goal.applicableCompanyIds || [],
      businessUnitId: goal.businessUnitId || goal.businessUnit || "",
      regionId: goal.regionId || goal.region || "",
      entityScope: goal.entityScope || "",
      status: goal.status || "",
      metrics
    };
    parentGoalContextCache.set(key, context);
    return context;
  };

  const applyParentGoalDefaults = async (goalId, options = {}) => {
    const parent = await getParentGoalContext(goalId);
    if (!parent) return;
    const forceRefreshPrefill = options.forceRefreshPrefill === true;
    selectedParentGoalContext = parent;
    selectedGoalPlanningContext = {
      strategyPeriodId: parent.strategyPeriodId || "",
      strategyPeriodCode: parent.strategyPeriodCode || "",
      strategyPeriodName: parent.strategyPeriodName || "",
      strategyPeriodStatus: parent.strategyPeriodStatus || "",
      strategyPeriodStartDate: parent.strategyPeriodStartDate || "",
      strategyPeriodEndDate: parent.strategyPeriodEndDate || "",
      goalStartDate: parent.goalStartDate || "",
      goalEndDate: parent.goalEndDate || ""
    };

    if (selectedGoalPlanningContext.strategyPeriodId) {
      ensureObjectivePlanningOption(selectedGoalPlanningContext.strategyPeriodId, strategyPeriodDisplayLabel(selectedGoalPlanningContext));
    }
    const inheritedStart = parent.goalStartDate || selectedGoalPlanningContext.goalStartDate || selectedGoalPlanningContext.strategyPeriodStartDate || parent.strategyPeriodStartDate || "";
    const inheritedEnd = parent.goalEndDate || selectedGoalPlanningContext.goalEndDate || selectedGoalPlanningContext.strategyPeriodEndDate || parent.strategyPeriodEndDate || "";

    withSuppressedOverrideTracking(() => {
      const themeEl = document.getElementById("objective-strategic-theme");
      if (themeEl && (!isEditMode || forceRefreshPrefill || !themeEl.value)) {
        themeEl.value = parent.strategicThemeId || "";
      }
      const planningEl = document.getElementById("objective-planning-cycle");
      if (planningEl) planningEl.value = selectedGoalPlanningContext.strategyPeriodId || "";
      const currentStartIso = objectiveHorizonIsoFromInput("objective-horizon-start-date");
      const currentEndIso = objectiveHorizonIsoFromInput("objective-horizon-end-date");
      if (!isEditMode || forceRefreshPrefill || !currentStartIso || !currentEndIso) {
        setObjectiveHorizonInputFromRaw("objective-horizon-start-date", inheritedStart, false);
        setObjectiveHorizonInputFromRaw("objective-horizon-end-date", inheritedEnd, true);
      } else {
        let nextStartIso = currentStartIso;
        let nextEndIso = currentEndIso;
        if (inheritedStart && (nextStartIso < inheritedStart || nextStartIso > (inheritedEnd || nextStartIso))) {
          nextStartIso = inheritedStart;
        }
        if (inheritedEnd && (nextEndIso > inheritedEnd || nextEndIso < (inheritedStart || nextEndIso))) {
          nextEndIso = inheritedEnd;
        }
        if (nextStartIso && nextEndIso && nextStartIso > nextEndIso) {
          nextStartIso = inheritedStart || nextStartIso;
          nextEndIso = inheritedEnd || nextEndIso;
        }
        if (nextStartIso !== currentStartIso) {
          setObjectiveHorizonInputFromRaw("objective-horizon-start-date", nextStartIso, false);
        }
        if (nextEndIso !== currentEndIso) {
          setObjectiveHorizonInputFromRaw("objective-horizon-end-date", nextEndIso, true);
        }
      }
      const unlockScopeEl = document.getElementById("objective-inherit-company-scope");
      if (unlockScopeEl && (!isEditMode || forceRefreshPrefill)) unlockScopeEl.checked = false;
      if (!unlockScopeEl?.checked || forceRefreshPrefill) {
        ensureSelectOption("objective-owner-company", parent.primaryCompanyId || "", companyLabelById(parent.primaryCompanyId || ""));
        ensureSelectOption("objective-primary-company", parent.primaryCompanyId || "", companyLabelById(parent.primaryCompanyId || ""));
        (parent.applicableCompanyIds || []).forEach((companyId) => ensureSelectOption("objective-applicable-companies", companyId, companyLabelById(companyId)));
        ensureSelectOption("objective-business-unit", parent.businessUnitId || "", parent.businessUnitId || "");
        ensureSelectOption("objective-region", parent.regionId || "", parent.regionId || "");
        setValueIfPresent("objective-owner-company", parent.primaryCompanyId || "");
        setValueIfPresent("objective-primary-company", parent.primaryCompanyId || "");
        setSelectedValues("objective-applicable-companies", parent.applicableCompanyIds || []);
        setValueIfPresent("objective-business-unit", parent.businessUnitId || "");
        setValueIfPresent("objective-region", parent.regionId || "");
        setValueIfPresent("objective-entity-scope-summary", parent.entityScope || "");
      }
    });

    applyObjectiveTypeFilterByGoal(parent.goalType);
    syncThemeOverrideUi();
    syncCompanyOverrideUi();
    refreshObjectiveOwnerPositionOptions();
    applyObjectivePlanningContextUi();
    renderParentGoalInheritedContext();
    refreshParentMetricSummary();
    updateObjectiveSourcePickerParentHint(parent.sourceTemplateId
      ? `Parent Goal anchored. Compatible Objective Templates are filtered by Parent Goal Template ${parent.sourceTemplateId} and Goal type ${parent.goalType || "-"}.`
      : `Parent Goal anchored. Compatible Objective Templates are filtered by Goal type ${parent.goalType || "-"}.`);
    syncObjectiveTemplateBrowseState();
    await syncObjectiveTemplateOwnerSuggestion();
    updateObjectiveSourceSummary();
  };

  const buildMetricAssignments = () => {
    const primaryMetricId = String(document.getElementById("objective-primary-kpi")?.value || "").trim();
    if (!primaryMetricId) return [];
    const primaryMetricLabel = document.getElementById("objective-primary-kpi")?.selectedOptions?.[0]?.textContent?.trim() || primaryMetricId;
    const seed = objectiveMetricAssignmentSeed || {};
    const yearlyValues = serializeObjectiveTargetPlanRows(
      objectiveTargetPlanRows.length
        ? objectiveTargetPlanRows
        : buildObjectiveTargetPlanRows({ existingRows: [], preserveValues: false })
    );
    return [{
      parentMetricAssignmentId: cleanText(seed.parentMetricAssignmentId || primaryMetricId),
      metricDefId: primaryMetricId,
      metricId: primaryMetricId,
      metricName: primaryMetricLabel,
      metricClass: cleanText(seed.metricClass || "Inherited") || "Inherited",
      metricRole: cleanText(seed.metricRole || "Contribution") || "Contribution",
      aggregationMethod: cleanText(seed.aggregationMethod || seed.aggregationMethodId || ""),
      aggregationMethodId: cleanText(seed.aggregationMethodId || seed.aggregationMethod || ""),
      unitOfMeasureId: document.getElementById("objective-kpi-uom")?.value || "",
      unitOfMeasure: document.getElementById("objective-kpi-uom")?.value || "",
      direction: document.getElementById("objective-direction")?.value || "",
      polarityCode: document.getElementById("objective-direction")?.value || "",
      reportingFrequencyCode: document.getElementById("objective-reporting-frequency")?.value || "",
      thresholdTolerance: cleanText(seed.thresholdTolerance || ""),
      thresholdValue: parseNullableDecimal(seed.thresholdValue),
      thresholdModelCode: cleanText(seed.thresholdModelCode || currentObjectiveMetricCatalogEntry()?.thresholdModelCode || currentObjectiveMetricCatalogEntry()?.thresholdModel || ""),
      metricBindingStatus: cleanText(seed.metricBindingStatus || ""),
      rollupEligibleFlag: seed.rollupEligibleFlag !== false,
      targetPeriod: currentObjectiveTargetPlanGranularity(),
      yearlyValues
    }];
  };

  const collectPayload = () => {
    const parentGoalId = selectedGoalIdFromForm();
    const unlocked = document.getElementById("objective-inherit-company-scope")?.checked === true;
    const objectiveId = isEditMode ? String(document.getElementById("objective-id")?.value || "").trim() : "";
    const name = String(document.getElementById("objective-name")?.value || "").trim();
    const statement = String(document.getElementById("objective-statement")?.value || "").trim();
    const strategicTheme = String(document.getElementById("objective-strategic-theme")?.value || "").trim();
    const status = String(document.getElementById("objective-status")?.value || "Draft").trim() || "Draft";
    const type = String(document.getElementById("objective-type")?.value || "").trim();
    const priority = String(document.getElementById("objective-priority")?.value || "").trim();
    const ownerCompanyId = String(document.getElementById("objective-owner-company")?.value || "").trim();
    const ownerPositionId = String(document.getElementById("objective-owner-position")?.value || "").trim();
    const currentOwnerPersonId = resolveUserId(document.getElementById("objective-current-owner-person")?.value || "");
    const planningCycle = String(selectedGoalPlanningContext?.strategyPeriodId || document.getElementById("objective-planning-cycle")?.value || "").trim();
    const timeHorizonStart = objectiveHorizonIsoFromInput("objective-horizon-start-date") || null;
    const timeHorizonEnd = objectiveHorizonIsoFromInput("objective-horizon-end-date") || null;
    const primaryCompanyId = String(document.getElementById("objective-primary-company")?.value || "").trim() || null;
    const applicableCompanyIds = selectedValues("objective-applicable-companies");
    const businessUnit = String(document.getElementById("objective-business-unit")?.value || "").trim() || null;
    const region = String(document.getElementById("objective-region")?.value || "").trim() || null;
    const entityScope = String(document.getElementById("objective-entity-scope-summary")?.value || "").trim();
    const primaryKpiMetric = String(document.getElementById("objective-primary-kpi")?.value || "").trim();
    const unitOfMeasure = String(document.getElementById("objective-kpi-uom")?.value || "").trim();
    const directionOfPerformance = String(document.getElementById("objective-direction")?.value || "").trim();
    const reportingFrequency = String(document.getElementById("objective-reporting-frequency")?.value || "").trim();
    const targetPlanGranularity = currentObjectiveTargetPlanGranularity();
    const legacyOwner = currentOwnerPersonId || ownerPositionId;
    const sourceTemplateType = objectiveUsesTemplateCatalog() && objectiveSourceTemplateId ? "Template" : "";
    const payload = {
      id: objectiveId,
      objectiveId,
      parentGoalId,
      goalId: parentGoalId,
      name,
      objectiveName: name,
      statement,
      objectiveStatement: statement,
      strategicTheme,
      strategicThemeId: strategicTheme,
      status,
      lifecycleState: status,
      type,
      objectiveTypeId: type,
      priority,
      ownerCompanyId,
      ownerPositionId,
      currentOwnerPersonId,
      owner: legacyOwner,
      ownerId: legacyOwner,
      planningCycle,
      planningCycleId: planningCycle,
      strategyPeriodId: planningCycle,
      timeHorizonStart,
      timeHorizonEnd,
      startDate: timeHorizonStart,
      endDate: timeHorizonEnd,
      inheritCompanyScope: !unlocked,
      inheritScopeFromParentGoal: !unlocked,
      primaryCompanyId,
      applicableCompanyIds,
      businessUnit,
      businessUnitId: businessUnit,
      region,
      regionId: region,
      entityScope,
      primaryKpiMetric,
      primaryMetricId: primaryKpiMetric,
      unitOfMeasure,
      unitOfMeasureId: unitOfMeasure,
      directionOfPerformance,
      performanceDirection: directionOfPerformance,
      targetPlanGranularity,
      reportingFrequency,
      reportingFrequencyId: reportingFrequency,
      sourceTemplateType,
      sourceTemplateId: sourceTemplateType ? objectiveSourceTemplateId : "",
      sourceTemplateVersion: sourceTemplateType ? (objectiveSourceTemplateVersion || 0) : null,
      createdFromLibrary: sourceTemplateType === "Template"
    };
    payload.metrics = buildMetricAssignments();
    payload.metricAssignments = payload.metrics;
    return payload;
  };

  const resolveSavedObjectiveIdentity = (result, fallbackObjectiveId = "") => {
    const data = result?.objective || result?.Objective || result?.data || result || {};
    const idCandidates = [
      data?.id, data?.objectiveId, data?.objectiveID,
      result?.id, result?.objectiveId, result?.objectiveID,
      fallbackObjectiveId
    ];
    const versionCandidates = [data?.version, result?.version];
    const id = idCandidates.map((value) => String(value || "").trim()).find(Boolean) || "";
    const version = versionCandidates
      .map((value) => Number(value))
      .find((value) => Number.isFinite(value) && value > 0) || null;
    return { id, version };
  };

  const validate = (payload) => {
    const errors = [];
    const fieldMap = fieldErrorMap(payload);
    return [...new Set(Array.from(fieldMap.values()).filter(Boolean))];
  };

  function fieldErrorMap(payload, options = {}) {
    const includeTargetPlan = options.includeTargetPlan !== false;
    const out = new Map();
    if (isEditMode && !payload.id) out.set("objective-id", "Objective ID is required.");
    if (!payload.parentGoalId) out.set("objective-parent-goal", "Parent Goal is required.");
    if (!payload.name) out.set("objective-name", "Objective Name is required.");
    if (!payload.statement) out.set("objective-statement", "Objective Statement is required.");
    if (!payload.type) out.set("objective-type", "Objective Type is required.");
    if (!payload.priority) out.set("objective-priority", "Priority is required.");
    if (!payload.ownerCompanyId) out.set("objective-owner-company", "Owner Company / Org is required.");
    if (!payload.ownerPositionId) out.set("objective-owner-position", "Owner Position is required.");
    const ownerState = objectiveOwnerState();
    if (ownerState.requiresNamedOwner && !payload.currentOwnerPersonId) {
      out.set("objective-current-owner-person-display", "Current Owner Person must be resolved from the selected position.");
    }
    if (payload.currentOwnerPersonId && !ownerState.currentMatches) {
      out.set("objective-current-owner-person-display", "Current Owner Person must be resolved from the selected position.");
    }
    if (!payload.planningCycle) out.set("objective-planning-cycle", "Strategy Period is inherited from Parent Goal.");
    if (!payload.primaryKpiMetric) out.set("objective-primary-kpi", "Primary KPI / Metric is required.");
    if (!payload.strategicTheme) out.set("objective-strategic-theme", "Strategic Theme / Pillar is required from Parent Goal or override.");
    if (!payload.timeHorizonStart) out.set("objective-horizon-start-date", "Start Date is required.");
    if (!payload.timeHorizonEnd) out.set("objective-horizon-end-date", "End Date is required.");
    if (payload.timeHorizonStart && payload.timeHorizonEnd && payload.timeHorizonEnd < payload.timeHorizonStart) {
      out.set("objective-horizon-end-date", "End Date must be after or equal to Start Date.");
    }
    const parent = selectedParentGoalContext && selectedParentGoalContext.goalId === payload.parentGoalId
      ? selectedParentGoalContext
      : goalsCache.find((goal) => String(goal.id || "") === String(payload.parentGoalId || ""));
    const bounds = deriveAllowedHorizonBounds(parent, selectedGoalPlanningContext);
    const templateCompatibility = currentObjectiveTemplateCompatibility();
    if (payload.parentGoalId && !selectedParentGoalContext) {
      out.set("objective-parent-goal", "Inherited Goal context could not be resolved from Parent Goal.");
    }
    if (payload.parentGoalId && !String(parent?.strategyPeriodId || "").trim()) {
      out.set("objective-parent-goal", "Parent Goal must be linked to an active Strategy Period.");
    }
    if (payload.parentGoalId && payload.type && !objectiveTypeCompatibleWithGoal(payload.type)) {
      out.set("objective-type", `Objective Type must stay compatible with Parent Goal type ${selectedParentGoalContext?.goalType || "-"}.`);
    }
    if (objectiveSourceTemplateId && templateCompatibility.state === "mismatch") {
      out.set("objective-parent-goal", templateCompatibility.message);
    }
    if (payload.parentGoalId && bounds.goalStart && payload.timeHorizonStart && payload.timeHorizonStart < bounds.goalStart) {
      out.set("objective-horizon-start-date", "Start Date must be on or after Parent Goal Start Date.");
    }
    if (payload.parentGoalId && bounds.goalEnd && payload.timeHorizonEnd && payload.timeHorizonEnd > bounds.goalEnd) {
      out.set("objective-horizon-end-date", "End Date must be on or before Parent Goal End Date.");
    }
    if (!payload.inheritCompanyScope && !payload.primaryCompanyId && !payload.applicableCompanyIds.length) {
      out.set("objective-primary-company", "Unlocked scope requires a Primary Company or Applicable Companies.");
    }
    if (includeTargetPlan) {
      if (objectiveTargetPlanNeedsRegeneration()) {
        out.set("objective-target-plan-anchor", "Objective Target Plan must be regenerated after Strategy Period, target granularity, KPI, or horizon changes.");
      } else {
        const periods = deriveObjectivePlanPeriods();
        const targetRowsOutsideHorizon = objectiveTargetPlanRows.some((row) => periods.length && !periods.some((period) => cleanText(period.key) === cleanText(row.periodKey || row.year || "")));
        if (targetRowsOutsideHorizon) {
          out.set("objective-target-plan-anchor", "Objective Target Plan rows must stay inside the current Strategy Period and objective horizon.");
        }
      }
    }
    return out;
  }

  const fieldLabel = (id) => {
    const label = document.querySelector(`label[for="${id}"]`);
    return String(label?.textContent || id || "Field").replace(/\*/g, "").trim();
  };

  const buildErrorShortcuts = (fieldMap) => {
    if (!(fieldMap instanceof Map) || !fieldMap.size) return "";
    const links = [];
    fieldMap.forEach((_, id) => {
      if (!id) return;
      links.push(`<button type="button" class="objective-error-jump btn btn-sm btn-outline-danger" data-field-id="${id}">${escapeHtml(fieldLabel(id))}</button>`);
    });
    return links.length ? `<div class="objective-error-shortcuts mt-2"><span class="small me-2">Go to:</span>${links.join("")}</div>` : "";
  };

  const showErrors = (errors, fieldMap) => {
    if (!errorEl) return;
    if (!errors.length) {
      errorEl.classList.add("d-none");
      errorEl.textContent = "";
      return;
    }
    errorEl.classList.remove("d-none");
    errorEl.innerHTML = `<strong>Please fix the following:</strong><ul class="mb-0">${errors.map((error) => `<li>${escapeHtml(error)}</li>`).join("")}</ul>${buildErrorShortcuts(fieldMap)}`;
    errorEl.querySelectorAll(".objective-error-jump").forEach((btn) => {
      btn.addEventListener("click", () => {
        const target = document.getElementById(btn.dataset.fieldId || "");
        if (!target) return;
        target.scrollIntoView?.({ behavior: "smooth", block: "center" });
        target.focus?.();
      });
    });
  };

  const applyFieldErrors = (payload, map = fieldErrorMap(payload)) => {
    objectiveFieldIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(document.getElementById(id)));
    map.forEach((message, id) => window.enterpriseModalFormUtils?.setFieldError?.(document.getElementById(id), message));
  };

  const applyValidation = () => {
    const payload = collectPayload();
    renderObjectiveReadinessPanel();
    if (!hasSubmitAttempt) {
      objectiveFieldIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(document.getElementById(id)));
      if (!isDirty) showErrors([], new Map());
      if (saveBtn) saveBtn.disabled = false;
      return;
    }
    const map = fieldErrorMap(payload);
    applyFieldErrors(payload, map);
    showErrors(validate(payload), map);
    if (saveBtn) saveBtn.disabled = false;
  };

  const canAdvanceWizard = (step) => {
    const payload = collectPayload();
    const map = fieldErrorMap(payload);
    const stepErrors = (wizardStepRequiredFields[step] || []).map((fieldId) => map.get(fieldId)).filter(Boolean);
    if (!stepErrors.length) return true;
    hasSubmitAttempt = true;
    applyFieldErrors(payload, map);
    showErrors(stepErrors, map);
    window.enterpriseModalFormUtils?.focusFirstInvalid?.(formRootEl);
    return false;
  };

  const setWizardStep = (step) => {
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
    if (safeStep === 3 && window.esbpHorizonDates?.initIn) {
      window.esbpHorizonDates.initIn(formRootEl);
      applyObjectivePlanningContextUi();
    }
    if (wizardBackBtn) wizardBackBtn.disabled = safeStep === 1;
    if (wizardNextBtn) wizardNextBtn.classList.toggle("d-none", safeStep === totalWizardSteps);
    if (saveBtn) saveBtn.classList.toggle("d-none", !isEditMode && safeStep !== totalWizardSteps);
  };

  const markDirty = () => {
    if (suppressDirtyTracking || suppressLeavePrompt) return;
    isDirty = true;
    applyValidation();
  };

  const hydrateObjectiveFormLookups = async () => {
    const typeOptions = nonEmpty(workbook.goalObjectiveTypes, uniq(objectivesCache.map((x) => x.type)));
    const strategicThemes = nonEmpty(workbook.strategicThemes, uniq(goalsCache.map((x) => x.category)));
    const priorityOptions = nonEmpty(workbook.priorities, uniq(objectivesCache.map((x) => x.priority)));
    const directionOptions = nonEmpty(workbook.directionOfPerformance, ["Increase", "Decrease", "Maintain", "Within Range"]);
    const frequencyOptions = nonEmpty(workbook.reportingFrequencies, ["Monthly", "Quarterly", "Annual"]);
    const companyOptions = normalizedObjectiveCompanyOptions();
    const parentGoalOptions = buildParentGoalOptions();
    const strategyPeriodOptions = Array.from(strategyPeriodsById.values())
      .map((period) => ({
        value: String(period?.id || "").trim(),
        label: strategyPeriodDisplayLabel({
          strategyPeriodCode: period?.code || period?.strategyPeriodCode || period?.id || "",
          strategyPeriodName: period?.name || period?.strategyPeriodName || "Strategy Period",
          strategyPeriodStartDate: period?.startDate || period?.strategyPeriodStartDate || "",
          strategyPeriodEndDate: period?.endDate || period?.strategyPeriodEndDate || ""
        })
      }))
      .filter((item) => item.value);

    const fillIfPresent = (id, options, config) => {
      const el = document.getElementById(id);
      if (!el) return;
      workbook.fillSelect?.(el, options, config);
    };
    fillIfPresent("objective-parent-goal", parentGoalOptions, { placeholder: "Search goal by name, ID, period, owner or company" });
    fillIfPresent("objective-owner-company", companyOptions, { placeholder: "Select owner company / org" });
    fillIfPresent("objective-type", typeOptions, { placeholder: "Select type" });
    fillIfPresent("objective-strategic-theme", strategicThemes, { placeholder: "Select strategic theme" });
    fillIfPresent("objective-priority", priorityOptions, { placeholder: "Select priority", defaultValue: "Medium" });
    fillIfPresent("objective-planning-cycle", strategyPeriodOptions, { placeholder: "Inherited from Parent Goal" });
    fillIfPresent("objective-primary-company", companyOptions, { placeholder: "Select primary company" });
    fillIfPresent("objective-applicable-companies", companyOptions, { placeholder: "Select applicable companies" });
    fillIfPresent("objective-business-unit", normalizedObjectiveBusinessUnitOptions(), { placeholder: "Select business unit" });
    fillIfPresent("objective-region", normalizedObjectiveRegionOptions(), { placeholder: "Select region" });
    fillIfPresent("objective-kpi-uom", nonEmpty(workbook.unitOfMeasure, []), { placeholder: "Select unit" });
    fillIfPresent("objective-direction", directionOptions, { placeholder: "Select direction" });
    fillIfPresent("objective-reporting-frequency", frequencyOptions, { placeholder: "Select frequency" });
    if (objectiveTargetPlanGranularityEl) objectiveTargetPlanGranularityEl.value = currentObjectiveTargetPlanGranularity();
    initObjectiveApplicableCompaniesPicker();
    syncObjectiveApplicableCompaniesPickerFromSelect();
    refreshObjectiveOwnerPositionOptions();
  };

  const fetchObjectiveForEdit = async (objectiveId) => {
    const detail = await window.strategyObjectivesApi.get(objectiveId);
    return normalizeObjectiveRow(detail?.objective || detail?.Objective || detail || {});
  };

  const refreshObjectiveIdPreview = async () => {
    const el = document.getElementById("objective-id");
    if (!el || isEditMode) return;
    el.readOnly = true;
    el.placeholder = "Loading preview...";
    try {
      const preview = await window.strategyEnterpriseMetaApi?.runtimeIdPreview?.();
      el.value = preview?.objectiveId || "";
      el.placeholder = "";
    } catch (_) {
      el.value = "";
      el.placeholder = "Assigned on save";
    }
  };

  const openEditor = async (item) => {
    await withSuppressedDirtyTracking(async () => {
      await hydrateObjectiveFormLookups();
      initObjectiveSelect2();
      isEditMode = Boolean(item?.id);
      userOverrides = new Set();
      objectiveTemplateAppliedFields = new Map();
      objectiveTargetPlanRows = [];
      objectiveTargetPlanSignature = "";
      objectiveMetricAssignmentSeed = null;
      lastParentGoalId = String(item?.parentGoalId || "").trim();
      currentVersion = Number(item?.version ?? item?.Version ?? 0) || 0;
      suppressLeavePrompt = false;
      objectiveCreationModeCode = cleanText(item?.sourceTemplateId || "") ? "Template" : "Blank";
      objectiveSourceTemplateId = cleanText(item?.sourceTemplateId || "");
      objectiveSourceTemplateVersion = normalizeObjectiveSourceVersion(item?.sourceTemplateVersion);
      selectedObjectiveSourceMeta = objectiveSourceTemplateId ? {
        id: objectiveSourceTemplateId,
        name: cleanText(item?.name || objectiveSourceTemplateId),
        status: cleanText(item?.status || ""),
        version: objectiveSourceTemplateVersion
      } : null;
      const creationModeEl = document.getElementById("objective-creation-mode-select");
      if (creationModeEl) creationModeEl.value = objectiveCreationModeCode;
      updateObjectiveCreationModeUi();
      if (modalTitle) modalTitle.textContent = isEditMode ? "Edit Objective" : "Create Objective";
      if (modalSubtitle) {
        modalSubtitle.textContent = isEditMode
          ? "Update a draft strategic objective while preserving Goal-aligned planning context."
          : "Create a draft strategic objective linked to a parent goal and measurable outcome.";
      }
      if (saveBtn) saveBtn.textContent = isEditMode ? "Save Changes" : "Create Objective";

      ensureSelectOption("objective-parent-goal", item?.parentGoalId, goalLabel(item?.parentGoalId || ""));
      ensureSelectOption("objective-type", item?.type, item?.type || "");
      ensureSelectOption("objective-strategic-theme", item?.strategicTheme, item?.strategicTheme || "");
      ensureSelectOption("objective-owner-company", item?.ownerCompanyId || item?.primaryCompanyId || "", companyLabelById(item?.ownerCompanyId || item?.primaryCompanyId || ""));
      ensureSelectOption("objective-owner-position", item?.ownerPositionId || "", objectiveOwnerPositionLabel(item?.ownerPositionId || ""));
      ensureSelectOption("objective-planning-cycle", item?.planningCycle || item?.planningCycleId || item?.strategyPeriodId || "", strategyPeriodDisplayLabel({
        strategyPeriodCode: item?.strategyPeriodCode || item?.planningCycle || item?.planningCycleId || item?.strategyPeriodId || "",
        strategyPeriodName: item?.strategyPeriodName || "Strategy Period",
        strategyPeriodStartDate: item?.timeHorizonStart || item?.startDate || "",
        strategyPeriodEndDate: item?.timeHorizonEnd || item?.endDate || ""
      }));
      ensureSelectOption("objective-priority", item?.priority, item?.priority || "");
      ensureSelectOption("objective-primary-company", item?.primaryCompanyId, companyLabelById(item?.primaryCompanyId || ""));
      (item?.applicableCompanyIds || []).forEach((companyId) => ensureSelectOption("objective-applicable-companies", companyId, companyLabelById(companyId)));
      ensureSelectOption("objective-business-unit", item?.businessUnit || item?.businessUnitId || "", item?.businessUnit || item?.businessUnitId || "");
      ensureSelectOption("objective-region", item?.region || item?.regionId || "", item?.region || item?.regionId || "");
      ensureSelectOption("objective-primary-kpi", item?.primaryKpiMetric || item?.primaryMetricId || "", item?.primaryKpiMetric || item?.primaryMetricId || "");
      ensureSelectOption("objective-kpi-uom", item?.unitOfMeasure || item?.unitOfMeasureId || "", item?.unitOfMeasure || item?.unitOfMeasureId || "");
      ensureSelectOption("objective-direction", item?.directionOfPerformance || item?.performanceDirection || "", item?.directionOfPerformance || item?.performanceDirection || "");
      ensureSelectOption("objective-reporting-frequency", item?.reportingFrequency || item?.reportingFrequencyId || "", item?.reportingFrequency || item?.reportingFrequencyId || "");
      ensureSelectOption("objective-owner", item?.currentOwnerPersonId || item?.owner || "", resolveUserName(item?.currentOwnerPersonId || item?.owner || ""));

      const idEl = document.getElementById("objective-id");
      if (idEl) {
        if (isEditMode) {
          idEl.value = item?.id || "";
          idEl.readOnly = true;
          idEl.placeholder = "";
        } else {
          idEl.value = "";
          idEl.readOnly = true;
          void refreshObjectiveIdPreview();
        }
      }

      document.getElementById("objective-name").value = item?.name || "";
      document.getElementById("objective-parent-goal").value = item?.parentGoalId || "";
      document.getElementById("objective-type").value = item?.type || "";
      document.getElementById("objective-statement").value = item?.statement || "";
      document.getElementById("objective-priority").value = item?.priority || "";
      document.getElementById("objective-status").value = item?.status || "Draft";
      const statusReadonlyEl = document.getElementById("objective-status-readonly");
      if (statusReadonlyEl) statusReadonlyEl.value = item?.status || "Draft";
      document.getElementById("objective-strategic-theme").value = item?.strategicTheme || "";
      document.getElementById("objective-theme-override").checked = false;
      document.getElementById("objective-owner-company").value = item?.ownerCompanyId || item?.primaryCompanyId || "";
      refreshObjectiveOwnerPositionOptions();
      document.getElementById("objective-owner-position").value = item?.ownerPositionId || "";
      setValueIfPresent("objective-owner", item?.currentOwnerPersonId || item?.owner || "");
      document.getElementById("objective-current-owner-person").value = resolveUserId(item?.currentOwnerPersonId || item?.owner || "");
      document.getElementById("objective-current-owner-person-display").value = resolveUserName(item?.currentOwnerPersonId || item?.owner || "");
      syncObjectiveCurrentOwnerPerson();
      syncObjectiveOwnerPosition();
      document.getElementById("objective-planning-cycle").value = item?.planningCycle || item?.planningCycleId || item?.strategyPeriodId || "";
      setObjectiveHorizonInputFromRaw("objective-horizon-start-date", item?.timeHorizonStart || item?.startDate || "", false);
      setObjectiveHorizonInputFromRaw("objective-horizon-end-date", item?.timeHorizonEnd || item?.endDate || "", true);
      document.getElementById("objective-inherit-company-scope").checked = item?.inheritCompanyScope === false;
      document.getElementById("objective-primary-company").value = item?.primaryCompanyId || "";
      setSelectedValues("objective-applicable-companies", item?.applicableCompanyIds || []);
      document.getElementById("objective-business-unit").value = item?.businessUnit || item?.businessUnitId || "";
      document.getElementById("objective-region").value = item?.region || item?.regionId || "";
      document.getElementById("objective-entity-scope-summary").value = item?.entityScope || "";
      document.getElementById("objective-primary-kpi").value = item?.primaryKpiMetric || item?.primaryMetricId || "";
      document.getElementById("objective-kpi-uom").value = item?.unitOfMeasure || item?.unitOfMeasureId || "";
      document.getElementById("objective-direction").value = item?.directionOfPerformance || item?.performanceDirection || "";
      document.getElementById("objective-reporting-frequency").value = item?.reportingFrequency || item?.reportingFrequencyId || "";
      if (objectiveTargetPlanGranularityEl) objectiveTargetPlanGranularityEl.value = normalizeObjectiveTargetPlanGranularity(item?.targetPlanGranularity || item?.TargetPlanGranularity || "Yearly");
      hydrateObjectiveTargetPlanFromMetrics(item || {});

      if (errorEl) {
        errorEl.classList.add("d-none");
        errorEl.textContent = "";
      }
      hasSubmitAttempt = false;
      isDirty = false;
      selectedParentGoalContext = null;
      selectedGoalPlanningContext = null;

      if (item?.parentGoalId) {
        await applyParentGoalDefaults(item.parentGoalId, { forceRefreshPrefill: false });
        const parentTheme = String(selectedParentGoalContext?.strategicThemeId || "").trim();
        const objectiveTheme = String(item?.strategicTheme || "").trim();
        const hasExplicitThemeOverride = Boolean(objectiveTheme && parentTheme && objectiveTheme !== parentTheme);
        document.getElementById("objective-theme-override").checked = hasExplicitThemeOverride;
        const unlockScopeEl = document.getElementById("objective-inherit-company-scope");
        const unlockSavedScope = item?.inheritCompanyScope === false;
        if (unlockScopeEl) unlockScopeEl.checked = unlockSavedScope;
        const savedOwnerCompanyId = cleanText(item?.ownerCompanyId || item?.primaryCompanyId || "");
        if (savedOwnerCompanyId) {
          ensureSelectOption("objective-owner-company", savedOwnerCompanyId, companyLabelById(savedOwnerCompanyId));
          setValueIfPresent("objective-owner-company", savedOwnerCompanyId);
          await refreshObjectiveOwnerPositionOptions();
          setValueIfPresent("objective-owner-position", item?.ownerPositionId || "");
          syncObjectiveCurrentOwnerPerson();
        }
        if (unlockSavedScope) {
          const savedPrimaryCompanyId = cleanText(item?.primaryCompanyId || "");
          if (savedPrimaryCompanyId) {
            ensureSelectOption("objective-primary-company", savedPrimaryCompanyId, companyLabelById(savedPrimaryCompanyId));
          }
          (item?.applicableCompanyIds || []).forEach((companyId) => ensureSelectOption("objective-applicable-companies", companyId, companyLabelById(companyId)));
          ensureSelectOption("objective-business-unit", item?.businessUnit || item?.businessUnitId || "", item?.businessUnit || item?.businessUnitId || "");
          ensureSelectOption("objective-region", item?.region || item?.regionId || "", item?.region || item?.regionId || "");
          setValueIfPresent("objective-primary-company", savedPrimaryCompanyId);
          setSelectedValues("objective-applicable-companies", item?.applicableCompanyIds || []);
          setValueIfPresent("objective-business-unit", item?.businessUnit || item?.businessUnitId || "");
          setValueIfPresent("objective-region", item?.region || item?.regionId || "");
          setValueIfPresent("objective-entity-scope-summary", item?.entityScope || "");
        }
      } else {
        clearParentInheritedValues();
      }

      if (objectiveTargetPlanRows.length) {
        objectiveTargetPlanRows = buildObjectiveTargetPlanRows({ existingRows: objectiveTargetPlanRows, preserveValues: true });
        objectiveTargetPlanSignature = currentObjectivePlanSignature();
      }

      syncThemeOverrideUi();
      syncCompanyOverrideUi();
      applyObjectivePlanningContextUi();
      renderParentGoalInheritedContext();
      refreshParentMetricSummary();
      syncObjectiveTemplateBrowseState();
      renderObjectiveTargetPlanTable();
      if (objectiveSourceTemplateId && window.strategyLibraryApi?.template) {
        try {
          await applyObjectiveTemplateDetail(objectiveSourceTemplateId, null, { prefillFields: false });
        } catch (_) {
          updateObjectiveSourceSummary();
        }
      } else {
        updateObjectiveSourceSummary();
      }
      setWizardStep(1);
      applyValidation();
      isDirty = false;
      window.scrollTo?.({ top: 0, behavior: "smooth" });
    });
  };

  const getCell = (item, key) => {
    if (key === "id") return escapeHtml(item.id || "");
    if (key === "name") return escapeHtml(item.name || "");
    if (key === "parentGoalId") {
      const goalId = String(item.parentGoalId || "").trim();
      return goalId
        ? `<a href="${objectiveListUrl.replace("/objectives", `/goals/${encodeURIComponent(goalId)}`)}">${escapeHtml(goalLabel(goalId))}</a>`
        : "-";
    }
    if (key === "owner") return escapeHtml(resolveUserName(item.owner || item.ownerId) || "-");
    if (key === "status") return statusBadgeHtml(item.status);
    if (key === "type") return escapeHtml(item.type || "-");
    if (key === "priority") return escapeHtml(item.priority || "-");
    if (key === "startYear") return escapeHtml(fromDateToYear(item.timeHorizonStart || item.startDate) || "-");
    if (key === "endYear") return escapeHtml(fromDateToYear(item.timeHorizonEnd || item.endDate) || "-");
    if (key === "metricSummary") return escapeHtml(metricSummaryText(item) || "-");
    if (key === "inheritCompanyScope") return item.inheritCompanyScope === false ? "Override" : "Inherited";
    if (key === "primaryCompanyId") return escapeHtml(companyLabelById(item.primaryCompanyId) || "-");
    if (key === "applicableCompanyIds") return escapeHtml((item.applicableCompanyIds || []).map(companyLabelById).join(", ") || "-");
    if (key === "entityScope") return escapeHtml(item.entityScope || "-");
    if (key === "actions") {
      return window.enterpriseRowActionsMenu?.render?.(item.id, [
        { action: "view", label: "View", href: `${objectiveListUrl}/${encodeURIComponent(item.id)}` },
        { action: "edit", label: "Edit" },
        { action: "duplicate", label: "Duplicate" },
        { action: "alignment", label: "Open alignment register" },
        { divider: true },
        { action: "openParentGoal", label: "Open parent goal" },
        { action: "archive", label: "Archive / Delete" },
        { action: "exportRow", label: "Export row" }
      ]) || "";
    }
    return "";
  };

  const getSortValue = (item, key) => {
    if (key === "startYear") return Number(fromDateToYear(item.timeHorizonStart || item.startDate) || 0);
    if (key === "endYear") return Number(fromDateToYear(item.timeHorizonEnd || item.endDate) || 0);
    if (key === "applicableCompanyIds") return (item.applicableCompanyIds || []).join(",");
    if (key === "metricSummary") return metricSummaryText(item);
    return item[key] ?? "";
  };

  const getExportValue = (item, key) => {
    if (key === "status") return item.status || "";
    if (key === "startYear") return fromDateToYear(item.timeHorizonStart || item.startDate) || "";
    if (key === "endYear") return fromDateToYear(item.timeHorizonEnd || item.endDate) || "";
    if (key === "metricSummary") return metricSummaryText(item);
    return item[key] ?? "";
  };

  const render = (items) => {
    if (!tableBody) return;
    tableBody.innerHTML = "";
    const cols = tableControls?.getVisibleColumns?.() || fallbackColumns;
    if (headerRow) {
      headerRow.innerHTML =
        `<th class="text-center" style="width:42px;"><input type="checkbox" id="objective-select-all" aria-label="Select all visible rows" ${items.length && items.every((item) => selectedObjectiveIds.has(String(item.id || ""))) ? "checked" : ""} /></th>` +
        cols.map((col) => {
          if (col.key === "actions") return `<th data-col-key="${col.key}" class="text-end es-row-actions-col"><span class="es-table-head-label">${escapeHtml(col.label)}</span></th>`;
          return `<th data-col-key="${col.key}"><span class="es-col-drag-handle me-1" title="Drag to reorder">⋮⋮</span><button type="button" class="btn btn-link btn-sm p-0 text-decoration-none es-table-head-label objective-sort" data-key="${col.key}">${escapeHtml(col.label)}${tableControls?.sortIndicator?.(col.key) || ""}</button></th>`;
        }).join("");
    }

    items.forEach((item) => {
      const itemId = String(item.id || "");
      const tr = document.createElement("tr");
      tr.innerHTML =
        `<td class="text-center"><input type="checkbox" class="objective-row-select" data-id="${escapeHtml(itemId)}" aria-label="Select objective ${escapeHtml(itemId)}" ${selectedObjectiveIds.has(itemId) ? "checked" : ""} /></td>` +
        cols.map((col) => `<td class="${col.key === "actions" ? "text-end es-row-actions-col" : ""}">${getCell(item, col.key)}</td>`).join("");
      tr.querySelector(".objective-row-select")?.addEventListener("change", (event) => {
        if (event.target.checked) selectedObjectiveIds.add(itemId);
        else selectedObjectiveIds.delete(itemId);
        updateBulkActionsState();
        const selectAll = document.getElementById("objective-select-all");
        if (selectAll) selectAll.checked = items.length > 0 && items.every((row) => selectedObjectiveIds.has(String(row.id || "")));
      });
      tr.querySelectorAll(".es-row-action-item").forEach((el) => {
        el.addEventListener("click", async (event) => {
          const action = String(el.dataset.action || "");
          if (!action || action === "view") return;
          event.preventDefault();
          if (action === "edit") {
            navigateToObjectiveWorkspace("edit", item.id);
            return;
          }
          if (action === "duplicate") {
            navigateToObjectiveWorkspace("duplicate", item.id);
            return;
          }
          if (action === "alignment") {
            window.location.assign(`${objectiveListUrl}/alignment?objectiveId=${encodeURIComponent(item.id)}`);
            return;
          }
          if (action === "openParentGoal" && item.parentGoalId) {
            window.location.assign(`/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(item.parentGoalId)}`);
            return;
          }
          if (action === "archive") {
            try {
              await window.strategyObjectivesApi.archive(item.id, item.version || 0);
              selectedObjectiveIds.delete(String(item.id || ""));
              await load();
            } catch (err) {
              notify(window.enterpriseStrategyUi?.getErrorMessage?.(err, "Archive failed") || "Archive failed", "error");
            }
            return;
          }
          if (action === "exportRow") {
            window.enterpriseWorkbookIo?.exportCsv?.("objective_row.csv", toObjectiveSheetRows([item]));
          }
        });
      });
      tableBody.appendChild(tr);
    });

    if (!items.length) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="${cols.length + 1}" class="text-center text-muted py-3">No objectives found for the current filters.</td>`;
      tableBody.appendChild(tr);
    }

    document.getElementById("objective-select-all")?.addEventListener("change", (event) => {
      if (event.target.checked) items.forEach((item) => selectedObjectiveIds.add(String(item.id || "")));
      else items.forEach((item) => selectedObjectiveIds.delete(String(item.id || "")));
      updateBulkActionsState();
      renderFiltered(false);
    });

    (headerRow || document).querySelectorAll(".objective-sort").forEach((btn) => {
      btn.addEventListener("click", () => tableControls?.cycleSort?.(btn.dataset.key));
    });
    window.enterpriseTablePageUtils?.bindHeaderColumnDrag?.(headerRow, {
      onReorder: (fromKey, toKey) => tableControls?.moveColumnTo?.(fromKey, toKey)
    });
  };

  const toObjectiveSheetRows = (items) => (items || []).map((item) => ({
    "Objective ID": item.id || "",
    "Objective": item.name || "",
    "Parent Goal": goalLabel(item.parentGoalId),
    "Owner": resolveUserName(item.owner || item.ownerId) || "",
    "Status": item.status || "",
    "Type": item.type || "",
    "Priority": item.priority || "",
    "Start Year": fromDateToYear(item.timeHorizonStart || item.startDate),
    "End Year": fromDateToYear(item.timeHorizonEnd || item.endDate),
    "Primary KPI / Metric": item.primaryKpiMetric || item.primaryMetricId || "",
    "Unit of Measure": item.unitOfMeasure || "",
    "Direction": item.directionOfPerformance || "",
    "Reporting Frequency": item.reportingFrequency || "",
    "Primary Company": companyLabelById(item.primaryCompanyId),
    "Applicable Companies": (item.applicableCompanyIds || []).map(companyLabelById).join(", "),
    "Entity Scope": item.entityScope || ""
  }));

  const exportSelectionCsvFallback = (rows) => {
    const headers = Object.keys(rows[0] || { "Objective ID": "", Objective: "" });
    const lines = [headers.join(",")].concat(rows.map((row) => headers.map((header) => `"${String(row[header] ?? "").replace(/"/g, '""')}"`).join(",")));
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "objectives_selection.csv";
    a.click();
    URL.revokeObjectURL(url);
  };

  const renderFiltered = (resetPage = true) => {
    if (!tableBody) return;
    const query = String(filters.search?.value || "").trim().toLowerCase();
    const yearRange = parseYearRange(filters.yearRange?.value);
    const rows = objectivesCache.filter((item) => {
      const blob = [
        item.id,
        item.name,
        item.parentGoalId,
        item.owner,
        item.status,
        item.type,
        item.priority,
        item.primaryCompanyId,
        (item.applicableCompanyIds || []).join(","),
        item.entityScope,
        metricSummaryText(item)
      ].join(" ").toLowerCase();
      if (query && !blob.includes(query)) return false;
      if (filters.parent?.value && String(item.parentGoalId || "") !== String(filters.parent.value || "")) return false;
      if (filters.owner?.value && resolveUserId(item.owner || item.ownerId) !== filters.owner.value) return false;
      if (filters.status?.value && String(item.status || "") !== String(filters.status.value || "")) return false;
      if (filters.type?.value && String(item.type || "") !== String(filters.type.value || "")) return false;
      if (filters.priority?.value && String(item.priority || "") !== String(filters.priority.value || "")) return false;
      if (filters.inheritCompanyScope?.value && String(item.inheritCompanyScope) !== String(filters.inheritCompanyScope.value)) return false;
      if (filters.company?.value) {
        const needle = String(filters.company.value || "").trim().toLowerCase();
        const companies = [item.primaryCompanyId].concat(item.applicableCompanyIds || [])
          .filter(Boolean)
          .flatMap((value) => [String(value).toLowerCase(), companyLabelById(value).toLowerCase()]);
        if (!companies.some((value) => value.includes(needle))) return false;
      }
      if (filters.scope?.value && !String(item.entityScope || "").toLowerCase().includes(String(filters.scope.value || "").toLowerCase())) return false;
      if (yearRange) {
        const startYear = Number(fromDateToYear(item.timeHorizonStart || item.startDate) || 0);
        const endYear = Number(fromDateToYear(item.timeHorizonEnd || item.endDate) || 0);
        if (!startYear || !endYear || endYear < yearRange.from || startYear > yearRange.to) return false;
      }
      return true;
    });
    filteredRows = tableControls?.sortRows?.(rows, getSortValue) || rows;
    tableControls?.setFilters?.({
      search: filters.search?.value || "",
      parent: filters.parent?.value || "",
      owner: filters.owner?.value || "",
      status: filters.status?.value || "",
      type: filters.type?.value || "",
      priority: filters.priority?.value || "",
      inheritCompanyScope: filters.inheritCompanyScope?.value || "",
      company: filters.company?.value || "",
      yearRange: filters.yearRange?.value || "",
      scope: filters.scope?.value || ""
    });
    filterDrawer?.setAppliedState(tableControls?.getFilters?.() || {});
    if (resetPage) pager?.resetToFirstPage?.();
    const paged = pager?.paginate?.(filteredRows) || filteredRows;
    render(paged);
  };

  const load = async () => {
    updateObjectiveCreationModeUi();
    syncObjectiveTemplateBrowseState();
    renderParentGoalInheritedContext();
    updateObjectiveSourceSummary();
    await workbook.ensureLookupsLoaded?.();
    await workbook.ensureUsersLoaded?.();
    await workbook.ensureCompaniesLoaded?.();

    const [objectivesResult, goalsResult, kpisResult, strategyPeriodsResult] = await Promise.allSettled([
      window.strategyObjectivesApi.list(),
      window.strategyGoalsApi.list(),
      window.strategyKpisApi?.list?.(),
      window.strategyPlanningApi?.listStrategyPeriods?.()
    ]);
    objectivesCache = objectivesResult.status === "fulfilled" ? (objectivesResult.value?.items || []).map(normalizeObjectiveRow) : [];
    goalsCache = goalsResult.status === "fulfilled" ? (goalsResult.value?.items || []).map(normalizeGoalRow).filter((goal) => goal.id) : [];
    const kpis = kpisResult.status === "fulfilled" ? (kpisResult.value?.items || []) : [];
    const strategyPeriods = strategyPeriodsResult.status === "fulfilled"
      ? (Array.isArray(strategyPeriodsResult.value) ? strategyPeriodsResult.value : (strategyPeriodsResult.value?.items || []))
      : [];
    strategyPeriodsById = new Map((strategyPeriods || []).map((period) => [String(period?.id || "").trim(), period]).filter(([id]) => id));
    objectiveMetricCatalogById = new Map((kpis || []).map((metric) => {
      const key = cleanText(metric?.id || metric?.kpiId || metric?.metricId || metric?.name || "");
      return [key, metric];
    }).filter(([key]) => key));

    metricOptionsCache = (kpis.length ? kpis : objectivesCache.map((item) => ({
      id: item.primaryKpiMetric || item.primaryMetricId,
      name: item.primaryKpiMetric || item.primaryMetricId
    })))
      .map((metric) => ({
        value: String(metric.id || metric.kpiId || metric.metricId || metric.name || "").trim(),
        label: String(metric.name || metric.label || metric.id || "").trim()
      }))
      .filter((metric) => metric.value)
      .filter((metric, index, list) => list.findIndex((candidate) => candidate.value === metric.value) === index);

    const primaryMetricEl = document.getElementById("objective-primary-kpi");
    if (primaryMetricEl) workbook.fillSelect?.(primaryMetricEl, metricOptionsCache, { placeholder: "Select primary KPI / metric" });
    const companyListEl = document.getElementById("objective-company-list");
    const companyLabels = normalizedObjectiveCompanyOptions().map((company) => company.label);
    if (companyListEl) workbook.fillDatalist?.(companyListEl, companyLabels);
    workbook.fillDatalist?.(document.getElementById("objective-filter-company-list"), companyLabels);
    if (isWorkspaceMode) await hydrateObjectiveFormLookups();

    if (isWorkspaceMode) {
      initObjectiveSelect2();
      const route = parseWorkspaceRouteContext();
      if (route.mode === "edit" && route.objectiveId) {
        const full = await fetchObjectiveForEdit(route.objectiveId);
        await openEditor(full || { id: route.objectiveId });
        return;
      }
      if (route.mode === "duplicate" && route.objectiveId) {
        const full = await fetchObjectiveForEdit(route.objectiveId);
        const clone = structuredClone(full || {});
        clone.id = "";
        clone.name = `${String(full?.name || "").trim()} (Copy)`.trim();
        clone.status = "Draft";
        await openEditor(clone);
        return;
      }
      await openEditor(null);
      return;
    }

    const statusOptions = nonEmpty(uniq(objectivesCache.map((item) => item.status)), ["Draft", "Active", "On Hold", "Archived"]);
    const ownerOptions = nonEmpty(workbook.userOptions?.() || [], []);
    const typeOptions = nonEmpty(workbook.goalObjectiveTypes, uniq(objectivesCache.map((item) => item.type)));
    const priorityOptions = nonEmpty(workbook.priorities, uniq(objectivesCache.map((item) => item.priority)));
    workbook.fillSelect?.(filters.parent, buildParentGoalOptions(), { placeholder: "Parent Goal" });
    workbook.fillSelect?.(filters.owner, ownerOptions, { placeholder: "Owner" });
    workbook.fillSelect?.(filters.status, statusOptions, { placeholder: "Status" });
    workbook.fillSelect?.(filters.type, typeOptions, { placeholder: "Type" });
    workbook.fillSelect?.(filters.priority, priorityOptions, { placeholder: "Priority" });

    const saved = tableControls?.getFilters?.() || {};
    Object.entries(saved).forEach(([key, value]) => {
      if (filters[key]) filters[key].value = value;
    });
    filterDrawer = window.enterpriseFilterDrawer?.create?.({
      pageKey: "objectives",
      triggerId: "objective-open-filters",
      drawerId: "objectiveFilterDrawer",
      applyButtonId: "objective-apply-filters",
      cancelButtonId: "objective-cancel-filters",
      clearButtonId: "objective-clear-filters",
      chipHostId: "objective-active-filters",
      fields: {
        search: filters.search,
        parent: filters.parent,
        owner: filters.owner,
        status: filters.status,
        type: filters.type,
        priority: filters.priority,
        inheritCompanyScope: filters.inheritCompanyScope,
        company: filters.company,
        yearRange: filters.yearRange,
        scope: filters.scope
      },
      labels: filterLabels,
      defaults: {
        search: "",
        parent: "",
        owner: "",
        status: "",
        type: "",
        priority: "",
        inheritCompanyScope: "",
        company: "",
        yearRange: "",
        scope: ""
      },
      onApply: () => renderFiltered(true)
    }) || null;
    filterDrawer?.setAppliedState(saved);
    updateBulkActionsState();
    renderFiltered();
  };

  objectiveFieldIds.forEach((fieldId) => {
    const el = document.getElementById(fieldId);
    el?.addEventListener("input", () => {
      trackUserOverride(fieldId);
      markDirty();
    });
    el?.addEventListener("change", () => {
      trackUserOverride(fieldId);
      markDirty();
    });
    el?.addEventListener("blur", () => {
      const map = fieldErrorMap(collectPayload());
      window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(fieldId) || "");
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

  document.getElementById("objective-parent-goal")?.addEventListener("change", async () => {
    const selectedGoalId = selectedGoalIdFromForm();
    if (!selectedGoalId) {
      if (objectiveSourceTemplateId) {
        clearObjectiveTemplateSelection({ preserveUserEdits: true, updateMode: false });
      }
      clearParentInheritedValues();
      applyObjectiveTypeFilterByGoal("");
      markDirty();
      return;
    }
    const hasOverrides = userOverrides.size > 0 && lastParentGoalId && !isEditMode;
    if (hasOverrides) {
      const refresh = await window.enterpriseStrategyUi?.confirm?.({
        title: "Apply parent goal defaults?",
        message: "Changing Parent Goal refreshes inherited planning and scope values. Continue?",
        confirmLabel: "Refresh from Goal",
        cancelLabel: "Keep my values",
        confirmKind: "primary"
      });
      await applyParentGoalDefaults(selectedGoalId, { forceRefreshPrefill: Boolean(refresh) });
    } else {
      await applyParentGoalDefaults(selectedGoalId, { forceRefreshPrefill: false });
    }
    lastParentGoalId = selectedGoalId;
    const compatibility = currentObjectiveTemplateCompatibility();
    if (objectiveUsesTemplateCatalog() && objectiveSourceTemplateId && compatibility.state === "mismatch") {
      clearObjectiveTemplateSelection({ preserveUserEdits: true, updateMode: false });
      notify(`${compatibility.message} The selected Objective Template was cleared.`, "warning");
      updateObjectiveSourceSummary();
    }
    markDirty();
  });

  document.getElementById("objective-theme-override")?.addEventListener("change", () => {
    syncThemeOverrideUi();
    markDirty();
  });

  document.getElementById("objective-owner-company")?.addEventListener("change", async () => {
    await refreshObjectiveOwnerPositionOptions();
    void syncObjectiveTemplateOwnerSuggestion();
    markDirty();
  });

  document.getElementById("objective-owner-position")?.addEventListener("change", () => {
    syncObjectiveCurrentOwnerPerson();
    markDirty();
  });

  document.getElementById("objective-inherit-company-scope")?.addEventListener("change", () => {
    const unlocked = document.getElementById("objective-inherit-company-scope")?.checked === true;
    if (!unlocked && selectedParentGoalContext) {
      withSuppressedOverrideTracking(() => {
        ensureSelectOption("objective-primary-company", selectedParentGoalContext.primaryCompanyId || "", companyLabelById(selectedParentGoalContext.primaryCompanyId || ""));
        (selectedParentGoalContext.applicableCompanyIds || []).forEach((companyId) => ensureSelectOption("objective-applicable-companies", companyId, companyLabelById(companyId)));
        ensureSelectOption("objective-business-unit", selectedParentGoalContext.businessUnitId || "", selectedParentGoalContext.businessUnitId || "");
        ensureSelectOption("objective-region", selectedParentGoalContext.regionId || "", selectedParentGoalContext.regionId || "");
        setValueIfPresent("objective-primary-company", selectedParentGoalContext.primaryCompanyId || "");
        setSelectedValues("objective-applicable-companies", selectedParentGoalContext.applicableCompanyIds || []);
        setValueIfPresent("objective-business-unit", selectedParentGoalContext.businessUnitId || "");
        setValueIfPresent("objective-region", selectedParentGoalContext.regionId || "");
      });
    }
    syncCompanyOverrideUi();
    markDirty();
  });

  ["objective-horizon-start-date", "objective-horizon-end-date"].forEach((fieldId) => {
    document.getElementById(fieldId)?.addEventListener("change", () => {
      applyObjectivePlanningContextUi();
      markDirty();
    });
  });

  ["objective-primary-company", "objective-applicable-companies", "objective-business-unit", "objective-region"].forEach((fieldId) => {
    document.getElementById(fieldId)?.addEventListener("change", () => {
      updateEntityScopeSummary();
      markDirty();
    });
  });

  ["objective-primary-kpi", "objective-kpi-uom", "objective-direction", "objective-reporting-frequency"].forEach((fieldId) => {
    document.getElementById(fieldId)?.addEventListener("change", () => {
      if (fieldId === "objective-primary-kpi") syncObjectiveKpiMetadataFromCatalog();
      refreshParentMetricSummary();
      if (fieldId !== "objective-reporting-frequency") renderObjectiveTargetPlanTable();
      else renderObjectiveReadinessPanel();
      markDirty();
    });
  });

  objectiveTargetPlanGranularityEl?.addEventListener("change", async () => {
    const previousGranularity = normalizeObjectiveTargetPlanGranularity(objectiveTargetPlanGranularityEl?.dataset.previousValue || "Yearly");
    const nextGranularity = currentObjectiveTargetPlanGranularity();
    if (objectiveTargetPlanRows.length) {
      const confirmed = await confirmObjectiveTargetPlanReset("Changing Target Plan Granularity rebuilds the Objective Target Plan row structure. Existing values are only preserved when the new periods still match exactly.", "Change granularity");
      if (!confirmed) {
        objectiveTargetPlanGranularityEl.value = previousGranularity;
        renderObjectiveReadinessPanel();
        return;
      }
      objectiveTargetPlanRows = buildObjectiveTargetPlanRows({ existingRows: objectiveTargetPlanRows, preserveValues: false });
      objectiveTargetPlanSignature = currentObjectivePlanSignature();
      objectiveTargetPlanGranularityEl.dataset.previousValue = nextGranularity;
      renderObjectiveTargetPlanTable();
      markDirty();
      return;
    }
    objectiveTargetPlanGranularityEl.value = nextGranularity;
    objectiveTargetPlanGranularityEl.dataset.previousValue = nextGranularity;
    renderObjectiveTargetPlanTable();
    markDirty();
  });

  document.getElementById("objective-generate-target-plan")?.addEventListener("click", async () => {
    await generateObjectiveTargetPlan({ preserveValues: true, force: false });
  });

  document.getElementById("objective-regenerate-target-plan")?.addEventListener("click", async () => {
    await generateObjectiveTargetPlan({ preserveValues: true, force: true });
  });

  document.getElementById("objective-target-plan-fill-flat")?.addEventListener("click", () => fillObjectiveTargetPlanFlat());
  document.getElementById("objective-target-plan-copy-down")?.addEventListener("click", () => copyDownObjectiveTargetPlan());
  document.getElementById("objective-target-plan-interpolate")?.addEventListener("click", () => interpolateObjectiveTargetPlan());
  document.getElementById("objective-target-plan-clear-values")?.addEventListener("click", async () => {
    await clearObjectiveTargetPlanValues();
  });

  objectiveTargetPlanBody?.addEventListener("input", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const rowIndex = Number(target.dataset.rowIndex || -1);
    const field = String(target.dataset.field || "").trim();
    if (!Number.isInteger(rowIndex) || rowIndex < 0 || rowIndex >= objectiveTargetPlanRows.length || !field) return;
    const nextRows = objectiveTargetPlanRows.slice();
    const current = { ...nextRows[rowIndex] };
    current[field] = field === "commentary"
      ? String(target.value || "")
      : parseNullableDecimal(target.value);
    nextRows[rowIndex] = current;
    objectiveTargetPlanRows = nextRows;
    renderObjectiveReadinessPanel();
    markDirty();
  });

  document.getElementById("objective-creation-mode-select")?.addEventListener("change", () => {
    const nextMode = cleanText(document.getElementById("objective-creation-mode-select")?.value || "Blank") || "Blank";
    if (!objectiveUsesTemplateCatalog(nextMode)) {
      clearObjectiveTemplateSelection({ preserveUserEdits: true, updateMode: false });
    }
    objectiveCreationModeCode = nextMode;
    updateObjectiveCreationModeUi();
    syncObjectiveTemplateBrowseState();
    updateObjectiveSourceSummary();
  });

  document.getElementById("objective-browse-source")?.addEventListener("click", async () => {
    if (!(window.strategyLibraryApi?.catalog && window.strategyLibraryApi?.template)) {
      notify("Objective Template catalog is unavailable.", "error");
      return;
    }
    if (!selectedGoalIdFromForm()) {
      notify("Select Parent Goal first to load compatible Objective templates.", "warning");
      return;
    }
    if (!objectiveUsesTemplateCatalog()) {
      objectiveCreationModeCode = "Template";
    }
    const creationModeEl = document.getElementById("objective-creation-mode-select");
    if (creationModeEl) creationModeEl.value = objectiveCreationModeCode;
    updateObjectiveCreationModeUi();
    await loadObjectiveSourcePickerCatalog();
    if (!objectiveTemplateCatalogAvailable) return;
    objectiveSourcePickerModal?.show();
  });

  document.getElementById("objective-clear-source")?.addEventListener("click", () => {
    clearObjectiveTemplateSelection({ preserveUserEdits: true, updateMode: true });
    markDirty();
  });

  document.getElementById("objective-source-picker-search")?.addEventListener("input", debounce(() => applyObjectiveSourcePickerFilters(), 120));
  document.getElementById("objective-source-picker-type")?.addEventListener("change", () => applyObjectiveSourcePickerFilters());
  document.getElementById("objective-source-picker-entity-scope")?.addEventListener("change", () => applyObjectiveSourcePickerFilters());

  saveBtn?.addEventListener("click", async () => {
    const payload = collectPayload();
    const fieldMap = fieldErrorMap(payload);
    const errors = validate(payload);
    hasSubmitAttempt = true;
    applyFieldErrors(payload, fieldMap);
    if (errors.length) {
      showErrors(errors, fieldMap);
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(formRootEl);
      return;
    }
    try {
      window.enterpriseModalFormUtils?.setSubmitting?.(saveBtn, true, isEditMode ? "Save Changes" : "Create Objective", "Saving...");
      let persisted = null;
      if (isEditMode) {
        persisted = await window.strategyObjectivesApi.update(payload.id, payload, currentVersion || 0);
      } else {
        persisted = await window.strategyObjectivesApi.create(payload);
      }
      isDirty = false;
      hasSubmitAttempt = false;
      suppressLeavePrompt = true;
      const savedIdentity = resolveSavedObjectiveIdentity(persisted, String(document.getElementById("objective-id")?.value || "").trim());
      if (savedIdentity.version) currentVersion = savedIdentity.version;
      if (savedIdentity.id) {
        isEditMode = true;
        setValueIfPresent("objective-id", savedIdentity.id);
      }
      const persistedId = savedIdentity.id || payload.id;
      window.location.assign(persistedId ? `${objectiveListUrl}/${encodeURIComponent(persistedId)}` : objectiveListUrl);
    } catch (err) {
      const backendErrors = window.enterpriseModalFormUtils?.backendErrors?.(err, "Save failed")
        || [window.enterpriseStrategyUi?.getErrorMessage?.(err, "Save failed") || "Save failed"];
      window.enterpriseModalFormUtils?.applyBackendFieldErrors?.(err, {
        id: document.getElementById("objective-id"),
        name: document.getElementById("objective-name"),
        statement: document.getElementById("objective-statement"),
        parentgoalid: document.getElementById("objective-parent-goal"),
        goal_id: document.getElementById("objective-parent-goal"),
        ownercompanyid: document.getElementById("objective-owner-company"),
        ownerpositionid: document.getElementById("objective-owner-position"),
        currentownerpersonid: document.getElementById("objective-current-owner-person-display"),
        owner: document.getElementById("objective-current-owner-person-display"),
        ownerid: document.getElementById("objective-current-owner-person-display"),
        strategictheme: document.getElementById("objective-strategic-theme"),
        strategicthemeid: document.getElementById("objective-strategic-theme"),
        type: document.getElementById("objective-type"),
        objectivetypeid: document.getElementById("objective-type"),
        planningcycleid: document.getElementById("objective-planning-cycle"),
        strategyperiodid: document.getElementById("objective-planning-cycle"),
        timehorizonstart: document.getElementById("objective-horizon-start-date"),
        timehorizonend: document.getElementById("objective-horizon-end-date"),
        primarykpimetric: document.getElementById("objective-primary-kpi"),
        primarymetricid: document.getElementById("objective-primary-kpi"),
        unitofmeasure: document.getElementById("objective-kpi-uom"),
        unitofmeasureid: document.getElementById("objective-kpi-uom"),
        directionofperformance: document.getElementById("objective-direction"),
        performancedirection: document.getElementById("objective-direction"),
        reportingfrequency: document.getElementById("objective-reporting-frequency"),
        reportingfrequencyid: document.getElementById("objective-reporting-frequency"),
        company_id: document.getElementById("objective-primary-company"),
        "metricassignments[0].parentmetricassignmentid": document.getElementById("objective-primary-kpi"),
        "metrics[0].yearlyvalues": document.getElementById("objective-target-plan-anchor"),
        "metrics[0].yearlyvalues.targetvalue": document.getElementById("objective-target-plan-anchor"),
        "metricassignments[0].yearlyvalues": document.getElementById("objective-target-plan-anchor"),
        "metricassignments[0].yearlyvalues.targetvalue": document.getElementById("objective-target-plan-anchor")
      });
      showErrors(backendErrors, fieldErrorMap(collectPayload()));
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(formRootEl);
    } finally {
      window.enterpriseModalFormUtils?.setSubmitting?.(saveBtn, false, isEditMode ? "Save Changes" : "Create Objective");
      applyValidation();
    }
  });

  densityDefaultBtn?.addEventListener("click", () => setTableDensity("default"));
  densityCompactBtn?.addEventListener("click", () => setTableDensity("compact"));

  bulkClearSelectionBtn?.addEventListener("click", () => clearSelection());
  bulkArchiveBtn?.addEventListener("click", async () => {
    const items = getSelectedItems();
    if (!items.length) return;
    const confirmed = await window.enterpriseStrategyUi?.confirm?.({
      title: "Archive selected objectives?",
      message: `Archive ${items.length} selected objective(s)?`,
      confirmLabel: "Archive",
      confirmKind: "danger"
    });
    if (!confirmed) return;
    let archived = 0;
    for (const item of items) {
      try {
        await window.strategyObjectivesApi.archive(item.id, item.version || 0);
        archived++;
      } catch (_) { }
    }
    clearSelection({ rerender: false });
    notify(`Archived ${archived} objective(s).`, archived === items.length ? "success" : "warning");
    await load();
  });

  exportCsvBtn?.addEventListener("click", () => {
    const rows = getSelectedItems();
    if (!rows.length) return notify("Select at least one objective to export.", "warning");
    const sheetRows = toObjectiveSheetRows(rows);
    if (window.enterpriseWorkbookIo?.exportCsv) {
      window.enterpriseWorkbookIo.exportCsv("objectives_selection.csv", sheetRows);
      return;
    }
    exportSelectionCsvFallback(sheetRows);
  });

  exportXlsxBtn?.addEventListener("click", () => {
    const rows = getSelectedItems();
    if (!rows.length) return notify("Select at least one objective to export.", "warning");
    if (!window.enterpriseWorkbookIo?.exportWorkbook) {
      notify("Excel export engine not loaded. Please hard refresh and retry.", "error");
      return;
    }
    window.enterpriseWorkbookIo.exportWorkbook("objectives_selection.xlsx", { Objectives: toObjectiveSheetRows(rows) });
  });

  exportWorkbookBtn?.addEventListener("click", () => {
    const rows = getSelectedItems();
    if (!rows.length) return notify("Select at least one objective to export.", "warning");
    if (!window.enterpriseWorkbookIo?.exportWorkbook) {
      notify("Workbook export engine not loaded. Please hard refresh and retry.", "error");
      return;
    }
    window.enterpriseWorkbookIo.exportWorkbook("objectives_selection.xlsx", { Objectives: toObjectiveSheetRows(rows) });
  });

  if (!isWorkspaceMode) {
    filters.search?.addEventListener("input", debounce(() => renderFiltered(true), 180));
    filters.apply?.addEventListener("click", () => renderFiltered(true));
    window.enterpriseTablePageUtils?.ensureResetButton?.("objectives", filters.apply, () => {
      Object.keys(filters).forEach((key) => {
        const el = filters[key];
        if (!el || key === "apply") return;
        el.value = "";
        if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) window.jQuery(el).trigger("change.select2");
      });
      renderFiltered(true);
    });
  }

  window.enterpriseModalFormUtils?.blockEnterSubmit?.(formRootEl);
  if (window.esbpHorizonDates?.initIn) window.esbpHorizonDates.initIn(formRootEl);

  if (isWorkspaceMode) {
    window.addEventListener("beforeunload", (event) => {
      if (!isDirty || suppressLeavePrompt) return;
      event.preventDefault();
      event.returnValue = "";
    });
  }

  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, isWorkspaceMode ? "Unable to initialize Objective workspace." : "Unable to load Objectives.")
      || (isWorkspaceMode ? "Unable to initialize Objective workspace." : "Unable to load Objectives.");
    if (isWorkspaceMode) showErrors([message], new Map());
    else notify(message, "error");
  });
})(window, document);
