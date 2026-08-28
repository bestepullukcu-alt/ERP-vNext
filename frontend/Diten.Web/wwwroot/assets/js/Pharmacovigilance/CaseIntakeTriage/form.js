'use strict';

(function () {
    const form = document.getElementById('pvg-case-intake-form');
    if (!form) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const alertEl = document.getElementById('pvg-form-alert');
    const submitButton = form.querySelector('button[type="submit"]');
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const ajaxHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const safeProxyUrl = path => {
        if (typeof path !== 'string' || !path.startsWith(`${endpoint}/`) || path.includes('://') || path.startsWith('//')) {
            throw new Error('Invalid same-origin PVG proxy endpoint.');
        }

        return path;
    };

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
        const url = mode === 'edit' ? safeProxyUrl(`${endpoint}/update/${id}`) : safeProxyUrl(`${endpoint}/create`);
        setSubmitting(true);
        const result = await postForm(url, new FormData(form));
        setSubmitting(false);
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
                headers: { ...ajaxHeaders(), ...antiForgeryHeader(form) },
                credentials: 'same-origin'
            });
            const body = await responseJson(response);
            if (!response.ok || isBlocked(body)) {
                return { ok: false, message: safeMessage(body, response.status) };
            }
            return { ok: true, body };
        } catch (error) {
            return { ok: false, message: error?.message === 'Invalid same-origin PVG proxy endpoint.' ? t('InvalidProxyEndpoint') : t('ErrorOccurred') };
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
        const statusCode = Number(body?.statusCode || body?.StatusCode || 0);
        return ['Blocked', 'Invalid'].includes(outcome) || [401, 403, 409].includes(statusCode);
    }

    function safeMessage(body, statusCode) {
        const status = Number(statusCode || body?.statusCode || body?.StatusCode || 0);
        if (status === 401) return t('SessionExpired');
        if (status === 403) return t('NotAuthorized');

        const reason = safeCode(body?.reasonCode || body?.ReasonCode || body?.reason_code || '');
        const validation = body?.validationReasonCodes || body?.ValidationReasonCodes || [];
        const codes = Array.isArray(validation) && validation.length
            ? validation.map(safeCode).filter(Boolean).join(', ')
            : reason;
        if (status === 409) {
            return `${t('ControlledBlock')}: ${codes || reason || t('ReasonCode')}`;
        }

        return codes ? `${t('ControlledBlock')}: ${codes}` : t('ErrorOccurred');
    }

    function safeCode(value) {
        return String(value || '').replace(/[^A-Za-z0-9._-]/g, '').slice(0, 96);
    }

    function showAlert(message) {
        if (!alertEl) return;
        alertEl.textContent = message;
        alertEl.classList.remove('d-none');
    }

    function setSubmitting(isSubmitting) {
        if (!submitButton) return;
        submitButton.disabled = isSubmitting;
        submitButton.setAttribute('aria-busy', isSubmitting ? 'true' : 'false');
    }
})();
