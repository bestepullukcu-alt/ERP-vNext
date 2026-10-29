'use strict';

(function () {
    const tableEl = document.querySelector('.datatables-pvg-case-intake-triage');
    if (!tableEl || !window.DitenDataTable?.createList) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const alertEl = document.getElementById('pvg-list-alert');
    const statusEl = document.getElementById('pvg-list-status');
    const skeletonEl = document.getElementById('skeleton-loader');
    const filterCollapseId = 'inlineFilterCollapse';
    const valueOf = (row, key) => row?.[key] ?? row?.[key.charAt(0).toUpperCase() + key.slice(1)] ?? '';
    const itemsOf = json => {
        const source = json?.items ?? json?.Items ?? json?.data?.items ?? json?.Data?.Items ?? json?.data ?? json?.Data ?? [];
        return Array.isArray(source) ? source : [];
    };
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const safeProxyUrl = path => {
        if (typeof path !== 'string' || !path.startsWith(`${endpoint}/`) || path.includes('://') || path.startsWith('//')) {
            throw new Error('Invalid same-origin PVG proxy endpoint.');
        }

        return path;
    };

    const rowActionHandlers = {
        quickView: ({ trigger }) => {
            const id = trigger?.closest('.js-quick-view')?.getAttribute('data-id');
            if (id) window.location.assign(`/Pharmacovigilance/CaseIntakeTriage/Details/${encodeURIComponent(id)}`);
        },
        edit: ({ id }) => {
            if (id) window.location.assign(`/Pharmacovigilance/CaseIntakeTriage/Edit/${encodeURIComponent(id)}`);
        }
    };

    // The list is the shared list component's (DitenDataTable.createList, server mode): it sends start, length and the
    // applied status filter to the same-origin proxy and reads { data: { items, total, filteredTotal } } back. The
    // service sorts and searches nothing, so both stay off (Known gap) and the table shows the service's own counts.
    tableEl.addEventListener('preXhr.dt', () => {
        showLoading();
        setListStatus(t('Loading'));
    });

    tableEl.addEventListener('xhr.dt', () => {
        hideLoading();
    });

    window.DitenDataTable.createList({
        tableEl,
        dataMode: 'server',
        ajax: {
            url: safeProxyUrl(`${endpoint}/list`),
            type: 'GET',
            // BL-515: a CONTROLLED refusal (a reason code from the service) is this page's own sentence and stays here.
            // Every other failure - session (401), permission (403), outage - is the list component's: shown in the
            // table, in the reader's language, with a retry (`return false` hands it over).
            error: xhr => {
                hideLoading();
                const body = tryParseJson(xhr?.responseText);
                if (!hasReasonCode(body)) return false;
                showAlert(safeMessage(body, xhr?.status));
                setListStatus(alertEl?.textContent || t('ErrorOccurred'));
                return true;
            },
            headers: getAuthHeaders()
        },
        actions: { onRowAction: rowActionHandlers },
        toolbar: { addNewText: '', exportPermitted: false },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: filterCollapseId,
            applyBtn: 'pvgApplyFilter',
            resetBtn: 'pvgResetFilter',
            fields: [{ id: 'pvgStatusFilter', key: 'status', kind: 'single' }]
        },
        savedView: {
            moduleKey: 'Pharmacovigilance',
            pageKey: 'CaseIntakeTriage',
            saveViewColumnIndexes: [1, 2],
            defaultVisibleColumnIndexes: [1, 2],
            baseOrder: [[1, 'asc']]
        },
        onResponse: json => {
            hideAlert();
            const items = itemsOf(json);
            setListStatus(items.length ? '' : t('NoRecords'));
        },
        config: {
            ordering: false,
            searching: false,
            responsive: true,
            columns: [
                { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
                {
                    data: null,
                    orderable: false,
                    render: row => `<span class="fw-medium text-break">${escapeHtml(valueOf(row, 'intakeDraftId'))}</span>`
                },
                {
                    data: null,
                    orderable: false,
                    render: row => `<span class="badge bg-label-primary">${escapeHtml(valueOf(row, 'status'))}</span>`
                },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    className: 'cell-fit text-end all',
                    render: row => {
                        const id = valueOf(row, 'intakeDraftId');
                        return window.DitenDataTable.renderActions([
                            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: t('Details') } },
                            { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: t('Edit'), attrs: { 'data-id': id } }
                        ]);
                    }
                }
            ],
            language: {
                emptyTable: t('NoRecords'),
                loadingRecords: t('Loading'),
                processing: t('Loading')
            },
            initComplete: () => {
                hideLoading();
            }
        }
    }).catch(() => showAlert(t('ErrorOccurred')));

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, char => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[char]));
    }

    function safeMessage(body, statusCode) {
        const status = Number(statusCode || body?.statusCode || body?.StatusCode || 0);
        if (status === 401) return t('SessionExpired');
        if (status === 403) return t('NotAuthorized');

        const reason = safeCode(body?.reasonCode || body?.ReasonCode || body?.reason_code || '');
        const validation = body?.validationReasonCodes || body?.ValidationReasonCodes || [];
        const codes = Array.isArray(validation)
            ? validation.map(safeCode).filter(Boolean).join(', ')
            : reason;

        if (status === 409 || reason || codes) {
            return `${t('ControlledBlock')}: ${codes || reason || t('ReasonCode')}`;
        }

        return t('ErrorOccurred');
    }

    function hasReasonCode(body) {
        const validation = body?.validationReasonCodes || body?.ValidationReasonCodes || [];
        return Boolean(safeCode(body?.reasonCode || body?.ReasonCode || body?.reason_code || ''))
            || (Array.isArray(validation) && validation.map(safeCode).some(Boolean));
    }

    function safeCode(value) {
        return String(value || '').replace(/[^A-Za-z0-9._-]/g, '').slice(0, 96);
    }

    function tryParseJson(value) {
        try {
            return JSON.parse(value || '{}');
        } catch (error) {
            return {};
        }
    }

    function showAlert(message) {
        if (!alertEl) return;
        alertEl.textContent = message || t('ErrorOccurred');
        alertEl.classList.remove('d-none');
        hideLoading();
    }

    function hideAlert() {
        alertEl?.classList.add('d-none');
    }

    function showLoading() {
        skeletonEl?.classList.remove('d-none');
    }

    function hideLoading() {
        skeletonEl?.classList.add('d-none');
    }

    function setListStatus(message) {
        if (!statusEl) return;
        statusEl.textContent = message || '';
    }
})();
