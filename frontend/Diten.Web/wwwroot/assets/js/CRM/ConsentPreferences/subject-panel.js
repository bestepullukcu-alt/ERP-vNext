(function (window, document) {
    'use strict';
    const panel = document.getElementById('consentPreferenceSubjectPanel');
    if (!panel) return;
    const L = window.ConsentPreferenceL10n || {};
    const base = '/CRM/ConsentPreferences';
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const date = value => value ? new Date(value).toLocaleString() : '—';
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };
    const setOptions = (id, values) => {
        const select = document.getElementById(id);
        if (!select) return;
        select.innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` + (values || []).map(x => `<option value="${esc(x)}">${esc(x)}</option>`).join('');
    };
    const applyContract = contract => setOptions('subjectPanelType', contract?.vocabulary?.subjectTypes);
    if (window.ConsentPreferenceContract) applyContract(window.ConsentPreferenceContract);
    document.addEventListener('consent-preference:contract-ready', e => applyContract(e.detail));

    const rows = (tbodyId, list, cols) => {
        const tbody = document.getElementById(tbodyId);
        if (!tbody) return;
        tbody.innerHTML = (list || []).map(x => `<tr>${cols.map(c => `<td>${esc(c(x))}</td>`).join('')}</tr>`).join('')
            || `<tr><td colspan="${cols.length}" class="text-muted">${esc(L.EmptyState)}</td></tr>`;
    };

    document.getElementById('subjectPanelForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const subjectType = document.getElementById('subjectPanelType')?.value.trim();
        const subjectId = document.getElementById('subjectPanelId')?.value.trim();
        if (!subjectType || !subjectId) { window.showToast?.(L.ValidationRequired, 'error'); return; }
        panel.dataset.subjectType = subjectType;
        panel.dataset.subjectId = subjectId;
        const qs = new URLSearchParams({ subjectType, subjectId, includeArchived: 'true' }).toString();
        try {
            const [consents, preferences] = await Promise.all([
                envelope(await fetch(`${base}/api/consents?${qs}`, { credentials:'same-origin', headers:{ Accept:'application/json' } })),
                envelope(await fetch(`${base}/api/preferences?${qs}`, { credentials:'same-origin', headers:{ Accept:'application/json' } }))
            ]);
            document.getElementById('subjectPanelResult')?.classList.remove('d-none');
            rows('subjectConsents', consents?.items, [x => x.channel, x => x.purpose, x => x.consentStatus, x => date(x.effectiveFrom), x => x.isArchived ? L.Yes : L.No]);
            rows('subjectPreferences', preferences?.items, [x => x.channel, x => x.preferenceType, x => x.preferenceValue, x => x.priority, x => x.isArchived ? L.Yes : L.No]);
            // Prefill create links with the subject (query hint only; no mutation of Contact/AccountContactLink).
            const link = document.getElementById('subjectCreateConsent');
            if (link) link.href = `${base}/Consents/Create`;
            const plink = document.getElementById('subjectCreatePreference');
            if (plink) plink.href = `${base}/Preferences/Create`;
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    });
})(window, document);
