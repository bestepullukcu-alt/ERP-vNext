(function (window, document) {
    'use strict';
    const form = document.getElementById('evaluateForm');
    if (!form) return;
    const L = window.ConsentPreferenceL10n || {};
    const base = '/CRM/ConsentPreferences';
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const setOptions = (id, values, placeholder) => {
        const select = document.getElementById(id);
        if (!select) return;
        select.innerHTML = `<option value="">${esc(placeholder || '')}</option>` + (values || []).map(x => `<option value="${esc(x)}">${esc(x)}</option>`).join('');
    };
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };

    const applyContract = contract => {
        const v = contract?.vocabulary || {};
        setOptions('evalSubjectType', v.subjectTypes, L.SelectOption);
        setOptions('evalChannel', v.channels, L.SelectOption);
        setOptions('evalPurpose', v.purposes, L.SelectOption);
        setOptions('evalScopeType', v.scopeTypes, L.SelectOption);
    };
    if (window.ConsentPreferenceContract) applyContract(window.ConsentPreferenceContract);
    document.addEventListener('consent-preference:contract-ready', e => applyContract(e.detail));

    // BADGE: allowed→success, blocked→danger, unknown/other→secondary. Unknown is NEVER shown as allowed.
    const badgeClass = status => status === 'allowed' ? 'bg-success' : (status === 'blocked' ? 'bg-danger' : 'bg-secondary');
    const label = status => ({ allowed:L.Allowed, blocked:L.Blocked, unknown:L.Unknown, not_applicable:L.NotApplicable }[status] || status);

    const rows = (tbodyId, list, cols) => {
        const tbody = document.getElementById(tbodyId);
        if (!tbody) return;
        tbody.innerHTML = (list || []).map(x => `<tr>${cols.map(c => `<td>${esc(c(x))}</td>`).join('')}</tr>`).join('')
            || `<tr><td colspan="${cols.length}" class="text-muted">${esc(L.EmptyState)}</td></tr>`;
    };

    form.addEventListener('submit', async event => {
        event.preventDefault();
        const subjectType = document.getElementById('evalSubjectType')?.value.trim();
        const subjectId = document.getElementById('evalSubjectId')?.value.trim();
        const channel = document.getElementById('evalChannel')?.value.trim();
        const purpose = document.getElementById('evalPurpose')?.value.trim();
        if (!subjectType || !subjectId || !channel || !purpose) {
            window.showToast?.(L.ValidationRequired || 'Subject, channel and purpose are required.', 'error');
            return;
        }
        const params = new URLSearchParams({ subjectType, subjectId, channel, purpose });
        const scopeType = document.getElementById('evalScopeType')?.value.trim();
        const scopeId = document.getElementById('evalScopeId')?.value.trim();
        const effectiveAt = document.getElementById('evalEffectiveAt')?.value;
        if (scopeType) params.set('scopeType', scopeType);
        if (scopeId) params.set('scopeId', scopeId);
        if (effectiveAt) params.set('effectiveAt', new Date(effectiveAt).toISOString());

        try {
            const result = await envelope(await fetch(`${base}/api/consents/evaluate?${params.toString()}`, { credentials:'same-origin', headers:{ Accept:'application/json' } }));
            const status = result?.eligibilityStatus;
            document.getElementById('evaluateResult')?.classList.remove('d-none');
            const badgeEl = document.getElementById('evalBadge');
            if (badgeEl) { badgeEl.className = `badge fs-6 ${badgeClass(status)}`; badgeEl.textContent = label(status); }
            const decisionEl = document.getElementById('evalDecision');
            if (decisionEl) decisionEl.textContent = result?.decision || '';
            // Unknown is not allowed — surface it explicitly.
            document.getElementById('evalUnknownNote')?.classList.toggle('d-none', status !== 'unknown');

            const reasonHost = document.getElementById('evalReasonCodes');
            if (reasonHost) reasonHost.innerHTML = (result?.reasonCodes || []).map(x => `<span class="badge bg-label-info">${esc(x)}</span>`).join('') || '—';
            const selEl = document.getElementById('evalSelectionReason');
            if (selEl) selEl.textContent = result?.selectionReason || '';
            document.getElementById('evalMatchedConsentId').textContent = result?.matchedConsentId || '—';
            document.getElementById('evalMatchedPreferenceIds').textContent = (result?.matchedPreferenceIds || []).join(', ') || '—';
            document.getElementById('evalEvaluatorVersion').textContent = result?.evaluatorVersion || '—';
            document.getElementById('evalEvaluatedAt').textContent = result?.evaluatedAt ? new Date(result.evaluatedAt).toLocaleString() : '—';

            rows('evalCandidateConsents', result?.candidateConsents, [x => x.consentId, x => x.consentStatus, x => x.selected ? L.Yes : L.No, x => x.reason]);
            rows('evalCandidatePreferences', result?.candidatePreferences, [x => x.preferenceId, x => x.preferenceType, x => x.restrictive ? L.Yes : L.No, x => x.reason]);
        } catch (error) {
            // A backend 400 (malformed question) is shown as an error — NEVER coerced into "allowed".
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    });
})(window, document);
