(function (window, document) {
  "use strict";

  const tableBody = document.querySelector("#initiatives-table tbody");
  const detailCard = document.getElementById("initiative-detail-card");
  const detailContent = document.getElementById("initiative-detail-content");
  const detailLinkBtn = document.getElementById("initiative-detail-link-btn");
  const ppmModal = new bootstrap.Modal(document.getElementById("initiativePpmModal"));
  const linkModal = new bootstrap.Modal(document.getElementById("initiativeLinkModal"));
  const ppmModalEl = document.getElementById("initiativePpmModal");
  const linkModalEl = document.getElementById("initiativeLinkModal");
  const ppmErr = document.getElementById("initiative-ppm-error");
  const linkErr = document.getElementById("initiative-link-error");
  const objectiveList = document.getElementById("ppm-objective-list");
  const initiativeList = document.getElementById("link-initiative-list");
  const headerRow = document.getElementById("initiatives-header-row");

  const state = { rows: [], filtered: [], goals: [], objectives: [], selected: null, selectedIds: new Set() };
  const byId = (id) => document.getElementById(id);
  const createUrl = "/management-governance/delivery-execution/initiatives/new";
  const editUrl = (initiativeId) => `/management-governance/delivery-execution/initiatives/${encodeURIComponent(String(initiativeId || "").trim())}/edit`;
  const detailUrl = (initiativeId) => `/management-governance/delivery-execution/initiatives/${encodeURIComponent(String(initiativeId || "").trim())}`;
  const importFileInput = byId("initiative-import-file");
  const importWorkbookInput = byId("initiative-import-workbook-file");
  const importPageActionBtn = byId("initiative-data-import-page");
  const importWorkbookActionBtn = byId("initiative-data-import-workbook");
  const bulkActionsToggle = byId("initiative-bulk-actions-toggle");
  const bulkExportCsvBtn = byId("initiative-bulk-export-csv");
  const bulkExportXlsxBtn = byId("initiative-bulk-export-xlsx");
  const bulkExportWorkbookBtn = byId("initiative-bulk-export-workbook");
  const bulkClearSelectionBtn = byId("initiative-bulk-clear-selection");
  const bulkActivateBtn = byId("initiative-bulk-activate");
  const bulkArchiveBtn = byId("initiative-bulk-archive");
  const ppmCreateBtn = byId("initiative-ppm-create");
  const linkConfirmBtn = byId("initiative-link-confirm");
  const periodRegex = /^\d{4}-Q[1-4]$/;
  const workbook = window.enterpriseWorkbookOptions || {};
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();
  const ppmRequiredIds = [
    "ppm-initiative-name",
    "ppm-parent-objective",
    "ppm-initiative-owner",
    "ppm-initiative-type",
    "ppm-sponsoring-company",
    "ppm-wave",
    "ppm-start-period",
    "ppm-end-period"
  ];
  const linkRequiredIds = ["link-initiative-id", "link-parent-objective"];
  let ppmDirty = false;
  let linkDirty = false;
  const filters = {
    search: byId("initiative-search"),
    owner: byId("initiative-filter-owner"),
    status: byId("initiative-filter-status"),
    type: byId("initiative-filter-type"),
    parentGoal: byId("initiative-filter-parent-goal"),
    parentObjective: byId("initiative-filter-parent-objective"),
    wave: byId("initiative-filter-wave"),
    priority: byId("initiative-filter-priority"),
    complexity: byId("initiative-filter-complexity"),
    maturity: byId("initiative-filter-maturity"),
    sponsoringCompany: byId("initiative-filter-sponsoring-company"),
    participatingCompany: byId("initiative-filter-participating-company"),
    initiativeClass: byId("initiative-filter-class"),
    scope: byId("initiative-filter-scope")
  };
  let tableControls = null;
  let pager = null;
  let filterSummaryHost = null;
  let filterDrawer = null;
  try {
    tableControls = window.enterpriseTableControls?.create({
      pageKey: "initiatives",
      storageKey: "initiativesTableLayout",
      columnsButtonId: "initiative-columns-btn",
      columns: [
        { key: "initiativeId", label: "Initiative ID", defaultVisible: false },
        { key: "initiativeName", label: "Initiative", defaultVisible: true },
        { key: "parentObjectiveId", label: "Parent Objective ID", defaultVisible: false },
        { key: "parentGoalId", label: "Parent Goal ID", defaultVisible: false },
        { key: "goal", label: "Goal", defaultVisible: true },
        { key: "objective", label: "Objective", defaultVisible: true },
        { key: "owner", label: "Owner", defaultVisible: true },
        { key: "status", label: "Status", defaultVisible: true },
        { key: "type", label: "Type", defaultVisible: true },
        { key: "waveOrPhase", label: "Planning Wave / Phase", defaultVisible: true },
        { key: "priority", label: "Priority", defaultVisible: true },
        { key: "complexity", label: "Complexity", defaultVisible: true },
        { key: "contributionPlanGranularity", label: "Contribution Plan", defaultVisible: true },
        { key: "readinessStatus", label: "Readiness", defaultVisible: true },
        { key: "sponsoringCompanyId", label: "Sponsoring Company", defaultVisible: false },
        { key: "participatingCompanyIds", label: "Participating Companies", defaultVisible: false },
        { key: "maturity", label: "Maturity / Readiness", defaultVisible: false },
        { key: "startDate", label: "Start Date", defaultVisible: true },
        { key: "endDate", label: "End Date", defaultVisible: true },
        { key: "entityScope", label: "Entity Scope", defaultVisible: false },
        { key: "initiativeClass", label: "Initiative Class", defaultVisible: false },
        { key: "version", label: "Version", defaultVisible: false },
        { key: "actions", label: "Actions", defaultVisible: true }
      ],
      onChange: () => applyFilters()
    }) || null;
  } catch (err) {
    console.error("initiatives table controls init failed", err);
  }
  filterSummaryHost = document.getElementById("initiative-active-filters")
    || window.enterpriseTablePageUtils?.ensureFilterSummaryHost?.(document.getElementById("initiative-open-filters")?.parentElement || null, "initiatives");
  pager = window.enterpriseTablePageUtils?.createPager?.({
    pageKey: "initiativesTable",
    tableEl: document.getElementById("initiatives-table"),
    tableControls,
    defaultPageSize: 25,
    onChange: () => applyFilters(false)
  });
  const fallbackColumns = [
    { key: "initiativeId", label: "Initiative ID" }, { key: "initiativeName", label: "Initiative" },
    { key: "parentObjectiveId", label: "Parent Objective ID" }, { key: "parentGoalId", label: "Parent Goal ID" },
    { key: "owner", label: "Owner" }, { key: "status", label: "Status" }, { key: "type", label: "Type" },
    { key: "waveOrPhase", label: "Planning Wave / Phase" }, { key: "priority", label: "Priority" },
    { key: "complexity", label: "Complexity" }, { key: "contributionPlanGranularity", label: "Contribution Plan" }, { key: "readinessStatus", label: "Readiness" }, { key: "sponsoringCompanyId", label: "Sponsoring Company" }, { key: "participatingCompanyIds", label: "Participating Companies" }, { key: "maturity", label: "Maturity / Readiness" },
    { key: "version", label: "Version" }, { key: "actions", label: "Actions" }
  ];
  const filterLabels = {
    search: "Search",
    owner: "Owner",
    status: "Status",
    type: "Type",
    parentGoal: "Parent Goal",
    parentObjective: "Parent Objective",
    wave: "Planning Wave / Phase",
    priority: "Priority",
    complexity: "Complexity",
    sponsoringCompany: "Sponsoring Company",
    participatingCompany: "Participating Company",
    maturity: "Maturity / Readiness",
    initiativeClass: "Initiative Class",
    scope: "Entity Scope"
  };

  function updateBulkActionsState() {
    if (!bulkActionsToggle) return;
    const count = state.selectedIds.size;
    bulkActionsToggle.disabled = count === 0;
    bulkActionsToggle.textContent = count ? `Bulk Actions (${count})` : "Bulk Actions";
  }

  function getSelectedItems() {
    return state.rows.filter((row) => state.selectedIds.has(String(row.initiativeId || "")));
  }

  function clearSelection({ rerender = true } = {}) {
    state.selectedIds.clear();
    updateBulkActionsState();
    if (rerender) {
      const paged = pager?.paginate?.(state.filtered) || state.filtered;
      render(paged);
    }
  }

  function pruneSelectedIds() {
    const validIds = new Set((state.rows || []).map((row) => String(row.initiativeId || "")));
    [...state.selectedIds].forEach((id) => {
      if (!validIds.has(id)) state.selectedIds.delete(id);
    });
    updateBulkActionsState();
  }

  function ensureInputDatalist(inputId, listId, values) {
    const input = byId(inputId);
    if (!input) return;
    input.setAttribute("list", listId);
    let dl = byId(listId);
    if (!dl) {
      dl = document.createElement("datalist");
      dl.id = listId;
      input.insertAdjacentElement("afterend", dl);
    }
    workbook.fillDatalist?.(dl, values || []);
  }

  function objectiveToGoal(objectiveId) {
    return state.objectives.find((o) => o.id === objectiveId)?.parentGoalId || "";
  }

  function extractLeadingId(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const m = raw.match(/^([^—-]+)\s*[—-]\s*/);
    return m ? m[1].trim() : raw;
  }
  function parseCompanyIds(value) {
    return String(value || "").split(",").map((x) => x.trim()).filter(Boolean);
  }
  function getVal(id) {
    return byId(id)?.value?.trim() || "";
  }

  function getCell(r, key) {
    if (key === "initiativeId") return r.initiativeId || "-";
    if (key === "initiativeName") return r.initiativeName || "-";
    if (key === "parentObjectiveId") return r.parentObjectiveId || "-";
    if (key === "parentGoalId") return r.parentGoalId || "-";
    if (key === "goal") return r.goal || "-";
    if (key === "objective") return r.objective || "-";
    if (key === "owner") return resolveUserName(r.owner) || "-";
    if (key === "status") return r.status || "-";
    if (key === "type") return r.type || "-";
    if (key === "waveOrPhase") return r.waveOrPhase || "-";
    if (key === "priority") return r.priority || "-";
    if (key === "complexity") return r.complexity || "-";
    if (key === "contributionPlanGranularity") return r.contributionPlanGranularity || "-";
    if (key === "readinessStatus") return r.readinessStatus || "-";
    if (key === "sponsoringCompanyId") return workbook.companyDisplayName?.(r.sponsoringCompanyId) || r.sponsoringCompanyId || "-";
    if (key === "participatingCompanyIds") return (r.participatingCompanyIds || []).map((x) => workbook.companyDisplayName?.(x) || x).join(", ") || "-";
    if (key === "maturity") return r.maturity || "-";
    if (key === "startDate") return String(r.startDate || "").slice(0, 10) || "-";
    if (key === "endDate") return String(r.endDate || "").slice(0, 10) || "-";
    if (key === "entityScope") return r.entityScope || "-";
    if (key === "initiativeClass") return r.initiativeClass || "-";
    if (key === "version") return String(r.version ?? 0);
    if (key === "actions") return window.enterpriseRowActionsMenu?.render?.(r.initiativeId, [
      { action: "view", label: "View" },
      { action: "editStrategyLink", label: "Edit initiative" },
      { action: "openPpm", label: "Open in PPM" },
      { action: "linkToggle", label: "Open workspace" },
      { divider: true },
      { action: "exportRow", label: "Export row" }
    ]) || "";
    return "";
  }

  function getSortValue(r, key) {
    return r[key] ?? "";
  }

  function getExportValue(r, key) {
    return r[key] ?? "";
  }

  function render(rows) {
    tableBody.innerHTML = "";
    const cols = tableControls?.getVisibleColumns?.() || fallbackColumns;
    const hdr = headerRow || document.querySelector("#initiatives-table thead tr");
    const visibleIds = rows.map((row) => String(row.initiativeId || "")).filter(Boolean);
    const selectedVisibleCount = visibleIds.filter((id) => state.selectedIds.has(id)).length;
    if (hdr) {
      hdr.innerHTML = `
        <th class="es-selection-col text-center align-middle">
          <input type="checkbox" class="form-check-input m-0" id="initiatives-select-all" aria-label="Select all visible initiatives" ${visibleIds.length && selectedVisibleCount === visibleIds.length ? "checked" : ""} />
        </th>
        ${cols.map((c) => {
          if (c.key === "actions") return `<th data-col-key="${c.key}" class="text-end es-row-actions-col"><span class="es-table-head-label">${c.label}</span></th>`;
          return `<th data-col-key="${c.key}"><span class="es-col-drag-handle me-1" title="Drag to reorder">⋮⋮</span><button type="button" class="btn btn-link btn-sm p-0 text-decoration-none es-table-head-label initiative-sort" data-key="${c.key}">${c.label}${tableControls?.sortIndicator?.(c.key) || ""}</button></th>`;
        }).join("")}
      `;
    }
    rows.forEach((r) => {
      const tr = document.createElement("tr");
      tr.style.cursor = "pointer";
      const rowId = String(r.initiativeId || "");
      tr.classList.toggle("table-active", state.selected?.initiativeId === r.initiativeId);
      tr.innerHTML = `
        <td class="es-selection-col text-center align-middle">
          <input type="checkbox" class="form-check-input m-0 initiative-row-select" data-id="${rowId}" aria-label="Select ${r.initiativeName || rowId}" ${state.selectedIds.has(rowId) ? "checked" : ""} />
        </td>
        ${cols.map((c) => `<td class="${c.key === "actions" ? "text-end es-row-actions-col" : ""}">${getCell(r, c.key)}</td>`).join("")}
      `;
      tr.addEventListener("click", (event) => {
        if (event.target.closest(".initiative-row-select") || event.target.closest(".es-row-actions-col")) return;
        selectRow(r);
        render(rows);
      });
      tr.querySelectorAll(".es-row-action-item").forEach((el) => {
        el.addEventListener("click", (e) => {
          e.stopPropagation();
          const action = String(el.dataset.action || "");
          if (action === "view") {
            window.location.assign(detailUrl(r.initiativeId));
            return;
          }
          e.preventDefault();
          if (action === "editStrategyLink") {
            window.location.assign(editUrl(r.initiativeId));
            return;
          }
          if (action === "openPpm") {
            notify("Opening in PPM is not yet wired for this environment.", "warning");
            return;
          }
          if (action === "linkToggle") {
            window.location.assign(editUrl(r.initiativeId));
            return;
          }
          if (action === "exportRow") {
            window.enterpriseWorkbookIo?.exportCsv?.("initiative_row.csv", toInitiativeSheetRows([r]));
          }
        });
      });
      tr.querySelector(".initiative-row-select")?.addEventListener("click", (event) => {
        event.stopPropagation();
      });
      tr.querySelector(".initiative-row-select")?.addEventListener("change", (event) => {
        const checked = Boolean(event.target.checked);
        if (checked) state.selectedIds.add(rowId);
        else state.selectedIds.delete(rowId);
        updateBulkActionsState();
        render(rows);
      });
      tableBody.appendChild(tr);
    });
    if (!rows.length) {
      const tr = document.createElement("tr");
      tr.innerHTML = `<td colspan="${cols.length + 1}" class="text-center text-muted py-3">No initiatives found for the current filters.</td>`;
      tableBody.appendChild(tr);
    }
    const selectAll = byId("initiatives-select-all");
    if (selectAll) {
      selectAll.indeterminate = selectedVisibleCount > 0 && selectedVisibleCount < visibleIds.length;
      selectAll.addEventListener("change", (event) => {
        if (event.target.checked) visibleIds.forEach((id) => state.selectedIds.add(id));
        else visibleIds.forEach((id) => state.selectedIds.delete(id));
        updateBulkActionsState();
        render(rows);
      });
    }
    (hdr || document).querySelectorAll(".initiative-sort").forEach((btn) => btn.addEventListener("click", () => { tableControls?.cycleSort?.(btn.dataset.key); }));
    window.enterpriseTablePageUtils?.bindHeaderColumnDrag?.(hdr, {
      onReorder: (fromKey, toKey) => tableControls?.moveColumnTo?.(fromKey, toKey)
    });
  }

  function selectRow(r) {
    state.selected = r;
    detailCard.classList.remove("d-none");
    detailContent.innerHTML = `<strong>${r.initiativeId}</strong> - ${r.initiativeName}<br/>Goal: ${r.goal || r.parentGoalName || "-"}<br/>Objective: ${r.objective || r.parentObjectiveName || "-"}<br/>Status: ${r.status || "-"}<br/>Readiness: ${r.readinessStatus || "-"}<br/>Owner: ${resolveUserName(r.owner) || "-"}`;
  }

  function applyFilters(resetPage = true) {
    const q = String(filters.search.value || "").trim().toLowerCase();
    state.filtered = state.rows.filter((r) => {
      const blob = [r.initiativeId, r.initiativeName, r.goal, r.objective, r.owner, r.status, r.type, r.waveOrPhase].join(" ").toLowerCase();
      if (q && !blob.includes(q)) return false;
      if (filters.owner.value && resolveUserId(r.owner) !== filters.owner.value) return false;
      if (filters.status.value && r.status !== filters.status.value) return false;
      if (filters.type.value && r.type !== filters.type.value) return false;
      if (filters.parentGoal.value && r.parentGoalId !== filters.parentGoal.value) return false;
      if (filters.parentObjective.value && r.parentObjectiveId !== filters.parentObjective.value) return false;
      if (filters.wave.value && r.waveOrPhase !== filters.wave.value) return false;
      if (filters.priority.value && r.priority !== filters.priority.value) return false;
      if (filters.complexity.value && r.complexity !== filters.complexity.value) return false;
      if (filters.sponsoringCompany.value && String(r.sponsoringCompanyId || "").toLowerCase() !== String(filters.sponsoringCompany.value || "").toLowerCase()) return false;
      if (filters.participatingCompany.value) {
        const p = String(filters.participatingCompany.value || "").toLowerCase();
        const participants = (r.participatingCompanyIds || []).map((x) => String(x || "").toLowerCase());
        if (!participants.includes(p)) return false;
      }
      if (filters.maturity.value && r.maturity !== filters.maturity.value) return false;
      if (filters.initiativeClass.value && r.initiativeClass !== filters.initiativeClass.value) return false;
      if (filters.scope.value && !String(r.entityScope || "").toLowerCase().includes(String(filters.scope.value).toLowerCase())) return false;
      return true;
    });
    tableControls?.setFilters?.({
      search: filters.search.value,
      owner: filters.owner.value,
      status: filters.status.value,
      type: filters.type.value,
      parentGoal: filters.parentGoal.value,
      parentObjective: filters.parentObjective.value,
      wave: filters.wave.value,
      priority: filters.priority.value,
      complexity: filters.complexity.value,
      sponsoringCompany: filters.sponsoringCompany.value,
      participatingCompany: filters.participatingCompany.value,
      maturity: filters.maturity.value,
      initiativeClass: filters.initiativeClass.value,
      scope: filters.scope.value
    });
    filterDrawer?.setAppliedState(tableControls?.getFilters?.() || {});
    state.filtered = tableControls?.sortRows?.(state.filtered, getSortValue) || state.filtered;
    if (resetPage) pager?.resetToFirstPage?.();
    pruneSelectedIds();
    const paged = pager?.paginate?.(state.filtered) || state.filtered;
    render(paged);
  }

  function hydrateOptions() {
    const uniq = (arr) => [...new Set(arr.filter(Boolean))];
    workbook.fillSelect?.(filters.owner, workbook.userOptions?.() || [], { placeholder: "Owner" });
    workbook.fillSelect?.(filters.status, workbook.lifecycleStatus || state.rows.map((x) => x.status), { placeholder: "Status" });
    workbook.fillSelect?.(filters.type, workbook.initiativeTypes || workbook.goalObjectiveTypes || state.rows.map((x) => x.type), { placeholder: "Type" });
    workbook.fillSelect?.(filters.parentGoal, uniq(state.rows.map((x) => x.parentGoalId)), { placeholder: "Parent Goal" });
    workbook.fillSelect?.(filters.parentObjective, uniq(state.rows.map((x) => x.parentObjectiveId)), { placeholder: "Parent Objective" });
    workbook.fillSelect?.(filters.wave, workbook.waveValues || uniq(state.rows.map((x) => x.waveOrPhase)), { placeholder: "Planning Wave / Phase" });
    workbook.fillSelect?.(filters.priority, workbook.priorities || state.rows.map((x) => x.priority), { placeholder: "Priority" });
    workbook.fillSelect?.(filters.complexity, workbook.complexityRiskScale || state.rows.map((x) => x.complexity), { placeholder: "Complexity" });
    workbook.fillSelect?.(filters.maturity, workbook.maturityValues || state.rows.map((x) => x.maturity), { placeholder: "Maturity / Readiness" });
    workbook.fillSelect?.(filters.initiativeClass, uniq(state.rows.map((x) => x.initiativeClass)), { placeholder: "Initiative Class" });
    objectiveList.innerHTML = state.objectives.map((o) => `<option value="${o.id}"></option><option value="${o.id} — ${o.name}"></option><option value="${o.id} - ${o.name}"></option>`).join("");
    initiativeList.innerHTML = state.rows.map((i) => `<option value="${i.initiativeId}"></option><option value="${i.initiativeId} - ${i.initiativeName || ""}"></option><option value="${i.initiativeId} — ${i.initiativeName || ""}"></option>`).join("");
    workbook.fillSelect?.(byId("ppm-initiative-owner"), workbook.userOptions?.() || [], { placeholder: "Select owner" });
    ensureInputDatalist("ppm-initiative-type", "ppm-initiative-type-list", workbook.initiativeTypes || workbook.goalObjectiveTypes || uniq(state.rows.map((x) => x.type)));
    ensureInputDatalist("ppm-wave", "ppm-wave-list", workbook.waveValues || uniq(state.rows.map((x) => x.waveOrPhase)));
    ensureInputDatalist("ppm-priority", "ppm-priority-list", workbook.priorities || uniq(state.rows.map((x) => x.priority)));
    ensureInputDatalist("ppm-complexity", "ppm-complexity-list", workbook.complexityRiskScale || uniq(state.rows.map((x) => x.complexity)));
    const companyOptions = workbook.companyOptions?.() || [];
    ensureInputDatalist("ppm-sponsoring-company", "initiative-company-list", companyOptions);
    ensureInputDatalist("initiative-filter-sponsoring-company", "initiative-company-list", companyOptions);
    ensureInputDatalist("initiative-filter-participating-company", "initiative-company-list", companyOptions);
    ensureInputDatalist("ppm-entity-scope", "ppm-entity-scope-list", workbook.entityScopes || uniq(state.rows.map((x) => x.entityScope)));
    ensureInputDatalist("ppm-readiness", "ppm-readiness-list", workbook.maturityValues || uniq(state.rows.map((x) => x.maturity)));
    ensureInputDatalist("initiative-filter-scope", "initiative-filter-scope-list", workbook.entityScopes || uniq(state.rows.map((x) => x.entityScope)));
    const saved = tableControls?.getFilters?.() || {};
    Object.entries(saved).forEach(([k, v]) => { if (filters[k]) filters[k].value = v; });
    filterDrawer = window.enterpriseFilterDrawer?.create?.({
      pageKey: "initiatives",
      triggerId: "initiative-open-filters",
      drawerId: "initiativeFilterDrawer",
      applyButtonId: "initiative-apply-filters",
      cancelButtonId: "initiative-cancel-filters",
      clearButtonId: "initiative-clear-filters",
      chipHostId: "initiative-active-filters",
      fields: {
        search: filters.search,
        owner: filters.owner,
        status: filters.status,
        type: filters.type,
        parentGoal: filters.parentGoal,
        parentObjective: filters.parentObjective,
        wave: filters.wave,
        priority: filters.priority,
        complexity: filters.complexity,
        sponsoringCompany: filters.sponsoringCompany,
        participatingCompany: filters.participatingCompany,
        maturity: filters.maturity,
        initiativeClass: filters.initiativeClass,
        scope: filters.scope
      },
      labels: filterLabels,
      defaults: {
        search: "",
        owner: "",
        status: "",
        type: "",
        parentGoal: "",
        parentObjective: "",
        wave: "",
        priority: "",
        complexity: "",
        sponsoringCompany: "",
        participatingCompany: "",
        maturity: "",
        initiativeClass: "",
        scope: ""
      },
      onApply: () => applyFilters(true)
    }) || filterDrawer;
    filterDrawer?.setAppliedState(saved);
  }

  function openLinkModal(item) {
    linkErr.classList.add("d-none");
    byId("link-initiative-id").value = item?.initiativeId || "";
    byId("link-parent-objective").value = item?.parentObjectiveId || "";
    byId("link-parent-goal").value = item?.parentGoalId || "";
    byId("link-alignment-note").value = item?.notes || "";
    byId("link-contribution-weight").value = item?.contributionWeight ?? "";
    byId("link-notes").value = "";
    linkDirty = false;
    linkRequiredIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(byId(id)));
    window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, []);
    linkModal.show();
  }

  function validateLinkForm() {
    const initiativeId = extractLeadingId(byId("link-initiative-id")?.value?.trim() || "");
    const parentObjectiveId = extractLeadingId(byId("link-parent-objective")?.value?.trim() || "");
    const errors = [];
    if (!initiativeId) errors.push("Initiative search is required.");
    if (!parentObjectiveId) errors.push("Parent Objective is required.");
    return errors;
  }

  function linkFieldErrorMap() {
    const out = new Map();
    const initiativeId = extractLeadingId(byId("link-initiative-id")?.value?.trim() || "");
    const parentObjectiveId = extractLeadingId(byId("link-parent-objective")?.value?.trim() || "");
    if (!initiativeId) out.set("link-initiative-id", "Initiative search is required.");
    if (!parentObjectiveId) out.set("link-parent-objective", "Parent Objective is required.");
    return out;
  }

  function validatePpmForm() {
    const values = {
      name: byId("ppm-initiative-name")?.value?.trim() || "",
      parentObjectiveId: extractLeadingId(byId("ppm-parent-objective")?.value?.trim() || ""),
      owner: resolveUserId(byId("ppm-initiative-owner")?.value?.trim() || ""),
      type: byId("ppm-initiative-type")?.value?.trim() || "",
      wave: byId("ppm-wave")?.value?.trim() || "",
      startPeriod: byId("ppm-start-period")?.value?.trim() || "",
      endPeriod: byId("ppm-end-period")?.value?.trim() || ""
    };
    const errors = [];
    if (!values.name) errors.push("Initiative Name is required.");
    if (!values.parentObjectiveId) errors.push("Parent Objective is required.");
    if (!getVal("ppm-sponsoring-company")) errors.push("Sponsoring Company is required.");
    if (!values.owner) errors.push("Initiative Owner is required.");
    if (!values.type) errors.push("Initiative Type is required.");
    if (!values.wave) errors.push("Planning Wave / Phase is required.");
    if (!values.startPeriod) errors.push("Start Period is required.");
    if (!values.endPeriod) errors.push("End Period is required.");
    if (values.startPeriod && !periodRegex.test(values.startPeriod)) errors.push("Start Period must be in YYYY-Q# format.");
    if (values.endPeriod && !periodRegex.test(values.endPeriod)) errors.push("End Period must be in YYYY-Q# format.");
    return errors;
  }

  function ppmFieldErrorMap() {
    const out = new Map();
    const name = byId("ppm-initiative-name")?.value?.trim() || "";
    const parentObjective = extractLeadingId(byId("ppm-parent-objective")?.value?.trim() || "");
    const owner = resolveUserId(byId("ppm-initiative-owner")?.value?.trim() || "");
    const type = byId("ppm-initiative-type")?.value?.trim() || "";
    const wave = byId("ppm-wave")?.value?.trim() || "";
    const startPeriod = byId("ppm-start-period")?.value?.trim() || "";
    const endPeriod = byId("ppm-end-period")?.value?.trim() || "";
    if (!name) out.set("ppm-initiative-name", "Initiative Name is required.");
    if (!parentObjective) out.set("ppm-parent-objective", "Parent Objective is required.");
    if (!owner) out.set("ppm-initiative-owner", "Initiative Owner is required.");
    if (!type) out.set("ppm-initiative-type", "Initiative Type is required.");
    if (!getVal("ppm-sponsoring-company")) out.set("ppm-sponsoring-company", "Sponsoring Company is required.");
    if (!wave) out.set("ppm-wave", "Planning Wave / Phase is required.");
    if (!startPeriod) out.set("ppm-start-period", "Start Period is required.");
    if (!endPeriod) out.set("ppm-end-period", "End Period is required.");
    if (startPeriod && !periodRegex.test(startPeriod)) out.set("ppm-start-period", "Start Period must be in YYYY-Q# format.");
    if (endPeriod && !periodRegex.test(endPeriod)) out.set("ppm-end-period", "End Period must be in YYYY-Q# format.");
    return out;
  }

  async function createInitiativeInPpm() {
    const name = byId("ppm-initiative-name").value.trim();
    const parentObjectiveId = extractLeadingId(byId("ppm-parent-objective").value.trim());
    const parentGoalId = byId("ppm-parent-goal").value.trim() || objectiveToGoal(parentObjectiveId);
    const errors = validatePpmForm();
    const fieldMap = ppmFieldErrorMap();
    ppmRequiredIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(byId(id)));
    fieldMap.forEach((msg, id) => window.enterpriseModalFormUtils?.setFieldError?.(byId(id), msg));
    if (errors.length) {
      window.enterpriseModalFormUtils?.showValidationSummary?.(ppmErr, errors);
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(ppmModalEl);
      return;
    }
    window.enterpriseModalFormUtils?.setSubmitting?.(ppmCreateBtn, true, "Create in PPM", "Creating...");
    try {
      const id = `INI-${Date.now()}`;
      const row = {
      initiativeId: id,
      parentObjectiveId,
      parentGoalId,
      goal: parentGoalId,
      objective: parentObjectiveId,
      initiativeName: name,
      initiativeDescription: byId("ppm-initiative-description").value.trim(),
      owner: resolveUserId(byId("ppm-initiative-owner").value.trim()),
      status: "Planned",
      type: byId("ppm-initiative-type").value.trim(),
      startDate: byId("ppm-start-period").value.trim(),
      endDate: byId("ppm-end-period").value.trim(),
      waveOrPhase: byId("ppm-wave").value.trim(),
      priority: byId("ppm-priority").value.trim(),
      complexity: byId("ppm-complexity").value.trim(),
      sponsoringCompanyId: getVal("ppm-sponsoring-company"),
      participatingCompanyIds: parseCompanyIds(getVal("ppm-participating-companies")),
      dependencyIds: "",
      primaryKpi: byId("ppm-primary-kpi").value.trim(),
      baseline: "",
      target: "",
      entityScope: byId("ppm-entity-scope").value.trim(),
      budgetEnvelope: byId("ppm-budget-envelope").value.trim(),
      maturity: byId("ppm-readiness").value.trim(),
      decisionReference: "",
      evidenceReference: "",
      version: 1,
      initiativeClass: ""
      };
      state.rows.unshift(row);
      hydrateOptions();
      applyFilters();
      ppmModal.hide();
      ppmDirty = false;
      notify("Initiative created in PPM and added to strategy alignment view.");
    } catch (err) {
      window.enterpriseModalFormUtils?.showValidationSummary?.(ppmErr, window.enterpriseModalFormUtils?.backendErrors?.(err, "Create in PPM failed."));
      window.enterpriseModalFormUtils?.applyBackendFieldErrors?.(err, {
        name: byId("ppm-initiative-name"),
        parentobjectiveid: byId("ppm-parent-objective"),
        owner: byId("ppm-initiative-owner"),
        type: byId("ppm-initiative-type"),
        sponsoringcompanyid: byId("ppm-sponsoring-company"),
        waveorphase: byId("ppm-wave"),
        startperiod: byId("ppm-start-period"),
        endperiod: byId("ppm-end-period")
      });
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(ppmModalEl);
    } finally {
      window.enterpriseModalFormUtils?.setSubmitting?.(ppmCreateBtn, false, "Create in PPM");
    }
  }

  function linkExistingInitiative() {
    const initiativeId = extractLeadingId(byId("link-initiative-id").value.trim());
    const parentObjectiveId = extractLeadingId(byId("link-parent-objective").value.trim());
    const parentGoalId = byId("link-parent-goal").value.trim() || objectiveToGoal(parentObjectiveId);
    const linkErrors = validateLinkForm();
    const linkMap = linkFieldErrorMap();
    linkRequiredIds.forEach((id) => window.enterpriseModalFormUtils?.clearFieldError?.(byId(id)));
    linkMap.forEach((msg, id) => window.enterpriseModalFormUtils?.setFieldError?.(byId(id), msg));
    if (linkErrors.length) {
      window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, linkErrors);
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(linkModalEl);
      return;
    }
    const row = state.rows.find((x) => x.initiativeId === initiativeId);
    if (!row) {
      window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, ["Select an existing initiative."]);
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(linkModalEl);
      return;
    }
    row.parentObjectiveId = parentObjectiveId || row.parentObjectiveId;
    row.parentGoalId = parentGoalId || row.parentGoalId;
    row.notes = byId("link-alignment-note").value.trim();
    row.contributionWeight = byId("link-contribution-weight").value.trim();
    row.linkNotes = byId("link-notes").value.trim();
    applyFilters();
    selectRow(row);
    linkModal.hide();
    linkDirty = false;
    window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, []);
    notify("Existing initiative linked to strategy context.");
  }

  function exportCsv() {
    const rows = state.filtered.length ? state.filtered : state.rows;
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    window.enterpriseTablePageUtils?.exportVisibleCsv?.("initiatives_list.csv", rows, cols, getExportValue);
  }

  function toInitiativeSheetRows(rows) {
    return (rows || []).map((r) => ({
      "Initiative ID": r.initiativeId,
      "Parent Objective ID": r.parentObjectiveId,
      "Parent Goal ID": r.parentGoalId,
      "Goal": r.goal,
      "Objective": r.objective,
      "Initiative": r.initiativeName,
      "Initiative Description": r.initiativeDescription,
      "Initiative Owner": resolveUserName(r.owner),
      "Initiative Owner ID": resolveUserId(r.owner),
      "Initiative Status": r.status,
      "Initiative Type": r.type,
      "Start Date": r.startDate,
      "End Date": r.endDate,
      "Planning Wave / Phase": r.waveOrPhase,
      "Priority": r.priority,
      "Complexity": r.complexity,
      "Sponsoring Company": r.sponsoringCompanyId || "",
      "Participating Companies": (r.participatingCompanyIds || []).join(", "),
      "Dependency IDs": r.dependencyIds,
      "Primary KPI / Success Measure": r.primaryKpi,
      "Baseline": r.baseline,
      "Target": r.target,
      "Entity Scope": r.entityScope,
      "Budget Envelope": r.budgetEnvelope,
      "Maturity / Readiness": r.maturity,
      "Decision Ref": r.decisionReference,
      "Evidence Ref": r.evidenceReference,
      "Version": r.version,
      "Initiative Class": r.initiativeClass
    }));
  }

  function exportXlsx() {
    const rows = state.filtered.length ? state.filtered : state.rows;
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    const visibleRows = rows.map((r) => {
      const out = {};
      cols.forEach((c) => { out[c.label] = getExportValue(r, c.key); });
      return out;
    });
    window.enterpriseWorkbookIo?.exportWorkbook("initiatives_list.xlsx", { Initiatives_List: visibleRows });
  }

  async function updateSelectedStatuses(nextStatus) {
    const selected = getSelectedItems();
    if (!selected.length) {
      notify("Select one or more initiatives first.", "warning");
      return;
    }
    const confirm = await window.enterpriseStrategyUi?.confirm?.({
      title: `${nextStatus} selected initiatives?`,
      message: `Apply status ${nextStatus} to ${selected.length} selected initiative(s)?`,
      confirmLabel: nextStatus,
      confirmKind: nextStatus === "Archived" ? "danger" : "primary"
    });
    if (confirm === false) return;
    let updated = 0;
    for (const item of selected) {
      try {
        if (window.initiativeStrategyApi?.status) {
          await window.initiativeStrategyApi.status(item.initiativeId, nextStatus, item.version || 0);
        }
        item.status = nextStatus;
        updated++;
      } catch (err) {
        console.warn("initiative bulk status update failed", item.initiativeId, err);
      }
    }
    clearSelection({ rerender: false });
    await load();
    notify(`${nextStatus} applied to ${updated} initiative(s).`, updated ? "success" : "warning");
  }

  function importInitiativeRows(rows) {
    let added = 0;
    let updated = 0;
    let invalid = 0;
    (rows || []).forEach((r) => {
      const id = String(r["Initiative ID"] || "").trim();
      const name = String(r["Initiative"] || "").trim();
      if (!id || !name) {
        invalid++;
        return;
      }
      const existing = state.rows.find((x) => x.initiativeId === id);
      const mapped = {
        initiativeId: id,
        parentObjectiveId: String(r["Parent Objective ID"] || ""),
        parentGoalId: String(r["Parent Goal ID"] || ""),
        goal: String(r["Goal"] || ""),
        objective: String(r["Objective"] || ""),
        initiativeName: name,
        initiativeDescription: String(r["Initiative Description"] || ""),
        owner: resolveUserId(String(r["Initiative Owner ID"] || r["Initiative Owner"] || "")),
        status: String(r["Initiative Status"] || ""),
        type: String(r["Initiative Type"] || ""),
        startDate: String(r["Start Date"] || ""),
        endDate: String(r["End Date"] || ""),
        waveOrPhase: String(r["Planning Wave / Phase"] || ""),
        priority: String(r["Priority"] || ""),
        complexity: String(r["Complexity"] || ""),
        sponsoringCompanyId: String(r["Sponsoring Company"] || ""),
        participatingCompanyIds: parseCompanyIds(r["Participating Companies"] || ""),
        dependencyIds: String(r["Dependency IDs"] || ""),
        primaryKpi: String(r["Primary KPI / Success Measure"] || ""),
        baseline: String(r["Baseline"] || ""),
        target: String(r["Target"] || ""),
        entityScope: String(r["Entity Scope"] || ""),
        budgetEnvelope: String(r["Budget Envelope"] || ""),
        maturity: String(r["Maturity / Readiness"] || ""),
        decisionReference: String(r["Decision Ref"] || ""),
        evidenceReference: String(r["Evidence Ref"] || ""),
        version: Number(r["Version"] || 1),
        initiativeClass: String(r["Initiative Class"] || "")
      };
      if (existing) {
        Object.assign(existing, mapped);
        updated++;
      } else {
        state.rows.unshift(mapped);
        added++;
      }
    });
    hydrateOptions();
    applyFilters();
    return { added, updated, invalid };
  }

  async function load() {
    try {
      await workbook.ensureLookupsLoaded?.();
      await workbook.ensureUsersLoaded?.();
      await workbook.ensureCompaniesLoaded?.();
      const [list, objectives, goals] = await Promise.all([
        window.initiativeStrategyApi.list(),
        window.strategyObjectivesApi.list(),
        window.strategyGoalsApi.list()
      ]);
      const goalById = new Map((goals?.items || []).map((g) => [g.id, g]));
      const objectiveById = new Map((objectives?.items || []).map((o) => [o.id, o]));
      state.rows = (list?.items || []).map((x) => {
        const g = goalById.get(x.parentGoalId);
        const o = objectiveById.get(x.parentObjectiveId);
        const goalLabel = g ? `${g.id} — ${g.name || ""}` : (x.parentGoalId || "");
        const objectiveLabel = o ? `${o.id} — ${o.name || ""}` : (x.parentObjectiveId || "");
        return {
        initiativeId: x.initiativeId,
        parentObjectiveId: x.parentObjectiveId,
        parentGoalId: x.parentGoalId,
        parentObjectiveName: x.parentObjectiveName || o?.name || "",
        parentGoalName: x.parentGoalName || g?.name || "",
        goal: goalLabel,
        objective: objectiveLabel,
        initiativeName: x.initiativeName,
        initiativeDescription: x.notes || "",
        owner: resolveUserId(x.owner || ""),
        status: x.status || "",
        type: x.type || "",
        startDate: x.startDate || "",
        endDate: x.endDate || "",
        waveOrPhase: x.waveOrPhase || "",
        priority: x.priority || "",
        complexity: x.complexity || "",
        contributionPlanGranularity: x.contributionPlanGranularity || "",
        readinessStatus: x.readinessStatus || x.readiness?.readinessStatus || "",
        sponsoringCompanyId: x.sponsoringCompanyId || "",
        participatingCompanyIds: x.participatingCompanyIds || [],
        dependencyIds: x.dependencyIds || "",
        primaryKpi: x.primaryKpi || "",
        baseline: x.baseline || "",
        target: x.target || "",
        entityScope: x.entityScope || "",
        budgetEnvelope: x.budgetEnvelope || "",
        maturity: x.maturity || "",
        decisionReference: x.decisionReference || "",
        evidenceReference: x.evidenceReference || "",
        version: x.version || 1,
        initiativeClass: x.initiativeClass || ""
      };
      });
      state.objectives = objectives?.items || [];
      state.goals = goals?.items || [];
    } catch {
      state.rows = [];
    }
    hydrateOptions();
    applyFilters();
  }

  const notify = (m, k = "success") => {
    window.enterpriseStrategyUi?.notify?.(m, k);
  };

  byId("initiative-create-ppm")?.addEventListener("click", () => window.location.assign(createUrl));
  byId("initiative-link-existing")?.addEventListener("click", () => openLinkModal(state.selected));
  byId("initiative-ppm-create")?.addEventListener("click", createInitiativeInPpm);
  byId("initiative-link-confirm")?.addEventListener("click", linkExistingInitiative);
  byId("initiative-sync")?.addEventListener("click", async () => { await load(); notify("Synced from PPM."); });
  byId("initiative-export")?.addEventListener("click", exportCsv);
  byId("initiative-export-xlsx")?.addEventListener("click", exportXlsx);
  bulkExportCsvBtn?.addEventListener("click", () => {
    const selected = getSelectedItems();
    if (!selected.length) return notify("Select one or more initiatives first.", "warning");
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    window.enterpriseTablePageUtils?.exportVisibleCsv?.("initiatives_selected.csv", selected, cols, getExportValue);
  });
  bulkExportXlsxBtn?.addEventListener("click", () => {
    const selected = getSelectedItems();
    if (!selected.length) return notify("Select one or more initiatives first.", "warning");
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, fallbackColumns) || fallbackColumns.filter((c) => c.key !== "actions");
    const rows = selected.map((item) => {
      const out = {};
      cols.forEach((c) => { out[c.label] = getExportValue(item, c.key); });
      return out;
    });
    window.enterpriseWorkbookIo?.exportWorkbook?.("initiatives_selected.xlsx", { Initiatives_List: rows });
  });
  bulkExportWorkbookBtn?.addEventListener("click", () => {
    const selected = getSelectedItems();
    if (!selected.length) return notify("Select one or more initiatives first.", "warning");
    window.enterpriseWorkbookIo?.exportWorkbook?.("initiatives_selected_workbook.xlsx", { Initiatives_List: toInitiativeSheetRows(selected) });
  });
  bulkClearSelectionBtn?.addEventListener("click", () => clearSelection());
  bulkActivateBtn?.addEventListener("click", async () => { await updateSelectedStatuses("Active"); });
  bulkArchiveBtn?.addEventListener("click", async () => { await updateSelectedStatuses("Archived"); });
  byId("initiative-export-workbook")?.addEventListener("click", async () => {
    try {
      if (!window.enterpriseWorkbookIo?.buildAllSheets || !window.enterpriseWorkbookIo?.exportWorkbook) {
        notify("Workbook export engine not loaded. Please hard refresh and retry.", "error");
        return;
      }
      const sheets = await window.enterpriseWorkbookIo.buildAllSheets();
      window.enterpriseWorkbookIo.exportWorkbook("enterprise_strategy_workbook.xlsx", sheets || {});
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Workbook export failed") || "Workbook export failed", "error");
    }
  });
  importFileInput?.addEventListener("change", async () => {
    const file = importFileInput.files?.[0];
    if (!file) return;
    try {
      if (!window.enterpriseWorkbookIo?.parseFile) {
        notify("Import engine not loaded. Please hard refresh and retry.", "error");
        return;
      }
      const parsed = await window.enterpriseWorkbookIo.parseFile(file);
      const res = importInitiativeRows(parsed?.rows || []);
      notify(`Initiatives import complete. Added ${res.added}, updated ${res.updated}, invalid ${res.invalid}.`);
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Import failed") || "Import failed", "error");
    } finally {
      importFileInput.value = "";
    }
  });
  importWorkbookInput?.addEventListener("change", async () => {
    const file = importWorkbookInput.files?.[0];
    if (!file) return;
    try {
      if (!window.enterpriseWorkbookIo?.parseFile) {
        notify("Workbook import engine not loaded. Please hard refresh and retry.", "error");
        return;
      }
      const parsed = await window.enterpriseWorkbookIo.parseFile(file);
      const res = importInitiativeRows(parsed?.sheets?.Initiatives_List || []);
      notify(`Workbook initiatives import complete. Added ${res.added}, updated ${res.updated}, invalid ${res.invalid}.`);
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Workbook import failed") || "Workbook import failed", "error");
    } finally {
      importWorkbookInput.value = "";
    }
  });
  importPageActionBtn?.addEventListener("click", () => importFileInput?.click());
  importWorkbookActionBtn?.addEventListener("click", () => importWorkbookInput?.click());
  byId("ppm-parent-objective")?.addEventListener("change", () => { byId("ppm-parent-goal").value = objectiveToGoal(extractLeadingId(byId("ppm-parent-objective").value.trim())); });
  byId("link-parent-objective")?.addEventListener("change", () => { byId("link-parent-goal").value = objectiveToGoal(extractLeadingId(byId("link-parent-objective").value.trim())); });
  detailLinkBtn?.addEventListener("click", () => {
    if (!state.selected?.initiativeId) return;
    window.location.assign(detailUrl(state.selected.initiativeId));
  });

  ppmModalEl?.querySelectorAll("input,select,textarea").forEach((el) => {
    el.addEventListener("input", () => {
      ppmDirty = true;
      window.enterpriseModalFormUtils?.showValidationSummary?.(ppmErr, []);
      if (ppmRequiredIds.includes(el.id)) {
        const map = ppmFieldErrorMap();
        window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(el.id) || "");
      }
    });
    el.addEventListener("change", () => {
      ppmDirty = true;
      window.enterpriseModalFormUtils?.showValidationSummary?.(ppmErr, []);
      if (ppmRequiredIds.includes(el.id)) {
        const map = ppmFieldErrorMap();
        window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(el.id) || "");
      }
    });
  });
  linkModalEl?.querySelectorAll("input,select,textarea").forEach((el) => {
    el.addEventListener("input", () => {
      linkDirty = true;
      window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, []);
      if (linkRequiredIds.includes(el.id)) {
        const map = linkFieldErrorMap();
        window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(el.id) || "");
      }
    });
    el.addEventListener("change", () => {
      linkDirty = true;
      window.enterpriseModalFormUtils?.showValidationSummary?.(linkErr, []);
      if (linkRequiredIds.includes(el.id)) {
        const map = linkFieldErrorMap();
        window.enterpriseModalFormUtils?.setFieldError?.(el, map.get(el.id) || "");
      }
    });
  });
  window.enterpriseModalFormUtils?.bindDirtyCloseGuard?.(ppmModalEl, () => ppmDirty);
  window.enterpriseModalFormUtils?.bindDirtyCloseGuard?.(linkModalEl, () => linkDirty);
  window.enterpriseModalFormUtils?.blockEnterSubmit?.(ppmModalEl);
  window.enterpriseModalFormUtils?.blockEnterSubmit?.(linkModalEl);

  load().catch(() => {});
})(window, document);
