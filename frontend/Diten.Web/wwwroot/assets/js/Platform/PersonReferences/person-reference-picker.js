'use strict';

(function () {
    const apiBase = '/Platform/PersonReferences/api';
    const defaultPageSize = 20;
    const maxPageSize = 100;

    const correlationId = () => {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
        return `person-ref-${Date.now()}-${Math.random().toString(16).slice(2)}`;
    };

    const asString = (value) => value === null || value === undefined ? '' : String(value).trim();
    const asBool = (value) => value === true || value === 'true' || value === 'True';
    const isGuid = (value) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(asString(value));

    const unwrap = (payload) => {
        if (payload?.data?.data !== undefined) return payload.data.data;
        if (payload?.Data?.Data !== undefined) return payload.Data.Data;
        return payload?.data ?? payload?.Data ?? payload;
    };

    const normalizePerson = (person) => {
        if (!person || typeof person !== 'object') return null;
        const personId = asString(person.personId || person.PersonId || person.person_id || person.id || person.Id);
        if (!personId) return null;

        return {
            personId,
            displayName: asString(
                person.displayName ||
                person.DisplayName ||
                person.referenceDisplayName ||
                person.ReferenceDisplayName ||
                person.reference_display_name),
            referenceCode: asString(person.referenceCode || person.ReferenceCode || person.reference_code),
            status: asString(person.status || person.Status || person.referenceableStatus || person.ReferenceableStatus),
            referenceable: asBool(person.referenceable ?? person.Referenceable ?? person.isReferenceable ?? person.IsReferenceable),
            profilePointer: asString(person.profilePointer || person.ProfilePointer || person.profile_pointers)
        };
    };

    const normalizeSearchResult = (payload) => {
        const data = unwrap(payload) || {};
        const items = Array.isArray(data.items) ? data.items : Array.isArray(data.Items) ? data.Items : Array.isArray(data) ? data : [];
        return {
            items: items.map(normalizePerson).filter(Boolean),
            page: Number(data.page || data.Page || 1),
            pageSize: Number(data.pageSize || data.PageSize || defaultPageSize)
        };
    };

    const normalizeValidationResult = (payload) => {
        const data = unwrap(payload) || {};
        const results = Array.isArray(data.results) ? data.results : Array.isArray(data.Results) ? data.Results : [];
        return results.map(normalizePerson).filter(Boolean);
    };

    const makeError = (message, code, status) => {
        const error = new Error(message || code || 'person_reference_error');
        error.code = code || 'person_reference_error';
        error.status = status || 0;
        return error;
    };

    const errorMessage = (payload, fallback) => {
        if (!payload) return fallback;
        if (Array.isArray(payload.errors) && payload.errors.length) return payload.errors.join('; ');
        if (payload.errors && typeof payload.errors === 'object') {
            const messages = Object.values(payload.errors).flat().filter((x) => typeof x === 'string' && x.trim());
            if (messages.length) return messages.join('; ');
        }
        return payload.message || payload.Message || payload.detail || payload.Detail || payload.title || payload.Title || fallback;
    };

    const classifyStatus = (status) => {
        if (status === 401) return 'unauthorized';
        if (status === 403) return 'permission_denied';
        if (status === 404) return 'missing_person';
        if (status === 503 || status === 502 || status === 504) return 'dependency_unavailable';
        return 'request_failed';
    };

    const request = async (path, options) => {
        const init = Object.assign({
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json',
                'X-Correlation-Id': correlationId()
            }
        }, options || {});

        const response = await fetch(`${apiBase}${path}`, init);
        const text = await response.text();
        let payload = null;
        if (text) {
            try {
                payload = JSON.parse(text);
            } catch (_error) {
                payload = { message: text };
            }
        }

        if (!response.ok) {
            throw makeError(errorMessage(payload, response.statusText), classifyStatus(response.status), response.status);
        }

        return payload;
    };

    const buildSearchParams = (options) => {
        const params = new URLSearchParams();
        const query = asString(options?.query);
        const status = asString(options?.status);
        const page = Math.max(1, Number(options?.page || 1));
        const pageSize = Math.min(maxPageSize, Math.max(1, Number(options?.pageSize || defaultPageSize)));

        if (query) params.set('query', query);
        if (status) params.set('status', status);
        if (options?.referenceable === true) params.set('referenceable', 'true');
        params.set('page', String(page));
        params.set('pageSize', String(pageSize));
        return params;
    };

    const validatePersonIds = async (personIds) => {
        const ids = (Array.isArray(personIds) ? personIds : [personIds]).map(asString).filter(Boolean);
        if (!ids.length) throw makeError('Person reference is required.', 'missing_person_id', 400);
        return normalizeValidationResult(await request('/lookup-validation', {
            method: 'POST',
            headers: {
                Accept: 'application/json',
                'Content-Type': 'application/json',
                'X-Correlation-Id': correlationId()
            },
            body: JSON.stringify({ personIds: ids })
        }));
    };

    const api = {
        search: async (options) => {
            const query = asString(options?.query);
            if (isGuid(query)) {
                const items = await validatePersonIds([query]);
                return {
                    items: options?.referenceable === true ? items.filter((item) => item.referenceable) : items,
                    page: Math.max(1, Number(options?.page || 1)),
                    pageSize: Math.min(maxPageSize, Math.max(1, Number(options?.pageSize || defaultPageSize)))
                };
            }

            return normalizeSearchResult(await request(`?${buildSearchParams(options).toString()}`));
        },
        getById: async (personId) => {
            const id = asString(personId);
            if (!id) throw makeError('Person reference is required.', 'missing_person_id', 400);
            return normalizePerson(unwrap(await request(`/${encodeURIComponent(id)}`)));
        },
        validate: validatePersonIds
    };

    const formatPersonText = (person) => {
        const code = person.referenceCode ? ` (${person.referenceCode})` : '';
        return `${person.displayName || person.personId}${code}`;
    };

    const processSelect2Results = (data, params) => {
        const page = params?.page || 1;
        const result = normalizeSearchResult(data);
        const items = Array.isArray(result.items) ? result.items : [];
        return {
            results: items.map((person) => ({
                id: person.personId,
                text: formatPersonText(person),
                person
            })),
            pagination: { more: items.length >= (result.pageSize || defaultPageSize) && page < 500 }
        };
    };

    const createSelect2Transport = (options, params, success, failure) => {
        let aborted = false;
        const term = params?.data?.term || '';
        const page = params?.data?.page || 1;

        api.search({
            query: term,
            status: options?.status,
            referenceable: options?.referenceable !== false,
            page,
            pageSize: options?.pageSize || defaultPageSize
        })
            .then((data) => {
                if (!aborted) success(data);
            })
            .catch((error) => {
                if (!aborted) failure(error);
            });

        return {
            abort: () => {
                aborted = true;
            }
        };
    };

    const setStatus = (element, message, state) => {
        const targetSelector = element.getAttribute('data-person-reference-status-target');
        const target = targetSelector ? document.querySelector(targetSelector) : null;
        if (!target) return;
        target.textContent = message || '';
        target.classList.toggle('text-danger', state === 'error');
        target.classList.toggle('text-muted', state !== 'error');
    };

    const dispatch = (element, name, detail) => {
        element.dispatchEvent(new CustomEvent(name, { bubbles: true, detail }));
    };

    const validateSelection = async (element, personId, labels) => {
        const id = asString(personId);
        if (!id) {
            setStatus(element, '', 'empty');
            dispatch(element, 'personreference:cleared', {});
            return null;
        }

        setStatus(element, labels.loading, 'loading');
        element.setAttribute('aria-busy', 'true');
        try {
            const results = await api.validate([id]);
            const result = results.find((item) => item.personId.toLowerCase() === id.toLowerCase()) || null;
            if (!result) {
                throw makeError(labels.missing, 'missing_person', 404);
            }

            if (!result.referenceable) {
                throw makeError(labels.notReferenceable, 'not_referenceable', 409);
            }

            setStatus(element, '', 'ready');
            dispatch(element, 'personreference:selected', { person: result });
            return result;
        } catch (error) {
            setStatus(element, labels[error.code] || error.message || labels.error, 'error');
            dispatch(element, 'personreference:error', { error });
            throw error;
        } finally {
            element.removeAttribute('aria-busy');
        }
    };

    const initSelect2 = (element, options, labels) => {
        const $ = window.jQuery;
        const $element = $(element);
        if ($element.hasClass('select2-hidden-accessible')) $element.select2('destroy');

        $element.select2({
            width: options.width || '100%',
            allowClear: options.allowClear !== false,
            placeholder: options.placeholder || labels.placeholder,
            minimumInputLength: Number(options.minimumInputLength || 2),
            ajax: {
                delay: Number(options.delay || 250),
                transport: (params, success, failure) => createSelect2Transport(options, params, success, failure),
                processResults: processSelect2Results
            },
            templateResult: (item) => item.loading ? item.text : item.text,
            templateSelection: (item) => item.text || labels.placeholder
        });

        $element.on('select2:select.personReference', async (event) => {
            try {
                await validateSelection(element, event.params?.data?.id, labels);
            } catch (_error) {
                $element.val(null).trigger('change.select2');
            }
        });

        $element.on('select2:clear.personReference', () => {
            setStatus(element, '', 'empty');
            dispatch(element, 'personreference:cleared', {});
        });
    };

    const initNative = (element, labels) => {
        element.addEventListener('change', async () => {
            try {
                await validateSelection(element, element.value, labels);
            } catch (_error) {
                element.value = '';
            }
        });
    };

    const init = (element, options) => {
        if (!element) return null;
        const labels = Object.assign({
            placeholder: 'Search person',
            loading: 'Validating person reference...',
            error: 'Person reference could not be validated.',
            missing: 'Person reference was not found.',
            missing_person: 'Person reference was not found.',
            notReferenceable: 'Person reference is not active.',
            not_referenceable: 'Person reference is not active.',
            permission_denied: 'Permission denied.',
            dependency_unavailable: 'Person reference service is unavailable.',
            unauthorized: 'Authentication is required.'
        }, options?.labels || {});

        element.setAttribute('data-person-reference-picker', 'true');
        if (!element.getAttribute('aria-label')) {
            element.setAttribute('aria-label', labels.placeholder);
        }

        if (window.jQuery?.fn?.select2 && element.tagName === 'SELECT') {
            initSelect2(element, options || {}, labels);
        } else {
            initNative(element, labels);
        }

        return {
            validate: () => validateSelection(element, element.value, labels),
            clear: () => {
                element.value = '';
                setStatus(element, '', 'empty');
                dispatch(element, 'personreference:cleared', {});
            }
        };
    };

    const initAll = (root, options) => {
        const host = root || document;
        return Array.from(host.querySelectorAll('[data-person-reference-picker]')).map((element) => init(element, options || {}));
    };

    window.PersonReferenceApi = api;
    window.PersonReferencePicker = {
        init,
        initAll,
        _test: {
            buildSearchParams,
            classifyStatus,
            createSelect2Transport,
            normalizePerson,
            normalizeSearchResult,
            normalizeValidationResult,
            processSelect2Results
        }
    };
})();
