(function (window, document) {
  "use strict";
  "use strict";

  const cycleId = String(window.planningCycleDetailId || "").trim();
  const api = window.strategyPlanningApi;
  const goalsApi = window.strategyGoalsApi;
  const objectivesApi = window.strategyObjectivesApi;
  const initiativesApi = window.initiativeStrategyApi;
  const ui = window.enterpriseStrategyUi || {};
  const workbook = window.enterpriseWorkbookOptions || {};
  const strategyPeriodOffcanvasEl = document.getElementById("strategyPeriodOffcanvas");
  const strategyPeriodOffcanvas = strategyPeriodOffcanvasEl ? new bootstrap.Offcanvas(strategyPeriodOffcanvasEl) : null;

  const detailReadinessIndicatorEl = document.getElementById("planning-cycle-detail-readiness-indicator");
  const detailReadinessTextEl = document.getElementById("planning-cycle-detail-readiness-text");
  const detailReadinessMissingEl = document.getElementById("planning-cycle-detail-readiness-missing");
  const detailReadinessBlockersEl = document.getElementById("planning-cycle-detail-readiness-blockers");
  const detailIdentityStateEl = document.getElementById("planning-cycle-detail-sec-identity-state");
  const detailHorizonStateEl = document.getElementById("planning-cycle-detail-sec-horizon-state");

  const addPeriodReadinessIndicatorEl = document.getElementById("cycle-period-readiness-indicator");
  const addPeriodReadinessTextEl = document.getElementById("cycle-period-readiness-text");
  const addPeriodReadinessMissingEl = document.getElementById("cycle-period-readiness-missing");
  const addPeriodReadinessBlockersEl = document.getElementById("cycle-period-readiness-blockers");
  const addPeriodIdentityStateEl = document.getElementById("cycle-period-sec-identity-state");
  const addPeriodScopeStateEl = document.getElementById("cycle-period-sec-scope-state");
  const addPeriodTimingStateEl = document.getElementById("cycle-period-sec-timing-state");

  let cycle = null;
  let cycles = [];
  let periods = [];
  let lookups = {};
  let ownerRefs = [];
  let globalPositions = [];
  let usageSnapshot = { periods: 0, goals: 0, objectives: 0, initiatives: 0 };
  let companyOptions = [];
  let editingPeriodId = "";
  let offcanvasEl = document.getElementById("planningCycleEditorOffcanvas");
  let offcanvas = offcanvasEl ? new bootstrap.Offcanvas(offcanvasEl) : null;

  function text(v) { return String(v || "").trim(); }
  function fmtDate(v) {
    if (!v) return "-";
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? "-" : d.toLocaleDateString();
  }
  function isoDate(v) { return text(v).slice(0, 10); }
  function notify(message, kind) { ui.notify?.(message, kind || "success"); }
  function showError(err, fallback) { notify(ui.getErrorMessage?.(err, fallback) || fallback, "danger"); }
  function byId(id) { return document.getElementById(id); }
  function isStatus(row, value) { return text(row?.status).toLowerCase() === value.toLowerCase(); }
  function statusPill(status) {
    const normalized = text(status).toLowerCase();
    if (normalized === "active") return { cls: "badge bg-label-success", label: "Active" };
    if (normalized === "draft") return { cls: "badge bg-label-primary", label: "Draft" };
    if (normalized === "archived") return { cls: "badge bg-label-secondary", label: "Archived" };
    return { cls: "badge bg-label-info", label: text(status) || "-" };
  }

  function ownerLabel(ownerId) {
    const id = text(ownerId).toLowerCase();
    if (!id) return "-";
    const hit = ownerRefs.find((x) => text(x.ownerId || x.value).toLowerCase() === id);
    return text(hit?.displayName || hit?.label || ownerId);
  }

  function ownerRoleLabel(value) {
    if (!value) return "";
    const fromPositions = workbook.positionDisplayName?.(value);
    if (text(fromPositions)) return text(fromPositions);
    if (typeof value === "string") return text(value);
    return text(value.positionName || value.name || value.roleName || value.title || value.code || value.id || "");
  }

  function normalizeOwnerOption(row) {
    const ownerId = text(row?.ownerId || row?.value || row?.id);
    if (!ownerId) return null;
    const baseLabel = text(row?.fullName || row?.displayName || row?.label || ownerId);
    return { ownerId, value: ownerId, displayName: baseLabel, label: baseLabel };
  }

  function ownerReferencesFromSources() {
    const workbookRefs = Array.isArray(workbook.ownerReferences)
      ? workbook.ownerReferences
      : (typeof workbook.ownerReferences === "function" ? workbook.ownerReferences() : []);
    const source = workbookRefs.length ? workbookRefs : (lookups.ownerReferences || []);
    const seen = new Set();
    return source
      .map(normalizeOwnerOption)
      .filter(Boolean)
      .filter((x) => {
        const key = text(x.ownerId).toLowerCase();
        if (!key || seen.has(key)) return false;
        seen.add(key);
        return true;
      });
  }

  async function refreshOwnerPositions() {
    const positionEl = byId("planning-cycle-owner-position");
    if (!positionEl) return;
    const current = text(positionEl.value);
    
    await workbook.ensurePositionsLoaded?.();
    const options = lookups.positions || [];
    
    workbook.fillSelect?.(positionEl, options, { placeholder: options.length ? "Select owner position" : "No positions available", keepCurrent: false });
    positionEl.removeAttribute("disabled"); // Force enabled as requested
    if (current && options.some((o) => String(o.value || o.id) === current)) positionEl.value = current;
    syncCurrentOwnerPerson();
  }

  function syncCurrentOwnerPerson() {
    const positionId = text(byId("planning-cycle-owner-position")?.value);
    const personSelect = byId("planning-cycle-current-owner-person");
    if (!personSelect || !$(personSelect).length) return;
    if (!positionId) {
      $(personSelect).val("").trigger("change");
      return;
    }
    const match = workbook.positionIncumbent?.(positionId);
    if (match && match.incumbentPersonId) {
      const incumbentId = text(match.incumbentPersonId);
      if (Array.from(personSelect.options).some(o => o.value === incumbentId)) {
        $(personSelect).val(incumbentId).trigger("change");
      }
    } else {
      $(personSelect).val("").trigger("change");
    }
  }

  async function refreshPeriodOwnerPositions() {
    const positionEl = byId("cycle-period-owner-position");
    if (!positionEl) return;
    const current = text(positionEl.value);
    const personSelect = byId("cycle-period-current-owner-person");
    const currentPerson = text(personSelect?.value);
    
    // Fallback to global positions if not yet initialized
    const options = (globalPositions && globalPositions.length > 0) ? globalPositions : (lookups.positions || []);
    
    workbook.fillSelect?.(positionEl, options, { placeholder: options.length ? "Select owner position" : "No positions available", keepCurrent: false });
    
    const $pos = $(positionEl);
    if ($pos.length && $.fn.select2) {
        $pos.prop("disabled", false).trigger("change"); // Force enabled
    } else {
        positionEl.removeAttribute("disabled");
    }

    if (current && options.some((o) => String(o.value || o.id) === current)) {
        positionEl.value = current;
        $pos.trigger("change");
    }
    syncPeriodCurrentOwnerPerson();
    if (currentPerson && personSelect && Array.from(personSelect.options).some((option) => option.value === currentPerson)) {
      $(personSelect).val(currentPerson).trigger("change.select2");
    }
  }

  function syncPeriodCurrentOwnerPerson() {
    const positionId = text(byId("cycle-period-owner-position")?.value);
    const personSelect = byId("cycle-period-current-owner-person");
    if (!personSelect || !$(personSelect).length) return;
    if (!positionId) {
      $(personSelect).val("").trigger("change");
      return;
    }
    const match = workbook.positionIncumbent?.(positionId);
    if (match && match.incumbentPersonId) {
      const incumbentId = text(match.incumbentPersonId);
      if (Array.from(personSelect.options).some(o => o.value === incumbentId)) {
        $(personSelect).val(incumbentId).trigger("change");
      }
    } else {
      $(personSelect).val("").trigger("change");
    }
  }

  function syncPeriodOwnerCompany() {
    const scopeCompany = byId("cycle-period-company")?.value;
    const ownerCompany = byId("cycle-period-owner-company");
    if (ownerCompany && scopeCompany && !ownerCompany.value) {
        ownerCompany.value = scopeCompany;
        $(ownerCompany).trigger("change"); // Notify Select2
        refreshPeriodOwnerPositions();
    }
  }

  function setStatus(status) {
    const hiddenInput = byId("planning-cycle-status");
    if (!hiddenInput) return;
    status = status || "Draft";
    hiddenInput.value = status;
    const container = byId("planning-cycle-status-container");
    if (!container) return;
    const pills = container.querySelectorAll(".status-pill");
    pills.forEach(pill => {
      const pStatus = pill.dataset.status;
      pill.className = "badge rounded-pill cursor-pointer status-pill px-3 py-2";
      if (pStatus === "Draft") pill.classList.add("bg-label-primary");
      else if (pStatus === "Active") pill.classList.add("bg-label-success");
      else if (pStatus === "Archived") pill.classList.add("bg-label-secondary");
      if (pStatus === status) {
        pill.classList.add("border", "border-primary", "border-2", "fw-bold");
        pill.style.opacity = "1";
      } else {
        pill.style.opacity = "0.4";
      }
    });
  }

  function companyLabel(companyId) {
    const id = text(companyId).toLowerCase();
    if (!id) return "-";
    // Check local options first
    let hit = companyOptions.find((x) => text(x.value).toLowerCase() === id);
    if (hit) return hit.label;
    // Fallback to workbook global options
    const options = workbook.companyOptions?.() || [];
    hit = options.find((x) => text(x.value).toLowerCase() === id);
    return hit?.label || companyId || "-";
  }
  function cycleLabel(row) {
    const direct = text(row?.planningCycleName || row?.planningCycleCode);
    if (direct) return direct;
    if (cycle && text(cycle.id).toLowerCase() === text(row?.planningCycleId).toLowerCase()) {
      return text(cycle.name || cycle.code) || "-";
    }
    const hit = cycles.find((x) => text(x.id).toLowerCase() === text(row?.planningCycleId).toLowerCase());
    return text(hit?.name || hit?.code) || text(cycle?.name || cycle?.code) || "-";
  }
  function compactDate(value) {
    if (!value) return "-";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "-";
    const day = String(date.getDate()).padStart(2, "0");
    const month = date.toLocaleString("en-US", { month: "short" });
    const year = String(date.getFullYear()).slice(-2);
    return `${day} ${month} ${year}`;
  }
  function timelineMarkup(startDate, endDate) {
    if (!startDate || !endDate) return "-";

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const toDate = new Date(endDate);
    const diffDays = Math.ceil((toDate - today) / (1000 * 60 * 60 * 24));

    let toColorClass = "text-success";
    if (diffDays < 0) toColorClass = "text-danger";
    else if (diffDays <= 15) toColorClass = "text-warning";

    return `
      <div class="d-flex flex-column align-items-center small fw-medium">
        <span class="text-muted mb-1">${compactDate(startDate)}</span>
        <span class="${toColorClass}">${compactDate(endDate)}</span>
      </div>`;
  }
  function usageBadge(row) {
    const totalUsage = Number(row?.goalsCount || 0) + Number(row?.objectivesCount || 0) + Number(row?.initiativesCount || 0);
    if (totalUsage > 0) return `<span class="badge bg-label-primary">${totalUsage} linked</span>`;
    return '<span class="badge bg-label-light text-body-secondary">Unused</span>';
  }
  function clearPeriodFormError() {
    const host = byId("cycle-period-form-error");
    if (!host) return;
    host.classList.add("d-none");
    host.innerHTML = "";
  }
  function showPeriodFormError(lines) {
    const host = byId("cycle-period-form-error");
    if (!host) return;
    const list = (lines || []).filter(Boolean);
    if (!list.length) return clearPeriodFormError();
    host.classList.remove("d-none");
    host.innerHTML = `<strong>Could not create Strategy Period.</strong><ul class="mb-0">${list.map((x) => `<li>${x}</li>`).join("")}</ul>`;
  }
  function clearCycleFormError() {
    const host = byId("planning-cycle-form-error");
    if (!host) return;
    host.classList.add("d-none");
    host.innerHTML = "";
  }
  function showCycleFormError(lines) {
    const host = byId("planning-cycle-form-error");
    if (!host) return;
    const list = (lines || []).filter(Boolean);
    if (!list.length) return clearCycleFormError();
    host.classList.remove("d-none");
    host.innerHTML = `<strong>Could not update Planning Cycle.</strong><ul class="mb-0">${list.map((x) => `<li>${x}</li>`).join("")}</ul>`;
  }
  function setSectionState(el, ok) {
    if (!el) return;
    el.className = `es-status-pill ${ok ? "is-ready" : "is-blocked"}`;
    el.textContent = ok ? "Complete" : "Blocked";
  }
  function renderReadinessList(el, items) {
    if (!el) return;
    const list = (items || []).filter(Boolean);
    if (!list.length) {
      el.innerHTML = '<li class="text-muted">None</li>';
      return;
    }
    el.innerHTML = list.map((x) => `<li>${x}</li>`).join("");
  }

  function normalizeCodePart(value) {
    return text(value).toUpperCase().replace(/[^A-Z0-9]+/g, "").slice(0, 6);
  }

  function autoPeriodCode() {
    const cyclePart = normalizeCodePart(cycle?.name || cycle?.code || "CYCLE");
    const namePart = normalizeCodePart(document.getElementById("cycle-period-name")?.value || "PERIOD");
    const stamp = Date.now().toString().slice(-6);
    return `SP-${cyclePart || "CYCLE"}-${namePart || "PERIOD"}-${stamp}`;
  }

  function cyclePayload() {
    return {
      name: text(document.getElementById("planning-cycle-name")?.value),
      code: text(document.getElementById("planning-cycle-code")?.value),
      planningCycleType: text(document.getElementById("planning-cycle-type")?.value),
      ownerCompanyId: text(document.getElementById("planning-cycle-owner-company")?.value),
      ownerPositionId: text(document.getElementById("planning-cycle-owner-position")?.value),
      currentOwnerPersonId: text(document.getElementById("planning-cycle-current-owner-person")?.value),
      ownerId: text(document.getElementById("planning-cycle-current-owner-person")?.value) || text(document.getElementById("planning-cycle-owner-position")?.value),
      description: text(document.getElementById("planning-cycle-description")?.value),
      effectiveFrom: text(document.getElementById("planning-cycle-effective-from")?.value),
      effectiveTo: text(document.getElementById("planning-cycle-effective-to")?.value),
      status: text(document.getElementById("planning-cycle-status")?.value) || cycle?.status || "Draft"
    };
  }

  function periodPayload() {
    const codeEl = document.getElementById("cycle-period-code");
    if (codeEl && !editingPeriodId && !text(codeEl.value)) {
      codeEl.value = autoPeriodCode();
    }
    return {
      planningCycleId: cycleId,
      name: text(document.getElementById("cycle-period-name")?.value),
      code: text(document.getElementById("cycle-period-code")?.value),
      companyId: text(document.getElementById("cycle-period-company")?.value),
      businessUnitId: text(document.getElementById("cycle-period-bu")?.value) || null,
      regionId: text(document.getElementById("cycle-period-region")?.value) || null,
      startDate: text(document.getElementById("cycle-period-start")?.value),
      endDate: text(document.getElementById("cycle-period-end")?.value),
      reviewCadence: text(document.getElementById("cycle-period-review")?.value),
      status: "Draft",
      ownerCompanyId: text(document.getElementById("cycle-period-owner-company")?.value),
      ownerPositionId: text(document.getElementById("cycle-period-owner-position")?.value),
      ownerEmployeeId: text(document.getElementById("cycle-period-current-owner-person")?.value),
      isDefaultForScope: Boolean(byId("cycle-period-default")?.checked),
      notes: text(document.getElementById("cycle-period-notes")?.value)
    };
  }

  function collectCycleEditReadiness() {
    const payload = cyclePayload();
    const missing = [];
    if (!payload.name) missing.push("Name");
    if (!payload.planningCycleType) missing.push("Planning Cycle Type");
    if (!payload.ownerPositionId) missing.push("Owner Position");
    if (!payload.effectiveFrom) missing.push("Effective From");
    if (!payload.effectiveTo) missing.push("Effective To");
    const blockers = [];
    if (payload.effectiveFrom && payload.effectiveTo && payload.effectiveTo < payload.effectiveFrom) {
      blockers.push("Effective To must be on or after Effective From.");
    }
    const earliestPeriodStart = periods
      .map((item) => isoDate(item?.startDate))
      .filter(Boolean)
      .sort()[0];
    const latestPeriodEnd = periods
      .map((item) => isoDate(item?.endDate))
      .filter(Boolean)
      .sort()
      .slice(-1)[0];
    if (payload.effectiveFrom && earliestPeriodStart && payload.effectiveFrom > earliestPeriodStart) {
      blockers.push(`Effective From cannot be moved later than ${earliestPeriodStart} while linked Strategy Periods exist.`);
    }
    if (payload.effectiveTo && latestPeriodEnd && payload.effectiveTo < latestPeriodEnd) {
      blockers.push(`Effective To cannot be moved earlier than ${latestPeriodEnd} while linked Strategy Periods exist.`);
    }
    const ready = missing.length === 0 && blockers.length === 0;
    return { payload, missing, blockers, ready };
  }

  function updateCycleEditReadiness() {
    // For offcanvas, we don't have the state pills anymore, but we can still show form errors on save.
    return collectCycleEditReadiness();
  }

  function collectAddPeriodReadiness() {
    const payload = periodPayload();
    const missing = [];
    if (!payload.name) missing.push("Name");
    if (!payload.ownerPositionId) missing.push("Owner Position");
    if (!payload.companyId) missing.push("Company");
    if (!payload.reviewCadence) missing.push("Review Cadence");
    if (!payload.startDate) missing.push("Start Date");
    if (!payload.endDate) missing.push("End Date");

    const blockers = [];
    if (payload.startDate && payload.endDate && payload.endDate < payload.startDate) {
      blockers.push("End Date must be on or after Start Date.");
    }
    const cycleFrom = isoDate(cycle?.effectiveFrom);
    const cycleTo = isoDate(cycle?.effectiveTo);
    if (payload.startDate && cycleFrom && payload.startDate < cycleFrom) {
      blockers.push("Start Date is before parent cycle Effective From.");
    }
    if (payload.endDate && cycleTo && payload.endDate > cycleTo) {
      blockers.push("End Date is after parent cycle Effective To.");
    }

    const identityReady = Boolean(payload.name && payload.ownerEmployeeId);
    const scopeReady = Boolean(payload.companyId);
    const timingReady = Boolean(payload.reviewCadence && payload.startDate && payload.endDate) && blockers.length === 0;
    const ready = missing.length === 0 && blockers.length === 0;
    return { payload, missing, blockers, identityReady, scopeReady, timingReady, ready };
  }

  function updateAddPeriodReadiness() {
    const snapshot = collectAddPeriodReadiness();
    
    // Enforce Date constraints based on parent cycle
    if (cycle) {
      const startEl = byId("cycle-period-start");
      const endEl = byId("cycle-period-end");
      const from = isoDate(cycle.effectiveFrom);
      const to = isoDate(cycle.effectiveTo);
      
      if (startEl && from && to) {
        startEl.setAttribute("min", from);
        startEl.setAttribute("max", to);
      }
      if (endEl && from && to) {
        endEl.setAttribute("min", from);
        endEl.setAttribute("max", to);
      }
    }

    // Readiness panel logic removed as it's no longer present in the UI
    return snapshot;
  }

  function getInitials(name) {
    if (!name) return "";
    const clean = text(name).split("—")[0].trim();
    return clean.split(" ").map((n) => n[0]).join("").toUpperCase().slice(0, 2);
  }

  function fmtNiceDate(v, includeTime = false) {
    if (!v) return "-";
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return "-";
    const opt = { day: "numeric", month: "short", year: "numeric" };
    if (includeTime) {
      opt.hour = "2-digit";
      opt.minute = "2-digit";
    }
    return d.toLocaleDateString("en-GB", opt).replace(",", "");
  }

  function renderCycle() {
    const badge = document.getElementById("planning-cycle-status-badge");
    const box = document.getElementById("planning-cycle-detail-metadata");
    if (!cycle) {
      if (badge) {
        badge.className = "badge bg-label-info";
        badge.textContent = "-";
      }
      if (box) box.textContent = "Planning cycle not found.";
      return;
    }

    const status = text(cycle.status).toLowerCase();
    if (badge) {
      const pill = statusPill(cycle.status);
      badge.className = pill.cls;
      badge.textContent = pill.label;
    }

    const titleEl = document.querySelector(".es-detail-title");
    if (titleEl && cycle.name) {
      titleEl.textContent = cycle.name;
    }

    const editBtn = document.getElementById("planning-cycle-detail-edit");
    const activateBtn = document.getElementById("planning-cycle-detail-activate");
    const archiveBtn = document.getElementById("planning-cycle-detail-archive");
    const divider = document.getElementById("planning-cycle-detail-divider");
    const actionsDropdown = document.querySelector(".es-action-cluster .btn-group");

    if (editBtn) editBtn.closest("li")?.classList.toggle("d-none", status !== "draft");
    if (activateBtn) activateBtn.closest("li")?.classList.toggle("d-none", status !== "draft");
    if (archiveBtn) archiveBtn.closest("li")?.classList.toggle("d-none", status === "archived");
    if (divider) divider.classList.toggle("d-none", status !== "draft");

    if (actionsDropdown) {
      const hasVisibleItem = status !== "archived";
      actionsDropdown.classList.toggle("d-none", !hasVisibleItem);
    }

    if (!box) return;

    const ownerName = ownerLabel(cycle.ownerId);
    const statusInfo = statusPill(cycle.status);
    const statusColorClass = statusInfo.cls.includes("success") ? "success" : (statusInfo.cls.includes("primary") ? "primary" : "secondary");

    box.innerHTML = `
      <div class="row row-cols-2 g-0 border-top">
        <div class="col p-3 border-bottom border-end">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Code</small>
          <div class="es-modern-meta-value">
            <span class="es-meta-code-pill">${cycle.code || "-"}</span>
          </div>
        </div>
        <div class="col p-3 border-bottom">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Planning Cycle Type</small>
          <div class="es-modern-meta-value">
            <span class="badge bg-label-primary text-uppercase">${cycle.planningCycleType || "-"}</span>
          </div>
        </div>

        <div class="col p-3 border-bottom border-end">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Effective From</small>
          <div class="es-modern-meta-value">${fmtNiceDate(cycle.effectiveFrom)}</div>
        </div>
        <div class="col p-3 border-bottom">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Effective To</small>
          <div class="es-modern-meta-value">${fmtNiceDate(cycle.effectiveTo)}</div>
        </div>

        <div class="col p-3 border-bottom border-end">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Owner</small>
          <div class="es-modern-meta-value d-flex align-items-center gap-2">
            <div class="es-avatar-initials">${getInitials(ownerName)}</div>
            <span class="fw-medium text-heading">${ownerName}</span>
          </div>
        </div>
        <div class="col p-3 border-bottom">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Status</small>
          <div class="es-modern-meta-value">
            <span class="badge bg-label-${statusColorClass} text-uppercase">
              <i class="bx bxs-circle me-1" style="font-size: 6px; vertical-align: middle;"></i> ${statusInfo.label}
            </span>
          </div>
        </div>

        <div class="col p-3 border-bottom border-end">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Updated On</small>
          <div class="es-modern-meta-value">${fmtNiceDate(cycle.updatedOn || cycle.updatedAt, true)}</div>
        </div>
        <div class="col p-3 border-bottom">
          <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Created By</small>
          <div class="es-modern-meta-value">${cycle.createdBy || "system-import"} / ${fmtNiceDate(cycle.createdOn || cycle.createdAt)}</div>
        </div>
      </div>

      <div class="p-4 pb-2">
        <small class="text-uppercase text-muted fw-bold mb-2 d-block" style="font-size: 0.65rem; letter-spacing: 0.05em;">Description</small>
        <div class="text-muted" style="font-size: 0.95rem; line-height: 1.6;">
          ${cycle.description || "No description provided."}
        </div>
      </div>
    `;
  }

  async function renderUsageSummary() {
    const periodIds = new Set((periods || []).map((x) => text(x.id)).filter(Boolean));
    let goalsCount = 0;
    let objectivesCount = 0;
    let initiativesCount = 0;

    if (periodIds.size) {
      try {
        const [goalsResp, objectivesResp, initiativesResp] = await Promise.all([
          goalsApi?.list?.("").catch(() => null),
          objectivesApi?.list?.("").catch(() => null),
          initiativesApi?.list?.("").catch(() => null)
        ]);
        const goals = Array.isArray(goalsResp?.items) ? goalsResp.items : [];
        const objectives = Array.isArray(objectivesResp?.items) ? objectivesResp.items : [];
        const initiatives = Array.isArray(initiativesResp?.items) ? initiativesResp.items : [];
        const scopedGoalIds = goals
          .filter((g) => periodIds.has(text(g?.strategyPeriodId)))
          .map((g) => text(g.id))
          .filter(Boolean);
        const scopedObjectiveIds = objectives
          .filter((o) => scopedGoalIds.includes(text(o.parentGoalId)))
          .map((o) => text(o.id))
          .filter(Boolean);
        goalsCount = scopedGoalIds.length;
        objectivesCount = scopedObjectiveIds.length;
        initiativesCount = initiatives.filter((item) => {
          const goalId = text(item?.parentGoalId);
          const objectiveId = text(item?.parentObjectiveId);
          return scopedGoalIds.includes(goalId) || scopedObjectiveIds.includes(objectiveId);
        }).length;
      } catch {
        // Keep fallback counts.
      }
    }

    usageSnapshot = {
      periods: periods.length,
      goals: goalsCount,
      objectives: objectivesCount,
      initiatives: initiativesCount
    };

    const wPeriods = byId("pc-widget-periods");
    const wGoals = byId("pc-widget-goals");
    const wObjectives = byId("pc-widget-objectives");
    const wInitiatives = byId("pc-widget-initiatives");

    if (wPeriods) wPeriods.textContent = periods.length;
    if (wGoals) wGoals.textContent = goalsCount;
    if (wObjectives) wObjectives.textContent = objectivesCount;
    if (wInitiatives) wInitiatives.textContent = initiativesCount;

    updateCycleActionState();
  }

  function updateCycleActionState() {
    const archiveBtn = byId("planning-cycle-detail-archive");
    if (!archiveBtn) return;
    const archiveBlocked = usageSnapshot.periods > 0 || usageSnapshot.goals > 0 || usageSnapshot.objectives > 0 || usageSnapshot.initiatives > 0;
    archiveBtn.disabled = archiveBlocked;
    archiveBtn.title = archiveBlocked
      ? `Cannot archive while linked to ${usageSnapshot.periods} strategy period(s), ${usageSnapshot.goals} goal(s), ${usageSnapshot.objectives} objective(s), and ${usageSnapshot.initiatives} initiative(s).`
      : "";
  }

  let dtPeriods = null;

  function updatePeriodSummaryChips() {
    const totalEl = byId("planning-cycle-linked-total");
    if (totalEl) totalEl.textContent = `${periods?.length || 0} total`;

    const counts = {
      Active: periods.filter((x) => isStatus(x, "Active")).length,
      Draft: periods.filter((x) => isStatus(x, "Draft")).length,
      Archived: periods.filter((x) => isStatus(x, "Archived")).length
    };

    Object.keys(counts).forEach((status) => {
      const chipId = `planning-cycle-linked-${status.toLowerCase()}`;
      const chipEl = byId(chipId);
      if (chipEl) {
        const span = chipEl.querySelector("span");
        if (span) span.textContent = `${counts[status]} ${status}`;
      }
    });
  }

  function fixDataTableLayout() {
    setTimeout(() => {
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
        document.querySelectorAll(selector).forEach((element) => {
          if (classToRemove) {
            classToRemove.split(" ").forEach((className) => element.classList.remove(className));
          }
          if (classToAdd) {
            classToAdd.split(" ").forEach((className) => element.classList.add(className));
          }
        });
      });

      // Mount filter panel
      const mountFilterPanel = () => {
          const host = document.getElementById('periodFilterCollapse');
          const filterBtn = document.querySelector('.dt-filter-btn');
          if (!host || !filterBtn) return;

          const toolbarRow = 
              filterBtn.closest('.dt-layout-row') || 
              filterBtn.closest('.row') || 
              filterBtn.closest('.dt-layout-end')?.parentElement;

          if (toolbarRow && host.previousElementSibling !== toolbarRow) {
              toolbarRow.insertAdjacentElement('afterend', host);
              host.classList.add('px-3');
          }
      };
      mountFilterPanel();

      // Group buttons
      const dtButtons = document.querySelector('.dt-buttons');
      if (dtButtons) {
          const eyeBtn = dtButtons.querySelector('.dt-eye-btn');
          const filterBtn = dtButtons.querySelector('.dt-filter-btn');
          if (eyeBtn && filterBtn && !eyeBtn.parentElement.classList.contains('btn-group')) {
              const group = document.createElement('div');
              group.className = 'btn-group';
              eyeBtn.parentNode.insertBefore(group, eyeBtn);
              group.appendChild(eyeBtn);
              group.appendChild(filterBtn);
              [eyeBtn, filterBtn].forEach(btn => {
                  btn.classList.remove('ms-3');
                  btn.style.margin = '0';
              });
          }
      }
    }, 100);
  }

  function updateFilterBadge() {
    const activeBadge = document.querySelector(".period-status-filter.active, .es-usage-chip.active");
    const count = activeBadge ? 1 : 0;
    const btn = document.querySelector(".dt-filter-btn");
    if (!btn) return;
    let badge = btn.querySelector(".badge");
    if (count > 0) {
      if (!badge) {
        badge = document.createElement("span");
        badge.className = "badge rounded-pill bg-primary badge-notifications";
        badge.style.cssText = "position:absolute;top:-5px;right:-5px;padding:0.2rem 0.4rem;font-size:0.65rem;border:2px solid white;";
        btn.appendChild(badge);
      }
      btn.style.position = "relative";
      badge.textContent = count;
    } else if (badge) {
      badge.remove();
    }
  }

  function wirePeriodFilters(dtApi) {
    const applyStatusFilter = (status, element) => {
        // Sync Visuals
        document.querySelectorAll(".period-status-filter, .es-usage-chip").forEach(el => {
            el.classList.remove("active", "border", "border-primary", "border-2", "shadow-sm");
        });

        if (status) {
            // Find all matching elements (both in chip row and filter panel)
            document.querySelectorAll(`[data-status="${status}"], .es-usage-chip`).forEach(el => {
                if (el.textContent.includes(status) || el.dataset.status === status) {
                    el.classList.add("active", "border", "border-primary", "border-2", "shadow-sm");
                }
            });
            dtApi.column(11).search(`^${status}$`, true, false).draw();
        } else {
            dtApi.column(11).search("").draw();
        }
        updateFilterBadge();
    };

    document.querySelectorAll(".period-status-filter").forEach(badge => {
        badge.addEventListener("click", function() {
            const status = this.classList.contains("active") ? "" : this.dataset.status;
            applyStatusFilter(status, this);
        });
    });

    document.querySelector(".btn-reset-period-filter")?.addEventListener("click", () => {
        applyStatusFilter("", null);
        bootstrap.Collapse.getInstance(document.getElementById("periodFilterCollapse"))?.hide();
    });

    // Strategy Page Summary Chip Filtering (Integrated)
    document.querySelectorAll(".es-usage-chip").forEach(chip => {
        chip.addEventListener("click", function() {
            const text = this.textContent.toLowerCase();
            let status = "";
            if (text.includes("active")) status = "Active";
            else if (text.includes("draft")) status = "Draft";
            else if (text.includes("archived")) status = "Archived";
            
            if (this.classList.contains("active") && status) {
                 applyStatusFilter("", null);
            } else {
                 applyStatusFilter(status, this);
            }
        });
    });
  }

  function renderPeriods() {
    const tableEl = document.querySelector(".strategy-periods-table");
    if (!tableEl) return;

    if (dtPeriods) {
      dtPeriods.clear().rows.add(periods).draw();
      updatePeriodSummaryChips();
      return;
    }

    dtPeriods = new DataTable(tableEl, {
      data: periods,
      responsive: {
        details: {
          display: DataTable.Responsive.display.modal({
            header: (row) => `<h5 class="modal-title">Period Details - ${row.data().name || ""}</h5>`
          }),
          type: "column",
          renderer: function (apiInstance, rowIdx, columns) {
            const data = $.map(columns, function (col) {
              return col.hidden && col.columnIndex !== 1 && col.columnIndex !== 12
                ? `<tr data-dt-row="${col.rowIndex}" data-dt-column="${col.columnIndex}"><td>${col.title}:</td><td>${col.data}</td></tr>`
                : "";
            }).join("");
            return data ? $('<table class="table"/>').append(data) : false;
          }
        }
      },
      columns: [
        { data: null, defaultContent: "" },
        { data: null, defaultContent: "", visible: false },
        { data: "name" },
        { data: "ownerEmployeeId" },
        { data: "planningCycleName" },
        { data: "companyId" },
        { data: "businessUnitId" },
        { data: "regionId" },
        { data: "startDate" },
        { data: "reviewCadence" },
        { data: "id" },
        { data: "status" },
        { data: null, className: "text-end" }
      ],
      columnDefs: [
        { className: "control", orderable: false, targets: 0 },
        {
          targets: 1,
          orderable: false,
          checkboxes: { selectAllRender: '<input type="checkbox" class="form-check-input">' },
          render: () => '<input type="checkbox" class="dt-checkboxes form-check-input">'
        },
        {
          targets: 2,
          responsivePriority: 1,
          render: (data, type, full) => `
            <div class="d-flex flex-column">
              <span class="text-heading fw-medium text-truncate">${full.name || "-"}</span>
              <small class="text-muted text-uppercase" style="font-size: 0.65rem;">${full.code || ""}</small>
            </div>`
        },
        {
          targets: 3,
          render: (data, type, full) => {
            const name = ownerLabel(full.currentOwnerPersonId || full.ownerEmployeeId);
            const initials = getInitials(name);
            return `
              <div class="d-flex justify-content-start align-items-center">
                <div class="avatar-wrapper">
                  <div class="avatar avatar-sm me-4">
                    <span class="avatar-initial rounded-circle bg-label-primary">${initials}</span>
                  </div>
                </div>
                <div class="d-flex flex-column">
                  <span class="text-heading text-truncate fw-medium">${name}</span>
                </div>
              </div>`;
          }
        },
        {
          targets: 4,
          render: (data, type, full) => `<span class="text-heading text-truncate fw-medium">${cycleLabel(full)}</span>`
        },
        {
          targets: 5,
          render: (data, type, full) => {
            return companyLabel(full.companyId);
          }
        },
        { targets: 6, render: (data) => data || "-" },
        { targets: 7, render: (data) => data || "-" },
        {
          targets: 8,
          render: (data, type, full) => timelineMarkup(full.startDate, full.endDate)
        },
        { targets: 9, render: (data) => data || "-" },
        {
          targets: 10,
          render: (data, type, full) => usageBadge(full)
        },
        {
          targets: 11,
          render: (data) => {
            const pill = statusPill(data);
            return `<span class="${pill.cls}">${pill.label}</span>`;
          }
        },
        {
          targets: -1,
          responsivePriority: 1,
          className: "all text-end",
          render: (data, type, full) => {
            const isArchived = text(full.status).toLowerCase() === "archived";
            const cycleArchived = text(cycle?.status).toLowerCase() === "archived";
            
            return `
            <div class="d-flex align-items-center justify-content-end">
              <a href="/management-governance/enterprise-strategy-business-performance/planning/strategy-periods/${encodeURIComponent(full.id)}" class="btn btn-icon">
                <i class="icon-base bx bx-show icon-md"></i>
              </a>
              ${!isArchived && !cycleArchived ? `
                <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                  <i class="icon-base bx bx-dots-vertical-rounded icon-md"></i>
                </a>
                <div class="dropdown-menu dropdown-menu-end m-0">
                  ${text(full.status).toLowerCase() === "draft"
                    ? `<a href="javascript:;" class="dropdown-item" data-action="activate" data-id="${encodeURIComponent(full.id)}">Activate</a>`
                    : ""}
                  <a href="javascript:;" class="dropdown-item" data-action="edit" data-id="${encodeURIComponent(full.id)}">Edit</a>
                  <a href="javascript:;" class="dropdown-item text-danger">Delete</a>
                </div>
              ` : ""}
            </div>`;
          }
        }
      ],
      order: [[2, "asc"]],
      layout: {
        topStart: {
          rowClass: "row m-3 justify-content-between pb-0",
          features: [{ pageLength: { menu: [5, 10, 25], text: "_MENU_" } }]
        },
        topEnd: {
          rowClass: "row mx-3 justify-content-between pb-0",
          features: [
            { search: { placeholder: "Search Period", text: "_INPUT_" } },
            {
              buttons: [
                {
                  text: '<i class="icon-base bx bx-show icon-sm"></i>',
                  className: 'btn btn-icon btn-label-secondary dt-eye-btn',
                  action: () => {}
                },
                {
                  text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                  className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                  action: function () { 
                      const filterEl = document.getElementById('periodFilterCollapse');
                      if (filterEl) {
                          bootstrap.Collapse.getOrCreateInstance(filterEl).toggle();
                          this.node().classList.toggle('active');
                      }
                  }
                },
                {
                  text: '<i class="icon-base bx bx-plus icon-sm me-sm-2"></i>Add Strategy Period',
                  className: "btn btn-primary ms-3",
                  action: () => {
                    resetAddPeriodForm();
                    strategyPeriodOffcanvas.show();
                  }
                }
              ]
            }
          ]
        },
        bottomStart: {
          rowClass: "row mx-3 justify-content-between",
          features: ["info"]
        },
        bottomEnd: {
          paging: { firstLast: false }
        }
      },
      language: {
        paginate: {
          next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
          previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
        }
      },
      initComplete: function () {
        wirePeriodFilters(this.api());
        fixDataTableLayout();
      },
      drawCallback: function () {
        updatePeriodSummaryChips();
        fixDataTableLayout();
      }
    });
    tableEl.addEventListener("click", async (event) => {
      const actionEl = event.target.closest("[data-action]");
      if (!actionEl) return;
      event.preventDefault();
      try {
        const periodId = decodeURIComponent(text(actionEl.dataset.id));
        const action = text(actionEl.dataset.action).toLowerCase();
        if (action === "activate") {
          await api.activatePeriod(periodId);
          notify("Activated.");
          await load();
          return;
        }
        if (action === "edit") {
          const period = await api.getStrategyPeriod(periodId);
          await fillPeriodEditForm(period);
          strategyPeriodOffcanvas.show();
        }
      } catch (err) {
        showError(err, actionEl.dataset.action === "activate" ? "Could not activate strategy period." : "Could not load strategy period.");
      }
    });
    tableEl.addEventListener("search.dt", () => fixDataTableLayout());
    window.addEventListener("resize", () => fixDataTableLayout());
  }

  async function fillEditForm() {
    byId("planning-cycle-name").value = cycle?.name || "";
    byId("planning-cycle-code").value = cycle?.code || "";
    byId("planning-cycle-type").value = cycle?.planningCycleType || "";
    setStatus(cycle?.status || "Draft");
    byId("planning-cycle-owner-company").value = cycle?.ownerCompanyId || "";
    await refreshOwnerPositions();
    byId("planning-cycle-owner-position").value = cycle?.ownerPositionId || "";
    syncCurrentOwnerPerson();
    if (cycle?.currentOwnerPersonId) {
       $("#planning-cycle-current-owner-person").val(String(cycle.currentOwnerPersonId)).trigger("change");
    }
    byId("planning-cycle-description").value = cycle?.description || "";
    byId("planning-cycle-effective-from").value = isoDate(cycle?.effectiveFrom);
    byId("planning-cycle-effective-to").value = isoDate(cycle?.effectiveTo);
    clearCycleFormError();
  }

  function resetAddPeriodForm() {
    editingPeriodId = "";
    if (byId("strategyPeriodOffcanvasLabel")) byId("strategyPeriodOffcanvasLabel").textContent = "Create Strategy Period";
    if (byId("strategyPeriodOffcanvasSubtitle")) byId("strategyPeriodOffcanvasSubtitle").textContent = "Add a strategy period under this cycle with scope and review governance controls.";
    if (byId("cycle-period-save")) byId("cycle-period-save").textContent = "Submit";
    ["cycle-period-name", "cycle-period-code", "cycle-period-owner-position", "cycle-period-current-owner-person", "cycle-period-bu", "cycle-period-region", "cycle-period-review", "cycle-period-start", "cycle-period-end", "cycle-period-notes"]
      .forEach((id) => { const el = byId(id); if (el) el.value = ""; });
    
    // Select2 reset
    $("#cycle-period-owner-position, #cycle-period-current-owner-person, #cycle-period-bu, #cycle-period-region, #cycle-period-review").val("").trigger("change");

    if (byId("cycle-period-default")) byId("cycle-period-default").checked = false;
    if (byId("cycle-period-code")) byId("cycle-period-code").value = autoPeriodCode();
    
    // Pre-select current cycle and disable it
    const $cycleSelect = $("#cycle-period-cycle");
    if ($cycleSelect.length) {
      $cycleSelect.val(cycleId).trigger("change.select2").attr("disabled", true);
    }

    // Lock company fields to cycle company
    if (cycle?.ownerCompanyId) {
       $("#cycle-period-company").val(cycle.ownerCompanyId).trigger("change").prop("disabled", true);
       $("#cycle-period-owner-company").val(cycle.ownerCompanyId).trigger("change").prop("disabled", true);
    } else {
       $("#cycle-period-company, #cycle-period-owner-company").val("").trigger("change").prop("disabled", true);
    }

    clearPeriodFormError();
    updateAddPeriodReadiness();
  }

  async function fillPeriodEditForm(period) {
    if (!period) return;
    editingPeriodId = text(period.id);
    if (byId("strategyPeriodOffcanvasLabel")) byId("strategyPeriodOffcanvasLabel").textContent = "Edit Strategy Period";
    if (byId("strategyPeriodOffcanvasSubtitle")) byId("strategyPeriodOffcanvasSubtitle").textContent = "Update the selected strategy period within this planning cycle.";
    if (byId("cycle-period-save")) byId("cycle-period-save").textContent = "Save Changes";

    const ownerPersonId = text(period.currentOwnerPersonId || period.ownerEmployeeId);
    $("#cycle-period-cycle").val(text(period.planningCycleId || cycleId)).trigger("change.select2").attr("disabled", true);
    byId("cycle-period-name").value = text(period.name);
    byId("cycle-period-code").value = text(period.code);
    $("#cycle-period-owner-company").val(text(period.ownerCompanyId)).trigger("change");
    await refreshPeriodOwnerPositions();
    $("#cycle-period-owner-position").val(text(period.ownerPositionId)).trigger("change");
    $("#cycle-period-current-owner-person").val(ownerPersonId).trigger("change.select2");
    $("#cycle-period-company").val(text(period.companyId)).trigger("change").prop("disabled", true);
    $("#cycle-period-owner-company").prop("disabled", true).trigger("change");
    $("#cycle-period-bu").val(text(period.businessUnitId)).trigger("change");
    $("#cycle-period-region").val(text(period.regionId)).trigger("change");
    $("#cycle-period-review").val(text(period.reviewCadence)).trigger("change");
    byId("cycle-period-default").checked = Boolean(period.isDefaultForScope);
    byId("cycle-period-start").value = isoDate(period.startDate);
    byId("cycle-period-end").value = isoDate(period.endDate);
    byId("cycle-period-notes").value = text(period.notes);
    clearPeriodFormError();
    clearPeriodValidation();
    updateAddPeriodReadiness();
  }

  console.log("DEBUG: planning-cycle-detail.js IIFE starting");

  async function load() {
    console.log("DEBUG: load() started", { cycleId });
    console.log("DEBUG: about to call Promise.all");
    const [lookupResult, c, list, userResult, positionResult, cp, allCycles] = await Promise.all([
      (window.strategyEnterpriseMetaApi?.lookups?.() || Promise.resolve({})).catch(e => { console.error("Lookups failed", e); return {}; }),
      api.getCycle(cycleId),
      api.listStrategyPeriods(cycleId).catch(e => { console.error("ListPeriods failed", e); return []; }),
      (window.strategyEnterpriseMetaApi?.getUsersByTenantId?.() || Promise.resolve({ data: [] })).catch(e => { console.error("Users failed", e); return { data: [] }; }),
      api.getAllPositions().catch(e => { 
        console.error("CRITICAL: Global Positions API (GetAllPosition) failed. This is likely due to Mixed Content (HTTP API on HTTPS site).", e); 
        return []; 
      }),
      (window.strategyCompaniesApi?.list?.() || Promise.resolve({ items: [] })).catch(e => { console.error("Companies failed", e); return { items: [] }; }),
      api.listCycles().catch(e => { console.error("ListCycles failed", e); return []; })
    ]);
    console.log("DEBUG: Promise.all finished", { c, allCyclesCount: allCycles?.length });

    if (workbook.ensureUsersLoaded) await workbook.ensureUsersLoaded().catch(() => {});
    if (workbook.ensurePositionsLoaded) await workbook.ensurePositionsLoaded().catch(() => {});
    if (workbook.ensureCompaniesLoaded) await workbook.ensureCompaniesLoaded().catch(() => {});

      lookups = lookupResult || {};
      cycle = c || null;
      cycles = Array.isArray(allCycles) ? allCycles : [];
      periods = Array.isArray(list) ? list : [];
      const companies = Array.isArray(cp?.items) ? cp.items : [];
      
      // Sync fetched companies with global workbook state
      if (companies.length && workbook.syncWorkbookCompanies) {
          workbook.syncWorkbookCompanies(companies);
      }
      
      companyOptions = companies.map(x => ({ value: x.id, label: x.companyName }));
      const sortOptShared = (arr) => [...(arr || [])].sort((a, b) => (a.label || "").localeCompare(b.label || ""));

      // Enhanced lookups for Offcanvas
      lookups.positions = Array.isArray(positionResult)
        ? positionResult.map(p => ({ 
            value: p.PositionId, 
            label: p.PositionName, 
            text: p.PositionName,
            CompanyId: p.CompanyId || p.companyId 
          }))
        : [];
      lookups.positions.sort((a, b) => (a.label || "").localeCompare(b.label || ""));
      globalPositions = lookups.positions; // Critical fix: Ensure refresh functions have data

      let rawUsers = Array.isArray(userResult) ? userResult : (userResult?.data || []);
      lookups.users = rawUsers.map(u => ({ value: u.id, label: u.fullName, text: u.fullName }));
      lookups.users.sort((a, b) => (a.label || "").localeCompare(b.label || ""));

      ownerRefs = ownerReferencesFromSources();
      
      workbook.fillSelect?.(byId("planning-cycle-type"), lookups.planningCycleTypes || [], { placeholder: "Select Type", keepCurrent: true });
      workbook.fillSelect?.(byId("planning-cycle-owner-company"), sortOptShared(companyOptions), { placeholder: "Select Company", keepCurrent: true });
      workbook.fillSelect?.(byId("planning-cycle-owner-position"), lookups.positions || [], { placeholder: "Select Position", keepCurrent: true });
      
      const $personSelect = $("#planning-cycle-current-owner-person");
      if ($personSelect.length && $.fn.select2) {
          $personSelect.empty().append("<option></option>");
          lookups.users.forEach(u => $personSelect.append(new Option(u.label, u.value)));
          if ($personSelect.hasClass("select2-hidden-accessible")) $personSelect.select2("destroy");
          $personSelect.select2({ dropdownParent: $("#planningCycleEditorOffcanvas"), placeholder: "Select Person", allowClear: true, width: "100%" });
      }

      workbook.fillSelect?.(document.getElementById("cycle-period-review"), sortOptShared(lookups.reviewCadences), { placeholder: "Select", keepCurrent: true });
      workbook.fillSelect?.(document.getElementById("cycle-period-cycle"), cycles.map(x => ({ value: x.id, label: `${x.code} - ${x.name}` })), { placeholder: "Select Cycle", keepCurrent: true });
      
      // Period Offcanvas Selects
      workbook.fillSelect?.(document.getElementById("cycle-period-company"), sortOptShared(companyOptions), { placeholder: "Select company", keepCurrent: true });
      workbook.fillSelect?.(document.getElementById("cycle-period-owner-company"), sortOptShared(companyOptions), { placeholder: "Select company", keepCurrent: true });
      workbook.fillSelect?.(document.getElementById("cycle-period-bu"), sortOptShared(lookups.businessUnits), { placeholder: "Select business unit", keepCurrent: true });
      workbook.fillSelect?.(document.getElementById("cycle-period-region"), sortOptShared(lookups.regions), { placeholder: "Select region", keepCurrent: true });
   

      const $periodOwnerPosSelect = $("#cycle-period-owner-position");
      if ($periodOwnerPosSelect.length && $.fn.select2) {
          if ($periodOwnerPosSelect.hasClass("select2-hidden-accessible")) $periodOwnerPosSelect.select2("destroy");
          $periodOwnerPosSelect.select2({ 
              dropdownParent: $("#strategyPeriodOffcanvas"), 
              placeholder: "Select owner position", 
              allowClear: true, 
              width: "100%" 
          });
      }

      const $periodOwnerSelect = $("#cycle-period-owner");
      if ($periodOwnerSelect.length && $.fn.select2) {
          const ownerOpts = ownerRefs
              .map((x) => ({ id: x.ownerId, text: x.displayName }))
              .sort((a, b) => a.text.localeCompare(b.text));
          $periodOwnerSelect.empty().append("<option></option>");
          ownerOpts.forEach(o => $periodOwnerSelect.append(new Option(o.text, o.id)));
          if ($periodOwnerSelect.hasClass("select2-hidden-accessible")) $periodOwnerSelect.select2("destroy");
          $periodOwnerSelect.select2({ 
              dropdownParent: $("#strategyPeriodOffcanvas"), 
              placeholder: "Select owner", 
              allowClear: true, 
              width: "100%" 
          });
      }

      // Ownership sync and position refresh
      syncPeriodOwnerCompany();
      
      const $personDropdown = $("#cycle-period-current-owner-person");
      if ($personDropdown.length && $.fn.select2) {
          $personDropdown.empty().append("<option></option>");
          lookups.users.forEach(u => $personDropdown.append(new Option(u.label, u.value)));
          if ($personDropdown.hasClass("select2-hidden-accessible")) $personDropdown.select2("destroy");
          $personDropdown.select2({ dropdownParent: $("#strategyPeriodOffcanvas"), placeholder: "Select Person", allowClear: true, width: "100%" });
      }
      
      renderCycle();
      await renderPeriods();
      await renderUsageSummary();
      updateAddPeriodReadiness();

      // Restriction: Disable add button if cycle is archived
      const isArchived = text(cycle?.status).toLowerCase() === "archived";
      const addBtn = document.querySelector(".dt-buttons .btn-primary");
      if (addBtn) {
          if (isArchived) {
              addBtn.classList.add("disabled");
              addBtn.setAttribute("disabled", "disabled");
              addBtn.style.opacity = "0.6";
              addBtn.title = "Cannot add periods to an archived planning cycle.";
          } else {
              addBtn.classList.remove("disabled");
              addBtn.removeAttribute("disabled");
              addBtn.style.opacity = "1";
              addBtn.title = "";
          }
      }
  }

  async function saveCycle() {
    try {
      clearCycleFormError();
      const snapshot = collectCycleEditReadiness();
      if (!snapshot.ready) {
        showCycleFormError([...snapshot.missing.map((x) => `${x} is required.`), ...snapshot.blockers]);
        return;
      }
      await api.updateCycle(cycleId, snapshot.payload);
      offcanvas.hide();
      notify("Planning cycle updated.");
      await load();
    } catch (err) {
      showCycleFormError(["Could not update planning cycle."]);
      showError(err, "Could not update planning cycle.");
    }
  }

  function clearPeriodValidation() {
    document.querySelectorAll("#strategyPeriodOffcanvas .is-invalid").forEach(el => el.classList.remove("is-invalid"));
  }

  function applyPeriodValidation(snapshot) {
    clearPeriodValidation();
    const mappings = {
      "Name": "cycle-period-name",
      "Owner Position": "cycle-period-owner-position",
      "Company": "cycle-period-company",
      "Review Cadence": "cycle-period-review",
      "Start Date": "cycle-period-start",
      "End Date": "cycle-period-end"
    };
    snapshot.missing.forEach(field => {
       const id = mappings[field];
       if (id) byId(id)?.classList.add("is-invalid");
    });
  }

  async function savePeriod() {
    try {
      clearPeriodFormError();
      clearPeriodValidation();
      const snapshot = collectAddPeriodReadiness();
      if (!snapshot.ready) {
        applyPeriodValidation(snapshot);
        showPeriodFormError([...snapshot.missing.map((x) => `${x} is required.`), ...snapshot.blockers]);
        return;
      }
      console.log("DEBUG: period payload collected", snapshot.payload);
      if (editingPeriodId) {
        await api.updateStrategyPeriod(editingPeriodId, snapshot.payload);
        notify("Strategy period updated.");
      } else {
        await api.createStrategyPeriod(snapshot.payload);
        notify("Strategy period created.");
      }
      strategyPeriodOffcanvas.hide();
      await load();
    } catch (err) {
      showError(err, editingPeriodId ? "Could not update strategy period." : "Could not create strategy period.");
    }
  }

  document.getElementById("planning-cycle-detail-edit")?.addEventListener("click", () => {
    fillEditForm().then(() => offcanvas.show());
  });
  document.getElementById("planning-cycle-save")?.addEventListener("click", saveCycle);
  document.querySelector('.add-new-cycle [data-bs-dismiss="offcanvas"]')?.addEventListener("click", () => offcanvas?.hide());
  document.getElementById("cycle-period-save")?.addEventListener("click", savePeriod);
  
  byId("planning-cycle-owner-company")?.addEventListener("change", refreshOwnerPositions);
  byId("planning-cycle-owner-position")?.addEventListener("change", syncCurrentOwnerPerson);
  
  byId("cycle-period-owner-company")?.addEventListener("change", refreshPeriodOwnerPositions);
  byId("cycle-period-owner-position")?.addEventListener("change", syncPeriodCurrentOwnerPerson);
  byId("cycle-period-company")?.addEventListener("change", syncPeriodOwnerCompany);

  // Event-driven refresh for Offcanvas
  strategyPeriodOffcanvasEl?.addEventListener("shown.bs.offcanvas", () => {
    refreshPeriodOwnerPositions();
  });
  strategyPeriodOffcanvasEl?.addEventListener("hidden.bs.offcanvas", resetAddPeriodForm);

  
  document.querySelectorAll("#planning-cycle-status-container .status-pill").forEach(pill => {
      pill.addEventListener("click", () => setStatus(pill.dataset.status));
  });

  ["cycle-period-name", "cycle-period-owner-company", "cycle-period-owner-position", "cycle-period-company", "cycle-period-bu", "cycle-period-region", "cycle-period-review", "cycle-period-start", "cycle-period-end", "cycle-period-default"]
    .forEach((id) => {
        const el = byId(id);
        if (el) {
            el.addEventListener("change", updateAddPeriodReadiness);
            el.addEventListener("change", function() { this.classList.remove("is-invalid"); });
            el.addEventListener("input", function() { this.classList.remove("is-invalid"); });
        }
    });

  byId("cycle-period-notes")?.addEventListener("input", updateAddPeriodReadiness);
  byId("cycle-period-name")?.addEventListener("input", () => {
    if (!editingPeriodId) {
      const codeEl = byId("cycle-period-code");
      if (codeEl) codeEl.value = autoPeriodCode();
    }
    updateAddPeriodReadiness();
  });

  load();
})(window, document);
