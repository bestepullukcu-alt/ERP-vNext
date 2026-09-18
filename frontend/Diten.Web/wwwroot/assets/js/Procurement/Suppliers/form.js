'use strict';

// Suppliers Create/Edit form init (Select2). Shared by Create.cshtml and Edit.cshtml so the
// views carry no inline script. Mirrors the GoldenReferenceCompact compact form.js Select2 setup.
document.addEventListener('DOMContentLoaded', function () {
    /*
     * SELECT2 — and the placeholder is DECLARED, not left to select2 to guess. Declaring it makes select2
     * render `.select2-selection__placeholder`, which the theme greys like every other hint on the form.
     * The text still comes from the OPTION, so it stays localized in the resx behind the markup.
     */
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent(),
                placeholder: $this.data('placeholder') || $this.find('option[value=""]').text() || ''
            });
        });
    }
});
