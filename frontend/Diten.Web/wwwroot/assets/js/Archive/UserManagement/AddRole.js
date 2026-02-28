'use strict';
document.addEventListener('DOMContentLoaded', function () {
    loadPermissions();
    const selectAllCheckbox = document.getElementById('selectAll');
    selectAllCheckbox.addEventListener('change', toggleAllCheckboxes);
    initializeFormValidation();
});

async function loadPermissions() {

    const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/Menu/GetPageByTenantId`); // API adresin 
    const result = await response.json();

    const tbody = document.getElementById('permissionsTableBody');
    tbody.innerHTML = ''; // Önce temizleyelim

    result.data.forEach(item => {
        const tr = document.createElement('tr');

        tr.innerHTML = `
                <td class="text-nowrap fw-medium text-heading">${item.menuName}</td>
                <td class="text-nowrap fw-medium text-heading">${item.name}</td>
                <td>
                    <div class="d-flex justify-content-end">
                        ${item.actions.map(permission => `
                            <div class="form-check mb-0 me-4 me-lg-12">
                                <input class="form-check-input" type="checkbox" id="${item.id.replace(/\s+/g, '')}_${permission.id}" />
                                <label class="form-check-label" for="${item.name.replace(/\s+/g, '')}${permission.name}"> ${permission.name} </label>
                            </div>
                        `).join('')}
                    </div>
                </td>
            `;

        tbody.appendChild(tr);
    });
}

function toggleAllCheckboxes() {
    const selectAllCheckbox = document.getElementById('selectAll');
    const allCheckboxes = document.querySelectorAll('tbody input[type="checkbox"]');

    allCheckboxes.forEach(checkbox => {
        checkbox.checked = selectAllCheckbox.checked;
    });
}


function initializeFormValidation() {
    const addRoleForm = document.getElementById('addRoleForm');

    if (!addRoleForm) return;

    const fv = FormValidation.formValidation(addRoleForm, {
        fields: {
            modalRoleName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
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
        }
    });

    handleFormSubmit(fv);
}

function handleFormSubmit(fv) {
    fv.on('core.form.valid', function () {
        //checkAuthentication();
        const userName = getUserName();
        const roleName = document.getElementById('modalRoleName').value;       
        const allCheckboxes = document.querySelectorAll('tbody input[type="checkbox"]:checked');
        const rolePermissionsMap = new Map();

        allCheckboxes.forEach(checkbox => {
            const checkboxId = checkbox.id; // Örn: 680e45a8e36b0b4415d973a3_680e39bcca1066ff1746f60c

            const [pageId, actionId] = checkboxId.split('_');

            if (!rolePermissionsMap.has(pageId)) {
                rolePermissionsMap.set(pageId, []);
            }

            rolePermissionsMap.get(pageId).push(actionId);
        });

        const rolePermissions = [];

        for (const [pageId, actionIds] of rolePermissionsMap.entries()) {
            rolePermissions.push({
                pageId: pageId,
                actionIds: actionIds
            });
        }


        const formData = {
            name: roleName,
            tenantId: "",
            description: "",
            createdBy: userName,
            rolePermissions: rolePermissions,
            };
        fetch(`${window.ApiBaseUrl}/services/PvTenant/Role/CreateRole`, {
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
                    window.location.href = '/UserManagement/Role';

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
