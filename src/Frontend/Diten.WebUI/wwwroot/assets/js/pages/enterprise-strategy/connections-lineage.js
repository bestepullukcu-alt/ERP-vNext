(function (window, document) {
  "use strict";

  const inputEl = document.getElementById("lineage-context");
  const loadBtn = document.getElementById("lineage-load");
  const stateEl = document.getElementById("lineage-state");
  const hosts = {
    context: document.getElementById("lineage-context-summary"),
    upstream: document.getElementById("lineage-upstream"),
    downstream: document.getElementById("lineage-downstream"),
    metrics: document.getElementById("lineage-metrics")
  };
  const YEARS = Array.from({ length: 20 }, (_, i) => 2027 + i);
  const workbook = window.enterpriseWorkbookOptions || {};

  const state = {
    goals: [],
    objectives: [],
    initiatives: [],
    projects: [],
    connections: [],
    selected: null
  };

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function parseSelected() {
    const raw = String(inputEl?.value || "").trim().toLowerCase();
    if (!raw) return null;
    const goal = state.goals.find((g) => [g.id, g.name].join(" ").toLowerCase().includes(raw));
    if (goal) return { type: "goal", id: goal.id, label: goal.name };
    const objective = state.objectives.find((o) => [o.id, o.name].join(" ").toLowerCase().includes(raw));
    if (objective) return { type: "objective", id: objective.id, label: objective.name };
    const initiative = state.initiatives.find((i) => [i.initiativeId, i.initiativeName].join(" ").toLowerCase().includes(raw));
    if (initiative) return { type: "initiative", id: initiative.initiativeId, label: initiative.initiativeName };
    const project = state.projects.find((p) => [p.projectId, p.projectName].join(" ").toLowerCase().includes(raw));
    if (project) return { type: "project", id: project.projectId, label: project.projectName };
    return null;
  }

  function connectionMetaList() {
    return state.connections.map((c) => ({ edge: c, meta: JSON.parse(c.metricBindingsJson || "{}") }));
  }

  function findConnectedRows(sel) {
    const metas = connectionMetaList();
    if (!sel) return [];
    return metas.filter(({ meta }) => {
      const blob = [meta.goalId, meta.goal, meta.objectiveId, meta.objective, meta.initiativeId, meta.initiative, meta.projectId, meta.project].join(" ").toLowerCase();
      return blob.includes(String(sel.id || "").toLowerCase()) || blob.includes(String(sel.label || "").toLowerCase());
    });
  }

  function render() {
    const selected = state.selected;
    if (!selected) {
      hosts.context.innerHTML = '<div class="small text-muted">Choose a context to inspect lineage.</div>';
      hosts.upstream.innerHTML = "";
      hosts.downstream.innerHTML = "";
      hosts.metrics.innerHTML = "";
      return;
    }
    const rows = findConnectedRows(selected);
    const first = rows[0]?.meta || {};

    hosts.context.innerHTML = `
      <div><strong>${escapeHtml(selected.label || selected.id)}</strong></div>
      <div class="small text-muted">Type: ${escapeHtml(selected.type)} | ID: ${escapeHtml(selected.id)}</div>
      <div class="small text-muted mt-1">Related alignment rows: ${rows.length}</div>
      <div class="mt-2"><a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/connections">Edit Alignment Row</a></div>
    `;

    hosts.upstream.innerHTML = `
      <div class="border rounded p-2 mb-2">
        <div><strong>Goal</strong>: ${escapeHtml(first.goal || "-")}</div>
        <div class="small text-muted">Goal ID: ${escapeHtml(first.goalId || "-")}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="${first.goalId ? `/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(first.goalId)}` : '/management-governance/enterprise-strategy-business-performance/goals'}">Open Goal</a>
      </div>
      <div class="border rounded p-2">
        <div><strong>Objective</strong>: ${escapeHtml(first.objective || "-")}</div>
        <div class="small text-muted">Objective ID: ${escapeHtml(first.objectiveId || "-")}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="/management-governance/enterprise-strategy-business-performance/objectives/alignment">Open Objective</a>
      </div>
    `;

    hosts.downstream.innerHTML = `
      <div class="border rounded p-2 mb-2">
        <div><strong>Initiative</strong>: ${escapeHtml(first.initiative || "-")}</div>
        <div class="small text-muted">Initiative ID: ${escapeHtml(first.initiativeId || "-")}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="${first.initiativeId ? `/management-governance/delivery-execution/initiatives/${encodeURIComponent(first.initiativeId)}` : '/management-governance/delivery-execution/initiatives'}">Open in Delivery</a>
      </div>
      <div class="border rounded p-2">
        <div><strong>Project</strong>: ${escapeHtml(first.project || "-")}</div>
        <div class="small text-muted">Project ID: ${escapeHtml(first.projectId || "-")}</div>
        <a class="btn btn-sm btn-outline-secondary mt-1" href="${first.projectId ? `/management-governance/delivery-execution/projects/${encodeURIComponent(first.projectId)}` : '/management-governance/delivery-execution/projects'}">Open in Delivery</a>
      </div>
    `;

    const plannedYears = YEARS.filter((y) => String(first[String(y)] || "").trim() !== "");
    const companySummary = `${first.companyScopeMode || "Derived"}${first.companyId ? ` (${workbook.companyDisplayName?.(first.companyId) || first.companyId})` : ""}`;
    hosts.metrics.innerHTML = `
      <div class="small text-muted">Baseline: ${escapeHtml(first.baselineYear || "-")} / ${escapeHtml(first.baselineValue || "-")}</div>
      <div class="small text-muted">Target: ${escapeHtml(first.targetYear || "-")} / ${escapeHtml(first.targetValue || "-")}</div>
      <div class="small text-muted">Aggregation Method: ${escapeHtml(first.aggregationMethod || "-")}</div>
      <div class="small text-muted">Annual planning presence: ${plannedYears.length ? `${plannedYears[0]} - ${plannedYears[plannedYears.length - 1]} (${plannedYears.length} years)` : "No annual plan values"}</div>
      <div class="small text-muted">Company scope: ${escapeHtml(companySummary)}</div>
    `;
  }

  async function load() {
    stateEl.textContent = "Loading lineage context...";
    try {
      const [goals, objectives, initiatives, projects, connections] = await Promise.all([
        window.strategyGoalsApi.list(),
        window.strategyObjectivesApi.list(),
        window.initiativeStrategyApi.list(),
        window.projectStrategyApi.list(),
        window.strategyConnectionsApi.list()
      ]);
      state.goals = goals?.items || [];
      state.objectives = objectives?.items || [];
      state.initiatives = initiatives?.items || [];
      state.projects = projects?.items || [];
      state.connections = connections?.items || [];
      const firstGoal = state.goals[0];
      if (firstGoal) {
        inputEl.value = `${firstGoal.id} - ${firstGoal.name}`;
        state.selected = { type: "goal", id: firstGoal.id, label: firstGoal.name };
      }
      stateEl.textContent = "Lineage context loaded.";
      render();
    } catch (err) {
      stateEl.textContent = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Failed to load lineage context.") || "Failed to load lineage context.";
    }
  }

  loadBtn?.addEventListener("click", () => {
    state.selected = parseSelected();
    render();
  });
  inputEl?.addEventListener("keydown", (e) => {
    if (e.key !== "Enter") return;
    state.selected = parseSelected();
    render();
  });

  load();
})(window, document);
