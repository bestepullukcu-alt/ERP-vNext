'use strict';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchMarketingAuthorization"] || "Search Marketing Authorization";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search Marketing Authorization", data); // fallback
        });
    initEdit();
    bindDeleteRecordEvent();
    //StatusChange();
});

function initEdit() {
    $(document).on('click', '.registered-record', function () {
        const id = $(this).data('id');
        var url = `/registration/edit-marketing-authorization?id=${id}&disabledStatus=0&maStatus=2`;
        window.location.href = url;
    });
    $(document).on('click', '.overview-record', function () {
        const id = $(this).data('id');
        var url = `/registration/edit-marketing-authorization?id=${id}&disabledStatus=1`;
        window.location.href = url;
    });
    $(document).on('click', '.rejected-record', function () {
        const id = $(this).data('id');
        var url = `/registration/edit-marketing-authorization?id=${id}&disabledStatus=0&maStatus=3`;
        window.location.href = url;
    });

    $(document).on('click', '.reregistration-record', function () {
        const id = $(this).data('id');
        var url = `/registration/add-marketing-authorization?id=${id}&disabledStatus=0&maStatus=4`;
        window.location.href = url;
    });

    $(document).on('click', '.inprogress-record', function () {
        const id = $(this).data('id');
        var url = `/registration/add-marketing-authorization?id=${id}&disabledStatus=0&maStatus=1`;
        window.location.href = url;
    });

    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        const maStatus = $(this).data('mastatus');
        var url = `/registration/add-marketing-authorization?id=${id}&disabledStatus=0&maStatus=${maStatus}`;
        window.location.href = url;
    });

    $(document).on('click', '.ioverview-record', function () {
        const id = $(this).data('id');
        const maStatus = $(this).data('mastatus');
        var url = `/registration/add-marketing-authorization?id=${id}&disabledStatus=1&maStatus=${maStatus}`;
        window.location.href = url;
    });
    
}


