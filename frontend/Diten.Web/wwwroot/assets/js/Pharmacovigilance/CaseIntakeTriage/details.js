'use strict';

(function () {
    const host = document.getElementById('pvg-case-intake-detail');
    if (!host) return;

    const L = window.PvgCaseIntakeTriageL10n || {};
    const t = key => L[key] || key;
    const endpoint = '/Pharmacovigilance/CaseIntakeTriage/api';
    const intakeDraftId = host.dataset.intakeDraftId || '';
    const alertEl = document.getElementById('pvg-detail-alert');

    loadDetail();
    bindCommand('pvg-triage-form', `${endpoint}/triage/${encodeURIComponent(intakeDraftId)}`, t('Triaged'));
    bindCommand('pvg-route-form', `${endpoint}/route/${encodeURIComponent(intakeDraftId)}`, t('Routed'));

    async function loadDetail() {
        try {
            const response = await fetch(`${endpoint}/detail/${encodeURIComponent(intakeDraftId)}`, { credentials: 'same-origin' });
            const body = await responseJson(response);
            const item = firstItem(body);
            if (!response.ok || isBlocked(body) || !item) {
                showAlert(safeMessage(body));
                return;
            }
            document.getElementById('pvgDetailStatus').textContent = item.status || item.Status || '';
        } catch (error) {
            showAlert(t('ErrorOccurred'));
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
                    headers: antiForgeryHeader(form),
                    credentials: 'same-origin'
                });
                const body = await responseJson(response);
                if (!response.ok || isBlocked(body)) {
                    showAlert(safeMessage(body));
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

    function hideAlert() {
        alertEl?.classList.add('d-none');
    }
})();
