'use strict';

const TrustLevelsList = (function () {
    let dt;
    let L = window.L10n || {};

    const root = document.getElementById('trust-levels-shell');
    const tableEl = document.querySelector('.datatables-trust-levels');
    const apiUrl = window.API?.tep || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/tep-trust-levels`;
    const policyStateNames = ['Draft', 'Deferred', 'Active', 'Archived'];
    const trustValidationStateNames = ['NotEvaluated', 'Approved', 'Deferred', 'Rejected'];
    const multiSignatureRequirementStateNames = ['NotRequired', 'Required', 'Approved', 'Deferred', 'Rejected'];
    const unavailableBehaviorNames = ['FailClosed', 'DeferredEvaluation'];
    const signatureSubstrateStateNames = ['NotRequired', 'Available', 'Deferred', 'Unavailable'];
    const legalSecurityTrustModelStateNames = ['Draft', 'LocalMetadata', 'Deferred', 'Approved', 'Rejected'];
    const activationStateNames = ['Draft', 'Ready', 'Active', 'Deferred', 'Archived'];
    const auditEvidenceStateNames = ['LocalMetadata', 'Deferred'];
    const retentionStateNames = ['LocalMetadata', 'Deferred'];
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

    const formatDependencyStates = (value) => {
        if (!value) return L.NotAvailable || 'N/A';
        if (Array.isArray(value)) {
            return value.map((item) => {
                const key = normalizeString(item?.dependencyKey ?? item?.DependencyKey ?? item?.name ?? item?.Name);
                const state = normalizeState(item?.state ?? item?.State, dependencyStateNames);
                const reason = normalizeString(item?.reason ?? item?.Reason);
                return [key, state, reason].filter(Boolean).join(': ');
            }).filter(Boolean).join(', ') || (L.NotAvailable || 'N/A');
        }
        if (typeof value === 'object') {
            return Object.entries(value).map(([key, state]) => `${key}: ${normalizeString(state)}`).join(', ') || (L.NotAvailable || 'N/A');
        }
        return normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const badgeClass = (text) => {
        if (text === 'Active' || text === 'Approved' || text === 'Ready' || text === 'Available') return 'bg-label-success';
        if (text === 'Archived' || text === 'NotRequired' || text === 'LocalMetadata') return 'bg-label-secondary';
        if (text === 'Rejected' || text === 'FailClosed' || text === 'Unavailable') return 'bg-label-danger';
        if (text === 'Required' || text === 'NotEvaluated' || text === 'Draft') return 'bg-label-warning';
        if (text === 'Deferred' || text === 'DeferredEvaluation') return 'bg-label-info';
        return 'bg-label-primary';
    };

    const renderBadge = (value, names) => {
        const text = normalizeState(value, names);
        return `<span class="badge ${badgeClass(text)}">${escapeHtml(text)}</span>`;
    };

    const updateRestrictedVisibility = () => {
        const evaluateButton = document.querySelector('[data-trust-level-action="evaluate"]');
        const manageButton = document.querySelector('[data-trust-level-action="manage"]');
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
            const substrate = normalizeState(item?.signatureSubstrateState ?? item?.SignatureSubstrateState, signatureSubstrateStateNames);
            const substrateRef = normalizeString(item?.signatureSubstrateReference ?? item?.SignatureSubstrateReference);
            const audit = normalizeState(item?.auditEvidenceState ?? item?.AuditEvidenceState, auditEvidenceStateNames);
            const retention = normalizeState(item?.retentionState ?? item?.RetentionState, retentionStateNames);
            const dependencies = formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates);
            const deferredReason = normalizeString(item?.deferredReason ?? item?.DeferredReason);
            return [substrate, substrateRef, audit, retention, dependencies, deferredReason].filter(Boolean).join(' | ');
        } catch (error) {
            console.error('[TrustLevels] Audit metadata load failed.', error);
            return L.NotAvailable || 'N/A';
        }
    };

    const setDetails = async (item) => {
        const empty = document.getElementById('trustLevelDetailsEmpty');
        const list = document.getElementById('trustLevelDetailsList');
        if (!empty || !list) return;

        const id = item?.id ?? item?.Id;
        const auditMetadata = id ? await loadAuditMetadata(id) : L.NotAvailable || 'N/A';
        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            trustLevelPolicyState: normalizeState(item?.trustLevelPolicyState ?? item?.TrustLevelPolicyState, policyStateNames),
            trustValidationState: normalizeState(item?.trustValidationState ?? item?.TrustValidationState, trustValidationStateNames),
            multiSignatureRequirementState: normalizeState(item?.multiSignatureRequirementState ?? item?.MultiSignatureRequirementState, multiSignatureRequirementStateNames),
            multiSignaturePolicyUnavailableBehavior: normalizeState(item?.multiSignaturePolicyUnavailableBehavior ?? item?.MultiSignaturePolicyUnavailableBehavior, unavailableBehaviorNames),
            associationMembershipRegistryId: item?.associationMembershipRegistryId ?? item?.AssociationMembershipRegistryId,
            consentVisibilityPolicyId: item?.consentVisibilityPolicyId ?? item?.ConsentVisibilityPolicyId,
            verifiedParticipantAccessId: item?.verifiedParticipantAccessId ?? item?.VerifiedParticipantAccessId,
            reviewBoardCaseId: item?.reviewBoardCaseId ?? item?.ReviewBoardCaseId,
            associationValidationState: normalizeState(item?.associationValidationState ?? item?.AssociationValidationState, dependencyStateNames),
            consentVisibilityValidationState: normalizeState(item?.consentVisibilityValidationState ?? item?.ConsentVisibilityValidationState, dependencyStateNames),
            verifiedAccessValidationState: normalizeState(item?.verifiedAccessValidationState ?? item?.VerifiedAccessValidationState, dependencyStateNames),
            reviewBoardValidationState: normalizeState(item?.reviewBoardValidationState ?? item?.ReviewBoardValidationState, dependencyStateNames),
            signatureSubstrateState: normalizeState(item?.signatureSubstrateState ?? item?.SignatureSubstrateState, signatureSubstrateStateNames),
            signatureSubstrateReference: item?.signatureSubstrateReference ?? item?.SignatureSubstrateReference,
            legalSecurityTrustModelState: normalizeState(item?.legalSecurityTrustModelState ?? item?.LegalSecurityTrustModelState, legalSecurityTrustModelStateNames),
            auditEvidenceState: normalizeState(item?.auditEvidenceState ?? item?.AuditEvidenceState, auditEvidenceStateNames),
            retentionState: normalizeState(item?.retentionState ?? item?.RetentionState, retentionStateNames),
            dependencyStates: formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt),
            trustPolicyVersion: item?.trustPolicyVersion ?? item?.TrustPolicyVersion,
            deferredReason: item?.deferredReason ?? item?.DeferredReason,
            auditMetadata
        };

        list.querySelectorAll('[data-field]').forEach((node) => {
            const key = node.getAttribute('data-field');
            const value = values[key];
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
            const panel = document.getElementById('offcanvasTrustLevelDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[TrustLevels] Detail load failed.', error);
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
                    callback({ data: unwrapResponseData(await response.json()) });
                } catch (error) {
                    console.error('[TrustLevels] List load failed.', error);
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
                { data: 'trustLevelPolicyState', render: (data, _type, item) => renderBadge(data ?? item?.TrustLevelPolicyState, policyStateNames) },
                { data: 'trustValidationState', render: (data, _type, item) => renderBadge(data ?? item?.TrustValidationState, trustValidationStateNames) },
                { data: 'multiSignatureRequirementState', render: (data, _type, item) => renderBadge(data ?? item?.MultiSignatureRequirementState, multiSignatureRequirementStateNames) },
                { data: 'activationState', render: (data, _type, item) => renderBadge(data ?? item?.ActivationState, activationStateNames) },
                { data: 'trustPolicyVersion', render: (data, _type, item) => escapeHtml(data ?? item?.TrustPolicyVersion) },
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

document.addEventListener('DOMContentLoaded', TrustLevelsList.init);
