'use strict';

(function () {
    const apiBase = '/Platform/ReferenceData/api';

    const correlationId = () => {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
        return `corr-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    };

    const makeHandledError = (message, code, status) => {
        const error = new Error(message);
        error.isHandled = true;
        error.code = code;
        error.status = status;
        return error;
    };

    const responseMessage = (payload, fallback) => {
        if (!payload) return fallback;
        if (Array.isArray(payload.errors) && payload.errors.length) return payload.errors.join('; ');
        // ASP.NET ValidationProblemDetails returns `errors` as an object keyed
        // by field name (e.g. { "Values": ["..."] }); surface those messages
        // instead of swallowing them so the offending field is visible.
        if (payload.errors && typeof payload.errors === 'object') {
            const fieldMsgs = Object.values(payload.errors).flat().filter((m) => typeof m === 'string' && m.trim());
            if (fieldMsgs.length) return fieldMsgs.join('; ');
        }
        if (typeof payload.message === 'string' && payload.message.trim()) return payload.message;
        if (typeof payload.error === 'string' && payload.error.trim()) return payload.error;
        if (typeof payload.data === 'string' && payload.data.trim()) return payload.data;
        if (typeof payload.detail === 'string' && payload.detail.trim()) {
            const title = typeof payload.title === 'string' && payload.title.trim() ? `${payload.title}: ` : '';
            return `${title}${payload.detail}`;
        }
        if (typeof payload.title === 'string' && payload.title.trim()) return payload.title;
        return fallback;
    };

    const handleUnauthorized = () => {
        if (window.DtDefaults?.handleUnauthorized) {
            window.DtDefaults.handleUnauthorized();
            return;
        }
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.location.href = `/Account/Login?returnUrl=${returnUrl}`;
    };

    const handleForbidden = () => {
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.location.href = `/Account/AccessDenied?returnUrl=${returnUrl}`;
    };

    const headers = (contentType, idempotencyKey) => {
        const h = {
            Accept: 'application/json',
            'X-Correlation-Id': correlationId()
        };
        if (contentType) h['Content-Type'] = contentType;
        if (idempotencyKey) h['Idempotency-Key'] = idempotencyKey;
        return h;
    };

    const unwrap = async (response, optional) => {
        const text = await response.text();
        let payload = null;
        if (text) {
            try {
                payload = JSON.parse(text);
            } catch (_error) {
                payload = { message: text };
            }
        }

        if (optional && [404, 501, 503].includes(response.status)) {
            return null;
        }

        if (response.status === 401) {
            handleUnauthorized();
            throw makeHandledError('unauthorized', 'auth_redirect', response.status);
        }

        if (response.status === 403) {
            handleForbidden();
            throw makeHandledError('forbidden', 'access_denied_redirect', response.status);
        }

        if (!response.ok) {
            const msg = responseMessage(payload, response.statusText);
            const error = new Error(String(msg || 'request_failed'));
            error.status = response.status;
            throw error;
        }
        return payload?.data ?? payload;
    };

    const request = async (path, method, body, idempotencyKey, optional) => {
        const init = { method: method || 'GET', headers: headers(body ? 'application/json' : null, idempotencyKey) };
        if (body) init.body = JSON.stringify(body);
        const response = await fetch(`${apiBase}${path}`, init);
        return unwrap(response, optional);
    };

    const requestWithDiagnostics = async (operation, path, method, body, idempotencyKey) => {
        try {
            return await request(path, method, body, idempotencyKey, false);
        } catch (error) {
            const log = error?.status >= 500 || !error?.status ? console.error : console.warn;
            log('BusinessReferenceData API call failed', {
                operation,
                method: method || 'GET',
                path,
                status: error?.status || null,
                message: error?.message || 'request_failed'
            });
            throw error;
        }
    };

    const requestOptional = async (operation, path, method, body) => {
        try {
            return await request(path, method, body, null, true);
        } catch (error) {
            if (error?.isHandled) return null;
            console.warn('BusinessReferenceData optional integration unavailable', {
                operation,
                path,
                status: error?.status || null,
                message: error?.message || 'request_failed'
            });
            return null;
        }
    };

    const permissionsFromDom = () => window.ReferenceDataPermissions || { can: () => true };
    const hasPermission = (permission) => {
        if (!permission) return true;
        return permissionsFromDom().can(permission);
    };

    window.ReferenceDataApi = {
        hasPermission,
        getScopeTypes: () => requestWithDiagnostics('getScopeTypes', '/scope-types', 'GET'),
        getSets: (query) => requestWithDiagnostics('getSets', `/sets${query || ''}`, 'GET'),
        getSet: (setId) => requestWithDiagnostics('getSet', `/sets/${setId}`, 'GET'),
        getSetVersions: (setId) => requestWithDiagnostics('getSetVersions', `/sets/${setId}/versions`, 'GET'),
        createSet: (payload) => requestWithDiagnostics('createSet', '/sets', 'POST', payload),
        patchSet: (setId, payload) => requestWithDiagnostics('patchSet', `/sets/${setId}`, 'PATCH', payload),
        createVersion: (setId, payload) => requestWithDiagnostics('createVersion', `/sets/${setId}/versions`, 'POST', payload || {}),
        getVersion: (versionId) => requestWithDiagnostics('getVersion', `/versions/${versionId}`, 'GET'),
        getVersionValues: (versionId) => requestWithDiagnostics('getVersionValues', `/versions/${versionId}/values`, 'GET'),
        replaceVersionValues: (versionId, payload) => requestWithDiagnostics('replaceVersionValues', `/versions/${versionId}/values`, 'PUT', payload),
        getVersionAttributeDefinitions: (versionId) => requestWithDiagnostics('getVersionAttributeDefinitions', `/versions/${versionId}/attribute-definitions`, 'GET'),
        replaceVersionAttributeDefinitions: (versionId, payload) => requestWithDiagnostics('replaceVersionAttributeDefinitions', `/versions/${versionId}/attribute-definitions`, 'PUT', payload),
        getVersionMappings: (versionId) => requestWithDiagnostics('getVersionMappings', `/versions/${versionId}/mappings`, 'GET'),
        replaceVersionMappings: (versionId, payload) => requestWithDiagnostics('replaceVersionMappings', `/versions/${versionId}/mappings`, 'PUT', payload),
        validateVersion: (versionId) => requestWithDiagnostics('validateVersion', `/versions/${versionId}/validate`, 'POST'),
        submitVersion: (versionId, payload) => requestWithDiagnostics('submitVersion', `/versions/${versionId}/submit`, 'POST', payload || {}),
        approveVersion: (versionId, payload) => requestWithDiagnostics('approveVersion', `/versions/${versionId}/approve`, 'POST', payload || { decision: 'approve' }),
        publishVersion: (versionId, payload, key) => requestWithDiagnostics('publishVersion', `/versions/${versionId}/publish`, 'POST', payload || {}, key),
        publishVersionOverride: (versionId, payload, key) => requestWithDiagnostics('publishVersionOverride', `/versions/${versionId}/publish-override`, 'POST', payload || {}, key),
        getEvidenceLinks: (query) => requestOptional('getEvidenceLinks', `/evidence-links${query || ''}`, 'GET'),
        getEvidenceLink: (evidenceLinkId) => requestOptional('getEvidenceLink', `/evidence-links/${encodeURIComponent(evidenceLinkId)}`, 'GET'),
        evaluateEvidenceRequirements: (payload) => requestOptional('evaluateEvidenceRequirements', '/evidence-requirements:evaluate', 'POST', payload || {}),
        getHierarchy: (setCode, q) => requestWithDiagnostics('getHierarchy', `/sets/${encodeURIComponent(setCode)}/hierarchy${q || ''}`, 'GET'),
        getValues: (setCode, q) => requestWithDiagnostics('getValues', `/sets/${encodeURIComponent(setCode)}/values${q || ''}`, 'GET'),
        registerUsage: (payload) => requestWithDiagnostics('registerUsage', '/usage-registrations', 'POST', payload),
        getUsageRegistrations: (setCode) => requestWithDiagnostics('getUsageRegistrations', `/usage-registrations?set_code=${encodeURIComponent(setCode)}`, 'GET'),
        getUsageFormOptions: (setCode) => requestWithDiagnostics('getUsageFormOptions', `/usage-registrations/form-options?set_code=${encodeURIComponent(setCode)}`, 'GET'),
        deleteUsageRegistration: (usageRegistrationId) => requestWithDiagnostics('deleteUsageRegistration', `/usage-registrations/${usageRegistrationId}`, 'DELETE'),
        bulkDeleteUsageRegistrations: (ids) => requestWithDiagnostics('bulkDeleteUsageRegistrations', '/usage-registrations/bulk', 'DELETE', ids || []),
        previewImport: (payload) => requestWithDiagnostics('previewImport', '/imports/preview', 'POST', payload),
        commitImport: (previewId, key) => requestWithDiagnostics('commitImport', `/imports/${previewId}/commit`, 'POST', null, key),
        searchAuditEvents: () => Promise.resolve(null)
    };
})();
