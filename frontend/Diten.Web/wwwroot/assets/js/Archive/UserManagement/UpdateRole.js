'use strict';
document.addEventListener('DOMContentLoaded', function () {
    loadPermissions();
    const selectAllCheckbox = document.getElementById('selectAll');
    selectAllCheckbox.addEventListener('change', toggleAllCheckboxes);
    initEditRole();
    initializeUpdateFormValidation();
});

async function loadPermissions() {

    const urlParams = new URLSearchParams(window.location.search);
    const id = urlParams.get('id');
    const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/Role/GetPermissionByRoleId/${id}`); // API adresin 
    const result = await response.json();

    const tbody = document.getElementById('permissionsTableBodyupdate');
    tbody.innerHTML = ''; // Önce temizleyelim

    result.data.forEach(item => {
        const tr = document.createElement('tr');

        
        tr.innerHTML = `
                <td class="text-nowrap fw-medium text-heading">${item.menuName}</td>
                <td class="text-nowrap fw-medium text-heading">${item.name}</td>
                <td>
                    <div class="d-flex justify-content-end">
                        ${item.actionPermissions.map(permission =>`
                            <div class="form-check mb-0 me-4 me-lg-12">
                                <input class="form-check-input" type="checkbox" id="${item.id.replace(/\s+/g, '')}_${permission.actionId}" ${permission.isChecked ? 'checked' : ''} />
                                <label class="form-check-label" for="${item.id.replace(/\s+/g, '')}${permission.actionId}"> ${permission.actionName} </label>
                            </div>
                        `).join('')}
                    </div>
                </td>
            `;

        tbody.appendChild(tr);
    });

    applyCheckboxStates(result);

}

function applyCheckboxStates(result) {
    let allChecked = true; // Başta hepsi checked varsayıyoruz
    result.data.forEach(item => {
        item.actionPermissions.forEach(permission => {
            const checkboxId = `update_${item.id.replace(/\s+/g, '')}_${permission.actionId}`;
            const checkbox = document.getElementById(checkboxId);
            if (checkbox) {
                checkbox.checked = permission.isChecked;
                if (!permission.isChecked) {
                    allChecked = false;
                }
            }
        });
    });

    const selectAllCheckbox = document.getElementById('selectAll');
    if (selectAllCheckbox) {
        selectAllCheckbox.checked = allChecked;
    }


}


function toggleAllCheckboxes() {
    const selectAllCheckbox = document.getElementById('selectAll');
    const allCheckboxes = document.querySelectorAll('tbody input[type="checkbox"]');

    allCheckboxes.forEach(checkbox => {
        checkbox.checked = selectAllCheckbox.checked;
    });
}

function initEditRole() {
        const urlParams = new URLSearchParams(window.location.search);
        const id = urlParams.get('id');
        const name = urlParams.get('name');
        $('#updateRoleName').val(name);
        $('#updateRoleForm').attr('data-id', id);
}

function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateRoleForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateRoleName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter a name'
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

    handleUpdateFormSubmit(fv);
}




function handleUpdateFormSubmit(fv) {
    fv.on('core.form.valid', function () {

        const userName = window.getUserName();
        const roleName = document.getElementById('updateRoleName').value; 
        const Id = $('#updateRoleForm').attr('data-id');
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
            id:Id,
            name: roleName,
            tenantId: "",
            description: "",
            modifiedBy: userName,
            rolePermissions: rolePermissions,
        };
        fetch(`${window.ApiBaseUrl}/services/PvTenant/Role/UpdateRole`, {
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

