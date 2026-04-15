(() => {
    const root = document.querySelector(".decomp-redesign-page");
    if (!root) return;
    const byId = (id) => document.getElementById(id);
    const escapeHtml = (v) => String(v ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

    const types = ["Initiative", "Workstream", "Work Package", "Task", "Deliverable"];
    const statuses = ["Draft", "In Progress", "Blocked", "Ready", "Approved"];
    const presets = {
        compact: ["code", "title", "status"],
        detailed: ["code", "type", "title", "owner", "dueDate", "status"],
        governance: ["code", "type", "title", "status", "validation", "dependencies", "owner", "dueDate"],
        financial: ["code", "title", "budget", "budgetMode", "owner", "status"]
    };

    const el = {
        search: byId("decomp-global-search"),
        statusBadge: byId("decomp-structure-status"),
        readinessSummary: byId("decomp-readiness-summary"),
        issueSummary: byId("decomp-issue-summary"),
        viewTabs: document.querySelectorAll("#decomp-view-tabs .nav-link"),
        inspectorTabs: document.querySelectorAll("#decomp-inspector-tabs .nav-link"),
        inspectorPanels: document.querySelectorAll(".decomp-inspector-panel"),
        outlinePreset: byId("decomp-outline-preset"),
        outlineColumns: byId("decomp-outline-columns"),
        paneTree: byId("decomp-view-tree"),
        paneOutline: byId("decomp-view-outline"),
        paneMap: byId("decomp-view-map"),
        workState: byId("decomp-work-state"),
        types: byId("decomp-filter-types"),
        statuses: byId("decomp-filter-statuses"),
        owners: byId("decomp-filter-owners"),
        flags: byId("decomp-filter-flags"),
        depth: byId("decomp-filter-depth"),
        lineage: byId("decomp-lineage-toggle"),
        reset: byId("decomp-filter-reset"),
        chips: byId("decomp-active-filter-chips"),
        nodeLibrary: byId("decomp-node-library"),
        selectedCode: byId("decomp-selected-code"),
        nodeCode: byId("decomp-node-code"),
        nodePath: byId("decomp-node-path"),
        nodeType: byId("decomp-node-type"),
        nodeTitle: byId("decomp-node-title"),
        nodeStatus: byId("decomp-node-status"),
        nodeOwner: byId("decomp-node-owner"),
        nodeDue: byId("decomp-node-due"),
        nodeParent: byId("decomp-node-parent"),
        nodeDescription: byId("decomp-node-description"),
        nodeBudget: byId("decomp-node-budget"),
        nodeBudgetMode: byId("decomp-node-budget-mode"),
        validate: byId("decomp-validate-btn"),
        readiness: byId("decomp-readiness-btn"),
        approve: byId("decomp-approval-btn"),
        addChild: byId("decomp-add-child"),
        addSibling: byId("decomp-add-sibling"),
        moveUp: byId("decomp-move-up"),
        moveDown: byId("decomp-move-down"),
        deleteNode: byId("decomp-delete-node"),
        moveTarget: byId("decomp-reparent-target"),
        moveApply: byId("decomp-reparent-apply"),
        depTarget: byId("decomp-dependency-target"),
        depType: byId("decomp-dependency-type"),
        depAdd: byId("decomp-add-dependency"),
        depList: byId("decomp-dependency-list"),
        govState: byId("decomp-governance-state"),
        issueList: byId("decomp-issue-list"),
        history: byId("decomp-history")
    };

    const state = {
        structureId: String(root.dataset.structureId || "").trim(),
        structureStatus: "Draft",
        version: 0,
        nodes: [],
        dependencies: [],
        issues: [],
        history: [],
        activeView: "tree",
        selectedId: null,
        activeColumns: [...presets.compact],
        expanded: new Set(),
        typeFilter: new Set(types),
        statusFilter: new Set(statuses),
        ownerFilter: new Set(),
        depthFilter: new Set(),
        flagFilter: new Set(),
        loading: false,
        saving: false,
        error: ""
    };

    const apiBase = (window.APP_CONFIG?.API_BASE_URL || "").replace(/\/$/, "");
    const api = {
        structures: () => (apiBase ? `${apiBase}/api/v1/decomposition-structures` : "/api/v1/decomposition-structures"),
        structure: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}` : `/api/v1/decomposition-structures/${id}`),
        validate: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}/validate` : `/api/v1/decomposition-structures/${id}/validate`),
        approve: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}/approve` : `/api/v1/decomposition-structures/${id}/approve`),
        issues: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}/issues` : `/api/v1/decomposition-structures/${id}/issues`),
        history: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}/history` : `/api/v1/decomposition-structures/${id}/history`),
        depAdd: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-structures/${id}/dependencies` : `/api/v1/decomposition-structures/${id}/dependencies`),
        depDelete: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-dependencies/${id}` : `/api/v1/decomposition-dependencies/${id}`),
        node: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-nodes/${id}` : `/api/v1/decomposition-nodes/${id}`),
        child: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-nodes/${id}/add-child` : `/api/v1/decomposition-nodes/${id}/add-child`),
        sibling: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-nodes/${id}/add-sibling` : `/api/v1/decomposition-nodes/${id}/add-sibling`),
        move: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-nodes/${id}/move` : `/api/v1/decomposition-nodes/${id}/move`),
        reorder: (id) => (apiBase ? `${apiBase}/api/v1/decomposition-nodes/${id}/reorder` : `/api/v1/decomposition-nodes/${id}/reorder`)
    };

    const notify = (m, k = "success") => {
        if (window.Notiflix?.Notify) { if (k === "error") Notiflix.Notify.failure(m); else if (k === "warning") Notiflix.Notify.warning(m); else Notiflix.Notify.success(m); return; }
        // eslint-disable-next-line no-alert
        alert(m);
    };
    const fetchJson = async (url, options = {}) => {
        const res = await fetch(url, { ...options, headers: { "Content-Type": "application/json", ...(options.headers || {}) } });
        const txt = await res.text(); let data = null; try { data = txt ? JSON.parse(txt) : null; } catch { data = txt; }
        return { res, data };
    };
    const getNode = (id) => state.nodes.find((n) => n.id === id) || null;
    const children = (id) => state.nodes.filter((n) => n.parentId === id).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
    const parent = (id) => { const n = getNode(id); return n?.parentId ? getNode(n.parentId) : null; };
    const depsFor = (id) => state.dependencies.filter((d) => d.fromNodeId === id || d.toNodeId === id);
    const normalize = (n) => ({ id: n.id, parentId: n.parentId || null, code: n.code || "", title: n.title || "", type: n.type || "Task", description: n.description || "", owner: n.responsibleName || "", dueDate: n.dueDate ? String(n.dueDate).slice(0, 10) : "", status: n.status || "Draft", budget: n.budget ?? null, budgetMode: n.budgetMode || "Estimate", level: n.level || 0, validationState: n.validationState || "Unknown", sortOrder: n.sortOrder || 0, metadata: n.metadata || {} });
    const pathOf = (id) => { const p = []; let n = getNode(id); while (n) { p.unshift(n.code); n = n.parentId ? getNode(n.parentId) : null; } return p.join(" / "); };
    const matchesSearch = (n) => !String(el.search?.value || "").trim() || [n.code, n.title, n.type, n.status, n.owner].join(" ").toLowerCase().includes(String(el.search.value).trim().toLowerCase());
    const flagsOf = (n) => ({ validationState: String(n.validationState).toLowerCase() !== "valid", overdue: n.dueDate && new Date(n.dueDate) < new Date(), unassigned: !n.owner, hasDependencies: depsFor(n.id).length > 0, missingRequired: !n.title || !n.owner || !n.dueDate });
    const ancestors = (id) => { const a = []; let p = parent(id); while (p) { a.push(p.id); p = p.parentId ? getNode(p.parentId) : null; } return a; };
    const descendants = (id) => { const s = new Set(); const walk = (pid) => children(pid).forEach((c) => { s.add(c.id); walk(c.id); }); walk(id); return s; };

    const visibleIds = () => {
        let rows = state.nodes.filter((n) => state.typeFilter.has(n.type) && state.statusFilter.has(n.status) && matchesSearch(n));
        if (state.ownerFilter.size) rows = rows.filter((n) => state.ownerFilter.has(n.owner || "Unassigned"));
        if (state.depthFilter.size) rows = rows.filter((n) => state.depthFilter.has(String(n.level || 0)));
        if (state.flagFilter.size) rows = rows.filter((n) => { const f = flagsOf(n); return [...state.flagFilter].every((x) => f[x]); });
        const set = new Set(rows.map((n) => n.id));
        if (!el.lineage?.checked) return set;
        rows.forEach((n) => { ancestors(n.id).forEach((a) => set.add(a)); descendants(n.id).forEach((d) => set.add(d)); });
        return set;
    };

    const treeRows = (pid = null, depth = 0, out = [], vis = new Set()) => {
        children(pid).forEach((n) => {
            if (!vis.has(n.id)) return;
            out.push({ ...n, depth });
            if (state.expanded.has(n.id)) treeRows(n.id, depth + 1, out, vis);
        });
        return out;
    };

    const setBusy = (loading, saving = false, error = "") => {
        state.loading = loading; state.saving = saving; state.error = error;
        if (error) el.workState.textContent = `Error: ${error}`;
        else if (loading) el.workState.textContent = "Loading structure...";
        else if (saving) el.workState.textContent = "Saving...";
        else el.workState.textContent = "";
    };

    const hydrate = async (dto) => {
        state.structureStatus = dto.status || "Draft";
        state.version = dto.version || 0;
        state.nodes = Array.isArray(dto.nodes) ? dto.nodes.map(normalize) : [];
        state.dependencies = Array.isArray(dto.dependencies) ? dto.dependencies : [];
        if (!state.selectedId || !getNode(state.selectedId)) state.selectedId = state.nodes[0]?.id || null;
        if (!state.expanded.size) state.nodes.forEach((n) => { if ((n.level || 0) < 2) state.expanded.add(n.id); });
        const [i, h] = await Promise.all([fetchJson(api.issues(state.structureId)), fetchJson(api.history(state.structureId))]);
        state.issues = i.res.ok && Array.isArray(i.data) ? i.data : [];
        state.history = h.res.ok && Array.isArray(h.data) ? h.data : [];
        renderAll();
    };

    const withStructureResult = async (req) => {
        setBusy(false, true);
        const { res, data } = await req;
        if (!res.ok || !data) { setBusy(false, false, data?.message || "Request failed."); notify(data?.message || "Request failed.", "error"); return false; }
        await hydrate(data); setBusy(false, false); return true;
    };

    const patchNode = async (nodeId, payload, toast = true) => {
        const ok = await withStructureResult(fetchJson(api.node(nodeId), { method: "PATCH", body: JSON.stringify({ expectedVersion: state.version, ...payload }) }));
        if (ok && toast) notify("Node updated.");
    };

    const addChild = async (forcedType = null, nodeId = null) => {
        const id = nodeId || state.selectedId; if (!id) return;
        await withStructureResult(fetchJson(api.child(id), { method: "POST", body: JSON.stringify({ expectedVersion: state.version, type: forcedType || getNode(id)?.type || "Task", title: `New ${forcedType || "Node"}` }) }));
    };
    const addSibling = async (forcedType = null, nodeId = null) => {
        const id = nodeId || state.selectedId; if (!id) return;
        await withStructureResult(fetchJson(api.sibling(id), { method: "POST", body: JSON.stringify({ expectedVersion: state.version, type: forcedType || getNode(id)?.type || "Task", title: `New ${forcedType || "Node"}` }) }));
    };
    const reorder = async (nodeId, delta) => {
        const n = getNode(nodeId); if (!n) return;
        const siblings = children(n.parentId); const idx = siblings.findIndex((x) => x.id === n.id); const next = Math.max(0, Math.min(siblings.length - 1, idx + delta));
        if (next === idx) return;
        await withStructureResult(fetchJson(api.reorder(nodeId), { method: "POST", body: JSON.stringify({ expectedVersion: state.version, targetIndex: next }) }));
    };
    const applyMove = async () => {
        const id = state.selectedId; if (!id) return;
        const targetParentId = el.moveTarget.value || null;
        await withStructureResult(fetchJson(api.move(id), { method: "POST", body: JSON.stringify({ expectedVersion: state.version, targetParentId, targetIndex: -1, placementMode: "child" }) }));
    };
    const addDependency = async () => {
        if (!state.selectedId || !el.depTarget.value) return;
        await withStructureResult(fetchJson(api.depAdd(state.structureId), { method: "POST", body: JSON.stringify({ expectedVersion: state.version, fromNodeId: state.selectedId, toNodeId: el.depTarget.value, dependencyType: el.depType.value }) }));
    };
    const deleteDependency = async (id) => withStructureResult(fetchJson(`${api.depDelete(id)}?expectedVersion=${state.version}`, { method: "DELETE" }));

    let draw = null; let drawReady = false;
    const drawAdapter = {
        init() {
            if (drawReady || !window.Drawflow) return;
            el.paneMap.innerHTML = "<div id='decomp-drawflow-host' class='decomp-drawflow-host'></div>";
            // eslint-disable-next-line no-undef
            draw = new Drawflow(document.getElementById("decomp-drawflow-host")); draw.start();
            draw.on("nodeSelected", (id) => selectNode(String(id)));
            draw.on("nodeMoved", (id) => {
                const n = getNode(String(id)); if (!n) return;
                const data = draw.getNodeFromId(id);
                patchNode(n.id, { metadata: { ...(n.metadata || {}), mapX: String(data.pos_x), mapY: String(data.pos_y) } }, false);
            });
            drawReady = true;
        },
        rebuild() {
            if (!drawReady || !draw) return;
            const vis = visibleIds(); draw.clear();
            const all = []; const walk = (pid = null, depth = 0) => children(pid).forEach((n) => { if (vis.has(n.id)) all.push({ ...n, depth }); walk(n.id, depth + 1); }); walk();
            all.forEach((n, i) => {
                const x = Number(n.metadata?.mapX || 40 + (i % 4) * 210); const y = Number(n.metadata?.mapY || 40 + Math.floor(i / 4) * 110);
                draw.addNode(n.id, 1, 1, x, y, "decomp", { id: n.id }, `<div class="decomp-df-card"><div class="decomp-df-code">${n.code}</div><div class="decomp-df-title">${escapeHtml(n.title)}</div><div class="decomp-df-meta">${n.type} · ${n.status}</div></div>`);
            });
            all.forEach((n) => { if (n.parentId && vis.has(n.parentId)) { try { draw.addConnection(n.parentId, n.id, "output_1", "input_1"); } catch { } } });
        }
    };

    const renderFilters = () => {
        const owners = [...new Set(state.nodes.map((n) => n.owner || "Unassigned"))].sort();
        const depths = [...new Set(state.nodes.map((n) => String(n.level || 0)))].sort((a, b) => Number(a) - Number(b));
        if (!state.ownerFilter.size) owners.forEach((o) => state.ownerFilter.add(o));
        if (!state.depthFilter.size) depths.forEach((d) => state.depthFilter.add(d));
        el.types.innerHTML = types.map((t) => `<label class="form-check d-flex justify-content-between"><span><input class="form-check-input me-2 f-type" type="checkbox" value="${t}" ${state.typeFilter.has(t) ? "checked" : ""}>${t}</span><span class="small text-muted">${state.nodes.filter((n) => n.type === t).length}</span></label>`).join("");
        el.statuses.innerHTML = statuses.map((s) => `<label class="form-check d-flex justify-content-between"><span><input class="form-check-input me-2 f-status" type="checkbox" value="${s}" ${state.statusFilter.has(s) ? "checked" : ""}>${s}</span><span class="small text-muted">${state.nodes.filter((n) => n.status === s).length}</span></label>`).join("");
        el.owners.innerHTML = owners.map((o) => `<label class="form-check d-flex justify-content-between"><span><input class="form-check-input me-2 f-owner" type="checkbox" value="${o}" ${state.ownerFilter.has(o) ? "checked" : ""}>${o}</span><span class="small text-muted">${state.nodes.filter((n) => (n.owner || "Unassigned") === o).length}</span></label>`).join("");
        el.depth.innerHTML = depths.map((d) => `<label class="form-check d-flex justify-content-between"><span><input class="form-check-input me-2 f-depth" type="checkbox" value="${d}" ${state.depthFilter.has(d) ? "checked" : ""}>Level ${d}</span><span class="small text-muted">${state.nodes.filter((n) => String(n.level || 0) === d).length}</span></label>`).join("");
        el.flags.innerHTML = [["validationState", "Validation state"], ["overdue", "Overdue"], ["unassigned", "Unassigned"], ["hasDependencies", "Has dependencies"], ["missingRequired", "Missing required fields"]].map(([k, l]) => `<label class="form-check"><input class="form-check-input f-flag" type="checkbox" value="${k}" ${state.flagFilter.has(k) ? "checked" : ""}>${l}</label>`).join("");
        const chips = []; if (el.search?.value) chips.push(`Search: ${el.search.value}`); state.flagFilter.forEach((f) => chips.push(f));
        el.chips.innerHTML = chips.map((c) => `<span class="badge bg-label-secondary text-secondary me-1 mb-1">${c}</span>`).join("") || "<span class='small text-muted'>No active filters.</span>";
        el.nodeLibrary.innerHTML = types.map((t) => `<button class="btn btn-sm btn-outline-secondary text-start lib mb-1" data-type="${t}">+ ${t} as child</button>`).join("");
        el.types.querySelectorAll(".f-type").forEach((i) => i.addEventListener("change", () => { i.checked ? state.typeFilter.add(i.value) : state.typeFilter.delete(i.value); renderAll(); }));
        el.statuses.querySelectorAll(".f-status").forEach((i) => i.addEventListener("change", () => { i.checked ? state.statusFilter.add(i.value) : state.statusFilter.delete(i.value); renderAll(); }));
        el.owners.querySelectorAll(".f-owner").forEach((i) => i.addEventListener("change", () => { i.checked ? state.ownerFilter.add(i.value) : state.ownerFilter.delete(i.value); renderAll(); }));
        el.depth.querySelectorAll(".f-depth").forEach((i) => i.addEventListener("change", () => { i.checked ? state.depthFilter.add(i.value) : state.depthFilter.delete(i.value); renderAll(); }));
        el.flags.querySelectorAll(".f-flag").forEach((i) => i.addEventListener("change", () => { i.checked ? state.flagFilter.add(i.value) : state.flagFilter.delete(i.value); renderAll(); }));
        el.nodeLibrary.querySelectorAll(".lib").forEach((i) => i.addEventListener("click", () => addChild(i.dataset.type)));
    };

    const renderTree = () => {
        const vis = visibleIds(); const rows = treeRows(null, 0, [], vis);
        if (!rows.length) { el.paneTree.innerHTML = "<div class='small text-muted'>No matching rows.</div>"; return; }
        el.paneTree.innerHTML = rows.map((n) => {
            const kids = children(n.id).filter((x) => vis.has(x.id)).length;
            const exp = kids ? `<button class="btn btn-sm btn-link p-0 tree-exp" data-id="${n.id}">${state.expanded.has(n.id) ? "▾" : "▸"}</button>` : "<span class='tree-exp-sp'></span>";
            const valid = String(n.validationState).toLowerCase() === "valid";
            return `<div class="decomp-tree-row ${n.id === state.selectedId ? "active" : ""}" data-id="${n.id}">
                <div class="decomp-tree-grid" style="padding-left:${n.depth * 16}px;">
                    <div class="d-flex align-items-center">${exp}<span class="fw-semibold me-2">${n.code}</span><input class="form-control form-control-sm qt-title" data-id="${n.id}" value="${escapeHtml(n.title)}" /></div>
                    <div><span class="badge bg-label-secondary text-secondary">${n.type}</span></div>
                    <div><input class="form-control form-control-sm qt-owner" data-id="${n.id}" value="${escapeHtml(n.owner || "")}" /></div>
                    <div><input type="date" class="form-control form-control-sm qt-due" data-id="${n.id}" value="${n.dueDate || ""}" /></div>
                    <div><select class="form-select form-select-sm qt-status" data-id="${n.id}">${statuses.map((s) => `<option ${s === n.status ? "selected" : ""}>${s}</option>`).join("")}</select></div>
                    <div><span class="badge ${valid ? "bg-label-success text-success" : "bg-label-danger text-danger"}">${valid ? "OK" : "Issue"}</span></div>
                    <div class="text-end"><button class="btn btn-sm btn-outline-primary t-child" data-id="${n.id}">+Child</button> <button class="btn btn-sm btn-outline-secondary t-sibling" data-id="${n.id}">+Sibling</button> <button class="btn btn-sm btn-outline-dark t-up" data-id="${n.id}">↑</button> <button class="btn btn-sm btn-outline-dark t-down" data-id="${n.id}">↓</button></div>
                </div>
            </div>`;
        }).join("");
        el.paneTree.querySelectorAll(".decomp-tree-row").forEach((r) => r.addEventListener("click", (e) => { if (e.target.closest("input,select,button")) return; selectNode(r.dataset.id); }));
        el.paneTree.querySelectorAll(".tree-exp").forEach((b) => b.addEventListener("click", (e) => { e.stopPropagation(); state.expanded.has(b.dataset.id) ? state.expanded.delete(b.dataset.id) : state.expanded.add(b.dataset.id); renderTree(); }));
        const bind = (s, f) => el.paneTree.querySelectorAll(s).forEach((i) => i.addEventListener("change", () => { const n = getNode(i.dataset.id); if (!n) return; patchNode(n.id, f(i, n), false); }));
        bind(".qt-title", (i, n) => ({ title: i.value, responsibleName: n.owner, dueDate: n.dueDate || null, status: n.status }));
        bind(".qt-owner", (i, n) => ({ title: n.title, responsibleName: i.value, dueDate: n.dueDate || null, status: n.status }));
        bind(".qt-due", (i, n) => ({ title: n.title, responsibleName: n.owner, dueDate: i.value || null, status: n.status }));
        bind(".qt-status", (i, n) => ({ title: n.title, responsibleName: n.owner, dueDate: n.dueDate || null, status: i.value }));
        el.paneTree.querySelectorAll(".t-child").forEach((b) => b.addEventListener("click", (e) => { e.stopPropagation(); addChild(null, b.dataset.id); }));
        el.paneTree.querySelectorAll(".t-sibling").forEach((b) => b.addEventListener("click", (e) => { e.stopPropagation(); addSibling(null, b.dataset.id); }));
        el.paneTree.querySelectorAll(".t-up").forEach((b) => b.addEventListener("click", (e) => { e.stopPropagation(); reorder(b.dataset.id, -1); }));
        el.paneTree.querySelectorAll(".t-down").forEach((b) => b.addEventListener("click", (e) => { e.stopPropagation(); reorder(b.dataset.id, 1); }));
    };

    const renderOutline = () => {
        const vis = visibleIds(); const rows = [];
        const walk = (pid = null, depth = 0) => children(pid).forEach((n) => { if (vis.has(n.id)) rows.push({ ...n, depth }); walk(n.id, depth + 1); }); walk();
        if (!rows.length) { el.paneOutline.innerHTML = "<div class='small text-muted'>No matching rows.</div>"; return; }
        const H = { code: "Code", type: "Type", title: "Title", status: "Status", owner: "Owner", dueDate: "Due", budget: "Budget", budgetMode: "Budget Mode", validation: "Validation", dependencies: "Deps" };
        const cell = (n, c) => {
            if (c === "title") return `<input class="form-control form-control-sm o-title" data-id="${n.id}" value="${escapeHtml(n.title)}" />`;
            if (c === "status") return `<select class="form-select form-select-sm o-status" data-id="${n.id}">${statuses.map((s) => `<option ${s === n.status ? "selected" : ""}>${s}</option>`).join("")}</select>`;
            if (c === "owner") return `<input class="form-control form-control-sm o-owner" data-id="${n.id}" value="${escapeHtml(n.owner || "")}" />`;
            if (c === "dueDate") return `<input type="date" class="form-control form-control-sm o-due" data-id="${n.id}" value="${n.dueDate || ""}" />`;
            if (c === "validation") return n.validationState || "Unknown";
            if (c === "dependencies") return String(depsFor(n.id).length);
            return escapeHtml(String(n[c] ?? ""));
        };
        el.paneOutline.innerHTML = `<div class="table-responsive"><table class="table table-sm decomp-outline-table"><thead><tr>${state.activeColumns.map((c) => `<th>${H[c] || c}</th>`).join("")}</tr></thead><tbody>${rows.map((n) => `<tr class="decomp-outline-row ${n.id === state.selectedId ? "active" : ""}" data-id="${n.id}">${state.activeColumns.map((c) => `<td>${cell(n, c)}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
        el.paneOutline.querySelectorAll(".decomp-outline-row").forEach((r) => r.addEventListener("click", (e) => { if (e.target.closest("input,select")) return; selectNode(r.dataset.id); }));
        const bind = (s, f) => el.paneOutline.querySelectorAll(s).forEach((i) => i.addEventListener("change", () => { const n = getNode(i.dataset.id); if (!n) return; patchNode(n.id, f(i, n), false); }));
        bind(".o-title", (i, n) => ({ title: i.value, responsibleName: n.owner, dueDate: n.dueDate || null, status: n.status }));
        bind(".o-owner", (i, n) => ({ title: n.title, responsibleName: i.value, dueDate: n.dueDate || null, status: n.status }));
        bind(".o-due", (i, n) => ({ title: n.title, responsibleName: n.owner, dueDate: i.value || null, status: n.status }));
        bind(".o-status", (i, n) => ({ title: n.title, responsibleName: n.owner, dueDate: n.dueDate || null, status: i.value }));
    };

    const renderMap = () => { drawAdapter.init(); if (!drawReady) { el.paneMap.innerHTML = "<div class='small text-muted'>Drawflow unavailable.</div>"; return; } drawAdapter.rebuild(); };
    const renderOutlineConfig = () => {
        const cols = ["code", "type", "title", "status", "owner", "dueDate", "budget", "budgetMode", "validation", "dependencies"];
        el.outlineColumns.innerHTML = cols.map((c) => `<label class="form-check"><input class="form-check-input oc" type="checkbox" value="${c}" ${state.activeColumns.includes(c) ? "checked" : ""}>${c}</label>`).join("");
        el.outlineColumns.querySelectorAll(".oc").forEach((i) => i.addEventListener("change", () => { if (i.checked && !state.activeColumns.includes(i.value)) state.activeColumns.push(i.value); if (!i.checked) state.activeColumns = state.activeColumns.filter((x) => x !== i.value); if (!state.activeColumns.length) state.activeColumns = ["code", "title"]; renderOutline(); }));
    };

    const renderInspector = () => {
        const n = getNode(state.selectedId);
        if (!n) return;
        el.selectedCode.textContent = n.code || "None";
        el.nodeCode.value = n.code; el.nodePath.value = pathOf(n.id); el.nodeType.value = n.type; el.nodeTitle.value = n.title; el.nodeStatus.value = n.status; el.nodeOwner.value = n.owner || ""; el.nodeDue.value = n.dueDate || ""; el.nodeDescription.value = n.description || ""; el.nodeBudget.value = n.budget ?? ""; el.nodeBudgetMode.value = n.budgetMode || "";
        const p = parent(n.id); el.nodeParent.value = p ? `${p.code} - ${p.title}` : "ROOT";
        const blocking = state.issues.filter((x) => x.blocking).length; const warnings = state.issues.length - blocking; const readiness = Math.max(0, 100 - (blocking * 10 + warnings * 3));
        el.statusBadge.textContent = state.structureStatus; el.readinessSummary.textContent = `Readiness ${readiness}%`; el.issueSummary.textContent = `${state.issues.length} issues`;
        el.govState.innerHTML = `Validation: <strong>${blocking ? `${blocking} blocking / ${warnings} warnings` : "Pass"}</strong><br/>Readiness: <strong>${readiness}%</strong>`;
        const nodeIssues = state.issues.filter((i) => !i.nodeId || i.nodeId === n.id);
        el.issueList.innerHTML = nodeIssues.map((i) => `<button type="button" class="btn btn-link p-0 d-block text-start issue-go" data-node="${i.nodeId || ""}">${i.code}: ${escapeHtml(i.message)}</button>`).join("") || "<span class='small text-muted'>No issues for selected scope.</span>";
        el.issueList.querySelectorAll(".issue-go").forEach((b) => b.addEventListener("click", () => { if (b.dataset.node) selectNode(b.dataset.node); }));
        el.history.innerHTML = state.history.slice(0, 10).map((h) => `<div class="small mb-1"><strong>${h.eventType}</strong> · ${h.actor} · ${new Date(h.createdAt).toLocaleString()}</div>`).join("") || "<span class='small text-muted'>No history.</span>";
        const moveOptions = ["<option value=''>ROOT</option>"].concat(state.nodes.filter((x) => x.id !== n.id && !descendants(n.id).has(x.id)).map((x) => `<option value="${x.id}" ${n.parentId === x.id ? "selected" : ""}>${x.code} - ${escapeHtml(x.title)}</option>`));
        el.moveTarget.innerHTML = moveOptions.join("");
        const depCandidates = state.nodes.filter((x) => x.id !== n.id).map((x) => `<option value="${x.id}">${x.code} - ${escapeHtml(x.title)}</option>`);
        el.depTarget.innerHTML = `<option value="">Select node...</option>${depCandidates.join("")}`;
        const ownDeps = state.dependencies.filter((d) => d.fromNodeId === n.id);
        el.depList.innerHTML = ownDeps.map((d) => { const t = getNode(d.toNodeId); return `<div class="d-flex justify-content-between align-items-center mb-1"><span>${d.dependencyType} → ${t ? `${t.code} ${escapeHtml(t.title)}` : d.toNodeId}</span><button type="button" class="btn btn-sm btn-outline-danger dep-del" data-id="${d.id}">Remove</button></div>`; }).join("") || "<span class='small text-muted'>No outgoing dependencies.</span>";
        el.depList.querySelectorAll(".dep-del").forEach((b) => b.addEventListener("click", () => deleteDependency(b.dataset.id)));
    };

    const renderViews = () => { el.paneTree.classList.toggle("d-none", state.activeView !== "tree"); el.paneOutline.classList.toggle("d-none", state.activeView !== "outline"); el.paneMap.classList.toggle("d-none", state.activeView !== "map"); renderTree(); renderOutline(); renderMap(); };
    const renderAll = () => { if (!getNode(state.selectedId)) state.selectedId = state.nodes[0]?.id || null; renderFilters(); renderViews(); renderInspector(); };
    const selectNode = (id) => { state.selectedId = id; renderAll(); };

    const ensureStructure = async () => {
        setBusy(true);
        if (!state.structureId) {
            const { res, data } = await fetchJson(api.structures(), { method: "POST", body: JSON.stringify({ parentEntityId: "Demand-Idea", name: "Decomposition Structure", structureType: "PPM_WBS" }) });
            if (!res.ok || !data?.id) { setBusy(false, false, "Could not create structure."); return; }
            state.structureId = data.id; const url = new URL(window.location.href); url.searchParams.set("structureId", data.id); history.replaceState({}, "", url.toString());
        }
        const { res, data } = await fetchJson(api.structure(state.structureId));
        if (!res.ok || !data) { setBusy(false, false, "Could not load structure."); return; }
        await hydrate(data);
        if (!state.nodes.length) {
            const rootRes = await fetchJson(`${api.structure(state.structureId)}/nodes`, {
                method: "POST",
                body: JSON.stringify({
                    expectedVersion: state.version,
                    parentId: null,
                    type: "Initiative",
                    title: "New Initiative"
                })
            });
            if (rootRes.res.ok && rootRes.data) await hydrate(rootRes.data);
        }
        setBusy(false, false);
    };

    // Events
    el.search?.addEventListener("input", renderAll);
    el.lineage?.addEventListener("change", renderAll);
    el.reset?.addEventListener("click", () => { state.typeFilter = new Set(types); state.statusFilter = new Set(statuses); state.ownerFilter = new Set(); state.depthFilter = new Set(); state.flagFilter = new Set(); if (el.search) el.search.value = ""; if (el.lineage) el.lineage.checked = true; renderAll(); });
    el.outlinePreset?.addEventListener("change", () => { state.activeColumns = [...(presets[el.outlinePreset.value] || presets.compact)]; renderOutlineConfig(); renderOutline(); });
    el.viewTabs.forEach((t) => t.addEventListener("click", () => { el.viewTabs.forEach((x) => x.classList.remove("active")); t.classList.add("active"); state.activeView = t.dataset.view; renderViews(); }));
    el.inspectorTabs.forEach((t) => t.addEventListener("click", () => { el.inspectorTabs.forEach((x) => x.classList.remove("active")); t.classList.add("active"); const tab = t.dataset.tab; el.inspectorPanels.forEach((p) => p.classList.toggle("d-none", p.dataset.panel !== tab)); }));
    [el.nodeType, el.nodeTitle, el.nodeStatus, el.nodeOwner, el.nodeDue, el.nodeDescription, el.nodeBudget, el.nodeBudgetMode].forEach((i) => i?.addEventListener("change", () => { const n = getNode(state.selectedId); if (!n) return; patchNode(n.id, { type: el.nodeType.value, title: el.nodeTitle.value, status: el.nodeStatus.value, responsibleName: el.nodeOwner.value, dueDate: el.nodeDue.value || null, description: el.nodeDescription.value, budget: el.nodeBudget.value ? Number(el.nodeBudget.value) : null, budgetMode: el.nodeBudgetMode.value }, false); }));
    el.addChild?.addEventListener("click", () => addChild());
    el.addSibling?.addEventListener("click", () => addSibling());
    el.moveUp?.addEventListener("click", () => reorder(state.selectedId, -1));
    el.moveDown?.addEventListener("click", () => reorder(state.selectedId, 1));
    el.moveApply?.addEventListener("click", applyMove);
    el.deleteNode?.addEventListener("click", async () => { if (!state.selectedId) return; if (!window.confirm("Delete selected node and descendants?")) return; await withStructureResult(fetchJson(`${api.node(state.selectedId)}?expectedVersion=${state.version}`, { method: "DELETE" })); });
    el.depAdd?.addEventListener("click", addDependency);
    el.validate?.addEventListener("click", async () => { await withStructureResult(fetchJson(api.validate(state.structureId), { method: "POST", body: "{}" })); notify("Validation completed."); });
    el.readiness?.addEventListener("click", () => { const b = state.issues.filter((x) => x.blocking).length; const w = state.issues.length - b; const r = Math.max(0, 100 - (b * 10 + w * 3)); notify(`Readiness: ${r}%`, r >= 85 ? "success" : "warning"); });
    el.approve?.addEventListener("click", async () => {
        const { res, data } = await fetchJson(api.approve(state.structureId), { method: "POST", body: JSON.stringify({ expectedVersion: state.version }) });
        if (!res.ok || !data) { notify(data?.message || "Approval failed.", "error"); if (Array.isArray(data?.reasons) && data.reasons.length) notify(data.reasons.slice(0, 3).join(" | "), "warning"); return; }
        await hydrate(data); notify("Structure approved.");
    });

    types.forEach((t) => el.nodeType.insertAdjacentHTML("beforeend", `<option>${t}</option>`));
    statuses.forEach((s) => el.nodeStatus.insertAdjacentHTML("beforeend", `<option>${s}</option>`));
    renderOutlineConfig();
    ensureStructure();
})();
