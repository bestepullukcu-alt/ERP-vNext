const LoginPage = (function () {
    'use strict';

    const config = {
        apiBaseUrl: window.ApiBaseUrl,
        loginEndpoint: '/api/auth/login',
        tenantId: '00000000-0000-0000-0000-000000000001'
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
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Giriş yapılıyor...';

        try {
            const response = await fetch(`${config.apiBaseUrl}${config.loginEndpoint}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Tenant-Id': config.tenantId
                },
                body: JSON.stringify({ email, password })
            });

            if (!response.ok) {
                const problem = await response.json();
                throw new Error(problem.detail || problem.title || 'Giriş başarısız. Lütfen bilgilerinizi kontrol edin.');
            }

            const data = await response.json();

            // Set cookies (Simulating HttpOnly access, but using JS accessible for simplicity)
            setCookie('access_token', data.accessToken, data.expiresAt);
            setCookie('refresh_token', data.refreshToken, 7);

            // Save user info (display only)
            localStorage.setItem('user', JSON.stringify(data.user));

            // Redirect to returnUrl or home
            const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
            window.location.href = returnUrl || '/Skus';

        } catch (error) {
            showError(error.message);
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        }
    }

    function setCookie(name, value, expiryOrDays) {
        let expires = '';
        if (typeof expiryOrDays === 'string') {
            expires = `expires=${new Date(expiryOrDays).toUTCString()}`;
        } else {
            const date = new Date();
            date.setTime(date.getTime() + (expiryOrDays * 24 * 60 * 60 * 1000));
            expires = `expires=${date.toUTCString()}`;
        }
        document.cookie = `${name}=${value};${expires};path=/;SameSite=Strict`;
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
