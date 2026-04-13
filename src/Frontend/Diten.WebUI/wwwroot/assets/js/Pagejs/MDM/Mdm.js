// ============================================================
// API CONFIGURATION
// ============================================================
const protocol = window.location.protocol;

const API_CONFIG = {
    protocol,
    domain: window.location.hostname,
    port: protocol === 'https:' ? '5003' : '5000',
    servicePath: 'services/DitenPPM',
    controller: 'TypeValue'
};

// Build base API URL according to specification
const API_BASE_URL = `${API_CONFIG.protocol}://${API_CONFIG.domain}:${API_CONFIG.port}/${API_CONFIG.servicePath}/${API_CONFIG.controller}`;

// ============================================================
// API ENDPOINTS MAPPING
// ============================================================
const API_ENDPOINTS = {
    getAll: `${API_BASE_URL}`,              // GET: https://domain:443/services/DitenPPM/TypeValue
    getById: (id) => `${API_BASE_URL}/${id}`,  // GET: https://domain:443/services/DitenPPM/TypeValue/{id}
    create: `${API_BASE_URL}`,              // POST: https://domain:443/services/DitenPPM/TypeValue
    update: (id) => `${API_BASE_URL}/${id}`,   // PUT: https://domain:443/services/DitenPPM/TypeValue/{id}
    delete: (id) => `${API_BASE_URL}/${id}`    // DELETE: https://domain:443/services/DitenPPM/TypeValue/{id}
};

let typeValueModal;
let isEditMode = false;

$(document).ready(function () {
    const modalEl = document.getElementById('typeValueModal');
    typeValueModal = new bootstrap.Modal(modalEl);

    $('#apiBaseDisplay').text(API_BASE_URL);
    loadTypeValues();
    initializeEventHandlers();
});

function initializeEventHandlers() {
    // Create button
    $('#btnCreate').on('click', function () {
        openModal(false);
    });

    // Form submit
    $('#typeValueForm').on('submit', function (e) {
        e.preventDefault();
        if (validateForm()) {
            saveTypeValue();
        }
    });

    // Color picker sync
    $('#color').on('input', function () {
        $('#colorHex').val($(this).val());
    });

    $('#colorHex').on('input', function () {
        const hex = $(this).val();
        if (/^#[0-9A-F]{6}$/i.test(hex)) {
            $('#color').val(hex);
        }
    });

    // Modal reset on close
    $('#typeValueModal').on('hidden.bs.modal', function () {
        resetForm();
    });
}

// ============================================================
// API CALL: GET ALL TYPE VALUES
// Endpoint: GET /services/DitenPPM/TypeValue
// Response: List<TypeValueDto>
// ============================================================
function loadTypeValues() {
    $('#loadingRow').show();
    $('#emptyState').hide();

    $.ajax({
        url: API_ENDPOINTS.getAll,
        method: 'GET',
        dataType: 'json',
        headers: {
            'Content-Type': 'application/json'
        },
        success: function (data) {
            console.log('API Response (GetAll):', data);
            renderTable(data);
        },
        error: function (xhr, status, error) {
            console.error('API Error (GetAll):', xhr.responseJSON || error);
            $('#loadingRow').html(`<td colspan="6" class="text-center text-danger py-4">
                        <i class="fas fa-exclamation-circle me-2"></i>Error loading data: ${xhr.responseJSON?.message || error}
                    </td>`);
        }
    });
}

function renderTable(data) {
    const tbody = $('#tableBody');
    tbody.empty();

    if (!data || data.length === 0) {
        $('#emptyState').show();
        return;
    }

    // Sort by order field
    data.sort((a, b) => a.order - b.order);

    data.forEach(item => {
        const statusBadge = item.status
            ? '<span class="badge bg-success">Active</span>'
            : '<span class="badge bg-secondary">Inactive</span>';

        const colorPreview = item.color
            ? `<span class="color-preview" style="background-color: ${item.color}"></span>`
            : '<span class="text-muted">-</span>';

        const row = `
                    <tr data-id="${item.id}">
                        <td><span class="cursor-move">${item.order}</span></td>
                        <td>${escapeHtml(item.label)}</td>
                        <td><code>${escapeHtml(item.code)}</code></td>
                        <td>${colorPreview}</td>
                        <td>${statusBadge}</td>
                        <td class="text-end">
                            <button class="btn btn-sm btn-icon btn-outline-primary" onclick="editTypeValue('${item.id}')" title="Edit">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button class="btn btn-sm btn-icon btn-outline-danger ms-1" onclick="deleteTypeValue('${item.id}', '${escapeHtml(item.label)}')" title="Delete">
                                <i class="fas fa-trash"></i>
                            </button>
                        </td>
                    </tr>
                `;
        tbody.append(row);
    });
}

function openModal(edit, data = null) {
    isEditMode = edit;
    $('#modalTitle').text(edit ? 'Edit Type Value' : 'Create Type Value');

    if (edit && data) {
        $('#recordId').val(data.id);
        $('#order').val(data.order);
        $('#label').val(data.label);
        $('#code').val(data.code).prop('readonly', true);
        $('#codeHelp').show();
        $('#color').val(data.color || '#6c757d');
        $('#colorHex').val(data.color || '#6c757d');
        $('#status').prop('checked', data.status);
    }

    typeValueModal.show();
}

function resetForm() {
    $('#typeValueForm')[0].reset();
    $('#typeValueForm').removeClass('was-validated');
    $('#recordId').val('');
    $('#code').prop('readonly', false).removeClass('is-invalid');
    $('#codeHelp').hide();
    $('#codeFeedback').text('Code is required');
    $('#color').val('#6c757d');
    $('#colorHex').val('#6c757d');
    isEditMode = false;
}

