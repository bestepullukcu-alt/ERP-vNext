(function (window, document) {
  "use strict";

  const workbook = window.enterpriseWorkbookOptions || {};
  const utils = window.enterpriseModalFormUtils;
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const idFromRoute = String(document.getElementById("kpi-editor-id")?.value || "").trim();
  const form = document.getElementById("kpi-editor-form");
  const errorEl = document.getElementById("kpi-editor-error");
  const saveBtn = document.getElementById("editor-kpi-save");
  const fields = {
    id: document.getElementById("editor-kpi-id"),
    name: document.getElementById("editor-kpi-name"),
    category: document.getElementById("editor-kpi-category"),
    type: document.getElementById("editor-kpi-type"),
    status: document.getElementById("editor-kpi-status"),
    description: document.getElementById("editor-kpi-description"),
    unit: document.getElementById("editor-kpi-unit"),
    agg: document.getElementById("editor-kpi-agg"),
    frequency: document.getElementById("editor-kpi-frequency"),
    threshold: document.getElementById("editor-kpi-threshold"),
    baseline: document.getElementById("editor-kpi-baseline"),
    target: document.getElementById("editor-kpi-target"),
    owner: document.getElementById("editor-kpi-owner"),
    backupOwner: document.getElementById("editor-kpi-backup-owner"),
    source: document.getElementById("editor-kpi-source"),
    scope: document.getElementById("editor-kpi-scope"),
    company: document.getElementById("editor-kpi-company"),
    decision: document.getElementById("editor-kpi-decision"),
    evidence: document.getElementById("editor-kpi-evidence"),
    version: document.getElementById("editor-kpi-version"),
    notes: document.getElementById("editor-kpi-notes")
  };

  const isEdit = !!idFromRoute;

  function kpiFieldLabel(el) {
    if (!el?.id) return "Field";
    const label = form?.querySelector(`label[for="${el.id}"]`);
    return String(label?.textContent || el.id).replace(/\*/g, "").trim();
  }

  function showSummary(errors, fieldMap) {
    const links = [];
    if (fieldMap instanceof Map) {
      fieldMap.forEach((_, el) => {
        if (!el?.id) return;
        links.push(`<button type="button" class="kpi-error-jump btn btn-sm btn-outline-danger" data-field-id="${el.id}">${kpiFieldLabel(el)}</button>`);
      });
    }
    const list = (errors || []).filter(Boolean);
    errorEl.classList.remove("d-none");
    errorEl.innerHTML = `<strong>Please fix the following:</strong><ul class="mb-0">${list.map((e) => `<li>${e}</li>`).join("")}</ul>${links.length ? `<div class="mt-2"><span class="small me-2">Go to:</span>${links.join("")}</div>` : ""}`;
    errorEl.querySelectorAll(".kpi-error-jump").forEach((btn) => {
      btn.addEventListener("click", () => {
        const target = document.getElementById(btn.dataset.fieldId || "");
        if (!target) return;
        target.scrollIntoView?.({ behavior: "smooth", block: "center" });
        target.focus?.();
      });
    });
  }

  function readPayload() {
    return {
      id: String(fields.id.value || "").trim(),
      name: String(fields.name.value || "").trim(),
      category: String(fields.category.value || "").trim(),
      type: String(fields.type.value || "").trim(),
      description: String(fields.description.value || "").trim(),
      owner: resolveUserId(fields.owner.value || ""),
      backupOwner: resolveUserId(fields.backupOwner.value || "") || null,
      unitOfMeasure: String(fields.unit.value || "").trim(),
      aggregationMethod: String(fields.agg.value || "").trim(),
      thresholdModel: String(fields.threshold.value || "").trim(),
      reportingFrequency: String(fields.frequency.value || "").trim(),
      status: String(fields.status.value || "Active"),
      scopeMode: String(fields.scope.value || "Enterprise"),
      companyId: String(fields.company.value || "").trim() || null,
      sourceType: String(fields.source.value || "Derived"),
      baselineValue: fields.baseline.value === "" ? null : Number(fields.baseline.value),
      targetValue: fields.target.value === "" ? null : Number(fields.target.value),
      decisionReference: String(fields.decision.value || "").trim() || null,
      evidenceReference: String(fields.evidence.value || "").trim() || null,
      version: Number(fields.version.value || 1),
      notes: String(fields.notes.value || "").trim()
    };
  }

  function validate(payload) {
    const errors = [];
    const fieldMap = new Map();
    const required = [
      [fields.id, payload.id, "KPI ID is required."],
      [fields.name, payload.name, "KPI Name is required."],
      [fields.category, payload.category, "KPI Category is required."],
      [fields.type, payload.type, "KPI Type is required."],
      [fields.owner, payload.owner, "Owner is required."],
      [fields.unit, payload.unitOfMeasure, "Unit of Measure is required."],
      [fields.agg, payload.aggregationMethod, "Aggregation Method is required."],
      [fields.frequency, payload.reportingFrequency, "Reporting Frequency is required."]
    ];
    required.forEach(([el, value, message]) => {
      if (String(value || "").trim()) utils?.clearFieldError?.(el);
      else {
        utils?.setFieldError?.(el, message);
        errors.push(message);
        fieldMap.set(el, message);
      }
    });
    if (payload.scopeMode === "SingleCompany" && !payload.companyId) {
      const msg = "Company is required for SingleCompany scope.";
      utils?.setFieldError?.(fields.company, msg);
      errors.push(msg);
      fieldMap.set(fields.company, msg);
    } else {
      utils?.clearFieldError?.(fields.company);
    }
    return { errors, fieldMap };
  }

  function setForm(data) {
    fields.id.value = data.id || "";
    fields.name.value = data.name || "";
    fields.category.value = data.category || "";
    fields.type.value = data.type || "";
    fields.status.value = data.status || "Active";
    fields.description.value = data.description || "";
    fields.unit.value = data.unitOfMeasure || "";
    fields.agg.value = data.aggregationMethod || "";
    fields.frequency.value = data.reportingFrequency || "Monthly";
    fields.threshold.value = data.thresholdModel || "";
    fields.baseline.value = data.baselineValue ?? "";
    fields.target.value = data.targetValue ?? "";
    fields.owner.value = resolveUserId(data.owner || "");
    fields.backupOwner.value = resolveUserId(data.backupOwner || "");
    fields.source.value = data.sourceType || "Derived";
    fields.scope.value = data.scopeMode || "Enterprise";
    fields.company.value = data.companyId || "";
    fields.decision.value = data.decisionReference || "";
    fields.evidence.value = data.evidenceReference || "";
    fields.version.value = String(data.version || 1);
    fields.notes.value = data.notes || "";
  }

  async function load() {
    if (!isEdit) {
      fields.version.value = "1";
      fields.status.value = "Active";
      fields.scope.value = "Enterprise";
      fields.frequency.value = "Monthly";
      return;
    }
    const data = await window.strategyKpisApi.get(idFromRoute);
    fields.id.readOnly = true;
    setForm(data || {});
  }

  async function save(event) {
    event?.preventDefault();
    utils?.showValidationSummary?.(errorEl, []);
    const payload = readPayload();
    const result = validate(payload);
    const errors = result.errors || [];
    if (errors.length) {
      showSummary(["Some required fields are missing or invalid.", ...errors.slice(0, 8)], result.fieldMap);
      utils?.focusFirstInvalid?.(form);
      return;
    }
    try {
      utils?.setSubmitting?.(saveBtn, true, "Save KPI", "Saving...");
      if (isEdit) await window.strategyKpisApi.update(idFromRoute, payload, Number(payload.version || 0));
      else await window.strategyKpisApi.create(payload);
      window.enterpriseStrategyUi?.notify?.("KPI saved successfully.");
      window.location.href = `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(payload.id)}`;
    } catch (err) {
      const list = utils?.backendErrors?.(err, "Unable to save KPI.") || ["Unable to save KPI."];
      utils?.applyBackendFieldErrors?.(err, {
        id: fields.id, name: fields.name, category: fields.category, type: fields.type, owner: fields.owner
      });
      showSummary(list, new Map([[fields.id, true], [fields.name, true], [fields.category, true], [fields.type, true], [fields.owner, true]]));
      utils?.focusFirstInvalid?.(form);
    } finally {
      utils?.setSubmitting?.(saveBtn, false, "Save KPI");
    }
  }

  function hydrateOptions() {
    workbook.fillSelect?.(fields.category, workbook.goalObjectiveTypes || [], { placeholder: "Select category" });
    workbook.fillSelect?.(fields.type, ["Leading", "Lagging", "Diagnostic", "Predictive"], { placeholder: "Select type" });
    workbook.fillSelect?.(fields.status, ["Active", "Draft", "Archived"], { placeholder: "Select status" });
    workbook.fillSelect?.(fields.unit, workbook.unitOfMeasure || [], { placeholder: "Select unit" });
    workbook.fillSelect?.(fields.agg, workbook.connectionAggregation || [], { placeholder: "Select method" });
    workbook.fillSelect?.(fields.frequency, workbook.reportingFrequencies || [], { placeholder: "Select frequency" });
    workbook.fillSelect?.(fields.threshold, workbook.thresholdModels || [], { placeholder: "Select threshold model" });
    workbook.fillSelect?.(fields.owner, workbook.userOptions?.() || [], { placeholder: "Select owner" });
    workbook.fillSelect?.(fields.backupOwner, workbook.userOptions?.() || [], { placeholder: "Select backup owner" });
    workbook.fillSelect?.(fields.scope, workbook.scopeModeValues || ["Enterprise", "SingleCompany", "MultiCompany"], { placeholder: "Select scope" });
    workbook.fillDatalist?.(document.getElementById("editor-kpi-company-list"), workbook.companyOptions?.() || []);
  }

  form?.addEventListener("submit", save);
  (async function init() {
    await workbook.ensureLookupsLoaded?.();
    await workbook.ensureUsersLoaded?.();
    await workbook.ensureCompaniesLoaded?.();
    hydrateOptions();
    await load();
  })().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load KPI.") || "Unable to load KPI.";
    utils?.showValidationSummary?.(errorEl, [msg]);
  });
})(window, document);
