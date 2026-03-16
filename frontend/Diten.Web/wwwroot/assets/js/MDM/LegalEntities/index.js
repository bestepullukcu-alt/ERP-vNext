/**
 * Legal Entities DataTables Initialization (Refactored Module Pattern)
 */

'use strict';

const LegalEntitiesList = (function () {
    // Constants & Variables
    let dt;
    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    let L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    let saveFilterArmed = false;
    const baseOrder = [[2, 'desc']];

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }
        L = L || {};
    };

    // Get TenantId from logged-in user or fallback
    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    /**
     * Helper to get cookie value
     */
    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const getCurrentUserKey = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.id || user.userId || 'anon';
        } catch (e) {
            return 'anon';
        }
    };

    const getDefaultViewStorageKey = () => {
        const tableId = dtTableEl?.id || '';
        // v2 standard: dt:view-default:{tenantId}:{userId}:{module}:{tableId}
        return `dt:view-default:${getTenantId()}:${getCurrentUserKey()}:MDM.LegalEntities:${tableId}`;
    };

    const loadDefaultView = () => {
        try {
            const raw = localStorage.getItem(getDefaultViewStorageKey());
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    };

    const saveDefaultView = (view) => {
        try {
            localStorage.setItem(getDefaultViewStorageKey(), JSON.stringify(view || {}));
        } catch (e) { }
    };

    const getCurrentView = (api) => {
        const companyType = $('#UserPlan').val() || '';
        const status = $('#FilterTransaction').val() || '';
        const search = typeof api?.search === 'function' ? (api.search() || '') : '';

        let colVis = null;
        try {
            colVis = api?.columns?.().visible().toArray();
        } catch (e) {
            colVis = null;
        }

        let order = null;
        try {
            order = api?.order?.() || null;
        } catch (e) {
            order = null;
        }

        return { companyType: companyType, status: status, search: search, colVis: colVis, order: order };
    };

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const def = loadDefaultView() || null;
        const cur = getCurrentView(api);

        const curHasHiddenCols = Array.isArray(cur.colVis) ? cur.colVis.some(v => !v) : false;

        const ref = def || {
            companyType: '',
            status: '',
            search: '',
            colVis: Array.isArray(cur.colVis) ? cur.colVis.map(() => true) : null,
            order: baseOrder
        };

        const refColVis = Array.isArray(ref.colVis) ? ref.colVis : null;
        const curColVis = Array.isArray(cur.colVis) ? cur.colVis : null;
        const colVisEqual =
            Array.isArray(refColVis) &&
                Array.isArray(curColVis) &&
                refColVis.length === curColVis.length
                ? refColVis.every((v, i) => !!v === !!curColVis[i])
                : refColVis === curColVis; // both null

        const refOrder = Array.isArray(ref.order) ? ref.order : null;
        const curOrder = Array.isArray(cur.order) ? cur.order : null;
        const orderEqual =
            Array.isArray(refOrder) &&
                Array.isArray(curOrder) &&
                refOrder.length === curOrder.length
                ? refOrder.every((o, i) => String(o?.[0]) === String(curOrder[i]?.[0]) && String(o?.[1]) === String(curOrder[i]?.[1]))
                : refOrder === curOrder; // both null

        if (!def) {
            // No saved default: still show on any meaningful change from baseline
            return [cur.companyType, cur.status].filter(Boolean).length > 0 ||
                !!cur.search ||
                curHasHiddenCols ||
                !orderEqual;
        }

        return (String(cur.companyType || '') !== String(ref.companyType || '')) ||
            (String(cur.status || '') !== String(ref.status || '')) ||
            (String(cur.search || '') !== String(ref.search || '')) ||
            !colVisEqual ||
            !orderEqual;
    };

    const applyFilterValues = (api, values) => {
        const companyType = values?.companyType || '';
        const status = values?.status || '';

        api.column('companyType:name').search(companyType ? `^${companyType}$` : '', true, false);
        api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
    };

    /**
     * Mount inline filter panel right under DataTable toolbar row.
     * (_Filter.cshtml is rendered on the page, we relocate it near the filter button.)
     */
    const mountInlineFilter = () => {
        if (!dtTableEl) return;

        const host = document.getElementById(filterHostId);
        if (!host) return;

        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
            return;
        }

        // Fallback: place it before the table within the same container
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.add('px-3');
        }
    };

    /**
     * Some DataTables button render flows don't play nicely with Bootstrap's data-API.
     * Bind explicit toggle behavior for the inline collapse.
     */
    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el) return;
        if (btn.dataset.inlineFilterBound) return;
        btn.dataset.inlineFilterBound = '1';

        // Keep aria-expanded in sync
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));

        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const statusObj = {
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    };

    const getCompanyTypeLabel = (value) => {
        const map = {
            LimitedLiability: L.LimitedLiability,
            Corporation: L.Corporation,
            SoleProprietorship: L.SoleProprietorship
        };
        return map[value] || value || '-';
    };

    const populateOffcanvas = (data) => {
        if (!data) return;

        document.getElementById('oc-title').innerText = data.title || '-';

        const subtitleParts = [];
        if (data.companyType) subtitleParts.push(getCompanyTypeLabel(data.companyType));
        if (data.sector) subtitleParts.push(data.sector);
        document.getElementById('oc-subtitle').innerText = subtitleParts.filter(Boolean).join(' • ') || '-';

        document.getElementById('oc-company-type').innerText = data.companyType ? getCompanyTypeLabel(data.companyType) : '-';
        document.getElementById('oc-sector').innerText = data.sector || '-';
        document.getElementById('oc-org-role').innerText = data.organizationRole || '-';
        document.getElementById('oc-contact').innerText = data.contactPerson || '-';

        document.getElementById('oc-taxnumber').innerText = data.taxNumber || '-';
        document.getElementById('oc-phone').innerText = data.phone || '-';
        document.getElementById('oc-email').innerText = data.email || '-';
        document.getElementById('oc-website').innerText = data.website || '-';
        document.getElementById('oc-address').innerText = data.address || '-';
        document.getElementById('oc-taxoffice').innerText = data.taxOffice || '-';
        document.getElementById('oc-jurisdiction').innerText = data.taxJurisdiction || '-';
        document.getElementById('oc-currency').innerText = data.primaryCurrency || '-';

        const statusEl = document.getElementById('oc-status');
        const status = statusObj[String(data.isActive)] || { title: L.Unknown || String(data.isActive), class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        document.getElementById('oc-btn-edit').href = `/LegalEntities/Edit/${data.id}`;
    };

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;

        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[LegalEntities QuickView] Could not parse row data', err);
            return null;
        }
    };

    /**
     * Initialize DataTable
     */
    const initDataTable = () => {
        if (!dtTableEl) return;
        syncL10n();

        // Uzantı butonları (Merkezi butona ek olarak)
        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: function () {
                    window.showToast?.(L.ComingSoon, 'info');
                }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false'
                }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    saveDefaultView(getCurrentView(tableApi));
                    setSaveFilterVisible(false);
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            // Disable DataTables stateSave (2h cache). Persistence is handled only via Save Filter default view.
            stateSave: false,
            ajax: {
                url: apiUrl + '/api/legal-entities',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'title', name: 'title' },
                { data: 'taxNumber', name: 'taxNumber' },
                { data: 'taxOffice', name: 'taxOffice' },
                { data: 'email', name: 'email' },
                { data: 'phone', name: 'phone' },
                { data: 'companyType', name: 'companyType' },
                { data: 'isActive', name: 'isActive' },
                { data: 'action', name: 'action' }
            ],
            preXhr: function () {
                // Veri yüklenmeye başladığında skeleton'ı geri getir
                $('#skeleton-loader').fadeIn(100);
            },
            columnDefs: [
                { className: 'control', searchable: false, orderable: false, responsivePriority: 2, targets: 0, render: () => '' },
                {
                    targets: 1, orderable: false, searchable: false, responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: function (data) {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input" value="' + data + '">';
                    }
                },
                { targets: 5, render: (data) => data ? `<a href="mailto:${data}">${data}</a>` : '-' },
                {
                    targets: 7,
                    render: (data, type) => {
                        const label = getCompanyTypeLabel(data);
                        if (type === 'display') return label;
                        return data || ''; // keep filters/sort stable across languages
                    }
                },
                {
                    targets: 8,
                    render: (data, type) => {
                        const status = statusObj[String(data)] || { title: L.Unknown || String(data), class: 'bg-label-primary' };
                        if (type === 'display') {
                            return `<span class="badge ${status.class}" text-capitalized>${status.title}</span>`;
                        }
                        return status.title || '';
                    }
                },
                {
                    targets: -1, title: L.Actions, searchable: false, orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) => `
            <div class="d-flex align-items-center">
              <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
              <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
              <div class="dropdown-menu dropdown-menu-end m-0">
                <a href="/LegalEntities/Details/${full['id']}" class="dropdown-item">${L.ViewDetails}</a>
                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                <a href="/LegalEntities/Edit/${full['id']}" class="dropdown-item">${L.Edit}</a>
              </div>
            </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewCompany, {
                onclick: "window.location.href='/LegalEntities/Create'"
            }, extraButtons),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
                // Arm Save Filter change detection after initial state/default restores are done
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                // Her çizimde (arama, sayfalama vb.) görsel durumları güncelle
                const count = [$('#UserPlan').val(), $('#FilterTransaction').val()].filter(Boolean).length;
                window.DtDefaults.updateVisualState(this.api(), count);

                // Skeleton'ı gizle
                $('#skeleton-loader').fadeOut(200);
            }
        }));

        // Sütun görünürlüğü değiştiğinde görsel durumları tetikle
        dt.on('column-visibility.dt', function () {
            const count = [$('#UserPlan').val(), $('#FilterTransaction').val()].filter(Boolean).length;
            window.DtDefaults.updateVisualState(dt, count);
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        // Global search changes should also enable Save Filter
        dt.on('search.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        // Sorting changes should also enable Save Filter (sorting is part of saved default)
        dt.on('order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    /**
     * Setup Filters
     */
    const setupFilters = (api) => {
        const renderFilterTrigger = (label, hasValue) => {
            const $wrap = $('<span class="dt-filter-trigger"></span>');
            $wrap.append($('<span class="dt-filter-trigger-label"></span>').text(label || ''));
            if (hasValue) {
                $wrap.append($('<span class="badge rounded-pill bg-primary-subtle text-primary dt-filter-trigger-badge ms-2"></span>').text('1'));
            }
            return $wrap;
        };

        const initSelect2 = () => {
            const $dropdownParent = $('#' + filterHostId).closest('.card');

            const clampInlineFilterDropdown = () => {
                // Clamp the open dropdown into the viewport to prevent page scrollbars (x/y) while open.
                // Applies to inline filter dropdowns only (dropdownCssClass: dt-inline-filter-dropdown).
                window.requestAnimationFrame(() => {
                    const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                    if (!dropdown) return;

                    dropdown.style.transform = '';
                    const rect = dropdown.getBoundingClientRect();
                    const pad = 8;

                    let tx = 0;
                    let ty = 0;

                    if (rect.right > window.innerWidth - pad) tx -= rect.right - (window.innerWidth - pad);
                    if (rect.left < pad) tx += pad - rect.left;

                    if (rect.bottom > window.innerHeight - pad) ty -= rect.bottom - (window.innerHeight - pad);
                    if (rect.top < pad) ty += pad - rect.top;

                    if (tx || ty) dropdown.style.transform = `translate(${tx}px, ${ty}px)`;
                });
            };

            const $companyType = $('#UserPlan');
            if ($companyType.length && !$companyType.hasClass('select2-hidden-accessible')) {
                $companyType.select2({
                    placeholder: L.CompanyType,
                    dropdownParent: $dropdownParent.length ? $dropdownParent : $(document.body),
                    minimumResultsForSearch: 0,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    templateSelection: (data) => renderFilterTrigger(L.CompanyType, !!(data && data.id)),
                    width: '100%'
                });
                $companyType.on('select2:open', clampInlineFilterDropdown);
            }

            const $status = $('#FilterTransaction');
            if ($status.length && !$status.hasClass('select2-hidden-accessible')) {
                $status.select2({
                    placeholder: (L.Status || L.SelectStatus),
                    dropdownParent: $dropdownParent.length ? $dropdownParent : $(document.body),
                    minimumResultsForSearch: 0,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    templateSelection: (data) => renderFilterTrigger((L.Status || L.SelectStatus), !!(data && data.id)),
                    width: '100%'
                });
                $status.on('select2:open', clampInlineFilterDropdown);
            }
        };

        // Company Type Filter
        const companyTypeContainer = document.querySelector('.user_plan');
        if (companyTypeContainer) {
            const selectId = 'UserPlan';
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'filter-select text-capitalize';
            select.innerHTML = `<option value="">${L.CompanyType}</option>`;
            companyTypeContainer.appendChild(select);

            [
                { value: 'LimitedLiability', label: L.LimitedLiability },
                { value: 'Corporation', label: L.Corporation },
                { value: 'SoleProprietorship', label: L.SoleProprietorship }
            ].forEach((opt) => {
                const option = document.createElement('option');
                option.value = opt.value;
                option.textContent = opt.label || opt.value;
                select.appendChild(option);
            });
        }

        // Status filter (special handling for statusObj)
        const statusContainer = document.querySelector('.user_status');
        if (statusContainer) {
            const selectId = 'FilterTransaction';
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'filter-select text-capitalize';
            select.innerHTML = `<option value="">${L.SelectStatus}</option>`;
            statusContainer.appendChild(select);

            [
                statusObj.true,
                statusObj.false
            ].filter(Boolean).forEach((status) => {
                const option = document.createElement('option');
                option.value = status.title;
                option.textContent = status.title;
                select.appendChild(option);
            });
        }

        // Initialize Select2 immediately to prevent FOUC when opening the panel
        initSelect2();

        let initialFilterCount = 0;

        // Apply user's saved default view (if any). Otherwise keep a clean state on load.
        const defaultView = loadDefaultView();
        if (defaultView) {
            $('#UserPlan').val(defaultView.companyType || '').trigger('change');
            $('#FilterTransaction').val(defaultView.status || '').trigger('change');

            if (typeof defaultView.search === 'string') {
                api.search(defaultView.search);
            }

            if (Array.isArray(defaultView.colVis)) {
                defaultView.colVis.forEach((vis, idx) => {
                    try { api.column(idx).visible(!!vis, false); } catch (e) { }
                });
            }

            if (Array.isArray(defaultView.order)) {
                try { api.order(defaultView.order); } catch (e) { }
            }

            applyFilterValues(api, defaultView);
            api.draw();
            initialFilterCount = [defaultView.companyType, defaultView.status].filter(Boolean).length;
        }

        // Initial visual sync
        window.DtDefaults.updateVisualState(api, initialFilterCount);

        // Save Filter button visibility sync (hidden by default)
        let filtersTouched = false;
        $('#UserPlan, #FilterTransaction')
            .off('change.saveFilter')
            .on('change.saveFilter', () => {
                filtersTouched = true;
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            });
        setSaveFilterVisible(false);

        // Buttons
        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
                const companyType = $('#UserPlan').val();
                const status = $('#FilterTransaction').val();

                applyFilterValues(api, { companyType: companyType, status: status });
                api.draw();

                let count = [companyType, status].filter(Boolean).length;
                window.DtDefaults.updateVisualState(api, count);
                filtersTouched = true;

                const el = document.getElementById(filterCollapseId);
                if (el) bootstrap.Collapse.getOrCreateInstance(el).hide();
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', () => {
                const def = loadDefaultView();
                if (def) {
                    $('#UserPlan').val(def.companyType || '').trigger('change');
                    $('#FilterTransaction').val(def.status || '').trigger('change');

                    if (typeof def.search === 'string') {
                        api.search(def.search);
                    }

                    if (Array.isArray(def.colVis)) {
                        def.colVis.forEach((vis, idx) => {
                            try { api.column(idx).visible(!!vis, false); } catch (e) { }
                        });
                    }

                    if (Array.isArray(def.order)) {
                        try { api.order(def.order); } catch (e) { }
                    } else {
                        try { api.order(baseOrder); } catch (e) { }
                    }

                    applyFilterValues(api, def);
                    api.draw();
                    window.DtDefaults.updateVisualState(api, [def.companyType, def.status].filter(Boolean).length);
                } else {
                    $('#UserPlan, #FilterTransaction').val('').trigger('change');
                    applyFilterValues(api, { companyType: '', status: '' });

                    try { api.search(''); } catch (e) { }
                    try { api.columns().visible(true, false); } catch (e) { }
                    try { api.order(baseOrder); } catch (e) { }

                    api.draw();
                    window.DtDefaults.updateVisualState(api, 0);
                }

                // Reset keeps panel open
                filtersTouched = false;
                setSaveFilterVisible(false);
            });
        }
    };

    // =================== Checkbox Selection Management ===================

    /**
     * Get all selected IDs from checked checkboxes
     */
    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            ids.push(cb.value);
        });
        return ids;
    };

    /**
     * Update Bulk Action Bar visibility and selected count
     */
    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (!bar || !countEl) return;

        if (ids.length > 0) {
            bar.classList.remove('d-none');
            countEl.textContent = ids.length;
        } else {
            bar.classList.add('d-none');
            countEl.textContent = '0';
        }

        // Sync header checkbox
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const totalVisible = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === totalVisible;
            headerCb.indeterminate = ids.length > 0 && ids.length < totalVisible;
        }
    };

    /**
     * Clear all checkboxes
     */
    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    // =================== Event Handlers ===================

    /**
     * Handle UI Events
     */
    const handleEvents = () => {
        if (!dtTableEl) return;

        // --- Single row delete ---
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const row = dt.row(tr);
                const data = row.data();

                window.showConfirm?.('DeleteConfirmation', () => {
                    fetch(`${apiUrl}/api/legal-entities/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    })
                        .then(res => {
                            if (res.ok) {
                                row.remove().draw();
                                window.showToast?.('RecordDeleted', 'success');
                            } else window.showToast?.('ErrorOccurred', 'error');
                        })
                        .catch(() => window.showToast?.('ErrorOccurred', 'error'));
                }, data.title);
            }

            const quickViewBtn = e.target.closest('.js-quick-view');
            if (quickViewBtn) {
                populateOffcanvas(tryParseRowJson(quickViewBtn));
            }
        });

        // --- Row checkbox change ---
        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        // --- Header "Select All" checkbox ---
        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        // --- Clear selection button ---
        document.getElementById('btnClearSelection')?.addEventListener('click', () => {
            clearSelection();
        });

        // --- Bulk Delete button ---
        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (ids.length === 0) return;

            const confirmMsg = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);

            Swal.fire({
                title: L.AreYouSure,
                html: `<div class="mb-2">${confirmMsg}</div>`,
                iconHtml: '<div class="swal-icon-circle"><i class="bx bx-trash"></i></div>',
                showCancelButton: true,
                confirmButtonText: L.BulkDelete,
                cancelButtonText: L.Cancel,
                width: '400px',
                padding: '2.5rem 1.5rem 2rem',
                customClass: {
                    popup: 'rounded-4 shadow-lg',
                    title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                    htmlContainer: 'text-muted mb-3 d-block w-100 text-center',
                    actions: 'd-flex justify-content-center mt-4 w-100',
                    confirmButton: 'btn btn-danger waves-effect waves-light mx-2',
                    cancelButton: 'btn btn-label-secondary waves-effect mx-2',
                    icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
                },
                buttonsStyling: false,
                reverseButtons: true
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch(`${apiUrl}/api/legal-entities/bulk`, {
                        method: 'DELETE',
                        headers: {
                            ...getAuthHeaders(),
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({ ids: ids })
                    })
                        .then(res => {
                            if (res.ok) {
                                return res.json();
                            }
                            throw new Error('Bulk delete failed');
                        })
                        .then(data => {
                            window.showToast?.((L.BulkDeleteSuccess || '').replace('{0}', data.deletedCount), 'success');
                            clearSelection();
                            dt.ajax.reload();
                        })
                        .catch(() => window.showToast?.('ErrorOccurred', 'error'));
                }
            });
        });
    };

    return {
        init: () => {
            syncL10n();
            initDataTable();
            handleEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
