(function (window, document) {
  "use strict";

  const kpiId = String(document.getElementById("kpi-detail-id")?.value || "").trim();
  const workbook = window.enterpriseWorkbookOptions || {};
  const stateEl = document.getElementById("kpi-detail-state");
  const set = (id, html) => { const el = document.getElementById(id); if (el) el.innerHTML = html; };
  const text = (v) => String(v ?? "-");
  const companyText = (id) => text(workbook.companyDisplayName?.(id) || id);
  const userText = (v) => text(workbook.userDisplayName?.(v) || v);

  function list(label, values) {
    const items = (values || []).filter(Boolean);
    return `<div><strong>${label}:</strong> ${items.length ? items.map((x) => `<span class="badge bg-label-secondary me-1">${x}</span>`).join("") : "-"}</div>`;
  }

  async function load() {
    if (!kpiId) {
      stateEl.textContent = "No KPI ID provided.";
      return;
    }
    const [kpi, usage] = await Promise.all([
      window.strategyKpisApi.get(kpiId),
      window.strategyKpisApi.usage(kpiId).catch(() => ({ goalIds: [], objectiveIds: [], initiativeIds: [], projectIds: [], scorecardIds: [] }))
    ]);

    document.getElementById("kpi-detail-edit").href = `/management-governance/enterprise-strategy-business-performance/kpis/${encodeURIComponent(kpiId)}/edit`;
    stateEl.textContent = `Loaded KPI ${kpiId}`;

    set("kpi-detail-overview",
      `<div><strong>ID:</strong> ${text(kpi.id)}</div>
       <div><strong>Name:</strong> ${text(kpi.name)}</div>
       <div><strong>Status:</strong> ${text(kpi.status)}</div>
       <div><strong>Version:</strong> ${text(kpi.version)}</div>
       <div><strong>Scope:</strong> ${text(kpi.scopeMode)} ${kpi.companyId ? `(${companyText(kpi.companyId)})` : ""}</div>`);

    set("kpi-detail-definition",
      `<div><strong>Category:</strong> ${text(kpi.category)}</div>
       <div><strong>Type:</strong> ${text(kpi.type)}</div>
       <div><strong>Description:</strong> ${text(kpi.description)}</div>
       <div><strong>Source:</strong> ${text(kpi.sourceType)}</div>`);

    set("kpi-detail-ownership",
      `<div><strong>Owner:</strong> ${userText(kpi.owner)}</div>
       <div><strong>Backup Owner:</strong> ${userText(kpi.backupOwner)}</div>`);

    set("kpi-detail-thresholds",
      `<div><strong>Threshold Model:</strong> ${text(kpi.thresholdModel)}</div>
       <div><strong>Baseline:</strong> ${text(kpi.baselineValue)}</div>
       <div><strong>Target:</strong> ${text(kpi.targetValue)}</div>`);

    set("kpi-detail-reporting",
      `<div><strong>Unit:</strong> ${text(kpi.unitOfMeasure)}</div>
       <div><strong>Aggregation:</strong> ${text(kpi.aggregationMethod)}</div>
       <div><strong>Cadence:</strong> ${text(kpi.reportingFrequency)}</div>`);

    set("kpi-detail-governance",
      `<div><strong>Decision Ref:</strong> ${text(kpi.decisionReference)}</div>
       <div><strong>Evidence Ref:</strong> ${text(kpi.evidenceReference)}</div>
       <div><strong>Notes:</strong> ${text(kpi.notes)}</div>
       <div><strong>Updated:</strong> ${kpi.updatedAt ? new Date(kpi.updatedAt).toLocaleString() : "-"}</div>`);

    set("kpi-detail-used-by",
      list("Goals", usage.goalIds) +
      list("Objectives", usage.objectiveIds) +
      list("Initiatives", usage.initiativeIds) +
      list("Projects", usage.projectIds) +
      list("Scorecards", usage.scorecardIds));
  }

  load().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load KPI detail.") || "Unable to load KPI detail.";
    stateEl.textContent = msg;
  });
})(window, document);
