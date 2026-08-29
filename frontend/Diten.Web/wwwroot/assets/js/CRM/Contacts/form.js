'use strict';

// Contact Create/Edit form init (Select2 + dismissible-notice memory). Shared by Create.cshtml and Edit.cshtml so the
// views carry no inline script.
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

    // MOD-0150 — contact avatar: read the chosen image, resize to a 256px square (canvas) and store it as a base64
    // data-URI in the hidden field. No server upload endpoint; the small data-URI travels with the normal form post.
    // Personal data — the backend keeps it off list/export/audit.
    (function () {
        var input = document.getElementById('contactPhotoInput');
        var hidden = document.getElementById('ContactPhotoDataUri');
        var preview = document.getElementById('contactPhotoPreview');
        var placeholder = document.getElementById('contactPhotoPlaceholder');
        var removeBtn = document.getElementById('contactPhotoRemove');
        if (!input || !hidden) return;

        var MAX = 256;

        function show(dataUri) {
            if (dataUri) {
                if (preview) { preview.src = dataUri; preview.classList.remove('d-none'); }
                if (placeholder) placeholder.classList.add('d-none');
                if (removeBtn) removeBtn.classList.remove('d-none');
            } else {
                if (preview) { preview.src = ''; preview.classList.add('d-none'); }
                if (placeholder) placeholder.classList.remove('d-none');
                if (removeBtn) removeBtn.classList.add('d-none');
            }
        }

        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            if (!file) return;
            if (!/^image\//.test(file.type)) {
                window.showToast && window.showToast('Only image files are allowed.', 'warning');
                input.value = '';
                return;
            }
            var reader = new FileReader();
            reader.onload = function (e) {
                var img = new Image();
                img.onload = function () {
                    var scale = Math.min(MAX / img.width, MAX / img.height, 1);
                    var w = Math.round(img.width * scale);
                    var h = Math.round(img.height * scale);
                    var canvas = document.createElement('canvas');
                    canvas.width = w; canvas.height = h;
                    canvas.getContext('2d').drawImage(img, 0, 0, w, h);
                    // JPEG @0.85 keeps a 256px avatar well under the size limit; PNG kept for transparency-less images.
                    var dataUri = canvas.toDataURL('image/jpeg', 0.85);
                    hidden.value = dataUri;
                    show(dataUri);
                };
                img.src = e.target.result;
            };
            reader.readAsDataURL(file);
            input.value = '';
        });

        if (removeBtn) {
            removeBtn.addEventListener('click', function () {
                hidden.value = '';
                show('');
            });
        }
    })();

    // MOD-0150 — remember dismissed PII/KVKK + Notes safety notices. Any alert with [data-dismiss-key] stays hidden
    // after the user closes it (per-browser localStorage). Fail-soft: if storage is unavailable the notice just always
    // shows. Never used for anything but UI preference — no PII stored (only a boolean flag under a fixed key).
    var STORAGE_PREFIX = 'diten:dismiss:';
    document.querySelectorAll('[data-dismiss-key]').forEach(function (el) {
        var key = STORAGE_PREFIX + el.getAttribute('data-dismiss-key');
        var stored = null;
        try { stored = window.localStorage.getItem(key); } catch (e) { stored = null; }
        if (stored === '1') {
            el.remove();
            return;
        }
        el.addEventListener('closed.bs.alert', function () {
            try { window.localStorage.setItem(key, '1'); } catch (e) { /* storage unavailable — nothing to persist */ }
        });
    });
});
