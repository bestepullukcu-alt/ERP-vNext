'use strict';
const protocol = window.location.protocol;
const domain = window.location.hostname;
const port = protocol === 'https:' ? '5003' : '5000';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchLcppv"] || "Search Lcppv";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search Report", data); // fallback
        });
    initEdit();
    bindDeleteRecordEvent();
    StatusChange();
});

function initDataTable(placeholderText, lanData) {

    const dt_lcppv_table = document.querySelector('.lcppv-table');
    if (dt_lcppv_table) {
        const dt_lcppv = new DataTable(dt_lcppv_table, {
            ajax: {
                url: `${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/GetLcppvs`,
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
                { data: 'idCode' },
                { data: 'companyName' },
                { data: 'companyTypeStr' },
                { data: 'countryName' },
                { data: 'startDateStr' },
                { data: 'endDateStr' },
                { data: 'dueDateStr' },
                { data: 'createDateStr' },
                { data: 'createdBy' },
                { data: 'modifiedDateStr' },
                { data: 'modifiedBy' },
                { data: 'approvedDateStr' },
                { data: 'approvedBy' },
                { data: 'statusName' },
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
                    // Company Type
                    targets: 3,
                    render: function (data, type, full, meta) {

                        if (!data || data.trim() === '-') {
                            return '';
                        }
                        const types = data.split(',').map(type => type.trim());
                        return types
                            .map(type => `<span class="badge bg-label-primary text-capitalized me-1">${type}</span>`)
                            .join('');
                        
                    }
                },
                {
                    targets: 9,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        var name = full['createdBy'];
                       
                        var output;

                            // For Avatar badge
                            var stateNum = Math.floor(Math.random() * 6);
                            var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                            var state = states[stateNum];
                            var initials = (name.split(' ').map(word => word[0]).join('')).toUpperCase();
                            output = '<span class="avatar-initial rounded-circle bg-label-' + state + '">' + initials + '</span>';
                        

                        // Creates full output for row
                        var row_output =
                            '<div class="d-flex justify-content-start align-items-center user-name">' +
                            '<div class="avatar-wrapper">' +
                            '<div class="avatar avatar-sm me-4">' +
                            output +
                            '</div>' +
                            '</div>' +
                            '<div class="d-flex flex-column">' +
                            '<a href="#" class="text-heading text-truncate"><span class="fw-medium">' +
                            name +
                            '</span></a>' +
                            '</div>' +
                            '</div>';
                        return row_output;
                    }
                },
                {
                    targets: 11,
                    responsivePriority: 3,
                    render: function (data, type, full, meta) {
                        var name = full['modifiedBy'];
                        // Eğer name boşsa, hücreyi boş döndür
                        if (!name || name.trim() === '') {
                            return '';
                        }
                        var output;

                        // For Avatar badge
                        var stateNum = Math.floor(Math.random() * 6);
                        var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                        var state = states[stateNum];
                        var initials = (name.split(' ').map(word => word[0]).join('')).toUpperCase();
                        output = '<span class="avatar-initial rounded-circle bg-label-' + state + '">' + initials + '</span>';


                        // Creates full output for row
                        var row_output =
                            '<div class="d-flex justify-content-start align-items-center user-name">' +
                            '<div class="avatar-wrapper">' +
                            '<div class="avatar avatar-sm me-4">' +
                            output +
                            '</div>' +
                            '</div>' +
                            '<div class="d-flex flex-column">' +
                            '<a href="#" class="text-heading text-truncate"><span class="fw-medium">' +
                            name +
                            '</span></a>' +
                            '</div>' +
                            '</div>';
                        return row_output;
                    }
                },
                {
                    targets: 12,
                    responsivePriority: 3,
                    render: function (data, type, full, meta) {
                        return data ? data : '';
                    }
                },
                {
                    targets: 13,
                    responsivePriority: 3,
                    render: function (data, type, full, meta) {
                        var name = full['approvedBy'];
                        // Eğer name boşsa, hücreyi boş döndür
                        if (!name || name.trim() === '') {
                            return '';
                        }
                        var output;

                        // For Avatar badge
                        var stateNum = Math.floor(Math.random() * 6);
                        var states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
                        var state = states[stateNum];
                        var initials = (name.split(' ').map(word => word[0]).join('')).toUpperCase();
                        output = '<span class="avatar-initial rounded-circle bg-label-' + state + '">' + initials + '</span>';


                        // Creates full output for row
                        var row_output =
                            '<div class="d-flex justify-content-start align-items-center user-name">' +
                            '<div class="avatar-wrapper">' +
                            '<div class="avatar avatar-sm me-4">' +
                            output +
                            '</div>' +
                            '</div>' +
                            '<div class="d-flex flex-column">' +
                            '<a href="#" class="text-heading text-truncate"><span class="fw-medium">' +
                            name +
                            '</span></a>' +
                            '</div>' +
                            '</div>';
                        return row_output;
                    }
                },
                {
                    // Status
                    targets: 14,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const statusId = full.statusId;
                        const statusName = full.statusName;

                        if (!statusId || !statusName || statusName.trim() === '-') {
                            return '';
                        }

                        let badgeClass = '';

                        switch (statusId) {
                            case 1:
                                badgeClass = 'bg-label-secondary';
                                break;
                            case 2:
                                badgeClass = 'bg-label-warning';
                                break;
                            case 3:
                                badgeClass = 'bg-label-success';
                                break;
                            case 4:
                                badgeClass = 'bg-label-danger';
                                break;
                            default:
                                badgeClass = 'bg-label-primary';
                                break;
                        }

                        return `<span class="badge ${badgeClass} text-capitalized me-1">${statusName}</span>`;
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        const statusId = full.statusId;
                        let buttons = '';
                        let dropdownItems = '';
                        // Edit veya View butonları
                        if (statusId === 1 || statusId === 4) {
                            buttons += `<a href="javascript:;" class="btn btn-icon edit-record" title="Edit" data-id="${full.id}">
                    <i class="icon-base bx bx-edit-alt icon-md"></i>
                  </a>`;
                        } else if (statusId === 2 || statusId === 3) {
                            buttons += `<a href="javascript:;" class="btn btn-icon view-record" title="View" data-id="${full.id}">
                    <i class="icon-base bx bx-show-alt icon-md"></i>
                  </a>`;
                        }

                        // Dropdown içeriği
                        if (statusId === 1) {
                            dropdownItems += `
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="send-to-approved">Send to Approved</a>
        <a href="javascript:void(0);" class="dropdown-item delete-record" data-id="${full.id}">Delete</a>`;
                        } else if (statusId === 2) {
                            dropdownItems += `
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="back-to-progress">In Progress</a>
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="approve-record">Approve</a>
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="reject-record">Reject</a>`;
                        } else if (statusId === 3) {
                            dropdownItems += `
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="back-to-progress">In Progress</a>`;
                        } else if (statusId === 4) {
                            dropdownItems += `
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="back-to-progress">In Progress</a>
        <a href="javascript:void(0);" class="dropdown-item status-button" data-id="${full.id}" data-action="send-to-approved">Send to Approved</a>
        <a href="javascript:void(0);" class="dropdown-item delete-record" data-id="${full.id}">Delete</a>`;
                        }

                        // Dropdown menü HTML’i
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
                                        window.location = '/pv-system/create-lcppv';
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
                createFilter(2, '.lcppv_company', 'LcppvCompany', 'Select Company');
                createFilter(4, '.lcppv_country', 'LcppvCountry', 'Select Country');
                createFilter(14, '.lcppv_status', 'LcppvStatus', 'Select Status');
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

function initEdit() {
    $(document).on('click', '.edit-record', function () {
        const id = $(this).data('id');
        var url = `/pv-system/create-lcppv?id=${id}&disabledStatus=0`;
        window.location.href = url;
    });
    $(document).on('click', '.view-record', function () {
        const id = $(this).data('id');
        var url = `/pv-system/create-lcppv?id=${id}&disabledStatus=1`;
        window.location.href = url;
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
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/DeleteLcppv`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.lcppv-table').DataTable();
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

async function updateStatus(id, statusId) {
    const userName = window.getUserName();

    try {
        const response = await fetch(`${protocol}//${domain}:${port}/services/PvOrganization/LcppvMonthlyReconcilation/StatusUpdateLcppv`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ id: id, statusId: statusId, modifiedBy: userName })
        });

        const result = await response.json();
        if (result.data === true) {
            const table = $('.lcppv-table').DataTable();
            table.ajax.reload(null, false);
        } else {
            showToast('The update operation failed.', "error");
            console.warn(result.errors); // Hata detayları varsa konsola yaz
        }
    } catch (error) {
        console.error(error);
        showToast('An unexpected error occurred.', "error");
    }
}

function StatusChange() {
    // Common click handler to update status based on class
    $(document).on('click', '.status-button', function () {
        const id = $(this).data('id');
        const action = $(this).data('action'); // We can use this to determine the statusId dynamically

        let statusId = 0;

        switch (action) {
            case 'back-to-progress':
                statusId = 1; // Set statusId for "Back to Progress"
                break;
            case 'send-to-approved':
                statusId = 2; // Set statusId for "Send to Approved"
                break;
            case 'approve-record':
                statusId = 3; // Set statusId for "Approve"
                break;
            case 'reject-record':
                statusId = 4; // Set statusId for "Reject"
                break;
            default:
                statusId = 0; // Default statusId (optional)
        }

        // Call the shared function
        updateStatus(id, statusId);
    });
}