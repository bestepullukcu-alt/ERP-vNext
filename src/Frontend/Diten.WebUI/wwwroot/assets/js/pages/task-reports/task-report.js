'use strict';

let dt_task_table;
let dt_tasks;
let statusChart;

document.addEventListener('DOMContentLoaded', function () {
    initSummaryData();
    initDataTable();
});

function initSummaryData() {
    let apiBaseUrl = window.APP_CONFIG ? window.APP_CONFIG.API_BASE_URL : '';
    const url = `${apiBaseUrl}/api/v1/task-reports/summary`;

    fetch(url)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const summary = data.data;
                document.getElementById('kpi-total-tasks').textContent = summary.totalCount;
                document.getElementById('kpi-completed-tasks').textContent = summary.completedCount;
                document.getElementById('kpi-inprogress-tasks').textContent = summary.inProgressCount;
                document.getElementById('kpi-completion-rate').textContent = summary.completionRate + '%';

                renderChart(summary.chartData, summary.chartLabels);
            }
        })
        .catch(error => console.error('Error fetching summary:', error));
}

function renderChart(data, labels) {
    const chartOrderStatistics = document.querySelector('#taskStatusChart');
    if (chartOrderStatistics === null) return;

    // Use current theme colors from Sneat (assuming standard config)
    let cardColor = config.colors.cardColor;
    let headingColor = config.colors.headingColor;
    let labelColor = config.colors.textMuted;
    let borderColor = config.colors.borderColor;

    const chartOptions = {
        chart: {
            height: 350,
            type: 'donut',
            parentHeightOffset: 0,
            fontFamily: 'Public Sans'
        },
        labels: labels,
        series: data,
        colors: [config.colors.success, config.colors.info, config.colors.warning],
        stroke: {
            width: 5,
            colors: cardColor
        },
        dataLabels: {
            enabled: false,
            formatter: function (val, opt) {
                return parseInt(val) + '%';
            }
        },
        legend: {
            show: true,
            position: 'bottom',
            markers: { offsetX: -3 },
            itemMargin: {
                vertical: 3,
                horizontal: 10
            },
            labels: {
                colors: labelColor,
                useSeriesColors: false
            }
        },
        plotOptions: {
            pie: {
                donut: {
                    size: '75%',
                    labels: {
                        show: true,
                        value: {
                            fontSize: '1.5rem',
                            fontFamily: 'Public Sans',
                            color: headingColor,
                            offsetY: -15,
                            formatter: function (val) {
                                return parseInt(val);
                            }
                        },
                        name: {
                            offsetY: 20,
                            fontFamily: 'Public Sans'
                        },
                        total: {
                            show: true,
                            fontSize: '0.8125rem',
                            label: 'Tasks',
                            color: labelColor,
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                            }
                        }
                    }
                }
            }
        },
        responsive: [
            {
                breakpoint: 992,
                options: {
                    chart: {
                        height: 300
                    },
                    legend: {
                        position: 'bottom'
                    }
                }
            },
            {
                breakpoint: 576,
                options: {
                    chart: {
                        height: 250
                    },
                    legend: {
                        show: false
                    }
                }
            }
        ]
    };

    if (statusChart) {
        statusChart.destroy();
    }
    statusChart = new ApexCharts(chartOrderStatistics, chartOptions);
    statusChart.render();
}

function initDataTable() {
    let apiBaseUrl = window.APP_CONFIG ? window.APP_CONFIG.API_BASE_URL : '';
    dt_task_table = document.querySelector('#tasksTable');

    if (dt_task_table) {
        dt_tasks = new DataTable(dt_task_table, {
            ajax: {
                url: apiBaseUrl + '/api/v1/task-reports',
                method: 'GET',
                dataSrc: 'data'
            },
            columns: [
                { data: null }, // Control
                { data: 'title' },
                { data: 'status' },
                { data: 'createdDate' },
                { data: null } // Actions
            ],
            columnDefs: [
                {
                    className: 'control',
                    orderable: false,
                    searchable: false,
                    responsivePriority: 2,
                    targets: 0,
                    render: function () {
                        return '';
                    }
                },
                {
                    targets: 1,
                    responsivePriority: 1,
                    render: function (data, type, full, meta) {
                        return '<span class="fw-bold text-primary">' + full['title'] + '</span>';
                    }
                },
                {
                    targets: 2,
                    responsivePriority: 3,
                    render: function (data, type, full, meta) {
                        var $status = full['status'];
                        var roleBadgeObj = {
                            'Completed': '<span class="badge bg-label-success">Completed</span>',
                            'Pending': '<span class="badge bg-label-warning">Pending</span>',
                            'InProgress': '<span class="badge bg-label-info">In Progress</span>'
                        };
                        return roleBadgeObj[$status] || '<span class="badge bg-label-secondary">' + $status + '</span>';
                    }
                },
                {
                    targets: 3,
                    responsivePriority: 4,
                    render: function (data, type, full, meta) {
                        var date = new Date(full['createdDate']);
                        if (isNaN(date.getTime())) return full['createdDate'];
                        const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
                        return date.getDate().toString().padStart(2, '0') + ' ' + months[date.getMonth()] + ' ' + date.getFullYear();
                    }
                },
                {
                    targets: -1,
                    title: 'Actions',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 5,
                    render: function (data, type, full, meta) {
                        return (
                            '<div class="d-flex align-items-center">' +
                            '<a href="javascript:;" class="text-body btn-view-task" data-id="' + full['id'] + '"><i class="bx bx-show mx-1"></i></a>' +
                            '<a href="javascript:;" class="text-body btn-edit-status" data-id="' + full['id'] + '" data-status="' + full['status'] + '"><i class="bx bx-edit-alt mx-1"></i></a>' +
                            '<a href="javascript:;" class="text-body btn-delete-task" data-id="' + full['id'] + '"><i class="bx bx-trash mx-1"></i></a>' +
                            '</div>'
                        );
                    }
                }
            ],
            order: [[3, 'desc']],
            layout: {
                topStart: {
                    rowClass: 'row mx-3 my-0 justify-content-between',
                    features: [{ pageLength: { menu: [10, 25, 50], text: '_MENU_' } }]
                },
                topEnd: {
                    features: [{ search: { placeholder: 'Search Task', text: '_INPUT_' } }]
                },
                bottomStart: {
                    rowClass: 'row mx-3 justify-content-between',
                    features: ['info']
                },
                bottomEnd: { paging: { firstLast: false } }
            },
            language: {
                sLengthMenu: '_MENU_',
                search: '',
                searchPlaceholder: 'Search Task',
                paginate: {
                    next: '<i class="bx bx-chevron-right"></i>',
                    previous: '<i class="bx bx-chevron-left"></i>'
                }
            },
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: function (row) { return 'Details of ' + row.data()['title']; }
                    }),
                    type: 'column',
                    renderer: function (api, rowIdx, columns) {
                        var data = columns.map(function (col) {
                            return col.title !== '' ? '<tr><td>' + col.title + ':</td><td>' + col.data + '</td></tr>' : '';
                        }).join('');
                        return data ? $('<table class="table"/><tbody/>').append(data) : false;
                    }
                }
            },
            initComplete: function () { modifyDataTableLayout(); },
            drawCallback: function () { modifyDataTableLayout(); }
        });

        bindActions();
    }
}

