(function (window) {
  "use strict";

  function removeGoalHierarchyUiCtas() {
    const entries = Array.from(document.querySelectorAll(
      'a[href*="/management-governance/enterprise-strategy-business-performance/goals/hierarchy"],' +
      'a[href*="/goals/hierarchy"],' +
      '.dropdown-item'
    ));
    entries.forEach((el) => {
      const text = String(el.textContent || "").trim().toLowerCase();
      const href = String(el.getAttribute("href") || "").toLowerCase();
      const isHierarchyEntry = href.includes("/goals/hierarchy") || text === "goal hierarchy";
      if (!isHierarchyEntry) return;
      const li = el.closest("li");
      if (li) li.remove();
      else el.remove();
    });
  }

  function normalizeCreateButtons() {
    const ids = ["goal-create", "objective-create", "initiative-create-ppm", "project-create-ppm", "kpi-create"];
    ids.forEach((id) => {
      const btn = document.getElementById(id);
      if (!btn) return;
      btn.textContent = "Create";
      btn.classList.remove("btn-outline-primary");
      btn.classList.add("btn-primary", "esbp-create-btn");
    });
  }

  window.enterpriseStrategyApis = {
    strategyGoalsApi: window.strategyGoalsApi,
    strategyObjectivesApi: window.strategyObjectivesApi,
    strategyConnectionsApi: window.strategyConnectionsApi,
    strategyPlanningApi: window.strategyPlanningApi,
    initiativeStrategyApi: window.initiativeStrategyApi,
    projectStrategyApi: window.projectStrategyApi,
    metricCatalogApi: window.metricCatalogApi,
    auditEvidenceApi: window.auditEvidenceApi,
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
      removeGoalHierarchyUiCtas();
      normalizeCreateButtons();
    }, { once: true });
  } else {
    removeGoalHierarchyUiCtas();
    normalizeCreateButtons();
  }
})(window);
