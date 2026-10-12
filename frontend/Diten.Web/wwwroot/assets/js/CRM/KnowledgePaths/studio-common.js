/**
 * WP-KP-UI-1 — Knowledge Path Studio shared client (list, new path, workspace).
 *  - L10n: the KnowledgePathStudio resx bridge (#kp-studio-l10n) merged into window.L10n (camelCase + PascalCase).
 *  - api: same-origin MVC proxy /CRM/KnowledgePaths/api only (never a Gateway URL or a bearer token).
 *  - errors: the KP-1 / KP-2 / KP-3 CRM codes are shown as user text; a code is never printed raw.
 *  - the legacy-path wizard is legacy-wizard.js (WP-KP-UI-2; it replaced the KP-UI-1 bind modal).
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
        claim_ref_duplicate: () => t('Err_claim_ref_duplicate'),
        // WP-KP-UI-2 — review (KP-2) and release (KP-3) answers.
        review_round_open: () => t('Err_review_round_open'),
        chain_conformance_failed: () => t('Err_chain_conformance_failed'),
        component_not_published: () => t('Err_component_not_published'),
        claim_no_country_version: () => t('Err_claim_no_country_version'),
        approval_template_missing: () => t('Err_approval_template_missing'),
        workflow_unavailable: () => t('Err_workflow_unavailable'),
        comment_required: () => t('Err_comment_required'),
        sod_submitter_cannot_decide: () => t('Err_sod_submitter_cannot_decide'),
        approval_via_workflow_only: () => t('Err_approval_via_workflow_only'),
        revision_not_approved: () => t('Err_revision_not_approved'),
        artifact_missing: () => t('Err_artifact_missing'),
        revision_superseded: () => t('Err_revision_superseded'),
        revision_not_released: () => t('Err_revision_not_released'),
        sod_submitter_cannot_release: () => t('Err_sod_submitter_cannot_release'),
        reason_required: () => t('Err_reason_required'),
        path_in_use: () => t('Err_path_in_use'),
        previous_path_in_use: () => t('Err_previous_path_in_use'),
        artifact_store_unavailable: () => t('Err_artifact_store_unavailable'),
        claim_not_approved: () => t('Err_claim_not_approved'),
        claim_language_mismatch: () => t('Err_claim_language_mismatch'),
        approval_forbidden: () => t('Err_approval_forbidden'),
        no_open_review: () => t('Err_no_open_review'),
        decision_invalid: () => t('Err_decision_invalid'),
        note_text_required: () => t('Err_note_text_required'),
        note_not_found: () => t('Err_note_not_found'),
        // WP-E2E-FIX-2 (E3-B4) — only the submitter withdraws a review round.
        withdraw_not_submitter: () => t('Err_withdraw_not_submitter')
    };

    /** The user text of a failed response body ({ errors: [code, message, …] }). The CRM message is never shown for a
     * known code; an unknown failure shows the generic text (the raw code stays in the tooltip only). */
    const errorText = body => {
        const errors = Array.isArray(body?.errors) ? body.errors : [];
        const known = errors.find(e => typeof e === 'string' && Object.prototype.hasOwnProperty.call(ERRORS, e));
        // [code, message, ...details]: the details (e.g. the journey stage names of path_in_use) are names, not codes.
        if (known) return { text: ERRORS[known](), code: known, details: errors.slice(errors.indexOf(known) + 2).filter(e => typeof e === 'string') };
        return { text: t('Err_generic'), code: errors.find(e => typeof e === 'string') || '', details: [] };
    };

    const request = async (method, path, body) => {
        const response = await fetch(`${endpoint}${path}`, {
            method, credentials: 'same-origin',
            headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        const json = await response.json().catch(() => ({}));
        if (!response.ok) {
            // A coded answer (reference_set_unavailable, workflow_unavailable, …) keeps its own text; a bare 502 / 503 is
            // "service unavailable".
            const coded = errorText(json);
            const failure = (response.status === 502 || response.status === 503) && !Object.prototype.hasOwnProperty.call(ERRORS, coded.code)
                ? { text: t('Err_unavailable'), code: String(response.status), details: [] }
                : coded;
            throw Object.assign(new Error(failure.text), { status: response.status, code: failure.code, details: failure.details });
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

    const storage = {
        get: key => { try { return window.localStorage.getItem(key); } catch (e) { return null; } },
        set: (key, value) => { try { window.localStorage.setItem(key, value); } catch (e) { /* storage blocked */ } }
    };

    window.KpStudio = Object.freeze({ t, esc, api, errorText, showAlert, hideAlert, countries, chains, fillOptions, wireCountryLanguage, storage, ERRORS });
})(window, document);
