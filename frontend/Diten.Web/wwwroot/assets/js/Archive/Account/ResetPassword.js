'use strict';
const port2 = protocol === 'https:' ? '5055' : '5050';


document.addEventListener('DOMContentLoaded', function () {

    getTokenFromURL();
    initializeFormValidation();
});


function initializeFormValidation() {
    const formAuthentication = document.querySelector('#formAuthentication');

    if (!formAuthentication) return;

    const fv = FormValidation.formValidation(formAuthentication, {
        fields: {
            password: {
                validators: {
                    notEmpty: {
                        message: 'Please enter your password'
                    },
                    stringLength: {
                        min: 8,
                        message: 'Password must be more than 8 characters'
                    }
                }
            },
            'confirm-password': {
                validators: {
                    notEmpty: {
                        message: 'Please confirm password'
                    },
                    identical: {
                        compare: () => formAuthentication.querySelector('[name="password"]').value,
                        message: 'The password and its confirmation do not match'
                    },
                    stringLength: {
                        min: 8,
                        message: 'Password must be more than 8 characters'
                    }
                }
            },
        },
        plugins: {
            trigger: new FormValidation.plugins.Trigger(),
            bootstrap5: new FormValidation.plugins.Bootstrap5({
                eleValidClass: '',
                rowSelector: function (field, ele) {
                    return '.form-control-validation';
                }
            }),
            submitButton: new FormValidation.plugins.SubmitButton(),
            autoFocus: new FormValidation.plugins.AutoFocus()
        },
        init: instance => {
            instance.on('plugins.message.placed', e => {
                if (e.element.parentElement.classList.contains('input-group')) {
                    e.element.parentElement.insertAdjacentElement('afterend', e.messageElement);
                }
            });
        }

    });

    handleFormSubmit(fv);
}

function getTokenFromURL() {
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');

    if (!token) {
        alert("Geçersiz veya eksik token.");
        window.location.href = "/";  // Ana sayfaya yönlendir
        return null;
    }

    return token;
}

function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {

        const token = getTokenFromURL();
        if (!token) return;

        const password = document.getElementById('password').value;

        const formData = {
            token: token,
            newPassword: password,
        };

        fetch(`${protocol}//${domain}:${port2}/api/PvUser/User/SetPasswordByToken`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                const isSuccess = data.errors === null;
                if (isSuccess) {

                    window.location.href = "login";
                }
                else {
                    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                    showToast(errorMessage, "error");
                }
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

            });
    });
}

function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    if (!toastEl || !toastBody || !toastHeader) return;

    toastBody.innerHTML = message;

    toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');

    switch (type) {
        case 'success':
            toastEl.classList.add('bg-success');
            toastHeader.textContent = 'Successfull';
            break;
        case 'error':
            toastEl.classList.add('bg-danger');
            toastHeader.textContent = 'Error';
            break;
        case 'warning':
            toastEl.classList.add('bg-warning');
            toastHeader.textContent = 'Warning';
            break;
        case 'info':
            toastEl.classList.add('bg-info');
            toastHeader.textContent = 'Information';
            break;
    }

    const toast = bootstrap.Toast.getOrCreateInstance(toastEl);
    toast.show();
}


