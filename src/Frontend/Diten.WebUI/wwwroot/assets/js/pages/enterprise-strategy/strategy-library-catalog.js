(function (window, document) {
  "use strict";

  const tbody = document.querySelector("#library-catalog-table tbody");
  const searchEl = document.getElementById("library-catalog-search");
  const typeEl = document.getElementById("library-catalog-type");
  const statusEl = document.getElementById("library-catalog-status");
  const applyBtn = document.getElementById("library-catalog-apply");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);

  function actionLinks(item) {
    const links = [];
    if (item.itemType === "BlueprintPack") {
      links.push(`<a class="dropdown-item" href="/management-governance/enterprise-strategy-business-performance/library/blueprints/${encodeURIComponent(item.id)}">View</a>`);
      links.push(`<a class="dropdown-item" href="/management-governance/enterprise-strategy-business-performance/library/instantiate?sourceType=BlueprintPack&sourceId=${encodeURIComponent(item.id)}">Instantiate</a>`);
    } else {
      links.push(`<a class="dropdown-item" href="/management-governance/enterprise-strategy-business-performance/library/governance?itemId=${encodeURIComponent(item.id)}">View</a>`);
      links.push(`<a class="dropdown-item" href="/management-governance/enterprise-strategy-business-performance/library/instantiate?sourceType=${encodeURIComponent(item.templateType)}&sourceId=${encodeURIComponent(item.id)}">Instantiate</a>`);
    }
    links.push(`<a class="dropdown-item" href="/management-governance/enterprise-strategy-business-performance/library/governance?itemId=${encodeURIComponent(item.id)}">Governance</a>`);
    return `<div class="dropdown"><button class="btn btn-sm btn-outline-secondary dropdown-toggle" data-bs-toggle="dropdown">Actions</button><div class="dropdown-menu dropdown-menu-end">${links.join("")}</div></div>`;
  }

  function render(items) {
    tbody.innerHTML = "";
    items.forEach((item) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${item.templateType || item.itemType || "-"}</td>
        <td><code>${item.id || ""}</code></td>
        <td>${item.name || ""}</td>
        <td>${item.status || ""}</td>
        <td>${item.version ?? 0}</td>
        <td>${item.owner || "-"}</td>
        <td>${item.entityScope || "-"}</td>
        <td>${item.usageCount ?? 0}</td>
        <td class="text-end">${actionLinks(item)}</td>`;
      tbody.appendChild(tr);
    });
    if (!items.length) {
      const tr = document.createElement("tr");
      tr.innerHTML = '<td colspan="9" class="text-center text-muted py-3">No library items found.</td>';
      tbody.appendChild(tr);
    }
  }

  async function load() {
    try {
      const query = {
        search: searchEl?.value || "",
        templateType: typeEl?.value || "",
        page: 1,
        pageSize: 200
      };
      if (statusEl?.value) query.status = statusEl.value;
      const data = await window.strategyLibraryApi.catalog(query);
      let items = data?.items || [];
      if (statusEl?.value) {
        items = items.filter((x) => String(x.status || "").toLowerCase() === String(statusEl.value).toLowerCase());
      }
      render(items);
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Catalog load failed") || "Catalog load failed", "error");
    }
  }

  applyBtn?.addEventListener("click", load);
  load();
})(window, document);
