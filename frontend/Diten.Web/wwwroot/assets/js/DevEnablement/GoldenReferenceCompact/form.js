'use strict';

// GoldenReferenceCompact Create/Edit form init (dates + Select2).
// Shared by Create.cshtml and Edit.cshtml so the views carry no inline script.
document.addEventListener('DOMContentLoaded', function () {
    /*
     * DATES — through the shared component, never re-implemented here.
     *
     * This file used to construct flatpickr itself. That was enough to render a calendar and NOT enough to make
     * the leading icon work: the icon sits over the field's inline start, so the click a user aims at "the
     * calendar" lands on the glyph. DitenDateField owns both halves, so a form that copies the markup gets the
     * behaviour with it. See assets/js/shared/diten-datefield.js.
     */
    if (window.DitenDateField) {
        window.DitenDateField.enhance(document);
    }

    /*
     * SELECT2 — and the placeholder is DECLARED, not left to select2 to guess.
     *
     * Measured on the running page (owner, 2026-08-27): without a `placeholder` option, select2 treats the
     * empty `<option value="">` as an ordinary SELECTION and paints its text in the body colour — rgb(56,69,81)
     * here, against rgb(167,172,178) for every plain input's placeholder in the same card. An empty field then
     * looks exactly like a filled one, which is the opposite of what "Seçiniz…" is there to say.
     *
     * Declaring it makes select2 render `.select2-selection__placeholder` instead, which the theme greys like
     * every other hint on the form. The text still comes from the OPTION, so it stays localized in one place —
     * the resx behind the markup — rather than being copied into a data- attribute no language file updates.
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
