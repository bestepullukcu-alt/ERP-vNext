'use strict';

// MOD-0023 Batch 08 — Workflow admin API client.
// Every call goes through the MVC proxy (/Platform/Workflow/api/**), which forwards to the
// gateway (/api/v1/workflow/**). The Platform service port is never contacted directly and the
// browser never sends TenantId — the proxy injects X-Tenant-Id from the server-side JWT context.
(function () {
    const apiBase = '/Platform/Workflow/api';

    const newGuid = () => {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = (Math.random() * 16) | 0;
            const v = c === 'x' ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    };

    const correlationId = () => newGuid();

    const makeHandledError = (message, code, status) => {
        const error = new Error(message);
        error.isHandled = true;
        error.code = code;
        error.status = status;
        return error;
    };

    // Extract a debug-friendly message from the Response<T> envelope / ProblemDetails / proxy errors.
    const responseMessage = (payload, fallback) => {
        if (!payload) return fallback;
        if (Array.isArray(payload.errors) && payload.errors.length) return payload.errors.join('; ');
        if (payload.errors && typeof payload.errors === 'object') {
            const fieldMsgs = Object.values(payload.errors).flat().filter((m) => typeof m === 'string' && m.trim());
            if (fieldMsgs.length) return fieldMsgs.join('; ');
        }
        if (typeof payload.message === 'string' && payload.message.trim()) return payload.message;
        if (typeof payload.error === 'string' && payload.error.trim()) return payload.error;
        if (typeof payload.detail === 'string' && payload.detail.trim()) {
            const title = typeof payload.title === 'string' && payload.title.trim() ? `${payload.title}: ` : '';
            return `${title}${payload.detail}`;
        }
        if (typeof payload.title === 'string' && payload.title.trim()) return payload.title;
        return fallback;
    };

    const handleUnauthorized = () => {
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.location.href = `/Account/Login?returnUrl=${returnUrl}`;
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

    // Returns { ok, status, data, payload, message, reasonCode, correlationId }.
    // Never throws for 4xx — workflow screens need the reasonCode/correlationId to drive UX
    // (e.g. idempotent replays, permission-denied, SOD violations). Auth redirects still happen.
    const send = async (path, method, body, idempotencyKey) => {
        const init = { method: method || 'GET', headers: headers(body ? 'application/json' : null, idempotencyKey) };
        if (body !== undefined && body !== null) init.body = JSON.stringify(body);

        let response;
        try {
            response = await fetch(`${apiBase}${path}`, init);
        } catch (networkError) {
            return {
                ok: false,
                status: 0,
                data: null,
                payload: null,
                message: networkError?.message || 'network_error',
                reasonCode: 'NETWORK_ERROR',
                correlationId: null
            };
        }

        if (response.status === 401) {
            handleUnauthorized();
            throw makeHandledError('unauthorized', 'auth_redirect', 401);
        }

        const text = await response.text();
        let payload = null;
        if (text) {
            try { payload = JSON.parse(text); } catch (_e) { payload = { message: text }; }
        }

        return {
            ok: response.ok,
            status: response.status,
            data: payload && Object.prototype.hasOwnProperty.call(payload, 'data') ? payload.data : payload,
            payload,
            message: responseMessage(payload, response.statusText),
            reasonCode: payload?.reasonCode || payload?.ReasonCode || null,
            correlationId: payload?.correlationId || payload?.CorrelationId
                || response.headers.get('X-Correlation-Id') || null
        };
    };

    window.WorkflowApi = {
        newGuid,
        // Definitions
        listDefinitions: () => send('/definitions', 'GET'),
        getDefinition: (id) => send(`/definitions/${id}`, 'GET'),
        createDefinition: (payload) => send('/definitions', 'POST', payload),
        publishDefinition: (id, payload) => send(`/definitions/${id}/publish`, 'POST', payload),
        // Versions
        listVersions: (id) => send(`/definitions/${id}/versions`, 'GET'),
        getVersion: (id, versionId) => send(`/definitions/${id}/versions/${versionId}`, 'GET'),
        // Instances
        listInstances: () => send('/instances', 'GET'),
        getInstance: (id) => send(`/instances/${id}`, 'GET'),
        startInstance: (payload) => send('/instances', 'POST', payload, payload?.idempotencyKey),
        // Tasks
        listTasks: () => send('/tasks', 'GET'),
        approveTask: (taskId, payload) => send(`/tasks/${taskId}/approve`, 'POST', payload, payload?.idempotencyKey),
        rejectTask: (taskId, payload) => send(`/tasks/${taskId}/reject`, 'POST', payload, payload?.idempotencyKey),
        delegateTask: (taskId, payload) => send(`/tasks/${taskId}/delegate`, 'POST', payload, payload?.idempotencyKey),
        requestInfoTask: (taskId, payload) => send(`/tasks/${taskId}/request-info`, 'POST', payload, payload?.idempotencyKey),
        cancelTask: (taskId, payload) => send(`/tasks/${taskId}/cancel`, 'POST', payload, payload?.idempotencyKey),
        // SLA rules
        listSlaRules: (templateId) => send(`/sla-rules${templateId ? `?templateId=${encodeURIComponent(templateId)}` : ''}`, 'GET'),
        createSlaRule: (payload) => send('/sla-rules', 'POST', payload),
        // Escalations
        runEscalations: (payload) => send('/escalations/run', 'POST', payload || {}, payload?.idempotencyKey),
        // Transition gate (read-only evaluate)
        evaluateTransition: (payload) => send('/transitions/evaluate', 'POST', payload)
    };
})();
