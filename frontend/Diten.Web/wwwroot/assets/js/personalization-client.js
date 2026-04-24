'use strict';

window.personalizationClient = (function () {
    const apiBaseUrl = window.ApiBaseUrl || '';
    const authRefreshSignal = 'auth-refresh-in-progress';

    const getTenantId = () => {
        const isPlatformContext = window.location.hostname.toLowerCase().startsWith('admin.')
            || window.location.pathname.toLowerCase().startsWith('/platform/');
        if (isPlatformContext) {
            return null;
        }

        const user = window.CurrentUser || {};
        return user.tenantId || '00000000-0000-0000-0000-000000000001';
    };

    const getHeaders = (includeJsonContentType) => {
        const headers = {};
        const tenantId = getTenantId();
        if (tenantId) {
            headers['X-Tenant-Id'] = tenantId;
        }

        if (includeJsonContentType) {
            headers['Content-Type'] = 'application/json';
        }

        return headers;
    };

    const redirectToLogin = () => {
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        const isAdminHost = window.location.hostname.toLowerCase().startsWith('admin.');
        const isPlatformRoute = window.location.pathname.toLowerCase().startsWith('/platform/');
        const loginPath = (isAdminHost || isPlatformRoute) ? '/platform/login' : '/account/login';
        window.location.href = `${loginPath}?returnUrl=${returnUrl}`;
    };

    const handleUnauthorized = () => {
        if (window.DtDefaults?.handleUnauthorized) {
            window.DtDefaults.handleUnauthorized();
            return;
        }

        redirectToLogin();
    };

    const handleResponse = async (response) => {
        if (response.ok) {
            if (response.status === 204) {
                return true;
            }

            return await response.json();
        }

        if (response.status === 401) {
            handleUnauthorized();

            const authError = new Error(authRefreshSignal);
            authError.code = authRefreshSignal;
            authError.authHandled = true;
            throw authError;
        }

        let message = 'ErrorOccurred';
        try {
            const problem = await response.json();
            message = problem.detail || problem.title || message;
        } catch (error) {
            message = response.statusText || message;
        }

        throw new Error(message);
    };

    const buildViewsUrl = (moduleKey, pageKey) => {
        const query = new URLSearchParams({
            moduleKey: moduleKey || '',
            pageKey: pageKey || ''
        });

        return `${apiBaseUrl}/api/personalization/views?${query.toString()}`;
    };

    const getViews = async (moduleKey, pageKey) => {
        const response = await fetch(buildViewsUrl(moduleKey, pageKey), {
            method: 'GET',
            credentials: 'include',
            headers: getHeaders(false)
        });

        return await handleResponse(response);
    };

    const saveView = async (payload) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views`, {
            method: 'POST',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const updateView = async (id, payload) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views/${id}`, {
            method: 'PUT',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const deleteView = async (id) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views/${id}`, {
            method: 'DELETE',
            credentials: 'include',
            headers: getHeaders(false)
        });

        return await handleResponse(response);
    };

    const setDefaultView = async (id) => {
        return await updateView(id, { isDefault: true });
    };

    const getDefaultView = async (moduleKey, pageKey) => {
        const views = await getViews(moduleKey, pageKey);
        return views?.find(v => v.isDefault) || null;
    };

    return {
        getViews: getViews,
        getDefaultView: getDefaultView,
        saveView: saveView,
        updateView: updateView,
        deleteView: deleteView,
        setDefaultView: setDefaultView
    };
})();
