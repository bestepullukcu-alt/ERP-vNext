/**
 * SCMM-14-UI (CAND-CAP-0011) Content Studio — create form. The template is chosen through a searchable select2 picker
 * backed by the same-origin proxy list endpoint (no raw id entry — D14-e). WP-SB-1R: the retired scope picker is replaced
 * by the set context — a country (BRD COUNTRY_CODES, ICU display name) and one of that country's content languages
 * (native name), both from /CRM/ContentSets/api/countries. CRM validates the pair again on create. On submit the server
 * pins the template's ChainVersion and redirects to the workspace.
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

    // WP-SB-1R — country + language pickers. The language list is the chosen country's content languages; changing the
    // country rebuilds it (a single-language country pre-selects its only language).
    async function contextPickers(countryEl, languageEl) {
        const error = document.getElementById('setCountryError');
        let countries = [];
        try {
            const res = await fetch(countryEl.getAttribute('data-url'), { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (!res.ok) throw new Error(String(res.status));
            countries = ((await res.json()) || {}).data || [];
        } catch (e) {
            error?.classList.remove('d-none');
        }

        const option = (value, text) => { const o = document.createElement('option'); o.value = value; o.textContent = text; return o; };
        countryEl.innerHTML = '';
        countryEl.appendChild(option('', ''));
        countries.forEach(c => countryEl.appendChild(option(c.code, c.name && c.name !== c.code ? `${c.name} (${c.code})` : c.code)));

        const fillLanguages = keep => {
            const country = countries.find(c => c.code === countryEl.value);
            const languages = (country && country.languages) || [];
            const selected = keep || languageEl.value;
            languageEl.innerHTML = '';
            languageEl.appendChild(option('', ''));
            languages.forEach(l => languageEl.appendChild(option(l.code,
                l.nativeName && l.nativeName !== l.name ? `${l.nativeName} — ${l.name}` : (l.name || l.code))));
            languageEl.value = languages.some(l => l.code === selected) ? selected
                : (languages.length === 1 ? languages[0].code : '');
            languageEl.disabled = languages.length === 0;
            $(languageEl).trigger('change.select2');
        };

        $(countryEl).select2({ width: '100%', placeholder: countryEl.getAttribute('data-placeholder') || '' });
        $(languageEl).select2({ width: '100%', placeholder: languageEl.getAttribute('data-placeholder') || '' });
        const preCountry = countryEl.getAttribute('data-selected');
        if (preCountry) { countryEl.value = preCountry; $(countryEl).trigger('change.select2'); }
        fillLanguages(languageEl.getAttribute('data-selected'));
        $(countryEl).on('change', () => fillLanguages(null));
    }

    document.addEventListener('DOMContentLoaded', () => {
        const template = document.getElementById('setTemplatePicker');
        const country = document.getElementById('setCountryPicker');
        const language = document.getElementById('setLanguagePicker');
        if (template) {
            ajaxPicker(template, r => ({
                id: r.conceptChainTemplateId,
                text: [r.chainCode, r.chainName].filter(Boolean).join(' — ') + (r.chainVersion ? ` (v${r.chainVersion})` : '')
            }), false);
        }
        if (country && language) {
            contextPickers(country, language);
        }
    });
})(window, document);
