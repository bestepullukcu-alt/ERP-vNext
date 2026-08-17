(() => {
    const escapeHtml = (s) => String(s ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/"/g, "&quot;");

    const captureUrl = (rowId) => `/DemandIdeas/Capture?id=${encodeURIComponent(rowId)}`;

    const formatDate = (iso) => {
        if (!iso) return "—";
        const d = new Date(iso);
        return Number.isNaN(d.getTime()) ? "—" : d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
    };
    const sortRows = (rows, key, dir) => {
        const mult = dir === "desc" ? -1 : 1;
        const po = { Critical: 4, High: 3, Medium: 2, Low: 1 };
        return [...rows].sort((a, b) => {
            let va; let vb;
            if (key === "id") { va = a.recordNumber || a.id; vb = b.recordNumber || b.id; }
            else if (key === "title") { va = a.title; vb = b.title; }
            else if (key === "priority") { va = po[a.priority] || 0; vb = po[b.priority] || 0; }
            else if (key === "status") { va = a.status; vb = b.status; }
            else if (key === "owner") { va = a.ownerName; vb = b.ownerName; }
            else if (key === "sponsor") { va = a.sponsorName; vb = b.sponsorName; }
            else if (key === "submitted") { va = a.submittedAt ? new Date(a.submittedAt).getTime() : 0; vb = b.submittedAt ? new Date(b.submittedAt).getTime() : 0; }
            else if (key === "due") { va = a.dueDate ? new Date(a.dueDate).getTime() : 0; vb = b.dueDate ? new Date(b.dueDate).getTime() : 0; }
            else return 0;
            if (va < vb) return -1 * mult; if (va > vb) return 1 * mult; return 0;
        });
    };
    const av = (i, n) => `<span class="avatar avatar-xs me-2" title="${escapeHtml(n)}"><span class="avatar-initial rounded-circle bg-label-primary text-primary fw-semibold">${escapeHtml(i || "?")}</span></span>`;
    const renderRow = (row) => {
        const sb = window.DemandIdeaStatusBadges;
        const ra = window.DemandIdeaRowActions;
        const idLabel = escapeHtml(row.recordNumber || row.id);
        const idCell = `<a href="${captureUrl(row.id)}" class="demand-id-link" title="Open in Demand &amp; Ideas Capture">${idLabel}</a>`;
        const sub = row.classification && row.classification !== "—"
            ? `<div class="text-muted demand-idea-sub">${escapeHtml(row.classification)}</div>`
            : "";
        return `<tr data-id="${escapeHtml(row.id)}">
            <td class="text-nowrap">${idCell}</td>
            <td><div class="demand-idea-title">${escapeHtml(row.title)}</div>${sub}</td>
            <td>${escapeHtml(row.requestType)}</td>
            <td><span class="badge rounded-pill ${sb.priorityClass(row.priority)}">${escapeHtml(row.priority)}</span></td>
            <td><span class="badge rounded-pill ${sb.statusClass(row.status)}">${escapeHtml(row.status)}</span></td>
            <td><div class="d-flex align-items-center">${av(row.ownerInitials, row.ownerName)}<span class="text-truncate" style="max-width:140px">${escapeHtml(row.ownerName)}</span></div></td>
            <td><span class="text-truncate d-inline-block" style="max-width:160px" title="${escapeHtml(row.sponsorName)}">${escapeHtml(row.sponsorName)}</span></td>
            <td>${escapeHtml(row.businessUnit)}</td>
            <td class="text-nowrap">${formatDate(row.submittedAt)}</td>
            <td class="text-nowrap">${formatDate(row.dueDate)}</td>
            <td class="text-end">${ra.renderRowActions(row)}</td></tr>`;
    };
    const paginate = (arr, page, pageSize) => { const s = (page - 1) * pageSize; return arr.slice(s, s + pageSize); };
    window.DemandIdeaTable = { formatDate, sortRows, renderRow, paginate };
})();
