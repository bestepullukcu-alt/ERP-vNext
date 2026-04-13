(function (window, document) {
  "use strict";

  const defaults = window.__strategyLibraryInstantiateDefaults || {};
  const sourceTypeEl = document.getElementById("library-inst-source-type");
  const sourceIdEl = document.getElementById("library-inst-source-id");
  const fullChainEl = document.getElementById("library-inst-full-chain");
  const allowDuplicatesEl = document.getElementById("library-inst-allow-duplicates");
  const overridesEl = document.getElementById("library-inst-overrides");
  const runBtn = document.getElementById("library-inst-run");
  const resultEl = document.getElementById("library-inst-result");
  const openPickerBtn = document.getElementById("library-inst-open-picker");
  const clearBtn = document.getElementById("library-inst-clear-selection");
  const pickerModalEl = document.getElementById("library-inst-picker-modal");
  const pickerSearchEl = document.getElementById("library-inst-picker-search");
  const pickerTbody = document.querySelector("#library-inst-picker-table tbody");
  const summaryBody = document.getElementById("library-inst-summary-body");

  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);

  let catalogRows = [];
  /** @type {Record<string, unknown> | null} */
  let selectedRow = null;

  const pickerModal = pickerModalEl && window.bootstrap?.Modal ? new window.bootstrap.Modal(pickerModalEl) : null;

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function parseOverrides(text) {
    const out = {};
    String(text || "")
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean)
      .forEach((pair) => {
        const idx = pair.indexOf("=");
        if (idx < 1) return;
        const key = pair.slice(0, idx).trim();
        const value = pair.slice(idx + 1).trim();
        if (!key) return;
        out[key] = value;
      });
    return out;
  }

  function currentTemplateTypeFilter() {
    const st = String(sourceTypeEl?.value || "");
    return st === "BlueprintPack" ? "BlueprintPack" : st;
  }

  function renderSummary() {
    if (!summaryBody) return;
    const id = String(sourceIdEl?.value || "").trim();
    if (!id) {
      summaryBody.innerHTML = '<span class="text-muted">No template selected yet. Use <strong>Browse catalog…</strong>.</span>';
      return;
    }
    if (selectedRow && String(selectedRow.id || "") === id) {
      const scope = selectedRow.entityScope || selectedRow.categoryOrType || "-";
      const applicability = selectedRow.categoryOrType && selectedRow.entityScope ? selectedRow.categoryOrType : scope;
      summaryBody.innerHTML = `
        <div><span class="text-muted">Code</span> <code>${escapeHtml(selectedRow.id)}</code></div>
        <div><span class="text-muted">Name</span> ${escapeHtml(selectedRow.name || "")}</div>
        <div><span class="text-muted">Type</span> ${escapeHtml(selectedRow.templateType || selectedRow.itemType || "")}</div>
        <div><span class="text-muted">Status</span> ${escapeHtml(selectedRow.status || "")} &nbsp;|&nbsp; <span class="text-muted">Version</span> ${escapeHtml(String(selectedRow.version ?? ""))}</div>
        <div><span class="text-muted">Scope summary</span> ${escapeHtml(String(scope))}</div>
        <div><span class="text-muted">Company applicability</span> ${escapeHtml(String(applicability))}</div>`;
      return;
    }
    summaryBody.innerHTML = `<span class="text-muted">Selected ID</span> <code>${escapeHtml(id)}</code> <span class="text-muted">(open picker to load full summary)</span>`;
  }

  function setSelection(row) {
    selectedRow = row;
    if (sourceIdEl) sourceIdEl.value = row ? String(row.id || "").trim() : "";
    renderSummary();
  }

  function filterRows(rows) {
    const q = String(pickerSearchEl?.value || "").trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((x) => {
      const blob = `${x.id} ${x.name} ${x.owner} ${x.templateType} ${x.status} ${x.entityScope} ${x.categoryOrType}`.toLowerCase();
      return blob.includes(q);
    });
  }

  function renderPickerTable() {
    if (!pickerTbody) return;
    const rows = filterRows(catalogRows);
    pickerTbody.innerHTML = "";
    if (!rows.length) {
      pickerTbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-3">No templates match this filter.</td></tr>';
      return;
    }
    rows.forEach((item) => {
      const tr = document.createElement("tr");
      const applicability = item.categoryOrType || item.entityScope || "-";
      tr.innerHTML = `
        <td><code>${escapeHtml(item.id)}</code></td>
        <td>${escapeHtml(item.name)}</td>
        <td>${escapeHtml(item.templateType || item.itemType || "")}</td>
        <td>${escapeHtml(item.status || "")}</td>
        <td>${escapeHtml(String(item.version ?? ""))}</td>
        <td>${escapeHtml(item.entityScope || "-")}</td>
        <td>${escapeHtml(String(applicability))}</td>
        <td class="text-end"><button type="button" class="btn btn-sm btn-primary library-inst-select-row">Select</button></td>`;
      tr.querySelector(".library-inst-select-row")?.addEventListener("click", () => {
        setSelection(item);
        pickerModal?.hide();
      });
      pickerTbody.appendChild(tr);
    });
  }

  async function loadCatalogForPicker() {
    const tt = currentTemplateTypeFilter();
    const query = { page: 1, pageSize: 200 };
    if (tt && tt !== "BlueprintPack") query.templateType = tt;
    try {
      const data = await window.strategyLibraryApi.catalog(query);
      let items = data?.items || [];
      if (tt === "BlueprintPack") {
        items = items.filter(
          (x) => String(x.templateType || "").toLowerCase() === "blueprintpack" || String(x.itemType || "").toLowerCase() === "blueprintpack"
        );
      }
      catalogRows = items;
      renderPickerTable();
    } catch (err) {
      catalogRows = [];
      renderPickerTable();
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Catalog load failed") || "Catalog load failed", "error");
    }
  }

  async function run() {
    const sourceType = String(sourceTypeEl?.value || "");
    const sourceId = String(sourceIdEl?.value || "").trim();
    if (!sourceId) {
      notify("Select a template or blueprint from the catalog.", "warning");
      return;
    }
    try {
      if (sourceType === "BlueprintPack") {
        const payload = {
          blueprintPackId: sourceId,
          fullChain: Boolean(fullChainEl?.checked),
          allowDuplicates: Boolean(allowDuplicatesEl?.checked),
          selectedPackItemIds: [],
          defaultOverrides: parseOverrides(overridesEl?.value)
        };
        const result = await window.strategyLibraryApi.instantiateBlueprint(sourceId, payload);
        resultEl.textContent = JSON.stringify(result, null, 2);
      } else {
        const payload = {
          templateType: sourceType,
          templateId: sourceId,
          fullChain: Boolean(fullChainEl?.checked),
          allowDuplicates: Boolean(allowDuplicatesEl?.checked),
          defaultOverrides: parseOverrides(overridesEl?.value)
        };
        const result = await window.strategyLibraryApi.instantiateTemplate(sourceId, payload);
        resultEl.textContent = JSON.stringify(result, null, 2);
      }
      notify("Instantiation completed.");
    } catch (err) {
      resultEl.textContent = JSON.stringify(err?.payload || { message: "Instantiation failed." }, null, 2);
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Instantiation failed") || "Instantiation failed", "error");
    }
  }

  if (defaults.sourceType) {
    sourceTypeEl.value = defaults.sourceType;
  }
  if (defaults.sourceId && sourceIdEl) {
    sourceIdEl.value = defaults.sourceId;
  }

  sourceTypeEl?.addEventListener("change", () => {
    setSelection(null);
  });

  openPickerBtn?.addEventListener("click", async () => {
    pickerSearchEl && (pickerSearchEl.value = "");
    await loadCatalogForPicker();
    pickerModal?.show();
  });

  pickerSearchEl?.addEventListener("input", () => renderPickerTable());

  clearBtn?.addEventListener("click", () => setSelection(null));

  runBtn?.addEventListener("click", run);

  async function resolveSelectionFromUrl() {
    const id = String(sourceIdEl?.value || "").trim();
    if (!id) {
      renderSummary();
      return;
    }
    try {
      await loadCatalogForPicker();
      const match = catalogRows.find((x) => String(x.id) === id);
      if (match) selectedRow = match;
    } catch {
      selectedRow = null;
    }
    renderSummary();
  }

  void resolveSelectionFromUrl();
})(window, document);
