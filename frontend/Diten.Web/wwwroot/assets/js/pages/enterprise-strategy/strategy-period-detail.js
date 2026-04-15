(function (window, document) {
  "use strict";

  const periodId = String(window.strategyPeriodDetailId || "").trim();
  const api = window.strategyPlanningApi;
  const goalsApi = window.strategyGoalsApi;
  const objectivesApi = window.strategyObjectivesApi;
  const ui = window.enterpriseStrategyUi || {};
  const workbook = window.enterpriseWorkbookOptions || {};
  
  const offcanvasEl = document.getElementById("strategyPeriodOffcanvas");
  const offcanvas = offcanvasEl ? new bootstrap.Offcanvas(offcanvasEl) : null;

  const readinessIndicatorEl = document.getElementById("cycle-period-readiness-indicator");
  const readinessTextEl = document.getElementById("cycle-period-readiness-text");
  const readinessMissingEl = document.getElementById("cycle-period-readiness-missing");
  const readinessBlockersEl = document.getElementById("cycle-period-readiness-blockers");
  
  const identityStateEl = document.getElementById("cycle-period-sec-identity-state");
  const scopeStateEl = document.getElementById("cycle-period-sec-scope-state");
  const timingStateEl = document.getElementById("cycle-period-sec-timing-state");

  let period = null;
  let cycles = [];
  let lookups = {};
  let usageSnapshot = { goalCount: 0, objectiveCount: 0, isInUse: false, goals: [] };
  let companyOptions = [];

  function text(v) { return String(v || "").trim(); }
  function isoDate(v) { return text(v).slice(0, 10); }
  function parseDateValue(raw) {
    const s = text(raw);
    if (!s) return null;
    const iso = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (iso) {
      const y = Number(iso[1]);
      const m = Number(iso[2]);
      const d = Number(iso[3]);
      return new Date(y, m - 1, d);
    }
    return new Date(s);
  }

  function notify(message, kind) { ui.notify?.(message, kind || "success"); }
  function showError(err, fallback) { notify(ui.getErrorMessage?.(err, fallback) || fallback, "danger"); }
  function byId(id) { return document.getElementById(id); }

  function statusPill(status) {
    const normalized = text(status).toLowerCase();
    if (normalized === "active") return { cls: "es-status-pill is-active", label: "Active" };
    if (normalized === "draft") return { cls: "es-status-pill is-draft", label: "Draft" };
    if (normalized === "archived") return { cls: "es-status-pill is-archived", label: "Archived" };
    return { cls: "es-status-pill is-info", label: text(status) || "-" };
  }

  function cycleById(id) {
    const key = text(id).toLowerCase();
    return cycles.find((x) => text(x.id).toLowerCase() === key) || null;
  }

  function companyLabel(id) {
    const key = text(id).toLowerCase();
    if (!key) return "-";
    // Check local options first
    let hit = companyOptions.find((x) => text(x.value).toLowerCase() === key);
    if (hit) return hit.label;
    // Fallback to workbook global options
    const options = workbook.companyOptions?.() || [];
    hit = options.find((x) => text(x.value).toLowerCase() === key);
    return hit?.label || id || "-";
  }

  function ownerLabel(id) {
    const key = text(id).toLowerCase();
    if (!key) return "-";
    return workbook.userDisplayName?.(id) || id;
  }

  function setSectionState(el, ok) {
    if (!el) return;
    el.className = `es-status-pill ${ok ? "is-ready" : "is-blocked"}`;
    el.textContent = ok ? "Complete" : "Blocked";
  }

  function renderReadinessList(el, items) {
    if (!el) return;
    const list = (items || []).filter(Boolean);
    el.innerHTML = list.length ? list.map((x) => `<li>${x}</li>`).join("") : '<li class="text-muted">None</li>';
  }

  function clearFormError() {
    const host = byId("cycle-period-form-error");
    if (host) { host.classList.add("d-none"); host.innerHTML = ""; }
  }

  function showFormError(messages) {
    const host = byId("cycle-period-form-error");
    if (!host) return;
    const list = (messages || []).filter(Boolean);
    if (!list.length) return clearFormError();
    host.classList.remove("d-none");
    host.innerHTML = `<strong>Could not save Strategy Period.</strong><ul class="mb-0">${list.map((x) => `<li>${x}</li>`).join("")}</ul>`;
  }

  function collectReadiness() {
    const p = getPayload();
    const missing = [];
    if (!p.planningCycleId) missing.push("Parent Planning Cycle");
    if (!p.name) missing.push("Name");
    if (!p.ownerCompanyId) missing.push("Owner Company");
    if (!p.ownerPositionId) missing.push("Owner Position");
    if (!p.companyId) missing.push("Company");
    if (!p.reviewCadence) missing.push("Review Cadence");
    if (!p.startDate) missing.push("Start Date");
    if (!p.endDate) missing.push("End Date");

    const blockers = [];
    const startDt = parseDateValue(p.startDate);
    const endDt = parseDateValue(p.endDate);
    if (startDt && endDt && endDt.getTime() < startDt.getTime()) {
      blockers.push("End Date must be on or after Start Date.");
    }
    const parent = cycleById(p.planningCycleId);
    const cycleFrom = parseDateValue(parent?.effectiveFrom);
    const cycleTo = parseDateValue(parent?.effectiveTo);
    if (startDt && cycleFrom && startDt.getTime() < cycleFrom.getTime()) {
      blockers.push("Start Date is before parent planning cycle Effective From.");
    }
    if (endDt && cycleTo && endDt.getTime() > cycleTo.getTime()) {
      blockers.push("End Date is after parent planning cycle Effective To.");
    }

    const identityReady = Boolean(p.planningCycleId && p.name && p.ownerCompanyId && p.ownerPositionId);
    const scopeReady = Boolean(p.companyId);
    const timingReady = Boolean(p.reviewCadence && p.startDate && p.endDate) && blockers.length === 0;
    return { p, missing, blockers, identityReady, scopeReady, timingReady, ready: missing.length === 0 && blockers.length === 0 };
  }

  function applyFieldLocking() {
    const isActive = text(period?.status).toLowerCase() === "active";
    const isInUse = usageSnapshot.isInUse || (usageSnapshot.goalCount > 0 || usageSnapshot.objectiveCount > 0);
    const lockStructural = isActive && isInUse;

    // Structural Fields
    const structuralFieldIds = [
      "cycle-period-cycle",
      "cycle-period-company",
      "cycle-period-bu",
      "cycle-period-region",
      "cycle-period-start",
      "cycle-period-end"
    ];

    structuralFieldIds.forEach((fieldId) => {
      const el = byId(fieldId);
      if (!el) return;
      const $el = $(el);
      if (lockStructural) {
        $el.prop("disabled", true).addClass("bg-light").removeClass("border-warning");
        el.title = `Bu alan, ${usageSnapshot.goalCount} goal ve ${usageSnapshot.objectiveCount} objective bağlı olduğu için kilitlidir.`;
      } else if (isActive) {
        $el.prop("disabled", false).removeClass("bg-light").addClass("border-warning");
        el.title = "Bu alan aktif durumda. Değişiklik gelecekteki atamaları etkileyebilir.";
      } else {
        $el.prop("disabled", false).removeClass("bg-light border-warning");
        el.title = "";
      }
      if ($el.hasClass("select2-hidden-accessible")) $el.trigger("change.select2");
    });

    // Behavioral Fields
    ["cycle-period-review", "cycle-period-default"].forEach((fieldId) => {
      const el = byId(fieldId);
      if (!el) return;
      const $el = $(el);
      if (isActive && isInUse) $el.addClass("border-warning");
      else $el.removeClass("border-warning");
    });

    // Banner Sync
    const banner = byId("cycle-period-in-use-banner");
    if (banner) {
      banner.classList.toggle("d-none", !lockStructural);
      if (lockStructural) {
        const gc = byId("in-use-goal-count");
        const oc = byId("in-use-obj-count");
        if (gc) gc.textContent = usageSnapshot.goalCount;
        if (oc) oc.textContent = usageSnapshot.objectiveCount;
      }
    }
  }

  function syncDetailActionButtons() {
    const isActive = text(period?.status).toLowerCase() === "active";
    const isDraft = text(period?.status).toLowerCase() === "draft";
    const isArchived = text(period?.status).toLowerCase() === "archived";
    const isInUse = usageSnapshot.isInUse || (usageSnapshot.goalCount > 0 || usageSnapshot.objectiveCount > 0);

    const editBtn = byId("strategy-period-detail-edit");
    const activateBtn = byId("strategy-period-detail-activate");
    const archiveBtn = byId("strategy-period-detail-archive");
    const cluster = document.querySelector(".es-action-cluster");

    if (isArchived && cluster) {
        cluster.classList.add("d-none");
        return;
    }
    if (cluster) cluster.classList.remove("d-none");

    if (editBtn) editBtn.closest("li").classList.toggle("d-none", isArchived);
    if (activateBtn) activateBtn.closest("li").classList.toggle("d-none", !isDraft);
    if (archiveBtn) {
        archiveBtn.closest("li").classList.toggle("d-none", isArchived);
        if (isInUse) {
            archiveBtn.classList.add("disabled", "text-muted");
            archiveBtn.classList.remove("text-danger");
            archiveBtn.title = `Bağlı ${usageSnapshot.goalCount} goal/objective olduğu için arşivlenemez.`;
        } else {
            archiveBtn.classList.remove("disabled", "text-muted");
            archiveBtn.classList.add("text-danger");
            archiveBtn.title = "";
        }
    }
  }

  function updateReadiness() {
    const snapshot = collectReadiness();
    if (readinessIndicatorEl) {
      readinessIndicatorEl.className = `es-status-pill ${snapshot.ready ? "is-ready" : "is-blocked"}`;
      readinessIndicatorEl.textContent = snapshot.ready ? "Readiness: Ready" : "Readiness: Blocked";
    }
    if (readinessTextEl) readinessTextEl.textContent = snapshot.ready ? "Form is complete." : "Resolve required fields.";
    renderReadinessList(readinessMissingEl, snapshot.missing);
    renderReadinessList(readinessBlockersEl, snapshot.blockers);
    setSectionState(identityStateEl, snapshot.identityReady);
    setSectionState(scopeStateEl, snapshot.scopeReady);
    setSectionState(timingStateEl, snapshot.timingReady);
    return snapshot;
  }

  async function refreshOwnerPositions() {
    const positionEl = byId("cycle-period-owner-position");
    if (!positionEl) return;
    const $pos = $(positionEl);
    const options = lookups.positions || [];
    workbook.fillSelect?.(positionEl, options, { placeholder: options.length ? "Select position" : "No positions available" });
    
    if ($pos.length && $.fn.select2) {
        $pos.prop("disabled", false).trigger("change"); // Force enabled
    } else {
        $pos.removeAttr("disabled");
    }
  }

  async function syncCurrentOwnerPerson() {
    const posId = byId("cycle-period-owner-position")?.value;
    const $person = $("#cycle-period-current-owner-person");
    if (!posId) {
      $person.empty().append('<option value="">Select position first</option>').trigger("change.select2");
      return;
    }
    try {
      const response = await window.strategyEnterpriseMetaApi?.getUsersByTenantId?.();
      const users = Array.isArray(response)
        ? response
        : (Array.isArray(response?.data) ? response.data : []);
      const options = users
        .map((user) => ({
          value: String(user?.id || "").trim(),
          label: String(user?.fullName || "").trim()
        }))
        .filter((user) => user.value && user.label);
      workbook.fillSelect?.(byId("cycle-period-current-owner-person"), options, { placeholder: "Select person" });
      $person.trigger("change.select2");
    } catch {
      $person.empty().append('<option value="">Error loading persons</option>').trigger("change.select2");
    }
  }

  async function fillForm() {
    if (!period) return;
    clearFormError();

    // 1. Set simple values and trigger Select2
    $("#cycle-period-cycle").val(text(period.planningCycleId)).trigger("change.select2");
    byId("cycle-period-name").value = text(period.name);
    byId("cycle-period-code").value = text(period.code);
    
    // 2. Hierarchical: Company -> Position -> Person
    $("#cycle-period-owner-company").val(text(period.ownerCompanyId)).trigger("change.select2");
    await refreshOwnerPositions();
    if (period.ownerPositionId) {
      $("#cycle-period-owner-position").val(text(period.ownerPositionId)).trigger("change.select2");
      await syncCurrentOwnerPerson();
      const ownerId = text(period.currentOwnerPersonId || period.ownerEmployeeId);
      if (ownerId) {
        $("#cycle-period-current-owner-person").val(ownerId).trigger("change.select2");
      }
    }

    // 3. Scope
    $("#cycle-period-company").val(text(period.companyId)).trigger("change.select2");
    $("#cycle-period-bu").val(text(period.businessUnitId)).trigger("change.select2");
    $("#cycle-period-region").val(text(period.regionId)).trigger("change.select2");
    byId("cycle-period-default").checked = Boolean(period.isDefaultForScope);
    
    // 4. Timing
    $("#cycle-period-review").val(text(period.reviewCadence)).trigger("change.select2");
    byId("cycle-period-start").value = isoDate(period.startDate);
    byId("cycle-period-end").value = isoDate(period.endDate);
    byId("cycle-period-notes").value = text(period.notes);
    
    // Lock company fields
    $("#cycle-period-company, #cycle-period-owner-company").prop("disabled", true).trigger("change");

    updateReadiness();
    applyFieldLocking();
  }

  function getPayload() {
    return {
      planningCycleId: text(byId("cycle-period-cycle")?.value),
      name: text(byId("cycle-period-name")?.value),
      code: text(byId("cycle-period-code")?.value),
      ownerCompanyId: text(byId("cycle-period-owner-company")?.value),
      ownerPositionId: text(byId("cycle-period-owner-position")?.value),
      currentOwnerPersonId: text(byId("cycle-period-current-owner-person")?.value) || null,
      companyId: text(byId("cycle-period-company")?.value),
      businessUnitId: text(byId("cycle-period-bu")?.value) || null,
      regionId: text(byId("cycle-period-region")?.value) || null,
      startDate: text(byId("cycle-period-start")?.value),
      endDate: text(byId("cycle-period-end")?.value),
      reviewCadence: text(byId("cycle-period-review")?.value),
      isDefaultForScope: Boolean(byId("cycle-period-default")?.checked),
      notes: text(byId("cycle-period-notes")?.value),
      status: period?.status || "Draft"
    };
  }

  function getInitials(name) {
    if (!name) return "";
    return text(name).split(" ").map((n) => n[0]).join("").toUpperCase().slice(0, 2);
  }

  function fmtNiceDate(v, includeTime = false) {
    if (!v) return "-";
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return "-";
    const opt = { day: "numeric", month: "short", year: "numeric" };
    if (includeTime) { opt.hour = "2-digit"; opt.minute = "2-digit"; }
    return d.toLocaleDateString("en-GB", opt).replace(",", "");
  }

  function render() {
    const badge = byId("strategy-period-status-badge");
    const metadata = byId("strategy-period-detail-metadata");
    if (!period) return;

    const statusInfo = statusPill(period.status);
    const statusColorClass = statusInfo.cls.includes("active") ? "success" : (statusInfo.cls.includes("draft") ? "primary" : "secondary");
    const titleEl = document.querySelector(".es-detail-title");
    if (titleEl) titleEl.textContent = period.name;

    if (badge) {
      badge.className = `badge bg-label-${statusColorClass} text-uppercase ms-2`;
      badge.innerHTML = `<i class="bx bxs-circle me-1" style="font-size: 6px; vertical-align: middle;"></i> ${statusInfo.label}`;
    }

    if (metadata) {
      const ownerName = ownerLabel(period.currentOwnerPersonId || period.ownerEmployeeId);
      const parentCycle = cycleById(period.planningCycleId);
      const parentCycleLabel = text(period.planningCycleName || period.planningCycleCode)
        || text(parentCycle?.name || parentCycle?.code)
        || "-";
      metadata.innerHTML = `
        <div class="row row-cols-2 g-0 border-top">
          <div class="col p-3 border-bottom border-end">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Code</small>
            <div class="es-modern-meta-value"><span class="es-meta-code-pill">${period.code || "-"}</span></div>
          </div>
          <div class="col p-3 border-bottom">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Parent Cycle</small>
            <div class="es-modern-meta-value"><span class="badge bg-label-primary">${parentCycleLabel}</span></div>
          </div>
          <div class="col p-3 border-bottom border-end">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Start Date</small>
            <div class="es-modern-meta-value">${fmtNiceDate(period.startDate)}</div>
          </div>
          <div class="col p-3 border-bottom">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">End Date</small>
            <div class="es-modern-meta-value">${fmtNiceDate(period.endDate)}</div>
          </div>
          <div class="col p-3 border-bottom border-end">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Owner</small>
            <div class="es-modern-meta-value d-flex align-items-center gap-2">
              <div class="es-avatar-initials">${getInitials(ownerName)}</div>
              <span class="fw-medium">${ownerName}</span>
            </div>
          </div>
          <div class="col p-3 border-bottom">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Status</small>
            <div class="es-modern-meta-value"><span class="badge bg-label-${statusColorClass}">${statusInfo.label}</span></div>
          </div>

          <div class="col p-3 border-bottom border-end">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">Company (Scope)</small>
            <div class="es-modern-meta-value"><span class="fw-medium">${companyLabel(period.companyId)}</span></div>
          </div>
          <div class="col p-3 border-bottom">
            <small class="text-uppercase text-muted fw-bold mb-1 d-block" style="font-size: 0.65rem;">BU / Region</small>
            <div class="es-modern-meta-value"><span class="fw-medium">${[text(period.businessUnitId), text(period.regionId)].filter(Boolean).join(" / ") || "-"}</span></div>
          </div>
        </div>
        <div class="p-4"><small class="text-uppercase text-muted fw-bold mb-2 d-block" style="font-size: 0.65rem;">Notes</small><div class="text-muted small">${period.notes || "No notes."}</div></div>
      `;
    }
  }
  function initSelect2() {
    const $offcanvas = $(offcanvasEl);
    if (!$.fn.select2) return;
    $offcanvas.find(".select2").each(function () {
      const $this = $(this);
      if ($this.hasClass("select2-hidden-accessible")) $this.select2("destroy");
      $this.select2({ dropdownParent: $offcanvas, placeholder: "Select", allowClear: true, width: "100%" });
    });
  }

  async function load() {
    await workbook.ensureUsersLoaded?.();
    const [l, c, cp, p, posResult, usage] = await Promise.all([
      (window.strategyEnterpriseMetaApi?.lookups?.() || Promise.resolve({})).catch(() => ({})),
      api.listCycles().catch(() => []),
      (window.strategyCompaniesApi?.list() || Promise.resolve({ items: [] })).catch(() => ({ items: [] })),
      api.getStrategyPeriod(periodId),
      api.getAllPositions().catch(e => { 
        console.error("CRITICAL: Global Positions API (GetAllPosition) failed.", e); 
        return []; 
      }),
      api.getStrategyPeriodUsageSummary(periodId).catch(() => ({ goalCount: 0, objectiveCount: 0, isInUse: false, goals: [] }))
    ]);
    lookups = l || {};
    cycles = Array.isArray(c) ? c : [];
    const companies = Array.isArray(cp?.items) ? cp.items : [];
    usageSnapshot = usage;
    
    // Sync fetched companies with global workbook state
    if (companies.length && workbook.syncWorkbookCompanies) {
        workbook.syncWorkbookCompanies(companies);
    }
    
    companyOptions = companies.map(x => ({ value: x.id, label: x.companyName }));
    period = p || null;

    // Standardize positions
    lookups.positions = (Array.isArray(posResult) ? posResult : []).map(x => ({
        value: x.PositionId || x.id,
        label: x.PositionName || x.name,
        companyId: x.CompanyId || x.companyId
    }));

    initSelect2();

    const sortOpt = (arr) => [...(arr || [])].sort((a, b) => (a.label || "").localeCompare(b.label || ""));

    workbook.fillSelect?.(byId("cycle-period-cycle"), cycles.map(x => ({ value: x.id, label: `${x.code} - ${x.name}` })), { placeholder: "Select Cycle" });
    workbook.fillSelect?.(byId("cycle-period-owner-company"), sortOpt(companyOptions), { placeholder: "Select Company" });
    workbook.fillSelect?.(byId("cycle-period-company"), sortOpt(companyOptions), { placeholder: "Select Company" });
    workbook.fillSelect?.(byId("cycle-period-bu"), sortOpt(lookups.businessUnits), { placeholder: "Select BU" });
    workbook.fillSelect?.(byId("cycle-period-region"), sortOpt(lookups.regions), { placeholder: "Select Region" });
    workbook.fillSelect?.(byId("cycle-period-review"), sortOpt(lookups.reviewCadences), { placeholder: "Select Cadence" });

    render();
    syncDetailActionButtons();
  }

  byId("strategy-period-detail-edit")?.addEventListener("click", async () => {
    byId("strategyPeriodOffcanvasLabel").textContent = "Edit Strategy Period";
    byId("cycle-period-save").textContent = "Save Changes";
    await fillForm();
    offcanvas.show();
  });

  byId("cycle-period-save")?.addEventListener("click", async () => {
    try {
      if (!updateReadiness().ready) { notify("Please fill required fields.", "warning"); return; }
      await api.updateStrategyPeriod(periodId, getPayload());
      offcanvas.hide();
      notify("Period updated.");
      await load();
    } catch (err) { showError(err, "Update failed."); }
  });

  byId("strategy-period-detail-activate")?.addEventListener("click", async () => {
    try { await api.activatePeriod(periodId); notify("Activated."); await load(); } catch (err) { showError(err, "Failed."); }
  });

  byId("strategy-period-detail-archive")?.addEventListener("click", async () => {
    if (usageSnapshot.isInUse || (usageSnapshot.goalCount > 0 || usageSnapshot.objectiveCount > 0)) {
        showError(null, `Bu Strategy Period, ${usageSnapshot.goalCount} goal ve ${usageSnapshot.objectiveCount} objective tarafından kullanılmaktadır. Arşivlemeden önce bu atamaları kaldırın.`);
        return;
    }
    if (!confirm("⚠️ DİKKAT: Bu işlem geri alınamaz.\n\n" +
        "Arşivlenen Strategy Period yeniden aktif hale getirilemez.\n\n" +
        "Devam etmek istiyor musunuz?")) return;

    try { await api.archivePeriod(periodId); notify("Archived."); await load(); } catch (err) { showError(err, "Failed."); }
  });

  ["cycle-period-name", "cycle-period-cycle", "cycle-period-company", "cycle-period-owner-company", "cycle-period-start", "cycle-period-end", "cycle-period-review"]
    .forEach(id => byId(id)?.addEventListener("change", () => {
        updateReadiness();
        applyFieldLocking();
    }));
  
  byId("cycle-period-owner-company")?.addEventListener("change", refreshOwnerPositions);
  byId("cycle-period-owner-position")?.addEventListener("change", async () => {
    await syncCurrentOwnerPerson();
    updateReadiness();
  });

  // Event-driven refresh for Offcanvas
  offcanvasEl?.addEventListener("shown.bs.offcanvas", () => {
    refreshOwnerPositions();
  });

  load();
})(window, document);
