/**
 * WP-KP-5a-UI — Create / Edit of a Regulatory master-data record (Safety Text / Country Legal Profile; WP-KP-5a).
 *
 *  - Identity (product for a safety text, country, language) is chosen on create and FIXED afterwards (the controls
 *    render disabled on edit and are never posted on an update). Country → its content languages (the knowledge path
 *    identity picker's source, COUNTRY_CODES + country-content-languages); product → the MDM global-product selector.
 *  - A picker whose source cannot be read is DISABLED with the reason (dependency_unavailable /
 *    reference_set_unavailable) — never a free-text box, never an invented option.
 *  - Long texts carry a live character counter (the textarea's maxlength is the contract limit).
 *  - The form posts JSON through the same-origin proxy; CRM decides. Its refusals are shown localised (Err_{code}) under
 *    the field they name, or at the head of the form — never as a raw code.
 */
(function (window, document) {
    'use strict';
    const form = document.getElementById('regulatoryTextForm');
    if (!form) return;

    const cfg = form.dataset;
    const api = cfg.endpoint;
    const isEdit = !!cfg.id;
    const hasProduct = cfg.hasProduct === 'true';
    const L = () => window.L10n || {};
    const field = (name) => form.querySelector(`[data-field="${name}"]`);
    const esc = (v) => String(v ?? '').replace(/[&<>'"]/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const fmt = (tpl, ...args) => String(tpl || '').replace(/\{(\d)\}/g, (_, i) => String(args[Number(i)] ?? ''));

    // Which field a CRM refusal belongs to; anything else goes to the head of the form.
    const ERROR_FIELDS = {
        country_invalid: 'countryCode',
        language_not_in_country: 'languageCode',
        reference_set_unavailable: 'countryCode',
        product_not_found: 'globalProductId',
        dependency_unavailable: 'globalProductId'
    };
    const KNOWN_ERRORS = [
        'safety_text_open_draft_exists', 'legal_profile_open_draft_exists', 'review_template_missing', 'not_editable',
        'country_invalid', 'language_not_in_country', 'reference_set_unavailable', 'product_not_found', 'dependency_unavailable'
    ];

    // ---------------- messages ----------------

    const clearErrors = () => {
        form.querySelectorAll('[data-error-for]').forEach((el) => { el.textContent = ''; });
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        const alert = document.getElementById('regulatoryFormAlert');
        alert.textContent = '';
        alert.classList.add('d-none');
    };
    const fieldError = (name, text) => {
        const slot = form.querySelector(`[data-error-for="${name}"]`);
        if (!slot) return false;
        slot.textContent = text;
        field(name)?.classList.add('is-invalid');
        return true;
    };
    const formError = (text) => {
        const alert = document.getElementById('regulatoryFormAlert');
        alert.textContent = text;
        alert.classList.remove('d-none');
    };
    const note = (name, text) => {
        const el = form.querySelector(`[data-note-for="${name}"]`);
        if (!el) return;
        el.textContent = text || '';
        el.classList.toggle('d-none', !text);
    };

    const showRefusal = (body) => {
        const errors = Array.isArray(body?.errors) ? body.errors.map(String) : [];
        const code = [body?.code, body?.reasonCode, ...errors].find((c) => KNOWN_ERRORS.includes(String(c || '').trim()));
        if (!code) { formError(errors.filter((e) => !/^[a-z_]+$/.test(e)).join(' · ') || L().ErrorOccurred || ''); return; }
        const text = L()['Err_' + code] || L().ErrorOccurred || '';
        if (!(ERROR_FIELDS[code] && fieldError(ERROR_FIELDS[code], text))) formError(text);
    };

    // ---------------- pickers ----------------

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        const body = await res.json().catch(() => ({}));
        if (!res.ok) throw Object.assign(new Error(String(res.status)), { status: res.status, body });
        return body?.data;
    };

    const fill = (select, options, selected) => {
        if (!select) return;
        const list = options.slice();
        // A stored value no longer offered stays visible so an edit never silently drops it.
        if (selected && !list.some((o) => String(o.value) === String(selected))) list.unshift({ value: selected, text: select.dataset.selectedText || selected });
        select.innerHTML = `<option value="">${esc(L().SelectOption || '')}</option>`
            + list.map((o) => `<option value="${esc(o.value)}"${String(o.value) === String(selected) ? ' selected' : ''}>${esc(o.text)}</option>`).join('');
    };

    let countries = [];
    const fillLanguages = () => {
        const lang = field('languageCode');
        const country = countries.find((c) => c.code === field('countryCode')?.value);
        fill(lang, (country?.languages || []).map((l) => ({ value: l.code, text: `${l.code} — ${l.name}` })), lang?.dataset.selected);
        if (lang && !isEdit) lang.disabled = !country;
    };

    const loadCountries = async () => {
        const select = field('countryCode');
        try {
            countries = (await getJson(`${api}/lookups/countries`)) || [];
            fill(select, countries.map((c) => ({ value: c.code, text: `${c.code} — ${c.name}` })), select?.dataset.selected);
            note('countryCode', '');
        } catch {
            countries = [];
            fill(select, [], select?.dataset.selected);
            if (!isEdit) select.disabled = true;
            note('countryCode', L().Err_reference_set_unavailable || '');
        }
        fillLanguages();
    };

    const loadProducts = async () => {
        const select = field('globalProductId');
        if (!select) return;
        if (isEdit) { fill(select, [], select.dataset.selected); return; }   // fixed identity: no catalogue needed
        try {
            // The proxy reads the whole MDM catalogue (paged server-side) or answers 503 dependency_unavailable.
            const items = (await getJson(`${api}/lookups/products`)) || [];
            fill(select, items.map((p) => ({ value: p.id, text: [p.code, p.name].filter(Boolean).join(' — ') })), select.dataset.selected);
            note('globalProductId', '');
        } catch {
            // MDM unreachable or the picker not permitted: fail closed, say why.
            fill(select, [], select.dataset.selected);
            select.disabled = true;
            note('globalProductId', L().Err_dependency_unavailable || L().PickerUnavailable || '');
        }
    };

    // ---------------- counters + the page approval code example ----------------

    const updateCounter = (textarea) => {
        const name = textarea.dataset.field;
        const slot = form.querySelector(`[data-count-for="${name}"]`);
        if (slot) slot.textContent = fmt(L().CharCountTpl || '{0} / {1}', textarea.value.length, textarea.maxLength > 0 ? textarea.maxLength : '');
    };

    /** {CC} → the country, {YYYY} → this year, {SEQ} → 0001 (the KP-UI-3 page approval code, shown as an example). */
    const updateCodeExample = () => {
        const out = form.querySelector('[data-code-format-preview]');
        const input = field('pageApprovalCodeFormat');
        if (!out || !input) return;
        const format = input.value.trim();
        const example = format
            .replace(/\{CC\}/gi, field('countryCode')?.value || 'TR')
            .replace(/\{YYYY\}/gi, String(new Date().getFullYear()))
            .replace(/\{SEQ\}/gi, '0001');
        out.textContent = format ? fmt(out.dataset.template || '{0}', example) : '';
    };

    form.addEventListener('input', (event) => {
        if (event.target.matches('.js-counted')) updateCounter(event.target);
        if (event.target.matches('[data-field="pageApprovalCodeFormat"]')) updateCodeExample();
    });
    form.addEventListener('change', (event) => {
        if (event.target.matches('[data-field="countryCode"]')) { fillLanguages(); updateCodeExample(); }
    });

    // ---------------- submit ----------------

    const valueOf = (name) => {
        const el = field(name);
        if (!el) return undefined;
        const v = el.value.trim();
        return v === '' ? null : v;
    };

    const collect = () => {
        const body = {};
        form.querySelectorAll('[data-field]').forEach((el) => {
            const name = el.dataset.field;
            if (el.classList.contains('js-identity')) return;
            body[name] = valueOf(name);
        });
        if (!isEdit) {
            if (hasProduct) {
                body.globalProductId = valueOf('globalProductId');
                body.globalProductCodeDisplay = field('globalProductId')?.selectedOptions?.[0]?.textContent?.split(' — ')[0] || null;
            }
            body.countryCode = valueOf('countryCode');
            body.languageCode = valueOf('languageCode');
        }
        return body;
    };

    const requiredMissing = (body) => {
        let missing = false;
        form.querySelectorAll('[required]').forEach((el) => {
            if (el.disabled || el.value.trim()) return;
            missing = true;
            fieldError(el.dataset.field, L().FieldRequired || '');
        });
        return missing || !body;
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        clearErrors();
        const body = collect();
        if (requiredMissing(body)) return;
        const save = document.getElementById('btnRegulatorySave');
        if (save) save.disabled = true;
        try {
            const url = isEdit ? `${api}/${cfg.resource}/${encodeURIComponent(cfg.id)}` : `${api}/${cfg.resource}`;
            const res = await fetch(url, {
                method: isEdit ? 'PUT' : 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const json = res.status === 204 ? {} : await res.json().catch(() => ({}));
            if (!res.ok) { showRefusal(json); return; }
            const data = json?.data;
            const newId = isEdit ? cfg.id : (typeof data === 'string' ? data : (data?.id || data?.safetyTextId || data?.countryLegalProfileId));
            window.showToast?.((isEdit ? L().Updated : L().Created) || '', 'success');
            window.location.href = newId ? cfg.detailsUrl + encodeURIComponent(newId) : cfg.detailsUrl.replace(/\/$/, '');
        } catch {
            formError(L().ErrorOccurred || '');
        } finally {
            if (save) save.disabled = false;
        }
    });

    const init = async () => {
        form.querySelectorAll('.js-counted').forEach(updateCounter);
        await Promise.all([loadCountries(), hasProduct ? loadProducts() : Promise.resolve()]);
        updateCodeExample();
    };

    init();
})(window, document);
