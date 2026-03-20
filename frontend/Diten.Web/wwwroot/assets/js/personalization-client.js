'use strict';

window.personalizationClient = (function () {
    const apiBaseUrl = window.ApiBaseUrl || '';
    const authRefreshSignal = 'auth-refresh-in-progress';

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }

        return null;
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (error) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getHeaders = (includeJsonContentType) => {
        const token = getCookie('access_token');
        const headers = {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };

        if (includeJsonContentType) {
            headers['Content-Type'] = 'application/json';
        }

        return headers;
    };

    const redirectToLogin = () => {
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.location.href = `/account/login?returnUrl=${returnUrl}`;
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
            headers: getHeaders(false)
        });

        return await handleResponse(response);
    };

    const saveView = async (payload) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views`, {
            method: 'POST',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const updateView = async (id, payload) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views/${id}`, {
            method: 'PUT',
            headers: getHeaders(true),
            body: JSON.stringify(payload)
        });

        return await handleResponse(response);
    };

    const deleteView = async (id) => {
        const response = await fetch(`${apiBaseUrl}/api/personalization/views/${id}`, {
            method: 'DELETE',
            headers: getHeaders(false)
        });

        return await handleResponse(response);
    };

    const setDefaultView = async (id) => {
        return await updateView(id, { isDefault: true });
    };

    return {
        getViews: getViews,
        saveView: saveView,
        updateView: updateView,
        deleteView: deleteView,
        setDefaultView: setDefaultView
    };
})();
