'use strict';

// TaskFieldDefinitions Create/Edit form init (flatpickr + Select2).
// Shared by Create.cshtml and Edit.cshtml so the views carry no inline script.
(function () {
document.addEventListener('DOMContentLoaded', function () {
    // Flatpickr
    var flatpickrDate = document.querySelectorAll('.flatpickr-date');
    if (flatpickrDate) {
        flatpickrDate.forEach(function (element) {
            element.flatpickr({
                monthSelectorType: 'static',
                dateFormat: 'Y-m-d'
            });
        });
    }

    // Select2
    var select2Elements = $('.select2');
    if (select2Elements.length) {
        select2Elements.each(function () {
            var $this = $(this);
            $this.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $this.parent()
            });
        });
    }
});
})();
