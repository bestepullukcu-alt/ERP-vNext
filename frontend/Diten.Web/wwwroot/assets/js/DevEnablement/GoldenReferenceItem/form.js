'use strict';

(function () {
    const init = () => {
        const form = document.getElementById('formGoldenReferenceItem');
        if (!form) {
            return;
        }

        // Initialize Select2 if present
        if (window.jQuery && $.fn.select2) {
            $('.select2').each(function () {
                var $this = $(this);
                $this.select2({
                    dropdownParent: $this.parent(),
                    placeholder: $this.data('placeholder') || 'Select...',
                    allowClear: true
                });
            });
        }

        form.addEventListener('submit', () => {
            const submitButtons = document.querySelectorAll('button[form="formGoldenReferenceItem"][type="submit"]');
            submitButtons.forEach((btn) => {
                btn.disabled = true;
            });
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
        return;
    }

    init();
})();
