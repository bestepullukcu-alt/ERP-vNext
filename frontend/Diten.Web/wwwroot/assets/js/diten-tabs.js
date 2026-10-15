'use strict';

/*
 * DitenTabs — K-TABS (owner decision, 2026-10-15): the behaviour of the shared tab card
 * (Views/Shared/Components/Tabs/_TabCard.cshtml, the Work Center's tab strip as a component).
 *
 * Every `ul[data-diten-tabs]` gets, once:
 *   - a click on a tab shows its panel (aria-controls) and hides its siblings' panels — the Bootstrap tab-pane
 *     classes (`active` + `show`), so the panes keep the page's `tab-pane fade` markup;
 *   - roving tabindex (the active tab 0, the others -1) and aria-selected;
 *   - the keyboard of the WAI-ARIA tabs pattern: ← / → move to the previous / next tab and select it (automatic
 *     activation), Home / End to the first / last; in a right-to-left document ← and → swap (the "next" tab sits on the
 *     left there), Home / End do not;
 *   - a `diten:tab-change` CustomEvent on the tablist (bubbles; detail { key, id, controls }) after each change, so a
 *     page can redraw what was hidden (a DataTable laid out at zero width) without knowing how the strip works.
 *
 * window.DitenTabs.activate(tab) selects a tab from code (a page's "go to that tab"); window.DitenTabs.init(root) binds
 * the strips under root (the document by default; called on DOMContentLoaded). No dependency, no shared file touched.
 */
(function (global) {
    const TAB = '[role="tab"]';

    const tabsOf = (list) => Array.from(list.querySelectorAll(TAB)).filter((t) => !t.disabled);
    const panelOf = (doc, tab) => {
        const id = tab.getAttribute('aria-controls');
        return id ? doc.getElementById(id) : null;
    };
    const isRtl = (el) => {
        const holder = el.closest ? el.closest('[dir]') : null;
        const dir = (holder && holder.getAttribute('dir')) || (el.ownerDocument && el.ownerDocument.documentElement && el.ownerDocument.documentElement.getAttribute('dir')) || '';
        return String(dir).toLowerCase() === 'rtl';
    };

    /** Selects `tab` in its tablist: classes, aria, tabindex, the panels; fires diten:tab-change when it changed. */
    const activate = (tab, options) => {
        if (!tab) { return false; }
        const list = tab.closest('[role="tablist"]');
        if (!list) { return false; }
        const doc = tab.ownerDocument || global.document;
        const wasActive = tab.getAttribute('aria-selected') === 'true';
        tabsOf(list).forEach((t) => {
            const on = t === tab;
            t.classList.toggle('active', on);
            t.setAttribute('aria-selected', on ? 'true' : 'false');
            t.setAttribute('tabindex', on ? '0' : '-1');
            const panel = panelOf(doc, t);
            if (panel) {
                panel.classList.toggle('active', on);
                panel.classList.toggle('show', on);
            }
        });
        if (options && options.focus && typeof tab.focus === 'function') { tab.focus(); }
        if (!wasActive && typeof global.CustomEvent === 'function') {
            list.dispatchEvent(new global.CustomEvent('diten:tab-change', {
                bubbles: true,
                detail: { key: tab.getAttribute('data-tab-key'), id: tab.id, controls: tab.getAttribute('aria-controls') }
            }));
        }
        return true;
    };

    /** The tab a key moves to from `current` (null = the key is not a tab key). */
    const target = (list, current, key) => {
        const tabs = tabsOf(list);
        const at = tabs.indexOf(current);
        if (at < 0 || !tabs.length) { return null; }
        const rtl = isRtl(list);
        if (key === 'Home') { return tabs[0]; }
        if (key === 'End') { return tabs[tabs.length - 1]; }
        const step = key === 'ArrowRight' ? 1 : key === 'ArrowLeft' ? -1 : 0;
        if (!step) { return null; }
        const delta = rtl ? -step : step;
        return tabs[(at + delta + tabs.length) % tabs.length];
    };

    const bind = (list) => {
        if (!list || list.getAttribute('data-diten-tabs-bound') === '1') { return; }
        list.setAttribute('data-diten-tabs-bound', '1');
        list.addEventListener('click', (event) => {
            const tab = event.target.closest(TAB);
            if (!tab || !list.contains(tab) || tab.disabled) { return; }
            event.preventDefault();
            activate(tab);
        });
        list.addEventListener('keydown', (event) => {
            const tab = event.target.closest(TAB);
            if (!tab) { return; }
            const next = target(list, tab, event.key);
            if (!next) { return; }
            event.preventDefault();
            activate(next, { focus: true });
        });
    };

    const init = (root) => {
        const scope = root || global.document;
        if (!scope || typeof scope.querySelectorAll !== 'function') { return 0; }
        const lists = Array.from(scope.querySelectorAll('[data-diten-tabs]'));
        lists.forEach(bind);
        return lists.length;
    };

    global.DitenTabs = { init: init, activate: activate, target: target };
    if (global.document && typeof global.document.addEventListener === 'function') {
        global.document.addEventListener('DOMContentLoaded', () => init());
    }
})(typeof window !== 'undefined' ? window : globalThis);
