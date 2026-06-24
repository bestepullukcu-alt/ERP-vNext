'use strict';

// MOD-0220 — Legal Entities (tenant shell, Master Data). Rich list (Görev 4) + Quick View (Görev 6).
// Backend list returns a plain array (Response<IReadOnlyList<T>>); the DataTable is client-side
// (serverSide:false). Filters are pushed to the gateway via query params (country, operationalStatus,
// evidenceStatus, baseCurrency, createdFrom, createdTo, incompleteOnly), then the list re-fetches.
// Create/Edit live on the full-page wizard (/LegalEntities/Wizard); the list only navigates there.
const LegalEntitiesList = (function () {
    let dt;
    let L = {};
    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const endpoint = '/LegalEntities/api';
    const wizardUrl = '/LegalEntities/Wizard';
    const detailsUrl = '/LegalEntities/Details';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    const baseOrder = [[1, 'asc']];

    let rowsData = [];
    const entityMap = {}; // legalEntityId -> { legalName, code } for parent resolution
    const lookupMaps = { legalForm: {}, organizationRole: {}, country: {}, currency: {} };

    let appliedFilters = {
        country: '', operationalStatus: '', evidenceStatus: '',
        baseCurrency: '', createdFrom: '', createdTo: '', incompleteOnly: false
    };

    const loadL10n = () => {
        const node = document.getElementById('legal-entities-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            const toPascal = (key) => key.charAt(0).toUpperCase() + key.slice(1);
            Object.keys(raw).forEach((key) => { L[toPascal(key)] = raw[key]; });
        } catch (error) {
            console.error('[LegalEntities] L10n payload could not be parsed.', error);
        }
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const operationalOf = (row) => String(row.operationalStatus ?? row.OperationalStatus ?? row.lifecycleState ?? row.LifecycleState ?? '').toUpperCase();

    // ─── Status badge palettes ───────────────────────────────────────────────
    const operationalBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = {
            DRAFT: { cls: 'bg-label-secondary', txt: L.StatusDraft || 'Draft' },
            INREVIEW: { cls: 'bg-label-info', txt: L.StatusInReview || 'In Review' },
            APPROVED: { cls: 'bg-label-info', txt: L.StatusApproved || 'Approved' },
            ACTIVE: { cls: 'bg-label-success', txt: L.StatusActive || 'Active' },
            SUSPENDED: { cls: 'bg-label-warning', txt: L.StatusSuspended || 'Suspended' },
            ARCHIVED: { cls: 'bg-label-secondary text-muted', txt: L.StatusArchived || 'Archived' }
        };
        const m = map[s] || { cls: 'bg-label-info', txt: s || '-' };
        return `<span class="badge ${m.cls}">${escapeHtml(m.txt)}</span>`;
    };

    const statutoryBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = {
            REGISTERED: { cls: 'bg-label-success', txt: L.StatusRegistered || 'Registered' },
            PENDING: { cls: 'bg-label-warning', txt: L.StatusPending || 'Pending' },
            SUSPENDED: { cls: 'bg-label-warning', txt: L.StatusSuspended || 'Suspended' },
            DISSOLVED: { cls: 'bg-label-secondary text-muted', txt: L.StatusDissolved || 'Dissolved' }
        };
        const m = map[s];
        return m ? `<span class="badge ${m.cls}">${escapeHtml(m.txt)}</span>` : `<span class="text-muted">-</span>`;
    };

    const evidenceBadge = (status) => {
        const s = String(status || '').toUpperCase();
        const map = {
            NOTSTARTED: { cls: 'bg-label-secondary', txt: L.EvidenceNotStarted || 'Not Started' },
            COMPLETE: { cls: 'bg-label-info', txt: L.EvidenceComplete || 'Complete' },
            VERIFIED: { cls: 'bg-label-success', txt: L.EvidenceVerified || 'Verified' }
        };
        const m = map[s];
        return m ? `<span class="badge ${m.cls}">${escapeHtml(m.txt)}</span>` : `<span class="text-muted">-</span>`;
    };

    const reloadWithSuccessToast = (messageKey, interpolationValue) => {
        window.DitenDataTable?.reloadWithToast?.(dt, dtTableEl, messageKey, interpolationValue);
    };

    const patchLifecycle = (id, action, confirmKey, toastKey, name) => {
        const typeMap = { activate: 'primary', suspend: 'warning', archive: 'warning' };
        const btnMap = { activate: L.Activate, suspend: L.Suspend, archive: L.Archive };
        window.showConfirm?.(L[confirmKey] || L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, { method: 'PATCH', headers: getAuthHeaders() });
                if (!response.ok) throw new Error(`${action} failed.`);
                reloadWithSuccessToast(toastKey, name);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName: name, type: typeMap[action] || 'primary', confirmButtonText: btnMap[action] });
    };

    const deleteRow = (id, name) => {
        window.showConfirm?.(L.AreYouSure, async () => {
            try {
                const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { method: 'DELETE', headers: getAuthHeaders() });
                if (!response.ok) throw new Error('Delete failed.');
                reloadWithSuccessToast('RecordDeleted', name);
            } catch (error) {
                console.error(error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
            }
        }, { entityName: name, type: 'danger', confirmButtonText: L.Delete });
    };

    const rowActionHandlers = {
        quickview: ({ id, row }) => { const rid = id || row?.id; if (rid) QuickView.open(rid); },
        details: ({ id, row }) => { const rid = id || row?.id; if (rid) window.location.href = `${detailsUrl}/${encodeURIComponent(rid)}`; },
        edit: ({ id, row }) => { const rid = id || row?.id; if (rid) window.location.href = `${wizardUrl}/${encodeURIComponent(rid)}`; },
        activate: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'activate', 'ActivateConfirm', 'RecordActivated', nameOf(row)); },
        suspend: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'suspend', 'SuspendConfirm', 'RecordSuspended', nameOf(row)); },
        archive: ({ id, row }) => { const rid = id || row?.id; if (rid) patchLifecycle(rid, 'archive', 'ArchiveConfirm', 'RecordArchived', nameOf(row)); },
        delete: ({ id, row }) => { const rid = id || row?.id; if (rid) deleteRow(rid, nameOf(row)); }
    };

    const nameOf = (row) => row?.legalName || row?.LegalName || row?.code || row?.Code || '';

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    // Backend rows expose `legalEntityId` (not `id`); normalize so the shared dispatcher + renderers work.
    const normalizeRow = (row) => ({ ...row, id: row.id || row.Id || row.legalEntityId || row.LegalEntityId });

    const lookupLabel = (mapKey, code) => (code ? (lookupMaps[mapKey][String(code)] || code) : '-');

    const parentLabel = (parentId) => {
        if (!parentId) return '-';
        const p = entityMap[String(parentId)];
        return p ? (p.legalName ? `${p.code ? p.code + ' — ' : ''}${p.legalName}` : (p.code || parentId)) : parentId;
    };

    const buildQuery = () => {
        const params = new URLSearchParams();
        if (appliedFilters.country) params.set('country', appliedFilters.country);
        if (appliedFilters.operationalStatus) params.set('operationalStatus', appliedFilters.operationalStatus);
        if (appliedFilters.evidenceStatus) params.set('evidenceStatus', appliedFilters.evidenceStatus);
        if (appliedFilters.baseCurrency) params.set('baseCurrency', appliedFilters.baseCurrency);
        if (appliedFilters.createdFrom) params.set('createdFrom', appliedFilters.createdFrom);
        if (appliedFilters.createdTo) params.set('createdTo', appliedFilters.createdTo);
        if (appliedFilters.incompleteOnly) params.set('incompleteOnly', 'true');
        const qs = params.toString();
        return qs ? `?${qs}` : '';
    };

    const rebuildEntityMap = () => {
        Object.keys(entityMap).forEach((k) => delete entityMap[k]);
        rowsData.forEach((r) => {
            const id = r.legalEntityId || r.LegalEntityId || r.id;
            if (id) entityMap[String(id)] = { legalName: r.legalName || r.LegalName || '', code: r.code || r.Code || '' };
        });
    };

    // ─── Lookups for table labels + filter selects (best-effort) ─────────────
    const fetchLookup = (mapKey, url, valueKey) => fetch(url, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r))
        .then((payload) => {
            const items = unwrapList(payload);
            items.forEach((it) => {
                const code = it.code ?? it.Code ?? it.value ?? it.Value;
                const name = it.name ?? it.Name ?? code;
                if (code != null) lookupMaps[mapKey][String(code)] = name;
            });
            return items;
        })
        .catch(() => []);

    const loadReferenceData = () => Promise.all([
        fetchLookup('legalForm', `${endpoint}/lookups/legal-form`),
        fetchLookup('organizationRole', `${endpoint}/lookups/organization-role`),
        fetchLookup('country', `${endpoint}/platform-lookups/countries`),
        fetchLookup('currency', `${endpoint}/platform-lookups/currencies`)
    ]).then(([, , countries, currencies]) => {
        populateFilterSelect('filterCountry', countries, L.Country);
        populateFilterSelect('filterBaseCurrency', currencies, L.BaseCurrency);
    });

    const populateFilterSelect = (selectId, items, placeholder) => {
        const select = document.getElementById(selectId);
        if (!select) return;
        select.innerHTML = `<option value="">${escapeHtml(placeholder || '')}</option>`;
        (items || []).forEach((it) => {
            const code = it.code ?? it.Code ?? it.value ?? it.Value;
            const name = it.name ?? it.Name ?? code;
            if (code == null) return;
            const opt = document.createElement('option');
            opt.value = code;
            opt.textContent = `${name}`;
            select.appendChild(opt);
        });
    };

    const initDataTable = () => {
        if (!dtTableEl || !window.DtDefaults) {
            console.error('[LegalEntities] DataTable element or DtDefaults not found.');
            return;
        }

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const dtConfig = window.DtDefaults.create({
            processing: true,
            serverSide: false,
            stateSave: false,
            order: baseOrder,
            colReorder: { columns: ':gt(1):not(:last-child)' },
            ajax: function (data, callback) {
                fetch(`${endpoint}${buildQuery()}`, { headers: getAuthHeaders() })
                    .then((response) => response.ok ? response.json() : Promise.reject(response))
                    .then((payload) => {
                        rowsData = unwrapList(payload).map(normalizeRow);
                        rebuildEntityMap();
                        callback({ data: rowsData });
                    })
                    .catch(() => {
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                        callback({ data: [] });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'legalName', name: 'legalName', render: escapeHtml },
                { data: 'code', name: 'code', render: (value) => `<span class="fw-medium font-monospace text-primary">${escapeHtml(value)}</span>` },
                { data: 'legalFormCode', name: 'legalForm', render: (v) => escapeHtml(lookupLabel('legalForm', v)) },
                { data: 'organizationRoleCode', name: 'orgRole', render: (v) => escapeHtml(lookupLabel('organizationRole', v)) },
                { data: 'countryCode', name: 'country', render: (v) => escapeHtml(lookupLabel('country', v)) },
                { data: 'baseCurrencyCode', name: 'baseCurrency', render: (v) => escapeHtml(lookupLabel('currency', v)) },
                { data: 'parentLegalEntityId', name: 'parent', render: (v) => escapeHtml(parentLabel(v)) },
                { data: 'statutoryStatus', name: 'statutoryStatus', render: (v) => statutoryBadge(v) },
                { data: 'operationalStatus', name: 'operationalStatus', render: (v, t, row) => operationalBadge(operationalOf(row)) },
                {
                    data: null,
                    name: 'action',
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (value, type, row) => {
                        const id = row.id;
                        const rowJson = JSON.stringify(row);
                        const status = operationalOf(row);
                        const baseAttrs = { 'data-id': id, 'data-json': rowJson };

                        const actions = [
                            { key: 'quickview', icon: 'bx bx-show', className: 'text-body', text: L.QuickView || '', attrs: baseAttrs },
                            { key: 'details', icon: 'bx bx-detail', text: L.ViewDetails || '', attrs: baseAttrs },
                            { key: 'edit', icon: 'bx bx-edit', className: 'js-edit-item', text: L.Edit || '', attrs: baseAttrs }
                        ];
                        if (status === 'DRAFT' || status === 'INREVIEW' || status === 'APPROVED') {
                            actions.push({ key: 'activate', icon: 'bx bx-check-circle', className: 'text-success', text: L.Activate || '', attrs: baseAttrs });
                        }
                        if (status === 'ACTIVE') {
                            actions.push({ key: 'suspend', icon: 'bx bx-pause-circle', className: 'text-warning', text: L.Suspend || '', attrs: baseAttrs });
                        }
                        if (status === 'ACTIVE' || status === 'SUSPENDED') {
                            actions.push({ key: 'archive', icon: 'bx bx-archive-in', className: 'text-warning', text: L.Archive || '', attrs: baseAttrs });
                        }
                        actions.push({ key: 'delete', icon: 'bx bx-trash', className: 'text-danger', text: L.Delete || '', attrs: baseAttrs });

                        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
                    }
                }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 },
                { targets: -1, title: L.Actions, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNew || '', {}, { filterBtn }, {
                exportColumns: saveViewColumnIndexes,
                colvisColumns: saveViewColumnIndexes
            }),
            initComplete: function () {
                const api = this.api();
                mountInlineFilter();
                initSelect2Filters();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        if (L.Showing) {
            dtConfig.language = dtConfig.language || {};
            dtConfig.language.info = `${L.Showing} _START_ - _END_ / _TOTAL_`;
        }

        dt = new DataTable(dtTableEl, dtConfig);

        window.DitenDataTable?.bindActionDispatcher?.({
            tableEl: dtTableEl,
            dt,
            onRowAction: rowActionHandlers
        });
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };

    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };

    const initSelect2Filters = () => {
        if (!window.jQuery?.fn?.select2) return;
        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: true,
                allowClear: true
            });
        });
    };

    const readFilters = () => ({
        country: document.getElementById('filterCountry')?.value || '',
        operationalStatus: document.getElementById('filterOperationalStatus')?.value || '',
        evidenceStatus: document.getElementById('filterEvidenceStatus')?.value || '',
        baseCurrency: document.getElementById('filterBaseCurrency')?.value || '',
        createdFrom: document.getElementById('filterCreatedFrom')?.value || '',
        createdTo: document.getElementById('filterCreatedTo')?.value || '',
        incompleteOnly: !!document.getElementById('filterIncompleteOnly')?.checked
    });

    const bindFilters = () => {
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readFilters();
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            appliedFilters = { country: '', operationalStatus: '', evidenceStatus: '', baseCurrency: '', createdFrom: '', createdTo: '', incompleteOnly: false };
            ['filterCountry', 'filterOperationalStatus', 'filterEvidenceStatus', 'filterBaseCurrency'].forEach((id) => {
                const el = document.getElementById(id);
                if (el) { el.value = ''; if (window.jQuery?.fn?.select2) $(el).val('').trigger('change'); }
            });
            ['filterCreatedFrom', 'filterCreatedTo'].forEach((id) => { const el = document.getElementById(id); if (el) el.value = ''; });
            const inc = document.getElementById('filterIncompleteOnly');
            if (inc) inc.checked = false;
            dt?.ajax.reload();
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });
    };

    const getAppliedFilterCount = () => Object.values(appliedFilters).filter((v) => v !== '' && v !== false).length;

    // ─── Add New → full-page wizard ──────────────────────────────────────────
    const bindAddNew = () => {
        document.addEventListener('click', (event) => {
            if (event.target.closest('.add-new')) {
                event.preventDefault();
                window.location.href = wizardUrl;
            }
        });
    };

    // ─── Quick View offcanvas (Görev 6) ──────────────────────────────────────
    const QuickView = (function () {
        const getOc = () => {
            const el = document.getElementById('offcanvasQuickView');
            return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
        };
        const setText = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value ?? '-'; };
        const setHtml = (id, html) => { const el = document.getElementById(id); if (el) el.innerHTML = html; };

        const renderLifecycle = (id, status) => {
            const host = document.getElementById('qvLifecycleActions');
            if (!host) return;
            const btns = [];
            if (status === 'DRAFT' || status === 'INREVIEW' || status === 'APPROVED') {
                btns.push(`<button type="button" class="btn btn-sm btn-success" data-qv-action="activate"><i class="bx bx-check-circle me-1"></i>${escapeHtml(L.Activate || '')}</button>`);
            }
            if (status === 'ACTIVE') {
                btns.push(`<button type="button" class="btn btn-sm btn-warning" data-qv-action="suspend"><i class="bx bx-pause-circle me-1"></i>${escapeHtml(L.Suspend || '')}</button>`);
            }
            if (status === 'ACTIVE' || status === 'SUSPENDED') {
                btns.push(`<button type="button" class="btn btn-sm btn-label-warning" data-qv-action="archive"><i class="bx bx-archive-in me-1"></i>${escapeHtml(L.Archive || '')}</button>`);
            }
            host.innerHTML = btns.join('');
            host.querySelectorAll('[data-qv-action]').forEach((b) => {
                b.addEventListener('click', () => {
                    const action = b.getAttribute('data-qv-action');
                    const name = document.getElementById('qvLegalName')?.textContent || '';
                    const map = {
                        activate: ['ActivateConfirm', 'RecordActivated'],
                        suspend: ['SuspendConfirm', 'RecordSuspended'],
                        archive: ['ArchiveConfirm', 'RecordArchived']
                    };
                    getOc()?.hide();
                    patchLifecycle(id, action, map[action][0], map[action][1], name);
                });
            });
        };

        const open = async (id) => {
            const oc = getOc();
            if (!oc) return;
            document.getElementById('quickViewLoading')?.classList.remove('d-none');
            document.getElementById('quickViewContent')?.classList.add('d-none');
            oc.show();
            try {
                const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() });
                if (!res.ok) throw new Error('load failed');
                const payload = await res.json();
                const d = payload.data || payload.Data || {};

                setText('qvLegalName', d.legalName || '-');
                setText('qvCode', d.code || '-');
                setText('qvDisplayName', d.displayName || '-');
                setText('qvLegalForm', lookupLabel('legalForm', d.legalFormCode));
                setText('qvOrgRole', lookupLabel('organizationRole', d.organizationRoleCode));
                setText('qvCountry', lookupLabel('country', d.countryCode));
                setText('qvBaseCurrency', lookupLabel('currency', d.baseCurrencyCode));
                setText('qvParent', parentLabel(d.parentLegalEntityId));
                setText('qvRegistrationNumber', d.registrationNumber || '-');
                setText('qvTaxId', d.taxId || '-');
                setText('qvOfficialEmail', d.officialEmail || '-');

                const status = String(d.operationalStatus || d.lifecycleState || '').toUpperCase();
                setHtml('qvOperationalStatus', operationalBadge(status));
                setHtml('qvStatutoryStatus', statutoryBadge(d.statutoryStatus));
                setHtml('qvEvidenceStatus', evidenceBadge(d.evidenceStatus));
                const refOk = d.referenceable === true || d.referenceable === 'true';
                setHtml('qvReferenceable', `<span class="badge ${refOk ? 'bg-label-success' : 'bg-label-secondary'}">${escapeHtml(L.Referenceable || 'Referenceable')}: ${refOk ? (L.Yes || 'Yes') : (L.No || 'No')}</span>`);

                const score = Number(d.completenessScore ?? 0);
                const pct = Math.max(0, Math.min(100, score));
                setText('qvCompletenessLabel', `${pct}%`);
                const bar = document.getElementById('qvCompletenessBar');
                if (bar) {
                    bar.style.width = `${pct}%`;
                    bar.className = `progress-bar ${pct >= 100 ? 'bg-success' : pct >= 50 ? 'bg-info' : 'bg-warning'}`;
                }

                const link = document.getElementById('qvViewDetailsLink');
                if (link) link.href = `${detailsUrl}/${encodeURIComponent(id)}`;
                renderLifecycle(id, status);

                document.getElementById('quickViewLoading')?.classList.add('d-none');
                document.getElementById('quickViewContent')?.classList.remove('d-none');
            } catch (error) {
                console.error('[LegalEntities] Quick view load failed.', error);
                window.showToast?.(L.ErrorOccurred || '', 'error');
                getOc()?.hide();
            }
        };

        return { open };
    })();

    // After a wizard save redirects back here, surface the success toast it stashed.
    const flushWizardToast = () => {
        try {
            const msg = sessionStorage.getItem('le-toast');
            if (msg) { sessionStorage.removeItem('le-toast'); window.showToast?.(msg, 'success'); }
        } catch { /* ignore */ }
    };

    const init = () => {
        loadL10n();
        flushWizardToast();
        bindFilters();
        bindAddNew();
        loadReferenceData().finally(() => initDataTable());
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
