(() => {
    const baseUrl = "";
    const moreMenuItems = () => [
        { key: "duplicate", label: "Duplicate", icon: "bx-copy" },
        { key: "assignReviewer", label: "Assign Reviewer", icon: "bx-user-plus" },
        { key: "changeStatus", label: "Change Status", icon: "bx-transfer" },
        { key: "transferPpm", label: "Transfer to PPM", icon: "bx-export" },
        { key: "archive", label: "Archive", icon: "bx-archive" }
    ];
    const renderRowActions = (row) => {
        const transferBtn = row.canTransfer === true
            ? `<button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill demand-row-transfer" data-id="${row.id}" title="Transfer"><i class="bx bx-send"></i></button>`
            : "";
        const mid = `more-${row.id.replace(/[^a-z0-9]/gi, "")}`;
        return `<div class="d-flex align-items-center justify-content-end gap-1 flex-nowrap">
            <a href="${baseUrl}/DemandIdeas/Capture?id=${encodeURIComponent(row.id)}" class="btn btn-sm btn-icon btn-text-secondary rounded-pill" title="View"><i class="bx bx-show"></i></a>
            <a href="${baseUrl}/DemandIdeas/Capture?id=${encodeURIComponent(row.id)}" class="btn btn-sm btn-icon btn-text-secondary rounded-pill" title="Edit"><i class="bx bx-edit-alt"></i></a>
            <button type="button" class="btn btn-sm btn-icon btn-text-danger rounded-pill demand-row-delete" data-id="${row.id}" title="Delete"><i class="bx bx-trash"></i></button>
            ${transferBtn}
            <div class="dropdown">
                <button class="btn btn-sm btn-icon btn-text-secondary rounded-pill" type="button" data-bs-toggle="dropdown" id="${mid}"><i class="bx bx-dots-vertical-rounded"></i></button>
                <ul class="dropdown-menu dropdown-menu-end shadow-sm border rounded-3">
                    ${moreMenuItems().map((i) => `<li><a class="dropdown-item demand-row-more" href="javascript:void(0)" data-action="${i.key}" data-id="${row.id}"><i class="bx ${i.icon} me-2"></i>${i.label}</a></li>`).join("")}
                </ul></div></div>`;
    };
    const wireRowActions = (rootEl) => {
        rootEl.querySelectorAll(".demand-row-delete").forEach((btn) => {
            btn.addEventListener("click", () => {
                const id = btn.getAttribute("data-id");
                const go = () => document.dispatchEvent(new CustomEvent("demand:deleted", { detail: { id } }));
                if (window.Swal) Swal.fire({ title: "Delete demand?", text: id, icon: "warning", showCancelButton: true }).then((r) => { if (r.isConfirmed) go(); });
                else if (confirm(`Delete ${id}?`)) go();
            });
        });
        rootEl.querySelectorAll(".demand-row-transfer").forEach((btn) => {
            btn.addEventListener("click", () => document.dispatchEvent(new CustomEvent("demand:transfer", { detail: { id: btn.getAttribute("data-id") } })));
        });
        rootEl.querySelectorAll(".demand-row-more").forEach((link) => {
            link.addEventListener("click", () => document.dispatchEvent(new CustomEvent("demand:moreAction", { detail: { id: link.getAttribute("data-id"), action: link.getAttribute("data-action") } })));
        });
    };
    window.DemandIdeaRowActions = { renderRowActions, wireRowActions, moreMenuItems };
})();
