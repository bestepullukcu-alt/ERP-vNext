(function (window, document) {
  "use strict";

  const fileEl = document.getElementById("library-import-file");
  const batchNameEl = document.getElementById("library-import-batch-name");
  const runBtn = document.getElementById("library-import-run");
  const approveBtn = document.getElementById("library-import-approve");
  const summaryEl = document.getElementById("library-import-summary");
  const issuesTbody = document.querySelector("#library-import-issues-table tbody");
  const notify = (m, k = "success") => window.enterpriseStrategyUi?.notify?.(m, k);
  let currentBatchId = "";

  function renderIssues(issues) {
    issuesTbody.innerHTML = "";
    (issues || []).forEach((issue) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${issue.severity || ""}</td>
        <td>${issue.sheetName || ""}</td>
        <td>${issue.rowNumber || ""}</td>
        <td>${issue.code || ""}</td>
        <td>${issue.message || ""}</td>`;
      issuesTbody.appendChild(tr);
    });
    if (!(issues || []).length) {
      const tr = document.createElement("tr");
      tr.innerHTML = '<td colspan="5" class="text-center text-muted py-3">No issues.</td>';
      issuesTbody.appendChild(tr);
    }
  }

  function renderBatch(batch) {
    if (!batch) {
      summaryEl.textContent = "No batch run yet.";
      approveBtn.disabled = true;
      return;
    }
    currentBatchId = batch.batchId || "";
    summaryEl.innerHTML = `
      Batch: <code>${batch.batchId || "-"}</code><br/>
      Name: ${batch.batchName || "-"}<br/>
      Status: ${batch.status || "-"}<br/>
      Rows: ${batch.totalRowsRead ?? 0}, Templates: ${batch.uniqueTemplatesCreated ?? 0}, Duplicates Collapsed: ${batch.duplicateRowsCollapsed ?? 0}`;
    approveBtn.disabled = !currentBatchId || String(batch.status || "").toLowerCase() === "approved";
    renderIssues(batch.issues || []);
  }

  async function runImport() {
    const file = fileEl?.files?.[0];
    if (!file) {
      notify("Choose a workbook first.", "warning");
      return;
    }
    try {
      if (!window.enterpriseWorkbookIo?.parseFile) {
        notify("Workbook parser is not available.", "error");
        return;
      }
      const parsed = await window.enterpriseWorkbookIo.parseFile(file);
      const payload = {
        batchName: String(batchNameEl?.value || "").trim() || file.name,
        sheets: parsed?.sheets || {}
      };
      const batch = await window.strategyLibraryApi.importWorkbook(payload);
      renderBatch(batch);
      notify("Library import completed.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Import failed") || "Import failed", "error");
    } finally {
      if (fileEl) fileEl.value = "";
    }
  }

  async function approveImport() {
    if (!currentBatchId) return;
    try {
      const batch = await window.strategyLibraryApi.approveImport(currentBatchId);
      renderBatch(batch);
      notify("Import batch approved.");
    } catch (err) {
      notify(window.enterpriseStrategyUi?.getErrorMessage(err, "Approve failed") || "Approve failed", "error");
    }
  }

  runBtn?.addEventListener("click", runImport);
  approveBtn?.addEventListener("click", approveImport);
  renderBatch(null);
})(window, document);
