(function (window, document) {
  "use strict";

  const root = document.getElementById("initiative-create-workspace");
  if (!root) return;

  const workbook = window.enterpriseWorkbookOptions || {};
  const notify = (message, kind = "success") => window.enterpriseStrategyUi?.notify?.(message, kind);
  const modalEl = document.getElementById("initiativeSourcePickerModal");
  const wizardButtons = Array.from(document.querySelectorAll("#initiative-wizard-steps .objective-wizard-step-btn"));
  const wizardPanes = Array.from(document.querySelectorAll(".initiative-wizard-step-pane"));
  const planBody = document.getElementById("initiative-contribution-plan-body");
  const planEmptyEl = document.getElementById("initiative-plan-empty");
  const planStatusChipEl = document.getElementById("initiative-plan-status-chip");
  const planContextEl = document.getElementById("initiative-plan-context");
  const readinessIndicatorEl = document.getElementById("initiative-readiness-indicator");
  const readinessTextEl = document.getElementById("initiative-readiness-text");
  const readinessMissingEl = document.getElementById("initiative-readiness-missing");
  const readinessSaveBlockersEl = document.getElementById("initiative-readiness-save-blockers");
  const readinessPlanningBlockersEl = document.getElementById("initiative-readiness-planning-blockers");
  const readinessWarningsEl = document.getElementById("initiative-readiness-warnings");
  const readinessDraftChipEl = document.getElementById("initiative-readiness-draft-chip");
  const readinessPlanChipEl = document.getElementById("initiative-readiness-plan-chip");
  const readinessPublishChipEl = document.getElementById("initiative-readiness-publish-chip");
  const readinessRowsChipEl = document.getElementById("initiative-readiness-rows-chip");
  const errorEl = document.getElementById("initiative-form-error");
  const saveBtn = document.getElementById("initiative-save");
  const nextBtn = document.getElementById("initiative-step-next");
  const backBtn = document.getElementById("initiative-step-back");

  const totalSteps = 5;
  let currentStep = 1;
  let currentVersion = 0;
  let sourceTemplateId = "";
  let sourceTemplateVersion = null;
  let templateRows = [];
  let selectedTemplateMeta = null;
  let objectivesCache = [];
  let baseInitiativeTypeOptions = [];
  let goalsById = new Map();
  let strategyPeriodsById = new Map();
  let objectiveTemplateNamesById = new Map();
  let strategyTemplateDetailsById = new Map();
  let currentObjective = null;
  let contributionPlanRows = [];
  let companyPickerActiveIndex = -1;
  let lastSourcePickerWarningKey = "";

  const editId = String(root.dataset.editId || "").trim();
  const prefillParentObjectiveId = String(root.dataset.prefillParentObjectiveId || "").trim();
  const isEditMode = Boolean(editId);
  const listUrl = "/management-governance/delivery-execution/initiatives";
  const detailUrl = (id) => `${listUrl}/${encodeURIComponent(String(id || "").trim())}`;

  const byId = (id) => document.getElementById(id);
  const cleanText = (value) => String(value ?? "").trim();
  const unique = (values) => Array.from(new Set((values || []).filter(Boolean)));
  const resolveOptions = (value, fallback = []) => {
    if (typeof value === "function") return value() || fallback;
    return Array.isArray(value) ? value : fallback;
  };
  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");

  function templateModalInstance() {
    if (!modalEl || !window.bootstrap?.Modal) return null;
    return window.bootstrap.Modal.getOrCreateInstance(modalEl);
  }

  function parseDate(value) {
    const text = cleanText(value);
    if (!text) return null;
    const date = new Date(text);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  function toIsoDate(value) {
    const date = value instanceof Date ? value : parseDate(value);
    if (!date) return "";
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }

  function formatDateRange(start, end) {
    const startText = toIsoDate(start);
    const endText = toIsoDate(end);
    if (!startText && !endText) return "";
    return `${startText || "?"} to ${endText || "?"}`;
  }

  function fromDateToYear(value) {
    const date = parseDate(value);
    return date ? String(date.getFullYear()) : "";
  }

  function formatStrategyPeriodLabel(goal) {
    const periodId = cleanText(goal?.strategyPeriodId || "");
    const period = strategyPeriodsById.get(periodId) || null;
    const code = cleanText(period?.code || goal?.strategyPeriodCode || "");
    const name = cleanText(period?.name || goal?.strategyPeriodName || "");
    const startYear = fromDateToYear(period?.startDate || goal?.startDate || "");
    const endYear = fromDateToYear(period?.endDate || goal?.endDate || "");
    const title = [code, name].filter(Boolean).join(" - ");
    const range = startYear && endYear ? `${startYear}-${endYear}` : "";
    if (title && range) return `${title} | ${range}`;
    return title || range || "";
  }

  function normalizeGoalRow(row) {
    const item = row || {};
    return {
      ...item,
      id: cleanText(item.id || item.goalId || item.goal_id || ""),
      name: cleanText(item.name || item.goalTitle || item.goalName || item.goal_name || ""),
      strategyPeriodId: cleanText(item.strategyPeriodId || item.planningCycle || item.planningCycleId || ""),
      strategyPeriodCode: cleanText(item.strategyPeriodCode || item.code || ""),
      strategyPeriodName: cleanText(item.strategyPeriodName || ""),
      startDate: item.startDate || item.planningHorizonStart || null,
      endDate: item.endDate || item.planningHorizonEnd || null,
    };
  }

  function normalizeObjectiveRow(row) {
    const item = row || {};
    return {
      ...item,
      id: cleanText(item.id || item.objectiveId || ""),
      name: cleanText(item.name || item.objectiveName || ""),
      parentGoalId: cleanText(item.parentGoalId || item.goalId || item.goal_id || ""),
      type: cleanText(item.type || item.objectiveTypeId || item.objectiveType || item.Type || item.ObjectiveTypeId || ""),
      owner: cleanText(item.owner || item.ownerId || ""),
      ownerCompanyId: cleanText(item.ownerCompanyId || item.OwnerCompanyId || item.primaryCompanyId || ""),
      ownerPositionId: cleanText(item.ownerPositionId || item.OwnerPositionId || ""),
      currentOwnerPersonId: cleanText(item.currentOwnerPersonId || item.CurrentOwnerPersonId || item.owner || item.ownerId || ""),
      executiveSponsor: cleanText(item.executiveSponsor || item.ExecutiveSponsor || item.executiveSponsorId || item.ExecutiveSponsorId || ""),
      primaryCompanyId: cleanText(item.primaryCompanyId || item.PrimaryCompanyId || item.ownerCompanyId || ""),
      sourceTemplateId: cleanText(item.sourceTemplateId || item.SourceTemplateId || ""),
      sourceTemplateType: cleanText(item.sourceTemplateType || item.SourceTemplateType || ""),
      sourceTemplateVersion: Number(item.sourceTemplateVersion ?? item.SourceTemplateVersion ?? 0) || 0,
      targetPlanGranularity: cleanText(item.targetPlanGranularity || item.TargetPlanGranularity || ""),
      targetPlanGranularityId: cleanText(item.targetPlanGranularityId || item.TargetPlanGranularityId || item.targetPlanGranularity || item.TargetPlanGranularity || ""),
      timeHorizonStart: item.timeHorizonStart || item.startDate || item.planningHorizonStart || null,
      timeHorizonEnd: item.timeHorizonEnd || item.endDate || item.planningHorizonEnd || null,
      startDate: item.startDate || item.timeHorizonStart || item.planningHorizonStart || null,
      endDate: item.endDate || item.timeHorizonEnd || item.planningHorizonEnd || null,
      entityScope: cleanText(item.entityScope || item.relatedEntityScope || item.EntityScope || item.RelatedEntityScope || ""),
      status: cleanText(item.status || item.Status || item.lifecycleStatus || item.LifecycleStatus || ""),
    };
  }

  function compatibleObjectiveTypesForInitiativeType(type) {
    const normalized = normalizedMatchKey(type);
    if (!normalized) return [];
    if (normalized.includes("compliance")) return ["Compliance", "Risk Reduction"];
    if (normalized.includes("cost optimization")) return ["Efficiency", "Sustainability", "Risk Reduction"];
    if (normalized.includes("improvement")) return ["Efficiency", "Customer Experience", "Capability Building"];
    if (normalized.includes("innovation")) return ["Innovation", "Growth", "Customer Experience"];
    if (normalized.includes("capability")) return ["Capability Building", "Efficiency", "Innovation", "Growth"];
    if (normalized.includes("transform")) return ["Growth", "Efficiency", "Customer Experience", "Capability Building", "Innovation", "Sustainability"];
    return [];
  }

  function isObjectiveCompatibleWithInitiativeType(objective, initiativeType) {
    const compatibleTypes = compatibleObjectiveTypesForInitiativeType(initiativeType);
    if (!compatibleTypes.length) return true;
    const objectiveType = normalizedMatchKey(objective?.type || "");
    return compatibleTypes.some((type) => normalizedMatchKey(type) === objectiveType);
  }

  function normalizeStatusKey(value) {
    return cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, "");
  }

  function currentObjectiveTemplateId() {
    return cleanText(currentObjective?.sourceTemplateId || "");
  }

  function hasParentObjectiveAnchor() {
    return Boolean(cleanText(byId("initiative-parent-objective")?.value));
  }

  function isTemplateStatusSelectable(status) {
    const normalized = normalizeStatusKey(status);
    if (!normalized) return true;
    if (["published", "active", "approved", "released", "live"].includes(normalized)) return true;
    return false;
  }

  function isInitiativeTypeCompatibleWithCurrentObjective(type) {
    if (!currentObjective) return true;
    return isObjectiveCompatibleWithInitiativeType(currentObjective, type);
  }

  function templateCompatibility(row) {
    if (!row) return { compatible: true, reasons: [] };

    const reasons = [];
    if (!currentObjective) {
      reasons.push("Select Parent Objective first to load compatible Initiative Templates.");
      return { compatible: false, reasons };
    }

    const rowType = cleanText(row.type || row.templateInitiativeType || "");
    const rowEntityScope = cleanText(row.entityScope || "");
    const selectedInitiativeType = cleanText(byId("initiative-type")?.value);
    const objectiveScope = resolvedObjectiveEntityScope();

    if (!isTemplateStatusSelectable(row.status)) {
      reasons.push("Template status is not active for Initiative creation.");
    }
    if (rowType && !isObjectiveCompatibleWithInitiativeType(currentObjective, rowType)) {
      reasons.push(`Template type ${rowType} is not compatible with Parent Objective type ${cleanText(currentObjective?.type || "-")}.`);
    }
    if (rowEntityScope && objectiveScope && !entityScopeMatchesObjective(objectiveScope, rowEntityScope)) {
      reasons.push(`Template entity scope ${rowEntityScope} does not match Parent Objective entity scope ${objectiveScope}.`);
    }
    if (selectedInitiativeType && rowType && normalizedMatchKey(selectedInitiativeType) !== normalizedMatchKey(rowType)) {
      reasons.push(`Template type ${rowType} does not match the selected Initiative Type ${selectedInitiativeType}.`);
    }

    return { compatible: reasons.length === 0, reasons };
  }

  function resolveParentObjectiveName(row) {
    const explicitName = cleanText(row?.parentObjectiveName || row?.ParentObjectiveName || "");
    if (explicitName) return explicitName;
    const templateId = cleanText(row?.parentObjectiveTemplateId || row?.ParentObjectiveTemplateId || "");
    if (!templateId) return "";
    const templateName = cleanText(objectiveTemplateNamesById.get(templateId) || "");
    if (templateName) return templateName;
    const match = objectivesCache.find((objective) => cleanText(objective?.sourceTemplateId || "") === templateId);
    return cleanText(match?.name || "");
  }

  function resolvedObjectiveEntityScope() {
    return cleanText(currentObjective?.entityScope || currentObjective?.relatedEntityScope || selectedGoal()?.entityScope || "");
  }

  function normalizedScopeKey(value) {
    return cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
  }

  function isInheritedEntityScopeText(value) {
    const normalized = normalizedScopeKey(value);
    return normalized === "inherited from parent goal" || normalized === "inherit from parent goal";
  }

  function entityScopeMatchesObjective(objectiveScope, initiativeScope) {
    const objectiveKey = normalizedScopeKey(objectiveScope);
    const initiativeKey = normalizedScopeKey(initiativeScope);
    if (!objectiveKey || !initiativeKey) return true;
    return objectiveKey === initiativeKey
      || objectiveKey.includes(initiativeKey)
      || initiativeKey.includes(objectiveKey);
  }

  function sourcePickerMismatchMessages() {
    const messages = [];
    const initiativeType = cleanText(byId("initiative-type")?.value);
    const initiativeScope = cleanText(byId("initiative-entity-scope")?.value);
    const objectiveType = cleanText(currentObjective?.type || "");
    const objectiveScope = resolvedObjectiveEntityScope();

    if (initiativeType && currentObjective && !isObjectiveCompatibleWithInitiativeType(currentObjective, initiativeType)) {
      messages.push(`Chosen Initiative Type ${initiativeType} does not match Parent Objective Type ${objectiveType || "-"}.`);
    }
    if (initiativeScope && objectiveScope && !entityScopeMatchesObjective(objectiveScope, initiativeScope)) {
      messages.push(`Chosen Initiative Entity Scope ${initiativeScope} does not match Parent Objective Entity Scope ${objectiveScope}.`);
    }

    return messages;
  }

  function normalizeGranularity(value) {
    const normalized = cleanText(value).toLowerCase().replace(/\s+/g, "");
    if (normalized === "monthly") return "Monthly";
    if (normalized === "quarterly") return "Quarterly";
    if (normalized === "yearly") return "Yearly";
    if (normalized === "totalinitiativehorizon" || normalized === "totalstrategyperiod" || normalized === "total") return "TotalInitiativeHorizon";
    return "InheritFromObjective";
  }

  function refreshParentObjectiveOptions() {
    const current = cleanText(byId("initiative-parent-objective")?.value);
    const initiativeType = cleanText(byId("initiative-type")?.value);
    const currentObjectiveRow = objectivesCache.find((objective) => cleanText(objective?.id) === current) || null;
    fillSelect("initiative-parent-objective", objectiveOptions(), "Select parent objective");
    if (!current) return;
    if (!isEditMode && initiativeType && !isObjectiveCompatibleWithInitiativeType(currentObjectiveRow, initiativeType)) {
      byId("initiative-parent-objective").value = "";
      currentObjective = null;
      applyObjectiveContext();
      return;
    }
    const stillAllowed = objectiveOptions().some((option) => cleanText(option.value) === current);
    if (stillAllowed) {
      byId("initiative-parent-objective").value = current;
      return;
    }
    byId("initiative-parent-objective").value = "";
    currentObjective = null;
    applyObjectiveContext();
  }

  function refreshInitiativeTypeOptions() {
    const current = cleanText(byId("initiative-type")?.value);
    const el = byId("initiative-type");
    if (!el) return;

    const options = currentObjective
      ? baseInitiativeTypeOptions.filter((item) => {
        const value = cleanText(item?.value ?? item);
        if (isEditMode && value === current) return true;
        return !value || isInitiativeTypeCompatibleWithCurrentObjective(value);
      })
      : [];

    fillSelect("initiative-type", options, currentObjective ? "Select type" : "Select Parent Objective first");
    if (!currentObjective) {
      if (!isEditMode) el.value = "";
      el.disabled = true;
      if (byId("initiative-type-help")) byId("initiative-type-help").textContent = "Select Parent Objective first to load compatible Initiative Types.";
      return;
    }

    const stillAllowed = options.some((item) => cleanText(item?.value ?? item) === current);
    if (stillAllowed) {
      ensureSelectValue("initiative-type", current, current);
      el.value = current;
    } else if (!isEditMode) {
      el.value = "";
    } else if (current) {
      ensureSelectValue("initiative-type", current, current);
      el.value = current;
    }

    el.disabled = options.length === 0;
    if (byId("initiative-type-help")) {
      byId("initiative-type-help").textContent = options.length
        ? `Compatible Initiative Types are filtered by Parent Objective${cleanText(currentObjective?.type) ? ` (${cleanText(currentObjective.type)})` : ""}.`
        : "No compatible Initiative Types are currently available for the selected Parent Objective.";
    }
  }

  function granularityRank(value) {
    const normalized = normalizeGranularity(value);
    if (normalized === "Monthly") return 1;
    if (normalized === "Quarterly") return 2;
    if (normalized === "Yearly") return 3;
    return 4;
  }

  function effectiveGranularity() {
    const selection = normalizeGranularity(byId("initiative-contribution-granularity")?.value || "InheritFromObjective");
    if (selection !== "InheritFromObjective") return selection;
    const objectiveGranularity = normalizeGranularity(currentObjective?.targetPlanGranularity || currentObjective?.targetPlanGranularityId || "");
    return objectiveGranularity === "InheritFromObjective" ? "Yearly" : objectiveGranularity;
  }

  function syncContributionGranularityConstraint({ notifyUser = false } = {}) {
    const el = byId("initiative-contribution-granularity");
    if (!el) return;
    const objectiveGranularity = normalizeGranularity(currentObjective?.targetPlanGranularity || currentObjective?.targetPlanGranularityId || "");
    if (!objectiveGranularity || objectiveGranularity === "InheritFromObjective") return;

    const currentSelection = normalizeGranularity(el.value || "InheritFromObjective");
    const isCoarser = currentSelection !== "InheritFromObjective"
      && granularityRank(currentSelection) > granularityRank(objectiveGranularity);

    if (isCoarser) {
      el.value = "InheritFromObjective";
      el.dispatchEvent(new Event("change", { bubbles: true }));
      if (notifyUser) {
        notify("Contribution Plan Granularity was reset to Inherit from Objective because it cannot be coarser than the Parent Objective target plan.", "warning");
      }
    }
  }

  function objectiveOptionLabel(item) {
    const id = cleanText(item?.id || "");
    const name = cleanText(item?.name || "");
    const type = cleanText(item?.type || "");
    const scope = cleanText(item?.entityScope || item?.relatedEntityScope || "");
    const endDate = toIsoDate(item?.timeHorizonEnd || item?.endDate || "");
    const parts = [
      `${id} - ${name}`,
      type ? `Type: ${type}` : "",
      scope ? `Entity Scope: ${scope}` : "",
      endDate ? `End: ${endDate}` : ""
    ].filter(Boolean);
    return parts.join(" | ");
  }

  function objectiveOptions() {
    const initiativeType = cleanText(byId("initiative-type")?.value);
    const selectedObjectiveId = cleanText(byId("initiative-parent-objective")?.value);
    return objectivesCache
      .filter((item) => {
        if (cleanText(item?.id) === selectedObjectiveId && isEditMode) return true;
        if (normalizeStatusKey(item?.status) === "archived") return false;
        return isObjectiveCompatibleWithInitiativeType(item, initiativeType);
      })
      .map((item) => ({
      value: item.id,
      label: objectiveOptionLabel(item),
    }));
  }

  function companyOptions() {
    return resolveOptions(workbook.companyOptions);
  }

  function userOptions() {
    return resolveOptions(workbook.userOptions);
  }

  function positionOptions() {
    return resolveOptions(workbook.positionOptions);
  }

  function normalizePersonOptions(rows) {
    return (rows || [])
      .map((row) => ({
        value: cleanText(row?.id || row?.value || ""),
        label: cleanText(row?.fullName || row?.label || row?.name || row?.value || ""),
      }))
      .filter((row) => row.value && row.label);
  }

  function scopedPositionOptions(companyId) {
    const scoped = companyId && typeof workbook.positionOptionsForCompany === "function"
      ? (workbook.positionOptionsForCompany(companyId) || [])
      : [];
    return scoped.length ? scoped : positionOptions();
  }

  function scopedPeopleResult(companyId, roleId) {
    if (!companyId || typeof workbook.usersForOwnershipContext !== "function") {
      return { options: [], usedCompanyFallback: false };
    }

    const exactMatches = normalizePersonOptions(workbook.usersForOwnershipContext(companyId, roleId || "", { activeOnly: false }) || []);
    if (exactMatches.length || !roleId) {
      return { options: exactMatches, usedCompanyFallback: false };
    }

    const companyEmployees = normalizePersonOptions(workbook.usersForOwnershipContext(companyId, "", { activeOnly: false }) || []);
    return { options: companyEmployees, usedCompanyFallback: companyEmployees.length > 0 };
  }

  function scopedPeopleOptions(companyId, roleId) {
    return scopedPeopleResult(companyId, roleId).options;
  }

  function normalizedMatchKey(value) {
    return cleanText(value).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
  }

  function currentStatusValue() {
    return cleanText(byId("initiative-status-editable")?.value || byId("initiative-status")?.value || "Draft") || "Draft";
  }

  function requiresSponsorValidationForLifecycle() {
    const status = currentStatusValue().toLowerCase();
    return !["", "draft", "proposed", "planned"].includes(status);
  }

  function setSelectIfBlank(id, value, label) {
    const el = byId(id);
    const normalizedValue = cleanText(value);
    if (!el || cleanText(el.value) || !normalizedValue) return false;
    ensureSelectValue(id, normalizedValue, cleanText(label) || normalizedValue);
    return true;
  }

  function preferredSponsorRoleCandidatesForInitiativeType(type) {
    const normalized = normalizedMatchKey(type);
    if (normalized.includes("compliance") || normalized.includes("risk")) {
      return ["Chief Compliance Officer", "Chief Risk Officer", "General Counsel", "Chief Executive Officer"];
    }
    if (normalized.includes("innovation") || normalized.includes("technology")) {
      return ["Chief Technology Officer", "Chief Innovation Officer", "Chief Information Officer", "Chief Executive Officer"];
    }
    if (normalized.includes("capability")) {
      return ["Chief Operating Officer", "Chief Human Resources Officer", "Transformation Director", "Chief Executive Officer"];
    }
    if (normalized.includes("cost") || normalized.includes("efficiency") || normalized.includes("improvement")) {
      return ["Chief Operating Officer", "Chief Financial Officer", "Operations Director", "Chief Executive Officer"];
    }
    if (normalized.includes("transform")) {
      return ["Transformation Director", "Chief Operating Officer", "Chief Executive Officer", "Managing Director"];
    }
    return ["Executive Sponsor", "Chief Executive Officer", "Managing Director", "Chief Operating Officer"];
  }

  function preferredSponsorRoleCandidatesForOwnerRole(role) {
    const normalized = normalizedMatchKey(role);
    if (!normalized) return [];
    if (normalized.includes("chief") || normalized.includes("vice president") || normalized.includes("vp")) return [cleanText(role)];
    if (normalized.includes("director") || normalized.includes("head")) {
      return ["Vice President", "Chief Operating Officer", "Chief Executive Officer"];
    }
    if (normalized.includes("manager") || normalized.includes("lead")) {
      return ["Director", "Vice President", "Chief Operating Officer", "Chief Executive Officer"];
    }
    return ["Chief Operating Officer", "Chief Executive Officer"];
  }

  function resolveBestPositionOption(companyId, candidates) {
    const options = scopedPositionOptions(companyId);
    if (!companyId || !options.length || !(candidates || []).length) return null;
    const normalizedCandidates = unique((candidates || []).map(normalizedMatchKey).filter(Boolean));
    return options.find((option) => {
      const optionValue = normalizedMatchKey(option?.value || "");
      const optionLabel = normalizedMatchKey(option?.label || "");
      return normalizedCandidates.some((candidate) =>
        candidate === optionValue
        || candidate === optionLabel
        || optionLabel.includes(candidate)
        || optionValue.includes(candidate)
        || candidate.includes(optionLabel)
      );
    }) || null;
  }

  function findUserOptionInContext(companyId, roleId, personCandidate) {
    const candidate = cleanText(personCandidate);
    if (!companyId || !roleId || !candidate) return null;
    const normalizedCandidate = normalizedMatchKey(workbook.userId?.(candidate) || candidate);
    const options = scopedPeopleOptions(companyId, roleId);
    return options.find((option) => {
      const optionValue = normalizedMatchKey(option.value);
      const optionLabel = normalizedMatchKey(option.label);
      return normalizedCandidate === optionValue
        || normalizedCandidate === optionLabel
        || optionLabel.includes(normalizedCandidate)
        || optionValue.includes(normalizedCandidate);
    }) || null;
  }

  function applySponsorPrefill() {
    const sponsorCompanyId = cleanText(byId("initiative-sponsoring-company")?.value);
    const currentSponsorRole = cleanText(byId("initiative-sponsor-role")?.value);
    const currentSponsorPerson = cleanText(byId("initiative-executive-sponsor")?.value);

    const objectiveSponsorCompanyId = cleanText(currentObjective?.primaryCompanyId || currentObjective?.ownerCompanyId || "");
    const templateSponsorRole = cleanText(selectedTemplateMeta?.sponsorRole || "");
    const templateSponsorPerson = cleanText(selectedTemplateMeta?.sponsorPerson || "");
    const objectiveSponsorPerson = cleanText(currentObjective?.executiveSponsor || currentObjective?.currentOwnerPersonId || currentObjective?.owner || "");
    const objectiveSponsorRole = cleanText(currentObjective?.ownerPositionId || "");

    if (!sponsorCompanyId && objectiveSponsorCompanyId) {
      setSelectIfBlank("initiative-sponsoring-company", objectiveSponsorCompanyId, workbook.companyDisplayName?.(objectiveSponsorCompanyId) || objectiveSponsorCompanyId);
    }

    const resolvedSponsorCompanyId = cleanText(byId("initiative-sponsoring-company")?.value);
    if (!resolvedSponsorCompanyId) return;
    refreshSponsorRoleOptions();

    let sponsorRole = currentSponsorRole;
    if (!sponsorRole && templateSponsorRole) {
      const templateRoleOption = resolveBestPositionOption(resolvedSponsorCompanyId, [templateSponsorRole]);
      if (templateRoleOption && setSelectIfBlank("initiative-sponsor-role", templateRoleOption.value, templateRoleOption.label)) {
        sponsorRole = templateRoleOption.value;
      }
    }

    if (!sponsorRole && objectiveSponsorRole) {
      const objectiveRoleOption = resolveBestPositionOption(resolvedSponsorCompanyId, [objectiveSponsorRole]);
      if (objectiveRoleOption && setSelectIfBlank("initiative-sponsor-role", objectiveRoleOption.value, objectiveRoleOption.label)) {
        sponsorRole = objectiveRoleOption.value;
      }
    }

    if (!sponsorRole) {
      const companyDefaultRole = resolveBestPositionOption(resolvedSponsorCompanyId, preferredSponsorRoleCandidatesForInitiativeType(byId("initiative-type")?.value || currentObjective?.type || ""));
      if (companyDefaultRole && setSelectIfBlank("initiative-sponsor-role", companyDefaultRole.value, companyDefaultRole.label)) {
        sponsorRole = companyDefaultRole.value;
      }
    }

    if (!sponsorRole) {
      const mappedRole = resolveBestPositionOption(resolvedSponsorCompanyId, preferredSponsorRoleCandidatesForOwnerRole(byId("initiative-owner-role")?.value || ""));
      if (mappedRole && setSelectIfBlank("initiative-sponsor-role", mappedRole.value, mappedRole.label)) {
        sponsorRole = mappedRole.value;
      }
    }

    if (cleanText(byId("initiative-sponsor-role")?.value) !== currentSponsorRole) {
      refreshSponsorRoleOptions();
    }

    sponsorRole = cleanText(byId("initiative-sponsor-role")?.value);
    if (!currentSponsorPerson && sponsorRole) {
      const templatePersonOption = findUserOptionInContext(resolvedSponsorCompanyId, sponsorRole, templateSponsorPerson);
      if (templatePersonOption && setSelectIfBlank("initiative-executive-sponsor", templatePersonOption.value, templatePersonOption.label)) {
        refreshSponsorPersonOptions();
        return;
      }

      const objectivePersonOption = findUserOptionInContext(resolvedSponsorCompanyId, sponsorRole, objectiveSponsorPerson);
      if (objectivePersonOption && setSelectIfBlank("initiative-executive-sponsor", objectivePersonOption.value, objectivePersonOption.label)) {
        refreshSponsorPersonOptions();
        return;
      }

      const incumbent = workbook.resolveActiveIncumbent?.(resolvedSponsorCompanyId, sponsorRole);
      const incumbentId = cleanText(incumbent?.id || incumbent?.value || "");
      const incumbentLabel = cleanText(incumbent?.fullName || incumbent?.label || incumbentId);
      if (incumbentId) {
        setSelectIfBlank("initiative-executive-sponsor", incumbentId, incumbentLabel);
      }
    }

    refreshSponsorPersonOptions();
  }

  function fillSelect(id, options, placeholder, extra = {}) {
    const el = byId(id);
    if (!el) return;
    workbook.fillSelect?.(el, options, {
      placeholder: placeholder || "Select",
      keepCurrent: true,
      ...extra
    });
  }

  function ensureSelectValue(id, value, label) {
    const el = byId(id);
    const normalizedValue = cleanText(value);
    if (!el || !normalizedValue) return;
    const hasExisting = Array.from(el.options || []).some((opt) => cleanText(opt.value) === normalizedValue);
    if (!hasExisting) {
      const option = document.createElement("option");
      option.value = normalizedValue;
      option.textContent = cleanText(label) || normalizedValue;
      el.appendChild(option);
    }
    el.value = normalizedValue;
  }

  function fillMultiSelect(id, options, selectedValues) {
    const el = byId(id);
    if (!el) return;
    const selected = new Set((selectedValues || []).map((value) => cleanText(value)).filter(Boolean));
    const rows = [];
    (options || []).forEach((item) => {
      const value = cleanText(item?.value ?? item);
      const label = cleanText(item?.label ?? item);
      if (!value || !label) return;
      rows.push(`<option value="${escapeHtml(value)}"${selected.has(value) ? " selected" : ""}>${escapeHtml(label)}</option>`);
    });
    el.innerHTML = rows.join("");
  }

  function selectedText(id) {
    const el = byId(id);
    if (!el) return "";
    const option = el.selectedOptions?.[0];
    return cleanText(option?.textContent || option?.label || "");
  }

  function renderErrors(errors) {
    if (!errorEl) return;
    const entries = Object.entries(errors || {}).flatMap(([, messages]) => Array.isArray(messages) ? messages : [messages]);
    if (!entries.length) {
      errorEl.classList.add("d-none");
      errorEl.innerHTML = "";
      return;
    }
    errorEl.classList.remove("d-none");
    errorEl.innerHTML = `<div class="alert alert-danger mb-0"><ul class="mb-0">${entries.map((message) => `<li>${escapeHtml(message)}</li>`).join("")}</ul></div>`;
  }

  function selectedGoal() {
    const goalId = cleanText(currentObjective?.parentGoalId || currentObjective?.goalId || "");
    return goalId ? goalsById.get(goalId) || null : null;
  }

  function currentMode() {
    return cleanText(byId("initiative-creation-mode-select")?.value || "Blank") || "Blank";
  }

  function modeLabel() {
    const mode = currentMode();
    if (mode === "Template") return "From Initiative Template";
    if (mode === "ObjectiveTemplate") return "From Objective + Initiative Template";
    return "Blank";
  }

  function syncStatusInputs() {
    const status = cleanText(byId("initiative-status-editable")?.value || byId("initiative-status")?.value || "Draft") || "Draft";
    if (byId("initiative-status")) byId("initiative-status").value = status;
    if (byId("initiative-status-readonly")) byId("initiative-status-readonly").value = status;
  }

  function applyObjectiveContext() {
    const parentGoal = selectedGoal();
    const objectiveScope = resolvedObjectiveEntityScope();
    byId("initiative-parent-goal").value = parentGoal ? `${parentGoal.id} - ${parentGoal.name}` : "";
    byId("initiative-strategy-period").value = formatStrategyPeriodLabel(parentGoal);
    byId("initiative-objective-type").value = cleanText(currentObjective?.type || "");
    const objectiveGranularity = cleanText(currentObjective?.targetPlanGranularity || currentObjective?.targetPlanGranularityId || "");
    byId("initiative-objective-granularity").value = objectiveGranularity || "-";
    byId("initiative-objective-horizon").value = formatDateRange(currentObjective?.timeHorizonStart || currentObjective?.startDate, currentObjective?.timeHorizonEnd || currentObjective?.endDate);
    byId("initiative-objective-entity-scope").value = objectiveScope;
    if (!cleanText(byId("initiative-entity-scope")?.value) && objectiveScope) {
      byId("initiative-entity-scope").value = objectiveScope;
    }
    syncDateConstraints();
    syncContributionGranularityConstraint();
    refreshInitiativeTypeOptions();
    syncStepOneUi();
    if (currentMode() === "ObjectiveTemplate") renderSourceSummary(selectedTemplateMeta);
    applySponsorPrefill();
    renderReadiness();
  }

  async function hydrateCurrentObjective(objectiveId) {
    const normalizedId = cleanText(objectiveId);
    if (!normalizedId) {
      currentObjective = null;
      return null;
    }

    const index = objectivesCache.findIndex((objective) => cleanText(objective?.id) === normalizedId);
    let objective = index >= 0 ? normalizeObjectiveRow(objectivesCache[index]) : null;
    if (!objective) return null;

    const needsDetail = !cleanText(objective.parentGoalId)
      || !cleanText(objective.sourceTemplateId)
      || !cleanText(objective.entityScope)
      || !cleanText(objective.targetPlanGranularity || objective.targetPlanGranularityId)
      || (!objective.timeHorizonStart && !objective.startDate)
      || (!objective.timeHorizonEnd && !objective.endDate);

    if (needsDetail) {
      try {
        const detail = await window.strategyObjectivesApi.get(normalizedId);
        const detailedObjective = normalizeObjectiveRow(detail?.objective || detail || {});
        if (cleanText(detailedObjective.id) === normalizedId) {
          objective = { ...objective, ...detailedObjective };
          if (index >= 0) objectivesCache[index] = objective;
        }
        const detailedGoal = normalizeGoalRow(detail?.parentGoal || {});
        if (cleanText(detailedGoal.id)) goalsById.set(detailedGoal.id, detailedGoal);
      } catch (_) {
      }
    }

    const sourceTemplateId = cleanText(objective?.sourceTemplateId || "");
    const needsExactEntityScope = sourceTemplateId && (!cleanText(objective?.entityScope) || isInheritedEntityScopeText(objective?.entityScope));
    if (needsExactEntityScope && window.strategyLibraryApi?.template) {
      try {
        let templateDetail = strategyTemplateDetailsById.get(sourceTemplateId) || null;
        if (!templateDetail) {
          templateDetail = await window.strategyLibraryApi.template(sourceTemplateId);
          strategyTemplateDetailsById.set(sourceTemplateId, templateDetail || {});
        }
        const templateScope = cleanText(
          templateDetail?.entityScope
          || templateDetail?.EntityScope
          || templateDetail?.objectivePrefill?.entityScope
          || templateDetail?.ObjectivePrefill?.EntityScope
          || templateDetail?.ObjectivePrefill?.entityScope
          || templateDetail?.attributes?.EntityScope
          || templateDetail?.Attributes?.EntityScope
          || ""
        );
        if (templateScope && !isInheritedEntityScopeText(templateScope)) {
          objective = { ...objective, entityScope: templateScope };
          if (index >= 0) objectivesCache[index] = objective;
        }
      } catch (_) {
      }
    }

    currentObjective = objective;
    return objective;
  }

  function objectiveBounds() {
    const objective = currentObjective;
    const goal = selectedGoal();
    const objectiveStart = parseDate(objective?.timeHorizonStart || objective?.startDate);
    const objectiveEnd = parseDate(objective?.timeHorizonEnd || objective?.endDate);
    const goalStart = parseDate(goal?.startDate);
    const goalEnd = parseDate(goal?.endDate);
    const initiativeStart = parseDate(byId("initiative-start-date")?.value);
    const initiativeEnd = parseDate(byId("initiative-end-date")?.value);
    const starts = [initiativeStart, objectiveStart, goalStart].filter(Boolean);
    const ends = [initiativeEnd, objectiveEnd, goalEnd].filter(Boolean);
    if (!initiativeStart || !initiativeEnd || !starts.length || !ends.length) return null;
    const start = new Date(Math.max.apply(null, starts.map((item) => item.getTime())));
    const end = new Date(Math.min.apply(null, ends.map((item) => item.getTime())));
    return end >= start ? { start, end } : null;
  }

  function initiativeDateGuardrails() {
    const objective = currentObjective;
    const goal = selectedGoal();
    const objectiveStart = parseDate(objective?.timeHorizonStart || objective?.startDate);
    const objectiveEnd = parseDate(objective?.timeHorizonEnd || objective?.endDate);
    const goalStart = parseDate(goal?.startDate);
    const goalEnd = parseDate(goal?.endDate);
    const starts = [objectiveStart, goalStart].filter(Boolean);
    const ends = [objectiveEnd, goalEnd].filter(Boolean);
    if (!starts.length && !ends.length) return null;
    const start = starts.length ? new Date(Math.max.apply(null, starts.map((item) => item.getTime()))) : null;
    const end = ends.length ? new Date(Math.min.apply(null, ends.map((item) => item.getTime()))) : null;
    if (start && end && end < start) return null;
    return { start, end };
  }

  function applyDateLimits(inputId, minDate, maxDate) {
    const el = byId(inputId);
    if (!el) return;
    const minText = toIsoDate(minDate);
    const maxText = toIsoDate(maxDate);
    if (minText) el.min = minText; else el.removeAttribute("min");
    if (maxText) el.max = maxText; else el.removeAttribute("max");
    const currentValue = cleanText(el.value);
    if (!currentValue) return;
    if ((minText && currentValue < minText) || (maxText && currentValue > maxText)) {
      el.value = "";
    }
  }

  function syncDateConstraints() {
    const guardrails = initiativeDateGuardrails();
    const initiativeStartEl = byId("initiative-start-date");
    const initiativeEndEl = byId("initiative-end-date");
    const initiativeStart = parseDate(initiativeStartEl?.value);
    const initiativeEnd = parseDate(initiativeEndEl?.value);

    applyDateLimits("initiative-start-date", guardrails?.start || null, initiativeEnd || guardrails?.end || null);
    applyDateLimits("initiative-end-date", initiativeStart || guardrails?.start || null, guardrails?.end || null);

    const benefitMin = parseDate(byId("initiative-start-date")?.value) || guardrails?.start || null;
    const benefitMax = parseDate(byId("initiative-end-date")?.value) || guardrails?.end || null;
    applyDateLimits("initiative-benefit-start", benefitMin, benefitMax);
    applyDateLimits("initiative-benefit-end", benefitMin, benefitMax);
  }

  function buildPeriods(start, end, granularity) {
    const rows = [];
    const effective = normalizeGranularity(granularity);
    if (effective === "TotalInitiativeHorizon") {
      rows.push({
        periodKey: `${toIsoDate(start)}:${toIsoDate(end)}`,
        periodLabel: "Total Initiative Horizon",
        periodStart: toIsoDate(start),
        periodEnd: toIsoDate(end),
      });
      return rows;
    }

    if (effective === "Monthly") {
      const cursor = new Date(start.getFullYear(), start.getMonth(), 1);
      while (cursor <= end) {
        const periodStart = new Date(Math.max(cursor.getTime(), start.getTime()));
        const monthEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0);
        const periodEnd = new Date(Math.min(monthEnd.getTime(), end.getTime()));
        rows.push({
          periodKey: `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, "0")}`,
          periodLabel: `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, "0")}`,
          periodStart: toIsoDate(periodStart),
          periodEnd: toIsoDate(periodEnd),
        });
        cursor.setMonth(cursor.getMonth() + 1);
      }
      return rows;
    }

    if (effective === "Quarterly") {
      const cursor = new Date(start.getFullYear(), Math.floor(start.getMonth() / 3) * 3, 1);
      while (cursor <= end) {
        const quarter = Math.floor(cursor.getMonth() / 3) + 1;
        const periodStart = new Date(Math.max(cursor.getTime(), start.getTime()));
        const quarterEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 3, 0);
        const periodEnd = new Date(Math.min(quarterEnd.getTime(), end.getTime()));
        rows.push({
          periodKey: `${cursor.getFullYear()}-Q${quarter}`,
          periodLabel: `${cursor.getFullYear()}-Q${quarter}`,
          periodStart: toIsoDate(periodStart),
          periodEnd: toIsoDate(periodEnd),
        });
        cursor.setMonth(cursor.getMonth() + 3);
      }
      return rows;
    }

    for (let year = start.getFullYear(); year <= end.getFullYear(); year += 1) {
      const yearStart = new Date(Math.max(new Date(year, 0, 1).getTime(), start.getTime()));
      const yearEnd = new Date(Math.min(new Date(year, 11, 31).getTime(), end.getTime()));
      rows.push({
        periodKey: String(year),
        periodLabel: String(year),
        periodStart: toIsoDate(yearStart),
        periodEnd: toIsoDate(yearEnd),
      });
    }
    return rows;
  }

  function periodMap(rows) {
    return new Map((rows || []).map((row) => [cleanText(row.periodKey), row]));
  }

  function renderContributionPlan() {
    if (!planBody) return;
    planBody.innerHTML = "";
    const hasRows = contributionPlanRows.length > 0;
    if (planEmptyEl) planEmptyEl.classList.toggle("d-none", hasRows);
    if (planStatusChipEl) planStatusChipEl.textContent = hasRows ? `Plan: ${contributionPlanRows.length} rows` : "Plan: Not generated";

    contributionPlanRows.forEach((row, index) => {
      const tr = document.createElement("tr");
      tr.innerHTML = [
        `<td><strong>${escapeHtml(row.periodLabel)}</strong></td>`,
        `<td>${escapeHtml(row.periodStart)}</td>`,
        `<td>${escapeHtml(row.periodEnd)}</td>`,
        `<td><input class="form-control form-control-sm" data-field="plannedValue" data-index="${index}" type="number" step="0.01" value="${row.plannedValue ?? ""}" /></td>`,
        `<td><input class="form-control form-control-sm" data-field="forecastValue" data-index="${index}" type="number" step="0.01" value="${row.forecastValue ?? ""}" /></td>`,
        `<td><input class="form-control form-control-sm" data-field="actualValue" data-index="${index}" type="number" step="0.01" value="${row.actualValue ?? ""}" /></td>`,
        `<td><textarea class="form-control form-control-sm" data-field="commentary" data-index="${index}" rows="2">${escapeHtml(row.commentary || "")}</textarea></td>`
      ].join("");
      planBody.appendChild(tr);
    });

    planBody.querySelectorAll("[data-field]").forEach((input) => {
      input.addEventListener("input", () => {
        const index = Number(input.dataset.index || -1);
        const field = String(input.dataset.field || "");
        if (!contributionPlanRows[index]) return;
        contributionPlanRows[index][field] = field === "commentary"
          ? input.value
          : (input.value === "" ? null : Number(input.value));
        renderReadiness();
      });
    });
  }

  function generateContributionPlan(preserveExisting) {
    const bounds = objectiveBounds();
    if (!bounds) {
      planContextEl.textContent = "Set Initiative Start and End Periods inside the parent Objective horizon before generating contribution rows.";
      renderReadiness();
      return;
    }
    const existing = preserveExisting ? periodMap(contributionPlanRows) : new Map();
    planContextEl.textContent = `Allowed contribution horizon: ${formatDateRange(bounds.start, bounds.end)}.`;
    contributionPlanRows = buildPeriods(bounds.start, bounds.end, effectiveGranularity()).map((row) => {
      const previous = existing.get(cleanText(row.periodKey));
      return {
        ...row,
        plannedValue: previous?.plannedValue ?? null,
        forecastValue: previous?.forecastValue ?? null,
        actualValue: previous?.actualValue ?? null,
        commentary: previous?.commentary || "",
      };
    });
    renderContributionPlan();
    renderReadiness();
  }

  function fillFlatPlan() {
    const value = cleanText(byId("initiative-plan-flat-value")?.value);
    if (!contributionPlanRows.length || value === "") return;
    const numericValue = Number(value);
    if (Number.isNaN(numericValue)) return;
    contributionPlanRows = contributionPlanRows.map((row) => ({ ...row, plannedValue: numericValue }));
    renderContributionPlan();
    renderReadiness();
  }

  function copyDownPlan() {
    if (!contributionPlanRows.length) return;
    let carry = null;
    contributionPlanRows = contributionPlanRows.map((row) => {
      if (row.plannedValue !== null && row.plannedValue !== undefined && !Number.isNaN(row.plannedValue)) {
        carry = row.plannedValue;
        return row;
      }
      return carry === null ? row : { ...row, plannedValue: carry };
    });
    renderContributionPlan();
    renderReadiness();
  }

  function interpolatePlan() {
    if (contributionPlanRows.length < 2) return;
    const rows = contributionPlanRows.map((row) => ({ ...row }));
    const anchors = rows
      .map((row, index) => ({ index, value: row.plannedValue }))
      .filter((item) => item.value !== null && item.value !== undefined && !Number.isNaN(item.value));
    if (anchors.length < 2) return;
    for (let i = 0; i < anchors.length - 1; i += 1) {
      const start = anchors[i];
      const end = anchors[i + 1];
      const distance = end.index - start.index;
      if (distance <= 1) continue;
      const step = (end.value - start.value) / distance;
      for (let cursor = start.index + 1; cursor < end.index; cursor += 1) {
        rows[cursor].plannedValue = Number((start.value + (step * (cursor - start.index))).toFixed(2));
      }
    }
    contributionPlanRows = rows;
    renderContributionPlan();
    renderReadiness();
  }

  function selectedParticipatingCompanies() {
    const select = byId("initiative-participating-companies-select");
    return Array.from(select?.selectedOptions || []).map((option) => cleanText(option.value)).filter(Boolean);
  }

  function readinessSnapshot() {
    const missing = [];
    const saveBlockers = [];
    const planningBlockers = [];
    const warnings = [];
    const startDate = parseDate(byId("initiative-start-date")?.value);
    const endDate = parseDate(byId("initiative-end-date")?.value);
    const benefitStart = parseDate(byId("initiative-benefit-start")?.value);
    const benefitEnd = parseDate(byId("initiative-benefit-end")?.value);
    const granularity = effectiveGranularity();
    const objectiveGranularity = normalizeGranularity(currentObjective?.targetPlanGranularity || currentObjective?.targetPlanGranularityId || "");
    const objectiveStart = parseDate(currentObjective?.timeHorizonStart || currentObjective?.startDate);
    const objectiveEnd = parseDate(currentObjective?.timeHorizonEnd || currentObjective?.endDate);
    const goal = selectedGoal();
    const initiativeType = cleanText(byId("initiative-type")?.value);
    const goalStart = parseDate(goal?.startDate);
    const goalEnd = parseDate(goal?.endDate);

    if (!cleanText(byId("initiative-parent-objective")?.value)) missing.push("Parent Objective");
    if (!cleanText(byId("initiative-name")?.value)) missing.push("Initiative Name");
    if (!cleanText(byId("initiative-type")?.value)) missing.push("Initiative Type");
    if (!cleanText(byId("initiative-sponsoring-company")?.value)) missing.push("Sponsoring Company");
    if (!startDate) missing.push("Start Period");
    if (!endDate) missing.push("End Period");

    if (requiresSponsorValidationForLifecycle()) {
      if (!cleanText(byId("initiative-sponsor-role")?.value)) saveBlockers.push("Sponsor Role is required before activation / publish.");
      if (!cleanText(byId("initiative-executive-sponsor")?.value)) saveBlockers.push("Accountable Sponsor is required before activation / publish.");
    }

    if (startDate && endDate && endDate < startDate) saveBlockers.push("End Period must be on or after Start Period.");
    if (cleanText(byId("initiative-parent-objective")?.value) && !cleanText(goal?.id || "")) saveBlockers.push("Parent Goal could not be derived from the selected Parent Objective.");
    if (initiativeType && currentObjective && !isObjectiveCompatibleWithInitiativeType(currentObjective, initiativeType)) {
      saveBlockers.push("Initiative Type is not compatible with the selected Parent Objective.");
    }
    if (sourceTemplateId && selectedTemplateMeta) {
      saveBlockers.push(...templateCompatibility(selectedTemplateMeta).reasons);
    }
    if (startDate && objectiveStart && startDate < objectiveStart) saveBlockers.push("Initiative Start Period sits before the Parent Objective horizon.");
    if (endDate && objectiveEnd && endDate > objectiveEnd) saveBlockers.push("Initiative End Period sits after the Parent Objective horizon.");
    if (startDate && goalStart && startDate < goalStart) saveBlockers.push("Initiative Start Period sits before the Parent Strategy Period.");
    if (endDate && goalEnd && endDate > goalEnd) saveBlockers.push("Initiative End Period sits after the Parent Strategy Period.");
    if (objectiveGranularity && granularityRank(granularity) > granularityRank(objectiveGranularity)) {
      saveBlockers.push("Contribution Plan Granularity cannot be coarser than the Parent Objective target plan.");
    }

    if (benefitStart && benefitEnd && benefitEnd < benefitStart) planningBlockers.push("Benefit Realization End must be on or after Benefit Realization Start.");
    if (benefitStart && startDate && benefitStart < startDate) planningBlockers.push("Benefit Realization Start sits before the Initiative period.");
    if (benefitEnd && endDate && benefitEnd > endDate) planningBlockers.push("Benefit Realization End sits after the Initiative period.");

    if (!cleanText(byId("initiative-contribution-metric")?.value)) planningBlockers.push("Contribution Metric / Success Measure is required for planning readiness.");
    if (!cleanText(byId("initiative-contribution-method")?.value)) planningBlockers.push("Contribution Method / Aggregation Method is required for planning readiness.");
    if (!cleanText(byId("initiative-benefit-hypothesis")?.value)) planningBlockers.push("Expected Contribution / Benefit Hypothesis is required for planning readiness.");

    const bounds = objectiveBounds();
    const allowedKeys = new Set((bounds ? buildPeriods(bounds.start, bounds.end, granularity) : []).map((row) => row.periodKey));
    const missingValuesCount = contributionPlanRows.filter((row) => row.plannedValue === null || row.plannedValue === undefined || Number.isNaN(row.plannedValue)).length;
    contributionPlanRows.forEach((row) => {
      if (allowedKeys.size && !allowedKeys.has(row.periodKey)) {
        planningBlockers.push(`Contribution row ${row.periodLabel} falls outside the allowed Initiative/Objective/Strategy horizon.`);
      }
    });
    if (bounds && contributionPlanRows.length === 0) planningBlockers.push("Generate contribution plan rows inside the allowed horizon.");
    if (contributionPlanRows.length > 0 && missingValuesCount > 0) planningBlockers.push("Contribution plan rows are missing planned values.");

    if (!cleanText(byId("initiative-owner-role")?.value)) warnings.push("Owner role is still blank.");
    if (!cleanText(byId("initiative-executive-sponsor")?.value) && !requiresSponsorValidationForLifecycle()) warnings.push("Accountable sponsor is still blank.");
    if (!cleanText(byId("initiative-sponsor-role")?.value) && !requiresSponsorValidationForLifecycle()) warnings.push("Sponsor role is still blank.");
    if (selectedParticipatingCompanies().length === 0) warnings.push("Participating companies are not selected yet.");
    if (!cleanText(byId("initiative-reporting-frequency")?.value)) warnings.push("Reporting Frequency is still blank.");
    if (!cleanText(byId("initiative-funding-source")?.value)) warnings.push("Funding Source is still blank.");
    if (!cleanText(byId("initiative-strategy-alignment-note")?.value)) warnings.push("Strategy Alignment Note is still blank.");
    if (!cleanText(byId("initiative-governance-notes")?.value)) warnings.push("Governance / Evidence Note is still blank.");
    if (currentMode() === "ObjectiveTemplate" && !cleanText(byId("initiative-parent-objective")?.value)) warnings.push("Objective + Template source mode works best after selecting the Parent Objective.");

    const draftReady = missing.length === 0 && saveBlockers.length === 0;
    const planningReady = draftReady && planningBlockers.length === 0 && contributionPlanRows.length > 0;
    const publishReady = planningReady
      && cleanText(byId("initiative-sponsor-role")?.value)
      && cleanText(byId("initiative-executive-sponsor")?.value)
      && cleanText(byId("initiative-reporting-frequency")?.value)
      && cleanText(byId("initiative-strategy-alignment-note")?.value)
      && cleanText(byId("initiative-governance-notes")?.value);
    if (!publishReady && planningReady) warnings.push("Publish readiness still needs Sponsor Role, Accountable Sponsor, Reporting Frequency, Strategy Alignment Note, and Governance / Evidence Note.");

    return {
      missing: unique(missing),
      saveBlockers: unique(saveBlockers),
      planningBlockers: unique(planningBlockers),
      warnings: unique(warnings),
      draftReady,
      planningReady,
      publishReady,
      rowsCount: contributionPlanRows.length,
      missingValuesCount,
    };
  }

  function renderReadiness() {
    const snapshot = readinessSnapshot();
    if (readinessIndicatorEl) {
      readinessIndicatorEl.className = `es-status-pill ${snapshot.planningReady ? "is-ready" : "is-blocked"}`;
      readinessIndicatorEl.textContent = snapshot.planningReady ? "Planning Readiness: Ready" : "Planning Readiness: Blocked";
    }
    if (readinessTextEl) {
      readinessTextEl.textContent = snapshot.planningReady
        ? "Initiative contribution planning is aligned to the Parent Objective timing and ready for operational use."
        : "Use draft readiness for save safety, then clear planning blockers before treating the Initiative as execution-ready.";
    }
    if (readinessMissingEl) readinessMissingEl.innerHTML = snapshot.missing.length ? snapshot.missing.map((item) => `<li>${escapeHtml(item)}</li>`).join("") : "<li>None</li>";
    if (readinessSaveBlockersEl) readinessSaveBlockersEl.innerHTML = snapshot.saveBlockers.length ? snapshot.saveBlockers.map((item) => `<li>${escapeHtml(item)}</li>`).join("") : "<li>None</li>";
    if (readinessPlanningBlockersEl) readinessPlanningBlockersEl.innerHTML = snapshot.planningBlockers.length ? snapshot.planningBlockers.map((item) => `<li>${escapeHtml(item)}</li>`).join("") : "<li>None</li>";
    if (readinessWarningsEl) readinessWarningsEl.innerHTML = snapshot.warnings.length ? snapshot.warnings.map((item) => `<li>${escapeHtml(item)}</li>`).join("") : "<li>None</li>";
    if (readinessDraftChipEl) readinessDraftChipEl.textContent = `Draft readiness: ${snapshot.draftReady ? "Ready" : "Blocked"}`;
    if (readinessPlanChipEl) readinessPlanChipEl.textContent = `Planning readiness: ${snapshot.planningReady ? "Ready" : "Blocked"}`;
    if (readinessPublishChipEl) readinessPublishChipEl.textContent = `Publish readiness: ${snapshot.publishReady ? "Ready" : "Blocked"}`;
    if (readinessRowsChipEl) readinessRowsChipEl.textContent = `Contribution rows: ${snapshot.rowsCount}`;
  }

  function setStep(step) {
    currentStep = Math.max(1, Math.min(totalSteps, Number(step || 1)));
    wizardButtons.forEach((button) => {
      const active = Number(button.dataset.step || 0) === currentStep;
      button.classList.toggle("active", active);
      button.setAttribute("aria-selected", active ? "true" : "false");
    });
    wizardPanes.forEach((pane) => pane.classList.toggle("d-none", Number(pane.dataset.step || 0) !== currentStep));
    if (backBtn) backBtn.disabled = currentStep === 1;
    if (nextBtn) nextBtn.classList.toggle("d-none", currentStep === totalSteps);
    if (saveBtn) saveBtn.classList.toggle("d-none", currentStep !== totalSteps);
  }

  function collectPayload() {
    syncStatusInputs();
    return {
      initiativeId: cleanText(byId("initiative-id")?.value),
      initiativeName: cleanText(byId("initiative-name")?.value),
      description: cleanText(byId("initiative-description")?.value),
      parentObjectiveId: cleanText(byId("initiative-parent-objective")?.value),
      parentGoalId: currentObjective?.parentGoalId || "",
      owner: selectedText("initiative-owner-person") || cleanText(byId("initiative-owner-person")?.value),
      deliveryOwnerCompanyId: cleanText(byId("initiative-owner-company")?.value),
      deliveryOwnerPositionId: cleanText(byId("initiative-owner-role")?.value),
      deliveryOwnerPersonId: cleanText(byId("initiative-owner-person")?.value),
      executiveSponsor: selectedText("initiative-executive-sponsor") || cleanText(byId("initiative-executive-sponsor")?.value),
      accountableSponsorRole: cleanText(byId("initiative-sponsor-role")?.value),
      status: cleanText(byId("initiative-status")?.value || "Draft"),
      type: cleanText(byId("initiative-type")?.value),
      waveOrPhase: cleanText(byId("initiative-wave")?.value),
      priority: cleanText(byId("initiative-priority")?.value),
      complexity: cleanText(byId("initiative-complexity")?.value),
      maturity: cleanText(byId("initiative-maturity")?.value),
      startDate: cleanText(byId("initiative-start-date")?.value) || null,
      endDate: cleanText(byId("initiative-end-date")?.value) || null,
      reportingFrequency: cleanText(byId("initiative-reporting-frequency")?.value),
      contributionMetricName: cleanText(byId("initiative-contribution-metric")?.value),
      contributionUnitOfMeasure: cleanText(byId("initiative-contribution-unit")?.value),
      contributionPlanGranularity: cleanText(byId("initiative-contribution-granularity")?.value || "InheritFromObjective"),
      contributionMethod: cleanText(byId("initiative-contribution-method")?.value),
      contributionTiming: cleanText(byId("initiative-contribution-timing")?.value),
      benefitHypothesis: cleanText(byId("initiative-benefit-hypothesis")?.value),
      benefitRealizationStart: cleanText(byId("initiative-benefit-start")?.value) || null,
      benefitRealizationEnd: cleanText(byId("initiative-benefit-end")?.value) || null,
      contributionPlanValues: contributionPlanRows.map((row) => ({
        periodKey: row.periodKey,
        periodLabel: row.periodLabel,
        periodStart: row.periodStart,
        periodEnd: row.periodEnd,
        plannedValue: row.plannedValue,
        forecastValue: row.forecastValue,
        actualValue: row.actualValue,
        commentary: cleanText(row.commentary),
      })),
      sponsoringCompanyId: cleanText(byId("initiative-sponsoring-company")?.value),
      participatingCompanyIds: selectedParticipatingCompanies(),
      entityScope: cleanText(byId("initiative-entity-scope")?.value),
      initiativeClass: cleanText(byId("initiative-class")?.value),
      budgetEnvelope: cleanText(byId("initiative-budget-envelope")?.value),
      budgetAmount: cleanText(byId("initiative-budget-amount")?.value) ? Number(byId("initiative-budget-amount").value) : null,
      currencyCode: cleanText(byId("initiative-currency")?.value),
      fundingSource: cleanText(byId("initiative-funding-source")?.value),
      strategyAlignmentNote: cleanText(byId("initiative-strategy-alignment-note")?.value),
      governanceStage: cleanText(byId("initiative-governance-stage")?.value),
      decisionReference: cleanText(byId("initiative-decision-reference")?.value),
      evidenceReference: cleanText(byId("initiative-evidence-reference")?.value),
      governanceNotes: cleanText(byId("initiative-governance-notes")?.value),
      dependencyFlag: Boolean(byId("initiative-dependency-flag")?.checked),
      notes: cleanText(byId("initiative-notes")?.value),
      strategyLinkStatus: "Linked",
      sourceTemplateType: sourceTemplateId ? "InitiativeTemplate" : "",
      sourceTemplateId: sourceTemplateId,
      sourceTemplateVersion: sourceTemplateId ? sourceTemplateVersion : null,
      createdFromLibrary: Boolean(sourceTemplateId),
    };
  }

  async function saveInitiative() {
    const payload = collectPayload();
    const snapshot = readinessSnapshot();
    const errors = {};
    if (snapshot.missing.length) errors.missing = snapshot.missing.map((item) => `${item} is required.`);
    if (snapshot.saveBlockers.length) errors.save = snapshot.saveBlockers;
    if (Object.keys(errors).length) {
      renderErrors(errors);
      return;
    }
    renderErrors({});
    try {
      const saved = isEditMode
        ? await window.initiativeStrategyApi.update(editId, payload, currentVersion)
        : await window.initiativeStrategyApi.create(payload);
      const id = cleanText(saved?.initiativeId || payload.initiativeId || editId);
      notify(isEditMode ? "Initiative updated." : "Initiative created.");
      window.location.assign(detailUrl(id));
    } catch (error) {
      renderErrors(error?.payload?.error?.details || error?.payload?.errors || {
        save: [window.enterpriseStrategyUi?.getErrorMessage?.(error, "Unable to save initiative.") || "Unable to save initiative."]
      });
    }
  }

  function renderSourceSummary(meta) {
    const host = byId("initiative-source-summary");
    if (!host) return;
    const mode = currentMode();
    const objectiveText = currentObjective ? `${cleanText(currentObjective.id)} - ${cleanText(currentObjective.name)}` : "";
    const anchorMissing = !objectiveText;
    if (!sourceTemplateId || !meta) {
      host.classList.add("is-empty");
      host.innerHTML = `
        <div class="goal-source-summary-name">${escapeHtml(modeLabel())}</div>
        <div class="goal-source-summary-note">
          ${anchorMissing
            ? "Select Parent Objective first so the system can derive Parent Goal, planning horizon, and compatible Initiative templates."
            : (mode === "Blank"
              ? "Blank starts without template defaults, but the Initiative still must be anchored to a Parent Objective."
              : `Parent Objective context: <code>${escapeHtml(objectiveText)}</code>. Choose an Initiative Template that matches this Objective anchor and governance chain.`)}
        </div>`;
      return;
    }
    const compatibility = templateCompatibility(meta);
    host.classList.remove("is-empty");
    host.innerHTML = `
      <div class="goal-source-summary-name">${escapeHtml(meta.name || sourceTemplateId)}</div>
      <div class="goal-source-summary-note">
        Mode: <strong>${escapeHtml(modeLabel())}</strong>
        | Template ID: <code>${escapeHtml(sourceTemplateId)}</code>
        ${meta.version ? ` | Version: <code>${escapeHtml(meta.version)}</code>` : ""}
        ${meta.status ? ` | Status: <strong>${escapeHtml(meta.status)}</strong>` : ""}
        ${meta.parentObjectiveTemplateId ? ` | Parent Objective Template: <code>${escapeHtml(meta.parentObjectiveTemplateId)}</code>` : ""}
        ${objectiveText ? ` | Runtime Parent Objective: <code>${escapeHtml(objectiveText)}</code>` : ""}
        ${meta.startDate || meta.endDate ? ` | Horizon hint: ${escapeHtml(formatDateRange(meta.startDate, meta.endDate))}` : ""}
        ${compatibility.compatible ? " | Compatible with current Parent Objective." : ` | <span class="text-warning">${escapeHtml(compatibility.reasons[0] || "Compatibility requires review.")}</span>`}
      </div>`;
  }

  async function applyTemplate(templateId) {
    const selectedRow = normalizeTemplateRow(
      templateRows.find((row) => cleanText(row?.id) === cleanText(templateId)) || {}
    );
    await hydrateCurrentObjective(cleanText(byId("initiative-parent-objective")?.value) || currentObjective?.id || "");
    const detail = await window.strategyLibraryApi.template(templateId);
    const prefill = detail?.initiativePrefill || detail?.InitiativePrefill || {};
    const attributes = detail?.attributes || detail?.Attributes || {};
    const candidateTemplateId = cleanText(prefill.templateId || prefill.TemplateId || detail?.id || detail?.Id || selectedRow.id || templateId);
    const candidateTemplateVersion = Number(prefill.version ?? prefill.Version ?? detail?.version ?? detail?.Version ?? selectedRow.version ?? 0) || null;
    const candidateMeta = {
      id: candidateTemplateId,
      name: cleanText(prefill.name || prefill.Name || detail?.name || detail?.Name || selectedRow.name || candidateTemplateId),
      description: cleanText(prefill.description || prefill.Description || attributes?.Description || selectedRow.description || ""),
      type: cleanText(prefill.type || prefill.Type || attributes?.Type || selectedRow.type || ""),
      owner: cleanText(prefill.owner || prefill.Owner || detail?.owner || detail?.Owner || attributes?.Owner || selectedRow.owner || ""),
      priority: cleanText(prefill.priority || prefill.Priority || attributes?.Priority || selectedRow.priority || ""),
      status: cleanText(prefill.lifecycleStatus || prefill.LifecycleStatus || detail?.status || detail?.Status || attributes?.Status || attributes?.LifecycleStatus || selectedRow.status || ""),
      version: cleanText(prefill.version || prefill.Version || detail?.version || detail?.Version || selectedRow.version || ""),
      parentObjectiveTemplateId: cleanText(prefill.parentObjectiveTemplateId || prefill.ParentObjectiveTemplateId || attributes?.ParentObjectiveTemplateId || selectedRow.parentObjectiveTemplateId || ""),
      parentObjectiveName: cleanText(detail?.parentObjectiveName || detail?.ParentObjectiveName || selectedRow.parentObjectiveName || ""),
      sponsorRole: cleanText(prefill.accountableSponsorRole || prefill.AccountableSponsorRole || attributes?.AccountableSponsorRole || ""),
      sponsorPerson: cleanText(attributes?.ExecutiveSponsorId || attributes?.ExecutiveSponsor || attributes?.AccountableSponsor || ""),
      startDate: prefill.startDate || prefill.StartDate || selectedRow.startDate || "",
      endDate: prefill.endDate || prefill.EndDate || selectedRow.endDate || "",
    };

    const compatibility = templateCompatibility(candidateMeta);
    if (!compatibility.compatible) {
      renderErrors({ template: compatibility.reasons });
      renderReadiness();
      return false;
    }

    sourceTemplateId = candidateTemplateId;
    sourceTemplateVersion = candidateTemplateVersion;
    selectedTemplateMeta = candidateMeta;

    const setIfBlank = (id, value) => {
      const el = byId(id);
      if (el && !cleanText(el.value) && cleanText(value)) el.value = cleanText(value);
    };
    const setValue = (id, value) => {
      const el = byId(id);
      if (!el) return;
      el.value = cleanText(value);
      el.dispatchEvent(new Event("input", { bubbles: true }));
      el.dispatchEvent(new Event("change", { bubbles: true }));
    };
    const setSelectValue = (id, value, label) => {
      const normalizedValue = cleanText(value);
      if (!normalizedValue) return;
      ensureSelectValue(id, normalizedValue, cleanText(label) || normalizedValue);
      const el = byId(id);
      if (!el) return;
      el.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const templateName = cleanText(prefill.name || prefill.Name || detail?.name || detail?.Name || selectedTemplateMeta?.name || "");
    const templateDescription = cleanText(prefill.description || prefill.Description || attributes?.Description || selectedTemplateMeta?.description || "");
    const templateType = cleanText(prefill.type || prefill.Type || attributes?.Type || selectedTemplateMeta?.type || "");
    const initiativeDraftState = isEditMode
      ? (cleanText(byId("initiative-status-editable")?.value || byId("initiative-status")?.value || "Draft") || "Draft")
      : "Draft";

    refreshInitiativeTypeOptions();

    if (templateName) setValue("initiative-name", templateName);
    if (templateDescription) setValue("initiative-description", templateDescription);
    if (templateType) setSelectValue("initiative-type", templateType, templateType);
    setSelectValue("initiative-status-editable", initiativeDraftState, initiativeDraftState);
    setValue("initiative-status", initiativeDraftState);
    if (byId("initiative-status-readonly")) byId("initiative-status-readonly").value = initiativeDraftState;

    setIfBlank("initiative-priority", prefill.priority);
    setIfBlank("initiative-complexity", prefill.complexity);
    setIfBlank("initiative-class", prefill.initiativeClass);
    setIfBlank("initiative-budget-envelope", prefill.budgetEnvelope);
    setIfBlank("initiative-entity-scope", prefill.entityScope);
    setIfBlank("initiative-owner-role", prefill.ownerRole);
    setIfBlank("initiative-sponsor-role", prefill.accountableSponsorRole);
    setIfBlank("initiative-maturity", prefill.maturityReadiness);
    setIfBlank("initiative-contribution-method", prefill.contributionMethod);
    setIfBlank("initiative-funding-source", prefill.fundingSource);
    setIfBlank("initiative-strategy-alignment-note", prefill.strategyAlignmentNote);
    if (!cleanText(byId("initiative-start-date")?.value) && prefill.startDate) byId("initiative-start-date").value = toIsoDate(prefill.startDate);
    if (!cleanText(byId("initiative-end-date")?.value) && prefill.endDate) byId("initiative-end-date").value = toIsoDate(prefill.endDate);
    refreshInitiativeTypeOptions();
    if (templateType) setSelectValue("initiative-type", templateType, templateType);
    syncStatusInputs();
    refreshParentObjectiveOptions();
    renderSourceSummary(selectedTemplateMeta);
    syncTemplateGateState();
    applySponsorPrefill();
    refreshOwnerPersonOptions();
    renderReadiness();
    renderErrors({});
    return true;
  }

  function normalizeTemplateRow(row) {
    return {
      id: cleanText(row.id || row.Id || row.templateCode || row.TemplateCode || ""),
      name: cleanText(row.name || row.Name || ""),
      description: cleanText(row.description || row.Description || row.statement || row.Statement || row.attributes?.Description || ""),
      owner: cleanText(row.owner || row.Owner || ""),
      type: cleanText(row.type || row.Type || row.categoryOrType || row.CategoryOrType || row.attributes?.Type || ""),
      entityScope: cleanText(row.entityScope || row.EntityScope || row.attributes?.EntityScope || ""),
      priority: cleanText(row.priority || row.Priority || ""),
      status: cleanText(row.status || row.Status || row.lifecycleStatus || row.LifecycleStatus || ""),
      templateType: cleanText(row.templateType || row.TemplateType || row.itemType || row.ItemType || ""),
      parentObjectiveTemplateId: cleanText(row.parentObjectiveTemplateId || row.ParentObjectiveTemplateId || row.parentObjectiveId || row.ParentObjectiveId || ""),
      parentObjectiveName: cleanText(row.parentObjectiveName || row.ParentObjectiveName || ""),
      version: cleanText(row.version || row.Version || ""),
    };
  }

  function syncTemplateGateState() {
    const browseBtn = byId("initiative-browse-source");
    const clearBtn = byId("initiative-clear-source");
    const helper = byId("initiative-template-gate-help");
    const hasObjective = hasParentObjectiveAnchor();
    const mode = currentMode();
    const objectiveText = currentObjective ? `${cleanText(currentObjective.id)} - ${cleanText(currentObjective.name)}` : "";
    const objectiveTemplateId = currentObjectiveTemplateId();

    if (browseBtn) browseBtn.disabled = !hasObjective || !(window.strategyLibraryApi?.catalog && window.strategyLibraryApi?.template);
    if (clearBtn) clearBtn.disabled = !sourceTemplateId;

    if (!helper) return;
    if (!hasObjective) {
      helper.textContent = "Select Parent Objective first so the system can derive Parent Goal, planning horizon, and compatible Initiative templates.";
      updateSourcePickerContext();
      return;
    }
    if (mode === "Blank") {
      helper.textContent = "Blank starts without template defaults, but the Initiative still must be anchored to a Parent Objective.";
      updateSourcePickerContext();
      return;
    }
    if (objectiveTemplateId) {
      helper.textContent = `Showing Initiative Templates compatible with Parent Objective ${cleanText(currentObjective?.name || objectiveTemplateId)} when available.`;
      updateSourcePickerContext();
      return;
    }
    helper.textContent = `Parent Objective ${objectiveText || "selected"} is set. Compatible active Initiative Templates are now available based on Objective type.`;
    updateSourcePickerContext();
  }

  function updateSourcePickerContext({ notifyOnMismatch = false } = {}) {
    const objectiveText = currentObjective ? `${cleanText(currentObjective.id)} - ${cleanText(currentObjective.name)}` : "Select Parent Objective first";
    const objectiveType = cleanText(currentObjective?.type || "") || "-";
    const objectiveScope = resolvedObjectiveEntityScope() || "-";
    const initiativeType = cleanText(byId("initiative-type")?.value) || "-";
    const initiativeScope = cleanText(byId("initiative-entity-scope")?.value) || objectiveScope || "-";
    const warningEl = byId("initiative-source-picker-context-warning");
    const messages = sourcePickerMismatchMessages();
    const warningKey = messages.join("|");

    if (byId("initiative-source-picker-current-objective")) byId("initiative-source-picker-current-objective").textContent = objectiveText;
    if (byId("initiative-source-picker-current-objective-type")) byId("initiative-source-picker-current-objective-type").textContent = objectiveType;
    if (byId("initiative-source-picker-current-objective-scope")) byId("initiative-source-picker-current-objective-scope").textContent = objectiveScope;
    if (byId("initiative-source-picker-current-initiative-type")) byId("initiative-source-picker-current-initiative-type").textContent = initiativeType;
    if (byId("initiative-source-picker-current-initiative-scope")) byId("initiative-source-picker-current-initiative-scope").textContent = initiativeScope;

    if (!warningEl) return;
    if (!messages.length) {
      warningEl.classList.add("d-none");
      warningEl.innerHTML = "";
      lastSourcePickerWarningKey = "";
      return;
    }

    warningEl.classList.remove("d-none");
    warningEl.innerHTML = messages.map((message) => `<div>${escapeHtml(message)}</div>`).join("");
    if (notifyOnMismatch && warningKey && warningKey !== lastSourcePickerWarningKey) {
      notify(messages[0], "warning");
      lastSourcePickerWarningKey = warningKey;
    }
  }

  function renderTemplatePickerBlockedState(message) {
    const helper = byId("initiative-source-picker-helper");
    const tbody = byId("initiative-source-picker-tbody");
    updateSourcePickerContext();
    if (helper) helper.textContent = message;
    if (tbody) tbody.innerHTML = `<tr><td colspan="10" class="text-center text-muted py-3">${escapeHtml(message)}</td></tr>`;
  }

  function renderTemplateRows() {
    const tbody = byId("initiative-source-picker-tbody");
    if (!tbody) return;
    updateSourcePickerContext();
    if (!currentObjective) {
      renderTemplatePickerBlockedState("Select Parent Objective first to load compatible Initiative templates.");
      return;
    }
    const search = cleanText(byId("initiative-source-picker-search")?.value).toLowerCase();
    const type = cleanText(byId("initiative-source-picker-type")?.value);
    const entityScope = cleanText(byId("initiative-source-picker-entity-scope")?.value);
    const parentObjectiveName = cleanText(byId("initiative-source-picker-parent-objective-name")?.value);
    const filtered = templateRows.filter((row) => {
      const resolvedParentObjectiveName = resolveParentObjectiveName(row);
      if (search && !`${row.id} ${row.name} ${row.description} ${row.owner} ${row.entityScope} ${resolvedParentObjectiveName}`.toLowerCase().includes(search)) return false;
      if (type && normalizedMatchKey(row.type) !== normalizedMatchKey(type)) return false;
      if (entityScope) {
        if (!row.entityScope) return false;
        if (!entityScopeMatchesObjective(entityScope, row.entityScope)) return false;
      }
      if (parentObjectiveName && resolvedParentObjectiveName !== parentObjectiveName) return false;
      if (!templateCompatibility(row).compatible) return false;
      return true;
    });
    const helper = byId("initiative-source-picker-helper");
    if (helper) {
      const objectiveTemplateId = currentObjectiveTemplateId();
      helper.textContent = objectiveTemplateId
        ? `Showing active Initiative Templates compatible with Parent Objective ${cleanText(currentObjective?.name || objectiveTemplateId)} when available.`
        : "Showing active Initiative Templates compatible with the selected Parent Objective type.";
    }
    tbody.innerHTML = filtered.length ? filtered.map((row) => `
      <tr class="initiative-template-picker-row" data-template-id="${escapeHtml(row.id)}" role="button" tabindex="0">
        <td>${escapeHtml(row.id)}</td>
        <td>${escapeHtml(resolveParentObjectiveName(row) || "-")}</td>
        <td>${escapeHtml(row.name)}</td>
        <td>${escapeHtml(row.description || "-")}</td>
        <td>${escapeHtml(row.type || "-")}</td>
        <td>${escapeHtml(row.owner || "-")}</td>
        <td>${escapeHtml(row.entityScope || "-")}</td>
        <td>${escapeHtml(row.priority || "-")}</td>
        <td>${escapeHtml(row.status || "-")}</td>
        <td><button type="button" class="btn btn-sm btn-outline-primary" data-template-id="${escapeHtml(row.id)}">Use</button></td>
      </tr>`).join("") : '<tr><td colspan="10" class="text-center text-muted py-3">No matching Initiative Templates found.</td></tr>';
    const applyTemplateSelection = async (templateId) => {
      const applied = await applyTemplate(templateId);
      if (applied) templateModalInstance()?.hide();
    };
    tbody.querySelectorAll(".initiative-template-picker-row[data-template-id]").forEach((rowEl) => {
      rowEl.addEventListener("click", async (event) => {
        if (event.target?.closest?.("button")) return;
        await applyTemplateSelection(rowEl.dataset.templateId);
      });
      rowEl.addEventListener("keydown", async (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        event.preventDefault();
        await applyTemplateSelection(rowEl.dataset.templateId);
      });
    });
    tbody.querySelectorAll("[data-template-id]").forEach((button) => {
      button.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        await applyTemplateSelection(button.dataset.templateId);
      });
    });
  }

  async function fetchAllInitiativeTemplateCatalog() {
    const pageSize = 200;
    const rows = [];
    const seen = new Set();
    let page = 1;
    let totalCount = null;

    while (true) {
      const data = await window.strategyLibraryApi.catalog({ page, pageSize, templateType: "Initiative" }, { skipCache: true });
      const items = Array.isArray(data?.items) ? data.items : (Array.isArray(data?.Items) ? data.Items : (Array.isArray(data) ? data : []));
      const normalizedItems = items
        .map(normalizeTemplateRow)
        .filter((row) => row.id && cleanText(row.templateType || "Initiative").toLowerCase().includes("initiative"));

      normalizedItems.forEach((row) => {
        if (seen.has(row.id)) return;
        seen.add(row.id);
        rows.push(row);
      });

      totalCount = Number(data?.totalCount ?? data?.TotalCount ?? totalCount ?? 0) || totalCount;
      if (!items.length) break;
      if (items.length < pageSize) break;
      if (totalCount && rows.length >= totalCount) break;
      page += 1;
    }

    return rows;
  }

  async function fetchAllObjectiveTemplateCatalog() {
    const pageSize = 500;
    const rows = [];
    const seen = new Set();
    let page = 1;
    let totalCount = null;

    while (true) {
      const data = await window.strategyLibraryApi.catalog({ page, pageSize, templateType: "Objective" }, { skipCache: true });
      const items = Array.isArray(data?.items) ? data.items : (Array.isArray(data?.Items) ? data.Items : (Array.isArray(data) ? data : []));
      const normalizedItems = items
        .map((row) => ({
          id: cleanText(row?.id || row?.Id || row?.templateCode || row?.TemplateCode || ""),
          name: cleanText(row?.name || row?.Name || row?.parentObjectiveName || row?.ParentObjectiveName || "")
        }))
        .filter((row) => row.id);

      normalizedItems.forEach((row) => {
        if (seen.has(row.id)) return;
        seen.add(row.id);
        rows.push(row);
      });

      totalCount = Number(data?.totalCount ?? data?.TotalCount ?? totalCount ?? 0) || totalCount;
      if (!items.length) break;
      if (items.length < pageSize) break;
      if (totalCount && rows.length >= totalCount) break;
      page += 1;
    }

    return rows;
  }

  async function loadTemplates() {
    const currentObjectiveId = cleanText(byId("initiative-parent-objective")?.value) || cleanText(currentObjective?.id || "");
    if (currentObjectiveId) {
      await hydrateCurrentObjective(currentObjectiveId);
      applyObjectiveContext();
    }
    if (!currentObjective) {
      renderTemplatePickerBlockedState("Select Parent Objective first to load compatible Initiative templates.");
      return;
    }
    const tbody = byId("initiative-source-picker-tbody");
    if (tbody) tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted py-3">Loading Initiative Templates...</td></tr>';
    const [rows, objectiveTemplateRows] = await Promise.all([
      fetchAllInitiativeTemplateCatalog(),
      fetchAllObjectiveTemplateCatalog()
    ]);
    objectiveTemplateNamesById = new Map(objectiveTemplateRows.map((row) => [row.id, row.name]));
    templateRows = rows.filter((row) => isTemplateStatusSelectable(row.status));
    fillSelect(
      "initiative-source-picker-type",
      [...new Set(templateRows.map((row) => row.type).filter(Boolean))],
      "All types",
      { keepCurrent: false }
    );
    fillSelect(
      "initiative-source-picker-entity-scope",
      [...new Set(templateRows.map((row) => row.entityScope).filter(Boolean))],
      "All entity scopes",
      { keepCurrent: false }
    );
    fillSelect(
      "initiative-source-picker-parent-objective-name",
      [...new Set(templateRows.map((row) => resolveParentObjectiveName(row)).filter(Boolean))],
      "All parent objectives",
      { keepCurrent: false }
    );

    const currentInitiativeType = cleanText(byId("initiative-type")?.value);
    const currentInitiativeScope = cleanText(byId("initiative-entity-scope")?.value);
    const objectiveType = cleanText(currentObjective?.type || "");
    const objectiveScope = resolvedObjectiveEntityScope();
    const popupTypeFilter = byId("initiative-source-picker-type");
    const popupScopeFilter = byId("initiative-source-picker-entity-scope");
    const popupParentObjectiveFilter = byId("initiative-source-picker-parent-objective-name");

    if (popupTypeFilter) {
      const availableTypeValues = new Set(Array.from(popupTypeFilter.options || []).map((opt) => cleanText(opt.value)).filter(Boolean));
      popupTypeFilter.value = availableTypeValues.has(currentInitiativeType)
        ? currentInitiativeType
        : (availableTypeValues.has(objectiveType) ? objectiveType : "");
    }
    if (popupScopeFilter) {
      const availableScopeValues = new Set(Array.from(popupScopeFilter.options || []).map((opt) => cleanText(opt.value)).filter(Boolean));
      const preferredScope = availableScopeValues.has(currentInitiativeScope)
        ? currentInitiativeScope
        : (availableScopeValues.has(objectiveScope) ? objectiveScope : "");
      popupScopeFilter.value = preferredScope;
    }
    if (popupParentObjectiveFilter) {
      popupParentObjectiveFilter.value = "";
    }
    renderTemplateRows();
  }

  async function openTemplateBrowser() {
    if (!hasParentObjectiveAnchor()) {
      renderErrors({ template: ["Select Parent Objective first to load compatible Initiative templates."] });
      syncTemplateGateState();
      return;
    }
    const modal = templateModalInstance();
    if (!modal) {
      renderErrors({ template: ["Template browser is unavailable right now. Reload the page and try again."] });
      return;
    }
    if (currentMode() === "Blank" && byId("initiative-creation-mode-select")) {
      byId("initiative-creation-mode-select").value = "Template";
    }
    renderErrors({});
    updateSourcePickerContext();
    const tbody = byId("initiative-source-picker-tbody");
    const helper = byId("initiative-source-picker-helper");
    if (helper) helper.textContent = "Loading Initiative Templates...";
    if (tbody) tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted py-3">Loading Initiative Templates...</td></tr>';
    modal.show();
    try {
      await loadTemplates();
      updateSourcePickerContext({ notifyOnMismatch: true });
      renderSourceSummary(selectedTemplateMeta);
    } catch (error) {
      if (helper) helper.textContent = "Unable to load Initiative Templates.";
      if (tbody) {
        tbody.innerHTML = '<tr><td colspan="10" class="text-center text-danger py-3">Unable to load Initiative Templates.</td></tr>';
      }
      renderErrors({
        template: [window.enterpriseStrategyUi?.getErrorMessage?.(error, "Unable to open Initiative Template browser.") || "Unable to open Initiative Template browser."]
      });
    }
  }

  async function ensurePositionOptionsLoaded() {
    const state = workbook.positionLoadState?.() || { status: "idle" };
    const hasRows = (workbook.positionOptions?.() || []).length > 0;
    if (hasRows || typeof workbook.ensurePositionsLoaded !== "function") return;
    if (state.status === "loading") return;
    try {
      await workbook.ensurePositionsLoaded();
    } catch (_) {
    }
  }

  function refreshOwnerRoleOptions() {
    const companyId = cleanText(byId("initiative-owner-company")?.value);
    const current = cleanText(byId("initiative-owner-role")?.value);
    const options = companyId ? scopedPositionOptions(companyId) : [];
    fillSelect("initiative-owner-role", options, companyId ? "Select owner role" : "Select owner company / org first");
    if (!companyId) {
      byId("initiative-owner-role").value = "";
      byId("initiative-owner-role").disabled = true;
      byId("initiative-owner-role-help").textContent = "Select Owner Company / Org to load valid owner roles.";
      return;
    }
    byId("initiative-owner-role").disabled = options.length === 0;
    ensureSelectValue("initiative-owner-role", current, workbook.positionDisplayName?.(current) || current);
    byId("initiative-owner-role-help").textContent = options.length
      ? "Owner role defaults from template when available and is filtered by the selected owner company / org."
      : "Position service unavailable or no roles available for the selected company.";
  }

  function refreshSponsorRoleOptions() {
    const companyId = cleanText(byId("initiative-sponsoring-company")?.value);
    const current = cleanText(byId("initiative-sponsor-role")?.value);
    const options = companyId ? scopedPositionOptions(companyId) : [];
    fillSelect("initiative-sponsor-role", options, companyId ? "Select sponsor role" : "Select sponsoring company first");
    if (!companyId) {
      byId("initiative-sponsor-role").value = "";
      byId("initiative-sponsor-role").disabled = true;
      byId("initiative-sponsor-role-help").textContent = "Select Sponsoring Company to load valid sponsor roles.";
      return;
    }
    byId("initiative-sponsor-role").disabled = options.length === 0;
    ensureSelectValue("initiative-sponsor-role", current, workbook.positionDisplayName?.(current) || current);
    byId("initiative-sponsor-role-help").textContent = options.length
      ? "Sponsor role is filtered by the selected sponsoring company."
      : "Position service unavailable or no sponsor roles available for the selected company.";
  }

  function refreshOwnerPersonOptions() {
    const companyId = cleanText(byId("initiative-owner-company")?.value);
    const roleId = cleanText(byId("initiative-owner-role")?.value);
    const current = cleanText(byId("initiative-owner-person")?.value);
    const peopleResult = scopedPeopleResult(companyId, roleId);
    const options = peopleResult.options;
    const placeholder = !companyId
      ? "Select owner company / org first"
      : (options.length ? "Select owner" : "No owners available");
    fillSelect("initiative-owner-person", options, placeholder);
    byId("initiative-owner-person").disabled = !companyId || options.length === 0;
    ensureSelectValue("initiative-owner-person", current, workbook.userDisplayName?.(current) || current);
    byId("initiative-owner-person-help").textContent = companyId
      ? (options.length
        ? (peopleResult.usedCompanyFallback
          ? "Initiative Owner is sourced from the employee API for the selected owner company / org. No exact role match was found, so the company employee list is shown."
          : (roleId
            ? "Initiative Owner is sourced from the employee API for the selected owner company / org and owner role."
            : "Initiative Owner is sourced from the employee API for the selected owner company / org."))
        : (roleId
          ? "No people were returned for the selected owner company / org and owner role."
          : "No employees were returned for the selected owner company / org."))
      : "Choose Owner Company / Org and Owner Role to load valid initiative owners.";
  }

  function refreshSponsorPersonOptions() {
    const companyId = cleanText(byId("initiative-sponsoring-company")?.value);
    const roleId = cleanText(byId("initiative-sponsor-role")?.value);
    const current = cleanText(byId("initiative-executive-sponsor")?.value);
    const peopleResult = scopedPeopleResult(companyId, roleId);
    const options = peopleResult.options;
    const placeholder = !companyId
      ? "Select sponsoring company first"
      : (options.length ? "Select sponsor" : "No sponsors available");
    fillSelect("initiative-executive-sponsor", options, placeholder);
    byId("initiative-executive-sponsor").disabled = !companyId || options.length === 0;
    ensureSelectValue("initiative-executive-sponsor", current, workbook.userDisplayName?.(current) || current);
    byId("initiative-executive-sponsor-help").textContent = companyId
      ? (options.length
        ? (peopleResult.usedCompanyFallback
          ? "Accountable Sponsor is sourced from the employee API for the selected sponsoring company. No exact role match was found, so the company employee list is shown."
          : (roleId
            ? "Accountable Sponsor is sourced from the employee API for the selected sponsoring company and sponsor role."
            : "Accountable Sponsor is sourced from the employee API for the selected sponsoring company."))
        : (roleId
          ? "No people were returned for the selected sponsoring company and sponsor role."
          : "No employees were returned for the selected sponsoring company."))
      : "Choose Sponsoring Company and Sponsor Role to load valid accountable sponsors.";
  }

  function hydrateStaticOptions() {
    baseInitiativeTypeOptions = resolveOptions(workbook.initiativeTypes, resolveOptions(workbook.goalObjectiveTypes));
    refreshParentObjectiveOptions();
    refreshInitiativeTypeOptions();
    fillSelect("initiative-status-editable", workbook.lifecycleStatus || ["Draft", "Planned", "Approved", "In Progress", "Completed", "Cancelled"], "Select status", { defaultValue: "Draft" });
    fillSelect("initiative-wave", workbook.waveValues || ["Wave 1", "Wave 2", "Wave 3"], "Select wave / phase");
    fillSelect("initiative-maturity", workbook.maturityValues || ["Emerging", "Defined", "Ready", "In Flight", "Scaled", "Stabilized"], "Select maturity / readiness");
    fillSelect("initiative-priority", workbook.priorities || ["Critical", "High", "Medium", "Low"], "Select priority");
    fillSelect("initiative-complexity", workbook.complexityRiskScale || ["Very High", "High", "Medium", "Low"], "Select complexity");
    fillSelect("initiative-reporting-frequency", workbook.reportingFrequencies || ["Monthly", "Quarterly", "Yearly"], "Select frequency");
    fillSelect("initiative-owner-company", companyOptions(), "Select owner company / org");
    fillSelect("initiative-sponsoring-company", companyOptions(), "Select sponsoring company");
    fillSelect("initiative-owner-person", [], "Select owner company / org first");
    fillSelect("initiative-executive-sponsor", [], "Select sponsoring company first");
    fillSelect("initiative-currency", workbook.currencyCodes || ["USD", "EUR", "GBP", "TRY"], "Select currency");
    fillSelect("initiative-contribution-granularity", [
      { value: "InheritFromObjective", label: "Inherit from Objective" },
      { value: "Monthly", label: "Monthly" },
      { value: "Quarterly", label: "Quarterly" },
      { value: "Yearly", label: "Yearly" },
      { value: "TotalInitiativeHorizon", label: "Total Initiative Horizon" }
    ], "Select granularity", { defaultValue: "InheritFromObjective" });
    fillSelect("initiative-contribution-method", resolveOptions(workbook.objectiveTargetAggregation, resolveOptions(workbook.connectionAggregation, ["Sum", "Average", "Latest", "WeightedAverage"])), "Select method");
    fillMultiSelect("initiative-participating-companies-select", companyOptions(), selectedParticipatingCompanies());
    syncStatusInputs();
  }

  function getCompanyPickerElements() {
    return {
      root: byId("initiative-participating-companies-picker"),
      select: byId("initiative-participating-companies-select"),
      toggle: byId("initiative-participating-companies-toggle"),
      display: byId("initiative-participating-companies-display"),
      panel: byId("initiative-participating-companies-panel"),
      search: byId("initiative-participating-companies-search"),
      options: byId("initiative-participating-companies-options"),
      selectAll: byId("initiative-participating-companies-select-all"),
      clearAll: byId("initiative-participating-companies-clear-all")
    };
  }

  function companyPickerPlaceholder() {
    return String(getCompanyPickerElements().root?.dataset?.placeholder || "Search and select participating companies...").trim();
  }

  function visibleCompanyButtons() {
    const { options } = getCompanyPickerElements();
    return Array.from(options?.querySelectorAll(".es-company-multi-select-option") || []);
  }

  function isCompanyPickerOpen() {
    const { panel } = getCompanyPickerElements();
    return Boolean(panel && !panel.classList.contains("d-none"));
  }

  function setCompanyPickerOpen(open) {
    const { toggle, panel, search, options } = getCompanyPickerElements();
    if (!toggle || !panel) return;
    const allowOpen = open && !toggle.disabled;
    panel.classList.toggle("d-none", !allowOpen);
    toggle.classList.toggle("is-open", allowOpen);
    toggle.setAttribute("aria-expanded", allowOpen ? "true" : "false");
    if (!allowOpen) {
      companyPickerActiveIndex = -1;
      return;
    }
    renderCompanyPickerOptions();
    if (search) {
      search.focus();
      search.select?.();
      return;
    }
    options?.focus();
  }

  function setCompanyPickerActiveIndex(nextIndex) {
    const buttons = visibleCompanyButtons();
    if (!buttons.length) {
      companyPickerActiveIndex = -1;
      return;
    }
    const bounded = Math.max(0, Math.min(nextIndex, buttons.length - 1));
    companyPickerActiveIndex = bounded;
    buttons.forEach((btn, idx) => btn.classList.toggle("is-active", idx === bounded));
  }

  function syncCompanyPickerDisplay() {
    const { select, display, toggle } = getCompanyPickerElements();
    if (!select || !display) return;
    const names = Array.from(select.selectedOptions || []).map((opt) => cleanText(opt.textContent)).filter(Boolean);
    if (!names.length) {
      display.textContent = companyPickerPlaceholder();
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

  function applyCompanySelections(values, dispatchChange) {
    const { select } = getCompanyPickerElements();
    if (!select) return;
    const selected = new Set((values || []).map((value) => cleanText(value)).filter(Boolean));
    let changed = false;
    Array.from(select.options || []).forEach((opt) => {
      const shouldSelect = selected.has(cleanText(opt.value));
      if (opt.selected !== shouldSelect) {
        opt.selected = shouldSelect;
        changed = true;
      }
    });
    if (dispatchChange && changed) {
      select.dispatchEvent(new Event("change", { bubbles: true }));
      return;
    }
    syncCompanyPickerDisplay();
    if (isCompanyPickerOpen()) renderCompanyPickerOptions();
  }

  function toggleCompanyValue(value) {
    const { select } = getCompanyPickerElements();
    if (!select) return;
    const normalizedValue = cleanText(value);
    const option = Array.from(select.options || []).find((opt) => cleanText(opt.value) === normalizedValue);
    if (!option) return;
    option.selected = !option.selected;
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function renderCompanyPickerOptions() {
    const { select, search, options } = getCompanyPickerElements();
    if (!select || !options) return;
    const query = cleanText(search?.value).toLowerCase();
    const rows = Array.from(select.options || []).map((opt) => ({
      value: cleanText(opt.value),
      label: cleanText(opt.textContent),
      selected: Boolean(opt.selected)
    })).filter((row) => row.value && row.label && (!query || row.label.toLowerCase().includes(query)));
    options.innerHTML = "";
    if (!rows.length) {
      const empty = document.createElement("div");
      empty.className = "es-company-multi-select-empty";
      empty.textContent = "No matching companies.";
      options.appendChild(empty);
      companyPickerActiveIndex = -1;
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
    setCompanyPickerActiveIndex(0);
  }

  function onCompanyPickerKeyDown(event) {
    const { search, toggle, select } = getCompanyPickerElements();
    const open = isCompanyPickerOpen();
    if (!open && (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      setCompanyPickerOpen(true);
      return;
    }
    if (!open) return;
    const buttons = visibleCompanyButtons();
    if (!buttons.length) {
      if (event.key === "Escape") {
        event.preventDefault();
        setCompanyPickerOpen(false);
        toggle?.focus();
      }
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setCompanyPickerActiveIndex(companyPickerActiveIndex + 1);
      return;
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      setCompanyPickerActiveIndex(companyPickerActiveIndex - 1);
      return;
    }
    if (event.key === "Home") {
      event.preventDefault();
      setCompanyPickerActiveIndex(0);
      return;
    }
    if (event.key === "End") {
      event.preventDefault();
      setCompanyPickerActiveIndex(buttons.length - 1);
      return;
    }
    if (event.key === "Enter" || event.key === " ") {
      if (event.target === search && event.key === " ") return;
      event.preventDefault();
      const activeBtn = buttons[companyPickerActiveIndex] || buttons[0];
      const value = cleanText(activeBtn?.dataset?.companyValue);
      if (value) toggleCompanyValue(value);
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      setCompanyPickerOpen(false);
      toggle?.focus();
      return;
    }
    if (select) syncCompanyPickerDisplay();
  }

  function initCompanyPicker() {
    const { select, root: host, toggle, panel, search, options, selectAll, clearAll } = getCompanyPickerElements();
    if (!select || !host || !toggle || !panel || !options) return;
    if (host.dataset.initialized === "1") {
      syncCompanyPickerDisplay();
      return;
    }
    host.dataset.initialized = "1";
    toggle.addEventListener("click", () => setCompanyPickerOpen(!isCompanyPickerOpen()));
    toggle.addEventListener("keydown", onCompanyPickerKeyDown);
    panel.addEventListener("keydown", onCompanyPickerKeyDown);
    search?.addEventListener("input", () => renderCompanyPickerOptions());
    options.addEventListener("click", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      const value = cleanText(btn?.dataset?.companyValue);
      if (value) toggleCompanyValue(value);
    });
    options.addEventListener("mousemove", (event) => {
      const btn = event.target.closest(".es-company-multi-select-option");
      if (!btn) return;
      const buttons = visibleCompanyButtons();
      const idx = buttons.indexOf(btn);
      if (idx >= 0) setCompanyPickerActiveIndex(idx);
    });
    selectAll?.addEventListener("click", () => {
      const values = Array.from(select.options || []).map((opt) => cleanText(opt.value)).filter(Boolean);
      applyCompanySelections(values, true);
      search?.focus();
    });
    clearAll?.addEventListener("click", () => {
      applyCompanySelections([], true);
      search?.focus();
    });
    select.addEventListener("change", () => {
      syncCompanyPickerDisplay();
      if (isCompanyPickerOpen()) renderCompanyPickerOptions();
      renderReadiness();
    });
    document.addEventListener("mousedown", (event) => {
      if (!isCompanyPickerOpen()) return;
      if (host.contains(event.target)) return;
      setCompanyPickerOpen(false);
    });
    syncCompanyPickerDisplay();
  }

  function populateForm(item) {
    currentVersion = Number(item?.version ?? 0) || 0;
    byId("initiative-id").value = cleanText(item?.initiativeId || "");
    byId("initiative-name").value = cleanText(item?.initiativeName || "");
    byId("initiative-description").value = cleanText(item?.description || "");
    byId("initiative-type").value = cleanText(item?.type || "");
    refreshParentObjectiveOptions();
    byId("initiative-parent-objective").value = cleanText(item?.parentObjectiveId || "");
    byId("initiative-status-editable").value = cleanText(item?.status || "Draft");
    byId("initiative-status").value = cleanText(item?.status || "Draft");
    syncStatusInputs();
    byId("initiative-owner-company").value = cleanText(item?.deliveryOwnerCompanyId || "");
    refreshOwnerRoleOptions();
    ensureSelectValue("initiative-owner-role", item?.deliveryOwnerPositionId || "", workbook.positionDisplayName?.(item?.deliveryOwnerPositionId) || item?.deliveryOwnerPositionId || "");
    refreshOwnerPersonOptions();
    ensureSelectValue("initiative-owner-person", item?.deliveryOwnerPersonId || item?.owner || "", workbook.userDisplayName?.(item?.deliveryOwnerPersonId || item?.owner || "") || item?.owner || item?.deliveryOwnerPersonId || "");
    byId("initiative-sponsoring-company").value = cleanText(item?.sponsoringCompanyId || "");
    refreshSponsorRoleOptions();
    ensureSelectValue("initiative-sponsor-role", item?.accountableSponsorRole || "", workbook.positionDisplayName?.(item?.accountableSponsorRole) || item?.accountableSponsorRole || "");
    refreshSponsorPersonOptions();
    ensureSelectValue("initiative-executive-sponsor", item?.executiveSponsor || "", workbook.userDisplayName?.(item?.executiveSponsor || "") || item?.executiveSponsor || "");
    fillMultiSelect("initiative-participating-companies-select", companyOptions(), item?.participatingCompanyIds || []);
    applyCompanySelections(item?.participatingCompanyIds || [], false);
    byId("initiative-entity-scope").value = cleanText(item?.entityScope || "");
    byId("initiative-wave").value = cleanText(item?.waveOrPhase || "");
    byId("initiative-priority").value = cleanText(item?.priority || "");
    byId("initiative-complexity").value = cleanText(item?.complexity || "");
    byId("initiative-class").value = cleanText(item?.initiativeClass || "");
    byId("initiative-maturity").value = cleanText(item?.maturity || "");
    byId("initiative-start-date").value = cleanText(item?.startDate ? toIsoDate(item.startDate) : "");
    byId("initiative-end-date").value = cleanText(item?.endDate ? toIsoDate(item.endDate) : "");
    byId("initiative-reporting-frequency").value = cleanText(item?.reportingFrequency || "");
    byId("initiative-contribution-metric").value = cleanText(item?.contributionMetricName || "");
    byId("initiative-contribution-unit").value = cleanText(item?.contributionUnitOfMeasure || "");
    ensureSelectValue("initiative-contribution-granularity", item?.contributionPlanGranularity || "InheritFromObjective", item?.contributionPlanGranularity || "Inherit from Objective");
    byId("initiative-contribution-granularity").value = cleanText(item?.contributionPlanGranularity || "InheritFromObjective");
    byId("initiative-contribution-method").value = cleanText(item?.contributionMethod || "");
    byId("initiative-contribution-timing").value = cleanText(item?.contributionTiming || "");
    byId("initiative-benefit-hypothesis").value = cleanText(item?.benefitHypothesis || "");
    byId("initiative-benefit-start").value = cleanText(item?.benefitRealizationStart ? toIsoDate(item.benefitRealizationStart) : "");
    byId("initiative-benefit-end").value = cleanText(item?.benefitRealizationEnd ? toIsoDate(item.benefitRealizationEnd) : "");
    byId("initiative-budget-envelope").value = cleanText(item?.budgetEnvelope || "");
    byId("initiative-budget-amount").value = item?.budgetAmount ?? "";
    byId("initiative-currency").value = cleanText(item?.currencyCode || "");
    byId("initiative-funding-source").value = cleanText(item?.fundingSource || "");
    byId("initiative-strategy-alignment-note").value = cleanText(item?.strategyAlignmentNote || "");
    byId("initiative-governance-stage").value = cleanText(item?.governanceStage || "");
    byId("initiative-decision-reference").value = cleanText(item?.decisionReference || "");
    byId("initiative-evidence-reference").value = cleanText(item?.evidenceReference || "");
    byId("initiative-governance-notes").value = cleanText(item?.governanceNotes || "");
    byId("initiative-dependency-flag").checked = Boolean(item?.dependencyFlag);
    byId("initiative-notes").value = cleanText(item?.notes || "");
    contributionPlanRows = Array.isArray(item?.contributionPlanValues) ? item.contributionPlanValues.map((row) => ({
      periodKey: cleanText(row.periodKey || ""),
      periodLabel: cleanText(row.periodLabel || ""),
      periodStart: cleanText(row.periodStart ? toIsoDate(row.periodStart) : row.periodStart),
      periodEnd: cleanText(row.periodEnd ? toIsoDate(row.periodEnd) : row.periodEnd),
      plannedValue: row.plannedValue ?? null,
      forecastValue: row.forecastValue ?? null,
      actualValue: row.actualValue ?? null,
      commentary: cleanText(row.commentary || ""),
    })) : [];
    sourceTemplateId = cleanText(item?.sourceTemplateId || "");
    sourceTemplateVersion = item?.sourceTemplateVersion ?? null;
    if (sourceTemplateId) {
      const editMode = cleanText(item?.parentObjectiveId || "") ? "ObjectiveTemplate" : "Template";
      byId("initiative-creation-mode-select").value = editMode;
      selectedTemplateMeta = {
        name: cleanText(item?.initiativeName || sourceTemplateId),
        type: cleanText(item?.type || "")
      };
    }
    currentObjective = normalizeObjectiveRow(objectivesCache.find((objective) => objective.id === cleanText(item?.parentObjectiveId || "")) || {});
    refreshInitiativeTypeOptions();
    applyObjectiveContext();
    syncDateConstraints();
    renderSourceSummary(selectedTemplateMeta);
    renderContributionPlan();
    syncContributionGranularityConstraint();
    renderReadiness();
  }

  async function loadEditRecord() {
    if (!isEditMode) return;
    const detail = await window.initiativeStrategyApi.get(editId);
    const item = detail?.strategyLink || detail?.initiative || {};
    populateForm(item);
    if (cleanText(item?.parentObjectiveId || "")) {
      await hydrateCurrentObjective(item.parentObjectiveId);
      applyObjectiveContext();
    }
  }

  async function applyParentObjectivePrefill() {
    if (isEditMode) return;

    const queryParentObjectiveId = cleanText(new URLSearchParams(window.location.search).get("parentObjectiveId"));
    const candidateId = cleanText(prefillParentObjectiveId || queryParentObjectiveId);
    if (!candidateId || cleanText(byId("initiative-parent-objective")?.value)) return;

    const objective = objectivesCache.find((item) => cleanText(item?.id) === candidateId);
    if (!objective) return;

    byId("initiative-parent-objective").value = objective.id;
    await hydrateCurrentObjective(objective.id);
    applyObjectiveContext();
  }

  function clearTemplateSelection({ resetMode = false } = {}) {
    sourceTemplateId = "";
    sourceTemplateVersion = null;
    selectedTemplateMeta = null;
    if (resetMode && byId("initiative-creation-mode-select")) {
      byId("initiative-creation-mode-select").value = "Blank";
    }
    renderSourceSummary(null);
    syncTemplateGateState();
  }

  function ensureTemplateStillCompatible() {
    if (!sourceTemplateId || !selectedTemplateMeta) return;
    const compatibility = templateCompatibility(selectedTemplateMeta);
    if (compatibility.compatible) return;
    if (!isEditMode) {
      clearTemplateSelection({ resetMode: false });
      renderErrors({ template: compatibility.reasons });
      notify("Selected Initiative Template was cleared because it is not compatible with the current Parent Objective.", "warning");
      return;
    }
    renderErrors({ template: compatibility.reasons });
  }

  function stepOneValidationMessages() {
    const messages = [];
    const parentObjectiveId = cleanText(byId("initiative-parent-objective")?.value);
    const initiativeName = cleanText(byId("initiative-name")?.value);
    const initiativeType = cleanText(byId("initiative-type")?.value);
    const parentGoal = selectedGoal();

    if (!parentObjectiveId) messages.push("Parent Objective is required before continuing.");
    if (!initiativeName) messages.push("Initiative Name is required before continuing.");
    if (!initiativeType) messages.push("Initiative Type is required before continuing.");
    if (parentObjectiveId && !cleanText(parentGoal?.id || "")) messages.push("Parent Goal could not be derived from the selected Parent Objective.");
    if (initiativeType && currentObjective && !isObjectiveCompatibleWithInitiativeType(currentObjective, initiativeType)) {
      messages.push("Initiative Type is not compatible with the selected Parent Objective.");
    }
    if (sourceTemplateId && selectedTemplateMeta) {
      messages.push(...templateCompatibility(selectedTemplateMeta).reasons);
    }
    return unique(messages);
  }

  function syncStepOneUi() {
    const anchorMessageEl = byId("initiative-step-one-top-message");
    const parentObjectiveHelpEl = byId("initiative-parent-objective-help");
    const derivedContextCopyEl = byId("initiative-derived-context-copy");
    const hasObjective = Boolean(currentObjective && cleanText(currentObjective.id));
    const goal = selectedGoal();

    if (anchorMessageEl) {
      anchorMessageEl.textContent = hasObjective
        ? "Initiative is anchored to the selected Parent Objective. Parent Goal and planning context are inherited here, and template selection is filtered by that anchor."
        : "Initiative must be linked to a Parent Objective before delivery planning begins. Parent Goal and planning context are inherited from that Objective, and compatible templates load only after the anchor is chosen.";
    }

    if (parentObjectiveHelpEl) {
      parentObjectiveHelpEl.textContent = hasObjective
        ? "Parent Objective is the mandatory anchor for this Initiative. Changing it will refresh the derived strategy context and compatible template set."
        : "Select the Parent Objective first so this Initiative can inherit strategic lineage, planning horizon, and compatible template context.";
    }

    if (derivedContextCopyEl) {
      derivedContextCopyEl.textContent = hasObjective
        ? `This Initiative inherits strategic lineage, Objective Type, Entity Scope, and planning constraints from the selected Parent Objective${cleanText(goal?.name) ? ` under Goal ${cleanText(goal.name)}` : ""}.`
        : "Select Parent Objective to derive Parent Goal, Parent Strategy Period, Objective Type, Objective Entity Scope, Objective Target Granularity, and Objective Horizon.";
    }

    syncTemplateGateState();
  }

  async function initialize() {
    try {
      await workbook.ensureLookupsLoaded?.();
      await workbook.ensureUsersLoaded?.();
      await workbook.ensureCompaniesLoaded?.();
      await workbook.ensurePositionsLoaded?.();
      const [objectives, goals, strategyPeriodsResult] = await Promise.allSettled([
        window.strategyObjectivesApi.list(),
        window.strategyGoalsApi.list(),
        window.strategyPlanningApi?.listStrategyPeriods?.()
      ]);
      const objectiveItems = objectives.status === "fulfilled" ? (objectives.value?.items || []) : [];
      const goalItems = goals.status === "fulfilled" ? (goals.value?.items || []) : [];
      const strategyPeriods = strategyPeriodsResult.status === "fulfilled"
        ? (Array.isArray(strategyPeriodsResult.value) ? strategyPeriodsResult.value : (strategyPeriodsResult.value?.items || []))
        : [];
      objectivesCache = objectiveItems.map((item) => normalizeObjectiveRow(item)).filter((item) => item.id);
      goalItems.map((goal) => normalizeGoalRow(goal)).filter((goal) => goal.id).forEach((goal) => goalsById.set(goal.id, goal));
      strategyPeriodsById = new Map((strategyPeriods || []).map((period) => [cleanText(period?.id || ""), period]).filter(([id]) => id));
      hydrateStaticOptions();
      refreshOwnerRoleOptions();
      refreshSponsorRoleOptions();
      refreshOwnerPersonOptions();
      refreshSponsorPersonOptions();
      initCompanyPicker();
      if (isEditMode) await loadEditRecord();
      else await applyParentObjectivePrefill();
      syncStepOneUi();
      renderSourceSummary(selectedTemplateMeta);
      renderReadiness();
    } catch (error) {
      renderErrors({
        init: [window.enterpriseStrategyUi?.getErrorMessage?.(error, "Unable to load initiative workspace.") || "Unable to load initiative workspace."]
      });
    }
  }

  wizardButtons.forEach((button) => button.addEventListener("click", () => {
    const targetStep = Number(button.dataset.step || 1);
    if (currentStep === 1 && targetStep > 1) {
      const stepOneErrors = stepOneValidationMessages();
      if (stepOneErrors.length) {
        renderErrors({ step1: stepOneErrors });
        return;
      }
    }
    renderErrors({});
    setStep(targetStep);
  }));
  backBtn?.addEventListener("click", () => setStep(currentStep - 1));
  nextBtn?.addEventListener("click", () => {
    if (currentStep === 1) {
      const stepOneErrors = stepOneValidationMessages();
      if (stepOneErrors.length) {
        renderErrors({ step1: stepOneErrors });
        return;
      }
    }
    renderErrors({});
    setStep(currentStep + 1);
  });
  saveBtn?.addEventListener("click", () => { void saveInitiative(); });

  byId("initiative-parent-objective")?.addEventListener("change", async () => {
    await hydrateCurrentObjective(cleanText(byId("initiative-parent-objective")?.value));
    refreshInitiativeTypeOptions();
    applyObjectiveContext();
    ensureTemplateStillCompatible();
  });

  byId("initiative-owner-company")?.addEventListener("change", () => {
    refreshOwnerRoleOptions();
    refreshOwnerPersonOptions();
    renderReadiness();
  });
  byId("initiative-owner-role")?.addEventListener("change", () => {
    refreshOwnerPersonOptions();
    applySponsorPrefill();
    renderReadiness();
  });
  byId("initiative-sponsoring-company")?.addEventListener("change", () => {
    refreshSponsorRoleOptions();
    applySponsorPrefill();
    refreshSponsorPersonOptions();
    renderReadiness();
  });
  byId("initiative-sponsor-role")?.addEventListener("change", () => {
    refreshSponsorPersonOptions();
    renderReadiness();
  });
  byId("initiative-status-editable")?.addEventListener("change", () => {
    syncStatusInputs();
    renderReadiness();
  });

  ["initiative-start-date", "initiative-end-date"].forEach((id) => {
    byId(id)?.addEventListener("change", () => {
      syncDateConstraints();
      renderReadiness();
    });
  });

  [
    "initiative-name",
    "initiative-type",
    "initiative-owner-person",
    "initiative-executive-sponsor",
    "initiative-sponsor-role",
    "initiative-sponsoring-company",
    "initiative-entity-scope",
    "initiative-wave",
    "initiative-priority",
    "initiative-complexity",
    "initiative-class",
    "initiative-maturity",
    "initiative-start-date",
    "initiative-end-date",
    "initiative-reporting-frequency",
    "initiative-contribution-metric",
    "initiative-contribution-unit",
    "initiative-contribution-granularity",
    "initiative-contribution-method",
    "initiative-contribution-timing",
    "initiative-benefit-start",
    "initiative-benefit-end",
    "initiative-benefit-hypothesis",
    "initiative-budget-envelope",
    "initiative-budget-amount",
    "initiative-currency",
    "initiative-funding-source",
    "initiative-strategy-alignment-note",
    "initiative-governance-stage",
    "initiative-governance-notes",
    "initiative-decision-reference",
    "initiative-evidence-reference",
    "initiative-dependency-flag"
  ].forEach((id) => {
    byId(id)?.addEventListener("change", renderReadiness);
    byId(id)?.addEventListener("input", renderReadiness);
  });

  byId("initiative-type")?.addEventListener("change", () => {
    refreshParentObjectiveOptions();
    ensureTemplateStillCompatible();
    applySponsorPrefill();
    if (modalEl?.classList.contains("show")) {
      updateSourcePickerContext({ notifyOnMismatch: true });
      renderTemplateRows();
    }
    renderReadiness();
  });

  byId("initiative-contribution-granularity")?.addEventListener("change", () => {
    syncContributionGranularityConstraint({ notifyUser: true });
  });

  byId("initiative-entity-scope")?.addEventListener("input", () => {
    if (modalEl?.classList.contains("show")) {
      updateSourcePickerContext();
      renderTemplateRows();
    }
  });
  byId("initiative-entity-scope")?.addEventListener("change", () => {
    if (modalEl?.classList.contains("show")) {
      updateSourcePickerContext({ notifyOnMismatch: true });
      renderTemplateRows();
    }
  });

  byId("initiative-generate-plan")?.addEventListener("click", () => generateContributionPlan(false));
  byId("initiative-regenerate-plan")?.addEventListener("click", () => generateContributionPlan(true));
  byId("initiative-fill-flat-plan")?.addEventListener("click", fillFlatPlan);
  byId("initiative-copy-down-plan")?.addEventListener("click", copyDownPlan);
  byId("initiative-interpolate-plan")?.addEventListener("click", interpolatePlan);
  byId("initiative-clear-plan")?.addEventListener("click", () => {
    contributionPlanRows = contributionPlanRows.map((row) => ({ ...row, plannedValue: null, forecastValue: null, actualValue: null, commentary: "" }));
    renderContributionPlan();
    renderReadiness();
  });

  byId("initiative-creation-mode-select")?.addEventListener("change", async () => {
    const mode = currentMode();
    if (mode === "Blank") {
      clearTemplateSelection({ resetMode: false });
      renderReadiness();
      return;
    }
    syncTemplateGateState();
    renderSourceSummary(selectedTemplateMeta);
    renderReadiness();
    if (!hasParentObjectiveAnchor()) {
      return;
    }
    await openTemplateBrowser();
    renderReadiness();
  });

  byId("initiative-browse-source")?.addEventListener("click", async () => {
    await openTemplateBrowser();
  });

  byId("initiative-clear-source")?.addEventListener("click", () => {
    clearTemplateSelection({ resetMode: true });
    renderReadiness();
  });

  ["initiative-source-picker-search", "initiative-source-picker-type", "initiative-source-picker-entity-scope", "initiative-source-picker-parent-objective-name"].forEach((id) => {
    byId(id)?.addEventListener("input", renderTemplateRows);
    byId(id)?.addEventListener("change", renderTemplateRows);
  });

  setStep(1);
  initialize().catch(() => {});
})(window, document);
