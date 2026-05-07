'use strict';

(function () {
    const trialToggle = document.getElementById('is-trial-plan');
    const trialDays = document.getElementById('trial-duration-days');
    
    const apply = () => {
        if (!trialToggle || !trialDays) return;
        const enabled = !!trialToggle.checked;
        trialDays.disabled = !enabled;
        if (!enabled) {
            trialDays.value = '';
        }
    };

    if (trialToggle) {
        trialToggle.addEventListener('change', apply);
        apply();
    }

    // Bootstrap validation
    const forms = document.querySelectorAll('.needs-validation');
    Array.from(forms).forEach(form => {
        form.addEventListener('submit', event => {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        }, false);
    });
})();

