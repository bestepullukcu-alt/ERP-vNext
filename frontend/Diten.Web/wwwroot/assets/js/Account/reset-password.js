const ResetPasswordPage = (function () {
    'use strict';

    function init() {
        document.getElementById('resetPasswordForm')?.addEventListener('submit', submit);
    }

    async function submit(event) {
        event.preventDefault();
        const button = event.target.querySelector('button[type="submit"]');
        const config = window.ResetPasswordConfig || {};
        const currentPassword = document.getElementById('currentPassword')?.value || '';
        const newPassword = document.getElementById('newPassword')?.value || '';
        const confirmPassword = document.getElementById('confirmPassword')?.value || '';

        if ((config.isForcedChange && !currentPassword) || !newPassword || !confirmPassword) {
            showError(window.L10n?.Required || 'All password fields are required.');
            return;
        }

        if (newPassword !== confirmPassword) {
            showError(window.L10n?.Mismatch || 'Passwords do not match.');
            return;
        }

        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${window.L10n?.Loading || 'Please wait...'}`;
        try {
            const url = config.isForcedChange ? '/platform/change-password' : '/platform/reset-password';
            const body = config.isForcedChange
                ? { currentPassword, newPassword, confirmPassword, rememberMe: true }
                : { email: config.email || document.getElementById('email')?.value || '', token: config.token || document.getElementById('token')?.value || '', newPassword, confirmPassword };
            const response = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(payload.detail || 'Password could not be changed.');
            await Swal.fire({
                title: window.L10n?.SuccessTitle || 'Şifre Güncellendi',
                html: `<div class="mb-1 text-muted">${window.L10n?.SuccessMessage || 'Şifreniz başarıyla güncellendi.'}</div>`,
                iconHtml: '<div class="swal-icon-circle bg-label-success border-success border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(113, 221, 55, 0.12); border: 2px solid rgba(113, 221, 55, 0.25); margin: 0 auto !important;"><i class="bx bx-check-circle text-success" style="font-size: 2.5rem; color: #71dd37;"></i></div>',
                confirmButtonText: 'Tamam',
                width: '400px',
                padding: '2.5rem 1.5rem 2rem',
                customClass: {
                    popup: 'rounded-4 shadow-lg',
                    title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                    htmlContainer: 'mb-1 d-block w-100 text-center',
                    actions: 'd-flex justify-content-center mt-4 w-100',
                    confirmButton: 'btn btn-primary waves-effect waves-light px-5',
                    icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
                },
                buttonsStyling: false,
                scrollbarPadding: false,
                heightAuto: false
            });
            window.location.href = payload.redirectUrl || '/platform/login';
        } catch (error) {
            showError(error.message);
        } finally {
            button.disabled = false;
            button.innerHTML = original;
        }
    }

    function showError(message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: window.L10n?.ErrorTitle || 'Hata',
                html: `<div class="mb-1 text-muted">${message}</div>`,
                iconHtml: '<div class="swal-icon-circle bg-label-danger border-danger border-opacity-25" style="display: flex; align-items: center; justify-content: center; width: 80px; height: 80px; border-radius: 50%; background: rgba(255, 76, 81, 0.12); border: 2px solid rgba(255, 76, 81, 0.25); margin: 0 auto !important;"><i class="bx bx-error-circle text-danger" style="font-size: 2.5rem; color: #ff4c51;"></i></div>',
                confirmButtonText: 'Tamam',
                width: '400px',
                padding: '2.5rem 1.5rem 2rem',
                customClass: {
                    popup: 'rounded-4 shadow-lg',
                    title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                    htmlContainer: 'mb-1 d-block w-100 text-center',
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

document.addEventListener('DOMContentLoaded', ResetPasswordPage.init);
