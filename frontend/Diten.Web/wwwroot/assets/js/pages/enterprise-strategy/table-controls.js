(function (window, document) {
  "use strict";

  function safeParse(json, fallback) {
    if (!json) return fallback;
    try {
      const parsed = JSON.parse(json);
      return parsed ?? fallback;
    } catch {
      return fallback;
    }
  }

  function safeStorage() {
    return {
      get(key) {
        try { return window.localStorage.getItem(key); } catch { return null; }
      },
      set(key, value) {
        try { window.localStorage.setItem(key, value); } catch { /* no-op */ }
      }
    };
  }

  function create(config) {
    const storage = safeStorage();
    const columns = config.columns || [];
    const byKey = new Map(columns.map((c) => [c.key, c]));
    const defaultOrder = columns.map((c) => c.key);
    const defaultVisible = columns.filter((c) => c.defaultVisible !== false).map((c) => c.key);
    const storageKey = config.storageKey;
    const yearKeys = new Set(config.yearKeys || []);
    const btn = document.getElementById(config.columnsButtonId);
    const panelId = `${config.pageKey}-columns-panel`;
    let panel = document.getElementById(panelId);

    const persisted = safeParse(storage.get(storageKey), {});

    function normalizeKeys(list, fallback) {
      const raw = Array.isArray(list) ? list : [];
      const filtered = raw.filter((k) => byKey.has(k));
      return filtered.length ? filtered : [...fallback];
    }

    function ensureRequiredVisible(list) {
      const current = [...list];
      const required = columns.filter((c) => c.required).map((c) => c.key);
      if (!required.length) return current;
      const hasRequired = current.some((k) => required.includes(k));
      if (hasRequired) return current;
      return [...new Set([required[0], ...current])];
    }
    const state = {
      order: normalizeKeys(persisted.columnOrder || persisted.order, defaultOrder),
      visible: ensureRequiredVisible(normalizeKeys(persisted.visibleColumns || persisted.visible, defaultVisible)),
      sort: persisted.sort && persisted.sort.key ? persisted.sort : { key: "", dir: "" },
      filters: persisted.filters || {},
      pageSize: Number(persisted.pageSize || config.defaultPageSize || 25)
    };
    if (!byKey.has(state.sort.key)) state.sort = { key: "", dir: "" };

    function save() {
      storage.set(storageKey, JSON.stringify({
        columnOrder: state.order,
        visibleColumns: state.visible,
        sort: state.sort,
        pageSize: state.pageSize,
        filters: state.filters,
        // backward-compat for old readers
        order: state.order,
        visible: state.visible
      }));
    }

    function requiredVisibleCount() {
      return state.visible.filter((k) => byKey.get(k)?.required).length;
    }

    function canToggleOff(key) {
      const col = byKey.get(key);
      if (!col?.required) return true;
      return requiredVisibleCount() > 1;
    }

    function toggleColumn(key) {
      if (!byKey.has(key)) return;
      const idx = state.visible.indexOf(key);
      if (idx >= 0) {
        if (!canToggleOff(key)) return;
        state.visible.splice(idx, 1);
        if (state.sort.key === key) state.sort = { key: "", dir: "" };
      } else {
        state.visible.push(key);
      }
      save();
      renderPanel();
      config.onChange?.();
    }

    function showAll() {
      state.visible = [...defaultOrder];
      save();
      renderPanel();
      config.onChange?.();
    }

    function hideAllOptional() {
      const required = columns.filter((c) => c.required).map((c) => c.key);
      state.visible = required.length ? required : [...defaultVisible.slice(0, 1)];
      if (state.sort.key && !state.visible.includes(state.sort.key)) state.sort = { key: "", dir: "" };
      save();
      renderPanel();
      config.onChange?.();
    }

    function resetColumns() {
      state.order = [...defaultOrder];
      state.visible = [...defaultVisible];
      state.sort = { key: "", dir: "" };
      if (state.sort.key && !state.visible.includes(state.sort.key)) state.sort = { key: "", dir: "" };
      save();
      renderPanel();
      config.onChange?.();
    }

    function resetTable() {
      state.order = [...defaultOrder];
      state.visible = [...defaultVisible];
      state.sort = { key: "", dir: "" };
      state.filters = {};
      state.pageSize = Number(config.defaultPageSize || 25);
      save();
      renderPanel();
      config.onChange?.();
    }

    function moveColumn(key, direction) {
      const idx = state.order.indexOf(key);
      if (idx < 0) return;
      const next = direction === "up" ? idx - 1 : idx + 1;
      if (next < 0 || next >= state.order.length) return;
      const currentKey = state.order[idx];
      const swapKey = state.order[next];
      if (config.lockYearBlock) {
        const currentIsYear = yearKeys.has(currentKey);
        const swapIsYear = yearKeys.has(swapKey);
        if (currentIsYear !== swapIsYear) return;
      }
      state.order[idx] = swapKey;
      state.order[next] = currentKey;
      save();
      renderPanel();
      config.onChange?.();
    }

    function moveColumnTo(sourceKey, targetKey) {
      if (!byKey.has(sourceKey) || !byKey.has(targetKey) || sourceKey === targetKey) return;
      const sourceIdx = state.order.indexOf(sourceKey);
      const targetIdx = state.order.indexOf(targetKey);
      if (sourceIdx < 0 || targetIdx < 0) return;
      if (config.lockYearBlock) {
        const sourceIsYear = yearKeys.has(sourceKey);
        const targetIsYear = yearKeys.has(targetKey);
        if (sourceIsYear !== targetIsYear) return;
      }
      const next = [...state.order];
      next.splice(sourceIdx, 1);
      const targetIndexAfterRemove = next.indexOf(targetKey);
      next.splice(targetIndexAfterRemove, 0, sourceKey);
      state.order = next;
      save();
      renderPanel();
      config.onChange?.();
    }

    function getVisibleColumns() {
      const visible = state.order.filter((k) => state.visible.includes(k) && byKey.has(k)).map((k) => byKey.get(k));
      if (visible.length) return visible;
      state.order = [...defaultOrder];
      state.visible = [...ensureRequiredVisible(defaultVisible)];
      save();
      return state.order.filter((k) => state.visible.includes(k) && byKey.has(k)).map((k) => byKey.get(k));
    }

    function cycleSort(key) {
      if (state.sort.key !== key) state.sort = { key, dir: "asc" };
      else if (state.sort.dir === "asc") state.sort = { key, dir: "desc" };
      else if (state.sort.dir === "desc") state.sort = { key: "", dir: "" };
      else state.sort = { key, dir: "asc" };
      save();
      config.onChange?.();
    }

    function sortRows(rows, valueGetter) {
      const s = state.sort;
      if (!s.key || !s.dir) return [...rows];
      const dir = s.dir === "asc" ? 1 : -1;
      return [...rows].sort((a, b) => {
        const av = valueGetter(a, s.key);
        const bv = valueGetter(b, s.key);
        const an = Number(av);
        const bn = Number(bv);
        if (!Number.isNaN(an) && !Number.isNaN(bn)) return (an - bn) * dir;
        return String(av ?? "").localeCompare(String(bv ?? ""), undefined, { numeric: true, sensitivity: "base" }) * dir;
      });
    }

    function setFilters(filters) {
      state.filters = { ...filters };
      save();
    }

    function getFilters() {
      return { ...(state.filters || {}) };
    }

    function setPageSize(value) {
      const numeric = Number(value);
      if (!Number.isFinite(numeric) || numeric < 1) return;
      state.pageSize = numeric;
      save();
    }

    function getPageSize() {
      return Number(state.pageSize || config.defaultPageSize || 25);
    }

    function sortIndicator(key) {
      if (state.sort.key !== key) return "";
      return state.sort.dir === "asc" ? " ▲" : state.sort.dir === "desc" ? " ▼" : "";
    }

    function renderPanel() {
      if (!panel) return;
      panel.innerHTML = "";
      const actions = document.createElement("div");
      actions.className = "d-flex gap-2 mb-2";
      actions.innerHTML =
        '<button type="button" class="btn btn-sm btn-outline-secondary" data-action="show-all">Show all</button>' +
        '<button type="button" class="btn btn-sm btn-outline-secondary" data-action="hide-optional">Hide optional</button>' +
        '<button type="button" class="btn btn-sm btn-outline-secondary" data-action="reset-columns">Reset columns</button>' +
        '<button type="button" class="btn btn-sm btn-outline-secondary" data-action="reset-table">Reset table</button>';
      panel.appendChild(actions);
      const helper = document.createElement("div");
      helper.className = "small text-muted mb-2";
      helper.textContent = "Tip: drag table headers to reorder columns quickly.";
      panel.appendChild(helper);

      const list = document.createElement("div");
      list.className = "d-flex flex-column gap-1";
      state.order.forEach((key) => {
        const col = byKey.get(key);
        if (!col) return;
        const row = document.createElement("div");
        row.className = "d-flex align-items-center justify-content-between border rounded p-1";
        const checked = state.visible.includes(key);
        row.innerHTML =
          `<label class="form-check mb-0"><input class="form-check-input" type="checkbox" data-toggle-col="${key}" ${checked ? "checked" : ""} /><span class="form-check-label ms-1">${col.label}</span></label>` +
          `<span class="btn-group btn-group-sm"><button type="button" class="btn btn-outline-secondary" data-col-up="${key}">↑</button><button type="button" class="btn btn-outline-secondary" data-col-down="${key}">↓</button></span>`;
        list.appendChild(row);
      });
      panel.appendChild(list);

      panel.querySelector('[data-action="show-all"]')?.addEventListener("click", showAll);
      panel.querySelector('[data-action="hide-optional"]')?.addEventListener("click", hideAllOptional);
      panel.querySelector('[data-action="reset-columns"]')?.addEventListener("click", resetColumns);
      panel.querySelector('[data-action="reset-table"]')?.addEventListener("click", resetTable);
      panel.querySelectorAll("[data-toggle-col]").forEach((el) => el.addEventListener("change", () => toggleColumn(el.dataset.toggleCol)));
      panel.querySelectorAll("[data-col-up]").forEach((el) => el.addEventListener("click", () => moveColumn(el.dataset.colUp, "up")));
      panel.querySelectorAll("[data-col-down]").forEach((el) => el.addEventListener("click", () => moveColumn(el.dataset.colDown, "down")));
    }

    function ensurePanel() {
      if (panel) return;
      panel = document.createElement("div");
      panel.id = panelId;
      panel.className = "card p-2 position-absolute d-none";
      panel.style.zIndex = "1060";
      panel.style.minWidth = "320px";
      panel.style.maxHeight = "420px";
      panel.style.overflow = "auto";
      document.body.appendChild(panel);
    }

    function positionPanel() {
      if (!btn || !panel) return;
      const rect = btn.getBoundingClientRect();
      panel.style.top = `${window.scrollY + rect.bottom + 6}px`;
      panel.style.left = `${window.scrollX + Math.max(8, rect.left - 140)}px`;
    }

    function init() {
      ensurePanel();
      renderPanel();
      btn?.addEventListener("click", () => {
        positionPanel();
        panel.classList.toggle("d-none");
      });
      document.addEventListener("click", (e) => {
        if (panel.classList.contains("d-none")) return;
        if (panel.contains(e.target) || btn?.contains(e.target)) return;
        panel.classList.add("d-none");
      });
      window.addEventListener("resize", () => {
        if (!panel.classList.contains("d-none")) positionPanel();
      });
    }

    init();
    return {
      state,
      getVisibleColumns,
      cycleSort,
      sortRows,
      sortIndicator,
      setFilters,
      getFilters,
      setPageSize,
      getPageSize,
      moveColumnTo,
      resetColumns,
      resetTable,
      reset: resetTable
    };
  }

  window.enterpriseTableControls = { create };
})(window, document);
