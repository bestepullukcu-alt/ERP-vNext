// Behaviour for the self-contained error/status pages: reload + copy request id.
(function () {
    'use strict';

    document.addEventListener('click', function (e) {
        var reload = e.target.closest('[data-misc-reload]');
        if (reload) {
            window.location.reload();
            return;
        }

        var copy = e.target.closest('[data-misc-copy]');
        if (copy) {
            var value = copy.getAttribute('data-misc-copy') || '';
            var done = function () {
                copy.classList.add('copied');
                window.setTimeout(function () { copy.classList.remove('copied'); }, 1200);
            };
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(value).then(done).catch(function () { });
            }
        }
    });
})();
