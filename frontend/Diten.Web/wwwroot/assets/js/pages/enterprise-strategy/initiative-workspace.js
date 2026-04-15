(function (window, document) {
  "use strict";

  const root = document.getElementById("initiative-create-workspace");
  if (!root) return;

  const workbook = window.enterpriseWorkbookOptions || {};
  const notify = (message, kind = "success") => window.enterpriseStrategyUi?.notify?.(message, kind);
  const modalEl = document.getElementById("initiativeSourcePickerModal");
  const templateModal = modalEl && window.bootstrap?.Modal ? new window.bootstrap.Modal(modalEl) : null;
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
  let goalsById = new Map();
  let currentObjective = null;
  let contributionPlanRows = [];
  let companyPickerActiveIndex = -1;

  const editId = String(root.dataset.editId || "").trim();
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

  function normalizeGranularity(value) {
    const normalized = cleanText(value).toLowerCase().replace(/\s+/g, "");
    if (normalized === "monthly") return "Monthly";
    if (normalized === "quarterly") return "Quarterly";
    if (normalized === "yearly") return "Yearly";
    if (normalized === "totalinitiativehorizon" || normalized === "totalstrategyperiod" || normalized === "total") return "TotalInitiativeHorizon";
    return "InheritFromObjective";
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

  function objectiveOptions() {
    return objectivesCache.map((item) => ({
      value: item.id,
      label: `${item.id} - ${item.name}`,
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
    return currentObjective ? goalsById.get(cleanText(currentObjective.parentGoalId)) || null : null;
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
    byId("initiative-parent-goal").value = parentGoal ? `${parentGoal.id} - ${parentGoal.name}` : "";
    byId("initiative-strategy-period").value = cleanText(parentGoal?.strategyPeriodId || parentGoal?.strategyPeriodCode || parentGoal?.strategyPeriodName || "");
    const objectiveGranularity = cleanText(currentObjective?.targetPlanGranularity || currentObjective?.targetPlanGranularityId || "");
    byId("initiative-objective-granularity").value = objectiveGranularity || "-";
    byId("initiative-objective-horizon").value = formatDateRange(currentObjective?.timeHorizonStart || currentObjective?.startDate, currentObjective?.timeHorizonEnd || currentObjective?.endDate);
    if (currentMode() === "ObjectiveTemplate") renderSourceSummary(selectedTemplateMeta);
    renderReadiness();
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
    const goalStart = parseDate(goal?.startDate);
    const goalEnd = parseDate(goal?.endDate);

    if (!cleanText(byId("initiative-parent-objective")?.value)) missing.push("Parent Objective");
    if (!cleanText(byId("initiative-name")?.value)) missing.push("Initiative Name");
    if (!cleanText(byId("initiative-type")?.value)) missing.push("Initiative Type");
    if (!cleanText(byId("initiative-owner-person")?.value)) missing.push("Initiative Owner");
    if (!cleanText(byId("initiative-sponsoring-company")?.value)) missing.push("Sponsoring Company");
    if (!startDate) missing.push("Start Period");
    if (!endDate) missing.push("End Period");

    if (startDate && endDate && endDate < startDate) saveBlockers.push("End Period must be on or after Start Period.");
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
    if (!cleanText(byId("initiative-executive-sponsor")?.value)) warnings.push("Accountable sponsor is still blank.");
    if (!cleanText(byId("initiative-sponsor-role")?.value)) warnings.push("Sponsor role is still blank.");
    if (selectedParticipatingCompanies().length === 0) warnings.push("Participating companies are not selected yet.");
    if (!cleanText(byId("initiative-reporting-frequency")?.value)) warnings.push("Reporting Frequency is still blank.");
    if (!cleanText(byId("initiative-funding-source")?.value)) warnings.push("Funding Source is still blank.");
    if (!cleanText(byId("initiative-strategy-alignment-note")?.value)) warnings.push("Strategy Alignment Note is still blank.");
    if (!cleanText(byId("initiative-governance-notes")?.value)) warnings.push("Governance / Evidence Note is still blank.");
    if (currentMode() === "ObjectiveTemplate" && !cleanText(byId("initiative-parent-objective")?.value)) warnings.push("Objective + Template source mode works best after selecting the Parent Objective.");

    const draftReady = missing.length === 0 && saveBlockers.length === 0;
    const planningReady = draftReady && planningBlockers.length === 0 && contributionPlanRows.length > 0;
    const publishReady = planningReady
      && cleanText(byId("initiative-reporting-frequency")?.value)
      && cleanText(byId("initiative-strategy-alignment-note")?.value)
      && cleanText(byId("initiative-governance-notes")?.value);
    if (!publishReady && planningReady) warnings.push("Publish readiness still needs Reporting Frequency, Strategy Alignment Note, and Governance / Evidence Note.");

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
      executiveSponsor: cleanText(byId("initiative-executive-sponsor")?.value),
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
    if (!sourceTemplateId || !meta) {
      host.classList.add("is-empty");
      host.innerHTML = `
        <div class="goal-source-summary-name">${escapeHtml(modeLabel())}</div>
        <div class="goal-source-summary-note">
          ${mode === "ObjectiveTemplate"
            ? (objectiveText
              ? `Parent Objective context: <code>${escapeHtml(objectiveText)}</code>. Choose an Initiative Template to layer source defaults onto this Objective anchor.`
              : "Select a Parent Objective, then choose an Initiative Template to prefill safe operational fields.")
            : "Start with a blank delivery-planning initiative, or choose an Initiative Template to prefill safe operational fields."}
        </div>`;
      return;
    }
    host.classList.remove("is-empty");
    host.innerHTML = `
      <div class="goal-source-summary-name">${escapeHtml(meta.name || sourceTemplateId)}</div>
      <div class="goal-source-summary-note">
        Mode: <strong>${escapeHtml(modeLabel())}</strong>
        | Template ID: <code>${escapeHtml(sourceTemplateId)}</code>
        ${meta.parentObjectiveTemplateId ? ` | Parent Objective Template: <code>${escapeHtml(meta.parentObjectiveTemplateId)}</code>` : ""}
        ${objectiveText && mode === "ObjectiveTemplate" ? ` | Runtime Parent Objective: <code>${escapeHtml(objectiveText)}</code>` : ""}
        ${meta.startDate || meta.endDate ? ` | Horizon hint: ${escapeHtml(formatDateRange(meta.startDate, meta.endDate))}` : ""}
      </div>`;
  }

  async function applyTemplate(templateId) {
    const detail = await window.strategyLibraryApi.template(templateId);
    const prefill = detail?.initiativePrefill || {};
    sourceTemplateId = cleanText(prefill.templateId || templateId);
    sourceTemplateVersion = Number(prefill.version ?? detail?.version ?? 0) || null;
    selectedTemplateMeta = {
      name: cleanText(prefill.name || detail?.name || sourceTemplateId),
      description: cleanText(prefill.description || detail?.attributes?.Description || ""),
      parentObjectiveTemplateId: cleanText(prefill.parentObjectiveTemplateId || detail?.attributes?.ParentObjectiveTemplateId || ""),
      startDate: prefill.startDate || "",
      endDate: prefill.endDate || "",
    };

    const setIfBlank = (id, value) => {
      const el = byId(id);
      if (el && !cleanText(el.value) && cleanText(value)) el.value = cleanText(value);
    };

    setIfBlank("initiative-name", prefill.name);
    setIfBlank("initiative-description", prefill.description);
    setIfBlank("initiative-type", prefill.type);
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
    if (!cleanText(byId("initiative-status-editable")?.value) && cleanText(prefill.lifecycleStatus)) {
      byId("initiative-status-editable").value = cleanText(prefill.lifecycleStatus);
    }
    if (!cleanText(byId("initiative-start-date")?.value) && prefill.startDate) byId("initiative-start-date").value = toIsoDate(prefill.startDate);
    if (!cleanText(byId("initiative-end-date")?.value) && prefill.endDate) byId("initiative-end-date").value = toIsoDate(prefill.endDate);
    syncStatusInputs();
    renderSourceSummary(selectedTemplateMeta);
    refreshOwnerPersonOptions();
    renderReadiness();
  }

  function normalizeTemplateRow(row) {
    return {
      id: cleanText(row.id || row.templateCode || ""),
      name: cleanText(row.name || ""),
      description: cleanText(row.description || row.attributes?.Description || ""),
      owner: cleanText(row.owner || ""),
      type: cleanText(row.type || row.attributes?.Type || ""),
      priority: cleanText(row.priority || ""),
      status: cleanText(row.status || ""),
      templateType: cleanText(row.templateType || row.itemType || ""),
      parentObjectiveTemplateId: cleanText(row.parentObjectiveTemplateId || row.parentObjectiveId || ""),
    };
  }

  function renderTemplateRows() {
    const tbody = byId("initiative-source-picker-tbody");
    if (!tbody) return;
    const search = cleanText(byId("initiative-source-picker-search")?.value).toLowerCase();
    const type = cleanText(byId("initiative-source-picker-type")?.value);
    const status = cleanText(byId("initiative-source-picker-status")?.value);
    const filtered = templateRows.filter((row) => {
      if (search && !`${row.id} ${row.name} ${row.description} ${row.owner}`.toLowerCase().includes(search)) return false;
      if (type && row.type !== type) return false;
      if (status && row.status !== status) return false;
      return true;
    });
    tbody.innerHTML = filtered.length ? filtered.map((row) => `
      <tr>
        <td>${escapeHtml(row.id)}</td>
        <td>${escapeHtml(row.parentObjectiveTemplateId || "-")}</td>
        <td>${escapeHtml(row.name)}</td>
        <td>${escapeHtml(row.description || "-")}</td>
        <td>${escapeHtml(row.type || "-")}</td>
        <td>${escapeHtml(row.owner || "-")}</td>
        <td>${escapeHtml(row.priority || "-")}</td>
        <td>${escapeHtml(row.status || "-")}</td>
        <td><button type="button" class="btn btn-sm btn-outline-primary" data-template-id="${escapeHtml(row.id)}">Use</button></td>
      </tr>`).join("") : '<tr><td colspan="9" class="text-center text-muted py-3">No matching Initiative Templates found.</td></tr>';
    tbody.querySelectorAll("[data-template-id]").forEach((button) => {
      button.addEventListener("click", async () => {
        await applyTemplate(button.dataset.templateId);
        templateModal?.hide();
      });
    });
  }

  async function loadTemplates() {
    const data = await window.strategyLibraryApi.catalog({ templateType: "Initiative" }, { skipCache: true });
    templateRows = (data?.items || data || [])
      .map(normalizeTemplateRow)
      .filter((row) => row.id && cleanText(row.templateType || "Initiative").toLowerCase().includes("initiative"));
    fillSelect("initiative-source-picker-type", [...new Set(templateRows.map((row) => row.type).filter(Boolean))], "All types");
    fillSelect("initiative-source-picker-status", [...new Set(templateRows.map((row) => row.status).filter(Boolean))], "All statuses");
    renderTemplateRows();
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
    const scoped = companyId && typeof workbook.positionOptionsForCompany === "function"
      ? (workbook.positionOptionsForCompany(companyId) || [])
      : [];
    const options = scoped.length ? scoped : positionOptions();
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
      ? "Owner role defaults from template when available and can be refined here."
      : "Position service unavailable or no roles available for the selected company.";
  }

  function refreshSponsorRoleOptions() {
    const current = cleanText(byId("initiative-sponsor-role")?.value);
    fillSelect("initiative-sponsor-role", positionOptions(), "Select sponsor role");
    ensureSelectValue("initiative-sponsor-role", current, workbook.positionDisplayName?.(current) || current);
  }

  function refreshOwnerPersonOptions() {
    const companyId = cleanText(byId("initiative-owner-company")?.value);
    const roleId = cleanText(byId("initiative-owner-role")?.value);
    const current = cleanText(byId("initiative-owner-person")?.value);
    const scoped = companyId && roleId && typeof workbook.usersForOwnershipContext === "function"
      ? (workbook.usersForOwnershipContext(companyId, roleId, { activeOnly: false }) || [])
      : userOptions();
    fillSelect("initiative-owner-person", scoped, "Select owner");
    ensureSelectValue("initiative-owner-person", current, current);
    byId("initiative-owner-person-help").textContent = companyId && roleId
      ? "Owner list is filtered by company and owner role when data is available."
      : "Choose the accountable delivery owner for this Initiative.";
  }

  function hydrateStaticOptions() {
    fillSelect("initiative-parent-objective", objectiveOptions(), "Select parent objective");
    fillSelect("initiative-type", resolveOptions(workbook.initiativeTypes, resolveOptions(workbook.goalObjectiveTypes)), "Select type");
    fillSelect("initiative-status-editable", workbook.lifecycleStatus || ["Draft", "Planned", "Approved", "In Progress", "Completed", "Cancelled"], "Select status", { defaultValue: "Draft" });
    fillSelect("initiative-wave", workbook.waveValues || ["Wave 1", "Wave 2", "Wave 3"], "Select wave / phase");
    fillSelect("initiative-maturity", workbook.maturityValues || ["Emerging", "Defined", "Ready", "In Flight", "Scaled", "Stabilized"], "Select maturity / readiness");
    fillSelect("initiative-priority", workbook.priorities || ["Critical", "High", "Medium", "Low"], "Select priority");
    fillSelect("initiative-complexity", workbook.complexityRiskScale || ["Very High", "High", "Medium", "Low"], "Select complexity");
    fillSelect("initiative-reporting-frequency", workbook.reportingFrequencies || ["Monthly", "Quarterly", "Yearly"], "Select frequency");
    fillSelect("initiative-owner-company", companyOptions(), "Select owner company / org");
    fillSelect("initiative-sponsoring-company", companyOptions(), "Select sponsoring company");
    fillSelect("initiative-owner-person", userOptions(), "Select owner");
    fillSelect("initiative-currency", workbook.currencyCodes || ["USD", "EUR", "GBP", "TRY"], "Select currency");
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
    byId("initiative-parent-objective").value = cleanText(item?.parentObjectiveId || "");
    byId("initiative-type").value = cleanText(item?.type || "");
    byId("initiative-status-editable").value = cleanText(item?.status || "Draft");
    byId("initiative-status").value = cleanText(item?.status || "Draft");
    syncStatusInputs();
    byId("initiative-owner-company").value = cleanText(item?.deliveryOwnerCompanyId || "");
    refreshOwnerRoleOptions();
    ensureSelectValue("initiative-owner-role", item?.deliveryOwnerPositionId || "", workbook.positionDisplayName?.(item?.deliveryOwnerPositionId) || item?.deliveryOwnerPositionId || "");
    refreshOwnerPersonOptions();
    ensureSelectValue("initiative-owner-person", item?.deliveryOwnerPersonId || item?.owner || "", item?.owner || item?.deliveryOwnerPersonId || "");
    byId("initiative-executive-sponsor").value = cleanText(item?.executiveSponsor || "");
    refreshSponsorRoleOptions();
    ensureSelectValue("initiative-sponsor-role", item?.accountableSponsorRole || "", workbook.positionDisplayName?.(item?.accountableSponsorRole) || item?.accountableSponsorRole || "");
    byId("initiative-sponsoring-company").value = cleanText(item?.sponsoringCompanyId || "");
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
      selectedTemplateMeta = { name: cleanText(item?.initiativeName || sourceTemplateId) };
    }
    currentObjective = objectivesCache.find((objective) => objective.id === cleanText(item?.parentObjectiveId || "")) || null;
    applyObjectiveContext();
    renderSourceSummary(selectedTemplateMeta);
    renderContributionPlan();
    renderReadiness();
  }

  async function loadEditRecord() {
    if (!isEditMode) return;
    const detail = await window.initiativeStrategyApi.get(editId);
    populateForm(detail?.strategyLink || detail?.initiative || {});
  }

  async function initialize() {
    try {
      const [objectives, goals] = await Promise.all([
        window.strategyObjectivesApi.list(),
        window.strategyGoalsApi.list(),
      ]);
      objectivesCache = objectives?.items || [];
      (goals?.items || []).forEach((goal) => goalsById.set(cleanText(goal.id), goal));
      await ensurePositionOptionsLoaded();
      hydrateStaticOptions();
      refreshOwnerRoleOptions();
      refreshSponsorRoleOptions();
      refreshOwnerPersonOptions();
      initCompanyPicker();
      if (isEditMode) await loadEditRecord();
      renderSourceSummary(selectedTemplateMeta);
      renderReadiness();
    } catch (error) {
      renderErrors({
        init: [window.enterpriseStrategyUi?.getErrorMessage?.(error, "Unable to load initiative workspace.") || "Unable to load initiative workspace."]
      });
    }
  }

  wizardButtons.forEach((button) => button.addEventListener("click", () => setStep(button.dataset.step)));
  backBtn?.addEventListener("click", () => setStep(currentStep - 1));
  nextBtn?.addEventListener("click", () => setStep(currentStep + 1));
  saveBtn?.addEventListener("click", () => { void saveInitiative(); });

  byId("initiative-parent-objective")?.addEventListener("change", () => {
    currentObjective = objectivesCache.find((objective) => objective.id === cleanText(byId("initiative-parent-objective")?.value)) || null;
    applyObjectiveContext();
  });

  byId("initiative-owner-company")?.addEventListener("change", () => {
    refreshOwnerRoleOptions();
    refreshOwnerPersonOptions();
    renderReadiness();
  });
  byId("initiative-owner-role")?.addEventListener("change", () => {
    refreshOwnerPersonOptions();
    renderReadiness();
  });
  byId("initiative-status-editable")?.addEventListener("change", () => {
    syncStatusInputs();
    renderReadiness();
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
      sourceTemplateId = "";
      sourceTemplateVersion = null;
      selectedTemplateMeta = null;
      renderSourceSummary(null);
      renderReadiness();
      return;
    }
    await loadTemplates();
    renderSourceSummary(selectedTemplateMeta);
    templateModal?.show();
    renderReadiness();
  });

  byId("initiative-browse-source")?.addEventListener("click", async () => {
    if (currentMode() === "Blank") {
      byId("initiative-creation-mode-select").value = "Template";
    }
    await loadTemplates();
    renderSourceSummary(selectedTemplateMeta);
    templateModal?.show();
  });

  byId("initiative-clear-source")?.addEventListener("click", () => {
    sourceTemplateId = "";
    sourceTemplateVersion = null;
    selectedTemplateMeta = null;
    byId("initiative-creation-mode-select").value = "Blank";
    renderSourceSummary(null);
    renderReadiness();
  });

  ["initiative-source-picker-search", "initiative-source-picker-type", "initiative-source-picker-status"].forEach((id) => {
    byId(id)?.addEventListener("input", renderTemplateRows);
    byId(id)?.addEventListener("change", renderTemplateRows);
  });

  setStep(1);
  initialize().catch(() => {});
})(window, document);
