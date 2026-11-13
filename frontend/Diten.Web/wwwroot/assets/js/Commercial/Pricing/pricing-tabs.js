'use strict';

/* WP-VW-W6 · W6-WEB-b — screen D's five tabs are five pages sharing one tab card: a tab change (diten-tabs.js fires
   diten:tab-change only when the tab really changed) opens that tab's page. */
(function () {
    document.addEventListener('diten:tab-change', (event) => {
        const nav = document.getElementById('pr-tabs-nav');
        const key = event.detail && event.detail.key;
        const href = nav && key ? nav.getAttribute('data-' + key) : null;
        if (href) { window.location.href = href; }
    });
})();
