'use strict';

window.personalizationClient = (function () {
    const viewsEndpoint = '/api/personalization/views';
    const authRefreshSignal = 'auth-refresh-in-progress';

    const getHeaders = (includeJsonContentType) => {
        const headers = {};
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

    // The shared rule from shared/http-media-type.js (loaded by both layouts ahead of this file). It replaced a substring
    // test that answered NO to `application/problem+json` — the exact media type the gateway's
    // TenantResolutionMiddleware uses to refuse the personalization routes this client calls.
    const isJsonResponse = (response) =>
        window.DitenHttp.isJsonMediaType(response.headers?.get('content-type'));

    const isLoginResponse = (response) => {
        if (!response?.redirected || !response.url) return false;

        try {
            const path = new URL(response.url, window.location.origin).pathname.toLowerCase();
            return path === '/account/login' || path === '/platform/login';
        } catch (error) {
            return false;
        }
    };

    const createNonJsonError = (response) => {
        const error = new Error(response.statusText || 'Unexpected non-JSON personalization response');
        error.code = 'personalization-non-json-response';
        error.nonJsonResponse = true;
        return error;
    };

    const handleResponse = async (response) => {
        if (response.ok) {
            if (response.status === 204) {
                return true;
            }

            if (!isJsonResponse(response)) {
                if (isLoginResponse(response)) {
                    handleUnauthorized();

                    const authError = new Error(authRefreshSignal);
                    authError.code = authRefreshSignal;
                    authError.authHandled = true;
                    throw authError;
                }

                throw createNonJsonError(response);
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

        return `${viewsEndpoint}?${query.toString()}`;
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

        try {
            return await handleResponse(response);
        } catch (error) {
            // A proxy fallback must not surface as a JSON parse console error or
            // prevent a register from operating without a saved view.
            if (error?.nonJsonResponse) return [];
            throw error;
        }
    };

    const saveView = async (payload) => {
        const url = appendScopeQuery(viewsEndpoint, payload?.moduleKey, payload?.pageKey);
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const updateView = async (id, payload) => {
        const url = appendScopeQuery(`${viewsEndpoint}/${encodeURIComponent(id)}`, payload?.moduleKey, payload?.pageKey);
        const response = await fetch(url, {
            method: 'PUT',
            credentials: 'include',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const deleteView = async (id, moduleKey, pageKey) => {
        const url = appendScopeQuery(`${viewsEndpoint}/${encodeURIComponent(id)}`, moduleKey, pageKey);
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
