'use strict';

$(function () {
    const cardEl = $('#details-card');
    if (!cardEl.length) return;

    const tenantId = cardEl.data('tenant-id');
    const envelopeId = cardEl.data('envelope-id');
    
    const spinner = $('#details-spinner');
    const content = $('#details-content');
    
    // Load Details
    $.ajax({
        url: `/Platform/ESignatures/api/tenants/${tenantId}/envelopes/${envelopeId}`,
        type: 'GET',
        success: function (res) {
            const data = res.data || res;
            $('#lblEnvelopeNumber').text(data.envelopeNumber || '-');
            $('#lblStatus').text(data.status || '-');
            $('#lblSubjectType').text(data.subjectType || '-');
            
            spinner.addClass('d-none');
            content.removeClass('d-none');
        },
        error: function () {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'Failed to load signature details.',
                customClass: {
                    confirmButton: 'btn btn-primary'
                },
                buttonsStyling: false
            });
            spinner.addClass('d-none');
        }
    });

    // Cancel Envelope
    $('#btnCancelEnvelope').on('click', function () {
        Swal.fire({
            title: 'Are you sure?',
            text: "You won't be able to revert this cancellation!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, cancel it!',
            customClass: {
                confirmButton: 'btn btn-danger me-3',
                cancelButton: 'btn btn-label-secondary'
            },
            buttonsStyling: false
        }).then(function (result) {
            if (result.value) {
                $.ajax({
                    url: `/Platform/ESignatures/api/tenants/${tenantId}/envelopes/${envelopeId}/cancel`,
                    type: 'POST',
                    contentType: 'application/json',
                    data: JSON.stringify({ reason: 'Canceled by Platform Admin' }),
                    success: function () {
                        Swal.fire({
                            icon: 'success',
                            title: 'Canceled!',
                            text: 'The signature envelope has been canceled.',
                            customClass: {
                                confirmButton: 'btn btn-success'
                            }
                        }).then(() => {
                            location.reload();
                        });
                    },
                    error: function () {
                        Swal.fire({
                            icon: 'error',
                            title: 'Error',
                            text: 'Failed to cancel the envelope.',
                            customClass: {
                                confirmButton: 'btn btn-primary'
                            },
                            buttonsStyling: false
                        });
                    }
                });
            }
        });
    });

    // Audit Export
    $('#btnAuditExport').on('click', function () {
        $.ajax({
            url: `/Platform/ESignatures/api/tenants/${tenantId}/envelopes/${envelopeId}/audit-export`,
            type: 'POST',
            success: function () {
                Swal.fire({
                    icon: 'success',
                    title: 'Export Triggered',
                    text: 'Audit export generation has been queued successfully.',
                    customClass: {
                        confirmButton: 'btn btn-success'
                    },
                    buttonsStyling: false
                });
            },
            error: function () {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: 'Failed to trigger audit export.',
                    customClass: {
                        confirmButton: 'btn btn-primary'
                    },
                    buttonsStyling: false
                });
            }
        });
    });

    // Download Artifact (Placeholder Logic, needs the Artifact ID. In a real scenario it would be listed in details)
    $('#btnDownloadArtifact').on('click', function () {
        Swal.fire({
            icon: 'info',
            title: 'Artifact Download',
            text: 'Artifact ID is required to download. In a full implementation, available artifacts would be listed here.',
            customClass: {
                confirmButton: 'btn btn-primary'
            },
            buttonsStyling: false
        });
    });
});
