(function (window, document) {
  "use strict";

  const YEARS = Array.from({ length: 20 }, (_, i) => 2027 + i);
  const yearRegex = /^\d{4}$/;
  const workbook = window.enterpriseWorkbookOptions || {};
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();

  const state = {
    rows: [],
    filtered: [],
    goals: [],
    objectives: [],
    initiatives: [],
    projects: [],
    selectedRows: new Set(),
    showYears: false,
    page: 1,
    pageSize: 15,
    currentEdit: null
  };

  const viewHosts = {
    register: document.getElementById("connections-register-view"),
    tree: document.getElementById("connections-tree-view"),
    graph: document.getElementById("connections-graph-view"),
    matrix: document.getElementById("connections-matrix-view"),
    coverage: document.getElementById("connections-coverage-view")
  };

  const tableEls = {
    header: document.getElementById("connection-register-header"),
    body: document.getElementById("connection-register-body"),
    state: document.getElementById("connection-register-state"),
    pageLabel: document.getElementById("conn-page-label"),
    prev: document.getElementById("conn-prev-page"),
    next: document.getElementById("conn-next-page"),
    toggleYears: document.getElementById("conn-toggle-years")
  };

  const filters = {
    search: document.getElementById("conn-search"),
    goal: document.getElementById("conn-filter-goal"),
    objective: document.getElementById("conn-filter-objective"),
    initiative: document.getElementById("conn-filter-initiative"),
    project: document.getElementById("conn-filter-project"),
    owner: document.getElementById("conn-filter-owner"),
    company: document.getElementById("conn-filter-company"),
    companyScopeMode: document.getElementById("conn-filter-company-scope-mode"),
    agg: document.getElementById("conn-filter-agg"),
    baselineYear: document.getElementById("conn-filter-baseline-year"),
    targetYear: document.getElementById("conn-filter-target-year"),
    missingProject: document.getElementById("conn-filter-missing-project"),
    missingTarget: document.getElementById("conn-filter-missing-target"),
    missingPlan: document.getElementById("conn-filter-missing-plan"),
    companySpecificOnly: document.getElementById("conn-filter-company-specific-only"),
    apply: document.getElementById("conn-apply-filters"),
    clear: document.getElementById("conn-clear-filters")
  };
  const filterSummaryHost = window.enterpriseTablePageUtils?.ensureFilterSummaryHost?.(filters.apply?.parentElement || null, "connections");

  const kpiEls = {
    total: document.getElementById("kpi-total-rows"),
    projMapped: document.getElementById("kpi-project-mapped"),
    projMissing: document.getElementById("kpi-project-missing"),
    initMetricMissing: document.getElementById("kpi-initiative-metric-missing"),
    targetMissing: document.getElementById("kpi-target-missing"),
    planMissing: document.getElementById("kpi-plan-missing")
  };

  const modalEl = document.getElementById("connectionEditorModal");
  const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
  const formError = document.getElementById("connection-form-error");
  const saveBtn = document.getElementById("connection-save");
  const form = {
    id: document.getElementById("connection-id"),
    goalId: document.getElementById("connection-goal-id"),
    goalName: document.getElementById("connection-goal-name"),
    goalMetric: document.getElementById("connection-goal-metric"),
    goalMetricType: document.getElementById("connection-goal-metric-type"),
    objective: document.getElementById("connection-objective"),
    objectiveMetric: document.getElementById("connection-objective-metric"),
    objectiveMetricType: document.getElementById("connection-objective-metric-type"),
    initiativeId: document.getElementById("connection-initiative-id"),
    initiativeName: document.getElementById("connection-initiative-name"),
    initiativeMetric: document.getElementById("connection-initiative-metric"),
    initiativeMetricType: document.getElementById("connection-initiative-metric-type"),
    projectId: document.getElementById("connection-project-id"),
    projectName: document.getElementById("connection-project-name"),
    projectMetric: document.getElementById("connection-project-metric"),
    projectMetricType: document.getElementById("connection-project-metric-type"),
    owner: document.getElementById("connection-metric-owner"),
    agg: document.getElementById("connection-aggregation-method"),
    baselineYear: document.getElementById("connection-baseline-year"),
    baselineValue: document.getElementById("connection-baseline-value"),
    targetYear: document.getElementById("connection-target-year"),
    targetValue: document.getElementById("connection-target-value"),
    entryNotes: document.getElementById("connection-entry-notes"),
    decisionRef: document.getElementById("connection-decision-reference"),
    evidenceRef: document.getElementById("connection-evidence-reference"),
    version: document.getElementById("connection-version"),
    planMethod: document.getElementById("connection-plan-method"),
    planMethodHelp: document.getElementById("connection-plan-method-help"),
    planSummary: document.getElementById("connection-plan-summary"),
    generatePlanBtn: document.getElementById("connection-generate-plan"),
    companyScopeMode: document.getElementById("connection-company-scope-mode"),
    companyId: document.getElementById("connection-company-id"),
    goalList: document.getElementById("connection-goal-id-list"),
    objectiveList: document.getElementById("connection-objective-list"),
    initiativeList: document.getElementById("connection-initiative-id-list"),
    projectList: document.getElementById("connection-project-id-list"),
    planGrid: document.getElementById("connection-plan-years-grid")
  };

  const addRowBtn = document.getElementById("conn-add-row");
  const importFileInput = document.getElementById("conn-import-file");
  const importWorkbookInput = document.getElementById("conn-import-workbook-file");
  const importPageActionBtn = document.getElementById("conn-data-import-page");
  const importWorkbookActionBtn = document.getElementById("conn-data-import-workbook");
  const exportCsvBtn = document.getElementById("conn-export-csv");
  const exportXlsxBtn = document.getElementById("conn-export-xlsx");
  const exportWorkbookBtn = document.getElementById("conn-export-workbook");
  const modalPlanState = { lastGenerated: null, manualEdits: false };
  let formDirty = false;
  let hasSubmitAttempt = false;

  const baseCols = [
    { key: "goalId", label: "Goal ID", defaultVisible: false },
    { key: "goal", label: "Goal", defaultVisible: true },
    { key: "goalMetric", label: "Goal Metric", defaultVisible: true },
    { key: "objective", label: "Objective", defaultVisible: true },
    { key: "objectiveMetric", label: "Objective Metric", defaultVisible: true },
    { key: "initiativeId", label: "Initiative ID", defaultVisible: false },
    { key: "initiative", label: "Initiative", defaultVisible: true },
    { key: "initiativeMetric", label: "Initiative Metric", defaultVisible: true },
    { key: "projectId", label: "Project ID", defaultVisible: false },
    { key: "project", label: "Project", defaultVisible: true },
    { key: "projectMetric", label: "Project Metric", defaultVisible: true },
    { key: "metricOwner", label: "Metric Owner", defaultVisible: false },
    { key: "aggregationMethod", label: "Aggregation Method", defaultVisible: true },
    { key: "baselineYear", label: "Baseline Year", defaultVisible: true },
    { key: "baselineValue", label: "Baseline Value", defaultVisible: true },
    { key: "targetYear", label: "Target Year", defaultVisible: true },
    { key: "targetValue", label: "Target Value", defaultVisible: true },
    { key: "companyScopeMode", label: "Company Scope Mode", defaultVisible: false },
    { key: "companyId", label: "Company", defaultVisible: false },
    { key: "entryNotes", label: "Entry Notes", defaultVisible: false }
  ];
  const yearCols = YEARS.map((y) => ({ key: `y${y}`, label: String(y), defaultVisible: false }));
  const fallbackCols = [...baseCols, { key: "actions", label: "Actions" }];
  let tableControls = null;
  try {
    tableControls = window.enterpriseTableControls?.create({
      pageKey: "connections",
      storageKey: "connectionsTableLayout",
      columnsButtonId: "conn-columns-btn",
      lockYearBlock: true,
      yearKeys: yearCols.map((c) => c.key),
      columns: [...baseCols, ...yearCols, { key: "actions", label: "Actions", defaultVisible: true }],
      onChange: () => renderRegister()
    }) || null;
  } catch (err) {
    console.error("connections table controls init failed", err);
  }
  state.pageSize = Number(tableControls?.getPageSize?.() || state.pageSize || 15);

  const notify = (m, k = "success") => {
    window.enterpriseStrategyUi?.notify?.(m, k);
  };

  const rowToCsvObject = (r) => {
    const annual = r.annualPlanValues || r.yearlyValues || {};
    const out = {
      "Goal ID": r.goalId,
      Goal: r.goal,
      "Goal Metric": r.goalMetric,
      "Goal Metric Type": r.goalMetricType,
      Objective: r.objective,
      "Objective Metric": r.objectiveMetric,
      "Objective Metric Type": r.objectiveMetricType,
      "Initiative ID": r.initiativeId,
      Initiative: r.initiative,
      "Initiative Metric": r.initiativeMetric,
      "Initiative Metric Type": r.initiativeMetricType,
      "Project ID": r.projectId,
      Project: r.project,
      "Project Metric": r.projectMetric,
      "Project Metric Type": r.projectMetricType,
      "Metric Owner": resolveUserName(r.metricOwner),
      "Metric Owner ID": resolveUserId(r.metricOwner),
      "Aggregation Method": r.aggregationMethod,
      "Baseline Year": r.baselineYear,
      "Baseline Value": r.baselineValue,
      "Target Year": r.targetYear,
      "Target Value": r.targetValue,
      "Company Scope Mode": r.companyScopeMode || "Derived",
      Company: r.companyId || "",
      "Entry Notes": r.entryNotes
    };
    YEARS.forEach((y) => { out[String(y)] = annual[String(y)] ?? ""; });
    return out;
  };

  function connectionCellValue(row, key) {
    if (key.startsWith("y")) return (row.annualPlanValues || {})[key.slice(1)] ?? "";
    if (key === "metricOwner") return resolveUserName(row.metricOwner);
    return row[key] ?? "";
  }

  function toVisibleExportRows(rows) {
    const cols = window.enterpriseTablePageUtils?.visibleExportColumns?.(tableControls, [...baseCols, ...yearCols, { key: "actions", label: "Actions" }]) ||
      [...baseCols, ...yearCols];
    return rows.map((row) => {
      const out = {};
      cols.forEach((c) => { out[c.label] = connectionCellValue(row, c.key); });
      return out;
    });
  }

  const compositeKey = (r) => [
    r.goalId, r.goalMetric, r.objective, r.initiativeId, r.initiativeMetric, r.projectId, r.projectMetric, r.baselineYear, r.targetYear
  ].map((v) => String(v || "").trim().toLowerCase()).join("|");

  const mapToConnectionMapRow = (edge) => {
    const goal = state.goals.find((g) => g.id === edge.fromId || g.id === edge.toId);
    const objective = state.objectives.find((o) => o.id === edge.fromId || o.id === edge.toId);
    const initiative = state.initiatives.find((i) => i.initiativeId === edge.fromId || i.initiativeId === edge.toId);
    const project = state.projects.find((p) => p.projectId === edge.fromId || p.projectId === edge.toId);
    const meta = safeJson(edge.metricBindingsJson, {});
    const notesMeta = safeJson(edge.evidenceReferencesJson, {});
    const annualPlanValues = {};
    YEARS.forEach((y) => { annualPlanValues[String(y)] = meta[String(y)] ?? ""; });
    return {
      rowKey: edge.id,
      sourceConnectionId: edge.id,
      goalId: goal?.id || meta.goalId || "",
      goal: goal?.name || meta.goal || "",
      goalMetric: meta.goalMetric || "",
      goalMetricType: meta.goalMetricType || "",
      objective: objective?.name || meta.objective || "",
      objectiveMetric: meta.objectiveMetric || "",
      objectiveMetricType: meta.objectiveMetricType || "",
      initiativeId: initiative?.initiativeId || meta.initiativeId || "",
      initiative: initiative?.initiativeName || meta.initiative || "",
      initiativeMetric: meta.initiativeMetric || "",
      initiativeMetricType: meta.initiativeMetricType || "",
      projectId: project?.projectId || meta.projectId || "",
      project: project?.projectName || meta.project || "",
      projectMetric: meta.projectMetric || "",
      projectMetricType: meta.projectMetricType || "",
      metricOwner: resolveUserId(meta.metricOwner || ""),
      aggregationMethod: meta.aggregationMethod || "",
      companyScopeMode: edge.companyScopeMode || meta.companyScopeMode || "Derived",
      companyId: edge.companyId || meta.companyId || "",
      baselineYear: meta.baselineYear || "",
      baselineValue: meta.baselineValue ?? "",
      annualPlanValues,
      targetYear: meta.targetYear || "",
      targetValue: meta.targetValue ?? "",
      entryNotes: notesMeta.entryNotes || "",
      decisionRef: safeJson(edge.decisionReferencesJson, {}).decisionRef || "",
      evidenceRef: notesMeta.evidenceRef || "",
      version: edge.version || 0
    };
  };

  function safeJson(v, fallback) {
    try { return v ? JSON.parse(v) : fallback; } catch { return fallback; }
  }

  function setView(name) {
    Object.entries(viewHosts).forEach(([k, v]) => v?.classList.toggle("d-none", k !== name));
    document.querySelectorAll(".connection-view-btn").forEach((btn) => btn.classList.toggle("active", btn.dataset.view === name));
  }

  function applyFilters() {
    const q = String(filters.search.value || "").trim().toLowerCase();
    const g = String(filters.goal.value || "").trim().toLowerCase();
    const o = String(filters.objective.value || "").trim().toLowerCase();
    const i = String(filters.initiative.value || "").trim().toLowerCase();
    const p = String(filters.project.value || "").trim().toLowerCase();
    const owner = String(filters.owner.value || "").trim();
    const company = String(filters.company.value || "").trim().toLowerCase();
    const companyScopeMode = String(filters.companyScopeMode.value || "").trim().toLowerCase();
    const agg = String(filters.agg.value || "").trim().toLowerCase();
    const by = String(filters.baselineYear.value || "").trim();
    const ty = String(filters.targetYear.value || "").trim();
    const onlyMissingProject = Boolean(filters.missingProject?.checked);
    const onlyMissingTarget = Boolean(filters.missingTarget?.checked);
    const onlyMissingPlan = Boolean(filters.missingPlan?.checked);
    const companySpecificOnly = Boolean(filters.companySpecificOnly?.checked);

    state.filtered = state.rows.filter((r) => {
      const searchBlob = [r.goalId, r.goal, r.goalMetric, r.objective, r.objectiveMetric, r.initiativeId, r.initiative, r.initiativeMetric, r.projectId, r.project, r.projectMetric].join(" ").toLowerCase();
      if (q && !searchBlob.includes(q)) return false;
      if (g && ![r.goalId, r.goal].join(" ").toLowerCase().includes(g)) return false;
      if (o && ![r.objective, r.objectiveMetric].join(" ").toLowerCase().includes(o)) return false;
      if (i && ![r.initiativeId, r.initiative].join(" ").toLowerCase().includes(i)) return false;
      if (p && ![r.projectId, r.project].join(" ").toLowerCase().includes(p)) return false;
      if (owner && resolveUserId(r.metricOwner) !== owner) return false;
      if (company && !String(r.companyId || "").toLowerCase().includes(company)) return false;
      if (companyScopeMode && String(r.companyScopeMode || "").toLowerCase() !== companyScopeMode) return false;
      if (agg && !String(r.aggregationMethod || "").toLowerCase().includes(agg)) return false;
      if (by && String(r.baselineYear || "") !== by) return false;
      if (ty && String(r.targetYear || "") !== ty) return false;
      if (onlyMissingProject && String(r.projectId || "").trim() !== "") return false;
      if (onlyMissingTarget && String(r.targetValue || "").trim() !== "") return false;
      if (onlyMissingPlan) {
        const annual = r.annualPlanValues || r.yearlyValues || {};
        if (!YEARS.every((y) => (annual[String(y)] ?? "") === "")) return false;
      }
      if (companySpecificOnly && String(r.companyScopeMode || "Derived") !== "Explicit") return false;
      return true;
    });
    tableControls?.setFilters?.({
      search: filters.search.value,
      goal: filters.goal.value,
      objective: filters.objective.value,
      initiative: filters.initiative.value,
      project: filters.project.value,
      owner: filters.owner.value,
      company: filters.company.value,
      companyScopeMode: filters.companyScopeMode.value,
      agg: filters.agg.value,
      baselineYear: filters.baselineYear.value,
      targetYear: filters.targetYear.value,
      missingProject: !!filters.missingProject?.checked,
      missingTarget: !!filters.missingTarget?.checked,
      missingPlan: !!filters.missingPlan?.checked,
      companySpecificOnly: !!filters.companySpecificOnly?.checked
    });
    window.enterpriseTablePageUtils?.renderFilterSummary?.(filterSummaryHost, tableControls?.getFilters?.() || {});
    state.page = 1;
    state.filtered = tableControls?.sortRows?.(state.filtered, (row, key) => {
      if (key.startsWith("y")) return Number((row.annualPlanValues || {})[key.slice(1)] || 0);
      return row[key] ?? "";
    }) || state.filtered;
    renderRegister();
    renderKpis();
  }

  function renderKpis() {
    const rows = state.filtered;
    const withProject = rows.filter((r) => r.projectId).length;
    const missingProject = rows.length - withProject;
    const missingInitMetric = rows.filter((r) => !r.initiativeMetric).length;
    const missingTarget = rows.filter((r) => r.targetValue === "" || r.targetValue === null || r.targetValue === undefined).length;
    const missingPlan = rows.filter((r) => {
      const annual = r.annualPlanValues || r.yearlyValues || {};
      return YEARS.every((y) => (annual[String(y)] ?? "") === "");
    }).length;
    kpiEls.total.textContent = String(rows.length);
    kpiEls.projMapped.textContent = String(withProject);
    kpiEls.projMissing.textContent = String(missingProject);
    kpiEls.initMetricMissing.textContent = String(missingInitMetric);
    kpiEls.targetMissing.textContent = String(missingTarget);
    kpiEls.planMissing.textContent = String(missingPlan);
  }

  function openRelated(type, id) {
    if (!id) return;
    const base = "/management-governance/enterprise-strategy-business-performance";
    const map = { goal: "goals", initiative: "initiatives", project: "projects" };
    const seg = map[type];
    if (!seg) return;
    window.location.href = `${base}/${seg}/${encodeURIComponent(id)}`;
  }

  function renderRegister() {
    const pageCount = Math.max(1, Math.ceil(state.filtered.length / state.pageSize));
    state.page = Math.min(state.page, pageCount);
    const start = (state.page - 1) * state.pageSize;
    const pageRows = state.filtered.slice(start, start + state.pageSize);

    const cols = tableControls?.getVisibleColumns?.() || (state.showYears ? [...baseCols, ...yearCols, { key: "actions", label: "Actions" }] : fallbackCols);
    tableEls.header.innerHTML = `<th><input type="checkbox" id="conn-select-all" /></th>` + cols.map((c) => {
      if (c.key === "actions") return `<th data-col-key="${c.key}" class="text-end es-row-actions-col"><span class="es-table-head-label">${c.label}</span></th>`;
      return `<th data-col-key="${c.key}"><span class="es-col-drag-handle me-1" title="Drag to reorder">⋮⋮</span><button type="button" class="btn btn-link btn-sm p-0 text-decoration-none es-table-head-label conn-sort" data-key="${c.key}">${c.label}${tableControls?.sortIndicator?.(c.key) || ""}</button></th>`;
    }).join("");

    tableEls.body.innerHTML = pageRows.map((r) => {
      const annual = r.annualPlanValues || r.yearlyValues || {};
      const cells = cols.map((c) => {
        if (c.key === "actions") {
          return `<td class="text-end es-row-actions-col">${window.enterpriseRowActionsMenu?.render?.(r.rowKey, [
            { action: "view", label: "View" },
            { action: "edit", label: "Edit row" },
            { action: "duplicate", label: "Duplicate row" },
            { action: "delete", label: "Delete row" },
            { divider: true },
            { action: "openGoal", label: "Open Goal" },
            { action: "openInitiative", label: "Open Initiative" },
            { action: "openProject", label: "Open Project" },
            { divider: true },
            { action: "exportRow", label: "Export row" }
          ]) || ""}</td>`;
        }
        if (c.key.startsWith("y")) return `<td>${annual[c.key.slice(1)] ?? ""}</td>`;
        return `<td title="${escapeHtml(connectionCellValue(r, c.key))}">${escapeHtml(connectionCellValue(r, c.key))}</td>`;
      }).join("");
      return `<tr data-key="${r.rowKey}">
        <td><input type="checkbox" class="conn-select-row" data-key="${r.rowKey}" ${state.selectedRows.has(r.rowKey) ? "checked" : ""} /></td>
        ${cells}
      </tr>`;
    }).join("");

    tableEls.pageLabel.textContent = `Page ${state.page} / ${pageCount}`;
    tableEls.state.textContent = state.filtered.length ? `${state.filtered.length} rows` : "No rows";
    ensurePageSizeControl();

    document.getElementById("conn-select-all")?.addEventListener("change", (e) => {
      pageRows.forEach((r) => {
        if (e.target.checked) state.selectedRows.add(r.rowKey); else state.selectedRows.delete(r.rowKey);
      });
      renderRegister();
    });
    tableEls.body.querySelectorAll(".conn-select-row").forEach((c) => c.addEventListener("change", () => {
      if (c.checked) state.selectedRows.add(c.dataset.key); else state.selectedRows.delete(c.dataset.key);
    }));
    tableEls.body.querySelectorAll(".es-row-action-item").forEach((el) => el.addEventListener("click", (e) => {
      const rowKey = String(el.dataset.rowId || "");
      const action = String(el.dataset.action || "");
      const row = state.rows.find((x) => x.rowKey === rowKey);
      if (!row) return;
      e.preventDefault();
      if (action === "view") return openModal(row, true);
      if (action === "edit") return openModal(row, false);
      if (action === "duplicate") return duplicateRow(rowKey);
      if (action === "delete") return deleteRow(rowKey);
      if (action === "openGoal") return openRelated("goal", row.goalId);
      if (action === "openInitiative") return openRelated("initiative", row.initiativeId);
      if (action === "openProject") return openRelated("project", row.projectId);
      if (action === "exportRow") return window.enterpriseWorkbookIo?.exportCsv?.("connection_row.csv", [rowToCsvObject(row)]);
    }));
    tableEls.header.querySelectorAll(".conn-sort").forEach((b) => b.addEventListener("click", () => tableControls?.cycleSort?.(b.dataset.key)));
    window.enterpriseTablePageUtils?.bindHeaderColumnDrag?.(tableEls.header, {
      onReorder: (fromKey, toKey) => tableControls?.moveColumnTo?.(fromKey, toKey)
    });
  }

  function ensurePageSizeControl() {
    if (!tableEls.pageLabel?.parentElement) return;
    if (document.getElementById("conn-page-size")) return;
    const wrap = document.createElement("span");
    wrap.className = "d-inline-flex align-items-center gap-1";
    wrap.innerHTML = `<label class="small text-muted mb-0" for="conn-page-size">Rows</label>
      <select id="conn-page-size" class="form-select form-select-sm" style="width:auto">
        <option value="15">15</option><option value="25">25</option><option value="50">50</option><option value="100">100</option>
      </select>`;
    tableEls.pageLabel.parentElement.prepend(wrap);
    const select = document.getElementById("conn-page-size");
    select.value = String(state.pageSize || 15);
    select.addEventListener("change", () => {
      state.pageSize = Number(select.value || 15);
      tableControls?.setPageSize?.(state.pageSize);
      state.page = 1;
      renderRegister();
    });
  }

  function escapeHtml(v) {
    return String(v ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }

  function renderSecondaryViews(listData, treeData, graphData, matrixData, coverageData) {
    const items = listData?.items || [];
    const treeNodes = treeData?.items || [];
    const graphNodes = graphData?.nodes || [];
    const graphEdges = graphData?.edges || [];
    const matrixCells = matrixData?.items || [];
    const coverageRows = coverageData?.items || [];

    viewHosts.tree.innerHTML = `<div class="card"><div class="card-body">
      <div class="small text-muted mb-2">Tree (secondary): ${treeNodes.length} root nodes</div>
      ${treeNodes.slice(0, 8).map((n) => `<div class="border rounded p-2 mb-1"><strong>${escapeHtml(n.name || n.id)}</strong><div class="small text-muted">${escapeHtml(n.type || "-")} | children: ${(n.children || []).length}</div></div>`).join("") || '<div class="small text-muted">No tree nodes.</div>'}
    </div></div>`;
    viewHosts.graph.innerHTML = `<div class="card"><div class="card-body">
      <div class="small text-muted mb-2">Graph (secondary): ${graphNodes.length} nodes / ${graphEdges.length} edges</div>
      ${graphEdges.slice(0, 12).map((e) => `<div class="border rounded p-2 mb-1"><strong>${escapeHtml(e.fromId)}</strong> -> <strong>${escapeHtml(e.toId)}</strong><div class="small text-muted">status: ${escapeHtml(e.status || "-")}</div></div>`).join("") || `<div class="small text-muted">No graph edges (links: ${items.length}).</div>`}
    </div></div>`;
    viewHosts.matrix.innerHTML = `<div class="card"><div class="card-body">
      <div class="small text-muted mb-2">Matrix (secondary): ${matrixCells.length} cells</div>
      ${matrixCells.slice(0, 12).map((c) => `<div class="border rounded p-2 mb-1">${escapeHtml(c.rowId)} -> ${escapeHtml(c.columnId)} <span class="small text-muted">(${escapeHtml(c.state)})</span></div>`).join("") || '<div class="small text-muted">No matrix cells.</div>'}
    </div></div>`;
    viewHosts.coverage.innerHTML = `<div class="card"><div class="card-body">
      <div class="small text-muted mb-2">Coverage (secondary): ${coverageRows.length} gaps</div>
      ${coverageRows.slice(0, 12).map((g) => `<div class="border rounded p-2 mb-1"><strong>${escapeHtml(g.gapType)}</strong> - ${escapeHtml(g.entityId)}<div class="small text-muted">${escapeHtml(g.message || "")}</div></div>`).join("") || '<div class="small text-muted">No coverage gaps.</div>'}
    </div></div>`;
  }

  function planYearInputs() {
    form.planGrid.innerHTML = YEARS.map((y) => (
      `<div class="col-6 col-md-3 conn-plan-cell" data-year-wrap="${y}">
        <label class="form-label small mb-1">${y}</label>
        <input class="form-control form-control-sm conn-plan-year" data-year="${y}" type="text" inputmode="decimal" />
      </div>`
    )).join("");
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((input) => {
      input.addEventListener("input", () => {
        if (modalPlanState.lastGenerated) {
          const year = input.dataset.year;
          if (String(input.value || "") !== String(modalPlanState.lastGenerated[year] ?? "")) {
            modalPlanState.manualEdits = true;
          }
        }
        validateYearInputs();
        updatePlanSummary();
      });
    });
  }

  function annualInputsMap() {
    const out = {};
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((i) => { out[i.dataset.year] = i; });
    return out;
  }

  function anchorsValid() {
    return yearRegex.test(form.baselineYear.value.trim()) && yearRegex.test(form.targetYear.value.trim()) &&
      Number(form.targetYear.value) >= Number(form.baselineYear.value);
  }

  function inActiveRange(year) {
    if (!anchorsValid()) return false;
    const by = Number(form.baselineYear.value);
    const ty = Number(form.targetYear.value);
    return year >= by && year <= ty;
  }

  function updatePlanMethodHelp() {
    const help = {
      "Manual": "Manual: enter values directly by year.",
      "Linear": "Linear: evenly interpolates values between baseline and target.",
      "CAGR / Compound": "CAGR / Compound: compounds growth to reach target value.",
      "Front-loaded": "Front-loaded: larger movement in early years then taper.",
      "Back-loaded": "Back-loaded: smaller movement in early years, larger later."
    };
    form.planMethodHelp.textContent = help[form.planMethod.value] || help.Manual;
  }

  function updatePlanInputStyles() {
    const wrappers = form.planGrid.querySelectorAll("[data-year-wrap]");
    wrappers.forEach((w) => {
      const y = Number(w.dataset.yearWrap);
      w.classList.remove("conn-plan-cell-active", "conn-plan-cell-dim");
      if (anchorsValid()) {
        if (inActiveRange(y)) w.classList.add("conn-plan-cell-active");
        else w.classList.add("conn-plan-cell-dim");
      }
    });
  }

  function validateYearInputs() {
    let hasBad = false;
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((i) => {
      const v = String(i.value || "").trim();
      const ok = v === "" || !Number.isNaN(Number(v));
      i.classList.toggle("is-invalid", !ok);
      if (!ok) hasBad = true;
    });
    return !hasBad;
  }

  async function generateAnnualTargets() {
    if (!anchorsValid()) return;
    const method = form.planMethod.value;
    if (method === "Manual") return;

    const baseline = Number(form.baselineValue.value);
    const target = Number(form.targetValue.value);
    if (Number.isNaN(baseline) || Number.isNaN(target)) {
      setFormErrors(["Baseline Value and Target Value are required for auto generation methods."]);
      return;
    }

    if (modalPlanState.manualEdits) {
      const ok = await (window.enterpriseStrategyUi?.confirm?.({
        title: "Overwrite annual values?",
        message: "Manual edits exist. Generate will overwrite the current annual schedule.",
        confirmLabel: "Overwrite",
        confirmKind: "danger"
      }) || Promise.resolve(false));
      if (!ok) return;
    }

    const by = Number(form.baselineYear.value);
    const ty = Number(form.targetYear.value);
    const span = Math.max(1, ty - by);
    const inputs = annualInputsMap();
    const generated = {};

    for (let y = by; y <= ty; y++) {
      const i = y - by;
      const p = i / span;
      let val = baseline;
      if (method === "Linear") {
        val = baseline + (target - baseline) * p;
      } else if (method === "CAGR / Compound") {
        if (baseline > 0 && target > 0) {
          val = baseline * Math.pow(target / baseline, p);
        } else {
          val = baseline + (target - baseline) * p;
        }
      } else if (method === "Front-loaded") {
        val = baseline + (target - baseline) * Math.sqrt(p);
      } else if (method === "Back-loaded") {
        val = baseline + (target - baseline) * (p * p);
      }
      if (y === ty) val = target;
      const rounded = Number(val.toFixed(4));
      generated[String(y)] = rounded;
      if (inputs[String(y)]) inputs[String(y)].value = String(rounded);
    }
    modalPlanState.lastGenerated = generated;
    modalPlanState.manualEdits = false;
    validateYearInputs();
    updatePlanSummary();
  }

  function updateGenerateVisibility() {
    const method = form.planMethod.value;
    const enabled = anchorsValid() && method !== "Manual";
    form.generatePlanBtn.disabled = !enabled;
  }

  function updatePlanSummary() {
    const vals = [];
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((i) => {
      const v = String(i.value || "").trim();
      if (v !== "" && !Number.isNaN(Number(v))) vals.push({ y: Number(i.dataset.year), v: Number(v) });
    });
    const first = vals.length ? Math.min(...vals.map((x) => x.y)) : "-";
    const last = vals.length ? Math.max(...vals.map((x) => x.y)) : "-";
    const count = vals.length;
    let variance = "-";
    if (anchorsValid() && String(form.targetValue.value || "").trim() !== "") {
      const targetYear = Number(form.targetYear.value);
      const target = Number(form.targetValue.value);
      const atTarget = vals.find((x) => x.y === targetYear)?.v;
      if (!Number.isNaN(target) && atTarget !== undefined) variance = (atTarget - target).toFixed(4);
    }
    form.planSummary.textContent = `First planned year: ${first} | Last planned year: ${last} | Populated years: ${count} | Target variance: ${variance}`;
  }

  function populateDatalists() {
    form.goalList.innerHTML = state.goals.map((g) => `<option value="${g.id}">${g.id} — ${g.name}</option><option value="${g.id} - ${g.name}"></option>`).join("");
    form.objectiveList.innerHTML = state.objectives.map((o) => `<option value="${o.name}">${o.id} — ${o.name}</option><option value="${o.id}"></option>`).join("");
    form.initiativeList.innerHTML = state.initiatives.map((i) => `<option value="${i.initiativeId}">${i.initiativeId} — ${i.initiativeName || ""}</option>`).join("");
    form.projectList.innerHTML = state.projects.map((p) => `<option value="${p.projectId}">${p.projectId} — ${p.projectName || ""}</option>`).join("");
    workbook.fillSelect?.(form.owner, workbook.userOptions?.() || [], { placeholder: "Select owner" });
    workbook.fillDatalist?.(document.getElementById("connection-year-list"), ["2026", ...YEARS.map(String)]);
    workbook.fillDatalist?.(document.getElementById("conn-filter-goal-list"), state.goals.map((g) => `${g.id} — ${g.name}`));
    workbook.fillDatalist?.(document.getElementById("conn-filter-objective-list"), state.objectives.map((o) => `${o.id} — ${o.name}`));
    workbook.fillDatalist?.(document.getElementById("conn-filter-initiative-list"), state.initiatives.map((i) => `${i.initiativeId} — ${i.initiativeName || ""}`));
    workbook.fillDatalist?.(document.getElementById("conn-filter-project-list"), state.projects.map((p) => `${p.projectId} — ${p.projectName || ""}`));
    workbook.fillSelect?.(filters.owner, workbook.userOptions?.() || [], { placeholder: "Metric Owner" });
    const companyOptions = workbook.companyOptions?.() || [];
    workbook.fillDatalist?.(document.getElementById("connection-company-list"), companyOptions);
    workbook.fillDatalist?.(document.getElementById("conn-filter-company-list"), companyOptions);
    workbook.fillSelect?.(filters.agg, workbook.connectionAggregation || state.rows.map((r) => r.aggregationMethod), { placeholder: "Aggregation Method" });
    workbook.fillSelect?.(form.goalMetricType, workbook.goalMetricType || [], { placeholder: "Select" });
    workbook.fillSelect?.(form.objectiveMetricType, workbook.objectiveMetricType || [], { placeholder: "Select" });
    workbook.fillSelect?.(form.initiativeMetricType, workbook.initiativeMetricType || [], { placeholder: "Select" });
    workbook.fillSelect?.(form.projectMetricType, workbook.projectMetricType || [], { placeholder: "Select" });
    workbook.fillSelect?.(form.agg, workbook.connectionAggregation || [], { placeholder: "Select" });
    refreshLineageOptionLists();
  }

  function refreshLineageOptionLists() {
    const selectedGoalId = extractLeadingId(form.goalId.value);
    const objRaw = String(form.objective.value || "").trim();
    const selectedObjectiveId = extractLeadingId(objRaw) || state.objectives.find((o) => o.name === objRaw)?.id || "";
    const selectedInitiativeId = extractLeadingId(form.initiativeId.value);
    const objectiveRows = selectedGoalId ? state.objectives.filter((o) => o.parentGoalId === selectedGoalId) : state.objectives;
    const initiativeRows = selectedObjectiveId ? state.initiatives.filter((i) => i.parentObjectiveId === selectedObjectiveId) : state.initiatives;
    const projectRows = selectedInitiativeId ? state.projects.filter((p) => p.parentInitiativeId === selectedInitiativeId) : state.projects;
    form.objectiveList.innerHTML = objectiveRows.map((o) => `<option value="${o.name}">${o.id} — ${o.name}</option><option value="${o.id}"></option>`).join("");
    form.initiativeList.innerHTML = initiativeRows.map((i) => `<option value="${i.initiativeId}">${i.initiativeId} — ${i.initiativeName || ""}</option>`).join("");
    form.projectList.innerHTML = projectRows.map((p) => `<option value="${p.projectId}">${p.projectId} — ${p.projectName || ""}</option>`).join("");
  }

  function extractLeadingId(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const m = raw.match(/^([^—-]+)\s*[—-]\s*/);
    return m ? m[1].trim() : raw;
  }

  function applyLineageCascade() {
    const goal = state.goals.find((g) => g.id === extractLeadingId(form.goalId.value));
    form.goalName.value = goal?.name || "";
    const objectiveRaw = String(form.objective.value || "").trim();
    const objectiveId = extractLeadingId(objectiveRaw);
    const obj = state.objectives.find((o) => o.id === objectiveId) ||
      state.objectives.find((o) => o.name === objectiveRaw || o.id === objectiveRaw);
    if (obj && obj.parentGoalId && obj.parentGoalId !== form.goalId.value) {
      form.goalId.value = obj.parentGoalId;
      form.goalName.value = state.goals.find((g) => g.id === obj.parentGoalId)?.name || form.goalName.value;
    }
    const iniId = extractLeadingId(form.initiativeId.value);
    const ini = state.initiatives.find((i) => i.initiativeId === iniId);
    form.initiativeName.value = ini?.initiativeName || "";
    if (obj?.id && ini?.parentObjectiveId && ini.parentObjectiveId !== obj.id) {
      form.initiativeId.value = "";
      form.initiativeName.value = "";
      form.projectId.value = "";
      form.projectName.value = "";
    }
    if (!String(form.initiativeId.value || "").trim()) {
      form.projectId.value = "";
      form.projectName.value = "";
    }
    const projId = extractLeadingId(form.projectId.value);
    const proj = state.projects.find((p) => p.projectId === projId);
    form.projectName.value = proj?.projectName || "";
    if (proj && ini?.initiativeId && proj.parentInitiativeId && proj.parentInitiativeId !== ini.initiativeId) {
      form.projectId.value = "";
      form.projectName.value = "";
    }
    refreshLineageOptionLists();
  }

  function collectFormRow() {
    const annualPlanValues = {};
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((i) => { annualPlanValues[i.dataset.year] = i.value; });
    return {
      rowKey: form.id.value || `row-${Date.now()}`,
      goalId: extractLeadingId(form.goalId.value.trim()),
      goal: form.goalName.value.trim(),
      goalMetric: form.goalMetric.value.trim(),
      goalMetricType: form.goalMetricType.value.trim(),
      objective: form.objective.value.trim(),
      objectiveMetric: form.objectiveMetric.value.trim(),
      objectiveMetricType: form.objectiveMetricType.value.trim(),
      initiativeId: extractLeadingId(form.initiativeId.value.trim()),
      initiative: form.initiativeName.value.trim(),
      initiativeMetric: form.initiativeMetric.value.trim(),
      initiativeMetricType: form.initiativeMetricType.value.trim(),
      projectId: extractLeadingId(form.projectId.value.trim()),
      project: form.projectName.value.trim(),
      projectMetric: form.projectMetric.value.trim(),
      projectMetricType: form.projectMetricType.value.trim(),
      metricOwner: resolveUserId(form.owner.value.trim()),
      aggregationMethod: form.agg.value.trim(),
      companyScopeMode: form.companyScopeMode.value.trim() || "Derived",
      companyId: extractLeadingId(form.companyId.value.trim()),
      baselineYear: form.baselineYear.value.trim(),
      baselineValue: form.baselineValue.value.trim(),
      annualPlanValues,
      targetYear: form.targetYear.value.trim(),
      targetValue: form.targetValue.value.trim(),
      entryNotes: form.entryNotes.value.trim(),
      decisionRef: form.decisionRef.value.trim(),
      evidenceRef: form.evidenceRef.value.trim(),
      version: Number(form.version.value || 0),
      sourceConnectionId: state.currentEdit?.sourceConnectionId || null
    };
  }

  function resolveObjective(row) {
    const raw = String(row.objective || "").trim();
    if (!raw) return null;
    const extracted = extractLeadingId(raw);
    return state.objectives.find((o) => o.id === extracted) ||
      state.objectives.find((o) => o.id === raw) ||
      state.objectives.find((o) => String(o.name || "").trim().toLowerCase() === raw.toLowerCase()) ||
      null;
  }

  function buildEdgeFromRow(row) {
    const objective = resolveObjective(row);
    const objectiveId = objective?.id || String(row.objective || "").trim();

    if (row.projectId) {
      return {
        fromType: "Initiative",
        fromId: row.initiativeId,
        toType: "Project",
        toId: row.projectId
      };
    }
    if (row.initiativeId) {
      return {
        fromType: "Objective",
        fromId: objectiveId,
        toType: "Initiative",
        toId: row.initiativeId
      };
    }
    return {
      fromType: "Goal",
      fromId: row.goalId,
      toType: "Objective",
      toId: objectiveId
    };
  }

  function validateFormRow(row) {
    const errs = [];
    if (!row.goalId) errs.push("Goal ID required.");
    if (!row.goal) errs.push("Goal required.");
    if (!row.objective) errs.push("Objective required.");
    if (!row.goalMetric) errs.push("Goal Metric required.");
    if (row.companyScopeMode === "Explicit" && !row.companyId) errs.push("Company is required when Company Scope Mode is Explicit.");
    if (row.baselineYear && !yearRegex.test(row.baselineYear)) errs.push("Baseline Year must be 4-digit.");
    if (row.targetYear && !yearRegex.test(row.targetYear)) errs.push("Target Year must be 4-digit.");
    if (row.baselineYear && row.targetYear && Number(row.targetYear) < Number(row.baselineYear)) errs.push("Target Year must be >= Baseline Year.");
    if (!validateYearInputs()) errs.push("Annual plan year values must be numeric.");
    if (form.planMethod.value !== "Manual") {
      if (String(row.baselineValue).trim() === "" || String(row.targetValue).trim() === "") {
        errs.push("Baseline Value and Target Value are required for auto generation methods.");
      }
    }
    if (row.targetYear && YEARS.includes(Number(row.targetYear)) && String(row.targetValue).trim() !== "") {
      const tv = Number(row.targetValue);
      const plan = Number((row.annualPlanValues || {})[row.targetYear]);
      if (!Number.isNaN(plan) && !Number.isNaN(tv) && Number(plan.toFixed(4)) !== Number(tv.toFixed(4))) {
        errs.push("Target year annual value must match Target Value.");
      }
    }
    if (row.initiativeId && !state.initiatives.some((i) => i.initiativeId === row.initiativeId)) errs.push("Initiative ID does not resolve.");
    if (row.projectId && !state.projects.some((p) => p.projectId === row.projectId)) errs.push("Project ID does not resolve.");
    if (row.projectId && !row.initiativeId) errs.push("Project ID requires Initiative ID.");
    const objective = resolveObjective(row);
    if (!objective) errs.push("Objective must resolve to a valid Objective ID.");
    if (objective && objective.parentGoalId && row.goalId && objective.parentGoalId !== row.goalId) {
      errs.push("Objective does not belong to selected Goal ID.");
    }
    const initiative = state.initiatives.find((i) => i.initiativeId === row.initiativeId);
    if (initiative && objective?.id && initiative.parentObjectiveId && initiative.parentObjectiveId !== objective.id) {
      errs.push("Initiative does not belong to selected Objective.");
    }
    const project = state.projects.find((p) => p.projectId === row.projectId);
    if (project && initiative?.initiativeId && project.parentInitiativeId && project.parentInitiativeId !== initiative.initiativeId) {
      errs.push("Project does not belong to selected Initiative.");
    }
    const edge = buildEdgeFromRow(row);
    const validLineage =
      (edge.fromType === "Goal" && edge.toType === "Objective") ||
      (edge.fromType === "Objective" && edge.toType === "Initiative") ||
      (edge.fromType === "Initiative" && edge.toType === "Project");
    if (!validLineage || !edge.fromId || !edge.toId) errs.push("Lineage selection is invalid. Use Goal->Objective->Initiative->Project order.");
    const key = compositeKey(row);
    if (state.rows.some((r) => r.rowKey !== row.rowKey && compositeKey(r) === key)) errs.push("Duplicate row detected by composite business key.");
    return errs;
  }

  function connectionFieldLabel(el) {
    if (!el || !el.id) return "Field";
    const label = modalEl?.querySelector(`label[for="${el.id}"]`);
    return String(label?.textContent || el.id).replace(/\*/g, "").trim();
  }

  function buildConnectionErrorShortcuts(fieldMap) {
    if (!(fieldMap instanceof Map) || fieldMap.size === 0) return "";
    const links = [];
    fieldMap.forEach((_, el) => {
      if (!el?.id) return;
      links.push(`<button type="button" class="connection-error-jump btn btn-sm btn-outline-danger" data-field-id="${el.id}">${connectionFieldLabel(el)}</button>`);
    });
    if (!links.length) return "";
    return `<div class="connection-error-shortcuts mt-2"><span class="small me-2">Go to:</span>${links.join("")}</div>`;
  }

  function setFormErrors(errors, fieldMap) {
    if (!errors.length) {
      formError.classList.add("d-none");
      formError.innerHTML = "";
      return;
    }
    saveBtn.disabled = false;
    formError.classList.remove("d-none");
    formError.innerHTML = `<strong>Please fix the following:</strong><ul class="mb-0">${errors.map((e) => `<li>${e}</li>`).join("")}</ul>${buildConnectionErrorShortcuts(fieldMap)}`;
    formError.querySelectorAll(".connection-error-jump").forEach((btn) => {
      btn.addEventListener("click", () => {
        const target = document.getElementById(btn.dataset.fieldId || "");
        if (!target) return;
        target.scrollIntoView?.({ behavior: "smooth", block: "center" });
        target.focus?.();
      });
    });
  }

  function connectionFieldErrorMap(row) {
    const out = new Map();
    if (!row.goalId) out.set(form.goalId, "Goal ID required.");
    if (!row.goal) out.set(form.goalName, "Goal required.");
    if (!row.objective) out.set(form.objective, "Objective required.");
    if (!row.goalMetric) out.set(form.goalMetric, "Goal Metric required.");
    if (!row.aggregationMethod) out.set(form.agg, "Aggregation Method is required.");
    if (row.companyScopeMode === "Explicit" && !row.companyId) out.set(form.companyId, "Company is required for Explicit mode.");
    if (row.baselineYear && !yearRegex.test(row.baselineYear)) out.set(form.baselineYear, "Baseline Year must be 4-digit.");
    if (row.targetYear && !yearRegex.test(row.targetYear)) out.set(form.targetYear, "Target Year must be 4-digit.");
    if (row.baselineYear && row.targetYear && Number(row.targetYear) < Number(row.baselineYear)) out.set(form.targetYear, "Target Year must be >= Baseline Year.");
    return out;
  }

  function applyConnectionFieldErrors(row, map = connectionFieldErrorMap(row)) {
    [form.goalId, form.goalName, form.objective, form.goalMetric, form.agg, form.companyId, form.baselineYear, form.targetYear]
      .forEach((el) => window.enterpriseModalFormUtils?.clearFieldError?.(el));
    map.forEach((msg, el) => window.enterpriseModalFormUtils?.setFieldError?.(el, msg));
    return map;
  }

  function openModal(row, readOnly) {
    state.currentEdit = row || null;
    const r = row || {
      rowKey: "",
      goalId: "",
      goal: "",
      goalMetric: "",
      goalMetricType: "",
      objective: "",
      objectiveMetric: "",
      objectiveMetricType: "",
      initiativeId: "",
      initiative: "",
      initiativeMetric: "",
      initiativeMetricType: "",
      projectId: "",
      project: "",
      projectMetric: "",
      projectMetricType: "",
      metricOwner: "",
      aggregationMethod: "",
      baselineYear: "",
      baselineValue: "",
      targetYear: "",
      targetValue: "",
      entryNotes: "",
      decisionRef: "",
      evidenceRef: "",
      version: 0,
      companyScopeMode: "Derived",
      companyId: "",
      annualPlanValues: {}
    };
    form.id.value = r.rowKey || "";
    form.goalId.value = r.goalId || "";
    form.goalName.value = r.goal || "";
    form.goalMetric.value = r.goalMetric || "";
    form.goalMetricType.value = r.goalMetricType || "";
    form.objective.value = r.objective || "";
    form.objectiveMetric.value = r.objectiveMetric || "";
    form.objectiveMetricType.value = r.objectiveMetricType || "";
    form.initiativeId.value = r.initiativeId || "";
    form.initiativeName.value = r.initiative || "";
    form.initiativeMetric.value = r.initiativeMetric || "";
    form.initiativeMetricType.value = r.initiativeMetricType || "";
    form.projectId.value = r.projectId || "";
    form.projectName.value = r.project || "";
    form.projectMetric.value = r.projectMetric || "";
    form.projectMetricType.value = r.projectMetricType || "";
    form.owner.value = resolveUserId(r.metricOwner || "");
    form.agg.value = r.aggregationMethod || "";
    form.companyScopeMode.value = r.companyScopeMode || "Derived";
    form.companyId.value = r.companyId || "";
    if (form.companyId) form.companyId.disabled = form.companyScopeMode.value !== "Explicit";
    form.baselineYear.value = r.baselineYear || "";
    form.baselineValue.value = r.baselineValue ?? "";
    form.targetYear.value = r.targetYear || "";
    form.targetValue.value = r.targetValue ?? "";
    form.entryNotes.value = r.entryNotes || "";
    form.decisionRef.value = r.decisionRef || "";
    form.evidenceRef.value = r.evidenceRef || "";
    form.version.value = String(r.version || 0);
    const annual = r.annualPlanValues || r.yearlyValues || {};
    form.planGrid.querySelectorAll(".conn-plan-year").forEach((i) => { i.value = annual[i.dataset.year] ?? ""; });
    form.planMethod.value = "Manual";
    modalPlanState.lastGenerated = null;
    modalPlanState.manualEdits = false;
    highlightPlanRange();
    updatePlanMethodHelp();
    updateGenerateVisibility();
    updatePlanSummary();
    modalEl.querySelector("#connection-modal-title").textContent = readOnly ? "View Link Row" : (row ? "Edit Link Row" : "Add Link Row");
    modalEl.querySelector(".modal-footer .btn-primary").textContent = row ? "Save Row" : "Create Row";
    modalEl.querySelectorAll("input,select,textarea,button").forEach((ctrl) => {
      if (ctrl.id === "connection-save" || ctrl.classList.contains("btn-close")) return;
      if (readOnly) ctrl.setAttribute("disabled", "disabled"); else ctrl.removeAttribute("disabled");
    });
    setFormErrors([], new Map());
    hasSubmitAttempt = false;
    applyConnectionFieldErrors(collectFormRow());
    formDirty = false;
    modal.show();
  }

  function highlightPlanRange() {
    updatePlanInputStyles();
    updateGenerateVisibility();
    updatePlanSummary();
  }

  async function persistRow(row) {
    const edge = buildEdgeFromRow(row);
    const payload = {
      id: row.sourceConnectionId || `conn-${Date.now()}`,
      fromType: edge.fromType,
      fromId: edge.fromId,
      toType: edge.toType,
      toId: edge.toId,
      relationshipType: "Supports",
      contributionType: "Supports",
      contributionWeight: 0,
      status: "Draft",
      version: row.version || 0,
      metricBindingsJson: JSON.stringify({
        goalId: row.goalId, goal: row.goal, goalMetric: row.goalMetric, goalMetricType: row.goalMetricType,
        objective: row.objective, objectiveMetric: row.objectiveMetric, objectiveMetricType: row.objectiveMetricType,
        initiativeId: row.initiativeId, initiative: row.initiative, initiativeMetric: row.initiativeMetric, initiativeMetricType: row.initiativeMetricType,
        projectId: row.projectId, project: row.project, projectMetric: row.projectMetric, projectMetricType: row.projectMetricType,
        metricOwner: resolveUserId(row.metricOwner), aggregationMethod: row.aggregationMethod, companyScopeMode: row.companyScopeMode, companyId: row.companyId, baselineYear: row.baselineYear, baselineValue: row.baselineValue,
        targetYear: row.targetYear, targetValue: row.targetValue, ...(row.annualPlanValues || {})
      }),
      companyScopeMode: row.companyScopeMode || "Derived",
      companyId: row.companyId || null,
      decisionReferencesJson: JSON.stringify({ decisionRef: row.decisionRef }),
      evidenceReferencesJson: JSON.stringify({ evidenceRef: row.evidenceRef, entryNotes: row.entryNotes })
    };
    if (row.sourceConnectionId) {
      await window.strategyConnectionsApi.update(row.sourceConnectionId, payload, row.version || 0);
    } else {
      await window.strategyConnectionsApi.create(payload);
    }
  }

  function duplicateRow(key) {
    const original = state.rows.find((x) => x.rowKey === key);
    if (!original) return;
    const cloned = { ...original, rowKey: `row-${Date.now()}`, sourceConnectionId: null, version: 0 };
    state.rows.unshift(cloned);
    applyFilters();
    openModal(cloned, false);
  }

  async function deleteRow(key) {
    const row = state.rows.find((x) => x.rowKey === key);
    if (!row) return;
    const confirmed = await (window.enterpriseStrategyUi?.confirm?.({
      title: "Delete row?",
      message: "This will delete/retire the selected planning row.",
      confirmLabel: "Delete",
      confirmKind: "danger"
    }) || Promise.resolve(false));
    if (!confirmed) return;
    try {
      if (row.sourceConnectionId) await window.strategyConnectionsApi.remove(row.sourceConnectionId);
      state.rows = state.rows.filter((x) => x.rowKey !== key);
      applyFilters();
      notify("Row deleted.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Delete failed") || "Delete failed", "error");
    }
  }

  function csvToRows(text) {
    const lines = text.split(/\r?\n/).filter(Boolean);
    if (!lines.length) return [];
    const headers = lines[0].split(",").map((h) => h.trim());
    return lines.slice(1).map((line) => {
      const vals = line.split(",");
      const obj = {};
      headers.forEach((h, idx) => { obj[h] = (vals[idx] || "").trim(); });
      return obj;
    });
  }

  async function importRowsFromFile(file) {
    const ext = file.name.toLowerCase().split(".").pop();
    let records = [];
    if (ext === "csv") {
      const text = await file.text();
      records = csvToRows(text);
    } else if (ext === "xlsx") {
      if (!window.XLSX) { notify("XLSX parser unavailable.", "error"); return; }
      const buf = await file.arrayBuffer();
      const wb = window.XLSX.read(buf, { type: "array" });
      const ws = wb.Sheets[wb.SheetNames[0]];
      records = window.XLSX.utils.sheet_to_json(ws, { defval: "" });
    } else {
      notify("Only .csv and .xlsx are supported.", "warning");
      return;
    }
    if (!records.length) { notify("No rows found in import.", "warning"); return; }
    const mapped = records.map((r, idx) => {
      const annualPlanValues = {};
      YEARS.forEach((y) => { annualPlanValues[String(y)] = r[String(y)] ?? ""; });
      return {
        rowKey: `imp-${Date.now()}-${idx}`,
        goalId: r["Goal ID"] || "",
        goal: r["Goal"] || "",
        goalMetric: r["Goal Metric"] || "",
        goalMetricType: r["Goal Metric Type"] || "",
        objective: r["Objective"] || "",
        objectiveMetric: r["Objective Metric"] || "",
        objectiveMetricType: r["Objective Metric Type"] || "",
        initiativeId: r["Initiative ID"] || "",
        initiative: r["Initiative"] || "",
        initiativeMetric: r["Initiative Metric"] || "",
        initiativeMetricType: r["Initiative Metric Type"] || "",
        projectId: r["Project ID"] || "",
        project: r["Project"] || "",
        projectMetric: r["Project Metric"] || "",
        projectMetricType: r["Project Metric Type"] || "",
        metricOwner: resolveUserId(r["Metric Owner ID"] || r["Metric Owner"] || ""),
        aggregationMethod: r["Aggregation Method"] || "",
        baselineYear: r["Baseline Year"] || "",
        baselineValue: r["Baseline Value"] || "",
        annualPlanValues,
        targetYear: r["Target Year"] || "",
        targetValue: r["Target Value"] || "",
        entryNotes: r["Entry Notes"] || "",
        decisionRef: r["Decision Ref"] || "",
        evidenceRef: r["Evidence Ref"] || "",
        version: 0,
        sourceConnectionId: null
      };
    });
    const invalid = [];
    const duplicates = [];
    const accepted = [];
    const existingKeys = new Set(state.rows.map(compositeKey));
    mapped.forEach((r) => {
      const errs = [];
      if (!r.goalId) errs.push("Goal ID");
      if (!r.goal) errs.push("Goal");
      if (!r.objective) errs.push("Objective");
      if (r.baselineYear && !yearRegex.test(String(r.baselineYear))) errs.push("Baseline Year");
      if (r.targetYear && !yearRegex.test(String(r.targetYear))) errs.push("Target Year");
      if (r.baselineYear && r.targetYear && Number(r.targetYear) < Number(r.baselineYear)) errs.push("Year range");
      const key = compositeKey(r);
      if (existingKeys.has(key)) { duplicates.push(r); return; }
      if (errs.length) invalid.push({ row: r, errors: errs });
      else { accepted.push(r); existingKeys.add(key); }
    });
    state.rows = accepted.concat(state.rows);
    applyFilters();
    notify(`Import complete. Added ${accepted.length}, invalid ${invalid.length}, duplicates ${duplicates.length}.`);
  }

  function exportCsv(rows) {
    const data = toVisibleExportRows(rows);
    const headers = Object.keys(data[0] || { "Goal ID": "", Goal: "" });
    const lines = [headers.join(",")].concat(data.map((r) => headers.map((h) => `"${String(r[h] ?? "").replace(/"/g, '""')}"`).join(",")));
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "connection_register.csv";
    a.click();
    URL.revokeObjectURL(url);
  }

  function exportXlsx(rows) {
    if (!window.XLSX) return notify("XLSX exporter unavailable.", "error");
    const data = toVisibleExportRows(rows);
    const ws = window.XLSX.utils.json_to_sheet(data);
    const wb = window.XLSX.utils.book_new();
    window.XLSX.utils.book_append_sheet(wb, ws, "Connection_Map");
    window.XLSX.writeFile(wb, "connection_register.xlsx");
  }

  async function loadData() {
    try {
      await workbook.ensureUsersLoaded?.();
      const [connections, goals, objectives, initiatives, projects, tree, graph, matrix, coverage] = await Promise.all([
        window.strategyConnectionsApi.list(),
        window.strategyGoalsApi.list(),
        window.strategyObjectivesApi.list(),
        window.initiativeStrategyApi.list(),
        window.projectStrategyApi.list(),
        window.strategyConnectionsApi.tree(),
        window.strategyConnectionsApi.graph(),
        window.strategyConnectionsApi.matrix("register"),
        window.strategyConnectionsApi.coverageGaps()
      ]);
      state.goals = goals?.items || [];
      state.objectives = objectives?.items || [];
      state.initiatives = initiatives?.items || [];
      state.projects = projects?.items || [];
      const edges = connections?.items || [];
      state.rows = edges.map(mapToConnectionMapRow);
      state.filtered = [...state.rows];
      populateDatalists();
      const saved = tableControls?.getFilters?.() || {};
      Object.entries(saved).forEach(([k, v]) => {
        if (!filters[k]) return;
        if (filters[k].type === "checkbox") filters[k].checked = Boolean(v);
        else filters[k].value = v;
      });
      renderSecondaryViews(connections || { items: [] }, tree || { items: [] }, graph || {}, matrix || { items: [] }, coverage || { items: [] });
      applyFilters();
    } catch {
      state.rows = [];
      state.filtered = [...state.rows];
      applyFilters();
    }
  }

  document.querySelectorAll(".connection-view-btn").forEach((btn) => btn.addEventListener("click", () => setView(btn.dataset.view)));
  filters.apply?.addEventListener("click", applyFilters);
  filters.clear?.addEventListener("click", () => {
    Object.values(filters).forEach((f) => {
      if (!f || !f.tagName) return;
      if (f.tagName === "INPUT" && f.type === "checkbox") f.checked = false;
      else if (f.tagName === "INPUT") f.value = "";
      else if (f.tagName === "SELECT") f.value = "";
    });
    applyFilters();
  });
  tableEls.prev?.addEventListener("click", () => { state.page = Math.max(1, state.page - 1); renderRegister(); });
  tableEls.next?.addEventListener("click", () => { const max = Math.max(1, Math.ceil(state.filtered.length / state.pageSize)); state.page = Math.min(max, state.page + 1); renderRegister(); });
  tableEls.toggleYears?.addEventListener("click", () => {
    if (!tableControls?.state) return;
    const yearKeys = yearCols.map((c) => c.key);
    const hasAny = tableControls.state.visible.some((k) => yearKeys.includes(k));
    if (hasAny) {
      tableControls.state.visible = tableControls.state.visible.filter((k) => !yearKeys.includes(k));
      tableEls.toggleYears.textContent = "Show Plan Years";
    } else {
      tableControls.state.visible = [...new Set([...tableControls.state.visible, ...yearKeys])];
      tableEls.toggleYears.textContent = "Hide Plan Years";
    }
    try { window.localStorage.setItem("connectionsTableLayout", JSON.stringify(tableControls.state)); } catch { /* ignore storage errors */ }
    renderRegister();
  });
  addRowBtn?.addEventListener("click", () => openModal(null, false));
  exportCsvBtn?.addEventListener("click", () => exportCsv(state.selectedRows.size ? state.filtered.filter((r) => state.selectedRows.has(r.rowKey)) : state.filtered));
  exportXlsxBtn?.addEventListener("click", () => exportXlsx(state.selectedRows.size ? state.filtered.filter((r) => state.selectedRows.has(r.rowKey)) : state.filtered));
  importFileInput?.addEventListener("change", async () => {
    const file = importFileInput.files?.[0];
    if (!file) return;
    await importRowsFromFile(file);
    importFileInput.value = "";
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
      const rows = parsed?.sheets?.Connection_Map || [];
      if (!rows.length) {
        notify("Connection_Map sheet not found or empty.", "warning");
      } else {
        const mapped = rows.map((r, idx) => {
          const annualPlanValues = {};
          YEARS.forEach((y) => { annualPlanValues[String(y)] = r[String(y)] ?? ""; });
          return {
            rowKey: `impwb-${Date.now()}-${idx}`,
            goalId: r["Goal ID"] || "",
            goal: r["Goal"] || "",
            goalMetric: r["Goal Metric"] || "",
            goalMetricType: r["Goal Metric Type"] || "",
            objective: r["Objective"] || "",
            objectiveMetric: r["Objective Metric"] || "",
            objectiveMetricType: r["Objective Metric Type"] || "",
            initiativeId: r["Initiative ID"] || "",
            initiative: r["Initiative"] || "",
            initiativeMetric: r["Initiative Metric"] || "",
            initiativeMetricType: r["Initiative Metric Type"] || "",
            projectId: r["Project ID"] || "",
            project: r["Project"] || "",
            projectMetric: r["Project Metric"] || "",
            projectMetricType: r["Project Metric Type"] || "",
            metricOwner: resolveUserId(r["Metric Owner ID"] || r["Metric Owner"] || ""),
            aggregationMethod: r["Aggregation Method"] || "",
            companyScopeMode: r["Company Scope Mode"] || "Derived",
            companyId: r["Company"] || "",
            baselineYear: r["Baseline Year"] || "",
            baselineValue: r["Baseline Value"] || "",
            annualPlanValues,
            targetYear: r["Target Year"] || "",
            targetValue: r["Target Value"] || "",
            entryNotes: r["Entry Notes"] || "",
            version: 0,
            sourceConnectionId: null
          };
        });
        const existingKeys = new Set(state.rows.map(compositeKey));
        let accepted = 0;
        let invalid = 0;
        let duplicates = 0;
        mapped.forEach((r) => {
          if (!r.goalId || !r.goal || !r.objective) { invalid++; return; }
          const key = compositeKey(r);
          if (existingKeys.has(key)) { duplicates++; return; }
          existingKeys.add(key);
          state.rows.unshift(r);
          accepted++;
        });
        applyFilters();
        notify(`Workbook Connection_Map import complete. Added ${accepted}, invalid ${invalid}, duplicates ${duplicates}.`);
      }
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Workbook import failed") || "Workbook import failed", "error");
    } finally {
      importWorkbookInput.value = "";
    }
  });
  importPageActionBtn?.addEventListener("click", () => importFileInput?.click());
  importWorkbookActionBtn?.addEventListener("click", () => importWorkbookInput?.click());
  exportWorkbookBtn?.addEventListener("click", async () => {
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

  [form.goalId, form.objective, form.initiativeId, form.projectId].forEach((i) => i?.addEventListener("change", applyLineageCascade));
  form.companyScopeMode?.addEventListener("change", () => {
    const explicit = String(form.companyScopeMode.value || "Derived") === "Explicit";
    if (form.companyId) form.companyId.disabled = !explicit;
    if (!explicit && form.companyId) form.companyId.value = "";
  });
  [form.baselineYear, form.targetYear].forEach((i) => i?.addEventListener("input", highlightPlanRange));
  [form.baselineValue, form.targetValue].forEach((i) => i?.addEventListener("input", () => {
    updateGenerateVisibility();
    updatePlanSummary();
  }));
  form.planMethod?.addEventListener("change", () => {
    updatePlanMethodHelp();
    updateGenerateVisibility();
  });
  form.generatePlanBtn?.addEventListener("click", generateAnnualTargets);
  modalEl?.querySelectorAll("input,select,textarea").forEach((i) => i.addEventListener("input", () => {
    if (!hasSubmitAttempt) return;
    const row = collectFormRow();
    const map = applyConnectionFieldErrors(row);
    setFormErrors(validateFormRow(row), map);
  }));
  modalEl?.querySelectorAll("input,select,textarea").forEach((i) => i.addEventListener("change", () => {
    const row = collectFormRow();
    const map = applyConnectionFieldErrors(row);
    if (hasSubmitAttempt) setFormErrors(validateFormRow(row), map);
  }));

  saveBtn?.addEventListener("click", async () => {
    const row = collectFormRow();
    const errs = validateFormRow(row);
    hasSubmitAttempt = true;
    const map = applyConnectionFieldErrors(row);
    if (errs.length) {
      setFormErrors(errs, map);
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(modalEl);
      return;
    }
    try {
      window.enterpriseModalFormUtils?.setSubmitting?.(saveBtn, true, row.sourceConnectionId ? "Save Row" : "Create Row", "Saving...");
      await persistRow(row);
      modal.hide();
      formDirty = false;
      hasSubmitAttempt = false;
      await loadData();
      notify("Row saved.");
    } catch (err) {
      const backendList = window.enterpriseModalFormUtils?.backendErrors?.(err, "Save failed") || [window.enterpriseStrategyUi?.getErrorMessage(err, "Save failed") || "Save failed"];
      window.enterpriseModalFormUtils?.applyBackendFieldErrors?.(err, {
        goalid: form.goalId,
        goal: form.goalName,
        objective: form.objective,
        companyid: form.companyId,
        initiativeid: form.initiativeId,
        projectid: form.projectId,
        baselineyear: form.baselineYear,
        targetyear: form.targetYear,
        aggregationmethod: form.agg
      });
      setFormErrors(backendList, connectionFieldErrorMap(collectFormRow()));
      window.enterpriseModalFormUtils?.focusFirstInvalid?.(modalEl);
    } finally {
      window.enterpriseModalFormUtils?.setSubmitting?.(saveBtn, false, row.sourceConnectionId ? "Save Row" : "Create Row");
    }
  });

  modalEl?.querySelectorAll("input,select,textarea").forEach((i) => i.addEventListener("input", () => { formDirty = true; }));
  modalEl?.querySelectorAll("input,select,textarea").forEach((i) => i.addEventListener("change", () => { formDirty = true; }));
  window.enterpriseModalFormUtils?.bindDirtyCloseGuard?.(modalEl, () => formDirty);
  window.enterpriseModalFormUtils?.blockEnterSubmit?.(modalEl);

  planYearInputs();
  setView("register");
  loadData();
})(window, document);
