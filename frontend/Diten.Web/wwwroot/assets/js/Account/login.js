const LoginPage = (function () {
    'use strict';

    const config = {
        authMode: (window.AuthMode || 'tenant').toLowerCase(),
        tenantLoginEndpoint: '/account/login',
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
            window.location.href = data.redirectUrl || window.PostLoginDefault || '/WorkCenter';

        } catch (error) {
            showError(error.message);
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        }
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
