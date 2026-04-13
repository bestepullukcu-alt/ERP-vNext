(function (window, document) {
  "use strict";

  function showValidationSummary(errorEl, errors) {
    if (!errorEl) return;
    const list = (errors || []).filter(Boolean);
    if (!list.length) {
      errorEl.classList.add("d-none");
      errorEl.innerHTML = "";
      return;
    }
    errorEl.classList.remove("d-none");
    errorEl.innerHTML = `<strong>Please fix the following:</strong><ul class="mb-0">${list.map((e) => `<li>${String(e)}</li>`).join("")}</ul>`;
  }

  function backendErrors(err, fallback) {
    const output = [];
    const payload = err?.payload || null;
    const details =
      payload?.error?.details ||
      payload?.error?.Details ||
      payload?.Error?.details ||
      payload?.Error?.Details ||
      payload?.errors ||
      payload?.Errors ||
      err?.error?.details ||
      err?.error?.Details ||
      err?.details ||
      err?.Details;
    if (details && typeof details === "object") {
      Object.values(details).forEach((value) => {
        if (Array.isArray(value)) value.forEach((v) => output.push(String(v)));
        else if (value !== null && value !== undefined && String(value).trim()) output.push(String(value));
      });
    }
    if (!output.length && payload?.message) output.push(String(payload.message));
    if (!output.length && payload?.detail) output.push(String(payload.detail));
    if (!output.length && payload?.title) output.push(String(payload.title));
    if (!output.length && err?.message) output.push(String(err.message));
    if (!output.length && fallback) output.push(String(fallback));
    return output;
  }

  function applyBackendFieldErrors(err, fieldMap) {
    const payload = err?.payload || null;
    const details =
      payload?.error?.details ||
      payload?.error?.Details ||
      payload?.Error?.details ||
      payload?.Error?.Details ||
      payload?.errors ||
      payload?.Errors ||
      err?.error?.details ||
      err?.error?.Details ||
      err?.details ||
      err?.Details;
    if (!details || typeof details !== "object") return 0;
    let count = 0;
    Object.entries(details).forEach(([key, value]) => {
      const el = fieldMap?.[key] || fieldMap?.[String(key || "").toLowerCase()];
      if (!el) return;
      const msg = Array.isArray(value) ? value[0] : value;
      if (!msg) return;
      setFieldError(el, String(msg));
      count++;
    });
    return count;
  }

  function setSubmitting(button, submitting, defaultLabel, busyLabel) {
    if (!button) return;
    if (submitting) {
      button.dataset.defaultLabel = defaultLabel || button.textContent || "Save";
      button.textContent = busyLabel || "Saving...";
      button.disabled = true;
      return;
    }
    button.textContent = button.dataset.defaultLabel || defaultLabel || "Save";
    button.disabled = false;
  }

  function focusFirstInvalid(modalEl) {
    if (!modalEl) return;
    const target = modalEl.querySelector(".is-invalid, [aria-invalid='true'], input:invalid, select:invalid, textarea:invalid");
    if (target && typeof target.focus === "function") target.focus();
  }

  function markFieldInvalid(el, invalid) {
    if (!el) return;
    el.classList.toggle("is-invalid", !!invalid);
    el.setAttribute("aria-invalid", invalid ? "true" : "false");
  }

  function getInlineErrorEl(el) {
    if (!el) return null;
    const inputGroup = el.closest(".input-group");
    const anchor = inputGroup && inputGroup.contains(el) ? inputGroup : el;
    let next = anchor.nextElementSibling;
    while (next && !next.classList?.contains("invalid-feedback")) next = next.nextElementSibling;
    if (next && next.classList?.contains("invalid-feedback")) return next;
    const node = document.createElement("div");
    node.className = "invalid-feedback es-inline-error";
    anchor.insertAdjacentElement("afterend", node);
    return node;
  }

  function setFieldError(el, message) {
    if (!el) return;
    markFieldInvalid(el, !!message);
    const inline = getInlineErrorEl(el);
    if (!inline) return;
    inline.textContent = String(message || "");
    if (message) inline.style.display = "block";
    else inline.style.display = "";
  }

  function clearFieldError(el) {
    setFieldError(el, "");
  }

  function clearFieldErrors(scopeEl) {
    if (!scopeEl) return;
    scopeEl.querySelectorAll(".is-invalid").forEach((el) => markFieldInvalid(el, false));
    scopeEl.querySelectorAll(".invalid-feedback.es-inline-error").forEach((el) => {
      el.textContent = "";
      el.style.display = "";
    });
  }

  function blockEnterSubmit(modalEl) {
    if (!modalEl) return;
    modalEl.addEventListener("keydown", (event) => {
      if (event.key !== "Enter") return;
      const tag = String(event.target?.tagName || "").toLowerCase();
      if (tag === "textarea" || tag === "button") return;
      event.preventDefault();
    });
  }

  function bindDirtyCloseGuard(modalEl, isDirtyGetter) {
    if (!modalEl) return;
    let allowClose = false;
    modalEl.addEventListener("hide.bs.modal", (event) => {
      if (allowClose || !isDirtyGetter?.()) return;
      event.preventDefault();
      (window.enterpriseStrategyUi?.confirm?.({
        title: "Discard changes?",
        message: "You have unsaved changes. Discard them?",
        confirmLabel: "Discard",
        confirmKind: "danger"
      }) || Promise.resolve(false)).then((ok) => {
        if (!ok) return;
        allowClose = true;
        window.bootstrap?.Modal?.getOrCreateInstance(modalEl)?.hide();
        allowClose = false;
      });
    });
  }

  window.enterpriseModalFormUtils = {
    showValidationSummary,
    backendErrors,
    applyBackendFieldErrors,
    setSubmitting,
    focusFirstInvalid,
    markFieldInvalid,
    setFieldError,
    clearFieldError,
    clearFieldErrors,
    blockEnterSubmit,
    bindDirtyCloseGuard
  };
})(window, document);
