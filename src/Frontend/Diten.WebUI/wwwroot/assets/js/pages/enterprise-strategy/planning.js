(function (window, document) {
  "use strict";

  const cyclesTableBody = document.querySelector("#planning-cycles-table tbody");
  const periodsTableBody = document.querySelector("#strategy-periods-table tbody");
  const cycleFilter = document.getElementById("planning-cycle-filter");

  const cycleModalEl = document.getElementById("planningCycleModal");
  const cycleModal = cycleModalEl ? new bootstrap.Modal(cycleModalEl) : null;
  const periodModalEl = document.getElementById("strategyPeriodModal");
  const periodModal = periodModalEl ? new bootstrap.Modal(periodModalEl) : null;

  const notify = (message, kind) => window.enterpriseStrategyUi?.notify?.(message, kind || "success");
  const workbook = window.enterpriseWorkbookOptions || {};

  let cycles = [];
  let periods = [];
  let lookups = {};

  function fmtDate(value) {
    if (!value) return "-";
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? "-" : d.toLocaleDateString();
  }

  function findCycleCode(cycleId) {
    const cycle = cycles.find((x) => String(x.id || "").toLowerCase() === String(cycleId || "").toLowerCase());
    return cycle ? cycle.code : cycleId || "-";
  }

  function renderCycles() {
    if (!cyclesTableBody) return;
    if (!cycles.length) {
      cyclesTableBody.innerHTML = '<tr><td colspan="6" class="text-muted">No planning cycles found.</td></tr>';
      return;
    }
    cyclesTableBody.innerHTML = cycles.map((c) => `
      <tr data-cycle-id="${c.id || ""}">
        <td>${c.code || ""}</td>
        <td>${c.name || ""}</td>
        <td>${c.planningCycleType || ""}</td>
        <td>${c.status || ""}</td>
        <td>${c.ownerId || ""}</td>
        <td>${fmtDate(c.effectiveFrom)} - ${fmtDate(c.effectiveTo)}</td>
      </tr>
    `).join("");
  }

  function renderPeriods() {
    if (!periodsTableBody) return;
    const selectedCycle = String(cycleFilter?.value || "").trim();
    const view = selectedCycle
      ? periods.filter((p) => String(p.planningCycleId || "").toLowerCase() === selectedCycle.toLowerCase())
      : periods;

    if (!view.length) {
      periodsTableBody.innerHTML = '<tr><td colspan="8" class="text-muted">No strategy periods found.</td></tr>';
      return;
    }

    periodsTableBody.innerHTML = view.map((p) => {
      const scope = [p.companyId, p.businessUnitId, p.regionId].filter(Boolean).join(" / ");
      return `
        <tr>
          <td>${p.code || ""}</td>
          <td>${p.name || ""}</td>
          <td>${findCycleCode(p.planningCycleId)}</td>
          <td>${scope || "-"}</td>
          <td>${p.reviewCadence || "-"}</td>
          <td>${p.status || ""}</td>
          <td>${p.isDefaultForScope ? "Yes" : "No"}</td>
          <td>${fmtDate(p.startDate)} - ${fmtDate(p.endDate)}</td>
        </tr>
      `;
    }).join("");
  }

  function fillCycleSelectors() {
    const cycleOptions = cycles.map((c) => ({ value: c.id, label: `${c.code} - ${c.name}` }));
    workbook.fillSelect?.(cycleFilter, cycleOptions, { placeholder: "All cycles", keepCurrent: true });
    workbook.fillSelect?.(document.getElementById("strategy-period-cycle"), cycleOptions, { placeholder: "Select", keepCurrent: true });
  }

  function fillPlanningLookups() {
    const cycleTypes = lookups.planningCycleTypes || [];
    const statuses = lookups.planningLifecycleStatuses || lookups.strategyPeriodLifecycleStatuses || [];
    const reviewCadences = lookups.reviewCadences || [];
    const scenarioTypes = lookups.strategyPeriodScenarioTypes || [];

    workbook.fillSelect?.(document.getElementById("planning-cycle-type"), cycleTypes, { placeholder: "Select" });
    workbook.fillSelect?.(document.getElementById("planning-cycle-status"), statuses, { placeholder: "Select", defaultValue: "Draft" });
    workbook.fillSelect?.(document.getElementById("strategy-period-review"), reviewCadences, { placeholder: "Select" });
    workbook.fillSelect?.(document.getElementById("strategy-period-status"), statuses, { placeholder: "Select", defaultValue: "Draft" });
    workbook.fillSelect?.(document.getElementById("strategy-period-scenario"), scenarioTypes, { placeholder: "None" });
  }

  async function loadData() {
    const [lookupsData, cyclesData, periodsData] = await Promise.all([
      window.strategyEnterpriseMetaApi?.lookups?.().catch(() => ({})),
      window.strategyPlanningApi?.listCycles?.().catch(() => []),
      window.strategyPlanningApi?.listStrategyPeriods?.().catch(() => [])
    ]);
    lookups = lookupsData || {};
    cycles = Array.isArray(cyclesData) ? cyclesData : [];
    periods = Array.isArray(periodsData) ? periodsData : [];
    fillPlanningLookups();
    fillCycleSelectors();
    renderCycles();
    renderPeriods();
  }

  function cyclePayload() {
    return {
      code: String(document.getElementById("planning-cycle-code")?.value || "").trim(),
      name: String(document.getElementById("planning-cycle-name")?.value || "").trim(),
      planningCycleType: String(document.getElementById("planning-cycle-type")?.value || "").trim(),
      description: String(document.getElementById("planning-cycle-description")?.value || "").trim(),
      status: String(document.getElementById("planning-cycle-status")?.value || "").trim() || "Draft",
      ownerId: String(document.getElementById("planning-cycle-owner")?.value || "").trim(),
      effectiveFrom: String(document.getElementById("planning-cycle-effective-from")?.value || "").trim(),
      effectiveTo: String(document.getElementById("planning-cycle-effective-to")?.value || "").trim()
    };
  }

  function periodPayload() {
    return {
      planningCycleId: String(document.getElementById("strategy-period-cycle")?.value || "").trim(),
      code: String(document.getElementById("strategy-period-code")?.value || "").trim(),
      name: String(document.getElementById("strategy-period-name")?.value || "").trim(),
      companyId: String(document.getElementById("strategy-period-company")?.value || "").trim(),
      businessUnitId: String(document.getElementById("strategy-period-bu")?.value || "").trim() || null,
      regionId: String(document.getElementById("strategy-period-region")?.value || "").trim() || null,
      startDate: String(document.getElementById("strategy-period-start")?.value || "").trim(),
      endDate: String(document.getElementById("strategy-period-end")?.value || "").trim(),
      reviewCadence: String(document.getElementById("strategy-period-review")?.value || "").trim(),
      scenarioType: String(document.getElementById("strategy-period-scenario")?.value || "").trim() || null,
      versionLabel: String(document.getElementById("strategy-period-version")?.value || "").trim() || null,
      status: String(document.getElementById("strategy-period-status")?.value || "").trim() || "Draft",
      isDefaultForScope: Boolean(document.getElementById("strategy-period-default")?.checked),
      notes: String(document.getElementById("strategy-period-notes")?.value || "").trim()
    };
  }

  async function createCycle() {
    try {
      await window.strategyPlanningApi.createCycle(cyclePayload());
      cycleModal?.hide();
      notify("Planning cycle created.");
      await loadData();
    } catch (err) {
      const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Could not create planning cycle.");
      notify(message || "Could not create planning cycle.", "danger");
    }
  }

  async function createPeriod() {
    try {
      await window.strategyPlanningApi.createStrategyPeriod(periodPayload());
      periodModal?.hide();
      notify("Strategy period created.");
      await loadData();
    } catch (err) {
      const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Could not create strategy period.");
      notify(message || "Could not create strategy period.", "danger");
    }
  }

  document.getElementById("planning-create-cycle")?.addEventListener("click", () => cycleModal?.show());
  document.getElementById("planning-create-period")?.addEventListener("click", () => periodModal?.show());
  document.getElementById("planning-cycle-save")?.addEventListener("click", createCycle);
  document.getElementById("strategy-period-save")?.addEventListener("click", createPeriod);
  cycleFilter?.addEventListener("change", renderPeriods);

  loadData().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Could not load planning cycle data.");
    notify(message || "Could not load planning cycle data.", "danger");
  });
})(window, document);
