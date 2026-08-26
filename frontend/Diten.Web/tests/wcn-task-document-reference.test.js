const fs = require("fs");
const path = require("path");

/*
 * DCP-005 slice 3 — a task citing a controlled document.
 *
 * The rule under test is one sentence long and fails silently: the six fields are frozen when the citation is
 * made, and nothing re-resolves them. On the client that means two things a test can actually see — the form
 * sends UIDS AND NOTHING ELSE, and the edit form hydrates from the task's own frozen values rather than from
 * the register.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);

const PICKER = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "document-references.js"), "utf8");
const FORM = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "form.js"), "utf8");
const FORM_PAGE = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "form-page.js"), "utf8");
const DETAILS = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "details-page.js"), "utf8");
const VIEW = fs.readFileSync(web("Views", "Tasks", "_Form.cshtml"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");

const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const loadPicker = () => {
    const module = { exports: {} };
    // eslint-disable-next-line no-new-func
    new Function("module", "window", "document", `${PICKER}\nmodule.exports = module.exports;`)(
        module, global.window, global.document);
    return module.exports;
};

const mount = () => {
    document.body.innerHTML = `
        <div data-task-document-section>
            <input id="taskDocumentSearch" />
            <p id="taskDocumentSuggestionNote" hidden></p>
            <ul id="taskDocumentResults"></ul>
            <ul id="taskDocumentChosen"></ul>
        </div>`;
    return document.querySelector("[data-task-document-section]");
};

const doc = (over = {}) => ({
    documentUid: "UID-0000104",
    documentCode: "GMG-QMS-SOP-0005",
    title: "Document Control",
    documentVersion: "1.0",
    status: "EFFECTIVE",
    linkableInErp: true,
    linkBlockedReason: null,
    ...over,
});

describe("citing a controlled document", () => {
    beforeEach(() => {
        global.window = global.window || {};
        window.TasksL10n = { t: (k) => k };
        window.TasksApi = {};
    });

    it("sends UIDs and never a title, a code or a version", () => {
        /*
         * MUTATION GUARD: make the form send the whole document object and this goes red.
         *
         * A client that sends titles and versions is a SECOND authority over what a citation says. The first
         * stale tab then writes a citation that matches no register version, and nobody can reproduce it.
         */
        const { controller } = loadPicker();
        const api = controller(mount());
        api.applySuggestion({ suggestions: [doc()], namedCount: 1, unresolvedUids: [], blockedSuggestions: [] });

        expect(api.uids()).toEqual(["UID-0000104"]);
        // The payload builder passes the UID list through untouched and adds nothing to it.
        expect(FORM).toContain("documentUids: normalizeDocumentUids(draft.documentUids)");
        expect(FORM).not.toMatch(/documentUids:\s*draft\.documentReferences/);
    });

    it("hydrates the edit form from the task's frozen values, not from the register", () => {
        /*
         * MUTATION GUARD — THE CENTRAL ONE on this side. Point `hydrate` at a search result instead of the
         * task's own `documentReferences` and this goes red.
         *
         * A form that re-read the register would render a refreshed title, the author would save it, and the
         * freeze would be gone with nothing on screen to notice.
         */
        const { controller } = loadPicker();
        const api = controller(mount());
        api.hydrate([{ documentUid: "UID-1", documentCode: "OLD-CODE", title: "Old title", documentVersion: "1.0" }]);

        const chip = document.querySelector("#taskDocumentChosen .tasks-docref-chip");
        expect(chip.textContent).toContain("OLD-CODE");
        expect(chip.textContent).toContain("Old title");
        expect(FORM_PAGE).toContain("hydrate(existing.data.documentReferences)");
    });

    it("shows a blocked row and refuses to let it be chosen", async () => {
        /*
         * MUTATION GUARD: render a blocked row as a `<button>` and this goes red.
         *
         * ⚠ THIS TEST WAS WRONG ONCE AND STAYED GREEN THROUGH THE MUTATION. It exercised the SUGGESTION path
         * (where a blocked row never reaches the results list at all) and then grepped the source for
         * `aria-disabled` — so it proved that a string exists in a file, which the mutation left untouched. The
         * measurement now runs the SEARCH path, which is the only path that renders a blocked row, and asks the
         * rendered DOM whether the row can be clicked.
         */
        const { controller } = loadPicker();
        const root = mount();
        window.TasksApi.searchDocuments = () => Promise.resolve({
            ok: true,
            data: [
                doc(),
                doc({
                    documentUid: "UID-0000115", documentCode: "GMG-GDP-SOP-0001", title: "Distribution Practice",
                    linkableInErp: false, linkBlockedReason: "planned, not yet issued",
                }),
            ],
        });
        controller(root);

        const box = document.querySelector("#taskDocumentSearch");
        box.value = "SOP";
        box.dispatchEvent(new window.Event("input"));
        await new Promise((resolve) => setTimeout(resolve, 320));

        const rows = [...document.querySelectorAll("#taskDocumentResults .tasks-docref-result")];
        expect(rows).toHaveLength(2);

        const blocked = rows.find((r) => r.textContent.includes("GMG-GDP-SOP-0001"));
        // Not choosable: no control inside it at all, and marked inert for anyone tabbing through.
        expect(blocked.querySelector("button")).toBeNull();
        expect(blocked.getAttribute("aria-disabled")).toBe("true");
        // And the register's own reason is READABLE TEXT, not a tooltip.
        expect(blocked.textContent).toContain("planned, not yet issued");

        // The citable one beside it is a real control, so the refusal is about the document and not the list.
        const citable = rows.find((r) => r.textContent.includes("GMG-QMS-SOP-0005"));
        expect(citable.querySelector("button[data-pick-uid]")).not.toBeNull();
    });

    it("says WHICH kind of empty an empty suggestion is", () => {
        /*
         * MUTATION GUARD: collapse the three empty states into one message and this goes red.
         *
         * MEASURED against the counterparty's seed on 2026-08-26: 15 of the 31 types have no citable governing
         * document — 1 names nothing, 7 name documents the register does not list, 7 name documents it refuses
         * to link. One empty box looks identical in all three and answers none of them.
         */
        const { controller } = loadPicker();
        const note = () => document.querySelector("#taskDocumentSuggestionNote").textContent;

        const a = controller(mount());
        a.applySuggestion({ suggestions: [], namedCount: 0, unresolvedUids: [], blockedSuggestions: [] });
        const namesNothing = note();

        const b = controller(mount());
        b.applySuggestion({ suggestions: [], namedCount: 1, unresolvedUids: ["UID-9"], blockedSuggestions: [] });
        const notInRegister = note();

        const c = controller(mount());
        c.applySuggestion({
            suggestions: [], namedCount: 1, unresolvedUids: [], blockedSuggestions: [doc({ linkableInErp: false })],
        });
        const allBlocked = note();

        expect(new Set([namesNothing, notInRegister, allBlocked]).size).toBe(3);
        expect(namesNothing).not.toBe("");
    });

    it("suggests without requiring — every suggested document can be removed", () => {
        /*
         * MUTATION GUARD: make a suggested document unremovable (drop the remove control, or ignore the click)
         * and this goes red. A type knows the usual answer, not the only one.
         */
        const { controller } = loadPicker();
        const api = controller(mount());
        api.applySuggestion({ suggestions: [doc()], namedCount: 1, unresolvedUids: [], blockedSuggestions: [] });
        expect(api.uids()).toHaveLength(1);

        document.querySelector("[data-remove-uid]").click();

        expect(api.uids()).toHaveLength(0);
    });

    it("draws no card on a task that cites nothing", () => {
        /*
         * MUTATION GUARD: draw the card unconditionally and this goes red.
         *
         * DCP-004 — do not announce a capability there is no data for. An empty "According to" heading on every
         * task in the product is a statement about the task, not about the feature.
         */
        expect(DETAILS).toMatch(/references\.length === 0.*\{ return ''; \}/s);
        expect(DETAILS).toContain("documentCard(task.documentReferences)");
    });

    it("does not repeat the contrast defect the document-list screen refused to copy", () => {
        // `--bs-secondary-color` measured 1.83:1 light / 2.02:1 dark on this product's own precedent — under AA.
        // The register's reason for refusing a citation is exactly the text that must stay readable.
        // ⚠ Measured on the DECLARATIONS, not on the prose: the block's own comment names the token in order
        // to explain why it is refused, and a test that matched that would be reading the explanation as the
        // defect.
        // Sliced from the COMMENT'S OPENING, not from the marker inside it — slicing mid-comment leaves an
        // orphaned body with no `/*` for the stripper to find, which is how this test first read its own prose
        // as a defect.
        const block = CSS.slice(CSS.indexOf("/* ── DCP-005 slice 3 — citing a controlled document"))
            .replace(/\/\*[\s\S]*?\*\//g, "");
        expect(block).not.toContain("--bs-secondary-color");
        expect(block).toContain("--bs-body-color");

        /*
         * ⚠ AND THE CHIP CARRIES NO FILL. Refusing the low-contrast TOKEN is not the same as having readable
         * text: with `--bs-body-color` on a `--bs-tertiary-bg` chip the measured ratio was 4.31:1 in the light
         * theme — under AA — while the same colour reads 5.19:1 on the page behind it. A tint is what put the
         * text on the wrong surface, so the chip has a border instead.
         *
         * MEASURED LIVE after the change, all four cells: chip 5.19 light / 6.54 dark, suggestion note the
         * same, blocked row 4.68 light / 4.78 dark (it keeps its tint on purpose — it has to be tellable apart
         * at a glance — and clears AA on it).
         */
        const chipRule = block.slice(block.indexOf(".tasks-docref-chip { border"));
        expect(chipRule.startsWith(".tasks-docref-chip { border")).toBe(true);
        expect(block).not.toMatch(/\.tasks-docref-chip \{ background:/);
    });

    it("keeps the twelve new strings in all seven languages", () => {
        const keys = [
            "FieldGoverningDocuments", "FieldGoverningDocumentsPlaceholder", "DocRefSuggestedByType",
            "DocRefNoneNamed", "DocRefNotInRegister", "DocRefAllBlocked", "DocRefBlocked", "DocRefRemove",
            "DocRefNoResults", "DocRefSectionTitle", "DocRefReferencedAt", "DocRefUnavailable",
        ];
        const missing = [];
        LANGS.forEach((lang) => {
            const resx = fs.readFileSync(web("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`), "utf8");
            keys.forEach((k) => { if (!resx.includes(`name="${k}"`)) { missing.push(`${lang}:${k}`); } });
        });
        expect(missing).toEqual([]);

        // ⚠ .NET prints an unresolved key as the KEY ITSELF — silently. So the bridge is checked too: a key in
        // the resx that never reaches the JS payload is a string the screen will never show.
        const bridge = fs.readFileSync(web("Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
        keys.forEach((k) => expect(bridge).toContain(`${k} = Localizer["${k}"]`));
    });

    it("has a browser-reachable route for the type's governing documents", () => {
        /*
         * MUTATION GUARD: remove the Web proxy method and this goes red.
         *
         * A route that exists on the SERVICE is invisible to the browser until the Web controller names it. That
         * gap has produced a live 404 three times in this module — the pin, the task types, the withdrawal.
         */
        const proxy = fs.readFileSync(web("Controllers", "TasksController.cs"), "utf8");
        expect(proxy).toContain('[HttpGet("api/task-types/{id:guid}/governing-documents")]');

        const service = fs.readFileSync(path.join(
            repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.API", "Controllers",
            "TasksController.cs"), "utf8");
        expect(service).toContain('[HttpGet("task-types/{id:guid}/governing-documents")]');
    });

    it("puts the citation question on the form where the type is answered", () => {
        expect(VIEW).toContain("data-task-document-section");
        expect(VIEW).toContain('Localizer["FieldGoverningDocuments"]');
        // FG-003 — classes, never an inline style.
        const section = VIEW.slice(VIEW.indexOf("data-task-document-section"), VIEW.indexOf("taskDescription"));
        expect(section).not.toContain('style="');
    });

    it("reads the task type off the form and writes it back on edit", () => {
        /*
         * MUTATION GUARD: delete either line and this goes red.
         *
         * ⚠ BOTH HALVES WERE BROKEN AND BOTH WERE INVISIBLE, found live on 2026-08-26 while wiring this slice:
         *   (1) `readForm` never read `taskTypeId`, so slice 1's picker wrote NOTHING — a task created with
         *       DEV-QMS visibly selected stored `taskTypeId: null`;
         *   (2) `writeForm` never wrote it back, so the edit form opened on "Tür yok" and a save — a full
         *       replace — would have cleared the type of a task that had one.
         * On a GxP record the type carries the record class, which makes it the field least able to afford a
         * silent reset. Neither half failed loudly; both were measured by asking the stored record.
         */
        expect(FORM_PAGE).toContain("taskTypeId: el('taskTypeId')?.value");
        expect(FORM_PAGE).toContain("__pendingTaskTypeId = draft.taskTypeId");
        // Applied AFTER the options land: a <select> silently drops a value it has no option for, which is how
        // the edit form came to show "no type" while holding one.
        expect(FORM_PAGE).toMatch(/select\.value = global\.__pendingTaskTypeId/);
    });

    it("rehydrates a task type's governing documents into its own edit form", () => {
        /*
         * MUTATION GUARD: remove the rehydration in LoadApiModelAsync and this goes red.
         *
         * The API answers with a LIST; the form edits a TEXTAREA; nothing derived one from the other. The edit
         * screen opened empty on a type carrying two documents and the save is a full replace — so pressing
         * Save without touching the field deleted them, with a success message. FOUND LIVE 2026-08-26.
         */
        const controller = fs.readFileSync(web("Controllers", "TaskTypesController.cs"), "utf8");
        expect(controller).toContain("model.GroupDocumentsText = string.Join(Environment.NewLine, model.GroupDocuments)");
    });
});
