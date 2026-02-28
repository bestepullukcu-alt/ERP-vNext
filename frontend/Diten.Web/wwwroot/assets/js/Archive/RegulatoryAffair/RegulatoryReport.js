'use strict';
document.addEventListener('DOMContentLoaded', function () {

    const lang = localStorage.getItem('language') || 'en';

    fetch(`/assets/lang/${lang}.json`)
        .then(response => response.json())
        .then(data => {
            const placeholderText = data["SearchReport"] || "Search Report";

            // DataTable veya custom tablo init fonksiyonunu burada çağır:
            initDataTable(placeholderText, data);
        })
        .catch(error => {
            console.error('Language file could not be loaded:', error);
            initDataTable("Search Report", data); // fallback
        });

    bindDeleteRecordEvent();
});

function initDataTable(placeholderText, lanData) {

    const dt_regulatoryReport_table = document.querySelector('.regulatory-report-table');
    if (dt_regulatoryReport_table) {
        
        const dt_regulatoryreport = new DataTable(dt_regulatoryReport_table, {
            ajax: {
                url: `${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/GetRegulatoryReports`,
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
                { data: 'countryName' },
                { data: 'regulatoryReportStatusStr' },
                { data: 'authorityName' },
                { data: 'totalTaskCount' },
                { data: 'publishDateStr' },
                { data: 'createdBy' },
                { data: 'createdDateStr' },
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
                    // Invoice Status with tooltip
                    targets: 2,
                    render: function (data, type, full, meta) {

                        return data === 'Active'
                            ? `<span class="badge bg-label-success text-capitalized">${data}</span>`
                            : `<span class="badge bg-label-danger text-capitalized">${data}</span>`;
                    }
                },
                {
                    // Invoice Status with tooltip
                    targets: 4,
                    render: function (data, type, full, meta) {

                        const inProgress = full.inprogressTaskCount;
                        const total = full.totalTaskCount;
                        // oranı yazdır (örnek: 2/5)
                        const ratioText = `${inProgress} / ${total}`;

                        if (total === 0) {
                            return ratioText;
                        }
                        const badgeClass = inProgress < total
                            ? 'bg-label-danger'   // kırmızı
                            : 'bg-label-primary'; // varsayılan (örnek: mavi)

                        return `<span class="badge ${badgeClass}">${ratioText}</span>`;
                    }
                },
                {
                    targets: 6,
                    responsivePriority: 3,
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
                            '</div>';
                        return row_output;
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
                                <a href="javascript:;" class="btn btn-icon delete-record" data-id="${full.id}">
                                    <i class="icon-base bx bx-trash icon-md"></i>
                                </a>
                            </div>
                        `;
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
                                        window.location = '/regulatory-affair/create-regulatory-report';
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
                            return 'Details of ' + data['authorityName'];
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
                modifyDataTableLayout(); // tablo ilk yüklendiğinde stil uygula
                dt_regulatoryreport.on('click', 'tbody tr', function (event) {
                    // Eğer tıklanan öğe delete butonu değilse, satıra tıklandıysa yönlendir
                    if (!event.target.closest('.delete-record')) {
                        const data = dt_regulatoryreport.row(this).data();
                        if (data && data.id) {
                            window.location.href = `/regulatory-affair/create-regulatory-report?id=${data.id}`;
                        }
                    }
                });
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
            const response = await fetch(`${window.ApiBaseUrl}/services/PvOrganization/RegulatoryReport/DeleteRegulatoryReport`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ id: recordIdToDelete, modifiedBy: userName })
            });

            const result = await response.json();
            if (result.data === true) {
                const table = $('.regulatory-report-table').DataTable();
                table.ajax.reload(null,false);

                // Modalı kapat
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
            } else {
                showToast('The delete operation could not be completed.', "error");
                console.warn(result.errors); // Hata detayları varsa konsola yaz
            }
        } catch (error) {
            console.error(error);
            showToast('An unexpected error occurred.', "error");

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

