const LoginPage = (function () {
    'use strict';

    const config = {
        authMode: (window.AuthMode || 'tenant').toLowerCase(),
        tenantLoginEndpoint: '/account/login',
        mfaEndpoint: '/account/login/mfa',
        mfaResendEndpoint: '/account/login/mfa/resend',
        platformLoginEndpoint: '/platform/login'
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

        // password toggle
        const toggleBtn = document.getElementById('togglePassword');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', togglePasswordVisibility);
        }
    }

    async function handleLogin(e) {
        e.preventDefault();
        const submitBtn = e.target.querySelector('button[type="submit"]');
        const email = document.getElementById('email').value.trim();
        const password = document.getElementById('password').value;

        if (!email || !password) {
            showError(window.L10n?.LoginErrorRequired || 'Email ve şifre zorunludur.');
            return;
        }

        // Loading state
        submitBtn.disabled = true;
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${window.L10n?.LoginLoading || 'Signing in...'}`;

        try {
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
                    tenantId: config.authMode === 'platform' ? null : resolveTenantIdForLogin(),
                    returnUrl: new URLSearchParams(window.location.search).get('ReturnUrl')
                        || new URLSearchParams(window.location.search).get('returnUrl')
                })
            });

            if (!response.ok) {
                const problem = await response.json();
                throw new Error(problem.detail || problem.title || 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.');
            }

            const data = await response.json();
            if (data.requiresMfa && data.challengeId) {
                showMfaStep(data);
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalText;
                return;
            }

            window.location.href = data.redirectUrl || window.PostLoginDefault || '/WorkCenter';

        } catch (error) {
            showError(error.message);
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
                const problem = await response.json();
                throw new Error(problem.detail || problem.title || 'Verification failed.');
            }

            const data = await response.json();
            window.location.href = data.redirectUrl || window.PostLoginDefault || '/WorkCenter';
        } catch (error) {
            showError(error.message);
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
                const problem = await response.json();
                throw new Error(problem.detail || problem.title || 'Verification code could not be resent.');
            }

            const data = await response.json();
            showMfaStep(data);
            if (typeof Swal !== 'undefined') {
                Swal.fire({
                    icon: 'success',
                    title: window.L10n?.MfaResentTitle || 'Code sent',
                    text: window.L10n?.MfaResentMessage || 'A new verification code has been sent.',
                    confirmButtonText: 'Tamam'
                });
            }
        } catch (error) {
            showError(error.message);
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

    function resolveTenantIdForLogin() {
        const tenantFromQuery = new URLSearchParams(window.location.search).get('tenantId');
        if (tenantFromQuery) {
            return tenantFromQuery;
        }

        // Existing project fallback used by local development until tenant discovery UI lands.
        return '00000000-0000-0000-0000-000000000001';
    }

    function showError(message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                icon: 'error',
                title: window.L10n?.LoginErrorTitle || 'Hata',
                text: message,
                confirmButtonText: 'Tamam'
            });
        } else {
            alert(message);
        }
    }

    function togglePasswordVisibility() {
        const input = document.getElementById('password');
        const icon = this.querySelector('i');
        if (input.type === 'password') {
            input.type = 'text';
            icon.classList.replace('bx-hide', 'bx-show');
        } else {
            input.type = 'password';
            icon.classList.replace('bx-show', 'bx-hide');
        }
    }

    return { init };
})();

document.addEventListener('DOMContentLoaded', LoginPage.init);
