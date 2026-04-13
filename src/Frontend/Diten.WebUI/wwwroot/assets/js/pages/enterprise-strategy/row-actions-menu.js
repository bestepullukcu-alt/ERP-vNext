(function (window) {
  "use strict";

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function render(rowId, items) {
    const key = escapeHtml(rowId || `row-${Date.now()}`);
    const menuId = `es-row-menu-${key.replace(/[^a-zA-Z0-9_-]/g, "")}`;
    const rows = (items || []).map((item) => {
      if (item.divider) return '<li><hr class="dropdown-divider"></li>';
      if (item.href) {
        return `<li><a class="dropdown-item es-row-action-item" href="${escapeHtml(item.href)}" data-action="${escapeHtml(item.action || "")}" data-row-id="${key}">${escapeHtml(item.label || "")}</a></li>`;
      }
      return `<li><button type="button" class="dropdown-item es-row-action-item" data-action="${escapeHtml(item.action || "")}" data-row-id="${key}">${escapeHtml(item.label || "")}</button></li>`;
    }).join("");

    return `<div class="dropdown es-row-actions">
      <button class="btn btn-sm btn-icon btn-text-secondary rounded-pill" type="button" data-bs-toggle="dropdown" data-bs-auto-close="true" aria-expanded="false" aria-label="Row actions" id="${menuId}">
        <i class="bx bx-dots-vertical-rounded"></i>
      </button>
      <ul class="dropdown-menu dropdown-menu-end shadow-sm border rounded-3" aria-labelledby="${menuId}">
        ${rows}
      </ul>
    </div>`;
  }

  window.enterpriseRowActionsMenu = {
    render
  };
})(window);
