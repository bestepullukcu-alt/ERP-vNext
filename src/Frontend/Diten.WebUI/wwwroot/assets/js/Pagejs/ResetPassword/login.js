'use strict';

document.addEventListener('DOMContentLoaded', function () {
    //document.getElementById('redirectUrl').value = window.location.origin;
    //checkAuthentication();
    initializeFormValidation();
});


function initializeFormValidation() {
    const formAuthentication = document.querySelector('#formAuthenticationLogin');

    if (!formAuthentication) return;

    const fv = FormValidation.formValidation(formAuthentication, {
        fields: {
            passwordLogin: {
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
            'email-username': {
                validators: {
                    notEmpty: {
                        message: 'Please enter email / username'
                    },
                    stringLength: {
                        min: 6,
                        message: 'Username must be more than 6 characters'
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
function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        const protocol = window.location.protocol;
        const domain = window.location.hostname;
        const port = protocol === 'https:' ? '5055' : '5050';


        const email = document.getElementById("email").value;
        const password = document.getElementById('passwordLogin').value;

        const formData = {
            email: email,
            password: password,
        };

        fetch(`${protocol}//${domain}:${port}/api/PvUser/User/login`, {
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
                    const result = data.data;
                    localStorage.setItem("token", result.token);
                    localStorage.setItem("expiration", result.expiration);
                    window.location.href = window.location.origin + "/index.html";
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

function checkAuthentication() {
    const token = localStorage.getItem("token");
    const expiration = localStorage.getItem("expiration");

    if (!token || !expiration) {
        // Token veya expiration yoksa login sayfasında kal
        return;
    }

    const expirationDate = new Date(expiration);
    const now = new Date();

    if (now >= expirationDate) {
        // Token süresi geçmiş, localStorage temizle ve login'e yönlendir
        localStorage.removeItem("token");
        localStorage.removeItem("expiration");
        window.location.href = "/account/login";
    } else {
        alert(window.location.origin);
        // Token geçerli, anasayfaya yönlendir
        window.location.href = window.location.origin + "/";
    }
}