(function (window, document) {
  "use strict";

  const pickEl = document.getElementById("objective-alignment-pick");
  const pickList = document.getElementById("objective-alignment-list");
  const loadBtn = document.getElementById("objective-alignment-load");
  const stateEl = document.getElementById("objective-alignment-state");
  const openInitiativesBtn = document.getElementById("objective-alignment-open-initiatives");
  const createInitiativeBtn = document.getElementById("objective-alignment-create-initiative");

  const hosts = {
    goal: document.getElementById("objective-alignment-goal"),
    objective: document.getElementById("objective-alignment-objective"),
    coverage: document.getElementById("objective-alignment-coverage"),
    initiatives: document.getElementById("objective-alignment-initiatives"),
    projects: document.getElementById("objective-alignment-projects"),
    register: document.getElementById("objective-alignment-register")
  };

  const state = {
    objectives: [],
    goals: [],
    initiatives: [],
    projects: [],
    connections: [],
    selectedObjectiveId: ""
  };

  const workbook = window.enterpriseWorkbookOptions || {};

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function getObjectiveIdFromInput(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const direct = state.objectives.find((objective) => objective.id === raw);
    if (direct) return direct.id;
    const dash = raw.match(/^([^—-]+)\s*[—-]\s*/);
    const extracted = dash ? dash[1].trim() : raw;
    const byExtract = state.objectives.find((objective) => objective.id === extracted);
    if (byExtract) return byExtract.id;
    const byName = state.objectives.find((objective) => String(objective.name || "").toLowerCase() === raw.toLowerCase());
    return byName?.id || "";
  }

  function contributionSummary(initiative) {
    const rows = Array.isArray(initiative?.contributionPlanValues) ? initiative.contributionPlanValues : [];
    const plannedRows = rows.filter((row) => row?.plannedValue !== null && row?.plannedValue !== undefined).length;
    const parts = [];
    if (initiative?.contributionMetricName) parts.push(initiative.contributionMetricName);
    if (initiative?.contributionPlanGranularity) parts.push(initiative.contributionPlanGranularity);
    if (plannedRows > 0) parts.push(`${plannedRows} planned row${plannedRows === 1 ? "" : "s"}`);
    return parts.length ? parts.join(" | ") : "No contribution summary yet.";
  }

  function objectiveRegisterRows(objective) {
    if (!objective) return [];
    return state.connections.filter((row) => {
      const meta = JSON.parse(row.metricBindingsJson || "{}");
      const objectiveText = String(meta.objective || "").toLowerCase();
      return objectiveText.includes(String(objective.name || "").toLowerCase()) || meta.objectiveId === objective.id;
    });
  }

  function syncDeliveryLinks(objectiveId) {
    const baseListUrl = objectiveId
      ? `/management-governance/delivery-execution/initiatives?parentObjectiveId=${encodeURIComponent(objectiveId)}`
      : "/management-governance/delivery-execution/initiatives";
    const baseCreateUrl = objectiveId
      ? `/management-governance/delivery-execution/initiatives/new?parentObjectiveId=${encodeURIComponent(objectiveId)}`
      : "/management-governance/delivery-execution/initiatives/new";
    if (openInitiativesBtn) openInitiativesBtn.href = baseListUrl;
    if (createInitiativeBtn) createInitiativeBtn.href = baseCreateUrl;
  }

  function render() {
    const objective = state.objectives.find((row) => row.id === state.selectedObjectiveId);
    syncDeliveryLinks(objective?.id || "");
    if (!objective) {
      hosts.goal.innerHTML = '<div class="small text-muted">Select an objective to view alignment.</div>';
      hosts.objective.innerHTML = "";
      hosts.coverage.innerHTML = "";
      hosts.initiatives.innerHTML = "";
      hosts.projects.innerHTML = "";
      hosts.register.innerHTML = "";
      return;
    }

    const goal = state.goals.find((row) => row.id === objective.parentGoalId);
    const initiatives = state.initiatives || [];
    const projects = (state.projects || []).filter((project) => project.parentObjectiveId === objective.id);
    const registerRows = objectiveRegisterRows(objective);

    const missingInitiatives = initiatives.length === 0;
    const missingProjects = projects.length === 0;
    const missingTargetLinkage = !(objective.metrics || []).length;
    const companyScopeInconsistency = objective.inheritCompanyScope === false &&
      !objective.primaryCompanyId &&
      !(objective.applicableCompanyIds || []).length;

    hosts.goal.innerHTML = goal ? `
      <div><strong>${escapeHtml(goal.name || goal.id)}</strong></div>
      <div class="small text-muted">${escapeHtml(goal.id)} | Status: ${escapeHtml(goal.status || "-")} | Owner: ${escapeHtml(workbook.userDisplayName?.(goal.ownerId || goal.owner) || goal.owner || "-")}</div>
      <div class="mt-2"><a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(goal.id)}">Open Goal Detail</a></div>
    ` : '<div class="small text-muted">Parent goal not found.</div>';

    hosts.objective.innerHTML = `
      <div><strong>${escapeHtml(objective.name || objective.id)}</strong></div>
      <div class="small text-muted">${escapeHtml(objective.id)} | Status: ${escapeHtml(objective.status || "-")} | Type: ${escapeHtml(objective.type || "-")} | Priority: ${escapeHtml(objective.priority || "-")}</div>
      <div class="small text-muted mt-1">Contribution: ${escapeHtml(objective.contributionType || "-")} (${escapeHtml(objective.contributionWeight)})</div>
      <div class="mt-2 d-flex flex-wrap gap-2">
        <a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(objective.id)}">Open Objective Detail</a>
        <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/new?parentObjectiveId=${encodeURIComponent(objective.id)}">Create Initiative in Delivery</a>
      </div>
    `;

    const badges = [];
    if (missingInitiatives) badges.push('<span class="badge bg-label-danger me-1">No aligned initiatives linked</span>');
    if (missingProjects) badges.push('<span class="badge bg-label-danger me-1">No aligned projects linked</span>');
    if (missingTargetLinkage) badges.push('<span class="badge bg-label-warning me-1">Missing target/metric linkage</span>');
    if (companyScopeInconsistency) badges.push('<span class="badge bg-label-warning me-1">Company scope inconsistency</span>');
    if (!badges.length) badges.push('<span class="badge bg-label-success">Coverage looks healthy</span>');
    hosts.coverage.innerHTML = badges.join("");

    hosts.initiatives.innerHTML = initiatives.length ? initiatives.map((initiative) => `
      <div class="border rounded p-2 mb-2">
        <strong>${escapeHtml(initiative.initiativeName || initiative.initiativeId)}</strong>
        <div class="small text-muted">${escapeHtml(initiative.initiativeId)} | Status: ${escapeHtml(initiative.status || initiative.readinessStatus || "-")} | Owner: ${escapeHtml(workbook.userDisplayName?.(initiative.deliveryOwnerPersonId || initiative.owner) || initiative.owner || initiative.deliveryOwnerPersonId || "-")}</div>
        <div class="small text-muted">Contribution: ${escapeHtml(contributionSummary(initiative))}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="/management-governance/delivery-execution/initiatives/${encodeURIComponent(initiative.initiativeId)}">Open in Delivery</a>
      </div>
    `).join("") : '<div class="small text-muted">No initiatives linked.</div>';

    hosts.projects.innerHTML = projects.length ? projects.map((project) => `
      <div class="border rounded p-2 mb-2">
        <strong>${escapeHtml(project.projectName || project.projectId)}</strong>
        <div class="small text-muted">${escapeHtml(project.projectId)} | Status: ${escapeHtml(project.status || "-")} | Phase: ${escapeHtml(project.phase || "-")}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="/management-governance/delivery-execution/projects/${encodeURIComponent(project.projectId)}">Open in Delivery</a>
      </div>
    `).join("") : '<div class="small text-muted">No projects linked.</div>';

    hosts.register.innerHTML = registerRows.length ? `
      <div class="small text-muted mb-2">Found ${registerRows.length} related register rows.</div>
      ${registerRows.slice(0, 20).map((row) => {
        const meta = JSON.parse(row.metricBindingsJson || "{}");
        return `<div class="border rounded p-2 mb-2">
          <div><strong>${escapeHtml(meta.goal || "-")} -> ${escapeHtml(meta.objective || "-")}</strong></div>
          <div class="small text-muted">Initiative: ${escapeHtml(meta.initiative || "-")} | Project: ${escapeHtml(meta.project || "-")} | Target: ${escapeHtml(meta.targetYear || "-")} / ${escapeHtml(meta.targetValue || "-")}</div>
          <a class="btn btn-sm btn-outline-secondary mt-1" href="/management-governance/enterprise-strategy-business-performance/connections">Open Register</a>
        </div>`;
      }).join("")}
    ` : '<div class="small text-muted">No related register rows found.</div>';
  }

  async function loadObjectiveReferences(objectiveId) {
    if (!objectiveId) {
      state.initiatives = [];
      return;
    }

    state.initiatives = await window.strategyObjectivesApi.initiatives(objectiveId).catch(() => []);
  }

  async function selectObjective(objectiveId) {
    state.selectedObjectiveId = objectiveId;
    if (!objectiveId) {
      render();
      return;
    }

    const objective = state.objectives.find((row) => row.id === objectiveId);
    if (objective && pickEl) pickEl.value = `${objective.id} — ${objective.name}`;
    stateEl.textContent = "Loading objective references...";
    await loadObjectiveReferences(objectiveId);
    stateEl.textContent = "Objective alignment loaded.";
    render();
  }

  async function loadData() {
    stateEl.textContent = "Loading objective alignment...";
    try {
      const [objectives, goals, projects, connections] = await Promise.all([
        window.strategyObjectivesApi.list(),
        window.strategyGoalsApi.list(),
        window.projectStrategyApi.list(),
        window.strategyConnectionsApi.list()
      ]);

      state.objectives = objectives?.items || [];
      state.goals = goals?.items || [];
      state.projects = projects?.items || [];
      state.connections = connections?.items || [];
      pickList.innerHTML = state.objectives.map((objective) => `<option value="${escapeHtml(objective.id)} — ${escapeHtml(objective.name)}"></option><option value="${escapeHtml(objective.id)}"></option>`).join("");

      const qs = new URLSearchParams(window.location.search);
      const queryObjectiveId = qs.get("objectiveId");
      await selectObjective(queryObjectiveId || state.objectives[0]?.id || "");
    } catch (err) {
      stateEl.textContent = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Failed to load objective alignment.") || "Failed to load objective alignment.";
    }
  }

  async function handleObjectiveSelection() {
    const objectiveId = getObjectiveIdFromInput(pickEl?.value);
    await selectObjective(objectiveId);
  }

  loadBtn?.addEventListener("click", () => { void handleObjectiveSelection(); });
  pickEl?.addEventListener("keydown", (event) => {
    if (event.key !== "Enter") return;
    void handleObjectiveSelection();
  });

  loadData().catch(() => {});
})(window, document);
