'use strict';

window.personalizationClient = (function () {
    const apiBaseUrl = window.ApiBaseUrl || '';
    const authRefreshSignal = 'auth-refresh-in-progress';

    const getTenantId = () => {
        const user = window.CurrentUser || {};
        return user.tenantId || null;
    };

    const shouldSendTenantHeader = () => {
        const actorType = (window.CurrentUser || {}).actorType || '';
        const isAdminHost = window.location.hostname.toLowerCase().startsWith('admin.');
        const isPlatformRoute = window.location.pathname.toLowerCase().startsWith('/platform/');
        return !isAdminHost && !isPlatformRoute && String(actorType).toLowerCase() === 'tenant_user';
    };

    const getHeaders = (includeJsonContentType) => {
        const headers = {};
        const tenantId = getTenantId();
        if (shouldSendTenantHeader() && tenantId) {
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

    // The gateway's TenantResolutionMiddleware decides whether a personalization
    // request is platform-scoped (TenantId = Guid.Empty) vs tenant-scoped by reading
    // the moduleKey query-string parameter. GET requests already carry it, but write
    // requests only carry it in the JSON body — invisible to the middleware. Without
    // it, a tenant_user's save lands under the JWT tenant while the load reads under
    // the platform scope, so the saved view is never found again. Mirror the scope on
    // the query string for writes so save and load resolve to the same partition.
    const appendScopeQuery = (url, moduleKey, pageKey) => {
        const params = new URLSearchParams();
        if (moduleKey) params.set('moduleKey', moduleKey);
        if (pageKey) params.set('pageKey', pageKey);
        const query = params.toString();
        if (!query) return url;
        return `${url}${url.includes('?') ? '&' : '?'}${query}`;
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
        const url = appendScopeQuery(`${apiBaseUrl}/api/personalization/views`, payload?.moduleKey, payload?.pageKey);
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const updateView = async (id, payload) => {
        const url = appendScopeQuery(`${apiBaseUrl}/api/personalization/views/${id}`, payload?.moduleKey, payload?.pageKey);
        const response = await fetch(url, {
            method: 'PUT',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const deleteView = async (id, moduleKey, pageKey) => {
        const url = appendScopeQuery(`${apiBaseUrl}/api/personalization/views/${id}`, moduleKey, pageKey);
        const response = await fetch(url, {
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
