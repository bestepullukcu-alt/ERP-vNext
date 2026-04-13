(function (window, document) {
  "use strict";

  const reviewInput = document.getElementById("review-pack-id");
  const loadBtn = document.getElementById("review-pack-load");
  const stateEl = document.getElementById("review-pack-state");

  function list(id, rows) {
    const el = document.getElementById(id);
    el.innerHTML = (rows || []).length
      ? `<ul class="mb-0">${rows.map((x) => `<li>${x}</li>`).join("")}</ul>`
      : '<span class="text-muted">No items.</span>';
  }

  function readQueryReviewId() {
    const url = new URL(window.location.href);
    return String(url.searchParams.get("reviewId") || "").trim();
  }

  function render(pack) {
    document.getElementById("review-pack-summary").innerHTML =
      `<div><strong>${pack.title}</strong></div><div class="text-muted">${pack.summary || ""}</div>
       <div class="small mt-2"><strong>Review ID:</strong> ${pack.reviewId}</div>
       <div class="small"><strong>Goals:</strong> ${(pack.goalIds || []).join(", ") || "-"}</div>
       <div class="small"><strong>Objectives:</strong> ${(pack.objectiveIds || []).join(", ") || "-"}</div>
       <div class="small"><strong>KPIs:</strong> ${(pack.kpiIds || []).join(", ") || "-"}</div>`;
    list("review-pack-cascade", pack.cascadeHighlights);
    list("review-pack-variance", pack.varianceHighlights);
    list("review-pack-decisions", pack.decisionsRequired);
    stateEl.textContent = `Loaded review pack ${pack.reviewId}.`;
  }

  async function load() {
    stateEl.textContent = "Loading review pack...";
    const query = { reviewId: String(reviewInput?.value || "").trim() };
    const pack = await window.strategyReviewsApi.pack(query);
    render(pack || {});
  }

  const initialId = readQueryReviewId();
  if (initialId) reviewInput.value = initialId;
  loadBtn?.addEventListener("click", load);
  load().catch((err) => {
    const message = window.enterpriseStrategyUi?.getErrorMessage?.(err, "Unable to load review pack.") || "Unable to load review pack.";
    stateEl.textContent = message;
  });
})(window, document);
