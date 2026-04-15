'use strict';

(function () {
    const initSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) {
            return;
        }

        const $dropdownParent = $(document.body);
        $('.select2').each(function () {
            const $el = $(this);
            if ($el.hasClass('select2-hidden-accessible')) {
                return;
            }

            $el.select2({
                dropdownParent: $dropdownParent,
                width: '100%',
                allowClear: $el.find('option[value=""]').length > 0
            });
        });
    };

    document.addEventListener('DOMContentLoaded', initSelect2);
})();
