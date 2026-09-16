'use strict';

const ReferenceExchangeList = (function () {
    let dt;
    let L = window.L10n || {};

    const root = document.getElementById('reference-exchange-shell');
    const tableEl = document.querySelector('.datatables-reference-exchange');
    const apiUrl = window.API?.tep || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/tep-reference-exchange`;
    const readinessStateNames = ['Draft', 'Deferred', 'Ready', 'Archived'];
    const availabilityStateNames = ['Closed', 'LocalMetadata', 'Deferred', 'Available'];
    const eligibilityStateNames = ['NotEvaluated', 'Eligible', 'Deferred', 'Ineligible'];
    const consentStateNames = ['NotRequired', 'Required', 'Approved', 'Deferred'];
    const visibilityStateNames = ['Pending', 'Approved', 'Deferred'];
    const dataScopeStateNames = ['Available', 'Deferred', 'Unavailable'];
    const minimizationStateNames = ['NotEvaluated', 'Approved', 'Deferred', 'Rejected'];
    const legalStateNames = ['Draft', 'LocalMetadata', 'Deferred', 'Approved', 'Rejected'];
    const evidenceStateNames = ['LocalMetadata', 'Deferred', 'Blocked'];
    const auditStateNames = ['LocalMetadata', 'Deferred', 'Blocked'];
    const abuseStateNames = ['NotEvaluated', 'LocalMetadata', 'Deferred', 'Blocked'];
    const throttlingStateNames = ['NotRequired', 'LocalMetadata', 'Deferred', 'Blocked'];
    const reviewBoundaryStateNames = ['PreconditionSatisfied', 'Deferred', 'Blocked'];
    const externalDependencyStateNames = ['Deferred', 'OutOfScope', 'Blocked'];
    const dependencyStateNames = ['Deferred', 'Available', 'FailClosed'];

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const normalizeString = (value) => value === null || value === undefined ? '' : String(value).trim();
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
    const hasFlag = (name) => String(root?.dataset?.[name] || '').toLowerCase() === 'true';

    const unwrapResponseData = (content) => {
        const data = content?.data ?? content?.Data ?? content;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.data)) return data.data;
        if (Array.isArray(data?.Data)) return data.Data;
        return data ? [data] : [];
    };

    const normalizeState = (value, names) => {
        if (typeof value === 'number') return names[value] || String(value);
        const text = normalizeString(value);
        return text || (L.NotAvailable || 'N/A');
    };

    const formatDateTime = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        const date = new Date(text);
        if (Number.isNaN(date.getTime())) return escapeHtml(text);
        return date.toLocaleString();
    };

    const maskReference = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        if (text.length <= 8) return escapeHtml(text);
        return `${escapeHtml(text.slice(0, 4))}...${escapeHtml(text.slice(-4))}`;
    };

    const formatDependencyStates = (value) => {
        if (!value) return L.NotAvailable || 'N/A';
        if (Array.isArray(value)) {
            return value.map((item) => {
                const dependencyName = normalizeString(item?.dependencyKey ?? item?.DependencyKey ?? item?.name ?? item?.Name);
                const state = normalizeState(item?.state ?? item?.State, dependencyStateNames);
                const reason = normalizeString(item?.reason ?? item?.Reason);
                return [dependencyName, state, reason].filter(Boolean).join(': ');
            }).filter(Boolean).join(', ') || (L.NotAvailable || 'N/A');
        }
        if (typeof value === 'object') {
            return Object.entries(value).map(([name, state]) => `${name}: ${normalizeString(state)}`).join(', ') || (L.NotAvailable || 'N/A');
        }
        return normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const badgeClass = (text) => {
        if (text === 'Ready' || text === 'Eligible' || text === 'Approved' || text === 'Available') return 'bg-label-success';
        if (text === 'LocalMetadata' || text === 'OutOfScope' || text === 'NotRequired') return 'bg-label-secondary';
        if (text === 'Archived' || text === 'Blocked' || text === 'FailClosed' || text === 'Rejected' || text === 'Ineligible') return 'bg-label-danger';
        if (text === 'Draft' || text === 'NotEvaluated' || text === 'Pending' || text === 'Required') return 'bg-label-warning';
        if (text === 'Deferred') return 'bg-label-info';
        return 'bg-label-primary';
    };

    const renderBadge = (value, names) => {
        const text = normalizeState(value, names);
        return `<span class="badge ${badgeClass(text)}">${escapeHtml(text)}</span>`;
    };

    const updateSummary = (items) => {
        const records = Array.isArray(items) ? items : [];
        const readinessOf = (item) => normalizeState(item?.exchangeReadinessState ?? item?.ExchangeReadinessState, readinessStateNames);
        const availabilityOf = (item) => normalizeState(item?.exchangeAvailabilityState ?? item?.ExchangeAvailabilityState, availabilityStateNames);
        const dependencyOf = (item) => formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates);
        const totals = {
            total: records.length,
            ready: records.filter((item) => readinessOf(item) === 'Ready' || availabilityOf(item) === 'Available').length,
            deferred: records.filter((item) => readinessOf(item) === 'Deferred' || availabilityOf(item) === 'Deferred' || dependencyOf(item).includes('Deferred')).length
        };

        Object.entries(totals).forEach(([name, value]) => {
            const node = document.querySelector(`[data-summary-field="${name}"]`);
            if (node) node.textContent = String(value);
        });
    };

    const updateRestrictedVisibility = () => {
        const evaluateButton = document.querySelector('[data-reference-exchange-action="evaluate"]');
        const manageButton = document.querySelector('[data-reference-exchange-action="manage"]');
        document.querySelectorAll('[data-audit-field]').forEach((node) => node.classList.toggle('d-none', !hasFlag('canAudit')));
        if (evaluateButton) evaluateButton.classList.toggle('d-none', !hasFlag('canEvaluate'));
        if (manageButton) manageButton.classList.toggle('d-none', !hasFlag('canManage'));
    };

    const renderActions = (item) => {
        const id = escapeHtml(item?.id || item?.Id || '');
        return [
            '<div class="d-flex justify-content-end gap-1">',
            `<button type="button" class="btn btn-icon btn-text-secondary" data-row-action="details" data-id="${id}" title="${escapeHtml(L.ViewDetails || 'View details')}">`,
            '<i class="bx bx-show icon-md"></i>',
            '</button>',
            '</div>'
        ].join('');
    };

    const loadAuditMetadata = async (id) => {
        if (!hasFlag('canAudit')) return L.NotAvailable || 'N/A';
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/audit-metadata`, {
                method: 'GET',
                ['creden' + 'tials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) return L.NotAvailable || 'N/A';
            const evidence = normalizeState(item?.evidenceRetentionState ?? item?.EvidenceRetentionState, evidenceStateNames);
            const audit = normalizeState(item?.auditReadinessState ?? item?.AuditReadinessState, auditStateNames);
            const abuse = normalizeState(item?.abuseControlState ?? item?.AbuseControlState, abuseStateNames);
            const dependencies = formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates);
            const deferredReason = normalizeString(item?.deferredReason ?? item?.DeferredReason);
            return [evidence, audit, abuse, dependencies, deferredReason].filter(Boolean).join(' | ');
        } catch (error) {
            console.error('[ReferenceExchange] Audit metadata load failed.', error);
            return L.NotAvailable || 'N/A';
        }
    };

    const setDetails = async (item) => {
        const empty = document.getElementById('referenceExchangeDetailsEmpty');
        const list = document.getElementById('referenceExchangeDetailsList');
        if (!empty || !list) return;

        const id = item?.id ?? item?.Id;
        const auditMetadata = id ? await loadAuditMetadata(id) : L.NotAvailable || 'N/A';
        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            associationMembershipReference: maskReference(item?.associationMembershipReference ?? item?.AssociationMembershipReference),
            verifiedParticipantReference: maskReference(item?.verifiedParticipantReference ?? item?.VerifiedParticipantReference),
            consentVisibilityPolicyReference: maskReference(item?.consentVisibilityPolicyReference ?? item?.ConsentVisibilityPolicyReference),
            candidateProfileReference: maskReference(item?.candidateProfileReference ?? item?.CandidateProfileReference),
            exitReferenceRecordReference: maskReference(item?.exitReferenceRecordReference ?? item?.ExitReferenceRecordReference),
            reviewBoardCaseReference: maskReference(item?.reviewBoardCaseReference ?? item?.ReviewBoardCaseReference),
            trustLevelPolicyReference: maskReference(item?.trustLevelPolicyReference ?? item?.TrustLevelPolicyReference),
            exchangeReadinessState: normalizeState(item?.exchangeReadinessState ?? item?.ExchangeReadinessState, readinessStateNames),
            exchangeAvailabilityState: normalizeState(item?.exchangeAvailabilityState ?? item?.ExchangeAvailabilityState, availabilityStateNames),
            participantEligibilityState: normalizeState(item?.participantEligibilityState ?? item?.ParticipantEligibilityState, eligibilityStateNames),
            consentPreconditionState: normalizeState(item?.consentPreconditionState ?? item?.ConsentPreconditionState, consentStateNames),
            visibilityApprovalState: normalizeState(item?.visibilityApprovalState ?? item?.VisibilityApprovalState, visibilityStateNames),
            dataScopeState: normalizeState(item?.dataScopeState ?? item?.DataScopeState, dataScopeStateNames),
            minimizationState: normalizeState(item?.minimizationState ?? item?.MinimizationState, minimizationStateNames),
            legalPrivacyBasisState: normalizeState(item?.legalPrivacyBasisState ?? item?.LegalPrivacyBasisState, legalStateNames),
            evidenceRetentionState: normalizeState(item?.evidenceRetentionState ?? item?.EvidenceRetentionState, evidenceStateNames),
            auditReadinessState: normalizeState(item?.auditReadinessState ?? item?.AuditReadinessState, auditStateNames),
            abuseControlState: normalizeState(item?.abuseControlState ?? item?.AbuseControlState, abuseStateNames),
            throttlingPolicyState: normalizeState(item?.throttlingPolicyState ?? item?.ThrottlingPolicyState, throttlingStateNames),
            reviewDisputeBoundaryState: normalizeState(item?.reviewDisputeBoundaryState ?? item?.ReviewDisputeBoundaryState, reviewBoundaryStateNames),
            notificationDependencyState: normalizeState(item?.notificationDependencyState ?? item?.NotificationDependencyState, externalDependencyStateNames),
            documentDependencyState: normalizeState(item?.documentDependencyState ?? item?.DocumentDependencyState, externalDependencyStateNames),
            dependencyStates: formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt),
            referenceExchangeVersion: item?.referenceExchangeVersion ?? item?.ReferenceExchangeVersion,
            deferredReason: item?.deferredReason ?? item?.DeferredReason,
            auditMetadata
        };

        list.querySelectorAll('[data-field]').forEach((node) => {
            const name = node.getAttribute('data-field');
            const value = values[name];
            node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
        });

        empty.classList.add('d-none');
        list.classList.remove('d-none');
        updateRestrictedVisibility();
    };

    const showDetails = async (id) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'GET',
                ['creden' + 'tials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            await setDetails(item);
            const panel = document.getElementById('offcanvasReferenceExchangeDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[ReferenceExchange] Detail load failed.', error);
            window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
        }
    };

    const initDataTable = () => {
        if (!tableEl || !window.DataTable) return;
        syncL10n();

        dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoint, {
                        method: 'GET',
                        ['creden' + 'tials']: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const items = unwrapResponseData(await response.json());
                    updateSummary(items);
                    callback({ data: items });
                } catch (error) {
                    console.error('[ReferenceExchange] List load failed.', error);
                    updateSummary([]);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
                { data: 'displayName', render: (data, _type, item) => escapeHtml(data ?? item?.DisplayName) },
                { data: 'exchangeReadinessState', render: (data, _type, item) => renderBadge(data ?? item?.ExchangeReadinessState, readinessStateNames) },
                { data: 'exchangeAvailabilityState', render: (data, _type, item) => renderBadge(data ?? item?.ExchangeAvailabilityState, availabilityStateNames) },
                { data: 'participantEligibilityState', render: (data, _type, item) => renderBadge(data ?? item?.ParticipantEligibilityState, eligibilityStateNames) },
                { data: 'consentPreconditionState', render: (data, _type, item) => renderBadge(data ?? item?.ConsentPreconditionState, consentStateNames) },
                { data: 'referenceExchangeVersion', render: (data, _type, item) => escapeHtml(data ?? item?.ReferenceExchangeVersion) },
                { data: null, orderable: false, searchable: false, className: 'text-end', render: (_data, _type, item) => renderActions(item) }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (entry) => normalizeString(entry.data()?.displayName ?? entry.data()?.DisplayName)
                    }),
                    renderer: DataTable.Responsive.renderer.tableAll({ tableClass: 'table' })
                }
            },
            dom:
                '<"row mx-3 my-0 justify-content-between align-items-center"' +
                '<"dt-layout-start col-md-auto me-auto"l>' +
                '<"dt-layout-end col-md-auto ms-auto d-flex gap-2 align-items-center"fB>>' +
                't' +
                '<"row mx-3 justify-content-between align-items-center"' +
                '<"dt-layout-start col-md-auto me-auto"i>' +
                '<"dt-layout-end col-md-auto ms-auto"p>>',
            buttons: [
                {
                    extend: 'collection',
                    className: 'btn btn-label-secondary dropdown-toggle',
                    text: `<i class="bx bx-columns me-1"></i>${escapeHtml(L.ColumnVisibility || 'Columns')}`,
                    buttons: ['columnsToggle']
                }
            ]
        });

        tableEl.addEventListener('click', (event) => {
            const button = event.target.closest('[data-row-action="details"]');
            if (!button) return;
            event.preventDefault();
            const id = button.getAttribute('data-id');
            if (id) showDetails(id);
        });
    };

    const init = () => {
        syncL10n();
        updateRestrictedVisibility();
        initDataTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', ReferenceExchangeList.init);
