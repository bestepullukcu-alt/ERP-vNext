(function (window, document) {
  "use strict";

  const idEl = document.getElementById("library-gov-item-id");
  const loadBtn = document.getElementById("library-gov-load");
  const submitReviewBtn = document.getElementById("library-gov-submit-review");
  const approveBtn = document.getElementById("library-gov-approve");
  const publishBtn = document.getElementById("library-gov-publish");
  const retireBtn = document.getElementById("library-gov-retire");
  const currentEl = document.getElementById("library-gov-current");
  const versionsTbody = document.querySelector("#library-gov-versions-table tbody");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);
  let isBlueprint = false;

  function renderVersions(rows) {
    versionsTbody.innerHTML = "";
    (rows || []).forEach((v) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${v.versionNumber ?? 0}</td>
        <td>${v.status || ""}</td>
        <td>${v.changeSummary || ""}</td>
        <td>${v.changedBy || ""}</td>
        <td>${v.changedAt || ""}</td>`;
      versionsTbody.appendChild(tr);
    });
    if (!(rows || []).length) {
      const tr = document.createElement("tr");
      tr.innerHTML = '<td colspan="5" class="text-center text-muted py-3">No versions found.</td>';
      versionsTbody.appendChild(tr);
    }
  }

  async function loadItem() {
    const id = String(idEl?.value || "").trim();
    if (!id) {
      notify("Enter an item ID first.", "warning");
      return;
    }
    try {
      let detail = null;
      try {
        detail = await window.strategyLibraryApi.template(id);
        isBlueprint = false;
      } catch {
        detail = await window.strategyLibraryApi.blueprint(id);
        isBlueprint = true;
      }
      currentEl.textContent = JSON.stringify(detail, null, 2);
      const versions = await window.strategyLibraryApi.templateVersions(id);
      renderVersions(versions);
      notify("Governance item loaded.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Load failed") || "Load failed", "error");
    }
  }

  async function act(action) {
    const id = String(idEl?.value || "").trim();
    if (!id) return;
    try {
      if (isBlueprint) {
        if (action === "publish") await window.strategyLibraryApi.publishBlueprint(id);
        else if (action === "retire") await window.strategyLibraryApi.retireBlueprint(id);
        else notify("Blueprint supports publish/retire in MVP.", "warning");
      } else {
        if (action === "submitReview") await window.strategyLibraryApi.submitReviewTemplate(id);
        if (action === "approve") await window.strategyLibraryApi.approveTemplate(id);
        if (action === "publish") await window.strategyLibraryApi.publishTemplate(id);
        if (action === "retire") await window.strategyLibraryApi.retireTemplate(id);
      }
      await loadItem();
      notify("Governance action applied.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Action failed") || "Action failed", "error");
    }
  }

  loadBtn?.addEventListener("click", loadItem);
  submitReviewBtn?.addEventListener("click", () => act("submitReview"));
  approveBtn?.addEventListener("click", () => act("approve"));
  publishBtn?.addEventListener("click", () => act("publish"));
  retireBtn?.addEventListener("click", () => act("retire"));

  const params = new URLSearchParams(window.location.search);
  const prefill = params.get("itemId");
  if (prefill) {
    idEl.value = prefill;
    loadItem();
  }
})(window, document);
