/**
 * MOD-0028-FU04 - Manual QMS baseline creation.
 * Same-origin MVC proxy only; no browser call to service ports and no client TenantId payload.
 */
'use strict';

(function () {
    const form = document.getElementById('qmsManualCreateForm');
    if (!form) return;

    const L = window.L10n || {};
    const errorBox = document.getElementById('qmsManualCreateError');
    const t = (key, fallback) => L[key] || fallback || key;
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const antiForgeryToken = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const showError = (message, correlationId) => {
        errorBox.classList.remove('d-none');
        errorBox.innerHTML = esc(message) + (correlationId ? `<div class="small mt-2">${esc(t('CorrelationId'))}: <code>${esc(correlationId)}</code></div>` : '');
    };

    const reasonText = (json) => {
        const code = json?.reason_code || json?.reasonCode;
        return {
            VALIDATION_FAILED: t('ReasonValidationFailed'),
            CONFLICT: t('ReasonConflict'),
            PERM_DENIED: t('ReasonPermDenied'),
            NOT_FOUND_NON_LEAKAGE: t('ReasonNotFound')
        }[code] || (json?.errors && json.errors[0]) || t('ErrorOccurred');
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        errorBox.classList.add('d-none');
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }

        const submit = form.querySelector('button[type="submit"]');
        submit?.setAttribute('disabled', 'disabled');
        const token = antiForgeryToken();
        const body = new FormData(form);
        if (token && !body.has('__RequestVerificationToken')) {
            body.append('__RequestVerificationToken', token);
        }

        try {
            const response = await fetch('/DocumentManagementQmsBaselines/manual', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { RequestVerificationToken: token },
                body
            });
            let json = null;
            try { json = await response.json(); } catch (e) { /* ignore */ }
            if (json?.isSuccessful && json.data?.id) {
                window.showToast?.(t('ManualBaselineCreated'), 'success');
                window.location.href = `/DocumentManagementQmsBaselines/Designer/${json.data.id}`;
                return;
            }

            showError(reasonText(json), json?.correlation_id || json?.correlationId);
            window.showToast?.(reasonText(json), 'error');
        } catch (error) {
            console.error('[QmsBaselines] manual create failed', error);
            showError(t('ErrorOccurred'));
        } finally {
            submit?.removeAttribute('disabled');
        }
    });
})();
