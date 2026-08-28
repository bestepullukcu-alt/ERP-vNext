'use strict';

// Account Create/Edit form init (Select2). Shared by Create.cshtml and Edit.cshtml so the views carry no inline script.
// Account has no date fields, so flatpickr is intentionally not wired here.
document.addEventListener('DOMContentLoaded', function () {
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent()
            });
        });
    }

    initLogoPicker();
});

// Account logo picker: reads the chosen image into a base64 data URI stored in the hidden field, previews it, and
// enforces a ~256 KB cap + allowed image types client-side (the backend re-validates format/size). No external upload.
function initLogoPicker() {
    var box = document.getElementById('accountLogoBox');
    if (!box) {
        return;
    }

    var MAX_BYTES = 256 * 1024;
    var ALLOWED = ['image/png', 'image/jpeg', 'image/gif', 'image/webp', 'image/svg+xml'];

    var fileInput = document.getElementById('accountLogoFile');
    var hidden = document.getElementById('accountLogoData');
    var preview = document.getElementById('accountLogoPreview');
    var placeholder = document.getElementById('accountLogoPlaceholder');
    var removeBtn = document.getElementById('accountLogoRemove');
    var error = document.getElementById('accountLogoError');

    function showError(message) {
        error.textContent = message;
        error.classList.remove('d-none');
    }

    function clearError() {
        error.textContent = '';
        error.classList.add('d-none');
    }

    function applyLogo(dataUri) {
        hidden.value = dataUri || '';
        if (dataUri) {
            preview.src = dataUri;
            preview.classList.remove('d-none');
            placeholder.classList.add('d-none');
            removeBtn.classList.remove('d-none');
        } else {
            preview.src = '';
            preview.classList.add('d-none');
            placeholder.classList.remove('d-none');
            removeBtn.classList.add('d-none');
        }
    }

    fileInput.addEventListener('change', function () {
        clearError();
        var file = fileInput.files && fileInput.files[0];
        if (!file) {
            return;
        }

        if (ALLOWED.indexOf(file.type) === -1) {
            showError(box.getAttribute('data-invalid-type'));
            fileInput.value = '';
            return;
        }

        if (file.size > MAX_BYTES) {
            showError(box.getAttribute('data-too-large'));
            fileInput.value = '';
            return;
        }

        var reader = new FileReader();
        reader.onload = function () {
            applyLogo(reader.result);
        };
        reader.readAsDataURL(file);
    });

    removeBtn.addEventListener('click', function () {
        clearError();
        fileInput.value = '';
        applyLogo('');
    });
}
