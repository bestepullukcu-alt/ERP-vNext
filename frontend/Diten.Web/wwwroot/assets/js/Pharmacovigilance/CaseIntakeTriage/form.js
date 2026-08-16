'use strict';

(function () {
    const form = document.getElementById('pvg-case-intake-form');
    if (!form) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const alertEl = document.getElementById('pvg-form-alert');
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';

    form.addEventListener('submit', async event => {
        event.preventDefault();
        event.stopPropagation();
        form.classList.add('was-validated');
        if (!form.checkValidity()) {
            showAlert(t('ReasonCode'));
            return;
        }

        const mode = form.dataset.mode || 'create';
        const id = encodeURIComponent(form.dataset.intakeDraftId || '');
        const url = mode === 'edit' ? `${endpoint}/update/${id}` : `${endpoint}/create`;
        const result = await postForm(url, new FormData(form));
        if (!result.ok) {
            showAlert(result.message);
            return;
        }

        const intakeDraftId = result.body?.intakeDraftId || result.body?.IntakeDraftId || result.body?.data?.intakeDraftId || id;
        window.location.assign(intakeDraftId
            ? `/Pharmacovigilance/CaseIntakeTriage/Details/${encodeURIComponent(intakeDraftId)}`
            : '/Pharmacovigilance/CaseIntakeTriage');
    });

    async function postForm(url, formData) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                body: formData,
                headers: antiForgeryHeader(form),
                credentials: 'same-origin'
            });
            const body = await responseJson(response);
            if (!response.ok || isBlocked(body)) {
                return { ok: false, message: safeMessage(body) };
            }
            return { ok: true, body };
        } catch (error) {
            return { ok: false, message: t('ErrorOccurred') };
        }
    }

    function antiForgeryHeader(scope) {
        const token = scope.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        return token ? { RequestVerificationToken: token } : {};
    }

    async function responseJson(response) {
        try {
            return await response.json();
        } catch (error) {
            return {};
        }
    }

    function isBlocked(body) {
        const outcome = body?.outcome || body?.Outcome || '';
        return ['Blocked', 'Invalid'].includes(outcome);
    }

    function safeMessage(body) {
        const reason = body?.reasonCode || body?.ReasonCode || body?.reason_code || '';
        const validation = body?.validationReasonCodes || body?.ValidationReasonCodes || [];
        const codes = Array.isArray(validation) && validation.length ? validation.join(', ') : reason;
        return codes ? `${t('Blocked')}: ${codes}` : t('ErrorOccurred');
    }

    function showAlert(message) {
        if (!alertEl) return;
        alertEl.textContent = message;
        alertEl.classList.remove('d-none');
    }
})();
