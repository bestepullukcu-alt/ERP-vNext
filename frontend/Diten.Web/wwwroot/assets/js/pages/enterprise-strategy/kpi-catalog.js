(function (window, document) {
  "use strict";

  const workbook = window.enterpriseWorkbookOptions || {};
  const utils = window.enterpriseModalFormUtils;
  const tableUtils = window.enterpriseTablePageUtils;
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();
  const tableBody = document.querySelector("#kpi-table tbody");
  const headerRow = document.getElementById("kpi-header-row");
  const modalEl = document.getElementById("kpiEditorModal");
  const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
  const modalTitle = document.getElementById("kpi-modal-title");
  const errorEl = document.getElementById("kpi-form-error");
  const createBtn = document.getElementById("kpi-create");
  const saveBtn = document.getElementById("kpi-save");
  const importFileInput = document.getElementById("kpi-import-file");
  const importActionBtn = document.getElementById("kpi-import-page");

  const fields = {
    id: document.getElementById("kpi-id"),
    name: document.getElementById("kpi-name"),
    category: document.getElementById("kpi-category"),
    type: document.getElementById("kpi-type"),
    description: document.getElementById("kpi-description"),
    owner: document.getElementById("kpi-owner"),
    backupOwner: document.getElementById("kpi-backup-owner"),
    unitOfMeasure: document.getElementById("kpi-unit"),
    aggregationMethod: document.getElementById("kpi-agg"),
    reportingFrequency: document.getElementById("kpi-frequency"),
    thresholdModel: document.getElementById("kpi-threshold"),
    baselineValue: document.getElementById("kpi-baseline"),
    targetValue: document.getElementById("kpi-target"),
    status: document.getElementById("kpi-status"),
    scopeMode: document.getElementById("kpi-scope-mode"),
    companyId: document.getElementById("kpi-company-id"),
    sourceType: document.getElementById("kpi-source-type"),
    decisionReference: document.getElementById("kpi-decision-ref"),
    evidenceReference: document.getElementById("kpi-evidence-ref"),
    notes: document.getElementById("kpi-notes"),
    version: document.getElementById("kpi-version")
  };

  const filters = {
    search: document.getElementById("kpi-search"),
    category: document.getElementById("kpi-filter-category"),
    type: document.getElementById("kpi-filter-type"),
    owner: document.getElementById("kpi-filter-owner"),
    status: document.getElementById("kpi-filter-status"),
    unitOfMeasure: document.getElementById("kpi-filter-unit"),
    aggregationMethod: document.getElementById("kpi-filter-agg"),
    reportingFrequency: document.getElementById("kpi-filter-frequency"),
    reset: document.getElementById("kpi-reset-filters"),
    apply: document.getElementById("kpi-apply-filters")
  };

  const columns = [
    { key: "id", label: "KPI ID", defaultVisible: false },
    { key: "name", label: "KPI Name", defaultVisible: true, required: true },
    { key: "category", label: "KPI Category", defaultVisible: true },
    { key: "type", label: "KPI Type", defaultVisible: true },
    { key: "owner", label: "Owner", defaultVisible: true },
    { key: "unitOfMeasure", label: "Unit of Measure", defaultVisible: true },
    { key: "aggregationMethod", label: "Aggregation Method", defaultVisible: true },
    { key: "reportingFrequency", label: "Reporting Frequency", defaultVisible: true },
    { key: "status", label: "Status", defaultVisible: true },
    { key: "scopeMode", label: "Scope", defaultVisible: false },
    { key: "companyId", label: "Company", defaultVisible: false },
    { key: "sourceType", label: "Source / Derived", defaultVisible: false },
    { key: "thresholdModel", label: "Threshold Model", defaultVisible: false },
    { key: "version", label: "Version", defaultVisible: false },
    { key: "actions", label: "Actions", defaultVisible: true }
  ];

  let allRows = [];
  let filteredRows = [];
  let currentEditId = "";
  let isEdit = false;

  const tableControls = window.enterpriseTableControls?.create({
    pageKey: "kpis",
    storageKey: "kpisTableLayout",
    columnsButtonId: "kpi-columns-btn",
    columns,
    onChange: () => renderFiltered()
  });

  const pager = tableUtils?.createPager?.({
    pageKey: "kpiCatalogTable",
    tableEl: document.getElementById("kpi-table"),
    tableControls,
    defaultPageSize: 25,
    onChange: () => renderFiltered(false)
  });
  const filterHost = tableUtils?.ensureFilterSummaryHost?.(filters.apply?.parentElement, "kpis");

  function unique(values) {
    return [...new Set((values || []).filter(Boolean).map((x) => String(x).trim()))];
  }

  function formatValue(item, key) {
    if (key === "actions") {
      return window.enterpriseRowActionsMenu?.render?.(item.id, [
        { action: "view", label: "View", href: `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(item.id)}` },
        { action: "edit", label: "Edit" },
        { action: "duplicate", label: "Duplicate" },
        { action: "archive", label: "Retire / Archive" },
        { action: "usage", label: "Open Scorecard Usage" },
        { divider: true },
        { action: "export-row", label: "Export row" }
      ]) || "";
    }
    if (key === "owner" || key === "backupOwner") return resolveUserName(item[key]) || "";
    return item[key] ?? "";
  }

  function textValue(item, key) {
    const value = formatValue(item, key);
    return String(value ?? "").replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
  }

  function populateFilterOptions() {
    const rows = allRows;
    workbook.fillSelect?.(filters.category, unique(rows.map((x) => x.category)), { placeholder: "KPI Category" });
    workbook.fillSelect?.(filters.type, unique(rows.map((x) => x.type)), { placeholder: "KPI Type" });
    workbook.fillSelect?.(filters.owner, workbook.userOptions?.() || [], { placeholder: "Owner" });
    workbook.fillSelect?.(filters.status, unique(rows.map((x) => x.status)), { placeholder: "Status" });
    workbook.fillSelect?.(filters.unitOfMeasure, unique(rows.map((x) => x.unitOfMeasure)), { placeholder: "Unit of Measure" });
    workbook.fillSelect?.(filters.aggregationMethod, unique(rows.map((x) => x.aggregationMethod)), { placeholder: "Aggregation Method" });
    workbook.fillSelect?.(filters.reportingFrequency, unique(rows.map((x) => x.reportingFrequency)), { placeholder: "Reporting Frequency" });
  }

  function renderHeader() {
    const visible = tableControls?.getVisibleColumns?.() || columns;
    headerRow.innerHTML = visible.map((col) => {
      if (col.key === "actions") return `<th class="text-end es-row-actions-col"><span class="es-table-head-label">${col.label}</span></th>`;
      const indicator = tableControls?.sortIndicator?.(col.key) || "";
      return `<th data-col-key="${col.key}" class="es-col-sortable" role="button">${col.label}${indicator}</th>`;
    }).join("");

    headerRow.querySelectorAll("th[data-col-key]").forEach((th) => {
      th.addEventListener("click", () => {
        tableControls?.cycleSort?.(th.dataset.colKey);
        renderFiltered(false);
      });
    });
    tableUtils?.bindHeaderColumnDrag?.(headerRow, {
      onReorder: (source, target) => tableControls?.moveColumnTo?.(source, target)
    });
  }

  function applyFilters() {
    const state = {
      search: String(filters.search?.value || "").trim().toLowerCase(),
      category: String(filters.category?.value || ""),
      type: String(filters.type?.value || ""),
      owner: String(filters.owner?.value || ""),
      status: String(filters.status?.value || ""),
      unitOfMeasure: String(filters.unitOfMeasure?.value || ""),
      aggregationMethod: String(filters.aggregationMethod?.value || ""),
      reportingFrequency: String(filters.reportingFrequency?.value || "")
    };
    tableControls?.setFilters?.(state);

    filteredRows = allRows.filter((row) => {
      if (state.search) {
        const bag = `${row.id} ${row.name} ${row.description} ${resolveUserName(row.owner)} ${resolveUserId(row.owner)}`.toLowerCase();
        if (!bag.includes(state.search)) return false;
      }
      if (state.category && row.category !== state.category) return false;
      if (state.type && row.type !== state.type) return false;
      if (state.owner && resolveUserId(row.owner) !== state.owner) return false;
      if (state.status && row.status !== state.status) return false;
      if (state.unitOfMeasure && row.unitOfMeasure !== state.unitOfMeasure) return false;
      if (state.aggregationMethod && row.aggregationMethod !== state.aggregationMethod) return false;
      if (state.reportingFrequency && row.reportingFrequency !== state.reportingFrequency) return false;
      return true;
    });
    tableUtils?.renderFilterSummary?.(filterHost, state);
  }

  function renderTable(rows) {
    const visible = tableControls?.getVisibleColumns?.() || columns;
    tableBody.innerHTML = rows.map((item) => {
      const cells = visible.map((col) => {
        const value = formatValue(item, col.key);
        const align = col.key === "actions" ? " class=\"text-end es-row-actions-col\"" : "";
        return `<td${align}>${value}</td>`;
      }).join("");
      return `<tr data-id="${item.id}">${cells}</tr>`;
    }).join("");
  }

  function renderKpis() {
    document.getElementById("kpi-kpi-total").textContent = String(allRows.length);
    document.getElementById("kpi-kpi-active").textContent = String(allRows.filter((x) => x.status === "Active").length);
    document.getElementById("kpi-kpi-archived").textContent = String(allRows.filter((x) => x.status === "Archived").length);
    document.getElementById("kpi-kpi-missing-owner").textContent = String(allRows.filter((x) => !String(x.owner || "").trim()).length);
  }

  function renderFiltered(resetPage = true) {
    renderHeader();
    applyFilters();
    const sorted = tableControls?.sortRows?.(filteredRows, textValue) || [...filteredRows];
    if (resetPage) pager?.resetToFirstPage?.();
    const paged = pager?.paginate?.(sorted) || sorted;
    renderTable(paged);
    renderKpis();
  }

  function getErrorFields() {
    return [fields.id, fields.name, fields.category, fields.type, fields.owner, fields.unitOfMeasure, fields.aggregationMethod];
  }

  function clearValidation() {
    getErrorFields().forEach((f) => utils?.clearFieldError?.(f));
    utils?.showValidationSummary?.(errorEl, []);
  }

  function readPayload() {
    return {
      id: String(fields.id?.value || "").trim(),
      name: String(fields.name?.value || "").trim(),
      category: String(fields.category?.value || "").trim(),
      type: String(fields.type?.value || "").trim(),
      description: String(fields.description?.value || "").trim(),
      owner: resolveUserId(fields.owner?.value || ""),
      backupOwner: resolveUserId(fields.backupOwner?.value || "") || null,
      unitOfMeasure: String(fields.unitOfMeasure?.value || "").trim(),
      aggregationMethod: String(fields.aggregationMethod?.value || "").trim(),
      thresholdModel: String(fields.thresholdModel?.value || "").trim(),
      reportingFrequency: String(fields.reportingFrequency?.value || "").trim(),
      status: String(fields.status?.value || "Active"),
      scopeMode: String(fields.scopeMode?.value || "Enterprise"),
      companyId: String(fields.companyId?.value || "").trim() || null,
      sourceType: String(fields.sourceType?.value || "Derived"),
      baselineValue: fields.baselineValue?.value === "" ? null : Number(fields.baselineValue?.value),
      targetValue: fields.targetValue?.value === "" ? null : Number(fields.targetValue?.value),
      decisionReference: String(fields.decisionReference?.value || "").trim() || null,
      evidenceReference: String(fields.evidenceReference?.value || "").trim() || null,
      notes: String(fields.notes?.value || "").trim(),
      version: Number(fields.version?.value || 0)
    };
  }

  function validatePayload(payload) {
    const errors = [];
    const required = [
      [fields.id, payload.id, "KPI ID is required."],
      [fields.name, payload.name, "KPI Name is required."],
      [fields.category, payload.category, "KPI Category is required."],
      [fields.type, payload.type, "KPI Type is required."],
      [fields.owner, payload.owner, "Owner is required."],
      [fields.unitOfMeasure, payload.unitOfMeasure, "Unit of Measure is required."],
      [fields.aggregationMethod, payload.aggregationMethod, "Aggregation Method is required."]
    ];
    required.forEach(([el, value, message]) => {
      if (String(value || "").trim()) {
        utils?.clearFieldError?.(el);
      } else {
        utils?.setFieldError?.(el, message);
        errors.push(message);
      }
    });
    if (payload.scopeMode === "SingleCompany" && !payload.companyId) {
      const message = "Company is required for SingleCompany scope.";
      utils?.setFieldError?.(fields.companyId, message);
      errors.push(message);
    } else {
      utils?.clearFieldError?.(fields.companyId);
    }
    return errors;
  }

  function fillEditor(payload) {
    fields.id.value = payload?.id || "";
    fields.name.value = payload?.name || "";
    fields.category.value = payload?.category || "";
    fields.type.value = payload?.type || "";
    fields.description.value = payload?.description || "";
    fields.owner.value = resolveUserId(payload?.owner || "");
    fields.backupOwner.value = resolveUserId(payload?.backupOwner || "");
    fields.unitOfMeasure.value = payload?.unitOfMeasure || "";
    fields.aggregationMethod.value = payload?.aggregationMethod || "";
    fields.thresholdModel.value = payload?.thresholdModel || "";
    fields.reportingFrequency.value = payload?.reportingFrequency || "Monthly";
    fields.status.value = payload?.status || "Active";
    fields.scopeMode.value = payload?.scopeMode || "Enterprise";
    fields.companyId.value = payload?.companyId || "";
    fields.sourceType.value = payload?.sourceType || "Derived";
    fields.baselineValue.value = payload?.baselineValue ?? "";
    fields.targetValue.value = payload?.targetValue ?? "";
    fields.decisionReference.value = payload?.decisionReference || "";
    fields.evidenceReference.value = payload?.evidenceReference || "";
    fields.notes.value = payload?.notes || "";
    fields.version.value = String(payload?.version || 0);
  }

  function openEditor(item, duplicate) {
    isEdit = !!item && !duplicate;
    currentEditId = isEdit ? item.id : "";
    modalTitle.textContent = isEdit ? "Edit KPI" : "Create KPI";
    saveBtn.textContent = isEdit ? "Save KPI" : "Create KPI";
    fillEditor(item || {});
    if (duplicate) {
      fields.id.value = "";
      fields.version.value = "0";
    }
    fields.id.readOnly = isEdit;
    clearValidation();
    modal?.show();
  }

  async function submitEditor() {
    clearValidation();
    const payload = readPayload();
    const errors = validatePayload(payload);
    if (errors.length) {
      utils?.showValidationSummary?.(errorEl, ["Please complete the required fields highlighted below.", ...errors.slice(0, 5)]);
      utils?.focusFirstInvalid?.(modalEl);
      return;
    }
    try {
      utils?.setSubmitting?.(saveBtn, true, isEdit ? "Save KPI" : "Create KPI", isEdit ? "Saving..." : "Creating...");
      if (isEdit) await window.strategyKpisApi.update(currentEditId, payload, Number(payload.version || 0));
      else await window.strategyKpisApi.create(payload);
      modal?.hide();
      await load();
      window.enterpriseStrategyUi?.notify?.(`KPI ${isEdit ? "updated" : "created"} successfully.`);
    } catch (err) {
      const list = utils?.backendErrors?.(err, "Unable to save KPI.") || ["Unable to save KPI."];
      utils?.applyBackendFieldErrors?.(err, fields);
      utils?.showValidationSummary?.(errorEl, list);
      utils?.focusFirstInvalid?.(modalEl);
    } finally {
      utils?.setSubmitting?.(saveBtn, false, isEdit ? "Save KPI" : "Create KPI");
    }
  }

  async function load() {
    const data = await window.strategyKpisApi.list();
    allRows = Array.isArray(data?.items) ? data.items : [];
    populateFilterOptions();
    renderFiltered(true);
  }

  function lookup(row, aliases) {
    const entries = Object.entries(row || {});
    for (const alias of aliases) {
      const hit = entries.find(([k]) => String(k || "").trim().toLowerCase() === String(alias || "").trim().toLowerCase());
      if (hit) return String(hit[1] ?? "").trim();
    }
    return "";
  }

  function toNullableNumber(value) {
    const text = String(value || "").trim();
    if (!text) return null;
    const parsed = Number(text);
    return Number.isFinite(parsed) ? parsed : null;
  }

  function toScopeMode(raw) {
    const normalized = String(raw || "").trim().toLowerCase();
    if (!normalized) return "Enterprise";
    if (normalized.includes("single")) return "SingleCompany";
    if (normalized.includes("multi")) return "MultiCompany";
    return "Enterprise";
  }

  function toStatus(raw) {
    const normalized = String(raw || "").trim();
    if (!normalized) return "Active";
    if (/arch/i.test(normalized)) return "Archived";
    if (/draft/i.test(normalized)) return "Draft";
    return "Active";
  }

  function toSourceType(raw) {
    return /source/i.test(String(raw || "")) ? "Source" : "Derived";
  }

  function pickKpiRows(parsed) {
    if (Array.isArray(parsed?.rows) && parsed.rows.length) return parsed.rows;
    const sheets = parsed?.sheets || {};
    const preferred = [
      "KPI_Library",
      "KPI Library",
      "KPI_Catalog",
      "KPI Catalog",
      "KPIs",
      "KPI_List",
      "KPI List",
      "Sheet1"
    ];
    for (const name of preferred) {
      if (Array.isArray(sheets[name]) && sheets[name].length) return sheets[name];
    }
    const first = Object.keys(sheets)[0];
    return first ? (sheets[first] || []) : [];
  }

  function parseDelimitedText(text, delimiter) {
    const lines = String(text || "").split(/\r?\n/).filter((x) => String(x || "").trim());
    if (!lines.length) return [];
    const headers = lines[0].split(delimiter).map((h) => String(h || "").trim());
    return lines.slice(1).map((line) => {
      const cols = line.split(delimiter);
      const row = {};
      headers.forEach((h, idx) => { row[h] = String(cols[idx] || "").trim(); });
      return row;
    });
  }

  function toKpiPayload(row, fallbackIndex) {
    const id = lookup(row, ["KPI ID", "Kpi ID", "ID", "KPI_Code", "KPI Code", "Metric ID", "Code", "KpiTemplateCode"]);
    const name = lookup(row, ["KPI Name", "KPI", "Name", "Metric Name", "Title", "KpiName"]);
    const payload = {
      id: id || `kpi-import-${Date.now()}-${fallbackIndex}`,
      name,
      category: lookup(row, ["KPI Category", "Category", "Domain", "Theme", "KpiCategory"]),
      type: lookup(row, ["KPI Type", "Type", "Indicator Type", "KpiType"]),
      description: lookup(row, ["Description", "KPI Description", "Definition"]),
      owner: resolveUserId(lookup(row, ["Owner ID", "Owner", "KPI Owner", "Metric Owner", "DefaultOwnerRole"])),
      backupOwner: resolveUserId(lookup(row, ["Backup Owner ID", "Backup Owner", "BackupOwner", "Steward", "ReviewRole"])) || null,
      unitOfMeasure: lookup(row, ["Unit of Measure", "Unit", "UoM"]),
      aggregationMethod: lookup(row, ["Aggregation Method", "Aggregation", "Rollup"]),
      thresholdModel: lookup(row, ["Threshold Model", "Threshold", "Thresholds", "ThresholdModelCode"]),
      reportingFrequency: lookup(row, ["Reporting Frequency", "Frequency", "Cadence"]),
      status: toStatus(lookup(row, ["Status", "KPI Status"])),
      scopeMode: toScopeMode(lookup(row, ["Scope", "Scope Mode", "Entity Scope", "StrategicPerspective"])),
      companyId: lookup(row, ["Company", "Company ID", "Entity", "Business Unit"]) || null,
      sourceType: toSourceType(lookup(row, ["Source / Derived From", "Source Type", "Source", "FormulaType"])),
      baselineValue: toNullableNumber(lookup(row, ["Baseline", "Baseline Value"])),
      targetValue: toNullableNumber(lookup(row, ["Target", "Target Value", "TargetLogic"])),
      decisionReference: lookup(row, ["Decision Ref", "Decision Reference", "DecisionReferenceRequirement"]) || null,
      evidenceReference: lookup(row, ["Evidence Ref", "Evidence Reference", "EvidenceRequirement"]) || null,
      notes: lookup(row, ["Notes", "Comment", "Comments", "BusinessQuestion", "Tags"]),
      version: Number(lookup(row, ["Version"]) || 0)
    };
    return payload;
  }

  async function importKpiRows(rows) {
    const existing = await window.strategyKpisApi.list({ page: 1, pageSize: 5000 });
    const byId = new Map((existing?.items || []).map((x) => [String(x.id || "").toLowerCase(), x]));
    let created = 0;
    let updated = 0;
    let invalid = 0;

    for (let i = 0; i < rows.length; i += 1) {
      const payload = toKpiPayload(rows[i], i + 1);
      if (!String(payload.name || "").trim()) {
        invalid += 1;
        continue;
      }
      const current = byId.get(String(payload.id || "").toLowerCase());
      try {
        if (current) {
          await window.strategyKpisApi.update(current.id, payload, Number(current.version || 0));
          updated += 1;
        } else {
          await window.strategyKpisApi.create(payload);
          created += 1;
        }
      } catch {
        invalid += 1;
      }
    }
    return { created, updated, invalid };
  }

  function exportVisible(type) {
    const visibleColumns = tableUtils?.visibleExportColumns?.(tableControls, columns) || columns.filter((c) => c.key !== "actions");
    const sorted = tableControls?.sortRows?.(filteredRows, textValue) || [...filteredRows];
    if (type === "csv") {
      tableUtils?.exportVisibleCsv?.("kpi-catalog.csv", sorted, visibleColumns, textValue);
      return;
    }
    if (window.XLSX) {
      const rows = sorted.map((item) => {
        const obj = {};
        visibleColumns.forEach((c) => { obj[c.label] = textValue(item, c.key); });
        return obj;
      });
      const wb = window.XLSX.utils.book_new();
      const ws = window.XLSX.utils.json_to_sheet(rows);
      window.XLSX.utils.book_append_sheet(wb, ws, "KPI_Catalog");
      window.XLSX.writeFile(wb, "kpi-catalog.xlsx");
    }
  }

  function bindEvents() {
    createBtn?.addEventListener("click", () => openEditor(null, false));
    saveBtn?.addEventListener("click", submitEditor);
    filters.apply?.addEventListener("click", () => renderFiltered(true));
    filters.reset?.addEventListener("click", () => {
      ["search", "category", "type", "owner", "status", "unitOfMeasure", "aggregationMethod", "reportingFrequency"].forEach((key) => {
        if (filters[key]) filters[key].value = "";
      });
      renderFiltered(true);
    });
    tableBody?.addEventListener("click", async (event) => {
      const actionEl = event.target.closest(".es-row-action-item");
      if (!actionEl) return;
      const rowId = actionEl.dataset.rowId;
      const action = actionEl.dataset.action;
      const item = allRows.find((x) => x.id === rowId);
      if (!item || !action) return;
      if (action === "edit") { event.preventDefault(); openEditor(item, false); return; }
      if (action === "duplicate") { event.preventDefault(); openEditor(item, true); return; }
      if (action === "archive") {
        event.preventDefault();
        const ok = await window.enterpriseStrategyUi?.confirm?.({ title: "Archive KPI?", message: `Archive ${item.name}?`, confirmKind: "danger", confirmLabel: "Archive" });
        if (!ok) return;
        await window.strategyKpisApi.archive(item.id, Number(item.version || 0));
        await load();
        return;
      }
      if (action === "usage") {
        event.preventDefault();
        window.location.href = `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(item.id)}`;
        return;
      }
      if (action === "export-row") {
        event.preventDefault();
        tableUtils?.exportVisibleCsv?.(`${item.id}.csv`, [item], columns.filter((c) => c.key !== "actions"), textValue);
      }
    });
    document.getElementById("kpi-export-csv")?.addEventListener("click", () => exportVisible("csv"));
    document.getElementById("kpi-export-xlsx")?.addEventListener("click", () => exportVisible("xlsx"));
    importActionBtn?.addEventListener("click", () => importFileInput?.click());
    importFileInput?.addEventListener("change", async () => {
      const file = importFileInput.files?.[0];
      if (!file) return;
      try {
        let rows = [];
        if (window.enterpriseWorkbookIo?.parseFile) {
          const parsed = await window.enterpriseWorkbookIo.parseFile(file);
          rows = pickKpiRows(parsed);
        }
        if (!rows.length) {
          const text = await file.text();
          if (String(file.name || "").toLowerCase().endsWith(".tsv")) {
            rows = parseDelimitedText(text, "\t");
          } else if (String(file.name || "").toLowerCase().endsWith(".csv")) {
            rows = parseDelimitedText(text, ",");
          }
        }
        if (!rows.length) {
          window.enterpriseStrategyUi?.notify?.("No KPI rows found in selected file.", "warning");
          return;
        }
        const result = await importKpiRows(rows);
        await load();
        window.enterpriseStrategyUi?.notify?.(`KPI import complete. Created ${result.created}, updated ${result.updated}, invalid ${result.invalid}.`);
      } catch (err) {
        const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "KPI import failed") || "KPI import failed";
        window.enterpriseStrategyUi?.notify?.(msg, "error");
      } finally {
        importFileInput.value = "";
      }
    });
  }

  async function hydrateEditorOptions() {
    workbook.fillSelect?.(fields.category, workbook.goalObjectiveTypes || [], { placeholder: "Select category" });
    workbook.fillSelect?.(fields.type, ["Leading", "Lagging", "Diagnostic", "Predictive"], { placeholder: "Select type" });
    workbook.fillSelect?.(fields.status, ["Active", "Draft", "Archived"], { placeholder: "Select status" });
    workbook.fillSelect?.(fields.unitOfMeasure, workbook.unitOfMeasure || [], { placeholder: "Select unit" });
    workbook.fillSelect?.(fields.aggregationMethod, workbook.connectionAggregation || [], { placeholder: "Select method" });
    workbook.fillSelect?.(fields.reportingFrequency, workbook.reportingFrequencies || [], { placeholder: "Select frequency" });
    workbook.fillSelect?.(fields.owner, workbook.userOptions?.() || [], { placeholder: "Select owner" });
    workbook.fillSelect?.(fields.backupOwner, workbook.userOptions?.() || [], { placeholder: "Select backup owner" });
    workbook.fillSelect?.(fields.scopeMode, workbook.scopeModeValues || ["Enterprise", "SingleCompany", "MultiCompany"], { placeholder: "Select scope" });
    workbook.fillDatalist?.(document.getElementById("kpi-company-list"), workbook.companyOptions?.() || []);
    try {
      const models = await window.kpiLibraryApi.thresholdModels();
      const values = (models || []).map((x) => x.modelCode).filter(Boolean);
      workbook.fillSelect?.(fields.thresholdModel, values.length ? values : (workbook.thresholdModels || []), { placeholder: "Select threshold model" });
    } catch {
      workbook.fillSelect?.(fields.thresholdModel, workbook.thresholdModels || [], { placeholder: "Select threshold model" });
    }
  }

  function restoreFilters() {
    const state = tableControls?.getFilters?.() || {};
    Object.keys(filters).forEach((key) => {
      if (key === "apply") return;
      if (filters[key] && typeof state[key] !== "undefined") filters[key].value = state[key];
    });
  }

  async function init() {
    await workbook.ensureUsersLoaded?.();
    await hydrateEditorOptions();
    bindEvents();
    await load();
    restoreFilters();
    renderFiltered(true);
  }

  init().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load KPI catalog") || "Unable to load KPI catalog";
    if (tableBody) tableBody.innerHTML = `<tr><td colspan="99" class="text-danger">${msg}</td></tr>`;
  });
})(window, document);
