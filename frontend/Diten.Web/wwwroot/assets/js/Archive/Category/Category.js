'use strict';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchCategory"] || "Search Category";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initCategoryDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Dil dosyası yüklenemedi:', error);
            initCategoryDataTable("Search Category", data); // fallback
        });


    modifyDataTableLayout();
    bindDeleteRecordEvent();

    const menuUrl = `${protocol}://${domain}:${port}/services/PvTenant/Menu/GetMenuByTenantId`;
    const pageUrl = `${protocol}://${domain}:${port}/services/PvTenant/Menu/GetPageByTenantId`;

    fetchOptions(menuUrl, "add-category-menu", "Select a menu");
    fetchOptions(pageUrl, "add-category-page", "Select a page");

    initializeFormValidation();
    initEditCategory();
    initializeUpdateFormValidation();

});


function initCategoryDataTable(placeholderText, lanData) {
    const dt_category_table = document.querySelector('.category-table');
    if (dt_category_table) {
        const dt_category = new DataTable(dt_category_table, {
            ajax: {
                url: `${protocol}://${domain}:${port}/services/PvTenant/Category/GetCategoriesByTenantId`,
                method: 'GET',
                dataSrc: 'data',
                error: function (jqxhr, textStatus, errorThrown) {
                    console.error("DataTable Hatası:", jqxhr.status, errorThrown);
                    if (jqxhr.status !== 200) {
                        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status;
                    }
                }
            },
            columns: [
                { data: 'id' },
                { data: 'name' },
                { data: 'menuName' },
                { data: 'pageName' },
                {
                    data: 'isActive',
                    render: function (data, type, row) {
                        return data
                            ? `<span class="badge bg-label-success text-capitalized">Yes</span>`
                            : `<span class="badge bg-label-danger text-capitalized">No</span>`;
                    }
                },
                { data: null }
            ],
            columnDefs: [
                {
                    className: 'control',
                    responsivePriority: 2,
                    searchable: false,
                    targets: 0,
                    render: function () {
                        return '';
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    render: (data, type, full) => {
                        return `
                            <div class="d-flex align-items-center">
                                <a href="javascript:;" class="btn btn-icon edit-category" 
                                    data-id="${full.id}" 
                                    data-name="${full.name}" 
                                    data-menuid="${full.menuId}" 
                                    data-pageid="${full.pageId}"
                                    data-is-active="${full.isActive}">
                                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                                </a>
                                <a href="javascript:;" class="btn btn-icon delete-category" data-id="${full.id}">
                                    <i class="icon-base bx bx-trash icon-md"></i>
                                </a>
                            </div>
                        `;
                    }
                }
            ],
            order: [[1, 'asc']],
            displayLength: 25,
            layout: {
                topStart: {
                    rowClass: 'row m-3 justify-content-between',
                    features: [
                        {
                            pageLength: {
                                menu: [10, 25, 50, 100],
                                text: '_MENU_'
                            }
                        }
                    ]
                },
                topEnd: {
                    rowClass: 'row mx-3 justify-content-between',
                    features: [
                        {
                            search: {
                                placeholder: placeholderText,
                                text: '_INPUT_'
                            }
                        },
                        {
                            buttons: [
                                {
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add Category</span>',
                                    className: 'add-new btn btn-primary',
                                    attr: {
                                        'data-bs-toggle': 'offcanvas',
                                        'data-bs-target': '#AddCategory'
                                    }
                                }
                            ]
                        }
                    ]
                },
                bottomStart: {
                    rowClass: 'row mx-3 justify-content-between',
                    features: ['info']
                },
                bottomEnd: {
                    paging: {
                        firstLast: false
                    }
                }
            },
            language: {
                paginate: {
                    next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
                    previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
                },
                sInfo: lanData.DataTable.sInfo,
                sInfoEmpty: lanData.DataTable.sInfoEmpty,
                sInfoFiltered: lanData.DataTable.sInfoFiltered,
                sLengthMenu: lanData.DataTable.sLengthMenu
            },
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: function (row) {
                            const data = row.data();
                            return 'Details of ' + data['name'];
                        }
                    }),
                    type: 'column',
                    renderer: function (api, rowIdx, columns) {
                        const data = columns
                            .map(col => col.title !== '' ? `
                                <tr data-dt-row="${col.rowIndex}" data-dt-column="${col.columnIndex}">
                                    <td>${col.title}:</td>
                                    <td>${col.data}</td>
                                </tr>` : ''
                            ).join('');
                        if (data) {
                            const div = document.createElement('div');
                            div.classList.add('table-responsive');
                            const table = document.createElement('table');
                            div.appendChild(table);
                            table.classList.add('table');
                            const tbody = document.createElement('tbody');
                            tbody.innerHTML = data;
                            table.appendChild(tbody);
                            return div;
                        }
                        return false;
                    }
                }
            },
            initComplete: function () {
                modifyDataTableLayout();
            },
            drawCallback: function () {
                modifyDataTableLayout();
            }
        });
    }
}
function bindDeleteRecordEvent() {
    let recordIdToDelete = null;
    let rowToDelete = null;

    document.addEventListener('click', function (e) {
        if (e.target.closest('.delete-category')) {
            const button = e.target.closest('.delete-category');
            recordIdToDelete = button.getAttribute('data-id');
            rowToDelete = button.closest('tr');

            const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
            deleteModal.show();
        }
    });

    document.getElementById('confirmDeleteBtn').addEventListener('click', async function () {
        if (!recordIdToDelete) return;
        const userName = window.getUserName();
        try {
            const response = await fetch(`${protocol}://${domain}:${port}/services/PvTenant/Category/DeleteCategory`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.category-table').DataTable();
                table.ajax.reload(null, false);

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                showToast('Silme işlemi başarısız oldu.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('Bir hata oluştu.', "error");

        }
    });
}
function modifyDataTableLayout() {

    const elementsToModify = [
        { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
        { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
        { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
        { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
        { selector: '.dt-search', classToAdd: 'mb-md-6 mb-2' },
        {
            selector: '.dt-layout-end',
            classToRemove: 'justify-content-between',
            classToAdd: 'd-flex gap-md-4 justify-content-md-between justify-content-center gap-4 flex-wrap mt-0'
        },
        { selector: '.dt-layout-start', classToAdd: 'mt-0' },
        { selector: '.dt-buttons', classToAdd: 'd-flex gap-4 mb-md-0 mb-6' },
        { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
        { selector: '.dt-layout-full', classToRemove: 'col-md col-12', classToAdd: 'table-responsive' }
    ];

    elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
        document.querySelectorAll(selector).forEach(element => {
            if (classToRemove) {
                classToRemove.split(' ').forEach(className => element.classList.remove(className));
            }
            if (classToAdd) {
                classToAdd.split(' ').forEach(className => element.classList.add(className));
            }
        });
    });

}

async function fetchOptions(apiUrl, selectElementId, placeholderText = "Select an option") {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // İlk boş option
        const defaultOption = new Option(placeholderText, "", false, false);
        selectElement.appendChild(defaultOption);

        data.data.forEach(item => {
            const value = item.id ?? item.iso2 ?? item.name;
            const option = new Option(item.name, value, false, false);
            selectElement.appendChild(option);
        });

        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Veriler alınırken hata oluştu:", error);
    }
}
async function fetchSelectedOptions(apiUrl, selectElementId, selectedOptionId, excludeOptionId, placeholderText = "Select an option") {
    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        const selectElement = document.getElementById(selectElementId);

        // Select2 varsa önce destroy et (varsa)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).empty().trigger('change');
        } else {
            selectElement.innerHTML = '';
        }

        // İlk boş option
        const defaultOption = new Option(placeholderText, "", false, false);
        selectElement.appendChild(defaultOption);

        let categoryList = data.data || [];
        if (excludeOptionId != null && excludeOptionId !== "") {
            categoryList = categoryList.filter(category => category.id !== excludeOptionId);
        }

        // Seçilecek ID belirleniyor
        let autoSelectId = null;
        if (selectedOptionId != null && selectedOptionId !== "") {
            autoSelectId = selectedOptionId;
        } else if (categoryList.length === 1 && (selectedOptionId == null || selectedOptionId == "") && (excludeOptionId == null || excludeOptionId == "")) {
            autoSelectId = categoryList[0].id;
        }

        categoryList.forEach(category => {
            const value = category.id ?? category.name;
            const isSelected = autoSelectId != null && value === autoSelectId;

            const option = new Option(category.name, value, isSelected, isSelected);
            selectElement.appendChild(option);
        });

        // Select2 aktifse change tetikle (yeniden initialize etmeye gerek yok)
        if ($(selectElement).hasClass("select2")) {
            $(selectElement).trigger('change');
        }

    } catch (error) {
        console.error("Ülkeler alınırken hata oluştu:", error);
    }
}

function initializeFormValidation() {
    const addCategoryForm = document.getElementById('addCategoryForm');

    if (!addCategoryForm) return;

    const fv = FormValidation.formValidation(addCategoryForm, {
        fields: {
            categoryMenu: {
                validators: {
                    notEmpty: {
                        message: 'Please select a menu '
                    }
                }
            },
            categoryPage: {
                validators: {
                    notEmpty: {
                        message: 'Please select a page '
                    }
                }
            },
            name: {
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

        const menu = document.getElementById('add-category-menu').value;
        const page = document.getElementById('add-category-page').value;
        const name = document.getElementById('add-category-name').value;
        const userName = window.getUserName();

        const formData = {
            menuId: menu,
            pageId: page,
            name: name,
            createdBy: userName
        };
        fetch(`${protocol}://${domain}:${port}/services/PvTenant/Category/CreateCategory`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                addCategoryForm.reset();

                const table = $('.category-table').DataTable();
                table.ajax.reload();
            })
            .catch(error => {
                console.error(error);
                showToast('Kayıt sırasında bir hata oluştu.', "error");

            });
    });
}
function initEditCategory() {
    $(document).on('click', '.edit-category', async function () {
        const id = $(this).data('id');
        const name = $(this).data('name');
        const isActive = $(this).data('isActive');
        const menuId = $(this).data('menuid');
        const pageId = $(this).data('pageid');

        const menuUrl = `${protocol}://${domain}:${port}/services/PvTenant/Menu/GetMenuByTenantId`;
        const pageUrl = `${protocol}://${domain}:${port}/services/PvTenant/Menu/GetPageByTenantId`;

        await fetchSelectedOptions(menuUrl, "update-menu", menuId, id);
        await fetchSelectedOptions(pageUrl, "update-page", pageId, id);

        document.getElementById('updateChcIsActive').checked = isActive;
        $('#update-category-name').val(name);
        $('#updateCategoryForm').attr('data-id', id);

        const offcanvasEl = document.getElementById('UpdateCategory');
        const bsOffcanvas = new bootstrap.Offcanvas(offcanvasEl);
        bsOffcanvas.show();
    });
}
function initializeUpdateFormValidation() {
    const updateForm = document.getElementById('updateCategoryForm');

    if (!updateForm) return;

    const fv = FormValidation.formValidation(updateForm, {
        fields: {
            updateCategoryMenu: {
                validators: {
                    notEmpty: {
                        message: 'Please select a menu '
                    }
                }
            },
            updateCategoryPage: {
                validators: {
                    notEmpty: {
                        message: 'Please select a page '
                    }
                }
            },
            updateName: {
                validators: {
                    notEmpty: {
                        message: 'Please enter name '
                    }
                }
            }
        },
        plugins: { // ← BU DIŞARIDA OLMALI
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
        const form = document.getElementById('updateCategoryForm');
        const categoryId = $('#updateCategoryForm').attr('data-id');
        const menuId = document.getElementById('update-menu').value;
        const pageId = document.getElementById('update-page').value;
        const name = document.getElementById('update-category-name').value;
        const isActive = document.getElementById('updateChcIsActive').checked;

        const userName = window.getUserName();


        const updatedData = {
            id: categoryId,
            name: name,
            menuId: menuId,
            pageId: pageId,
            isActive: isActive,
            modifiedBy: userName
        };
        fetch(`${protocol}://${domain}:${port}/services/PvTenant/Category/UpdateCategory`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedData)
        })
            .then(response => response.json())
            .then(data => {
                fv.resetForm(true);
                const isSuccess = data.errors === null;
                if (isSuccess) {

                    const table = $('.category-table').DataTable();
                    table.ajax.reload();


                    const offcanvasEl = document.getElementById('UpdateCategory');
                    const bsOffcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                    bsOffcanvas.hide();

                    showToast('The record has been added successfully.', "success");
                } else {
                    const errorMessage = data.errors?.join('<br>') || 'Güncelleme sırasında bir hata oluştu.';
                    showToast(errorMessage, "error");
                }
            })
            .catch(error => {
                console.error(error);

                showToast('Sunucuya bağlanırken bir hata oluştu.', "error");
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

async function loadSelectPickerOptions(apiUrl, selectId, valueField, textField) {
    const response = await fetch(apiUrl);
    if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
    }
    const result = await response.json();
    const $select = $(`#${selectId}`);
    $select.selectpicker('destroy').empty();
    result.data.forEach(item => {
        const value = item[valueField];
        const text = item[textField];
        $select.append(new Option(text, value));
    });

    $select.selectpicker('refresh');

}

async function loadUpdateSelectPickerOptions(apiUrl, selectId, valueField, textField) {

    $select.selectpicker('refresh');


}
