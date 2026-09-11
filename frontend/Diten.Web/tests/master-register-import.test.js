const fs = require("fs");
const path = require("path");

/*
 * WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the register CSV import wizard's client behaviour: it renders the preview
 * counts the server returned, it never commits without an explicit confirmation (BL-367 window.showConfirm), and
 * commit is only reachable after a preview of the SAME file.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);

const SCRIPT = fs.readFileSync(
    web("wwwroot", "assets", "js", "DocumentManagement", "MasterRegister", "import.js"), "utf8");

const mount = () => {
    document.body.innerHTML = `
        <input type="hidden" name="__RequestVerificationToken" value="token" />
        <input type="file" id="registerImportFile" />
        <div id="registerImportFileInfo" class="d-none"></div>
        <ul id="registerImportSteps">
            <li class="qms-step active" data-step="1"></li>
            <li class="qms-step" data-step="2"></li>
            <li class="qms-step" data-step="3"></li>
        </ul>
        <button id="btnRegisterImportPreview"></button>
        <span id="registerImportPreviewSpinner" class="d-none"></span>
        <div id="registerImportSummaryCard" class="d-none">
            <span id="registerImportSummaryBadge"></span>
            <div id="registerImportAlreadyAppliedAlert" class="d-none"></div>
            <div id="registerImportSummaryCounts"></div>
            <div id="registerImportSummaryFindings"></div>
        </div>
        <div id="registerImportCommitCard" class="d-none">
            <button id="btnRegisterImportCommit" disabled></button>
            <span id="registerImportCommitSpinner" class="d-none"></span>
        </div>`;
};

const runScript = () => {
    // eslint-disable-next-line no-new-func
    new Function("window", "document", SCRIPT)(global.window, global.document);
};

const csvFile = (name = "register.csv") => {
    const file = new File(["document_uid,document_code\nUID-1,C-1\n"], name, { type: "text/csv" });
    return file;
};

const setFile = (file) => {
    const input = document.getElementById("registerImportFile");
    Object.defineProperty(input, "files", { value: [file], configurable: true });
    input.dispatchEvent(new Event("change"));
};

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("Document Master Register import wizard", () => {
    beforeEach(() => {
        document.body.innerHTML = "";
        mount();
        window.L10n = {
            CommitConfirmMessage: "This writes {0} row(s).",
            PreviewTotalRows: "Total rows", PreviewCreated: "New", PreviewUpdated: "Updated",
            PreviewUnchanged: "Unchanged", PreviewBlocked: "Blocked", PreviewCitableYes: "Citable",
            PreviewCitableNo: "Not citable", ImportFileRequired: "Select a CSV file first.",
            ImportInvalidFileType: "Only .csv files are accepted.", AlreadyImportedWarning: "Already imported on {0} by {1}.",
            CommitImport: "Commit import", CommitSucceeded: "Import committed.",
        };
        window.showToast = vi.fn();
        window.showConfirm = vi.fn();
        window.DitenDataTable = undefined; // history table init is opt-in; skip it in this unit test
        window.DtDefaults = undefined;
        global.fetch = vi.fn();
    });

    it("requires a file before previewing", () => {
        runScript();
        document.getElementById("btnRegisterImportPreview").click();
        expect(window.showToast).toHaveBeenCalledWith("Select a CSV file first.", "error");
        expect(global.fetch).not.toHaveBeenCalled();
    });

    it("rejects a non-csv file client-side without calling the server", () => {
        runScript();
        setFile(csvFile("register.xlsx"));
        document.getElementById("btnRegisterImportPreview").click();
        expect(window.showToast).toHaveBeenCalledWith("Only .csv files are accepted.", "error");
        expect(global.fetch).not.toHaveBeenCalled();
    });

    it("renders the preview counts the server returned and enables commit", async () => {
        setFile(csvFile());
        runScript();
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({
                isSuccessful: true,
                data: {
                    fileName: "register.csv", contentHash: "abc123", totalRows: 5, created: 2, updated: 1,
                    unchanged: 2, blocked: 0, citableByQualityDecisionYes: 4, citableByQualityDecisionNo: 1,
                    missingColumns: [], errors: [], lifecycleDistribution: { Draft: 5 },
                    alreadyImported: false, alreadyImportedAt: null, alreadyImportedBy: null,
                },
            }),
        });

        document.getElementById("btnRegisterImportPreview").click();
        await flush(); await flush();

        expect(global.fetch).toHaveBeenCalledWith(
            "/DocumentManagement/MasterRegister/api/import/dry-run", expect.objectContaining({ method: "POST" }));
        expect(document.getElementById("registerImportSummaryCard").classList.contains("d-none")).toBe(false);
        expect(document.getElementById("registerImportSummaryCounts").textContent).toContain("5");
        expect(document.getElementById("registerImportSummaryCounts").textContent).toContain("2");
        // MUTATION GUARD: commit must be enabled after a clean (error-free) preview.
        expect(document.getElementById("btnRegisterImportCommit").disabled).toBe(false);
    });

    it("shows the already-imported warning without disabling commit", async () => {
        setFile(csvFile());
        runScript();
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({
                isSuccessful: true,
                data: {
                    fileName: "register.csv", contentHash: "abc123", totalRows: 5, created: 0, updated: 0,
                    unchanged: 5, blocked: 0, citableByQualityDecisionYes: 5, citableByQualityDecisionNo: 0,
                    missingColumns: [], errors: [], lifecycleDistribution: { Draft: 5 },
                    alreadyImported: true, alreadyImportedAt: "2026-09-01T00:00:00Z", alreadyImportedBy: "qa@diten.test",
                },
            }),
        });

        document.getElementById("btnRegisterImportPreview").click();
        await flush(); await flush();

        const alert = document.getElementById("registerImportAlreadyAppliedAlert");
        expect(alert.classList.contains("d-none")).toBe(false);
        expect(alert.textContent).toContain("qa@diten.test");
    });

    it("never commits without an explicit confirmation, and uses window.showConfirm (not window.confirm)", async () => {
        /*
         * MUTATION GUARD: call doCommit() directly from the click handler (skip showConfirm) and this goes red —
         * fetch to the commit endpoint would fire with zero user confirmation in between.
         */
        setFile(csvFile());
        runScript();
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({
                isSuccessful: true,
                data: {
                    fileName: "register.csv", contentHash: "abc123", totalRows: 5, created: 5, updated: 0,
                    unchanged: 0, blocked: 0, citableByQualityDecisionYes: 5, citableByQualityDecisionNo: 0,
                    missingColumns: [], errors: [], lifecycleDistribution: {}, alreadyImported: false,
                    alreadyImportedAt: null, alreadyImportedBy: null,
                },
            }),
        });
        document.getElementById("btnRegisterImportPreview").click();
        await flush(); await flush();

        document.getElementById("btnRegisterImportCommit").click();

        expect(window.showConfirm).toHaveBeenCalledTimes(1);
        expect(window.showConfirm.mock.calls[0][0]).toContain("5 row(s)");
        // The confirm callback was captured but never invoked (this stub does not call it) — so the commit
        // request must NOT have fired yet.
        expect(global.fetch).toHaveBeenCalledTimes(1); // only the earlier dry-run call
    });

    it("commits only after the confirmation callback runs, sending the previewed hash", async () => {
        setFile(csvFile());
        runScript();
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({
                isSuccessful: true,
                data: {
                    fileName: "register.csv", contentHash: "the-previewed-hash", totalRows: 5, created: 5,
                    updated: 0, unchanged: 0, blocked: 0, citableByQualityDecisionYes: 5, citableByQualityDecisionNo: 0,
                    missingColumns: [], errors: [], lifecycleDistribution: {}, alreadyImported: false,
                    alreadyImportedAt: null, alreadyImportedBy: null,
                },
            }),
        });
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({ isSuccessful: true, data: { batchId: "b1" } }),
        });

        document.getElementById("btnRegisterImportPreview").click();
        await flush(); await flush();

        window.showConfirm.mockImplementation((_msg, onConfirm) => onConfirm());
        document.getElementById("btnRegisterImportCommit").click();
        await flush(); await flush();

        expect(global.fetch).toHaveBeenCalledTimes(2);
        const [, options] = global.fetch.mock.calls[1];
        const sentHash = options.body.get("expectedContentHash");
        expect(sentHash).toBe("the-previewed-hash");
        expect(window.showToast).toHaveBeenCalledWith("Import committed.", "success");
    });

    it("re-guards on commit: a file swapped after preview cannot be committed against the stale hash", async () => {
        setFile(csvFile("first.csv"));
        runScript();
        global.fetch.mockResolvedValueOnce({
            ok: true,
            json: async () => ({
                isSuccessful: true,
                data: {
                    fileName: "first.csv", contentHash: "hash-1", totalRows: 1, created: 1, updated: 0,
                    unchanged: 0, blocked: 0, citableByQualityDecisionYes: 1, citableByQualityDecisionNo: 0,
                    missingColumns: [], errors: [], lifecycleDistribution: {}, alreadyImported: false,
                    alreadyImportedAt: null, alreadyImportedBy: null,
                },
            }),
        });
        document.getElementById("btnRegisterImportPreview").click();
        await flush(); await flush();

        // Swap the file WITHOUT re-previewing — the change handler disables commit again.
        setFile(csvFile("second.csv"));
        expect(document.getElementById("btnRegisterImportCommit").disabled).toBe(true);
    });
});
