(function (window, document) {
  "use strict";

  const treeHost = document.getElementById("goal-hierarchy-tree");
  const stateEl = document.getElementById("goal-hierarchy-state");
  const searchEl = document.getElementById("goal-hierarchy-search");
  const expandBtn = document.getElementById("goal-hierarchy-expand-all");
  const collapseBtn = document.getElementById("goal-hierarchy-collapse-all");
  const kpiGoals = document.getElementById("gh-kpi-goals");
  const kpiObjectives = document.getElementById("gh-kpi-objectives");
  const kpiInitiatives = document.getElementById("gh-kpi-initiatives");
  const kpiProjects = document.getElementById("gh-kpi-projects");
  const workbook = window.enterpriseWorkbookOptions || {};

  const state = {
    goals: [],
    objectives: [],
    initiatives: [],
    projects: [],
    expanded: new Set()
  };

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function toggleNode(key) {
    if (state.expanded.has(key)) state.expanded.delete(key);
    else state.expanded.add(key);
    render();
  }

  function render() {
    const q = String(searchEl?.value || "").trim().toLowerCase();
    const goals = state.goals.filter((g) => {
      if (!q) return true;
      return [g.id, g.name, g.owner, g.status, g.scopeMode].join(" ").toLowerCase().includes(q);
    });

    treeHost.innerHTML = goals.map((goal) => {
      const goalKey = `goal:${goal.id}`;
      const goalExpanded = state.expanded.has(goalKey);
      const objectives = state.objectives.filter((o) => o.parentGoalId === goal.id);
      const initiativeForObjective = {};
      objectives.forEach((o) => {
        initiativeForObjective[o.id] = state.initiatives.filter((i) => i.parentObjectiveId === o.id);
      });
      const projectForInitiative = {};
      Object.values(initiativeForObjective).flat().forEach((i) => {
        projectForInitiative[i.initiativeId] = state.projects.filter((p) => p.parentInitiativeId === i.initiativeId);
      });
      const metricCount = (goal.metrics || []).length;
      const companySummary = goal.primaryCompanyId || (goal.applicableCompanyIds || []).join(", ") || "Enterprise";

      return `<div class="border rounded mb-2">
        <div class="p-2 d-flex align-items-center gap-2">
          <button class="btn btn-sm btn-outline-secondary gh-toggle" data-key="${escapeHtml(goalKey)}">${goalExpanded ? "-" : "+"}</button>
          <div class="flex-grow-1">
            <strong>${escapeHtml(goal.name || goal.id)}</strong>
            <div class="small text-muted">${escapeHtml(goal.id)} | Owner: ${escapeHtml(workbook.userDisplayName?.(goal.ownerId || goal.owner) || goal.owner || "-")} | Status: ${escapeHtml(goal.status || "-")} | Scope: ${escapeHtml(companySummary)}</div>
            <div class="small text-muted">Objectives: ${objectives.length} | Aligned Initiatives: ${Object.values(initiativeForObjective).flat().length} | Aligned Projects: ${Object.values(projectForInitiative).flat().length} | Metrics: ${metricCount}</div>
          </div>
          <div class="d-flex gap-1">
            <a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/goals/${encodeURIComponent(goal.id)}">Open Goal Detail</a>
            <a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/connections">Add/Register Alignment Row</a>
          </div>
        </div>
        <div class="p-2 pt-0 ${goalExpanded ? "" : "d-none"}">
          ${objectives.length ? objectives.map((o) => {
            const objKey = `objective:${o.id}`;
            const objExpanded = state.expanded.has(objKey);
            const initiatives = initiativeForObjective[o.id] || [];
            return `<div class="border rounded mb-2 ms-2">
              <div class="p-2 d-flex align-items-center gap-2">
                <button class="btn btn-sm btn-outline-secondary gh-toggle" data-key="${escapeHtml(objKey)}">${objExpanded ? "-" : "+"}</button>
                <div class="flex-grow-1">
                  <strong>${escapeHtml(o.name || o.id)}</strong>
                  <div class="small text-muted">${escapeHtml(o.id)} | Status: ${escapeHtml(o.status || "-")} | Type: ${escapeHtml(o.type || "-")}</div>
                  <div class="small text-muted">Aligned Initiatives: ${initiatives.length}</div>
                </div>
                <a class="btn btn-sm btn-outline-secondary" href="/management-governance/enterprise-strategy-business-performance/objectives/${encodeURIComponent(o.id)}">Open Objective Detail</a>
              </div>
              <div class="p-2 pt-0 ${objExpanded ? "" : "d-none"}">
                ${initiatives.length ? initiatives.map((i) => {
                  const iniKey = `initiative:${i.initiativeId}`;
                  const iniExpanded = state.expanded.has(iniKey);
                  const projects = projectForInitiative[i.initiativeId] || [];
                  return `<div class="border rounded mb-2 ms-2">
                    <div class="p-2 d-flex align-items-center gap-2">
                      <button class="btn btn-sm btn-outline-secondary gh-toggle" data-key="${escapeHtml(iniKey)}">${iniExpanded ? "-" : "+"}</button>
                      <div class="flex-grow-1">
                        <strong>${escapeHtml(i.initiativeName || i.initiativeId)}</strong>
                        <div class="small text-muted">${escapeHtml(i.initiativeId)} | Status: ${escapeHtml(i.status || "-")} | Company: ${escapeHtml(i.sponsoringCompanyId || "-")}</div>
                        <div class="small text-muted">Aligned Projects: ${projects.length}</div>
                      </div>
                      <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/initiatives/${encodeURIComponent(i.initiativeId)}">Open Initiative in Delivery</a>
                    </div>
                    <div class="p-2 pt-0 ${iniExpanded ? "" : "d-none"}">
                      ${projects.length ? projects.map((p) => `
                        <div class="border rounded p-2 mb-1 ms-2 d-flex align-items-center justify-content-between">
                          <div>
                            <strong>${escapeHtml(p.projectName || p.projectId)}</strong>
                            <div class="small text-muted">${escapeHtml(p.projectId)} | Status: ${escapeHtml(p.status || "-")} | Delivery Company: ${escapeHtml(p.deliveryCompanyId || "-")}</div>
                          </div>
                          <a class="btn btn-sm btn-outline-secondary" href="/management-governance/delivery-execution/projects/${encodeURIComponent(p.projectId)}">Open Project in Delivery</a>
                        </div>
                      `).join("") : `<div class="small text-muted ms-2">No aligned projects.</div>`}
                    </div>
                  </div>`;
                }).join("") : `<div class="small text-muted ms-2">No aligned initiatives.</div>`}
              </div>
            </div>`;
          }).join("") : `<div class="small text-muted ms-2">No child objectives.</div>`}
        </div>
      </div>`;
    }).join("");

    if (!goals.length) {
      treeHost.innerHTML = '<div class="text-muted small">No hierarchy nodes found.</div>';
    }

    treeHost.querySelectorAll(".gh-toggle").forEach((btn) => {
      btn.addEventListener("click", () => toggleNode(btn.dataset.key));
    });
  }

  async function load() {
    try {
      stateEl.textContent = "Loading hierarchy...";
      const [goals, objectives, projects] = await Promise.all([
        window.strategyGoalsApi.list(),
        window.strategyObjectivesApi.list(),
        window.projectStrategyApi.list()
      ]);
      const objectiveRows = objectives?.items || [];
      const initiativeGroups = await Promise.all(
        objectiveRows.map((objective) => window.strategyObjectivesApi.initiatives(objective.id).catch(() => []))
      );
      state.goals = goals?.items || [];
      state.objectives = objectiveRows;
      state.initiatives = initiativeGroups.flat();
      state.projects = projects?.items || [];
      kpiGoals.textContent = String(state.goals.length);
      kpiObjectives.textContent = String(state.objectives.length);
      kpiInitiatives.textContent = String(state.initiatives.length);
      kpiProjects.textContent = String(state.projects.length);
      stateEl.textContent = "Hierarchy loaded.";
      render();
    } catch (err) {
      stateEl.textContent = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Failed to load hierarchy.") || "Failed to load hierarchy.";
    }
  }

  searchEl?.addEventListener("input", render);
  expandBtn?.addEventListener("click", () => {
    state.expanded = new Set([
      ...state.goals.map((g) => `goal:${g.id}`),
      ...state.objectives.map((o) => `objective:${o.id}`),
      ...state.initiatives.map((i) => `initiative:${i.initiativeId}`)
    ]);
    render();
  });
  collapseBtn?.addEventListener("click", () => {
    state.expanded.clear();
    render();
  });

  load();
})(window, document);