function validateForm() {
    const form = $('#typeValueForm')[0];
    form.classList.add('was-validated');

    if (!form.checkValidity()) {
        return false;
    }

    const order = $('#order').val();
    if (!order || order < 0) {
        $('#order').addClass('is-invalid');
        return false;
    }

    return true;
}

// ============================================================
// API CALL: CREATE TYPE VALUE
// Endpoint: POST /services/DitenPPM/TypeValue
// Request Body: CreateTypeValueDto
// Response: TypeValueDto
// ============================================================
// API CALL: UPDATE TYPE VALUE
// Endpoint: PUT /services/DitenPPM/TypeValue/{id}
// Request Body: UpdateTypeValueDto
// Response: TypeValueDto
// ============================================================
function saveTypeValue() {
    const saveBtn = $('#btnSave');
    const spinner = $('#saveSpinner');

    saveBtn.prop('disabled', true);
    spinner.show();

    let requestData;
    let url;
    let method;

    if (isEditMode) {
        // UPDATE: UpdateTypeValueDto (no code field)
        requestData = {
            order: parseInt($('#order').val()),
            label: $('#label').val().trim(),
            color: $('#color').val(),
            status: $('#status').is(':checked')
        };
        url = API_ENDPOINTS.update($('#recordId').val());
        method = 'PUT';
        console.log('API Request (Update):', { url, method, data: requestData });
    } else {
        // CREATE: CreateTypeValueDto (includes code field)
        requestData = {
            order: parseInt($('#order').val()),
            label: $('#label').val().trim(),
            code: $('#code').val().trim(),
            color: $('#color').val(),
            status: $('#status').is(':checked')
        };
        url = API_ENDPOINTS.create;
        method = 'POST';
        console.log('API Request (Create):', { url, method, data: requestData });
    }

    $.ajax({
        url: url,
        method: method,
        contentType: 'application/json',
        dataType: 'json',
        data: JSON.stringify(requestData),
        headers: {
            'Content-Type': 'application/json'
        },
        success: function (response) {
            console.log('API Response (Save):', response);
            typeValueModal.hide();
            loadTypeValues();
            showToast('success', `Type value ${isEditMode ? 'updated' : 'created'} successfully`);
        },
        error: function (xhr) {
            console.error('API Error (Save):', xhr.responseJSON || xhr.statusText);
            const error = xhr.responseJSON?.message || 'An error occurred';

            // Handle code uniqueness error (409 Conflict)
            if (xhr.status === 409 || (error.toLowerCase().includes('code') && error.toLowerCase().includes('exists'))) {
                $('#code').addClass('is-invalid');
                $('#codeFeedback').text('Code already exists');
            }
            // Handle validation errors (400 Bad Request)
            else if (xhr.status === 400 && xhr.responseJSON?.errors) {
                const errors = xhr.responseJSON.errors;
                showToast('error', Array.isArray(errors) ? errors.join(', ') : errors);
            } else {
                showToast('error', error);
            }
        },
        complete: function () {
            saveBtn.prop('disabled', false);
            spinner.hide();
        }
    });
}

// ============================================================
// API CALL: GET TYPE VALUE BY ID
// Endpoint: GET /services/DitenPPM/TypeValue/{id}
// Response: TypeValueDto
// ============================================================
function editTypeValue(id) {
    $.ajax({
        url: API_ENDPOINTS.getById(id),
        method: 'GET',
        dataType: 'json',
        headers: {
            'Content-Type': 'application/json'
        },
        success: function (data) {
            console.log('API Response (GetById):', data);
            openModal(true, data);
        },
        error: function (xhr) {
            console.error('API Error (GetById):', xhr.responseJSON || xhr.statusText);
            showToast('error', xhr.responseJSON?.message || 'Failed to load type value');
        }
    });
}

// ============================================================
// API CALL: DELETE TYPE VALUE (Soft Delete)
// Endpoint: DELETE /services/DitenPPM/TypeValue/{id}
// Response: 204 No Content
// ============================================================
function deleteTypeValue(id, label) {
    if (!confirm(`Are you sure you want to delete "${label}"?`)) {
        return;
    }

    console.log('API Request (Delete):', { url: API_ENDPOINTS.delete(id), method: 'DELETE' });

    $.ajax({
        url: API_ENDPOINTS.delete(id),
        method: 'DELETE',
        headers: {
            'Content-Type': 'application/json'
        },
        success: function () {
            console.log('API Response (Delete): Success');
            loadTypeValues();
            showToast('success', 'Type value deleted successfully');
        },
        error: function (xhr) {
            console.error('API Error (Delete):', xhr.responseJSON || xhr.statusText);
            showToast('error', xhr.responseJSON?.message || 'Failed to delete type value');
        }
    });
}

function showToast(type, message) {
    const bgClass = type === 'success' ? 'bg-success' : 'bg-danger';
    const icon = type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle';

    const toast = $(`
                <div class="toast align-items-center text-white ${bgClass} border-0" role="alert" style="position: fixed; top: 20px; right: 20px; z-index: 9999;">
                    <div class="d-flex">
                        <div class="toast-body">
                            <i class="fas ${icon} me-2"></i>${message}
                        </div>
                        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                    </div>
                </div>
            `);

    $('body').append(toast);
    const bsToast = new bootstrap.Toast(toast[0], { delay: 3000 });
    bsToast.show();
    toast.on('hidden.bs.toast', function () {
        $(this).remove();
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}