function bindActions() {
    let apiBaseUrl = window.APP_CONFIG ? window.APP_CONFIG.API_BASE_URL : '';

    // View Task
    $(document).on('click', '.btn-view-task', function () {
        const id = $(this).data('id');
        fetch(`${apiBaseUrl}/api/v1/task-reports/${id}`)
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    const task = data.data;
                    document.getElementById('view-task-title').textContent = task.title;
                    document.getElementById('view-task-description').textContent = task.description || 'No description provided.';
                    document.getElementById('view-task-date').textContent = new Date(task.createdDate).toLocaleDateString();

                    let statusBadgeClass = 'bg-label-secondary';
                    if (task.status === 'Completed') statusBadgeClass = 'bg-label-success';
                    if (task.status === 'InProgress') statusBadgeClass = 'bg-label-info';
                    if (task.status === 'Pending') statusBadgeClass = 'bg-label-warning';

                    document.getElementById('view-task-status').innerHTML = `<span class="badge ${statusBadgeClass}">${task.status}</span>`;

                    new bootstrap.Modal(document.getElementById('taskDetailsModal')).show();
                }
            });
    });

    // Edit Status
    $(document).on('click', '.btn-edit-status', function () {
        const id = $(this).data('id');
        const status = $(this).data('status');
        document.getElementById('edit-task-id').value = id;
        document.getElementById('edit-task-status').value = status;
        new bootstrap.Modal(document.getElementById('editStatusModal')).show();
    });

    document.getElementById('btn-save-status').addEventListener('click', function () {
        const id = document.getElementById('edit-task-id').value;
        const status = document.getElementById('edit-task-status').value;

        fetch(`${apiBaseUrl}/api/v1/task-reports/status`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id, status })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    bootstrap.Modal.getInstance(document.getElementById('editStatusModal')).hide();
                    refreshAll();
                }
            });
    });

    // Delete Task
    $(document).on('click', '.btn-delete-task', function () {
        const id = $(this).data('id');
        document.getElementById('delete-task-id').value = id;
        new bootstrap.Modal(document.getElementById('deleteConfirmModal')).show();
    });

    document.getElementById('btn-confirm-delete').addEventListener('click', function () {
        const id = document.getElementById('delete-task-id').value;

        fetch(`${apiBaseUrl}/api/v1/task-reports/${id}`, {
            method: 'DELETE'
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
                    refreshAll();
                }
            });
    });
}

function refreshAll() {
    initSummaryData(); // Refresh KPIs and Chart
    dt_tasks.ajax.reload(null, false); // Refresh DataTable
}

function modifyDataTableLayout() {
    const elementsToModify = [
        { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
        { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
        { selector: '.dt-length', classToAdd: 'mb-md-0 mb-0' },
        { selector: '.dt-search', classToAdd: 'mb-md-0 mb-2' },
        { selector: '.dt-layout-end', classToAdd: 'd-flex justify-content-end gap-2 flex-wrap mt-0' },
        { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
        { selector: '.dt-layout-full', classToAdd: 'table-responsive text-nowrap' }
    ];

    elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
        document.querySelectorAll(selector).forEach(element => {
            if (classToRemove) classToRemove.split(' ').forEach(c => element.classList.remove(c));
            if (classToAdd) classToAdd.split(' ').forEach(c => element.classList.add(c));
        });
    });
}
