"use strict";

window.strategyPeriodsApp = (function (window, document) {
  const api = window.strategyPlanningApi;
  const ui = window.enterpriseStrategyUi || {};
  const workbook = window.enterpriseWorkbookOptions || {};
  const strategyPeriodsListUrl = "/management-governance/enterprise-strategy-business-performance/planning/strategy-periods";

  let tableEl, dt, offcanvasEl, offcanvas;
  let readinessIndicatorEl, readinessTextEl, readinessMissingEl, readinessBlockersEl;
  let identityStateEl, scopeStateEl, ownershipStateEl, timingStateEl;
  let scopeSummaryEl, parentHorizonHintEl;

  let lookups = {};
  let cycles = [];
  let rows = [];
  let usedPeriodIds = new Set();
  let editId = "";
  let companyOptions = [];

  const editableFieldIds = [
    "cycle-period-cycle", "cycle-period-name", "cycle-period-code", "cycle-period-company",
    "cycle-period-owner-company", "cycle-period-owner-position", "cycle-period-current-owner-person",
    "cycle-period-start", "cycle-period-end", "cycle-period-review"
  ];

  function text(v) { return String(v || "").trim(); }
  function fmtDate(v) {
    if (!v) return "-";
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? "-" : d.toLocaleDateString();
  }
  function notify(message, kind) { ui.notify?.(message, kind || "success"); }
  function showError(err, fallback) { notify(ui.getErrorMessage?.(err, fallback) || fallback, "danger"); }
  const byId = (id) => document.getElementById(id);

  function statusBadge(status) {
    const normalized = text(status).toLowerCase();
    const statusObj = {
      active: { title: "Active", class: "bg-label-success" },
      draft: { title: "Draft", class: "bg-label-warning" },
      archived: { title: "Archived", class: "bg-label-secondary" }
    };
    const hit = statusObj[normalized] || { title: text(status) || "-", class: "bg-label-info" };
    return `<span class="badge ${hit.class}">${hit.title}</span>`;
  }

  function companyLabel(id) {
    const hit = companyOptions.find(x => text(x.value).toLowerCase() === text(id).toLowerCase());
    return hit?.label || id || "-";
  }

  function cycleLabel(row) {
    const direct = text(row?.planningCycleName || row?.planningCycleCode);
    if (direct) return direct;
    const hit = cycles.find((x) => text(x.id).toLowerCase() === text(row?.planningCycleId).toLowerCase());
    return text(hit?.name || hit?.code) || "-";
  }

  function ownerLabel(ownerId) {
    return workbook.userDisplayName?.(ownerId) || ownerId || "-";
  }

  function getInitials(name) {
    return (text(name).match(/\b\w/g) || []).map((char) => char.toUpperCase()).join("").slice(0, 2) || "--";
  }

  function syncCompanyPair(sourceId, targetId) {
    const source = byId(sourceId);
    const target = byId(targetId);
    if (!source || !target) return;

    const value = text(source.value);
    if (text(target.value) === value) return;

    if (window.jQuery && window.jQuery(target).hasClass("select2-hidden-accessible")) {
      window.jQuery(target).val(value || "").trigger("change.select2");
    } else {
      target.value = value || "";
      target.dispatchEvent(new Event("change", { bubbles: true }));
    }
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

    return `<div class="d-flex flex-column align-items-center small fw-medium"><span class="text-muted mb-1">${compactDate(startDate)}</span><span class="${toColorClass}">${compactDate(endDate)}</span></div>`;
  }

  function errorHost() { return byId("cycle-period-form-error"); }

  function clearFormError() {
    const host = errorHost();
    if (host) { host.classList.add("d-none"); host.innerHTML = ""; }
  }

  function showFormError(errors) {
    const host = errorHost();
    if (!host) return;
    const list = (errors || []).filter(Boolean);
    if (!list.length) return clearFormError();
    host.classList.remove("d-none");
    host.innerHTML = `<strong>Could not save Strategy Period.</strong><ul class="mb-0">${list.map(x => `<li>${x}</li>`).join("")}</ul>`;
  }

  function clearInvalidField(fieldId) {
    const el = byId(fieldId);
    if (!el) return;
    el.classList.remove("is-invalid");
    const s2 = el.nextElementSibling;
    if (s2?.classList.contains("select2-container")) {
      s2.querySelector(".select2-selection")?.classList.remove("is-invalid");
    }
  }

  function markInvalidField(fieldId) {
    const el = byId(fieldId);
    if (!el) return;
    el.classList.add("is-invalid");
    const s2 = el.nextElementSibling;
    if (s2?.classList.contains("select2-container")) {
      s2.querySelector(".select2-selection")?.classList.add("is-invalid");
    }
  }

  function clearFieldErrors() { editableFieldIds.forEach(clearInvalidField); }

  function setSectionState(el, ok) {
    if (!el) return;
    el.className = `es-status-pill ${ok ? "is-ready" : "is-blocked"}`;
    el.textContent = ok ? "Complete" : "Blocked";
  }

  function normalizeCodePart(value) {
    return text(value).toUpperCase().replace(/[^A-Z0-9]+/g, "").slice(0, 6);
  }

  function selectedCycleCodeSource() {
    const selectedCycleId = text(byId("cycle-period-cycle")?.value).toLowerCase();
    const hit = cycles.find((x) => text(x.id).toLowerCase() === selectedCycleId);
    return text(hit?.name || hit?.code || "CYCLE");
  }

  function autoPeriodCode() {
    const cyclePart = normalizeCodePart(selectedCycleCodeSource() || "CYCLE");
    const namePart = normalizeCodePart(byId("cycle-period-name")?.value || "PERIOD");
    const stamp = Date.now().toString().slice(-6);
    return `SP-${cyclePart || "CYCLE"}-${namePart || "PERIOD"}-${stamp}`;
  }

  function getPayload() {
    const codeEl = byId("cycle-period-code");
    if (codeEl && !editId && !text(codeEl.value)) {
      codeEl.value = autoPeriodCode();
    }
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
      notes: text(byId("cycle-period-notes")?.value)
    };
  }

  async function fillForm(data) {
    clearFormError();
    clearFieldErrors();
    const p = data || {};
    
    $("#cycle-period-cycle").val(text(p.planningCycleId)).trigger("change.select2");
    byId("cycle-period-name").value = text(p.name);
    byId("cycle-period-code").value = text(p.code);
    
    $("#cycle-period-owner-company").val(text(p.ownerCompanyId)).trigger("change.select2");
    await refreshOwnerPositions();
    if (p.ownerPositionId) {
      $("#cycle-period-owner-position").val(text(p.ownerPositionId)).trigger("change.select2");
      await syncCurrentOwnerPerson();
      const ownerId = text(p.currentOwnerPersonId || p.ownerEmployeeId);
      if (ownerId) {
        $("#cycle-period-current-owner-person").val(ownerId).trigger("change.select2");
      }
    }

    $("#cycle-period-company").val(text(p.companyId)).trigger("change.select2");
    $("#cycle-period-bu").val(text(p.businessUnitId)).trigger("change.select2");
    $("#cycle-period-region").val(text(p.regionId)).trigger("change.select2");
    byId("cycle-period-default").checked = Boolean(p.isDefaultForScope);
    
    $("#cycle-period-review").val(text(p.reviewCadence)).trigger("change.select2");
    byId("cycle-period-start").value = text(p.startDate).slice(0, 10);
    byId("cycle-period-end").value = text(p.endDate).slice(0, 10);
    byId("cycle-period-notes").value = text(p.notes);
    
    updateReadiness();
  }

  function applyFieldLocking(p, usage) {
    const isActive = text(p?.status).toLowerCase() === "active";
    const isInUse = usage?.isInUse || false;
    const lockStructural = isActive && isInUse;

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
        el.title = `Bu alan, ${usage.goalCount} goal ve ${usage.objectiveCount} objective bağlı olduğu için kilitlidir.`;
      } else if (isActive) {
        $el.prop("disabled", false).removeClass("bg-light").addClass("border-warning");
        el.title = "Bu alan aktif durumda. Değişiklik gelecekteki atamaları etkileyebilir.";
      } else {
        $el.prop("disabled", false).removeClass("bg-light border-warning");
        el.title = "";
      }
      if ($el.hasClass("select2-hidden-accessible")) $el.trigger("change.select2");
    });

    ["cycle-period-review", "cycle-period-default"].forEach((fieldId) => {
      const el = byId(fieldId);
      if (!el) return;
      const $el = $(el);
      if (isActive && isInUse) $el.addClass("border-warning");
      else $el.removeClass("border-warning");
    });

    const banner = byId("cycle-period-in-use-banner");
    if (banner) {
      banner.classList.toggle("d-none", !lockStructural);
      if (lockStructural) {
        const gc = byId("in-use-goal-count");
        const oc = byId("in-use-obj-count");
        if (gc) gc.textContent = usage.goalCount;
        if (oc) oc.textContent = usage.objectiveCount;
      }
    }
  }

  function resetForm() {
    editId = "";
    fillForm({});
    byId("strategyPeriodOffcanvasLabel").textContent = "Create Strategy Period";
    byId("cycle-period-save").textContent = "Submit";
    $("#cycle-period-company, #cycle-period-owner-company").prop("disabled", false).trigger("change");
    if (byId("cycle-period-code")) byId("cycle-period-code").value = autoPeriodCode();
  }

  function updateReadiness() {
    const p = getPayload();
    const missing = [];
    if (!p.planningCycleId) missing.push("Parent Cycle");
    if (!p.name) missing.push("Name");
    if (!p.ownerCompanyId) missing.push("Owner Company");
    if (!p.ownerPositionId) missing.push("Owner Position");
    if (!p.companyId) missing.push("Company");
    if (!p.reviewCadence) missing.push("Review Cadence");
    if (!p.startDate) missing.push("Start Date");
    if (!p.endDate) missing.push("End Date");

    const blockers = [];
    if (p.startDate && p.endDate && p.endDate < p.startDate) blockers.push("End Date < Start Date.");
    
    const ready = missing.length === 0 && blockers.length === 0;
    if (readinessIndicatorEl) {
      readinessIndicatorEl.className = `es-status-pill ${ready ? "is-ready" : "is-blocked"}`;
      readinessIndicatorEl.textContent = ready ? "Readiness: Ready" : "Readiness: Blocked";
    }
    setSectionState(identityStateEl, Boolean(p.planningCycleId && p.name));
    setSectionState(scopeStateEl, Boolean(p.companyId));
    setSectionState(ownershipStateEl, Boolean(p.ownerCompanyId && p.ownerPositionId));
    setSectionState(timingStateEl, Boolean(p.reviewCadence && p.startDate && p.endDate && blockers.length === 0));
    return { ready, missing, blockers };
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
      $person.empty().append('<option value="">Error</option>').trigger("change.select2");
    }
  }

  async function save() {
    try {
      if (!updateReadiness().ready) { notify("Please fix errors.", "warning"); return; }
      const draft = getPayload();
      if (editId) { await api.updateStrategyPeriod(editId, draft); notify("Updated."); }
      else { await api.createStrategyPeriod(draft); notify("Created."); }
      offcanvas?.hide();
      await load();
    } catch (err) { showError(err, "Save failed."); }
  }

  function setSummaryCards(all) {
    if (byId("strategy-period-total")) byId("strategy-period-total").textContent = String(all.length);
    if (byId("strategy-period-active")) byId("strategy-period-active").textContent = String(all.filter(x => text(x.status).toLowerCase() === "active").length);
    if (byId("strategy-period-draft")) byId("strategy-period-draft").textContent = String(all.filter(x => text(x.status).toLowerCase() === "draft").length);
    if (byId("strategy-period-in-use")) byId("strategy-period-in-use").textContent = String(all.filter(x => usedPeriodIds.has(text(x.id).toLowerCase())).length);
  }

  function usageBadge(row) {
    return usedPeriodIds.has(text(row.id).toLowerCase())
      ? '<span class="badge bg-label-primary">Used</span>'
      : '<span class="badge bg-label-light text-body-secondary">Unused</span>';
  }

  function sortOptions(arr) {
    return [...(arr || [])].sort((a, b) => (a.label || a.text || "").localeCompare(b.label || b.text || "", "tr", { sensitivity: "base" }));
  }

  function asOptionList(arr) {
    return (arr || []).map((item) => typeof item === "string" ? { value: item, label: item, text: item } : item);
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
          if (classToRemove) classToRemove.split(" ").forEach((className) => element.classList.remove(className));
          if (classToAdd) classToAdd.split(" ").forEach((className) => element.classList.add(className));
        });
      });

      const host = document.getElementById("filterCollapse");
      const filterBtn = document.querySelector(".dt-filter-btn");
      if (host && filterBtn) {
        const toolbarRow =
          filterBtn.closest(".dt-layout-row") ||
          filterBtn.closest(".row") ||
          filterBtn.closest(".dt-layout-end")?.parentElement;

        if (toolbarRow && host.previousElementSibling !== toolbarRow) {
          toolbarRow.insertAdjacentElement("afterend", host);
          host.classList.add("px-3");
        }
      }

      const dtButtons = document.querySelector(".dt-buttons");
      if (dtButtons) {
        const eyeBtn = dtButtons.querySelector(".dt-eye-btn");
        const filterBtnInner = dtButtons.querySelector(".dt-filter-btn");
        if (eyeBtn && filterBtnInner && !eyeBtn.parentElement.classList.contains("btn-group")) {
          const group = document.createElement("div");
          group.className = "btn-group";
          eyeBtn.parentNode.insertBefore(group, eyeBtn);
          group.appendChild(eyeBtn);
          group.appendChild(filterBtnInner);
          [eyeBtn, filterBtnInner].forEach((btn) => {
            btn.classList.remove("ms-2", "mx-1", "mx-2", "mx-3", "mx-4", "ms-3");
            btn.style.margin = "0";
          });
        }
      }
    }, 100);
  }

  function updateFilterBadge() {
    const filterGroups = [
      { id: ".strategy_period_cycle", label: "Cycle" },
      { id: ".strategy_period_company", label: "Company" },
      { id: ".strategy_period_review", label: "Review" },
      { id: ".strategy_period_status", label: "Status" }
    ];

    let count = 0;
    const tooltipRows = [];

    filterGroups.forEach((group) => {
      const select = document.querySelector(`${group.id} select`);
      if (select && select.value) {
        count++;
        tooltipRows.push(`${group.label}: ${select.options[select.selectedIndex].text}`);
      }
    });

    const btn = document.querySelector(".dt-filter-btn");
    if (!btn) return;

    let badge = btn.querySelector(".badge");
    if (count > 0) {
      if (!badge) {
        badge = document.createElement("span");
        badge.className = "badge rounded-pill bg-primary badge-notifications";
        badge.style.position = "absolute";
        badge.style.top = "-5px";
        badge.style.right = "-5px";
        badge.style.padding = "0.2rem 0.4rem";
        badge.style.fontSize = "0.65rem";
        badge.style.border = "2px solid white";
        btn.appendChild(badge);
      }
      btn.style.position = "relative";
      badge.textContent = count;
      badge.setAttribute("data-bs-toggle", "tooltip");
      badge.setAttribute("data-bs-placement", "top");
      badge.setAttribute("data-bs-html", "true");
      badge.setAttribute("title", tooltipRows.join("<br>"));
      if (window.bootstrap?.Tooltip) {
        window.bootstrap.Tooltip.getInstance(badge)?.dispose();
        new window.bootstrap.Tooltip(badge);
      }
    } else if (badge) {
      window.bootstrap?.Tooltip?.getInstance?.(badge)?.dispose?.();
      badge.remove();
    }
  }

  async function loadData() {
    await Promise.all([
      workbook.ensureUsersLoaded?.(),
      workbook.ensureLookupsLoaded?.(),
      workbook.ensureCompaniesLoaded?.()
    ]);

    const [lookupResult, cycleList, all, cp, gList, posResult] = await Promise.all([
      window.strategyEnterpriseMetaApi?.lookups?.().catch(() => ({})),
      api.listCycles().catch(() => []),
      api.listStrategyPeriods().catch(() => []),
      (window.strategyCompaniesApi?.list() || Promise.resolve({ items: [] })).catch(() => ({ items: [] })),
      (window.strategyGoalsApi?.list?.() || Promise.resolve({ items: [] })).catch(() => ({ items: [] })),
      (api.getAllPositions ? api.getAllPositions() : Promise.resolve([])).catch(() => [])
    ]);

    lookups = lookupResult || {};
    lookups.positions = (Array.isArray(posResult) ? posResult : []).map((x) => ({
      id: x.PositionId || x.id,
      value: x.PositionId || x.id,
      label: x.PositionName || x.name,
      companyId: x.CompanyId || x.companyId
    }));

    cycles = Array.isArray(cycleList) ? cycleList : [];
    rows = Array.isArray(all) ? all : [];
    const companies = Array.isArray(cp?.items) ? cp.items : [];
    companyOptions = companies.map((x) => ({ value: x.id || x.companyId, label: x.companyName || x.label || x.name }));
    usedPeriodIds = new Set((gList?.items || []).map((g) => text(g.strategyPeriodId).toLowerCase()).filter(Boolean));

    setSummaryCards(rows);
    await initLookups();
    return rows;
  }

  async function initLookups() {
    workbook.fillSelect?.(byId("cycle-period-cycle"), cycles.map((x) => ({ value: x.id, label: x.name })), { placeholder: "Select Cycle" });
    workbook.fillSelect?.(byId("cycle-period-owner-company"), sortOptions(companyOptions), { placeholder: "Select Company" });
    workbook.fillSelect?.(byId("cycle-period-company"), sortOptions(companyOptions), { placeholder: "Select Company" });
    workbook.fillSelect?.(byId("cycle-period-bu"), sortOptions(asOptionList(lookups.businessUnits)), { placeholder: "Select BU" });
    workbook.fillSelect?.(byId("cycle-period-region"), sortOptions(asOptionList(lookups.regions)), { placeholder: "Select Region" });
    workbook.fillSelect?.(byId("cycle-period-review"), sortOptions(asOptionList(lookups.reviewCadences)), { placeholder: "Select Cadence" });

    const cycleFilterSelect = document.querySelector(".strategy_period_cycle select");
    if (cycleFilterSelect) workbook.fillSelect?.(cycleFilterSelect, cycles.map((x) => ({ value: x.id, label: x.name })), { placeholder: "Select Cycle", keepCurrent: true });

    const companyFilterSelect = document.querySelector(".strategy_period_company select");
    if (companyFilterSelect) workbook.fillSelect?.(companyFilterSelect, sortOptions(companyOptions), { placeholder: "Select Company", keepCurrent: true });

    const reviewFilterSelect = document.querySelector(".strategy_period_review select");
    if (reviewFilterSelect) workbook.fillSelect?.(reviewFilterSelect, sortOptions(asOptionList(lookups.reviewCadences)), { placeholder: "Select Cadence", keepCurrent: true });

    const statusFilterSelect = document.querySelector(".strategy_period_status select");
    if (statusFilterSelect) {
      const statuses = asOptionList(lookups.strategyPeriodLifecycleStatuses);
      workbook.fillSelect?.(statusFilterSelect, sortOptions(statuses), { placeholder: "Select Status", keepCurrent: true });
    }
  }

  function renderFallbackTable(data) {
    const tbody = tableEl?.querySelector("tbody");
    if (!tbody) return;

    tbody.innerHTML = (data || []).length ? data.map((x) => {
      const status = text(x.status).toLowerCase();
      const owner = ownerLabel(x.currentOwnerPersonId || x.ownerEmployeeId);
      return `
        <tr>
          <td><div class="fw-medium text-heading">${x.name || "-"}</div><div class="small text-muted">${x.code || ""}</div></td>
          <td>${owner}</td>
          <td>${cycleLabel(x)}</td>
          <td>${companyLabel(x.companyId)}</td>
          <td>${x.businessUnitId || "-"}</td>
          <td>${x.regionId || "-"}</td>
          <td>${timelineMarkup(x.startDate, x.endDate)}</td>
          <td>${x.reviewCadence || "-"}</td>
          <td>${usageBadge(x)}</td>
          <td>${statusBadge(x.status)}</td>
          <td class="text-end">
            <div class="d-flex align-items-center justify-content-end strategy-period-col-actions">
              <a href="${strategyPeriodsListUrl}/${encodeURIComponent(x.id)}" class="btn btn-icon"><i class="icon-base bx bx-show icon-md"></i></a>
              ${status !== "archived" ? `
                <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="icon-base bx bx-dots-vertical-rounded icon-md"></i></a>
                <ul class="dropdown-menu dropdown-menu-end">
                  <li><a class="dropdown-item" href="javascript:;" data-action="edit" data-id="${x.id}">Edit</a></li>
                  ${status === "draft" ? `<li><a class="dropdown-item" href="javascript:;" data-action="activate" data-id="${x.id}">Activate</a></li>` : ""}
                  <li><hr class="dropdown-divider"></li>
                  <li><a class="dropdown-item text-danger" href="javascript:;" data-action="archive" data-id="${x.id}">Archive / Delete</a></li>
                </ul>
              ` : ""}
            </div>
          </td>
        </tr>`;
    }).join("") : '<tr><td colspan="11" class="text-center p-5 text-muted">No records.</td></tr>';
  }

  function initDataTable() {
    if (!tableEl || typeof DataTable === "undefined") return false;
    if (dt) return true;

    dt = new DataTable(tableEl, {
      responsive: {
        details: {
          display: DataTable.Responsive.display.modal({
            header: function (row) {
              const data = row.data();
              return `<h5 class="modal-title">Strategy Period Details - ${data?.name || ""}</h5>`;
            }
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
      processing: true,
      serverSide: false,
      ajax: async (data, callback) => {
        try {
          if (typeof Notiflix !== "undefined") {
            Notiflix.Block.standard("#strategy-periods-card", {
              backgroundColor: "rgba(255, 255, 255, 0.45)",
              svgSize: "45px",
              svgColor: "#5a5fe0",
              messageFontSize: "14px",
              cssAnimation: true,
              cssAnimationDuration: 300
            });
          }
          const loaded = await loadData();
          callback({ data: loaded });
        } catch (err) {
          console.error("Failed to load strategy periods:", err);
          callback({ data: [] });
        } finally {
          if (typeof Notiflix !== "undefined") Notiflix.Block.remove("#strategy-periods-card", 400);
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
          responsivePriority: 1000,
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
        { targets: 5, render: (data) => companyLabel(data) },
        { targets: 6, render: (data) => data || "-" },
        { targets: 7, render: (data) => data || "-" },
        {
          targets: 8,
          render: (data, type, full) => timelineMarkup(full.startDate, full.endDate)
        },
        { targets: 9, render: (data) => data || "-" },
        { targets: 10, render: (data, type, full) => usageBadge(full) },
        { targets: 11, render: (data) => statusBadge(data) },
        {
          targets: -1,
          responsivePriority: 1,
          className: "all text-end",
          render: (data, type, full) => {
            const status = text(full.status).toLowerCase();
            return `
              <div class="d-flex align-items-center justify-content-end strategy-period-col-actions">
                <a href="${strategyPeriodsListUrl}/${encodeURIComponent(full.id)}" class="btn btn-icon">
                  <i class="icon-base bx bx-show icon-md"></i>
                </a>
                ${status !== "archived" ? `
                  <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                    <i class="icon-base bx bx-dots-vertical-rounded icon-md"></i>
                  </a>
                  <div class="dropdown-menu dropdown-menu-end m-0">
                    <a href="javascript:;" class="dropdown-item btn-edit" data-id="${full.id}">Edit</a>
                    ${status === "draft" ? `<a href="javascript:;" class="dropdown-item btn-activate" data-id="${full.id}">Activate</a>` : ""}
                    <a href="javascript:;" class="dropdown-item btn-archive" data-id="${full.id}">Archive / Delete</a>
                  </div>
                ` : ""}
              </div>`;
          }
        }
      ],
      order: [[2, "asc"]],
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
                placeholder: "Search Period",
                text: "_INPUT_"
              }
            },
            {
              buttons: [
                {
                  extend: "collection",
                  className: "btn btn-label-secondary dropdown-toggle",
                  text: '<i class="icon-base bx bx-export icon-sm me-2"></i>Export',
                  buttons: ["print", "csv", "excel", "pdf", "copy"]
                },
                {
                  text: '<i class="icon-base bx bx-show icon-sm"></i>',
                  className: "btn btn-icon btn-label-secondary dt-eye-btn",
                  action: function () {}
                },
                {
                  text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                  className: "btn btn-icon btn-label-secondary dt-filter-btn",
                  action: function () {
                    const filterEl = document.getElementById("filterCollapse");
                    if (filterEl) {
                      const bsCollapse = bootstrap.Collapse.getOrCreateInstance(filterEl);
                      bsCollapse.toggle();
                      this.node().classList.toggle("active");
                    }
                  }
                },
                {
                  text: '<i class="icon-base bx bx-refresh icon-sm"></i>',
                  className: "btn btn-icon btn-label-secondary",
                  action: async () => { await load(); }
                },
                {
                  text: '<i class="icon-base bx bx-plus icon-sm me-sm-2"></i>Create Strategy Period',
                  className: "btn btn-primary",
                  action: async () => { await resetForm(); offcanvas?.show(); }
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
        const apiInstance = this.api();
        const createFilter = (colIdx, container, placeholder, sourceOptions) => {
          const wrapper = document.querySelector(container);
          if (!wrapper) return;
          wrapper.innerHTML = "";
          const select = document.createElement("select");
          select.className = "form-select form-select-sm text-capitalize";
          select.innerHTML = `<option value="">${placeholder}</option>`;
          wrapper.appendChild(select);

          select.addEventListener("change", () => {
            const val = select.value ? `^${select.value}$` : "";
            apiInstance.column(colIdx).search(val, true, false);
          });

          (sourceOptions || []).forEach((item) => {
            const value = typeof item === "string" ? item : (item.value || item.id);
            const label = typeof item === "string" ? item : (item.label || item.text || item.name);
            if (!value) return;
            const option = document.createElement("option");
            option.value = value;
            option.textContent = label;
            select.appendChild(option);
          });
        };

        createFilter(4, ".strategy_period_cycle", "Select Cycle", rows.map((x) => {
          const cycleText = cycleLabel(x);
          return { value: cycleText, label: cycleText };
        }));
        createFilter(5, ".strategy_period_company", "Select Company", companyOptions);
        createFilter(9, ".strategy_period_review", "Select Cadence", sortOptions(asOptionList(lookups.reviewCadences)));
        createFilter(11, ".strategy_period_status", "Select Status", sortOptions(asOptionList(lookups.strategyPeriodLifecycleStatuses)));

        document.querySelector(".btn-apply-filter")?.addEventListener("click", () => {
          apiInstance.draw();
          const filterEl = document.getElementById("filterCollapse");
          if (filterEl) {
            bootstrap.Collapse.getInstance(filterEl)?.hide();
            document.querySelector(".dt-filter-btn")?.classList.remove("active");
          }
          updateFilterBadge();
        });

        document.querySelector(".btn-reset-filter")?.addEventListener("click", () => {
          document.querySelectorAll("#filterCollapse select").forEach((select) => { select.value = ""; });
          apiInstance.columns().search("").draw();
          updateFilterBadge();
        });

        fixDataTableLayout();
      },
      drawCallback: function () {
        fixDataTableLayout();
      }
    });

    return true;
  }

  async function load() {
    if (dt) {
      dt.ajax.reload();
      return;
    }

    const initialized = initDataTable();
    if (!initialized) {
      const data = await loadData();
      renderFallbackTable(data);
    }
  }

  function registerEvents() {
    byId("cycle-period-save")?.addEventListener("click", save);
    byId("cycle-period-owner-company")?.addEventListener("change", refreshOwnerPositions);
    byId("cycle-period-owner-position")?.addEventListener("change", async () => { await syncCurrentOwnerPerson(); updateReadiness(); });
    byId("cycle-period-owner-company")?.addEventListener("change", () => {
      syncCompanyPair("cycle-period-owner-company", "cycle-period-company");
    });
    byId("cycle-period-company")?.addEventListener("change", () => {
      syncCompanyPair("cycle-period-company", "cycle-period-owner-company");
    });

    offcanvasEl?.addEventListener("shown.bs.offcanvas", () => {
      refreshOwnerPositions();
    });

    byId("cycle-period-cycle")?.addEventListener("change", function() {
      const cid = this.value;
      if (!cid) {
        if (!editId && byId("cycle-period-code")) byId("cycle-period-code").value = autoPeriodCode();
        return;
      }
      const hit = cycles.find((c) => text(c.id) === text(cid));
      if (hit && hit.ownerCompanyId) {
        $("#cycle-period-company").val(hit.ownerCompanyId).trigger("change");
        $("#cycle-period-owner-company").val(hit.ownerCompanyId).trigger("change");
      }
      if (!editId && byId("cycle-period-code")) byId("cycle-period-code").value = autoPeriodCode();
    });

    byId("cycle-period-name")?.addEventListener("input", () => {
      if (!editId) {
        const codeEl = byId("cycle-period-code");
        if (codeEl) codeEl.value = autoPeriodCode();
      }
      updateReadiness();
    });

    offcanvasEl?.addEventListener("hidden.bs.offcanvas", resetForm);

    tableEl?.addEventListener("click", async (e) => {
      const btn = e.target.closest(".btn-edit, .btn-activate, .btn-archive, [data-action]");
      if (!btn) return;

      const id = btn.dataset.id;
      const action = btn.dataset.action ||
        (btn.classList.contains("btn-edit") ? "edit" :
          btn.classList.contains("btn-activate") ? "activate" :
            btn.classList.contains("btn-archive") ? "archive" : "");
      if (!action || !id) return;

      if (action === "edit") {
        editId = id;
        const [p, usage] = await Promise.all([
            api.getStrategyPeriod(id),
            api.getStrategyPeriodUsageSummary(id).catch(() => ({ goalCount: 0, objectiveCount: 0, isInUse: false }))
        ]);
        byId("strategyPeriodOffcanvasLabel").textContent = "Edit Strategy Period";
        await fillForm(p);
        applyFieldLocking(p, usage);
        offcanvas?.show();
      } else if (action === "activate") {
        await api.activatePeriod(id);
        notify("Activated.");
        await load();
      } else if (action === "archive") {
        const usage = await api.getStrategyPeriodUsageSummary(id).catch(() => ({ goalCount: 0, objectiveCount: 0, isInUse: false }));
        if (usage.isInUse || (usage.goalCount > 0 || usage.objectiveCount > 0)) {
            showError(null, `Bu Strategy Period, ${usage.goalCount} goal ve ${usage.objectiveCount} objective tarafından kullanılmaktadır. Arşivlemeden önce bu atamaları kaldırın.`);
            return;
        }

        if (!confirm("⚠️ DİKKAT: Bu işlem geri alınamaz.\n\n" +
            "Arşivlenen Strategy Period yeniden aktif hale getirilemez.\n\n" +
            "Devam etmek istiyor musunuz?")) return;

        await api.archivePeriod(id);
        notify("Archived.");
        await load();
      }
    });

    document.querySelectorAll(".planning-filter-card").forEach((card) => {
      card.addEventListener("click", function () {
        const filterValue = this.dataset.filterStatus;
        document.querySelectorAll(".planning-filter-card").forEach((item) => {
          item.classList.remove("border", "border-primary", "border-2", "shadow-sm");
        });

        if (!dt) return;

        const statusSelect = document.querySelector(".strategy_period_status select");

        if (filterValue === "all") {
          dt.column(11).search("").column(10).search("").draw();
          if (statusSelect) statusSelect.value = "";
        } else if (filterValue === "Used") {
          this.classList.add("border", "border-primary", "border-2", "shadow-sm");
          dt.column(11).search("").column(10).search("Used", true, false).draw();
          if (statusSelect) statusSelect.value = "";
        } else {
          this.classList.add("border", "border-primary", "border-2", "shadow-sm");
          dt.column(10).search("").column(11).search(filterValue, true, false).draw();
          if (statusSelect) statusSelect.value = filterValue;
        }
        updateFilterBadge();
      });
    });
  }

  return {
    initPage: async function() {
      tableEl = document.querySelector(".strategy-periods-table") || document.getElementById("strategy-periods-table");
      offcanvasEl = document.getElementById("strategyPeriodOffcanvas");
      offcanvas = offcanvasEl ? new bootstrap.Offcanvas(offcanvasEl) : null;
      readinessIndicatorEl = document.getElementById("cycle-period-readiness-indicator");
      identityStateEl = document.getElementById("cycle-period-sec-identity-state");
      scopeStateEl = document.getElementById("cycle-period-sec-scope-state");
      ownershipStateEl = document.getElementById("cycle-period-sec-ownership-state");
      timingStateEl = document.getElementById("cycle-period-sec-timing-state");

      registerEvents();
      await load();
    }
  };
})(window, document);

document.addEventListener("DOMContentLoaded", () => {
  window.strategyPeriodsApp.initPage().catch(err => console.error(err));
});
