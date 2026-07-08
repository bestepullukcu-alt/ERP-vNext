/**
 * MOD-0251 Employee Detail Read-Only Page Script
 * Diten ERP vNext - HCM/Employees/{employeeId}
 */
'use strict';

window.HcmEmployeeDetail = (function () {
    let L = window.L10n || {};

    const page = document.getElementById('hcm-employee-detail-page');
    const apiBase = page?.getAttribute('data-api-base') || '/HCM/Employees/api';
    const employeeId = page?.getAttribute('data-employee-id') || '';
    const canView = page?.getAttribute('data-can-view') === 'true';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
        }
    };

    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };
    const field = (name) => page?.querySelector(`[data-field="${name}"]`) || null;
    const setText = (name, value) => {
        const element = field(name);
        if (!element) return;
        element.textContent = normalizeDisplay(value);
    };
    const show = (id, visible) => document.getElementById(id)?.classList.toggle('d-none', !visible);
    const setError = (message) => {
        const error = document.getElementById('hcm-detail-error');
        if (!error) return;
        error.textContent = message || '';
        error.classList.toggle('d-none', !message);
    };

    const normalizeDisplay = (value) => {
        if (value === null || value === undefined || value === '') {
            return L.EmptyValue || '';
        }
        return String(value);
    };

    const formatDate = (value) => value ? new Date(value).toLocaleDateString() : normalizeDisplay('');
    const formatDateTime = (value) => value ? new Date(value).toLocaleString() : normalizeDisplay('');
    const unwrap = (json) => json?.data || json?.Data || json || {};
    const classifyStatus = (status) => {
        if (status === 401 || status === 403) return L.ForbiddenState || 'Permission denied.';
        if (status === 404) return L.NotFoundState || 'Employee not found.';
        if (status >= 500) return L.DependencyError || 'Dependency unavailable.';
        return L.ErrorOccurred || 'Request failed.';
    };

    const buildDisplayName = (detail) => {
        const legal = detail?.legalProfile || detail?.LegalProfile || {};
        return normalizeDisplay(legal.preferredName || legal.PreferredName || [
            legal.legalFirstName || legal.LegalFirstName,
            legal.legalMiddleName || legal.LegalMiddleName,
            legal.legalLastName || legal.LegalLastName
        ].filter(Boolean).join(' '));
    };

    const renderLegalProfile = (detail) => {
        const legal = detail.legalProfile || detail.LegalProfile || {};
        setText('legalFirstName', legal.legalFirstName || legal.LegalFirstName);
        setText('legalMiddleName', legal.legalMiddleName || legal.LegalMiddleName);
        setText('legalLastName', legal.legalLastName || legal.LegalLastName);
        setText('preferredName', legal.preferredName || legal.PreferredName);
        setText('nationalityCode', legal.nationalityCode || legal.NationalityCode);
        setText('workEmail', legal.workEmail || legal.WorkEmail);
        setText(
            'sensitiveFieldsMasked',
            (detail.sensitiveFieldsMasked ?? detail.SensitiveFieldsMasked) === true
                ? L.SensitiveFieldsMasked
                : L.SensitiveFieldsSafeOnly);
        setText(
            'governmentIdentifierPresent',
            (legal.governmentIdentifierPresent ?? legal.GovernmentIdentifierPresent) === true
                ? L.GovernmentIdentifierPresentMasked
                : L.GovernmentIdentifierAbsent);
    };

    const renderEmploymentRecords = (detail) => {
        const body = document.getElementById('hcm-employment-records-body');
        const empty = document.getElementById('hcm-employment-records-empty');
        if (!body) return;

        const records = detail.employmentRecords || detail.EmploymentRecords || [];
        body.innerHTML = '';
        empty?.classList.toggle('d-none', records.length > 0);

        records.forEach((record) => {
            const row = document.createElement('tr');
            const values = [
                record.legalEntityId || record.LegalEntityId,
                record.organizationUnitId || record.OrganizationUnitId,
                record.positionId || record.PositionId,
                record.startDate || record.StartDate,
                record.endDate || record.EndDate,
                record.contractType || record.ContractType,
                record.employmentStatus || record.EmploymentStatus,
                record.approvalStatus || record.ApprovalStatus
            ];

            values.forEach((value, index) => {
                const cell = document.createElement('td');
                cell.textContent = index === 3 || index === 4 ? formatDate(value) : normalizeDisplay(value);
                if (index < 3) cell.classList.add('text-break');
                row.appendChild(cell);
            });

            body.appendChild(row);
        });
    };

    const renderDetail = (detail) => {
        setText('employeeNumber', detail.employeeNumber || detail.EmployeeNumber);
        setText('displayName', buildDisplayName(detail));
        setText('employeeStatus', detail.employeeStatus || detail.EmployeeStatus);
        setText('sensitivityLevel', detail.sensitivityLevel || detail.SensitivityLevel);
        setText('personId', detail.personId || detail.PersonId);
        setText('version', detail.version ?? detail.Version);
        setText('etag', detail.etag || detail.ETag);
        setText('updatedAt', formatDateTime(detail.updatedAt || detail.UpdatedAt));
        renderLegalProfile(detail);
        renderEmploymentRecords(detail);
    };

    const loadDetail = async () => {
        if (!page) return;
        syncL10n();

        if (!canView) {
            show('hcm-detail-loading', false);
            show('hcm-detail-content', false);
            return;
        }

        try {
            const response = await fetch(`${apiBase}/${encodeURIComponent(employeeId)}`, {
                method: 'GET',
                headers: getAuthHeaders()
            });
            const text = await response.text();
            const json = text ? JSON.parse(text) : {};
            if (!response.ok) {
                setError(classifyStatus(response.status));
                show('hcm-detail-loading', false);
                show('hcm-detail-content', false);
                return;
            }

            renderDetail(unwrap(json));
            setError('');
            show('hcm-detail-loading', false);
            show('hcm-detail-content', true);
        } catch (error) {
            console.error('[EmployeeMasterDetail] Detail load failed.', error);
            setError(L.DependencyError || L.ErrorOccurred || '');
            show('hcm-detail-loading', false);
            show('hcm-detail-content', false);
        }
    };

    document.addEventListener('DOMContentLoaded', loadDetail);

    return {
        _test: {
            buildDisplayName,
            classifyStatus,
            normalizeDisplay,
            unwrap
        }
    };
})();
