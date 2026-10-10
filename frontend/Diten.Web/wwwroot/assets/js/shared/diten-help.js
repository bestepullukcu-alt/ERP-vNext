'use strict';

/*
 * DitenHelp — the top bar's "?" help panel (WP-UX-COMMON-01, BL-495).
 *
 * The panel's text is rendered by the server (_PageHelpPanel.cshtml, resx, encoded). This file only:
 *   · opens it from the top-bar button and from the `?` key (diten-shortcuts.js hands `?` here when it exists);
 *   · fills (c), the page's registered shortcuts, from DitenShortcuts.list() — the global `?` entry is not the
 *     page's, and a page that registers none gets no shortcut section at all;
 *   · shows (d), "Start the tour", only when DitenTour says this page's tour can run (library loaded, a tour
 *     registered, a target on the page, the screen at least `md` wide).
 *
 *   DitenHelp.open()      → shows the panel (false when the shell drew none: the unauthorized face)
 *   DitenHelp.pageCode()  → the stable code of the page, or '' when the shell could not tell
 */
(function (global) {
    const doc = global.document;
    if (!doc) { return; }
    const PANEL_ID = 'diten-help-panel';

    const panelEl = () => doc.getElementById(PANEL_ID);
    const pageCode = () => (panelEl() && panelEl().getAttribute('data-page-code')) || '';

    const capOf = (key) => {
        const shortcuts = global.DitenShortcuts;
        return shortcuts && typeof shortcuts.keyCap === 'function' ? shortcuts.keyCap(key) : key;
    };

    // Built with DOM nodes and textContent: a description can never become markup.
    const fillShortcuts = (panel) => {
        const section = panel.querySelector('[data-diten-help-shortcuts]');
        const host = panel.querySelector('[data-diten-help-shortcut-list]');
        if (!section || !host) { return; }
        host.textContent = '';
        const shortcuts = global.DitenShortcuts;
        const groups = shortcuts && typeof shortcuts.list === 'function'
            ? shortcuts.list().filter((group) => group.scope !== 'global' && group.entries.length > 0)
            : [];
        section.hidden = groups.length === 0;
        groups.forEach((group) => {
            const table = doc.createElement('table');
            table.className = 'table table-sm diten-shortcuts-table';
            table.setAttribute('data-diten-help-scope', group.scope);
            const body = doc.createElement('tbody');
            group.entries.forEach((entry) => {
                const row = doc.createElement('tr');
                const keys = doc.createElement('th');
                keys.scope = 'row';
                keys.className = 'diten-shortcuts-keys';
                entry.keys.forEach((key, index) => {
                    if (index > 0) {
                        const or = doc.createElement('span');
                        or.className = 'diten-shortcuts-or';
                        or.textContent = '/';
                        keys.appendChild(or);
                    }
                    const cap = doc.createElement('kbd');
                    cap.className = 'diten-kbd';
                    cap.textContent = capOf(key);
                    keys.appendChild(cap);
                });
                const text = doc.createElement('td');
                text.className = 'diten-shortcuts-desc';
                text.textContent = entry.description;
                row.appendChild(keys);
                row.appendChild(text);
                body.appendChild(row);
            });
            table.appendChild(body);
            host.appendChild(table);
        });
    };

    const tourAvailable = (code) => !!(code && global.DitenTour && typeof global.DitenTour.isAvailable === 'function'
        && global.DitenTour.isAvailable(code));

    const offcanvasOf = (panel) => {
        const bs = global.bootstrap;
        return bs && bs.Offcanvas && typeof bs.Offcanvas.getOrCreateInstance === 'function'
            ? bs.Offcanvas.getOrCreateInstance(panel)
            : null;
    };

    const open = () => {
        const panel = panelEl();
        if (!panel) { return false; }
        fillShortcuts(panel);
        const tour = panel.querySelector('[data-diten-help-tour]');
        if (tour) { tour.hidden = !tourAvailable(pageCode()); }
        const instance = offcanvasOf(panel);
        if (!instance) {
            console.error('[DitenHelp] Bootstrap Offcanvas is unavailable; the help panel cannot open.');
            return false;
        }
        instance.show();
        return true;
    };

    const startTour = () => {
        const panel = panelEl();
        const code = pageCode();
        if (!panel || !tourAvailable(code)) { return false; }
        const instance = offcanvasOf(panel);
        if (instance) {
            // The tour highlights the page, not the panel: start it once the panel is gone.
            panel.addEventListener('hidden.bs.offcanvas', () => global.DitenTour.start(code), { once: true });
            instance.hide();
            return true;
        }
        return global.DitenTour.start(code);
    };

    doc.addEventListener('click', (event) => {
        const target = event.target && event.target.closest ? event.target : null;
        if (!target) { return; }
        if (target.closest('[data-diten-help-open]')) {
            event.preventDefault();
            open();
        } else if (target.closest('[data-diten-help-start-tour]')) {
            event.preventDefault();
            startTour();
        }
    });

    global.DitenHelp = { open, pageCode, startTour };
})(typeof window !== 'undefined' ? window : globalThis);
