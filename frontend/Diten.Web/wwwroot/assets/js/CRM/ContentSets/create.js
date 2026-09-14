/**
 * SCMM-14-UI (CAND-CAP-0011) Content Studio — create form. The template + scope references are chosen through
 * searchable select2 pickers backed by the same-origin proxy list endpoints (no raw id entry — D14-e). On submit the
 * server pins the template's ChainVersion + the scope's ScopeVersion and redirects to the workspace.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    if (!$ || !$.fn.select2) return;

    // A searchable, server-backed single picker. `map` turns an API row into { id, text }.
    function ajaxPicker(el, map, allowClear) {
        const url = el.getAttribute('data-url');
        $(el).select2({
            width: '100%',
            placeholder: el.getAttribute('data-placeholder') || '',
            allowClear: !!allowClear,
            minimumInputLength: 0,
            ajax: {
                dataType: 'json',
                delay: 250,
                data: params => ({ term: params.term || '' }),
                processResults: body => {
                    const items = (body && body.data && (body.data.items || body.data)) || [];
                    return { results: (Array.isArray(items) ? items : []).map(map).filter(r => r.id) };
                },
                transport: (params, success, failure) => {
                    // The proxy forwards the query string to the gateway; carry the typed term as `search`.
                    params.url = `${url}?search=${encodeURIComponent(params.data.term || '')}&includeArchived=false`;
                    const request = $.ajax(params);
                    request.then(success);
                    request.fail(failure);
                    return request;
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        const template = document.getElementById('setTemplatePicker');
        const scope = document.getElementById('setScopePicker');
        if (template) {
            ajaxPicker(template, r => ({
                id: r.conceptChainTemplateId,
                text: [r.chainCode, r.chainName].filter(Boolean).join(' — ') + (r.chainVersion ? ` (v${r.chainVersion})` : '')
            }), false);
        }
        if (scope) {
            ajaxPicker(scope, r => ({
                id: r.contentScopeId,
                text: [r.scopeCode, r.scopeName].filter(Boolean).join(' — ') + (r.scopeVersion ? ` (v${r.scopeVersion})` : '')
            }), true);
        }
    });
})(window, document);
