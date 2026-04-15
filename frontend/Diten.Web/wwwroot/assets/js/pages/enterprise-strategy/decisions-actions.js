(function (window, document) {
  "use strict";

  const utils = window.enterpriseModalFormUtils;
  const modalEl = document.getElementById("decisionEditorModal");
  const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
  const table = document.getElementById("decisions-table");
  const errorEl = document.getElementById("decision-form-error");
  const filterReview = document.getElementById("decision-filter-review");
  const filterStatus = document.getElementById("decision-filter-status");
  const applyBtn = document.getElementById("decision-apply");
  const createBtn = document.getElementById("decision-create");
  const saveBtn = document.getElementById("decision-save");
  const workbook = window.enterpriseWorkbookOptions || {};
  const resolveUserId = (value) => workbook.userId?.(value) || String(value || "").trim();
  const resolveUserName = (value) => workbook.userDisplayName?.(value) || String(value || "").trim();

  const fields = {
    reviewId: document.getElementById("decision-review"),
    title: document.getElementById("decision-title"),
    goal: document.getElementById("decision-goal"),
    objective: document.getElementById("decision-objective"),
    kpi: document.getElementById("decision-kpi"),
    owner: document.getElementById("decision-owner"),
    dueDate: document.getElementById("decision-due-date"),
    status: document.getElementById("decision-status"),
    evidence: document.getElementById("decision-evidence"),
    rationale: document.getElementById("decision-rationale")
  };

  function validate(payload) {
    const errors = [];
    if (!payload.reviewId) { utils?.setFieldError?.(fields.reviewId, "Review ID is required."); errors.push("Review ID is required."); } else utils?.clearFieldError?.(fields.reviewId);
    if (!payload.title) { utils?.setFieldError?.(fields.title, "Decision title is required."); errors.push("Decision title is required."); } else utils?.clearFieldError?.(fields.title);
    if (!payload.owner) { utils?.setFieldError?.(fields.owner, "Owner is required."); errors.push("Owner is required."); } else utils?.clearFieldError?.(fields.owner);
    if (!payload.dueDate) { utils?.setFieldError?.(fields.dueDate, "Due date is required."); errors.push("Due date is required."); } else utils?.clearFieldError?.(fields.dueDate);
    return errors;
  }

  function payloadFromForm() {
    return {
      reviewId: String(fields.reviewId.value || "").trim(),
      title: String(fields.title.value || "").trim(),
      relatedGoalId: String(fields.goal.value || "").trim(),
      relatedObjectiveId: String(fields.objective.value || "").trim(),
      relatedKpiId: String(fields.kpi.value || "").trim(),
      owner: resolveUserId(fields.owner.value || ""),
      dueDate: fields.dueDate.value || "",
      status: String(fields.status.value || "Open"),
      evidence: String(fields.evidence.value || "").trim(),
      rationale: String(fields.rationale.value || "").trim()
    };
  }

  function clearForm() {
    Object.values(fields).forEach((x) => { if (x) x.value = ""; });
    fields.status.value = "Open";
    utils?.showValidationSummary?.(errorEl, []);
    utils?.clearFieldErrors?.(modalEl);
  }

  function render(rows) {
    table.innerHTML = rows.map((row) => {
      const actions = window.enterpriseRowActionsMenu?.render?.(row.id, [
        { action: "review", label: "Open Review Pack", href: `/management-governance/enterprise-strategy-business-performance/reviews/pack?reviewId=${encodeURIComponent(row.reviewId)}` },
        { action: "toggle", label: row.isOpen ? "Mark Completed" : "Reopen" }
      ]) || "";
      return `<tr data-id="${row.id}">
        <td>${row.title}</td>
        <td>${row.reviewId}</td>
        <td>${row.relatedGoalId || "-"}</td>
        <td>${row.relatedObjectiveId || "-"}</td>
        <td>${row.relatedKpiId || "-"}</td>
        <td>${resolveUserName(row.owner) || "-"}</td>
        <td>${row.dueDate ? new Date(row.dueDate).toLocaleDateString() : "-"}</td>
        <td><span class="badge bg-label-${row.isOpen ? "warning" : "success"}">${row.status}</span></td>
        <td class="text-end es-row-actions-col">${actions}</td>
      </tr>`;
    }).join("");
  }

  async function load() {
    const query = { reviewId: String(filterReview.value || "").trim(), status: String(filterStatus.value || "").trim() };
    const rows = await window.strategyReviewsApi.decisions(query);
    render(Array.isArray(rows) ? rows : []);
  }

  async function save() {
    const payload = payloadFromForm();
    const errors = validate(payload);
    if (errors.length) {
      utils?.showValidationSummary?.(errorEl, ["Please complete the required fields highlighted below.", ...errors]);
      utils?.focusFirstInvalid?.(modalEl);
      return;
    }
    try {
      utils?.setSubmitting?.(saveBtn, true, "Create Decision", "Saving...");
      await window.strategyReviewsApi.createDecision(payload);
      modal?.hide();
      await load();
      window.enterpriseStrategyUi?.notify?.("Decision saved.");
    } catch (err) {
      const list = utils?.backendErrors?.(err, "Unable to save decision.") || ["Unable to save decision."];
      utils?.showValidationSummary?.(errorEl, list);
    } finally {
      utils?.setSubmitting?.(saveBtn, false, "Create Decision");
    }
  }

  table?.addEventListener("click", async (event) => {
    const actionEl = event.target.closest(".es-row-action-item");
    if (!actionEl) return;
    const rowId = actionEl.dataset.rowId;
    const action = actionEl.dataset.action;
    if (action !== "toggle") return;
    event.preventDefault();
    const tr = actionEl.closest("tr");
    const statusBadge = tr?.querySelector(".badge");
    const current = statusBadge?.textContent?.trim() || "Open";
    const next = current === "Completed" ? "Open" : "Completed";
    await window.strategyReviewsApi.updateDecisionStatus(rowId, next);
    await load();
  });

  applyBtn?.addEventListener("click", load);
  createBtn?.addEventListener("click", () => { clearForm(); modal?.show(); });
  saveBtn?.addEventListener("click", save);

  (async function init() {
    await workbook.ensureUsersLoaded?.();
    workbook.fillSelect?.(fields.owner, workbook.userOptions?.() || [], { placeholder: "Select owner" });
    await load();
  })().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load decisions & actions.") || "Unable to load decisions & actions.";
    table.innerHTML = `<tr><td colspan="9" class="text-danger">${message}</td></tr>`;
  });
})(window, document);
