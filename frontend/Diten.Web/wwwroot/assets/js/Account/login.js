const LoginPage = (function () {
    'use strict';

    const config = {
        authMode: (window.AuthMode || 'tenant').toLowerCase(),
        tenantLoginEndpoint: '/account/login',
        mfaEndpoint: '/account/login/mfa',
        mfaResendEndpoint: '/account/login/mfa/resend',
        platformLoginEndpoint: '/platform/login',
        // FIX-LOGIN-TENANT-FALLBACK — Development only: DefaultTenant id injected by the server (empty in Production).
        devDefaultTenantId: window.DevDefaultTenantId || null
    };

    function init() {
        bindEvents();
    }

    function bindEvents() {
        const form = document.getElementById('loginForm');
        if (form) {
            form.addEventListener('submit', handleLogin);
        }
        const mfaForm = document.getElementById('mfaForm');
        if (mfaForm) {
            mfaForm.addEventListener('submit', handleMfaVerify);
        }
        const resendBtn = document.getElementById('resendMfaCode');
        if (resendBtn) {
            resendBtn.addEventListener('click', handleMfaResend);
        }
    }

    async function handleLogin(e) {
        e.preventDefault();
        const submitBtn = e.target.querySelector('button[type="submit"]');
        const email = document.getElementById('email').value.trim();
        const password = document.getElementById('password').value;
        const rememberMe = document.getElementById('remember-me')?.checked === true;

        if (!email || !password) {
            showError(window.L10n?.LoginErrorRequired || '');
            return;
        }

        // Loading state
        submitBtn.disabled = true;
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${window.L10n?.LoginLoading || 'Signing in...'}`;

        try {
            const tenantId = config.authMode === 'platform' ? null : resolveTenantIdForLogin();
            if (config.authMode !== 'platform' && !tenantId) {
                throw refusal(window.L10n?.TenantLoginMissingTenant || '');
            }

            const loginEndpoint = config.authMode === 'platform'
                ? config.platformLoginEndpoint
                : config.tenantLoginEndpoint;
            const response = await fetch(loginEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'same-origin',
                body: JSON.stringify({
                    email,
                    password,
                    rememberMe,
                    tenantId,
                    returnUrl: new URLSearchParams(window.location.search).get('ReturnUrl')
                        || new URLSearchParams(window.location.search).get('returnUrl')
                })
            });

            if (!response.ok) {
                throw refusal(await refusalSentence(response));
            }

            const data = await response.json();
            if (data.requiresMfa && data.challengeId) {
                showMfaStep(data);
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalText;
                return;
            }

            // FIX-TENANT-MUSTCHANGEPW — temp-password tenant user must change it first (server sets redirectUrl).
            if (data.requiresPasswordChange) {
                window.location.href = data.redirectUrl || '/account/change-password';
                return;
            }

            window.location.href = data.redirectUrl || window.PostLoginDefault || '/WorkCenterNext';

        } catch (error) {
            showError(sentenceOf(error));
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        }
    }

    async function handleMfaVerify(e) {
        e.preventDefault();
        const submitBtn = e.target.querySelector('button[type="submit"]');
        const challengeId = document.getElementById('mfaChallengeId').value;
        const code = document.getElementById('mfaCode').value.trim();
        if (!challengeId || !code) {
            showError(window.L10n?.MfaRequired || 'Verification code is required.');
            return;
        }

        submitBtn.disabled = true;
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${window.L10n?.MfaVerifying || 'Verifying...'}`;

        try {
            const response = await fetch(config.mfaEndpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify({
                    challengeId,
                    code,
                    returnUrl: new URLSearchParams(window.location.search).get('ReturnUrl')
                        || new URLSearchParams(window.location.search).get('returnUrl')
                })
            });

            if (!response.ok) {
                throw refusal(await refusalSentence(response));
            }

            const data = await response.json();
            window.location.href = data.redirectUrl || window.PostLoginDefault || '/WorkCenterNext';
        } catch (error) {
            showError(sentenceOf(error));
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        }
    }

    async function handleMfaResend() {
        const resendBtn = document.getElementById('resendMfaCode');
        const challengeId = document.getElementById('mfaChallengeId').value;
        if (!resendBtn || !challengeId) {
            showError(window.L10n?.MfaChallengeRequired || 'Verification challenge is required.');
            return;
        }

        resendBtn.disabled = true;
        const originalText = resendBtn.innerHTML;
        resendBtn.innerHTML = window.L10n?.MfaResending || 'Sending...';

        try {
            const response = await fetch(config.mfaResendEndpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify({ challengeId })
            });

            if (!response.ok) {
                throw refusal(await refusalSentence(response));
            }

            const data = await response.json();
            showMfaStep(data);
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    icon: 'success',
                    title: window.L10n?.MfaResentTitle || 'Code sent',
                    text: window.L10n?.MfaResentMessage || 'A new verification code has been sent.',
                    confirmButtonText: window.L10n?.ConfirmButton || 'OK'
                });
            }
        } catch (error) {
            showError(sentenceOf(error));
        } finally {
            window.setTimeout(() => {
                resendBtn.disabled = false;
                resendBtn.innerHTML = originalText;
            }, 60000);
        }
    }

    function showMfaStep(data) {
        document.getElementById('loginForm')?.classList.add('d-none');
        document.getElementById('mfaForm')?.classList.remove('d-none');
        document.getElementById('mfaChallengeId').value = data.challengeId || '';
        document.getElementById('mfaDestination').innerText = data.maskedDestination || data.channel || '';
        document.getElementById('mfaCode')?.focus();
    }

    // WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — a refusal is said through the shared bridge (diten-refusal.js): the Web
    // door already answers in the reader's language ({ errors:[sentence], local:true }); anything else (no body, an
    // unexpected shape) is the general sentence — never the service's English, never a raw code.
    async function refusalSentence(response) {
        const problem = await response.json().catch(() => ({}));
        if (window.DitenRefusal) return window.DitenRefusal.message(problem, {}, window.L10n, 'login');
        return (problem && problem.local === true && problem.detail) || window.L10n?.ErrorOccurred || '';
    }

    // FIX2 item 6 — only a refusal this page put together (already the reader's language) is shown as it is; anything else
    // thrown on the way (a network failure's TypeError, a parse error — English, and not the reader's business) is the
    // general sentence.
    function refusal(sentence) {
        const error = new Error(sentence);
        error.isRefusal = true;
        return error;
    }

    function sentenceOf(error) {
        return error && error.isRefusal && error.message ? error.message : (window.L10n?.ErrorOccurred || '');
    }

    function resolveTenantIdForLogin() {
        // FIX-LOGIN-TENANT-FALLBACK — prefer ?tenantId= (invite link); otherwise fall back to the Development-only
        // DefaultTenant injected by the server. In Production devDefaultTenantId is null, so the guard below still
        // blocks a tenant login that has no explicit tenant.
        const tenantFromQuery = new URLSearchParams(window.location.search).get('tenantId');
        return tenantFromQuery || config.devDefaultTenantId || null;
    }

    function showError(message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: window.L10n?.LoginErrorTitle || '',
                // FIX2 item 6 — TEXT, never HTML: whatever the sentence holds is shown, not run.
                text: message,
                iconHtml: '<div class="swal-icon-circle bg-label-danger border-danger border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(255, 76, 81, 0.12); border: 2px solid rgba(255, 76, 81, 0.25); margin: 0 auto !important;"><i class="bx bx-error-circle text-danger" style="font-size: 2.5rem; color: #ff4c51;"></i></div>',
                confirmButtonText: window.L10n?.ConfirmButton || 'OK',
                width: '400px',
                padding: '2.5rem 1.5rem 2rem',
                customClass: {
                    popup: 'rounded-4 shadow-lg',
                    title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                    htmlContainer: 'mb-1 d-block w-100 text-center text-muted',
                    actions: 'd-flex justify-content-center mt-4 w-100',
                    confirmButton: 'btn btn-primary waves-effect waves-light px-5',
                    icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
                },
                buttonsStyling: false,
                scrollbarPadding: false,
                heightAuto: false
            });
        } else {
            alert(message);
        }
    }

    return { init };
})();

document.addEventListener('DOMContentLoaded', LoginPage.init);
