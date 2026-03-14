/**
 * Legal Entities DataTables Initialization (Refactored Module Pattern)
 */

'use strict';

const LegalEntitiesList = (function () {
    // Constants & Variables
    let dt;
    const dtTableEl = document.querySelector('.datatables-legal-entities');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';

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
            host.classList.add('mx-3');
            return;
        }

        // Fallback: place it before the table within the same container
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.add('mx-3');
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
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
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
        });
    };

    /**
     * Setup Filters
     */
    const setupFilters = (api) => {
        const collapseEl = document.getElementById(filterCollapseId);
        const initSelect2 = () => {
            const $companyType = $('#UserPlan');
            if ($companyType.length && !$companyType.hasClass('select2-hidden-accessible')) {
                $companyType.select2({
                    placeholder: L.CompanyType,
                    dropdownParent: $('#' + filterHostId),
                    minimumResultsForSearch: 0,
                    width: '100%'
                });
            }

            const $status = $('#FilterTransaction');
            if ($status.length && !$status.hasClass('select2-hidden-accessible')) {
                $status.select2({
                    placeholder: L.SelectStatus,
                    dropdownParent: $('#' + filterHostId),
                    minimumResultsForSearch: 0,
                    width: '100%'
                });
            }
        };

        if (collapseEl && !collapseEl.dataset.select2InitBound) {
            collapseEl.dataset.select2InitBound = '1';
            collapseEl.addEventListener('shown.bs.collapse', () => {
                initSelect2();
            });
        }

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

            // Select2 is initialized lazily when the collapse is shown
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

            // Select2 is initialized lazily when the collapse is shown
        }

        // Restore state values to UI
        const state = api.state.loaded();
        let initialFilterCount = 0;

        if (state) {
            const companyTypeCol = api.column('companyType:name').index();
            const statusCol = api.column('isActive:name').index();

            if (companyTypeCol !== undefined && state.columns[companyTypeCol].search.search) {
                const val = state.columns[companyTypeCol].search.search.replace(/\^|\$/g, '').replace(/\\/g, '');
                $('#UserPlan').val(val).trigger('change');
                if (val) initialFilterCount++;
            }
            if (statusCol !== undefined && state.columns[statusCol].search.search) {
                const val = state.columns[statusCol].search.search.replace(/\^|\$/g, '').replace(/\\/g, '');
                $('#FilterTransaction').val(val).trigger('change');
                if (val) initialFilterCount++;
            }
        }

        // Initial visual sync
        window.DtDefaults.updateVisualState(api, initialFilterCount);

        // Buttons
        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
            const companyType = $('#UserPlan').val();
            const status = $('#FilterTransaction').val();

            api.column('companyType:name').search(companyType ? `^${companyType}$` : '', true, false);
            api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
            api.draw();

            let count = [companyType, status].filter(Boolean).length;
            window.DtDefaults.updateVisualState(api, count);

            const el = document.getElementById(filterCollapseId);
            if (el) bootstrap.Collapse.getOrCreateInstance(el).hide();
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', () => {
            $('#UserPlan, #FilterTransaction').val('').trigger('change');
            api.state.clear(); // Clear saved state
            api.columns().search('').draw();
            window.DtDefaults.updateVisualState(api, 0);
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

    return { init: () => { initDataTable(); handleEvents(); } };
})();

document.addEventListener('DOMContentLoaded', () => LegalEntitiesList.init());
