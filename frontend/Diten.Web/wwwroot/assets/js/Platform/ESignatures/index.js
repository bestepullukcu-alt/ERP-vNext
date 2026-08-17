'use strict';

$(function () {
    const tableEl = $('#esignatures-table');
    if (!tableEl.length) return;

    const tenantId = tableEl.data('tenant-id');
    const ajaxUrl = `/Platform/ESignatures/api/tenants/${tenantId}/envelopes`;

    const dt = tableEl.DataTable({
        serverSide: true,
        processing: true,
        ajax: {
            url: ajaxUrl,
            type: 'GET',
            data: function (d) {
                return {
                    page: (d.start / d.length) + 1,
                    pageSize: d.length
                };
            },
            dataSrc: function (json) {
                json.recordsTotal = json.totalCount || 0;
                json.recordsFiltered = json.totalCount || 0;
                return json.data || [];
            }
        },
        columns: [
            { data: 'envelopeNumber', name: 'EnvelopeNumber' },
            { data: 'subjectType', name: 'SubjectType' },
            { 
                data: 'status', 
                name: 'Status',
                render: function (data) {
                    let badgeClass = 'bg-label-secondary';
                    if (data === 'Completed') badgeClass = 'bg-label-success';
                    if (data === 'Canceled') badgeClass = 'bg-label-danger';
                    if (data === 'Pending') badgeClass = 'bg-label-warning';
                    return `<span class="badge ${badgeClass}">${data}</span>`;
                }
            },
            { 
                data: 'createdAt', 
                name: 'CreatedAt',
                render: function (data) {
                    if (!data) return '';
                    return new Date(data).toLocaleString();
                }
            },
            {
                data: 'id',
                orderable: false,
                searchable: false,
                render: function (data) {
                    return `<a href="/Platform/ESignatures/${tenantId}/Details/${data}" class="btn btn-sm btn-icon btn-text-secondary rounded-pill waves-effect" title="Details">
                                <i class="bx bx-show-alt"></i>
                            </a>`;
                }
            }
        ],
        order: [[3, 'desc']],
        dom: '<"row mx-2"<"col-md-2"<"me-3"l>><"col-md-10"<"dt-action-buttons text-xl-end text-lg-start text-md-end text-start d-flex align-items-center justify-content-end flex-md-row flex-column mb-3 mb-md-0"fB>>>t<"row mx-2"<"col-sm-12 col-md-6"i><"col-sm-12 col-md-6"p>>',
        language: {
            sLengthMenu: '_MENU_',
            search: '',
            searchPlaceholder: 'Search...'
        },
        buttons: []
    });
});
