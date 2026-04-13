(function (window, document) {
  "use strict";

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function ensureFilterSummaryHost(toolbarEl, pageKey) {
    if (!toolbarEl) return null;
    let host = document.getElementById(`${pageKey}-active-filters`);
    if (host) return host;
    host = document.createElement("div");
    host.id = `${pageKey}-active-filters`;
    host.className = "w-100 small text-muted mt-1";
    toolbarEl.appendChild(host);
    return host;
  }

  function renderFilterSummary(host, filterState) {
    if (!host) return;
    const active = Object.entries(filterState || {})
      .filter(([, value]) => {
        if (typeof value === "boolean") return value;
        return String(value ?? "").trim() !== "";
      })
      .map(([key, value]) => `${key}: ${String(value)}`);
    if (!active.length) {
      host.textContent = "No active filters";
      return;
    }
    host.innerHTML = active.map((chip) => `<span class="badge bg-label-secondary me-1 mb-1">${escapeHtml(chip)}</span>`).join("");
  }

  function ensurePagerHost(tableEl, pageKey) {
    if (!tableEl) return null;
    let host = document.getElementById(`${pageKey}-pager-host`);
    if (host) return host;
    host = document.createElement("div");
    host.id = `${pageKey}-pager-host`;
    host.className = "d-flex flex-wrap justify-content-between align-items-center gap-2 py-2 px-2 border-top";
    host.innerHTML = `
      <div class="small text-muted" id="${pageKey}-pager-count">0 rows</div>
      <div class="d-flex align-items-center gap-2">
        <label class="small text-muted mb-0" for="${pageKey}-page-size">Rows</label>
        <select id="${pageKey}-page-size" class="form-select form-select-sm" style="width:auto">
          <option value="15">15</option>
          <option value="25">25</option>
          <option value="50">50</option>
          <option value="100">100</option>
        </select>
        <button id="${pageKey}-page-prev" class="btn btn-sm btn-outline-secondary" type="button">Prev</button>
        <span class="small" id="${pageKey}-page-label">Page 1 / 1</span>
        <button id="${pageKey}-page-next" class="btn btn-sm btn-outline-secondary" type="button">Next</button>
      </div>`;
    const card = tableEl.closest(".card");
    if (card) card.appendChild(host);
    else tableEl.parentElement?.appendChild(host);
    return host;
  }

  function createPager(options) {
    const pageKey = options.pageKey;
    const tableEl = options.tableEl;
    const tableControls = options.tableControls;
    const onChange = options.onChange;
    const host = ensurePagerHost(tableEl, pageKey);
    const pageSizeSelect = document.getElementById(`${pageKey}-page-size`);
    const prevBtn = document.getElementById(`${pageKey}-page-prev`);
    const nextBtn = document.getElementById(`${pageKey}-page-next`);
    const countEl = document.getElementById(`${pageKey}-pager-count`);
    const labelEl = document.getElementById(`${pageKey}-page-label`);

    const state = {
      page: 1,
      pageSize: Number(tableControls?.getPageSize?.() || options.defaultPageSize || 25)
    };

    if (pageSizeSelect) {
      pageSizeSelect.value = String(state.pageSize);
      pageSizeSelect.addEventListener("change", () => {
        state.pageSize = Number(pageSizeSelect.value || 25);
        state.page = 1;
        tableControls?.setPageSize?.(state.pageSize);
        onChange?.();
      });
    }

    prevBtn?.addEventListener("click", () => {
      state.page = Math.max(1, state.page - 1);
      onChange?.();
    });
    nextBtn?.addEventListener("click", () => {
      state.page += 1;
      onChange?.();
    });

    function paginate(rows) {
      const total = rows.length;
      const totalPages = Math.max(1, Math.ceil(total / state.pageSize));
      state.page = Math.min(state.page, totalPages);
      const start = (state.page - 1) * state.pageSize;
      const paged = rows.slice(start, start + state.pageSize);
      if (countEl) countEl.textContent = `${total} filtered rows`;
      if (labelEl) labelEl.textContent = `Page ${state.page} / ${totalPages}`;
      if (prevBtn) prevBtn.disabled = state.page <= 1;
      if (nextBtn) nextBtn.disabled = state.page >= totalPages;
      return paged;
    }

    function resetToFirstPage() {
      state.page = 1;
    }

    return { paginate, resetToFirstPage, state, host };
  }

  function visibleExportColumns(tableControls, fallbackColumns) {
    const cols = tableControls?.getVisibleColumns?.() || fallbackColumns || [];
    return cols.filter((c) => c.key !== "actions");
  }

  function ensureResetButton(pageKey, anchorButton, onReset) {
    if (!anchorButton?.parentElement) return null;
    const id = `${pageKey}-reset-filters`;
    let btn = document.getElementById(id);
    if (!btn) {
      btn = document.createElement("button");
      btn.id = id;
      btn.type = "button";
      btn.className = "btn btn-sm btn-outline-secondary";
      btn.textContent = "Reset";
      anchorButton.insertAdjacentElement("afterend", btn);
    }
    btn.onclick = onReset;
    return btn;
  }

  function bindHeaderColumnDrag(rowEl, options) {
    if (!rowEl || !options?.onReorder) return;
    let draggingKey = "";
    rowEl.querySelectorAll("th[data-col-key]").forEach((th) => {
      const key = th.dataset.colKey;
      if (!key || key === "actions") return;
      th.draggable = true;
      th.classList.add("es-col-draggable");
      th.addEventListener("dragstart", (event) => {
        draggingKey = key;
        event.dataTransfer?.setData("text/plain", key);
        th.classList.add("es-col-dragging");
      });
      th.addEventListener("dragend", () => {
        draggingKey = "";
        rowEl.querySelectorAll("th").forEach((x) => x.classList.remove("es-col-drop-target", "es-col-dragging"));
      });
      th.addEventListener("dragover", (event) => {
        if (!draggingKey || draggingKey === key) return;
        event.preventDefault();
        th.classList.add("es-col-drop-target");
      });
      th.addEventListener("dragleave", () => {
        th.classList.remove("es-col-drop-target");
      });
      th.addEventListener("drop", (event) => {
        event.preventDefault();
        const source = draggingKey || event.dataTransfer?.getData("text/plain");
        th.classList.remove("es-col-drop-target");
        if (!source || source === key) return;
        options.onReorder(source, key);
      });
    });
  }

  function exportVisibleCsv(fileName, rows, columns, getCellValue) {
    const headers = columns.map((c) => c.label);
    const keys = columns.map((c) => c.key);
    const lines = [headers.join(",")];
    rows.forEach((row) => {
      const csvRow = keys.map((key) => {
        const value = getCellValue(row, key);
        return `"${String(value ?? "").replace(/"/g, '""')}"`;
      }).join(",");
      lines.push(csvRow);
    });
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
  }

  window.enterpriseTablePageUtils = {
    ensureFilterSummaryHost,
    renderFilterSummary,
    createPager,
    ensureResetButton,
    bindHeaderColumnDrag,
    visibleExportColumns,
    exportVisibleCsv
  };
})(window, document);
