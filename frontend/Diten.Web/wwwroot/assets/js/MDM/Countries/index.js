/**
 * Countries DataTables Initialization (Golden Module Pattern)
 */

'use strict';

const CountriesList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-countries');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const L = window.L10n || {};

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
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

    const fmtDateTime = (val) => {
        if (!val) return '-';
        try {
            const d = new Date(val);
            if (Number.isNaN(d.getTime())) return String(val);
            return d.toLocaleString();
        } catch (e) {
            return String(val);
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) return;

        document.getElementById('oc-title').innerText = data.name || '-';
        const subtitle = [data.iso2Code, data.iso3Code].filter(Boolean).join(' • ') || '-';
        document.getElementById('oc-subtitle').innerText = subtitle;

        document.getElementById('oc-iso2').innerText = data.iso2Code || '-';
        document.getElementById('oc-iso2-2').innerText = data.iso2Code || '-';
        document.getElementById('oc-iso3').innerText = data.iso3Code || '-';
        document.getElementById('oc-phonecode').innerText = data.phoneCode || '-';
        document.getElementById('oc-createdat').innerText = fmtDateTime(data.createdAt);
        document.getElementById('oc-updatedat').innerText = fmtDateTime(data.updatedAt);

        const statusEl = document.getElementById('oc-status');
        const status = statusObj[String(data.isActive)] || { title: L.Unknown || String(data.isActive), class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        document.getElementById('oc-btn-edit').href = `/Countries/Edit/${data.id}`;
    };

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;

        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[Countries QuickView] Could not parse row data', err);
            return null;
        }
    };

    const initDataTable = () => {
        if (!dtTableEl) return;

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
                attr: { title: L.Filter, 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasFilter' }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/countries',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            columns: [
                { data: 'id', name: 'control' },   // 0
                { data: 'id', name: 'checkbox' },  // 1
                { data: 'name', name: 'name' },    // 2
                { data: 'iso2Code', name: 'iso2Code' }, // 3
                { data: 'iso3Code', name: 'iso3Code' }, // 4
                { data: 'phoneCode', name: 'phoneCode' }, // 5
                { data: 'isActive', name: 'isActive' },   // 6
                { data: 'action', name: 'action' }        // 7
            ],
            preXhr: function () {
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
                {
                    targets: 6,
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
                <a href="/Countries/Details/${full['id']}" class="dropdown-item">${L.ViewDetails}</a>
                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                <a href="/Countries/Edit/${full['id']}" class="dropdown-item">${L.Edit}</a>
              </div>
            </div>`
                }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewCountry, {
                onclick: "window.location.href='/Countries/Create'"
            }, extraButtons, {
                exportColumns: [2, 3, 4, 5, 6],
                colvisColumns: [2, 3, 4, 5, 6],
                showAllColumns: [2, 3, 4, 5, 6]
            }),
            initComplete: function () {
                setupFilters(this.api());
            },
            drawCallback: function () {
                const count = [$('#FilterTransaction').val()].filter(Boolean).length;
                window.DtDefaults.updateVisualState(this.api(), count);
                $('#skeleton-loader').fadeOut(200);
            }
        }));

        dt.on('column-visibility.dt', function () {
            const count = [$('#FilterTransaction').val()].filter(Boolean).length;
            window.DtDefaults.updateVisualState(dt, count);
        });
    };

    const setupFilters = (api) => {
        const statusContainer = document.querySelector('.user_status');
        if (statusContainer) {
            const selectId = 'FilterTransaction';
            const select = document.createElement('select');
            select.id = selectId;
            select.className = 'form-select select2 text-capitalize';
            select.innerHTML = `<option value="">${L.SelectStatus}</option>`;
            statusContainer.appendChild(select);

            [statusObj.true, statusObj.false].filter(Boolean).forEach((status) => {
                const option = document.createElement('option');
                option.value = status.title;
                option.textContent = status.title;
                select.appendChild(option);
            });

            $(select).select2({
                placeholder: L.SelectStatus,
                dropdownParent: $('#offcanvasFilter'),
                minimumResultsForSearch: Infinity
            });
        }

        // Restore state values to UI
        const state = api.state.loaded();
        let initialFilterCount = 0;
        if (state) {
            const statusCol = api.column('isActive:name').index();
            if (statusCol !== undefined && state.columns[statusCol].search.search) {
                const val = state.columns[statusCol].search.search.replace(/\^|\$/g, '').replace(/\\/g, '');
                $('#FilterTransaction').val(val).trigger('change');
                if (val) initialFilterCount++;
            }
        }

        window.DtDefaults.updateVisualState(api, initialFilterCount);

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            const status = $('#FilterTransaction').val();
            api.column('isActive:name').search(status ? `^${status}$` : '', true, false);
            api.draw();

            const count = [status].filter(Boolean).length;
            window.DtDefaults.updateVisualState(api, count);

            bootstrap.Offcanvas.getInstance(document.getElementById('offcanvasFilter'))?.hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            $('#FilterTransaction').val('').trigger('change');
            api.state.clear();
            api.columns().search('').draw();
            window.DtDefaults.updateVisualState(api, 0);
        });
    };

    // =================== Checkbox Selection Management ===================

    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            ids.push(cb.value);
        });
        return ids;
    };

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

        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const totalVisible = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === totalVisible;
            headerCb.indeterminate = ids.length > 0 && ids.length < totalVisible;
        }
    };

    const clearSelection = () => {
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    const handleEvents = () => {
        if (!dtTableEl) return;

        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const row = dt.row(tr);
                const data = row.data();

                window.showConfirm?.('DeleteConfirmation', () => {
                    fetch(`${apiUrl}/api/countries/${data.id}`, {
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
                }, data.name);
            }

            const quickViewBtn = e.target.closest('.js-quick-view');
            if (quickViewBtn) {
                populateOffcanvas(tryParseRowJson(quickViewBtn));
            }
        });

        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        document.getElementById('btnClearSelection')?.addEventListener('click', () => {
            clearSelection();
        });

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
                    fetch(`${apiUrl}/api/countries/bulk`, {
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

document.addEventListener('DOMContentLoaded', () => CountriesList.init());

