(function (window, document) {
  "use strict";

  function readFieldValue(el) {
    if (!el) return "";
    if (el.type === "checkbox") return Boolean(el.checked);
    if (el.multiple) {
      return Array.from(el.selectedOptions || []).map((opt) => String(opt.value || "").trim()).filter(Boolean);
    }
    return String(el.value ?? "").trim();
  }

  function writeFieldValue(el, value) {
    if (!el) return;
    if (el.type === "checkbox") {
      el.checked = Boolean(value);
      return;
    }
    if (el.multiple) {
      const values = new Set(Array.isArray(value) ? value.map((v) => String(v)) : []);
      Array.from(el.options || []).forEach((opt) => { opt.selected = values.has(String(opt.value || "")); });
      if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) window.jQuery(el).trigger("change.select2");
      return;
    }
    el.value = value == null ? "" : String(value);
    if (window.jQuery && window.jQuery(el).hasClass("select2-hidden-accessible")) window.jQuery(el).trigger("change.select2");
  }

  function isEmptyValue(value) {
    if (Array.isArray(value)) return value.length === 0;
    if (typeof value === "boolean") return value === false;
    return String(value ?? "").trim() === "";
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function create(options) {
    const drawerEl = document.getElementById(options.drawerId);
    const triggerEl = document.getElementById(options.triggerId);
    const applyBtn = document.getElementById(options.applyButtonId);
    const cancelBtn = document.getElementById(options.cancelButtonId);
    const clearBtn = document.getElementById(options.clearButtonId);
    const chipHost = document.getElementById(options.chipHostId);
    if (!drawerEl || !triggerEl) return null;

    const offcanvas = window.bootstrap?.Offcanvas ? new window.bootstrap.Offcanvas(drawerEl) : null;
    const fields = options.fields || {};
    const labels = options.labels || {};
    const defaults = options.defaults || {};
    let appliedState = {};

    function snapshot() {
      const out = {};
      Object.entries(fields).forEach(([key, el]) => {
        out[key] = readFieldValue(el);
      });
      return out;
    }

    function restore(state) {
      Object.entries(fields).forEach(([key, el]) => {
        writeFieldValue(el, state[key]);
      });
    }

    function clearFields() {
      Object.entries(fields).forEach(([key, el]) => {
        writeFieldValue(el, defaults[key]);
      });
    }

    function renderChips(state) {
      if (!chipHost) return;
      const active = Object.entries(state || {}).filter(([, value]) => !isEmptyValue(value));
      if (!active.length) {
        chipHost.innerHTML = "";
        return;
      }
      chipHost.innerHTML =
        `<div class="esbp-filter-chip-row">` +
        active.map(([key, value]) => {
          const text = Array.isArray(value) ? value.join(", ") : String(value);
          return `<span class="esbp-filter-chip"><span>${escapeHtml(labels[key] || key)}: ${escapeHtml(text)}</span><button type="button" data-filter-key="${escapeHtml(key)}" aria-label="Remove ${escapeHtml(labels[key] || key)} filter">&times;</button></span>`;
        }).join("") +
        `<button type="button" class="esbp-filter-clear-all" data-filter-clear-all="true">Clear all</button>` +
        `</div>`;
      chipHost.querySelectorAll("[data-filter-key]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const key = btn.getAttribute("data-filter-key");
          const nextState = { ...appliedState, [key]: defaults[key] };
          restore(nextState);
          appliedState = snapshot();
          options.onApply?.(appliedState, { source: "chip-remove", key });
          renderChips(appliedState);
        });
      });
      chipHost.querySelector("[data-filter-clear-all='true']")?.addEventListener("click", () => {
        clearFields();
        appliedState = snapshot();
        options.onApply?.(appliedState, { source: "clear-all" });
        renderChips(appliedState);
      });
    }

    function initSelect2InsideDrawer() {
      if (!window.jQuery || !window.jQuery.fn?.select2) return;
      const $ = window.jQuery;
      $(drawerEl).find("select.select2").each(function () {
        const $el = $(this);
        if ($el.hasClass("select2-hidden-accessible")) return;
        $el.select2({
          width: "100%",
          dropdownParent: $(drawerEl),
          closeOnSelect: !this.multiple,
          placeholder: this.multiple ? "Select..." : "Choose..."
        });
      });
    }

    triggerEl.addEventListener("click", () => {
      restore(appliedState);
      initSelect2InsideDrawer();
      offcanvas?.show();
    });

    cancelBtn?.addEventListener("click", () => {
      restore(appliedState);
      offcanvas?.hide();
    });

    clearBtn?.addEventListener("click", () => {
      clearFields();
    });

    applyBtn?.addEventListener("click", () => {
      appliedState = snapshot();
      renderChips(appliedState);
      options.onApply?.(appliedState, { source: "drawer-apply" });
      offcanvas?.hide();
    });

    drawerEl.addEventListener("shown.bs.offcanvas", initSelect2InsideDrawer);

    appliedState = snapshot();
    renderChips(appliedState);

    return {
      open: () => offcanvas?.show(),
      close: () => offcanvas?.hide(),
      getAppliedState: () => ({ ...appliedState }),
      setAppliedState: (state) => {
        appliedState = { ...defaults, ...(state || {}) };
        restore(appliedState);
        renderChips(appliedState);
      },
      renderChips: (state) => renderChips(state || appliedState),
      clearFields
    };
  }

  window.enterpriseFilterDrawer = { create };
})(window, document);
