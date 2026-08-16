'use strict';

(function () {
    const tableEl = document.querySelector('.datatables-pvg-case-intake-triage');
    if (!tableEl || !window.DataTable) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const alertEl = document.getElementById('pvg-list-alert');
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

    const buildListUrl = () => {
        const params = new URLSearchParams();
        const status = document.getElementById('pvgStatusFilter')?.value || '';
        if (status) params.set('status', status);
        params.set('pageNumber', '1');
        params.set('pageSize', '100');
        return safeProxyUrl(`${endpoint}/list?${params.toString()}`);
    };

    const filterBtn = {
        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + escapeHtml(t('Apply')) + '</span>',
        className: 'btn btn-label-secondary dt-filter-btn',
        attr: { title: t('Apply'), 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
        action: () => {
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
        }
    };

    const config = window.DtDefaults.create({
        ajax: {
            url: buildListUrl(),
            dataSrc: json => {
                if (isControlledFailure(json)) {
                    showAlert(safeMessage(json));
                    return [];
                }

                hideAlert();
                return itemsOf(json);
            },
            error: xhr => {
                showAlert(safeMessage(tryParseJson(xhr?.responseText), xhr?.status));
            },
            headers: getAuthHeaders()
        },
        columns: [
            { data: null, defaultContent: '', className: 'control', orderable: false, searchable: false },
            {
                data: null,
                render: row => {
                    const id = valueOf(row, 'intakeDraftId');
                    return `<span class="fw-medium text-break">${escapeHtml(id)}</span>`;
                }
            },
            {
                data: null,
                render: row => `<span class="badge bg-label-primary">${escapeHtml(valueOf(row, 'status'))}</span>`
            },
            {
                data: null,
                orderable: false,
                searchable: false,
                className: 'text-end',
                render: row => {
                    const id = encodeURIComponent(valueOf(row, 'intakeDraftId'));
                    return `<div class="d-inline-flex gap-1">
                        <a class="btn btn-sm btn-icon btn-label-secondary js-quick-view" href="/Pharmacovigilance/CaseIntakeTriage/Details/${id}" title="${escapeHtml(t('Details'))}" aria-label="${escapeHtml(t('Details'))}">
                            <i class="icon-base bx bx-show"></i>
                        </a>
                        <a class="btn btn-sm btn-icon btn-label-primary" href="/Pharmacovigilance/CaseIntakeTriage/Edit/${id}" title="${escapeHtml(t('Edit'))}" aria-label="${escapeHtml(t('Edit'))}">
                            <i class="icon-base bx bx-edit"></i>
                        </a>
                    </div>`;
                }
            }
        ],
        order: [[1, 'asc']],
        responsive: true,
        processing: true,
        buttons: window.DtDefaults?.toolbarButtons
            ? window.DtDefaults.toolbarButtons({ filterBtn })
            : [filterBtn],
        language: {
            emptyTable: t('NoRecords'),
            loadingRecords: t('Loading'),
            processing: t('Loading')
        },
        initComplete: () => {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    });

    const dt = new DataTable(tableEl, config);

    document.getElementById('pvgApplyFilter')?.addEventListener('click', () => {
        dt.ajax.url(buildListUrl()).load();
    });

    document.getElementById('pvgResetFilter')?.addEventListener('click', () => {
        const status = document.getElementById('pvgStatusFilter');
        if (status) status.value = '';
        dt.ajax.url(buildListUrl()).load();
    });

    tableEl.addEventListener('click', event => {
        const link = event.target.closest('.js-quick-view');
        if (!link) return;
        window.location.assign(link.getAttribute('href'));
    });

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, char => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[char]));
    }

    function isControlledFailure(body) {
        const outcome = body?.outcome || body?.Outcome || '';
        const statusCode = Number(body?.statusCode || body?.StatusCode || 0);
        return ['Blocked', 'Invalid'].includes(outcome) || [401, 403, 409].includes(statusCode);
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
    }

    function hideAlert() {
        alertEl?.classList.add('d-none');
    }
})();
