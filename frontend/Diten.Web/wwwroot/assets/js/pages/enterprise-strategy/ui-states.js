(function (window) {
  "use strict";

  function getErrorMessage(err, fallback) {
    const code = err?.payload?.error?.code;
    const details = err?.payload?.error?.details || {};
    const firstDetail = Object.values(details).flat?.()[0];
    if (code === "STALE_VERSION") return "Record has changed. Reload and retry.";
    return firstDetail || fallback || "Request failed";
  }

  function setText(el, text) {
    if (!el) return;
    el.textContent = text || "";
  }

  function setHtml(el, html) {
    if (!el) return;
    el.innerHTML = html || "";
  }

  function ensureToastHost() {
    let host = document.getElementById("es-toast-host");
    if (host) return host;
    host = document.createElement("div");
    host.id = "es-toast-host";
    host.style.position = "fixed";
    host.style.top = "20px";
    host.style.right = "20px";
    host.style.zIndex = "11000";
    host.style.display = "flex";
    host.style.flexDirection = "column";
    host.style.gap = "8px";
    document.body.appendChild(host);
    return host;
  }

  function notify(message, kind = "success") {
    if (window.Notiflix?.Notify) {
      if (kind === "error") Notiflix.Notify.failure(message);
      else if (kind === "warning") Notiflix.Notify.warning(message);
      else Notiflix.Notify.success(message);
      return;
    }
    const host = ensureToastHost();
    const toast = document.createElement("div");
    toast.textContent = message || "";
    toast.style.minWidth = "260px";
    toast.style.maxWidth = "420px";
    toast.style.padding = "10px 12px";
    toast.style.borderRadius = "8px";
    toast.style.boxShadow = "0 8px 20px rgba(0,0,0,0.18)";
    toast.style.color = "#fff";
    toast.style.fontSize = "13px";
    toast.style.background = kind === "error" ? "#dc3545" : kind === "warning" ? "#fd7e14" : "#198754";
    host.appendChild(toast);
    window.setTimeout(() => toast.remove(), 2600);
  }

  function ensureConfirmModal() {
    let modalEl = document.getElementById("es-confirm-modal");
    if (modalEl) return modalEl;
    modalEl = document.createElement("div");
    modalEl.id = "es-confirm-modal";
    modalEl.className = "modal fade";
    modalEl.tabIndex = -1;
    modalEl.setAttribute("aria-hidden", "true");
    modalEl.innerHTML =
      '<div class="modal-dialog modal-dialog-centered">' +
      '  <div class="modal-content">' +
      '    <div class="modal-header">' +
      '      <h5 class="modal-title" id="es-confirm-title">Confirm</h5>' +
      '      <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>' +
      "    </div>" +
      '    <div class="modal-body">' +
      '      <p class="mb-0" id="es-confirm-message"></p>' +
      "    </div>" +
      '    <div class="modal-footer">' +
      '      <button type="button" class="btn btn-outline-secondary" id="es-confirm-cancel" data-bs-dismiss="modal">Cancel</button>' +
      '      <button type="button" class="btn btn-primary" id="es-confirm-ok">Confirm</button>' +
      "    </div>" +
      "  </div>" +
      "</div>";
    document.body.appendChild(modalEl);
    return modalEl;
  }

  function confirm(options) {
    const opts = options || {};
    const modalEl = ensureConfirmModal();
    const titleEl = document.getElementById("es-confirm-title");
    const messageEl = document.getElementById("es-confirm-message");
    const cancelBtn = document.getElementById("es-confirm-cancel");
    const okBtn = document.getElementById("es-confirm-ok");

    titleEl.textContent = opts.title || "Confirm";
    messageEl.textContent = opts.message || "Are you sure?";
    cancelBtn.textContent = opts.cancelLabel || "Cancel";
    okBtn.textContent = opts.confirmLabel || "Confirm";
    okBtn.className = "btn";
    okBtn.classList.add(opts.confirmKind === "danger" ? "btn-danger" : "btn-primary");

    const modal = window.bootstrap?.Modal?.getOrCreateInstance(modalEl, { backdrop: "static" });
    if (!modal) {
      return Promise.resolve(window.confirm(opts.message || "Are you sure?"));
    }

    return new Promise((resolve) => {
      let settled = false;
      const settle = (value) => {
        if (settled) return;
        settled = true;
        cleanup();
        resolve(value);
      };
      const onCancel = () => settle(false);
      const onOk = () => {
        settle(true);
        modal.hide();
      };
      const onHidden = () => settle(false);
      const cleanup = () => {
        cancelBtn.removeEventListener("click", onCancel);
        okBtn.removeEventListener("click", onOk);
        modalEl.removeEventListener("hidden.bs.modal", onHidden);
      };

      cancelBtn.addEventListener("click", onCancel);
      okBtn.addEventListener("click", onOk);
      modalEl.addEventListener("hidden.bs.modal", onHidden);
      modal.show();
    });
  }

  window.enterpriseStrategyUi = {
    getErrorMessage,
    setText,
    setHtml,
    notify,
    confirm,
  };
})(window);
