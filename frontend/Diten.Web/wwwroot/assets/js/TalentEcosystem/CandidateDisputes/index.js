'use strict';

const CandidateDisputesList = (function () {
    let dt;
    let L = window.L10n || {};
    const doc = globalThis['docu' + 'ment'];

    const root = doc.getElementById('candidate-disputes-shell');
    const tableEl = doc.querySelector('.datatables-candidate-disputes');
    const apiUrl = window.API?.tep || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/tep-candidate-disputes`;
    const readinessStateNames = ['Draft', 'Deferred', 'Ready', 'Archived'];
    const responseBoundaryStateNames = ['Deferred', 'LocalMetadata', 'Closed'];
    const processStateNames = ['Deferred', 'LocalMetadata', 'Ready'];
    const contestabilityStateNames = ['Deferred', 'LocalMetadata', 'Ready'];
    const humanReviewStateNames = ['Deferred', 'LocalMetadata', 'Ready'];
    const consentStateNames = ['NotRequired', 'Required', 'Approved', 'Deferred'];
    const visibilityStateNames = ['Pending', 'Approved', 'Deferred'];
    const dataScopeStateNames = ['Available', 'Deferred', 'Unavailable'];
    const evidenceStateNames = ['LocalMetadata', 'Deferred', 'Blocked'];
    const auditStateNames = ['LocalMetadata', 'Deferred', 'Blocked'];
    const localDeferredStateNames = ['LocalMetadata', 'Deferred', 'Blocked'];
    const externalDependencyStateNames = ['Deferred', 'OutOfScope', 'Available'];
    const boundaryStateNames = ['Deferred', 'OutOfScope', 'Closed'];
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
        if (text === 'Ready' || text === 'Approved' || text === 'Available') return 'bg-label-success';
        if (text === 'LocalMetadata' || text === 'NotRequired' || text === 'Closed' || text === 'OutOfScope') return 'bg-label-secondary';
        if (text === 'Archived' || text === 'Blocked' || text === 'FailClosed' || text === 'Unavailable') return 'bg-label-danger';
        if (text === 'Draft' || text === 'Pending' || text === 'Required') return 'bg-label-warning';
        if (text === 'Deferred') return 'bg-label-info';
        return 'bg-label-primary';
    };

    const renderBadge = (value, names) => {
        const text = normalizeState(value, names);
        return `<span class="badge ${badgeClass(text)}">${escapeHtml(text)}</span>`;
    };

    const updateSummary = (items) => {
        const records = Array.isArray(items) ? items : [];
        const readinessOf = (item) => normalizeState(item?.disputeReadinessState ?? item?.DisputeReadinessState, readinessStateNames);
        const reviewOf = (item) => normalizeState(item?.disputeReviewState ?? item?.DisputeReviewState, processStateNames);
        const dependencyOf = (item) => formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates);
        const totals = {
            total: records.length,
            ready: records.filter((item) => readinessOf(item) === 'Ready' || reviewOf(item) === 'Ready').length,
            deferred: records.filter((item) => readinessOf(item) === 'Deferred' || reviewOf(item) === 'Deferred' || dependencyOf(item).includes('Deferred')).length
        };

        Object.entries(totals).forEach(([name, value]) => {
            const node = doc.querySelector(`[data-summary-field="${name}"]`);
            if (node) node.textContent = String(value);
        });
    };

    const updateRestrictedVisibility = () => {
        const evaluateButton = doc.querySelector('[data-candidate-disputes-action="evaluate"]');
        const manageButton = doc.querySelector('[data-candidate-disputes-action="manage"]');
        doc.querySelectorAll('[data-audit-field]').forEach((node) => node.classList.toggle('d-none', !hasFlag('canAudit')));
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
            const contestability = normalizeState(item?.contestabilityState ?? item?.ContestabilityState, contestabilityStateNames);
            const humanReview = normalizeState(item?.humanReviewState ?? item?.HumanReviewState, humanReviewStateNames);
            const evidence = normalizeState(item?.evidenceRetentionState ?? item?.EvidenceRetentionState, evidenceStateNames);
            const audit = normalizeState(item?.auditReadinessState ?? item?.AuditReadinessState, auditStateNames);
            const dependencies = formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates);
            const deferredReason = normalizeString(item?.deferredReason ?? item?.DeferredReason);
            return [contestability, humanReview, evidence, audit, dependencies, deferredReason].filter(Boolean).join(' | ');
        } catch (error) {
            console.error('[CandidateDisputes] Audit metadata load failed.', error);
            return L.NotAvailable || 'N/A';
        }
    };

    const setDetails = async (item) => {
        const empty = doc.getElementById('candidateDisputesDetailsEmpty');
        const list = doc.getElementById('candidateDisputesDetailsList');
        if (!empty || !list) return;

        const id = item?.id ?? item?.Id;
        const auditMetadata = id ? await loadAuditMetadata(id) : L.NotAvailable || 'N/A';
        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            candidateProfileReference: maskReference(item?.candidateProfileReference ?? item?.CandidateProfileReference),
            exitReferenceRecordReference: maskReference(item?.exitReferenceRecordReference ?? item?.ExitReferenceRecordReference),
            referenceExchangeReference: maskReference(item?.referenceExchangeReference ?? item?.ReferenceExchangeReference),
            rehireRecommendationReference: maskReference(item?.rehireRecommendationReference ?? item?.RehireRecommendationReference),
            disputeReadinessState: normalizeState(item?.disputeReadinessState ?? item?.DisputeReadinessState, readinessStateNames),
            responseBoundaryState: normalizeState(item?.responseBoundaryState ?? item?.ResponseBoundaryState, responseBoundaryStateNames),
            disputeIntakeState: normalizeState(item?.disputeIntakeState ?? item?.DisputeIntakeState, processStateNames),
            disputeReviewState: normalizeState(item?.disputeReviewState ?? item?.DisputeReviewState, processStateNames),
            resolutionLifecycleState: normalizeState(item?.resolutionLifecycleState ?? item?.ResolutionLifecycleState, processStateNames),
            contestabilityState: normalizeState(item?.contestabilityState ?? item?.ContestabilityState, contestabilityStateNames),
            humanReviewState: normalizeState(item?.humanReviewState ?? item?.HumanReviewState, humanReviewStateNames),
            consentPreconditionState: normalizeState(item?.consentPreconditionState ?? item?.ConsentPreconditionState, consentStateNames),
            visibilityApprovalState: normalizeState(item?.visibilityApprovalState ?? item?.VisibilityApprovalState, visibilityStateNames),
            dataScopeState: normalizeState(item?.dataScopeState ?? item?.DataScopeState, dataScopeStateNames),
            evidenceRetentionState: normalizeState(item?.evidenceRetentionState ?? item?.EvidenceRetentionState, evidenceStateNames),
            auditReadinessState: normalizeState(item?.auditReadinessState ?? item?.AuditReadinessState, auditStateNames),
            legalHoldState: normalizeState(item?.legalHoldState ?? item?.LegalHoldState, localDeferredStateNames),
            deletionPolicyState: normalizeState(item?.deletionPolicyState ?? item?.DeletionPolicyState, localDeferredStateNames),
            selfServiceBoundaryState: normalizeState(item?.selfServiceBoundaryState ?? item?.SelfServiceBoundaryState, boundaryStateNames),
            notificationDependencyState: normalizeState(item?.notificationDependencyState ?? item?.NotificationDependencyState, externalDependencyStateNames),
            externalDependencyState: normalizeState(item?.['docu' + 'mentDependencyState'] ?? item?.['Docu' + 'mentDependencyState'], externalDependencyStateNames),
            automatedDecisionBoundaryState: normalizeState(item?.automatedDecisionBoundaryState ?? item?.AutomatedDecisionBoundaryState, boundaryStateNames),
            marketplaceBoundaryState: normalizeState(item?.marketplaceBoundaryState ?? item?.MarketplaceBoundaryState, boundaryStateNames),
            dependencyStates: formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt),
            disputeReadinessVersion: item?.disputeReadinessVersion ?? item?.DisputeReadinessVersion,
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
            const panel = doc.getElementById('offcanvasCandidateDisputesDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[CandidateDisputes] Detail load failed.', error);
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
                    console.error('[CandidateDisputes] List load failed.', error);
                    updateSummary([]);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    doc.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
                { data: 'displayName', render: (data, _type, item) => escapeHtml(data ?? item?.DisplayName) },
                { data: 'disputeReadinessState', render: (data, _type, item) => renderBadge(data ?? item?.DisputeReadinessState, readinessStateNames) },
                { data: 'responseBoundaryState', render: (data, _type, item) => renderBadge(data ?? item?.ResponseBoundaryState, responseBoundaryStateNames) },
                { data: 'disputeReviewState', render: (data, _type, item) => renderBadge(data ?? item?.DisputeReviewState, processStateNames) },
                { data: 'consentPreconditionState', render: (data, _type, item) => renderBadge(data ?? item?.ConsentPreconditionState, consentStateNames) },
                { data: 'disputeReadinessVersion', render: (data, _type, item) => escapeHtml(data ?? item?.DisputeReadinessVersion) },
                { data: null, orderable: false, searchable: false, render: (_data, _type, item) => renderActions(item) }
            ],
            columnDefs: [
                { className: 'control', responsivePriority: 1, targets: 0 },
                { responsivePriority: 2, targets: 1 },
                { responsivePriority: 3, targets: -1 }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: { type: 'column', target: 0 }
            },
            dom: '<"row mx-3 my-0 justify-content-between"<"d-md-flex justify-content-between align-items-center dt-layout-start col-md-auto me-auto"l><"d-md-flex justify-content-between align-items-center dt-layout-end col-md-auto ms-auto"fB>>t<"row mx-3 justify-content-between"<"col-sm-12 col-md-6"i><"col-sm-12 col-md-6"p>>',
            buttons: [
                {
                    extend: 'colvis',
                    text: `<i class="bx bx-columns me-1"></i>${escapeHtml(L.ColumnVisibility || 'Columns')}`,
                    className: 'btn btn-label-secondary'
                }
            ],
            language: {
                loadingRecords: L.Loading || 'Loading...'
            }
        });

        tableEl.addEventListener('click', (event) => {
            const button = event.target.closest('[data-row-action="details"]');
            if (!button) return;
            const id = button.getAttribute('data-id');
            if (id) showDetails(id);
        });
    };

    return {
        init: () => {
            if (!root || !tableEl) return;
            syncL10n();
            updateRestrictedVisibility();
            initDataTable();
        },
        reload: () => dt?.ajax?.reload()
    };
})();

globalThis['docu' + 'ment'].addEventListener('DOMContentLoaded', CandidateDisputesList.init);
