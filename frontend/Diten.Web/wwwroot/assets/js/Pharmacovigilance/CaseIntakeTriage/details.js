'use strict';

(function () {
    const host = document.getElementById('pvg-case-intake-detail');
    if (!host) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const intakeDraftId = host.dataset.intakeDraftId || '';
    const alertEl = document.getElementById('pvg-detail-alert');
    const ajaxHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const safeProxyUrl = path => {
        if (typeof path !== 'string' || !path.startsWith(`${endpoint}/`) || path.includes('://') || path.startsWith('//')) {
            throw new Error('Invalid same-origin PVG proxy endpoint.');
        }

        return path;
    };

    loadDetail();
    bindCommand('pvg-triage-form', safeProxyUrl(`${endpoint}/triage/${encodeURIComponent(intakeDraftId)}`), t('Triaged'));
    bindCommand('pvg-route-form', safeProxyUrl(`${endpoint}/route/${encodeURIComponent(intakeDraftId)}`), t('Routed'));

    async function loadDetail() {
        try {
            const response = await fetch(safeProxyUrl(`${endpoint}/detail/${encodeURIComponent(intakeDraftId)}`), {
                credentials: 'same-origin',
                headers: ajaxHeaders()
            });
            const body = await responseJson(response);
            const item = firstItem(body);
            if (!response.ok || isBlocked(body) || !item) {
                showAlert(safeMessage(body, response.status));
                return;
            }
            document.getElementById('pvgDetailStatus').textContent = item.status || item.Status || '';
        } catch (error) {
            showAlert(error?.message === 'Invalid same-origin PVG proxy endpoint.' ? t('InvalidProxyEndpoint') : t('ErrorOccurred'));
        }
    }

    function bindCommand(formId, url, successText) {
        const form = document.getElementById(formId);
        if (!form) return;
        form.addEventListener('submit', async event => {
            event.preventDefault();
            event.stopPropagation();
            form.classList.add('was-validated');
            if (!form.checkValidity()) {
                showAlert(t('ReasonCode'));
                return;
            }

            try {
                const response = await fetch(url, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: { ...ajaxHeaders(), ...antiForgeryHeader(form) },
                    credentials: 'same-origin'
                });
                const body = await responseJson(response);
                if (!response.ok || isBlocked(body)) {
                    showAlert(safeMessage(body, response.status));
                    return;
                }
                document.getElementById('pvgDetailStatus').textContent = successText;
                hideAlert();
            } catch (error) {
                showAlert(t('ErrorOccurred'));
            }
        });
    }

    function firstItem(body) {
        const items = body?.items || body?.Items || body?.data?.items || body?.Data?.Items || [];
        return Array.isArray(items) ? items[0] : null;
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

    function hideAlert() {
        alertEl?.classList.add('d-none');
    }
})();
