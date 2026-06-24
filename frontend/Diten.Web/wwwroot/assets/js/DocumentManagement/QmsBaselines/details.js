/**
 * MOD-0028-FU03 - QMS Baseline detail + nested definition tree + publish.
 * Same-origin proxy only. The nested tree is built from the backend's parentCanonicalId/displayOrder;
 * it is never flattened and folder names are shown as atomic values (names may contain '/').
 */
'use strict';

(function () {
    let L = window.L10n || {};
    const root = document.getElementById('qmsBaselineRoot');
    if (!root) return;

    const baselineId = root.dataset.baselineId;
    const canPublish = root.dataset.canPublish === '1';
    const skeleton = document.getElementById('qmsBaselineSkeleton');
    const errorBox = document.getElementById('qmsBaselineError');
    const content = document.getElementById('qmsBaselineContent');
    const statusBadge = document.getElementById('qmsStatusBadge');
    const btnPublish = document.getElementById('btnPublish');
    const publishSpinner = document.getElementById('publishSpinner');
    const treeEl = document.getElementById('qmsDefinitionTree');
    const treeError = document.getElementById('qmsTreeError');

    const t = (k, fallback) => (L[k] || fallback || k);
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const setText = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = (v === null || v === undefined || v === '') ? '-' : String(v); };
    const setHtml = (id, v) => { const el = document.getElementById(id); if (el) el.innerHTML = (v === null || v === undefined || v === '') ? '-' : String(v); };
    // Two-line stacked date/time (project standard, cf. list page): "Jun 18, 26" + muted "05:04 PM".
    const fmtDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        if (Number.isNaN(d.getTime())) return String(v);
        const locale = window.CurrentLanguage || undefined;
        const datePart = new Intl.DateTimeFormat(locale, { month: 'short', day: '2-digit', year: '2-digit' }).format(d);
        const timePart = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hour12: true }).format(d);
        return `<span class="d-inline-flex flex-column lh-sm"><span>${datePart}</span><small class="text-muted">${timePart}</small></span>`;
    };
    const antiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const mapReason = (rc) => ({
        VALIDATION_FAILED: t('ReasonValidationFailed'),
        CONFLICT: t('ReasonConflict'),
        PERM_DENIED: t('ReasonPermDenied'),
        NOT_FOUND_NON_LEAKAGE: t('ReasonNotFound')
    }[rc] || t('ErrorOccurred'));

    const showError = (message, corr) => {
        skeleton?.classList.add('d-none');
        errorBox.classList.remove('d-none');
        errorBox.innerHTML = esc(message) + (corr ? `<div class="small mt-2">${t('CorrelationId')}: <code>${esc(corr)}</code></div>` : '');
    };

    const getJson = async (url) => {
        const res = await fetch(url, { method: 'GET', credentials: 'same-origin' });
        let json = null;
        try { json = await res.json(); } catch (e) { /* ignore */ }
        return { ok: res.ok, status: res.status, json };
    };

    const renderStatusBadge = (status) => {
        const s = String(status || '').toUpperCase();
        statusBadge.className = 'badge ' + (s === 'PUBLISHED' ? 'bg-label-success' : s === 'DRAFT' ? 'bg-label-warning' : 'bg-label-secondary');
        statusBadge.textContent = s === 'PUBLISHED' ? t('StatusPublished') : s === 'DRAFT' ? t('StatusDraft') : t('Unknown');
    };

    const renderDetail = (d) => {
        setText('md-baselineReleaseId', d.baselineReleaseId);
        setText('md-version', d.baselineVersion);
        setText('md-definitionCount', d.definitionCount);
        setText('md-snapshotHash', d.snapshotHash);
        setHtml('md-createdAt', fmtDate(d.createdAt));
        setHtml('md-publishedAt', fmtDate(d.publishedAt));
        renderStatusBadge(d.status);
        const subtitle = document.getElementById('qmsBaselineSubtitle');
        if (subtitle) subtitle.textContent = d.baselineReleaseId || '';
        if (btnPublish && canPublish && String(d.status).toUpperCase() === 'DRAFT') {
            btnPublish.classList.remove('d-none');
        }
        skeleton?.classList.add('d-none');
        content?.classList.remove('d-none');
    };

    // Build nested tree from a flat list (parentCanonicalId + displayOrder), preserving order, never flattening.
    const buildTree = (items) => {
        const byParent = new Map();
        items.forEach((it) => {
            const key = it.parentCanonicalId || '__root__';
            if (!byParent.has(key)) byParent.set(key, []);
            byParent.get(key).push(it);
        });
        byParent.forEach((arr) => arr.sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0)));
        return byParent;
    };

    // jsTree node from the flat definition (parentCanonicalId hierarchy). Folder name is atomic (may contain '/'),
    // shown verbatim and escaped. Extra metadata is surfaced via the node tooltip (a_attr.title).
    const toJstreeNode = (node, byParent) => {
        const children = byParent.get(node.canonicalId) || [];
        const titleBits = [];
        if (node.fullPath) titleBits.push(`${t('FullPath')}: ${node.fullPath}`);
        if (node.requiredByScope) titleBits.push(node.requiredByScope);
        if (node.defaultClassificationLevel) titleBits.push(node.defaultClassificationLevel);
        return {
            text: `<span class="fw-medium">${esc(node.name || '')}</span>`,
            type: 'folder', // every QMS definition node is a documentation folder
            state: { opened: true },
            a_attr: { title: titleBits.join('  ·  ') },
            children: children.map((c) => toJstreeNode(c, byParent))
        };
    };

    const $tree = () => window.jQuery(treeEl);
    const treeApi = () => (window.jQuery && window.jQuery.fn.jstree ? $tree().jstree(true) : null);

    const setupTreeToolbar = () => {
        // Drive the jsTree instance (initialized later by renderTree) via its public API.
        document.getElementById('btnExpandAll')?.addEventListener('click', () => treeApi()?.open_all());
        document.getElementById('btnCollapseAll')?.addEventListener('click', () => treeApi()?.close_all());
        const search = document.getElementById('qmsTreeSearch');
        let debounce;
        search?.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => treeApi()?.search(search.value || ''), 200);
        });
    };

    const emptyStateHtml = () =>
        `<div class="text-center py-5">` +
        `<i class="bx bx-folder-open mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>` +
        `<h6 class="mb-2">${esc(t('NoDefinitionsHeading'))}</h6>` +
        `<p class="text-muted mb-0 mx-auto" style="max-width:520px;">${esc(t('NoDefinitions'))}</p>` +
        `</div>`;

    // When the tree has no definitions: hide the tree toolbar (search + expand/collapse) and disable Publish
    // with an explanatory tooltip (a disabled <button> needs pointer-events re-enabled for the tooltip to show).
    const applyTreeEmptyConstraints = (isEmpty) => {
        document.getElementById('qmsTreeToolbar')?.classList.toggle('d-none', isEmpty);
        if (!btnPublish) return;
        const bs = window.bootstrap;
        if (isEmpty) {
            btnPublish.disabled = true;
            btnPublish.style.pointerEvents = 'auto';
            btnPublish.setAttribute('data-bs-toggle', 'tooltip');
            btnPublish.setAttribute('data-bs-placement', 'top');
            btnPublish.setAttribute('title', t('PublishNeedsDefinitions'));
            bs?.Tooltip?.getOrCreateInstance(btnPublish);
        } else {
            bs?.Tooltip?.getInstance(btnPublish)?.dispose();
            btnPublish.disabled = false;
            btnPublish.style.pointerEvents = '';
            btnPublish.removeAttribute('data-bs-toggle');
            btnPublish.removeAttribute('title');
        }
    };

    const renderTree = (items) => {
        if (!window.jQuery || !window.jQuery.fn.jstree) {
            treeEl.innerHTML = emptyStateHtml();
            return;
        }
        const $t = $tree();
        if ($t.jstree(true)) $t.jstree('destroy');
        if (!items || !items.length) {
            treeEl.innerHTML = emptyStateHtml();
            applyTreeEmptyConstraints(true);
            return;
        }
        const byParent = buildTree(items);
        const roots = byParent.get('__root__') || [];
        const data = roots.map((r) => toJstreeNode(r, byParent));
        const theme = window.jQuery('html').attr('data-bs-theme') === 'dark' ? 'default-dark' : 'default';
        $t.jstree({
            core: { themes: { name: theme, dots: true, responsive: true }, data: data, multiple: false },
            plugins: ['types', 'search', 'wholerow'],
            types: {
                default: { icon: 'icon-base bx bx-folder text-warning' },
                folder: { icon: 'icon-base bx bx-folder text-warning' },
                file: { icon: 'icon-base bx bx-file text-muted' }
            },
            search: { show_only_matches: true, show_only_matches_children: true, close_opened_onclear: false }
        });
        applyTreeEmptyConstraints(false);
    };

    const loadDefinitions = async () => {
        const { json } = await getJson(`/DocumentManagementQmsBaselines/definitions/${baselineId}`);
        if (json && json.isSuccessful) {
            renderTree(json.data || json.Data || []);
        } else {
            treeError.classList.remove('d-none');
            treeError.textContent = mapReason(json?.reason_code || json?.reasonCode);
        }
    };

    const load = async () => {
        if (!baselineId) { showError(t('NotFound')); return; }
        const { status, json } = await getJson(`/DocumentManagementQmsBaselines/detail/${baselineId}`);
        if (json && json.isSuccessful && json.data) {
            renderDetail(json.data);
            await loadDefinitions();
        } else if (status === 404) {
            showError(t('NotFound'), json?.correlation_id);
        } else if (status === 403) {
            showError(t('AccessDenied'), json?.correlation_id);
        } else {
            showError(mapReason(json?.reason_code || json?.reasonCode), json?.correlation_id || json?.correlationId);
        }
    };

    btnPublish?.addEventListener('click', () => {
        const doPublish = async () => {
            btnPublish.disabled = true; publishSpinner.classList.remove('d-none');
            try {
                const token = antiForgeryToken();
                const fd = new FormData();
                fd.append('expectedVersion', '0'); // server still guards with the loaded version
                if (token) fd.append('__RequestVerificationToken', token);
                const res = await fetch(`/DocumentManagementQmsBaselines/publish/${baselineId}`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'RequestVerificationToken': token },
                    body: fd
                });
                let json = null; try { json = await res.json(); } catch (e) { /* ignore */ }
                if (json && json.isSuccessful) {
                    window.showToast?.(t('PublishSuccess'), 'success');
                    btnPublish.classList.add('d-none');
                    await load();
                } else {
                    window.showToast?.(mapReason(json?.reason_code || json?.reasonCode), 'error');
                    btnPublish.disabled = false;
                }
            } catch (e) {
                console.error('[QmsBaselines] publish failed', e);
                window.showToast?.(t('ErrorOccurred'), 'error');
                btnPublish.disabled = false;
            } finally {
                publishSpinner.classList.add('d-none');
            }
        };
        if (typeof window.showConfirm === 'function') {
            window.showConfirm(t('PublishConfirm'), doPublish, { type: 'warning', confirmButtonText: t('Publish') });
        } else if (window.confirm(t('PublishConfirm'))) {
            doPublish();
        }
    });

    setupTreeToolbar();
    document.addEventListener('DOMContentLoaded', load);
})();
