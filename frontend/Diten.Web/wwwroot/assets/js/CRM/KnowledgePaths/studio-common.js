/**
 * WP-KP-UI-1 — Knowledge Path Studio shared client (list, new path, workspace).
 *  - L10n: the KnowledgePathStudio resx bridge (#kp-studio-l10n) merged into window.L10n (camelCase + PascalCase).
 *  - api: same-origin MVC proxy /CRM/KnowledgePaths/api only (never a Gateway URL or a bearer token).
 *  - errors: the 13 CRM (KP-1) codes are shown as user text; a code is never printed raw.
 *  - bind-chain modal: chain (bindable, the path's subject) + country + language → POST bind-chain.
 */
(function (window, document) {
    'use strict';

    let values = {};
    const payload = document.getElementById('kp-studio-l10n');
    try { values = JSON.parse(payload?.textContent || '{}'); }
    catch (error) { console.error('[KnowledgePathStudio] Localization payload could not be parsed.', error); }
    const toPascal = k => k.charAt(0).toUpperCase() + k.slice(1);
    const merged = {};
    Object.keys(values).forEach(k => { merged[k] = values[k]; merged[toPascal(k)] = values[k]; });
    window.L10n = Object.assign({}, window.L10n || {}, merged);
    window.KnowledgePathStudioL10n = Object.freeze(values);

    const t = (key, ...args) => {
        let text = values[key];
        if (text === undefined || text === null) return '';
        args.forEach((a, i) => { text = text.split(`{${i}}`).join(a == null ? '' : String(a)); });
        return text;
    };

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const endpoint = '/CRM/KnowledgePaths/api';

    // The CRM error codes the studio can receive (KP-1) → user text. Unknown codes fall back to a generic text.
    const ERRORS = {
        chain_template_invalid: () => t('Err_chain_template_invalid'),
        chain_template_required: () => t('Err_chain_template_required'),
        chain_subject_mismatch: () => t('Err_chain_subject_mismatch'),
        path_identity_locked: () => t('Err_path_identity_locked'),
        country_invalid: () => t('Err_country_invalid'),
        language_not_in_country: () => t('Err_language_not_in_country'),
        reference_set_unavailable: () => t('Err_reference_set_unavailable'),
        chain_slot_invalid: () => t('Err_chain_slot_invalid'),
        chain_slot_full: () => t('Err_chain_slot_full'),
        chain_slot_move_forbidden: () => t('Err_chain_slot_move_forbidden'),
        component_language_mismatch: () => t('Err_component_language_mismatch'),
        claim_product_mismatch: () => t('Err_claim_product_mismatch'),
        claim_ref_duplicate: () => t('Err_claim_ref_duplicate')
    };

    /** The user text of a failed response body ({ errors: [code, message, …] }). The CRM message is never shown for a
     * known code; an unknown failure shows the generic text (the raw code stays in the tooltip only). */
    const errorText = body => {
        const errors = Array.isArray(body?.errors) ? body.errors : [];
        const known = errors.find(e => typeof e === 'string' && Object.prototype.hasOwnProperty.call(ERRORS, e));
        if (known) return { text: ERRORS[known](), code: known };
        return { text: t('Err_generic'), code: errors.find(e => typeof e === 'string') || '' };
    };

    const request = async (method, path, body) => {
        const response = await fetch(`${endpoint}${path}`, {
            method, credentials: 'same-origin',
            headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        const json = await response.json().catch(() => ({}));
        if (!response.ok) {
            const failure = response.status === 502 || response.status === 503
                ? { text: (json?.errors || []).includes('reference_set_unavailable') ? t('Err_reference_set_unavailable') : t('Err_unavailable'), code: String(response.status) }
                : errorText(json);
            throw Object.assign(new Error(failure.text), { status: response.status, code: failure.code });
        }
        return json;
    };
    const api = {
        get: path => request('GET', path),
        post: (path, body) => request('POST', path, body ?? {}),
        put: (path, body) => request('PUT', path, body ?? {})
    };

    const showAlert = (el, error) => {
        if (!el) return;
        el.textContent = error?.message || t('Err_generic');
        el.title = error?.code || '';
        el.classList.remove('d-none');
    };
    const hideAlert = el => { if (el) { el.textContent = ''; el.title = ''; el.classList.add('d-none'); } };

    let countriesPromise = null;
    const countries = () => {
        countriesPromise = countriesPromise || api.get('/lookups/countries').then(r => r.data || []).catch(e => { countriesPromise = null; throw e; });
        return countriesPromise;
    };
    const chains = subjectId => api.get(`/lookups/chain-templates${subjectId ? `?subjectId=${encodeURIComponent(subjectId)}` : ''}`).then(r => r.data || []);

    const fillOptions = (select, options, placeholder) => {
        select.innerHTML = `<option value="">${esc(placeholder ?? t('SelectPlaceholder'))}</option>`
            + options.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };

    /** Country select → language select (the country's content languages, native name). */
    const wireCountryLanguage = (countrySelect, languageSelect, list, onChange) => {
        fillOptions(countrySelect, list.map(c => ({ value: c.code, text: c.name })));
        const sync = () => {
            const country = list.find(c => c.code === countrySelect.value);
            const languages = country?.languageDetails || [];
            fillOptions(languageSelect, languages.map(l => ({ value: l.code, text: l.nativeName })));
            languageSelect.disabled = !country || languages.length === 0;
            if (languages.length === 1) languageSelect.value = languages[0].code;
            onChange?.();
        };
        countrySelect.addEventListener('change', sync);
        languageSelect.addEventListener('change', () => onChange?.());
        sync();
    };

    /** The simple bind-chain modal (the legacy-path wizard is KP-UI-2). */
    const openBindModal = async (path, onDone) => {
        const modalEl = document.getElementById('kpBindModal');
        if (!modalEl || !window.bootstrap) return;
        const chainSelect = document.getElementById('kpBindChain');
        const countrySelect = document.getElementById('kpBindCountry');
        const languageSelect = document.getElementById('kpBindLanguage');
        const submit = document.getElementById('kpBindSubmit');
        const alert = document.getElementById('kpBindAlert');
        const noChain = document.getElementById('kpBindNoChain');
        document.getElementById('kpBindPathName').textContent = path.name || '';
        hideAlert(alert);
        const modal = window.bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();

        const ready = () => { submit.disabled = !(chainSelect.value && countrySelect.value && languageSelect.value); };
        try {
            const [chainList, countryList] = await Promise.all([chains(path.subjectId), countries()]);
            fillOptions(chainSelect, chainList.map(c => ({ value: c.id, text: `${c.name} · ${t('VersionShort', c.version)}` })));
            noChain.classList.toggle('d-none', chainList.length > 0);
            chainSelect.onchange = ready;
            const freshCountry = countrySelect.cloneNode(false); countrySelect.replaceWith(freshCountry);
            const freshLanguage = languageSelect.cloneNode(false); languageSelect.replaceWith(freshLanguage);
            wireCountryLanguage(freshCountry, freshLanguage, countryList, () => {
                submit.disabled = !(chainSelect.value && freshCountry.value && freshLanguage.value);
            });
            submit.onclick = async () => {
                submit.disabled = true;
                hideAlert(alert);
                try {
                    await api.post(`/paths/${path.pathId}/bind-chain`, {
                        chainTemplateId: chainSelect.value, countryCode: freshCountry.value, languageCode: freshLanguage.value
                    });
                    modal.hide();
                    window.showToast?.(t('BindDone'), 'success');
                    onDone?.();
                } catch (error) {
                    showAlert(alert, error);
                    submit.disabled = false;
                }
            };
        } catch (error) {
            showAlert(alert, error);
        }
    };

    const storage = {
        get: key => { try { return window.localStorage.getItem(key); } catch (e) { return null; } },
        set: (key, value) => { try { window.localStorage.setItem(key, value); } catch (e) { /* storage blocked */ } }
    };

    window.KpStudio = Object.freeze({ t, esc, api, errorText, showAlert, hideAlert, countries, chains, fillOptions, wireCountryLanguage, openBindModal, storage, ERRORS });
})(window, document);
