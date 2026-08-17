(function (window, document) {
  "use strict";

  const stateEl = document.getElementById("graph-state");
  const listEl = document.getElementById("graph-list");
  const kpiNodes = document.getElementById("graph-kpi-nodes");
  const kpiEdges = document.getElementById("graph-kpi-edges");
  const kpiGoals = document.getElementById("graph-kpi-goals");
  const kpiProjects = document.getElementById("graph-kpi-projects");

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  async function load() {
    stateEl.textContent = "Loading graph...";
    try {
      const graph = await window.strategyConnectionsApi.graph();
      const nodes = graph?.nodes || [];
      const edges = graph?.edges || [];
      const goalCount = nodes.filter((n) => String(n.type || "").toLowerCase() === "goal").length;
      const projectCount = nodes.filter((n) => String(n.type || "").toLowerCase() === "project").length;

      kpiNodes.textContent = String(nodes.length);
      kpiEdges.textContent = String(edges.length);
      kpiGoals.textContent = String(goalCount);
      kpiProjects.textContent = String(projectCount);

      const nodeById = new Map(nodes.map((n) => [n.id, n]));
      listEl.innerHTML = edges.length ? edges.map((e) => {
        const from = nodeById.get(e.fromId);
        const to = nodeById.get(e.toId);
        return `<div class="border rounded p-2 mb-2">
          <div><strong>${escapeHtml(from?.label || e.fromId)}</strong> -> <strong>${escapeHtml(to?.label || e.toId)}</strong></div>
          <div class="small text-muted">Edge: ${escapeHtml(e.id)} | Status: ${escapeHtml(e.status || "-")}</div>
        </div>`;
      }).join("") : '<div class="small text-muted">No graph edges found.</div>';
      stateEl.textContent = "Graph loaded.";
    } catch (err) {
      stateEl.textContent = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Failed to load graph.") || "Failed to load graph.";
    }
  }

  load();
})(window, document);