function initDataTable(placeholderText, lanData) {

    const dt_ma_table = document.querySelector('.ma-table');
    if (dt_ma_table) {
        const dt_ma = new DataTable(dt_ma_table, {
            ajax: {
                url: `${window.ApiBaseUrl}/services/PvOrganization/Ma/GetMarketingAuthorizations`,
                method: 'GET',
                dataSrc: 'data',
                error: function (jqxhr, textStatus, errorThrown) {
                    // Bu callback sadece DataTable özelinde hata olursa çalışır
                    console.error("DataTable Error:", jqxhr.status, errorThrown);
                    if (jqxhr.status !== 200) {
                        window.location.href = '/pages-misc-error.html?code=' + jqxhr.status; // Hata kodu ile yönlendirme
                    }
                }
            },
            columns: [
                { data: 'id' },
                { data: 'globalSkuName' },
                { data: 'countryBrandName' },
                { data: 'countryName' },
                { data: 'organizationName' },
                { data: 'maStatusName' },
                { data: 'forms' },
                { data: null },
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
                    targets: 5, // 'maStatusName' kolonu
                    render: function (data, type, row, meta) {
                        let badgeColor = 'primary'; // Varsayılan

                        switch (row.maStatus) {
                            case 1:
                                badgeColor = 'warning';
                                break;
                            case 2:
                                badgeColor = 'success';
                                break;
                            case 3:
                                badgeColor = 'danger';
                                break;
                            case 4:
                                badgeColor = 'info';
                                break;
                            case 5:
                                badgeColor = 'danger';
                                break;
                            case 0:
                                badgeColor = 'primary';
                                break;
                        }

                        return `<span class="badge bg-label-${badgeColor}">${data || '-'}</span>`;
                    }
                },
                {
                    targets: 6, // 'forms' kolonunun indexi
                    render: function (data, type, row, meta) {
                        return data && data.length > 0
                            ? data.join('<br>')  // veya ', ' ile ayırabilirsin
                            : '-';
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const statusId = full.maStatus;
                        let buttons = '';
                        let dropdownItems = '';

                        // Butonlar
                        if ([1, 2, 4, 5].includes(statusId)) {
                            buttons += `<a href="javascript:;" class="btn btn-icon edit-record" title="Edit" data-id="${full.id}" data-mastatus="${full.maStatus}">
            <i class="icon-base bx bx-edit-alt icon-md"></i>
        </a>`;
                        }

                        if ([1, 2, 3, 4, 5].includes(statusId)) {
                            buttons += `<a href="javascript:;" class="btn btn-icon delete-record" title="Delete" data-id="${full.id}">
            <i class="icon-base bx bx-trash icon-md"></i>
        </a>`;
                        }

                        // Dropdown menü öğeleri
                        if (statusId === 1) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button text-success registered-record" data-id="${full.id}" data-action="register">
    <i class="bx bx-check-circle me-1"></i> Registered
</a>
            <a href="javascript:void(0);" class="dropdown-item status-button text-danger rejected-record" data-id="${full.id}" data-action="reject">
    <i class="bx bx-x-circle me-1"></i> Rejected
</a>
            <a href="javascript:void(0);" class="dropdown-item status-button ioverview-record" data-id="${full.id}" data-mastatus="${full.maStatus}" data-action="overview">
                <i class="bx bx-show-alt me-1"></i> Overview
            </a>`;
                        } else if (statusId === 2) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button reregistration-record" data-id="${full.id}" data-action="re-register">
                <i class="bx bx-refresh me-1"></i> Re-registration
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="download">
                <i class="bx bx-download me-1"></i> Download
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button overview-record" data-id="${full.id}" data-action="overview">
                <i class="bx bx-show-alt me-1"></i> Overview
            </a>`;
                        } else if (statusId === 3) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button inprogress-record" data-id="${full.id}" data-action="in-progress">
                <i class="bx bx-time me-1"></i> In Progress
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button overview-record" data-id="${full.id}" data-action="overview">
                <i class="bx bx-show-alt me-1"></i> Overview
            </a>`;
                        } else if (statusId === 4) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button text-success registered-record" data-id="${full.id}" data-action="register">
    <i class="bx bx-check-circle me-1"></i> Registered
</a>
<a href="javascript:void(0);" class="dropdown-item status-button text-danger rejected-record" data-id="${full.id}" data-action="reject">
    <i class="bx bx-x-circle me-1"></i> Rejected
</a>
            <a href="javascript:void(0);" class="dropdown-item ioverview-record" data-id="${full.id}" data-mastatus="${full.maStatus}" data-action="overview">
                <i class="bx bx-show-alt me-1"></i> Overview
            </a>`;
                        } else if (statusId === 5) {
                            dropdownItems += `
            <a href="javascript:void(0);" class="dropdown-item status-button reregistration-record" data-id="${full.id}" data-action="re-register">
                <i class="bx bx-refresh me-1"></i> Re-registration
            </a>
            <a href="javascript:void(0);" class="dropdown-item status-button ioverview-record" data-id="${full.id}" data-mastatus="${full.maStatus}" data-action="overview">
                <i class="bx bx-show-alt me-1"></i> Overview
            </a>`;
                        }

                        // Dropdown HTML
                        const dropdown = `
        <div class="btn-group">
            <button class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                <i class="bx bx-dots-vertical-rounded"></i>
            </button>
            <div class="dropdown-menu dropdown-menu-end">
                ${dropdownItems}
            </div>
        </div>`;

                        return `
        <div class="d-flex justify-content-sm-start align-items-sm-center">
            ${buttons}
            ${dropdown}
        </div>`;
                    }
                }
            ],
            select: {
                style: 'multi',
                selector: 'td:nth-child(2)'
            },
            order: [[1, 'asc']],
            displayLength: 100,
            layout: {
                topStart: {
                    rowClass: 'row m-3 justify-content-between',
                    features: [
                        {
                            pageLength: {
                                menu: [10, 25, 50, 100],
                                text: '_MENU_'
                            },
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
                                    text: '<i class="icon-base bx bx-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block" data-i18n="Add">Add</span>',
                                    className: 'add-new btn btn-primary',
                                    action: function () {
                                        window.location = '/registration/add-marketing-authorization';
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
                            return 'Details of ' + data['companyName'];
                        }
                    }),
                    type: 'column',
                    renderer: function (api, rowIdx, columns) {
                        const data = columns
                            .map(function (col) {
                                return col.title !== '' // ? Do not show row in modal popup if title is blank (for check box)
                                    ? `<tr data-dt-row="${col.rowIndex}" data-dt-column="${col.columnIndex}">
                      <td>${col.title}:</td>
                      <td>${col.data}</td>
                    </tr>`
                                    : '';
                            })
                            .join('');

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
                const api = this.api();

                // Helper function to create a select dropdown and append options
                const createFilter = (columnIndex, containerClass, selectId, defaultOptionText) => {
                    const column = api.column(columnIndex);
                    const select = document.createElement('select');
                    select.id = selectId;
                    select.className = 'form-select text-capitalize';
                    select.innerHTML = `<option value="">${defaultOptionText}</option>`;
                    document.querySelector(containerClass).appendChild(select);

                    // Add event listener for filtering
                    select.addEventListener('change', () => {
                        const val = select.value ? `^${select.value}$` : '';
                        column.search(val, true, false).draw();
                    });

                    // Populate options based on unique column data
                    const uniqueData = Array.from(new Set(column.data().toArray())).sort();
                    uniqueData.forEach(d => {
                        if (d && d.trim() !== '') {
                            const option = document.createElement('option');
                            option.value = d;
                            option.textContent = d;
                            select.appendChild(option);
                        }
                    });
                };

                // ✅ Yeni filtreler (Column index'lerini güncel tabloya göre kontrol et!)
                createFilter(4, '.ma_company', 'MaCompany', 'Select Company');
                createFilter(3, '.ma_country', 'MaCountry', 'Select Country');
                createFilter(5, '.ma_status', 'MaStatus', 'Select Status');
                modifyDataTableLayout();
            },
            drawCallback: function () {
                modifyDataTableLayout(); // her yeniden çizimde stil uygula (sayfalama, filtre vs.)
            }
        });

    }

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

function bindDeleteRecordEvent() {
    let recordIdToDelete = null;
    let rowToDelete = null;

    document.addEventListener('click', function (e) {
        if (e.target.closest('.delete-record')) {
            const button = e.target.closest('.delete-record');
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
            const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/Ma/DeleteMa`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.ma-table').DataTable();
                table.ajax.reload(null, false);

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                showToast('The delete operation failed.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('An unexpexted error.', "error");

        }
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
