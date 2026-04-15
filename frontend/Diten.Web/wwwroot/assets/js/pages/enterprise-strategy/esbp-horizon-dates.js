/**
 * ES&BP hierarchy horizon fields: flatpickr dd/mm/yyyy + calendar affordance.
 * Depends on window.flatpickr (loaded from layout).
 */
(function (window, document) {
  "use strict";

  function pad2(n) {
    return String(n).padStart(2, "0");
  }

  function isoFromDate(d) {
    if (!d || Number.isNaN(d.getTime())) return "";
    return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
  }

  function parseIsoToLocalDate(iso) {
    if (!iso) return null;
    const m = String(iso).trim().match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (!m) return null;
    const y = Number(m[1]);
    const mo = Number(m[2]);
    const d = Number(m[3]);
    const dt = new Date(y, mo - 1, d);
    if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return null;
    return dt;
  }

  function parseManualDmy(text) {
    const t = String(text || "").trim();
    if (!t) return null;
    const parts = t.split(/[./-]/).map((x) => x.trim());
    if (parts.length !== 3) return null;
    const d = Number(parts[0]);
    const mo = Number(parts[1]);
    const y = Number(parts[2]);
    if (!y || mo < 1 || mo > 12 || d < 1 || d > 31) return null;
    const dt = new Date(y, mo - 1, d);
    if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return null;
    return dt;
  }

  function getIsoFromInput(input) {
    if (!input) return "";
    const fp = input._flatpickr;
    if (fp && fp.selectedDates && fp.selectedDates[0]) return isoFromDate(fp.selectedDates[0]);
    return isoFromDate(parseManualDmy(input.value));
  }

  function setInputIso(input, iso, triggerChange = true) {
    if (!input) return;
    const d = parseIsoToLocalDate(iso);
    const fp = input._flatpickr;
    if (fp) {
      if (d) fp.setDate(d, Boolean(triggerChange));
      else fp.clear(Boolean(triggerChange));
      return;
    }
    if (!iso) {
      input.value = "";
      return;
    }
    const dd = parseIsoToLocalDate(iso);
    input.value = dd ? `${pad2(dd.getDate())}/${pad2(dd.getMonth() + 1)}/${dd.getFullYear()}` : "";
  }

  function bindCalendarButton(input, fp) {
    const group = input.closest(".esbp-horizon-input-group");
    const btn = group && group.querySelector(".esbp-horizon-calendar-btn");
    if (!btn || !fp) return;
    const open = (e) => {
      e.preventDefault();
      fp.open();
    };
    btn.addEventListener("click", open);
    btn.addEventListener("mousedown", open);
    btn.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") open(e);
    });
  }

  /**
   * Initialize all [data-esbp-horizon="1"] inputs under root (or document).
   */
  function initIn(root) {
    const scope = root && root.querySelectorAll ? root : document;
    const inputs = scope.querySelectorAll ? scope.querySelectorAll('.esbp-horizon-date[data-esbp-horizon="1"]') : [];
    inputs.forEach((input) => {
      if (input._flatpickr) {
        try {
          input._flatpickr.destroy();
        } catch (_) { /* ignore */ }
      }
      if (!window.flatpickr) {
        input.type = "date";
        return;
      }
      const fp = window.flatpickr(input, {
        dateFormat: "d/m/Y",
        allowInput: true,
        clickOpens: true,
        disableMobile: true,
        onChange: () => input.dispatchEvent(new Event("change", { bubbles: true }))
      });
      if (fp?.calendarContainer?.classList) fp.calendarContainer.classList.add("esbp-horizon-themed");
      bindCalendarButton(input, fp);
    });
  }

  window.esbpHorizonDates = {
    initIn,
    getIsoFromInput,
    setInputIso,
    parseIsoToLocalDate,
    isoFromDate
  };
})(window, document);
