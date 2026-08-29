// MOD-0151 FU02 — Territory node form: show the MicroZoneProfile section only when the level is 'microzone'.
// Backend rejects a MicroZoneProfile on any other level, so the UI mirrors that: non-microzone → hidden + cleared,
// which keeps the fields empty so ToNodePayload does not send a profile. No API calls here (pure UX).
(function () {
    'use strict';
    var select = document.getElementById('territory-level-select');
    var section = document.getElementById('microzone-profile-section');
    if (!select || !section) {
        return;
    }

    function toggle() {
        var isMicroZone = (select.value || '').toLowerCase() === 'microzone';
        section.style.display = isMicroZone ? '' : 'none';
        if (!isMicroZone) {
            section.querySelectorAll('input').forEach(function (el) {
                el.value = '';
            });
        }
    }

    select.addEventListener('change', toggle);
    toggle();
})();
