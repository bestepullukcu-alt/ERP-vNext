(function (window, document) {
  "use strict";

  const state = { snapshot: null, rows: [] };
  const workbook = window.enterpriseWorkbookOptions || {};
  const el = {
    goal: document.getElementById("allocation-goal"),
    metric: document.getElementById("allocation-metric"),
    load: document.getElementById("allocation-load"),
    generate: document.getElementById("allocation-generate"),
    validate: document.getElementById("allocation-validate"),
    save: document.getElementById("allocation-save"),
    table: document.getElementById("allocation-table")
  };

  const toNumber = (v) => Number(v || 0);

  function recalcFinalTotal() {
    const total = state.rows.reduce((sum, row) => sum + toNumber(row.finalTarget), 0);
    document.getElementById("allocation-final-total").textContent = total.toFixed(2);
    return total;
  }

  function renderTable() {
    el.table.innerHTML = state.rows.map((row, idx) => `<tr data-idx="${idx}">
      <td>${row.levelType}</td>
      <td>${row.entityName}</td>
      <td>${workbook.companyDisplayName?.(row.companyId) || row.companyId || "-"}</td>
      <td class="text-end">${toNumber(row.generatedTarget).toFixed(2)}</td>
      <td class="text-end"><input type="number" class="form-control form-control-sm allocation-manual text-end" value="${toNumber(row.manualTarget).toFixed(2)}" /></td>
      <td class="text-end"><input type="number" class="form-control form-control-sm allocation-final text-end" value="${toNumber(row.finalTarget).toFixed(2)}" /></td>
    </tr>`).join("");
    recalcFinalTotal();
  }

  function readTableRows() {
    Array.from(el.table.querySelectorAll("tr")).forEach((tr) => {
      const idx = Number(tr.dataset.idx);
      const manual = tr.querySelector(".allocation-manual");
      const final = tr.querySelector(".allocation-final");
      if (!state.rows[idx]) return;
      state.rows[idx].manualTarget = toNumber(manual?.value);
      state.rows[idx].finalTarget = toNumber(final?.value);
    });
  }

  function validateTotals() {
    readTableRows();
    const expected = toNumber(state.snapshot?.parentTarget);
    const actual = recalcFinalTotal();
    const diff = Math.abs(expected - actual);
    const message = diff < 0.0001
      ? `Valid: allocation total matches parent target (${expected.toFixed(2)}).`
      : `Mismatch: expected ${expected.toFixed(2)}, allocated ${actual.toFixed(2)}.`;
    document.getElementById("allocation-validation").textContent = message;
    document.getElementById("allocation-validation").className = `h6 mb-0 ${diff < 0.0001 ? "text-success" : "text-danger"}`;
  }

  function generateEven() {
    if (!state.rows.length) return;
    const parent = toNumber(state.snapshot?.parentTarget);
    const share = parent / state.rows.length;
    state.rows = state.rows.map((x) => ({ ...x, generatedTarget: share, finalTarget: share }));
    renderTable();
    validateTotals();
  }

  async function load() {
    const query = { goalId: String(el.goal?.value || "").trim(), metric: String(el.metric?.value || "").trim() };
    const snapshot = await window.strategyCascadeApi.targetAllocation(query);
    state.snapshot = snapshot;
    state.rows = (snapshot?.allocations || []).map((x) => ({ ...x }));
    document.getElementById("allocation-parent-target").textContent = String(toNumber(snapshot?.parentTarget).toFixed(2));
    document.getElementById("allocation-validation").textContent = "Not checked";
    renderTable();
  }

  el.table?.addEventListener("input", (event) => {
    if (event.target.closest(".allocation-manual") || event.target.closest(".allocation-final")) {
      readTableRows();
      recalcFinalTotal();
    }
  });
  el.load?.addEventListener("click", load);
  el.generate?.addEventListener("click", generateEven);
  el.validate?.addEventListener("click", validateTotals);
  el.save?.addEventListener("click", () => {
    validateTotals();
    window.enterpriseStrategyUi?.notify?.("Target allocation snapshot saved locally for this session.", "info");
  });

  load().catch((err) => {
    const msg = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load target allocation.") || "Unable to load target allocation.";
    document.getElementById("allocation-validation").textContent = msg;
  });
})(window, document);
