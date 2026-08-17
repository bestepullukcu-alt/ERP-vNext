(function (window, document) {
  "use strict";

  const tbody = document.querySelector("#proj-lib-table tbody");
  const metricsCard = document.getElementById("proj-lib-metrics-card");
  const metricsFor = document.getElementById("proj-lib-metrics-for");
  const metricsTbody = document.querySelector("#proj-lib-metrics-table tbody");
  const summaryEl = document.getElementById("proj-lib-summary");
  const searchEl = document.getElementById("proj-lib-search");
  const statusEl = document.getElementById("proj-lib-status");
  const phaseEl = document.getElementById("proj-lib-phase");
  const deliveryEl = document.getElementById("proj-lib-delivery");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);

  let allItems = [];

  function uniqSelect(select, values, placeholder) {
    if (!select) return;
    const cur = select.value;
    const opts = [...new Set((values || []).filter(Boolean).map((x) => String(x).trim()))].sort((a, b) => a.localeCompare(b));
    select.innerHTML = `<option value="">${placeholder}</option>` + opts.map((v) => `<option value="${v}">${v}</option>`).join("");
    if (opts.includes(cur)) select.value = cur;
  }

  function buildQuery() {
    const p = new URLSearchParams();
    p.set("page", "1");
    p.set("pageSize", "5000");
    const s = String(searchEl?.value || "").trim();
    if (s) p.set("search", s);
    if (statusEl?.value) p.set("projectStatus", statusEl.value);
    if (phaseEl?.value) p.set("phase", phaseEl.value);
    if (deliveryEl?.value) p.set("deliveryType", deliveryEl.value);
    return p.toString();
  }

  async function loadTemplateDetail(projectId) {
    if (!window.strategyLibraryApi?.template || !metricsCard || !metricsTbody) return;
    try {
      const t = await window.strategyLibraryApi.template(projectId);
      metricsFor.textContent = `${projectId} — template`;
      metricsTbody.innerHTML = `<tr><td colspan="6"><pre class="small mb-0 text-wrap" style="white-space:pre-wrap;max-height:320px;overflow:auto">${escapeHtml(JSON.stringify(t, null, 2))}</pre></td></tr>`;
      metricsCard.classList.remove("d-none");
    } catch (e) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(e, "Failed to load template") || "Failed to load template", "error");
    }
  }

  async function loadMetrics(projectId) {
    if (!window.strategyLibraryApi?.projectLibraryMetrics || !metricsCard || !metricsTbody) return;
    try {
      const rows = await window.strategyLibraryApi.projectLibraryMetrics(projectId);
      metricsFor.textContent = projectId;
      metricsTbody.innerHTML = (rows || []).map((m) =>
        `<tr><td>${escapeHtml(m.successMetric || "")}</td><td>${escapeHtml(m.metricType || "")}</td>` +
        `<td>${escapeHtml(String(m.baselineValue ?? ""))}</td><td>${escapeHtml(String(m.targetValue ?? ""))}</td>` +
        `<td>${escapeHtml(m.unitOfMeasure || "")}</td><td>${escapeHtml(m.aggregationMethod || "")}</td></tr>`
      ).join("") || `<tr><td colspan="6" class="text-muted text-center py-2">No metrics</td></tr>`;
      metricsCard.classList.remove("d-none");
    } catch (e) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(e, "Failed to load metrics") || "Failed to load metrics", "error");
    }
  }

  function escapeHtml(s) {
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function renderRows(items) {
    if (!tbody) return;
    tbody.innerHTML = (items || []).map((r) => {
      const menu = window.enterpriseRowActionsMenu?.render?.(r.projectId, [
        { action: "metrics", label: "View metrics" },
        { action: "detail", label: "Open template detail" },
        { action: "instantiate", label: "Instantiate" }
      ]) || "";
      return `<tr data-project-id="${escapeHtml(r.projectId)}">
        <td><code>${escapeHtml(r.projectId || "")}</code></td>
        <td>${escapeHtml(r.name || "")}</td>
        <td>${escapeHtml(r.ownerPm || "")}</td>
        <td>${escapeHtml(r.sponsor || "")}</td>
        <td>${escapeHtml(r.status || "")}</td>
        <td>${escapeHtml(r.phase || "")}</td>
        <td>${escapeHtml(r.deliveryType || "")}</td>
        <td>${escapeHtml(r.entityScope || "")}</td>
        <td>${escapeHtml(r.riskRating || "")}</td>
        <td>${escapeHtml(r.readinessStatus || "")}</td>
        <td>${escapeHtml(String(r.version ?? ""))}</td>
        <td>${escapeHtml(String(r.metricCount ?? 0))}</td>
        <td class="text-end es-row-actions-col">${menu}</td>
      </tr>`;
    }).join("");

    tbody.querySelectorAll(".es-row-action-item").forEach((el) => {
      el.addEventListener("click", (ev) => {
        ev.preventDefault();
        const tr = el.closest("tr");
        const id = tr?.dataset?.projectId;
        const action = el.dataset?.action;
        if (!id || !action) return;
        if (action === "metrics") loadMetrics(id);
        if (action === "detail") loadTemplateDetail(id);
        if (action === "instantiate") {
          window.location.href = "/management-governance/enterprise-strategy-business-performance/library/instantiate?sourceType=Project&sourceId=" + encodeURIComponent(id);
        }
      });
    });
  }

  async function load() {
    try {
      const data = await window.strategyLibraryApi.projectsLibrary(buildQuery());
      allItems = data?.items || [];
      uniqSelect(statusEl, allItems.map((x) => x.status), "Project status");
      uniqSelect(phaseEl, allItems.map((x) => x.phase), "Stage / phase");
      uniqSelect(deliveryEl, allItems.map((x) => x.deliveryType), "Delivery type");
      renderRows(allItems);
      summaryEl.textContent = `Showing ${allItems.length} of ${data?.totalCount ?? allItems.length} project templates (server total).`;
    } catch (e) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(e, "Projects library load failed") || "Load failed", "error");
      summaryEl.textContent = "Load failed.";
    }
  }

  document.getElementById("proj-lib-apply")?.addEventListener("click", () => load());
  searchEl?.addEventListener("keydown", (e) => { if (e.key === "Enter") load(); });
  load();
})(window, document);
